using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TwoBears.Expanse
{
    [CustomEditor(typeof(RuleSet))]
    public class RuleSetEditor : Editor
    {
        //Bold Foldout
        public static GUIStyle BoldFoldout
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
        private static GUIStyle boldFoldout;

        public override void OnInspectorGUI()
        {
            DrawRuleSet(serializedObject);
        }

        public static bool DrawRuleSet(SerializedObject ruleSet)
        {
            //Get Modifiers
            SerializedProperty modifiers = ruleSet.FindProperty("modifiers");

            //Track changes
            bool update = false;

            //Draw Modifiers
            for (int i = 0; i < modifiers.arraySize; i++)
            {
                //Get object
                Modifier modifier = modifiers.GetArrayElementAtIndex(i).objectReferenceValue as Modifier;
                if (DrawModifier(ruleSet, modifier, i)) update = true;
            }

            //Add Modifers
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+", GUILayout.Width(220), GUILayout.Height(18)))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Height Clip"), false, AddHeightClip, ruleSet);
                    menu.AddItem(new GUIContent("Normal Clip"), false, AddNormalClip, ruleSet);
                    menu.AddItem(new GUIContent("Texture Clip"), false, AddTextureClip, ruleSet);
                    menu.AddItem(new GUIContent("Contour Clip"), false, AddContourClip, ruleSet);
                    menu.AddItem(new GUIContent("Perlin Clip"), false, AddPerlinClip, ruleSet);
                    menu.AddItem(new GUIContent("Random Offset"), false, AddRandomOffset, ruleSet);
                    menu.AddItem(new GUIContent("Random Scale"), false, AddRandomScale, ruleSet);
                    menu.AddItem(new GUIContent("Perlin Scale"), false, AddPerlinScale, ruleSet);
                    menu.AddItem(new GUIContent("Contour Scale"), false, AddContourScale, ruleSet);
                    menu.AddItem(new GUIContent("Contour Offset"), false, AddContourOffset, ruleSet);
                    menu.AddItem(new GUIContent("Texture Sample"), false, AddTextureSample, ruleSet);

                    menu.ShowAsContext();
                }
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.Space();
            return update;
        }
        public static bool DrawModifier(SerializedObject ruleSet, Modifier modifier, int index)
        {
            //Get foldout
            bool foldout = EditorPrefs.GetBool(modifier.name);

            //Draw header
            using (new EditorGUILayout.HorizontalScope())
            {
                //Draw foldout
                foldout = EditorGUILayout.Foldout(foldout, " " + modifier.name, BoldFoldout);
                EditorPrefs.SetBool(modifier.name, foldout);

                GUILayout.FlexibleSpace();

                modifier.active = EditorGUILayout.Toggle(modifier.active, GUILayout.Width(34));
                if (GUILayout.Button("-", GUILayout.Width(28), GUILayout.Height(15)))
                {
                    RemoveModifier(ruleSet, index);
                    return true;
                }
            }

            //Track changes
            bool update = false;

            //Draw foldout
            if (foldout)
            {
                using (EditorGUI.ChangeCheckScope scope = new EditorGUI.ChangeCheckScope())
                {
                    //Draw body
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Space();

                    modifier.Draw();

                    EditorGUI.indentLevel--;
                    if (scope.changed) update = true;
                }
            }
            EditorGUILayout.Space();

            //Return changes
            return update;
        }

        private static void AddModifier(RuleSet scriptableObject, Modifier modifier)
        {
            scriptableObject.Add(modifier);

            //Attach to scriptable object
            AssetDatabase.AddObjectToAsset(modifier, scriptableObject);

            //Save asset
            EditorUtility.SetDirty(scriptableObject);
            EditorUtility.SetDirty(modifier);

            AssetDatabase.SaveAssetIfDirty(scriptableObject);
            AssetDatabase.SaveAssetIfDirty(modifier);
        }
        private static void RemoveModifier(SerializedObject ruleSet, int index)
        {
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                if (scriptableObject.modifiers.Length > index)
                {
                    //Get modifier
                    Modifier modifier = scriptableObject.modifiers[index];

                    //Remove from ruleset
                    scriptableObject.RemoveAt(index);

                    //Destroy
                    AssetDatabase.RemoveObjectFromAsset(modifier);
                    DestroyImmediate(modifier, true);
                }
            }
        }

        private static void AddRandomOffset(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;

            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                RandomOffset modifier = CreateInstance<RandomOffset>();
                modifier.name = "Random Offset";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddRandomScale(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                RandomScale modifier = CreateInstance<RandomScale>();
                modifier.name = "Random Scale";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddPerlinScale(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                PerlinScale modifier = CreateInstance<PerlinScale>();
                modifier.name = "Perlin Scale";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddPerlinClip(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                PerlinClip modifier = CreateInstance<PerlinClip>();
                modifier.name = "Perlin Clip";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddTextureSample(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                TextureSample modifier = CreateInstance<TextureSample>();
                modifier.name = "Texture Sample";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddHeightClip(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                HeightClip modifier = CreateInstance<HeightClip>();
                modifier.name = "Height Clip";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddTextureClip(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                TextureClip modifier = CreateInstance<TextureClip>();
                modifier.name = "Texture Clip";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddNormalClip(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                NormalClip modifier = CreateInstance<NormalClip>();
                modifier.name = "Normal Clip";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddContourClip(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                ContourClip modifier = CreateInstance<ContourClip>();
                modifier.name = "Contour Clip";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddContourScale(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                ContourScale modifier = CreateInstance<ContourScale>();
                modifier.name = "Contour Scale";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
        private static void AddContourOffset(object input)
        {
            SerializedObject ruleSet = input as SerializedObject;
            for (int i = 0; i < ruleSet.targetObjects.Length; i++)
            {
                //Get reference to scripable object
                RuleSet scriptableObject = ruleSet.targetObjects[i] as RuleSet;

                //Create new modifier instance
                ContourOffset modifier = CreateInstance<ContourOffset>();
                modifier.name = "Contour Offset";

                //Add modifier to scriptable object
                AddModifier(scriptableObject, modifier);
            }
        }
    }
}