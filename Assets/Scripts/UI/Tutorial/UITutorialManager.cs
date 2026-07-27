using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class UITutorialManager : MonoBehaviour
    {
        [SerializeField] private TutorialManager tutorialManager;
        
        public void StartTutorial(string tutorialName)
        {
            tutorialManager.StartTutorial(tutorialName);
        }
    }
}