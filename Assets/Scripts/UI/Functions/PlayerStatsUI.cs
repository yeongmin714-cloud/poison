using UnityEngine;
using TMPro;

public class PlayerStatsUI : MonoBehaviour
{
    public GameObject statsPanel;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI manaText;
    
    private void Start()
    {
        // Initialize player stats
        // TODO: Implement proper initialization
        Debug.Log("Player stats UI initialized");
    }
    
    public void UpdateStats()
    {
        // Update stats display
        Debug.Log("Player stats updated");
    }
}