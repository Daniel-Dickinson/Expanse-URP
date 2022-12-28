using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GlobalWind : MonoBehaviour
{
    public Vector2 direction;
    public Vector2 density;
    public float speed;

    //Mono
    private void OnEnable()
    {
        UpdateWind();
    }

    //Core
    public void UpdateWind()
    {
        Shader.SetGlobalVector("_WindSpeed", direction.normalized * speed);
        Shader.SetGlobalVector("_WindDensity", density);
    }
}
