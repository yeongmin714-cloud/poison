using UnityEngine;

public class UIHelper : MonoBehaviour
{
    public static RectTransform GetRectTransform(GameObject go)
    {
        return go.GetComponent<RectTransform>();
    }
    
    public static Canvas GetCanvas(GameObject go)
    {
        return go.GetComponent<Canvas>();
    }
}