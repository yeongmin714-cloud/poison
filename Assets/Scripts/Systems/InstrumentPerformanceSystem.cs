using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase O7: 바드 연주 시스템 — B키 토글.
    /// docs/reference/openmmo/INSTRUMENT.md 이식 (포이즌엔 노트 신스 없음 → 악기 아이템 + 연주 버프로 변환,
    /// 노트 신스는 YAGNI). 장착된 악기의 InstrumentData 프로필 퍼센트를 파티 전체(가드+용병)에
    /// 버프 소스 "performance"로 부여, duration 후 자동 종료 시 소스 해제.
    /// 종료 후 cooldown 동안 재시작 불가(30초).
    /// </summary>
    public class InstrumentPerformanceSystem : MonoBehaviour
    {
        public static InstrumentPerformanceSystem Instance { get; private set; }

        /// <summary>연주 버프 소스 ID (BardBuffManager.ClearBySource 대응).</summary>
        public const string PerformanceSource = "performance";

        [Header("연주 설정")]
        [SerializeField] private float _duration = 60f;   // 연주 지속 시간(초)
        [SerializeField] private float _cooldown = 30f;   // 종료 후 재시작 대기(초)
        [SerializeField] private float _refreshInterval = 1.0f; // 버프 재등록 주기(신규 아군 커버)

        private bool _isPerforming = false;
        private float _lastEndTime = -999f;  // 마지막 연주 종료 시각(-999 = 쿨다운 없음)
        private float _endAtTime = 0f;
        private float _refreshTimer = 0f;
        private InstrumentProfile _activeProfile;

        /// <summary>현재 연주 중 여부.</summary>
        public bool IsPerforming => _isPerforming;

        /// <summary>연주 남은 시간(초) — 비연주 중 0.</summary>
        public float RemainingSeconds => _isPerforming ? Mathf.Max(0f, _endAtTime - Time.time) : 0f;

        /// <summary>연주 중인 악기 ID (비연주 중 null).</summary>
        public string ActiveInstrumentId => _isPerforming ? _activeProfile.itemId : null;

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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                EndPerformance();
                Instance = null;
            }
        }

        /// <summary>쿨다운 판정 순수 함수 — 테스트 가능. lastEndTime < 0(이력 없음)이면 항상 허용.</summary>
        public static bool CanStartAgain(float lastEndTime, float now, float cooldownSeconds)
        {
            if (lastEndTime < 0f) return true;
            return (now - lastEndTime) >= cooldownSeconds;
        }

        /// <summary>
        /// 연주 시작 — 조건: 비연주 중, BardBuffManager/PlayerStats 존재, 등록된 악기 프로필, 쿨다운 통과.
        /// 성공 시 소스 "performance"로 파티 전체(가드+용병)에 프로필 버프 등록.
        /// </summary>
        public bool TryStartPerformance(string instrumentItemId)
        {
            if (_isPerforming)
            {
                Debug.Log("[Instrument] 이미 연주 중입니다.");
                return false;
            }
            // [O7] 싱글턴 프로퍼티 의존 제거 — 에디터 테스트 등 Awake 미실행 환경 대응(FindAnyObjectByType 폴백)
            var buffMgr = BardBuffManager.Instance ?? FindAnyObjectByType<BardBuffManager>();
            if (buffMgr == null)
            {
                Debug.LogWarning("[Instrument] BardBuffManager 없음 — 연주 버프 등록 불가.");
                return false;
            }
            var stats = PlayerStats.Instance ?? FindAnyObjectByType<PlayerStats>();
            if (stats == null)
            {
                Debug.LogWarning("[Instrument] PlayerStats 없음 — 연주 불가.");
                return false;
            }
            var profile = InstrumentData.GetProfile(instrumentItemId);
            if (!profile.HasValue)
            {
                Debug.LogWarning($"[Instrument] 미등록 악기: {instrumentItemId}");
                return false;
            }
            if (!CanStartAgain(_lastEndTime, Time.time, _cooldown))
            {
                Debug.Log($"[Instrument] 쿨다운 중 — {_cooldown - (Time.time - _lastEndTime):F0}초 후 재개 가능.");
                return false;
            }

            _activeProfile = profile.Value;
            ApplyBuffSource();
            _isPerforming = true;
            _endAtTime = Time.time + _duration;
            _refreshTimer = 0f;
            Debug.Log($"[Instrument] 🎵 연주 시작: {_activeProfile.displayName} " +
                      $"(공+{_activeProfile.attackBuffPercent:F0}% 방+{_activeProfile.defenseBuffPercent:F0}% " +
                      $"속+{_activeProfile.speedBuffPercent:F0}% / {_duration:F0}초)");
            return true;
        }

        /// <summary>연주 종료 — 버프 소스 해제 + 쿨다운 기록. (수동 조기 종료/자동 종료 공용)</summary>
        public void EndPerformance()
        {
            if (!_isPerforming) return;

            var bm = BardBuffManager.Instance ?? FindAnyObjectByType<BardBuffManager>();
            bm?.ClearBySource(PerformanceSource);
            _isPerforming = false;
            _lastEndTime = Time.time;
            Debug.Log($"[Instrument] 연주 종료: {_activeProfile.displayName} (쿨다운 {_cooldown:F0}초)");
        }

        private void Update()
        {
            // B키 폴링 — 연주 토글.
            // ⚠️ B키 중복 패치 방지: 다른 시스템과 키 충돌 시 이 메서드의 KeyCode만 변경할 것.
            if (Input.GetKeyDown(KeyCode.B))
                HandleBKey();

            if (!_isPerforming) return;

            // 자동 종료 (duration 경과)
            if (Time.time >= _endAtTime)
            {
                EndPerformance();
                return;
            }

            // 주기 재등록 — 연주 중 스폰된 아군도 버프 커버
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= _refreshInterval)
            {
                _refreshTimer = 0f;
                ApplyBuffSource();
            }
        }

        /// <summary>B키 처리: 연주 중이면 조기 종료, 아니면 바드+악기 확인 후 연주 시도.</summary>
        private void HandleBKey()
        {
            if (_isPerforming)
            {
                EndPerformance();
                return;
            }

            if (MercenaryManager.Instance == null)
            {
                Debug.Log("[Instrument] 바드 고용 + 악기 장착 필요");
                return;
            }

            // 고용된 용병 중 Bard 직업 검색 → 그 바드의 Instrument 슬롯 장착 악기로 연주
            var hired = MercenaryManager.Instance.GetHiredMercenaries();
            if (hired != null)
            {
                foreach (var merc in hired)
                {
                    if (!merc.data.jobType.Contains("Bard")) continue;

                    var equipped = GuardEquipmentSystem.Instance?.GetMercenaryEquipped(
                        merc.data.id, GuardEquipmentSystem.EquipSlot.Instrument);
                    var itemId = equipped?.itemData.id;
                    if (!string.IsNullOrEmpty(itemId))
                    {
                        TryStartPerformance(itemId);
                        return;
                    }
                }
            }

            Debug.Log("[Instrument] 바드 고용 + 악기 장착 필요");
        }

        /// <summary>소스 "performance"로 현재 존재하는 가드+용병 전체에 버프 등록.
        /// BardBuffManager에 "전체 일괄 적용" 메서드가 없어 BardMercenary와 동일한
        /// Register 개별 API를 전체 순회로 사용 (ApplyBuffData 헬퍼).</summary>
        private void ApplyBuffSource()
        {
            var buffData = new BardBuffData
            {
                attackBonus = _activeProfile.attackBuffPercent / 100f,
                defenseBonus = _activeProfile.defenseBuffPercent / 100f,
                speedBonus = _activeProfile.speedBuffPercent / 100f,
                sourceId = PerformanceSource
            };

            // 가드 전체 (생존자만)
            var guards = FindObjectsByType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                if (!guard.IsAlive) continue;
                BardBuffManager.Instance.RegisterBuffedGuard(guard, buffData);
            }

            // 용병 전체 (바드 본인 포함)
            var mercs = FindObjectsByType<MercenaryPlaceholder>();
            foreach (var merc in mercs)
            {
                BardBuffManager.Instance.RegisterBuffedMercenary(merc, buffData);
            }
        }
    }
}
