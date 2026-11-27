using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [145차 P1] 마을 설계도 5종 + 고산 변형 5종 카탈로그 (총 10마을).
    ///
    /// 계획서: docs/145_VILLAGE_5_DESIGNS_PLAN.md §2(표준 5종)/§6(고산 5종).
    /// 자산: Assets/새로운 glb/마을 GLB/ 46종 — P0 실측(TestOutput/village5_glb_audit_detail.json)에 따라
    ///   전종목 유닛 큐브 정규화(X≤1.01/Y 0~1.01, minY=0)이므로 실제 크기는 역할별 목표높이
    ///   스케일 계수(TargetHeight)로 부여한다(렌더러 bounds 정규화 — VillageBuildingCatalog 선례).
    ///
    /// 설계 계약:
    ///  · 좌표/회전/스케일 전부 하드코딩 결정론(동굴 17-각형 선례) — 난수 API 미사용(Random 0건).
    ///  · 배분 매트릭스 = 계획 §2/§6 확정본. 46파일(Stylized Bell Tower 계열 3파일 포함)이
    ///    표준 세트 5마을에 전수 ≥1 배치(FindMissingStandard로 검산 — 누락 0).
    ///    ※ P0 실측: Stylized Bell Tower는 원본(d74a5810) + "(1)" 중복(d74a5810) + 변형(a5ae8861,
    ///      tri 5758≠5756 별개 에셋) 3파일 — 변형 1개는 계획 §2 마을2 "종탑 2개" 근거로 성당 지구에 배치.
    ///  · MinSpacing 6m + 건물 footprint 보수폭 = TargetHeight×1.2(RequiredSpacing로 산출).
    ///  · 과밀 금지: 광장(FountainPlaza) 중심 반경 6m 내 건물 금지(기존 PlazaRadius 계약).
    ///  · 고산(H1~H5): HighlandProfile(가우시안 완화 고원+분지) 상면에만 건물 배치 —
    ///    분지/수면 위 금지, 다리(Wooden Dock 세그먼트 폴리라인)는 예외.
    ///
    /// 테마 주석(설계도 N 근거): 각 VillageDesign 헤더에 기록.
    /// </summary>
    public enum Village5Role
    {
        Residential,   // 주거
        Shop,          // 상점
        Production,    // 생산
        Landmark,      // 랜드마크
        Prop,          // 소품
        Farm,          // 농경
        Diorama,       // 디오라마
    }

    public enum Village5Type
    {
        Standard,      // 표준 플롯(평지 60×60m)
        Highland,      // 고산 플롯(미니 지형 80×80m — 고원+분지+호수+다리)
    }

    /// <summary>마을 GLB 1종 정의 — fileName은 마을 GLB 디렉토리 상대 경로.</summary>
    public sealed class Village5BuildingDef
    {
        public readonly string Id;
        public readonly string FileName;
        public readonly Village5Role Role;
        public readonly float TargetHeight;   // 목표 높이(m) — 현실적 규모 하드코딩

        public Village5BuildingDef(string id, string fileName, Village5Role role, float targetHeight)
        {
            Id = id; FileName = fileName; Role = role; TargetHeight = targetHeight;
        }
    }

    /// <summary>배치 엔트리 — 플롯 로컬 좌표(x,z)/yaw(도)/선택 스케일 배수.</summary>
    public sealed class Village5Placement
    {
        public readonly string BuildingId;
        public readonly float LocalX;
        public readonly float LocalZ;
        public readonly float YawDeg;
        public readonly float ScaleMul;

        public Village5Placement(string buildingId, float localX, float localZ, float yawDeg, float scaleMul = 1f)
        {
            BuildingId = buildingId; LocalX = localX; LocalZ = localZ; YawDeg = yawDeg; ScaleMul = scaleMul;
        }
    }

    /// <summary>
    /// [145차 P2] 고산 지형 프로파일 — 가우시안 완화 고원+분지(파라미터 하드코딩).
    /// 메시 생성과 접지 샘플러가 이 단일 SampleHeight을 공유한다(단일소스 계약 — 침하 0).
    /// </summary>
    public sealed class Village5HighlandProfile
    {
        /// <summary>플롯 외곽 기저 지면 높이(m) — 분지 밖 육지(호숫가) 높이.</summary>
        public const float BaseY = 1.2f;
        /// <summary>고원 가장자리 완화 폭(m) — 가우시안 스커트 σ.</summary>
        public const float SkirtWidth = 5f;
        /// <summary>분지 가장자리 완화 폭(m).</summary>
        public const float BasinSkirtWidth = 4f;

        public readonly float PlateauCenterX, PlateauCenterZ;   // 고원 중심(플롯 로컬)
        public readonly float PlateauRadius;                    // 고원 상면(평탄부) 반경
        public readonly float PlateauTopY;                      // 고원 상면 높이
        public readonly float BasinCenterX, BasinCenterZ;       // 분지 중심(플롯 로컬)
        public readonly float BasinRadius;                      // 분지 평탄부 반경
        public readonly float BasinBottomY;                     // 분지 바닥 높이
        public readonly float WaterY;                           // 수면 높이 — BasinBottomY < WaterY < PlateauTopY
        public readonly float BridgeX0, BridgeZ0, BridgeX1, BridgeZ1;   // 다리 폴리라인 2점(육지-육지)

        public Village5HighlandProfile(
            float plateauX, float plateauZ, float plateauRadius, float plateauTopY,
            float basinX, float basinZ, float basinRadius, float basinBottomY, float waterY,
            float bridgeX0, float bridgeZ0, float bridgeX1, float bridgeZ1)
        {
            PlateauCenterX = plateauX; PlateauCenterZ = plateauZ;
            PlateauRadius = plateauRadius; PlateauTopY = plateauTopY;
            BasinCenterX = basinX; BasinCenterZ = basinZ;
            BasinRadius = basinRadius; BasinBottomY = basinBottomY; WaterY = waterY;
            BridgeX0 = bridgeX0; BridgeZ0 = bridgeZ0; BridgeX1 = bridgeX1; BridgeZ1 = bridgeZ1;
        }

        /// <summary>고원 완화 계수 — 반경 내 1(완전 평탄), 밖에서 가우시안 감쇠.</summary>
        private static float PlateauFalloff(float dist, float radius)
        {
            if (dist <= radius) return 1f;
            float d = dist - radius;
            return Mathf.Exp(-(d * d) / (2f * SkirtWidth * SkirtWidth));
        }

        private static float BasinFalloff(float dist, float radius)
        {
            if (dist <= radius) return 1f;
            float d = dist - radius;
            return Mathf.Exp(-(d * d) / (2f * BasinSkirtWidth * BasinSkirtWidth));
        }

        /// <summary>
        /// [145차 P4-PAD] 패드 포함 높이 샘플 — 기존 SampleHeight 무수정(기존 계약·테스트 문자열 보존),
        /// 본 메서드는 자연높이 h 계산 후 패드를 적용하는 위임 래퍼. 규칙(계약):
        /// d ≤ radius×0.75 → padY(완전 평탄 코어) / radius×0.75~radius+4m → smoothstep 블렌드 /
        /// 그 외 자연 그대로. 다중 패드: 완전 내부는 중심이 가장 가까운 패드 1승(결정론),
        /// 블렌드 존은 목록 순서대로 순차 Lerp. 메시 정점과 접지 샘플러가 이 함수를 공유한다
        /// (메시/접지 단일소스 계약 유지 — 침하 0).
        /// </summary>
        public float SampleHeightWithPads(float localX, float localZ, IList<Village5TerrainPad> pads)
        {
            float h = SampleHeight(localX, localZ);
            if (pads == null || pads.Count == 0) return h;

            // 1) 완전 평탄 코어 — 중심이 가장 가까운 패드의 padY(겹침 시 결정론 1승)
            int bestIdx = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < pads.Count; i++)
            {
                float d = PadDist(localX, localZ, pads[i]);
                if (d <= pads[i].Radius * 0.75f && d < bestD) { bestD = d; bestIdx = i; }
            }
            if (bestIdx >= 0) return pads[bestIdx].PadY;

            // 2) 블렌드 존 — 순차 Lerp(코어에 걸치지 않은 패드만)
            for (int i = 0; i < pads.Count; i++)
            {
                Village5TerrainPad pad = pads[i];
                float d = PadDist(localX, localZ, pad);
                float outer = pad.Radius + 4f;
                if (d >= outer || d <= pad.Radius * 0.75f) continue;
                float t = Mathf.InverseLerp(pad.Radius * 0.75f, outer, d);
                float w = 1f - Mathf.SmoothStep(0f, 1f, t);   // 내측 가장자리 1 → 외측 0
                h = Mathf.Lerp(h, pad.PadY, w);
            }
            return h;
        }

        private static float PadDist(float x, float z, Village5TerrainPad pad)
        {
            float dx = x - pad.Cx;
            float dz = z - pad.Cz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// 지형 높이 샘플(플롯 로컬 xz → 월드 y). 렌더러 메시 정점과 건물 접지가 동일 함수 사용.
        /// h = BaseY + (Top−BaseY)·t_p − carve·t_b — carve는 고원 꼬리가 분지 바닥을 들어올리지
        /// 않도록 분지 중심에서의 고원 계수로 보정(분지 중심 h == BasinBottomY 보장).
        /// </summary>
        public float SampleHeight(float localX, float localZ)
        {
            float dPlateau = Mathf.Sqrt(
                (localX - PlateauCenterX) * (localX - PlateauCenterX) +
                (localZ - PlateauCenterZ) * (localZ - PlateauCenterZ));
            float dBasin = Mathf.Sqrt(
                (localX - BasinCenterX) * (localX - BasinCenterX) +
                (localZ - BasinCenterZ) * (localZ - BasinCenterZ));
            float tPlateau = PlateauFalloff(dPlateau, PlateauRadius);
            float tBasin = BasinFalloff(dBasin, BasinRadius);
            float dBasinToPlateau = Mathf.Sqrt(
                (BasinCenterX - PlateauCenterX) * (BasinCenterX - PlateauCenterX) +
                (BasinCenterZ - PlateauCenterZ) * (BasinCenterZ - PlateauCenterZ));
            float tPlateauAtBasin = PlateauFalloff(dBasinToPlateau, PlateauRadius);
            float carve = Mathf.Max(BaseY - BasinBottomY, BaseY + (PlateauTopY - BaseY) * tPlateauAtBasin - BasinBottomY);
            return BaseY + (PlateauTopY - BaseY) * tPlateau - carve * tBasin;
        }
    }

    /// <summary>
    /// [145차 P4-PAD] 고산 지형 국소 평탄화 패드 — 건물/나무/다리 착지점 접지감용.
    /// 중심(cx, cz) 반경 radius: 내부 0.75r은 padY 완전 평탄, r~r+4m smoothstep 블렌드.
    /// padY는 항상 자연 SampleHeight에서 채취 — 지형을 새로 만들지 않고 평탄화만 한다(결정론).
    /// </summary>
    public struct Village5TerrainPad
    {
        public readonly float Cx;
        public readonly float Cz;
        public readonly float Radius;
        public readonly float PadY;

        public Village5TerrainPad(float cx, float cz, float radius, float padY)
        {
            Cx = cx; Cz = cz; Radius = radius; PadY = padY;
        }
    }

    /// <summary>마을 설계 1종 — 표준(평지) 또는 고산(프로파일) 플롯 + 배치표.</summary>
    public sealed class Village5Design
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Theme;          // 설계도 근거 주석
        public readonly Village5Type Type;
        public readonly float PlotSize;        // 변 길이(m) — 표준 60 / 고산 80
        public readonly Village5Placement[] Entries;
        public readonly Village5HighlandProfile Profile;   // 고산 전용 — 표준은 null

        public Village5Design(string id, string name, string theme, Village5Type type,
            float plotSize, Village5Placement[] entries, Village5HighlandProfile profile)
        {
            Id = id; Name = name; Theme = theme; Type = type;
            PlotSize = plotSize; Entries = entries; Profile = profile;
        }
    }

    public static class Village5DesignCatalog
    {
        /// <summary>마을 GLB 디렉토리(P0 감사 대상과 동일).</summary>
        public const string VillageGlbDir = "Assets/새로운 glb/마을 GLB/";

        /// <summary>최소 건물 간격(m) — VillageBuilder.MinSpacing 계약 상속.</summary>
        public const float MinSpacing = 6f;

        /// <summary>광장 중심 금지 반경(m) — VillageBuilder.PlazaRadius 계약 상속.</summary>
        public const float PlazaRadius = 6f;

        /// <summary>footprint 보수폭 계수 — footprint = TargetHeight × 1.2.</summary>
        public const float FootprintFactor = 1.2f;

        // ── 46파일 목표높이표(현실적 규모 — 계획 §2/P0 실측 기반 하드코딩) ──
        private static readonly Village5BuildingDef[] AllDefs =
        {
            // 주거 코티지류 4~7m
            new Village5BuildingDef("ThatchedCottage", "Thatched Roof Cottage-ba3ef08d992872ef6fa2dee668312c80.glb", Village5Role.Residential, 4.0f),
            new Village5BuildingDef("MedievalCottage", "Medieval Cottage-29dd2028555bfedfdc3b4ab2791e873c.glb", Village5Role.Residential, 4.5f),
            new Village5BuildingDef("TimberCottage", "Medieval Timber Cottage-f8ea4a8f5d06d09c07222c05c31e7653.glb", Village5Role.Residential, 4.5f),
            new Village5BuildingDef("EarthenHill", "Earthen Hill House-8a06f98c521f9459b224116b778d2923.glb", Village5Role.Residential, 4.5f),
            new Village5BuildingDef("RoundHouse", "Fantasy Round House-fdb4ae44b36db7304212d12429cdf640.glb", Village5Role.Residential, 4.5f),
            new Village5BuildingDef("TudorHouse", "Stylized Tudor House-f536e2fc02f802fee7733ce5507be57b.glb", Village5Role.Residential, 5.5f),
            new Village5BuildingDef("MushroomA", "Fantasy Mushroom House-819aafdcbd3a01fefe6232c2af183776.glb", Village5Role.Residential, 3.5f),
            new Village5BuildingDef("MushroomB", "Fantasy Mushroom House-a93757c36b77755c3d74c76d892d546b.glb", Village5Role.Residential, 3.5f),
            new Village5BuildingDef("TreeHouse", "Fantasy Tree House-2c61bd42ddb7d5c1203ff9fb266e049e.glb", Village5Role.Residential, 7.0f),
            // 상점류 5~6.5m
            new Village5BuildingDef("FantasyBakery", "Fantasy Bakery Building-29dfc8acfb97dc7615c64ca29949659a.glb", Village5Role.Shop, 5.0f),
            new Village5BuildingDef("IsometricBakery", "Isometric Bakery Shop-be7bfe24b72de46288e26ca67944499e.glb", Village5Role.Shop, 5.0f),
            new Village5BuildingDef("GeneralStore", "Stylized General Store-ac9b7a146e2aa54286618cfd8dc32f0c.glb", Village5Role.Shop, 5.5f),
            new Village5BuildingDef("PostOffice", "Stylized Post Office-a6fa9e7d3a4ca4430b1a6ac77c246898.glb", Village5Role.Shop, 6.0f),
            new Village5BuildingDef("PotionShop", "Fantasy Potion Shop-7d674b41f21507b168947d3bb4287039.glb", Village5Role.Shop, 5.0f),
            new Village5BuildingDef("Tavern", "Stylized Fantasy Tavern-ce017fde56b199ee90ec46014960affb.glb", Village5Role.Shop, 6.5f),
            // 생산 3~11m
            new Village5BuildingDef("BlacksmithForge", "Fantasy Blacksmith Forge-b9590f30ca22b0bded9533a54d23ff0d.glb", Village5Role.Production, 5.0f),
            new Village5BuildingDef("CartoonBlacksmith", "Medieval Cartoon Blacksmith-80807849e61f02acf7d8ad8ed487dfbf.glb", Village5Role.Production, 4.5f),
            new Village5BuildingDef("Watermill", "Stone Watermill House-a5f47848b1411112f63a376b56430856.glb", Village5Role.Production, 8.0f),
            new Village5BuildingDef("Windmill", "Stylized Wooden Windmill-c7a8b2725b5aa110eeb7f95c95df6825.glb", Village5Role.Production, 11.0f),
            new Village5BuildingDef("FarmBarn", "Wooden Farm Barn-d0d0edb7707b5d8dee0d20ed9fcff3aa.glb", Village5Role.Production, 6.0f),
            new Village5BuildingDef("Greenhouse", "Wooden Garden Greenhouse-2ce043ec986c08b0c5c0ccc5304bb8bf.glb", Village5Role.Production, 3.5f),
            new Village5BuildingDef("HorseStable", "Horse Stable Diorama-9d70adc9af843ed636d0f32fd7d272c7.glb", Village5Role.Production, 3.0f),
            // 랜드마크 4~14m
            new Village5BuildingDef("Church", "Grey Stylized Church-2f4a0dd9fc90948c3a3cabb55dec4389.glb", Village5Role.Landmark, 12.0f),
            new Village5BuildingDef("ClockTower", "Medieval Clock Tower-9b42d0dc7b79272a487653515a7077e8.glb", Village5Role.Landmark, 12.0f),
            new Village5BuildingDef("BellTower", "Stylized Bell Tower-d74a5810f5cf2f262092008007be71f4.glb", Village5Role.Landmark, 10.0f),
            new Village5BuildingDef("BellTowerDup", "Stylized Bell Tower-d74a5810f5cf2f262092008007be71f4 (1).glb", Village5Role.Landmark, 10.0f),
            new Village5BuildingDef("BellTowerAlt", "Stylized Bell Tower-a5ae8861d2dbeda4ae5f9aec275c5a75.glb", Village5Role.Landmark, 10.0f),
            new Village5BuildingDef("WizardTower", "Purple Wizard Tower-31e014083da92cace70160abb9ff6421.glb", Village5Role.Landmark, 14.0f),
            new Village5BuildingDef("Gatehouse", "Medieval Stone Gatehouse-8c02049efc6114561a86c89d3721504e.glb", Village5Role.Landmark, 8.0f),
            new Village5BuildingDef("CrystalGazebo", "Fantasy Crystal Gazebo-9b75636d7c9824921b4bfb2f4f872f3a.glb", Village5Role.Landmark, 4.0f),
            // 소품 0.44~4m
            new Village5BuildingDef("FountainPlaza", "Stone Fountain Plaza-e41820eb6588aa8795042ec30dd03a26.glb", Village5Role.Prop, 2.0f),
            new Village5BuildingDef("WishingWell", "Stone Wishing Well-1302e0f53e4e43b21696c8bcd4ef7f6d.glb", Village5Role.Prop, 2.5f),
            new Village5BuildingDef("KnightStatue", "Stone Knight Statue-0295e24f455ba7019a43599ed9a92e29.glb", Village5Role.Prop, 3.0f),
            new Village5BuildingDef("StonePillar", "Mossy Stone Pillar-b36b7551410db6b3f4efc99fdc0cc387.glb", Village5Role.Prop, 3.0f),
            new Village5BuildingDef("RockBenches", "Mossy Rock Benches-83673867f7320ab1d3b2133552d27cce.glb", Village5Role.Prop, 1.0f),
            new Village5BuildingDef("ArchBell", "Stone Arch Bell-0c129a8d61d8648a564caffb7ff3dd4e.glb", Village5Role.Prop, 4.0f),
            new Village5BuildingDef("Campfire", "Campfire Cooking Pot-2e2ce197dbd8545d4fcef098dd6a5099.glb", Village5Role.Prop, 1.0f),
            new Village5BuildingDef("TreasureChest", "Treasure Chest Gold Gems-33dca69396049271bccfe950058e6fe4.glb", Village5Role.Prop, 1.0f),
            new Village5BuildingDef("ProduceCart", "Wooden Produce Cart-e47c6cdbefb6b50087269ba50e95116c.glb", Village5Role.Prop, 1.5f),
            new Village5BuildingDef("Signpost", "Wooden Signpost-ae7b6926239ad7322b92536bd07ff169.glb", Village5Role.Prop, 2.0f),
            new Village5BuildingDef("WoodenDock", "Stylized Wooden Dock-9bd9d3db2134d9a2adc4af32cc450419.glb", Village5Role.Prop, 0.44f),
            new Village5BuildingDef("DockFishingRod", "Wooden Dock Fishing Rod-e0b13155c2f959a1b5f4b8e34578d1c4.glb", Village5Role.Prop, 0.44f),
            // 농경 2~2.5m
            new Village5BuildingDef("FencedGarden", "Fenced Vegetable Garden-e83d752757cea3bda1827d7c58936c02.glb", Village5Role.Farm, 2.0f),
            new Village5BuildingDef("FarmAnimal", "Farm Animal Diorama-91e2fe3e5b9e3082f5104d6af8e646ce.glb", Village5Role.Farm, 2.5f),
            // 디오라마
            new Village5BuildingDef("WhimsicalVillage", "Whimsical Fantasy Village-493a89e8a85ae120ecc03230276d5981.glb", Village5Role.Diorama, 3.0f),
            // 생산(광산)
            new Village5BuildingDef("CaveEntrance", "Stone Cave Entrance-153864a03bf6b9a278a7aff45388f129.glb", Village5Role.Production, 5.0f),
        };

        // ── 배치표 헬퍼 ──
        private static Village5Placement E(string id, float x, float z, float yaw, float mul = 1f)
            => new Village5Placement(id, x, z, yaw, mul);

        // ── 표준 5종 (플롯 60×60m, 로컬 ±26m 이내) ──

        /// <summary>V1 — 설계도 1(강변 자급자족): 동쪽 강+부두, 북서 호수변 신전, 서 주거 언덕, 남 성문.</summary>
        private static readonly Village5Placement[] V1Entries =
        {
            E("Gatehouse", 0f, 22f, 0f),               // 남 성문(진입로 향해 외향)
            E("FountainPlaza", 0f, 0f, 0f),            // 중앙 광장 분수대
            E("Signpost", -1.3f, 14.9f, 175f),         // 성문 앞 이정표
            E("RockBenches", 6.9f, 4f, 240f),          // 광장 벤치
            E("CrystalGazebo", -12f, -12f, 45f),       // 북서 호수변 "신비한 신전"
            E("WizardTower", 20f, 0f, 270f),           // 동 강가 연금술사의 탑
            E("Watermill", 18.1f, 15.7f, 230f),        // 강가 방앗간
            E("TimberCottage", -15.8f, -2.8f, 80f),    // 서 주거 언덕
            E("MedievalCottage", -19f, -8.9f, 65f),    // 서 주거 언덕
            E("ThatchedCottage", -19.7f, 3.5f, 100f),  // 서 주거 언덕
            E("FarmBarn", 9.3f, 19.9f, 205f),          // 동 농장 창고
            E("FencedGarden", 8.6f, 12.3f, 215f),      // 동 채소밭
            E("FarmAnimal", 11.5f, -9.6f, 310f),       // 동 가축 디오라마
            E("WoodenDock", -8.6f, 23.5f, 160f),       // 강 하류 부두(남서 강변)
            E("DockFishingRod", -13.5f, 17.3f, 140f),  // 부두 낚싯대
            E("CaveEntrance", 16.3f, -16.3f, 315f),    // 북동 광산 입구
            E("ProduceCart", -14.7f, 8.5f, 120f),      // 농산물 카트
            E("WhimsicalVillage", -21.7f, 12.5f, 120f, 1.5f),  // 외곽 기념물 디오라마
        };

        /// <summary>V2 — 설계도 2(종교·교역): 동쪽 성당 지구(종탑 2), 중앙 분수 광장, 서쪽 소원 우물, 북 풍차.</summary>
        private static readonly Village5Placement[] V2Entries =
        {
            E("Church", 22f, 0f, 270f),                // 동쪽 성당
            E("BellTower", 12.1f, -10.5f, 310f),       // 성당 지구 종탑(원본 파일)
            E("BellTowerAlt", 11.9f, 10.7f, 230f),     // 성당 지구 제2종탑(변형 파일 — 계획 §2 "종탑 2개")
            E("ArchBell", 0f, 14f, 180f),              // 교회 앞 아치 종
            E("FountainPlaza", 0f, 0f, 0f),            // 중앙 광장 분수대
            E("GeneralStore", -12.7f, -5.9f, 65f),     // 잡화점
            E("IsometricBakery", -12.2f, 4.4f, 110f),  // 빵집 1
            E("FantasyBakery", -14f, 11.3f, 130f),     // 빵집 2
            E("PotionShop", -12f, -13.4f, 40f),        // 물약 상점
            E("Tavern", -3.4f, -13.6f, 15f),           // 여관
            E("WishingWell", -18f, 0f, 90f),           // 서쪽 소원 우물
            E("Windmill", 4.4f, -22.6f, 350f),         // 풍차(생산)
            E("FarmBarn", -21.3f, -5.7f, 75f),         // 농장 창고
            E("FencedGarden", -5.4f, -20.3f, 15f),     // 과수원 울타리 밭
            E("MedievalCottage", -7f, 14.4f, 155f),    // 주거
            E("Signpost", -5.3f, 8.5f, 150f),          // 광장 이정표
            E("RockBenches", 4f, 6.9f, 210f),          // 광장 벤치
        };

        /// <summary>V3 — 설계도 3(광산·군사 생산 기지): 북 광산 입구, 남 용광로 2동, 훈련장 석주/석상.</summary>
        private static readonly Village5Placement[] V3Entries =
        {
            E("CaveEntrance", 0f, -22f, 0f),           // 북 광산 입구
            E("BlacksmithForge", 0f, 18f, 180f),       // 남 용광로
            E("CartoonBlacksmith", -6.8f, 20.9f, 160f),// 남 대장간 2
            E("HorseStable", 10f, 17.3f, 210f),        // 마구간
            E("FarmAnimal", 9.9f, 9.9f, 225f),         // 동물 울타리
            E("FarmBarn", 17.3f, 10f, 240f),           // 물자 창고
            E("StonePillar", -9.9f, 9.9f, 135f),       // 훈련장 석주
            E("KnightStatue", -13.3f, 4.3f, 110f),     // 훈련장 기사 석상
            E("ProduceCart", 12.5f, 3.6f, 255f),       // 물류 카트
            E("TreasureChest", 8.9f, -1.3f, 280f),     // 물자 보관 상자
            E("EarthenHill", -16.9f, -6.2f, 70f),      // 서 주거
            E("RoundHouse", -9.9f, -9.9f, 45f),        // 서 주거
            E("Greenhouse", -8f, -17.2f, 25f),         // 과수원 온실
            E("Campfire", 6.1f, 5.1f, 230f),           // 광장 취사 화롯발
            E("Signpost", 6f, -10.4f, 330f),           // 이정표
            E("RockBenches", 0f, -10f, 0f),            // 휴식 벤치
        };

        /// <summary>V4 — 설계도 4(성벽 요새 행정): 남 성문, 감시탑 겸 종탑, 튜더 주거 2동(2인스턴스 소진).</summary>
        private static readonly Village5Placement[] V4Entries =
        {
            E("Gatehouse", 0f, 23f, 0f),               // 남 성문
            E("ClockTower", 18f, 0f, 270f),            // 감시탑 겸 종탑
            E("PostOffice", 9.7f, 8.7f, 230f),         // 시청 행정
            E("TudorHouse", -15f, 5.5f, 110f),         // 주거 밀집 1
            E("TudorHouse", -18.9f, -2f, 85f),         // 주거 밀집 2(동일 에셋 2인스턴스)
            E("GeneralStore", -7.5f, 13f, 150f),       // 잡화
            E("TreasureChest", 8.7f, 2.3f, 255f),      // 왕실 금고 연출
            E("PotionShop", 5.8f, 16f, 200f),          // 허브 연금술
            E("Greenhouse", 12.9f, 16.5f, 220f),       // 허브 온실
            E("FarmBarn", 19.3f, 12.5f, 235f),         // 곡물 창고
            E("Watermill", 20.5f, -14.3f, 305f),       // 강가 방앗간(우하단 강)
            E("FencedGarden", 12.4f, -8.4f, 305f),     // 과수원
            E("RockBenches", -8.2f, -5.7f, 55f),       // 휴식 벤치
            E("KnightStatue", -6.1f, 5.1f, 130f),      // 광장 기사 석상
            E("Signpost", -1.5f, 13.9f, 175f),         // 이정표
        };

        /// <summary>V5 — 설계도 5(숲속 휴양·약초): 동 트리하우스, 북 고지 종탑(중복 파일 소진), 약방+온실.</summary>
        private static readonly Village5Placement[] V5Entries =
        {
            E("TreeHouse", 20f, 0f, 270f),             // 동 절벽 트리하우스
            E("MushroomA", 14.3f, -4.6f, 290f),        // 버섯 주택 1
            E("MushroomB", 14.1f, 5.1f, 250f),         // 버섯 주택 2(2종 소진)
            E("EarthenHill", -16.9f, -6.2f, 70f),      // 언덕 돌집
            E("RoundHouse", -9.9f, -9.9f, 45f),        // 통나무 둥근집
            E("ThatchedCottage", -16f, 5.8f, 110f),    // 코티지
            E("PotionShop", -12f, -0.6f, 85f),         // 약방
            E("Tavern", -5f, -16.3f, 15f),             // 공용 상업(여관 — V2 재사용, 소품류 아님 주석: 계획 §2 원칙 ③ 완화)
            E("FountainPlaza", 0f, 0f, 0f),            // 중앙 휴식 광장
            E("RockBenches", 6.9f, 4f, 240f),          // 광장 벤치
            E("Campfire", 2.9f, 8.5f, 200f),           // 취사 화롯발
            E("Greenhouse", 3.3f, 15.7f, 190f),        // 꽃밭 온실
            E("FencedGarden", -3f, 16.7f, 170f),       // 허브 정원
            E("Signpost", -5.8f, 9.3f, 150f),          // 입구 이정표
            E("ArchBell", 4.8f, -13.2f, 340f),         // 숲 입구 아치 종
            E("BellTowerDup", 14.1f, -16.9f, 320f),    // 북 고지 종탑(중복 파일 소진)
        };

        // ── 고산 5종 (플롯 80×80m, 고원 상면에만 배치 — 계획 §6-1) ──

        /// <summary>H1 — 호수반도 고지(§6-1 1): 3면 호수 고원 반도, 교머리 Gatehouse, 단일 목교 진입.</summary>
        private static readonly Village5Placement[] H1Entries =
        {
            E("Gatehouse", 4.8f, -13.2f, 160f),        // 교머리 성문(다리 진입 끝)
            E("FountainPlaza", 0f, 0f, 230f),          // 반도 중앙 광장
            E("MedievalCottage", -10f, -8.4f, 50f),    // 주거
            E("ThatchedCottage", -6.3f, -13.6f, 25f),  // 주거
            E("Tavern", -17.4f, -4.7f, 75f),           // 여관
            E("GeneralStore", -1.7f, -19.9f, 5f),      // 잡화점
            E("WishingWell", 5.7f, -5.7f, 315f),       // 소원 우물
            E("Signpost", -10.8f, -2.3f, 80f),         // 이정표
            E("RockBenches", -1.4f, -7.9f, 10f),       // 전망 벤치
            E("Campfire", -6.4f, 7.7f, 140f),          // 취사 화롯발
        };

        /// <summary>H2 — 협곡 다리 광산(§6-1 2): 고원 절벽 광산 입구, 대장간/마구간 생산 기지 고산판.</summary>
        private static readonly Village5Placement[] H2Entries =
        {
            E("CaveEntrance", 3.8f, -16.6f, 295f),     // 고원 절벽 가장자리 광산 입구
            E("BlacksmithForge", -10f, 8.4f, 180f),    // 용광로
            E("HorseStable", -14.8f, 2.6f, 160f),      // 마구간
            E("FarmBarn", 7.4f, -5.2f, 255f),          // 창고
            E("ProduceCart", -4f, 6.9f, 200f),         // 물류 카트
            E("TreasureChest", 2.3f, -8.7f, 265f),     // 물자 상자
            E("RoundHouse", -4.1f, -13.4f, 300f),      // 주거
            E("Signpost", 7.8f, -11.6f, 275f),         // 이정표
            E("StonePillar", -11.6f, -3.1f, 165f),     // 석주
        };

        /// <summary>H3 — 단애 테라스(§6-1 3): 상단 종탑 중앙, 주거/농경 테라스 상단 배치(단일 고원 프로파일 계약).</summary>
        private static readonly Village5Placement[] H3Entries =
        {
            E("BellTower", 6f, 0f, 235f),              // 상단 중앙 종탑(원본 파일)
            E("MedievalCottage", -13.6f, 6.3f, 160f),  // 중단 주거(테라스 상단 배치)
            E("ThatchedCottage", -17f, 0f, 140f),      // 중단 주거(테라스 상단 배치)
            E("FencedGarden", -6.9f, 5.8f, 185f),      // 테라스 상단 채소밭
            E("Greenhouse", -11f, 0f, 165f),           // 온실
            E("RockBenches", 5f, -8.7f, 265f),         // 전망 벤치
            E("Campfire", 0f, -13f, 290f),             // 취사 화롯발
            E("DockFishingRod", 9.2f, -13.1f, 280f),   // 하단 부두 연출(부두 예외 — 수변 가장자리 고원단)
        };

        /// <summary>H4 — 호수 섬 어촌(§6-1 4): 섬 고원, 긴 부두형 목교, 낚시/부두 소품 집중.</summary>
        private static readonly Village5Placement[] H4Entries =
        {
            E("DockFishingRod", -2.3f, -12.8f, 310f),  // 수변 낚시 데크(남쪽 호수변)
            E("ThatchedCottage", -10.4f, 6f, 170f),    // 주거
            E("RoundHouse", -11.8f, -2.1f, 145f),      // 주거
            E("Campfire", 5.9f, -1.2f, 245f),          // 취사 화롯발
            E("RockBenches", 3.9f, -7f, 265f),         // 호수 전망 벤치
            E("WishingWell", -15.8f, 2.8f, 145f),      // 소원 우물
            E("Signpost", 5.9f, -12.7f, 290f),         // 이정표
            E("FarmAnimal", -3f, 6.3f, 200f),          // 가축 디오라마
        };

        /// <summary>H5 — 산성 고원(§6-1 5): 가장 높은 고원(15m), 성문+감시 종탑+기사 석상 요새 행정.</summary>
        private static readonly Village5Placement[] H5Entries =
        {
            E("FountainPlaza", 0f, 0f, 225f),          // 중앙 광장
            E("WizardTower", -9.9f, -9.9f, 45f),       // 마법사 탑
            E("ClockTower", 7.7f, -6.4f, 310f),        // 감시 종탑
            E("Gatehouse", -7.8f, 7.8f, 315f),         // 산성 성문(스위치백 진입로)
            E("TudorHouse", -15.9f, 1.4f, 95f),        // 행정 주거
            E("KnightStatue", 1.3f, -14.9f, 355f),     // 기사 석상
            E("StonePillar", 6.5f, -17.9f, 340f),      // 석주
            E("Signpost", -20.3f, -5.4f, 75f),         // 이정표
            E("RockBenches", -3.6f, -18.7f, 10f),      // 전망 벤치
        };

        /// <summary>고산 프로파일 팩토리 헬퍼(가독성).</summary>
        private static Village5HighlandProfile HP(float px, float pz, float pr, float top,
            float bx, float bz, float br, float bot, float water,
            float x0, float z0, float x1, float z1)
            => new Village5HighlandProfile(px, pz, pr, top, bx, bz, br, bot, water, x0, z0, x1, z1);

        /// <summary>표준 5종(한 줄) + 고산 5종(두 번째 줄) — 렌더러 배치 순서.</summary>
        public static readonly Village5Design[] Designs =
        {
            new Village5Design("V1", "강변 자급자족 마을", "설계도 1 — 동 강+부두/북서 호수 신전/서 주거 언덕/남 성문", Village5Type.Standard, 60f, V1Entries, null),
            new Village5Design("V2", "종교·교역 마을", "설계도 2 — 동 성당 지구(종탑 2)/중앙 분수 광장/서 소원 우물/북 풍차", Village5Type.Standard, 60f, V2Entries, null),
            new Village5Design("V3", "광산·군사 생산 기지", "설계도 3 — 북 광산/남 용광로/훈련장 석주·석상", Village5Type.Standard, 60f, V3Entries, null),
            new Village5Design("V4", "성벽 요새 행정 도시", "설계도 4 — 남 성문/감시탑/튜더 주거 2동/우하단 방앗간", Village5Type.Standard, 60f, V4Entries, null),
            new Village5Design("V5", "숲속 휴양·약초 마을", "설계도 5 — 동 트리하우스/북 고지 종탑/약방+허브 온실", Village5Type.Standard, 60f, V5Entries, null),
            new Village5Design("H1", "호수반도 고지 마을", "§6-1 1 — 3면 호수 고원 반도, 교머리 성문, 단일 목교 진입", Village5Type.Highland, 80f, H1Entries,
                HP(-10f, -8f, 17f, 11f, 16f, 10f, 8f, 0f, 0.7f, -2.6f, -2.9f, 29.2f, 19.1f)),
            new Village5Design("H2", "협곡 다리 광산 마을", "§6-1 2 — 고원 절벽 광산 입구, 협곡 목교, 생산 기지 고산판", Village5Type.Highland, 80f, H2Entries,
                HP(-10f, -10f, 19f, 13f, 18f, 10f, 8f, 0f, 0.8f, 3.8f, -0.1f, 32.6f, 20.5f)),
            new Village5Design("H3", "단애 테라스 마을", "§6-1 3 — 상단 종탑/중단 주거/하단 부두(테라스 상단 통합 배치)", Village5Type.Highland, 80f, H3Entries,
                HP(-8f, -10f, 18f, 10f, 16f, 12f, 8f, 0f, 0.6f, 2.3f, -0.5f, 27.8f, 22.8f)),
            new Village5Design("H4", "호수 섬 어촌 마을", "§6-1 4 — 섬 고원, 긴 부두형 목교, 낚시/어촌 소품", Village5Type.Highland, 80f, H4Entries,
                HP(-8f, -8f, 16f, 9f, 18f, 10f, 9f, 0f, 0.7f, 1.9f, -1.2f, 32.8f, 20.2f)),
            new Village5Design("H5", "산성 고원 마을", "§6-1 5 — 최고 고원(15m) 산성, 성문+감시 종탑+기사 석상", Village5Type.Highland, 80f, H5Entries,
                HP(-10f, -10f, 19f, 15f, 18f, 12f, 7f, 0f, 0.8f, 3.4f, 0.5f, 30.6f, 21.9f)),
        };

        // ── 조회/검산 API ──

        /// <summary>46종 정의 전체(P0 감사 파일 수와 일치).</summary>
        public static IReadOnlyList<Village5BuildingDef> Buildings => AllDefs;

        public static int BuildingCount => AllDefs.Length;

        /// <summary>id로 정의 조회 — 없으면 null.</summary>
        public static Village5BuildingDef Get(string id)
        {
            foreach (var def in AllDefs)
                if (def.Id == id) return def;
            return null;
        }

        /// <summary>표준 5종만 반환.</summary>
        public static IEnumerable<Village5Design> StandardDesigns()
        {
            foreach (var d in Designs)
                if (d.Type == Village5Type.Standard) yield return d;
        }

        /// <summary>고산 5종만 반환.</summary>
        public static IEnumerable<Village5Design> HighlandDesigns()
        {
            foreach (var d in Designs)
                if (d.Type == Village5Type.Highland) yield return d;
        }

        /// <summary>
        /// 전수 검산 — 46파일(중복 Bell Tower 포함)이 표준 세트 5마을에 ≥1 배치되는지.
        /// 누락된 건물 id 목록 반환(빈 목록 = 전수 배치 성공).
        /// </summary>
        public static List<string> FindMissingStandard()
        {
            var missing = new List<string>();
            foreach (var def in AllDefs)
            {
                bool placed = false;
                foreach (var village in StandardDesigns())
                {
                    foreach (var e in village.Entries)
                    {
                        if (e.BuildingId == def.Id) { placed = true; break; }
                    }
                    if (placed) break;
                }
                if (!placed) missing.Add(def.Id);
            }
            return missing;
        }

        /// <summary>전수 검산 통과 여부(누락 0).</summary>
        public static bool IsEveryBuildingPlacedStandard() => FindMissingStandard().Count == 0;

        /// <summary>표준 세트 총 배치 인스턴스 수.</summary>
        public static int TotalStandardEntryCount()
        {
            int n = 0;
            foreach (var village in StandardDesigns()) n += village.Entries.Length;
            return n;
        }

        /// <summary>
        /// 두 건물 간 요소 최소 중심거리(m) — MinSpacing 6m와 footprint 보수폭
        /// (TargetHeight×1.2×scaleMul의 절반 합 + 1m 여유) 중 큰 값.
        /// </summary>
        public static float RequiredSpacing(Village5Placement a, Village5Placement b)
        {
            float ra = Get(a.BuildingId) != null ? Get(a.BuildingId).TargetHeight * FootprintFactor * a.ScaleMul * 0.5f : 0f;
            float rb = Get(b.BuildingId) != null ? Get(b.BuildingId).TargetHeight * FootprintFactor * b.ScaleMul * 0.5f : 0f;
            return Mathf.Max(MinSpacing, ra + rb + 1f);
        }

        /// <summary>마을 광장 중심(플롯 로컬) — FountainPlaza 위치, 없으면 고산=고원 중심/표준=원점.</summary>
        public static void PlazaCenter(Village5Design village, out float x, out float z)
        {
            x = 0f; z = 0f;
            if (village.Type == Village5Type.Highland && village.Profile != null)
            {
                x = village.Profile.PlateauCenterX; z = village.Profile.PlateauCenterZ;
            }
            foreach (var e in village.Entries)
            {
                if (e.BuildingId == "FountainPlaza") { x = e.LocalX; z = e.LocalZ; return; }
            }
        }
    }
}
