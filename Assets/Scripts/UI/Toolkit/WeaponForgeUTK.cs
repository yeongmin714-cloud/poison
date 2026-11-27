using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems; // [129차] HumanoidClipDriver — 액션 애니 배선
using ProjectName.UI;      // [144차 P2-WF] UIFont — GeistMono 에셋 부재 폴백 선례(LootWindowUTK/UTKSlot GetGeistMonoFont)

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P18-C2 — 무기/장비 제작대 (마인크래프트식, UTK).
    /// 재료 3칸 + 결과 1칸. 레시피 = WeaponCraftDatabase(멧돼지 엄니/늑대 이빨/가죽 계열).
    /// 제작 = CraftingHelper.CraftWeapon(확정 성공, EXP+20, CraftSucceeded 발화).
    /// [144차 P2-WF] crafting.md §3.1 v1(63:5) W행 수리 — WeaponForgeUTK v1 고유분(우측 배지/EN명/
    /// TierBadge/BlueprintVisual/StatsGrid/BadgeItem + P1 잔존). 기반 공통분은 CraftBenchBaseUTK의
    /// [144차 P2-CRAFT] 마커 소관(범위 화이트리스트 준수 — 본 파일만 수정). 기반 공개 훅
    /// (CraftPanelRoot/DetailPanelRoot/StoragePanelRoot + virtual 슬롯) 위에 v1 오버레이를 얹는다.
    /// </summary>
    public class WeaponForgeUTK : CraftBenchBaseUTK
    {
        private static WeaponForgeUTK _instance;

        public static void Open()
        {
            if (_instance == null || _instance.panel == null)
                _instance = new WeaponForgeUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) { Debug.LogWarning("[WeaponForgeUTK] UIRoot 없음"); return; }
            if (_instance.parent == null) root.Add(_instance);
            _instance.Show();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // [144차 P2-WF W5] 제목 텍스트 — Figma 63:21 "아이템 제작"(24/700). 기존 "⚒️ 무기 제작대"는 Figma 무존재 문구.
        private WeaponForgeUTK() : base("아이템 제작", 3, new Vector2(520f, 560f))
        {
            ApplyV1FigmaLayer();
            // [144차 P2-WF] 베이스 상태 변화(탭/행/슬롯/스토리지 재구성)는 모두 클릭 경유 —
            // 패널 루트 버블 1곳 + 오픈 훅으로 v1 오버레이(배지/카운트/스트립)를 재동기화한다.
            CraftPanelRoot.RegisterCallback<PointerDownEvent>(OnV1PanelPointer);
            StoragePanelRoot.RegisterCallback<PointerDownEvent>(OnV1PanelPointer);
        }

        protected override string BenchSubtitle => "CRAFTING";
        // [144차 P2-WF W39] Detail 제목 — 63:116 "상세 정보"(기반 기본값 "선택한 결과 상세" 교체).
        protected override string DetailHeading => "상세 정보";
        protected override string RecipeHeading => "제작 가능한 레시피 목록";
        // [144차 P2-WF W48] 사양 헤딩 — 63:155 "무기 사양 및 제작 정보"(기존 "제작 및 아이템 정보" 교체).
        protected override string DetailDescriptionHeading => "무기 사양 및 제작 정보";
        protected override string CraftActionText => "아이템 제작하기 (CRAFT)";
        // [144차 P2-WF W52] 스토리지 배지 문구 — 63:190 "보유 재료 리스트"(기존 "플레이어 인벤토리"는 Figma 무존재 — D7 문구 교체).
        protected override string StorageSource => "보유 재료 리스트";
        // [144차 P2-WF W58] v1 WeightLabel — 63:302 "가방 적재 중량"(§1 판본차: v2 "재료 적재 중량"은 C행 몫).
        // 값은 D7(위조 금지) — PlayerInventory 무게 데이터 부재(2026-10-31 실측) → "—" 슬롯 표기, kg 위조 없음.
        protected override string StorageFooterWeightLabel => "가방 적재 중량";
        protected override bool MatchesFilter(BenchRecipe recipe, string filter)
        {
            if (filter == "전체") return true;
            var item = PlayerInventory.GetItemById(recipe.ResultId);
            if (item == null) return false;
            switch (filter)
            {
                case "무기": return item.category == PlayerInventory.ItemCategory.Weapon;
                case "장비": return item.category == PlayerInventory.ItemCategory.Armor || item.category == PlayerInventory.ItemCategory.Accessory;
                case "도구": return item.category == PlayerInventory.ItemCategory.Tool;
                default: return false;
            }
        }

        protected override IReadOnlyList<BenchRecipe> Recipes
        {
            get
            {
                var list = new List<BenchRecipe>();
                foreach (var r in WeaponCraftDatabase.All)
                {
                    var mats = new string[3];
                    mats[0] = r.Mat1Id;
                    if (r.Mat1Count >= 2) mats[1] = r.Mat1Id;
                    if (r.Mat1Count >= 3) mats[2] = r.Mat1Id;
                    if (r.Mat2Count >= 1 && !string.IsNullOrEmpty(r.Mat2Id))
                        mats[r.Mat1Count >= 2 ? 2 : 1] = r.Mat2Id;

                    list.Add(new BenchRecipe
                    {
                        ResultId = r.ResultId,
                        ResultName = WeaponCraftDatabase.DisplayName(r.ResultId),
                        MatIds = mats,
                        Note = $"Lv.{r.RequiredLevel}+",
                        rarity = r.rarity,   // [Milestone A/D] 성공률 희귀도 페널티
                    });
                }
                return list;
            }
        }

        // [Milestone D] 미발견 레시피 = "?" 블라인드 — 성공 시 CraftWeapon이 MarkDiscovered → 공개.
        protected override bool IsDiscovered(BenchRecipe r) => RecipeDiscoverySystem.IsDiscovered(r.ResultName);

        // [Milestone D] 발견된 레시피의 성공률·운 표시.
        protected override string RateHint(BenchRecipe r)
        {
            int chance = CraftingHelper.ComputeFinalCraftChance(CraftingHelper.WeaponBaseSuccessRate, r.rarity);
            string luck = "";
            if (PlayerStats.Instance != null)
            {
                int luckBonus = Mathf.RoundToInt(PlayerStats.Instance.GetLuckCraftBonus());
                if (luckBonus > 0) luck = $" (운 +{luckBonus}%)";
            }
            return $"성공률 {chance}%{luck}";
        }

        protected override bool TryCraft(BenchRecipe recipe, List<string> placedIds, out string message)
        {
            // [129차] 제작 애니(Craft) 발화 — 플레이어 접근 선례(HerbPickup L309)
            var player = GameObject.FindGameObjectWithTag("Player");
            var driver = player != null ? (player.GetComponent<HumanoidClipDriver>() ?? player.GetComponentInChildren<HumanoidClipDriver>()) : null;
            driver?.TriggerCraft();

            if (CraftingHelper.CraftWeapon(recipe.ResultId, out message))
            {
                // [129차] 확정 성공 — Craft→Cheer는 ExitTime 0.92 게이트라 애니 마무리 후 환호
                driver?.TriggerCheer();
                return true;
            }
            return false;
        }

        // =====================================================================
        //  [144차 P2-WF] v1(63:5) 고유 수리 레이어 — crafting.md §3.1 W행 근거.
        //  기반(CraftBenchBaseUTK)은 공통 기하만 제공하고, v1 판본 요소는 본 파일에서
        //  공개 패널 루트에 주입한다(범위 화이트리스트 — 기반 파일 수정 없음).
        //  =====================================================================

        // ── v1 프레임 티어 팔레트 — 63:40/45/50 스트립 실측(#1F6FEB/#BC8CFF/#D29922) + 그레이(63:210).
        //    Rare=#1F6FEB는 InventoryWindowUTK EquipStripRare, Epic=#BC8CFF는 63:125 TierBadge 표본과 정합.
        private static readonly Color WfTierRare = new Color(0x1F / 255f, 0x6F / 255f, 0xEB / 255f, 1f);
        private static readonly Color WfTierEpic = new Color(0xBC / 255f, 0x8C / 255f, 0xFF / 255f, 1f);
        private static readonly Color WfTierTop = new Color(0xD2 / 255f, 0x99 / 255f, 0x22 / 255f, 1f);   // Legendary/Unique
        private static readonly Color WfStatRed = new Color(0xFF / 255f, 0x7B / 255f, 0x72 / 255f, 1f);   // 63:159
        private static readonly Color WfStatGreen = new Color(0x56 / 255f, 0xD3 / 255f, 0x64 / 255f, 1f); // 63:63/162
        private static readonly Color WfInkOnAccent = new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f); // 63:28/100 라벨
        private const string RowNamePrefix = "craft-bench-recipe-row-";

        private Label _wfRateValue;                      // [144차 P2-WF W24] 성공확률 값 라벨(기반 craft-bench-success-rate)
        private Label _wfDetailNameEn;                   // [144차 P2-WF W43] EN 아이템명 슬롯
        private VisualElement _wfTierBadge;              // [144차 P2-WF W44] TierBadge 프레임
        private Label _wfTierBadgeLabel;                 // [144차 P2-WF W44] TierBadge 텍스트
        private Label[] _wfStatValues;                   // [144차 P2-WF W49] 스탯 값 라벨 3(슬롯 표기)
        private Label _wfStorageCapacity;                // [144차 P2-WF W53] SlotCapacity 라벨
        private Label _wfStorageBadge;                   // [144차 P2-WF W52] BadgeItem(기반 storage-source 전용)
        private Label _wfStorageWeightValue;             // [144차 P2-WF W58] StorageFooter 값 라벨
        private VisualElement _wfStorageProgressFill;    // [144차 P2-WF W58] StorageFooter 진행바 fill
        private bool _wfStaticApplied;
        private bool _wfRefreshScheduled;
        private string _wfSelectedId;                    // [144차 P2-WF W34] 선택 행 미러(베이스 _selected private)
        private Dictionary<int, string> _wfTexToItemId;  // [144차 P2-WF W19] 슬롯 아이콘 텍스처 → 재료 id
        private Dictionary<string, WeaponCraftDatabase.WeaponRecipe> _wfRecipeById;

        /// <summary>v1 정적 레이어 — 베이스 생성 직후 1회. 동적 상태(행/슬롯 내용)는 RefreshV1Dynamic이 담당.</summary>
        private void ApplyV1FigmaLayer()
        {
            if (_wfStaticApplied) return;
            _wfStaticApplied = true;

            StyleCraftPanelV1();
            StyleDetailPanelV1();
            StyleStoragePanelV1();

            // [144차 P2-WF] 신규/변경 요소에 전역 폰트 재적용(단일 폰트 정책) 후 Geist Mono 재지정.
            ApplyUIToolkitFont(CraftPanelRoot);
            ApplyUIToolkitFont(DetailPanelRoot);
            ApplyUIToolkitFont(StoragePanelRoot);
            ApplyMonoFont(_wfRateValue, true);            // W24 — GeistMono700
            ApplyMonoFont(_wfStorageCapacity, false);     // W53 — GeistMono400
            ApplyMonoFont(_wfStorageWeightValue, true);   // W58 — GeistMono700
            if (_wfStatValues != null)
                foreach (var valueLabel in _wfStatValues) ApplyMonoFont(valueLabel, true);   // W49 — GeistMono700

            RefreshV1Dynamic();   // 탭/빈셀 등 생성 즉시 분 — 행/슬롯 내용은 오픈 훅이 재동기화
        }

        // ── CraftPanel(63:6) v1 정적 스타일 ──
        private void StyleCraftPanelV1()
        {
            // [144차 P2-WF W13] 조합섹션 카드 — 63:35 fill #FFFFFF@0.012 · stroke #30363D@0.502 1.2 · r9.6.
            var combination = CraftPanelRoot.Q<VisualElement>("craft-bench-combination-section");
            if (combination != null)
            {
                combination.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.012f));
                UTKTheme.ApplyElementBorder(combination);
                UTKTheme.ApplyRadius(combination, 9.6f);
            }

            // [144차 P2-WF W14] 섹션 라벨 — 63:36 14.4/700 #58A6FF(기반 TextSecondary/Normal 교체).
            var comboHeading = CraftPanelRoot.Q<Label>("craft-bench-combination-heading");
            if (comboHeading != null)
            {
                comboHeading.style.color = new StyleColor(UTKTheme.Accent);
                comboHeading.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            // [144차 P2-WF W23] 성공확률 좌측 라벨 — 63:62 "제작 성공 확률" 14.4/400 #8B949E @(19.2,157).
            // 기반 W23 슬롯(SuccessRateLabel 프로퍼티)에 v1 문구 주입. _status(게임 전용 안내, §5 유지)와 별행.
            if (combination != null)
            {
                var rateLabel = new Label(SuccessRateLabel) { name = "wf-success-rate-label", pickingMode = PickingMode.Ignore };
                rateLabel.style.position = Position.Absolute;
                rateLabel.style.left = 19.2f;
                rateLabel.style.top = 157f;
                rateLabel.style.fontSize = 14.4f;
                rateLabel.style.color = new StyleColor(UTKTheme.TextSub);
                combination.Add(rateLabel);
            }

            // [144차 P2-WF W24/W22] 성공확률 값 — 63:63 14.4 GeistMono700 #56D364, y157(기반 13.2/#3FB950/158 교체).
            _wfRateValue = CraftPanelRoot.Q<Label>("craft-bench-success-rate");
            if (_wfRateValue != null)
            {
                _wfRateValue.style.fontSize = 14.4f;
                _wfRateValue.style.top = 157f;
                _wfRateValue.style.color = new StyleColor(WfStatGreen);
            }

            // [144차 P2-WF W25] Divider — 63:64 (24,366,456,1.2) #30363D@0.502. 조합/레시피 섹션 사이 구분선.
            var divider = new VisualElement { name = "wf-combination-divider", pickingMode = PickingMode.Ignore };
            divider.style.position = Position.Absolute;
            divider.style.left = 24f;
            divider.style.top = 366f;
            divider.style.width = 456f;
            divider.style.height = 1.2f;
            divider.style.backgroundColor = new StyleColor(UTKTheme.BorderChrome);
            CraftPanelRoot.Add(divider);

            // [144차 P2-WF W27] 레시피 섹션 라벨 — 63:66 14.4/700 #8B949E(기반 TextPrimary 반전 교체).
            var recipeHeading = CraftPanelRoot.Q<Label>("craft-bench-recipe-heading");
            if (recipeHeading != null)
            {
                recipeHeading.style.color = new StyleColor(UTKTheme.TextSub);
                recipeHeading.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            // [144차 P2-WF W20/W21] 플로우 화살표 — 63:54 #58A6FF, 좌우 gap 14.4
            // (프레임 폭 28.8 고정 → ResultGroup 합산 x≈302 ≈ Figma 301.8 — 동적 글리프 폭 상수화).
            var arrow = CraftPanelRoot.Q<Label>("craft-bench-flow-arrow");
            if (arrow != null)
            {
                arrow.style.color = new StyleColor(UTKTheme.Accent);
                arrow.style.width = 28.8f;
                arrow.style.height = 21.6f;
                arrow.style.marginLeft = 14.4f;
                arrow.style.marginRight = 14.4f;
                arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            }

            // [144차 P2-WF W17] 결과 슬롯 — 63:57 fill #30363D@0.8 · stroke #58A6FF 1.8 · r9.6(UTKSlot 기본값 교체).
            var resultSlot = CraftPanelRoot.Q<UTKSlot>("BenchResult");
            if (resultSlot != null)
            {
                resultSlot.style.backgroundColor = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.8f));
                var resultStroke = new StyleColor(UTKTheme.Accent);
                resultSlot.style.borderTopColor = resultStroke;
                resultSlot.style.borderBottomColor = resultStroke;
                resultSlot.style.borderLeftColor = resultStroke;
                resultSlot.style.borderRightColor = resultStroke;
                resultSlot.style.borderTopWidth = 1.8f;
                resultSlot.style.borderBottomWidth = 1.8f;
                resultSlot.style.borderLeftWidth = 1.8f;
                resultSlot.style.borderRightWidth = 1.8f;
                UTKTheme.ApplyRadius(resultSlot, 9.6f);
            }

            // [144차 P2-WF W36/W37] 제작 버튼 — 63:99 fill #58A6FF · r7.2 · 무스트로크, 라벨 16.8/700 #0B0E14.
            var craftButton = CraftPanelRoot.Q<Button>("craft-bench-craft-button");
            if (craftButton != null)
            {
                craftButton.style.backgroundColor = new StyleColor(UTKTheme.Accent);
                craftButton.style.borderTopWidth = craftButton.style.borderBottomWidth = 0f;
                craftButton.style.borderLeftWidth = craftButton.style.borderRightWidth = 0f;
                UTKTheme.ApplyRadius(craftButton, 7.2f);
                craftButton.style.color = new StyleColor(WfInkOnAccent);
                craftButton.style.fontSize = 16.8f;
                craftButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            // [144차 P2-WF W7] 닫기 버튼 — 63:23 31.2×31.2 fill #FFFFFF@0.059 · stroke 크롬 1.2 · r7.2.
            var closeButton = CraftPanelRoot.Q<Button>("craft-bench-close-button");
            if (closeButton != null)
            {
                closeButton.style.width = 31.2f;
                closeButton.style.height = 31.2f;
                closeButton.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.059f));
                UTKTheme.ApplyElementBorder(closeButton);
                UTKTheme.ApplyRadius(closeButton, 7.2f);
            }

            // [144차 P2-WF W5/W39+W6/W40] 헤더 타이포 — 제목 24/700(63:21/116), 부제 14.4 ls1.2(63:22/117).
            StyleHeaderTypo(CraftPanelRoot.Q<VisualElement>("craft-bench-crafting-header"));
            StyleHeaderTypo(DetailPanelRoot.Q<VisualElement>("craft-bench-detail-header"));
        }

        /// <summary>패널 헤더 타이포 보정 — 제목 라벨(이름 *-title) 24/700, 영문 부제(텍스트 매칭) 14.4/ls1.2.</summary>
        private void StyleHeaderTypo(VisualElement header)
        {
            if (header == null) return;
            foreach (var label in header.Query<Label>().ToList())
            {
                if (!string.IsNullOrEmpty(label.name) && label.name.EndsWith("-title"))
                {
                    label.style.fontSize = 24f;                                    // W5/W39 — fs 21.6→24
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;          // 700
                }
                else if (label.text == "CRAFTING" || label.text == "SPECIFICATIONS")
                {
                    label.style.fontSize = 14.4f;                                  // W6/W40 — fs 12→14.4
                    label.style.letterSpacing = 1.2f;                              // W6 — 63:22 ls1.2
                    label.style.unityFontStyleAndWeight = FontStyle.Normal;        // 500→UITK Normal 매핑
                }
            }
        }

        // ── DetailPanel(63:101) v1 정적 스타일/요소 ──
        private void StyleDetailPanelV1()
        {
            // [144차 P2-WF W41] 아이템명 섹션 카드 — 63:121 fill #FFFFFF@0.02 · stroke 크롬 1.2 · r9.6.
            var nameSection = DetailPanelRoot.Q<VisualElement>("craft-bench-detail-name-section");
            if (nameSection != null)
            {
                nameSection.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
                UTKTheme.ApplyElementBorder(nameSection);
                UTKTheme.ApplyRadius(nameSection, 9.6f);
                nameSection.style.paddingRight = 9.6f;   // 배지 우측 여백(63:125 우단 근사)
            }

            var detailName = DetailPanelRoot.Q<Label>("craft-bench-detail-name");
            // [144차 P2-WF W42] 아이템명 굵기 — 63:123 21.6/700(기반 Normal 교체).
            if (detailName != null) detailName.style.unityFontStyleAndWeight = FontStyle.Bold;

            // [144차 P2-WF W43/W44] ItemNameGroup 2단(명 63:123 + EN명 63:124) + TierBadge(63:125-126).
            if (nameSection != null && detailName != null)
            {
                var group = new VisualElement { name = "wf-detail-name-group" };
                group.style.flexDirection = FlexDirection.Column;
                group.style.flexShrink = 1f;
                group.Add(detailName);   // 재부모 — 이름/EN명 2단 그룹(Figma 63:122)
                // [144차 P2-WF W43] EN 아이템명 — 63:124 14.4/400 #8B949E.
                // 데이터 판정(2026-10-31 실측): PlayerInventory.ItemData에 EN명 필드 없음
                // (id/displayName/description/category/icon/maxStack/maxDurability/rarity/effects/isBomb/basePrice)
                // → 위조 금지(D7): 슬롯 표기 "—" 고정. EN명 데이터 확보 시 본 라벨 갱신점만 연결.
                _wfDetailNameEn = new Label("—") { name = "wf-detail-name-en", pickingMode = PickingMode.Ignore };
                _wfDetailNameEn.style.fontSize = 14.4f;
                _wfDetailNameEn.style.marginTop = 2f;
                _wfDetailNameEn.style.color = new StyleColor(UTKTheme.TextSub);
                _wfDetailNameEn.style.whiteSpace = WhiteSpace.NoWrap;
                group.Add(_wfDetailNameEn);
                nameSection.Add(group);

                // [144차 P2-WF W44] TierBadge — 63:125-126 48.2×24.6 r4.8 · fill 티어색@0.122 · stroke 티어색 1.2 ·
                // "EPIC" 13.2/700 티어색 — Phase 0 V6 UTKTheme.CreateTierBadge 사용(기반 MakeLabel 플레인 텍스트 폐기).
                // 기반 _detailRarity(플레인 라벨)는 등급 데이터 소스로 유지(숨김) — 동기화는 RefreshV1TierBadge.
                var rarityLabel = DetailPanelRoot.Q<Label>("craft-bench-detail-rarity");
                if (rarityLabel != null) rarityLabel.style.display = DisplayStyle.None;
                _wfTierBadge = UTKTheme.CreateTierBadge("", UTKTheme.Accent, "wf-detail-tier-badge");
                _wfTierBadge.style.display = DisplayStyle.None;
                _wfTierBadgeLabel = _wfTierBadge.Q<Label>();
                nameSection.Add(_wfTierBadge);
            }

            // [144차 P2-WF W45] 아이템 이미지 섹션 카드 — 63:127 fill #FFFFFF@0.012 · stroke 크롬 1.2 · r9.6
            // (기반 PanelSub 단색 배경 교체).
            var imageSection = DetailPanelRoot.Q<VisualElement>("craft-bench-detail-image-section");
            if (imageSection != null)
            {
                imageSection.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.012f));
                UTKTheme.ApplyElementBorder(imageSection);
                UTKTheme.ApplyRadius(imageSection, 9.6f);
            }

            // [144차 P2-WF W46] BlueprintVisual — 63:128-153: MeshGrid 가로 8본(y0..384 pitch≈55) +
            // 세로 12본(x22..506 pitch44 · h120) + Ellipse 216/96(#58A6FF@0.102/0.122) + RenderGraphic 240×240 중앙.
            // 라인/타원은 UTK 어휘(border/backgroundColor 요소)로 구현 — 벡터메시 금지 계약 준수.
            if (imageSection != null)
            {
                var blueprint = new VisualElement { name = "wf-blueprint-visual", pickingMode = PickingMode.Ignore };
                blueprint.style.position = Position.Absolute;
                blueprint.style.left = 0f;
                blueprint.style.top = 0f;
                blueprint.style.width = 528f;
                blueprint.style.height = 384f;
                float[] horizontalY = { 0f, 55f, 110f, 164f, 219f, 274f, 329f, 382.8f };   // 63:130-137(말단 384→382.8 클램프)
                foreach (float y in horizontalY)
                    blueprint.Add(BuildBlueprintBar(0f, y, 528f, 1.2f, "wf-blueprint-line-h"));
                for (int i = 0; i < 12; i++)                                              // 63:139-150
                    blueprint.Add(BuildBlueprintBar(22f + i * 44f, 0f, 1.2f, 120f, "wf-blueprint-line-v"));
                blueprint.Add(BuildBlueprintEllipse(156f, 84f, 216f, 0.102f, "wf-blueprint-ellipse-outer"));   // 63:151
                blueprint.Add(BuildBlueprintEllipse(216f, 144f, 96f, 0.122f, "wf-blueprint-ellipse-inner"));   // 63:152
                imageSection.Add(blueprint);
                var icon = DetailPanelRoot.Q<VisualElement>("craft-bench-detail-actual-item-icon");
                if (icon != null)
                {
                    icon.style.width = 240f;    // [144차 P2-WF W46] RenderGraphic 240×240(기반 160 교체)
                    icon.style.height = 240f;
                    icon.BringToFront();        // Figma 자식순 — RenderGraphic가 그리드/타원 상층(63:153 말단)
                }
            }

            // [144차 P2-WF W47] 사양 섹션 카드 — 63:154 fill #FFFFFF@0.02 · stroke 크롬 1.2 · r9.6 · pad 하단 19.2.
            var description = DetailPanelRoot.Q<VisualElement>("craft-bench-detail-description-section");
            if (description != null)
            {
                description.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
                UTKTheme.ApplyElementBorder(description);
                UTKTheme.ApplyRadius(description, 9.6f);
                description.style.paddingBottom = 19.2f;
            }

            // [144차 P2-WF W48] 헤딩색/굵기 — 63:155 15.6/700 #58A6FF(텍스트는 override "무기 사양 및 제작 정보").
            var heading = DetailPanelRoot.Q<Label>("craft-bench-detail-description-heading");
            if (heading != null)
            {
                heading.style.color = new StyleColor(UTKTheme.Accent);
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            // [144차 P2-WF W49] StatsGrid/StatRow×3 — 63:156-165 라벨 15.6 #8B949E / 값 우측 GeistMono700
            // (#FF7B72·#56D364·#BC8CFF) + Divider(63:166 489.6×1.2 #30363D@0.502).
            // 데이터 판정(2026-10-31 실측): ItemData에 스탯 필드 없음 → 위조 금지(D7): 값 "—" 슬롯 표기.
            // 실스탯 소스(예: 무기 공격력 DB) 확보 시 값 라벨 갱신점만 연결(W50 바이오텍스트 위 배치).
            if (description != null)
            {
                var stats = new VisualElement { name = "wf-stats-grid" };
                stats.style.marginTop = 14.8f;   // 63:156 desc-로컬 y52(헤딩 하단 기준)
                string[] statLabels = { "에너지 피해량", "자원 채취 효율", "추가 속성" };
                Color[] statValueColors = { WfStatRed, WfStatGreen, WfTierEpic };
                _wfStatValues = new Label[3];
                for (int i = 0; i < 3; i++)
                {
                    var row = new VisualElement { name = "wf-stat-row-" + i };
                    row.style.height = 20f;
                    row.style.marginTop = i == 0 ? 0f : 9.6f;   // StatsGrid gap 9.6(63:156)
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.justifyContent = Justify.SpaceBetween;
                    var rowLabel = new Label(statLabels[i]) { name = "wf-stat-label-" + i, pickingMode = PickingMode.Ignore };
                    rowLabel.style.fontSize = 15.6f;
                    rowLabel.style.color = new StyleColor(UTKTheme.TextSub);
                    var rowValue = new Label("—") { name = "wf-stat-value-" + i, pickingMode = PickingMode.Ignore };
                    rowValue.style.fontSize = 15.6f;
                    rowValue.style.color = new StyleColor(statValueColors[i]);
                    rowValue.style.whiteSpace = WhiteSpace.NoWrap;
                    row.Add(rowLabel);
                    row.Add(rowValue);
                    _wfStatValues[i] = rowValue;
                    stats.Add(row);
                }
                var statDivider = new VisualElement { name = "wf-stats-divider", pickingMode = PickingMode.Ignore };
                statDivider.style.height = 1.2f;
                statDivider.style.marginTop = 13.8f;   // 63:166 desc-로컬 y144
                statDivider.style.backgroundColor = new StyleColor(UTKTheme.BorderChrome);
                stats.Add(statDivider);
                description.Add(stats);

                var bio = DetailPanelRoot.Q<Label>("craft-bench-detail-description");
                if (bio != null)
                {
                    description.Add(bio);   // 재부모 — 자식순 [헤딩, StatsGrid, 바이오텍스트] 복원
                    // [144차 P2-WF W50] 바이오텍스트 — 63:167 15.6/400 #8B949E lineH 23.4(기반 13.2 교체).
                    bio.style.fontSize = 15.6f;
                    // [144차 P2-WF W50] lineH 23.4는 Unity 6 IStyle 미지원(43차 함정) — fs 15.6만 적용, 행높이 보류 기록.
                    bio.style.marginTop = 14.8f;   // 63:167 desc-로컬 y160
                }
            }
        }

        /// <summary>BlueprintVisual 라인/타원 공통 — #FFFFFF@0.02 스트로크 근사 바(UTK 어휘).</summary>
        private static VisualElement BuildBlueprintBar(float x, float y, float w, float h, string elementName)
        {
            var bar = new VisualElement { name = elementName, pickingMode = PickingMode.Ignore };
            bar.style.position = Position.Absolute;
            bar.style.left = x;
            bar.style.top = y;
            bar.style.width = w;
            bar.style.height = h;
            bar.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));   // 63:130-150
            return bar;
        }

        /// <summary>BlueprintVisual 타원 — 보더 라운드 원(UTK 어휘)으로 Ellipse 근사, #58A6FF@alpha 스트로크 1.2.</summary>
        private static VisualElement BuildBlueprintEllipse(float x, float y, float size, float alpha, string elementName)
        {
            var ellipse = new VisualElement { name = elementName, pickingMode = PickingMode.Ignore };
            ellipse.style.position = Position.Absolute;
            ellipse.style.left = x;
            ellipse.style.top = y;
            ellipse.style.width = size;
            ellipse.style.height = size;
            UTKTheme.ApplyRadius(ellipse, size * 0.5f);
            var stroke = new StyleColor(new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, alpha));
            ellipse.style.borderTopColor = stroke;
            ellipse.style.borderBottomColor = stroke;
            ellipse.style.borderLeftColor = stroke;
            ellipse.style.borderRightColor = stroke;
            ellipse.style.borderTopWidth = 1.2f;
            ellipse.style.borderBottomWidth = 1.2f;
            ellipse.style.borderLeftWidth = 1.2f;
            ellipse.style.borderRightWidth = 1.2f;
            return ellipse;
        }

        // ── StoragePanel(63:168) v1 정적 스타일/요소 ──
        private void StyleStoragePanelV1()
        {
            // [144차 P2-WF W52] BadgeItem — 63:189-190 111.2×24.6 r4.8 fill #FFFFFF@0.039 ·
            // "보유 재료 리스트" 13.2/700 #F0F6FC. 기존 "플레이어 인벤토리" 라벨(Figma 무존재)을 배지로 전환
            // (텍스트는 StorageSource override — 기반 RefreshStorage가 매 갱신 유지).
            _wfStorageBadge = StoragePanelRoot.Q<Label>("craft-bench-storage-source");
            if (_wfStorageBadge != null)
            {
                _wfStorageBadge.style.position = Position.Absolute;
                _wfStorageBadge.style.left = 24f;
                _wfStorageBadge.style.top = 96f;   // 63:188 StorageStats 행(기반 y121 게임 전용 위치 교체)
                _wfStorageBadge.style.height = 24.6f;
                _wfStorageBadge.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.039f));
                UTKTheme.ApplyRadius(_wfStorageBadge, 4.8f);
                _wfStorageBadge.style.paddingLeft = _wfStorageBadge.style.paddingRight = 9.6f;
                _wfStorageBadge.style.paddingTop = _wfStorageBadge.style.paddingBottom = 4.8f;
                _wfStorageBadge.style.fontSize = 13.2f;
                _wfStorageBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
                _wfStorageBadge.style.color = new StyleColor(UTKTheme.TextMain);
                _wfStorageBadge.style.whiteSpace = WhiteSpace.NoWrap;
            }

            // [144차 P2-WF W53] SlotCapacity — 63:191 "사용 슬롯: n / m" 14.4 GeistMono400 #8B949E 우측 정렬(x340).
            _wfStorageCapacity = StoragePanelRoot.Q<Label>("craft-bench-storage-capacity");
            if (_wfStorageCapacity != null)
            {
                _wfStorageCapacity.style.left = StyleKeyword.Auto;
                _wfStorageCapacity.style.right = 24f;
                _wfStorageCapacity.style.top = 99f;   // 63:191 StorageStats(24.6) 행 세로센터 근사
                _wfStorageCapacity.style.fontSize = 14.4f;
                _wfStorageCapacity.style.color = new StyleColor(UTKTheme.TextSub);
                _wfStorageCapacity.style.whiteSpace = WhiteSpace.NoWrap;
            }

            // [144차 P2-WF W55] 스토리지 셀 — 63:194 fill #21262D@0.6 · stroke #30363D@0.502 1.2 · r9.6.
            for (int i = 0; i < CraftBenchBaseUTK.StorageCellCount; i++)
            {
                var cell = StoragePanelRoot.Q<VisualElement>("craft-bench-storage-cell-" + i);
                if (cell == null) continue;
                cell.style.backgroundColor = new StyleColor(new Color(0x21 / 255f, 0x26 / 255f, 0x2D / 255f, 0.6f));
                UTKTheme.ApplyElementBorder(cell);
                UTKTheme.ApplyRadius(cell, 9.6f);
            }

            // [144차 P2-WF W58] StorageFooter — 63:297-305 (24,697,456,56):
            // WeightRow(y14 · 라벨 15.6/400 #8B949E + 값 GeistMono700 #58A6FF) +
            // ProgressBar(y48 h7.2 r3.6 · track #FFFFFF@0.078 · fill #58A6FF).
            // 값 데이터 — PlayerInventory 무게 데이터 부재(D7 위조 금지) → "—" 슬롯 표기 + fill 폭 0.
            // dumbbell 벡터 아이콘(63:300)은 글리프 근사 대상(P2 어휘) — 본 차 제외, 보고서 기록.
            var footer = new VisualElement { name = "wf-storage-footer" };
            footer.style.position = Position.Absolute;
            footer.style.left = 24f;
            footer.style.top = 697f;
            footer.style.width = 456f;
            footer.style.height = 56f;
            var weightRow = new VisualElement { name = "wf-storage-weight-row" };
            weightRow.style.position = Position.Absolute;
            weightRow.style.left = 0f;
            weightRow.style.top = 14f;
            weightRow.style.width = 456f;
            weightRow.style.height = 20f;
            weightRow.style.flexDirection = FlexDirection.Row;
            weightRow.style.alignItems = Align.Center;
            weightRow.style.justifyContent = Justify.SpaceBetween;
            var weightLabel = new Label(StorageFooterWeightLabel) { name = "wf-storage-weight-label", pickingMode = PickingMode.Ignore };
            weightLabel.style.fontSize = 15.6f;
            weightLabel.style.color = new StyleColor(UTKTheme.TextSub);
            weightRow.Add(weightLabel);
            _wfStorageWeightValue = new Label("—") { name = "wf-storage-weight-value", pickingMode = PickingMode.Ignore };
            _wfStorageWeightValue.style.fontSize = 15.6f;
            _wfStorageWeightValue.style.color = new StyleColor(UTKTheme.Accent);
            _wfStorageWeightValue.style.whiteSpace = WhiteSpace.NoWrap;
            weightRow.Add(_wfStorageWeightValue);
            footer.Add(weightRow);
            var track = new VisualElement { name = "wf-storage-progress-track" };
            track.style.position = Position.Absolute;
            track.style.left = 0f;
            track.style.top = 48f;
            track.style.width = 456f;
            track.style.height = 7.2f;
            track.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.078f));   // 63:304
            UTKTheme.ApplyRadius(track, 3.6f);
            _wfStorageProgressFill = new VisualElement { name = "wf-storage-progress-fill", pickingMode = PickingMode.Ignore };
            _wfStorageProgressFill.style.height = 7.2f;
            _wfStorageProgressFill.style.width = 0f;   // 무게 데이터 부재 — 진행 없음(위조 금지)
            _wfStorageProgressFill.style.backgroundColor = new StyleColor(UTKTheme.Accent);   // 63:305
            UTKTheme.ApplyRadius(_wfStorageProgressFill, 3.6f);
            track.Add(_wfStorageProgressFill);
            footer.Add(track);
            StoragePanelRoot.Add(footer);
        }

        // =====================================================================
        //  [144차 P2-WF] 동기화 — 탭/행 배지/슬롯 카운트·스트립/티어배지/스토리지 셀
        // =====================================================================

        /// <summary>베이스 상태 변화(클릭) 후 v1 오버레이 재동기화 — 다음 프레임 1회 스로틀.</summary>
        private void OnV1PanelPointer(PointerDownEvent evt)
        {
            // [144차 P2-WF W34] 행 클릭 추적 — 베이스 _selected는 private → 행명(ResultId)으로 미러링.
            for (var node = evt.target as VisualElement; node != null; node = node.parent)
            {
                string elementName = node.name ?? string.Empty;
                if (elementName.StartsWith(RowNamePrefix))
                {
                    _wfSelectedId = elementName.Substring(RowNamePrefix.Length);
                    break;
                }
            }
            ScheduleV1Refresh();
        }

        private void ScheduleV1Refresh()
        {
            if (_wfRefreshScheduled) return;
            _wfRefreshScheduled = true;
            schedule.Execute(() =>
            {
                _wfRefreshScheduled = false;
                RefreshV1Dynamic();
            }).StartingIn(1);
        }

        protected override void OnWindowOpen()
        {
            base.OnWindowOpen();   // 베이스 재구성(필터/행/매치/스토리지) 후 v1 오버레이 동기화
            RefreshV1Dynamic();
        }

        private void RefreshV1Dynamic()
        {
            if (!_wfStaticApplied || CraftPanelRoot == null) return;
            RefreshV1Tabs();
            RefreshV1SlotsAndResult();
            RefreshV1Rows();
            RefreshV1TierBadge();
            RefreshV1StorageCells();
        }

        /// <summary>[144차 P2-WF W10/W11/W12] 탭 스타일 — 활성 fill #58A6FF·라벨 #0B0E14 반전 / 비활성 투명+크롬 스트로크 / r7.2 / fs15.6.</summary>
        private void RefreshV1Tabs()
        {
            var bar = CraftPanelRoot.Q<VisualElement>("craft-bench-recipe-filters");
            if (bar == null) return;
            foreach (var button in bar.Query<Button>().ToList())
            {
                bool active = button.enabledSelf == false;   // 기반 활성 표현 = SetEnabled(false)
                button.style.backgroundColor = new StyleColor(active
                    ? (Color)UTKTheme.Accent
                    : new Color(0f, 0f, 0f, 0f));            // 63:27 / 63:29(#000000@0)
                var borderColor = new StyleColor(active ? UTKTheme.Accent : UTKTheme.BorderChrome);
                button.style.borderTopColor = borderColor;
                button.style.borderBottomColor = borderColor;
                button.style.borderLeftColor = borderColor;
                button.style.borderRightColor = borderColor;
                float borderWidth = active ? 0f : UTKTheme.BorderChromeWidth;
                button.style.borderTopWidth = borderWidth;
                button.style.borderBottomWidth = borderWidth;
                button.style.borderLeftWidth = borderWidth;
                button.style.borderRightWidth = borderWidth;
                UTKTheme.ApplyRadius(button, 7.2f);
                button.style.fontSize = 15.6f;               // W12 — 63:28 15.6
                button.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
                button.style.color = new StyleColor(active ? WfInkOnAccent : UTKColor.TextPrimary);
            }
        }

        /// <summary>[144차 P2-WF W18 완결/W19] 재료 슬롯 카운트("보유/요구")+티어 스트립, 결과 슬롯 스트립.</summary>
        private void RefreshV1SlotsAndResult()
        {
            var placed = ReadPlacements();
            bool hasMatch = TryFindMatchedRecipe(placed, out BenchRecipe matched);
            var inventory = PlayerInventory.Instance;
            for (int i = 0; i < SlotCount; i++)
            {
                var slot = CraftPanelRoot.Q<UTKSlot>("BenchSlot_" + i);
                if (slot == null) continue;
                string matId = ReadSlotItemId(i);
                if (string.IsNullOrEmpty(matId))
                {
                    slot.SetCount("");
                    slot.SetTierStrip(false, UTKTheme.TextSub);
                    continue;
                }
                var material = PlayerInventory.GetItemById(matId);
                slot.SetTierStrip(true, TierColor(material != null ? material.rarity : ItemRarity.Common));   // 63:40-50 실등급색
                if (hasMatch)
                {
                    int required = 0;
                    if (matched.MatIds != null)
                        foreach (string id in matched.MatIds)
                            if (id == matId) required++;
                    int held = inventory != null ? inventory.GetItemCount(matId) : 0;
                    slot.SetCount(held + "/" + required);   // 63:43 "12/10"(보유/요구) 13.2 GeistMono600 #8B949E
                }
                else
                {
                    slot.SetCount("");   // 미매칭 — 요구량 미판정(위조 금지)
                }
            }
            var resultSlot = CraftPanelRoot.Q<UTKSlot>("BenchResult");
            if (resultSlot != null)
            {
                if (hasMatch) resultSlot.SetTierStrip(true, TierColor(matched.rarity));   // 63:58
                else resultSlot.SetTierStrip(false, UTKTheme.TextSub);
            }
        }

        /// <summary>[144차 P2-WF W30/W31/W32/W33/W34] 레시피 행 — 상태 배경/스트로크, 아이콘 셀+티어스트립, 타이포, 우측 배지.</summary>
        private void RefreshV1Rows()
        {
            var book = CraftPanelRoot.Q<ScrollView>("craft-bench-recipe-list");
            if (book == null) return;
            string selectedId = ResolveSelectedId();
            foreach (var row in book.contentContainer.Children())
            {
                string rowName = row.name ?? string.Empty;
                if (!rowName.StartsWith(RowNamePrefix)) continue;
                string resultId = rowName.Substring(RowNamePrefix.Length);
                bool selected = resultId == selectedId;

                // [144차 P2-WF W30] 행 상태 — 63:68 활성 fill #FFFFFF@0.039+stroke #58A6FF 1.2 /
                // 63:78 비활성 무배경+stroke #30363D@0.502 · r7.2(기반 전 행 white 0.035 무테두리 교체).
                row.style.backgroundColor = new StyleColor(selected ? new Color(1f, 1f, 1f, 0.039f) : new Color(0f, 0f, 0f, 0f));
                var rowBorder = new StyleColor(selected ? UTKTheme.Accent : UTKTheme.BorderChrome);
                row.style.borderTopColor = rowBorder;
                row.style.borderBottomColor = rowBorder;
                row.style.borderLeftColor = rowBorder;
                row.style.borderRightColor = rowBorder;
                row.style.borderTopWidth = row.style.borderBottomWidth = 1.2f;
                row.style.borderLeftWidth = row.style.borderRightWidth = 1.2f;
                UTKTheme.ApplyRadius(row, 7.2f);

                // [144차 P2-WF W31] 행 아이콘 셀 — 63:70 52.8 fill #21262D@0.6 · stroke 크롬 1.2 · r9.6 + TierStrip(63:71).
                var icon = row.Q<VisualElement>("recipe-result-icon");
                if (icon != null)
                {
                    icon.style.backgroundColor = new StyleColor(new Color(0x21 / 255f, 0x26 / 255f, 0x2D / 255f, 0.6f));
                    UTKTheme.ApplyElementBorder(icon);
                    UTKTheme.ApplyRadius(icon, 9.6f);
                    var strip = icon.Q<VisualElement>("wf-row-tier-strip");
                    if (strip == null)
                    {
                        strip = new VisualElement { name = "wf-row-tier-strip", pickingMode = PickingMode.Ignore };
                        strip.style.position = Position.Absolute;
                        strip.style.left = 0f;
                        strip.style.right = 0f;
                        strip.style.top = 0f;
                        strip.style.height = 3.6f;
                        icon.Add(strip);
                    }
                    strip.style.backgroundColor = new StyleColor(TierColor(RarityOf(resultId)));
                }

                // [144차 P2-WF W32/W33] 행 타이포 — 이름 16.8/700(63:75), 부제 13.2/400 #8B949E + "필요 전투 레벨 n"
                // (63:76 — WeaponCraftDatabase.RequiredLevel 실데이터. 미발견 행은 베이스 재료 표기 유지=블라인드 체계 보존).
                VisualElement textColumn = null;
                foreach (var child in row.Children())
                {
                    string childName = child.name ?? string.Empty;
                    if (childName == "recipe-result-icon" || childName == "wf-row-badge") continue;
                    textColumn = child;
                    break;
                }
                if (textColumn != null)
                {
                    var rowLabels = textColumn.Query<Label>().ToList();
                    if (rowLabels.Count > 0)
                    {
                        rowLabels[0].style.fontSize = 16.8f;
                        rowLabels[0].style.unityFontStyleAndWeight = FontStyle.Bold;
                    }
                    if (rowLabels.Count > 1)
                    {
                        rowLabels[1].style.fontSize = 13.2f;
                        rowLabels[1].style.color = new StyleColor(UTKTheme.TextSub);
                        if (IsDiscoveredById(resultId))
                            rowLabels[1].text = "필요 전투 레벨 " + LevelOf(resultId);
                    }
                }

                // [144차 P2-WF W34] 우측 배지 — 63:77 "SELECTED"(#58A6FF) / 63:87/97 "LV. n"(13.2 GeistMono600).
                var badge = row.Q<Label>("wf-row-badge");
                if (badge == null)
                {
                    badge = new Label("") { name = "wf-row-badge", pickingMode = PickingMode.Ignore };
                    badge.style.fontSize = 13.2f;
                    badge.style.whiteSpace = WhiteSpace.NoWrap;
                    badge.style.marginLeft = 8f;
                    row.Add(badge);
                }
                if (selected)
                {
                    badge.text = "SELECTED";
                    badge.style.color = new StyleColor(UTKTheme.Accent);       // 63:77
                }
                else
                {
                    badge.text = "LV. " + LevelOf(resultId);
                    badge.style.color = new StyleColor(LvBadgeColor(RarityOf(resultId)));   // 63:87/97
                }
                ApplyMonoFont(badge, true);   // GeistMono600 → UITK Bold 매핑(StyleFigmaText 규약)
            }
        }

        /// <summary>[144차 P2-WF W44] TierBadge 동기화 — 기반 _detailRarity 텍스트(등급 데이터 소스)를 배지로 반영.</summary>
        private void RefreshV1TierBadge()
        {
            if (_wfTierBadge == null) return;
            var rarityLabel = DetailPanelRoot.Q<Label>("craft-bench-detail-rarity");
            string tierText = rarityLabel != null ? rarityLabel.text : null;
            if (string.IsNullOrEmpty(tierText))
            {
                _wfTierBadge.style.display = DisplayStyle.None;
                return;
            }
            ItemRarity parsed = ItemRarity.Common;
            if (!System.Enum.TryParse(tierText, true, out parsed)) parsed = ItemRarity.Common;
            Color tier = TierColor(parsed);
            _wfTierBadge.style.display = DisplayStyle.Flex;
            _wfTierBadge.style.backgroundColor = new StyleColor(new Color(tier.r, tier.g, tier.b, 0.122f));   // 63:125 fill@0.122
            var stroke = new StyleColor(tier);
            _wfTierBadge.style.borderTopColor = stroke;
            _wfTierBadge.style.borderBottomColor = stroke;
            _wfTierBadge.style.borderLeftColor = stroke;
            _wfTierBadge.style.borderRightColor = stroke;
            if (_wfTierBadgeLabel != null)
            {
                _wfTierBadgeLabel.text = tierText;
                _wfTierBadgeLabel.style.color = new StyleColor(tier);   // 63:126 텍스트 티어색
            }
        }

        /// <summary>[144차 P2-WF W56/W57] 스토리지 셀 — 카운트색 #8B949E + 빈 셀 원형 마크(28.8).</summary>
        private void RefreshV1StorageCells()
        {
            for (int i = 0; i < CraftBenchBaseUTK.StorageCellCount; i++)
            {
                var cell = StoragePanelRoot.Q<VisualElement>("craft-bench-storage-cell-" + i);
                if (cell == null) continue;
                var slot = cell.Q<UTKSlot>();
                if (slot != null)
                {
                    // [144차 P2-WF W56] 셀 카운트 — 63:198 13.2 GeistMono600 #8B949E(기반 SetCount(int) USS색 교체).
                    var count = slot.Q<Label>("Count");
                    if (count != null)
                    {
                        count.style.fontSize = 13.2f;
                        count.style.color = new StyleColor(UTKTheme.TextSub);
                    }
                    var occupiedMark = cell.Q<VisualElement>("wf-storage-empty-mark");
                    if (occupiedMark != null) occupiedMark.RemoveFromHierarchy();   // 실슬롯과 공존 금지
                }
                else
                {
                    // [144차 P2-WF W57] 빈 셀 마크 — 63:256 dashed 원형 아이콘 28.8 —
                    // UI Toolkit 점선 보더 미지원 → 실선 원 보더 근사(◇ 글리프 치환 동급 P2 어휘).
                    var mark = cell.Q<VisualElement>("wf-storage-empty-mark");
                    if (mark == null)
                    {
                        mark = new VisualElement { name = "wf-storage-empty-mark", pickingMode = PickingMode.Ignore };
                        mark.style.width = 28.8f;
                        mark.style.height = 28.8f;
                        UTKTheme.ApplyRadius(mark, 14.4f);
                        UTKTheme.ApplyElementBorder(mark, UTKTheme.TextSub, 1.2f);
                        mark.style.position = Position.Absolute;
                        mark.style.left = (CraftBenchBaseUTK.StorageCellSize - 28.8f) * 0.5f;
                        mark.style.top = (CraftBenchBaseUTK.StorageCellSize - 28.8f) * 0.5f;
                        cell.Add(mark);
                    }
                }
            }
        }

        // ── v1 레이어 공용 헬퍼 ──

        /// <summary>티어 스트립/배지색 — v1 프레임 실측 팔레트(63:40/45/50 + 그레이). Uncommon은 표본 부재 → 그레이(ShopWindowUTK 선례).</summary>
        private static Color TierColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return WfTierRare;
                case ItemRarity.Epic: return WfTierEpic;
                case ItemRarity.Legendary:
                case ItemRarity.Unique: return WfTierTop;
                default: return UTKTheme.TextSub;   // Common/Uncommon — #8B949E
            }
        }

        /// <summary>LV. 배지색 — 63:87/97 표본 2색(#8B949E/#D29922). 상위 티어(Epic+) → 앰버, 그 외 그레이(판단 기록).</summary>
        private static Color LvBadgeColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Epic:
                case ItemRarity.Legendary:
                case ItemRarity.Unique: return WfTierTop;
                default: return UTKTheme.TextSub;
            }
        }

        /// <summary>Geist Mono 로드(Figma 패밀리 계약) — 에셋 미보유 → UIFont 폴백(UTKSlot.GetGeistMonoFont 선례).</summary>
        private static Font _wfGeistMono;

        private static Font GeistMonoFont()
        {
            if (_wfGeistMono == null)
                _wfGeistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _wfGeistMono != null ? _wfGeistMono : UIFont.Load();
        }

        /// <summary>Geist Mono 적용 — Figma 600/700 → UITK Bold, 400 → Normal(StyleFigmaText 규약).</summary>
        private static void ApplyMonoFont(Label label, bool bold)
        {
            if (label == null) return;
            label.style.unityFont = GeistMonoFont();
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        }

        /// <summary>선택 행 판정 — 배치 매칭(베이스 RefreshMatch 미러) 우선, 없으면 클릭 추적값.</summary>
        private string ResolveSelectedId()
        {
            var placed = ReadPlacements();
            if (placed.Count > 0 && TryFindMatchedRecipe(placed, out BenchRecipe matched))
                return matched.ResultId;
            return _wfSelectedId;
        }

        private List<string> ReadPlacements()
        {
            var placed = new List<string>();
            for (int i = 0; i < SlotCount; i++)
            {
                string id = ReadSlotItemId(i);
                if (!string.IsNullOrEmpty(id)) placed.Add(id);
            }
            return placed;
        }

        /// <summary>슬롯 배치 재료 id — 아이콘 텍스처 인스턴스 역매핑(UTKTextureSafe 사본 캐시 경로 동일성 이용).</summary>
        private string ReadSlotItemId(int index)
        {
            var slot = CraftPanelRoot.Q<UTKSlot>("BenchSlot_" + index);
            if (slot == null) return null;
            StyleBackground background = slot.IconHost.style.backgroundImage;
            if (background.keyword == StyleKeyword.Null || background.value.texture == null) return null;
            Texture2D texture = background.value.texture;
            if (texture == null) return null;
            return TexToItemId(texture.GetInstanceID());
        }

        private string TexToItemId(int textureId)
        {
            if (_wfTexToItemId == null)
            {
                _wfTexToItemId = new Dictionary<int, string>();
                var seen = new HashSet<string>();
                foreach (var recipe in WeaponCraftDatabase.All)
                {
                    foreach (string matId in new[] { recipe.Mat1Id, recipe.Mat2Id })
                    {
                        if (string.IsNullOrEmpty(matId) || !seen.Add(matId)) continue;
                        var item = PlayerInventory.GetItemById(matId);
                        if (item == null) continue;
                        StyleBackground iconBackground = UTKTextureSafe.ToBackground(ItemIconDatabase.GetOrCreateIcon(item));
                        if (iconBackground.keyword == StyleKeyword.Null || iconBackground.value.texture == null) continue;
                        _wfTexToItemId[iconBackground.value.texture.GetInstanceID()] = matId;
                    }
                }
            }
            return _wfTexToItemId.TryGetValue(textureId, out string itemId) ? itemId : null;
        }

        /// <summary>배치 멀티셋 매칭 — 베이스 SameMultiset 미러( private 접근 없이 동일 판정 ).</summary>
        private bool TryFindMatchedRecipe(List<string> placed, out BenchRecipe matched)
        {
            foreach (var recipe in Recipes)
            {
                var need = new List<string>();
                if (recipe.MatIds != null)
                    foreach (string id in recipe.MatIds)
                        if (!string.IsNullOrEmpty(id)) need.Add(id);
                if (need.Count != placed.Count) continue;
                var pool = new List<string>(placed);
                bool ok = true;
                foreach (string item in need)
                {
                    int index = pool.FindIndex(candidate => candidate == item);
                    if (index < 0) { ok = false; break; }
                    pool.RemoveAt(index);
                }
                if (ok && pool.Count == 0) { matched = recipe; return true; }
            }
            matched = default;
            return false;
        }

        private WeaponCraftDatabase.WeaponRecipe RecipeOf(string resultId)
        {
            if (_wfRecipeById == null)
            {
                _wfRecipeById = new Dictionary<string, WeaponCraftDatabase.WeaponRecipe>();
                foreach (var recipe in WeaponCraftDatabase.All)
                    _wfRecipeById[recipe.ResultId] = recipe;
            }
            _wfRecipeById.TryGetValue(resultId, out WeaponCraftDatabase.WeaponRecipe found);
            return found;
        }

        private int LevelOf(string resultId)
        {
            var recipe = RecipeOf(resultId);
            return recipe.ResultId != null ? recipe.RequiredLevel : 0;
        }

        private ItemRarity RarityOf(string resultId)
        {
            var recipe = RecipeOf(resultId);
            return recipe.ResultId != null ? recipe.rarity : ItemRarity.Common;
        }

        private bool IsDiscoveredById(string resultId)
        {
            return RecipeDiscoverySystem.IsDiscovered(WeaponCraftDatabase.DisplayName(resultId));
        }
    }
}
