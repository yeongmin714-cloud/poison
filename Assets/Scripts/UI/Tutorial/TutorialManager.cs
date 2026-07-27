using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private List<TutorialLordSequence> sequences;
        private Dictionary<string, TutorialLordSequence> sequenceDictionary;
        
        public void Initialize()
        {
            sequenceDictionary = new Dictionary<string, TutorialLordSequence>();
            foreach(var sequence in sequences)
            {
                if(sequence != null && !sequenceDictionary.ContainsKey(sequence.SequenceName))
                {
                    sequenceDictionary.Add(sequence.SequenceName, sequence);
                }
            }
        }

        public void StartTutorial(string tutorialName)
        {
            if(sequenceDictionary != null && sequenceDictionary.ContainsKey(tutorialName))
            {
                sequenceDictionary[tutorialName].StartSequence();
            }
            else
            {
                // Fallback to searching through all sequences
                foreach (var sequence in sequences)
                {
                    if (sequence != null && sequence.SequenceName == tutorialName)
                    {
                        sequence.StartSequence();
                        break;
                    }
                }
            }
        }
    }
}