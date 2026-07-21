using UnityEngine;
using UnityEngine.UI;

public class HorizontalLayoutGroupComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private HorizontalLayoutGroup layoutGroup;
    
    public void Initialize()
    {
        if (layoutGroup == null)
            layoutGroup = GetComponent<HorizontalLayoutGroup>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
}