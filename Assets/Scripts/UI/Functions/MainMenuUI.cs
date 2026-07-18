using UnityEngine;

public class MainMenuUI : MonoBehaviour
{
    public GameObject mainMenuPanel;
    
    private void Start()
    {
        // Initialize main menu UI
        Debug.Log("Main Menu UI initialized");
    }
    
    public void ShowMainMenu()
    {
        // Show main menu
        mainMenuPanel.SetActive(true);
    }
}