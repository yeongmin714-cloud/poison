// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;          // QuestManager (static), PlayerStats
using ProjectName.Core.Data;     // QuestData, QuestState, QuestObjective, QuestChainData
using ProjectName.Systems;       // QuestChainManager, QuestChainManager.ChainProgress

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round A-3 — 퀘스트 윈도우 포팅.
    /// 원본: Assets/Scripts/UI/QuestWindow.cs (304줄, IMGUI) — 본 파일은 그것의 데이터 경로만
    /// UTKWindowBase 파생으로 이식한 별도 파일. 원본은 절대 수정하지 않는다.
    ///
    /// [데이터 경로 — 원본 실측]
    ///  - 진행/완료 목록: QuestManager.GetActiveQuests() / GetCompletedQuests()  (원본 RefreshQuestList)
    ///  - 수락 목록:      QuestManager.GetAvailableQuests(PlayerStats.Instance.Level)
    ///  - 수락 버튼:       QuestManager.AcceptQuest(questId)
    ///  - 완료 버튼:       QuestManager.TryCompleteQuest(questId) — quest.AllObjectivesMet 일 때만 활성
    ///  - 활성 체인:       QuestChainManager.Instance.GetAllChainIds() → GetChainProgress(isActive) → GetChainData
    ///  - 체인 진행 버튼:  QuestChainManager.Instance.CompleteCurrentNode(chainId)
    ///  - 보상 요약:       QuestRewardPreview.GetRewardSummary(QuestData)  (ProjectName.UI)
    ///
    /// 폴링 400ms(schedule.Execute().Every) + Q 키 토글 / ESC 닫기 / 체인·장비 안전 구독 없음(정적 데이터 폴링).
    /// </summary>
    public class QuestWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static QuestWindowUTK _instance;
        public static QuestWindowUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            _instance = new QuestWindowUTK();
            var go = new GameObject("QuestWindowUTK");
            Object.DontDestroyOnLoad(go);
            var updater = go.AddComponent<Updater>();
            updater.window = _instance;
            Debug.Log("[QuestWindowUTK] 초기화 완료 — Q키로 토글");
        }

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance == null)
            {
                _instance = new QuestWindowUTK();
                var go = new GameObject("QuestWindowUTK_UPD");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Updater>().window = _instance;
            }
        }

        /// <summary>열기 (팩토리 겸용).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 =====
        private const float WinW = 760f;
        private const float WinH = 560f;
        private const long RefreshMs = 400L;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 퀘스트 목록 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(행/버튼)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트) — 진행/목표
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/보상/수락가능/골드
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선
            public static readonly Color Success  = Hex(0x3FB950);   // success 그린 — 완료

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        /// <summary>GitHub-dark 버튼 인라인 오버라이드(이 창 한정) — IStyle 쇼트핸드 없음 → 4면 개별 대입.</summary>
        private static void StyleButton(Button btn, UTKButton.Variant variant)
        {
            if (btn == null) return;
            Color baseBg, hoverBg, textColor;
            switch (variant)
            {
                case UTKButton.Variant.Primary:
                    baseBg = GitHubDark.Accent; hoverBg = new Color32(0x79, 0xC0, 0xFF, 0xFF); textColor = GitHubDark.Panel; break;
                case UTKButton.Variant.Danger:
                    baseBg = new Color32(0xF8, 0x51, 0x49, 0xFF); hoverBg = new Color32(0xDA, 0x36, 0x33, 0xFF); textColor = GitHubDark.TextMain; break;
                default:
                    baseBg = GitHubDark.PanelSub; hoverBg = GitHubDark.Stroke; textColor = GitHubDark.TextMain; break;
            }

            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = baseBg;
            btn.style.color = textColor;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(baseBg);
            btn.style.borderTopLeftRadius = 6f;
            btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f;
            btn.style.borderBottomRightRadius = 6f;

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverBg);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = baseBg);
        }

        /// <summary>창 크롬(본체/타이틀바/닫기버튼) GitHub-dark 리스타일 — 생성 시 1회.</summary>
        private void ApplyGitHubDarkStyle()
        {
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
            style.color = GitHubDark.TextMain;   // 명시색 없는 라벨 상속색 — 기본 텍스트

            var titleBar = this.Q("TitleBar");
            if (titleBar != null)
            {
                titleBar.style.backgroundColor = GitHubDark.PanelSub;
                titleBar.style.borderTopLeftRadius = 8f;
                titleBar.style.borderTopRightRadius = 8f;
                titleBar.style.borderBottomWidth = 1f;
                titleBar.style.borderBottomColor = GitHubDark.Stroke;
            }

            if (_titleLabel != null)
                _titleLabel.style.color = GitHubDark.TextMain;

            var closeBtn = this.Q<Button>("CloseButton");
            if (closeBtn != null)
            {
                closeBtn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                closeBtn.style.backgroundColor = GitHubDark.PanelSub;
                closeBtn.style.borderTopWidth = closeBtn.style.borderBottomWidth = closeBtn.style.borderLeftWidth = closeBtn.style.borderRightWidth = 0f;
                closeBtn.style.borderTopColor = closeBtn.style.borderBottomColor = closeBtn.style.borderLeftColor = closeBtn.style.borderRightColor = new StyleColor(GitHubDark.PanelSub);
                closeBtn.style.borderTopLeftRadius = 4f;
                closeBtn.style.borderTopRightRadius = 4f;
                closeBtn.style.borderBottomLeftRadius = 4f;
                closeBtn.style.borderBottomRightRadius = 4f;            // 작은배지 r4
                closeBtn.style.color = GitHubDark.TextMain;
            }
        }

        /// <summary>GitHub-dark 리스트 행(퀘스트/체인 박스) — 보조 패널 #21262D + 1px 스트로크 + r6 (이 창 한정).</summary>
        private static void ApplyDarkRowStyle(VisualElement row)
        {
            if (row == null) return;
            row.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            row.style.backgroundColor = GitHubDark.PanelSub;
            row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 1f;
            row.style.borderTopColor = row.style.borderBottomColor = row.style.borderLeftColor = row.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            row.style.borderTopLeftRadius = 6f;
            row.style.borderTopRightRadius = 6f;
            row.style.borderBottomLeftRadius = 6f;
            row.style.borderBottomRightRadius = 6f;   // 서브 반경 r6
        }

        // ===== 레퍼런스 =====
        private readonly ScrollView _list;
        private Label _statActive, _statCompleted, _statAvailable, _statChain;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private QuestWindowUTK() : base("📋 퀘스트 목록", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            // ── 통계 행 ──
            var statRow = new VisualElement();
            statRow.name = "StatRow";
            statRow.style.flexDirection = FlexDirection.Row;
            statRow.style.marginBottom = 4f;
            statRow.style.flexWrap = Wrap.Wrap;
            _content.Add(statRow);

            _statAvailable = MkStatLabel("🆕 수락 가능: 0");
            _statActive = MkStatLabel("🔄 진행 중: 0");
            _statCompleted = MkStatLabel("✅ 완료: 0");
            _statChain = MkStatLabel("🔗 활성 체인: 0");
            statRow.Add(_statAvailable);
            statRow.Add(_statActive);
            statRow.Add(_statCompleted);
            statRow.Add(_statChain);

            // ── 스크롤 목록 ──
            _list = new ScrollView { name = "QuestList" };
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 6f;
            _content.Add(_list);

            ApplyUIToolkitFont(this);
            ApplyGitHubDarkStyle();   // [GitHub-dark] 창 크롬 리스타일 — 이 창 한정 인라인
            style.display = DisplayStyle.None;
            style.left = 40f;
            style.top = 80f;
        }

        private static Label MkStatLabel(string text)
        {
            var l = new Label(text ?? "");
            l.style.width = 185f;
            l.style.fontSize = 14f;
            l.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 통계 라벨 — 기본 텍스트
            return l;
        }

        // =====================================================================
        //  생명주기
        // =====================================================================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[QuestWindowUTK] 퀘스트 창 열림 (키: Q)");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[QuestWindowUTK] 퀘스트 창 닫힘");
        }

        // =====================================================================
        //  폴링 루프 (schedule.Execute().Every, Pause 정지)
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshDisplay();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        // =====================================================================
        //  데이터 갱신 — (원본 실측 경로)
        // =====================================================================

        private void RefreshDisplay()
        {
            // ── 진행/완료/수락 ──
            List<QuestData> active = QuestManager.GetActiveQuests();
            List<QuestData> completed = QuestManager.GetCompletedQuests();
            List<QuestData> available = new List<QuestData>();
            if (PlayerStats.Instance != null)
                available = QuestManager.GetAvailableQuests(PlayerStats.Instance.Level);

            // ── 활성 체인 ──
            List<KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>> chains =
                new List<KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>>();
            var mgr = QuestChainManager.Instance;
            if (mgr != null)
            {
                List<string> ids = new List<string>(mgr.GetAllChainIds());
                for (int i = 0; i < ids.Count; i++)
                {
                    var progress = mgr.GetChainProgress(ids[i]);
                    if (progress != null && progress.isActive)
                    {
                        var data = mgr.GetChainData(ids[i]);
                        if (data != null)
                            chains.Add(new KeyValuePair<QuestChainData, QuestChainManager.ChainProgress>(data, progress));
                    }
                }
            }

            // 통계
            if (_statAvailable != null) _statAvailable.text = $"🆕 수락 가능: {available.Count}";
            if (_statActive != null) _statActive.text = $"🔄 진행 중: {active.Count}";
            if (_statCompleted != null) _statCompleted.text = $"✅ 완료: {completed.Count}";
            if (_statChain != null) _statChain.text = $"🔗 활성 체인: {chains.Count}";
            Debug.Log($"[QuestWindowUTK] 목록 갱신: 수락 {available.Count} / 진행 {active.Count} / 완료 {completed.Count} / 체인 {chains.Count}");

            // 목록 재조립
            _list.Clear();

            if (chains.Count > 0)
                _list.Add(BuildChainHeader());
            for (int i = 0; i < chains.Count; i++)
                _list.Add(BuildChainBox(chains[i]));

            for (int i = 0; i < available.Count; i++)
                _list.Add(BuildQuestBox(available[i], QuestState.Available));
            for (int i = 0; i < active.Count; i++)
                _list.Add(BuildQuestBox(active[i], QuestState.Active));
            for (int i = 0; i < completed.Count; i++)
                _list.Add(BuildQuestBox(completed[i], QuestState.Completed));

            if (available.Count == 0 && active.Count == 0 && completed.Count == 0 && chains.Count == 0)
            {
                var empty = MkLabel("퀘스트가 없습니다. NPC를 찾아 퀘스트를 수락하세요.", 15, GitHubDark.TextSub, TextAnchor.UpperLeft);   // [GitHub-dark] 보조 텍스트
                empty.style.flexGrow = 1f;
                _list.Add(empty);
            }
        }

        private VisualElement BuildChainHeader()
        {
            var h = MkLabel("🔗 활성 퀘스트 체인", 17, GitHubDark.Accent, TextAnchor.MiddleLeft);   // [GitHub-dark] 섹션 제목 — 액센트
            h.style.marginTop = 6f;
            h.style.marginBottom = 2f;
            return h;
        }

        private VisualElement BuildChainBox(KeyValuePair<QuestChainData, QuestChainManager.ChainProgress> kvp)
        {
            var chainData = kvp.Key;
            var progress = kvp.Value;

            var box = new VisualElement();
            box.AddToClassList("utk-slot");
            ApplyDarkRowStyle(box);   // [GitHub-dark] 체인 박스 = 보조 패널 리스트 아이템 (bg #21262D + 스트로크 + r6)
            box.style.flexDirection = FlexDirection.Column;
            box.style.marginTop = 3f;
            box.style.marginBottom = 3f;
            box.style.paddingTop = 5f;
            box.style.paddingBottom = 5f;

            string progressStr = chainData.nodes != null
                ? $"{progress.completedNodeIds.Count}/{chainData.nodes.Length}"
                : "0/0";
            var title = MkLabel($"{chainData.chainTitle}  [{progressStr}]", 15, GitHubDark.Accent, TextAnchor.MiddleLeft);   // [GitHub-dark] 체인명 — 액센트
            box.Add(title);

            string nodeTitle = "알 수 없음";
            string nodeDesc = "";
            bool hasChoices = false;
            var node = chainData.GetNode(progress.currentNodeId);
            if (!string.IsNullOrEmpty(node.id))
            {
                nodeTitle = string.IsNullOrEmpty(node.title) ? node.id : node.title;
                nodeDesc = string.IsNullOrEmpty(node.description) ? "" : node.description;
                hasChoices = node.choices != null && node.choices.Length > 0;
            }

            var desc = MkLabel($"▸ 현재: {nodeTitle}  {nodeDesc}", 13, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [GitHub-dark] 보조 텍스트
            desc.style.whiteSpace = WhiteSpace.Normal;
            box.Add(desc);

            string btnText = hasChoices ? "선택(자동진행)" : "진행";
            string chainId = progress.chainId;
            var btn = UTKButton.Create(btnText, () =>
            {
                if (hasChoices)
                {
                    Debug.LogWarning("[QuestWindowUTK] 선택지 체인 노드는 자동 진행(legacy QuestChoiceUI는 IMGUI)으로 처리.");
                }
                bool ok = QuestChainManager.Instance != null
                    && QuestChainManager.Instance.CompleteCurrentNode(chainId, -1);
                Debug.Log($"[QuestWindowUTK] 체인 노드 진행({chainId}, 자동): 성공={ok}");
                RefreshDisplay();
            }, UTKButton.Variant.Primary);
            StyleButton(btn, UTKButton.Variant.Primary);   // [GitHub-dark] 진행 버튼 인라인 리스타일
            btn.style.alignSelf = Align.FlexEnd;
            box.Add(btn);

            return box;
        }

        private VisualElement BuildQuestBox(QuestData quest, QuestState state)
        {
            var box = new VisualElement();
            box.AddToClassList("utk-slot");
            ApplyDarkRowStyle(box);   // [GitHub-dark] 퀘스트 박스 = 보조 패널 리스트 아이템 (bg #21262D + 스트로크 + r6)
            box.style.flexDirection = FlexDirection.Column;
            box.style.marginTop = 4f;
            box.style.marginBottom = 4f;
            box.style.paddingTop = 6f;
            box.style.paddingBottom = 6f;

            // 이름 + 상태
            string stateStr = state == QuestState.Active ? "🔄 진행 중"
                : state == QuestState.Completed ? "✅ 완료" : "🆕 수락 가능";
            // [GitHub-dark] 상태 배지 — 진행=액센트 / 완료=그린 / 수락가능=골드
            Color stateColor = state == QuestState.Active ? GitHubDark.Accent
                : state == QuestState.Completed ? GitHubDark.Success : GitHubDark.Gold;

            var row1 = new VisualElement();
            row1.style.flexDirection = FlexDirection.Row;
            row1.style.alignItems = Align.Center;
            var nameLabel = MkLabel(quest.questName, 16, GitHubDark.TextMain, TextAnchor.MiddleLeft);   // [GitHub-dark] 기본 텍스트
            nameLabel.style.flexGrow = 1f;
            row1.Add(nameLabel);
            var stateLabel = MkLabel(stateStr, 13, stateColor, TextAnchor.MiddleRight);
            stateLabel.style.width = 110f;
            row1.Add(stateLabel);
            box.Add(row1);
            Debug.Log($"[QuestWindowUTK] 퀘스트 항목: {quest.questName} ({quest.questId}) [{stateStr}]");

            // 설명
            if (!string.IsNullOrEmpty(quest.description))
            {
                var desc = MkLabel(quest.description, 13, GitHubDark.TextSub, TextAnchor.MiddleLeft);   // [GitHub-dark] 보조 텍스트
                desc.style.whiteSpace = WhiteSpace.Normal;
                box.Add(desc);
            }

            // 목표 1줄
            if (quest.objectives != null && quest.objectives.Count > 0)
            {
                var obj = quest.objectives[0];
                string prog = obj.requiredCount > 0 ? $" ({obj.currentCount}/{obj.requiredCount})" : "";
                var objLabel = MkLabel($"▸ {obj.description}{prog}", 13, GitHubDark.Accent, TextAnchor.MiddleLeft);   // [GitHub-dark] 목표 — 액센트
                objLabel.style.whiteSpace = WhiteSpace.Normal;
                box.Add(objLabel);
            }

            // 보상 요약
            string reward = QuestRewardPreview.GetRewardSummary(quest);
            if (!string.IsNullOrEmpty(reward))
            {
                var rewardLabel = MkLabel(reward, 13, GitHubDark.Gold, TextAnchor.MiddleLeft);   // [GitHub-dark] 보상 — 골드
                box.Add(rewardLabel);
            }

            // 수락 / 완료 버튼
            if (state == QuestState.Available)
            {
                string qid = quest.questId;
                var btn = UTKButton.Create("수락", () =>
                {
                    bool ok = QuestManager.AcceptQuest(qid);
                    Debug.Log($"[QuestWindowUTK] 퀘스트 수락 시도({qid}): 성공={ok}");
                    RefreshDisplay();
                }, UTKButton.Variant.Primary);
                StyleButton(btn, UTKButton.Variant.Primary);   // [GitHub-dark] 수락 버튼 인라인 리스타일
                btn.style.alignSelf = Align.FlexEnd;
                box.Add(btn);
            }
            else if (state == QuestState.Active)
            {
                string qid = quest.questId;
                bool completeable = quest.AllObjectivesMet;
                var btn = UTKButton.Create("완료", () =>
                {
                    bool ok = QuestManager.TryCompleteQuest(qid);
                    Debug.Log($"[QuestWindowUTK] 퀘스트 완료 시도({qid}): 성공={ok}");
                    RefreshDisplay();
                }, UTKButton.Variant.Danger);
                btn.SetEnabled(completeable);
                StyleButton(btn, UTKButton.Variant.Danger);   // [GitHub-dark] 완료 버튼 인라인 리스타일
                btn.style.alignSelf = Align.FlexEnd;
                box.Add(btn);
            }

            return box;
        }

        // =====================================================================
        //  키 토글 / ESC용 Updater — StatusWindowUTK 패턴
        // =====================================================================

        private class Updater : MonoBehaviour
        {
            public QuestWindowUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (kb.qKey.wasPressedThisFrame && window != null) if (window.IsOpen) window.Close(); else Toggle();;
                    if (kb.escapeKey.wasPressedThisFrame && window != null && window.IsOpen) window.Close();
                }
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.StopRefreshLoop();
                    window.RemoveFromHierarchy();
                }
            }
        }

        // =====================================================================
        //  내부 헬퍼
        // =====================================================================

        private static Label MkLabel(string text, float size, Color color, TextAnchor align)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            l.style.unityTextAlign = align;
            return l;
        }
    }
}