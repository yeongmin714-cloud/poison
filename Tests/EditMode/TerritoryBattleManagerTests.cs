using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class TerritoryBattleManagerTests
    {
        private GameObject _mgrGo;
        private TerritoryBattleManager _mgr;
        private TerritoryDatabase _db;

        [SetUp]
        public void SetUp()
        {
            // Initialize TerritoryDatabase
            _db = TerritoryDatabase.Instance;

            _mgrGo = new GameObject("TerritoryBattleManager");
            _mgr = _mgrGo.AddComponent<TerritoryBattleManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_mgrGo);
        }

        [Test]
        public void Singleton_Instance_IsSet()
        {
            Assert.IsNotNull(TerritoryBattleManager.Instance);
        }

        [Test]
        public void DefaultState_IsPeaceful()
        {
            var state = _db.GetState(NationType.East, 1);
            Assert.AreEqual(TerritoryBattleState.Peaceful, state.battleState);
        }

        [Test]
        public void StartBattle_TransitionsToUnderAttack()
        {
            var id = new TerritoryId(NationType.East, 1);
            _mgr.StartBattle(id);

            var state = _db.GetState(id);
            Assert.AreEqual(TerritoryBattleState.UnderAttack, state.battleState);
        }

        [Test]
        public void StartBattle_AlreadyInBattle_DoesNothing()
        {
            var id = new TerritoryId(NationType.East, 1);
            _mgr.StartBattle(id); // Peaceful → UnderAttack
            _mgr.StartBattle(id); // Should not change

            var state = _db.GetState(id);
            Assert.AreEqual(TerritoryBattleState.UnderAttack, state.battleState);
        }

        [Test]
        public void StartBattle_RecordsGuardCount()
        {
            var id = new TerritoryId(NationType.East, 1);
            _mgr.StartBattle(id);

            var state = _db.GetState(id);
            Assert.AreEqual(0, state.totalGuardCount);
        }

        [Test]
        public void OnGuardDied_IncreasesDeadCount()
        {
            var id = new TerritoryId(NationType.East, 1);
            _mgr.StartBattle(id);

            _mgr.OnGuardDied(id);
            _mgr.OnGuardDied(id);

            var state = _db.GetState(id);
            Assert.AreEqual(2, state.deadGuardCount);
        }

        [Test]
        public void OnGuardDied_ConqueredState_DoesNothing()
        {
            var id = new TerritoryId(NationType.East, 1);
            _mgr.SetBattleStateForTest(id, TerritoryBattleState.Conquered);

            _mgr.OnGuardDied(id);

            var state = _db.GetState(id);
            Assert.AreEqual(0, state.deadGuardCount);
        }

        [Test]
        public void OnLordDefeated_TransitionsToConquered()
        {
            var id = new TerritoryId(NationType.East, 1);
            _mgr.StartBattle(id);
            _mgr.OnLordDefeated(id);

            var state = _db.GetState(id);
            Assert.AreEqual(TerritoryBattleState.Conquered, state.battleState);
        }

        [Test]
        public void RestorePeace_NotConquered_Works()
        {
            var id = new TerritoryId(NationType.East, 1);
            _mgr.SetBattleStateForTest(id, TerritoryBattleState.Retreated);
            _mgr.RestorePeace(id);

            var state = _db.GetState(id);
            Assert.AreEqual(TerritoryBattleState.Peaceful, state.battleState);
        }

        [Test]
        public void RestorePeace_Conquered_DoesNotChange()
        {
            var id = new TerritoryId(NationType.East, 1);
            _mgr.SetBattleStateForTest(id, TerritoryBattleState.Conquered);
            _mgr.RestorePeace(id);

            var state = _db.GetState(id);
            Assert.AreEqual(TerritoryBattleState.Conquered, state.battleState);
        }

        [Test]
        public void GetBattleState_ReturnsCorrectState()
        {
            var id = new TerritoryId(NationType.East, 1);
            _mgr.SetBattleStateForTest(id, TerritoryBattleState.Reinforcing);

            var state = _mgr.GetBattleState(id);
            Assert.AreEqual(TerritoryBattleState.Reinforcing, state);
        }

        [Test]
        public void GetBattleState_Default_ReturnsPeaceful()
        {
            var id = new TerritoryId(NationType.East, 1);
            // Don't start battle - should still be Peaceful
            var state = _mgr.GetBattleState(id);
            Assert.AreEqual(TerritoryBattleState.Peaceful, state);
        }
    }
}