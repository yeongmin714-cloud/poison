using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [118차 R3] 월드맵 지형 베이커 — 실측 데이터만으로 월드맵 지형 텍스처를 동기 베이크하는 static 클래스.
    ///
    /// 월드맵 양피지 위에 얹는 반투명 지형 레이어(국가 바이옴 바탕 + 강 + 호수 + 숲 나무)를
    /// 256×256 RGBA32 텍스처 1장으로 굽는다. 사용자 요구: "호수나 숲지형이면 숲 나무 물 표시".
    /// 좌표계는 월드맵 마커와 동일(캔버스 기준 %) — 배경 텍스처도 100%×100% 신축되므로
    /// 마커(left%/top%)와 픽셀 정합이 캔버스 종횡비와 무관하게 유지된다.
    ///
    /// [실측 근거 — 추측 금지 원칙, 2026-10-23 코드 실측]
    ///  - 좌표계: WorldMapWindowUTK L16-17/L68-76 — WORLD_SPAN=3200f(extent ±1600m),
    ///    WorldU=0.5+x/3200, WorldV=0.5+z/3200, left%=u*100, top%=(1-v)*100(GUI y 반전, +z 북=위).
    ///    본 베이커는 그 역변환만 사용(아래 WORLD_SPAN 상수와 NormToWorld 주석).
    ///  - 호수: TerrainGenerator.TerrainLakeDef{center/radius/depth/waterLevel}(L564-570),
    ///    Lakes(지연 초기화, L580) / LakesReady(L595) / LakesOrNull(L598).
    ///    베이커는 생성 트리거를 흘리지 않기 위해 LakesOrNull+LakesReady만 사용(재귀 가드 L592-593 존중).
    ///  - 강: TerrainGenerator.GetRiverMask(x,z)(L990-996) — 0~1(중심선 1, 둑 3m smoothstep 감쇠,
    ///    하천 없으면 0). 임계 0.5 초안. RiversOrNull은 EnsureRivers(호수 캐시 완성 후 1회, L959-964)가
    ///    채움 → 지형 생성이 선행된 런타임에서 유효(미준비 시 mask 0 → 강 레이어 자동 생략).
    ///    주의: 1px=12.5m 대비 수폭 4~7m(L890)는 서브픽셀 — 픽셀 중심 샘플링이라 강선이
    ///    점선처럼 보일 수 있음(초안, Play 피드백으로 임계/보간 조정 대상).
    ///  - 나무: TerrainModelPlacer L109-110의 나무 PlaceType 호출은 주석 처리됨(트리 미배치 →
    ///    (a) 부모 자식 열거 불가). 실제 나무는 NaturalResourceSpawner가 "NaturalResources"
    ///    컨테이너(L37/L141-162)에 배치: 벌목 나무 = ResourceNode(Wood) 200그루(전 국가 균등,
    ///    ConfigureResourceType(Wood) L452, WoodAcceptance L228) + 과일나무 = HerbPickup(L439,
    ///    별도 컴포넌트). → (b) ResourceNode 컴포넌트 스캔을 택함(구현 단순 + 유일 신뢰원).
    ///    판별: NodeType==Wood. 과일나무는 본 스캔에 미포함(초안 — 필요 시 후속 확장).
    ///  - 국가↔사분면: TerritoryDatabase.GetNationBaseAngle(L436-446) — East=0°/North=90°/
    ///    West=180°/South=270°(도 단위, Atan2(z,x)). TerrainGenerator.GenerateRivers L920의
    ///    45° 경계 섹터 판정과 동일 규약. 실측 보강: 영지 링 반경 380~1550m(GetDifficultyRingBand
    ///    L425-428), wedgeBaseDeg=nationBaseAngle(L276) — 국가 영지는 기저각 ±45° wedge 내 분포.
    ///    황제국 성은 원점 (0,0,0)(L299-324), 드라큘라 앵커는 1350m@150°(L237-241 — 서 사분면 구역).
    /// </summary>
    public static class WorldMapTerrainBaker
    {
        // [118차 R3] WorldMapWindowUTK 마커 좌표계 계약 복제 — 단일소스화는 후속.
        /// <summary>정규화 분모 = 2×extent. u = 0.5 + x/WORLD_SPAN (WorldMapWindowUTK L68-69 실측: 3200f).</summary>
        private const float WORLD_SPAN = 3200f;

        private const int Res = 256;                      // 베이크 해상도(정사각 — 캔버스 % 좌표계와 정합)
        private const float RiverMaskThreshold = 0.5f;    // 강 임계(초안): mask≥0.5 → 중심선~둑 절반
        private const float TreeRadiusPx = 1.5f;          // 나무 스탬프 반경(픽셀)
        private const float WorldPerPx = WORLD_SPAN / Res;   // 12.5m/px (실측 참고용 — 강 서브픽셀 근거)

        // 레이어 색 — [118차 톤 초안 — Play 피드백으로 조정]
        private static readonly Color GrassEast    = new Color(0.45f, 0.60f, 0.30f);   // 동(초원)
        private static readonly Color SnowNorth    = new Color(0.85f, 0.88f, 0.92f);   // 북(설산)
        private static readonly Color VolcanicWest = new Color(0.55f, 0.40f, 0.28f);   // 서(화산·황토)
        private static readonly Color DesertSouth  = new Color(0.78f, 0.70f, 0.45f);   // 남(사막)
        private static readonly Color WaterColor   = new Color(0.25f, 0.45f, 0.70f);   // 강/호수 공용 물색
        private static readonly Color TreeColor    = new Color(0.16f, 0.30f, 0.14f);   // 숲 진녹

        // 레이어 알파(사양 고정)
        private const float BiomeAlpha = 0.42f;   // ② 국가 바이옴 바탕(양피지 비침) — 0.30은 구별 어려워 0.42로 상향(118차 후속)
        private const float RiverAlpha = 0.80f;   // ③ 강
        private const float LakeAlpha  = 0.85f;   // ④ 호수
        private const float TreeAlpha  = 0.80f;   // ⑤ 나무

        private static Texture2D _texture;
        private static bool _warnedNoLakes;
        private static int _bakedTreeCount;   // [118차 후속] 캐시된 베이크의 나무 수 — 0그루 캐시 리베이크 판정용

        /// <summary>베이크 완료 여부(정적 텍스처 캐시 존재).</summary>
        public static bool Ready => _texture != null;

        /// <summary>
        /// 지형 텍스처 조회(없으면 동기 베이크 1회 + 정적 캐시). 실패 시 null 반환.
        /// 첫 호출에서 수십 ms 소요 가능(65k 픽셀 × 강 마스크) — WorldMapWindowUTK 생성 경로 전용.
        /// [118차 후속] 생성자 호출 시점(씬 로드 직후)은 ResourceNode 미스폰 → 0그루 베이크 캐시.
        ///   개방 시점(OnWindowOpen) 재호출에서 나무가 존재하면 1회 리베이크한다.
        /// </summary>
        public static Texture2D GetOrBake()
        {
            if (_texture != null)
            {
                if (_bakedTreeCount == 0 && CollectTreeXZ().Count > 0)
                {
                    try
                    {
                        Debug.Log("[118차 R3] 지형 리베이크 — 초기 캐시가 0그루(스폰 전 베이크) → 나무 포함 재베이크");
                        _texture = Bake();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[118차 R3] 지형 리베이크 실패(기존 캐시 유지): {ex}");
                    }
                }
                return _texture;
            }
            try
            {
                _texture = Bake();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[118차 R3] 지형 베이크 실패: {ex}");
                _texture = null;
            }
            return _texture;
        }

        // =====================================================================
        //  베이크
        // =====================================================================
        private static Texture2D Bake()
        {
            // 호수 캐시 — 생성 트리거 없이 조회(LakesOrNull, TerrainGenerator L598).
            var lakes = TerrainGenerator.LakesOrNull;
            bool lakesReady = TerrainGenerator.LakesReady;
            if (!lakesReady && !_warnedNoLakes)
            {
                _warnedNoLakes = true;
                Debug.LogWarning("[118차 R3] 지형 베이크 — TerrainGenerator.LakesReady=false → 호수 레이어 생략");
            }
            int lakeCount = lakes != null ? lakes.Count : 0;

            // 나무 수집 — 베이크 1회 한정 스캔(위 CollectTreeXZ 실측 주석).
            List<Vector2> trees = CollectTreeXZ();

            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false)
            {
                name = "WorldMapTerrainBake",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[Res * Res];

            // ── 픽셀 루프(①투명 → ②바이옴 → ③강 → ④호수, ⑤나무는 후속 스탬프) ──
            // 좌표 변환(마커 산식의 역변환): u=(px+0.5)/Res, v=(py+0.5)/Res
            //   → x=(u−0.5)·SPAN, z=(v−0.5)·SPAN  [WorldU/V = 0.5+w/SPAN 의 역].
            // GUI y 반전 계약: top%=(1−v)*100으로 요소 상단=북(z+). UI Toolkit 배경 텍스처는
            //   텍스처 상단(V=1)이 요소 상단에 온다 → 텍스처 V = v 그대로(상쇄, 추가 플립 불필요).
            //   SetPixels 행 0 = V 0 = 요소 하단 = 남(z−). WorldMapWindowUTK L16-17 계약과 정확히 일치.
            for (int py = 0; py < Res; py++)
            {
                float z = NormToWorld((py + 0.5f) / Res);
                int rowBase = py * Res;
                for (int px = 0; px < Res; px++)
                {
                    float x = NormToWorld((px + 0.5f) / Res);
                    Color c = NationBiomeColor(NationByAngle(x, z));   // ② 국가 바이옴 바탕
                    c.a = BiomeAlpha;

                    // ③ 강 — GetRiverMask 0~1(중심선 1, 둑 3m smoothstep — TerrainGenerator L990 실측).
                    //    하천 캐시 미준비(RiversOrNull=null)면 mask 0 → 자동 생략.
                    if (TerrainGenerator.GetRiverMask(x, z) >= RiverMaskThreshold)
                    {
                        c = WaterColor;
                        c.a = RiverAlpha;
                    }

                    // ④ 호수 — 픽셀-월드 좌표가 center 반경 내. plain radius 초안(실수역은 만 변조
                    //    rMod 최대 −8% — ApplyLakeBasins L1036-1041; 필요 시 후속 정밀화).
                    if (lakesReady && lakes != null)
                    {
                        for (int i = 0; i < lakes.Count; i++)
                        {
                            var lk = lakes[i];
                            float ldx = x - lk.center.x;
                            float ldz = z - lk.center.z;
                            if (ldx * ldx + ldz * ldz <= lk.radius * lk.radius)
                            {
                                c = WaterColor;
                                c.a = LakeAlpha;
                                break;
                            }
                        }
                    }

                    pixels[rowBase + px] = c;
                }
            }

            // ── ⑤ 나무 스탬프(최상위) — 반경 1.5px 디스크, 진녹 α0.80 ──
            for (int t = 0; t < trees.Count; t++)
            {
                // 정변환(역변환의 정확한 역): pxF=(0.5+x/SPAN)·Res − 0.5 (픽셀 중심 규약 일치)
                float pxF = (0.5f + trees[t].x / WORLD_SPAN) * Res - 0.5f;
                float pyF = (0.5f + trees[t].y / WORLD_SPAN) * Res - 0.5f;
                int cx = Mathf.RoundToInt(pxF);
                int cy = Mathf.RoundToInt(pyF);
                int r = Mathf.CeilToInt(TreeRadiusPx);
                for (int dy = -r; dy <= r; dy++)
                {
                    int yy = cy + dy;
                    if (yy < 0 || yy >= Res) continue;
                    int rowBase = yy * Res;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = cx + dx;
                        if (xx < 0 || xx >= Res) continue;
                        if (dx * dx + dy * dy > TreeRadiusPx * TreeRadiusPx) continue;
                        pixels[rowBase + xx] = new Color(TreeColor.r, TreeColor.g, TreeColor.b, TreeAlpha);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _bakedTreeCount = trees.Count;

            // 최초 베이크 1회 로그(실측값) — 사양 로그 형식 준수.
            Debug.Log($"[118차 R3] 지형 베이크 — {lakeCount}호수 {trees.Count}그루 mask임계={RiverMaskThreshold:0.##}");
            return tex;
        }

        /// <summary>정규화 좌표(0~1) → 월드: w = (n − 0.5) × SPAN — WorldU/V(0.5+w/SPAN)의 역변환.</summary>
        private static float NormToWorld(float n) => (n - 0.5f) * WORLD_SPAN;

        // =====================================================================
        //  사분면 → 국가 바이옴 (실측 계약)
        // =====================================================================
        /// <summary>
        /// 각도 → 국가(실측: TerritoryDatabase.GetNationBaseAngle L436-446 — East=0°/North=90°/
        /// West=180°/South=270°). 45° 경계 섹터 판정은 TerrainGenerator.GenerateRivers L920 방위
        /// 규약(N/E/S/W)과 동일. 원점(황제국 성)은 Atan2(0,0)=0 → East 근사(초안).
        /// </summary>
        private static NationType NationByAngle(float x, float z)
        {
            float ang = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
            if (ang >= -45f && ang < 45f) return NationType.East;      // 동 (+x)
            if (ang >= 45f && ang < 135f) return NationType.North;     // 북 (+z)
            if (ang >= -135f && ang < -45f) return NationType.South;   // 남 (−z)
            return NationType.West;                                     // 서 (−x, 135°~180°/−180°~−135°)
        }

        /// <summary>국가 → 바이옴 바탕색(118차 톤 초안). Empire/Dracula/None은 사분면 근사색.</summary>
        private static Color NationBiomeColor(NationType n)
        {
            switch (n)
            {
                case NationType.East:  return GrassEast;     // 초원
                case NationType.North: return SnowNorth;     // 설산
                case NationType.South: return DesertSouth;   // 사막
                case NationType.West:  return VolcanicWest;  // 화산·황토
                default:               return GrassEast;     // Empire(원점)/Dracula/None — 근사(초안)
            }
        }

        // =====================================================================
        //  나무 수집
        // =====================================================================
        /// <summary>
        /// 나무(벌목 Wood 노드) 월드 XZ 수집 — 베이크 1회 한정 FindObjectsByType(Unity 6 API,
        /// 프로젝트 컨벤션: PlayModeDiagnostics L155 등). 선택 근거: (a) TerrainModelPlacer 부모
        /// 자식 열거는 L109-110 나무 배치 주석으로 불가 → (b) 컴포넌트 스캔이 유일 신뢰원.
        /// 판별: ResourceNode.NodeType==Wood(실측 NaturalResourceSpawner L452 — 벌목 나무 200그루).
        /// 과일나무는 HerbPickup(L439)이라 미포함 — 초안(필요 시 후속 확장).
        /// 정렬 미지정(SortMode.None)이지만 스탬프는 공간 덮어쓰기라 순서 무관 → 결과 결정론.
        /// </summary>
        private static List<Vector2> CollectTreeXZ()
        {
            var result = new List<Vector2>(256);
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].NodeType != ResourceNode.ResourceType.Wood) continue;
                var p = nodes[i].transform.position;
                result.Add(new Vector2(p.x, p.z));
            }
            return result;
        }
    }
}
