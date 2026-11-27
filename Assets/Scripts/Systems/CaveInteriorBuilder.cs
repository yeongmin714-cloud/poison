using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [156차 본 명세 변경 — 2026-11-06] 동굴 실내 생성기 — 17꼭짓점 다각형(원형 실루엣) 폐기 →
    /// 직각 꺽임(90도) 셀맵 기반 전면 재작업. 확정 도면: docs/plans/2026-11-06_cave_cellmaps_F1-F5.txt.
    /// 사용자 지시 "직각 꺽임으로 다시 설계" — 예시 사진(내부씬 예시.PNG) 문법: 직교 꺽임만,
    /// 중앙 대형 홀+부속 구역, 개구부 연결, 얇은 벽, 그리드 정합.
    ///
    /// 1. 셀맵 5종(F1~F5) — string[] 문자 아트 상수(도면 파일과 동일), 1칸=2m, 48×36 그리드.
    ///    월드 변환: world.x=(cx−24)*2, world.z=(cy−18)*2(룸 원점 중앙 정렬).
    /// 2. 바닥 = 바닥 셀(비'#') 병합 쿼드 메시 1개(CaveFloor 유지, MeshCollider — 충돌 단일소스 계약),
    ///    노멀 +y 강제, UV=셀 단위(셀당 1 UV → 머티리얼 타일링으로 흙 텍스처 반복).
    /// 3. 벽 = 얇은 돌 벽(던전 팩 Wall GLB 폐기) — "바닥셀-비바닥셀" 공유 경계에 두께 0.5m·높이 3m 박스,
    ///    같은 직선 위 인접 세그먼트 런 머징. 내부 '#'(양쪽이 바닥)는 셀 중심선 벽 1개,
    ///    외곽(바닥 한쪽만)은 바닥 쪽 경계에. 벽 윗면 평면 캡(y=3.02, 두께 0, 콜라이더 없음 — CaveWallCap 유지).
    /// 4. 배치 = 셀맵 마커 → 월드 결정론(스캔 순: 행→열). 신규 배치의 rng 소비 0건(GemType 시드만 기존 계약).
    ///    F 중앙화로 / m 채굴노드 / C 보석상자 / c 석관(Cofin GLB, 없으면 상자 폴백) / s 상자 / K SkullCandle /
    ///    o 잔석 / v 덩굴 / w 물웅덩이(평면 수면 시각, 콜라이더 없음) / P 기둥(GLB+0.8×0.8m 장애물) /
    ///    t 횟불(층당 8 — ≤8 계약) / D 하강포탈 / U 상승포탈 / E 입구봉합.
    /// 5. tierScale 폐기(명세 변경) — 셀맵에 크기 내재화(×2 좌표 곱셈 제거, 셀맵 좌표 직접 반환).
    ///    tier 파라미터는 난이도 파라미터로만 존속(시그니처 유지 — CaveLevelDirector 호환).
    /// 6. 폐기(156차): CreatePolygonWalls/CreateAssetWalls/CreateWallCaps(구 박스 캡)/CreateEntranceTimbering/
    ///    CreateAssetFloorTiles/CreateFloorToneStrips/CreateRockWallRing(암석링 — 직각 벽라인이 경계 대체)/
    ///    PolyVertices/PolyTriangles/BuildFanIndices/IsPointInPolygon/TileKeepsPolygonCoverage(다각형 계열 전부).
    /// 7. 기존 유지 계약 — ROOM_NAME "CaveRoom", IndoorLighting.SetupIndoorLighting, CreateTorchModel/TorchFlicker,
    ///    GemType 시드 결정론, Random 0건(InteriorRandomizer 단일 시드), 시그니처 BuildCaveInterior(territoryId, tier).
    ///    ApplyFloorLayout(floorIndex) 시그니처 유지 — 셀맵 스왑으로 층 전환.
    /// 8. [158차 P3-C] 실내 고품질 — 돌판 경로(BFS 스폰→포탈)·흙 자갈 스탬프·벽면 마스너리·받침 밴드.
    /// 9. [191차] 던전 팩 바닥 타일 통일 — 전 바닥 셀에 Floor_1 시리즈 6변형 GLB 타일(결정론 변형 순환,
    ///    비주얼 전용·콜라이더 없음 — 병합 쿼드 단일소스 유지, 스태틱 배치 결합). BFS 돌판 경로 셀은 타일 배제
    ///    — 흙 낟길 대비 유지(158차 안내 계약 시각 보존). 더불어 하강 포탈 계단굴 — D 포탈 2셀 굴(깊이 1.5m)
    ///    파기(주바닥 쿼드 제외)+박스 벽 3면+Stairs_1A 축척 0.5 이동 가능 계단(MeshCollider)+아치 굴 바닥 이전
    ///    (CaveLevelPortal 트리거 동행 — 안내 문자열 유지 계약). 상승 포탈은 평지 아치 무수정.
    /// </summary>
    public static class CaveInteriorBuilder
    {
        private const string ROOM_NAME = "CaveRoom";

        // ---- 셀맵 기하 상수(156차) ----
        private const float CELL_SIZE = 2f;             // 1칸 = 2m
        private const int GRID_CENTER_X = 24;           // 48열 — world.x=(cx−24)*2
        private const int GRID_CENTER_Z = 18;           // 36행 — world.z=(cy−18)*2
        private const float WALL_THICKNESS = 0.5f;      // 얇은 돌 벽 — 셀 2m의 1/4(예시 사진 문법)
        private const float WALL_HEIGHT = 3f;
        private const float WALL_CAP_Y = 3.02f;         // 캡 상면 — 벽 상단(+0.02) 성 내부 평면캡 패턴
        private const float PILLAR_OBSTACLE = 0.8f;     // P 기둥 장애물 0.8×0.8m BoxCollider

        // ---- 스폰 상수(셀맵 월드 좌표 직접 반환 — 156차 tierScale 폐기) ----
        // F1 입구 스폰 = 셀 (23,31)(입구 통로 내부, 문 블로커 E행 cy=33에서 안쪽).
        private const float ENTRANCE_SPAWN_X = -2f;
        private const float ENTRANCE_SPAWN_Z = 26f;

        // ================================================================
        // [156차] F1~F5 셀맵 5종 — 확정 도면(docs/plans/2026-11-06_cave_cellmaps_F1-F5.txt)과 동일.
        // 범례: '#' 비바닥 · '.' 바닥 · F 중앙화로 · m 채굴노드 · C 보석상자 · c 석관 · s 상자 ·
        //        K SkullCandle · o 잔석 · v 덩굴 · w 물웅덩이(시각) · P 기둥 · t 횟불 · D 하강포탈 · U 상승포탈 · E 입구봉합
        // ================================================================

        private static readonly string[] F1Cellmap =
        {
            "################################################",
            "#..............#...............................#",
            "#...o..........#...............................#",
            "#..............#....s...K...K..................#",
            "#.t............#..............t................#",
            "#..............#...............................#",
            "#.....o........#...............................#",
            "#..o...........#...............................#",
            "#..............#...............................#",
            "####..######################..##################",
            "#..............................................#",
            "#...........t.......t.......t.......t..........#",
            "#......#................................#......#",
            "#......#................................#.C..C.#",
            "#..m...#................................#......#",
            "#......#.......................................#",
            "#....m.#.......................................#",
            "#......................F................#......#",
            "#.......................F...............#......#",
            "#......#................................#......#",
            "#..m...#................................#......#",
            "#......#................................#......#",
            "#....m.#................................#.C..C.#",
            "#......#................................#......#",
            "#...........t.......................t..........#",
            "#..............................................#",
            "###..##################..########..#############",
            "#........#############....######......##########",
            "#........#############....######......##########",
            "#.D......#############....######..U...##########",
            "#........#############....######......##########",
            "#........#############....######################",
            "######################....######################",
            "######################.EE.######################",
            "################################################",
            "################################################",
        };

        private static readonly string[] F2Cellmap =
        {
            "################################################",
            "################################################",
            "###........########..........########........###",
            "###......m.########..........########.....m..###",
            "###........########..C....C..########........###",
            "###........########..........########........###",
            "###........########..........########........###",
            "###........########..........########........###",
            "###........########..........########........###",
            "###........########..........########........###",
            "###t.......########..........########.......t###",
            "###........########..........########........###",
            "###........########..........########........###",
            "#..............................................#",
            "#...........t...............t..................#",
            "#.U..m................................m......D.#",
            "#..............................................#",
            "#..............................................#",
            "#...................t..........................#",
            "#..............................................#",
            "#....m................................m........#",
            "#..............................................#",
            "#..............................................#",
            "#..................................t...........#",
            "#..............................................#",
            "###........########..........########........###",
            "###........########..........########........###",
            "###t.......########..........########.......t###",
            "###........########..........########........###",
            "###........########..........########........###",
            "###......m.########..........########.....m..###",
            "###........########..........########........###",
            "###........########..........########........###",
            "###........########..........########........###",
            "################################################",
            "################################################",
        };

        private static readonly string[] F3Cellmap =
        {
            "################################################",
            "################################################",
            "################################################",
            "#...........................................t..#",
            "#...........v..................................#",
            "#.U.wwwwww........C............................#",
            "#...wwwwww.....................................#",
            "#...wwwwww....m................................#",
            "#...wwwwww.....................................#",
            "#...wwwwww.....................................#",
            "#...wwwwww.....................................#",
            "#.............................m................#",
            "#..............................................#",
            "#.....t.............t.............t............#",
            "#...................................v..........#",
            "####################........####################",
            "####################..wwww..####################",
            "####################..wwww..####################",
            "####################..wwww..####################",
            "####################..wwww..####################",
            "####################........####################",
            "#.....t.............t.............t............#",
            "#...................................v..........#",
            "#..............................................#",
            "#..............................................#",
            "#.............................m.......wwwwww...#",
            "#.....................................wwwwww...#",
            "#.............m.......................wwwwww...#",
            "#.....................................wwwwww...#",
            "#.......................C.............wwwwww...#",
            "#.....................................wwwwww.D.#",
            "#...........................................t..#",
            "#...........v..................................#",
            "################################################",
            "################################################",
            "################################################",
        };

        private static readonly string[] F4Cellmap =
        {
            "################################################",
            "################################################",
            "##########################################U#####",
            "###..........................................###",
            "###..m...m...m...m...m...m...m...m...m...m...###",
            "###..........................................###",
            "###...t.................................t....###",
            "###.....P.......P.......P.......P.......P....###",
            "###..........................................###",
            "###.....................t....................###",
            "###..........................................###",
            "###..........................................###",
            "###..........................................###",
            "###.....P.......P.......P.......P.......P....###",
            "###..........................................###",
            "###..........................................###",
            "###..........................................###",
            "###...........t...................t..........###",
            "###..........................................###",
            "###.....P.......P.......P.......P.......P....###",
            "###..........................................###",
            "###..........................................###",
            "###..........................................###",
            "###..........................................###",
            "###..........................................###",
            "###.....P.......P.......P.......P.......P....###",
            "###..........................................###",
            "###...t.................t...............t....###",
            "###..........................................###",
            "###....m...m...m...m...m...m...m...m...m.....###",
            "###..........................................###",
            "#####################......#####################",
            "#####################.D....#####################",
            "#####################......#####################",
            "################################################",
            "################################################",
        };

        private static readonly string[] F5Cellmap =
        {
            "################################################",
            "##............................................##",
            "##............................................##",
            "##............................................##",
            "##.U..C...t...................t.........t.C...##",
            "##............................................##",
            "##............................................##",
            "##............................................##",
            "######################..########################",
            "##########....t...............t.......##########",
            "##########............................##########",
            "##########..#......................#..##########",
            "##########..#......................#..##########",
            "##########..#.....c..........c.....#..##########",
            "##########..#......................#..##########",
            "##########..#......................#..##########",
            "##########..#......................#..##########",
            "##########..#..........c...........#..##########",
            "##########..#......................#..##########",
            "##########..#......................#..##########",
            "##########..#......................#..##########",
            "##########..#....K.................#..##########",
            "##########..#.....c..........c.....#..##########",
            "##########..#......................#..##########",
            "##########..#......................#..##########",
            "##########............................##########",
            "##########.......K....................##########",
            "######################..########################",
            "##............................................##",
            "##............................................##",
            "##............................................##",
            "##....C...t...................t.........t.C...##",
            "##............................................##",
            "##............................................##",
            "##............................................##",
            "################################################",
        };

        private static readonly string[][] FloorCellmaps = { F1Cellmap, F2Cellmap, F3Cellmap, F4Cellmap, F5Cellmap };

        /// <summary>
        /// [165차 사용자 지시] 층별 무드 테이블 — 층간 밝기 F1 기준 전층 통일. 깊이 감쇠(146~158차 승계:
        /// ambient 0.10→0.055, central 0.6→0.4)는 폐기 — 하층이 과도하게 어두워 판별성 저하.
        /// ambientIntensity 0.10 / centralLightIntensity 0.6 전층 동일. 배열 구조·moodIndex 클램프는 유지
        /// (층수 확장 시 배열 인덱스 호환 — 로직 무수정).
        /// </summary>
        private static readonly float[] FloorAmbientIntensity = { 0.10f, 0.10f, 0.10f, 0.10f, 0.10f };
        private static readonly float[] FloorCentralLightIntensity = { 0.6f, 0.6f, 0.6f, 0.6f, 0.6f };

        // ================================================================
        // [156차] 셀맵 파싱 상태 — ApplyFloorLayout이 층 전환 시 재구성(결정론 스캔 순: 행→열)
        // ================================================================

        private static string[] s_cells = F1Cellmap;      // 현재 층 셀맵(문자 아트)
        private static int s_floorIndex;
        private static bool s_poolActive;                 // [157차 P-C] CaveLayoutPool 선택 맵 활성(S 스폰 마커 경로)
        private static readonly List<Vector2Int> s_spawnCells = new List<Vector2Int>();   // S — 플레이어 스폰(풀 맵 전용)

        private static readonly List<Vector2Int> s_floorCells = new List<Vector2Int>();      // 모든 비'#' 셀
        private static readonly List<Vector2Int> s_hearthCells = new List<Vector2Int>();     // F
        private static readonly List<Vector2Int> s_mineCells = new List<Vector2Int>();       // m
        private static readonly List<Vector2Int> s_chestCells = new List<Vector2Int>();      // C
        private static readonly List<Vector2Int> s_cofinCells = new List<Vector2Int>();      // c
        private static readonly List<Vector2Int> s_crateCells = new List<Vector2Int>();      // s
        private static readonly List<Vector2Int> s_skullCells = new List<Vector2Int>();      // K
        private static readonly List<Vector2Int> s_rockCells = new List<Vector2Int>();       // o
        private static readonly List<Vector2Int> s_vineCells = new List<Vector2Int>();       // v
        private static readonly List<Vector2Int> s_waterCells = new List<Vector2Int>();      // w
        private static readonly List<Vector2Int> s_pillarCells = new List<Vector2Int>();     // P
        private static readonly List<Vector2Int> s_torchCells = new List<Vector2Int>();      // t
        private static readonly List<Vector2Int> s_portalDownCells = new List<Vector2Int>(); // D
        private static readonly List<Vector2Int> s_portalUpCells = new List<Vector2Int>();   // U
        private static readonly List<Vector2Int> s_entranceCells = new List<Vector2Int>();   // E

        /// <summary>셀 → 월드 좌표 — world.x=(cx−24)*2, world.z=(cy−18)*2(룸 원점 중앙 정렬 계약).</summary>
        private static float CellWorldX(int cx) => (cx - GRID_CENTER_X) * CELL_SIZE;
        private static float CellWorldZ(int cy) => (cy - GRID_CENTER_Z) * CELL_SIZE;
        private static Vector2 CellWorld(Vector2Int cell) => new Vector2(CellWorldX(cell.x), CellWorldZ(cell.y));
        private static readonly List<Vector2Int> s_pathCells = new List<Vector2Int>();      // [158차 P3-C] BFS 경로 셀(스폰→포탈)
        private static readonly List<Vector2Int> s_gravelCells = new List<Vector2Int>();    // [158차 P3-C] 자갈 스탬프 셀(경로 제외)

        // ---- [191차] 포탈 계단굴 상태 — ResolvePortalPitCells가 바닥 쿼드 생성 직전 결정론 산출 ----
        private static readonly List<Vector2Int> s_portalPitCells = new List<Vector2Int>(); // D 포탈 셀+진입 셀 — 주바닥 쿼드·타일 배제(굴 조형)
        private static Vector2 s_portalPitDir;             // 진입 셀→포탈 셀 단위 방향(월드 XZ — 계단 하강축)
        private static bool s_portalPitActive;             // 굴 성립 여부(부재 시 기존 평지 포탈 유지)

        // ---- [191차] 바닥 타일 프리팹 캐시 — Floor_1 시리즈 6변형 1회 프리로드(룸 재빌드와 무관) ----
        private static GameObject[] s_cellTilePrefabs;

        /// <summary>바닥 판정 — 비'#' 셀은 전부 바닥(마커 포함). 범위 밖은 비바닥(외곽 봉합).</summary>
        private static bool IsFloor(int cx, int cy)
        {
            if (cy < 0 || cy >= s_cells.Length) return false;
            string row = s_cells[cy];
            if (cx < 0 || cx >= row.Length) return false;
            return row[cx] != '#';
        }

        /// <summary>
        /// [156차] ApplyFloorLayout — 층 전환을 셀맵 스왑으로 수행(시그니처 유지 계약).
        /// 마커 리스트를 스캔 순(행→열)으로 재구성 — 배치 전부 결정론(랜덤 요소 0).
        /// </summary>
        private static void ApplyFloorLayout(string territoryId, int floorIndex)
        {
            // [157차 P-C0] 존별 결정론 레이아웃 — 풀 선택 실패/클래식 영지면 기본 시퀀스(계약테스트 핀 보존).
            s_poolActive = CaveLayoutPool.TrySelect(territoryId, floorIndex + 1, out var poolCells);
            if (s_poolActive)
            {
                s_cells = poolCells;
                s_floorIndex = floorIndex;
            }
            else if (floorIndex < 0 || floorIndex >= FloorCellmaps.Length)
            {
                Debug.LogWarning($"[CaveBuilder] ApplyFloorLayout({floorIndex}) — 층 레이아웃 범위 밖, 입구층 셀맵 폴백");
                floorIndex = 0;
                s_cells = FloorCellmaps[floorIndex];
                s_floorIndex = floorIndex;
            }
            else
            {
                s_cells = FloorCellmaps[floorIndex];
                s_floorIndex = floorIndex;
            }

            s_floorCells.Clear();
            s_hearthCells.Clear();
            s_mineCells.Clear();
            s_chestCells.Clear();
            s_cofinCells.Clear();
            s_crateCells.Clear();
            s_skullCells.Clear();
            s_rockCells.Clear();
            s_vineCells.Clear();
            s_waterCells.Clear();
            s_pillarCells.Clear();
            s_torchCells.Clear();
            s_portalDownCells.Clear();
            s_portalUpCells.Clear();
            s_entranceCells.Clear();
            s_spawnCells.Clear();

            for (int cy = 0; cy < s_cells.Length; cy++)
            {
                string row = s_cells[cy];
                for (int cx = 0; cx < row.Length; cx++)
                {
                    char ch = row[cx];
                    if (ch == '#') continue;   // 비바닥 — 바닥 셀 목록 제외(벽 생성 입력으로만 사용)
                    var cell = new Vector2Int(cx, cy);
                    s_floorCells.Add(cell);
                    switch (ch)
                    {
                        case 'F': s_hearthCells.Add(cell); break;
                        case 'm': s_mineCells.Add(cell); break;
                        case 'C': s_chestCells.Add(cell); break;
                        case 'c': s_cofinCells.Add(cell); break;
                        case 's': s_crateCells.Add(cell); break;
                        case 'K': s_skullCells.Add(cell); break;
                        case 'o': s_rockCells.Add(cell); break;
                        case 'v': s_vineCells.Add(cell); break;
                        case 'w': s_waterCells.Add(cell); break;
                        case 'P': s_pillarCells.Add(cell); break;
                        case 't': s_torchCells.Add(cell); break;
                        case 'D': s_portalDownCells.Add(cell); break;
                        case 'U': s_portalUpCells.Add(cell); break;
                        case 'E': s_entranceCells.Add(cell); break;
                        case 'S': s_spawnCells.Add(cell); break;   // [157차 P-C] 풀 맵 스폰 앵커
                    }
                }
            }
        }

        /// <summary>마커 셀 집합의 결정론 중심(월드) — 다중 마커(F 2셀/E 2셀)는 평균.</summary>
        /// <summary>[157차 B-D] 앰비언트 먼지 — 룸 상공 와이드 박스에서 느리게 떠다니는 먼지 입자.
        ///  절차 소프트닷 텍스처 32²(캐시 1회 — 결정론), Random API 0건(파티클 내부 랜덤은 엔진 영역).
        ///  렌더러만 — 콜라이더/조명 없음(광 예산 계약 무영향).</summary>
        private static Texture2D _dustTex;
        private static void CreateAmbientDust(GameObject room)
        {
            if (_dustTex == null)
            {
                const int size = 32;
                _dustTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "CaveDustTex",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                        float a = Mathf.Clamp01(1f - r);
                        a *= a;
                        px[y * size + x] = new Color(1f, 0.95f, 0.85f, a * 0.5f);
                    }
                _dustTex.SetPixels(px);
                _dustTex.Apply(false, true);
            }

            var go = new GameObject("CaveAmbientDust");
            go.transform.SetParent(room.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 20f;
            main.loop = true;
            main.startLifetime = 8f;
            main.startSpeed = 0.05f;
            main.startSize = 0.035f;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(1f, 0.95f, 0.85f, 0.35f);
            var emit = ps.emission;
            emit.rateOverTime = 4f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(80f, 2f, 60f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.y = 0.06f;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "CaveDust_Mat" };
                mat.mainTexture = _dustTex;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                psr.material = mat;
            }
            ps.Play();
            Debug.Log($"[CaveBuilder] 🌫 앰비언트 먼지 배치(157차 B-D) — 박스 80×60m/최대 48입자");
        }
        private static Vector2 CellsCentroid(List<Vector2Int> cells)
        {
            float sx = 0f, sz = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                sx += CellWorldX(cells[i].x);
                sz += CellWorldZ(cells[i].y);
            }
            return new Vector2(sx / cells.Count, sz / cells.Count);
        }

        // ---- 횟불 공통(기존 계약 유지 — C22-08 값) ----
        private static readonly Color TorchColor = new Color(1.0f, 0.6f, 0.2f);
        private const float TORCH_RANGE = 6f;
        private const float HEARTH_RANGE = 9f;
        private const float TORCH_INTENSITY = 1.2f;
        private static bool _torchGlbFallbackLogged;   // [147차 P-B3] 폴백 로그 1행 플래그(횟불 호출 다수 — 중복 스팸 방지)

        /// <summary>동굴 내부 생성 — 시그니처 유지(IndoorSceneTransition/IndoorPreviewMenu 호출부 계약).
        /// [143차 P7] 2인자 = 입구층(0층) 위임.</summary>
        public static GameObject BuildCaveInterior(string territoryId = "default", int tier = 1)
        {
            return BuildCaveInteriorCore(territoryId, tier, 0);
        }

        /// <summary>[143차 P7 → P9 2단] 층 지정 빌드 — floorIndex 0=입구층(F1)~4=심부(F5).
        /// 시그니처 계약: 3인자 호출부(CaveLevelDirector.BuildFloor) 무영향.</summary>
        public static GameObject BuildCaveInterior(string territoryId, int tier, int floorIndex)
        {
            if (floorIndex < 0 || floorIndex > 4)
            {
                Debug.LogWarning($"[CaveBuilder] floorIndex {floorIndex} — 지원 범위(0~4) 초과, 입구층 셀맵으로 임시 빌드");
                floorIndex = 0;
            }
            return BuildCaveInteriorCore(territoryId, tier, floorIndex);
        }

        /// <summary>[143차 P7 승계 → 156차 셀맵 재작업] 본체. tier = 난이도 파라미터만(기하 곱셈 폐기).</summary>
        private static GameObject BuildCaveInteriorCore(string territoryId, int tier, int floorIndex)
        {
            tier = InteriorRandomizer.ClampTier(tier);   // 파라미터 검증 계약 — [156차] 기하 적용 없음(tierScale 폐기)

            // Random 0건 규약 — 유일한 무작위원은 시드 결정론 System.Random(보석상자 GemType 순서 고정).
            // [156차] 신규 배치(마커 스캔 순)는 rng 소비 0건 — 결정론 계약 유지.
            var rng = InteriorRandomizer.CreateRandom(territoryId + "_cave");

            ApplyFloorLayout(territoryId, floorIndex);   // [157차 P-C0] 층 전환 = 존별 셀맵 선택 + 마커 재구성

            // [146차 P1] 티어 컨텍스트 동기화 — 부트(Test_21)의 2인자 직접 빌드 경유에서 Director 상태 누락 수리(무수정 계약).
            CaveLevelDirector.SyncContext(territoryId, tier, floorIndex + 1);

            GameObject room = new GameObject(ROOM_NAME);
            room.transform.position = Vector3.zero;
            room.AddComponent<CaveFallGuard>();   // [156차 P2-4] 낙하 안전망 — y<−3m → 현재 층 스폰 복귀(블로커 상시 봉합과 이중 방어)

            ResolvePortalPitCells();        // [191차] 하강 포탈 계단굴 셀 확정 — 바닥 쿼드 생성 전(BFS 재계산, 결정론)
            CreateCellFloor(room);          // [156차] 병합 쿼드 바닥 — MeshCollider 충돌 단일소스 유지(굴 셀 제외 — 191차)
            CreateStonePath(room);          // [158차 P3-C] BFS 경로 돌판 오버레이(스폰→포탈) — 무수정 계약
            CreateCellFloorTiles(room);     // [191차] 던전 팩 바닥 타일(Floor_1 시리즈 통일, 경로 셀 배제)
            CreateDirtDetail(room);         // [158차 P3-C] 흙 자갈 스탬프(경로 제외)
            CreateCellWalls(room);          // [156차] 얇은 돌 벽(런 머징) + 평면 캡

            // [P9 1단 승계] 층별 무드 테이블 — 암전/중앙광 단계, ambientColor 현행 유지.
            int moodIndex = Mathf.Clamp(floorIndex, 0, FloorAmbientIntensity.Length - 1);
            IndoorLighting.SetupIndoorLighting(room, new Color(0.02f, 0.02f, 0.06f), FloorAmbientIntensity[moodIndex], true, FloorCentralLightIntensity[moodIndex]);
            CreateAmbientLight(room);

            CreateGroundDetails(room);      // [143차 P3 승계] 블롭/마모 스탬프/입구 빛웅덩이 — 마커 기반 재배치(렌더러 전용)
            CreateAmbientDust(room);        // [157차 B-D] 먼지 입자 — 실내 생동감(절차 텍스처·결정론 캐시)

            // 마커 배치 — [156차] 셀맵 마커 → 월드 결정론(스캔 순). rng는 GemType 시드만 소비.
            var zone = CreateZone(room, $"CaveZone_F{floorIndex + 1}");
            CreateHearth(zone);             // F — 중앙화로(기존 CaveHearthFlame 체계)
            CreateMiningNodes(zone);        // m — ResourceNode(GLB 승격 계약 유지)
            CreateGemChests(zone, rng);     // C — GemChest(GemType 시드 결정론)
            CreateCofins(zone);             // c — Cofin GLB, 없으면 상자 폴백
            CreateCrates(zone);             // s — 상자(IndoorFurnitureCatalog.CreateCrate)
            CreateDungeonProps(zone, rng);  // [160차 P2] 던전 소품 패스 — 수용분 5종 GLB(결정론 rng, 렌더러 전용)
            CreateSkullCandles(zone);       // K — SkullCandle GLB(비주얼만)
            CreateRockAccents(zone);        // o — 잔석(Rocks GLB 소형)
            CreateVines(zone);              // v — Vines GLB(부재 시 스킵 로그 1행)
            CreateWaterPools(zone);         // w — 평면 수면(콜라이더 없음, Idyllic 워터 머티리얼)
            CreatePillars(zone);            // P — Pilar GLB + 0.8×0.8m BoxCollider 장애물
            CreateWallTorches(zone);        // t — 횟불(Torch GLB+Light+TorchFlicker, 층당 8 ≤ 8 계약)

            CreateFloorPortals(room, floorIndex);   // [156차] D/U 마커 좌표에 포탈(Arch GLB 유지)
            CreateEntranceDoor(room);               // [156차] E 마커 — 루트+블로커만(패널 시각 폐기), 블로커 enabled=true

            return room;
        }

        // ================================================================
        // 스폰 — 셀맵 좌표 직접 반환([156차] tierScale 폐기 — 명세 변경, 사유: 셀맵에 크기 내재화.
        // ×tierScale 곱셈 제거. tier 파라미터는 난이도 파라미터로만 존속 — CaveLevelDirector 호환)
        // ================================================================

        /// <summary>
        /// [156차] F1 입구 스폰 — 입구 통로 내부 셀 (23,31)(월드 (−2, 26), 문 블로커 E행 안쪽).
        /// 137~148차의 "입구 변 중점×tierScale 내부 오프셋" 계산은 셀맵 전환으로 폐기 — 고정 셀 좌표 직접 반환.
        /// </summary>
        public static Vector3 GetEntranceSpawnPosition(int tier)
        {
            tier = InteriorRandomizer.ClampTier(tier);   // 파라미터 검증 계약 유지(기하 무관 — 156차 스케일 배수 폐기)
            // [157차 P-C] 풀 입구 맵 — S 마커 셀 직접 반환(클래식은 기존 고정 좌표 계약 유지).
            if (s_poolActive && s_spawnCells.Count > 0)
                return new Vector3(CellWorldX(s_spawnCells[0].x), 1.5f, CellWorldZ(s_spawnCells[0].y));
            return new Vector3(ENTRANCE_SPAWN_X, 1.5f, ENTRANCE_SPAWN_Z);
        }

        /// <summary>[P9 1단 승계] 1인자 계약 — tier=1 위임(CaveLevelDirector 호출부는 2인자 직접 호출).</summary>
        public static Vector3 GetFloorSpawnPosition(int floor)
        {
            return GetFloorSpawnPosition(floor, 1);
        }

        /// <summary>
        /// [156차] 층별 스폰 테이블(2인자) — 셀맵 월드 좌표 직접 반환(×tierScale 곱셈 폐기).
        /// F1=(23,31) / F2=(6,15) / F3=(6,4) / F4=(38,4) / F5=(6,5) — 전부 해당 층 상승 포탈에서 ≥2.2m 이격
        /// (146차 스폰-포탈 이격 계약). F3 (6,5)는 w 수역, F5 (6,4)는 C 보석상자 마커라 인접 바닥 셀로 1셀 보정.
        /// floor는 1기반(CaveLevelDirector CurrentFloor 계약).
        /// </summary>
        public static Vector3 GetFloorSpawnPosition(int floor, int tier)
        {
            tier = InteriorRandomizer.ClampTier(tier);   // 파라미터 검증 계약 유지(기하 무관 — 156차 스케일 배수 폐기)
            // [157차 P-C] 풀 맵 — S 마커 셀 직접 반환(스폰-포탈 이격은 도면 검증기 계약). 클래식은 기존 테이블.
            if (s_poolActive && s_spawnCells.Count > 0)
                return new Vector3(CellWorldX(s_spawnCells[0].x), 1.5f, CellWorldZ(s_spawnCells[0].y));
            switch (floor)
            {
                case 1:
                    return GetEntranceSpawnPosition(tier);
                case 2:
                    return new Vector3(-36f, 1.5f, -6f);    // F2 셀 (6,15) — 상승 포탈 (2,15)에서 8.0m 이격
                case 3:
                    return new Vector3(-36f, 1.5f, -28f);   // F3 셀 (6,4) — (6,5)는 w 수역이라 북 1셀 보정, 포탈 (2,5)에서 8.2m
                case 4:
                    return new Vector3(28f, 1.5f, -28f);    // F4 셀 (38,4) — 상승 포탈 (42,2)에서 8.9m 이격
                case 5:
                    return new Vector3(-36f, 1.5f, -30f);   // F5 셀 (6,5) — (6,4)는 C 마커라 남 1셀 보정, 포탈 (3,4)에서 6.3m
                default:
                    Debug.LogWarning($"[CaveBuilder] GetFloorSpawnPosition({floor}) — 층 스폰 테이블 범위 밖, 5층 위치 폴백");
                    return new Vector3(-36f, 1.5f, -30f);
            }
        }

        // ================================================================
        // [156차] 셸 — 셀맵 바닥/얇은 돌 벽/벽 캡
        // ================================================================

        /// <summary>
        /// [156차] 셀맵 바닥 — 비'#' 셀 전부의 병합 쿼드 메시 1개. 이름 CaveFloor 유지,
        /// MeshCollider(충돌 단일소스 계약), 노멀 +y 강제, UV=셀 단위(셀당 1 UV → 흙 텍스처 셀당 1회 반복).
        /// </summary>
        private static void CreateCellFloor(GameObject room)
        {
            // [191차] 하강 포탈 계단굴 셀은 주바닥 병합 쿼드에서 제외 — 굴(2셀, 깊이 1.5m) 조형.
            // 굴 바닥은 CreatePortalStairwell이 별도 쿼드(y=−1.5)+MeshCollider로 봉합(주바닥과 동일 충돌 방식).
            var quads = new List<Vector2Int>(s_floorCells.Count);
            foreach (var cell in s_floorCells)
                if (!s_portalPitCells.Contains(cell)) quads.Add(cell);
            int count = quads.Count;
            if (count == 0) return;   // 전 셀 제외(비정상 셀맵) — 퇴화 가드
            var mesh = new Mesh { name = "CaveFloorMesh" };
            var vertices = new Vector3[count * 4];
            var uvs = new Vector2[count * 4];
            var normals = new Vector3[count * 4];
            var triangles = new int[count * 6];

            int v = 0;
            foreach (var cell in quads)
            {
                float x0 = CellWorldX(cell.x) - CELL_SIZE * 0.5f;
                float x1 = CellWorldX(cell.x) + CELL_SIZE * 0.5f;
                float z0 = CellWorldZ(cell.y) - CELL_SIZE * 0.5f;
                float z1 = CellWorldZ(cell.y) + CELL_SIZE * 0.5f;

                // 정점 순서/와인딩 — 구 CreateWallCaps 평면캡의 위향(+Y) 패턴 승계(v0=좌하, v1=우하, v2=좌상, v3=우상).
                vertices[v] = new Vector3(x0, 0f, z0);
                vertices[v + 1] = new Vector3(x1, 0f, z0);
                vertices[v + 2] = new Vector3(x0, 0f, z1);
                vertices[v + 3] = new Vector3(x1, 0f, z1);
                uvs[v] = new Vector2(cell.x, cell.y);             // 셀 단위 UV — 셀당 1 타일
                uvs[v + 1] = new Vector2(cell.x + 1f, cell.y);
                uvs[v + 2] = new Vector2(cell.x, cell.y + 1f);
                uvs[v + 3] = new Vector2(cell.x + 1f, cell.y + 1f);
                normals[v] = Vector3.up;                          // 노멀 +y 강제 계약
                normals[v + 1] = Vector3.up;
                normals[v + 2] = Vector3.up;
                normals[v + 3] = Vector3.up;
                v += 4;
            }
            // 인덱스 — 쿼드당 6개(위향 +Y 와인딩: (0,2,1),(2,3,1) — 구 CreateWallCaps 평면캡 패턴 승계).
            for (int q = 0; q < count; q++)
            {
                int b = q * 4;
                triangles[q * 6 + 0] = b + 0;
                triangles[q * 6 + 1] = b + 2;
                triangles[q * 6 + 2] = b + 1;
                triangles[q * 6 + 3] = b + 2;
                triangles[q * 6 + 4] = b + 3;
                triangles[q * 6 + 5] = b + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.RecalculateBounds();

            var floor = new GameObject("CaveFloor");
            floor.transform.SetParent(room.transform, false);
            var mf = floor.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = floor.AddComponent<MeshRenderer>();
            mr.material = CaveMaterialFactory.CreateDirtFloor();   // [156차] 흙 단일화 — FarmPlotVisuals 절차 흙 텍스처 단일소스
            floor.AddComponent<MeshCollider>();   // 이동 충돌 — 바닥 단일소스(계약 유지)
        }

        // ================================================================
        // [191차] 던전 팩 바닥 타일 통일 + 하강 포탈 계단굴
        // ================================================================

        /// <summary>[191차] 바닥 타일 변형 키 6종 — Floor_1 시리즈 단일 계열(전 층 통일 계약, 결정론 순환).</summary>
        private static readonly string[] CellTileVariantKeys =
        {
            "Floor_1_1A", "Floor_1_2A", "Floor_1_3A", "Floor_1_4A", "Floor_1_5A", "Floor_1_6A",
        };

        /// <summary>
        /// [191차] 타일 프리팹 프리로드 — 정적 캐시 1회. RuntimeModelLoader 던전 팩 폴백 경로(L158 선례) 1차,
        /// 미수중 직접 Resources.Load(DungeonAssetCatalog.ResourceRoot) 2차. 부분 성공 허용(결핍 변형 셀 스킵).
        /// </summary>
        private static void EnsureCellTilePrefabs()
        {
            if (s_cellTilePrefabs != null) return;
            var prefabs = new GameObject[CellTileVariantKeys.Length];
            int loaded = 0;
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (RuntimeModelLoader.TryGetModel(CellTileVariantKeys[i], out var prefab) && prefab != null)
                {
                    prefabs[i] = prefab;
                    loaded++;
                    continue;
                }
                prefabs[i] = Resources.Load<GameObject>(DungeonAssetCatalog.ResourceRoot + CellTileVariantKeys[i]);
                if (prefabs[i] != null) loaded++;
            }
            if (loaded == 0)
                Debug.Log("[CaveBuilder] 🧱 던전 팩 바닥 타일 GLB 부재 — 흙 바닥 유지(폭 191차 폴백)");
            s_cellTilePrefabs = prefabs;
        }

        /// <summary>
        /// [191차] 던전 팩 바닥 타일 통일 — 전 바닥 셀에 Floor_1 시리즈 타일 GLB 1인스턴스(비주얼 전용).
        /// BFS 돌판 경로 셀(s_pathCells)은 타일 배제 — 흙 낟길 대비 유지(158차 P3-C 안내 계약 시각 보존).
        /// 계단굴 셀(s_portalPitCells)도 배제 — 조각난 바닥 위 부유 타일 금지.
        /// 결정론: 변형 인덱스 = (cell.x*31 + cell.y*17) % 6(그리드 좌표 비음수 — 무작위원 0건 프로젝트 규약),
        /// 회전 = (cell.x*13 + cell.y*29) & 3 → 90° 스텝 4방위([192차] 변형 해시와 독립 위상 —
        /// 팔레트 UV 면단위 붕괴 스타일이라 회전 무해, 격자 반복 타파. 정사각형 슬랩이라 bounds 정렬 불변).
        /// 타일 상면 y=+0.01(bounds 기반 정렬 — 피벗 규약 무관), 콜라이더 없음
        /// (충돌은 병합 쿼드 MeshCollider 단일소스 유지). 배치 후 스태틱 배치 결합 — 벽 박스 런 머징 계열.
        /// [192차] 타일 GLB 자체는 정점AO(R절대5cm·s²/d²)+에지다크닝(55°·×0.84)+팔레트 스펙클 수술 완료
        /// (Tools/floor_quality_bake.py — 원본 백업·매니페스트 계약, 실내씬 마감 수준 정합).
        /// </summary>
        private static void CreateCellFloorTiles(GameObject room)
        {
            EnsureCellTilePrefabs();
            int loadedCount = 0;
            for (int i = 0; i < s_cellTilePrefabs.Length; i++)
                if (s_cellTilePrefabs[i] != null) loadedCount++;
            if (loadedCount == 0) return;   // 폴백 — 전 변형 미수중(로그는 프리로드 1행)

            var tilesRoot = new GameObject("CaveCellTiles");
            tilesRoot.transform.SetParent(room.transform, false);
            const float TileTopY = 0.01f;
            int placed = 0;
            for (int i = 0; i < s_floorCells.Count; i++)
            {
                var cell = s_floorCells[i];
                if (s_pathCells.Contains(cell)) continue;         // 돌판 경로 — 흙 낟길 유지(대비 계약)
                if (s_portalPitCells.Contains(cell)) continue;    // 계단굴 조각 셀 — 부유 타일 금지
                var prefab = s_cellTilePrefabs[(cell.x * 31 + cell.y * 17) % CellTileVariantKeys.Length];
                if (prefab == null) continue;
                var tile = new GameObject($"CaveCellTile_{cell.x:D2}_{cell.y:D2}");
                tile.transform.SetParent(tilesRoot.transform, false);
                tile.transform.localPosition = new Vector3(CellWorldX(cell.x), 0f, CellWorldZ(cell.y));
                int rotStep = (cell.x * 13 + cell.y * 29) & 3;   // [192차] 회전 다양화 — 결정론 4방위(무작위원 0건)
                tile.transform.localRotation = Quaternion.Euler(0f, rotStep * 90f, 0f);
                var visual = Object.Instantiate(prefab, tile.transform);
                visual.name = "Visual";
                StripColliders(visual);   // 타일 비주얼 전용 — 콜라이더 단일소스(병합 쿼드) 계약
                var renderers = visual.GetComponentsInChildren<Renderer>();
                if (renderers != null && renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                    if (bounds.size.y > 0.0001f)
                        tile.transform.localPosition += new Vector3(0f, TileTopY - bounds.max.y, 0f);   // 상면 y=+0.01
                }
                placed++;
            }
            if (placed == 0)
            {
                Object.DestroyImmediate(tilesRoot);
                return;
            }
            StaticBatchingUtility.Combine(tilesRoot);   // 스태틱 결합 — 드로우콜 통합(비주얼만, 단일 인자 오버로드)
            Debug.Log($"[CaveBuilder] 🧱 던전 팩 바닥 타일 {placed}매(F{s_floorIndex + 1}, Floor_1 시리즈 6변형 결정론 순환 — 191차)");
        }

        /// <summary>
        /// [191차] 하강 포탈 계단굴 셀 결정 — BFS 최단 경로(스폰→D 포탈)의 포탈 직전 셀을 진입 셀로 확정
        /// (경로 진입측 정렬 계약 — 흙 낟길이 계단 상단으로 이어짐). 경로 부재 시 고정 순회(E,S,W,N) 폴백.
        /// CreateCellFloor 병합 쿼드에서 굴 셀을 빼내기 위해 바닥 생성 전 호출 — ComputeStonePathCells는
        /// 순수 함수(부작용 없음)라 CreateStonePath와 동일 결정론 재계산(결과 동일).
        /// 풀 맵 5층 하강 억제(CreateFloorPortals와 동일 판정) 시 굴 미성립 — 기존 평지 포탈 유지.
        /// </summary>
        private static void ResolvePortalPitCells()
        {
            s_portalPitCells.Clear();
            s_portalPitDir = Vector2.zero;
            s_portalPitActive = false;
            if (s_portalDownCells.Count == 0) return;
            bool downSuppressed = s_poolActive && s_floorIndex >= 4;   // CreateFloorPortals 판정과 동일 계약
            if (downSuppressed) return;

            var portalCell = s_portalDownCells[0];
            var path = ComputeStonePathCells(ResolvePathStartCell(), new List<Vector2Int> { portalCell });
            Vector2Int approach;
            if (path.Count >= 2)
            {
                approach = path[1];   // path[0]=포탈, path[1]=포탈 직전 셀(진입측) — 역추적 순서 계약
            }
            else
            {
                approach = portalCell;
                Vector2Int[] dirs =
                {
                    new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1),
                };
                foreach (var d in dirs)
                {
                    var n = portalCell + d;
                    if (!IsFloor(n.x, n.y)) continue;
                    approach = n;
                    break;
                }
                if (approach == portalCell) return;   // 진입 불가 — 굴 성립 없음(기존 평지 포탈 유지)
            }
            s_portalPitCells.Add(portalCell);
            s_portalPitCells.Add(approach);
            s_portalPitDir = (CellWorld(portalCell) - CellWorld(approach)).normalized;   // 축 정렬 셀 인접 — 단위 벡터
            s_portalPitActive = true;
            Debug.Log($"[CaveBuilder] 🕳 하강 포탈 계단굴 셀 확정(F{s_floorIndex + 1}) — 포탈{portalCell}+진입{approach}, 깊이 1.5m(191차)");
        }

        /// <summary>
        /// [191차] 하강 포탈 계단굴 조형 — 2셀 굴(깊이 1.5m): 굴 바닥 병합 쿼드(y=−1.5, MeshCollider —
        /// 주바닥과 동일 충돌 방식, CreateDirtFloor 같은 머티리얼 계열), 굴 벽 3면(CreateCellWalls 박스 벽
        /// 런 패턴 재사용 — 두께 0.5m·BoxCollider 낙하 봉합), Stairs_1A 축척 0.5(상승 1.486m·수평 2.137m·
        /// 폭 1.328m) 하위 메시 전부 MeshCollider 부착(CC 접지 이동 가능), 굴 입구 변(주바닥 경계)에 최상단 정렬.
        /// 성공 시 아치 이동 좌표(굴 바닥 포탈 셀 중심, y=−1.5) 반환 — CreateFloorPortals가 아치+트리거 이전.
        /// GLB 실측: Stairs_1A 로컬 −z측이 상단(상승 방향 −z) — yaw로 로컬 +z(하단)를 하강축에 정렬.
        /// </summary>
        private static bool CreatePortalStairwell(GameObject room, out Vector2 archXz, out float archY)
        {
            archXz = Vector2.zero;
            archY = 0f;
            if (!s_portalPitActive || s_portalPitCells.Count < 2) return false;

            const float PitDepth = 1.5f;
            float half = CELL_SIZE * 0.5f;
            var portalCell = s_portalPitCells[0];
            var approachCell = s_portalPitCells[1];
            Vector2 dir = s_portalPitDir;                       // 진입 셀→포탈 셀(하강축, 축 정렬)
            Vector2 portalW = CellWorld(portalCell);
            Vector2 approachW = CellWorld(approachCell);
            Vector2 entry = approachW - dir * half;             // 굴 입구 변 중점 — 주바닥 경계(계단 상단 접지점)
            Vector2 far = portalW + dir * half;                 // 굴 끝 변 중점
            bool runAlongX = Mathf.Abs(dir.x) > 0.5f;           // BFS 축 정렬 — 하강축이 X인가(아니면 Z)

            // 굴 바닥 — 2셀 병합 쿼드 1개(좌표 = 셀 AABB, CreateCellFloor 정점/와인딩 패턴 재사용)
            var pitMat = CaveMaterialFactory.CreateDirtFloor();
            var pitFloor = new GameObject("CavePitFloor");
            pitFloor.transform.SetParent(room.transform, false);
            float minX = Mathf.Min(approachW.x, portalW.x) - half;
            float maxX = Mathf.Max(approachW.x, portalW.x) + half;
            float minZ = Mathf.Min(approachW.y, portalW.y) - half;
            float maxZ = Mathf.Max(approachW.y, portalW.y) + half;
            int minCX = Mathf.Min(portalCell.x, approachCell.x);
            int maxCX = Mathf.Max(portalCell.x, approachCell.x);
            int minCY = Mathf.Min(portalCell.y, approachCell.y);
            int maxCY = Mathf.Max(portalCell.y, approachCell.y);
            var pitMesh = new Mesh { name = "CavePitFloorMesh" };
            pitMesh.vertices = new[]
            {
                new Vector3(minX, -PitDepth, minZ), new Vector3(maxX, -PitDepth, minZ),
                new Vector3(minX, -PitDepth, maxZ), new Vector3(maxX, -PitDepth, maxZ),
            };
            pitMesh.uv = new[]
            {
                new Vector2(minCX, minCY), new Vector2(maxCX + 1, minCY),
                new Vector2(minCX, maxCY + 1), new Vector2(maxCX + 1, maxCY + 1),
            };
            pitMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };   // 노멀 +y 강제 — CreateCellFloor 계약 승계
            pitMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };   // 위향(+Y) 와인딩 — CreateCellFloor 패턴 승계
            pitMesh.RecalculateBounds();
            pitFloor.AddComponent<MeshFilter>().sharedMesh = pitMesh;
            pitFloor.AddComponent<MeshRenderer>().material = pitMat;
            pitFloor.AddComponent<MeshCollider>();   // 굴 바닥 이동 충돌 — 주바닥과 동일 방식(191차)

            // 굴 벽 3면 — 박스 벽 런 패턴 재사용(입구 제외: 진입측은 계단 개방). 캡 없음(상부 개방 굴).
            Material wallMat = CaveMaterialFactory.CreateWallSide(4f);
            float axisLine = runAlongX ? approachW.y : approachW.x;   // 굴 축선(포탈/진입 셀 동일 좌표)
            float runStart = runAlongX ? Mathf.Min(entry.x, far.x) : Mathf.Min(entry.y, far.y);
            float runEnd = runAlongX ? Mathf.Max(entry.x, far.x) : Mathf.Max(entry.y, far.y);
            CreatePitWallBox(room, "CavePitWall_S1", !runAlongX, axisLine - CELL_SIZE, runStart, runEnd, PitDepth, wallMat);
            CreatePitWallBox(room, "CavePitWall_S2", !runAlongX, axisLine + CELL_SIZE, runStart, runEnd, PitDepth, wallMat);
            CreatePitWallBox(room, "CavePitWall_End", runAlongX, runAlongX ? far.x : far.y, axisLine - CELL_SIZE - WALL_THICKNESS * 0.5f, axisLine + CELL_SIZE + WALL_THICKNESS * 0.5f, PitDepth, wallMat);

            // 이동 가능 계단 — Stairs_1A 축척 0.5, 최상단(−z측)을 굴 입구 변에 접지(bounds 기반 — 피벗 무관)
            if (RuntimeModelLoader.TryGetModel("Stairs_1A", out var stairsModel) && stairsModel != null)
            {
                var stairRoot = new GameObject($"CavePortalStairs_F{s_floorIndex + 1}");
                stairRoot.transform.SetParent(room.transform, false);
                stairRoot.transform.localPosition = new Vector3(entry.x, 0f, entry.y);
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;   // 로컬 +z(하단측) → 하강축(모델 상단 −z)
                stairRoot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                var visual = Object.Instantiate(stairsModel, stairRoot.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = Vector3.one * 0.5f;
                // 이동 가능 — 하위 메시 전부 MeshCollider(GLB 내장 콜라이더 보존, 결핍만 보강 — CC 접지 계약)
                foreach (var meshFilter in visual.GetComponentsInChildren<MeshFilter>())
                    if (meshFilter.GetComponent<MeshCollider>() == null)
                        meshFilter.gameObject.AddComponent<MeshCollider>().sharedMesh = meshFilter.sharedMesh;

                var stairRenderers = visual.GetComponentsInChildren<Renderer>();
                if (stairRenderers != null && stairRenderers.Length > 0)
                {
                    Bounds sb = stairRenderers[0].bounds;
                    for (int r = 1; r < stairRenderers.Length; r++) sb.Encapsulate(stairRenderers[r].bounds);
                    if (sb.size.y > 0.0001f)
                    {
                        // 수직 — 계단 블록 최상단(최고 단 상면)을 주바닥 +0.01에 정렬(최상단 단 = 바닥 표면)
                        stairRoot.transform.localPosition += new Vector3(0f, 0.01f - sb.max.y, 0f);
                        // 수평 — 상단(−z측) 끝면의 하강축 투영을 굴 입구 변 +0.02에 정렬(이음새 겹침)
                        float dMin = Mathf.Min(sb.min.x * dir.x, sb.max.x * dir.x)
                                   + Mathf.Min(sb.min.z * dir.y, sb.max.z * dir.y);
                        float shiftAlong = 0.02f - dMin;
                        stairRoot.transform.localPosition += new Vector3(dir.x * shiftAlong, 0f, dir.y * shiftAlong);
                    }
                }
            }
            else
            {
                Debug.LogWarning("[CaveBuilder] Stairs GLB 부재 — 계단굴은 바닥 쿼드 없이 개방 굴(계단 대신 사다리 폴백 없음 — 로그만)");
            }

            archXz = portalW;        // 아치 이전 — 굴 바닥 포탈 셀 중심(트리거 동행, 안내 문자열 유지)
            archY = -PitDepth;
            Debug.Log($"[CaveBuilder] 🕳 계단굴 조형 완료(F{s_floorIndex + 1}) — 굴 바닥 y={-PitDepth}·박스 벽 3면·이동 계단+아치 이전(191차)");
            return true;
        }

        /// <summary>[191차] 굴 벽 박스 1개 — CreateCellWalls 박스 벽 패턴 재사용(두께 0.5m, BoxCollider 낙하 봉합 유지).</summary>
        private static void CreatePitWallBox(GameObject room, string goName, bool alongX,
            float line, float start, float end, float height, Material material)
        {
            float length = end - start;
            if (length < 0.01f) return;
            float mid = (start + end) * 0.5f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = goName;
            wall.transform.SetParent(room.transform, false);
            if (alongX)
            {
                wall.transform.localPosition = new Vector3(mid, height * 0.5f, line);
                wall.transform.localScale = new Vector3(length, height, WALL_THICKNESS);
            }
            else
            {
                wall.transform.localPosition = new Vector3(line, height * 0.5f, mid);
                wall.transform.localScale = new Vector3(WALL_THICKNESS, height, length);
            }
            var renderer = wall.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.material = material;
        }

        // ================================================================
        // [156차] 얇은 돌 벽 — 던전 팩 Wall GLB 폐기 → 박스 벽(런 머징) + 평면 캡
        // ================================================================

        /// <summary>벽 런 후보 — alongX=false: NS 벽(x=line, z: start~end) / true: EW 벽(z=line, x: start~end).</summary>
        private struct WallRun
        {
            public bool AlongX;
            public float Line;
            public float Start;
            public float End;
        }

        /// <summary>
        /// [156차] 얇은 돌 벽 — 각 비바닥 '#' 셀 기준: 가로(서/동) 양쪽이 바닥이면 셀 중심선에 NS 벽 1개,
        /// 한쪽만 바닥이면 그쪽 공유 경계에 벽. 세로(북/남)도 동일. 같은 직선 위 인접 세그먼트는
        /// 런 머징으로 하나의 박스(두께 0.5m·높이 3m, BoxCollider — 낙하 봉합 충돌).
        /// 머티리얼 = CaveMaterialFactory.CreateCaveWall(돌 느낌 강화본).
        /// </summary>
        private static void CreateCellWalls(GameObject room)
        {
            var candidates = new List<WallRun>();
            for (int cy = 0; cy < s_cells.Length; cy++)
            {
                for (int cx = 0; cx < s_cells[cy].Length; cx++)
                {
                    if (IsFloor(cx, cy)) continue;   // 바닥 셀 — 벽 없음
                    bool west = IsFloor(cx - 1, cy);
                    bool east = IsFloor(cx + 1, cy);
                    bool north = IsFloor(cx, cy - 1);
                    bool south = IsFloor(cx, cy + 1);
                    float wx = CellWorldX(cx);
                    float wz = CellWorldZ(cy);
                    float half = CELL_SIZE * 0.5f;

                    // NS 벽(서/동 축) — 내부 '#'(양쪽 바닥)는 중심선 1개, 외곽은 바닥 쪽 경계.
                    if (west && east) candidates.Add(new WallRun { AlongX = false, Line = wx, Start = wz - half, End = wz + half });
                    else if (west) candidates.Add(new WallRun { AlongX = false, Line = wx - half, Start = wz - half, End = wz + half });
                    else if (east) candidates.Add(new WallRun { AlongX = false, Line = wx + half, Start = wz - half, End = wz + half });

                    // EW 벽(북/남 축) — 동일 규칙.
                    if (north && south) candidates.Add(new WallRun { AlongX = true, Line = wz, Start = wx - half, End = wx + half });
                    else if (north) candidates.Add(new WallRun { AlongX = true, Line = wz - half, Start = wx - half, End = wx + half });
                    else if (south) candidates.Add(new WallRun { AlongX = true, Line = wz + half, Start = wx - half, End = wx + half });
                }
            }

            var runs = MergeWallRuns(candidates);
            var capMat = CreateWallCapMaterial();

            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                float length = run.End - run.Start;
                if (length < 0.01f) continue;
                float mid = (run.Start + run.End) * 0.5f;

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"CaveWall_{i:D2}";
                wall.transform.SetParent(room.transform, false);
                if (run.AlongX)
                {
                    wall.transform.localPosition = new Vector3(mid, WALL_HEIGHT * 0.5f, run.Line);
                    wall.transform.localRotation = Quaternion.identity;
                    wall.transform.localScale = new Vector3(length, WALL_HEIGHT, WALL_THICKNESS);
                }
                else
                {
                    wall.transform.localPosition = new Vector3(run.Line, WALL_HEIGHT * 0.5f, mid);
                    wall.transform.localRotation = Quaternion.identity;
                    wall.transform.localScale = new Vector3(WALL_THICKNESS, WALL_HEIGHT, length);
                }
                var renderer = wall.GetComponent<MeshRenderer>();
                renderer.material = CaveMaterialFactory.CreateWallSide(length);   // [158차 P3-C] 마스너리 줄눈 벽면(세로 1랩 — 줄눈 정렬)

                CreateWallCap(room, capMat, run, length, mid, i);   // 벽 윗면 평면 캡 — 콜라이더 없음
                CreateWallBaseBand(room, run, length, i);   // [158차 P3-C] 벽 하단 받침 밴드 — 렌더러 전용
            }

            Debug.Log($"[CaveBuilder] 🧱 얇은 돌 벽 {runs.Count}런 배치(F{s_floorIndex + 1}) — 두께 {WALL_THICKNESS}m·높이 {WALL_HEIGHT}m 런 머징 + 평면 캡 y={WALL_CAP_Y}(던전 팩 벽 폐기 — 156차)");
        }

        /// <summary>같은 직선 위 인접 세그먼트 병합 — (축, 라인) 그룹별 정렬 후 연속 구간 런 머징(결정론 순수 함수).</summary>
        private static List<WallRun> MergeWallRuns(List<WallRun> candidates)
        {
            var byKey = new Dictionary<long, List<WallRun>>();
            foreach (var c in candidates)
            {
                long key = System.BitConverter.DoubleToInt64Bits(c.Line) * 2 + (c.AlongX ? 1L : 0L);
                if (!byKey.TryGetValue(key, out var list))
                {
                    list = new List<WallRun>();
                    byKey[key] = list;
                }
                list.Add(c);
            }

            var merged = new List<WallRun>();
            foreach (var kvp in byKey)
            {
                kvp.Value.Sort((a, b) => a.Start.CompareTo(b.Start));
                var current = kvp.Value[0];
                for (int i = 1; i < kvp.Value.Count; i++)
                {
                    var next = kvp.Value[i];
                    if (next.Start <= current.End + 0.01f)
                        current.End = Mathf.Max(current.End, next.End);
                    else
                    {
                        merged.Add(current);
                        current = next;
                    }
                }
                merged.Add(current);
            }
            merged.Sort((a, b) =>
            {
                int byAxis = a.AlongX.CompareTo(b.AlongX);
                if (byAxis != 0) return byAxis;
                int byLine = a.Line.CompareTo(b.Line);
                if (byLine != 0) return byLine;
                return a.Start.CompareTo(b.Start);
            });
            return merged;
        }

        /// <summary>[156차] 벽 캡 공용 머티리얼 — 짙은 암석 톤(0.34, 0.32, 0.30), 성 내부 평면캡 패턴 승계.</summary>
        // ================================================================
        // [158차 P3-C] 실내 고품질 — 돌판 경로(BFS)/자갈 스탬프/벽 받침 밴드
        // ================================================================

        /// <summary>경로 시작 셀 — 풀 맵=S 스폰 마커, 클래식=입구(스폰 상수 셀 역산). 풀 맵 S 부재 시 입구 마커.</summary>
        private static Vector2Int ResolvePathStartCell()
        {
            if (s_poolActive && s_spawnCells.Count > 0) return s_spawnCells[0];
            if (s_entranceCells.Count > 0) return s_entranceCells[0];
            return new Vector2Int(
                Mathf.RoundToInt(ENTRANCE_SPAWN_X / CELL_SIZE) + GRID_CENTER_X,
                Mathf.RoundToInt(ENTRANCE_SPAWN_Z / CELL_SIZE) + GRID_CENTER_Z);
        }

        /// <summary>[158차 P3-C] 돌판 경로 BFS — 시작→포탈(D/U) 최단 경로 셀 합집합(4방향·기둥 셀 제외).
        ///   결정론: Queue FIFO 고정 순서, 단일 소스 BFS 후 목표별 역추적 — 동일 셀맵·시드에서 동일 결과.</summary>
        private static List<Vector2Int> ComputeStonePathCells(Vector2Int start, List<Vector2Int> targets)
        {
            var result = new List<Vector2Int>();
            if (targets == null || targets.Count == 0) return result;
            var parents = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new List<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            Vector2Int[] dirs =
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                for (int i = 0; i < dirs.Length; i++)
                {
                    var next = cur + dirs[i];
                    if (!IsFloor(next.x, next.y)) continue;
                    if (s_pillarCells.Contains(next)) continue;   // 기둥 통과 금지 — P 셀 우회 경로
                    if (visited.Contains(next)) continue;
                    visited.Add(next);
                    parents[next] = cur;
                    queue.Enqueue(next);
                }
            }
            for (int i = 0; i < targets.Count; i++)
            {
                var walk = targets[i];
                while (walk != start)
                {
                    if (!result.Contains(walk)) result.Add(walk);
                    if (!parents.TryGetValue(walk, out walk)) break;
                }
                if (!result.Contains(start)) result.Add(start);
            }
            return result;
        }

        /// <summary>[158차 P3-C] 돌판 경로 오버레이 — BFS 경로 셀에 돌판 쿼드 병합 메시(y+0.015, 렌더러 전용).
        ///   흙 바닥 MeshCollider 단일소스 무영향, UV 셀당 1타일(CreateStonePathMat 2×2 슬랩).</summary>
        private static void CreateStonePath(GameObject room)
        {
            s_pathCells.Clear();
            var targets = new List<Vector2Int>();
            targets.AddRange(s_portalDownCells);
            targets.AddRange(s_portalUpCells);
            var start = ResolvePathStartCell();
            s_pathCells.AddRange(ComputeStonePathCells(start, targets));
            if (s_pathCells.Count == 0)
            {
                Debug.Log("[CaveBuilder] 🪨 돌판 경로 0셀 — 포탈/스폰 마커 부재, 스킵");
                return;
            }
            int count = s_pathCells.Count;
            var vertices = new Vector3[count * 4];
            var uvs = new Vector2[count * 4];
            var normals = new Vector3[count * 4];
            var tris = new int[count * 6];
            const float PathY = 0.015f;
            int v = 0;
            int t = 0;
            for (int i = 0; i < count; i++)
            {
                var cell = s_pathCells[i];
                float x0 = CellWorldX(cell.x) - CELL_SIZE * 0.5f;
                float x1 = CellWorldX(cell.x) + CELL_SIZE * 0.5f;
                float z0 = CellWorldZ(cell.y) - CELL_SIZE * 0.5f;
                float z1 = CellWorldZ(cell.y) + CELL_SIZE * 0.5f;
                vertices[v] = new Vector3(x0, PathY, z0);
                vertices[v + 1] = new Vector3(x1, PathY, z0);
                vertices[v + 2] = new Vector3(x0, PathY, z1);
                vertices[v + 3] = new Vector3(x1, PathY, z1);
                normals[v] = Vector3.up;
                normals[v + 1] = Vector3.up;
                normals[v + 2] = Vector3.up;
                normals[v + 3] = Vector3.up;
                uvs[v] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(0f, 1f);
                uvs[v + 3] = new Vector2(1f, 1f);
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;      // 위향 +Y(캡 패턴 승계)
                tris[t + 3] = v + 2; tris[t + 4] = v + 3; tris[t + 5] = v + 1;
                v += 4;
                t += 6;
            }
            var mesh = new Mesh { name = "CaveStonePathMesh" };
            mesh.vertices = vertices;
            mesh.triangles = tris;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            var path = new GameObject("CaveStonePath");
            path.transform.SetParent(room.transform, false);
            path.AddComponent<MeshFilter>().sharedMesh = mesh;
            path.AddComponent<MeshRenderer>().sharedMaterial = CaveMaterialFactory.CreateStonePathMat();
            Debug.Log($"[CaveBuilder] 🪨 돌판 경로 {count}셀 오버레이(y={PathY}) — BFS 스폰→포탈(158차 P3-C)");
        }

        /// <summary>[158차 P3-C] 흙 자갈 스탬프 — 경로·기둥 제외 바닥 셀 중 해시 8% 선정, 자갈 데칼(0.9m, y+0.010).
        ///   결정론: Hash32(cx, cy)%1000<80 — 유니티 Random 0건 계약.</summary>
        private static void CreateDirtDetail(GameObject room)
        {
            s_gravelCells.Clear();
            int placed = 0;
            for (int i = 0; i < s_floorCells.Count; i++)
            {
                var cell = s_floorCells[i];
                if (s_pathCells.Contains(cell)) continue;
                if (s_pillarCells.Contains(cell)) continue;
                if (s_portalPitCells.Contains(cell)) continue;   // [191차] 계단굴 조각 셀 — 부유 자갈 금지(진입 셀 폴백 대비)
                if (Hash32(cell.x, cell.y) % 1000u >= 80u) continue;
                var world = CellWorld(cell);
                CreateFlatDecal(room, $"CaveGravel_{cell.x:D2}_{cell.y:D2}", world,
                                0.9f, 0.010f, CaveMaterialFactory.CreateGravelMat(), 0f);
                s_gravelCells.Add(cell);
                placed++;
            }
            if (placed > 0)
                Debug.Log($"[CaveBuilder] 🪨 자갈 스탬프 {placed}패치 — 경로 제외 8% 해시 선정(158차 P3-C)");
        }

        /// <summary>[158차 P3-C] 벽 하단 받침 밴드 — 런 길이×0.35m 짙은 돌 플린스 박스(두께 +0.024m), 콜라이더 제거(렌더러 전용).</summary>
        private static void CreateWallBaseBand(GameObject room, WallRun run, float length, int index)
        {
            const float BandHeight = 0.35f;
            const float BandThickness = WALL_THICKNESS + 0.024f;
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = $"CaveWallBand_{index:D2}";
            band.transform.SetParent(room.transform, false);
            if (run.AlongX)
            {
                band.transform.localPosition = new Vector3((run.Start + run.End) * 0.5f, BandHeight * 0.5f, run.Line);
                band.transform.localRotation = Quaternion.identity;
                band.transform.localScale = new Vector3(length, BandHeight, BandThickness);
            }
            else
            {
                band.transform.localPosition = new Vector3(run.Line, BandHeight * 0.5f, (run.Start + run.End) * 0.5f);
                band.transform.localRotation = Quaternion.identity;
                band.transform.localScale = new Vector3(BandThickness, BandHeight, length);
            }
            var col = band.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            band.GetComponent<MeshRenderer>().sharedMaterial = CaveMaterialFactory.CreateBaseBandMat(length);
        }

        /// <summary>[158차 P3-C] FNV-1a 정수 해시 — 자갈 선정 결정론(CaveLayoutPool 수식 승계, 유니티 Random 0건).</summary>
        private static uint Hash32(int x, int y)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h ^= h >> 15;
                return h;
            }
        }

        private static Material CreateWallCapMaterial()
        {
            var capMat = CaveMaterialFactory.CreateCaveWall(4f, 1f);   // 호출마다 신규 인스턴스 — 공유 오염 없음
            capMat.name = "CaveWallTopCapMat";
            capMat.SetColor("_BaseColor", new Color(0.34f, 0.32f, 0.30f));
            return capMat;
        }

        /// <summary>
        /// [156차] 벽 윗면 평면 캡 — 두께 0 평면(y=WALL_CAP_Y, 콜라이더 없음 — 렌더러 전용).
        /// 구 CreateWallCaps(박스 캡)의 "막대기" 오독 원인 제거를 위한 평면 캡 패턴을
        /// 새 셀맵 벽 체계(런 단위)에 맞게 재구현. 이름 CaveWallCap 유지 계약.
        /// </summary>
        private static void CreateWallCap(GameObject room, Material capMat, WallRun run, float length, float mid, int index)
        {
            float hw = (length + 0.02f) * 0.5f;      // 런 길이 + 극소 여유(이음새 갭 방지)
            float hd = (WALL_THICKNESS + 0.02f) * 0.5f;

            var cap = new GameObject($"CaveWallCap_{index:D2}");
            cap.transform.SetParent(room.transform, false);
            if (run.AlongX)
            {
                cap.transform.localPosition = new Vector3(mid, WALL_CAP_Y, run.Line);
                cap.transform.localRotation = Quaternion.identity;
            }
            else
            {
                cap.transform.localPosition = new Vector3(run.Line, WALL_CAP_Y, mid);
                cap.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // 로컬 X → 월드 Z(런 방향)
            }

            var mesh = new Mesh { name = $"CaveWallCapMesh_{index:D2}" };
            mesh.vertices = new[]
            {
                new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd),
                new Vector3(-hw, 0f, hd), new Vector3(hw, 0f, hd),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };   // 위향(+Y) 와인딩 — 성 내부 F1 동일
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            cap.AddComponent<MeshFilter>().sharedMesh = mesh;
            cap.AddComponent<MeshRenderer>().sharedMaterial = capMat;   // 콜라이더 추가 없음 — 평면 장식
        }

        // ================================================================
        // [156차] 마커 배치 — 셀맵 마커 → 월드 결정론(스캔 순: 행→열, rng 소비 0건)
        // ================================================================

        /// <summary>
        /// F 중앙화로 — 기존 CaveHearth 체계 승계(스톤 링+대형 불꽃+Light+TorchFlicker opt-in+암석 링 8).
        /// F 마커 다셀(F1 2셀)은 평균 중심 배치 — 화로 발판 셀들은 바닥 유지(장애물 충돌은 스톤 실린더뿐).
        /// </summary>
        private static void CreateHearth(GameObject zone)
        {
            if (s_hearthCells.Count == 0) return;
            var pos = CellsCentroid(s_hearthCells);

            var hearth = new GameObject("CaveHearth");
            hearth.transform.SetParent(zone.transform, false);
            hearth.transform.localPosition = new Vector3(pos.x, 0f, pos.y);

            var stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stone.name = "CaveHearthStone";
            stone.transform.SetParent(hearth.transform, false);
            stone.transform.localPosition = new Vector3(0f, 0.125f, 0f);
            stone.transform.localScale = new Vector3(1.2f, 0.125f, 1.2f);   // r=0.6, h=0.25
            stone.GetComponent<MeshRenderer>().material =
                MaterialHelper.CreateLitMaterial(new Color(0.35f, 0.32f, 0.30f), "CaveHearthStoneMat");

            // 화로 불꽃 — 대형(×1.6) 토치 모델 + range 9 포인트라이트(TorchFlicker).
            var flame = new GameObject("CaveHearthFlame");
            flame.transform.SetParent(hearth.transform, false);
            flame.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            flame.transform.localScale = Vector3.one * 1.6f;
            CreateTorchModel(flame);
            CreateTorchFlame(flame);   // 불꽃 시각 — 크로스 쿼드 2장(Light 추가 없음, ≤8 계약 유지)
            var light = flame.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = TorchColor;
            light.range = HEARTH_RANGE;
            light.intensity = TORCH_INTENSITY;
            light.shadows = LightShadows.Soft;
            flame.AddComponent<TorchFlicker>();
            flame.GetComponent<TorchFlicker>().ConfigureFlicker(true, 0.85f, 1.15f, 0.85f, 1.15f);   // 베이스 상대 플리커 — range 9 실동작

            CreateHearthRockRing(hearth);   // 화로 주변 암석 링 8 — 하드박제 각도(rng 소비 0건)
        }

        /// <summary>
        /// [156차] 화로 주변 암석 링 8 — 하드박제 각도(45° 간격, rng 소비 0건) 기존 계약 유지.
        /// 로드 실패 시 로그+스킵(선례 준용).
        /// </summary>
        private static void CreateHearthRockRing(GameObject hearth)
        {
            GameObject[] rockPrefabs = null;
            try
            {
                rockPrefabs = NaturalResourceSpawner.LoadRockVisuals();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CaveBuilder] 화로 암석 링 GLB 로드 실패 — 링 생략: " + e.Message);
                return;
            }
            if (rockPrefabs == null || rockPrefabs.Length == 0)
            {
                Debug.Log("[CaveBuilder] 암석 GLB 부재 — 화로 암석 링 생략.");
                return;
            }

            for (int k = 0; k < 8; k++)
            {
                float angle = k * 45f * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(angle) * 0.85f, Mathf.Sin(angle) * 0.85f);
                float scale = 0.30f + 0.05f * (k % 3);

                var rock = new GameObject($"CaveHearthRock_{k:D2}");
                rock.transform.SetParent(hearth.transform, false);
                rock.transform.localPosition = new Vector3(offset.x, 0f, offset.y);
                rock.transform.localRotation = Quaternion.Euler(0f, k * 45f + 22.5f, 0f);

                var visual = Object.Instantiate(rockPrefabs[k % rockPrefabs.Length], rock.transform);
                visual.name = "Visual";
                visual.transform.localScale = Vector3.one * scale;
                StripColliders(visual);           // 링 장식 — 충돌은 화로 스톤/바닥 담당
                AlignVisualBottomToFloor(visual); // 밑면 == 바닥(스포너 선례)
            }
        }

        /// <summary>
        /// [156차] m 채굴노드 — 마커 스캔 순 배치, 타입 결정론(3번째마다 IronOre — 기존 Stone:Iron 비율 2:1 승계).
        /// 큐브 루트+콜라이더 유지, 시각만 GLB 승격(147차 B3 패턴: Stone→Rocks_1_1 / IronOre→Crystal_1).
        /// </summary>
        private static void CreateMiningNodes(GameObject zone)
        {
            Color stoneColor = new Color(0.6f, 0.6f, 0.62f, 1f);    // 회색 — 선례 동일
            Color ironColor = new Color(0.8f, 0.35f, 0.1f, 1f);     // 주황(철광 테마) — 선례 동일

            for (int i = 0; i < s_mineCells.Count; i++)
            {
                var type = (i % 3 == 2) ? ResourceNode.ResourceType.IronOre : ResourceNode.ResourceType.Stone;
                var pos = CellWorld(s_mineCells[i]);
                string typeName = type == ResourceNode.ResourceType.IronOre ? "IronOre" : "Stone";
                CreateMiningNode(zone, $"ResourceNode_{typeName}_F{s_floorIndex + 1}_{i:D2}",
                    new Vector3(pos.x, 0f, pos.y), type, type == ResourceNode.ResourceType.IronOre ? ironColor : stoneColor);
            }
        }

        /// <summary>[156차] C 보석상자 — 마커 수 그대로(F1 4개 고정 계약 승계). GemType 시드 결정론(호출 순서 고정).</summary>
        private static void CreateGemChests(GameObject zone, System.Random rng)
        {
            for (int i = 0; i < s_chestCells.Count; i++)
            {
                var pos = CellWorld(s_chestCells[i]);
                CreateGemChest(zone, $"GemChest_F{s_floorIndex + 1}_{i:D2}", new Vector3(pos.x, 0.3f, pos.y), rng, i);
            }
        }

        /// <summary>
        /// [156차] c 석관 — Cofin GLB(147차 카탈로그), 로드 실패 시 상자 폴백(CreateCrateAt 결정론).
        /// 전부 장식(콜라이더 없음 — 충돌은 바닥 단일소스 계약).
        /// </summary>
        private static void CreateCofins(GameObject zone)
        {
            for (int i = 0; i < s_cofinCells.Count; i++)
            {
                var pos = CellWorld(s_cofinCells[i]);
                if (RuntimeModelLoader.TryGetModel("Cofin", out var model) && model != null)
                {
                    CreateZoneVisual(zone, $"CaveCofin_{i:D2}", "Cofin", pos);
                    continue;
                }
                // 상자 폴백 — 결정론 poolIndex(147차 카탈로그 선례), 장식(콜라이더 스트립).
                var root = new GameObject($"CaveCofin_{i:D2}");
                root.transform.SetParent(zone.transform, false);
                root.transform.localPosition = new Vector3(pos.x, 0f, pos.y);
                var visual = IndoorFurnitureCatalog.CreateCrateAt(i % 2, 0.9f, 0.7f, 1.8f, null);
                if (visual != null)
                {
                    visual.name = "Visual";
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localPosition = Vector3.zero;
                    StripColliders(visual);
                    AlignVisualBottomToFloor(visual);
                }
            }
        }

        /// <summary>
        /// [160차 P2] 던전 소품 패스 — Polyfork 던전 수용분 5종(159차 보류 → 본 패스로 수용).
        /// 결정론: 유일한 무작위원은 인자 rng(System.Random 시드 결정론 — 유니티 Random 금지 계약 유지).
        /// 배치 상한(과밀 배제): 존당 통상 3~4프롭 — 존 최소 폭 기반 스케일.
        ///   최소 폭 4m 미만(2셀 미만): sarcophagus/shelf 생략 → 3종(겔문·철문·모방상자).
        ///   4m 이상 ~ 7m 미만: shelf 생략 → 4종(석관 가산).
        ///   7m 이상(대형 존): 5종 전량 — 층면이 넓어 과밀 없음(상한 논거: 협소 존에서 2.0m급 관·선반의 통로 폐색 방지).
        /// 위치: 존 bounds는 셀맵 파생(s_floorCells min/max — CellsCentroid/CellWorld 선례 패턴 복제).
        /// 콜라이더: PlacePolyfork 자동 제거(렌더러 전용 — 기능 블로킹 없음). 스케일: targetHeight 0 = 자연 크기(미터 실스케일).
        /// </summary>
        private static void CreateDungeonProps(GameObject zone, System.Random rng)
        {
            if (s_floorCells.Count == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var cell in s_floorCells)
            {
                float wx = CellWorldX(cell.x);
                float wz = CellWorldZ(cell.y);
                if (wx < minX) minX = wx;
                if (wx > maxX) maxX = wx;
                if (wz < minZ) minZ = wz;
                if (wz > maxZ) maxZ = wz;
            }
            var center = CellsCentroid(s_floorCells);
            float minSpan = Mathf.Min(maxX - minX, maxZ - minZ);

            int corner = rng.Next(4);   // 코너 선택 — 소비 순서 고정(협소 존에서도 결정론 유지)

            // 1) 석관 — 존 코너 1(벽 인셋 1.2m, 실내 중심 향해 yaw). 최소 폭 4m 미만 생략(상한 스케일).
            if (minSpan >= 4f)
            {
                const float inset = 1.2f;
                float sarcX = (corner % 2 == 0) ? minX + inset : maxX - inset;
                float sarcZ = (corner < 2) ? minZ + inset : maxZ - inset;
                float sarcYaw = Mathf.Atan2(center.x - sarcX, center.y - sarcZ) * Mathf.Rad2Deg;
                InteriorPropCatalog.PlacePolyfork(zone.transform, "stone-sarcophagus-cc96a4",
                    $"CaveDungeonSarcophagus_F{s_floorIndex + 1}", new Vector3(sarcX, 0f, sarcZ), sarcYaw, 0f);
            }

            // 2) 겔 문 — 벽 라인 1(벽 마진 0.3m, 벽면 향함 — 코너 선택 z측과 정합: 남측 코너면 남벽, 북측이면 북벽).
            bool doorOnSouth = corner < 2;
            float doorZ = doorOnSouth ? minZ + 0.3f : maxZ - 0.3f;
            InteriorPropCatalog.PlacePolyfork(zone.transform, "barred-cell-door-571e9c",
                $"CaveDungeonCellDoor_F{s_floorIndex + 1}", new Vector3(center.x, 0f, doorZ), doorOnSouth ? 0f : 180f, 0f);

            // 3) 두개골 선반 — 겔 문 반대 벽 라인(마진 0.4m). 최소 폭 7m 미만 생략(상한 스케일).
            if (minSpan >= 7f)
            {
                float shelfZ = doorOnSouth ? maxZ - 0.4f : minZ + 0.4f;
                InteriorPropCatalog.PlacePolyfork(zone.transform, "ossuary-skull-shelf-a8fdf0",
                    $"CaveDungeonSkullShelf_F{s_floorIndex + 1}", new Vector3(center.x, 0f, shelfZ), doorOnSouth ? 180f : 0f, 0f);
            }

            // 4) 철문 — 존 출구(up 포탈) 추정 위치 옆 1, 부재 시 스폰 셀 옆, 전부 부재 시 임의 벽(동벽) 폴백.
            bool haveExit = s_portalUpCells.Count > 0 || s_spawnCells.Count > 0;
            float ironX, ironZ, ironYaw;
            if (haveExit)
            {
                var exitCell = s_portalUpCells.Count > 0 ? s_portalUpCells[0] : s_spawnCells[0];
                var exitPos = CellWorld(exitCell);
                ironX = Mathf.Clamp(exitPos.x + 1.2f, minX + 0.75f, maxX - 0.75f);
                ironZ = Mathf.Clamp(exitPos.y, minZ + 0.75f, maxZ - 0.75f);
                ironYaw = 0f;
            }
            else
            {
                ironX = maxX - 0.3f;      // 임의 벽 폴백 — 동벽 라인, 서측 향함
                ironZ = center.y;
                ironYaw = -90f;
            }
            InteriorPropCatalog.PlacePolyfork(zone.transform, "iron-bound-door-e0e953",
                $"CaveDungeonIronDoor_F{s_floorIndex + 1}", new Vector3(ironX, 0f, ironZ), ironYaw, 0f);

            // 5) 모방 상자 — 존 내 무작위 위치 1(rng), GemChest 2m 이격(미충족 후보 폐기 8회 시도, 폴백 중심 오프셋).
            var mimicPos = new Vector2(
                Mathf.Clamp(center.x + 1.5f, minX + 0.8f, maxX - 0.8f),
                Mathf.Clamp(center.y, minZ + 0.8f, maxZ - 0.8f));
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float mx = Mathf.Clamp(minX + 0.8f + (float)rng.NextDouble() * (maxX - minX - 1.6f), minX + 0.5f, maxX - 0.5f);
                float mz = Mathf.Clamp(minZ + 0.8f + (float)rng.NextDouble() * (maxZ - minZ - 1.6f), minZ + 0.5f, maxZ - 0.5f);
                var candidate = new Vector2(mx, mz);
                bool clearOfChests = true;
                foreach (var chestCell in s_chestCells)
                {
                    var chestPos = CellWorld(chestCell);
                    if ((candidate - chestPos).sqrMagnitude < 4f)   // 2m 이격 계약(제곱거리 4)
                    {
                        clearOfChests = false;
                        break;
                    }
                }
                if (clearOfChests)
                {
                    mimicPos = candidate;
                    break;
                }
            }
            InteriorPropCatalog.PlacePolyfork(zone.transform, "mimic-chest-57e440",
                $"CaveDungeonMimic_F{s_floorIndex + 1}", new Vector3(mimicPos.x, 0f, mimicPos.y), 0f, 0f);

            // 6) 로프다리 — [161차 P3] 존 중심 서측 오프셋 1(룸당 1·기존 던전 배치들과 2m+ 이격·결정론 yaw 0).
            InteriorPropCatalog.PlacePolyfork(zone.transform, "rope-bridge-span-159f8c", "Dungeon_RopeBridge", new Vector3(center.x - 1.8f, 0f, center.y), 0f, 0f);
        }

        /// <summary>[156차] s 상자 — IndoorFurnitureCatalog.CreateCrate 결정론 배치, 장식(콜라이더 스트립).</summary>
        private static void CreateCrates(GameObject zone)
        {
            for (int i = 0; i < s_crateCells.Count; i++)
            {
                var pos = CellWorld(s_crateCells[i]);
                var root = new GameObject($"CaveCrate_{i:D2}");
                root.transform.SetParent(zone.transform, false);
                root.transform.localPosition = new Vector3(pos.x, 0f, pos.y);
                var visual = IndoorFurnitureCatalog.CreateCrate(0.8f, 0.7f, 0.8f, null);
                if (visual != null)
                {
                    visual.name = "Visual";
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localPosition = Vector3.zero;
                    StripColliders(visual);
                    AlignVisualBottomToFloor(visual);
                }
            }
        }

        /// <summary>[156차] K SkullCandle — 던전 카탈로그 GLB, 조명 없는 비주얼만(TorchFlicker 부착 금지 — ≤8 계약).</summary>
        private static void CreateSkullCandles(GameObject zone)
        {
            for (int i = 0; i < s_skullCells.Count; i++)
                CreateZoneVisual(zone, $"CaveSkullCandle_{i:D2}", "SkullCandle", CellWorld(s_skullCells[i]));
        }

        /// <summary>[156차] o 잔석 — Rocks GLB 소형(스케일 결정론 0.45~0.55, yaw 해시 순환), 장식(콜라이더 스트립).</summary>
        private static void CreateRockAccents(GameObject zone)
        {
            if (s_rockCells.Count == 0) return;
            GameObject[] rockPrefabs = null;
            try
            {
                rockPrefabs = NaturalResourceSpawner.LoadRockVisuals();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CaveBuilder] 잔석 GLB 로드 실패 — 잔석 생략: " + e.Message);
                return;
            }
            if (rockPrefabs == null || rockPrefabs.Length == 0)
            {
                Debug.Log("[CaveBuilder] 암석 GLB 부재 — 잔석 생략.");
                return;
            }

            for (int i = 0; i < s_rockCells.Count; i++)
            {
                var pos = CellWorld(s_rockCells[i]);
                var rock = new GameObject($"CaveRockAccent_{i:D2}");
                rock.transform.SetParent(zone.transform, false);
                rock.transform.localPosition = new Vector3(pos.x, 0f, pos.y);
                rock.transform.localRotation = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);

                var visual = Object.Instantiate(rockPrefabs[i % rockPrefabs.Length], rock.transform);
                visual.name = "Visual";
                visual.transform.localScale = Vector3.one * (0.45f + 0.05f * (i % 3));
                StripColliders(visual);           // 잔석 장식 — 충돌은 바닥 담당
                AlignVisualBottomToFloor(visual);
            }
        }

        /// <summary>
        /// [156차] v 덩굴 — Vines GLB 있으면 마커에 배치(키 4종 결정론 사이클), 없으면 스킵 로그 1행.
        /// </summary>
        private static void CreateVines(GameObject zone)
        {
            if (s_vineCells.Count == 0) return;
            string[] vineKeys = { "Vines_1_1", "Vines_2_1", "Vines_3_1", "Vines_4_1" };
            var loaded = new List<string>();
            foreach (var key in vineKeys)
                if (RuntimeModelLoader.TryGetModel(key, out var model) && model != null)
                    loaded.Add(key);
            if (loaded.Count == 0)
            {
                Debug.LogWarning($"[CaveBuilder] Vines GLB 부재 — 덩굴 마커 {s_vineCells.Count}셀 스킵.");   // 스킵 로그 1행 계약
                return;
            }
            for (int i = 0; i < s_vineCells.Count; i++)
                CreateZoneVisual(zone, $"CaveVine_{i:D2}", loaded[i % loaded.Count], CellWorld(s_vineCells[i]));
        }

        /// <summary>
        /// [156차] w 물웅덩이 — 평면 수면 시각(콜라이더 없음 계약), Idyllic 워터 머티리얼 재사용
        /// (WaterMaterialUpgrader.CreateNationTintedWaterMaterial). 같은 행 연속 w 셀은 하나의 스트립으로 런 머징.
        /// </summary>
        private static void CreateWaterPools(GameObject zone)
        {
            if (s_waterCells.Count == 0) return;
            var wset = new HashSet<Vector2Int>(s_waterCells);
            int placed = 0;
            for (int i = 0; i < s_waterCells.Count; i++)
            {
                var cell = s_waterCells[i];
                if (!wset.Contains(cell)) continue;               // 이미 소속 런 — 스킵
                if (wset.Contains(new Vector2Int(cell.x - 1, cell.y))) continue;   // 런 선두만 스트립 생성
                int end = cell.x;
                while (wset.Contains(new Vector2Int(end + 1, cell.y))) end++;
                for (int cx = cell.x; cx <= end; cx++) wset.Remove(new Vector2Int(cx, cell.y));

                float x0 = CellWorldX(cell.x) - CELL_SIZE * 0.5f;
                float x1 = CellWorldX(end) + CELL_SIZE * 0.5f;
                float z = CellWorldZ(cell.y);
                var center = new Vector3((x0 + x1) * 0.5f, 0.025f, z);

                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = $"CaveWater_{placed:D2}";
                strip.transform.SetParent(zone.transform, false);
                strip.transform.localPosition = center;
                strip.transform.localScale = new Vector3(x1 - x0, 0.05f, CELL_SIZE);
                strip.GetComponent<MeshRenderer>().material =
                    WaterMaterialUpgrader.CreateNationTintedWaterMaterial($"CaveWaterMat_{placed:D2}", center, 0.65f);
                Object.DestroyImmediate(strip.GetComponent<BoxCollider>());   // 수면은 시각 전용 — 콜라이더 없음 계약
                placed++;
            }
            Debug.Log($"[CaveBuilder] 🌊 물웅덩이 수면 {placed}스트립 배치(F{s_floorIndex + 1}) — w {s_waterCells.Count}셀 런 머징, 콜라이더 없음(Idyllic 워터)");
        }

        /// <summary>
        /// [156차] P 기둥 — Pilar GLB(5종 결정론 사이클) + 0.8×0.8m BoxCollider 장애물(바닥은 유지 — 셀 바닥 메시 그대로).
        /// GLB 부재 시 조용한 스킵(CreateZoneVisual 선례).
        /// </summary>
        private static void CreatePillars(GameObject zone)
        {
            string[] pilarKeys = { "Pilar_1_1A", "Pilar_1_2A", "Pilar_1_3A", "Pilar_2_1A", "Pilar_2_2A" };
            for (int i = 0; i < s_pillarCells.Count; i++)
            {
                string key = null;
                for (int k = 0; k < pilarKeys.Length; k++)
                {
                    string candidate = pilarKeys[(i + k) % pilarKeys.Length];
                    if (RuntimeModelLoader.TryGetModel(candidate, out var probe) && probe != null) { key = candidate; break; }
                }
                if (key == null) continue;   // 조용한 스킵 — 장식 부재는 게임플레이를 막지 않음

                var pos = CellWorld(s_pillarCells[i]);
                var root = new GameObject($"CavePillar_{i:D2}");
                root.transform.SetParent(zone.transform, false);
                root.transform.localPosition = new Vector3(pos.x, 0f, pos.y);

                var obstacle = root.AddComponent<BoxCollider>();
                obstacle.size = new Vector3(PILLAR_OBSTACLE, WALL_HEIGHT, PILLAR_OBSTACLE);
                obstacle.center = new Vector3(0f, WALL_HEIGHT * 0.5f, 0f);

                RuntimeModelLoader.TryGetModel(key, out var model);
                var visual = Object.Instantiate(model, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                StripColliders(visual);            // 충돌은 루트 BoxCollider(장애물) 담당 — 바닥은 CaveFloor 단일소스
                AlignVisualBottomToFloor(visual);
            }
        }

        /// <summary>
        /// [156차] t 횟불 — Torch GLB+Light+TorchFlicker(기존 계약 유지). 맵당 t가 8개 — 층당 ≤8 계약 유지.
        /// 색은 마커 순환 결정론(웜/앰버/쿨/청백), 그림자 전부 None(상시 계약 단순화 — Soft는 화로뿐).
        /// </summary>
        private static void CreateWallTorches(GameObject zone)
        {
            Color[] palette =
            {
                new Color(1.0f, 0.72f, 0.42f),   // 웜
                new Color(1.0f, 0.66f, 0.28f),   // 앰버
                new Color(0.72f, 0.85f, 1.0f),   // 쿨(카바이드)
                new Color(0.62f, 0.78f, 0.95f),  // 청백
            };
            for (int i = 0; i < s_torchCells.Count; i++)
            {
                var pos = CellWorld(s_torchCells[i]);
                CreateWallTorch(zone, $"CaveTorch_F{s_floorIndex + 1}_{i:D2}",
                    new Vector3(pos.x, 1.6f, pos.y), LightShadows.None, palette[i % palette.Length]);
                // [157차 B-C] 전 벽횟불 base-relative 플리커 — 화레다만이 아니라 전 횟불 자연화(계약테스트 동행 갱신).
                var torchT = zone.transform.Find($"CaveTorch_F{s_floorIndex + 1}_{i:D2}");
                torchT?.GetComponent<TorchFlicker>()?.ConfigureFlicker(true, 0.9f, 1.1f, 0.85f, 1.15f);
            }
        }

        // ================================================================
        // [143차 P3 승계 → 156차 마커 기반 재배치] 접지 디테일 — 렌더러 전용
        // ================================================================

        /// <summary>
        /// [156차] 접지 디테일 — 채굴노드 블롭(첫 3 m마커)+화로 블롭 1, 마모 스탬프 12(바닥 셀 균등 샘플 — 층 무관 12 보장),
        /// 입구 빛웅덩이 1(E 마커 층만 — F1). 전부 렌더러 전용 데칼 — 콜라이더/프리미티브 미사용.
        /// (구 다각형 좌표 하드박제는 셀맵 전환으로 폐기 — 마커 기반 결정론 재배치.)
        /// </summary>
        private static void CreateGroundDetails(GameObject room)
        {
            // 블롭 — 노드 앞 3 + 화로 1.
            Material blobMat = null;
            try
            {
                blobMat = IndoorMaterialFactory.CreateBlobShadowMat();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CaveBuilder] 블롭 머티리얼 생성 실패 — 블롭 생략: " + e.Message);
            }
            if (blobMat != null)
            {
                int nodeBlobs = Mathf.Min(3, s_mineCells.Count);
                for (int i = 0; i < nodeBlobs; i++)
                {
                    var pos = CellWorld(s_mineCells[i]);
                    CreateFlatDecal(room, $"CaveBlob_Node_{i + 1}", new Vector2(pos.x, pos.y), 2.0f, 0.045f, blobMat, 0f);
                }
                if (s_hearthCells.Count > 0)
                {
                    var hearth = CellsCentroid(s_hearthCells);
                    CreateFlatDecal(room, "CaveBlob_Hearth", hearth, 3.6f, 0.045f, blobMat, 0f);
                }
            }

            // 마모/재 스탬프 12 — 바닥 셀 균등 샘플(스캔 순 k*count/12), yaw = (i%4)*90°.
            Material wearMat = null;
            try
            {
                wearMat = IndoorMaterialFactory.CreateFloorWearMat();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CaveBuilder] 마모 스탬프 머티리얼 생성 실패 — 스탬프 생략: " + e.Message);
            }
            if (wearMat != null && s_floorCells.Count >= 12)
            {
                for (int i = 0; i < 12; i++)
                {
                    var cell = s_floorCells[i * s_floorCells.Count / 12];
                    var pos = CellWorld(cell);
                    CreateFlatDecal(room, $"CaveWearStamp_{i:D2}", new Vector2(pos.x, pos.y), 1.8f, 0.045f, wearMat, (i % 4) * 90f);
                }
            }

            // 입구 빛웅덩이 1 — E 마커 층(F1)만. 문 블로커 안쪽 1셀 북쪽 바닥에 웜 화이트 광원 풀 데칼.
            if (s_entranceCells.Count > 0)
            {
                try
                {
                    var poolMat = CaveMaterialFactory.CreateEntranceLightPoolMat();
                    if (poolMat != null)
                    {
                        var entrance = CellsCentroid(s_entranceCells);
                        CreateFlatDecal(room, "CaveEntranceLightPool", new Vector2(entrance.x, entrance.y - CELL_SIZE), 4.4f, 0.05f, poolMat, 0f);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[CaveBuilder] 입구 빛웅덩이 생성 실패 — 생략: " + e.Message);
                }
            }
        }

        // ================================================================
        // [156차] 포탈 — D/U 마커 좌표(CaveLevelPortal 거리 판정형 계약 유지)
        // ================================================================

        /// <summary>
        /// [156차] 층 전환 포탈 — D/U 마커 셀 좌표에 기존 CreatePortalFrame(Arch GLB 유지)+CaveLevelPortal.Configure.
        /// 메시지 문자열 기존 유지. yaw는 기존 2~5층 규약(D=90/U=270) 승계 — 직교 셀맵 외부 방향 근사.
        /// </summary>
        private static void CreateFloorPortals(GameObject room, int floorIndex)
        {
            // [157차 P-C] 풀 맵 — 포탈 활성은 층 위치로 판정(5층=하강 억제; D/U 앵커는 맵에 상시 존재).
            bool downSuppressed = s_poolActive && floorIndex >= 4;
            if (s_portalUpCells.Count > 0)
            {
                string upName = $"CavePortal_F{floorIndex + 1}_Up";
                CreatePortalFrame(room, upName, CellWorld(s_portalUpCells[0]), 270f);
                room.transform.Find(upName).gameObject.AddComponent<CaveLevelPortal>()
                    .Configure(false, -1, UpPortalPrompt(floorIndex));
            }
            if (s_portalDownCells.Count > 0 && !downSuppressed)
            {
                string downName = $"CavePortal_F{floorIndex + 1}_Down";
                // [191차] 계단굴 성립 시 아치를 굴 바닥으로 이전(트리거 동행) — 미성립 시 기존 평지 좌표 유지.
                Vector2 archXz = CellWorld(s_portalDownCells[0]);
                float archY = 0f;
                if (CreatePortalStairwell(room, out var stairArchXz, out var stairArchY))
                {
                    archXz = stairArchXz;
                    archY = stairArchY;
                }
                CreatePortalFrame(room, downName, archXz, 90f, archY);
                room.transform.Find(downName).gameObject.AddComponent<CaveLevelPortal>()
                    .Configure(true, 1, DownPortalPrompt(floorIndex));
            }
        }

        /// <summary>상승 포탈 메시지 — 기존 문자열 유지 계약.</summary>
        private static string UpPortalPrompt(int floorIndex)
        {
            switch (floorIndex)
            {
                case 0: return "E: 지상으로 귀환";
                default: return $"E: 지하 {floorIndex}층으로";
            }
        }

        /// <summary>하강 포탈 메시지 — 기존 문자열 유지 계약.</summary>
        private static string DownPortalPrompt(int floorIndex)
        {
            return $"E: 지하 {floorIndex + 2}층으로";
        }

        /// <summary>[143차 P7 → 148차 P1-3 승계] 포탈 게이트 — Arch GLB(Arch_1A, 4×3×1.03m) 단일 비주얼.
        /// 로드 실패 시 포스트 2만 폴백. 전부 장식 — 콜라이더 없음(포탈 판정은 거리형 CaveLevelPortal).</summary>
        private static void CreatePortalFrame(GameObject room, string goName, Vector2 xz, float yawDeg, float y = 0f)
        {
            var root = new GameObject(goName);
            root.transform.SetParent(room.transform, false);
            root.transform.localPosition = new Vector3(xz.x, y, xz.y);
            root.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            if (RuntimeModelLoader.TryGetModel("Arch_1A", out var archModel) && archModel != null)
            {
                var visual = Object.Instantiate(archModel, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                StripColliders(visual);             // 포탈은 거리 판정형 — 비주얼 전용
                if (y != 0f)
                    AlignVisualBottomToWorldY(visual, y);   // [191차] 계단우물 하단 아치 — 밑면 == 지정 월드 y(굴 바닥)
                else
                    AlignVisualBottomToFloor(visual);       // 밑면 == 바닥(스포너/잔석 선례)
                return;
            }
            Debug.LogWarning("[CaveBuilder] Arch GLB 부재 — 포탈 프레임 포스트 2만 폴백(린틀/터널 박스 폐기 유지).");

            CreatePortalPost(root.transform, "Post_L", new Vector3(-0.9f, 1.1f, 0f));
            CreatePortalPost(root.transform, "Post_R", new Vector3(+0.9f, 1.1f, 0f));
        }

        /// <summary>[143차 P7] 포탈 포스트 1개 — 0.22×2.2×0.22, 장식(콜라이더 DestroyImmediate).</summary>
        private static void CreatePortalPost(Transform parent, string goName, Vector3 localPos)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = goName;
            post.transform.SetParent(parent, false);
            post.transform.localPosition = localPos;
            post.transform.localScale = new Vector3(0.22f, 2.2f, 0.22f);
            var renderer = post.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = IndoorMaterialFactory.CreateWoodFrame();
            Object.DestroyImmediate(post.GetComponent<BoxCollider>());
        }

        // ================================================================
        // [156차] 1층 입구 봉합 — 루트+블로커만(패널 시각 폐기 — 156차 명세 변경)
        // ================================================================

        /// <summary>
        /// [156차 명세 변경] 입구 봉합 — E 마커 셀 중심에 CaveEntranceDoor 루트 + CaveDoorBlocker BoxCollider만 생성.
        /// 패널 시각 생성 폐기(156차 — 나무 기둥/문 패널 사용자 지시 승계). 블로커는 enabled=true 상시 봉합으로 생성
        /// (낙하 금지 지시 — 156차 P2-3 상시 봉합 명세 변경, CaveEntranceDoor 기본값 갱신과 짝).
        /// 콜라이더는 CaveDoorBlocker 1개뿐 계약 유지.
        /// </summary>
        private static void CreateEntranceDoor(GameObject room)
        {
            if (s_entranceCells.Count == 0) return;   // F2~F5 — 입구 마커 없음(상시 폐쇄층)

            var center = CellsCentroid(s_entranceCells);
            var root = new GameObject("CaveEntranceDoor");
            root.transform.SetParent(room.transform, false);
            root.transform.localPosition = new Vector3(center.x, 0f, center.y);
            root.transform.localRotation = Quaternion.identity;   // 직교 셀맵 — 남측 입구 통로(N-S), 도어 라인 E-W

            // 블로커 — 콜라이더 1개뿐 계약(빈 GO + BoxCollider), enabled=true 상시 봉합(156차).
            var blocker = new GameObject("CaveDoorBlocker");
            blocker.transform.SetParent(root.transform, false);
            blocker.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var box = blocker.AddComponent<BoxCollider>();
            box.size = new Vector3(s_entranceCells.Count * CELL_SIZE, 3.0f, 0.12f);   // E 셀 폭 커버(F1 2셀 = 4m)
            box.enabled = true;   // [156차] 상시 봉합 — 낙하 금지 지시(기본 열림 계약 폐기)

            root.AddComponent<CaveEntranceDoor>();
        }

        // ================================================================
        // 배치물 구성기 — 선례 복제(SetupMiningNode / 기존 CreateGemChest 계약)
        // ================================================================

        /// <summary>채굴 노드 — SetupMiningNode 선례 복제(Cube 0.6³, y=바닥면+0.3, ConfigureResourceType).</summary>
        private static void CreateMiningNode(GameObject parent, string goName, Vector3 xzPos,
            ResourceNode.ResourceType nodeType, Color color)
        {
            // 프리미티브 Cube — Renderer+BoxCollider 동봉이라 ResourceNode.Awake의 GetComponent 캐시 요건 충족.
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = goName;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(xzPos.x, 0.3f, xzPos.z);   // 바닥면(y=0)+0.3 — 선례 계약
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            var r = go.GetComponent<Renderer>();
            if (r != null)
                r.material = MaterialHelper.CreateLitMaterial(color, goName + "_Mat");

            // TryAutoMine/Respawn이 GetComponent<Collider>().enabled 토글 — 선례의 방어 가드 유지.
            if (go.GetComponent<Collider>() == null)
                go.AddComponent<BoxCollider>();

            var node = go.AddComponent<ResourceNode>();
            node.ConfigureResourceType(nodeType);   // [34차] 파생상태 동시 설정

            // [147차 P-B3] 채굴노드 비주얼 승격 — Decor GLB 노드 타입별 하드박제 매핑(결정론 — 무작위원 없음).
            //   Stone→Rocks_1_1 / IronOre→Crystal_1. 루트(이름/좌표/BoxCollider/ResourceNode) 무수정 — 큐브 시각만 off.
            //   GLB 실패 시 큐브 시각 유지(기존 외관 100% 보존 폴백).
            string nodeVisualKey = nodeType == ResourceNode.ResourceType.IronOre ? "Crystal_1" : "Rocks_1_1";
            if (RuntimeModelLoader.TryGetModel(nodeVisualKey, out var nodeModel) && nodeModel != null)
            {
                if (r != null) r.enabled = false;   // 큐브 시각 off — 콜라이더/ResourceNode 루트 유지(GemChest 선례)
                var visual = Object.Instantiate(nodeModel, go.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                StripColliders(visual);   // 비주얼만 — 충돌은 루트 BoxCollider(ResourceNode 토글 대상) 담당
                AlignVisualBottomToWorldY(visual, 0f);   // 밑면 == 바닥(루트 y=0.3·스케일 0.6 보정)
            }
        }

        /// <summary>보석상자(고정 좌표) — 기존 계약 유지: Cube 0.6×0.5×0.6, GemData 색, GemChest 컴포넌트+GemType.
        /// [143차 P5] poolIndex — 전리품 상자 GLB 2종 교차 pick(결정론, rng 소비 0건).</summary>
        private static void CreateGemChest(GameObject parent, string goName, Vector3 localPos, System.Random rng, int poolIndex)
        {
            var chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = goName;
            chest.transform.SetParent(parent.transform, false);
            chest.transform.localPosition = localPos;   // y=0.3 — 원본 계약(밑면 0.05)
            chest.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            var gemTypes = System.Enum.GetValues(typeof(GemType));
            var gemType = (GemType)gemTypes.GetValue(rng.Next(gemTypes.Length));

            var data = GemData.GetGemData(gemType);
            var renderer = chest.GetComponent<MeshRenderer>();
            renderer.material = MaterialHelper.CreateLitMaterial(data.color, goName + "_Mat");

            // [143차 P5] 시각 GLB 치환 — 루트(이름/좌표/BoxCollider/GemChest) 불변, 큐브 시각만 비활성.
            chest.GetComponent<MeshRenderer>().enabled = false;
            var chestVisual = IndoorFurnitureCatalog.CreateCrateAt(poolIndex % 2, 0.6f, 0.7f, 0.6f, null);   // 결정론 — 전리품 상자 GLB 2종 교차
            if (chestVisual != null)
            {
                chestVisual.name = "Visual";
                chestVisual.transform.SetParent(chest.transform, false);
                chestVisual.transform.localPosition = new Vector3(0f, 0f, 0f);
                StripColliders(chestVisual);
                AlignVisualBottomToFloor(chestVisual);   // 루트 피벗이 y=0.3이라 (0,0,0) 착지 시 0.3m 부유 — 밑면 바닥 정렬
            }

            var gemChest = chest.AddComponent<GemChest>();
            gemChest.GemType = gemType;
        }

        // ================================================================
        // 조명 — 기존 유지 계약
        // ================================================================

        private static void CreateAmbientLight(GameObject room)
        {
            GameObject lightGo = new GameObject("CaveAmbientLight");
            lightGo.transform.SetParent(room.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 2f, 0);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.3f, 0.4f, 0.8f);
            light.range = 40f;   // [156차] 셀맵 96×72m 커버 보강(구 19.2는 44m 다각형 외접원 기준 — 중앙광+횟불과 병행)
            light.intensity = 0.3f;
            light.shadows = LightShadows.Soft;
        }

        // ================================================================
        // 횟불(C22-08 계약 유지) — TorchFlicker 재사용
        // ================================================================

        /// <summary>
        /// 벽면/구역 횟불 — CreateTorchModel(토치 모델) + 포인트라이트 + TorchFlicker.
        /// 그림자는 호출부 지정(화로만 Soft — 156차 마커 배치에서 t 전부 None).
        /// </summary>
        private static void CreateWallTorch(GameObject parent, string goName, Vector3 localPos, LightShadows shadows, Color torchColor)
        {
            GameObject torchGo = new GameObject(goName);
            torchGo.transform.SetParent(parent.transform, false);
            torchGo.transform.localPosition = localPos;

            // 시각 토치 모델 — [147차 P-B3] Torch GLB 승격 시도(실패 시 기존 프리미티브 폴백).
            CreateTorchModel(torchGo);
            CreateTorchFlame(torchGo);   // 불꽃 시각 — 크로스 쿼드 2장(Light 추가 없음, ≤8 계약 유지)

            var light = torchGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = torchColor;
            light.range = TORCH_RANGE;
            light.intensity = TORCH_INTENSITY;
            light.shadows = shadows;

            // 플리커 컴포넌트 — 기존 재사용 계약.
            torchGo.AddComponent<TorchFlicker>();
        }

        /// <summary>
        /// Creates a simple torch model (cylinder + small cube base)
        /// [147차 P-B3] 횟불 비주얼 승격 — "Torch" GLB(카탈로그 Lighting 실물) 교체 시도, 실패 시 기존 프리미티브 폴백
        /// (로그 1행 — 정적 플래그). Light/TorchFlicker는 호출부 루트 부착 — 조명 로직 무수정 계약.
        /// GLB는 bounds 중심을 부모 원점(광원점)에 정렬.
        /// </summary>
        private static void CreateTorchModel(GameObject parent)
        {
            // GLB 승격 시도 — 결정론(키 고정, 무작위원 없음). 성공 시 프리미티브 생성 스킵.
            if (RuntimeModelLoader.TryGetModel("Torch", out var torchModel) && torchModel != null)
            {
                var visual = Object.Instantiate(torchModel, parent.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                StripColliders(visual);            // 시각 전용 — 콜라이더 없음(횟불은 상호작용 대상 아님)
                AlignVisualCenterToParent(visual); // bounds 중심 → 부모 원점(광원점)
                return;
            }
            if (!_torchGlbFallbackLogged)
            {
                _torchGlbFallbackLogged = true;
                Debug.LogWarning("[CaveBuilder] Torch GLB 부재 — 기존 프리미티브 토치 폴백(조명 로직 무수정).");
            }

            // Torch handle (cylinder) — 기존 프리미티브(폴백 유지 계약).
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.name = "TorchHandle";
            handle.transform.SetParent(parent.transform, false);
            handle.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            handle.transform.localScale = new Vector3(0.08f, 0.3f, 0.08f);
            Object.DestroyImmediate(handle.GetComponent<CapsuleCollider>());
            var handleRenderer = handle.GetComponent<MeshRenderer>();
            handleRenderer.material = MaterialHelper.CreateLitMaterial(
                new Color(0.35f, 0.25f, 0.15f), "TorchHandleMat");

            // Torch head (small cube)
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "TorchHead";
            head.transform.SetParent(parent.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            head.transform.localScale = new Vector3(0.15f, 0.1f, 0.15f);
            Object.DestroyImmediate(head.GetComponent<BoxCollider>());
            var headRenderer = head.GetComponent<MeshRenderer>();
            headRenderer.material = MaterialHelper.CreateLitMaterial(
                new Color(0.8f, 0.4f, 0.1f), "TorchHeadMat");
        }

        // ================================================================
        // 공용 헬퍼 — 시각 인스턴스 정리/정렬/존/데칼/불꽃(기존 계약 유지)
        // ================================================================

        /// <summary>GLB 시각 인스턴스의 콜라이더 전부 제거 — 충돌 단일 소스 계약.</summary>
        private static void StripColliders(GameObject visual)
        {
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        /// <summary>시각 모델(Visual) 렌더러 bounds 밑면을 바닥(y=0)에 정렬 — Room은 원점 항등 변환이라 월드=로컬.</summary>
        private static void AlignVisualBottomToFloor(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y < 0.0001f) return;   // bounds 미계산(드물게) — 스킵
            visual.transform.localPosition += new Vector3(0f, -bounds.min.y, 0f);
        }

        /// <summary>
        /// [147차 P-B3] 시각 모델 bounds 밑면을 지정 월드 y에 정렬 — 부모 스케일 보정(월드 보정량을 부모 로컬로 변환).
        /// 채굴노드 루트 스케일 0.6 대응용.
        /// </summary>
        private static void AlignVisualBottomToWorldY(GameObject visual, float worldY)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y < 0.0001f) return;   // bounds 미계산(드물게) — 스킵
            var parent = visual.transform.parent;
            if (parent == null) return;
            Vector3 delta = new Vector3(0f, worldY - bounds.min.y, 0f);   // 월드 보정량(밑면 → worldY)
            visual.transform.localPosition += parent.worldToLocalMatrix.MultiplyVector(delta);
        }

        /// <summary>[147차 P-B3] 시각 모델 bounds 중심을 부모 원점에 정렬 — 횟불 GLB(광원점 기준).</summary>
        private static void AlignVisualCenterToParent(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            var parent = visual.transform.parent;
            if (parent == null) return;
            Vector3 delta = parent.position - bounds.center;   // 월드 보정량 — bounds 중심 → 부모 원점(광원점)
            visual.transform.localPosition += parent.worldToLocalMatrix.MultiplyVector(delta);
        }

        private static GameObject CreateZone(GameObject room, string zoneName)
        {
            var zone = new GameObject(zoneName);
            zone.transform.SetParent(room.transform, false);
            return zone;
        }

        /// <summary>
        /// [P9 2단 승계] 구역 GLB 비주얼 1개 — TryGetModel→Instantiate→StripColliders→AlignVisualBottomToFloor.
        /// GLB 부재 시 조용한 스킵(로그 없음 — 폴백 스팸 방지). alignBottom=false면 y 좌표 존중(밑면 접지 생략).
        /// 전부 장식 — 충돌은 바닥 단일소스 계약.
        /// </summary>
        private static void CreateZoneVisual(GameObject parent, string goName, string glbKey, Vector2 xz, float y = 0f, bool alignBottom = true)
        {
            if (!RuntimeModelLoader.TryGetModel(glbKey, out var model) || model == null) return;   // 조용한 스킵
            var root = new GameObject(goName);
            root.transform.SetParent(parent.transform, false);
            root.transform.localPosition = new Vector3(xz.x, y, xz.y);
            var visual = Object.Instantiate(model, root.transform);
            visual.name = "Visual";
            StripColliders(visual);   // 장식 — 충돌은 바닥 단일소스 계약
            if (alignBottom) AlignVisualBottomToFloor(visual);
        }

        /// <summary>
        /// [143차 P3 승계] 평평한 바닥 데칼 — 4정점 쿼드 메시+렌더러(콜라이더 없음).
        /// AddFloorWearStamps 선례 패턴: Euler(90, yaw, 0)로 수평 눕히기. 프리미티브 미사용.
        /// </summary>
        private static void CreateFlatDecal(GameObject parent, string goName, Vector2 xz, float size, float yHeight, Material mat, float yawDeg)
        {
            if (mat == null) return;
            var decal = new GameObject(goName);
            decal.transform.SetParent(parent.transform, false);
            decal.transform.localPosition = new Vector3(xz.x, yHeight, xz.y);
            decal.transform.localRotation = Quaternion.Euler(90f, yawDeg, 0f);

            float h = size * 0.5f;
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-h, -h, 0f), new Vector3(h, -h, 0f),
                new Vector3(-h, h, 0f), new Vector3(h, h, 0f),
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            decal.AddComponent<MeshFilter>().sharedMesh = mesh;
            decal.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// [143차 P4 승계] 수직 쿼드 — 4정점 메시(밑변 y=0, 높이 h)+렌더러(콜라이더 없음).
        /// 불꽃/기둥형 시각용. CreateFlatDecal 선례 패턴(빈 GO+MeshFilter+MeshRenderer).
        /// </summary>
        private static void CreateVerticalQuad(GameObject parent, string goName, Vector3 localPos, float width, float height, Material mat, float yawDeg)
        {
            if (mat == null) return;
            var quad = new GameObject(goName);
            quad.transform.SetParent(parent.transform, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);

            float hw = width * 0.5f;
            var mesh = new Mesh { name = goName + "_Mesh" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-hw, 0f, 0f), new Vector3(hw, 0f, 0f),
                new Vector3(-hw, height, 0f), new Vector3(hw, height, 0f),
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            quad.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// [143차 P4 승계] 불꽃 시각 — 크로스 쿼드 2장(yaw 0°/90°)+절차 불꽃 텍스처(CaveMaterialFactory).
        /// Light 추가 0건·TorchFlicker 부착 0건 — 횟불 ≤8 계약 불변. 머티리얼 실패 시 스킵.
        /// </summary>
        private static void CreateTorchFlame(GameObject torchGo)
        {
            Material flameMat = null;
            try
            {
                flameMat = CaveMaterialFactory.CreateFlameMat();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CaveBuilder] 불꽃 머티리얼 생성 실패 — 불꽃 쿼드 생략: " + e.Message);
            }
            if (flameMat == null) return;

            var flameGo = new GameObject("CaveFlame");
            flameGo.transform.SetParent(torchGo.transform, false);
            flameGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);

            CreateVerticalQuad(flameGo, "CaveFlameQuad_A", new Vector3(0f, 0f, 0f), 0.22f, 0.42f, flameMat, 0f);
            CreateVerticalQuad(flameGo, "CaveFlameQuad_B", new Vector3(0f, 0f, 0f), 0.22f, 0.42f, flameMat, 90f);
        }
    }

    /// <summary>
    /// C22-08: Flickering torch light animation component.
    /// Randomly varies light intensity and range to simulate torch flicker.
    /// </summary>
    public class TorchFlicker : MonoBehaviour
    {
        [SerializeField] private float _minIntensity = 0.6f;
        [SerializeField] private float _maxIntensity = 1.4f;
        [SerializeField] private float _minRange = 5f;
        [SerializeField] private float _maxRange = 7f;
        [SerializeField] private float _flickerSpeed = 8f;

        // [143차 P4] 옵트인 — 기본 false면 현행 절대값 플리커 불변(기존 계약 유지).
        [SerializeField] private bool _useBaseRelative = false;
        [SerializeField] private float _minIntensityFactor = 0.85f;
        [SerializeField] private float _maxIntensityFactor = 1.15f;
        [SerializeField] private float _minRangeFactor = 0.85f;
        [SerializeField] private float _maxRangeFactor = 1.15f;

        private Light _light;
        private float _baseIntensity;
        private float _baseRange;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null)
            {
                _baseIntensity = _light.intensity;
                _baseRange = _light.range;
            }
        }

        /// <summary>[143차 P4] 베이스 상대 플리커 옵트인 — 계수 지정(기본 절대값 경로는 무변경).</summary>
        public void ConfigureFlicker(bool useBaseRelative, float minIntensityFactor, float maxIntensityFactor,
            float minRangeFactor, float maxRangeFactor)
        {
            _useBaseRelative = useBaseRelative;
            _minIntensityFactor = minIntensityFactor;
            _maxIntensityFactor = maxIntensityFactor;
            _minRangeFactor = minRangeFactor;
            _maxRangeFactor = maxRangeFactor;
        }

        private void Update()
        {
            if (_light == null) return;

            // Perlin noise-based flicker for natural-looking variation
            float noiseVal = Mathf.PerlinNoise(Time.time * _flickerSpeed, transform.position.x * 0.1f);
            if (_useBaseRelative)
            {
                // [143차 P4] 베이스 상대 — Awake 캡처값 배수(화로 range 9 클램프 버그 수리).
                _light.intensity = Mathf.Lerp(_baseIntensity * _minIntensityFactor, _baseIntensity * _maxIntensityFactor, noiseVal);
                _light.range = Mathf.Lerp(_baseRange * _minRangeFactor, _baseRange * _maxRangeFactor, noiseVal);
                return;
            }
            _light.intensity = Mathf.Lerp(_minIntensity, _maxIntensity, noiseVal);
            _light.range = Mathf.Lerp(_minRange, _maxRange, noiseVal);
        }
    }
}
