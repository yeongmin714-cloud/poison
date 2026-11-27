using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [전리품설명] 전리품 창 슬롯 좌클릭 → 중앙 아이템 설명창 표출 계약(사용자 지시 2026-11-07).
    /// 소스 계약(문자열 어서션) 원칙 — InteriorInteractionTests 선례.
    /// </summary>
    public class LootDescriptionContractTests
    {
        [Test]
        public void LootSlot_LeftClick_ShowsDescriptionWindow()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/LootWindowUTK.cs");
            Assert.That(src, Does.Contain("() => ShowLootDescription(itemIndex, entry)"),
                "[전리품설명 C1] 슬롯 좌클릭 콜백이 ShowLootDescription로 연결돼야 한다.");
            Assert.That(src, Does.Contain("ItemDescriptionWindowUTK.ShowItem(entry.Item, entry.Count)"),
                "[전리품설명 C1] 좌클릭 → 중앙 설명창 ShowItem 갱신(인벤 OnSlotClick 선례 계약).");
        }

        [Test]
        public void LootWindow_CloseAndTake_ClearDescription()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/LootWindowUTK.cs");
            Assert.That(src, Does.Contain("ItemDescriptionWindowUTK.Clear();   // [전리품설명 C2] 창 닫힘"),
                "[전리품설명 C2] 전리품 창 닫힘 시 설명창 동기 해제.");
            Assert.That(src, Does.Contain("ItemDescriptionWindowUTK.Clear();   // [전리품설명 C2] 획득"),
                "[전리품설명 C2] 획득 완료 시 설명창 스테일 방지.");
        }

        [Test]
        public void DetailWindow_ShowItem_OpensWhenClosed()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/ItemDescriptionWindowUTK.cs");
            Assert.That(src, Does.Contain("if (!i.IsOpen) Open();"),
                "ShowItem은 닫힘 상태에서 창을 개방해야 한다(좌클릭 즉시 표출 계약).");
        }

        [Test]
        public void UtkWireUp_LootRoute_ContractUpdated()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKWireUp.cs");
            Assert.That(src, Does.Contain("전리품설명 C3"),
                "[전리품설명 C3] 전리품 라우트 상세창 개방 금지 구계약이 좌클릭 개방 허용으로 갱신됐다.");
            Assert.That(src, Does.Not.Contain("전리품 라우트에서는 개방하지 않"),
                "구 계약 문구(개방 금지) 잔존 금지.");
        }
    }
}
