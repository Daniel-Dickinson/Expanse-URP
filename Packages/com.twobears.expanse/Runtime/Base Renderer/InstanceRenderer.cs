using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TwoBears.Expanse
{
    [ExecuteAlways]
    public abstract class InstanceRenderer : MonoBehaviour, IAnalyzerInstance
    {
        public ComputeShader preDraw;

        //Core
        public Computation type;
        public GenerationState state;
        public InstanceDataSet dataSet;

        //Performance
        public int priority = 5;
        public bool drawShadows = true;
        public bool pool = false;
        public int poolIndex = 0;
        public UpdateType liveTransform;
        public bool extraData;

        //Debug
        public bool debugLOD;
        public bool debugBounds;
        public bool debugTimes;
        public Camera debugCam;

        //Global Tuning
        public static bool render;
        public static float densityBias = 1;
        public static float distanceBias = 1;
        public static int minPriority = 0;

        //Hidden
        public bool Hidden
        {
            get { return hidden; }
            set { hidden = value; }
        }
        private bool hidden;

        //Camera
        protected Camera Camera
        {
            get
            {
                if (currentCamera == null || !currentCamera.isActiveAndEnabled) currentCamera = Camera.main;
                if (currentCamera == null || !currentCamera.isActiveAndEnabled) currentCamera = FindObjectOfType<Camera>();

                #if UNITY_EDITOR
                if (!Application.isPlaying && SceneView.lastActiveSceneView != null) currentCamera = SceneView.lastActiveSceneView.camera;
                #endif

                return currentCamera;
            }
        }
        protected Camera currentCamera;

        //Sub renderers
        private List<GenRenderer> renderers;

        //Standard
        private List<JobHandle> preDrawHandles;

        //Async generation
        protected GenusData[] data;
        private RendState initState = RendState.Inactive;

        //Timing
        private int timeIndex = 0;
        private float timeSinceUpdate = 0;

        private double[] preDrawTime;
        private double[] updateTime;
        private double[] drawTime;

        private const int timeSamples = 24;

        //Analysis
        public string Name
        {
            get { return gameObject.name; }
        }
        public string DrawShadows
        {
            get { return drawShadows ? "Shadows On" : "Shadows Off"; }
        }
        public int InstanceCount
        {
            get
            {
                if (hidden || priority < minPriority) return 0;

                int total = 0;
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Count; i++)
                    {
                        total += renderers[i].InstanceCount;
                    }
                }
                return total;
            }
        }
        public int VertexCount
        {
            get
            {
                if (hidden || priority < minPriority) return 0;

                int total = 0;
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Count; i++)
                    {
                        total += renderers[i].VertexCount;
                    }
                }

                //Shadows doubls tri count
                if (drawShadows) total *= 2;

                return total;
            }
        }
        public int TriangleCount
        {
            get
            {
                if (hidden || priority < minPriority) return 0;

                int total = 0;
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Count; i++)
                    {
                        total += renderers[i].TriangleCount;
                    }
                }

                //Shadows doubls tri count
                if (drawShadows) total *= 2;

                return total;
            }
        }

        public double PrepTime
        {
            get
            {
                if (hidden || priority < minPriority) return 0;

                double total = 0;
                for (int i = 0; i < timeSamples; i++)
                {
                    if (liveTransform == UpdateType.EveryFrame) total += preDrawTime[i] + updateTime[i];
                    else total += preDrawTime[i];
                }
                return total / timeSamples;
            }
        }
        public double DrawTime
        {
            get
            {
                if (hidden || priority < minPriority) return 0;

                double total = 0;
                for (int i = 0; i < timeSamples; i++) total += drawTime[i];
                return total / timeSamples;
            }
        }

        //DrawType
        private DrawType DrawType
        {
            get
            {
                if (Application.isPlaying)
                {
                    if (type == Computation.Burst) return DrawType.Standard;
                    else return DrawType.Indirect;
                }
                else return DrawType.Standard;
            }
        }

        //Mono
        protected virtual void Awake()
        {
            //Initialize timing arrays
            preDrawTime = new double[timeSamples];
            updateTime = new double[timeSamples];
            drawTime = new double[timeSamples];

        }
        protected virtual void Start()
        {
            //Register
            RegisterToAnalyzer();
        }
        protected virtual void OnDisable()
        {
            DeregisterFromAnalyzer();
            Release();
        }
        protected virtual void Update()
        {
            Initialize();
            RenderLoop();

#if UNITY_EDITOR
            if (!Application.isPlaying) EditorApplication.delayCall += EditorApplication.QueuePlayerLoopUpdate;
#endif
        }

        //Analysis
        private void RegisterToAnalyzer()
        {
            if (!pool) ExpanseAnalyzer.RegisterRenderer(this);
        }
        private void DeregisterFromAnalyzer()
        {
            if (!pool) ExpanseAnalyzer.DeregisterRenderer(this);
        }

        //Init
        public void Queue()
        {
            //Now queued for async
            initState = RendState.Queued;
        }
        public void Initialize()
        {
            //Break out
            if (initState != RendState.Inactive) return;

            //Trigger generation/load step
            switch (state)
            {
                case GenerationState.OnInitialize:
                    data = GenerateData();
                    break;
                case GenerationState.Baked:
                    if (dataSet != null) data = dataSet.LoadData();
                    break;
            }

            //Initialize sub-renderers with data
            for (int i = 0; i < data.Length; i++)
            {
                //initialize a single genus
                InitializeGenus(data[i]);
            }

            //Set final state
            if (pool) initState = RendState.Pooled;
            else initState = RendState.Active;

            //Release data if possible
            if (liveTransform == UpdateType.Never) ReleaseLocalData();
        }
        public IEnumerator InitializeAsync()
        {
            //Break out
            if (initState != RendState.Inactive && initState != RendState.Queued) yield break;

            //Now generating
            initState = RendState.Generating;

            //Trigger generation/load step
            switch (state)
            {
                case GenerationState.OnInitialize:
                    yield return StartCoroutine(GenerateDataAsync());
                    break;
                case GenerationState.Baked:
                    if (dataSet != null) data = dataSet.LoadData();
                    break;
            }

            //Initialize sub-renderers with data
            for (int i = 0; i < data.Length; i++)
            {
                //initialize a single genus
                InitializeGenus(data[i]);
            }

            //Set final state
            if (pool) initState = RendState.Pooled;
            else initState = RendState.Active;

            //Release data if possible
            if (liveTransform == UpdateType.Never) ReleaseLocalData();
        }

        //Generation Step
        protected abstract GenusData[] GenerateData();
        protected abstract IEnumerator GenerateDataAsync();

        //Setup
        protected void UpdateGenus(GenusData dataSet)
        {
            //Data required
            if (dataSet.data.Length == 0) return;

            //Pool interupt
            if (pool) InitializePooled(dataSet);
            else
            {
                //Initialize if required
                if (renderers == null) renderers = new List<GenRenderer>();

                //Check if renderer exists
                for (int i = 0; i < renderers.Count; i++)
                {
                    if (renderers[i].genus == dataSet.genus)
                    {
                        //Release previous renderer
                        renderers[i].Release();

                        //Update new renderer
                        renderers[i].Setup(transform, dataSet.data, dataSet.offset, dataSet.bounds, extraData);
                    }
                }

                //If no renderer exists initialize new renderer
                InitializeGenus(dataSet);
            }
        }
        protected void InitializeGenus(GenusData dataSet)
        {
            //Data required
            if (dataSet.data.Length == 0) return;

            //Pool interupt
            if (pool) InitializePooled(dataSet);
            else InitializeLocal(dataSet);
        }

        private void InitializeLocal(GenusData dataSet)
        {
            //Initialize if required
            if (renderers == null) renderers = new List<GenRenderer>();

            //Setup new renderer for genus
            switch (DrawType)
            {
                case DrawType.Standard:
                    StandardRenderer standard = new StandardRenderer(dataSet.genus, drawShadows, dataSet.matOverride, dataSet.billboardOverride);
                    standard.Setup(transform, dataSet.data, dataSet.offset, dataSet.bounds, extraData);
                    renderers.Add(standard);
                    break;
                case DrawType.Indirect:
                case DrawType.Procedural:
                    ComputeRenderer compute = new ComputeRenderer(dataSet.genus, preDraw, drawShadows, dataSet.matOverride, dataSet.billboardOverride);
                    compute.Setup(transform, dataSet.data, dataSet.offset, dataSet.bounds, extraData);
                    renderers.Add(compute);
                    break;
            }            
        }
        private void InitializePooled(GenusData dataSet)
        {
            InstancePool.Register(this, dataSet);
        }

        //Core
        private void RenderLoop()
        {
            if (initState != RendState.Active) return;

            PerformCulling();
            UpdateLoop();

            PreDraw(DrawType);
            Draw();
        }
        private void Release()
        {
            //Clear out pooling
            if (initState == RendState.Pooled)
            {
                InstancePool.Deregister(this);
            }

            //Clear out renderers
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Count; i++) renderers[i].Release();
                renderers = null;
            }

            //Clear out local data
            ReleaseLocalData();

            //No longer initialized
            initState = RendState.Inactive;
        }
        private void ReleaseLocalData()
        {
            if (data == null) return;

            //Dispose native arrays
            for (int i = 0; i < data.Length; i++)
            {
                NativeList<GenusInstance> list = data[i].data;
                if (list.IsCreated) list.Dispose();
            }

            //Data now null
            data = null;
        }

        //Culling
        protected virtual void PerformCulling()
        {
            
        }

        //Update
        private void UpdateLoop()
        {
            if (liveTransform == UpdateType.Never) return;

            if (liveTransform == UpdateType.EveryFrame) UpdateData();
            else
            {
                timeSinceUpdate += Time.deltaTime;
                if (timeSinceUpdate > 1)
                {
                    //Perform single update
                    UpdateData();

                    //If only updating once swap to never
                    if (liveTransform == UpdateType.Once)
                    {
                        //Don't continue updating
                        liveTransform = UpdateType.Never;

                        //Release data
                        ReleaseLocalData();
                    }
                }
            }
        }
        private void UpdateData()
        {
            double startTime = Time.realtimeSinceStartupAsDouble;

            //Check priority
            if (renderers != null && !hidden && priority >= minPriority)
            {
                for (int i = 0; i < renderers.Count; i++) renderers[i].Update(transform, data[i].data);
            }

            //Record timing
            updateTime[timeIndex] = (Time.realtimeSinceStartupAsDouble - startTime) * 1000; //Convert to milliseconds
        }

        //Pre Draw
        private void PreDraw(DrawType type = DrawType.Indirect)
        {
            double startTime = Time.realtimeSinceStartupAsDouble;

            //Check priority
            if (!hidden && priority >= minPriority)
            {
                switch (type)
                {
                    case DrawType.Standard:
                        PreDrawStandard();
                        break;
                    case DrawType.Procedural:
                    case DrawType.Indirect:
                        PreDrawCompute();
                        break;
                }
            }

            //Record timing
            preDrawTime[timeIndex] = (Time.realtimeSinceStartupAsDouble - startTime) * 1000; //Convert to milliseconds
        }
        private void PreDrawStandard()
        {
            //Initialize/Clear handles
            if (preDrawHandles == null) preDrawHandles = new List<JobHandle>();

            //Schedule Pre-Draw
            SchedulePreDrawStandard(ref preDrawHandles);

            //Complete Pre-Draw
            foreach (JobHandle handle in preDrawHandles) handle.Complete();
            preDrawHandles.Clear();

            //Schedule Collection
            ScheduleCollectStandard(ref preDrawHandles);

            //Complete Collection
            foreach (JobHandle handle in preDrawHandles) handle.Complete();
            preDrawHandles.Clear();
        }
        private void SchedulePreDrawStandard(ref List<JobHandle> handles)
        {
            //Renderers required
            if (renderers == null) return;

            //Grab camera
            Camera cam = (debugCam == null || !debugCam.isActiveAndEnabled) ? debugCam : Camera;

            //Valid camera required
            if (cam == null) return;

            //Schedule pre draw on all renderers
            foreach (StandardRenderer renderer in renderers) renderer.ScheduleCull(cam, ref handles);
        }
        private void ScheduleCollectStandard(ref List<JobHandle> handles)
        {
            if (renderers == null) return;
            if (currentCamera == null) return;

            foreach (StandardRenderer renderer in renderers)
            {
                renderer.ScheduleCollect(ref handles);
            }
        }
        private void PreDrawCompute()
        {
            //Renderers required
            if (renderers == null) return;

            //Grab camera
            Camera cam = (debugCam == null || !debugCam.isActiveAndEnabled) ? debugCam : Camera;

            //Valid camera required
            if (cam == null) return;

            //Schedule pre draw on all renderers
            foreach (ComputeRenderer renderer in renderers) renderer.PreDraw(cam);
        }

        //Draw
        private void Draw()
        {
            if (renderers == null) return;
            if (currentCamera == null) return;

            double startTime = Time.realtimeSinceStartupAsDouble;

            //Check priority
            if (renderers != null && !hidden && priority >= minPriority)
            {
                foreach (GenRenderer renderer in renderers) renderer.Draw(transform);
            }

            //Record timing
            drawTime[timeIndex] = (Time.realtimeSinceStartupAsDouble - startTime) * 1000; //Convert to milliseconds

            //Increment time index
            timeIndex++;
            if (timeIndex >= timeSamples) timeIndex = 0;
        }

        //Editor
#if UNITY_EDITOR
        public void UpdateGenus()
        {
            if (!gameObject.activeInHierarchy) return;

            //Release current genus
            if (renderers != null && renderers.Count > 0)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    renderers[i].Release();
                }

                //Clear current renderers
                renderers.Clear();
            }

            //Calculate data
            GenusData[] generated = GenerateData();

            //Save if required
            if (state == GenerationState.Baked && dataSet != null)
            {
                dataSet.SaveData(generated);
            }

            //Initialize new data
            for (int i = 0; i < generated.Length; i++)
            {
                InitializeGenus(generated[i]);
            }

        }
        public void BakeData()
        {
            //Make sure data is valid
            if (dataSet == null) return;

            //Calculate & save
            GenusData[] generated = GenerateData();
            dataSet.SaveData(generated);

            //Update Genus
            for (int i = 0; i < generated.Length; i++)
            {
                UpdateGenus(generated[i]);
            }
        }

        //Gizmos
        public void OnDrawGizmosSelected()
        {
            if (renderers != null && debugBounds) DrawGizmoBounds(renderers);
        }
        protected virtual void DrawGizmoBounds(List<GenRenderer> renderers)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                GenRenderer rend = renderers[i];
                Genus genus = rend.genus;

                Matrix4x4[] transforms = rend.Transforms;

                for (int p = 0; p < transforms.Length; p++)
                {
                    Vector3 instancePosition = transforms[p].GetColumn(3);
                    Gizmos.DrawWireCube(instancePosition + genus.boundsCenter, genus.boundsExtents * 2);
                }
            }
        }
#endif
    }

    //Serialized
    [System.Serializable]
    public struct GenusData
    {
        public Genus genus;

        public Material matOverride;
        public Material billboardOverride;

        public Vector3 offset;
        public Vector3 bounds;

        public NativeList<GenusInstance> data;

        public GenusData(Genus genus, Material matOverride, Material billboardOverride, NativeList<GenusInstance> data, Vector3 offset, Vector3 bounds)
        {
            this.genus = genus;

            this.matOverride = matOverride;
            this.billboardOverride = billboardOverride;

            this.offset = offset;
            this.bounds = bounds;

            this.data = data;
        }
    }

    [System.Serializable]
    public struct GenusInstance
    {
        public float density;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector4 extra;

        public GenusInstance(Vector3 position, float density = 1)
        {
            this.density = density;
            this.position = position;
            this.rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(0, 360), Vector3.up);
            this.scale = Vector3.one;
            this.extra = Vector4.zero;
        }
        public GenusInstance(Vector3 position, Vector3 scale, float density = 1)
        {
            this.density = density;
            this.position = position;
            this.rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(0, 360), Vector3.up);
            this.scale = scale;
            this.extra = Vector4.zero;
        }
        public GenusInstance(Vector3 position, float rotation, Vector3 scale, float density = 1)
        {
            this.density = density;
            this.position = position;
            this.rotation = Quaternion.AngleAxis(rotation, Vector3.up);
            this.scale = scale;
            this.extra = Vector4.zero;
        }
        public GenusInstance(Vector3 position, float rotation, Vector3 scale, Vector4 extra, float density = 1)
        {
            this.density = density;
            this.position = position;
            this.rotation = Quaternion.AngleAxis(rotation, Vector3.up);
            this.scale = scale;
            this.extra = extra;
        }
        public GenusInstance(Vector3 position, Quaternion rotation, Vector3 scale, float density = 1)
        {
            this.density = density;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            this.extra = Vector4.zero;
        }
        public GenusInstance(Vector3 position, Quaternion rotation, Vector3 scale, Vector4 extra, float density = 1)
        {
            this.density = density;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            this.extra = extra;
        }

    }

    [System.Serializable]
    public enum GenerationState { OnInitialize, Baked }

    //Renderer
    public abstract class GenRenderer
    {
        public Genus genus;
        protected Vector3 offset;
        protected Vector3 bounds;

        //Analysis
        public abstract int VertexCount
        {
            get;
        }
        public abstract int TriangleCount
        {
            get;
        }
        public abstract int InstanceCount
        {
            get;
        }
        public abstract int MemorySize
        {
            get;
        }
        public abstract Matrix4x4[] Transforms
        {
            get;
        }

        //Standard data management
        public abstract void Setup(Transform transform, NativeArray<GenusInstance> data, Vector3 offset, Vector3 bounds, bool extraData);
        public abstract void Alter(Transform transform, NativeArray<GenusInstance> data);
        public abstract void Update(Transform transform, NativeArray<GenusInstance> data);

        //Core
        public abstract void Draw(Transform transform);

        //Release
        public abstract void Release();
    }

    //Standard
    public class StandardRenderer : GenRenderer
    {
        //Core
        private bool processExtraData;

        private StandardInstance lod0;
        private StandardInstance lod1;
        private StandardInstance lod2;
        private StandardInstance lod3;

        private int drawCount;
        private int totalCount;

        //Standard
        public NativeArray<float> densities;
        public NativeArray<Matrix4x4> transforms;
        public NativeArray<StandardShaderData> extra;

        private ComputeBuffer extraBuffer;
        private NativeArray<int> states;
        private PreDrawJob preDraw;

        //Analysis
        public override int VertexCount
        {
            get { return lod0.VertCount + lod1.VertCount + lod2.VertCount; }
        }
        public override int TriangleCount
        {
            get { return lod0.TriangleCount + lod1.TriangleCount + lod2.TriangleCount; }
        }
        public override int InstanceCount
        {
            get { return lod0.InstanceCount + lod1.InstanceCount + lod2.InstanceCount; }
        }
        public override int MemorySize
        {
            get
            {
                int total = 0;
                return total;
            }
        }
        public override Matrix4x4[] Transforms
        {
            get { return transforms.ToArray(); }
        }

        //Constructor
        public StandardRenderer(Genus genus, bool drawShadows, Material matOverride = null, Material matBillboardOverride = null)
        {
            this.genus = genus;

            //Base setup
            if (matOverride != null)
            {
                lod0 = new StandardInstance(genus.lod0, matOverride, drawShadows);
                lod1 = new StandardInstance(genus.lod1, matOverride, drawShadows);
                lod2 = new StandardInstance(genus.lod2, matOverride, drawShadows);
            }
            else
            {
                lod0 = new StandardInstance(genus.lod0, genus.baseMaterial, drawShadows);
                lod1 = new StandardInstance(genus.lod1, genus.baseMaterial, drawShadows);
                lod2 = new StandardInstance(genus.lod2, genus.baseMaterial, drawShadows);
            }

            //Billboard setup
            if (genus.UseLOD3)
            {
                lod3 = new StandardInstance(genus.billboard, (matBillboardOverride != null) ? matBillboardOverride : genus.billboardMaterial, false);
            }
        }

        //Core data management
        private void Resize(int maxCount)
        {
            //Dispose old data sets
            if (densities.IsCreated) densities.Dispose();
            if (transforms.IsCreated) transforms.Dispose();
            if (extra.IsCreated) extra.Dispose();
            
            if (states.IsCreated) states.Dispose();
            if (extraBuffer != null) extraBuffer.Dispose();

            //Initialize data sets
            densities = new NativeArray<float>(maxCount, Allocator.Persistent);
            transforms = new NativeArray<Matrix4x4>(maxCount, Allocator.Persistent);
            if (processExtraData) extra = new NativeArray<StandardShaderData>(maxCount, Allocator.Persistent);

            states = new NativeArray<int>(maxCount, Allocator.Persistent);
            if (processExtraData) extraBuffer = new ComputeBuffer(maxCount, StandardShaderData.Size);

            //Reassign to job
            preDraw.densities = densities;
            preDraw.transforms = transforms;
            preDraw.states = states;
        }

        //Standard data management
        public override void Setup(Transform transform, NativeArray<GenusInstance> data, Vector3 offset, Vector3 bounds, bool extraData)
        {
            //Core
            this.offset = offset;
            this.bounds = bounds;

            //Secondary
            processExtraData = extraData;

            //Initialize job
            preDraw = new PreDrawJob()
            {
                distanceBias = InstanceRenderer.distanceBias,
                densityBias = InstanceRenderer.distanceBias,

                boundsCenter = genus.boundsCenter,
                boundsExtents = genus.boundsExtents,
                useBillboard = genus.UseLOD3,
                cullDistance = genus.cullDistance,
                lod3Distance = genus.billboardDistance,
                lod2Distance = genus.lod2Distance,
                lod1Distance = genus.lod1Distance,
                optDistance = genus.optDistance,
            };

            //Setup Renderers
            lod0.SetupStandard();
            lod1.SetupStandard();
            lod2.SetupStandard();
            if (genus.UseLOD3) lod3.SetupStandard();

            //Setup Data
            SetupData(transform, data);
        }
        public override void Alter(Transform transform, NativeArray<GenusInstance> data)
        {
            //Setup new data
            SetupData(transform, data);
        }
        public override void Update(Transform transform, NativeArray<GenusInstance> data)
        {
            //Don't update if data doesn't match
            if (data.Length != transforms.Length) return;

            //Overwrite transform data
            for (int i = 0; i < drawCount; i++)
            {
                GenusInstance d = data[i];
                transforms[i] = Matrix4x4.TRS(transform.position + (transform.rotation * (d.position + offset)), d.rotation, d.scale);
            }
        }
        
        private void SetupData(Transform transform, NativeArray<GenusInstance> data)
        {
            //Set new count
            if (drawCount != data.Length)
            {
                drawCount = data.Length;
                preDraw.drawCount = drawCount;
            }

            //Don't generate if no data
            if (drawCount == 0) return;

            //Check if we need to resize arrays
            if (totalCount != data.Length)
            {
                //Populate backend buffers
                Resize(data.Length);
            }

            //Populate data
            for (int i = 0; i < drawCount; i++)
            {
                //Grab instance
                GenusInstance d = data[i];

                //Transforms
                transforms[i] = Matrix4x4.TRS(transform.position + (transform.rotation * (d.position + offset)), d.rotation, d.scale);

                //Density
                densities[i] = d.density;

                //Extra
                if (processExtraData) extra[i] = new StandardShaderData(data[i].extra);
            }

            //Apply extra data
            if (processExtraData) extraBuffer.SetData(extra);
        }

        //Burst data management
        public void Setup(NativeArray<float> densities, NativeArray<Matrix4x4> transforms, int drawCount, Vector3 offset, Vector3 bounds, bool extraData)
        {
            //Core
            this.offset = offset;
            this.bounds = bounds;

            //Secondary
            processExtraData = extraData;

            //Initialize job
            preDraw = new PreDrawJob()
            {
                distanceBias = InstanceRenderer.distanceBias,
                densityBias = InstanceRenderer.distanceBias,

                boundsCenter = genus.boundsCenter,
                boundsExtents = genus.boundsExtents,
                useBillboard = genus.UseLOD3,
                cullDistance = genus.cullDistance,
                lod3Distance = genus.billboardDistance,
                lod2Distance = genus.lod2Distance,
                lod1Distance = genus.lod1Distance,
                optDistance = genus.optDistance,
            };

            //Setup Renderers
            lod0.SetupStandard();
            lod1.SetupStandard();
            lod2.SetupStandard();
            if (genus.UseLOD3) lod3.SetupStandard();

            //Setup Data
            SetupData(densities, transforms, drawCount);
        }
        public void Setup(NativeArray<float> densities, NativeArray<Matrix4x4> transforms, NativeArray<StandardShaderData> extra, int drawCount, Vector3 offset, Vector3 bounds, bool extraData)
        {
            //Core
            this.offset = offset;
            this.bounds = bounds;

            //Secondary
            processExtraData = extraData;

            //Initialize job
            preDraw = new PreDrawJob()
            {
                distanceBias = InstanceRenderer.distanceBias,
                densityBias = InstanceRenderer.distanceBias,

                boundsCenter = genus.boundsCenter,
                boundsExtents = genus.boundsExtents,
                useBillboard = genus.UseLOD3,
                cullDistance = genus.cullDistance,
                lod3Distance = genus.billboardDistance,
                lod2Distance = genus.lod2Distance,
                lod1Distance = genus.lod1Distance,
                optDistance = genus.optDistance,
            };

            //Setup Renderers
            lod0.SetupStandard();
            lod1.SetupStandard();
            lod2.SetupStandard();
            if (genus.UseLOD3) lod3.SetupStandard();

            //Setup Data
            SetupData(densities, transforms, extra, drawCount);
        }

        public void Alter(NativeArray<float> densities, NativeArray<Matrix4x4> transforms, int drawCount)
        {
            //Setup new data
            SetupData(densities, transforms, drawCount);
        }
        public void Alter(NativeArray<float> densities, NativeArray<Matrix4x4> transforms, NativeArray<StandardShaderData> extra, int drawCount)
        {
            //Setup new data
            SetupData(densities, transforms, extra, drawCount);
        }

        private void SetupData(NativeArray<float> densities, NativeArray<Matrix4x4> transforms, int drawCount)
        {
            if (this.drawCount != drawCount)
            {
                //Set new draw count
                this.drawCount = drawCount;

                //Set max count on pre draw
                preDraw.drawCount = drawCount;
            }

            //Don't generate if no data
            if (this.drawCount == 0) return;

            //Check if we need to resize arrays
            if (totalCount != densities.Length) Resize(densities.Length);

            //Copy data to local arrays
            for (int i = 0; i < densities.Length; i++)
            {
                this.densities[i] = densities[i];
                this.transforms[i] = transforms[i];
            }
        }
        private void SetupData(NativeArray<float> densities, NativeArray<Matrix4x4> transforms, NativeArray<StandardShaderData> extra, int drawCount)
        {
            if (this.drawCount != drawCount)
            {
                //Set new draw count
                this.drawCount = drawCount;

                //Set max count on pre draw
                preDraw.drawCount = drawCount;
            }

            //Don't generate if no data
            if (this.drawCount == 0) return;

            //Check if we need to resize arrays
            if (totalCount != densities.Length) Resize(densities.Length);

            //Copy data to local arrays
            for (int i = 0; i < densities.Length; i++)
            {
                this.densities[i] = densities[i];
                this.transforms[i] = transforms[i];
                this.extra[i] = extra[i];
            }

            //Set data
            extraBuffer.SetData(extra);
        }

        //Release
        public override void Release()
        {
            //Instance Buffers
            lod0.Release();
            lod1.Release();
            lod2.Release();
            if (lod3 != null) lod3.Release();

            //Job data
            if (densities.IsCreated) densities.Dispose();
            if (transforms.IsCreated) transforms.Dispose();
            if (extra.IsCreated) extra.Dispose();

            if (states.IsCreated) states.Dispose();

            //Extra Buffers
            if (extraBuffer != null)
            {
                extraBuffer.Release();
                extraBuffer = null;
            }
        }

        //Predraw
        public void ScheduleCull(Camera camera, ref List<JobHandle> handles)
        {
            if (drawCount == 0) return;

            //Calculate camera vp matrix
            Matrix4x4 cameraVPMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;

            //Setup pre draw job
            preDraw.densityBias = InstanceRenderer.densityBias;
            preDraw.distanceBias = InstanceRenderer.distanceBias;
            preDraw.cameraPosition = camera.transform.position;
            preDraw.cameraVPMatrix = cameraVPMatrix;

            //Schedule job
            handles.Add(preDraw.Schedule(drawCount, 100));
        }
        public void ScheduleCollect(ref List<JobHandle> handles)
        {
            if (drawCount == 0) return;

            handles.Add(lod0.Collect(transforms, states, drawCount, 0));
            handles.Add(lod1.Collect(transforms, states, drawCount, 1));
            handles.Add(lod2.Collect(transforms, states, drawCount, 2));
            if (genus.UseLOD3) handles.Add(lod3.Collect(transforms, states, drawCount, 3));
        }

        //Draw
        public override void Draw(Transform transform)
        {
            if (drawCount == 0) return;

            if (extraBuffer != null)
            {
                lod0.Draw(extraBuffer);
                lod1.Draw(extraBuffer);
                lod2.Draw(extraBuffer);
                if (genus.UseLOD3) lod3.Draw(extraBuffer);
            }
            else
            {
                lod0.Draw();
                lod1.Draw();
                lod2.Draw();
                if (genus.UseLOD3) lod3.Draw();
            }
        }

        //Predraw Job
        [BurstCompile]
        public struct PreDrawJob : IJobParallelFor
        {
            public int drawCount;

            public float3 boundsCenter;
            public float3 boundsExtents;

            public bool useBillboard;

            public float optDistance;
            public float lod1Distance;
            public float lod2Distance;
            public float lod3Distance;
            public float cullDistance;

            public float distanceBias;
            public float densityBias;

            [ReadOnly] public NativeArray<float> densities;
            [ReadOnly] public NativeArray<Matrix4x4> transforms;

            public NativeArray<int> states;

            public float3 cameraPosition;
            public Matrix4x4 cameraVPMatrix;

            public void Execute(int index)
            {
                if (index > drawCount) return;

                Vector3 position = transforms[index].GetColumn(3);
                float density = densities[index];

                //Calculate distance
                float distance = Vector3.Distance(position, cameraPosition);

                //Cull
                if (!CullInstance(position, distance))
                {
                    //LOD
                    states[index] = CalculateLOD(distance, density);
                }
                else states[index] = -1;
            }

            private bool CullInstance(float3 position, float distance)
            {
                //Always keep instances within opt distance
                if (distance < optDistance) return false;

                //Calculate min max points
                float3 min = position + boundsCenter - boundsExtents;
                float3 max = position + boundsCenter + boundsExtents;

                //If any point in frustum return false
                if (!PointOutsideCameraFrustum(cameraVPMatrix, new float4(min.x, min.y, min.z, 1))) return false;
                if (!PointOutsideCameraFrustum(cameraVPMatrix, new float4(min.x, min.y, max.z, 1))) return false;
                if (!PointOutsideCameraFrustum(cameraVPMatrix, new float4(min.x, max.y, min.z, 1))) return false;
                if (!PointOutsideCameraFrustum(cameraVPMatrix, new float4(min.x, max.y, max.z, 1))) return false;
                if (!PointOutsideCameraFrustum(cameraVPMatrix, new float4(max.x, min.y, min.z, 1))) return false;
                if (!PointOutsideCameraFrustum(cameraVPMatrix, new float4(max.x, min.y, max.z, 1))) return false;
                if (!PointOutsideCameraFrustum(cameraVPMatrix, new float4(max.x, max.y, min.z, 1))) return false;
                if (!PointOutsideCameraFrustum(cameraVPMatrix, new float4(max.x, max.y, max.z, 1))) return false;

                //If no points in frustum cull
                return true;
            }
            private bool PointOutsideCameraFrustum(Matrix4x4 cameraVPMatrix, float4 boxPosition)
            {
                float4 clipPosition = cameraVPMatrix * boxPosition;
                return (clipPosition.z > clipPosition.w
                    || clipPosition.x < -clipPosition.w || clipPosition.x > clipPosition.w
                    || clipPosition.y < -clipPosition.w || clipPosition.y > clipPosition.w
                    );
            }
            private int CalculateLOD(float distance, float density)
            {
                //Check density
                if (density < densityBias) return 0;

                //Check LOD
                if (distance > cullDistance * distanceBias) return -1;
                if (useBillboard && distance > lod3Distance * distanceBias) return 3;
                if (distance > lod2Distance * distanceBias) return 2;
                if (distance > lod1Distance * distanceBias) return 1;
                else return 0;
            }
        }
    }
    public class StandardInstance
    {
        private Mesh instanceMesh;
        private Material instanceMaterial;
        private ShadowCastingMode shadowMode;

        //Storage
        private int vertCount;
        private int triCount;

        //Core Data
        private List<MaterialPropertyBlock> blocks;
        private List<ComputeBuffer> indexBuffers;
        private List<Matrix4x4> batchTransforms;
        private List<int> batchIndicies;

        //Collection Step
        private NativeList<Matrix4x4> matricies;
        private NativeList<int> indicies;

        //Debug
        public int VertCount
        {
            get { return vertCount * drawnCount; }
        }
        public int TriangleCount
        {
            get { return triCount * drawnCount; }
        }
        public int InstanceCount
        {
            get { return drawnCount; }
        }

        private int drawnCount;

        //Constructor
        public StandardInstance(Mesh mesh, Material material, bool drawShadows)
        {
            //Initialize
            instanceMesh = mesh;
            instanceMaterial = new Material(material);

            shadowMode = (drawShadows) ? ShadowCastingMode.On : ShadowCastingMode.Off;

            vertCount = mesh.vertexCount;
            triCount = instanceMesh.isReadable ? instanceMesh.GetTriangles(0).Length : Mathf.CeilToInt(vertCount * 0.75f);
        }

        //Setup
        public void SetupStandard()
        {
            //Initialize standard
            blocks = new List<MaterialPropertyBlock>();
            if (indexBuffers == null) indexBuffers = new List<ComputeBuffer>();

            if (!matricies.IsCreated) matricies = new NativeList<Matrix4x4>(Allocator.Persistent);
            if (!indicies.IsCreated) indicies = new NativeList<int>(Allocator.Persistent);

            batchTransforms = new List<Matrix4x4>();
            batchIndicies = new List<int>();

            //Keywords
            instanceMaterial.EnableKeyword("_INSTANCETYPE_STANDARD");
            instanceMaterial.DisableKeyword("_INSTANCETYPE_COMPUTE");
        }

        //Release
        public void Release()
        {
            //Standard
            if (indexBuffers != null)
            {
                for (int i = 0; i < indexBuffers.Count; i++)
                {
                    if (indexBuffers[i] != null)
                    {
                        indexBuffers[i].Release();
                        indexBuffers[i] = null;
                    }
                }
            }

            //Material
            Object.DestroyImmediate(instanceMaterial);

            //Collections
            if (matricies.IsCreated) matricies.Dispose();
            if (indicies.IsCreated) indicies.Dispose();
        }

        //PreDraw
        public JobHandle Collect(NativeArray<Matrix4x4> transforms, NativeArray<int> states, int totalCount, int stateIndex)
        {
            matricies.Clear();
            indicies.Clear();

            CollectionJob job = new CollectionJob()
            {
                totalCount = totalCount,
                transforms = transforms,
                states = states,
                stateIndex = stateIndex,
                matricies = matricies,
                indicies = indicies
            };

            return job.Schedule();
        }

        //Draw
        public void Draw()
        {
            //Clear count
            drawnCount = 0;

            for (int i = 0; i < matricies.Length; i++)
            {
                batchTransforms.Add(matricies[i]);

                //Draw & clear as needed
                if (batchTransforms.Count == 1023 || (i == matricies.Length - 1 && batchTransforms.Count > 0))
                {
                    //Draw batch
                    Graphics.DrawMeshInstanced(instanceMesh, 0, instanceMaterial, batchTransforms, null, shadowMode);

                    //Record count
                    drawnCount += batchTransforms.Count;

                    //Clear matricies
                    batchTransforms.Clear();
                }
            }
        }
        public void Draw(ComputeBuffer extra)
        {
            //Clear count
            drawnCount = 0;

            //Buffer
            if (extra != null && !instanceMaterial.HasBuffer("_Extra")) instanceMaterial.SetBuffer("_Extra", extra);

            //Init Data
            int blockIndex = 0;

            for (int i = 0; i < matricies.Length; i++)
            {
                batchTransforms.Add(matricies[i]);
                batchIndicies.Add(indicies[i]);

                //Draw & clear as needed
                if (batchTransforms.Count == 440 || (i == matricies.Length - 1 && batchTransforms.Count > 0))
                {
                    //Setup property block
                    if (blocks.Count <= blockIndex)
                    {
                        MaterialPropertyBlock block = new MaterialPropertyBlock();
                        ComputeBuffer buffer = new ComputeBuffer(1023, sizeof(int));
                        block.SetBuffer("_LOD", buffer);

                        blocks.Add(block);
                        indexBuffers.Add(buffer);
                    }

                    //Clear & setup
                    indexBuffers[blockIndex].SetData(batchIndicies);

                    //Draw batch with property block
                    Graphics.DrawMeshInstanced(instanceMesh, 0, instanceMaterial, batchTransforms, blocks[blockIndex], shadowMode);

                    //Record count
                    drawnCount += batchTransforms.Count;

                    //Clear matricies
                    batchTransforms.Clear();
                    batchIndicies.Clear();

                    //Increase block index
                    blockIndex++;
                }
            }
        }

        public struct CollectionJob : IJob
        {
            [ReadOnly] public int totalCount;
            [ReadOnly] public NativeArray<Matrix4x4> transforms;
            [ReadOnly] public NativeArray<int> states;

            public int stateIndex;

            public NativeList<Matrix4x4> matricies;
            public NativeList<int> indicies;

            public void Execute()
            {
                for (int i = 0; i < totalCount; i++)
                {
                    if (states[i] == stateIndex)
                    {
                        matricies.Add(transforms[i]);
                        indicies.Add(i);
                    }
                }
            }
        }
    }

    //Compute
    public class ComputeRenderer : GenRenderer
    {
        //Core
        private ComputeInstance lod0;
        private ComputeInstance lod1;
        private ComputeInstance lod2;
        private ComputeInstance lod3;

        private int drawCount;
        private int totalCount;

        //Compute
        private ComputeShaderData[] posData;
        private ComputeShader preDraw;
        private ComputeBuffer positionBuffer;
        private ComputeBuffer lod0Buffer;
        private ComputeBuffer lod1Buffer;
        private ComputeBuffer lod2Buffer;
        private ComputeBuffer lod3Buffer;

        //Shader Ids
        private int posBufferID;

        private int lod0ID;
        private int lod1ID;
        private int lod2ID;
        private int lod3ID;

        private int optDistID;
        private int lod1DistID;
        private int lod2DistID;
        private int lod3DistID;
        private int cullDistID;

        private int rootID;
        private int maxCountID;
        private int bCenterID;
        private int bExtentID;

        private int use1ID;
        private int use2ID;
        private int use3ID;

        //Analysis
        public override int VertexCount
        {
            get 
            {
                int total = 0;
                if (lod0 != null) total += lod0.VertCount;
                if (lod1 != null) total += lod1.VertCount;
                if (lod2 != null) total += lod2.VertCount;
                return total; 
            }
        }
        public override int TriangleCount
        {
            get
            {
                int total = 0;
                if (lod0 != null) total += lod0.TriangleCount;
                if (lod1 != null) total += lod1.TriangleCount;
                if (lod2 != null) total += lod2.TriangleCount;
                return total;
            }
        }
        public override int InstanceCount
        {
            get
            {
                int total = 0;
                if (lod0 != null) total += lod0.InstanceCount;
                if (lod1 != null) total += lod1.InstanceCount;
                if (lod2 != null) total += lod2.InstanceCount;
                return total;
            }
        }
        public override int MemorySize
        {
            get
            {
                int total = 0;

                if (positionBuffer != null) total += (positionBuffer.stride * positionBuffer.count);

                if (lod0Buffer != null) total += (lod0Buffer.stride * lod0Buffer.count);
                if (lod1Buffer != null) total += (lod1Buffer.stride * lod1Buffer.count);
                if (lod2Buffer != null) total += (lod2Buffer.stride * lod2Buffer.count);
                if (lod3Buffer != null) total += (lod3Buffer.stride * lod3Buffer.count);

                return total;
            }
        }
        public override Matrix4x4[] Transforms
        {
            get
            {
                Matrix4x4[] ret = new Matrix4x4[drawCount];
                for (int i = 0; i < drawCount; i++)
                {
                    ret[i] = posData[i].position;
                }
                return ret;
            }
        }

        //Constructor
        public ComputeRenderer(Genus genus, ComputeShader shader, bool drawShadows, Material matOverride = null, Material matBillboardOverride = null)
        {
            this.genus = genus;

            //Shader IDs
            posBufferID = Shader.PropertyToID("_PositionBuffer");

            lod0ID = Shader.PropertyToID("_LOD0");
            lod1ID = Shader.PropertyToID("_LOD1");
            lod2ID = Shader.PropertyToID("_LOD2");
            lod3ID = Shader.PropertyToID("_LOD3");

            optDistID = Shader.PropertyToID("_OptDistance");
            lod1DistID = Shader.PropertyToID("_LOD1Distance");
            lod2DistID = Shader.PropertyToID("_LOD2Distance");
            lod3DistID = Shader.PropertyToID("_LOD3Distance");
            cullDistID = Shader.PropertyToID("_CullDistance");

            rootID = Shader.PropertyToID("_RootPosition");
            maxCountID = Shader.PropertyToID("_MaxCount");
            bCenterID = Shader.PropertyToID("_BoundsCenter");
            bExtentID = Shader.PropertyToID("_BoundsExtent");

            use1ID = Shader.PropertyToID("_UseLOD1");
            use2ID = Shader.PropertyToID("_UseLOD2");
            use3ID = Shader.PropertyToID("_UseLOD3");

            //Base setup
            if (matOverride != null)
            {
                lod0 = new ComputeInstance(genus.lod0, matOverride, drawShadows);
                if (genus.UseLOD1) lod1 = new ComputeInstance(genus.lod1, matOverride, drawShadows);
                if (genus.UseLOD2) lod2 = new ComputeInstance(genus.lod2, matOverride, drawShadows);
            }
            else
            {
                lod0 = new ComputeInstance(genus.lod0, genus.baseMaterial, drawShadows);
                if (genus.UseLOD1) lod1 = new ComputeInstance(genus.lod1, genus.baseMaterial, drawShadows);
                if (genus.UseLOD2) lod2 = new ComputeInstance(genus.lod2, genus.baseMaterial, drawShadows);
            }

            //Billboard setup
            if (genus.UseLOD3)
            {
                lod3 = new ComputeInstance(genus.billboard, (matBillboardOverride != null) ? matBillboardOverride : genus.billboardMaterial, false);
            }

            preDraw = Object.Instantiate(shader);
        }

        //Core data management
        private void Resize(int maxCount)
        {
            //Setup or resize position buffers
            if (positionBuffer != null) positionBuffer.Dispose();

            //Create new buffer
            positionBuffer = new ComputeBuffer(maxCount, ComputeShaderData.Size);

            //Attach to predraw
            preDraw.SetBuffer(0, posBufferID, positionBuffer);

            //Attach to LODs
            lod0.SetupPositions(positionBuffer);
            if (genus.UseLOD1) lod1.SetupPositions(positionBuffer);
            if (genus.UseLOD2) lod2.SetupPositions(positionBuffer);
            if (genus.UseLOD3) lod3.SetupPositions(positionBuffer);

            //Setup LOD buffers
            if (lod0Buffer != null) lod0Buffer.Dispose();
            if (lod1Buffer != null) lod1Buffer.Dispose();
            if (lod2Buffer != null) lod2Buffer.Dispose();
            if (lod3Buffer != null) lod3Buffer.Dispose();

            lod0Buffer = new ComputeBuffer(maxCount, sizeof(int), ComputeBufferType.Append);
            preDraw.SetBuffer(0, lod0ID, lod0Buffer);
            lod0.SetupLOD(lod0Buffer);

            if (genus.UseLOD1)
            {
                lod1Buffer = new ComputeBuffer(maxCount, sizeof(int), ComputeBufferType.Append);
                preDraw.SetBuffer(0, lod1ID, lod1Buffer);
                lod1.SetupLOD(lod1Buffer);
            }
            else
            {
                lod1Buffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Append);
                preDraw.SetBuffer(0, lod1ID, lod1Buffer);
            }

            if (genus.UseLOD2)
            {
                lod2Buffer = new ComputeBuffer(maxCount, sizeof(int), ComputeBufferType.Append);
                preDraw.SetBuffer(0, lod2ID, lod2Buffer);
                lod2.SetupLOD(lod2Buffer);
            }
            else
            {
                lod2Buffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Append);
                preDraw.SetBuffer(0, lod2ID, lod2Buffer);
            }

            if (genus.UseLOD3)
            {
                lod3Buffer = new ComputeBuffer(maxCount, sizeof(int), ComputeBufferType.Append);
                preDraw.SetBuffer(0, lod3ID, lod3Buffer);
                lod3.SetupLOD(lod3Buffer);
            }
            else
            {
                lod3Buffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Append);
                preDraw.SetBuffer(0, lod3ID, lod3Buffer);
            }

            //Set new max count
            this.totalCount = maxCount;
        }

        //Standard data management
        public override void Setup(Transform transform, NativeArray<GenusInstance> data, Vector3 offset, Vector3 bounds, bool extraData)
        {
            //Core
            this.offset = offset;
            this.bounds = bounds;

            //Setup core
            lod0.SetupCore(transform);
            if (genus.UseLOD1) lod1.SetupCore(transform);
            if (genus.UseLOD2) lod2.SetupCore(transform);
            if (genus.UseLOD3) lod3.SetupCore(transform);

            //Setup pre-draw variables
            preDraw.SetVector(rootID, transform.position);

            //Setup bounds
            preDraw.SetVector(bCenterID, genus.boundsCenter);
            preDraw.SetVector(bExtentID, genus.boundsExtents);

            //Setup LOD Data
            preDraw.SetBool(use1ID, genus.UseLOD1);
            preDraw.SetBool(use1ID, genus.UseLOD2);
            preDraw.SetBool(use3ID, genus.UseLOD3);

            preDraw.SetFloat(optDistID, genus.optDistance);
            preDraw.SetFloat(lod1DistID, genus.lod1Distance);
            preDraw.SetFloat(lod2DistID, genus.lod2Distance);
            preDraw.SetFloat(lod3DistID, genus.billboardDistance);
            preDraw.SetFloat(cullDistID, genus.cullDistance);

            //Setup Data
            SetupData(transform, data);
        }
        public override void Alter(Transform transform, NativeArray<GenusInstance> data)
        {
            SetupData(transform, data);
        }
        public override void Update(Transform transform, NativeArray<GenusInstance> data)
        {
            //Don't update if data doesn't match
            if (data.Length != posData.Length) return;

            //Fill core data
            for (int i = 0; i < drawCount; i++)
            {
                GenusInstance instance = data[i];

                posData[i].density = instance.density;
                posData[i].position = Matrix4x4.TRS(transform.position + (transform.rotation * (instance.position + offset)), instance.rotation, instance.scale);
                posData[i].inversePosition = Matrix4x4.Inverse(posData[i].position);
                posData[i].extra = instance.extra;
            }

            //Assign to buffer
            positionBuffer.SetData(posData);
        }

        private void SetupData(Transform transform, NativeArray<GenusInstance> data)
        {
            //Set new totalCount
            if (drawCount != data.Length)
            {
                //Record total
                drawCount = data.Length;

                //Set max count in pre-draw
                preDraw.SetInt(maxCountID, drawCount);
            }

            //Don't generate if no data
            if (drawCount == 0) return;

            //Check if we need to resize arrays
            if (totalCount != data.Length)
            {
                //Populate backend buffers
                Resize(data.Length);

                //Populate core data
                posData = new ComputeShaderData[totalCount];
            }

            //Fill core data
            for (int i = 0; i < drawCount; i++)
            {
                GenusInstance instance = data[i];

                posData[i].density = instance.density;
                posData[i].position = Matrix4x4.TRS(transform.position + (transform.rotation * (instance.position + offset)), instance.rotation, instance.scale);
                posData[i].inversePosition = Matrix4x4.Inverse(posData[i].position);
                posData[i].extra = instance.extra;
            }

            //Set new data
            positionBuffer.SetData(posData);
        }

        //Burst data management
        public void Setup(Transform transform, NativeArray<ComputeShaderData> data, int drawCount, Vector3 offset, Vector3 bounds)
        {
            //Core
            this.offset = offset;
            this.bounds = bounds;

            //Setup core
            lod0.SetupCore(transform);
            if (genus.UseLOD1) lod1.SetupCore(transform);
            if (genus.UseLOD2) lod2.SetupCore(transform);
            if (genus.UseLOD3) lod3.SetupCore(transform);

            //Setup pre-draw variables
            preDraw.SetVector(rootID, transform.position);

            //Setup bounds
            preDraw.SetVector(bCenterID, genus.boundsCenter);
            preDraw.SetVector(bExtentID, genus.boundsExtents);

            //Setup LOD Data
            preDraw.SetBool(use1ID, genus.UseLOD1);
            preDraw.SetBool(use1ID, genus.UseLOD2);
            preDraw.SetBool(use3ID, genus.UseLOD3);

            preDraw.SetFloat(optDistID, genus.optDistance);
            preDraw.SetFloat(lod1DistID, genus.lod1Distance);
            preDraw.SetFloat(lod2DistID, genus.lod2Distance);
            preDraw.SetFloat(lod3DistID, genus.billboardDistance);
            preDraw.SetFloat(cullDistID, genus.cullDistance);

            //Setup Data
            SetupData(data, drawCount);
        }
        public void Alter(NativeArray<ComputeShaderData> data, int drawCount)
        {
            SetupData(data, drawCount);
        }

        private void SetupData(NativeArray<ComputeShaderData> data, int drawCount)
        {
            //Set new totalCount
            if (this.drawCount != drawCount)
            {
                //Record total
                this.drawCount = drawCount;

                //Set max count in pre-draw
                preDraw.SetInt(maxCountID, drawCount);
            }

            //Don't generate if no data
            if (drawCount == 0) return;

            //Check if we need to resize arrays
            if (totalCount != data.Length) Resize(data.Length);

            //Set new data
            positionBuffer.SetData(data);
        }

        //Release
        public override void Release()
        {
            //Position Buffers
            if (positionBuffer != null)
            {
                positionBuffer.Release();
                positionBuffer = null;
            }

            //LOD Buffers
            if (lod0Buffer != null)
            {
                lod0Buffer.Release();
                lod0Buffer.Dispose();
                lod0Buffer = null;
            }
            if (lod1Buffer != null)
            {
                lod1Buffer.Release();
                lod1Buffer.Dispose();
                lod1Buffer = null;
            }
            if (lod2Buffer != null)
            {
                lod2Buffer.Release();
                lod2Buffer.Dispose();
                lod2Buffer = null;
            }
            if (lod3Buffer != null)
            {
                lod3Buffer.Release();
                lod3Buffer.Dispose();
                lod3Buffer = null;
            }

            //Instance Buffers
            lod0.Release();
            if (lod1 != null) lod1.Release();
            if (lod2 != null) lod2.Release();
            if (lod3 != null) lod3.Release();

            //Clear pos data
            posData = null;

            //Compute Instance
            Object.DestroyImmediate(preDraw);
        }

        //Predraw
        public void PreDraw(Camera camera)
        {
            //Wait until we have data
            if (drawCount == 0) return;

            Vector3 cameraPosition = camera.transform.position;
            Matrix4x4 cameraVPMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;

            preDraw.SetFloat("_DensityBias", InstanceRenderer.densityBias);
            preDraw.SetFloat("_DistanceBias", InstanceRenderer.distanceBias);

            preDraw.SetVector("_FrustrumPosition", cameraPosition);
            preDraw.SetMatrix("_VPMatrix", cameraVPMatrix);

            //Clear LOD buffers
            lod0Buffer.SetCounterValue(0);
            if (genus.UseLOD1) lod1Buffer.SetCounterValue(0);
            if (genus.UseLOD2) lod2Buffer.SetCounterValue(0);
            if (genus.UseLOD3) lod3Buffer.SetCounterValue(0);

            int iterations = Mathf.CeilToInt(drawCount / 1024.0f);
            preDraw.Dispatch(0, iterations, 1, 1);
        }

        //Draw
        public override void Draw(Transform transform)
        {
            //Wait until we have data
            if (drawCount == 0) return;

            DrawIndirect(transform);
        }
        private void DrawIndirect(Transform transform)
        {
            lod0.DrawIndirect(transform, offset, bounds, lod0Buffer);
            if (genus.UseLOD1) lod1.DrawIndirect(transform, offset, bounds, lod1Buffer);
            if (genus.UseLOD2) lod2.DrawIndirect(transform, offset, bounds, lod2Buffer);
            if (genus.UseLOD3) lod3.DrawIndirect(transform, offset, bounds, lod3Buffer);
        }
        private void DrawProcedural(Transform transform)
        {
            lod0.DrawProcedural(transform, offset, bounds, lod0Buffer);
            if (genus.UseLOD1) lod1.DrawProcedural(transform, offset, bounds, lod1Buffer);
            if (genus.UseLOD2) lod2.DrawProcedural(transform, offset, bounds, lod2Buffer);
            if (genus.UseLOD3) lod3.DrawProcedural(transform, offset, bounds, lod3Buffer);
        }
    }
    public class ComputeInstance
    {
        private Mesh instanceMesh;
        private Material instanceMaterial;
        private ShadowCastingMode shadowMode;

        //Storage
        private int vertCount;
        private int triCount;

        //Compute
        private ComputeBuffer argsBuffer;
        //AsyncGPUReadbackRequest argsRequest;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        //Debug
        public int VertCount
        {
            get { return vertCount * drawnCount; }
        }
        public int TriangleCount
        {
            get { return triCount * drawnCount; }
        }
        public int InstanceCount
        {
            get { return drawnCount; }
        }

        private int drawnCount;

        //Constructor
        public ComputeInstance(Mesh mesh, Material material, bool drawShadows)
        {
            //Initialize
            instanceMesh = mesh;
            instanceMaterial = new Material(material);

            shadowMode = (drawShadows) ? ShadowCastingMode.On : ShadowCastingMode.Off;

            vertCount = mesh.vertexCount;
            triCount = instanceMesh.isReadable ? instanceMesh.GetTriangles(0).Length : Mathf.CeilToInt(vertCount * 0.75f);
        }

        //Setup
        public void SetupCore(Transform transform)
        {
            instanceMaterial.SetVector("_RootPosition", transform.position);

            //Initialize compute
            if (argsBuffer == null)
            {
                argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                args[0] = instanceMesh.GetIndexCount(0);
                args[1] = 0;
                args[2] = instanceMesh.GetIndexStart(0);
                args[3] = instanceMesh.GetBaseVertex(0);
                argsBuffer.SetData(args);
            }

            //Keywords
            instanceMaterial.EnableKeyword("_INSTANCETYPE_COMPUTE");
            instanceMaterial.DisableKeyword("_INSTANCETYPE_STANDARD");
        }
        public void SetupPositions(ComputeBuffer positionBuffer)
        {
            instanceMaterial.SetBuffer("_PositionBuffer", positionBuffer);
        }
        public void SetupLOD(ComputeBuffer LODBuffer)
        {
            instanceMaterial.SetBuffer("_LOD", LODBuffer);
        }

        //Release
        public void Release()
        {
            //Compute
            if (argsBuffer != null)
            {
                argsBuffer.Release();
                argsBuffer.Dispose();
                argsBuffer = null;
            }

            //Material
            Object.DestroyImmediate(instanceMaterial);
        }

        //Draw
        public void DrawIndirect(Transform transform, Vector3 offset, Vector3 bounds, ComputeBuffer LODBuffer)
        {
            //Update Args
            ComputeBuffer.CopyCount(LODBuffer, argsBuffer, 4);

            //Check for previous args
            //if (argsRequest.hasError || argsRequest.done)
            //{
            //    //Update drawn count
            //    if (!argsRequest.hasError) drawnCount = (int)argsRequest.GetData<uint>()[1];

            //    //Execute new request
            //    argsRequest = AsyncGPUReadback.Request(argsBuffer);
            //}

            //Draw
            Graphics.DrawMeshInstancedIndirect(instanceMesh, 0, instanceMaterial, new Bounds(transform.position + offset, bounds * 2), argsBuffer, 0, null, shadowMode);
        }
        public void DrawProcedural(Transform transform, Vector3 offset, Vector3 bounds, ComputeBuffer LODBuffer)
        {
            //Update Args every frame
            ComputeBuffer.CopyCount(LODBuffer, argsBuffer, 4);

            //Check for previous args
            //if (argsRequest.hasError || argsRequest.done)
            //{
            //    //Update drawn count
            //    if (!argsRequest.hasError) drawnCount = (int)argsRequest.GetData<uint>()[0];

            //    //Execute new request
            //    argsRequest = AsyncGPUReadback.Request(argsBuffer, argsBuffer.stride, 4);
            //}

            //Get count
            int count = LODBuffer.count;

            //Draw
            Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, instanceMaterial, new Bounds(transform.position + offset, bounds), count, null, shadowMode);
        }
    }

    public struct ComputeShaderData
    {
        public float density;
        public Matrix4x4 position;
        public Matrix4x4 inversePosition;
        public Vector4 extra;
        public static int Size
        {
            get
            {
                int size = 0;
                size += sizeof(float);
                size += sizeof(float) * 4 * 4;
                size += sizeof(float) * 4 * 4;
                size += sizeof(float) * 4;
                return size;
            }
        }
    }
    public struct StandardShaderData
    {
        public Vector4 extra;

        public static int Size
        {
            get
            {
                int size = 0;
                size += sizeof(float) * 4;
                return size;
            }
        }

        public StandardShaderData(Vector4 extra)
        {
            this.extra = extra;
        }
    }
    public struct ComputeData
    {
        public Matrix4x4 position;

        public static int Size
        {
            get { return sizeof(float) * 4 * 4; }
        }
    }

    public enum Computation { Compute, Burst };
    public enum RendState { Inactive, Queued, Generating, Pooled, Active };
    public enum DrawType { Standard, Indirect, Procedural };
    public enum UpdateType { Never, Once, EverySecond, EveryFrame };
}