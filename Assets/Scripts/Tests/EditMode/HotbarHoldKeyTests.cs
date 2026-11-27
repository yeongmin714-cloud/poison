using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [68차 P3] 핫바 숫자키 들기 계약 — 판정 순수 함수(ResolveHoldAction) + 무기 해석기(WeaponItemResolver)
    /// + 아이템 홀드 매니저(ItemHoldManager — EditMode 안전 경로) + 배선 소스 어서션.
    /// 키 시뮬은 EditMode에서 불가하므로 판정 로직을 순수 함수로 추출해 검증한다.
    /// </summary>
    public class HotbarHoldKeyTests
    {
        // ── 판정 순수 함수 ──

        [Test]
        public void Resolve_SquadMode_Skips()
        {
            // 부대 모드 선택은 GuardSquadHotbar.HandleSelectKeys 단일소스 — 핫바 홀드 경로 스킵.
            var a = HotbarUIUTK.ResolveHoldAction("steel_sword", isSquadMode: true, fHeld: false,
                sameSlotActive: false, isWeaponItem: true);
            Assert.That(a, Is.EqualTo(HotbarUIUTK.HotbarHoldAction.None));
        }

        [Test]
        public void Resolve_FHeld_Skips()
        {
            // F+숫자 = 부대 등록(HandleCtrlAssignKeys) 전용 — 홀드 경로 스킵.
            var a = HotbarUIUTK.ResolveHoldAction("steel_sword", isSquadMode: false, fHeld: true,
                sameSlotActive: false, isWeaponItem: true);
            Assert.That(a, Is.EqualTo(HotbarUIUTK.HotbarHoldAction.None));
        }

        [Test]
        public void Resolve_EmptySlot_Skips()
        {
            var a = HotbarUIUTK.ResolveHoldAction(null, isSquadMode: false, fHeld: false,
                sameSlotActive: false, isWeaponItem: false);
            Assert.That(a, Is.EqualTo(HotbarUIUTK.HotbarHoldAction.None));
        }

        [Test]
        public void Resolve_WeaponAndItem_AndToggleOff()
        {
            var w = HotbarUIUTK.ResolveHoldAction("steel_sword", false, false, false, true);
            var i = HotbarUIUTK.ResolveHoldAction("herb_blue", false, false, false, false);
            var t = HotbarUIUTK.ResolveHoldAction("steel_sword", false, false, true, true);
            Assert.That(w, Is.EqualTo(HotbarUIUTK.HotbarHoldAction.WeaponEquip),
                "무기 아이템 = WeaponEquipManager.Equip 경로.");
            Assert.That(i, Is.EqualTo(HotbarUIUTK.HotbarHoldAction.ItemHold),
                "비무기 아이템 = ItemHoldManager.Hold 경로.");
            Assert.That(t, Is.EqualTo(HotbarUIUTK.HotbarHoldAction.ToggleOff),
                "동일 활성 슬롯 재입력 = 해제 토글.");
        }

        // ── 무기 id 해석기 (InventoryWindowUTK private본과 동일 규약) ──

        [Test]
        public void Resolver_LegacyIds()
        {
            Assert.That(WeaponItemResolver.TryResolve("steel_sword", out var e1, out var t1), Is.True);
            Assert.That(e1, Is.EqualTo("steel"));
            Assert.That(t1, Is.EqualTo(WeaponType.Sword));

            Assert.That(WeaponItemResolver.TryResolve("crystal_bow", out var e2, out var t2), Is.True);
            Assert.That(e2, Is.EqualTo("crystal"));
            Assert.That(t2, Is.EqualTo(WeaponType.Bow));

            Assert.That(WeaponItemResolver.TryResolve("wood_spear", out var e3, out var t3), Is.True);
            Assert.That(e3, Is.EqualTo("wood"));
            Assert.That(t3, Is.EqualTo(WeaponType.Spear));
        }

        [Test]
        public void Resolver_ParticleForm_AndTierFallback()
        {
            Assert.That(WeaponItemResolver.TryResolve("weapon_iron_spear", out var e1, out var t1), Is.True);
            Assert.That(t1, Is.EqualTo(WeaponType.Spear));

            Assert.That(WeaponItemResolver.TryResolve("wood_sword", out var e2, out var t2), Is.True);
            Assert.That(e2, Is.EqualTo("wood"));
            Assert.That(t2, Is.EqualTo(WeaponType.Sword));
        }

        // ── ItemHoldManager — EditMode 안전 경로(no player/모델 부재 = no-op, 예외 금지 계약) ──

        [Test]
        public void Hold_EmptyKey_NoThrow_NoHold()
        {
            Assert.DoesNotThrow(() => ItemHoldManager.Hold(null, null));
            Assert.That(ItemHoldManager.IsHolding, Is.False, "빈 modelKey는 홀드 없음.");
        }

        [Test]
        public void Hold_NoPlayerInEditMode_GracefulNoOp()
        {
            // EditMode 씬에 Player 없음 — 경고 후 no-op(예외 아님).
            Assert.DoesNotThrow(() => ItemHoldManager.Hold("herb_blue", null));
            Assert.That(ItemHoldManager.IsHolding, Is.False);
        }

        [Test]
        public void Unhold_Idempotent_NoThrow()
        {
            Assert.DoesNotThrow(() => ItemHoldManager.Unhold());
            Assert.DoesNotThrow(() => ItemHoldManager.UnholdIfHolding());
            Assert.That(ItemHoldManager.IsHolding, Is.False);
        }

        // ── 배선 소스 어서션 (HudBottomAnchorTests 선례) ──

        [Test]
        public void PlayerMovement_FKey_RoutesSoldierToFigmaPanel()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerMovement.cs");
            Assert.That(src, Does.Contain("RaiseTargetInteraction("),
                "F키 병사 분기가 피그마 상호작용 패널(InteractionPanelUTK) 브리지로 라우팅되어야 한다.");
            Assert.That(src, Does.Contain("[68차 P1]"),
                "68차 F키 교체 마커가 있어야 한다.");
        }

        [Test]
        public void HotbarUIUTK_HoldKeys_WiredInUpdater()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/HotbarUIUTK.cs");
            Assert.That(src, Does.Contain("bar.HandleHoldNumberKeys();"),
                "Updater가 숫자키 홀드 폴링을 호출해야 한다.");
            Assert.That(src, Does.Contain("bar.UpdateHoldBorderFeedback();"),
                "홀드 중 슬롯 액센트 테두리 피드백이 갱신되어야 한다.");
            Assert.That(src, Does.Contain("ItemHoldManager.Hold("),
                "비무기 아이템은 ItemHoldManager로 들어야 한다.");
            Assert.That(src, Does.Contain("WeaponEquipManager.Equip("),
                "무기 아이템은 WeaponEquipManager로 장착해야 한다.");
        }

        [Test]
        public void ItemHoldManager_EquipInterlock_Present()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/WeaponEquipManager.cs");
            Assert.That(src, Does.Contain("ItemHoldManager.UnholdIfHolding();"),
                "무기 장착 시작 시 홀드 아이템 정리(상호 배타)가 있어야 한다.");
        }

        [Test]
        public void GblItemIconRenderer_ResolveModelKey_IsPublic()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/UI/GblItemIconRenderer.cs");
            Assert.That(src, Does.Contain("public static string ResolveModelKey"),
                "핫바 홀드 경로에서 모델키 해석 재사용 — public 계약.");
        }
    }
}
