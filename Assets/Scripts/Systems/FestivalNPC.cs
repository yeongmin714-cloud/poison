using System.Collections.Generic;
using System.Linq;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 38.5: 축제 기간에만 등장하는 임시 NPC.
    /// 특수 아이템 판매, 미니게임, 퀘스트 제공 기능을 담당합니다.
    /// FestivalManager의 이벤트를 구독하여 활성화/비활성화됩니다.
    /// </summary>
    public class FestivalNPC : MonoBehaviour
    {
        [Header("=== NPC 기본 정보 ===")]
        [SerializeField] private string _npcName = "축제 상인";
        [SerializeField] private string _greeting = "어서 오세요! 축제는 즐거우신가요?";

        [Header("=== 연결된 축제 ===")]
        [SerializeField] private string _festivalId; // 이 NPC가 속한 축제 ID

        [Header("=== 판매 아이템 ===")]
        [SerializeField] private List<FestivalItem> _itemsForSale = new List<FestivalItem>();

        [Header("=== 미니게임 설정 ===")]
        [SerializeField] private bool _hasMiniGame;
        [SerializeField] private string _miniGameName;
        [SerializeField][TextArea(2, 4)] private string _miniGameDescription;

        [Header("=== 자동 설정 ===")]
        [SerializeField] private bool _registerWithManager = true;

        // ===== 상태 =====
        private bool _isInitialized;
        private FestivalData _linkedFestival;
        private bool _isActive;

        // ===== 프로퍼티 =====

        public string npcName => _npcName;
        public string greeting => _greeting;
        public string festivalId => _festivalId;
        public bool hasMiniGame => _hasMiniGame;
        public string miniGameName => _miniGameName;
        public string miniGameDescription => _miniGameDescription;
        public IReadOnlyList<FestivalItem> itemsForSale => _itemsForSale.AsReadOnly();
        public bool isActive => _isActive;

        // ===== 생명주기 =====

        private void Start()
        {
            if (_registerWithManager)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
            if (_registerWithManager && FestivalManager.Instance != null)
            {
                FestivalManager.OnFestivalStarted -= OnFestivalStarted;
                FestivalManager.OnFestivalEnded -= OnFestivalEnded;
            }
        }

        // ===== 초기화 =====

        /// <summary>
        /// FestivalManager 이벤트 구독 및 초기 상태 설정.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            // 연결된 축제 데이터 찾기
            if (FestivalManager.Instance != null)
            {
                _linkedFestival = FestivalManager.Instance.GetFestivalById(_festivalId);

                FestivalManager.OnFestivalStarted += OnFestivalStarted;
                FestivalManager.OnFestivalEnded += OnFestivalEnded;

                // 이미 활성화된 축제면 바로 활성화
                if (_linkedFestival != null && FestivalManager.Instance.ActiveFestivals.Contains(_linkedFestival))
                {
                    Activate();
                }
            }
            else
            {
                Debug.LogWarning($"[FestivalNPC] FestivalManager.Instance가 없습니다. NPC '{_npcName}'는 수동 활성화가 필요합니다.");
            }
        }

        // ===== 이벤트 핸들러 =====

        private void OnFestivalStarted(FestivalData festival)
        {
            if (festival.festivalId == _festivalId)
            {
                Activate();
            }
        }

        private void OnFestivalEnded(FestivalData festival)
        {
            if (festival.festivalId == _festivalId)
            {
                Deactivate();
            }
        }

        // ===== 활성화/비활성화 =====

        /// <summary>NPC를 활성화 (축제 시작)</summary>
        public void Activate()
        {
            if (_isActive) return;
            _isActive = true;
            gameObject.SetActive(true);

            Debug.Log($"[FestivalNPC] 🎪 NPC '{_npcName}' 활성화! (축제: {_festivalId})");
        }

        /// <summary>NPC를 비활성화 (축제 종료)</summary>
        public void Deactivate()
        {
            if (!_isActive) return;
            _isActive = false;
            gameObject.SetActive(false);

            Debug.Log($"[FestivalNPC] NPC '{_npcName}' 비활성화 (축제 종료: {_festivalId})");
        }

        // ===== 상호작용 =====

        /// <summary>
        /// 플레이어가 NPC와 대화할 때 호출됩니다.
        /// NPCDialogueWindow와 연동하여 사용합니다.
        /// </summary>
        public string GetDialogueText()
        {
            if (!_isActive) return "... (축제 기간이 아닙니다)";

            string text = $"{_greeting}\n\n";
            if (_linkedFestival != null)
            {
                text += $"지금은 [{_linkedFestival.festivalName}] 기간입니다!\n";
                text += $"효과: {_linkedFestival.GetEffect().GetSummary()}\n\n";
            }

            if (_itemsForSale.Count > 0)
            {
                text += "판매 아이템:\n";
                foreach (var item in _itemsForSale)
                {
                    string priceStr = item.price > 0 ? $" - {item.price}G" : " (무료)";
                    text += $"  • {item.itemName}{priceStr}\n";
                }
            }

            if (_hasMiniGame)
            {
                text += $"\n🎮 미니게임: {_miniGameName}\n{_miniGameDescription}";
            }

            return text;
        }

        /// <summary>아이템 구매 (WarehouseSystem 연동)</summary>
        public bool TryBuyItem(int itemIndex, out string resultMessage)
        {
            resultMessage = "";

            if (!_isActive)
            {
                resultMessage = "축제 기간이 아닙니다.";
                return false;
            }

            if (itemIndex < 0 || itemIndex >= _itemsForSale.Count)
            {
                resultMessage = "잘못된 아이템 인덱스입니다.";
                return false;
            }

            var item = _itemsForSale[itemIndex];

            // TODO: 실제 재화 차감 및 인벤토리 추가 로직
            // 예: PlayerInventory.Instance.TrySpendGold(item.price);
            // 예: PlayerInventory.Instance.AddItem(item.itemName, 1);

            Debug.Log($"[FestivalNPC] {_npcName} → {item.itemName} 구매 시도 (가격: {item.price}G)");
            resultMessage = $"{item.itemName}을(를) 구매했습니다!";
            return true;
        }

        /// <summary>미니게임 시작 요청</summary>
        public bool TryStartMiniGame(out string resultMessage)
        {
            resultMessage = "";
            if (!_isActive)
            {
                resultMessage = "축제 기간이 아닙니다.";
                return false;
            }

            if (!_hasMiniGame)
            {
                resultMessage = "미니게임이 없습니다.";
                return false;
            }

            Debug.Log($"[FestivalNPC] {_npcName} → 미니게임 '{_miniGameName}' 시작");
            resultMessage = $"미니게임 '{_miniGameName}'을(를) 시작합니다!";
            return true;
        }

        /// <summary>수동으로 축제 강제 연결 (Inspector에서 설정 외 사용)</summary>
        public void SetLinkedFestivalId(string festivalId)
        {
            _festivalId = festivalId;
            if (FestivalManager.Instance != null)
            {
                _linkedFestival = FestivalManager.Instance.GetFestivalById(festivalId);
            }
        }
    }

    /// <summary>
    /// 축제 NPC 판매 아이템 정의
    /// </summary>
    [System.Serializable]
    public class FestivalItem
    {
        [SerializeField] private string _itemName;
        [SerializeField][TextArea(1, 3)] private string _description;
        [SerializeField] private int _price;
        [SerializeField] private bool _isLegendary;

        public string itemName => _itemName;
        public string description => _description;
        public int price => _price;
        public bool isLegendary => _isLegendary;
    }
}