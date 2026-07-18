using UnityEngine;
using TMPro;

public class UIText : MonoBehaviour
{
    public TMPro.TextMeshProUGUI textComponent;
    public string defaultText = "Default Text";
    
    private void Start()
    {
        if(textComponent != null)
        {
            textComponent.text = defaultText;
        }
    }
    
    public void SetText(string newText)
    {
        if(textComponent != null)
        {
            textComponent.text = newText;
        }
    }
}