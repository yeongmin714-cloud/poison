using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [154차 P4] 음식점 초기 등록가 모달(RestaurantInitialPriceModalUTK) + ConstructionManager.RestaurantPlacementSucceeded
    /// 정적 이벤트 계약 테스트.
    ///
    ///  1) 이벤트(실 TryPlace — ConstructionTests 선례 준용):
    ///     · 음식점 TryPlace 성공 → 정확히 1회 발화 + 인자 (nation, villageIndex, PermitFee(difficulty)).
    ///     · bp_forge(비음식점) 성공 → 미발화.
    ///     · 골드 부족 실패 경로 → 미발화(소비 없음).
    ///  2) 모달 커밋(생성자 + Bind — UTRoot 불필요):
    ///     · Bind → 42 입력 → CommitInitialPrice → 스냅샷 42.
    ///     · 사전값 7 상태에서 -5/abc 거부 + 상태 무변경.
    ///     · Show 후 999 입력 → Discard → 무변경 + 닫힘.
    ///     · TryParseInitialPrice 순수 경계(0/음수/비숫자/공백/null).
    ///  3) 브리지 멱등 — EditMode Keyboard.current 불확실 → 소스 어서션 + 리플렉션 Attach 2회/Detach 2회 무예외.
    /// </summary>
    public class RestaurantInitialPriceModalTests
    {
        private static readonly BindingFlags StaticPrivate = BindingFlags.NonPublic | BindingFlags.Static;

        private ConstructionManager _cm;
        private GameObject _statsGo;
        private PlayerStats _stats;   // [O8] Awake 미실행 환경 — 컴포넌트 참조 사용
        private GameObject _invGo;
        private PlayerInventory _inv;
        private VillagePlacementSystem.VillageInfo _village;   // GetAllVillages()[0] — 결정론 마을
        private string _key;           // nation_(index+1) 2자리 조합 영지 키
        private RestaurantInitialPriceModalUTK _modal;

        // 발화 계측
        private int _eventCount;
        private NationType _eventNation;
        private int _eventVillageIndex;
        private int _eventPermitFee;

        [SetUp]
        public void SetUp()
        {
            // ── PlayerStats — ConstructionTests 선례(컴포넌트 직접 참조, 1000G 시드) ──
            PlayerStats.ResetInstance();
            _statsGo = new GameObject("PlayerStats_154P4");
            _stats = _statsGo.AddComponent<PlayerStats>();
            _stats.AddGold(1000, "test_seed");

            // ── PlayerInventory — CraftBenchTests 선례(EditMode Awake 미실행 → 리플렉션 보장) ──
            PlayerInventory.ResetInstance();
            _invGo = new GameObject("TestInventory_154P4");
            _inv = _invGo.AddComponent<PlayerInventory>();
            var instProp = typeof(PlayerInventory).GetProperty("Instance");
            if (instProp != null && instProp.GetValue(null) == null)
                instProp.SetValue(null, _inv);
            if (_inv.GetAllSlots() == null)
            {
                var f = typeof(PlayerInventory).GetField("_slots",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var maxF = typeof(PlayerInventory).GetField("_maxSlots",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                int max = maxF != null ? (int)maxF.GetValue(_inv) : 30;
                f.SetValue(_inv, new PlayerInventory.ItemSlot[max]);
            }
            // bp_restaurant 요구 — 나무 5 + 돌 3(스펙 단일소스: BlueprintData.bp_restaurant).
            Assert.IsTrue(_inv.AddItem(PlayerInventory.Mat_Wood, 5), "나무 5 시딩");
            Assert.IsTrue(_inv.AddItem(PlayerInventory.Mat_Stone, 3), "돌 3 시딩");

            VillageDrugState.ResetAll();

            // ── 결정론 마을 — GetAllVillages()[0] (순서 고정: East 먼저, index 0) ──
            _village = VillagePlacementSystem.GetAllVillages()[0];
            _key = $"{_village.nation}_{_village.index + 1:D2}";   // 예: East_01 — (index+1) 2자리 조합

            // ── UTKWindowManager 오염 스냅샷(SoldierManagementLifecycleSummaryTests 선례) ──
            _prevUpdater = typeof(UTKWindowManager).GetField("_updater", StaticPrivate).GetValue(null);
            _prevWarned = typeof(UTKWindowManager).GetField("_warnedNoUpdater", StaticPrivate).GetValue(null);
        }

        private object _prevUpdater;
        private object _prevWarned;

        [TearDown]
        public void TearDown()
        {
            // 정적 이벤트 해제 — 도메인 잔존 구독 오염 차단
            ConstructionManager.RestaurantPlacementSucceeded -= OnRestaurantPlacementSucceeded;

            if (_cm != null && _cm.gameObject != null) Object.DestroyImmediate(_cm.gameObject);
            if (_statsGo != null) Object.DestroyImmediate(_statsGo);
            PlayerStats.ResetInstance();
            PlayerInventory.ResetInstance();
            if (_invGo != null) Object.DestroyImmediate(_invGo);
            VillageDrugState.ResetAll();

            // 모달 인스턴스 정리 — 개방 중이면 먼저 닫고 트리에서 제거
            if (_modal != null)
            {
                if (_modal.IsOpen) _modal.Hide();
                _modal.RemoveFromHierarchy();
                _modal = null;
            }

            // UTKWindowManager 복원 — EditMode DDOL/업데이터 오염 제거(선례 준용)
            var fUpdater = typeof(UTKWindowManager).GetField("_updater", StaticPrivate);
            var curUpdater = fUpdater != null ? fUpdater.GetValue(null) as Component : null;
            if (curUpdater != null && !ReferenceEquals(curUpdater, _prevUpdater))
                Object.DestroyImmediate(curUpdater.gameObject);
            if (fUpdater != null) fUpdater.SetValue(null, _prevUpdater);
            var fWarned = typeof(UTKWindowManager).GetField("_warnedNoUpdater", StaticPrivate);
            if (fWarned != null) fWarned.SetValue(null, _prevWarned);

            var fStack = typeof(UTKWindowManager).GetField("_openStack", StaticPrivate);
            if (fStack != null && fStack.GetValue(null) is IList stack)
                stack.Clear();
            ProjectName.Core.UITransitionState.AnyWindowOpen = false;
        }

        private void OnRestaurantPlacementSucceeded(NationType nation, int villageIndex, int permitFee)
        {
            _eventCount++;
            _eventNation = nation;
            _eventVillageIndex = villageIndex;
            _eventPermitFee = permitFee;
        }

        /// <summary>ConstructionTests.TerritoryCenter 동일 — DB 실측 영지 중심 좌표.</summary>
        private static Vector3 TerritoryCenter(string key)
        {
            var db = TerritoryDatabase.Instance;
            var def = db != null ? db.GetDefinition(key) : new TerritoryDefinition();
            return def.worldPosition;
        }

        /// <summary>기대 허가세 — 마을 기준 영지(인덱스 = villageIndex+1) 난이도 링 배율(순수 단일소스).</summary>
        private int ExpectedPermitFee()
        {
            var def = TerritoryDatabase.Instance.GetDefinition(_village.nation, _village.index + 1);
            return VillageDrugMath.PermitFee(def.difficulty);
        }

        // ═══════════════ 1. 이벤트 — 실 TryPlace(ConstructionTests 선례 준용) ═══════════════

        [Test]
        public void RestaurantPlacementSucceeded_TryPlaceSuccess_FiresOnce_WithExactArgs()
        {
            _cm = new GameObject("CM_RestaurantEvent").AddComponent<ConstructionManager>();
            ConstructionManager.RestaurantPlacementSucceeded += OnRestaurantPlacementSucceeded;

            int fee = ExpectedPermitFee();
            int goldBefore = _stats.Gold;

            // 마을 중심 배치 — TryFindRestaurantVillage 반경(radius+5m) 내 최근접 매칭
            var result = _cm.TryPlace(_key, "bp_restaurant", _village.center);

            StringAssert.Contains("건설 시작", result);
            Assert.AreEqual(1, _cm.Structures.Count, result);

            Assert.AreEqual(1, _eventCount, "음식점 배치 성공 — 정확히 1회 발화");
            Assert.AreEqual(_village.nation, _eventNation, "인자 nation");
            Assert.AreEqual(_village.index, _eventVillageIndex, "인자 villageIndex");
            Assert.AreEqual(fee, _eventPermitFee, "인자 permitFee = VillageDrugMath.PermitFee(def.difficulty)");

            // 골드 실측 — 시공 200G + 허가세 동시 차감(벤치 원칙 부수 실측)
            Assert.AreEqual(goldBefore - 200 - fee, _stats.Gold, "시공 200G + 허가세 차감");
        }

        [Test]
        public void RestaurantPlacementSucceeded_ForgePlacement_DoesNotFire()
        {
            // bp_forge — 장비 크래프트(나무 12/돌 8/철 6/가죽 4, 골드 100). 비음식점 성공 경로.
            Assert.IsTrue(_inv.AddItem(PlayerInventory.Mat_Wood, 12), "나무 추가 시딩");
            Assert.IsTrue(_inv.AddItem(PlayerInventory.Mat_Stone, 8), "돌 추가 시딩");
            Assert.IsTrue(_inv.AddItem(PlayerInventory.IronOre, 6), "철 시딩");
            Assert.IsTrue(_inv.AddItem(PlayerInventory.BoarLeather, 4), "가죽 시딩");

            _cm = new GameObject("CM_ForgeEvent").AddComponent<ConstructionManager>();
            ConstructionManager.RestaurantPlacementSucceeded += OnRestaurantPlacementSucceeded;

            // 성 중심 + 오프셋 — ConstructionTests 배치 패턴 준용(음식점 분기 미유입)
            Vector3 pos = TerritoryCenter(_key) + new Vector3(3f, 0f, 3f);
            var result = _cm.TryPlace(_key, "bp_forge", pos);

            StringAssert.Contains("건설 시작", result);
            Assert.AreEqual(1, _cm.Structures.Count, result);
            Assert.AreEqual(0, _eventCount, "비음식점(bp_forge) 성공 — 무발화");
        }

        [Test]
        public void RestaurantPlacementSucceeded_InsufficientGold_DoesNotFire()
        {
            // 무골드 PlayerStats로 교체 — 합산 판정(시공 200 + 허가세) 실패 경로
            Object.DestroyImmediate(_statsGo);
            _statsGo = new GameObject("PlayerStats_154P4_Poor");
            _stats = _statsGo.AddComponent<PlayerStats>();
            Assert.AreEqual(0, _stats.Gold, "무골드 시드");

            _cm = new GameObject("CM_PoorEvent").AddComponent<ConstructionManager>();
            ConstructionManager.RestaurantPlacementSucceeded += OnRestaurantPlacementSucceeded;

            var result = _cm.TryPlace(_key, "bp_restaurant", _village.center);

            StringAssert.Contains("골드 부족", result);
            Assert.AreEqual(0, _cm.Structures.Count, "실패 경로 — 등록 없음");
            Assert.AreEqual(0, _eventCount, "골드 부족 실패 — 무발화");
            // 재료 무소비(벤치 원칙) — 나무 5/돌 3 유지
            Assert.AreEqual(5, _inv.GetItemCount(ConstructionManager.MaterialWoodItemId), "나무 무소비");
            Assert.AreEqual(3, _inv.GetItemCount(ConstructionManager.MaterialStoneItemId), "돌 무소비");
        }

        // ═══════════════ 2. 모달 커밋(생성자 + Bind — UTRoot 불필요) ═══════════════

        private RestaurantInitialPriceModalUTK CreateBoundModal()
        {
            _modal = new RestaurantInitialPriceModalUTK();   // public 생성자 — 테스트 진입점
            _modal.Bind(_village.nation, _village.index, ExpectedPermitFee());
            return _modal;
        }

        private static TextField PriceField(RestaurantInitialPriceModalUTK modal)
        {
            var field = modal.Content.Q<TextField>("InitialPriceField");
            Assert.IsNotNull(field, "InitialPriceField 존재(테스트 진입 계약)");
            return field;
        }

        [Test]
        public void Bind_DefaultValue_UsesRegisteredPriceOrRecommended()
        {
            var modal = CreateBoundModal();
            var field = PriceField(modal);

            // ResetAll 직후 — registeredPrice 0 → 권장가 폴백(RecommendedPrice(DefaultFoodBasePrice=10, addiction=0) = 10)
            Assert.AreEqual(
                VillageDrugMath.RecommendedPrice(RestaurantSystem.DefaultFoodBasePrice, 0).ToString(),
                field.value,
                "기본값 = 기등록가 > 0 ? 그 값 : 권장가(진실원 단일소스)");

            // 기등록가 > 0 — 스냅샷값 우선
            VillageDrugState.SetRegisteredPrice(_village.nation, _village.index, 77);
            modal.Bind(_village.nation, _village.index, ExpectedPermitFee());
            Assert.AreEqual("77", field.value, "기등록가 77 > 0 — 스냅샷값 우선");
        }

        [Test]
        public void CommitInitialPrice_Valid42_UpdatesSnapshotRegisteredPrice()
        {
            var modal = CreateBoundModal();
            var field = PriceField(modal);

            field.value = "42";
            modal.CommitInitialPrice();

            Assert.AreEqual(42, VillageDrugState.GetSnapshot(_village.nation, _village.index).registeredPrice,
                "커밋 — SetRegisteredPrice 42");
            Assert.IsFalse(modal.IsOpen, "커밋 후 닫힘");
        }

        [Test]
        public void CommitInitialPrice_RejectsNegativeAndNonNumeric_StateUnchanged()
        {
            VillageDrugState.SetRegisteredPrice(_village.nation, _village.index, 7);
            var modal = CreateBoundModal();
            var field = PriceField(modal);
            Assert.AreEqual("7", field.value, "사전값 7 — 기본값 스냅샷 우선");

            field.value = "-5";
            modal.CommitInitialPrice();
            Assert.AreEqual(7, VillageDrugState.GetSnapshot(_village.nation, _village.index).registeredPrice,
                "음수 거부 — 상태 무변경");

            field.value = "abc";
            modal.CommitInitialPrice();
            Assert.AreEqual(7, VillageDrugState.GetSnapshot(_village.nation, _village.index).registeredPrice,
                "비숫자 거부 — 상태 무변경");
        }

        [Test]
        public void Discard_AfterShow_StateUnchanged_AndClosed()
        {
            // Show()는 UTKWindowManager.Register → DontDestroyOnLoad로 EditMode 실행 불가(UTKWindowBase.Show 실측).
            // 개방 상태만 반사 시뮬레이션(_isOpen) — 실제 닫힘 경로(Hide → Unregister/OnWindowClosed/Detach)는 실측한다.
            var modal = CreateBoundModal();
            var isOpenField = typeof(UTKWindowBase).GetField("_isOpen",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(isOpenField, "UTKWindowBase._isOpen 필드 존재");
            isOpenField.SetValue(modal, true);
            modal.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
            Assert.IsTrue(modal.IsOpen, "개방 상태 시뮬레이션 실측");

            var field = PriceField(modal);
            field.value = "999";
            modal.Discard();

            Assert.IsFalse(modal.IsOpen, "Discard — 닫힘(Hide 실행)");
            Assert.AreEqual(0, VillageDrugState.GetSnapshot(_village.nation, _village.index).registeredPrice,
                "Discard — 상태 무변경(999 미커밋)");
        }

        [Test]
        public void TryParseInitialPrice_PureBoundaries()
        {
            // 수용 — 0 이상 정수(Trim 규약)
            Assert.IsTrue(RestaurantInitialPriceModalUTK.TryParseInitialPrice("0", out int zero), "0 수용");
            Assert.AreEqual(0, zero);
            Assert.IsTrue(RestaurantInitialPriceModalUTK.TryParseInitialPrice("7", out int seven), "양수 수용");
            Assert.AreEqual(7, seven);
            Assert.IsTrue(RestaurantInitialPriceModalUTK.TryParseInitialPrice(" 42 ", out int trimmed), "공백 Trim 수용");
            Assert.AreEqual(42, trimmed);

            // 거부 — 음수/비숫자/공백/null/소수
            Assert.IsFalse(RestaurantInitialPriceModalUTK.TryParseInitialPrice("-5", out _), "음수 거부");
            Assert.IsFalse(RestaurantInitialPriceModalUTK.TryParseInitialPrice("abc", out _), "비숫자 거부");
            Assert.IsFalse(RestaurantInitialPriceModalUTK.TryParseInitialPrice("", out _), "빈 문자열 거부");
            Assert.IsFalse(RestaurantInitialPriceModalUTK.TryParseInitialPrice("   ", out _), "공백만 거부(Trim → 빈 문자열)");
            Assert.IsFalse(RestaurantInitialPriceModalUTK.TryParseInitialPrice(null, out _), "null 거부");
            Assert.IsFalse(RestaurantInitialPriceModalUTK.TryParseInitialPrice("12.5", out _), "소수 거부(int.TryParse)");
        }

        // ═══════════════ 3. 브리지/부트 계약 — 소스 어서션 + 리플렉션 멱등 실측 ═══════════════

        private static string CmSrc() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ConstructionManager.cs");
        private static string ModalSrc() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/RestaurantInitialPriceModalUTK.cs");

        [Test]
        public void SourceContract_EventDeclaration_GuardedFire_AndSubscriptionHygiene()
        {
            string cm = CmSrc();

            // Systems 측 — 이벤트 신설 + 가드 발화(음식점 성공 한정)
            Assert.That(cm, Does.Contain(
                    "public static event System.Action<NationType, int, int> RestaurantPlacementSucceeded"),
                "[154차 P4] Systems→UI 단방향 정적 이벤트 선언");
            Assert.That(cm, Does.Contain(
                    "RestaurantPlacementSucceeded?.Invoke(restaurantNation, restaurantVillageIndex, permitFee)"),
                "발화 인자 (nation, villageIndex, permitFee)");
            Assert.Less(cm.IndexOf("if (restaurantNation != NationType.None)", System.StringComparison.Ordinal),
                cm.IndexOf("RestaurantPlacementSucceeded?.Invoke", System.StringComparison.Ordinal),
                "가드(restaurantNation != None)가 발화 선행 — 실패/비음식점 경로 무발화 구조");

            string modal = ModalSrc();

            // UI 측 — 구독 전 해제(도메인 리로드/재진입 중복 방지) + Bootstrap 자가부트
            int unsub = modal.IndexOf("RestaurantPlacementSucceeded -= OnRestaurantPlacementSucceeded;", System.StringComparison.Ordinal);
            int sub = modal.IndexOf("RestaurantPlacementSucceeded += OnRestaurantPlacementSucceeded;", System.StringComparison.Ordinal);
            Assert.Greater(unsub, -1, "구독 전 해제 존재");
            Assert.Greater(sub, -1, "구독 존재");
            Assert.Less(unsub, sub, "해제가 구독에 선행(구독 전 해제 패턴)");

            // EnsureAttached 멱등 — 고아 가드 + parent null 재부착
            Assert.That(modal, Does.Contain("private static void EnsureAttached()"), "EnsureAttached 존재");
            Assert.That(modal, Does.Contain("if (_instance.parent == null)"), "parent null 가드 재부착");

            // 창 닫힘 — 브리지 Detach 보장([151차 P0] 패턴 이식)
            int closedIdx = modal.IndexOf("protected override void OnWindowClosed()", System.StringComparison.Ordinal);
            Assert.Greater(closedIdx, -1, "OnWindowClosed 오버라이드 존재");
            Assert.That(modal.Substring(closedIdx), Does.Contain("DetachPriceKeyboardBridge();"),
                "창 닫힘 — 편집 브리지 정리 보장");

            // Attach 멱등 — 이중 부착 방지 선행 해제
            Assert.That(modal, Does.Contain("DetachPriceKeyboardBridge();   // 이중 부착 방지(멱등)"),
                "Attach 진입 즉시 Detach — 이중 부착 방지");
        }

        [Test]
        public void KeyboardBridge_AttachTwiceDetachTwice_NoException()
        {
            var modal = CreateBoundModal();
            var field = PriceField(modal);

            var attach = typeof(RestaurantInitialPriceModalUTK).GetMethod("AttachPriceKeyboardBridge",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var detach = typeof(RestaurantInitialPriceModalUTK).GetMethod("DetachPriceKeyboardBridge",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(attach, "AttachPriceKeyboardBridge 리플렉션 접근");
            Assert.IsNotNull(detach, "DetachPriceKeyboardBridge 리플렉션 접근");

            // EditMode Keyboard.current 불확실 — 부재 시 조기 반환, 존재 시 멱등 부착. 어느 쪽이든 무예외 계약.
            Assert.DoesNotThrow(() => attach.Invoke(modal, new object[] { field }), "Attach 1회");
            Assert.DoesNotThrow(() => attach.Invoke(modal, new object[] { field }), "Attach 2회 — 멱등");
            Assert.DoesNotThrow(() => detach.Invoke(modal, null), "Detach 1회");
            Assert.DoesNotThrow(() => detach.Invoke(modal, null), "Detach 2회 — 멱등");

            var fBridgeField = typeof(RestaurantInitialPriceModalUTK).GetField("_bridgeField",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNull(fBridgeField.GetValue(modal), "Detach 후 _bridgeField null — 브리지 완전 해제");
        }
    }
}
