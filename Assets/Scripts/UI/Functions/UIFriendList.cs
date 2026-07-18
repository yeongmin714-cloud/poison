using UnityEngine;
using UnityEngine.UI;

namespace UI.Functions
{
    public class UIFriendList : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform friendListPanel;
        public GameObject friendItemPrefab;
        public Text friendCountText;
        
        [Header("Friend Data")]
        public int friendCount = 0;
        public string[] friendNames = {"Friend1", "Friend2", "Friend3"};

        private void Start()
        {
            InitializeFriendList();
        }

        public void InitializeFriendList()
        {
            friendCount = friendNames.Length;
            friendCountText.text = $"Friends: {friendCount}";
            
            // Display friends
            foreach(string friendName in friendNames)
            {
                GameObject friendItem = Instantiate(friendItemPrefab, friendListPanel);
                // Update friend item UI
            }
        }

        public void AddFriend(string friendName)
        {
            friendCount++;
            friendCountText.text = $"Friends: {friendCount}";
        }

        public void RemoveFriend(string friendName)
        {
            friendCount--;
            friendCountText.text = $"Friends: {friendCount}";
        }
    }
}