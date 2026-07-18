using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIConditionSystem : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform conditionPanel;
        public GameObject conditionPrefab;
        public Text conditionCountText;
        
        [Header("Condition Data")]
        public int conditionCount = 0;
        public string[] conditionNames = {"Poisoned", "Burned", "Stunned"};

        private void Start()
        {
            InitializeConditionSystem();
        }

        public void InitializeConditionSystem()
        {
            UpdateConditionDisplay();
            
            // Display conditions
            foreach(string conditionName in conditionNames)
            {
                GameObject conditionGO = Instantiate(conditionPrefab, conditionPanel);
                // Update condition UI
            }
        }

        public void AddCondition(string conditionName)
        {
            conditionCount++;
            conditionCountText.text = $"Conditions: {conditionCount}";
        }

        public void RemoveCondition(string conditionName)
        {
            conditionCount--;
            conditionCountText.text = $"Conditions: {conditionCount}";
        }

        public void UpdateConditionDisplay()
        {
            conditionCountText.text = $"Conditions: {conditionCount}";
        }
    }
}