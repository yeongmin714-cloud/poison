using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI.Themes;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// C9-30: 퀘스트 윈도우 — QuestManager 연동, 진행 중/완료 퀘스트 표시
    /// Phase 39: QuestChainManager 연동, 활성 퀘스트 체인 표시, 선택지 UI 오픈, 완료 보상 UI
    /// </summary>
    public class QuestWindow : UIWindow
    {
        protected virtual void Start()
        {
            ApplyTheme(Phase33_Themes.CreateQuestTheme());
        }

        // ===== 스크롤 =====
        private Vector2 _scrollPos;

        // 캐시된 GUIStyle — GC-safe
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleDesc;
        private GUIStyle _styleObjective;
        private GUIStyle _styleReward;
        private GUIStyle _styleChain;
        private GUIStyle _styleChainTitle;
        private GUIStyle _styleChainNode;

        // 캐시된 퀘스트 리스트 — OnGUI GC 할당 방지
        private List<QuestData> _cachedActive;
        private List<QuestData> _cachedCompleted;
        private bool _needsRefresh = true;

        // Phase 39: 체인 데이터 캐시
        private List<KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>> _cachedActiveChains;
        private bool _needsChainRefresh = true;

        protected override void OnShow()
        {
            Debug.Log("[QuestWindow] 열림");
            base.OnShow(); // ★ 필수: 테마 배경 렌더링
            RefreshQuestList();
            RefreshChainList();
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

        /// <summary>
        /// Phase 39: 활성 퀘스트 체인 목록 갱신
        /// </summary>
        public void RefreshChainList()
        {
            _cachedActiveChains = new List<KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>>();
            var mgr = QuestChainManager.Instance;
            if (mgr != null)
            {
                foreach (var chainId in mgr.GetAllChainIds())
                {
                    var progress = mgr.GetChainProgress(chainId);
                    if (progress != null && progress.isActive)
                    {
                        var data = mgr.GetChainData(chainId);
                        if (data != null)
                            _cachedActiveChains.Add(new KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>(data, progress));
                    }
                }
            }
            _needsChainRefresh = false;
        }

        /// <summary>강제 재갱신 플래그 설정 (외부에서 퀘스트 변경 시 호출)</summary>
        public void MarkDirty()
        {
            _needsRefresh = true;
            _needsChainRefresh = true;
        }

        protected override void OnGUI()
        {
            if (!IsOpen) return;

            EnsureStyles();

            // 변경 감지 시 캐시 갱신
            if (_needsRefresh)
                RefreshQuestList();
            if (_needsChainRefresh)
                RefreshChainList();

            float panelW = 900f;
            float panelH = 750f;
            float x = (Screen.width - panelW) / 2f;
            float y = (Screen.height - panelH) / 2f;

            Rect windowRect = new Rect(x, y, panelW, panelH);
            GUI.Box(windowRect, "");

            // Phase 33: 테마 데코레이션
            DrawThemeDecorations(windowRect);

            // 타이틀
            GUI.Label(new Rect(x + 10, y + 5, panelW - 20, 42), "📋 퀘스트 목록", _styleTitle);

            // 통계
            GUI.Label(new Rect(x + 10, y + 35, 400, 30), $"🔄 진행 중: {_cachedActive.Count}개", _styleLabel);
            GUI.Label(new Rect(x + 420, y + 35, 400, 30), $"✅ 완료: {_cachedCompleted.Count}개", _styleValue);
            if (_cachedActiveChains.Count > 0)
            {
                GUI.Label(new Rect(x + 600, y + 35, 400, 30), $"🔗 활성 체인: {_cachedActiveChains.Count}개", _styleChain);
            }

            // Phase 39: QuestChoiceUI가 표시 중이면 OnGUI를 여기서 호출
            if (QuestChoiceUI.IsVisible)
            {
                QuestChoiceUI.OnChoiceGUI();
            }

            // 퀘스트 목록
            float listY = y + 60;
            float listH = panelH - 100;

            // 활성 체인 섹션 높이
            float chainSectionH = 0f;
            if (_cachedActiveChains.Count > 0)
            {
                chainSectionH = 60f + _cachedActiveChains.Count * 55f;
            }

            float questListH = listH - chainSectionH;
            float totalH = (_cachedActive.Count + _cachedCompleted.Count) * 120f + 10;

            _scrollPos = GUI.BeginScrollView(new Rect(x + 10, listY, panelW - 20, listH), _scrollPos,
                new Rect(0, 0, panelW - 40, totalH + chainSectionH));

            float cy = 5;

            // Phase 39: 활성 체인 섹션
            if (_cachedActiveChains.Count > 0)
            {
                DrawChainSection(cy, panelW - 60);
                cy += chainSectionH + 5;
            }

            // 진행 중 퀘스트
            for (int i = 0; i < _cachedActive.Count; i++)
            {
                DrawQuestEntry(_cachedActive[i], cy, panelW - 60, QuestState.Active);
                cy += 120f;
            }

            // 완료 퀘스트
            for (int i = 0; i < _cachedCompleted.Count; i++)
            {
                DrawQuestEntry(_cachedCompleted[i], cy, panelW - 60, QuestState.Completed);
                cy += 120f;
            }

            if (_cachedActive.Count == 0 && _cachedCompleted.Count == 0)
            {
                GUI.Label(new Rect(10, cy, panelW - 40, 30), "퀘스트가 없습니다. NPC를 찾아 퀘스트를 수락하세요.", _styleLabel);
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// Phase 39: 활성 퀘스트 체인 섹션 그리기
        /// </summary>
        private void DrawChainSection(float y, float width)
        {
            float sectionY = y;
            GUI.Box(new Rect(0, sectionY, width, 50 + _cachedActiveChains.Count * 55f), "");

            GUI.Label(new Rect(10, sectionY + 4, width - 20, 26), "🔗 활성 퀘스트 체인", _styleChainTitle);
            sectionY += 28f;

            for (int i = 0; i < _cachedActiveChains.Count; i++)
            {
                var kvp = _cachedActiveChains[i];
                var chainData = kvp.Key;
                var progress = kvp.Value;

                // 현재 노드 정보
                var currentNode = chainData.GetNode(progress.currentNodeId);
                string nodeTitle = !string.IsNullOrEmpty(currentNode.id) ? currentNode.title : "알 수 없음";
                string nodeDesc = !string.IsNullOrEmpty(currentNode.id) ? currentNode.description : "";

                // 진행률: 완료 노드 / 전체 노드
                string progressStr = chainData.nodes != null
                    ? $"{progress.completedNodeIds.Count}/{chainData.nodes.Length}"
                    : "0/0";

                // 체인 항목 박스
                GUI.Box(new Rect(5, sectionY, width - 10, 48), "");

                // 체인 제목
                GUI.Label(new Rect(12, sectionY + 2, width - 30, 22),
                    $"<color=#FFDD44>{chainData.chainTitle}</color>  [{progressStr}]", _styleChainTitle);

                // 현재 노드
                GUI.Label(new Rect(12, sectionY + 22, width - 100, 22),
                    $"▸ 현재: {nodeTitle}", _styleChainNode);

                // 보기 버튼 (open choices if node has choices)
                if (!string.IsNullOrEmpty(currentNode.id) && currentNode.choices != null && currentNode.choices.Length > 0)
                {
                    float btnX = width - 80;
                    if (GUI.Button(new Rect(btnX, sectionY + 8, 70, 35), "선택", _styleChain))
                    {
                        QuestChoiceUI.Show(progress.chainId, currentNode);
                    }
                }
                else if (!string.IsNullOrEmpty(currentNode.id))
                {
                    // 선택지 없으면 자동 진행 버튼
                    float btnX = width - 80;
                    if (GUI.Button(new Rect(btnX, sectionY + 8, 70, 35), "진행", _styleChain))
                    {
                        QuestChainManager.Instance.CompleteCurrentNode(progress.chainId);
                        _needsChainRefresh = true;
                        _needsRefresh = true;
                    }
                }

                sectionY += 53f;
            }
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

            // Phase 39: 체인 스타일
            _styleChain = new GUIStyle(GUI.skin.button) { fontSize = 38, normal = { textColor = Color.white }, hover = { textColor = Color.yellow } };
            _styleChainTitle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, richText = true, normal = { textColor = new Color(1f, 0.85f, 0.3f, 1f) } };
            _styleChainNode = new GUIStyle(GUI.skin.label) { fontSize = 38, normal = { textColor = Color.cyan } };
        }
    }
}