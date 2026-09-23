// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory, PlayerStats, RecipeDiscoverySystem
using ProjectName.Core.Data;       // HerbComboDatabase, HerbDatabase, HerbComboResult
using ProjectName.Systems;         // CraftPresetManager, CraftSuccessSystem, CraftResult, CraftType, SoundManager
using ProjectName.UI;              // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U3 Round B — 크래프트 테이블 창.
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/CraftingUI.cs (IMGUI, 1108줄) + PresetNamePopup.cs (221줄).
    /// 원본은 절대 수정하지 않는다 — 본 파일은 동일 게임 데이터 소스를 직접 호출한다.
    ///
    /// [포팅 범위]
    ///   ① 레시피 북 (좌)   — HerbComboDatabase.AllCombos 중 RecipeDiscoverySystem으로 발견된 조합 + 즐겨찾기 필터.
    ///   ② 레시피 상세 (우) — 선택 레시피의 재료 요구(2종) + 보유 수량 표시.
    ///   ③ 제작             — CraftSuccessSystem.ExecuteCraft 직접 호출 (원본 제작 데이터 경로): 성공/실패 분배,
    ///                        재료 차감·결과물 지급·RecipeDiscoverySystem.MarkDiscovered·경험치·사운드.
    ///   ④ 결과 표시        — UTKToastService 토스트 (원본 CraftResultPopup은 후속 라운드).
    ///   ⑤ 프리셋           — CraftPresetManager 직접 호출 (저장/불러오기/삭제), 즐겨찾기(★) 토글.
    ///                        프리셋 이름 입력은 PresetNamePopup 파일을 만들지 않고 본 클래스 인라인 모달(TextField)로 처리.
    ///   ⑥ 재료 변화 폴링    — schedule.Execute().Every(250ms) → IVisualElementScheduledItem.Pause 로 정지 (낙하 규약 준수).
    ///   ⑦ static Open / Ensure 진입점.
    /// 각 동작에 [CraftUTK] 디버그 로그.
    /// </summary>
    public class CraftingWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static CraftingWindowUTK _instance;
        public static CraftingWindowUTK Instance => _instance;

        /// <summary>팩토리 — UIRoot 부착 + 생성. 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[CraftUTK] UI Toolkit 루트 없음 — Ensure 지연 (부트스트랩 후 재시도).");
                return;
            }
            _instance = new CraftingWindowUTK();
            root.Add(_instance);
        }

        /// <summary>크래프트 창 열기 (팩토리 겸용).</summary>
        public static void Open()
        {
            Ensure();
            if (_instance != null) _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 =====
        private const float WinW = 1060f;
        private const float WinH = 660f;
        private const long RefreshMs = 250L;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 이 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·UTKWindowBase·타 UTK 창은 절대 수정하지 않는다.
        //  우드 배경 이미지 제거 → 다크 #161B22 패널 + 보조 #21262D + 스트로크 #2E343D.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);   // 최배경
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(타이틀바/버튼/행)
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트) — 선택/주요 버튼
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/활성/골드
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선
            public static readonly Color Danger   = Hex(0xF85149);   // danger 버튼(GitHub-dark danger 토큰)

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        /// <summary>GitHub-dark 버튼 인라인 오버라이드(이 창 한정) — Theme bg_button.png/브론즈 베벨 대체.
        ///   IStyle에 borderWidth 쇼트핸드가 없어 4면 개별 대입한다. 호버는 인라인 배경이 USS :hover를
        ///   가리므로 진입/이탈 콜백으로 밝기 계층(액센트/화이트 오버레이)만 토글.</summary>
        private static void StyleButton(Button btn, UTKButton.Variant variant)
        {
            if (btn == null) return;
            Color baseBg, hoverBg, textColor;
            switch (variant)
            {
                case UTKButton.Variant.Primary:
                    baseBg = GitHubDark.Accent; hoverBg = Hex(0x79C0FF); textColor = GitHubDark.BgBase; break;
                case UTKButton.Variant.Danger:
                    baseBg = GitHubDark.Danger; hoverBg = Hex(0xDA3633); textColor = GitHubDark.TextMain; break;
                default:
                    baseBg = GitHubDark.PanelSub; hoverBg = GitHubDark.Stroke; textColor = GitHubDark.TextMain; break;
            }

            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);   // 우드 베이크 이미지 제거
            btn.style.backgroundColor = baseBg;
            btn.style.color = textColor;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(baseBg);
            btn.style.borderTopLeftRadius = 6f;
            btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f;
            btn.style.borderBottomRightRadius = 6f;   // 서브 반경 r6

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverBg);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = baseBg);
        }

        private static Color Hex(uint rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

        /// <summary>창 크롬(본체/타이틀바/닫기버튼) GitHub-dark 리스타일 — 생성 시 1회.</summary>
        private void ApplyGitHubDarkStyle()
        {
            // 창 본체: bg_window.png/브론즈 베벨 2px → 다크 패널 + 1px 스트로크 + r8 (이 창에서만)
            style.backgroundColor = GitHubDark.Panel;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = GitHubDark.Stroke;
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
            style.color = GitHubDark.TextMain;

            // 타이틀 바: 보조 패널 + 하단 1px 스트로크 (상단 코너 r8 — 창 클리핑 정합)
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

            // 닫기 버튼: 보조 패널 바탕 + r4(작은배지)
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
                closeBtn.style.borderBottomRightRadius = 4f;
                closeBtn.style.color = GitHubDark.TextMain;
            }
        }

        // ===== 선택 레시피 표현 =====
        private sealed class RecipeEntry
        {
            public string resultName;
            public string effect;
            public string resultId;
            public string ingIdA;      // 재료1 id (GAME_DATA 허브 id)
            public string ingIdB;      // 재료2 id
            public string ingNameA;    // 재료1 표시 이름
            public string ingNameB;    // 재료2 표시 이름
        }

        private readonly List<RecipeEntry> _allRecipes = new List<RecipeEntry>();
        private readonly List<RecipeButton> _recipeButtons = new List<RecipeButton>();
        private RecipeEntry _selected;
        private bool _favoritesOnly;

        // ===== UI 노드 =====
        private ScrollView _recipeList;
        private Label _detailName;
        private Label _detailEffect;
        private Label _ingA;
        private Label _ingB;
        private Button _craftBtn;
        private Label _footerLabel;
        private VisualElement _presetDropPanel;

        // 프리셋 드롭다운 항목 버튼 (이름 → 삭제 핸들러)
        private readonly List<VisualElement> _presetRowCache = new List<VisualElement>();

        // ===== 폴링 =====
        private IVisualElementScheduledItem _refreshTask;

        // ===== 프리셋 이름 인라인 모달 =====
        private VisualElement _nameModal;
        private TextField _nameField;

        private CraftingWindowUTK() : base("🧪 크래프트 테이블", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            BuildTopBar();
            BuildMainSplit();
            BuildFooter();

            ApplyUIToolkitFont(this);
            // [Figma GitHub-dark 리스타일] 다크 창 크롬 — 시각 전용, 기능 경로 무관(테스트 범위: 이 창 한정)
            ApplyGitHubDarkStyle();
            RefreshRecipeData();
            RefreshDisplay();

            style.display = DisplayStyle.None;
            style.left = 10f;
            style.top = 80f;
            Debug.Log("[CraftUTK] 크래프트 창 생성됨");
        }

        // =====================================================================
        //  콘텐츠 빌드
        // =====================================================================

        private void BuildTopBar()
        {
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            top.style.marginBottom = 6f;

            var allBtn = UTKButton.Create("📜 전체 레시피", () => { _favoritesOnly = false; RefreshRecipeData(); RefreshDisplay(); }, UTKButton.Variant.Primary);
            allBtn.style.width = 130f;
            StyleButton(allBtn, UTKButton.Variant.Primary);
            top.Add(allBtn);

            var favBtn = UTKButton.Create("📌 즐겨찾기", () => { _favoritesOnly = true; RefreshRecipeData(); RefreshDisplay(); }, UTKButton.Variant.Secondary);
            favBtn.style.width = 130f;
            StyleButton(favBtn, UTKButton.Variant.Secondary);
            top.Add(favBtn);

            var sep = new Label("  |  ");
            sep.style.color = new StyleColor(GitHubDark.TextSub);
            top.Add(sep);

            var presetLoad = UTKButton.Create("📂 프리셋", TogglePresetDropdown, UTKButton.Variant.Secondary);
            presetLoad.style.width = 120f;
            StyleButton(presetLoad, UTKButton.Variant.Secondary);
            top.Add(presetLoad);

            _content.Add(top);
        }

        private void BuildMainSplit()
        {
            var split = new VisualElement();
            split.style.flexGrow = 1f;
            split.style.flexDirection = FlexDirection.Row;
            _content.Add(split);

            // ── 좌: 레시피 목록 ──
            var left = new VisualElement();
            left.style.width = 420f;
            left.style.flexShrink = 0;
            left.style.flexDirection = FlexDirection.Column;
            left.style.paddingRight = 14f;
            split.Add(left);

            var leftTitle = new Label("레시피 북");
            leftTitle.AddToClassList("utk-title-label");
            leftTitle.style.fontSize = 18f;
            leftTitle.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 섹션 제목 기본 텍스트
            left.Add(leftTitle);

            _recipeList = new ScrollView();
            _recipeList.style.flexGrow = 1f;
            left.Add(_recipeList);

            _presetDropPanel = new VisualElement();
            _presetDropPanel.style.flexDirection = FlexDirection.Column;
            _presetDropPanel.style.display = DisplayStyle.None;
            left.Add(_presetDropPanel);

            // ── 우: 선택 레시피 상세 ──
            var right = new VisualElement();
            right.style.flexGrow = 1f;
            right.style.flexDirection = FlexDirection.Column;
            right.style.paddingLeft = 6f;
            split.Add(right);

            var rightTitle = new Label("레시피 상세");
            rightTitle.AddToClassList("utk-title-label");
            rightTitle.style.fontSize = 18f;
            rightTitle.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 섹션 제목 기본 텍스트
            right.Add(rightTitle);

            _detailName = MkLabel("—", 24, GitHubDark.Accent, TextAnchor.MiddleLeft);   // [GitHub-dark] 레시피명 액센트
            right.Add(_detailName);

            _detailEffect = MkLabel("", 14, GitHubDark.TextSub, TextAnchor.UpperLeft);
            _detailEffect.style.whiteSpace = WhiteSpace.Normal;
            right.Add(_detailEffect);

            AddSeparator(right);

            var ingTitle = MkLabel("재료 요구 / 보유", 15, GitHubDark.TextMain, TextAnchor.MiddleLeft);
            ingTitle.style.marginTop = 4f;
            right.Add(ingTitle);

            _ingA = MkLabel("—", 16, GitHubDark.TextMain, TextAnchor.MiddleLeft);
            right.Add(_ingA);
            _ingB = MkLabel("—", 16, GitHubDark.TextMain, TextAnchor.MiddleLeft);
            right.Add(_ingB);

            _craftBtn = UTKButton.Create("⚒ 제작하기", ExecuteCraftForSelected, UTKButton.Variant.Primary);
            _craftBtn.style.height = 42f;
            _craftBtn.style.marginTop = 10f;
            StyleButton(_craftBtn, UTKButton.Variant.Primary);
            right.Add(_craftBtn);

            var presetRow = new VisualElement();
            presetRow.style.flexDirection = FlexDirection.Row;
            presetRow.style.marginTop = 8f;
            right.Add(presetRow);

            var presetInfo = MkLabel("선택 레시피를 프리셋으로 저장합니다.", 13, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            presetRow.Add(presetInfo);

            var saveBtn = UTKButton.Create("💾 프리셋 저장", OpenNameModal, UTKButton.Variant.Secondary);
            saveBtn.style.marginLeft = 8f;
            StyleButton(saveBtn, UTKButton.Variant.Secondary);
            presetRow.Add(saveBtn);
        }

        private void BuildFooter()
        {
            _footerLabel = MkLabel("", 13, GitHubDark.TextSub, TextAnchor.MiddleLeft);
            _footerLabel.style.marginTop = 6f;
            _content.Add(_footerLabel);
        }

        private static void AddSeparator(VisualElement parent)
        {
            var sep = new VisualElement();
            sep.style.height = 1f;
            sep.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
            sep.style.marginTop = 6f;
            sep.style.marginBottom = 6f;
            parent.Add(sep);
        }

        // =====================================================================
        //  생명주기 (UTKWindowBase 훅)
        // =====================================================================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 10f;
            style.top = 80f;
            RefreshRecipeData();
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[CraftUTK] 크래프트 테이블 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[CraftUTK] 크래프트 테이블 닫힘");
        }

        // =====================================================================
        //  ⑥ 재료 변화 폴링 — schedule.Execute().Every → Pause 정지
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) { RefreshRecipeData(); RefreshDisplay(); }
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
        //  레시피 데이터 — HerbComboDatabase + RecipeDiscoverySystem
        // =====================================================================

        /// <summary>조합 키("idA_idB", 알파벳 정렬)를 두 재료 id로 분해. 허브 id에 '_'가 없음을 전제.</summary>
        private static void SplitKey(string key, out string idA, out string idB)
        {
            string[] parts = key.Split('_');
            if (parts.Length >= 2)
            {
                idA = parts[0];
                idB = parts[1];
                return;
            }
            idA = "";
            idB = "";
        }

        private void RefreshRecipeData()
        {
            _allRecipes.Clear();

            var allCombos = HerbComboDatabase.AllCombos;
            var discovered = RecipeDiscoverySystem.GetAllDiscovered();
            var favorites = CraftPresetManager.Instance != null ? CraftPresetManager.Instance.GetFavorites() : null;
            var favSet = favorites != null ? new HashSet<string>(favorites) : new HashSet<string>();

            foreach (var kvp in allCombos)
            {
                string recipeId = kvp.Value.resultName;
                if (!discovered.Contains(recipeId))
                    continue;
                if (_favoritesOnly && !favSet.Contains(recipeId))
                    continue;

                var entry = new RecipeEntry
                {
                    resultName = recipeId,
                    effect = kvp.Value.effect,
                    resultId = kvp.Value.resultId,
                };

                SplitKey(kvp.Key, out string idA, out string idB);
                entry.ingIdA = idA;
                entry.ingIdB = idB;

                var infoA = HerbDatabase.GetHerbInfo(idA);
                var infoB = HerbDatabase.GetHerbInfo(idB);
                entry.ingNameA = string.IsNullOrEmpty(infoA.displayName) ? idA : infoA.displayName;
                entry.ingNameB = string.IsNullOrEmpty(infoB.displayName) ? idB : infoB.displayName;

                _allRecipes.Add(entry);
            }

            // 선택 유지
            if (_selected != null)
            {
                RecipeEntry keep = null;
                for (int i = 0; i < _allRecipes.Count; i++)
                {
                    if (_allRecipes[i].resultName == _selected.resultName)
                    {
                        keep = _allRecipes[i];
                        break;
                    }
                }
                _selected = keep ?? (_allRecipes.Count > 0 ? _allRecipes[0] : null);
            }
            else if (_allRecipes.Count > 0)
            {
                _selected = _allRecipes[0];
            }
        }

        private void _RebuildRecipeList()
        {
            _recipeList.Clear();
            _recipeButtons.Clear();

            if (_allRecipes.Count == 0)
            {
                string msg = _favoritesOnly
                    ? "즐겨찾기한 레시피가 없습니다. ★을 눌러 추가하세요."
                    : "아직 발견한 레시피가 없습니다.";
                var empty = MkLabel(msg, 14, GitHubDark.TextSub, TextAnchor.UpperLeft);
                empty.style.whiteSpace = WhiteSpace.Normal;
                _recipeList.Add(empty);
                return;
            }

            foreach (var entry in _allRecipes)
            {
                var btn = new RecipeButton(this, entry);
                _recipeButtons.Add(btn);
                _recipeList.Add(btn.Root);
            }

            RefreshButtonsSelection();
        }

        private void RefreshButtonsSelection()
        {
            foreach (var btn in _recipeButtons)
                btn.RefreshSelected();
        }

        // =====================================================================
        //  표시 갱신 (선택 강조 + 재료 보유 수량)
        // =====================================================================

        private void RefreshDisplay()
        {
            // 1) 레시피 목록 재생성 (필터/힐라이트 반영)
            _RebuildRecipeList();

            // 2) 상세
            if (_selected == null)
            {
                _detailName.text = "—";
                _detailEffect.text = "";
                _ingA.text = "—";
                _ingB.text = "—";
                _craftBtn.SetEnabled(false);
                _footerLabel.text = $"발견된 조합: {RecipeDiscoverySystem.DiscoveredCount:D2} / {HerbComboDatabase.AllCombos.Count:D2}  |  즐겨찾기: {(CraftPresetManager.Instance?.GetFavorites().Count ?? 0):D2}";
                return;
            }

            _detailName.text = _selected.resultName;
            _detailEffect.text = string.IsNullOrEmpty(_selected.effect) ? "" : $"효과: {_selected.effect}";

            int ownedA = CountOwnedByName(_selected.ingNameA);
            int ownedB = CountOwnedByName(_selected.ingNameB);

            _ingA.text = IngredientLine(_selected.ingNameA, 1, ownedA);
            _ingB.text = IngredientLine(_selected.ingNameB, 1, ownedB);

            bool canCraft = ownedA >= 1 && ownedB >= 1;
            _craftBtn.SetEnabled(canCraft);

            _footerLabel.text = $"발견된 조합: {RecipeDiscoverySystem.DiscoveredCount:D2} / {HerbComboDatabase.AllCombos.Count:D2}  |  즐겨찾기: {(CraftPresetManager.Instance?.GetFavorites().Count ?? 0):D2}";
        }

        private static string IngredientLine(string name, int need, int owned)
        {
            string status = owned >= need ? $"{owned}" : $"{owned} (부족!)";
            return $"{name}  ·  요구 {need} / 보유 {status}";
        }

        /// <summary>인벤토리에서 표시 이름 기준 보유 수량 합산 (원본 GetItemCount의 id 외 표시명 매칭 보조).</summary>
        private int CountOwnedByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return 0;
            var inv = PlayerInventory.Instance;
            if (inv == null) return 0;
            int total = 0;
            foreach (var slot in inv.GetAllSlots())
            {
                if (slot == null || slot.item == null || slot.count <= 0) continue;
                if (slot.item.displayName == displayName || slot.item.id == displayName)
                    total += slot.count;
            }
            return total;
        }

        private void SelectRecipe(RecipeEntry entry)
        {
            _selected = entry;
            RefreshDisplay();
            Debug.Log($"[CraftUTK] 레시피 선택: {entry?.resultName}");
        }

        // =====================================================================
        //  ③ 제작 — 원본 CraftingUI.TryCraft 데이터 경로 직접 호출
        // =====================================================================

        private void ExecuteCraftForSelected()
        {
            if (_selected == null) return;

            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                UTKToastService.Show("[CraftUTK] 인벤토리를 찾을 수 없습니다.");
                return;
            }

            // 재료(보유 인벤) 찾기 — 표시 이름 / id 매칭
            var itemA = FindInventoryItemByMatch(_selected.ingNameA, _selected.ingIdA);
            var itemB = FindInventoryItemByMatch(_selected.ingNameB, _selected.ingIdB);

            if (itemA == null || itemB == null)
            {
                UTKToastService.Show("재료가 부족합니다: " + _selected.resultName);
                return;
            }
            if (!inventory.HasItem(itemA.id) || !inventory.HasItem(itemB.id))
            {
                UTKToastService.Show("재료가 인벤토리에 없습니다.");
                return;
            }

            // 등급 추정 + 제작 판정 (연금술 = true)
            string grade1 = CraftSuccessSystem.GetGradeFromItemId(itemA.id);
            string grade2 = CraftSuccessSystem.GetGradeFromItemId(itemB.id);
            CraftResult result = CraftSuccessSystem.ExecuteCraft(true, grade1, grade2);

            switch (result)
            {
                case CraftResult.Success:
                    inventory.RemoveItem(itemA.id, 1);
                    inventory.RemoveItem(itemB.id, 1);

                    var resultItem = CreateResultItem(_selected);
                    if (resultItem != null)
                        inventory.AddItem(resultItem, 1);

                    RecipeDiscoverySystem.MarkDiscovered(_selected.resultName);

                    if (PlayerStats.Instance != null)
                        PlayerStats.Instance.AddExp(Random.Range(3, 11));

                    SoundManager.Instance?.PlaySFX("craft_success");

                    UTKToastService.Show($"🟢 제작 성공! '{_selected.resultName}' 획득!");
                    Debug.Log($"[CraftUTK] 조합 성공: {itemA.displayName} + {itemB.displayName} → {_selected.resultName}");
                    break;

                case CraftResult.Fail_MaterialPreserved:
                    SoundManager.Instance?.PlaySFX("craft_fail");
                    UTKToastService.Show("🟡 제작 실패... 재료가 보존되었다.");
                    Debug.Log($"[CraftUTK] 조합 실패 (재료보존): {itemA.displayName} + {itemB.displayName}");
                    break;

                case CraftResult.Fail_MaterialDestroyed:
                    bool destroyA = Random.value < 0.5f;
                    string destroyedId = destroyA ? itemA.id : itemB.id;
                    string destroyedName = destroyA ? itemA.displayName : itemB.displayName;
                    inventory.RemoveItem(destroyedId, 1);
                    SoundManager.Instance?.PlaySFX("craft_fail");
                    UTKToastService.Show($"🔴 제작 실패! '{destroyedName}'이(가) 소멸했다!");
                    Debug.Log($"[CraftUTK] 조합 실패 (재료소멸): {destroyedName} 파괴됨");
                    break;

                case CraftResult.Fail_Burned:
                    inventory.RemoveItem(itemA.id, 1);
                    inventory.RemoveItem(itemB.id, 1);
                    SoundManager.Instance?.PlaySFX("craft_fail");
                    UTKToastService.Show("💥 제작 실패! 모든 재료가 전소했다!");
                    Debug.Log($"[CraftUTK] 조합 실패 (전소): {itemA.displayName} + {itemB.displayName} 소멸");
                    break;
            }

            RefreshDisplay();
        }

        /// <summary>표시 이름 또는 id로 보유 인벤토리 아이템 검색.</summary>
        private PlayerInventory.ItemData FindInventoryItemByMatch(string displayName, string id)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return null;
            foreach (var slot in inv.GetAllSlots())
            {
                if (slot == null || slot.item == null || slot.count <= 0) continue;
                if (slot.item.id == id || slot.item.displayName == displayName)
                    return slot.item;
            }
            return null;
        }

        /// <summary>HerbComboResult → PlayerInventory.ItemData (원본 CreateResultItem 로직). 원본은 절대 수정하지 않는다.</summary>
        private PlayerInventory.ItemData CreateResultItem(RecipeEntry entry)
        {
            var category = PlayerInventory.ItemCategory.Potion;
            string name = entry.resultName;

            if (name.Contains("접착제") || name.Contains("코팅제") || name.Contains("도구") ||
                name.Contains("방패") || name.Contains("트랩") || name.Contains("용액"))
                category = PlayerInventory.ItemCategory.Material;
            else if (name.Contains("독") || name.Contains("맹독") || name.Contains("마비") ||
                     name.Contains("환각") || name.Contains("혼란") || name.Contains("수면"))
                category = PlayerInventory.ItemCategory.Potion;
            else if (name.Contains("치유") || name.Contains("회복") || name.Contains("해독") ||
                     name.Contains("생명") || name.Contains("치료"))
                category = PlayerInventory.ItemCategory.Potion;

            return new PlayerInventory.ItemData
            {
                id = $"combo_{name}",
                displayName = name,
                description = entry.effect,
                category = category,
                maxStack = 10
            };
        }

        // =====================================================================
        //  ⑤ 프리셋 — CraftPresetManager 직접 호출 + 인라인 이름 모달
        // =====================================================================

        private void TogglePresetDropdown()
        {
            bool open = _presetDropPanel.style.display == DisplayStyle.None;
            _presetDropPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open) RefreshPresetDropdown();
        }

        private void RefreshPresetDropdown()
        {
            // 이전 행 제거
            for (int i = 0; i < _presetRowCache.Count; i++)
            {
                var row = _presetRowCache[i];
                if (row != null && row.parent == _presetDropPanel)
                    row.RemoveFromHierarchy();
            }
            _presetRowCache.Clear();

            var mgr = CraftPresetManager.Instance;
            if (mgr == null) return;

            var presets = mgr.LoadPresets();
            foreach (var preset in presets)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 2f;

                var nameBtn = UTKButton.Create(preset.presetName, () => ApplyPresetByName(preset.presetName), UTKButton.Variant.Secondary);
                nameBtn.style.flexGrow = 1f;
                nameBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                StyleButton(nameBtn, UTKButton.Variant.Secondary);
                row.Add(nameBtn);

                var delBtn = UTKButton.Create("✕", () =>
                {
                    if (mgr != null) mgr.DeletePreset(preset.presetName);
                    RefreshPresetDropdown();
                    Debug.Log($"[CraftUTK] 프리셋 삭제: {preset.presetName}");
                }, UTKButton.Variant.Danger);
                delBtn.style.width = 40f;
                StyleButton(delBtn, UTKButton.Variant.Danger);
                row.Add(delBtn);

                _presetDropPanel.Add(row);
                _presetRowCache.Add(row);
            }
        }

        private void ApplyPresetByName(string presetName)
        {
            var mgr = CraftPresetManager.Instance;
            if (mgr == null) return;
            var presetOpt = mgr.GetPreset(presetName);
            if (!presetOpt.HasValue)
            {
                Debug.LogWarning($"[CraftUTK] 프리셋 '{presetName}'을 찾을 수 없습니다.");
                return;
            }
            var preset = presetOpt.Value;

            // resultId "combo_{resultName}" → resultName 복원해 선택
            string target = preset.resultId;
            if (!string.IsNullOrEmpty(target) && target.StartsWith("combo_"))
                target = target.Substring("combo_".Length);

            RecipeEntry found = null;
            for (int i = 0; i < _allRecipes.Count; i++)
            {
                if (_allRecipes[i].resultName == target)
                {
                    found = _allRecipes[i];
                    break;
                }
            }

            if (found != null)
            {
                _selected = found;
                RefreshDisplay();
                Debug.Log($"[CraftUTK] 프리셋 적용: {presetName} → {found.resultName}");
            }
            else
            {
                Debug.Log($"[CraftUTK] 프리셋 적용 실패(레시피 미발견): {presetName} → {target}");
                UTKToastService.Show($"프리셋 '{presetName}'의 레시피가 발견되지 않았습니다.");
            }
        }

        private void OpenNameModal()
        {
            if (_selected == null) return;

            var mgr = CraftPresetManager.Instance;
            if (mgr == null)
            {
                UTKToastService.Show("프리셋 시스템을 찾을 수 없습니다.");
                return;
            }

            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;

            // 이름 모달 (인라인 — PresetNamePopup 파일 미사용)
            _nameModal = new VisualElement();
            _nameModal.AddToClassList("utk-modal");
            _nameModal.style.left = 40f;
            _nameModal.style.top = 40f;
            _nameModal.style.minWidth = 300f;

            var title = new Label("프리셋 이름 입력");
            title.AddToClassList("utk-title-label");
            title.style.fontSize = 18f;
            title.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 모달 제목 기본 텍스트
            _nameModal.Add(title);

            // [GitHub-dark] 모달 박스: 다크 패널 + 1px 스트로크 + r8 (utk-modal 우드 배경 대체)
            _nameModal.style.backgroundColor = GitHubDark.Panel;
            _nameModal.style.borderTopWidth = _nameModal.style.borderBottomWidth = _nameModal.style.borderLeftWidth = _nameModal.style.borderRightWidth = 1f;
            _nameModal.style.borderTopColor = _nameModal.style.borderBottomColor = _nameModal.style.borderLeftColor = _nameModal.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            _nameModal.style.borderTopLeftRadius = 8f;
            _nameModal.style.borderTopRightRadius = 8f;
            _nameModal.style.borderBottomLeftRadius = 8f;
            _nameModal.style.borderBottomRightRadius = 8f;

            _nameField = new TextField("") { value = _selected.resultName };
            _nameField.style.marginTop = 8f;
            _nameField.style.marginBottom = 8f;
            _nameModal.Add(_nameField);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;

            var save = UTKButton.Create("저장", () =>
            {
                string name = (_nameField?.value ?? "").Trim();
                if (string.IsNullOrEmpty(name))
                {
                    Debug.LogWarning("[CraftUTK] 프리셋 이름이 비어있습니다.");
                    return;
                }
                var ingredientIds = new List<string>();
                if (!string.IsNullOrEmpty(_selected.ingIdA)) ingredientIds.Add(_selected.ingIdA);
                if (!string.IsNullOrEmpty(_selected.ingIdB)) ingredientIds.Add(_selected.ingIdB);
                // 원본 관례: resultId = "combo_{resultName}" (CraftingUI.SavePreset와 동일). ApplyPresetByName이 이로 복원.
                string resultId = "combo_" + _selected.resultName;
                mgr.SavePreset(name, ingredientIds, resultId, CraftType.Alchemy);
                CloseNameModal();
                RefreshPresetDropdown();
                RefreshDisplay();
                Debug.Log($"[CraftUTK] 프리셋 저장: {name} (재료 {ingredientIds.Count}개, 결과: {resultId})");
            }, UTKButton.Variant.Primary);
            StyleButton(save, UTKButton.Variant.Primary);
            btnRow.Add(save);

            var cancel = UTKButton.Create("취소", () => CloseNameModal(), UTKButton.Variant.Secondary);
            cancel.style.marginLeft = 6f;
            StyleButton(cancel, UTKButton.Variant.Secondary);
            btnRow.Add(cancel);

            _nameModal.Add(btnRow);
            UTKWindowBase.ApplyUIToolkitFont(_nameModal);
            root.Add(_nameModal);
            _nameModal.BringToFront();

            _nameField.Focus();
        }

        private void CloseNameModal()
        {
            if (_nameModal != null && _nameModal.parent != null)
                _nameModal.RemoveFromHierarchy();
            _nameModal = null;
            _nameField = null;
        }

        // =====================================================================
        //  레시피 행 버튼 (이름 + ★ 즐겨찾기)
        // =====================================================================

        private sealed class RecipeButton
        {
            private readonly CraftingWindowUTK _owner;
            private readonly RecipeEntry _entry;
            private readonly Button _favBtn;

            public VisualElement Root { get; }

            public RecipeButton(CraftingWindowUTK owner, RecipeEntry entry)
            {
                _owner = owner;
                _entry = entry;

                Root = new VisualElement();
                Root.style.flexDirection = FlexDirection.Row;
                Root.style.alignItems = Align.Center;
                Root.style.marginBottom = 2f;
                Root.style.paddingTop = 2f;
                Root.style.paddingBottom = 2f;
                Root.style.paddingLeft = 4f;
                Root.style.paddingRight = 4f;
                // [GitHub-dark] 레시피 행 = 보조 패널 리스트 아이템 (bg #21262D + 1px 스트로크 + r6)
                Root.style.backgroundColor = GitHubDark.PanelSub;
                Root.style.borderTopWidth = Root.style.borderBottomWidth = Root.style.borderLeftWidth = Root.style.borderRightWidth = 1f;
                Root.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
                Root.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
                Root.style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
                Root.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
                Root.style.borderTopLeftRadius = 6f;
                Root.style.borderTopRightRadius = 6f;
                Root.style.borderBottomLeftRadius = 6f;
                Root.style.borderBottomRightRadius = 6f;
                Root.style.marginBottom = 4f;

                var nameBtn = UTKButton.Create(entry.resultName, () => owner.SelectRecipe(entry), UTKButton.Variant.Secondary);
                nameBtn.style.flexGrow = 1f;
                nameBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                StyleButton(nameBtn, UTKButton.Variant.Secondary);
                Root.Add(nameBtn);

                _favBtn = UTKButton.Create("★", ToggleFav, UTKButton.Variant.Secondary);
                _favBtn.style.width = 38f;
                StyleButton(_favBtn, UTKButton.Variant.Secondary);
                Root.Add(_favBtn);
            }

            private void ToggleFav()
            {
                var mgr = CraftPresetManager.Instance;
                if (mgr == null) return;
                mgr.ToggleFavorite(_entry.resultName);
                bool isFav = mgr.IsFavorite(_entry.resultName);
                _favBtn.text = isFav ? "★" : "☆";
                _favBtn.style.color = new StyleColor(isFav ? GitHubDark.Gold : GitHubDark.TextSub);   // [GitHub-dark] 활성 즐겨찾기=금색
                Debug.Log($"[CraftUTK] 즐겨찾기 토글: {_entry.resultName} → {(isFav ? "★" : "☆")}");
                _owner.RefreshDisplay();
            }

            public void RefreshSelected()
            {
                bool isSel = _owner._selected != null && _owner._selected.resultName == _entry.resultName;
                var mgr = CraftPresetManager.Instance;
                bool isFav = mgr != null && mgr.IsFavorite(_entry.resultName);

                _favBtn.text = isFav ? "★" : "☆";
                _favBtn.style.color = new StyleColor(isFav ? GitHubDark.Gold : GitHubDark.TextSub);   // [GitHub-dark] 활성 즐겨찾기=금색

                // 선택 행 강조 (테두리/글자색)
                Root.RemoveFromClassList("utk-selected-row");
                if (isSel)
                    Root.AddToClassList("utk-selected-row");

                // [GitHub-dark] 선택 행 인라인 강조 — 액센트 링 + 반투명 화이트 0.08 오버레이
                //   (utk-selected-row의 골드 USS는 인라인 배경에 가려지므로 시각을 여기서 통일)
                if (isSel)
                {
                    Root.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
                    Root.style.borderTopColor = new StyleColor(GitHubDark.Accent);
                    Root.style.borderBottomColor = new StyleColor(GitHubDark.Accent);
                    Root.style.borderLeftColor = new StyleColor(GitHubDark.Accent);
                    Root.style.borderRightColor = new StyleColor(GitHubDark.Accent);
                }
                else
                {
                    Root.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
                    Root.style.borderTopColor = new StyleColor(GitHubDark.Stroke);
                    Root.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
                    Root.style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
                    Root.style.borderRightColor = new StyleColor(GitHubDark.Stroke);
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