using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core.Data;      // NationType/VillageDrugState/VillageDrugSnapshot/RestaurantStockItem
using ProjectName.Systems;        // RestaurantSystem/GuardManager
using ProjectName.UI.Toolkit;     // RestaurantWindowUTK — 테스트 대상 순수 표시 헬퍼/개방 API
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-16 88차 Part B — 음식점 관리 창 Figma 재구현(RestaurantWindowUTK.cs)] 계약 테스트 — EditMode 가능 부.
    ///
    /// [88차 갱신] Figma 237:792 전면 수용으로 기존 섹션(병사 배치/중독 게이지/수요 문구)이 창에서 제거됨:
    ///  · 제거 — FormatDemandLine / AddictionGradeText / FormatStockRow / ResolveDemandBasePrice /
    ///    FindAvailableGuard 계약(삭제 섹션 관련 — Figma 부재).
    ///  · 유지 — Open 미건설 안전 반환(진입점 계약), AssignGuardFromPlayer 가용 병사 없음 무변경(시스템 계약 — A4 자동배치 보호).
    ///  · 신규 — Figma 데이터 배선 순수 헬퍼: FormatSlotSummary('N / 40 슬롯') / FormatStockCounter('N / 6') /
    ///    IsOpenHour·FormatBusinessBadge(영업 배지) / ResolveRowRecommendedPrice / ResolveRowSalePrice /
    ///    ComputeRowSaleGold / FormatAfterPlaceLine / ComputeFreeStockSlots.
    ///    (치수/색/좌표 진실치는 RestaurantWindowFigmaTests 소스 어서션 담당.)
    ///
    /// ⚠ Play 게이트(EditMode 미실행 — GameObject 의존부):
    ///   · 실제 창 개방/표시 경로 — UIRoot 부착(UIToolkitBootstrap), Show/폴링/ApplyCanvasFit(VisualElement 렌더 의존)
    ///   · 입고/수령 버튼 경유 실트랜잭션 — PlayerInventory/PlayerStats 실슬롯 의좐(RestaurantSystemTests 담당)
    ///
    /// EditMode 규약: AddComponent는 Awake 미실행 → GuardManager.Instance는 리플렉션 세팅/원복
    /// (HostileTerritoryCombatTests 선례 패턴 준용).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public sealed class RestaurantWindowTests
    {
        private static readonly NationType TestNation = NationType.East;

        // 마을 키 — SetUp에서 VillageDrugState.ResetAll()로 전면 초기화하므로 테스트 간 간섭 없음.
        private const int TestUndevelopedVillage = 12; // 미건설 개방 테스트
        private const int TestAssignVillage = 2;       // 병사 배치 실패(시스템 계약 유지)

        private GameObject _managerGo;
        private GuardManager _previousManager;          // GuardManager.Instance 보존/복원 — 픽스처 순서 무관 결정론
        private object _prevWindowInstance;             // RestaurantWindowUTK._instance 정적 캐시 보존/복원

        [SetUp]
        public void SetUp()
        {
            VillageDrugState.ResetAll();

            // 싱글턴 보존/초기화 — GuardManager 리플렉션 세팅/원복 선례(HostileTerritoryCombatTests) 준용
            _previousManager = GuardManager.Instance;
            typeof(GuardManager).GetProperty("Instance").SetValue(null, null);

            // RestaurantWindowUTK 정적 캐시 클리어 — EditMode에선 통상 null이지만 결정론 보장을 위해 강제.
            FieldInfo instanceField = typeof(RestaurantWindowUTK)
                .GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            _prevWindowInstance = instanceField?.GetValue(null);
            instanceField?.SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            VillageDrugState.ResetAll();

            if (_managerGo != null)
            {
                Object.DestroyImmediate(_managerGo);
                _managerGo = null;
            }

            // 원래 싱글턴/캐시 복원 — 다른 테스트·에디터 상태 오염 방지(선례 패턴)
            typeof(GuardManager).GetProperty("Instance").SetValue(null, _previousManager);
            typeof(RestaurantWindowUTK).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, _prevWindowInstance);
        }

        // ===== 1. Open — 미건설 마을 안전 반환(진입점 계약 — 시그니처 불변) =====

        [Test]
        public void Open_UndevelopedVillage_SafeNoInstance()
        {
            VillageDrugSnapshot before = VillageDrugState.GetSnapshot(TestNation, TestUndevelopedVillage);
            Assert.IsFalse(before.restaurantBuilt, "전제: 미건설 마을(ResetAll 직후 기본값 스냅샷)");

            Assert.DoesNotThrow(() => RestaurantWindowUTK.Open(TestNation, TestUndevelopedVillage),
                "미건설 마을 개방은 예외 없이 로그만 남기고 반환해야 한다(멱등/EditMode 무해)");

            Assert.IsNull(RestaurantWindowUTK.Instance, "미건설 마을 개방은 창 Instance를 생성하지 않아야 한다");
        }

        // ===== 2. FormatSlotSummary — 'N / 40 슬롯' (실측 _maxSlots=40 — Figma 목업치 15 미채용) =====

        [Test]
        public void FormatSlotSummary_UsesMeasuredFortySlots()
        {
            Assert.AreEqual("12 / 40 슬롯", RestaurantWindowUTK.FormatSlotSummary(12, 40),
                "사용 12 / 전체 40 — 'N / 40 슬롯' 형식(실측 상한)");
            Assert.AreEqual("0 / 40 슬롯", RestaurantWindowUTK.FormatSlotSummary(0, 40),
                "빈 인벤 — 0 / 40");
            Assert.AreEqual("0 / 0 슬롯", RestaurantWindowUTK.FormatSlotSummary(-3, -1),
                "음수 입력 방어 — 0 클램프");
        }

        // ===== 3. FormatStockCounter — 'N / 6' (RestaurantSystem.MaxStockSlots 실측 상한) =====

        [Test]
        public void FormatStockCounter_UsesMaxStockSlotsSix()
        {
            Assert.AreEqual(6, RestaurantSystem.MaxStockSlots,
                "[88차 A4] 슬롯 상한 상수 6 — Figma 'N / 6' 계약의 단일 소스");
            Assert.AreEqual("2 / 6", RestaurantWindowUTK.FormatStockCounter(2, RestaurantSystem.MaxStockSlots),
                "재고 2슬롯 — '2 / 6' 형식");
            Assert.AreEqual("0 / 6", RestaurantWindowUTK.FormatStockCounter(0, RestaurantSystem.MaxStockSlots),
                "재고 없음 — '0 / 6'");
            Assert.AreEqual("0 / 6", RestaurantWindowUTK.FormatStockCounter(-1, RestaurantSystem.MaxStockSlots),
                "음수 입력 방어 — 0 클램프");
        }

        // ===== 4. IsOpenHour/FormatBusinessBadge — 영업 배지(TimeManager.Hour 8~22 실측 판정) =====

        [Test]
        public void IsOpenHour_BadgeBands()
        {
            (int hour, bool open)[] cases =
            {
                (0, false), (7, false),          // 폐업 — '영업 종료'
                (8, true), (12, true), (21, true), // 영업 — 8≤h<22
                (22, false), (23, false),        // 22시 경계 — 폐업
            };
            foreach ((int hour, bool open) in cases)
            {
                Assert.AreEqual(open, RestaurantWindowUTK.IsOpenHour(hour),
                    $"Hour {hour} — 영업 판정 8≤h<22 경계 정합");
                Assert.AreEqual(open ? "영업 중" : "영업 종료", RestaurantWindowUTK.FormatBusinessBadge(hour),
                    $"Hour {hour} — 배지 문구 정합");
            }
        }

        // ===== 5. ResolveRowRecommendedPrice — 마약=RecommendedPrice(수학 단일 소스) / 일반=basePrice =====

        [Test]
        public void ResolveRowRecommendedPrice_DruggedUsesMath_NormalUsesBase()
        {
            int basePrice = 20;
            float addiction = 60f;
            int expected = VillageDrugMath_WrapperRecommended(basePrice, addiction);
            Assert.AreEqual(expected, RestaurantWindowUTK.ResolveRowRecommendedPrice(true, basePrice, addiction),
                "마약 행 권장가 — VillageDrugMath.RecommendedPrice(basePrice, addiction) 실측 단일 소스");
            Assert.AreEqual(basePrice, RestaurantWindowUTK.ResolveRowRecommendedPrice(false, basePrice, addiction),
                "일반 행 권장가 — basePrice 그대로(수학 미적용)");
            Assert.AreEqual(0, RestaurantWindowUTK.ResolveRowRecommendedPrice(false, 0, 0f),
                "무가격(0) 일반 행 — 0 표시(위조 금지)");
        }

        /// <summary>VillageDrugMath.RecommendedPrice 직접 위임 확인용 래퍼(테스트 내 단일 참조점).</summary>
        private static int VillageDrugMath_WrapperRecommended(int basePrice, float addiction)
            => ProjectName.Core.Utils.VillageDrugMath.RecommendedPrice(basePrice, addiction);

        // ===== 6. ResolveRowSalePrice/ComputeRowSaleGold — 마약=등록가 / 일반=기본가, 판매금=soldTotal×가격 =====

        [Test]
        public void RowSalePriceAndGold_Mapping()
        {
            Assert.AreEqual(25, RestaurantWindowUTK.ResolveRowSalePrice(true, 20, 25),
                "마약 행 판매 가격 = 마을 registeredPrice(TextField — SetRegisteredPrice 경유)");
            Assert.AreEqual(20, RestaurantWindowUTK.ResolveRowSalePrice(false, 20, 25),
                "일반 행 판매 가격 = basePrice 고정 표시(등록가 무시)");

            Assert.AreEqual(75, RestaurantWindowUTK.ComputeRowSaleGold(3, 25),
                "행 판매금 = soldTotal × 판매 가격");
            Assert.AreEqual(0, RestaurantWindowUTK.ComputeRowSaleGold(0, 25), "판매 0 — 판매금 0");
            Assert.AreEqual(0, RestaurantWindowUTK.ComputeRowSaleGold(-2, 25), "음수 soldTotal 방어");
            Assert.AreEqual(0, RestaurantWindowUTK.ComputeRowSaleGold(3, -5), "음수 가격 방어");
        }

        // ===== 7. FormatAfterPlaceLine — '배치 후 보유 N개 · 예상 판매금 N G'(=N×가격) =====

        [Test]
        public void FormatAfterPlaceLine_PredictsRemainingAndGold()
        {
            Assert.AreEqual("배치 후 보유 3개 · 예상 판매금 24 G",
                RestaurantWindowUTK.FormatAfterPlaceLine(5, 2, 12),
                "보유 5 - 배치 2 = 3, 예상 판매금 = 2×12 = 24 G");
            Assert.AreEqual("배치 후 보유 0개 · 예상 판매금 30 G",
                RestaurantWindowUTK.FormatAfterPlaceLine(3, 3, 10),
                "전량 배치 — 잔여 0(음수 방어)");
        }

        // ===== 8. ComputeFreeStockSlots — 'N칸 사용 가능' = 상한 6 − 현재 슬롯 수 =====

        [Test]
        public void ComputeFreeStockSlots_SubtractsFromSix()
        {
            Assert.AreEqual(6, RestaurantWindowUTK.ComputeFreeStockSlots(0), "재고 없음 — 6칸 사용 가능");
            Assert.AreEqual(4, RestaurantWindowUTK.ComputeFreeStockSlots(2), "재고 2 — 4칸 사용 가능");
            Assert.AreEqual(0, RestaurantWindowUTK.ComputeFreeStockSlots(6), "상한 도달 — 0칸");
            Assert.AreEqual(0, RestaurantWindowUTK.ComputeFreeStockSlots(9), "상한 초과 입력 방어 — 0 하한 클램프");
        }

        // ===== 9. AssignGuardFromPlayer — 가용 병사 없음(시스템 계약 유지 — A4 자동배치 보호) =====

        [Test]
        public void AssignGuardFromPlayer_NoGuard_ReturnsNull_NoStateChange()
        {
            VillageDrugSnapshot before = VillageDrugState.GetSnapshot(TestNation, TestAssignVillage);
            Assert.AreEqual(string.Empty, before.assignedGuardId, "전제: 미배치(빈 문자열 규약)");

            // 빈 GuardManager 구성(가용 병사 없음) — AddComponent(Awake 미실행) + Instance 리플렉션 세팅(선례 패턴)
            CreateTestGuardManager();
            Assert.AreEqual(0, GuardManager.Instance.GetAllPlayerGuards().Count,
                "전제: 가용(플레이어 소유/포섭) 병사 0명");

            string assignedId = RestaurantSystem.AssignGuardFromPlayer(TestNation, TestAssignVillage);
            Assert.IsNull(assignedId, "가용 병사 없음 — 배치 실패(null)이어야 한다");

            VillageDrugSnapshot after = VillageDrugState.GetSnapshot(TestNation, TestAssignVillage);
            Assert.AreEqual(string.Empty, after.assignedGuardId,
                "배치 실패 시 assignedGuardId 빈문자열이 유지되어야 한다(마을 상태 무변경)");
            Assert.AreEqual(0, VillageDrugState.GetStock(TestNation, TestAssignVillage).Count,
                "배치 실패 시 마을 상태(재고 엔트리)가 생성되지 않아야 한다");
        }

        // ===== 헬퍼 =====

        /// <summary>테스트 전용(빈) GuardManager 구성 — AddComponent(Awake 미실행) + Instance 리플렉션 세팅.</summary>
        private void CreateTestGuardManager()
        {
            _managerGo = new GameObject("D5B_TestGuardManager");
            var manager = _managerGo.AddComponent<GuardManager>();
            typeof(GuardManager).GetProperty("Instance").SetValue(null, manager);
        }
    }
}
