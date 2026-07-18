using UnityEngine;
using UnityEngine.Localization;

public class QuestUI : MonoBehaviour
{
    public GameObject questPanel;
    public Transform questList;
    
    private void Start()
    {
        // Initialize quests
        Debug.Log("Quest UI initialized");
    }
    
    public void UpdateQuestList()
    {
        // Update quest display
        Debug.Log("Updating quest list");
    }
}