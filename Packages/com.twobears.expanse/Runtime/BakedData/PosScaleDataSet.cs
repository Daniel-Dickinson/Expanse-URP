using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace TwoBears.Expanse
{
    [CreateAssetMenu(menuName = "Expanse/Data/PosScale")]
    public class PosScaleDataSet : InstanceDataSet
    {
        [SerializeField]
        private PosScaleData[] data;

        public override GenusData[] LoadData()
        {
            GenusData[] outData = new GenusData[data.Length];

            for (int i = 0; i < outData.Length; i++)
            {
                outData[i] = data[i].ConvertToGenus();
            }

            return outData;
        }
        protected override void WriteData(GenusData[] data)
        {
            this.data = new PosScaleData[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                this.data[i] = new PosScaleData(data[i]);
            }
        }


        [System.Serializable]
        private struct PosScaleData
        {
            public Genus genus;
            public Vector3[] positions;
            public Vector3[] scales;
            public Vector3 offset;
            public Vector3 bounds;

            public PosScaleData(GenusData input)
            {
                genus = input.genus;
                offset = input.offset;
                bounds = input.bounds;

                positions = new Vector3[input.data.Length];
                scales = new Vector3[input.data.Length];

                for (int i = 0; i < input.data.Length; i++)
                {
                    positions[i] = input.data[i].position;
                    scales[i] = input.data[i].scale;
                }
            }

            public GenusData ConvertToGenus()
            {
                NativeList<GenusInstance> instanceData = new NativeList<GenusInstance>(positions.Length, Allocator.Persistent);

                for (int i = 0; i < instanceData.Length; i++) instanceData.Add(new GenusInstance(positions[i], scales[i]));

                return new GenusData(genus, null, null, instanceData, offset, bounds);
            }
        }
    }
}