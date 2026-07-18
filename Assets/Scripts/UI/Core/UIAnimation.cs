using UnityEngine;
using System.Collections;
using UnityEngine.Localization;

public class UIAnimation : MonoBehaviour
{
    public Animator animator;
    
    public void PlayAnimation(string animationName)
    {
        if(animator != null)
        {
            animator.Play(animationName);
        }
    }
    
    public IEnumerator AnimateWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // Animation logic here
        Debug.Log("Animation finished");
    }
}