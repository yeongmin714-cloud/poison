// ============================================================================
// [무번호 63-B] 플레이어 상태이상 14종 시스템 (2026-11-16 계획서 §1+§2 이행)
//  · 단일 파일 — Assets/Scripts/Systems/PlayerStatusEffectSystem.cs (신규)
//  · UI(StatusEffectBarUTK)와 공유하는 교차 시그니처 불변(계획서 §1 핀)
//  · 자가부트: [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] 멱등 Ensure + DDOL (BuffManager 선례)
//  · DOT 자기재귀 가드 협약: DOT은 PlayerHealth.TakeDamage 3인자 오버로드에 weaponType
//    "status"를 전달 — §4 훅#1(PlayerHealth, Phase C)이 이 문자열로 피격 발동 판정을 스킵.
//  · BuffManager 무수정 계약(§6) — 스탯 가감은 본 파일이 Apply/Reverse 산식을 동일하게 수행.
// ============================================================================

using System;
using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

// [63-B] EditMode 테스트(§5-1)가 internal 순수 훅(TickCore/ResetForTest)에 접근하기 위한 핀.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ProjectName.Tests.EditMode")]

namespace ProjectName.Systems
{
    /// <summary>
    /// [63-B] 상태이상 식별자 — 14종 전부 채택(사용자 확정). 순서 = 계획서 §1 정의와 1:1.
    /// </summary>
    public enum StatusEffectId
    {
        Bleeding, Poisoning, Slowness, Exhaustion, Fracture, Daze,
        Hunger, Burn, Hypothermia, Terror, Infection, Weaken,
        Drunkenness, BrittleDefense
    }

    /// <summary>
    /// [63-B] 플레이어 상태이상 시스템 — 정적 상태 + 얇은 MonoBehaviour 드라이버.
    ///  · 전체 상태가 static — EditMode(AddComponent/Awake 미호출, Time.deltaTime=0)에서도
    ///    정적 경로(TickCore)만으로 완전 동작(계획서 §5-1 제약).
    ///  · Update에서만 Tick(Time.deltaTime) 호출 — 만료/DOT/감염 연쇄/허기 판정 수행.
    /// </summary>
    public class PlayerStatusEffectSystem : MonoBehaviour
    {
        public static PlayerStatusEffectSystem Instance { get; private set; }

        // ── [63-B] 핵심 API (UI와 공유하는 교차 시그니처 — 이 시그니처 불변, 계획서 §1) ──

        /// <summary>추가/제거/만료 시 1회 발화(UI 폴링 보조) — 멱등 갱신(재추가) 시에는 발화하지 않음.</summary>
        public static event Action OnChanged;

        /// <summary>
        /// [63-B] 상태이상 추가 — 멱등. 이미 활성이면 remaining=duration 갱신(중첩 아님:
        /// 스탯 재적용 금지 — 1회 가감 원칙), 그 외 신규 추가 시 스탯 1회 가감 + OnChanged.
        /// </summary>
        public static void AddEffect(StatusEffectId id, float duration)
        {
            if (duration <= 0f) duration = DefaultDurationOf(id); // 방어 — 명시 지속 누락 시 §2 정의치
            if (_effects.TryGetValue(id, out var existing))
            {
                // [63-B] 멱등 갱신 — 중첩 아님. 스탯 재적용/OnChanged 모두 없음(추가/제거/만료만 발화).
                existing.Remaining = duration;
                existing.Duration = duration;
                return;
            }
            var state = new EffectState { Remaining = duration, Duration = duration };
            _effects[id] = state;
            _activeOrder.Add(id);
            ApplyStats(id);   // 적용 시 1회 PlayerStats 직접 가감(BuffManager.ApplyBuff 동일 산식)
            NotifyChanged();
        }

        /// <summary>[63-B] 수동 해제(치료 포함 공용 경로) — 역가감 + OnChanged. 비활성 해제는 무시(멱등).</summary>
        public static void RemoveEffect(StatusEffectId id) => Deactivate(id);

        /// <summary>
        /// [63-B] 활성 상태이상 전체 해제 — §4 훅#7④("상태이상 방지" 소비 아이템) 소비 예정.
        /// </summary>
        public static void RemoveAllEffects()
        {
            for (int i = _activeOrder.Count - 1; i >= 0; i--)
                Deactivate(_activeOrder[i]);
        }

        /// <summary>[63-B] 활성 여부.</summary>
        public static bool HasEffect(StatusEffectId id) => _effects.ContainsKey(id);

        /// <summary>[63-B] 남은 시간(초) — 비활성 시 0. UI 원형 아크 분자.</summary>
        public static float GetRemainingSeconds(StatusEffectId id) => _effects.TryGetValue(id, out var s) ? s.Remaining : 0f;

        /// <summary>[63-B] 현재 구간 전체 지속(초) — UI 분모. 비활성 시 0.</summary>
        public static float GetDurationSeconds(StatusEffectId id) => _effects.TryGetValue(id, out var s) ? s.Duration : 0f;

        /// <summary>[63-B] 활성 목록 — UI 표시 순서(추가 순) 스냅샷(BuffManager.GetActiveBuffs 선례).</summary>
        public static IReadOnlyList<StatusEffectId> GetActiveEffects() => new List<StatusEffectId>(_activeOrder);

        // ── [63-B] 해제 수단 (치료 채널 — ConsumableSystem/수면/환경에서 호출, §1 규약) ──

        /// <summary>[63-B] 붕대 — Bleeding 제거. 출혈 치료 시 감염 연쇄 타이머 리셋.</summary>
        public static void CureBleeding()
        {
            RemoveEffect(StatusEffectId.Bleeding);
            _infectionTimer = 0f;
        }

        /// <summary>[63-B] 해독제 — Poisoning 제거.</summary>
        public static void CurePoisoning() => RemoveEffect(StatusEffectId.Poisoning);

        /// <summary>
        /// [63-B] 수면 — Exhaustion/Fracture/Hypothermia/Terror/Infection 제거 +
        /// Hunger remaining을 60%로 절반 완화(제거 아님 — §2 해제=식사). §1 규약.
        /// </summary>
        public static void CureSleep()
        {
            RemoveEffect(StatusEffectId.Exhaustion);
            RemoveEffect(StatusEffectId.Fracture);
            RemoveEffect(StatusEffectId.Hypothermia);
            RemoveEffect(StatusEffectId.Terror);
            RemoveEffect(StatusEffectId.Infection);
            if (_effects.TryGetValue(StatusEffectId.Hunger, out var hunger))
                hunger.Remaining *= HungerSleepReliefFactor; // 분모(Duration) 유지 — 게이지 비율로 완화 표시
        }

        /// <summary>[63-B] 식사 — Hunger 제거 + 마지막 식사 시각(허기 기준) 갱신.</summary>
        public static void CureMeal()
        {
            RemoveEffect(StatusEffectId.Hunger);
            MarkMealTime();
        }

        /// <summary>[63-B] 실내/모닥불 — Hypothermia 제거.</summary>
        public static void CureWarm() => RemoveEffect(StatusEffectId.Hypothermia);

        /// <summary>[63-B] 진정제 — Terror 제거.</summary>
        public static void CureCalm() => RemoveEffect(StatusEffectId.Terror);

        /// <summary>[63-B] 물 접촉(수영) — Burn 즉시 제거.</summary>
        public static void Extinguish() => RemoveEffect(StatusEffectId.Burn);

        // ── [63-B] 행동 잠금/배율 조회 API (PlayerMovement/PlayerCombat/PlayerHealth 등이 읽음, §1) ──

        /// <summary>[63-B] 대쉬 잠금 — Exhaustion || Fracture.</summary>
        public static bool BlocksDash() => HasEffect(StatusEffectId.Exhaustion) || HasEffect(StatusEffectId.Fracture);

        /// <summary>[63-B] 구르기 잠금 — Fracture.</summary>
        public static bool BlocksRoll() => HasEffect(StatusEffectId.Fracture);

        /// <summary>
        /// [63-B] 힐 완전 정지 여부 — Infection/Hunger(§2 "힐 정지"). Poisoning은 정지 아님
        /// (회복효율 ×0.5 — 정지 아님) → Heal 절반 경로는 ApplyHealMultiplier()/HealMultiplier().
        /// </summary>
        public static bool BlocksHeal() => HasEffect(StatusEffectId.Infection) || HasEffect(StatusEffectId.Hunger);

        /// <summary>[63-B] 힐 배율 — Poisoning 0.5, Infection 0(정지), Hunger 0(자연회복만 정지). 다중 활성 시 최소값.</summary>
        public static float HealMultiplier()
        {
            float m = 1f;
            if (HasEffect(StatusEffectId.Poisoning)) m = Mathf.Min(m, PoisonHealMultiplier);
            if (HasEffect(StatusEffectId.Infection)) m = Mathf.Min(m, 0f);
            if (HasEffect(StatusEffectId.Hunger)) m = Mathf.Min(m, 0f);
            return m;
        }

        /// <summary>[63-B] Heal 절반 경로 — §1 규약: 회복 처리부가 이 한 줄로 배율 적용(§4 훅 소비 예정).</summary>
        public static float ApplyHealMultiplier(float healAmount) => healAmount * HealMultiplier();

        /// <summary>[63-B] 스태미너 회복 배율 — Exhaustion 0.5, Daze 0.7, Hypothermia 0.6. 다중 최소값(§1).</summary>
        public static float StaminaRegenMultiplier()
        {
            float m = 1f;
            if (HasEffect(StatusEffectId.Exhaustion)) m = Mathf.Min(m, ExhaustionStaminaRegenMultiplier);
            if (HasEffect(StatusEffectId.Daze)) m = Mathf.Min(m, DazeStaminaRegenMultiplier);
            if (HasEffect(StatusEffectId.Hypothermia)) m = Mathf.Min(m, HypothermiaStaminaRegenMultiplier);
            return m;
        }

        /// <summary>[63-B] 콤보 누락 확률 — Terror 0.3, Daze 0.15, Drunkenness 0.1 합산, clamp 0.8(§1).</summary>
        public static float ComboMissChance()
        {
            float c = 0f;
            if (HasEffect(StatusEffectId.Terror)) c += TerrorMissChance;
            if (HasEffect(StatusEffectId.Daze)) c += DazeMissChance;
            if (HasEffect(StatusEffectId.Drunkenness)) c += DrunkennessMissChance;
            return Mathf.Min(c, ComboMissClamp);
        }

        /// <summary>[63-B] 스태미너 최대 배율 — Hunger 시 0.7(최대 −30%, §2). PlayerMovement.MaxStamina 소비(§4 훅#3 예정).</summary>
        public static float MaxStaminaMultiplier() => HasEffect(StatusEffectId.Hunger) ? HungerStaminaMaxMultiplier : 1f;

        /// <summary>[63-B] 젖음 플래그 — 수영 훅(#3⑤)이 설정, TimeManager 저체온 재발 판정(#6②)이 소비. 세션 휘발성.</summary>
        public static bool IsWet { get; private set; }

        /// <summary>[63-B] 젖음 설정 — 물 접촉 시 true, 건조/수면 후 false.</summary>
        public static void SetWet(bool wet) => IsWet = wet;

        // ── [63-B] 14종 정의 상수 (§2 표 — 기본값, Play 체감 후 트림 대상: 상수만 수정) ──

        // 지속(초)
        private const float BleedingDuration = 10f;
        private const float PoisoningDuration = 12f;
        private const float SlownessDuration = 5f;
        private const float ExhaustionDuration = 20f;
        private const float FractureDuration = 90f;
        private const float DazeDuration = 8f;
        private const float HungerDuration = 600f;       // 갱신형 — 24h 미식 동안 remaining 600 유지
        private const float BurnDuration = 4f;
        private const float HypothermiaDuration = 15f;
        private const float TerrorDuration = 6f;
        private const float InfectionDuration = 30f;
        private const float WeakenDuration = 8f;
        private const float DrunkennessDuration = 25f;
        private const float BrittleDefenseDuration = 10f;

        // DOT(HP/s) — 1초 주기 강틱(§1)
        private const float BleedingDps = 2f;
        private const float PoisoningDps = 1.5f;
        private const float BurnDps = 5f;
        private const float InfectionDps = 1f;

        // 스탯 가감 상수 — BuffManager.Apply/ReverseBuff 동일 산식(적용 시 1회 고정 가감 + 해제 시 정확히 역연산).
        // [63-B] 산식 규약(§1): "적용 시점 현재값에 비율을 직접 곱하는 고정치" 금지 — 반드시
        // "기본값 기준 비율 → 상수" 로 환산한 값을 적용/역적용한다(해제 역산 대칭 보장).
        private const float SlownessSpeedPenalty = 0.5f;   // 이속 −(기존 몬스터 값 −0.5 유지 — BuffManager AddBuff("Slowness",0.5,5) 선례)
        private const float FractureSpeedPenalty = 2f;     // 이속 −20%: 기본 이동속도 10 기준 ×0.2(계획서 §1 고정 상수) — 실측: PlayerStats._moveSpeedBase 코드 기본값 5f → Play 체감 후 트림 대상
        private const float HypothermiaSpeedPenalty = 1.5f;
        private const float WeakenAttackPenalty = 3f;      // 공격력 −30%: 기본 공격력 10f 실측(PlayerStats._attackDamageBase=10f) ×0.3 — 기본값 기준 비율 산식(§1 주석 필수 준수)
        private const float DrunkennessAttackBonus = 1f;   // 공격 +10%: 기본 공격력 10f ×0.1
        private const float DrunkennessSpeedPenalty = 1f;
        private const float BrittleDefenseDefensePenalty = 3f; // 방어력 −30%: DefenseBase 코드 기본값 0(비율식 가산치)이라 0×30%는 무효 → 실질 기본 방어(PlayerStats.FinalDefense Lv1≈10.5 = DefenseBase 0 + Lv 0.5 + VIT 5×2) 기준 30% 근사 상수 — Play 체감 후 트림 대상

        // 조회 계수 (§1 규약 수치)
        private const float PoisonHealMultiplier = 0.5f;
        private const float ExhaustionStaminaRegenMultiplier = 0.5f;
        private const float DazeStaminaRegenMultiplier = 0.7f;
        private const float HypothermiaStaminaRegenMultiplier = 0.6f;
        private const float TerrorMissChance = 0.3f;
        private const float DazeMissChance = 0.15f;
        private const float DrunkennessMissChance = 0.1f;
        private const float ComboMissClamp = 0.8f;
        private const float HungerStaminaMaxMultiplier = 0.7f; // 스태 최대 −30%
        private const float HungerGameSeconds = 86400f;        // 24h(게임초) — TimeManager.GameTime 스케일
        private const float HungerSleepReliefFactor = 0.6f;    // 수면 시 Hunger remaining 60%(§1 "remaining 60%로 절반")
        private const float InfectionDelaySeconds = 30f;       // 출혈/골절 미치료 경과 임계

        /// <summary>[63-B] DOT 자기재귀 가드 핀 — §4 훅#1(PlayerHealth)이 이 weaponType 문자열로 DOT을 식별해 피격 발동 판정을 스킵.</summary>
        private const string DotWeaponType = "status";

        // ── [63-B] 정적 상태 (§5-1 — EditMode 제약: 인스턴스 무관 정적 상태) ──

        private sealed class EffectState
        {
            public float Remaining;  // 남은 초
            public float Duration;   // 현재 구간 전체 지속(UI 분모)
            public float DotTimer;   // DOT 1초 주기 누적기
        }

        private static readonly Dictionary<StatusEffectId, EffectState> _effects = new Dictionary<StatusEffectId, EffectState>();
        private static readonly List<StatusEffectId> _activeOrder = new List<StatusEffectId>(); // 추가 순 보존
        private static readonly List<StatusEffectId> _expiredScratch = new List<StatusEffectId>(); // 프레임 내 만료 스캐치
        private static float _infectionTimer;        // 감염 연쇄 — "치료 안 된 채 경과" 독립 추적(§1)
        private static float _prevGameTime = float.NaN;   // 허기 적산용 직전 GameTime(자정 랩 정규화)
        private static float _gameSecondsSinceMeal;  // 마지막 식사 이후 게임초 적산 — 세이브/로드 미연동(휘발성 1차)

        // ── [63-B] 자가부트 (BuffManager 선례 — 멱등 Ensure + DDOL) ──

        /// <summary>[63-B] 런타임 자가부트 진입점 — Play 세션당 1회. 도메인 리로드 Off 시 잔존 정적을 먼저 청소.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            ResetStateForSession(); // 세션 경계 청소 — 런타임 전용 경로(게임 코드에서 호출 금지)
            Ensure();
        }

        /// <summary>[63-B] 멱등 Ensure — GameObject "PlayerStatusEffectSystem" 생성 + DDOL. 씬 전환에도 유지(§1 접근성).</summary>
        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("PlayerStatusEffectSystem");
            Instance = go.AddComponent<PlayerStatusEffectSystem>(); // Awake가 Instance 확정 + DDOL(Play 전용)
            if (Application.isPlaying) DontDestroyOnLoad(go);       // EditMode 안전 가드(TimeManager 선례)
        }

        private void Awake()
        {
            // === 단순화: 첫 번째가 주인 (BuffManager/PlayerHealth 선례) ===
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // [63-B] DDOL은 Play 전용 — EditMode AddComponent 경로 예외 방지(TimeManager [QA 수리] 선례)
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// [63-B] 유일 시간 주입부 — EditMode(Time.deltaTime=0) 제약과 분리된 순수 훅으로 위임.
        /// Update에서만 호출됨(§5-1 핀 — 테스트는 정적 TickCore 경유).
        /// </summary>
        private void Update() => Tick(Time.deltaTime);

        // ── [63-B] 순수 시간 훅 (§5-1 — internal, dt 주입, 인스턴스 무관 정적 상태) ──

        /// <summary>[63-B] 인스턴스 경로(Update 전용) — 상태는 전부 static이므로 인스턴스는 드라이버 역할만.</summary>
        internal void Tick(float dt) => TickCore(dt);

        /// <summary>[63-B] 정적 순수 훅 — 만료 → DOT 강틱 → 감염 연쇄 → 허기 판정 순. dt≤0 무시(일시정지/EditMode 안전).</summary>
        internal static void TickCore(float dt)
        {
            if (dt <= 0f) return;
            TickDurations(dt);
            TickDots(dt);
            TickInfection(dt);
            TickHunger(NowGameTime());
        }

        private static void TickDurations(float dt)
        {
            // 역방향 순회 — 만료 판정은 스캐치에 모아 루프 밖에서 일괄 해제(순회 중 변형 방지)
            for (int i = _activeOrder.Count - 1; i >= 0; i--)
            {
                if (!_effects.TryGetValue(_activeOrder[i], out var state)) continue;
                state.Remaining -= dt;
                if (state.Remaining <= 0f) _expiredScratch.Add(_activeOrder[i]);
            }
            for (int i = 0; i < _expiredScratch.Count; i++)
                Deactivate(_expiredScratch[i]); // 만료 해제 — 역가감 + OnChanged(§1 "만료 시 발화")
            _expiredScratch.Clear();
        }

        private static void TickDots(float dt)
        {
            TickDotOne(StatusEffectId.Bleeding, BleedingDps, dt);
            TickDotOne(StatusEffectId.Poisoning, PoisoningDps, dt);
            TickDotOne(StatusEffectId.Burn, BurnDps, dt);
            TickDotOne(StatusEffectId.Infection, InfectionDps, dt);
        }

        private static void TickDotOne(StatusEffectId id, float dps, float dt)
        {
            if (!_effects.TryGetValue(id, out var state)) return; // DOT 없는 종류 — 무시
            state.DotTimer += dt;
            while (state.DotTimer >= 1f) // [63-B] 1초 주기 강틱 — HP/s 체감 유지(§1 DOT 틱 규약)
            {
                state.DotTimer -= 1f;
                DealDot(id, dps);
            }
        }

        private static void DealDot(StatusEffectId id, float dps)
        {
            var health = PlayerHealth.Instance;
            if (health == null) return; // [63-B] 더블 가드 ① — 부팅 전/EditMode 안전(인스턴스 없음)

            // [63-B] 더블 가드 ② — weaponType "status"(DotWeaponType):
            // §4 훅#1이 이 문자열로 DOT을 식별해 피격 발동 판정(Bleeding/Fracture/BrittleDefense 재진입)을
            // 스킵한다. hitDirection=zero → 3인자 오버로드의 넉백 분기 미진입(순수 대미지).
            health.TakeDamage(dps, Vector3.zero, DotWeaponType);
        }

        private static void TickInfection(float dt)
        {
            if (HasEffect(StatusEffectId.Bleeding) || HasEffect(StatusEffectId.Fracture))
            {
                // [63-B] "치료 안 된 채 경과" 추적 — 효과 자체 remaining과 무관한 독립 타이머(§1 _infectionTimer)
                _infectionTimer += dt;
                if (_infectionTimer >= InfectionDelaySeconds)
                {
                    _infectionTimer = 0f;
                    if (!HasEffect(StatusEffectId.Infection))
                        AddEffect(StatusEffectId.Infection, InfectionDuration); // 연쇄 발동(§1)
                }
            }
            else
            {
                _infectionTimer = 0f; // 출혈/골절 모두 해제(치료 포함) → 타이머 리셋(§1 "출혈 치료 시 타이머 리셋")
            }
        }

        private static void TickHunger(float gameNow)
        {
            if (float.IsNaN(_prevGameTime))
            {
                _prevGameTime = gameNow; // 첫 판정 — 기준점 시딩(세션 시작 = 식사 후 상태 가정, 휘발성 1차)
                return;
            }
            float step = gameNow - _prevGameTime;
            _prevGameTime = gameNow;
            if (step < 0f) step += HungerGameSeconds; // GameTime [0,86400) 자정 랩 정규화
            _gameSecondsSinceMeal += Mathf.Max(0f, step);
            if (_gameSecondsSinceMeal >= HungerGameSeconds)
                AddEffect(StatusEffectId.Hunger, HungerDuration); // 갱신형 — 미식 지속 중 remaining 600 리셋(§1/§2)
        }

        /// <summary>[63-B] 마지막 식사 시각 갱신 — CureMeal 경유 + ConsumableSystem 훅(#7⑥)이 직접 호출 가능.</summary>
        public static void MarkMealTime() => _gameSecondsSinceMeal = 0f;

        private static float NowGameTime()
        {
            var time = TimeManager.Instance; // Systems 동일 어셈블리 — 직접 참조(Core 역참조 회피 계약)
            return time != null ? time.GameTime : 0f;
        }

        // ── [63-B] 내부 해제/스탯 산식 ──

        private static void Deactivate(StatusEffectId id)
        {
            if (!_effects.Remove(id)) return; // 비활성 해제 무시(멱등)
            _activeOrder.Remove(id);          // List.Remove는 순서 보존 — 추가 순 유지
            ReverseStats(id);                 // 해제 시 정확히 역가감(BuffManager.ReverseBuff 동일 산식)
            NotifyChanged();
        }

        private static void ApplyStats(StatusEffectId id)
        {
            var stats = PlayerStats.Instance;
            if (stats == null) return; // [63-B] EditMode/부팅 전 — 효과 목록은 동작, 스탯만 스킵
            switch (id)
            {
                case StatusEffectId.Slowness: stats.MoveSpeedBase -= SlownessSpeedPenalty; break;
                case StatusEffectId.Fracture: stats.MoveSpeedBase -= FractureSpeedPenalty; break;
                case StatusEffectId.Hypothermia: stats.MoveSpeedBase -= HypothermiaSpeedPenalty; break;
                case StatusEffectId.Weaken: stats.AttackDamageBase -= WeakenAttackPenalty; break;
                case StatusEffectId.Drunkenness:
                    stats.AttackDamageBase += DrunkennessAttackBonus;
                    stats.MoveSpeedBase -= DrunkennessSpeedPenalty;
                    break;
                case StatusEffectId.BrittleDefense: stats.DefenseBase -= BrittleDefenseDefensePenalty; break;
            }
        }

        private static void ReverseStats(StatusEffectId id)
        {
            var stats = PlayerStats.Instance;
            if (stats == null) return;
            switch (id)
            {
                case StatusEffectId.Slowness: stats.MoveSpeedBase += SlownessSpeedPenalty; break;
                case StatusEffectId.Fracture: stats.MoveSpeedBase += FractureSpeedPenalty; break;
                case StatusEffectId.Hypothermia: stats.MoveSpeedBase += HypothermiaSpeedPenalty; break;
                case StatusEffectId.Weaken: stats.AttackDamageBase += WeakenAttackPenalty; break;
                case StatusEffectId.Drunkenness:
                    stats.AttackDamageBase -= DrunkennessAttackBonus;
                    stats.MoveSpeedBase += DrunkennessSpeedPenalty;
                    break;
                case StatusEffectId.BrittleDefense: stats.DefenseBase += BrittleDefenseDefensePenalty; break;
            }
        }

        private static float DefaultDurationOf(StatusEffectId id) => id switch
        {
            StatusEffectId.Bleeding => BleedingDuration,
            StatusEffectId.Poisoning => PoisoningDuration,
            StatusEffectId.Slowness => SlownessDuration,
            StatusEffectId.Exhaustion => ExhaustionDuration,
            StatusEffectId.Fracture => FractureDuration,
            StatusEffectId.Daze => DazeDuration,
            StatusEffectId.Hunger => HungerDuration,
            StatusEffectId.Burn => BurnDuration,
            StatusEffectId.Hypothermia => HypothermiaDuration,
            StatusEffectId.Terror => TerrorDuration,
            StatusEffectId.Infection => InfectionDuration,
            StatusEffectId.Weaken => WeakenDuration,
            StatusEffectId.Drunkenness => DrunkennessDuration,
            StatusEffectId.BrittleDefense => BrittleDefenseDuration,
            _ => 5f,
        };

        private static void ResetStateForSession()
        {
            _effects.Clear();
            _activeOrder.Clear();
            _expiredScratch.Clear();
            _infectionTimer = 0f;
            _prevGameTime = float.NaN;
            _gameSecondsSinceMeal = 0f;
        }

        /// <summary>[63-B] 테스트 전용 리셋 — EditMode 상태 격리(§5-1 주입 훅). 런타임 경로에서 호출 금지.</summary>
        internal static void ResetForTest()
        {
            ResetStateForSession();
            OnChanged = null; // 구독 잔존 방지 — 테스트 간 이벤트 카운트 오염 차단
        }

        /// <summary>[63-B] UI 구독자 예외가 만료/DOT 흐름을 끊지 않도록 흡수(PlayerHealth 이벤트 흡수 선례).</summary>
        private static void NotifyChanged()
        {
            try { OnChanged?.Invoke(); }
            catch (Exception ex) { Debug.LogWarning($"[PlayerStatusEffectSystem] OnChanged 구독자 예외 흡수: {ex.Message}"); }
        }
    }
}
