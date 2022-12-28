using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TwoBears.Expanse
{
    [CreateAssetMenu(menuName = "Expanse/Perlin Set")]
    public class PerlinSet : ScriptableObject
    {
        public float strengthOne = 0.5f;
        public float frequencyOne = 0.01f;

        public float strengthTwo = 0.3f;
        public float frequencyTwo = 0.1f;

        public float strengthThree = 0.2f;
        public float frequencyThree = 1.0f;

        public float SampleSet(Vector2 coords)
        {
            float octaveOne = Mathf.PerlinNoise(coords.x * frequencyOne, coords.y * frequencyOne) * strengthOne;
            float octaveTwo = Mathf.PerlinNoise(coords.x * frequencyTwo, coords.y * frequencyTwo) * strengthTwo;
            float octaveThree = Mathf.PerlinNoise(coords.x * frequencyThree, coords.y * frequencyThree) * strengthThree;
            float total = octaveOne + octaveTwo + octaveThree;

            return total;
        }

#if UNITY_EDITOR
        public void Draw()
        {
            SerializedObject serializedObject = new SerializedObject(this);

            SerializedProperty strengthOne = serializedObject.FindProperty("strengthOne");
            SerializedProperty frequencyOne = serializedObject.FindProperty("frequencyOne");

            SerializedProperty strengthTwo = serializedObject.FindProperty("strengthTwo");
            SerializedProperty frequencyTwo = serializedObject.FindProperty("frequencyTwo");

            SerializedProperty strengthThree = serializedObject.FindProperty("strengthThree");
            SerializedProperty frequencyThree = serializedObject.FindProperty("frequencyThree");

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(strengthOne);
            EditorGUILayout.PropertyField(frequencyOne);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(strengthTwo);
            EditorGUILayout.PropertyField(frequencyTwo);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(strengthThree);
            EditorGUILayout.PropertyField(frequencyThree);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Average - " + ((strengthOne.floatValue + strengthTwo.floatValue + strengthThree.floatValue) / 2.0f));

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
#endif

        [BurstCompile]
        public struct PerlinToTexture : IJob
        {
            public float min;

            public float strengthOne;
            public float frequencyOne;

            public float strengthTwo;
            public float frequencyTwo;

            public float strengthThree;
            public float frequencyThree;

            public int resolutionX;
            public int resolutionY;

            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public Vector3 worldScale;

            public NativeArray<Color32> textureData;

            void IJob.Execute()
            {
                for (int x = 0; x < resolutionX; x++)
                {
                    for (int y = 0; y < resolutionY; y++)
                    {
                        //Calculate texture index
                        int index = x + (resolutionX * y);

                        //Calculate sample position
                        Vector3 samplePosition = worldPosition + (worldRotation * new Vector3(x * worldScale.x, 0, y * worldScale.z));

                        //Offset by half ((0,0) should sample bottom left, not center)
                        samplePosition -= worldRotation * new Vector3(resolutionX * -worldScale.x * 0.5f, 0, resolutionY * -worldScale.z * 0.5f);

                        //Create noise
                        float octaveOne = math.unlerp(-1, 1, noise.cnoise(samplePosition * frequencyOne)) * strengthOne;
                        float octaveTwo = math.unlerp(-1, 1, noise.cnoise(samplePosition * frequencyTwo)) * strengthTwo;
                        float octaveThree = math.unlerp(-1, 1, noise.cnoise(samplePosition * frequencyThree)) * strengthThree;
                        float total = min + octaveOne + octaveTwo + octaveThree;

                        int value = (int)(total * 255);

                        //Write to color data
                        textureData[index] = new Color32((byte)value, (byte)value, (byte)value, 255);
                    }
                }
            }
        }
    }
}