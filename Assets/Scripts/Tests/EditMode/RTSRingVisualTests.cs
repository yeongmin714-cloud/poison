using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-11 사용자 요구 — RTS 링 수정] 링 시각 단일화 계약.
    /// rts 1.PNG 실측: 파란 링 외 검은 사각형(링 쿼드 그림자/불투명 배경) + 레거시 Cylinder 디스크 이중 표시.
    /// </summary>
    public class RTSRingVisualTests
    {
        [Test]
        public void SetSelected_DoesNotSpawnLegacySpecialEffectsOutline()
        {
            // 09-30 시각 계약 이행 — SetSelected는 레거시 SpecialEffectsController 아웃라인을 만들지 않는다.
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.That(src, Does.Not.Contain("AddSelectionOutline"),
                "GuardPlaceholder는 레거시 선택 아웃라인을 스폰하지 않아야 한다(파란 링 단일 표시).");
            Assert.That(src, Does.Not.Contain("RemoveSelectionOutline"),
                "제거 경로도 함께 폐기 — 이중 생산자 잔존 방지.");
        }

        [Test]
        public void RingRenderers_DisableRealShadows()
        {
            // 검은 사각형 근원 차단 — 지면 데칼 쿼드가 태양 그림자를 방출/수신하지 않는다.
            string ringSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/SelectionRingController.cs");
            Assert.That(ringSrc, Does.Contain("ShadowCastingMode.Off"),
                "SelectionRing 쿼드는 그림자를 방출하지 않아야 한다.");
            Assert.That(ringSrc, Does.Contain("receiveShadows = false"),
                "SelectionRing 쿼드는 그림자를 수신하지 않아야 한다.");
            string markerSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.That(markerSrc, Does.Contain("ShadowCastingMode.Off"),
                "CommandMarker 쿼드는 그림자를 방출하지 않아야 한다.");
            Assert.That(markerSrc, Does.Contain("receiveShadows = false"),
                "CommandMarker 쿼드는 그림자를 수신하지 않아야 한다.");
        }

        [Test]
        public void SharedMaterial_IsShaderBased_WithHalvedDiameterAndThickerBand()
        {
            var blue = CommandRingPresentation.CreateMaterial(CommandRingPresentation.SelectedGuardColor);
            var red = CommandRingPresentation.CreateMaterial(CommandRingPresentation.ExplicitTargetColor);
            var gold = CommandRingPresentation.CreateMaterial(CommandRingPresentation.MoveTargetColor);
            try
            {
                Assert.AreEqual("Custom/SelectionRing", blue.shader.name);
                // [2026-10-11 사용자 요구] 이동 링 = 선택 링과 동일 Fluent 액센트(#58A6FF) — 구분은 크기가 담당.
                Assert.AreEqual(CommandRingPresentation.SelectedGuardColor,
                    CommandRingPresentation.MoveTargetColor,
                    "이동 링은 노랑이 아닌 파란 액센트여야 한다.");
                Assert.AreNotEqual(CommandRingPresentation.SelectedGuardColor,
                    CommandRingPresentation.ExplicitTargetColor,
                    "타겟 빨강은 독립 유지.");
                // Fluent 다크 백킹 배선 — 액센트 아래 패널 톤 밴드.
                Assert.AreEqual(CommandRingPresentation.RingBackingColor, blue.GetColor("_BackingColor"));
                Assert.AreEqual(CommandRingPresentation.RingBackingInnerPad, blue.GetFloat("_BackingInnerPad"), 0.0001f);
                Assert.AreEqual(CommandRingPresentation.RingBackingOuterPad, blue.GetFloat("_BackingOuterPad"), 0.0001f);
                // 지름 절반 + 증두 형상 상수.
                Assert.AreEqual(0.95f, CommandRingPresentation.BaseScale, 0.0001f,
                    "링 지름 = 현행 절반(사용자 요구).");
                Assert.AreEqual(0.78f, CommandRingPresentation.ShaderRingRadius, 0.0001f);
                Assert.AreEqual(0.14f, CommandRingPresentation.ShaderRingThickness, 0.0001f,
                    "밴드 코어 폭 0.28 — 구 베이크(0.22)보다 두꺼움(사용자 요구).");
                Assert.AreEqual(0.04f, CommandRingPresentation.ShaderGlowWidth, 0.0001f,
                    "Fluent 절제 글로우(0.08→0.04).");
                Assert.AreEqual(CommandRingPresentation.BaseScale * 0.65f, CommandRingPresentation.MoveMarkerScale, 0.0001f,
                    "이동 포인트 링 = BaseScale의 65%(15차 사용자 요구 — 조금 더 크게, 0.475→0.6175).");
                Assert.AreEqual(blue.GetFloat("_RingThickness"), gold.GetFloat("_RingThickness"), 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(blue);
                Object.DestroyImmediate(red);
                Object.DestroyImmediate(gold);
            }
        }

        [Test]
        public void FormationSpread_SquadDestinationsStayNearClickPoint()
        {
            // D1 기본안 — 분산 유지, 반경 축소: 링1 = 0.9×2 = 1.8m(구 3.2m), 링2 = 0.9×3.5 = 3.15m(구 5.6m).
            var result = FormationSpread.Distribute(Vector3.zero, 7);
            Assert.AreEqual(Vector3.zero, result[0], "index 0 = 클릭점(중심)");
            foreach (int i in System.Linq.Enumerable.Range(1, 6))
            {
                float dist = Vector3.Distance(result[i], Vector3.zero);
                Assert.AreEqual(1.8f, dist, 0.1f, $"index {i} 링1 반경 = 1.8m(축소)");
            }
            var wide = FormationSpread.Distribute(Vector3.zero, 19);
            foreach (int i in System.Linq.Enumerable.Range(7, 12))
            {
                float dist = Vector3.Distance(wide[i], Vector3.zero);
                Assert.AreEqual(3.15f, dist, 0.1f, $"index {i} 링2 반경 = 3.15m(축소)");
            }
        }

        [Test]
        public void MovePrecision_StopRadiiTightenedPerUserRequest()
        {
            // 이동 정확도 — 정지 반경 상수 계약(기존 1.0/0.6 → 0.25/0.15).
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.That(src, Does.Contain("MOVE_CLEAR_RADIUS = 0.25f"),
                "이동 명령 해제 반경 0.25m — 지정점 ±0.25m 도착.");
            Assert.That(src, Does.Contain("MOVE_STOP_RADIUS = 0.15f"),
                "이동 정지 판정 반경 0.15m.");
            Assert.That(src, Does.Contain("MOVE_STUCK_CLEAR_SECONDS"),
                "진행 정체 해제 게이트 배선(영구 멈춤 방지).");
            Assert.That(src.IndexOf("MOVE_CLEAR_RADIUS = 0.25f", System.StringComparison.Ordinal),
                Is.LessThan(src.IndexOf("distXZ <= MOVE_CLEAR_RADIUS", System.StringComparison.Ordinal)),
                "상수 선언이 사용부보다 선행(정상 배선).");
        }
    }
}
