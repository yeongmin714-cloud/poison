using UnityEngine;
using System.Collections.Generic;
using System.Collections;
public class EventSystemManager : MonoBehaviour
{
    private static EventSystemManager instance;
    public static EventSystemManager Instance => instance;
    
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
}