using UnityEngine;
using Game.UI.Core;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VerticalLayoutGroupComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private VerticalLayoutGroup layoutGroup;
    
    public void Initialize()
    {
        if (layoutGroup == null)
            layoutGroup = GetComponent<VerticalLayoutGroup>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
}