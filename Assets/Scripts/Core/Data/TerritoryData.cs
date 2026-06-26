using System;
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
        [field: SerializeField] public string lordName { get; set; }                  // 영주 이름
        [field: SerializeField] public string preferredFood { get; set; }             // 선호 음식
        [field: SerializeField] public string chronicDisease { get; set; }            // 지병 (null/"" = 없음)
        [field: SerializeField, Range(0, 100)]
        public int loyalty { get; set; }                      // 충성도 (0=반역, 100=완전 충성)
        [field: SerializeField] public LordPersonality personality { get; set; }       // 성격
    }

    /// <summary>
    /// 영지 고유 식별자 — 국가 + 인덱스로 구성
    /// </summary>
    [Serializable]
    public struct TerritoryId
    {
        [field: SerializeField] public NationType nation { get; private set; }
        [field: SerializeField] public int index { get; private set; }  // 1~20 (Ring 1~4 각 5개씩)

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
        [field: SerializeField] public TerritoryId id { get; set; }
        [field: SerializeField] public string territoryName { get; set; }             // 영지 이름 (예: "동쪽 초원지대")
        [field: SerializeField] public NationType nation { get; set; }                // 소속 국가
        [field: SerializeField] public TerritoryDifficulty difficulty { get; set; }    // 난이도 링
        [field: SerializeField] public int guardCount { get; set; }                   // 병사 수
        [field: SerializeField] public LordInfo lord { get; set; }                    // 영주 정보
        [field: SerializeField] public string description { get; set; }               // 영지 설명
        [field: SerializeField] public bool isNightOnly { get; set; }                 // 야간에만 활성화되는 영지 (ND-01)
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
        [SerializeField] private TerritoryId _id;
        [SerializeField] private TerritoryOwnership _ownership = TerritoryOwnership.Unoccupied;
        [SerializeField] private float _guardAliveRatio = 1f;       // 0~1, 전투로 인한 병사 손실 반영
        [SerializeField] private float _loyaltyToPlayer = 0f;       // 0~100, 점령 후 주민/병사의 플레이어 충성도
        [SerializeField] private bool _isUnderAttack = false;       // 전쟁 중 플래그
        [SerializeField] private bool _flagRaised = false;          // 국기 게양 여부
        [SerializeField] private bool _lordSurrendered = false;     // 영주 항복 여부 (C10-10)
        [SerializeField] private bool _lordDefeated = false;        // 영주 처치 여부 (C10-10)
        [SerializeField] private bool _lordExecuted = false;        // 영주 처형 여부 (C10-11)
        [SerializeField] private bool _lordSpared = false;          // 영주 살려주기 여부 (C10-11)

        // ===== 정보원 수집 플래그 (SpySystem 연동) =====
        [SerializeField] private bool _spyReportRecon = false;       // 정찰 정보 수집 완료
        [SerializeField] private bool _spyReportInfiltrate = false;  // 잠입 정보 수집 완료
        [SerializeField] private bool _spyReportSurvey = false;      // 측량 정보 수집 완료
        [SerializeField] private float _lastSpyTime = 0f;            // 마지막 정보 수집 시간

        // ===== 전투 상태 (TerritoryBattleManager 연동) =====
        [SerializeField] private TerritoryBattleState _battleState = TerritoryBattleState.Peaceful;
        [SerializeField] private float _retreatTimer = 0f;
        [SerializeField] private float _reinforceTimer = 0f;
        [SerializeField] private int _reinforcedCount = 0;
        [SerializeField] private int _deadGuardCount = 0;
        [SerializeField] private int _totalGuardCount = 0;

        // ===== 야간 전용 영지 (ND-01) =====
        [SerializeField] private bool _isActive = true;               // 낮에 비활성화되는 영지용 (ND-01)

        public TerritoryId id => _id;
        public TerritoryOwnership ownership { get => _ownership; public set => _ownership = value; }
        public float guardAliveRatio { get => _guardAliveRatio; public set => _guardAliveRatio = value; }
        public float loyaltyToPlayer { get => _loyaltyToPlayer; public set => _loyaltyToPlayer = value; }
        public bool isUnderAttack { get => _isUnderAttack; public set => _isUnderAttack = value; }
        public bool flagRaised { get => _flagRaised; public set => _flagRaised = value; }
        public bool lordSurrendered { get => _lordSurrendered; public set => _lordSurrendered = value; }
        public bool lordDefeated { get => _lordDefeated; public set => _lordDefeated = value; }
        public bool lordExecuted { get => _lordExecuted; public set => _lordExecuted = value; }
        public bool lordSpared { get => _lordSpared; public set => _lordSpared = value; }
        public bool spyReportRecon { get => _spyReportRecon; public set => _spyReportRecon = value; }
        public bool spyReportInfiltrate { get => _spyReportInfiltrate; public set => _spyReportInfiltrate = value; }
        public bool spyReportSurvey { get => _spyReportSurvey; public set => _spyReportSurvey = value; }
        public float lastSpyTime { get => _lastSpyTime; public set => _lastSpyTime = value; }
        public TerritoryBattleState battleState { get => _battleState; public set => _battleState = value; }
        public float retreatTimer { get => _retreatTimer; public set => _retreatTimer = value; }
        public float reinforceTimer { get => _reinforceTimer; public set => _reinforceTimer = value; }
        public int reinforcedCount { get => _reinforcedCount; public set => _reinforcedCount = value; }
        public int deadGuardCount { get => _deadGuardCount; public set => _deadGuardCount = value; }
        public int totalGuardCount { get => _totalGuardCount; public set => _totalGuardCount = value; }
        public bool isActive { get => _isActive; public set => _isActive = value; }

        public TerritoryState(TerritoryId id)
        {
            _id = id;
        }
    }
}