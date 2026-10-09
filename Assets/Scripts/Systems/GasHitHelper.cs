using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-10-08] 가스 피격 반응 공용 헬퍼.
    /// 가스 데미지 weaponType("Poison"/"GasSprayer_Poison"/"GasCloud_Poison" 등) 판정과,
    /// 병사(GuardPlaceholder)에게 방독면 미착용 시 경직 반응을 부여한다.
    ///
    /// ⚠ 몬스터(AnimalAI)는 TakeDamage에서 이미 모든 공격에 HitReaction 경직을 플레이하므로
    ///   이곳에서 HitReactionDriver를 호출하면 이중 반응(회귀)이 된다 — 몬스터는 건드리지 않는다.
    /// </summary>
    public static class GasHitHelper
    {
        /// <summary>가스 계열 무기 타입인지 판정 (접두 "gas"/"poison", 대소문자 무관).</summary>
        public static bool IsGasWeaponType(string weaponType)
        {
            if (string.IsNullOrEmpty(weaponType)) return false;
            string t = weaponType.Trim().ToLowerInvariant();
            return t.StartsWith("gas") || t.StartsWith("poison") || t.Contains("gascloud");
        }

        /// <summary>
        /// 병사에게 가스 경직 반응 적용. 방독면 미착용일 때만 유효(착용 시 반응 없음).
        /// 실제로 경직을 적용했으면 true.
        /// </summary>
        public static bool TryApplyGuardGasStun(GuardPlaceholder guard, Vector3 hitDirection, string weaponType)
        {
            if (guard == null) return false;
            if (!IsGasWeaponType(weaponType)) return false;
            if (guard.IsGasMaskEquipped) return false;   // 방독면 착용 → 반응 없음

            HitReactionDriver.Apply(guard.gameObject, hitDirection, HitSeverity.Light, false);
            return true;
        }
    }
}