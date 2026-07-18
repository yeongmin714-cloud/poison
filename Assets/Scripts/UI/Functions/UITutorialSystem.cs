using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UITutorialSystem : MonoBehaviour
    {
        [Header("UI References")]
        public Text tutorialTitleText;
        public Text tutorialDescriptionText;
        public RectTransform tutorialPanel;
        public Button nextButton;
        public Button prevButton;
        public Button skipButton;
        
        [Header("Tutorial Data")]
        public string tutorialTitle = "Welcome to Tutorial";
        public string tutorialDescription = "This is the first step of the tutorial.";
        public int currentStep = 0;
        public string[] tutorialSteps = {"Step 1", "Step 2", "Step 3"};

        private void Start()
        {
            InitializeTutorial();
        }

        public void InitializeTutorial()
        {
            tutorialTitleText.text = tutorialTitle;
            tutorialDescriptionText.text = tutorialDescription;
            
            // Add button listeners
            nextButton.onClick.AddListener(NextStep);
            prevButton.onClick.AddListener(PrevStep);
            skipButton.onClick.AddListener(SkipTutorial);
        }

        public void NextStep()
        {
            if (currentStep < tutorialSteps.Length - 1)
            {
                currentStep++;
                UpdateTutorialStep();
            }
        }

        public void PrevStep()
        {
            if (currentStep > 0)
            {
                currentStep--;
                UpdateTutorialStep();
            }
        }

        public void SkipTutorial()
        {
            // Skip tutorial
            Debug.Log("Tutorial skipped");
        }

        public void UpdateTutorialStep()
        {
            tutorialDescriptionText.text = tutorialSteps[currentStep];
        }
    }
}