using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 180] 몬스터 스킬 Intermediate+ 확장 + 보유 VFX 배선 — 소스 문자열 계약
    /// (NameplatePlateAlignTests S() 패턴 관례).
    /// ①Intermediate+ 10종 스킬 커버리지 + 기존 항목 유지
    /// ②VFX Resources 경로 실재(소스 문자열 + 파일 시스템)
    /// ③절차 파티클 금지(46차) / ④벡터메시 금지(84차)
    /// ⑤기존 밸런스 동결 핀(_skillMap 기존 라인 + 50% 선택 확률)
    /// ⑥MeteoriteVFXRunner.PlayImpactSequence 존재 + 기존 Burst 상수 무수정.
    /// </summary>
    public sealed class MonsterSkillVfxTests
    {
        private const string SkillSrc = "Assets/Scripts/Systems/MonsterSkillSystem.cs";
        private const string RunnerSrc = "Assets/Scripts/Systems/MeteoriteVFXRunner.cs";
        private const string AiSrc = "Assets/Scripts/Systems/AnimalAI.cs";

        private static string S(string relativePath) => System.IO.File.ReadAllText(relativePath);

        // ── ① 커버리지: 계획 표 10종 + 기존 항목 유지 ──

        [Test]
        public void SkillMap_Covers_All_Ten_IntermediatePlus_Ids()
        {
            var src = S(SkillSrc);
            string[] ids =
            {
                "electric_porcupine", "swamp_croc", "forest_spirit", "wild_troll",
                "ogre", "banshee", "griffin", "minotaur", "salamander", "shadow_assassin",
            };
            foreach (var id in ids)
                Assert.That(src, Does.Contain("\"" + id + "\""),
                    $"[180] _skillMap Intermediate+ 커버리지: {id}");
        }

        [Test]
        public void SkillMap_Keeps_Existing_Entries_Untouched()
        {
            var src = S(SkillSrc);
            string[] existingIds =
            {
                "rabbit", "wolf", "boar", "stone_golem", "fire_lizard",
                "poison_snake", "bat", "slime", "manticore",
            };
            foreach (var id in existingIds)
                Assert.That(src, Does.Contain("\"" + id + "\""),
                    $"[180] 기존 _skillMap 항목 유지: {id}");
            Assert.That(src, Does.Contain("_draculaSkills"), "[180] 드라큘라 보스 스킬 유지");
        }

        // ── ② VFX 경로 실재(소스 문자열 + 파일 시스템) ──

        [Test]
        public void Vfx_ResourcePaths_Present_In_Source_And_On_Disk()
        {
            var src = S(SkillSrc);
            string[] prefabPaths =
            {
                "FX/Selection/MagicCircle2",
                "FX/Impact/MagicHit",
                "FX/Impact/BasicHit",
                "FX/Impact/BasicHit2",
                "FX/Death/DeathSmoke",
                "FX/Selection/Buff",
            };
            foreach (var p in prefabPaths)
                Assert.That(src, Does.Contain(p), $"[180] VFX 경로 상수(단일 소스): {p}");

            // 파일 시스템 실재 — Application.dataPath = <project>/Assets
            foreach (var p in prefabPaths)
            {
                string file = System.IO.Path.Combine(Application.dataPath, "Resources", p + ".prefab");
                Assert.That(System.IO.File.Exists(file), Is.True,
                    $"[180] Resources 프리팹 실재: {p}.prefab");
            }
        }

        // ── ③ 절차 파티클 금지(46차) / ④ 벡터메시 금지(84차) ──

        [Test]
        public void MonsterSkillSystem_ForBids_Procedural_Particles_And_VectorMesh()
        {
            var src = S(SkillSrc);
            Assert.That(src, Does.Not.Contain("AddComponent<ParticleSystem>"),
                "[46차/180] 절차 파티클 컴포넌트 수동 생성 금지");
            Assert.That(src, Does.Not.Contain("ParticleSystem("),
                "[46차/180] 절차 파티클 생성자 호출 금지");
            Assert.That(src, Does.Not.Contain("CreateMesh"),
                "[84차/180] 벡터메시 생성 금지");
            Assert.That(src, Does.Not.Contain("Mesh("),
                "[84차/180] 런타임 Mesh 생성 금지");
        }

        // ── ⑤ 밸런스 동결 핀(기존 쿨다운/데미지/선택확률 무수정) ──

        [Test]
        public void Balance_Freeze_Existing_SkillMap_Lines_Pinned()
        {
            var src = S(SkillSrc);
            Assert.That(src, Does.Contain("new MonsterSkillData(MonsterSkill.Charge, 3f, 1.5f, 8f, \"Charge\")"),
                "[180] wolf Charge 기존 밸런스 동결(cd3/dmg1.5/range8)");
            Assert.That(src, Does.Contain("new MonsterSkillData(MonsterSkill.Leap, 5f, 2f, 5f, \"Leap\")"),
                "[180] boar Leap 기존 밸런스 동결(cd5/dmg2/range5)");
            Assert.That(src, Does.Contain("new MonsterSkillData(MonsterSkill.AoEExplosion, 4f, 2f, 5f, \"AoE\")"),
                "[180] stone_golem AoE 기존 밸런스 동결(cd4/dmg2/range5)");
            Assert.That(src, Does.Contain("new MonsterSkillData(MonsterSkill.Fireball, 3f, 1.5f, 10f, \"Fireball\")"),
                "[180] fire_lizard Fireball 기존 밸런스 동결(cd3/dmg1.5/range10)");
            Assert.That(src, Does.Contain("new MonsterSkillData(MonsterSkill.PoisonSting, 2f, 1.2f, 3f, \"PoisonSting\")"),
                "[180] poison_snake 독침 기존 밸런스 동결(cd2/dmg1.2/range3)");
        }

        [Test]
        public void Skill_Selection_Random50_Gate_Remains_In_AnimalAI()
        {
            var ai = S(AiSrc);
            Assert.That(ai, Does.Contain("Random.value < 0.5f"),
                "[180] 스킬 우선 선택 확률 50% 유지(기존 계약 — AnimalAI.TryAttack)");
        }

        // ── ⑥ MeteoriteVFXRunner 계약(additive — 기존 Burst 무수정) ──

        [Test]
        public void MeteoriteRunner_PlayImpactSequence_Added_Without_Burst_Changes()
        {
            var src = S(RunnerSrc);
            Assert.That(src, Does.Contain("public static void PlayImpactSequence(Vector3 position)"),
                "[180] AoE 임팩트 전용 시퀀스 진입점(additive public)");
            Assert.That(src, Does.Contain("public const float AnticipationDelaySec = 0f"),
                "[180] 기존 Burst 예고 지연 상수 무수정");
            Assert.That(src, Does.Contain("public static void Burst(Vector3 position, bool isCrit)"),
                "[180] 기존 Burst 진입점 시그니처 무수정");

            // 01_Anticipation 없이 — 임팩트 전용 시퀀서 본문에 예고 경로 부재
            int seqIdx = src.IndexOf("class MeteoriteVfxImpactSequencer", System.StringComparison.Ordinal);
            Assert.That(seqIdx, Is.GreaterThanOrEqualTo(0), "[180] 임팩트 전용 시퀀서 존재");
            string seqBody = src.Substring(seqIdx);
            Assert.That(seqBody, Does.Not.Contain("AnticipationPath"),
                "[180] PlayImpactSequence — 01_Anticipation 미발화(판정-시각 정합)");
        }

        // ── 보너스 핀: VFX 배선 호출 위치 존재(Execute별) ──

        [Test]
        public void Vfx_Wiring_Call_Sites_Present_In_Skill_Executors()
        {
            var src = S(SkillSrc);
            Assert.That(src, Does.Contain("MonsterSkillVfx.PlayCastCircle(monsterPos);"),
                "[180] Fireball — 발사 시점 캐스트 마법진");
            Assert.That(src, Does.Contain("MonsterSkillVfx.PlayImpactSmall(target.transform.position);"),
                "[180] PoisonSting — 피격점 소형 임팩트");
            Assert.That(src, Does.Contain("MonsterSkillVfx.PlayImpact(target.transform.position);"),
                "[180] Charge — 히트 임팩트");
            Assert.That(src, Does.Contain("MonsterSkillVfx.PlaySmoke(monsterPos);")
                .And.Contain("MonsterSkillVfx.PlaySmoke(teleportPos);"),
                "[180] Teleport — 원 위치+도착 위치 스모크 ×2");
            Assert.That(src, Does.Contain("MeteoriteVFXRunner.PlayImpactSequence(monsterPos);"),
                "[180] AoEExplosion — 폭발 임팩트 시퀀스");
            Assert.That(src, Does.Contain("MonsterSkillVfx.PlayBuff(healCasterPos);"),
                "[180] Heal — 시전자 발밑 버프 링");
            Assert.That(src, Does.Contain("MonsterSkillVfx.PlayDebuffCircle(target.transform.position);"),
                "[180] Debuff — 대상 발밑 마법진");
            Assert.That(src, Does.Contain("MonsterSkillSystem.MonsterSkillVfx.PlayImpact(other.transform.position,"),
                "[180] FireballProjectile — 충돌 지점 임팩트(MagicHit)");
        }
    }
}
