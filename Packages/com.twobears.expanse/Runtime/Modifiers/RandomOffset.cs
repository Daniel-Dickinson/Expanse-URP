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
    public class RandomOffset : Modifier
    {
        public Vector3 range = Vector3.zero;

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
            RandomOffsetJob job = new RandomOffsetJob()
            {
                range = range,
                random = new Unity.Mathematics.Random((uint)world.seed),
                states = states
            };

            //Schedule & complete
            jobHandle = job.Schedule(states.Length, batchSize);
            jobHandle.Complete();
        }
        protected override IEnumerator ApplyAsync(WorldState world, NativeArray<GenusState> states)
        {
            //Setup job
            RandomOffsetJob job = new RandomOffsetJob()
            {
                range = range,
                random = new Unity.Mathematics.Random((uint)world.seed),
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
            RandomOffsetJob job = new RandomOffsetJob()
            {
                range = range,
                random = new Unity.Mathematics.Random((uint)world.seed),
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
            SerializedProperty range = serializedObject.FindProperty("range");

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(range);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                return true;
            }
            else return false;
        }
#endif

        [BurstCompile]
        public struct RandomOffsetJob : IJobParallelFor
        {
            public float3 range;
            public Unity.Mathematics.Random random;
            public NativeArray<GenusState> states;

            void IJobParallelFor.Execute(int index)
            {
                GenusState state = states[index];
                state.instance.position += (Vector3)random.NextFloat3(-range, range);
                states[index] = state;
            }
        }
    }
}