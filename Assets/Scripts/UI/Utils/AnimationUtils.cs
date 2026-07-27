using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Utils
{
    public static class AnimationUtils
    {
        public static void PlayAnimation(Animator animator, string animationName)
        {
            animator.Play(animationName);
        }
    }
}