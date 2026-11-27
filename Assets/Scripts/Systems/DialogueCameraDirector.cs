using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.Systems
{
    /// <summary>
    /// [73차 Phase B] 대화 카메라 확대(정적 디렉터) — 대화하기 클릭 → 대상 기준 카메라 전환,
    /// 대상 병사를 플레이어 쪽으로 시선 회전(대화 자세), 종료 시 원래 카메라 복원.
    ///
    /// [92차 결함3 — 스왑 폐기, 최우선 수리] 구현 방식 전환: 신규 GameObject 카메라 생성
    /// (신규 카메라 오브젝트 생성 + 파라미터 복사 + MainCamera 태그 교체 + 구카메라 enabled=false 스왑)은
    /// 마을씬 테스트 2.mp4 포렌식에서 대화 세션 2회 동안 게임뷰 11.8초 전면 동결(픽셀 diff 0.000)을
    /// 유발 — 동결 시작이 Begin#1과 정확히 일치, End#1 re-enable로도 회복 불가(카메라 스왑 경합/
    /// 렌더 순서 불안정). → <b>기존 Main Camera를 직접 제어</b>: Begin 시 TopDownCameraController만
    /// enabled=false + position/rotation/FOV/near 저장 → 같은 카메라 트랜스폰을 대화 프레이밍으로
    /// 이동 → End 시 원복 + 컨트롤러 재개. <b>카메라 개수 불변</b> → 태그/depth/렌더 순서 경합 원천 제거.
    ///
    /// [92차 결함3 → 92차 후속 F-A] 전신 프레이밍(주민만): fullBody=true 오버로드 — FOV 40 고정,
    /// <b>bounds 불신 1.8m 고정 계약</b>: frameH=1.8m(프로젝트 캐릭터 키 계약 — 전 캐릭터 1.8m),
    /// center = target.position + Vector3.up×0.9, dist = (1.8×0.5)/tan(20°)×1.18 ≈ 2.92m.
    /// 구 bounds 산식 폐기 — 근거: 렌더 상태 Begin 로그 3세션 전부 pos y=6.3~6.8(프레이밍 적용 후 값 —
    /// 1.8m NPC라면 center.y≈0.9 기대) → 주민 SkinnedMeshRenderer bounds 비대(center.y≈6.8·size.y≈13m
    /// 추정)가 fullBody 프레이밍을 dist≈22m·높이 6.8m로 발사 → 영상 25.5s NPC 화면 높이 5~7% =
    /// "정면 안뜸" 체감. MeasureBounds는 프레이밍 경로에서 제거(진단 로그 boundsY 전용 — 비대 근본 확정용).
    /// 카메라 = 플레이어→대상 <b>정면</b>(SightYaw 25° 사선 폐기) 방향 dist 지점 + 높이 = 고정 center,
    /// LookAt = 고정 center(매 프레임 ReframeCamera 재조준 갱신). fullBody=false(병사/영주 기존 경로):
    /// 2.0m 머리 앵커 + SightYaw 25° 사선(계약 무변경).
    ///
    /// [94차 F1] 대상 고정 완성(주민 fullBody 한정): ①Begin에서 고정 베어링 1회 확정(플레이어→대상
    /// 방향 = NPC 정면 — NPC 응시 회전과 일치, 플레이어 부재 시 targetT.forward 폴백) 후 세션 내 불변 —
    /// 카메라 = 대상 center + 베어링×2.92m, 높이 = center.y + 1.2m(약상반), LookAt = 대상 center.
    /// ②ReframeCamera fullBody 분기 단순화 — dir 재투영 폐기(측면/등 잔존 원인) → 고정 베어링 유지 +
    /// 매 프레임 LookAt 갱신(대상 이동 추적). 구 90° 클립 회전 폐기(fullBody 한정 — 병사/영주 경로 무변경).
    /// ③최종 적용권 이전 — 업데이터 Update→LateUpdate(PlayerMovement 93차 게이트보다 나중 실행 —
    /// 게이트 코드는 이중 안전으로 유지). ④End 원복 유지(93차 계약) + 베어링 정적 필드 클리어.
    ///
    /// [96차 F1] 대화 카메라 플레이어 가림 수리(주민 fullBody) — 94차 정중앙 배치의 기하학적 결함 수리:
    /// 카메라 = center + 베어링×2.92m + 측면 오프셋 perpendicular(bearing의 Y축 90° 회전)×0.9m,
    /// 높이 = center.y + 0.7m(1.2→0.7 — 옆에서 약간 내려다보는 3/4 뷰). 근거(영상6 실측): 정중앙 배치면
    /// 카메라↔NPC 시선(2.92m)상 플레이어(NPC 앞 0.9m)는 2.0m 지점 — 시선 y@2.0m≈3.8 &lt; 플레이어 머리 4.3
    /// 관통 → 플레이어 등/머리가 프레임을 채움. 측면 오프셋으로 시선이 플레이어 몸통을 피해 NPC 정면 3/4가
    /// 프레임에 들어온다. LookAt=center 유지(대상 고정 추적), ReframeCamera/LateUpdate 틱/게이트/End 원복
    /// 계약 전부 무변경(94차 성공분 보존).
    ///
    /// [99차 F-B] 대화 카메라 플레이어 시선축 배치 반전 — [96차 F1] 배치(카메라 = center + 베어링×2.92m)는
    /// toTarget=target−player(플레이어→NPC) 방향의 +측에 카메라를 세움 = NPC 반대편(뒤) 설치 = NPC 등 촬영
    /// (마을씬 테스트 10 영상 01:08~01:15/01:22~01:25 클로즈업 — NPC 등/측후면 + 하체 절단 실측). 수리:
    /// ①B-1 camPos = center − bearing×dist(플레이어 측 — NPC 정면 촬영; bearing 정의(플레이어→NPC)와
    /// 세션 내 불변 계약 유지) ②B-2 camPos.y = max(center.y + FullBodyCamHeightUpM, 플레이어 y + 2.3m) —
    /// 플레이어가 카메라와 NPC 사이에 있어도 머리 위 넘어 촬영 ③B-3 Begin 시 전신 프레임 실측 로그
    /// ([99차 F-B] 전신프레임 — boundsY/화면상단·하단여유 추정) ④B-4 측면 오프셋 0.9m 유지(3/4 뷰 계약).
    /// 병사/영주 5s 타이머 경로 무변경.
    ///
    /// [100차 F-2] 카메라 정면 보정 — [99차 F-B] 배치(반전+가드)로 근접했으나 클로즈업에서 모자/상체
    /// 위주+하체 절단+정면 미노출(측후면 느낌 — 마을씬 테스트 11 영상 33~42s 실측). 수리: ①높이 가드 완화
    /// playerY+2.3 → playerY+1.75(머리 1.7m+여유)와 center.y+0.6 중 큰 값(구 2.3m 가드가 4.3m 하이 앵글
    /// 지배 — 정면 얼굴이 프레임 상단 밖/측면으로 치우침) ②측면 오프셋 0.9 → 0.45m(3/4 뷰 유지 — 정면에
    /// 가깝게) ③LookAt = center + up×0.15m(머리 쪽 조준 — Begin/ReframeCamera 양쪽 일관) ④전신프레임
    /// 실측 하단 기준 bounds minY → Foot 본 최하단 y(스킨바운드 부양 방어 — 실측 minY=1.85는 발보다 높음,
    /// 발 본 부재 시 bounds 폴백) ⑤가림 실측 로그 [100차 F-2] 가림 — 카메라→NPC 머리/중심/발 3점 Ray에
    /// 플레이어 태그 히트 시 기록(Begin 1회). 병사/영주 경로 무변경.
    ///
    /// [103차 IMPL-B — P2 대화 카메라 3요구 수리] ①NPC 정면 ②전신 프레임 ③플레이어 비노출
    /// (fullBody=주민 경로 전용 — 병사/영주 0변경). 근거(실측): 주민 루트가 지하 y≈-1.7로 가라앉은 채
    /// ([103차 P1] 접지 근원 — IMPL-A 소관 진단 로그) 발 본 월드 y는 +2.0 유지 → 구 center(root.y+0.9)가
    /// 지하(≈-0.8)를 조준해 얼굴 화면 밖/상반신만 촬영. 수리: F-1 center를 가시 발(Foot 본) 기준으로
    /// 재정의(ComputeFullBodyCenter 단일소스 — 실측 실패 시 기존 root+0.9 폴백), F-2 플레이어 렌더러
    /// 비노출(Begin 숨김 → End 역순 복원 — 콜라이더/Physics 무변경), F-3 완전 정면화(측면 오프셋
    /// 0.45→0.0 + 높이 가드 폐기 → camPos.y = center.y+0.6m 단일식), F-4 NPC 정면 보장(Begin 룩앳 유지 +
    /// LateUpdate 틱 재적용 + 정면각 판정 로그), F-5 가림 로그 정합(플레이어숨김=ON 주석 — 히트 기대 아님).
    ///
    /// 계약:
    ///  ① Begin — Camera.main(테스트 오버라이드 MainCameraOverride 유지) 참조/position/rotation/FOV/near 저장
    ///     → 컨트롤러 정지 → 대화 프레이밍 적용 ② 대상 시선 회전 ③ DialogueStarted 발화.
    ///  ② End — 트랜스폼/FOV/near 원복 + 컨트롤러 재개 + DialogueEnded 발화(멱등).
    ///  ③ 자동 종료 — [97차 F2 → 105차 B] 단일화: 패널 닫힘 폴링(전 대상 — 병사/영주도 fullBody 통일로
    ///     동일 수명), 5s 타이머·이동키 종료 폐기, ESC만 수동 종료 유지. 세션 수명 = 대화창 수명
    ///     ("대화가 끝날 때까지 NPC를 비춰라"). 공통: 유예(0.8s)+대상 파괴/사망 감시.
    ///  ④ 대상 사망/파괴 시 즉시 End. TopDownCameraController 타입 부재 씬(병사 테스트 등)은
    ///     GetComponent 탐색으로 스킵(예외 금지). AudioListener 부여 없음(기존 리스너 계약).
    ///
    /// [104차 F2 — NPC 시선 전환 수리] 근거(영상 테스트14 36~38s + Editor.log): [103차 F-2] 정면각 로그는
    /// "각=0.0° OK"(루트 forward는 플레이어 정면)인데 영상은 NPC가 측면 응시 → 루트 yaw와 가시 신체
    /// facing 불일치 = 골격 체인 내부 회전 오프셋([104차 F3] 골격루트 구동 과잉 — 부상+요각 동반 실측).
    /// 수리: ①Begin(fullBody 한정) 원본 yaw 기록(_gazeSavedYaw) → ②시선 회전을 yaw 전용으로 재정의
    /// (ComputeFacingYaw 순수 헬퍼 + ApplyDialogueFacingYaw — AngleAxis yaw만 회전, 피치/롤 보존, 멱등)
    /// ③LateUpdate 매 틱 유지(기존 [103차 F-4] LookRotation 재적용을 yaw 전용 헬퍼로 교체 — fullBody
    /// 전용 블록이라 병사/영주 무영향) ④End 원본 yaw 복원 + 게이트 해제 ⑤VillagerWanderer 대화 중
    /// yaw 제어권 위임 게이트(DialogueYawControlled — 멱등). 병사/영주(fullBody=false) 경로 0변경.
    ///
    /// [104차 F3 연동 — LateUpdate 실행순서] 골격루트 접지 보상(VillagerWanderer.LateUpdate)이 애니메이터
    /// 평가 직후 발 본을 바인드 오프셋으로 되돌린다 → 본 클래스 LateUpdate가 그 "보상 후" 자세를 봐야
    /// Foot 앵커 center([103차 F-1])가 지면 근방을 조준한다 → DefaultExecutionOrder(10)로 본 업데이터를
    /// 기본 순서보다 나중에 실행(최종 적용권 강화 — [94차 F1] LateUpdate 전환의 연장).
    ///
    /// [105차 IMPL-B — 대화 카메라 전 대상 일반화] 주민 계약(104차 정면 전신+시선+플레이어 숨김)을 병사/영주에도
    /// 동일 적용. ①진입 전환: 2인자 일반화 오버로드(구 병사/영주 사선 경로)가 fullBody:true로 위임 전환 +
    /// 3인자 최종 경로 기본값 true(구 false 폐기 — false는 명시 옵트인 레거시 사선으로만 잔존).
    /// Begin(GuardPlaceholder) 1인자와 NPCAmbientDialogue 오버로드는 시그니처·호출 문자열 무변경.
    /// ②시선 yaw 확장: [104차 F2] 기구(Begin 원본 yaw 기록 → LateUpdate 매 틱 적용 → End 원복)를 fullBody
    /// 전체로 확장 — 비주민(병사/영주, VillagerWanderer 부재)도 동일. VillagerWanderer.DialogueYawControlled
    /// 게이트 자체는 주민 경로 보존(폐기하지 않음). ③종료 단일화: [97차 F2] 병사/영주=5s 타이머+이동키 분기는
    /// fullBody 통일로 무의해져 폐기 — 패널 닫힘 폴링 단일 경로(병사 세션 수명 = NPCDialoguePanelUTK
    /// OpenForGuard 패널 수명 — DialoguePanelOpenOverride 공급). ④병사/영주 Foot 본 유무 차이는
    /// ComputeFullBodyCenter 폴백(root+0.9)이 커버([103차 F-1] 계약 유지).
    /// </summary>
    [DefaultExecutionOrder(10)]   // [104차 F3 연동] Wanderer 접지 보상(LateUpdate) 이후 실행 — Foot 앵커가 보상 후 자세를 조준
    public class DialogueCameraDirector : MonoBehaviour
    {
        public const float DialogueDistance = 2.0f; // 대상 앞 카메라 거리(m) — 병사/영주 기존 경로
        public const float InputGraceSeconds = 0.8f;   // [91차 F5] 입력 종료 유예(초) — Begin 직후 소실 방지
        private const float HeightLift = 0.35f;     // 눈높이 위언덕 — "약간 위"
        private const float SightYaw = 25f;         // 사선(3/4 뷰) 꺾각 각도 — 병사/영주 경로 전용([92차] 주민 폐기)
        private const float ClipAvoidMinDistance = 0.9f;   // [91차 F5] 클립 방지 최소 거리(m) — 플레이어까지
        // [92차 결함3 → 92차 후속 F-A] 전신 프레이밍(주민) — FOV 40° 고정.
        //   dist = (frameH×0.5)/tan(FOV/2) × 1.18 — frameH는 1.8m 고정 계약(FullBodyFixedFrameH):
        //   (1.8×0.5)/tan(20°)×1.18 ≈ 2.92m(전신 프레임 상하 여유 18% — 프러스텀 실측 계수).
        public const float FullBodyFov = 40f;
        private const float FullBodyFrameFactor = 1.18f;
        // [92차 후속 F-A] bounds 불신 고정 계약 — 프레임 높이/center 오프셋을 bounds와 무관하게 고정.
        //   근거: 렌더 상태 Begin 로그 3세션 전부 pos y=6.3~6.8(프레이밍 적용 후 값 — 1.8m NPC라면
        //   center.y≈0.9 기대) → 주민 SkinnedMeshRenderer bounds 비대(center.y≈6.8·size.y≈13m 추정,
        //   mesh-space bounds 비대 의심) → fullBody 프레이밍이 dist≈22m·높이 6.8m로 발사 → 영상 25.5s
        //   NPC 화면 높이 5~7% = "정면 안뜸" 체감. → frameH=1.8m(프로젝트 캐릭터 키 계약 — 전 캐릭터
        //   1.8m)/center=target.position+0.9up/dist=(1.8×0.5)/tan(20°)×1.18≈2.92m 고정. MeasureBounds는
        //   프레이밍 경로에서 제거(진단 로그 boundsY 전용 — 비대 근본 확정용).
        private const float FullBodyFixedFrameH = 1.8f;
        private const float FullBodyCenterUpM = 0.9f;
        // [96차 F1 → 100차 F-2 → 103차 F-3] 카메라 높이 = center.y + 0.6m — [103차 F-3] 단일식(구
        //   max(center.y+0.6, 플레이어 y+1.75) 가드 폐기 — 아래 FullBodyPlayerHeadClearM 폐기 사유 참조).
        //   히스토리: 구 1.2m 약상반은 정중앙 배치와 조합되면 시선이 플레이어 머리(≈4.3 world)를 관통 — 영상6
        //   실측 → 0.7 → 0.6 완화([100차 F-2] 하이 앵글 완화로 정면 얼굴 프레임 진입).
        private const float FullBodyCamHeightUpM = 0.6f;
        // [99차 F-B → 100차 F-2 → 103차 F-3 폐기] B-2 비가림 상향 기준 FullBodyPlayerHeadClearM(1.75m) —
        //   플레이어 y + 1.75m 높이 가드. [103차 F-3] 폐기 사유: [103차 F-2] 플레이어 렌더러 숨김(콜라이더
        //   유지)으로 가림 원천이 제거됨 → 플레이어 y 기반 상향 가드는 불요 + 정면 계약을 해치는 하이 앵글
        //   요인으로 판정되어 상수·사용처 동시 폐기(ComputeFullBodyCamPos 높이는 center.y + 0.6m 단일식).
        //   구 근거(히스토리 보존): B-1 반전으로 카메라가 플레이어 측에 설치되므로, 플레이어가 카메라와 NPC
        //   사이에 있어도 머리 위 넘어 촬영한다. [100차 F-2] 가드 완화(구 2.3m) — 2.3m 가드가 4.3m 하이 앵글을
        //   지배해 정면 얼굴 노출을 막았음(마을씬 테스트 11 실측 — 모자/상체 위주+하체 절단+정면 미노출).
        // [96차 F1 → 100차 F-2 → 103차 F-3] 측면 오프셋(m) — 카메라를 NPC 정면축에서 perpendicular(bearing의
        //   Y축 90° 회전) 방향으로 떼어내는 오버숄더. 히스토리: 구 0.9m → 0.45m 완화([100차 F-2] — 3/4 뷰
        //   유지하되 정면에 가깝게 — 정면 얼굴 노출). 근거([96차 F1]): 정중앙 배치면 카메라↔NPC 시선상
        //   플레이어(NPC 앞 0.9m)는 2.0m 지점 — 시선 y@2.0m≈3.8 < 플레이어 머리 4.3 관통 → 플레이어 등/머리가
        //   프레임을 채움(영상6 실측). [103차 F-3] 완전 정면화 — 0.45 → 0.0(가림 원천은 [103차 F-2] 플레이어
        //   렌더러 숨김으로 제거 — 완전 정면 계약). sideDir×오프셋 곱 구조는 유지(0.0 대입만 — 합성 산식
        //   단일소스 보존).
        private const float FullBodySideOffsetM = 0.0f;
        // [100차 F-2] LookAt 상향 바이어스(m) — 조준점 = center + up×0.15(머리 쪽 — 얼굴 중심 프레이밍).
        //   Begin/ReframeCamera 양쪽 일관(단일 상수 — 두 경로가 어긋나면 프레임이 튄다).
        private const float FullBodyLookAtUpM = 0.15f;
        // [100차 F-2] 가림 실측 머리 프로브 높이(m) — 카메라→NPC 머리 Ray 도달점(캐릭터 키 계약 1.8m 머리 근방).
        private const float FullBodyHeadProbeM = 1.7f;
        private const float NearPlaneFix = 0.15f;   // [92차 결함3] near plane 보정(저장/원복 포함) — 근접 클립 방지

        // ── [130차] 상반신 클로즈업(획득 연출 — AcquisitionCinema 진입점) ──
        // 전신 기계(억제 게이트/저장 원복/고정 베어링/패널 폴링 End/yaw 기록·원복) 완전 재사용,
        // 프레이밍 수치만 상수 교체 + 플레이어 렌더러 숨김 스킵(촬영 대상이 플레이어).
        public const float UpperBodyFrameH = 1.05f;       // 상반신 프레임 높이(머리~가슴 + 손에 든 아이템)
        public const float UpperBodyCenterLiftM = 0.35f;  // center = 발앵커(0.9) + 0.35 ≈ 가슴 1.25m
        public const float UpperBodyCamHeightUpM = 0.35f; // 카메라 높이 = center.y + 0.35(눈높이 근접 — AC 톤)
        private static bool _upperBodyMode;               // 상반신 세션 진행 플래그(BeginUpperBody=true, End 클리어)

        /// <summary>[73차 Phase B] 대화 카메라 시작 — 패널(InteractionPanelUTK 대화하기)이 선행 호출.
        /// [89차] 타입 일반화 — GuardPlaceholder → Component(마을 주민 NPCAmbientDialogue 지원).</summary>
        public static event System.Action<Component> DialogueStarted;

        /// <summary>[73차 Phase B] 대화 카메라 복원 — 타이머/입력/사망/수동 End 포함 모든 종료 경로.
        /// [89차] 타입 일반화 — GuardPlaceholder → Component.</summary>
        public static event System.Action<Component> DialogueEnded;

        // ── 저장/복원 상태(정적 — Begin/End 계약의 단일소스) ──
        // [92차 결함3] 스왑 폐기 — 신규 카메라 필드/GO 참조 제거. _dialogueCam = 제어 중인 기존 카메라 참조.
        private static Camera _savedCamera;         // 제어 대상 카메라(Camera.main — 테스트 오버라이드 경유)
        private static Vector3 _savedPos;
        private static Quaternion _savedRot;
        private static float _savedFov;             // [92차 결함3] FOV 저장/원복
        private static float _savedNear;            // [92차 결함3] near plane 저장/원복
        private static bool _savedOrtho;            // [125차 후속10] 직교 모드 저장(원경/직교 이중 경로 판정용)
        private static float _savedOrthoSize;       // [125차 후속10] 직교 크기 저장/원복 — 직교 카메라(테스트씬 탑다운) 대응
        private static TopDownCameraController _savedController;   // [92차 결함3] 컨트롤러 정지/재개 대상
        private static bool _hadController;         // 컨트롤러 존재 플래그(부재 씬 스킵 계약)
        private static Camera _dialogueCam;         // [92차] 제어 중인 기존 카메라(=Camera.main — 신규 생성 없음)
        private static bool _fullBody;              // [92차 결함3] 전신 프레이밍 모드(주민)
        private static float _fullBodyDist;         // [92차 결함3] 전신 프레임 거리(m) — Begin 확정, Reframe 유지
        private static Vector3 _fullBodyBearing;    // [94차 F1] fullBody 고정 베어링 — Begin 1회 확정(플레이어→대상), 세션 내 불변(End 클리어)
        private static Component _target;   // [89차] GuardPlaceholder → Component 일반화(주민 지원)
        private static Quaternion _targetSavedRot;
        // [104차 F2] 시선 yaw 세션 상태(fullBody=주민 전용) — Begin에서 원본 yaw 기록 → 세션 내 Director가
        //   yaw 단일 제어(ApplyDialogueFacingYaw 매 틱) → End에서 원본 yaw 복원 + 상태 클리어(멱등).
        //   _targetSavedRot(전체 회전 원복 — 기존 계약)과 별도 명시 상태 — 시선 세션 판정/테스트 어서션 대상.
        private static float _gazeSavedYaw;
        private static bool _gazeYawCaptured;

        // [118차 후속2 — 대화 카메라 가시 신체 정면] 골격 체인 내부 회전 오프셋 대응 — 루트 yaw만
        //   플레이어 향하게 해도 가시 신체(mixamo 스켈레톤 Hips 본)가 클립/리그 내부 yaw로 다른 방향을
        //   봐서 "정면각 OK인데 등이 보인다"가 재발(104차 진단 '골격 체인 내부 회전 오프셋'의 미수리 잔여).
        //   해법 = 클로즈드루프 서보: 매 틱 Hips 본의 월드 forward 요(yaw)를 실측해 루트에 부족분(delta)만
        //   가산 → 몸통 월드 yaw가 항상 목표에 수렴(애니 상태가 바뀌어도 자기수정). End에서 원본 yaw 복원
        //   (기존 계약) + 본 캐시 해제.
        private static Transform _gazeHipsBone;
        private static bool _gazeHipsResolved;

        /// <summary>Hips 본 해석(세션 1회 캐시) — 휴머노이드 Animator 우선, 제너릭 리그는 이름 폴백("Hips"/"Root").</summary>
        private static Transform ResolveGazeHipsBone(Transform targetT)
        {
            if (_gazeHipsResolved) return _gazeHipsBone;
            _gazeHipsResolved = true;
            if (targetT == null) return null;
            var animator = targetT.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var hips = animator.GetBoneTransform(UnityEngine.HumanBodyBones.Hips);
                if (hips != null)
                {
                    _gazeHipsBone = hips;
                    return _gazeHipsBone;
                }
            }
            foreach (var t in targetT.GetComponentsInChildren<Transform>())
            {
                if (t == targetT) continue;
                if (t.name == "Hips" || t.name == "Root")
                {
                    _gazeHipsBone = t;
                    break;
                }
            }
            return _gazeHipsBone;
        }

        /// <summary>가시 신체의 월드 요(yaw) — Hips 본 forward 수평 투영(Unity 규약 Atan2(x,z)). 판정 불가 시 NaN.</summary>
        private static float ComputeVisibleBodyYaw(Transform targetT)
        {
            var hips = ResolveGazeHipsBone(targetT);
            if (hips == null) return float.NaN;
            Vector3 fwd = hips.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) return float.NaN;   // 퇴화 — hips forward가 수직(비정형 리그)
            return Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }
        private static float _elapsedSinceBegin;   // [91차 F5] 유예 판정용 Begin 경과시간
        private static bool _active;
        private static DialogueCameraDirector _updater;

        // [103차 F-2] 플레이어 렌더러 숨김 스냅샷 — Begin(fullBody) 캡처 → End 역순 복원(복원 후 클리어 — 멱등).
        //   enabled = 복원 대상(본 수리가 변경하는 유일 속성), shadowCastingMode = 스냅샷 전용(본 수리 미변경 —
        //   진단/향후 확장 여유). 콜라이더/Physics는 일절 건드리지 않는다(가림 Ray 진단 LogOcclusionCheck 유지).
        private static readonly System.Collections.Generic.List<Renderer> _playerRendererSnapshot =
            new System.Collections.Generic.List<Renderer>();
        private static readonly System.Collections.Generic.List<bool> _playerRendererSnapshotEnabled =
            new System.Collections.Generic.List<bool>();
        private static readonly System.Collections.Generic.List<UnityEngine.Rendering.ShadowCastingMode> _playerRendererSnapshotShadow =
            new System.Collections.Generic.List<UnityEngine.Rendering.ShadowCastingMode>();

        public static bool IsActive => _active;
        public static Camera DialogueCamera => _dialogueCam;   // [92차] 제어 중인 기존 카메라(End 후 null)
        public static Component Target => _target;   // [89차] Component 일반화(소비처 0 실측 — 안전)

        /// <summary>
        /// [테스트 주입 지점] Camera.main 대체 제공자 — EditMode 배치 환경의 Camera.main 해상 불안 회피
        /// (BowAimAlignmentTests가 카메라를 직접 주입하는 선례와 동일 취지). 프로덕션은 항상 null(=Camera.main).
        /// </summary>
        public static System.Func<Camera> MainCameraOverride;

        /// <summary>
        /// [97차 F2] 대화 패널 개방 실측 주입 지점 — fullBody(주민) 세션 수명이 NPCDialoguePanelUTK 패널에
        /// 연동된다(패널 닫힘 → End). 프로덕션 공급처 = NPCDialoguePanelUTK.EnsureAttached 등록 위임
        /// (Systems asmdef는 UI 역참조 금지 — UI 측 등록). EditMode에서 UI 루트 부착 없이 패널 수명을
        /// 시뮬레이션(MainCameraOverride 선례 동일 취지).
        /// </summary>
        public static System.Func<bool> DialoguePanelOpenOverride;

        /// <summary>
        /// [98차 F2] F2-2 대화 패널 UI 실측 진단 공급 — UI 측 등록(DialoguePanelOpenOverride 선례 동일 방식,
        /// Systems asmdef는 UI 역참조 금지 — UI→Systems 단방향 계약). Director의 End 원인 진단 로그가
        /// _instance 존재/IsOpen/resolvedStyle.display/parent 부첞性을 UI 실측 문자열로 수령한다.
        /// 미등록 씬 = "진단미등록"(진단 생략 — 동작 무영향).
        /// </summary>
        public static System.Func<string> DialoguePanelOpenDiagnostics;

        // [98차 F2] F2-2 End 진입 원인 진단 상태 — 세션 내 최후 분기 기록 + 1회/세션 가드(Begin 리셋).
        private static string _lastEndCause = "미기록";
        private static bool _endCauseLogged;

        // ── Begin / End ──

        /// <summary>[73차 Phase B → 89차 위임 → 105차 B] 병사 대화 카메라 시작 — Component 일반화 오버로드로 위임
        /// (위임 경유 fullBody=true — 병사도 전신 정면 통일). InteractionPanelUTK 소스의
        /// "DialogueCameraDirector.Begin(talkGuard)" 문자열 계약 유지(DialogueFeedbackTests 어서션).</summary>
        public static void Begin(GuardPlaceholder target)
        {
            if (target == null) return;
            // [93차 F1] 병사 경로도 아래 일반화 오버로드 → 최종 경로 Begin(Component,string,bool)로 수렴 —
            // 추적 억제 게이트(PlayerMovement.CameraFollowSuppressed=true) 자동 커버(별도 코드 불요).
            Begin(target, target.GuardName);   // [89차] 일반화 오버로드 위임 — 병사 경로 무변경(fullBody=false)
        }

        /// <summary>
        /// [92차 결함3] 마을 주민(전신 정면 프레이밍) 진입점 — fullBody:true 고정 위임.
        /// InteractionPanelUTK 소스의 "DialogueCameraDirector.Begin(villager, villager.DisplayName)"
        /// 호출 문자열 계약 유지(VillageNpcFKeyTests 어서션) — 오버로드 해상으로 fullBody=true 주입.
        /// </summary>
        public static void Begin(NPCAmbientDialogue target, string displayName)
        {
            if (target == null) return;
            Begin((Component)target, displayName, fullBody: true);   // [92차 결함3] 주민 = 전신 정면
        }

        /// <summary>[89차 → 105차 B] 대화 카메라 시작(일반화) — 구 병사/영주 사선 경로도 fullBody=true로 위임
        /// 전환([105차 IMPL-B] 전 대상 전신 정면 통일). VillageNpcFKeyTests "public static void Begin(Component
        /// target, string displayName)" 문자열 계약 유지.</summary>
        public static void Begin(Component target, string displayName)
        {
            Begin(target, displayName, fullBody: true);   // [105차 B] 병사/영주 경로도 전신 정면 — 구 fullBody:false 폐기
        }

        /// <summary>
        /// [92차 결함3 → 105차 B] 대화 카메라 시작 — 스왑 폐기(기존 Main Camera 직접 제어) + 전신 프레이밍 파라미터.
        /// [105차 B] fullBody 기본값 true — 전 대상 전신 정면(FOV 40/정면/Foot 앵커 center 조준). false는 명시
        /// 옵트인 레거시 사선(2.0m/머리/SightYaw 25°) 전용.
        /// </summary>
        /// <summary>[130차] 상반신 클로즈업 시작(획득 연출 전용) — 대상=플레이어 본인.
        /// fullBody 기계를 그대로 타되 프레이밍만 UpperBody* 상수로 축소(계약 테스트 무변경).</summary>
        public static void BeginUpperBody(Component target, string displayName, bool frontView = false)
        {
            BeginCore(target, displayName, fullBody: true, upperBody: true, frontView: frontView);
        }

        public static void Begin(Component target, string displayName, bool fullBody = true)
        {
            BeginCore(target, displayName, fullBody, upperBody: false);
        }

        private static void BeginCore(Component target, string displayName, bool fullBody, bool upperBody, bool frontView = false)
        {
            if (target == null) return;
            if (_active) End();   // 재진입 — 이전 대화 정리 후 새로 시작(End가 게이트를 false로 되돌린 뒤 아래서 재설정)
            _upperBodyMode = upperBody;   // [130차] End 클리어 이후 설정 — 프레이밍/가드/진단 공용 분기

            // [93차 F1] 대화 중 플레이어 추적 카메라 억제(근본 수리) — PlayerMovement.ApplyFollowCamera의
            // LateUpdate 매 프레임 덮어쓰기를 차단한다(근거 주석은 PlayerMovement.CameraFollowSuppressed).
            // 멱등 — 재진입/중복 Begin에도 동일 값. 병사 경로(GuardPlaceholder 오버로드)와 주민/영주 경로는
            // 모두 이 최종 경로로 수렴하므로 자동 커버(별도 코드 불요 — Begin(GuardPlaceholder) 위임 확인).
            PlayerMovement.CameraFollowSuppressed = true;

            _target = target;
            _targetSavedRot = target.transform.rotation;
            _fullBody = fullBody;

            // [103차 F-2] 플레이어 렌더러 비노출(fullBody=주민 전용) — 카메라(플레이어 측 2.92m)와 NPC 사이
            //   플레이어 가림 원천 제거([100차 F-2] 가림 2/3 히트 실측). 렌더러만 숨기고 콜라이더/Physics 무변경.
            //   스냅샷은 Begin 진입 시 비어 있음 보장 — 재진입 경로의 직전 End()가 복원+클리어(모든 종료 경로
            //   End 수렴) + Hide 내 방어 복원. 순서: 숨김 → 프레이밍/진단 로그(가림 재실측이 숨김 후 상태 기록).
            if (fullBody && !upperBody)   // [130차] 상반신(대상=플레이어)은 숨기지 않는다 — 촬영 대상 본인
                HidePlayerRenderersForDialogue();

            // ① [92차 결함3] 스왑 폐기 — 기존 Camera.main을 직접 제어(카메라 개수 불변).
            //    구 신규 GameObject 카메라 생성+MainCamera 태그 교체+구카메라 enabled=false 스왑은
            //    마을씬 테스트 2.mp4 실측 게임뷰 11.8초 전면 동결(픽셀 diff 0.000)의 트리거 — 전면 제거.
            _savedCamera = MainCameraOverride != null ? MainCameraOverride() : Camera.main;
            _dialogueCam = _savedCamera;
            _savedController = null;
            _hadController = false;
            if (_savedCamera != null)
            {
                _savedPos = _savedCamera.transform.position;
                _savedRot = _savedCamera.transform.rotation;
                _savedFov = _savedCamera.fieldOfView;
                _savedNear = _savedCamera.nearClipPlane;
                _savedOrtho = _savedCamera.orthographic;            // [125차 후속10]
                _savedOrthoSize = _savedCamera.orthographicSize;    // [125차 후속10]

                // 컨트롤러 정지 — GetComponent 탐색만(타입 부재 씬=병사 테스트 등 → 스킵, 예외 금지)
                _savedController = _savedCamera.GetComponent<TopDownCameraController>();
                _hadController = _savedController != null;
                if (_hadController) _savedController.enabled = false;
            }

            // ② 대화 프레이밍 — 같은 카메라 트랜스폰을 대상 기준으로 이동(신규 카메라 없음)
            Transform targetT = target.transform;
            if (fullBody)
            {
                // [92차 후속 F-A] 전신 정면 프레이밍(주민) — bounds 불신 1.8m 고정 계약 + 플레이어→대상 정면.
                //   구 bounds 산식(center/size.y 실측) 폐기 — 비대 bounds가 dist≈22m 프레이밍을 만든
                //   3세션 실측(근거 주석은 FullBodyFixedFrameH 상단). bounds는 진단 로그 boundsY로만 조회.
                float frameH = _upperBodyMode ? UpperBodyFrameH : FullBodyFixedFrameH;   // [130차] 상반신=1.05m / 전신=1.8m 고정 계약
                // [103차 F-1] center = 가시 발(Foot 본) 기준 — ComputeFullBodyCenter 단일소스. 주민 루트가
                //   지하 y≈-1.7로 침강해도 발 본 월드 y(+2.0)를 앵커로 얼굴/전신이 프레임에 들어온다.
                //   실측 실패 시 기존 target.position+0.9up 폴백(기존 계약). 수평은 target.position 유지.
                Vector3 center = ComputeFullBodyCenter(targetT);
                if (_upperBodyMode) center += Vector3.up * UpperBodyCenterLiftM;   // [130차] 발+0.9 → 가슴(≈1.25m)
                float fov = FullBodyFov;   // 40° 고정
                // dist = (frameH×0.5) / tan(FOV/2) × 1.18 — 1.8m 기준 ≈ 2.92m(프레임 상하 여유 18%)
                _fullBodyDist = (frameH * 0.5f) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * FullBodyFrameFactor;

                // [94차 F1] 고정 베어링 1회 확정 — 플레이어→대상 방향(NPC 정면 — NPC 응시 회전과 일치;
                //   플레이어 부재 시 targetT.forward 폴백). 정적 필드에 저장, 세션 내 불변 — ReframeCamera도
                //   이 베어링을 그대로 사용(매 프레임 재투영 폐기 — 측면/등 잔존 원인 제거).
                Vector3 bearing = targetT.forward;
                Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (player != null)
                {
                    // 정면 = 플레이어→대상 방향 그대로 — [92차] SightYaw 25° 사선 폐기(주민 전신 계약)
                    Vector3 toTarget = targetT.position - player.position;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude > 0.0001f)
                        bearing = toTarget.normalized;
                }

                // [142차 I] 대상=플레이어 자신(획득 연출 등) — toTarget 퇴화로 bearing=targetT.forward가
                // 유지되면 카메라 = center − forward×dist = **뒤**(환호 영상 후면 촬영 실측 결함 — 사용자 보고
                // "플레이어의 정면 얼굴이 보여야 하는데 지금은 뒷모습만"). frontView면 bearing 역산 →
                // 카메라 = center + forward×dist = 대상 정면(얼굴 촬영).
                if (frontView)
                    bearing = -targetT.forward;
                _fullBodyBearing = bearing;   // [94차 F1] 세션 내 불변 고정

                // [134차 C2] NPC forward 정합 프로브 — 대상 transform.forward와 플레이어→대상 단위벡터 dot 실측.
                //   신규 FBX forward 축이 구 GLB와 반대일 가능성 검증용(계획 §7 C2) — 판정/자동 수리 금지(프로브만),
                //   다음 Play 로그의 부호로 확정 후 별도 수리. 1=정면/−1=뒷모습 의심.
                // [141차 C1] 판정 규약 갱신 — [136차 확정] dot(플레이어→NPC) −1 = 정면(구 134차 라벨 반전 오판 방지).
                Debug.Log($"[134차 C2] 대화 forward 정합 — {targetT.name} " +
                          $"dot={Vector3.Dot(targetT.forward, bearing):F2} ([136차 확정] −1=정면/1=뒷모습 의심)");

                // [104차 F2 → 105차 B] 원본 yaw 기록 + yaw 제어권 위임 게이트 — _targetSavedRot 캡처 직후 값에서
                //   기록(③ 시선 회전 이전 — 원본 보존 보장). [105차 B] 병사/영주(비주민)도 fullBody로 진입하므로
                //   동일 기록/적용/원복 경로를 탄다. 게이트는 VillagerWanderer의 배회 회전 분기가 존중한다
                //   (주민 경로 보존 게이트 — _paused 정지와 이중 안전, End에서 해제).
                _gazeSavedYaw = _targetSavedRot.eulerAngles.y;
                _gazeYawCaptured = true;
                VillagerWanderer.DialogueYawControlled = true;

                if (_dialogueCam != null)
                {
                    // [125차 후속10] 직교 카메라(테스트씬 탑다운 공통 패턴) 대응 — FOV가 무효라 FOV 기반
                    //   거리 산식(dist=2.92m)은 위치로만 유효하고 프레이밍은 orthographicSize가 결정한다.
                    //   전신 프레이밍 = frameH(1.8m)×0.5×FullBodyFrameFactor(1.18) ≈ 1.06m — 전신+18% 여유.
                    //   원근 경로 로직은 바이트 동일 유지(회귀 가드 — DialogueCamera103Tests).
                    if (_dialogueCam.orthographic)
                    {
                        _dialogueCam.orthographicSize = frameH * 0.5f * FullBodyFrameFactor;
                    }
                    else
                    {
                        _dialogueCam.fieldOfView = fov;
                    }
                    if (_dialogueCam.nearClipPlane > NearPlaneFix)
                        _dialogueCam.nearClipPlane = NearPlaneFix;   // [92차] near 보정(저장/원복 포함)
                    // [94차 F1 → 99차 F-B → 100차 F-2 → 103차 F-3] B-1 반전 — 카메라 = 대상 center − 베어링×2.92m
                    //   (플레이어 측 — NPC 정면 촬영; 구 center + 베어링×2.92m는 NPC 반대편 설치 = 등 촬영 실측 폐기),
                    //   B-2 높이 = center.y+0.6m 단일식([103차 F-3] 플레이어 y 가드 폐기), B-4 측면 오프셋 = 0.0
                    //   ([103차 F-3] 완전 정면). 고정 베어링 계약 유지(합성은 ComputeFullBodyCamPos 단일소스).
                    Vector3 camPos = ComputeFullBodyCamPos(center, bearing, _fullBodyDist);
                    _dialogueCam.transform.position = camPos;
                    _dialogueCam.transform.LookAt(center + Vector3.up * FullBodyLookAtUpM);   // [100차 F-2] 머리 쪽 조준(상향 바이어스 — 양쪽 경로 일관)
                }
            }
            else
            {
                // [105차 B] 명시 옵트인 레거시 사선 경로(구 병사/영주 기본 경로 — 기본 진입은 fullBody로 통일됨)
                //   2.0m 앞 + 약간 위(얼굴/상체 프레이밍 — bounds 실측) + SightYaw 25° 사선
                Vector3 guardPos = targetT.position;
                float headY = MeasureHeadY(targetT);
                Vector3 facePos = guardPos + Vector3.up * headY;
                Vector3 dir = ComputeSightlineDir(targetT, guardPos);
                if (_dialogueCam != null)
                {
                    // [125차 후속10] 직교 카메라는 FOV 무효 — orthographicSize는 원값 유지(광역 프레이밍 폴백).
                    if (!_dialogueCam.orthographic)
                        _dialogueCam.fieldOfView = Mathf.Clamp(_savedFov, 30f, 45f);
                    if (_dialogueCam.nearClipPlane > NearPlaneFix)
                        _dialogueCam.nearClipPlane = NearPlaneFix;   // [92차] near 보정(저장/원복 포함)
                    _dialogueCam.transform.position = facePos + dir * DialogueDistance + Vector3.up * HeightLift;
                    _dialogueCam.transform.LookAt(facePos);
                }
            }

            // ③ 대상 플레이어 쪽 시선 회전(대화 자세) — 플레이어 부재 시 유지
            //    [104차 F2 → 105차 B] fullBody(전 대상 — 주민/병사/영주) = yaw 전용 회전(ApplyDialogueFacingYaw
            //    — 피치/롤 보존, 멱등). 비fullBody(명시 옵트인 레거시) = 기존 LookRotation 그대로(73차 계약).
            Transform playerT = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (playerT != null)
            {
                Vector3 toPlayer = playerT.position - targetT.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    if (fullBody)
                        ApplyDialogueFacingYaw(targetT, playerT.position);   // [104차 F2] yaw 전용(원본 yaw는 위 블록에서 기록됨)
                    else
                        targetT.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                }
            }

            // [103차 F-2] 정면각 판정(Begin 1회 — fullBody 전용) — NPC forward vs (플레이어→NPC) 수평각 ≤5°.
            //   회전 자체는 위 ③ 블록(유지) + [103차 F-4] LateUpdate 틱 재적용이 담당 — 본 로그는 판정 기록.
            if (fullBody && !upperBody)   // [130차] 상반신 세션은 NPC 정면 판정 무의미 — 스킵
                LogFacingAngleCheck(targetT);

            _elapsedSinceBegin = 0f;   // [91차 F5] 유예 카운터 리셋
            _lastEndCause = "미기록";   // [98차 F2] 세션 원인 기록 초기화(1회/세션 가드)
            _endCauseLogged = false;
            _active = true;
            Ensure();   // LateUpdate 틱(타이머/입력/사망 감시) 담당 GO — [94차 F1] Update→LateUpdate 전환

            Debug.Log($"[DialogueCameraDirector] 🔍 대화 카메라 시작 — {displayName} (fullBody={fullBody} — 스왑 폐기, 기존 카메라 직접 제어 / [97차 F2] fullBody=패널 수명 연동, 기존=5s 타이머·0.8s 유예)");
            LogRenderState("Begin");   // [92차 결함3] 렌더 진단 — 동결 재발 시 즉격
            if (_fullBody && !_upperBodyMode)   // [130차] 상반신 세션 — NPC 대상 진단 스킵
            {
                LogFullBodyFrameCheck();   // [99차 F-B → 100차 F-2] 전신 프레임 실측(Begin 1회 — 하단 기준 Foot 본 실측 교체)
                LogOcclusionCheck();       // [100차 F-2] 가림 실측(Begin 1회 — 머리/중심/발 3점 Ray 플레이어 히트 기록)
            }
            // ④ DialogueStarted 발화
            DialogueStarted?.Invoke(target);
        }

        /// <summary>
        /// [92차 결함3] 대화 카메라 종료 — 저장한 트랜스폼/FOV/near 원복 + 컨트롤러 재개(신규 카메라
        /// 파괴/enabled 스왑 없음 — 스왑 폐기 계약) + DialogueEnded 발화(멱등).
        /// </summary>
        public static void End()
        {
            // [93차 F1] 추적 억제 해제 — 멱등(finally 성격: 활성 여부와 무관하게 항상 해제).
            //   Begin과 정확히 쌍을 이루는 계약 — 타이머/입력/사망/수동 종료 전 경로가 이 메서드로 수렴한다.
            //   비활성 상태에서의 중복 End도 안전(게이트는 Begin에서만 true가 되므로 해제가 손실되지 않는다).
            PlayerMovement.CameraFollowSuppressed = false;
            // [104차 F2] yaw 제어권 게이트 해제 — 위 게이트와 동일 멱등 계약(Begin fullBody 세션 대응 —
            //   비세션 End는 무영향). VillagerWanderer 배회 회전 분기가 다시 yaw를 제어한다.
            VillagerWanderer.DialogueYawControlled = false;
            if (!_active) return;
            if (_fullBody) LogEndCause();   // [98차 F2] F2-2 End 진입 원인 진단(1회/세션 — fullBody 한정, 클리어 전 판정)
            _active = false;

            var target = _target;
            _target = null;

            // [92차 결함3] 원복 — Destroy/enable 스왑 없음: 저장값 복원만(카메라 개수 불변).
            // [125차 후속28 F4] 관전 세션 중 카메라 복원 금지 — 기록자 전원 게이트 계약.
            if (_dialogueCam != null && !ArenaSpectatorCameraLock.Active)
            {
                _dialogueCam.transform.position = _savedPos;
                _dialogueCam.transform.rotation = _savedRot;
                // [125차 후속10] 직교 카메라 원복 — orthographicSize 저장/복원(원근 경로 무영향).
                if (_savedOrtho && _dialogueCam.orthographic)
                    _dialogueCam.orthographicSize = _savedOrthoSize;
                _dialogueCam.fieldOfView = _savedFov;
                _dialogueCam.nearClipPlane = _savedNear;
            }

            // [92차 후속] End 진단 로그 캡처 순서 수리 — 구현은 참조 해제 후 호출해 pos=(0,0,0)이 찍혔다
            //   (진단 버그 — 동작 무관). 복원 직후(참조 해제 전) 캡처로 원복 상태가 로그에 반영된다.
            LogRenderState("End");
            _dialogueCam = null;

            if (_hadController && _savedController != null)
                _savedController.enabled = true;   // 컨트롤러 재개
            _savedController = null;
            _hadController = false;
            _fullBody = false;
            _upperBodyMode = false;   // [130차] 상반신 플래그 클리어(멱등)
            _fullBodyDist = 0f;
            _fullBodyBearing = Vector3.zero;   // [94차 F1] 고정 베어링 클리어 — 세션 잔존 방지
            _savedCamera = null;

            // 대화 자세 원복(대상 생존 시)
            if (target != null)
                target.transform.rotation = _targetSavedRot;

            // [104차 F2] 시선 yaw 명시 복원 — Begin에서 기록한 원본 yaw로 되돌린다(yaw 전용 최소 보정 —
            //   위 _targetSavedRot 전체 원복과 동일 결과가 정상 경로. 이중 안전 + 시선 세션 상태의
            //   기록→복원 쌍 판정용 명시 경로). 복원 후 상태 클리어(멱등 — 중복 End 무영향).
            if (_gazeYawCaptured && target != null)
            {
                Transform restoredT = target.transform;
                float yawDelta = Mathf.DeltaAngle(restoredT.eulerAngles.y, _gazeSavedYaw);
                if (Mathf.Abs(yawDelta) > 0.01f)
                    restoredT.rotation = Quaternion.AngleAxis(yawDelta, Vector3.up) * restoredT.rotation;
            }
            _gazeYawCaptured = false;
            _gazeHipsBone = null;        // [118차 후속2] 세션 캐시 해제 — 다음 대화 세션 재해석(멱등)
            _gazeHipsResolved = false;

            // [103차 F-2] 플레이어 렌더러 복원 — 역순 + null 가드 + fail-safe(try 블록), 복원 후 스냅샷 클리어.
            //   스냅샷 비어 있음 = 무동작(멱등 — fullBody 세션이 아니었거나 이미 복원된 경우 로그 없이 통과).
            RestorePlayerRenderersAfterDialogue();

            Debug.Log("[DialogueCameraDirector] 대화 카메라 복원");
            DialogueEnded?.Invoke(target);

            // [98차 F1] F1-4 폴백 재개 — DialogueEnded 구독자(OnDialogueEndedForPose) 부재 시 재개 누락 봉합.
            //   근거: 마을씬 테스트 9 — End가 DialogueEnded를 발화해도 구독이 해제된 상태면 위임 경로가 죽고
            //   VillagerWanderer._paused 잔존(영구 정지 + 안전망 부재). 구독 유무 무관 — _paused 잔존만 검사해
            //   직접 재개(ResumeAfterDialogue 멱등 — 구독자가 이미 재개했다면 무동작).
            if (target is NPCAmbientDialogue endedVillager)
            {
                var endedWanderer = endedVillager.GetComponent<VillagerWanderer>();
                if (endedWanderer != null && endedWanderer.IsDialoguePaused)
                {
                    endedWanderer.ResumeAfterDialogue();
                    Debug.Log($"[98차 F1] End 폴백 재개 — {endedVillager.DisplayName} (DialogueEnded 구독자 미재개 — ResumeAfterDialogue 직접 호출)");
                }
            }
        }

        /// <summary>
        /// [103차 F-2] 플레이어 렌더러 비노출(fullBody=주민 세션 전용) — 카메라(플레이어 측 2.92m)와 NPC 사이에
        /// 플레이어가 서 [100차 F-2] 가림 2/3 히트 실측 → 렌더러 전수 숨김으로 가림 원천 제거([103차 F-3] 완전
        /// 정면 계약과 짝). GetComponentsInChildren<Renderer>(true) 전수(비활성 GO 포함) 스냅샷(enabled,
        /// shadowCastingMode) 후 enabled=false. 콜라이더/Physics 무변경(가림 Ray 진단 계속 유효 — 히트 기대 아님).
        /// 재진입 안전: Begin 내 직전 End()가 복원+클리어 + 본 메서드 방어 복원(잔존 스냅샷 시) — 스냅샷 비어 있는
        /// 상태에서만 캡처. 복원은 RestorePlayerRenderersAfterDialogue(End에서 호출).
        /// </summary>
        private static void HidePlayerRenderersForDialogue()
        {
            // 방어 — 정상 경로에서는 직전 End()가 스냅샷을 비운다. 잔존 시(비정상 — 예: 외부 코드가 End 우회)
            //   먼저 복원해 원래 상태를 잃지 않게 한 뒤 새로 캡처한다(fail-safe).
            if (_playerRendererSnapshot.Count > 0)
                RestorePlayerRenderersAfterDialogue();

            _playerRendererSnapshot.Clear();
            _playerRendererSnapshotEnabled.Clear();
            _playerRendererSnapshotShadow.Clear();

            Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.Log("[103차 F-2] 플레이어 렌더러 숨김 — 0건 (플레이어 부재)");
                return;
            }
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                _playerRendererSnapshot.Add(r);
                _playerRendererSnapshotEnabled.Add(r.enabled);
                _playerRendererSnapshotShadow.Add(r.shadowCastingMode);   // 스냅샷 전용 — 본 수리는 enabled만 변경
                r.enabled = false;
            }
            Debug.Log($"[103차 F-2] 플레이어 렌더러 숨김 — {_playerRendererSnapshot.Count}건 (콜라이더/Physics 무변경)");
        }

        /// <summary>
        /// [103차 F-2] 플레이어 렌더러 복원 — 스냅샷 역순 복원(파괴된 렌더러 null 가드 — 생존분만 복원) + 복원 후
        /// 스냅샷 클리어(멱등 — End 중복 호출 무영향). restore 중 예외가 종료 흐름을 죽이지 않게 try 블록
        /// fail-safe(부분 복원 잔존 시 다음 Begin의 방어 복원 경로가 재시도). 스냅샷 비어 있음 = 무동작/무로그.
        /// </summary>
        private static void RestorePlayerRenderersAfterDialogue()
        {
            int count = _playerRendererSnapshot.Count;
            if (count == 0) return;
            int restored = 0;
            try
            {
                for (int i = count - 1; i >= 0; i--)   // 역순 복원
                {
                    var r = _playerRendererSnapshot[i];
                    if (r == null) continue;   // 파괴된 렌더러 — null 가드(복원 대상 소실은 정상 종료로 간주)
                    r.enabled = _playerRendererSnapshotEnabled[i];
                    r.shadowCastingMode = _playerRendererSnapshotShadow[i];   // 스냅샷값 원복(본 수리 미변경 — 멱등)
                    restored++;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[103차 F-2] 플레이어 렌더러 복원 중 예외 — fail-safe 계속 ({ex.GetType().Name}: {ex.Message})");
            }
            _playerRendererSnapshot.Clear();
            _playerRendererSnapshotEnabled.Clear();
            _playerRendererSnapshotShadow.Clear();
            Debug.Log($"[103차 F-2] 플레이어 렌더러 복원 — {restored}건");
        }

        /// <summary>LateUpdate 틱 담당 GO — 정적 Begin/End와 분리(테스트 친화: 상태 전부 정적).
        /// [94차 F1] Update→LateUpdate 전환(최종 적용권 — PlayerMovement 게이트보다 나중 실행).</summary>
        public static void Ensure()
        {
            if (_updater != null) return;
            _updater = new GameObject("DialogueCameraDirector").AddComponent<DialogueCameraDirector>();
        }

        /// <summary>
        /// [97차 F2] NPCDialoguePanelUTK 패널 개방 실측 — fullBody(주민) 세션 수명 판정 단일소스.
        /// 프로덕션 공급처 = NPCDialoguePanelUTK.EnsureAttached가 등록하는 위임(패널 측 IsOpen 폴링 —
        /// Systems asmdef는 UI 역참조 금지(UI→Systems 단방향 계약)이므로 UI 측 등록 방식).
        /// 주입 부재 = 닫힘 취급 — 패널이 대화 세션의 수명 소스(미등록 씬 fullBody 세션은 즉시 종료).
        /// EditMode 테스트는 DialoguePanelOpenOverride로 패널 수명 시뮬레이션(MainCameraOverride 선례).
        /// </summary>
        private static bool IsDialoguePanelOpen()
        {
            if (DialoguePanelOpenOverride != null) return DialoguePanelOpenOverride();
            return false;   // [97차 F2] 주입 부재 = 닫힘 취급(패널 수명 소스 계약)
        }

        /// <summary>
        /// [98차 F2] F2-2 End 분기 진단 로그(1회/세션 — fullBody 한정). 근거: 마을씬 테스트 9 — 대화창이
        /// 화면에 남아 있는 상태로 카메라만 End(= !IsDialoguePanelOpen() 분기) → 복원(36s 실측). IsOpen
        /// 폴링 결과와 실제 표시 상태 불일치의 원인 확정용 데이터: End 경로 / override 등록 여부 / 폴링
        /// 재실측 / UI 실측(_instance 존재·IsOpen·resolvedStyle.display·parent 부첞性 — UI 공급 위임) / Time.time.
        /// 다음 Play에서 [98차 F2] 카메라 End 원인 로그로 원인 확정(진단 전용 — 동작 무영향).
        /// </summary>
        private static void LogEndCause()
        {
            if (_endCauseLogged) return;
            _endCauseLogged = true;
            bool hasOverride = DialoguePanelOpenOverride != null;
            bool pollOpen = IsDialoguePanelOpen();
            string uiDiag = DialoguePanelOpenDiagnostics != null ? DialoguePanelOpenDiagnostics() : "진단미등록";
            Debug.Log($"[98차 F2] 카메라 End 원인 — 경로={_lastEndCause} override={(hasOverride ? "등록" : "null")} " +
                      $"pollOpen={pollOpen} {uiDiag} t={Time.time:F2}s");
        }

        // ── 자동 종료 감시 ──

        // [94차 F1] 최종 적용권 이전 — Update → LateUpdate. PlayerMovement(93차 CameraFollowSuppressed
        // 게이트 유지 — 아래 가드 코드 그대로, 이중 안전)보다 나중에 실행되어 어떤 추격 스크립트도
        // 대화 프레이밍을 덮어쓸 수 없다. Ensure()가 만드는 업데이터(Updater GO의 DialogueCameraDirector
        // 컴포넌트)가 이 틱을 담당한다.
        private void LateUpdate()
        {
            if (!_active) return;

            // 대상 사망/파괴 시 즉시 End — 유예와 무관(End 유지 조건 계약). [89차] IsDead 감시는 병사(GuardPlaceholder)일 때만,
            // 그 외(Component 일반)는 파괴(null) 감시만.
            if (_target == null)
            {
                _lastEndCause = "대상소실";   // [98차 F2] End 진입 원인 기록
                End();
                return;
            }
            var watchGuard = _target as GuardPlaceholder;   // [89차] 병사만 IsDead 감시 — 일반 Component는 파괴 감시만
            if (watchGuard != null && watchGuard.IsDead)
            {
                _lastEndCause = "대상사망";   // [98차 F2] End 진입 원인 기록
                End();
                return;
            }

            // [97차 F2] 종료 조건 — [105차 B] 단일화: 패널 닫힘 폴링(전 대상 — 병사/영주도 fullBody 통일).
            // 구 5초 타이머 경로는 fullBody 통일로 폐기(_remaining 미선언 CS0103 — 폐기 블록 제거가 수리).
            {
                // [97차 F2] ① NPCDialoguePanelUTK 패널 닫힘 폴링 — 틱에서 패널 IsOpen 실측, 열려 있지 않으면 End.
                //   5s 타이머 폐기(세션 수명 = 대화창 수명 — "대화가 끝날 때까지 NPC를 비춰라"),
                //   이동키 종료 폐기(대화창이 입력 게이트 — 이동 무시). ESC만 수동 종료 유지.
                // [98차 F2] F2-2 End 진입 원인 기록 — 폴링 실패 경로(진단 로그는 End 진입 1회 발화).
                _lastEndCause = "패널닫힘폴링";
                if (!IsDialoguePanelOpen())
                {
                    End();
                    return;
                }
            }

            // [91차 F5] 매 프레임 재조준 — [94차 F1] fullBody= 고정 베어링 프레이밍 + center 재조준 / 기존= 머리 앵커 LookAt.
            ReframeCamera();

            // [91차 F5] 입력 유예 — Begin 직후 0.8s는 입력 종료 무시(이동 중/직후 대화 시작 →
            //   즉시 복원되어 확대가 "깜빡하고 사라짐" 실측 수리).
            _elapsedSinceBegin += Time.deltaTime;
            if (_elapsedSinceBegin < InputGraceSeconds) return;

            // [91차 F5] 선별 입력 종료 — ESC 또는 WASD/화살표 이동키 wasPressedThisFrame만 End
            //   (구 전수 키 스캔 폐기 — 타이핑/기타 키 무시, 양 이동 체계 커버 유지).
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { _lastEndCause = "ESC수동"; End(); return; }   // [98차 F2] 원인 기록
            // [97차 F2] ③ 이동키 종료 폐기(fullBody 한정) — 대화창이 입력 게이트(이동 무시), ESC만 유지.
            //   병사/영주(fullBody=false)는 기존 이동키 종료 유지(73차 계약 무변경).
            if (_fullBody) return;
            if (kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
                kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame ||
                kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
            {
                End();
                return;
            }
        }

        /// <summary>
        /// [91차 F5 → 92차 결함3 → 94차 F1 → 96차 F1 → 99차 F-B → 100차 F-2 → 103차 F-1/F-3/F-4] 매 프레임
        /// 재조준. fullBody(주민): 고정 베어링 반전 배치(center − bearing×dist — 플레이어 측) + 측면 오프셋
        /// (perpendicular)×0.0m([103차 F-3] 완전 정면) + 높이 = center.y+0.6m 단일식([103차 F-3] 가드 폐기),
        /// center = [103차 F-1] 가시 발(Foot 본) 앵커, LookAt = center + up×0.15m 재조준(머리 쪽 조준 — 대상
        /// 이동 추적 — 구 dir 재투영/90° 클립 회전 폐기). 말단 [103차 F-4] NPC 정면 재적용(매 틱 — 시선고정).
        /// 기존(병사/영주): 위치 = facePos + dir×DialogueDistance 후 머리
        /// (bounds max.y−12%) LookAt. 플레이어까지 거리 &lt; 0.9m로 좁혀지면 dir를 90° 회전(측면 사선)
        /// — 클립 방지([91차 F5] 계약 유지 — 병사/영주 경로 전용).
        /// </summary>
        private static void ReframeCamera()
        {
            // [125차 후속28 F4] 관전 세션 중 대화 카메라 기록 금지 — 카메라 기록자 전원 게이트 계약.
            //   근거: 락 위반 로그 지속(5회×N부착) — ApplyFollowCamera 게이트 외의 기록자 존재 확정.
            if (ArenaSpectatorCameraLock.Active) return;
            if (_dialogueCam == null || _target == null) return;
            Transform targetT = _target.transform;

            if (_fullBody)
            {
                // [94차 F1] 고정 베어링 프레이밍 — 구 dir 재투영 폐기(현재 위치 기준 재투영이 측면/등
                //   잔존의 원인) → Begin에서 확정한 고정 베어링×2.92m를 유지하고 LookAt만 매 프레임 갱신
                //   (대상 center 재조준 — 대상 이동 추적). 구 90° 클립 회전 폐기(fullBody 한정 — 발생
                //   근거 없는 방향 이상 요인; 병사/영주 경로는 기존 무변경).
                // [96차 F1 → 99차 F-B → 100차 F-2 → 103차 F-1/F-3] Begin과 동일 합성 오프셋(측면×0.0m + B-1
                //   반전/B-2 높이 center.y+0.6m 단일식) 유지 — 단일소스 헬퍼(ComputeFullBodyCamPos)로 기하
                //   이중화 폐기. 고정 베어링 계약 무변경. center = [103차 F-1] 가시 발(Foot 본) 앵커(Begin과
                //   동일 헬퍼 — 일원화).
                Vector3 center = ComputeFullBodyCenter(targetT);
                if (_upperBodyMode) center += Vector3.up * UpperBodyCenterLiftM;   // [130차] 발+0.9 → 가슴(≈1.25m)
                Vector3 bearing = _fullBodyBearing;
                if (bearing.sqrMagnitude < 0.0001f)
                    bearing = targetT.forward;   // 방어 폴백 — 베어링 미확정 상태 진행 방지(정상 경로에서는 Begin 확정값)
                // [101차 F-2] Distance Guard — 매 틱 재계산: max(2.92m 기본, NPC bounds 반경 × 2 + 0.5m).
                //   모델 침투 방어(모자/상체에 파고들어 폴리곤 깨짐 방지) — Begin 확정 dist보다 가드가 크면 가드 우선.
                float guardDist = 2.92f;
                var rend = targetT.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    float radius = rend.bounds.extents.magnitude;
                    guardDist = Mathf.Max(guardDist, radius * 2f + 0.5f);
                }
                // [130차] 상반신 세션 — 가드 스킵(근접 거리 유지: 촬영 대상=플레이어 본인, 1.7m에서 침투 여유 충분).
                // [135차 P3-B] 상반신 세션 가드 스킵 철회 — 근평면 침입 실측(구르기 3 영상 파손+프로브 0.6/1.4m 클리핑
                //   재현)으로 가드 필요 확정. 상반신 프레이밍 유지를 위해 기본 2.92m는 미적용하고 메시 반경 가드만 적용:
                //   radius×2+0.5 (girl1 r≈0.66 → 1.82m — [130차] 1.7m 기본과 동일 스케일, 침투 방지).
                if (_upperBodyMode)
                {
                    var upperRend = targetT.GetComponentInChildren<Renderer>();
                    guardDist = upperRend != null ? upperRend.bounds.extents.magnitude * 2f + 0.5f : 1.2f;
                }
                float effectiveDist = Mathf.Max(_fullBodyDist, guardDist);
                Vector3 camPos = ComputeFullBodyCamPos(center, bearing, effectiveDist);
                _dialogueCam.transform.position = camPos;
                _dialogueCam.transform.LookAt(center + Vector3.up * FullBodyLookAtUpM);   // [100차 F-2] 머리 쪽 조준(상향 바이어스 — 양쪽 경로 일관)

                // [103차 F-4 → 104차 F2] NPC 정면 보장 — 매 틱 재적용(Begin 룩앳 1회만으로는 배회/외부 회전에
                //   흐틀어질 수 있음). [104차 F2] yaw 전용 회전(ApplyDialogueFacingYaw — 피치/롤 보존, 멱등 —
                //   유효 회전 없으면 무기입). 구 LookRotation 재적용은 fullBody 블록 내 교체(병사/영주 무영향).
                //   대화 중 주민은 정지 상태(_paused + [104차 F2] DialogueYawControlled 게이트)라 경합 없음 —
                //   매 틱 회전 = 시선고정 계약.
                Transform playerTick = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (playerTick != null)
                    ApplyDialogueFacingYaw(targetT, playerTick.position);
                return;
            }

            Vector3 facePos = targetT.position + Vector3.up * MeasureHeadY(targetT);
            Vector3 gdir = _dialogueCam.transform.position - facePos;
            float glen = gdir.magnitude;
            if (glen < 0.0001f) return;
            gdir /= glen;

            Transform p = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (p != null
                && Vector3.Distance(facePos + gdir * DialogueDistance, p.position) < ClipAvoidMinDistance)
            {
                gdir = Quaternion.Euler(0f, 90f, 0f) * gdir;   // [91차 F5] 90° 측면 사선 회전
            }

            _dialogueCam.transform.position = facePos + gdir * DialogueDistance;
            _dialogueCam.transform.LookAt(facePos);
        }

        /// <summary>
        /// [96차 F1 → 99차 F-B → 100차 F-2 → 103차 F-3] fullBody 카메라 위치 합성 단일소스 — center − 베어링×dist
        /// (B-1 반전 — 플레이어 측 설치, NPC 정면 촬영) + 측면 오프셋×FullBodySideOffsetM(0.0 — [103차 F-3] 완전
        /// 정면, 곱 구조 유지), 높이 = center.y + FullBodyCamHeightUpM(0.6m) 단일식 — [103차 F-3] 플레이어 y 가드
        /// 폐기(가림 원천은 [103차 F-2] 플레이어 렌더러 숨김으로 제거 — 가드 불요 + 정면을 해치는 하이 앵글 요인).
        /// Begin과 ReframeCamera 공용(기하 이중화 폐기 — 두 경로가 어긋나면 프레임이 튄다). perpendicular =
        /// bearing의 Y축 90° 회전 — 외적 Cross(up, bearing)로 산출
        /// (수평 단위벡터 한정 Quaternion.Euler(0,90,0) yaw와 동일 기하). bearing 정의(플레이어→NPC)와
        /// 세션 내 불변 계약([94차 F1])은 유지 — 배치 축만 반전. [96차 F1] 근거(영상6 실측): 정중앙 배치면
        /// 시선 y@2.0m≈3.8 &lt; 플레이어 머리 4.3 관통 → 플레이어 등이 프레임을 채움(측면 오프셋 근거 — 유지).
        /// </summary>
        private static Vector3 ComputeFullBodyCamPos(Vector3 center, Vector3 bearing, float dist)
        {
            Vector3 sideDir = Vector3.Cross(Vector3.up, bearing).normalized;   // [96차 F1] perpendicular(bearing Y축 90° 회전)
            // [99차 F-B] B-1 베어링 반전 — camPos = center − bearing×dist(플레이어 측 — NPC 정면 촬영).
            //   구 center + bearing×dist는 카메라를 NPC 반대편(뒤)에 설치 = NPC 등 촬영(마을씬 테스트 10
            //   영상 01:08~01:15/01:22~01:25 클로즈업 등/측후면 실측) — 폐기.
            Vector3 camPos = center - bearing * dist + sideDir * FullBodySideOffsetM;
            // [99차 F-B → 100차 F-2 → 103차 F-3 폐기] B-2 비가림 상향 가드(max(..., 플레이어 y+1.75m)) — 폐기
            //   사유: [103차 F-2] 플레이어 렌더러 숨김으로 가림 원천 제거 → 가드 불요 + 정면 계약을 해치는 하이
            //   앵글 요인(구 근거 주석은 상수 폐기 블록 — FullBodyPlayerHeadClearM 주석 — 히스토리 보존).
            // [103차 F-3] 높이 단일식 — camPos.y = center.y + FullBodyCamHeightUpM(0.6m). playerY 조회
            //   (FindGameObjectWithTag)도 함께 제거 — 시그니처(center, bearing, dist)·호출부 2곳 불변.
            camPos.y = center.y + (_upperBodyMode ? UpperBodyCamHeightUpM : FullBodyCamHeightUpM);   // [130차] 상반신=0.35m(거의 수평) / 전신=0.6m
            return camPos;
        }

        /// <summary>
        /// [103차 F-1] fullBody center 산출 단일소스 — 가시 발(Foot 본) 기준 center.
        /// TryMeasureFootBottomY 성공 → footY + FullBodyCenterUpM(0.9m), 실패 → 기존 target.position + up×0.9
        /// 폴백(기존 계약 유지). 수평 성분은 target.position 그대로(베어링/LookAt 수평 기하 불변).
        /// 적용 단일소스: Begin 프레이밍 / ReframeCamera / LogFullBodyFrameCheck(b.center 구성) / 분기 주석.
        /// 근거(실측): 주민 루트 지하 침강(y≈-1.7 — [103차 P1])에서도 발 본 월드 y(+2.0)는 유지 → root.y+0.9
        /// 조준은 지하(얼굴 화면 밖) — Foot 앵커로 얼굴/전신 프레임 정상화. internal — EditMode 테스트 직접판정용
        /// (InternalsVisibleTo(ProjectName.Tests.EditMode) — NPCDialogueAdapter 어셈블리 선언 선례).
        /// </summary>
        internal static Vector3 ComputeFullBodyCenter(Transform target)
        {
            if (target == null) return Vector3.zero;
            if (TryMeasureFootBottomY(target, out float footY))
                return new Vector3(target.position.x, footY + FullBodyCenterUpM, target.position.z);   // [103차 F-1] Foot 앵커
            return target.position + Vector3.up * FullBodyCenterUpM;   // 폴백 — 기존 계약(root+0.9up)
        }

        /// <summary>
        /// [104차 F2] 대화 시선 yaw 산출 순수 헬퍼 — fromPos에서 toPos를 바라보는 월드 yaw(도, 수평 투영).
        /// Atan2 기반 단일산식(Quaternion.LookRotation 폐기 — yaw만 필요, 피치/롤 비개입). 퇴화 입력(동일
        /// 위치/수평 영벡터)은 float.NaN 반환 — 호출처 무기입 폴백. internal — EditMode 직접판정용
        /// (InternalsVisibleTo(ProjectName.Tests.EditMode) — [103차 F-1] ComputeFullBodyCenter 선례).
        /// [103차 F-2] 정면각 로그(≤5° 판정)와 동일 기하 — 실측 로그 연동 판정 기준.
        /// </summary>
        internal static float ComputeFacingYaw(Vector3 fromPos, Vector3 toPos)
        {
            Vector3 dir = toPos - fromPos;
            dir.y = 0f;   // 수평 투영 — yaw만 산출(피치/롤 비개입)
            if (dir.sqrMagnitude < 0.0001f) return float.NaN;   // 퇴화 — 동일 위치/영벡터
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// [104차 F2] 시선 yaw 적용(fullBody 세션 전용) — 현재 회전에서 목표 yaw까지의 최소 각도만
        /// AngleAxis(world up)로 회전(yaw 전용 — 기존 피치/롤 보존, 새 기울임 도입 없음). 멱등 — 이미
        /// 목표 yaw면 무기입. NaN(퇴화)·유효 회전 없음(≥0.01°)은 무시. 적용처: Begin ③ 블록(fullBody 분기) +
        /// [103차 F-4] LateUpdate 매 틱 재적용 — 대화 중 시선고정 단일소스.
        /// </summary>
        internal static void ApplyDialogueFacingYaw(Transform targetT, Vector3 focusPos)
        {
            if (targetT == null) return;
            float targetYaw = ComputeFacingYaw(targetT.position, focusPos);
            if (float.IsNaN(targetYaw)) return;   // 퇴화 — 동일 위치 등(무기입)
            // [118차 후속2] 가시 신체 서보 — 판정 기준을 루트 yaw에서 Hips 본 월드 yaw로 교체.
            //   루트가 정면이어도 골격 내부 오프셋(클립 베이크 요각/리그 축)으로 몸이 등을 보이는 실측
            //   (104차 '골격 체인 내부 회전 오프셋')을 매 틱 자기수정으로 봉합. Hips 판정 불가 시
            //   기존 루트 yaw 기준으로 폴백(휴머노이드 부재 씬 = 기존 계약 동일).
            float currentYaw = ComputeVisibleBodyYaw(targetT);
            if (float.IsNaN(currentYaw))
            {
                currentYaw = targetT.eulerAngles.y;   // 루트 폴백 — 139차 규약(얼굴≈루트 forward) 그대로
            }
            else
            {
                // [141차 C1] 서보 표적 정정 — 시각 얼굴 = Hips + 180°([139차 확정: face=hips+180, 전상태 hips−root≈±180]).
                // 구 표적(Hips→플레이어)은 얼굴을 플레이어 반대편으로 수렴 = 등 노출(141차 사용자 재Play 실측).
                // 얼굴→플레이어 ⇔ Hips→플레이어+180 — 수렴 시 root≈플레이어 방향, 플레이어 측 고정 카메라가 정면 촬영.
                targetYaw += 180f;
            }
            float delta = Mathf.DeltaAngle(currentYaw, targetYaw);
            if (Mathf.Abs(delta) < 0.01f) return;   // 멱등 — 유효 회전 없음
            targetT.rotation = Quaternion.AngleAxis(delta, Vector3.up) * targetT.rotation;
        }

        /// <summary>
        /// [103차 F-4] 정면각 판정(Begin 1회 — fullBody 전용) — NPC forward vs (플레이어→NPC) 수평각 ≤5°
        /// 판정 로그. 플레이어 부재 시 "플레이어부재" 기록. 진단 전용(회전은 Begin ③ 블록 + [103차 F-4] 틱
        /// 재적용 담당 — 본 메서드는 상태 변경 없음).
        /// </summary>
        private static void LogFacingAngleCheck(Transform targetT)
        {
            Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.Log("[103차 F-2] 정면각 — 플레이어부재");
                return;
            }
            Vector3 toPlayer = player.position - targetT.position;
            toPlayer.y = 0f;
            Vector3 fwd = targetT.forward;
            fwd.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f || fwd.sqrMagnitude < 0.0001f)
            {
                Debug.Log("[103차 F-2] 정면각 — 판정불가(동일 위치/영벡터)");
                return;
            }
            float angle = Vector3.Angle(fwd.normalized, toPlayer.normalized);
            // [118차 후속2] 가시 신체 각 병기 — 루트 각이 0°여도 Hips 본 월드 요가 판정의 진실(사용자 체감).
            float bodyAngle = -1f;
            float bodyYaw = ComputeVisibleBodyYaw(targetT);
            if (!float.IsNaN(bodyYaw))
            {
                // [141차 C1] 판정 기준 = 얼굴(face = hips+180 [139차 확정]) — 구식 Hips-정면 가정 폐기.
                float faceYaw = bodyYaw + 180f;
                Vector3 bodyFwd = new Vector3(Mathf.Sin(faceYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(faceYaw * Mathf.Deg2Rad));
                bodyAngle = Vector3.Angle(bodyFwd, toPlayer.normalized);
            }
            float verdictAngle = bodyAngle >= 0f ? bodyAngle : angle;   // 얼굴 각 우선 — 판정 불가 시 루트 폴백
            string verdict = verdictAngle <= 5f ? "OK" : "탈락";
            Debug.Log($"[141차 C1] 정면각 — 루트={angle:F1}° 얼굴={(bodyAngle >= 0f ? bodyAngle.ToString("F1") : "측정불가")}° (≤5° {verdict})");
        }

        /// <summary>
        /// [99차 F-B → 100차 F-2 → 100차 회귀 수리] 전신 프레이밍 실측(Begin 1회) — 고정 프레임 계약 기준
        /// 즉격 진단. [100차 회귀 수리] MeasureBounds 진단 호출 폐기(92차 후속 F-A 계약 — MeasureBounds에
        /// targetT 인자 직접 전달 소스 부재. 구 bounds center/min/max 기준은 스킨바운드 부양으로 신뢰 불가) →
        /// 전 기준 Foot 본/고정 상수 기반: 하단=Foot 본 최하단 y(부재 시 target.position.y 폴백 — 가림 프로브
        /// 지면 근방 폴백과 동일), 상단=하단+1.8m(FullBodyFixedFrameH 고정 프레임 계약), frameH=1.8m 고정.
        /// bounds 비대 감시(boundsY)는 LogRenderState 진단 전용 잔존(MeasureBounds 유일 호출처).
        /// </summary>
        private static void LogFullBodyFrameCheck()
        {
            if (_target == null || _dialogueCam == null) return;
            Transform targetT = _target.transform;
            // [100차 회귀 수리 → 103차 F-1] b = 프러스텀 기준 밴드 — center = ComputeFullBodyCenter
            //   (가시 발(Foot 본) 기준 — 루트 지하 침강 방어; 실측 실패 시 target.position+0.9up 폴백).
            //   bounds 조회 아님(MeasureBounds 진단 경로 폐기). b.center.y ± halfH = 프러스텀 상하단.
            Bounds b = new Bounds(ComputeFullBodyCenter(targetT), Vector3.zero);
            float halfH = _fullBodyDist * Mathf.Tan(FullBodyFov * 0.5f * Mathf.Deg2Rad);   // 프레임 반높이(NPC 평면)
            // [100차 F-2] 하단 기준 — Foot 본 최하단 y(스킨바운드 부양 방어). 발 본 부재 시
            //   target.position.y 폴백(지면 근방). 하단여유 = 발 기준 − 프러스텀 하단(양수 = 발이 프레임 내).
            float bottomRefY = targetT.position.y;
            string bottomRef = "target폴백";
            if (TryMeasureFootBottomY(targetT, out float footY))
            {
                bottomRefY = footY;
                bottomRef = "Foot본";
            }
            // [100차 회귀 수리] 상단 기준 = 하단 + 1.8m(고정 프레임 계약 — 구 bounds max.y 기준 폐기).
            float topRefY = bottomRefY + FullBodyFixedFrameH;
            float topMargin = (b.center.y + halfH) - topRefY;      // 화면상단여유 추정(프러스텀 상단 − 상단기준)
            float bottomMargin = bottomRefY - (b.center.y - halfH);
            Debug.Log($"[99차 F-B] 전신프레임 — 하단기준={bottomRef}({bottomRefY:F2}) 상단=하단+1.8m({topRefY:F2}) " +
                      $"dist={_fullBodyDist:F2}m frameH={FullBodyFixedFrameH:F2}m(고정계약) 화면상단여유≈{topMargin:F2}m 하단여유≈{bottomMargin:F2}m (FOV {FullBodyFov:F0}°) [100차 F-2]");
        }

        /// <summary>
        /// [100차 F-2] 발 본 최하단 y 실측 — 이름에 foot/toe 포함 본(좌우 Foot/Toe — 대소문자 무시) 중
        /// 최저 y를 반환. 스킨바운드 부양(bounds minY가 실제 발보다 높게 실측 — 마을씬 테스트 11 minY=1.85)
        /// 방어용 실측. 발 본 부재 시 false — 호출처 폴백(target.position.y 지면 근방 — 전신프레임/가림 공통).
        /// 진단/가림 프로브 전용(프레이밍 산식 불변).
        /// </summary>
        private static bool TryMeasureFootBottomY(Transform target, out float footY)
        {
            footY = float.MaxValue;
            bool found = false;
            foreach (var t in target.GetComponentsInChildren<Transform>())
            {
                if (t == null) continue;
                string lower = t.name.ToLowerInvariant();
                if (string.IsNullOrEmpty(lower)) continue;
                if (!lower.Contains("foot") && !lower.Contains("toe")) continue;
                if (t.position.y < footY) { footY = t.position.y; found = true; }
            }
            return found;
        }

        /// <summary>
        /// [100차 F-2] 가림 실측(Begin 1회) — 카메라→NPC 머리/중심/발 3점 Ray에 플레이어 태그 히트 시 기록
        /// (다음 판정 데이터 — 진단 전용, 프레이밍 무영향). 머리=target.position+1.7m(캐릭터 키 계약 머리 근방),
        /// 중심=FullBodyCenterUpM(0.9m), 발=Foot 본 최하단(부재 시 target.position — 지면 근방). Ray는 도달점
        /// 직전까지 발사(0.05m 여유 — NPC 자기 콜라이더 종단 히트 배제), 히트 콜라이더가 Player 태그(본체/루트)
        /// 일 때만 기록 — 히트 없음=무가림(무로그).
        /// [103차 F-5] 플레이어숨김=ON([103차 F-2] 렌더러 비노출 — 콜라이더/Physics 유지라 Ray 히트는 여전히
        /// 가능하나 히트 기대 아님 — 히트 기록은 렌더 가림이 아닌 콜라이더 교차 진단 데이터).
        /// </summary>
        private static void LogOcclusionCheck()
        {
            if (_target == null || _dialogueCam == null) return;
            Transform targetT = _target.transform;
            Vector3 camPos = _dialogueCam.transform.position;
            float footY = targetT.position.y;
            if (TryMeasureFootBottomY(targetT, out float measuredFootY)) footY = measuredFootY;
            Vector3[] probes =
            {
                targetT.position + Vector3.up * FullBodyHeadProbeM,             // 머리
                targetT.position + Vector3.up * FullBodyCenterUpM,              // 중심
                new Vector3(targetT.position.x, footY, targetT.position.z),     // 발
            };
            string[] labels = { "머리", "중심", "발" };
            var hits = new System.Collections.Generic.List<string>(3);
            for (int i = 0; i < probes.Length; i++)
            {
                Vector3 dir = probes[i] - camPos;
                float dist = dir.magnitude;
                if (dist < 0.05f) continue;
                dir /= dist;
                if (!Physics.Raycast(camPos, dir, out RaycastHit hit, dist - 0.05f)) continue;
                Transform hitT = hit.collider != null ? hit.collider.transform : null;
                if (hitT == null) continue;
                if (!hitT.CompareTag("Player") && !hitT.root.CompareTag("Player")) continue;   // 플레이어 태그 히트만 기록
                hits.Add($"{labels[i]}=히트 히트점=({hit.point.x:F2},{hit.point.y:F2},{hit.point.z:F2}) 높이={hit.point.y:F2}m 거리={hit.distance:F2}m ({hitT.name})");
            }
            if (hits.Count > 0)
                Debug.Log($"[100차 F-2] 가림 — 카메라→NPC 3점 Ray 플레이어 태그 히트 {hits.Count}/3 — {string.Join(" / ", hits)} (플레이어숨김=ON — [103차 F-2] 렌더러 비노출, 콜라이더 유지라 히트 기대 아님)");
        }

        /// <summary>[92차 결함3] 렌더 진단 로그 — Camera.allCameras 요약(Begin/End 1회 — 동결 재발 시 즉격).
        /// [92차 후속] 대상 bounds size.y 진단(boundsY) 추가 — 비대 bounds 근본 확정용(Begin 3세션 실측
        /// pos y=6.3~6.8). MeasureBounds는 이 진단 경로에서만 조회(프레이밍 경로는 [92차 후속 F-A] 고정 계약).
        /// End는 _target 해제 후 호출 순서라 boundsY=-1(pos 캡처는 복원 직후 수리 — 진단 대상 아님).</summary>
        private static void LogRenderState(string phase)
        {
            var all = Camera.allCameras;
            var enabledList = new System.Collections.Generic.List<string>(all.Length);
            var depthList = new System.Collections.Generic.List<string>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null) continue;
                enabledList.Add($"{c.name}:{(c.enabled ? "on" : "off")}");
                depthList.Add($"{c.name}:{c.depth:F1}");
            }
            Vector3 pos = _dialogueCam != null ? _dialogueCam.transform.position : Vector3.zero;
            // [92차 후속] boundsY — 대상 렌더러 bounds size.y(Begin 시점 _target 유효). 비대 확정용.
            float boundsY = -1f;
            if (_target != null)
            {
                Bounds b = MeasureBounds(_target.transform, out bool hasB);
                if (hasB) boundsY = b.size.y;
            }
            Debug.Log($"[DialogueCameraDirector] 렌더 상태 — camCount={all.Length} enabled=[{string.Join(",", enabledList)}] depth=[{string.Join(",", depthList)}] pos=({pos.x:F1},{pos.y:F1},{pos.z:F1}) boundsY={boundsY:F2} ({phase})");
        }

        // ── 프레이밍 실측 ──

        /// <summary>[92차 결함3] 대상 전체 bounds — 활성 렌더러 합산. 부재 시 false.
        /// [92차 후속 F-A] 프레이밍 경로 미사용 — 진단 로그(boundsY) 전용으로 강등.</summary>
        private static Bounds MeasureBounds(Transform target, out bool has)
        {
            has = false;
            Bounds b = default;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>대상 얼굴/상체 라인(m) — 렌더러 bounds 실측(최상단에서 약간 아래). 부재 시 1.6m 폴백.
        /// [89차] Transform 시그니처 일반화(로직 무변경).</summary>
        private static float MeasureHeadY(Transform target)
        {
            float headY = 1.6f;
            bool has = false;
            Bounds b = default;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            if (has)
                headY = Mathf.Clamp(b.max.y - b.size.y * 0.12f, 1.1f, 2.3f);
            return headY;
        }

        /// <summary>사선 방향 — 플레이어 방향을 25° 꺾은 3/4 뷰. 플레이어 부재 시 대상 정면 기준.
        /// [89차] Transform 시그니처 일반화(로직 무변경). [92차] 병사/영주 경로 전용(주민은 정면 폐기).</summary>
        private static Vector3 ComputeSightlineDir(Transform target, Vector3 pos)
        {
            Vector3 baseDir = target.forward;
            Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player != null)
            {
                Vector3 toPlayer = player.position - pos;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                    baseDir = toPlayer.normalized;
            }
            return Quaternion.Euler(0f, SightYaw, 0f) * baseDir;
        }
    }
}
