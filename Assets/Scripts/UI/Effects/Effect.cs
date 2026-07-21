using UnityEngine;
using System.Collections.Generic;

public class Effect : MonoBehaviour
{
    public string effectName;
    public AnimationClip animationClip;
    public AudioClip soundClip;
    
    public virtual void Play(Vector3 position)
    {
        // Base effect play implementation
    }
}