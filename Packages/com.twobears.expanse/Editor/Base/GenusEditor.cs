using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditor(typeof(Genus))]
public class GenusEditor : Editor
{
    SerializedProperty lod0;
    SerializedProperty lod1;
    SerializedProperty lod2;
    SerializedProperty baseMaterial;

    SerializedProperty billboard;
    SerializedProperty billboardMaterial;

    SerializedProperty boundsCenter;
    SerializedProperty boundsExtents;

    SerializedProperty optDistance;
    SerializedProperty lod1Distance;
    SerializedProperty lod2Distance;
    SerializedProperty billboardDistance;
    SerializedProperty cullDistance;

    public void OnEnable()
    {
        lod0 = serializedObject.FindProperty("lod0");
        lod1 = serializedObject.FindProperty("lod1");
        lod2 = serializedObject.FindProperty("lod2");
        baseMaterial = serializedObject.FindProperty("baseMaterial");

        billboard = serializedObject.FindProperty("billboard");
        billboardMaterial = serializedObject.FindProperty("billboardMaterial");

        boundsCenter = serializedObject.FindProperty("boundsCenter");
        boundsExtents = serializedObject.FindProperty("boundsExtents");

        optDistance = serializedObject.FindProperty("optDistance");
        lod1Distance = serializedObject.FindProperty("lod1Distance");
        lod2Distance = serializedObject.FindProperty("lod2Distance");
        billboardDistance = serializedObject.FindProperty("billboardDistance");
        cullDistance = serializedObject.FindProperty("cullDistance");
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Core", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lod0, new GUIContent("Mesh 0"));
        EditorGUILayout.PropertyField(lod1, new GUIContent("Mesh 1"));
        EditorGUILayout.PropertyField(lod2, new GUIContent("Mesh 2"));
        EditorGUILayout.PropertyField(baseMaterial, new GUIContent("Material"));
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Billboard", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(billboard, new GUIContent("Mesh"));
        EditorGUILayout.PropertyField(billboardMaterial, new GUIContent("Material"));
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Bounds", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(boundsCenter, new GUIContent("Center"));
        EditorGUILayout.PropertyField(boundsExtents, new GUIContent("Extents"));
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Distances", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(optDistance, new GUIContent("Opt"));
        EditorGUILayout.PropertyField(lod1Distance, new GUIContent("LOD 1"));
        EditorGUILayout.PropertyField(lod2Distance, new GUIContent("LOD 2"));
        if (billboard.objectReferenceValue != null && billboardMaterial.objectReferenceValue != null)
        {
            EditorGUILayout.PropertyField(billboardDistance, new GUIContent("Billboard"));
        }
        EditorGUILayout.PropertyField(cullDistance, new GUIContent("Cull"));

        EditorGUILayout.Space();

        EditorGUILayout.Space();
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }
    }
}
