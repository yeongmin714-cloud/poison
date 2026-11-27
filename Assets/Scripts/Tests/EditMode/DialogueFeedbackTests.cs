using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [73차 Phase B] 상호작용 피드백(StatusMessageChanged 말풍선/토스트/시드 골드) + 대화 카메라 확대 계약.
    /// 계획서: docs/2026-10-15_73TH_SLEEP_PANEL_AND_DIALOGUE_PLAN.md Phase B.
    /// GoldEconomyTests 선례 — 소스 어서션(마커) + EditMode AddComponent Awake 미호출 규약(67차 실측).
    /// 진단(사실 재구성): ①결과 피드백 부재(_statusMessage 세팅만 — 표시 경로 없음) ②조용한 실패(뇌물 골드 부족
    /// false 조용함 — 16씬 골드 0) ③대화하기는 statusMessage 세팅뿐(카메라/말풍선 없음).
    /// </summary>
    public class DialogueFeedbackTests
    {
        // ── 1. StatusMessageChanged 이벤트 + SetStatusMessage 래핑 ──

        [Test]
        public void GuardPlaceholder_StatusMessageChanged_Event_WrappedBySetStatusMessage()
        {
            var go = new GameObject("DFTest_Guard");
            try
            {
                var guard = go.AddComponent<GuardPlaceholder>();   // EditMode — Awake 미호출 규약

                string received = null;
                int fired = 0;
                System.Action<string> handler = msg => { fired++; received = msg; };
                guard.StatusMessageChanged += handler;
                try
                {
                    var m = typeof(GuardPlaceholder).GetMethod("SetStatusMessage",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert.That(m, Is.Not.Null, "[73차 Phase B] SetStatusMessage 세터 래핑 메서드 존재.");

                    m.Invoke(guard, new object[] { "테스트 대사" });
                    Assert.That(fired, Is.EqualTo(1), "SetStatusMessage 경유 변경 시 StatusMessageChanged 1회 발화.");
                    Assert.That(received, Is.EqualTo("테스트 대사"), "이벤트 인자 = 변경된 메시지.");
                    Assert.That(guard.StatusMessage, Is.EqualTo("테스트 대사"),
                        "StatusMessage 프로퍼티 유지(278행 계약) — 래핑 후에도 동일 값.");
                }
                finally
                {
                    guard.StatusMessageChanged -= handler;
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GuardPlaceholder_AllDirectAssignments_WrappedIntoSetStatusMessage()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.That(src, Does.Contain("public event System.Action<string> StatusMessageChanged"),
                "[73차 Phase B] StatusMessageChanged 이벤트 신설.");
            Assert.That(src, Does.Contain("private void SetStatusMessage(string message)"),
                "SetStatusMessage(msg) 경유 — 직접 대입 전수 교체.");
            Assert.That(src, Does.Contain("StatusMessageChanged?.Invoke(_statusMessage);"),
                "세터 내 이벤트 fire.");
            Assert.That(src, Does.Contain("public string StatusMessage => _statusMessage;"),
                "StatusMessage 프로퍼티 유지.");

            // 직접 대입 잔존 검사 — 필드 선언 + 세터 본문 2곳만 허용(전수 grep 계약).
            int direct = 0;
            foreach (var raw in System.IO.File.ReadAllLines("Assets/Scripts/Systems/GuardPlaceholder.cs"))
            {
                if (raw.Contains("_statusMessage = "))
                    direct++;
            }
            Assert.That(direct, Is.EqualTo(2),
                $"_statusMessage 직접 대입은 필드 선언+세터 본문 2곳만 허용(실제={direct}) — 대입처 잔존 시 이벤트 미발화.");
        }

        // ── 2. GuardTalkBubble — 월드 말풍선 ──

        [Test]
        public void GuardTalkBubble_Show_ShowsAboveHead_4sTimerReset_OnRetrigger()
        {
            var guardGo = new GameObject("DFTest_BubbleGuard");
            var bubbleGo = new GameObject("DFTest_Bubble");
            try
            {
                var guard = guardGo.AddComponent<GuardPlaceholder>();
                guardGo.transform.position = new Vector3(3f, 0f, 5f);
                var bubble = bubbleGo.AddComponent<GuardTalkBubble>();

                bubble.ShowInstance(guard, "첫 대사");
                var text = GetPrivateField<TextMesh>(bubble, "_text");
                Assert.That(text, Is.Not.Null, "TextMesh 리그 지연 생성.");
                Assert.That(text.text, Is.EqualTo("첫 대사"), "말풍선 텍스트 교체.");
                Assert.That(GetPrivateField<float>(bubble, "_remaining"), Is.EqualTo(GuardTalkBubble.DisplaySeconds),
                    "표시 타이머 = 4초 계약.");

                var textT = GetPrivateField<Transform>(bubble, "_textT");
                Assert.That(textT.position.y, Is.EqualTo(guardGo.transform.position.y + GuardTalkBubble.HeadOffsetY)
                    .Within(0.01f), "대상 머리 위(y+2.2m) 배치.");

                // 연속 대사 — 재발화 시 타이머 리셋 + 텍스트 교체.
                bubble.ShowInstance(guard, "두 번째 대사");
                Assert.That(text.text, Is.EqualTo("두 번째 대사"), "연속 대사 텍스트 교체.");
                Assert.That(GetPrivateField<float>(bubble, "_remaining"), Is.EqualTo(GuardTalkBubble.DisplaySeconds),
                    "재발화 시 타이머 리셋.");
            }
            finally
            {
                Object.DestroyImmediate(bubbleGo);
                Object.DestroyImmediate(guardGo);
            }
        }

        [Test]
        public void GuardTalkBubble_Contract_StaticBootSubscription_Billboard()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardTalkBubble.cs");
            Assert.That(src, Does.Contain("public const float DisplaySeconds = 4f"),
                "[73차 Phase B] 말풍선 4초 표시 계약.");
            Assert.That(src, Does.Contain("HeadOffsetY = 2.2f"), "대상 머리 위 y+2.2m.");
            Assert.That(src, Does.Contain("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]"),
                "정적 부트(EInteractionPromptUTK 선례) — 구독 자동 배선.");
            Assert.That(src, Does.Contain("StatusMessageChanged"), "StatusMessageChanged 구독.");
            Assert.That(src, Does.Contain("rotation = cam.transform.rotation"),
                "카메라 빌보드(DamageFont 선례 — 텍스트 트랜스폼이 카메라 회전을 따름).");
            Assert.That(src, Does.Contain("LegacyRuntime.ttf"), "TextMesh 폰트(EInteractionPromptUTK 선례).");
            Assert.That(src, Does.Not.Contain("CreatePrimitive"), "배경 프리미티브 없음 — TextMesh만(깔끔).");
        }

        // ── 3. DialogueCameraDirector — 대화 카메라 확대 ──

        [Test]
        public void DialogueCameraDirector_Begin_DirectControl_End_Restores()
        {
            var camGo = new GameObject("DFTest_MainCam");
            var guardGo = new GameObject("DFTest_CamGuard");
            var playerGo = new GameObject("DFTest_Player");
            try
            {
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                camGo.transform.position = new Vector3(0f, 5f, -10f);
                camGo.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
                Vector3 savedPos = camGo.transform.position;
                Quaternion savedRot = camGo.transform.rotation;

                playerGo.tag = "Player";
                playerGo.transform.position = new Vector3(0f, 0f, 3f);

                var guard = guardGo.AddComponent<GuardPlaceholder>();
                guardGo.transform.position = Vector3.zero;
                guardGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 플레이어 반대 방향에서 시작

                // [73차 수리] EditMode에서 Camera.main 캐시는 씬 상태 의존 — 테스트 주입 경로(MainCameraOverride)로 고정.
                DialogueCameraDirector.MainCameraOverride = () => cam;

                int camCountBefore = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;

                int started = 0, ended = 0;
                // [89차] 이벤트 타입 일반화(GuardPlaceholder → Component) — 핸들러 시그니처 동기 갱신.
                System.Action<Component> onStart = _ => started++;
                System.Action<Component> onEnd = _ => ended++;
                DialogueCameraDirector.DialogueStarted += onStart;
                DialogueCameraDirector.DialogueEnded += onEnd;
                try
                {
                    DialogueCameraDirector.Begin(guard);

                    Assert.That(DialogueCameraDirector.IsActive, Is.True, "Begin → 대화 카메라 활성.");
                    Assert.That(started, Is.EqualTo(1), "DialogueStarted 발화.");

                    // [92차 결함3] 스왑 폐기 — 기존 Main Camera 직접 제어(카메라 개수 불변, enabled 유지).
                    var directed = DialogueCameraDirector.DialogueCamera;
                    Assert.That(directed, Is.Not.Null, "제어 카메라 참조(=기존 Main Camera).");
                    Assert.That(directed, Is.EqualTo(cam), "[92차] 신규 생성 아님 — 기존 카메라 직접 제어.");
                    Assert.That(directed.enabled, Is.True, "[92차] 기존 카메라 enabled 유지(스왑 폐기 — 렌더 생존).");
                    Assert.That(directed.CompareTag("MainCamera"), Is.True, "MainCamera 태그 불변(태그 교체 폐기).");
                    Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,
                        Is.EqualTo(camCountBefore), "[92차] 카메라 개수 불변(스왑 폐기).");

                    // [105차 B] fullBody=true(전 대상 전신 정면 통일) — FOV 40 고정(FullBodyFov), near 보정 0.15, 거리 ≈ 2.92m((1.8×0.5)/tan(20°)×1.18).
                    Assert.That(cam.fieldOfView, Is.EqualTo(40f), "[105차 B] FOV 40 고정(전신 프레이밍 FullBodyFov) 적용.");
                    Assert.That(cam.nearClipPlane, Is.EqualTo(0.15f), "[92차] near plane 0.15 보정(저장/원복 포함).");
                    float dist = Vector3.Distance(cam.transform.position, guardGo.transform.position);
                    Assert.That(dist, Is.InRange(1.5f, 3.35f), "[105차 B] 전신 프레이밍 거리 — 수평 계약 ≈2.92m + 카메라 높이 1.5m(루트→카메라 3D 실측 ≈3.28m, 어서션 상한 계산 착오 정정).");
                    Assert.That(cam.transform.position, Is.Not.EqualTo(savedPos),
                        "[92차] 제어 중 트랜스폼 이동(구 '훼손 없음' 어서션 폐기 — 저장 후 이동/End 원복 계약).");

                    // 대상 병사 플레이어 쪽 시선 회전(대화 자세).
                    Vector3 toPlayer = (playerGo.transform.position - guardGo.transform.position).normalized;
                    Assert.That(Vector3.Dot(guardGo.transform.forward, toPlayer), Is.GreaterThan(0.9f),
                        "대상 병사 플레이어 쪽으로 시선 회전.");

                    DialogueCameraDirector.End();

                    Assert.That(DialogueCameraDirector.IsActive, Is.False, "End → 비활성.");
                    Assert.That(ended, Is.EqualTo(1), "DialogueEnded 발화.");
                    // [92차 결함3] 원복 — 파괴/enabled 스왑 없음: 트랜스폼/FOV/near 저장값 복원.
                    Assert.That(cam.enabled, Is.True, "카메라 enabled 불변(스왑 폐기 — 렌더 경합 원인 제거).");
                    Assert.That(cam.transform.position, Is.EqualTo(savedPos), "[92차] position 원복.");
                    Assert.That(cam.transform.rotation, Is.EqualTo(savedRot), "[92차] rotation 원복.");
                    Assert.That(cam.fieldOfView, Is.EqualTo(60f), "[92차] FOV 원복.");
                    Assert.That(cam.nearClipPlane, Is.EqualTo(0.3f), "[92차] near plane 원복.");
                    Assert.That(DialogueCameraDirector.DialogueCamera, Is.Null, "[92차] 제어 참조 해제(null — 구 '신규 카메라 파괴' 대체).");
                }
                finally
                {
                    DialogueCameraDirector.DialogueStarted -= onStart;
                    DialogueCameraDirector.DialogueEnded -= onEnd;
                    DialogueCameraDirector.End();   // 멱등 — 잔여 정리
                    DialogueCameraDirector.MainCameraOverride = null;   // [73차 수리] 주입 원복
                }
            }
            finally
            {
                Object.DestroyImmediate(guardGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void DialogueCameraDirector_Contract_AutoEndTimer_InputExit_NoAudioListener()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");
            Assert.That(src, Does.Not.Contain("AutoEndSeconds"),
                "[105차 B] 5초 타이머 폐기 — 세션 수명 = 대화창 수명(패널 닫힘 폴링으로 단일화).");
            Assert.That(src, Does.Contain("wasPressedThisFrame"),
                "이동/ESC 입력 감지 — Keyboard.current wasPressedThisFrame(양 입력 체계 커버).");
            // [91차 F5] 유예/선별 종료로 계약 갱신 — 전수 스캔(allKeys)은 유예 없는 즉시 End(확대 소실
            // 실측)의 원인으로 폐기됐다. ESC/이동키 선별 어서션으로 교체.
            Assert.That(src, Does.Contain("escapeKey.wasPressedThisFrame"),
                "[91차 F5] ESC 종료(선별).");
            Assert.That(src, Does.Contain("wKey.wasPressedThisFrame"),
                "[91차 F5] WASD/화살표 이동키 종료(선별 — 타이핑/기타 키 무시).");
            Assert.That(src, Does.Contain("IsDead"), "대상 사망 시 즉시 End.");
            // [89차] 이벤트 타입 일반화(GuardPlaceholder → Component) — 주민 대화 지원, 소비처 0 실측 무영향.
            Assert.That(src, Does.Contain("public static event System.Action<Component> DialogueStarted"),
                "[73차/89차] DialogueStarted 이벤트(Component 일반화).");
            Assert.That(src, Does.Contain("public static event System.Action<Component> DialogueEnded"),
                "[73차/89차] DialogueEnded 이벤트(Component 일반화).");
            Assert.That(src, Does.Not.Contain("AddComponent<AudioListener>"),
                "AudioListener 중복 방지 — 카메라에 미부여.");
            // [92차 결함3] CopyFrom 폐기(신규 카메라 계승 부재) — 저장/원복 계약으로 대체.
            Assert.That(src, Does.Not.Contain("CopyFrom"),
                "[92차 결함3] 신규 카메라 파라미터 계승(CopyFrom) 폐기 — 기존 카메라 직접 제어.");
        }

        [Test]
        public void DialogueCameraDirector_Contract_GraceAndReframe_91()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");
            Assert.That(src, Does.Contain("[91차 F5]"), "[91차 F5] 마커 주석.");
            Assert.That(src, Does.Contain("public const float InputGraceSeconds = 0.8f"),
                "[91차 F5] 입력 유예 0.8s — Begin 직후 확대 소실(마을씬 실측) 수리.");
            Assert.That(src, Does.Contain("_elapsedSinceBegin < InputGraceSeconds"),
                "[91차 F5] 유예 경과 전 입력 종료 무시.");
            Assert.That(src, Does.Contain("escapeKey.wasPressedThisFrame"),
                "[91차 F5] 유예 후 ESC 종료.");
            Assert.That(src, Does.Contain("kb.wKey.wasPressedThisFrame").Or.Contain("kb.upArrowKey.wasPressedThisFrame"),
                "[91차 F5] 유예 후 WASD/화살표 이동키만 종료(선별).");
            Assert.That(src, Does.Not.Contain("allKeys"),
                "[91차 F5] 전수 키 스캔 폐기 — 소실 원인 제거.");
            Assert.That(src, Does.Contain("ReframeCamera"),
                "[91차 F5] 매 프레임 재조준 진입(Update 경유).");
            Assert.That(src, Does.Contain("_dialogueCam.transform.LookAt(facePos)"),
                "[91차 F5] 매 프레임 LookAt — 대상 머리(bounds max.y−12%).");
            Assert.That(src, Does.Contain("Quaternion.Euler(0f, 90f, 0f)"),
                "[91차 F5] 클립 방지 — dir 90° 측면 사선 회전.");
            Assert.That(src, Does.Contain("ClipAvoidMinDistance = 0.9f"),
                "[91차 F5] 클립 방지 최소 거리 0.9m.");
        }

        // ── 3-1. [92차 결함3] 스왑 폐기 + 기존 Main Camera 직접 제어 + 전신 프레이밍 계약 ──

        [Test]
        public void DialogueCameraDirector_Contract_SwapAbolished_DirectControl_FullBody_92()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");
            // [92차 결함3] 스왑 폐기(최우선) — 마을씬 테스트 2.mp4 실측: 대화 세션 2회 동안 게임뷰
            //   11.8초 전면 동결(픽셀 diff 0.000) = 신규 카메라 스왑(생성+MainCamera 태그 교체+
            //   구카메라 disabled)이 렌더를 마비. 동결 근거 주석 유지 확인.
            Assert.That(src, Does.Not.Contain("new GameObject(\"DialogueCamera\")"),
                "[92차 결함3] 신규 DialogueCamera 생성 폐기(동결 원인 제거).");
            Assert.That(src, Does.Not.Contain("_dialogueCamGo"),
                "[92차 결함3] 신규 카메라 GO 참조 필드 제거(Destroy 복원 로직 소멸).");
            Assert.That(src, Does.Not.Contain("_savedCamera.enabled"),
                "[92차 결함3] 구카메라 enabled 스왑 폐기(생존 계약 — enabled 불변).");
            Assert.That(src, Does.Contain("[92차 결함3"), "[92차 결함3] 마커 주석.");
            Assert.That(src, Does.Contain("11.8초"), "[92차 결함3] 동결 근거 주석(11.8초 0.000 diff) 유지.");

            // [92차 결함3] 기존 Main Camera 직접 제어 — 컨트롤러 정지/재개(GetComponent 탐색, 부재 씬 스킵).
            Assert.That(src, Does.Contain("GetComponent<TopDownCameraController>()"),
                "[92차 결함3] 컨트롤러 탐색 = GetComponent만(예외 금지 — 타입 부재 씬 스킵).");
            Assert.That(src, Does.Contain("TopDownCameraController"),
                "[92차 결함3] TopDownCameraController 비활성/원복 코드 존재.");
            Assert.That(src, Does.Contain("_savedController.enabled = false"),
                "[92차 결함3] Begin → 컨트롤러 enabled=false(카메라 트랜스폼 독점).");
            Assert.That(src, Does.Contain("_savedController.enabled = true"),
                "[92차 결함3] End → 컨트롤러 enabled 재개(원복 계약).");

            // [92차 결함3] 전신 프레이밍 파라미터 + 산식 + 저장/원복.
            Assert.That(src, Does.Contain("bool fullBody"),
                "[92차 결함3] fullBody 파라미터 존재(주민 전신 정면 / 병사·영주 기존).");
            Assert.That(src, Does.Contain("public static void Begin(Component target, string displayName, bool fullBody"),
                "[92차 결함3] Begin(Component, displayName, fullBody) 오버로드 존재.");
            Assert.That(src, Does.Contain("public static void Begin(NPCAmbientDialogue target, string displayName)"),
                "[92차 결함3] 주민 타입 진입점 — InteractionPanelUTK 호출 문자열 계약 유지 + fullBody:true 주입.");
            Assert.That(src, Does.Contain("FullBodyFov = 40f"),
                "[92차 결함3] 전신 FOV 40° 고정.");
            Assert.That(src, Does.Contain("FullBodyFrameFactor = 1.18f"),
                "[92차 결함3] dist 산식 ×1.18 — (bounds높이×0.5)/tan(FOV/2)×1.18(1.8m 기준 ≈2.95m).");
            Assert.That(src, Does.Contain("Mathf.Tan("), "[92차 결함3] FOV 기반 거리 산식.");
            Assert.That(src, Does.Contain("NearPlaneFix = 0.15f"),
                "[92차 결함3] near plane 0.15 보정(저장/원복 포함).");

            // [92차 결함3] 렌더 진단 로그 — Begin/End 1회(Camera.allCameras 요약).
            Assert.That(src, Does.Contain("[DialogueCameraDirector] 렌더 상태 — camCount="),
                "[92차 결함3] 렌더 상태 진단 로그(camCount/enabled/depth/pos).");
            Assert.That(src, Does.Contain("LogRenderState(\"Begin\")"), "[92차 결함3] Begin 진단 1회.");
            Assert.That(src, Does.Contain("LogRenderState(\"End\")"), "[92차 결함3] End 진단 1회.");
        }

        /// <summary>
        /// [92차 후속 F-A] fullBody 프레이밍 bounds 불신 → 1.8m 고정 계약 + 진단 로그 수리(소스 어서션).
        /// 근거: 렌더 상태 Begin 로그 3세션 전부 pos y=6.3~6.8(프레이밍 적용 후 값 — 1.8m NPC라면
        /// center.y≈0.9 기대) → 주민 SkinnedMeshRenderer bounds 비대(center.y≈6.8·size.y≈13m 추정)가
        /// fullBody 프레이밍을 dist≈22m·높이 6.8m로 발사(영상 25.5s NPC 화면 높이 5~7%).
        /// </summary>
        [Test]
        public void DialogueCameraDirector_Contract_FixedFrameH_DiagBoundsY_EndLogOrder_92Followup()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");

            Assert.That(src, Does.Contain("[92차 후속 F-A]"), "[92차 후속 F-A] 마커 주석.");
            Assert.That(src, Does.Contain("[92차 후속]"), "[92차 후속] 마커 주석.");
            // ① 1.8m 고정 계약 — frameH/center/dist 산식(Begin 로그 y=6.8 3세션 실측 근거 주석 동반).
            Assert.That(src, Does.Contain("FullBodyFixedFrameH = 1.8f"),
                "[92차 후속 F-A] frameH 1.8m 고정(프로젝트 캐릭터 키 계약 — 전 캐릭터 1.8m).");
            Assert.That(src, Does.Contain("FullBodyCenterUpM = 0.9f"),
                "[92차 후속 F-A] center = target.position + 0.9m up 고정.");
            Assert.That(src, Does.Contain("Mathf.Tan("),
                "[92차 후속 F-A] dist = (1.8×0.5)/tan(20°)×1.18 ≈ 2.92m 산식 존재.");
            Assert.That(src, Does.Contain("y=6.3~6.8"),
                "[92차 후속 F-A] 근거 주석(Begin 로그 3세션 실측 pos y=6.3~6.8) 유지.");
            // MeasureBounds 프레이밍 경로 미사용 — 구 프레이밍 호출부(MeasureBounds(targetT) 두 곳) 제거,
            //   진단 전용 호출(MeasureBounds(_target.transform) — LogRenderState boundsY)만 잔존.
            Assert.That(src, Does.Not.Contain("MeasureBounds(targetT"),
                "[92차 후속 F-A] 프레이밍 경로의 MeasureBounds 호출 제거(Begin/Reframe fullBody 분기).");
            Assert.That(src, Does.Contain("MeasureBounds(_target.transform, out bool hasB)"),
                "[92차 후속] MeasureBounds 진단 전용 잔존 — LogRenderState boundsY 산출.");
            // ② Begin 진단 로그 boundsY 필드 — 비대 bounds 근본 확정용.
            Assert.That(src, Does.Contain("boundsY={boundsY:F2}"),
                "[92차 후속] Begin 진단 로그 boundsY 필드(대상 bounds size.y).");
            // ③ End 진단 로그 캡처 순서 수리 — 복원 직후(참조 해제 전) 캡처. 구현은 _dialogueCam=null
            //   이후 호출해 pos=(0,0,0)이 찍혔다(진단 버그 — 동작 무관).
            int endLog = src.IndexOf("LogRenderState(\"End\")");
            int nullAssign = src.IndexOf("_dialogueCam = null;");
            Assert.That(endLog, Is.GreaterThan(0), "End 진단 로그 호출 존재.");
            Assert.That(nullAssign, Is.GreaterThan(0), "_dialogueCam=null 해제 존재.");
            Assert.That(endLog, Is.LessThan(nullAssign),
                "[92차 후속] End 로그가 _dialogueCam=null 해제 전에 캡처(복원 상태 반영 — pos=(0,0,0) 버그 수리).");
            // ④ ReframeCamera fullBody 분기 — 고정 center 재조준(bounds 기반 center 재계산 제거).
            //   [103차 수리2] 계약 전이 — 구 산식(target.position + up×0.9 직접 구성) 폐기 →
            //   ComputeFullBodyCenter 단일소스(Foot 본 기준 footY+0.9, 폴백 root+0.9 — [103차 F-1]).
            //   Begin(286행)/ReframeCamera(644행) 양 경로 동일 호출 — 소스 실측 문자열과 일치.
            Assert.That(src, Does.Contain("Vector3 center = ComputeFullBodyCenter(targetT);"),
                "[103차 F-1] ReframeCamera fullBody center = ComputeFullBodyCenter(targetT) 단일소스"
                + "(Foot 본 기준 — 구 target.position+up×0.9 직접 구성 폐기, Begin과 동일 호출 일원화).");
        }

        /// <summary>
        /// [92차 결함3 → 92차 후속 F-A → 94차 F1 → 96차 F1] 전신 프레이밍 동작 테스트 — 계약 진화: 구 bounds
        /// 산식(dist=(b.size.y×0.5)/tan20°×1.18·center=b.center) 폐기 → 1.8m 고정 계약
        /// (center=target.position+0.9up, dist≈2.92m — 비대 bounds 프레이밍 붕괴 수리) → [94차 F1]
        /// 고정 베어링 프레이밍(수평 오프셋 = 고정 베어링×2.92m — 대상 고정) → [96차 F1] 측면 오프셋 합성
        /// (수평 오프셋 = 베어링×2.92m + perpendicular×0.9m, 높이 = center.y+0.7m — 플레이어 가림 수리) →
        /// [100차 F-2] 정면 보정 → [103차 F-3] 완전 정면(측면 오프셋 0.45m 폐기 → FullBodySideOffsetM=0.0,
        /// 높이 max 가드 폐기 → camPos.y = center.y + 0.6m 단일식 / LookAt 상향 0.15m).
        /// </summary>
        [Test]
        public void DialogueCameraDirector_FullBody_FrontalFraming_ControllerToggle_92()
        {
            var camGo = new GameObject("DF92_MainCam");
            var playerGo = new GameObject("DF92_Player");
            GameObject villagerGo = null;
            try
            {
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                camGo.transform.position = new Vector3(10f, 20f, -30f);
                camGo.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
                var ctrl = camGo.AddComponent<TopDownCameraController>();
                Assert.That(ctrl.enabled, Is.True, "사전 조건 — 컨트롤러 활성.");

                playerGo.tag = "Player";
                playerGo.transform.position = new Vector3(0f, 0f, -5f);

                // 전신 프레이밍 대상 — 1m 큐브(bounds center y=0.9, size.y=1.0)
                villagerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                villagerGo.name = "DF92_Villager";
                villagerGo.transform.position = new Vector3(0f, 0.9f, 0f);

                int camCountBefore = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;

                DialogueCameraDirector.MainCameraOverride = () => cam;
                try
                {
                    DialogueCameraDirector.Begin(villagerGo.transform, "주민", fullBody: true);
                    Assert.That(DialogueCameraDirector.IsActive, Is.True, "Begin → 활성.");

                    // [92차 결함3] 스왑 폐기 — 카메라 개수 불변 + 컨트롤러 정지 + 카메라 생존.
                    Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,
                        Is.EqualTo(camCountBefore), "[92차] 카메라 개수 불변(신규 생성 없음).");
                    Assert.That(ctrl.enabled, Is.False, "[92차] Begin → TopDownCameraController 비활성.");
                    Assert.That(cam.enabled, Is.True, "[92차] 기존 카메라 enabled 유지.");

                    // 전신 정면 프레이밍 — [92차 후속 F-A] → [94차 F1] → [96차 F1] → [99차 F-B] →
                    //   [103차 F-3] 계약 진화: 1.8m 고정 계약(FOV 40/dist≈2.92m) 유지 + 고정 베어링 +
                    //   B-1 반전(카메라 = center − 베어링×dist — 플레이어 측, NPC 정면 촬영) +
                    //   [103차 F-3] 완전 정면 — 측면 오프셋 폐기(FullBodySideOffsetM = 0.0f) + 높이 단일식
                    //   (camPos.y = center.y + FullBodyCamHeightUpM(0.6m) — B-2 플레이어 y 가드 폐기,
                    //   가림 원천은 [103차 F-2] 플레이어 렌더러 숨김으로 제거). 구 "측면 0.9→0.45m"/
                    //   "높이 max(center.y+0.6, playerY+1.75)" 순차 폐기.
                    Assert.That(cam.fieldOfView, Is.EqualTo(40f), "[92차] fullBody — FOV 40 고정.");
                    Assert.That(cam.nearClipPlane, Is.EqualTo(0.15f), "[92차] near plane 0.15 보정.");

                    // [103차 F-1] 합성 기하 — center = ComputeFullBodyCenter 단일소스(internal 직접판정 —
                    //   InternalsVisibleTo 선례). 발 본 부재 큐브는 폴백(root+0.9up) = 기존 기하 동일 —
                    //   대상 큐브(position y=0.9)도 bounds와 무관하게 동일 산식 적용이 수리 본질.
                    Vector3 center = DialogueCameraDirector.ComputeFullBodyCenter(villagerGo.transform);
                    float dist = (1.8f * 0.5f) / Mathf.Tan(20f * Mathf.Deg2Rad) * 1.18f;
                    Vector3 bearing = villagerGo.transform.position - playerGo.transform.position;
                    bearing.y = 0f;
                    bearing.Normalize();
                    // [103차 F-3] perpendicular(sideDir) 성분 = FullBodySideOffsetM(0.0)×로 소멸 — 선언 폐기.
                    // [103차 F-3] 높이 단일식 — camPos.y = center.y + 0.6m(B-2 가드 폐기 — 플레이어 y 무관).
                    Assert.That(Mathf.Abs(cam.transform.position.y - (center.y + 0.6f)), Is.LessThan(0.02f),
                        "[103차 F-3] 카메라 높이 = center.y + FullBodyCamHeightUpM(0.6m) 단일식(B-2 max 가드 폐기 — [103차 F-2] 플레이어 렌더러 숨김으로 가림 원천 제거).");
                    // [103차 F-3] 완전 정면 — 측면 오프셋 0(데이터 0 판정): 수평 오프셋 = −베어링×dist 단독.
                    Vector3 expectedOffset = -bearing * dist;   // [103차 F-3] FullBodySideOffsetM = 0.0 — 측면 성분 소멸(곱 구조 유지)
                    Vector3 camOffset = cam.transform.position - center;
                    camOffset.y = 0f;
                    Assert.That(Vector3.Distance(camOffset, expectedOffset), Is.LessThan(0.02f),
                        "[99차 F-B → 103차 F-3] B-1 수평 오프셋 = center − 베어링×2.92m(플레이어 측) + 측면(perpendicular)×0.0m — 완전 정면(측면 0.45m 폐기, 0 데이터 판정).");
                    Assert.That(Vector3.Dot(camOffset.normalized, expectedOffset.normalized), Is.GreaterThan(0.99f),
                        "[96차 F1 → 103차 F-3] 합성 방향 = 베어링 단독(측면 성분 0 — 고정 베어링 성분 유지 — SightYaw 사선 폐기 유지).");
                    Assert.That(Vector3.Dot(cam.transform.forward, (center - cam.transform.position).normalized),
                        Is.GreaterThan(0.99f), "[94차 F1 → 100차 F-2] LookAt = center + up×0.15m(머리 쪽 — center 근방 조준 dot>0.99 — Begin 시점 검증).");

                    // [96차 F1 → 103차 F-3] 가림 수리 본질 전이 — 측면 오프셋 폐기(0.0)로 시선은 플레이어
                    //   축상과 정렬되지만 [103차 F-2] 플레이어 렌더러 숨김으로 가림 원천 자체가 제거됨.
                    //   본 어서션은 테스트 기하(플레이어가 카메라 뒤 z=-5)에서 성립하는 하한 핀으로 유지
                    //   (시선-플레이어 최근접 ≥ 0.35m — 렌더러 숨김 계약의 보조 기하 핀).
                    Vector3 sightDir = (center - cam.transform.position).normalized;
                    Vector3 toPlayer = playerGo.transform.position - cam.transform.position;
                    float proj = Mathf.Max(Vector3.Dot(toPlayer, sightDir), 0f);
                    Vector3 closestOnSight = cam.transform.position + sightDir * proj;
                    Assert.That(Vector3.Distance(closestOnSight, playerGo.transform.position),
                        Is.GreaterThan(0.35f),
                        "[96차 F1] 시선이 플레이어 캡슐(반경 0.35m 근사)과 교차하지 않음 — 플레이어 가림 수리 검증.");

                    DialogueCameraDirector.End();

                    Assert.That(ctrl.enabled, Is.True, "[92차] End → 컨트롤러 재개.");
                    Assert.That(cam.transform.position, Is.EqualTo(new Vector3(10f, 20f, -30f)), "[92차] position 원복.");
                    // [92차] rotation 원복 — Quaternion 정확 비교는 부동소수 비트 차이(Expected/But was 표시값 동일)로
                    //   오탐하므로 각도 허용오차 비교로 갱신. 원복 로직(DialogueCameraDirector)은 표시값 동일로 미수정.
                    Assert.That(Quaternion.Angle(cam.transform.rotation, Quaternion.Euler(10f, 20f, 30f)), Is.LessThan(0.5f),
                        "[92차] rotation 원복(부동소수 비트 차이 허용 — Quaternion.Angle < 0.5°).");
                    Assert.That(cam.fieldOfView, Is.EqualTo(60f), "[92차] FOV 원복.");
                    Assert.That(cam.nearClipPlane, Is.EqualTo(0.3f), "[92차] near plane 원복.");
                    Assert.That(DialogueCameraDirector.DialogueCamera, Is.Null, "[92차] 제어 참조 해제.");
                }
                finally
                {
                    DialogueCameraDirector.End();   // 멱등 — 잔여 정리
                    DialogueCameraDirector.MainCameraOverride = null;   // 주입 원복
                }
            }
            finally
            {
                if (villagerGo != null) Object.DestroyImmediate(villagerGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(camGo);
            }
        }

        /// [100차 F-2 B-2 → 103차 F-3 폐기] 비가림 상향 가드(max(center.y+0.6m, playerY+1.75m)) 폐기 —
        /// 높이 단일식 camPos.y = center.y + FullBodyCamHeightUpM(0.6m) 계약 전환(테스트 92와 동일 신계약,
        /// 고지대 플레이어 기하로 가드 폐기 실측 보강 — ComputeFullBodyCenter internal 직접판정) ──

        [Test]
        public void DialogueCameraDirector_FullBody_HeadClearAbovePlayer_B2_99()
        {
            var camGo = new GameObject("DF99B2_MainCam");
            var playerGo = new GameObject("DF99B2_Player");
            GameObject villagerGo = null;
            try
            {
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                camGo.transform.position = new Vector3(10f, 20f, -30f);
                camGo.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
                var ctrl = camGo.AddComponent<TopDownCameraController>();
                Assert.That(ctrl.enabled, Is.True, "사전 조건 — 컨트롤러 활성.");

                // [103차 F-3] 가드 폐기 재현 — 플레이어를 고지대(y=10m)에 배치: 구 B-2였다면
                //   max(center.y+0.6m=2.4, 10+1.75)=11.75 상향이었으나, 신계약 단일식은 플레이어 y 무관
                //   2.4m 고정 — 고지대 플레이어에서도 하이 앵글로 튀지 않는 것이 본 재현의 판정점.
                playerGo.tag = "Player";
                playerGo.transform.position = new Vector3(0f, 10f, -5f);

                // 전신 프레이밍 대상 — 1m 큐브(bounds와 무관 1.8m 고정 산식 — 테스트 92 동일 기하).
                villagerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                villagerGo.name = "DF99B2_Villager";
                villagerGo.transform.position = new Vector3(0f, 0.9f, 0f);

                DialogueCameraDirector.MainCameraOverride = () => cam;
                try
                {
                    DialogueCameraDirector.Begin(villagerGo.transform, "주민", fullBody: true);
                    Assert.That(DialogueCameraDirector.IsActive, Is.True, "Begin → 활성.");

                    // [103차 F-1] center = ComputeFullBodyCenter 단일소스(internal 직접판정 —
                    //   InternalsVisibleTo 선례). 발 본 부재 큐브 = 폴백(root+0.9up) — 테스트 92 동일 기하.
                    Vector3 center = DialogueCameraDirector.ComputeFullBodyCenter(villagerGo.transform);
                    float dist = (1.8f * 0.5f) / Mathf.Tan(20f * Mathf.Deg2Rad) * 1.18f;
                    Vector3 bearing = villagerGo.transform.position - playerGo.transform.position;
                    bearing.y = 0f;
                    bearing.Normalize();

                    // [103차 F-3] 높이 단일식 — camPos.y = center.y + FullBodyCamHeightUpM(0.6m) = 1.8+0.6=2.4.
                    //   구 B-2(10+1.75=11.75) 폐기 — 고지대 플레이어(y=10m)에서도 2.4m 유지가 폐기 실측 본질.
                    Assert.That(Mathf.Abs(cam.transform.position.y - (center.y + 0.6f)), Is.LessThan(0.02f),
                        "[103차 F-3] 높이 단일식 — camPos.y = center.y + 0.6m(구 B-2 max(playerY+1.75) 가드 폐기 — 고지대 플레이어 y=10m에서도 하이 앵글 없음 실측).");

                    // [99차 F-B → 103차 F-3] B-1 유지 — 측면 오프셋 폐기(0.0) 반영 수평 오프셋.
                    Vector3 camOffset = cam.transform.position - center;
                    Vector3 expectedOffset = -bearing * dist;   // [103차 F-3] FullBodySideOffsetM = 0.0 — 측면 성분 소멸
                    camOffset.y = 0f;
                    Assert.That(Vector3.Distance(camOffset, expectedOffset), Is.LessThan(0.02f),
                        "[99차 F-B → 103차 F-3] B-1 유지 — 높이 단일식 전환 후에도 수평 오프셋 = center − 베어링×2.92m + 측면×0.0m(완전 정면 — 플레이어 측 배치).");

                    // [99차 F-B] B-1 촬영 방향 계약 — 카메라→NPC 방향과 플레이어→NPC 방향 동일(플레이어 측 설치).
                    Vector3 camToNpc = center - cam.transform.position;
                    camToNpc.y = 0f;
                    Assert.That(Vector3.Dot(camToNpc.normalized, bearing), Is.GreaterThan(0.9f),
                        "[99차 F-B] B-1 촬영 방향 계약 — 카메라→NPC 방향과 플레이어→NPC 방향 동일(내적 > 0).");

                    // [103차 F-3] 가드 폐기 소스 고정 핀 — max 산식/1.75m 상수 소멸 + 단일식 잔존 확인.
                    string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");
                    Assert.That(src, Does.Not.Contain("FullBodyPlayerHeadClearM = 1.75f"),
                        "[103차 F-3] B-2 비가림 상향 상수 폐기 — FullBodyPlayerHeadClearM(1.75f) 잔존 금지(폐기 블록 주석 히스토리만 허용).");
                    Assert.That(src, Does.Not.Contain("camPos.y = Mathf.Max(center.y + FullBodyCamHeightUpM, playerY + FullBodyPlayerHeadClearM);"),
                        "[103차 F-3] B-2 높이 max 산식 폐기 — 잔존 금지.");
                    Assert.That(src, Does.Contain("camPos.y = center.y + (_upperBodyMode ? UpperBodyCamHeightUpM : FullBodyCamHeightUpM);"),
                        "[103차 F-3 → 130차] 높이 단일식 — ComputeFullBodyCamPos 단일소스 유지, [130차] 상반신 분기 추가(전신 0.6m 경로 무변경).");
                }
                finally
                {
                    DialogueCameraDirector.End();   // 멱등 — 잔여 정리
                    DialogueCameraDirector.MainCameraOverride = null;   // 주입 원복
                }
            }
            finally
            {
                if (villagerGo != null) Object.DestroyImmediate(villagerGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(camGo);
            }
        }

        // ── 4. InteractionPanelUTK — 토스트 + 카메라 확대 라우팅 ──

        [Test]
        public void InteractionPanelUTK_BribeRecruit_Toasts_And_TalkCameraZoom()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");
            Assert.That(src, Does.Contain("DialogueCameraDirector.Begin(talkGuard)"),
                "[73차 Phase B] 대화하기 → 대화 카메라 확대 선행.");
            Assert.That(src, Does.Contain("talkGuard.BeginTalk()"), "BeginTalk 호출 유지(72차 경로).");
            Assert.That(src, Does.Contain("UTKToastService.Show"), "결과 토스트(조용한 실패 제거).");
            Assert.That(src, Does.Contain("골드 부족 — 뇌물 실패(30+Lv×25 골드 필요)"), "뇌물 실패 안내.");
            Assert.That(src, Does.Contain("뇌물 지급 — 호감도 상승"), "뇌물 성공 안내.");
            Assert.That(src, Does.Contain("입단비 부족"), "포섭 실패 안내.");
            Assert.That(src, Does.Contain("포섭 완료"), "포섭 성공 안내.");
            Assert.That(src, Does.Contain("bool bribed = bribeGuard.AttemptBribe()"),
                "뇌물 결과 bool 수신(AttemptBribe 시그니처 불변).");
        }

        // ── 5. 16씬 시드 골드 ──

        [Test]
        public void IndoorWarTestSetup_SeedsGold_ForBribeRecruitTesting()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/IndoorWarTestSetup.cs");
            Assert.That(src, Does.Contain("AddGold(500, \"test_seed\")"),
                "[73차 Phase B] 16씬 시드 골드(15씬 shop_test_setup 선례).");
            Assert.That(src, Does.Contain("PlayerStats.Instance != null"), "PlayerStats null 가드.");
            Assert.That(src.IndexOf("EnsurePlayer();", System.StringComparison.Ordinal),
                Is.LessThan(src.IndexOf("AddGold(500", System.StringComparison.Ordinal)),
                "EnsurePlayer 후 시드 골드(순서 계약).");
        }

        // ── 6. [93차 F1] 대화 카메라 덮어쓰기 근본 수리 — PlayerMovement 추적 억제 게이트 ──

        /// <summary>
        /// [93차 F1] 소스 어서션 — 근거: 마을씬 테스트 4.mp4(영상4) Begin 로그 pos=(-429.8,3.2,-87.1)
        /// boundsY=1.80(1.8m 고정 계약 프레이밍 정상 적용)인데 같은 순간 영상은 탑다운 원거리 그대로
        /// 줌인 전무. 원인 = PlayerMovement.ApplyFollowCamera가 LateUpdate에서 "모든 스크립트 이후 카메라
        /// 최종 적용" 계약대로 대화 중에도 매 프레임 추적 오빗을 덮어씀. 수리 = 정적 게이트
        /// (CameraFollowSuppressed) + ApplyFollowCamera 선두 가드 + Director Begin/End 세팅(멱등).
        /// </summary>
        [Test]
        public void DialogueCameraGate_93_Source_Contract()
        {
            var pmSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerMovement.cs");
            Assert.That(pmSrc, Does.Contain("public static bool CameraFollowSuppressed;"),
                "[93차 F1] 게이트 정적 필드(전역 1개 스위치).");
            Assert.That(pmSrc, Does.Contain("[93차 F1]"), "[93차 F1] 마커 주석.");
            // 가드 위치 — ApplyFollowCamera 본문 최선행(카메라 접근 전 차단 — 부작용 0 보장).
            int method = pmSrc.IndexOf("private void ApplyFollowCamera()");
            int guard = pmSrc.IndexOf("if (CameraFollowSuppressed) return;");
            Assert.That(method, Is.GreaterThan(0), "ApplyFollowCamera 메서드 존재.");
            Assert.That(guard, Is.GreaterThan(method), "[93차 F1] 가드가 ApplyFollowCamera 내부에 위치.");
            int firstCameraTouch = pmSrc.IndexOf("if (_cameraTransform == null)", method);
            Assert.That(guard, Is.LessThan(firstCameraTouch),
                "[93차 F1] 가드가 본문 최선행 — 억제 중 카메라 트랜스폼 접근 원천 차단.");

            var dcSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");
            Assert.That(dcSrc, Does.Contain("PlayerMovement.CameraFollowSuppressed = true;"),
                "[93차 F1] Begin → 추적 억제 ON.");
            Assert.That(dcSrc, Does.Contain("PlayerMovement.CameraFollowSuppressed = false;"),
                "[93차 F1] End → 추적 억제 OFF(멱등 — finally 성격).");
            // ON 위치 — 재진입 End(=false) 이후 최종 경로에서 설정(재진입 시 억제 유지 보장).
            int reentry = dcSrc.IndexOf("if (_active) End();");
            int beginTrue = dcSrc.IndexOf("CameraFollowSuppressed = true;");
            int endFalse = dcSrc.IndexOf("CameraFollowSuppressed = false;");
            Assert.That(reentry, Is.GreaterThan(0), "Begin 재진입 정리 존재.");
            Assert.That(beginTrue, Is.GreaterThan(reentry),
                "[93차 F1] 게이트 ON은 재진입 End 이후 — 재진입 Begin에서 억제 유지.");
            Assert.That(endFalse, Is.GreaterThan(beginTrue), "[93차 F1] End 세팅 존재(Begin/End 쌍).");
            // 병사 경로 자동 커버 — GuardPlaceholder 오버로드가 최종 경로로 위임(별도 코드 불요 계약).
            Assert.That(dcSrc, Does.Contain("Begin(target, target.GuardName);"),
                "[93차 F1] 병사 경로 위임 유지 — 최종 경로 경유 게이트 자동 커버.");
        }

        /// <summary>
        /// [93차 F1] 동작 테스트 — CameraFollowSuppressed=true일 때 ApplyFollowCamera가 카메라 트랜스폼을
        /// 건드리지 않는다(대화 프레이밍 보존). false면 기존 추적 오빗 적용(회귀 없음). 카메라/플레이어는
        /// 컴포넌트 주입(Camera.main 불요 — MainCameraOverride 선례 동일 취지), 프라이빗 메서드는 리플렉션 호출.
        /// </summary>
        [Test]
        public void PlayerMovement_CameraFollowSuppressed_Gate_BlocksApplyFollowCamera_93()
        {
            var playerGo = new GameObject("DF93_Player");
            var camGo = new GameObject("DF93_Cam");
            try
            {
                playerGo.tag = "Player";
                playerGo.transform.position = Vector3.zero;
                var pm = playerGo.AddComponent<PlayerMovement>();   // RequireComponent(CharacterController) 자동 부착

                camGo.AddComponent<Camera>();
                Vector3 sentinel = new Vector3(100f, 100f, 100f);
                camGo.transform.position = sentinel;
                // 주입 — Camera.main 캐시 불안 회피(_cameraTransform/_camera 동시 세팅 — Cinemachine 탐색 경로 NRE 방지)
                SetPrivateField(pm, "_cameraTransform", camGo.transform);
                SetPrivateField(pm, "_camera", camGo.GetComponent<Camera>());

                var m = typeof(PlayerMovement).GetMethod("ApplyFollowCamera",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.That(m, Is.Not.Null, "[93차 F1] ApplyFollowCamera 프라이빗 메서드 존재.");

                try
                {
                    // 게이트 ON — 억제 중엔 카메라 트랜스폼 무손상(대화 프레이밍이 살아남는 본질 어서션).
                    PlayerMovement.CameraFollowSuppressed = true;
                    m.Invoke(pm, null);
                    Assert.That(camGo.transform.position, Is.EqualTo(sentinel),
                        "[93차 F1] 억제 중 ApplyFollowCamera는 카메라 트랜스폼을 건드리지 않는다(매 프레임 덮어쓰기 차단).");

                    // 게이트 OFF — 플레이어 추적 오빗 정상 적용(기존 계약 회귀 없음).
                    PlayerMovement.CameraFollowSuppressed = false;
                    m.Invoke(pm, null);
                    Assert.That(camGo.transform.position, Is.Not.EqualTo(sentinel),
                        "[93차 F1] 억제 해제 시 추적 오빗 재개.");
                    Assert.That(Mathf.Abs(camGo.transform.position.x), Is.LessThan(0.1f),
                        "[93차 F1] 추적 오빗 x — 플레이어 축 정렬(오빗 산식 실측).");
                    Assert.That(camGo.transform.position.y, Is.GreaterThan(1f),
                        "[93차 F1] 추적 오빗 y — 지면 위 탑다운 위치(1.4m 피벗 + 피치 오빗).");
                }
                finally
                {
                    PlayerMovement.CameraFollowSuppressed = false;   // 정적 상태 오염 방지 — 후속 테스트 보호
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(playerGo);
            }
        }

        // ── 7. [94차 F1] 대화 카메라 대상 고정 — 고정 베어링 + fullBody 90° 회전 폐기 + LateUpdate 최종 적용권 ──

        /// <summary>
        /// [94차 F1] 소스 어서션 — 근거: 마을씬 테스트 5.mp4(66.7s) 포렌식. 잔존 결함 = 타일 7~8(전환)/
        /// 10~12(세션 중) NPC 측면/등 노출("카메라 방향 이상"). 원인 3: ①ReframeCamera가 현재 위치 기준
        /// dir 재투영(고정 베어링 아님) ②fullBody 경로 90° 클립 회전 잔존(발생 근거 없음) ③Director Update
        /// 우선권 — 추격 스크립트 타이밍 경합. 수리 = 고정 베어링(Begin 1회 확정·세션 내 불변·End 클리어) +
        /// fullBody 90° 회전 폐기(병사/영주 경로 무변경) + 업데이터 Update→LateUpdate(최종 적용권 —
        /// 93차 게이트 코드는 이중 안전으로 유지).
        /// </summary>
        [Test]
        public void DialogueCameraDirector_Contract_FixedBearing_LateUpdate_94()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");
            Assert.That(src, Does.Contain("[94차 F1]"), "[94차 F1] 마커 주석.");

            // ① 고정 베어링 — 정적 필드(Begin 1회 확정, 세션 내 불변, End 클리어) + 높이 상수.
            Assert.That(src, Does.Contain("private static Vector3 _fullBodyBearing;"),
                "[94차 F1] 고정 베어링 정적 필드 존재(세션 내 불변 — 재투영 폐기의 데이터 기반).");
            Assert.That(src, Does.Contain("_fullBodyBearing = bearing;"),
                "[94차 F1] Begin에서 베어링 1회 확정(플레이어→대상 — NPC 응시 회전과 일치, 플레이어 부재 시 targetT.forward).");
            Assert.That(src, Does.Contain("_fullBodyBearing = Vector3.zero;"),
                "[94차 F1] End에서 베어링 클리어(세션 잔존 방지).");
            // [96차 F1 → 100차 F-2] 계약 진화: 1.2m → 0.7m → 0.6m — 측면 오프셋(FullBodySideOffsetM 0.45m)과
            //   조합되어 옆에서 약간 내려다보는 3/4 뷰. 구 1.2m 약상반은 정중앙 배치 시 시선이 플레이어 머리를
            //   관통(영상6 실측). [100차 F-2] 하향 — 하이 앵글 완화로 정면 얼굴 프레임 진입(테스트 11 실측).
            Assert.That(src, Does.Contain("FullBodyCamHeightUpM = 0.6f"),
                "[100차 F-2] 카메라 높이 = max(center.y + 0.6m, 플레이어 y + 1.75m)(구 0.7m 계약 진화 — 하이 앵글 완화).");

            // ② 90° 클립 회전 폐기(fullBody 한정) — 병사/영주 경로(ReframeCamera 비-fullBody 분기) 1곳만 잔존.
            int rotCount = 0, idx = 0;
            while ((idx = src.IndexOf("Quaternion.Euler(0f, 90f, 0f)", idx, System.StringComparison.Ordinal)) >= 0)
            {
                rotCount++;
                idx += 5;
            }
            Assert.That(rotCount, Is.EqualTo(1),
                "[94차 F1] 90° 클립 회전은 병사/영주 경로 1곳만 잔존 — fullBody 경로에서 폐기(방향 이상 요인 제거).");

            // ③ 최종 적용권 이전 — 업데이터 Update → LateUpdate(PlayerMovement 93차 게이트보다 나중 실행).
            Assert.That(src, Does.Contain("private void LateUpdate()"),
                "[94차 F1] 업데이터 틱 LateUpdate 전환(최종 적용권 — 추격 스크립트 덮어쓰기 원천 차단).");
            Assert.That(src, Does.Not.Contain("private void Update()"),
                "[94차 F1] 구 Update 틱 잔존 금지(LateUpdate 이전 완료).");
            Assert.That(src, Does.Contain("PlayerMovement.CameraFollowSuppressed = true;"),
                "[93차 F1] 게이트 코드 유지(이중 안전 — LateUpdate 전환과 병행).");

            // ④ 매 프레임 LookAt 유지 — fullBody 분기가 대상 머리 쪽(center + up×0.15m)을 재조준(대상 이동 추적 계약).
            Assert.That(src, Does.Contain("_dialogueCam.transform.LookAt(center + Vector3.up * FullBodyLookAtUpM);"),
                "[100차 F-2] fullBody 매 프레임 LookAt(center + up×0.15m — 머리 쪽 조준, 대상 추적 유지 — 구 center 직접 조준 계약 진화).");
        }

        /// <summary>
        /// [96차 F1 → 103차 F-3] 소스 어서션 — 대화 카메라 측면 오프셋(완전 정면 폐기) + 시선 높이 단일식. 근거: 마을씬 테스트 6
        /// 포렌식 — 카메라가 NPC 정면축 정중앙(베어링×2.92m 단독)에 서면 시선 y@2.0m≈3.8 &lt; 플레이어 머리 4.3
        /// 관통 → 플레이어 등/머리가 프레임을 채움. 수리 = 카메라 = center + 베어링×2.92m +
        /// perpendicular(bearing의 Y축 90° 회전)×0.9m, 높이 = center.y + 0.7m(1.2→0.7).
        /// </summary>
        [Test]
        public void DialogueCameraDirector_Contract_SideOffset_96()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");
            Assert.That(src, Does.Contain("[96차 F1]"), "[96차 F1] 마커 주석.");

            // ① 상수 — [103차 F-3] 완전 정면: 측면 오프셋 0.0f(구 0.9→0.45→0.0 계약 진화) / 높이 0.6m 단일식.
            Assert.That(src, Does.Contain("FullBodySideOffsetM = 0.0f"),
                "[103차 F-3] 측면 오프셋 0.0f 상수 — 완전 정면(가림 원천은 [103차 F-2] 플레이어 렌더러 숨김으로 제거, 오버숄더 폐기 — 구 0.9/0.45m 계약 진화).");
            Assert.That(src, Does.Contain("FullBodyCamHeightUpM = 0.6f"),
                "[103차 F-3] 카메라 높이 = center.y + 0.6m 단일식(B-2 max 가드 폐기 — 구 0.7m 계약 진화).");
            Assert.That(src, Does.Not.Contain("FullBodyCamHeightUpM = 1.2f"),
                "[96차 F1] 구 높이(1.2m) 잔존 금지 — 0.7m 이행 확인.");

            // ② 기하 합성 — bearing + perpendicular(외적 = Y축 90° 회전) 단일소스 헬퍼.
            Assert.That(src, Does.Contain("private static Vector3 ComputeFullBodyCamPos(Vector3 center, Vector3 bearing, float dist)"),
                "[96차 F1] 합성 헬퍼 단일소스(Begin/ReframeCamera 공용 — 기하 이중화 폐기).");
            Assert.That(src, Does.Contain("Vector3.Cross(Vector3.up, bearing)"),
                "[96차 F1] perpendicular = bearing의 Y축 90° 회전(외적 산출).");
            Assert.That(src, Does.Contain("+ sideDir * FullBodySideOffsetM"),
                "[96차 F1 → 103차 F-3] 기하 합성 코드 — center − 베어링×dist + 측면×FullBodySideOffsetM(0.0 — 곱 구조 유지).");

            // ③ 근거 주석 — 영상6 실측(시선 관통 수치) 유지.
            Assert.That(src, Does.Contain("시선 y@2.0m≈3.8"),
                "[96차 F1] 근거 주석(시선 y@2.0m≈3.8 < 플레이어 머리 4.3 관통 → 플레이어 등이 프레임 채움 — 영상6 실측) 유지.");

            // ④ Begin/ReframeCamera 양 경로가 헬퍼 경유(합성 오프셋 누락 경로 금지).
            int helperDef = src.IndexOf("private static Vector3 ComputeFullBodyCamPos", System.StringComparison.Ordinal);
            int beginCall = src.IndexOf("Vector3 camPos = ComputeFullBodyCamPos(center, bearing, _fullBodyDist);", System.StringComparison.Ordinal);
            Assert.That(helperDef, Is.GreaterThan(0), "합성 헬퍼 정의 존재.");
            Assert.That(beginCall, Is.GreaterThan(0), "Begin fullBody 분기 합성 호출 존재.");
            Assert.That(src.IndexOf("Vector3 camPos = ComputeFullBodyCamPos(center, bearing, effectiveDist);", System.StringComparison.Ordinal),
                Is.GreaterThan(beginCall), "[101차 F-2] ReframeCamera fullBody 분기 합성 호출(2곳 — 거리 가드 재조준 effectiveDist 경유 — 누락 금지).");

            // ⑤ LookAt 유지 — fullBody 분기 LookAt(center + up×FullBodyLookAtUpM) 2곳(Begin+Reframe —
            //    [100차 F-2] 머리 쪽 조준 상향 바이어스, 대상 고정 추적 계약 유지).
            int lookAtCount = 0, idx = 0;
            while ((idx = src.IndexOf("_dialogueCam.transform.LookAt(center + Vector3.up * FullBodyLookAtUpM);", idx, System.StringComparison.Ordinal)) >= 0)
            {
                lookAtCount++;
                idx += 5;
            }
            Assert.That(lookAtCount, Is.EqualTo(2),
                "[100차 F-2] LookAt(center+up×0.15m) 유지(Begin+ReframeCamera 2곳 — 대상 고정 추적 계약 무변경).");
        }

        /// <summary>
        /// [100차 F-2] 소스 어서션 — 대화 카메라 전신프레임 발 기준 + 가림 실측 계약. 근거: 전신프레임 하단
        /// 기준이 bounds minY를 쓰면 스킨바운드 부양(minY 1.85 — 마을씬 테스트 11)으로 발이 프레임 밖으로
        /// 나감. 수리 = ①발 본 최하단 y 실측(TryMeasureFootBottomY — foot/toe 포함 본 중 최저 y, 부재 시
        /// bounds 폴백) ②가림 실측(LogOcclusionCheck — Begin 1회, 카메라→머리/중심/발 3점 Ray 플레이어
        /// 태그 히트 기록 — 진단 전용, 프레이밍 무영향). LookAt(center + up×FullBodyLookAtUpM) 계약 유지.
        /// </summary>
        [Test]
        public void DialogueCameraDirector_FootMeasure_And_OcclusionProbe_100F2()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");

            Assert.That(src, Does.Contain("[100차 F-2]"), "[100차 F-2] 마커 주석.");

            // ① 발 본 최하단 y 실측 — foot/toe 포함 본(대소문자 무시) 중 최저 y, 부재 시 false(bounds 폴백).
            Assert.That(src, Does.Contain("private static bool TryMeasureFootBottomY(Transform target, out float footY)"),
                "[100차 F-2] 발 본 최하단 y 실측 API 존재(스킨바운드 부양 방어 — bounds 폴백 계약).");
            Assert.That(src, Does.Contain("!lower.Contains(\"foot\") && !lower.Contains(\"toe\")"),
                "[100차 F-2] 발 본 필터 — foot/toe 포함(대소문자 무시) 본만 실측.");
            Assert.That(src, Does.Contain("if (t.position.y < footY) { footY = t.position.y; found = true; }"),
                "[100차 F-2] 최저 y 선택 — 좌우 Foot/Toe 중 최하단 본.");

            // ② 전신프레임 하단 기준 교체 — bounds minY → Foot 본(부재 시 bounds 폴백).
            Assert.That(src, Does.Contain("if (TryMeasureFootBottomY(targetT, out float footY))"),
                "[100차 F-2] 전신프레임 하단 기준 — Foot 본 실측 우선(발 본 부재 시 bounds 폴백 계약).");
            Assert.That(src, Does.Contain("bottomRef = \"Foot본\""),
                "[100차 F-2] 하단 기준 로그 — Foot본/bounds폴백 병기(실측 경로 판별).");
            Assert.That(src, Does.Contain("bottomRefY - (b.center.y - halfH)"),
                "[100차 F-2] 하단여유 = 발 기준 − 프러스텀 하단(양수 = 발이 프레임 내).");

            // ③ 가림 실측(Begin 1회) — 머리/중심/발 3점 Ray + 플레이어 태그 히트만 기록.
            Assert.That(src, Does.Contain("LogOcclusionCheck();       // [100차 F-2]"),
                "[100차 F-2] 가림 실측 Begin 1회 호출(다음 판정 데이터 — 진단 전용).");
            Assert.That(src, Does.Contain("private static void LogOcclusionCheck()"),
                "[100차 F-2] 가림 실측 프로브 존재(진단 전용 — 프레이밍 무영향).");
            Assert.That(src, Does.Contain("!hitT.CompareTag(\"Player\") && !hitT.root.CompareTag(\"Player\")"),
                "[100차 F-2] 가림 기록 필터 — 플레이어 태그(본체/루트) 히트만 기록.");
            Assert.That(src, Does.Contain("float footY = targetT.position.y;"),
                "[100차 F-2] 발 프로브 — Foot 본 최하단(부재 시 target.position — 지면 근방) 폴백.");
            Assert.That(src, Does.Contain("[100차 F-2] 가림 — 카메라→NPC 3점 Ray 플레이어 태그 히트"),
                "[100차 F-2] 가림 로그 형식(히트 N/3 + 점별 거리/높이 — 무가림 무로그).");

            // ④ LookAt 머리 쪽 조준 계약 유지 — center + up×FullBodyLookAtUpM(0.15m).
            Assert.That(src, Does.Contain("FullBodyLookAtUpM = 0.15f"),
                "[100차 F-2] LookAt 상향 바이어스 0.15m 상수(머리 쪽 조준 — 양쪽 경로 일관).");
            Assert.That(src, Does.Contain("targetT.position + Vector3.up * FullBodyHeadProbeM"),
                "[100차 F-2] 머리 프로브 — target.position + FullBodyHeadProbeM(캐릭터 키 계약 머리 근방).");
        }

        /// <summary>
        /// [94차 F1 → 96차 F1 → 103차 F-3] 동작 테스트 — fullBody Begin 후 대상이 이동해도 카메라는 고정
        /// 베어링(플레이어→대상 방향) 반전×2.92m(플레이어 측) + 측면 오프셋 0([103차 F-3] 완전 정면 —
        /// FullBodySideOffsetM=0.0) + 높이 = center.y+0.6m 단일식(B-2 가드 폐기)을 유지하고 LookAt만 새
        /// 머리 지점(center+up×0.15m)을 재조준한다(대상 이동 추적). center = [103차 F-1]
        /// ComputeFullBodyCenter 단일소스(internal 직접판정). ReframeCamera는 프라이빗 정적 — 리플렉션 호출.
        /// </summary>
        [Test]
        public void DialogueCameraDirector_FullBody_Reframe_KeepsFixedBearing_TracksTarget_94()
        {
            var camGo = new GameObject("DF94_MainCam");
            var playerGo = new GameObject("DF94_Player");
            GameObject villagerGo = null;
            try
            {
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                camGo.transform.position = new Vector3(10f, 20f, -30f);

                playerGo.tag = "Player";
                playerGo.transform.position = new Vector3(0f, 0f, -5f);

                villagerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                villagerGo.name = "DF94_Villager";
                villagerGo.transform.position = new Vector3(0f, 0.9f, 0f);

                DialogueCameraDirector.MainCameraOverride = () => cam;
                try
                {
                    DialogueCameraDirector.Begin(villagerGo.transform, "주민", fullBody: true);
                    Assert.That(DialogueCameraDirector.IsActive, Is.True, "Begin → 활성.");

                    // Begin 확정 베어링 — 플레이어→대상 방향(테스트 기하 기반 재계산).
                    Vector3 bearing = villagerGo.transform.position - playerGo.transform.position;
                    bearing.y = 0f;
                    bearing.Normalize();
                    float dist = (1.8f * 0.5f) / Mathf.Tan(20f * Mathf.Deg2Rad) * 1.18f;

                    // 대상 이동 — 세션 중 배회/스웨이 시나리오(베어링은 불변, LookAt만 추적).
                    villagerGo.transform.position = new Vector3(2f, 0.9f, 1f);

                    var reframe = typeof(DialogueCameraDirector).GetMethod("ReframeCamera",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    Assert.That(reframe, Is.Not.Null, "ReframeCamera 프라이빗 메서드 존재.");
                    reframe.Invoke(null, null);

                    // [103차 F-1] center 재판정 — ReframeCamera도 ComputeFullBodyCenter 단일소스(Begin 동일).
                    Vector3 center2 = DialogueCameraDirector.ComputeFullBodyCenter(villagerGo.transform);
                    // [96차 F1 → 99차 F-B → 103차 F-3] 계약 진화 — 수평 오프셋 = 고정 베어링×2.92m 반전
                    //   (center − bearing×dist — 플레이어 측, NPC 정면) + 측면(perpendicular)×0.0m,
                    //   높이 = center.y+0.6m 단일식(구 [94차 F1] 단일 베어링 + 1.2m 약상반 → B-2 가드 순차 폐기).
                    Vector3 expectedOffset = -bearing * dist;   // [103차 F-3] FullBodySideOffsetM = 0.0 — 측면 성분 소멸
                    Vector3 camOffset = cam.transform.position - center2; camOffset.y = 0f;
                    Assert.That(Vector3.Distance(camOffset, expectedOffset), Is.LessThan(0.02f),
                        "[103차 F-3] B-1 대상 이동 후에도 수평 오프셋 = center − 베어링×2.92(플레이어 측) + 측면×0.0(완전 정면) 유지.");
                    Assert.That(Vector3.Dot(camOffset.normalized, expectedOffset.normalized), Is.GreaterThan(0.99f),
                        "[96차 F1 → 103차 F-3] 베어링 성분 불변 — 대상 이동과 무관하게 Begin 확정 베어링 고정(측면 0 — 재투영 폐기 검증).");
                    Assert.That(Mathf.Abs(cam.transform.position.y - (center2.y + 0.6f)), Is.LessThan(0.02f),
                        "[103차 F-3] 높이 = center.y + FullBodyCamHeightUpM(0.6m) 단일식 유지(B-2 max 가드 폐기).");
                    Assert.That(Vector3.Dot(cam.transform.forward, (center2 - cam.transform.position).normalized),
                        Is.GreaterThan(0.99f),
                        "[94차 F1] 매 프레임 LookAt — 대상 이동 후 새 center 재조준(대상 추적).");

                    // [96차 F1] 가림 수리 본질 — 시선(연장 포함)이 플레이어 캡슐(반경 0.35m 근사)과 교차 금지.
                    Vector3 sightDir = (center2 - cam.transform.position).normalized;
                    Vector3 toPlayer = playerGo.transform.position - cam.transform.position;
                    float proj = Mathf.Max(Vector3.Dot(toPlayer, sightDir), 0f);
                    Vector3 closestOnSight = cam.transform.position + sightDir * proj;
                    Assert.That(Vector3.Distance(closestOnSight, playerGo.transform.position),
                        Is.GreaterThan(0.35f),
                        "[96차 F1] 시선-플레이어 최근접 ≥ 0.35m — 대상 이동 후에도 가림 없음.");
                }
                finally
                {
                    DialogueCameraDirector.End();   // 멱등 — 잔여 정리(+베어링 클리어)
                    DialogueCameraDirector.MainCameraOverride = null;   // 주입 원복
                }
            }
            finally
            {
                if (villagerGo != null) Object.DestroyImmediate(villagerGo);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(camGo);
            }
        }

        // ── 8. [94차 F4] 상호작용 패널 — F키 개방 즉시 정지/닫힘 재개 + 정면 앵커 ──

        /// <summary>
        /// [94차 F4] 소스 어서션 — 사용자 요구: F키 → ①대상 NPC 이동 정지 ②패널이 NPC 바로 앞 즉시 부착.
        /// 수리 = ①OpenForTarget 개방 시점 주민 배회 정지(VillagerWanderer.PauseForDialogue 재사용 — 멱등)
        /// + 닫힘(Close/대상 클리어) 재개 ②정면 앵커(bounds center + forward×0.6m, 높이 기존 클램프 1.2~2.2m).
        /// </summary>
        [Test]
        public void InteractionPanelUTK_FKeyWanderPause_And_FrontAnchor_94()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");

            Assert.That(src, Does.Contain("[94차 F4]"), "[94차 F4] 마커 주석.");
            // ① 개방 즉시 정지 — 주민=VillagerWanderer.PauseForDialogue 재사용(멱등 — 대화하기 경로와 중복 무해).
            Assert.That(src, Does.Contain("EnsureResumeOnAnyClose();"),
                "[94차 F4 → 96차 F2] 개방 진입 재개 수렴 호출(이전 대상/멱등 — 96차 단일 수렴 개명).");
            Assert.That(src, Does.Contain("PauseTargetWander(target);"), "[94차 F4] OpenForTarget 개방 즉시 정지 호출.");
            Assert.That(src, Does.Contain("wanderer.PauseForDialogue();"), "주민 정지 — VillagerWanderer.PauseForDialogue 재사용.");
            Assert.That(src, Does.Contain("wanderer.ResumeAfterDialogue();"), "주민 재개 — ResumeAfterDialogue.");
            Assert.That(src, Does.Contain("_wanderPausedTarget"), "[94차 F4] 정지 대상 추적(닫힘/대상 변경 재개 데이터).");
            // 닫힘 경로 — Hide 오버라이드(Close/PlacementTick 자동 닫힘 공통 경로)에서 재개.
            int hide = src.IndexOf("public override void Hide()");
            int resumeInHide = hide >= 0 ? src.IndexOf("EnsureResumeOnAnyClose();", hide) : -1;
            Assert.That(hide, Is.GreaterThan(0), "Hide 오버라이드 존재.");
            Assert.That(resumeInHide, Is.GreaterThan(hide),
                "[94차 F4 → 96차 F2] 닫힘(Hide) 경로 재개 수렴 — Close/자동 닫힘 공통.");
            // 대화 소유권 가드 — 대화 카메라 세션 중 재개 위임(대화 중 주민 보호/EndDialoguePose 소유).
            Assert.That(src, Does.Contain("DialogueCameraDirector.Target == paused"), "[94차 F4] 대화 소유권 가드 — 세션 중 재개 위임.");
            // 마을NPC/병사·영주 무간섭 — TerritoryNPCBehaviour는 배회 없음(실측), 병사/영주는 기존 정지 체계.
            Assert.That(src, Does.Contain("배회 로직 없음"), "[94차 F4] 마을NPC 실측 주석(정지 API 불요) 존재.");

            // ② 본체 단일 앵커 — [101차 F-1] bounds 중심 x + Foot 본 y + 1.95m(머리 위 — [94차 F4] 정면앵커 폐기 승계).
            Assert.That(src, Does.Contain("BodyAnchorFootUpM = 1.95f"), "[101차 F-1] 본체 앵커 상승 1.95m 상수([94차 F4] FrontOffset 0.6m 폐기 승계).");
            Assert.That(src, Does.Contain("BodyAnchorBoundsPadM = 0.15f"), "[101차 F-1] Foot 본 부재 폴백 — bounds maxY + 0.15m.");
            Assert.That(src, Does.Contain("TryProjectBodyAnchor"), "[101차 F-1] 본체 단일 앵커 투영 경로([94차 F4] TryProjectFrontAnchor 폐기 승계).");
            // 개방 프레임 즉시 1회 + PlacementTick 유지(기존 계약 — PlaceAtTarget이 Show 선행).
            int placeCall = src.IndexOf("PlaceAtTarget(target);");
            int showCall = placeCall >= 0 ? src.IndexOf("Show();", placeCall) : -1;
            Assert.That(placeCall, Is.GreaterThan(0), "OpenForTarget 개방 프레임 PlaceAtTarget 즉시 1회.");
            Assert.That(showCall, Is.GreaterThan(placeCall), "[94차 F4] PlaceAtTarget이 Show 선행(즉시 부착 계약 유지).");
            Assert.That(src, Does.Contain("schedule.Execute(PlacementTick).Every(PlacePollMs)"), "PlacementTick 추적 유지(기존 계약).");
        }

        // ── 9. [96차 F2] 정지 재개 전수화 — 수렴/재개 로그/15s 안전망/브리지 안전망(봉인 해소) ──

        /// <summary>
        /// [96차 F2] 소스 어서션 — 근거: 마을씬 테스트 6.mp4 + Editor.log(22:26) — `[94차 F4] 패널 개방 즉시 정지`
        /// 2회인데 재개 로그 전무 → Villager_10 PauseForDialogue 봉인(다리/상체 고정). 수리 = ①단일 수렴 메서드
        /// EnsureResumeOnAnyClose(Hide/PlacementTick 자동 닫힘/DetachFromPanel/개방 진입 전 경로 수렴) ②재개 로그
        /// 신설 ③15s 안전망(패널 측 PlacementTick+스케줄 감시) ④브리지 안전망(PlayerMovement — 개방 이벤트 유실 대비).
        /// 대화 소유권 가드(94차) 유지 — 대화 중 강제 재개 부작용 금지.
        /// </summary>
        [Test]
        public void InteractionPanelUTK_ResumeConvergence_SafetyNet_96()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");

            Assert.That(src, Does.Contain("[96차 F2]"), "[96차 F2] 마커 주석.");
            // ① 단일 수렴 메서드 + 재개 로그(스펙 수리 1/2).
            Assert.That(src, Does.Contain("private void EnsureResumeOnAnyClose()"),
                "[96차 F2] 단일 수렴 메서드 존재(Hide/PlacementTick/Detach/개방 진입 수렴).");
            Assert.That(src, Does.Contain("[94차 F4] 패널 닫힘 — 주민"),
                "[96차 F2] 재개 로그 태그/형식 — 정지 로그와 쌍 대조 가능.");
            Assert.That(src, Does.Contain("배회 재개 (ResumeAfterDialogue)"),
                "[96차 F2] 재개 로그 문구('배회 재개' — 스펙 지정 형식).");
            // 수렴 경로 — 개방 진입/Hide/PlacementTick(개방 플래그 소실+대상 소멸+사망)/DetachFromPanel.
            int convergeCount = 0, idx = 0;
            while ((idx = src.IndexOf("EnsureResumeOnAnyClose();", idx, System.StringComparison.Ordinal)) >= 0)
            {
                convergeCount++;
                idx += 5;
            }
            Assert.That(convergeCount, Is.GreaterThanOrEqualTo(5),
                "[96차 F2] 수렴 호출 5곳 이상(개방 진입/Hide/PlacementTick×3/DetachFromPanel).");
            Assert.That(src, Does.Contain("RegisterCallback<DetachFromPanelEvent>(_ => EnsureResumeOnAnyClose());"),
                "[96차 F2] 부모 DetachFromPanel 경로 수렴(Hide 미경유 닫힘 커버).");
            // ② 안전망 — 15s 초과 자동 재개+경고(스펙 수리 3).
            Assert.That(src, Does.Contain("PauseWatchdogTimeoutSeconds = 15f"), "[96차 F2] 안전망 15s 상수.");
            Assert.That(src, Does.Contain("private static void PauseWatchdogCheck()"),
                "[96차 F2] 안전망 판정 메서드(정적 타이머).");
            Assert.That(src, Does.Contain("ArmPauseWatchdog(villager);"),
                "[96차 F2] 정지 시 안전망 가동.");
            Assert.That(src, Does.Contain("PauseWatchdogCheck();"),
                "[96차 F2] 패널 측 PlacementTick 감시 경로.");
            Assert.That(src, Does.Contain("안전망 — 정지"),
                "[96차 F2] 안전망 경고 로그(누락 닫힘 경로 회복 추적).");
            // ③ 대화 소유권 가드 유지(94차) — 수렴/안전망 모두 가드(대화 중 재개 부작용 금지).
            Assert.That(src, Does.Contain("DialogueCameraDirector.IsActive && DialogueCameraDirector.Target == paused"),
                "[94차 F4 → 96차 F2] 대화 소유권 가드 유지(수렴+안전망 공통).");
            // ④ 브리지 안전망(PlayerMovement — 화이트리스트 보강).
            string pm = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerMovement.cs");
            Assert.That(pm, Does.Contain("[96차 F2]"), "[96차 F2] 브리지 마커 주석.");
            Assert.That(pm, Does.Contain("BridgePendingTimeoutSeconds = 15f"),
                "[96차 F2] 브리지 안전망 15s(패널 개방 유실 대비).");
            Assert.That(pm, Does.Contain("BridgePendingBegin(nearestVillager);"),
                "[96차 F2] 정지 전달 후 브리지 기록.");
            Assert.That(pm, Does.Contain("BridgePendingConfirm(nearestVillager);"),
                "[96차 F2] 패널 소유 확인 — 소유 시 안전망 해제(이중 강제 재개 방지).");
            Assert.That(pm, Does.Contain("TickBridgePendingWatchdog();"),
                "[96차 F2] Update 감시 경로.");
            Assert.That(pm, Does.Contain("DialogueCameraDirector.IsActive"),
                "[96차 F2] 브리지 안전망 대화 가드 정합.");
            // ⑤ UI 측 소유 확인 공용 API — 브리지 리플렉션 소비 계약.
            Assert.That(src, Does.Contain("public static bool IsHoldingPause(object target)"),
                "[96차 F2] 패널 소유 확인 공용 API(Systems 리플렉션 게이트).");
        }

        /// <summary>
        /// [96차 F2] 로직 테스트 — PlacementTick 자동 닫힘 경로(대상 클리어) → 재개 실측. 근거: 스펙 원인 후보 ①
        /// "패널 닫힘 경로가 Hide() 오버라이드를 안 탐(PlacementTick 자동 닫힘/부모 detach)". IsOpen 플래그+
        /// 대상 클리어를 리플렉션으로 시뮬레이션(94차 로직 테스트 선례 — UIRoot 의존 개방 전체 회피) 후
        /// PlacementTick 호출 → 수렴 재개+Close→Hide → _paused=false 실측.
        /// </summary>
        [Test]
        public void InteractionPanelUTK_PlacementTickAutoClose_Resumes_96()
        {
            var villagerGo = new GameObject("F96_Villager_AutoClose");
            try
            {
                var npc = villagerGo.AddComponent<NPCAmbientDialogue>();   // EditMode — Awake 미호출 규약
                var wanderer = villagerGo.AddComponent<VillagerWanderer>();
                typeof(VillagerWanderer).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(wanderer, new System.Random(11));   // 컴포넌트 주입 — IdleDuration 요구(94차 선례)

                InteractionPanelUTK.Ensure();
                var panel = InteractionPanelUTK.Instance;
                Assert.That(panel, Is.Not.Null, "패널 싱글턴 확보.");
                typeof(InteractionPanelUTK).GetMethod("PauseTargetWander", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(panel, new object[] { npc });   // 개방 정지 경로 시뮬레이션(94차 선례).
                Assert.That(GetPrivateField<bool>(wanderer, "_paused"), Is.True, "[96차 F2] 개방 정지 실측.");
                Assert.That(InteractionPanelUTK.IsHoldingPause(npc), Is.True,
                    "[96차 F2] 패널 소유 확인 API 실측(IsHoldingPause — 브리지 게이트).");

                // 개방 상태 + 대상 클리어 시뮬레이션 — PlacementTick 자동 닫힘 조건(tracked == null).
                typeof(UTKWindowBase).GetField("_isOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(panel, true);
                SetPrivateField(panel, "_trackedTarget", null);

                typeof(InteractionPanelUTK).GetMethod("PlacementTick", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(panel, null);   // 자동 닫힘 경로 — 수렴 재개 + Close→Hide.

                Assert.That(panel.IsOpen, Is.False, "[96차 F2] 자동 닫힘 실측(대상 클리어 → Close).");
                Assert.That(GetPrivateField<bool>(wanderer, "_paused"), Is.False,
                    "[96차 F2] 자동 닫힘 경로 재개 실측 — Hide 오버라이드 외 경로에서도 ResumeAfterDialogue.");
                Assert.That(InteractionPanelUTK.IsHoldingPause(npc), Is.False,
                    "[96차 F2] 수렴 후 소유 해제(안전망 이중 강제 재개 방지).");
            }
            finally
            {
                Object.DestroyImmediate(villagerGo);
            }
        }

        /// <summary>
        /// [96차 F2] 로직 테스트 — 대화 소유권 가드 정합(94차 유지): 대화 카메라 세션(DialogueCameraDirector
        /// _active/_target 정적 주입) 중 닫힘 수렴 시 재개 금지(위임) — 대화 중 주민이 움직이는 부작용 방지.
        /// 위임 후 수렴은 무재개(소유 클리어 — 대화 종료 경로 전담) 확인.
        /// </summary>
        [Test]
        public void InteractionPanelUTK_DialogueGuard_DefersResume_96()
        {
            var villagerGo = new GameObject("F96_Villager_Guard");
            try
            {
                var npc = villagerGo.AddComponent<NPCAmbientDialogue>();
                var wanderer = villagerGo.AddComponent<VillagerWanderer>();
                typeof(VillagerWanderer).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(wanderer, new System.Random(13));

                InteractionPanelUTK.Ensure();
                var panel = InteractionPanelUTK.Instance;
                typeof(InteractionPanelUTK).GetMethod("PauseTargetWander", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(panel, new object[] { npc });

                // 대화 카메라 세션 흉내 — DialogueCameraDirector 정적 상태 주입([93차 F1] 프라이빗 주입 선례).
                var directorType = typeof(DialogueCameraDirector);
                directorType.GetField("_active", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
                directorType.GetField("_target", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, npc);
                try
                {
                    typeof(InteractionPanelUTK).GetMethod("EnsureResumeOnAnyClose", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(panel, null);   // 닫힘 수렴 — 가드 활성.
                    Assert.That(GetPrivateField<bool>(wanderer, "_paused"), Is.True,
                        "[96차 F2] 대화 소유권 가드 — 세션 중 재개 금지(EndDialoguePose 위임).");
                    Assert.That(InteractionPanelUTK.IsHoldingPause(npc), Is.False,
                        "[96차 F2] 위임 시 패널 소유 해제(안전망이 대화 중 강제 재개하지 않는 전제).");
                }
                finally
                {
                    directorType.GetField("_active", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, false);
                    directorType.GetField("_target", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
                }

                // 세션 해제 후 재수렴 — 위임 건은 대화 종료 경로(EndDialoguePose)가 재개하므로 무재개(멱등) 확인.
                typeof(InteractionPanelUTK).GetMethod("EnsureResumeOnAnyClose", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(panel, null);
                Assert.That(GetPrivateField<bool>(wanderer, "_paused"), Is.True,
                    "[96차 F2] 위임 후 수렴은 무재개(소유 클리어 — 대화 종료 경로 전담 계약).");
                wanderer.ResumeAfterDialogue();   // 정리 — 대화 종료 재개 시뮬레이션(테스트 간 오염 방지).
            }
            finally
            {
                Object.DestroyImmediate(villagerGo);
            }
        }

        /// <summary>
        /// [96차 F2] 로직 테스트 — 15s 안전망 실측: 패널 소유 정지에서 감시 개시 시각을 16s 과거로 주입 →
        /// PauseWatchdogCheck 호출 → 자동 재개 실측(누락 닫힘 경로 회복 — 스펙 수리 3). 대화 세션 부재 전제.
        /// </summary>
        [Test]
        public void InteractionPanelUTK_Watchdog15s_AutoResumes_96()
        {
            var villagerGo = new GameObject("F96_Villager_Watchdog");
            try
            {
                var npc = villagerGo.AddComponent<NPCAmbientDialogue>();
                var wanderer = villagerGo.AddComponent<VillagerWanderer>();
                typeof(VillagerWanderer).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(wanderer, new System.Random(17));

                InteractionPanelUTK.Ensure();
                var panel = InteractionPanelUTK.Instance;
                typeof(InteractionPanelUTK).GetMethod("PauseTargetWander", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(panel, new object[] { npc });
                Assert.That(GetPrivateField<bool>(wanderer, "_paused"), Is.True, "[96차 F2] 안전망 전제 — 정지 실측.");

                typeof(InteractionPanelUTK).GetField("_watchStartedRealtime", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.SetValue(null, Time.realtimeSinceStartup - 16f);   // 15s 경과 시뮬레이션.
                typeof(InteractionPanelUTK).GetMethod("PauseWatchdogCheck", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(panel, null);

                Assert.That(GetPrivateField<bool>(wanderer, "_paused"), Is.False,
                    "[96차 F2] 안전망 — 15s 초과 자동 재개 실측(ResumeAfterDialogue).");
                Assert.That(InteractionPanelUTK.IsHoldingPause(npc), Is.False,
                    "[96차 F2] 안전망 발동 후 소유 해제.");
            }
            finally
            {
                Object.DestroyImmediate(villagerGo);
            }
        }

        // ── 10. [97차 F2] 대화 카메라 수명 연동 — fullBody(주민)=패널 닫힘 폴링, 병사/영주=기존 계약 ──

        /// <summary>
        /// [97차 F2] 소스 어서션 — 근거: 사용자 지시 "카메라는 대화가 끝날 때까지 NPC를 비춰라".
        /// 5s 타이머/이동키 종료가 대화창이 열려 있는데 복원시키는 결함(97차 결함 2) 수리 = 종료 조건이
        /// fullBody 플래그로 분기: ①fullBody(주민)=LateUpdate 틱에서 NPCDialoguePanelUTK 패널 닫힘 폴링
        /// (열려 있지 않으면 End) — 5s 타이머 폐기+이동키 종료 폐기(패널이 입력 게이트), ESC만 유지
        /// ②병사/영주(fullBody=false)=기존 계약 무변경(5s 타이머+ESC/이동키 — 73차).
        /// 패널 IsOpen 접근자는 UTKWindowBase 계승(public bool IsOpen — 최소 추가 불요) 사용.
        /// </summary>
        [Test]
        public void DialogueCameraDirector_Contract_FullBodyPanelLifetime_97F2()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");
            Assert.That(src, Does.Contain("[97차 F2]"), "[97차 F2] 마커 주석.");

            // ① 패널 개방 실측 단일소스 — 주입 지점(Director) + 공급 등록(NPCDialoguePanelUTK — IsOpen 사용).
            Assert.That(src, Does.Contain("public static System.Func<bool> DialoguePanelOpenOverride"),
                "[97차 F2] 패널 개방 실측 주입 지점(MainCameraOverride 선례 — EditMode 시뮬레이션).");
            Assert.That(src, Does.Contain("private static bool IsDialoguePanelOpen()"),
                "[97차 F2] 패널 개방 실측 단일소스 헬퍼.");
            var panelSrc = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs");
            Assert.That(panelSrc, Does.Contain("DialogueCameraDirector.DialoguePanelOpenOverride ="),
                "[97차 F2] NPCDialoguePanelUTK — 개방 실측 위임 등록(Systems 역참조 없이 UI 측 공급).");
            Assert.That(panelSrc, Does.Contain("_instance != null && _instance.IsOpen"),
                "[97차 F2] NPCDialoguePanelUTK.IsOpen 접근자 사용(UTKWindowBase 계승 — 최소 추가 불요).");

            // ② LateUpdate 틱 종료 조건 — [105차 B] 단일화: 패널 닫힘 폴링(전 대상 무조건 — 5s 타이머 폐기).
            int late = src.IndexOf("private void LateUpdate()");
            Assert.That(late, Is.GreaterThan(0), "LateUpdate 틱 존재(94차 계약).");
            int poll = src.IndexOf("if (!IsDialoguePanelOpen())", late);
            Assert.That(poll, Is.GreaterThan(late),
                "[105차 B] 패널 닫힘 폴링 — fullBody 분기 이전, 전 대상 무조건(열려 있지 않으면 End).");
            int pollEnd = src.IndexOf("End();", poll);
            Assert.That(pollEnd, Is.GreaterThan(poll).And.LessThan(poll + 200),
                "[97차 F2] 폴링 실패 → End(패널 수명 = 세션 수명).");
            Assert.That(src.IndexOf("_remaining -= Time.deltaTime", late), Is.EqualTo(-1),
                "[105차 B] 5s 타이머 폐기 — _remaining 잔존 없음(패널 폴링 단일화).");

            // ③ 이동키 종료 폐기(fullBody 한정) — ESC 뒤 `if (_fullBody) return;` 게이트, 이동키 검사는 그 뒤.
            int esc = src.IndexOf("kb.escapeKey.wasPressedThisFrame", late);
            int gate = src.IndexOf("if (_fullBody) return;", esc);
            int wasd = src.IndexOf("kb.wKey.wasPressedThisFrame", gate);
            Assert.That(esc, Is.GreaterThan(0), "[91차 F5] ESC 종료 유지([97차 F2] 계약).");
            Assert.That(gate, Is.GreaterThan(esc), "[97차 F2] ESC 이후 fullBody 이동키 게이트 존재.");
            Assert.That(wasd, Is.GreaterThan(gate),
                "[97차 F2] 이동키 종료는 게이트 뒤(병사/영주만) — 주민 경로 폐기(대화창=입력 게이트).");

            // ④ 기존 계약 보존 — 대상 파괴 감시/게이트/원복 유지(96차 무손상).
            Assert.That(src, Does.Contain("IsDead"), "[97차 F2] 대상 사망 감시 유지(병사 경로).");
            Assert.That(src, Does.Contain("PlayerMovement.CameraFollowSuppressed = true;"),
                "[93차 F1] 게이트 유지(96차 계약 무손상).");
        }

        /// <summary>
        /// [97차 F2] 로직 테스트 — fullBody 세션 수명 = 패널 수명 실측(DialoguePanelOpenOverride 주입).
        /// ①패널 열림 유지 → 틱 경과에도 세션 유지(미종료) ②패널 닫힘 → 다음 틱 End(DialogueEnded 1회).
        /// LateUpdate는 프라이빗 인스턴스 — _updater 리플렉션 획득 후 호출(93차 프라이빗 주입 선례).
        /// </summary>
        [Test]
        public void DialogueCameraDirector_FullBody_PanelCloseEnds_PanelOpenKeeps_97F2()
        {
            var camGo = new GameObject("DF97_MainCam");
            GameObject villagerGo = null;
            try
            {
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                camGo.transform.position = new Vector3(10f, 20f, -30f);

                villagerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                villagerGo.name = "DF97_Villager";
                villagerGo.transform.position = new Vector3(0f, 0.9f, 0f);

                DialogueCameraDirector.MainCameraOverride = () => cam;
                bool panelOpen = true;
                DialogueCameraDirector.DialoguePanelOpenOverride = () => panelOpen;
                try
                {
                    DialogueCameraDirector.Begin(villagerGo.transform, "주민", fullBody: true);
                    Assert.That(DialogueCameraDirector.IsActive, Is.True, "[97차 F2] fullBody Begin → 활성.");

                    var directorType = typeof(DialogueCameraDirector);
                    var updater = directorType.GetField("_updater",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null)
                        as DialogueCameraDirector;
                    Assert.That(updater, Is.Not.Null, "[97차 F2] 업데이터 확보(Begin → Ensure).");
                    var tick = directorType.GetMethod("LateUpdate",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.That(tick, Is.Not.Null, "LateUpdate 틱 리플렉션 획득.");

                    // ① 패널 열림 유지 — 틱 경과에도 세션 유지(타이머 폐기·이동키 폐기 — 패널이 수명 소스).
                    tick.Invoke(updater, null);
                    tick.Invoke(updater, null);
                    Assert.That(DialogueCameraDirector.IsActive, Is.True,
                        "[97차 F2] 패널 열림 유지 → 틱 경과에도 미종료(5s 타이머 폐기 실측).");

                    // ② 패널 닫힘 — 다음 틱 End(DialogueEnded 1회).
                    int ended = 0;
                    System.Action<Component> onEnd = _ => ended++;
                    DialogueCameraDirector.DialogueEnded += onEnd;
                    panelOpen = false;
                    try
                    {
                        tick.Invoke(updater, null);
                        Assert.That(DialogueCameraDirector.IsActive, Is.False,
                            "[97차 F2] 패널 닫힘 → 다음 틱 End(세션 수명 = 패널 수명 연동 실측).");
                        Assert.That(ended, Is.EqualTo(1), "[97차 F2] DialogueEnded 1회 발화(재개 위임 트리거).");
                    }
                    finally
                    {
                        DialogueCameraDirector.DialogueEnded -= onEnd;
                    }
                }
                finally
                {
                    DialogueCameraDirector.End();   // 멱등 — 잔여 정리
                    DialogueCameraDirector.MainCameraOverride = null;
                    DialogueCameraDirector.DialoguePanelOpenOverride = null;
                }
            }
            finally
            {
                if (villagerGo != null) Object.DestroyImmediate(villagerGo);
                Object.DestroyImmediate(camGo);
            }
        }

        // ── [97차 F3] F키 프롬프트 즉시 부착 보강 — 다음 프레임 재배치 1회 + 부착 검증 로그 ──

        /// <summary>
        /// [97차 F3] 소스 어서션 — F키 상호작용 프롬프트 NPC 바로 앞 즉시 부착 보강(스펙 결함 3). 근거: 영상8에서
        /// 개방 첫 프레임 레이아웃 미확정으로 배치가 어긋나(중앙 폴백처럼) 보이던 순간. 수리 = ①개방 프레임 즉시
        /// 배치(기존 [94차 F4]) + 다음 프레임 재배치 1회(스케줄러 — 레이아웃 확정 후 보정) ②부착 검증 로그
        /// (배치 시 패널 worldBound + NPC screenPx 동시 즉격). 기존 계약 유지: 정면 앵커 FrontOffset 0.6m/
        /// 클램프 1.2~2.2m/PlacementTick/정지-재개 수렴(96차 F2).
        /// </summary>
        [Test]
        public void InteractionPanelUTK_NextFrameReplacement_And_PlacementVerify_97F3()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs");

            Assert.That(src, Does.Contain("[97차 F3]"), "[97차 F3] 마커 주석.");

            // ① 다음 프레임 재배치 1회 — 스케줄러 단발(레이아웃 확정 후 보정).
            Assert.That(src, Does.Contain("private void ScheduleNextFrameReplacement()"),
                "[97차 F3] 다음 프레임 재배치 스케줄러 존재.");
            Assert.That(src, Does.Contain("schedule.Execute(NextFrameReplacementTick).StartingIn(16)"),
                "[97차 F3] 스케줄러 1회(다음 프레임 — StartingIn(16) 단발, SleepPanelUTK CenterOnParent 선례).");
            Assert.That(src, Does.Contain("private void NextFrameReplacementTick()"),
                "[97차 F3] 재배치 틱 존재.");
            Assert.That(src, Does.Contain("if (!IsOpen || _trackedTarget == null)"),
                "[97차 F3] 닫힘/대상 소실 무배치 가드(멱등 — 스케줄 스팸 방지).");
            Assert.That(src, Does.Contain("PlaceAtTarget(_trackedTarget);   // [97차 F3]"),
                "[97차 F3] 재배치 틱이 PlaceAtTarget 재호출(부착 검증 로그 동반).");
            // 개방 프레임 즉시 배치(기존 계약) 유지 + 재배치 스케줄이 그 뒤(순서 계약).
            int place = src.IndexOf("PlaceAtTarget(target);   // [72차] 대상 바로 앞 배치");
            int scheduleCall = src.IndexOf("ScheduleNextFrameReplacement();");
            Assert.That(place, Is.GreaterThan(0), "[94차 F4] 개방 프레임 즉시 배치 유지(기존 계약).");
            Assert.That(scheduleCall, Is.GreaterThan(place),
                "[97차 F3] 다음 프레임 재배치가 개방 프레임 배치 뒤에 스케줄(보정 1회).");

            // ② 부착 검증 로그 — 패널 worldBound + NPC screenPx 동시 즉격(개방 순간+다음 프레임 2회).
            Assert.That(src, Does.Contain("private void LogPlacementVerify(Vector2? screenPx, string path, string reason = null)"),
                "[97차 F3] 부착 검증 로그 메서드 존재(폴백 경로 null screenPx 계약 — [99차 F-A] A-3 사유 병기 reason 기본 null 확장).");
            Assert.That(src, Does.Contain("부착 검증 — 경로="),
                "[97차 F3] 부착 검증 로그 형식 — 경로 필드(정면앵커/폴백 판별).");
            Assert.That(src, Does.Contain("npc screenPx="),
                "[97차 F3] 부착 검증 로그 형식 — NPC screenPx 필드.");
            Assert.That(src, Does.Contain("panel worldBound=("),
                "[97차 F3] 부착 검증 로그 형식 — 패널 worldBound 필드.");
            Assert.That(src, Does.Contain("LogPlacementVerify(screen, \"본체앵커\", tracked);"),
                "[97차 F3 → 101차 F-1] 본체 앵커 배치 경로 부착 검증 호출([94차 F4] 정면앵커 폐기 승계 — 본체 실측 대조 인수 병기).");
            Assert.That(src, Does.Contain("LogPlacementVerify(null, \"폴백\");"),
                "[97차 F3] 폴백(투영 실패 z<0) 경로 부착 검증 호출.");
            // 재배치 틱 경유 — 다음 프레임 로그가 개방 프레임 로그와 대조 가능(레이아웃 전후).
            int verifyIdx = src.IndexOf("private void LogPlacementVerify");
            int tickIdx = src.IndexOf("private void NextFrameReplacementTick()");
            Assert.That(verifyIdx, Is.GreaterThan(0), "부착 검증 메서드 존재.");
            Assert.That(tickIdx, Is.GreaterThan(0), "재배치 틱 존재.");

            // ③ 기존 계약 무손상 — 본체 앵커 1.95m([94차 F4] 정면앵커 0.6m/클램프 폐기 승계)/PlacementTick/정지-재개 수렴(96차).
            Assert.That(src, Does.Contain("BodyAnchorFootUpM = 1.95f"), "[101차 F-1] 본체 앵커 상승 1.95m 유지([94차 F4] 정면 오프셋 0.6m 폐기 승계).");
            Assert.That(src, Does.Contain("_placeTask = schedule.Execute(PlacementTick).Every(PlacePollMs);"),
                "[72차] PlacementTick 1s 폴링 유지(개방 중 이동 추적 계약).");
            Assert.That(src, Does.Contain("EnsureResumeOnAnyClose"), "[96차 F2] 닫힘 전 경로 수렴(정지-재개) 유지.");
            Assert.That(src, Does.Contain("ArmPauseWatchdog(villager);"), "[96차 F2] 정지 안전망 유지.");
        }

        /// <summary>
        /// [98차 F2] F2-1 UI 측 폴링 폴백 실측 — DialoguePanelOpenOverride가 IsOpen=false + UI 실측 표시 중
        /// (parent 부착 + display=Flex) 상태에서 true 반환. 근거: 마을씬 테스트 9 — 패널은 OpenForVillager로
        /// 실제 개방·표시 중인데 IsOpen 플래그 폴링과 불일치하여 카메라만 조기 End(36s 실측). 수리 =
        /// NPCDialoguePanelUTK.EnsureAttached 등록 위임 람다 내부 실측표시 폴백(parent 부착 +
        /// resolvedStyle.display != DisplayStyle.None). DialogueFeedbackTests override 시뮬레이션 패턴
        /// ([97차 F2] 1130행 선례) 준용 — 종료 시 override/diagnostics=null 복원. _instance는 private —
        /// 리플렉션 치환([97차 F2] _updater / [93차 F1] 프라이빗 주입 선례).
        /// </summary>
        [Test]
        public void DialogueCameraDirector_DialoguePanelOpenOverride_DisplayFlexFallback_ReturnsOpen_98F2F1()
        {
            var panelType = typeof(NPCDialoguePanelUTK);
            var instanceField = panelType.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var ensureAttached = panelType.GetMethod("EnsureAttached", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(instanceField, Is.Not.Null, "[98차 F2] F2-1 _instance 필드 확보(리플렉션).");
            Assert.That(ensureAttached, Is.Not.Null, "[98차 F2] F2-1 EnsureAttached 등록 경로 확보(리플렉션).");

            var previousInstance = instanceField.GetValue(null) as NPCDialoguePanelUTK;
            try
            {
                // ① 프로덕션 등록 경로 — EnsureAttached(오버라이드/진단 위임 등록 + _instance 생성;
                //   EditMode에서 UIRoot 부재 → 부착만 생략하고 등록은 성립).
                ensureAttached.Invoke(null, null);
                var ensureInstance = NPCDialoguePanelUTK.Instance;   // 정리용 — UIRoot 존재 시 부착된 본체
                Assert.That(DialogueCameraDirector.DialoguePanelOpenOverride, Is.Not.Null,
                    "[98차 F2] F2-1 EnsureAttached → DialoguePanelOpenOverride 등록 실측.");

                // 본체 치환 — 신선 패널(기본 IsOpen=false)로 _instance 교체(플래그/UI 불일치 시나리오 재현).
                var fresh = (NPCDialoguePanelUTK)System.Activator.CreateInstance(panelType, nonPublic: true);
                Assert.That(fresh.IsOpen, Is.False,
                    "[98차 F2] F2-1 사전조건 — 신선 패널 IsOpen=false(플래그 폴링 불일치 시나리오).");
                instanceField.SetValue(null, fresh);

                // ② 부정 통제 — 미부착(parent null) → 실측표시 폴백 미인정(열림 아님).
                Assert.That(DialogueCameraDirector.DialoguePanelOpenOverride(), Is.False,
                    "[98차 F2] F2-1 미부착 패널 = 열림 판정 불가(폴백 무효 실측 — 무조건 true 금지).");

                // ③ 개방 실측 시나리오 — IsOpen=false + parent 부착 + display=Flex → 열림 판정 true.
                var detachedRoot = new VisualElement { name = "DF98F2_Root" };
                detachedRoot.Add(fresh);
                fresh.style.display = DisplayStyle.Flex;
                Assert.That(fresh.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None),
                    "[98차 F2] F2-1 사전조건 — 실측표시(display != None) 성립.");
                Assert.That(DialogueCameraDirector.DialoguePanelOpenOverride(), Is.True,
                    "[98차 F2] F2-1 IsOpen=false + parent 부착 + display=Flex → 열림 판정 true(조기 End 방어 실측).");
            }
            finally
            {
                // [97차 F2] 선례 — 종료 시 오버라이드/진단 null 복원(+ _instance 원복, EnsureAttached 잔여 본체 정리).
                DialogueCameraDirector.DialoguePanelOpenOverride = null;
                DialogueCameraDirector.DialoguePanelOpenDiagnostics = null;
                var attached = NPCDialoguePanelUTK.Instance;
                instanceField.SetValue(null, previousInstance);
                if (attached != null && !ReferenceEquals(attached, previousInstance))
                    attached.RemoveFromHierarchy();
            }
        }

        /// <summary>
        /// [F-2' → 101차 F-2 → 102차 후속] 대화 카메라 거리 가드(Distance Guard) + bounds 반경 계약 — 소스 문자열 어서션.
        /// [102차 후속] 구 리플렉션 테스트(new DialogueCameraDirector + guardDist/effectiveDist 필드 주입)는 폐기된
        /// 구현 형상을 미러링 — qa102_isolated3.log 533-538행: MonoBehaviour 'new' 생성 경고 후 1454행
        /// Component.get_transform NRE. 실제 구현은 guardDist/effectiveDist가 인스턴스 필드가 아니라
        /// ReframeCamera(정적)의 지역변수([101차 F-2] Distance Guard)이고, 프로덕션 생성 경로는
        /// AddComponent(DialogueCameraDirector.cs 397행 선례 — 본 파일 헤더 AddComponent 규약 동일).
        /// [101차 F-2] 계약: 기본 2.92m → NPC bounds 반경 실측(extents.magnitude) → 가드 = max(기본, 반경×2+0.5m)
        /// → effectiveDist = max(Begin 확정 _fullBodyDist, 가드 — 가드 우선) → ComputeFullBodyCamPos 실배치 반영.
        /// </summary>
        [Test]
        public void DialogueCameraDirector_Contract_GuardDist_And_BoundsCalc_F2()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DialogueCameraDirector.cs");

            Assert.That(src, Does.Contain("[101차 F-2] Distance Guard"),
                "[101차 F-2] Distance Guard 마커 주석(모델 침투 방지 가드).");

            // 가드 하한 — 기본 2.92m(전신 프레임 계약 dist).
            int baseDist = src.IndexOf("float guardDist = 2.92f;", System.StringComparison.Ordinal);
            Assert.That(baseDist, Is.GreaterThan(0),
                "[101차 F-2] 가드 기본 2.92m(전신 프레임 dist — 하한).");

            // NPC bounds 반경 실측 — bounds 계산 근거(extents.magnitude).
            int radius = src.IndexOf("float radius = rend.bounds.extents.magnitude;", System.StringComparison.Ordinal);
            Assert.That(radius, Is.GreaterThan(baseDist),
                "[101차 F-2] NPC bounds 반경 실측(rend.bounds.extents.magnitude — 가드 산 입력).");

            // 가드 산식 — max(기본 2.92m, 반경×2+0.5m) — 모델 침투 방어.
            int guardMax = src.IndexOf("guardDist = Mathf.Max(guardDist, radius * 2f + 0.5f);", System.StringComparison.Ordinal);
            Assert.That(guardMax, Is.GreaterThan(radius),
                "[101차 F-2] 가드 = max(2.92m 기본, NPC bounds 반경×2+0.5m) — 모델 침투 방어 산식.");

            // 가드 우선 — Begin 확정 dist(_fullBodyDist)와 가드 중 큰 값.
            int effective = src.IndexOf("float effectiveDist = Mathf.Max(_fullBodyDist, guardDist);", System.StringComparison.Ordinal);
            Assert.That(effective, Is.GreaterThan(guardMax),
                "[101차 F-2] effectiveDist = max(Begin 확정 dist, 가드) — 가드가 크면 가드 우선.");

            // 가드 dist 실배치 반영 — ComputeFullBodyCamPos 인자로 전달.
            int camPos = src.IndexOf("ComputeFullBodyCamPos(center, bearing, effectiveDist)", System.StringComparison.Ordinal);
            Assert.That(camPos, Is.GreaterThan(effective),
                "[101차 F-2] 가드 dist가 카메라 배치(ComputeFullBodyCamPos)에 실반영.");

            // 가드 산식 단일소스 핀 — ReframeCamera 지역변수 1곳(필드 부활/중복 산식 금지).
            Assert.That(src.IndexOf("float guardDist = 2.92f;", baseDist + 1, System.StringComparison.Ordinal),
                Is.EqualTo(-1), "[101차 F-2] 가드 산식 단일소스(ReframeCamera 지역변수 1곳 — 필드 부활/중복 금지).");
        }

        // ── 헬퍼 ──

        /// <summary>[151차 P3] 농경 상태 메시지 → 대화판넬 라우팅 소스계약 — Systems→UI 이벤트 브리지
        ///   (FarmPlot.FarmStatusBroadcast 발화 → NPCDialoguePanelUTK.ShowTransientMessage 구독·표출·자동 닫힘).</summary>
        [Test]
        public void FarmStatus_PanelRouting_SourceContract_151()
        {
            // 1) FarmPlot — 브로드캐스트 이벤트 신설 + ShowStatus 발화 연결.
            string farm = System.IO.File.ReadAllText(
                "Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(farm, Does.Contain("public static event System.Action<string> FarmStatusBroadcast"),
                "[151차 P3] FarmPlot 브로드캐스트 이벤트 신설(Systems→UI 단방향 계약).");
            Assert.That(farm, Does.Contain("FarmStatusBroadcast?.Invoke(message);"),
                "[151차 P3] ShowStatus가 브로드캐스트 발화 — 문구 문자열 무변경(15건 전수 유지).");

            // 2) NPCDialoguePanelUTK — 트랜지언트 표출 + 구독 부트 + 자동 닫힘.
            string panel = System.IO.File.ReadAllText(
                "Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs");
            Assert.That(panel, Does.Contain("public static void ShowTransientMessage(string message)"),
                "[151차 P3] 대화판넬 트랜지언트 표출 진입점.");
            Assert.That(panel, Does.Contain("FarmPlot.FarmStatusBroadcast += ShowTransientMessage"),
                "[151차 P3] EnsureAttached 구독 부트(UI→Systems 단방향 — 역참조 금지 계약 준용).");
            Assert.That(panel, Does.Contain("_transientUntil = Time.time + 3.5f"),
                "[151차 P3] 3.5s 자동 닫힘 — FarmPlot _statusUntil 창 계약 승계.");
            Assert.That(panel, Does.Contain("if (_instance.IsOpen && !_transientActive) return;"),
                "[151차 P3] NPC 대화 보호 — 개방 중 덮어쓰기 스킵.");

            // 3) 정상 개설 경로 트랜지언트 해제 — 만료가 NPC 대화를 닫는 사고 방지(5개 경로+SelectChoice).
            int releaseCount = panel.Split(new[] { "_transientActive = false;" }, System.StringSplitOptions.None).Length - 1;
            Assert.That(releaseCount, Is.GreaterThanOrEqualTo(6),
                "[151차 P3] 정상 개설 경로(Open/ForGuard/ForVillager/ForStatic/ForAcquisition/SelectChoice) 트랜지언트 해제 — 최소 6곳.");

            // 4) 문구 무변경 핀 — 사용자 계약 문구가 소스에 그대로 존재.
            Assert.That(farm, Does.Contain("물뿌리개가 필요합니다"), "[151차 P3] 농경 문구 무변경 핀 1.");
            Assert.That(farm, Does.Contain("씨앗이 부족합니다"), "[151차 P3] 농경 문구 무변경 핀 2.");
            Assert.That(farm, Does.Contain("물뿌리개에 물이 없습니다"), "[151차 P3] 농경 문구 무변경 핀 3.");
        }

        // ── 헬퍼 ──

        private static T GetPrivateField<T>(object obj, string name)
        {
            var v = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj);
            return (T)v;
        }

        /// <summary>[93차 F1] 프라이빗 필드 주입 헬퍼 — Camera.main 불요 테스트 주입(MainCameraOverride 선례).</summary>
        private static void SetPrivateField(object obj, string name, object value)
        {
            obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(obj, value);
        }
    }
}
