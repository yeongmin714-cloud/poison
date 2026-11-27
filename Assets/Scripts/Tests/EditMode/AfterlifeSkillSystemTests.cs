using System.Collections.Generic;
using System.IO;
using System.Linq; // [156차 P5] Find(.First) — CS1061 수리
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [156차 P5] 이계 스킬 8종 + 생존 배선 EditMode 테스트.
    /// 검증: 자동발동 쿨 게이트(주입 시계) / 반경 피해 / 슬로우 계수 / 뼈 방패 충전-소비 / 장판 틱 /
    ///   해골 반지 예약-소비 / 두 번째 목숨 소비 / 소스-계약(SurvivalSystem·PlayerHealth·Enemy·Movement·AnimalAI·Orb 훅 존재).
    /// 시간 의존은 UnscaledTimeOverride 주입(EditMode 특성 — Time.unscaledTime 정지).
    /// GameObject 사용 시 DestroyImmediate만 사용(126차 — Destroy는 EditMode 에러 로그 유발).
    /// </summary>
    public sealed class AfterlifeSkillSystemTests
    {
        private AfterlifeEnemy _enemy;
        private PlayerHealth _hp;

        [SetUp]
        public void SetUp()
        {
            AfterlifeSurvivalSystem.ResetAll();
            AfterlifeCardSystem.ResetAll();
            AfterlifeSkillSystem.ResetState();
        }

        [TearDown]
        public void TearDown()
        {
            AfterlifeSkillSystem.TestActiveOverride = null;
            AfterlifeSkillSystem.UnscaledTimeOverride = null;
            AfterlifeSkillSystem.PlayerOverride = null;
            AfterlifeCardSystem.EndRun();
            if (_enemy != null) Object.DestroyImmediate(_enemy.gameObject);
            if (_hp != null) Object.DestroyImmediate(_hp.gameObject);
        }

        private AfterlifeEnemy SpawnEnemy(float x, float z, float maxHP = 1000f)
        {
            var go = new GameObject("skillTestEnemy");
            var e = go.AddComponent<AfterlifeEnemy>();
            e.Configure(maxHP, 5f, 2f, 1);
            go.transform.position = new Vector3(x, 0f, z);
            AfterlifeSurvivalSystem.TestRegisterEnemy(e); // [156차 P5-수리] SpawnWave 미경유 EditMode 등록
            _enemy = e;
            return e;
        }

        private PlayerHealth EnsurePlayer()
        {
            if (_hp == null)
            {
                var go = new GameObject("skillTestPlayer");
                _hp = go.AddComponent<PlayerHealth>();
                go.transform.position = Vector3.zero;
            }
            AfterlifeSkillSystem.PlayerOverride = _hp.transform; // [156차 P5-수리] EditMode Awake 미호출 — Instance null 대비
            return _hp;
        }

        private static CardDef Find(string id) => AfterlifeCardSystem.Pool.First(c => c.Id == id);

        private void GrantAndArm(float now)
        {
            EnsurePlayer(); // [156차 P5-수리] PlayerOverride 주입 — Fire 위치 해석용(미호출 시 발화 스킵)
            AfterlifeCardSystem.SelectFromDef(Find("s_spin_slash"));
            AfterlifeSkillSystem.TestActiveOverride = true;
            AfterlifeSkillSystem.UnscaledTimeOverride = () => now;
        }

        // ===== 회전 참격 — [208차] 궤도 접촉피해 대체(Tick 직접 피해 제거) =====

        [Test]
        public void SpinSlash_Tick_DealsNoDirectDamage_CooldownRefireStaysSafe()
        {
            var e = SpawnEnemy(2f, 0f); // 구 계약 기준 3.5m 이내
            GrantAndArm(10f);
            AfterlifeSkillSystem.Tick();
            // [208차] s_spin_slash는 궤도 접촉피해 앵커로 대체(피해원 단일) — Tick은 앵커 EnsureRunning
            //         (편집 모드 isPlaying 게이트로 무동작)+링 VFX 뿐, 직접 피해 없음.
            //         접촉 피해는 AfterlifeSpinAnchorTests.SweepDamage 직접판정으로 이관.
            Assert.AreEqual(1000f, e.CurrentHP, 0.01f);

            AfterlifeSkillSystem.UnscaledTimeOverride = () => 15f; // 쿨 8초 미경과 — 무피해
            AfterlifeSkillSystem.Tick();
            Assert.AreEqual(1000f, e.CurrentHP, 0.01f);

            AfterlifeSkillSystem.UnscaledTimeOverride = () => 18.5f; // 쿨 경과 — 재발동도 여전히 직접 피해 없음
            AfterlifeSkillSystem.Tick();
            Assert.AreEqual(1000f, e.CurrentHP, 0.01f);
        }

        // ===== 시간 찌그러짐 — 적 로컬 슬로우 =====

        [Test]
        public void TimeWarp_SlowFactor_04_ThenRestores()
        {
            EnsurePlayer();
            AfterlifeCardSystem.SelectFromDef(Find("s_time_warp"));
            AfterlifeSkillSystem.TestActiveOverride = true;
            Assert.AreEqual(1f, AfterlifeSkillSystem.EnemySlowFactor, 0.0001f);

            AfterlifeSkillSystem.UnscaledTimeOverride = () => 100f;
            AfterlifeSkillSystem.Tick();
            Assert.AreEqual(0.4f, AfterlifeSkillSystem.EnemySlowFactor, 0.0001f);

            AfterlifeSkillSystem.UnscaledTimeOverride = () => 103f; // 2초 경과
            Assert.AreEqual(1f, AfterlifeSkillSystem.EnemySlowFactor, 0.0001f);
        }

        // ===== 뼈 방패 — 충전/1회 소비 =====

        [Test]
        public void BoneShield_Charge_OnceConsumable()
        {
            EnsurePlayer();
            AfterlifeCardSystem.SelectFromDef(Find("s_bone_shield"));
            AfterlifeSkillSystem.TestActiveOverride = true;
            AfterlifeSkillSystem.UnscaledTimeOverride = () => 5f;
            AfterlifeSkillSystem.Tick();

            Assert.IsTrue(AfterlifeCardSystem.TryConsumeBoneShield(), "첫 타격 차단");
            Assert.IsFalse(AfterlifeCardSystem.TryConsumeBoneShield(), "2회차는 소비 불가");
        }

        // ===== 혼불 장판 — 틱 피해 =====

        [Test]
        public void FireZone_Ticks_ThenExpires()
        {
            var e = SpawnEnemy(1f, 1f); // 반경 2.5m 이내(~1.41m)
            EnsurePlayer(); // [156차 P5-수리] PlayerOverride 주입 — 발동 위치 해석용
            AfterlifeCardSystem.SelectFromDef(Find("s_fire_zone"));
            AfterlifeSkillSystem.TestActiveOverride = true;
            AfterlifeSkillSystem.UnscaledTimeOverride = () => 10f;
            AfterlifeSkillSystem.Tick(); // 발동(첫 틱 포함)

            AfterlifeSkillSystem.UnscaledTimeOverride = () => 10.5f;
            AfterlifeSkillSystem.Tick(); // 틱 2
            Assert.AreEqual(988f, e.CurrentHP, 0.01f); // 1000 - 6×2

            AfterlifeSkillSystem.UnscaledTimeOverride = () => 14f; // 3초 경과 — 만료
            AfterlifeSkillSystem.Tick();
            Assert.IsFalse(AfterlifeSkillSystem.FireZoneActive);
            Assert.AreEqual(988f, e.CurrentHP, 0.01f); // 만료 후 피해 없음
        }

        // ===== 해골 반지 — 예약/소비 =====

        [Test]
        public void SkullRing_Chain10_ArmsNextOrbBonus()
        {
            AfterlifeCardSystem.SelectFromDef(Find("p_skull_ring"));
            Assert.IsFalse(AfterlifeCardSystem.ConsumeSkullRingBonus(), "예약 전 소비 불가");
            for (int i = 0; i < 9; i++)
                Assert.IsFalse(AfterlifeCardSystem.NotifyEnemyKilledForSkullRing(), $"{i + 1}연쇄 — 미달성");
            Assert.IsTrue(AfterlifeCardSystem.NotifyEnemyKilledForSkullRing(), "10연쇄 — 예약");
            Assert.IsTrue(AfterlifeCardSystem.ConsumeSkullRingBonus(), "예약 소비");
            Assert.IsFalse(AfterlifeCardSystem.ConsumeSkullRingBonus(), "1회 소비 후 소멸");
        }

        // ===== 두 번째 목숨 =====

        [Test]
        public void SecondLife_Consume_ReviveAtRatio()
        {
            var hp = EnsurePlayer();
            hp.ReviveAtRatio(0.5f); // 사망 상태 해제+비율 부활 경로
            Assert.AreEqual(hp.MaxHP * 0.5f, hp.CurrentHP, 0.01f);

            AfterlifeCardSystem.SelectFromDef(Find("sp_second_life"));
            Assert.IsTrue(AfterlifeCardSystem.SecondLifeAvailable);
            Assert.AreEqual(0.5f, AfterlifeCardSystem.ConsumeSecondLife(), 0.0001f);
            Assert.IsFalse(AfterlifeCardSystem.SecondLifeAvailable, "1회 소비 후 소멸");
        }

        // ===== 혼백 활 자동 발사 — 쿨 게이트 =====

        [Test]
        public void SoulBow_AutoFire_OncePerSixSeconds()
        {
            var e = SpawnEnemy(5f, 0f);
            EnsurePlayer(); // [156차 P5-수리] PlayerOverride 주입 — 자동 발사 위치 해석용
            AfterlifeCardSystem.SelectFromDef(Find("w_soul_bow")); // 활성 무기 = 혼백 활
            AfterlifeSkillSystem.TestActiveOverride = true;
            AfterlifeSkillSystem.UnscaledTimeOverride = () => 100f;
            AfterlifeSkillSystem.Tick();
            float expected = 1000f - WeaponData.Bow.damage * 0.8f;
            Assert.AreEqual(expected, e.CurrentHP, 0.01f);

            AfterlifeSkillSystem.UnscaledTimeOverride = () => 103f; // 6초 미경과
            AfterlifeSkillSystem.Tick();
            Assert.AreEqual(expected, e.CurrentHP, 0.01f);

            AfterlifeSkillSystem.UnscaledTimeOverride = () => 106.5f; // 쿨 경과
            AfterlifeSkillSystem.Tick();
            Assert.AreEqual(expected - WeaponData.Bow.damage * 0.8f, e.CurrentHP, 0.01f);
        }

        // ===== 소스-계약 (배선 존재) =====

        private static string Src(string rel) => File.ReadAllText(Path.Combine(Application.dataPath, rel));

        [Test]
        public void SourceContract_SurvivalDriver_WiresCardAndSkill()
        {
            string src = Src("Scripts/Systems/AfterlifeSurvivalSystem.cs");
            Assert.That(src, Does.Contain("AfterlifeCardSystem.BeginRun"));
            Assert.That(src, Does.Contain("AfterlifeSkillSystem.ResetState"));
            Assert.That(src, Does.Contain("AfterlifeCardSystem.WaveIntervalSeconds"));
            Assert.That(src, Does.Contain("AfterlifeCardSystem.SurrenderHarvestRatio"));
            Assert.That(src, Does.Contain("ReviveAtRatio"));
            Assert.That(src, Does.Contain("ForceReturnDoor"));
            Assert.GreaterOrEqual(src.Split(new[] { "AfterlifeCardSystem.EndRun()" }, System.StringSplitOptions.None).Length - 1, 3,
                "EndRun 배선 3곳(FinishRun/Permadeath/ResetAll)");
        }

        [Test]
        public void SourceContract_PlayerHealthHooks_DelegateInjection()
        {
            string src = Src("Scripts/Core/PlayerHealth.cs");
            Assert.That(src, Does.Contain("IncomingDamageModifier"));
            Assert.That(src, Does.Contain("TryBlockOnce"));
            Assert.GreaterOrEqual(src.Split(new[] { "TryBlockOnce" }, System.StringSplitOptions.None).Length - 1, 3,
                "뼈 방패 게이트 — 훅 필드+TakeDamage 2경로(PlayerHealth는 델리게이트 TryBlockOnce 소비 — [156차 P5-수리])");

            string card = Src("Scripts/Systems/AfterlifeCardSystem.cs");
            Assert.That(card, Does.Contain("PlayerHealth.IncomingDamageModifier"));
            Assert.That(card, Does.Contain("PlayerHealth.TryBlockOnce"));
        }

        [Test]
        public void SourceContract_EnemyMovement_And_TelegraphGates()
        {
            string enemy = Src("Scripts/Systems/AfterlifeEnemy.cs");
            Assert.That(enemy, Does.Contain("AfterlifeSkillSystem.EnemySlowFactor"));
            Assert.That(enemy, Does.Contain("AfterlifeCardSystem.EnemyMoveFactor"));

            string ai = Src("Scripts/Systems/AnimalAI.cs");
            Assert.That(ai, Does.Contain("AfterlifeCardSystem.TelegraphBonusSeconds"));

            string pm = Src("Scripts/Systems/PlayerMovement.cs");
            Assert.That(pm, Does.Contain("AfterlifeCardSystem.RollDistanceFactor"));

            string orb = Src("Scripts/Systems/AfterlifeOrb.cs");
            Assert.That(orb, Does.Contain("public static System.Collections.Generic.IReadOnlyList<AfterlifeOrb> Live"));
        }
    }
}
