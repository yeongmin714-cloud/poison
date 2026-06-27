using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 5.7.3: 떠돌이 상인 — 랜덤 아이템을 판매하는 이동형 NPC.
    /// 일정 시간마다 위치를 변경하고, 보유 아이템을 랜덤으로 교체합니다.
    /// </summary>
    public class WanderingMerchant : MonoBehaviour
    {
        [System.Serializable]
        public class MerchantItem
        {
            public string itemId;
            public int price;
            public int stock;
        }

        [Header("상인 설정")]
        [SerializeField] private string _merchantName = "떠돌이 상인";
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private float _wanderInterval = 60f;   // 이동 간격 (초)

        [Header("판매 목록 (최대 6종)")]
        [SerializeField] private MerchantItem[] _inventory = new MerchantItem[6];

        [Header("판매 보정")]
        [SerializeField][Range(0.5f, 3f)] private float _priceMultiplier = 1.5f; // 영지 외 지역 할증

        private Transform _player;
        private bool _isPlayerNearby;
        private float _wanderTimer;
        private Vector3 _origin;
        private bool _isTrading;

        // FindItemData용 캐시: 최초 1회 리플렉션 후 Dictionary에 저장
        private static Dictionary<string, PlayerInventory.ItemData> _itemDataCache;

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            _origin = transform.position;
            _wanderTimer = _wanderInterval;
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _isPlayerNearby = dist <= _interactRange;

            if (_isPlayerNearby && !_isTrading && Input.GetKeyDown(KeyCode.E))
            {
                OpenTradeUI();
            }

            // 위치 이동 타이머 (거래 중에는 이동하지 않음)
            if (!_isTrading)
            {
                _wanderTimer -= Time.deltaTime;
                if (_wanderTimer <= 0f)
                {
                    WanderToNewPosition();
                    _wanderTimer = _wanderInterval;
                }
            }
        }

        /// <summary>
        /// 상인 위치를 랜덤으로 변경합니다.
        /// </summary>
        private void WanderToNewPosition()
        {
            Vector3 offset = new Vector3(
                Random.Range(-10f, 10f),
                0f,
                Random.Range(-10f, 10f)
            );
            transform.position = _origin + offset;

            // 보유 아이템 랜덤 변경 (50% 확률)
            if (Random.value > 0.5f)
                RefreshInventory();

            Debug.Log($"[WanderingMerchant] {_merchantName} 이동 완료 → {transform.position}");
        }

        /// <summary>
        /// 판매 목록을 랜덤으로 갱신합니다.
        /// </summary>
        private void RefreshInventory()
        {
            // 기본 재료/소모품 ID 목록
            string[] possibleItems = new string[]
            {
                "herb_red", "herb_green", "herb_silver",
                "meat_rabbit", "meat_boar",
                "mat_rabbit_fur", "mat_boar_leather"
            };

            for (int i = 0; i < _inventory.Length; i++)
            {
                if (Random.value > 0.3f) // 70% 확률로 아이템 채움
                {
                    _inventory[i] = new MerchantItem
                    {
                        itemId = possibleItems[Random.Range(0, possibleItems.Length)],
                        price = Random.Range(20, 80),
                        stock = Random.Range(1, 5)
                    };
                }
                else
                {
                    _inventory[i] = null;
                }
            }

            Debug.Log($"[WanderingMerchant] {_merchantName} 판매 목록 갱신!");
        }

        /// <summary>
        /// 플레이어가 아이템을 구매합니다.
        /// </summary>
        public bool BuyItem(int index, int count = 1)
        {
            if (_isTrading == false) return false;
            if (index < 0 || index >= _inventory.Length) return false;
            var item = _inventory[index];
            if (item == null || item.stock < count) return false;

            if (PlayerStats.Instance == null) return false;

            int totalPrice = Mathf.RoundToInt(item.price * _priceMultiplier * count);

            if (!PlayerStats.Instance.SpendGold(totalPrice))
            {
                Debug.Log($"[WanderingMerchant] 골드 부족! 필요: {totalPrice}");
                return false;
            }

            // PlayerInventory에 아이템 추가 (정적 ItemData 참조)
            var itemData = FindItemData(item.itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[WanderingMerchant] 아이템 데이터 없음: {item.itemId}");
                PlayerStats.Instance.AddGold(totalPrice); // 환불
                return false;
            }

            bool added = PlayerInventory.Instance.AddItem(itemData, count);
            if (!added)
            {
                PlayerStats.Instance.AddGold(totalPrice); // 환불
                Debug.LogWarning("[WanderingMerchant] 인벤토리 가득 참!");
                return false;
            }

            item.stock -= count;
            Debug.Log($"[WanderingMerchant] {itemData.displayName} x{count} 구매 완료! ({totalPrice}G)");
            return true;
        }

        private PlayerInventory.ItemData FindItemData(string itemId)
        {
            // 캐시 미초기화 시 최초 1회 리플렉션으로 빌드
            if (_itemDataCache == null)
            {
                _itemDataCache = new Dictionary<string, PlayerInventory.ItemData>();
                var fields = typeof(PlayerInventory).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(PlayerInventory.ItemData))
                    {
                        var val = field.GetValue(null) as PlayerInventory.ItemData;
                        if (val != null && !string.IsNullOrEmpty(val.id) && !_itemDataCache.ContainsKey(val.id))
                        {
                            _itemDataCache[val.id] = val;
                        }
                    }
                }
            }

            _itemDataCache.TryGetValue(itemId, out var result);
            return result;
        }

        private void OpenTradeUI()
        {
            if (_isTrading) return;
            _isTrading = true;
            Debug.Log($"[WanderingMerchant] {_merchantName} 거래 UI 열림");
        }

        /// <summary>
        /// 거래 UI를 닫고 거래 상태를 해제합니다.
        /// </summary>
        public void CloseTradeUI()
        {
            if (!_isTrading) return;
            _isTrading = false;
            Debug.Log($"[WanderingMerchant] {_merchantName} 거래 UI 닫힘");
        }

        private void OnGUI()
        {
            if (!_isPlayerNearby) return;

            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30),
                $"[E] {_merchantName} — 거래하기");
        }
    }
}