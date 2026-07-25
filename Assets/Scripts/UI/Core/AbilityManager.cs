using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbilityManager : MonoBehaviour
{
    public static AbilityManager Instance { get; private set; }

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

    public void UseAbility(string abilityName)
    {
        // Implementation would go here
    }
    
    public void UnlockAbility(string abilityName)
    {
        // Implementation would go here
    }
}