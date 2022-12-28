using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TwoBears.Expanse
{
    public class RandomScale : Modifier
    {
        public float min = 0.5f;
        public float max = 1.5f;

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
            //Setup job
            RandomScaleJob job = new RandomScaleJob()
            {
                min = min,
                max = max,
                random = new Random((uint)world.seed),
                states = states
            };

            //Schedule & complete
            jobHandle = job.Schedule(states.Length, batchSize);
            jobHandle.Complete();
        }
        protected override IEnumerator ApplyAsync(WorldState world, NativeArray<GenusState> states)
        {
            //Setup job
            RandomScaleJob job = new RandomScaleJob()
            {
                min = min,
                max = max,
                random = new Random((uint)world.seed),
                states = states
            };

            //Schedule
            jobHandle = job.Schedule(states.Length, batchSize);

            //Wait for setup to complete
            while (!jobHandle.IsCompleted) yield return new UnityEngine.WaitForEndOfFrame();
            jobHandle.Complete();
        }

        //Burst
        public override JobHandle ScheduleModifier(WorldState world, NativeArray<GenusState> states, JobHandle previous)
        {
            //Setup job
            RandomScaleJob job = new RandomScaleJob()
            {
                min = min,
                max = max,
                random = new Random((uint)world.seed),
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

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(min);
            EditorGUILayout.PropertyField(max);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                return true;
            }
            else return false;
        }
#endif

        [BurstCompile]
        public struct RandomScaleJob : IJobParallelFor
        {
            public float min;
            public float max;
            public Random random;
            public NativeArray<GenusState> states;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];
                state.instance.scale *= random.NextFloat(min, max);
                states[index] = state;
            }
        }
    }
}