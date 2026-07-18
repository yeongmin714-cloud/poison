using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIQuestManager : MonoBehaviour
    {
        [Header("UI References")]
        public Text questTitleText;
        public Text questDescriptionText;
        public Text questStatusText;
        public Button acceptButton;
        public Button completeButton;
        
        [Header("Quest Data")]
        public string questTitle = "New Quest";
        public string questDescription = "Complete this quest to earn rewards.";
        public QuestStatus currentStatus = QuestStatus.InProgress;

        public enum QuestStatus
        {
            Available,
            InProgress,
            Completed
        }

        private void Start()
        {
            InitializeQuest();
        }

        public void InitializeQuest()
        {
            questTitleText.text = questTitle;
            questDescriptionText.text = questDescription;
            UpdateQuestStatus();
        }

        public void UpdateQuestStatus()
        {
            switch(currentStatus)
            {
                case QuestStatus.Available:
                    questStatusText.text = "Available";
                    questStatusText.color = Color.green;
                    acceptButton.gameObject.SetActive(true);
                    completeButton.gameObject.SetActive(false);
                    break;
                case QuestStatus.InProgress:
                    questStatusText.text = "In Progress";
                    questStatusText.color = Color.yellow;
                    acceptButton.gameObject.SetActive(false);
                    completeButton.gameObject.SetActive(true);
                    break;
                case QuestStatus.Completed:
                    questStatusText.text = "Completed";
                    questStatusText.color = Color.blue;
                    acceptButton.gameObject.SetActive(false);
                    completeButton.gameObject.SetActive(false);
                    break;
            }
        }

        public void AcceptQuest()
        {
            currentStatus = QuestStatus.InProgress;
            UpdateQuestStatus();
        }

        public void CompleteQuest()
        {
            currentStatus = QuestStatus.Completed;
            UpdateQuestStatus();
        }
    }
}