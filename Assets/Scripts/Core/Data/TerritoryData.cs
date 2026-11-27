using System;
using UnityEngine;
#pragma warning disable 0414

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
    /// (struct: 생성 후 변경 불가, 복사본 수정 버그 방지)
    /// </summary>
    [Serializable]
    public struct LordInfo
    {
        [field: SerializeField] public string lordName { get; set; }                  // 영주 이름
        [field: SerializeField] public string preferredFood { get; set; }             // 선호 음식 (null/"" = 없음)
        [field: SerializeField] public string chronicDisease { get; set; }            // 지병 (null/"" = 없음)
        [field: SerializeField, Range(0, 100)]
        public int loyalty { get; set; }                      // 충성도 (0=반역, 100=완전 충성)
        [field: SerializeField] public LordPersonality personality { get; set; }       // 성격
    }

    /// <summary>
    /// 영지 고유 식별자 — 국가 + 인덱스로 구성
    /// (struct: 생성 후 변경 불가)
    /// </summary>
    [Serializable]
    public struct TerritoryId
    {
        [field: SerializeField] public NationType nation { get; set; }
        [field: SerializeField] public int index { get; set; }  // 1~20 (Ring 1~4 각 5개씩)

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
    /// (struct: 생성 후 변경 불가, 복사본 수정 버그 방지)
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
        [field: SerializeField] public Vector3 worldPosition { get; set; }            // 월드 위치
    }

    /// <summary>
    /// 영지 전투 상태 열거형 (TerritoryBattleManager 연동)
    /// </summary>
    public enum TerritoryBattleState
    {
        /// <summary>평화 상태 — 전투 없음</summary>
        Peaceful,
        /// <summary>공격 받는 중 — 방어전 진행</summary>
        UnderAttack,
        /// <summary>후퇴 — 병사 후퇴 타이머 가동</summary>
        Retreated,
        /// <summary>증원 — 지원군 도착 대기</summary>
        Reinforcing,
        /// <summary>정복됨 — 영지 함락 완료</summary>
        Conquered
    }

    /// <summary>
    /// 병사 배치 역할 — 플레이어 소유 영지의 주둔군을 공격/수비 모드로 지정 (TerritoryDeploymentSystem 연동)
    /// </summary>
    public enum GarrisonRole
    {
        /// <summary>배치 없음 (기본)</summary>
        None,
        /// <summary>공격 배치 — 타 영지로 공격 명령 발동</summary>
        Attack,
        /// <summary>수비 배치 — 문지기/주둔군 수비 태세</summary>
        Defense
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
        [SerializeField] private bool _playerHostile = false;       // [D3-B] 마약 발각 적대 플래그 (RestaurantSystem — 발각→적대 전환 롤 성공 시 true)
        [SerializeField] private bool _lordViolent = false;         // [D4-B] 영주 폭력화 플래그 (LordViolenceSystem — 주간 세수 기준 미달 시 true, 세탁 없이 유지)
        [SerializeField] private bool _flagRaised = false;          // 국기 게양 여부
        [SerializeField] private bool _lordSurrendered = false;     // 영주 항복 여부 (C10-10)
        [SerializeField] private bool _lordDefeated = false;        // 영주 처치 여부 (C10-10)
        [SerializeField] private bool _lordExecuted = false;        // 영주 처형 여부 (C10-11)
        [SerializeField] private bool _lordSpared = false;          // 영주 살려주기 여부 (C10-11)

        // ===== [113차] 영주 선호 음식 기능확장 (A/B/C) — 선물 보너스/인텔 재료 힌트/미지급 페널티 =====
        [SerializeField] private int _lastPreferredFeedDay = -1;    // [113차] 영주 선호 음식 마지막 지급 일차 (-1 = 미지급/구세이브)

        // ===== [126차] 영주 딸 — 호감도/결혼 상태 (선례: 113차 _lastPreferredFeedDay) =====
        [SerializeField] private int _daughterAffection = 0;        // 0~100, 들꽃 선물 1개당 +10
        [SerializeField] private bool _daughterMarried = false;     // 프로포즈 수락 시 1회 설정(멱등), 이후 불변

        // ===== F2/F3: 살려준 영주 공물/배신 상태 (세이브 v5 — FLAG_TAKEOVER_PLAN F5) =====
        [SerializeField] private int _tributeWeeksPaid = 0;         // 공물 지급 누적 주차 수
        [SerializeField] private int _betrayalCount = 0;            // 배신 누적 횟수 (2회 이상 → 배신 확률 ×1.5)

        // ===== 병사 배치 (TerritoryDeploymentSystem 연동) =====
        [SerializeField] private GarrisonRole _garrisonRole = GarrisonRole.None;   // 공격/수비 배치 상태
        [SerializeField] private TerritoryId _attackTargetId;                       // 공격 목표 영지 (Attack 모드일 때)

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

        // ===== P30-A: 마약 오염도 (TerritoryDrugSystem 연동) =====
        [SerializeField] private float _drugContamination = 0f;       // 0~100, 영지 단위 마약 오염도

        // ===== Phase D: 영지 금고 (TerritoryLootSystem 연동) =====
        [SerializeField] private int _territoryGold = 0;              // 영지 금고 (점령 시 몰수 대상, 미시딩=0)

        // ===== [123차] 소유 영주 추적 (Phase 1 — 월드맵 호버 영주 툴팁 전제 데이터) =====
        [SerializeField] private string _ownerLordName = string.Empty; // 현재 소유 영주 이름 (영주↔영주 점령 시 승리 영주로 갱신)

        public TerritoryId id => _id;
        public TerritoryOwnership ownership { get => _ownership; set => _ownership = value; }

        /// <summary>
        /// 병사 생존 비율 (0~1). setter가 자동으로 0~1 범위로 클램핑합니다.
        /// </summary>
        public float guardAliveRatio
        {
            get => _guardAliveRatio;
            set => _guardAliveRatio = Mathf.Clamp01(value);
        }

        /// <summary>
        /// 플레이어에 대한 충성도 (0~100). setter가 자동으로 0~100 범위로 클램핑합니다.
        /// </summary>
        public float loyaltyToPlayer
        {
            get => _loyaltyToPlayer;
            set => _loyaltyToPlayer = Mathf.Clamp(value, 0f, 100f);
        }

        public bool isUnderAttack { get => _isUnderAttack; set => _isUnderAttack = value; }

        /// <summary>[D3-B] 마약 음식점 발각에 의한 영지 적대 플래그 — 발각 후 적대 전환 롤 성공 시 true (RestaurantSystem).</summary>
        public bool playerHostile { get => _playerHostile; set => _playerHostile = value; }

        /// <summary>[D4-B] 영주 폭력화 플래그 — 주간 세수 기준 미달 시 true (LordViolenceSystem). 세이브/세탁 없이 유지.</summary>
        public bool lordViolent { get => _lordViolent; set => _lordViolent = value; }
        public bool flagRaised { get => _flagRaised; set => _flagRaised = value; }
        public bool lordSurrendered { get => _lordSurrendered; set => _lordSurrendered = value; }
        public bool lordDefeated { get => _lordDefeated; set => _lordDefeated = value; }
        public bool lordExecuted { get => _lordExecuted; set => _lordExecuted = value; }
        public bool lordSpared { get => _lordSpared; set => _lordSpared = value; }

        /// <summary>[F5] 공물 지급 누적 주차 수 (살려준 영주 — SparedLordTributeSystem).</summary>
        public int tributeWeeksPaid { get => _tributeWeeksPaid; set => _tributeWeeksPaid = Mathf.Max(0, value); }

        /// <summary>[F5] 배신 누적 횟수 — 2회 이상 배신한 영주는 배신 확률 ×1.5 (LordBetrayalSystem).</summary>
        public int betrayalCount { get => _betrayalCount; set => _betrayalCount = Mathf.Max(0, value); }

        /// <summary>[113차] 마지막 선호 음식 지급 일차 — 미지급/구세이브는 -1 (미지급 페널티 판정 기준). -1 하한 클램프.</summary>
        public int lastPreferredFeedDay { get => _lastPreferredFeedDay; set => _lastPreferredFeedDay = Mathf.Max(-1, value); }

        /// <summary>
        /// [126차] 영주 딸 호감도 (0~100). setter가 자동으로 0~100 범위로 클램핑합니다.
        /// 들꽃 선물 1개당 +10, 100 도달 시 추가 선물은 거부(꽃 소비 없음).
        /// </summary>
        public int daughterAffection
        {
            get => _daughterAffection;
            set => _daughterAffection = Mathf.Clamp(value, 0, 100);
        }

        /// <summary>[126차] 영주 딸 결혼 여부 — 프로포즈 수락 시 1회 설정(멱등), 재호출 시 재가 없음.</summary>
        public bool daughterMarried { get => _daughterMarried; set => _daughterMarried = value; }
        public bool spyReportRecon { get => _spyReportRecon; set => _spyReportRecon = value; }
        public bool spyReportInfiltrate { get => _spyReportInfiltrate; set => _spyReportInfiltrate = value; }
        public bool spyReportSurvey { get => _spyReportSurvey; set => _spyReportSurvey = value; }
        public float lastSpyTime { get => _lastSpyTime; set => _lastSpyTime = value; }
        public TerritoryBattleState battleState { get => _battleState; set => _battleState = value; }
        public float retreatTimer { get => _retreatTimer; set => _retreatTimer = value; }
        public float reinforceTimer { get => _reinforceTimer; set => _reinforceTimer = value; }
        public int reinforcedCount { get => _reinforcedCount; set => _reinforcedCount = value; }
        public int deadGuardCount { get => _deadGuardCount; set => _deadGuardCount = value; }
        public int totalGuardCount { get => _totalGuardCount; set => _totalGuardCount = value; }
        public bool isActive { get => _isActive; set => _isActive = value; }

        /// <summary>
        /// 영지 단위 마약 오염도 (0~100). setter가 자동으로 0~100 범위로 클램핑합니다.
        /// TerritoryDrugSystem이 가산하며, 병사/영주 중독 연동의 기준값입니다.
        /// </summary>
        public float drugContamination
        {
            get => _drugContamination;
            set => _drugContamination = Mathf.Clamp(value, 0f, 100f);
        }

        /// <summary>
        /// [Phase D] 영지 금고 (0 이상). TerritoryLootSystem이 난이도 기반으로 시딩하며,
        /// 플레이어 점령 시 몰수 후 0으로 초기화됩니다. setter가 자동으로 0 이상으로 클램핑합니다.
        /// </summary>
        public int territoryGold
        {
            get => _territoryGold;
            set => _territoryGold = Mathf.Max(0, value);
        }

        /// <summary>
        /// [123차] 현재 소유 영주 이름 — 영주↔영주 점령(AIWarSystem/TerritoryWarManager)에서
        /// 승리 영주로 갱신되고, 배신(LordBetrayalSystem)·뉴게임+ 리셋에서 원래 영주로 복귀합니다.
        /// 초기값은 TerritoryDatabase.InitializeStates()가 def.lord.lordName으로 결정론 매핑합니다.
        /// PlayerOwned(플레이어 점령) 시에는 툴팁이 "플레이어"로 표기하므로 저장·조회 불필요.
        /// </summary>
        public string ownerLordName
        {
            get => _ownerLordName;
            set => _ownerLordName = value ?? string.Empty;
        }

        // ===== 병사 배치 프로퍼티 (TerritoryDeploymentSystem 연동) =====
        /// <summary>현재 배치 역할 (None/Attack/Defense)</summary>
        public GarrisonRole garrisonRole { get => _garrisonRole; set => _garrisonRole = value; }
        /// <summary>공격 목표 영지 (Attack 모드일 때만 유효)</summary>
        public TerritoryId attackTargetId { get => _attackTargetId; set => _attackTargetId = value; }

        /// <summary>
        /// Unity 역직렬화용 매개변수 없는 생성자.
        /// 필드 초기값은 인라인 선언에 의해 설정됩니다.
        /// </summary>
        public TerritoryState()
        {
        }

        public TerritoryState(TerritoryId id)
        {
            _id = id;
        }
    }
}
