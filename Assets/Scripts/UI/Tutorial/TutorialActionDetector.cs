using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Tutorial
{
    public class TutorialActionDetector : MonoBehaviour
    {
        [SerializeField] private string actionName;
        
        public string ActionName => actionName;
    }
}