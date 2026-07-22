using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UICore : MonoBehaviour
{
    public static UICore Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // TODO: Implement core UI functionality
    public void InitializeUI()
    {
        // This is a placeholder
    }
}