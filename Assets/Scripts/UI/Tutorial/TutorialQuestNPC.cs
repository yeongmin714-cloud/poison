using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Tutorial
{
    public class TutorialQuestNPC : MonoBehaviour
    {
        [Header("NPC Settings")]
        public string npcName = "Quest NPC";
        public string questTitle = "Tutorial Quest";
        public string questDescription = "Complete this tutorial quest";
        public GameObject questMarker;
        public bool isQuestActive = false;
        
        [Header("Dialogue")]
        public string[] dialogueLines;
        public int currentDialogueIndex = 0;
        
        [Header("Events")]
        public UnityEngine.Events.UnityEvent onQuestAccepted;
        public UnityEngine.Events.UnityEvent onQuestCompleted;
        
        public void AcceptQuest()
        {
            isQuestActive = true;
            onQuestAccepted?.Invoke();
            ShowQuestMarker();
        }
        
        public void CompleteQuest()
        {
            isQuestActive = false;
            onQuestCompleted?.Invoke();
            HideQuestMarker();
        }
        
        private void ShowQuestMarker()
        {
            if (questMarker != null)
            {
                questMarker.SetActive(true);
            }
        }
        
        private void HideQuestMarker()
        {
            if (questMarker != null)
            {
                questMarker.SetActive(false);
            }
        }
        
        public void NextDialogueLine()
        {
            if (currentDialogueIndex < dialogueLines.Length)
            {
                // // Debug.Log($"NPC: {dialogueLines[currentDialogueIndex]}");
                currentDialogueIndex++;
            }
            else
            {
                currentDialogueIndex = 0;
            }
        }
    }
}