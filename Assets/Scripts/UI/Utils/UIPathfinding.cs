using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UIPathfinding : MonoBehaviour
    {
        [Header("Pathfinding Settings")]
        public float moveSpeed = 5.0f;

        public void FindPath(Vector3 start, Vector3 end)
        {
            // Find path from start to end
            Debug.Log($"Finding path from {start} to {end}");
        }

        public void MoveAlongPath(GameObject target, Vector3[] path)
        {
            // Move target along path
            Debug.Log($"Moving {target.name} along path");
        }

        public void StopMovement(GameObject target)
        {
            // Stop movement of target
            Debug.Log($"Stopping movement of {target.name}");
        }
    }
}