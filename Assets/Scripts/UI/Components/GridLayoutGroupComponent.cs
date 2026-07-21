using UnityEngine;
using UnityEngine.UI;

public class GridLayoutGroupComponent : MonoBehaviour, IUIComponent
{
    [SerializeField] private GridLayoutGroup gridLayoutGroup;
    
    public void Initialize()
    {
        if (gridLayoutGroup == null)
            gridLayoutGroup = GetComponent<GridLayoutGroup>();
    }
    
    public void Cleanup()
    {
        // Cleanup logic
    }
}