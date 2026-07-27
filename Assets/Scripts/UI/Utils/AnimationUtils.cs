using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public static class AnimationUtils
    {
        public static void SmoothRotate(Transform target, Vector3 rotation, float duration)
        {
            if(target != null)
            {
                // Implementation would use DOTween or LeanTween for smooth rotation
                // Example using Quaternion.Lerp
                Quaternion startRotation = target.rotation;
                Quaternion endRotation = Quaternion.Euler(rotation);
                
                float elapsedTime = 0f;
                while(elapsedTime < duration)
                {
                    elapsedTime += Time.deltaTime;
                    target.rotation = Quaternion.Slerp(startRotation, endRotation, elapsedTime / duration);
                    // This is just conceptual - real implementation would use coroutines
                }
            }
        }
        
        public static void SmoothMove(Transform target, Vector3 position, float duration)
        {
            if(target != null)
            {
                // Implementation would use DOTween or LeanTween for smooth movement
                // Example using Vector3.Lerp
                Vector3 startPosition = target.position;
                float elapsedTime = 0f;
                while(elapsedTime < duration)
                {
                    elapsedTime += Time.deltaTime;
                    target.position = Vector3.Lerp(startPosition, position, elapsedTime / duration);
                    // This is just conceptual - real implementation would use coroutines
                }
            }
        }
    }
}