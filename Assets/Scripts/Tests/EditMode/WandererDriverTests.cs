using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [119차] 방랑자 코어 계약 테스트 — GuardPlaceholder 방랑 상태/아웃로 게이트 + WandererDriver (EditMode 가능 부).
    ///  · BecomeWanderer: 플래그 세팅 + 씬 루트 재부모 + 드라이버 부착 + 멱등
    ///  · EndWanderer: 플래그 해제 + 드라이버 제거 + 지정 지점 보행 명령
    ///  · TryCommandRaid: GuardManager 부재 안전 반환 / 양측 교전 명령 발행
    ///
    /// EditMode 규약: AddComponent는 Awake 미실행 → GuardManager.Instance는 리플렉션 세팅
    /// (HostileTerritoryCombatTests/CraftBenchTests 패턴 준용). 실행 판정은 QA 서브에이전트 증거로 확정.
    /// </summary>
    public sealed class WandererDriverTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private GameObject _managerGo;
        private GuardManager _previousManager;   // 싱글턴 보존/복원 — 픽스처 순서 무관 결정론

        [SetUp]
        public void SetUp()
        {
            WandererDriver.ResetAll();
            _previousManager = GuardManager.Instance;
            typeof(GuardManager).GetProperty("Instance").SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            if (_managerGo != null)
            {
                Object.DestroyImmediate(_managerGo);
                _managerGo = null;
            }

            WandererDriver.ResetAll();
            typeof(GuardManager).GetProperty("Instance").SetValue(null, _previousManager);
        }

        // ===== BecomeWanderer 계약 =====

        [Test]
        public void BecomeWanderer_SetsFlagsAndDetachesParent()
        {
            GameObject parent = new GameObject("Wanderer_Parent");
            _spawned.Add(parent);
            GuardPlaceholder guard = CreateGuard("테스트병사", 1, NationType.East);
            guard.transform.SetParent(parent.transform, false);
            Assert.IsNotNull(guard.transform.parent, "전제: Territory_ 계층 부모 존재");

            guard.BecomeWanderer("테스트");

            Assert.IsTrue(guard.IsWanderer, "방랑자 플래그 세팅");
            Assert.IsTrue(guard.IsOutlawWanderer, "아웃로 판정 = IsWanderer 별칭");
            Assert.IsNull(guard.transform.parent, "씬 루트 재부모(Territory_ 계층 이탈)");
            Assert.IsNotNull(guard.GetComponent<WandererDriver>(), "WandererDriver 부착");
        }

        [Test]
        public void BecomeWanderer_Idempotent()
        {
            GuardPlaceholder guard = CreateGuard("테스트병사", 1, NationType.East);

            guard.BecomeWanderer("테스트1");
            Assert.DoesNotThrow(() => guard.BecomeWanderer("테스트2"), "이미 방랑자 — 멱등 스킵(예외 없음)");
            Assert.IsTrue(guard.IsWanderer, "방랑자 상태 유지");
            Assert.AreEqual(1, guard.GetComponents<WandererDriver>().Length, "드라이버 중복 부착 없음");
        }

        // ===== EndWanderer 계약 =====

        [Test]
        public void EndWanderer_ClearsFlagAndIssuesMoveCommand()
        {
            GuardPlaceholder guard = CreateGuard("테스트병사", 1, NationType.East);
            guard.BecomeWanderer("테스트");

            guard.EndWanderer(new Vector3(5f, 0f, 5f));

            Assert.IsFalse(guard.IsWanderer, "방랑 상태 해제");
            Assert.IsNull(guard.GetComponent<WandererDriver>(), "드라이버 제거");
            Assert.IsTrue(guard.HasCommand, "지정 지점 보행 명령 발행");
            Assert.IsFalse(guard.IsAttackCommand, "보행 명령(공격 아님)");
        }

        // ===== TryCommandRaid 계약 =====

        [Test]
        public void TryCommandRaid_NoGuardManager_ReturnsFalse()
        {
            GuardPlaceholder guard = CreateGuard("테스트병사", 1, NationType.East);
            guard.BecomeWanderer("테스트");
            Assert.IsNull(GuardManager.Instance, "전제: GuardManager 미건설");

            bool result = true;
            Assert.DoesNotThrow(() => result = WandererDriver.TryCommandRaid(guard, new TerritoryId(NationType.East, 1)),
                "GuardManager 부재 — 예외 없이 안전 반환");
            Assert.IsFalse(result);
        }

        [Test]
        public void TryCommandRaid_IssuesMutualCommands()
        {
            // 테스트 전용 GuardManager — AddComponent(Awake 미실행) + Instance 리플렉션 세팅
            _managerGo = new GameObject("Wanderer_TestGuardManager");
            GuardManager manager = _managerGo.AddComponent<GuardManager>();
            typeof(GuardManager).GetProperty("Instance").SetValue(null, manager);

            TerritoryId east1 = new TerritoryId(NationType.East, 1);
            GuardPlaceholder defender = CreateGuard("테스트수비병", 2, NationType.West);
            manager.RegisterGuard(east1, defender);

            GuardPlaceholder wanderer = CreateGuard("테스트방랑자", 1, NationType.East);
            wanderer.BecomeWanderer("테스트");

            bool result = WandererDriver.TryCommandRaid(wanderer, east1);

            Assert.IsTrue(result, "수비 존재 — 습격 명령 성공");
            Assert.IsTrue(wanderer.HasCommand, "방랑자 습격 명령 발행");
            Assert.IsTrue(defender.HasCommand, "수비병 교전 명령 발행");
            Assert.IsTrue(defender.IsAttackCommand, "수비병 명령 = 공격(반격)");
        }

        // ===== 헬퍼 =====

        /// <summary>테스트 전용 GuardPlaceholder — Awake 미실행 환경 최소 세팅(AILaborRehireFeeTests 패턴 준용).</summary>
        private GuardPlaceholder CreateGuard(string name, int level, NationType nation)
        {
            GameObject go = new GameObject(name);
            _spawned.Add(go);
            GuardPlaceholder guard = go.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo(name, level, nation);
            guard.SetRecruited(false);
            return guard;
        }
    }
}
