using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

/// <summary>
/// Test IAggroable implementation for unit testing MonsterAggroSystem.
/// </summary>
public class TestAggroable : MonoBehaviour, IAggroable
{
    public string monsterType;
    public GameObject aggroTarget;
    public AggroState aggroState = AggroState.Idle;
    private float _alertTimer;
    private float _cooldownTimer;

    public void SetAggroTarget(GameObject target)
    {
        aggroTarget = target;
        if (aggroState == AggroState.Idle)
        {
            aggroState = AggroState.Alert;
            _alertTimer = 0f;
        }
        else if (aggroState == AggroState.Cooldown)
        {
            aggroState = AggroState.Combat;
        }
    }

    public void ClearAggro()
    {
        aggroTarget = null;
        aggroState = AggroState.Cooldown;
        _cooldownTimer = 0f;
    }

    public bool IsInCombat => aggroState == AggroState.Combat || aggroState == AggroState.Alert;
    public string MonsterType => monsterType;
    public AggroState CurrentAggroState => aggroState;
    public GameObject AggroTarget => aggroTarget;

    public void UpdateAggroTimer(float deltaTime)
    {
        switch (aggroState)
        {
            case AggroState.Alert:
                _alertTimer += deltaTime;
                if (_alertTimer >= 3f)
                {
                    aggroState = AggroState.Combat;
                }
                break;
            case AggroState.Cooldown:
                _cooldownTimer += deltaTime;
                if (_cooldownTimer >= 5f)
                {
                    aggroState = AggroState.Idle;
                }
                break;
        }
    }
}

public class MonsterAggroTests
{
    private GameObject _systemGo;
    private MonsterAggroSystem _system;

    [SetUp]
    public void SetUp()
    {
        MonsterAggroSystem.ResetInstance();
        _systemGo = new GameObject("AggroSystem");
        _system = _systemGo.AddComponent<MonsterAggroSystem>();
    }

    [TearDown]
    public void TearDown()
    {
        MonsterAggroSystem.ResetInstance();
        if (_systemGo != null)
            Object.DestroyImmediate(_systemGo);
    }

    private (GameObject attacker, GameObject victim, TestAggroable victimAggro) CreateAttackSetup()
    {
        var attacker = new GameObject("Attacker");
        var victim = new GameObject("Victim");
        var victimAggro = victim.AddComponent<TestAggroable>();
        victimAggro.monsterType = "wolf";
        return (attacker, victim, victimAggro);
    }

    private TestAggroable CreateNearbyMonster(string type, Vector3 position)
    {
        var go = new GameObject("Nearby_" + type);
        go.transform.position = position;
        var agg = go.AddComponent<TestAggroable>();
        agg.monsterType = type;
        _system.RegisterMonster(agg);
        return agg;
    }

    [Test]
    public void RegisterMonster_AddsToSystem()
    {
        var (_, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);
        Assert.AreEqual(1, _system.MonsterCount);
    }

    [Test]
    public void RegisterMonster_Null_DoesNothing()
    {
        _system.RegisterMonster(null);
        Assert.AreEqual(0, _system.MonsterCount);
    }

    [Test]
    public void NotifyAttack_VictimGetsAggro()
    {
        var (attacker, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);
        _system.NotifyAttack(victim, attacker);
        Assert.IsTrue(victimAggro.IsInCombat);
        Assert.AreEqual(attacker, victimAggro.aggroTarget);
    }

    [Test]
    public void NotifyAttack_NearbySameTypeJoins()
    {
        var (attacker, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);

        // Nearby wolf (5m away - within 10m range)
        var nearby = CreateNearbyMonster("wolf", victim.transform.position + new Vector3(5, 0, 0));

        _system.NotifyAttack(victim, attacker);
        Assert.IsTrue(nearby.IsInCombat, "Nearby same-type monster should join aggro");
    }

    [Test]
    public void NotifyAttack_FarSameType_DoesNotJoin()
    {
        var (attacker, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);

        // Far wolf (11m away - outside 10m range)
        var far = CreateNearbyMonster("wolf", victim.transform.position + new Vector3(11, 0, 0));

        _system.NotifyAttack(victim, attacker);
        Assert.IsFalse(far.IsInCombat, "Far monster should NOT join aggro");
    }

    [Test]
    public void NotifyAttack_DifferentType_DoesNotJoin()
    {
        var (attacker, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);

        // Nearby boar (different type) - should not join
        var boar = CreateNearbyMonster("boar", victim.transform.position + new Vector3(3, 0, 0));

        _system.NotifyAttack(victim, attacker);
        Assert.IsFalse(boar.IsInCombat, "Different-type monster should NOT join aggro");
    }

    [Test]
    public void NotifyAttack_AlreadyInCombat_DoesNotReAggro()
    {
        var (attacker, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);

        var secondAttacker = new GameObject("SecondAttacker");

        // First attack - victim enters combat
        _system.NotifyAttack(victim, attacker);
        Assert.IsTrue(victimAggro.IsInCombat);
        Assert.AreEqual(attacker, victimAggro.aggroTarget);

        // Second attack - target should not change (already in combat)
        _system.NotifyAttack(victim, secondAttacker);
        Assert.AreEqual(attacker, victimAggro.aggroTarget);
    }

    [Test]
    public void UnregisterMonster_RemovesFromSystem()
    {
        var (_, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);
        Assert.AreEqual(1, _system.MonsterCount);
        _system.UnregisterMonster(victimAggro);
        Assert.AreEqual(0, _system.MonsterCount);
    }

    [Test]
    public void ClearAggro_TransitionsToCooldown()
    {
        var (attacker, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);
        _system.NotifyAttack(victim, attacker);

        // Manually clear
        victimAggro.ClearAggro();
        Assert.AreEqual(AggroState.Cooldown, victimAggro.CurrentAggroState);
        Assert.IsNull(victimAggro.aggroTarget);
    }

    [Test]
    public void UpdateAggroTimer_AlertToCombat_After3Seconds()
    {
        var (attacker, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);
        _system.NotifyAttack(victim, attacker);
        Assert.AreEqual(AggroState.Alert, victimAggro.CurrentAggroState);

        // Simulate 3 seconds
        victimAggro.UpdateAggroTimer(3.5f);
        Assert.AreEqual(AggroState.Combat, victimAggro.CurrentAggroState);
    }

    [Test]
    public void UpdateAggroTimer_CooldownToIdle_After5Seconds()
    {
        var (attacker, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);
        _system.NotifyAttack(victim, attacker);

        // Enter combat then clear
        victimAggro.UpdateAggroTimer(3.5f);
        victimAggro.ClearAggro();
        Assert.AreEqual(AggroState.Cooldown, victimAggro.CurrentAggroState);

        // Simulate 5 seconds cooldown
        victimAggro.UpdateAggroTimer(5.5f);
        Assert.AreEqual(AggroState.Idle, victimAggro.CurrentAggroState);
    }

    [Test]
    public void GetMonsterType_FromGameObject()
    {
        var (_, victim, victimAggro) = CreateAttackSetup();
        string type = MonsterAggroSystem.GetMonsterType(victim);
        Assert.AreEqual("wolf", type);
    }

    [Test]
    public void GetMonsterType_Null_ReturnsNull()
    {
        string type = MonsterAggroSystem.GetMonsterType(null);
        Assert.IsNull(type);
    }

    [Test]
    public void NotifyAttack_MultipleMonsters_AllJoin()
    {
        var (attacker, victim, victimAggro) = CreateAttackSetup();
        _system.RegisterMonster(victimAggro);

        var nearby1 = CreateNearbyMonster("wolf", victim.transform.position + new Vector3(2, 0, 0));
        var nearby2 = CreateNearbyMonster("wolf", victim.transform.position + new Vector3(4, 0, 2));
        var far = CreateNearbyMonster("wolf", victim.transform.position + new Vector3(12, 0, 0));

        _system.NotifyAttack(victim, attacker);

        Assert.IsTrue(nearby1.IsInCombat, "nearby1 should join");
        Assert.IsTrue(nearby2.IsInCombat, "nearby2 should join");
        Assert.IsFalse(far.IsInCombat, "far should NOT join");
    }
}
