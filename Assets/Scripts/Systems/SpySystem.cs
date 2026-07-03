using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-25: 정보원 파견 시스템 — 영주/병력/약도 정보 수집 + 방해 공작
    /// 
    /// 포섭된 병사를 정보원으로 선택하여 인근 영지에 파견합니다.
    /// 정보 수집 성공 시 TerritoryState에 spyReport 플래그 저장.
    /// 발각 시 정보원 사망 처리.
    /// 
    /// [5.3.9.3] 정보원 파견
    /// - 영주 정보 (Lv.5+)  — 선호 음식, 지병 파악
    /// - 병력 정보 (Lv.10+) — 병사 수, 레벨, 배치 파악
    /// - 영지 약도 (Lv.15+) — 영지 내부 구조, 취약점 파악
    /// - 방해 공작 (Lv.20+) — 병사 중독, 식량 창고 파괴 등
    /// </summary>
    public static class SpySystem
    {
        // 정보 수집 임무 타입 — ROADMAP 5.3.9.3
        public enum SpyMission
        {
            LordInfo,       // 🔍 영주 정보 — Lv.5+: 선호 음식, 지병 파악 (1일)
            TroopInfo,      // 📋 병력 정보 — Lv.10+: 병사 수, 레벨, 배치 파악 (2일)
            TerritoryMap,   // 🗺️ 영지 약도 — Lv.15+: 영지 내부 구조, 취약점 파악 (3일)
            Sabotage        // 💣 방해 공작 — Lv.20+: 병사 중독, 식량 창고 파괴 등 (2일)
        }

        // 레벨 요구사항 — ROADMAP 기준
        public const int LORDINFO_REQUIRED_LEVEL = 5;
        public const int TROOPINFO_REQUIRED_LEVEL = 10;
        public const int TERRITORYMAP_REQUIRED_LEVEL = 15;
        public const int SABOTAGE_REQUIRED_LEVEL = 20;

        // 발각 확률
        public const float BASE_DETECT_CHANCE = 0.3f;       // 기본 30%
        public const float LEVEL_DETECT_REDUCTION = 0.008f; // 정보원 레벨 1당 -0.8%
        public const float LOYALTY_DETECT_REDUCTION = 0.005f; // 호감도 1당 -0.5%
        public const float DIFFICULTY_DETECT_INCREASE = 0.05f; // 난이도 링 1단계당 +5%

        // 임무 소요 시간 (초) — ROADMAP 기준 1일=30초로 환산
        public const float LORDINFO_DURATION = 30f;     // 1일
        public const float TROOPINFO_DURATION = 60f;    // 2일
        public const float TERRITORYMAP_DURATION = 90f; // 3일
        public const float SABOTAGE_DURATION = 60f;     // 2일

        // 발각 시 정보원 즉시 사망 데미지
        private const float EXECUTION_DAMAGE = 9999f;

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
            if (string.IsNullOrEmpty(def.territoryName))
                return Fail("대상 영지를 찾을 수 없습니다.", mission);

            switch (mission)
            {
                case SpyMission.LordInfo:
                    return ExecuteLordInfoMission(spy, targetTerritory, def);
                case SpyMission.TroopInfo:
                    return ExecuteTroopInfoMission(spy, targetTerritory, def);
                case SpyMission.TerritoryMap:
                    return ExecuteTerritoryMapMission(spy, targetTerritory, def);
                case SpyMission.Sabotage:
                    return ExecuteSabotageMission(spy, targetTerritory, def);
                default:
                    return Fail("알 수 없는 임무입니다.", mission);
            }
        }

        // ===== 임무별 실행 =====

        /// <summary>
        /// 🔍 영주 정보 — 선호 음식, 지병 파악 (Lv.5+)
        /// </summary>
        private static SpyResult ExecuteLordInfoMission(GuardPlaceholder spy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = GetStateOrFail(targetId);
            if (state == null) return Fail("영지 상태를 찾을 수 없습니다.", SpyMission.LordInfo);

            // 발각 확인
            var detectResult = TryDetect(spy, targetId, SpyMission.LordInfo);
            if (detectResult.HasValue) return detectResult.Value;

            // 성공: 영주 정보 수집
            string lordName = def.lord.lordName;
            string food = string.IsNullOrEmpty(def.lord.preferredFood) ? "알 수 없음" : def.lord.preferredFood;
            string disease = string.IsNullOrEmpty(def.lord.chronicDisease) ? "없음" : def.lord.chronicDisease;
            string personality = GetPersonalityName(def.lord.personality);

            string info = $"영주 {lordName}\n선호 음식: {food}\n지병: {disease}\n성격: {personality}";

            // 플래그 저장
            state.spyReportInfiltrate = true;
            state.lastSpyTime = Time.time;

            return Success($"🔍 {def.territoryName} 영주 정보 수집 성공!", SpyMission.LordInfo, info);
        }

        /// <summary>
        /// 📋 병력 정보 — 병사 수, 레벨, 배치 파악 (Lv.10+)
        /// </summary>
        private static SpyResult ExecuteTroopInfoMission(GuardPlaceholder spy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = GetStateOrFail(targetId);
            if (state == null) return Fail("영지 상태를 찾을 수 없습니다.", SpyMission.TroopInfo);

            // 발각 확인
            var detectResult = TryDetect(spy, targetId, SpyMission.TroopInfo);
            if (detectResult.HasValue) return detectResult.Value;

            // 성공: 병력 정보 수집
            string defenseStatus = GetDefenseStatus(def.guardCount);
            string guardLevelRange = GetGuardLevelRange(def.difficulty);
            string deployment = GetDeploymentInfo(def.difficulty);

            string info = $"병력 {def.guardCount}명 (Lv.{guardLevelRange})\n방어 상태: {defenseStatus}\n배치: {deployment}";

            // 플래그 저장
            state.spyReportRecon = true;
            state.lastSpyTime = Time.time;

            return Success($"📋 {def.territoryName} 병력 정보 수집 성공!", SpyMission.TroopInfo, info);
        }

        /// <summary>
        /// 🗺️ 영지 약도 — 영지 내부 구조, 취약점 파악 (Lv.15+)
        /// </summary>
        private static SpyResult ExecuteTerritoryMapMission(GuardPlaceholder spy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = GetStateOrFail(targetId);
            if (state == null) return Fail("영지 상태를 찾을 수 없습니다.", SpyMission.TerritoryMap);

            // 발각 확인
            var detectResult = TryDetect(spy, targetId, SpyMission.TerritoryMap);
            if (detectResult.HasValue) return detectResult.Value;

            // 성공: 지형/약도 정보 수집
            string terrainType = GetDifficultyTerrainName(def.difficulty);
            string approach = GetApproachPath(def.difficulty);
            string hideout = GetHideoutSpots(def.difficulty);
            string weakPoint = GetWeakPoint(def.difficulty);

            string info = $"지형: {terrainType}\n접근 경로: {approach}\n은신처: {hideout}\n취약점: {weakPoint}";

            // 플래그 저장
            state.spyReportSurvey = true;
            state.lastSpyTime = Time.time;

            return Success($"🗺️ {def.territoryName} 영지 약도 수집 성공!", SpyMission.TerritoryMap, info);
        }

        /// <summary>
        /// 💣 방해 공작 — 병사 중독, 식량 창고 파괴 등 (Lv.20+)
        /// </summary>
        private static SpyResult ExecuteSabotageMission(GuardPlaceholder spy, TerritoryId targetId, TerritoryDefinition def)
        {
            var state = GetStateOrFail(targetId);
            if (state == null) return Fail("영지 상태를 찾을 수 없습니다.", SpyMission.Sabotage);

            // 방해 공작은 발각 확률이 더 높음 (기본 40%)
            var detectResult = TryDetect(spy, targetId, SpyMission.Sabotage, extraDetectChance: 0.1f);
            if (detectResult.HasValue) return detectResult.Value;

            // 성공: 방해 공작 효과 적용
            // - 병사 중독: guardCount 일부 감소
            // - 식량 창고 파괴: 영지 호감도 하락
            int casualties = Mathf.Max(1, Mathf.FloorToInt(def.guardCount * 0.3f));
            int remainingGuards = Mathf.Max(0, def.guardCount - casualties);

            float loyaltyPenalty = 10f;
            state.loyaltyToPlayer = Mathf.Max(0, state.loyaltyToPlayer - loyaltyPenalty);

            string info = $"방해 공작 성공!\n피해 병사: {casualties}명 (잔여: {remainingGuards}명)\n영지 호감도 -{loyaltyPenalty}";

            // 플래그 저장
            state.spyReportSurvey = true; // 재사용
            state.lastSpyTime = Time.time;

            return Success($"💣 {def.territoryName} 방해 공작 성공!", SpyMission.Sabotage, info);
        }

        // ===== 헬퍼 =====

        public static string GetMissionName(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.LordInfo: return "🔍 영주 정보";
                case SpyMission.TroopInfo: return "📋 병력 정보";
                case SpyMission.TerritoryMap: return "🗺️ 영지 약도";
                case SpyMission.Sabotage: return "💣 방해 공작";
                default: return "알 수 없음";
            }
        }

        public static string GetMissionDescription(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.LordInfo: return "영주 선호 음식, 지병 파악 (Lv.5+)";
                case SpyMission.TroopInfo: return "병사 수, 레벨, 배치 파악 (Lv.10+)";
                case SpyMission.TerritoryMap: return "영지 내부 구조, 취약점 파악 (Lv.15+)";
                case SpyMission.Sabotage: return "병사 중독, 식량 창고 파괴 등 (Lv.20+)";
                default: return "";
            }
        }

        public static int GetRequiredLevel(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.LordInfo: return LORDINFO_REQUIRED_LEVEL;
                case SpyMission.TroopInfo: return TROOPINFO_REQUIRED_LEVEL;
                case SpyMission.TerritoryMap: return TERRITORYMAP_REQUIRED_LEVEL;
                case SpyMission.Sabotage: return SABOTAGE_REQUIRED_LEVEL;
                default: return 99;
            }
        }

        public static float GetDuration(SpyMission mission)
        {
            switch (mission)
            {
                case SpyMission.LordInfo: return LORDINFO_DURATION;
                case SpyMission.TroopInfo: return TROOPINFO_DURATION;
                case SpyMission.TerritoryMap: return TERRITORYMAP_DURATION;
                case SpyMission.Sabotage: return SABOTAGE_DURATION;
                default: return 0f;
            }
        }

        /// <summary>
        /// 정보원으로 파견 가능한 병사 목록 반환 (포섭 + 생존)
        /// </summary>
        public static List<GuardPlaceholder> GetAvailableSpies()
        {
            var result = new List<GuardPlaceholder>();
            var guards = Object.FindObjectsByType<GuardPlaceholder>();
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

            var def = TerritoryDatabase.Instance.GetDefinition(targetId);
            if (string.IsNullOrEmpty(def.territoryName))
                return 0.5f;

            float chance = BASE_DETECT_CHANCE;
            chance -= state.loyaltyToPlayer * LOYALTY_DETECT_REDUCTION;
            chance -= spy.Level * LEVEL_DETECT_REDUCTION;
            chance += GetDifficultyModifier(def.difficulty);
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

        private static string GetDeploymentInfo(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "정문 집중 배치";
                case TerritoryDifficulty.Ring2: return "정문 + 성벽 순찰";
                case TerritoryDifficulty.Ring3: return "다중 초소 분산 배치";
                case TerritoryDifficulty.Ring4: return "전 방위 밀집 배치";
                case TerritoryDifficulty.Empire: return "계층적 방어 체계";
                default: return "알 수 없음";
            }
        }

        private static string GetApproachPath(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "남쪽에서 접근 용이";
                case TerritoryDifficulty.Ring2: return "동쪽 숲길 우회 가능";
                case TerritoryDifficulty.Ring3: return "북서쪽 절벽 경로";
                case TerritoryDifficulty.Ring4: return "지하 통로 존재";
                case TerritoryDifficulty.Empire: return "비밀 통로 확인 필요";
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

        private static string GetWeakPoint(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "야간 경계 허술";
                case TerritoryDifficulty.Ring2: return "동쪽 담장 낮음";
                case TerritoryDifficulty.Ring3: return "서쪽 성벽 균열";
                case TerritoryDifficulty.Ring4: return "식량 비축 장소 노출";
                case TerritoryDifficulty.Empire: return "내부 분열 징후";
                default: return "확인되지 않음";
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

        // ===== 공통 헬퍼 =====

        /// <summary>
        /// 영지 상태 조회
        /// </summary>
        private static TerritoryState GetStateOrFail(TerritoryId targetId)
        {
            return TerritoryDatabase.Instance.GetState(targetId);
        }

        /// <summary>
        /// 발각 판정 및 정보원 사망 처리
        /// 발각되면 SpyResult 반환, 아니면 null 반환
        /// </summary>
        private static SpyResult? TryDetect(GuardPlaceholder spy, TerritoryId targetId, SpyMission mission, float extraDetectChance = 0f)
        {
            float detectChance = CalculateDetectChance(spy, targetId) + extraDetectChance;
            detectChance = Mathf.Clamp01(detectChance);
            bool detected = Random.value < detectChance;

            if (!detected) return null;

            // 정보원 사망 처리
            spy.TakeDamage(EXECUTION_DAMAGE, Vector3.zero, "Spy caught");
            return new SpyResult
            {
                success = false,
                message = $"💀 발각! 정보원 {spy.GuardName} 처형됨.",
                mission = mission,
                detected = true,
                spyLost = true,
                infoGathered = ""
            };
        }

        private static SpyResult Success(string msg, SpyMission mission, string infoGathered)
            => new SpyResult { success = true, message = msg, mission = mission, detected = false, spyLost = false, infoGathered = infoGathered };

        private static SpyResult Fail(string msg, SpyMission mission)
            => new SpyResult { success = false, message = msg, mission = mission, detected = false, spyLost = false, infoGathered = "" };
    }
}
