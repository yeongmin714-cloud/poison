using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 86-P2] 승마 자세 계약 — 드라이버 SetMounted 플래그 전이(애니 부재 환경 포함) +
    /// 기존 Sit 파이프라인(SitDown→SitHold→SitUp) 재용 소스-계약 + 탑승/하차 배선 + Player_AC 사전 존재.
    /// 컨트롤러 무수정 계약 — Player_AC의 기존 SitDown/SitUp 트리거와 SitHold 상태를 재용한다.
    /// </summary>
    public class MountedPoseTests
    {
        static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void SetMounted_FlagFlips_WithoutAnimator()
        {
            var go = new GameObject("MountPoseTestDriver");
            var driver = go.AddComponent<HumanoidClipDriver>();
            try
            {
                Assert.That(driver.IsMountedPose, Is.False, "[86-P2] 초기값 미승마");
                driver.SetMounted(true);
                Assert.That(driver.IsMountedPose, Is.True, "[86-P2] 탑승 → 플래그 ON");
                driver.SetMounted(true); // 멱등 — 재호출 무변경
                Assert.That(driver.IsMountedPose, Is.True, "[86-P2] 멱등 재호출");
                driver.SetMounted(false);
                Assert.That(driver.IsMountedPose, Is.False, "[86-P2] 하차 → 플래그 OFF");
            }
            finally
            {
                Object.DestroyImmediate(go); // EditMode 계약 — Destroy 아님
            }
        }

        [Test]
        public void Driver_UsesExistingSitPipeline()
        {
            string src = Read("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(src, Does.Contain("[무번호 86-P2]"), "[86-P2] 마커 주석");
            Assert.That(src, Does.Contain("SetMounted"), "[86-P2] SetMounted API");
            Assert.That(src, Does.Contain("\"SitDown\""), "[86-P2] 기존 SitDown 트리거 재용");
            Assert.That(src, Does.Contain("\"SitUp\""), "[86-P2] 기존 SitUp 트리거 재용");
        }

        [Test]
        public void MountSystem_WiresDriverOnMountAndDismount()
        {
            string src = Read("Assets/Scripts/Systems/HorseMountSystem.cs");
            Assert.That(src, Does.Contain("SetMounted(true)"), "[86-P2] 탑승 시 승마 자세 진입");
            Assert.That(src, Does.Contain("SetMounted(false)"), "[86-P2] 하차 시 복귀");
        }

        [Test]
        public void PlayerAC_HasSitHoldStateAndTriggers()
        {
            var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                "Assets/Resources/Animation/Controllers/Player_AC.controller");
            Assert.That(ctrl, Is.Not.Null, "[86-P2] Player_AC 존재");

            bool hasSitDown = false, hasSitUp = false;
            foreach (var p in ctrl.parameters)
            {
                if (p.name == "SitDown") hasSitDown = true;
                if (p.name == "SitUp") hasSitUp = true;
            }
            bool hasSitHold = false;
            var root = ctrl.layers[0].stateMachine;
            foreach (var s in root.states)
                if (s.state != null && s.state.name == "SitHold") hasSitHold = true;

            Assert.That(hasSitDown && hasSitUp && hasSitHold, Is.True,
                "[86-P2] Sit 파이프라인 사전 존재(SitDown/SitUp 파라미터 + SitHold 상태) — 컨트롤러 신규 편집 불요 증명");
        }
    }
}
