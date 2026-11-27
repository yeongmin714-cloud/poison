using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [61차 TDD] 홀드 반격 초기 딜레이 계약 — GuardPlaceholder.TryHoldEngage()가 새로 감지한 타겟에 대해
    /// 즉시 반격하지 않고 0.4s 관찰 딜레이를 거친 뒤 타격해야 한다.
    /// 근거: Test_16 실측 — 홀드 수비병이 도착 즉시(≤0.2s) 반격 선공 → 동률 HP 대결에서 병사 3타 전 사망.
    /// TryHoldEngage는 private(리플렉션 호출), _holdNewTargetDelay/_attackCooldown는 private 필드(리플렉션 검증).
    /// </summary>
    public class HoldEngageDelayTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private GuardPlaceholder _guard;
        private MethodInfo _tryHoldEngage;
        private FieldInfo _delayField;
        private FieldInfo _cooldownField;

        [SetUp]
        public void SetUp()
        {
            _guard = Spawn("HoldDelay_Guard", Vector3.zero).AddComponent<GuardPlaceholder>();
            _guard.SetGuardInfo("홀드딜레이테스트병사", 1, NationType.East);
            _guard.SetHold(true);

            _tryHoldEngage = typeof(GuardPlaceholder).GetMethod("TryHoldEngage",
                BindingFlags.NonPublic | BindingFlags.Instance);
            _delayField = typeof(GuardPlaceholder).GetField("_holdNewTargetDelay",
                BindingFlags.NonPublic | BindingFlags.Instance);
            _cooldownField = typeof(GuardPlaceholder).GetField("_attackCooldown",
                BindingFlags.NonPublic | BindingFlags.Instance);
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
        public void HoldEngage_NewTarget_FirstCallDelayed_NoHit()
        {
            var target = SpawnFake("HoldDelay_Target", new Vector3(1.0f, 0f, 0f));
            Physics.SyncTransforms();

            InvokeTryHoldEngage();

            Assert.That(target.HitCount, Is.EqualTo(0),
                "New-target first detection must enter the 0.4s observation delay — no immediate strike.");
            Assert.That(GetDelay(), Is.GreaterThan(0f),
                "First detection must arm _holdNewTargetDelay (>0).");
        }

        [Test]
        public void HoldEngage_DelayElapsed_ThenHits()
        {
            var target = SpawnFake("HoldDelay_Target", new Vector3(1.0f, 0f, 0f));
            Physics.SyncTransforms();

            // 1차 호출 — 새 타겟 감지로 딜레이 진입(타격 없음)이 전제조건.
            InvokeTryHoldEngage();
            Assert.That(target.HitCount, Is.EqualTo(0), "precondition: first call is delayed, no strike yet.");

            // 0.4s 경과를 리플렉션으로 시뮬(EditMode — deltaTime 조작 불가) 후 재호출.
            // 같은 타겟 재감지이므로 딜레이 재무장 없음 → 즉시 1타 + 표준 쿨다운 장전.
            SetDelay(0f);
            InvokeTryHoldEngage();

            Assert.That(target.HitCount, Is.EqualTo(1),
                "After the observation delay elapses, the same target must be struck.");
            Assert.That(GetCooldown(), Is.EqualTo(1.2f).Within(1e-4f),
                "Strike must arm the standard 1.2s attack cooldown.");
        }

        [Test]
        public void HoldEngage_SameTarget_SecondCycle_NoExtraDelay()
        {
            var target = SpawnFake("HoldDelay_Target", new Vector3(1.0f, 0f, 0f));
            Physics.SyncTransforms();

            // 상태 재현(테스트 2와 동일): 첫 감지(딜레이 진입, 무타격) → 딜레이 경과 시뮬 → 1타(쿨다운 1.2s 장전).
            InvokeTryHoldEngage();
            SetDelay(0f);
            InvokeTryHoldEngage();
            Assert.That(target.HitCount, Is.EqualTo(1), "precondition: one strike after the delay elapsed.");

            // 쿨다운 잔여 동안 재호출 2회 — TryHoldEngage 첫 줄의 쿨다운 게이트가 최우선이라 추가 타격 없음.
            // (구조상 쿨다운 return이 딜레이 판정보다 앞서므로, 이 구간에서 딜레이 재무장도 발생할 수 없다.)
            InvokeTryHoldEngage();
            InvokeTryHoldEngage();
            Assert.That(target.HitCount, Is.EqualTo(1),
                "Cooldown armed by the strike gates subsequent cycles — no extra hits while active.");
            Assert.That(GetDelay(), Is.EqualTo(0f),
                "No delay re-arm may occur while the cooldown return short-circuits the scan.");

            // 동일 타겟 지속 교전 2사이클에서 딜레이가 재무장되지 않음을 직접 검증:
            // 쿨다운 경과를 리플렉션으로 시뮬(0)하고 재호출 → 같은 타겟 재감지에도 0.4s 딜레이 없이 즉시 2타.
            SetCooldown(0f);
            InvokeTryHoldEngage();
            Assert.That(target.HitCount, Is.EqualTo(2),
                "A known (same) target must NOT re-arm the 0.4s delay on later cycles — strike proceeds immediately.");
            Assert.That(GetDelay(), Is.EqualTo(0f),
                "Same-target re-detection must leave _holdNewTargetDelay at 0.");
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
            Assert.That(_tryHoldEngage, Is.Not.Null, "GuardPlaceholder.TryHoldEngage() must exist.");
            _tryHoldEngage.Invoke(_guard, null);
        }

        private float GetDelay()
        {
            Assert.That(_delayField, Is.Not.Null,
                "GuardPlaceholder._holdNewTargetDelay field must exist ([61차] 0.4s delay contract).");
            return (float)_delayField.GetValue(_guard);
        }

        private void SetDelay(float value)
        {
            Assert.That(_delayField, Is.Not.Null,
                "GuardPlaceholder._holdNewTargetDelay field must exist ([61차] 0.4s delay contract).");
            _delayField.SetValue(_guard, value);
        }

        private float GetCooldown()
        {
            Assert.That(_cooldownField, Is.Not.Null, "GuardPlaceholder._attackCooldown field must exist.");
            return (float)_cooldownField.GetValue(_guard);
        }

        private void SetCooldown(float value)
        {
            Assert.That(_cooldownField, Is.Not.Null, "GuardPlaceholder._attackCooldown field must exist.");
            _cooldownField.SetValue(_guard, value);
        }
    }
}