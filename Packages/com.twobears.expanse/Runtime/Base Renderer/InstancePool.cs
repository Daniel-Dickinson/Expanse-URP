using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TwoBears.Expanse
{
    [ExecuteAlways]
    public class InstancePool : MonoBehaviour, IAnalyzerInstance
    {
        //Pools
        private static List<GenusPool> genera;

        //Job handles
        private List<JobHandle> cullHandles = new List<JobHandle>();
        private List<JobHandle> collectHandles = new List<JobHandle>();

        //Asnyc update
        private List<QueuedPool> queue = new List<QueuedPool>();

        //Timing
        private int timeIndex = 0;

        private double[] preDrawTime;
        private double[] updateTime;
        private double[] drawTime;

        private const int timeSamples = 24;

        //Properties
        private static DrawType DrawType
        {
            get
            {
                if (Application.isPlaying) return DrawType.Indirect;
                else return DrawType.Standard;
            }
        }

        private Camera Camera
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
        private Camera currentCamera;

        //Analysis
        public string Name
        {
            get { return "Pool"; }
        }
        public string DrawShadows
        {
            get { return "Shadows Mixed"; }
        }
        public int InstanceCount
        {
            get 
            {
                int total = 0;
                for (int i = 0; i < genera.Count; i++)
                {
                    total += genera[i].renderer.InstanceCount;
                }
                return total; 
            }
        }
        public int VertexCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < genera.Count; i++)
                {
                    total += genera[i].renderer.VertexCount;
                }
                return total;
            }
        }
        public int TriangleCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < genera.Count; i++)
                {
                    total += genera[i].renderer.TriangleCount;
                }
                return total;
            }
        }
       
        public int MemorySize
        {
            get
            {
                int total = 0;
                if (genera == null) return total;
                for (int i = 0; i < genera.Count; i++)
                {
                    total += genera[i].renderer.MemorySize;
                }
                return total;
            }
        }
        public string MemorySizeDebug
        {
            get { return ((MemorySize / 1024.0f) / 1024.0f) / 8 + " MB"; }
        }

        public double PrepTime
        {
            get
            {
                double total = 0;
                for (int i = 0; i < timeSamples; i++)
                {
                    total += (preDrawTime[i] + updateTime[i]);
                }
                return total / timeSamples;
            }
        }
        public double DrawTime
        {
            get
            {
                double total = 0;
                for (int i = 0; i < timeSamples; i++) total += drawTime[i];
                return total / timeSamples;
            }
        }

        //Mono
        private void Awake()
        {
            //Initialize timing arrays
            preDrawTime = new double[timeSamples];
            updateTime = new double[timeSamples];
            drawTime = new double[timeSamples];

        }
        private void OnEnable()
        {
            RegisterToAnalyzer();
        }
        private void OnDisable()
        {
            DeregisterFromAnalyzer();
            Release();
        }
        private void Update()
        {
            RenderLoop();
            //Debug.Log(MemorySizeDebug);
        }

        //Core
        private void RenderLoop()
        {
            if (genera == null) return;
            if (genera.Count == 0) return;

            Initialize();
            PreDraw();
            Draw();
        }
        private void Release()
        {
            if (genera == null) return;
            for (int i = 0; i < genera.Count; i++) genera[i].Release();
            genera.Clear();
        }

        //Initialize
        private void Initialize()
        {
            double startTime = Time.realtimeSinceStartupAsDouble;

            if (Application.isPlaying)
            {
                //Initialize or update pools as required
                if (!InitializePools()) UpdatePoolsBurst();
            }
            else
            {
                InitializePoolsImmediate();
                UpdatedPoolsImmediate();
            }

            //Record timing
            updateTime[timeIndex] = (Time.realtimeSinceStartupAsDouble - startTime) * 1000; //Convert to milliseconds
        }

        private void InitializePoolsImmediate()
        {
            for (int i = 0; i < genera.Count; i++)
            {
                if (genera[i].State == PoolState.Uninitialized)
                {
                    genera[i].Initialize(transform);
                }
            }
        }
        private bool InitializePools()
        {
            //Initialize as required
            for (int i = 0; i < genera.Count; i++)
            {
                if (genera[i].State == PoolState.Uninitialized)
                {
                    genera[i].Initialize(transform);

                    //Only initialize a single pool per frame
                    return true; 
                }
            }
            return false;
        }

        private void UpdatedPoolsImmediate()
        {
            for (int i = 0; i < genera.Count; i++)
            {
                if (genera[i].State == PoolState.Dirty) genera[i].UpdateImmediately(transform);
            }
        }
        private bool UpdatePoolsBurst()
        {
            //Complete requested jobs
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].handle.IsCompleted)
                {
                    //Complete handle
                    queue[i].handle.Complete();

                    //Upload data to renderer
                    queue[i].pool.UpdateBurstData(transform);

                    //Remove from queue
                    queue.RemoveAt(i);

                    //Only apply a single pool per frame
                    return true;
                }
            }

            //Schedule required updates
            for (int i = 0; i < genera.Count; i++)
            {
                if (genera[i].State == PoolState.Dirty)
                {
                    queue.Add(new QueuedPool(genera[i], genera[i].ScheduleBurstCombine(transform)));

                    //Only queue a single pool per frame
                    return true;
                }
            }

            //No pools updated
            return false;
        }

        //PreDraw
        private void PreDraw()
        {
            double startTime = Time.realtimeSinceStartupAsDouble;

            switch (DrawType)
            {
                case DrawType.Standard:
                    PreDrawStandard();
                    break;
                case DrawType.Indirect:
                case DrawType.Procedural:
                    PreDrawCompute();
                    break;
            }

            //Record timing
            preDrawTime[timeIndex] = (Time.realtimeSinceStartupAsDouble - startTime) * 1000; //Convert to milliseconds
        }
        private void PreDrawStandard()
        {
            //Grab Camera
            Camera cam = Camera;
            if (cam == null) return;

            //Schedule culling
            for (int i = 0; i < genera.Count; i++)
            {
                if (genera[i].type.priority < InstanceRenderer.minPriority) continue;
                genera[i].PreDrawStandardCull(cam, ref cullHandles);
            }

            //Wait for culling to complete
            for (int c = 0; c < cullHandles.Count; c++) cullHandles[c].Complete();
            cullHandles.Clear();

            //Schedule collection
            for (int i = 0; i < genera.Count; i++)
            {
                if (genera[i].type.priority < InstanceRenderer.minPriority) continue;
                genera[i].PreDrawStandardCollect(ref collectHandles);
            }

            //Wait for collection to complete
            for (int c = 0; c < collectHandles.Count; c++) collectHandles[c].Complete();
            collectHandles.Clear();
        }
        private void PreDrawCompute()
        {
            //Grab Camera
            Camera cam = Camera;
            if (cam == null) return;

            for (int i = 0; i < genera.Count; i++)
            {
                if (genera[i].type.priority < InstanceRenderer.minPriority) continue;
                genera[i].PreDrawCompute(cam);
            }   
        }

        //Draw
        private void Draw()
        {
            //Camera required (Will be null if not found on PreDraw)
            if (currentCamera == null) return;

            double startTime = Time.realtimeSinceStartupAsDouble;

            //Draw all genera
            for (int i = 0; i < genera.Count; i++)
            {
                if (genera[i].type.priority < InstanceRenderer.minPriority) continue;
                genera[i].Draw(transform);
            }

            //Record timing
            drawTime[timeIndex] = (Time.realtimeSinceStartupAsDouble - startTime) * 1000; //Convert to milliseconds

            //Increment time index
            timeIndex++;
            if (timeIndex >= timeSamples) timeIndex = 0;
        }

        //Analysis
        private void RegisterToAnalyzer()
        {
            ExpanseAnalyzer.RegisterRenderer(this);
        }
        private void DeregisterFromAnalyzer()
        {
            ExpanseAnalyzer.DeregisterRenderer(this);
        }

        //Registration
        public static void Register(InstanceRenderer renderer, GenusData data)
        {
            //Setup type
            GenusType type = new GenusType(data.genus, data.matOverride, data.billboardOverride, renderer.drawShadows, renderer.extraData, renderer.poolIndex, renderer.priority);

            //Check if pool exists
            GenusPool pool = FindPoolOfType(type);

            //Initalize pools if required
            if (genera == null) genera = new List<GenusPool>();

            //Create new pool if required
            if (pool == null)
            {
                switch (DrawType)
                {
                    case DrawType.Standard:
                        StandardPool standardPool = new StandardPool(type);
                        pool = standardPool;
                        break;
                    case DrawType.Indirect:
                    case DrawType.Procedural:
                        ComputePool computePool = new ComputePool(type, renderer.preDraw);
                        pool = computePool;
                        break;
                }
                genera.Add(pool);
            }

            //Add data to pool
            pool.AddSource(renderer, data.data);
        }
        public static void Deregister(InstanceRenderer renderer)
        {
            //Genera required
            if (genera == null) return;

            //Strip from all renderers
            for (int i = 0; i < genera.Count; i++)
            {
                genera[i].RemoveSource(renderer);
            }
        }

        private static GenusPool FindPoolOfType(GenusType type)
        {
            if (genera == null) return null;

            for (int g = 0; g < genera.Count; g++)
            {
                if (genera[g].type == type) return genera[g];
            }

            return null;
        }
    }

    //Pool
    public abstract class GenusPool
    {
        public GenusType type;
        public GenRenderer renderer;

        //Immediate
        protected List<DataSource> sources;

        //Burst Input
        protected NativeArray<GenusInstance> combinedData;
        protected NativeList<ConversionSource> conversions;
        protected NativeList<GenusInstance> flattenedData;

        //State
        protected PoolState state;
        protected int total;

        //Properties
        public int Total
        {
            get { return total; }
        }
        public PoolState State
        {
            get { return state; }
        }

        //Initialize
        public void Initialize(Transform transform)
        {
            //Calculate combined data
            combinedData = GetCombinedData(transform);

            //Setup renderer with data
            renderer.Setup(transform, combinedData, Vector3.zero, new Vector3(100000, 100000, 100000), type.extraData);

            //No longer need combined data
            combinedData.Dispose();

            //No longer dirty
            state = PoolState.Clean;
        }

        //Predraw
        public void PreDrawStandardCull(Camera camera, ref List<JobHandle> cullHandles)
        {
            if (total == 0) return;
            (renderer as StandardRenderer).ScheduleCull(camera, ref cullHandles);
        }
        public void PreDrawStandardCollect(ref List<JobHandle> collectHandles)
        {
            if (total == 0) return;
            (renderer as StandardRenderer).ScheduleCollect(ref collectHandles);
        }
        public void PreDrawCompute(Camera camera)
        {
            if (total == 0) return;
            (renderer as ComputeRenderer).PreDraw(camera);
        }
        
        public void Draw(Transform transform)
        {
            if (total == 0) return;
            renderer.Draw(transform);
        }

        //State
        public void MarkDirty()
        {
            //Clean states become dirty
            if (state == PoolState.Clean) state = PoolState.Dirty;

            //If marked dirty during generation interrupt & start again
            if (state == PoolState.Generating) state = PoolState.Interrupted;
        }
        protected void MarkComplete()
        {
            //Non interupted states now clean
            if (state == PoolState.Generating) state = PoolState.Clean;

            //Interupted states return to dirty
            if (state == PoolState.Interrupted) state = PoolState.Dirty;
        }

        //Add or remove
        public void AddSource(InstanceRenderer renderer, NativeArray<GenusInstance> data)
        {
            //Don't allow duplicate sources
            foreach (DataSource source in sources) if (source.renderer == renderer) return;

            //Create new dataSource
            DataSource dataSource = new DataSource(renderer, data);

            //Add new data source
            sources.Add(dataSource);

            //Mark dirty
            MarkDirty();
        }
        public void AddSource(InstanceRenderer renderer, NativeList<GenusInstance> data)
        {
            //Don't allow duplicate sources
            foreach (DataSource source in sources) if (source.renderer == renderer) return;

            //Create new dataSource
            DataSource dataSource = new DataSource(renderer, data);

            //Add new data source
            sources.Add(dataSource);

            //Mark dirty
            MarkDirty();
        }
        public void RemoveSource(InstanceRenderer renderer)
        {
            //Make sure we have the data source
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                if (sources[i].renderer == renderer)
                {
                    sources[i].data.Dispose();
                    sources.RemoveAt(i);
                    MarkDirty();
                }
            }
        }

        //Cleanup
        public void Release()
        {
            renderer.Release();
            ReleaseBurst();
        }

        //Immediate Mode
        public void UpdateImmediately(Transform transform)
        {
            //Only update when required
            if (state != PoolState.Dirty) return;

            //Calculate combined data
            combinedData = GetCombinedData(transform);

            //Updata data
            renderer.Alter(transform, combinedData);

            //No longer need combined data
            combinedData.Dispose();

            //No longer dirty
            state = PoolState.Clean;
        }
        private NativeArray<GenusInstance> GetCombinedData(Transform transform)
        {
            //Calculate total count
            total = 0;
            for (int i = 0; i < sources.Count; i++) total += sources[i].data.Length;

            //Initialize data array
            combinedData = new NativeArray<GenusInstance>(total, Allocator.Temp);

            //Populate data
            int index = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                InstanceRenderer renderer = sources[i].renderer;
                AreaRenderer areaRenderer = renderer as AreaRenderer;

                for (int d = 0; d < sources[i].data.Length; d++)
                {
                    //Grab instance
                    GenusInstance instance = sources[i].data[d];

                    //Grab starting position
                    Vector3 instancePosition = instance.position;

                    //Account for bounds offset
                    if (areaRenderer != null) instancePosition -= new Vector3(0, areaRenderer.bounds.y * 0.5f, 0);

                    //Convert to world space
                    Vector3 worldPosition = sources[i].renderer.transform.TransformPoint(instancePosition);

                    //Convert to local space of pool transform
                    Vector3 localPosition = transform.InverseTransformPoint(worldPosition);

                    //Set new data
                    instance.position = localPosition;

                    //Add to data set
                    combinedData[index] = instance;
                    index++;
                }
            }

            return combinedData;
        }

        //Burst 
        public abstract JobHandle ScheduleBurstCombine(Transform transform);
        public abstract void UpdateBurstData(Transform transform);

        protected virtual void InitializeBurst()
        {
            conversions = new NativeList<ConversionSource>(1024, Allocator.Persistent);
            flattenedData = new NativeList<GenusInstance>(1024, Allocator.Persistent);
        }
        protected virtual void ReleaseBurst()
        {
            conversions.Dispose();
            flattenedData.Dispose();

            //Dispose of source data
            for (int i = 0; i < sources.Count; i++) if (sources[i].data.IsCreated) sources[i].data.Dispose();

            //Clear sources
            sources.Clear();
        }

        protected void FlattenData()
        {
            conversions.Clear();
            flattenedData.Clear();

            foreach (DataSource source in sources)
            {
                conversions.Add(new ConversionSource(source));
                for (int i = 0; i < source.data.Length; i++) flattenedData.Add(source.data[i]);
            }
        }
    }
    public class StandardPool : GenusPool
    {
        //Output
        private int maxCount = 1;
        private NativeArray<float> densities;
        private NativeArray<Matrix4x4> transforms;

        //Job
        JobHandle jobHandle;

        //Constructor
        public StandardPool(GenusType type)
        {
            //Set type
            this.type = type;

            //Setup burst
            InitializeBurst();

            //Initialize sources
            sources = new List<DataSource>();

            //Construct renderer
            renderer = new StandardRenderer(type.genus, type.drawShadows, type.materialOverride, type.billboardOverride);
        }

        //Burst
        public override JobHandle ScheduleBurstCombine(Transform transform)
        {
            //Now generating
            state = PoolState.Generating;

            //Reach parity
            FlattenData();

            //Resize pool as needed
            if (flattenedData.Length > densities.Length)
            {
                //Keep doubling pool size until big enough to fit all data
                if (maxCount < 128) maxCount = 128;
                while (maxCount < flattenedData.Length) maxCount *= 2;

                //Resize output arrays to fit (Will also resize renderer buffers)
                densities.Dispose();
                densities = new NativeArray<float>(maxCount, Allocator.Persistent);

                transforms.Dispose();
                transforms = new NativeArray<Matrix4x4>(maxCount, Allocator.Persistent);
            }

            //Create convert job
            ConvertToStandard convertJob = new ConvertToStandard()
            {
                //Input
                conversions = conversions,
                flattenedData = flattenedData,

                //Output
                densities = densities,
                transforms = transforms
            };

            jobHandle = convertJob.Schedule();

            //Schedule
            return jobHandle;
        }
        public override void UpdateBurstData(Transform transform)
        {
            //Update renderer data
            (renderer as StandardRenderer).Alter(densities, transforms, flattenedData.Length);

            //Now completed
            MarkComplete();
        }

        protected override void InitializeBurst()
        {
            base.InitializeBurst();

            densities = new NativeArray<float>(1024, Allocator.Persistent);
            transforms = new NativeArray<Matrix4x4>(1024, Allocator.Persistent);
        }
        protected override void ReleaseBurst()
        {
            if (densities.IsCreated) densities.Dispose();
            if (transforms.IsCreated) transforms.Dispose();

            base.ReleaseBurst();
        }

        [BurstCompile]
        private struct ConvertToStandard : IJob
        {
            //Input
            [ReadOnly] public NativeList<ConversionSource> conversions;
            [ReadOnly] public NativeList<GenusInstance> flattenedData;

            //Output
            public NativeArray<float> densities;
            public NativeArray<Matrix4x4> transforms;

            public void Execute()
            {
                //Populate data
                int index = 0;
                for (int i = 0; i < conversions.Length; i++)
                {
                    ConversionSource conversion = conversions[i];
                    for (int d = 0; d < conversion.count; d++)
                    {
                        //Grab raw data
                        GenusInstance instance = flattenedData[index];

                        //Grab starting position
                        Vector3 instancePosition = instance.position;

                        //Account for half bounds offset
                        instancePosition -= new Vector3(0, conversion.bounds.y * 0.5f, 0);

                        //Convert to world space
                        Vector3 worldPosition = conversion.objectToWorld.MultiplyPoint(instancePosition);

                        //Convert to local space of pool transform
                        //Vector3 poolPosition = worldToPool.MultiplyPoint(worldPosition);

                        //Set new data
                        //instance.position = poolPosition;

                        //Create Shader data
                        densities[index] = instance.density;
                        transforms[index] = Matrix4x4.TRS(worldPosition, instance.rotation, instance.scale);

                        //Increment total index
                        index++;
                    }
                }
            }
        }
    }
    public class ComputePool : GenusPool
    {
        //Output
        private int maxCount = 1;
        private NativeArray<ComputeShaderData> convertedData;

        //Job
        JobHandle jobHandle;

        //Constructor
        public ComputePool(GenusType type, ComputeShader shader)
        {
            //Set type
            this.type = type;

            //Setup burst
            InitializeBurst();

            //Initialize sources
            sources = new List<DataSource>();

            //Construct renderer
            renderer = new ComputeRenderer(type.genus, shader, type.drawShadows, type.materialOverride, type.billboardOverride);
        }

        //Burst 
        public override JobHandle ScheduleBurstCombine(Transform transform)
        {
            //Now generating
            state = PoolState.Generating;

            //Reach parity
            FlattenData();

            //Resize pool as needed
            if (flattenedData.Length > convertedData.Length)
            {
                //Keep doubling pool size until big enough to fit all data
                if (maxCount < 128) maxCount = 128;
                while (maxCount < flattenedData.Length) maxCount *= 2;

                //Resize output array to fit (Will also resize renderer buffers)
                convertedData.Dispose();
                convertedData = new NativeArray<ComputeShaderData>(maxCount, Allocator.Persistent);
            }

            //Create convert job
            ConvertToCompute convertJob = new ConvertToCompute()
            {
                conversions = conversions,
                flattenedData = flattenedData,
                convertedData = convertedData
            };

            jobHandle = convertJob.Schedule();

            //Schedule
            return jobHandle;
        }
        public override void UpdateBurstData(Transform transform)
        {
            //Update renderer data
            (renderer as ComputeRenderer).Alter(convertedData, flattenedData.Length);

            //Now completed
            MarkComplete();
        }

        protected override void InitializeBurst()
        {
            base.InitializeBurst();
            convertedData = new NativeArray<ComputeShaderData>(1024, Allocator.Persistent);
        }
        protected override void ReleaseBurst()
        {
            jobHandle.Complete();
            convertedData.Dispose();

            base.ReleaseBurst();
        }

        [BurstCompile]
        private struct ConvertToCompute : IJob
        {
            //Input
            [ReadOnly] public NativeList<ConversionSource> conversions;
            [ReadOnly] public NativeList<GenusInstance> flattenedData;

            //Output
            public NativeArray<ComputeShaderData> convertedData;

            public void Execute()
            {
                //Populate data
                int index = 0;
                for (int i = 0; i < conversions.Length; i++)
                {
                    ConversionSource conversion = conversions[i];
                    for (int d = 0; d < conversion.count; d++)
                    {
                        //Grab raw data
                        GenusInstance instance = flattenedData[index];

                        //Grab starting position
                        Vector3 instancePosition = instance.position;

                        //Account for half bounds offset
                        instancePosition -= new Vector3(0, conversion.bounds.y * 0.5f, 0);

                        //Convert to world space
                        Vector3 worldPosition = conversion.objectToWorld.MultiplyPoint(instancePosition);

                        //Convert to local space of pool transform
                        //Vector3 poolPosition = worldToPool.MultiplyPoint(worldPosition);

                        //Set new data
                        //instance.position = poolPosition;

                        //Create Shader data
                        ComputeShaderData posData = new ComputeShaderData();

                        posData.density = instance.density;
                        posData.position = Matrix4x4.TRS(worldPosition, instance.rotation, instance.scale);
                        posData.inversePosition = Matrix4x4.Inverse(posData.position);
                        posData.extra = instance.extra;

                        //Add to data set
                        convertedData[index] = posData;

                        //Increment total index
                        index++;
                    }
                }
            }
        }
    }

    //Source Data
    [System.Serializable]
    public class DataSource
    {
        public InstanceRenderer renderer;
        public NativeArray<GenusInstance> data;

        public DataSource(InstanceRenderer renderer, NativeArray<GenusInstance> data)
        {
            this.renderer = renderer;
            this.data = new NativeArray<GenusInstance>(data.Length, Allocator.Persistent);
            for (int i = 0; i < data.Length; i++) this.data[i] = data[i];
        }
        public DataSource(InstanceRenderer renderer, NativeList<GenusInstance> data)
        {
            this.renderer = renderer;
            this.data = new NativeArray<GenusInstance>(data.Length, Allocator.Persistent);
            for (int i = 0; i < data.Length; i++) this.data[i] = data[i];
        }
    }

    //Scheduling
    public struct QueuedPool
    {
        public GenusPool pool;
        public JobHandle handle;

        public QueuedPool (GenusPool pool, JobHandle handle)
        {
            this.pool = pool;
            this.handle = handle;
        }
    }

    //Burst
    public struct ConversionSource
    {
        public Vector3 bounds;
        public Matrix4x4 objectToWorld;
        public int count;

        public ConversionSource(DataSource source)
        {
            Transform transform = source.renderer.transform;
            AreaRenderer areaRenderer = source.renderer as AreaRenderer;

            bounds = (areaRenderer != null) ? areaRenderer.bounds : Vector3.zero;
            objectToWorld = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            count = source.data.Length;
        }
        public ConversionSource(InstanceRenderer renderer, GenusInstance[] data)
        {
            Transform transform = renderer.transform;
            AreaRenderer areaRenderer = renderer as AreaRenderer;

            bounds = (areaRenderer != null) ? areaRenderer.bounds : Vector3.zero;
            objectToWorld = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            count = data.Length;
        }
    }
    public struct RemovalJob
    {
        public int setIndex;
        public int startIndex;
        public int count;

        public RemovalJob(int set, int start, int count)
        {
            setIndex = set;
            startIndex = start;
            this.count = count;
        }
    }

    //Classification
    public struct GenusType
    {
        public Genus genus;
        public Material materialOverride;
        public Material billboardOverride;
        public bool drawShadows;
        public bool extraData;
        public int poolIndex;
        public int priority;

        //Constructor
        public GenusType(Genus genus, bool drawShadows, bool extraData, int poolIndex, int priority)
        {
            this.genus = genus;
            materialOverride = null;
            billboardOverride = null;
            this.drawShadows = drawShadows;
            this.extraData = extraData;
            this.poolIndex = poolIndex;
            this.priority = priority;
        }
        public GenusType(Genus genus, Material materialOverride, bool drawShadows, bool extraData, int poolIndex, int priority)
        {
            this.genus = genus;
            this.materialOverride = materialOverride;
            billboardOverride = null;
            this.drawShadows = drawShadows;
            this.extraData = extraData;
            this.poolIndex = poolIndex;
            this.priority = priority;
        }
        public GenusType(Genus genus, Material materialOverride, Material billboardOverride, bool drawShadows, bool extraData, int poolIndex, int priority)
        {
            this.genus = genus;
            this.materialOverride = materialOverride;
            this.billboardOverride = billboardOverride;
            this.drawShadows = drawShadows;
            this.extraData = extraData;
            this.poolIndex = poolIndex;
            this.priority = priority;
        }

        //Operators
        public static bool operator ==(GenusType a, GenusType b)
        {
            if (a.genus != b.genus) return false;
            if (a.materialOverride != b.materialOverride) return false;
            if (a.billboardOverride != b.billboardOverride) return false;
            if (a.drawShadows != b.drawShadows) return false;
            if (a.extraData != b.extraData) return false;
            if (a.poolIndex != b.poolIndex) return false;
            if (a.priority != b.priority) return false;
            return true;
        }
        public static bool operator !=(GenusType a, GenusType b)
        {
            if (a.genus != b.genus) return true;
            if (a.materialOverride != b.materialOverride) return true;
            if (a.billboardOverride != b.billboardOverride) return true;
            if (a.drawShadows != b.drawShadows) return true;
            if (a.extraData != b.extraData) return true;
            if (a.poolIndex != b.poolIndex) return true;
            if (a.priority != b.priority) return true;
            return false;
        }
        public override bool Equals(object obj)
        {
            return this == (GenusType)obj;
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        public override string ToString()
        {
            return genus.ToString();
        }
    }

    public enum PoolState { Uninitialized, Dirty, Generating, Interrupted, Clean };
}