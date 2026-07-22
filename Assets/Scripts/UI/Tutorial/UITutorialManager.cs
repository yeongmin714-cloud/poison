using UnityEngine;

public class UITutorialManager : MonoBehaviour
{
    public GameObject tutorialPanel;
    public TMPro.TextMeshProUGUI tutorialText;
    
    private void Start()
    {
        // Initialize tutorial
        // Debug.Log("UI Tutorial Manager initialized");
    }
    
    public void ShowTutorialStep(int step)
    {
        // Show tutorial step
        Debug.Log($"Showing tutorial step {step}");
    }
}