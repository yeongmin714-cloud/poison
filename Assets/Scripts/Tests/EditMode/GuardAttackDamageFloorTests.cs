using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [56차 후속 — "적 체력이 안 준다" 실측 수리] PerformAttack 데미지 하한 계약:
    ///   damage = Mathf.Max(GetAttack(), level). 근거(Editor.log Test_16 세션 실측): 무장비 병사
    ///   (테스트 씬 GuardEquipmentSystem 미부팅)의 스탯 롤이 Lv8에서 dmg=1.0까지 붕괴 — 타당 체력바가
    ///   타당 4%라 가시 데미지가 성립하지 않았다. 하한 = level 복원으로 무장비/저롤 병사도
    ///   level 이상의 데미지를 보장하고, 롤/무기 보너스가 level 이상이면 롤값을 그대로 유지한다.
    /// PerformAttack은 private — 리플렉션 호출(기존 GuardHoldEngageTests 패턴). FakeHoldDamageable 재사용.
    /// </summary>
    public class GuardAttackDamageFloorTests
    {
        private const int TestLevel = 8;
        private const int RollAttempts = 200;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private GuardPlaceholder _attacker;

        [SetUp]
        public void SetUp()
        {
            _attacker = Spawn("DamageFloor_Guard", Vector3.zero).AddComponent<GuardPlaceholder>();
            _attacker.SetGuardInfo("데미지하한테스트병사", TestLevel, NationType.East);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            // PerformAttack -> CombatVFXController.ShowDamageNumber spawns attacker-side "DamageNumber" objects.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name == "DamageNumber") Object.DestroyImmediate(t.gameObject);
            }
        }

        [Test]
        public void PerformAttack_LowRoll_DealsAtLeastLevelDamage()
        {
            // 스탯 롤이 하한(level=8) 미만이 되도록 강제(랜덤 — 재시도 후 미달 시 Inconclusive로 건너뜀).
            if (!TryForceAttackRoll(minAttack: int.MinValue, maxAttack: TestLevel - 1))
                Assert.Inconclusive($"Lv.{TestLevel} 공격 롤 {TestLevel - 1} 이하 미달(랜덤) — 하한 계약은 케이스 2(롤 보존)와 함께 종합 판정.");

            var target = Spawn("DamageFloor_Target", new Vector3(1f, 0f, 0f));
            var fake = target.AddComponent<FakeHoldDamageable>();
            InvokePerformAttack(fake);

            Assert.That(fake.HitCount, Is.EqualTo(1), "PerformAttack must hit the passed target exactly once.");
            Assert.That(fake.LastAmount, Is.GreaterThanOrEqualTo(TestLevel),
                "무장비+저롤 병사의 데미지도 level 하한을 보장해야 한다(적 체력바 가시 감소 계약).");
        }

        [Test]
        public void PerformAttack_RollAtOrAboveLevel_KeepsRolledDamage()
        {
            if (!TryForceAttackRoll(minAttack: TestLevel, maxAttack: int.MaxValue))
                Assert.Inconclusive($"Lv.{TestLevel} 공격 롤 {TestLevel} 이상 미달(랜덤).");

            var target = Spawn("DamageFloor_Target2", new Vector3(1f, 0f, 0f));
            var fake = target.AddComponent<FakeHoldDamageable>();
            InvokePerformAttack(fake);

            Assert.That(fake.LastAmount, Is.EqualTo(_attacker.GetStatAttack()).Within(1e-5f),
                "롤이 level 이상이면 하한은 롤값을 덮어쓰지 않는다(무장비 가정 — GetAttack = 순수 롤).");
        }

        // ----- helpers -----

        private bool TryForceAttackRoll(int minAttack, int maxAttack)
        {
            for (int i = 0; i < RollAttempts; i++)
            {
                _attacker.RollStats();
                int atk = _attacker.GetStatAttack();
                if (atk >= minAttack && atk <= maxAttack) return true;
            }
            return false;
        }

        private GameObject Spawn(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        private void InvokePerformAttack(IDamageable target)
        {
            MethodInfo m = typeof(GuardPlaceholder).GetMethod("PerformAttack",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(m, Is.Not.Null, "GuardPlaceholder.PerformAttack(IDamageable) must exist.");
            m.Invoke(_attacker, new object[] { target });
        }
    }
}
