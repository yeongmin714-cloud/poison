// 2026-10-23: 'The Rusty Dragon Inn' 여관 실내 빌더 — 전면 재설계.
// 설계 규격: 100 설계 단위 = 1m (authored) × 2.45 = 최종 좌표 스케일.
// 최종 방 크기: Width 45.6 (x) × Depth 61.3 (z) × Height 8m (천장 없음 — Top-Down Principle).
// 좌표 규약: 룸 중심 원점, +z=북(North), -z=남(South).
#pragma warning disable 618
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// The Rusty Dragon Inn 여관 실내 빌더.
    /// IndoorBuilder.CreateRoom 셸 + Bar/Fireplace/Booth/Kitchen/Storage/Stairs/Stage 등 시설 구현.
    /// </summary>
    public static class ArenaInteriorBuilder
    {
        // ── The Rusty Dragon Inn (회색 용 여관) 레이아웃 상수 ─────────────────────────────────────────────
        public const float ROOM_WIDTH = 45.6f;
        public const float ROOM_HEIGHT = 8f;
        public const float ROOM_DEPTH = 61.3f;
        /// <summary>사용자 제공 GLB 규약 경로 (Models/UserProvided/arena_&lt;nation&gt;.glb).</summary>
        public const string ARENA_GLB_PREFIX = "arena_";

        /// <summary>국가 이름 → GLB 키 (사용자 GLB 파일명 규약: arena_&lt;nation&gt;.glb).</summary>
        public static string ArenaGlbKey(string nationStyle)
        {
            string n = (nationStyle ?? "empire").ToLower();
            switch (n)
            {
                case "eastern":  return ARENA_GLB_PREFIX + "east";
                case "western":  return ARENA_GLB_PREFIX + "west";
                case "southern": return ARENA_GLB_PREFIX + "south";
                case "northern": return ARENA_GLB_PREFIX + "north";
                default:         return ARENA_GLB_PREFIX + "empire";
            }
        }

        // ── [159차 P2] 레스토랑 GLB 가구 팩 (Resources/Models/Restaurant/) 교체 상수/헬퍼 ──────────────────
        //   GLB 실측(glb_bounds.py/glb_shape.py): Table_2/3=원형(rCV 0.035, Ø1.24/1.37×h0.80), Table_6=정사각형
        //   (1.21×1.27×h0.88), Chair_1=h1.04, Furnit_*_10/15=벤치(1.8×0.36), Furnit_*_12=캐비닛(0.90×0.36×0.94),
        //   Stool_1=원형 통형(32/36 bins, Ø0.44×h0.55), StationTable_1=조리대(1.25×0.88×h1.00),
        //   StationTable_3=장 카운터(2.50×0.88×h1.00), Sink=0.90×0.90×h1.30, Owen=0.90×0.94×h1.04.
        /// <summary>159차 P2 GLB 가구 목표 높이(m) — 159차 계획 §P2 (탁자 0.75/의자·벤치 0.9/카운터 1.0).</summary>
        private const float GlbTableHeight = 0.75f;
        private const float GlbChairHeight = 0.9f;
        private const float GlbBenchHeight = 0.9f;
        private const float GlbPartitionHeight = 1.5f;
        private const float GlbBarrelHeight = 1.0f;
        private const float GlbCounterHeight = 1.0f;

        /// <summary>변형 알파 계열 (159차 계획 §P2 — A~G). 팩에 없는 변형은 자동 스킵(다음 후보 순환).</summary>
        private static readonly string[] GlbVariantLetters = { "A", "B", "C", "D", "E", "F", "G" };

        /// <summary>Furnit 계열은 알파 접미사 대신 패밀리 문자(A,B,C,E,F,G,H — 팩에 D 없음)가 변형 축이다.</summary>
        private static readonly string[] GlbFurnitFamilies = { "A", "B", "C", "E", "F", "G", "H" };

        /// <summary>알파 변형 키 배열 생성 (예: "Chair_1_" → Chair_1_A ~ Chair_1_G).</summary>
        private static string[] LetterKeys(string prefix)
        {
            var keys = new string[GlbVariantLetters.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = prefix + GlbVariantLetters[i];
            return keys;
        }

        /// <summary>Furnit 패밀리 변형 키 배열 생성 (예: 10 → Furnit_A_10 ~ Furnit_H_10).</summary>
        private static string[] FurnitKeys(int modelNumber)
        {
            var keys = new string[GlbFurnitFamilies.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = "Furnit_" + GlbFurnitFamilies[i] + "_" + modelNumber;
            return keys;
        }

        /// <summary>
        /// [159차 P2] 정수 해시(FNV-1a) — Random API 0건 계약. 변형 순환/국가 오프셋의 결정론 근원.
        /// public — 변형 순환 결정론 계약 테스트(ArenaFurnitureGlbTests)에서 직접 검증.
        /// </summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
                return (int)(h & 0x7FFFFFFFu);
            }
        }

        /// <summary>
        /// [159차 P2] 변형 후보 키를 시드 해시 순서로 순환 시도 — 첫 로드 성공 키를 반환한다.
        /// 시드에 배치 인덱스 + nation을 넣어 동종 연속 배치가 변형 A~G로 결정론 순환되고,
        /// 국가별 오프셋은 hash(nation)으로만 들어간다(159차 계획 §P2).
        /// public — 변형 순환/폴백 계약 테스트(ArenaFurnitureGlbTests)에서 직접 검증.
        /// </summary>
        public static bool TryGetVariantModel(string[] keys, string seed, out GameObject model, out string usedKey)
        {
            model = null;
            usedKey = null;
            if (keys == null || keys.Length == 0) return false;
            int h = StableHash(seed ?? "arena");
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[(h + i) % keys.Length];
                if (RuntimeModelLoader.TryGetModel(k, out model)) { usedKey = k; return true; }
                model = null;
            }
            return false;
        }

        /// <summary>
        /// [159차 P2] GLB 가구 배치 공용 — 변형 순환 로드 → NormalizeNpcVisual 정규화(목표 높이+바닥 접지) → 배치.
        /// 로드 실패 시 null 반환 — 호출자가 기존 프리미티브로 폴백(가구 소실 금지 계약).
        /// 머티리얼 교체 금지(141차) — GLB 원본 렌더러 그대로 사용.
        /// </summary>
        /// <param name="addBoxCollider">바닥 접촉 충돌 유지 계약(베팅 데스크) — GLB는 콜라이더가 없어 바운즈 박스 보정.</param>
        private static GameObject PlaceGlbFurniture(GameObject room, string[] keys, string seed, string name,
            Vector3 position, float yaw, float targetHeight, bool addBoxCollider = false)
        {
            if (!TryGetVariantModel(keys, seed, out var prefab, out var usedKey) || prefab == null)
                return null;

            var inst = Object.Instantiate(prefab, room.transform, false);
            inst.name = name;
            inst.transform.localPosition = position;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            NormalizeNpcVisual(inst, targetHeight);

            if (addBoxCollider)
            {
                var rends = inst.GetComponentsInChildren<Renderer>();
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    // 회전과 무관하게 월드 축 정렬 박스 — 항등 회전 자식에 부착(원본 데스크 MeshCollider 충돌 면적 승계).
                    var colGo = new GameObject(name + "_Collider");
                    colGo.transform.SetParent(inst.transform, false);
                    colGo.transform.position = b.center;
                    colGo.transform.rotation = Quaternion.identity;
                    colGo.AddComponent<BoxCollider>().size = b.size;
                }
            }
            Debug.Log($"[ArenaInteriorBuilder] [159차 P2] GLB 가구 배치 — {name} ← {usedKey} (h→{targetHeight}m, yaw {yaw}°)");
            return inst;
        }

        /// <summary>
        /// The Rusty Dragon Inn 여관 셸 생성 — 주요 시설물 (바/화롯불/부스/부엌/창고 등) 포함.
        /// </summary>
        public static GameObject BuildArenaInterior(string nationStyle)
        {
            try
            {
                string n = (nationStyle ?? "empire").ToLower();

                Material wallMat = ResolveWallMaterial(n, nationStyle);
                GameObject room = IndoorBuilder.CreateRoom(ROOM_WIDTH, ROOM_HEIGHT, ROOM_DEPTH,
                    ResolveFloorMaterial(n, nationStyle), wallMat, ResolveCeilingMaterial());
                if (room == null)
                {
                    Debug.LogError("[ArenaInteriorBuilder] IndoorBuilder.CreateRoom returned null.");
                    return new GameObject("Room_Fallback");
                }
                room.name = "ArenaRoom";
                room.transform.position = Vector3.zero;

                // ── [Top-Down Principle] 천장 파괴 (실내 셸이 생성하더라도 Top-Down 시점을 위해 제거) ──
                Transform ceiling = room.transform.Find("Ceiling");
                if (ceiling != null) Object.DestroyImmediate(ceiling.gameObject);

                float halfWidth = ROOM_WIDTH * 0.5f;  // 22.8
                float halfDepth = ROOM_DEPTH * 0.5f;  // 30.65

                // ── 1. MAIN ENTRANCE (남동측 개구부, 폭 8m — 기존 ArenaSouthEntry 패턴 재사용) ──────
                //    x=+11 (동측) 기준, 폭 8 (x=+7 부터 x=+15까지).
                Transform backWall = room.transform.Find("Wall_Back");
                if (backWall != null) Object.DestroyImmediate(backWall.gameObject); // EditMode 즉시 파괴 (성 빌더 선례)
                IndoorBuilder.CreateSolidInteriorWall(room, "ArenaSouthEntry_Left",
                    new Vector2(-halfWidth, -halfDepth), new Vector2(halfWidth + 7f, -halfDepth),
                    ROOM_HEIGHT, 0.5f, 0f, 0f, 0f, wallMat);
                IndoorBuilder.CreateSolidInteriorWall(room, "ArenaSouthEntry_Right",
                    new Vector2(halfWidth + 15f, -halfDepth), new Vector2(halfWidth, -halfDepth),
                    ROOM_HEIGHT, 0.5f, 0f, 0f, 0f, wallMat);

                // ── 베팅 데스크 (중앙 남쪽, 투기장 문 앞) ──────────────────────────
                PlaceBettingDesk(room, wallMat, n);

                // ── 아레나 관리자 NPC — E키로 ArenaMenuUI(직접 싸우기/병사 출전/베팅/전투 로그) ──
                SpawnArenaNpc(room, n);

                // ── [121차] 베팅 관리인 NPC — 클릭 → 상호판넬(대화하기/베팅하기, 피그마 261:3) ──
                SpawnBetManager(room, nationStyle);

                // ── 4. 투기장 출입문 자리 — 장식 문(수동 트리거 폐지) ─────────────────────────────
                // [125차 후속36 C] 로비→투기장 수동 트리거(CreateArenaPitTrigger) 폐기 —
                //   베팅 확정 시 ArenaBettingSystem이 EnterBuildingViaReflection(TYPE_ARENA_PIT)으로
                //   자동 전환(L209 불변)하므로 수동 진입은 중복(사용자 결정).
                //   북벽 문 자리는 장식 문으로 유지(구조 인지용 — 상호작용 없음).
                float pitDoorZ = halfDepth;
                var decoDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                decoDoor.name = "ArenaPit_Door_Decor";
                decoDoor.transform.SetParent(room.transform);
                decoDoor.transform.localPosition = new Vector3(0f, 1.5f, pitDoorZ - 0.2f);
                decoDoor.transform.localScale = new Vector3(2f, 3f, 0.4f);
                Object.DestroyImmediate(decoDoor.GetComponent<Collider>());   // 렌더러 전용 — 상호작용 없음
                var decoRend = decoDoor.GetComponent<MeshRenderer>();
                if (decoRend != null) decoRend.sharedMaterial = wallMat;

                // ── [125차 후속36 B] 로비 출구 문 — 남동측 개구부 중앙(x=+11), E키 → 진입 전 월드 복귀 ──
                //   투기장 출구 선례(BuildingType "Exit") 재사용 — BuildingEvents.OnExitBuildingRequest →
                //   IndoorSceneTransition.ExitBuilding() → 진입 전 월드 씬 복귀. 메인씬 통합 시도 동일 경로.
                //   문/기둥/상인방 전부 렌더러 전용 — 개구부 보행 무차단(메인씬 진입로 통과 보장).
                const float exitDoorX = 11f;
                var exitDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                exitDoor.name = "ArenaLobby_ExitDoor";
                exitDoor.transform.SetParent(room.transform);
                exitDoor.transform.localPosition = new Vector3(exitDoorX, 1.5f, -(halfDepth - 0.25f));
                exitDoor.transform.localScale = new Vector3(2.2f, 3f, 0.3f);
                Object.DestroyImmediate(exitDoor.GetComponent<Collider>());
                var exitRend = exitDoor.GetComponent<MeshRenderer>();
                if (exitRend != null) exitRend.sharedMaterial = wallMat;
                var exitTrigger = exitDoor.AddComponent<BuildingTrigger>();
                exitTrigger.BuildingType = "Exit";
                for (int s = -1; s <= 1; s += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = $"ArenaLobby_ExitPost_{(s < 0 ? "L" : "R")}";
                    post.transform.SetParent(room.transform);
                    post.transform.localPosition = new Vector3(exitDoorX + s * 1.35f, 1.7f, -(halfDepth - 0.25f));
                    post.transform.localScale = new Vector3(0.35f, 3.4f, 0.45f);
                    Object.DestroyImmediate(post.GetComponent<Collider>());
                    var pr = post.GetComponent<MeshRenderer>();
                    if (pr != null) pr.sharedMaterial = wallMat;
                }
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "ArenaLobby_ExitLintel";
                lintel.transform.SetParent(room.transform);
                lintel.transform.localPosition = new Vector3(exitDoorX, 3.55f, -(halfDepth - 0.25f));
                lintel.transform.localScale = new Vector3(3.4f, 0.3f, 0.5f);
                Object.DestroyImmediate(lintel.GetComponent<Collider>());
                var lr = lintel.GetComponent<MeshRenderer>();
                if (lr != null) lr.sharedMaterial = wallMat;
                Debug.Log("[ArenaInteriorBuilder] [125차 후속36 B] 로비 출구 문 배치 — 남동측 개구부 중앙, BuildingTrigger Exit(E키 → 월드 복귀)");

                // ── 5. 사용자 GLB 규약 로드 ──────────────────────────────────────────────────────────
                TryLoadArenaGlb(room, n, nationStyle);

                // ── 6. THE RUSTY DRAGON INN 주요 시설물 (설계도 레이아웃 배치) ─────────────────────────
                // Stage (북서) — 반원형 알코브 돌출 + 무대 판자 + 악기 장식
                BuildStage(room, wallMat);
                // Private Booths (북벽 따라) — 2인용 부스 탁자+벤치+격벽
                BuildPrivateBooths(room, wallMat, n);
                // Storage (북동) — enclosed 방 + 오크통
                BuildStorage(room, wallMat, n);
                // Stairs (북동 동벽) — 직선 나무 계단
                BuildStairs(room, wallMat);
                // Kitchen (남서 사분면) — 조리대/개수대/화로/오크통
                BuildKitchen(room, wallMat, n);
                // Fireplace (방 중앙) — 원형 돌 굴뚝 + 주황 러그
                BuildFireplace(room, wallMat);
                // Table Area (남동 홀) — 원형/사각 탁자 + 의자
                BuildTableArea(room, wallMat, n);

                // ── 7. [2026-10-23 Quality Layers — The Rusty Dragon Inn 계약] ──────────────────────
                //   ① AddQuadWallCaps (개수 >= 5, 남쪽 입구 상단 AddBoxCap 2개 포함)
                //   ② AddWearStamps 6장 (y=0.02)
                //   ③ AddBlobAt 총 12개 (주요 가구마다 접지 블롭)
                int caps = IndoorQualityLayers.AddQuadWallCaps(room, ROOM_WIDTH, ROOM_DEPTH, ROOM_HEIGHT);
                IndoorQualityLayers.AddBoxCap(room, "ArenaSouthEntry_Left_TopCap",
                    new Vector3(halfWidth + 11f, ROOM_HEIGHT + 0.075f, -halfDepth), new Vector3(8f, 0.15f, 0.5f));
                IndoorQualityLayers.AddBoxCap(room, "ArenaSouthEntry_Right_TopCap",
                    new Vector3(halfWidth + 11f, ROOM_HEIGHT + 0.075f, -halfDepth), new Vector3(8f, 0.15f, 0.5f));
                IndoorQualityLayers.AddWearStamps(room, "ArenaWear", new[]
                {
                    // [125차 후속35-R P4-2] 마모 스탬프 방 밖 좌표 정정 — halfWidth+11f(33.8)/halfWidth+5f(27.8)는
                    //   방(halfWidth 22.8) 밖이라 미가시였음(후속34 O3 실측). 전부 방 내부로 회수.
                    new Vector2(halfWidth - 3f, -halfDepth + 5f), new Vector2(0f, -20f),
                    new Vector2(halfWidth - 3f, 0f), new Vector2(-halfWidth + 5f, 0f),
                    new Vector2(0f, 20f), new Vector2(halfWidth - 3f, halfDepth - 5f),
                }, 2.0f, 0.02f);

                // [125차 후속35-R P4-1] 상부 들보 링 — tavern_wall_timber 소비(CreateTavernWoodFrame 첫 사용처).
                //   벽 상단 접착 얇은 요소(벽톱면캡 계열) — 천장/존매트/걸레받이 금지 계약과 무충돌
                //   (두께 0.25m, 벽면 부착 — 탑다운 중심 시야 무가림).
                var beamMat = IndoorMaterialFactory.CreateTavernWoodFrame();
                if (beamMat != null)
                {
                    float beamY = ROOM_HEIGHT - 0.125f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var beamX = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        beamX.name = $"Arena_Beam_X_{(s < 0 ? "S" : "N")}";
                        beamX.transform.SetParent(room.transform);
                        beamX.transform.localPosition = new Vector3(0f, beamY, s * (halfDepth - 0.125f));
                        beamX.transform.localScale = new Vector3(ROOM_WIDTH + 0.5f, 0.25f, 0.25f);
                        Object.DestroyImmediate(beamX.GetComponent<Collider>());   // 렌더러 전용
                        var bxR = beamX.GetComponent<MeshRenderer>();
                        if (bxR != null) bxR.sharedMaterial = beamMat;

                        var beamZ = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        beamZ.name = $"Arena_Beam_Z_{(s < 0 ? "W" : "E")}";
                        beamZ.transform.SetParent(room.transform);
                        beamZ.transform.localPosition = new Vector3(s * (halfWidth - 0.125f), beamY, 0f);
                        beamZ.transform.localScale = new Vector3(0.25f, 0.25f, ROOM_DEPTH + 0.5f);
                        Object.DestroyImmediate(beamZ.GetComponent<Collider>());
                        var bzR = beamZ.GetComponent<MeshRenderer>();
                        if (bzR != null) bzR.sharedMaterial = beamMat;
                    }
                    Debug.Log("[ArenaInteriorBuilder] [125차 후속35-R P4-1] 상부 들보 링 4본 — tavern_wall_timber 소비");
                }

                Debug.Log($"[ArenaInteriorBuilder] 품질 계층 적용 — 벽 캡 {caps + 2}개, 마모 6장");

                Debug.Log($"[ArenaInteriorBuilder] Rusty Dragon Inn 여관 생성 완료 (nationStyle: {nationStyle}, 유형 {GetArenaKind(n)})");

                // [125차 후속7] 아레나 로비 조명 밝게 — 후속5 값(앰비언트 (0.10,0.09,0.07)×0.55 ≈ 유효 0.05)은
                //   사실상 '어둠 유지'였고, 게다가 ambientMode가 Skybox여서 앰비언트 자체가 무효였다.
                //   근본 수리(IndoorLighting.SetupIndoorLighting이 Flat 강제)로 앰비언트가 이제 실제로 적용되므로
                //   값을 상향한다. ① 앰비언트 (0.22,0.18,0.14) — 성 내부 46차 밝기 선례(유효 ≈0.26/0.20/0.14)와 동급.
                //   ② 중앙 PointLight 1.6→2.4·18m→26m — 45.6×61.3 대형 방에서 로비(남측 z=-15 부근)까지 도달.
                IndoorLighting.SetupIndoorLighting(room, new Color(0.38f, 0.32f, 0.25f), 1.0f, true, 3.0f, 26f);   // [125차 후속8] 대형 홀 — 앰비언트 배가(0.22→0.38) + 중앙광 2.4→3.0 / [125차 후속35-R P1-3] 중앙광 플리커(FlickerRunner 재사용)
                // [125차 후속7] 로비 전용 보조광 3등 — 중앙광만으로는 남측 로비가 어둡다.
                //   (베팅 데스크 위 / 남동 입구 / 관리인 대기석 — 관리인 NPC 가시성 확보 겸용.
                //    y는 천장 없는 Top-Down 방에서 부유 광원 — 성 내부 랜턴 선례와 동일 방식.)
                //   [125차 후속35-R P1-3] 촛불 인상 플리커 3등.
                IndoorLighting.AddPointLight(room, new Vector3(0f, 4.2f, -15f), new Color(1f, 0.82f, 0.58f), 10f, 2.2f, flicker: true);    // 베팅 데스크·바 위
                IndoorLighting.AddPointLight(room, new Vector3(11f, 4.2f, -26f), new Color(1f, 0.82f, 0.58f), 9f, 2.0f, flicker: true);    // 남동 입구
                IndoorLighting.AddPointLight(room, new Vector3(3.5f, 3.6f, -11.5f), new Color(1f, 0.85f, 0.62f), 6f, 1.8f, flicker: true); // 관리인 대기석

                // [125차 후속8] 대형 홀 균일 보강광(Direction) — 45.6×61.3 홀은 포인트라이트 감쇠로 전역 밝기가
                //   부족하다(로비 제외 구역이 반쯤 어두움). 방향광 필 1개로 홀 전체를 균일하게 밝힌다.
                //   탑다운 원칙과 무충돌(기하 추가 없음 — 광원만). room 하위 → 실내 퇴장(씬 언로드) 시 자동 소멸.
                var fillSunGo = new GameObject("ArenaFillSun");
                fillSunGo.transform.SetParent(room.transform, false);
                fillSunGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
                var fillSun = fillSunGo.AddComponent<Light>();
                fillSun.type = LightType.Directional;
                fillSun.color = new Color(1f, 0.94f, 0.82f);
                fillSun.intensity = 0.75f;
                // [125차 후속35-R P1-1] 방향광 Soft 섀도우 — 가구·벽 그림자가 오크 판자에 실림.
                //   H4의 포인트 큐브 섀도우 점무늬 노이즈(실내씬 7/8 지적)와 무관 — 방향광 섀도우는 고밀도.
                //   포인트 섀도우는 기존 None 유지(H4 계약 존중).
                fillSun.shadows = LightShadows.Soft;
                // [125차 후속8] 투기장(북측) 횃불 4곳 — 북벽 투기장 문(pitDoorZ=halfDepth) 주변은 광원 0이라 어둡다.
                //   [125차 후속35-R P1-3] 횃불 플리커 4곳.
                IndoorLighting.AddPointLight(room, new Vector3(-7f, 4.2f, 18f), new Color(1f, 0.82f, 0.58f), 9f, 2.0f, flicker: true);  // 투기장 서북 횃불
                IndoorLighting.AddPointLight(room, new Vector3(7f, 4.2f, 18f), new Color(1f, 0.82f, 0.58f), 9f, 2.0f, flicker: true);   // 투기장 동북 횃불
                IndoorLighting.AddPointLight(room, new Vector3(-7f, 4.2f, 28f), new Color(1f, 0.82f, 0.58f), 9f, 2.0f, flicker: true);  // 투기장 서측 문턱 횃불
                IndoorLighting.AddPointLight(room, new Vector3(7f, 4.2f, 28f), new Color(1f, 0.82f, 0.58f), 9f, 2.0f, flicker: true);   // 투기장 동측 문턱 횃불
                // [125차 후속35-R P1-5] 북측 투기장 문 쿨톤 림 — 웜광 대비 심화(입구 역광 인상).
                IndoorLighting.AddPointLight(room, new Vector3(0f, 4.5f, halfDepth - 2.5f), new Color(0.72f, 0.82f, 1f), 14f, 1.1f);
                // [125차 후속35-R P1-4] 로비 반사 프로브 — Realtime 1회 베이크(ViaScripting). 오크 광택(0.25)에
                //   천장·벽 반사 입힘. 상시 비용 없음(진입 1회).
                var probeGo = new GameObject("ArenaLobby_ReflectionProbe");
                probeGo.transform.SetParent(room.transform, false);
                probeGo.transform.localPosition = new Vector3(0f, ROOM_HEIGHT * 0.5f, 0f);
                var probe = probeGo.AddComponent<ReflectionProbe>();
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;   // 빌드 직후 1회 수동 베이크
                probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.intensity = 1.0f;
                probe.boxProjection = true;
                probe.center = Vector3.zero;
                probe.size = new Vector3(ROOM_WIDTH, ROOM_HEIGHT, ROOM_DEPTH);
                probe.RenderProbe();
                Debug.Log("[ArenaInteriorBuilder] [125차 후속35-R P1] 로비 라이팅 고도화 — 플리커 8등(중앙+보조3+횃불4)+쿨톤 림+방향광 Soft 섀도우+반사 프로브 1회 베이크");
                Debug.Log("[ArenaInteriorBuilder] [125차 후속8] 아레나 조명 결정 보강 — 앰비언트 배가(0.38,0.32,0.25) + 중앙광 3.0/26m + 방향광 필 0.75 + 로비 보조광 3등 + 투기장 횃불 4곳");
                return room;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ArenaInteriorBuilder] 여관 생성 실패: {e.Message}");
                return new GameObject("Room_Fallback");
            }
        }

        /// <summary>
        /// Stage (북서) — 반원형 알코브 돌출 + 무대 판자 + 악기 장식 (프리미티브).
        /// 위치: 북서 모서리. x=-15, z=+20 기준.
        /// </summary>
        private static void BuildStage(GameObject room, Material wallMat)
        {
            try
            {
                // 무대 바닥 (원통형 알코브)
                var stageFloor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stageFloor.name = "Arena_Stage_Floor";
                stageFloor.transform.SetParent(room.transform);
                stageFloor.transform.localPosition = new Vector3(-15f, 0.25f, 20f);
                stageFloor.transform.localScale = new Vector3(6f, 0.5f, 6f);
                stageFloor.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Stage_Floor_Mat", new Color(0.35f, 0.25f, 0.15f));

                // 배경 격벽 (배우들의 대기 공간을 가리기 위한 파티션)
                var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backdrop.name = "Arena_Stage_Backdrop";
                backdrop.transform.SetParent(room.transform);
                backdrop.transform.localPosition = new Vector3(-15f, 2f, 24f);
                backdrop.transform.localScale = new Vector3(8f, 4f, 0.3f);
                backdrop.GetComponent<MeshRenderer>().material = wallMat;

                // 악기 장식 (프리미티브)
                var instrument = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                instrument.name = "Arena_Stage_Instrument";
                instrument.transform.SetParent(room.transform);
                instrument.transform.localPosition = new Vector3(-17f, 1f, 20f);
                instrument.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
                instrument.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Stage_Instrument_Mat", new Color(0.5f, 0.35f, 0.15f));

                // 접지 블롭 (Stage) — 1번
                IndoorQualityLayers.AddBlobAt(room, new Vector3(-15f, 0f, 20f), 5.0f, 2.5f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] Stage 배치 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Private Booths (북벽 따라) — 2인용 부스 탁자+벤치+격벽 4~6세트.
        /// 위치: 북벽 z=+25 선을 따라 x=-9부터 +9까지.
        /// [159차 P2] GLB 교체 — 탁자=Table_6(실측 정사각 1.21×1.27), 벤치=Furnit_*_10/15(실측 1.8×0.36 벤치형),
        /// 격벽=Furnit_*_12 캐비닛형(실측 0.90×0.36×0.94, 90° 회전으로 박판 방향). 변형은 해시 순환, 실패 시 프리미티브 폴백.
        /// </summary>
        private static void BuildPrivateBooths(GameObject room, Material wallMat, string nation)
        {
            try
            {
                // 4세트의 부스 탁자+벤치+격벽.
                for (int i = 0; i < 4; i++)
                {
                    float boothX = -9f + (i * 6f); // 부스 위치 간격: 6m
                    float boothZ = 25f;

                    // 탁자 — [159차 P2] Table_6 계열 변형 A~D/F 해시 순환
                    bool tableGlb = PlaceGlbFurniture(room, LetterKeys("Table_6_"),
                        $"arena_booth_table_{i}_{nation}", $"Arena_Booth_{i}_Table",
                        new Vector3(boothX, 0f, boothZ), 0f, GlbTableHeight);
                    if (!tableGlb)
                    {
                        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        table.name = $"Arena_Booth_{i}_Table";
                        table.transform.SetParent(room.transform);
                        table.transform.localPosition = new Vector3(boothX, 1f, boothZ);
                        table.transform.localScale = new Vector3(3f, 0.2f, 1.5f);
                        table.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Booth_Table_Mat", new Color(0.45f, 0.3f, 0.18f));
                    }

                    // 벤치 (좌/우) — [159차 P2] Furnit 벤치형 변형 순환(좌=Furnit_*_10, 우=Furnit_*_15 — 패밀리 A~H가 변형 축)
                    for (int j = 0; j < 2; j++)
                    {
                        bool benchGlb = PlaceGlbFurniture(room, FurnitKeys(j == 0 ? 10 : 15),
                            $"arena_booth_bench_{i}_{j}_{nation}", $"Arena_Booth_{i}_Bench_{(j == 0 ? "Left" : "Right")}",
                            new Vector3(boothX, 0f, boothZ + (j == 0 ? -1.0f : 1.0f)), 0f, GlbBenchHeight);
                        if (!benchGlb)
                        {
                            var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            bench.name = $"Arena_Booth_{i}_Bench_{(j == 0 ? "Left" : "Right")}";
                            bench.transform.SetParent(room.transform);
                            bench.transform.localPosition = new Vector3(boothX, 0.5f, boothZ + (j == 0 ? -1.2f : 1.2f));
                            bench.transform.localScale = new Vector3(3f, 0.5f, 0.5f);
                            bench.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Booth_Bench_Mat", new Color(0.4f, 0.28f, 0.16f));
                        }
                    }

                    // 격벽 (파티션) — [159차 P2] Furnit_*_12 캐비닛형 변형 순환, 90° 회전(슬림면이 x축 두께)
                    bool partitionGlb = PlaceGlbFurniture(room, FurnitKeys(12),
                        $"arena_booth_partition_{i}_{nation}", $"Arena_Booth_{i}_Partition",
                        new Vector3(boothX + 3f, 0f, boothZ), 90f, GlbPartitionHeight);
                    if (!partitionGlb)
                    {
                        var partition = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        partition.name = $"Arena_Booth_{i}_Partition";
                        partition.transform.SetParent(room.transform);
                        partition.transform.localPosition = new Vector3(boothX + 3f, 1.5f, boothZ);
                        partition.transform.localScale = new Vector3(0.2f, 3f, 3f);
                        partition.GetComponent<MeshRenderer>().material = wallMat;
                    }

                    // 접지 블롭 (Booths) — 2,3,4,5번 [개수 계약 무손상]
                    IndoorQualityLayers.AddBlobAt(room, new Vector3(boothX, 0f, boothZ), 3.0f, 1.5f);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] Private Booths 배치 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Storage (북동) — enclosed 방 + 오크통 적립 3~5개.
        /// 위치: 북동 모서리 x=+18, z=+25.
        /// [159차 P2] 오크통 GLB 교체 — Stool_1 계열(실측 원형 통형: 32/36 각도빈, Ø0.44×h0.55 → h1.0 정규 시 Ø0.8).
        /// 팩 내 원형 '통' 형태는 Stool_1이 유일(Furnit은 전원 각형 — 실측). 변형 A~D 해시 순환, 실패 시 프리미티브 폴백.
        /// 파티션 벽은 건축 요소(프리미티브 계약)로 유지.
        /// </summary>
        private static void BuildStorage(GameObject room, Material wallMat, string nation)
        {
            try
            {
                // enclosed 방 (파티션 벽)
                var partitionWall1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                partitionWall1.name = "Arena_Storage_Wall_1";
                partitionWall1.transform.SetParent(room.transform);
                partitionWall1.transform.localPosition = new Vector3(18f, 2f, 20f);
                partitionWall1.transform.localScale = new Vector3(10f, 4f, 0.3f);
                partitionWall1.GetComponent<MeshRenderer>().material = wallMat;

                var partitionWall2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                partitionWall2.name = "Arena_Storage_Wall_2";
                partitionWall2.transform.SetParent(room.transform);
                partitionWall2.transform.localPosition = new Vector3(13f, 2f, 25f);
                partitionWall2.transform.localScale = new Vector3(0.3f, 4f, 10f);
                partitionWall2.GetComponent<MeshRenderer>().material = wallMat;

                // 오크통 5개 적립 — [159차 P2] Stool_1 변형 A~D 해시 순환
                for (int i = 0; i < 5; i++)
                {
                    Vector3 spot = new Vector3(18f + ((i % 3) * 1.2f), 0f, 25f + ((i / 3) * 1.2f));
                    bool barrelGlb = PlaceGlbFurniture(room, LetterKeys("Stool_1_"),
                        $"arena_storage_barrel_{i}_{nation}", $"Arena_Storage_Barrel_{i}", spot, 0f, GlbBarrelHeight);
                    if (!barrelGlb)
                    {
                        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        barrel.name = $"Arena_Storage_Barrel_{i}";
                        barrel.transform.SetParent(room.transform);
                        barrel.transform.localPosition = spot + new Vector3(0f, 0.5f, 0f);
                        barrel.transform.localScale = new Vector3(1f, 0.5f, 1f);
                        barrel.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Storage_Barrel_Mat", new Color(0.55f, 0.35f, 0.2f));
                    }
                }

                // 접지 블롭 (Storage) — 6번 [개수 계약 무손상]
                IndoorQualityLayers.AddBlobAt(room, new Vector3(18f, 0f, 25f), 4.0f, 2.0f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] Storage 배치 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Stairs (북동 동벽) — 직선 나무 계단 (발판 6~8단, 비충돌 장식).
        /// 위치: 북동 동벽 x=+20, z=+15.
        /// </summary>
        private static void BuildStairs(GameObject room, Material wallMat)
        {
            try
            {
                // 발판 7단 (비충돌 장식 — 콜라이더 미부착).
                for (int i = 0; i < 7; i++)
                {
                    var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    step.name = $"Arena_Stairs_Step_{i}";
                    step.transform.SetParent(room.transform);
                    step.transform.localPosition = new Vector3(20f, 0.25f + (i * 0.5f), 15f - (i * 0.8f));
                    step.transform.localScale = new Vector3(1.5f, 0.5f, 0.8f);
                    step.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Stairs_Step_Mat", new Color(0.4f, 0.28f, 0.16f));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] Stairs 배치 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Kitchen (남서 사분면) — 조리대/개수대/화로/오크통.
        /// 위치: 남서 모서리 x=-18, z=-20.
        /// [159차 P2] GLB 교체 — 조리대=StationTable_1(실측 1.25×0.88×h1.00), 개수대=Sink(0.90×0.90×h1.30),
        /// 화로=Owen 오븐형(0.90×0.94×h1.04), 오크통=Stool_1 통형. 각 계열 변형 해시 순환, 실패 시 프리미티브 폴백.
        /// </summary>
        private static void BuildKitchen(GameObject room, Material wallMat, string nation)
        {
            try
            {
                // 조리대 (Counter) — [159차 P2] StationTable_1 변형(A/B/C/F) 해시 순환
                bool counterGlb = PlaceGlbFurniture(room, LetterKeys("StationTable_1_"),
                    $"arena_kitchen_counter_{nation}", "Arena_Kitchen_Counter",
                    new Vector3(-18f, 0f, -20f), 0f, GlbCounterHeight);
                if (!counterGlb)
                {
                    var counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    counter.name = "Arena_Kitchen_Counter";
                    counter.transform.SetParent(room.transform);
                    counter.transform.localPosition = new Vector3(-18f, 1f, -20f);
                    counter.transform.localScale = new Vector3(5f, 2f, 1.5f);
                    counter.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Kitchen_Counter_Mat", new Color(0.5f, 0.35f, 0.2f));
                }

                // 개수대 (Sink) — [159차 P2] Sink 변형(A~G, 팩 내 D 결측 자동 스킵) 해시 순환
                bool sinkGlb = PlaceGlbFurniture(room, LetterKeys("Sink_"),
                    $"arena_kitchen_sink_{nation}", "Arena_Kitchen_Sink",
                    new Vector3(-15f, 0f, -20f), 0f, GlbCounterHeight);
                if (!sinkGlb)
                {
                    var sink = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sink.name = "Arena_Kitchen_Sink";
                    sink.transform.SetParent(room.transform);
                    sink.transform.localPosition = new Vector3(-15f, 1.2f, -20f);
                    sink.transform.localScale = new Vector3(1f, 0.5f, 1f);
                    sink.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Kitchen_Sink_Mat", new Color(0.7f, 0.7f, 0.7f));
                }

                // 화로 (Furnace) — [159차 P2] Owen 오븐형 변형(A~G, 팩 내 D 결측 자동 스킵) 해시 순환
                bool furnaceGlb = PlaceGlbFurniture(room, LetterKeys("Owen_"),
                    $"arena_kitchen_furnace_{nation}", "Arena_Kitchen_Furnace",
                    new Vector3(-20f, 0f, -20f), 0f, GlbCounterHeight);
                if (!furnaceGlb)
                {
                    var furnace = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    furnace.name = "Arena_Kitchen_Furnace";
                    furnace.transform.SetParent(room.transform);
                    furnace.transform.localPosition = new Vector3(-20f, 0.5f, -20f);
                    furnace.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);
                    furnace.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Kitchen_Furnace_Mat", new Color(0.3f, 0.3f, 0.3f));
                }

                // 오크통 2개 — [159차 P2] Stool_1 변형 A~D 해시 순환
                for (int i = 0; i < 2; i++)
                {
                    Vector3 spot = new Vector3(-21f + (i * 1.5f), 0f, -17f);
                    bool barrelGlb = PlaceGlbFurniture(room, LetterKeys("Stool_1_"),
                        $"arena_kitchen_barrel_{i}_{nation}", $"Arena_Kitchen_Barrel_{i}", spot, 0f, GlbBarrelHeight);
                    if (!barrelGlb)
                    {
                        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        barrel.name = $"Arena_Kitchen_Barrel_{i}";
                        barrel.transform.SetParent(room.transform);
                        barrel.transform.localPosition = spot + new Vector3(0f, 0.5f, 0f);
                        barrel.transform.localScale = new Vector3(1f, 0.5f, 1f);
                        barrel.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Kitchen_Barrel_Mat", new Color(0.55f, 0.35f, 0.2f));
                    }
                }

                // 접지 블롭 (Kitchen) — 7번 [개수 계약 무손상]
                IndoorQualityLayers.AddBlobAt(room, new Vector3(-18f, 0f, -20f), 4.0f, 2.0f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] Kitchen 배치 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Fireplace (방 중앙) — 원형 돌 굴뚝 (최종 r=2.7m) + 주황 러그 평면 데칼.
        /// </summary>
        private static void BuildFireplace(GameObject room, Material wallMat)
        {
            try
            {
                // 원형 돌 굴뚝 (r=2.7m)
                var chimney = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chimney.name = "Arena_Fireplace_Chimney";
                chimney.transform.SetParent(room.transform);
                chimney.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                chimney.transform.localScale = new Vector3(5.4f, 1.5f, 5.4f);
                chimney.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Fireplace_Stone_Mat", new Color(0.4f, 0.4f, 0.4f));

                // [125차 후속34 O1b] 주황 러그 삭제 — 기존 러그는 Plane 원본 10×10m×scale(6,1,6)=60×60m라
                //   방(45.6×61.3m) 전체를 덮어 주점 바닥(오크 판자)을 가렸음(후속34 진단). 사용자 결정: 러그 폐지,
                //   바닥 전면 오크 판자 노출. 러그 텍스처링/재도입은 GLB 가구 사이클에서 재검토.
                // [125차 후속35-R P1-6] 화로 불꽃 인상 — 이메시브 잔불 링+웜 점광원(플리커) — 텍스처 없는 발광.
                //   지금 화로는 굴뚝(돌)만 존재하고 불이 없어 방 중앙 심볼이 죽어 있음(실측: BuildFireplace 광원 0).
                var ember = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ember.name = "Arena_Fireplace_EmberRing";
                ember.transform.SetParent(room.transform);
                ember.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                ember.transform.localScale = new Vector3(6.0f, 0.04f, 6.0f);   // 굴뚝(지름 5.4m) 기단을 감싸는 잔불 링
                Object.DestroyImmediate(ember.GetComponent<Collider>());   // 렌더러 전용 — 보행 무간섭
                var emberRend = ember.GetComponent<MeshRenderer>();
                if (emberRend != null)
                {
                    var emberMat = ResolveShaderMaterial("Arena_Fireplace_Ember_Mat", new Color(0.35f, 0.12f, 0.05f, 1f));
                    if (emberMat != null)
                    {
                        emberMat.EnableKeyword("_EMISSION");
                        emberMat.SetColor("_EmissionColor", new Color(1f, 0.42f, 0.12f) * 1.6f);
                        emberRend.material = emberMat;
                    }
                }
                // 화로 웜광 — 굴뚝 내부 기점(섀도우 없어 투과 — 잔불 주변 조명). 플리커 연동.
                IndoorLighting.AddPointLight(room, new Vector3(0f, 0.8f, 0f), new Color(1f, 0.55f, 0.25f), 9f, 2.2f, flicker: true);
                // 접지 블롭 (Fireplace) — 8번
                IndoorQualityLayers.AddBlobAt(room, new Vector3(0f, 0f, 0f), 5.0f, 2.5f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] Fireplace 배치 실패: {e.Message}");
            }
        }

        /// <summary>
        /// Table Area (남동 홀) — 원형 탁자 2 + 사각 탁자 3 + 의자.
        /// [159차 P2] GLB 교체 — 원형탁자=Table_2(실측 원형 rCV 0.035, Ø1.24/1.37), 사각탁자=Table_6(실측 ~정사각
        /// 1.21×1.27), 의자=Chair_1(실측 h1.04). 계획의 Table_1 후보는 실측 결과 원형이라 사각 배치에서 제외.
        /// 각 계열 변형 A~G 해시 순환, 실패 시 프리미티브 폴백.
        /// </summary>
        private static void BuildTableArea(GameObject room, Material wallMat, string nation)
        {
            try
            {
                // 원형 탁자 2개 — [159차 P2] Table_2 변형 A~G 해시 순환
                for (int i = 0; i < 2; i++)
                {
                    Vector3 spot = new Vector3(10f + (i * 5f), 0f, -10f - (i * 5f));
                    bool tableGlb = PlaceGlbFurniture(room, LetterKeys("Table_2_"),
                        $"arena_round_table_{i}_{nation}", $"Arena_Table_Circular_{i}", spot, 0f, GlbTableHeight);
                    if (!tableGlb)
                    {
                        var table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        table.name = $"Arena_Table_Circular_{i}";
                        table.transform.SetParent(room.transform);
                        table.transform.localPosition = new Vector3(10f + (i * 5f), 1f, -10f - (i * 5f));
                        table.transform.localScale = new Vector3(2f, 0.2f, 2f);
                        table.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Table_Wood_Mat", new Color(0.45f, 0.3f, 0.18f));
                    }
                    // 접지 블롭 (Circular Tables) — 9,10번 [개수 계약 무손상]
                    IndoorQualityLayers.AddBlobAt(room, new Vector3(10f + (i * 5f), 0f, -10f - (i * 5f)), 2.5f, 1.2f);
                }

                // 사각 탁자 3개 — [159차 P2] Table_6 변형(A~D/F) 해시 순환
                for (int i = 0; i < 3; i++)
                {
                    Vector3 spot = new Vector3(15f, 0f, -15f + (i * 6f));
                    bool tableGlb = PlaceGlbFurniture(room, LetterKeys("Table_6_"),
                        $"arena_square_table_{i}_{nation}", $"Arena_Table_Square_{i}", spot, 0f, GlbTableHeight);
                    if (!tableGlb)
                    {
                        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        table.name = $"Arena_Table_Square_{i}";
                        table.transform.SetParent(room.transform);
                        table.transform.localPosition = new Vector3(15f, 1f, -15f + (i * 6f));
                        table.transform.localScale = new Vector3(2.5f, 0.2f, 2.5f);
                        table.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Table_Wood_Mat", new Color(0.45f, 0.3f, 0.18f));
                    }

                    // 의자 (각 탁자 옆) — [159차 P2] Chair_1 변형 A~G 해시 순환, 탁자를 향해 회전
                    //   (RestaurantInteriorBuilder 좌/우 90° 회전 선례 — 동측 의자 → -90°로 탁자 방향)
                    Vector3 chairSpot = new Vector3(15f + 1.1f, 0f, -15f + (i * 6f));
                    bool chairGlb = PlaceGlbFurniture(room, LetterKeys("Chair_1_"),
                        $"arena_square_chair_{i}_{nation}", $"Arena_Table_Square_{i}_Chair", chairSpot, -90f, GlbChairHeight);
                    if (!chairGlb)
                    {
                        var chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        chair.name = $"Arena_Table_Square_{i}_Chair";
                        chair.transform.SetParent(room.transform);
                        chair.transform.localPosition = new Vector3(15f + 2f, 0.5f, -15f + (i * 6f));
                        chair.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
                        chair.GetComponent<MeshRenderer>().material = ResolveShaderMaterial("Arena_Table_Chair_Mat", new Color(0.4f, 0.28f, 0.16f));
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] Table Area 배치 실패: {e.Message}");
            }
        }

        /// <summary>유틸리티: 주어진 이름과 색상으로 URP/Standard Shader 머티리얼 생성.</summary>
        private static Material ResolveShaderMaterial(string name, Color color)
        {
            Shader shader = ResolveShader();
            return new Material(shader) { name = name, color = color };
        }

        /// <summary>바 카운터 위치로 재배치된 베팅 데스크 (MeshCollider 유지 계약).
        /// [159차 P2] GLB 교체 — StationTable_3 계열 장 카운터(GLB 실측 2.50×0.88×h1.00, 팩 최장 카운터).
        /// 변형(A/B/C/F) 해시 순환, 실패 시 기존 프리미티브 폴백. 접지 블롭 11번은 무손상.</summary>
        private static void PlaceBettingDesk(GameObject room, Material wallMat, string nation)
        {
            try
            {
                // [159차 P2] StationTable_3 변형 해시 순환 — 충돌 유지 계약으로 BoxCollider 부착 요청
                var deskGlb = PlaceGlbFurniture(room, LetterKeys("StationTable_3_"),
                    $"arena_betting_desk_{nation}", "ArenaBettingDesk",
                    new Vector3(0f, 0f, -15f), 0f, GlbCounterHeight, addBoxCollider: true);
                if (deskGlb != null)
                {
                    // [159차 P2] I2 계약 — ArenaBettingDesk 루트 MeshRenderer 보장.
                    //   GLB 루트는 렌더러가 없어(MeshFilter도 없음) 렌더 기여 0 — 테스트 계약만 충족하는 캐리어.
                    if (deskGlb.GetComponent<MeshRenderer>() == null)
                        deskGlb.AddComponent<MeshRenderer>();
                }
                else
                {
                    var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    desk.name = "ArenaBettingDesk";
                    desk.transform.SetParent(room.transform);
                    desk.transform.localPosition = new Vector3(0f, 1.0f, -15f); // 바 위치 (남중앙)
                    desk.transform.localScale = new Vector3(8f, 2f, 1.5f);
                    var rend = desk.GetComponent<MeshRenderer>();
                    if (rend != null)
                    {
                        rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                        { name = "ArenaBettingDeskMat", color = new Color(0.35f, 0.22f, 0.10f) };
                    }
                    desk.AddComponent<MeshCollider>();
                }
                // 접지 블롭 (Bar) — 11번 [개수 계약 무손상]
                IndoorQualityLayers.AddBlobAt(room, new Vector3(0f, 0f, -15f), 7.0f, 2.6f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] 베팅 데스크 배치 실패: {e.Message}");
            }
        }

        /// <summary>아레나 관리자 NPC 배치 — 근접 E키로 ArenaMenuUI 오픈 (ArenaNPCPlaceholder).</summary>
        private static void SpawnArenaNpc(GameObject room, string nation)
        {
            try
            {
                var npc = new GameObject("ArenaNpc");
                npc.transform.SetParent(room.transform);
                npc.transform.localPosition = new Vector3(0f, 0f, -17f); // 바 뒤
                npc.AddComponent<ArenaNPCPlaceholder>();
                // [125차 후속7] 관리자 NPC 가시성 확보 — 기존은 ArenaNPCPlaceholder 컴포넌트만 부착한
                //   시각 오브젝트 전무(투명) 상태 → '관리인이 안 보인다'는 사용자 신고의 직접 원인.
                //   베팅 관리인(SpawnBetManager)과 동일 규격: 병사 GLB 우선(1.8m 보정·접지), 캡슐 폴백.
                npc.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // 남쪽 입구(-z)를 바라봄 — 플레이어 진입 방향
                bool visual = false;
                if (RuntimeModelLoader.TryGetModel("soldier", out var soldierPrefab))
                {
                    var inst = Object.Instantiate(soldierPrefab, npc.transform, false);
                    inst.name = "ArenaNpc_Model";
                    NormalizeNpcVisual(inst, 1.8f);
                    visual = true;
                    Debug.Log("[ArenaInteriorBuilder] 아레나 관리자 — 병사 GLB 시각 적용 (1.8m 보정·접지)");
                }
                if (!visual)
                {
                    // 캡슐 폴백 — 1.8m 캐릭터 키 계약(병사·플레이어·주민 전 씬 1.8m)
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "ArenaNpc_Body";
                    body.transform.SetParent(npc.transform, false);
                    body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    body.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                    var rend = body.GetComponent<Renderer>();
                    if (rend != null)
                        rend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                        { name = "ArenaNpcMat", color = new Color(0.55f, 0.42f, 0.28f) };
                    Debug.Log("[ArenaInteriorBuilder] 아레나 관리자 — 캡슐 폴백 시각");
                }
                Debug.Log($"[ArenaInteriorBuilder] 아레나 관리자 NPC 배치 완료 ({nation})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] 아레나 NPC 배치 실패: {e.Message}");
            }
        }

        /// <summary>[121차] 베팅 관리인 NPC — 좌클릭/F키 상호판넬(대화하기/베팅하기) 대상. 병사 GLB 우선, 캡슐 폴백.</summary>
        private static void SpawnBetManager(GameObject room, string nationStyle)
        {
            try
            {
                var npc = new GameObject("ArenaBetManagerNpc");
                npc.transform.SetParent(room.transform);
                npc.transform.localPosition = new Vector3(3.5f, 0f, -12.5f);   // 바 우측 대기 자리
                npc.AddComponent<ArenaBetManager>().NationStyle = nationStyle ?? "Western";

                bool visual = false;
                if (RuntimeModelLoader.TryGetModel("soldier", out var soldierPrefab))
                {
                    var inst = Object.Instantiate(soldierPrefab, npc.transform, false);
                    inst.name = "BetManager_Model";
                    // [125차 후속7] GLB 스케일/접지 보정 — 기존은 무보정 Instantiate라 GLB 원본 크기 그대로
                    //   바닥에 묻히거나(과소) 공중에 뜨거나(과대) → '관리인이 안 보인다'는 신고의 직접 원인.
                    //   ArenaBettingSystem.SpawnFighterVisual 선례: 렌더러 바운즈로 1.8m 캐릭터 키 계약 스케일 + 접지.
                    NormalizeNpcVisual(inst, 1.8f);
                    // [125차 후속7] 클릭 판정용 히트 캡슐 — 런타임 GLTF 임포트는 콜라이더가 없어
                    //   HoverTargetClassifier 좌클릭 레이캐스트가 모델에 맞지 않는다.
                    //   캡슐 폴백(프리미티브 자체 콜라이더)과 동일 규격의 1.8m 히트박스를 루트에 부여.
                    var hit = npc.AddComponent<CapsuleCollider>();
                    hit.height = 1.8f; hit.radius = 0.35f; hit.center = new Vector3(0f, 0.9f, 0f);
                    // 남쪽 입구(플레이어 진입 방향, -z)를 바라보게 — GLB 기본 정면(+z) 180° 회전.
                    npc.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    visual = true;
                    Debug.Log("[ArenaInteriorBuilder] 베팅 관리인 — 병사 GLB 시각 적용 (1.8m 보정·접지·남향·히트 캡슐)");
                }
                if (!visual)
                {
                    // 캡슐 폴백 — 1.8m 캐릭터 키 계약(병사·플레이어·주민 전 씬 1.8m)
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "BetManager_Body";
                    body.transform.SetParent(npc.transform, false);
                    body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    body.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                    var rend = body.GetComponent<Renderer>();
                    if (rend != null)
                        rend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                        { name = "ArenaBetManagerMat", color = new Color(0.42f, 0.36f, 0.55f) };
                    Debug.Log("[ArenaInteriorBuilder] 베팅 관리인 — 캡슐 폴백 시각");
                }

                Debug.Log($"[ArenaInteriorBuilder] 베팅 관리인 NPC 배치 완료 ({nationStyle}) — 좌클릭/F키 → 대화하기/베팅하기");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] 베팅 관리인 배치 실패: {e.Message}");
            }
        }

        /// <summary>
        /// [125차 후속7] NPC GLB 시각 보정 — 렌더러 바운즈 합산으로 캐릭터 키 계약(1.8m) 스케일 + 바닥 접지.
        /// ArenaBettingSystem.NormalizeHeight 선례(스케일 적용 후 재측정 바운즈 최하단을 y=0으로).
        /// 부모 체인(npc→room)이 Y회전만 있고 스케일이 1이라 Y좌표는 로컬≈월드 — 오프셋 직접 반영 가능.
        /// </summary>
        private static void NormalizeNpcVisual(GameObject visualRoot, float targetHeight)
        {
            if (visualRoot == null) return;
            var renderers = visualRoot.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning("[ArenaInteriorBuilder] [125차 후속7] NPC GLB에 렌더러 없음 — 스케일/접지 보정 스킵");
                return;
            }
            Bounds before = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) before.Encapsulate(renderers[i].bounds);
            if (before.size.y <= 0.01f)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] [125차 후속7] NPC GLB 높이 0({before.size.y:F3}m) — 보정 스킵");
                return;
            }
            float k = targetHeight / before.size.y;
            visualRoot.transform.localScale *= k;
            // 스케일 적용 후 재측정 → 최하단을 로컬 y=0에 맞춰 접지 (ArenaBettingSystem 선례)
            Bounds after = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) after.Encapsulate(renderers[i].bounds);
            visualRoot.transform.localPosition += Vector3.down * after.min.y;
            Debug.Log($"[ArenaInteriorBuilder] [125차 후속7] NPC GLB 보정 — 키 {before.size.y:F2}m → {targetHeight}m(×{k:F3}), 접지 오프셋 {after.min.y:F3}m");
        }

        /// <summary>사용자 GLB 규약 로드 — 로비 중앙에 배치 (바닥 보정).</summary>
        private static void TryLoadArenaGlb(GameObject room, string nation, string nationStyle)
        {
            string glbKey = ArenaGlbKey(nationStyle);
            if (RuntimeModelLoader.TryGetModel(glbKey, out var modelPrefab))
            {
                GameObject inst = Object.Instantiate(modelPrefab, room.transform, false);
                inst.name = "ArenaModel";
                inst.transform.localPosition = new Vector3(0f, 0f, 5f);
                NormalizeArenaModel(inst);
                Debug.Log($"[ArenaInteriorBuilder] 아레나 GLB 로드: '{glbKey}' → 로비 중앙 배치 완료");
            }
            else
            {
                Debug.Log($"[ArenaInteriorBuilder] 아레나 GLB '{glbKey}' 없음 — 셸만 사용 (나중에 arena_{nation}.glb 추가 시 자동 로드)");
            }
            // 접지 블롭 (GLB) — 12번 [개수 계약 무손상]
            //   GLB 부재 시에도 로비 중앙 집결지 표시 블롭은 유지 (159차 수리 — 블롭 12→11 회귀 방지)
            IndoorQualityLayers.AddBlobAt(room, new Vector3(0f, 0f, 5f), 2.5f, 2.5f);
        }

        private static Material ResolveFloorMaterial(string n, string nationStyle)
        {
            Texture2D tex;
            switch (n)
            {
                case "eastern":  tex = IndoorTextureGenerator.GenerateCastleFloorEastern(); break;
                case "western":  tex = IndoorTextureGenerator.GenerateCastleFloorWestern(); break;
                case "southern": tex = IndoorTextureGenerator.GenerateCastleFloorSouthern(); break;
                case "northern": tex = IndoorTextureGenerator.GenerateCastleFloorNorthern(); break;
                default:         tex = IndoorTextureGenerator.GenerateCastleFloorEmpire(); break;
            }
            Shader shader = ResolveShader();
            return new Material(shader) { name = $"Arena_FloorMat_{nationStyle}", mainTexture = tex };
        }

        private static Material ResolveWallMaterial(string n, string nationStyle)
        {
            Texture2D tex;
            switch (n)
            {
                case "eastern":  tex = IndoorTextureGenerator.GenerateCastleWallEastern(); break;
                case "western":  tex = IndoorTextureGenerator.GenerateCastleWallWestern(); break;
                case "southern": tex = IndoorTextureGenerator.GenerateCastleWallSouthern(); break;
                case "northern": tex = IndoorTextureGenerator.GenerateCastleWallNorthern(); break;
                default:         tex = IndoorTextureGenerator.GenerateCastleWallEmpire(); break;
            }
            return new Material(ResolveShader()) { name = $"Arena_WallMat_{nationStyle}", mainTexture = tex };
        }

        private static Material ResolveCeilingMaterial()
        {
            Texture2D tex = IndoorTextureGenerator.GeneratePlasterWall(256, 256, new Color(0.58f, 0.53f, 0.48f));
            return new Material(ResolveShader()) { name = "Arena_CeilingMat", mainTexture = tex };
        }

        private static Shader ResolveShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null)
            {
                Debug.LogError("[ArenaInteriorBuilder] No supported shader found.");
            }
            return s;
        }

        /// <summary>아레나 유형 (설계 로그용).</summary>
        public static string GetArenaKind(string nationStyle)
        {
            switch ((nationStyle ?? "").ToLower())
            {
                case "eastern":  return "사격/무도 베팅장(East)";
                case "western":  return "기사 결투 베팅장(West)";
                case "southern": return "격투 투기 베팅장(South)";
                case "northern": return "맹수 투기 베팅장(North)";
                default:         return "아우레우스 원형 베팅장(Empire)";
            }
        }

        /// <summary>GLB를 바닥(y=0) 위로 보정 (바운즈 최하단 y를 0에 맞춤).</summary>
        private static void NormalizeArenaModel(GameObject inst)
        {
            var renderer = inst.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            var bounds = renderer.bounds;
            if (bounds == null) return;
            float minY = bounds.min.y;
            if (minY < 0f)
                inst.transform.localPosition = new Vector3(0f, -minY, 0f);
        }
    }
}
