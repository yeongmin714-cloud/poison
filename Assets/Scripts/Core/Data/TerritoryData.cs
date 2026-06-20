using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 4개 국가 + 황제국 열거형
    /// </summary>
    public enum NationType
    {
        None,     // 미소속
        East,     // 🏁 동 (East) — 파랑
        West,     // 🏁 서 (West) — 초록
        South,    // 🏁 남 (South) — 빨강
        North,    // 🏁 북 (North) — 보라
        Empire,   // 🏁 황제국 (Empire) — 보라+금
        Dracula   // 🧛 드라큘라 (Night Dracula) — 검정+빨강
    }

    /// <summary>
    /// 영지 난이도 (1~5성, 방사형 구조와 연동)
    /// </summary>
    public enum TerritoryDifficulty
    {
        Ring1,   // 🟢 최외곽 — 쉬움
        Ring2,   // 🟡 중간 바깥 — 보통
        Ring3,   // 🟠 중간 안쪽 — 어려움
        Ring4,   // 🔴 황제국 인접 — 매우 어려움
        Empire   // 👑 황제국 — 최종
    }

    /// <summary>
    /// 영지 소유 상태
    /// </summary>
    public enum TerritoryOwnership
    {
        Unoccupied,            // 미점령 (초기 상태)
        PlayerOwned,           // 플레이어 소유
        LordOwned,             // AI 영주 소유
        Contested              // 전쟁 중
    }

    /// <summary>
    /// 영주 성격
    /// </summary>
    public enum LordPersonality
    {
        Neutral,     // 보통
        Greedy,      // 탐욕스러움
        Suspicious,  // 의심 많음
        Brave,       // 용감함
        Cowardly,    // 겁많음
        Wise,        // 현명함
        Cruel        // 잔인함
    }

    /// <summary>
    /// 영주 정보 — 선호 음식, 지병, 충성심, 성격
    /// </summary>
    [Serializable]
    public struct LordInfo
    {
        public string lordName;                  // 영주 이름
        public string preferredFood;             // 선호 음식
        public string chronicDisease;            // 지병 (null/"" = 없음)
        [Range(0, 100)]
        public int loyalty;                      // 충성심 (0=반역, 100=완전 충성)
        public LordPersonality personality;       // 성격
    }

    /// <summary>
    /// 영지 고유 식별자 — 국가 + 인덱스로 구성
    /// </summary>
    [Serializable]
    public struct TerritoryId
    {
        public NationType nation;
        public int index;  // 1~20 (Ring 1~4 각 5개씩)

        public TerritoryId(NationType nation, int index)
        {
            this.nation = nation;
            this.index = index;
        }

        public override string ToString()
        {
            return $"{nation}_{index:D2}";
        }
    }

    /// <summary>
    /// 영지 정의 데이터 (변하지 않는 설계 정보)
    /// Resources에서 로드하거나 코드에서 정의
    /// </summary>
    [Serializable]
    public struct TerritoryDefinition
    {
        public TerritoryId id;
        public string territoryName;             // 영지 이름 (예: "동쪽 초원지대")
        public NationType nation;                // 소속 국가
        public TerritoryDifficulty difficulty;    // 난이도 링
        public int guardCount;                   // 병사 수
        public LordInfo lord;                    // 영주 정보
        public string description;               // 영지 설명
        public bool isNightOnly;                 // 야간에만 활성화되는 영지 (ND-01)
    }

    /// <summary>
    /// 영지 전투 상태 열거형 (TerritoryBattleManager 연동)
    /// </summary>
    public enum TerritoryBattleState
    {
        Peaceful,
        UnderAttack,
        Retreated,
        Reinforcing,
        Conquered
    }

    /// <summary>
    /// 영지 런타임 상태 — 게임 진행 중 변경되는 데이터
    /// </summary>
    [Serializable]
    public class TerritoryState
    {
        public TerritoryId id;
        public TerritoryOwnership ownership = TerritoryOwnership.Unoccupied;
        public float guardAliveRatio = 1f;       // 0~1, 전투로 인한 병사 손실 반영
        public float loyaltyToPlayer = 0f;       // 0~100, 점령 후 주민/병사의 플레이어 충성도
        public bool isUnderAttack = false;       // 전쟁 중 플래그
        public bool flagRaised = false;          // 국기 게양 여부
        public bool lordSurrendered = false;     // 영주 항복 여부 (C10-10)
        public bool lordDefeated = false;        // 영주 처치 여부 (C10-10)
        public bool lordExecuted = false;        // 영주 처형 여부 (C10-11)
        public bool lordSpared = false;          // 영주 살려주기 여부 (C10-11)

        // ===== 정보원 수집 플래그 (SpySystem 연동) =====
        public bool spyReportRecon = false;       // 정찰 정보 수집 완료
        public bool spyReportInfiltrate = false;  // 잠입 정보 수집 완료
        public bool spyReportSurvey = false;      // 측량 정보 수집 완료
        public float lastSpyTime = 0f;            // 마지막 정보 수집 시간

        // ===== 전투 상태 (TerritoryBattleManager 연동) =====
        public TerritoryBattleState battleState = TerritoryBattleState.Peaceful;
        public float retreatTimer = 0f;
        public float reinforceTimer = 0f;
        public int reinforcedCount = 0;
        public int deadGuardCount = 0;
        public int totalGuardCount = 0;

        // ===== 야간 전용 영지 (ND-01) =====
        public bool isActive = true;               // 낮에 비활성화되는 영지용 (ND-01)

        public TerritoryState(TerritoryId id)
        {
            this.id = id;
        }
    }
}