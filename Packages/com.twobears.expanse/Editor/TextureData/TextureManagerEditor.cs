using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TwoBears.Expanse
{
    [CustomEditor(typeof(TextureManager))]
    public class TextureManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space();

            //Main Button
            if (GUILayout.Button("Generate Child Texture Sets"))
            {
                (serializedObject.targetObject as TextureManager).GenerateChildTextures();
            }

            EditorGUILayout.Space();
        }
    }
}