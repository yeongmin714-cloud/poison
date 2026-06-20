using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using System.Reflection;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-26: 약초꾼 임무 테스트 — HerbGatheringMission + HerbPickup.TryAutoGather
    /// </summary>
    public class HerbGatheringTests
    {
        // ===================== 헬퍼 =====================

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(obj, value);
        }

        private static GameObject CreateGuard(GuardRole role, int level, bool recruited, bool alive = true)
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Role = role;
            SetPrivateField(guard, "level", level);
            SetPrivateField(guard, "_isRecruited", recruited);
            if (!alive)
            {
                guard.TakeDamage(999f, Vector3.zero);
            }
            return go;
        }

        private static GameObject CreateHerb(HerbPickup.HerbType type, int minYield = 1, int maxYield = 3)
        {
            var go = new GameObject("TestHerb");
            var herb = go.AddComponent<HerbPickup>();
            SetPrivateField(herb, "_herbType", type);
            SetPrivateField(herb, "_minYield", minYield);
            SetPrivateField(herb, "_maxYield", maxYield);
            SetPrivateField(herb, "_respawnTime", 999f); // long respawn to avoid invoke issues
            return go;
        }

        private static GameObject CreatePlayerInventory()
        {
            var go = new GameObject("TestInventory");
            var inv = go.AddComponent<PlayerInventory>();
            // Force singleton via reflection
            var instanceField = typeof(PlayerInventory).GetField("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceField != null && instanceField.GetValue(null) == null)
            {
                // Singleton already set by Awake, do nothing extra
            }
            return go;
        }

        // ===================== HerbPickup.TryAutoGather 테스트 =====================

        [Test]
        public void HerbPickup_TryAutoGather_ReturnsItemAndYield()
        {
            var go = CreateHerb(HerbPickup.HerbType.Red, 2, 4);
            var herb = go.GetComponent<HerbPickup>();

            bool result = herb.TryAutoGather(out var item, out int yield);

            Assert.IsTrue(result, "TryAutoGather는 성공해야 함");
            Assert.IsNotNull(item, "item은 null이 아니어야 함");
            Assert.GreaterOrEqual(yield, 2, "수확량 >= 최소");
            Assert.LessOrEqual(yield, 4, "수확량 <= 최대");
            Assert.AreEqual("치유초", item.displayName, "Red 약초는 치유초");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void HerbPickup_TryAutoGather_AlreadyHarvested_ReturnsFalse()
        {
            var go = CreateHerb(HerbPickup.HerbType.Purple, 1, 2);
            var herb = go.GetComponent<HerbPickup>();

            // First gather succeeds
            bool first = herb.TryAutoGather(out _, out _);
            Assert.IsTrue(first, "첫 채집은 성공");

            // Second gather should fail
            bool second = herb.TryAutoGather(out var item, out int yield);
            Assert.IsFalse(second, "이미 채집된 약초는 실패");
            Assert.IsNull(item, "item은 null");
            Assert.AreEqual(0, yield, "yield는 0");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void HerbPickup_IsAvailable_AfterGather_ReturnsFalse()
        {
            var go = CreateHerb(HerbPickup.HerbType.Yellow);
            var herb = go.GetComponent<HerbPickup>();

            Assert.IsTrue(herb.IsAvailable, "채집 전에는 Available");

            herb.TryAutoGather(out _, out _);

            Assert.IsFalse(herb.IsAvailable, "채집 후에는 Available=false");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void HerbPickup_TryAutoGather_DifferentTypes_ReturnCorrectItem()
        {
            // Test all herb types
            var types = new (HerbPickup.HerbType type, string displayName)[]
            {
                (HerbPickup.HerbType.Red, "치유초"),
                (HerbPickup.HerbType.Purple, "독나물"),
                (HerbPickup.HerbType.Yellow, "황혼초"),
                (HerbPickup.HerbType.Silver, "은빛 이끼"),
                (HerbPickup.HerbType.Green, "피어리"),
            };

            foreach (var (type, name) in types)
            {
                var go = CreateHerb(type);
                var herb = go.GetComponent<HerbPickup>();

                bool result = herb.TryAutoGather(out var item, out int yield);
                Assert.IsTrue(result, $"{type} 채집 성공");
                Assert.AreEqual(name, item.displayName, $"{type} → {name}");
                Assert.Greater(yield, 0, $"{type} yield > 0");

                Object.DestroyImmediate(go);
            }
        }

        // ===================== HerbGatheringMission.ExecuteGathering 테스트 =====================

        [Test]
        public void ExecuteGathering_NoHerbalists_ReturnsFailure()
        {
            var results = HerbGatheringMission.ExecuteGathering();

            Assert.IsNotEmpty(results, "결과 리스트는 비어있지 않아야 함");
            Assert.IsFalse(results[0].success, "약초꾼이 없으면 실패");
            StringAssert.Contains("약초꾼", results[0].message, "약초꾼 없음 메시지");
        }

        [Test]
        public void ExecuteGathering_HerbalistWithHerb_GathersSuccess()
        {
            var invGo = CreatePlayerInventory();
            var guardGo = CreateGuard(GuardRole.Herbalist, 1, true);
            var herbGo = CreateHerb(HerbPickup.HerbType.Green, 2, 3);

            // Position them close together
            guardGo.transform.position = Vector3.zero;
            herbGo.transform.position = Vector3.one; // ~1.7m apart, well within 15m

            var results = HerbGatheringMission.ExecuteGathering();

            Assert.IsNotEmpty(results, "결과 존재");
            bool anySuccess = false;
            foreach (var r in results)
            {
                if (r.success)
                {
                    anySuccess = true;
                    Assert.Greater(r.herbsGathered, 0, "채집량 > 0");
                    Assert.AreEqual("피어리", r.herbName, "Green 약초 = 피어리");
                    Assert.IsNotEmpty(r.gathererName, "채집자 이름 있음");
                }
            }
            Assert.IsTrue(anySuccess, "하나 이상 성공한 채집 결과가 있어야 함");

            Object.DestroyImmediate(herbGo);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void ExecuteGathering_HerbalistNoHerbs_ReturnsNoHerbs()
        {
            var guardGo = CreateGuard(GuardRole.Herbalist, 1, true);
            guardGo.transform.position = Vector3.zero;

            // No herbs in scene
            var results = HerbGatheringMission.ExecuteGathering();

            Assert.IsNotEmpty(results, "결과 존재");
            foreach (var r in results)
            {
                Assert.IsFalse(r.success, "약초 없으면 실패");
                StringAssert.Contains("약초 없음", r.message, "약초 없음 메시지");
            }

            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteGathering_DeadHerbalist_NotIncluded()
        {
            // Create dead herbalist — should not be found
            var guardGo = CreateGuard(GuardRole.Herbalist, 1, true, alive: false);
            var herbGo = CreateHerb(HerbPickup.HerbType.Red);

            var active = HerbGatheringMission.GetActiveHerbalists();
            Assert.IsEmpty(active, "죽은 약초꾼은 포함되지 않아야 함");

            Object.DestroyImmediate(herbGo);
            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteGathering_UnrecruitedHerbalist_NotIncluded()
        {
            var guardGo = CreateGuard(GuardRole.Herbalist, 1, recruited: false);
            var herbGo = CreateHerb(HerbPickup.HerbType.Red);

            var active = HerbGatheringMission.GetActiveHerbalists();
            Assert.IsEmpty(active, "포섭되지 않은 약초꾼은 포함되지 않아야 함");

            Object.DestroyImmediate(herbGo);
            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteGathering_MultipleHerbalists_AllGather()
        {
            var invGo = CreatePlayerInventory();
            var guardGo1 = CreateGuard(GuardRole.Herbalist, 1, true);
            var guardGo2 = CreateGuard(GuardRole.Herbalist, 2, true);
            var herbGo1 = CreateHerb(HerbPickup.HerbType.Red);
            var herbGo2 = CreateHerb(HerbPickup.HerbType.Purple);

            guardGo1.transform.position = Vector3.zero;
            guardGo2.transform.position = new Vector3(5f, 0, 0);
            herbGo1.transform.position = new Vector3(1f, 0, 0);
            herbGo2.transform.position = new Vector3(6f, 0, 0);

            var results = HerbGatheringMission.ExecuteGathering();

            int successCount = 0;
            foreach (var r in results)
            {
                if (r.success) successCount++;
            }
            Assert.AreEqual(2, successCount, "두 약초꾼 모두 채집 성공");

            Object.DestroyImmediate(herbGo1);
            Object.DestroyImmediate(herbGo2);
            Object.DestroyImmediate(guardGo1);
            Object.DestroyImmediate(guardGo2);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void ExecuteGathering_BonusApplied_BasedOnRole()
        {
            var invGo = CreatePlayerInventory();
            var guardGo = CreateGuard(GuardRole.Herbalist, 1, true);
            var herbGo = CreateHerb(HerbPickup.HerbType.Silver, 2, 2); // exact yield 2

            guardGo.transform.position = Vector3.zero;
            herbGo.transform.position = Vector3.one;

            var results = HerbGatheringMission.ExecuteGathering();

            foreach (var r in results)
            {
                if (r.success)
                {
                    // Base yield = 2, Herbalist bonus = 1.5x, so final = 3
                    Assert.AreEqual(3, r.herbsGathered, "1.5배 보너스 적용: 2 → 3");
                    Assert.AreEqual("은빛 이끼", r.herbName);
                }
            }

            Object.DestroyImmediate(herbGo);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(invGo);
        }

        // ===================== GetActiveHerbalists 테스트 =====================

        [Test]
        public void GetActiveHerbalists_ReturnsOnlyAliveRecruitedHerbalists()
        {
            var g1 = CreateGuard(GuardRole.Herbalist, 1, true);     // should be included
            var g2 = CreateGuard(GuardRole.Herbalist, 1, false);    // not recruited
            var g3 = CreateGuard(GuardRole.Soldier, 1, true);       // wrong role
            var g4 = CreateGuard(GuardRole.Herbalist, 1, true, alive: false); // dead
            var g5 = CreateGuard(GuardRole.Herbalist, 2, true);     // should be included

            var active = HerbGatheringMission.GetActiveHerbalists();

            Assert.AreEqual(2, active.Count, "살아있고 포섭된 Herbalist만 포함");

            Object.DestroyImmediate(g1);
            Object.DestroyImmediate(g2);
            Object.DestroyImmediate(g3);
            Object.DestroyImmediate(g4);
            Object.DestroyImmediate(g5);
        }

        [Test]
        public void GetActiveHerbalists_MaxGatherers_StopsAtLimit()
        {
            var guards = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < HerbGatheringMission.MAX_GATHERERS + 5; i++)
            {
                var g = CreateGuard(GuardRole.Herbalist, 1, true);
                g.transform.position = new Vector3(i * 10f, 0, 0);
                guards.Add(g);
            }

            var active = HerbGatheringMission.GetActiveHerbalists();

            Assert.LessOrEqual(active.Count, HerbGatheringMission.MAX_GATHERERS, "MAX_GATHERERS 제한");

            foreach (var g in guards)
                Object.DestroyImmediate(g);
        }

        [Test]
        public void ExecuteGathering_HerbalistTooFar_Skipped()
        {
            var guardGo = CreateGuard(GuardRole.Herbalist, 1, true);
            var herbGo = CreateHerb(HerbPickup.HerbType.Red);

            // Position far away — beyond GATHER_RANGE + level*0.5
            guardGo.transform.position = Vector3.zero;
            herbGo.transform.position = new Vector3(HerbGatheringMission.GATHER_RANGE + 10f, 0, 0);

            var results = HerbGatheringMission.ExecuteGathering();

            foreach (var r in results)
            {
                if (!r.success)
                {
                    StringAssert.Contains("너무 멀리", r.message, "거리 초과 메시지");
                }
            }

            Object.DestroyImmediate(herbGo);
            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteGathering_HerbalistHigherLevel_GathersFurther()
        {
            var guardGo = CreateGuard(GuardRole.Herbalist, 20, true); // level 20 = +10m range
            var herbGo = CreateHerb(HerbPickup.HerbType.Red);

            // At edge of base range + level bonus
            float maxRange = HerbGatheringMission.GATHER_RANGE + 20 * 0.5f; // 15 + 10 = 25
            guardGo.transform.position = Vector3.zero;
            herbGo.transform.position = new Vector3(maxRange - 0.5f, 0, 0); // just within range

            var results = HerbGatheringMission.ExecuteGathering();

            bool anySuccess = false;
            foreach (var r in results)
            {
                if (r.success) anySuccess = true;
            }
            Assert.IsTrue(anySuccess, "레벨 20 약초꾼은 확장된 범위에서 채집 가능");

            Object.DestroyImmediate(herbGo);
            Object.DestroyImmediate(guardGo);
        }
    }
}