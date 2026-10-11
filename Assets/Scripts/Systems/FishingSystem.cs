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

        // 미니게임 내부 상태 (FishingUI에서 읽음 — 레거시 1바 핀 호환 유지)
        private float _pinPosition;
        private bool _pinDirectionRight = true;
        private float _sweetSpotStart;

        // [Figma 160:11] 2바 리일 시뮬 — 진행(물고기 당기는 중) + 장력(라인/위험)
        private float _reelProgress = 0f;   // 0..1 — Space로 증가, 1.0 도달 = 성공
        private float _reelTension = 0f;    // 0..1 — Space로 증가·자연 감소, 1.0 도달 = 라인 끊김 실패

        // [32차] 등급별 난이도 — 입질 순간 등급을 확정해 미니게임 파라미터에 반영.
        //   희귀 등급일수록: 당김 효율↓·장력 상승↑·저항(드레인)↑·완화↓·핀 속도↑ → 무작정 스페이스 연타 불가.
        private FishCatalog.FishTier _pendingTier = FishCatalog.FishTier.Common;
        private float _progressGain;   // 리일 1회 진행 상승
        private float _tensionCost;    // 리일 1회 장력 상승
        private float _progressDrain;  // 초당 진행 하락 (물고기 저항)
        private float _tensionRelax;   // 초당 장력 완화
        private float _pinSpeed;       // 핀 이동 속도(px/s — 레거시 1바)

        /// <summary>등급별 미니게임 파라미터 산출 — WaitForBite(입질) 시 1회.</summary>
        private void ApplyDifficulty(FishCatalog.FishTier tier)
        {
            switch (tier)
            {
                case FishCatalog.FishTier.Legendary:   // 전설 — 어렵음
                    _progressGain = 0.045f; _tensionCost = 0.100f;
                    _progressDrain = 0.050f; _tensionRelax = 0.030f; _pinSpeed = 400f;
                    break;
                case FishCatalog.FishTier.Rare:        // 희귀 — 중간
                    _progressGain = 0.060f; _tensionCost = 0.085f;
                    _progressDrain = 0.035f; _tensionRelax = 0.040f; _pinSpeed = 320f;
                    break;
                default:                               // 일반 — 기존 난이도(쉬움)
                    _progressGain = 0.080f; _tensionCost = 0.070f;
                    _progressDrain = 0.020f; _tensionRelax = 0.050f; _pinSpeed = 250f;
                    break;
            }
        }

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

        /// <summary>스위트스팟 너비 (픽셀) — [32차] 물고기 등급별 난이도(희귀할수록 좁음)</summary>
        public float SweetSpotWidth => _pendingTier == FishCatalog.FishTier.Legendary ? 15f
                                     : _pendingTier == FishCatalog.FishTier.Rare      ? 22f
                                     : 30f;

        /// <summary>프로그레스바 너비 (픽셀, 고정 300)</summary>
        public float ProgressBarWidth => 300f;

        // ===== [Figma 160:11] 2바 리일 상태 (새 UI가 읽음) =====
        /// <summary>리일 진행률 0..1 (1.0 도달 = 성공/물고기 획득)</summary>
        public float ReelProgress => _reelProgress;

        /// <summary>라인 장력 0..1 (1.0 도달 = 라인 끊김/실패)</summary>
        public float ReelTension => _reelTension;

        /// <summary>라인 장력 위험 임계 (이상 시 위험 배지 표시)</summary>
        public float TensionDangerThreshold => 0.75f;

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
                bool nearWater = IsNearWater();
                Transform anchor = _player != null ? _player : transform;
                // 물가에서 낚시 가능 → 공용 E 프롬프트 표시(등록), 아니면 해제.
                if (nearWater)
                    EInteractionPromptBridge.RaiseRegister(anchor, "E키를 눌러 상호작용", _waterCheckRange);
                else
                    EInteractionPromptBridge.RaiseUnregister(anchor);

                // 물가에서 E키 → 낚시 시작
                if (Input.GetKeyDown(KeyCode.E) && nearWater)
                    TryStartFishing();

                return;
            }

            // 낚시 중 → 프롬프트 해제 (FishingUTK 창이 대신 표시)
            EInteractionPromptBridge.RaiseUnregister(_player != null ? _player : transform);

            // 낚시 중: 미니게임 핀 업데이트
            if (_isMinigameActive)
            {
                UpdatePinMovement();
                UpdateReelSim();
            }

            // E키로 낚시 종료
            if (Input.GetKeyDown(KeyCode.E))
                StopFishing();
        }

        // ===== 물 감지 =====

        // [2026-10-11 수리] 물가 근접 OverlapSphere 버퍼 — GC 없는 재사용.
        private readonly Collider[] _waterOverlap = new Collider[16];

        /// <summary>물 근접 판정 — ①발밑 수직 하향(물 위에 선 경우) ②수평 근접 OverlapSphere(물가에 선 경우).
        /// 기존엔 수직 하향만 검사해 물 위에 직접 서야만 true — 해안에 서면 발밑이 땅이라 항상 false →
        /// "호수에서 낚시 상호작용 안 됨"의 근본 원인(테스트 15 영상 실측). 물가=해안 근접이므로 수평 판정 병행.</summary>
        private bool IsNearWater()
        {
            if (_player == null) return false;

            RaycastHit hit;
            Vector3 origin = _player.position + Vector3.up * 0.1f;
            if (Physics.Raycast(origin, Vector3.down, out hit, _waterCheckRange)
                && hit.collider.CompareTag("Water"))
                return true;

            int n = Physics.OverlapSphereNonAlloc(_player.position, _waterCheckRange, _waterOverlap);
            for (int i = 0; i < n; i++)
            {
                var c = _waterOverlap[i];
                if (c != null && c.CompareTag("Water")) return true;
            }
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

            // 미니게임 초기화 — [32차] 입질 순간 등급 확정 → 난이도 파라미터 반영
            _pendingTier = RollFishingTier();
            ApplyDifficulty(_pendingTier);
            _pinPosition = 0f;
            _pinDirectionRight = true;
            _sweetSpotStart = Random.Range(0f, ProgressBarWidth - SweetSpotWidth);
            _reelProgress = 0f;
            _reelTension = 0f;
        }

        // ===== 미니게임 핀 이동 =====

        private void UpdatePinMovement()
        {
            float speed = _pinSpeed; // [32차] 등급별 핀 속도(일반 250/희귀 320/전설 400 px/s)
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

        // ===== [Figma 160:11] 2바 리일 시뮬 =====
        //  진행(물고기를 당기는 중)은 Space로 상승·시간 경과로 살짝 하락(물고기가 버팀).
        //  장력은 Space로 상승·시간 경과로 완화. 장력 100% 도달 = 라인 끊겨 실패.
        private void UpdateReelSim()
        {
            // [32차] 등급별 저항/완화(일반 0.02/0.05 · 희귀 0.035/0.04 · 전설 0.05/0.03)
            _reelProgress = Mathf.Max(0f, _reelProgress - _progressDrain * Time.deltaTime);
            _reelTension = Mathf.Clamp(_reelTension - _tensionRelax * Time.deltaTime, 0f, 1f);
        }

        /// <summary>
        /// [Figma 160:11] Space 리일 입력 — 진행을 당긴다. 진행 100% 도달 = 성공, 장력 100% 도달 = 라인 끊김.
        /// (레거시 TryCatch(핀 1바)와 별개 — 원본 호환 유지.)
        /// </summary>
        public void TryReel()
        {
            if (!_isMinigameActive) return;
            if (PlayerInventory.Instance == null) return;

            // [32차] 등급별 리일 효율/장력(일반 0.08/0.07 · 희귀 0.06/0.085 · 전설 0.045/0.10)
            //   — 연타만으로 성공하던 기존 구조(13연타=무조건 성공)에서 리듬 캐치로 변경.
            float progressGain = _progressGain;
            float tensionCost  = _tensionCost;

            _reelProgress += progressGain;
            _reelTension += tensionCost;

            if (_reelTension >= 1f)
            {
                // 라인 끊김 — 실패
                FinishMinigame(false);
            }
            else if (_reelProgress >= 1f)
            {
                // 다 당김 — 성공
                FinishMinigame(true);
            }
        }

        /// <summary>[Figma 2바] 미니게임 종료 — 성공 시 물고기 획득.</summary>
        private void FinishMinigame(bool success)
        {
            bool wasActive = _isMinigameActive;
            _isMinigameActive = false;
            _isFishing = false;
            _reelProgress = 0f;
            _reelTension = 0f;

            if (_waitCoroutine != null)
            {
                StopCoroutine(_waitCoroutine);
                _waitCoroutine = null;
            }

            if (!wasActive) return;

            if (success)
            {
                var fish = GetRandomFish();
                if (fish != null)
                {
                    PlayerInventory.Instance.AddItem(fish, 1);
                    ShowPopup($"{fish.displayName}(을)를 낚았습니다!");
                    // [F3] 공용 결과 팝업 — Figma fishing-result-ui 템플릿
                    HarvestResultBridge.Publish(
                        HarvestResultBridge.HarvestKind.Fishing,
                        "대어 획득 성공!",
                        $"{fish.displayName}(을)를 낚았습니다!",
                        fish.description ?? "물고기입니다.",
                        fish.rarity,
                        1,
                        "인벤토리에 빈 공간이 충분한지 확인하십시오. 보관 시 무게가 적재량에 추가됩니다.",
                        fish,
                        immediate: true);
                }
            }
            else
            {
                ShowPopup("라인이 끊어졌습니다! 물고기가 도망갔습니다.");
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
                    ShowPopup($"{fish.displayName}(을)를 낚았습니다!");
                    // [F3] 공용 결과 팝업 — Figma fishing-result-ui 템플릿
                    HarvestResultBridge.Publish(
                        HarvestResultBridge.HarvestKind.Fishing,
                        "대어 획득 성공!",
                        $"{fish.displayName}(을)를 낚았습니다!",
                        fish.description ?? "물고기입니다.",
                        fish.rarity,
                        1,
                        "인벤토리에 빈 공간이 충분한지 확인하십시오. 보관 시 무게가 적재량에 추가됩니다.",
                        fish,
                        immediate: true);
                }
            }
            else
            {
                ShowPopup("물고기가 도망갔습니다!");
            }
        }

        // ===== 확률 보정 아이템 획득 =====

        /// <summary>
        /// [32차] 시간대/날씨 보정 등급 롤 — 입질(WaitForBite) 시 1회 호출해 _pendingTier 확정.
        /// 미니게임 난이도와 결과 물고기가 같은 등급을 공유(어려운 게임 = 희귀 물고기).
        /// </summary>
        private FishCatalog.FishTier RollFishingTier()
        {
            float rareWeight = 30f;      // 30%
            float legendaryWeight = 10f; // 10%
            float commonWeight = 60f;    // 60%

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
                return FishCatalog.FishTier.Legendary;
            if (roll < legendaryWeight + rareWeight)
                return FishCatalog.FishTier.Rare;
            return FishCatalog.FishTier.Common;
        }

        /// <summary>입질 시 확정된 등급의 물고기 반환 — [32차] 성공 시점 재추첨 폐지(난이도-보상 정합).</summary>
        private PlayerInventory.ItemData GetRandomFish()
        {
            return FishCatalog.GetRandomFishItem(_pendingTier);
        }

        // ===== 팝업 =====

        private void ShowPopup(string message)
        {
            _popupMessage = message;
            _popupTimer = 3f;
        }
    }
}