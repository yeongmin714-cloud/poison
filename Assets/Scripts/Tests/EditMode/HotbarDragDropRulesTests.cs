using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [65차 P3] 인벤→핫바 드래그 경로 계약 — HotbarSlotDropTarget 수용 규칙 + UTKDragDrop 종료 이벤트.
    /// </summary>
    public class HotbarDragDropRulesTests
    {
        private static UTKDragPayload MakePayload(PlayerInventory.ItemCategory category)
        {
            var item = new PlayerInventory.ItemData
            {
                id = "test_" + category,
                displayName = "테스트 " + category,
                category = category,
            };
            return new UTKDragPayload { Source = UTKDragSourceKind.Inventory, Item = item };
        }

        [Test]
        public void Armor_IsRejected_AtCanDrop()
        {
            // [65차 P3 수리] 몸 장비는 AssignItem 가드와 일치하게 CanDrop에서 거부 —
            // 기존은 CanDrop true → Drop "성공"(고스트 소멸) 후 내부 거부 = 정직하지 않은 경로.
            var target = new HotbarSlotDropTarget(0);
            Assert.That(target.CanDrop(MakePayload(PlayerInventory.ItemCategory.Armor)), Is.False,
                "몸 장비(방어구)는 핫바 드롭 대상이 아니어야 한다 — CanDrop에서 거부.");
        }

        [Test]
        public void InventoryItem_IsAccepted_AtCanDrop()
        {
            var target = new HotbarSlotDropTarget(0);
            // 싱글턴 미존재(EditMode) → 부대 모드 게이트 통과, Armor 아님 → 수용
            Assert.That(HotbarUIUTK.Instance, Is.Null, "테스트 시작 시 핫바 싱글턴 없음.");
            Assert.That(target.CanDrop(MakePayload(PlayerInventory.ItemCategory.Potion)), Is.True,
                "인벤 소스 소모품은 핫바 드롭 수용.");
        }

        [Test]
        public void NonInventorySource_IsRejected()
        {
            var target = new HotbarSlotDropTarget(0);
            var payload = MakePayload(PlayerInventory.ItemCategory.Potion);
            payload.Source = UTKDragSourceKind.Loot;
            Assert.That(target.CanDrop(payload), Is.False,
                "인벤 소스가 아닌 페이로드(전리품 등)는 핫바 드롭 거부.");
        }

        [Test]
        public void Cancel_FiresDragEnded_ExactlyOnce()
        {
            int ended = 0;
            System.Action handler = () => ended++;
            UTKDragDrop.DragEnded += handler;
            try
            {
                var payload = MakePayload(PlayerInventory.ItemCategory.Potion);
                UTKDragDrop.Begin(payload);
                Assert.That(UTKDragDrop.Active, Is.True);

                UTKDragDrop.Cancel();
                Assert.That(UTKDragDrop.Active, Is.False);
                Assert.That(ended, Is.EqualTo(1), "첫 Cancel에서 DragEnded 1회 발화.");

                // 이미 종료 상태에서 재호출 — 중복 발화 없음(숫자패드 원복 1회 보장)
                UTKDragDrop.Cancel();
                Assert.That(ended, Is.EqualTo(1), "종료 후 재-Cancel은 DragEnded를 재발화하지 않는다.");
            }
            finally
            {
                UTKDragDrop.DragEnded -= handler;
                UTKDragDrop.Cancel();
            }
        }
    }
}
