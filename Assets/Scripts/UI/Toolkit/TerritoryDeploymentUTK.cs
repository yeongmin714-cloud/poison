// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round A-2 — 영지 병사 배치 윈도우 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/TerritoryDeploymentUI.cs (189줄) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 배치 버튼 5종 (사냥/공격동행/수비문지기/채집/농경) + 특사/해제
    ///     → GuardTaskSystem.AssignTerritoryTask 실측 API 직접 호출 (복제 금지).
    ///  ② 특사 = 대상 영지 pick 모드 (원본 _pickingEnvoy 로직 차용 — GetAttackTargets 재사용).
    ///  ③ 해제 = AssignTerritoryTask(id, GuardTask.None) — 역할 해제 (원본 ⏹️ 해제 대응).
    ///  ④ 250ms 폴링 갱신 (배치 후 병사 수 / garrisonRole 실시간 반영).
    ///  각 경로에 [DeployUTK] Debug.Log 실측 로그.
    /// [진입점] static Open() / Ensure() / Toggle(). 순수 VisualElement 트리.
    /// </summary>
    public class TerritoryDeploymentUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static TerritoryDeploymentUTK _instance;
        public static TerritoryDeploymentUTK Instance => _instance;

        /// <summary>팩토리 — UIRoot 좌상단 배치. 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new TerritoryDeploymentUTK();
        }

        /// <summary>배치 패널 열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
        }

        // ===== 설정 =====
        private const float WinW = 460f;
        private const float WinH = 560f;
        private const long RefreshMs = 250L;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 영지 배치 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(행/버튼)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트)
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선

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

        /// <summary>GitHub-dark 리스트 행(영지 항목) — 보조 패널 #21262D + 1px 스트로크 + r6 (이 창 한정).</summary>
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
        private readonly VisualElement _list;
        private Label _summaryLabel;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        // 특사(정보원) 대상 선택 상태 — 원본 _pickingEnvoy 로직 차용.
        private bool _pickingEnvoy = false;
        private TerritoryId _pickSourceId;

        private TerritoryDeploymentUTK() : base("영지 병사 배치", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _summaryLabel = new Label("⚔️ 영지 병사 배치");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 18f;
            _summaryLabel.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 요약 제목 — 기본 텍스트
            _content.Add(_summaryLabel);

            _list = new VisualElement();
            _list.name = "TerritoryDeployList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);
            ApplyGitHubDarkStyle();   // [GitHub-dark] 창 크롬 리스타일 — 이 창 한정 인라인

            // 기본 숨김 + 위치
            style.display = DisplayStyle.None;
            style.left = 16f;
            style.top = 96f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 16f;
            style.top = 96f;
            StartRefreshLoop();
            RefreshList();
            Debug.Log("[DeployUTK] 영지 배치 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[DeployUTK] 영지 배치 창 닫힘");
        }

        // ===== 폴링 갱신 =====

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshList();
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

        // ===== 목록 갱신 (매 갱신 재조회) =====

        private void RefreshList()
        {
            _list.Clear();

            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                _list.Add(MakeLabel("(TerritoryDatabase 없음)", UTKColor.TextSecondary));
                return;
            }

            var playerTerrs = GetPlayerTerritories(db);
            if (playerTerrs.Count == 0)
            {
                _list.Add(MakeLabel("점령한 영지가 없습니다.", UTKColor.TextSecondary));
                return;
            }

            _summaryLabel.text = $"⚔️ 영지 병사 배치 — 점령 영지 {playerTerrs.Count}곳";

            foreach (var def in playerTerrs)
            {
                TerritoryState st = db.GetState(def.id);
                int live = GuardManager.Instance != null
                    ? GuardManager.Instance.GetGuardsInTerritory(def.id).Count
                    : 0;
                int count = live > 0 ? live : def.guardCount;
                string role = st != null ? st.garrisonRole.ToString() : "None";
                string seen = def.id.ToString();

                var row = new VisualElement();
                row.name = "TerrRow_" + seen;
                ApplyDarkRowStyle(row);   // [GitHub-dark] 영지 행 = 보조 패널 리스트 아이템 (bg #21262D + 스트로크 + r6)
                row.style.flexDirection = FlexDirection.Column;
                row.style.paddingLeft = 6f;   // 다크 행 배경 내 여백 (가시 전용)
                row.style.paddingRight = 6f;
                row.style.paddingTop = 4f;
                row.style.paddingBottom = 6f;

                row.Add(MakeLabel($"◆ {def.territoryName}  [병사 {count} · {role}]", GitHubDark.TextMain));   // [GitHub-dark] 기본 텍스트

                // ── 역할 버튼 세로: 2개 행에 나눔 (행당 4개) ──
                AddButtonRow(row,
                    ("🏹 사냥", () => AssignRole(def.id, seen, GuardTaskSystem.GuardTask.Hunt)),
                    ("⚔️ 동행", () => AssignRole(def.id, seen, GuardTaskSystem.GuardTask.Attack)),
                    ("🛡️ 수비", () => AssignRole(def.id, seen, GuardTaskSystem.GuardTask.Defend)),
                    ("🌿 채집", () => AssignRole(def.id, seen, GuardTaskSystem.GuardTask.Gather)));
                AddButtonRow(row,
                    ("🌾 농경", () => AssignRole(def.id, seen, GuardTaskSystem.GuardTask.Farm)),
                    ("🕵️ 특사", () =>
                    {
                        _pickingEnvoy = true;
                        _pickSourceId = def.id;
                        RefreshList();
                    }),
                    ("⏹️ 해제", () => AssignRole(def.id, seen, GuardTaskSystem.GuardTask.None)),
                    ("ℹ️ 상세", () =>
                    {
                        TerritoryInfoPopupUTK.Open(def.id);
                        Debug.Log($"[DeployUTK] ℹ️ 영지 상세 열기 → {seen}");
                    }));

                // ── 특사 대상 선택 (원본 _pickingEnvoy 차용) ──
                if (_pickingEnvoy && _pickSourceId.Equals(def.id))
                {
                    var targets = GetAttackTargets(def.id);
                    if (targets.Count == 0)
                        row.Add(MakeLabel("(대상 영지 없음)", GitHubDark.TextSub));   // [GitHub-dark] 보조 텍스트
                    else
                    {
                        row.Add(MakeLabel("🕵️ 특사 대상 영지 선택:", GitHubDark.TextSub));   // [GitHub-dark] 보조 텍스트
                        foreach (var targetDef in targets)
                        {
                            var tBtn = UTKButton.Create(
                                $"→ {targetDef.territoryName} ({targetDef.id})",
                                () =>
                                {
                                    AssignRole(def.id, seen, GuardTaskSystem.GuardTask.Envoy);
                                    _pickingEnvoy = false;
                                    RefreshList();
                                },
                                UTKButton.Variant.Primary);
                            StyleButton(tBtn, UTKButton.Variant.Primary);   // [GitHub-dark] 대상 버튼 인라인 리스타일
                            row.Add(tBtn);
                        }
                        row.Add(UTKButton.Create("선택 취소", () =>
                        {
                            _pickingEnvoy = false;
                            RefreshList();
                        }, UTKButton.Variant.Secondary));
                    }
                }

                _list.Add(row);
            }
        }

        // ===== 배치 =====

        /// <summary>GuardTaskSystem.AssignTerritoryTask 실측 API 직접 호출 (원본 AssignTerritoryRole 대응).</summary>
        private void AssignRole(TerritoryId id, string seen, GuardTaskSystem.GuardTask task)
        {
            GuardTaskSystem system = GuardTaskSystem.Instance != null
                ? GuardTaskSystem.Instance
                : GuardTaskSystem.Ensure();
            if (system == null)
            {
                Debug.LogWarning("[DeployUTK] GuardTaskSystem 미생성 — 역할 배정 실패");
                return;
            }
            int n = system.AssignTerritoryTask(id, task);
            Debug.Log($"[DeployUTK] 영지 {seen} → {task} ({n}명 배정)");
            RefreshList();
        }

        // ===== 헬퍼 =====

        private static List<TerritoryDefinition> GetPlayerTerritories(TerritoryDatabase db)
        {
            var result = new List<TerritoryDefinition>();
            foreach (var def in db.GetAllDefinitions())
            {
                var st = db.GetState(def.id);
                if (st != null && st.ownership == TerritoryOwnership.PlayerOwned)
                    result.Add(def);
            }
            return result;
        }

        /// <summary>공격/특사 대상 후보 — 자기 자신 제외, 최대 8개 (원본 GetAttackTargets 차용).</summary>
        private static List<TerritoryDefinition> GetAttackTargets(TerritoryId sourceId)
        {
            var db = TerritoryDatabase.Instance;
            var result = new List<TerritoryDefinition>();
            if (db == null) return result;
            foreach (var def in db.GetAllDefinitions())
            {
                if (def.id.Equals(sourceId)) continue;
                result.Add(def);
            }
            return result.Count > 8 ? result.GetRange(0, 8) : result;
        }

        private static Label MakeLabel(string text, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = 13f;
            l.style.color = new StyleColor(color);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        private static void AddButtonRow(VisualElement parent, params (string label, System.Action onClick)[] buttons)
        {
            var rowEl = new VisualElement();
            rowEl.style.flexDirection = FlexDirection.Row;
            rowEl.style.flexWrap = Wrap.Wrap;
            rowEl.style.marginTop = 3f;
            foreach (var (label, onClick) in buttons)
            {
                var b = UTKButton.Create(label, onClick, UTKButton.Variant.Secondary);
                b.style.flexGrow = 1f;
                b.style.fontSize = 12f;
                rowEl.Add(b);
            }
            parent.Add(rowEl);
        }
    }
}