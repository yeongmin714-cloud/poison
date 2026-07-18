using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIAchievementSystem : MonoBehaviour
    {
        [Header("UI References")]
        public Text achievementTitleText;
        public Text achievementDescriptionText;
        public Image achievementIcon;
        public GameObject achievementPanel;
        
        [Header("Achievement Data")]
        public string achievementTitle = "New Achievement";
        public string achievementDescription = "You've earned this achievement!";
        public Sprite achievementSprite;

        public void UnlockAchievement()
        {
            achievementTitleText.text = achievementTitle;
            achievementDescriptionText.text = achievementDescription;
            achievementIcon.sprite = achievementSprite;
            
            achievementPanel.SetActive(true);
            
            // Auto-hide after 3 seconds
            Invoke("HideAchievement", 3f);
        }

        private void HideAchievement()
        {
            achievementPanel.SetActive(false);
        }
    }
}