using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 3A contract: GuardPlaceholder.TryHoldEngage() target selection and attack gating.
    /// Test-only: targets are FakeHoldDamageable (no real GuardPlaceholder target, no HP/VFX/Die side effects).
    /// TryHoldEngage is private and invoked by reflection. Production code is not modified.
    /// </summary>
    public class GuardHoldEngageTests
    {
        private const float HoldRange = 2.2f;   // ATTACK_MELEE_RANGE mirror (production const is private)
        private const float HoldCooldown = 1.2f; // ATTACK_COOLDOWN_SECONDS mirror

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private GuardPlaceholder _guard;

        [SetUp]
        public void SetUp()
        {
            _guard = Spawn("HoldEngage_Guard", Vector3.zero).AddComponent<GuardPlaceholder>();
            _guard.SetGuardInfo("홀드반격테스트병사", 1, NationType.East);
            _guard.SetHold(true);
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
        public void TryHoldEngage_NearestEligibleAliveTarget_IsCalled()
        {
            var near = SpawnFake("HoldEngage_Near", new Vector3(1.0f, 0f, 0f));
            var far = SpawnFake("HoldEngage_Far", new Vector3(0f, 0f, 1.8f));
            Physics.SyncTransforms();

            InvokeTryHoldEngage();

            Assert.That(near.HitCount, Is.EqualTo(1), "Nearest alive target in range must be attacked once.");
            Assert.That(far.HitCount, Is.EqualTo(0), "Farther target must not be attacked when a nearer one is eligible.");
            Assert.That(near.LastAmount, Is.GreaterThanOrEqualTo(0f));
            Assert.That(near.LastWeaponType, Is.EqualTo("guard"), "PerformAttack must pass weaponType \"guard\".");
        }

        [Test]
        public void TryHoldEngage_OutOfRangeFake_IsNotHit()
        {
            var outOfRange = SpawnFake("HoldEngage_OutOfRange", new Vector3(HoldRange + 1.0f, 0f, 0f));
            Physics.SyncTransforms();

            InvokeTryHoldEngage();

            Assert.That(outOfRange.HitCount, Is.EqualTo(0), "Target beyond the 2.2m hold radius must not be attacked.");
            Assert.That(outOfRange.LastAmount, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void TryHoldEngage_DeadFake_IsIgnored_AliveFartherTargetIsHit()
        {
            var dead = SpawnFake("HoldEngage_Dead", new Vector3(0.5f, 0f, 0f));
            dead.IsAliveOverride = false;
            var alive = SpawnFake("HoldEngage_AliveFar", new Vector3(0f, 0f, 1.5f));
            Physics.SyncTransforms();

            InvokeTryHoldEngage();

            Assert.That(dead.HitCount, Is.EqualTo(0), "IsAlive=false target must be skipped even when nearest.");
            Assert.That(alive.HitCount, Is.EqualTo(1), "Alive target in range must be attacked after dead nearer target is skipped.");
        }

        [Test]
        public void TryHoldEngage_ImmediateSecondCall_IsBlockedByCooldown()
        {
            var target = SpawnFake("HoldEngage_Cooldown", new Vector3(1.0f, 0f, 0f));
            Physics.SyncTransforms();

            InvokeTryHoldEngage();
            Assert.That(target.HitCount, Is.EqualTo(1), "First engage must hit.");
            Assert.That(GetAttackCooldown(), Is.EqualTo(HoldCooldown).Within(1e-5f), "Hit must arm the 1.2s cooldown.");

            InvokeTryHoldEngage();

            Assert.That(target.HitCount, Is.EqualTo(1), "Immediate second call must not hit while cooldown is active.");
        }

        // ----- helpers -----

        private FakeHoldDamageable SpawnFake(string name, Vector3 position)
        {
            var go = Spawn(name, position);
            go.AddComponent<BoxCollider>().size = Vector3.one * 0.5f;
            return go.AddComponent<FakeHoldDamageable>();
        }

        private GameObject Spawn(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _spawned.Add(go);
            return go;
        }

        private void InvokeTryHoldEngage()
        {
            MethodInfo m = typeof(GuardPlaceholder).GetMethod("TryHoldEngage",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(m, Is.Not.Null, "GuardPlaceholder.TryHoldEngage() must exist.");
            m.Invoke(_guard, null);

            // ----- [61차 회귀 수리 — 사유] -----
            // TryHoldEngage에 "신규 타겟 최초 감지 0.4s 관찰 딜레이" 계약이 추가되었다(HoldEngageDelayTests 참조).
            //   이는 기존 "반격 성립" 계약과 별개의 새 행동 계약이며, 기존 4건은 첫 호출 즉시 1타를 어서션하기 때문에
            //   딜레이 미반영 시 전부 실패한다(RED 실측 확인). 기존 어서션(첫 호출 1타)을 유지하기 위해 헬퍼에서
            //   ①첫 호출(새 타겟 감지 → 딜레이 진입, 무타격) → ②리플렉션으로 _holdNewTargetDelay=0(0.4s 경과 시뮬)
            //   → ③재호출(동일 타겟 재무장 없음 → 쿨다운 게이트 통과 시 1타) 순서로 우회한다.
            //   헬퍼 1회 호출 = 최대 1타(③에서 쿨다운 장전) — 기존 어서션의 호출 횟수/쿨다운 기대값과 정합.
            FieldInfo delay = typeof(GuardPlaceholder).GetField("_holdNewTargetDelay",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (delay != null) delay.SetValue(_guard, 0f);
            m.Invoke(_guard, null);
        }

        private float GetAttackCooldown()
        {
            FieldInfo f = typeof(GuardPlaceholder).GetField("_attackCooldown",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, "GuardPlaceholder._attackCooldown field must exist.");
            return (float)f.GetValue(_guard);
        }
    }

    /// <summary>
    /// Test-only IDamageable target. Records hits; no VFX, no death, no HP mutation.
    /// </summary>
    public class FakeHoldDamageable : MonoBehaviour, IDamageable
    {
        public int HitCount;
        public float LastAmount;
        public string LastWeaponType;
        public bool IsAliveOverride = true;

        public float CurrentHP => IsAliveOverride ? 100f : 0f;
        public float MaxHP => 100f;
        public bool IsDead => !IsAliveOverride;
        public bool IsAlive => IsAliveOverride;

        public void TakeDamage(DamageInfo damageInfo)
        {
            HitCount++;
            LastAmount = damageInfo.amount;
        }

        public void TakeDamage(float amount, Vector3 hitDirection, string weaponType = "melee")
        {
            HitCount++;
            LastAmount = amount;
            LastWeaponType = weaponType;
        }
    }
}
