using UnityEngine;
using System.Collections.Generic;

namespace UI.Core
{
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        private Dictionary<string, TutorialStep> _tutorials = new Dictionary<string, TutorialStep>();

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

        public void StartTutorial(string tutorialName)
        {
            if (_tutorials.TryGetValue(tutorialName, out TutorialStep tutorial))
            {
                tutorial.Start();
            }
        }

        public void CompleteTutorial(string tutorialName)
        {
            if (_tutorials.TryGetValue(tutorialName, out TutorialStep tutorial))
            {
                tutorial.Complete();
            }
        }

        public void RegisterTutorial(string name, TutorialStep tutorial)
        {
            _tutorials.Add(name, tutorial);
        }
    }

    public class TutorialStep
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsCompleted { get; private set; }

        public void Start()
        {
            // Implementation for starting the tutorial step
        }

        public void Complete()
        {
            IsCompleted = true;
        }
    }
}