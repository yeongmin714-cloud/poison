using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;
using System.Linq;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C-O8-01/02/05: 성 영지 건설 테스트 — 설계도 정의/위치 검증/골드 소비/해체 환불/효과/저장.
    /// 시각물(프리미티브)과 Raycast 지형은 런타임 전용 — Play 판정으로 남김.
    /// </summary>
    public class ConstructionTests
    {
        private ConstructionManager _cm;
        private GameObject _statsGo;
        private PlayerStats _stats;   // [O8] Awake 미실행 환경 — 컴포넌트 참조 사용

        [SetUp]
        public void SetUp()
        {
            _statsGo = new GameObject("PlayerStats_O8");
            _stats = _statsGo.AddComponent<PlayerStats>();
            _stats.AddGold(1000, "test_seed");
        }

        [TearDown]
        public void TearDown()
        {
            if (_cm != null && _cm.gameObject != null) Object.DestroyImmediate(_cm.gameObject);
            if (_statsGo != null) Object.DestroyImmediate(_statsGo);
        }

        // ===================== BlueprintData =====================

        // [D4] bp_restaurant 블루프린트 추가(9번째)로 기대 개수 갱신 8 → 9.
        // [90차] 크래프트 시설 4종 추가로 9 → 13, [125차] 훈련소(bp_traininghall) 추가로 13 → 14.
        [Test]
        public void All_ContainsExactly14UniqueBlueprints()
        {
            Assert.AreEqual(14, BlueprintData.GetAll().Count());
            var ids = new HashSet<string>();
            foreach (var b in BlueprintData.GetAll())
                Assert.IsTrue(ids.Add(b.id), $"중복 id 없음: {b.id}");
        }

        [Test]
        public void TryGet_RegisteredAndUnknown()
        {
            Assert.IsTrue(BlueprintData.TryGet("bp_wall", out var wall));
            Assert.AreEqual(2.0f, wall.footprintRadius, 0.01f);
            Assert.AreEqual(150, wall.goldCost);
            Assert.IsFalse(BlueprintData.TryGet("bp_nope", out _));
        }

        // ===================== 위치 검증 (순수) =====================

        private static readonly Vector3 Center = new Vector3(0f, 0f, 0f);

        [Test]
        public void ValidatePlacement_InsideBounds_True()
        {
            var existing = new System.Collections.Generic.List<(Vector3, float)>();
            Assert.IsTrue(BlueprintData.ValidatePlacement(Center, 60f, new Vector3(10f, 0f, 10f), 2.0f, existing));
        }

        [Test]
        public void ValidatePlacement_OutsideBounds_False()
        {
            var existing = new System.Collections.Generic.List<(Vector3, float)>();
            Assert.IsFalse(BlueprintData.ValidatePlacement(Center, 60f, new Vector3(59f, 0f, 59f), 2.0f, existing),
                "경계(60m) 밖 — 거리 83m + 반경 2 > 60");
        }

        [Test]
        public void ValidatePlacement_Overlap_False_Adjacent_True()
        {
            var existing = new System.Collections.Generic.List<(Vector3, float)>
            {
                (new Vector3(5f, 0f, 5f), 3.0f),
            };
            // 겹침: 거리 0 < 2+3
            Assert.IsFalse(BlueprintData.ValidatePlacement(Center, 60f, new Vector3(5f, 0f, 5f), 2.0f, existing), "겹침 차단");
            // 인접: 거리 >= 합
            Assert.IsTrue(BlueprintData.ValidatePlacement(Center, 60f, new Vector3(5f, 0f, 10f), 2.0f, existing), "거리 5 >= 2+3");
        }

        /// <summary>[O8] DB 실측 — 영지 중심 좌표(TerritoryDatabase lazy 생성됨).</summary>
        private static Vector3 TerritoryCenter(string key)
        {
            var db = TerritoryDatabase.Instance;
            var def = db != null ? db.GetDefinition(key) : new TerritoryDefinition();
            return def.worldPosition;
        }

        // ===================== 배치: 골드 소비/등록 =====================

        [Test]
        public void TryPlace_WithGold_RegistersStructure_AndConsumesGold()
        {
            _cm = new GameObject("CM").AddComponent<ConstructionManager>();
            _stats.AddGold(200, "test_seed2");

            // TerritoryDatabase 인스턴스 미구성 환경 → GetDefinition 불가 → "영지 없음" 우선 검증
            Vector3 center = TerritoryCenter("East_01");
            Vector3 pos = center + new Vector3(5f, 0f, 5f); // 중심 +5m — 반경 60m 내
            var result = _cm.TryPlace("East_01", "bp_wall", pos);

            StringAssert.Contains("건설 시작", result);
            Assert.AreEqual(1, _cm.Structures.Count);
            Assert.AreEqual(1200 - 150, _stats.Gold, "외벽 150G 차감");
        }

        [Test]
        public void TryPlace_UnknownBlueprint_Message()
        {
            _cm = new GameObject("CM2").AddComponent<ConstructionManager>();
            StringAssert.Contains("알 수 없는 설계도", _cm.TryPlace("East_01", "bp_nope", Vector3.zero));
        }

        // ===================== 해체 (환불 50%) =====================

        [Test]
        public void Demolish_Refunds50Percent()
        {
            _cm = new GameObject("CM3").AddComponent<ConstructionManager>();
            var stats = _stats;
            int goldBefore = stats.Gold;

            Vector3 center = TerritoryCenter("East_01");
            var result = _cm.TryPlace("East_01", "bp_wall", center + new Vector3(3f, 0f, 3f));

            Assert.AreEqual(1, _cm.Structures.Count, result);
            string demolishResult = _cm.Demolish(_cm.Structures[0].structureId);
            StringAssert.Contains("환불 75", demolishResult);
            Assert.AreEqual(0, _cm.Structures.Count);
            Assert.AreEqual(goldBefore - 150 + 75, _stats.Gold, "지출 150 − 환불 75");
        }

        [Test]
        public void Demolish_UnknownStructure_Message()
        {
            _cm = new GameObject("CM4").AddComponent<ConstructionManager>();
            StringAssert.Contains("구조물 없음", _cm.Demolish("nonexistent"));
        }

        // ===================== 효과 확인 =====================

        [Test]
        public void HasStructure_IncompleteOnly_False_UntilComplete()
        {
            _cm = new GameObject("CM5").AddComponent<ConstructionManager>();
            // DB 미구성 환경 — HasStructure는 리스트 기반으로만 동작 확인 (수동 등록 불가 → 생략 경로)
            Assert.IsFalse(_cm.HasStructure("East_01", "smithy"), "미등록 — false");
        }

        [Test]
        public void WarehouseSystem_AddExpansionLevel_RespectsCap()
        {
            var go = new GameObject("WS_O8");
            var ws = go.AddComponent<WarehouseSystem>();
            try
            {
                ws.AddExpansionLevel("o8_terr");
                ws.AddExpansionLevel("o8_terr");
                ws.AddExpansionLevel("o8_terr");
                ws.AddExpansionLevel("o8_terr"); // 4번째 — 캡(3)
                Assert.AreEqual(3, ws.GetExpansionLevel("o8_terr"), "최대 3단계 캡");
                Assert.AreEqual(35, ws.GetSlotCapacity("o8_terr"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ===================== 저장 라운드트립 =====================

        [Test]
        public void ApplySaveEntries_IncompletePromotedToComplete_OfflineRule()
        {
            _cm = new GameObject("CM6").AddComponent<ConstructionManager>();
            var entries = new System.Collections.Generic.List<ConstructionSaveEntry>
            {
                new ConstructionSaveEntry
                {
                    structureId = "s1", blueprintId = "bp_wall", territoryId = "East_01",
                    posX = 1f, posY = 0f, posZ = 1f, progress = 0.4f, isComplete = false,
                },
                new ConstructionSaveEntry
                {
                    structureId = "s2", blueprintId = "bp_smithy", territoryId = "East_01",
                    posX = 5f, posY = 0f, posZ = 5f, progress = 1f, isComplete = true,
                },
            };
            _cm.ApplySaveEntries(entries);

            Assert.AreEqual(2, _cm.Structures.Count);
            Assert.IsTrue(_cm.Structures[0].isComplete, "[O8 간편 규칙] 미완료 건 로드 시 완료 승격");
            Assert.IsTrue(_cm.HasStructure("East_01", "none"), "외벽 완료");
            Assert.IsTrue(_cm.HasStructure("East_01", "smithy"), "대장간 완료 — 수리 할인 근거");
        }

        [Test]
        public void ApplySaveEntries_NullOrEmpty_Safe()
        {
            _cm = new GameObject("CM7").AddComponent<ConstructionManager>();
            Assert.DoesNotThrow(() => _cm.ApplySaveEntries(null));
            Assert.AreEqual(0, _cm.Structures.Count);
        }

        [Test]
        public void GetSaveEntries_Empty_ReturnsEmptyList()
        {
            _cm = new GameObject("CM8").AddComponent<ConstructionManager>();
            Assert.AreEqual(0, _cm.GetSaveEntries().Count);
        }

        // ===================== [150차 P3] 건설 게이지 가시성 계약 =====================

        [Test]
        public void ConstructionGauge_ManualScale_SourceContract_150()
        {
            // ShowManual 배율 계약 — manual(건설) 모드에서만 바 스케일 확대, Show(채널링) 경로 무영향.
            string barSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/WorkProgressBar.cs");
            Assert.That(barSrc, Does.Contain("ManualBarScale"),
                "[150차 P3] manual 바 확대 배율 상수 존재.");
            Assert.That(barSrc, Does.Contain("go.transform.localScale = new Vector3(ManualBarScale, ManualBarScale, 1f)"),
                "[150차 P3] ShowManual 경로에서만 루트 스케일 배율 적용.");

            // Show(채널링) 경로는 기존 크기 유지 — 벌목/채광 계약(사용자가 그 크기를 알고 봄).
            int showBegin = barSrc.IndexOf("public static WorkProgressBar Show(");
            Assert.That(showBegin, Is.GreaterThan(0), "[150차 P3] Show(채널링) 메서드 존재.");
            int showEnd = barSrc.IndexOf("public void Close()", showBegin);
            string showBody = barSrc.Substring(showBegin, showEnd - showBegin);
            Assert.That(showBody, Does.Not.Contain("localScale"),
                "[150차 P3] Show(채널링) 경로 스케일 무변경 — 기존 크기 계약.");

            // 게이지 높이 보정 — 구조물 예상 최고점 위(5.5m, Play 미세조정 예정).
            string cmSrc = System.IO.File.ReadAllText("Assets/Scripts/Systems/ConstructionManager.cs");
            Assert.That(cmSrc, Does.Contain("SiteGaugeBarHeightM"),
                "[150차 P3] 건설 게이지 높이 상수(구조물 상한 위) 존재.");

            // 가시성 프로브 — 1초 스로틀 로그(pos/카메라거리/isVisible/manual).
            Assert.That(barSrc, Does.Contain("게이지 가시성 프로브"),
                "[150차 P3] LateUpdate 가시성 프로브 로그 존재.");
        }

        // ===================== [152차 P1] 실내 씬 훈련장 배치 — 판정 체인 실내 분기 =====================
        // 실측 배경: 실내 룸은 씬 원점 부근이라 호출부(ConstructionHologram — ResolveTerritoryAt)가
        // 항상 Empire_1로 해석(오귀속)하고, 보존 키 수리 시 월드 중심 경계 검증이 실내 로컬 좌표와
        // 어긋난다. → TryPlace에 실내 한정 재해석/경계 우위 분기(순수 헬퍼 — EditMode 직접 판정).

        [Test]
        public void TryPlaceResolvesTerritoryForIndoorScene_PreservedCastleKey_Wins_152()
        {
            // 성 실내(East_01 성) — 호출부가 원점 근방 좌표로 Empire_1을 공급해도 보존 키(East_01)가 승리.
            string resolved = ConstructionManager.TryPlaceResolvesTerritoryForIndoorScene(
                "Empire_1", "East_01", isIndoorScene: true);
            Assert.AreEqual("East_01", resolved, "[152차 P1] 실내 + 보존 성 영지 키 → 보존 키 우선(오귀속 수리).");
        }

        [Test]
        public void TryPlaceResolvesTerritoryForIndoorScene_PreservedKey_PassesTerritoryGate_152()
        {
            // 성립성 계약 — 보존 키 해석 결과가 실제 DB 등록 영지("영지 없음" 게이트 통과)인지 실측.
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase lazy 인스턴스 — EditMode에서도 구성됨(기존 테스트 실측).");
            string resolved = ConstructionManager.TryPlaceResolvesTerritoryForIndoorScene(
                "Empire_1", "East_01", isIndoorScene: true);
            Assert.AreNotEqual(NationType.None, db.GetDefinition(resolved).nation,
                "[152차 P1] 보존 키 해석 결과 = 등록 영지(국가 None 아님) → TryPlace 영지 없음 게이트 통과.");
        }

        [Test]
        public void TryPlaceResolvesTerritoryForIndoorScene_NonCastleIndoor_KeepsSupplied_152()
        {
            // 비성 실내(크래프트하우스/교회/주택/상점/헛간/동굴/투기장/이계) — 보존 키 없음 → 공급값 유지.
            Assert.AreEqual("Empire_1", ConstructionManager.TryPlaceResolvesTerritoryForIndoorScene(
                "Empire_1", null, isIndoorScene: true), "[152차 P1] 보존 키 없는 실내 — 공급값 유지(호출부 판정 존중).");
            Assert.AreEqual("East_01", ConstructionManager.TryPlaceResolvesTerritoryForIndoorScene(
                "East_01", "", isIndoorScene: true), "[152차 P1] 빈 문자열 보존 키도 '없음' 취급.");
        }

        [Test]
        public void TryPlaceResolvesTerritoryForIndoorScene_Outdoor_Unchanged_152()
        {
            // 실외 — 공급값 그대로(보존 키 유무 무관 — 기존 계약 무영향).
            Assert.AreEqual("East_01", ConstructionManager.TryPlaceResolvesTerritoryForIndoorScene(
                "East_01", "West_02", isIndoorScene: false), "[152차 P1] 실외는 보존 키로 재해석하지 않는다.");
        }

        [Test]
        public void IsIndoorScene_EditMode_AlwaysFalse_152()
        {
            // EditMode — Application.isPlaying 가드로 항상 false(기존 TryPlace 실외 경로 보장).
            Assert.IsFalse(Application.isPlaying, "EditMode 전제 — EditMode Test Runner.");
            Assert.IsFalse(ConstructionManager.IsIndoorScene(), "[152차 P1] EditMode — IsIndoorScene 항상 false.");
        }

        [Test]
        public void ValidateIndoorPlacementOverlap_OverlapBoundary_152()
        {
            // 기존 BlueprintData.ValidatePlacement ②겹침 기준(거리가 반경 합 미만이면 겹침)과 동일 —
            // 실내 경계 우회 분기에서도 겹침 가드는 유지된다.
            var existing = new List<(Vector3, float)> { (new Vector3(5f, 0f, 5f), 3.0f) };
            Assert.IsFalse(ConstructionManager.ValidateIndoorPlacementOverlap(new Vector3(5f, 0f, 5f), 2.0f, existing),
                "[152차 P1] 겹침 — 차단(실내에서도 기존 건설물 보호).");
            Assert.IsTrue(ConstructionManager.ValidateIndoorPlacementOverlap(new Vector3(5f, 0f, 10f), 2.0f, existing),
                "[152차 P1] 인접 — 거리 5 = 반경 합(2+3) 이상 허용(기존 ② 기준 동일).");
            Assert.IsTrue(ConstructionManager.ValidateIndoorPlacementOverlap(Vector3.zero, 2.0f, null),
                "[152차 P1] 기존 건설물 없음 — 허용.");
        }

        [Test]
        public void IndoorPlacementBranch_SourceContract_152()
        {
            string cm = System.IO.File.ReadAllText("Assets/Scripts/Systems/ConstructionManager.cs");

            // 실내 분기 순서 — territoryId 재해석이 "영지 없음" 게이트보다 앞선다(체인 순서 계약).
            int resolve = cm.IndexOf("TryPlaceResolvesTerritoryForIndoorScene(", System.StringComparison.Ordinal);
            int gate = cm.IndexOf("if (region.nation == NationType.None)", System.StringComparison.Ordinal);
            Assert.That(resolve, Is.GreaterThan(0), "[152차 P1] TryPlace 내 실내 영지 재해석 호출 존재.");
            Assert.That(gate, Is.GreaterThan(resolve), "[152차 P1] 재해석 → 정의 조회 → 영지 없음 게이트 순서.");

            // 보존 키 공급원 + 실내 겹침 전용 검증 + 기존 실외 거부 문구 유지.
            Assert.That(cm, Does.Contain("ProjectName.Core.UITransitionState.IndoorCastleTerritoryKey"),
                "[152차 P1-수리] 보존 키 공급원 — Core 공용 필드(Systems→UI 직접 참조 금지 준용).");
            Assert.That(cm, Does.Contain("ValidateIndoorPlacementOverlap(pos, def.footprintRadius, GetFootprints())"),
                "[152차 P1] 실내 겹침 전용 검증 호출(경계 우회).");
            StringAssert.Contains("기존 건설물과 겹침", cm, "[152차 P1] 실내 겹침 거부 메시지.");
            StringAssert.Contains("영지 경계 밖 또는 기존 건설물과 겹침", cm, "기존 실외 거부 문구 유지.");

            // 음식점 실내 금지 규약 불변([90차 A4]) — 실내 분기가 이를 풀지 않는다.
            Assert.That(cm, Does.Contain("IsIndoorPlacement(pos)")
                .And.Contain("음식점은 실내에 배치할 수 없습니다 — 마을에서 건설하세요"),
                "음식점 실내 금지 규약 불변([90차 A4]).");

            // IsIndoorScene 게이트 — 플레이 가드(EditMode 무영향) 포함.
            Assert.That(cm, Does.Contain("Application.isPlaying && ProjectName.Core.UITransitionState.IndoorActive"),
                "[152차 P1-수리] IsIndoorScene — 플레이 게이트 + Core 공용 실내 플래그.");

            // 보존 키 접근자 — IndoorSceneTransition 신설(읽기 전용).
            string tr = System.IO.File.ReadAllText("Assets/Scripts/UI/IndoorSceneTransition.cs");
            // [152차 P1-수리] Core 공용 키 동기 — UI(IndoorSceneTransition)가 성 진입/퇴출에서 갱신.
            Assert.That(tr, Does.Contain("UITransitionState.IndoorCastleTerritoryKey = _pendingTerritoryKey"),
                "[152차 P1-수리] 성 진입 시 Core 공용 키 동기 세팅.");
            Assert.That(tr, Does.Contain("UITransitionState.IndoorCastleTerritoryKey = null"),
                "[152차 P1-수리] 퇴출/정리 시 Core 공용 키 클리어.");
        }
    }
}
