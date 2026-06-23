using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// C9-30: 퀘스트 윈도우 — QuestManager 연동, 진행 중/완료 퀘스트 표시
    /// </summary>
    public class QuestWindow : UIWindow
    {
        protected virtual void Start()
        {
            ApplyTheme(Phase33_Themes.CreateQuestTheme());
        }

        [Header("Quest Window")]
        [SerializeField] private Transform _questListContainer;

        private Vector2 _scrollPos;

        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;

        protected override void OnShow()
        {
            Debug.Log("[QuestWindow] 열림");
            RefreshQuestList();
        }

        protected override void OnHide()
        {
            Debug.Log("[QuestWindow] 닫힘");
        }

        /// <summary>
        /// 퀘스트 목록 갱신 — QuestManager에서 데이터 로드
        /// </summary>
        public void RefreshQuestList()
        {
            Debug.Log($"[QuestWindow] 퀘스트 목록 갱신: {QuestManager.GetActiveQuests().Count} 진행 중");
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            EnsureStyles();

            float panelW = 900f;
            float panelH = 750f;
            float x = (Screen.width - panelW) / 2f;
            float y = (Screen.height - panelH) / 2f;

            GUI.Box(new Rect(x, y, panelW, panelH), "");

            // 타이틀
            GUI.Label(new Rect(x + 10, y + 5, panelW - 20, 42), "📋 퀘스트 목록", _styleTitle);

            // 통계
            var active = QuestManager.GetActiveQuests();
            var completed = QuestManager.GetCompletedQuests();
            GUI.Label(new Rect(x + 10, y + 35, 450, 30), $"🔄 진행 중: {active.Count}개", _styleLabel);
            GUI.Label(new Rect(x + 220, y + 35, 450, 30), $"✅ 완료: {completed.Count}개", _styleValue);

            // 퀘스트 목록
            float listY = y + 60;
            float listH = panelH - 100;
            float itemH = 120f;
            float totalH = (active.Count + completed.Count) * itemH + 10;

            _scrollPos = GUI.BeginScrollView(new Rect(x + 10, listY, panelW - 20, listH), _scrollPos,
                new Rect(0, 0, panelW - 40, totalH));

            float cy = 5;

            // 진행 중 퀘스트
            for (int i = 0; i < active.Count; i++)
            {
                var quest = active[i];
                DrawQuestEntry(quest, cy, panelW - 60, QuestState.Active);
                cy += itemH;
            }

            // 완료 퀘스트
            for (int i = 0; i < completed.Count; i++)
            {
                var quest = completed[i];
                DrawQuestEntry(quest, cy, panelW - 60, QuestState.Completed);
                cy += itemH;
            }

            if (active.Count == 0 && completed.Count == 0)
            {
                GUI.Label(new Rect(10, cy, panelW - 40, 30), "퀘스트가 없습니다. NPC를 찾아 퀘스트를 수락하세요.", _styleLabel);
            }

            GUI.EndScrollView();
        }

        private void DrawQuestEntry(QuestData quest, float y, float width, QuestState state)
        {
            GUI.Box(new Rect(0, y, width, 112), "");

            string stateStr = state == QuestState.Active ? "🔄 진행 중" : "✅ 완료";
            string colorHex = state == QuestState.Active ? "#FFDD44" : "#44FF44";

            GUI.Label(new Rect(10, y + 4, width - 20, 33), $"<color={colorHex}>{quest.questName}</color>  {stateStr}", _styleTitle);

            if (!string.IsNullOrEmpty(quest.description))
                GUI.Label(new Rect(10, y + 28, width - 20, 27), quest.description, new GUIStyle(GUI.skin.label) { fontSize = 48, normal = { textColor = Color.gray } });

            if (quest.objectives != null && quest.objectives.Count > 0)
            {
                var obj = quest.objectives[0];
                string prog = obj.requiredCount > 0 ? $" ({obj.currentCount}/{obj.requiredCount})" : "";
                GUI.Label(new Rect(10, y + 48, width - 20, 24), $"▸ {obj.description}{prog}", new GUIStyle(GUI.skin.label) { fontSize = 44, normal = { textColor = Color.cyan } });
            }

            // 보상 표시
            string rewardStr = "";
            if (quest.reward.gold > 0) rewardStr += $"💰{quest.reward.gold} ";
            if (quest.reward.exp > 0) rewardStr += $"✨{quest.reward.exp}EXP";
            if (!string.IsNullOrEmpty(rewardStr))
                GUI.Label(new Rect(10, y + 64, width - 20, 21), rewardStr, new GUIStyle(GUI.skin.label) { fontSize = 40, normal = { textColor = Color.yellow } });
        }

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;
            _styleTitle = new GUIStyle(GUI.skin.label) { fontSize = 60, fontStyle = FontStyle.Bold, richText = true, normal = { textColor = Color.white } };
            _styleLabel = new GUIStyle(GUI.skin.label) { fontSize = 52, normal = { textColor = Color.white } };
            _styleValue = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold, normal = { textColor = Color.green } };
        }
    }
}