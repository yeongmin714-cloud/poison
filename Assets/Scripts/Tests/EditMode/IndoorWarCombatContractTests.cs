using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>Test_16 양측 진영의 적대 판정 및 공격 대상별 HP 변경 계약.</summary>
    public class IndoorWarCombatContractTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private bool _indoorActiveBefore;

        [SetUp]
        public void SetUp()
        {
            // 실내 게이트 정적 상태 유출 방지(다른 테스트 클래스가 true로 남겨도 본 클래스는 실외 기준으로 검증).
            _indoorActiveBefore = ProjectName.Core.UITransitionState.IndoorActive;
            ProjectName.Core.UITransitionState.IndoorActive = false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
            ProjectName.Core.UITransitionState.IndoorActive = _indoorActiveBefore;
        }

        [Test]
        public void OpposingNations_AreMutualEnemies_ButSameNationIsNot()
        {
            var east = SpawnGuard("IndoorContract_East", NationType.East);
            var west = SpawnGuard("IndoorContract_West", NationType.West);
            var otherEast = SpawnGuard("IndoorContract_East2", NationType.East);
            bool eastHostility = east.IsEnemyGuard(west);
            bool westHostility = west.IsEnemyGuard(east);
            bool alliedHostility = east.IsEnemyGuard(otherEast);

            Assert.IsTrue(eastHostility, "플레이어 진영은 적 영지 병사를 적으로 판정해야 한다.");
            Assert.IsTrue(westHostility, "적 수비군은 플레이어 진영 병사를 적으로 판정해야 한다.");
            Assert.IsFalse(alliedHostility, "같은 국가 병사는 적으로 판정하면 안 된다.");
        }

        [Test]
        public void AttackOrders_ApplyDamageOnlyToTheirResolvedEnemyTarget()
        {
            var attacker = SpawnGuard("IndoorContract_Attacker", NationType.East);
            var enemy = SpawnGuard("IndoorContract_Enemy", NationType.West);
            var friendly = SpawnGuard("IndoorContract_Friendly", NationType.East);
            attacker.transform.position = new Vector3(-8f, 0f, 0f);
            enemy.transform.position = new Vector3(2f, 0f, 0f);
            friendly.transform.position = new Vector3(0f, 0f, 1.5f);
            attacker.SetCommandTarget(Vector3.zero, true);
            var beforeEnemy = enemy.CurrentHP;
            var beforeFriendly = friendly.CurrentHP;

            attacker.ExecuteMovement();
            var resolved = attacker.CurrentAttackTarget;
            Physics.SyncTransforms();
            attacker.transform.position = enemy.transform.position + Vector3.left * 1.5f;
            SetPrivateField(attacker, "_attackCooldown", 0f);
            attacker.ExecuteMovement();

            Assert.AreSame(enemy, resolved, "명령 위치에서 적 병사만 타겟으로 획득되어야 한다.");
            Assert.Less(enemy.CurrentHP, beforeEnemy, "사정거리 내 공격 명령은 지정된 적 HP만 감소시켜야 한다.");
            Assert.AreEqual(beforeFriendly, friendly.CurrentHP, "동국가 아군이 공격 대상이 되면 안 된다.");
        }

        [Test]
        public void HoldDefender_DoesNotDamageSameNationGuardEvenWhenBothAreHostileToPlayer()
        {
            var defender = SpawnGuard("IndoorContract_WestDefender", NationType.West);
            defender.HostileToPlayerFaction = true;
            SetPrivateField(defender, "_attackCooldown", 0f);
            defender.SetHold(true);

            var sameNationObject = new GameObject("IndoorContract_WestAlly");
            _objects.Add(sameNationObject);
            sameNationObject.transform.position = Vector3.right;
            sameNationObject.AddComponent<BoxCollider>().size = Vector3.one * 0.5f;
            var sameNationGuard = sameNationObject.AddComponent<GuardPlaceholder>();
            sameNationGuard.SetGuardInfo("same-nation", 1, NationType.West);
            sameNationGuard.SetRecruited(false);
            sameNationGuard.HostileToPlayerFaction = true;
            // [픽스처 보완] EditMode AddComponent 경로는 Start()가 실행되지 않아 _currentHP가 0으로 남는다.
            // 유효한 현재/최대 HP를 명시 초기화하고 TryHoldEngage 전후 '변화 없음'으로 단정한다(절대값 100 단정 제거).
            sameNationGuard.SetHP(sameNationGuard.MaxHP);
            var beforeAllyHP = sameNationGuard.CurrentHP;
            Assert.Greater(beforeAllyHP, 0f,
                "fixture: ally must start with valid positive HP for the no-damage assertion to be meaningful.");
            Physics.SyncTransforms();

            var engage = typeof(GuardPlaceholder).GetMethod("TryHoldEngage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(engage);
            engage.Invoke(defender, null);

            Assert.AreEqual(beforeAllyHP, sameNationGuard.CurrentHP,
                "HostileToPlayerFaction describes hostility toward the player, not permission for same-nation friendly fire.");
        }

        [Test]
        public void ExplicitAttackOrder_ReacquiresAnotherEnemyWhenClickedEnemyDies()
        {
            var attacker = SpawnGuard("IndoorContract_Attacker", NationType.East);
            attacker.transform.position = new Vector3(-8f, 0f, 0f);
            var focused = SpawnGuard("IndoorContract_FocusedEnemy", NationType.West);
            focused.transform.position = new Vector3(2f, 0f, 0f);
            focused.gameObject.AddComponent<SphereCollider>();
            var next = SpawnGuard("IndoorContract_NextEnemy", NationType.West);
            next.transform.position = new Vector3(1f, 0f, 2f);
            next.gameObject.AddComponent<SphereCollider>();
            Physics.SyncTransforms();

            attacker.SetExplicitAttackTarget(focused, focused.transform.position);
            attacker.ExecuteMovement();
            Assert.AreSame(focused, attacker.CurrentAttackTarget);

            focused.gameObject.SetActive(false);
            Physics.SyncTransforms();
            attacker.ExecuteMovement();

            Assert.IsTrue(attacker.HasCommand, "eliminating a focused defender must not clear the attack order");
            Assert.AreSame(next, attacker.CurrentAttackTarget,
                "the surviving hostile defender must be reacquired after the clicked target dies");
        }

        [Test]
        public void SpawnTimeAutoEngagement_AssignsDistinctEnemyPerFriendly()
        {
            List<GuardPlaceholder> friends;
            List<GuardPlaceholder> enemies;
            BuildIndoorWarLineup(out friends, out enemies);

            IndoorWarTestSetup.AssignSpawnTimeEngagement(friends, enemies);

            for (int i = 0; i < friends.Count; i++)
            {
                friends[i].ExecuteMovement();
                Assert.IsTrue(friends[i].HasCommand, $"내 병사 {i}는 스폰 즉시 공격 명령을 보유해야 한다.");
                Assert.IsTrue(friends[i].IsAttackCommand, $"내 병사 {i}의 스폰 명령은 공격 명령이어야 한다.");
                Assert.AreSame(enemies[i], friends[i].CurrentAttackTarget,
                    $"내 병사 {i}는 인덱스 순서대로 결정적 1:1 적에게 고정되어야 한다.");
            }

            Assert.AreNotSame(friends[0].CurrentAttackTarget, friends[1].CurrentAttackTarget,
                "두 병사가 같은 적을 초기 타겟으로 삼으면 안 된다(부하 분산 1:1 배정).");
            Assert.AreNotSame(friends[1].CurrentAttackTarget, friends[2].CurrentAttackTarget,
                "두 병사가 같은 적을 초기 타겟으로 삼으면 안 된다(부하 분산 1:1 배정).");
            Assert.AreNotSame(friends[0].CurrentAttackTarget, friends[2].CurrentAttackTarget,
                "두 병사가 같은 적을 초기 타겟으로 삼으면 안 된다(부하 분산 1:1 배정).");
        }

        [Test]
        public void SpawnTimeAutoEngagement_KeepsOrderAndRetargetsSurvivorWhenInitialTargetDies()
        {
            List<GuardPlaceholder> friends;
            List<GuardPlaceholder> enemies;
            BuildIndoorWarLineup(out friends, out enemies);

            IndoorWarTestSetup.AssignSpawnTimeEngagement(friends, enemies);
            for (int i = 0; i < friends.Count; i++)
                friends[i].ExecuteMovement();
            Assert.AreSame(enemies[0], friends[0].CurrentAttackTarget);

            enemies[0].gameObject.SetActive(false);
            Physics.SyncTransforms();
            friends[0].ExecuteMovement();

            Assert.IsTrue(friends[0].HasCommand, "초기 타겟 사망 후에도 스폰 시점 공격 명령은 유지되어야 한다.");
            Assert.AreSame(enemies[1], friends[0].CurrentAttackTarget,
                "재탐색 반경(2.5m) 내 유일 생존 적(2.4m 간격 배치)이 새 타겟이 되어야 한다.");
        }

        [Test]
        public void SpawnTimeAutoEngagement_ManualCommandReplacesAutoOrderWithoutReassertion()
        {
            List<GuardPlaceholder> friends;
            List<GuardPlaceholder> enemies;
            BuildIndoorWarLineup(out friends, out enemies);

            IndoorWarTestSetup.AssignSpawnTimeEngagement(friends, enemies);
            for (int i = 0; i < friends.Count; i++)
                friends[i].ExecuteMovement();
            Assert.AreSame(enemies[0], friends[0].CurrentAttackTarget);

            // 수동 이동 명령 — 스폰 시점 자동 공격 명령을 대체한다.
            Vector3 manualMove = new Vector3(0f, 0f, -14f);
            friends[0].SetCommandTarget(manualMove, false);
            Assert.IsFalse(friends[0].IsAttackCommand, "수동 이동 명령은 자동 공격 명령을 대체해야 한다.");
            Assert.IsNull(friends[0].CurrentAttackTarget, "수동 명령 직후 자동 지정 타겟이 표시되어서는 안 된다.");

            friends[0].ExecuteMovement();
            friends[0].ExecuteMovement();
            Assert.IsTrue(friends[0].HasCommand, "수동 명령이 명령 상태를 유지해야 한다.");
            Assert.IsNull(friends[0].CurrentAttackTarget, "수동 이동 명령 후 자동 지정 적이 재주장되어서는 안 된다.");

            // 수동 공격 명령(적 없는 지점 앵커) — 이 경우에도 자동 지정 적이 되살아나지 않는다.
            Vector3 manualAttackAnchor = new Vector3(-14f, 0f, 10f);
            friends[0].SetCommandTarget(manualAttackAnchor, true);
            friends[0].ExecuteMovement();
            Assert.IsTrue(friends[0].IsAttackCommand, "수동 공격 명령이 유지되어야 한다.");
            Assert.AreEqual(manualAttackAnchor, friends[0].CommandTarget, "수동 명령 앵커가 유지되어야 한다.");
            Assert.AreNotSame(enemies[0], friends[0].CurrentAttackTarget,
                "수동 명령 우선 — 스폰 시점 자동 지정 적은 절대 재주장되지 않는다.");
            Assert.IsNull(friends[0].CurrentAttackTarget,
                "수동 앵커 주변(2.5m)에 적이 없으면 자동 재주장 없이 대기해야 한다.");
        }

        /// <summary>Test_16 실내 전쟁 라인업 미러 — 적 수비병 3명(2.4m 간격, 홀드) + 내 병사 3명(2.4m 간격, 12m 후방).</summary>
        private void BuildIndoorWarLineup(out List<GuardPlaceholder> friends, out List<GuardPlaceholder> enemies)
        {
            enemies = new List<GuardPlaceholder>();
            friends = new List<GuardPlaceholder>();
            for (int i = 0; i < 3; i++)
            {
                var enemy = SpawnGuard($"IndoorWar16_Enemy_{i + 1}", NationType.West);
                enemy.SetRecruited(false);
                enemy.HostileToPlayerFaction = true;
                enemy.transform.position = new Vector3(i * 2.4f, 0f, 0f);
                enemies.Add(enemy);

                var friend = SpawnGuard($"IndoorWar16_Friend_{i + 1}", NationType.East);
                friend.transform.position = new Vector3(i * 2.4f, 0f, -10f);
                friends.Add(friend);
            }
            Physics.SyncTransforms();
        }

        private static void SetPrivateField(GuardPlaceholder target, string fieldName, object value)
        {
            var field = typeof(GuardPlaceholder).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(field, "Expected private test seam field: " + fieldName);
            field.SetValue(target, value);
        }

        private GuardPlaceholder SpawnGuard(string name, NationType nation)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            go.AddComponent<BoxCollider>().size = Vector3.one;
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo(name, 1, nation);
            guard.SetRecruited(true);
            guard.SetHP(100f);
            return guard;
        }
    }
}
