using System.Collections.Generic;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
using UnityEngine;
using UnityEngine.InputSystem;   // [90차 A3] Keyboard.current.eKey — 상점/음식점(ShopPlaceholder/RestaurantPropInteractable) 동일 E키 폴링

namespace ProjectName.Systems
{
    /// <summary>
    /// [90차 A3] 크래프트 시설 실물 시각 빌더 — ConstructionManager.StructureCompleted 이벤트 구독형.
    /// RestaurantPropBuilder 패턴 전체 복제(구독 전 해제/점유 캐시/국가 틴트/접지/세이브 복원)이며
    /// 대상은 시설 4종 — bp_restaurant는 스킵(기존 RestaurantPropBuilder 소유, 이중 생성 방지).
    ///
    /// 설계:
    ///  · bp_cooking/bp_forge/bp_barracks/bp_alchemy 구조물 완성 시 배치 pos에 시설 실물을 절차 생성한다.
    ///    - 요리   = 모닥불 원(평판 실린더) + 불씨 + 솥 실린더
    ///    - 장비   = 화덕 박스 + 모루 박스(받침대 포함)
    ///    - 병사배치 = 막사 박스 + 사면(4경사면) 지붕 — RestaurantPropBuilder 지붕 기하 동일 수식
    ///    - 연금술 = 탁자 박스 + 플라스크 원통
    ///  · 국가 틴트: RestaurantPropBuilder.GetNationBuildingTint 규약 동일(동=녹갈, 서=황토, 남=적갈, 북=회백)
    ///    — private이라 복제한다. 국가는 구조물의 territoryId → TerritoryDatabase 조회(결정론).
    ///  · 지면 접지: TerrainGenerator.GetHeightAt(..., BiomeType.Plains, 42) + GROUND_BASE(1f) — RestaurantPropBuilder와
    ///    동일 기저 보정(TerritoryBuilder.TrySpawnModelOrPlaceholder 준용).
    ///  · 배선 불요: MonoBehaviour 아님 — RuntimeInitializeOnLoadMethod로 이벤트 구독(도메인 리로드
    ///    안전을 위해 구독 전 해제). ConstructionManager가 자체 Update로 시공 완료 처리하며
    ///    시설 4종은 BuildVisual 기본 큐브를 스킵한다(음식점 D4-A 동일 패턴 — 이중 생성 방지).
    ///  · E키 상호작용: FacilityStationInteractable(하단) — 프롬프트 "E: {시설명}" →
    ///    FacilityOpenRequested(effectId) 정적 이벤트 발화. Systems→UI 순환참조 회피를 위해
    ///    effectId "문자열"만 전달(NationType 아님) — UI 구독은 Part B 창이 담당하며
    ///    무구독 시 로그만 남는다(RestaurantPropInteractable OpenRequestedUTK 동일 정신).
    ///
    /// ※ 세이브 복원 경로(ApplySaveEntries의 미완료→완료 승격)는 이벤트가 발화되지 않으므로
    ///   RebuildFromSave(ConstructionManager.ApplySaveEntries 마지막 훅에서 호출)가
    ///   실물/상호작용을 복원한다 — RestaurantPropBuilder.RebuildFromSave 선례 준용.
    /// </summary>
    public static class FacilityPropBuilder
    {
        /// <summary>지형 기저 보정 (Ground_Inner 월드 y=1) — RestaurantPropBuilder/VillageBuilder와 동일.</summary>
        private const float GROUND_BASE = 1f;

        /// <summary>지형 높이 조회 시드 — RestaurantPropBuilder/VillageBuilder와 동일.</summary>
        private const int TerrainSeed = 42;

        /// <summary>시설 설계도 id 4종 — BlueprintData [90차 A2] 정의와 동일 값(정적 클래스라 상수 공유 불가).</summary>
        public const string CookingBlueprintId = "bp_cooking";
        public const string ForgeBlueprintId = "bp_forge";
        public const string BarracksBlueprintId = "bp_barracks";
        public const string AlchemyBlueprintId = "bp_alchemy";

        /// <summary>시설 effectId 4종 — BlueprintData [90차 A2] effectId와 동일 값(FacilityOpenRequested 페이로드).</summary>
        public const string CookingEffectId = "cooking_facility";
        public const string ForgeEffectId = "forge";
        public const string BarracksEffectId = "barracks";
        public const string AlchemyEffectId = "alchemy_facility";

        /// <summary>[125차] 훈련소 설계도/effectId — BlueprintData.bp_traininghall과 동일 값.</summary>
        public const string TrainingHallBlueprintId = "bp_traininghall";
        public const string TrainingHallEffectId = "training_hall";

        /// <summary>실물 부모 컨테이너 이름 (중복 생성 방지용) — RestaurantProps_Root 분리.</summary>
        private const string RootName = "FacilityProps_Root";

        /// <summary>생성된 실물 캐시 — structureId 중복 생성 방지 (구조물 1건 = 실물 1개).</summary>
        private static readonly Dictionary<string, GameObject> _props = new Dictionary<string, GameObject>();

        /// <summary>시설 설계도 id 판정 — 4종 중 하나면 true (bp_restaurant는 기존 RestaurantPropBuilder 소유 → false 스킵).</summary>
        public static bool IsFacilityBlueprintId(string blueprintId) =>
            blueprintId == CookingBlueprintId || blueprintId == ForgeBlueprintId ||
            blueprintId == BarracksBlueprintId || blueprintId == AlchemyBlueprintId ||
            blueprintId == TrainingHallBlueprintId;   // [125차] 훈련소 — 시설 5종

        /// <summary>이벤트 구독 — 씬 로드 후 1회 (구독 전 해제로 도메인 리로드/재진입 중복 방지).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            ConstructionManager.StructureCompleted -= OnStructureCompleted;
            ConstructionManager.StructureCompleted += OnStructureCompleted;
        }

        // ===== 세이브 복원 경로 =====

        /// <summary>
        /// 세이브 로드 직후 시설 실물+상호작용 복원 — ConstructionManager.ApplySaveEntries 마지막 훅에서 호출.
        /// ApplySaveEntries는 StructureCompleted 이벤트를 발화하지 않으므로(기존 규약), 로드 직후에는
        /// 완료된 시설의 실물이 생성되지 않는 갭이 있다. 본 메서드가 완료된 시설 4종 구조물을 순회해
        /// 런타임 완성 경로와 동일 로직(BuildProp)으로 복원한다. structureId 캐시(_props)로 중복 생성을 방지한다.
        /// 런타임 전용 — EditMode 무동작(OnStructureCompleted 동일 규약).
        /// </summary>
        public static void RebuildFromSave()
        {
            if (!Application.isPlaying) return; // 런타임 전용

            var mgr = ConstructionManager.Instance;
            if (mgr == null) return; // Awake 미실행 환경 — 안전 무동작(O7 폴백 패턴 취급)

            foreach (ConstructionManager.StructureEntry entry in mgr.Structures)
            {
                if (!IsFacilityBlueprintId(entry.blueprintId) || !entry.isComplete)
                    continue; // 완료된 시설 4종만 대상 (bp_restaurant는 스킵 — RestaurantPropBuilder 소유)

                if (_props.ContainsKey(entry.structureId))
                    continue; // 실물 이미 존재 — structureId 캐시 중복 생성 방지

                BuildProp(entry.structureId, entry.blueprintId, entry.pos, ResolveTerritoryNation(entry.territoryId));
            }
        }

        /// <summary>
        /// [105차 A] 결함5 — 구조물 해체 시 실물 파괴. ConstructionManager.Demolish에서 호출한다.
        /// _props 캐시에서 제거 + Destroy(go). 캐시 부재 시 false 반환(로그 없음 — 미생성/이미 파괴 무해).
        /// 런타임 전용 — EditMode 무동작(RebuildFromSave의 Application.isPlaying 규약 동일).
        /// </summary>
        public static bool TryDestroyProp(string structureId)
        {
            if (!Application.isPlaying) return false; // 런타임 전용 — EditMode 무동작
            if (string.IsNullOrEmpty(structureId) || !_props.TryGetValue(structureId, out GameObject go))
                return false;
            _props.Remove(structureId);
            if (go != null) Object.Destroy(go);
            return true;
        }

        /// <summary>완성 이벤트 처리 — 시설 4종만 필터링, 실물 생성 (bp_restaurant는 스킵).</summary>
        private static void OnStructureCompleted(ConstructionManager.StructureEntry entry)
        {
            if (!Application.isPlaying) return;                       // EditMode/에디터에서 무동작 (런타임 전용)
            if (!IsFacilityBlueprintId(entry.blueprintId)) return;    // 시설 4종만 대상 — bp_restaurant 스킵

            BuildProp(entry.structureId, entry.blueprintId, entry.pos, ResolveTerritoryNation(entry.territoryId));
        }

        /// <summary>구조물 territoryId("East_01" 형식) → 국가 조회. DB 부재/미등록은 None(회색 폴백 틴트).</summary>
        private static NationType ResolveTerritoryNation(string territoryId)
        {
            var db = TerritoryDatabase.Instance;
            var def = db != null ? db.GetDefinition(territoryId) : new TerritoryDefinition();
            return def.nation;
        }

        // ===== 실물 생성 (프리미티브 폴백 — 결정론 조합) =====

        /// <summary>시설 실물 생성 — effectId별 프리미티브 조합 + BuildingPlaceholder(Other, 시설명) + E키 상호작용.</summary>
        private static void BuildProp(string structureId, string blueprintId, Vector3 pos, NationType nation)
        {
            if (_props.ContainsKey(structureId)) return; // 중복 생성 방지
            if (!BlueprintData.TryGet(blueprintId, out var def)) return;

            // 지면 접지 — RestaurantPropBuilder와 동일 기저 (TerrainGenerator 결정론 높이 + GROUND_BASE)
            float groundY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, TerrainSeed) + GROUND_BASE;

            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);

            var go = new GameObject($"Facility_{structureId}");
            go.transform.position = new Vector3(pos.x, groundY, pos.z);

            Color nationTint = GetNationBuildingTint(nation);
            switch (def.effectId)
            {
                case CookingEffectId:
                    BuildCookingStation(go.transform, nationTint);
                    break;
                case ForgeEffectId:
                    BuildForgeStation(go.transform, nationTint);
                    break;
                case BarracksEffectId:
                    BuildBarracksStation(go.transform, nationTint);
                    break;
                case AlchemyEffectId:
                    BuildAlchemyStation(go.transform, nationTint);
                    break;
                case TrainingHallEffectId:
                    BuildTrainingHallStation(go.transform, nationTint);
                    break;
                default:
                    return; // 미정의 effectId — 실물 없음(방어. 시설 5종 필터로 도달하지 않음)
            }

            // 건물 표식 — BuildingPlaceholder(Other, 시설명). E 상호작용은 하단 컴포넌트가 담당.
            var placeholder = go.AddComponent<BuildingPlaceholder>();
            placeholder.buildingType = BuildingPlaceholder.BuildingType.Other;
            placeholder.buildingName = def.displayName;

            // E키 상호작용 — 프롬프트 "E: {시설명}", 개방 요청은 effectId 문자열 페이로드
            // (Systems→UI 순환참조 회피 — UI 구독은 Part B 창, 무구독 시 로그만).
            var interactable = go.AddComponent<FacilityStationInteractable>();
            interactable.Configure(def.effectId, GetPromptLabel(def.effectId));

            go.transform.SetParent(root.transform, true);
            _props[structureId] = go;
            Debug.Log($"[FacilityPropBuilder] 🏗️ 시설 실물 생성: {def.displayName} pos=({pos.x:F1},{pos.z:F1}) nation={nation}");
        }

        /// <summary>[125차] 훈련소 — 단련 더미 2개(기둥+가로대) + 무기 걸이 랙(상판+다리+무기 3).
        /// 기존 시설 프롭과 동일 CreatePart 프리미티브 패턴(지면 접지는 BuildProp 기저 담당).</summary>
        private static void BuildTrainingHallStation(Transform parent, Color tint)
        {
            Color wood = new Color(0.45f, 0.32f, 0.20f);   // 기존 시설 목재 톤(캠프파이 돌 톤 계열 어둡게 보정)
            Color metal = new Color(0.35f, 0.37f, 0.40f);

            // 단련 더미 1 — 기둥(원통) + 가로 팔대(박스)
            CreatePart(parent, PrimitiveType.Cylinder, "TrainingDummy1_Post",
                new Vector3(-1.2f, 0.55f, 0f), new Vector3(0.36f, 1.1f, 0.36f), wood, "FacilityWood_Mat");
            CreatePart(parent, PrimitiveType.Cube, "TrainingDummy1_Arms",
                new Vector3(-1.2f, 1.05f, 0f), new Vector3(1.0f, 0.12f, 0.12f), wood, "FacilityWood_Mat");

            // 단련 더미 2
            CreatePart(parent, PrimitiveType.Cylinder, "TrainingDummy2_Post",
                new Vector3(1.2f, 0.55f, 0f), new Vector3(0.36f, 1.1f, 0.36f), wood, "FacilityWood_Mat");
            CreatePart(parent, PrimitiveType.Cube, "TrainingDummy2_Arms",
                new Vector3(1.2f, 1.05f, 0f), new Vector3(1.0f, 0.12f, 0.12f), wood, "FacilityWood_Mat");

            // 무기 걸이 랙 — 상판 + 다리 2 + 무기 3(얇은 박스)
            CreatePart(parent, PrimitiveType.Cube, "TrainingRack_Top",
                new Vector3(0f, 1.0f, 2.0f), new Vector3(1.8f, 0.08f, 0.5f), wood, "FacilityWood_Mat");
            CreatePart(parent, PrimitiveType.Cube, "TrainingRack_LegL",
                new Vector3(-0.8f, 0.5f, 2.0f), new Vector3(0.1f, 1.0f, 0.1f), wood, "FacilityWood_Mat");
            CreatePart(parent, PrimitiveType.Cube, "TrainingRack_LegR",
                new Vector3(0.8f, 0.5f, 2.0f), new Vector3(0.1f, 1.0f, 0.1f), wood, "FacilityWood_Mat");
            CreatePart(parent, PrimitiveType.Cube, "TrainingRack_Weapon1",
                new Vector3(-0.4f, 1.15f, 2.0f), new Vector3(0.06f, 0.5f, 0.06f), metal, "FacilityMetal_Mat");
            CreatePart(parent, PrimitiveType.Cube, "TrainingRack_Weapon2",
                new Vector3(0f, 1.15f, 2.0f), new Vector3(0.06f, 0.5f, 0.06f), metal, "FacilityMetal_Mat");
            CreatePart(parent, PrimitiveType.Cube, "TrainingRack_Weapon3",
                new Vector3(0.4f, 1.15f, 2.0f), new Vector3(0.06f, 0.5f, 0.06f), metal, "FacilityMetal_Mat");
        }

        /// <summary>[90차 A3] 요리 크래프트 — 모닥불 원(평판 실린더) + 불씨 + 솥 실린더.</summary>
        private static void BuildCookingStation(Transform parent, Color tint)
        {
            // 모닥불 돌 링 베이스 — 평판 원통(지름 2.2, 두께 0.1)
            CreatePart(parent, PrimitiveType.Cylinder, "Campfire_Base",
                new Vector3(0f, 0.05f, 0f), new Vector3(2.2f, 0.05f, 2.2f),
                new Color(0.36f, 0.34f, 0.31f), "FacilityStone_Mat");

            // 불씨 — 원통(주황)
            CreatePart(parent, PrimitiveType.Cylinder, "Campfire_Ember",
                new Vector3(0f, 0.35f, 0f), new Vector3(1.0f, 0.2f, 1.0f),
                new Color(0.85f, 0.45f, 0.15f), "FacilityEmber_Mat");

            // 솥 — 실린더(국가 틴트 암색 금속)
            CreatePart(parent, PrimitiveType.Cylinder, "Cooking_Pot",
                new Vector3(0f, 0.85f, 0f), new Vector3(1.1f, 0.3f, 1.1f),
                Color.Lerp(tint, Color.black, 0.55f), "FacilityPot_Mat");
        }

        /// <summary>[90차 A3] 장비 크래프트 — 화덕 박스 + 모루 박스(받침대 포함).</summary>
        private static void BuildForgeStation(Transform parent, Color tint)
        {
            // 화덕 — 석재 박스
            CreatePart(parent, PrimitiveType.Cube, "Forge_Hearth",
                new Vector3(0f, 0.55f, -0.6f), new Vector3(1.6f, 1.1f, 1.0f),
                new Color(0.42f, 0.40f, 0.38f), "FacilityHearth_Mat");

            // 화덕 불씨 — 박스(주황)
            CreatePart(parent, PrimitiveType.Cube, "Forge_Ember",
                new Vector3(0f, 1.18f, -0.6f), new Vector3(1.2f, 0.15f, 0.7f),
                new Color(0.85f, 0.40f, 0.12f), "FacilityEmber_Mat");

            // 모루 받침대 — 목재 박스(국가 틴트)
            CreatePart(parent, PrimitiveType.Cube, "Anvil_Stand",
                new Vector3(0f, 0.35f, 0.9f), new Vector3(0.5f, 0.7f, 0.5f),
                Color.Lerp(tint, Color.black, 0.35f), "FacilityWood_Mat");

            // 모루 — 암철 박스
            CreatePart(parent, PrimitiveType.Cube, "Anvil",
                new Vector3(0f, 0.9f, 0.9f), new Vector3(1.1f, 0.4f, 0.55f),
                new Color(0.22f, 0.22f, 0.24f), "FacilityIron_Mat");
        }

        /// <summary>[90차 A3] 병사배치 — 막사 박스 + 사면(4경사면) 지붕 (RestaurantPropBuilder 지붕 기하 동일 수식).</summary>
        private static void BuildBarracksStation(Transform parent, Color tint)
        {
            // 막사 몸통 박스 (4.2 × 2.2 × 3.0 — 국가 틴트 밝은 벽체, 음식점 wallColor 수식 동일)
            Color wallColor = Color.Lerp(tint, Color.white, 0.30f);
            CreatePart(parent, PrimitiveType.Cube, "Barracks_Body",
                new Vector3(0f, 1.1f, 0f), new Vector3(4.2f, 2.2f, 3.0f),
                wallColor, "FacilityWall_Mat");

            Color roofColor = Color.Lerp(tint, Color.black, 0.40f);

            // 사면(4경사면) 지붕 — 박스 4장을 각 경사각으로 회전 배치 (결정론 기하 — Restaurant 동일)
            const float bodyW = 4.2f, bodyD = 3.0f, bodyH = 2.2f;
            const float ridgeH = 0.9f;   // 처마에서 용마루까지 높이
            const float eave = 0.30f;    // 처마 내밈
            const float t = 0.12f;       // 지붕판 두께
            float baseY = bodyH;         // 지붕 시작 높이 (몸통 상단)
            CreateRoofSlab(parent, "Roof_Front",
                new Vector3(0f, baseY + ridgeH * 0.5f, bodyD * 0.25f), Quaternion.Euler(Angle(ridgeH, bodyD), 0f, 0f),
                new Vector3(bodyW + eave * 2f, t, SlopeLen(ridgeH, bodyD) + eave), roofColor);
            CreateRoofSlab(parent, "Roof_Back",
                new Vector3(0f, baseY + ridgeH * 0.5f, -bodyD * 0.25f), Quaternion.Euler(-Angle(ridgeH, bodyD), 0f, 0f),
                new Vector3(bodyW + eave * 2f, t, SlopeLen(ridgeH, bodyD) + eave), roofColor);
            CreateRoofSlab(parent, "Roof_Right",
                new Vector3(bodyW * 0.25f, baseY + ridgeH * 0.5f, 0f), Quaternion.Euler(0f, 0f, -Angle(ridgeH, bodyW)),
                new Vector3(SlopeLen(ridgeH, bodyW) + eave, t, bodyD + eave * 2f), roofColor);
            CreateRoofSlab(parent, "Roof_Left",
                new Vector3(-bodyW * 0.25f, baseY + ridgeH * 0.5f, 0f), Quaternion.Euler(0f, 0f, Angle(ridgeH, bodyW)),
                new Vector3(SlopeLen(ridgeH, bodyW) + eave, t, bodyD + eave * 2f), roofColor);
        }

        /// <summary>[90차 A3] 연금술 크래프트 — 탁자 박스 + 플라스크 원통.</summary>
        private static void BuildAlchemyStation(Transform parent, Color tint)
        {
            // 탁자 — 목재 박스(국가 틴트)
            CreatePart(parent, PrimitiveType.Cube, "Alchemy_Table",
                new Vector3(0f, 0.45f, 0f), new Vector3(2.0f, 0.9f, 1.0f),
                Color.Lerp(tint, Color.white, 0.10f), "FacilityWood_Mat");

            // 플라스크 — 원통(연금술 보라)
            CreatePart(parent, PrimitiveType.Cylinder, "Alchemy_Flask",
                new Vector3(0.3f, 1.2f, 0f), new Vector3(0.4f, 0.3f, 0.4f),
                new Color(0.55f, 0.30f, 0.75f), "FacilityFlask_Mat");
        }

        /// <summary>프리미티브 파트 1개 생성 — 위치/스케일/색상 지정, 태그 Untagged(기존 파트 규약 동일).</summary>
        private static GameObject CreatePart(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 localScale, Color color, string matName)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            part.tag = "Untagged";
            var renderer = part.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.material = MaterialHelper.CreateLitMaterial(color, matName);
            return part;
        }

        /// <summary>지붕 경사판 1장 생성 — 위치/회전/스케일 지정, 국가 틴트 기반 지붕색 (Restaurant 동일).</summary>
        private static void CreateRoofSlab(Transform parent, string name, Vector3 localPos, Quaternion rot, Vector3 scale, Color roofColor)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, false);
            slab.transform.localPosition = localPos;
            slab.transform.localRotation = rot;
            slab.transform.localScale = scale;
            slab.tag = "Untagged";
            var r = slab.GetComponent<MeshRenderer>();
            if (r != null)
                r.material = MaterialHelper.CreateLitMaterial(roofColor, "FacilityRoof_Mat");
        }

        /// <summary>지붕 경사각 (rad) — 용마루 높이와 수평 반폭 기준 (Restaurant 동일 수식).</summary>
        private static float Angle(float ridgeH, float halfSpan) => Mathf.Atan2(ridgeH, halfSpan);

        /// <summary>지붕 경사면 길이 — 피타고라스 (용마루 높이, 수평 반폭) (Restaurant 동일 수식).</summary>
        private static float SlopeLen(float ridgeH, float halfSpan) => Mathf.Sqrt(halfSpan * halfSpan + ridgeH * ridgeH);

        /// <summary>건물 국가 틴트 — RestaurantPropBuilder.GetNationBuildingTint와 동일 규약 (동=녹갈, 서=황토, 남=적갈, 북=회백).
        /// 원본은 private이라 복제한다(90차 A3 — 단일소스 우선이나 접근 불가로 패턴 복제).</summary>
        private static Color GetNationBuildingTint(NationType nation) => nation switch
        {
            NationType.East  => new Color(0.42f, 0.34f, 0.22f), // 동 — 녹갈 토
            NationType.West  => new Color(0.50f, 0.38f, 0.22f), // 서 — 황토
            NationType.South => new Color(0.48f, 0.31f, 0.20f), // 남 — 적갈
            NationType.North => new Color(0.40f, 0.40f, 0.38f), // 북 — 회백 설토
            _                => new Color(0.50f, 0.48f, 0.45f)  // 기타(None/Empire/Dracula) — 회색
        };

        /// <summary>[90차 A3] E키 프롬프트 문구 — effectId별 고정(Figma/스펙 확정 문구). 시설명 = BlueprintDef.displayName.</summary>
        private static string GetPromptLabel(string effectId) => effectId switch
        {
            CookingEffectId  => "E: 요리 크래프트",
            ForgeEffectId    => "E: 장비 크래프트",
            BarracksEffectId => "E: 병사배치",
            AlchemyEffectId  => "E: 연금술 크래프트",
            TrainingHallEffectId => "E: 훈련소",
            _                => "E: 시설 사용"
        };

        // ===== [170차 P0] 홀로그램 시각 전용 — 실물 파트 빌더 재사용(단일 소스) =====

        /// <summary>홀로그램용 시설 시각 생성 — 실물 경로와 동일 BuildXStation을 재사용해 형태만 생성한다.
        /// 콜라이더 제거(isPlaying 가드 — EditMode 호환), E키 상호작용/BuildingPlaceholder/이벤트 배제.
        /// nation은 실물 경로와 동일 시그니처 유지용(최종 색은 홀로그램 틴트가 덮음 — 형태는 nation 무관).
        /// 미정의 effectId는 null 반환(실물 경로 default return 동일 방어).</summary>
        public static GameObject BuildHologramVisual(string blueprintId, NationType nation)
        {
            if (!BlueprintData.TryGet(blueprintId, out var def)) return null;
            Color tint = GetNationBuildingTint(nation);
            var go = new GameObject($"Hologram_{def.id}");
            switch (def.effectId)
            {
                case CookingEffectId:
                    BuildCookingStation(go.transform, tint);
                    break;
                case ForgeEffectId:
                    BuildForgeStation(go.transform, tint);
                    break;
                case BarracksEffectId:
                    BuildBarracksStation(go.transform, tint);
                    break;
                case AlchemyEffectId:
                    BuildAlchemyStation(go.transform, tint);
                    break;
                case TrainingHallEffectId:
                    BuildTrainingHallStation(go.transform, tint);
                    break;
                default:
                    Object.DestroyImmediate(go);
                    return null; // 미정의 effectId — 실물 없음(방어. 실물 경로와 동일)
            }
            StripColliders(go);
            return go;
        }

        /// <summary>홀로그램 파트 콜라이더 제거 — CreatePrimitive 자동 콜라이더 포함 전종.
        /// [126차 함정] EditMode에서 Object.Destroy는 금지 — isPlaying 가드로 Destroy/DestroyImmediate 분기.</summary>
        private static void StripColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
        }
    }

    /// <summary>
    /// [90차 A3] 시설 실물 E 상호작용 컴포넌트 — RestaurantPropInteractable 패턴 준용:
    ///   · Update 근접 폴링 → EInteractionPromptBridge.RaiseRegister/Unregister(프롬프트 "E: {시설명}", 범위 3m)
    ///   · E키(Input System) → FacilityOpenRequested(effectId) 정적 이벤트 발화.
    ///   · 개방 페이로드는 effectId "문자열"("cooking_facility"/"forge"/"barracks"/"alchemy_facility") —
    ///     Systems→UI 순환참조 회피(NationType 아님). UI 구독은 Part B 창이 담당하며
    ///     무구독 시 로그만 남는다(RestaurantPropInteractable.OpenRequestedUTK 동일 정신).
    /// ※ 런타임 AddComponent 전용(FacilityPropBuilder.BuildProp 부착) — 프리팹/씬 직렬화 대상 아님
    ///   (RestaurantPropInteractable/RestaurantDailyTicker 동일 규약: 런타임 생성 컴포넌트의 동일 파일 배치 선례).
    /// </summary>
    public class FacilityStationInteractable : MonoBehaviour
    {
        /// <summary>[90차 A3] 시설 개방 요청 — UI(Part B 창)가 구독. 파라미터 = 시설 effectId (string).</summary>
        public static event System.Action<string> FacilityOpenRequested;

        /// <summary>상호작용 범위 (m) — RestaurantPropInteractable.InteractRange 동일 기본값(상점 3m 준용).</summary>
        private const float InteractRange = 3f;

        private string _effectId;      // "cooking_facility"/"forge"/"barracks"/"alchemy_facility"/"training_hall" — 개방 요청 페이로드

        /// <summary>[125차] 부착된 시설 effectId — HoverTargetClassifier 훈련소 분기 판정용(읽기 전용 노출).</summary>
        public string EffectId => _effectId;
        private string _promptLabel;   // "E: 요리 크래프트" 등 — EInteractionPromptBridge 등록 문구
        private bool _configured;      // Configure 호출 전 무동작 가드(부착 규약 위반 방어)

        private Transform _player;     // 캐싱된 플레이어 참조 (매 프레임 Find 방지 — RestaurantPropInteractable 동일)

        /// <summary>시설 구성 — BuildProp 부착 직후 1회 호출.</summary>
        public void Configure(string effectId, string promptLabel)
        {
            _effectId = effectId;
            _promptLabel = promptLabel;
            _configured = !string.IsNullOrEmpty(effectId);
        }

        private void Update()
        {
            if (!_configured)
                return; // 미구성 — 안전 무동작(부착 규약 위반 방어)

            // 플레이어 참조 캐싱 (씬 전환 시 재탐색 — RestaurantPropInteractable/ShopPlaceholder 동일)
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (_player == null) return;
            }

            // sqrMagnitude로 거리 비교 (Distance보다 GC/성능 우수 — 기존 인터랙터블 동일)
            float sqrDist = (transform.position - _player.position).sqrMagnitude;
            bool nearby = sqrDist <= InteractRange * InteractRange;

            // [43차] 공용 E 월드 프롬프트 — 근접 시 등록, 이탈 시 해제(창 열림 시 UI가 자동 숨김).
            if (nearby)
                EInteractionPromptBridge.RaiseRegister(transform, _promptLabel, InteractRange);
            else
                EInteractionPromptBridge.RaiseUnregister(transform);
            if (!nearby) return;

            // E키로 상호작용 (Input System — RestaurantPropInteractable/ShopPlaceholder 동일)
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                // Systems→UI 참조 불가 — 개방 요청 이벤트만 발화. UI 미구독 시 로그만(라우팅은 Part B).
                Debug.Log($"[FacilityPropBuilder] E키 시설 개방 요청: {_effectId} (UI 구독 없으면 무효 — Part B 라우팅)");
                FacilityOpenRequested?.Invoke(_effectId);
            }
        }

        private void OnDisable()
        {
            // 프롬프트 잔존 방지 — 비활성화 직전 해제(방어. anchor null은 브리지 내부에서 가드).
            EInteractionPromptBridge.RaiseUnregister(transform);
        }
    }
}
