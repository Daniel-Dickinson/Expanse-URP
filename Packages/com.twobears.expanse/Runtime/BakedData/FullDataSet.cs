using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwoBears.Expanse
{
    [CreateAssetMenu(menuName = "Expanse/Data/Full")]
    public class FullDataSet : InstanceDataSet
    {
        [SerializeField]
        private GenusData[] data;

        public override GenusData[] LoadData()
        {
            return data;
        }
        protected override void WriteData(GenusData[] data)
        {
            this.data = data;
        }
    }
}