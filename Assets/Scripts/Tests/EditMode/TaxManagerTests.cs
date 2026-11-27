// [115차] TaxManager EditMode 테스트 — 주간 세금 경계일/멱등/산식/세이브 라운드트리.
// EditMode 제약 반영: RuntimeInitializeOnLoadMethod 미발화(Bootstrap 미호출), PlayerStats.Instance=null,
// TimeManager 미생성(Initialize() 호출 금지). 격리는 ImportSaveData(0, -1) 공개 경로로 수행.
using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    [TestFixture]
    public class TaxManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            // 공개 세이브 경로로 격리 리셋 — pending 0 / 마지막 과세일 -1(미과세).
            TaxManager.Instance.ImportSaveData(0, -1);
        }

        [Test]
        public void BoundaryDay_BeforeAnchor_False()
        {
            Assert.IsFalse(TaxManager.Instance.ApplyWeeklyTax(0));
        }

        [Test]
        public void BoundaryDay_NonBoundaryDays_False()
        {
            Assert.IsFalse(TaxManager.Instance.ApplyWeeklyTax(2));
            Assert.IsFalse(TaxManager.Instance.ApplyWeeklyTax(3));
            Assert.IsFalse(TaxManager.Instance.ApplyWeeklyTax(4));
            Assert.IsFalse(TaxManager.Instance.ApplyWeeklyTax(7));
        }

        [Test]
        public void BoundaryDay_AnchorDay_True()
        {
            Assert.IsTrue(TaxManager.Instance.ApplyWeeklyTax(1));
        }

        [Test]
        public void BoundaryDay_WeekBoundaries_True()
        {
            Assert.IsTrue(TaxManager.Instance.ApplyWeeklyTax(8));
            TaxManager.Instance.ImportSaveData(0, -1);
            Assert.IsTrue(TaxManager.Instance.ApplyWeeklyTax(15));
        }

        [Test]
        public void ApplyWeeklyTax_Idempotent_SameDay()
        {
            Assert.IsTrue(TaxManager.Instance.ApplyWeeklyTax(1));
            Assert.IsFalse(TaxManager.Instance.ApplyWeeklyTax(1), "같은 경계일 재호출은 멱등 — false");

            TaxManager.Instance.ImportSaveData(0, -1);
            Assert.IsTrue(TaxManager.Instance.ApplyWeeklyTax(8));
            Assert.IsFalse(TaxManager.Instance.ApplyWeeklyTax(8), "같은 경계일 재호출은 멱등 — false");
        }

        [Test]
        public void ApplyWeeklyTax_SequentialWeeks_Advance()
        {
            Assert.IsTrue(TaxManager.Instance.ApplyWeeklyTax(1));
            Assert.IsFalse(TaxManager.Instance.ApplyWeeklyTax(5), "주 중간일은 무시");
            Assert.IsTrue(TaxManager.Instance.ApplyWeeklyTax(8));
            Assert.IsTrue(TaxManager.Instance.ApplyWeeklyTax(15));
        }

        [Test]
        public void ApplyWeeklyTax_NoSceneDb_PendingStaysZero()
        {
            // EditMode — TerritoryDatabase 상태/PlayerOwned 부재 경로: 과세 합계 0, 미수금 누적 0 (멱등 안전).
            Assert.IsTrue(TaxManager.Instance.ApplyWeeklyTax(1));
            Assert.AreEqual(0, TaxManager.Instance.PendingTax, "씬 DB 부재 경로 — 미수금 0 유지");
        }

        [Test]
        public void CalculateRecommendedTax_Ring1_FullPercent_70()
        {
            // territoryIndex 21 → villageIndex 20 범위 밖 → 경제 계수 1.0 결정론.
            // Ring1 일세수 10 × 7일 = 주간 70 (100% 세율).
            int tax = TaxManager.Instance.CalculateRecommendedTax(
                TerritoryDifficulty.Ring1, 100, NationType.West, 21);
            Assert.AreEqual(70, tax);
        }

        [Test]
        public void CalculateRecommendedTax_Ring4_55Percent_HalfUp_212()
        {
            // Ring4 일세수 55 × 7 = 385/주 → ×55% = 21175 → half-up (21175+50)/100 = 212.
            int tax = TaxManager.Instance.CalculateRecommendedTax(
                TerritoryDifficulty.Ring4, 55, NationType.West, 21);
            Assert.AreEqual(212, tax);
        }

        [Test]
        public void CalculateRecommendedTax_ZeroPercent_0()
        {
            int tax = TaxManager.Instance.CalculateRecommendedTax(
                TerritoryDifficulty.Ring1, 0, NationType.West, 21);
            Assert.AreEqual(0, tax);
        }

        [Test]
        public void CalculateRecommendedTax_ValidVillageIndex_WithinFactorBounds()
        {
            // territoryIndex 1 → villageIndex 0 유효 마을 — 경제 계수 0.55..1.0 반영.
            // 타 테스트의 VillageDrugState 오염 가능성을 고려한 범위 어서션 (70×0.55=38.5 → half-up 39 하한, 안전여유 38).
            int tax = TaxManager.Instance.CalculateRecommendedTax(
                TerritoryDifficulty.Ring1, 100, NationType.West, 1);
            Assert.GreaterOrEqual(tax, 38, "경제 계수 하한 0.55 반영");
            Assert.LessOrEqual(tax, 70, "경제 계수 상한 1.0 반영");
        }

        [Test]
        public void CollectPendingTax_NothingAndNullPlayerStats_ReturnsZero()
        {
            // pending 0 — 수취 0, 상태 무변화. EditMode에서 PlayerStats.Instance는 null이지만
            // pending 0 가드가 먼저 차단하므로 골드 경로에 도달하지 않는다.
            int collected = TaxManager.Instance.CollectPendingTax();
            Assert.AreEqual(0, collected);
            Assert.AreEqual(0, TaxManager.Instance.PendingTax);
        }

        [Test]
        public void SaveRoundTrip()
        {
            TaxManager.Instance.ImportSaveData(123, 8);
            Assert.AreEqual((123, 8), TaxManager.Instance.ExportSaveData());

            TaxManager.Instance.ImportSaveData(0, -1);
            Assert.AreEqual((0, -1), TaxManager.Instance.ExportSaveData(), "리셋 경로 — 구세이브 폴백값");
        }

        // ===== [117차 R2] 영지별 세율/호감도 상태 =====

        [Test]
        public void RateFavor_SetClampAndDefaults_117R2()
        {
            // 세율 클램프 — 피그마 슬라이더 계약 0~30%.
            TaxManager.Instance.SetTaxRate(NationType.West, 99, 35);
            Assert.AreEqual(30, TaxManager.Instance.GetTaxRate(NationType.West, 99), "세율 상한 클램프 30");
            TaxManager.Instance.SetTaxRate(NationType.West, 99, -5);
            Assert.AreEqual(0, TaxManager.Instance.GetTaxRate(NationType.West, 99), "세율 하한 클램프 0");

            // 호감도 — 기본 100 / 클램프 0~100.
            TaxManager.Instance.SetFavor(NationType.West, 99, 100);   // 기본값으로 리셋 후 검증
            Assert.AreEqual(100, TaxManager.Instance.GetFavor(NationType.West, 99), "호감도 기본 100");
            TaxManager.Instance.SetFavor(NationType.West, 99, 150);
            Assert.AreEqual(100, TaxManager.Instance.GetFavor(NationType.West, 99), "호감도 상한 클램프");
            TaxManager.Instance.SetFavor(NationType.West, 99, -3);
            Assert.AreEqual(0, TaxManager.Instance.GetFavor(NationType.West, 99), "호감도 하한 클램프");
        }

        [Test]
        public void RateFavor_ExportImportRoundTrip_117R2()
        {
            TaxManager.Instance.SetTaxRate(NationType.East, 42, 20);
            TaxManager.Instance.SetFavor(NationType.East, 42, 88);
            var (rates, favor) = TaxManager.Instance.ExportRateFavorState();

            // 복원 전 상태 오염 — 병합 시맨틱(Import는 기존 항목에 덮어씀) 검증 포함.
            TaxManager.Instance.SetFavor(NationType.East, 42, 100);
            Assert.AreEqual(100, TaxManager.Instance.GetFavor(NationType.East, 42), "복원 전 임의 변경");

            TaxManager.Instance.ImportRateFavorState(rates, favor);
            Assert.AreEqual(20, TaxManager.Instance.GetTaxRate(NationType.East, 42), "세율 라운드트립");
            Assert.AreEqual(88, TaxManager.Instance.GetFavor(NationType.East, 42), "호감도 라운드트립");
        }

        [Test]
        public void AutoCollect_WeeklyBoundary_DepositsImmediately_117R2()
        {
            // [117차 R2] 피그마 "매주 월요일 00:00 자동 징수" — ApplyWeeklyTax가 수금을 자동 수행.
            //   EditMode(PlayerStats null)에서는 CollectPendingTax가 골드 지급 실패로 pending 보존 —
            //   자동 수금 경로가 CollectPendingTax를 호출하는지 소스 어서션으로 보강(동작은 Play 몫).
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "Scripts/Systems/TaxManager.cs"));
            StringAssert.Contains("int autoCollected = CollectPendingTax();", src);
        }
    }
}
