using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIPlayerStats : MonoBehaviour
    {
        [Header("UI References")]
        public Text playerNameText;
        public Text levelText;
        public Slider expBar;
        public Text goldText;
        public Text statsText;
        
        [Header("Player Data")]
        public string playerName = "Player";
        public int level = 1;
        public int experience = 0;
        public int gold = 100;
        public int strength = 10;
        public int agility = 10;
        public int intelligence = 10;

        private void Start()
        {
            InitializePlayerStats();
        }

        public void InitializePlayerStats()
        {
            playerNameText.text = playerName;
            levelText.text = $"Level {level}";
            expBar.maxValue = 100;
            goldText.text = $"Gold: {gold}";
            UpdateStatsText();
            UpdateUI();
        }

        public void UpdateStatsText()
        {
            statsText.text = $"STR: {strength}\nAGI: {agility}\nINT: {intelligence}";
        }

        public void UpdateUI()
        {
            expBar.value = experience;
        }

        public void AddExperience(int amount)
        {
            experience += amount;
            if (experience >= 100)
            {
                LevelUp();
            }
            UpdateUI();
        }

        public void LevelUp()
        {
            level++;
            levelText.text = $"Level {level}";
            strength++;
            agility++;
            intelligence++;
            UpdateStatsText();
            experience = 0;
        }
    }
}