using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 19-C] 전투 VFX Phase C 계약테스트 — RTS 병사 지정 링 고품질화.
    /// C1 회전 세그먼트 링 + 중앙 그라디언트 필 + 선택 순간 확산 펄스(0.15s 이징)
    /// C2 명령 종류별 색상 계약: 이동=청록·공격=주황·집결=금
    /// C3 해제 알파 페이드 소멸(팝 소멸 금지)
    /// C4 다중 선택 링 겹침 밀도/알파 조절
    /// </summary>
    public class CombatFxCommandRingPhaseCTests
    {
        // ── T1. C1 셰이더 프로퍼티(세그먼트/중앙 필/선택 펄스) 존재 ──
        [Test]
        public void T1_SelectionRingShader_HasPhaseCProperties()
        {
            var shader = Shader.Find("Custom/SelectionRing");
            Assert.IsNotNull(shader, "Custom/SelectionRing 셰이더 탐색");
            Assert.IsTrue(shader.GetPropertyCount() > 0);
            Assert.GreaterOrEqual(shader.FindPropertyIndex("_Segments"), 0, "C1 회전 세그먼트 링 프로퍼티(_Segments)");
            Assert.GreaterOrEqual(shader.FindPropertyIndex("_CenterFillAlpha"), 0, "C1 중앙 그라디언트 필(_CenterFillAlpha)");
            Assert.GreaterOrEqual(shader.FindPropertyIndex("_BurstT"), 0, "C1 선택 순간 확산 펄스(_BurstT)");
        }

        // ── T2. C1 CreateMaterial 배선 — 세그먼트/중앙 필/선택 펄스 ──
        [Test]
        public void T2_CreateMaterial_WiresPhaseCProperties()
        {
            var mat = CommandRingPresentation.CreateMaterial(CommandRingPresentation.SelectedGuardColor);
            try
            {
                Assert.IsNotNull(mat);
                Assert.AreEqual(CommandRingPresentation.ShaderSegmentCount, mat.GetFloat("_Segments"), 0.0001f,
                    "C1 회전 세그먼트 링 6분할 배선");
                Assert.AreEqual(CommandRingPresentation.ShaderCenterFillAlpha, mat.GetFloat("_CenterFillAlpha"), 0.0001f,
                    "C1 중앙 그라디언트 필 배선");
                Assert.AreEqual(-1f, mat.GetFloat("_BurstT"), 0.0001f,
                    "C1 선택 펄스 초기 off(SelectionRingController가 스폰 순간 구동)");
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        // ── T3. C2 명령 종류별 색상 계약 — 이동=청록·공격=주황·집결=금 ──
        [Test]
        public void T3_CommandColorContract_MoveCyan_AttackOrange_RallyGold()
        {
            // 이동 링 = 청록(계획서 C2)
            var cyan = new Color(0.188f, 0.835f, 0.784f, 1f);
            Assert.AreEqual(cyan, CommandRingPresentation.MoveTargetColor,
                "C2 이동 링 = 청록(#30D5C8 계열)");
            // 공격 링 = 빨강([무번호 83 사용자 결정] 11-20 — 기존 주황(19-C2)에서 변경)
            var redRing = new Color(0.85f, 0.16f, 0.12f, 1f);
            Assert.AreEqual(redRing, CommandRingPresentation.ExplicitTargetColor,
                "[83] 공격 링 = 빨강");
            // 집결 링 = 금(계획서 C2 — 집결 명령은 RTS 명령 체계에 미구현, 상수만 준비)
            var gold = new Color(0.980f, 0.860f, 0.300f, 1f);
            Assert.AreEqual(gold, CommandRingPresentation.RallyColor,
                "C2 집결 링 = 금(집결 명령 도입 시 착색)");
            // 선택 링 = 이동 링과 동일 액센트(기존 계약 — 구분은 크기가 담당)
            Assert.AreEqual(CommandRingPresentation.MoveTargetColor,
                CommandRingPresentation.SelectedGuardColor,
                "C2 이동 링은 선택 링과 동일 액센트(청록)여야 한다.");
            Assert.AreNotEqual(CommandRingPresentation.SelectedGuardColor,
                CommandRingPresentation.ExplicitTargetColor,
                "C2 공격 링 주황은 독립 유지.");
        }

        // ── T4. C3 해제 알파 페이드 소멸(팝 소멸 금지) — 기존 0.3s 규약 ──
        [Test]
        public void T4_FadeSeconds_ContractPopsProhibited()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.That(src, Does.Contain("FadeSeconds = 0.3f"), "C3 해제 축소 페이드 0.3s(팝 소멸 금지).");
            Assert.That(src, Does.Contain("SetMarkerAlpha"), "C3 해제 시 알파 페이드 배선.");
        }

        // ── T5. C4 다중 선택 링 겹침 밀도/알파 조절 ──
        [Test]
        public void T5_OverlapAlpha_3PlusDilutes()
        {
            Assert.AreEqual(1f, CommandRingPresentation.GetOverlapAlpha(1), 0.0001f,
                "C4 1개 링 — 알파 완화 없음");
            Assert.AreEqual(1f, CommandRingPresentation.GetOverlapAlpha(2), 0.0001f,
                "C4 2개 링 — 알파 완화 없음");
            Assert.AreEqual(CommandRingPresentation.OverlapMinAlpha,
                CommandRingPresentation.GetOverlapAlpha(3), 0.0001f,
                "C4 3+ 겹침 — 알파 완화(가독성)");
            Assert.AreEqual(CommandRingPresentation.OverlapMinAlpha,
                CommandRingPresentation.GetOverlapAlpha(8), 0.0001f,
                "C4 8+ 겹침 — 알파 완화(가독성)");
        }

        // ── T6. C1 선택 순간 0.15s 이징 확산 펄스 — SelectionRingController 구동 ──
        [Test]
        public void T6_SelectionRingController_SelectPulseDriven()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/SelectionRingController.cs");
            Assert.That(src, Does.Contain("_selectPulseElapsed"), "C1 선택 펄스 진행 필드.");
            Assert.That(src, Does.Contain("CommandRingPresentation.SelectPulseDuration"), "C1 선택 펄스 0.15s 이징.");
            Assert.That(src, Does.Contain("_BurstT"), "C1 _BurstT 구동.");
            Assert.That(src, Does.Contain("easeOutCubic"), "C1 easeOutCubic 이징.");
        }
    }
}
