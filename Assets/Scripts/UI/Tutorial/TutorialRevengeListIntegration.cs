using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialRevengeListIntegration : MonoBehaviour
    {
        [SerializeField] private TutorialManager tutorialManager;
        [SerializeField] private GameObject revengeListPanel;
        
        public void EnableRevengeList()
        {
            revengeListPanel.SetActive(true);
            tutorialManager.StartTutorial("revenge");
        }
    }
}