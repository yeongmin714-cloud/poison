using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    public GameObject gameOverPanel;
    
    private void Start()
    {
        // Initialize game over UI

    }
    
    public void ShowGameOver()
    {
        // Show game over screen
        gameOverPanel.SetActive(true);
    }
}