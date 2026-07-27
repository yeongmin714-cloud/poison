using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public static class MathUtils
    {
        public static int Clamp(int value, int min, int max)
        {
            return Mathf.Clamp(value, min, max);
        }
        
        public static float Clamp(float value, float min, float max)
        {
            if(value < min)
                return min;
            if(value > max)
                return max;
            return value;
        }
        
        public static float Lerp(float start, float end, float t)
        {
            return start + (end - start) * Mathf.Clamp01(t);
        }
        
        public static Vector2 Lerp(Vector2 start, Vector2 end, float t)
        {
            return Vector2.Lerp(start, end, Mathf.Clamp01(t));
        }
        
        public static Vector3 Lerp(Vector3 start, Vector3 end, float t)
        {
            return Vector3.Lerp(start, end, Mathf.Clamp01(t));
        }
    }
}