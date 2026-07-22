using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    private Dictionary<string, MonoBehaviour> uiComponents = new Dictionary<string, MonoBehaviour>();
    
    public void RegisterUIComponent(string name, MonoBehaviour component)
    {
        if (!uiComponents.ContainsKey(name))
        {
            uiComponents.Add(name, component);
        }
        else
        {
            // Debug.LogWarning($"UI Component {name} already registered.");
        }
    }
    
    public T GetUIComponent<T>(string name) where T : MonoBehaviour
    {
        if (uiComponents.TryGetValue(name, out MonoBehaviour component))
        {
            return component as T;
        }
        return null;
    }
    
    public void UnregisterUIComponent(string name)
    {
        uiComponents.Remove(name);
    }
    
    public void ShowScreen(string screenName)
    {
        // Implementation for showing screens
    }
    
    public void HideScreen(string screenName)
    {
        // Implementation for hiding screens
    }
    
    public void ShowTooltip(string text, Vector2 position)
    {
        // Implementation for showing tooltip
    }
    
    public void HideTooltip()
    {
        // Implementation for hiding tooltip
    }
}