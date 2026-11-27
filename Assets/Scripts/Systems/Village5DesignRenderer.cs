using System.Collections.Generic;
using ProjectName.Core.Utils;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [145차 P2] 마을 설계 10종(표준 5 + 고산 5) 쇼케이스 렌더러 — VillageBuilder 패턴 준용.
    ///
    /// 계획서: docs/145_VILLAGE_5_DESIGNS_PLAN.md §3/§6-2. 카탈로그 = Village5DesignCatalog(좌표 단일소스).
    ///
    /// 구성:
    ///  · 루트 "Village5Designs_Root" — 중복 생성 가드(GameObject.Find), Play 가드(Application.isPlaying).
    ///  · 마을 플롯을 XZ 130m 피치로 배치 — 표준 5종 = z=0 한 줄, 고산 5종 = z=+130 두 번째 줄.
    ///    [145차 P3-TOUR] PlotPitch 130m(플롯 60/80m + 투어 보행 여백) + BuildAll(origin) 오버로드.
    ///  · 표준 플롯: [145차 P5-EDGE] 미니 지형 메시(중심 미세 FBM 굴곡 + 가장자리 falloff — 기존
    ///    박스 프리미티브 패치 대체. TestTerritoryCombatSetup.BuildVillageGroundPatch 선례의 평탄판 구조를
    ///    메시화) 후 카탈로그 좌표 배치, 접지 = 메시와 동일 StandardGroundHeight 샘플러(단일소스).
    ///  · 고산 플롯: 미니 지형 메시(64×64 세그먼트/80×80m) — HighlandProfile.SampleHeight로 정점 생성,
    ///    같은 함수를 건물 접지 샘플러로 공유(메시/접지 단일소스 — 침하 0). 수면 디스크(호수 원판 선례),
    ///    다리 = Wooden Dock 1×1m 세그먼트를 폴리라인 따라 연결(데크 = 양끝 지면 보간 + 수면 위 0.15m
    ///    최소 여유 — 경사 ≤25° 보행 계약, 계획 §6-1). 데크가 수면보다 1.5m 이상 높은 구간에는
    ///    절차 지지 기둥 박스(동굴 배면벽 박스 선례)를 4모듈마다 추가.
    ///  · 접지/스케일: VillageBuildingCatalog.InstantiateAtGround 재사용(bounds 높이 → TargetHeight
    ///    정규화 + minY 접지). GLB 로드 실패 시 프리미티브 폴백(몸통 박스 — 역할별 색).
    ///  · 결정론: 난수 API 미사용(Random 0건) — 좌표/치수는 카탈로그 하드코딩, 자연 산포는
    ///    [145차 P3-NATURE] 설계 id 해시 나선표본(황금각+xorshift32 — VillageBuilder.GetHash 패턴).
    ///  · [145차 P4] 접지감 패키지: A 고산 평탄화 패드(메시/접지 단일소스 — SampleHeightWithPads 위임 래퍼)
    ///    B 코블스톤 기초 링 / C 흙 디퍼링 디스크 / D 수변 갈대 산포 / E 로컬 블롭 섀도우.
    ///  · [145차 P5] 완전 자연화: EDGE 표준 플롯 미니 지형 메시(중심 FBM 굴곡+가장자리 falloff —
    ///    메시/접지 단일소스 샘플러, 고산과 동일 구조) / GRASS 잔디·꽃 정적 산포 /
    ///    TEXMIX 이끼·바위 텍스처 패치 디스크 / CLUSTER 나무 클러스터링(가우시안 유사 산포) /
    ///    CURVE 사인 곡선 보행로(폴리라인 캐시 단일소스) / WATERFX 수련·수변 바위·캠프파이어 화염.
    /// </summary>
    public static class Village5DesignRenderer
    {
        /// <summary>지면 패치 상면 높이 — VillageBuilder.GROUND_BASE 계약 상속.</summary>
        private const float GROUND_BASE = 1f;

        /// <summary>마을 플롯 간 피치(m) — [145차 P3-TOUR] 200→130(플롯 60/80m + 투어 보행 거리 단축).</summary>
        private const float PlotPitch = 130f;

        /// <summary>고산 미니 지형 메시 세그먼트 수(64×64 → 65×65=4225 정점 — 정점수 상한 여유).</summary>
        private const int MeshSegments = 64;

        private const string RootName = "Village5Designs_Root";

        /// <summary>
        /// [145차 P2b] 단일 플롯 API — designId로 카탈로그 설계 1종을 조회해 center에 플롯을 생성.
        /// Test_15 고산 검증(SetupTest15VillageDrugEconomy H1 실배치) 등 개별 마을 검증에서 재사용.
        /// Play 가드(Application.isPlaying) 유지 — BuildAll과 동일 계약. 중복 플롯 가드 포함.
        /// </summary>
        public static GameObject BuildPlot(string designId, Vector3 center)
        {
            return BuildPlot(designId, center, string.Empty);   // [145차 P3-TOUR] 기본 시그니처 보존
        }

        /// <summary>[145차 P3-TOUR] suffix — 동일 설계 다중 인스턴스(게임 플롯 vs 쇼케이스 플롯) 분리용.</summary>
        public static GameObject BuildPlot(string designId, Vector3 center, string suffix)
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[145차 P2b] [Village5DesignRenderer] BuildPlot — 비Play 환경 스킵 (가드)");
                return null;
            }

            Village5Design design = null;
            foreach (var d in Village5DesignCatalog.Designs)
                if (d.Id == designId) { design = d; break; }
            if (design == null)
            {
                Debug.LogWarning($"[145차 P2b] [Village5DesignRenderer] 알 수 없는 설계 id — 스킵: {designId}");
                return null;
            }

            // 중복 가드 — 동일 플롯 GameObject가 이미 존재하면 스킵(멱등)
            string plotName = $"Village5_{design.Id}{suffix}";
            if (GameObject.Find(plotName) != null)
            {
                Debug.Log($"[145차 P2b] [Village5DesignRenderer] 플롯 이미 존재 — 스킵: {designId} ({plotName})");
                return GameObject.Find(plotName);
            }

            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);

            GameObject plotGo;
            if (design.Type == Village5Type.Highland)
                plotGo = BuildHighlandPlot(root.transform, design, center);
            else
                plotGo = BuildStandardPlot(root.transform, design, center);
            return plotGo;
        }

        /// <summary>
        /// [147차] 평지 플롯 — Test_15(Livelihood) 기본 평지용 마을 설계 플롯(계획 §1-2/§6-2).
        ///
        /// BuildPlot과 동일 계약(Play 가드/루트 Village5Designs_Root/플롯 명명 Village5_{id}{suffix}/중복 가드)이며,
        /// 접지 샘플러만 평지 상수(1 + TerrainGenerator.Test15FlatHeight — SetupGround 표면 SurfaceY=1+GetHeightAt와
        /// 동일 단일소스)로 교체한다. 기존 경로(BuildPlot/BuildStandardPlot/BuildHighlandPlot)는 무수정 — 신규 추가만.
        ///
        /// 재사용: PlaceEntries(설계 배치표 전수 — 신규 마을 GLB 46종 카탈로그 배치 + 기초 링/디퍼링/블롭 섀도우 공통 경로),
        /// BuildStandardDirtPaths(흙길 진입로+광장 링 — 캐시 메시 저가, §5 ②유지).
        /// 스킵(평지 모드): BuildTerrainMeshCore 지면 메시/Idyllic 잔디 머티리얼/자연 산포(나무·덤불)/
        /// 잔디·꽃 산포/텍스처 패치 — 기본 평지 위 중복 전개 방지(로딩 경량화).
        /// 고산 설계는 프로필 지형(분지/고원/목교)이 전제라 평지 모드 대상에서 제외 — 표준 설계만 배치.
        /// </summary>
        public static GameObject BuildPlotFlat(string designId, Vector3 center, string suffix)
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[147차] [Village5DesignRenderer] BuildPlotFlat — 비Play 환경 스킵 (가드)");
                return null;
            }

            Village5Design design = null;
            foreach (var d in Village5DesignCatalog.Designs)
                if (d.Id == designId) { design = d; break; }
            if (design == null)
            {
                Debug.LogWarning($"[147차] [Village5DesignRenderer] BuildPlotFlat — 알 수 없는 설계 id — 스킵: {designId}");
                return null;
            }
            if (design.Type != Village5Type.Standard)
            {
                Debug.LogWarning($"[147차] [Village5DesignRenderer] BuildPlotFlat — 고산 설계는 평지 모드 대상 외(프로필 지형 전제) — 스킵: {designId}");
                return null;
            }

            // 중복 가드/루트 — BuildPlot 동일 계약(멱등)
            string plotName = $"Village5_{design.Id}{suffix}";
            var existing = GameObject.Find(plotName);
            if (existing != null)
            {
                Debug.Log($"[147차] [Village5DesignRenderer] BuildPlotFlat — 플롯 이미 존재 — 스킵: {designId} ({plotName})");
                return existing;
            }
            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);

            var plot = new GameObject(plotName);
            plot.transform.SetParent(root.transform, false);
            // y=0 고정 — 건물/기초/디퍼링/블롭은 PlaceEntries가 절대 groundY로 접지하고, 흙길 스트립 로컬 y만
            // plot.position.y의 영향을 받으므로(빌드 순서 계약) 평지 표면과 정확히 정렬된다(마을 center.y 무시).
            plot.transform.position = new Vector3(center.x, 0f, center.z);

            // [147차] 평지 상수 샘플러 — 표면 y = 1 + Test15FlatHeight(GROUND_BASE 상속 계약).
            // 메시 정점 생성이 없는 평지 모드라도 접지 단일소스 계약 준수(PlaceEntries/흙길 전부 이 함수 공유).
            System.Func<float, float, float> sampler = (x, z) => GROUND_BASE + TerrainGenerator.Test15FlatHeight;

            BuildStandardDirtPaths(plot.transform, design, sampler);   // [147차] 흙길 유지(§5 ② — 캐시 메시 저가)
            PlaceEntries(plot.transform, design, sampler);             // GLB 전수 배치+기초 링/디퍼링/블롭 공통 경로
            // 스킵(평지 모드 — §6-2): BuildTerrainMeshCore(지면 메시)/BuildNatureScatter(자연 산포)/
            // BuildGrassFlowerScatter(잔디·꽃)/BuildTexturePatches(텍스처 패치) — Idyllic 잔디 머티리얼 경로 미진입.

            Debug.Log($"[147차] [Village5DesignRenderer] 평지 플롯 {design.Id}({design.Name}) — entries={design.Entries.Length} " +
                      $"flatY={GROUND_BASE + TerrainGenerator.Test15FlatHeight:F2} " +
                      "(평지 모드 — 지면 메시/자연·잔디 산포/텍스처 패치 스킵, 흙길 유지)");
            return plot;
        }

        /// <summary>전체 10마을 일괄 렌더(기본 원점). 중복 호출 시 스킵, 에디터(비Play)에서 무동작.
        /// [145차 P3-TOUR] origin 오버로드 위임 — 기존 시그니처 보존(동작 동일성).</summary>
        public static void BuildAll()
        {
            BuildAll(Vector3.zero);
        }

        /// <summary>
        /// [145차 P3-TOUR] 전체 10마을 일괄 렌더 — origin 파라미터(마을 투어 코스 원점).
        /// 각 플롯 배치 좌표 = origin + (col×PlotPitch, 0, row×PlotPitch). PlotPitch 130m.
        /// </summary>
        public static void BuildAll(Vector3 origin)
        {
            // [ExecuteInEditMode] 오염 방지 — VillageBuilder와 동일 Play 가드
            if (!Application.isPlaying)
                return;

            // [145차 P3-TOUR 보정] 루트 존재로 전체 스킵하지 않는다 — BuildPlot("H1")이 먼저 루트를
            //   만든 경우(테스트 씬 순서)에도 나머지 플롯을 빌드해야 한다. 멱등성은 BuildPlot의
            //   플롯 단위 가드(Village5_<Id> 존재 스킵)가 담당한다.
            var root = GameObject.Find(RootName);
            if (root == null) root = new GameObject(RootName);

            int built = 0;
            // 표준 5종 — 첫 번째 줄(z=0)
            int col = 0;
            foreach (var design in Village5DesignCatalog.StandardDesigns())
            {
                BuildPlot(design.Id, origin + new Vector3(col * PlotPitch, 0f, 0f));
                col++; built++;
            }
            // 고산 5종 — 두 번째 줄(z=+130)
            col = 0;
            foreach (var design in Village5DesignCatalog.HighlandDesigns())
            {
                BuildPlot(design.Id, origin + new Vector3(col * PlotPitch, 0f, PlotPitch));
                col++; built++;
            }

            Debug.Log($"[145차 P3-TOUR] [Village5DesignRenderer] 마을 설계 플롯 빌드 완료: plots={built} " +
                      $"origin=({origin.x:F1},{origin.y:F1},{origin.z:F1}) pitch={PlotPitch:F0} " +
                      $"standardEntries={Village5DesignCatalog.TotalStandardEntryCount()} " +
                      $"everyPlaced={Village5DesignCatalog.IsEveryBuildingPlacedStandard()}");
        }

        // ────────────────────────── 표준 플롯 ──────────────────────────

        /// <summary>
        /// 표준 플롯 — [145차 P5-EDGE] 미니 지형 메시(박스 패치 대체) + 카탈로그 좌표 배치.
        /// 중심부 미세 FBM 굴곡(진폭 ≤0.3m, 주파수 0.08, seed=설계 id 해시) + 가장자리 falloff
        /// (경계에서 주변 기저 StandardEdgeBaseY = GROUND_BASE−0.1로 smoothstep 하강 — SetupGround/
        /// SurfaceY 실측: 주변 지면 = 1 + GetHeightAt(0~1) → 기저 기본값 승인치 사용).
        /// 접지 샘플러 = 메시 정점과 동일 StandardGroundHeight 단일소스(고산 SampleHeightWithPads와
        /// 동일 구조 — PlaceEntries/나무/흙길/기초/디퍼링/섀도우 전부 공유).
        /// [145차 P3-STD] 지면 Idyllic 잔디(east_grass1_albedo) + 흙길/자연 산포 유지.
        /// </summary>
        private static GameObject BuildStandardPlot(Transform root, Village5Design design, Vector3 center)
        {
            var plot = new GameObject($"Village5_{design.Id}");
            plot.transform.SetParent(root, false);
            plot.transform.position = center;

            // [145차 P5-EDGE] 미니 지형 메시 — 중심 FBM 굴곡 + 가장자리 falloff, MeshCollider(고산 선례)
            System.Func<float, float, float> sampler = StandardGroundSampler(design);
            BuildTerrainMeshCore(plot.transform, design, $"{design.Id}_Terrain", sampler, new Color(0.42f, 0.55f, 0.30f));

            BuildStandardDirtPaths(plot.transform, design, sampler);                        // [145차 P5-CURVE] 곡선 보행로
            // [145차 P4-PAD] 표준 플롯은 중심 평탄+미세 굴곡 메시 — 평탄화 패드 A 해당 없음(기록).
            //   기초 링/디퍼링/블롭(B/C/E)은 PlaceEntries 공통 경로에서 표준/고산 모두 적용된다.
            BuildNatureScatter(plot.transform, design, sampler);                   // [145차 P3-NATURE] 결정론 산포
            BuildGrassFlowerScatter(plot.transform, design, sampler);              // [145차 P5-GRASS] 잔디/꽃 정적 산포
            BuildTexturePatches(plot.transform, design, sampler);                  // [145차 P5-TEXMIX] 이끼 패치
            PlaceEntries(plot.transform, design, sampler);
            Debug.Log($"[145차 P2] [Village5DesignRenderer] 표준 플롯 {design.Id}({design.Name}) — entries={design.Entries.Length} " +
                      $"sampler=FBM+edgeFalloff edgeBaseY={StandardEdgeBaseY:F2}");
            return plot;
        }

        /// <summary>
        /// [145차 P5-EDGE] 표준 접지 샘플러 팩토리 — 미니 지형 메시 정점과 건물/산포/흙길/기초/디퍼링/
        /// 섀도우 접지가 이 단일 함수를 공유한다(고산 SampleHeightWithPads와 동일 단일소스 계약 — 침하 0).
        /// </summary>
        private static System.Func<float, float, float> StandardGroundSampler(Village5Design design)
        {
            int seed = GetHash(design.Id + "_ground145");
            return (x, z) => StandardGroundHeight(design, x, z, seed);
        }

        /// <summary>[145차 P5-EDGE] 표준 플롯 가장자리 기저 높이 — 주변 지면 레벨(기본 GROUND_BASE−0.1).</summary>
        private const float StandardEdgeBaseY = GROUND_BASE - 0.1f;
        /// <summary>[145차 P5-EDGE] 중심부 미세 FBM 굴곡 진폭 상한(m).</summary>
        private const float StandardBumpAmp = 0.3f;
        /// <summary>[145차 P5-EDGE] 미세 굴곡 기본 주파수 — 파장 ~12.5m(완만한 언덕 결).</summary>
        private const float StandardBumpFreq = 0.08f;
        /// <summary>[145차 P5-EDGE] 가장자리 falloff 개시(정규 사각 반경 0~1 — half 기준).</summary>
        private const float EdgeFalloffStart = 0.72f;

        /// <summary>
        /// [145차 P5-EDGE] 표준 플롯 지면 높이 — 중심부 2옥타브 밸류 노이즈 굴곡(진폭 ≤0.3m) +
        /// 가장자리 smoothstep falloff(StandardEdgeBaseY로 하강). 메시/접지 단일소스.
        /// </summary>
        private static float StandardGroundHeight(Village5Design design, float x, float z, int seed)
        {
            float half = design.PlotSize * 0.5f;
            float n = ValueNoise(seed, x * StandardBumpFreq, z * StandardBumpFreq) * 0.65f
                    + ValueNoise(seed + 101, x * StandardBumpFreq * 2.13f, z * StandardBumpFreq * 2.13f) * 0.35f;
            float h = GROUND_BASE + (n - 0.5f) * 2f * StandardBumpAmp;
            float dEdge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / half;
            float t = Mathf.SmoothStep(EdgeFalloffStart, 1f, dEdge);
            return Mathf.Lerp(h, StandardEdgeBaseY, t);
        }

        /// <summary>[145차 P5-EDGE] 결정론 밸류 노이즈 — 정수 해시 격자 smoothstep 보간(Random 0건 계약).</summary>
        private static float ValueNoise(int seed, float x, float z)
        {
            int xi = Mathf.FloorToInt(x), zi = Mathf.FloorToInt(z);
            float tx = x - xi, tz = z - zi;
            float sx = tx * tx * (3f - 2f * tx), sz = tz * tz * (3f - 2f * tz);
            float a = NoiseHash(seed, xi, zi), b = NoiseHash(seed, xi + 1, zi);
            float c = NoiseHash(seed, xi, zi + 1), d = NoiseHash(seed, xi + 1, zi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sz);
        }

        /// <summary>[145차 P5-EDGE] 격자 정수 해시 → [0,1) — xorshift 계열 혼합(결정론).</summary>
        private static float NoiseHash(int seed, int x, int z)
        {
            unchecked
            {
                int h = seed ^ (x * 374761393) ^ (z * 668265263);
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFF) / 8388608f;
            }
        }

        /// <summary>
        /// [145차 P3-STD] Idyllic 잔디 지면 머티리얼 팩토리 — 메인씬 동일 텍스처(east_grass1_albedo).
        /// 표준 지면 패치/고산 메시 공용(단일소스). 텍스처 로드 실패 시 null(호출부가 단색 폴백).
        /// </summary>
        private static Material CreateIdyllicGrassMaterial(string matName)
        {
            var grassTex = Resources.Load<Texture2D>("Models/UserProvided/terrain/textures_idyllic/east_grass1_albedo");
            if (grassTex == null) return null;
            var mat = new Material(Shader.Find("Standard"));
            mat.mainTexture = grassTex;
            mat.name = matName;
            return mat;
        }

        /// <summary>[145차 P3-STD] 표준 플롯 흙길 — 진입점→광장 곡선 + 광장 링(고산과 동일 구성, 접지 = 샘플러).</summary>
        private static void BuildStandardDirtPaths(Transform plot, Village5Design design, System.Func<float, float, float> sampler)
        {
            BuildDirtPathCore(plot, design, sampler);
        }

        // ────────────────────────── 고산 플롯 ──────────────────────────

        /// <summary>
        /// 고산 플롯 — 미니 지형 메시(프로파일 단일소스) + 수면 디스크 + 목교 + 고원 상면 배치.
        /// </summary>
        private static GameObject BuildHighlandPlot(Transform root, Village5Design design, Vector3 center)
        {
            var profile = design.Profile;
            var plot = new GameObject($"Village5_{design.Id}");
            plot.transform.SetParent(root, false);
            plot.transform.position = center;

            // [145차 P4-PAD] ① 자연 산포 후보 위치를 먼저 결정 — 기존 산포 로직 이동, 자연 샘플러로
            //   판정해 위치 결과 불변(패드는 이후 단계에서만 지형을 평탄화).
            List<NatureSpot> treeSpots, bushSpots;
            ComputeNaturePositions(design, (x, z) => profile.SampleHeight(x, z), out treeSpots, out bushSpots);

            // [145차 P4-PAD] ② 패드 목록 = 건물 엔트리 전부 + 나무 각 1건 + 다리 양끝 2건 —
            //   padY는 전부 자연 SampleHeight 채취(결정론).
            List<Village5TerrainPad> pads = BuildHighlandPads(design, profile, treeSpots);

            // ③ 패드 포함 샘플러 — 미니 지형 메시/흙길/다리/건물/나무 배치 전부 동일 래퍼 사용
            //   (메시와 접지가 같은 함수 — 단일소스 계약 유지, 침하 0).
            System.Func<float, float, float> sampler =
                (x, z) => profile.SampleHeightWithPads(x, z, pads);

            BuildHighlandMesh(plot.transform, design, profile, sampler);   // 메시 = 패드 샘플러(빌드 순서 계약)
            BuildWaterDisk(plot.transform, design, profile);
            BuildDirtPaths(plot.transform, design, profile, sampler);
            BuildBridge(plot.transform, design, profile, sampler);
            BuildShoreScatter(plot.transform, design, profile, sampler);   // [145차 P4-SHORE] 수변 갈대
            BuildWaterFinish(plot.transform, design, profile, sampler);    // [145차 P5-WATERFX] 수련/수변 바위
            BuildTexturePatches(plot.transform, design, sampler);          // [145차 P5-TEXMIX] 이끼+바위 노출 패치
            BuildGrassFlowerScatter(plot.transform, design, sampler);      // [145차 P5-GRASS] 잔디/꽃 정적 산포
            PlaceNature(plot.transform, design, treeSpots, bushSpots, sampler);   // [145차 P4-PAD] 사전 결정 위치
            PlaceEntries(plot.transform, design, sampler);

            Debug.Log($"[145차 P2] [Village5DesignRenderer] 고산 플롯 {design.Id}({design.Name}) — entries={design.Entries.Length} " +
                      $"pads={pads.Count} " +
                      $"plateauTop={profile.PlateauTopY:F1} waterY={profile.WaterY:F1} basinBottom={profile.BasinBottomY:F1}");
            return plot;
        }

        /// <summary>
        /// [145차 P4-PAD] 고산 평탄화 패드 목록 — ①건물 엔트리 전부(중심=LocalX/LocalZ,
        /// radius=max(footprint/2+1.5, 2.5), padY=자연 SampleHeight(center)) ②나무 각 1건
        /// (radius 1.6, padY=자연 높이) ③다리 양끝 2건(radius 2.5, padY=자연 높이).
        /// 표준 플롯은 중심 평탄+미세 굴곡 메시 — 패드 A 해당 없음(기록).
        /// </summary>
        private static List<Village5TerrainPad> BuildHighlandPads(Village5Design design, Village5HighlandProfile profile, List<NatureSpot> treeSpots)
        {
            var pads = new List<Village5TerrainPad>();

            // ① 건물 패드 — footprint(TargetHeight×1.2×scaleMul) 절반 + 1.5m 여유, 최소 2.5m
            foreach (var e in design.Entries)
            {
                var def = Village5DesignCatalog.Get(e.BuildingId);
                if (def == null) continue;
                float footprint = def.TargetHeight * Village5DesignCatalog.FootprintFactor * e.ScaleMul;
                float radius = Mathf.Max(footprint * 0.5f + 1.5f, 2.5f);
                pads.Add(new Village5TerrainPad(e.LocalX, e.LocalZ, radius, profile.SampleHeight(e.LocalX, e.LocalZ)));
            }

            // ② 나무 패드 — 산포 확정 위치 각 1건(radius 1.6)
            foreach (var t in treeSpots)
                pads.Add(new Village5TerrainPad(t.Pos.x, t.Pos.z, 1.6f, profile.SampleHeight(t.Pos.x, t.Pos.z)));

            // ③ 다리 양끝 패드 — 목교 상·하륙지 평탄(끊김 없는 보행 계약)
            pads.Add(new Village5TerrainPad(profile.BridgeX0, profile.BridgeZ0, 2.5f, profile.SampleHeight(profile.BridgeX0, profile.BridgeZ0)));
            pads.Add(new Village5TerrainPad(profile.BridgeX1, profile.BridgeZ1, 2.5f, profile.SampleHeight(profile.BridgeX1, profile.BridgeZ1)));

            return pads;
        }

        /// <summary>
        /// 고산 미니 지형 메시 — [145차 P5-EDGE] 공용 BuildTerrainMeshCore 위임(고산/표준 단일 팩토리).
        /// </summary>
        private static void BuildHighlandMesh(Transform plot, Village5Design design, Village5HighlandProfile profile, System.Func<float, float, float> groundSampler)
        {
            BuildTerrainMeshCore(plot, design, $"{design.Id}_HighlandTerrain", groundSampler, new Color(0.38f, 0.50f, 0.27f));
        }

        /// <summary>
        /// [145차 P5-EDGE] 미니 지형 메시 공용 코어 — 65×65 정점 생성(플롯 로컬 y = 샘플러 높이) +
        /// MeshCollider(고산 선례 — 표준/고산 공통 보행 가능). 샘플러 파라미터화: 고산은 패드 포함
        /// 래퍼(SampleHeightWithPads), 표준은 StandardGroundHeight — 메시 정점과 접지가 같은 함수를
        /// 공유(단일소스 계약 유지, 침하 0).
        /// </summary>
        private static void BuildTerrainMeshCore(Transform plot, Village5Design design, string terrainGoName, System.Func<float, float, float> groundSampler, Color fallbackColor)
        {
            int vertsPerSide = MeshSegments + 1;
            var vertices = new Vector3[vertsPerSide * vertsPerSide];
            var triangles = new int[MeshSegments * MeshSegments * 6];

            float half = design.PlotSize * 0.5f;
            float step = design.PlotSize / MeshSegments;

            int vi = 0;
            for (int zi = 0; zi < vertsPerSide; zi++)
            {
                for (int xi = 0; xi < vertsPerSide; xi++)
                {
                    float x = -half + xi * step;
                    float z = -half + zi * step;
                    vertices[vi++] = new Vector3(x, groundSampler(x, z), z);   // 접지와 동일 샘플러(단일소스 — [145차 P4-PAD] 패드 포함)
                }
            }

            int ti = 0;
            for (int zi = 0; zi < MeshSegments; zi++)
            {
                for (int xi = 0; xi < MeshSegments; xi++)
                {
                    int row = zi * vertsPerSide;
                    int a = row + xi;
                    int b = row + xi + 1;
                    int c = row + vertsPerSide + xi;
                    int d = row + vertsPerSide + xi + 1;
                    triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = b;
                    triangles[ti++] = b; triangles[ti++] = c; triangles[ti++] = d;
                }
            }

            var mesh = new Mesh();
            mesh.name = $"{terrainGoName}_Mesh";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var terrainGo = new GameObject(terrainGoName);
            terrainGo.transform.SetParent(plot, false);
            terrainGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = terrainGo.AddComponent<MeshRenderer>();
            // [145차 P2b] Idyllic 지면 — [145차 P3-STD] 공용 팩토리(east_grass1_albedo), 실패 시 프리미티브 폴백.
            var grassMat = CreateIdyllicGrassMaterial($"Village5_{design.Id}_Terrain_Idyllic");
            mr.material = grassMat != null
                ? grassMat
                : MaterialHelper.CreateLitMaterial(fallbackColor, $"Village5_{design.Id}_Terrain");
            terrainGo.AddComponent<MeshCollider>().sharedMesh = mesh;   // 보행 가능(고원 상면/경사로 — [145차 P5-EDGE] 표준도 동일)
        }

        /// <summary>수면 디스크 — 분지 중심 원판(호수 원판 선례), 상면이 WaterY가 되도록.</summary>
        private static void BuildWaterDisk(Transform plot, Village5Design design, Village5HighlandProfile profile)
        {
            float radius = profile.BasinRadius + 3f;   // 완사면 걸침 — 수변 시각 안정
            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = $"{design.Id}_Water";
            water.transform.SetParent(plot, false);
            // 실린더 기본: 지름 1 × 높이 2 — 스케일 (2r, 0.05, 2r) → 두께 0.1m
            water.transform.localPosition = new Vector3(profile.BasinCenterX, profile.WaterY - 0.05f, profile.BasinCenterZ);
            water.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
            var wr = water.GetComponent<MeshRenderer>();
            if (wr != null)
            {
                // [145차 P2b] 메인씬 동일 Idyllic 워터 — LakeGenerator와 같은 WaterMaterialUpgrader 팩토리
                //   (Idyllic Water 텍스처+UV 흐름+반투명). 실패 시 기존 단색 폴백(프리미티브 폴백 계약 준용).
                var waterMat = WaterMaterialUpgrader.CreateNationTintedWaterMaterial(
                    $"Village5_{design.Id}_Water_Idyllic",
                    plot.position + new Vector3(profile.BasinCenterX, profile.WaterY, profile.BasinCenterZ));
                wr.material = waterMat != null
                    ? waterMat
                    : MaterialHelper.CreateLitMaterial(new Color(0.22f, 0.44f, 0.62f), $"Village5_{design.Id}_Water");
            }
        }


        /// <summary>
        /// [145차 P2b] Idyllic 흙길 — 고산 플롯 보행로. 브릿지 고원 상륙지(p0)→고원 광장 직선 + 광장 링.
        /// 지면 스트립(2m 스텝 쿼드, 폭 2.4m)으로 미니 지형을 따라가며, 텍스처는 메인씬 L4 dirt_path와
        /// 동일 Idyllic 소스(west_dirt_albedo — TerrainLayerDef IDYLLIC_FOLDER 실존 확인). 색 어휘는
        /// TerrainPathGenerator.GetPathColor(BiomeType.Plains) 갈색 계약과 동일 톤. 결정론(Random 0건).
        /// [145차 P3-STD] 폴리라인 생성을 BuildDirtPathCore로 추출 — 표준(진입점→광장)과 공용.
        /// </summary>
        private static void BuildDirtPaths(Transform plot, Village5Design design, Village5HighlandProfile profile, System.Func<float, float, float> groundSampler)
        {
            BuildDirtPathCore(plot, design, groundSampler);   // [145차 P4-PAD] 패드 포함 래퍼 샘플러
        }

        /// <summary>
        /// [145차 P3-STD] 흙길 접근 폴리라인 2점(진입점→광장) — 흙길 생성과 산포 제약 검증의 단일소스.
        /// 고산 = 브릿지 상륙지→고원 중심(P2b 계약 유지). 표준 = 성문(Gatehouse 류 Landmark) 위치,
        /// 없으면 플롯 가장자리 외곽 진입점(광장 반대편 진입 — StandardEntryDir 결정 표).
        /// </summary>
        private static void DirtPathEndpoints(Village5Design design, out Vector3 p0, out Vector3 plaza)
        {
            if (design.Type == Village5Type.Highland && design.Profile != null)
            {
                p0 = new Vector3(design.Profile.BridgeX0, 0f, design.Profile.BridgeZ0);
                plaza = new Vector3(design.Profile.PlateauCenterX, 0f, design.Profile.PlateauCenterZ);
                return;
            }

            float px, pz;
            Village5DesignCatalog.PlazaCenter(design, out px, out pz);
            plaza = new Vector3(px, 0f, pz);

            p0 = StandardEntryDir(design.Id) * (design.PlotSize * 0.5f - 3f);
            foreach (var e in design.Entries)
            {
                var def = Village5DesignCatalog.Get(e.BuildingId);
                if (def != null && def.Role == Village5Role.Landmark && def.Id.Contains("Gatehouse"))
                {
                    p0 = new Vector3(e.LocalX, 0f, e.LocalZ);
                    break;
                }
            }
        }

        /// <summary>
        /// [145차 P3-STD] 무성문 표준 마을 외곽 진입 방향(플롯 로컬 단위벡터) — 건물 회피 결정 표.
        /// 기본 +Z(성문 진입 관례 — V1/V4 남 성문과 동일 축). V3 = -X(남측 용광로 BlacksmithForge
        /// (0,18) 회피). 나머지(V2/V5)는 +Z 통과 — V2는 ArchBell(0,14)이 경로 위 아치 관문 연출.
        /// </summary>
        private static Vector3 StandardEntryDir(string designId)
        {
            if (designId == "V3") return new Vector3(-1f, 0f, 0f);
            return new Vector3(0f, 0f, 1f);
        }

        /// <summary>
        /// [145차 P3-STD] 흙길 공용 코어 — [145차 P5-CURVE] 사인 곡선 접근로(가변 폭) + 가지길(광장→Landmark)
        /// + 광장 링. 폴리라인은 CachedPaths 캐시 단일소스 — 흙길 스트립과 산포 제약(나무/덤불 3m,
        /// 잔디/꽃 1.5m)이 동일 곡선 좌표를 공유한다. 접지 샘플러 파라미터화(표준 = StandardGroundHeight /
        /// 고산 = SampleHeightWithPads 래퍼). 결정론(Random 0건).
        /// </summary>
        private static void BuildDirtPathCore(Transform plot, Village5Design design, System.Func<float, float, float> groundSampler)
        {
            // [145차 P4-DIRTDECAL] 텍스처 로드는 공용 팩토리 CreateDirtMaterial로 이관(동일 west_dirt 소스).

            var cp = CachedPaths(design);   // [145차 P5-CURVE] 곡선 폴리라인 단일소스(캐시)
            BuildDirtStrip(plot, design, cp.Approach, cp.ApproachHalfWidths, $"{design.Id}_DirtPath_Approach", groundSampler);
            if (cp.Branch != null && cp.Branch.Count >= 2)
                BuildDirtStrip(plot, design, cp.Branch, cp.BranchHalfWidths, $"{design.Id}_DirtPath_Branch", groundSampler);

            // 광장 링 — 반경 PlazaRadius+1.2, 24분할 결정론(직선 링 유지).
            Vector3 p0, plaza;
            DirtPathEndpoints(design, out p0, out plaza);
            const int ringSegs = 24;
            float ringR = Village5DesignCatalog.PlazaRadius + 1.2f;
            var ring = new List<Vector3>();
            for (int i = 0; i <= ringSegs; i++)
            {
                float a = i * Mathf.PI * 2f / ringSegs;
                ring.Add(plaza + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringR);
            }

            BuildDirtStrip(plot, design, ring, null, $"{design.Id}_DirtPath_PlazaRing", groundSampler);
        }

        /// <summary>[145차 P5-CURVE] 결정론 흙길 폴리라인 캐시(설계 id별 1회 계산 — 스트립/제약 공유).</summary>
        private class CachedDirtPath
        {
            public List<Vector3> Approach;
            public List<float> ApproachHalfWidths;
            public List<Vector3> Branch;
            public List<float> BranchHalfWidths;
        }

        private static readonly Dictionary<string, CachedDirtPath> _dirtPathCache = new Dictionary<string, CachedDirtPath>();

        /// <summary>[145차 P5-CURVE] 폴리라인 캐시 접근 — approach(진입점→광장 곡선)/branch(광장→Landmark) 반환.</summary>
        private static void GetCachedPaths(Village5Design design, out List<Vector3> approach, out List<Vector3> branch)
        {
            var cp = CachedPaths(design);
            approach = cp.Approach;
            branch = cp.Branch;
        }

        /// <summary>
        /// [145차 P5-CURVE] 사인 곡선 보행로 폴리라인 — TerrainPathGenerator.CurvedRoads 수학 결정론 이식:
        /// 양끝 0 봉투(Sin(frac·PI)) × 사행(Sin(frac·PI·cycles·2 + phase)). 진폭 2.5~3.5m, 주기 1~1.5,
        /// 세그먼트별 halfW 1.0~1.4(xorshift). 가지길 = 광장 링 → Landmark footprint+1.5m 지점(직선+약한 곡률).
        /// </summary>
        private static CachedDirtPath CachedPaths(Village5Design design)
        {
            CachedDirtPath cp;
            if (_dirtPathCache.TryGetValue(design.Id, out cp)) return cp;
            cp = new CachedDirtPath();

            Vector3 p0, plaza;
            DirtPathEndpoints(design, out p0, out plaza);
            Vector3 toPlaza = plaza - p0; toPlaza.y = 0f;
            float dist = toPlaza.magnitude;
            Vector3 dir = dist > 0.01f ? toPlaza / dist : Vector3.forward;
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);   // 진행 축에 수직 — 사인 옵셋 축

            int h = GetHash(design.Id + "_curve145");
            h = NextHash(h);
            float amp = 2.5f + Frac01(h) * 1.0f;             // 수직 오프셋 진폭 2.5~3.5m
            h = NextHash(h);
            float cycles = 1f + Frac01(h) * 0.5f;            // 사행 주기 1~1.5
            h = NextHash(h);
            float curvePhase = Frac01(h) * Mathf.PI * 2f;

            const float step = 2f;
            float straight = Mathf.Max(0f, dist - Village5DesignCatalog.PlazaRadius);
            cp.Approach = new List<Vector3>();
            cp.ApproachHalfWidths = new List<float>();
            int hW = GetHash(design.Id + "_pathwidth145");
            for (float d = 0f; d <= straight; d += step)
            {
                float frac = straight > 0.01f ? d / straight : 0f;
                float lat = amp * Mathf.Sin(frac * Mathf.PI) * Mathf.Sin(frac * Mathf.PI * cycles * 2f + curvePhase);
                cp.Approach.Add(p0 + dir * d + perp * lat);
                hW = NextHash(hW);
                cp.ApproachHalfWidths.Add(1.0f + Frac01(hW) * 0.4f);   // halfW 1.0~1.4 세그먼트 가변
            }

            // 가지길 — 광장 링 바깥에서 Landmark role 건물 footprint+1.5m 지점까지(직선+약간의 곡률).
            cp.Branch = new List<Vector3>();
            cp.BranchHalfWidths = new List<float>();
            Village5Placement lmEntry = null;
            Village5BuildingDef lmDef = null;
            foreach (var e in design.Entries)
            {
                var def = Village5DesignCatalog.Get(e.BuildingId);
                if (def != null && def.Role == Village5Role.Landmark) { lmEntry = e; lmDef = def; break; }
            }
            if (lmEntry != null && lmDef != null)
            {
                float lmClear = lmDef.TargetHeight * Village5DesignCatalog.FootprintFactor * lmEntry.ScaleMul * 0.5f + 1.5f;
                var lmPos = new Vector3(lmEntry.LocalX, 0f, lmEntry.LocalZ);
                Vector3 toLm = lmPos - plaza; toLm.y = 0f;
                float lmDist = toLm.magnitude;
                float ringR = Village5DesignCatalog.PlazaRadius + 1.2f;
                if (lmDist > ringR + lmClear + 2f)
                {
                    Vector3 ldir = toLm / lmDist;
                    Vector3 lperp = new Vector3(-ldir.z, 0f, ldir.x);
                    Vector3 start = plaza + ldir * ringR;
                    Vector3 end = lmPos - ldir * lmClear;
                    float blen = Vector3.Distance(start, end);
                    hW = NextHash(hW);
                    float bAmp = 0.8f;   // 약한 곡률 — 가지길
                    float bPhase = Frac01(hW) * Mathf.PI;
                    for (float d = 0f; d <= blen; d += step)
                    {
                        float frac = blen > 0.01f ? d / blen : 0f;
                        float lat = bAmp * Mathf.Sin(frac * Mathf.PI) * Mathf.Sin(frac * Mathf.PI * 2f + bPhase);
                        cp.Branch.Add(start + ldir * d + lperp * lat);
                        hW = NextHash(hW);
                        cp.BranchHalfWidths.Add(1.0f + Frac01(hW) * 0.4f);
                    }
                    cp.Branch.Add(end);
                    cp.BranchHalfWidths.Add(cp.BranchHalfWidths.Count > 0 ? cp.BranchHalfWidths[cp.BranchHalfWidths.Count - 1] : 1.2f);
                }
            }

            _dirtPathCache[design.Id] = cp;
            return cp;
        }

        /// <summary>
        /// [145차 P5-CURVE] 흙길 폴리라인 xz 거리(세그먼트 최소) — 산포 제약 검산용(곡선 폴리라인 단일소스).
        /// </summary>
        private static float DistanceToPolylineXZ(Vector3 p, List<Vector3> pts)
        {
            if (pts == null || pts.Count < 2) return float.MaxValue;
            float best = float.MaxValue;
            for (int i = 0; i < pts.Count - 1; i++)
                best = Mathf.Min(best, SegmentDistanceXZ(p, pts[i], pts[i + 1]));
            return best;
        }

        /// <summary>[145차 P2b] 흙길 스트립 — 폴리라인 따라 2m 쿼드, y=샘플러+0.03 접지 추종.
        /// [145차 P5-CURVE] halfWidths(점별 halfW 1.0~1.4) 파라미터 — null이면 기존 1.2m 고정(링 등).</summary>
        private static void BuildDirtStrip(Transform plot, Village5Design design, List<Vector3> pts, List<float> halfWidths, string stripName, System.Func<float, float, float> groundSampler)
        {
            if (pts == null || pts.Count < 2) return;
            const float defaultHalfW = 1.2f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            float vAccum = 0f;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 seg = b - a; seg.y = 0f;
                float len = seg.magnitude;
                if (len < 0.05f) continue;
                Vector3 fwd = seg / len;
                Vector3 side = new Vector3(fwd.z, 0f, -fwd.x);
                Vector3 aW = a, bW = b;
                // [145차 P5-CURVE] 세그먼트별 폭 변화 — 점별 halfW(트래페조이드 쿼드)
                float wA = halfWidths != null && halfWidths.Count > i ? halfWidths[i] : defaultHalfW;
                float wB = halfWidths != null && halfWidths.Count > i + 1 ? halfWidths[i + 1] : defaultHalfW;

                int baseIdx = verts.Count;
                verts.Add(new Vector3(aW.x - side.x * wA, groundSampler(aW.x - side.x * wA, aW.z - side.z * wA) + 0.03f, aW.z - side.z * wA));
                verts.Add(new Vector3(aW.x + side.x * wA, groundSampler(aW.x + side.x * wA, aW.z + side.z * wA) + 0.03f, aW.z + side.z * wA));
                verts.Add(new Vector3(bW.x - side.x * wB, groundSampler(bW.x - side.x * wB, bW.z - side.z * wB) + 0.03f, bW.z - side.z * wB));
                verts.Add(new Vector3(bW.x + side.x * wB, groundSampler(bW.x + side.x * wB, bW.z + side.z * wB) + 0.03f, bW.z + side.z * wB));

                uvs.Add(new Vector2(0f, vAccum)); uvs.Add(new Vector2(1f, vAccum));
                uvs.Add(new Vector2(0f, vAccum + len * 0.5f)); uvs.Add(new Vector2(1f, vAccum + len * 0.5f));
                vAccum += len * 0.5f;

                tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
            }

            if (verts.Count < 4) return;
            var stripGo = new GameObject(stripName);
            stripGo.transform.SetParent(plot, false);
            var mf = stripGo.AddComponent<MeshFilter>();
            var mesh = new Mesh { vertices = verts.ToArray(), uv = uvs.ToArray(), triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            var mr = stripGo.AddComponent<MeshRenderer>();
            var mat = CreateDirtMaterial($"Village5_{design.Id}_Dirt_Mat");   // [145차 P4-DIRTDECAL] 공용 흙 팩토리 재사용(동일 west_dirt 소스)
            mr.material = mat;
        }

        /// <summary>
        /// 목교 — Wooden Dock 1×1m 세그먼트를 폴리라인(p0→p1) 따라 연결.
        /// 데크 높이 = max(수면+0.15, 양끝 지면 보간+0.15) — 말뚝(GLB 모듈 하부)이 물에 박히고,
        /// 데크가 수면보다 1.5m 이상 높은 모듈에는 절차 지지 기둥 박스를 추가한다.
        /// </summary>
        private static void BuildBridge(Transform plot, Village5Design design, Village5HighlandProfile profile, System.Func<float, float, float> groundSampler)
        {
            Vector3 p0 = new Vector3(profile.BridgeX0, 0f, profile.BridgeZ0);
            Vector3 p1 = new Vector3(profile.BridgeX1, 0f, profile.BridgeZ1);
            float h0 = groundSampler(p0.x, p0.z);   // [145차 P4-PAD] 패드 포함 — 양끝 패드 코어 = padY
            float h1 = groundSampler(p1.x, p1.z);

            Vector3 flat = new Vector3(p1.x - p0.x, 0f, p1.z - p0.z);
            float length = flat.magnitude;
            if (length < 0.5f) return;

            int modules = Mathf.CeilToInt(length);   // 1m 모듈
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;   // 폴리라인 방향을 +Z로
            string dockPath = Village5DesignCatalog.VillageGlbDir + Village5DesignCatalog.Get("WoodenDock").FileName;
            const float deckClear = 0.15f;

            var bridgeRoot = new GameObject($"{design.Id}_Bridge");
            bridgeRoot.transform.SetParent(plot, false);
            Vector3 origin = plot.position;   // 플롯 월드 원점 — InstantiateAtGround는 월드 좌표 계약

            for (int k = 0; k <= modules; k++)
            {
                float t = (float)k / modules;
                Vector3 pos = new Vector3(Mathf.Lerp(p0.x, p1.x, t), 0f, Mathf.Lerp(p0.z, p1.z, t));
                float ground = Mathf.Lerp(h0, h1, t);
                float deckY = Mathf.Max(profile.WaterY + deckClear, ground + deckClear);

                var module = VillageBuildingCatalog.InstantiateAtGround(
                    dockPath, origin + pos, deckY - Village5DesignCatalog.Get("WoodenDock").TargetHeight,
                    Village5DesignCatalog.Get("WoodenDock").TargetHeight, $"{design.Id}_BridgeDeck_{k:D2}");
                if (module != null)
                {
                    module.transform.SetParent(bridgeRoot.transform, true);
                    module.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
                else
                {
                    // 프리미티브 폴백 — 판자 데크 박스(1×0.1×1.6m)
                    var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    plank.name = $"{design.Id}_BridgeDeck_{k:D2}_Fallback";
                    plank.transform.SetParent(bridgeRoot.transform, false);
                    plank.transform.localPosition = new Vector3(pos.x, deckY - 0.05f, pos.z);
                    plank.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    plank.transform.localScale = new Vector3(1f, 0.1f, 1.6f);
                    var lr = plank.GetComponent<MeshRenderer>();
                    if (lr != null)
                        lr.material = MaterialHelper.CreateLitMaterial(new Color(0.45f, 0.32f, 0.18f), $"Village5_{design.Id}_Plank");
                }

                // 지지 기둥(절차 박스 — 동굴 배면벽 선례): 데크가 지면/수면보다 1.5m 이상 높은 구간
                if (deckY - profile.WaterY > 1.5f && k % 4 == 0)
                {
                    float bottomY = profile.WaterY - 0.5f;   // 말뚝이 물에 박히도록
                    float postH = deckY - 0.1f - bottomY;
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = $"{design.Id}_BridgePost_{k:D2}";
                    post.transform.SetParent(bridgeRoot.transform, false);
                    post.transform.localPosition = new Vector3(pos.x, bottomY + postH * 0.5f, pos.z);
                    post.transform.localScale = new Vector3(0.3f, postH, 0.3f);
                    var ptr = post.GetComponent<MeshRenderer>();
                    if (ptr != null)
                        ptr.material = MaterialHelper.CreateLitMaterial(new Color(0.35f, 0.24f, 0.14f), $"Village5_{design.Id}_Post");
                }
            }
        }

        // ────────────────────────── [145차 P3-NATURE] 자연 산포 ──────────────────────────

        /// <summary>마을당 자연물 산포 수 — 나무 14그루(외곽 링) / 덤불 8그루(내부 곳곳).</summary>
        private const int NatureTreeCount = 14;
        private const int NatureBushCount = 8;
        /// <summary>산포 제약: 건물 footprint+2m / 흙길·광장링·다리 3m / 자연물 상호 4m.</summary>
        private const float NatureBuildingClear = 2f;
        private const float NaturePathClear = 3f;
        private const float NatureMinSpacing = 4f;
        /// <summary>후보 슬롯 수(황금각 나선표본) — 미충족 후보 스킵(배치 수 보장 아님).
        /// 128슬롯×10마을 실측 검산: 나무 140/140 · 덤불 80/80 · 제약 위반 0.</summary>
        private const int NatureCandidateSlots = 128;
        /// <summary>황금각(rad) — 결정론 나선표본 위상 간격.</summary>
        private const float NatureGoldenAngle = 2.399963f;

        // ── [145차 P5-CLUSTER] 나무 클러스터링 상수 ──
        /// <summary>마을당 클러스터 수 3~5개 / 클러스터당 나무 3~5그루 / 가우시안 유사 산포 반경 3~6m.</summary>
        private const int NatureClusterMin = 3;
        private const int NatureClusterMax = 5;
        private const int NatureClusterTreeMin = 3;
        private const int NatureClusterTreeMax = 5;
        private const float NatureClusterRadius = 3f;
        private const float NatureClusterSpread = 3f;
        /// <summary>클러스터 내부 상호 간격 완화(외곽 링 NatureMinSpacing 4m 유지 — 단독 나무 기준).</summary>
        private const float NatureClusterMinSpacing = 2.2f;
        /// <summary>덤불 페어링 간격 완화 — 나무 1.2m 이내 배치 허용(기존 덤불 수 유지).</summary>
        private const float NaturePairSpacing = 1.5f;

        // ── [145차 P5-GRASS] 잔디/꽃 정적 산포 상수 — 마을당 잔디 105(90~120 중간)/꽃 16(12~20 중간)/꽃밭 2.
        //   GO 예산(플롯당 ~350) 내: 표준 ≈ 241 / 고산 ≈ 280~300 (아래 BuildGrassFlowerScatter 주석 검산).
        private const int GrassTuftCount = 105;
        private const int FlowerCount = 16;
        private const int FlowerMeadowCount = 2;
        private const float GrassBuildingClear = 1.5f;   // 건물 footprint+1.5m(기초 링 반경 footprint×0.62도 자동 제외)
        private const float GrassPathClear = 1.5f;       // 흙길 곡선 폴리라인 1.5m
        private const float GrassMinSpacing = 0.8f;      // 상호 0.8m
        /// <summary>Idyllic 정적 산포 프리팹 풀 — Resources 실측(Grass 73종/Flowers 81종/Meadows 8종 — 사이클 사용).
        /// 로드 경로 주의: Resources.Load는 .prefab 확장자 없이(Shore 산포와 동일 계약).</summary>
        private static readonly string[] GrassTuftPrefabs = { "Grass_01", "Grass_02", "Grass_03", "Grass_1" };
        private static readonly string[] FlowerPrefabs = { "Flower_Blue_01", "Flower_Pink", "Flower_Yellow_01", "Flower_White" };
        private static readonly string[] FlowerMeadowPrefabs = { "FlowerMeadow_Pink", "FlowerMeadow_Blue" };

        // ── [145차 P5-WATERFX] 물 마감 상수 — 수련 사이클 6~9(중간 7)/수변 바위 3~5(중간 4).
        private const int WaterlilyCount = 7;
        private const int ShoreRockCount = 4;
        private static readonly string[] WaterlilyPrefabs = { "Waterlily_01", "Waterlily_02", "LilyPads_01", "LilyPads_02", "LilyPads_03" };
        private static readonly string[] ShoreRockPrefabs = { "Rock_Big_01", "Rock_Big_02", "Rock_Big_03" };
        /// <summary>[145차 P5-WATERFX] 캠프파이어 화염 프리팹(Resources/FX/Fire 실측 — 스케일 0.35~0.5).</summary>
        private const string CampfireFirePrefab = "FX/Fire/VFX_Fire_01_Big";

        // ── [145차 P5-TEXMIX] 지면 텍스처 혼합 상수 — 이끼 패치 6~10(표준 6/고산 8)/바위 노출 고산 전용 3.
        private const int MossPatchCountStandard = 6;
        private const int MossPatchCountHighland = 8;
        private const int RockPatchCountHighland = 3;
        /// <summary>패치 머티리얼 캐시 — 기초 링/디퍼링과 동일 팩토리 방식(매 빌드 1회 생성).</summary>
        private static Material _sharedMossMat;
        private static Material _sharedRockMat;

        /// <summary>Resources Idyllic 자연 프리팹 풀(실측: Trees 122종/Bushes 88종 — 3종 사이클 사용).</summary>
        private const string NatureTreeDir = "Assets/Resources/IdyllicPrefabs/Trees/";
        private const string NatureBushDir = "Assets/Resources/IdyllicPrefabs/Bushes/";
        private static readonly string[] NatureTreePrefabs =
        {
            "BroadleafTree_01_Green.prefab",
            "WillowTree_01_Green.prefab",
            "Fir_1.prefab",
        };
        private static readonly string[] NatureBushPrefabs =
        {
            "Bush_1.prefab",
            "Bush_2.prefab",
            "Bush_3.prefab",
        };

        /// <summary>
        /// [145차 P3-NATURE] 결정론 자연 산포 — 나무 14(외곽 링: 고산=고원 반경~플롯 경계,
        /// 표준=플롯 가장자리 밴드) + 덤불 8(내부 곳곳). 후보 = 황금각 나선 + 설계 id 해시 위상
        /// (VillageBuilder.GetHash 패턴 — UnityEngine.Random 금지). 제약: ①건물 footprint+2m
        /// ②흙길 폴리라인/광장 링 3m ③고산 수면/분지 밖 ④다리 폴리라인 밖 ⑤플롯 경계 내
        /// ⑥자연물 상호 4m — 미충족 후보 스킵(배치 수 보장 아님, 실배치 수만 로그).
        /// 프리팹 로드 실패 시 스킵(자연물은 프리미티브 폴백 대상 아님 — 생략이 위조보다 낫다).
        /// </summary>
        private static void BuildNatureScatter(Transform plot, Village5Design design, System.Func<float, float, float> groundSampler)
        {
            List<NatureSpot> treeSpots, bushSpots;
            ComputeNaturePositions(design, groundSampler, out treeSpots, out bushSpots);
            PlaceNature(plot, design, treeSpots, bushSpots, groundSampler);
        }

        /// <summary>[145차 P4-PAD] 산포 확정 위치 — 위치/후보 인덱스(yaw 결정론)/배치 순번(프리팹 사이클)/
        /// [145차 P5-CLUSTER] Size(목표높이 m — 클러스터 분산 5~10m).</summary>
        private struct NatureSpot
        {
            public Vector3 Pos;
            public int Candidate;
            public int Order;
            public float Size;
        }

        /// <summary>
        /// [145차 P3-NATURE] 결정론 자연 산포 후보 결정 — [145차 P5-CLUSTER] 나무 14 = 클러스터 3~5개
        /// (클러스터당 3~5그루 가우시안 유사 산포, 반경 3~6m) + 잔여 단독 나무(외곽 링 유지).
        /// 덤불 8 = 나무 페어링 우선(나무 1.2m 이내 1그루) + 잔여 내부 곳곳(기존 수 유지).
        /// 후보 = 황금각 나선 + 설계 id 해시 위상(VillageBuilder.GetHash 패턴 — UnityEngine.Random 금지).
        /// 제약: ①건물 footprint+2m ②흙길 곡선 폴리라인/광장 링 3m ③고산 수면/분지 밖 ④다리 폴리라인 밖
        /// ⑤플롯 경계 내 ⑥자연물 상호 4m(클러스터 내부 2.2m 완화) — 미충족 후보 스킵(실배치 수만 로그).
        /// [145차 P4-PAD] 기존 BuildNatureScatter의 후보 결정 단계 분리 — 고산은 패드 목록 생성 전에
        /// 먼저 호출(자연 샘플러 판정 → 위치 결과 불변), 배치는 패드 샘플러로 PlaceNature가 담당.
        /// </summary>
        private static void ComputeNaturePositions(Village5Design design, System.Func<float, float, float> groundSampler, out List<NatureSpot> treeSpots, out List<NatureSpot> bushSpots)
        {
            treeSpots = new List<NatureSpot>();
            bushSpots = new List<NatureSpot>();
            float half = design.PlotSize * 0.5f;

            // 결정론 시드 — 설계 id 문자열 해시(VillageBuilder.GetHash 동일 알고리즘).
            int seed = GetHash(design.Id + "_nature145");
            float phase = ((seed & 0xFFFF) % 360) * Mathf.Deg2Rad;
            var placed = new List<Vector3>();   // 배치된 자연물 xz(플롯 로컬) — 상호 간격 검산
            int h = seed;

            // 외곽 밴드(고산: 고원 반경+2~플롯 경계-4 / 표준: 플롯 가장자리 밴드[half-10, half-4])
            float rMin = design.Type == Village5Type.Highland && design.Profile != null
                ? design.Profile.PlateauRadius + 2f
                : half - 10f;
            float rMax = half - 4f;

            // ── [145차 P5-CLUSTER] ① 나무 클러스터 3~5개 — 중심은 외곽 밴드 황금각 슬롯 + 반경 xorshift ──
            int clusterCount = NatureClusterMin + (int)(Frac01(h = NextHash(h)) * (NatureClusterMax - NatureClusterMin + 0.999f));
            int treesLeft = NatureTreeCount;
            for (int c = 0; c < clusterCount && treesLeft > NatureClusterTreeMin; c++)
            {
                h = NextHash(h);
                float cr = Mathf.Lerp(rMin, rMax, Frac01(h));
                h = NextHash(h);
                float ca = phase + c * NatureGoldenAngle * 2.3f + Frac01(h) * 1.2f;
                var center = new Vector3(Mathf.Cos(ca) * cr, 0f, Mathf.Sin(ca) * cr);
                if (!NatureSpotValid(design, center, groundSampler, placed, true, NatureMinSpacing)) continue;

                h = NextHash(h);
                int members = NatureClusterTreeMin + (int)(Frac01(h) * (NatureClusterTreeMax - NatureClusterTreeMin + 0.999f));
                members = Mathf.Min(members, treesLeft - 2);   // 단독 나무 최소 2그루 예약 — 외곽 링 유지
                if (members < NatureClusterTreeMin) continue;

                int placedInCluster = 0;
                for (int k = 0; k < members; k++)
                {
                    h = NextHash(h); float u1 = Frac01(h);
                    h = NextHash(h); float u2 = Frac01(h);
                    h = NextHash(h); float ang = Frac01(h) * Mathf.PI * 2f;
                    // 가우시안 유사 산포 — 균등 2합 평균(중심 쏠림), 반경 3~6m
                    float radius = NatureClusterRadius + (u1 + u2) * 0.5f * NatureClusterSpread;
                    var pos = center + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
                    if (!NatureSpotValid(design, pos, groundSampler, placed, true, NatureClusterMinSpacing)) continue;
                    placed.Add(pos);
                    h = NextHash(h);
                    treeSpots.Add(new NatureSpot { Pos = pos, Candidate = c * 10 + k, Order = treeSpots.Count, Size = 5f + Frac01(h) * 5f });   // 5~10m
                    placedInCluster++; treesLeft--;
                }
                if (placedInCluster > 0)
                {
                    placed.Add(center);   // 클러스터 중심 앵커 — 단독 나무/덤불이 밀착하지 않도록
                }
            }

            // ── ② 단독 나무 — 잔여를 외곽 링(기존 황금각 루프 유지) ──
            for (int i = 0; i < NatureCandidateSlots && treeSpots.Count < NatureTreeCount; i++)
            {
                h = NextHash(h);
                if (rMax <= rMin + 1f) break;
                float r = Mathf.Lerp(rMin, rMax, Frac01(h));
                var pos = new Vector3(Mathf.Cos(phase + i * NatureGoldenAngle) * r, 0f, Mathf.Sin(phase + i * NatureGoldenAngle) * r);
                if (!NatureSpotValid(design, pos, groundSampler, placed, true, NatureMinSpacing)) continue;
                placed.Add(pos);
                h = NextHash(h);
                treeSpots.Add(new NatureSpot { Pos = pos, Candidate = i, Order = treeSpots.Count, Size = 5f + Frac01(h) * 5f });   // [145차 P5-CLUSTER] 5~10m
            }

            // ── ③ 덤불 — 나무 페어링 우선(나무 1.2m 이내 1그루) + 잔여 내부 곳곳(기존 수 유지) ──
            float phase2 = phase + 1.7f;
            for (int i = 0; i < NatureCandidateSlots && bushSpots.Count < NatureBushCount; i++)
            {
                h = NextHash(h);
                bool tryPair = treeSpots.Count > 0 && Frac01(h) < 0.7f;   // 70% 슬롯 페어링 시도 — 결정론
                Vector3 pos = Vector3.zero;
                bool ok = false;
                if (tryPair)
                {
                    h = NextHash(h);
                    Vector3 tp = treeSpots[(h & 0x7FFF) % treeSpots.Count].Pos;
                    h = NextHash(h);
                    float pa = Frac01(h) * Mathf.PI * 2f;
                    h = NextHash(h);
                    float pr = 0.5f + Frac01(h) * 0.7f;   // 나무 0.5~1.2m 이내
                    pos = tp + new Vector3(Mathf.Cos(pa) * pr, 0f, Mathf.Sin(pa) * pr);
                    ok = NatureSpotValid(design, pos, groundSampler, placed, false, NaturePairSpacing);
                }
                if (!ok)
                {
                    h = NextHash(h);
                    float r = Mathf.Lerp(Village5DesignCatalog.PlazaRadius + 2f, half - 4f, Frac01(h));
                    pos = new Vector3(Mathf.Cos(phase2 + i * NatureGoldenAngle) * r, 0f, Mathf.Sin(phase2 + i * NatureGoldenAngle) * r);
                    ok = NatureSpotValid(design, pos, groundSampler, placed, false, NatureMinSpacing);
                }
                if (ok)
                {
                    placed.Add(pos);
                    bushSpots.Add(new NatureSpot { Pos = pos, Candidate = i, Order = bushSpots.Count });
                }
            }
        }

        /// <summary>
        /// [145차 P4-PAD] 산포 배치 — ComputeNaturePositions가 확정한 위치에 프리팹 인스턴스화.
        /// 고산은 패드 포함 래퍼 샘플러로 접지(메시/접지 단일소스), 표준은 StandardGroundHeight 샘플러.
        /// [145차 P4-BLOB] 나무마다 로컬 블롭 섀도우(3.0m) 추가.
        /// [145차 P5-CLUSTER] 나무 크기 = spot.Size(5~10m 클러스터 분산 — 기존 6~9m 인덱스 기반 대체).
        /// 프리팹 로드 실패 시 스킵.
        /// </summary>
        private static void PlaceNature(Transform plot, Village5Design design, List<NatureSpot> treeSpots, List<NatureSpot> bushSpots, System.Func<float, float, float> groundSampler)
        {
            var natureRoot = new GameObject($"{design.Id}_Nature");
            natureRoot.transform.SetParent(plot, false);
            Vector3 origin = plot.position;   // InstantiateAtGround는 월드 좌표 계약

            // ── 나무 ──
            int treesPlaced = 0;
            foreach (var spot in treeSpots)
            {
                var go = VillageBuildingCatalog.InstantiateAtGround(
                    NatureTreeDir + NatureTreePrefabs[treesPlaced % NatureTreePrefabs.Length],   // 인덱스 3종 사이클
                    origin + spot.Pos, groundSampler(spot.Pos.x, spot.Pos.z), spot.Size,          // [145차 P5-CLUSTER] 5~10m 결정론 크기
                    $"{design.Id}_NatureTree_{treesPlaced:D2}");
                if (go == null) continue;   // 로드 실패 — 스킵(프리미티브 폴백 없음)
                go.transform.SetParent(natureRoot.transform, true);
                go.transform.rotation = Quaternion.Euler(0f, (spot.Candidate * 47f) % 360f, 0f);
                AttachLocalBlobShadow(natureRoot.transform, $"{design.Id}_NatureTree_{treesPlaced:D2}_Blob",
                    spot.Pos, groundSampler(spot.Pos.x, spot.Pos.z) + 0.02f, 3.0f);   // [145차 P4-BLOB] 나무 3.0m
                treesPlaced++;
            }

            // ── 덤불 ──
            int bushesPlaced = 0;
            foreach (var spot in bushSpots)
            {
                var go = VillageBuildingCatalog.InstantiateAtGround(
                    NatureBushDir + NatureBushPrefabs[bushesPlaced % NatureBushPrefabs.Length],  // 인덱스 3종 사이클
                    origin + spot.Pos, groundSampler(spot.Pos.x, spot.Pos.z), 0.8f + (bushesPlaced % 5) * 0.1f,  // 0.8~1.2m
                    $"{design.Id}_NatureBush_{bushesPlaced:D2}");
                if (go == null) continue;   // 로드 실패 — 스킵(프리미티브 폴백 없음)
                go.transform.SetParent(natureRoot.transform, true);
                go.transform.rotation = Quaternion.Euler(0f, (spot.Candidate * 47f) % 360f, 0f);
                bushesPlaced++;
            }

            Debug.Log($"[145차 P3-NATURE] [Village5DesignRenderer] {design.Id} 자연 산포: " +
                      $"trees={treesPlaced}/{NatureTreeCount} bushes={bushesPlaced}/{NatureBushCount} " +
                      $"cluster=5~10m분산 pool=({NatureTreePrefabs.Length}종+{NatureBushPrefabs.Length}종, Resources Idyllic 실측)");
        }

        /// <summary>
        /// 산포 후보 검증 — ①건물 footprint+2m ②흙길 곡선 폴리라인/광장 링 3m ③고산 수면/분지 밖
        /// ④다리 폴리라인 3m ⑤플롯 경계 내(여백 2m) ⑥자연물 상호 간격(minSpacing — 외곽 링 4m,
        /// [145차 P5-CLUSTER] 클러스터 내부 2.2m·페어링 1.5m 완화). 고산 나무는 추가로
        /// 고원 반경 밖(고원 상면은 건물/덤불 영역 — "고원 반경~플롯 경계" 밴드 계약).
        /// </summary>
        private static bool NatureSpotValid(Village5Design design, Vector3 pos, System.Func<float, float, float> groundSampler, List<Vector3> placed, bool isTree, float minSpacing)
        {
            float half = design.PlotSize * 0.5f;

            // ⑤ 플롯 경계 내(여백 2m)
            if (Mathf.Abs(pos.x) > half - 2f || Mathf.Abs(pos.z) > half - 2f) return false;

            // ① 건물 footprint 보수폭(TargetHeight×1.2×scaleMul의 절반) + 2m
            foreach (var e in design.Entries)
            {
                var def = Village5DesignCatalog.Get(e.BuildingId);
                if (def == null) continue;
                float clear = def.TargetHeight * Village5DesignCatalog.FootprintFactor * e.ScaleMul * 0.5f + NatureBuildingClear;
                if (DistXZ(pos.x - e.LocalX, pos.z - e.LocalZ) < clear) return false;
            }

            // ② 흙길 폴리라인([145차 P5-CURVE] 사인 곡선 접근로+가지길 + 광장 링) 3m — 캐시 단일소스
            Vector3 entryPt, plaza;
            DirtPathEndpoints(design, out entryPt, out plaza);
            List<Vector3> approach, branch;
            GetCachedPaths(design, out approach, out branch);
            float ringOuter = Village5DesignCatalog.PlazaRadius + 1.2f + 1.4f + NaturePathClear;   // 링 반경+스트립 최대 절폭+여유
            if (DistXZ(pos.x - plaza.x, pos.z - plaza.z) < ringOuter) return false;
            if (DistanceToPolylineXZ(pos, approach) < NaturePathClear) return false;
            if (DistanceToPolylineXZ(pos, branch) < NaturePathClear) return false;

            // ⑥ 자연물 상호 간격(외곽 링 4m / 클러스터·페어링 완화 — 파라미터)
            foreach (var p in placed)
            {
                if (DistXZ(pos.x - p.x, pos.z - p.z) < minSpacing) return false;
            }

            // ③④ 고산 — 수면/분지 밖 + 다리 폴리라인 밖 + 나무는 고원 반경 밖
            if (design.Type == Village5Type.Highland && design.Profile != null)
            {
                var prof = design.Profile;
                if (DistXZ(pos.x - prof.BasinCenterX, pos.z - prof.BasinCenterZ) <
                    prof.BasinRadius + Village5HighlandProfile.BasinSkirtWidth + NaturePathClear) return false;
                if (groundSampler(pos.x, pos.z) < prof.WaterY + 0.3f) return false;   // 수면 위 육지만
                if (SegmentDistanceXZ(pos, new Vector3(prof.BridgeX0, 0f, prof.BridgeZ0),
                    new Vector3(prof.BridgeX1, 0f, prof.BridgeZ1)) < NaturePathClear) return false;
                if (isTree && DistXZ(pos.x - prof.PlateauCenterX, pos.z - prof.PlateauCenterZ) < prof.PlateauRadius + 1f) return false;
            }

            return true;
        }

        /// <summary>xz 평면 거리(양의 제곱근 1회).</summary>
        private static float DistXZ(float dx, float dz) => Mathf.Sqrt(dx * dx + dz * dz);

        /// <summary>점-선분 xz 거리(2D 투영 — 흙길/다리 폴리라인 제약 검산용).</summary>
        private static float SegmentDistanceXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            var pv = new Vector2(p.x, p.z);
            var av = new Vector2(a.x, a.z);
            var bv = new Vector2(b.x, b.z);
            Vector2 ab = bv - av;
            float lenSq = ab.sqrMagnitude;
            float t = lenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(pv - av, ab) / lenSq) : 0f;
            return Vector2.Distance(pv, av + ab * t);
        }

        /// <summary>[145차 P3-NATURE] 결정론 문자열 해시 — VillageBuilder.GetHash 동일 알고리즘(31진법).</summary>
        private static int GetHash(string input)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>xorshift32 정수 흐름 — 후보별 결정론 지터(UnityEngine.Random 대체, Random 0건 계약).</summary>
        private static int NextHash(int h)
        {
            unchecked
            {
                h ^= h << 13;
                h ^= h >> 17;
                h ^= h << 5;
                return h;
            }
        }

        /// <summary>해시 → [0,1) 실수(부호 마스크 — 음수 해시도 양수 분율).</summary>
        private static float Frac01(int h) => (h & 0x7FFFFF) / 8388608f;

        // ────────────────────────── 공통 배치 ──────────────────────────

        /// <summary>카탈로그 엔트리 전체 배치 — groundSampler(플롯 로컬 xz → 접지 y)로 접지.</summary>
        private static void PlaceEntries(Transform plot, Village5Design design, System.Func<float, float, float> groundSampler)
        {
            var buildings = new GameObject($"{design.Id}_Buildings");
            buildings.transform.SetParent(plot, false);
            Vector3 origin = plot.position;   // 플롯 월드 원점 — InstantiateAtGround는 월드 좌표 계약

            for (int i = 0; i < design.Entries.Length; i++)
            {
                var e = design.Entries[i];
                var def = Village5DesignCatalog.Get(e.BuildingId);
                if (def == null)
                {
                    Debug.LogWarning($"[145차 P2] [Village5DesignRenderer] 미등록 건물 id — 스킵: {design.Id}/{e.BuildingId}");
                    continue;
                }

                float groundY = groundSampler(e.LocalX, e.LocalZ);
                Vector3 worldPos = origin + new Vector3(e.LocalX, 0f, e.LocalZ);
                float targetHeight = def.TargetHeight * e.ScaleMul;
                float footprint = targetHeight * Village5DesignCatalog.FootprintFactor;   // [145차 P4] 기초/디퍼링/블롭 공통 치수

                // [145차 P4-DIRTDECAL] C — 흙 디퍼링 디스크(기초 링보다 아래 — 먼저 배치)
                CreateDirtPad(plot.transform, design.Id, i, e.LocalX, e.LocalZ, groundY, footprint);
                // [145차 P4-FOUNDATION] B — 코블스톤 기초 링(건물/폴백 공통)
                CreateFoundationRing(plot.transform, design.Id, i, e.LocalX, e.LocalZ, groundY, footprint);

                var go = VillageBuildingCatalog.InstantiateAtGround(
                    Village5DesignCatalog.VillageGlbDir + def.FileName, worldPos, groundY, targetHeight,
                    $"{def.Id}_{i:D2}");
                if (go != null)
                {
                    go.transform.SetParent(buildings.transform, true);
                    go.transform.rotation = Quaternion.Euler(0f, e.YawDeg, 0f);   // Y 회전 — minY 접지 불변
                }
                else
                {
                    // 프리미티브 폴백 — 역할별 색 몸통 박스(전수 배치 보장, GLB 부재 환경 대비)
                    go = CreateFallback(buildings.transform, def, e, groundY, i);
                }

                // [145차 P4-BLOB] E — 건물 블롭 섀도우(footprint×1.6, 지면+0.02 — GLB/폴백 공통)
                AttachLocalBlobShadow(plot.transform, $"Village5_{design.Id}_{i:D2}_Blob",
                    new Vector3(e.LocalX, 0f, e.LocalZ), groundY + 0.02f, footprint * 1.6f);

                // [145차 P5-WATERFX] 캠프파이어 화염 — VFX_Fire_01_Big 스케일 0.35~0.5 부착(로드 실패 스킵)
                if (def.Id == "Campfire")
                    AttachCampfireFire(plot.transform, design.Id, i, worldPos, groundY);
            }
        }

        /// <summary>프리미티브 폴백 — footprint(TargetHeight×1.2) 박스 + 지붕 박스.</summary>
        private static GameObject CreateFallback(Transform parent, Village5BuildingDef def, Village5Placement e, float groundY, int index)
        {
            float footprint = def.TargetHeight * Village5DesignCatalog.FootprintFactor * e.ScaleMul;
            var go = new GameObject($"{def.Id}_{index:D2}_Fallback");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(e.LocalX, groundY, e.LocalZ);
            go.transform.localRotation = Quaternion.Euler(0f, e.YawDeg, 0f);

            Color bodyColor = def.Role == Village5Role.Residential ? new Color(0.82f, 0.72f, 0.58f)
                : def.Role == Village5Role.Shop ? new Color(0.85f, 0.66f, 0.25f)
                : def.Role == Village5Role.Production ? new Color(0.60f, 0.55f, 0.50f)
                : def.Role == Village5Role.Landmark ? new Color(0.75f, 0.75f, 0.78f)
                : new Color(0.55f, 0.45f, 0.30f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, def.TargetHeight * e.ScaleMul * 0.4f, 0f);
            body.transform.localScale = new Vector3(footprint, def.TargetHeight * e.ScaleMul * 0.8f, footprint);
            var br = body.GetComponent<MeshRenderer>();
            if (br != null)
                br.material = MaterialHelper.CreateLitMaterial(bodyColor, $"Village5_{def.Id}_Body");

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(go.transform, false);
            roof.transform.localPosition = new Vector3(0f, def.TargetHeight * e.ScaleMul * 0.9f, 0f);
            roof.transform.localScale = new Vector3(footprint * 1.15f, def.TargetHeight * e.ScaleMul * 0.2f, footprint * 1.15f);
            var rr = roof.GetComponent<MeshRenderer>();
            if (rr != null)
                rr.material = MaterialHelper.CreateLitMaterial(Color.Lerp(bodyColor, Color.black, 0.4f), $"Village5_{def.Id}_Roof");

            return go;
        }

        // ────────────────────────── [145차 P5] 완전 자연화 ──────────────────────────

        /// <summary>
        /// [145차 P5-GRASS] 잔디/꽃 정적 산포 — 마을당 잔디 튜프 105(90~120 중간) + 개별 꽃 16(12~20 중간)
        /// + FlowerMeadow 큰 덩어리 2(1~2 중간). 스케일 0.8~1.3(xorshift32 — Random 0건).
        ///
        /// IdyllicGrassCover 재사용 판정: **미채용** — IdyllicGrassCover.Configure(Transform player)는
        /// 플레이어 추종 구조(Update 루프 + 청크 좌표가 플레이어 셀 기반 + 반경 45m 활성 컬링 + 풀 회수)라
        /// 고정 플롯 정적 산포와 계약이 다르다. 플롯은 항상 존재하는 공간이라 컬링 이점이 없고, 플레이어가
        /// 없는 테스트/쇼케이스 씬에서도 산포가 유지되어야 한다 → 정적 프리팹 산포로 대체(판정 근거 기록).
        /// 제약: 건물 footprint+1.5m(기초 링 자동 제외) / 흙길 곡선 폴리라인 1.5m / 수면·다리 밖 /
        /// 상호 0.8m / 플롯 경계 내. 프리팹 로드 실패 시 스킵(위조 금지). 콜라이더 제거(정적 데코 — GO 예산:
        /// 표준 ≈ 241 / 고산 ≈ 280~300개(브릿지 모듈 수에 따라), 상한 ~350 내 — 산포 로그 실측).
        /// </summary>
        private static void BuildGrassFlowerScatter(Transform plot, Village5Design design, System.Func<float, float, float> groundSampler)
        {
            var scatterRoot = new GameObject($"{design.Id}_GrassFlowers");
            scatterRoot.transform.SetParent(plot, false);
            Vector3 origin = plot.position;
            float half = design.PlotSize * 0.5f;
            int h = GetHash(design.Id + "_grass145");
            var placed = new List<Vector3>();
            int grassPlaced = 0, flowersPlaced = 0, meadowsPlaced = 0;

            // ── 꽃밭 덩어리 1~2 — 외곽 밴드 황금각 위치, 큰 스케일(1.0~1.4) ──
            for (int m = 0; m < FlowerMeadowCount; m++)
            {
                h = NextHash(h);
                float mr = Mathf.Lerp(6f, half - 4f, Frac01(h));
                h = NextHash(h);
                float ma = Frac01(h) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(ma) * mr, 0f, Mathf.Sin(ma) * mr);
                if (!GrassSpotValid(design, pos, groundSampler, placed)) continue;
                var prefab = Resources.Load<GameObject>("IdyllicPrefabs/Meadows/" + FlowerMeadowPrefabs[m % FlowerMeadowPrefabs.Length]);
                if (prefab == null) continue;   // 로드 실패 — 스킵(위조 금지)
                placed.Add(pos);
                h = NextHash(h);
                float mScale = 1.0f + Frac01(h) * 0.4f;
                var go = Object.Instantiate(prefab, origin + new Vector3(pos.x, groundSampler(pos.x, pos.z), pos.z), Quaternion.identity, scatterRoot.transform);
                go.name = $"{design.Id}_FlowerMeadow_{m:D2}";
                go.transform.localRotation = Quaternion.Euler(0f, Frac01(h = NextHash(h)) * 360f, 0f);
                go.transform.localScale = new Vector3(mScale, mScale, mScale);
                StripColliders(go);
                meadowsPlaced++;
            }

            // ── 잔디 튜프 105 — 황금각 나선표본 ──
            for (int i = 0; i < NatureCandidateSlots * 3 && grassPlaced < GrassTuftCount; i++)
            {
                h = NextHash(h);
                float r = Mathf.Lerp(2.5f, half - 1.8f, Frac01(h));
                h = NextHash(h);
                float a = Frac01(h) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!GrassSpotValid(design, pos, groundSampler, placed)) continue;
                var prefab = Resources.Load<GameObject>("IdyllicPrefabs/Grass/" + GrassTuftPrefabs[grassPlaced % GrassTuftPrefabs.Length]);
                if (prefab == null) continue;   // 로드 실패 — 스킵(위조 금지)
                placed.Add(pos);
                h = NextHash(h);
                float scale = 0.8f + Frac01(h) * 0.5f;   // 0.8~1.3
                var go = Object.Instantiate(prefab, origin + new Vector3(pos.x, groundSampler(pos.x, pos.z), pos.z), Quaternion.identity, scatterRoot.transform);
                go.name = $"{design.Id}_Grass_{grassPlaced:D2}";
                go.transform.localRotation = Quaternion.Euler(0f, Frac01(h = NextHash(h)) * 360f, 0f);
                go.transform.localScale = new Vector3(scale, scale, scale);
                StripColliders(go);
                grassPlaced++;
            }

            // ── 개별 꽃 16 — 잔디와 동일 제약(꽃밭 근처 선호 — 황금각 오프셋 위상) ──
            for (int i = 0; i < NatureCandidateSlots && flowersPlaced < FlowerCount; i++)
            {
                h = NextHash(h);
                float r = Mathf.Lerp(3f, half - 2f, Frac01(h));
                h = NextHash(h);
                float a = 1.3f + Frac01(h) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!GrassSpotValid(design, pos, groundSampler, placed)) continue;
                var prefab = Resources.Load<GameObject>("IdyllicPrefabs/Flowers/" + FlowerPrefabs[flowersPlaced % FlowerPrefabs.Length]);
                if (prefab == null) continue;   // 로드 실패 — 스킵(위조 금지)
                placed.Add(pos);
                h = NextHash(h);
                float scale = 0.8f + Frac01(h) * 0.5f;
                var go = Object.Instantiate(prefab, origin + new Vector3(pos.x, groundSampler(pos.x, pos.z), pos.z), Quaternion.identity, scatterRoot.transform);
                go.name = $"{design.Id}_Flower_{flowersPlaced:D2}";
                go.transform.localRotation = Quaternion.Euler(0f, Frac01(h = NextHash(h)) * 360f, 0f);
                go.transform.localScale = new Vector3(scale, scale, scale);
                StripColliders(go);
                flowersPlaced++;
            }

            Debug.Log($"[145차 P5-GRASS] [Village5DesignRenderer] {design.Id} 잔디/꽃 산포: " +
                      $"grass={grassPlaced}/{GrassTuftCount} flowers={flowersPlaced}/{FlowerCount} meadows={meadowsPlaced}/{FlowerMeadowCount} " +
                      "(IdyllicGrassCover 미채용 — 플레이어 추종 컬링 구조, 정적 프리팹 산포 대체)");
        }

        /// <summary>정적 데코 콜라이더 제거 — 잔디/꽃은 보행 충돌 대상 아님(GO 예산·물리 비용 절감).</summary>
        private static void StripColliders(GameObject go)
        {
            if (go == null) return;
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                Object.Destroy(col);
        }

        /// <summary>
        /// [145차 P5-GRASS] 잔디/꽃 산포 제약 — ①건물 footprint+1.5m ②흙길 곡선 폴리라인 1.5m
        /// ③광장 링 밖 ④상호 0.8m ⑤플롯 경계 내 ⑥고산 수면/분지/다리 밖.
        /// </summary>
        private static bool GrassSpotValid(Village5Design design, Vector3 pos, System.Func<float, float, float> groundSampler, List<Vector3> placed)
        {
            float half = design.PlotSize * 0.5f;
            if (Mathf.Abs(pos.x) > half - 1.5f || Mathf.Abs(pos.z) > half - 1.5f) return false;

            // ① 건물 footprint+1.5m — 기초 링(footprint×0.62)/디퍼링(×0.85)도 이 거리 밖(자동 제외)
            foreach (var e in design.Entries)
            {
                var def = Village5DesignCatalog.Get(e.BuildingId);
                if (def == null) continue;
                float clear = def.TargetHeight * Village5DesignCatalog.FootprintFactor * e.ScaleMul * 0.5f + GrassBuildingClear;
                if (DistXZ(pos.x - e.LocalX, pos.z - e.LocalZ) < clear) return false;
            }

            // ② 흙길 곡선 폴리라인(접근로+가지길) 1.5m — [145차 P5-CURVE] 캐시 단일소스
            List<Vector3> approach, branch;
            GetCachedPaths(design, out approach, out branch);
            if (DistanceToPolylineXZ(pos, approach) < GrassPathClear) return false;
            if (DistanceToPolylineXZ(pos, branch) < GrassPathClear) return false;

            // ③ 광장 링 밖(링 반경+스트립 최대 절폭+1.5m)
            Vector3 entryPt, plaza;
            DirtPathEndpoints(design, out entryPt, out plaza);
            float ringOuter = Village5DesignCatalog.PlazaRadius + 1.2f + 1.4f + GrassPathClear;
            if (DistXZ(pos.x - plaza.x, pos.z - plaza.z) < ringOuter) return false;

            // ④ 상호 0.8m
            foreach (var p in placed)
                if (DistXZ(pos.x - p.x, pos.z - p.z) < GrassMinSpacing) return false;

            // ⑥ 고산 — 수면/분지 밖 + 다리 폴리라인 밖
            if (design.Type == Village5Type.Highland && design.Profile != null)
            {
                var prof = design.Profile;
                if (DistXZ(pos.x - prof.BasinCenterX, pos.z - prof.BasinCenterZ) <
                    prof.BasinRadius + Village5HighlandProfile.BasinSkirtWidth + GrassPathClear) return false;
                if (groundSampler(pos.x, pos.z) < prof.WaterY + 0.3f) return false;
                if (SegmentDistanceXZ(pos, new Vector3(prof.BridgeX0, 0f, prof.BridgeZ0),
                    new Vector3(prof.BridgeX1, 0f, prof.BridgeZ1)) < GrassPathClear) return false;
            }
            return true;
        }

        /// <summary>
        /// [145차 P5-TEXMIX] 지면 텍스처 혼합 — URP Lit 단일 텍스처 제약 대응 패치 디스크 방식
        /// (위조 아님 — Resources 실존 텍스처): (a) 이끼 패치 6~10개 — east_moss_albedo 불규칙 패치
        /// 디스크(표준/고산 공통, 지면+0.012, 결정론 위치) (b) 바위 노출 — 고산 고원 가장자리(경사 급한 곳)
        /// empire_rock_albedo 패치 소수. 머티리얼은 기존 디퍼링/기초 링과 동일 팩토리 방식(정적 캐시).
        /// </summary>
        private static void BuildTexturePatches(Transform plot, Village5Design design, System.Func<float, float, float> groundSampler)
        {
            if (_sharedMossMat == null) _sharedMossMat = CreatePatchMaterial(
                "Models/UserProvided/terrain/textures_idyllic/east_moss_albedo", "Village5_MossPatch_Mat");
            var patchRoot = new GameObject($"{design.Id}_GroundTexPatches");
            patchRoot.transform.SetParent(plot, false);
            float half = design.PlotSize * 0.5f;

            int h = GetHash(design.Id + "_tex145");
            int mossCount = design.Type == Village5Type.Highland ? MossPatchCountHighland : MossPatchCountStandard;
            int placed = 0;
            for (int i = 0; i < NatureCandidateSlots && placed < mossCount; i++)
            {
                h = NextHash(h);
                float r = Mathf.Lerp(3f, half - 2.5f, Frac01(h));
                h = NextHash(h);
                float a = Frac01(h) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!PatchSpotValid(design, pos)) continue;
                h = NextHash(h);
                float radius = 1.5f + Frac01(h) * 1.5f;   // 불규칙 패치 — 반경 1.5~3m
                CreateSurfacePatchDisc(patchRoot.transform, $"{design.Id}_MossPatch_{placed:D2}", pos, radius, _sharedMossMat, groundSampler, 0.012f);
                placed++;
            }

            // 바위 노출 — 고산 전용: 고원 스커트 밴드(고원 가장자리~플롯 경계, 경사 급한 곳) 결정론 각도.
            if (design.Type == Village5Type.Highland && design.Profile != null)
            {
                if (_sharedRockMat == null) _sharedRockMat = CreatePatchMaterial(
                    "Models/UserProvided/terrain/textures_idyllic/empire_rock_albedo", "Village5_RockPatch_Mat");
                var prof = design.Profile;
                int rockPlaced = 0;
                for (int i = 0; i < NatureCandidateSlots && rockPlaced < RockPatchCountHighland; i++)
                {
                    h = NextHash(h);
                    float rr = Mathf.Lerp(prof.PlateauRadius + 1.5f, half - 4f, Frac01(h));
                    h = NextHash(h);
                    float ra = prof.PlateauCenterX * 0.1f + i * NatureGoldenAngle + Frac01(h) * 0.8f;
                    var pos = new Vector3(Mathf.Cos(ra) * rr, 0f, Mathf.Sin(ra) * rr);
                    if (!PatchSpotValid(design, pos)) continue;
                    if (groundSampler(pos.x, pos.z) < prof.WaterY + 0.3f) continue;   // 수면 아래 스킵
                    h = NextHash(h);
                    float rockRadius = 2f + Frac01(h) * 1.5f;
                    CreateSurfacePatchDisc(patchRoot.transform, $"{design.Id}_RockPatch_{rockPlaced:D2}", pos, rockRadius, _sharedRockMat, groundSampler, 0.012f);
                    rockPlaced++;
                }
            }
            Debug.Log($"[145차 P5-TEXMIX] [Village5DesignRenderer] {design.Id} 지면 텍스처 패치: moss={mossCount} rock={(design.Type == Village5Type.Highland ? RockPatchCountHighland : 0)} (실존 텍스처 패치 디스크 — 단일 텍스처 제약 대응)");
        }

        /// <summary>[145차 P5-TEXMIX] 패치 위치 검증 — 건물 footprint+0.5m/흙길 폴리라인 스트립 폭 밖(지면 데칼).</summary>
        private static bool PatchSpotValid(Village5Design design, Vector3 pos)
        {
            foreach (var e in design.Entries)
            {
                var def = Village5DesignCatalog.Get(e.BuildingId);
                if (def == null) continue;
                float clear = def.TargetHeight * Village5DesignCatalog.FootprintFactor * e.ScaleMul * 0.5f + 0.5f;
                if (DistXZ(pos.x - e.LocalX, pos.z - e.LocalZ) < clear) return false;
            }
            List<Vector3> approach, branch;
            GetCachedPaths(design, out approach, out branch);
            if (DistanceToPolylineXZ(pos, approach) < 1.7f) return false;
            if (DistanceToPolylineXZ(pos, branch) < 1.7f) return false;
            return true;
        }

        /// <summary>
        /// [145차 P5-TEXMIX] 지면 추종 패치 디스크 — 14분할 팬 메시, 정점 y = 샘플러+lift(접지 단일소스 —
        /// 기복 있는 메시에 매립, 플랫 원판 클리핑 없음). 콜라이더 없음(데칼 성격).
        /// </summary>
        private static void CreateSurfacePatchDisc(Transform parent, string name, Vector3 pos, float radius, Material mat, System.Func<float, float, float> groundSampler, float lift)
        {
            if (mat == null) return;
            const int segs = 14;
            var verts = new Vector3[segs + 2];
            var uvs = new Vector2[segs + 2];
            var tris = new int[segs * 3];
            verts[0] = new Vector3(pos.x, groundSampler(pos.x, pos.z) + lift, pos.z);
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i <= segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                float x = pos.x + Mathf.Cos(a) * radius;
                float z = pos.z + Mathf.Sin(a) * radius;
                verts[i + 1] = new Vector3(x, groundSampler(x, z) + lift, z);
                uvs[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
            }
            for (int i = 0; i < segs; i++)
            {
                tris[i * 3] = 0; tris[i * 3 + 1] = i + 2; tris[i * 3 + 2] = i + 1;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().material = mat;
        }

        /// <summary>[145차 P5-TEXMIX] 패치 머티리얼 팩토리 — 디퍼링/기초 링과 동일 팩토리 방식(Resources 실존 텍스처).</summary>
        private static Material CreatePatchMaterial(string texResource, string matName)
        {
            var mat = MaterialHelper.CreateLitMaterial(Color.white, matName);
            var tex = Resources.Load<Texture2D>(texResource);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.mainTextureScale = new Vector2(2f, 2f);
            }
            return mat;
        }

        /// <summary>
        /// [145차 P5-WATERFX] 물 마감 — (a) 수련/수련잎: Waterlily_01/02 + LilyPads_01~03 5종 사이클 7개
        /// (6~9 중간), 수면 위 y=WaterY+0.02, 분지 반경 내 결정론 (b) 수변 바위: Rock_Big_* 4개(3~5 중간),
        /// 수변 링 육지 쪽 — 갈대 각도 그리드(i·15°)와 반-offset 7.5°로 겹침 회피, 스케일 0.8~1.6.
        /// 로드 실패 스킵(위조 금지).
        /// </summary>
        private static void BuildWaterFinish(Transform plot, Village5Design design, Village5HighlandProfile profile, System.Func<float, float, float> sampler)
        {
            var finishRoot = new GameObject($"{design.Id}_WaterFinish");
            finishRoot.transform.SetParent(plot, false);
            Vector3 origin = plot.position;
            int h = GetHash(design.Id + "_water145");
            int liliesPlaced = 0, rocksPlaced = 0;

            // ── 수련/수련잎 — 분지 수면 원판 내부(반경 1.2~BasinRadius−1.0) 황금각 슬롯 ──
            for (int i = 0; i < WaterlilyCount * 2 && liliesPlaced < WaterlilyCount; i++)
            {
                h = NextHash(h);
                float r = Mathf.Lerp(1.2f, Mathf.Max(1.6f, profile.BasinRadius - 1f), Frac01(h));
                h = NextHash(h);
                float a = i * NatureGoldenAngle;
                var pos = new Vector3(profile.BasinCenterX + Mathf.Cos(a) * r, 0f, profile.BasinCenterZ + Mathf.Sin(a) * r);
                var prefab = Resources.Load<GameObject>("IdyllicPrefabs/Water/" + WaterlilyPrefabs[liliesPlaced % WaterlilyPrefabs.Length]);
                if (prefab == null) continue;   // 로드 실패 — 스킵(위조 금지)
                h = NextHash(h);
                float scale = 1.0f + Frac01(h) * 0.3f;
                var go = Object.Instantiate(prefab, origin + new Vector3(pos.x, profile.WaterY + 0.02f, pos.z), Quaternion.identity, finishRoot.transform);
                go.name = $"{design.Id}_Waterlily_{liliesPlaced:D2}";
                go.transform.localRotation = Quaternion.Euler(0f, Frac01(h = NextHash(h)) * 360f, 0f);
                go.transform.localScale = new Vector3(scale, scale, scale);
                liliesPlaced++;
            }

            // ── 수변 바위 — 갈대 링(BasinRadius+2.5, 15° 그리드)의 육지 쪽 반-offset(7.5°) 위치 ──
            for (int i = 0; i < ShoreRockCount * 2 && rocksPlaced < ShoreRockCount; i++)
            {
                float a = i * (Mathf.PI * 2f / ShoreRockCount) + Mathf.PI * 2f / (ShoreRockCount * 4f);   // 갈대 그리드와 45°/N 오프셋
                float rockR = profile.BasinRadius + 4.5f;   // 갈대(+2.5)보다 육지 쪽
                float x = profile.BasinCenterX + Mathf.Cos(a) * rockR;
                float z = profile.BasinCenterZ + Mathf.Sin(a) * rockR;
                if (sampler(x, z) < profile.WaterY + 0.3f) continue;   // 수면 아래 잠김 지점 스킵 — 육지에만 배치
                var prefab = Resources.Load<GameObject>("IdyllicPrefabs/Rocks/" + ShoreRockPrefabs[rocksPlaced % ShoreRockPrefabs.Length]);
                if (prefab == null) continue;   // 로드 실패 — 스킵(위조 금지)
                h = NextHash(h);
                float scale = 0.8f + Frac01(h) * 0.8f;   // 0.8~1.6
                var go = Object.Instantiate(prefab, origin + new Vector3(x, sampler(x, z) - 0.1f, z), Quaternion.identity, finishRoot.transform);
                go.name = $"{design.Id}_ShoreRock_{rocksPlaced:D2}";
                go.transform.localRotation = Quaternion.Euler(0f, Frac01(h = NextHash(h)) * 360f, 0f);
                go.transform.localScale = new Vector3(scale, scale, scale);
                rocksPlaced++;
            }
            Debug.Log($"[145차 P5-WATERFX] [Village5DesignRenderer] {design.Id} 물 마감: waterlilies={liliesPlaced}/{WaterlilyCount} shoreRocks={rocksPlaced}/{ShoreRockCount} (수면+0.02 / 육지 쪽 바위 — 갈대 오프셋)");
        }

        /// <summary>
        /// [145차 P5-WATERFX] 캠프파이어 화염 — Resources/FX/Fire/VFX_Fire_01_Big을 캠프파이어 소품 위치에
        /// 스케일 0.35~0.5로 부착(파티클 자동 재생 — 상태 확인 로그). 로드 실패 스킵(위조 금지).
        /// </summary>
        private static void AttachCampfireFire(Transform plot, string designId, int index, Vector3 worldPos, float groundY)
        {
            var fire = Resources.Load<GameObject>(CampfireFirePrefab);
            if (fire == null) return;   // 로드 실패 — 스킵(위조 금지)
            int h = NextHash(GetHash(designId + "_fire145") + index);
            float scale = 0.35f + Frac01(h) * 0.15f;   // 0.35~0.5
            var go = Object.Instantiate(fire, new Vector3(worldPos.x, groundY + 0.15f, worldPos.z), Quaternion.identity, plot);
            go.name = $"Village5_{designId}_{index:D2}_Campfire_FX";
            go.transform.localScale = new Vector3(scale, scale, scale);
            var ps = go.GetComponentInChildren<ParticleSystem>(true);
            Debug.Log($"[145차 P5-WATERFX] 캠프파이어 화염 부착: {go.name} scale={scale:F2} " +
                      $"particles={(ps != null ? (ps.isPlaying ? "autoplaying" : "loaded(paused)") : "none")}");
        }

        // ────────────────────────── [145차 P4] 접지감 패키지 ──────────────────────────

        /// <summary>[145차 P4-SHORE] 수변 프리팹 3종 사이클 — Resources IdyllicPrefabs/Shore 실측(Cattail 3종/Reeds 2종).</summary>
        private static readonly string[] ShorePrefabs = { "Cattail_01", "Reeds_01", "Cattail_02" };

        /// <summary>
        /// [145차 P4-SHORE] 수변 갈대 산포 — 고산 플롯 분지 수변 링(각도 15° 스텝 24지점),
        /// 반지름 = BasinRadius+2.5, y = 샘플러 높이+0.02. y가 WaterY+0.3 초과(수면 위 육지)면 스킵 —
        /// 수면 아래 잠김 지점만 배치(결정론). 프리팹 = Resources "IdyllicPrefabs/Shore/" 교대 3종 사이클,
        /// 스케일 1.0~1.4(xorshift32 — Random 0건), yaw = 접선 방향. 로드 실패 시 스킵(위조 금지).
        /// </summary>
        private static void BuildShoreScatter(Transform plot, Village5Design design, Village5HighlandProfile profile, System.Func<float, float, float> sampler)
        {
            const int shoreSteps = 24;   // 15° 스텝
            const float shoreOffset = 2.5f;   // BasinRadius+2.5 근처 수변 링
            var shoreRoot = new GameObject($"{design.Id}_Shore");
            shoreRoot.transform.SetParent(plot, false);
            Vector3 origin = plot.position;

            int h = GetHash(design.Id + "_shore145");   // 결정론 시드 — Random 0건 계약
            int placed = 0;
            for (int i = 0; i < shoreSteps; i++)
            {
                float a = i * Mathf.PI * 2f / shoreSteps;
                float x = profile.BasinCenterX + Mathf.Cos(a) * (profile.BasinRadius + shoreOffset);
                float z = profile.BasinCenterZ + Mathf.Sin(a) * (profile.BasinRadius + shoreOffset);
                float y = sampler(x, z) + 0.02f;
                if (y > profile.WaterY + 0.3f) continue;   // 수면 위 육지 — 스킵(수면 아래 잠김 지점만 배치)

                var prefab = Resources.Load<GameObject>("IdyllicPrefabs/Shore/" + ShorePrefabs[i % ShorePrefabs.Length]);   // 3종 사이클
                if (prefab == null) continue;   // 로드 실패 — 스킵(위조 금지)

                h = NextHash(h);
                float scale = 1.0f + Frac01(h) * 0.4f;   // 1.0~1.4 결정론 지터
                var go = Object.Instantiate(prefab, origin + new Vector3(x, y, z), Quaternion.identity, shoreRoot.transform);
                go.name = $"{design.Id}_Shore_{i:D2}";
                // yaw = 접선 방향(반지름 벡터에 +90° — 링을 따라 눕는 갈대 결정론 배치)
                float yaw = Mathf.Atan2(-Mathf.Sin(a), Mathf.Cos(a)) * Mathf.Rad2Deg;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = new Vector3(scale, scale, scale);
                placed++;
            }
            Debug.Log($"[145차 P4-SHORE] [Village5DesignRenderer] {design.Id} 수변 갈대: placed={placed}/{shoreSteps} " +
                      "(수면 근처 잠김 지점만 — 육지 스킵 결정론)");
        }

        /// <summary>[145차 P4-DIRTDECAL] 흙 머티리얼 공용 팩토리 — 흙길/디퍼링 디스크 동일 west_dirt_albedo 소스.</summary>
        private static Material CreateDirtMaterial(string matName)
        {
            var mat = MaterialHelper.CreateLitMaterial(Color.white, matName);
            var dirtTex = Resources.Load<Texture2D>("Models/UserProvided/terrain/textures_idyllic/west_dirt_albedo");
            if (dirtTex != null) mat.mainTexture = dirtTex;
            mat.mainTextureScale = new Vector2(1.5f, 1.5f);
            return mat;
        }

        /// <summary>[145차 P4-DIRTDECAL] 디퍼링 디스크 공유 머티리얼 — 매 빌드 1회 생성(static 캐시).</summary>
        private static Material _sharedDirtPadMat;

        /// <summary>
        /// [145차 P4-DIRTDECAL] C — 건물 발밑 흙 디퍼링 디스크: 실린더 radius=footprint×0.85,
        /// 두께 0.02, 중심 y=지면+0.01 — 기초 링(B)보다 아래(먼저 배치). 텍스처 = 기존 흙길과
        /// 동일 west_dirt_albedo(흙길 머티리얼 팩토리 재사용). 표준/고산 공통.
        /// </summary>
        private static void CreateDirtPad(Transform plot, string designId, int index, float x, float z, float groundY, float footprint)
        {
            if (_sharedDirtPadMat == null) _sharedDirtPadMat = CreateDirtMaterial("Village5_DirtPad_Mat");

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = $"Village5_{designId}_{index:D2}_DirtPad";
            disc.transform.SetParent(plot, false);
            disc.transform.localPosition = new Vector3(x, groundY + 0.01f, z);
            float r = footprint * 0.85f;
            disc.transform.localScale = new Vector3(r * 2f, 0.01f, r * 2f);   // 실린더 기본 높이 2 → 두께 0.02
            var dr = disc.GetComponent<MeshRenderer>();
            if (dr != null) dr.material = _sharedDirtPadMat;
            Object.Destroy(disc.GetComponent<Collider>());   // 데칼 성격 — 보행 콜라이더 없음
        }

        /// <summary>[145차 P4-FOUNDATION] 기초 링 공유 코블스톤 머티리얼 — 매 빌드 1회 생성(static 캐시).</summary>
        private static Material _sharedFoundationMat;

        /// <summary>
        /// [145차 P4-FOUNDATION] B — 건물 코블스톤 기초 링: 실린더 radius=footprint×0.62,
        /// 높이 0.3, 상면 = 지면+0.25(건물 바닥이 0.25m 파묻힘 — "붙여놓은 느낌" 완화).
        /// 텍스처 = empire_cobble_albedo(Resources 실존 확인). GLB/폴백 공통, 표준/고산 공통.
        /// </summary>
        private static void CreateFoundationRing(Transform plot, string designId, int index, float x, float z, float groundY, float footprint)
        {
            if (_sharedFoundationMat == null)
            {
                var cobbleTex = Resources.Load<Texture2D>("Models/UserProvided/terrain/textures_idyllic/empire_cobble_albedo");
                var mat = MaterialHelper.CreateLitMaterial(new Color(0.55f, 0.53f, 0.50f), "Village5_Foundation_Cobble");
                if (cobbleTex != null)
                {
                    mat.mainTexture = cobbleTex;
                    mat.mainTextureScale = new Vector2(2f, 1f);
                }
                _sharedFoundationMat = mat;
            }

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = $"Village5_{designId}_{index:D2}_Foundation";
            ring.transform.SetParent(plot, false);
            ring.transform.localPosition = new Vector3(x, groundY + 0.25f - 0.15f, z);   // 상면 = 지면+0.25, 높이 0.3
            float r = footprint * 0.62f;
            ring.transform.localScale = new Vector3(r * 2f, 0.15f, r * 2f);   // 실린더 기본 높이 2 → 0.3
            var fr = ring.GetComponent<MeshRenderer>();
            if (fr != null) fr.material = _sharedFoundationMat;
        }

        /// <summary>[145차 P4-BLOB] 공유 블롭 섀도우 머티리얼 — 매 빌드 1회 캐시(static 필드).</summary>
        private static Material _sharedBlobShadowMat;

        /// <summary>
        /// [145차 P4-BLOB] 로컬 정적 블롭 섀도우 — 수평 쿼드(회전 -90 X), y = 지면+0.02.
        /// BlobShadow.cs의 LateUpdate 전역 TerrainGenerator.GetHeightAt 의존을 의도적으로 피한다:
        /// 마을 GLB 플롯은 Village5HighlandProfile 미니 지형(패드 포함 래퍼)을 쓰므로 전역 지형 함수와
        /// 높이가 불일치 — 여기서는 배치 시점 로컬 샘플러 높이에 고정한 정적 1회 배치(컴포넌트 없음).
        /// 텍스처/머티리얼 = IndoorMaterialFactory.GetBlobShadowTexture()/CreateBlobShadowMat() 공유 팩토리
        /// (매 빌드 1회 캐시). 건물 = footprint×1.6 / 나무 = 3.0. 표준 = GROUND_BASE, 고산 = 래퍼 샘플러.
        /// worldPos/y는 월드 좌표 — InstantiateAtGround 접지 계약(월드 y 절대값)과 동일 기준.
        /// </summary>
        private static void AttachLocalBlobShadow(Transform parent, string name, Vector3 worldXZ, float y, float size)
        {
            if (_sharedBlobShadowMat == null) _sharedBlobShadowMat = IndoorMaterialFactory.CreateBlobShadowMat();
            if (_sharedBlobShadowMat == null) return;   // 팩토리 실패 — 스킵(위조 금지)

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.position = new Vector3(worldXZ.x, y, worldXZ.z);   // 월드 좌표 — 접지 계약 동일
            quad.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 세로 Quad → 법선 +Y 수평
            quad.transform.localScale = new Vector3(size, size, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = _sharedBlobShadowMat;
        }
    }
}
