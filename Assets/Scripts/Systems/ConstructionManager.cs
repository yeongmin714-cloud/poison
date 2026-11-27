using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase O8 (C-O8-01/02/05): 성 영지 건설 관리자 — HOUSE_BUILDING.md 벤치마크.
    /// 흐름: 설계도 선택 → 위치 검증(영지 경계/겹침/지형) → 골드 소비 → 시공 진행 → 완성.
    /// 벤치 원칙: "검증 실패 시 아이템 유지(소비 없음)", 성공 시에만 소비 — 골드 싱크.
    /// Awake 미실행 환경 대응 — FindAnyObjectByType 폴백(O7 확립 패턴).
    /// [D4-A] 음식점(bp_restaurant) 전용 분기 추가: 마을 반경 검증 + 마을당 1개 제한 +
    ///   영주 허가세(restaurant_permit → TerritoryState.territoryGold 적립) + 재료(나무/돌) 소비.
    ///   음식점 실물 시각은 RestaurantPropBuilder(StructureCompleted 구독)가 담당한다.
    /// </summary>
    public class ConstructionManager : MonoBehaviour
    {
        public static ConstructionManager Instance { get; private set; }

        /// <summary>[D4-A] 음식점 설계도 효과 id — BlueprintData.bp_restaurant.effectId와 일치(실측).</summary>
        public const string RestaurantBlueprintEffectId = "restaurant";

        /// <summary>[125차] 훈련소 설계도 효과 id — BlueprintData.bp_traininghall.effectId와 일치(실측).</summary>
        public const string TrainingHallBlueprintEffectId = "training_hall";

        /// <summary>[D4-A] 건설 재료 — 나무 아이템 id. PlayerInventory.Mat_Wood 실측: id "mat_wood"(통나무), ItemCategory.Material.</summary>
        public const string MaterialWoodItemId = "mat_wood";

        /// <summary>[D4-A] 건설 재료 — 돌 아이템 id. PlayerInventory.Mat_Stone 실측: id "mat_stone"(석재), ItemCategory.Material.</summary>
        public const string MaterialStoneItemId = "mat_stone";

        /// <summary>[90차 A1] 건설 재료 — 철광석 아이템 id. PlayerInventory.IronOre 실측: id "iron_ore"(철광석), ItemCategory.Material.</summary>
        public const string MaterialIronItemId = "iron_ore";

        /// <summary>[90차 A1] 건설 재료 — 가죽 아이템 id. PlayerInventory.BoarLeather 실측: id "mat_boar_leather"(멧돼지 가죽), ItemCategory.Material.</summary>
        public const string MaterialLeatherItemId = "mat_boar_leather";

        /// <summary>[90차 A1] 건설 재료 — 수정석 아이템 id. PlayerInventory.CrystalShard 실측: id "crystal_shard"(수정석).
        /// Figma '마력석' 라벨은 실측 아이템 '수정석'(crystal_shard) 단일소스로 통일 — 원장 단일소스 우선 원칙.</summary>
        public const string MaterialManaItemId = "crystal_shard";

        /// <summary>
        /// [90차 A4] 성 내부 반경 (m) — 음식점 실내 배치 금지 판정 반경. 실측 근거 2건:
        ///  ① 월드 성 실물 — TerritoryBuilder.NormalizeCastleScale이 성 GLB를 XZ 최대 반경 25m로 정규화(실측,
        ///     폴백 Cube도 30×15×30 = 반경 15m) → 영지 중심 25m 내 = 성 실물 부지(실내/성벽 내부) 취급.
        ///  ② 실내 씬 — InteriorLayoutDefinition.CreateKeepOctagon 로비 팔각 바닥 정점 최대 반경
        ///     √(30²+8²) = √964 ≈ 31.05m. 단, 실내 씬은 '안이 더 넓은' 공간이라 월드 성 부지보다 크며,
        ///     마을(성 중심에서 38~46m 오프셋 배치 — VillagePlacementSystem 실측) 배치 여유를 위해
        ///     권장 상한 25m 이하를 채택. 25m &lt; 마을 중심 최소 이격 38m → 마을 중심 배치와 충돌 없음.
        /// </summary>
        public const float CastleInteriorRadius = 25f;

        /// <summary>[D4-A] 음식점 마을 배치 마진 (m) — 마을 중심 반경 + 이 값 내에서만 배치 허용.</summary>
        public const float VillagePlacementMargin = 5f;

        /// <summary>건설 구조물 1건 (설계도+위치+진행).</summary>
        public struct StructureEntry
        {
            public string structureId;
            public string blueprintId;
            public string territoryId;
            public Vector3 pos;
            public float progress;   // 0~1
            public bool isComplete;
            public long completedAtUnix;
        }

        /// <summary>완성 시 발화 (연출/효과 구독용).</summary>
        public static event System.Action<StructureEntry> StructureCompleted;

        /// <summary>
        /// [154차 P4] 음식점 배치 확정 성공 발화 — (nation, villageIndex, permitFee).
        /// Systems→UI 단방향(정적 이벤트 계약 — FarmPlot.FarmStatusBroadcast 선례). 음식점 분기 성공 리턴 직전
        /// (구조물 등록 후)에 한해 발화 — 비음식점 배치/실패 경로는 발화 없음. UI 구독: RestaurantInitialPriceModalUTK.
        /// </summary>
        public static event System.Action<NationType, int, int> RestaurantPlacementSucceeded;

        /// <summary>영지 반경 기본값 — TerritoryDatabase에 별도 반경 API 없음(주석: 향후 데이터화).</summary>
        public const float TerritoryRadius = 60f;

        private readonly List<StructureEntry> _structures = new List<StructureEntry>();

        // [142차 F] 시공 사이트 게이지 — 벌목/채광과 동일 어휘(WorkProgressBar 수동 모드)를
        // 건설 부지 위에 표시해 "모두 지어질 때까지" 진행을 보여준다(사용자 지시).
        private readonly System.Collections.Generic.Dictionary<string, WorkProgressBar> _siteGauges =
            new System.Collections.Generic.Dictionary<string, WorkProgressBar>();
        private readonly System.Collections.Generic.Dictionary<string, GameObject> _siteAnchors =
            new System.Collections.Generic.Dictionary<string, GameObject>();
        private static readonly Color SiteGaugeColor = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 1f);   // 채널링 블루 동일 어휘

        /// <summary>[150차 P3] 건설 게이지 높이 — 지면(entry.pos.y) 기준 게이지 중심 오프셋.
        /// 기존 2.5m는 구조물에 가림 실측 → 구조물 예상 최고점 위로 상향. 정확한 구조물 최대 높이 근거
        /// (Figma/카탈로그) 미확보로 5.5m 상수 적용 — Play 관측으로 미세조정 예정.</summary>
        private const float SiteGaugeBarHeightM = 5.5f;

        public IReadOnlyList<StructureEntry> Structures => _structures;

        /// <summary>[142차 F] 시공 게이지 부착 — 사이트 앵커 GO 생성(완료/제거/해체 시 함께 정리).
        /// WorkProgressBar 수동 모드 — 벌목/채광 채널링 바와 동일 어휘(빌보딩·트랙/채움 베이크).</summary>
        private void AttachSiteGauge(StructureEntry entry)
        {
            if (!Application.isPlaying) return;   // [142차 F] EditMode(테스트) 무동작 — GameObject 생성/Destroy 오염 차단(ConstructionTests 보호)
            var anchor = new GameObject($"ConstructionSite_{entry.structureId}");
            anchor.transform.position = new Vector3(entry.pos.x, entry.pos.y + 0.05f, entry.pos.z);
            var bar = WorkProgressBar.ShowManual(anchor.transform, SiteGaugeColor);
            // [150차 P3] 앵커 높이 보정 — 게이지를 구조물 예상 최고점 위로(기존 2.5m → SiteGaugeBarHeightM 5.5m).
            // ShowManual 로컬 오프셋(2.5m)을 건설 게이지 한정으로 덮어써 Show(채널링) 경로는 무영향.
            bar.transform.localPosition = new Vector3(0f, SiteGaugeBarHeightM, 0f);
            bar.SetProgress01(entry.progress);
            _siteAnchors[entry.structureId] = anchor;
            _siteGauges[entry.structureId] = bar;
            // [142차 F2] 부착 진단 — 미표출 재보고(142차) 지상 증거 수집: 위치/높이/자식 수.
            Debug.Log($"[142차 F] 건설 게이지 부착 — site={entry.structureId} pos=({entry.pos.x:F1},{entry.pos.y:F1},{entry.pos.z:F1}) " +
                      $"barY={bar.transform.position.y:F1} children={bar.GetComponentsInChildren<Renderer>().Length}");
            // [149차 P4] 미표출 진단 계측 강화 — 렌더러/머티리얼/텍스처/스케일 상태 덤프(표시 로직 무변경,
            //   원인 확정용: 가림=isVisible false / Build 실패=matNull / 텍스처 부재=texNull 판별 근거).
            foreach (var r in bar.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                bool matNull = mats == null || mats.Length == 0;
                if (!matNull)
                    foreach (var m in mats)
                        if (m == null) { matNull = true; break; }
                bool texNull = false;
                if (!matNull)
                    foreach (var m in mats)
                        if (m != null && m.mainTexture == null) { texNull = true; break; }
                Debug.Log($"[149차][P4] 게이지 렌더 상태 — {r.name} enabled={r.enabled} matNull={matNull} texNull={texNull} scale={r.transform.lossyScale}");
            }
        }

        /// <summary>[142차 F] 시공 게이지 갱신 — 진행값 대입. 완료 구조물은 즉시 정리(멱등).</summary>
        private void UpdateSiteGauge(StructureEntry s)
        {
            if (_siteGauges.TryGetValue(s.structureId, out var bar) && bar != null)
                bar.SetProgress01(s.progress);
            if (s.isComplete)
                CleanupSiteGauge(s.structureId);
        }

        /// <summary>[142차 F] 게이지+앵커 정리 — 완료/제거/해체 공용(멱등 — 중복 호출 무영향).</summary>
        private void CleanupSiteGauge(string structureId)
        {
            if (_siteGauges.TryGetValue(structureId, out var bar))
            {
                if (bar != null) bar.Close();
                _siteGauges.Remove(structureId);
            }
            if (_siteAnchors.TryGetValue(structureId, out var anchor))
            {
                if (anchor != null) Destroy(anchor);
                _siteAnchors.Remove(structureId);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── 배치 (설계도 사용) ──

        /// <summary>
        /// 설계도 배치 — 순서: 정의 조회 → 위치 검증 → 지형 검증 → 골드 소비 → 시공 등록.
        /// 벤치 원칙: 검증 실패 시 소비 없음(사유 메시지 반환).
        /// </summary>
        public string TryPlace(string territoryId, string blueprintId, Vector3 pos)
        {
            if (!BlueprintData.TryGet(blueprintId, out var def))
                return "알 수 없는 설계도";

            // [152차 P1] 실내 씬 분기 — 실내(Core 공용 UITransitionState.IndoorActive)면 territoryId를
            // 실내 전환 시 보존된 성 영지 키로 재해석한다. 실측: 실내 룸은 씬 원점 부근(빌더 원점 기준,
            // 로비 팔각 최대 반경 약 31m)이라 호출부(ConstructionHologram — ResolveTerritoryAt)의
            // 위치 판정이 항상 황제국(Empire_1, 중심 (0,0,0))으로 기울어 East_01 등 타 영지 성
            // 실내에서도 오귀속되고, Empire_1에 훈련장이 이미 있으면 실내 배치가 전부 거부된다.
            // 음식점 실내 금지 규약(하단 IsIndoorPlacement, [90차 A4])은 불변 — 본 분기는 훈련소 등
            // 기존 비음식점 시설 흐름만 보장한다. 기존 판정 체인 순서/시그니처 보존(증분 삽입).
            bool indoorScene = IsIndoorScene();
            if (indoorScene)
                territoryId = TryPlaceResolvesTerritoryForIndoorScene(
                    territoryId, ProjectName.Core.UITransitionState.IndoorCastleTerritoryKey, indoorScene);

            // 영지 정의 조회 (nation None = 미등록 키)
            var territoryDb = TerritoryDatabase.Instance;
            var region = territoryDb != null ? territoryDb.GetDefinition(territoryId) : new TerritoryDefinition();
            if (region.nation == NationType.None)
                return "영지 없음";

            // 위치 검증 (순수)
            // [152차 P1] 실내 분기 — 영지 경계(월드 중심 반경)는 실내 로컬 좌표와 무의미해 우회하고
            // 겹침 검증만 수행한다(보존 키 수리로 월드 중심 vs 실내 좌표 비교가 성립하면 신규 거부
            // "영지 경계 밖"이 생기는 것 차단). 실외는 기존 검증 그대로.
            if (indoorScene)
            {
                if (!ValidateIndoorPlacementOverlap(pos, def.footprintRadius, GetFootprints()))
                    return "기존 건설물과 겹침";
            }
            else if (!BlueprintData.ValidatePlacement(region.worldPosition, TerritoryRadius, pos, def.footprintRadius, GetFootprints()))
                return "영지 경계 밖 또는 기존 건설물과 겹침";

            // 지형 검증 (런타임 전용 — 에디터 테스트에서는 Raycast 회피)
            if (Application.isPlaying && !BlueprintData.TerrainFlatCheck(pos))
                return "경사진 지형";

            // ── [D4-A] 음식점 전용 사전 검증 (검증 실패 시 골드/재료 전부 소비 없음 — 벤치 원칙) ──
            var stats = PlayerStats.Instance ?? FindAnyObjectByType<PlayerStats>();
            int permitFee = 0;
            NationType restaurantNation = NationType.None;
            int restaurantVillageIndex = -1;

            if (def.effectId == RestaurantBlueprintEffectId)
            {
                // ⓪ [90차 A4] 실내 배치 금지 — 분기 최상단. 성(영지 중심) 내부 반경 25m 내 거부.
                //    마을은 성 중심에서 38~46m 오프셋 배치(VillagePlacementSystem 실측)라 정상 마을 배치와 충돌 없음.
                if (IsIndoorPlacement(pos))
                    return "음식점은 실내에 배치할 수 없습니다 — 마을에서 건설하세요";

                // ① pos가 어떤 마을 중심 반경(radius+5m) 내 최근접 마을 조회 (순수 결정론)
                if (!TryFindRestaurantVillage(pos, out restaurantNation, out restaurantVillageIndex))
                    return $"마을 반경 밖 (마을 중심 반경+{VillagePlacementMargin:0}m 내에서만 건설 가능)";

                // ② 그 마을에 음식점 미건설 — 마을당 1개 제한 (VillageDrugState 연계)
                if (VillageDrugState.GetSnapshot(restaurantNation, restaurantVillageIndex).restaurantBuilt)
                    return "이미 음식점이 있는 마을";

                // ②' [92차 결함1] 시공 중 재배치 가드 — restaurantBuilt는 완성 시점에만 true라
                //     시공 60s 창에서 재배치가 허용됨(중복 등록 → 완료 로그 스팸 트리거, 결함 1 연쇄).
                //     [88차] 스팟 점유 판정(RestaurantPlacementSpotSystem ①')과 동일 기준 —
                //     Structures 순회에서 bp_restaurant 구조물(진행/완료 무관)이 해당 마을에서
                //     발견되면 거부한다.
                foreach (var restaurantEntry in Structures)
                {
                    if (restaurantEntry.blueprintId != RestaurantPropBuilder.RestaurantBlueprintId)
                        continue;
                    if (!TryFindRestaurantVillage(restaurantEntry.pos, out NationType entryNation, out int entryVillageIndex))
                        continue;
                    if (entryNation == restaurantNation && entryVillageIndex == restaurantVillageIndex)
                        return "이미 음식점이 있는 마을(시공 중 포함)";
                }

                // ③ 허가세 산출 — 마을 기준 영지(인덱스 = villageIndex+1) 난이도 링 배율 상수표
                var villageDef = territoryDb.GetDefinition(restaurantNation, restaurantVillageIndex + 1);
                permitFee = VillageDrugMath.PermitFee(villageDef.difficulty);

                // 골드 사전 합산 판정 — 시공 골드 + 허가세 동시 부족 시 부분 소비 방지
                int totalGoldNeeded = def.goldCost + permitFee;
                if (stats == null || stats.Gold < totalGoldNeeded)
                    return $"골드 부족 (필요 {totalGoldNeeded}G = 시공 {def.goldCost}G + 허가세 {permitFee}G)";
            }

            // ── [125차] 훈련소 전용 사전 검증 — 영지당 1개 제한 (실내 배치는 허용: 음식점 금지 규약과 무관) ──
            if (def.effectId == TrainingHallBlueprintEffectId)
            {
                // ① 완성물 존재 — HasStructure (기존 API). 이미 훈련소가 있는 영지면 거부.
                if (HasStructure(territoryId, TrainingHallBlueprintEffectId))
                    return "이미 훈련소가 있는 영지";

                // ② 시공 중 가드 — Structures 순회에서 bp_traininghall 구조물(진행/완료 무관)이
                //    해당 영지에 존재하면 거부 (음식점 마을당 1개 시공 중 판정 패턴 준용).
                foreach (var hallEntry in Structures)
                {
                    if (hallEntry.blueprintId != blueprintId)
                        continue;
                    if (hallEntry.territoryId == territoryId)
                        return "이미 훈련소가 있는 영지(시공 중 포함)";
                }
            }

            // ── [90차 A1] 재료 사전 합산 판정 — 전 설계도 공통 (요구 0 = 무관 충족 규약) ──
            // 부족 시 골드/허가세/재료 전부 미소비(부분 소비 금지 원칙). 음식점 5종 재료 + 시설 4종 재료 일괄 판정.
            var invPrecheck = PlayerInventory.Instance ?? FindAnyObjectByType<PlayerInventory>();
            int preWood = invPrecheck != null ? invPrecheck.GetItemCount(MaterialWoodItemId) : 0;
            int preStone = invPrecheck != null ? invPrecheck.GetItemCount(MaterialStoneItemId) : 0;
            int preIron = invPrecheck != null ? invPrecheck.GetItemCount(MaterialIronItemId) : 0;
            int preLeather = invPrecheck != null ? invPrecheck.GetItemCount(MaterialLeatherItemId) : 0;
            int preMana = invPrecheck != null ? invPrecheck.GetItemCount(MaterialManaItemId) : 0;
            if (!HasEnoughMaterials(preWood, preStone, preIron, preLeather, preMana,
                    def.materialWoodCost, def.materialStoneCost, def.materialIronCost,
                    def.materialLeatherCost, def.materialManaCost))
                return BuildMaterialShortageMessage(def);

            // 골드 소비 (성공 시에만 — 벤치 원칙, 기존 goldCost 소비 그대로 유지)
            if (stats == null || !stats.SpendGold(def.goldCost, "construction"))
                return $"골드 부족 (필요 {def.goldCost}G)";

            // ── [D4-A] 허가세 — 플레이어 즉시 지출 → 영주 금고(TerritoryState.territoryGold) 적립 ──
            if (permitFee > 0)
            {
                if (!stats.SpendGold(permitFee, "restaurant_permit"))
                {
                    // 방어 분기 — 위 합산 판정으로 도달하지 않음. 부분 소비 금지 원칙 준수를 위해 시공 골드 환불.
                    stats.AddGold(def.goldCost, "construction");
                    return $"허가세 지출 실패 (영주 허가세 {permitFee}G)";
                }
                var lordState = territoryDb.GetState(restaurantNation, restaurantVillageIndex + 1);
                if (lordState != null)
                    lordState.territoryGold += permitFee; // 영주 금고 적립 (실측 setter — 0 이상 클램프)
            }

            // ── [D4-A→90차 A1] 재료 소비 (골드 소비 성공 후 — 사전 판정으로 실패 없음, 방어 롤백 포함) ──
            // [90차 A1] 나무/돌 패턴을 철(iron_ore)/가죽(mat_boar_leather)/수정석(crystal_shard)로 동일 확장 —
            // 순차 제거 → 실패 시 실제 소비된 재료만 복구(부분 소비 금지) + 골드/허가세 전액 환불.
            if (def.materialWoodCost > 0 || def.materialStoneCost > 0 || def.materialIronCost > 0
                || def.materialLeatherCost > 0 || def.materialManaCost > 0)
            {
                var inv = PlayerInventory.Instance ?? FindAnyObjectByType<PlayerInventory>();
                bool woodNeeded = def.materialWoodCost > 0;
                bool stoneNeeded = def.materialStoneCost > 0;
                bool ironNeeded = def.materialIronCost > 0;
                bool leatherNeeded = def.materialLeatherCost > 0;
                bool manaNeeded = def.materialManaCost > 0;
                bool woodTaken = !woodNeeded;
                bool stoneTaken = !stoneNeeded;
                bool ironTaken = !ironNeeded;
                bool leatherTaken = !leatherNeeded;
                bool manaTaken = !manaNeeded;
                if (inv != null)
                {
                    if (woodNeeded) woodTaken = inv.RemoveItem(MaterialWoodItemId, def.materialWoodCost);
                    if (woodTaken && stoneNeeded) stoneTaken = inv.RemoveItem(MaterialStoneItemId, def.materialStoneCost);
                    if (woodTaken && stoneTaken && ironNeeded) ironTaken = inv.RemoveItem(MaterialIronItemId, def.materialIronCost);
                    if (woodTaken && stoneTaken && ironTaken && leatherNeeded) leatherTaken = inv.RemoveItem(MaterialLeatherItemId, def.materialLeatherCost);
                    if (woodTaken && stoneTaken && ironTaken && leatherTaken && manaNeeded) manaTaken = inv.RemoveItem(MaterialManaItemId, def.materialManaCost);
                }
                if (!(woodTaken && stoneTaken && ironTaken && leatherTaken && manaTaken))
                {
                    // 롤백 — 부분 소비 복구 + 골드 전액 환불 (재료/골드 유지 보장)
                    // 실제 소비된 것만 복구 (xxTaken=true 초기값은 "미필요=미소비" 의미 — inv 접근 금지)
                    if (woodNeeded && woodTaken && inv != null)
                        inv.AddItem(PlayerInventory.Mat_Wood, def.materialWoodCost);
                    if (stoneNeeded && stoneTaken && inv != null)
                        inv.AddItem(PlayerInventory.Mat_Stone, def.materialStoneCost);
                    if (ironNeeded && ironTaken && inv != null)
                        inv.AddItem(PlayerInventory.IronOre, def.materialIronCost);
                    if (leatherNeeded && leatherTaken && inv != null)
                        inv.AddItem(PlayerInventory.BoarLeather, def.materialLeatherCost);
                    if (manaNeeded && manaTaken && inv != null)
                        inv.AddItem(PlayerInventory.CrystalShard, def.materialManaCost);
                    if (stats != null)
                    {
                        stats.AddGold(def.goldCost, "construction");
                        if (permitFee > 0) stats.AddGold(permitFee, "restaurant_permit");
                    }
                    return BuildMaterialShortageMessage(def);
                }
            }

            var entry = new StructureEntry
            {
                structureId = $"struct_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}",
                blueprintId = blueprintId,
                territoryId = territoryId,
                pos = pos,
                progress = 0f,
                isComplete = false,
            };
            _structures.Add(entry);

            // [142차 F] 시공 게이지 부착 — 부지 위 진행 바(완료까지).
            AttachSiteGauge(entry);

            // [129차] 건설 = 크래프트 애니 — 시공 등록 완료 직전 발화 (Q3 환호 없음)
            var player = GameObject.FindGameObjectWithTag("Player");
            var driver = player != null ? (player.GetComponent<HumanoidClipDriver>() ?? player.GetComponentInChildren<HumanoidClipDriver>()) : null;
            driver?.TriggerCraft();

            // [154차 P4] 음식점 배치 확정 성공 — 초기 등록가 입력 모달(RestaurantInitialPriceModalUTK) 트리거.
            // 음식점 분기 성공에 한해 발화(가드: restaurantNation None 아님) — 비음식점 배치/실패 경로 무발화.
            if (restaurantNation != NationType.None)
                RestaurantPlacementSucceeded?.Invoke(restaurantNation, restaurantVillageIndex, permitFee);

            Debug.Log($"[Construction] 🏗️ 건설 시작: {def.displayName} @ {territoryId} (예상 {def.buildTimeSeconds}초)");
            return $"건설 시작! {def.displayName} (예상 {def.buildTimeSeconds}초)";
        }

        private List<(Vector3 pos, float radius)> GetFootprints()
        {
            var list = new List<(Vector3, float)>();
            foreach (var s in _structures)
            {
                if (BlueprintData.TryGet(s.blueprintId, out var def))
                    list.Add((s.pos, def.footprintRadius));
            }
            return list;
        }

        // ── [D4-A] 음식점 배치 순수 헬퍼 (EditMode 테스트 대상) ──

        /// <summary>
        /// 재료 보유 판정 순수 함수 — 보유 수량이 필요 수량 이상인지 (나무/돌 동시 판정).
        /// 소비 수행은 TryPlace 인스턴스 경로(Play 게이트) — 본 메서드는 판정만 담당한다(결정론).
        /// [D4 수리] 요구량 ≤ 0 재료는 보유와 무관하게 충족 판정(판정 대상 제외) — 요구 0 = 보유 무관 규약.
        /// </summary>
        public static bool HasEnoughMaterials(int woodCount, int stoneCount, int needWood, int needStone)
        {
            return (needWood <= 0 || woodCount >= needWood)
                && (needStone <= 0 || stoneCount >= needStone);
        }

        /// <summary>
        /// [90차 A1] 재료 보유 판정 순수 함수 — 5종 확장 (나무/돌/철/가죽/수정석 동시 판정).
        /// 10인자 오버로드 — 기존 4인자 시그니처는 테스트 호환을 위해 그대로 유지한다.
        /// 규약 동일: 요구량 ≤ 0 재료는 보유와 무관하게 충족 판정(기존 8종 설계도 무영향의 논리적 기반).
        /// </summary>
        public static bool HasEnoughMaterials(
            int woodCount, int stoneCount, int ironCount, int leatherCount, int manaCount,
            int needWood, int needStone, int needIron, int needLeather, int needMana)
        {
            return (needWood <= 0 || woodCount >= needWood)
                && (needStone <= 0 || stoneCount >= needStone)
                && (needIron <= 0 || ironCount >= needIron)
                && (needLeather <= 0 || leatherCount >= needLeather)
                && (needMana <= 0 || manaCount >= needMana);
        }

        /// <summary>
        /// [90차 A1] 재료 부족 사유 메시지 — 0인 재료는 문구에서 생략(음식점 "나무 5 + 돌 3 필요" 기존 형식 보존,
        /// 시설 4종은 철/가죽/수정석 구간을 필요한 만큼만 덧붙임). TryPlace 사전 판정/롤백 양쪽에서 동일 사용.
        /// </summary>
        private static string BuildMaterialShortageMessage(BlueprintData.BlueprintDef def)
        {
            var parts = new List<string>(5);
            if (def.materialWoodCost > 0) parts.Add($"나무 {def.materialWoodCost}");
            if (def.materialStoneCost > 0) parts.Add($"돌 {def.materialStoneCost}");
            if (def.materialIronCost > 0) parts.Add($"철 {def.materialIronCost}");
            if (def.materialLeatherCost > 0) parts.Add($"가죽 {def.materialLeatherCost}");
            if (def.materialManaCost > 0) parts.Add($"수정석 {def.materialManaCost}");
            return $"재료 부족 ({string.Join(" + ", parts)} 필요)";
        }

        /// <summary>
        /// [90차 A4] pos가 성(영지 중심) 내부/성 부지인지 — 최근접 영지 중심까지 거리 ≤ CastleInteriorRadius(25m).
        /// 순수 판정(TerritoryDatabase 등록 정의 순회 — Empire 중심 (0,0,0) 등록 정의 포함, 미등록 정의 없음).
        /// 음식점 전용 금지 검사(TryPlace 음식점 분기 최상단) — 다른 시설은 실내 포함 배치 가능(90차 사용자 요구 2).
        ///
        /// [106차 A4 수리 — 마을 존 우선 배제] 근거(105차 실측): 성 가드 25m < 마을 원 40m(일반) →
        ///   마을 원이 성 원을 침범(마을 중심 38~46m, 반경 40m — 결정론 VillagePlacementSystem) →
        ///   마을 안(성 쪽) 배치가 "실내 금지"로 오탐 — Editor.log 거부 56건 실측("음식점은 실내에
        ///   배치할 수 없습니다" 다수). 수리: pos가 어떤 마을 중심 반경(radius, 마진 제외) 내면
        ///   마을 존 우선 — 실내 판정 false(마을에서 건설하라는 규약 자체가 마을 존 = 실외 전제).
        ///   성 중심 계약(A4_IsIndoorPlacement_TerritoryCenter_True)은 마을 원 밖 중심(예: East_01
        ///   오프셋 41.4m > 40m — 결정론 실측)이라 무변경 유지.
        /// </summary>
        public static bool IsIndoorPlacement(Vector3 pos)
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return false; // DB 미구성 환경 — 판정 불가 → 금지 아님(기존 규약)

            var villages = VillagePlacementSystem.GetAllVillages();
            for (int i = 0; i < villages.Count; i++)
            {
                if (Vector3.Distance(villages[i].center, pos) <= villages[i].radius)
                    return false; // 마을 존 내 — 실외(마을 배치) 우선
            }

            float best = float.MaxValue;
            foreach (var def in db.GetAllDefinitions())
            {
                float d = Vector3.Distance(def.worldPosition, pos);
                if (d < best) best = d;
            }
            return best <= CastleInteriorRadius;
        }

        /// <summary>
        /// pos에 대응하는 음식점 건설 마을 조회 — "마을 중심 반경 + VillagePlacementMargin(5m)" 내
        /// 최근접 마을 (결정론: VillagePlacementSystem.GetAllVillages() 고정 순회 순서에서
        /// 최초의 최솟값 채택 — 동점이면 목록 앞쪽 국가/인덱스 우선).
        /// 실패 시 nation = NationType.None, villageIndex = -1.
        /// </summary>
        public static bool TryFindRestaurantVillage(Vector3 pos, out NationType nation, out int villageIndex)
        {
            nation = NationType.None;
            villageIndex = -1;

            var villages = VillagePlacementSystem.GetAllVillages();
            float best = float.MaxValue;
            for (int i = 0; i < villages.Count; i++)
            {
                float d = Vector3.Distance(villages[i].center, pos);
                if (d <= villages[i].radius + VillagePlacementMargin && d < best)
                {
                    best = d;
                    nation = villages[i].nation;
                    villageIndex = villages[i].index;
                }
            }
            return nation != NationType.None;
        }

        // ── [152차 P1] 실내 배치 판정 체인 분기 (순수 헬퍼 — EditMode 직접 판정) ──

        /// <summary>
        /// [152차 P1] 현재 플레이가 실내(IndoorScene) 구동 중인지 — Core 공용 UITransitionState.IndoorActive 게이트.
        /// Application.isPlaying 가드 — EditMode(테스트)에서는 항상 false(기존 실외 TryPlace 계약 무영향).
        /// </summary>
        public static bool IsIndoorScene()
        {
            return Application.isPlaying && ProjectName.Core.UITransitionState.IndoorActive;
        }

        /// <summary>
        /// [152차 P1] 실내 배치 영지 해석 순수 헬퍼 — TryPlace가 받은 territoryId를 실내에서 재해석한다.
        ///  · 실외: 공급값 그대로(기존 계약 무영향).
        ///  · 실내 + 성 실내 보존 키 있음: 보존 키 우선 — 성 진입 시 확정된 실제 귀속 영지
        ///    (Core 공용 UITransitionState.IndoorCastleTerritoryKey — 원점 좌표 Empire_1 오귀속 수리).
        ///  · 실내 + 보존 키 없음(비성 실내 — 크래프트하우스/교회/주택/상점/헛간/동굴/투기장/이계):
        ///    공급값 유지 — 실내 룸은 원점 부근이라 호출부 ResolveTerritoryAt 판정(황제국 반경 60m 내)이
        ///    유효하다. 무효 키는 기존 "영지 없음" 거부가 그대로 작동(체인 순서 보존).
        /// </summary>
        public static string TryPlaceResolvesTerritoryForIndoorScene(
            string suppliedTerritoryId, string activeCastleTerritoryKey, bool isIndoorScene)
        {
            if (!isIndoorScene)
                return suppliedTerritoryId;
            if (!string.IsNullOrEmpty(activeCastleTerritoryKey))
                return activeCastleTerritoryKey;
            return suppliedTerritoryId;
        }

        /// <summary>
        /// [152차 P1] 실내 배치 겹침 순수 판정 — BlueprintData.ValidatePlacement의 ②겹침 검증만 분리.
        /// 실내 로컬 좌표계에서 영지 경계(①월드 중심 반경)는 성립하지 않아 우회하며, 기존 건설물과의
        /// 겹침 방지는 기존 기준(거리가 반경 합 미만이면 겹침)으로 그대로 유지한다.
        /// </summary>
        public static bool ValidateIndoorPlacementOverlap(
            Vector3 pos, float footprintRadius, List<(Vector3 pos, float radius)> existing)
        {
            if (existing == null) return true;
            foreach (var e in existing)
            {
                if (Vector3.Distance(pos, e.pos) < footprintRadius + e.radius)
                    return false;
            }
            return true;
        }

        // ── 시공 진행 ──

        /// <summary>[90차 A5] 건설 지원 — 병사 1명당 진행 배율 가산치 (1 + 0.25×N).</summary>
        public const float AssistMultiplierPerGuard = 0.25f;

        /// <summary>[90차 A5] 건설 지원 — 진행 배율 상한 (2.0 = 병사 4명 이상).</summary>
        public const float AssistMultiplierCap = 2.0f;

        /// <summary>[90차 A5] 건설 지원 판정 반경 (m) — 구조물 중심 기준.</summary>
        public const float AssistRadius = 12f;

        /// <summary>[90차 A5] 구조물별 마지막 지원 병사 수 — 로그 1회성(카운트 변화 시만) 추적용.</summary>
        private readonly Dictionary<string, int> _lastAssistCount = new Dictionary<string, int>();

        /// <summary>
        /// [90차 A5] 지원 병사 수 → 진행 배율 순수 함수 — 1 + 0.25×N, 상한 2.0 (N=0 → 1.0).
        /// Update 진행/GetAssistMultiplier 양쪽에서 동일 사용(단일소스).
        /// </summary>
        public static float ComputeAssistMultiplier(int assistCount) =>
            Mathf.Min(1f + AssistMultiplierPerGuard * Mathf.Max(0, assistCount), AssistMultiplierCap);

        /// <summary>
        /// [90차 A5] pos 반경 AssistRadius(12m) 내 플레이어 소유 병사 수 — 모집됨(IsRecruited) && 생존(IsAlive).
        /// GetAllPlayerGuards 소비처 실측 준용(ArenaMenuUI.GetAvailableGuards/TerritoryBuilder — null 가드 스킵).
        /// GuardManager 부재 시 0(안전 무동작). candidates == null이면 이 호출에서 목록을 새로 조회한다.
        /// </summary>
        private static int CountNearbyRecruitedAliveGuards(Vector3 pos, List<GuardPlaceholder> candidates)
        {
            if (candidates == null)
            {
                var guardManager = GuardManager.Instance;
                if (guardManager == null) return 0;
                candidates = guardManager.GetAllPlayerGuards();
            }

            int count = 0;
            float sqrRange = AssistRadius * AssistRadius;
            foreach (var guard in candidates)
            {
                if (guard == null || !guard.IsRecruited || !guard.IsAlive) continue;
                if ((guard.transform.position - pos).sqrMagnitude <= sqrRange)
                    count++;
            }
            return count;
        }

        /// <summary>[90차 A5] 시공 진행 중 구조물의 현재 지원 병사 수 — 진행 중이 아니면 0 (UI '유휴 인원' 표시용).</summary>
        public int GetAssistCount(string structureId)
        {
            foreach (var s in _structures)
            {
                if (s.structureId != structureId || s.isComplete) continue;
                return CountNearbyRecruitedAliveGuards(s.pos, null);
            }
            return 0;
        }

        /// <summary>[90차 A5] 시공 진행 중 구조물의 현재 지원 배율 — 진행 중이 아니면 1 (UI 표시용).</summary>
        public float GetAssistMultiplier(string structureId)
        {
            foreach (var s in _structures)
            {
                if (s.structureId != structureId || s.isComplete) continue;
                return ComputeAssistMultiplier(CountNearbyRecruitedAliveGuards(s.pos, null));
            }
            return 1f;
        }

        private void Update()
        {
            if (_structures.Count == 0) return;

            // [90차 A5] 병사 목록은 프레임당 1회 조회(구조물 N건이 공유) — GetAllPlayerGuards 할당 최소화.
            List<GuardPlaceholder> assistCandidates = null;
            var guardManager = GuardManager.Instance;
            if (guardManager != null)
                assistCandidates = guardManager.GetAllPlayerGuards();

            // [O8 규약 수리] foreach 반복 변수 수정 금지(CS1654) — 인덱스 루프로 진행 갱신
            for (int i = 0; i < _structures.Count; i++)
            {
                if (_structures[i].isComplete) continue;
                if (!BlueprintData.TryGet(_structures[i].blueprintId, out var def)) continue;

                float advance = Time.deltaTime / Mathf.Max(1f, def.buildTimeSeconds);

                // [90차 A5] 병사 건설 지원 — 반경 12m 내 모집+생존 병사 N → 진행 배율 1+0.25N (상한 2.0).
                // 로그는 카운트 변화 시만(_lastAssistCount 추적 — 매 프레임 스팸 방지).
                int assistCount = CountNearbyRecruitedAliveGuards(_structures[i].pos, assistCandidates);
                bool hadLast = _lastAssistCount.TryGetValue(_structures[i].structureId, out int lastCount);
                if (!hadLast || lastCount != assistCount)
                {
                    _lastAssistCount[_structures[i].structureId] = assistCount;
                    if (assistCount > 0)
                        Debug.Log($"[Construction] 🛠️ 병사 {assistCount}명 건설 지원 — 배율 {ComputeAssistMultiplier(assistCount):0.##}");
                    else if (hadLast)
                        Debug.Log("[Construction] 🛠️ 건설 지원 종료 — 배율 1");
                }
                advance *= ComputeAssistMultiplier(assistCount);

                var s = _structures[i];
                s.progress = Mathf.Clamp01(s.progress + advance);
                UpdateSiteGauge(s);   // [142차 F] 게이지 갱신(완료 시 내부에서 정리까지 — 멱등)

                if (s.progress >= 1f)
                {
                    // [92차 결함1] 구조체 사본 버그 수리 — CompleteStructure는 값 전달이라 내부
                    // s.isComplete=true가 메서드 지역 사본에만 반영되고, 미완료 사본이 write-back
                    // 되어 매 프레임 StructureCompleted 재발화(포렌식: "건설 완료" 로그 3,450회,
                    // 토스트 큐 3,449개 적층 — docs/92_PANEL_ATTACH_AND_LOCOMOTION_PLAN.md 결함 1).
                    // 루프 사본에 완료 플래그를 직접 세팅해 목록 write-back이 완료 상태를 반영한다.
                    s.isComplete = true;
                    CompleteStructure(s);
                    _structures[i] = s;
                }
                else
                {
                    _structures[i] = s;
                }
            }
        }

        /// <summary>구조물 완료 처리 — 이벤트 발화 + 효과 적용 + 시각물 생성(런타임).</summary>
        private void CompleteStructure(StructureEntry s)
        {
            s.isComplete = true;
            s.completedAtUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _lastAssistCount.Remove(s.structureId); // [90차 A5] 완료 — 지원 추적 정리(누수 방지)

            CleanupSiteGauge(s.structureId);   // [142차 F] 게이지/앵커 정리(완료 — 실물로 교체)

            // 효과 적용
            if (BlueprintData.TryGet(s.blueprintId, out var def))
            {
                if (def.effectId == "warehouse_ext")
                {
                    var ws = WarehouseSystem.Instance ?? FindAnyObjectByType<WarehouseSystem>();
                    ws?.AddExpansionLevel(s.territoryId);
                }
                Debug.Log($"[Construction] ✅ 건설 완료: {def.displayName} @ {s.territoryId}");
            }

            BuildVisual(s);
            StructureCompleted?.Invoke(s);
        }

        /// <summary>테스트/강제 완료 — progress 1 승격 (Play 없이 효과 검증용).</summary>
        public void ForceComplete(string structureId)
        {
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s.structureId == structureId && !s.isComplete)
                {
                    s.progress = 1f;
                    // [92차 결함1] 동일 사본 버그 — 값 전달 CompleteStructure가 변경한 isComplete는
                    // 지역 사본에만 머무므로, 루프 사본에 직접 세팅해 write-back을 보장한다
                    // (Update 루프 수리와 동일 근거 — 로그 3,450회 재발화).
                    s.isComplete = true;
                    CompleteStructure(s);
                    _structures[i] = s;
                    return;
                }
            }
        }

        // ── 조회/효과 확인 ──

        /// <summary>해당 영지에 해당 effectId의 "완료된" 구조물 존재? (대장간/축사 확인용)</summary>
        public bool HasStructure(string territoryId, string effectId)
        {
            foreach (var s in _structures)
            {
                if (s.territoryId != territoryId || !s.isComplete) continue;
                if (BlueprintData.TryGet(s.blueprintId, out var def) && def.effectId == effectId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// [O8 C-O8-03] 영지 무관 완료 구조물 존재 여부 — 전역 효과 판정용(대장간 수리비 할인 등).
        /// 현재 영지 추적이 미구성(후속 과제)이라 전역 스코프로 판정한다.
        /// </summary>
        /// <summary>[116차 R3] 구조물 제거 — Test_15 음식점 상태 리셋용. id 미일치 시 false(멱등 무해).</summary>
        public bool TryRemoveStructure(string structureId)
        {
            for (int i = _structures.Count - 1; i >= 0; i--)
            {
                if (_structures[i].structureId != structureId) continue;
                CleanupSiteGauge(structureId);   // [142차 F] 게이지/앵커 정리(제거 경로 — 멱등)
                _structures.RemoveAt(i);
                return true;
            }
            return false;
        }

        public bool HasAnyCompletedStructure(string effectId)
        {
            foreach (var s in _structures)
            {
                if (!s.isComplete) continue;
                if (BlueprintData.TryGet(s.blueprintId, out var def) && def.effectId == effectId)
                    return true;
            }
            return false;
        }

        // ── 해체 (환불 50%) ──

        /// <summary>
        /// 구조물 해체 — 골드 환불 50%(원장 태그 construction_refund). 벤치: 철거 검증 후 소유자만.
        /// [105차 A] 결함5 — 해체 후 음식점 플래그 리셋 + 실물 프롭 파괴(TryDestroyProp) 정리.
        /// </summary>
        public string Demolish(string structureId)
        {
            for (int i = 0; i < _structures.Count; i++)
            {
                if (_structures[i].structureId != structureId) continue;

                var s = _structures[i];
                string resultMessage;
                if (BlueprintData.TryGet(s.blueprintId, out var def))
                {
                    int refund = def.goldCost / 2;
                    var stats = PlayerStats.Instance ?? FindAnyObjectByType<PlayerStats>();
                    stats?.AddGold(refund, "construction_refund");
                    CleanupSiteGauge(structureId);   // [142차 F] 게이지/앵커 정리(해체 경로)
                    _structures.RemoveAt(i);
                    resultMessage = $"해체 완료 (환불 {refund}G)";
                }
                else
                {
                    CleanupSiteGauge(structureId);   // [142차 F] 게이지/앵커 정리(해체 경로)
                    _structures.RemoveAt(i);
                    resultMessage = "해체 완료";
                }

                // [105차 A] 결함5 — RemoveAt만으로는 잔재가 정리되지 않는다(restaurantBuilt true 잔존 →
                // 해체 후 해당 마을 음식점 영구 잠금 + 실물 프롭 고아). 구조물 제거 후 순서대로 정리한다.
                // ① bp_restaurant — 마을 매칭 성공 시 플래그 리셋(재건설 허용)
                if (s.blueprintId == RestaurantPropBuilder.RestaurantBlueprintId &&
                    TryFindRestaurantVillage(s.pos, out var restaurantNation, out var restaurantVillageIndex))
                {
                    VillageDrugState.SetRestaurantBuilt(restaurantNation, restaurantVillageIndex, false);
                    Debug.Log($"[105차 A] 음식점 해체 — 플래그 리셋: {restaurantNation} 마을#{restaurantVillageIndex} (id={s.structureId})");
                }
                // ② 음식점 실물 파괴 (런타임 전용 — EditMode no-op)
                RestaurantPropBuilder.TryDestroyProp(s.structureId);
                // ③ 시설 4종 실물 파괴 (런타임 전용 — EditMode no-op)
                FacilityPropBuilder.TryDestroyProp(s.structureId);

                return resultMessage;
            }
            return "구조물 없음";
        }

        // ── 시각물 (절차 프리미티브 — 런타임만) ──

        private readonly Dictionary<string, GameObject> _visuals = new Dictionary<string, GameObject>();

        private void BuildVisual(StructureEntry s)
        {
            if (!Application.isPlaying || _visuals.ContainsKey(s.structureId)) return;
            if (!BlueprintData.TryGet(s.blueprintId, out var def)) return;

            // [D4-A] 음식점은 RestaurantPropBuilder(StructureCompleted 구독)가 전용 실물(박스+사면 지붕)을
            // 생성한다 — 기본 큐브 이중 생성 방지 스킵.
            if (def.effectId == RestaurantBlueprintEffectId) return;

            // [90차 A3] 시설 4종(cooking/forge/barracks/alchemy)은 FacilityPropBuilder(StructureCompleted 구독)가
            // 전용 실물(모닥불+솥/모루+화덕/막사+사면 지붕/탁자+플라스크)을 생성한다 — 음식점 동일 스킵 패턴.
            if (FacilityPropBuilder.IsFacilityBlueprintId(def.id)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Structure_{def.displayName}";
            float height = def.footprintRadius * 2f;
            go.transform.position = s.pos + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(def.footprintRadius * 2f, height, def.footprintRadius * 2f);
            _visuals[s.structureId] = go;
        }

        // ── 저장 접합 (C-O8-05) ──

        /// <summary>SaveManager 수집용 — 현재 구조물 목록.</summary>
        public List<ConstructionSaveEntry> GetSaveEntries()
        {
            var list = new List<ConstructionSaveEntry>();
            foreach (var s in _structures)
            {
                list.Add(new ConstructionSaveEntry
                {
                    structureId = s.structureId,
                    blueprintId = s.blueprintId,
                    territoryId = s.territoryId,
                    posX = s.pos.x, posY = s.pos.y, posZ = s.pos.z,
                    progress = s.progress,
                    isComplete = s.isComplete,
                });
            }
            return list;
        }

        /// <summary>SaveManager 복원용 — 구형 세이브(빈 리스트) 호환. [O8 간편 규칙] 미완료 건은 즉시 완료 승격(오프라인 경과 단순화).</summary>
        public void ApplySaveEntries(List<ConstructionSaveEntry> entries)
        {
            _structures.Clear();
            if (entries == null) return;

            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.structureId)) continue;

                var s = new StructureEntry
                {
                    structureId = e.structureId,
                    blueprintId = e.blueprintId,
                    territoryId = e.territoryId,
                    pos = new Vector3(e.posX, e.posY, e.posZ),
                    progress = e.progress,
                    isComplete = e.isComplete,
                };

                // 오프라인 간편 규칙: 저장 시점에 미완료였던 건은 로드 시 완료 승격
                if (!s.isComplete)
                {
                    s.progress = 1f;
                    s.isComplete = true;
                }
                _structures.Add(s);
            }

            // [D5-A] 로드 시 완료 음식점 실물 복원 — ApplySaveEntries는 StructureCompleted를
            // 발화하지 않으므로(기존 규약), 실물/플래그 복원을 RestaurantPropBuilder에 위임한다.
            // 호출 순서 규약: SaveManager.Load가 VillageDrugState.ImportSaveEntries를 먼저 실행한 뒤 본 훅이 호출된다.
            if (entries.Exists(e => e != null && e.blueprintId == RestaurantPropBuilder.RestaurantBlueprintId))
                RestaurantPropBuilder.RebuildFromSave();

            // [90차 A3] 로드 시 완료 시설 4종 실물+상호작용 복원 — 음식점(D5-A) 동일 훅 위치(마지막).
            // ApplySaveEntries는 StructureCompleted를 발화하지 않으므로 복원은 FacilityPropBuilder에 위임한다.
            if (entries.Exists(e => e != null && FacilityPropBuilder.IsFacilityBlueprintId(e.blueprintId)))
                FacilityPropBuilder.RebuildFromSave();
        }
    }
}
