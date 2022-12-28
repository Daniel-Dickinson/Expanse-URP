using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TwoBears.Expanse
{
    public class HeightClip : Modifier
    {
        public float min = 0.0f;
        public float max = 0.8f;

        //Job
        JobHandle jobHandle;
        NativeArray<R16Pixel> height;

        //Mono
        private void OnDisable()
        {
            jobHandle.Complete();
            if (height.IsCreated) height.Dispose();
        }

        //Standard
        protected override void Apply(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                //Get Texture
                height = world.textureSet.height.GetRawTextureData<R16Pixel>();

                //Setup job
                HeightClipJob job = new HeightClipJob()
                {
                    min = min,
                    max = max,

                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,
                    direction = world.textureSet.direction,

                    texture = height,
                    states = states
                };

                //Schedule & complete
                jobHandle = job.Schedule(states.Length, batchSize);
                jobHandle.Complete();

                //Dispose of texture
                height.Dispose();
            }
        }
        protected override IEnumerator ApplyAsync(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                //Get Texture
                height = world.textureSet.height.GetRawTextureData<R16Pixel>();

                //Setup job
                HeightClipJob job = new HeightClipJob()
                {
                    min = min,
                    max = max,

                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,
                    direction = world.textureSet.direction,

                    texture = height,
                    states = states
                };

                //Schedule
                jobHandle = job.Schedule(states.Length, batchSize);

                //Wait for setup to complete
                while (!jobHandle.IsCompleted) yield return new WaitForEndOfFrame();
                jobHandle.Complete();

                //Dispose of texture
                height.Dispose();
            }
        }

        //Burst
        public override JobHandle ScheduleModifier(WorldState world, NativeArray<GenusState> states, JobHandle previous)
        {
            //Get Texture
            height = world.textureSet.height.GetRawTextureData<R16Pixel>();

            //Setup job
            HeightClipJob job = new HeightClipJob()
            {
                min = min,
                max = max,

                resolutionX = world.textureSet.resolutionX,
                resolutionY = world.textureSet.resolutionY,

                bounds = world.textureSet.bounds,
                direction = world.textureSet.direction,

                texture = height,
                states = states
            };

            //Schedule
            jobHandle = job.Schedule(states.Length, batchSize, previous);

            //Return
            return jobHandle;
            
        }
        public override void CompleteModifier()
        {
            jobHandle.Complete();
            if (height.IsCreated) height.Dispose();
        }


#if UNITY_EDITOR
        public override bool Draw()
        {
            SerializedObject serializedObject = new SerializedObject(this);
            SerializedProperty min = serializedObject.FindProperty("min");
            SerializedProperty max = serializedObject.FindProperty("max");

            float minValue = min.floatValue;
            float maxValue = max.floatValue;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.MinMaxSlider("Threshold", ref minValue, ref maxValue, 0.0f, 1.0f);

            if (EditorGUI.EndChangeCheck())
            {
                min.floatValue = minValue;
                max.floatValue = maxValue;
                serializedObject.ApplyModifiedProperties();
                return true;
            }
            else return false;
        }
#endif

        public struct R16Pixel
        {
            public byte r1;
            public byte r2;

            public float value
            {
                get
                {
                    float value = r1 + (r2 * 256.0f);
                    return value / 65536.0f;
                }
            }
        }

        [BurstCompile]
        public struct HeightClipJob : IJobParallelFor
        {
            public float min;
            public float max;

            public int resolutionX;
            public int resolutionY;

            public float3 bounds;
            public SampleDirection direction;

            public NativeArray<GenusState> states;
            [ReadOnly] public NativeArray<R16Pixel> texture;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];

                if (!state.valid) return;

                float3 localPosition = state.instance.position;
                float2 samplePostion = new float2(localPosition.x, localPosition.z);

                switch (direction)
                {
                    case SampleDirection.Down:
                        float downValue = 1 - SampleTextureBilinear(samplePostion);
                        state.valid = (state.valid && downValue >= min && downValue <= max);
                        break;
                    case SampleDirection.Up:
                        float upValue = SampleTextureBilinear(samplePostion);
                        state.valid = (state.valid && upValue >= min && upValue <= max);
                        break;
                }

                states[index] = state;
            }

            public float SampleTexture(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int x = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int y = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);

                return texture[x + (y * resolutionX)].value;
            }
            public float SampleTextureBilinear(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int xMin = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int yMin = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);
                int xMax = math.clamp(xMin + 1, 0, resolutionX - 1);
                int yMax = math.clamp(yMin + 1, 0, resolutionY - 1);

                float xLerp = (normalizedCoords.x * resolutionX) - xMin;
                float yLerp = (normalizedCoords.y * resolutionY) - yMin;

                float minXminY = texture[xMin + (yMin * resolutionX)].value;
                float maxXminY = texture[xMax + (yMin * resolutionX)].value;
                float minXmaxY = texture[xMin + (yMax * resolutionX)].value;
                float maxXmaxY = texture[xMax + (yMax * resolutionX)].value;

                float minX = math.lerp(minXminY, minXmaxY, yLerp);
                float maxX = math.lerp(maxXminY, maxXmaxY, yLerp);

                return math.lerp(minX, maxX, xLerp);
            }
        }
    }
}