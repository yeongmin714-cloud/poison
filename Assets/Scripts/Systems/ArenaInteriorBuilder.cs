// 2026-10-09: 4방위국 아레나 실내 빌더 (베팅 로비) — 메인씬 아레나 문(E키) 진입 시 등장.
// 베팅 로비: 중앙 베팅 데스크 + ArenaNPCPlaceholder(E키로 ArenaMenuUI) + 벽면 투기장 출입문(ArenaPit).
// 실제 투기장은 ArenaPitInteriorBuilder.BuildArenaPitInterior 가 담당 (실내→실내 전환).
// GLB 규약: Assets/Resources/Models/UserProvided/arena_<nation>.glb 를 넣으면 자동 로드되어 로비 중앙에 배치.
#pragma warning disable 618
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 4방위국(동East/서West/남South/북North) 아레나 베팅 로비 실내.
    /// IndoorBuilder.CreateRoom 셸 + 선택적 사용자 GLB(arena_&lt;nation&gt;.glb) + 베팅 NPC/투기장 문.
    /// </summary>
    public static class ArenaInteriorBuilder
    {
        // ── 레이아웃 상수 ─────────────────────────────────────────────
        public const float ROOM_WIDTH = 60f;
        public const float ROOM_HEIGHT = 8f;
        public const float ROOM_DEPTH = 60f;
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

        /// <summary>국가별 베팅 로비 셸 생성 — 베팅 NPC + 투기장 출입문 포함.</summary>
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

                // ── 진입로: 남벽(Wall_Back)을 중앙 8m 개구부로 교체 ──────────────────
                Transform backWall = room.transform.Find("Wall_Back");
                if (backWall != null) Object.DestroyImmediate(backWall.gameObject); // EditMode 즉시 파괴 (성 빌더 선례)
                float halfDepth = ROOM_DEPTH * 0.5f; // 30
                float halfWidth = ROOM_WIDTH * 0.5f; // 30
                IndoorBuilder.CreateSolidInteriorWall(room, "ArenaSouthEntry_Left",
                    new Vector2(-halfWidth, -halfDepth), new Vector2(-4f, -halfDepth),
                    ROOM_HEIGHT, 0.5f, 0f, 0f, 0f, wallMat);
                IndoorBuilder.CreateSolidInteriorWall(room, "ArenaSouthEntry_Right",
                    new Vector2(4f, -halfDepth), new Vector2(halfWidth, -halfDepth),
                    ROOM_HEIGHT, 0.5f, 0f, 0f, 0f, wallMat);

                // ── 베팅 데스크 (중앙 남쪽, 투기장 문 앞) ──────────────────────────
                PlaceBettingDesk(room, wallMat);

                // ── 아레나 관리자 NPC — E키로 ArenaMenuUI(직접 싸우기/병사 출전/베팅/전투 로그) ──
                SpawnArenaNpc(room, n);

                // ── 투기장 출입문 (북벽 — 실제 투기장 실내로 전환) ─────────────────────
                float pitDoorZ = halfDepth;
                IndoorTransitionSetup.CreateArenaPitTrigger(
                    new Vector3(0f, 1.5f, pitDoorZ), nationStyle, room.transform);

                // ── 사용자 GLB 규약 로드 ────────────────────────────────────────
                TryLoadArenaGlb(room, n, nationStyle);

                // ── [2026-10-10 I1~I3 — 계획 v4] 아레나 로비 품질 계층 — 성 내부 승인 언어 이식 ──
                //   벽 톱면 돌 캡(탑다운에서 벽이 "돌 선"으로 읽힘) + 마모 스탬프(색 변화 없음).
                //   전부 렌더러 전용(무콜라이더)·그림자 미방출 — 성 내부 계약과 동일.
                int caps = IndoorQualityLayers.AddQuadWallCaps(room, ROOM_WIDTH, ROOM_DEPTH, ROOM_HEIGHT);
                // 남쪽 입구 대체 벽 2개도 캡 연속(Wall_Back 개구부 부분 커버).
                IndoorQualityLayers.AddBoxCap(room, "ArenaSouthEntry_Left_TopCap",
                    new Vector3(-17f, ROOM_HEIGHT + 0.075f, -halfDepth), new Vector3(26f, 0.15f, 0.5f));
                IndoorQualityLayers.AddBoxCap(room, "ArenaSouthEntry_Right_TopCap",
                    new Vector3(17f, ROOM_HEIGHT + 0.075f, -halfDepth), new Vector3(26f, 0.15f, 0.5f));
                // 마모 스탬프 6장 — 입구 동선+중앙 개방부(색 변화 없음).
                IndoorQualityLayers.AddWearStamps(room, "ArenaWear", new[]
                {
                    new Vector2(0f, -22f), new Vector2(0f, -14f), new Vector2(8f, 6f),
                    new Vector2(-8f, 6f), new Vector2(14f, 18f), new Vector2(-14f, 18f),
                }, 2.0f, 0.02f);
                Debug.Log($"[ArenaInteriorBuilder] 품질 계층 적용 — 벽 캡 {caps + 2}개, 마모 6장");

                Debug.Log($"[ArenaInteriorBuilder] 베팅 로비 생성 완료 (nationStyle: {nationStyle}, 유형 {GetArenaKind(n)})");
                return room;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ArenaInteriorBuilder] 아레나 로비 생성 실패: {e.Message}");
                return new GameObject("Room_Fallback");
            }
        }

        /// <summary>베팅 데스크 — 투기장 문 앞 좌측(중심 기준 x=-6, z=+26) 카운터 큐브 블록.</summary>
        private static void PlaceBettingDesk(GameObject room, Material wallMat)
        {
            try
            {
                var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                desk.name = "ArenaBettingDesk";
                desk.transform.SetParent(room.transform);
                desk.transform.localPosition = new Vector3(-6f, 1.0f, 26f);
                desk.transform.localScale = new Vector3(6f, 2f, 1.5f);
                var rend = desk.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                    { name = "ArenaBettingDeskMat", color = new Color(0.35f, 0.22f, 0.10f) };
                }
                desk.AddComponent<MeshCollider>();
                // [2026-10-10 I2] 접지 블롭 — 데스크가 바닥에 앉은 느낌(룸 기준 배치로
                //   부모 비균등 스케일 영향 회피). 렌더러 전용.
                IndoorQualityLayers.AddBlobAt(room, new Vector3(-6f, 0f, 26f), 7.0f, 2.6f);
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
                npc.transform.localPosition = new Vector3(-6f, 0f, 22f); // 데스크 뒤
                npc.AddComponent<ArenaNPCPlaceholder>();
                Debug.Log($"[ArenaInteriorBuilder] 아레나 관리자 NPC 배치 완료 ({nation})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaInteriorBuilder] 아레나 NPC 배치 실패: {e.Message}");
            }
        }

        /// <summary>사용자 GLB 규약 로드 — 로비 중앙에 배치 (바닥 보정).</summary>
        private static void TryLoadArenaGlb(GameObject room, string nation, string nationStyle)
        {
            string glbKey = ArenaGlbKey(nationStyle);
            if (RuntimeModelLoader.TryGetModel(glbKey, out var modelPrefab))
            {
                GameObject inst = Object.Instantiate(modelPrefab, room.transform, false);
                inst.name = "ArenaModel";
                inst.transform.localPosition = new Vector3(0f, 0f, 0f);
                NormalizeArenaModel(inst);
                // [2026-10-10 I2] GLB 접지 블롭(정규화 후 룸 기준 — GLB 루트 스케일 영향 회피).
                IndoorQualityLayers.AddBlobAt(room, new Vector3(0f, 0f, 0f), 2.5f, 2.5f);
                Debug.Log($"[ArenaInteriorBuilder] 아레나 GLB 로드: '{glbKey}' → 로비 중앙 배치 완료");
            }
            else
            {
                Debug.Log($"[ArenaInteriorBuilder] 아레나 GLB '{glbKey}' 없음 — 셸만 사용 (나중에 arena_{nation}.glb 추가 시 자동 로드)");
            }
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