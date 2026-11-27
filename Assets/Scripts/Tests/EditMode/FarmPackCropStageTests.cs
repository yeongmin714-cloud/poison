using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 183-P3] Farm Pack 작물 단계 모델 매핑 계약 — 약초 5종 ↔ 팩 식물(사용자 승인).
    /// FarmPlotVisualsTests 규약 1:1(네임스페이스/using/NUnit 표기 동일).
    ///
    /// 판정 대상:
    ///  ① HerbType → Farm Pack 단계 키 목록 결정론(Red 2단계 / Green 3단계 / Purple·Yellow·Silver 1단계 / 미상 null).
    ///  ② 단계 인덱스 결정론 경계 — stageCount 1 전 상태 0 · Planted=0 · Growing=마지막 단계 미도달
    ///     (Clamp) · Ready/Withered/Rotted=성숙(stageCount-1).
    ///  ③ 팩 GLB 8종 Resources 실존 + herb 폴백 경로(herb_red) 실존 동시 증명.
    ///  ④ 소스계약 — 폴백 체인(팩 단계 → herb GLB → Sphere) 유지 판정.
    ///
    /// EditMode 제약: GameObject.Instantiate/Resources.Load 자체는 EditMode 허용 —
    /// 재빌드/틴트 로직은 src 계약으로만 판정(직접 실행 테스트 금지).
    /// </summary>
    public class FarmPackCropStageTests
    {
        // ===== ① HerbType → Farm Pack 단계 키 목록 결정론 =====

        [Test]
        public void StageMapping_Keys_Contract()
        {
            // Red — 토마토 2단계(어림→성숙 순)
            string[] red = FarmPlotVisuals.ResolveCropStageGlbKeys(HerbPickup.HerbType.Red);
            Assert.IsNotNull(red, "Red 단계 키 목록 존재");
            Assert.AreEqual(2, red.Length, "Red — 2단계(토마토 어림→성숙)");
            Assert.AreEqual("TomatoPlant_1_A", red[0], "Red[0] — TomatoPlant_1_A");
            Assert.AreEqual("TomatoPlant_2_A", red[1], "Red[1] — TomatoPlant_2_A");

            // Green — 감자 3단계
            string[] green = FarmPlotVisuals.ResolveCropStageGlbKeys(HerbPickup.HerbType.Green);
            Assert.IsNotNull(green, "Green 단계 키 목록 존재");
            Assert.AreEqual(3, green.Length, "Green — 3단계(감자)");
            Assert.AreEqual("PotatoPlant_1", green[0], "Green[0] — PotatoPlant_1");
            Assert.AreEqual("PotatoPlant_2", green[1], "Green[1] — PotatoPlant_2");
            Assert.AreEqual("PotatoPlant_3", green[2], "Green[2] — PotatoPlant_3");

            // Purple — 가지 1단계(단일 모델 — 스케일 보간 계약)
            string[] purple = FarmPlotVisuals.ResolveCropStageGlbKeys(HerbPickup.HerbType.Purple);
            Assert.IsNotNull(purple, "Purple 단계 키 목록 존재");
            Assert.AreEqual(1, purple.Length, "Purple — 1단계(가지)");
            Assert.AreEqual("EggPlant_Plant", purple[0], "Purple[0] — EggPlant_Plant");

            // Yellow — 옥수수 1단계
            string[] yellow = FarmPlotVisuals.ResolveCropStageGlbKeys(HerbPickup.HerbType.Yellow);
            Assert.IsNotNull(yellow, "Yellow 단계 키 목록 존재");
            Assert.AreEqual(1, yellow.Length, "Yellow — 1단계(옥수수)");
            Assert.AreEqual("CornPlant", yellow[0], "Yellow[0] — CornPlant");

            // Silver — 양배추 1단계
            string[] silver = FarmPlotVisuals.ResolveCropStageGlbKeys(HerbPickup.HerbType.Silver);
            Assert.IsNotNull(silver, "Silver 단계 키 목록 존재");
            Assert.AreEqual(1, silver.Length, "Silver — 1단계(양배추)");
            Assert.AreEqual("Cabbage", silver[0], "Silver[0] — Cabbage");

            // 미상 타입 — null → 기존 herb GLB 폴백 경로 계약
            var unknown = (HerbPickup.HerbType)999;
            Assert.That(FarmPlotVisuals.ResolveCropStageGlbKeys(unknown), Is.Null,
                "미상 타입 → null(기존 herb GLB 폴백 경로 접기)");
        }

        // ===== ② 단계 인덱스 결정론 경계 =====

        [Test]
        public void ComputeCropStageIndex_Boundaries()
        {
            // stageCount 1 — 전 상태 0(단일 모델 — 기존 스케일 보간만으로 성장 표현)
            Assert.AreEqual(0, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Grass, 0f, 1), "1단계 Grass → 0");
            Assert.AreEqual(0, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Planted, 0f, 1), "1단계 Planted → 0");
            Assert.AreEqual(0, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Growing, 0.5f, 1), "1단계 Growing → 0");
            Assert.AreEqual(0, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Ready, 1f, 1), "1단계 Ready → 0");

            // Planted — 3단계에서도 0(최소 단계 선빌드)
            Assert.AreEqual(0, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Planted, 0f, 3), "Planted(3단계) → 0");

            // Growing 3단계 — 진행 스왑, 마지막(성숙) 단계 미도달 계약(Clamp stageCount-2)
            Assert.AreEqual(0, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Growing, 0f, 3), "Growing p=0 → 0");
            Assert.AreEqual(0, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Growing, 0.49f, 3), "Growing p=0.49 → 0");
            Assert.AreEqual(1, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Growing, 0.5f, 3), "Growing p=0.5 → 1");
            Assert.AreEqual(1, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Growing, 0.99f, 3), "Growing p=0.99 → 1");
            Assert.AreEqual(1, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Growing, 1f, 3), "Growing p=1 → Clamp 1(성숙 2 방지)");

            // Growing 2단계 — 성숙(1) 미도달 계약
            Assert.AreEqual(0, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Growing, 0.99f, 2), "Growing(2단계) p=0.99 → 0(성숙 미도달)");

            // 표시 완료 상태 — 성숙 단계(stageCount-1, 틴트는 기존 경로)
            Assert.AreEqual(2, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Ready, 1f, 3), "Ready(3단계) → 2(성숙)");
            Assert.AreEqual(2, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Withered, 1f, 3), "Withered(3단계) → 2(성숙)");
            Assert.AreEqual(2, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Rotted, 1f, 3), "Rotted(3단계) → 2(성숙)");
            Assert.AreEqual(1, FarmPlotVisuals.ComputeCropStageIndex(FarmPlot.CropPhase.Ready, 1f, 2), "Ready(2단계) → 1(성숙)");
        }

        // ===== ③ 팩 GLB 8종 + herb 폴백 Resources 실존 =====

        [Test]
        public void StageGlb_ResourcesKeys_Exist()
        {
            // 팩 단계 모델 8키 — Resources.Load 실존 판정(테스트 어셈블리 Resources 접근 허용)
            Assert.IsNotNull(Resources.Load<GameObject>("Models/UserProvided/TomatoPlant_1_A"), "TomatoPlant_1_A 실존");
            Assert.IsNotNull(Resources.Load<GameObject>("Models/UserProvided/TomatoPlant_2_A"), "TomatoPlant_2_A 실존");
            Assert.IsNotNull(Resources.Load<GameObject>("Models/UserProvided/PotatoPlant_1"), "PotatoPlant_1 실존");
            Assert.IsNotNull(Resources.Load<GameObject>("Models/UserProvided/PotatoPlant_2"), "PotatoPlant_2 실존");
            Assert.IsNotNull(Resources.Load<GameObject>("Models/UserProvided/PotatoPlant_3"), "PotatoPlant_3 실존");
            Assert.IsNotNull(Resources.Load<GameObject>("Models/UserProvided/CornPlant"), "CornPlant 실존");
            Assert.IsNotNull(Resources.Load<GameObject>("Models/UserProvided/EggPlant_Plant"), "EggPlant_Plant 실존");
            Assert.IsNotNull(Resources.Load<GameObject>("Models/UserProvided/Cabbage"), "Cabbage 실존");

            // herb 폴백 경로 실존 동시 증명 — 폴백 체인(팩 단계 → herb GLB → Sphere) 2단계 유효
            Assert.IsNotNull(Resources.Load<GameObject>("Models/UserProvided/herb_red"), "herb_red 폴백 실존");
        }

        // ===== ④ 소스계약 — herb GLB 폴백 체인 유지 =====

        [Test]
        public void StageFallback_HerbPath_Preserved()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/FarmPlotVisuals.cs");
            Assert.That(src, Does.Contain("LoadCropGlbPrefab(plot.CropType)"),
                "[무번호 183-P3] 폴백 체인 존재 src 계약 — 팩 단계 모델 부재 시 기존 herb GLB 경로 접기");
        }
    }
}
