using UnityEngine;

public class LanguageUI : MonoBehaviour
{
    public GameObject languagePanel;
    public TMPro.TextMeshProUGUI languageLabel;
    
    private void Start()
    {
        // Initialize language UI
        Debug.Log("Language UI initialized");
    }
    
    public void ChangeLanguage(string language)
    {
        // Change language
        Debug.Log("Language changed to: " + language);
    }
}