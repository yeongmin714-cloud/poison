using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Localization;

public class UIPanelGroup : MonoBehaviour
{
    public List<UIPanel> panels = new List<UIPanel>();
    
    public void ShowPanel(UIPanel panelToShow)
    {
        foreach(var panel in panels)
        {
            panel.SetActive(panel == panelToShow);
        }
    }
}