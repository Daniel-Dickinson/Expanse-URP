using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TwoBears.Expanse
{
    [ExecuteAlways]
    public class TextureManager : MonoBehaviour
    {
#if UNITY_EDITOR
        public void GenerateChildTextures()
        {
            //Get all child generators
            TextureGenerator[] generators = GetComponentsInChildren<TextureGenerator>();

            float percentage = 1.0f / generators.Length;

            //Generate textures for each
            for (int i = 0; i < generators.Length; i++)
            {
                TextureGenerator generator = generators[i];

                float startPercentage = percentage * i;
                float endPercentage = percentage * (i + 1);

                generator.Generate(startPercentage, endPercentage);
            }

            //Clear progress bar
            EditorUtility.ClearProgressBar();
        }
#endif
    }
}