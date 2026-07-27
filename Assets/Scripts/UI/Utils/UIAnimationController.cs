using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public class UIAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        
        public void PlayAnimation(string animationName)
        {
            animator.Play(animationName);
        }
    }
}