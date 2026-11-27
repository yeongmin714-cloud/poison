using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-M2] 몬스터 디테일 계약 — M1-2 충전 림/M2-1 히트 림/M3-2 사망 플립북/M4-2 오라 림.
    /// Source 잠금 + 폴백/계약 보존 검증. Random 0건.
    /// </summary>
    public class FxMonsterDetailM2Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void Source_M1_2_ChargeRim()
        {
            string src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("[무번호 29-M1-2]"), "[29-M1-2] 마커.");
            Assert.That(src, Does.Contain("float fill = FXMotion.Progress(t);"), "[29-M1-2] fill 산출.");
            Assert.That(src, Does.Contain("if (fill >= 0.75f)"), "[29-M1-2] 위험 임박 문턱 0.75.");
            Assert.That(src, Does.Contain("elite ? FXPalette.ElitAura : FXPalette.MonsterDanger"),
                "[29-M1-2] 등 위계(정예=주황/일반=적색) — FXPalette 단일 소스.");
            Assert.That(src, Does.Contain("private GameObject _chargeRim;"), "[29-M1-2] 림 인스턴스 필드.");
            Assert.That(src, Does.Contain("FxRimOverlay.Release(_chargeRim); _chargeRim = null;"),
                "[29-M1-2] 해제 경로(멱등+누출 금지) 존재.");
            Assert.That(src, Does.Contain("충전 림 폴백"),
                "[29-M1-2] 림 불가 환경 폴백 명시(기존 emissive만).");
        }

        [Test]
        public void Source_M2_1_HitRimFlash()
        {
            string src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("public void PlayHitRimFlash()"), "[29-M2-1] 진입점.");
            Assert.That(src, Does.Contain("PlayHitRimFlash();   // [무번호 29-M2-1]"),
                "[29-M2-1] 히트 체인 배선(CombatVFXController 직후).");
            Assert.That(src, Does.Contain("ReleaseHitRimAfter(rim, 0.12f)"), "[29-M2-1] 0.12s 유한 수명.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;"),
                "[29-M2-1] EditMode 무영향 가드.");
        }

        [Test]
        public void Source_M3_2_DeathFlipbook()
        {
            string src = Read("Assets/Scripts/Systems/DeathVFX.cs");
            Assert.That(src, Does.Contain("[무번호 29-M3-2]"), "[29-M3-2] 마커.");
            Assert.That(src, Does.Contain("if (!SpawnFlipbookSmoke(position, scale))"),
                "[29-M3-2] 플립북 1차 경로 + 프리팹 폴백 분기.");
            Assert.That(src, Does.Contain("SmokeResourcePath"),
                "[29-M3-2] 기존 DeathSmoke 프리팹 폴백 경로 보존.");
            Assert.That(src, Does.Contain("sheet.mode = ParticleSystemAnimationMode.Grid;"),
                "[29-M3-2] 시트 애니 Grid(플립북 UV).");
            Assert.That(src, Does.Contain("Object.Destroy(mat, life);"),
                "[29-M3-2] 머티리얼 인스턴스 유한 수명 파괴(누출 금지).");
        }

        [Test]
        public void Source_M4_2_EliteAuraRim()
        {
            string src = Read("Assets/Scripts/Systems/AfterlifeEnemy.cs");
            Assert.That(src, Does.Contain("[무번호 29-M4-2]"), "[29-M4-2] 마커.");
            Assert.That(src, Does.Contain("private GameObject _eliteRim;"), "[29-M4-2] 림 필드.");
            Assert.That(src, Does.Contain("FXMotion.SpawnPulse(phase)"),
                "[29-M4-2] 펄스 이징(FXMotion.SpawnPulse) 적용.");
            Assert.That(src, Does.Contain("FXPalette.ElitAura"),
                "[29-M4-2] 틴트 동기 — ElitAura 단일 소스.");
            Assert.That(src, Does.Contain("FxRimOverlay.Release(_eliteRim); _eliteRim = null;"),
                "[29-M4-2] 사망/전환 해제(누출 금지) 존재.");
        }

        [Test]
        public void Source_M_RowsTopDown_D1Alignment()
        {
            string src = Read("Assets/Scripts/Systems/FxFlipbookGenerator.cs");
            Assert.That(src, Does.Contain("(Rows - 1 - (f / Columns)) * CellSize"),
                "[29-D1] 프레임 행 배치 top-down(Unity 시트 애니 row-major 정합 — M3-2 소비 전제).");
        }

        [Test]
        public void Source_M_AllDeterministic()
        {
            Assert.That(Read("Assets/Scripts/Systems/AnimalAI.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[29-M2] AnimalAI Random 0건.");
            Assert.That(Read("Assets/Scripts/Systems/DeathVFX.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[29-M2] DeathVFX Random 0건.");
            // AfterlifeEnemy는 기존 난수 사용처 존재(사망 낫 확률 — 29-M4-2 이전 원형) → 신규 림 블록만 스코프 검사.
            string ae = Read("Assets/Scripts/Systems/AfterlifeEnemy.cs");
            int i0 = ae.IndexOf("[무번호 29-M4-2]");
            string blk = i0 >= 0 ? ae.Substring(i0, System.Math.Min(1400, ae.Length - i0)) : "";
            Assert.That(blk, Does.Not.Contain("UnityEngine.Random"), "[29-M4-2] 림 블록 결정론(Random 0건).");
        }
    }
}
