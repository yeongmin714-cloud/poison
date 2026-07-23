using UnityEngine;

public class PauseMenuUI : MonoBehaviour
{
    public GameObject pauseMenuPanel;
    
    private void Start()
    {
        // Initialize pause menu UI
        Debug.Log("Pause menu UI initialized");
    }
    
    public void ShowPauseMenu()
    {
        // Show pause menu
        pauseMenuPanel.SetActive(true);
        Debug.Log("Pause menu shown");
    }
}