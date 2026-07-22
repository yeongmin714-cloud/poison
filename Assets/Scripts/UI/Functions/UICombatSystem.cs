using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UICombatSystem : MonoBehaviour
    {
        [Header("UI References")]
        public Slider healthBar;
        public Slider manaBar;
        public Text combatStatsText;
        public Button attackButton;
        public Button defendButton;
        
        [Header("Combat Data")]
        public int playerHealth = 100;
        public int playerMana = 100;
        public int playerAttack = 10;
        public int playerDefense = 5;

        private void Start()
        {
            InitializeCombat();
        }

        public void InitializeCombat()
        {
            // Setup UI elements
            healthBar.maxValue = playerHealth;
            manaBar.maxValue = playerMana;
            UpdateUI();
            
            // Add button listeners
            attackButton.onClick.AddListener(Attack);
            defendButton.onClick.AddListener(Defend);
        }

        public void UpdateUI()
        {
            healthBar.value = playerHealth;
            manaBar.value = playerMana;
            combatStatsText.text = $"ATK: {playerAttack} | DEF: {playerDefense}";
        }

        public void Attack()
        {
            // Perform attack action
            // Debug.Log("Attacking!");
            UpdateUI();
        }

        public void Defend()
        {
            // Perform defend action
            // Debug.Log("Defending!");
            UpdateUI();
        }

        public void TakeDamage(int damage)
        {
            playerHealth = Mathf.Max(0, playerHealth - damage);
            UpdateUI();
        }
    }
}