using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [193차] 폭탄 대미지·소비 계약 — 소스 문자열 어서션(FxCleanup193Tests 관례).
    /// 근거: 사용자 지시 "폭탄도 몬스터에게 대미지가 안들어가" / "폭탄은 소비품이니 사용하면 갯수가 줄었으면 좋겠고".
    /// 근본 원인 3건: ①GetComponent 직접 탐색(자식 히트박스 미스) ②하드코딩 10f ③재무장 개수 검증 부재.
    /// </summary>
    public class BombDamageConsume193Tests
    {
        [Test]
        public void Bomb_Uses_GetComponentInParent_For_All_Damage_Types()
        {
            string src = System.IO.File.ReadAllText(
                "Assets/Scripts/Systems/Bomb.cs");

            // [193차] 루트 탐색 — 근접(AttackSystem)과 동일 단일소스. 자식 히트박스에서도 해석.
            int inParentCount = System.Text.RegularExpressions.Regex
                .Matches(src, "GetComponentInParent<IDamageable>").Count;
            Assert.That(inParentCount, Is.GreaterThanOrEqualTo(2),
                "[193] 폭탄 전 피해 경로(폭발+독가스)에서 GetComponentInParent 사용 — GetComponent 직접 탐색 폐지");

            // 레거시 직접 탐색 잔존 0건
            Assert.That(src, Does.Not.Contain("GetComponent<IDamageable>"),
                "[193] 폭탄 전 피해 경로에서 GetComponent<IDamageable> 직접 탐색 잔존 금지");
        }

        [Test]
        public void Bomb_Damage_Is_Stat_Based_Not_Hardcoded()
        {
            string src = System.IO.File.ReadAllText(
                "Assets/Scripts/Systems/Bomb.cs");

            // [193차] 스탯 기반 피해량 — 하드코딩 10f 폐지
            Assert.That(src, Does.Contain("public float bombDamage"),
                "[193] Bomb.bombDamage 필드 존재 (폴백 25f)");
            Assert.That(src, Does.Contain("bombDamage * (1f - distance / explosionRadius)"),
                "[193] 폭발 피해량 = bombDamage × 거리감쇠 — 하드코딩 폐지");
            Assert.That(src, Does.Not.Contain("10f * (1f - distance"),
                "[193] 예시 데미지 10f 하드코딩 잔존 금지");
        }

        [Test]
        public void ItemData_Carries_BombDamage_Stat()
        {
            string src = System.IO.File.ReadAllText(
                "Assets/Scripts/Core/PlayerInventory.cs");

            // [193차] ItemData.bombDamage — 아이템 스탯 기반 피해량
            Assert.That(src, Does.Contain("public float bombDamage = 25f"),
                "[193] ItemData.bombDamage 필드 존재 (아이템 스탯 기반)");
        }

        [Test]
        public void BombArmController_Gates_Arm_On_Zero_Count()
        {
            string src = System.IO.File.ReadAllText(
                "Assets/Scripts/Systems/BombArmController.cs");

            // [193차] 무장 게이트 — 0개 무장 거부 + 피드백(상호작용불가=피드백필수 계약)
            Assert.That(src, Does.Contain("GetItemCount(item.id) <= 0"),
                "[193] Arm에서 개수 게이트 검증");
            Assert.That(src, Does.Not.Contain("PlayerNotify.Notify"),
                "[205차] 갱신 — 폭탄 0개 알림 제거(사용자 지시: \"폭탄 부족시 따로 알림 안떠도 괜찮고\") — 조용한 거부(구 193 피드백 계약 완화)");
            Assert.That(src, Does.Contain("bomb.bombDamage ="),
                "[193] SpawnThrownBomb에서 ItemData.bombDamage 주입");
        }

        [Test]
        public void Inventory_Has_Change_Event_For_Immediate_Ui_Refresh()
        {
            string src = System.IO.File.ReadAllText(
                "Assets/Scripts/Core/PlayerInventory.cs");
            string ui = System.IO.File.ReadAllText(
                "Assets/Scripts/UI/Toolkit/HotbarUIUTK.cs");

            // [193차] 수량 즉시 갱신 — RemoveItem 이벤트 발화 + UI 구독
            Assert.That(src, Does.Contain("public event System.Action OnInventoryChanged"),
                "[193] PlayerInventory.OnInventoryChanged 이벤트 존재");
            Assert.That(src, Does.Contain("OnInventoryChanged?.Invoke()"),
                "[193] RemoveItem에서 이벤트 발화");
            Assert.That(ui, Does.Contain("OnInventoryChanged += bar.RefreshAllIcons"),
                "[193] HotbarUIUTK가 이벤트 구독 — 투척 즉시 개수 갱신");
        }
    }
}
