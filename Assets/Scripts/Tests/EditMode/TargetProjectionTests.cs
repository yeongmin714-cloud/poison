using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [91차 F3/F4] 대상 앵커 투영 공용화 + 패널 대상 배치 계약 (소스 문자열 어서션 — [91차] 마커,
    /// DialogueFeedbackTests 선례).
    ///
    /// 진단(마을씬 실측): 상호작용 패널이 우상단 고정 — Camera.main 무효/멀티 카메라 씬 + z≤0 가드 부재,
    /// NPC 대화창 하단 처짐 — 패널 중심을 대상+0.9m에 배치.
    ///
    ///  ① TargetScreenProjector.TryProject — 시그니처/z&gt;0 가드/Camera.main→allCameras 폴백 순서
    ///  ② InteractionPanelUTK.PlaceAtTarget — 유틸 사용 + 실패 시 화면 하단 중앙 폴백
    ///  ③ NPCDialoguePanelUTK.PlaceAtAnchor — [151차 P2] 항상 중앙 하단 고정(구 머리 위 앵커/투영 경로 폐기)
    ///  ④ EInteractionPromptUTK.OnGUI — 유틸 사용(screenPx 직접, GUI y 반전 유지)
    ///
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class TargetProjectionTests
    {
        private const string ProjectorPath = "Assets/Scripts/UI/Toolkit/TargetScreenProjector.cs";
        private const string PanelPath = "Assets/Scripts/UI/Toolkit/InteractionPanelUTK.cs";
        private const string DialoguePath = "Assets/Scripts/UI/Toolkit/NPCDialoguePanelUTK.cs";
        private const string PromptPath = "Assets/Scripts/UI/Toolkit/EInteractionPromptUTK.cs";

        // ── 1. TryProject — 시그니처/z>0 가드/allCameras 폴백 순서 ──

        [Test]
        public void TryProject_Signature_ZGuard_And_CameraFallbackOrder()
        {
            var src = System.IO.File.ReadAllText(ProjectorPath);
            Assert.That(src, Does.Contain("[91차 F3]"), "[91차 F3] 마커 주석.");
            Assert.That(src, Does.Contain("[92차 결함2]"), "[92차 결함2] 마커 주석.");
            Assert.That(src, Does.Contain(
                    "public static bool TryProject(Component target, float upOffsetM, out Vector2 screenPx, out string camName)"),
                "[91차 F3] TryProject 시그니처(공용 계약 — 3소비처).");
            Assert.That(src, Does.Contain("vp.z <= 0f"),
                "[91차 F3/92차 결함2] z 가드 — 카메라 뒤 무효(z>0 = 유일 성패 기준).");
            // [92차 결함2] ±64px 화면 마진 검사 폐기 — 근접/대화 카메라 프러스텀 가장자리 앵커까지
            //   무효화해 PlacementTick 폴백 튐("부착 안 됨" 체감)을 유발. 화면 좌표는 항상 반환(호출부 클램프).
            Assert.That(src, Does.Not.Contain("ScreenMarginPx"),
                "[92차 결함2] ±64px 화면 마진 검사 폐기.");
            Assert.That(src, Does.Contain("b.center.y + b.extents.y * 0.15f"),
                "[92차 결함2] 앵커 = bounds 중심 + 높이 15%(머리 살짝 위 — 프러스텀 안쪽).");
            Assert.That(src, Does.Not.Contain("b.max.y"),
                "[92차 결함2] 구 max.y 앵커 폐기(프러스텀 상단 밖 무효 원인).");

            int main = src.IndexOf("Camera.main");
            int all = src.IndexOf("Camera.allCameras");
            Assert.That(main, Is.GreaterThan(0), "① Camera.main 우선.");
            Assert.That(all, Is.GreaterThan(main), "② 무효 → Camera.allCameras 순회 폴백(멀티 카메라 씬 수리).");

            Assert.That(src, Does.Contain("[TargetProject] 대상 투영 — target="),
                "④ 카메라 결정 진단 로그.");
            Assert.That(src, Does.Contain("LogThrottleSeconds = 0.5f"),
                "④ 진단 로그 스로틀 0.5s.");
            Assert.That(src, Does.Contain("public static bool HasVisualBounds"),
                "bounds 유무 판정 공개 — PlaceAtAnchor 1.9m 폴백 분기용(F4).");
        }

        // ── 1-1. [92차 결함2] 로직 테스트 — z>0 성공(화면 밖 좌표 반환 포함)/z<0 실패 ──
        //  IsProjectionValid 리플렉션 직접 호출 — TryProject의 Screen.width 가드(배치 환경 0)와
        //  Camera.main 씬 상태 의존을 우회해 카메라별 성패 계약을 결정론 검증.

        [Test]
        public void TryProject_Logic_ZPositive_OffscreenReturned_ZNegative_Invalid_92()
        {
            var camGo = new GameObject("TP92_Cam");
            try
            {
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 0f, -10f);   // 기본 전방 = +Z

                var mi = typeof(ProjectName.UI.Toolkit.TargetScreenProjector).GetMethod("IsProjectionValid",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.That(mi, Is.Not.Null, "[92차] IsProjectionValid 존재(TryProject 카메라별 성패 단일소스).");

                // z>0 + 화면 밖(원거리) — 유효, 화면 밖 좌표도 그대로 반환([92차] 클램프 계약 전제)
                object[] argsFront = { cam, new Vector3(5000f, 0f, 0f), default(Vector2), null };
                bool okFront = (bool)mi.Invoke(null, argsFront);
                var px = (Vector2)argsFront[2];
                Assert.That(okFront, Is.True, "[92차 결함2] z>0 = 유효(±64px 화면 밖 판정 폐기).");
                Assert.That(px.x, Is.GreaterThan(1920f),
                    "[92차 결함2] 화면 밖 좌표도 반환(호출부 루트 클램프 후 배치 계약).");
                Assert.That(argsFront[3], Is.EqualTo("TP92_Cam"), "카메라명 반환.");

                // z<0 — 카메라 뒤 = 무효(유일 실패 기준)
                object[] argsBack = { cam, new Vector3(0f, 0f, -30f), default(Vector2), null };
                Assert.That((bool)mi.Invoke(null, argsBack), Is.False,
                    "[92차 결함2] z<0(카메라 뒤) = 무효 — 폴백/스킵 계약.");

                // 카메라 부재 = 무효
                object[] argsNull = { null, Vector3.zero, default(Vector2), null };
                Assert.That((bool)mi.Invoke(null, argsNull), Is.False, "카메라 부재 = 무효.");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void TryProject_EndToEnd_ZPositive_True_ZNegative_False_92()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
                Assert.Ignore("[92차] 배치 환경 Screen 크기 0 — 카메라별 성패는 IsProjectionValid 로직 테스트로 검증.");

            var camGo = new GameObject("TP92_E2E_Cam");
            var targetFront = new GameObject("TP92_E2E_Front");
            var targetBack = new GameObject("TP92_E2E_Back");
            try
            {
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                targetFront.transform.position = new Vector3(0f, 0f, 0f);    // 카메라 전방(z>0)
                targetBack.transform.position = new Vector3(0f, 0f, -30f);   // 카메라 뒤(z<0)

                // z>0 — 성공(bounds 부재 → transform+upOffset 앵커)
                Assert.That(ProjectName.UI.Toolkit.TargetScreenProjector.TryProject(
                        targetFront.transform, 0.5f, out Vector2 frontPx, out _),
                    Is.True, "[92차 결함2] z>0 → TryProject 성공.");
                Assert.That(frontPx, Is.Not.EqualTo(default(Vector2)), "화면 좌표 반환.");

                // z<0 — 실패(전 카메라 뒤) — 호출부 폴백(하단 중앙)/무배치 트리거 계약
                Assert.That(ProjectName.UI.Toolkit.TargetScreenProjector.TryProject(
                        targetBack.transform, 0.5f, out _, out _),
                    Is.False, "[92차 결함2] z<0 → TryProject 실패(전 카메라 뒤).");
            }
            finally
            {
                Object.DestroyImmediate(targetFront);
                Object.DestroyImmediate(targetBack);
                Object.DestroyImmediate(camGo);
            }
        }

        // ── 2. InteractionPanelUTK.PlaceAtTarget — 유틸 + 폴백(NPC 본체 재투영 → 화면 클램프) ──

        /// <summary>
        /// [91차 F3 → 94차 F4 → 98차 F3 → 101차 F-1] 계약 진화 — 구 "투영 실패 → 화면 하단 중앙 상수 폴백
        /// (rootW/2−panelW/2, rootH−panelH−80)"은 [98차 F3]에서 폐기(마을씬 테스트 9 — 폴백 배치(630,760)
        /// 다수로 NPC에서 ≈150px 이탈). [98차 F3] 폴백 체인(본체 재투영/눌러투영)은 [101차 F-1] 본체 단일
        /// 앵커(TryProjectBodyAnchor)로 수렴 폐기 — 투영 실패 = 총 투영 실패 무배치. 구 상수/경로는 폐기
        /// 회귀 핀으로 전환. [102차 후속] 구 폴백 메서드 핀(TryProjectNpcBody 등)을 [101차 F-1] 계약으로 갱신
        /// (qa102_isolated3 — TryProjectNpcBody IndexOf -1 기인).
        /// </summary>
        [Test]
        public void InteractionPanelUTK_PlaceAtTarget_Util_And_NpcBodyClampFallback_98F3()
        {
            var src = System.IO.File.ReadAllText(PanelPath);
            Assert.That(src, Does.Contain("[91차 F3]"), "[91차 F3] 마커 주석.");
            Assert.That(src, Does.Contain("private void PlaceAtTarget(object target)"),
                "PlaceAtTarget 시그니처 유지([72차] 계약).");

            // [94차 F4 → 101차 F-1] 앵커 계약 진화 — 정면 앵커(TryProjectFrontAnchor/_frontAnchorProxy/
            //   TargetUpM=0.9m)는 폐기되고 본체 단일 앵커(TryProjectBodyAnchor/_bodyAnchorProxy,
            //   BodyAnchorFootUpM=1.95m)로 치환(NPC 머리 위 배치).
            int utilCall = src.IndexOf("if (!TryProjectBodyAnchor(tracked, out Vector2 screen))");
            Assert.That(utilCall, Is.GreaterThan(0),
                "[101차 F-1] PlaceAtTarget → 본체 앵커 투영(TryProjectBodyAnchor — 프록시 Transform 경유).");
            // [94차 F4 → 101차 F-1] 공용 유틸 경유 유지 — 프록시 지점을 TargetScreenProjector.TryProject에 실어 투영
            //   (z>0 가드/카메라 폴백 일원화 계약 승계 — TargetScreenProjector 무수정).
            Assert.That(src, Does.Contain("TargetScreenProjector.TryProject(_bodyAnchorProxy, 0f, out screen, out _)"),
                "[101차 F-1] 공용 TargetScreenProjector.TryProject 경유 유지(렌더러 없는 프록시 Transform).");
            Assert.That(src, Does.Contain("[94차 F4]"), "[94차 F4] 정면 앵커 마커 주석.");
            Assert.That(src, Does.Contain("BodyAnchorFootUpM = 1.95f"),
                "[101차 F-1] Foot 본 y 기준 상승 1.95m(머리 위 앵커 — 구 TargetUpM 0.9m 폐기 승계).");
            Assert.That(src, Does.Contain("(Screen.height - screen.y)"),
                "y축 반전 산식 유지(165-166행 계약).");
            Assert.That(src, Does.Contain("panelBounds.width * 0.5f"),
                "패널 하단 중앙 정렬(중앙 X 보정) 유지.");

            // [102차 후속] [101차 F-1] 폴백 수렴 — 구 ①NPC 본체 재투영(TryProjectNpcBody)/②전방평면 눌러투영
            //   (TryProjectNpcBodyPushedToCameraPlane)은 폐기(단일 앵커 수렴 — 소스 삭제 이력 주석). 본체 앵커
            //   투영 실패(전 카메라 z<0/카메라 부재) = 총 투영 실패 — LogPlacementVerify(null, "폴백") 후 무배치 즉귀.
            int bodyFallback = src.IndexOf("LogPlacementVerify(null, \"폴백\")", System.StringComparison.Ordinal);
            Assert.That(bodyFallback, Is.GreaterThan(utilCall),
                "[101차 F-1] 폴백 수렴 — 본체 앵커 투영 실패 후 총 투영 실패 로그(폴백 오판 z<0 수리 계약 승계).");
            Assert.That(src.IndexOf("return;", bodyFallback, System.StringComparison.Ordinal) - bodyFallback,
                Is.LessThan(200).And.GreaterThan(0),
                "[101차 F-1] 총 투영 실패 → 무배치 즉귀(return — 배치 산출 미진행).");
            // [102차 후속] [101차 F-1] 폐기 회귀 핀 — 구 폴백/정면앵커 투영 메서드 잔존 금지(단일 앵커 수렴 계약).
            Assert.That(src, Does.Not.Contain("TryProjectNpcBody"),
                "[101차 F-1] 구 폴백 ① NPC 본체 재투영 폐기(TryProjectNpcBody 잔존 금지).");
            Assert.That(src, Does.Not.Contain("TryProjectNpcBodyPushedToCameraPlane"),
                "[101차 F-1] 구 폴백 ② 전방평면 눌러투영 폐기(TryProjectNpcBodyPushedToCameraPlane 잔존 금지).");
            Assert.That(src, Does.Not.Contain("TryProjectFrontAnchor"),
                "[101차 F-1] 구 정면앵커 폐기(본 패널 잔존 금지 — EInteractionPromptUTK 별도 구현 무관).");
            // [98차 F3] 구 하단 고정 상수 경로 폐기 — 폴백 배치(630,760) 상수 제거 회귀 핀.
            Assert.That(src, Does.Not.Contain("rootHeight - panelBounds.height - 80f"),
                "[98차 F3] 구 하단 고정 상수 폐기(rootH−panelH−80 — 폴백 배치(630,760) 상수 경로 제거).");
            Assert.That(src, Does.Not.Contain("rootWidth * 0.5f - panelBounds.width * 0.5f"),
                "[98차 F3] 구 폴백 X=화면 중앙 상수 폐기(하단 고정 경로와 동반 제거).");
            // [92차 결함2] 클램프 배치 계약 — z>0이면 화면 밖 좌표도 받아 패널 전체가 화면 안에 들도록 클램프.
            Assert.That(src, Does.Contain("[92차 결함2]"), "[92차 결함2] 마커 주석.");
            Assert.That(src, Does.Contain("Mathf.Clamp(scaledPoint.x - panelBounds.width * 0.5f"),
                "[92차 결함2] X 클램프(패널 전체 화면 안 — 하단 중앙 기준).");
            Assert.That(src, Does.Contain("Mathf.Clamp(scaledPoint.y - panelBounds.height"),
                "[92차 결함2] Y 클램프(패널 전체 화면 안).");
            // [98차 F3] F3-1 배치 산출 worldBound 비의존화 — 패널 크기 3단 폴백(resolvedStyle/캐시/기본 크기).
            Assert.That(src, Does.Contain("private Rect ResolvePanelBoundsPx(float rootWidth, float rootHeight)"),
                "[98차 F3] F3-1 패널 실측 크기 3단 폴백(resolvedStyle → 이전 실측 캐시 → 기본 크기).");
            Assert.That(src, Does.Contain("[98차 F3] F3-1 배치 산출 worldBound 비의존화"),
                "[98차 F3] F3-1 마커 주석(worldBound 비의존 — worldBound NaN 시 중앙 폴백 배치 금지).");
        }

        // ── 2b. [99차 F-A] A-2/A-3 — 화면 경계 밖 ±200px 초과 → 비가시 숨김(display만 None)/투영 회복 →
        //        재표시(Flex) + ScreenBoundsMarginPx 상수 고정 핀(소스 단정 — 98F3 선례 패턴) ──

        [Test]
        public void InteractionPanelUTK_ScreenBoundsGuard_HideBeyond200px_Recover_99FA()
        {
            var src = System.IO.File.ReadAllText(PanelPath);

            // [99차 F-A] A-2 여유 상수 — 화면 경계 밖 ±200px 기준.
            Assert.That(src, Does.Contain("private const float ScreenBoundsMarginPx = 200f;"),
                "[99차 F-A] A-2 화면 경계 여유 상수(±200px).");

            // [99차 F-A] A-2 가드 메서드 존재 + 경계 판정이 가드 내부(양축 ±200px)에서 수행.
            int guard = src.IndexOf("private bool UpdateOcclusionVisibility(Vector2 screen, string path)",
                System.StringComparison.Ordinal);
            Assert.That(guard, Is.GreaterThan(0),
                "[99차 F-A] A-2 대상 화면 가시성 가드 메서드 존재.");
            Assert.That(src.IndexOf("screen.x <= Screen.width + ScreenBoundsMarginPx"),
                Is.GreaterThan(guard), "A-2 경계 판정 — x 상단(+200px) 가드 내부.");
            Assert.That(src.IndexOf("screen.y <= Screen.height + ScreenBoundsMarginPx"),
                Is.GreaterThan(guard), "A-2 경계 판정 — y 상단(+200px) 가드 내부.");

            // [99차 F-A] A-2 비가시 숨김 — display만 None(Hide/Close 계약 미경유 — IsOpen/배치 추적 폴링 생존)
            // + A-3 사유 로그. 숨김 상태 플래그 저장(1s 틱 스팸 방지).
            int hideLine = src.IndexOf("style.display = DisplayStyle.None;   // 비가시 숨김", guard);
            Assert.That(hideLine, Is.GreaterThan(guard),
                "[99차 F-A] A-2 가드가 style.display = None으로 비가시 숨김(패널 사라짐 — 좌하단 고정 폐기).");
            int hideLog = src.IndexOf("\"폴백=비가시숨김(화면경계±200px 초과)\"");
            Assert.That(hideLog, Is.GreaterThan(hideLine),
                "[99차 F-A] A-3 숨김 사유 로그(폴백=비가시숨김(화면경계±200px 초과)).");
            Assert.That(src, Does.Contain("_occludedHidden = true;"),
                "[99차 F-A] A-2 숨김 상태 플래그(재로그 스팸 방지 — 폴링 생존).");

            // [99차 F-A] A-2 투영 회복 자동 재표시 — Flex + BringToFront(Show 경유 아님) + 회복 사유 로그.
            int showLine = src.IndexOf("style.display = DisplayStyle.Flex;   // 투영 회복", guard);
            Assert.That(showLine, Is.GreaterThan(guard),
                "[99차 F-A] A-2 투영 회복 시 자동 재표시(display Flex).");
            int showLog = src.IndexOf("\"폴백=재표시(투영 회복)\"");
            Assert.That(showLog, Is.GreaterThan(showLine),
                "[99차 F-A] A-3 회복 사유 로그(폴백=재표시(투영 회복)).");
            Assert.That(src.IndexOf("BringToFront();", showLine, System.StringComparison.Ordinal) - showLine,
                Is.LessThan(showLog - showLine).And.GreaterThan(0),
                "[99차 F-A] A-2 재표시 후 BringToFront(회복 프레임 상면 유지).");

            // [102차 후속] [101차 F-1] 가드 배선 단일 수렴 — 구 폴백("폴백")/정면앵커("정면앵커") 2-경로 배선은
            //   본체 단일 앵커 수렴으로 폐기. PlaceAtTarget의 유일 가드 호출처 = "본체앵커" 레이블(투영 성공 좌표
            //   가시성 판정 — [99차 F-A] 계약 승계). 총 투영 실패(z<0/카메라 부재)는 가드 도달 전 무배치 즉귀.
            int bodyCall = src.IndexOf("if (!UpdateOcclusionVisibility(screen, \"본체앵커\")) return;",
                System.StringComparison.Ordinal);
            Assert.That(bodyCall, Is.GreaterThan(0),
                "[101차 F-1] 본체앵커 경로 가드 배선(경계 밖 → 비가시 숨김/무배치 — [99차 F-A] 계약 승계).");
            Assert.That(src.IndexOf("if (!UpdateOcclusionVisibility(", bodyCall + 1, System.StringComparison.Ordinal),
                Is.EqualTo(-1),
                "[101차 F-1] 가드 호출처 단일 수렴(폐기 경로 재배선 금지 — 본체 단일 앵커 계약).");
            int failLog = src.IndexOf("LogPlacementVerify(null, \"폴백\")", System.StringComparison.Ordinal);
            Assert.That(bodyCall, Is.GreaterThan(failLog).And.LessThan(guard),
                "[101차 F-1] 배선 순서 유지 — 총 투영 실패 무배치(폴백 로그) → 본체앵커 가드(가드 정의는 호출처 후단).");

            // [99차 F-A] A-2 개방 리셋 — 신규 대상 개방 시 숨김 상태 초기화(재표시 기준).
            Assert.That(src, Does.Contain("_occludedHidden = false;   // [99차 F-A] 개방 시 숨김 상태 리셋"),
                "[99차 F-A] A-2 Show 경유 숨김 상태 리셋(직전 세션 잔존 방지).");

            // [99차 F-A] A-3 사유 병기 배선 — LogPlacementVerify reason 인수(숨김/회복 사유 로그 말미 병기).
            Assert.That(src, Does.Contain("[99차 F-A] A-3 폴백 숨김 사유 — reason 인수"),
                "[99차 F-A] A-3 사유 인수 병기 설계 주석(경로 존재 핀).");
        }

        // ── 2c. [100차 F-1] 머리 위 부착 배치 산식 + 시맨틱 판정(소스 단정 — 99F-A 선례 패턴) ──

        [Test]
        public void InteractionPanelUTK_HeadAbovePlacement_And_SemanticAttach_100F1()
        {
            var src = System.IO.File.ReadAllText(PanelPath);
            Assert.That(src, Does.Contain("[100차 F-1]"), "[100차 F-1] 마커 주석.");

            // [100차 F-1 → 101차 F-1] 머리 위 부착 상수 진화 — 앵커 = Foot 본 y + 1.95m(머리 위, 스펙 지정).
            Assert.That(src, Does.Contain("private const float BodyAnchorFootUpM = 1.95f;"),
                "[101차 F-1] 본체 앵커 상승분(m) — Foot 본 y 기준(구 HeadAnchorUpM 0.25m 폐기 승계).");
            Assert.That(src, Does.Contain("private const float BodyAnchorBoundsPadM = 0.15f;"),
                "[101차 F-1] Foot 본 부재 폴백 — bounds maxY + 0.15m(스펙 지정).");
            Assert.That(src, Does.Contain("private const float HeadAnchorGapPx = 6f;"),
                "[100차 F-1] 패널 하단 ↔ 머리앵커 투영점 간격(px).");
            Assert.That(src, Does.Contain("private const float HeadAttachMaxDistancePx = 24f;"),
                "[100차 F-1] 시맨틱 부착 판정 거리(px) — ≤24px = 부착(성공).");

            // [100차 F-1 → 101차 F-1] 본체 앵커 고도 산식 — TryMeasureFootBoneY 우선(Foot 본 y + 1.95m)/
            //   bounds 폴백(maxY + 0.15m)/부재 폴백(transform.y + 1.95m) 3단 분기(ComputeBodyAnchorHeight).
            Assert.That(src, Does.Contain("if (TryMeasureFootBoneY(tracked, out float footY))"),
                "[101차 F-1] 본체 앵커 고도 — Foot 본 측정 우선 분기(캐릭터 1.8m 키 계약).");

            // [100차 F-1] 머리 위 배치 산식 — y = 앵커 y − panelH − HeadAnchorGapPx(패널 하단이 앵커 위 6px 얹힘).
            //   구 산식(top = 앵커 y − panelH)은 패널이 NPC 지점 아래로 안양 — 시맨틱 계약 부재 → 갱신 핀.
            Assert.That(src, Does.Contain("Mathf.Clamp(scaledPoint.y - panelBounds.height - HeadAnchorGapPx"),
                "[100차 F-1] 머리 위 배치 산식 — y = 앵커 y − panelH − HeadAnchorGapPx(하단이 앵커 위 6px).");
            Assert.That(src, Does.Not.Contain("rootHeight - panelBounds.height - 80f"),
                "[100차 F-1] 구 하단 고정 상수 폐기 회귀 핀([98차 F3] 승계).");

            // [100차 F-1] 시맨틱 부착 판정 — 패널 하단 중앙 ↔ 머리앵커 거리 ≤ 24px = 부착(성공).
            Assert.That(src, Does.Contain("float bottomCenterY = style.top.value.value + panelBounds.height;"),
                "[100차 F-1] 패널 하단 중앙 Y 산출(top + panelH — 배치 산식과 짝).");
            int semantic = src.IndexOf("headDist <= HeadAttachMaxDistancePx", System.StringComparison.Ordinal);
            Assert.That(semantic, Is.GreaterThan(0),
                "[100차 F-1] 시맨틱 부착 판정 — 패널하단중앙↔머리앵커 거리 ≤ HeadAttachMaxDistancePx(24px).");
            Assert.That(src, Does.Contain("$\"머리부착={headDist:F0}px(성공)\""),
                "[100차 F-1] 시맨틱 판정 성공 로그(머리부착=…px(성공)).");
            Assert.That(src, Does.Contain("$\"머리부착={headDist:F0}px(실패 — {HeadAttachMaxDistancePx:F0}px 초과)\""),
                "[100차 F-1] 시맨틱 판정 실패 로그(머리부착=…px(실패 — 24px 초과)).");

            // [100차 F-1] 검증 로그 태그 병기 — [97차 F3] 부착 검증 로그 말미에 [100차 F-1] {semantic}.
            int logTag = src.IndexOf("[97차 F3] 부착 검증 — 경로={path}", System.StringComparison.Ordinal);
            Assert.That(logTag, Is.GreaterThan(0), "[97차 F3] 부착 검증 로그 유지(시맨틱 판정 병기 기반).");
            Assert.That(src.IndexOf("[100차 F-1] {semantic}", System.StringComparison.Ordinal),
                Is.GreaterThan(logTag),
                "[100차 F-1] 검증 로그 태그 병기 — Debug.Log 말미에 [100차 F-1] {semantic}(기존 120px 판정과 병기).");
        }

        // ── 3. NPCDialoguePanelUTK.PlaceAtAnchor — [151차 P2] 항상 중앙 하단 고정(구 머리 위 앵커 폐기) ──

        [Test]
        public void NPCDialoguePanelUTK_PlaceAtAnchor_AlwaysBottomCenter_151()
        {
            var src = System.IO.File.ReadAllText(DialoguePath);
            // [151차 P2] 계약 진화 — 구 머리 위 앵커(HeadAnchorUpM 0.55m/NoBoundsHeadM 1.9m/HasVisualBounds 분기/
            //   AnchorClamp 1.2~2.2m/프록시)는 폐기되고 항상 중앙 하단(50%, 85%)으로 일원화
            //   (Play 로그 `[150차 P2] 패널 투영 무효` 반복 — 앵커 투영 불안정, 사용자 지시 "항상 중앙 하단").
            Assert.That(src, Does.Contain("[151차 P2]"), "[151차 P2] 마커 주석.");
            Assert.That(src, Does.Not.Contain("HeadAnchorUpM"),
                "[151차 P2] 구 HeadAnchorUpM 상수 폐기(머리 위 앵커 경로 소멸).");
            Assert.That(src, Does.Not.Contain("NoBoundsHeadM"),
                "[151차 P2] 구 NoBoundsHeadM 상수 폐기(머리 위 앵커 경로 소멸).");
            Assert.That(src, Does.Not.Contain("HasVisualBounds(anchor)"),
                "[151차 P2] 구 bounds 유무 분기 폐기(투영 경로 소멸).");
            Assert.That(src, Does.Not.Contain("TargetScreenProjector.TryProject(anchor, upM, out Vector2 screen, out _)"),
                "[151차 P2] 구 공용 유틸 앵커 투영 호출 폐기(배치는 폴백 산식 상시 — 투영 경로 소멸).");
            Assert.That(src, Does.Contain("style.top = Mathf.Clamp(scaled.y - panelH, 0f, Mathf.Max(0f, rootH - panelH))"),
                "[91차 F4 → 151차 P2] 패널 바닥 중앙 = 배치 지점(루트 클램프 산식 유지 — ApplyAnchorLayout).");
            // [92차 결함2] 클램프 배치 계약 — 화면 밖 좌표도 받아 패널 전체가 화면 안에 들도록 클램프.
            Assert.That(src, Does.Contain("[92차 결함2]"), "[92차 결함2] 마커 주석.");
            Assert.That(src, Does.Contain("Mathf.Clamp(scaled.x - panelW * 0.5f"),
                "[92차 결함2] X 클램프(패널 전체 화면 안 — 바닥 중앙 기준).");
        }

        // ── 4. EInteractionPromptUTK.OnGUI — 유틸 사용 ──

        [Test]
        public void EInteractionPromptUTK_OnGUI_UsesSharedProjector()
        {
            var src = System.IO.File.ReadAllText(PromptPath);
            // [92차] 마커 계약 진화 — [91차 F3] 마커가 [91차 F3 → 92차 결함2]로 치환됨(투영 공용 유틸 계약 갱신 이력).
            Assert.That(src, Does.Contain("[91차 F3 → 92차 결함2]"),
                "[91차 F3 → 92차 결함2] 마커 주석(계약 진화 반영).");
            // [94차 F4] out 매개변수 선언 분리 — Vector2 screenPx 사전 선언 후 분기별 대입(호출 형태 갱신).
            Assert.That(src, Does.Contain("Vector2 screenPx;"),
                "[94차 F4] out 선언 분리 — screenPx 사전 선언.");
            int utilCall = src.IndexOf("projected = TargetScreenProjector.TryProject(_screenCandidate.tr, AnchorGapM, out screenPx, out _)");
            Assert.That(utilCall, Is.GreaterThan(0),
                "[91차 F3 → 94차 F4] OnGUI → 공용 유틸(out screenPx — 선언 분리 호출 형태).");
            // [94차 F4] 정면 앵커 분기 — NPC 계열 대상은 상호작용 패널과 동일 정면 앵커(프록시 경유 공용 유틸).
            int frontBranch = src.IndexOf("projected = TryProjectFrontAnchor(_screenCandidate.tr, out screenPx)");
            Assert.That(frontBranch, Is.GreaterThan(0), "[94차 F4] 정면 앵커 분기 존재(NPC 계열).");
            Assert.That(frontBranch, Is.LessThan(utilCall), "[94차 F4] NPC 계열 우선 분기 → 비NPC 공용 유틸 경로 순서.");
            Assert.That(src, Does.Contain("Screen.height - screenPx.y"),
                "GUI 좌표계 y 반전 유지(기존 산식).");
            // [92차 결함2] 스킵 로직 갱신 — z<0(전 카메라 뒤)만 스킵, z>0은 클램프 표시(화면 근접/밖 공통).
            Assert.That(src, Does.Contain("[92차 결함2]"), "[92차 결함2] 마커 주석.");
            // [94차 F4] 스킵 계약 주석 갱신 — "구 z<0 스킵 계약 승계"(전 카메라 뒤(z≤0) 미표시).
            int skipComment = src.IndexOf("구 z<0 스킵 계약 승계");
            Assert.That(skipComment, Is.GreaterThan(0), "[92차 결함2 → 94차 F4] 전 카메라 뒤 미표시(스킵) 계약 주석.");
        }
    }
}
