using UnityEngine;

namespace ProjectName.UI.Utils
{
    public class UIAnimationController : MonoBehaviour
    {
        [Header("Animation Settings")]
        public Animator animator;
        public string animationName = "Idle";

        public void PlayAnimation(string animName)
        {
            // Play specific animation
            if (animator != null)
            {
                animator.Play(animName);
            }
        }

        public void SetAnimation(string animName)
        {
            // Set animation clip
            animationName = animName;
            // Debug.Log($"Animation set to: {animName}");
        }

        public new void StopAnimation()
        {
            // Stop current animation
            if (animator != null)
            {
                animator.enabled = false;
            }
        }
    }
}