using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Core;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 🔢 퀵슬롯 UI — 화면 하단 중앙에 6개 슬롯을 IMGUI로 표시.
    /// 키 1~6 으로 아이템 사용. 인벤토리(I) 열린 상태에서 우클릭으로 등록.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class QuickSlotUI : UIWindow
    {
        public static QuickSlotUI Instance { get; private set; }

        [Header("QuickSlot Settings")]
        [SerializeField] private float _slotSize = 48f;
        [SerializeField] private float _slotGap = 4f;
        [SerializeField] private float _bottomMargin = 60f;

        [Header("Colors")]
        [SerializeField] private Color _slotEmptyColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);
        [SerializeField] private Color _slotFilledColor = new Color(0.22f, 0.17f, 0.14f, 0.85f);
        [SerializeField] private Color _slotHoverColor = new Color(0.35f, 0.25f, 0.18f, 0.9f);
        [SerializeField] private Color _slotBorderColor = new Color(0.12f, 0.09f, 0.11f, 1f);
        [SerializeField] private Color _textColor = new Color(0.92f, 0.88f, 0.80f, 1f);
        [SerializeField] private Color _keyColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _countColor = new Color(0.80f, 0.60f, 0.20f, 1f);

        // ===== 캐시 =====
        private GUIStyle _styleKeyLabel;
        private GUIStyle _styleCountLabel;
        private GUIStyle _styleEmptySlot;
        private GUIStyle _styleSlotBg;
        private Texture2D _texWhite;
        private bool _stylesInitialized;
        private Rect[] _slotRects;
        private int _hoveredSlot = -1;

        public override void Awake()
                {
                    if (Instance != null && Instance != this)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    Instance = this;
                    // DontDestroyOnLoad은 서브씬 매니저가 관리 (Root GameObject에서만 유효)
                    if (transform.parent == null)
                        DontDestroyOnLoad(gameObject);

                    _slotRects = new Rect[6];
                }

                public override void OnDestroy()
                {
                    if (_texWhite != null)
                    {
                        Destroy(_texWhite);
                        _texWhite = null;
                    }
                }

                public override void OnGUI()
                {
                    if (QuickSlotManager.Instance == null) return;

                    InitStyles();
                    DrawQuickSlots();
                }
            if (QuickSlotManager.Instance == null) return;

            InitStyles();
            DrawQuickSlots();
        }

        // ===== 스타일 초기화 =====
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _texWhite = MakeTexture(1, 1, Color.white);

            // 키 번호 (1~6) — 작은 흰색 텍스트
            _styleKeyLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = _keyColor },
                padding = new RectOffset(3, 0, 2, 0)
            };

            // 수량 텍스트 (우측 하단)
            _styleCountLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerRight,
                normal = { textColor = _countColor },
                padding = new RectOffset(0, 3, 0, 2)
            };

            // 빈 슬롯 스타일
            _styleEmptySlot = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, _slotEmptyColor) },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            // 채워진 슬롯 배경
            _styleSlotBg = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, _slotFilledColor) },
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _stylesInitialized = true;
        }

        // ===== 퀵슬롯 렌더링 =====
        private void DrawQuickSlots()
        {
            int slotCount = QuickSlotManager.Instance.SlotCount;
            float totalWidth = slotCount * _slotSize + (slotCount - 1) * _slotGap;

            float startX = (Screen.width - totalWidth) * 0.5f;
            float startY = Screen.height - _slotSize - _bottomMargin;

            for (int i = 0; i < slotCount; i++)
            {
                float sx = startX + i * (_slotSize + _slotGap);
                Rect slotRect = new Rect(sx, startY, _slotSize, _slotSize);
                _slotRects[i] = slotRect;

                bool hasItem = QuickSlotManager.Instance.HasItemInSlot(i);
                bool isHovered = slotRect.Contains(Event.current.mousePosition);
                _hoveredSlot = isHovered ? i : _hoveredSlot;

                // === 슬롯 배경 ===
                if (hasItem)
                {
                    // 채워진 슬롯 — 호버 시 강조
                    Color bgColor = isHovered ? _slotHoverColor : _slotFilledColor;
                    var oldColor = GUI.color;
                    GUI.color = bgColor;
                    GUI.Box(slotRect, "", _styleSlotBg);
                    GUI.color = oldColor;
                }
                else
                {
                    // 빈 슬롯 — 반투명 회색
                    GUI.Box(slotRect, "", _styleEmptySlot);
                }

                // === 테두리 ===
                DrawColoredRect(new Rect(slotRect.x, slotRect.y, slotRect.width, 1), _slotBorderColor);
                DrawColoredRect(new Rect(slotRect.x, slotRect.y + slotRect.height - 1, slotRect.width, 1), _slotBorderColor);
                DrawColoredRect(new Rect(slotRect.x, slotRect.y, 1, slotRect.height), _slotBorderColor);
                DrawColoredRect(new Rect(slotRect.x + slotRect.width - 1, slotRect.y, 1, slotRect.height), _slotBorderColor);

                // === 키 번호 표시 (1~6) ===
                GUI.Label(new Rect(sx, startY, 20, 16), (i + 1).ToString(), _styleKeyLabel);

                // === 아이템이 있으면 아이콘 + 수량 표시 ===
                if (hasItem)
                {
                    var itemData = QuickSlotManager.Instance.GetItemInSlot(i);
                    if (itemData != null)
                    {
                        // 아이콘 (ItemIconDatabase 사용)
                        Texture2D iconTex = ItemIconDatabase.GetOrCreateIcon(itemData);
                        if (iconTex != null)
                        {
                            // 아이콘을 슬롯 중앙에 40×40 크기로 표시
                            float iconSize = 40f;
                            float iconOffset = (_slotSize - iconSize) * 0.5f;
                            GUI.DrawTexture(new Rect(sx + iconOffset, startY + iconOffset, iconSize, iconSize), iconTex);
                        }
                        else
                        {
                            // 폴백: 카테고리 색상 사각형
                            var oldColor = GUI.color;
                            GUI.color = GetCategoryColor(itemData.category);
                            float iconSize = 40f;
                            float iconOffset = (_slotSize - iconSize) * 0.5f;
                            GUI.DrawTexture(new Rect(sx + iconOffset, startY + iconOffset, iconSize, iconSize), _texWhite);
                            GUI.color = oldColor;
                        }

                        // 인벤토리에서 실제 아이템 개수 가져오기
                        int invCount = 0;
                        if (PlayerInventory.Instance != null)
                        {
                            invCount = PlayerInventory.Instance.GetItemCount(itemData.id);
                        }
                        if (invCount > 0)
                        {
                            GUI.Label(new Rect(sx, startY, _slotSize, _slotSize),
                                $"x{invCount}", _styleCountLabel);
                        }
                    }
                }

                // === 우클릭 처리 (인벤토리 열린 상태에서만) ===
                if (Event.current.type == EventType.MouseDown &&
                    Event.current.button == 1 &&
                    slotRect.Contains(Event.current.mousePosition))
                {
                    HandleRightClick(i);
                    Event.current.Use();
                }
            }
        }

        // ===== 키 입력 처리 =====
        private void HandleKeyInput()
        {
            if (QuickSlotManager.Instance == null) return;
            if (PlayerInventory.Instance == null) return;

            // 게임 일시정지 상태에서는 무시
            if (Time.timeScale == 0f) return;

            // UIManager가 있고 인벤토리가 열려있으면 퀵슬롯 키는 사용 등록 모드
            bool inventoryOpen = (UIManager.Instance != null &&
                                  UIManager.Instance.inventoryWindow != null &&
                                  UIManager.Instance.inventoryWindow.IsOpen);

            for (int i = 0; i < QuickSlotManager.Instance.SlotCount; i++)
            {
                KeyCode alphaKey = GetAlphaKey(i);
                if (alphaKey == KeyCode.None) continue;

                if (Input.GetKeyDown(alphaKey))
                {
                    if (inventoryOpen)
                    {
                        // 인벤토리 열린 상태: 현재 선택된 아이템을 퀵슬롯에 등록
                        HandleQuickSlotRegister(i);
                    }
                    else
                    {
                        // 일반 상태: 퀵슬롯 아이템 사용
                        HandleQuickSlotUse(i);
                    }
                }
            }
        }

        /// <summary>
        /// 키코드 1~6 반환
        /// </summary>
        private static KeyCode GetAlphaKey(int index)
        {
            switch (index)
            {
                case 0: return KeyCode.Alpha1;
                case 1: return KeyCode.Alpha2;
                case 2: return KeyCode.Alpha3;
                case 3: return KeyCode.Alpha4;
                case 4: return KeyCode.Alpha5;
                case 5: return KeyCode.Alpha6;
                default: return KeyCode.None;
            }
        }

        // ===== 퀵슬롯 사용 (키 입력) =====
        private void HandleQuickSlotUse(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6) return;

            string itemId = QuickSlotManager.Instance.GetItemIdInSlot(slotIndex);
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.Log($"[QuickSlotUI] 슬롯 {slotIndex + 1}이 비어있습니다.");
                return;
            }

            // 인벤토리에서 해당 itemId의 실제 슬롯 인덱스 찾기
            int invSlotIndex = FindInventorySlotIndex(itemId);
            if (invSlotIndex < 0)
            {
                Debug.Log($"[QuickSlotUI] '{itemId}' 아이템이 인벤토리에 없습니다. 슬롯을 초기화합니다.");
                QuickSlotManager.Instance.ClearSlot(slotIndex);
                return;
            }

            // 아이템 사용
            PlayerInventory.Instance.UseItem(invSlotIndex);
            Debug.Log($"[QuickSlotUI] 퀵슬롯 {slotIndex + 1} 사용: {itemId}");

            // 사용 후 개수가 0이면 슬롯 정리
            int remainingCount = PlayerInventory.Instance.GetItemCount(itemId);
            if (remainingCount <= 0)
            {
                QuickSlotManager.Instance.ClearSlot(slotIndex);
            }
        }

        // ===== 퀵슬롯 등록 (인벤토리 열린 상태) =====
        private void HandleQuickSlotRegister(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6) return;
            if (PlayerInventory.Instance == null) return;

            // InventoryWindow에서 현재 선택된 아이템 가져오기
            var invWindow = UIManager.Instance.inventoryWindow as InventoryWindow;
            if (invWindow == null)
            {
                Debug.LogWarning("[QuickSlotUI] InventoryWindow를 찾을 수 없습니다.");
                return;
            }

            if (!invWindow.HasSelectedItem())
            {
                Debug.Log("[QuickSlotUI] 인벤토리에서 선택된 아이템이 없습니다.");
                return;
            }

            var selectedItem = invWindow.GetSelectedItemData();
            if (selectedItem == null)
            {
                Debug.Log("[QuickSlotUI] 선택된 아이템 데이터가 null입니다.");
                return;
            }

            QuickSlotManager.Instance.SetSlot(slotIndex, selectedItem);
            Debug.Log($"[QuickSlotUI] 퀵슬롯 {slotIndex + 1}에 '{selectedItem.displayName}' 등록됨");
        }

        // ===== 우클릭 처리 (슬롯 내용 변경) =====
        private void HandleRightClick(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6) return;

            bool inventoryOpen = (UIManager.Instance != null &&
                                  UIManager.Instance.inventoryWindow != null &&
                                  UIManager.Instance.inventoryWindow.IsOpen);

            if (!inventoryOpen)
            {
                Debug.Log("[QuickSlotUI] 인벤토리가 열려있지 않아 퀵슬롯을 변경할 수 없습니다.");
                return;
            }

            // 우클릭 시: 이미 아이템이 있으면 제거, 없으면 인벤토리 선택 항목 등록
            if (QuickSlotManager.Instance.HasItemInSlot(slotIndex))
            {
                // 이미 등록된 아이템이 있으면 제거
                QuickSlotManager.Instance.ClearSlot(slotIndex);
                Debug.Log($"[QuickSlotUI] 퀵슬롯 {slotIndex + 1} 제거됨");
            }
            else
            {
                // 비어있으면 인벤토리에서 선택된 아이템 등록
                RegisterSelectedInventoryItem(slotIndex);
            }
        }

        /// <summary>
        /// 인벤토리에서 선택된 아이템을 퀵슬롯에 등록
        /// </summary>
        private void RegisterSelectedInventoryItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6) return;
            if (PlayerInventory.Instance == null) return;

            var invWindow = UIManager.Instance.inventoryWindow as InventoryWindow;
            if (invWindow == null) return;

            if (!invWindow.HasSelectedItem()) return;

            var selectedItem = invWindow.GetSelectedItemData();
            if (selectedItem == null) return;

            QuickSlotManager.Instance.SetSlot(slotIndex, selectedItem);
            Debug.Log($"[QuickSlotUI] 우클릭: 퀵슬롯 {slotIndex + 1}에 '{selectedItem.displayName}' 등록됨");
        }

        // ===== 유틸리티 =====

        /// <summary>
        /// 인벤토리에서 itemId가 있는 첫 번째 슬롯 인덱스 찾기
        /// </summary>
        private static int FindInventorySlotIndex(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return -1;
            if (PlayerInventory.Instance == null) return -1;

            var allSlots = PlayerInventory.Instance.GetAllSlots();
            if (allSlots == null) return -1;

            for (int i = 0; i < allSlots.Length; i++)
            {
                if (allSlots[i] != null && allSlots[i].item != null &&
                    allSlots[i].item.id == itemId && allSlots[i].count > 0)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 카테고리별 색상 반환 (InventoryWindow와 동일)
        /// </summary>
        private static Color GetCategoryColor(PlayerInventory.ItemCategory category)
        {
            switch (category)
            {
                case PlayerInventory.ItemCategory.Herb: return new Color(0.3f, 0.8f, 0.3f);
                case PlayerInventory.ItemCategory.Meat: return new Color(0.8f, 0.4f, 0.2f);
                case PlayerInventory.ItemCategory.Food: return new Color(0.9f, 0.8f, 0.2f);
                case PlayerInventory.ItemCategory.Potion: return new Color(0.7f, 0.3f, 0.8f);
                case PlayerInventory.ItemCategory.Material: return new Color(0.5f, 0.5f, 0.5f);
                case PlayerInventory.ItemCategory.Quest: return new Color(0.2f, 0.7f, 0.8f);
                case PlayerInventory.ItemCategory.Weapon: return new Color(0.8f, 0.3f, 0.3f);
                case PlayerInventory.ItemCategory.Armor: return new Color(0.3f, 0.3f, 0.8f);
                case PlayerInventory.ItemCategory.Tool: return new Color(0.6f, 0.4f, 0.2f);
                default: return Color.gray;
            }
        }

        /// <summary>1x1 텍스처 생성</summary>
        private static Texture2D MakeTexture(int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        /// <summary>컬러 사각형 그리기</summary>
        private void DrawColoredRect(Rect rect, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _texWhite);
            GUI.color = oldColor;
        }

        // ===== 공개 API (InventoryWindow 연동용) =====

        /// <summary>
        /// 외부에서 특정 아이템을 퀵슬롯에 등록 (InventoryWindow에서 호출)
        /// </summary>
        public void RegisterItem(int slotIndex, PlayerInventory.ItemData itemData)
        {
            if (slotIndex < 0 || slotIndex >= 6) return;
            if (itemData == null) return;
            if (QuickSlotManager.Instance == null) return;

            QuickSlotManager.Instance.SetSlot(slotIndex, itemData);
        }

        /// <summary>
        /// 외부에서 퀵슬롯 초기화 (InventoryWindow에서 호출)
        /// </summary>
        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6) return;
            QuickSlotManager.Instance?.ClearSlot(slotIndex);
        }
    }
}
