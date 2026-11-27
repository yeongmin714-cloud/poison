using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [156차 §3] 이계 3택1 카드 제시기 — 런 한정 (귀환 시 소멸, 기존 증강과 동일 계약).
    /// - 마크 300/600 (증강 Mark1/Mark2와 동일) — 단, 증강 오퍼 미해결 중엔 카드 오퍼 보류
    ///   (UI 단일 표시 보장 — AfterlifeAugmentSystem.OnOfferResolved 후 발화).
    /// - 가중치: 기본 C70/R22/E6/L2, Epic/Legendary 링 배율 {1:0.5, 2:1, 3:2, 4:3}.
    ///   가중 추출은 PickWeightedIndex(System.Random, int[]) 순수 함수.
    /// - 자격 필터: 무기 슬롯 4 만석→무기 배제 · 강화는 보유 무기+Lv 미만만 ·  [156차 P3 — Lv 표기 HTML 엔티티 제거]
    ///   비스택 카드 보유 중→배제 · 특수 1회성 보유 중→배제. 중복 제시 금지.
    /// - 20초 폴백 = 결정론 index 0 자동 선택 (증강 동일).
    /// - BeginRun/EndRun — 소유/보류/배율 전부 소멸 (증강 EndRun 좌표 3곳 동일 배선 — Phase 5).
    /// - 모든 Apply 경로 null-guard (PlayerHealth/PlayerCombat Instance null 시 no-op — EditMode 안전).
    /// </summary>
    public static class AfterlifeCardSystem
    {
        // [156차 P5] Core→Systems 역참조 회피 — PlayerHealth 피해 게이트 훅 주입(정적 생성자, 도메인 리로드 시 재실행)
        static AfterlifeCardSystem()
        {
            PlayerHealth.IncomingDamageModifier = f => f * IncomingDamageFactor;
            PlayerHealth.TryBlockOnce = TryConsumeBoneShield;
        }

        // ===== 상수 =====
        public const int Mark1 = 300;               // 5분 — 첫 카드 (증강 Mark1과 동일 마크)
        public const int Mark2 = 600;               // 10분 — 두 번째
        public const float PendingTimeoutSec = 20f; // 대기 폴백 — 초과 시 결정론 index 0 자동 선택
        public const float DefaultWaveIntervalSec = 120f; // p_time_sand 미보유 기본 (SurvivalSystem 교체점)
        public const float DefaultSurrenderHarvestRatio = 0.5f; // sp_pledge_receipt 미보유 기본
        public const int MaxWeaponSlots = 4;

        // ===== 카탈로그 참조 =====
        public static IReadOnlyList<CardDef> Pool => CardCatalog.Cards;

        // ===== 소유 상태 (런 한정) =====
        private static readonly Dictionary<string, int> _ownedCounts = new Dictionary<string, int>();
        private static readonly List<CardDef> _ownedSkills = new List<CardDef>();
        private static readonly Dictionary<string, int> _weaponLevels = new Dictionary<string, int>();
        public static IReadOnlyDictionary<string, int> OwnedCounts => _ownedCounts;
        public static IReadOnlyList<CardDef> OwnedSkills => _ownedSkills;

        /// <summary>보유 기본 무기 개수 (무기 슬롯 — 최대 4).</summary>
        public static int WeaponCount => _weaponLevels.Count;

        /// <summary>기본 무기 현재 레벨 (미보유 0, 획득 1, 강화 2/3).</summary>
        public static int WeaponLevel(string baseWeaponId)
            => _weaponLevels.TryGetValue(baseWeaponId, out var lv) ? lv : 0;

        // ===== 적용 상태 (public 게이트 — Phase 4/5 훅이 직접 읽음) =====
        // [156차 P4] 데미지 배율 분리 — 무기(활성 무기) × 패시브(스택). 최종은 프로퍼티 통합.
        private static float _weaponDamageMult = 1f;
        private static float _passiveDamageMult = 1f;
        /// <summary>최종 데미지 배율 — 활성 무기 배율 × 패시브 스택 배율 (런 외부 1f 무영향).</summary>
        public static float DamageMult => _weaponDamageMult * _passiveDamageMult;
        /// <summary>활성 무기 쿨다운 계수 (PlayerCombat.CanAttack 분모 추가 — 런 외부 1f 무영향).</summary>
        public static float CooldownFactor => _cooldownFactor;
        /// <summary>활성(마지막 획득) 무기 카드 Id — 무기 슬롯 4칸 중 실전 투입 무기.</summary>
        public static string ActiveWeaponId => _activeWeaponId;
        /// <summary>받는 피해 감쇠 (p_steel_shield 스택 — ×0.92씩 곱).</summary>
        public static float IncomingDamageFactor { get; private set; } = 1f;
        /// <summary>적 이속 배율 (패시브 p_chain_seal 스택 — ×0.9씩 곱; 스킬 time_warp와 별도 게이트).</summary>
        public static float EnemyMoveFactor { get; private set; } = 1f;
        /// <summary>오브 가치 배율 (p_soul_jar — ×1.2).</summary>
        public static float OrbValueMult { get; private set; } = 1f;
        /// <summary>최대 HP 보너스 (p_tombstone — 스택당 +50 flat).</summary>
        public static float MaxHPBonus { get; private set; }
        /// <summary>웨이브 간격 (p_time_sand 보유 시 135, 기본 120).</summary>
        public static float WaveIntervalSeconds => _ownedCounts.ContainsKey("p_time_sand") ? 135f : DefaultWaveIntervalSec;
        /// <summary>구르기 거리 배율 (p_shadow_boots — ×1.2).</summary>
        public static float RollDistanceFactor { get; private set; } = 1f;
        /// <summary>PERFECT 히트스톱 추가 (p_perfect_mark — +0.02s).</summary>
        public static float ExtraPerfectHitStop { get; private set; }
        /// <summary>텔레그래프 보정 (p_afterlife_talisman — +0.1s).</summary>
        public static float TelegraphBonusSeconds { get; private set; }
        /// <summary>처치힐 (p_vampire_fang — 처치 시 +2).</summary>
        public static float KillHealPerStack { get; private set; }
        /// <summary>사신 즉사 활성 (p_death_scythe) + 기본/정예 가산 확률(%).</summary>
        public static bool DeathScytheActive { get; private set; }
        public static int DeathScytheBasePercent { get; private set; }
        public static int DeathScytheEliteBonusPercent { get; private set; }
        /// <summary>해골 반지 연쇄 (10연속 처치 → 다음 오브 ×2).</summary>
        public static bool SkullRingActive { get; private set; }
        public static int SkullRingChainCount => _skullRingChain;
        /// <summary>탈주 수확 비율 (sp_pledge_receipt 보유 시 1.0).</summary>
        public static float SurrenderHarvestRatio =>
            _ownedCounts.ContainsKey("sp_pledge_receipt") ? 1f : DefaultSurrenderHarvestRatio;
        /// <summary>사신 즉사 판정 진입 (Phase 4 — TakeDamage 훅): basePercent + (정예 ? eliteBonus : 0).</summary>
        public static int GetDeathScythePercent(bool isElite)
            => DeathScytheActive ? DeathScytheBasePercent + (isElite ? DeathScytheEliteBonusPercent : 0) : 0;

        // ===== 특수 카드 소비 상태 (1회성 — 소유는 유지, 소비만 차감) =====
        private static int _secondLifeCount;    // sp_second_life — 부활 가능 횟수
        private static int _wavePardonCount;    // sp_wave_pardon — 다음 웨이브 무시 횟수
        private static int _skullRingChain;     // p_skull_ring 연쇄 카운터

        /// <summary>부활 가능 여부 (sp_second_life) — ExecutePermadeath 진입 게이트 (Phase 5).</summary>
        public static bool SecondLifeAvailable => _secondLifeCount > 0;
        /// <summary>부활 소비 — HP 비율 0.5 반환 (null-guard는 호출자 몫). 소비 실패 시 false.</summary>
        public static float ConsumeSecondLife()
        {
            if (_secondLifeCount <= 0) return 0f;
            _secondLifeCount--;
            return 0.5f;
        }
        /// <summary>웨이브 사면 소비 — 다음 웨이브 무시 (Phase 5 CheckWave 게이트). 성공 시 true.</summary>
        public static bool ConsumeWavePardon()
        {
            if (_wavePardonCount <= 0) return false;
            _wavePardonCount--;
            return true;
        }
        /// <summary>웨이브 사면 잔여 (Phase 5 표시용).</summary>
        public static int WavePardonCount => _wavePardonCount;
        /// <summary>귀환 문 즉시 개방 (sp_realm_gate — OpenReturnDoor 훅). 성공 시 수확 감면 ×0.5 반환, 실패 시 0.</summary>
        public static float ConsumeRealmGate()
            => _ownedCounts.ContainsKey("sp_realm_gate") ? 0.5f : 0f;

        // ===== [156차 P5] 뼈 방패 충전/소비 — s_bone_shield가 충전, PlayerHealth 훅이 소비 =====
        private static bool _boneShieldCharged;
        public static bool BoneShieldCharged => _boneShieldCharged;
        public static void ChargeBoneShield() => _boneShieldCharged = true;
        public static bool TryConsumeBoneShield()
        {
            if (!_boneShieldCharged) return false;
            _boneShieldCharged = false;
            return true;
        }

        // ===== [156차 P5] 해골 반지 — 10연쇄 달성 시 다음 오브 ×2 예약(1회 소비) =====
        private static bool _skullRingBonusPending;
        public static void ArmSkullRingBonus() => _skullRingBonusPending = true;
        public static bool ConsumeSkullRingBonus()
        {
            if (!_skullRingBonusPending) return false;
            _skullRingBonusPending = false;
            return true;
        }

        // ===== 제시 대기 =====
        public static bool IsPending { get; private set; }
        private static readonly List<CardDef> _choices = new List<CardDef>();
        public static IReadOnlyList<CardDef> PendingChoices => _choices;

        public static event System.Action<List<CardDef>> OnOfferCreated;   // (3택)
        public static event System.Action<CardDef> OnCardApplied;          // (선택된 카드)
        public static event System.Action OnOfferResolved;                 // 선택/만료 — A4 UI 갱신용

        private static readonly HashSet<int> _offeredMarks = new HashSet<int>();
        private static float _pendingStartedAtUnscaled;
        private static bool _runActive;

        // ===== 순수 수학 (EditMode 검증 대상) =====

        /// <summary>가중 인덱스 추출 — 순수 함수. weights 합계 누적 구간에 rng 난수 매핑.</summary>
        public static int PickWeightedIndex(System.Random rng, int[] weights)
        {
            long total = 0;
            foreach (var w in weights) total += System.Math.Max(0, w);
            if (total <= 0) return 0;
            int roll = rng.Next((int)System.Math.Min(total, int.MaxValue));
            long acc = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                acc += System.Math.Max(0, weights[i]);
                if (roll < acc) return i;
            }
            return weights.Length - 1;
        }

        /// <summary>Epic/Legendary 링 배율 — {1:0.5, 2:1, 3:2, 4:3} (클램프).</summary>
        public static float GetRingFactor(int ring)
        {
            switch (Mathf.Clamp(ring, 1, 4))
            {
                case 1: return 0.5f;
                case 3: return 2f;
                case 4: return 3f;
                default: return 1f;
            }
        }

        /// <summary>마크 오퍼 게이트 — 마크가 목표 시간 미만 + 도달 (증강 ShouldOffer 동일 산식).</summary>
        public static bool ShouldOffer(int mark, float elapsed, float targetSec)
            => mark < targetSec && elapsed >= mark;

        /// <summary>자격 필터 — §3 규칙. 보유/슬롯/레벨/스택/특수 조건 판정 (결정론).</summary>
        public static bool IsEligible(CardDef card)
        {
            if (card == null) return false;
            int owned = _ownedCounts.TryGetValue(card.Id, out var c) ? c : 0;

            switch (card.Kind)
            {
                case CardKind.Weapon:
                    // 무기 슬롯 4 만석 → 무기 배제 · 이미 보유한 무기 배제
                    if (WeaponCount >= MaxWeaponSlots) return false;
                    if (owned > 0) return false;
                    return true;

                case CardKind.WeaponUpgrade:
                    // 보유 무기 + Lv<3만 — 직전 레벨 보유 필수 (Lv2는 Lv1, Lv3는 Lv2)
                    return WeaponLevel(card.WeaponId) == card.Level - 1;

                case CardKind.Skill:
                    // 보유 중 배제 (중복 제시 금지)
                    return owned == 0;

                case CardKind.Passive:
                    // 스택 카드는 MaxStacks까지, 비스택은 보유 중 배제
                    return owned < card.MaxStacks;

                case CardKind.Special:
                    // 특수 1회성 — 보유 중 배제 (소비 후에도 소유 유지)
                    return owned == 0;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 결정론 3택 생성 — 희귀도 가중 추출 → 해당 희귀도 자격 풀 Fisher–Yates(시드) → 선두 3.
        /// 자격 풀 전체를 셔플해 중복 제시 금지. 동일 시드/링 = 동일 오퍼.
        /// </summary>
        public static List<CardDef> GenerateOffer(int seed, int ring)
        {
            var rng = new System.Random(seed);
            float ringFactor = GetRingFactor(ring);
            int[] rarityWeights =
            {
                70,                                    // Common
                22,                                    // Rare
                Mathf.RoundToInt(6f * ringFactor),     // Epic
                Mathf.RoundToInt(2f * ringFactor),     // Legendary
            };

            // 자격 풀 전체 (중복 제시 금지 — 한 오퍼 내 동일 Id 금지)
            var eligible = Pool.Where(IsEligible).ToList();
            if (eligible.Count == 0) return new List<CardDef>();

            var picked = new List<CardDef>();
            var remaining = eligible;

            while (picked.Count < 3 && remaining.Count > 0)
            {
                // 희귀도 가중 추출 — 해당 희귀도 자격 없으면 동일 시드 스트림에서 전체 풀 폴백
                int[] effective = (int[])rarityWeights.Clone();
                int drawnRarity = PickWeightedIndex(rng, effective);
                var rarityPool = remaining.Where(c => (int)c.Rarity == drawnRarity).ToList();
                if (rarityPool.Count == 0) rarityPool = remaining;

                var idx = Enumerable.Range(0, rarityPool.Count).ToList();
                for (int i = idx.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (idx[i], idx[j]) = (idx[j], idx[i]);
                }
                picked.Add(rarityPool[idx[0]]);
                remaining = remaining.Where(c => c.Id != picked[picked.Count - 1].Id).ToList();
            }

            return picked;
        }

        // ===== 런 라이프사이클 =====

        /// <summary>런 시작 — 소유/보류/상태 전부 리셋 (귀환 시 소멸 계약). AfterlifeSurvivalSystem.BeginRun 직후 호출 (Phase 5 배선).</summary>
        public static void BeginRun(int ring)
        {
            ResetAll();
            _runActive = true;

            // [156차 P4] 진입 무기 캡처 — 귀환 시 원복(EndRun). null-guard — EditMode/씬 부재 no-op
            var combat = PlayerCombat.Instance;
            _capturedWeapon = combat != null ? combat.CurrentWeapon : null;
        }

        /// <summary>
        /// 드라이버 틱 — 만료 폴백/마크 오퍼. 증강 오퍼 미해결 중엔 카드 오퍼 보류 (§3 단일 표시 보장).
        /// IsPending 중에는 생존 드라이버가 호출하지 않는다 (증강과 동일 게이트).
        /// </summary>
        public static void Tick(float elapsed, float targetSec, int seedBase, int ring, float unscaledDt)
        {
            if (!_runActive) return;

            // 대기 만료 폴백 — 결정론 index 0 (증강 동일)
            if (IsPending && Time.unscaledTime - _pendingStartedAtUnscaled > PendingTimeoutSec)
            {
                Debug.LogWarning("[AfterlifeCard] ⏳ 20초 미선택 — 첫 번째 카드 자동 선택(폴백)");
                Select(0);
                return;
            }

            if (IsPending) return;
            // 증강 오퍼 미해결 중 → 카드 오퍼 보류 (UI 단일 표시 보장)
            if (AfterlifeAugmentSystem.IsPending) return;

            TryOffer(Mark1, elapsed, targetSec, seedBase, ring);
            TryOffer(Mark2, elapsed, targetSec, seedBase, ring);
        }

        private static void TryOffer(int mark, float elapsed, float targetSec, int seedBase, int ring)
        {
            if (IsPending) return;                    // 대기 중 신규 오퍼 금지
            if (_offeredMarks.Contains(mark)) return; // 중복 제시 금지
            if (AfterlifeAugmentSystem.IsPending) return; // 증강 보류 게이트
            if (!ShouldOffer(mark, elapsed, targetSec)) return;
            _offeredMarks.Add(mark);

            var offer = GenerateOffer(seedBase + mark * 104729, ring);
            if (offer.Count == 0) return; // 자격 풀 고갈 — 마크 소모 후 다음 기회

            _choices.Clear();
            _choices.AddRange(offer);
            IsPending = true;
            _pendingStartedAtUnscaled = Time.unscaledTime;

            var names = string.Join(" / ", _choices.Select(c => c.Name));
            Debug.Log($"[AfterlifeCard] 🃏 카드 3택 제시 — {names}");
            CombatLog.AddEntry("🃏 저승의 카드가 펼쳐진다 — 한 장을 선택하라", LogType.Warning);
            OnOfferCreated?.Invoke(_choices);
        }

        /// <summary>직접 적용 경로 — UI 핫픽/테스트용 (자격 필터 우회 명시적 호출). null 시 no-op.</summary>
        public static void SelectFromDef(CardDef card)
        {
            if (card == null) return;
            ApplyCard(card);
            Debug.Log($"[AfterlifeCard] ⬆️ 카드 직접 적용 — {card.Name} ({card.Desc})");
            OnCardApplied?.Invoke(card);
        }

        /// <summary>카드 선택 — 소유 등록 + 효과 적용 + 특수 소비 상태 갱신. A4 UI와 키 입력 경유 (Phase 3).</summary>
        public static void Select(int index)
        {
            if (!IsPending) return;
            if (index < 0 || index >= _choices.Count) return;

            var card = _choices[index];
            ApplyCard(card);

            IsPending = false;
            _choices.Clear();
            Debug.Log($"[AfterlifeCard] ⬆️ 카드 적용 — {card.Name} ({card.Desc})");
            OnCardApplied?.Invoke(card);
            OnOfferResolved?.Invoke();
        }

        // ===== Apply (모든 경로 null-guard — EditMode 안전) =====

        private static void ApplyCard(CardDef card)
        {
            if (card == null) return;

            // 소유 등록
            _ownedCounts.TryGetValue(card.Id, out var cur);
            _ownedCounts[card.Id] = cur + 1;

            switch (card.Kind)
            {
                case CardKind.Weapon:
                    _weaponLevels[card.Id] = 1;
                    // [156차 P4] 마지막 획득 무기 = 활성 — 배율 재계산 + 플레이어 스왑
                    _activeWeaponId = card.Id;
                    RecalcWeaponMultipliers();
                    SwapPlayerWeapon(card.Id);
                    break;
                case CardKind.WeaponUpgrade:
                    _weaponLevels[card.WeaponId] = card.Level;
                    RecalcWeaponMultipliers(); // [156차 P4] 활성 무기 강화 즉시 반영
                    break;
                case CardKind.Skill:
                    _ownedSkills.Add(card);
                    break;
                case CardKind.Passive:
                    ApplyPassive(card);
                    break;
                case CardKind.Special:
                    ApplySpecial(card);
                    break;
            }
        }

        // ===== [156차 P4] 무기 스왑/배율 (마지막 획득 무기 = 활성 — 계획서 §3) =====
        private static string _activeWeaponId;        // 활성 무기 카드 Id (마지막 획득)
        private static WeaponData _capturedWeapon;    // 런 진입 시 장착 무기 — 귀환 시 원복용
        private static float _cooldownFactor = 1f;    // 활성 무기 쿨다운 계수

        /// <summary>활성 무기 배율 재계산 — 무기 획득/강화 시 호출. 강화는 활성 무기 레벨(WeaponLevel) 기준(결정론).</summary>
        private static void RecalcWeaponMultipliers()
        {
            float dmg = 1f, cd = 1f;
            int lv = string.IsNullOrEmpty(_activeWeaponId) ? 0 : WeaponLevel(_activeWeaponId); // 1=획득/2=II/3=III — 강화 카드 id 규약 {base}_2·_3
            switch (_activeWeaponId)
            {
                case "w_saw_dagger":
                    dmg = 0.8f; cd = 0.7f;
                    if (lv >= 2) cd *= 0.85f;
                    if (lv >= 3) dmg *= 2.0f; // 연속 타격
                    break;
                case "w_grave_sword":
                    dmg = 1.1f;
                    if (lv >= 2) dmg *= 1.3f;
                    if (lv >= 3) dmg *= 1.5f;
                    break;
                case "w_pierce_spear":
                    dmg = 1.25f; cd = 1.15f;
                    if (lv >= 2) dmg *= 1.4f;
                    break; // [156차 P5] lv3 관통(전방 6m 60%)은 확장 슬롯 — v1은 배율만
                case "w_soul_bow":
                    dmg = 1.0f;
                    break; // [156차 P5] lv2/lv3 화살 수는 자동 발사 배선에서 반영
            }
            _weaponDamageMult = dmg;
            _cooldownFactor = cd;
        }

        /// <summary>무기 카드 획득 시 플레이어 무기 스왑 — null-guard(씬 부재 no-op, EditMode 안전).</summary>
        private static void SwapPlayerWeapon(string weaponCardId)
        {
            var combat = PlayerCombat.Instance;
            if (combat == null) return;
            WeaponData target;
            switch (weaponCardId)
            {
                case "w_saw_dagger": target = WeaponData.Fist; break;   // 톱니 단검 — 주먹 계승(계획서 §2)
                case "w_grave_sword": target = WeaponData.Sword; break;
                case "w_pierce_spear": target = WeaponData.Spear; break;
                case "w_soul_bow": target = WeaponData.Bow; break;
                default: return;
            }
            combat.SetWeapon(target);
        }

        /// <summary>런 종료 — 진입 시 장착 무기 원복 (ResetAll 이전 호출 필수).</summary>
        private static void RestorePlayerWeapon()
        {
            if (_capturedWeapon == null) return;
            var combat = PlayerCombat.Instance;
            if (combat != null) combat.SetWeapon(_capturedWeapon);
            _capturedWeapon = null;
        }

        /// <summary>사신 즉사 판정 순수 함수 — 결정론 EditMode 테스트용(런타임은 UnityEngine.Random 경유).</summary>
        public static bool RollDeathScytheInstakill(System.Random rng)
            => DeathScytheActive && DeathScytheBasePercent > 0 && rng.Next(100) < DeathScytheBasePercent;

        private static void ApplyPassive(CardDef card)
        {
            var health = PlayerHealth.Instance; // null-guard — EditMode/씬 부재 시 no-op
            switch (card.Id)
            {
                case "p_vampire_fang":
                    KillHealPerStack += card.Heal;
                    break;
                case "p_skull_ring":
                    SkullRingActive = true;
                    _skullRingChain = 0;
                    break;
                case "p_emperor_seal":
                    _passiveDamageMult *= card.MultA; // [156차 P4] 패시브 배율 분리
                    break;
                case "p_steel_shield":
                    IncomingDamageFactor *= card.MultA;
                    break;
                case "p_death_scythe":
                    DeathScytheActive = true;
                    DeathScytheBasePercent = card.Int1;
                    DeathScytheEliteBonusPercent = card.Int1; // 정예 5% 가산
                    break;
                case "p_tombstone":
                    MaxHPBonus += card.Flat;
                    if (health != null)
                    {
                        float oldMax = health.MaxHP;
                        health.SetMaxHP(oldMax + card.Flat);
                        health.Heal(card.Flat); // 추가분 즉시 회복
                    }
                    break;
                case "p_time_sand":
                    // 웨이브 간격은 WaveIntervalSeconds 게이트로 Phase 5 배선 — 즉시치환 불필요
                    break;
                case "p_soul_jar":
                    OrbValueMult *= card.MultA;
                    break;
                case "p_shadow_boots":
                    RollDistanceFactor *= card.MultA;
                    break;
                case "p_perfect_mark":
                    ExtraPerfectHitStop += card.Flat;
                    break;
                case "p_chain_seal":
                    EnemyMoveFactor *= card.MultA;
                    break;
                case "p_afterlife_talisman":
                    TelegraphBonusSeconds += card.Flat;
                    break;
                // [156차 P3] 패시브 스택II 4종 — 기존 패시브 case 복제(+1 스택 효과, 값은 카탈로그 슬롯 기준)
                case "p_emperor_seal_plus":
                    _passiveDamageMult *= card.MultA; // [156차 P4] 패시브 배율 분리
                    break;
                case "p_tombstone_plus":
                    MaxHPBonus += card.Flat;
                    if (health != null)
                    {
                        float oldMaxPlus = health.MaxHP;
                        health.SetMaxHP(oldMaxPlus + card.Flat);
                        health.Heal(card.Flat); // 추가분 즉시 회복
                    }
                    break;
                case "p_steel_shield_plus":
                    IncomingDamageFactor *= card.MultA;
                    break;
                case "p_vampire_fang_plus":
                    KillHealPerStack += card.Heal;
                    break;
            }
        }

        private static void ApplySpecial(CardDef card)
        {
            switch (card.Id)
            {
                case "sp_second_life":
                    _secondLifeCount += card.Int1 > 0 ? card.Int1 : 1;
                    break;
                case "sp_pledge_receipt":
                    // SurrenderHarvestRatio 게이트가 소유 기준으로 1.0 반환 — 즉시치환 불필요
                    break;
                case "sp_wave_pardon":
                    _wavePardonCount += card.Int1 > 0 ? card.Int1 : 1;
                    break;
                case "sp_realm_gate":
                    // [156차 P5] 즉시 귀환 문 개방 — 감면은 FinishRun의 ConsumeRealmGate 소비로 적용
                    AfterlifeSurvivalSystem.ForceReturnDoor();
                    break;
            }
        }

        /// <summary>해골 반지 연쇄 카운터 — Phase 5 처치 훅이 호출. 10 도달 시 오브 ×2 후 리셋.</summary>
        public static bool NotifyEnemyKilledForSkullRing()
        {
            if (!SkullRingActive) return false;
            _skullRingChain++;
            if (_skullRingChain >= 10)
            {
                _skullRingChain = 0;
                _skullRingBonusPending = true; // [156차 P5] 다음 오브 ×2 예약 — 흡수 시 소비
                return true;
            }
            return false;
        }

        /// <summary>런 종료 — 소유/보류/배율 전부 소멸 (증강 EndRun 좌표 3곳 동일 배선 — Phase 5).</summary>
        public static void EndRun()
        {
            RestorePlayerWeapon(); // [156차 P4] 귀환 시 진입 무기 원복
            ResetAll();
        }

        /// <summary>테스트/리셋 공용 전면 초기화.</summary>
        public static void ResetAll()
        {
            _ownedCounts.Clear();
            _ownedSkills.Clear();
            _weaponLevels.Clear();
            _choices.Clear();
            _offeredMarks.Clear();
            _passiveDamageMult = 1f; // [156차 P4] 패시브 배율
            _weaponDamageMult = 1f;  // [156차 P4] 무기 배율
            _cooldownFactor = 1f;    // [156차 P4] 쿨다운 계수
            _activeWeaponId = null;  // [156차 P4] 활성 무기 해제(원복 무기는 RestorePlayerWeapon 소관)
            IncomingDamageFactor = 1f;
            EnemyMoveFactor = 1f;
            OrbValueMult = 1f;
            MaxHPBonus = 0f;
            RollDistanceFactor = 1f;
            ExtraPerfectHitStop = 0f;
            TelegraphBonusSeconds = 0f;
            KillHealPerStack = 0f;
            DeathScytheActive = false;
            DeathScytheBasePercent = 0;
            DeathScytheEliteBonusPercent = 0;
            SkullRingActive = false;
            _skullRingChain = 0;
            _boneShieldCharged = false;     // [156차 P5]
            _skullRingBonusPending = false; // [156차 P5]
            _secondLifeCount = 0;
            _wavePardonCount = 0;
            IsPending = false;
            _pendingStartedAtUnscaled = 0f;
            _runActive = false;
        }
    }
}
