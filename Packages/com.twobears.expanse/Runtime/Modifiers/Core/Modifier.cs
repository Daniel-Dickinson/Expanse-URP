using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TwoBears.Expanse
{
    public abstract class Modifier : ScriptableObject
    {
        public bool active = true;
        protected int batchSize = 50000;

        //Standard
        public void ApplyModifier(WorldState world, NativeArray<GenusState> states)
        {
            if (active) Apply(world, states);
        }
        protected abstract void Apply(WorldState world, NativeArray<GenusState> states);

        //Burst
        public abstract JobHandle ScheduleModifier(WorldState world, NativeArray<GenusState> states, JobHandle previous);
        public abstract void CompleteModifier();

        //Async
        public IEnumerator ApplyModifierAsync(MonoBehaviour host, WorldState world, NativeArray<GenusState> states)
        {
            if (active) yield return host.StartCoroutine(ApplyAsync(world, states));
        }
        protected abstract IEnumerator ApplyAsync(WorldState world, NativeArray<GenusState> states);

#if UNITY_EDITOR
        public abstract bool Draw();
#endif
    }

    public struct WorldState
    {
        public int seed;
        public Vector3 position;
        public TextureDataSet textureSet;

        public WorldState(int seed, Vector3 position, TextureDataSet textureSet)
        {
            this.seed = seed;
            this.position = position;
            this.textureSet = textureSet;
        }
    }
    public struct GenusState
    {
        public bool valid;
        public int distribution;
        public GenusInstance instance;

        public GenusState(GenusInstance instance)
        {
            valid = true;
            distribution = -1;
            this.instance = instance;
        }
    }
}