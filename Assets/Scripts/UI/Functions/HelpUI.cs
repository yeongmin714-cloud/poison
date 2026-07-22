using UnityEngine;

public class HelpUI : MonoBehaviour
{
    public GameObject helpPanel;
    
    private void Start()
    {
        // Initialize help UI

    }
    
    public void ShowHelp()
    {
        // Show help content
        helpPanel.SetActive(true);
    }
}