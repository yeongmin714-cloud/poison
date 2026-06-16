using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-30: NPC 퀘스트 제공자 — 말풍선 UI로 퀘스트 제공
    /// </summary>
    public class NpcQuestGiver : MonoBehaviour
    {
        [Header("NPC 설정")]
        [SerializeField] private string npcId = "npc_001";
        [SerializeField] private string npcName = "수상한 여행자";
        [SerializeField] private float _interactRange = 3f;

        [Header("퀘스트")]
        [SerializeField] private string[] _questIds;

        // 상태
        private bool _playerNearby = false;
        private bool _showQuestUI = false;
        private Transform _player;
        private Vector2 _scrollPos;

        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleButton;
        private GUIStyle _styleMsg;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning($"[NpcQuestGiver] {npcName}: Player 태그 오브젝트 없음");
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _playerNearby = dist <= _interactRange;

            if (_playerNearby && Input.GetKeyDown(KeyCode.E))
            {
                _showQuestUI = !_showQuestUI;
            }

            if (_showQuestUI && dist > _interactRange * 1.5f)
            {
                _showQuestUI = false;
            }
        }

        private void OnGUI()
        {
            if (!_playerNearby) return;

            // 말풍선: NPC 머리 위 표시
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);
            if (screenPos.z < 0) return;
            screenPos.y = Screen.height - screenPos.y;

            // "E 키" 말풍선
            float bubbleW = 60f;
            float bubbleH = 24f;
            GUI.Box(new Rect(screenPos.x - bubbleW / 2f, screenPos.y - bubbleH - 5, bubbleW, bubbleH), "💬 E");
            GUI.Label(new Rect(screenPos.x - bubbleW / 2f, screenPos.y - bubbleH - 5, bubbleW, bubbleH), "💬 E", new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter });

            if (!_showQuestUI) return;

            // 퀘스트 UI 패널
            float panelW = 350f;
            float panelH = 300f;
            float x = (Screen.width - panelW) / 2f;
            float y = (Screen.height - panelH) / 2f;

            EnsureStyles();

            GUI.Box(new Rect(x, y, panelW, panelH), "");
            GUI.Label(new Rect(x + 10, y + 5, panelW - 20, 24), $"🗣️ {npcName} — 퀘스트", _styleTitle);

            if (_questIds == null || _questIds.Length == 0)
            {
                GUI.Label(new Rect(x + 10, y + 40, panelW - 20, 20), "제공할 퀘스트가 없습니다.", _styleLabel);
                if (GUI.Button(new Rect(x + panelW / 2f - 40, y + panelH - 35, 80, 28), "닫기"))
                    _showQuestUI = false;
                return;
            }

            // 퀘스트 목록 스크롤
            float contentY = y + 35;
            float contentH = panelH - 80;
            float itemH = 70f;
            float totalH = _questIds.Length * itemH;

            _scrollPos = GUI.BeginScrollView(new Rect(x + 5, contentY, panelW - 10, contentH), _scrollPos,
                new Rect(0, 0, panelW - 30, totalH));

            for (int i = 0; i < _questIds.Length; i++)
            {
                string qid = _questIds[i];
                QuestData quest = QuestManager.GetQuest(qid);
                QuestState state = QuestManager.GetQuestState(qid);
                float iy = i * itemH;

                GUI.Box(new Rect(5, iy + 2, panelW - 40, itemH - 4), "");

                string stateStr = state switch
                {
                    QuestState.Locked => "🔒 잠김",
                    QuestState.Available => "📋 수락 가능",
                    QuestState.Active => "🔄 진행 중",
                    QuestState.Completed => "✅ 완료",
                    QuestState.Failed => "❌ 실패",
                    _ => "?"
                };

                GUI.Label(new Rect(10, iy + 4, panelW - 60, 20), $"{quest.questName} {stateStr}", _styleLabel);

                if (quest.objectives != null && quest.objectives.Count > 0)
                {
                    var obj = quest.objectives[0];
                    string prog = obj.requiredCount > 0 ? $"({obj.currentCount}/{obj.requiredCount})" : "";
                    GUI.Label(new Rect(10, iy + 26, panelW - 60, 16), $"{obj.description} {prog}", new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.gray } });
                }

                // 버튼
                float btnX = panelW - 100;
                if (state == QuestState.Available)
                {
                    if (GUI.Button(new Rect(btnX, iy + 20, 75, 24), "수락"))
                    {
                        QuestManager.AcceptQuest(qid);
                    }
                }
                else if (state == QuestState.Active)
                {
                    if (GUI.Button(new Rect(btnX, iy + 20, 75, 24), "확인"))
                    {
                        if (QuestManager.TryCompleteQuest(qid))
                            Debug.Log($"[NpcQuestGiver] ✅ {quest.questName} 완료!");
                    }
                }
            }

            GUI.EndScrollView();

            if (GUI.Button(new Rect(x + panelW - 90, y + panelH - 35, 80, 28), "닫기"))
                _showQuestUI = false;
        }

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;
            _styleTitle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _styleLabel = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            _styleButton = new GUIStyle(GUI.skin.button) { fontSize = 12 };
            _styleMsg = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Italic, normal = { textColor = Color.yellow } };
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}