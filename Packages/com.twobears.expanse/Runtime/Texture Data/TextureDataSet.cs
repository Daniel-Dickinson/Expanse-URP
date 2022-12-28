using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TwoBears.Expanse
{
    [CreateAssetMenu(menuName = "Expanse/Data/TextureSet")]
    public class TextureDataSet : ScriptableObject
    {
        public Texture2D height;
        public Texture2D normal;
        public Texture2D contour;
        public Texture2D color;

        public int resolutionX = 512;
        public int resolutionY = 512;
        public float maxDistance = 100;

        public Vector3 bounds = Vector3.one;
        public Vector3 offset = Vector3.zero;
        public SampleDirection direction = SampleDirection.Down;

        public float maxContour = 50;
        public int contourSamples = 40;

        public float contourOne = 0.5f;
        public float contourTwo = 1.5f;

#if UNITY_EDITOR
        public void Initialize(bool generateHeight, bool generateNormal, bool generateContour, bool generateColor)
        {
            //Cleanup
            if (height != null && !generateHeight)
            {
                AssetDatabase.RemoveObjectFromAsset(height);
                DestroyImmediate(height, true);
            }
            if (normal != null && !generateNormal)
            {
                AssetDatabase.RemoveObjectFromAsset(normal);
                DestroyImmediate(normal, true);
            }
            if (contour != null && !generateContour)
            {
                AssetDatabase.RemoveObjectFromAsset(contour);
                DestroyImmediate(contour, true);
            }
            if (color != null && !generateContour)
            {
                AssetDatabase.RemoveObjectFromAsset(color);
                DestroyImmediate(color, true);
            }

            //Initialize
            if (generateHeight && (height == null || height.width != resolutionX || height.height != resolutionY))
            {
                //Clear previous asset
                if (height != null)
                {
                    AssetDatabase.RemoveObjectFromAsset(height);
                    DestroyImmediate(height, true);
                }

                //Create textures
                height = new Texture2D(resolutionX, resolutionY, TextureFormat.R16, false, true);
                height.wrapMode = TextureWrapMode.Clamp;
                height.name = "Height Data";

                //Add to asset
                AssetDatabase.AddObjectToAsset(height, this);
            }

            if (generateNormal && (normal == null || normal.width != resolutionX || normal.height != resolutionY))
            {
                //Clear previous asset
                if (normal != null)
                {
                    AssetDatabase.RemoveObjectFromAsset(normal);
                    DestroyImmediate(normal, true);
                }

                //Create textures
                normal = new Texture2D(resolutionX, resolutionY, TextureFormat.RGB24, false, true);
                normal.wrapMode = TextureWrapMode.Clamp;
                normal.name = "Normal Data";

                //Add to asset
                AssetDatabase.AddObjectToAsset(normal, this);
            }

            if (generateContour && (contour == null || contour.width != resolutionX || contour.height != resolutionY))
            {
                //Clear previous asset
                if (contour != null)
                {
                    AssetDatabase.RemoveObjectFromAsset(contour);
                    DestroyImmediate(contour, true);
                }

                //Create textures
                contour = new Texture2D(resolutionX, resolutionY, TextureFormat.RGBA32, false, true);
                contour.wrapMode = TextureWrapMode.Clamp;
                contour.name = "Contour Data";

                //Add to asset
                AssetDatabase.AddObjectToAsset(contour, this);
            }

            if (generateColor && (color == null || color.width != resolutionX || color.height != resolutionY))
            {
                //Clear previous asset
                if (color != null)
                {
                    AssetDatabase.RemoveObjectFromAsset(color);
                    DestroyImmediate(color, true);
                }

                //Create textures
                color = new Texture2D(resolutionX, resolutionY, TextureFormat.RGB24, false, true);
                color.wrapMode = TextureWrapMode.Clamp;
                color.name = "Color Data";

                //Add to asset
                AssetDatabase.AddObjectToAsset(color, this);
            }
        }
#endif
    }

    public enum SampleDirection { Down, Up };
}