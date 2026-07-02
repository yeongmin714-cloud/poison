using System.Collections;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 낚시 시스템 싱글톤. 물가에서 E키로 낚시 시작/종료.
    /// 낚시대 필요, 미니게임 상태 관리, 시간대/날씨 보정 확률.
    /// </summary>
    public class FishingSystem : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField, Tooltip("물 감지 범위")]
        private float _waterCheckRange = 2.5f;

        [SerializeField, Tooltip("입질 대기 시간 (최소)")]
        private float _minWaitTime = 1f;

        [SerializeField, Tooltip("입질 대기 시간 (최대)")]
        private float _maxWaitTime = 3f;

        // 싱글톤
        public static FishingSystem Instance { get; private set; }

        // 상태
        private bool _isFishing;
        private bool _isWaitingForBite;
        private bool _isMinigameActive;
        private Transform _player;
        private Coroutine _waitCoroutine;

        // 미니게임 내부 상태 (FishingUI에서 읽음)
        private float _pinPosition;
        private bool _pinDirectionRight = true;
        private float _sweetSpotStart;

        // 팝업
        private string _popupMessage = "";
        private float _popupTimer;

        // ===== 공개 프로퍼티 =====

        /// <summary>낚시 중인지 여부 (이동 불가 플래그)</summary>
        public bool IsFishing => _isFishing;

        /// <summary>입질 대기 중인지 여부</summary>
        public bool IsWaitingForBite => _isWaitingForBite;

        /// <summary>미니게임 활성화 여부</summary>
        public bool IsMinigameActive => _isMinigameActive;

        /// <summary>현재 핀 위치 (픽셀, 0~300)</summary>
        public float PinPosition => _pinPosition;

        /// <summary>스위트스팟 시작 위치 (픽셀)</summary>
        public float SweetSpotStart => _sweetSpotStart;

        /// <summary>스위트스팟 너비 (픽셀, 고정 30)</summary>
        public float SweetSpotWidth => 30f;

        /// <summary>프로그레스바 너비 (픽셀, 고정 300)</summary>
        public float ProgressBarWidth => 300f;

        /// <summary>현재 팝업 메시지</summary>
        public string PopupMessage => _popupMessage;

        /// <summary>팝업 잔여 시간 (초)</summary>
        public float PopupTimer => _popupTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null)
                Debug.LogWarning("[FishingSystem] Player 태그 오브젝트를 찾을 수 없음");
        }

        private void Update()
        {
            // 팝업 타이머
            if (_popupTimer > 0f)
            {
                _popupTimer -= Time.deltaTime;
                if (_popupTimer <= 0f)
                    _popupMessage = "";
            }

            if (_player == null) return;

            if (!_isFishing)
            {
                // 물가에서 E키 → 낚시 시작
                if (Input.GetKeyDown(KeyCode.E) && IsNearWater())
                    TryStartFishing();

                return;
            }

            // 낚시 중: 미니게임 핀 업데이트
            if (_isMinigameActive)
                UpdatePinMovement();

            // E키로 낚시 종료
            if (Input.GetKeyDown(KeyCode.E))
                StopFishing();
        }

        // ===== 물 감지 =====

        /// <summary>플레이어 아래 "Water" 태그가 있는지 Raycast로 확인</summary>
        private bool IsNearWater()
        {
            if (_player == null) return false;

            RaycastHit hit;
            Vector3 origin = _player.position + Vector3.up * 0.1f;
            if (Physics.Raycast(origin, Vector3.down, out hit, _waterCheckRange))
                return hit.collider.CompareTag("Water");

            return false;
        }

        // ===== 낚시 시작/종료 =====

        /// <summary>낚시 시작 시도</summary>
        private void TryStartFishing()
        {
            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[FishingSystem] PlayerInventory.Instance가 없습니다.");
                return;
            }

            if (!PlayerInventory.Instance.HasItem("fishing_rod"))
            {
                ShowPopup("낚시대가 필요합니다.");
                return;
            }

            _isFishing = true;
            _isWaitingForBite = true;
            _waitCoroutine = StartCoroutine(WaitForBite());
        }

        /// <summary>낚시 종료</summary>
        public void StopFishing()
        {
            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
            }

            _isFishing = false;
            _isWaitingForBite = false;
            _isMinigameActive = false;
        }

        /// <summary>ESC 키 등 외부에서 낚시 취소 (FishingUI → 호출)</summary>
        public void CancelFishing()
        {
            StopFishing();
        }

        // ===== 입질 대기 → 미니게임 =====

        private IEnumerator WaitForBite()
        {
            float waitTime = Random.Range(_minWaitTime, _maxWaitTime);
            yield return new WaitForSeconds(waitTime);

            if (!_isFishing) yield break; // 중간에 종료됨

            _isWaitingForBite = false;
            _isMinigameActive = true;

            // 미니게임 초기화
            _pinPosition = 0f;
            _pinDirectionRight = true;
            _sweetSpotStart = Random.Range(0f, ProgressBarWidth - SweetSpotWidth);
        }

        // ===== 미니게임 핀 이동 =====

        private void UpdatePinMovement()
        {
            float speed = 250f; // 픽셀/초
            float delta = speed * Time.deltaTime;

            if (_pinDirectionRight)
            {
                _pinPosition += delta;
                if (_pinPosition >= ProgressBarWidth)
                {
                    _pinPosition = ProgressBarWidth;
                    _pinDirectionRight = false;
                }
            }
            else
            {
                _pinPosition -= delta;
                if (_pinPosition <= 0f)
                {
                    _pinPosition = 0f;
                    _pinDirectionRight = true;
                }
            }
        }

        // ===== 잡기 시도 (Space 키 - FishingUI에서 호출) =====

        /// <summary>
        /// 스페이스바 입력 시 호출. 핀이 스위트스팟 내에 있으면 성공.
        /// </summary>
        public void TryCatch()
        {
            if (!_isMinigameActive) return;
            if (PlayerInventory.Instance == null) return;

            bool success = _pinPosition >= _sweetSpotStart &&
                           _pinPosition <= _sweetSpotStart + SweetSpotWidth;

            _isMinigameActive = false;
            _isFishing = false;

            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
            }

            if (success)
            {
                var fish = GetRandomFish();
                if (fish != null)
                {
                    PlayerInventory.Instance.AddItem(fish, 1);
                    ShowPopup($"🐟 {fish.displayName}을(를) 낚았습니다!");
                }
            }
            else
            {
                ShowPopup("물고기가 도망갔습니다!");
            }
        }

        // ===== 확률 보정 아이템 획득 =====

        /// <summary>시간대/날씨 보정된 확률로 물고기 반환</summary>
        private PlayerInventory.ItemData GetRandomFish()
        {
            float rareWeight = 30f;   // 30%
            float legendaryWeight = 10f; // 10%
            float commonWeight = 60f; // 60%

            // 시간대 보정: 밤(20~4시) 희귀 확률 2배
            if (TimeManager.Instance != null)
            {
                int hour = TimeManager.Instance.Hour;
                bool isNight = hour >= 20 || hour < 4;
                if (isNight)
                {
                    rareWeight *= 2f;
                    legendaryWeight *= 2f;
                }
            }

            // 날씨 보정: 비 희귀 확률 1.5배
            if (WeatherManager.Instance != null &&
                WeatherManager.Instance.CurrentWeather == WeatherManager.WeatherType.Rain)
            {
                rareWeight *= 1.5f;
                legendaryWeight *= 1.5f;
            }

            float totalWeight = commonWeight + rareWeight + legendaryWeight;
            float roll = Random.Range(0f, totalWeight);

            if (roll < legendaryWeight)
                return PlayerInventory.Fish_Legendary;
            if (roll < legendaryWeight + rareWeight)
                return PlayerInventory.Fish_Rare;
            return PlayerInventory.Fish_Common;
        }

        // ===== 팝업 =====

        private void ShowPopup(string message)
        {
            _popupMessage = message;
            _popupTimer = 3f;
        }
    }
}