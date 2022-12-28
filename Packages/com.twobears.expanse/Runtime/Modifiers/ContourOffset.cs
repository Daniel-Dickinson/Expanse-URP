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
    public class ContourOffset : Modifier
    {
        public float contourMin = 0.0f;
        public float contourMax = 1.0f;

        public Vector3 offsetMin = Vector3.zero;
        public Vector3 offsetMax = Vector3.zero;

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
                ContourOffsetJob job = new ContourOffsetJob()
                {
                    offsetMin = offsetMin,
                    offsetMax = offsetMax,

                    contourMin = contourMin,
                    contourMax = contourMax,

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
                ContourOffsetJob job = new ContourOffsetJob()
                {
                    offsetMin = offsetMin,
                    offsetMax = offsetMax,

                    contourMin = contourMin,
                    contourMax = contourMax,

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
            ContourOffsetJob job = new ContourOffsetJob()
            {
                offsetMin = offsetMin,
                offsetMax = offsetMax,

                contourMin = contourMin,
                contourMax = contourMax,

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

            SerializedProperty offsetMin = serializedObject.FindProperty("offsetMin");
            SerializedProperty offsetMax = serializedObject.FindProperty("offsetMax");

            SerializedProperty contourMin = serializedObject.FindProperty("contourMin");
            SerializedProperty contourMax = serializedObject.FindProperty("contourMax");

            float minValue = contourMin.floatValue;
            float maxValue = contourMax.floatValue;

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.MinMaxSlider("Contour", ref minValue, ref maxValue, 0.0f, 1.0f);
            EditorGUILayout.PropertyField(offsetMin);
            EditorGUILayout.PropertyField(offsetMax);

            if (EditorGUI.EndChangeCheck())
            {
                contourMin.floatValue = minValue;
                contourMax.floatValue = maxValue;

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
        public struct ContourOffsetJob : IJobParallelFor
        {
            public Vector3 offsetMin;
            public Vector3 offsetMax;

            public float contourMin;
            public float contourMax;

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
                float lerp = Mathf.InverseLerp(contourMin, contourMax, value.w);
                state.instance.position += Vector3.Lerp(offsetMin, offsetMax, lerp);

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