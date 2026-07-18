using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    public GameObject gameOverPanel;
    
    private void Start()
    {
        // Initialize game over UI
        Debug.Log("Game Over UI initialized");
    }
    
    public void ShowGameOver()
    {
        // Show game over screen
        gameOverPanel.SetActive(true);
    }
}