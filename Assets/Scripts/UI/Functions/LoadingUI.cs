using UnityEngine;

public class LoadingUI : MonoBehaviour
{
    public GameObject loadingPanel;
    public TMPro.TextMeshProUGUI loadingText;
    
    private void Start()
    {
        // Initialize loading UI

    }
    
    public void ShowLoading(string message)
    {
        // Show loading screen
        loadingPanel.SetActive(true);
        if(loadingText != null)
            loadingText.text = message;
    }
}