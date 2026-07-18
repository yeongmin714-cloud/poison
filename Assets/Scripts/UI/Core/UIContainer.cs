using UnityEngine;

public class UIContainer : MonoBehaviour
{
    public RectTransform containerRect;
    public Transform content;
    
    public void AddChild(Transform child)
    {
        if(content != null && child != null)
        {
            child.SetParent(content, false);
        }
    }
}