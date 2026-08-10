using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.UI.Core;

namespace Game.UI.Core
{
    public class UICore : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;

        public void Initialize()
        {
            if (uiManager != null)
            {
                uiManager.Initialize();
            }
            else
            {
                Debug.LogError("UIManager reference is missing in UICore");
            }
        }
    }
}