using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.UI.Core
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI References")]
        public UIScreen[] screens;

        void Awake()
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

        private void Start()
        {
            Debug.Log("UI Manager initialized");
        }

        public void ShowScreen(UIScreen screen)
        {
            if (screens == null) return;
            foreach (var s in screens)
            {
                if (s != null)
                    s.gameObject.SetActive(s == screen);
            }
        }
    }
}