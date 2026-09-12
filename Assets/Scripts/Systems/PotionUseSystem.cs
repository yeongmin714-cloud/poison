using System.Collections;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 2026-09-12(P5): 우클릭 복용 완성 — id/displayName 기반 효과 레지스트리.
    /// 분기: 회복(heal/hp/회복/치유) / 은신(stealth/은신) / 신속(speed/신속/속도) / 괴력(attack/괴력/강화) / 진정(sedative/진정/수면).
    /// - 회복: PlayerHealth.Heal/HealFull — "만능" 포함 시 풀회복(GAME_DATA 2.3 만능 치유액=체력 풀회복), 기본 MaxHP×0.4.
    /// - 신속: PlayerMovement.SpeedModifier 임시 배율(만료 시 복원 — 코루틴).
    /// - 괴력: PlayerStats.AttackDamageBase 임시 가산(만료 시 차감 복원 — 코루틴).
    /// - 진정: 본인 복용 개념이라 투약 기록만(적 대상 투약은 별도 경로).
    /// - 효과 피드백은 out effectText로 반환(InventoryWindow 훅에서 로그 표기).
    /// 실패/예외 시 소모 없음(훅 계약: true일 때만 인벤 1개 제거). Systems 어셈블리 — UI 참조 금지.
    /// </summary>
    public static class PotionUseSystem
    {
        // ── 튜닝 상수(GAME_DATA.md 기준) ──
        private const float HealRatio = 0.4f;          // 회복량 = MaxHP × 0.4
        private const float SpeedMultiplier = 1.5f;    // 신속: 이동속도 ×1.5
        private const float SpeedMultiplierHero = 2f;  // 영웅의 신속: 이동속도 ×2(GAME_DATA 2.4)
        private const float SpeedDuration = 15f;       // 신속 지속(초)
        private const float AttackBonusRatio = 0.5f;   // 괴력: 공격력 +50%
        private const float AttackDuration = 20f;      // 괴력 지속(초)

        /// <summary>복용 시도. true = 효과 적용됨(호출부에서 인벤 1개 소모), false = 소모 없음. effectText = 효과 요약(피드백용).</summary>
        public static bool Use(PlayerInventory.ItemData item, Transform player, out string effectText)
        {
            effectText = null;
            try
            {
                if (item == null) return false;

                string id = (item.id ?? string.Empty).ToLowerInvariant();
                string name = (item.displayName ?? string.Empty).ToLowerInvariant();

                // ── 회복 ── heal/hp/회복/치유 — "만능" 포함 시 풀회복
                if (id.Contains("heal") || id.Contains("hp") ||
                    name.Contains("회복") || name.Contains("치유") || name.Contains("만능") ||
                    id.Contains("만능") || name.Contains("생명수"))
                {
                    bool fullHeal = id.Contains("만능") || name.Contains("만능") || name.Contains("생명수");
                    return ApplyHeal(item, player, fullHeal, out effectText);
                }

                // ── 은신 ── 기존 C키 은신 경로(StealthSystem.ToggleStealth) 재사용
                if (id.Contains("stealthpotion") || id.Contains("stealth") || name.Contains("은신"))
                {
                    var stealth = StealthSystem.Instance;
                    if (stealth == null)
                    {
                        Debug.Log("[Potion] 은신 물약 → 실패(사유: StealthSystem 없음)");
                        return false;
                    }
                    if (!stealth.IsStealthed)
                        stealth.ToggleStealth();   // C키와 동일 진입 — 이미 은신 중이면 유지
                    Debug.Log("[Potion] 은신 효과 활성화");
                    effectText = "은신 활성화";
                    return true;
                }

                // ── 신속 ── speed/신속/속도 — PlayerMovement.SpeedModifier 임시 배율
                if (id.Contains("speed") || name.Contains("신속") || name.Contains("속도"))
                    return ApplySpeed(item, player, name.Contains("영웅"), out effectText);

                // ── 괴력 ── attack/strength/괴력/강화/근력 — PlayerStats 공격력 임시 버프
                if (id.Contains("attack") || id.Contains("strength") ||
                    name.Contains("괴력") || name.Contains("근력") || name.Contains("강화"))
                    return ApplyAttack(item, out effectText);

                // ── 진정 ── sedative/수면/진정 — 투약 기록만(적 대상 투약은 별도 경로)
                if (id.Contains("sedative") || id.Contains("sedat") ||
                    name.Contains("진정") || name.Contains("수면") || name.Contains("안정"))
                {
                    Debug.Log($"[Potion] 진정제 복용 기록: {item.displayName} (본인 투약 — 효과 기록만)");
                    effectText = "진정(투약 기록)";
                    return true;
                }

                Debug.Log($"[Potion] {item.id}: 효과 매핑 준비 중");
                return false;
            }
            catch (System.Exception ex)
            {
                // 복용 실패가 게임 진행을 방해하지 않도록 삼킴 — 소모 없음
                Debug.LogWarning($"[Potion] 복용 처리 예외 흡수(소모 없음): {ex.Message}");
                effectText = null;
                return false;
            }
        }

        // ── 효과 적용기 ─────────────────────────────────────────────────────

        /// <summary>회복: 현재HP + MaxHP×0.4 (만능=풀회복). 사망/풀피/PlayerHealth 부재 시 소모 없음.</summary>
        private static bool ApplyHeal(PlayerInventory.ItemData item, Transform player, bool fullHeal, out string effectText)
        {
            effectText = null;
            var health = PlayerHealth.Instance;
            if (health == null && player != null)
                health = player.GetComponent<PlayerHealth>();
            if (health == null)
            {
                Debug.Log($"[Potion] {item.id}: 회복 실패(사유: PlayerHealth 없음)");
                return false;
            }
            if (health.IsDead)
            {
                Debug.Log($"[Potion] {item.id}: 회복 실패(사유: 사망 상태)");
                return false;
            }
            if (!fullHeal && health.CurrentHP >= health.MaxHP)
            {
                // 이미 풀피 — 소모해도 효과 없으므로 낭비 방지(소모 없음)
                Debug.Log($"[Potion] {item.id}: 회복 실패(사유: 이미 최대 체력)");
                return false;
            }

            if (fullHeal)
            {
                health.HealFull();
                effectText = "체력 풀회복";
                Debug.Log("[Potion] 만능 치유액 — 체력 풀회복");
            }
            else
            {
                float amount = health.MaxHP * HealRatio;
                health.Heal(amount);
                effectText = $"체력 +{amount:0}";
                Debug.Log($"[Potion] 체력 +{amount:0} (현재 {health.CurrentHP:0}/{health.MaxHP:0})");
            }
            return true;
        }

        /// <summary>신속: PlayerMovement.SpeedModifier ×배율, 지속 후 /배율 복원(코루틴).</summary>
        private static bool ApplySpeed(PlayerInventory.ItemData item, Transform player, bool hero, out string effectText)
        {
            effectText = null;
            var movement = player != null ? player.GetComponent<PlayerMovement>() : null;
            if (movement == null && player != null)
                movement = player.GetComponentInChildren<PlayerMovement>();
            if (movement == null)
            {
                Debug.Log($"[Potion] {item.id}: 신속 실패(사유: PlayerMovement 없음)");
                return false;
            }

            float mult = hero ? SpeedMultiplierHero : SpeedMultiplier;
            GetRunner().ApplySpeed(movement, mult, SpeedDuration);
            effectText = $"신속 이동속도 ×{mult:0.#} ({SpeedDuration:0}초)";
            Debug.Log($"[Potion] 신속 버프: 이동속도 ×{mult:0.#} ({SpeedDuration:0}초)");
            return true;
        }

        /// <summary>괴력: 공격력 임시 버프 — BuffManager.AddBuff("AttackUp") 우선(자동 만료 복원+HUD 표시),
        /// BuffManager 부재 시 내장 러너 폴백(AttackDamageBase 가산→만료 차감).</summary>
        private static bool ApplyAttack(PlayerInventory.ItemData item, out string effectText)
        {
            effectText = null;
            var stats = PlayerStats.Instance;
            if (stats == null)
            {
                Debug.Log($"[Potion] {item.id}: 괴력 실패(사유: PlayerStats 없음)");
                return false;
            }

            float delta = stats.AttackDamageBase * AttackBonusRatio;
            var buffs = BuffManager.Instance;
            if (buffs != null)
            {
                buffs.AddBuff("AttackUp", delta, AttackDuration);
            }
            else
            {
                GetRunner().ApplyAttack(delta, AttackDuration); // 폴백: BuffManager 없는 씬 대비
            }
            effectText = $"괴력 공격력 +{delta:0} ({AttackDuration:0}초)";
            Debug.Log($"[Potion] 괴력 버프: 공격력 +{delta:0:F1} ({AttackDuration:0}초)");
            return true;
        }

        // ── 버프 러너(정적 Use에서 코루틴 구동용 — 최소 침습 숨은 헬퍼) ──────────

        private static PotionBuffRunner _runner;

        private static PotionBuffRunner GetRunner()
        {
            if (_runner == null) // Unity overload == — 파괴 시 null 취급
            {
                var go = new GameObject("PotionBuffRunner");
                Object.DontDestroyOnLoad(go); // 씬 전환에도 버프 만료 보장
                _runner = go.AddComponent<PotionBuffRunner>();
            }
            return _runner;
        }

        /// <summary>시간 제한 버프 만료 처리 전용 헬퍼(파괴/비활성 금지 — 코루틴 호스트).</summary>
        private sealed class PotionBuffRunner : MonoBehaviour
        {
            public void ApplySpeed(PlayerMovement movement, float multiplier, float duration)
            {
                StartCoroutine(SpeedRoutine(movement, multiplier, duration));
            }

            private IEnumerator SpeedRoutine(PlayerMovement movement, float multiplier, float duration)
            {
                movement.SpeedModifier *= multiplier; // 곱산 스택 — 만료 시 /배율로 복원
                float endTime = Time.time + duration;
                while (Time.time < endTime && movement != null) yield return null;
                if (movement != null)
                {
                    movement.SpeedModifier /= multiplier;
                    Debug.Log($"[Potion] 신속 버프 만료 (이동속도 ÷{multiplier:0.#})");
                }
            }

            public void ApplyAttack(float delta, float duration)
            {
                StartCoroutine(AttackRoutine(delta, duration));
            }

            private IEnumerator AttackRoutine(float delta, float duration)
            {
                var stats = PlayerStats.Instance;
                if (stats == null) yield break;
                stats.AttackDamageBase += delta; // 가산 스택 — 만료 시 차감 복원(타 시스템 변경 보존)
                float endTime = Time.time + duration;
                while (Time.time < endTime && PlayerStats.Instance == stats) yield return null;
                if (PlayerStats.Instance == stats)
                {
                    stats.AttackDamageBase -= delta;
                    Debug.Log($"[Potion] 괴력 버프 만료 (공격력 -{delta:0})");
                }
            }
        }
    }
}