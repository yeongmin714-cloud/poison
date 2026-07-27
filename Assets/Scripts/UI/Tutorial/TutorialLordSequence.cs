using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialLordSequence : MonoBehaviour
    {
        [SerializeField] private TutorialExecutionSequence sequence;
        
        public void StartSequence()
        {
            sequence.ExecuteSequence();
        }
    }
}