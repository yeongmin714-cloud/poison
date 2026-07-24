using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UICore : MonoBehaviour
{
    public static UICore Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}