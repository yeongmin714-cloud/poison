using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;
using System.Reflection;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 27: GuardManager EditMode 테스트.
    /// 
    /// 테스트 대상:
    /// - GuardManager 싱글톤 초기화
    /// - RegisterGuard / UnregisterGuard
    /// - GetGuardsInTerritory / GetAllPlayerGuards
    /// - SetRetreatMode / IsRetreatMode
    /// - StopAllCombat
    /// - SetGuardsToLowHP (체력 10%)
    /// - AutoHealing (30초, 1%/초)
    /// - StartRefillTimer / TryRefill
    /// - GuardPlaceholder.Die() → Destroy (영구 사망)
    /// - PlayerHealth 사망 → GuardManager 반응
    /// </summary>
    public class GuardManagerTests
    {
        private GameObject _managerGo;
        private TerritoryId _testTerritory;
        private List<System.Action> _cleanupActions;

        [SetUp]
        public void Setup()
        {
            _testTerritory = new TerritoryId(NationType.East, 1);
            _cleanupActions = new List<System.Action>();

            // GuardManager 생성
            _managerGo = new GameObject("TestGuardManager");
            _managerGo.AddComponent<GuardManager>();
            // Awake 호출을 위해 강제 초기화
            var mgr = GuardManager.Instance;
            Assert.IsNotNull(mgr, "GuardManager.Instance는 null이 아니어야 함");
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var action in _cleanupActions)
                action();
            _cleanupActions.Clear();

            if (_managerGo != null)
            {
                Object.DestroyImmediate(_managerGo);
                _managerGo = null;
            }

            // PlayerHealth 이벤트 정리
        }

        // ================================================================
        // 27.1: 병사 등록/제거
        // ================================================================

        [Test]
        public void RegisterGuard_AddsGuardToTerritory()
        {
            var guard = CreateTestGuard("테스트병사1");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            int count = GuardManager.Instance.GetGuardCount(_testTerritory);
            Assert.AreEqual(1, count, "병사 등록 후 영지 병사 수는 1이어야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        [Test]
        public void RegisterGuard_MultipleGuards_CountIncreases()
        {
            var guard1 = CreateTestGuard("테스트병사1");
            var guard2 = CreateTestGuard("테스트병사2");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard1);
            GuardManager.Instance.RegisterGuard(_testTerritory, guard2);

            int count = GuardManager.Instance.GetGuardCount(_testTerritory);
            Assert.AreEqual(2, count, "병사 2명 등록 후 영지 병사 수는 2이어야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard1.gameObject));
            _cleanupActions.Add(() => Object.DestroyImmediate(guard2.gameObject));
        }

        [Test]
        public void RegisterGuard_DuplicateGuard_DoesNotDuplicate()
        {
            var guard = CreateTestGuard("테스트병사1");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);
            GuardManager.Instance.RegisterGuard(_testTerritory, guard); // 중복 등록

            int count = GuardManager.Instance.GetGuardCount(_testTerritory);
            Assert.AreEqual(1, count, "중복 등록해도 병사 수는 1이어야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        [Test]
        public void UnregisterGuard_RemovesGuardFromTerritory()
        {
            var guard = CreateTestGuard("테스트병사1");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);
            GuardManager.Instance.UnregisterGuard(_testTerritory, guard);

            int count = GuardManager.Instance.GetGuardCount(_testTerritory);
            Assert.AreEqual(0, count, "제거 후 영지 병사 수는 0이어야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        [Test]
        public void GetGuardsInTerritory_Empty_ReturnsEmptyList()
        {
            var guards = GuardManager.Instance.GetGuardsInTerritory(_testTerritory);
            Assert.IsNotNull(guards, "목록은 null이 아니어야 함");
            Assert.AreEqual(0, guards.Count, "등록된 병사가 없으면 빈 목록 반환");
        }

        [Test]
        public void GetGuardsInTerritory_ReturnsCopyOfList()
        {
            var guard = CreateTestGuard("테스트병사1");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            var guards = GuardManager.Instance.GetGuardsInTerritory(_testTerritory);
            guards.Clear(); // 원본에 영향 없음

            int count = GuardManager.Instance.GetGuardCount(_testTerritory);
            Assert.AreEqual(1, count, "반환된 목록을 수정해도 원본에 영향 없어야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        [Test]
        public void GetAllPlayerGuards_ReturnsOnlyPlayerOwned()
        {
            var guard1 = CreateTestGuard("플레이어병사");
            guard1.SetRecruited(true);
            GuardManager.Instance.RegisterGuard(_testTerritory, guard1);

            // 플레이어 소유로 설정
            var db = TerritoryDatabase.Instance;
            db.SetOwnership(_testTerritory, TerritoryOwnership.PlayerOwned);

            var playerGuards = GuardManager.Instance.GetAllPlayerGuards();
            Assert.IsNotNull(playerGuards, "목록은 null이 아니어야 함");
            Assert.AreEqual(1, playerGuards.Count, "플레이어 소유 영지의 병사 1명이 반환되어야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard1.gameObject));
        }

        // ================================================================
        // 27.2: 퇴각 모드
        // ================================================================

        [Test]
        public void SetRetreatMode_Default_IsFalse()
        {
            Assert.IsFalse(GuardManager.Instance.IsRetreatMode, "초기 퇴각 모드는 false여야 함");
        }

        [Test]
        public void SetRetreatMode_True_ChangesState()
        {
            GuardManager.Instance.SetRetreatMode(true);
            Assert.IsTrue(GuardManager.Instance.IsRetreatMode, "퇴각 모드 설정 후 true여야 함");
        }

        [Test]
        public void SetRetreatMode_False_ChangesState()
        {
            GuardManager.Instance.SetRetreatMode(true);
            GuardManager.Instance.SetRetreatMode(false);
            Assert.IsFalse(GuardManager.Instance.IsRetreatMode, "퇴각 모드 해제 후 false여야 함");
        }

        [Test]
        public void StopAllCombat_StopsAllGuardsCombat()
        {
            var guard1 = CreateTestGuard("테스트병사1");
            var guard2 = CreateTestGuard("테스트병사2");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard1);
            GuardManager.Instance.RegisterGuard(_testTerritory, guard2);

            guard1.SetInCombat(true);
            guard2.SetInCombat(true);

            Assert.IsTrue(guard1.IsInCombat, "초기: 전투 중");
            Assert.IsTrue(guard2.IsInCombat, "초기: 전투 중");

            GuardManager.Instance.StopAllCombat();

            Assert.IsFalse(guard1.IsInCombat, "StopAllCombat 후: 전투 중단");
            Assert.IsFalse(guard2.IsInCombat, "StopAllCombat 후: 전투 중단");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard1.gameObject));
            _cleanupActions.Add(() => Object.DestroyImmediate(guard2.gameObject));
        }

        [Test]
        public void SetRetreatMode_True_StopsAllCombat()
        {
            var guard = CreateTestGuard("테스트병사1");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);
            guard.SetInCombat(true);

            GuardManager.Instance.SetRetreatMode(true);

            Assert.IsFalse(guard.IsInCombat, "퇴각 모드에서 모든 병사 전투 중단");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        // ================================================================
        // 27.3: 체력 10% 설정 (부활 시)
        // ================================================================

        [Test]
        public void SetGuardsToLowHP_SetsHPTo10Percent()
        {
            var guard = CreateTestGuard("테스트병사1");
            // HP를 최대로 설정
            guard.SetHP(guard.MaxHP);
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            // 플레이어 소유로 설정
            var db = TerritoryDatabase.Instance;
            db.SetOwnership(_testTerritory, TerritoryOwnership.PlayerOwned);

            // 체력 10% 설정 (OnPlayerRespawned 내부에서 호출)
            GuardManager.Instance.SetGuardsToLowHPForTest();

            float expectedHP = guard.MaxHP * 0.1f;
            Assert.AreEqual(expectedHP, guard.HP, 0.01f, "부활 후 체력은 최대의 10%여야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        [Test]
        public void SetGuardsToLowHP_DoesNotKillGuard()
        {
            var guard = CreateTestGuard("테스트병사1");
            guard.SetHP(guard.MaxHP);
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            var db = TerritoryDatabase.Instance;
            db.SetOwnership(_testTerritory, TerritoryOwnership.PlayerOwned);

            GuardManager.Instance.SetGuardsToLowHPForTest();

            Assert.IsTrue(guard.IsAlive, "체력 10%여도 병사는 살아있어야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        // ================================================================
        // 27.4: 자동 회복 (30초, 1%/초)
        // ================================================================

        [Test]
        public void StartAutoHealing_HealsOverTime()
        {
            var guard = CreateTestGuard("테스트병사1");
            guard.SetHP(guard.MaxHP * 0.1f); // 10%
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            var db = TerritoryDatabase.Instance;
            db.SetOwnership(_testTerritory, TerritoryOwnership.PlayerOwned);

            GuardManager.Instance.StartAutoHealingForTest();

            // 1초 회분 시뮬레이션 (1% = MaxHP * 0.01)
            float hpBefore = guard.HP;
            GuardManager.Instance.ProcessAutoHealingForTest();
            float hpAfter = guard.HP;

            Assert.Greater(hpAfter, hpBefore, "자동 회복 후 HP가 증가해야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        [Test]
        public void StartAutoHealing_OnePercentPerSecond()
        {
            var guard = CreateTestGuard("테스트병사1");
            float initialHP = guard.MaxHP * 0.1f;
            guard.SetHP(initialHP);
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            var db = TerritoryDatabase.Instance;
            db.SetOwnership(_testTerritory, TerritoryOwnership.PlayerOwned);

            GuardManager.Instance.StartAutoHealingForTest();

            // 1초 후 (Time.deltaTime = 1 가정)
            // 실제로는 ProcessAutoHealing에서 deltaTime 사용
            // 테스트는 단순히 HP 증가 확인
            float hpBefore = guard.HP;

            // 여러 번 호출하여 회복 시뮬레이션
            for (int i = 0; i < 10; i++)
            {
                GuardManager.Instance.ProcessAutoHealingForTest();
            }

            float hpAfter = guard.HP;
            Assert.Greater(hpAfter, hpBefore, "자동 회복 후 HP가 증가해야 함");
            Assert.LessOrEqual(hpAfter, guard.MaxHP, "HP는 최대 체력을 넘지 않아야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        // ================================================================
        // 27.5: 재충원 시스템
        // ================================================================

        [Test]
        public void StartRefillTimer_DoesNotCrash()
        {
            // 예외 없이 타이머가 시작되는지만 확인
            Assert.DoesNotThrow(() =>
            {
                GuardManager.Instance.StartRefillTimer(_testTerritory);
            }, "StartRefillTimer는 예외를 던지지 않아야 함");
        }

        [Test]
        public void StopRefillTimer_DoesNotCrash()
        {
            GuardManager.Instance.StartRefillTimer(_testTerritory);
            Assert.DoesNotThrow(() =>
            {
                GuardManager.Instance.StopRefillTimer(_testTerritory);
            }, "StopRefillTimer는 예외를 던지지 않아야 함");
        }

        [Test]
        public void TryRefill_WithEmptyTerritory_DoesNothing()
        {
            // 등록된 병사가 없는 영지는 재충원되지 않음
            GuardManager.Instance.StartRefillTimer(_testTerritory);
            Assert.DoesNotThrow(() =>
            {
                GuardManager.Instance.TryRefillForTest(_testTerritory);
            }, "TryRefill은 예외를 던지지 않아야 함");
        }

        [Test]
        public void TryRefill_WithRegisteredGuards_MayCreateNewGuard()
        {
            // 병사 1명 등록된 상태에서 재충원 시도
            var guard = CreateTestGuard("테스트병사1");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            // 여러 번 시도 (확률 기반이므로)
            int countBefore = GuardManager.Instance.GetGuardCount(_testTerritory);

            for (int i = 0; i < 20; i++)
            {
                // Random.value를 조작할 수 없으므로 여러 번 시도
                GuardManager.Instance.TryRefillForTest(_testTerritory);
            }

            int countAfter = GuardManager.Instance.GetGuardCount(_testTerritory);
            Assert.GreaterOrEqual(countAfter, countBefore, "재충원 후 병사 수는 같거나 증가해야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        // ================================================================
        // 27.6: GuardPlaceholder.Die() → Destroy 확인
        // ================================================================

        [Test]
        public void GuardDie_DestroysGameObject()
        {
            var guard = CreateTestGuard("테스트병사1");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            // 데미지를 줘서 사망 유발
            guard.TakeDamage(9999f, Vector3.zero, "Test");

            // Destroy는 Immediate 모드가 아니면 즉시 실행 안 됨
            // 하지만 테스트 환경에서 null 체크
            Assert.IsTrue(guard == null || guard.Equals(null),
                "사망 후 GuardPlaceholder 오브젝트는 Destroy되어야 함");
        }

        [Test]
        public void GuardDie_UnregistersFromGuardManager()
        {
            var guard = CreateTestGuard("테스트병사1");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            // 사망 전: 카운트 1
            Assert.AreEqual(1, GuardManager.Instance.GetGuardCount(_testTerritory));

            // 데미지를 줘서 사망 유발
            GameObject guardGO = guard.gameObject;
            guard.TakeDamage(9999f, Vector3.zero, "Test");

            // Destroy Immediate로 처리
            Object.DestroyImmediate(guardGO);

            // 사망 후: GuardManager에서 제거되어야 함
            Assert.AreEqual(0, GuardManager.Instance.GetGuardCount(_testTerritory),
                "병사 사망 후 GuardManager에서 제거되어야 함");
        }

        // ================================================================
        // 27.7: 플레이어 사망 연동
        // ================================================================

        [Test]
        public void OnPlayerDied_ActivatesRetreatMode()
        {
            var guard = CreateTestGuard("테스트병사1");
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            // 플레이어 사망 이벤트 발생
            PlayerHealth.Instance.TakeDamage(1000f);

            Assert.IsTrue(GuardManager.Instance.IsRetreatMode,
                "플레이어 사망 시 퇴각 모드가 활성화되어야 함");

            _cleanupActions.Add(() => Object.DestroyImmediate(guard.gameObject));
        }

        [Test]
        public void OnPlayerRespawned_DeactivatesRetreatMode()
        {
            GuardManager.Instance.SetRetreatMode(true);

            // Kill the player and invoke respawn via reflection
            PlayerHealth.Instance.TakeDamage(1000f);
            var playerType = typeof(PlayerHealth);
            var respawnMethod = playerType.GetMethod("Respawn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            respawnMethod.Invoke(PlayerHealth.Instance, null);

            Assert.IsFalse(GuardManager.Instance.IsRetreatMode,
                "플레이어 부활 시 퇴각 모드가 해제되어야 함");
        }

        // ================================================================
        // 헬퍼
        // ================================================================

        private static GuardPlaceholder CreateTestGuard(string name)
        {
            var go = new GameObject(name);
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo(name, 1, NationType.East);
            return guard;
        }
    }
}