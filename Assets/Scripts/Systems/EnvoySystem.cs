using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-24: 특사 파견 시스템 — 외교, 선물, 독살
    /// 
    /// 포섭된 병사를 특사로 선택하여 인근 영지에 파견합니다.
    /// 영지 호감도 변경, 동맹 제안, 독살 등 다양한 외교 행동 가능.
    /// </summary>
    public static class EnvoySystem
    {
        // 특사 임무 타입
        public enum EnvoyMission
        {
            Gift,           // 선물 전달 (+5~15 호감도)
            Friendship,     // 우호 제안 (+20 호감도, 일시적 휴전)
            Alliance,       // 동맹 제안 (국가 단위)
            Assassinate     // 독살 선물 (영주 암살)
        }

        // 임무별 필요 레벨
        public const int GIFT_REQUIRED_LEVEL = 5;
        public const int FRIENDSHIP_REQUIRED_LEVEL = 10;
        public const int ALLIANCE_REQUIRED_LEVEL = 20;
        public const int ASSASSINATE_REQUIRED_LEVEL = 15;

        // 발각 확률 관련
        public const float BASE_DETECT_CHANCE = 0.2f;
        public const float LOYALTY_DETECT_REDUCTION = 0.01f; // 호감도 1당 -1%
        public const float LEVEL_DETECT_REDUCTION = 0.005f; // 특사 레벨 1당 -0.5%

        // 결과
        public struct EnvoyResult
        {
            public bool success;
            public string message;
            public int loyaltyChange;
            public bool detected;
        }

        /// <summary>
        /// 특사 파견 시도
        /// </summary>
        public static EnvoyResult SendEnvoy(GuardPlaceholder envoy, TerritoryId targetTerritory, EnvoyMission mission)
        {
            if (envoy == null || !envoy.IsAlive)
                return Fail("특사가 없거나 사망했습니다.");

            if (!envoy.IsRecruited)
                return Fail("포섭된 병사만 특사로 파견할 수 있습니다.");

            // 레벨 체크
            int requiredLevel = GetRequiredLevel(mission);
            if (envoy.Level < requiredLevel)
                return Fail($"특사 레벨 부족! 필요: Lv.{requiredLevel}, 현재: Lv.{envoy.Level}");

            // 대상 영지 정보
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(targetTerritory);
            if (def.territoryName == null)
                return Fail("대상 영지를 찾을 수 없습니다.");

            switch (mission)
            {
                case EnvoyMission.Gift:
                    return ExecuteGiftMission(envoy, targetTerritory, def);
                case EnvoyMission.Friendship:
                    return ExecuteFriendshipMission(envoy, targetTerritory, def);
                case EnvoyMission.Alliance:
                    return ExecuteAllianceMission(envoy, targetTerritory, def);
                case EnvoyMission.Assassinate:
                    return ExecuteAssassinateMission(envoy, targetTerritory, def);
                default:
                    return Fail("알 수 없는 임무입니다.");
            }
        }

        private static EnvoyResult ExecuteGiftMission(GuardPlaceholder envoy, TerritoryId targetId, TerritoryDefinition def)
        {
            int loyaltyGain = Random.Range(5, 16);
            int currentLoyalty = GetTerritoryLoyalty(targetId);
            SetTerritoryLoyalty(targetId, currentLoyalty + loyaltyGain);

            return Success($"{def.territoryName}에 선물 전달 성공! 호감도 +{loyaltyGain}", loyaltyGain, false);
        }

        private static EnvoyResult ExecuteFriendshipMission(GuardPlaceholder envoy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.");

            int currentLoyalty = state.loyaltyToPlayer > 0 ? (int)state.loyaltyToPlayer : 0;
            if (currentLoyalty < 30)
                return Fail($"우호 제안 실패! 현재 호감도가 너무 낮습니다 ({currentLoyalty}). 30 이상 필요.");

            int gain = 20;
            SetTerritoryLoyalty(targetId, currentLoyalty + gain);
            return Success($"{def.territoryName}와 우호 관계! 호감도 +{gain}", gain, false);
        }

        private static EnvoyResult ExecuteAllianceMission(GuardPlaceholder envoy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.");

            if (state.loyaltyToPlayer < 60)
                return Fail($"동맹 제안 실패! 호감도 부족 ({(int)state.loyaltyToPlayer}/60)");

            int gain = 30;
            SetTerritoryLoyalty(targetId, (int)state.loyaltyToPlayer + gain);
            return Success($"🎉 {def.nation}와 동맹 체결! 호감도 +{gain}", gain, false);
        }

        private static EnvoyResult ExecuteAssassinateMission(GuardPlaceholder envoy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.");

            // 발각 확률 계산
            float detectChance = BASE_DETECT_CHANCE;
            detectChance -= state.loyaltyToPlayer * LOYALTY_DETECT_REDUCTION; // 호감도 높으면 덜 발각
            detectChance -= envoy.Level * LEVEL_DETECT_REDUCTION;             // 레벨 높으면 덜 발각
            detectChance = Mathf.Clamp01(detectChance);

            bool detected = Random.value < detectChance;

            if (detected)
            {
                // 발각
                SetTerritoryLoyalty(targetId, 0);
                // 특사 사망 처리
                envoy.TakeDamage(9999f, Vector3.zero, "Executed");
                return Fail($"💀 발각! {envoy.GuardName} 처형됨. 호감도 0");
            }
            else
            {
                // 성공: 영주 암살
                SetTerritoryLoyalty(targetId, Mathf.Max(0, (int)state.loyaltyToPlayer - 30));

                // 영주 사망 처리 — TerritoryState에 플래그 (현재는 간단히)
                return Success($"☠️ {def.territoryName} 영주 암살 성공! (미발각)", -30, false);
            }
        }

        // ===== 헬퍼 =====

        public static string GetMissionName(EnvoyMission mission)
        {
            switch (mission)
            {
                case EnvoyMission.Gift: return "🎁 선물 전달";
                case EnvoyMission.Friendship: return "🤝 우호 제안";
                case EnvoyMission.Alliance: return "🕊️ 동맹 제안";
                case EnvoyMission.Assassinate: return "☠️ 독살 선물";
                default: return "알 수 없음";
            }
        }

        public static string GetMissionDescription(EnvoyMission mission)
        {
            switch (mission)
            {
                case EnvoyMission.Gift: return "대상 영지에 선물을 전달하여 호감도를 높입니다";
                case EnvoyMission.Friendship: return "우호 관계를 제안하여 일시적 휴전을 얻습니다";
                case EnvoyMission.Alliance: return "국가 단위 동맹을 제안합니다";
                case EnvoyMission.Assassinate: return "음식에 독을 넣어 영주를 암살합니다 (발각 위험)";
                default: return "";
            }
        }

        public static int GetRequiredLevel(EnvoyMission mission)
        {
            switch (mission)
            {
                case EnvoyMission.Gift: return GIFT_REQUIRED_LEVEL;
                case EnvoyMission.Friendship: return FRIENDSHIP_REQUIRED_LEVEL;
                case EnvoyMission.Alliance: return ALLIANCE_REQUIRED_LEVEL;
                case EnvoyMission.Assassinate: return ASSASSINATE_REQUIRED_LEVEL;
                default: return 99;
            }
        }

        /// <summary>
        /// 특사로 파견 가능한 병사 목록 반환 (포섭 + 생존 + 전투가능 or 역할 상관없음)
        /// </summary>
        public static List<GuardPlaceholder> GetAvailableEnvoys()
        {
            var result = new List<GuardPlaceholder>();
            var guards = Object.FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);
            foreach (var g in guards)
            {
                if (g.IsAlive && g.IsRecruited)
                    result.Add(g);
            }
            return result;
        }

        public static float CalculateDetectChance(GuardPlaceholder envoy, TerritoryId targetId)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null) return 0.5f;

            float chance = BASE_DETECT_CHANCE;
            chance -= state.loyaltyToPlayer * LOYALTY_DETECT_REDUCTION;
            chance -= envoy.Level * LEVEL_DETECT_REDUCTION;
            return Mathf.Clamp01(chance);
        }

        private static int GetTerritoryLoyalty(TerritoryId id)
        {
            var state = TerritoryDatabase.Instance.GetState(id);
            return state != null ? (int)state.loyaltyToPlayer : 0;
        }

        private static void SetTerritoryLoyalty(TerritoryId id, int value)
        {
            var state = TerritoryDatabase.Instance.GetState(id);
            if (state != null)
                state.loyaltyToPlayer = Mathf.Clamp(value, 0, 100);
        }

        private static EnvoyResult Success(string msg, int loyaltyChange, bool detected)
            => new EnvoyResult { success = true, message = msg, loyaltyChange = loyaltyChange, detected = detected };
        private static EnvoyResult Fail(string msg)
            => new EnvoyResult { success = false, message = msg, loyaltyChange = 0, detected = false };
    }
}