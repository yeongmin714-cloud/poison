using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [100차] 음식점 손님 방문 시각화(RCT식) + 결제 코인 연출 — 계약 테스트 (EditMode 가능 부).
    ///  · 혼합 계약: 실시간 구매는 '연출 전용' — RestaurantVisitorSystem은 재고/골드/정산 상태에 접촉 금지
    ///    (ConsumeStock/AddPendingRevenue/AddGold/RestockFromPlayer 부재 = 정산 진실원천 불변 핀).
    ///  · 수요 연동: 방문 비율 = VisitorRatio(주민, VisitorDemandFactor(판매가, basePrice)) — 가격↓ → 방문↑.
    ///  · 풀 재사용 + 구독-해제 + U8 창 게이트 계약(코인 팝업).
    /// ⚠ Play 게이트(EditMode 미실행 — 씬/오브젝트 의존): 실제 손님 이동 궤적, 코인 투영/부상 연출.
    /// </summary>
    public sealed class RestaurantVisitorTests
    {
        private static string Read(string rel) => System.IO.File.ReadAllText(rel);

        // ═══════════════ 1. 정산 무접촉 핀(혼합 계약 — 최우선) ═══════════════

        [Test]
        public void VisitorSystem_PureVisual_NoSettlementContact_100()
        {
            string src = Read("Assets/Scripts/Systems/RestaurantVisitorSystem.cs");
            Assert.That(src, Does.Contain("PurchaseVisualized"), "[100차] 구매 연출 정적 이벤트 발화");
            Assert.That(src, Does.Contain("SetVisitTarget"), "[100차] 방문 배정 — VillagerWanderer 위임");
            Assert.That(src, Does.Not.Contain("ConsumeStock"),
                "[100차 혼합 계약] 재고 소모 금지 — 정산은 밤 RunDailySale 진실원천");
            Assert.That(src, Does.Not.Contain("AddPendingRevenue"),
                "[100차 혼합 계약] 적립금 접촉 금지");
            Assert.That(src, Does.Not.Contain("AddGold("),
                "[100차 혼합 계약] 골드 지급 금지 — 연출 전용");
            Assert.That(src, Does.Not.Contain("RestockFromPlayer"),
                "[100차 혼합 계약] 재고 입고 경로 부재");
        }

        // ═══════════════ 2. 수요 연동 ═══════════════

        [Test]
        public void VisitorSystem_Uses_DemandMath_Contract_100()
        {
            string src = Read("Assets/Scripts/Systems/RestaurantVisitorSystem.cs");
            Assert.That(src, Does.Contain("RestaurantDemandMath.VisitorDemandFactor(salePrice"),
                "[100차] 가격→수요 계수 — 낮은 가격일수록 방문자 증가");
            Assert.That(src, Does.Contain("RestaurantDemandMath.VisitorRatio(candidates.Count, factor)"),
                "[100차] 방문 비율 산출 — 수요 계수 연동");
            Assert.That(src, Does.Contain("hour < OpenHourMin || hour >= OpenHourMax"),
                "[100차] 영업 게이트 — TimeManager.Hour 8≤h<22(RestaurantWindowUTK 실측 계약)");
            Assert.That(src, Does.Contain("IsVisiting"), "[100차] 중복 선정 방지 게이트");
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"),
                "[100차] 결정론 — 주입 난수(System.Random)만 사용");
        }

        [Test]
        public void ComputeVisitorCount_RoundAndClamp_100()
        {
            Assert.That(RestaurantVisitorSystem.ComputeVisitorCount(0, 0.5f), Is.EqualTo(0), "주민 0 — 0명");
            Assert.That(RestaurantVisitorSystem.ComputeVisitorCount(8, 0.25f), Is.EqualTo(2), "8×0.25=2 — 반올림");
            Assert.That(RestaurantVisitorSystem.ComputeVisitorCount(8, 0f), Is.EqualTo(0), "비율 0 — 0명");
            Assert.That(RestaurantVisitorSystem.ComputeVisitorCount(8, 1f), Is.EqualTo(8), "비율 1 — 전원");
            Assert.That(RestaurantVisitorSystem.ComputeVisitorCount(8, 3f), Is.EqualTo(8),
                "비율 초과 입력 — Clamp01 방어(상한 1)");
            Assert.That(RestaurantVisitorSystem.ComputeVisitorCount(8, -1f), Is.EqualTo(0),
                "비율 음수 — Clamp01 방어(하한 0)");
            Assert.That(RestaurantVisitorSystem.ComputeVisitorCount(-3, 0.5f), Is.EqualTo(0),
                "주민 음수 — 방어 0");
        }

        [Test]
        public void ResolvePurchaseAmount_SettlementMirror_Contract_100()
        {
            // 마약 재고 + 등록가 → 등록가(정산과 동일 우선순위)
            var drugged = new VillageDrugSnapshot { assignedGuardId = "G1", registeredPrice = 50, normalRegisteredPrice = 8 };
            var stockDrugged = new System.Collections.Generic.List<RestaurantStockItem>
            {
                new RestaurantStockItem { foodItemId = "d1", displayName = "d", count = 3, isDrugged = true, drugStage = 2, basePrice = 10 },
            };
            Assert.That(RestaurantVisitorSystem.ResolvePurchaseAmount(drugged, stockDrugged), Is.EqualTo(50),
                "마약 우선 — 등록가");

            // 마약 재고 + 등록가 미설정 → 재고 basePrice(폴백 순위 동일)
            var druggedNoPrice = new VillageDrugSnapshot { assignedGuardId = "G1", registeredPrice = 0 };
            Assert.That(RestaurantVisitorSystem.ResolvePurchaseAmount(druggedNoPrice, stockDrugged), Is.EqualTo(10),
                "마약 등록가 미설정 — 재고 basePrice");

            // 일반 재고 + 일반 등록가 → 일반 등록가
            var normal = new VillageDrugSnapshot { assignedGuardId = "G1", normalRegisteredPrice = 8 };
            var stockNormal = new System.Collections.Generic.List<RestaurantStockItem>
            {
                new RestaurantStockItem { foodItemId = "n1", displayName = "n", count = 5, isDrugged = false, basePrice = 7 },
            };
            Assert.That(RestaurantVisitorSystem.ResolvePurchaseAmount(normal, stockNormal), Is.EqualTo(8),
                "일반 등록가 우선");

            // 일반 등록가 미설정 → 재고 basePrice
            var normalNoPrice = new VillageDrugSnapshot { assignedGuardId = "G1" };
            Assert.That(RestaurantVisitorSystem.ResolvePurchaseAmount(normalNoPrice, stockNormal), Is.EqualTo(7),
                "일반 등록가 미설정 — 재고 basePrice");

            // 재고 비움 → 일반 등록가만으로 산출(재고 부재 마을은 호출부 스캔 게이트가 차단 — 방어 경로)
            Assert.That(RestaurantVisitorSystem.ResolvePurchaseAmount(normal, null), Is.EqualTo(8),
                "재고 null — normalRegisteredPrice 폴백");
        }

        [Test]
        public void VisitSeed_Deterministic_100()
        {
            int a = RestaurantVisitorSystem.DeriveVisitSeed(42, NationType.East, 3);
            int b = RestaurantVisitorSystem.DeriveVisitSeed(42, NationType.East, 3);
            Assert.That(a, Is.EqualTo(b), "동일 입력 → 동일 시드(결정론 계약)");
            Assert.That(a, Is.Not.EqualTo(RestaurantVisitorSystem.DeriveVisitSeed(43, NationType.East, 3)),
                "일차 변화 → 시드 변화(방문자 재추첨)");
        }

        // ═══════════════ 3. VillagerWanderer 방문 확장 ═══════════════

        [Test]
        public void Wanderer_VisitTarget_Contract_100()
        {
            string src = Read("Assets/Scripts/Systems/VillagerWanderer.cs");
            Assert.That(src, Does.Contain("public void SetVisitTarget(Vector3 point, float lingerSeconds)"),
                "[100차] 방문 목표 API — 기존 시그니처 무변경, 추가만");
            Assert.That(src, Does.Contain("public bool IsVisiting => _visitPoint.HasValue;"),
                "[100차] 방문 중 게이트 — 중복 선정 방지");
            Assert.That(src, Does.Contain("VisitArrived?.Invoke()"),
                "[100차] 도착 → 구매 연출 이벤트 발화");
            Assert.That(src, Does.Contain("fromCenter.magnitude > _radius"),
                "[100차] 방문 지점 마을 반경 클램프 — 0.82 배회 규약 준용");
            Assert.That(src, Does.Contain("_idleLeft = _visitLinger;"),
                "[100차] 머무름 = 휴식 카운터 재사용 — 이동/애니 로직 무변경");
            // 기존 계약 회귀 핀 — 확장이 기존 배회 계약을 훼손하지 않는다
            Assert.That(src, Does.Contain("_anim.SetFloat(\"Speed\", _walking ? 1f : 0f);"),
                "[87차] Speed 계약 무변경(회귀 핀)");
        }

        // ═══════════════ 4. 코인 팝업 오버레이 ═══════════════

        [Test]
        public void CoinPopup_Subscribes_AndPoolReuses_100()
        {
            string src = Read("Assets/Scripts/UI/Toolkit/RestaurantCoinPopupOverlayUTK.cs");
            Assert.That(src, Does.Contain("RestaurantVisitorSystem.PurchaseVisualized += OnPurchase"),
                "[100차] 구매 이벤트 구독(Systems→UI 단방향)");
            Assert.That(src, Does.Contain("RestaurantVisitorSystem.PurchaseVisualized -= OnPurchase"),
                "[100차] 구독 해제 — 유출 방지");
            Assert.That(src, Does.Contain("if (amount <= 0) return;"),
                "[100차] 0원 연출 방지 — 금액 미확보 마을 스킵");
            Assert.That(src, Does.Contain("_pool.Add(p)"),
                "[100차] 만료 → 풀 반환(재사용 — 파괴 아님)");
            Assert.That(src, Does.Contain("pickingMode = PickingMode.Ignore"),
                "[100차] 클릭 통과 — 오버레이 계약");
            Assert.That(src, Does.Contain("ProjectName.Core.UITransitionState.AnyWindowOpen"),
                "[100차] U8 창 게이트 — 창 열리면 오버레이 숨김(이름표 계약 계승)");
            Assert.That(src, Does.Contain("WorldToViewportPoint"),
                "[100차] 월드→스크린 투영 — NameplateOverlayUTK.PlaceSlot 동일 방식");
        }

        // ═══════════════ 5. [151차 P1] 음식점 즉시 구매 트리거 — 소스계약 ═══════════════

        [Test]
        public void Restock_Triggers_Immediate_Visitor_Scan_Livelihood_151()
        {
            // [151차 P1] '바로' 구매 — 재고 입고 완료 직후 방문 스캔 1회 즉시 실행(다음 티커 스캔 대기 제거).
            string restockSrc = Read("Assets/Scripts/Systems/RestaurantSystem.cs");
            Assert.That(restockSrc, Does.Contain("RestaurantVisitorSystem.ScanAndAssign();"),
                "[151차 P1] RestockFromPlayer 성공 경로 → ScanAndAssign 즉시 1회 호출(internal — 같은 어셈블리)");
            Assert.That(restockSrc, Does.Contain(".name.Contains(\"Livelihood\")"),
                "[151차 P1] 즉시 스캔은 Livelihood 씬 게이트 — 프로덕션 씬 무영향");

            // 스캔 간격 씬 게이트 분기 — Livelihood(Test_15/20) 1s / 프로덕션 기존 2.5s 유지
            string visitorSrc = Read("Assets/Scripts/Systems/RestaurantVisitorSystem.cs");
            Assert.That(visitorSrc, Does.Contain("internal const float LivelihoodScanIntervalSeconds = 1f;"),
                "[151차 P1] Livelihood 한정 스캔 간격 1s 상수");
            Assert.That(visitorSrc, Does.Contain("bool livelihoodScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains(\"Livelihood\");"),
                "[151차 P1] 티커 씬 게이트 분기 진입");
            Assert.That(visitorSrc, Does.Contain("RestaurantVisitorSystem.LivelihoodScanIntervalSeconds")
                .And.Contain(": RestaurantVisitorSystem.ScanIntervalAccess"),
                "[151차 P1] 분기 — Livelihood 1s / 프로덕션 ScanIntervalAccess(2.5s) 계약 유지");

            // 기존 계약 회귀 핀 — [151차 P1]로도 불변
            Assert.That(visitorSrc, Does.Contain("if (stockTotal <= 0) return;"),
                "[100차] 재고 0 → 방문 없음 게이트 유지");
            Assert.That(visitorSrc, Does.Not.Contain("ConsumeStock").And.Not.Contain("AddPendingRevenue"),
                "[100차 혼합 계약] 정산 진실원천 불접촉 유지 — 즉시 스캔은 연출 배정 경로만");
        }
    }
}
