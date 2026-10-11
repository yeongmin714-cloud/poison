// 2026-10-09: 실제 투기가 벌어지는 투기장 실내 (ArenaPit) — 베팅 로비(Arena)의 출입문으로 진입.
// 중앙 함몰 원형 무대 + 4면 난간/관중석 느낌 + Exit(월드 복귀). ArenaSystem 전투 시뮬레이션은
// ArenaMenuUI가 담당하므로, 이 실내는 전투가 벌어지는 무대의 시각 공간을 제공한다.
// GLB 규약: Assets/Resources/Models/UserProvided/arena_pit_<nation>.glb
#pragma warning disable 618
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 실제 투기장 실내 — 중앙 원형 무대(함몰) + 관중석 링 + 출구.
    /// </summary>
    public static class ArenaPitInteriorBuilder
    {
        public const float PIT_WIDTH = 56f;
        public const float PIT_HEIGHT = 8f;
        public const float PIT_DEPTH = 56f;
        /// <summary>사용자 제공 투기장 GLB 규약 (arena_pit_&lt;nation&gt;.glb).</summary>
        public const string ARENA_PIT_GLB_PREFIX = "arena_pit_";

        /// <summary>투기장 GLB 키.</summary>
        public static string ArenaPitGlbKey(string nationStyle)
        {
            string n = (nationStyle ?? "empire").ToLower();
            switch (n)
            {
                case "eastern":  return ARENA_PIT_GLB_PREFIX + "east";
                case "western":  return ARENA_PIT_GLB_PREFIX + "west";
                case "southern": return ARENA_PIT_GLB_PREFIX + "south";
                case "northern": return ARENA_PIT_GLB_PREFIX + "north";
                default:         return ARENA_PIT_GLB_PREFIX + "empire";
            }
        }

        public static GameObject BuildArenaPitInterior(string nationStyle)
        {
            try
            {
                string n = (nationStyle ?? "empire").ToLower();
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null)
                {
                    Debug.LogError("[ArenaPitInteriorBuilder] No supported shader found.");
                    return new GameObject("Room_Fallback");
                }

                // 사각 투기장 셸
                Material floorMat = IndoorMaterialFactory.CreateFloor(PIT_WIDTH, PIT_DEPTH);
                Material wallMat = ResolveWallMaterial(n, nationStyle, shader);
                Material ceilingMat = new Material(shader) { name = "ArenaPit_CeilingMat",
                    mainTexture = IndoorTextureGenerator.GeneratePlasterWall(256, 256, new Color(0.55f, 0.50f, 0.46f)) };

                GameObject room = IndoorBuilder.CreateRoom(PIT_WIDTH, PIT_HEIGHT, PIT_DEPTH, floorMat, wallMat, ceilingMat);
                if (room == null)
                {
                    Debug.LogError("[ArenaPitInteriorBuilder] IndoorBuilder.CreateRoom returned null.");
                    return new GameObject("Room_Fallback");
                }
                room.name = "ArenaPitRoom";
                room.transform.position = Vector3.zero;

                // ── 중앙 함몰 원형 무대 (20m 지름, 깊이 1.5m) ─────────────────
                CreatePitStage(room, wallMat);

                // ── 관중석 링 (4면 계단형 발판 — 비충돌, 위치 장식) ─────────────
                BuildSpectatorRings(room, wallMat);

                // ── 출구 — 월드(메인씬) 복귀. 남벽 중앙. ─────────────────────
                AddExitTrigger(room);

                // ── 사용자 투기장 GLB ────────────────────────────────────────
                TryLoadPitGlb(room, n, nationStyle);

                // ── [2026-10-10 I1~I3 — 계획 v4] 투기장 품질 계층 — 성 내부 승인 언어 이식 ──
                IndoorQualityLayers.AddQuadWallCaps(room, PIT_WIDTH, PIT_DEPTH, PIT_HEIGHT);
                // 무대 위 모래 마모 4장(투기 흔적 — 색 변화 없음. 무대 반경 5m 내부 유지) + 주변 바닥 4장.
                IndoorQualityLayers.AddWearStamps(room, "ArenaPitWearStage", new[]
                {
                    new Vector2(3f, 3f), new Vector2(-3f, 3f), new Vector2(3f, -3f), new Vector2(-3f, -3f),
                }, 1.8f, 0.17f);
                IndoorQualityLayers.AddWearStamps(room, "ArenaPitWearFloor", new[]
                {
                    new Vector2(0f, 12f), new Vector2(0f, -12f), new Vector2(12f, 0f), new Vector2(-12f, 0f),
                }, 2.2f, 0.02f);
                Debug.Log($"[ArenaPitInteriorBuilder] 품질 계층 적용 — 벽 캡 4개, 마모 8장");

                Debug.Log($"[ArenaPitInteriorBuilder] 투기장 실내 생성 완료 (nationStyle: {nationStyle})");
                return room;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ArenaPitInteriorBuilder] 투기장 실내 생성 실패: {e.Message}");
                return new GameObject("Room_Fallback");
            }
        }

        /// <summary>중앙 함몰 원형 무대 — 바닥보다 낮은 원(지름 20m)에 벽체를 두른 구조.</summary>
        private static void CreatePitStage(GameObject room, Material wallMat)
        {
            try
            {
                float radius = 10f;
                float depthY = 1.5f;
                // 무대 바닥 (원 — 함몰)
                var stage = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stage.name = "ArenaPit_Stage";
                stage.transform.SetParent(room.transform);
                stage.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                stage.transform.localScale = new Vector3(radius, 0.1f, radius);
                var stageRend = stage.GetComponent<MeshRenderer>();
                if (stageRend != null)
                {
                    stageRend.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                    { name = "ArenaPit_StageMat", color = new Color(0.75f, 0.62f, 0.45f) };
                }

                // 무대 테두리 난간 (4면 단순 블록)
                float half = radius;
                for (int i = 0; i < 4; i++)
                {
                    var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rail.name = $"ArenaPit_Rail_{i}";
                    rail.transform.SetParent(room.transform);
                    bool horizontal = (i % 2) == 0;
                    rail.transform.localScale = new Vector3(horizontal ? half * 2f : 0.4f, 1.2f, horizontal ? 0.4f : half * 2f);
                    float x = (i == 1) ? half : (i == 3) ? -half : 0f;
                    float z = (i == 0) ? half : (i == 2) ? -half : 0f;
                    rail.transform.localPosition = new Vector3(x, 0.7f, z);
                    var rend = rail.GetComponent<MeshRenderer>();
                    if (rend != null) rend.material = wallMat;
                    rail.AddComponent<MeshCollider>();
                }
                Debug.Log($"[ArenaPitInteriorBuilder] 중앙 함몰 무대 생성 (r={radius}m, 깊이 {depthY}m)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaPitInteriorBuilder] 무대 생성 실패: {e.Message}");
            }
        }

        /// <summary>4면 관중석 발판 (비충돌 — MeshCollider 없음, 위치 장식).</summary>
        private static void BuildSpectatorRings(GameObject room, Material wallMat)
        {
            try
            {
                float[] offsets = { 14f, 18f, 22f };
                int offCount = 3;
                for (int di = 0; di < offCount; di++)
                {
                    float d = offsets[di];
                    // 4면 각각 긴 발판
                    for (int side = 0; side < 4; side++)
                    {
                        var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        seat.name = $"ArenaPit_Seat_{side}_{(int)(d)}";
                        seat.transform.SetParent(room.transform);
                        bool horizontal = (side % 2) == 0;
                        seat.transform.localScale = new Vector3(horizontal ? d * 1.6f : 0.8f, 0.15f, horizontal ? 0.8f : d * 1.6f);
                        float sign = (side < 2) ? 1f : -1f;
                        seat.transform.localPosition = new Vector3(
                            (side == 1) ? d * sign : (side == 3) ? -d * sign : 0f,
                            0.4f,
                            (side == 0) ? d * sign : (side == 2) ? -d * sign : 0f);
                        var rend = seat.GetComponent<MeshRenderer>();
                        if (rend != null) rend.material = wallMat;
                        // 비충돌 (장식)
                        // [2026-10-10 I2] 관중석 발판 접지 블롭 — 떠 있는 판이 아니라 바닥에 놓인 발판으로.
                        IndoorQualityLayers.AddBlobAt(room,
                            new Vector3(seat.transform.localPosition.x, 0f, seat.transform.localPosition.z),
                            horizontal ? d * 0.9f : 1.2f, horizontal ? 1.2f : d * 0.9f, 0.025f);
                    }
                }
                Debug.Log("[ArenaPitInteriorBuilder] 관중석 발판 배치 완료");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaPitInteriorBuilder] 관중석 배치 실패: {e.Message}");
            }
        }

        /// <summary>출구 트리거 (E키 → 월드 복귀).</summary>
        private static void AddExitTrigger(GameObject room)
        {
            try
            {
                var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
                door.name = "ArenaPit_ExitDoor";
                door.transform.SetParent(room.transform);
                door.transform.localPosition = new Vector3(0f, 1.5f, PIT_DEPTH * 0.5f);
                door.transform.localScale = new Vector3(2f, 3f, 0.4f);
                var rend = door.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                    { name = "ArenaPit_ExitMat", color = new Color(0.40f, 0.25f, 0.10f) };
                }
                var trigger = door.AddComponent<BuildingTrigger>();
                trigger.BuildingType = "Exit";
                Debug.Log("[ArenaPitInteriorBuilder] 출구 트리거 배치 — 월드 복귀");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ArenaPitInteriorBuilder] 출구 배치 실패: {e.Message}");
            }
        }

        private static void TryLoadPitGlb(GameObject room, string nation, string nationStyle)
        {
            string glbKey = ArenaPitGlbKey(nationStyle);
            if (RuntimeModelLoader.TryGetModel(glbKey, out var modelPrefab))
            {
                GameObject inst = Object.Instantiate(modelPrefab, room.transform, false);
                inst.name = "ArenaPit_Model";
                inst.transform.localPosition = new Vector3(0f, 0f, 0f);
                Debug.Log($"[ArenaPitInteriorBuilder] 투기장 GLB 로드: '{glbKey}' → 무대 중앙 배치");
            }
            else
            {
                Debug.Log($"[ArenaPitInteriorBuilder] 투기장 GLB '{glbKey}' 없음 — 셸만 사용 (arena_pit_{nation}.glb 추가 시 자동 로드)");
            }
        }

        private static Material ResolveWallMaterial(string n, string nationStyle, Shader shader)
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
            return new Material(shader) { name = $"ArenaPit_WallMat_{nationStyle}", mainTexture = tex };
        }
    }
}