using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TwoBears.Expanse
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(AreaRenderer))]
    public class AreaRendererEditor : Editor
    {
        private SerializedProperty genera;

        private SerializedProperty standardMaterials;
        private SerializedProperty billboardMaterials;

        private SerializedProperty textureSet;

        private SerializedProperty offset;
        private SerializedProperty bounds;

        private SerializedProperty countX;
        private SerializedProperty countY;

        private SerializedProperty seed;
        private SerializedProperty ruleSet;

        private SerializedProperty state;
        private SerializedProperty dataSet;

        private SerializedProperty type;
        private SerializedProperty pool;
        private SerializedProperty priority;
        private SerializedProperty drawShadows;
        private SerializedProperty liveTransform;
        private SerializedProperty extraData;
        private SerializedProperty preDraw;

        private SerializedProperty debugLOD;
        private SerializedProperty debugBounds;
        private SerializedProperty debugTimes;
        private SerializedProperty debugCamera;

        private bool autoUpdate;

        private bool materialFoldout;
        private bool areaFoldout;
        private bool generationFoldout;
        private bool rulesetFoldout;
        private bool serializationFoldout;
        private bool computeFoldout;
        private bool debugFoldout;

        //Scrollview
        public GUIStyle ScrollView
        {
            get
            {
                if (scrollView == null)
                {
                    scrollView = new GUIStyle();
                    GUIStyleState normal = new GUIStyleState();

                    Texture2D backgroundTex = new Texture2D(1, 1);
                    backgroundTex.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.15f));
                    backgroundTex.Apply();

                    normal.background = backgroundTex;

                    scrollView.normal = normal;
                    scrollView.active = normal;
                    scrollView.hover = normal;
                }
                return scrollView;
            }
        }
        private GUIStyle scrollView;

        //Bold Foldout
        public GUIStyle BoldFoldout
        {
            get
            {
                if (boldFoldout == null)
                {
                    boldFoldout = new GUIStyle(EditorStyles.foldout);
                    boldFoldout.fontStyle = FontStyle.Bold;
                }
                return boldFoldout;
            }
        }
        private GUIStyle boldFoldout;

        public void OnEnable()
        {
            //Load editor variables
            autoUpdate = EditorPrefs.GetBool("autoUpdate", autoUpdate);
            materialFoldout = EditorPrefs.GetBool("materialFoldout", materialFoldout);
            areaFoldout = EditorPrefs.GetBool("areaFoldout", areaFoldout);
            generationFoldout = EditorPrefs.GetBool("generationFoldout", generationFoldout);
            rulesetFoldout = EditorPrefs.GetBool("rulesetFoldout", rulesetFoldout);
            serializationFoldout = EditorPrefs.GetBool("serializationFoldout", serializationFoldout);
            computeFoldout = EditorPrefs.GetBool("computeFoldout", computeFoldout);
            debugFoldout = EditorPrefs.GetBool("debugFoldout", debugFoldout);

            //Get properties
            genera = serializedObject.FindProperty("genera");

            standardMaterials = serializedObject.FindProperty("standardMaterials");
            billboardMaterials = serializedObject.FindProperty("billboardMaterials");

            textureSet = serializedObject.FindProperty("textureSet");

            offset = serializedObject.FindProperty("offset");
            bounds = serializedObject.FindProperty("bounds");

            countX = serializedObject.FindProperty("countX");
            countY = serializedObject.FindProperty("countY");

            seed = serializedObject.FindProperty("seed");
            ruleSet = serializedObject.FindProperty("ruleSet");

            state = serializedObject.FindProperty("state");
            dataSet = serializedObject.FindProperty("dataSet");

            type = serializedObject.FindProperty("type");
            pool = serializedObject.FindProperty("pool");
            priority = serializedObject.FindProperty("priority");
            drawShadows = serializedObject.FindProperty("drawShadows");
            liveTransform = serializedObject.FindProperty("liveTransform");
            extraData = serializedObject.FindProperty("extraData");
            preDraw = serializedObject.FindProperty("preDraw");

            debugLOD = serializedObject.FindProperty("debugLOD");
            debugBounds = serializedObject.FindProperty("debugBounds");
            debugTimes = serializedObject.FindProperty("debugTimes");
            debugCamera = serializedObject.FindProperty("debugCam");
        }
        public void OnDisable()
        {
            //Save editor variables
            EditorPrefs.SetBool("autoUpdate", autoUpdate);
            EditorPrefs.SetBool("areaFoldout", areaFoldout);
            EditorPrefs.SetBool("materialFoldout", materialFoldout);
            EditorPrefs.SetBool("generationFoldout", generationFoldout);
            EditorPrefs.SetBool("rulesetFoldout", rulesetFoldout);
            EditorPrefs.SetBool("serializationFoldout", serializationFoldout);
            EditorPrefs.SetBool("computeFoldout", computeFoldout);
            EditorPrefs.SetBool("debugFoldout", debugFoldout);
        }

        public override void OnInspectorGUI()
        {
            //Update
            serializedObject.Update();

            //Properties
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(genera);
            EditorGUILayout.Space();

            bool update = false;

            if (MaterialOverrides()) update = true;
            if (Area()) update = true;
            if (Generation()) update = true;
            if (RuleSet()) update = true;
            if (Performance()) update = true;

            Debug();
            Serialization();

            //Apply
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                //Update on auto-update
                if (update && autoUpdate)
                {
                    for (int i = 0; i < serializedObject.targetObjects.Length; i++)
                    {
                        AreaRenderer rend = serializedObject.targetObjects[i] as AreaRenderer;
                        if (rend != null) rend.UpdateGenus();
                    }
                }
            }
        }

        private bool MaterialOverrides()
        {
            materialFoldout = EditorGUILayout.Foldout(materialFoldout, new GUIContent(" Materials"), true, BoldFoldout);
            using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
            {
                //Resize arrays to match genera
                if (standardMaterials.arraySize != genera.arraySize) standardMaterials.arraySize = genera.arraySize;
                if (billboardMaterials.arraySize != genera.arraySize) billboardMaterials.arraySize = genera.arraySize;

                if (materialFoldout)
                {
                    EditorGUI.indentLevel++;

                    //Standard
                    EditorGUILayout.LabelField("Standard Overrides", EditorStyles.boldLabel);
                    for (int s = 0; s < standardMaterials.arraySize; s++)
                    {
                        EditorGUILayout.PropertyField(standardMaterials.GetArrayElementAtIndex(s), new GUIContent(genera.GetArrayElementAtIndex(s).objectReferenceValue.name));
                    }
                    

                    EditorGUILayout.Space();

                    //Billboard
                    EditorGUILayout.LabelField("Billboard Overrides", EditorStyles.boldLabel);
                    for (int b = 0; b < billboardMaterials.arraySize; b++)
                    {
                        EditorGUILayout.PropertyField(billboardMaterials.GetArrayElementAtIndex(b), new GUIContent(genera.GetArrayElementAtIndex(b).objectReferenceValue.name));
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();
                return scope.changed;
            }
        }
        private bool Area()
        {
            areaFoldout = EditorGUILayout.Foldout(areaFoldout, new GUIContent(" Area"), true, BoldFoldout);
            using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
            {
                if (areaFoldout)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(offset);
                    EditorGUILayout.PropertyField(bounds);
                    EditorGUILayout.PropertyField(textureSet);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();
                return scope.changed;
            }
        }
        private bool Generation()
        {
            generationFoldout = EditorGUILayout.Foldout(generationFoldout, new GUIContent(" Generation"), true, BoldFoldout);
            using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
            {
                if (generationFoldout)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(countX);
                    EditorGUILayout.PropertyField(countY);
                    EditorGUILayout.PropertyField(seed);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();
                return scope.changed;
            }

        }
        private bool RuleSet()
        {
            bool update = false;

            //Ruleset header
            using (new EditorGUILayout.HorizontalScope())
            {
                rulesetFoldout = EditorGUILayout.Foldout(rulesetFoldout, new GUIContent(" Rule Set"), true, BoldFoldout);
                using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.PropertyField(ruleSet, new GUIContent(""));
                    if (scope.changed) update = true;
                }

            }

            //Ruleset foldout
            if (rulesetFoldout && ruleSet.objectReferenceValue != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.Space();
                EditorGUI.indentLevel++;
                if (RuleSetEditor.DrawRuleSet(new SerializedObject(ruleSet.objectReferenceValue))) update = true;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();
            return update;
        }
        private bool Performance()
        {
            computeFoldout = EditorGUILayout.Foldout(computeFoldout, new GUIContent(" Performance"), true, BoldFoldout);
            using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
            {
                if (computeFoldout)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(type, new GUIContent("Computation"));
                    EditorGUILayout.PropertyField(pool);
                    EditorGUILayout.IntSlider(priority, 0, 5);
                    EditorGUILayout.PropertyField(drawShadows);
                    EditorGUILayout.PropertyField(liveTransform);
                    
                    EditorGUILayout.Space();
                    if (!serializedObject.isEditingMultipleObjects)
                    {
                        AreaRenderer rend = serializedObject.targetObject as AreaRenderer;
                        if (rend != null)
                        {
                            EditorGUILayout.LabelField("Vertex Count : " + rend.VertexCount.ToString("#,##0"));
                            EditorGUILayout.LabelField("Triangle Count : " + rend.TriangleCount.ToString("#,##0"));
                            EditorGUILayout.LabelField("Instance Count : " + rend.InstanceCount.ToString("#,##0"));
                            EditorGUILayout.Space();
                            EditorGUILayout.Space();
                        }
                    }
                    EditorGUILayout.PropertyField(extraData);
                    EditorGUILayout.PropertyField(preDraw);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();
                return scope.changed;
            }
        }
        private void Serialization()
        {
            serializationFoldout = EditorGUILayout.Foldout(serializationFoldout, new GUIContent(" Serialization"), true, BoldFoldout);
            if (serializationFoldout)
            {
                EditorGUI.indentLevel++;
                autoUpdate = EditorGUILayout.Toggle(new GUIContent("Auto Update"), autoUpdate);
                EditorGUILayout.PropertyField(state);

                if (state.enumValueIndex > 0)
                {
                    EditorGUILayout.PropertyField(dataSet);
                    EditorGUILayout.Space();

                    if (dataSet.objectReferenceValue != null && GUILayout.Button("Generate"))
                    {
                        for (int i = 0; i < targets.Length; i++)
                        {
                            AreaRenderer rend = targets[i] as AreaRenderer;
                            if (rend != null) rend.BakeData();
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();
        }
        private void Debug()
        {
            debugFoldout = EditorGUILayout.Foldout(debugFoldout, new GUIContent(" Debug"), true, BoldFoldout);
            if (debugFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(debugLOD);
                EditorGUILayout.PropertyField(debugBounds);
                EditorGUILayout.PropertyField(debugTimes);
                EditorGUILayout.PropertyField(debugCamera);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();
        }
    }
}