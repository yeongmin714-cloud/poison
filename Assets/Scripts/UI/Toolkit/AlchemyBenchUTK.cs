using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.UI;      // [144차 P2-ALCH] UIFont — GeistMono 에셋 부재 폴백 선례(LootWindowUTK/UTKSlot/WeaponForgeUTK)

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P18-C2 — 연금술 제작대 (마인크래프트식, UTK).
    /// 레시피 = HerbComboDatabase.AllCombos(GAME_DATA 조합법) 전체 나열 — 재료는 DB 약초.
    /// 제작 = CraftingHelper.CraftAlchemy(성공률 롤/재료 소모/실패 페널티 — 기존 로직 재사용).
    /// ⚠️ 알려진 간극: DB 약초(붉은줄기 등)는 인벤 약초(치유초 등)와 별개 체계 — DB 약초 아이템
    ///   획득 경로(채집/재배 데이터 패스) 연결 전까지는 재료 부족으로 실패할 수 있음(정상 경로).
    /// </summary>
    public class AlchemyBenchUTK : CraftBenchBaseUTK
    {
        private static AlchemyBenchUTK _instance;

        public static void Open()
        {
            if (_instance == null || _instance.panel == null)
                _instance = new AlchemyBenchUTK();
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) { Debug.LogWarning("[AlchemyBenchUTK] UIRoot 없음"); return; }
            if (_instance.parent == null) root.Add(_instance);
            _instance.Show();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // [144차 P2-ALCH A1/A6] 제목 — Figma 64:353 "물약 제작"(24/700). 기존 이모지 병기 문구는 Figma 무존재(W5 선례).
        // 재료 슬롯 3칸 실사용(v3 64:371/377/383 76.8×3 — 3제어 약물 레시피 존재, DrugInfo.Ingredients 2~3) → slotCount 3.
        private AlchemyBenchUTK() : base("물약 제작", 3, new Vector2(520f, 560f))
        {
            ApplyV3FigmaLayer();
            // [144차 P2-ALCH] 베이스 상태 변화(탭/행/슬롯/스토리지 재구성)는 모두 클릭 경유 —
            // 패널 루트 버블 2곳 + 오픈 훅으로 v3 오버레이(탭/카운트/스트립/행 부제)를 재동기화한다.
            CraftPanelRoot.RegisterCallback<PointerDownEvent>(OnV3PanelPointer);
            StoragePanelRoot.RegisterCallback<PointerDownEvent>(OnV3PanelPointer);
        }

        // [144차 P2-ALCH A2] Subtitle — 64:354 "BREWING"(기존 영문 서브타이틀 폐기).
        protected override string BenchSubtitle => "BREWING";
        protected override string CombinationHeading => "연금 레시피 슬롯";
        protected override string RecipeHeading => "제조 가능한 물약 목록";
        protected override string DetailHeading => "물약 정보";
        protected override string DetailDescriptionHeading => "물약 성분 및 제조 정보";
        protected override string StorageHeading => "연금술 재료";
        // [144차 P2-ALCH A20] "출처: 플레이어 인벤토리"는 Figma 무존재 게임 전용 요소 — 유지(§5 목록, 삭제 금지).
        protected override string StorageSource => "출처: 플레이어 인벤토리";
        protected override string CraftActionText => "물약 제조하기 (BREW)";
        // [144차 P2-ALCH A6] 판본 전용 분기 폐기 — v3는 재료 3칸 실사용(플레이스홀더 60/결과 72 오배칙 해소).
        protected override bool ShowThirdIngredientPlaceholder => false;
        // [144차 P2-ALCH A7] 성공확률 좌측 라벨 슬롯 — 64:399 "제조 성공 확률"(기반 W23 슬롯 주입).
        protected override string SuccessRateLabel => "제조 성공 확률";
        protected override IReadOnlyList<string> RecipeFilters => new[] { "전체", "공격성", "회복성", "마약성", "정신성", "물리성" };

        // [144차 P2-ALCH A7] 성공확률 값 데이터 — CraftAlchemy 실 롤 경로의 코드 진실치
        // (CraftingHelper.CraftAlchemy recipe.baseSuccessRate=60)로 산출 — 위조 아님(WeaponForge RateHint 선례).
        protected override string RateHint(BenchRecipe recipe)
            => "성공률 " + CraftingHelper.ComputeFinalCraftChance(60, recipe.rarity) + "%";

        protected override bool MatchesFilter(BenchRecipe recipe, string filter)
        {
            if (filter == "전체") return true;
            if (filter == "마약성") return recipe.ResultId.StartsWith("drug_", System.StringComparison.Ordinal);
            if (recipe.ResultId.StartsWith("drug_", System.StringComparison.Ordinal) || recipe.MatIds == null) return false;
            foreach (var id in recipe.MatIds)
            {
                var herb = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(id);
                if (string.IsNullOrEmpty(herb.id)) continue;
                switch (filter)
                {
                    case "공격성": if (herb.attribute == ProjectName.Core.Data.HerbAttribute.Attack) return true; break;
                    case "회복성": if (herb.attribute == ProjectName.Core.Data.HerbAttribute.Recovery) return true; break;
                    case "정신성": if (herb.attribute == ProjectName.Core.Data.HerbAttribute.Mental) return true; break;
                    case "물리성": if (herb.attribute == ProjectName.Core.Data.HerbAttribute.Physical) return true; break;
                }
            }
            return false;
        }

        protected override PlayerInventory.ItemData GetResultItemData(BenchRecipe recipe)
        {
            return recipe.ResultId.StartsWith("drug_", System.StringComparison.Ordinal)
                ? DrugEffectSystem.CreateDrugItem(ParseDrugStage(recipe.ResultId))
                : base.GetResultItemData(recipe);
        }

        protected override string IngredientDisplayName(string itemId)
        {
            var herb = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(itemId);
            return !string.IsNullOrEmpty(herb.id) ? herb.displayName : base.IngredientDisplayName(itemId);
        }

        private static int ParseDrugStage(string resultId)
        {
            return resultId != null && resultId.StartsWith("drug_", System.StringComparison.Ordinal) &&
                   int.TryParse(resultId.Substring("drug_".Length), out int stage) ? stage : -1;
        }

        private static string[] ResolveDrugIngredientIds(ProjectName.Core.Data.DrugInfo drug)
        {
            if (string.IsNullOrWhiteSpace(drug.Ingredients)) return null;
            string[] names = drug.Ingredients.Split('+');
            if (names.Length < 2 || names.Length > 3) return null;
            var ids = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i].Trim();
                var herb = ProjectName.Core.Data.HerbDatabase.GetHerbInfoByDisplayName(name);
                if (!string.IsNullOrEmpty(herb.id))
                {
                    ids[i] = herb.id;
                    continue;
                }

                // The 10-stage catalog references special raw materials not yet represented by
                // an inventory definition/harvest mapping. Do not silently substitute other herbs.
                return null;
            }
            return ids;
        }

        protected override IReadOnlyList<BenchRecipe> Recipes
        {
            get
            {
                var list = new List<BenchRecipe>();
                foreach (var kv in ProjectName.Core.Data.HerbComboDatabase.AllCombos)
                {
                    // Key = "id1_id2"; current authored IDs contain no underscores.
                    int sep = kv.Key.IndexOf('_');
                    if (sep <= 0) continue;
                    var pair = new[] { kv.Key.Substring(0, sep), kv.Key.Substring(sep + 1) };
                    var h1 = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(pair[0]);
                    var h2 = ProjectName.Core.Data.HerbDatabase.GetHerbInfo(pair[1]);
                    if (string.IsNullOrEmpty(h1.id) || string.IsNullOrEmpty(h2.id)) continue;
                    list.Add(new BenchRecipe
                    {
                        ResultId = kv.Key,
                        ResultName = kv.Value.resultName ?? "???",
                        MatIds = new[] { pair[0], pair[1] },
                        Note = kv.Value.effect,
                        rarity = ItemRarity.Common,
                    });
                }

                foreach (var drug in ProjectName.Core.Data.DrugDatabase.All)
                {
                    if (drug.Stage == 10) continue; // Stage 10 explicitly requires an undefined rare ingredient.
                    string[] ingredients = ResolveDrugIngredientIds(drug);
                    if (ingredients == null) continue;
                    list.Add(new BenchRecipe
                    {
                        ResultId = "drug_" + drug.Stage.ToString("D2"),
                        ResultName = drug.DrugName,
                        MatIds = ingredients,
                        Note = drug.Description + " · 중독성: " + drug.Addiction,
                        rarity = ItemRarity.Common,
                    });
                }
                return list;
            }
        }

        protected override bool TryCraft(BenchRecipe recipe, List<string> placedIds, out string message)
        {
            if (recipe.ResultId.StartsWith("drug_", System.StringComparison.Ordinal))
                return TryCraftDrug(recipe, out message);

            int sep = recipe.ResultId.IndexOf('_');
            if (sep <= 0) { message = "잘못된 레시피"; return false; }
            bool ok = CraftingHelper.CraftAlchemy(recipe.ResultId.Substring(0, sep), recipe.ResultId.Substring(sep + 1));
            message = ok ? "🟢 물약 제작 성공!" : "🔴 물약 제작 실패 — 재료가 없거나 손실되었다.";
            return ok;
        }

        // [144차 D6] AlchemyStationUTK 폐기 → 연금 성공 징글 계약(133차-P2)을 살아있는 Bench 경로로 승계
        // (구 AlchemyStationUTK 287행 위임 계승 — SFX_Acquire_Common 반2D, 실패 분기 무음 유지).
        protected override void OnCraftSuccess()
        {
            ProjectName.Systems.GatherFeedbackSystem.PlayAcquireJingle(0);
            base.OnCraftSuccess();
        }

        // =====================================================================
        //  [144차 P2-ALCH] v3(64:337) 고유 수리 레이어 — crafting.md A행 근거.
        //  기반(CraftBenchBaseUTK)은 공통 기하 스캐폴딩만 제공 — 기반 파일 수정 금지 계약.
        //  v3 판본 요소는 본 파일에서 공개 패널 루트에 주입/보정한다(WeaponForgeUTK v1 레이어 선례).
        //  =====================================================================

        // ── v3 프레임 티어 팔레트 — 64:372/378/384 스트립 실측(#1F6FEB/#BC8CFF/#D29922) + 그레이(64:385).
        private static readonly Color AlchTierRare = new Color(0x1F / 255f, 0x6F / 255f, 0xEB / 255f, 1f);
        private static readonly Color AlchTierEpic = new Color(0xBC / 255f, 0x8C / 255f, 0xFF / 255f, 1f);
        private static readonly Color AlchTierTop = new Color(0xD2 / 255f, 0x99 / 255f, 0x22 / 255f, 1f);     // Legendary/Unique
        private static readonly Color AlchRateGreen = new Color(0x56 / 255f, 0xD3 / 255f, 0x64 / 255f, 1f);   // 64:400 값색
        private static readonly Color AlchInkOnAccent = new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 1f); // 64:360 활성 라벨
        private const string RowNamePrefix = "craft-bench-recipe-row-";

        private bool _alchStaticApplied;
        private bool _alchRefreshScheduled;
        private Label _alchStorageCapacity;              // [144차 P2-ALCH A14 동반] SlotCapacity 우측 정렬
        private Label _alchStorageWeightValue;           // [144차 P2-ALCH A15] StorageFooter 값 라벨
        private VisualElement _alchStorageProgressFill;  // [144차 P2-ALCH A16] StorageFooter 진행바 fill
        private Dictionary<int, string> _alchTexToItemId; // [144차 P2-ALCH A8] 슬롯 아이콘 텍스처 → 재료 id
        private static Font _alchGeistMono;

        /// <summary>v3 정적 레이어 — 베이스 생성 직후 1회. 동적 상태(탭/행/슬롯 내용)는 RefreshV3Dynamic이 담당.</summary>
        private void ApplyV3FigmaLayer()
        {
            if (_alchStaticApplied) return;
            _alchStaticApplied = true;

            StyleV3CraftPanel();
            StyleV3DetailPanel();
            StyleV3StoragePanel();

            // [144차 P2-ALCH] 신규/변경 요소에 전역 폰트 재적용(단일 폰트 정책) 후 Geist Mono 재지정.
            ApplyUIToolkitFont(CraftPanelRoot);
            ApplyUIToolkitFont(DetailPanelRoot);
            ApplyUIToolkitFont(StoragePanelRoot);
            ApplyMonoFont(CraftPanelRoot.Q<Label>("craft-bench-success-rate"), true);   // A7 — GeistMono700
            ApplyMonoFont(_alchStorageCapacity, false);                                 // A14 동반 — GeistMono400
            ApplyMonoFont(_alchStorageWeightValue, true);                               // A15 — GeistMono700

            RefreshV3Dynamic();   // 탭/행 부제 등 생성 즉시 분 — 행/슬롯 내용은 오픈 훅이 재동기화
        }

        // ── CraftPanel(64:338) v3 정적 스타일 ──
        private void StyleV3CraftPanel()
        {
            // [144차 P2-ALCH A5] v3 섹션 체인 −1px — 조합섹션 y 152.4→151(64:367 ay238).
            var combination = CraftPanelRoot.Q<VisualElement>("craft-bench-combination-section");
            if (combination != null) combination.style.top = 151f;

            // [144차 P2-ALCH A7] 성공확률 좌측 라벨 — 64:399 "제조 성공 확률" 14.4/400 #8B949E @(19.2,157).
            // (기반 W23 슬롯 SuccessRateLabel 프로퍼티에 v3 문구 주입 — Cooking C14/WeaponForge W23 선례.
            //  기반 _status 안내 라벨은 게임 전용(§5 유지)과 별행.)
            if (combination != null)
            {
                var rateLabel = new Label(SuccessRateLabel) { name = "alch-success-rate-label", pickingMode = PickingMode.Ignore };
                rateLabel.style.position = Position.Absolute;
                rateLabel.style.left = 19.2f;
                rateLabel.style.top = 157f;
                rateLabel.style.fontSize = 14.4f;
                rateLabel.style.color = new StyleColor(UTKTheme.TextSub);
                combination.Add(rateLabel);
            }

            // [144차 P2-ALCH A7] 성공확률 값 — 64:400 14.4 GeistMono700 #56D364, y157(기반 13.2/#3FB950/158 교체).
            var rateValue = CraftPanelRoot.Q<Label>("craft-bench-success-rate");
            if (rateValue != null)
            {
                rateValue.style.fontSize = 14.4f;
                rateValue.style.top = 157f;
                rateValue.style.color = new StyleColor(AlchRateGreen);
            }

            // [144차 P2-ALCH] Divider — 64:401 (24,365,456,1.2) #30363D@0.502(레시피섹션 위 — v3 섹션 체인 −1px).
            var divider = new VisualElement { name = "alch-combination-divider", pickingMode = PickingMode.Ignore };
            divider.style.position = Position.Absolute;
            divider.style.left = 24f;
            divider.style.top = 365f;
            divider.style.width = 456f;
            divider.style.height = 1.2f;
            divider.style.backgroundColor = new StyleColor(UTKTheme.BorderChrome);
            CraftPanelRoot.Add(divider);

            // [144차 P2-ALCH A18] 레시피섹션 y 386.4→385 · h 419.2→420.2(64:402 ay472 h420.2).
            var recipeRegion = CraftPanelRoot.Q<VisualElement>("craft-bench-recipe-list-section");
            if (recipeRegion != null)
            {
                recipeRegion.style.top = 385f;
                recipeRegion.style.height = 420.2f;
            }

            // [144차 P2-ALCH] RecipesScroll h 392.2→393.6(64:404 h393.6; top 27 = 499-87-385 진실 유지).
            var book = CraftPanelRoot.Q<ScrollView>("craft-bench-recipe-list");
            if (book != null) book.style.height = 393.6f;

            // [144차 P2-ALCH A3] 탭바 호스트 — 64:358 TabContainer (24,96,456,36.2) · h 37.2→36.2.
            var tabBar = CraftPanelRoot.Q<VisualElement>("craft-bench-recipe-filters");
            if (tabBar != null) tabBar.style.height = 36.2f;

            // [144차 P2-ALCH A1/A2/A10/A13] 패널 헤더 타이포 — 제목 24/700(64:353/456/523), 영문 부제 14.4/ls1.2(64:354/457/524).
            StyleHeaderTypo(CraftPanelRoot.Q<VisualElement>("craft-bench-crafting-header"));
            StyleHeaderTypo(DetailPanelRoot.Q<VisualElement>("craft-bench-detail-header"));
            StyleHeaderTypo(StoragePanelRoot.Q<VisualElement>("craft-bench-storage-header"));
        }

        /// <summary>패널 헤더 타이포 보정 — 제목 라벨(이름 *-title) 24/700, 영문 부제(텍스트 매칭) 14.4/ls1.2 +
        /// v3 부제 문구 교체(A10/A13 — 기반 고정 영문 문자열은 슬롯 프로퍼티 부재 → 텍스트 치환).</summary>
        private void StyleHeaderTypo(VisualElement header)
        {
            if (header == null) return;
            foreach (var label in header.Query<Label>().ToList())
            {
                if (!string.IsNullOrEmpty(label.name) && label.name.EndsWith("-title"))
                {
                    label.style.fontSize = 24f;                              // A1/A9/A12 — fs 21.6→24
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;    // 700
                }
                else if (label.text == "SPECIFICATIONS")
                {
                    label.text = "POTIONS";                                  // A10 — 64:457
                    StyleHeaderSubtitle(label);
                }
                else if (label.text == "INVENTORY")
                {
                    label.text = "REAGENTS";                                 // A13 — 64:524
                    StyleHeaderSubtitle(label);
                }
                else if (label.text == "BREWING")
                {
                    StyleHeaderSubtitle(label);                              // A2 — 64:354
                }
            }
        }

        private static void StyleHeaderSubtitle(Label label)
        {
            label.style.fontSize = 14.4f;                              // fs 12→14.4(64:354/457/524)
            label.style.letterSpacing = 1.2f;                          // WeaponForge W6 선례 ls1.2
            label.style.unityFontStyleAndWeight = FontStyle.Normal;    // 500→UITK Normal 매핑
        }

        // ── DetailPanel(64:441) v3 정적 스타일 ──
        private void StyleV3DetailPanel()
        {
            // [144차 P2-ALCH A11 승계/W50] BioText — 64:507 15.6/400 #8B949E · h 69 · desc-로컬 y160(marginTop 12→14.8).
            // lineH 23.4는 Unity 6 IStyle 미지원(43차 CS1061 함정) — fs/높이만 적용, 행높이 보류 기록.
            var bio = DetailPanelRoot.Q<Label>("craft-bench-detail-description");
            if (bio != null)
            {
                bio.style.fontSize = 15.6f;
                bio.style.marginTop = 14.8f;
                bio.style.height = 69f;   // 64:507 BioText h 46→69(digest_v3 실측)
            }
        }

        // ── StoragePanel(64:508) v3 정적 스타일/요소 ──
        private void StyleV3StoragePanel()
        {
            // [144차 P2-ALCH A14] BadgeItem — 64:529-530 r4.8 fill #FFFFFF@0.039 · "보유 연금술 재료" 13.2/700 #F0F6FC
            // (Cooking C35/WeaponForge W52 선례 — Figma 배지는 신설, 기반 storage-source 라벨은 게임 전용(A20)으로 별행 유지).
            var statsBadge = new VisualElement { name = "alch-storage-stats-badge" };
            statsBadge.style.position = Position.Absolute;
            statsBadge.style.left = 24f;
            statsBadge.style.top = 96f;      // 64:529 StorageStats 행(ay183→96)
            statsBadge.style.height = 24.6f;
            statsBadge.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.039f));
            UTKTheme.ApplyRadius(statsBadge, 4.8f);
            statsBadge.style.paddingLeft = statsBadge.style.paddingRight = 9.6f;
            statsBadge.style.paddingTop = statsBadge.style.paddingBottom = 4.8f;
            var badgeText = new Label("보유 연금술 재료") { name = "alch-storage-section-label" };
            badgeText.style.fontSize = 13.2f;
            badgeText.style.unityFontStyleAndWeight = FontStyle.Bold;
            badgeText.style.color = new StyleColor(UTKTheme.TextMain);   // #F0F6FC
            statsBadge.Add(badgeText);
            StoragePanelRoot.Add(statsBadge);

            // [144차 P2-ALCH A14 동반] SlotCapacity — 64:531 "사용 슬롯: n / m" 14.4 GeistMono400 #8B949E 우측 정렬(ay186→99).
            // 기반 라벨이 (24,96) 좌측 배치 → 신설 배지와 충돌하므로 우측으로 이동(WeaponForge W53 선례).
            _alchStorageCapacity = StoragePanelRoot.Q<Label>("craft-bench-storage-capacity");
            if (_alchStorageCapacity != null)
            {
                _alchStorageCapacity.style.left = StyleKeyword.Auto;
                _alchStorageCapacity.style.right = 24f;
                _alchStorageCapacity.style.top = 99f;
                _alchStorageCapacity.style.fontSize = 14.4f;
                _alchStorageCapacity.style.color = new StyleColor(UTKTheme.TextSub);
                _alchStorageCapacity.style.whiteSpace = WhiteSpace.NoWrap;
            }

            // [144차 P2-ALCH A15/A16] StorageFooter — 64:662-670 (24,697,456,56):
            // WeightRow(y14 · "재료 적재 중량" 15.6/400 #8B949E + 값 GeistMono700 #58A6FF) +
            // ProgressBar(y48 h7.2 r3.6 · track #FFFFFF@0.078 · fill #58A6FF).
            // 값 데이터 — PlayerInventory 무게 데이터 부재(D7 위조 금지) → "—" 슬롯 표기 + fill 폭 0
            // (Cooking C38/C39 · WeaponForge W58 선례). dumbbell 벡터 아이콘(64:665)은 글리프 근사 대상 — 본 차 제외 기록.
            var footer = new VisualElement { name = "alch-storage-footer" };
            footer.style.position = Position.Absolute;
            footer.style.left = 24f;
            footer.style.top = 697f;
            footer.style.width = 456f;
            footer.style.height = 56f;
            var weightRow = new VisualElement { name = "alch-storage-weight-row" };
            weightRow.style.position = Position.Absolute;
            weightRow.style.left = 0f;
            weightRow.style.top = 14f;
            weightRow.style.width = 456f;
            weightRow.style.height = 20f;
            weightRow.style.flexDirection = FlexDirection.Row;
            weightRow.style.alignItems = Align.Center;
            weightRow.style.justifyContent = Justify.SpaceBetween;
            var weightLabel = new Label("재료 적재 중량") { name = "alch-storage-weight-label", pickingMode = PickingMode.Ignore };
            weightLabel.style.fontSize = 15.6f;
            weightLabel.style.color = new StyleColor(UTKTheme.TextSub);
            weightRow.Add(weightLabel);
            _alchStorageWeightValue = new Label("—") { name = "alch-storage-weight-value", pickingMode = PickingMode.Ignore };
            _alchStorageWeightValue.style.fontSize = 15.6f;
            _alchStorageWeightValue.style.color = new StyleColor(UTKTheme.Accent);
            _alchStorageWeightValue.style.whiteSpace = WhiteSpace.NoWrap;
            weightRow.Add(_alchStorageWeightValue);
            footer.Add(weightRow);
            var track = new VisualElement { name = "alch-storage-progress-track" };
            track.style.position = Position.Absolute;
            track.style.left = 0f;
            track.style.top = 48f;
            track.style.width = 456f;
            track.style.height = 7.2f;
            track.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.078f));   // 64:669
            UTKTheme.ApplyRadius(track, 3.6f);
            _alchStorageProgressFill = new VisualElement { name = "alch-storage-progress-fill", pickingMode = PickingMode.Ignore };
            _alchStorageProgressFill.style.height = 7.2f;
            _alchStorageProgressFill.style.width = 0f;   // 무게 데이터 부재 — 진행 없음(위조 금지)
            _alchStorageProgressFill.style.backgroundColor = new StyleColor(UTKTheme.Accent);   // 64:670
            UTKTheme.ApplyRadius(_alchStorageProgressFill, 3.6f);
            track.Add(_alchStorageProgressFill);
            footer.Add(track);
            StoragePanelRoot.Add(footer);
        }

        // =====================================================================
        //  [144차 P2-ALCH] 동기화 — 탭/슬롯 카운트·스트립/행 부제
        //  =====================================================================

        /// <summary>베이스 상태 변화(클릭) 후 v3 오버레이 재동기화 — 다음 프레임 1회 스로틀(WeaponForge 선례).</summary>
        private void OnV3PanelPointer(PointerDownEvent evt) => ScheduleV3Refresh();

        private void ScheduleV3Refresh()
        {
            if (_alchRefreshScheduled) return;
            _alchRefreshScheduled = true;
            schedule.Execute(() =>
            {
                _alchRefreshScheduled = false;
                RefreshV3Dynamic();
            }).StartingIn(1);
        }

        protected override void OnWindowOpen()
        {
            base.OnWindowOpen();   // 베이스 재구성(필터/행/매치/스토리지) 후 v3 오버레이 동기화
            RefreshV3Dynamic();
        }

        private void RefreshV3Dynamic()
        {
            if (!_alchStaticApplied || CraftPanelRoot == null) return;
            RefreshV3Tabs();
            RefreshV3SlotsAndResult();
            RefreshV3RowSubtitles();
        }

        /// <summary>[144차 P2-ALCH A3/A4] 탭 스타일 — 64:359-366 활성 fill #58A6FF·라벨 #0B0E14(폭 70) /
        /// 비활성 투명+크롬 스트로크·라벨 #8B949E(폭 72.4) · r7.2 · fs14.4/700(양태 모두 700 — 64:360/362) · gap 4.8 · h 36.2.
        /// 6탭 합산 70+5×72.4+5×4.8=456 진실.</summary>
        private void RefreshV3Tabs()
        {
            var bar = CraftPanelRoot.Q<VisualElement>("craft-bench-recipe-filters");
            if (bar == null) return;
            foreach (var button in bar.Query<Button>().ToList())
            {
                bool active = button.enabledSelf == false;   // 기반 활성 표현 = SetEnabled(false)
                button.style.width = active ? 70f : 72.4f;   // 64:359 활성 70 / 64:361 비활성 72.4
                button.style.height = 36.2f;                 // 64:358 TabContainer h(기반 37.2 교체)
                button.style.marginRight = 4.8f;             // 64:358 gap 4.8(기반 4 교체)
                button.style.backgroundColor = new StyleColor(active
                    ? (Color)UTKTheme.Accent
                    : new Color(0f, 0f, 0f, 0f));            // 64:359 / 64:361(#000000@0)
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
                button.style.fontSize = 14.4f;               // A4 — 64:344 14.4(기반 미설정 17 교체)
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
                button.style.color = new StyleColor(active ? AlchInkOnAccent : UTKTheme.TextSub);   // 64:360/362
            }
        }

        /// <summary>[144차 P2-ALCH A8/W18-W19 승계] 재료 슬롯 카운트("보유/요구" — 64:360 "12/12")+티어 스트립(배치 실등급색),
        /// 결과 슬롯 스트립(매칭 레시피 등급 — 64:394).</summary>
        private void RefreshV3SlotsAndResult()
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
                slot.SetTierStrip(true, AlchTierColor(material != null ? material.rarity : ItemRarity.Common));   // 64:372 등
                if (hasMatch)
                {
                    int required = 0;
                    if (matched.MatIds != null)
                        foreach (string id in matched.MatIds)
                            if (id == matId) required++;
                    int held = inventory != null ? inventory.GetItemCount(matId) : 0;
                    slot.SetCount(held + "/" + required);   // 64:360 "12/12"(보유/요구) 13.2 GeistMono600 #8B949E
                }
                else
                {
                    slot.SetCount("");   // 미매칭 — 요구량 미판정(위조 금지)
                }
            }
            var resultSlot = CraftPanelRoot.Q<UTKSlot>("BenchResult");
            if (resultSlot != null)
            {
                if (hasMatch) resultSlot.SetTierStrip(true, AlchTierColor(matched.rarity));
                else resultSlot.SetTierStrip(false, UTKTheme.TextSub);
            }
        }

        /// <summary>[144차 P2-ALCH A19] 레시피 부제 — 64:414 "필요 연금술 레벨 35" 13.2/400 #8B949E 체계.
        /// 기반 DescribeMats(재료 나열) 대체 — 연금 체인(HerbInfo/HerbComboResult/DrugInfo)에 레벨 필드가 없어
        /// (2026-10-31 실측) 실데이터 확보 전까지 "필요 연금술 레벨 —" 슬롯 표기(위조 금지 D7).
        /// 상세 패널 "필요 재료:" 행은 기반 RefreshDetail이 유지 — 재료 정보 소실 아님.</summary>
        private void RefreshV3RowSubtitles()
        {
            var book = CraftPanelRoot.Q<ScrollView>("craft-bench-recipe-list");
            if (book == null) return;
            foreach (var row in book.contentContainer.Children())
            {
                string rowName = row.name ?? string.Empty;
                if (!rowName.StartsWith(RowNamePrefix)) continue;
                VisualElement textColumn = null;
                foreach (var child in row.Children())
                {
                    string childName = child.name ?? string.Empty;
                    if (childName == "recipe-result-icon") continue;
                    textColumn = child;
                    break;
                }
                if (textColumn == null) continue;
                var rowLabels = textColumn.Query<Label>().ToList();
                if (rowLabels.Count < 2) continue;
                rowLabels[1].style.fontSize = 13.2f;
                rowLabels[1].style.color = new StyleColor(UTKTheme.TextSub);
                rowLabels[1].text = "필요 연금술 레벨 —";   // 레벨 실데이터 부재 — 슬롯 표기(D7)
            }
        }

        // ── v3 레이어 공용 헬퍼 ──

        /// <summary>티어 스트립색 — v3 프레임 실측 팔레트(64:372/378/384 + 그레이 64:385):
        /// Rare #1F6FEB · Epic #BC8CFF · Top #D29922 · Common #8B949E. Uncommon은 표본 부재 → 그레이(ShopWindowUTK 선례).</summary>
        private static Color AlchTierColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return AlchTierRare;
                case ItemRarity.Epic: return AlchTierEpic;
                case ItemRarity.Legendary:
                case ItemRarity.Unique: return AlchTierTop;
                default: return UTKTheme.TextSub;   // Common/Uncommon — #8B949E
            }
        }

        /// <summary>Geist Mono 로드(Figma 패밀리 계약) — 에셋 미보유 → UIFont 폴백(WeaponForgeUTK 선례).</summary>
        private static Font GeistMonoFont()
        {
            if (_alchGeistMono == null)
                _alchGeistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _alchGeistMono != null ? _alchGeistMono : UIFont.Load();
        }

        /// <summary>Geist Mono 적용 — Figma 600/700 → UITK Bold, 400 → Normal(StyleFigmaText 규약).</summary>
        private static void ApplyMonoFont(Label label, bool bold)
        {
            if (label == null) return;
            label.style.unityFont = GeistMonoFont();
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
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

        /// <summary>슬롯 배치 재료 id — 아이콘 텍스처 인스턴스 역매핑(UTKTextureSafe 사본 캐시 경로 동일성 이용, WeaponForge 선례).</summary>
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
            if (_alchTexToItemId == null)
            {
                _alchTexToItemId = new Dictionary<int, string>();
                var seen = new HashSet<string>();
                foreach (var recipe in Recipes)
                {
                    if (recipe.MatIds == null) continue;
                    foreach (string matId in recipe.MatIds)
                    {
                        if (string.IsNullOrEmpty(matId) || !seen.Add(matId)) continue;
                        var item = PlayerInventory.GetItemById(matId);
                        if (item == null) continue;
                        StyleBackground iconBackground = UTKTextureSafe.ToBackground(ItemIconDatabase.GetOrCreateIcon(item));
                        if (iconBackground.keyword == StyleKeyword.Null || iconBackground.value.texture == null) continue;
                        _alchTexToItemId[iconBackground.value.texture.GetInstanceID()] = matId;
                    }
                }
            }
            return _alchTexToItemId.TryGetValue(textureId, out string itemId) ? itemId : null;
        }

        /// <summary>배치 멀티셋 매칭 — 베이스 SameMultiset 미러(private 접근 없이 동일 판정, WeaponForge 선례).</summary>
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

        private static bool TryCraftDrug(BenchRecipe recipe, out string message)
        {
            var inventory = PlayerInventory.Instance;
            int stage = ParseDrugStage(recipe.ResultId);
            if (inventory == null || stage < 1 || stage > 9 || recipe.MatIds == null || recipe.MatIds.Length < 2)
            {
                message = "약물 레시피 또는 인벤토리를 사용할 수 없습니다.";
                return false;
            }

            var resultItem = DrugEffectSystem.CreateDrugItem(stage);
            if (resultItem == null)
            {
                message = "약물 데이터가 없습니다.";
                return false;
            }

            var required = new Dictionary<string, int>();
            foreach (string id in recipe.MatIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                required.TryGetValue(id, out int count);
                required[id] = count + 1;
            }
            foreach (var pair in required)
            {
                if (inventory.GetItemCount(pair.Key) < pair.Value)
                {
                    message = "재료 부족 — " + ProjectName.Core.Data.HerbDatabase.GetHerbInfo(pair.Key).displayName;
                    return false;
                }
            }

            foreach (var pair in required)
                if (!inventory.RemoveItem(pair.Key, pair.Value))
                {
                    // The inventory was prevalidated; return any consumed items if a removal unexpectedly fails.
                    foreach (var rollback in required) inventory.AddItem(PlayerInventory.GetItemById(rollback.Key), rollback.Value);
                    message = "재료를 인벤토리에서 제거할 수 없습니다.";
                    return false;
                }

            if (!inventory.AddItem(resultItem, 1))
            {
                foreach (var rollback in required) inventory.AddItem(PlayerInventory.GetItemById(rollback.Key), rollback.Value);
                message = "인벤토리가 가득 찼습니다. 재료는 반환했습니다.";
                return false;
            }

            CraftingHelper.NotifyCraftSucceeded(resultItem.id);
            message = "🟢 " + resultItem.displayName + " 제조 완료! (중독성: " + ProjectName.Core.Data.DrugDatabase.GetByStage(stage)?.Addiction + ")";
            return true;
        }
    }
}
