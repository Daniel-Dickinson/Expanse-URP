using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace TwoBears.Expanse
{
    public class AreaRenderer : InstanceRenderer
    {
        public Genus[] genera;
        
        public TextureDataSet textureSet;
        public RuleSet ruleSet;

        public Material[] standardMaterials;
        public Material[] billboardMaterials;

        public int seed = 1;

        public int countX = 10;
        public int countY = 10;

        public Vector3 bounds = Vector3.one;
        public Vector3 offset = Vector3.zero;

        //Generation
        private JobHandle jobHandle;

        private NativeArray<GenusState> states;

        //Mono
        protected override void OnDisable()
        {
            //Cancel generation
            CancelAsyncGeneration();

            //Base 
            base.OnDisable();
        }

        //Culling
        protected override void PerformCulling()
        {
            //Camera required
            if (currentCamera == null) return;

            //Calculate camera vp matrix
            Matrix4x4 cameraVPMatrix = currentCamera.projectionMatrix * currentCamera.worldToCameraMatrix;

            //Frustrum cull
            Hidden = FrustrumCull(currentCamera.transform.position, currentCamera.transform.forward, cameraVPMatrix);
        }
        
        private bool FrustrumCull(Vector3 cameraPosition, Vector3 cameraForward, Matrix4x4 cameraVPMatrix)
        {
            //Check if inside bounds
            if (WithinHorizontalBounds(cameraPosition)) return false;

            //Calculate distance
            float distance = Vector3.Distance(transform.position + (transform.rotation * offset), cameraPosition);
            
            //Calculate if facing
            Vector3 direction = (cameraPosition - transform.position).normalized;
            bool facingCenter = Vector3.Dot(direction, cameraForward) > 0;

            //Always keep instances within opt distance
            if (facingCenter && distance < bounds.magnitude) return false;

            float3 min = transform.position + (transform.rotation * (offset - bounds));
            float3 max = transform.position + (transform.rotation * (offset + bounds));

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
        private bool WithinHorizontalBounds(Vector3 cameraPosition)
        {
            Vector3 localCameraPosition = transform.InverseTransformPoint(cameraPosition);

            if (localCameraPosition.x < -bounds.x + offset.x || localCameraPosition.x > bounds.x + offset.x) return false;
            if (localCameraPosition.x < -bounds.z + offset.z || localCameraPosition.x > bounds.z + offset.z) return false;

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

        //Generation
        protected override GenusData[] GenerateData()
        {
            //Genera required
            if (genera == null) return null;

            //Initialize states
            states = new NativeArray<GenusState>(countX * countY, Allocator.TempJob);

            //Populate initial states
            SetupJob setupJob = new SetupJob()
            {
                countX = countX,
                countY = countY,
                bounds = bounds,
                offset = offset,
                position = transform.position,
                rotation = transform.rotation,
                random = new Unity.Mathematics.Random((uint)seed),
                states = states
            };
            setupJob.Schedule(states.Length, 1000).Complete();

            //Apply modifiers
            if (ruleSet != null && ruleSet.modifiers != null)
            {
                //Create world state
                WorldState world = new WorldState(seed, transform.position, textureSet);

                for (int m = 0; m < ruleSet.modifiers.Length; m++)
                {
                    //Apply modifier
                    ruleSet.modifiers[m].ApplyModifier(world, states);
                }
            }

            //Setup data sets
            GenusData[] dataSet = new GenusData[genera.Length];

            //Scan instances per data set
            for (int i = 0; i < dataSet.Length; i++)
            {
                //Initialize new distribution
                NativeList<GenusInstance> output = new NativeList<GenusInstance>(Allocator.Persistent);

                //Perform distribution job
                DistributeScanJob distJob = new DistributeScanJob()
                {
                    distributionIndex = i,
                    distributionTotal = dataSet.Length,

                    states = states,
                    output = output,

                    //Seed must be reset so random distribution is uniform
                    random = new Unity.Mathematics.Random((uint)seed),

                };
                distJob.Schedule().Complete();

                //Check for override materials
                Material standard = (standardMaterials != null && standardMaterials.Length > i)? standardMaterials[i]: null;
                Material billboard = (billboardMaterials != null && billboardMaterials.Length > i) ? billboardMaterials[i] : null;

                //Pass as data set
                dataSet[i] = new GenusData(genera[i], standard, billboard, output, offset, bounds);
            }

            //Dispose of states
            states.Dispose();

            //Return
            return dataSet;
        }
        protected override IEnumerator GenerateDataAsync()
        {
            //Genera required
            if (genera == null) yield break;

            //Initialize states
            states = new NativeArray<GenusState>(countX * countY, Allocator.Persistent);

            //Populate initial states
            SetupJob setupJob = new SetupJob()
            {
                countX = countX,
                countY = countY,
                bounds = bounds,
                offset = offset,
                position = transform.position,
                rotation = transform.rotation,
                random = new Unity.Mathematics.Random((uint)seed),
                states = states
            };
            jobHandle = setupJob.Schedule(states.Length, 1000);

            //Wait for setup to complete
            while (!jobHandle.IsCompleted) yield return new WaitForEndOfFrame();
            jobHandle.Complete();

            //Apply modifiers
            if (ruleSet != null && ruleSet.modifiers != null)
            {
                //Create world state
                WorldState world = new WorldState(seed, transform.position, textureSet);

                for (int m = 0; m < ruleSet.modifiers.Length; m++)
                {
                    //Schedule job
                    jobHandle = ruleSet.modifiers[m].ScheduleModifier(world, states, jobHandle);

                    //Wait for job to complete
                    while (!jobHandle.IsCompleted) yield return new WaitForEndOfFrame();

                    //Complete Job
                    ruleSet.modifiers[m].CompleteModifier();
                }
            }

            //Setup data sets
            data = new GenusData[genera.Length];

            //Scan instances per data set
            for (int i = 0; i < data.Length; i++)
            {
                //Initialize new distribution
                NativeList<GenusInstance> output = new NativeList<GenusInstance>(Allocator.Persistent);

                //Check for override materials
                Material standard = (standardMaterials != null && standardMaterials.Length > i) ? standardMaterials[i] : null;
                Material billboard = (billboardMaterials != null && billboardMaterials.Length > i) ? billboardMaterials[i] : null;

                //Pass as data set
                data[i] = new GenusData(genera[i], standard, billboard, output, offset, bounds);

                //Perform distribution job
                DistributeScanJob distJob = new DistributeScanJob()
                {
                    distributionIndex = i,
                    distributionTotal = data.Length,

                    states = states,
                    output = output,

                    //Seed must be reset so random distribution is uniform
                    random = new Unity.Mathematics.Random((uint)seed),

                };
                jobHandle = distJob.Schedule(jobHandle);

                //Wait for setup to complete
                while (!jobHandle.IsCompleted) yield return new WaitForEndOfFrame();
                jobHandle.Complete();
            }

            //Dispose of states
            states.Dispose();
        }
        private void CancelAsyncGeneration()
        {
            //Stop any generation
            StopAllCoroutines();

            //Force complete any jobs
            jobHandle.Complete();

            //Clean up (May occur if generation is interrupted)
            if (states.IsCreated) states.Dispose();
        }

        [BurstCompile]
        public struct SetupJob : IJobParallelFor
        {
            public int countX;
            public int countY;

            public Vector3 bounds;
            public Vector3 offset;

            public Vector3 position;
            public Quaternion rotation;
            public Unity.Mathematics.Random random;

            public NativeArray<GenusState> states;

            public void Execute(int index)
            {
                //Calculate axis
                int x = index % countX;
                int y = Mathf.FloorToInt(index / (float)countY);

                //Calculate instance position
                Vector3 halfBounds = bounds / 2.0f;

                float xDist = bounds.x / countX;
                float xStart = xDist * 0.5f;
                float zDist = bounds.z / countY;
                float zStart = zDist * 0.5f;

                Vector3 localPosition = new Vector3(xStart + (x * xDist), bounds.y * 0.5f, zStart + (y * zDist));
                localPosition -= halfBounds;

                states[index] = new GenusState(new GenusInstance(localPosition, random.NextFloat(0, 360), Vector3.one, random.NextFloat(0, 1)));
            }
        }

        [BurstCompile]
        public struct DistributeScanJob : IJob
        {
            public int distributionIndex;
            public int distributionTotal;

            [ReadOnly] public NativeArray<GenusState> states;
            public NativeList<GenusInstance> output;
            public Unity.Mathematics.Random random;

            public void Execute()
            {
                for (int index = 0; index < states.Length; index++) Execute(index);
            }
            public void Execute(int index)
            {
                if (states[index].valid)
                {
                    int distribution = (states[index].distribution < 0) ? random.NextInt(0, distributionTotal) : states[index].distribution;
                    if (distribution == distributionIndex) output.Add(states[index].instance);
                }
            }
        }
    }
}