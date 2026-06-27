using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 5.4: NPC 퀘스트 제공자.
    /// 플레이어가 근처에서 E 키를 누르면 퀘스트를 수락/제출.
    /// 예: 고기 3개 가져오기 → 보상 지급.
    /// </summary>
    public class NpcQuestGiver : MonoBehaviour
    {
        [System.Serializable]
        public class QuestDef
        {
            public string questId = "quest_meat_01";
            public string questName = "고기 수집";
            public string description = "신선한 고기 3개를 가져와 주세요.";
            public string requiredItemId = "meat_rabbit";
            public int requiredCount = 3;
            public int rewardGold = 100;
            public int rewardExp = 50;
        }

        [Header("퀘스트 설정")]
        [SerializeField] private QuestDef _quest;
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private string _npcName = "마을 주민";

        [Header("상태")]
        [SerializeField] private bool _questAccepted;
        [SerializeField] private bool _questCompleted;

        private Transform _player;
        private bool _isPlayerNearby;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            if (_player == null || _questCompleted) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _isPlayerNearby = dist <= _interactRange;

            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                Interact();
            }
        }

        private void Interact()
        {
            if (!_questAccepted)
            {
                AcceptQuest();
            }
            else
            {
                TryCompleteQuest();
            }
        }

        private void AcceptQuest()
        {
            if (_quest == null)
            {
                Debug.LogError("[NpcQuestGiver] QuestDef가 할당되지 않았습니다! Inspector에서 _quest를 설정해주세요.");
                return;
            }

            _questAccepted = true;
            Debug.Log($"[NpcQuestGiver] {_npcName}: \"{_quest.description}\"");
        }

        private void TryCompleteQuest()
        {
            if (_quest == null)
            {
                Debug.LogError("[NpcQuestGiver] QuestDef가 할당되지 않았습니다! Inspector에서 _quest를 설정해주세요.");
                return;
            }

            if (PlayerInventory.Instance == null)
            {
                Debug.LogError("[NpcQuestGiver] PlayerInventory.Instance가 없습니다!");
                return;
            }

            int count = PlayerInventory.Instance.GetItemCount(_quest.requiredItemId);
            if (count >= _quest.requiredCount)
            {
                if (!PlayerInventory.Instance.RemoveItem(_quest.requiredItemId, _quest.requiredCount))
                {
                    Debug.LogError($"[NpcQuestGiver] 아이템 제거 실패: {_quest.requiredItemId} x{_quest.requiredCount}");
                    return;
                }

                if (PlayerStats.Instance != null)
                {
                    PlayerStats.Instance.AddGold(_quest.rewardGold);
                    PlayerStats.Instance.AddEXP(_quest.rewardExp);
                }
                else
                {
                    Debug.LogWarning("[NpcQuestGiver] PlayerStats.Instance가 없어 골드/경험치를 지급할 수 없습니다.");
                }

                _questCompleted = true;
                Debug.Log($"[NpcQuestGiver] ✅ 퀘스트 완료! {_quest.questName} — 골드+{_quest.rewardGold}, 경험치+{_quest.rewardExp}");
            }
            else
            {
                Debug.Log($"[NpcQuestGiver] {_npcName}: 아직 아이템이 부족합니다 ({count}/{_quest.requiredCount})");
            }
        }

        private void OnGUI()
        {
            if (!_isPlayerNearby || _questCompleted) return;

            if (_quest == null)
            {
                GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30), "[E] 설정 오류: 퀘스트 데이터 없음");
                return;
            }

            string msg = _questAccepted
                ? $"[E] {_npcName} — 퀘스트 제출"
                : $"[E] {_npcName} — \"{_quest.description}\"";
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30), msg);
        }
    }
}