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
    public class ContourClip : Modifier
    {
        public float rMin = 0.0f;
        public float rMax = 1.0f;
        public float gMin = 0.0f;
        public float gMax = 1.0f;
        public float bMin = 0.0f;
        public float bMax = 1.0f;
        public float aMin = 0.0f;
        public float aMax = 1.0f;

        //Job
        JobHandle jobHandle;
        NativeArray<RGBAPixel> contour;

        //Mono
        private void OnDisable()
        {
            jobHandle.Complete();
            if (contour.IsCreated) contour.Dispose();
        }

        //Standard
        protected override void Apply(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                //Get Texture
                contour = world.textureSet.contour.GetRawTextureData<RGBAPixel>();

                //Setup job
                ContourClipJob job = new ContourClipJob()
                {
                    rMin = rMin,
                    rMax = rMax,
                    gMin = gMin,
                    gMax = gMax,
                    bMin = bMin,
                    bMax = bMax,
                    aMin = aMin,
                    aMax = aMax,

                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,

                    texture = contour,
                    states = states
                };

                //Schedule & complete
                jobHandle = job.Schedule(states.Length, batchSize);
                jobHandle.Complete();

                //Dispose of texture
                contour.Dispose();
            }
        }
        protected override IEnumerator ApplyAsync(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                //Get Texture
                contour = world.textureSet.contour.GetRawTextureData<RGBAPixel>();

                //Setup job
                ContourClipJob job = new ContourClipJob()
                {
                    rMin = rMin,
                    rMax = rMax,
                    gMin = gMin,
                    gMax = gMax,
                    bMin = bMin,
                    bMax = bMax,
                    aMin = aMin,
                    aMax = aMax,

                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,

                    texture = contour,
                    states = states
                };

                //Schedule
                jobHandle = job.Schedule(states.Length, batchSize);

                //Wait for setup to complete
                while (!jobHandle.IsCompleted) yield return new WaitForEndOfFrame();
                jobHandle.Complete();

                //Dispose of texture
                contour.Dispose();
            }
        }

        //Burst
        public override JobHandle ScheduleModifier(WorldState world, NativeArray<GenusState> states, JobHandle previous)
        {
            //Get Texture
            contour = world.textureSet.contour.GetRawTextureData<RGBAPixel>();

            //Setup job
            ContourClipJob job = new ContourClipJob()
            {
                rMin = rMin,
                rMax = rMax,
                gMin = gMin,
                gMax = gMax,
                bMin = bMin,
                bMax = bMax,
                aMin = aMin,
                aMax = aMax,

                resolutionX = world.textureSet.resolutionX,
                resolutionY = world.textureSet.resolutionY,

                bounds = world.textureSet.bounds,

                texture = contour,
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
            if (contour.IsCreated) contour.Dispose();
        }

#if UNITY_EDITOR
        public override bool Draw()
        {
            SerializedObject serializedObject = new SerializedObject(this);

            SerializedProperty rMin = serializedObject.FindProperty("rMin");
            SerializedProperty rMax = serializedObject.FindProperty("rMax");

            SerializedProperty gMin = serializedObject.FindProperty("gMin");
            SerializedProperty gMax = serializedObject.FindProperty("gMax");

            SerializedProperty bMin = serializedObject.FindProperty("bMin");
            SerializedProperty bMax = serializedObject.FindProperty("bMax");

            SerializedProperty aMin = serializedObject.FindProperty("aMin");
            SerializedProperty aMax = serializedObject.FindProperty("aMax");

            float rMinValue = rMin.floatValue;
            float rMaxValue = rMax.floatValue;

            float gMinValue = gMin.floatValue;
            float gMaxValue = gMax.floatValue;

            float bMinValue = bMin.floatValue;
            float bMaxValue = bMax.floatValue;

            float aMinValue = aMin.floatValue;
            float aMaxValue = aMax.floatValue;

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.MinMaxSlider("Below Minor", ref rMinValue, ref rMaxValue, 0.0f, 1.0f);
            EditorGUILayout.MinMaxSlider("Below Major", ref gMinValue, ref gMaxValue, 0.0f, 1.0f);
            EditorGUILayout.MinMaxSlider("Above Minor", ref bMinValue, ref bMaxValue, 0.0f, 1.0f);
            EditorGUILayout.MinMaxSlider("Above Major", ref aMinValue, ref aMaxValue, 0.0f, 1.0f);

            if (EditorGUI.EndChangeCheck())
            {
                rMin.floatValue = rMinValue;
                rMax.floatValue = rMaxValue;

                gMin.floatValue = gMinValue;
                gMax.floatValue = gMaxValue;

                bMin.floatValue = bMinValue;
                bMax.floatValue = bMaxValue;

                aMin.floatValue = aMinValue;
                aMax.floatValue = aMaxValue;

                serializedObject.ApplyModifiedProperties();
                return true;
            }
            else return false;
        }
#endif

        public struct RGBAPixel
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;

            public float4 rgba
            {
                get { return new float4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f); }
            }
        }

        [BurstCompile]
        public struct ContourClipJob : IJobParallelFor
        {
            public float rMin;
            public float rMax;
            public float gMin;
            public float gMax;
            public float bMin;
            public float bMax;
            public float aMin;
            public float aMax;

            public int resolutionX;
            public int resolutionY;

            public float3 bounds;

            public NativeArray<GenusState> states;
            [ReadOnly] public NativeArray<RGBAPixel> texture;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];

                if (!state.valid) return;

                float3 localPosition = state.instance.position;
                float2 samplePostion = new float2(localPosition.x, localPosition.z);

                float4 value = SampleTextureBilinear(samplePostion);

                bool rValid = value.x >= rMin && value.x <= rMax;
                bool gValid = value.y >= gMin && value.y <= gMax;
                bool bValid = value.z >= bMin && value.z <= bMax;
                bool aValid = value.w >= aMin && value.w <= aMax;

                state.valid = (state.valid && rValid && gValid && bValid && aValid);

                states[index] = state;
            }

            public float4 SampleTexture(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int x = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int y = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);

                return texture[x + (y * resolutionX)].rgba;
            }
            public float4 SampleTextureBilinear(float2 coords)
            {
                float2 normalizedCoords = new float2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int xMin = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int yMin = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);
                int xMax = math.clamp(xMin + 1, 0, resolutionX - 1);
                int yMax = math.clamp(yMin + 1, 0, resolutionY - 1);

                float xLerp = (normalizedCoords.x * resolutionX) - xMin;
                float yLerp = (normalizedCoords.y * resolutionY) - yMin;

                float4 minXminY = texture[xMin + (yMin * resolutionX)].rgba;
                float4 maxXminY = texture[xMax + (yMin * resolutionX)].rgba;
                float4 minXmaxY = texture[xMin + (yMax * resolutionX)].rgba;
                float4 maxXmaxY = texture[xMax + (yMax * resolutionX)].rgba;

                float4 minX = math.lerp(minXminY, minXmaxY, yLerp);
                float4 maxX = math.lerp(maxXminY, maxXmaxY, yLerp);

                return math.lerp(minX, maxX, xLerp);
            }
        }
    }
}