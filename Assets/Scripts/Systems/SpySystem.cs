using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-25: 정보원 파견 시스템 — 영주/병력/약도 정보 수집
    /// 
    /// 포섭된 병사를 정보원으로 선택하여 인근 영지에 파견합니다.
    /// 정보 수집 성공 시 TerritoryState에 spyReportRecon, spyReportInfiltrate, spyReportSurvey 플래그 저장.
    /// 발각 시 정보원 사망 처리.
    /// </summary>
    public static class SpySystem
    {
        // 정보 수집 임무 타입
        public enum SpyMission
        {
            Recon,        // 정찰: 병력 수, 방어 상태, 병사 레벨 정보 수집
            Infiltrate,   // 잠입: 영주 정보(성격, 지병, 선호음식) 수집
            Survey        // 측량: 지형, 약도, 접근 경로 정보 수집
        }

        // 레벨 요구사항
        public const int RECON_REQUIRED_LEVEL = 3;
        public const int INFILTRATE_REQUIRED_LEVEL = 8;
        public const int SURVEY_REQUIRED_LEVEL = 5;

        // 발각 확률
        public const float BASE_DETECT_CHANCE = 0.3f;       // 기본 30%
        public const float LEVEL_DETECT_REDUCTION = 0.008f; // 정보원 레벨 1당 -0.8%
        public const float LOYALTY_DETECT_REDUCTION = 0.005f; // 호감도 1당 -0.5%
        public const float DIFFICULTY_DETECT_INCREASE = 0.05f; // 난이도 링 1단계당 +5%

        // 임무 소요 시간 (초)
        public const float RECON_DURATION = 30f;
        public const float INFILTRATE_DURATION = 60f;
        public const float SURVEY_DURATION = 45f;

        // 결과 구조체
        public struct SpyResult
        {
            public bool success;
            public string message;
            public SpyMission mission;
            public bool detected;
            public bool spyLost;       // 발각 시 정보원 사망
            public string infoGathered; // 수집된 정보 요약 텍스트
        }

        // ===== 메인 메서드 =====

        /// <summary>
        /// 정보원 파견 시도
        /// </summary>
        public static SpyResult SendSpy(GuardPlaceholder spy, TerritoryId targetTerritory, SpyMission mission)
        {
            if (spy == null)
                return Fail("정보원이 없습니다.", mission);

            if (!spy.IsAlive)
                return Fail("정보원이 사망했습니다.", mission);

            if (!spy.IsRecruited)
                return Fail("포섭된 병사만 정보원으로 파견할 수 있습니다.", mission);

            // 레벨 체크
            int requiredLevel = GetRequiredLevel(mission);
            if (spy.Level < requiredLevel)
                return Fail($"정보원 레벨 부족! 필요: Lv.{requiredLevel}, 현재: Lv.{spy.Level}", mission);

            // 대상 영지 정보
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(targetTerritory);
            if (def.territoryName == null)
                return Fail("대상 영지를 찾을 수 없습니다.", mission);

            switch (mission)
            {
                case SpyMission.Recon:
                    return ExecuteReconMission(spy, targetTerritory, def);
                case SpyMission.Infiltrate:
                    return ExecuteInfiltrateMission(spy, targetTerritory, def);
                case SpyMission.Survey:
                    return ExecuteSurveyMission(spy, targetTerritory, def);
                default:
                    return Fail("알 수 없는 임무입니다.", mission);
            }
        }

        // ===== 임무별 실행 =====

        private static SpyResult ExecuteReconMission(GuardPlaceholder spy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.", SpyMission.Recon);

            // 발각 확인
            float detectChance = CalculateDetectChance(spy, targetId);
            bool detected = Random.value < detectChance;

            if (detected)
            {
                // 정보원 사망 처리
                spy.TakeDamage(9999f, Vector3.zero, "Spy caught");
                return new SpyResult
                {
                    success = false,
                    message = $"💀 발각! 정보원 {spy.GuardName} 처형됨.",
                    mission = SpyMission.Recon,
                    detected = true,
                    spyLost = true,
                    infoGathered = ""
                };
            }

            // 성공: 병력 정보 수집
            string terrainType = GetDifficultyTerrainName(def.difficulty);
            string defenseStatus = GetDefenseStatus(def.guardCount);
            string guardLevelRange = GetGuardLevelRange(def.difficulty);

            string info = $"병력 {def.guardCount}명 (Lv.{guardLevelRange}), {terrainType} 방어 상태: {defenseStatus}";

            // 플래그 저장
            state.spyReportRecon = true;
            state.lastSpyTime = Time.time;

            return new SpyResult
            {
                success = true,
                message = $"🔍 {def.territoryName} 정찰 성공!",
                mission = SpyMission.Recon,
                detected = false,
                spyLost = false,
                infoGathered = info
            };
        }

        private static SpyResult ExecuteInfiltrateMission(GuardPlaceholder spy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.", SpyMission.Infiltrate);

            // 발각 확인
            float detectChance = CalculateDetectChance(spy, targetId);
            bool detected = Random.value < detectChance;

            if (detected)
            {
                // 정보원 사망 처리
                spy.TakeDamage(9999f, Vector3.zero, "Spy caught");
                return new SpyResult
                {
                    success = false,
                    message = $"💀 발각! 정보원 {spy.GuardName} 처형됨.",
                    mission = SpyMission.Infiltrate,
                    detected = true,
                    spyLost = true,
                    infoGathered = ""
                };
            }

            // 성공: 영주 정보 수집
            string lordName = def.lord.lordName;
            string personality = GetPersonalityName(def.lord.personality);
            string food = string.IsNullOrEmpty(def.lord.preferredFood) ? "알 수 없음" : def.lord.preferredFood;
            string disease = string.IsNullOrEmpty(def.lord.chronicDisease) ? "없음" : def.lord.chronicDisease;

            string info = $"영주 {lordName}, 성격: {personality}, 선호: {food}, 지병: {disease}";

            // 플래그 저장
            state.spyReportInfiltrate = true;
            state.lastSpyTime = Time.time;

            return new SpyResult
            {
                success = true,
                message = $"🕵️ {def.territoryName} 잠입 성공!",
                mission = SpyMission.Infiltrate,
                detected = false,
                spyLost = false,
                infoGathered = info
            };
        }

        private static SpyResult ExecuteSurveyMission(GuardPlaceholder spy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null)
                return Fail("영지 상태를 찾을 수 없습니다.", SpyMission.Survey);

            // 발각 확인
            float detectChance = CalculateDetectChance(spy, targetId);
            bool detected = Random.value < detectChance;

            if (detected)
            {
                // 정보원 사망 처리
                spy.TakeDamage(9999f, Vector3.zero, "Spy caught");
                return new SpyResult
                {
                    success = false,
                    message = $"💀 발각! 정보원 {spy.GuardName} 처형됨.",
                    mission = SpyMission.Survey,
                    detected = true,
                    spyLost = true,
                    infoGathered = ""
                };
            }

            // 성공: 지형/약도 정보 수집
            string terrainType = GetDifficultyTerrainName(def.difficulty);
            string approach = GetApproachPath(def.difficulty);
            string hideout = GetHideoutSpots(def.difficulty);

            string info = $"{terrainType} 지형, {approach} 접근 용이, {hideout}";

            // 플래그 저장
            state.spyReportSurvey = true;
            state.lastSpyTime = Time.time;

            return new SpyResult
            {
                success = true,
                message = $"🗺️ {def.territoryName} 측량 성공!",
                mission = SpyMission.Survey,
                detected = false,
                spyLost = false,
                infoGathered = info
            };
        }

        // ===== 헬퍼 =====

        public static string GetMissionName(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.Recon: return "🔍 정찰";
                case SpyMission.Infiltrate: return "🕵️ 잠입";
                case SpyMission.Survey: return "🗺️ 측량";
                default: return "알 수 없음";
            }
        }

        public static string GetMissionDescription(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.Recon: return "병력 수, 방어 상태, 병사 레벨 정보를 수집합니다 (Lv.3+)";
                case SpyMission.Infiltrate: return "영주 정보(성격, 지병, 선호음식)를 수집합니다 (Lv.8+)";
                case SpyMission.Survey: return "지형, 약도, 접근 경로 정보를 수집합니다 (Lv.5+)";
                default: return "";
            }
        }

        public static int GetRequiredLevel(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.Recon: return RECON_REQUIRED_LEVEL;
                case SpyMission.Infiltrate: return INFILTRATE_REQUIRED_LEVEL;
                case SpyMission.Survey: return SURVEY_REQUIRED_LEVEL;
                default: return 99;
            }
        }

        public static float GetDuration(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.Recon: return RECON_DURATION;
                case SpyMission.Infiltrate: return INFILTRATE_DURATION;
                case SpyMission.Survey: return SURVEY_DURATION;
                default: return 0f;
            }
        }

        /// <summary>
        /// 정보원으로 파견 가능한 병사 목록 반환 (포섭 + 생존)
        /// </summary>
        public static List<GuardPlaceholder> GetAvailableSpies()
        {
            var result = new List<GuardPlaceholder>();
            var guards = Object.FindObjectsOfType<GuardPlaceholder>();
            foreach (var g in guards)
            {
                if (g.IsAlive && g.IsRecruited)
                    result.Add(g);
            }
            return result;
        }

        /// <summary>
        /// 발각 확률 계산
        /// 기본 30% - 호감도*0.5% - 레벨*0.8% + 난이도링*5%
        /// </summary>
        public static float CalculateDetectChance(GuardPlaceholder spy, TerritoryId targetId)
        {
            var state = TerritoryDatabase.Instance.GetState(targetId);
            if (state == null) return 0.5f;

            float chance = BASE_DETECT_CHANCE;
            chance -= state.loyaltyToPlayer * LOYALTY_DETECT_REDUCTION;
            chance -= spy.Level * LEVEL_DETECT_REDUCTION;
            chance += GetDifficultyModifier(TerritoryDatabase.Instance.GetDefinition(targetId).difficulty);
            return Mathf.Clamp01(chance);
        }

        private static float GetDifficultyModifier(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return 0f;
                case TerritoryDifficulty.Ring2: return DIFFICULTY_DETECT_INCREASE;      // +0.05
                case TerritoryDifficulty.Ring3: return DIFFICULTY_DETECT_INCREASE * 2f;  // +0.10
                case TerritoryDifficulty.Ring4: return DIFFICULTY_DETECT_INCREASE * 3f;  // +0.15
                case TerritoryDifficulty.Empire: return DIFFICULTY_DETECT_INCREASE * 4f; // +0.20
                default: return 0f;
            }
        }

        private static string GetDifficultyTerrainName(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "초원";
                case TerritoryDifficulty.Ring2: return "구릉";
                case TerritoryDifficulty.Ring3: return "산악";
                case TerritoryDifficulty.Ring4: return "협곡";
                case TerritoryDifficulty.Empire: return "황성";
                default: return "알 수 없음";
            }
        }

        private static string GetDefenseStatus(int guardCount)
        {
            if (guardCount <= 3) return "약함";
            if (guardCount <= 6) return "보통";
            if (guardCount <= 10) return "강함";
            return "매우 강함";
        }

        private static string GetGuardLevelRange(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "1~3";
                case TerritoryDifficulty.Ring2: return "3~8";
                case TerritoryDifficulty.Ring3: return "5~12";
                case TerritoryDifficulty.Ring4: return "8~15";
                case TerritoryDifficulty.Empire: return "10~20";
                default: return "1~5";
            }
        }

        private static string GetApproachPath(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "남쪽에서";
                case TerritoryDifficulty.Ring2: return "동쪽에서";
                case TerritoryDifficulty.Ring3: return "북서쪽에서";
                case TerritoryDifficulty.Ring4: return "지하 통로로";
                case TerritoryDifficulty.Empire: return "비밀 통로로";
                default: return "정문에서";
            }
        }

        private static string GetHideoutSpots(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "북쪽 바위 뒤 은신 가능";
                case TerritoryDifficulty.Ring2: return "동쪽 숲에 은신 가능";
                case TerritoryDifficulty.Ring3: return "서쪽 동굴에 은신 가능";
                case TerritoryDifficulty.Ring4: return "남쪽 폐허에 은신 가능";
                case TerritoryDifficulty.Empire: return "지하 비밀 방에 은신 가능";
                default: return "은신처 없음";
            }
        }

        private static string GetPersonalityName(LordPersonality personality)
        {
            switch (personality)
            {
                case LordPersonality.Neutral: return "보통";
                case LordPersonality.Greedy: return "탐욕스러움";
                case LordPersonality.Suspicious: return "의심 많음";
                case LordPersonality.Brave: return "용감함";
                case LordPersonality.Cowardly: return "겁많음";
                case LordPersonality.Wise: return "현명함";
                case LordPersonality.Cruel: return "잔인함";
                default: return "알 수 없음";
            }
        }

        private static SpyResult Success(string msg, SpyMission mission, string infoGathered)
            => new SpyResult { success = true, message = msg, mission = mission, detected = false, spyLost = false, infoGathered = infoGathered };

        private static SpyResult Fail(string msg, SpyMission mission)
            => new SpyResult { success = false, message = msg, mission = mission, detected = false, spyLost = false, infoGathered = "" };
    }
}
