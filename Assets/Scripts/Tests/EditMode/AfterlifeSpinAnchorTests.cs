using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [208차] 회전 참격 앵커 — 접촉판정(SweepDamage) 직접판정 + 라이프사이클 소스핀 EditMode 테스트.
    /// - SweepDamage는 internal static — InternalsVisibleTo(ProjectName.Tests.EditMode) 경유 직접판정([103차 F-1] 선례)
    /// - 적 주입 seam은 AfterlifeSkillSystemTests와 동일(TestRegisterEnemy — SpawnWave 미경유)
    /// - GameObject 사용 시 DestroyImmediate만 사용(126차 — Destroy는 EditMode 에러 로그 유발)
    /// </summary>
    public sealed class AfterlifeSpinAnchorTests
    {
        private readonly List<AfterlifeEnemy> _spawned = new List<AfterlifeEnemy>();

        [SetUp]
        public void SetUp()
        {
            AfterlifeSurvivalSystem.ResetAll();
            AfterlifeCardSystem.ResetAll();
            AfterlifeSkillSystem.ResetState(); // 앵커 DestroyAll 배선 경유 — 잔존 정리 겸용
        }

        [TearDown]
        public void TearDown()
        {
            AfterlifeSpinAnchor.TimeOverride = null;
            foreach (var e in _spawned)
                if (e != null) Object.DestroyImmediate(e.gameObject);
            _spawned.Clear();
        }

        private AfterlifeEnemy SpawnEnemy(float x, float z, float maxHP = 1000f)
        {
            var go = new GameObject("spinAnchorTestEnemy");
            var e = go.AddComponent<AfterlifeEnemy>();
            e.Configure(maxHP, 5f, 2f, 1);
            go.transform.position = new Vector3(x, 0f, z);
            AfterlifeSurvivalSystem.TestRegisterEnemy(e); // [156차 P5-수리] SpawnWave 미경유 EditMode 등록
            _spawned.Add(e);
            return e;
        }

        // ===== SweepDamage — 접촉 판정 (결정론) =====

        [Test]
        public void SweepDamage_HitsInRadius_AppliesHitDamage()
        {
            var e = SpawnEnemy(0.5f, 0.3f); // 평면거리 ~0.58m — HitRadius 0.9m 이내
            var cooldowns = new Dictionary<AfterlifeEnemy, float>();

            int hits = AfterlifeSpinAnchor.SweepDamage(Vector3.zero, 10f, cooldowns);

            Assert.AreEqual(1, hits);
            Assert.AreEqual(985f, e.CurrentHP, 0.01f); // 1000 - HitDamage 15
            Assert.That(cooldowns.ContainsKey(e), "개별 적 쿨다운 기록");
        }

        [Test]
        public void SweepDamage_CooldownBlocksRefire_UntilHitCooldownElapses()
        {
            var e = SpawnEnemy(0f, 0f);
            var cooldowns = new Dictionary<AfterlifeEnemy, float>();

            Assert.AreEqual(1, AfterlifeSpinAnchor.SweepDamage(Vector3.zero, 10f, cooldowns));
            float afterFirst = e.CurrentHP; // 985

            int hitsBlocked = AfterlifeSpinAnchor.SweepDamage(Vector3.zero, 11.9f, cooldowns); // 쿨 2초 미경과
            Assert.AreEqual(0, hitsBlocked);
            Assert.AreEqual(afterFirst, e.CurrentHP, 0.01f, "쿨다운 내 재판정 0건");

            int hitsAgain = AfterlifeSpinAnchor.SweepDamage(Vector3.zero, 12.1f, cooldowns); // 쿨 경과 — 재타격
            Assert.AreEqual(1, hitsAgain);
            Assert.AreEqual(afterFirst - AfterlifeSpinAnchor.HitDamage, e.CurrentHP, 0.01f);
        }

        [Test]
        public void SweepDamage_OutOfRadius_NoHit()
        {
            var e = SpawnEnemy(2f, 0f); // HitRadius 0.9m 밖
            var cooldowns = new Dictionary<AfterlifeEnemy, float>();

            int hits = AfterlifeSpinAnchor.SweepDamage(Vector3.zero, 10f, cooldowns);

            Assert.AreEqual(0, hits);
            Assert.AreEqual(1000f, e.CurrentHP, 0.01f);
        }

        // ===== 소스-계약 (앵커 라이프사이클 배선 존재) =====

        private static string Src(string rel) => File.ReadAllText(Path.Combine(Application.dataPath, rel));

        [Test]
        public void SourceContract_SpinAnchor_LifecycleWiring()
        {
            // 핀 1 — EnsureRunning isPlaying 게이트(편집 모드 스폰 금지) + 피해원 소스 계약
            string anchor = Src("Scripts/Systems/AfterlifeSpinAnchor.cs");
            Assert.That(anchor, Does.Contain("if (!Application.isPlaying) return;"));
            Assert.That(anchor, Does.Contain("TakeDamage(HitDamage, flat.normalized, \"afterlife_skill\")"),
                "피해원 소스 = afterlife_skill(기존 스킬 피해원 계승)");

            // 핀 2 — ResetState→DestroyAll 배선 + s_spin_slash case의 EnsureRunning(즉발 피해 제거)
            string skill = Src("Scripts/Systems/AfterlifeSkillSystem.cs");
            Assert.That(skill, Does.Contain("AfterlifeSpinAnchor.DestroyAll()"));
            // LastIndexOf — 선두의 CooldownOf 스위치가 아닌 Fire 스위치 case 블록 겨냥(s_spin_slash가 파일 내 2회 발생)
            int start = skill.LastIndexOf("case \"s_spin_slash\":", System.StringComparison.Ordinal);
            int end = skill.LastIndexOf("case \"s_charge\":", System.StringComparison.Ordinal);
            Assert.Greater(start, -1, "s_spin_slash case 존재");
            Assert.Greater(end, start, "s_spin_slash case 블록 경계");
            string block = skill.Substring(start, end - start);
            Assert.That(block, Does.Contain("AfterlifeSpinAnchor.EnsureRunning(playerT)"));
            Assert.That(block, Does.Not.Contain("DamageRadius"),
                "[208차] 즉발 반경 피해 제거 — 궤도 접촉피해로 대체(피해원 단일)");
        }
    }
}
