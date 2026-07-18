using UnityEngine;

public class PauseMenuUI : MonoBehaviour
{
    public GameObject pauseMenuPanel;
    
    private void Start()
    {
        // Initialize pause menu UI
        Debug.Log("Pause Menu UI initialized");
    }
    
    public void ShowPauseMenu()
    {
        // Show pause menu
        pauseMenuPanel.SetActive(true);
    }
}