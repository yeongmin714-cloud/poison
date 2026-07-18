using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIPlayerList : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform playerListPanel;
        public GameObject playerItemPrefab;
        public Text playerCountText;
        
        [Header("Player Data")]
        public int playerCount = 0;
        public string[] playerNames = {"Player1", "Player2", "Player3"};

        private void Start()
        {
            InitializePlayerList();
        }

        public void InitializePlayerList()
        {
            playerCount = playerNames.Length;
            playerCountText.text = $"Players: {playerCount}";
            
            // Display players
            foreach(string playerName in playerNames)
            {
                GameObject playerItem = Instantiate(playerItemPrefab, playerListPanel);
                // Update player item UI
            }
        }

        public void AddPlayer(string playerName)
        {
            playerCount++;
            playerCountText.text = $"Players: {playerCount}";
        }

        public void RemovePlayer(string playerName)
        {
            playerCount--;
            playerCountText.text = $"Players: {playerCount}";
        }
    }
}