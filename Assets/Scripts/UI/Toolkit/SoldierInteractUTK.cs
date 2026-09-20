using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;      // PlayerInventory
using ProjectName.Systems;   // GuardPlaceholder, SoldierInteractBridge
using ProjectName.UI;        // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P13 — 병사 통합 상호작용 창 (UI Toolkit).
    /// 원본: GuardPlaceholder.OnGUI 상호작용 패널 + DrawItemSelectionPopup (IMGUI) — 철거 후 이관.
    ///
    /// [진입점]
    ///  F키 → Systems(PlayerMovement) → SoldierInteractBridge.RaiseInteract(guard)
    ///       → 본 창 Open (Systems→UI 순환참조 회피 이벤트 경유 — SoldierInteractBridge 동일 패턴).
    ///  "📋 병사 정보보기" 버튼 → SoldierInteractBridge.Raise(guard) → GuardInfoUTK 정보창 (기존 경로 유지).
    ///
    /// [메뉴] 🗣️ 말걸기 / 🥩 음식주기 / 💊 약주기 / 🤝 포섭 / 📋 병사 정보보기 / 🔙 닫기
    ///  - 말걸기/포섭: GuardPlaceholder public 래퍼(BeginTalk/BeginRecruit) → 기존 OnTalk/OnRecruit 로직 재사용.
    ///  - 음식/약: 아이템 선택 팝업을 UTK 스크롤 리스트로 재구현 (IMGUI DrawItemSelectionPopup 대체).
    ///    GuardPlaceholder.BeginFoodSelection/BeginDrugSelection → GetInventoryItemsByMode() 필터 재사용
    ///    → GiveFood/GiveDrug 지급(기존 GiveItemToGuard 경로, 데이터/규칙 불변).
    ///
    /// [자동 닫기] 200ms 자체 폴링(UTKWindowManager.Updater 미사용 — schedule.Execute):
    ///  대상 null / 사망 / 비활성 / 플레이어 거리 > 4.5m 시 Close + 선택 모드 해제.
    ///
    /// [규약] foreach만(읽기 전용) / public 멤버(CS0050) / UnityEngine.Debug /
    ///        IStyle 4면 개별 속성 / IMGUI(GUI.xxx) 금지 / USS gradient·url 절대경로 금지.
    /// </summary>
    public class SoldierInteractUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static SoldierInteractUTK _instance;
        public static SoldierInteractUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance == null)
                _instance = new SoldierInteractUTK();
        }

        /// <summary>병사 상호작용 창 열기 — 정적 진입점 (F키 브리지 구독 경유).</summary>
        public static void Open(GuardPlaceholder guard)
        {
            if (guard == null || !guard.IsAlive) return;
            Ensure();
            _instance.OpenForGuard(guard);
        }

        /// <summary>
        /// [P13 배선] F키 병사 상호작용 — Systems(PlayerMovement) 이벤트 → UTK 상호작용 창.
        /// GuardInfoUTK.BootstrapBridge와 동일 AfterSceneLoad 패턴.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapBridge()
        {
            SoldierInteractBridge.OnInteractRequested += guard => Open(guard);
        }

        // ===== 설정 =====
        private const float WinW = 360f;
        private const float WinH = 400f;
        private const float MaxInteractDistance = 4.5f;   // IMGUI 시절 _interactRange(3m)×1.5 — 동일 임계 유지
        private const long PollMs = 200L;                 // 유효성/거리/상태메시지 폴링 주기

        private enum ItemMode { None, Food, Drug }
        private ItemMode _itemMode = ItemMode.None;

        // ===== 상태 =====
        private GuardPlaceholder _guard;
        private GameObject _playerCache;
        private IVisualElementScheduledItem _pollTask;

        // ===== UI 참조 =====
        private Label _headerLabel;
        private Label _statusLabel;
        private VisualElement _menuSection;
        private VisualElement _itemSection;
        private Label _itemTitleLabel;
        private ScrollView _itemList;
        private Label _itemEmptyLabel;

        private SoldierInteractUTK() : base("병사 상호작용", new Vector2(WinW, WinH))
        {
            BuildHeader();
            BuildMenu();
            BuildItemSection();
            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  콘텐츠 빌드
        // =====================================================================

        private void BuildHeader()
        {
            _headerLabel = new Label("");
            _headerLabel.name = "GuardHeader";
            _headerLabel.style.fontSize = 17f;
            _headerLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            _headerLabel.style.marginLeft = 10f;
            _headerLabel.style.marginTop = 4f;
            _headerLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_headerLabel);

            _statusLabel = new Label("");
            _statusLabel.name = "GuardStatus";
            _statusLabel.style.fontSize = 13f;
            _statusLabel.style.color = new StyleColor(UTKColor.AccentMagic);
            _statusLabel.style.marginLeft = 10f;
            _statusLabel.style.marginTop = 2f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_statusLabel);
        }

        private void BuildMenu()
        {
            _menuSection = new VisualElement();
            _menuSection.name = "MenuSection";
            _menuSection.style.flexGrow = 1f;
            _menuSection.style.marginTop = 8f;
            _content.Add(_menuSection);

            AddMenuButton("🗣️ 말걸기", OnTalkClicked, UTKButton.Variant.Secondary);
            AddMenuButton("🥩 음식주기", () => ShowItemSection(ItemMode.Food), UTKButton.Variant.Secondary);
            AddMenuButton("💊 약주기", () => ShowItemSection(ItemMode.Drug), UTKButton.Variant.Secondary);
            AddMenuButton("🤝 포섭", OnRecruitClicked, UTKButton.Variant.Secondary);
            AddMenuButton("📋 병사 정보보기", OnInfoClicked, UTKButton.Variant.Primary);
            AddMenuButton("🔙 닫기", Close, UTKButton.Variant.Danger);
        }

        private void AddMenuButton(string text, System.Action onClick, UTKButton.Variant variant)
        {
            var btn = UTKButton.Create(text, onClick, variant);
            btn.style.height = 44f;
            btn.style.marginTop = 6f;
            btn.style.marginLeft = 10f;
            btn.style.marginRight = 10f;
            btn.style.fontSize = 16f;
            _menuSection.Add(btn);
        }

        private void BuildItemSection()
        {
            _itemSection = new VisualElement();
            _itemSection.name = "ItemSection";
            _itemSection.style.flexGrow = 1f;
            _itemSection.style.display = DisplayStyle.None;
            _content.Add(_itemSection);

            _itemTitleLabel = new Label("🥩 음식 선택");
            _itemTitleLabel.name = "ItemTitle";
            _itemTitleLabel.style.fontSize = 17f;
            _itemTitleLabel.style.color = new StyleColor(UTKColor.AccentRare);
            _itemTitleLabel.style.marginLeft = 10f;
            _itemTitleLabel.style.marginTop = 4f;
            _itemSection.Add(_itemTitleLabel);

            // 인벤토리 아이템 스크롤 리스트 (IMGUI BeginScrollView 대체)
            _itemList = new ScrollView(ScrollViewMode.Vertical);
            _itemList.name = "ItemList";
            _itemList.style.flexGrow = 1f;
            _itemList.style.marginTop = 6f;
            _itemList.style.marginLeft = 10f;
            _itemList.style.marginRight = 10f;
            _itemSection.Add(_itemList);

            _itemEmptyLabel = new Label("보유한 아이템이 없습니다.");
            _itemEmptyLabel.name = "ItemEmpty";
            _itemEmptyLabel.style.fontSize = 13f;
            _itemEmptyLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _itemEmptyLabel.style.marginLeft = 10f;
            _itemEmptyLabel.style.marginTop = 10f;
            _itemSection.Add(_itemEmptyLabel);

            var cancelBtn = UTKButton.Create("🔙 취소", ShowMenu, UTKButton.Variant.Secondary);
            cancelBtn.style.height = 36f;
            cancelBtn.style.marginTop = 6f;
            cancelBtn.style.marginLeft = 10f;
            cancelBtn.style.marginRight = 10f;
            cancelBtn.style.marginBottom = 6f;
            _itemSection.Add(cancelBtn);
        }

        // =====================================================================
        //  생명주기
        // =====================================================================

        /// <summary>대상 지정 후 열기 — 이미 열려있으면 대상만 교체.</summary>
        public void OpenForGuard(GuardPlaceholder guard)
        {
            if (guard == null) return;
            _guard = guard;
            if (IsOpen)
            {
                // 다른 병사로 대상 교체 — UI 즉시 갱신 (base.Show는 IsOpen 시 no-op)
                RefreshHeader();
                RefreshStatus();
            }
            Show();
        }

        public override void Show()
        {
            bool wasOpen = IsOpen;
            base.Show();
            if (wasOpen) return;

            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            CenterOnParent();

            _playerCache = null;
            RefreshHeader();
            ShowMenu();
            StartPoll();
            Debug.Log("[SoldierInteractUTK] 병사 상호작용 창 열림: " + _guard.GuardName);
        }

        public override void Hide()
        {
            base.Hide();
            StopPoll();
            if (_guard != null)
            {
                _guard.CancelItemSelection();   // 아이템 선택 중이던 모드 해제 (데이터/규칙 불변)
                _guard = null;
            }
            Debug.Log("[SoldierInteractUTK] 병사 상호작용 창 닫힘");
        }

        // =====================================================================
        //  자체 폴링 (200ms) — 유효성/거리/상태메시지
        // =====================================================================

        private void StartPoll()
        {
            if (_pollTask != null) return;
            _pollTask = schedule.Execute(PollTick).Every(PollMs);
        }

        private void StopPoll()
        {
            if (_pollTask != null)
            {
                _pollTask.Pause();
                _pollTask = null;
            }
        }

        private void PollTick()
        {
            if (!IsOpen) return;

            GuardPlaceholder guard = _guard;
            // 대상 무효 (null / 사망 / 비활성) → 자동 닫기
            if (guard == null || !guard.IsAlive || !guard.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            // 거리 폴링 — 플레이어 캐시 (GuardPlaceholder와 동일 "Player" 태그)
            if (_playerCache == null || !_playerCache.activeInHierarchy)
                _playerCache = GameObject.FindGameObjectWithTag("Player");
            if (_playerCache != null)
            {
                float dist = Vector3.Distance(guard.transform.position, _playerCache.transform.position);
                if (dist > MaxInteractDistance)
                {
                    Close();
                    return;
                }
            }

            // 상태 메시지 갱신 (말걸기/지급/포섭 응답 + 시스템 메시지 폴링 반영)
            RefreshStatus();
        }

        // =====================================================================
        //  메뉴 동작
        // =====================================================================

        private void OnTalkClicked()
        {
            if (_guard == null) return;
            _guard.BeginTalk();     // 기존 OnTalk 로직 재사용
            RefreshStatus();
        }

        private void OnRecruitClicked()
        {
            if (_guard == null) return;
            _guard.BeginRecruit();  // 기존 OnRecruit 로직 재사용
            RefreshStatus();
        }

        /// <summary>📋 병사 정보보기 — 기존 GuardInfoUTK 구독 경로(SoldierInteractBridge.Raise) 유지.</summary>
        private void OnInfoClicked()
        {
            if (_guard == null) return;
            SoldierInteractBridge.Raise(_guard);
        }

        // =====================================================================
        //  아이템 선택 (음식/약) — IMGUI DrawItemSelectionPopup의 UTK 재구현
        // =====================================================================

        private void ShowItemSection(ItemMode mode)
        {
            if (_guard == null) return;
            _itemMode = mode;
            if (mode == ItemMode.Food) _guard.BeginFoodSelection();
            else _guard.BeginDrugSelection();

            _menuSection.style.display = DisplayStyle.None;
            _itemSection.style.display = DisplayStyle.Flex;
            _itemTitleLabel.text = mode == ItemMode.Food ? "🥩 음식 선택" : "💊 약 선택";
            RebuildItemList();
        }

        /// <summary>메뉴로 복귀 + 병사 쪽 선택 모드 해제.</summary>
        private void ShowMenu()
        {
            _itemMode = ItemMode.None;
            _menuSection.style.display = DisplayStyle.Flex;
            _itemSection.style.display = DisplayStyle.None;
            if (_guard == null) return;
            _guard.CancelItemSelection();
            RefreshStatus();
        }

        /// <summary>
        /// 아이템 목록 재구성 — GuardPlaceholder.GetInventoryItemsByMode() 필터 재사용
        /// (음식 = Food / 약 = Potion+Drug, 기존 IMGUI와 동일 규칙).
        /// </summary>
        private void RebuildItemList()
        {
            _itemList.Clear();
            var items = _guard != null ? _guard.GetInventoryItemsByMode() : null;
            if (items == null || items.Count == 0)
            {
                _itemEmptyLabel.style.display = DisplayStyle.Flex;
                return;
            }
            _itemEmptyLabel.style.display = DisplayStyle.None;
            foreach (var pair in items)
                _itemList.Add(BuildItemRow(pair.Key, pair.Value));
        }

        private VisualElement BuildItemRow(PlayerInventory.ItemData item, int count)
        {
            var row = new VisualElement();
            row.name = "ItemRow";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 48f;
            row.style.marginBottom = 4f;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;

            var slot = new UTKSlot();
            slot.name = "ItemIcon";
            slot.style.width = 38f;
            slot.style.height = 38f;
            slot.style.flexShrink = 0f;
            slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
            slot.SetRank(UTKRarity.ClassForIndex((int)item.rarity));
            row.Add(slot);

            var name = new Label(!string.IsNullOrEmpty(item.displayName) ? item.displayName : item.id);
            name.style.fontSize = 14f;
            name.style.color = new StyleColor(UTKColor.TextPrimary);
            name.style.marginLeft = 8f;
            name.style.flexGrow = 1f;
            row.Add(name);

            var countLabel = new Label("x" + count);
            countLabel.style.fontSize = 13f;
            countLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            countLabel.style.marginRight = 8f;
            row.Add(countLabel);

            var giveBtn = UTKButton.Create("주기", () => GiveItem(item), UTKButton.Variant.Primary);
            giveBtn.style.width = 64f;
            giveBtn.style.height = 30f;
            giveBtn.style.flexShrink = 0f;
            row.Add(giveBtn);

            return row;
        }

        /// <summary>아이템 지급 — GuardPlaceholder.GiveFood/GiveDrug(기존 GiveItemToGuard 경로) 후 메뉴 복귀.</summary>
        private void GiveItem(PlayerInventory.ItemData item)
        {
            if (_guard == null || item == null) return;
            if (_itemMode == ItemMode.Food) _guard.GiveFood(item);
            else _guard.GiveDrug(item);
            ShowMenu();   // 지급 로직이 _selectionMode를 해제하지 않으므로 복귀 시 해제
        }

        // =====================================================================
        //  표시 갱신 / 헬퍼
        // =====================================================================

        private void RefreshHeader()
        {
            if (_guard == null) return;
            _headerLabel.text = $"⚔️ {_guard.GuardName} Lv.{_guard.Level} ({_guard.Nation}) — 호감도 {_guard.Loyalty:F0}";
        }

        /// <summary>병사 상태메시지 표시 (GuardPlaceholder.StatusMessage public 프로퍼티).</summary>
        private void RefreshStatus()
        {
            if (_guard == null) return;
            string msg = _guard.StatusMessage;
            _statusLabel.text = string.IsNullOrEmpty(msg) ? "무슨 일이냐?" : msg;
        }

        private void CenterOnParent()
        {
            var par = parent;
            if (par == null) return;
            float pw = par.resolvedStyle.width;
            float ph = par.resolvedStyle.height;
            if (pw <= 0f || ph <= 0f) return;
            style.left = Mathf.Max(0f, (pw - resolvedStyle.width) * 0.5f);
            style.top = Mathf.Max(0f, (ph - resolvedStyle.height) * 0.5f);
        }
    }
}
