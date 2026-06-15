using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-07: 떠돌이 상인 — 랜덤 방문, 희귀 레시피 판매
    /// 일정 시간마다 영지 입구에 나타나 짧은 시간 동안 희귀 아이템을 판매합니다.
    /// E키 상호작용 → ShopWindow 연동 (희귀 전용 재고)
    /// </summary>
    public class WanderingMerchant : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private string _merchantName = "떠돌이 상인";
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private float _visitDuration = 45f; // 체류 시간 (초)
        [SerializeField] private float _minInterval = 120f;  // 최소 방문 간격 (초)
        [SerializeField] private float _maxInterval = 300f;  // 최대 방문 간격 (초)
        [SerializeField] private float _spawnRadius = 8f;    // 영지 중심 기준 스폰 반경

        [Header("참조")]
        [SerializeField] private Transform _merchantModel;
        [SerializeField] private TextMesh _nameLabel;

        // 상태
        private bool _isVisiting = false;
        private float _departureTime;
        private float _nextVisitTime;
        private GameObject _shopWindowInstance;
        private ShopWindow _shopWindow;
        private UIManager _uiManager;
        private Vector3 _spawnCenter;

        // 희귀 상점 재고 (ShopWindow 재정의용)
        private List<ShopWindow.ShopItem> _rareInventory;

        private void Awake()
        {
            _uiManager = UIManager.Instance;
            _spawnCenter = transform.position;

            // 기본 비활성화
            gameObject.SetActive(false);

            // 첫 방문 타이머 설정
            _nextVisitTime = Time.time + Random.Range(_minInterval, _maxInterval);
        }

        private void Update()
        {
            // 방문 시간 체크
            if (!_isVisiting && Time.time >= _nextVisitTime)
            {
                StartVisit();
            }

            // 방문 중 시간 체크
            if (_isVisiting)
            {
                HandleInteraction();

                if (Time.time >= _departureTime)
                {
                    EndVisit();
                }
            }
        }

        /// <summary>
        /// 방문 시작 — 랜덤 위치에 생성 + 희귀 재고 설정
        /// </summary>
        private void StartVisit()
        {
            // 랜덤 위치 선정
            Vector3 offset = new Vector3(
                Random.Range(-_spawnRadius, _spawnRadius),
                0,
                Random.Range(-_spawnRadius, _spawnRadius)
            );
            transform.position = _spawnCenter + offset;

            // 희귀 재고 준비
            _rareInventory = GenerateRareInventory();

            // 표시
            gameObject.SetActive(true);
            _isVisiting = true;
            _departureTime = Time.time + _visitDuration;

            UpdateNameLabel();

            Debug.Log($"[WanderingMerchant] 🧳 {_merchantName} 등장! ({_rareInventory.Count}종 희귀 아이템)");
        }

        /// <summary>
        /// 방문 종료 — 상점 닫고 사라짐
        /// </summary>
        private void EndVisit()
        {
            // 열린 상점 닫기
            if (_shopWindowInstance != null)
            {
                if (_shopWindow != null && _shopWindow.IsOpen)
                    _uiManager?.CloseWindow(_shopWindow);
                Destroy(_shopWindowInstance);
                _shopWindowInstance = null;
                _shopWindow = null;
            }

            gameObject.SetActive(false);
            _isVisiting = false;

            // 다음 방문 시간 설정
            _nextVisitTime = Time.time + Random.Range(_minInterval, _maxInterval);

            Debug.Log($"[WanderingMerchant] {_merchantName} 떠남!");
        }

        /// <summary>
        /// E키 상호작용 처리
        /// </summary>
        private void HandleInteraction()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist <= _interactRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ToggleShop();
            }
        }

        /// <summary>
        /// 희귀 상점 토글
        /// </summary>
        private void ToggleShop()
        {
            if (_shopWindowInstance == null)
            {
                CreateShopWindow();
            }

            if (_shopWindow == null) return;

            _uiManager.ToggleWindow(_shopWindow);
        }

        /// <summary>
        /// 희귀 전용 ShopWindow 생성
        /// </summary>
        private void CreateShopWindow()
        {
            _shopWindowInstance = new GameObject("WanderingMerchant_Shop");
            _shopWindow = _shopWindowInstance.AddComponent<ShopWindow>();

            // 희귀 재고 설정 (리플렉션으로 _shopInventory 교체)
            var invField = typeof(ShopWindow).GetField("_shopInventory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (invField != null)
            {
                invField.SetValue(_shopWindow, _rareInventory);
            }

            _shopWindowInstance.SetActive(false);
        }

        /// <summary>
        /// 희귀 아이템 재고 생성
        /// </summary>
        private List<ShopWindow.ShopItem> GenerateRareInventory()
        {
            var items = new List<ShopWindow.ShopItem>();

            // 희귀 요리 (3~4종)
            items.Add(MakeRareItem(DishDatabase.GetItemData("토끼 허브 구이"), 80, 2));
            items.Add(MakeRareItem(DishDatabase.GetItemData("멧돼지 독구이"), 120, 2));
            items.Add(MakeRareItem(DishDatabase.GetItemData("늑대 환각 스튜"), 180, 1));

            // 희귀 포션 (2~3종)
            items.Add(MakeRareItem(CreatePotion("불굴의 물약", "힘의 근원", "공격력 +50 for 60s"), 500, 1));
            items.Add(MakeRareItem(CreatePotion("투명 물약", "환영초", "은신 30초"), 400, 1));
            items.Add(MakeRareItem(CreatePotion("대지의 수호", "단단한 수정", "방어력 +100 for 30s"), 350, 2));

            // 희귀 재료 (2종)
            items.Add(MakeRareItem(new PlayerInventory.ItemData
            {
                id = "mat_rare_gem",
                displayName = "희귀 보석",
                description = "마법의 힘이 깃든 보석. 고급 장비 재료.",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 5
            }, 200, 3));

            items.Add(MakeRareItem(new PlayerInventory.ItemData
            {
                id = "mat_dragon_scale",
                displayName = "용비늘 조각",
                description = "전설의 용 비늘 조각. 최고급 방어구 재료.",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 3
            }, 800, 1));

            // 희귀 무기 레시피 (1종)
            items.Add(MakeRareItem(new PlayerInventory.ItemData
            {
                id = "recipe_iron_sword",
                displayName = "[레시피] 철검 제작법",
                description = "철 주괴로 강력한 철검을 만드는 방법.",
                category = PlayerInventory.ItemCategory.Quest,
                maxStack = 1
            }, 300, 1));

            return items;
        }

        private ShopWindow.ShopItem MakeRareItem(PlayerInventory.ItemData item, int price, int stock)
        {
            return new ShopWindow.ShopItem
            {
                item = item,
                price = price,
                stock = stock,
                isRare = true
            };
        }

        private PlayerInventory.ItemData CreatePotion(string name, string ingredient, string effect)
        {
            return new PlayerInventory.ItemData
            {
                id = $"rare_potion_{name}",
                displayName = name,
                description = $"{ingredient} - {effect}",
                category = PlayerInventory.ItemCategory.Potion,
                maxStack = 3
            };
        }

        private void UpdateNameLabel()
        {
            if (_nameLabel != null)
            {
                _nameLabel.text = $"🧳 {_merchantName}\n({_visitDuration:F0}초 후 떠남)";
            }
        }

        /// <summary>
        /// 남은 체류 시간 (UI 표시용)
        /// </summary>
        public float RemainingTime => _isVisiting ? Mathf.Max(0, _departureTime - Time.time) : 0f;

        /// <summary>
        /// 현재 방문 중인가?
        /// </summary>
        public bool IsVisiting => _isVisiting;
    }
}