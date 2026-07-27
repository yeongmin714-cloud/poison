using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Core
{
    public class UICore : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        
        public void Initialize()
        {
            uiManager.Initialize();
        }
    }
}