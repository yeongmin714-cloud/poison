using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EnvoySystem Intel(정보 수집) 임무 — 7일 주기 정보 스크롤 보고 검증.
    /// [2026-10-21 사용자 요구] 특사 파견 후 7일 주기로 대상 영지 병사 수 + 영주 입맛을
    /// 정보 스크롤로 창고에 전달. 특사 사망 시 보고 중단.
    ///
    /// 결정론 보장: 특사 레벨 40 → 발각 확률 Clamped 0 (Random.value < 0 불가능).
    /// TimeManager.GetOrCreate()는 EditMode에서 동작 (Application.isPlaying=false → DDOL 생략).
    /// </summary>
    public sealed class EnvoyIntelTests
    {
        private GameObject _envoyGo;
        private GameObject _warehouseGo;
        private TimeManager _tm;

        [SetUp]
        public void SetUp()
        {
            // TimeManager — CurrentDay 0 보장
            _tm = TimeManager.GetOrCreate();
            _tm.GameTime = 0f;

            // WarehouseSystem — GetOrCreate 기반 생성 (EditMode에서 Awake 미호출 → Instance null 문제 대응).
            // 잔존 인스턴스 정리 후 신규 생성으로 각 테스트 격리 보장.
            if (WarehouseSystem.Instance != null)
                Object.DestroyImmediate(WarehouseSystem.Instance.gameObject);
            _warehouseGo = WarehouseSystem.GetOrCreate().gameObject;

            // 정보 특사 — Lv.40 (발각 확률 0, 결정론), 포섭 완료
            _envoyGo = new GameObject("[IntelEnvoy-Test]");
            var guard = _envoyGo.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo("테스트 특사", 40, NationType.East);
            guard.SetRecruited(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_envoyGo != null) Object.DestroyImmediate(_envoyGo);
            if (_warehouseGo != null) Object.DestroyImmediate(_warehouseGo);
        }

        /// <summary>
        /// Intel 파견 성공 — Lv.40 특사 (발각 0) → Success 반환, 파견 완료 메시지에 대상 영지명 포함.
        /// </summary>
        [Test]
        public void SendEnvoy_Intel_Lv40_DeterministicSuccess()
        {
            var target = new TerritoryId(NationType.East, 2);
            var envoy = _envoyGo.GetComponent<GuardPlaceholder>();

            var result = EnvoySystem.SendEnvoy(envoy, target, EnvoySystem.EnvoyMission.Intel);

            Assert.IsTrue(result.success, "Lv.40 특사는 발각 확률 0 → 반드시 성공");
            // [고침] 영지명은 결정론 생성이므로 ID가 아닌 이름으로 단언 (ID는 메시지에 노출되지 않음)
            var def = TerritoryDatabase.Instance.GetDefinition(target);
            Assert.IsNotNull(def, "East_02 정의 존재");
            StringAssert.Contains(def.territoryName, result.message, "파견 완료 메시지에 대상 영지명 포함");
            StringAssert.Contains("7일", result.message, "보고 주기 안내 포함");
        }

        /// <summary>
        /// 7일 경과 전 TickIntelReports → 스크롤 미전달 (창고 슬롯 수 불변).
        /// </summary>
        [Test]
        public void TickIntelReports_BeforeInterval_NoScrollDelivered()
        {
            var target = new TerritoryId(NationType.East, 2);
            var envoy = _envoyGo.GetComponent<GuardPlaceholder>();

            var dispatch = EnvoySystem.SendEnvoy(envoy, target, EnvoySystem.EnvoyMission.Intel);
            Assume.That(dispatch.success, Is.True, "발각 0 결정론 파견");

            string key = TerritoryManager.Instance != null
                ? TerritoryManager.Instance.CurrentTerritoryId.ToString()
                : "East_01";
            // [NRE 방어] EditMode 싱글턴 fake-null 케이스 — 존재하지 않으면 0으로 취급
            int slotsBefore = (WarehouseSystem.Instance != null)
                ? WarehouseSystem.Instance.GetItemCount(key) : 0;

            // 6일 경과 — 주기(7일) 미도래
            _tm.GameTime = 6 * 86400f;
            EnvoySystem.TickIntelReports();

            int slotsAfter = (WarehouseSystem.Instance != null)
                ? WarehouseSystem.Instance.GetItemCount(key) : 0;
            Assert.AreEqual(slotsBefore, slotsAfter,
                "7일 전에는 정보 스크롤이 창고에 전달되지 않아야 함");
        }

        /// <summary>
        /// 7일 경과 TickIntelReports → 정보 스크롤 전달 + 내용 검증
        /// (병사 수 + 영주 입맛 = preferredFood). 이후 14일차에 2번째 보고(주기 갱신 확인).
        /// </summary>
        [Test]
        public void TickIntelReports_AtInterval_DeliversScrollWithIntel()
        {
            var target = new TerritoryId(NationType.East, 2);
            var envoy = _envoyGo.GetComponent<GuardPlaceholder>();

            var dispatch = EnvoySystem.SendEnvoy(envoy, target, EnvoySystem.EnvoyMission.Intel);
            Assume.That(dispatch.success, Is.True, "발각 0 결정론 파견");

            // 대상 영지 기준 데이터
            var def = TerritoryDatabase.Instance.GetDefinition(target);
            Assume.That(def, Is.Not.Null, "East_02 정의 존재");
            string expectedFood = string.IsNullOrEmpty(def.lord.preferredFood)
                ? "알 수 없음" : def.lord.preferredFood;

            // 7일 경과 → 1차 보고.
            // [주의] TimeManager.GameTime setter는 _currentDay에 dayDelta를 누적하는 방식이므로
            // 절댓값 재대입 시 일차가 겹쳐 쌓인다. 델타 경과로 진행하고 스크롤 id도
            // CurrentDay에서 동적으로 산출 (절대 일차 하드코딩 금지 — 누적 일차 회귀 방지).
            _tm.GameTime += 7 * 86400f;
            EnvoySystem.TickIntelReports();
            int day1 = _tm.CurrentDay;

            string scrollId = $"intel_scroll_{target}_{day1}";
            var found = FindScroll(scrollId);
            Assert.IsNotNull(found, "7일 경과 후 정보 스크롤이 창고에 전달되어야 함");

            // 스크롤 내용 — 병사 수 + 영주 입맛
            StringAssert.Contains($"병력: {def.guardCount}명", found.description,
                "스크롤에 대상 영지 병사 수 기록");
            StringAssert.Contains($"영주 입맛: {expectedFood}", found.description,
                "스크롤에 영주 선호 음식 기록");
            StringAssert.Contains(def.territoryName, found.displayName, "스크롤 이름에 영지명 포함");

            // 7일 추가 경과 → 2차 보고 (주기 갱신 확인)
            _tm.GameTime += 7 * 86400f;
            EnvoySystem.TickIntelReports();
            int day2 = _tm.CurrentDay;

            string scrollId2 = $"intel_scroll_{target}_{day2}";
            var found2 = FindScroll(scrollId2);
            Assert.IsNotNull(found2, "2차 보고서가 전달되어야 함 (주기 갱신)");
            Assert.AreNotEqual(day1, day2, "2차 보고서는 1차와 다른 일차 id를 가져야 함");
        }

        /// <summary>
        /// 특사 사망 → 보고 중단 (7일 경과해도 스크롤 미전달).
        /// </summary>
        [Test]
        public void TickIntelReports_EnvoyDead_NoDelivery()
        {
            var target = new TerritoryId(NationType.East, 3);
            var envoy = _envoyGo.GetComponent<GuardPlaceholder>();

            var dispatch = EnvoySystem.SendEnvoy(envoy, target, EnvoySystem.EnvoyMission.Intel);
            Assume.That(dispatch.success, Is.True, "발각 0 결정론 파견");

            // 특사 사망 — 보고 중단 트리거
            envoy.TakeDamage(9999f, Vector3.zero, "test-kill");
            Assume.That(envoy.IsAlive, Is.False, "TakeDamage(9999) 후 사망");

            // 7일 경과 — 사망 특사는 스크롤 미전달
            _tm.GameTime = 7 * 86400f;
            EnvoySystem.TickIntelReports();

            var found = FindScroll($"intel_scroll_{target}_7");
            Assert.IsNull(found, "사망한 특사는 7일 경과 후에도 보고서를 전달하지 않아야 함");
        }

        /// <summary>모든 창고에서 스크롤 id 검색 — TerritoryManager 존재 여부와 무관하게 안전.</summary>
        private static PlayerInventory.ItemData FindScroll(string scrollId)
        {
            // [NRE 방어] EditMode에서 싱글턴이 fake-null로 남아있을 수 있으므로 방어
            if (WarehouseSystem.Instance == null) return null;
            var save = WarehouseSystem.Instance.GetSaveData();
            if (save == null || save.warehouseData == null) return null;
            foreach (var entry in save.warehouseData)
            {
                if (entry?.slots == null) continue;
                foreach (var slot in entry.slots)
                {
                    if (slot?.item != null && slot.item.id == scrollId)
                        return slot.item;
                }
            }
            return null;
        }
    }
}
