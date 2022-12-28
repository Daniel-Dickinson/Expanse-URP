using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TwoBears.Expanse
{
    [CustomEditor(typeof(TextureGenerator))]
    public class TextureGeneratorEditor : Editor
    {
        SerializedProperty bakeHeight;
        SerializedProperty bakeNormal;
        SerializedProperty bakeContour;
        SerializedProperty bakeColor;

        SerializedProperty data;

        SerializedProperty clipInside;
        SerializedProperty clipDistance;
        SerializedProperty clipDirections;

        
        SerializedProperty layers;
        SerializedProperty ignore;
        SerializedProperty mesh;

        public void OnEnable()
        {
            bakeHeight = serializedObject.FindProperty("bakeHeight");
            bakeNormal = serializedObject.FindProperty("bakeNormal");
            bakeContour = serializedObject.FindProperty("bakeContour");
            bakeColor = serializedObject.FindProperty("bakeColor");

            data = serializedObject.FindProperty("data");

            clipInside = serializedObject.FindProperty("clipInside");
            clipDistance = serializedObject.FindProperty("clipDistance");
            clipDirections = serializedObject.FindProperty("clipDirections");

            layers = serializedObject.FindProperty("layers");
            ignore = serializedObject.FindProperty("ignore");
            mesh = serializedObject.FindProperty("mesh");
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Bake Types", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(bakeHeight);
            EditorGUILayout.PropertyField(bakeNormal);
            EditorGUILayout.PropertyField(bakeContour);
            EditorGUILayout.PropertyField(bakeColor);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Bake Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(data);

            EditorGUILayout.Space();

            if (data.objectReferenceValue != null)
            {
                SerializedObject subObject = new SerializedObject(data.objectReferenceValue);
                using (EditorGUI.ChangeCheckScope subcheck = new EditorGUI.ChangeCheckScope())
                {
                    
                    SerializedProperty resolutionX = subObject.FindProperty("resolutionX");
                    SerializedProperty resolutionY = subObject.FindProperty("resolutionY");

                    SerializedProperty maxDistance = subObject.FindProperty("maxDistance");

                    SerializedProperty bounds = subObject.FindProperty("bounds");
                    SerializedProperty offset = subObject.FindProperty("offset");
                    SerializedProperty direction = subObject.FindProperty("direction");

                    SerializedProperty maxContour = subObject.FindProperty("maxContour");
                    SerializedProperty contourSamples = subObject.FindProperty("contourSamples");

                    SerializedProperty contourOne = subObject.FindProperty("contourOne");
                    SerializedProperty contourTwo = subObject.FindProperty("contourTwo");
                    

                    EditorGUILayout.LabelField("Data Options", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(resolutionX);
                    EditorGUILayout.PropertyField(resolutionY);

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(maxDistance);

                    EditorGUILayout.Space();

                    EditorGUILayout.PropertyField(bounds);
                    EditorGUILayout.PropertyField(offset);
                    EditorGUILayout.PropertyField(direction);

                    if (bakeContour.boolValue)
                    {
                        EditorGUILayout.Space();

                        EditorGUILayout.LabelField("Contour Options", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(maxContour);
                        EditorGUILayout.IntSlider(contourSamples, 10, 100);

                        EditorGUILayout.Space();

                        EditorGUILayout.PropertyField(contourOne);
                        EditorGUILayout.PropertyField(contourTwo);
                    }


                    if (subcheck.changed)
                    {
                        subObject.ApplyModifiedProperties();
                    }
                }
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Physics Options", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(layers);
            EditorGUILayout.PropertyField(ignore);
            EditorGUILayout.PropertyField(mesh);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Clip Options", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(clipInside);
            if (clipInside.boolValue)
            {
                EditorGUILayout.PropertyField(clipDistance);
                EditorGUILayout.PropertyField(clipDirections);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate"))
            {
                TextureGenerator component = serializedObject.targetObject as TextureGenerator;
                component.Generate();
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}