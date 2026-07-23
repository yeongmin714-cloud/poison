using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class Transition : MonoBehaviour
{
    public virtual void Play(float duration)
    {
        // Base transition implementation
        Debug.Log("Playing base transition for " + duration + " seconds");
    }
}