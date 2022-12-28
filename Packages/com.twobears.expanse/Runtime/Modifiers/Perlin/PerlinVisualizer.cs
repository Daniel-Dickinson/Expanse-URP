using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace TwoBears.Expanse
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PerlinVisualizer : MonoBehaviour
    {
        public PerlinSet set;

        public int resolutionX = 1024;
        public int resolutionY = 1024;

        private MeshRenderer rend;
        private Texture2D perlinTexture;

        //Mono
        private void OnEnable()
        {
            rend = GetComponent<MeshRenderer>();

            GenerateTexture();
            ApplyToMaterial();
        }

        public void GenerateTexture()
        {
            //Set required
            if (set == null) return;

            //Generate texture
            if (perlinTexture == null || perlinTexture.width != resolutionX || perlinTexture.height != resolutionY)
            {
                perlinTexture = new Texture2D(resolutionX, resolutionY, TextureFormat.RGBA32, false);
            }

            //Grab colors
            NativeArray<Color32> textureData = perlinTexture.GetRawTextureData<Color32>();

            //Setup job
            PerlinSet.PerlinToTexture job = new PerlinSet.PerlinToTexture()
            {
                min = 0,

                strengthOne = set.strengthOne,
                strengthTwo = set.strengthTwo,
                strengthThree = set.strengthThree,

                frequencyOne = set.frequencyOne,
                frequencyTwo = set.frequencyTwo,
                frequencyThree = set.frequencyThree,

                resolutionX = resolutionX,
                resolutionY = resolutionY,

                worldPosition = transform.position,
                worldRotation = transform.rotation,
                worldScale = transform.lossyScale / 10, //Default plane is 10 x 10

                textureData = textureData
            };

            //Execute job
            job.Schedule().Complete();

            //Apply to texture
            perlinTexture.Apply();
        }
        public void ApplyToMaterial()
        {
            rend.sharedMaterial.SetTexture("_BaseMap", perlinTexture);
        }
    }
}