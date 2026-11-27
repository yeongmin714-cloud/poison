using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [119차] 충성도 탈영 시스템 — 충성도 임계 이하 병사가 소속을 떠나 방랑자로 전환한다.
    ///  · 호출: GuardSalaryManager.OnDayStart(일 1회). 정적 무상태(멱등) — 중복 호출 무해.
    ///  · 임계: -70 (GuardHostilitySystem ALARM -50 아래 별도 신설 — 기존 적대 규약 무변경)
    ///  · 제외: 사망/게이트키퍼(TerritoryGateSystem 구조 보호)/이미 방랑자/소속 없는 중간 상태
    ///  · 포섭 병사: SetRecruited(false) + 로스터 제거 + 방랑자화
    ///  · AI 고용(IsAIHired): 로스터 제거 + 방랑자화
    ///  · 영지 guardAliveRatio는 감산하지 않는다(탈영은 감축과 달리 영지 전력 산식 변경 없음 — v1 규약)
    /// </summary>
    public static class WandererDesertionSystem
    {
        /// <summary>탈영 충성도 임계 — 이 값 이하(≤)인 소속 병사가 탈영한다.</summary>
        public const float DesertionLoyaltyThreshold = -70f;

        /// <summary>
        /// 전체 병사 스캔 후 임계 이하 소속 병사를 방랑자로 전환한다. 반환값은 전환된 병사 수.
        /// 정적 무상태 — 같은 날 중복 호출해도 이미 방랑자가 된 병사는 재처리되지 않아 멱등.
        /// </summary>
        public static int ProcessDesertions()
        {
            int deserted = 0;
            try
            {
                GuardManager manager = GuardManager.Instance;
                var guards = Object.FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);
                if (guards == null) return 0;
                foreach (var guard in guards)
                {
                    if (guard == null || !guard.IsAlive || guard.IsGatekeeper || guard.IsWanderer) continue;
                    if (guard.Loyalty > DesertionLoyaltyThreshold) continue;
                    if (!guard.IsRecruited && !guard.IsAIHired) continue;   // 소속 없는 중간 상태(시장 풀 등) 스킵
                    string reason = $"탈영(충성도 {guard.Loyalty:F0})";
                    if (guard.IsRecruited) guard.SetRecruited(false);
                    try { manager?.RemoveGuardFromAllTerritories(guard); }
                    catch (System.Exception ex) { Debug.LogWarning($"[탈영] 로스터 제거 실패 — {guard.GuardName}: {ex.Message}"); }
                    guard.BecomeWanderer(reason);
                    deserted++;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[탈영] 스캔 실패 — {ex.Message}");
                return deserted;
            }
            if (deserted > 0) Debug.Log($"[탈영] 충성도 이탈 병사 {deserted}명 — 방랑자 전환 완료");
            return deserted;
        }
    }
}
