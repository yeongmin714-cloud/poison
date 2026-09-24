using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// P18-C4 — 크래프트 벤치 3종 시스템 검증.
    /// 무기 제작(신규 WeaponCraftDatabase+CraftWeapon), 요리/물약 레시피 나열, 멀티셋 매칭.
    /// EditMode 규약: AddComponent Awake 미실행 → PlayerInventory는 컴포넌트 참조로 생성.
    /// </summary>
    public class CraftBenchTests
    {
        private PlayerInventory _inv;

        [SetUp]
        public void SetUp()
        {
            PlayerInventory.ResetInstance();
            var go = new GameObject("TestInventory");
            _inv = go.AddComponent<PlayerInventory>();
            // [EditMode 규약] AddComponent는 Awake 미실행 — Instance/슬롯 리플렉션 보장.
            var instProp = typeof(PlayerInventory).GetProperty("Instance");
            if (instProp != null && instProp.GetValue(null) == null)
                instProp.SetValue(null, _inv);
            if (_inv.GetAllSlots() == null)
            {
                var f = typeof(PlayerInventory).GetField("_slots",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var maxF = typeof(PlayerInventory).GetField("_maxSlots",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int max = maxF != null ? (int)maxF.GetValue(_inv) : 30;
                f.SetValue(_inv, new PlayerInventory.ItemSlot[max]);
            }
        }

        [TearDown]
        public void TearDown()
        {
            PlayerInventory.ResetInstance();
        }

        private void Give(string itemId, int count)
        {
            var item = PlayerInventory.GetItemById(itemId);
            Assert.IsNotNull(item, $"아이템 존재: {itemId}");
            Assert.IsTrue(_inv.AddItem(item, count));
        }

        // ─────────────── C1 무기 제작 ───────────────

        [Test]
        public void WeaponCraft_AllRecipes_HaveValidResultItems()
        {
            var all = WeaponCraftDatabase.All;
            Assert.Greater(all.Count, 0, "유효 무기 레시피 존재");
            foreach (var r in all)
            {
                Assert.IsNotNull(PlayerInventory.GetItemById(r.ResultId), $"결과 아이템 존재: {r.ResultId}");
                Assert.Greater(r.Mat1Count, 0);
                Assert.IsNotNull(PlayerInventory.GetItemById(r.Mat1Id), $"재료 아이템 존재: {r.Mat1Id}");
            }
        }

        [Test]
        public void WeaponCraft_CraftSuccess_ConsumesMaterials_GivesResult()
        {
            Give("mat_boar_tusk", 2);
            int before = _inv.GetItemCount("weapon_sword_wood");

            bool ok = CraftingHelper.CraftWeapon("weapon_sword_wood", out var msg);

            Assert.IsTrue(ok, msg);
            Assert.AreEqual(0, _inv.GetItemCount("mat_boar_tusk"), "재료 소모");
            Assert.AreEqual(before + 1, _inv.GetItemCount("weapon_sword_wood"), "결과 지급");
        }

        [Test]
        public void WeaponCraft_InsufficientMaterials_Fails()
        {
            Give("mat_boar_tusk", 1);   // 목검엔 2 필요
            bool ok = CraftingHelper.CraftWeapon("weapon_sword_wood", out var msg);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(msg);
            Assert.AreEqual(1, _inv.GetItemCount("mat_boar_tusk"), "실패 시 재료 보존");
        }

        [Test]
        public void WeaponCraft_MultisetMatch_FindsRecipe()
        {
            var placed = new List<string> { "mat_boar_tusk", "mat_boar_tusk" };
            bool ok = WeaponCraftDatabase.TryMatch(placed, out var r);
            Assert.IsTrue(ok);
            Assert.AreEqual("weapon_sword_wood", r.ResultId);
        }

        [Test]
        public void WeaponCraft_MultisetMatch_WrongMaterials_Fails()
        {
            var placed = new List<string> { "mat_wolf_tooth", "mat_rabbit_fur" };
            bool ok = WeaponCraftDatabase.TryMatch(placed, out _);
            Assert.IsFalse(ok, "매칭 안 되는 조합");
        }

        // ─────────────── C2 요리/물약 레시피 나열 ───────────────

        [Test]
        public void CookingDatabase_AllRecipes_Loaded()
        {
            // 레거시 2-key CookingDatabase/DishDatabase는 허브 조합이 물약 전용으로 이동하면서
            // GAME_DATA에서 0행으로 의도적으로 비워짐 (2026-09). 요리 카탈로그는
            // 코드 기반 RecipeCatalog(760종)로 대체됨 — RecipeCatalogTests 참조.
            Assert.AreEqual(0, ProjectName.Core.Data.CookingDatabase.AllRecipes.Count,
                "레거시 요리 DB는 의도적으로 0행 (요리는 RecipeCatalog 사용)");
            Assert.AreEqual(760, RecipeCatalog.TotalCount, "신규 요리 카탈로그 760종 로드");
        }

        [Test]
        public void HerbComboDatabase_AllCombos_Loaded_AndKnownPairMatches()
        {
            Assert.Greater(ProjectName.Core.Data.HerbComboDatabase.AllCombos.Count, 0, "연금술 조합 DB 로드");
            // GAME_DATA 실측 조합 — 붉은줄기 + 회복꽃 = 혈압 상승제
            var id1 = ProjectName.Core.Data.HerbDatabase.GetHerbInfoByDisplayName("붉은줄기");
            var id2 = ProjectName.Core.Data.HerbDatabase.GetHerbInfoByDisplayName("회복꽃");
            Assert.IsFalse(string.IsNullOrEmpty(id1.id), "HerbDatabase '붉은줄기' 존재");
            Assert.IsFalse(string.IsNullOrEmpty(id2.id), "HerbDatabase '회복꽃' 존재");
            Assert.IsTrue(ProjectName.Core.Data.HerbComboDatabase.GetCombo(id1.id, id2.id).HasValue, "알려진 조합 매칭");
        }

        // ─────────────── C3 스테이션 이벤트 배선 ───────────────

        [Test]
        public void CraftingHelper_NotifyCraftSucceeded_FiresEvent()
        {
            string received = null;
            System.Action<string> handler = id => received = id;
            CraftingHelper.CraftSucceeded += handler;

            Give("mat_boar_tusk", 2);
            CraftingHelper.CraftWeapon("weapon_sword_wood", out _);

            CraftingHelper.CraftSucceeded -= handler;
            Assert.AreEqual("weapon_sword_wood", received, "칭호 카운터 경유 발화 확인");
        }
    }
}
