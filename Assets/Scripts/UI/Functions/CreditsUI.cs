using UnityEngine;

public class CreditsUI : MonoBehaviour
{
    public GameObject creditsPanel;
    public TMPro.TextMeshProUGUI creditsText;
    
    private void Start()
    {
        // Initialize credits UI
        // Debug.Log("Credits UI initialized");
    }
    
    public void ShowCredits()
    {
        // Show credits
        creditsPanel.SetActive(true);
    }
}