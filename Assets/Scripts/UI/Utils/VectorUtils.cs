using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Utils
{
    public static class VectorUtils
    {
        public static Vector3 SnapToGrid(Vector3 position, float gridSize)
        {
            return new Vector3(
                Mathf.Round(position.x / gridSize) * gridSize,
                Mathf.Round(position.y / gridSize) * gridSize,
                Mathf.Round(position.z / gridSize) * gridSize
            );
        }
        
        public static Vector3 GetDirectionToTarget(Vector3 from, Vector3 to)
        {
            return (to - from).normalized;
        }
        
        public static Vector3 GetRandomVector3InRange(Vector3 min, Vector3 max)
        {
            return new Vector3(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                Random.Range(min.z, max.z)
            );
        }
        
        public static bool IsVector3Equal(Vector3 a, Vector3 b, float epsilon = 0.001f)
        {
            return Vector3.Distance(a, b) < epsilon;
        }
    }
}