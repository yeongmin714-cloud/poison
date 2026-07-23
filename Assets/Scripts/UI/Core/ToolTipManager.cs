using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;

public class ToolTipManager : MonoBehaviour
{
    private static ToolTipManager instance;
    public static ToolTipManager Instance => instance;
    
    [SerializeField] private GameObject tooltipPrefab;
    [SerializeField] private Transform tooltipParent;
    
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
    
    public void ShowTooltip(string text, Vector2 position)
    {
        if (tooltipPrefab == null || tooltipParent == null)
            return;
        
        GameObject tooltip = Instantiate(tooltipPrefab, tooltipParent);
        // Additional tooltip setup would go here
    }
    
    public void HideTooltip()
    {
        // Implementation for hiding tooltip
        // Add actual tooltip hiding logic here if needed
    }
    
    public void SetTooltipPosition(Vector2 position)
    {
        // Implementation for setting tooltip position
        // Add actual position setting logic here if needed
    }
}