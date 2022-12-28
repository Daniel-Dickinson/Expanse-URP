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
    public class TextureSample : Modifier
    {
        public bool sampleHeight;
        public bool sampleNormal;
        public bool sampleColor;

        //Job
        JobHandle jobHandle;

        NativeArray<R16Pixel> height;
        NativeArray<RGBPixel> normal;
        NativeArray<RGBPixel> color;

        //Mono
        private void OnDisable()
        {
            jobHandle.Complete();

            if (height.IsCreated) height.Dispose();
            if (color.IsCreated) color.Dispose();
            if (normal.IsCreated) normal.Dispose();
        }

        //Standard
        protected override void Apply(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                if (sampleHeight)
                {
                    //Get Texture
                    height = world.textureSet.height.GetRawTextureData<R16Pixel>();

                    //Setup job
                    HeightSampleJob job = new HeightSampleJob()
                    {
                        resolutionX = world.textureSet.resolutionX,
                        resolutionY = world.textureSet.resolutionY,
                        maxDistance = world.textureSet.maxDistance,

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

                if (sampleNormal)
                {
                    //Get Texture
                    normal = world.textureSet.normal.GetRawTextureData<RGBPixel>();

                    //Setup job
                    NormalSampleJob job = new NormalSampleJob()
                    {
                        resolutionX = world.textureSet.resolutionX,
                        resolutionY = world.textureSet.resolutionY,

                        bounds = world.textureSet.bounds,

                        texture = normal,
                        states = states
                    };

                    //Schedule & complete
                    jobHandle = job.Schedule(states.Length, batchSize);
                    jobHandle.Complete();

                    //Dispose of texture
                    normal.Dispose();
                }

                if (sampleColor)
                {
                    //Get Texture
                    color = world.textureSet.color.GetRawTextureData<RGBPixel>();

                    //Setup job
                    ColorSampleJob job = new ColorSampleJob()
                    {
                        resolutionX = world.textureSet.resolutionX,
                        resolutionY = world.textureSet.resolutionY,

                        bounds = world.textureSet.bounds,

                        texture = color,
                        states = states
                    };

                    //Schedule & complete
                    jobHandle = job.Schedule(states.Length, batchSize);
                    jobHandle.Complete();

                    //Dispose of texture
                    color.Dispose();
                }
            }
        }
        protected override IEnumerator ApplyAsync(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                if (sampleHeight)
                {
                    //Get Texture
                    height = world.textureSet.height.GetRawTextureData<R16Pixel>();

                    //Setup job
                    HeightSampleJob job = new HeightSampleJob()
                    {
                        resolutionX = world.textureSet.resolutionX,
                        resolutionY = world.textureSet.resolutionY,
                        maxDistance = world.textureSet.maxDistance,

                        bounds = world.textureSet.bounds,
                        direction = world.textureSet.direction,

                        texture = height,
                        states = states
                    };

                    //Schedule & complete
                    jobHandle = job.Schedule(states.Length, batchSize);

                    //Dispose of texture
                    height.Dispose();
                }

                if (sampleNormal)
                {
                    //Get Texture
                    normal = world.textureSet.normal.GetRawTextureData<RGBPixel>();

                    //Setup job
                    NormalSampleJob job = new NormalSampleJob()
                    {
                        resolutionX = world.textureSet.resolutionX,
                        resolutionY = world.textureSet.resolutionY,

                        bounds = world.textureSet.bounds,

                        texture = normal,
                        states = states
                    };

                    //Schedule & complete
                    jobHandle = job.Schedule(states.Length, batchSize, jobHandle);

                    //Dispose of texture
                    normal.Dispose();
                }

                if (sampleColor)
                {
                    //Get Texture
                    color = world.textureSet.color.GetRawTextureData<RGBPixel>();

                    //Setup job
                    ColorSampleJob job = new ColorSampleJob()
                    {
                        resolutionX = world.textureSet.resolutionX,
                        resolutionY = world.textureSet.resolutionY,

                        bounds = world.textureSet.bounds,

                        texture = color,
                        states = states
                    };

                    //Schedule
                    jobHandle = job.Schedule(states.Length, batchSize, jobHandle);

                    //Wait for setup to complete
                    while (!jobHandle.IsCompleted) yield return new WaitForEndOfFrame();
                    jobHandle.Complete();

                    //Dispose of texture
                    color.Dispose();
                }
            }
        }

        //Burst
        public override JobHandle ScheduleModifier(WorldState world, NativeArray<GenusState> states, JobHandle previous)
        {
            if (sampleHeight)
            {
                //Get Texture
                height = world.textureSet.height.GetRawTextureData<R16Pixel>();

                //Setup job
                HeightSampleJob job = new HeightSampleJob()
                {
                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,
                    maxDistance = world.textureSet.maxDistance,

                    bounds = world.textureSet.bounds,
                    direction = world.textureSet.direction,

                    texture = height,
                    states = states
                };

                //Schedule & complete
                jobHandle = job.Schedule(states.Length, batchSize, previous);
            }

            if (sampleNormal)
            {
                //Get Texture
                normal = world.textureSet.normal.GetRawTextureData<RGBPixel>();

                //Setup job
                NormalSampleJob job = new NormalSampleJob()
                {
                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,

                    texture = normal,
                    states = states
                };

                //Schedule & complete
                jobHandle = job.Schedule(states.Length, batchSize, jobHandle);
            }

            if (sampleColor)
            {
                //Get Texture
                color = world.textureSet.color.GetRawTextureData<RGBPixel>();

                //Setup job
                ColorSampleJob job = new ColorSampleJob()
                {
                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,

                    texture = color,
                    states = states
                };

                //Schedule & complete
                jobHandle = job.Schedule(states.Length, batchSize, jobHandle);
            }

            //Return
            return jobHandle;
        }
        public override void CompleteModifier()
        {
            jobHandle.Complete();

            if (height.IsCreated) height.Dispose();
            if (color.IsCreated) color.Dispose();
            if (normal.IsCreated) normal.Dispose();
        }


#if UNITY_EDITOR
        public override bool Draw()
        {
            SerializedObject serializedObject = new SerializedObject(this);
            SerializedProperty sampleHeight = serializedObject.FindProperty("sampleHeight");
            SerializedProperty sampleNormal = serializedObject.FindProperty("sampleNormal");
            SerializedProperty sampleColor = serializedObject.FindProperty("sampleColor");

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(sampleHeight);
            EditorGUILayout.PropertyField(sampleNormal);
            EditorGUILayout.PropertyField(sampleColor);

            if (EditorGUI.EndChangeCheck())
            {
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
        public struct RGBPixel
        {
            public byte r;
            public byte g;
            public byte b;

            public float3 rgb
            {
                get { return new float3(r / 255.0f, g / 255.0f, b / 255.0f); }
            }
        }

        [BurstCompile]
        public struct HeightSampleJob : IJobParallelFor
        {
            public int resolutionX;
            public int resolutionY;
            public float maxDistance;

            public float3 bounds;
            public SampleDirection direction;

            public NativeArray<GenusState> states;
            [ReadOnly] public NativeArray<R16Pixel> texture;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];

                if (state.valid)
                {
                    float3 localPosition = state.instance.position;
                    float2 samplePostion = new float2(localPosition.x, localPosition.z);

                    if (SampleMask(samplePostion))
                    {
                        bool clip = SampleTextureBilinearClip(samplePostion, maxDistance, 2.0f);
                        if (clip) state.valid = false;
                        else
                        {
                            float value = SampleTextureBilinear(samplePostion);

                            switch (direction)
                            {
                                case SampleDirection.Down:
                                    state.instance.position.y += (0.5f * bounds.y);
                                    state.instance.position.y -= (value * maxDistance);
                                    break;
                                case SampleDirection.Up:
                                    state.instance.position.y += (0.5f * bounds.y);
                                    state.instance.position.y -= (1.0f - value) * maxDistance;
                                    break;
                            }
                        }
                    }
                    else state.valid = false;
                }

                states[index] = state;
            }

            public bool SampleMask(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int xMin = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int yMin = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);
                int xMax = math.clamp((int)math.ceil(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int yMax = math.clamp((int)math.ceil(normalizedCoords.y * resolutionY), 0, resolutionY - 1);

                if (texture[xMin + (yMin * resolutionX)].value == 0) return false;
                if (texture[xMin + (yMax * resolutionX)].value == 0) return false;
                if (texture[xMax + (yMin * resolutionX)].value == 0) return false;
                if (texture[xMax + (yMax * resolutionX)].value == 0) return false;

                return true;
            }
            public float SampleTexture(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int x = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int y = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);

                return texture[x + (y * resolutionX)].value;
            }
            public bool SampleTextureBilinearClip(float2 coords, float maxDistance, float threshold)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int xMin = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int yMin = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);
                int xMax = math.clamp(xMin + 1, 0, resolutionX - 1);
                int yMax = math.clamp(yMin + 1, 0, resolutionY - 1);

                float minXminY = texture[xMin + (yMin * resolutionX)].value;
                float maxXminY = texture[xMax + (yMin * resolutionX)].value;
                float minXmaxY = texture[xMin + (yMax * resolutionX)].value;
                float maxXmaxY = texture[xMax + (yMax * resolutionX)].value;

                bool minX = Mathf.Abs(minXminY - minXmaxY) * maxDistance > threshold;
                bool maxX = Mathf.Abs(maxXminY - maxXmaxY) * maxDistance > threshold;
                bool minY = Mathf.Abs(minXminY - maxXminY) * maxDistance > threshold;
                bool maxY = Mathf.Abs(minXmaxY - maxXmaxY) * maxDistance > threshold;

                return minX || maxX || minY || maxY;
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

        [BurstCompile]
        public struct NormalSampleJob : IJobParallelFor
        {
            public int resolutionX;
            public int resolutionY;

            public float3 bounds;

            public NativeArray<GenusState> states;
            [ReadOnly] public NativeArray<RGBPixel> texture;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];

                float3 localPosition = state.instance.position;
                float2 samplePostion = new float2(localPosition.x, localPosition.z);

                float3 packed = SampleTextureBilinear(samplePostion);
                float3 unpacked = new float3((packed.x - 0.5f) * 2, (packed.y - 0.5f) * 2, (packed.z - 0.5f) * 2);
                state.instance.rotation = Quaternion.FromToRotation(new float3(0, 1, 0), unpacked) * state.instance.rotation;

                states[index] = state;
            }

            public float3 SampleTexture(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int x = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int y = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);

                return texture[x + (y * resolutionX)].rgb;
            }
            public float3 SampleTextureBilinear(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int xMin = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int yMin = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);
                int xMax = math.clamp(xMin + 1, 0, resolutionX - 1);
                int yMax = math.clamp(yMin + 1, 0, resolutionY - 1);

                float xLerp = (normalizedCoords.x * resolutionX) - xMin;
                float yLerp = (normalizedCoords.y * resolutionY) - yMin;

                float3 minXminY = texture[xMin + (yMin * resolutionX)].rgb;
                float3 maxXminY = texture[xMax + (yMin * resolutionX)].rgb;
                float3 minXmaxY = texture[xMin + (yMax * resolutionX)].rgb;
                float3 maxXmaxY = texture[xMax + (yMax * resolutionX)].rgb;

                float3 minX = math.lerp(minXminY, minXmaxY, yLerp);
                float3 maxX = math.lerp(maxXminY, maxXmaxY, yLerp);

                return math.lerp(minX, maxX, xLerp);
            }
        }

        [BurstCompile]
        public struct ColorSampleJob : IJobParallelFor
        {
            public int resolutionX;
            public int resolutionY;

            public float3 bounds;

            public NativeArray<GenusState> states;
            [ReadOnly] public NativeArray<RGBPixel> texture;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];

                float3 localPosition = state.instance.position;
                float2 samplePostion = new float2(localPosition.x, localPosition.z);
                float3 color = SampleTextureBilinear(samplePostion);
                state.instance.extra = new float4(color.x, color.y, color.z, state.instance.extra.w);

                states[index] = state;
            }

            public float3 SampleTexture(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int x = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int y = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);

                return texture[x + (y * resolutionX)].rgb;
            }
            public float3 SampleTextureBilinear(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int xMin = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int yMin = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);
                int xMax = math.clamp(xMin + 1, 0, resolutionX - 1);
                int yMax = math.clamp(yMin + 1, 0, resolutionY - 1);

                float xLerp = (normalizedCoords.x * resolutionX) - xMin;
                float yLerp = (normalizedCoords.y * resolutionY) - yMin;

                float3 minXminY = texture[xMin + (yMin * resolutionX)].rgb;
                float3 maxXminY = texture[xMax + (yMin * resolutionX)].rgb;
                float3 minXmaxY = texture[xMin + (yMax * resolutionX)].rgb;
                float3 maxXmaxY = texture[xMax + (yMax * resolutionX)].rgb;

                float3 minX = math.lerp(minXminY, minXmaxY, yLerp);
                float3 maxX = math.lerp(maxXminY, maxXmaxY, yLerp);

                return math.lerp(minX, maxX, xLerp);
            }
        }
    }
}