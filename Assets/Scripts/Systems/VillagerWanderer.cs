using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [87차] 마을 주민 배회 — 플레이어형 이동/애니.
    ///  · 이동: 마을 반경×0.82 내 랜덤 목표로 보행(선희 보간), 도착 후 휴식(1.5~5.5s) 반복.
    ///  · 애니: [89차] Player_AC Speed 파라미터 계약 — >0.55 Walk 진입 / <0.35 Idle 복귀
    ///    (값 _walking?1f:0f 는 계약 충족 유지. 플레이어/병사 드라이버 동일 트리거 체계와 병행).
    ///  · 접지: TerrainGenerator(GetHeightAt+1) — VillageNpcSpawner/VillageBuilder 동일 수식,
    ///    스폰 시 기록한 footOffset을 유지해 스케일 캘리브레이션 후에도 발밑이 지면에 붙는다.
    ///  · 결정론: 목표/휴식은 System.Random(시드=마을+주민 해시) — 동일 씬 재실행 시 동일 궤적.
    ///  · [89차] 대화 정지 API: PauseForDialogue(배회 정지+Speed 0 즉시) / ResumeAfterDialogue(휴식 카운터 재시작)
    ///    — NPCAmbientDialogue.BeginDialoguePose/EndDialoguePose 경유 호출(F키 대화 자세).
    ///  · [92차] 모델별 보폭-속도 정합 — SetWalkModel(스폰 경로 모델키)로 walkSpeed 테이블 적용
    ///    (성인/노인=1.4 기존, girl 계열=1.25 보정). Speed 파라미터 계약(>0.55 Walk / <0.35 Idle) 무변경.
    ///  · [92차 후속 F-D] 이동 직전 전방 레이캐스트 회피(0.25s 주기 캐시) — 건물/주민 관통 완화
    ///    (transform 직접 이동은 충돌을 무시 — 마을씬 테스트 3 포렌식 5~6번 타일 실측).
    ///  · [93차 F3] 애니 폴리시 — ①스트라이드-속도 자동 정합(첫 Walk 진입 캘리브레이션 1회 — 접지 발
    ///    슬립 실측 → anim.speed = 목표속도/실측속도, 실패 시 고정값 유지) ②선회 보간 완화(회전속도
    ///    상한 360°/s 상수 + 계수 240→120°/s — 방향 전환 즉시 회전 체감 수리).
    ///  · [96차 F4] 스트라이드 폴백 재시도 — 폴백(샘플 부족 등)은 영구 잠금이 아니라 다음 걷기 사이클마다
    ///    쿨다운 경유 재시도(성공할 때까지 — 94차 girl2 샘플 0/girl3 샘플 2 영구 폴백 잔존 수리).
    ///  · [100차 F-3] 보행 정합 재설계 — 캘리브레이션 방향 반전(속도를 애니에 맞춤): 실측 성공 시
    ///    measuredSpeed(클립 유효 보행속도)를 walkSpeed에 채택 + animSpeed 1.0 고정 회귀(배속 폐지 —
    ///    StrideAnimSpeedMax 남용 제거). 모델별 캐시 — 세션당 1회 실측, 대화 정지→재개 재실행 시 기존
    ///    보정치 재사용. 이동 로직(forward×walkSpeed×dt)/Speed 플립 계약 무변경.
    ///  · [101차 F-3] 모델별 보행 베이크 — 실측 확정 모델(girl1~3/man1~2/oldman1~2) walkSpeed 상수 테이블
    ///    우선(첫 스폰부터 정합 — 캘리브레이션 대기 시간 제거) + 잔존 슬립 >0.1m/s 모델 animSpeed 미세 트림
    ///    (0.9~1.1 클램프). 캘리브레이션은 테이블 미지 모델(예: shop_npc)의 폴백으로만 유지.
    ///  · [104차 F3] 루트↔가시 신체 접지 보상(LateUpdate) — 애니메이터(클립 비리타겟 골격루트 커브/휴머노이드
    ///    리타겟)가 골격 루트를 과도 구동해도 발 본이 루트 기준 바인드 오프셋 부근에 유지된다. 근거(실측):
    ///    [103차 P1] 스폰 스냅 정합(Δ≈-0.3) → 첫 Idle 평가 후 발 본 +3.5~4.1m 부상 → [99차 F-D] 배회 스냅이
    ///    루트를 지하 -3.5m 침강(루트↔가시 3.7m 괴리 — 콜라이더/회피레이/E키 판정 지하화). 보상으로 스냅
    ///    대상(루트)과 가시 신체 일관 — SnapToGroundRay shift ≤ 0.5m 수렴(SnapToGroundRay/P1 로그 무변경).
    ///  · [104차 F2] 대화 중 yaw 제어권 위임 게이트 — DialogueCameraDirector.Begin(fullBody)가
    ///    DialogueYawControlled=true / End=false(멱등). 게이트 중 배회 회전 분기 무기입(시선 = Director 단일
    ///    제어). _paused 정지와 이중 안전 — [94차 F4] F키 정지 경로와 간섭 없음.
    ///  · [133차-P3] 밤 귀가 스케줄 — TimeManager.OnNightStart/OnDayStart 이벤트 기반(IsDay/IsNight 계약,
    ///    L154~155). 밤 진입: 웨이포인트 순환 중단 → 스폰 앵커(홈) 복귀 보행(기존 Walk 경로 재사용 —
    ///    ApplyWalkPhaseOnce/스트라이드/프리징 셀프체크 파이프 그대로, 신규 애니 경로 없음) → 도착 후 정지 홀드.
    ///    낮 복귀: 기존 배회 재개(Speed 파라미터 1/0 플립 계약 무변경 — Walk 자동 재개). 진행 중 방문 목표는
    ///    VisitArrived 정상 발화 후 이행(지연 보고 — RestaurantVisitorSystem 기존 계약 무변경).
    ///  · [133차-P3] 주민 교차 정지(대화 흉내) — 두 주민 1.8m 조우 시 2.5초 정지+서로 대면(루트 yaw Slerp).
    ///    NPCAmbientDialogue와 역할 분담: 플레이어 대화 = 말풍선 대사(E/F키 경로 — 기존 계약), 주민-주민 =
    ///    무언 정지+대면만(말풍선 스프라이트 부재 — 프로젝트 실측). 같은 쌍 재발 쿨다운 8초(정적 딕셔너리).
    /// </summary>
    public class VillagerWanderer : MonoBehaviour
    {
        private const int TerrainSeed = 42;      // VillageNpcSpawner/VillageBuilder 동일 시드 계약
        private const float GroundBase = 1f;

        // [92차] 모델별 보폭-속도 테이블(m/s) → [100차 F-3] 보행 정합 재설계로 하향. 근거: 마을씬 테스트 11
        //   스트라이드 실측 — 슬립 0.34~0.52m/s, 클립 유효 보행속도(measuredSpeed) 0.81~1.05m/s vs 구 루트
        //   walkSpeed 1.4(성인)/1.25(girl) → 루트가 클립 자연 보폭보다 40~60% 빠른 문워크. 초기값 = 클립
        //   유효 보행속도 부근(성인 0.95/girl 0.85 — 영상 재실측 후 확정치), 캘리브레이션 성공 시
        //   measuredSpeed를 walkSpeed에 채택해 재조정한다(animSpeed 배속 폐지 — [100차 F-3]).
        private const float WalkSpeedDefault = 0.95f;
        private const float WalkSpeedGirl = 0.85f;

        // [128-2 R] 러너 — run 클립 장착 주민의 보행속도. 초기 2.0 + 캘리브레이션 채택 상한 2.6(러너 한정 —
        // StrideAdoptWalkSpeedMax 1.5는 보행 계약 유지). Speed 파라미터 임계(>0.55 Walk/<0.35 Idle) 무변경.
        private const float RunnerWalkSpeed = 2.0f;
        private const float RunnerAdoptWalkSpeedMax = 2.6f;

        // [101차 F-3] 모델별 보행 베이크 상수 테이블(m/s) — 테스트11/12 실측 확정치([100차 F-3] 캘리브레이션
        //   채택치의 최신 실측 고정). 첫 스폰부터 정합 — 캘리브레이션 대기 시간 제거(캘리브레이션은 테이블
        //   미지 모델의 폴백으로만 유지). 키 = VillageNpcSpawner 모델키 계약(man1/man2/girl1~3/oldman1~2).
        private const float BakeWalkSpeedGirl1 = 0.80f;
        private const float BakeWalkSpeedGirl2 = 0.85f;
        private const float BakeWalkSpeedGirl3 = 0.97f;
        private const float BakeWalkSpeedMan1 = 1.10f;
        private const float BakeWalkSpeedMan2 = 1.11f;
        private const float BakeWalkSpeedOldman1 = 1.06f;
        private const float BakeWalkSpeedOldman2 = 1.07f;
        private static readonly System.Collections.Generic.Dictionary<string, float> ModelWalkBakeTable =
            new System.Collections.Generic.Dictionary<string, float>
            {
                { "girl1", BakeWalkSpeedGirl1 }, { "girl2", BakeWalkSpeedGirl2 }, { "girl3", BakeWalkSpeedGirl3 },
                { "man1", BakeWalkSpeedMan1 }, { "man2", BakeWalkSpeedMan2 },
                { "oldman1", BakeWalkSpeedOldman1 }, { "oldman2", BakeWalkSpeedOldman2 },
            };
        // [101차 F-3] 잔존 슬립 >0.1m/s 모델 한정 animSpeed 미세 트림(0.9~1.1 클램프) — 베이크 후 캘리브레이션
        //   스킵. 근거: [100차 F-3] 채택 후에도 슬립 ±0.05~0.19m/s 잔존(테스트12 — man2 −0.19 = 애니 빠름).
        //   산식: trim = walkSpeed / (walkSpeed − 슬립) — man2 = 1.11/(1.11+0.19) ≈ 0.854 → 하한 클램프 0.9.
        //   게이트(|슬립| ≤ 0.1) 미달 모델은 트림 없음(animSpeed 1.0 유지 — [100차 F-3] 1.0 고정 회귀 승계).
        private const float ResidualSlipTrimGate = 0.1f;   // 트림 개입 게이트(m/s) — |잔존 슬립| 초과 모델만
        private const float AnimSpeedTrimMin = 0.9f;       // animSpeed 트림 하한 클램프(스펙 지정)
        private const float AnimSpeedTrimMax = 1.1f;       // animSpeed 트림 상한 클램프(스펙 지정)
        // 잔존 슬립 실측치(m/s — [100차 F-3] 채택 후 테스트12 잔존 실측, 게이트 초과 모델만 수록 — 최신 실측 우선 확장).
        private static readonly System.Collections.Generic.Dictionary<string, float> ModelResidualSlipTable =
            new System.Collections.Generic.Dictionary<string, float>
            {
                { "man2", -0.19f },
            };

        // [92차 후속 F-D] 이동 직전 전방 레이캐스트 회피 — VillagerWanderer는 transform 직접 이동(충돌 무시)이라
        //   건물/주민 관통(마을씬 테스트 3 포렌식 — 5~6번 타일 실측). 이동 직전 흉부 라인(0.9m)에서 전방 1.2m
        //   레이캐스트로 장애물을 감지하고 목표 방향을 재선정한다. 레이캐스트 비용 최소화 — 매 프레임 폐기,
        //   0.25s 주기 캐시(AvoidRayInterval).
        private const float AvoidRayInterval = 0.25f;   // 회피 판정 주기(초) — 매 프레임 대신 캐시
        private const float AvoidRayHeight = 0.9f;      // 레이 원점 높이(m) — 흉부 라인
        private const float AvoidRayDistance = 1.2f;    // 회피 판정 전방 거리(m)
        // ── [133차-P3] 밤 귀가 스케줄 파라미터 ──
        private const float NightIdleHoldSeconds = 30f;       // 홈 도착 정지 홀드 충전 단위(초) — 낮 복귀 이벤트까지 반복 갱신
        private const float NightStateCheckInterval = 2f;     // 이벤트 누락 자가정합 폴링 주기(초) — 이벤트가 주 경로
        private const float HomeArrivalDist = 0.35f;          // 홈 도착 판정(m) — 기존 이동 도착 계약과 동일값
        private Vector3 _homePoint;         // 스폰 앵커(홈) — Init 시점 위치(스포너 결정론 배치 지점)
        private bool _nightMode;            // 밤 시간표 활성 — 배회 중단/홈 복귀/정지 홀드
        private bool _nightReturnWalking;   // 홈 복귀 보행 진행 중(개시 게이트 — 중복 방지)
        private float _nightStateCheckLeft; // 자가정합 폴링 카운터
        private bool _timeEventsHooked;     // TimeManager 이벤트 구독 플래그(멱등 게이트)

        // ── [133차-P3] 주민 교차 정지(대화 흉내) 파라미터 ──
        private const float CrossStopTriggerDist = 1.8f;      // 조우 판정 거리(m) — 스펙 지정
        private const float CrossStopDurationSeconds = 2.5f;  // 정지 지속(초) — 스펙 지정
        private const float CrossPairCooldownSeconds = 8f;    // 같은 쌍 재발 쿨다운(초) — 정적 딕셔너리 캐시
        private const int CrossScanFrameInterval = 30;        // 스캔 스로틀(프레임 — 0.5s@60fps, 프레임마다 전체 N² 비교 금지 계약)
        private const float CrossTurnSlerpRate = 6f;          // 대면 회전 Slerp 계수(부드러운 선회 — 격자 회전 없음)
        private const int CrossPairCooldownCap = 256;         // 쿨다운 캐시 크기 방어(만료 정리 트리거)
        // 장애물 레이어 — ConstructionManager/FacilityPropBuilder 배치 판정은 데이터 기반으로 물리 레이어를
        //   쓰지 않는다(grep 실측 — 레이어 마스크 부재) → Default(0) 건물 레이어 고정. 주민 캡슐/플레이어도
        //   Default라 주민 간·주민-플레이어 관통 완화에 동일 적용된다.
        private const int ObstacleMask = 1 << 0;

        // ── [93차 F3] 스트라이드-속도 자동 정합(첫 Walk 진입 캘리브레이션 1회) 파라미터 ──
        // 방법(선택 근거 명시): 첫 Walk 구간에서 "접지국면 발 미끄러짐"을 직접 실측한다. 접지 중 발은
        // 세계에서 정지해야 하므로 발 본의 전방 성분 속도 = 루트 속도 − 클립 유효 보행 속도(슬립) →
        // [100차 F-3] 캘리브레이션 방향 반전: 실측속도(measuredSpeed = _walkSpeed − 슬립 평균 = 클립 유효
        // 보행 속도)를 walkSpeed에 채택한다. 구 산식(anim.speed = 목표속도/실측속도 배속 — 애니를 속도에
        // 맞춤)은 테스트 11 실측에서 구조적 한계 확정(animSpeed 1.33~1.59 부여 후에도 슬립 0.34~0.52m/s
        // 잔존 = 문워크)으로 폐기 — animSpeed는 1.0 고정 회귀.
        // 발 슬립은 유일한 비순환 관측량이며 결함(미세 발 미끄러짐)을 직접 계량한다. clip.length는
        // 유효 보행 속도에 흡수된다(현재 anim.speed 배율 포함 실측 — 별도 조회 불요).
        // 실패(발 본 부재/샘플 부족/비정상치) 시 기존 고정값 유지(anim.speed=1.0 — walkSpeed 테이블
        // 성인 0.95/girl 0.85 계약 [100차 F-3] 하향) [93차 F3].
        // [94차 F3] 요건 완화 — 근거: 마을씬 테스트 5 전 주민 `스트라이드 정합 — model=... animSpeed=1.00
        // (폴백 — 실측 실패: 발 본=있음 샘플 8/7)` — 샘플 10 미달로 전량 폴백, 보정 미작동이 "여전히
        // 미끄러짐"의 주원인. 샘플 10→5 + 윈도우 2.5→3.5s 완화(걷기 재진입 재시도 최대 3회는 기존 유지).
        private const float StrideCalibrationWindow = 3.5f;   // 실측 윈도우(초) — 보행 사이클 ~3회 분 [94차 F3] 2.5→3.5 완화
        private const int StrideMinSamples = 5;               // 최소 접지 샘플 수(미달이면 실측 무효) [94차 F3] 10→5 완화
        private const float StrideContactEpsilon = 0.03f;     // 접지 판정 — 윈도우 최저 발 Y 허용차(m)
        private const float StrideTurnGateDegPerSec = 45f;    // 선회 중 샘플 제외 게이트(°/s — 회전이 발을 휩쓸어 오측정)
        // [100차 F-3] 채택 walkSpeed 방어 클램프 — 실측 이탈치(측정 오류)가 walkSpeed에 직행하지 않게 한다.
        //   구 StrideAnimSpeedMin/Max(anim.speed 배속 클램프 0.6/1.8)는 animSpeed 올림 폐지와 함께 폐기 —
        //   클립 유효 보행속도 실측치 0.81~1.05m/s·초기값 0.95/0.85를 포함하는 대역으로 재정의.
        private const float StrideAdoptWalkSpeedMin = 0.5f;   // 채택 walkSpeed 하한(m/s — 이탈치는 측정 오류 간주)
        private const float StrideAdoptWalkSpeedMax = 1.5f;   // 채택 walkSpeed 상한(m/s — 이탈치는 측정 오류 간주)
        private const int StrideMaxAttempts = 3;              // 개시 시도 상한(첫 구간이 도착/대화로 잘리는 경우 재시도)
        // [96차 F4] 폴백 영구 잠금 폐기 — 근거: 94차 실측 girl2 샘플 0/girl3 샘플 2(걷기 시간이 관측창
        //   3.5s보다 짧아 샘플 부족) 폴백 잔존 = 영구 폴백으로 보정 기회 상실. 재시도 소진(StrideMaxAttempts)
        //   후에도 성공할 때까지 다음 걷기 사이클마다 재시도하되 시도 간 쿨다운을 두어 재시도/로그 스팸을 막는다.
        private const float StrideRetryCooldownSeconds = 10f;   // 폴백 후 재시도 쿨다운(초) — 걷기 사이클 1~2회당 1회 수준
        // [93차 F3] 선회 보간 — 회전속도 상한(Quaternion.RotateTowards 구조 유지·계수만 완화).
        //   근거: 영상4 1~9s 실측 "방향 전환 즉시 회전(선회 계수 과민)" — 240°/s는 1.4m/s 보행에서
        //   선회 반경 v/ω≈0.33m로 제자리 회전처럼 보인다. 120°/s로 완화(반경 ≈0.67m — 곡선 보행)하고
        //   상한 상수(TurnSpeedCapDegPerSec)로 회전속도가 급변 계수를 넘지 못하게 고정한다.
        private const float TurnSpeedCapDegPerSec = 360f;     // 선회 회전속도 상한(°/s) — [93차 F3]

        private Vector3 _center;
        private float _radius;            // 배회 한계 반경(마을 반경×0.82 — 펜스/건물 안쪽 유지)
        private float _walkSpeed = WalkSpeedDefault;  // 보행 속도(m/s) — [92차] SetWalkModel로 모델별 치환 → [100차 F-3] 하향 초기값(girl=0.85/기타=0.95)+캘리브레이션 채택 재조정
        private bool _isRunner;   // [128-2 R] run 클립 장착 주민(스폰 경로 SetRunner로 지정 — 보행속도 상향)
        private float _turnDegPerSec = 120f;   // [93차 F3] 선회 계수 완화(240→120°/s — 상한 TurnSpeedCapDegPerSec와 함께 적용)
        private System.Random _rng;
        private Animator _anim;
        private float _animSpeed = 1.0f;  // [92차] anim.speed 예약 필드(1.0 유지) — 배속 튜닝은 영상 재실측 후
        private string _modelKey = "";    // [93차 F3] 모델 키 보관(SetWalkModel) — 스트라이드 정합 로그 식별용
        private bool _strideCalibrated;   // [93차 F3] → [96차 F4] 성공 확정 잠금(폴백은 잠금 없이 재시도 대기로 전환)
        private int _strideAttempts;      // [93차 F3] 개시 시도 카운터(StrideMaxAttempts 상한 — 소진 후에도 [96차 F4] 쿨다운 경유 재시도)
        private float _strideWindowLeft;  // [93차 F3] 실측 윈도우 잔여 시간(>0 = 실측 진행 중)
        private int _strideSamples;       // [93차 F3] 접지 샘플 수
        private float _strideSlideSum;    // [93차 F3] 슬립 합(전방 성분, 부호 유지 — +:애니 느림/−:애니 빠름)
        private Transform _strideFoot;    // [93차 F3] 실측 대상 발 본(LeftFoot 우선 — 믹사모 리그 계약)
        private Vector3 _strideFootPrev;  // [93차 F3] 발 본 직전 프레임 위치(속도 산출용)
        private bool _strideFootPrevValid;
        private float _strideFootMinY = float.PositiveInfinity;   // [93차 F3] 윈도우 내 발 최저 Y(접지 판정 기준)
        private float _stridePrevYaw;     // [93차 F3] 요각속도 게이트용 직전 요각
        private bool _strideFailedPendingRetry;   // [96차 F4] 마지막 시도 폴백 — 다음 걷기 사이클부터 쿨다운 경유 재시도(성공 시 해제)
        private float _strideRetryCooldownLeft;   // [96차 F4] 재시도 쿨다운 잔여(초) — >0이면 걷기 진입 재시도 보류(스팸 방지)
        // [100차 F-3] 모델별 walkSpeed 보정 캐시 — 세션당 1회 실측 계약. 첫 실측 성공 모델의 채택치를
        //   보관하고, 이후 같은 모델 스폰/캘리브레이션 재실행(대화 정지→재개 포함)에서 재실측 없이 채택한다.
        //   스태틱 생명주기 = 플레이 세션(도메인 리로드 시 초기화) = "세션당" 계약과 일치.
        private static readonly System.Collections.Generic.Dictionary<string, float> ModelWalkSpeedCache =
            new System.Collections.Generic.Dictionary<string, float>();
        private float _yOffset;           // 스폰 시점 발밑 오프셋(스케일 캘리브레이션 보존)
        private Vector3 _target;
        private bool _walking;
        private float _idleLeft;
        private bool _paused;   // [89차] 대화 자세 중 배회 정지 플래그(PauseForDialogue/ResumeAfterDialogue)
        private bool _walkPhaseApplied;   // [103차 P3] 첫 Walk 진입 1회 페이즈 오프셋 — 스트라이드 계약 무영향(산식/속도 불변)
        private int _seed;                // [103차 P3] Init 시드 보관 — 보행/Idle 페이즈 오프셋 결정론 산식용(Init 시그니처 무변경)
        private float _avoidRayLeft;   // [92차 후속 F-D] 회피 레이캐스트 주기 캐시 카운터(0.25s 간격 판정)
        private bool _avoidTurnLeft;   // [92차 후속 F-D] 회피 회전 좌우 교대 플래그(결정론 — 좌/우 번갈아)

        // ── [133차-P3] 주민 교차 정지 정적 캐시 — 레지스트리/쌍 쿨다운/진행 중 조우 목록 ──
        private bool _crossStopped;    // 교차 정지 진행 중(공통 조기 반환 — Speed 0, 대면 회전은 정적 틱 담당)
        private Vector3 _crossFaceTo;  // 상대 위치(월드) — 대면 회전 목표(정지 중 상대도 정지라 스냅샷 유효)
        private static readonly System.Collections.Generic.List<VillagerWanderer> CrossRegistry =
            new System.Collections.Generic.List<VillagerWanderer>();
        private static readonly System.Collections.Generic.Dictionary<long, float> CrossPairCooldown =
            new System.Collections.Generic.Dictionary<long, float>();
        private static readonly System.Collections.Generic.List<CrossEncounter> CrossEncounters =
            new System.Collections.Generic.List<CrossEncounter>();
        private static int _crossNextScanFrame;    // 다음 스캔 프레임(스로틀 게이트)
        private static int _crossLastTickFrame = -1;   // 진행 틱 프레임당 1회 게이트

        /// <summary>[133차-P3] 진행 중 조우 단위 — 양측 참조 + 잔여 정지 시간.</summary>
        private sealed class CrossEncounter
        {
            public VillagerWanderer A;
            public VillagerWanderer B;
            public float Left;
        }

        // ── [100차] 음식점 방문 목표(RCT식 손님 이동) — RestaurantVisitorSystem 위임 전용 ──
        private Vector3? _visitPoint;      // 방문 지점(음식점 앳 오프셋) — null = 일반 배회
        private float _visitLinger;        // 도착 후 머무름(초)
        private bool _atVisit;             // 방문 지점 도착 상태(머무름 중)
        /// <summary>[100차] 방문 지점 도착(구매 연출 시점) — RestaurantVisitorSystem이 구독(PurchaseVisualized 발화용).</summary>
        public event System.Action VisitArrived;
        /// <summary>[100차] 방문 목표 보유 중 여부 — 스캔 중복 선정 방지 게이트(RestaurantVisitorSystem 소비).</summary>
        public bool IsVisiting => _visitPoint.HasValue;

        // ── [98차 F1] F1-2 런타임 애니 진단(프리징 판별 데이터) — 1Hz, 스폰 후 30s 한정 ──
        // 근거: 마을씬 테스트 9 — 걷기 사이클 관찰 0(포즈 완전 고정 + 화면 내 이동)이 특정 시점 이후
        // 발생. 기존 로그(테이크명 'mixamo.com' 동일 — F1-1)만으론 재생 상태 판별 불가 → 1Hz 실측으로
        // 상태/클립/파라미터를 직접 관측한다(스폰 후 30s 한정 — 상시 저비용 대안 대비 로그 스팸 방지).
        private const float AniDiagIntervalSeconds = 1f;    // 진단 주기(초) — 스펙 지정 1Hz
        private const float AniDiagWindowSeconds = 30f;     // 진단 윈도우(초) — 스폰 후 30s 한정
        private float _aniDiagElapsed;     // 스폰(Init) 후 경과(초) — 윈도우 판정
        private float _aniDiagTimer;       // 다음 진단까지 잔여(초)
        private bool _aniDiagActive;       // 진단 윈도우 활성(Init에서 가동 — EditMode 컴포넌트 주입 무영향)

        // ── [134차 B] 배회 진단 프로브 — Init 후 10초 시점 1회 ──
        // 근거(계획 항목 B): walk 클립 로드 성공·보행 캘리브레이션 로그 0건 = Walk 상태 진입 관측 안 됨 →
        // Animator 참조 구조/Speed 세팅/실이동 여부를 단발 프로브로 직접 관측(후보 단일 특정 데이터).
        // moved = Init 시점 position 기준 누적 이동 거리(프레임 델타 합 — 수직 스냅 노이즈 제외, y=0 델타).
        private const float WanderProbeAtSeconds = 10f;   // 프로브 발화 시점(초) — 스펙 지정
        private float _probeElapsed;      // Init 후 경과(초) — 프로브 발화 판정
        private bool _probeDone;          // 프로브 1회 발화 플래그(재발화 금지)
        private float _probeMoved;        // Init 시점 이후 누적 이동 거리(m — 수평 성분)
        private Vector3 _probeLastPos;    // 직전 프레임 위치(델타 산출용)

        // ── [98차 F1] F1-3 프리징 자가수리 — 걷기 진입 후 1.5s 셀프체크 ──
        // 판정: 발 본 Δ≈0(애니 평가 정지) + 루트 이동 지속(테스트 9 증상 — 포즈 완전 고정+화면 내 이동).
        // 수리: animator.Rebind() + 오버라이드 컨트롤러 재적용(GetResolvedMixamoOverride — 92차 캐시 승계)
        //       + Speed 파라미터 재설정(91차 Rebind 경로 확장). 수리 로그 1회(수리 발생 시에만 — 정상 무로그).
        private const float FreezeCheckDelaySeconds = 1.5f;   // 걷기 진입 후 셀프체크 시점(초 — 스펙 지정)
        private const float FreezeFootEpsilonM = 0.05f;       // 발 본 Δ≈0 판정(체크 구간 누적 이동 하한 미만)
        private const float FreezeRootMinM = 0.5f;            // 루트 이동 지속 판정 하한(같은 구간 이동 — 슬라이드 근거)
        private float _freezeCheckLeft;          // 셀프체크까지 잔여(>0 = 측정 중, ≤0 = 휴면)
        private bool _freezeFootStartValid;      // 개시 프레임 기록 완료 플래그
        private Vector3 _freezeFootStart;        // 체크 개시 시점 발 본 위치
        private Vector3 _freezeRootStart;        // 체크 개시 시점 루트 위치
        private Transform _freezeFoot;           // 체크 대상 발 본(FindFootBone 재사용 — LeftFoot 우선 계약)
        private int _freezeRepairs;              // 수리 실행 횟수(로그 식별용 — 수리 자체는 멱등)

        // ── [99차 F-C] C-3 자가수리 기준 보강 — 발 본 Δ 기준에 SMR-Animator hips 불일치 거리 추가 ──
        // 근거: [98차 F1] 판정이 "본" 기준이라 렌더 동결(골격 불일치 — SMR이 다른 골격에 바인딩)을 못 봄.
        // hips 불일치 > 임계면 본 Δ가 있어도(스트라이드 실측 성공 케이스) 골격정합 수리(RemapSmrToAnimator)를
        // 실행한다. 수리 로그는 F1-3 형식 확장(폐기 아님).
        private const float SkeletonHipsMismatchM = 0.05f;   // SMR-Animator hips 불일치 임계(m)

        // ── [99차 F-D] D-2 배회 중 접지 유지 — 0.5s 주기 Raycast 접지 스냅(경사 대응, y만 보정) ──
        // 기존 이동/애니 로직 무변경. TerrainGenerator 접지(기존 Update 말단)와 병행 — 물리 지면(건물 지붕/
        // 경사 실측)과 지형 함수 간 괴차만 보정한다. 스냅은 Wanderer 부착 후(Init에서 _yOffset 기록 이후) 틱에서 실행.
        private const float GroundSnapInterval = 0.5f;   // [99차 F-D] 접지 스냅 주기(초) — 스펙 지정
        private float _groundSnapTimer;                  // [99차 F-D] 다음 스냅까지 잔여(초)

        // ── [104차 F3] 루트↔가시 신체 접지 보상(LateUpdate) 파라미터 ──
        // 보행 발 들어올림 여유(m) — 바인드 오프셋 + 이 여유까지의 애니 수직변위(스텝 리프트/Idle 동요)는
        // 존중하고 초과분만 상쇄한다(정상 보행 애니 무손상 — 스트라이드 실측 산식 무영향).
        private const float BodyLiftAllowanceM = 0.35f;
        // 진단 로그 게이트(m)/스로틀(초) — 괴리 초과 실측 데이터([104차 F3] 근원 확정용 — 애니메이터 골격루트
        // 구동 과잉의 발생 시점·크기를 다음 Play 로그로 남긴다. 보상이 상시 작동해 잔존 괴리는 제한적).
        private const float DivergenceLogThresholdM = 1.0f;
        private const float DivergenceLogIntervalSeconds = 5f;
        // ── [104차 F2] 대화 중 yaw 제어권 위임 게이트(정적 — 세션 단일소스) ──
        /// <summary>
        /// [104차 F2] 대화 중 주민 yaw 제어권 위임 — DialogueCameraDirector.Begin(fullBody) true / End false
        /// (멱등 — [93차] CameraFollowSuppressed와 동일 Begin/End 쌍 계약). true 중 배회 회전 분기는
        /// 무기입(시선 = Director가 단일 제어 — LateUpdate 매 틱 유지). _paused 정지와 이중 안전이며
        /// [94차 F4] F키 정지 경로(PauseForDialogue)와 간섭 없다.
        /// </summary>
        public static bool DialogueYawControlled;
        // 골격 루트/바인드 캡처 — Init(스폰 직후 = 애니메이터 첫 평가 이전 = 바인드 포즈) 1회 기록.
        private Transform _skeletonRoot;          // [104차 F3] 골격 루트(발 본 최상위 직계 조상 — 루트 GO 직계 자식)
        private Vector3 _bindSkeletonLocalPos;    // [104차 F3] 골격 루트 바인드 로컬 위치
        private Quaternion _bindSkeletonLocalRot; // [104차 F3] 골격 루트 바인드 로컬 회전(요각 오프셋 상쇄 기준)
        private float _bindFootOffset;            // [104차 F3] 바인드 발 오프셋(m) — 발 본y − 루트y(스폰 접지 직후)
        private Transform _footBone;              // [104차 F3] 보상 측정 발 본(FindFootBone 계약 재사용 — LeftFoot 우선)
        private bool _bodyCompensatorReady;       // [104차 F3] 보상 가동 가능(캡처 성공) — 발 본 부재 모델 스킵(무해)
        private float _divergenceLogNextTime;     // [104차 F3] 괴리 진단 로그 스로틀 기준(초)
        private bool _captureWarned;              // [117차 R4] 골격 캡처 스킵 사유 경고 1회성 플래그
        private bool _heightCensusDone;           // [117차 R4] 키실측 진단 세션당 1회 플래그(동일 측정법)

        /// <summary>배회 초기화 — center=마을 중심, radius=마을 반경, seed=주민별 결정론 시드.</summary>
        public void Init(Vector3 center, float radius, int seed)
        {
            _center = center;
            _radius = Mathf.Max(6f, radius * 0.82f);
            _rng = new System.Random(seed);
            _seed = seed;   // [103차 P3] 시드 보관 — 페이즈 오프셋 결정론 산식용(시그니처/기존 소비처 무변경)
            _anim = GetComponent<Animator>();
            if (_anim != null) _anim.speed = _animSpeed;   // [92차] 1.0 유지(필드 1회 적용 — 애니 속도 계약 무변경)
            // 발밑 오프셋 보존 — SpawnNpc가 이미 접지시킨 상태로 부착된다
            _yOffset = transform.position.y - (TerrainGenerator.GetHeightAt(
                transform.position.x, transform.position.z, ProjectName.Core.Data.BiomeType.Plains, TerrainSeed) + GroundBase);
            // [104차 F3] 골격 루트 바인드 캡처 — 애니메이터 첫 평가 이전(스폰 직후 = 바인드 포즈) 1회.
            //   바인드 발 오프셋 + 골격 루트 로컬 TRS = LateUpdate 접지 보상 기준선([104차 F3] 괴리 상쇄).
            CaptureSkeletonBindPose();
            _walking = false;
            _idleLeft = IdleDuration();
            // [98차 F1] F1-2 진단 윈도우 가동 — 스폰(Init) 기준 30s 한정 1Hz(스폰 후 판정 계약).
            _aniDiagElapsed = 0f;
            _aniDiagTimer = AniDiagIntervalSeconds;
            _aniDiagActive = _anim != null;
            // [134차 B] 배회 진단 프로브 개시 — Init 시점 위치 기록(누적 이동거리 기준선) + 카운터 리셋.
            _probeLastPos = transform.position;
            _probeMoved = 0f;
            _probeElapsed = 0f;
            _probeDone = false;
            // [99차 F-D] D-2 배회 중 접지 스냅 개시 — Init 직후 첫 틱부터 0.5s 주기 가동(스폰 접지 스냅 연속).
            _groundSnapTimer = GroundSnapInterval;
            // [133차-P3] 밤 귀가 스케줄 개시 — 스폰 앵커(홈) 기록 + TimeManager 이벤트 구독 + 초기 밤 동기화
            _homePoint = transform.position;
            _nightStateCheckLeft = NightStateCheckInterval;
            HookTimeEvents();
            if (!CrossRegistry.Contains(this))   // [133차-P3] 이중 Init 방어 — 레지스트리 중복 등록 금지
                CrossRegistry.Add(this);   // 교차 정지 스캔 레지스트리 등록(해제는 OnDestroy)
            // [103차 P3] Idle 페이즈 오프셋 — 주별 시작 자세 다양화(결정론 — 시드 재사용, walk 계약 무영향).
            //   normTime 지정 Play만 수행(클립/속도/스트라이드 산식 불변). 상태 평가는 PlayMode에서만
            //   유효하므로 EditMode(테스트 주입) 예외 방지 try/catch 존속.
            if (_anim != null)
            {
                try
                {
                    float idleOffset = Mathf.Abs(_seed % 997) / 997f;
                    _anim.Play("Idle", 0, idleOffset);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[103차 P3] Idle 페이즈 적용 실패 — {name} {ex.Message}");
                }
            }
            PickTarget();
        }

        /// <summary>[103차 P3] 보행 페이즈 오프셋 — 첫 Walk 진입 1회, 시드 결정론 normTime 지정 Play.
        ///   스트라이드 계약 무영향: 클립/walkSpeed/Speed 산식 불변이며 캘리브레이션 윈도우는 dt 누적
        ///   기반(TickStrideCalibration)이라 normTime 리셋이 실측 윈도우에 영향 없음(상태 시작 위상만 변경).</summary>
        private void ApplyWalkPhaseOnce()
        {
            if (_walkPhaseApplied || _anim == null) return;
            _walkPhaseApplied = true;
            try
            {
                float walkOffset = Mathf.Abs(_seed % 1000) / 1000f;
                _anim.Play("Walk", 0, walkOffset);
                Debug.Log($"[103차 P3] 보행 페이즈 — {name} offset={walkOffset:F3}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[103차 P3] Walk 페이즈 실패 — {name} {ex.Message}");
            }
        }

        private void Update()
        {
            // [133차-P3] 교차 정지 틱 — 전 인스턴스가 호출하되 프레임당 1회만 실행(정적 프레임 게이트 —
            //   프레임마다 전체 N² 비교 금지 계약: 스캔은 0.5s 간격 스로틀, 진행 틱은 프레임 단일화).
            TickCrossEncountersFrame();
            ScanCrossEncountersFrame();

            // [89차] 대화 자세 — 배회/휴식 카운터 동결 + Speed 0 유지(Idle. Talk 트리거는 NPCAmbientDialogue가 발화).
            // [133차-P3] 교차 정지도 동일 게이트 — 정지 중 이동/휴식 동결(대면 회전은 위 정적 틱이 담당).
            if (_paused || _crossStopped)
            {
                if (_anim != null) _anim.SetFloat("Speed", 0f);
                return;
            }

            float dt = Time.deltaTime;
            TickWanderProbe(dt);   // [134차 B] 이동 누적 + 10초 시점 1회 진단 프로브
            // [133차-P3] 밤 귀가 — 이벤트 누락 자가정합(보조 폴링) 후 홈 복귀 보행 개시 게이트
            TickNightStateCheck(dt);
            EnsureNightReturnStarted();
            // [96차 F4] 재시도 쿨다운 감소 — 폴백 후 다음 걷기 사이클 재시도 간격 보장(스팸 방지).
            //   대화 정지(_paused) 중엔 감소하지 않는다(시간 동결 — 재개 후 걷기 사이클 기준 판정).
            if (_strideRetryCooldownLeft > 0f)
                _strideRetryCooldownLeft = Mathf.Max(0f, _strideRetryCooldownLeft - dt);
            TickAnimationDiagnostics(dt);   // [98차 F1] F1-2 1Hz 진단(30s 한정 — 프리징 판별 데이터)
            // [99차 F-D] D-2 배회 중 접지 유지 — 0.5s 주기 Raycast 스냅(y만 보정 — 이동/애니 로직 무변경).
            //   TerrainGenerator 접지(Update 말단)가 y를 재기록하므로 스냅 후 Δ만큼 _yOffset을 갱신해 유지한다.
            _groundSnapTimer -= dt;
            if (_groundSnapTimer <= 0f)
            {
                _groundSnapTimer = GroundSnapInterval;
                float preY = transform.position.y;
                float snapDelta = VillageNpcSpawner.SnapToGroundRay(gameObject, 1.5f, 8f);
                if (Mathf.Abs(snapDelta) > 0.005f)
                {
                    _yOffset = transform.position.y - (TerrainGenerator.GetHeightAt(
                        transform.position.x, transform.position.z, ProjectName.Core.Data.BiomeType.Plains, TerrainSeed) + GroundBase);
                    Debug.Log($"[99차 F-D] 접지(배회) — model={_modelKey} y={preY:F3}→{transform.position.y:F3} Δ={snapDelta:F3}");
                }
            }
            if (_walking)
            {
                Vector3 to = _target - transform.position; to.y = 0f;
                if (to.magnitude < 0.35f)
                {
                    _walking = false;
                    // [133차-P3] 홈 도착 — 복귀 보행 종료(정지 홀드 진입 — 낮 복귀 이벤트까지 유지)
                    if (_nightMode) _nightReturnWalking = false;
                    // [100차] 방문 지점 도착 — 구매 연출 이벤트 발화 후 머무름(linger) → 일반 배회 복귀.
                    if (_visitPoint.HasValue && !_atVisit && NearVisitPoint(_target))
                    {
                        _atVisit = true;
                        _idleLeft = _visitLinger;   // 머무름 = 휴식 카운터 재사용(이동/애니 로직 무변경)
                        try { VisitArrived?.Invoke(); }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[100차] VisitArrived 구독자 오류 — {ex.Message}");
                        }
                    }
                    else
                    {
                        _idleLeft = IdleDuration();
                    }
                }
                else
                {
                    // [92차 후속 F-D] 이동 직전 전방 레이캐스트 회피 — 0.25s 주기 캐시(매 프레임 폐기 — 비용 최소화).
                    //   흉부 라인(0.9m)에서 전방 1.2m에 장애물(Default 건물/주민 캡슐) 히트 시 목표 방향 재선정.
                    _avoidRayLeft -= dt;
                    if (_avoidRayLeft <= 0f)
                    {
                        _avoidRayLeft = AvoidRayInterval;
                        if (Physics.Raycast(transform.position + Vector3.up * AvoidRayHeight,
                                transform.forward, AvoidRayDistance, ObstacleMask))
                        {
                            RerouteAroundObstacle();
                        }
                    }

                    Vector3 dir = to.normalized;
                    // 부드러운 선회 — 현재 forward에서 목표 방향으로 yaw 보간(플레이어 조작감 유사 곡선 보행)
                    // [93차 F3] 회전속도 상한 적용 — 기존 RotateTowards 구조 유지, 계수만 완화+상한 고정.
                    // [104차 F2] 대화 중 yaw 제어권 위임 게이트 — DialogueCameraDirector(fullBody 세션)가
                    //   시선을 단일 제어한다(멱등 — _paused 정지와 이중 안전, [94차 F4] F키 정지 경로와 간섭 없음).
                    float turnRate = Mathf.Min(_turnDegPerSec, TurnSpeedCapDegPerSec);
                    Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
                    if (!DialogueYawControlled)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnRate * dt);
                    transform.position += transform.forward * (_walkSpeed * dt);

                    // [93차 F3] 스트라이드 정합 실측 — 첫 Walk 구간(이동 적용 후 프레임 기준 접지 슬립 수집)
                    TickStrideCalibration(dt);
                    // [98차 F1] F1-3 프리징 자가수리 셀프체크(걷기 중 1.5s 판정 — 발 본 Δ≈0 + 루트 이동 지속)
                    TickFreezeSelfCheck(dt);
                }
            }
            else
            {
                _idleLeft -= dt;
                if (_idleLeft <= 0f)
                {
                    // [133차-P3] 밤 홀드 — 홈 정지 유지(낮 복귀 이벤트까지 재배회 금지). 방문 목표 보유 중이면
                    //   기존 흐름 우선(VisitArrived 정상 발화 후 귀가 이행 — 지연 보고는 EnterNightSchedule 로그).
                    if (_nightMode && !_visitPoint.HasValue)
                    {
                        _idleLeft = NightIdleHoldSeconds;
                        if (_anim != null) _anim.SetFloat("Speed", 0f);
                        return;   // Update 조기 반환 — 목표 재추첨/이동 없이 정지 홀드(상단 _paused 게이트와 동일 패턴)
                    }
                    // [100차] 머무름 종료 → 방문 목표 소멸 후 일반 배회 복귀. 방문 예정(걷기 중 설정)이면
                    // 다음 목표를 방문 지점으로 — RCT식 손님 이동(기존 PickTarget 계약 유지).
                    if (_atVisit) { _atVisit = false; _visitPoint = null; }
                    else if (_visitPoint.HasValue) _target = _visitPoint.Value;
                    else PickTarget();
                    _walking = true;
                    ApplyWalkPhaseOnce();   // [103차 P3] 첫 Walk 진입 1회 페이즈 오프셋(캘리브레이션은 dt 누적 기반 — 무영향)
                    TryBeginStrideCalibration();   // [93차 F3] 첫 Walk 진입 시 캘리브레이션 개시(1회 — 절단 시 제한 재시도)
                    BeginFreezeSelfCheck();   // [98차 F1] F1-3 걷기 진입 프리징 셀프체크 개시(1.5s 후 판정)
                }
            }

            // 접지 — 발밑 오프셋 보존
            Vector3 p = transform.position;
            p.y = TerrainGenerator.GetHeightAt(p.x, p.z, ProjectName.Core.Data.BiomeType.Plains, TerrainSeed)
                 + GroundBase + _yOffset;
            transform.position = p;

            // 애니 — [89차] Player_AC Speed 계약(>0.55 Walk 진입 / <0.35 Idle 복귀 — 값 1/0 계약 충족)
            if (_anim != null) _anim.SetFloat("Speed", _walking ? 1f : 0f);
        }

        /// <summary>
        /// [104차 F3] 루트↔가시 신체 접지 보상(LateUpdate — 애니메이터 평가 후 최종 적용권).
        /// 근원(실측 — [103차 P1] 12건 + [99차 F-D]): 첫 Idle 평가 후 골격 루트가 클립/리타겟에 의해
        /// 과도 구동(발 본이 루트 기준 +3.5~4.1m 부상) → SnapToGroundRay가 그 괴리만큼 루트를 지하로
        /// 침강([99차 F-D] -3.5m) = 루트 지하/가시 지면 3.7m 괴리(콜라이더·회피레이·E키 판정 지하화).
        /// 수리: ①골격 루트를 바인드 로컬 TRS로 리셋(비리타겟 골격루트 커브 무효화 — 부상+요각 오프셋)
        /// ②잔여 괴리 = (발y − 루트y) − (바인드 오프셋 + 보행 발 들어올림 여유) 초과분만 골격 루트
        /// local y에서 상쇄. → SnapToGroundRay shift가 바인드 범위(≤0.5m)로 수렴 — 스냅 대상(루트)과
        /// 가시 신체 일관(루트 지상 유지 — SnapToGroundRay/P1 로그 구조 무변경).
        /// 대화 정지(_paused) 중에도 실행(Idle/Talk도 골격 구동 — 괴리 지속). 매 프레임 재판정(누적 없음 —
        /// 리셋 후 실측). 정상 보행(들어올림 ≤ 여유)은 무보상 — 스트라이드 실측 산식 무영향.
        /// 진단: 잔여 괴리 > DivergenceLogThresholdM 시 [104차 F3] 로그(스로틀 — 애니 근원 확정 데이터).
        /// </summary>
        private void LateUpdate()
        {
            if (!_bodyCompensatorReady || _skeletonRoot == null) return;
            // ① 바인드 리셋 — 클립 구동 골격루트 변위(위치/회전) 무효화. 바인드 캡처는 Init(애니 평가 전) 기준.
            _skeletonRoot.localPosition = _bindSkeletonLocalPos;
            _skeletonRoot.localRotation = _bindSkeletonLocalRot;
            // ② 잔여 괴리 상쇄 — 바인드 복원 후 실측(휴머노이드 힙 구동 과잉 등 잔여분만)
            if (_footBone == null) return;
            float excess = VillageNpcSpawner.ComputeBodyDivergenceExcess(
                _footBone.position.y, transform.position.y, _bindFootOffset, BodyLiftAllowanceM);
            if (excess <= 0f) return;   // 정상 범위(여유 이내) — 무보상
            Vector3 p = _skeletonRoot.localPosition;
            p.y -= excess;
            _skeletonRoot.localPosition = p;
            if (excess > DivergenceLogThresholdM && Time.time >= _divergenceLogNextTime)
            {
                _divergenceLogNextTime = Time.time + DivergenceLogIntervalSeconds;
                Debug.Log($"[104차 F3] 골격 괴리 — {name} footRootOffset=" +
                          $"{VillageNpcSpawner.ComputeFootRootOffset(_footBone.position.y, transform.position.y):F2}m " +
                          $"bind={_bindFootOffset:F2}m excess={excess:F2}m skeletonRoot={_skeletonRoot.name} " +
                          $"localPos.y={p.y:F3}(bind {_bindSkeletonLocalPos.y:F3}) — 골격루트 구동 과잉 상쇄([104차 F3] 진단 — 애니 근원 확정용)");
            }
        }

        /// <summary>
        /// [104차 F3] 골격 루트 바인드 캡처 — Init(스폰 직후, 애니메이터 첫 평가 이전 = 바인드 포즈) 1회.
        /// ①측정 발 본 = FindFootBone 계약(LeftFoot 우선) ②골격 루트 = 발 본의 최상위 직계 조상
        /// (루트 GO의 직계 자식 — 실측 계층 "Villager_N/Root" 계약, 이름 무의존 탐색) ③바인드 로컬
        /// TRS + 바인드 발 오프셋 기록. 발 본 부재/루트 직계 아님 = 보상 스킵(무해 — 기존 경로 유지).
        /// </summary>
        private void CaptureSkeletonBindPose()
        {
            _footBone = FindFootBone();
            if (_footBone == null)
            {
                if (!_captureWarned)
                {
                    _captureWarned = true;
                    Debug.Log($"[117차 R4] 골격 캡처 스킵 — 발 본 부재 ({name}) — [104차 F3] 보상 미적용(부동 의심 후보 1)");
                }
                return;   // 발 본 부재 — 보상 불가(무해)
            }
            Transform t = _footBone;
            while (t.parent != null && t.parent != transform) t = t.parent;
            if (t.parent != transform)
            {
                if (!_captureWarned)
                {
                    _captureWarned = true;
                    Debug.Log($"[117차 R4] 골격 캡처 스킵 — 발 본이 루트 GO 후손 아님 ({name}) — [104차 F3] 보상 미적용(부동 의심 후보 2)");
                }
                return;   // 발 본이 루트 GO 직계 하위가 아님 — 보상 스킵(무해)
            }
            _skeletonRoot = t;
            _bindSkeletonLocalPos = t.localPosition;
            _bindSkeletonLocalRot = t.localRotation;
            _bindFootOffset = VillageNpcSpawner.ComputeFootRootOffset(_footBone.position.y, transform.position.y);
            _bodyCompensatorReady = true;
        }

        /// <summary>[89차] 대화 자세 진입 — 배회 정지 + Speed 0 즉시(Idle 유지. Talk 트리거는 NPCAmbientDialogue가 발화).</summary>
        public void PauseForDialogue()
        {
            _paused = true;
            _walking = false;
            if (_anim != null) _anim.SetFloat("Speed", 0f);
        }

        /// <summary>[89차] 대화 자세 해제 — 휴식 카운터 재시작(기존 휴식 1.5~5.5s 계약 — 즉시 재배회 방지).</summary>
        public void ResumeAfterDialogue()
        {
            _paused = false;
            _walking = false;
            _idleLeft = IdleDuration();
        }

        /// <summary>
        /// [100차] 음식점 방문 목표 설정 — 다음 이동 목표를 방문 지점으로 교체(RCT식 손님 이동).
        ///  · 마을 반경 내 클램프 — 배회 반경 밖 목표 금지(기존 0.82 규약 준용).
        ///  · 걷기 중 설정 시 = 방문 예정(현 목표 도착 → 휴식 경과 후 방문 지점으로).
        ///  · 휴식 중 설정 시 = 즉시 출발. 대화 정지(_paused) 중엔 기록만(재개 후 방문 — 시간 동결 규약 존중).
        /// </summary>
        public void SetVisitTarget(Vector3 point, float lingerSeconds)
        {
            Vector3 fromCenter = new Vector3(point.x - _center.x, 0f, point.z - _center.z);
            if (fromCenter.magnitude > _radius)
                point = _center + fromCenter.normalized * _radius;
            _visitPoint = point;
            _visitLinger = Mathf.Max(3f, lingerSeconds);
            _atVisit = false;
            if (!_walking && !_paused)
            {
                _target = point;
                _walking = true;
                ApplyWalkPhaseOnce();   // [103차 P3] 첫 Walk 진입 1회 페이즈 오프셋(방문 즉시 출발 경로 — 동일 1회 계약)
                TryBeginStrideCalibration();   // [93차 F3] 첫 Walk 진입 캘리브레이션(기존 경로 재사용)
                BeginFreezeSelfCheck();        // [98차 F1] 프리징 셀프체크 개시(기존 경로 재사용)
            }
        }

        /// <summary>[100차] 목표가 방문 지점인지 판정 — 도착 판정과 동일 0.35m 계약(제곱 비교).</summary>
        private bool NearVisitPoint(Vector3 target)
            => _visitPoint.HasValue &&
               new Vector2(target.x - _visitPoint.Value.x, target.z - _visitPoint.Value.z).sqrMagnitude <= 0.1225f;

        /// <summary>[98차 F1] F1-4 재개 봉합 판정용 — 대화 자세 정지(_paused) 잔존 실측(같은 어셈블리 소비:
        /// DialogueCameraDirector.End 폴백 재개 / InteractionPanelUTK 워치독 — 구독자 미재개 검증).</summary>
        public bool IsDialoguePaused => _paused;

        // ================================================================
        // [133차-P3] 밤 귀가 스케줄 레이어 — TimeManager 이벤트 기반(IsNight/IsDay 계약)
        // ================================================================

        /// <summary>[133차-P3] 밤 시간표 활성 여부 — 교차 정지 스캔 등 외부 소비 판정용(홀드/귀가 중 true).</summary>
        public bool IsNightHolding => _nightMode;

        /// <summary>[133차-P3] TimeManager 이벤트 구독 — 부재 시(TickNightStateCheck) 후속 부착 재시도. 멱등.
        /// 초기 상태 동기화: 스폰 시점이 이미 밤이면 즉시 시간표 진입(이벤트 유실 대비).</summary>
        private void HookTimeEvents()
        {
            if (_timeEventsHooked) return;
            TimeManager tm = TimeManager.Instance;
            if (tm == null) return;
            tm.OnNightStart += HandleNightStart;
            tm.OnDayStart += HandleDayStart;
            _timeEventsHooked = true;
            if (tm.IsNight) EnterNightSchedule("스폰 시점 밤 동기화");
        }

        /// <summary>[133차-P3] 이벤트 구독 해제 — 파괴 경로(OnDestroy) 단일 소비(유출 방지).</summary>
        private void UnhookTimeEvents()
        {
            if (!_timeEventsHooked) return;
            TimeManager tm = TimeManager.Instance;
            if (tm != null)
            {
                tm.OnNightStart -= HandleNightStart;
                tm.OnDayStart -= HandleDayStart;
            }
            _timeEventsHooked = false;
        }

        private void HandleNightStart() => EnterNightSchedule("OnNightStart");
        private void HandleDayStart() => ExitNightSchedule();

        /// <summary>
        /// [133차-P3] 밤 스케줄 진입 — 웨이포인트 순환 중단 + 홈 복귀(개시는 EnsureNightReturnStarted 게이트).
        /// 진행 중 방문 목표는 유지: VisitArrived가 정상 발화해 RestaurantVisitorSystem 기록이 정상 소멸된다
        /// (강제 소멸 시 구독 유출 — 기존 계약 무변경 원칙). 방문 완료 즉시 귀가 이행 — 스케줄이 양보하는
        /// 것이 아니라 자연 경계에서 주도권을 회복하며, 지연 시 본 로그로 보고한다.
        /// </summary>
        private void EnterNightSchedule(string reason)
        {
            if (_nightMode) return;
            _nightMode = true;
            _nightReturnWalking = false;
            if (_visitPoint.HasValue)
                Debug.Log($"[133차-P3] 귀가 지연 보고 — {name} 진행 중 방문 목표 보유(VisitArrived 발화 후 즉시 귀가 — reason={reason})");
            ClearCrossStopState();   // 진행 중 교차 정지 해제 — 시간표가 우선(양보 금지 계약)
        }

        /// <summary>[133차-P3] 낮 스케줄 복귀 — 기존 배회 재개. 휴식 카운터 재충전 → 다음 틱 PickTarget → Walk
        /// 자동 재개(Speed 파라미터 1/0 플립은 기존 Update 말단 경로 — 신규 애니 경로 없음).</summary>
        private void ExitNightSchedule()
        {
            if (!_nightMode) return;
            _nightMode = false;
            _nightReturnWalking = false;
            _walking = false;
            _idleLeft = IdleDuration();
            ClearCrossStopState();
        }

        /// <summary>
        /// [133차-P3] 홈 복귀 보행 개시 게이트 — 밤 활성 + 비정지 + 방문 목표 부재 조건에서 1회 개시(멱등).
        /// 이동은 기존 드라이버 파이프 재사용(_target 지정 + Walk 진입 — ApplyWalkPhaseOnce/스트라이드 캘리브레이션/
        /// 프리징 셀프체크 기존 경로 그대로, 신규 애니 경로 없음).
        /// </summary>
        private void EnsureNightReturnStarted()
        {
            if (!_nightMode || _paused || _nightReturnWalking || _visitPoint.HasValue) return;
            if (NearHomeXZ())
            {
                _nightReturnWalking = false;   // 이미 홈 — 정지 홀드는 idle 분기(NightIdleHoldSeconds)가 담당
                return;
            }
            _target = _homePoint;
            _walking = true;
            _nightReturnWalking = true;
            ApplyWalkPhaseOnce();
            TryBeginStrideCalibration();
            BeginFreezeSelfCheck();
            Debug.Log($"[133차-P3] 귀가 개시 — {name} 홈({_homePoint.x:F1},{_homePoint.z:F1}) 복귀 보행");
        }

        /// <summary>[133차-P3] 이벤트 누락 자가정합 — TimeManager 후속 생성/유실 대비 보조 폴링(2s 주기 저비용).
        /// 이벤트(OnNightStart/OnDayStart)가 주 경로이며 폴링은 불일치 시에만 개입한다.</summary>
        private void TickNightStateCheck(float dt)
        {
            _nightStateCheckLeft -= dt;
            if (_nightStateCheckLeft > 0f) return;
            _nightStateCheckLeft = NightStateCheckInterval;
            HookTimeEvents();   // 후속 부착(멱등)
            TimeManager tm = TimeManager.Instance;
            if (tm == null || tm.IsNight == _nightMode) return;
            if (tm.IsNight) EnterNightSchedule("폴링 정합");
            else ExitNightSchedule();
        }

        /// <summary>[133차-P3] 홈 도착 판정 — 기존 이동 도착 계약과 동일 0.35m(xz 제곱거리 비교).</summary>
        private bool NearHomeXZ()
        {
            Vector3 d = _homePoint - transform.position; d.y = 0f;
            return d.sqrMagnitude <= HomeArrivalDist * HomeArrivalDist;
        }

        // ================================================================
        // [133차-P3] 주민 교차 정지(대화 흉내) — 정적 스캔/진행 관리
        //   NPCAmbientDialogue와 역할 분담: 플레이어 대화 = 말풍선 대사(기존 계약), 주민-주민 조우 =
        //   무언 정지+서로 대면만(말풍선 스프라이트 부재 — 프로젝트 실측). 정지+대면만 제공.
        // ================================================================

        /// <summary>[133차-P3] 스캔 스로틀 — 프레임 카운터 기반 0.5s 간격(프레임마다 전체 N² 비교 금지 계약).</summary>
        private void ScanCrossEncountersFrame()
        {
            int f = Time.frameCount;
            if (f < _crossNextScanFrame) return;
            _crossNextScanFrame = f + CrossScanFrameInterval;
            ScanCrossEncounters();
        }

        /// <summary>[133차-P3] 조우 스캔 — 레지스트리 순회(xz 제곱거리 1차 판정). 대화 정지/밤 홀드/진행 중
        /// 주민은 제외(대화 자세와 시간표가 우선 — 교차 정지가 양보). 성립 시 양측 정지 + 쿨다운 기록.</summary>
        private static void ScanCrossEncounters()
        {
            CrossRegistry.RemoveAll(v => v == null);   // 파기된 인스턴스 정리
            if (CrossPairCooldown.Count >= CrossPairCooldownCap)
            {
                var expired = new System.Collections.Generic.List<long>();
                foreach (var kv in CrossPairCooldown)
                    if (kv.Value <= Time.time) expired.Add(kv.Key);
                for (int i = 0; i < expired.Count; i++) CrossPairCooldown.Remove(expired[i]);
            }
            for (int i = 0; i < CrossRegistry.Count; i++)
            {
                VillagerWanderer a = CrossRegistry[i];
                if (a._crossStopped || a._paused || a._nightMode) continue;
                for (int j = i + 1; j < CrossRegistry.Count; j++)
                {
                    VillagerWanderer b = CrossRegistry[j];
                    if (b._crossStopped || b._paused || b._nightMode) continue;
                    Vector3 d = b.transform.position - a.transform.position; d.y = 0f;
                    if (d.sqrMagnitude > CrossStopTriggerDist * CrossStopTriggerDist) continue;
                    long key = CrossPairKey(a, b);
                    if (CrossPairCooldown.TryGetValue(key, out float until) && Time.time < until) continue;
                    CrossPairCooldown[key] = Time.time + CrossPairCooldownSeconds;   // 같은 쌍 재발 쿨다운 8초
                    CrossEncounters.Add(new CrossEncounter { A = a, B = b, Left = CrossStopDurationSeconds });
                    a._crossStopped = true; b._crossStopped = true;
                    a._walking = false; b._walking = false;
                    a._crossFaceTo = b.transform.position;
                    b._crossFaceTo = a.transform.position;
                    Debug.Log($"[133차-P3] 교차 정지 개시 — {a.name} × {b.name} ({CrossStopDurationSeconds:0.0}s 대면)");
                    break;   // a는 이미 정지 진입 — 다수 동시 페어링 금지(대면 목표 충돌 방지)
                }
            }
        }

        /// <summary>[133차-P3] 쌍 키 — 인스턴스 ID 오름차순 패킹(교환 불변 — 정적 딕셔너리 캐시 계약).</summary>
        private static long CrossPairKey(VillagerWanderer a, VillagerWanderer b)
        {
            int ia = a.GetInstanceID();
            int ib = b.GetInstanceID();
            return ia < ib ? ((long)ia << 32) | (uint)ib : ((long)ib << 32) | (uint)ia;
        }

        /// <summary>[133차-P3] 교차 정지 진행 — 프레임당 1회(정적 게이트). 상대 대면 Slerp 회전 + 만료 해제.</summary>
        private static void TickCrossEncountersFrame()
        {
            int f = Time.frameCount;
            if (f == _crossLastTickFrame) return;
            _crossLastTickFrame = f;

            float dt = Time.deltaTime;
            for (int i = CrossEncounters.Count - 1; i >= 0; i--)
            {
                CrossEncounter e = CrossEncounters[i];
                // 짝 상실/외부 해제(시간표 전환 등) — 양쪽 해제 후 정리(봉금 방지)
                if (e.A == null || e.B == null || !e.A._crossStopped || !e.B._crossStopped)
                {
                    if (e.A != null) e.A._crossStopped = false;
                    if (e.B != null) e.B._crossStopped = false;
                    CrossEncounters.RemoveAt(i);
                    continue;
                }
                e.Left -= dt;
                if (e.Left <= 0f)
                {
                    e.A._crossStopped = false;
                    e.B._crossStopped = false;
                    e.A._idleLeft = e.A.IdleDuration();   // 재개 시 기존 휴식 카운터 경로(즉시 재충돌 방지)
                    e.B._idleLeft = e.B.IdleDuration();
                    CrossEncounters.RemoveAt(i);
                    continue;
                }
                // 서로 대면 — 부드러운 Slerp(루트 yaw). 대화 중 시선 단일제어 계약(DialogueYawControlled) 존중.
                if (!DialogueYawControlled)
                {
                    e.A.FaceTowardSmooth(e.A._crossFaceTo);
                    e.B.FaceTowardSmooth(e.B._crossFaceTo);
                }
            }
        }

        /// <summary>[133차-P3] 대면 회전 — Slerp yaw(부드러운 선회). 상대 월드 좌표를 향해 루트만 회전.</summary>
        private void FaceTowardSmooth(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, Mathf.Clamp01(CrossTurnSlerpRate * Time.deltaTime));
        }

        /// <summary>[133차-P3] 교차 정지 강제 해제 — 시간표 전환 시 진행 상태 정리(상대 쪽은 Encounter 틱이 정리).</summary>
        private void ClearCrossStopState()
        {
            if (_crossStopped) _crossStopped = false;
        }

        /// <summary>[133차-P3] 해지 — 시간 이벤트 구독 해제 + 교차 정지 레지스트리 탈퇴(정적 캐시 유출 방지).</summary>
        private void OnDestroy()
        {
            UnhookTimeEvents();
            CrossRegistry.Remove(this);
        }

        // ── [134차 B] 배회 진단 프로브 — Init 후 10초 시점 1회 ──

        /// <summary>
        /// [134차 B] 배회 진단 프로브 — 매 틱 위치 델타를 수평 누적(moved)하고 WanderProbeAtSeconds(10s)
        /// 도달 시 1회 관측 로그. 근거(계획 항목 B): walk 클립 로드 성공·보행 캘리브레이션 0건 = Walk 상태
        /// 진입 관측 실패 → Animator 참조(_anim — 기존 Speed 세팅 필드 재사용)/컨트롤러/Speed 파라미터/
        /// 현재 상태/누적 이동거리를 단발로 직접 관측(재Play 후보 단일 특정 데이터). _paused/교차 정지 중엔
        /// Update 조기 반환으로 동결 — [98차 F1] 진단 윈도우와 동일 관측 시간 기준.
        /// </summary>
        private void TickWanderProbe(float dt)
        {
            // 이동 누적 — 프레임 델타 합(수평 성분만: 접지 스냅의 y 보정은 이동 아님 — 노이즈 제외)
            Vector3 d = transform.position - _probeLastPos; d.y = 0f;
            _probeMoved += d.magnitude;
            _probeLastPos = transform.position;

            if (_probeDone) return;
            _probeElapsed += dt;
            if (_probeElapsed < WanderProbeAtSeconds) return;
            _probeDone = true;   // 1회 발화 플래그 — 이후 누적만 유지

            // 상태명 판정 — [98차 F1] F1-2 진단의 Walk/Idle/TalkA/TalkB 매칭 패턴 준용
            bool hasCtrl = _anim != null && _anim.runtimeAnimatorController != null;
            string stateName = "null";
            if (hasCtrl)
            {
                AnimatorStateInfo st = _anim.GetCurrentAnimatorStateInfo(0);
                stateName = st.IsName("Walk") ? "Walk"
                    : st.IsName("Idle") ? "Idle"
                    : st.IsName("TalkA") ? "TalkA"
                    : st.IsName("TalkB") ? "TalkB"
                    : $"hash{st.shortNameHash}";
            }
            string animRef = _anim != null ? _anim.name : "null";
            bool onSameGo = _anim != null && _anim.gameObject == gameObject;
            string ctrl = hasCtrl ? _anim.runtimeAnimatorController.name : "null";
            float speedParam = _anim != null ? _anim.GetFloat("Speed") : 0f;
            Debug.Log($"[134차 B] 배회 프로브 — {name} animRef={animRef} onSameGo={onSameGo} " +
                      $"ctrl={ctrl} speed={speedParam:F1} state={stateName} moved={_probeMoved:F2}m " +
                      // [134차 B-2] night/hold 표기 — [133차-P3] 밤 귀가 홀드 중이면 Idle/moved=0이 정상 동작.
                      // 향후 "정지=고장" 오진단 방지(134차 실측: 전원 밤 홀드였음).
                      $"night={IsNightHolding} walkSpeed={_walkSpeed:F2} runner={_isRunner}");
        }

        // ── [98차 F1] F1-2 런타임 애니 진단 — 1Hz, 스폰 후 30s 한정 ──

        /// <summary>
        /// [98차 F1] F1-2 런타임 애니 진단(프리징 판별 데이터) — animator.enabled / anim.speed / 현재
        /// 상태명·normalizedTime / Speed 파라미터 / active clip name·isLooping. 1Hz × 스폰 후 30s 한정.
        /// 근거: 마을씬 테스트 9 — 걷기 사이클 관찰 0이 특정 시점(대화 정지→재개 사이클 유력) 이후 발생,
        /// 스트라이드 정합(발 본 실측)은 성공했으므로 애니 자체는 부분 재생 실존 — 정지 시점의 상태 스냅샷 필요.
        /// _paused 중엔 틱 자체가 동결(Update 조기 반환) — 윈도우 경과도 동결(관측 시간 기준 30s 유지).
        /// </summary>
        private void TickAnimationDiagnostics(float dt)
        {
            if (!_aniDiagActive || _anim == null) return;
            _aniDiagElapsed += dt;
            if (_aniDiagElapsed > AniDiagWindowSeconds)
            {
                _aniDiagActive = false;   // 30s 한정 종료 — 상시 저비용 대안 대비 로그 스팸 방지
                return;
            }
            _aniDiagTimer -= dt;
            if (_aniDiagTimer > 0f) return;
            _aniDiagTimer = AniDiagIntervalSeconds;

            // [117차 R4] 키실측 진단(세션당 1회) — 주민/플레이어 동일 측정법(렌더러 월드 bounds, FX 포함) 실측.
            if (!_heightCensusDone)
            {
                _heightCensusDone = true;
                float ownH = TerritoryBuilder.MeasureStandingHeight(gameObject, false);
                float playerH = 0f;
                var playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null)
                {
                    Transform pModel = null;
                    foreach (Transform c in playerGo.transform)
                        if (c.name.Contains("PlayerModel")) { pModel = c; break; }
                    if (pModel == null)
                        foreach (Transform c in playerGo.transform)
                            if (c.GetComponentInChildren<SkinnedMeshRenderer>() != null) { pModel = c; break; }
                    playerH = TerritoryBuilder.MeasureStandingHeight(pModel != null ? pModel.gameObject : playerGo, false);
                }
                Debug.Log($"[117차 R4] 키실측: {name}={ownH:F2}m 플레이어={(playerH > 0f ? playerH.ToString("F2") + "m" : "부재")} " +
                          $"비율={(playerH > 0.01f ? (ownH / playerH).ToString("F2") : "-")} (동일 측정법 — 렌더러 월드 bounds, FX 포함)");
            }

            bool hasCtrl = _anim.runtimeAnimatorController != null;
            AnimatorStateInfo state = hasCtrl ? _anim.GetCurrentAnimatorStateInfo(0) : default;
            string stateName = state.IsName("Walk") ? "Walk"
                : state.IsName("Idle") ? "Idle"
                : state.IsName("TalkA") ? "TalkA"
                : state.IsName("TalkB") ? "TalkB"
                : $"hash{state.shortNameHash}";
            string clipName = "없음";
            bool clipLoop = false;
            if (hasCtrl)
            {
                var clipInfos = _anim.GetCurrentAnimatorClipInfo(0);
                if (clipInfos != null && clipInfos.Length > 0 && clipInfos[0].clip != null)
                {
                    clipName = clipInfos[0].clip.name;
                    clipLoop = clipInfos[0].clip.isLooping;
                }
            }
            Debug.Log($"[98차 F1] 애니 진단 — t={_aniDiagElapsed:F1}s enabled={_anim.enabled} animSpeed={_anim.speed:F2} " +
                      $"state={stateName} norm={state.normalizedTime:F2} Speed={_anim.GetFloat("Speed"):F1} " +
                      $"clip='{clipName}' loop={clipLoop} walking={_walking} paused={_paused} model={_modelKey}");
        }

        // ── [98차 F1] F1-3 프리징 자가수리 — 걷기 진입 후 1.5s 셀프체크 ──

        /// <summary>[98차 F1] F1-3 걷기 진입 시 셀프체크 개시 — 발 본(FindFootBone 계약 재사용)과 루트 위치를
        /// 기록하고 FreezeCheckDelaySeconds(1.5s) 후 판정. 발 본 부재 모델은 측정 불가 — 체크 스킵(무해).</summary>
        private void BeginFreezeSelfCheck()
        {
            _freezeFoot = FindFootBone();
            _freezeFootStartValid = false;
            _freezeCheckLeft = _freezeFoot != null ? FreezeCheckDelaySeconds : 0f;
        }

        /// <summary>
        /// [98차 F1] F1-3 프리징 자가수리 — 걷기 진입 후 1.5s 셀프체크: 발 본 Δ≈0 + 루트 이동 지속 시
        /// animator.Rebind() + 오버라이드 컨트롤러 재적용(GetResolvedMixamoOverride) + Speed 파라미터
        /// 재설정(91차 Rebind 경로 확장). 근거: 테스트 9 — 주민 전원 포즈 완전 고정 + 화면 내 이동(걷기
        /// 사이클 관찰 0) — 애니 평가만 정지하고 transform 이동은 지속. 수리 로그 1회(수리 발생 시에만).
        /// </summary>
        private void TickFreezeSelfCheck(float dt)
        {
            if (_freezeCheckLeft <= 0f || _freezeFoot == null || _anim == null) return;
            if (!_freezeFootStartValid)
            {
                _freezeFootStart = _freezeFoot.position;
                _freezeRootStart = transform.position;
                _freezeFootStartValid = true;
                return;
            }
            _freezeCheckLeft -= dt;
            if (_freezeCheckLeft > 0f) return;

            float footDelta = (_freezeFoot.position - _freezeFootStart).magnitude;
            float rootDelta = Vector3.Distance(transform.position, _freezeRootStart);
            // [99차 F-C] C-3 기준 보강 — 발 본 Δ 기준에 SMR-Animator hips 불일치 거리 추가(본은 움직여도
            //   메시 동결 감지). 스트라이드 실측 성공 케이스([98차 F1] — 발 본은 구동, 렌더 동결) 커버.
            float hipsMismatch = VillageNpcSpawner.MeasureHipsMismatch(gameObject, _anim);
            bool skeletonMismatch = hipsMismatch > SkeletonHipsMismatchM;
            bool freezeDetected = footDelta <= FreezeFootEpsilonM && rootDelta >= FreezeRootMinM;
            if (freezeDetected || skeletonMismatch)
            {
                _freezeRepairs++;
                // [99차 F-C] 골격 불일치 판정이면 이름 리맵 선행(원인 직결 수리) 후 기존 Rebind 경로 실행.
                int remapped = skeletonMismatch ? VillageNpcSpawner.RemapSmrToAnimator(gameObject, _anim) : 0;
                // 91차 Rebind 경로 확장 — Rebind + 오버라이드 컨트롤러 재적용 + Speed 파라미터 재설정.
                var currentCtrl = _anim.runtimeAnimatorController;
                _anim.Rebind();
                // [103차] 주민별 인스턴스 재구성 — 이름 주입(npcName 식별 계약).
                var resolved = VillageNpcSpawner.GetResolvedMixamoOverride(currentCtrl, _modelKey + "_" + name);
                if (resolved != null) _anim.runtimeAnimatorController = resolved;
                _anim.speed = _animSpeed;
                _anim.SetFloat("Speed", _walking ? 1f : 0f);
                Debug.Log($"[98차 F1] 프리징 자가수리 — Rebind {name} (발Δ={footDelta:F3}m 루트Δ={rootDelta:F2}m " +
                          $"hipsΔ={hipsMismatch:F3}m 골격리맵={(skeletonMismatch ? remapped.ToString() : "해당없음")} " +
                          $"재적용={(resolved != null ? resolved.name : "없음")} #{_freezeRepairs})");
            }
            _freezeCheckLeft = 0f;   // 판정 완료 — 다음 걷기 진입(BeginFreezeSelfCheck)까지 휴면
        }

        /// <summary>
        /// [92차] 모델별 보폭-속도 정합 — VillageNpcSpawner가 스폰 직후 모델 키
        /// (man1/man2/girl1~3/oldman1~2/shop_npc)를 전달. [100차 F-3] 모델별 보정 캐시 우선 — 세션 내
        /// 선행 실측 성공 모델은 스폰 즉시 보정치로 개시(재실측 없음). 캐시 부재 시 테이블 초기값:
        /// girl 계열=WalkSpeedGirl(0.85), 그 외=WalkSpeedDefault(0.95) — [100차 F-3] 클립 유효 보행속도
        /// 실측(0.81~1.05m/s) 반영 하향치(영상 재실측 후 확정). Speed 파라미터 계약(>0.55 Walk 진입 /
        /// <0.35 Idle 복귀)은 무변경 — 값 1/0 플립 유지.
        /// </summary>
        public void SetWalkModel(string modelKey)
        {
            _modelKey = modelKey ?? "";   // [93차 F3] 스트라이드 정합 로그 식별용 보관
            // [101차 F-3] 베이크 테이블 우선 — 실측 확정 모델(girl1~3/man1~2/oldman1~2)은 첫 스폰부터
            //   정합(캘리브레이션 대기 시간 제거). 채택 잠금은 기존 단일 경로(LockStrideCalibration) 승계 —
            //   이후 캘리브레이션 스킵(캘리브레이션은 테이블 미지 모델 폴백 전용 계약).
            if (!string.IsNullOrEmpty(modelKey) && ModelWalkBakeTable.TryGetValue(modelKey, out float baked))
            {
                LockStrideCalibration(baked);   // [101차 F-3] 베이크 walkSpeed 채택 + 성공 잠금(유일 잠금 경로 승계)
                // [101차 F-3] 잔존 슬립 >0.1m/s 모델 한정 animSpeed 미세 트림(0.9~1.1 클램프) — 베이크로
                //   walkSpeed를 맞춘 뒤 남는 슬립을 배속 미세 트림으로 흡수. Speed 파라미터 계약(1/0 플립) 무변경.
                if (ModelResidualSlipTable.TryGetValue(modelKey, out float slip)
                    && Mathf.Abs(slip) > ResidualSlipTrimGate)
                {
                    _animSpeed = Mathf.Clamp(baked / (baked - slip), AnimSpeedTrimMin, AnimSpeedTrimMax);
                    if (_anim != null) _anim.speed = _animSpeed;   // Init 이전 호출 대비 가드(Init도 _animSpeed 재적용)
                }
                Debug.Log($"[101차 F-3] 보행 베이크 — model={modelKey} walkSpeed={_walkSpeed:F2}(테이블) animSpeed={_animSpeed:F2} 슬립목표=<0.1");
                return;
            }
            // [100차 F-3] 캐시 히트 시 보정치로 개시 — 이후 첫 걷기 진입 캘리브레이션도 캐시 경로로 확정.
            //   ([101차 F-3] 테이블 미지 모델 폴백 경로 — 캘리브레이션은 이 경로에서만 가동.)
            if (!string.IsNullOrEmpty(modelKey) && ModelWalkSpeedCache.TryGetValue(modelKey, out float calibrated))
                _walkSpeed = calibrated;
            else
                _walkSpeed = !string.IsNullOrEmpty(modelKey) && modelKey.StartsWith("girl")
                    ? WalkSpeedGirl
                    : WalkSpeedDefault;
            // [92차] 보행 정합 로그 — 스폰당 1회(Init 직후 경로에서만 호출, 프레임 스팸 없음).
            Debug.Log($"[VillagerWanderer] 보행 정합 — model={modelKey} walkSpeed={_walkSpeed}");
        }

        /// <summary>[128-2 R] 러너 지정 — walkSpeed를 RunnerWalkSpeed로 치환(스폰 경로 호출 — SetWalkModel 이후).
        /// 캘리브레이션은 유지하되 채택 상한만 러너 한정 상향(RunnerAdoptWalkSpeedMax).</summary>
        public void SetRunner(bool isRunner)
        {
            _isRunner = isRunner;
            if (isRunner) { _walkSpeed = RunnerWalkSpeed; Debug.Log($"[128-2 R] 러너 — walkSpeed={_walkSpeed:F2}(초기) calibrate상한={RunnerAdoptWalkSpeedMax:F1}"); }
        }

        // ── [93차 F3] 스트라이드-속도 자동 정합 — 첫 Walk 진입 캘리브레이션 1회 ──

        /// <summary>[93차 F3 → 96차 F4 → 100차 F-3] Walk 진입 시 실측 개시. 모델별 보정 캐시 히트 시
        /// 재실측 없이 채택 확정(세션당 1회 실측 계약). 성공 확정만 영구 잠금 — 폴백은 재시도 대기
        /// 상태(_strideFailedPendingRetry)로 전환되어 다음 걷기 사이클마다 쿨다운 경유 재시도(성공할 때까지).
        /// 첫 구간이 도착/대화로 잘려 데이터 부족으로 끝난 경우(폴백 미확정)는 기존대로 StrideMaxAttempts까지 재개시.</summary>
        private void TryBeginStrideCalibration()
        {
            // [96차 F4] 성공 확정만 영구 잠금 — 폴백은 잠금 없이 재시도 대기로 전환(영구 폴백 금지).
            if (_strideCalibrated) return;
            // [100차 F-3] 모델별 보정 캐시 재사용 — 세션당 1회 실측 계약(쿨다운/소진 게이트 이전 채택).
            //   대화 정지→재개 재실행도 이 경로로 기존 보정치를 재사용한다(재실측 없음 — 이후 재실행은
            //   walkSpeed 재조정 목적이며 animSpeed 올림은 폐지됐다).
            if (TryAdoptCachedWalkSpeed()) return;
            // [96차 F4] 쿨다운 중 — 다음 걷기 사이클 재시도 간격 보장(스팸 방지).
            if (_strideFailedPendingRetry && _strideRetryCooldownLeft > 0f) return;
            // [93차 F3] 소진 전 절단 재시도 상한(기존 계약) — 소진 후 폴백 재시도 대기면 쿨다운 경유로 계속 시도.
            if (!_strideFailedPendingRetry && _strideAttempts >= StrideMaxAttempts) return;
            _strideAttempts++;
            _strideWindowLeft = StrideCalibrationWindow;
            _strideSamples = 0;
            _strideSlideSum = 0f;
            _strideFootPrevValid = false;
            _strideFootMinY = float.PositiveInfinity;
            _strideFoot = FindFootBone();
            _stridePrevYaw = transform.eulerAngles.y;
            if (_strideFoot == null)
                FinishStrideCalibration();   // 발 본 부재 — 대기 무의미, 즉시 폴백 확정(고정값 유지)
        }

        /// <summary>[93차 F3] Walk 프레임마다 접지 슬립 수집 → 윈도우 만료 시 확정. 방법 주석은
        /// 상단 StrideCalibrationWindow 상수 블록(접지국면 발 전방 속도 = 루트 속도 − 클립 유효 보행 속도).</summary>
        private void TickStrideCalibration(float dt)
        {
            if (_strideCalibrated || _strideWindowLeft <= 0f || _strideFoot == null) return;

            // 요각속도 게이트 — 매 프레임 갱신(선회 중 슬립 샘플은 오측정이라 폐기한다)
            float yaw = transform.eulerAngles.y;
            float yawRate = dt > 0f ? Mathf.DeltaAngle(_stridePrevYaw, yaw) / dt : 0f;
            _stridePrevYaw = yaw;

            Vector3 fp = _strideFoot.position;
            float fy = fp.y;
            if (fy < _strideFootMinY) _strideFootMinY = fy;   // 윈도우 최저 발 Y 갱신(접지 기준선)

            if (_strideFootPrevValid && dt > 0f
                && fy <= _strideFootMinY + StrideContactEpsilon      // 접지국면(최저 부근)만
                && Mathf.Abs(yawRate) <= StrideTurnGateDegPerSec)    // 직진 구간만
            {
                Vector3 fvel = (fp - _strideFootPrev) / dt;
                Vector3 fwd = transform.forward;
                float slide = fvel.x * fwd.x + fvel.z * fwd.z;       // 전방 성분(수직 성분 무관)
                if (Mathf.Abs(slide) < _walkSpeed * 0.8f)            // 이상치(트랜지션 급변) 폐기
                {
                    _strideSlideSum += slide;
                    _strideSamples++;
                }
            }
            _strideFootPrev = fp;
            _strideFootPrevValid = true;

            _strideWindowLeft -= dt;
            if (_strideWindowLeft <= 0f)
                FinishStrideCalibration();   // 윈도우 만료 — 성공/실패 확정([96차 F4] 실패는 재시도 대기로 전환)
        }

        /// <summary>[93차 F3 → 96차 F4 → 100차 F-3] 실측 확정 — 캘리브레이션 방향 반전: measuredSpeed
        /// (루트 속도 − 슬립 평균 = 클립 유효 보행속도)를 walkSpeed에 채택하고 animSpeed는 1.0 고정 회귀
        /// (구 anim.speed=목표속도/실측속도 배속은 테스트 11 문워크 원인으로 폐기 — StrideAnimSpeedMax
        /// 남용 제거). 성공 시 모델별 캐시 저장(세션당 1회 실측 — 재실행은 캐시 채택) + 영구 잠금.
        /// 실패(샘플 부족/비정상 분모) 시 기존 고정값 유지 + [96차 F4] 재시도 대기 전환 — 다음 걷기
        /// 사이클마다 쿨다운 경유 재시도(영구 폴백 금지).</summary>
        private void FinishStrideCalibration()
        {
            _strideWindowLeft = 0f;
            if (_strideSamples >= StrideMinSamples)
            {
                float slideMean = _strideSlideSum / _strideSamples;
                float measuredSpeed = _walkSpeed - slideMean;   // 실측속도(m/s) — 클립 유효 보행 속도
                if (measuredSpeed > _walkSpeed * 0.25f)         // 분모 방어 — 붕괴치는 측정 오류로 간주
                {
                    // [100차 F-3] 캘리브레이션 방향 반전 — measuredSpeed(클립 유효 보행속도)를 walkSpeed에
                    //   채택(속도를 애니에 맞춤). 근거: 테스트 11 실측 — animSpeed 1.33~1.59(상한 1.8)를
                    //   부여해도 슬립 0.34~0.52m/s 잔존 = 문워크. 클램프는 실측 이탈치 방어.
                    float adopted = Mathf.Clamp(measuredSpeed, StrideAdoptWalkSpeedMin, _isRunner ? RunnerAdoptWalkSpeedMax : StrideAdoptWalkSpeedMax);
                    LockStrideCalibration(adopted);
                    if (!string.IsNullOrEmpty(_modelKey))
                        ModelWalkSpeedCache[_modelKey] = adopted;   // 모델별 캐시 — 세션당 1회 실측(재실행 채택용)
                    Debug.Log($"[100차 F-3] 보행정합 — model={_modelKey} walkSpeed={_walkSpeed:F2} animSpeed={_animSpeed:F2} 슬립={slideMean:F2}m/s (실측 {measuredSpeed:F2}m/s · 샘플 {_strideSamples} · 목표=walkSpeed정합 · 채택)");
                    return;
                }
            }
            // [96차 F4] 폴백 — 영구 잠금 폐기: 기존 고정값 유지(walkSpeed 테이블 성인 0.95/girl 0.85 [100차 F-3]
            //   하향 초기값 + anim.speed 1.0 계약) + 다음 걷기 사이클 재시도 대기(성공할 때까지 주기 재시도 —
            //   쿨다운으로 스팸 방지). 재시도 소진(3회) 후에도 동일 — 폴백이 곧 잠금이던 93차 계약을 [96차 F4]에서 폐기.
            _strideFailedPendingRetry = true;
            _strideRetryCooldownLeft = StrideRetryCooldownSeconds;
            Debug.Log($"[VillagerWanderer] 스트라이드 정합 — model={_modelKey} animSpeed={_animSpeed:F2} (폴백 — 실측 실패: 발 본={(_strideFoot != null ? "있음" : "부재")} 샘플 {_strideSamples})");
        }

        /// <summary>[100차 F-3] 보정 확정 잠금 — 채택 walkSpeed 적용 + animSpeed 1.0 고정 회귀(배속 폐지) +
        /// 성공 잠금/재시도 대기 해제([96차 F4] 성공 경로 유일 잠금 계약 승계 — 보정 확정 잠금문은 소스에서
        /// 이 곳 단 1곳, 폴백 경로는 호출 금지).</summary>
        private void LockStrideCalibration(float adoptedSpeed)
        {
            _walkSpeed = adoptedSpeed;
            _animSpeed = 1.0f;                       // [100차 F-3] 1.0 고정 회귀 — 배속 보정 폐지
            if (_anim != null) _anim.speed = _animSpeed;
            _strideCalibrated = true;                // [96차 F4] 성공 확정 잠금(유일 지점 — 테스트 계약)
            _strideFailedPendingRetry = false;       // [96차 F4] 성공 — 재시도 대기 해제(보정 확정 잠금)
        }

        /// <summary>[100차 F-3] 모델별 보정 캐시 채택 — 세션 내 실측 성공 보정치가 있으면 walkSpeed에 채택하고
        /// 보정 확정 처리한다(재실측 없음 — 세션당 1회 실측 계약). 대화 정지→재개 재실행도 이 경로로 기존
        /// 보정치를 재사용한다. 캐시 부재(첫 실측 대상 모델)면 false — 기존 실측 경로 진행.</summary>
        private bool TryAdoptCachedWalkSpeed()
        {
            if (string.IsNullOrEmpty(_modelKey) || !ModelWalkSpeedCache.TryGetValue(_modelKey, out float cached))
                return false;
            LockStrideCalibration(cached);
            Debug.Log($"[100차 F-3] 보행정합 — model={_modelKey} walkSpeed={_walkSpeed:F2} animSpeed={_animSpeed:F2} 슬립=캐시재사용 (세션당 1회 실측 계약 — 모델 보정치 채택)");
            return true;
        }

        /// <summary>[93차 F3] 발 본 탐색 — 믹사모 리그 계약(LeftFoot 우선, "Foot" 포함 임의 본 폴백). 부재 시 null.</summary>
        private Transform FindFootBone()
        {
            Transform left = null, any = null;
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                string n = t.name;
                if (n.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (left == null && n.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0) left = t;
                if (any == null) any = t;
            }
            return left != null ? left : any;
        }

        /// <summary>다음 배회 목표 — 마을 중심 반경 내 랜덤(현재 위치에서 2.5m 이상, 최대 8회 시도).</summary>
        private void PickTarget()
        {
            for (int i = 0; i < 8; i++)
            {
                double ang = _rng.NextDouble() * 360.0;
                double rr = 3.0 + _rng.NextDouble() * (_radius - 3.0);
                var p = _center + new Vector3(
                    Mathf.Cos((float)(ang * Mathf.Deg2Rad)) * (float)rr, 0f,
                    Mathf.Sin((float)(ang * Mathf.Deg2Rad)) * (float)rr);
                if ((p - transform.position).magnitude > 2.5f)
                {
                    _target = p;
                    return;
                }
            }
            _target = _center;   // 폴백 — 광장으로 복귀
        }

        private float IdleDuration() => 1.5f + (float)_rng.NextDouble() * 4.0f;

        /// <summary>
        /// [92차 후속 F-D] 전방 장애물 히트 — 목표 방향 재선정(관통 완화, 최소 변경). 현재 전방 기준
        /// 35~65° 좌우 교대 회전 지점을 새 목표로 삼는다(랜덤 재추첨은 막힌 방향을 반복 선택할 수 있어
        /// 회전 확정 — 기존 선회 보간/Speed 계약 무변경). 회전 지점이 배회 반경 밖이면 기존
        /// PickTarget(결정론 랜덤 재추첨)으로 폴백 — 마을 반경 내 유지 계약.
        /// </summary>
        private void RerouteAroundObstacle()
        {
            _avoidTurnLeft = !_avoidTurnLeft;
            float turnDeg = 35f + (float)_rng.NextDouble() * 30f;   // 35~65°
            if (_avoidTurnLeft) turnDeg = -turnDeg;
            Vector3 rerouted = transform.position
                + Quaternion.Euler(0f, turnDeg, 0f) * transform.forward * 3f;
            Vector3 fromCenter = new Vector3(rerouted.x - _center.x, 0f, rerouted.z - _center.z);
            if (fromCenter.magnitude > _radius)
            {
                PickTarget();   // [92차 후속 F-D] 폴백 — 기존 결정론 랜덤 재추첨(반경 내)
                return;
            }
            _target = rerouted;
        }
    }
}
