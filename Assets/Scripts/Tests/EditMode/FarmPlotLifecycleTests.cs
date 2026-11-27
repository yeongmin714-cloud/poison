using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [26차 농경시스템 업그레이드 — docs/FARM_UPGRADE_PLAN.md §1·§2] FarmPlot 신규 상태머신 계약.
    /// 순수 판정 함수(ComputeGrowProgress01/ComputeReadyElapsedDays/ComputeMoistureFraction/IsRotten)는
    /// MonoBehaviour 상태 없이 절대시간 스냅샷만으로 판정 — EditMode(Time.deltaTime=0) 제약 회피.
    /// 상태 전이 메서드(CompleteTill/CompletePlant/CompleteWater/CompleteClear)는 guard 조건을
    /// 소스-계약 문자열로 검증(컴포넌트 라이프사이클은 Play 몫).
    /// </summary>
    public class FarmPlotLifecycleTests
    {
        // ===== 성장 진행률 (3일 계약) =====

        [Test]
        public void GrowProgress_ZeroElapsed_IsZero()
        {
            Assert.AreEqual(0f, FarmPlot.ComputeGrowProgress01(0.0, 3), 0.0001f, "파종 직후 진행률 0");
        }

        [Test]
        public void GrowProgress_HalfOfThreeDays_IsHalf()
        {
            // 1.5일 = 3일의 절반
            double sec = 1.5 * 86400.0;
            Assert.AreEqual(0.5f, FarmPlot.ComputeGrowProgress01(sec, 3), 0.0001f, "3일 성장의 절반 경과 → 0.5");
        }

        [Test]
        public void GrowProgress_ThreeDays_IsComplete()
        {
            // [26차] 성장 2일→3일 계약 — 3일 경과 시 1.0
            double sec = 3 * 86400.0;
            Assert.AreEqual(1f, FarmPlot.ComputeGrowProgress01(sec, 3), 0.0001f, "3일 경과 → 성장 완료");
        }

        [Test]
        public void GrowProgress_TwoDays_IsNotComplete_ThreeDayContract()
        {
            // 기존 2일 계약 폐기 검증 — 2일 경과는 아직 미숙성(3일 계약)
            double sec = 2 * 86400.0;
            Assert.That(FarmPlot.ComputeGrowProgress01(sec, 3), Is.LessThan(1f), "2일 경과는 3일 계약에서 미완료");
        }

        [Test]
        public void GrowProgress_ClampsAtOne()
        {
            double sec = 10 * 86400.0;
            Assert.AreEqual(1f, FarmPlot.ComputeGrowProgress01(sec, 3), 0.0001f, "초과 경과는 클램프");
        }

        [Test]
        public void GrowProgress_GrowDaysMinimumOne()
        {
            double sec = 86400.0;
            Assert.AreEqual(1f, FarmPlot.ComputeGrowProgress01(sec, 0), 0.0001f, "growDays 0 이하는 1일로 보정");
        }

        // ===== Ready 경과/부패 (7일 계약) =====

        [Test]
        public void ReadyElapsed_Zero_IsZeroDays()
        {
            Assert.AreEqual(0, FarmPlot.ComputeReadyElapsedDays(0.0), "Ready 직후 경과 0일");
        }

        [Test]
        public void ReadyElapsed_SevenDays_IsSeven()
        {
            Assert.AreEqual(7, FarmPlot.ComputeReadyElapsedDays(7 * 86400.0), "7일 경과 → 7");
        }

        [Test]
        public void IsRotten_WithinGrace_IsFalse()
        {
            // 7일 경과 = 허용 일수 '이내' — 초과해야 부패(> 7)
            Assert.IsFalse(FarmPlot.IsRotten(7 * 86400.0, 7), "Ready 7일 경과는 아직 부패 아님(경계)");
        }

        [Test]
        public void IsRotten_PastGrace_IsTrue()
        {
            // [26차] 수확 시기 일주일 초과 → 부패 계약
            double over = 7 * 86400.0 + 3600.0;   // 7일 + 1시간
            Assert.IsTrue(FarmPlot.IsRotten(over, 7), "7일 초과 → 부패");
        }

        [Test]
        public void IsRotten_NegativeElapsed_IsFalse()
        {
            Assert.IsFalse(FarmPlot.IsRotten(-100.0, 7), "음수 경과(시간 역행/초기화)는 부패 아님");
        }

        // ===== 수분 (1일 드레인 계약) =====

        [Test]
        public void Moisture_FreshWater_IsFull()
        {
            Assert.AreEqual(1f, FarmPlot.ComputeMoistureFraction(0.0), 0.0001f, "물 방금 줌 → 100%");
        }

        [Test]
        public void Moisture_HalfDay_IsHalf()
        {
            double sec = 0.5 * 86400.0;
            Assert.AreEqual(0.5f, FarmPlot.ComputeMoistureFraction(sec), 0.0001f, "반나절 경과 → 수분 50%");
        }

        [Test]
        public void Moisture_FullDay_IsZero()
        {
            // [26차] 하루마다 물을 줘야 한다 — 1일 경과 시 바닥
            Assert.AreEqual(0f, FarmPlot.ComputeMoistureFraction(86400.0), 0.0001f, "1일 경과 → 수분 0(물 필요)");
        }

        [Test]
        public void Moisture_OverDay_ClampsZero()
        {
            Assert.AreEqual(0f, FarmPlot.ComputeMoistureFraction(2 * 86400.0), 0.0001f, "1일 초과 경과도 0 클램프");
        }

        [Test]
        public void Moisture_NegativeElapsed_IsFull()
        {
            Assert.AreEqual(1f, FarmPlot.ComputeMoistureFraction(-5.0), 0.0001f, "음수 경과(재충전 직후)는 만수");
        }

        // ===== 채널링 규약 상수 [26차 §2] =====

        [Test]
        public void ChannelSeconds_ContractValues()
        {
            Assert.AreEqual(2f, FarmPlot.TillChannelSeconds, "굉이질 2초");
            Assert.AreEqual(1f, FarmPlot.PlantChannelSeconds, "파종 1초");
            Assert.AreEqual(1.5f, FarmPlot.WaterChannelSeconds, "물주기 1.5초");
            Assert.AreEqual(2f, FarmPlot.HarvestChannelSeconds, "수확 2초");
            Assert.AreEqual(1.5f, FarmPlot.ClearChannelSeconds, "정리 1.5초");
        }

        // ===== 상태 전이 소스-계약 =====

        [Test]
        public void StateMachine_ContainsAllEightPhases()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            // 8상태 — Grass/Tilled/Planted/Growing/Ready/Withered/Rotted/Dirt
            Assert.That(src, Does.Contain("Grass, Dirt, Tilled, Planted, Growing, Ready, Withered, Rotted"),
                "26차 8상태 enum 선언");
        }

        [Test]
        public void Till_GuardsGrassAndDirtOnly()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("_phase != CropPhase.Grass && _phase != CropPhase.Dirt) return"),
                "굉이질은 Grass/Dirt에서만");
        }

        [Test]
        public void Plant_GuardsTilledOnly()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("_phase != CropPhase.Tilled) return"), "파종은 Tilled에서만");
        }

        [Test]
        public void Water_GuardsGrowingStatesOnly()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("Planted && _phase != CropPhase.Growing && _phase != CropPhase.Ready) return"),
                "물주기는 재배 상태(Planted/Growing/Ready)에서만");
        }

        [Test]
        public void Clear_GuardsWitheredAndRottedOnly()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("_phase != CropPhase.Withered && _phase != CropPhase.Rotted) return"),
                "정리는 시듦/부패에서만");
        }

        [Test]
        public void Harvest_ReturnsDirt_NotEmpty()
        {
            // 수확 후 기본 흙땅 복귀 계약 — 구 Empty 상태 폐기
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Not.Contain("CropPhase.Empty"), "구 Empty 상태 완전 폐기");
            Assert.That(src, Does.Contain("SetPhaseDirt($\"{_cropType} 수확 완료\")"),
                "수확 완료 → Dirt 복귀");
        }

        [Test]
        public void Withering_TriggersOnMoistureZero()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            // TickPhase의 Growing/Ready 두 지점 모두 수분 0 → Withered
            Assert.That(src, Does.Contain("ComputeMoistureFraction(now - _lastMoistureTick) <= 0f"),
                "수분 0 판정 경유");
            int witherCount = System.Text.RegularExpressions.Regex.Matches(src, "CropPhase\\.Withered;\r?\n").Count
                + System.Text.RegularExpressions.Regex.Matches(src, "_phase = CropPhase.Withered;").Count;
            Assert.That(witherCount, Is.GreaterThanOrEqualTo(2), "Growing/Ready 두 지점에서 시듦 전이");
        }

        [Test]
        public void Rotting_UsesSevenDayGrace()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("_rotGraceDays = 7"), "부패 허용 기본 7일");
            Assert.That(src, Does.Contain("IsRotten(now - _readyGameTime, _rotGraceDays)"), "Ready 경과 기준 부패 판정");
        }

        [Test]
        public void AllInteractions_GoThroughGatherChannel()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            // 4종 채널 — 굉이질/파종/물주기(+정리). 수확은 HerbPickup 자체 채널.
            Assert.That(src, Does.Contain("GatherChannelSystem.Instance.Begin"), "채널 게이트 경유");
            Assert.That(src, Does.Contain("CompleteTill"), "굉이질 콜백");
            Assert.That(src, Does.Contain("CompletePlant"), "파종 콜백");
            Assert.That(src, Does.Contain("CompleteWater"), "물주기 콜백");
            Assert.That(src, Does.Contain("CompleteClear"), "정리 콜백");
        }

        [Test]
        public void Plant_ChannelPreflightsSeed()
        {
            // 채널 낭비 방지 — 씨앗 없으면 채널 시작 전 피드백(상호작용불가=피드백 필수 계약)
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("씨앗이 부족합니다"), "파종 전 씨앗 사전 검증 피드백");
        }

        [Test]
        public void GrowDuration_DefaultThreeDays()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("_growDurationDay = 3"), "기본 성장 3일 [26차]");
        }

        [Test]
        public void SceneSetup_UsesThreeDayGrow()
        {
            string setup = System.IO.File.ReadAllText("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            Assert.That(setup, Does.Contain("HerbPickup.HerbType.Red, 3"), "SetupFarm 성장 3일 [26차]");
            Assert.That(setup, Does.Not.Contain("HerbPickup.HerbType.Red, 2)"), "구 2일 계약 소멸");
        }

        [Test]
        public void OwnershipLoss_ResetsToGrass()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlot.cs");
            Assert.That(src, Does.Contain("ResetToGrass(\"아군 영지에서만 경작 가능 — 소유 상실/미소유\")"),
                "소유 상실 → Grass 강제 초기화");
        }

        [Test]
        public void EInteractionPrompt_UpdatedToEightStates()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/EInteractionPromptUTK.cs");
            Assert.That(src, Does.Contain("bool avail = f.IsOwned;"), "성장 중도 물주기 가능 — IsOwned 단일 조건");
            Assert.That(src, Does.Not.Contain("f.Phase != FarmPlot.CropPhase.Growing"), "구 Growing 제외 조건 소멸");
        }

        // ===== FarmingManager 소멸 문자열 계약 갱신 =====

        [Test]
        public void FarmingManager_ContractCommentUpdated()
        {
            // 구 자동수확 경로(AutoMissionManager)는 Phase 5에서 병사 워커로 대체 — 현재 주석 계약만 확인
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmingManager.cs");
            Assert.That(src, Does.Contain("FarmPlot"), "FarmPlot 연동 유지");
        }
    }
}
