using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [202차] F 상호작용 → 소속 병사 장비 착용 배선 계약 — P30-C EquipAllyItem(사장 코드)의 UI 활성화.
    ///  ①SoldierInteractUTK 장비 모드(버튼+ItemMode.Equipment+GiveItem 분기)
    ///  ②GuardPlaceholder 장비 선택 모드(SelectingEquipment+Begin+Weapon||Armor 필터)
    ///  ③EquipAllyItem 내부 계약 핀(아군 가드·6슬롯 분류·인벤 소비·이전 장비 반환·GVS 즉시 갱신)
    ///  ④Test_10 스폰 체인(SpawnSoldier→GuardPlaceholder→SpawnEquipment→BootIfMissing — 201차 배선의 테스트 씬 적용 증명)
    /// 실제 착용/렌더는 Play 영역 — EditMode는 소스계약만(UTKClickRippleTests 관례 동일).
    /// </summary>
    public class GuardEquipFlow202Tests
    {
        private const string UtkSrc = "Assets/Scripts/UI/Toolkit/SoldierInteractUTK.cs";
        private const string GpSrc = "Assets/Scripts/Systems/GuardPlaceholder.cs";
        private const string SetupSrc = "Assets/Scripts/Systems/TestTerritoryCombatSetup.cs";
        private const string SpawnerSrc = "Assets/Scripts/Systems/GuardEquipmentSpawner.cs";

        [Test]
        public void InteractWindow_EquipmentMode_ButtonAndGiveWired()
        {
            string s = System.IO.File.ReadAllText(UtkSrc);
            Assert.That(s, Does.Contain("AddMenuButton(\"장비주기\", () => ShowItemSection(ItemMode.Equipment)"),
                "아군 메뉴에 장비주기 버튼(물약/음식 옆 3행)");
            Assert.That(s, Does.Contain("public enum ItemMode { None, Food, Drug, Equipment }"),
                "ItemMode.Equipment 승격(패널 경유 정적 진입 호환)");
            Assert.That(s, Does.Contain("else _guard.BeginEquipmentSelection();   // [202차] 장비 모드"),
                "ShowItemSection → BeginEquipmentSelection 위임");
            Assert.That(s, Does.Contain("\"🛡️ 장비 선택\""), "장비 섹션 타이틀");
            Assert.That(s, Does.Contain("_guard.EquipAllyItem(item);"),
                "GiveItem 장비 분기 — P30-C EquipAllyItem UI 활성화(사장 코드 소멸)");
            // 기존 음식/약 경로 무손상
            Assert.That(s, Does.Contain("_guard.GiveAllyFood(item);"), "음식 경로 유지");
            Assert.That(s, Does.Contain("_guard.ApplyAllyPotion(item);"), "약 경로 유지");
            Assert.That(s, Does.Contain("_guard.GiveFood(item);"), "적병사 음식 경로 유지");
        }

        [Test]
        public void GuardPlaceholder_EquipmentSelectionMode_AndFilter()
        {
            string s = System.IO.File.ReadAllText(GpSrc);
            Assert.That(s, Does.Contain("None, SelectingFood, SelectingDrug, SelectingEquipment"),
                "SelectionMode 장비 확장");
            Assert.That(s, Does.Contain("public void BeginEquipmentSelection()"),
                "장비 선택 모드 진입(음식/약 진입 선례 동일)");
            Assert.That(s, Does.Contain("PlayerInventory.ItemCategory.Weapon\n                      || slot.item.category == PlayerInventory.ItemCategory.Armor"),
                "장비 모드 필터 = Weapon||Armor(무기 16종+방어구류 — shield=Armor 카테고리 포함)");
            Assert.That(s, Does.Contain("if (!IsAlly) { SetStatusMessage(\"적병사에게는 장비를 직접 등록할 수 없습니다.\"); return false; }"),
                "EquipAllyItem 아군 전용 가드(소속 판정 계약)");
        }

        [Test]
        public void EquipAllyItem_SlotSwapInventoryReturnAndVisualRefresh()
        {
            string s = System.IO.File.ReadAllText(GpSrc);
            Assert.That(s, Does.Contain("prev = WeaponItem; WeaponItem = item; slotLabel = \"무기\";"),
                "무기 슬롯 교체(WeaponItem — 시각+전리품 공용 소스)");
            Assert.That(s, Does.Contain("inv.RemoveItem(item.id);"), "인벤 소비");
            Assert.That(s, Does.Contain("if (prev != null) inv.AddItem(prev);"),
                "교체된 이전 장비 인벤 반환(GuardEquipmentSystem.ReturnToInventory 동일 규칙)");
            Assert.That(s, Does.Contain("GuardVisualAttachSystem.RequestRefreshFor(this);"),
                "즉시 GLB 재부착 트리거(P30 후속② — 폴링 대기 없이)");
            Assert.That(s, Does.Contain("EquipAllyArmorBySubType"), "Armor 6슬롯 서브 분류(투구/갑옷/신발/장갑/방패)");
        }

        [Test]
        public void Test10_SpawnChain_GuardsEquippedVia201Wiring()
        {
            string setup = System.IO.File.ReadAllText(SetupSrc);
            Assert.That(setup, Does.Contain("GuardPlaceholder"), "Test_10 병사 = GuardPlaceholder(201차 배선 적용 대상)");
            Assert.That(setup, Does.Contain("Test_10_TerritoryCombat"),
                "Test_10_TerritoryCombat 씬 전용 셋업(씬 가드 주석 핀)");

            string gp = System.IO.File.ReadAllText(GpSrc);
            Assert.That(gp, Does.Contain("GuardEquipmentSpawner.SpawnEquipment(gameObject, level);"),
                "GuardPlaceholder.Start → SpawnEquipment(스폰 시 착용 배선)");

            string spawner = System.IO.File.ReadAllText(SpawnerSrc);
            Assert.That(spawner, Does.Contain("GuardEquipmentSystem.BootIfMissing();"),
                "[201차] 부트 보장 — Test_10에서도 Instance 부재 시 자가 생성(무장비 스킵 소멸)");
            Assert.That(spawner, Does.Contain("GuardVisualAttachSystem.Instance == null"),
                "[201차] 부착 시스템 자가 생성 — Test_10 장비 GLB 부착 보장");
        }
    }
}
