using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;

public class UIModal : MonoBehaviour
{
    public GameObject modal;
    public Button closeButton;
    
    private void Start()
    {
        // Setup close button
        if(closeButton != null)
        {
            closeButton.onClick.AddListener(HideModal);
        }
    }
    
    public void ShowModal()
    {
        if(modal != null)
        {
            modal.SetActive(true);
        }
    }
    
    public void HideModal()
    {
        if(modal != null)
        {
            modal.SetActive(false);
        }
    }
}