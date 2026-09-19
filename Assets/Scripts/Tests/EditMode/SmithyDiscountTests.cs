using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// O8 Batch B (C-O8-03): 대장간 수리비 할인 접합 테스트.
    ///  - HasAnyCompletedStructure: 영지 무관 완료 구조물 전역 판정(빈 매니저 false / smithy 완료 true / 타 effect false).
    ///  - GetRepairCost: 대장간(smithy) 완료 시 30% 할인(올림), 타 effectId(stable)만 있으면 원가 유지,
    ///    ItemSlot 오버로드도 위임 경유로 동일 할인.
    ///
    /// [EditMode 주의] Awake 미실행 환경이라 ConstructionManager.Instance가 자동 주입되지 않는다 →
    /// 리플렉션으로 주입/원복. TearDown에서 반드시 null 원복하여 RepairLedgerTests 등
    /// 기존 수리비 테스트(무할인 기대)의 오염을 방지한다.
    /// 계수 표 기존 검증은 RepairLedgerTests가 소유 — 본 파일은 할인 접합만 검증(중복 없음).
    /// </summary>
    public class SmithyDiscountTests
    {
        private ConstructionManager _cm;

        private const System.Reflection.BindingFlags InstanceFlags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;

        [SetUp]
        public void SetUp()
        {
            SetInstance(null);   // 혹시 남아있을 인스턴스 정리
            _cm = null;
        }

        [TearDown]
        public void TearDown()
        {
            // [필수] Instance 원복 — 기존 수리비 테스트(RepairLedgerTests, 무할인 기대) 오염 방지
            SetInstance(null);
            if (_cm != null) Object.DestroyImmediate(_cm.gameObject);
            _cm = null;
        }

        private static void SetInstance(ConstructionManager value)
        {
            typeof(ConstructionManager).GetProperty("Instance", InstanceFlags)
                ?.SetValue(null, value);   // static 프로퍼티 — obj는 null, value가 새 값
        }

        /// <summary>대장간 완료 1건 수제 세이브 엔트리 적용.</summary>
        private void ApplySmithySaveEntry()
        {
            var entries = new List<ConstructionSaveEntry>
            {
                new ConstructionSaveEntry
                {
                    structureId = "smithy_01", blueprintId = "bp_smithy",
                    territoryId = "East_01", posX = 3f, posY = 0f, posZ = 3f,
                    progress = 1f, isComplete = true,
                },
            };
            _cm.ApplySaveEntries(entries);
        }

        // ===================== HasAnyCompletedStructure =====================

        [Test]
        public void HasAnyCompletedStructure_EmptyManager_ReturnsFalse()
        {
            _cm = new GameObject("CM_smithy_empty").AddComponent<ConstructionManager>();

            Assert.IsFalse(_cm.HasAnyCompletedStructure("smithy"));
        }

        [Test]
        public void HasAnyCompletedStructure_SmithyCompletedViaSave_True()
        {
            _cm = new GameObject("CM_smithy_done").AddComponent<ConstructionManager>();
            ApplySmithySaveEntry();

            Assert.IsTrue(_cm.HasAnyCompletedStructure("smithy"),
                "대장간 완료 1건 — 영지 무관 전역 판정 true");
        }

        [Test]
        public void HasAnyCompletedStructure_OtherEffect_False()
        {
            _cm = new GameObject("CM_smithy_other").AddComponent<ConstructionManager>();
            ApplySmithySaveEntry();

            Assert.IsFalse(_cm.HasAnyCompletedStructure("stable"),
                "대장간만 완료 — stable 조회는 false");
        }

        // ===================== GetRepairCost 대장간 할인 =====================

        [Test]
        public void GetRepairCost_SmithyCompleted_CostIsCeilOf70Percent()
        {
            // ① 대장간 없음 — 원가 A (Common 2g × 손실 50 = 100G, RepairLedgerTests 기대와 동일)
            SetInstance(null);
            int costA = EquipmentRepairSystem.GetRepairCost(50, 100, "Common");
            Assert.AreEqual(100, costA);

            // ② 대장간 완료 — B == ceil(A × 0.7)
            _cm = new GameObject("CM_discount").AddComponent<ConstructionManager>();
            SetInstance(_cm);
            ApplySmithySaveEntry();

            int costB = EquipmentRepairSystem.GetRepairCost(50, 100, "Common");
            Assert.AreEqual(Mathf.CeilToInt(costA * 0.7f), costB, "동일 입력 — B == ceil(A × 0.7)");
            Assert.AreEqual(70, costB);
        }

        [Test]
        public void GetRepairCost_OtherEffectOnly_NoDiscount()
        {
            _cm = new GameObject("CM_stable_only").AddComponent<ConstructionManager>();
            SetInstance(_cm);
            var entries = new List<ConstructionSaveEntry>
            {
                new ConstructionSaveEntry
                {
                    structureId = "stable_01", blueprintId = "bp_stable",
                    territoryId = "East_01", posX = 3f, posY = 0f, posZ = 3f,
                    progress = 1f, isComplete = true,
                },
            };
            _cm.ApplySaveEntries(entries);

            // stable 완료뿐 — 대장간 할인 미적용(원가 유지)
            Assert.AreEqual(100, EquipmentRepairSystem.GetRepairCost(50, 100, "Common"),
                "타 effectId만 있으면 원가 그대로");
        }

        [Test]
        public void GetRepairCost_SlotOverload_AlsoDiscounted()
        {
            _cm = new GameObject("CM_slot").AddComponent<ConstructionManager>();
            SetInstance(_cm);
            ApplySmithySaveEntry();

            var slot = new PlayerInventory.ItemSlot
            {
                item = new PlayerInventory.ItemData
                {
                    id = "sword_01",
                    maxDurability = 100,
                    rarity = ItemRarity.Common,
                },
                currentDurability = 50,
            };

            // 오버로드는 위임 경유(GetRepairCost(int,int,string))라 할인이 자동 적용되어야 함
            Assert.AreEqual(70, EquipmentRepairSystem.GetRepairCost(slot),
                "ItemSlot 오버로드도 대장간 30% 할인 적용");
        }
    }
}
