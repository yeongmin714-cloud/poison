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
    /// [2026-10-15 D4-C — 적대 영지 병사 전투] 계약 테스트 (HostileTerritoryCombat.cs) — EditMode 가능 부.
    /// - TerritoryState.playerHostile 플래그 set/get 라운드트립(세이브/영지별 상태 계약).
    /// - NotifyHostile 방어 경로: GuardManager 미건설 / 빈 영지 / 플레이어 부재에서 예외 없이 안전 반환.
    /// - NotifyHostile 정상 경로(테스트 전용 싱글턴 구성): 생존 비포섭 병사에게만
    ///   HostileToPlayerFaction 해제 + 공격 명령(SetCommandTarget(플레이어 위치, true)) + SetInCombat(true) 발행.
    ///
    /// ⚠ Play 게이트(EditMode 미실행 — 씬/물리 의존):
    ///   · 병사 실제 이동/공격/플레이어 데미지(Physics.OverlapSphere 타겟 해상, PerformAttack — GameObject 의존)
    ///   · RestaurantSystem.RunDailySale 훅 경유 트리거(확률 롤 + TimeManager 일일 틱)
    ///
    /// EditMode 규약: AddComponent는 Awake 미실행 → GuardManager.Instance는 리플렉션 세팅
    /// (CraftBenchTests/SoldierManagementLifecycleSummaryTests 패턴 준용).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class HostileTerritoryCombatTests
    {
        private static readonly NationType TestNation = NationType.East;
        private const int TestTerritoryIndex = 1;   // 기준 영지 = East_01

        private GameObject _managerGo;
        private GameObject _playerGo;               // "Player" 태그 더미 — TearDown에서 파괴(태그 오염 방지)
        private GuardManager _previousManager;
        private PlayerHealth _previousPlayerHealth; // 싱글턴 보존/복원 — 픽스처 순서 무관 결정론
        private readonly List<GameObject> _guardObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            // 싱글턴 보존/초기화 — 테스트 로컬 상태만 사용(SoldierManagementLifecycleSummaryTests 패턴 준용)
            _previousManager = GuardManager.Instance;
            typeof(GuardManager).GetProperty("Instance").SetValue(null, null);
            _previousPlayerHealth = PlayerHealth.Instance;
            typeof(PlayerHealth).GetProperty("Instance").SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _guardObjects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _guardObjects.Clear();

            if (_managerGo != null)
            {
                Object.DestroyImmediate(_managerGo);
                _managerGo = null;
            }

            if (_playerGo != null)
            {
                Object.DestroyImmediate(_playerGo);   // "Player" 태그 잔존 → 타 테스트 전제 오염 방지
                _playerGo = null;
            }

            // 원래 싱글턴 복원 — 다른 테스트/에디터 상태 오염 방지
            typeof(GuardManager).GetProperty("Instance").SetValue(null, _previousManager);
            typeof(PlayerHealth).GetProperty("Instance").SetValue(null, _previousPlayerHealth);
        }

        // ===== TerritoryState.playerHostile 플래그 계약 =====

        [Test]
        public void PlayerHostileFlag_SetAndRoundTrip()
        {
            var state = new TerritoryState();
            Assert.IsFalse(state.playerHostile, "기본값은 비적대(false)여야 한다");

            state.playerHostile = true;
            Assert.IsTrue(state.playerHostile, "세터 반영 — playerHostile = true 라운드트립");

            state.playerHostile = false;
            Assert.IsFalse(state.playerHostile, "세터 반영 — playerHostile = false 복귀");
        }

        // ===== NotifyHostile 방어 경로(예외 없이 안전 반환) =====

        [Test]
        public void NotifyHostile_NoGuardManager_SafeReturn()
        {
            // 미건설 — GuardManager.Instance == null 상태에서 예외 없이 반환해야 한다.
            Assert.IsNull(GuardManager.Instance, "전제: GuardManager 미건설");
            Assert.DoesNotThrow(() => HostileTerritoryCombat.NotifyHostile(TestNation, TestTerritoryIndex));
        }

        [Test]
        public void NotifyHostile_EmptyTerritory_SafeReturn()
        {
            CreateTestGuardManager();
            // 빈 영지 — 등록 병사 0명에서도 예외 없이 반환해야 한다.
            Assert.AreEqual(0, GuardManager.Instance.GetGuardCount(new TerritoryId(TestNation, TestTerritoryIndex)),
                "전제: 영지에 등록 병사 없음");
            Assert.DoesNotThrow(() => HostileTerritoryCombat.NotifyHostile(TestNation, TestTerritoryIndex));
        }

        [Test]
        public void NotifyHostile_NoPlayer_IssuesNoCommand()
        {
            CreateTestGuardManager();
            GuardPlaceholder guard = CreateTestGuard("D4C_NoPlayerGuard");
            GuardManager.Instance.RegisterGuard(new TerritoryId(TestNation, TestTerritoryIndex), guard);

            // 플레이어 부재(PlayerHealth.Instance == null + "Player" 태그 GameObject 없음) —
            // 명령을 발행하지 않고 안전 반환해야 한다(플레이어 좌표 없이 공격 명령 불가).
            Assert.IsNull(PlayerHealth.Instance, "전제: PlayerHealth.Instance 부재");
            Assert.IsNull(GameObject.FindGameObjectWithTag("Player"), "전제: 'Player' 태그 오브젝트 부재");
            Assert.DoesNotThrow(() => HostileTerritoryCombat.NotifyHostile(TestNation, TestTerritoryIndex));
            Assert.IsFalse(guard.HasCommand, "플레이어 부재 시 공격 명령이 발행되지 않아야 한다");
            Assert.IsFalse(guard.IsInCombat, "플레이어 부재 시 전투 상태 진입이 없어야 한다");
        }

        // ===== NotifyHostile 정상 경로(테스트 전용 싱글턴 구성) =====

        [Test]
        public void NotifyHostile_WithPlayer_CommandsAliveNonRecruitedGuardsOnly()
        {
            CreateTestGuardManager();

            // 플레이어 더미 — "Player" 태그 폴백 경로(PlayerHealth.Instance == null → 태그 탐색)
            _playerGo = new GameObject("D4C_TestPlayer");
            _playerGo.tag = "Player";
            _playerGo.transform.position = new Vector3(3f, 0f, 4f);

            GuardPlaceholder attacker = CreateTestGuard("D4C_Attacker");
            GuardPlaceholder recruited = CreateTestGuard("D4C_Recruited");
            recruited.SetRecruited(true);   // 플레이어 포섭 병사 — 명령 제외 대상
            GuardPlaceholder dead = CreateTestGuard("D4C_Dead");
            typeof(GuardPlaceholder).GetField("_isDead", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(dead, true);      // 사망 병사 — 명령 제외 대상(IsAlive false)

            var territoryId = new TerritoryId(TestNation, TestTerritoryIndex);
            GuardManager.Instance.RegisterGuard(territoryId, attacker);
            GuardManager.Instance.RegisterGuard(territoryId, recruited);
            GuardManager.Instance.RegisterGuard(territoryId, dead);

            Assert.DoesNotThrow(() => HostileTerritoryCombat.NotifyHostile(TestNation, TestTerritoryIndex));

            // 생존 비포섭 병사 — GuardHostilitySystem 3종 세트와 동일 계약
            Assert.IsTrue(attacker.HostileToPlayerFaction, "적대 게이트(HostileToPlayerFaction)가 해제되어야 한다");
            Assert.IsTrue(attacker.HasCommand, "공격 명령이 발행되어야 한다");
            Assert.IsTrue(attacker.IsAttackCommand, "명령이 공격 명령(true)이어야 한다");
            Assert.IsTrue(attacker.IsInCombat, "전투 상태가 활성화되어야 한다");
            Assert.LessOrEqual(Vector3.Distance(attacker.CommandTarget, _playerGo.transform.position), 0.01f,
                "명령 목표가 플레이어 현재 위치여야 한다");

            // 포섭 병사 — 명령 발행 금지
            Assert.IsFalse(recruited.HostileToPlayerFaction, "포섭 병사는 적대 게이트가 해제되지 않아야 한다");
            Assert.IsFalse(recruited.HasCommand, "포섭 병사는 공격 명령 대상이 아니다");

            // 사망 병사 — 명령 발행 금지
            Assert.IsFalse(dead.HasCommand, "사망 병사는 공격 명령 대상이 아니다");
        }

        // ===== 헬퍼 =====

        /// <summary>테스트 전용 GuardManager 구성 — AddComponent(Awake 미실행) + Instance 리플렉션 세팅.</summary>
        private void CreateTestGuardManager()
        {
            _managerGo = new GameObject("D4C_TestGuardManager");
            var manager = _managerGo.AddComponent<GuardManager>();
            typeof(GuardManager).GetProperty("Instance").SetValue(null, manager);
        }

        /// <summary>테스트 전용 GuardPlaceholder 구성 — Awake 미실행 환경에서 필요한 상태만 최소 세팅.</summary>
        private GuardPlaceholder CreateTestGuard(string name)
        {
            var go = new GameObject(name);
            _guardObjects.Add(go);
            return go.AddComponent<GuardPlaceholder>();
        }
    }
}
