using UnityEngine;
using UnityEngine.UI;

public class UIAnimationController : MonoBehaviour
{
    private void Awake()
    {
        // Initialize animation controller
    }

    [SerializeField] private Animator animator;

    public void PlayAnimation(string animationName)
    {
        if (animator != null)
            animator.Play(animationName);
        else
            Debug.LogWarning("No animator component found for animation: " + animationName);
    }
}