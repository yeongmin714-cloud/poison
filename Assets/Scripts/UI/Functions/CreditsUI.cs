using UnityEngine;

public class CreditsUI : MonoBehaviour
{
    public GameObject creditsPanel;
    public TMPro.TextMeshProUGUI creditsText;
    
    private void Start()
    {
        // Initialize credits UI

    }
    
    public void ShowCredits()
    {
        // Show credits
        creditsPanel.SetActive(true);
    }
}