using System;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 영주 병력 배분 계획 — 공격 파견 / 문지기 / 실내 수비 3개 역할로 병력을 나눈 결과.
    /// (struct: 생성 후 변경 불가, 복사본 수정 버그 방지)
    /// </summary>
    [Serializable]
    public struct LordDeploymentPlan
    {
        /// <summary>공격 파견 병력 (타 영지 공격 등 외부 작전용)</summary>
        public int attackSoldiers;
        /// <summary>문지기 병력 (성문 경비)</summary>
        public int gatekeeperSoldiers;
        /// <summary>실내 수비 병력 (영주 호위/내부 방어)</summary>
        public int interiorDefenseSoldiers;

        /// <summary>전체 배분 병력 합계 (파생값 — 세 역할의 합으로 항상 계산됨)</summary>
        public int totalSoldiers => attackSoldiers + gatekeeperSoldiers + interiorDefenseSoldiers;

        public override string ToString()
        {
            return $"Attack {attackSoldiers} / Gate {gatekeeperSoldiers} / Interior {interiorDefenseSoldiers} (Total {totalSoldiers})";
        }
    }

    /// <summary>
    /// 영주 성향 파라미터 시스템 (Phase A).
    /// LordInfo.personality (LordPersonality 7종)를 병력 배분 수치로 변환하는 순수 로직 static 클래스.
    ///
    /// - aggression A ∈ [0,1]: 공격 성향 수치 (높을수록 공격적)
    /// - defensiveness D = 1 - A: 수비 성향 수치
    /// - 성향은 TerritoryDatabase가 결정 시드로 배정하므로 같은 영지는 항상 같은 배분이 나옴 (결정론 보장)
    /// - 배분 계산 내부에 System.Random 사용 금지 (결정론 재현)
    ///
    /// 후속 Phase(B/C/D)에서 TerritoryWarManager / AIWarSystem / WarMarchSimulation 이 소비할 예정.
    /// </summary>
    public static class LordPersonalitySystem
    {
        // ===== 배분 계수 =====

        /// <summary>공격성 강조 계수 — 공격 파견 = round(total × A × 이 값)</summary>
        private const float AttackEmphasis = 0.6f;

        /// <summary>문지기 최소 가중치 (A=1, 가장 공격적인 영주)</summary>
        private const float GatekeeperWeightMin = 0.25f;

        /// <summary>문지기 가중치 방어 보너스 폭 — gatekeeperWeight = 0.25 + (1 - A) × 0.35</summary>
        private const float GatekeeperDefensiveBonus = 0.35f;

        // ===== 성향 → 수치 매핑 =====

        /// <summary>
        /// 성향별 공격성 수치 (0=완전 수비, 1=완전 공격).
        /// enum 전체를 스위치로 명시하며, 알 수 없는 값은 Neutral(0.5)로 처리.
        /// </summary>
        private static float Aggression(LordPersonality personality)
        {
            switch (personality)
            {
                case LordPersonality.Brave:      return 0.80f; // 용감함 — 정면 공격 선호
                case LordPersonality.Cruel:      return 0.90f; // 잔인함 — 가장 공격적
                case LordPersonality.Greedy:     return 0.60f; // 탐욕스러움 — 약탈 지향
                case LordPersonality.Neutral:    return 0.50f; // 보통
                case LordPersonality.Wise:       return 0.50f; // 현명함 — 신중하지만 소극적이진 않음
                case LordPersonality.Suspicious: return 0.30f; // 의심 많음 — 수비 균형
                case LordPersonality.Cowardly:   return 0.15f; // 겁많음 — 가장 수비적
                default:                         return 0.50f; // fallback = Neutral
            }
        }

        /// <summary>
        /// 영지 영주의 공격성 수치 (0~1).
        /// TerritoryDatabase의 결정론적 성향 배정을 기반으로 하므로 항상 같은 값이 반환됨.
        /// </summary>
        public static float GetAggression(TerritoryId id)
        {
            TerritoryDefinition definition = TerritoryDatabase.Instance.GetDefinition(id);
            return Aggression(definition.lord.personality);
        }

        /// <summary>
        /// 영지 영주의 수비성 수치 (0~1) = 1 - 공격성.
        /// </summary>
        public static float GetDefensiveness(TerritoryId id)
        {
            return 1f - Mathf.Clamp01(GetAggression(id));
        }

        // ===== 배분 계획 조회 =====

        /// <summary>
        /// 영지의 현재 병력 기준 배분 계획.
        /// 총 병력 = 정의의 guardCount × state.guardAliveRatio (기본 1) 를 반올림한 값.
        /// </summary>
        public static LordDeploymentPlan GetDeploymentPlan(TerritoryId id)
        {
            TerritoryDatabase db = TerritoryDatabase.Instance;
            TerritoryDefinition definition = db.GetDefinition(id);
            TerritoryState state = db.GetState(id);

            float aliveRatio = state != null ? state.guardAliveRatio : 1f;
            int totalGuardCount = RoundHalfUp(definition.guardCount * Mathf.Clamp01(aliveRatio));

            return GetDeploymentPlan(id, totalGuardCount);
        }

        /// <summary>
        /// 영지 + 총 병력 지정 배분 계획. 영주 성향은 TerritoryDatabase에서 조회.
        /// </summary>
        public static LordDeploymentPlan GetDeploymentPlan(TerritoryId id, int totalGuardCount)
        {
            float aggression = GetAggression(id);
            return BuildPlan(aggression, totalGuardCount);
        }

        /// <summary>
        /// 성향 + 총 병력 직접 지정 배분 계획 (오버로드 — DB 조회 없이 순수 계산).
        /// </summary>
        public static LordDeploymentPlan GetDeploymentPlan(LordPersonality personality, int totalGuardCount)
        {
            return BuildPlan(Aggression(personality), totalGuardCount);
        }

        // ===== 내부 계산 (결정론 — Random 사용 금지) =====

        /// <summary>
        /// 공격성 수치로 3역할(공격/문지기/실내 수비) 병력 배분을 계산합니다.
        ///
        /// 규칙:
        /// 1. 공격 파견 = round(total × A × 0.6)  — 공격성을 60% 강조
        /// 2. 문지기 = round(total × (0.25 + (1 - A) × 0.35))  — 공격적일수록 문지기 감소
        /// 3. 실내 수비 = total - 공격 - 문지기  (0 미만 클램프, 초과분은 실내에서 차감)
        /// 4. total ≥ 3이면 세 역할 모두 최소 1명 보장 (합계 total 불변, 최대 풀에서 차감)
        /// 5. total == 0이면 전부 0
        /// </summary>
        private static LordDeploymentPlan BuildPlan(float aggression, int totalGuardCount)
        {
            int total = totalGuardCount > 0 ? totalGuardCount : 0;
            if (total == 0)
                return default; // 병력 없음 — 전부 0

            float a = Mathf.Clamp01(aggression);

            // 1) 공격 파견: 공격성 60% 강조
            int attack = RoundHalfUp(total * a * AttackEmphasis);
            if (attack > total)
                attack = total;

            // 2) 문지기: 공격적일수록 문지기 가중치 감소 (A=0.9 → 0.285, A=0.15 → 0.5475)
            float gatekeeperWeight = GatekeeperWeightMin + (1f - a) * GatekeeperDefensiveBonus;
            int gatekeeper = RoundHalfUp(total * gatekeeperWeight);
            if (gatekeeper > total - attack)
                gatekeeper = total - attack;

            // 3) 실내 수비: 잔여 병력 전부 (합계 == total 보장)
            int interior = total - attack - gatekeeper;

            // 4) 최소 1명 보정 — total ≥ 3이면 세 역할 모두 0이 안 되도록
            //    (0인 역할을 1로 올리고, 가장 큰 풀에서 1 차감해 합계 total 유지)
            if (total >= 3)
            {
                int[] pools = { attack, gatekeeper, interior };
                for (int i = 0; i < pools.Length; i++)
                {
                    if (pools[i] >= 1)
                        continue;

                    pools[i] = 1;

                    // 가장 큰 풀에서 1 차감 (합계 보존)
                    int largest = 0;
                    for (int j = 1; j < pools.Length; j++)
                    {
                        if (pools[j] > pools[largest])
                            largest = j;
                    }

                    if (pools[largest] > 1)
                    {
                        pools[largest] -= 1;
                    }
                    else
                    {
                        // 방어 코드: total ≥ 3인데 차감 불가 — 보정 되돌림 (합계 보존 우선)
                        pools[i] = 0;
                    }
                }

                attack = pools[0];
                gatekeeper = pools[1];
                interior = pools[2];
            }

            return new LordDeploymentPlan
            {
                attackSoldiers = attack,
                gatekeeperSoldiers = gatekeeper,
                interiorDefenseSoldiers = interior
            };
        }

        /// <summary>
        /// 양수 반올림 (반올림 내림 0.5 이상 올림 — 결정론적, 한국어 "반올림" 규약).
        /// Mathf.Round/MidpointRounding.ToEven 의 banker's rounding 혼동을 피하기 위해 별도 구현.
        /// 음수 입력은 0으로 처리 (배분 수치는 항상 비음수).
        /// </summary>
        private static int RoundHalfUp(float value)
        {
            if (value <= 0f)
                return 0;
            return (int)Mathf.Floor(value + 0.5f);
        }
    }
}
