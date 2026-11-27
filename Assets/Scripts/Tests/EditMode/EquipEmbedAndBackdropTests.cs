using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [66차] 드래그 백드롭 양보 + 장비 임베드 카드형 계약 (소스 문자열 어서션 — HudBottomAnchorTests 선례).
    /// Play 실측 결함: UIRoot 백드롭(WorldDropTarget)이 후등록 우선 체인에서 핫바 슬롯 드롭을
    /// 가로채 아이템이 땅에 버려짐(targets=[WorldDropTarget,HotbarSlotDropTarget] → World 소비).
    /// </summary>
    public class EquipEmbedAndBackdropTests
    {
        [Test]
        public void UTKDragDrop_HasBackdropMarkerAndYieldFilter()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/UTKDragDrop.cs");
            Assert.That(src, Does.Contain("interface IUTKBackdropDropTarget"),
                "백드롭 마커 인터페이스가 정의되어야 한다.");
            Assert.That(src, Does.Contain("is IUTKBackdropDropTarget"),
                "Complete()가 구체 타겟 존재 시 백드롭을 제외해야 한다(핫바 드롭 가로채기 차단).");
        }

        [Test]
        public void WorldDropTarget_IsBackdrop()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs");
            Assert.That(src, Does.Contain("WorldDropTarget : IUTKBackdropDropTarget"),
                "월드 버림 타겟은 백드롭으로 선언되어야 한다.");
        }

        [Test]
        public void InventoryRefresh_OnEquipmentChanged_ForcesGridRefresh()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs");
            Assert.That(src, Does.Contain("RefreshGrid(force: true)"),
                "장비 변경 이벤트 경로는 PointerOverUI/드래그 가드를 우회해 즉시 갱신해야 한다.");
        }

        [Test]
        public void EquipEmbed_HasFigmaCardsWithWeaponLoadoutSlots()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/InventoryWindowUTK.cs");
            Assert.That(src, Does.Contain("\"Loadout\" + def.loadoutIndex"),
                "무기 로드아웃 슬롯 1·2가 임베드 장비 패널에 등록되어야 한다(숫자 배지 1/2).");
            Assert.That(src, Does.Contain("EquipBadge_"),
                "EQ 배지(피그마 15:33)가 카드에 생성되어야 한다.");
            Assert.That(src, Does.Contain("WeaponNumber"),
                "무기 슬롯 핫키 배지(피그마 221:8)가 카드에 생성되어야 한다.");
            Assert.That(src, Does.Contain("ToggleWeaponLoadout"),
                "무기 슬롯 우클릭 장착 토글 경로가 있어야 한다.");
        }
    }
}
