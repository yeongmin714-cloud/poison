using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialQuestNPC : MonoBehaviour
    {
        [SerializeField] private string npcName;
        [SerializeField] private GameObject questIndicator;
        
        public void ShowQuestIndicator()
        {
            questIndicator.SetActive(true);
        }
    }
}