using UnityEngine;

public class HelpUI : MonoBehaviour
{
    public GameObject helpPanel;
    
    private void Start()
    {
        // Initialize help UI
        Debug.Log("Help UI initialized");
    }
    
    public void ShowHelp()
    {
        // Show help content
        helpPanel.SetActive(true);
    }
}