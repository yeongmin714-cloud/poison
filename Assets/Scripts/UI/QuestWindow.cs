using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;
#pragma warning disable 0414

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

        private Vector2 _scrollPos;

        // 캐시된 GUIStyle — GC-safe
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleDesc;
        private GUIStyle _styleObjective;
        private GUIStyle _styleReward;

        // 캐시된 퀘스트 리스트 — OnGUI GC 할당 방지
        private List<QuestData> _cachedActive;
        private List<QuestData> _cachedCompleted;
        private bool _needsRefresh = true;

        protected override void OnShow()
        {
            Debug.Log("[QuestWindow] 열림");
            base.OnShow(); // ★ 필수: 테마 배경 렌더링
            RefreshQuestList();
        }

        protected override void OnHide()
        {
            Debug.Log("[QuestWindow] 닫힘");
        }

        /// <summary>
        /// 퀘스트 목록 갱신 — QuestManager에서 데이터 로드, 캐시 갱신
        /// </summary>
        public void RefreshQuestList()
        {
            _cachedActive = QuestManager.GetActiveQuests();
            _cachedCompleted = QuestManager.GetCompletedQuests();
            _needsRefresh = false;
            Debug.Log($"[QuestWindow] 캐시 갱신: {_cachedActive.Count} 진행 중, {_cachedCompleted.Count} 완료");
        }

        /// <summary>강제 재갱신 플래그 설정 (외부에서 퀘스트 변경 시 호출)</summary>
        public void MarkDirty()
        {
            _needsRefresh = true;
        }

        protected override void OnGUI()
        {
            if (!IsOpen) return;

            EnsureStyles();

            // 변경 감지 시 캐시 갱신 (OnGUI 루프 내 QuestManager 직접 호출 방지)
            if (_needsRefresh)
                RefreshQuestList();

            float panelW = 900f;
            float panelH = 750f;
            float x = (Screen.width - panelW) / 2f;
            float y = (Screen.height - panelH) / 2f;

            Rect windowRect = new Rect(x, y, panelW, panelH);
            GUI.Box(windowRect, "");

            // Phase 33: 테마 데코레이션 (그라디언트 + 장식 테두리)
            DrawThemeDecorations(windowRect);

            // 타이틀
            GUI.Label(new Rect(x + 10, y + 5, panelW - 20, 42), "📋 퀘스트 목록", _styleTitle);

            // 통계 (캐시 사용)
            GUI.Label(new Rect(x + 10, y + 35, 400, 30), $"🔄 진행 중: {_cachedActive.Count}개", _styleLabel);
            GUI.Label(new Rect(x + 420, y + 35, 400, 30), $"✅ 완료: {_cachedCompleted.Count}개", _styleValue);

            // 퀘스트 목록
            float listY = y + 60;
            float listH = panelH - 100;
            float itemH = 120f;
            float totalH = (_cachedActive.Count + _cachedCompleted.Count) * itemH + 10;

            _scrollPos = GUI.BeginScrollView(new Rect(x + 10, listY, panelW - 20, listH), _scrollPos,
                new Rect(0, 0, panelW - 40, totalH));

            float cy = 5;

            // 진행 중 퀘스트 (캐시 사용)
            for (int i = 0; i < _cachedActive.Count; i++)
            {
                DrawQuestEntry(_cachedActive[i], cy, panelW - 60, QuestState.Active);
                cy += itemH;
            }

            // 완료 퀘스트 (캐시 사용)
            for (int i = 0; i < _cachedCompleted.Count; i++)
            {
                DrawQuestEntry(_cachedCompleted[i], cy, panelW - 60, QuestState.Completed);
                cy += itemH;
            }

            if (_cachedActive.Count == 0 && _cachedCompleted.Count == 0)
            {
                GUI.Label(new Rect(10, cy, panelW - 40, 30), "퀘스트가 없습니다. NPC를 찾아 퀘스트를 수락하세요.", _styleLabel);
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// 퀘스트 항목 하나를 IMGUI로 렌더링합니다.
        /// 모든 스타일은 캐시된 인스턴스를 사용하여 GC 할당을 방지합니다.
        /// </summary>
        private void DrawQuestEntry(QuestData quest, float y, float width, QuestState state)
        {
            GUI.Box(new Rect(0, y, width, 112), "");

            string stateStr = state == QuestState.Active ? "🔄 진행 중" : "✅ 완료";
            string colorHex = state == QuestState.Active ? "#FFDD44" : "#44FF44";

            GUI.Label(new Rect(10, y + 4, width - 20, 33), $"<color={colorHex}>{quest.questName}</color>  {stateStr}", _styleTitle);

            if (!string.IsNullOrEmpty(quest.description))
                GUI.Label(new Rect(10, y + 28, width - 20, 27), quest.description, _styleDesc);

            if (quest.objectives != null && quest.objectives.Count > 0)
            {
                var obj = quest.objectives[0];
                string prog = obj.requiredCount > 0 ? $" ({obj.currentCount}/{obj.requiredCount})" : "";
                GUI.Label(new Rect(10, y + 48, width - 20, 24), $"▸ {obj.description}{prog}", _styleObjective);
            }

            // 보상 표시
            string rewardStr = "";
            if (quest.reward.gold > 0) rewardStr += $"💰{quest.reward.gold} ";
            if (quest.reward.exp > 0) rewardStr += $"✨{quest.reward.exp}EXP";
            if (!string.IsNullOrEmpty(rewardStr))
                GUI.Label(new Rect(10, y + 64, width - 20, 21), rewardStr, _styleReward);
        }

        /// <summary>
        /// GUIStyle을 1회 생성 후 캐시합니다.
        /// OnGUI 내에서 new GUIStyle(...) 호출이 없도록 보장합니다.
        /// </summary>
        private void EnsureStyles()
        {
            if (_styleTitle != null) return;
            _styleTitle = new GUIStyle(GUI.skin.label) { fontSize = 60, fontStyle = FontStyle.Bold, richText = true, normal = { textColor = Color.white } };
            _styleLabel = new GUIStyle(GUI.skin.label) { fontSize = 52, normal = { textColor = Color.white } };
            _styleValue = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold, normal = { textColor = Color.green } };
            _styleDesc = new GUIStyle(GUI.skin.label) { fontSize = 48, normal = { textColor = Color.gray } };
            _styleObjective = new GUIStyle(GUI.skin.label) { fontSize = 44, normal = { textColor = Color.cyan } };
            _styleReward = new GUIStyle(GUI.skin.label) { fontSize = 40, normal = { textColor = Color.yellow } };
        }
    }
}