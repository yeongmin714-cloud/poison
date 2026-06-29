using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Themes;
using UnityEngine;
using ProjectName.Core.Data;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 5.6.3: 장비창 — 6개 장비 슬롯(헬멧/갑옷/무기/신발/장갑/Back) 표시 및 관리.
    /// E 키로 열기/닫기, IMGUI 다크 테마.
    /// </summary>
    public class EquipmentWindow : UIWindow
    {
        [Header("Equipment Window Settings")]
        [SerializeField] private EquipmentManager _equipmentManager;

        // ===== 레이아웃 상수 =====
        private const float WINDOW_WIDTH = 600f;
        private const float WINDOW_HEIGHT = 540f;
        private const float TITLE_BAR_HEIGHT = 40f;
        private const float SLOT_HEIGHT = 64f;
        private const float SLOT_GAP = 6f;
        private const float BUTTON_AREA_HEIGHT = 60f;

        // ===== 선택 상태 =====
        private EquipmentManager.EquipmentSlot _selectedSlot = EquipmentManager.EquipmentSlot.Helmet;
        private bool _hasSelection;

        // ===== 다크 테마 색상 (InventoryWindow와 일관성) =====
        private static readonly Color ColorBg = new Color(0.18f, 0.13f, 0.16f, 0.92f);
        private static readonly Color ColorTitleBar = new Color(0.12f, 0.09f, 0.11f, 1f);
        private static readonly Color ColorSlotBg = new Color(0.22f, 0.17f, 0.14f, 0.9f);
        private static readonly Color ColorSlotHover = new Color(0.35f, 0.25f, 0.18f, 0.9f);
        private static readonly Color ColorSlotEmpty = new Color(0.15f, 0.12f, 0.10f, 0.7f);
        private static readonly Color ColorSlotSelected = new Color(0.40f, 0.28f, 0.20f, 1f);
        private static readonly Color ColorTextPrimary = new Color(0.92f, 0.88f, 0.80f, 1f);
        private static readonly Color ColorTextSecondary = new Color(0.70f, 0.65f, 0.60f, 1f);
        private static readonly Color ColorTextDim = new Color(0.50f, 0.45f, 0.40f, 1f);
        private static readonly Color ColorAccent = new Color(0.80f, 0.60f, 0.20f, 1f);
        private static readonly Color ColorBorder = new Color(0.12f, 0.09f, 0.11f, 1f);
        private static readonly Color ColorDestructive = new Color(0.80f, 0.25f, 0.20f, 1f);

        // ===== GUIStyle 캐시 =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleSlotLabel;
        private GUIStyle _styleSlotValue;
        private GUIStyle _styleEmptyText;
        private GUIStyle _styleInfoText;
        private GUIStyle _styleButton;
        private GUIStyle _stylePanelBox;
        private GUIStyle _styleDurabilityLabel; // C01: 캐시된 내구도 레이블 스타일 (매 프레임 new GUIStyle 방지)
        private bool _stylesInitialized;

        // ===== 슬롯 정의 (표시 순서) =====
        private struct SlotDef
        {
            public string icon;
            public string label;
            public EquipmentManager.EquipmentSlot slot;
        }

        private static readonly SlotDef[] _slotDefs = new SlotDef[]
        {
            new SlotDef { icon = "🪖", label = "헬멧", slot = EquipmentManager.EquipmentSlot.Helmet },
            new SlotDef { icon = "👕", label = "갑옷", slot = EquipmentManager.EquipmentSlot.Armor },
            new SlotDef { icon = "🗡️", label = "무기", slot = EquipmentManager.EquipmentSlot.Weapon },
            new SlotDef { icon = "👢", label = "신발", slot = EquipmentManager.EquipmentSlot.Shoes },
            new SlotDef { icon = "🧤", label = "장갑", slot = EquipmentManager.EquipmentSlot.Gloves },
            new SlotDef { icon = "🎒", label = "Back", slot = EquipmentManager.EquipmentSlot.Back }
        };

        protected override void Awake()
        {
            base.Awake();
            // Phase 33 UI-03: 장비창 테마 적용
            ApplyTheme(Phase33_Themes.CreateMedievalInventoryTheme());
            if (_equipmentManager == null)
                _equipmentManager = FindAnyObjectByType<EquipmentManager>();
        }

        protected override void OnShow()
        {
            Debug.Log("[EquipmentWindow] 열림");
            _hasSelection = false;
        }

        protected override void OnHide()
        {
            Debug.Log("[EquipmentWindow] 닫힘");
        }

        // ===== 텍스처 정리 =====
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (!_stylesInitialized) return;

            if (_styleTitle?.normal?.background != null) Destroy(_styleTitle.normal.background);
            if (_styleSlotLabel?.normal?.background != null) Destroy(_styleSlotLabel.normal.background);
            if (_styleSlotValue?.normal?.background != null) Destroy(_styleSlotValue.normal.background);
            if (_styleEmptyText?.normal?.background != null) Destroy(_styleEmptyText.normal.background);
            if (_styleInfoText?.normal?.background != null) Destroy(_styleInfoText.normal.background);
            if (_styleDurabilityLabel?.normal?.background != null) Destroy(_styleDurabilityLabel.normal.background);
            if (_styleButton?.normal?.background != null) Destroy(_styleButton.normal.background);
            if (_styleButton?.hover?.background != null) Destroy(_styleButton.hover.background);
            if (_styleButton?.active?.background != null) Destroy(_styleButton.active.background);
            if (_stylePanelBox?.normal?.background != null) Destroy(_stylePanelBox.normal.background);
            _stylesInitialized = false;
        }

        // ===== 스타일 초기화 =====
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary },
                padding = new RectOffset(16, 4, 0, 0)
            };

            _styleSlotLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextPrimary },
                padding = new RectOffset(10, 4, 0, 0)
            };

            _styleSlotValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextSecondary },
                padding = new RectOffset(10, 4, 0, 0)
            };

            _styleEmptyText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextDim },
                padding = new RectOffset(10, 4, 0, 0)
            };

            _styleInfoText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorTextDim },
                padding = new RectOffset(10, 4, 0, 0)
            };

            _styleDurabilityLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColorTextPrimary }
            };

            _styleButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 2, 2),
                normal = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorSlotHover) },
                hover = { textColor = ColorAccent, background = MakeTexture(1, 1, new Color(0.45f, 0.32f, 0.22f, 1f)) },
                active = { textColor = ColorTextPrimary, background = MakeTexture(1, 1, ColorSlotSelected) }
            };

            _stylePanelBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, ColorBg), textColor = ColorTextPrimary },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _stylesInitialized = true;
        }

        // ===== OnGUI — IMGUI 렌더링 =====
        protected override void OnGUI()
        {
            base.OnGUI();
            if (!IsOpen) return;
            if (Event.current == null) return; // C02: NRE 방지

            InitStyles();

            float x = (Screen.width - WINDOW_WIDTH) / 2;
            float y = (Screen.height - WINDOW_HEIGHT) / 2;

            // === 배경 박스 ===
            GUI.Box(new Rect(x, y, WINDOW_WIDTH, WINDOW_HEIGHT), "", _stylePanelBox);
            DrawColoredRect(new Rect(x, y, WINDOW_WIDTH, 2), ColorBorder);

            // === 타이틀 바 ===
            DrawColoredRect(new Rect(x, y + 2, WINDOW_WIDTH, TITLE_BAR_HEIGHT), ColorTitleBar);
            GUI.Label(new Rect(x, y + 2, WINDOW_WIDTH - 60, TITLE_BAR_HEIGHT), "  🛡️ 장비창", _styleTitle);

            // 닫기 버튼
            if (GUI.Button(new Rect(x + WINDOW_WIDTH - 44, y + 6, 36, 28), "✕", _styleButton))
            {
                Hide();
                return;
            }

            DrawColoredRect(new Rect(x, y + TITLE_BAR_HEIGHT + 2, WINDOW_WIDTH, 2), ColorBorder);

            // === 장비 슬롯 목록 ===
            float listX = x + 8;
            float listY = y + TITLE_BAR_HEIGHT + 8;
            float listWidth = WINDOW_WIDTH - 16;
            float listHeight = WINDOW_HEIGHT - TITLE_BAR_HEIGHT - BUTTON_AREA_HEIGHT - 24;

            DrawEquipmentSlots(listX, listY, listWidth, listHeight);

            // === 하단 버튼 영역 ===
            float buttonY = listY + listHeight + 4;
            DrawBottomButtons(x, buttonY);
        }

        // ===== 장비 슬롯 그리기 =====
        private void DrawEquipmentSlots(float panelX, float panelY, float panelWidth, float panelHeight)
        {
            // 배경
            DrawColoredRect(new Rect(panelX - 4, panelY - 2, panelWidth + 8, panelHeight + 4), ColorSlotEmpty);

            float currentY = panelY;

            for (int i = 0; i < _slotDefs.Length; i++)
            {
                var slotDef = _slotDefs[i];
                var slotData = _equipmentManager != null ? _equipmentManager.GetSlotData(slotDef.slot) : null;
                bool isEmpty = slotData == null || string.IsNullOrEmpty(slotData.itemId);

                Rect slotRect = new Rect(panelX, currentY, panelWidth, SLOT_HEIGHT);

                // 슬롯 배경 (호버 시 강조)
                bool isHovered = slotRect.Contains(Event.current.mousePosition);
                Color slotColor = isHovered ? ColorSlotHover : ColorSlotBg;
                DrawColoredRect(slotRect, slotColor);

                // 슬롯 테두리
                DrawColoredRect(new Rect(slotRect.x, slotRect.y, slotRect.width, 1), ColorBorder);

                // === 아이콘 (색상 원) ===
                float iconSize = 42f;
                float iconX = slotRect.x + 10;
                float iconY = slotRect.y + (SLOT_HEIGHT - iconSize) / 2;

                if (!isEmpty && slotData.itemData != null)
                {
                    // 아이템 카테고리 색상으로 아이콘 표시
                    GUI.color = GetCategoryColor(slotData.itemData.category);
                    GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                else
                {
                    // 빈 슬롯 — 더 어두운 아이콘
                    GUI.color = ColorTextDim;
                    GUI.DrawTexture(new Rect(iconX, iconY, iconSize, iconSize), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                // === 슬롯 라벨 ===
                float labelX = iconX + iconSize + 10;
                float labelWidth = panelWidth - labelX - 130; // 오른쪽 내구도 바 공간 확보

                GUI.Label(new Rect(labelX, slotRect.y + 2, labelWidth, 22),
                    $"{slotDef.icon} {slotDef.label}:", _styleSlotLabel);

                if (!isEmpty && slotData.itemData != null)
                {
                    // 아이템 이름
                    string itemName = slotData.itemData.displayName;
                    GUI.Label(new Rect(labelX, slotRect.y + 24, labelWidth, 22),
                        itemName, _styleSlotValue);

                    // === 내구도 바 ===
                    float durBarX = slotRect.x + panelWidth - 120;
                    float durBarY = slotRect.y + (SLOT_HEIGHT - 12) / 2;
                    float durBarWidth = 110;
                    float durBarHeight = 14f;

                    if (slotData.itemData.maxDurability > 0)
                    {
                        // 내구도 비율
                        float ratio = (float)slotData.currentDurability / slotData.itemData.maxDurability;
                        Color durColor = ratio >= 0.6f ? Color.green : (ratio >= 0.3f ? Color.yellow : Color.red);

                        // 배경
                        DrawColoredRect(new Rect(durBarX, durBarY, durBarWidth, durBarHeight), new Color(0.15f, 0.15f, 0.15f, 0.8f));

                        if (ratio > 0f)
                        {
                            DrawColoredRect(new Rect(durBarX, durBarY, durBarWidth * Mathf.Clamp01(ratio), durBarHeight), durColor);
                        }

                        // 내구도 텍스트
                        string durLabel = slotData.currentDurability <= 0 ? "🔴 파괴됨" : $"{GetDurabilityEmoji(ratio)} {slotData.currentDurability}/{slotData.itemData.maxDurability}";
                        GUI.Label(new Rect(durBarX, durBarY, durBarWidth, durBarHeight), durLabel, _styleDurabilityLabel);
                    }
                    else
                    {
                        // 내구도 없는 아이템 (소모품 등)
                        GUI.Label(new Rect(durBarX, durBarY, durBarWidth, durBarHeight), "∞", _styleInfoText);
                    }
                }
                else
                {
                    // 빈 슬롯
                    GUI.Label(new Rect(labelX, slotRect.y + 6, labelWidth, 22),
                        "[비어있음]", _styleEmptyText);

                    // 우측 빈 공간
                    float emptyX = slotRect.x + panelWidth - 120;
                    float emptyY = slotRect.y + 8;
                    GUI.Label(new Rect(emptyX, emptyY, 110, 16), "- - -", _styleInfoText);
                }

                // === 클릭 처리 (슬롯 클릭 → 장비 해제) ===
                if (Event.current.type == EventType.MouseDown && slotRect.Contains(Event.current.mousePosition))
                {
                    if (!isEmpty && _equipmentManager != null)
                    {
                        _selectedSlot = slotDef.slot;
                        _hasSelection = true;
                    }
                    Event.current.Use(); // C02: 빈 슬롯 클릭도 이벤트 소비 (하위 전파 방지)
                }

                currentY += SLOT_HEIGHT + SLOT_GAP;
            }

            // 마지막 구분선
            DrawColoredRect(new Rect(panelX, currentY, panelWidth, 1), ColorBorder);
        }

        // ===== 하단 버튼 영역 =====
        private void DrawBottomButtons(float panelX, float buttonY)
        {
            float btnWidth = 160f;
            float btnHeight = 32f;
            float gap = 12f;
            float totalWidth = btnWidth * 2 + gap;
            float leftX = panelX + (WINDOW_WIDTH - totalWidth) / 2;

            // 장비 해제 버튼 (선택된 슬롯이 있을 때)
            GUI.enabled = _hasSelection && _equipmentManager != null;
            if (GUI.Button(new Rect(leftX, buttonY + 4, btnWidth, btnHeight), "🔓 장비 해제", _styleButton))
            {
                if (_equipmentManager != null)
                {
                    bool success = _equipmentManager.UnequipSlot(_selectedSlot);
                    if (success)
                    {
                        _hasSelection = false;
                        Debug.Log($"[EquipmentWindow] {_selectedSlot} 장비 해제 완료");
                    }
                }
            }

            // 아이템 정보 버튼
            if (GUI.Button(new Rect(leftX + btnWidth + gap, buttonY + 4, btnWidth, btnHeight), "ℹ️ 아이템 정보", _styleButton))
            {
                if (_hasSelection && _equipmentManager != null)
                {
                    var slotData = _equipmentManager.GetSlotData(_selectedSlot);
                    if (slotData != null && slotData.itemData != null)
                    {
                        Debug.Log($"[EquipmentWindow] {slotData.itemData.displayName}: {slotData.itemData.description}");
                    }
                }
            }
            GUI.enabled = true;

            // 하단 도움말
            GUI.Label(new Rect(panelX + 8, buttonY + 38, WINDOW_WIDTH - 16, 24),
                "💡 슬롯 클릭 → 장비 해제", _styleInfoText);
        }

        // ===== 유틸리티 =====
        private static string GetDurabilityEmoji(float ratio)
        {
            if (ratio >= 0.6f) return "🟢";
            if (ratio >= 0.3f) return "🟡";
            return "🔴";
        }

        private static Color GetCategoryColor(PlayerInventory.ItemCategory category)
        {
            switch (category)
            {
                case PlayerInventory.ItemCategory.Herb: return new Color(0.3f, 0.8f, 0.3f);
                case PlayerInventory.ItemCategory.Meat: return new Color(0.9f, 0.4f, 0.3f);
                case PlayerInventory.ItemCategory.Food: return new Color(0.9f, 0.7f, 0.3f);
                case PlayerInventory.ItemCategory.Potion: return new Color(0.5f, 0.3f, 0.9f);
                case PlayerInventory.ItemCategory.Material: return new Color(0.6f, 0.6f, 0.6f);
                case PlayerInventory.ItemCategory.Quest: return new Color(0.3f, 0.6f, 0.9f);
                case PlayerInventory.ItemCategory.Weapon: return new Color(0.9f, 0.3f, 0.3f);
                case PlayerInventory.ItemCategory.Armor: return new Color(0.3f, 0.5f, 0.9f);
                case PlayerInventory.ItemCategory.Tool: return new Color(0.7f, 0.5f, 0.2f);
                default: return Color.gray;
            }
        }

        // ===== 텍스처/렉트 헬퍼 =====
        private static Texture2D MakeTexture(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static void DrawColoredRect(Rect rect, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }
    }
}