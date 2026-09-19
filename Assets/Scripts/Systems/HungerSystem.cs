using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase O10 C-O10-02: 허기 시스템 — docs/reference/openmmo/HUNGER.md 이식(죽음 없음, 페널티만).
    /// - 허기 0~100(기본 100). 인게임 1시간당 -4 → 하루(24h) -96, 약 1일 1식 유도.
    /// - 게임시간 경과 취득: TimeManager에 DeltaGameHours 유사 API가 없어 TimeScale 기반 환산 사용
    ///   (게임초 = 현실 deltaTime × TimeManager.TimeScale → ÷3600 = 게임시간).
    ///   TimeManager 부재 환경(에디터 테스트 등)은 감소 스킵 — NRE 없음(스테일 세이프).
    ///   unscaledDeltaTime 폴백은 게임시간과 무관한 현실시간이라 왜곡을 만들므로 사용하지 않는다.
    /// - 페널티 API: BlocksNaturalRegen(Hunger &lt; 30 → 자연 재생 정지, HUNGER.md 벤치 "쇠약 시 회복 정지"
    ///   — RegenRules.CanRegen은 수정하지 않고 이 시스템에서 게이트),
    ///   GetSpeedMultiplier(&lt;20: ×0.7 / &lt;50: ×0.85 / 그 외 ×1.0).
    /// - 저장: PlayerPrefs "hunger_v1" — Awake Load(멱등), 변경 시 10초 스로틀 Save.
    /// - UI: 정적 이벤트 HungerChanged(현재값 전달). Eat/ForceSet은 이산 행위라 즉시 발화,
    ///   연속 감소 틱만 1초 스로틀(UI 폴링 0.25초 백업 — StatusWindowUTK).
    /// - 싱글턴 + DontDestroyOnLoad, GameManager 부트스트랩 1줄(InstrumentPerformanceSystem 옆).
    /// </summary>
    public class HungerSystem : MonoBehaviour
    {
        public static HungerSystem Instance { get; private set; }

        /// <summary>UI 갱신용 정적 이벤트 — 허기 변경 시 현재 값(0~100) 전달.</summary>
        public static event System.Action<float> HungerChanged;

        // ===== 튜닝 상수 =====
        public const float MaxHunger = 100f;

        /// <summary>인게임 1시간당 허기 감소량(튜닝: 하루 24h → -96, 약 1일 1식 유도).</summary>
        public const float ConsumptionPerGameHour = 4f;

        /// <summary>자연 재생 정지 임계값(이 값 미만) — HUNGER.md 벤치.</summary>
        public const float RegenBlockThreshold = 30f;

        /// <summary>이속 페널티 임계값(미만 ×0.7 / 미만 ×0.85).</summary>
        public const float SlowSpeedThreshold = 20f;
        public const float NormalSpeedThreshold = 50f;

        private const string SaveKey = "hunger_v1";
        private const float SaveThrottleSeconds = 10f;
        private const float EventThrottleSeconds = 1f;

        [SerializeField] private float _hunger = MaxHunger;

        // 내부 상태
        private TimeManager _timeMgr;              // 캐싱(파괴되면 Unity == null → 재탐색)
        private bool _saveDirty;                   // 미저장 변경 플래그(10초 스로틀 저장)
        private float _lastSaveTime = -999f;
        private float _lastEventTime = -999f;      // 감소 틱 이벤트 스로틀(Time.time 기준)

        /// <summary>현재 허기(0~100 클램프).</summary>
        public float Hunger => _hunger;

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            // === 싱글턴: 첫 번째가 주인 (InstrumentPerformanceSystem 패턴 동일) ===
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load(); // 멱등 초기화 — 저장된 값 복원(키 없으면 기본 100)
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Save(); // 파괴 시 미저장 변경 즉시 반영
                Instance = null;
            }
        }

        private void Update()
        {
            // 지연 저장 플러시 — 10초 스로틀 경과 시 대기 중 변경 저장
            if (_saveDirty && Time.unscaledTime - _lastSaveTime >= SaveThrottleSeconds)
                Save();

            // 게임시간 경과 취득 — TimeManager 부재 시 감소 스킵(스테일 세이프: NRE 없음)
            if (_timeMgr == null) _timeMgr = TimeManager.Instance;
            if (_timeMgr == null) return;

            float gameHours = Time.deltaTime * _timeMgr.TimeScale / 3600f;
            if (gameHours <= 0f) return; // 정지/역행 시간 — 감소 없음

            float before = _hunger;
            _hunger = Mathf.Clamp(_hunger - gameHours * ConsumptionPerGameHour, 0f, MaxHunger);
            if (_hunger < before)
            {
                FireHungerChangedThrottled(); // 감소 틱 — 1초 스로틀 발화
                _saveDirty = true;            // 저장은 10초 스로틀(Update 플러시)
            }
        }

        // ===== Public API (컴포넌트 public 규약) =====

        /// <summary>섭취 — 허기 회복(+클램프 0~100). 과다 섭취도 상한에서 절단.</summary>
        public void Eat(float amount)
        {
            _hunger = Mathf.Clamp(_hunger + amount, 0f, MaxHunger);
            FireHungerChangedImmediate(); // 이산 행위 — 즉시 발화
            _saveDirty = true;
        }

        /// <summary>강제 설정(테스트/디버그) — 0~100 클램프.</summary>
        public void ForceSet(float v)
        {
            _hunger = Mathf.Clamp(v, 0f, MaxHunger);
            FireHungerChangedImmediate();
        }

        /// <summary>자연 재생 게이트 — 쇠약(Hunger &lt; 30)이면 true. NaturalRegenSystem.Tick에서 소비.
        /// RegenRules.CanRegen은 수정하지 않는다(O10 규약 — 게이트는 이 시스템 소유).</summary>
        public bool BlocksNaturalRegen() => _hunger < RegenBlockThreshold;

        /// <summary>이속 배율 — Hunger &lt;20: ×0.7 / &lt;50: ×0.85 / 그 외 ×1.0.</summary>
        public float GetSpeedMultiplier()
        {
            if (_hunger < SlowSpeedThreshold) return 0.7f;
            if (_hunger < NormalSpeedThreshold) return 0.85f;
            return 1f;
        }

        /// <summary>게임시간 1시간당 허기 소모량(= 4). C#에 const 메서드가 없어 상수 반환 메서드로 제공.</summary>
        public float GetConsumptionPerGameHour() => ConsumptionPerGameHour;

        // ===== 저장 (PlayerPrefs "hunger_v1") =====

        /// <summary>저장 — PlayerPrefs.SetFloat + Save. dirty 플래그 해제.</summary>
        public void Save()
        {
            PlayerPrefs.SetFloat(SaveKey, _hunger);
            PlayerPrefs.Save();
            _saveDirty = false;
            _lastSaveTime = Time.unscaledTime;
        }

        /// <summary>복원 — PlayerPrefs.GetFloat(키 없으면 기본 100). 멱등.</summary>
        public void Load()
        {
            _hunger = Mathf.Clamp(PlayerPrefs.GetFloat(SaveKey, MaxHunger), 0f, MaxHunger);
        }

        // ===== 이벤트 발화 =====

        /// <summary>감소 틱용 — 1초 스로틀(연속 감소로 UI 이벤트 홍수 방지).</summary>
        private void FireHungerChangedThrottled()
        {
            if (Time.time - _lastEventTime < EventThrottleSeconds) return;
            _lastEventTime = Time.time;
            HungerChanged?.Invoke(_hunger);
        }

        /// <summary>Eat/ForceSet용 — 이산 행위 즉시 발화.</summary>
        private void FireHungerChangedImmediate()
        {
            _lastEventTime = Time.time;
            HungerChanged?.Invoke(_hunger);
        }

        // ===== 디버그 =====

        [ContextMenu("허기 리셋")]
        private void ResetHunger()
        {
            ForceSet(MaxHunger);
            Debug.Log("[HungerSystem] 허기 리셋 → 100");
        }
    }
}
