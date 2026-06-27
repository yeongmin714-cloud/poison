using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI;
using ProjectName.UI.Themes;
using System.Collections.Generic;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// 상점 윈도우 — 아이템 구매 및 판매 인터페이스.
    /// 구매 가능한 아이템 목록 표시 및 골드 거래 처리.
    /// </summary>
    public class ShopWindow : UIWindow
    {
        [Header("Shop Window")]
        [SerializeField] private Transform _itemsGridContainer; // 아이템 그리드 컨테이너
        [SerializeField] private GameObject _itemSlotPrefab;    // 아이템 슬롯 프리팹
        
        [Header("Shop Inventory")]
        [SerializeField] private List<ShopItem> _shopInventory = new List<ShopItem>(); // 상점에서 판매하는 아이템 목록
        
        /// <summary>테스트/외부 접근용 읽기 전용 인벤토리</summary>
        public IReadOnlyList<ShopItem> ShopInventory => _shopInventory;
        
        [Header("UI Texts")]
        [SerializeField] private UnityEngine.UI.Text _goldText; // 현재 골드 표시
        
        // 현재 표시중인 아이템 데이터
        private List<ShopItem> _currentItems;
        private Vector2 _scrollPosition;
        private int _selectedSlotIndex = -1;
        
        // OnGUI GC 최적화: 캐시된 표시 문자열
        private string _cachedGoldText;
        private int _cachedGoldValue = int.MinValue;
        private string[] _cachedPriceTexts;
        private string[] _cachedStockTexts;
        private Texture2D[] _cachedIconTextures;
        
        // ===== 상점 아이템 데이터 구조 =====
        [System.Serializable]
        public class ShopItem
        {
            public PlayerInventory.ItemData item;      // 판매할 아이템
            public int price;          // 가격 (골드)
            public int stock;          // 재고 (-1이면 무한, 0이면 품절, 양수면 남은 재고)
            public bool isRare;        // 희귀 아이템 여부
        }
        
        // ===== 커스텀 GUIStyle 캐시 =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleSlot;
        private GUIStyle _styleSlotSelected;
        private GUIStyle _styleItemName;
        private GUIStyle _styleItemPrice;
        private GUIStyle _styleItemStock;
        private GUIStyle _styleRareTag;
        private GUIStyle _styleBuyButton;
        private GUIStyle _styleSellButton;
        private GUIStyle _styleSlotLabel;
        private GUIStyle _styleEmptyText;
        private GUIStyle _stylePanelBox;
        private bool _stylesInitialized;
        private Texture2D _texWhite;
        
        protected override void Awake()
        {
            base.Awake();
            ApplyTheme(Phase33_Themes.CreateMedievalShopTheme());
            InitializeShopInventory(); // 상점 초기 재고 설정
        }
        
        protected override void OnShow()
        {
            Debug.Log("[ShopWindow] 열림");
            _selectedSlotIndex = -1;
            RefreshShopItems();
            UpdateGoldDisplay();
        }
        
        protected override void OnHide()
        {
            Debug.Log("[ShopWindow] 닫힘");
        }
        
        // 상점 초기 재고 설정 (희귀 레시피 포함)
        public void InitializeShopInventory()
        {
            // 상점이 비어있으면 기본 아이템들로 채움
            if (_shopInventory.Count == 0)
            {
                // 기본 재료 아이템들
                _shopInventory.Add(new ShopItem { 
                    item = PlayerInventory.Herb_Red, 
                    price = 10, 
                    stock = 99, 
                    isRare = false 
                });
                _shopInventory.Add(new ShopItem { 
                    item = PlayerInventory.Herb_Purple, 
                    price = 15, 
                    stock = 50, 
                    isRare = false 
                });
                _shopInventory.Add(new ShopItem { 
                    item = PlayerInventory.Herb_Yellow, 
                    price = 12, 
                    stock = 75, 
                    isRare = false 
                });
                _shopInventory.Add(new ShopItem { 
                    item = PlayerInventory.RabbitMeat, 
                    price = 20, 
                    stock = 30, 
                    isRare = false 
                });
                
                // 희귀 레시피들 (요리)
                // 토끼 허브 구이 (토끼고기 + 회복꽃) - effect: "체력 회복 +50"
                _shopInventory.Add(new ShopItem { 
                    item = DishDatabase.GetItemData("토끼 허브 구이"), 
                    price = 100, 
                    stock = 5, 
                    isRare = true 
                });
                
                // 멧돼지 독구이 (멧돼지고기 + 독나물) - effect: "공격력 증가 +10 for 30s"
                _shopInventory.Add(new ShopItem { 
                    item = DishDatabase.GetItemData("멧돼지 독구이"), 
                    price = 150, 
                    stock = 3, 
                    isRare = true 
                });
                
                // 늑대 환각 스튜 (늑대고기 + 황혼초) - effect: "적 시야 흐림 (은신 효과)"
                _shopInventory.Add(new ShopItem { 
                    item = DishDatabase.GetItemData("늑대 환각 스튜"), 
                    price = 200, 
                    stock = 2, 
                    isRare = true 
                });
                
                // 희귀 약물들 (연금술)
                // 은빛 회복제 (은빛 이끼 + 피어리) - effect: "지속 체력 재생 5 HP/s for 20s"
                _shopInventory.Add(new ShopItem { 
                    item = CreatePotionItem("은빛 회복제", "은빛 이끼 + 피어리", "지속 체력 재생 5 HP/s for 20s"), 
                    price = 300, 
                    stock = 2, 
                    isRare = true 
                });
                
                // 독 elite (독나물 + 은빛 이끼) - effect: "중독 공격 +15 데미지 for 10s"
                _shopInventory.Add(new ShopItem { 
                    item = CreatePotionItem("독 엘리트", "독나물 + 은빛 이끼", "중독 공격 +15 데미지 for 10s"), 
                    price = 250, 
                    stock = 3, 
                    isRare = true 
                });

                // ===== C9-06: 무기/방어구/도구 =====
                // 무기
                _shopInventory.Add(new ShopItem { item = PlayerInventory.SwordWood,  price = 80,  stock = 5,  isRare = false });
                _shopInventory.Add(new ShopItem { item = PlayerInventory.SpearWood,  price = 100, stock = 3,  isRare = false });
                _shopInventory.Add(new ShopItem { item = PlayerInventory.BowWood,    price = 120, stock = 3,  isRare = false });
                // 방어구
                _shopInventory.Add(new ShopItem { item = PlayerInventory.ClothArmor,   price = 50,  stock = 5,  isRare = false });
                _shopInventory.Add(new ShopItem { item = PlayerInventory.LeatherArmor, price = 150, stock = 3,  isRare = false });
                // 도구
                _shopInventory.Add(new ShopItem { item = PlayerInventory.Pickaxe,    price = 60,  stock = 3,  isRare = false });
                _shopInventory.Add(new ShopItem { item = PlayerInventory.Axe,        price = 60,  stock = 3,  isRare = false });
                _shopInventory.Add(new ShopItem { item = PlayerInventory.FishingRod, price = 40,  stock = 5,  isRare = false });
            }
        }
        
        // 임시 물약 아이템 생성 (실제로는 HerbComboDatabase에서 만들어야 하지만 semplificato)
        private PlayerInventory.ItemData CreatePotionItem(string displayName, string description, string effect)
        {
            // 실제 프로젝트에서는 물약 아이템 데이터베이스가 있어야 함
            // 여기서는 간단히 소비 가능한 아이템으로 생성
            // id는 안정적인 문자열 사용 (GetHashCode() 대신)
            return new PlayerInventory.ItemData
            {
                id = $"potion_{displayName.Replace(" ", "_")}",
                displayName = displayName,
                description = description,
                category = PlayerInventory.ItemCategory.Potion,
                maxStack = 99
                // Icon은 실제로는 Resources에서 로드하거나 설정해야 함
            };
        }
        
        // ===================================================================
        // OnGUI — IMGUI 렌더링
        // ===================================================================
        private void OnGUI()
        {
            if (!IsOpen) return;
            if (_currentItems == null) return; // NRE 방지
            
            InitStyles();
            
            float x = (Screen.width - 680) / 2;
            float y = (Screen.height - 580) / 2;
            float width = 1440;
            float height = 1170;
            
            // === 배경 + 외곽 박스 ==
            GUI.Box(new Rect(x, y, width, height), "", _stylePanelBox);
            
            // === 타이틀 바 ==
            float titleHeight = 90;
            GUI.Label(new Rect(x, y, width, titleHeight), "🏪 상점", _styleTitle);
            
            // === 골드 표시 (캐시 사용) ==
            float goldY = y + titleHeight + 10f;
            int currentGold = PlayerStats.Instance?.Gold ?? 0;
            if (currentGold != _cachedGoldValue)
            {
                _cachedGoldValue = currentGold;
                _cachedGoldText = $"골드: {currentGold}";
            }
            GUI.Label(new Rect(x + 10, goldY, width - 20, 38), _cachedGoldText, _styleItemName);
            
            // === 아이템 목록 ==
            float itemsY = goldY + 30f;
            float itemsHeight = height - itemsY - 50f; // 아래 버튼들을 위한 공간 남김
            DrawItemsGrid(x, itemsY, width - 20f, itemsHeight);
            
            // === 하단 버튼들 ==
            float buttonY = y + height - 40f;
            float buttonWidth = (width - 40f) / 2f;
            
            // 구매 버튼
            if (_selectedSlotIndex >= 0 && _selectedSlotIndex < _currentItems.Count)
            {
                ShopItem selectedItem = _currentItems[_selectedSlotIndex];
                bool canAfford = (PlayerStats.Instance?.Gold ?? 0) >= selectedItem.price;
                bool inStock = selectedItem.stock == -1 || selectedItem.stock > 0;
                
                GUI.enabled = canAfford && inStock;
                if (GUI.Button(new Rect(x + 10, buttonY, buttonWidth, 45), "구매", _styleBuyButton))
                {
                    BuySelectedItem();
                }
                GUI.enabled = true;
                
                // 재고나 금액 부족 표시
                if (!canAfford)
                {
                    GUI.Label(new Rect(x + 10, buttonY - 20f, buttonWidth, 27), "골드 부족!", _styleEmptyText);
                }
                else if (!inStock)
                {
                    GUI.Label(new Rect(x + 10, buttonY - 20f, buttonWidth, 27), "품절!", _styleEmptyText);
                }
            }
            
            // 판매 버튼 (플레이어 인벤토리에서 selected item이 있으면)
            if (GUI.Button(new Rect(x + width - buttonWidth - 10, buttonY, buttonWidth, 45), "판매", _styleSellButton))
            {
                SellSelectedItem();
            }
        }
        
        // 상점 아이템 그리드 그리기
        private void DrawItemsGrid(float panelX, float panelY, float panelWidth, float panelHeight)
        {
            if (_currentItems == null || _currentItems.Count == 0)
            {
                GUI.Label(new Rect(panelX, panelY, panelWidth, 45), "판매 중인 아이템이 없습니다.", _styleEmptyText);
                return;
            }
            
            // 그리드 설정
            int columns = 2;
            float slotWidth = (panelWidth - 10f) / columns;
            float slotHeight = 180;
            float margin = 5f;
            
            // 스크롤 뷰
            float viewHeight = panelHeight - 10f;
            float contentHeight = Mathf.CeilToInt((float)_currentItems.Count / columns) * (slotHeight + margin) + margin;
            
            GUI.BeginGroup(new Rect(panelX, panelY, panelWidth, panelHeight));
            _scrollPosition = GUI.BeginScrollView(
                new Rect(0, 0, panelWidth, viewHeight),
                _scrollPosition,
                new Rect(0, 0, panelWidth - 20f, contentHeight)
            );
            
            for (int i = 0; i < _currentItems.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                
                float sx = margin + col * (slotWidth + margin);
                float sy = margin + row * (slotHeight + margin);
                
                Rect slotRect = new Rect(sx, sy, slotWidth, slotHeight);
                bool isSelected = (i == _selectedSlotIndex);
                
                // 슬롯 배경
                GUI.Box(slotRect, "", isSelected ? _styleSlotSelected : _styleSlot);
                
                ShopItem item = _currentItems[i];
                
                // 아이콘 캐싱 (ItemIconDatabase 사용)
                Texture2D iconTex;
                if (i < _cachedIconTextures.Length && _cachedIconTextures[i] != null)
                    iconTex = _cachedIconTextures[i];
                else
                    iconTex = ItemIconDatabase.GetOrCreateIcon(item.item);
                if (iconTex != null)
                {
                    GUI.DrawTexture(new Rect(sx + 5, sy + 5, 90, 90), iconTex);
                }
                else
                {
                    // 폴백: 카테고리 색상 사각형
                    Color fallbackColor = GetCategoryColor(item.item.category);
                    var oldColor = GUI.color;
                    GUI.color = fallbackColor;
                    GUI.DrawTexture(new Rect(sx + 5, sy + 5, 90, 90), _texWhite);
                    GUI.color = oldColor;
                }
                
                // 아이템 이름
                float nameY = sy + 5f;
                GUI.Label(new Rect(sx + 42, nameY, slotWidth - 47, 30), 
                    item.item.displayName, _styleItemName);
                
                // 희귀 태그
                if (item.isRare)
                {
                    GUI.Label(new Rect(sx + 42, nameY + 18f, slotWidth - 47, 24), 
                        "[희귀]", _styleRareTag);
                }
                
                // 아이템 설명 (간단히)
                float descY = sy + 35f;
                GUI.Label(new Rect(sx + 42, descY, slotWidth - 47, 24), 
                    item.item.description, _styleSlotLabel);
                
                // 가격 (캐시 사용)
                float priceY = sy + 52f;
                GUI.Label(new Rect(sx + 42, priceY, slotWidth/2 - 10, 27), 
                    _cachedPriceTexts != null && i < _cachedPriceTexts.Length ? _cachedPriceTexts[i] : $"가격: {item.price}G", _styleItemPrice);
                
                // 재고 (캐시 사용)
                GUI.Label(new Rect(sx + slotWidth/2 + 5, priceY, slotWidth/2 - 10, 27), 
                    _cachedStockTexts != null && i < _cachedStockTexts.Length ? _cachedStockTexts[i] : "재고: ?", _styleItemStock);
                
                // 클릭 처리
                if (Event.current.type == EventType.MouseDown && slotRect.Contains(Event.current.mousePosition))
                {
                    _selectedSlotIndex = i;
                    Event.current.Use();
                }
            }
            
            GUI.EndScrollView();
            GUI.EndGroup();
        }
        
        // 선택된 아이템 구매
        private void BuySelectedItem()
        {
            if (_selectedSlotIndex < 0 || _selectedSlotIndex >= _currentItems.Count) return;
            
            ShopItem item = _currentItems[_selectedSlotIndex];
            
            // 골드 확인 및 차감
            if (!(PlayerStats.Instance?.SpendGold(item.price) ?? false)) return;
            
            // 재고 확인 및 감소 (-1은 무한)
            if (item.stock > 0)
            {
                item.stock--;
            }
            
            // 아이템을 플레이어 인벤토리에 추가
            if (PlayerInventory.Instance.AddItem(item.item, 1))
            {
                Debug.Log($"[ShopWindow] 구매 성공: {item.item.displayName}");
                RefreshShopItems(); // UI 업데이트 + 캐시 갱신
            }
            else
            {
                // 인벤토리 가득 찼으면 골드 환불
                PlayerStats.Instance?.AddGold(item.price);
                Debug.LogWarning("[ShopWindow] 인벤토리 가득 참! 구매 취소.");
            }
        }

        // Public buy method for tests/external calls
        public bool BuyItem(ShopItem item)
        {
            if (item == null) return false;
            if (!(PlayerStats.Instance?.SpendGold(item.price) ?? false)) return false;

            if (item.stock > 0)
            {
                item.stock--;
            }

            if (PlayerInventory.Instance.AddItem(item.item, 1))
            {
                Debug.Log($"[ShopWindow] 구매 성공: {item.item.displayName}");
                RefreshShopItems();
                return true;
            }
            else
            {
                PlayerStats.Instance?.AddGold(item.price);
                Debug.LogWarning("[ShopWindow] 인벤토리 가득 참! 구매 취소.");
                return false;
            }
        }

        // 선택된 아이템 판매 (플레이어 인벤토리에서)
        private void SellSelectedItem()
        {
            if (PlayerInventory.Instance == null) return;

            // TODO: UI에서 판매할 아이템을 선택하는 기능. 현재는 임시로 첫 번째 아이템 판매
            var allItems = PlayerInventory.Instance.GetAllSlots();
            if (allItems.Length == 0)
            {
                Debug.Log("[ShopWindow] 판매할 아이템이 없습니다.");
                return;
            }

            // 첫 번째 아이템 판매 (가격은 아이템 등급에 따라 1~100G)
            var firstItem = allItems[0];
            int sellPrice = CalculateSellPrice(firstItem.item);
            PlayerInventory.Instance.RemoveItem(firstItem.item?.id ?? "", 1);
            PlayerStats.Instance?.AddGold(sellPrice);
            Debug.Log($"[ShopWindow] 판매 성공: {firstItem.item.displayName} → {sellPrice}G");
            RefreshShopItems();
            UpdateGoldDisplay();
        }

        // 판매 가격 계산 (아이템 카테고리/등급 기반)
        private int CalculateSellPrice(PlayerInventory.ItemData item)
        {
            // 기본 가격 5G, Potion=15G, Weapon=30G, Armor=25G, Tool=20G
            switch (item.category)
            {
                case PlayerInventory.ItemCategory.Potion: return 15;
                case PlayerInventory.ItemCategory.Weapon: return 30;
                case PlayerInventory.ItemCategory.Armor: return 25;
                case PlayerInventory.ItemCategory.Tool: return 20;
                case PlayerInventory.ItemCategory.Material: return 5;
                default: return 5;
            }
        }
        
        // 상점 아이템 목록 새로고침
        public void RefreshShopItems()
        {
            _currentItems = new List<ShopItem>(_shopInventory); // 복사본 생성
            BuildDisplayCache(); // GC 최적화: 표시 문자열 캐시 갱신
        }
        
        // 표시 문자열 캐시 빌드 (OnGUI 내 string interpolation GC 할당 방지)
        private void BuildDisplayCache()
        {
            if (_currentItems == null || _currentItems.Count == 0)
            {
                _cachedPriceTexts = null;
                _cachedStockTexts = null;
                _cachedIconTextures = null;
                return;
            }
            int count = _currentItems.Count;
            if (_cachedPriceTexts == null || _cachedPriceTexts.Length != count)
            {
                _cachedPriceTexts = new string[count];
                _cachedStockTexts = new string[count];
                _cachedIconTextures = new Texture2D[count];
            }
            for (int i = 0; i < count; i++)
            {
                var item = _currentItems[i];
                _cachedPriceTexts[i] = $"가격: {item.price}G";
                _cachedStockTexts[i] = item.stock == -1 ? "재고: 무한" : $"재고: {item.stock}개";
                _cachedIconTextures[i] = ItemIconDatabase.GetOrCreateIcon(item.item);
            }
        }
        
        // 골드 표시 업데이트
        private void UpdateGoldDisplay()
        {
            if (_goldText != null)
            {
                _goldText.text = $"골드: {PlayerStats.Instance?.Gold ?? 0}";
            }
        }
        
        // 스타일 초기화
        private void InitStyles()
        {
            if (_stylesInitialized) return;
            
            _texWhite = MakeTexture(1, 1, Color.white);
            
            // 타이틀
            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 96,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };
            
            // 슬롯 배경
            _styleSlot = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, new Color(0.2f, 0.15f, 0.2f, 0.9f)), textColor = Color.white },
                hover = { background = MakeTexture(1, 1, new Color(0.3f, 0.2f, 0.3f, 0.9f)), textColor = Color.white },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(4, 4, 4, 4),
                margin = new RectOffset(2, 2, 2, 2)
            };
            
            // 선택된 슬롯
            _styleSlotSelected = new GUIStyle(_styleSlot)
            {
                normal = { background = MakeTexture(1, 1, new Color(0.4f, 0.25f, 0.1f, 1f)), textColor = Color.white }
            };
            
            // 아이템 이름
            _styleItemName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };
            
            // 가격
            _styleItemPrice = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.yellow }
            };
            
            // 재고
            _styleItemStock = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.cyan }
            };
            
            // 희귀 태그
            _styleRareTag = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.magenta }
            };
            
            // 버튼들
            _styleBuyButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 48,
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.2f, 0.6f, 0.2f, 1f)) },
                hover = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.3f, 0.7f, 0.3f, 1f)) }
            };
            
            _styleSellButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 48,
                normal = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.6f, 0.2f, 0.2f, 1f)) },
                hover = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.7f, 0.3f, 0.3f, 1f)) }
            };
            
            // 슬롯 라벨
            _styleSlotLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.grey }
            };
            
            // 빈 목록 텍스트
            _styleEmptyText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.grey }
            };
            
            // 패널 외곽 박스
            _stylePanelBox = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(1, 1, new Color(0.1f, 0.05f, 0.15f, 0.95f)), textColor = Color.white },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            
            _stylesInitialized = true;
        }
        
        // 헬퍼 메서드들
        private Color GetCategoryColor(PlayerInventory.ItemCategory category)
        {
            return category switch
            {
                PlayerInventory.ItemCategory.Herb => new Color(0.3f, 0.8f, 0.3f),
                PlayerInventory.ItemCategory.Meat => new Color(0.8f, 0.4f, 0.2f),
                PlayerInventory.ItemCategory.Food => new Color(0.9f, 0.8f, 0.2f),
                PlayerInventory.ItemCategory.Potion => new Color(0.7f, 0.3f, 0.8f),
                PlayerInventory.ItemCategory.Drug => new Color(0.6f, 0.2f, 0.6f),
                PlayerInventory.ItemCategory.Material => new Color(0.5f, 0.5f, 0.5f),
                PlayerInventory.ItemCategory.Weapon => new Color(0.8f, 0.3f, 0.3f),
                PlayerInventory.ItemCategory.Armor => new Color(0.3f, 0.3f, 0.8f),
                PlayerInventory.ItemCategory.Tool => new Color(0.6f, 0.4f, 0.2f),
                PlayerInventory.ItemCategory.Quest => new Color(0.2f, 0.7f, 0.8f),
                _ => Color.gray,
            };
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Texture2D texture = new Texture2D(width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}