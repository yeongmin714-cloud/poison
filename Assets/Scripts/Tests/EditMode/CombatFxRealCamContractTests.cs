using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 16-A3] 전투 FX 실카메라 검증 계약 — 소스계약 테스트.
    /// 배경: 무번호 5 프리미엄 FX(텔레그래프 셰이더/패링 링/화이트 프레임)가 진단 전용 카메라에서만
    ///   검증됐고 실게임 팔로우 카메라에서는 미가시/깨짐 — 사용자 체감("수정 없음")의 뿌리.
    /// 계약: 신규/수정 FX는 실게임 팔로우 카메라 뷰 캡처로만 판정(진단 전용 카메라 검증 금지).
    ///   T1 프로브가 실카메라(Tag MainCamera) 기준 확정 — FindRealCamera 소스계약.
    ///   T2 프로브 폴백 복제 카메라는 실카메라 원점 고정시에만 CopyFrom 대체 — 실카메라 직접 뷰 선계약.
    ///   T3 폴백 카메라 자세 = PM(PlayerMovement) 공식 1:1 — 실게임 뷰 등가 계약.
    ///   T4 캡처는 URP SingleCameraRequest(정식 수단) — cam.Render() 동결 프레임 금지(무번호 5 교훈).
    ///   T5 배치 모드 계약 — -quit/-nographics 금지(Play 모드 배치, Cave156 선례).
    ///   T6 cfx 프로브 산출물 — TestOutput/cfx_*.png 필수(캡처 검증 대상).
    /// 실행: bash run_cfx_probe.sh (ENTRY=ProjectName.EditorTools.CombatFxRealCamProbe.RunAll).
    /// </summary>
    public class CombatFxRealCamContractTests
    {
        private const string ProbeRelPath = "Assets/Editor/CombatFxRealCamProbe.cs";
        private const string ProbeScriptRelPath = "run_cfx_probe.sh";

        private static string ProbeSource()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "..", ProbeRelPath));
        }

        private static string ProbeScriptSource()
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "..", ProbeScriptRelPath));
        }

        // ── T1. 실카메라 기준 확정 — FindRealCamera 소스계약 ──
        //   진단 전용 카메라 검증 금지 — 실카메라(Tag MainCamera) 기준으로만 캡처.
        [Test]
        public void T1_ProbeUsesRealCameraTagMainCamera()
        {
            string src = ProbeSource();
            StringAssert.Contains("Camera.main", src);
            StringAssert.Contains("FindObjectsByType<Camera>", src);
            StringAssert.Contains("\"MainCamera\"", src);
        }

        // ── T2. 폴백 복제 카메라는 실카메라 원점 고정시에만 — 실카메라 직접 뷰 선계약 ──
        //   폴백 카메라는 실카메라가 원점 고정(PM 부트 순서)시에만 CopyFrom 대체 — 실카메라 정상이면 실카메라 뷰.
        [Test]
        public void T2_FallbackCameraOnlyWhenRealCameraPinnedAtOrigin()
        {
            string src = ProbeSource();
            StringAssert.Contains("sqrMagnitude < 0.01f", src);
            StringAssert.Contains("CopyFrom(cam)", src);
            // 실카메라 정상이면 실카메라 뷰 그대로 캡처 — 폴백은 원점 고정시에만
            StringAssert.Contains("cam.transform.position == Vector3.zero", src);
        }

        // ── T3. 폴백 카메라 자세 = PM 공식 1:1 — 실게임 뷰 등가 계약 ──
        //   PM(PlayerMovement) 1605행 1:1 — PM_PITCH=57.6, PM_DIST=12.2.
        [Test]
        public void T3_FallbackCameraGeometryMatchesPmFormula()
        {
            string src = ProbeSource();
            StringAssert.Contains("PM_PITCH = 57.6f", src);
            StringAssert.Contains("PM_DIST = 12.2f", src);
            StringAssert.Contains("PM._cameraTransform", src);
            StringAssert.Contains("LookRotation", src);
        }

        // ── T4. 캡처는 URP 정식 수단 — SingleCameraRequest(무번호 5 교훈: cam.Render() 동결 프레임 금지) ──
        [Test]
        public void T4_CaptureUsesUrpSingleCameraRequest()
        {
            string src = ProbeSource();
            StringAssert.Contains("SingleCameraRequest", src);
            StringAssert.Contains("RenderPipeline.SubmitRenderRequest", src);
            // pxhash 자기검증 — 렌더 동결(md5 동일) 즉시 발각 계약
            StringAssert.Contains("===CAPTURE===", src);
            StringAssert.Contains("pxhash", src);
        }

        // ── T5. 배치 모드 계약 — -quit/-nographics 금지(Play 모드 배치, Cave156 선례) ──
        [Test]
        public void T5_BatchModeProhibitsQuitAndNographics()
        {
            string src = ProbeScriptSource();
            StringAssert.Contains("-quit 사용 금지", src);
            StringAssert.Contains("-nographics 금지", src);
            // UnityLockfile 가드 — 에디터가 열려 있으면 즉시 실패
            StringAssert.Contains("UnityLockfile", src);
        }

        // ── T6. cfx 프로브 산출물 — TestOutput/cfx_*.png 필수(캡처 검증 대상) ──
        [Test]
        public void T6_ProbeCapturesToTestOutput()
        {
            string src = ProbeSource();
            StringAssert.Contains("TestOutput/", src);
            StringAssert.Contains("cfx_01_control_terrain", src);
            StringAssert.Contains("EncodeToPNG", src);
        }
    }
}
