using UnityEngine;
using System.Collections.Generic;

public class MathUtils : MonoBehaviour
{
    public static float Clamp(float value, float min, float max)
    {
        return Mathf.Clamp(value, min, max);
    }
    
    public static Vector2 Clamp(Vector2 value, Vector2 min, Vector2 max)
    {
        return new Vector2(Mathf.Clamp(value.x, min.x, max.x), Mathf.Clamp(value.y, min.y, max.y));
    }
}