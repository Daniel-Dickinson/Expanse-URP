using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Expanse/Genus")]
public class Genus : ScriptableObject
{
    public Mesh lod0;
    public Mesh lod1;
    public Mesh lod2;
    public Mesh billboard;
    public Material baseMaterial;
    public Material billboardMaterial;

    public bool UseLOD1
    {
        get { return (lod1 != null && lod1 != lod0); }
    }
    public bool UseLOD2
    {
        get { return (lod2 != null && lod2 != lod0 && (!UseLOD1 || lod2 != lod1)); }
    }
    public bool UseLOD3
    {
        get { return (billboard != null && billboardMaterial != null); }
    }

    public Vector3 boundsCenter = Vector3.zero;
    public Vector3 boundsExtents = Vector3.one;

    [Range(0, 50)]
    public float optDistance = 20;
    [Range(0, 5000)]
    public float lod1Distance = 100;
    [Range(0, 5000)]
    public float lod2Distance = 500;
    [Range(0, 5000)]
    public float billboardDistance = 1000;
    [Range(0, 5000)]
    public float cullDistance = 1500;
}
