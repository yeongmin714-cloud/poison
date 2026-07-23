using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

public class AbilityManager : MonoBehaviour
{
    private static AbilityManager instance;
    public static AbilityManager Instance => instance;
    
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
    
    public void UseAbility(string abilityName)
    {
        // Implementation for using abilities
        Debug.Log("Ability used: " + abilityName);
    }
    
    public bool CanUseAbility(string abilityName)
    {
        // Implementation for checking if ability can be used
        Debug.Log("Checking if ability can be used: " + abilityName);
        return true;
    }
}