using UnityEngine;
using System.Collections.Generic;
using System.Collections;

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
        // Implementation for showing tooltip
    }
    
    public void HideTooltip()
    {
        // Implementation for hiding tooltip
    }
    
    public void SetTooltipPosition(Vector2 position)
    {
        // Implementation for setting tooltip position
    }
}