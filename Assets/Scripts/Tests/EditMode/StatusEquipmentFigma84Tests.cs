using System.Reflection;
using NUnit.Framework;
using ProjectName.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.Tests.EditMode
{
    public class StatusEquipmentFigma84Tests
    {
        private StatusWindowUTK CreateWindow()
        {
            ConstructorInfo constructor = typeof(StatusWindowUTK).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
            Assert.That(constructor, Is.Not.Null, "StatusWindowUTK must retain its private owner constructor.");
            return (StatusWindowUTK)constructor.Invoke(null);
        }

        [Test]
        public void Figma84Bounds_AreCanvasLocalAfterSubtractingFrameOrigin()
        {
            // Figma frame 84:4 origin=(29618,3); panel absolute bounds are normalized to that origin.
            AssertRect(new Rect(391.8f, 72f, 576f, 936f), StatusWindowUTK.CharacterStatusCanvasBounds);
            AssertRect(new Rect(996.6f, 72f, 531.6f, 936f), StatusWindowUTK.EquipmentCanvasBounds);
            AssertRect(new Rect(391.8f, 72f, 1136.4f, 936f), StatusWindowUTK.CompositionCanvasBounds);
        }

        [Test]
        public void RuntimeComposition_HasTwoCanvasPanelSiblings_AndKeepsStatusBelowFoldContent()
        {
            StatusWindowUTK host = CreateWindow();
            try
            {
                VisualElement content = host.Q<VisualElement>("Content");
                VisualElement status = host.Q<VisualElement>("CharacterStatusPanel");
                VisualElement equipment = host.Q<VisualElement>("EquipmentPanel");
                Assert.That(content, Is.Not.Null);
                Assert.That(status, Is.Not.Null);
                Assert.That(equipment, Is.Not.Null);
                Assert.That(status.parent, Is.SameAs(content));
                Assert.That(equipment.parent, Is.SameAs(content));
                Assert.That(content.childCount, Is.EqualTo(2), "Only the two Figma panel roots are visible siblings.");
                Assert.That(host.IsFrameless, Is.True);
                Assert.That(host.style.backgroundColor.value.a, Is.EqualTo(0f).Within(0.001f));

                Assert.That(status.Q<ScrollView>("StatusScrollView"), Is.Not.Null);
                Assert.That(status.Q<VisualElement>("LegacySupplementaryDetails"), Is.Not.Null);
                Assert.That(status.Q<Label>("CoreStat_Attack"), Is.Not.Null);
                Assert.That(status.Q<Label>("CoreStat_Defense"), Is.Not.Null);
                Assert.That(status.Q<Label>("CoreStat_MaxHealth"), Is.Not.Null);
                Assert.That(status.Q<Label>("CoreStat_Agility"), Is.Not.Null);
                Assert.That(status.Q<Button>("StatusCloseButton"), Is.Not.Null);
                Assert.That(status.Q<Button>("Allocate_Str"), Is.Not.Null);
                Assert.That(status.Q<Button>("TitleCycleButton"), Is.Not.Null);
                Assert.That(status.Q<Button>("AuditReportButton"), Is.Not.Null);

                for (int i = 0; i < 8; i++)
                    Assert.That(equipment.Q<VisualElement>("EquipSlot_" + new[] { "Helmet", "Armor", "Weapon", "Shoes", "Gloves", "Back", "Mask", "Bag" }[i]), Is.Not.Null);
                Assert.That(equipment.Q<VisualElement>("InventoryPackGrid").childCount, Is.EqualTo(25),
                    "Pack presents exactly 25 fixed visual cells, independently of inventory contents.");
                Assert.That(equipment.Q<ScrollView>("EquipmentScrollView"), Is.Not.Null);
            }
            finally
            {
                host.RemoveFromHierarchy();
            }
        }

        [Test]
        public void ToggleRoute_UsesPKey_AndLifecycleStillExposesEscapeClose()
        {
            MethodInfo bootstrap = typeof(StatusWindowUTK).GetMethod("Bootstrap", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo update = typeof(StatusWindowUTK).GetNestedType("Updater", BindingFlags.NonPublic)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bootstrap, Is.Not.Null, "The post-scene status bootstrap must remain in place.");
            Assert.That(update, Is.Not.Null, "P-key and ESC routing must remain in the updater lifecycle.");
            string updateBody = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/StatusWindowUTK.cs");
            StringAssert.Contains("kb.pKey.wasPressedThisFrame", updateBody);
            StringAssert.Contains("kb.escapeKey.wasPressedThisFrame", updateBody);
            Assert.That(typeof(UTKWindowBase).GetMethod("Close", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null,
                "ESC and the panel close button must retain the shared close lifecycle.");
        }

        /// <summary>
        /// [144차 P0/P1-EQ] 장비구획(84:55) 정합 수리 계약 — EP 코너 데칼 4개(B7), 전술 배낭 25셀 UTKSlot
        /// TierStrip(B37)·Count(B38), EQ 배지 박스 8슬롯(B22·B23), 장비 셀 아이콘 호스트(B27).
        /// </summary>
        [Test]
        public void EquipmentPanel_Decals_PackTierStrips_BadgeBoxes_AreFigmaTruth()
        {
            StatusWindowUTK host = CreateWindow();
            try
            {
                VisualElement equipment = host.Q<VisualElement>("EquipmentPanel");
                Assert.That(equipment, Is.Not.Null);

                // [144차 P0-① B7] EP 코너 데칼 4개(84:56~67) — EP 패널 자식
                foreach (var decal in new[] { "DecalTL", "DecalTR", "DecalBL", "DecalBR" })
                    Assert.That(equipment.Q<VisualElement>(decal), Is.Not.Null, $"EP corner decal {decal} must exist.");

                // [144차 P0-② B37] 전술 배낭 25셀 전부 — TierStrip 요소 상시 존재 + Count 라벨 내장(B38)
                VisualElement packGrid = equipment.Q<VisualElement>("InventoryPackGrid");
                Assert.That(packGrid.childCount, Is.EqualTo(25));
                foreach (VisualElement cell in packGrid.Children())
                {
                    Assert.That(cell.Q<VisualElement>("TierStrip"), Is.Not.Null, "Pack cell must embed a TierStrip element (93:92).");
                    Assert.That(cell.Q<Label>("Count"), Is.Not.Null, "Pack cell must embed a Count label (93:93).");
                }

                // [144차 P1-EQ B23] EQ 배지 박스 — 반지·목걸이 제외 8슬롯
                var badgeSlots = new[] { "Helmet", "Armor", "Weapon", "Shoes", "Gloves", "Back", "Mask", "Bag" };
                foreach (var slot in badgeSlots)
                    Assert.That(equipment.Q<VisualElement>("EquipBadge_" + slot), Is.Not.Null, $"EQ badge box missing on {slot}.");
                Assert.That(equipment.Q<VisualElement>("EquipBadge_Ring"), Is.Null, "Ring must not carry an EQ badge (Figma 8 slots).");
                Assert.That(equipment.Q<VisualElement>("EquipBadge_Necklace"), Is.Null, "Necklace must not carry an EQ badge (Figma 8 slots).");

                // [144차 P1-EQ B27] 장비 셀 아이콘 호스트 — 10슬롯 전부(아이템명 라벨 폐지, 툴팁 대체)
                foreach (var slot in new[] { "Helmet", "Armor", "Weapon", "Shoes", "Gloves", "Back", "Mask", "Bag", "Ring", "Necklace" })
                    Assert.That(equipment.Q<VisualElement>("EquippedIcon_" + slot), Is.Not.Null, $"Equipped icon host missing on {slot}.");
            }
            finally
            {
                host.RemoveFromHierarchy();
            }
        }

        /// <summary>[144차 P1-EQ] 장비구획 수치 진실치 소스 계약 — equipment.md B행 (알파 재검증 정정 반영).</summary>
        [Test]
        public void EquipmentPanel_NumericTruths_SourceContract()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/StatusWindowUTK.cs");
            // B11 — SYS_EQ_STABLE #56E39F(84:73)
            StringAssert.Contains("GitHubDark.SysStable", src);
            StringAssert.Contains("0x56E39F", src);
            // B12 — EquippedSection marginTop 19.2(84:68 gap)
            StringAssert.Contains("equipped.style.marginTop = 19.2f;", src);
            // B29 — EquippedSection 높이 진실치 고정 230.4(93:4)
            StringAssert.Contains("equipped.style.height = 230.4f;", src);
            // B13 — 섹션 카드 저알파 화이트 글래스(93:4 @0.02 / 93:88 @0.012)
            StringAssert.Contains("new Color(1f, 1f, 1f, 0.02f)", src);
            StringAssert.Contains("new Color(1f, 1f, 1f, 0.012f)", src);
            // B15/B33 — 섹션 제목 19.2 · B34 — 부제 12
            StringAssert.Contains("MkLabel(\"장착 중인 장비\", 19.2f,", src);
            StringAssert.Contains("MkLabel(\"전술 배낭\", 19.2f,", src);
            StringAssert.Contains("MkLabel(\"TACTICAL PACK\", 12f,", src);
            // B16 — 그리드 섹션 내 중앙정렬 · B17 — 행갭 9.6(행 피치 91.2)
            StringAssert.Contains("grid.style.alignSelf = Align.Center;", src);
            StringAssert.Contains("cell.style.marginBottom = row == 1 ? 0f : 9.6f;", src);
            // B20/B36 — 셀 bg #21262D@0.6 + 크롬 테두리 + r9.6
            StringAssert.Contains("0x21 / 255f, 0x26 / 255f, 0x2D / 255f, 0.6f", src);
            StringAssert.Contains("UTKTheme.ApplyElementBorder(slot);", src);
            // B22 — EQ 배지 박스 22.6×15.8 · B27/B39 — 아이콘 38.4 · B38 — Count 우하단(7.6/4.6)
            StringAssert.Contains("badge.style.width = 22.6f;", src);
            StringAssert.Contains("badge.style.height = 15.8f;", src);
            StringAssert.Contains("icon.style.width = 38.4f;", src);
            StringAssert.Contains("count.style.right = 7.6f;", src);
            StringAssert.Contains("count.style.bottom = 4.6f;", src);
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(actual.width, Is.EqualTo(expected.width).Within(0.01f));
            Assert.That(actual.height, Is.EqualTo(expected.height).Within(0.01f));
        }
    }
}

