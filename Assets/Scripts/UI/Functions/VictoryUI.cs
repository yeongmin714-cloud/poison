using UnityEngine;

public class VictoryUI : MonoBehaviour
{
    public GameObject victoryPanel;
    
    private void Start()
    {
        // Initialize victory UI
        Debug.Log("Victory UI initialized");
    }
    
    public void ShowVictory()
    {
        // Show victory screen
        victoryPanel.SetActive(true);
        Debug.Log("Victory screen shown");
    }
}