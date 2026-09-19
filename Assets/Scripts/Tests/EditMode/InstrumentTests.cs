using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase O7: 바드 악기 확장 테스트 — InstrumentData 5종 프로필/ItemData 변환/
    /// IsItemValidForSlot Instrument 슬롯 통과/연주 시스템 시작·중복·종료·쿨다운.
    /// 싱글톤 통합 테스트는 TitleTests 패턴(GameObject + AddComponent)으로 EditMode 생성.
    /// </summary>
    public class InstrumentTests
    {
        // ===================== 프로필 표 (5종 퍼센트 정확) =====================

        [Test]
        public void Profile_BardLute_15_10_10()
        {
            var p = InstrumentData.GetProfile("instrument_bard_lute");
            Assert.IsTrue(p.HasValue, "류트 프로필 존재");
            Assert.AreEqual("바드의 류트", p.Value.displayName);
            Assert.AreEqual(15f, p.Value.attackBuffPercent);
            Assert.AreEqual(10f, p.Value.defenseBuffPercent);
            Assert.AreEqual(10f, p.Value.speedBuffPercent);
            Assert.IsFalse(string.IsNullOrEmpty(p.Value.flavorText));
        }

        [Test]
        public void Profile_WarFlute_20_5_15()
        {
            var p = InstrumentData.GetProfile("instrument_flute_war");
            Assert.IsTrue(p.HasValue, "피리 프로필 존재");
            Assert.AreEqual("전쟁 피리", p.Value.displayName);
            Assert.AreEqual(20f, p.Value.attackBuffPercent);
            Assert.AreEqual(5f, p.Value.defenseBuffPercent);
            Assert.AreEqual(15f, p.Value.speedBuffPercent);
        }

        [Test]
        public void Profile_MarchDrum_10_15_20()
        {
            var p = InstrumentData.GetProfile("instrument_drum_march");
            Assert.IsTrue(p.HasValue, "드럼 프로필 존재");
            Assert.AreEqual("행군 드럼", p.Value.displayName);
            Assert.AreEqual(10f, p.Value.attackBuffPercent);
            Assert.AreEqual(15f, p.Value.defenseBuffPercent);
            Assert.AreEqual(20f, p.Value.speedBuffPercent);
        }

        [Test]
        public void Profile_HarpOfJoy_10_10_10()
        {
            var p = InstrumentData.GetProfile("instrument_harp_joy");
            Assert.IsTrue(p.HasValue, "하프 프로필 존재");
            Assert.AreEqual("환희의 하프", p.Value.displayName);
            Assert.AreEqual(10f, p.Value.attackBuffPercent);
            Assert.AreEqual(10f, p.Value.defenseBuffPercent);
            Assert.AreEqual(10f, p.Value.speedBuffPercent);
        }

        [Test]
        public void Profile_ChargeHorn_25_5_5()
        {
            var p = InstrumentData.GetProfile("instrument_horn_charge");
            Assert.IsTrue(p.HasValue, "나팔 프로필 존재");
            Assert.AreEqual("돌격 나팔", p.Value.displayName);
            Assert.AreEqual(25f, p.Value.attackBuffPercent);
            Assert.AreEqual(5f, p.Value.defenseBuffPercent);
            Assert.AreEqual(5f, p.Value.speedBuffPercent);
        }

        // ===================== 조회/변환 =====================

        [Test]
        public void GetProfile_UnknownOrNull_ReturnsNull()
        {
            Assert.IsFalse(InstrumentData.GetProfile("instrument_unknown").HasValue, "미등록 id → null");
            Assert.IsFalse(InstrumentData.GetProfile("").HasValue, "빈 문자열 → null");
            Assert.IsFalse(InstrumentData.GetProfile(null).HasValue, "null → null");
        }

        [Test]
        public void ToItemData_FieldsCorrect()
        {
            var profile = InstrumentData.GetProfile("instrument_bard_lute").Value;
            var item = InstrumentData.ToItemData(profile);

            Assert.AreEqual("instrument_bard_lute", item.id);
            Assert.AreEqual("바드의 류트", item.displayName);
            Assert.AreEqual("익숙한 류트. 버프 밸런스형.", item.description);
            Assert.AreEqual(PlayerInventory.ItemCategory.Material, item.category);
            Assert.AreEqual(ItemRarity.Uncommon, item.rarity);
            Assert.AreEqual(1, item.maxStack);
            Assert.AreEqual(150, item.basePrice);
        }

        [Test]
        public void AllItemData_FiveItems_AllIdsContainInstrumentPrefix()
        {
            var items = InstrumentData.AllItemData;
            Assert.AreEqual(5, items.Length);
            foreach (var item in items)
            {
                Assert.IsTrue(item.id.Contains("instrument_"),
                    $"{item.id} — IsItemValidForSlot Instrument 슬롯 통과 조건(id Contains \"instrument\")");
            }
        }

        [Test]
        public void Count_Is5_And_AllUnique()
        {
            Assert.AreEqual(5, InstrumentData.Count);
            Assert.AreEqual(5, InstrumentData.All.Length);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var p in InstrumentData.All)
                Assert.IsTrue(seen.Add(p.itemId), $"중복 id 없음: {p.itemId}");
        }

        [Test]
        public void IsItemValidForSlot_InstrumentSlot_AcceptsAllInstrumentItems()
        {
            var go = new GameObject("GuardEquipmentSystemTest");
            var ges = go.AddComponent<GuardEquipmentSystem>();

            foreach (var item in InstrumentData.AllItemData)
            {
                Assert.IsTrue(ges.IsItemValidForSlot(GuardEquipmentSystem.EquipSlot.Instrument, item, true),
                    $"바드 Instrument 슬롯 통과: {item.id}");
                Assert.IsFalse(ges.IsItemValidForSlot(GuardEquipmentSystem.EquipSlot.Instrument, item, false),
                    $"비바드는 Instrument 슬롯 불가: {item.id}");
            }

            Object.DestroyImmediate(go);
        }

        // ===================== 연주 시스템 (통합) =====================

        private InstrumentPerformanceSystem _ips;
        private GameObject _ipsGo;
        private GameObject _statsGo;
        private GameObject _buffGo;
        private GameObject _mercGo;
        private GameObject _equipGo;

        [TearDown]
        public void TearDown()
        {
            // 싱글톤 정리 (Instance 정적 참조 리셋 포함)
            ResetSingleton<InstrumentPerformanceSystem>();
            ResetSingleton<BardBuffManager>();
            ResetSingleton<MercenaryManager>();
            ResetSingleton<GuardEquipmentSystem>();
            PlayerStats.ResetInstance();

            foreach (var go in new[] { _ipsGo, _statsGo, _buffGo, _mercGo, _equipGo })
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _ipsGo = _statsGo = _buffGo = _mercGo = _equipGo = null;
            _ips = null;
        }

        private static void ResetSingleton<T>() where T : MonoBehaviour
        {
            var field = typeof(T).GetField("<Instance>k__BackingField",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null) field.SetValue(null, null);
        }

        /// <summary>통합 셋업: PlayerStats(Lv10 — tavern_mercenary 게이트 통과) + BardBuffManager
        /// + MercenaryManager(바드 고용) + GuardEquipmentSystem(류트 장착) + 연주 시스템.</summary>
        private void SetupPerformanceStack()
        {
            _statsGo = new GameObject("PlayerStatsTest");
            var stats = _statsGo.AddComponent<PlayerStats>();
            // _level private SerializeField — tavern_mercenary 게이트(minLevel 5) 통과용
            var levelField = typeof(PlayerStats).GetField("_level",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            levelField.SetValue(stats, 10);

            _buffGo = new GameObject("BardBuffManagerTest");
            _buffGo.AddComponent<BardBuffManager>();

            _mercGo = new GameObject("MercenaryManagerTest");
            var mercMgr = _mercGo.AddComponent<MercenaryManager>();
            // [O7] 싱글턴 프로퍼티 대신 컴포넌트 참조 — Awake 미실행 환경 대응
            Assert.IsTrue(mercMgr.HireMercenary("merc_bard_01"), "바드 고용");

            _equipGo = new GameObject("GuardEquipmentSystemTest");
            var equipSys = _equipGo.AddComponent<GuardEquipmentSystem>();
            var lute = InstrumentData.ToItemData(InstrumentData.GetProfile("instrument_bard_lute").Value);
            Assert.IsTrue(equipSys.EquipMercenary(
                "merc_bard_01", GuardEquipmentSystem.EquipSlot.Instrument, lute), "바드 악기 장착");

            _ipsGo = new GameObject("InstrumentPerformanceSystemTest");
            _ips = _ipsGo.AddComponent<InstrumentPerformanceSystem>();
        }

        [Test]
        public void Performance_TryStart_Success_IsPerforming()
        {
            SetupPerformanceStack();

            Assert.IsTrue(_ips.TryStartPerformance("instrument_bard_lute"), "연주 시작 성공");
            Assert.IsTrue(_ips.IsPerforming, "연주 중 플래그");
            Assert.Greater(_ips.RemainingSeconds, 0f, "남은 시간 > 0");
            Assert.AreEqual("instrument_bard_lute", _ips.ActiveInstrumentId);
        }

        [Test]
        public void Performance_DoubleStart_Blocked_WhilePerforming()
        {
            SetupPerformanceStack();
            Assert.IsTrue(_ips.TryStartPerformance("instrument_bard_lute"), "첫 연주 성공");

            Assert.IsFalse(_ips.TryStartPerformance("instrument_horn_charge"), "연주 중 재시작 차단");
            Assert.IsTrue(_ips.IsPerforming, "여전히 연주 중");
            Assert.AreEqual("instrument_bard_lute", _ips.ActiveInstrumentId, "원래 악기 유지");
        }

        [Test]
        public void Performance_EndPerformance_ClearsState_AndCooldownBlocks()
        {
            SetupPerformanceStack();
            Assert.IsTrue(_ips.TryStartPerformance("instrument_bard_lute"), "연주 시작");

            _ips.EndPerformance(); // 60초 수동 경과 불가 → 직접 호출 (자동 종료와 동일 경로)

            Assert.IsFalse(_ips.IsPerforming, "종료 후 연주 중 해제");
            Assert.AreEqual(0f, _ips.RemainingSeconds, "남은 시간 0");

            // 쿨다운(30초) — 즉시 재시작 차단
            Assert.IsFalse(_ips.TryStartPerformance("instrument_bard_lute"), "쿨다운 중 재시작 차단");
            Assert.IsFalse(_ips.IsPerforming, "차단 후에도 비연주 상태");
        }

        // ===================== 쿨다운 순수 판정 =====================

        [Test]
        public void CanStartAgain_Static_CooldownBoundary()
        {
            // 이력 없음(-999) → 항상 허용
            Assert.IsTrue(InstrumentPerformanceSystem.CanStartAgain(-999f, 0f, 30f));
            // 종료 직후 → 차단
            Assert.IsFalse(InstrumentPerformanceSystem.CanStartAgain(100f, 100f, 30f));
            Assert.IsFalse(InstrumentPerformanceSystem.CanStartAgain(100f, 129.9f, 30f));
            // 30초 경과(경계 포함) → 허용
            Assert.IsTrue(InstrumentPerformanceSystem.CanStartAgain(100f, 130f, 30f));
            Assert.IsTrue(InstrumentPerformanceSystem.CanStartAgain(100f, 500f, 30f));
        }
    }
}
