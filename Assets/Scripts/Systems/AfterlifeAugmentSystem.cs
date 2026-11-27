using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [A3] 이계 3택1 증강 — 런 한정 버프 (AFTERLIFE_SURVIVAL_PLAN §A3-2, 귀환 시 소멸).
    /// - 5/10분 마크 도달 시 풀(6종)에서 결정론 3택 제시(Fisher–Yates, 시드 = 런 시드+마크)
    /// - 선택까지 아레나 정지(VS식) — 적/오브/타이머 전부 IsPending 게이트 (드라이버/적/오브 3곳)
    /// - 적용 훅 (기존 인프라 연결만 — 신규 파이프라인 없음):
    ///     공속   = PlayerCombat 쿨다운 게이트 나눗셈 (CanAttack/RemainingCooldown 2곳 — AttackSpeedMult)
    ///     이속   = PlayerMovement.SpeedModifier (BiomeEffectController 동일 훅 — 진입값 캡처→곱→복원)
    ///     최대HP = PlayerHealth.SetMaxHP (진입값 캡처→곱→복원, 추가분 즉시 Heal)
    ///     흡수   = AfterlifeOrb.MagnetRadius
    ///     XP     = 흡수 시 RoundXPGain 배율
    ///     재생   = 드라이버 Tick 직접 Heal (NaturalRegenSystem 10초 그레이스 무시 — 이계 전용 채널)
    /// - 귀환/퍼머데스/ResetAll → EndRun: 캡처값 복원 + 배율 전부 리셋 — 월드 오염 없음
    /// </summary>
    public static class AfterlifeAugmentSystem
    {
        public enum Kind { AttackSpeed, MoveSpeed, MaxHP, MagnetRadius, XPGain, Regen }

        public struct AugmentDef
        {
            public string Id;
            public string Name;
            public string Desc;
            public Kind Kind;
            public float Value;
        }

        // ===== 상수 =====
        public const int Mark1 = 300;             // 5분 — 첫 증강 (Easy 5분 목표엔 미제시)
        public const int Mark2 = 600;             // 10분 — 두 번째 (Hard 전용)
        public const float PendingTimeoutSec = 20f; // 대기 폴백 — A4 UI 이전 초과 시 결정론 index 0 자동 선택

        // ===== 풀 (6종 — 계획서 4종 + XP/재생) =====
        private static readonly AugmentDef[] _pool =
        {
            new AugmentDef { Id = "atk_speed",  Name = "공속 증폭",     Desc = "공격 속도 +15%",                        Kind = Kind.AttackSpeed,   Value = 0.15f },
            new AugmentDef { Id = "move_speed", Name = "질주",         Desc = "이동 속도 +10%",                        Kind = Kind.MoveSpeed,     Value = 0.10f },
            new AugmentDef { Id = "max_hp",     Name = "생명 강화",     Desc = "최대 HP +20% (추가분 즉시 회복)",        Kind = Kind.MaxHP,         Value = 0.20f },
            new AugmentDef { Id = "magnet",     Name = "흡수의 인장",   Desc = "오브 흡수 반경 +50%",                   Kind = Kind.MagnetRadius,  Value = 0.50f },
            new AugmentDef { Id = "xp_gain",    Name = "영혼 갈취",     Desc = "경험치 획득 +25%",                      Kind = Kind.XPGain,        Value = 0.25f },
            new AugmentDef { Id = "regen",      Name = "빠른 재생",     Desc = "초당 HP +2 재생",                       Kind = Kind.Regen,         Value = 2f },
        };
        public static IReadOnlyList<AugmentDef> Pool => _pool;

        // ===== 배율 상태 (public 게이트가 직접 읽음) =====
        /// <summary>공속 배율 — PlayerCombat 쿨다운을 이 값으로 나눈다(기본 1 — 월드 무영향).</summary>
        public static float AttackSpeedMult { get; private set; } = 1f;
        /// <summary>XP 획득 배율 — 오브 흡수 시 적용.</summary>
        public static float XPGainMult { get; private set; } = 1f;
        /// <summary>초당 재생량 (flat 합산).</summary>
        public static float RegenPerSec { get; private set; }

        // ===== 선택 대기 =====
        public static bool IsPending { get; private set; }
        private static readonly List<AugmentDef> _choices = new List<AugmentDef>();
        public static IReadOnlyList<AugmentDef> PendingChoices => _choices;

        public static event System.Action<List<AugmentDef>> OnOfferCreated;   // (3택)
        public static event System.Action<AugmentDef> OnAugmentApplied;       // (선택된 증강)
        public static event System.Action OnOfferResolved;                    // 선택/만료 — A4 UI 갱신용

        private static readonly HashSet<int> _offeredMarks = new HashSet<int>();
        private static float _pendingStartedAtUnscaled;
        private static float _moveBaseAtEntry = 1f;
        private static float _hpBaseAtEntry = 100f;
        private static bool _moveCaptured;
        private static bool _hpCaptured;
        private static float _moveMult = 1f;
        private static float _hpMult = 1f;
        private static float _magnetMult = 1f;
        public static int DeathRing { get; private set; }

        // HUD(A4) 판독용 곱 현황
        public static float MoveSpeedMult => _moveMult;
        public static float MaxHPMult => _hpMult;
        public static float MagnetMult => _magnetMult;

        // ===== 순수 수학 (EditMode 검증 대상) =====

        /// <summary>마크 오퍼 게이트 — 마크가 목표 시간 미만 + 도달. (Easy 5분 = 마크 없음)</summary>
        public static bool ShouldOffer(int mark, float elapsed, float targetSec)
            => mark < targetSec && elapsed >= mark;

        /// <summary>결정론 3택 생성 — Fisher–Yates(시드) 후 선두 3개. 동일 시드 = 동일 오퍼.</summary>
        public static List<AugmentDef> GenerateOffer(int seed)
        {
            var rng = new System.Random(seed);
            var idx = new List<int>();
            for (int i = 0; i < _pool.Length; i++) idx.Add(i);
            for (int i = idx.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (idx[i], idx[j]) = (idx[j], idx[i]);
            }
            var offer = new List<AugmentDef>();
            for (int i = 0; i < 3 && i < idx.Count; i++) offer.Add(_pool[idx[i]]);
            return offer;
        }

        /// <summary>이계 티어 배율 — 사망 링(Ring1=외곽 쉬움 0.85 → Ring4=중앙 어려움 1.30). 승률 우선 — 완만.</summary>
        public static float GetTierFactor(int ring)
        {
            switch (Mathf.Clamp(ring, 1, 4))
            {
                case 1: return 0.85f;
                case 3: return 1.15f;
                case 4: return 1.30f;
                default: return 1.0f;
            }
        }

        /// <summary>XP 오브 흡수 배율 적용 (배율 1.0 = 원값). 순수 — 오브 집계가 호출.</summary>
        public static int RoundXPGain(int baseValue, float mult) => Mathf.RoundToInt(baseValue * mult);
        public static int RoundXPGain(int baseValue) => RoundXPGain(baseValue, XPGainMult);

        // ===== 런 라이프사이클 =====

        /// <summary>런 시작 — 배율 리셋 + 플레이어 스탯 진입값 캡처(복원 기준).</summary>
        public static void BeginRun(int ring, int territoryIndex)
        {
            DeathRing = ring;
            AttackSpeedMult = 1f;
            XPGainMult = 1f;
            RegenPerSec = 0f;
            _moveMult = 1f;
            _hpMult = 1f;
            _magnetMult = 1f;
            _offeredMarks.Clear();
            _choices.Clear();
            IsPending = false;

            var health = PlayerHealth.Instance;
            if (health != null)
            {
                _hpBaseAtEntry = health.MaxHP;
                _hpCaptured = true;
            }
            var movement = Object.FindAnyObjectByType<PlayerMovement>();
            if (movement != null)
            {
                _moveBaseAtEntry = movement.SpeedModifier;
                _moveCaptured = true;
            }

            AfterlifeOrb.MagnetRadius = AfterlifeOrb.BaseAbsorbRadius;
        }

        /// <summary>
        /// 드라이버 틱 — 재생/만료 폴백/마크 오퍼. IsPending 중에는 생존 드라이버가 호출하지 않는다
        /// (아레나 전체 정지 — 타이머도 동결).
        /// </summary>
        public static void Tick(float elapsed, float targetSec, int seedBase, float unscaledDt)
        {
            // 재생 — 증강 전용 채널 (자연재생 그레이스 무관, 항상 작동)
            if (RegenPerSec > 0f)
            {
                var health = PlayerHealth.Instance;
                if (health != null && !health.IsDead)
                    health.Heal(RegenPerSec * unscaledDt);
            }

            // 대기 만료 폴백 — A4 UI 이전 안전장치(결정론 index 0)
            if (IsPending && Time.unscaledTime - _pendingStartedAtUnscaled > PendingTimeoutSec)
            {
                Debug.LogWarning("[AfterlifeAugment] ⏳ 20초 미선택 — 첫 번째 증강 자동 적용(폴백)");
                Select(0);
                return;
            }

            if (IsPending) return;
            TryOffer(Mark1, elapsed, targetSec, seedBase);
            TryOffer(Mark2, elapsed, targetSec, seedBase);
        }

        private static void TryOffer(int mark, float elapsed, float targetSec, int seedBase)
        {
            if (IsPending) return; // 대기 중 신규 오퍼 금지 — 선택 완료 후 다음 마크
            if (_offeredMarks.Contains(mark)) return;
            if (!ShouldOffer(mark, elapsed, targetSec)) return;
            _offeredMarks.Add(mark);

            var offer = GenerateOffer(seedBase + mark * 104729);
            _choices.Clear();
            _choices.AddRange(offer);
            IsPending = true;
            _pendingStartedAtUnscaled = Time.unscaledTime;

            Debug.Log($"[AfterlifeAugment] ✨ 증강 3택 제시 — {_choices[0].Name} / {_choices[1].Name} / {_choices[2].Name}");
            CombatLog.AddEntry("✨ 이계의 힘이 흘러든다 — 증강을 선택하라", LogType.Warning);
            OnOfferCreated?.Invoke(_choices);
        }

        /// <summary>증강 선택 — 배율 갱신 + 훅 즉시 적용. A4 UI와(미래) 키 입력 경유.</summary>
        public static void Select(int index)
        {
            if (!IsPending) return;
            if (index < 0 || index >= _choices.Count) return;

            var def = _choices[index];
            switch (def.Kind)
            {
                case Kind.AttackSpeed:
                    AttackSpeedMult *= 1f + def.Value;
                    break;
                case Kind.MoveSpeed:
                    _moveMult *= 1f + def.Value;
                    ApplyMoveSpeed();
                    break;
                case Kind.MaxHP:
                    _hpMult *= 1f + def.Value;
                    ApplyMaxHP();
                    break;
                case Kind.MagnetRadius:
                    _magnetMult *= 1f + def.Value;
                    AfterlifeOrb.MagnetRadius = AfterlifeOrb.BaseAbsorbRadius * _magnetMult;
                    break;
                case Kind.XPGain:
                    XPGainMult *= 1f + def.Value;
                    break;
                case Kind.Regen:
                    RegenPerSec += def.Value;
                    break;
            }

            IsPending = false;
            _choices.Clear();
            Debug.Log($"[AfterlifeAugment] ⬆️ 증강 적용 — {def.Name} ({def.Desc})");
            OnAugmentApplied?.Invoke(def);
            OnOfferResolved?.Invoke();
        }

        private static void ApplyMoveSpeed()
        {
            var movement = Object.FindAnyObjectByType<PlayerMovement>();
            if (movement != null) movement.SpeedModifier = _moveBaseAtEntry * _moveMult;
        }

        private static void ApplyMaxHP()
        {
            var health = PlayerHealth.Instance;
            if (health == null) return;
            float newMax = _hpBaseAtEntry * _hpMult;
            float oldMax = health.MaxHP;
            health.SetMaxHP(newMax);
            if (newMax > oldMax) health.Heal(newMax - oldMax); // 추가분 즉시 회복
        }

        /// <summary>런 종료 — 캡처값 복원(최대HP/이속) + 배율 전부 리셋. 귀환/퍼머데스/ResetAll 공용.</summary>
        public static void EndRun()
        {
            if (_hpCaptured)
            {
                var health = PlayerHealth.Instance;
                if (health != null) health.SetMaxHP(_hpBaseAtEntry);
            }
            if (_moveCaptured)
            {
                var movement = Object.FindAnyObjectByType<PlayerMovement>();
                if (movement != null) movement.SpeedModifier = _moveBaseAtEntry;
            }
            AfterlifeOrb.MagnetRadius = AfterlifeOrb.BaseAbsorbRadius;

            AttackSpeedMult = 1f;
            XPGainMult = 1f;
            RegenPerSec = 0f;
            _moveMult = 1f;
            _hpMult = 1f;
            _magnetMult = 1f;
            _offeredMarks.Clear();
            _choices.Clear();
            IsPending = false;
            _hpCaptured = false;
            _moveCaptured = false;
        }
    }
}
