using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using System.Reflection;
using System.Collections.Generic;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-27: 사냥꾼 임무 테스트 — HuntingMission + AnimalAI.TryAutoHunt
    /// </summary>
    public class HuntingTests
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

        private static GameObject CreateAnimal(string monsterId, bool alive = true, 
            PlayerInventory.ItemData meatDrop = null, int minMeat = 1, int maxMeat = 2,
            PlayerInventory.ItemData materialDrop = null, int materialCount = 1,
            PlayerInventory.ItemData rareDrop = null, float rareDropChance = 0f)
        {
            var go = new GameObject("TestAnimal");
            var animal = go.AddComponent<AnimalAI>();
            
            // Set monster ID via SetMonsterId public method
            animal.SetMonsterId(monsterId);
            
            // Set private fields via reflection
            SetPrivateField(animal, "_isDead", !alive);
            SetPrivateField(animal, "_meatDrop", meatDrop);
            SetPrivateField(animal, "_minMeat", minMeat);
            SetPrivateField(animal, "_maxMeat", maxMeat);
            SetPrivateField(animal, "_materialDrop", materialDrop);
            SetPrivateField(animal, "_materialCount", materialCount);
            SetPrivateField(animal, "_rareDrop", rareDrop);
            SetPrivateField(animal, "_rareDropChance", rareDropChance);
            
            return go;
        }

        private static GameObject CreatePlayerInventory()
        {
            var go = new GameObject("TestInventory");
            var inv = go.AddComponent<PlayerInventory>();
            // Singleton already set by Awake, do nothing extra
            return go;
        }

        // ===================== AnimalAI.TryAutoHunt 테스트 =====================

        [Test]
        public void AnimalAI_TryAutoHunt_ReturnsDrops()
        {
            var go = CreateAnimal("rabbit", alive: true,
                meatDrop: PlayerInventory.RabbitMeat, minMeat: 1, maxMeat: 2,
                materialDrop: PlayerInventory.RabbitFur, materialCount: 1);
            var animal = go.GetComponent<AnimalAI>();

            bool result = animal.TryAutoHunt(out var drops);

            Assert.IsTrue(result, "TryAutoHunt는 성공해야 함");
            Assert.IsNotEmpty(drops, "드랍 리스트는 비어있지 않아야 함");
            Assert.GreaterOrEqual(drops.Count, 1, "최소 1개 이상 드랍");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AnimalAI_TryAutoHunt_DeadReturnsFalse()
        {
            var go = CreateAnimal("rabbit", alive: false);
            var animal = go.GetComponent<AnimalAI>();

            bool result = animal.TryAutoHunt(out var drops);

            Assert.IsFalse(result, "죽은 동물은 사냥 실패");
            Assert.IsEmpty(drops, "드랍 리스트는 비어있어야 함");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AnimalAI_TryAutoHunt_IsDeadAfterHunt()
        {
            var go = CreateAnimal("boar", alive: true,
                meatDrop: PlayerInventory.BoarMeat, minMeat: 1, maxMeat: 2);
            var animal = go.GetComponent<AnimalAI>();

            Assert.IsTrue(animal.IsAlive, "사냥 전에는 살아있음");

            animal.TryAutoHunt(out _);

            Assert.IsTrue(animal.IsDead, "사냥 후에는 죽음");
            Assert.IsFalse(animal.IsAlive, "사냥 후에는 IsAlive=false");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AnimalAI_TryAutoHunt_DifferentMonsters_DifferentDrops()
        {
            // Rabbit
            var rabbitGo = CreateAnimal("rabbit", alive: true,
                meatDrop: PlayerInventory.RabbitMeat, minMeat: 1, maxMeat: 2,
                materialDrop: PlayerInventory.RabbitFur, materialCount: 1);
            var rabbit = rabbitGo.GetComponent<AnimalAI>();
            rabbit.TryAutoHunt(out var rabbitDrops);

            Assert.IsTrue(rabbitDrops.Exists(d => d.item.id == "meat_rabbit"), "토끼 고기 드랍");
            Assert.IsTrue(rabbitDrops.Exists(d => d.item.id == "mat_rabbit_fur"), "토끼털 드랍");

            // Boar
            var boarGo = CreateAnimal("boar", alive: true,
                meatDrop: PlayerInventory.BoarMeat, minMeat: 1, maxMeat: 2,
                materialDrop: PlayerInventory.BoarLeather, materialCount: 1,
                rareDrop: PlayerInventory.BoarTusk, rareDropChance: 1.0f);
            var boar = boarGo.GetComponent<AnimalAI>();
            boar.TryAutoHunt(out var boarDrops);

            Assert.IsTrue(boarDrops.Exists(d => d.item.id == "meat_boar"), "멧돼지 고기 드랍");
            Assert.IsTrue(boarDrops.Exists(d => d.item.id == "mat_boar_leather"), "멧돼지 가죽 드랍");
            Assert.IsTrue(boarDrops.Exists(d => d.item.id == "mat_boar_tusk"), "멧돼지 엄니 드랍 (100%)");

            Object.DestroyImmediate(rabbitGo);
            Object.DestroyImmediate(boarGo);
        }

        [Test]
        public void AnimalAI_TryAutoHunt_RareDropByChance()
        {
            // Force rare drop with 100% chance
            var go = CreateAnimal("wolf", alive: true,
                meatDrop: PlayerInventory.WolfMeat, minMeat: 1, maxMeat: 2,
                materialDrop: PlayerInventory.WolfTooth, materialCount: 1,
                rareDrop: PlayerInventory.WolfFur, rareDropChance: 1.0f);
            var animal = go.GetComponent<AnimalAI>();

            animal.TryAutoHunt(out var drops);

            Assert.IsTrue(drops.Exists(d => d.item.id == "mat_wolf_fur"), "늑대 모피 드랍 (100% 확률)");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AnimalAI_TryAutoHunt_RareDropZeroChance_NotDropped()
        {
            // Zero chance for rare drop
            var go = CreateAnimal("rabbit", alive: true,
                meatDrop: PlayerInventory.RabbitMeat, minMeat: 1, maxMeat: 2,
                materialDrop: PlayerInventory.RabbitFur, materialCount: 1,
                rareDrop: PlayerInventory.BoarTusk, rareDropChance: 0f);
            var animal = go.GetComponent<AnimalAI>();

            animal.TryAutoHunt(out var drops);

            Assert.IsFalse(drops.Exists(d => d.item.id == "mat_boar_tusk"), "0% 확률 드랍은 없어야 함");

            Object.DestroyImmediate(go);
        }

        // ===================== HuntingMission.ExecuteHunting 테스트 =====================

        [Test]
        public void ExecuteHunting_NoHunters_ReturnsFailure()
        {
            var results = HuntingMission.ExecuteHunting();

            Assert.IsNotEmpty(results, "결과 리스트는 비어있지 않아야 함");
            Assert.IsFalse(results[0].success, "사냥꾼이 없으면 실패");
            StringAssert.Contains("사냥꾼", results[0].message, "사냥꾼 없음 메시지");
        }

        [Test]
        public void ExecuteHunting_HunterWithAnimal_HuntsSuccess()
        {
            var invGo = CreatePlayerInventory();
            var guardGo = CreateGuard(GuardRole.Hunter, 1, true);
            var animalGo = CreateAnimal("rabbit", alive: true,
                meatDrop: PlayerInventory.RabbitMeat, minMeat: 2, maxMeat: 2,
                materialDrop: PlayerInventory.RabbitFur, materialCount: 1);

            // Position them close together
            guardGo.transform.position = Vector3.zero;
            animalGo.transform.position = Vector3.one; // ~1.7m apart, well within 20m

            var results = HuntingMission.ExecuteHunting();

            Assert.IsNotEmpty(results, "결과 존재");
            bool anySuccess = false;
            foreach (var r in results)
            {
                if (r.success)
                {
                    anySuccess = true;
                    Assert.Greater(r.itemsGathered, 0, "획득 아이템 수 > 0");
                    Assert.AreEqual("rabbit", r.monsterName, "토끼 사냥");
                    Assert.IsNotEmpty(r.hunterName, "사냥꾼 이름 있음");
                }
            }
            Assert.IsTrue(anySuccess, "하나 이상 성공한 사냥 결과가 있어야 함");

            Object.DestroyImmediate(animalGo);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void ExecuteHunting_HunterNoAnimals_ReturnsNoAnimals()
        {
            var guardGo = CreateGuard(GuardRole.Hunter, 1, true);
            guardGo.transform.position = Vector3.zero;

            // No animals in scene
            var results = HuntingMission.ExecuteHunting();

            Assert.IsNotEmpty(results, "결과 존재");
            foreach (var r in results)
            {
                Assert.IsFalse(r.success, "몬스터 없으면 실패");
                StringAssert.Contains("몬스터 없음", r.message, "몬스터 없음 메시지");
            }

            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteHunting_DeadHunter_NotIncluded()
        {
            // Create dead hunter — should not be found
            var guardGo = CreateGuard(GuardRole.Hunter, 1, true, alive: false);
            var animalGo = CreateAnimal("rabbit", alive: true);

            var active = HuntingMission.GetActiveHunters();
            Assert.IsEmpty(active, "죽은 사냥꾼은 포함되지 않아야 함");

            Object.DestroyImmediate(animalGo);
            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteHunting_UnrecruitedHunter_NotIncluded()
        {
            var guardGo = CreateGuard(GuardRole.Hunter, 1, recruited: false);
            var animalGo = CreateAnimal("rabbit", alive: true);

            var active = HuntingMission.GetActiveHunters();
            Assert.IsEmpty(active, "포섭되지 않은 사냥꾼은 포함되지 않아야 함");

            Object.DestroyImmediate(animalGo);
            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void ExecuteHunting_MultipleHunters_AllHunt()
        {
            var invGo = CreatePlayerInventory();
            var guardGo1 = CreateGuard(GuardRole.Hunter, 1, true);
            var guardGo2 = CreateGuard(GuardRole.Hunter, 2, true);
            var animalGo1 = CreateAnimal("rabbit", alive: true,
                meatDrop: PlayerInventory.RabbitMeat, minMeat: 1, maxMeat: 1);
            var animalGo2 = CreateAnimal("boar", alive: true,
                meatDrop: PlayerInventory.BoarMeat, minMeat: 1, maxMeat: 1);

            guardGo1.transform.position = Vector3.zero;
            guardGo2.transform.position = new Vector3(5f, 0, 0);
            animalGo1.transform.position = new Vector3(1f, 0, 0);
            animalGo2.transform.position = new Vector3(6f, 0, 0);

            var results = HuntingMission.ExecuteHunting();

            int successCount = 0;
            foreach (var r in results)
            {
                if (r.success) successCount++;
            }
            Assert.AreEqual(2, successCount, "두 사냥꾼 모두 사냥 성공");

            Object.DestroyImmediate(animalGo1);
            Object.DestroyImmediate(animalGo2);
            Object.DestroyImmediate(guardGo1);
            Object.DestroyImmediate(guardGo2);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void ExecuteHunting_BonusApplied_BasedOnRole()
        {
            var invGo = CreatePlayerInventory();
            var guardGo = CreateGuard(GuardRole.Hunter, 1, true);
            var animalGo = CreateAnimal("rabbit", alive: true,
                meatDrop: PlayerInventory.RabbitMeat, minMeat: 2, maxMeat: 2,  // exact yield 2
                materialDrop: PlayerInventory.RabbitFur, materialCount: 1);   // material count 1

            guardGo.transform.position = Vector3.zero;
            animalGo.transform.position = Vector3.one;

            var results = HuntingMission.ExecuteHunting();

            foreach (var r in results)
            {
                if (r.success)
                {
                    // Base yield = 2 (meat) + 1 (fur) = 3, Hunter bonus = 1.5x
                    // Mathf.RoundToInt(2 * 1.5) = 3, Mathf.RoundToInt(1 * 1.5) = 2
                    // Total = 3 + 2 = 5
                    Assert.AreEqual(5, r.itemsGathered, "1.5배 보너스 적용: 3 → 5");
                    Assert.AreEqual("rabbit", r.monsterName);
                }
            }

            Object.DestroyImmediate(animalGo);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(invGo);
        }

        [Test]
        public void ExecuteHunting_HunterOutOfRange_ReturnsTooFar()
        {
            var invGo = CreatePlayerInventory();
            var guardGo = CreateGuard(GuardRole.Hunter, 1, true);
            var animalGo = CreateAnimal("rabbit", alive: true,
                meatDrop: PlayerInventory.RabbitMeat, minMeat: 1, maxMeat: 1);

            // Position far apart (well beyond range)
            guardGo.transform.position = Vector3.zero;
            animalGo.transform.position = new Vector3(100f, 0, 0);

            var results = HuntingMission.ExecuteHunting();

            bool anyTooFar = false;
            foreach (var r in results)
            {
                if (!r.success && r.message.Contains("너무 멀리"))
                {
                    anyTooFar = true;
                    break;
                }
            }
            Assert.IsTrue(anyTooFar, "너무 멀리 있음 메시지가 있어야 함");

            Object.DestroyImmediate(animalGo);
            Object.DestroyImmediate(guardGo);
            Object.DestroyImmediate(invGo);
        }

        // ===================== GetActiveHunters 테스트 =====================

        [Test]
        public void GetActiveHunters_ReturnsOnlyAliveRecruitedHunters()
        {
            var g1 = CreateGuard(GuardRole.Hunter, 1, true);     // should be included
            var g2 = CreateGuard(GuardRole.Hunter, 1, false);    // not recruited
            var g3 = CreateGuard(GuardRole.Soldier, 1, true);    // wrong role
            var g4 = CreateGuard(GuardRole.Hunter, 1, true, alive: false); // dead
            var g5 = CreateGuard(GuardRole.Hunter, 2, true);     // should be included

            var active = HuntingMission.GetActiveHunters();

            Assert.AreEqual(2, active.Count, "살아있고 포섭된 Hunter만 포함");

            Object.DestroyImmediate(g1);
            Object.DestroyImmediate(g2);
            Object.DestroyImmediate(g3);
            Object.DestroyImmediate(g4);
            Object.DestroyImmediate(g5);
        }

        [Test]
        public void GetActiveHunters_MaxHunters_StopsAtLimit()
        {
            var guards = new List<GameObject>();
            for (int i = 0; i < HuntingMission.MAX_HUNTERS + 5; i++)
            {
                var g = CreateGuard(GuardRole.Hunter, 1, true);
                g.transform.position = new Vector3(i * 10f, 0, 0);
                guards.Add(g);
            }

            var active = HuntingMission.GetActiveHunters();

            Assert.LessOrEqual(active.Count, HuntingMission.MAX_HUNTERS, "MAX_HUNTERS 제한");

            foreach (var g in guards)
                Object.DestroyImmediate(g);
        }

        [Test]
        public void GetActiveHunters_SoldierNotHunter_NotIncluded()
        {
            var g1 = CreateGuard(GuardRole.Soldier, 1, true);
            var g2 = CreateGuard(GuardRole.Hunter, 1, true);

            var active = HuntingMission.GetActiveHunters();

            Assert.AreEqual(1, active.Count, "Soldier는 포함되지 않아야 함");
            Assert.AreEqual(GuardRole.Hunter, active[0].Role, "Hunter만 포함");

            Object.DestroyImmediate(g1);
            Object.DestroyImmediate(g2);
        }
    }
}