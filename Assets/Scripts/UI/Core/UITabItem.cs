using UnityEngine;
using UnityEngine.UI;

public class UITabItem : MonoBehaviour
{
    public Toggle toggle;
    public GameObject content;
    
    public void OnTabSelected()
    {
        if(content != null)
        {
            content.SetActive(toggle.isOn);
        }
    }
}