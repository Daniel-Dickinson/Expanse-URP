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
    public class NormalClip : Modifier
    {
        public float normalClip = 0.5f;

        //Job
        JobHandle jobHandle;
        NativeArray<RGBPixel> normal;

        //Mono
        private void OnDisable()
        {
            jobHandle.Complete();
            if (normal.IsCreated) normal.Dispose();
        }

        //Standard
        protected override void Apply(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                //Get Texture
                normal = world.textureSet.normal.GetRawTextureData<RGBPixel>();

                //Setup job
                NormalClipJob job = new NormalClipJob()
                {
                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,

                    normalClip = normalClip,

                    texture = normal,
                    states = states
                };

                //Schedule & complete
                jobHandle = job.Schedule(states.Length, batchSize);
                jobHandle.Complete();

                //Dispose of texture
                normal.Dispose();
            }
        }
        protected override IEnumerator ApplyAsync(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                //Get Texture
                normal = world.textureSet.normal.GetRawTextureData<RGBPixel>();

                //Setup job
                NormalClipJob job = new NormalClipJob()
                {
                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,

                    normalClip = normalClip,

                    texture = normal,
                    states = states
                };

                //Schedule
                jobHandle = job.Schedule(states.Length, batchSize);

                //Wait for setup to complete
                while (!jobHandle.IsCompleted) yield return new WaitForEndOfFrame();
                jobHandle.Complete();

                //Dispose of texture
                normal.Dispose();
            }
        }

        //Burst
        public override JobHandle ScheduleModifier(WorldState world, NativeArray<GenusState> states, JobHandle previous)
        {
            //Get Texture
            normal = world.textureSet.normal.GetRawTextureData<RGBPixel>();

            //Setup job
            NormalClipJob job = new NormalClipJob()
            {
                resolutionX = world.textureSet.resolutionX,
                resolutionY = world.textureSet.resolutionY,

                bounds = world.textureSet.bounds,

                normalClip = normalClip,

                texture = normal,
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
            if (normal.IsCreated) normal.Dispose();
        }


#if UNITY_EDITOR
        public override bool Draw()
        {
            SerializedObject serializedObject = new SerializedObject(this);
            SerializedProperty normalClip = serializedObject.FindProperty("normalClip");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.Slider(normalClip, 0, 1);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                return true;
            }
            else return false;
        }
#endif

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
        public struct NormalClipJob : IJobParallelFor
        {
            public int resolutionX;
            public int resolutionY;

            public float normalClip;

            public float3 bounds;

            public NativeArray<GenusState> states;
            [ReadOnly] public NativeArray<RGBPixel> texture;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];

                if (!state.valid) return;

                float3 localPosition = state.instance.position;
                float2 samplePostion = new float2(localPosition.x, localPosition.z);

                float3 packed = SampleTextureBilinear(samplePostion);
                float3 unpacked = new float3((packed.x - 0.5f) * 2, (packed.y - 0.5f) * 2, (packed.z - 0.5f) * 2);
                float dot = math.dot(unpacked, new float3(0, 1, 0));

                state.valid = (dot > normalClip);

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