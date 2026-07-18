using UnityEngine;
using TMPro;
using UnityEngine.Localization;

public class UIDialog : MonoBehaviour
{
    public GameObject dialogBox;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;
    
    private void Start()
    {
        // Initialize dialog components
        if(dialogBox != null)
        {
            dialogBox.SetActive(false);
        }
    }
    
    public void ShowDialog(string title, string message)
    {
        if(dialogBox != null)
        {
            dialogBox.SetActive(true);
            if(titleText != null)
                titleText.text = title;
            if(messageText != null)  
                messageText.text = message;
        }
    }
}