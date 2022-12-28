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
    public class TextureClip : Modifier
    {
        public float rMin = 0;
        public float rMax = 1;
        public float gMin = 0;
        public float gMax = 1;
        public float bMin = 0;
        public float bMax = 1;

        //Job
        JobHandle jobHandle;
        NativeArray<RGBPixel> texture;

        //Mono
        private void OnDisable()
        {
            jobHandle.Complete();
            if (texture.IsCreated) texture.Dispose();
        }

        //Standard
        protected override void Apply(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                //Get Texture
                texture = world.textureSet.color.GetRawTextureData<RGBPixel>();

                //Setup job
                TextureClipJob job = new TextureClipJob()
                {
                    rMin = rMin,
                    rMax = rMax,
                    gMin = gMin,
                    gMax = gMax,
                    bMin = bMin,
                    bMax = bMax,

                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,

                    texture = texture,
                    states = states
                };

                //Schedule & complete
                jobHandle = job.Schedule(states.Length, batchSize);
                jobHandle.Complete();

                //Dispose of texture
                texture.Dispose();
            }
        }
        protected override IEnumerator ApplyAsync(WorldState world, NativeArray<GenusState> states)
        {
            if (world.textureSet != null)
            {
                //Get Texture
                texture = world.textureSet.color.GetRawTextureData<RGBPixel>();

                //Setup job
                TextureClipJob job = new TextureClipJob()
                {
                    rMin = rMin,
                    rMax = rMax,
                    gMin = gMin,
                    gMax = gMax,
                    bMin = bMin,
                    bMax = bMax,

                    resolutionX = world.textureSet.resolutionX,
                    resolutionY = world.textureSet.resolutionY,

                    bounds = world.textureSet.bounds,

                    texture = texture,
                    states = states
                };

                //Schedule
                jobHandle = job.Schedule(states.Length, batchSize);

                //Wait for setup to complete
                while (!jobHandle.IsCompleted) yield return new WaitForEndOfFrame();
                jobHandle.Complete();

                //Dispose of texture
                texture.Dispose();
            }
        }

        //Burst
        public override JobHandle ScheduleModifier(WorldState world, NativeArray<GenusState> states, JobHandle previous)
        {
            //Get Texture
            texture = world.textureSet.color.GetRawTextureData<RGBPixel>();

            //Setup job
            TextureClipJob job = new TextureClipJob()
            {
                rMin = rMin,
                rMax = rMax,
                gMin = gMin,
                gMax = gMax,
                bMin = bMin,
                bMax = bMax,

                resolutionX = world.textureSet.resolutionX,
                resolutionY = world.textureSet.resolutionY,

                bounds = world.textureSet.bounds,

                texture = texture,
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
            if (texture.IsCreated) texture.Dispose();
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

            EditorGUI.BeginChangeCheck();

            using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
            {
                float rmin = rMin.floatValue;
                float rmax = rMax.floatValue;
                EditorGUILayout.MinMaxSlider(new GUIContent("Red"), ref rmin, ref rmax, 0, 1);

                float gmin = gMin.floatValue;
                float gmax = gMax.floatValue;
                EditorGUILayout.MinMaxSlider(new GUIContent("Green"), ref gmin, ref gmax, 0, 1);

                float bmin = bMin.floatValue;
                float bmax = bMax.floatValue;
                EditorGUILayout.MinMaxSlider(new GUIContent("Blue"), ref bmin, ref bmax, 0, 1);

                if (scope.changed)
                {
                    rMin.floatValue = rmin;
                    rMax.floatValue = rmax;
                    gMin.floatValue = gmin;
                    gMax.floatValue = gmax;
                    bMin.floatValue = bmin;
                    bMax.floatValue = bmax;
                }
            }

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
        public struct TextureClipJob : IJobParallelFor
        {
            public float rMin;
            public float rMax;
            public float gMin;
            public float gMax;
            public float bMin;
            public float bMax;

            public int resolutionX;
            public int resolutionY;

            public Vector3 bounds;

            public NativeArray<GenusState> states;
            [ReadOnly] public NativeArray<RGBPixel> texture;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];

                Vector3 localPosition = state.instance.position;
                Vector2 samplePostion = new Vector2(localPosition.x, localPosition.z);
                float3 color = SampleTextureBilinear(samplePostion);

                if (color.x < rMin || color.x > rMax) state.valid = false;
                if (color.y < gMin || color.y > gMax) state.valid = false;
                if (color.z < bMin || color.z > bMax) state.valid = false;

                states[index] = state;
            }

            public float3 SampleTexture(float2 coords)
            {
                Vector2 normalizedCoords = new Vector2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

                int x = math.clamp((int)math.floor(normalizedCoords.x * resolutionX), 0, resolutionX - 1);
                int y = math.clamp((int)math.floor(normalizedCoords.y * resolutionY), 0, resolutionY - 1);

                return texture[x + (y * resolutionX)].rgb;
            }
            public float3 SampleTextureBilinear(float2 coords)
            {
                Vector2 normalizedCoords = new Vector2((coords.x + (bounds.x * 0.5f)) / bounds.x, (coords.y + (bounds.z * 0.5f)) / bounds.z);

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