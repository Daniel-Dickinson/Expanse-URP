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
    public class PerlinClip : Modifier
    {
        public float min = 0.0f;
        public float max = 0.8f;
        public PerlinSet set;

        //Job
        JobHandle jobHandle;

        //Mono
        private void OnDisable()
        {
            jobHandle.Complete();
        }

        //Standard
        protected override void Apply(WorldState world, NativeArray<GenusState> states)
        {
            //Set required
            if (set == null) return;

            //Setup job
            PerlinClipJob job = new PerlinClipJob()
            {
                min = min,
                max = max,

                strengthOne = set.strengthOne,
                strengthTwo = set.strengthTwo,
                strengthThree = set.strengthThree,

                frequencyOne = set.frequencyOne,
                frequencyTwo = set.frequencyTwo,
                frequencyThree = set.frequencyThree,

                position = world.position,
                states = states
            };

            //Schedule & complete
            jobHandle = job.Schedule(states.Length, batchSize);
            jobHandle.Complete();
        }
        protected override IEnumerator ApplyAsync(WorldState world, NativeArray<GenusState> states)
        {
            //Set required
            if (set == null) yield break;

            //Setup job
            PerlinClipJob job = new PerlinClipJob()
            {
                min = min,
                max = max,

                strengthOne = set.strengthOne,
                strengthTwo = set.strengthTwo,
                strengthThree = set.strengthThree,

                frequencyOne = set.frequencyOne,
                frequencyTwo = set.frequencyTwo,
                frequencyThree = set.frequencyThree,

                position = world.position,
                states = states
            };

            //Schedule
            jobHandle = job.Schedule(states.Length, batchSize);

            //Wait for setup to complete
            while (!jobHandle.IsCompleted) yield return new WaitForEndOfFrame();
            jobHandle.Complete();
        }

        //Burst
        public override JobHandle ScheduleModifier(WorldState world, NativeArray<GenusState> states, JobHandle previous)
        {
            //Setup job
            PerlinClipJob job = new PerlinClipJob()
            {
                min = min,
                max = max,

                strengthOne = set.strengthOne,
                strengthTwo = set.strengthTwo,
                strengthThree = set.strengthThree,

                frequencyOne = set.frequencyOne,
                frequencyTwo = set.frequencyTwo,
                frequencyThree = set.frequencyThree,

                position = world.position,
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
        }


#if UNITY_EDITOR
        public override bool Draw()
        {
            SerializedObject serializedObject = new SerializedObject(this);
            SerializedProperty min = serializedObject.FindProperty("min");
            SerializedProperty max = serializedObject.FindProperty("max");
            SerializedProperty set = serializedObject.FindProperty("set");

            float minValue = min.floatValue;
            float maxValue = max.floatValue;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.MinMaxSlider("Threshold", ref minValue, ref maxValue, 0.0f, 1.0f);
            EditorGUILayout.PropertyField(set);
            EditorGUILayout.Space();
            PerlinSet setObject = set.objectReferenceValue as PerlinSet;
            if (setObject != null)
            {
                EditorGUI.indentLevel++;
                setObject.Draw();
                EditorGUI.indentLevel--;
            }

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

        [BurstCompile]
        public struct PerlinClipJob : IJobParallelFor
        {
            public float min;
            public float max;

            public float strengthOne;
            public float frequencyOne;

            public float strengthTwo;
            public float frequencyTwo;

            public float strengthThree;
            public float frequencyThree;

            public Vector3 position;
            public NativeArray<GenusState> states;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];

                float octaveOne = math.unlerp(-1, 1, noise.cnoise(position + state.instance.position * frequencyOne)) * strengthOne;
                float octaveTwo = math.unlerp(-1, 1, noise.cnoise(position + state.instance.position * frequencyTwo)) * strengthTwo;
                float octaveThree = math.unlerp(-1, 1, noise.cnoise(position + state.instance.position * frequencyThree)) * strengthThree;
                float total = Mathf.Clamp01(octaveOne + octaveTwo + octaveThree);

                state.valid = (state.valid && total >= min && total <= max);

                states[index] = state;
            }
        }
    }
}