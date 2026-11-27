using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [65차 P1] 무기 로드아웃 2슬롯 시스템 테스트.
    /// Equip은 Transform/GLB 프리팹이 필요해 EditMode에서 직접 호출 불가 →
    /// Equip 성공 분기 후크가 호출하는 public static RegisterWeaponLoadout을 직접 검증한다.
    /// PlayerPrefs 격리: poison_weapon_loadout_* 키를 SetUp/TearDown에서 전부 DeleteKey
    /// (저장 강제 금지 — DeleteKey만으로 세션 내 격리, TitleTests/HungerTests 패턴).
    /// 정적 상태(_loadout/_loadoutLoaded)는 리플렉션 리셋으로 테스트 간 오염 차단.
    /// </summary>
    public class WeaponLoadoutTests
    {
        // LoadoutPrefsId("poison_weapon_loadout_{0}") + 타입 접미사 키 — 사용 키 전부
        private static readonly string[] PrefsKeys =
        {
            "poison_weapon_loadout_0", "poison_weapon_loadout_0_type",
            "poison_weapon_loadout_1", "poison_weapon_loadout_1_type",
        };

        [SetUp]
        public void SetUp()
        {
            CleanupPrefs();
            ResetLoadoutState();
        }

        [TearDown]
        public void TearDown()
        {
            ResetLoadoutState();
            CleanupPrefs();
        }

        private static void CleanupPrefs()
        {
            foreach (var k in PrefsKeys)
                PlayerPrefs.DeleteKey(k);   // 저장 강제 금지 — DeleteKey만(세션 내 격리 목적)
        }

        /// <summary>정적 로드아웃 상태 리셋 — 지연 로드 게이트를 풀어 다음 접근이 빈 prefs에서 재구축하게 한다.</summary>
        private static void ResetLoadoutState()
        {
            var arr = typeof(WeaponEquipManager)
                .GetField("_loadout", BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null) as WeaponEquipManager.WeaponLoadoutEntry[];
            if (arr != null) { arr[0] = default; arr[1] = default; }
            typeof(WeaponEquipManager)
                .GetField("_loadoutLoaded", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, false);
        }

        // ─────────────── Test1: 순차 채움 ───────────────

        [Test]
        public void RegisterWeaponLoadout_SequentialFill()
        {
            WeaponEquipManager.RegisterWeaponLoadout("weapon_sword_wood", WeaponType.Sword);

            var s0 = WeaponEquipManager.GetLoadoutSlot(0);
            Assert.AreEqual("weapon_sword_wood", s0.ItemId, "첫 등록 → 로드아웃 슬롯 1([0])");
            Assert.AreEqual(WeaponType.Sword, s0.Type, "타입 저장");
            Assert.IsTrue(s0.Equipped, "등록 시 Equipped=true");
            Assert.IsTrue(string.IsNullOrEmpty(WeaponEquipManager.GetLoadoutSlot(1).ItemId), "슬롯 2([1])는 아직 빈 상태");

            WeaponEquipManager.RegisterWeaponLoadout("weapon_spear_wood", WeaponType.Spear);

            var s1 = WeaponEquipManager.GetLoadoutSlot(1);
            Assert.AreEqual("weapon_spear_wood", s1.ItemId, "두 번째 등록 → 로드아웃 슬롯 2([1])");
            Assert.AreEqual(WeaponType.Spear, s1.Type, "타입 저장");
            Assert.IsTrue(s1.Equipped);
            Assert.AreEqual("weapon_sword_wood", WeaponEquipManager.GetLoadoutSlot(0).ItemId, "슬롯 1 불변");
        }

        // ─────────────── Test2: 동일 무기 재등록 ───────────────

        [Test]
        public void SameWeapon_Reregister_KeepsSlot()
        {
            WeaponEquipManager.RegisterWeaponLoadout("weapon_sword_wood", WeaponType.Sword);
            WeaponEquipManager.RegisterWeaponLoadout("weapon_spear_wood", WeaponType.Spear);

            // A(슬롯 1 무기) 재등록 — 슬롯 재배치 없이 그 슬롯에서 갱신만
            WeaponEquipManager.RegisterWeaponLoadout("weapon_sword_wood", WeaponType.Sword);

            Assert.AreEqual("weapon_sword_wood", WeaponEquipManager.GetLoadoutSlot(0).ItemId, "A는 [0] 유지");
            Assert.AreEqual("weapon_spear_wood", WeaponEquipManager.GetLoadoutSlot(1).ItemId, "B는 [1] 유지 — 재등록이 순서를 흔들지 않음");
            Assert.IsTrue(WeaponEquipManager.GetLoadoutSlot(0).Equipped, "재등록 슬롯 Equipped=true 유지");
        }

        // ─────────────── Test3: 포화 시 [1] 덮어쓰기 ───────────────

        [Test]
        public void Overflow_OverwritesSlot2()
        {
            WeaponEquipManager.RegisterWeaponLoadout("weapon_sword_wood", WeaponType.Sword);
            WeaponEquipManager.RegisterWeaponLoadout("weapon_spear_wood", WeaponType.Spear);

            // 세 번째 무기 — 둘 다 차 있으므로 [1]만 교체(최근 장착 우선)
            WeaponEquipManager.RegisterWeaponLoadout("weapon_bow_wood", WeaponType.Bow);

            Assert.AreEqual("weapon_sword_wood", WeaponEquipManager.GetLoadoutSlot(0).ItemId, "포화 시에도 슬롯 1([0]) 불변");
            Assert.AreEqual("weapon_bow_wood", WeaponEquipManager.GetLoadoutSlot(1).ItemId, "포화 시 슬롯 2([1])를 최근 장착 무기로 덮어쓰기");
            Assert.AreEqual(WeaponType.Bow, WeaponEquipManager.GetLoadoutSlot(1).Type, "덮어쓴 슬롯 타입 갱신");
            Assert.IsTrue(WeaponEquipManager.GetLoadoutSlot(1).Equipped);
        }

        // ─────────────── Test4: 해제 시 Equipped 플래그만 해제 ───────────────

        [Test]
        public void Unequip_ClearsEquippedFlag()
        {
            WeaponEquipManager.RegisterWeaponLoadout("weapon_sword_wood", WeaponType.Sword);
            Assert.IsTrue(WeaponEquipManager.GetLoadoutSlot(0).Equipped, "전제: 장착 등록 상태");

            // Unequip은 CurrentId(장착 무기) 기준으로 슬롯 플래그를 해제 — EditMode에서
            // Equip 전체 경로 대신 CurrentId를 리플렉션으로 세팅해 실제 Unequip 경로를 검증.
            typeof(WeaponEquipManager).GetProperty("CurrentId")?.SetValue(null, "weapon_sword_wood");

            WeaponEquipManager.Unequip();

            var s0 = WeaponEquipManager.GetLoadoutSlot(0);
            Assert.AreEqual("weapon_sword_wood", s0.ItemId, "등록(ItemId) 자체는 유지 — 핫바 등록 유지 = 재장착 토글 가능");
            Assert.IsFalse(s0.Equipped, "해제 → Equipped=false");
        }
    }
}
