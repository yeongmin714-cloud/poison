using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-09: AlarmSystem EditMode 테스트.
    /// 
    /// 테스트 대상:
    /// - AlarmState 기본값 (Peaceful)
    /// - Alert → Battle → Peace 상태 전이
    /// - UpdateAlarm 타이머 기반 전이
    /// - TriggerAlert / RestorePeace
    /// - GuardPlaceholder 전투 활성화 범위
    /// - ForceState / ResetAll
    /// </summary>
    public class AlarmSystemTests
    {
        private TerritoryId _testTerritory;

        [SetUp]
        public void Setup()
        {
            _testTerritory = new TerritoryId(NationType.East, 1);
            AlarmSystem.ResetAll();
        }

        [TearDown]
        public void Teardown()
        {
            AlarmSystem.ResetAll();
        }

        // ================================================================
        // 기본값
        // ================================================================

        [Test]
        public void GetState_Default_ReturnsPeaceful()
        {
            var state = AlarmSystem.GetState(_testTerritory);
            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, state,
                "초기화하지 않은 영지의 경보 상태는 Peaceful이어야 함");
        }

        [Test]
        public void GetState_MultipleTerritories_AreIndependent()
        {
            var territory2 = new TerritoryId(NationType.East, 2);
            var territory3 = new TerritoryId(NationType.West, 1);

            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, AlarmSystem.GetState(_testTerritory));
            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, AlarmSystem.GetState(territory2));
            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, AlarmSystem.GetState(territory3));
        }

        // ================================================================
        // TriggerAlert
        // ================================================================

        [Test]
        public void TriggerAlert_ChangesStateToAlert()
        {
            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);

            var state = AlarmSystem.GetState(_testTerritory);
            Assert.AreEqual(AlarmSystem.AlarmState.Alert, state,
                "TriggerAlert 후 상태는 Alert여야 함");
        }

        [Test]
        public void TriggerAlert_AlreadyBattle_DoesNotChange()
        {
            AlarmSystem.ForceState(_testTerritory, AlarmSystem.AlarmState.Battle);

            // Battle 상태에서 TriggerAlert 시도
            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);

            var state = AlarmSystem.GetState(_testTerritory);
            Assert.AreEqual(AlarmSystem.AlarmState.Battle, state,
                "Battle 상태에서는 TriggerAlert가 무시되어야 함");
        }

        [Test]
        public void TriggerAlert_FiresOnAlarmTriggeredEvent()
        {
            bool eventFired = false;
            TerritoryId eventTerritory = default;
            AlarmSystem.AlarmState eventState = default;

            AlarmSystem.OnAlarmTriggered += (tId, state) =>
            {
                eventFired = true;
                eventTerritory = tId;
                eventState = state;
            };

            AlarmSystem.TriggerAlert(_testTerritory, Vector3.one);

            Assert.IsTrue(eventFired, "OnAlarmTriggered 이벤트 발생해야 함");
            Assert.AreEqual(_testTerritory, eventTerritory);
            Assert.AreEqual(AlarmSystem.AlarmState.Alert, eventState);

            // 정리
            AlarmSystem.OnAlarmTriggered = null;
        }

        [Test]
        public void TriggerAlert_FiresOnAlarmStateChangedEvent()
        {
            bool eventFired = false;
            AlarmSystem.AlarmState oldState = default;
            AlarmSystem.AlarmState newState = default;

            AlarmSystem.OnAlarmStateChanged += (tId, oldS, newS) =>
            {
                eventFired = true;
                oldState = oldS;
                newState = newS;
            };

            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);

            Assert.IsTrue(eventFired, "OnAlarmStateChanged 이벤트 발생해야 함");
            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, oldState);
            Assert.AreEqual(AlarmSystem.AlarmState.Alert, newState);

            AlarmSystem.OnAlarmStateChanged = null;
        }

        // ================================================================
        // UpdateAlarm → Battle 전이 (Alert → Battle)
        // ================================================================

        [Test]
        public void UpdateAlarm_AlertToBattle_AfterDelay()
        {
            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);

            // Alert → Battle: ALARM_TO_BATTLE_DELAY(3초) 미만
            AlarmSystem.UpdateAlarm(_testTerritory, 2f);
            Assert.AreEqual(AlarmSystem.AlarmState.Alert, AlarmSystem.GetState(_testTerritory),
                "2초 경과 후 Alert 유지");

            // 3초 도달
            AlarmSystem.UpdateAlarm(_testTerritory, 1f);
            Assert.AreEqual(AlarmSystem.AlarmState.Battle, AlarmSystem.GetState(_testTerritory),
                "3초 경과 후 Battle로 전이");
        }

        [Test]
        public void UpdateAlarm_AlertToBattle_ExactDelay()
        {
            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);

            // 정확히 ALARM_TO_BATTLE_DELAY 만큼 업데이트
            AlarmSystem.UpdateAlarm(_testTerritory, AlarmSystem.ALARM_TO_BATTLE_DELAY);

            Assert.AreEqual(AlarmSystem.AlarmState.Battle, AlarmSystem.GetState(_testTerritory),
                "정확히 ALARM_TO_BATTLE_DELAY 후 Battle");
        }

        [Test]
        public void UpdateAlarm_BattleToPeace_AfterTimeout()
        {
            AlarmSystem.ForceState(_testTerritory, AlarmSystem.AlarmState.Battle);

            // BATTLE_TO_PEACE_TIMEOUT(60초) 미만
            AlarmSystem.UpdateAlarm(_testTerritory, 30f);
            Assert.AreEqual(AlarmSystem.AlarmState.Battle, AlarmSystem.GetState(_testTerritory),
                "30초 경과 후 Battle 유지");

            // 타임아웃 도달
            AlarmSystem.UpdateAlarm(_testTerritory, 30f + 1f); // 총 61초
            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, AlarmSystem.GetState(_testTerritory),
                "타임아웃 경과 후 Peaceful로 전이");
        }

        [Test]
        public void UpdateAlarm_Peaceful_DoesNothing()
        {
            // Peaceful 상태에서 UpdateAlarm 호출 — 변화 없음
            AlarmSystem.UpdateAlarm(_testTerritory, 100f);

            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, AlarmSystem.GetState(_testTerritory));
        }

        // ================================================================
        // RestorePeace
        // ================================================================

        [Test]
        public void RestorePeace_ChangesToPeaceful()
        {
            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);
            Assert.AreEqual(AlarmSystem.AlarmState.Alert, AlarmSystem.GetState(_testTerritory));

            AlarmSystem.RestorePeace(_testTerritory);
            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, AlarmSystem.GetState(_testTerritory));
        }

        [Test]
        public void RestorePeace_FiresOnPeaceRestoredEvent()
        {
            bool eventFired = false;
            TerritoryId eventTerritory = default;

            AlarmSystem.OnPeaceRestored += (tId) =>
            {
                eventFired = true;
                eventTerritory = tId;
            };

            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);
            AlarmSystem.RestorePeace(_testTerritory);

            Assert.IsTrue(eventFired, "OnPeaceRestored 이벤트 발생해야 함");
            Assert.AreEqual(_testTerritory, eventTerritory);

            AlarmSystem.OnPeaceRestored = null;
        }

        [Test]
        public void RestorePeace_AlreadyPeaceful_DoesNothing()
        {
            // 이미 Peaceful 상태에서 RestorePeace — 이벤트 없음
            bool eventFired = false;
            AlarmSystem.OnPeaceRestored += (tId) => { eventFired = true; };

            AlarmSystem.RestorePeace(_testTerritory);

            Assert.IsFalse(eventFired, "Peaceful 상태에서는 이벤트가 발생하지 않아야 함");

            AlarmSystem.OnPeaceRestored = null;
        }

        [Test]
        public void RestorePeace_FiresOnAlarmStateChangedEvent()
        {
            bool eventFired = false;
            AlarmSystem.AlarmState oldState = default;
            AlarmSystem.AlarmState newState = default;

            AlarmSystem.OnAlarmStateChanged += (tId, oldS, newS) =>
            {
                eventFired = true;
                oldState = oldS;
                newState = newS;
            };

            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);
            AlarmSystem.RestorePeace(_testTerritory);

            Assert.IsTrue(eventFired);
            Assert.AreEqual(AlarmSystem.AlarmState.Alert, oldState);
            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, newState);

            AlarmSystem.OnAlarmStateChanged = null;
        }

        // ================================================================
        // Guard 전투 활성화 (범위 테스트)
        // ================================================================

        [Test]
        public void TriggerAlert_ActivatesNearbyGuard()
        {
            // GuardPlaceholder 생성
            var guardGo = new GameObject("TestGuard");
            guardGo.transform.position = Vector3.zero;
            var guard = guardGo.AddComponent<GuardPlaceholder>();

            // 가까운 위치에서 경보 발령
            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);

            // 주변 가드가 전투 상태가 되었는지 확인
            Assert.IsTrue(guard.IsInCombat, "Alert 반경 내 Guard는 전투 상태가 되어야 함");

            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void TriggerAlert_DoesNotActivateFarGuard()
        {
            var farGuardGo = new GameObject("FarGuard");
            farGuardGo.transform.position = new Vector3(999f, 0f, 999f); // 매우 먼 거리
            var farGuard = farGuardGo.AddComponent<GuardPlaceholder>();

            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);

            // GuardCombatAI.COMBAT_DETECT_RANGE * 2 보다 훨씬 먼 거리
            // SetInCombat(true)가 호출되지 않아야 함 — 직접 확인은 어려우므로
            // 기본값 IsInCombat = false 확인
            Assert.IsFalse(farGuard.IsInCombat, "Alert 반경 밖 Guard는 전투 상태가 아니어야 함");

            Object.DestroyImmediate(farGuardGo);
        }

        [Test]
        public void TriggerAlert_MultipleGuardsInRange_AllActivated()
        {
            var guard1 = new GameObject("Guard1").AddComponent<GuardPlaceholder>();
            guard1.transform.position = new Vector3(5f, 0f, 0f);

            var guard2 = new GameObject("Guard2").AddComponent<GuardPlaceholder>();
            guard2.transform.position = new Vector3(0f, 0f, 5f);

            var guard3 = new GameObject("Guard3").AddComponent<GuardPlaceholder>();
            guard3.transform.position = new Vector3(-5f, 0f, -5f);

            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);

            Assert.IsTrue(guard1.IsInCombat, "Guard1 전투 활성화");
            Assert.IsTrue(guard2.IsInCombat, "Guard2 전투 활성화");
            Assert.IsTrue(guard3.IsInCombat, "Guard3 전투 활성화");

            Object.DestroyImmediate(guard1.gameObject);
            Object.DestroyImmediate(guard2.gameObject);
            Object.DestroyImmediate(guard3.gameObject);
        }

        // ================================================================
        // ForceState
        // ================================================================

        [Test]
        public void ForceState_SetsStateDirectly()
        {
            AlarmSystem.ForceState(_testTerritory, AlarmSystem.AlarmState.Battle);
            Assert.AreEqual(AlarmSystem.AlarmState.Battle, AlarmSystem.GetState(_testTerritory));

            AlarmSystem.ForceState(_testTerritory, AlarmSystem.AlarmState.Peaceful);
            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, AlarmSystem.GetState(_testTerritory));

            AlarmSystem.ForceState(_testTerritory, AlarmSystem.AlarmState.Alert);
            Assert.AreEqual(AlarmSystem.AlarmState.Alert, AlarmSystem.GetState(_testTerritory));
        }

        [Test]
        public void ForceState_FiresStateChangedEvent()
        {
            bool eventFired = false;
            AlarmSystem.OnAlarmStateChanged += (tId, oldS, newS) =>
            {
                eventFired = true;
                Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, oldS);
                Assert.AreEqual(AlarmSystem.AlarmState.Battle, newS);
            };

            AlarmSystem.ForceState(_testTerritory, AlarmSystem.AlarmState.Battle);
            Assert.IsTrue(eventFired);

            AlarmSystem.OnAlarmStateChanged = null;
        }

        // ================================================================
        // ResetAll
        // ================================================================

        [Test]
        public void ResetAll_ClearsAllStates()
        {
            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);
            var territory2 = new TerritoryId(NationType.West, 2);
            AlarmSystem.TriggerAlert(territory2, Vector3.one);

            Assert.AreEqual(AlarmSystem.AlarmState.Alert, AlarmSystem.GetState(_testTerritory));
            Assert.AreEqual(AlarmSystem.AlarmState.Alert, AlarmSystem.GetState(territory2));

            AlarmSystem.ResetAll();

            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, AlarmSystem.GetState(_testTerritory));
            Assert.AreEqual(AlarmSystem.AlarmState.Peaceful, AlarmSystem.GetState(territory2));
        }

        // ================================================================
        // 이벤트 정리
        // ================================================================

        [Test]
        public void Events_CanBeUnsubscribed()
        {
            bool eventFired = false;
            System.Action<TerritoryId, AlarmSystem.AlarmState> handler =
                (tId, state) => { eventFired = true; };

            AlarmSystem.OnAlarmTriggered += handler;
            AlarmSystem.TriggerAlert(_testTerritory, Vector3.zero);
            Assert.IsTrue(eventFired);

            eventFired = false;
            AlarmSystem.OnAlarmTriggered -= handler;
            AlarmSystem.TriggerAlert(new TerritoryId(NationType.East, 3), Vector3.zero);
            Assert.IsFalse(eventFired, "구독 해제 후 이벤트 발생하지 않아야 함");
        }
    }
}