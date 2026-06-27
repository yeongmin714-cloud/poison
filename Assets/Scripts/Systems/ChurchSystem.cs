using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 5.5: 교회 기부/친밀도 시스템.
    /// 골드를 기부하면 친밀도가 상승하고, 일정 수준 이상일 때 축복 효과를 받습니다.
    /// </summary>
    public class ChurchSystem : MonoBehaviour
    {
        public static ChurchSystem Instance { get; private set; }

        [Header("교회 설정")]
        [SerializeField] private string _churchName = "성당";
        [SerializeField] private float _interactRange = 3f;

        [Header("친밀도")]
        [SerializeField][Range(0, 100)] private int _favor = 0;
        [SerializeField] private int _maxFavor = 100;

        [Header("기부 설정")]
        [SerializeField] private int _donationAmount = 50;       // 1회 기부 골드
        [SerializeField] private int _favorPerDonation = 5;      // 기부당 친밀도 증가량
        [SerializeField] private int _favorThresholdBlessing = 30; // 축복 필요 친밀도

        // 친밀도 변경 이벤트
        public System.Action<int> OnFavorChanged;

        private Transform _player;
        private bool _isPlayerNearby;

        public int Favor => _favor;
        public int MaxFavor => _maxFavor;
        public string ChurchName => _churchName;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            _isPlayerNearby = dist <= _interactRange;

            if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            {
                OpenChurchUI();
            }
        }

        /// <summary>
        /// 골드를 기부하고 친밀도를 상승시킵니다.
        /// </summary>
        public void Donate()
        {
            if (PlayerStats.Instance == null) return;

            if (!PlayerStats.Instance.SpendGold(_donationAmount))
            {
                Debug.Log($"[ChurchSystem] {_churchName}: 골드가 부족합니다.");
                return;
            }

            _favor = Mathf.Min(_favor + _favorPerDonation, _maxFavor);
            OnFavorChanged?.Invoke(_favor);

            Debug.Log($"[ChurchSystem] {_churchName} 기부 완료! 친밀도 +{_favorPerDonation} (현재 {_favor})");

            if (_favor >= _favorThresholdBlessing)
            {
                ApplyBlessing();
            }
        }

        /// <summary>
        /// 친밀도가 일정 수준 이상일 때 축복 효과 적용.
        /// </summary>
        private void ApplyBlessing()
        {
            Debug.Log($"[ChurchSystem] 🙏 {_churchName}의 축복! 이동 속도 +10% (임시)");
            // 실제 효과는 PlayerStats나 버프 시스템과 연동 가능
        }

        private void OpenChurchUI()
        {
            Debug.Log($"[ChurchSystem] {_churchName} UI 열림 (친밀도: {_favor}/{_maxFavor})");
        }

        // ===== ChurchUI 연동 메서드 =====
        public string GetFavorLevelText()
        {
            if (_favor >= 80) return "🌟 충성 (영주 대면 가능)";
            if (_favor >= 60) return "✨ 존경";
            if (_favor >= 40) return "👍 호의적";
            if (_favor >= 20) return "🤝 보통";
            return "❄️ 냉담";
        }

        public string GetFavorBenefitsText()
        {
            if (_favor >= 80) return "• 이동 속도 +15%\n• 체력 회복 +3/초\n• 영주 대면 가능";
            if (_favor >= 60) return "• 이동 속도 +10%\n• 체력 회복 +2/초";
            if (_favor >= 40) return "• 이동 속도 +5%\n• 체력 회복 +1/초";
            if (_favor >= 20) return "• 체력 회복 +1/초";
            return "• 기본 혜택 없음";
        }

        public int GetFavor() => _favor;

        public int DonateGold(int amount)
        {
            if (PlayerStats.Instance == null) return 0;

            int canSpend = Mathf.Min(amount, PlayerStats.Instance.Gold);
            if (canSpend <= 0) return 0;

            if (!PlayerStats.Instance.SpendGold(canSpend))
                return 0;

            int favorGain = canSpend / 10;
            _favor = Mathf.Min(_favor + favorGain, _maxFavor);
            OnFavorChanged?.Invoke(_favor);

            Debug.Log($"[ChurchSystem] {_churchName} 기부 완료! {canSpend}골드 소모, 친밀도 +{favorGain} (현재 {_favor})");

            if (_favor >= _favorThresholdBlessing)
                ApplyBlessing();

            return canSpend;
        }

        public bool CanRequestAudience() => _favor >= 80;

        private void OnGUI()
        {
            if (!_isPlayerNearby) return;

            string info = $"[E] {_churchName} — 친밀도: {_favor}/{_maxFavor}";
            if (_favor >= _favorThresholdBlessing)
                info += " 🙏 축복 가능";

            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 + 50, 300, 30), info);
        }
    }
}