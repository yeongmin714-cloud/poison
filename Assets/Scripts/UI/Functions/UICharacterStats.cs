using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UICharacterStats : MonoBehaviour
    {
        [Header("UI References")]
        public Text playerNameText;
        public Text levelText;
        public Slider healthBar;
        public Slider manaBar;
        public Text statPointsText;
        
        [Header("Character Data")]
        public string playerName = "Player";
        public int level = 1;
        public int maxHealth = 100;
        public int currentHealth = 100;
        public int maxMana = 100;
        public int currentMana = 100;
        public int statPoints = 0;

        private void Start()
        {
            InitializeStats();
        }

        public void InitializeStats()
        {
            playerNameText.text = playerName;
            levelText.text = "Level: " + level;
            healthBar.maxValue = maxHealth;
            manaBar.maxValue = maxMana;
            UpdateUI();
        }

        public void UpdateUI()
        {
            healthBar.value = currentHealth;
            manaBar.value = currentMana;
            statPointsText.text = "Stat Points: " + statPoints;
        }

        public void TakeDamage(int damage)
        {
            currentHealth = Mathf.Max(0, currentHealth - damage);
            UpdateUI();
        }

        public void Heal(int amount)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            UpdateUI();
        }

        public void TakeMana(int cost)
        {
            currentMana = Mathf.Max(0, currentMana - cost);
            UpdateUI();
        }

        public void RestoreMana(int amount)
        {
            currentMana = Mathf.Min(maxMana, currentMana + amount);
            UpdateUI();
        }

        public void AddStatPoint()
        {
            statPoints++;
            UpdateUI();
        }

        public void LevelUp()
        {
            level++;
            maxHealth += 20;
            maxMana += 10;
            currentHealth = maxHealth;
            currentMana = maxMana;
            statPoints += 5;
            levelText.text = "Level: " + level;
            UpdateUI();
        }
    }
}