using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Guard hold cooldown lifecycle: _attackCooldown must tick down while the guard is holding.
    /// Root cause: Update() called ExecuteMovement(), which returned early when !_hasCommand (always true
    /// while holding, since SetHold(true) calls ClearCommand()), so the cooldown never decremented.
    /// EditMode Time.deltaTime is 0, so the decrement is verified through the pure TickAttackCooldown helper.
    /// </summary>
    public class GuardHoldCooldownTickTests
    {
        private const string GuardSource = "Assets/Scripts/Systems/GuardPlaceholder.cs";
        private const string TickCall = "if (!_hasCommand) _attackCooldown = TickAttackCooldown(_attackCooldown, Time.deltaTime);";

        private static float Tick(float cooldown, float delta)
        {
            MethodInfo m = typeof(GuardPlaceholder).GetMethod("TickAttackCooldown",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.That(m, Is.Not.Null, "GuardPlaceholder.TickAttackCooldown(float, float) static helper must exist.");
            return (float)m.Invoke(null, new object[] { cooldown, delta });
        }

        [Test]
        public void TickAttackCooldown_SubtractsSuppliedDelta()
        {
            Assert.That(Tick(1.2f, 0.5f), Is.EqualTo(0.7f).Within(1e-5f));
        }

        [Test]
        public void TickAttackCooldown_ZeroDeltaLeavesCooldownUnchanged()
        {
            Assert.That(Tick(1.2f, 0f), Is.EqualTo(1.2f).Within(1e-5f));
        }

        [Test]
        public void TickAttackCooldown_ClampsAtZero_NoOvershoot()
        {
            Assert.That(Tick(0.3f, 1.0f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void TickAttackCooldown_ReadyCooldownStaysZero()
        {
            Assert.That(Tick(0f, 0.5f), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void TickAttackCooldown_HoldFramesRecoverCooldownForCounterattack()
        {
            // 1.2s cooldown ticked over 0.2s hold frames must reach <= 0 so TryHoldEngage can strike again.
            float cd = 1.2f;
            for (int i = 0; i < 5; i++) cd = Tick(cd, 0.2f);
            Assert.That(cd, Is.GreaterThan(0f), "Still cooling after 1.0s.");
            cd = Tick(cd, 0.2f);
            // float accumulation leaves ~3e-8 residue after 6 steps of 0.2 — treat as ready (within 1e-4s).
            Assert.That(cd, Is.LessThanOrEqualTo(1e-4f), "Cooldown must be ready after 1.2s of hold frames.");
        }

        [Test]
        public void Update_TicksCooldownOnlyWhenNoCommand_BeforeExecuteMovement()
        {
            string src = System.IO.File.ReadAllText(GuardSource);
            int updateStart = src.IndexOf("private void Update()", System.StringComparison.Ordinal);
            Assert.That(updateStart, Is.GreaterThan(-1), "Update() must exist.");

            int tickIdx = src.IndexOf(TickCall, updateStart, System.StringComparison.Ordinal);
            int execIdx = src.IndexOf("ExecuteMovement();", updateStart, System.StringComparison.Ordinal);
            Assert.That(tickIdx, Is.GreaterThan(-1),
                "Update() must tick _attackCooldown when no command (hold) — single decrement, no double-tick with command.");
            Assert.That(execIdx, Is.GreaterThan(tickIdx),
                "Hold tick must run before the ExecuteMovement() early return.");
        }

        [Test]
        public void ExecuteMovement_StillSingleCooldownDecrement()
        {
            // Guards against double-decrement: the command-path decrement remains the only one.
            string src = System.IO.File.ReadAllText(GuardSource);
            const string decrement = "if (_attackCooldown > 0f) _attackCooldown -= delta;";
            int first = src.IndexOf(decrement, System.StringComparison.Ordinal);
            Assert.That(first, Is.GreaterThan(-1), "Command-path decrement must exist.");
            Assert.That(src.IndexOf(decrement, first + 1, System.StringComparison.Ordinal), Is.EqualTo(-1),
                "Command-path decrement must appear exactly once.");
        }
    }
}
