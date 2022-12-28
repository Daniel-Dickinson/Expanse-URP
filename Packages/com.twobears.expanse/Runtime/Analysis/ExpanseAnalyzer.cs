using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwoBears.Expanse
{
    [ExecuteAlways]
    public class ExpanseAnalyzer : MonoBehaviour
    {
        //Cache
        private static List<IAnalyzerInstance> renderers;

        //Registration
        public static void RegisterRenderer(IAnalyzerInstance renderer)
        {
            if (renderer == null) return;
            if (renderers == null) renderers = new List<IAnalyzerInstance>();
            renderers.Add(renderer);
        }
        public static void DeregisterRenderer(IAnalyzerInstance renderer)
        {
            if (renderer == null) return;
            if (renderers == null) return;
            renderers.Remove(renderer);
        }

        //Totals
        public int VisibleInstances
        {
            get
            {
                if (renderers == null) return 0;

                int total = 0;
                for (int i = 0; i < renderers.Count; i++)
                {
                    total += renderers[i].InstanceCount;
                }
                return total;
            }
        }
        public int VisibleVerts
        {
            get
            {
                if (renderers == null) return 0;

                int total = 0;
                for (int i = 0; i < renderers.Count; i++)
                {
                    total += renderers[i].VertexCount;
                }
                return total;
            }
        }
        public int VisibleTris
        {
            get
            {
                if (renderers == null) return 0;

                int total = 0;
                for (int i = 0; i < renderers.Count; i++)
                {
                    total += renderers[i].TriangleCount;
                }
                return total;
            }
        }
        public double VisibleTime
        {
            get
            {
                if (renderers == null) return 0;

                double total = 0;
                for (int i = 0; i < renderers.Count; i++)
                {
                    total += renderers[i].PrepTime + renderers[i].DrawTime;
                }
                return total;
            }
        }

        //Instances
        public int RendererCount
        {
            get { return (renderers != null) ? renderers.Count : 0; }
        }
        public List<IAnalyzerInstance> Renderers
        {
            get { return renderers; }
        }
        public IAnalyzerInstance GetInstance(int index)
        {
            return Renderers[index];
        }
    }
}