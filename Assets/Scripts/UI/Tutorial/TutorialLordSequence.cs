using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialLordSequence : MonoBehaviour
    {
        [SerializeField] private TutorialExecutionSequence sequence;
        [SerializeField] private string sequenceName;
        
        public string SequenceName => sequenceName;
        
        public void Initialize()
        {
            if(sequence != null)
            {
                sequence.Initialize();
            }
        }

        public void StartSequence()
        {
            if(sequence != null)
                sequence.ExecuteSequence();
            else
                Debug.LogError("Tutorial sequence is not assigned");
        }
    }
}