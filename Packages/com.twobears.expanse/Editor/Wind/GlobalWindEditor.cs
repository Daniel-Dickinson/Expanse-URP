using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(GlobalWind))]
public class GlobalWindEditor : Editor
{
    SerializedProperty direction;
    SerializedProperty density;
    SerializedProperty speed;

    public void OnEnable()
    {
        direction = serializedObject.FindProperty("direction");
        density = serializedObject.FindProperty("density");
        speed = serializedObject.FindProperty("speed");
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(direction);
        EditorGUILayout.PropertyField(density);
        EditorGUILayout.PropertyField(speed);
        EditorGUILayout.Space();

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            foreach (GlobalWind wind in serializedObject.targetObjects)
            {
                if (wind != null) wind.UpdateWind();
            }
        }
    }
}
