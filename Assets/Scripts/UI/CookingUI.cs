using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;
using ProjectName.UI.Themes;

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
        private string _resultMessage = "";
        private string _resultItemName = "";
        private string _resultEffect = "";
        private string _resultGrade = "";
        private bool _hasResult = false;
        private Vector2 _meatScrollPos;
        private Vector2 _herbScrollPos;
        private int _gridColumns = 4;

        // ── 스타일 ──
        private GUIStyle _titleStyle;
        private GUIStyle _slotStyle;
        private GUIStyle _resultStyle;
        private GUIStyle _categoryHeaderStyle;
        private bool _stylesInitialized;

        protected override void OnShow()
        {
            base.OnShow();
            _selectedMeat = null;
            _selectedHerb = null;
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

        public void Open()
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
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f, 0.9f)) }
            };

            _resultStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 52,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 1f, 0.7f), background = MakeTexture(1, 1, new Color(0.1f, 0.25f, 0.1f, 0.8f)) }
            };

            _categoryHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _stylesInitialized = true;
        }

        private Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            // G3-05: 통일 스타일 — 딤드 오버레이 + 배경 + 타이틀 + 닫기 버튼
            UIStyleManager.DrawDimOverlay();
            float _winX = (Screen.width - _windowWidth) / 2f;
            float _winY = (Screen.height - _windowHeight) / 2f;
            Rect _winRect = new Rect(_winX, _winY, _windowWidth, _windowHeight);
            UIStyleManager.DrawWindowBackground(_winRect);
            UIStyleManager.DrawTitle(_winRect, "  🍳 요리 테이블");
            if (UIStyleManager.DrawCloseButton(_winRect))
            {
                Hide();
                return;
            }

            InitializeStyles();

            // 메인 윈도우 영역 (중앙)
            float x = _winX;
            float y = _winY;
            Rect windowRect = _winRect;

            GUILayout.BeginArea(windowRect, GUI.skin.box);

            // ── 제목 표시줄 ──
            GUILayout.BeginHorizontal();
            GUILayout.Label("  🍳 요리 테이블", _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기 X", GUILayout.Width(90), GUILayout.Height(36)))
            {
                Hide();
                return;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ── 재료 슬롯 2개 ──
            GUILayout.BeginHorizontal(GUILayout.Height(120));
            DrawIngredientSlot("고기", ref _selectedMeat, Color.green);
            GUILayout.FlexibleSpace();
            DrawIngredientSlot("약초", ref _selectedHerb, Color.cyan);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ── 요리하기 버튼 ──
            bool canCook = _selectedMeat != null && _selectedHerb != null;
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
                var style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 52,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.6f, 0.4f), background = MakeTexture(1, 1, new Color(0.25f, 0.1f, 0.1f, 0.8f)) }
                };
                GUILayout.Box(_resultMessage, style, GUILayout.Height(60));
            }
            else
            {
                GUILayout.Box("고기와 약초를 선택하고 요리해보세요.", GUILayout.Height(54));
            }

            GUILayout.Space(4);

            // ── 구분선 ──
            GUILayout.Label("─── 인벤토리 (고기) ───", _categoryHeaderStyle);

            // ── 고기 인벤토리 그리드 ──
            float availableWidth = _windowWidth - 30;
            float itemSlotSize = Mathf.Min(80, (availableWidth - (_gridColumns - 1) * 6) / _gridColumns);
            float gridHeight = (_windowHeight - 340) / 2f;
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

            GUILayout.EndArea();
        }

        /// <summary>
        /// 재료 슬롯 (고기/약초 선택)
        /// </summary>
        private void DrawIngredientSlot(string label, ref PlayerInventory.ItemData slot, Color accentColor)
        {
            GUILayout.BeginVertical(GUILayout.Width(240), GUILayout.Height(120));

            GUILayout.Label(label, GUILayout.Height(27));

            Rect slotRect = GUILayoutUtility.GetRect(120, 54);
            var slotBg = MakeTexture(1, 1, new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f, 0.7f));
            GUI.Box(slotRect, "", _slotStyle);
            GUI.DrawTexture(slotRect, slotBg);

            if (slot != null)
            {
                GUI.Label(new Rect(slotRect.x + 4, slotRect.y + 4, slotRect.width - 8, 30), slot.displayName);

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
        private void DrawInventoryFoodSlot(PlayerInventory.ItemSlot invSlot, float size, bool isMeat)
        {
            var item = invSlot.item;
            string label = $"{item.displayName}\nx{invSlot.count}";

            Rect rect = GUILayoutUtility.GetRect(size, size);

            // 배경
            Color bgColor = isMeat
                ? new Color(0.5f, 0.25f, 0.1f, 0.8f)
                : new Color(0.15f, 0.4f, 0.15f, 0.8f);

            // 이미 선택된 아이템이면 하이라이트
            bool isSelected = isMeat
                ? (_selectedMeat != null && _selectedMeat.id == item.id)
                : (_selectedHerb != null && _selectedHerb.id == item.id);
            if (isSelected)
                bgColor = isMeat ? new Color(0.6f, 0.4f, 0.2f, 0.9f) : new Color(0.3f, 0.6f, 0.3f, 0.9f);

            var slotBg = MakeTexture(1, 1, bgColor);
            GUI.DrawTexture(rect, slotBg);

            // 아이콘 (색상 사각형 fallback)
            Color iconColor = isMeat ? new Color(0.8f, 0.4f, 0.2f) : new Color(0.2f, 0.7f, 0.2f);
            GUI.color = iconColor;
            GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, size - 8, size - 24), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 이름 + 개수
            GUI.Label(new Rect(rect.x + 2, rect.y + size - 22, rect.width - 4, 30), label, new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            });

            // 클릭 감지
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                if (isMeat)
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
                return;
            }

            var inventory = PlayerInventory.Instance;
            if (inventory == null)
            {
                _resultMessage = "인벤토리를 찾을 수 없습니다.";
                _hasResult = false;
                return;
            }

            // 인벤토리에 재료 확인
            if (!inventory.HasItem(_selectedMeat.id) || inventory.GetItemCount(_selectedMeat.id) < 1)
            {
                _resultMessage = $"'{_selectedMeat.displayName}'이(가) 인벤토리에 없습니다.";
                _hasResult = false;
                return;
            }
            if (!inventory.HasItem(_selectedHerb.id) || inventory.GetItemCount(_selectedHerb.id) < 1)
            {
                _resultMessage = $"'{_selectedHerb.displayName}'이(가) 인벤토리에 없습니다.";
                _hasResult = false;
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

                        // 요리 아이템 생성 및 지급
                        var dishItem = DishDatabase.GetItemData(result.dishName);
                        if (dishItem != null)
                            inventory.AddItem(dishItem, 1);

                        // 발견 등록
                        RecipeDiscoverySystem.MarkDiscovered(result.dishName);

                        // 미식 등급 조회 (DishInfo에서)
                        var dishInfo = DishDatabase.GetDishInfoByName(result.dishName);
                        string gradeStr = "";
                        if (dishInfo != null && dishInfo.StarRating > 0)
                        {
                            var gourmetGrade = GourmetDatabase.GetGrade(dishInfo.StarRating);
                            gradeStr = gourmetGrade.HasValue
                                ? $"★{dishInfo.StarRating} {gourmetGrade.Value.gradeName}"
                                : $"★{dishInfo.StarRating}";
                        }
                        else
                        {
                            gradeStr = "일반";
                        }

                        _resultMessage = $"🟢 요리 성공! '{result.dishName}' 획득!";
                        _resultItemName = result.dishName;
                        _resultEffect = result.effect;
                        _resultGrade = gradeStr;
                        _hasResult = true;

                        // 경험치 획득 (요리 성공 시 +5~15)
                        if (PlayerStats.Instance != null)
                            PlayerStats.Instance.AddExp(Random.Range(5, 16));

                        Debug.Log($"[CookingUI] 요리 성공: {_selectedMeat.displayName} + {_selectedHerb.displayName} → {result.dishName}");

                        // 슬롯 비우기
                        _selectedMeat = null;
                        _selectedHerb = null;
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
                        Debug.Log($"[CookingUI] 요리 실패 (재료소멸): {destroyedName} 파괴됨");

                        if (destroyMeat) _selectedMeat = null;
                        else _selectedHerb = null;
                        break;

                    case CraftResult.Fail_Burned:
                        inventory.RemoveItem(_selectedMeat.id, 1);
                        inventory.RemoveItem(_selectedHerb.id, 1);

                        _resultMessage = "💥 요리 실패! 모든 재료가 전소했다!";
                        _hasResult = false;
                        _resultItemName = "";
                        _resultEffect = "";
                        _resultGrade = "";
                        Debug.Log($"[CookingUI] 요리 실패 (전소): {_selectedMeat.displayName} + {_selectedHerb.displayName} 소멸");

                        _selectedMeat = null;
                        _selectedHerb = null;
                        break;
                }
            }
            else
            {
                _resultMessage = "조합 불가 — 해당 요리법이 없습니다.";
                _hasResult = false;
                _resultItemName = "";
                _resultEffect = "";
                _resultGrade = "";
                Debug.Log($"[CookingUI] 요리 실패: {_selectedMeat.displayName} + {_selectedHerb.displayName} — 레시피 없음");
            }
        }
    }
}