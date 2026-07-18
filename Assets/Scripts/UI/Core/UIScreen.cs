using UnityEngine;
using UnityEngine.UI;

public class UIScreen : MonoBehaviour
{
    [Header("Screen Settings")]
    public bool isActive = false;
    public Canvas canvas;
    
    private void Awake()
    {
        // Verify required components
        if(canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }
    }
    
    public virtual void Show()
    {
        gameObject.SetActive(true);
        isActive = true;
    }
    
    public virtual void Hide()
    {
        gameObject.SetActive(false);
        isActive = false;
    }
}