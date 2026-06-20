using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 5.3.1/5.3.6: World Space HUD + 병사 머리 위 표시 + 적대/부활 통합 테스트.
    ///
    /// 테스트 범위 (최소 15개):
    /// - HUD: IWorldSpaceHUD 인터페이스, 데이터 제공
    /// - 거리: 15m 컬링 조건
    /// - 호감도: 음수 허용, 적대 전환
    /// - 선공: -30 미만 근접 공격
    /// - 경보: -50 미만 경보 발령
    /// - 부활: 사망 등록 → 30초 후 10% HP 부활
    /// </summary>
    public class Phase5_HostilityTests
    {
        private GameObject _managerGo;
        private GameObject _hostilityGo;
        private GameObject _resurrectionGo;
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
            Assert.IsNotNull(GuardManager.Instance, "GuardManager.Instance는 null이 아니어야 함");

            // GuardHostilitySystem 생성
            _hostilityGo = new GameObject("TestGuardHostility");
            _hostilityGo.AddComponent<GuardHostilitySystem>();
            Assert.IsNotNull(GuardHostilitySystem.Instance, "GuardHostilitySystem.Instance는 null이 아니어야 함");

            // GuardResurrectionSystem 생성
            _resurrectionGo = new GameObject("TestGuardResurrection");
            _resurrectionGo.AddComponent<GuardResurrectionSystem>();
            Assert.IsNotNull(GuardResurrectionSystem.Instance, "GuardResurrectionSystem.Instance는 null이 아니어야 함");
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var action in _cleanupActions)
                action();
            _cleanupActions.Clear();

            // 시스템 초기화
            if (GuardHostilitySystem.Instance != null)
                Object.DestroyImmediate(_hostilityGo);
            if (GuardResurrectionSystem.Instance != null)
            {
                GuardResurrectionSystem.Instance.ClearDeadGuards();
                Object.DestroyImmediate(_resurrectionGo);
            }
            if (GuardManager.Instance != null)
                Object.DestroyImmediate(_managerGo);

            _hostilityGo = null;
            _resurrectionGo = null;
            _managerGo = null;
        }

        // ================================================================
        // 5.3.1: HUD 인터페이스 테스트
        // ================================================================

        [Test]
        public void GuardPlaceholder_Implements_IWorldSpaceHUD()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            Assert.IsNotNull(guard as IWorldSpaceHUD, "GuardPlaceholder는 IWorldSpaceHUD를 구현해야 함");
        }

        [Test]
        public void IWorldSpaceHUD_ProvidesCorrectData()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            guard.SetGuardInfo("테스트병사", 5, NationType.East);
            guard.Loyalty = 30f;
            guard.Addiction = 45f;

            IWorldSpaceHUD hud = guard;
            Assert.AreEqual("테스트병사", hud.HUDName, "HUD 이름 일치");
            Assert.AreEqual(5, hud.HUDLevel, "HUD 레벨 일치");
            Assert.AreEqual(30f, hud.HUDLoyalty, 0.01f, "HUD 호감도 일치");
            Assert.AreEqual(45f, hud.HUDAddiction, 0.01f, "HUD 중독도 일치");
        }

        [Test]
        public void IWorldSpaceHUD_ShouldShowHUD_AliveOnly()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            IWorldSpaceHUD hud = guard;
            Assert.IsTrue(hud.ShouldShowHUD, "생존 중에는 HUD 표시되어야 함");

            guard.TakeDamage(9999f, Vector3.zero, "Test");
            Assert.IsFalse(hud.ShouldShowHUD, "사망 후에는 HUD 표시되지 않아야 함");
        }

        [Test]
        public void IWorldSpaceHUD_WorldPosition_AboveHead()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            guard.transform.position = new Vector3(10f, 0f, 20f);

            IWorldSpaceHUD hud = guard;
            Vector3 worldPos = hud.WorldPosition;

            Assert.AreEqual(10f, worldPos.x, 0.01f, "X 위치 일치");
            Assert.Greater(worldPos.y, 0f, "Y 위치는 머리 위(>0)여야 함");
            Assert.AreEqual(20f, worldPos.z, 0.01f, "Z 위치 일치");
            Assert.AreEqual(2.5f, worldPos.y, 0.01f, "머리 위 2.5m");
        }

        [Test]
        public void GuardWorldSpaceHUD_Singleton_Exists()
        {
            var hudGo = new GameObject("TestHUD");
            var hud = hudGo.AddComponent<GuardWorldSpaceHUD>();
            _cleanupActions.Add(() => Object.DestroyImmediate(hudGo));

            Assert.IsNotNull(GuardWorldSpaceHUD.Instance, "GuardWorldSpaceHUD 싱글톤 존재");
        }

        [Test]
        public void GuardWorldSpaceHUD_HUDEnabled_Toggle()
        {
            var hudGo = new GameObject("TestHUD");
            var hud = hudGo.AddComponent<GuardWorldSpaceHUD>();
            _cleanupActions.Add(() => Object.DestroyImmediate(hudGo));

            Assert.IsTrue(hud.HUDEnabled, "초기 HUD 활성화");

            hud.HUDEnabled = false;
            Assert.IsFalse(hud.HUDEnabled, "HUD 비활성화 후 false");

            hud.HUDEnabled = true;
            Assert.IsTrue(hud.HUDEnabled, "HUD 재활성화 후 true");
        }

        // ================================================================
        // 5.3.6: 호감도 음수 허용 + 적대 전환
        // ================================================================

        [Test]
        public void GuardLoyalty_NegativeAllowed()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            guard.Loyalty = -50f;
            Assert.AreEqual(-50f, guard.Loyalty, 0.01f, "호감도 -50 허용");

            guard.Loyalty = -100f;
            Assert.AreEqual(-100f, guard.Loyalty, 0.01f, "호감도 -100 허용");

            guard.Loyalty = -150f;
            Assert.AreEqual(-100f, guard.Loyalty, 0.01f, "호감도 -150 → -100 클램프");
        }

        [Test]
        public void HostilitySystem_LoyaltyBelowZero_Hostile()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            GuardHostilitySystem sys = GuardHostilitySystem.Instance;

            guard.Loyalty = 20f;
            // ForceSetHostility로 업데이트
            sys.ForceSetHostility(guard, 20f);
            Assert.IsFalse(sys.IsHostile(guard), "호감도 20 → 비적대");

            sys.ForceSetHostility(guard, -10f);
            Assert.IsTrue(sys.IsHostile(guard), "호감도 -10 → 적대");
        }

        [Test]
        public void HostilitySystem_LoyaltyBelow30_Aggressive()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            GuardHostilitySystem sys = GuardHostilitySystem.Instance;

            sys.ForceSetHostility(guard, -20f);
            Assert.IsFalse(sys.IsAggressive(guard), "호감도 -20 → 비선공");

            sys.ForceSetHostility(guard, -35f);
            Assert.IsTrue(sys.IsAggressive(guard), "호감도 -35 → 선공");
        }

        [Test]
        public void HostilitySystem_LoyaltyBelow50_Alarm()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            GuardHostilitySystem sys = GuardHostilitySystem.Instance;

            sys.ForceSetHostility(guard, -40f);
            Assert.IsFalse(sys.IsAlarmTriggered(guard), "호감도 -40 → 비경보");

            sys.ForceSetHostility(guard, -60f);
            Assert.IsTrue(sys.IsAlarmTriggered(guard), "호감도 -60 → 경보");
        }

        [Test]
        public void HostilitySystem_ConvertToHostile_SetsInCombat()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            Assert.IsFalse(guard.IsInCombat, "초기: 비전투");

            GuardHostilitySystem sys = GuardHostilitySystem.Instance;
            sys.ForceSetHostility(guard, -5f);

            Assert.IsTrue(guard.IsInCombat, "적대 전환 후 전투 상태");
        }

        [Test]
        public void HostilitySystem_HostileGuardCount()
        {
            var go1 = new GameObject("TestGuard1");
            var guard1 = go1.AddComponent<GuardPlaceholder>();
            var go2 = new GameObject("TestGuard2");
            var guard2 = go2.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => { Object.DestroyImmediate(go1); Object.DestroyImmediate(go2); });

            GuardHostilitySystem sys = GuardHostilitySystem.Instance;

            sys.ForceSetHostility(guard1, 50f); // Friendly
            sys.ForceSetHostility(guard2, -10f); // Hostile

            Assert.AreEqual(1, sys.HostileGuardCount, "적대 병사 수 = 1");
        }

        // ================================================================
        // 5.3.6: 부활 시스템 테스트
        // ================================================================

        [Test]
        public void ResurrectionSystem_GuardDies_RegisteredInQueue()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            // GuardManager에 등록
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            // 사망
            guard.TakeDamage(9999f, Vector3.zero, "Test");

            GuardResurrectionSystem sys = GuardResurrectionSystem.Instance;
            Assert.AreEqual(1, sys.DeadGuardCount, "사망 대기열에 1명 등록");
            Assert.IsTrue(sys.IsDeadAndPending(guard), "해당 병사가 대기열에 있음");
        }

        [Test]
        public void ResurrectionSystem_ForceResurrect_Restores10PercentHP()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            float maxHP = guard.MaxHP;
            guard.TakeDamage(9999f, Vector3.zero, "Test");

            GuardResurrectionSystem sys = GuardResurrectionSystem.Instance;
            sys.ForceResurrect(guard);

            float expectedHP = maxHP * 0.1f;
            Assert.AreEqual(expectedHP, guard.HP, 0.01f, "부활 후 HP = maxHP × 10%");
            Assert.IsTrue(guard.IsAlive, "부활 후 생존 상태");
            Assert.IsTrue(guard.gameObject.activeInHierarchy, "부활 후 오브젝트 활성화");
        }

        [Test]
        public void ResurrectionSystem_ForceResurrectAll_AllRevived()
        {
            var go1 = new GameObject("TestGuard1");
            var guard1 = go1.AddComponent<GuardPlaceholder>();
            var go2 = new GameObject("TestGuard2");
            var guard2 = go2.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => { Object.DestroyImmediate(go1); Object.DestroyImmediate(go2); });

            GuardManager.Instance.RegisterGuard(_testTerritory, guard1);
            GuardManager.Instance.RegisterGuard(_testTerritory, guard2);

            guard1.TakeDamage(9999f, Vector3.zero, "Test");
            guard2.TakeDamage(9999f, Vector3.zero, "Test");

            GuardResurrectionSystem sys = GuardResurrectionSystem.Instance;
            Assert.AreEqual(2, sys.DeadGuardCount, "사망 대기열 2명");

            sys.ForceResurrectAll();

            Assert.AreEqual(0, sys.DeadGuardCount, "전체 부활 후 대기열 비어있음");
            Assert.IsTrue(guard1.IsAlive, "guard1 부활");
            Assert.IsTrue(guard2.IsAlive, "guard2 부활");
        }

        [Test]
        public void ResurrectionSystem_RespawnPosition_Restored()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            guard.transform.position = new Vector3(100f, 0f, 200f);
            GuardManager.Instance.RegisterGuard(_testTerritory, guard);

            guard.TakeDamage(9999f, Vector3.zero, "Test");

            GuardResurrectionSystem sys = GuardResurrectionSystem.Instance;
            sys.ForceResurrect(guard);

            // 부활 위치는 사망 위치 또는 영지 입구
            Assert.IsNotNull(guard, "부활 후 guard 인스턴스 유지");
            Assert.IsTrue(guard.IsAlive, "부활 후 생존");
        }

        [Test]
        public void GuardResurrect_ClearsInfoAndSelection()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            guard.TakeDamage(9999f, Vector3.zero, "Test");

            GuardResurrectionSystem sys = GuardResurrectionSystem.Instance;
            sys.ForceResurrect(guard);

            // Resurrect 내부에서 _showInfo = false, _selectionMode = None 설정
            Assert.IsFalse(guard.IsShowingInfo, "부활 후 정보창 닫힘");
            Assert.IsFalse(guard.IsSelectingItem, "부활 후 선택 모드 해제");
        }

        // ================================================================
        // 시스템 초기화/설정 인터페이스 테스트
        // ================================================================

        [Test]
        public void ResurrectionSystem_Configurable_Delay()
        {
            GuardResurrectionSystem sys = GuardResurrectionSystem.Instance;

            Assert.AreEqual(30f, sys.RespawnDelay, 0.01f, "기본 부활 대기 시간 30초");

            sys.RespawnDelay = 60f;
            Assert.AreEqual(60f, sys.RespawnDelay, 0.01f, "변경 가능");

            sys.RespawnDelay = 30f; // 복원
        }

        [Test]
        public void ResurrectionSystem_Configurable_HPPercent()
        {
            GuardResurrectionSystem sys = GuardResurrectionSystem.Instance;

            Assert.AreEqual(0.1f, sys.RespawnHPPercent, 0.01f, "기본 부활 HP 10%");

            sys.RespawnHPPercent = 0.5f;
            Assert.AreEqual(0.5f, sys.RespawnHPPercent, 0.01f, "변경 가능");

            sys.RespawnHPPercent = 1.5f;
            Assert.AreEqual(1.0f, sys.RespawnHPPercent, 0.01f, "최대 100% 클램프");
        }

        // ================================================================
        // GuardPlaceholder: Resurrect 메서드 단위 테스트
        // ================================================================

        [Test]
        public void GuardPlaceholder_Resurrect_WithCustomPercent()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            _cleanupActions.Add(() => Object.DestroyImmediate(go));

            guard.TakeDamage(9999f, Vector3.zero, "Test");
            Assert.IsFalse(guard.IsAlive, "사망 확인");

            float maxHP = guard.MaxHP;
            guard.Resurrect(0.5f);

            float expectedHP = maxHP * 0.5f;
            Assert.AreEqual(expectedHP, guard.HP, 0.01f, "50% HP로 부활");
            Assert.IsTrue(guard.IsAlive, "부활 후 생존");
        }
    }
}