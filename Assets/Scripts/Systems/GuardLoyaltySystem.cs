using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-10: 병사 호감도 시스템 (국가 기반 + 행동 영향)
    /// 
    /// 영지 소유 상태 변화를 감지하여 소속 국가 병사들의 호감도를 자동 조정합니다.
    /// 선물/뇌물/약물/위협 등 행동에 따른 호감도 변화도 처리합니다.
    /// </summary>
    public static class GuardLoyaltySystem
    {
        // 호감도 최소/최대
        public const float MIN_LOYALTY = -100f;
        public const float MAX_LOYALTY = 100f;

        // ===== 국가 기반 호감도 변화 =====
        private static readonly Dictionary<NationType, NationType[]> _hostileNations = new Dictionary<NationType, NationType[]>
        {
            { NationType.East, new[] { NationType.West } },
            { NationType.West, new[] { NationType.East, NationType.North } },
            { NationType.South, new[] { NationType.North } },
            { NationType.North, new[] { NationType.South, NationType.West } },
            { NationType.Empire, new[] { NationType.East, NationType.West, NationType.South, NationType.North } }
        };

        // ===== 호감도 변화 상수 (ROADMAP 5.3.3) =====
        public const float SAME_NATION_BONUS = 10f;        // 동일 국가 영지 소유
        public const float HOSTILE_NATION_PENALTY = -20f;   // 적대 국가 영지 소유
        public const float HOSTILE_MULTI_PENALTY = -5f;     // 추가 적대 영지당 패널티
        public const float GIFT_BONUS_MIN = 5f;             // 선물 최소
        public const float GIFT_BONUS_MAX = 30f;            // 선물 최대
        public const float DRUG_BONUS = 15f;                // 약물 제공
        public const float THREAT_BONUS = 20f;              // 위협 (일시적)
        public const float THREAT_BACKLASH = -30f;          // 위협 후폭풍
        public const float LOYAL_AT = 100f;                 // 완전 충성
        public const float HOSTILE_AT = 0f;                 // 적대 전환

        /// <summary>
        /// GuardPlaceholder의 호감도를 업데이트합니다.
        /// 영지 소유 상태와 병사 국가에 기반한 기본 호감도를 계산합니다.
        /// </summary>
        public static void UpdateLoyaltyByTerritory(GuardPlaceholder guard)
        {
            if (guard == null) return;
            var db = TerritoryDatabase.Instance;
            NationType guardNation = ParseNationFromKorean(guard.Nation);
            if (guardNation == NationType.None) return;

            float baseLoyalty = 50f; // 기본값
            int hostileCount = 0;

            // 모든 영지의 소유 상태 확인
            foreach (var def in db.GetAllDefinitions())
            {
                var state = db.GetState(def.id);
                if (state == null || state.ownership != TerritoryOwnership.PlayerOwned)
                    continue;

                // 플레이어가 소유한 영지의 국가 확인
                if (def.nation == guardNation)
                {
                    // 동일 국가 영지 점령 → 호감도 상승
                    baseLoyalty += SAME_NATION_BONUS;
                }
                else if (IsHostileNation(guardNation, def.nation))
                {
                    // 적대 국가 영지 점령 → 호감도 하락
                    baseLoyalty += HOSTILE_NATION_PENALTY;
                    hostileCount++;
                }
            }

            // 추가 적대 영지당 패널티 (한 번만 적용)
            if (hostileCount > 1)
            {
                baseLoyalty += (hostileCount - 1) * HOSTILE_MULTI_PENALTY;
            }

            guard.Loyalty = Mathf.Clamp(baseLoyalty, MIN_LOYALTY, MAX_LOYALTY);
        }

        /// <summary>
        /// 모든 GuardPlaceholder의 호감도를 Territory 상태에 맞게 일괄 업데이트
        /// </summary>
        public static void UpdateAllGuards()
        {
            var guards = Object.FindObjectsByType<GuardPlaceholder>();
            foreach (var guard in guards)
            {
                UpdateLoyaltyByTerritory(guard);
            }
        }

        /// <summary>
        /// 선물/뇌물: 호감도 +5~30 (아이템 가치 기반)
        /// </summary>
        public static void GiveGift(GuardPlaceholder guard, int giftValue)
        {
            if (guard == null) return;
            float bonus = Mathf.Lerp(GIFT_BONUS_MIN, GIFT_BONUS_MAX, Mathf.Clamp01(giftValue / 100f));
            guard.Loyalty += bonus;
            Debug.Log($"[GuardLoyalty] 선물 지급: {guard.GuardName} 호감도 +{bonus:F1} → {guard.Loyalty:F0}");
        }

        /// <summary>
        /// 약물 제공: 호감도 +15 (중독도 +5~15)
        /// </summary>
        public static void GiveDrug(GuardPlaceholder guard, int drugPotency = 1)
        {
            if (guard == null) return;
            guard.Loyalty += DRUG_BONUS;
            guard.Addiction += 5f * drugPotency;
            Debug.Log($"[GuardLoyalty] 약물 제공: {guard.GuardName} 호감도 +{DRUG_BONUS}, 중독도 +{5f * drugPotency}");
        }

        /// <summary>
        /// 위협: 즉시 호감도 +20 (일시적), 이후 -30 보복
        /// 보복은 코루틴/지연 호출 필요 — 여기서는 GuardPlaceholder에 플래그 설정
        /// </summary>
        public static void Threaten(GuardPlaceholder guard)
        {
            if (guard == null) return;
            guard.Loyalty += THREAT_BONUS;
            Debug.Log($"[GuardLoyalty] 위협: {guard.GuardName} 호감도 +{THREAT_BONUS} (일시적, 보복 예정)");
            // TODO: 일정 시간 후 THREAT_BACKLASH 적용 (코루틴 필요, C9-xx에서 구현)
        }

        /// <summary>
        /// 보복 적용 (위협 후 일정 시간 뒤 호출)
        /// </summary>
        public static void ApplyThreatBacklash(GuardPlaceholder guard)
        {
            if (guard == null) return;
            guard.Loyalty += THREAT_BACKLASH;
            Debug.Log($"[GuardLoyalty] 위협 보복: {guard.GuardName} 호감도 {THREAT_BACKLASH} → {guard.Loyalty:F0}");
        }

        /// <summary>
        /// 호감도 기반 태그 반환
        /// </summary>
        public static string GetLoyaltyTag(float loyalty)
        {
            if (loyalty >= 90) return "충성";
            if (loyalty >= 70) return "우호적";
            if (loyalty >= 40) return "보통";
            if (loyalty >= 20) return "냉담";
            return "적대";
        }

        // ===== 헬퍼 메서드 =====

        private static bool IsHostileNation(NationType guardNation, NationType targetNation)
        {
            if (_hostileNations.TryGetValue(guardNation, out var hostiles))
            {
                foreach (var h in hostiles)
                    if (h == targetNation) return true;
            }
            return false;
        }

        private static NationType ParseNationFromKorean(string korean)
        {
            switch (korean)
            {
                case "동": return NationType.East;
                case "서": return NationType.West;
                case "남": return NationType.South;
                case "북": return NationType.North;
                case "황제국": return NationType.Empire;
                default: return NationType.None;
            }
        }
    }
}