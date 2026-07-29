using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIAnimationController : MonoBehaviour
{
    private void Awake()
    {
        // Initialize animation controller
    }
    {
        [SerializeField] private Animator animator;
        
        public void PlayAnimation(string animationName)
        {
            if (animator != null)
                animator.Play(animationName);
        }
    }
}