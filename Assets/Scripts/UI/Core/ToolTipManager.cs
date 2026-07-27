using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToolTipManager : MonoBehaviour
{
    public static ToolTipManager Instance { get; private set; }
    
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
    
    public void ShowToolTip(string message)
    {
        // Implementation would go here
    }
    
    public void HideToolTip()
    {
        // Implementation would go here
    }
    
    /// <summary>
    /// Updates tooltip position
    /// </summary>
    public void UpdateToolTipPosition(Vector2 position)
    {
        // Implementation would go here
    }
}