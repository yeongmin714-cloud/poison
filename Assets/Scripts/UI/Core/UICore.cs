using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

namespace ProjectName.UI.Core
{
    public class UICore : MonoBehaviour
    {
        public static UICore Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Initialize()
        {
            Debug.Log("UI Core Initialized");
        }
    }
}