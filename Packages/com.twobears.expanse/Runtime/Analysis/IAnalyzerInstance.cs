using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IAnalyzerInstance
{
    public string Name
    {
        get;
    }
    public string DrawShadows
    {
        get;
    }
    public int InstanceCount
    {
        get;
    }
    public int VertexCount
    {
        get;
    }
    public int TriangleCount
    {
        get;
    }
    public double PrepTime
    {
        get;
    }
    public double DrawTime
    {
        get;
    }
    
}
