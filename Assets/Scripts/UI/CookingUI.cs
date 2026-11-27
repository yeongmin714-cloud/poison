using UnityEngine;
using ProjectName.UI.Themes;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;
using ProjectName.Core.Themes;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 요리 제작 UI (IMGUI, UIWindow 기반)
    /// 고기 + 약초 조합으로 요리 제작
    /// 성공/실패 시스템 적용
    /// </summary>
    public class CookingUI : UIWindow
    {
        protected virtual void Start()
        {
            ApplyTheme(Phase33_Themes.CreateCookingTheme());
        }

        [Header("Cooking UI Settings")]
        [SerializeField] private int _windowWidth = 1500;
        [SerializeField] private int _windowHeight = 1305;

        // ── 상태 ──
        private PlayerInventory.ItemData _selectedMeat;
        private PlayerInventory.ItemData _selectedHerb;
        private PlayerInventory.ItemData _selectedDrug; // D3-A2: 마약 재료 (null = 미선택)
        private string _resultMessage = "";
        private string _resultItemName = "";
        private string _resultEffect = "";
        private string _resultGrade = "";
        private bool _hasResult = false;
        private Vector2 _meatScrollPos;
        private Vector2 _herbScrollPos;
        private Vector2 _drugScrollPos; // D3-A2

        // ── 스타일 ──
        private GUIStyle _titleStyle;
        private GUIStyle _slotStyle;
        private GUIStyle _resultStyle;
        private GUIStyle _categoryHeaderStyle;
        private GUIStyle _failResultStyle;
        private GUIStyle _foodSlotLabelStyle;
        private GUIStyle _slotLabelStyle;
        private GUIStyle _slotItemNameStyle;
        private bool _stylesInitialized;

        // ── 캐싱된 텍스처 (GC 방지) ──
        private Texture2D _meatSlotBg;
        private Texture2D _herbSlotBg;
        private Texture2D _meatFoodBg;
        private Texture2D _herbFoodBg;
        private Texture2D _meatFoodSelectedBg;
        private Texture2D _herbFoodSelectedBg;
        private Texture2D _drugSlotBg;         // D3-A2: 마약 재료 슬롯 배경
        private Texture2D _drugFoodBg;         // D3-A2: 마약 인벤 슬롯 배경
        private Texture2D _drugFoodSelectedBg; // D3-A2: 마약 인벤 슬롯 선택 배경

        protected override void OnShow()
        {
            base.OnShow();
            _selectedMeat = null;
            _selectedHerb = null;
            _selectedDrug = null;
            _resultMessage = "";
            _hasResult = false;
            _stylesInitialized = false;
            Debug.Log("[CookingUI] 요리 테이블 열림");
        }

        protected override void OnHide()
        {
            base.OnHide();
            Debug.Log("[CookingUI] 요리 테이블 닫힘");
        }

        protected override void OnDestroy()
        {
            // Clean up cached textures
            if (_meatSlotBg != null) Destroy(_meatSlotBg);
            if (_herbSlotBg != null) Destroy(_herbSlotBg);
            if (_meatFoodBg != null) Destroy(_meatFoodBg);
            if (_herbFoodBg != null) Destroy(_herbFoodBg);
            if (_meatFoodSelectedBg != null) Destroy(_meatFoodSelectedBg);
            if (_herbFoodSelectedBg != null) Destroy(_herbFoodSelectedBg);
            if (_drugSlotBg != null) Destroy(_drugSlotBg);
            if (_drugFoodBg != null) Destroy(_drugFoodBg);
            if (_drugFoodSelectedBg != null) Destroy(_drugFoodSelectedBg);
            base.OnDestroy();
        }

        /// <summary>
        /// 창을 엽니다. 베이스의 Open()을 override하여 Show()를 호출합니다.
        /// </summary>
        public new void Open()
        {
            if (!_isOpen)
                Show();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 72,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _slotStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = UIStyleManager.MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f, 0.9f)) }
            };

            _resultStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 52,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 1f, 0.7f), background = UIStyleManager.MakeTexture(1, 1, new Color(0.1f, 0.25f, 0.1f, 0.8f)) }
            };

            _categoryHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            // 실패 메시지 스타일 (캐싱)
            _failResultStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 52,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.6f, 0.4f), background = UIStyleManager.MakeTexture(1, 1, new Color(0.25f, 0.1f, 0.1f, 0.8f)) }
            };

            // 인벤토리 푸드 슬롯 라벨 스타일 (캐싱)
            _foodSlotLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            // 재료 슬롯 라벨 스타일 (기본 폰트 판독성 수정)
            _slotLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            // 재료 슬롯 아이템 이름 스타일 (캐싱)
            _slotItemNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white }
            };

            // ── 재료 슬롯 배경 텍스처 캐싱 ──
            _meatSlotBg = UIStyleManager.MakeTexture(1, 1, new Color(Color.green.r * 0.3f, Color.green.g * 0.3f, Color.green.b * 0.3f, 0.7f));
            _herbSlotBg = UIStyleManager.MakeTexture(1, 1, new Color(Color.cyan.r * 0.3f, Color.cyan.g * 0.3f, Color.cyan.b * 0.3f, 0.7f));
            _drugSlotBg = UIStyleManager.MakeTexture(1, 1, new Color(0.4f, 0.15f, 0.45f, 0.7f)); // D3-A2: 보라 계열

            // ── 인벤토리 슬롯 배경 텍스처 캐싱 ──
            _meatFoodBg = UIStyleManager.MakeTexture(1, 1, new Color(0.5f, 0.25f, 0.1f, 0.8f));
            _herbFoodBg = UIStyleManager.MakeTexture(1, 1, new Color(0.15f, 0.4f, 0.15f, 0.8f));
            _meatFoodSelectedBg = UIStyleManager.MakeTexture(1, 1, new Color(0.6f, 0.4f, 0.2f, 0.9f));
            _herbFoodSelectedBg = UIStyleManager.MakeTexture(1, 1, new Color(0.3f, 0.6f, 0.3f, 0.9f));
            _drugFoodBg = UIStyleManager.MakeTexture(1, 1, new Color(0.3f, 0.12f, 0.4f, 0.8f));
            _drugFoodSelectedBg = UIStyleManager.MakeTexture(1, 1, new Color(0.45f, 0.2f, 0.55f, 0.9f));

            _stylesInitialized = true;
        }

        protected override void OnGUI()
        {
            if (!_isOpen) return;

            // G3-05: 통일 스타일 — 딤드 오버레이 + 배경 + 타이틀 + 닫기 버튼
            UIStyleManager.DrawDimOverlay();
            // 1080p 등 저해상도 클램프: 창이 화면보다 크면 화면에 맞춤 (필드 원본은 유지)
            float effW = Mathf.Min(_windowWidth, Screen.width - 40);
            float effH = Mathf.Min(_windowHeight, Screen.height - 40);
            float winX = (Screen.width - effW) / 2f;
            float winY = (Screen.height - effH) / 2f;
            Rect winRect = new Rect(winX, winY, effW, effH);
            UIStyleManager.DrawWindowBackground(winRect);
            UIStyleManager.DrawTitle(winRect, "  🍳 요리 테이블");
            if (UIStyleManager.DrawCloseButton(winRect))
            {
                Hide();
                return;
            }

            // Phase 33: 테마 데코레이션 (그라디언트 + 장식 테두리)
            DrawThemeDecorations(winRect);

            InitializeStyles();

            GUILayout.BeginArea(winRect, GUI.skin.box);

            GUILayout.Space(60); // 타이틀 영역 확보 (UIStyleManager 타이틀과 겹치지 않도록)

            // ── 재료 슬롯 2개 ──
            GUILayout.BeginHorizontal(GUILayout.Height(120));
            DrawIngredientSlot("고기", ref _selectedMeat, Color.green, true);
            GUILayout.FlexibleSpace();
            DrawIngredientSlot("약초", ref _selectedHerb, Color.cyan, false);
            GUILayout.FlexibleSpace();
            DrawIngredientSlot("마약", ref _selectedDrug, Color.magenta, false, true); // D3-A2
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ── 요리하기 버튼 ──
            bool canCook = _selectedMeat != null && _selectedHerb != null;
            // D3-A2: 마약 선택 시 마약 보유 여부도 활성 조건에 포함 (기존 재료 체크 패턴 준용)
            if (canCook && _selectedDrug != null)
            {
                var invForCook = PlayerInventory.Instance;
                canCook = invForCook != null && invForCook.HasItem(_selectedDrug.id) && invForCook.GetItemCount(_selectedDrug.id) >= 1;
            }
            GUI.enabled = canCook;
            if (GUILayout.Button("▼ 요리하기 ▼", GUILayout.Height(54)))
            {
                TryCook();
            }
            GUI.enabled = true;

            GUILayout.Space(6);

            // ── 결과 표시 ──
            if (_hasResult)
            {
                string resultText = string.IsNullOrEmpty(_resultItemName)
                    ? $"{_resultMessage}"
                    : $"{_resultMessage}\n효과: {_resultEffect}\n등급: {_resultGrade}";
                GUILayout.Box(resultText, _resultStyle, GUILayout.Height(90));
            }
            else if (!string.IsNullOrEmpty(_resultMessage))
            {
                // 캐싱된 실패 스타일 사용 (GC-safe)
                GUILayout.Box(_resultMessage, _failResultStyle, GUILayout.Height(60));
            }
            else
            {
                GUILayout.Box("고기와 약초를 선택하고 요리해보세요.", GUILayout.Height(54));
            }

            GUILayout.Space(4);

            // ── 구분선 ──
            GUILayout.Label("─── 인벤토리 (고기) ───", _categoryHeaderStyle);

            // ── 고기 인벤토리 그리드 ──
            float availableWidth = effW - 30;
            float itemSlotSize = Mathf.Min(80, (availableWidth - 3 * 6) / 4f);
            float gridHeight = (effH - 400) / 3f; // D3-A2: 마약 인벤 그리드 추가로 3분할
            if (gridHeight < 60) gridHeight = 60;

            _meatScrollPos = GUILayout.BeginScrollView(_meatScrollPos, GUILayout.Height(gridHeight));

            var inventory = PlayerInventory.Instance;
            if (inventory != null)
            {
                var allSlots = inventory.GetAllSlots();
                int cols = Mathf.Max(1, (int)(availableWidth / (itemSlotSize + 6)));
                if (cols < 1) cols = 1;

                GUILayout.BeginVertical();
                int idx = 0;
                foreach (var slot in allSlots)
                {
                    if (slot == null || slot.item == null || slot.count <= 0)
                    {
                        idx++;
                        continue;
                    }
                    if (slot.item.category != PlayerInventory.ItemCategory.Meat)
                    {
                        idx++;
                        continue;
                    }

                    if (idx % cols == 0)
                        GUILayout.BeginHorizontal();

                    DrawInventoryFoodSlot(slot, itemSlotSize, true);

                    idx++;
                    if (idx % cols == 0)
                        GUILayout.EndHorizontal();
                }
                if (idx % cols != 0)
                    GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(2);

            // ── 구분선 ──
            GUILayout.Label("─── 인벤토리 (약초) ───", _categoryHeaderStyle);

            // ── 약초 인벤토리 그리드 ──
            _herbScrollPos = GUILayout.BeginScrollView(_herbScrollPos, GUILayout.Height(gridHeight));

            if (inventory != null)
            {
                var allSlots = inventory.GetAllSlots();
                int cols = Mathf.Max(1, (int)(availableWidth / (itemSlotSize + 6)));
                if (cols < 1) cols = 1;

                GUILayout.BeginVertical();
                int idx = 0;
                foreach (var slot in allSlots)
                {
                    if (slot == null || slot.item == null || slot.count <= 0)
                    {
                        idx++;
                        continue;
                    }
                    if (slot.item.category != PlayerInventory.ItemCategory.Herb)
                    {
                        idx++;
                        continue;
                    }

                    if (idx % cols == 0)
                        GUILayout.BeginHorizontal();

                    DrawInventoryFoodSlot(slot, itemSlotSize, false);

                    idx++;
                    if (idx % cols == 0)
                        GUILayout.EndHorizontal();
                }
                if (idx % cols != 0)
                    GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(2);

            // ── 구분선 ──
            GUILayout.Label("─── 인벤토리 (마약) ───", _categoryHeaderStyle);

            // ── 마약 인벤토리 그리드 (D3-A2: ItemCategory.Drug 필터) ──
            _drugScrollPos = GUILayout.BeginScrollView(_drugScrollPos, GUILayout.Height(gridHeight));

            if (inventory != null)
            {
                var allSlots = inventory.GetAllSlots();
                int cols = Mathf.Max(1, (int)(availableWidth / (itemSlotSize + 6)));
                if (cols < 1) cols = 1;

                GUILayout.BeginVertical();
                int idx = 0;
                int drawnCount = 0;
                foreach (var slot in allSlots)
                {
                    if (slot == null || slot.item == null || slot.count <= 0)
                    {
                        idx++;
                        continue;
                    }
                    if (slot.item.category != PlayerInventory.ItemCategory.Drug)
                    {
                        idx++;
                        continue;
                    }

                    if (idx % cols == 0)
                        GUILayout.BeginHorizontal();

                    DrawInventoryFoodSlot(slot, itemSlotSize, false, true);

                    drawnCount++;
                    idx++;
                    if (idx % cols == 0)
                        GUILayout.EndHorizontal();
                }
                if (idx % cols != 0)
                    GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                // D3-A2: 마약 인벤토리가 비어 있으면 '마약 없음' 안내 표시
                if (drawnCount == 0)
                    GUILayout.Label("마약 없음", _slotLabelStyle);
            }

            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        /// <summary>
        /// 재료 슬롯 (고기/약초 선택)
        /// </summary>
        private void DrawIngredientSlot(string label, ref PlayerInventory.ItemData slot, Color accentColor, bool isMeat, bool isDrug = false)
        {
            GUILayout.BeginVertical(GUILayout.Width(240), GUILayout.Height(120));

            GUILayout.Label(label, _slotLabelStyle, GUILayout.Height(48));

            Rect slotRect = GUILayoutUtility.GetRect(120, 54);
            Texture2D slotBg = isDrug ? _drugSlotBg : (isMeat ? _meatSlotBg : _herbSlotBg);
            GUI.Box(slotRect, "", _slotStyle);
            GUI.DrawTexture(slotRect, slotBg);

            if (slot != null)
            {
                GUI.Label(new Rect(slotRect.x + 4, slotRect.y + 4, slotRect.width - 8, 36), slot.displayName, _slotItemNameStyle);

                // 우클릭 감지 → 슬롯 비우기
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && slotRect.Contains(Event.current.mousePosition))
                {
                    slot = null;
                    _hasResult = false;
                    _resultMessage = "";
                    Event.current.Use();
                }

                if (GUI.Button(new Rect(slotRect.xMax - 20, slotRect.y + 4, 24, 24), "X"))
                {
                    slot = null;
                    _hasResult = false;
                    _resultMessage = "";
                }
            }
            else
            {
                GUI.Label(slotRect, "  [비어있음]\n  (아이템 클릭)", _slotStyle);
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 인벤토리 아이템 슬롯 (고기 또는 약초)
        /// </summary>
        private void DrawInventoryFoodSlot(PlayerInventory.ItemSlot invSlot, float size, bool isMeat, bool isDrug = false)
        {
            var item = invSlot.item;
            string label = $"{item.displayName}\nx{invSlot.count}";

            Rect rect = GUILayoutUtility.GetRect(size, size);

            // 캐싱된 배경 텍스처 사용 (GC-safe)
            bool isSelected = isDrug
                ? (_selectedDrug != null && _selectedDrug.id == item.id)
                : isMeat
                    ? (_selectedMeat != null && _selectedMeat.id == item.id)
                    : (_selectedHerb != null && _selectedHerb.id == item.id);

            Texture2D bgTexture;
            if (isSelected)
                bgTexture = isDrug ? _drugFoodSelectedBg : (isMeat ? _meatFoodSelectedBg : _herbFoodSelectedBg);
            else
                bgTexture = isDrug ? _drugFoodBg : (isMeat ? _meatFoodBg : _herbFoodBg);

            GUI.DrawTexture(rect, bgTexture);

            // 아이콘 (색상 사각형 fallback)
            Color iconColor = isDrug ? new Color(0.7f, 0.3f, 0.9f)
                : isMeat ? new Color(0.8f, 0.4f, 0.2f) : new Color(0.2f, 0.7f, 0.2f);
            GUI.color = iconColor;
            GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, size - 8, size - 24), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 이름 + 개수 (캐싱된 스타일 사용)
            GUI.Label(new Rect(rect.x + 2, rect.y + size - 22, rect.width - 4, 30), label, _foodSlotLabelStyle);

            // 클릭 감지
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                if (isDrug)
                    _selectedDrug = item;
                else if (isMeat)
                    _selectedMeat = item;
                else
                    _selectedHerb = item;
                _resultMessage = "";
                _hasResult = false;
                Event.current.Use();
            }
        }

        /// <summary>
        /// 요리 시도
        /// </summary>
        private void TryCook()
        {
            if (_selectedMeat == null || _selectedHerb == null)
            {
                _resultMessage = "고기와 약초를 선택해주세요.";
                _hasResult = false;
                _resultItemName = "";
                _resultEffect = "";
                _resultGrade = "";
                return;
            }

            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                _resultMessage = "인벤토리를 찾을 수 없습니다.";
                _hasResult = false;
                _resultItemName = "";
                _resultEffect = "";
                _resultGrade = "";
                return;
            }

            // 인벤토리에 재료 확인
            if (!inventory.HasItem(_selectedMeat.id) || inventory.GetItemCount(_selectedMeat.id) < 1)
            {
                _resultMessage = $"'{_selectedMeat.displayName}'이(가) 인벤토리에 없습니다.";
                _hasResult = false;
                _resultItemName = "";
                _resultEffect = "";
                _resultGrade = "";
                return;
            }
            if (!inventory.HasItem(_selectedHerb.id) || inventory.GetItemCount(_selectedHerb.id) < 1)
            {
                _resultMessage = $"'{_selectedHerb.displayName}'이(가) 인벤토리에 없습니다.";
                _hasResult = false;
                _resultItemName = "";
                _resultEffect = "";
                _resultGrade = "";
                return;
            }

            // D3-A2: 마약 선택 시 마약 보유 확인 (기존 재료 체크 패턴 준용)
            if (_selectedDrug != null &&
                (!inventory.HasItem(_selectedDrug.id) || inventory.GetItemCount(_selectedDrug.id) < 1))
            {
                _resultMessage = $"'{_selectedDrug.displayName}'이(가) 인벤토리에 없습니다.";
                _hasResult = false;
                _resultItemName = "";
                _resultEffect = "";
                _resultGrade = "";
                return;
            }

            // CookingDatabase에서 레시피 검색 (displayName 기반)
            var cookingResult = CookingDatabase.GetCooking(_selectedMeat.displayName, _selectedHerb.displayName);

            if (cookingResult.HasValue)
            {
                var result = cookingResult.Value;

                // 재료 등급 추정
                string grade1 = CraftSuccessSystem.GetGradeFromItemId(_selectedMeat.id);
                string grade2 = CraftSuccessSystem.GetGradeFromItemId(_selectedHerb.id);

                // 성공/실패 판정 (요리 = false)
                CraftResult craftResult = CraftSuccessSystem.ExecuteCraft(false, grade1, grade2);

                switch (craftResult)
                {
                    case CraftResult.Success:
                        // 재료 차감
                        inventory.RemoveItem(_selectedMeat.id, 1);
                        inventory.RemoveItem(_selectedHerb.id, 1);
                        // D3-A2: 마약 확장 — 마약 선택 시 마약 1개를 세 번째 재료로 소모
                        if (_selectedDrug != null)
                            inventory.RemoveItem(_selectedDrug.id, 1);

                        // 요리 아이템 생성 및 지급
                        var dishItem = DishDatabase.GetItemData(result.DishName);
                        if (dishItem != null)
                        {
                            if (_selectedDrug != null)
                            {
                                // D3-A2: 마약 뿌려진 음식 지급 — CookingDrugHelper 규약 문자열로 파생,
                                // 기존 지급 경로(AddItem)는 그대로 유지. ParseDrugStage 실패(-1) 시 stage 0 방어.
                                int drugStage = CookingDrugHelper.ParseDrugStage(_selectedDrug.id);
                                if (drugStage < 0) drugStage = 0;
                                var drugged = CookingDrugHelper.MakeDruggedFood(result, drugStage);
                                var druggedItem = new PlayerInventory.ItemData
                                {
                                    id = drugged.itemId,
                                    displayName = drugged.displayName,
                                    description = drugged.description,
                                    category = dishItem.category,
                                    icon = dishItem.icon,
                                    maxStack = dishItem.maxStack,
                                    maxDurability = dishItem.maxDurability,
                                    rarity = dishItem.rarity,
                                    effects = dishItem.effects,
                                    basePrice = dishItem.basePrice
                                };
                                inventory.AddItem(druggedItem, 1);
                            }
                            else
                            {
                                inventory.AddItem(dishItem, 1);
                            }
                        }

                        // 발견 등록
                        RecipeDiscoverySystem.MarkDiscovered(result.DishName);

                        // 미식 등급 조회 (DishInfo에서)
                        var dishInfo = DishDatabase.GetDishInfoByName(result.DishName);
                        string gradeStr = "";
                        if (dishInfo != null && dishInfo.StarRating > 0)
                        {
                            var gourmetGrade = GourmetDatabase.GetGrade(dishInfo.StarRating);
                            gradeStr = gourmetGrade.HasValue
                                ? $"★{dishInfo.StarRating} {gourmetGrade.Value.GradeName}"
                                : $"★{dishInfo.StarRating}";
                        }
                        else
                        {
                            gradeStr = "일반";
                        }

                        // D3-A2: 마약 선택 시 결과 문구에 마약 마커 반영 (_resultItemName/_resultEffect 흐름은 기존 그대로)
                        _resultMessage = _selectedDrug != null
                            ? $"🟢 요리 성공! '마약 뿌려진 {result.DishName}' 획득!"
                            : $"🟢 요리 성공! '{result.DishName}' 획득!";
                        _resultItemName = result.DishName;
                        _resultEffect = result.Effect;
                        _resultGrade = gradeStr;
                        _hasResult = true;

                        // [129차 Q1] 요리 성공 — 애니 없이 환호만 발화
                        var player = GameObject.FindGameObjectWithTag("Player");
                        var driver = player != null ? (player.GetComponent<HumanoidClipDriver>() ?? player.GetComponentInChildren<HumanoidClipDriver>()) : null;
                        driver?.TriggerCheer();

                        // 경험치 획득 (요리 성공 시 +5~15)
                        if (PlayerStats.Instance != null)
                            PlayerStats.Instance.AddExp(Random.Range(5, 16));

                        // [133차-P2] 요리 성공 징글 — SFX_Acquire_Common(반2D). 실패 분기는 무음(추가 클립 없음).
                        GatherFeedbackSystem.PlayAcquireJingle(0);

                        Debug.Log($"[CookingUI] 요리 성공: {_selectedMeat.displayName} + {_selectedHerb.displayName} → {result.DishName}");

                        // 슬롯 비우기
                        _selectedMeat = null;
                        _selectedHerb = null;
                        _selectedDrug = null;
                        break;

                    case CraftResult.Fail_MaterialPreserved:
                        _resultMessage = "🟡 요리 실패... 재료가 보존되었다.";
                        _hasResult = false;
                        _resultItemName = "";
                        _resultEffect = "";
                        _resultGrade = "";
                        Debug.Log($"[CookingUI] 요리 실패 (재료보존): {_selectedMeat.displayName} + {_selectedHerb.displayName}");
                        break;

                    case CraftResult.Fail_MaterialDestroyed:
                        bool destroyMeat = Random.value < 0.5f;
                        string destroyedName = destroyMeat ? _selectedMeat.displayName : _selectedHerb.displayName;
                        string destroyedId = destroyMeat ? _selectedMeat.id : _selectedHerb.id;
                        inventory.RemoveItem(destroyedId, 1);

                        _resultMessage = $"🔴 요리 실패! '{destroyedName}'이(가) 소멸했다!";
                        _hasResult = false;
                        _resultItemName = "";
                        _resultEffect = "";
                        _resultGrade = "";
                        Debug.Log($"[CookingUI] 요리 실패 (재료소멸): {_selectedMeat.displayName} + {_selectedHerb.displayName} → '{destroyedName}' 소멸");
                        break;

                    case CraftResult.Fail_Burned:
                        // 두 재료 모두 소멸
                        inventory.RemoveItem(_selectedMeat.id, 1);
                        inventory.RemoveItem(_selectedHerb.id, 1);

                        _resultMessage = $"🔥 요리 대실패! 모든 재료가 타서 사라졌다!";
                        _hasResult = false;
                        _resultItemName = "";
                        _resultEffect = "";
                        _resultGrade = "";
                        Debug.Log($"[CookingUI] 요리 대실패 (전소): {_selectedMeat.displayName} + {_selectedHerb.displayName} → 모두 소멸");
                        break;
                }
            }
            else
            {
                // 레시피를 찾을 수 없는 경우
                _resultMessage = $"'{_selectedMeat.displayName}'와(과) '{_selectedHerb.displayName}'의 조합으로는 요리를 만들 수 없습니다.";
                _hasResult = false;
                _resultItemName = "";
                _resultEffect = "";
                _resultGrade = "";
                Debug.Log($"[CookingUI] 알 수 없는 레시피: {_selectedMeat.displayName} + {_selectedHerb.displayName}");
            }
        }
    }
}