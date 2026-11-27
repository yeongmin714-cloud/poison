using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// LordPersonalitySystem.GetLiveHiredCount — 등록 기준 실제 병력(중복 제외) 집계 검증.
    /// 누적 카운트(LaborMarketSystem.GetHiredCount)와 독립적으로 동작해야 한다.
    /// 등록/정리와 집계 모두 테스트가 생성한 로컬 GuardManager 컴포넌트를 명시적으로 전달하여 수행하며,
    /// 정적 GuardManager.Instance에 의존하지 않는다.
    /// </summary>
    public class LordLiveHiredCountTests
    {
        private static readonly TerritoryId LordId = new TerritoryId(NationType.North, 1);

        private GameObject _managerGo;
        private GuardManager _manager;
        private GameObject _guardGo;
        private GuardPlaceholder _guard;

        [SetUp]
        public void SetUp()
        {
            _managerGo = new GameObject("LiveHiredCount_GuardManager");
            _manager = _managerGo.AddComponent<GuardManager>();

            _guardGo = new GameObject("LiveHiredCount_Guard");
            _guard = _guardGo.AddComponent<GuardPlaceholder>();
            _guard.SetGuardInfo("live테스트병사", 3, NationType.East);
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null && _guard != null)
                _manager.RemoveGuardFromAllTerritories(_guard);

            if (_guardGo != null) Object.DestroyImmediate(_guardGo);
            if (_managerGo != null) Object.DestroyImmediate(_managerGo);

            _manager = null;
            _managerGo = null;
            _guard = null;
            _guardGo = null;
        }

        [Test]
        public void GetLiveHiredCount_NoRegisteredGuards_IsZero()
        {
            Assert.AreEqual(0, LordPersonalitySystem.GetLiveHiredCount(LordId, _manager));
        }

        [Test]
        public void GetLiveHiredCount_NonAIHiredGuard_IsExcluded()
        {
            _manager.RegisterGuard(LordId, _guard);   // IsAIHired = false (기본 영지 병사)

            Assert.AreEqual(0, LordPersonalitySystem.GetLiveHiredCount(LordId, _manager));
        }

        [Test]
        public void GetLiveHiredCount_SameGuardRegisteredTwice_CountsOnce()
        {
            _guard.IsAIHired = true;
            _manager.RegisterGuard(LordId, _guard);
            _manager.RegisterGuard(LordId, _guard);

            Assert.AreEqual(1, LordPersonalitySystem.GetLiveHiredCount(LordId, _manager));
        }
    }
}
