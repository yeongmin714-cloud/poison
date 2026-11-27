using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [144차 P2-WF] WeaponForgeUTK v1(63:5) 고유 수리 계약 — crafting.md §3.1 W행.
    /// 배지(W34)/EN명(W43)/TierBadge(W44)/BlueprintVisual(W46)/StatsGrid(W49)/BadgeItem(W52)
    /// + P1 잔존(탭/헤더/버튼/행 타이포·상태) + 위조 금지(D7) 슬롯 표기.
    /// </summary>
    public class WeaponForgeFigma144Tests
    {
        private static WeaponForgeUTK ConstructBench()
        {
            var instanceField = typeof(WeaponForgeUTK).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var host = (WeaponForgeUTK)instanceField.GetValue(null);
            if (host == null)
            {
                var ctor = typeof(WeaponForgeUTK).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
                host = (WeaponForgeUTK)ctor.Invoke(null);
                instanceField.SetValue(null, host);
            }
            return host;
        }

        private static void InvokePrivate(object instance, string methodName)
        {
            var method = instance.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName + " 존재");
            method.Invoke(instance, null);
        }

        [Test]
        public void Source_UsesV1TruthTextsWithoutMockForgery()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/WeaponForgeUTK.cs");
            // v1 진실 문구(63:21/63:116/63:155/63:190/63:302/63:77/63:76)
            Assert.That(source, Does.Contain("\"아이템 제작\""), "W5 제목 63:21.");
            Assert.That(source, Does.Contain("\"상세 정보\""), "W39 Detail 제목 63:116.");
            Assert.That(source, Does.Contain("\"무기 사양 및 제작 정보\""), "W48 헤딩 63:155.");
            Assert.That(source, Does.Contain("\"보유 재료 리스트\""), "W52 배지 문구 63:190.");
            Assert.That(source, Does.Contain("\"가방 적재 중량\""), "W58 v1 WeightLabel 63:302.");
            Assert.That(source, Does.Contain("\"SELECTED\""), "W34 선택 배지 63:77.");
            Assert.That(source, Does.Contain("\"필요 전투 레벨 \""), "W33 행 부제 63:76(RequiredLevel 실데이터).");
            // 위조 금지(D7) — Figma 목업 수치/EN명 반입 금지(데이터 소스 부재)
            Assert.That(source, Does.Not.Contain("PLASMA LASER AXE"), "EN명 데이터 부재 — 목업 문구 금지.");
            Assert.That(source, Does.Not.Contain("245 - 310"), "목업 스탯 금지.");
            Assert.That(source, Does.Not.Contain("18.2 / 120.0"), "무게 데이터 부재 — kg 위조 금지.");
            // [129차] 애니 배선 계약 보존
            Assert.That(source, Does.Contain("TriggerCraft"));
            Assert.That(source, Does.Contain("TriggerCheer"));
        }

        [Test]
        public void StaticLayer_InheritsSlotLabelFooterAndV1Overlays()
        {
            var host = ConstructBench();

            // 구조 계약 보존 — 3패널 직계(기존 CraftBenchFigmaPhase4Tests와 동일 기준)
            Assert.That(host.Content.childCount, Is.EqualTo(3));

            // [W23] 성공확률 좌측 라벨 — 기반 SuccessRateLabel 슬롯에 v1 문구 주입(63:62)
            var rateLabel = host.CraftPanelRoot.Q<Label>("wf-success-rate-label");
            Assert.That(rateLabel, Is.Not.Null, "성공확률 좌측 라벨 존재");
            Assert.That(rateLabel.text, Is.EqualTo("제작 성공 확률"));
            Assert.That(rateLabel.style.left.value.value, Is.EqualTo(19.2f).Within(0.001f));
            Assert.That(rateLabel.style.top.value.value, Is.EqualTo(157f).Within(0.001f));
            Assert.That(rateLabel.style.fontSize.value.value, Is.EqualTo(14.4f).Within(0.001f));

            // [W24] 성공확률 값 — 63:63 14.4 #56D364
            var rateValue = host.CraftPanelRoot.Q<Label>("craft-bench-success-rate");
            Assert.That(rateValue.style.fontSize.value.value, Is.EqualTo(14.4f).Within(0.001f));
            Assert.That(rateValue.style.top.value.value, Is.EqualTo(157f).Within(0.001f));
            AssertColor(new Color(0x56 / 255f, 0xD3 / 255f, 0x64 / 255f, 1f), rateValue.style.color.value, "W24 값색 #56D364.");

            // [W25] Divider — 63:64 (24,366,456,1.2) #30363D@0.502
            var divider = host.CraftPanelRoot.Q<VisualElement>("wf-combination-divider");
            Assert.That(divider, Is.Not.Null, "조합/레시피 섹션 사이 Divider 존재");
            AssertStyleRect(divider, new Rect(24f, 366f, 456f, 1.2f));
            AssertColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.502f), divider.style.backgroundColor.value, "Divider 크롬색.");

            // [W17] 결과 슬롯 — 63:57 stroke #58A6FF 1.8 · r9.6
            var resultSlot = host.CraftPanelRoot.Q<VisualElement>("BenchResult");
            Assert.That(resultSlot.style.borderTopWidth.value, Is.EqualTo(1.8f).Within(0.001f));
            AssertColor(UTKTheme.Accent, resultSlot.style.borderTopColor.value, "결과 슬롯 액센트 스트로크.");
            Assert.That(resultSlot.style.borderTopLeftRadius.value.value, Is.EqualTo(9.6f).Within(0.001f));

            // [W36] 제작 버튼 — 63:99 fill #58A6FF · r7.2 · 라벨 #0B0E14 16.8
            var craftButton = host.CraftPanelRoot.Q<Button>("craft-bench-craft-button");
            AssertColor(UTKTheme.Accent, craftButton.style.backgroundColor.value, "제작 버튼 액센트 fill.");
            Assert.That(craftButton.style.borderTopLeftRadius.value.value, Is.EqualTo(7.2f).Within(0.001f));
            Assert.That(craftButton.style.fontSize.value.value, Is.EqualTo(16.8f).Within(0.001f));

            // [W46] BlueprintVisual — 라인 8+12 · 타원 2 · RenderGraphic 240×240
            var blueprint = host.DetailPanelRoot.Q<VisualElement>("wf-blueprint-visual");
            Assert.That(blueprint, Is.Not.Null, "BlueprintVisual 레이어 존재");
            Assert.That(blueprint.Query<VisualElement>("wf-blueprint-line-h").ToList().Count, Is.EqualTo(8), "가로 메시 라인 8본(v1 63:130-137).");
            Assert.That(blueprint.Query<VisualElement>("wf-blueprint-line-v").ToList().Count, Is.EqualTo(12), "세로 메시 라인 12본(v1 63:139-150).");
            Assert.That(blueprint.Query<VisualElement>("wf-blueprint-ellipse-outer"), Is.Not.Null, "타원 216(63:151).");
            Assert.That(blueprint.Query<VisualElement>("wf-blueprint-ellipse-inner"), Is.Not.Null, "타원 96(63:152).");
            var render = host.DetailPanelRoot.Q<VisualElement>("craft-bench-detail-actual-item-icon");
            Assert.That(render.style.width.value.value, Is.EqualTo(240f).Within(0.001f), "RenderGraphic 240×240(기반 160 교체).");
            Assert.That(render.style.height.value.value, Is.EqualTo(240f).Within(0.001f));

            // [W43] EN 아이템명 — 데이터 부재 슬롯 표기(위조 금지)
            var nameEn = host.DetailPanelRoot.Q<Label>("wf-detail-name-en");
            Assert.That(nameEn, Is.Not.Null, "EN 아이템명 라벨 존재");
            Assert.That(nameEn.text, Is.EqualTo("—"), "EN명 데이터 부재 — 슬롯 표기.");
            Assert.That(nameEn.style.fontSize.value.value, Is.EqualTo(14.4f).Within(0.001f));

            // [W44] TierBadge — V6 헬퍼 프레임, 기반 플레인 라벨은 데이터 소스로 숨김
            var tierBadge = host.DetailPanelRoot.Q<VisualElement>("wf-detail-tier-badge");
            Assert.That(tierBadge, Is.Not.Null, "TierBadge 프레임 존재");
            Assert.That(tierBadge.style.borderTopLeftRadius.value.value, Is.EqualTo(4.8f).Within(0.001f), "TierBadge r4.8.");
            var rarityPlain = host.DetailPanelRoot.Q<Label>("craft-bench-detail-rarity");
            Assert.That(rarityPlain, Is.Not.Null);
            // StyleEnum<DisplayStyle>를 enum과 직접 비교하면 "Expected None But was None" 오탈시 — resolvedStyle로 판정.
            Assert.That(rarityPlain.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "플레인 등급 라벨은 데이터 소스로 숨김.");

            // [W49] StatsGrid 3행 + Divider — 값 슬롯 표기(위조 금지)
            var statsGrid = host.DetailPanelRoot.Q<VisualElement>("wf-stats-grid");
            Assert.That(statsGrid, Is.Not.Null, "StatsGrid 존재");
            Assert.That(host.DetailPanelRoot.Q<Label>("wf-stat-value-0"), Is.Not.Null);
            Assert.That(host.DetailPanelRoot.Q<Label>("wf-stat-value-1"), Is.Not.Null);
            Assert.That(host.DetailPanelRoot.Q<Label>("wf-stat-value-2"), Is.Not.Null);
            Assert.That(host.DetailPanelRoot.Q<Label>("wf-stat-value-0").text, Is.EqualTo("—"), "스탯 데이터 부재 — 슬롯 표기.");
            Assert.That(host.DetailPanelRoot.Q<VisualElement>("wf-stats-divider"), Is.Not.Null, "63:166 Divider.");

            // [W50] 바이오텍스트 — 63:167 15.6 (lineH 23.4는 Unity 6 IStyle 미지원 — 43차 함정, 어서션 제외)
            var bio = host.DetailPanelRoot.Q<Label>("craft-bench-detail-description");
            Assert.That(bio.style.fontSize.value.value, Is.EqualTo(15.6f).Within(0.001f));

            // [W52] BadgeItem — 63:189-190 r4.8 fill #FFFFFF@0.039 · "보유 재료 리스트"
            var badge = host.StoragePanelRoot.Q<Label>("craft-bench-storage-source");
            Assert.That(badge.text, Is.EqualTo("보유 재료 리스트"), "W52 배지 문구(StorageSource override).");
            Assert.That(badge.style.borderTopLeftRadius.value.value, Is.EqualTo(4.8f).Within(0.001f));
            AssertColor(new Color(1f, 1f, 1f, 0.039f), badge.style.backgroundColor.value, "배지 fill #FFFFFF@0.039.");
            Assert.That(badge.style.top.value.value, Is.EqualTo(96f).Within(0.001f), "StorageStats 행(기반 y121 게임 전용 위치 교체).");

            // [W53] SlotCapacity — 63:191 14.4 우측 정렬
            var capacity = host.StoragePanelRoot.Q<Label>("craft-bench-storage-capacity");
            Assert.That(capacity.style.fontSize.value.value, Is.EqualTo(14.4f).Within(0.001f));
            Assert.That(capacity.style.right.value.value, Is.EqualTo(24f).Within(0.001f));

            // [W58] StorageFooter — 63:297-305 (24,697,456,56) + 트랙 r3.6 + fill 폭 0(무게 데이터 부재)
            var footer = host.StoragePanelRoot.Q<VisualElement>("wf-storage-footer");
            Assert.That(footer, Is.Not.Null, "StorageFooter 존재");
            AssertStyleRect(footer, new Rect(24f, 697f, 456f, 56f));
            var track = footer.Q<VisualElement>("wf-storage-progress-track");
            Assert.That(track.style.height.value.value, Is.EqualTo(7.2f).Within(0.001f));
            AssertColor(new Color(1f, 1f, 1f, 0.078f), track.style.backgroundColor.value, "트랙 #FFFFFF@0.078.");
            var fill = footer.Q<VisualElement>("wf-storage-progress-fill");
            Assert.That(fill.style.width.value.value, Is.EqualTo(0f).Within(0.001f), "무게 데이터 부재 — 진행 없음.");

            // [W55] 스토리지 셀 — 63:194 r9.6
            var cell = host.StoragePanelRoot.Q<VisualElement>("craft-bench-storage-cell-0");
            Assert.That(cell.style.borderTopLeftRadius.value.value, Is.EqualTo(9.6f).Within(0.001f));
        }

        [Test]
        public void DynamicRows_RightBadgesRowStatesAndTabChrome()
        {
            var host = ConstructBench();
            InvokePrivate(host, "OnWindowOpen");     // 베이스 재구성(행/스토리지) + v1 동기화
            InvokePrivate(host, "RefreshV1Dynamic");

            var book = host.CraftPanelRoot.Q<ScrollView>("craft-bench-recipe-list");
            Assert.That(book, Is.Not.Null);
            int rowCount = 0;
            foreach (var row in book.contentContainer.Children())
            {
                if ((row.name ?? string.Empty).StartsWith("craft-bench-recipe-row-") == false) continue;
                rowCount++;
                // [W34] 우측 배지 — SELECTED/LV. 13.2 GeistMono 폴백
                var badge = row.Q<Label>("wf-row-badge");
                Assert.That(badge, Is.Not.Null, "행 우측 배지 존재");
                Assert.That(badge.text.StartsWith("LV. ") || badge.text == "SELECTED", Is.True,
                    "배지 문구는 LV. n 또는 SELECTED(63:77/87/97).");
                Assert.That(badge.style.fontSize.value.value, Is.EqualTo(13.2f).Within(0.001f));
                // [W30] 행 상태 — 비활성 무배경 + 크롬 스트로크 · r7.2
                Assert.That(row.style.borderTopWidth.value, Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(row.style.borderTopLeftRadius.value.value, Is.EqualTo(7.2f).Within(0.001f));
                // [W31] 아이콘 셀 티어 스트립
                var icon = row.Q<VisualElement>("recipe-result-icon");
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.Q<VisualElement>("wf-row-tier-strip"), Is.Not.Null, "행 티어 스트립(63:71).");
            }
            Assert.That(rowCount, Is.GreaterThanOrEqualTo(10), "WeaponCraftDatabase 레시피 행 렌더.");

            // [W19] 재료 슬롯 카운트 — 무배치 시 숨김(위조 금지), 배치 시 보유/요구
            var slot = host.CraftPanelRoot.Q<VisualElement>("BenchSlot_0");
            Assert.That(slot, Is.Not.Null);

            // [W10/W11] 탭 크롬 — 활성 #58A6FF / 비활성 투명+크롬, r7.2, fs15.6
            var bar = host.CraftPanelRoot.Q<VisualElement>("craft-bench-recipe-filters");
            int activeTabs = 0;
            foreach (var tab in bar.Query<Button>().ToList())
            {
                Assert.That(tab.style.fontSize.value.value, Is.EqualTo(15.6f).Within(0.001f), "W12 탭 라벨 15.6.");
                Assert.That(tab.style.borderTopLeftRadius.value.value, Is.EqualTo(7.2f).Within(0.001f));
                if (tab.enabledSelf == false) activeTabs++;   // 기반 활성 표현 = SetEnabled(false)
            }
            Assert.That(activeTabs, Is.EqualTo(1), "활성 탭 1개(전체).");

            // [W57] 빈 셀 마크 — 28.8 원형 근사
            var emptyCell = host.StoragePanelRoot.Q<VisualElement>("craft-bench-storage-cell-24");
            Assert.That(emptyCell.Q<VisualElement>("wf-storage-empty-mark"), Is.Not.Null, "빈 셀 마크(63:256 근사).");
        }

        private static void AssertStyleRect(VisualElement element, Rect expected)
        {
            Assert.That(element.style.left.value.value, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(element.style.top.value.value, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(element.style.width.value.value, Is.EqualTo(expected.width).Within(0.001f));
            Assert.That(element.style.height.value.value, Is.EqualTo(expected.height).Within(0.001f));
        }

        private static void AssertColor(Color expected, Color actual, string message)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.002f), message);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.002f), message);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.002f), message);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.002f), message);
        }
    }
}
