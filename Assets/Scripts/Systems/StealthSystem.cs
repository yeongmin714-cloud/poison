using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 34: 은신 (Stealth) 코어 시스템.
    /// Ctrl 키 토글, 이동속도 50%, 카메라 낮춤, 비네트 효과,
    /// 발각 게이지 계산, 레벨 차이 보정.
    /// </summary>
    public class StealthSystem : MonoBehaviour
    {
        public static StealthSystem Instance { get; private set; }

        [Header("Stealth Settings")]
        [SerializeField] private float _stealthSpeedMultiplier = 0.5f;
        [SerializeField] private float _cameraLowerAmount = 0.3f;
        [SerializeField] private float _cameraLerpSpeed = 5f;

        [Header("Detection Settings")]
        [SerializeField] private float _baseDetectionRange = 15f;
        [SerializeField] private float _detectionAngle = 60f;
        [SerializeField] private float _behindAngle = 150f; // 뒤에서 30도 이내
        [SerializeField] private float _dayDetectionMultiplier = 1.5f;
        [SerializeField] private float _nightDetectionMultiplier = 0.7f;
        [SerializeField] private float _levelRangeBonus = 0.3f; // Lv 차이 +10당 감지거리 30% 감소

        [Header("Vignette Effect")]
        [SerializeField] private Color _vignetteColor = new Color(0f, 0f, 0f, 0.3f);
        [SerializeField] private int _vignetteBorderSize = 40;

        // 싱글톤
        private PlayerMovement _playerMovement;
        private Camera _mainCamera;
        private Vector3 _cameraOriginalLocalPos;
        private float _cameraOriginalFOV;

        // 은신 상태
        private bool _isStealthed = false;

        // 발각 게이지
        private float _detectionGauge = 0f; // 0~100
        private float _detectionDecayRate = 10f; // 초당 감소

        // 플레이어 레벨 캐시
        private int _playerLevel = 1;

        // 비네트 텍스처 캐시
        private Texture2D _vignetteTexture;

        // ===== Public Properties =====
        public bool IsStealthed => _isStealthed;
        public float DetectionGauge => _detectionGauge;
        public float DetectionGaugeNormalized => Mathf.Clamp01(_detectionGauge / 100f);
        public float DetectionRangeBonus => GetLevelDetectionBonus();
        public bool IsFullyDetected => _detectionGauge >= 100f;

        // ===== Events =====
        public event System.Action<bool> OnStealthStateChanged;
        public event System.Action<float> OnDetectionGaugeChanged;

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
            _playerMovement = GetComponent<PlayerMovement>();
            if (_playerMovement == null)
                _playerMovement = FindFirstObjectByType<PlayerMovement>();

            _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                _cameraOriginalLocalPos = _mainCamera.transform.localPosition;
                _cameraOriginalFOV = _mainCamera.fieldOfView;
            }

            if (PlayerStats.Instance != null)
                _playerLevel = PlayerStats.Instance.Level;

            // 비네트 텍스처 생성
            CreateVignetteTexture();

            Debug.Log("[StealthSystem] 은신 시스템 초기화 완료");
        }

        private void CreateVignetteTexture()
        {
            _vignetteTexture = new Texture2D(4, 4);
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    _vignetteTexture.SetPixel(x, y, Color.white);
            _vignetteTexture.Apply();
        }

        private void Update()
        {
            if (_playerMovement == null) return;

            // 플레이어 레벨 갱신
            if (PlayerStats.Instance != null)
                _playerLevel = PlayerStats.Instance.Level;

            HandleCameraLower();
            UpdateDetectionGauge();

            // 발각 게이지 이벤트
            OnDetectionGaugeChanged?.Invoke(_detectionGauge);
        }

        public void ToggleStealth()
        {
            _isStealthed = !_isStealthed;

            // 속도 수정자 제거: PlayerMovement에서 직접 처리
            // SpeedModifier는 BiomeEffectController 등 다른 시스템과 충돌 방지를 위해 사용 안 함

            // 발각 게이지 리셋 (은신 해제 시)
            if (!_isStealthed)
                _detectionGauge = 0f;

            OnStealthStateChanged?.Invoke(_isStealthed);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[StealthSystem] 은신 {( _isStealthed ? "ON" : "OFF" )}");
#endif
        }

        private void HandleCameraLower()
        {
            if (_mainCamera == null) return;

            Vector3 targetPos = _cameraOriginalLocalPos;
            if (_isStealthed)
                targetPos.y -= _cameraLowerAmount;

            _mainCamera.transform.localPosition = Vector3.Lerp(
                _mainCamera.transform.localPosition,
                targetPos,
                Time.deltaTime * _cameraLerpSpeed
            );
        }

        /// <summary>
        /// 발각 게이지 업데이트.
        /// 주변 NPC 거리/시야각/조명/소음 기반 계산.
        /// </summary>
        private void UpdateDetectionGauge()
        {
            if (!_isStealthed)
            {
                // 은신 중이 아니면 게이지 감소
                _detectionGauge = Mathf.Max(0f, _detectionGauge - _detectionDecayRate * Time.deltaTime);
                return;
            }

            // 주변 NPC 스캔
            float totalDetection = 0f;
            NPCAwarenessSystem[] npcs = FindObjectsByType<NPCAwarenessSystem>(FindObjectsSortMode.None);
            if (npcs == null || npcs.Length == 0)
            {
                // 감지 대상 없음 → 게이지 감소
                _detectionGauge = Mathf.Max(0f, _detectionGauge - _detectionDecayRate * Time.deltaTime);
                return;
            }

            Vector3 playerPos = transform.position;
            Vector3 playerForward = transform.forward;
            bool isNight = IsNightTime();

            foreach (var npc in npcs)
            {
                if (!npc.IsActive) continue;

                float detection = CalculateNPCDetection(npc, playerPos, playerForward, isNight);
                totalDetection += detection;
            }

            // 프레임당 최대 증가량 제한 (갑작스러운 발각 방지)
            float maxIncreasePerFrame = 30f * Time.deltaTime;
            float netChange = Mathf.Clamp(totalDetection * Time.deltaTime, -_detectionDecayRate * Time.deltaTime, maxIncreasePerFrame);

            _detectionGauge = Mathf.Clamp(_detectionGauge + netChange, 0f, 100f);
        }

        /// <summary>
        /// 단일 NPC의 플레이어 감지도를 계산합니다.
        /// </summary>
        private float CalculateNPCDetection(NPCAwarenessSystem npc, Vector3 playerPos, Vector3 playerForward, bool isNight)
        {
            if (npc == null) return 0f;

            Vector3 npcPos = npc.transform.position;
            Vector3 npcForward = npc.transform.forward;
            float distance = Vector3.Distance(playerPos, npcPos);

            // 감지 거리 계산 (레벨 차이 보정)
            float detectionRange = _baseDetectionRange;
            if (npc.NPCLevel > 0)
            {
                int levelDiff = _playerLevel - npc.NPCLevel;
                if (levelDiff >= 10)
                    detectionRange *= (1f - _levelRangeBonus);
            }

            // 야간 보정
            if (isNight)
                detectionRange *= _nightDetectionMultiplier;
            else
                detectionRange *= _dayDetectionMultiplier;

            // 어두운 망토 아이템 보정 (야간 감지 거리 50% 감소)
            if (isNight && HasItemEquipped("dark_cloak"))
                detectionRange *= 0.5f;

            // 은신 부츠 보정 (발소음 50% 감소 → 감지 거리 감소)
            if (HasItemEquipped("stealth_boots"))
                detectionRange *= 0.85f; // 발소음 감소로 감지 거리 15% 추가 감소

            // 거리가 너무 멀면 감지 없음
            if (distance > detectionRange)
                return 0f;

            // 거리 기반 감지 (가까울수록 높음)
            float distanceFactor = 1f - Mathf.Clamp01(distance / detectionRange);
            float detection = distanceFactor * 40f;

            // 시야각 확인 (NPC가 플레이어를 보고 있는가)
            Vector3 dirToPlayer = (playerPos - npcPos).normalized;
            float angleToPlayer = Vector3.Angle(npcForward, dirToPlayer);

            bool inSightCone = angleToPlayer < _detectionAngle;
            bool behindPlayer = Vector3.Angle(playerForward, -dirToPlayer) < (180f - _behindAngle);

            if (inSightCone)
            {
                // 정면 시야: 감지 증가
                detection *= 2f;
            }
            else
            {
                // 시야 밖: 감지 감소
                detection *= 0.3f;
            }

            // 플레이어 뒤에서 접근 시 감지 감소
            if (behindPlayer)
                detection *= 0.5f;

            // NPC 상태에 따른 보정
            switch (npc.CurrentAwarenessState)
            {
                case NPCAwarenessSystem.AwarenessState.Suspicious:
                    detection *= 1.5f;
                    break;
                case NPCAwarenessSystem.AwarenessState.Searching:
                    detection *= 2f;
                    break;
                case NPCAwarenessSystem.AwarenessState.Alert:
                    detection *= 1.8f;
                    break;
            }

            // 은신 물약 효과 (반투명 → 감지 감소)
            if (HasBuff("StealthInvisibility"))
                detection *= 0.3f;

            return detection;
        }

        /// <summary>
        /// 야간 여부 확인 (DayNightCycle 연동).
        /// </summary>
        private bool IsNightTime()
        {
            var dnc = FindFirstObjectByType<DayNightCycle>();
            if (dnc == null) return false;

            // DayNightCycle이 태양 각도를 추적 중이면 그대로 사용
            // 간단한 방식: TimeManager의 DayProgress 확인
            var timeManager = FindFirstObjectByType<TimeManager>();
            if (timeManager == null) return false;

            float progress = timeManager.DayProgress;
            // 0.0 ~ 0.25 (새벽), 0.75 ~ 1.0 (밤)
            return progress < 0.25f || progress > 0.75f;
        }

        /// <summary>
        /// 특정 버프 보유 여부 확인.
        /// </summary>
        private bool HasBuff(string buffId)
        {
            if (BuffManager.Instance == null) return false;
            var buffs = BuffManager.Instance.GetActiveBuffs();
            foreach (var b in buffs)
            {
                if (b.BuffId == buffId) return true;
            }
            return false;
        }

        /// <summary>
        /// 특정 아이템 장착 여부 확인 (임시 — 실제 장비 시스템 연동 필요).
        /// 인벤토리에 해당 아이템이 있는지 간단히 확인.
        /// </summary>
        private bool HasItemEquipped(string itemId)
        {
            // TODO: 실제 장비 슬롯 시스템 연동 (Phase 4 장비 시스템)
            // 현재는 임시로 인벤토리 확인
            if (PlayerInventory.Instance == null) return false;

            var slots = PlayerInventory.Instance.GetAllSlots();
            foreach (var slot in slots)
            {
                if (slot != null && slot.item != null && slot.item.id == itemId && slot.count > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 레벨 차이에 따른 감지 거리 보정 계수를 반환합니다.
        /// 플레이어 Lv > NPC Lv +10 → 감지거리 30% 감소
        /// </summary>
        private float GetLevelDetectionBonus()
        {
            return _levelRangeBonus;
        }

        /// <summary>
        /// 강제로 은신 해제 (암살 발각 등).
        /// </summary>
        public void ForceExitStealth()
        {
            if (!_isStealthed) return;
            _isStealthed = false;

            // SpeedModifier는 PlayerMovement에서 직접 관리하므로 제거

            _detectionGauge = 0f;
            OnStealthStateChanged?.Invoke(false);
        }

        /// <summary>
        /// 발각 게이지 즉시 증가 (소음 발생 등).
        /// </summary>
        public void AddDetection(float amount)
        {
            if (!_isStealthed) return;
            _detectionGauge = Mathf.Clamp(_detectionGauge + amount, 0f, 100f);
        }

        // ===== IMGUI: 비네트 효과 =====
        private void OnGUI()
        {
            if (!_isStealthed) return;

            // 화면 주변 비네트 효과
            if (_vignetteTexture != null)
            {
                Color prevColor = GUI.color;
                GUI.color = _vignetteColor;

                // 상단
                GUI.DrawTexture(new Rect(0, 0, Screen.width, _vignetteBorderSize), _vignetteTexture);
                // 하단
                GUI.DrawTexture(new Rect(0, Screen.height - _vignetteBorderSize, Screen.width, _vignetteBorderSize), _vignetteTexture);
                // 좌측
                GUI.DrawTexture(new Rect(0, 0, _vignetteBorderSize, Screen.height), _vignetteTexture);
                // 우측
                GUI.DrawTexture(new Rect(Screen.width - _vignetteBorderSize, 0, _vignetteBorderSize, Screen.height), _vignetteTexture);

                GUI.color = prevColor;
            }
        }

        private void OnDestroy()
        {
            if (_vignetteTexture != null)
                Destroy(_vignetteTexture);

            if (Instance == this)
                Instance = null;
        }
    }
}
