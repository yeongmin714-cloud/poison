using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIBuffSystem : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform buffPanel;
        public GameObject buffPrefab;
        public Text buffCountText;
        
        [Header("Buff Data")]
        public int buffCount = 0;
        public string[] buffNames = {"Strength Boost", "Speed Boost", "Healing"};

        private void Start()
        {
            InitializeBuffSystem();
        }

        public void InitializeBuffSystem()
        {
            UpdateBuffDisplay();
            
            // Display buffs
            foreach(string buffName in buffNames)
            {
                GameObject buffGO = Instantiate(buffPrefab, buffPanel);
                // Update buff UI
            }
        }

        public void AddBuff(string buffName)
        {
            buffCount++;
            buffCountText.text = $"Buffs: {buffCount}";
        }

        public void RemoveBuff(string buffName)
        {
            buffCount--;
            buffCountText.text = $"Buffs: {buffCount}";
        }

        public void UpdateBuffDisplay()
        {
            buffCountText.text = $"Buffs: {buffCount}";
        }
    }
}