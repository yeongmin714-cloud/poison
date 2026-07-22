using UnityEngine;

public class VictoryUI : MonoBehaviour
{
    public GameObject victoryPanel;
    
    private void Start()
    {
        // Initialize victory UI

    }
    
    public void ShowVictory()
    {
        // Show victory screen
        victoryPanel.SetActive(true);
    }
}