using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private List<TutorialLordSequence> sequences;
        
        public void StartTutorial(string tutorialName)
        {
            foreach (var sequence in sequences)
            {
                // Implementation would select specific sequence based on tutorialName
                sequence.StartSequence();
            }
        }
    }
}