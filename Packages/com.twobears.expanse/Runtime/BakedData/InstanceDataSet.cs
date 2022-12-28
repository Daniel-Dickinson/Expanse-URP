using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TwoBears.Expanse
{
    public abstract class InstanceDataSet : ScriptableObject
    {
        public abstract GenusData[] LoadData();
#if UNITY_EDITOR
        public void SaveData(GenusData[] data)
        {
            WriteData(data);

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }
#endif

        protected abstract void WriteData(GenusData[] data);
    }
}