using UnityEngine;
using UnityEngine.UI;

namespace UI.Utils
{
    public class UITutorialManager : MonoBehaviour
    {
        [Header("UI References")]
        public Text tutorialTitle;
        public Text tutorialDescription;
        public RectTransform tutorialPanel;
        public Button nextButton;
        public Button prevButton;
        public Button skipButton;
        
        [Header("Tutorial Data")]
        public string[] tutorialSteps = {"Step 1", "Step 2", "Step 3"};
        public int currentStep = 0;

        public void InitializeTutorial()
        {
            // Setup tutorial UI
            if (tutorialTitle != null)
                tutorialTitle.text = "Tutorial";
            if (tutorialDescription != null)
                tutorialDescription.text = tutorialSteps[currentStep];
            
            // Add button listeners
            if (nextButton != null)
                nextButton.onClick.AddListener(NextStep);
            if (prevButton != null)
                prevButton.onClick.AddListener(PrevStep);
            if (skipButton != null)
                skipButton.onClick.AddListener(SkipTutorial);
        }

        public void NextStep()
        {
            if (currentStep < tutorialSteps.Length - 1)
            {
                currentStep++;
                if (tutorialDescription != null)
                    tutorialDescription.text = tutorialSteps[currentStep];
            }
        }

        public void PrevStep()
        {
            if (currentStep > 0)
            {
                currentStep--;
                if (tutorialDescription != null)
                    tutorialDescription.text = tutorialSteps[currentStep];
            }
        }

        public void SkipTutorial()
        {
            // Skip tutorial logic
            Debug.Log("Tutorial skipped");
        }
    }
}