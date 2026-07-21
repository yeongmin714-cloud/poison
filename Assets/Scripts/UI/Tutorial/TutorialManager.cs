using UnityEngine;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    private static TutorialManager instance;
    public static TutorialManager Instance => instance;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void StartTutorial(string tutorialId)
    {
        // Implementation for starting tutorial
    }
    
    public void CompleteTutorial(string tutorialId)
    {
        // Implementation for completing tutorial
    }
}