using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIRankingSystem : MonoBehaviour
    {
        [Header("UI References")]
        public Text rankTitleText;
        public Text rankDescriptionText;
        public RectTransform rankingListPanel;
        public GameObject rankingItemPrefab;
        
        [Header("Ranking Data")]
        public string rankTitle = "Global Ranking";
        public string rankDescription = "Top players in the game";
        public int[] playerRanks = {1, 2, 3, 4, 5};

        private void Start()
        {
            InitializeRanking();
        }

        public void InitializeRanking()
        {
            rankTitleText.text = rankTitle;
            rankDescriptionText.text = rankDescription;
            
            // Display ranking
            foreach(int rank in playerRanks)
            {
                GameObject rankingItem = Instantiate(rankingItemPrefab, rankingListPanel);
                // Update ranking item UI
            }
        }

        public void UpdateRankings(int[] newRanks)
        {
            playerRanks = newRanks;
            // Refresh UI with new rankings
        }
    }
}