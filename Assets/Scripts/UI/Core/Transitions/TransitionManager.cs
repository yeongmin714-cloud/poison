using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class TransitionManager : MonoBehaviour
{
    private static TransitionManager instance;
    public static TransitionManager Instance => instance;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void PlayTransition(TransitionType type, float duration)
    {
        // Implementation for playing transitions
        Debug.Log("Playing transition: " + type + " for " + duration + " seconds");
    }
    
    public void PlayTransition(Transition transition, float duration)
    {
        // Implementation for playing custom transitions
        Debug.Log("Playing custom transition for " + duration + " seconds");
    }
}