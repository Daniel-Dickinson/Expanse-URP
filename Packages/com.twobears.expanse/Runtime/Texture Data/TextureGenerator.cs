using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TwoBears.Expanse
{
    public class TextureGenerator : MonoBehaviour
    {
        public bool bakeHeight;
        public bool bakeNormal;
        public bool bakeContour;
        public bool bakeColor;

        public TextureDataSet data;

        public LayerMask layers;
        public LayerMask ignore;
        public MeshFilter mesh;

        public bool clipInside = false;
        public float clipDistance = 100;
        public int clipDirections = 32;

        private Color[] height;
        private Color[] normal;
        private Color[] contour;
        private Color[] color;

#if UNITY_EDITOR
        public void Generate()
        {
            //Generate
            Generate(0, 1);

            //Clear progress bar
            EditorUtility.ClearProgressBar();
        }
        public void Generate(float startPercentage, float endPercentage)
        {
            //Initialize textures
            data.Initialize(bakeHeight, bakeNormal, bakeContour, bakeColor);

            //Sample data
            if (mesh == null) SampleData(startPercentage, endPercentage);
            else SampleData(mesh.sharedMesh, startPercentage, endPercentage);
        }
        
        private void SampleData(float startPercentage = 0, float endPercentage = 1)
        {
            int dataSize = data.resolutionX * data.resolutionY;

            if (bakeHeight) height = new Color[dataSize];
            if (bakeNormal) normal = new Color[dataSize];
            if (bakeContour) contour = new Color[dataSize];
            if (bakeColor) color = new Color[dataSize];

            Vector2 boundsStep = new Vector2(data.bounds.x / data.resolutionX, data.bounds.z / data.resolutionY);
            Vector2 halfBounds = new Vector2(data.bounds.x * 0.5f, data.bounds.z * 0.5f);

            //Summon progress bar
            EditorUtility.DisplayProgressBar("Baking Textures", "Baking out requested textures for " + gameObject.name, startPercentage);

            for (int i = 0; i < dataSize; i++)
            {
                int x = i % data.resolutionX;
                int y = Mathf.FloorToInt(i / (float)data.resolutionX);

                //Calculate local position
                Vector3 localPosition = new Vector3((x * boundsStep.x) - halfBounds.x, 0, (y * boundsStep.y) - halfBounds.y);
                if (data.direction == SampleDirection.Up) localPosition.y -= data.maxDistance;

                //Calculate world position
                Vector3 worldPosition = transform.position + data.offset + localPosition;

                //Calculate raycast direction
                Vector3 rayDirection = data.direction == SampleDirection.Down ? Vector3.down : Vector3.up;

                //Raycast into environment
                if (Physics.Raycast(worldPosition, rayDirection, out RaycastHit hit, data.maxDistance, layers | ignore))
                {
                    bool clip = false;

                    //Check layer
                    int hitLayer = hit.collider.gameObject.layer;
                    if ((layers | (1 << hitLayer)) != layers) clip = true;

                    //Check inside clip
                    if (!clip && clipInside)
                    {
                        Vector3 position = worldPosition + (rayDirection * (hit.distance - 0.5f));
                        if (SampleClipPosition(position)) clip = true;
                    }

                    if (!clip)
                    {
                        if (bakeHeight)
                        {
                            height[i] = new Color(hit.distance / data.maxDistance, 0, 0, 0);
                        }
                        if (bakeNormal)
                        {
                            Vector3 unpacked = hit.normal;
                            normal[i] = new Color((unpacked.x / 2.0f) + 0.5f, (unpacked.y / 2.0f) + 0.5f, (unpacked.z / 2.0f) + 0.5f);
                        }
                        if (bakeContour)
                        {
                            contour[i] = SampleContour(worldPosition, rayDirection, hit.distance);
                        }
                        if (bakeColor)
                        {
                            MeshCollider collider = hit.collider as MeshCollider;
                            if (collider != null)
                            {
                                Mesh mesh = collider.sharedMesh;
                                if (mesh != null && mesh.colors != null && mesh.colors.Length > 0 && mesh.triangles != null && mesh.triangles.Length > 0)
                                {
                                    // Extract local space normals of the triangle we hit
                                    Color c0 = mesh.colors[mesh.triangles[hit.triangleIndex * 3 + 0]];
                                    Color c1 = mesh.colors[mesh.triangles[hit.triangleIndex * 3 + 1]];
                                    Color c2 = mesh.colors[mesh.triangles[hit.triangleIndex * 3 + 2]];

                                    // interpolate using the barycentric coordinate of the hitpoint
                                    Vector3 baryCenter = hit.barycentricCoordinate;

                                    // Use barycentric coordinate to interpolate normal
                                    color[i] = c0 * baryCenter.x + c1 * baryCenter.y + c2 * baryCenter.z;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (bakeHeight) height[i] = new Color(0, 0, 0, 0);
                        if (bakeNormal) normal[i] = new Color(0, 1, 0, 0);
                        if (bakeContour) contour[i] = new Color(0, 0, 0, 0);
                        if (bakeColor) color[i] = Color.black;
                    }
                }
                else
                {
                    if (bakeHeight) height[i] = new Color(0, 0, 0, 0);
                    if (bakeNormal) normal[i] = new Color(0, 1, 0, 0);
                    if (bakeContour) contour[i] = new Color(0, 0, 0, 0);
                    if (bakeColor) color[i] = Color.black;
                }

                //Display progress
                EditorUtility.DisplayProgressBar("Baking Textures", "Baking out requested textures for " + gameObject.name, Mathf.Lerp(startPercentage, endPercentage, (float)i / dataSize));
            }

            //Write textures
            if (bakeHeight)
            {
                data.height.SetPixels(height, 0);
                data.height.Apply();
            }
            if (bakeNormal)
            {
                data.normal.SetPixels(normal, 0);
                data.normal.Apply();
            }
            if (bakeContour)
            {
                data.contour.SetPixels(contour, 0);
                data.contour.Apply();
            }
            if (bakeColor)
            {
                data.color.SetPixels(color, 0);
                data.color.Apply();
            }
        }
        private void SampleData(Mesh mesh, float startPercentage = 0, float endPercentage = 1)
        {
            int dataSize = data.resolutionX * data.resolutionY;

            if (bakeHeight) height = new Color[dataSize];
            if (bakeNormal) normal = new Color[dataSize];
            if (bakeContour) contour = new Color[dataSize];
            if (bakeColor) color = new Color[dataSize];

            Vector2 boundsStep = new Vector2(data.bounds.x / data.resolutionX, data.bounds.z / data.resolutionY);
            Vector2 halfBounds = new Vector2(data.bounds.x * 0.5f, data.bounds.z * 0.5f);

            Color[] colors = mesh.colors;
            int[] triangles = mesh.triangles;

            //Summon progress bar
            EditorUtility.DisplayProgressBar("Baking Textures", "Baking out requested textures for " + gameObject.name, startPercentage);

            for (int i = 0; i < dataSize; i++)
            {
                int x = i % data.resolutionX;
                int y = Mathf.FloorToInt(i / (float)data.resolutionX);

                //Calculate local position
                Vector3 localPosition = new Vector3((x * boundsStep.x) - halfBounds.x, 0, (y * boundsStep.y) - halfBounds.y);
                if (data.direction == SampleDirection.Up) localPosition.y -= data.maxDistance;

                //Calculate world position
                Vector3 worldPosition = transform.position + data.offset + localPosition;

                //Calculate raycast direction
                Vector3 rayDirection = data.direction == SampleDirection.Down ? Vector3.down : Vector3.up;

                //Raycast into environment
                if (Physics.Raycast(worldPosition, rayDirection, out RaycastHit hit, data.maxDistance, layers | ignore))
                {
                    //Check layer
                    int hitLayer = hit.collider.gameObject.layer;
                    if ((layers | (1 << hitLayer)) == layers)
                    {
                        //Check inside clip
                        bool clip = false;
                        if (clipInside)
                        {
                            Vector3 position = hit.point + (hit.normal * 0.5f);
                            //Vector3 position = worldPosition + (rayDirection * (hit.distance - 0.5f));
                            if (Physics.CheckSphere(position, 0.025f, layers | ignore)) clip = true;
                        }

                        //Check collider
                        MeshCollider collider = hit.collider as MeshCollider;
                        if (!clip && collider != null && collider.sharedMesh == mesh)
                        {
                            if (bakeHeight)
                            {
                                height[i] = new Color(hit.distance / data.maxDistance, 0, 0, 0);
                            }
                            if (bakeNormal)
                            {
                                Vector3 unpacked = hit.normal;
                                normal[i] = new Color((unpacked.x / 2.0f) + 0.5f, (unpacked.y / 2.0f) + 0.5f, (unpacked.z / 2.0f) + 0.5f);
                            }
                            if (bakeContour)
                            {
                                contour[i] = SampleContour(worldPosition, rayDirection, hit.distance);
                            }
                            if (bakeColor)
                            {
                                // Extract local space normals of the triangle we hit
                                Color c0 = colors[triangles[hit.triangleIndex * 3 + 0]];
                                Color c1 = colors[triangles[hit.triangleIndex * 3 + 1]];
                                Color c2 = colors[triangles[hit.triangleIndex * 3 + 2]];

                                // interpolate using the barycentric coordinate of the hitpoint
                                Vector3 baryCenter = hit.barycentricCoordinate;

                                // Use barycentric coordinate to interpolate normal
                                color[i] = c0 * baryCenter.x + c1 * baryCenter.y + c2 * baryCenter.z;
                            }
                        }
                        else
                        {
                            if (bakeHeight) height[i] = new Color(0, 0, 0, 0);
                            if (bakeNormal) normal[i] = new Color(0, 1, 0, 0);
                            if (bakeContour) contour[i] = new Color(0, 0, 0, 0);
                            if (bakeColor) color[i] = Color.black;
                        }
                    }
                    else
                    {
                        if (bakeHeight) height[i] = new Color(0, 0, 0, 0);
                        if (bakeNormal) normal[i] = new Color(0, 1, 0, 0);
                        if (bakeContour) contour[i] = new Color(0, 0, 0, 0);
                        if (bakeColor) color[i] = Color.black;
                    }
                }
                else
                {
                    if (bakeHeight) height[i] = new Color(0, 0, 0, 0);
                    if (bakeNormal) normal[i] = new Color(0, 1, 0, 0);
                    if (bakeContour) contour[i] = new Color(0, 0, 0, 0);
                    if (bakeColor) color[i] = Color.black;
                }

                //Display progress
                EditorUtility.DisplayProgressBar("Baking Textures", "Baking out requested textures for " + gameObject.name, Mathf.Lerp(startPercentage, endPercentage, (float)i / dataSize));
            }

            //Write textures
            if (bakeColor)
            {
                data.height.SetPixels(height, 0);
                data.height.Apply();
            }
            if (bakeNormal)
            {
                data.normal.SetPixels(normal, 0);
                data.normal.Apply();
            }
            if (bakeContour)
            {
                data.contour.SetPixels(contour, 0);
                data.contour.Apply();
            }
            if (bakeColor)
            {
                data.color.SetPixels(color, 0);
                data.color.Apply();
            }
        }

        //Contour
        private Vector4 SampleContour(Vector3 origin, Vector3 direction, float height)
        {
            //Record
            float radius = 1;
            Vector2 heightDifference = Vector2.zero;
            Vector4 output = Vector4.zero;

            while (radius < data.maxContour && (heightDifference.y < data.contourTwo || heightDifference.x > -data.contourTwo))
            {
                heightDifference = SampleContourRadius(origin, direction, radius, height);

                //Below
                if (heightDifference.x > -data.contourOne) output.x = (radius / data.maxContour);
                if (heightDifference.x > -data.contourTwo) output.y = (radius / data.maxContour);

                //Above
                if (heightDifference.y < data.contourOne) output.z = (radius / data.maxContour);
                if (heightDifference.y < data.contourTwo) output.w = (radius / data.maxContour);
                
                radius += data.maxContour / data.contourSamples;
            }

            return output;
        }
        private Vector2 SampleContourRadius(Vector3 origin, Vector3 direction, float radius, float height)
        {
            //Functions return distance into cast, NOT distance in world space. All operations are flipped.
            float north = SampleContourPosition(origin + (new Vector3(0, 0, 1).normalized * radius), direction);
            float northEast = SampleContourPosition(origin + (new Vector3(1, 0, 1).normalized * radius), direction);
            float east = SampleContourPosition(origin + (new Vector3(1, 0, 0).normalized * radius), direction);
            float southEast = SampleContourPosition(origin + (new Vector3(1, 0, -1).normalized * radius), direction);
            float south = SampleContourPosition(origin + (new Vector3(0, 0, -1).normalized * radius), direction);
            float southWest = SampleContourPosition(origin + (new Vector3(-1, 0, -1).normalized * radius), direction);
            float west = SampleContourPosition(origin + (new Vector3(-1, 0, 0).normalized * radius), direction);
            float northWest = SampleContourPosition(origin + (new Vector3(-1, 0, 1).normalized * radius), direction);

            float lowest = Mathf.Min(new float[] { north, northEast, east, southEast, south, southWest, west, northWest });
            float highest = Mathf.Max(new float[] { north, northEast, east, southEast, south, southWest, west, northWest });

            float distanceBelow = height - highest; //Should be negative (below zero)
            float distanceAbove = height - lowest;  //Should be positive (Above zero)

            return new Vector2(distanceBelow, distanceAbove);
        }
        private float SampleContourPosition(Vector3 position, Vector3 direction)
        {
            if (Physics.Raycast(position, direction, out RaycastHit hit, data.maxDistance, layers | ignore)) return hit.distance;
            else return data.maxDistance;
        }
        
        private bool SampleClipPosition(Vector3 position)
        {
            //Clip pixel to black if true
            bool forward = SampleClipRadius(position, false);
            bool reversed = SampleClipRadius(position, true);
            return forward || reversed;
        }
        private bool SampleClipRadius(Vector3 position, bool reversed = false)
        {
            //Clips pixel to black if true
            //Returns true only when all directions hit
            for (int i = 0; i < clipDirections; i++)
            {
                //Calculate forward direction
                Vector3 direction = Quaternion.Euler(0, (360.0f / clipDirections) * i, 0) * Vector3.forward;

                //If a single direction hits air position is considered open
                if (!SampleClipDirection(position, direction, reversed)) return false;
            }
            return true;
        }
        private bool SampleClipDirection(Vector3 position, Vector3 direction, bool reversed)
        {
            //Calculate reversed direction & position
            if (reversed)
            {
                position += (direction * clipDistance);
                direction *= -1;
            }

            if (Physics.Raycast(position, direction, clipDistance, layers | ignore)) return true;
            else return false;
        }

        //Gizmo
        private void OnDrawGizmosSelected()
        {
            if (data != null)
            {
                Gizmos.color = Color.white;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(data.offset + new Vector3(0, -data.maxDistance * 0.5f, 0), new Vector3(data.bounds.x, data.maxDistance, data.bounds.z));
            }
        }
#endif
    }
}