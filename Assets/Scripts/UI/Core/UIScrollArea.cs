using UnityEngine;
using UnityEngine.UI;

public class UIScrollArea : MonoBehaviour
{
    public ScrollRect scrollRect;
    
    public void ScrollToTop()
    {
        if(scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1;
        }
    }
}