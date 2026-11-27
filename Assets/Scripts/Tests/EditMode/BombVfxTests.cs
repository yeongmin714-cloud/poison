using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 181] 폭탄 폭발 VFX 에셋 배선 + Test_10 폭탄 지급 + 착지 텔레그래프 — 소스 문자열 어서션
    /// (NameplatePlateAlignTests S() 관례). ①Explosive→PlayImpactSequence 배선 ②VFX 경로 문자열+디스크 실존
    /// ③기존 로직 동결 핀(퓨즈/투척 히/피해 소스) ④SpawnCircle 신설+기존 부채꼴 Spawn 무수정 ⑤Test_10 지급 핀
    /// ⑥절차 파티클 금지(신규 범위).
    /// </summary>
    public sealed class BombVfxTests
    {
        private const string SrcBomb = "Assets/Scripts/Systems/Bomb.cs";
        private const string SrcBombArm = "Assets/Scripts/Systems/BombArmController.cs";
        private const string SrcTelegraph = "Assets/Scripts/Systems/TelegraphArcIndicator.cs";
        private const string SrcTestSetup = "Assets/Scripts/Systems/TestTerritoryCombatSetup.cs";
        private const string SrcMeteoriteRunner = "Assets/Scripts/Systems/MeteoriteVFXRunner.cs";

        private static string S(string path) => System.IO.File.ReadAllText(path);

        // ── ① Explosive → PlayImpactSequence 배선 소스 계약(Bomb.cs Explode 내) ──
        [Test]
        public void Explosive_Explode_Wires_Meteorite_PlayImpactSequence()
        {
            var src = S(SrcBomb);
            int explodeIdx = src.IndexOf("private void Explode", System.StringComparison.Ordinal);
            int applyIdx = src.IndexOf("private void ApplyExplosiveDamage", System.StringComparison.Ordinal);
            Assert.That(explodeIdx, Is.GreaterThanOrEqualTo(0), "Explode 메서드 존재");
            Assert.That(applyIdx, Is.GreaterThan(explodeIdx), "ApplyExplosiveDamage 순서 확인용");
            string explodeBody = src.Substring(explodeIdx, applyIdx - explodeIdx);
            Assert.That(explodeBody, Does.Contain("MeteoriteVFXRunner.PlayImpactSequence(transform.position)"),
                "[181] Explosive 폭발 — Meteorite 임팩트 시퀀스(03_GroundImpact+LightFlash→04_Adds) 1회 배선(180차 재사용)");
        }

        // ── ② VFX 경로 문자열 3종 + 디스크 실존 ──
        [Test]
        public void Vfx_Prefab_Paths_Exist_On_Disk()
        {
            var bombSrc = S(SrcBomb);
            Assert.That(bombSrc, Does.Contain("\"FX/Fire/VFX_Fire_01_Big\""),
                "[181] Molotov 화염 영역 경로 — BombVfx 상수 단일 소스");
            Assert.That(bombSrc, Does.Contain("\"FX/Death/DeathSmoke\""),
                "[181] Smoke 스모크 경로 — BombVfx 상수 단일 소스");
            // Explosive 임팩트 시퀀스 의존 자산 — MeteoriteVFXRunner 경로 단일 소스(3종째)
            var meteoriteSrc = S(SrcMeteoriteRunner);
            Assert.That(meteoriteSrc, Does.Contain("\"FX/Meteorite/VFX/03_GroundImpact\""),
                "[181] Explosive 임팩트 시퀀스 자산 경로 단일 소스");

            // 디스크 실존 — Application.dataPath = "<프로젝트>/Assets"
            string assets = Application.dataPath;
            Assert.That(System.IO.File.Exists($"{assets}/Resources/FX/Fire/VFX_Fire_01_Big.prefab"), Is.True,
                "Resources/FX/Fire/VFX_Fire_01_Big.prefab 실존");
            Assert.That(System.IO.File.Exists($"{assets}/Resources/FX/Death/DeathSmoke.prefab"), Is.True,
                "Resources/FX/Death/DeathSmoke.prefab 실존");
            Assert.That(System.IO.File.Exists($"{assets}/Resources/FX/Meteorite/VFX/03_GroundImpact.vfx"), Is.True,
                "Resources/FX/Meteorite/VFX/03_GroundImpact.vfx 실존");
        }

        // ── ③ 기존 로직 동결 핀 — 퓨즈/투척 히/피해 소스 잔존 ──
        [Test]
        public void Frozen_Logic_Pins_Fuse_ThrowForce_DamageSource()
        {
            var bombSrc = S(SrcBomb);
            Assert.That(bombSrc, Does.Contain("Invoke(nameof(Explode), explosionDelay)"),
                "[181] 동결 핀 — 퓨즈 Invoke(0.5s 기본) 잔존");
            Assert.That(bombSrc, Does.Contain("damageable.TakeDamage(damage, hitDir, \"Explosion\")"),
                "[181] 동결 핀 — OverlapSphere 피해 소스 \"Explosion\" 잔존");
            var armSrc = S(SrcBombArm);
            Assert.That(armSrc, Does.Contain("throwForce = 12f"),
                "[181] 동결 핀 — 포물선 투척 히 12(BombArmController) 잔존");
        }

        // ── ④ SpawnCircle 신설 + 기존 부채꼴 Spawn/BuildFanMesh 무수정 + followRoot 파라미터 ──
        [Test]
        public void SpawnCircle_New_While_Fan_Spawn_Intact()
        {
            var src = S(SrcTelegraph);
            Assert.That(src, Does.Contain(
                    "public static GameObject SpawnCircle(Vector3 pos, float radius, float duration, Transform followRoot = null, bool isElite = false)"),
                "[181] 원형 스폰 신설 — followRoot 추적 파라미터 포함");
            Assert.That(src, Does.Contain("Transform followRoot = null"),
                "[181] followRoot 파라미터 존재 — 폭탄 낙하 추적 모드");
            Assert.That(src, Does.Contain("BuildFanMesh"),
                "[181] 기존 부채꼴 판정/메시 패밀리 무수정 — BuildFanMesh 잔존");
            Assert.That(src, Does.Contain(
                    "public static GameObject Spawn(Vector3 pos, float radius, float duration, Vector3 direction, bool isElite = false)"),
                "[181] 기존 호 Spawn 시그니처 무수정");
        }

        // ── ⑤ Test_10 폭탄 지급 핀 ──
        [Test]
        public void Test10_Grants_Three_Bomb_Explosive()
        {
            var src = S(SrcTestSetup);
            Assert.That(src, Does.Contain("Bomb_Explosive, 3"),
                "[181] Test_10 지급 핀 — AddItem(PlayerInventory.Bomb_Explosive, 3)");
        }

        // ── ⑥ 절차 파티클 금지(Bomb.cs·TelegraphArcIndicator.cs 신규 범위 포함 전체) ──
        [Test]
        public void No_ProceduralParticles_In_Bomb_And_Telegraph()
        {
            // [181] 절차 파티클 금지(46차) — Bomb.cs/TelegraphArcIndicator.cs 전체에서 ParticleSystem
            // 컴포넌트 조립 부재(신규 코드가 도입하지 않음을 강제 — 기존 두 파일은 0건이므로 전체 스캔으로
            // 신규 범위 커버).
            // 벡터메시 금지 문자열("CreateMesh"/"Mesh(")은 소스 계약 대상에서 제외(계획 §P4-⑥) —
            // 기존 텔레그래프 절차 메시 패밀리(BuildFanMesh/BuildRingMesh/BuildBandMesh 선례)와 그 승계
            // 신규 BuildCircleMesh가 승인 예외 경로이므로 "Mesh(" 부재 계약은 성립 불가. 파티클 금지만 강제.
            var bombSrc = S(SrcBomb);
            Assert.That(bombSrc, Does.Not.Contain("AddComponent<ParticleSystem>"), "[181] 절차 파티클 금지 — Bomb.cs");
            Assert.That(bombSrc, Does.Not.Contain("ParticleSystem("), "[181] 절차 파티클 금지 — Bomb.cs");
            var telegraphSrc = S(SrcTelegraph);
            Assert.That(telegraphSrc, Does.Not.Contain("AddComponent<ParticleSystem>"), "[181] 절차 파티클 금지 — TelegraphArcIndicator.cs");
            Assert.That(telegraphSrc, Does.Not.Contain("ParticleSystem("), "[181] 절차 파티클 금지 — TelegraphArcIndicator.cs");
        }
    }
}
