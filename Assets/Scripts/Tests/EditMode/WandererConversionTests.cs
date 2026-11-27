using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [119차] 방랑자 전환 3경로 계약 테스트 — 방출(ReleaseGuard) / AI 재고용(ProcessAILordHiring) / 탈영(WandererDesertionSystem).
    /// GuardPlaceholder 방랑 API(IsWanderer·BecomeWanderer·EndWanderer)는 [119차 API 계약] 시그니처와 일치 전제.
    /// EditMode 규약: AddComponent는 Awake 미실행 → GuardManager.Instance는 리플렉션 세팅(HostileTerritoryCombatTests 준용).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class WandererConversionTests
    {
        private const int TestTerritoryIndex = 1;
        private static readonly TerritoryId LordId = new TerritoryId(NationType.North, TestTerritoryIndex);
        private static readonly TerritoryId RosterId = new TerritoryId(NationType.East, TestTerritoryIndex);

        private TerritoryState _state;
        private TerritoryOwnership _prevOwnership;
        private int _prevGold;
        private GameObject _parentGo;
        private GameObject _guardGo;
        private GuardPlaceholder _guard;
        private GameObject _managerGo;
        private GuardManager _previousManager;

        [SetUp]
        public void SetUp()
        {
            SoldierWageSystem.ResetAll();
            LaborMarketSystem.ResetAll();

            TerritoryDatabase db = TerritoryDatabase.Instance;
            _state = db.GetState(LordId);
            _prevOwnership = _state.ownership;
            _prevGold = _state.territoryGold;
            db.SetOwnership(LordId, TerritoryOwnership.LordOwned);
            _state.territoryGold = 100000;

            // GuardManager 리플렉션 세팅 — AddComponent는 Awake 미실행. 기존 인스턴스는 TearDown에서 복원.
            _previousManager = GuardManager.Instance;
            typeof(GuardManager).GetProperty("Instance").SetValue(null, null);
            _managerGo = new GameObject("WandererConversion_TestGuardManager");
            var manager = _managerGo.AddComponent<GuardManager>();
            typeof(GuardManager).GetProperty("Instance").SetValue(null, manager);

            CreateGuard("방랑전환테스트병사");
        }

        [TearDown]
        public void TearDown()
        {
            LaborMarketSystem.ResetAll();
            SoldierWageSystem.ResetAll();
            // GuardManager.Instance 복원 — 리플렉션 오염 방지(다른 테스트 스위트 보호).
            typeof(GuardManager).GetProperty("Instance").SetValue(null, _previousManager);
            if (_guardGo != null) Object.DestroyImmediate(_guardGo);
            if (_parentGo != null) Object.DestroyImmediate(_parentGo);
            if (_managerGo != null) Object.DestroyImmediate(_managerGo);
            _state.ownership = _prevOwnership;
            _state.territoryGold = _prevGold;
        }

        /// <summary>테스트 병사 생성 — 기존 병사가 있으면 먼저 제거(1병사 스캔 전제 유지).</summary>
        private void CreateGuard(string guardName)
        {
            if (_guardGo != null) Object.DestroyImmediate(_guardGo);
            _guardGo = new GameObject(guardName);
            _guard = _guardGo.AddComponent<GuardPlaceholder>();
            _guard.SetGuardInfo(guardName, 1, NationType.East);
            _guard.SetRecruited(false);
        }

        // ── 경로 1: 방출 → 방랑자 전환 ─────────────────────────────────

        [Test]
        public void ReleaseGuard_MakesWanderer()
        {
            // 부모 GO 아래 병사 생성 — 방출 시 세계(루트)로 배회 전환 확인용.
            _parentGo = new GameObject("WandererConversion_Parent");
            _guardGo.transform.SetParent(_parentGo.transform);

            LaborMarketSystem.ReleaseGuard(_guard);

            Assert.IsTrue(_guard.IsWanderer, "방출 병사는 방랑자로 전환되어야 한다");
            Assert.AreEqual(1, LaborMarketSystem.MarketPoolSize(), "시장 풀 등록 유지(AI 재고용 대상)");
            Assert.IsNull(_guard.transform.parent, "방랑 개시 — 영지 계층에서 분리");
        }

        [Test]
        public void ReleaseGuard_Idempotent()
        {
            LaborMarketSystem.ReleaseGuard(_guard);

            Assert.DoesNotThrow(() => LaborMarketSystem.ReleaseGuard(_guard), "중복 방출은 멱등 — 예외 없음");
            Assert.AreEqual(1, LaborMarketSystem.MarketPoolSize(), "풀 중복 등록 방지");
        }

        // ── 경로 2: 방랑자 AI 재고용 → 방랑 해제 ───────────────────────

        [Test]
        public void ReleaseThenAIHire_ClearsWanderer()
        {
            LaborMarketSystem.ReleaseGuard(_guard);
            Assert.IsTrue(_guard.IsWanderer, "전제: 방출로 방랑자화");

            // 결정론 할당(0~2 해시)이 0인 날을 회피 — 40일 루프에서 풀 감소일을 찾는다.
            int before = LaborMarketSystem.MarketPoolSize();
            int hiredDay = -1;
            for (int day = 1; day <= 40; day++)
            {
                LaborMarketSystem.ProcessAILordHiring(day);
                if (LaborMarketSystem.MarketPoolSize() < before)
                {
                    hiredDay = day;
                    break;
                }
            }

            Assert.Greater(hiredDay, 0, "40일 내 AI 재고용 성립(입단비 100000G 충분)");
            Assert.IsFalse(_guard.IsWanderer, "재고용 시 방랑 해제");
            Assert.IsTrue(_guard.IsAIHired, "AI 고용 마커 부여");
        }

        // ── 경로 3: 충성도 탈영 → 방랑자 전환 ──────────────────────────

        [Test]
        public void Desertion_ConvertsLowLoyaltyRecruit()
        {
            _guard.SetRecruited(true);
            _guard.Loyalty = -75f;   // 임계(-70) 이하
            GuardManager.Instance.RegisterGuard(RosterId, _guard);

            int deserted = WandererDesertionSystem.ProcessDesertions();

            Assert.AreEqual(1, deserted, "임계 이하 포섭 병사 1명 탈영");
            Assert.IsTrue(_guard.IsWanderer, "탈영 병사는 방랑자로 전환");
            Assert.IsFalse(_guard.IsRecruited, "탈영 시 포섭 해제");
            Assert.IsFalse(GuardManager.Instance.GetGuardsInTerritory(RosterId).Contains(_guard), "로스터 제거");
        }

        [Test]
        public void Desertion_SkipsHealthyAndGatekeeper()
        {
            // (a) 충성도 임계 위(건강) 병사 — 탈영 없음
            _guard.SetRecruited(true);
            _guard.Loyalty = -30f;
            Assert.AreEqual(0, WandererDesertionSystem.ProcessDesertions(), "임계(-70) 위 병사는 탈영하지 않는다");
            Assert.IsFalse(_guard.IsWanderer, "건강 병사 방랑자화 없음");

            // (b) 게이트키퍼 — 충성도 임계 이하여도 TerritoryGateSystem 구조 보호로 탈영 제외
            CreateGuard("게이트키퍼탈영테스트");
            _guard.SetRecruited(true);
            _guard.IsGatekeeper = true;   // public 프로퍼티 세터 — Awake 미실행이라 _isGatekeeper 동기화 없음 → 직접 세팅
            _guard.Loyalty = -75f;

            Assert.AreEqual(0, WandererDesertionSystem.ProcessDesertions(), "게이트키퍼는 탈영 대상에서 제외");
            Assert.IsFalse(_guard.IsWanderer, "게이트키퍼는 방랑자화되지 않는다");
            Assert.IsTrue(_guard.IsRecruited, "게이트키퍼 포섭 상태 유지");
        }

        [Test]
        public void Desertion_SkipsAlreadyWanderer()
        {
            LaborMarketSystem.ReleaseGuard(_guard);   // 방출 → 이미 방랑자
            Assert.IsTrue(_guard.IsWanderer, "전제: 이미 방랑자 상태");

            Assert.AreEqual(0, WandererDesertionSystem.ProcessDesertions(), "이미 방랑자인 병사는 탈영 처리에서 제외");
        }
    }
}
