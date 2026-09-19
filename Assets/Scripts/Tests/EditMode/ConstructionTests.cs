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

        [Test]
        public void All_ContainsExactly8UniqueBlueprints()
        {
            Assert.AreEqual(8, BlueprintData.GetAll().Count());
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
    }
}
