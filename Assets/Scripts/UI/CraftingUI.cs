using UnityEngine;
using ProjectName.Core;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.UI
{
    /// <summary>
    /// 크래프트 테이블 제작 UI (IMGUI)
    /// 재료 2개를 선택하여 조합합니다.
    /// </summary>
    public class CraftingUI : UIWindow
    {
        [Header("Crafting UI Settings")]
        [SerializeField] private int _windowWidth = 600;
        [SerializeField] private int _windowHeight = 480;

        // ── 상태 ──
        private PlayerInventory.ItemData _slot1;
        private PlayerInventory.ItemData _slot2;
        private string _resultMessage = "";
        private string _resultItemName = "";
        private string _resultEffect = "";
        private bool _hasResult = false;
        private Vector2 _inventoryScrollPos;
        private int _inventoryGridColumns = 5;

        // ── 스타일 ──
        private GUIStyle _titleStyle;
        private GUIStyle _slotStyle;
        private GUIStyle _resultStyle;
        private GUIStyle _categoryHeaderStyle;
        private bool _stylesInitialized;

        protected override void OnShow()
        {
            base.OnShow();
            _slot1 = null;
            _slot2 = null;
            _resultMessage = "";
            _hasResult = false;
            _stylesInitialized = false;
            Debug.Log("[CraftingUI] 크래프트 테이블 열림");
        }

        protected override void OnHide()
        {
            base.OnHide();
            Debug.Log("[CraftingUI] 크래프트 테이블 닫힘");
        }

        /// <summary>
        /// 외부에서 강제로 열기 (CraftingStation에서 호출)
        /// </summary>
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
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _slotStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.2f, 0.2f, 0.25f, 0.9f)) }
            };

            _resultStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 1f, 0.7f), background = MakeTexture(1, 1, new Color(0.1f, 0.25f, 0.1f, 0.8f)) }
            };

            _categoryHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
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
            UIStyleManager.DrawTitle(_winRect, "  🧪 크래프트 테이블");
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
            GUILayout.Label("  🧪 크래프트 테이블", _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("닫기 X", GUILayout.Width(60), GUILayout.Height(24)))
            {
                Hide();
                return;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ── 재료 슬롯 2개 ──
            GUILayout.BeginHorizontal(GUILayout.Height(80));
            DrawMaterialSlot("재료 1", ref _slot1);
            GUILayout.FlexibleSpace();
            DrawMaterialSlot("재료 2", ref _slot2);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ── 조합 버튼 ──
            bool canCraft = _slot1 != null && _slot2 != null;
            GUI.enabled = canCraft;
            if (GUILayout.Button("▼ 조합하기 ▼", GUILayout.Height(36)))
            {
                TryCraft();
            }
            GUI.enabled = true;

            GUILayout.Space(6);

            // ── 결과 표시 ──
            if (_hasResult)
            {
                string resultText = string.IsNullOrEmpty(_resultItemName)
                    ? $"{_resultMessage}"
                    : $"{_resultMessage}\n효과: {_resultEffect}";
                GUILayout.Box(resultText, _resultStyle, GUILayout.Height(50));
            }
            else if (!string.IsNullOrEmpty(_resultMessage))
            {
                var style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.6f, 0.4f), background = MakeTexture(1, 1, new Color(0.25f, 0.1f, 0.1f, 0.8f)) }
                };
                GUILayout.Box(_resultMessage, style, GUILayout.Height(40));
            }
            else
            {
                GUILayout.Box("재료 2개를 선택하고 조합해보세요.", GUILayout.Height(36));
            }

            GUILayout.Space(4);

            // ── 구분선 ──
            GUILayout.Label("─── 인벤토리 ───", _categoryHeaderStyle);

            // ── 인벤토리 그리드 (스크롤) ──
            float availableWidth = _windowWidth - 30;
            float itemSlotSize = Mathf.Min(90, (availableWidth - (_inventoryGridColumns - 1) * 6) / _inventoryGridColumns);
            float gridHeight = _windowHeight - 280;
            if (gridHeight < 80) gridHeight = 80;

            _inventoryScrollPos = GUILayout.BeginScrollView(_inventoryScrollPos, GUILayout.Height(gridHeight));

            var inventory = PlayerInventory.Instance;
            if (inventory != null)
            {
                var slots = inventory.GetAllSlots();
                int cols = Mathf.Max(1, (int)(availableWidth / (itemSlotSize + 6)));
                if (cols < 1) cols = 1;

                GUILayout.BeginVertical();
                int idx = 0;
                foreach (var slot in slots)
                {
                    if (slot == null || slot.item == null || slot.count <= 0)
                    {
                        idx++;
                        continue;
                    }

                    if (idx % cols == 0)
                        GUILayout.BeginHorizontal();

                    DrawInventoryItemSlot(slot, itemSlotSize);

                    idx++;
                    if (idx % cols == 0)
                        GUILayout.EndHorizontal();
                }

                // 마지막 행 닫기
                if (idx % cols != 0)
                    GUILayout.EndHorizontal();

                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();

            GUILayout.Space(4);

            // ── 발견된 조합 카운트 ──
            int totalCombos = HerbComboDatabase.AllCombos.Count;
            int discovered = RecipeDiscoverySystem.DiscoveredCount;
            GUILayout.Label($"발견된 조합: {discovered:D2}/{totalCombos}");

            GUILayout.EndArea();
        }

        /// <summary>
        /// 재료 슬롯 하나를 그립니다.
        /// </summary>
        private void DrawMaterialSlot(string label, ref PlayerInventory.ItemData slot)
        {
            GUILayout.BeginVertical(GUILayout.Width(160), GUILayout.Height(80));

            GUILayout.Label(label, GUILayout.Height(18));

            Rect slotRect = GUILayoutUtility.GetRect(120, 54);
            GUI.Box(slotRect, "", _slotStyle);

            if (slot != null)
            {
                // 아이템 이름 표시
                GUI.Label(new Rect(slotRect.x + 4, slotRect.y + 4, slotRect.width - 8, 20), slot.displayName);

                // 우클릭 감지 → 슬롯 비우기
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && slotRect.Contains(Event.current.mousePosition))
                {
                    slot = null;
                    _hasResult = false;
                    _resultMessage = "";
                    Event.current.Use();
                }

                // 작은 X 버튼
                if (GUI.Button(new Rect(slotRect.xMax - 20, slotRect.y + 4, 16, 16), "X"))
                {
                    slot = null;
                    _hasResult = false;
                    _resultMessage = "";
                }

                // 툴팁 (TooltipWindow 시스템)
                if (slotRect.Contains(Event.current.mousePosition))
                {
                    var td = new ItemTooltipData
                    {
                        itemName = slot.displayName,
                        description = slot.description,
                        effects = slot.effects,
                        rarity = slot.rarity,
                        category = slot.category,
                        maxDurability = slot.maxDurability,
                        currentDurability = slot.maxDurability,
                        count = 1
                    };
                    TooltipWindow.Instance.ShowTooltip(td, Event.current.mousePosition);
                }
            }
            else
            {
                GUI.Label(slotRect, "  [비어있음]\n  (아이템 클릭)", _slotStyle);
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 인벤토리 아이템 슬롯을 그립니다. 클릭 시 재료 슬롯에 배치됩니다.
        /// </summary>
        private void DrawInventoryItemSlot(PlayerInventory.ItemSlot invSlot, float size)
        {
            var item = invSlot.item;
            string label = $"{item.displayName}\nx{invSlot.count}";

            Rect rect = GUILayoutUtility.GetRect(size, size);

            // 배경
            Color bgColor = item.category == PlayerInventory.ItemCategory.Herb
                ? new Color(0.15f, 0.4f, 0.15f, 0.8f)
                : new Color(0.2f, 0.2f, 0.2f, 0.8f);

            // 이미 재료 슬롯에 있는 아이템이면 하이라이트
            if ((_slot1 != null && _slot1.id == item.id) || (_slot2 != null && _slot2.id == item.id))
                bgColor = new Color(0.3f, 0.6f, 0.3f, 0.9f);

            var slotBg = MakeTexture(1, 1, bgColor);
            GUI.DrawTexture(rect, slotBg);

            // 아이콘 (색상 사각형 fallback)
            Color iconColor = GetCategoryColor(item.category);
            GUI.color = iconColor;
            GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, size - 8, size - 24), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 이름 + 개수
            GUI.Label(new Rect(rect.x + 2, rect.y + size - 22, rect.width - 4, 20), label, new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            });

            // 클릭 감지
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                // 재료 슬롯에 배치
                AssignToMaterialSlot(item);
                Event.current.Use();
            }

            // 툴팁 (TooltipWindow 시스템)
            if (rect.Contains(Event.current.mousePosition))
            {
                var td = new ItemTooltipData
                {
                    itemName = item.displayName,
                    description = item.description,
                    effects = item.effects,
                    rarity = item.rarity,
                    category = item.category,
                    maxDurability = item.maxDurability,
                    currentDurability = invSlot.currentDurability,
                    count = invSlot.count
                };
                TooltipWindow.Instance.ShowTooltip(td, Event.current.mousePosition);
            }
        }

        /// <summary>
        /// 아이템을 재료 슬롯에 배치합니다.
        /// </summary>
        private void AssignToMaterialSlot(PlayerInventory.ItemData item)
        {
            _resultMessage = "";
            _hasResult = false;

            // 이미 슬롯에 있는 경우 교체
            if (_slot1 != null && _slot1.id == item.id)
            {
                // 같은 아이템이면 아무 동작 안 함
                return;
            }
            if (_slot2 != null && _slot2.id == item.id)
            {
                return;
            }

            if (_slot1 == null)
                _slot1 = item;
            else if (_slot2 == null)
                _slot2 = item;
            else
                // 둘 다 차있으면 slot1 교체
                _slot1 = item;
        }

        /// <summary>
        /// 조합 시도 (성공/실패 시스템 적용)
        /// </summary>
        private void TryCraft()
        {
            if (_slot1 == null || _slot2 == null)
            {
                _resultMessage = "재료를 2개 선택해주세요.";
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

            // 인벤토리에 재료가 실제로 있는지 확인
            if (!inventory.HasItem(_slot1.id) || inventory.GetItemCount(_slot1.id) < 1)
            {
                _resultMessage = $"'{_slot1.displayName}'이(가) 인벤토리에 없습니다.";
                _hasResult = false;
                return;
            }
            if (!inventory.HasItem(_slot2.id) || inventory.GetItemCount(_slot2.id) < 1)
            {
                _resultMessage = $"'{_slot2.displayName}'이(가) 인벤토리에 없습니다.";
                _hasResult = false;
                return;
            }

            // HerbComboDatabase에서 조합 검색
            string key = MakeKey(_slot1.id, _slot2.id);
            var allCombos = HerbComboDatabase.AllCombos;

            if (allCombos.TryGetValue(key, out var comboResult))
            {
                // 재료 등급 추정
                string grade1 = CraftSuccessSystem.GetGradeFromItemId(_slot1.id);
                string grade2 = CraftSuccessSystem.GetGradeFromItemId(_slot2.id);

                // 성공/실패 판정 (연금술 = true)
                CraftResult result = CraftSuccessSystem.ExecuteCraft(true, grade1, grade2);

                switch (result)
                {
                    case CraftResult.Success:
                        // 재료 차감
                        inventory.RemoveItem(_slot1.id, 1);
                        inventory.RemoveItem(_slot2.id, 1);

                        // 결과물 아이템 생성 및 지급
                        var resultItem = CreateResultItem(comboResult);
                        if (resultItem != null)
                            inventory.AddItem(resultItem, 1);

                        // 발견 등록
                        RecipeDiscoverySystem.MarkDiscovered(comboResult.resultName);

                        // 결과 표시
                        _resultMessage = $"🟢 제작 성공! '{comboResult.resultName}' 획득!";
                        _resultItemName = comboResult.resultName;
                        _resultEffect = comboResult.effect;
                        _hasResult = true;

                        // 경험치 획득 (제작 성공 시 +3~10)
                        if (PlayerStats.Instance != null)
                            PlayerStats.Instance.AddExp(Random.Range(3, 11));

                        // Phase 8.3: 제작 성공 사운드
                        SoundManager.Instance?.PlaySFX("craft_success");

                        Debug.Log($"[CraftingUI] 조합 성공: {_slot1.displayName} + {_slot2.displayName} → {comboResult.resultName}");

                        // 슬롯 비우기
                        _slot1 = null;
                        _slot2 = null;
                        break;

                    case CraftResult.Fail_MaterialPreserved:
                        _resultMessage = $"🟡 제작 실패... 재료가 보존되었다.";
                        _hasResult = false;
                        _resultItemName = "";
                        _resultEffect = "";
                        // Phase 8.3: 제작 실패 사운드
                        SoundManager.Instance?.PlaySFX("craft_fail");
                        Debug.Log($"[CraftingUI] 조합 실패 (재료보존): {_slot1.displayName} + {_slot2.displayName}");
                        break;

                    case CraftResult.Fail_MaterialDestroyed:
                        // 재료 1개 소멸 (랜덤 선택)
                        bool destroySlot1 = Random.value < 0.5f;
                        string destroyedName = destroySlot1 ? _slot1.displayName : _slot2.displayName;
                        string destroyedId = destroySlot1 ? _slot1.id : _slot2.id;
                        inventory.RemoveItem(destroyedId, 1);

                        _resultMessage = $"🔴 제작 실패! '{destroyedName}'이(가) 소멸했다!";
                        _hasResult = false;
                        _resultItemName = "";
                        _resultEffect = "";
                        // Phase 8.3: 제작 실패 사운드
                        SoundManager.Instance?.PlaySFX("craft_fail");
                        Debug.Log($"[CraftingUI] 조합 실패 (재료소멸): {destroyedName} 파괴됨");

                        // 소멸된 슬롯 비우기
                        if (destroySlot1) _slot1 = null;
                        else _slot2 = null;
                        break;

                    case CraftResult.Fail_Burned:
                        // 재료 전소
                        inventory.RemoveItem(_slot1.id, 1);
                        inventory.RemoveItem(_slot2.id, 1);

                        _resultMessage = "💥 제작 실패! 모든 재료가 전소했다!";
                        _hasResult = false;
                        _resultItemName = "";
                        _resultEffect = "";
                        // Phase 8.3: 제작 실패 사운드
                        SoundManager.Instance?.PlaySFX("craft_fail");
                        Debug.Log($"[CraftingUI] 조합 실패 (전소): {_slot1.displayName} + {_slot2.displayName} 소멸");

                        _slot1 = null;
                        _slot2 = null;
                        break;
                }
            }
            else
            {
                // displayName 기반으로 재시도 (HerbDatabase lookup)
                bool foundByDisplayName = false;
                var info1 = HerbDatabase.GetHerbInfoByDisplayName(_slot1.displayName);
                var info2 = HerbDatabase.GetHerbInfoByDisplayName(_slot2.displayName);

                if (!string.IsNullOrEmpty(info1.id) && !string.IsNullOrEmpty(info2.id))
                {
                    string altKey = MakeKey(info1.id, info2.id);
                    if (allCombos.TryGetValue(altKey, out var altCombo))
                    {
                        foundByDisplayName = true;

                        string grade1 = CraftSuccessSystem.GetGradeFromItemId(info1.id);
                        string grade2 = CraftSuccessSystem.GetGradeFromItemId(info2.id);

                        CraftResult result = CraftSuccessSystem.ExecuteCraft(true, grade1, grade2);

                        switch (result)
                        {
                            case CraftResult.Success:
                                inventory.RemoveItem(_slot1.id, 1);
                                inventory.RemoveItem(_slot2.id, 1);

                                var resultItem = CreateResultItem(altCombo);
                                if (resultItem != null)
                                    inventory.AddItem(resultItem, 1);

                                RecipeDiscoverySystem.MarkDiscovered(altCombo.resultName);

                                _resultMessage = $"🟢 제작 성공! '{altCombo.resultName}' 획득!";
                                _resultItemName = altCombo.resultName;
                                _resultEffect = altCombo.effect;
                                _hasResult = true;

                                if (PlayerStats.Instance != null)
                                    PlayerStats.Instance.AddExp(Random.Range(3, 11));

                                // Phase 8.3: 제작 성공 사운드
                                SoundManager.Instance?.PlaySFX("craft_success");

                                Debug.Log($"[CraftingUI] 조합 성공 (displayName): {_slot1.displayName} + {_slot2.displayName} → {altCombo.resultName}");

                                _slot1 = null;
                                _slot2 = null;
                                break;

                            case CraftResult.Fail_MaterialPreserved:
                                _resultMessage = $"🟡 제작 실패... 재료가 보존되었다.";
                                _hasResult = false;
                                _resultItemName = "";
                                _resultEffect = "";
                                // Phase 8.3: 제작 실패 사운드
                                SoundManager.Instance?.PlaySFX("craft_fail");
                                break;

                            case CraftResult.Fail_MaterialDestroyed:
                                bool destroySlot1 = Random.value < 0.5f;
                                string destroyedName = destroySlot1 ? _slot1.displayName : _slot2.displayName;
                                string destroyedId = destroySlot1 ? _slot1.id : _slot2.id;
                                inventory.RemoveItem(destroyedId, 1);

                                _resultMessage = $"🔴 제작 실패! '{destroyedName}'이(가) 소멸했다!";
                                _hasResult = false;
                                _resultItemName = "";
                                _resultEffect = "";
                                // Phase 8.3: 제작 실패 사운드
                                SoundManager.Instance?.PlaySFX("craft_fail");

                                if (destroySlot1) _slot1 = null;
                                else _slot2 = null;
                                break;

                            case CraftResult.Fail_Burned:
                                inventory.RemoveItem(_slot1.id, 1);
                                inventory.RemoveItem(_slot2.id, 1);

                                _resultMessage = "💥 제작 실패! 모든 재료가 전소했다!";
                                _hasResult = false;
                                _resultItemName = "";
                                _resultEffect = "";
                                // Phase 8.3: 제작 실패 사운드
                                SoundManager.Instance?.PlaySFX("craft_fail");

                                _slot1 = null;
                                _slot2 = null;
                                break;
                        }
                    }
                }

                if (!foundByDisplayName)
                {
                    _resultMessage = "조합 불가 — 해당 조합법이 없습니다.";
                    _hasResult = false;
                    _resultItemName = "";
                    _resultEffect = "";
                    Debug.Log($"[CraftingUI] 조합 실패: {_slot1.id} + {_slot2.id} → key={key} — 조합법 없음");
                }
            }
        }

        /// <summary>
        /// HerbComboResult로부터 PlayerInventory.ItemData 생성
        /// </summary>
        private PlayerInventory.ItemData CreateResultItem(HerbComboResult combo)
        {
            var category = PlayerInventory.ItemCategory.Potion;
            string name = combo.resultName;

            // 카테고리 추론 (CraftingHelper.CreatePotionItem 로직과 동일)
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
                id = $"combo_{combo.resultName}",
                displayName = combo.resultName,
                description = combo.effect,
                category = category,
                maxStack = 10
            };
        }

        /// <summary>
        /// 아이템 카테고리에 따른 색상 반환
        /// </summary>
        private Color GetCategoryColor(PlayerInventory.ItemCategory category)
        {
            switch (category)
            {
                case PlayerInventory.ItemCategory.Herb:    return new Color(0.2f, 0.7f, 0.2f);
                case PlayerInventory.ItemCategory.Meat:    return new Color(0.8f, 0.4f, 0.2f);
                case PlayerInventory.ItemCategory.Food:    return new Color(0.9f, 0.6f, 0.2f);
                case PlayerInventory.ItemCategory.Potion:  return new Color(0.3f, 0.5f, 0.9f);
                case PlayerInventory.ItemCategory.Material:return new Color(0.6f, 0.6f, 0.6f);
                case PlayerInventory.ItemCategory.Drug:    return new Color(0.8f, 0.2f, 0.8f);
                case PlayerInventory.ItemCategory.Weapon:  return new Color(0.8f, 0.3f, 0.1f);
                case PlayerInventory.ItemCategory.Armor:   return new Color(0.3f, 0.5f, 0.7f);
                case PlayerInventory.ItemCategory.Tool:    return new Color(0.7f, 0.5f, 0.2f);
                default:                                   return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        /// <summary>
        /// 알파벳 순 정렬된 조합 키 생성
        /// </summary>
        private static string MakeKey(string id1, string id2)
        {
            return string.CompareOrdinal(id1, id2) < 0 ? $"{id1}_{id2}" : $"{id2}_{id1}";
        }
    }
}