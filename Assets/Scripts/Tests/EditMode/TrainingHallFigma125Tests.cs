// [125차 P5] 훈련소 Figma 정합 계약 테스트 — 소스 텍스트 계약(docs/125_TRAINING_HALL_PLAN.md 진실치).
// 런타임 렌더/Play는 QA 서브/승인 게이트 몫 — 본 파일은 소스 레벨 계약만 단언.
// 패턴 소스: WorldMapFigma124Tests(ReadSource 헬퍼).
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class TrainingHallFigma125Tests
    {
        private const string WindowPath = "Assets/Scripts/UI/Toolkit/TrainingHallWindowUTK.cs";
        private const string SystemPath = "Assets/Scripts/Systems/TrainingHallSystem.cs";
        private const string BlueprintPath = "Assets/Scripts/Core/Data/BlueprintData.cs";
        private const string ConstructionPath = "Assets/Scripts/Systems/ConstructionManager.cs";
        private const string FacilityPath = "Assets/Scripts/Systems/FacilityPropBuilder.cs";
        private const string ClassifierPath = "Assets/Scripts/Systems/HoverTargetClassifier.cs";
        private const string CursorPath = "Assets/Scripts/UI/Toolkit/UTKCursorOverlay.cs";
        private const string GuardPath = "Assets/Scripts/Systems/GuardPlaceholder.cs";
        private const string MapWindowPath = "Assets/Scripts/UI/Toolkit/ConstructionWindowUTK.cs";

        private string ReadSource(string path)
        {
            var full = Path.Combine(Application.dataPath, "..", path);
            Assert.IsTrue(File.Exists(full), $"소스 파일 없음: {path}");
            return File.ReadAllText(full);
        }

        // ===== P4 창 골격 — 1600×1040 음식점창 동일 =====
        [Test]
        public void Figma125_Window_Skeleton_TruthValues()
        {
            string src = ReadSource(WindowPath);
            StringAssert.Contains("WinW = 1600f", src);
            StringAssert.Contains("WinH = 1040f", src);
            StringAssert.Contains("UTKWindowChrome.Frameless", src);
            StringAssert.Contains("ApplyCanvasFit).StartingIn(16)", src);
            StringAssert.Contains("ApplyCanvasFit).StartingIn(120)", src);
            StringAssert.Contains("ApplyCanvasFit).StartingIn(400)", src);
            StringAssert.Contains("FigmaCanvasLayout.CanvasWidth", src);
        }

        [Test]
        public void Figma125_Window_Layout_TruthValues()
        {
            string src = ReadSource(WindowPath);
            // 헤더/좌 명부/우 진행/도움말 — Figma rel 좌표 ([144차 P6-TRN #1] 루트 pad-top 36 반영 y+36)
            StringAssert.Contains("Abs(header, 64f, 36f, 1472f, 18f)", src);
            StringAssert.Contains("Abs(panel, 64f, 78f, 480f, 882f)", src);
            StringAssert.Contains("Abs(panel, 568f, 78f, 968f, 882f)", src);
            StringAssert.Contains("Abs(help, 64f, 984f, 1472f, 14f)", src);
            StringAssert.Contains("\"병사 명부\"", src);
            StringAssert.Contains("\"훈련 진행 현황\"", src);
            StringAssert.Contains("\"훈련 진행률, 레벨, 완료 상태\"", src);
        }

        // ===== 144차 P6-TRN — P0 계약(헤더 아이콘 2종 + 대기중 취소 버튼) =====
        [Test]
        public void Figma144_TrainingHall_P0_HeaderIcons_And_WaitingCancel()
        {
            string src = ReadSource(WindowPath);
            // P0-1 전초기지 VECTOR(269:325) — map-pin PNG 근사 매핑
            StringAssert.Contains("UI/icons/map-pin", src);
            // P0-2 시계 VECTOR(269:330) — clock PNG 부재 → 프레임 근사(원+바늘)
            StringAssert.Contains("BuildClockGlyph", src);
            // P0-3 대기중 항목 취소 버튼(271:366) — 기존 OnRowCancel 경로 연결 [144차 P6-TRN #8]
            StringAssert.Contains("AddRowButton(this, 334f, \"취소\", GhostBtnBg, TextMain, onCancel, ChromeBorder)", src);
            // 브레드크럼/게임시간 구조 분리(269:324-332) + 낮/밤 실데이터
            StringAssert.Contains("BreadcrumbScreen", src);
            StringAssert.Contains("tm.IsDay ? \"낮\" : \"밤\"", src);
        }

        [Test]
        public void Figma125_Window_Colors_TruthValues()
        {
            string src = ReadSource(WindowPath);
            StringAssert.Contains("C(0x13, 0x18, 0x1F)", src);            // #13181F 패널
            StringAssert.Contains("C(0x19, 0x1F, 0x27)", src);            // #191F27
            StringAssert.Contains("C(0x0F, 0x14, 0x1B)", src);            // #0F141B 검색/열제목
            StringAssert.Contains("C(0x8B, 0x5C, 0xF6)", src);            // #8B5CF6 훈련 버튼
            StringAssert.Contains("C(0x22, 0xC5, 0x5E)", src);            // #22C55E 완료 섹션
            StringAssert.Contains("C(0x58, 0xA6, 0xFF, 0.08f)", src);     // 행 배경 #58A6FF/0.08
            StringAssert.Contains("C(0x0B, 0x0E, 0x14)", src);            // 탭 활성 텍스트
        }

        [Test]
        public void Figma125_FilterTabs_And_Columns()
        {
            string src = ReadSource(WindowPath);
            StringAssert.Contains("\"대기 중\"", src);
            StringAssert.Contains("\"훈련 중\"", src);
            StringAssert.Contains("\"완료\"", src);
            // 열제목 7칼럼
            StringAssert.Contains("\"병사\"", src);
            StringAssert.Contains("\"병과\"", src);
            StringAssert.Contains("\"레벨\"", src);
            StringAssert.Contains("\"훈련 진행\"", src);
            StringAssert.Contains("\"남은 시간\"", src);
            StringAssert.Contains("\"완료 예정\"", src);
            StringAssert.Contains("\"상태\"", src);
        }

        [Test]
        public void Figma125_HelpBar_And_OpNote()
        {
            string src = ReadSource(WindowPath);
            StringAssert.Contains("클릭 병사 선택 · Enter 완료 확인 · Esc 닫기", src);
            StringAssert.Contains("훈련소를 닫아도 훈련은 계속되며, 완료된 병사는 바로 배치할 수 있습니다", src);
            StringAssert.Contains("한 번에 최대", src);
        }

        // ===== P3 시스템 계약 =====
        [Test]
        public void Figma125_System_CoreContracts()
        {
            string src = ReadSource(SystemPath);
            StringAssert.Contains("public static int CostForLevel(int level) => Mathf.Max(0, level) * CostPerLevel;", src);
            StringAssert.Contains("TrainingDurationSeconds = 3600", src);
            StringAssert.Contains("Capacity = 20", src);
            StringAssert.Contains("public static (int atk, int def, int vit) RollGains(int seed)", src);
            StringAssert.Contains("new System.Random(seed)", src);        // 결정론 랜덤 규약
            StringAssert.Contains("public static bool CompleteNow(string stableId)", src);
            StringAssert.Contains("GetGuardsForTerritory", src);
            StringAssert.Contains("public static int GetTier(string stableId)", src);
            StringAssert.Contains("ResetForTest", src);
        }

        [Test]
        public void Figma125_GuardPlaceholder_ApplyTrainingLevelUp()
        {
            string src = ReadSource(GuardPath);
            StringAssert.Contains("public void ApplyTrainingLevelUp(int atkGain, int defGain, int vitGain)", src);
        }

        // ===== P1 건설 설계도 계약 =====
        [Test]
        public void Figma125_Blueprint_TruthValues()
        {
            string src = ReadSource(BlueprintPath);
            StringAssert.Contains("id = \"bp_traininghall\"", src);
            StringAssert.Contains("goldCost = 600, buildTimeSeconds = 120, effectId = \"training_hall\"", src);
            StringAssert.Contains("materialWoodCost = 12, materialStoneCost = 8", src);
            StringAssert.Contains("displayName = \"훈련소\"", src);
        }

        [Test]
        public void Figma125_ConstructionManager_OnePerTerritory()
        {
            string src = ReadSource(ConstructionPath);
            StringAssert.Contains("TrainingHallBlueprintEffectId = \"training_hall\"", src);
            StringAssert.Contains("\"이미 훈련소가 있는 영지\"", src);
            StringAssert.Contains("\"이미 훈련소가 있는 영지(시공 중 포함)\"", src);
        }

        // ===== P2 실물/프롬프트/커서 계약 =====
        [Test]
        public void Figma125_FacilityPropBuilder_TrainingHall()
        {
            string src = ReadSource(FacilityPath);
            StringAssert.Contains("TrainingHallBlueprintId = \"bp_traininghall\"", src);
            StringAssert.Contains("TrainingHallEffectId = \"training_hall\"", src);
            StringAssert.Contains("blueprintId == TrainingHallBlueprintId", src);
            StringAssert.Contains("BuildTrainingHallStation", src);
            StringAssert.Contains("TrainingHallEffectId => \"E: 훈련소\"", src);
        }

        [Test]
        public void Figma125_Classifier_And_Cursor_TrainingHall()
        {
            string src = ReadSource(ClassifierPath);
            StringAssert.Contains("TrainingHall", src);
            StringAssert.Contains("FacilityStationInteractable", src);
            string cursor = ReadSource(CursorPath);
            StringAssert.Contains("TargetKind.TrainingHall", cursor);
        }

        // ===== E키 매핑 — FacilityOpenRequested → 훈련소 UI =====
        [Test]
        public void Figma125_FacilityOpenRequested_MapsToTrainingWindow()
        {
            string src = ReadSource(MapWindowPath);
            StringAssert.Contains("case \"training_hall\": TrainingHallWindowUTK.Open();", src);
        }
    }
}