using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 16-B] 전투 VFX Phase B 계약테스트 — 몬스터 공격 예고 VFX(적색 텔레그래프) 고품질화.
    ///   T1 부채꼴 게이지 오버로드 Spawn(groundPosition, radius, duration, elite, useWedge, attackDirection) — 6개 인자.
    ///   T2 부채꼴 게이지 0→1 진행(예고 진행에 따라 각도 방향 채움) — _Wedge01 소스계약.
    ///   T3 셰이더 B2 효과 프로퍼티 — _Wedge01/_AttackAngle/_Scan/_StartPulse/_Flash.
    ///   T4 위험도 색 계약 — 일반 공격=적색(NormalColor)·정예/강공격=주황(EliteColor) — 기존 주황-적(1, 0.30, 0.16) 정합.
    ///   T5 경고 SFX — SFXType.Telegraph_Warning + GetSFXName 매핑 + SFX_Telegraph_Warning 클립(B4).
    ///   T6 스폰 시그니처 불변 — AnimalAI/AfterlifeEnemy 무수정(기존 3개 인자 Spawn 유지).
    ///   T7 데칼 방식 비교 실측 — 프로브 CaptureDecalProjector(B5).
    /// </summary>
    public class CombatFxTelegraphPhaseBTests
    {
        private const string IndicatorRelPath = "Assets/Scripts/Systems/AttackTelegraphIndicator.cs";
        private const string ShaderRelPath = "Assets/Shaders/TelegraphRing.shader";
        private const string SfxRelPath = "Assets/Scripts/Systems/SoundEffectManager.cs";
        private const string ProbeRelPath = "Assets/Editor/CombatFxRealCamProbe.cs";

        private static string Source(string relPath)
        {
            return System.IO.File.ReadAllText(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "..", relPath));
        }

        // ── T1. 부채꼴 게이지 오버로드 — 6개 인자 Spawn(groundPosition, radius, duration, elite, useWedge, attackDirection) ──
        [Test]
        public void T1_WedgeGaugeSpawnOverload()
        {
            string src = Source(IndicatorRelPath);
            StringAssert.Contains("bool elite, bool useWedge, Vector3 attackDirection", src);
            // 기존 시그니처 유지(AnimalAI/AfterlifeEnemy 무수정 계약)
            StringAssert.Contains("public static GameObject Spawn(Vector3 groundPosition, float radius, float duration)", src);
        }

        // ── T2. 부채꼴 게이지 0→1 진행 — _Wedge01 소스계약 ──
        [Test]
        public void T2_WedgeGaugeProgressesWithTelegraph()
        {
            string src = Source(IndicatorRelPath);
            StringAssert.Contains("_useWedge", src);
            StringAssert.Contains("_Wedge01", src);
            StringAssert.Contains("_attackAngle", src);
        }

        // ── T3. 셰이더 B2 효과 프로퍼티 — _Wedge01/_AttackAngle/_Scan/_StartPulse/_Flash ──
        [Test]
        public void T3_ShaderPhaseBProperties()
        {
            string src = Source(ShaderRelPath);
            StringAssert.Contains("_Wedge01", src);
            StringAssert.Contains("_AttackAngle", src);
            StringAssert.Contains("_Scan", src);
            StringAssert.Contains("_StartPulse", src);
            StringAssert.Contains("_Flash", src);
        }

        // ── T4. 위험도 색 계약 — 일반 공격=적색 · 정예/강공격=주황 ──
        [Test]
        public void T4_RiskColorContract_NormalRed_EliteOrange()
        {
            string src = Source(IndicatorRelPath);
            // 일반 공격=적색(1, 0.15, 0.10) — 순수 적색(톤 조화 알파 유지)
            StringAssert.Contains("new Color(1f, 0.15f, 0.10f, ALPHA)", src);
            // 정예/강공격=주황(1, 0.30, 0.16) — 기존 주황-적 톤 승계
            StringAssert.Contains("new Color(1f, 0.30f, 0.16f, ALPHA)", src);
            StringAssert.Contains("NormalColor", src);
            StringAssert.Contains("EliteColor", src);
            StringAssert.Contains("elite ? EliteColor : NormalColor", src);
        }

        // ── T5. 경고 SFX — SFXType.Telegraph_Warning + GetSFXName 매핑 + SFX_Telegraph_Warning 클립 ──
        [Test]
        public void T5_WarningSfxTelegraph_Warning()
        {
            string src = Source(SfxRelPath);
            StringAssert.Contains("Telegraph_Warning", src);
            StringAssert.Contains("SFX_Telegraph_Warning", src);

            string indSrc = Source(IndicatorRelPath);
            StringAssert.Contains("SoundEffectManager.SFXType.Telegraph_Warning", indSrc);
            // Application.isPlaying 가드 — EditMode 무영향(기존 패턴)
            StringAssert.Contains("Application.isPlaying", indSrc);

            // 클립 존재
            string clipPath = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "Resources/Audio/SFX/SFX_Telegraph_Warning.wav");
            Assert.IsTrue(System.IO.File.Exists(clipPath), "SFX_Telegraph_Warning.wav 클립 미발견");
        }

        // ── T6. 스폰 시그니처 불변 — AnimalAI/AfterlifeEnemy 무수정(기존 3개 인자 Spawn 유지) ──
        [Test]
        public void T6_SpawnSignatureUnchanged()
        {
            // [190차] 몬스터 적색링(공격 텔레그래프 호) 제거 — 스폰 경로 삭제 계약으로 리핀.
            //   발동 플래시/인디케이터 정리는 null 가드라 무영향, SpawnAlert(!링)·폭탄 착지 텔레그래프는 별도 유지.
            string aiSrc = Source("Assets/Scripts/Systems/AnimalAI.cs");
            StringAssert.Contains("_telegraphIndicator = null;   // [190차] 적색링 제거", aiSrc);
            Assert.That(aiSrc, Does.Not.Contain("TelegraphArcIndicator.Spawn(transform.position, _attackRange, TelegraphWindupSeconds, arcDir)"),
                "[190차] 몬스터 적색링 스폰 경로 삭제 확인(AnimalAI).");

            string afterlifeSrc = Source("Assets/Scripts/Systems/AfterlifeEnemy.cs");
            StringAssert.Contains("_telegraphIndicator = null;   // [190차] 적색링 제거", afterlifeSrc);
            Assert.That(afterlifeSrc, Does.Not.Contain("TelegraphArcIndicator.Spawn(transform.position, _attackRange, TelegraphWindupSeconds, arcDir)"),
                "[190차] 몬스터 적색링 스폰 경로 삭제 확인(AfterlifeEnemy).");
        }

        // ── T7. 데칼 방식 비교 실측 — 프로브 CaptureDecalProjector ──
        [Test]
        public void T7_DecalProjectorComparisonProbe()
        {
            string src = Source(ProbeRelPath);
            StringAssert.Contains("CaptureDecalProjector", src);
            StringAssert.Contains("DecalProjector", src);
            StringAssert.Contains("cfx_12_decal_projector", src);
        }
    }
}
