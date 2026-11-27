// 2026-10-23 (120차): 실제 투기가 벌어지는 투기장 — 'COLOSSEUM LUDUS MAGNUS: ARENA FLOOR' 설계도 재구축.
// 사각 셸 폐기 → 타원형 오픈에어 투기장: 모래 타원 바닥 + 외곽 벽 링(48세그먼트) + 6게이트(서/동 대형·북×2·남×2)
// + 글래디에이터 배치구역(서/동) + 장애물 + 중앙 결투 마커 + 남측 왕가 박스(설계도 하단 중앙 돌출부).
// 베팅 로비(Arena)의 출입문으로 진입. ArenaSystem 전투 시뮬레이션은 ArenaMenuUI가 담당.
// GLB 규약: Assets/Resources/Models/UserProvided/arena_pit_<nation>.glb
#pragma warning disable 618
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 타원형 오픈에어 투기장 — 모래 바닥 + 외곽 벽 링 + 6게이트 + 배치구역 + 출구.
    /// [120차 설계도 재구축] 스케일 계약: 설계도 100단위=1m authored ×2.45=최종(직접 배치).
    /// 외곽 타원 반축 22.8×23.9 / 모래 반축 17.8×18.6(톱 y=0.15 — 구 무대 톱과 동일, 보행 계약 유지).
    /// </summary>
    public static class ArenaPitInteriorBuilder
    {
        // ── 타원 기하 (최종 좌표 — 설계도 1860×1950단위 ×2.45/100) ──────────
        public const float PIT_OUTER_A = 22.8f;   // 외곽 타원 x반축
        public const float PIT_OUTER_B = 23.9f;   // 외곽 타원 z반축
        public const float PIT_SAND_A = 17.8f;    // 모래 타원 x반축
        public const float PIT_SAND_B = 18.6f;    // 모래 타원 z반축
        public const float PIT_HEIGHT = 8f;       // 벽 높이(기존 실내 표준)
        public const float SAND_TOP_Y = 0.15f;    // 모래 톱 — 구 무대 톱과 동일(보행/마모 y 계약)
        private const int WALL_SEGMENTS = 48;     // 벽 링 세그먼트 수
        private const float WALL_THICKNESS = 0.8f;
        private const float GATE_HALF_ANGLE_DEG = 7f;   // 게이트 개구 반각(°)
        /// <summary>사용자 제공 투기장 GLB 규약 (arena_pit_<nation>.glb).</summary>
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

        /// <summary>6게이트 중심각(°) — 서180/동0 대형, 북68·112, 남248·292(설계도 배치).</summary>
        private static readonly float[] GateAngles = { 0f, 68f, 112f, 180f, 248f, 292f };

        /// <summary>기둥 12개 각도 — 게이트 사이/게이트 오프셋(게이트 ±7° 회피).</summary>
        private static readonly float[] ColumnAngles =
            { 34f, 90f, 146f, 214f, 270f, 326f, 14f, 82f, 126f, 194f, 262f, 338f };

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
                Material wallMat = ResolveWallMaterial(n, nationStyle, shader);

                GameObject room = new GameObject("ArenaPitRoom");
                room.transform.position = Vector3.zero;

                // ── 1. 외곽 에이프런(벽 안쪽 보행 띠, 톱 y=0.02) + 모래 타원(톱 y=0.15) ──
                CreateApron(room, shader);
                CreateSandFloor(room, shader);
                BuildApronBlend(room);   // [125차 후속36 A4] 모래-에이프런 경계 블렌드 링

                // ── 2. 외곽 벽 링(48세그먼트 — 게이트 각도 스킵) ─────────────────
                int segs = BuildWallRing(room, wallMat);

                // ── 3. 6게이트 아치(기둥 2 + 인방 + 톱 캡) ──────────────────────
                int caps = BuildGateArches(room, wallMat);

                // ── 4. 기둥 12개(내면) + 접지 블롭 12개 ─────────────────────────
                int blobs = BuildColumns(room, wallMat);

                // ── 5. 글래디에이터 배치구역(서/동 점선 원) ─────────────────────
                BuildDeploymentAreas(room, shader);

                // ── 6. 장애물(바구니/단지/돌 6개 — 결정론 위치) ─────────────────
                BuildObstacles(room, shader);

                // ── 7. 중앙 결투 마커(결투 원 + 출발선) ────────────────────────
                BuildCenterMarkers(room, shader);

                // ── 8. 남측 왕가 박스(설계도 하단 중앙 돌출부) ──────────────────
                caps += BuildRoyalBox(room, wallMat, shader);

                // ── 9. 출구 — 북측 중앙(θ=90°) 내면 문. E키 → 월드/로비 복귀. ────
                AddExitTrigger(room, wallMat);

                // ── 사용자 투기장 GLB ────────────────────────────────────────
                TryLoadPitGlb(room, n, nationStyle);

                // ── [120차] 마모 스탬프 4+4(테스트 계약 — 모래 위 r<4.6 / 에이프런) ──
                IndoorQualityLayers.AddWearStamps(room, "ArenaPitWearStage", new[]
                {
                    new Vector2(2.2f, 2.2f), new Vector2(-2.2f, 2.2f),
                    new Vector2(2.2f, -2.2f), new Vector2(-2.2f, -2.2f),
                }, 1.8f, 0.17f);   // 모래 톱 0.15 위 얇은 높이(테스트 y 계약 유지)
                IndoorQualityLayers.AddWearStamps(room, "ArenaPitWearFloor", new[]
                {
                    new Vector2(20f, 0f), new Vector2(-20f, 0f),
                    new Vector2(0f, 21.5f), new Vector2(0f, -21.5f),
                }, 2.2f, 0.02f);   // 에이프런(모래 밖 띠) 위

                // ── [125차 후속24 — A2] 품질레이어 확장: 벽톱 캡(곡선 추종) + 게이트/왕가 블롭 + 마모 18장 ──
                // 벽톱 캡 — 타원 곡선 추종 회전 캡(AddBoxCap은 축정렬이라 곡선 구간 부적합 → 직접 생성,
                //   성 내부 AddCap과 동일 마감: 석재 파일 + 캡 톤 0.78/0.76/0.73). _TopCap 계약 접미사 유지.
                var capStone = IndoorMaterialFactory.CreateWallStoneLower();
                if (capStone != null) capStone.color = new Color(0.78f, 0.76f, 0.73f);
                int wallCaps = 0;
                for (int i = 0; i < WALL_SEGMENTS; i++)
                {
                    float theta = i * (360f / WALL_SEGMENTS);
                    if (IsGateAngle(theta)) continue;
                    if (capStone == null) break;   // 텍스처 미투입 시 스킵(선택 강화 규약)
                    Vector3 pos = EllipsePoint(theta, PIT_OUTER_A, PIT_OUTER_B);
                    var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cap.name = $"ArenaPit_WallSeg{i}_TopCap";
                    Object.DestroyImmediate(cap.GetComponent<Collider>());   // 렌더러 전용 계약
                    cap.transform.SetParent(room.transform, false);
                    cap.transform.localPosition = new Vector3(pos.x, PIT_HEIGHT + 0.06f, pos.z);
                    cap.transform.localRotation =
                        Quaternion.LookRotation(EllipseTangent(theta, PIT_OUTER_A, PIT_OUTER_B), Vector3.up);
                    cap.transform.localScale = new Vector3(3.5f, 0.12f, WALL_THICKNESS + 0.35f);
                    var crend = cap.GetComponent<MeshRenderer>();
                    if (crend != null)
                    {
                        crend.sharedMaterial = capStone;
                        crend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    wallCaps++;
                }
                // 게이트 기둥 접지 블롭 12 — 좌우 기둥 위치(BuildGateArches halfSpan 3.1 동일 좌표, 에이프런 높이)
                // [125차 후속24 보완] 1차 구현의 게이트 중심 블롭 6 → 계획(A2) 명세대로 기둥 위치 12로 승격
                int gateBlobs = 0;
                foreach (float gate in GateAngles)
                {
                    Vector3 gpos = EllipsePoint(gate, PIT_OUTER_A, PIT_OUTER_B);
                    Vector3 gtan = EllipseTangent(gate, PIT_OUTER_A, PIT_OUTER_B);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 ppos = gpos + gtan * (3.1f * s);
                        IndoorQualityLayers.AddBlobAt(room, new Vector3(ppos.x, 0f, ppos.z), 1.3f, 1.3f, 0.03f);
                        gateBlobs++;
                    }
                }
                // 장애물 프롭 접지 블롭 6 — BuildObstacles 결정론 위치 동일 좌표(모래 톱 0.15 위)
                int propBlobs = 0;
                foreach (var spot in new[]
                {
                    new Vector2(5f, 3f), new Vector2(-5f, 3f),
                    new Vector2(7f, -2f), new Vector2(-7f, -2f),
                    new Vector2(0f, 8f), new Vector2(0f, -8f),
                })
                {
                    IndoorQualityLayers.AddBlobAt(room, new Vector3(spot.x, 0f, spot.y), 1.1f, 1.1f, 0.17f);
                    propBlobs++;
                }
                // 왕가 박스 접지 블롭 1 — 남측 돌출부 하단
                IndoorQualityLayers.AddBlobAt(room, new Vector3(0f, 0f, -(PIT_OUTER_B + 1.5f)), 6.4f, 4.8f, 0.03f);

                // 마모 확장 — 결투 궤적(모래) 6 / 게이트 발(모래) 6 / 게이트 에이프런 6 (신규 프리픽스 — 기존 4+4 계약 무충돌)
                IndoorQualityLayers.AddWearStamps(room, "ArenaPitWearDuel", new[]
                {
                    new Vector2(1.5f, 1.2f), new Vector2(-1.6f, 1.0f),
                    new Vector2(1.1f, -1.7f), new Vector2(-1.2f, -1.4f),
                    new Vector2(2.4f, 0.6f), new Vector2(-2.5f, -0.7f),
                }, 1.6f, 0.17f);   // 모래 톱 0.15 위 — 무대 마모와 동일 높이
                var gateSand = new Vector2[GateAngles.Length];
                var gateApron = new Vector2[GateAngles.Length];
                for (int g = 0; g < GateAngles.Length; g++)
                {
                    Vector3 sand = EllipsePoint(GateAngles[g], PIT_SAND_A - 0.8f, PIT_SAND_B - 0.8f);
                    gateSand[g] = new Vector2(sand.x, sand.z);
                    Vector3 apron = EllipsePoint(GateAngles[g], PIT_OUTER_A - 1.3f, PIT_OUTER_B - 1.3f);
                    gateApron[g] = new Vector2(apron.x, apron.z);
                }
                IndoorQualityLayers.AddWearStamps(room, "ArenaPitWearGate", gateSand, 1.4f, 0.17f);
                IndoorQualityLayers.AddWearStamps(room, "ArenaPitWearApron", gateApron, 1.8f, 0.04f);

                Debug.Log($"[ArenaPitInteriorBuilder] 타원 투기장 생성 완료 (nationStyle: {nationStyle}) — " +
                          $"외곽 {PIT_OUTER_A}×{PIT_OUTER_B} / 모래 {PIT_SAND_A}×{PIT_SAND_B} / 벽세그먼트 {segs} / 캡 {caps} / 블롭 {blobs} / " +
                          $"[125차 후속24] 벽톱캡 {wallCaps} + 게이트기둥블롭 {gateBlobs} + 프롭블롭 {propBlobs} — 블롭 총 {blobs + gateBlobs + propBlobs + 1}(계획 31 구성)");
                // [125차 후속13] 시간연동 조명 — 외부 아레나는 메인씬 시간에 따라 낮/밤(사용자 지시).
                //   낮=밝은 앰비언트+태양 방향광 / 밤=어두운 앰비언트+기둥 횃불 링(포인트라이트).
                //   IndoorScene 전환 본문의 앰비언트 덮어쓰기는 후속12에서 조건화돼 이 값이 유지된다.
                bool isDay = TimeManager.Instance == null || TimeManager.Instance.IsDay;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                // [125차 후속23 F1] 투기장 포그 강제 해제 — 사용자 지시: "아레나 외부 씬에는 포그 없애, 포그는 실내씬만".
                //   야외 오픈에어 경기장 계약 — 미래에 DNC/날씨 계열이 부착돼도 투기장에서 포그가 켜지지 않게
                //   빌더 진입점에서 명시 해제(매 빌드마다 재적용). 실내 건물 빌더 경로는 영향 없음.
                RenderSettings.fog = false;
                if (isDay)
                {
                    RenderSettings.ambientLight = new Color(0.55f, 0.52f, 0.46f);
                    var sunGo = new GameObject("ArenaPit_Sun");
                    sunGo.transform.SetParent(room.transform, false);
                    sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    var sun = sunGo.AddComponent<Light>();
                    sun.type = LightType.Directional;
                    sun.color = new Color(1f, 0.96f, 0.88f);
                    sun.intensity = 1.0f;
                    // [125차 후속35-R P1-2] 태양 Soft 섀도우 — 외곽 벽(8m)·게이트 기둥·왕가 박스 그림자가
                    //   모래(톱 0.15)에 실림 = 투기장 공간감 결정적 상승. 방향광 섀도우는 고밀도(H4 포인트
                    //   큐브 노이즈 이슈와 무관). 섀도우 거리 50은 URP 에셋에 이미 설정.
                    sun.shadows = LightShadows.Soft;
                    Debug.Log("[ArenaPitInterior] [125차 후속13] 주간 조명 — 앰비언트 0.55+태양 방향광");
                }
                else
                {
                    // [125차 후속18] 야간 앰비언트 달빛 상향 0.10→0.17 청백 — 구조물 실루엣 확보(영상6 실측:
                    //   바닥에 빛의 원이 안 보이고 구조물이 식별 불가 — 앰비언트 기저와 광원 강도 동시 부족).
                    // [125차 후속25 R2] RT 실측 갱신 — (0.13,0.15,0.20)에서 관측 모래 밝기 0.378 = "안 보임" 지각.
                    //   (0.19,0.21,0.27)로 상향 — 목표 모래 렌더 ~0.55-0.60(pit_audit_rt.png 재판독으로 검증).
                    RenderSettings.ambientLight = new Color(0.19f, 0.21f, 0.27f);
                    // [125차 후속19] 달빛 방향광(메인 라이트) — URP 추가 라이트는 오브젝트당 4개 한계
                    //   (URPAsset.asset m_AdditionalLightsPerObjectLimit:4 실측)라 17개 포인트라이트가 예산에서
                    //   잘려 대부분 무광이었다("깜깜함"의 뿌리). 방향광은 메인 라이트라 한계 무관 항상 렌더 —
                    //   밤에도 기저 조명 보장. 낮 분기의 태양(ArenaPit_Sun)과 동일 구조.
                    var moonGo = new GameObject("ArenaPit_Moon");
                    moonGo.transform.SetParent(room.transform, false);
                    moonGo.transform.rotation = Quaternion.Euler(45f, 60f, 0f);
                    var moon = moonGo.AddComponent<Light>();
                    moon.type = LightType.Directional;
                    moon.color = new Color(0.72f, 0.80f, 1f);   // 달빛 청백
                    moon.intensity = 1.15f;   // [125차 후속25 R2] 0.7→1.15 — RT 실측(관측 0.378="안 보임" 지각) 근거 상향
                    // [125차 후속35-R P1-2] 달빛 Soft 섀도우 — 야간 횃불 링과 대비되는 구조물 그림자.
                    moon.shadows = LightShadows.Soft;
                    foreach (float ang in ColumnAngles)
                    {
                        Vector3 tpos = EllipsePoint(ang, PIT_OUTER_A - 1.6f, PIT_OUTER_B - 1.6f);
                        // [125차 후속35-R P1-3] 기둥 횃불 플리커 — 불링 전체가 각자 다른 위상으로 진동.
                        IndoorLighting.AddPointLight(room, new Vector3(tpos.x, 3.2f, tpos.z), new Color(1f, 0.72f, 0.42f), 13f, 3.0f, flicker: true);   // 기둥 횃불 — 2.4→3.0/범위 11→13
                        // [125차 후속18] 가시 횃불 프롭 — 기존 횃불은 보이지 않는 포인트라이트만 존재해 어둠 속에서
                        //   "여기가 투기장"인지 인지 불가(영상6 실측). 포스트+발광 화염(emissive)으로 광원 위치를
                        //   시각화 — 라이트와 동일 좌표 일치.
                        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        post.name = $"ArenaPit_TorchPost_{(int)ang}";
                        post.transform.SetParent(room.transform, false);
                        post.transform.localPosition = new Vector3(tpos.x, 1.1f, tpos.z);
                        post.transform.localScale = new Vector3(0.14f, 2.2f, 0.14f);
                        var postRend = post.GetComponent<MeshRenderer>();
                        if (postRend != null)
                            postRend.material = new Material(shader) { name = "ArenaPit_TorchPostMat", color = new Color(0.24f, 0.17f, 0.12f, 1f) };
                        var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        flame.name = $"ArenaPit_TorchFlame_{(int)ang}";
                        flame.transform.SetParent(room.transform, false);
                        flame.transform.localPosition = new Vector3(tpos.x, 2.55f, tpos.z);
                        flame.transform.localScale = new Vector3(0.30f, 0.42f, 0.30f);
                        Object.Destroy(flame.GetComponent<Collider>());
                        var flameRend = flame.GetComponent<MeshRenderer>();
                        if (flameRend != null)
                        {
                            var flameMat = new Material(shader) { name = "ArenaPit_TorchFlameMat", color = new Color(1f, 0.62f, 0.20f, 1f) };
                            flameMat.EnableKeyword("_EMISSION");
                            flameMat.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.15f) * 2.2f);
                            flameRend.material = flameMat;   // 발광 화염 — 어둠 속에서도 자체 발광으로 식별
                        }
                    }
                    // [125차 후속15] 밤 중앙 스타디움 조명 — 횃불 링은 외곽만이라 관전 카메라 시점(중앙)이
                    //   무광이었다(아레나 테스트 3 영상 실측). [125차 후속18] y 14→12(바닥 도달 강도↑)+강도 3.0→4.5.
                    //   [125차 후속25 R2] 4.5→6.5 — 물리 감쇠상 12m 하부 도달이 약해(RT 실측) 중앙 결투면 가시성 강화.
                    IndoorLighting.AddPointLight(room, new Vector3(0f, 12f, 0f), new Color(1f, 0.93f, 0.80f), 26f, 6.5f);   // 중앙 스타디움
                    foreach (var c in new[] { new Vector3(-10f, 9f, -10f), new Vector3(10f, 9f, -10f), new Vector3(-10f, 9f, 10f), new Vector3(10f, 9f, 10f) })
                        IndoorLighting.AddPointLight(room, c, new Color(1f, 0.88f, 0.70f), 18f, 3.0f);   // 코너 플러드 — 2.2→3.0/범위 16→18
                    // [125차 후속19] 라이트 덤프 — 씬 라이트 총수/활성/강도 실측(재발 방지 판별표)
                    int totalLights = 0, enabledLights = 0;
                    foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(UnityEngine.FindObjectsSortMode.None))
                    {
                        totalLights++;
                        if (l.enabled) enabledLights++;
                    }
                    Debug.Log($"[ArenaPitInterior] [125차 후속18/19/25R2] 야간 조명 — 앰비언트 (0.19,0.21,0.27)+달빛 방향광 1.15+기둥 횃불 {ColumnAngles.Length}개(3.0/13m)+중앙 스타디움(6.5/y12/범위26)+4코너 플러드(3.0/18m) / 씬 라이트 총 {totalLights}개(활성 {enabledLights})");
                }
                return room;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ArenaPitInteriorBuilder] 투기장 실내 생성 실패: {e.Message}");
                return new GameObject("Room_Fallback");
            }
        }

        /// <summary>외곽 에이프런 — 모래 밖·벽 안쪽 보행 띠(톱 y=0.02, floorWear 받침).</summary>
        private static void CreateApron(GameObject room, Shader shader)
        {
            var apron = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            apron.name = "ArenaPit_Apron";
            apron.transform.SetParent(room.transform, false);
            apron.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            apron.transform.localScale = new Vector3(PIT_OUTER_A * 2f, 0.04f, PIT_OUTER_B * 2f);
            var rend = apron.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                // [125차 후속24 A1] 에이프런 = 다져진 흙 슬래브 팩토리(스무스니스 0.30). 폴백: 기존 텍스처.
                var mat = ArenaOutdoorMaterialFactory.CreateApron();
                if (mat == null)
                {
                    var tex = IndoorTextureGenerator.GenerateFloorTile(256, 256,
                        new Color(0.50f, 0.46f, 0.39f), new Color(0.41f, 0.38f, 0.32f), 0.5f);
                    mat = new Material(shader) { name = "ArenaPit_ApronMat", mainTexture = tex };
                }
                rend.material = mat;
            }
            // 실린더 프리미티브 기본 MeshCollider 유지 — 에이프런 전역 보행 가능.
        }

        /// <summary>
        /// [125차 후속36 A4] 모래-에이프런 경계 블렌드 링 — 모래 타원 외연에서 1.2m 바깥까지
        /// 라디얼 그라데이션 링(모래색→흙색 페이드). 에이프런 톱(0.02) 위 얇은 데칼(y=0.035),
        /// 렌더러 전용(충돌/그림자 없음). 판독 근거: 영상15 f_012 "경계가 칼같이 잘림".
        /// </summary>
        private static void BuildApronBlend(GameObject room)
        {
            var mat = ArenaOutdoorMaterialFactory.CreateApronBlend();
            if (mat == null) return;   // 폴백 계약 — 블렌드 없는 기존 경로

            const int segs = 64;
            const float ringOut = 1.2f;   // 링 폭 — 모래 외연에서 바깥으로
            var verts = new Vector3[(segs + 1) * 2];
            var uvs = new Vector2[(segs + 1) * 2];
            var tris = new int[segs * 6];
            for (int i = 0; i <= segs; i++)
            {
                float theta = (float)i / segs * Mathf.PI * 2f;
                float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
                // 내연(모래 타원 외연) — u=0
                verts[i * 2] = new Vector3(PIT_SAND_A * c, 0.035f, PIT_SAND_B * s);
                uvs[i * 2] = new Vector2(0f, (float)i / segs);
                // 외연(+1.2m 확장 타원) — u=1
                verts[i * 2 + 1] = new Vector3((PIT_SAND_A + ringOut) * c, 0.035f, (PIT_SAND_B + ringOut) * s);
                uvs[i * 2 + 1] = new Vector2(1f, (float)i / segs);
                if (i < segs)
                {
                    int a = i * 2, b = i * 2 + 1, c2 = (i + 1) * 2 + 1, d = (i + 1) * 2;
                    // 위(+Y)를 향하는 와인딩 — (A,D,C)+(A,C,B) (0.035 데칼, 항상 위에서 관람)
                    tris[i * 6 + 0] = a; tris[i * 6 + 1] = d; tris[i * 6 + 2] = c2;
                    tris[i * 6 + 3] = a; tris[i * 6 + 4] = c2; tris[i * 6 + 5] = b;
                }
            }
            var mesh = new Mesh { name = "ArenaPit_ApronBlend_Mesh" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var blendGo = new GameObject("ArenaPit_ApronBlend");
            blendGo.transform.SetParent(room.transform, false);
            blendGo.transform.localPosition = Vector3.zero;
            var mf = blendGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = blendGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>모래 타원 바닥 — name "ArenaPit_Stage"(테스트 계약), 시각 톱 y=0.15(구 무대 톱 동일). 보행면=[125차 후속29 G2] 박스 콜라이더(톱 0.14) — 실린더 MeshCollider 폐기.</summary>
        private static void CreateSandFloor(GameObject room, Shader shader)
        {
            var sand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sand.name = "ArenaPit_Stage";
            sand.transform.SetParent(room.transform, false);
            sand.transform.localPosition = new Vector3(0f, 0f, 0f);
            sand.transform.localScale = new Vector3(PIT_SAND_A * 2f, 0.15f, PIT_SAND_B * 2f);
            var rend = sand.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                // [125차 후속24 A1] 모래 = 격자 폐기 → 입자 노이즈 팩토리(성 내부 HQ 계약 이식). 폴백: 기존 격자 텍스처.
                var mat = ArenaOutdoorMaterialFactory.CreateSand();
                if (mat == null)
                {
                    var tex = IndoorTextureGenerator.GenerateFloorTile(256, 256,
                        new Color(0.88f, 0.80f, 0.62f), new Color(0.80f, 0.72f, 0.54f), 0.35f);
                    mat = new Material(shader) { name = "ArenaPit_SandMat", mainTexture = tex };
                }
                rend.material = mat;
            }

            // [125차 후속29 G2] 보행용 결정론 박스 콜라이더 — 비균일 스케일(35.6, 0.15, 37.2) 실린더
            //   MeshCollider의 접지 불신뢰를 박스로 대체. 렌더와 무관한 충돌 전용 — 톱 y=0.14
            //   (시각 톱 0.15 대비 1cm 침하, 시각 변화 0). 플레이어+병사 공용 보행면.
            var stageMeshCol = sand.GetComponent<MeshCollider>();
            if (stageMeshCol != null) stageMeshCol.enabled = false;   // 실린더 메시 콜라이더 폐기 — 박스 단일화
            var walkBox = sand.AddComponent<BoxCollider>();
            walkBox.center = new Vector3(0f, -0.01f, 0f);   // y ∈ [-0.16, +0.14] — 톱 0.14
            walkBox.size = new Vector3(PIT_SAND_A * 2f, 0.3f, PIT_SAND_B * 2f);
        }

        /// <summary>외곽 벽 링 — 타원 48세그먼트 박스(게이트 각도 스킵), 각 MeshCollider. 반환: 생성 세그먼트 수.</summary>
        private static int BuildWallRing(GameObject room, Material wallMat)
        {
            int built = 0;
            for (int i = 0; i < WALL_SEGMENTS; i++)
            {
                float theta = i * (360f / WALL_SEGMENTS);
                if (IsGateAngle(theta)) continue;   // 게이트 개구부 — 세그먼트 생략
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"ArenaPit_WallSeg_{i}";
                seg.transform.SetParent(room.transform, false);
                Vector3 pos = EllipsePoint(theta, PIT_OUTER_A, PIT_OUTER_B);
                seg.transform.localPosition = new Vector3(pos.x, PIT_HEIGHT * 0.5f, pos.z);
                Vector3 tangent = EllipseTangent(theta, PIT_OUTER_A, PIT_OUTER_B);
                seg.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
                seg.transform.localScale = new Vector3(3.4f, PIT_HEIGHT, WALL_THICKNESS);   // 1.15중첩 — 틈 방지
                var rend = seg.GetComponent<MeshRenderer>();
                if (rend != null) rend.material = wallMat;
                built++;
            }
            return built;
        }

        /// <summary>6게이트 아치 — 좌우 기둥 + 인방 + 톱 캡(_TopCap — 렌더러 전용, 테스트 캡≥4). 반환: 캡 수.</summary>
        private static int BuildGateArches(GameObject room, Material wallMat)
        {
            int caps = 0;
            foreach (float gate in GateAngles)
            {
                Vector3 pos = EllipsePoint(gate, PIT_OUTER_A, PIT_OUTER_B);
                Vector3 tangent = EllipseTangent(gate, PIT_OUTER_A, PIT_OUTER_B);
                Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);
                float halfSpan = 3.1f;   // 게이트 폭≈5.4m(반폭 2.7 + 기둥 절반)
                for (int s = -1; s <= 1; s += 2)
                {
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.name = $"ArenaPit_GatePillar_{(int)gate}_{(s < 0 ? "L" : "R")}";
                    pillar.transform.SetParent(room.transform, false);
                    pillar.transform.localPosition = pos + tangent * (halfSpan * s);
                    pillar.transform.localPosition = new Vector3(
                        pillar.transform.localPosition.x, PIT_HEIGHT * 0.5f, pillar.transform.localPosition.z);
                    pillar.transform.localRotation = rot;
                    pillar.transform.localScale = new Vector3(0.8f, PIT_HEIGHT, 0.8f);
                    var rend = pillar.GetComponent<MeshRenderer>();
                    if (rend != null) rend.material = wallMat;
                }
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = $"ArenaPit_GateLintel_{(int)gate}";
                lintel.transform.SetParent(room.transform, false);
                lintel.transform.localPosition = new Vector3(pos.x, PIT_HEIGHT - 0.6f, pos.z);
                lintel.transform.localRotation = rot;
                lintel.transform.localScale = new Vector3(halfSpan * 2f + 0.8f, 1.2f, 0.9f);
                var lrend = lintel.GetComponent<MeshRenderer>();
                if (lrend != null) lrend.material = wallMat;
                // 인방 톱 캡 — 렌더러 전용(탑다운 벽톱면 계약)
                IndoorQualityLayers.AddBoxCap(room, $"ArenaPit_GateLintel_{(int)gate}_TopCap",
                    new Vector3(pos.x, PIT_HEIGHT + 0.075f, pos.z), new Vector3(halfSpan * 2f + 0.8f, 0.15f, 0.9f));
                // [125차 후속35-R P5-2] 게이트 현판 — 국가 절차 벽 텍스처 배너(파일 없음, 국가색 유지).
                //   인방 내면 앞면 부착(렌더러 전용 — 보행 무간섭).
                var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
                banner.name = $"ArenaPit_GateBanner_{(int)gate}";
                banner.transform.SetParent(room.transform, false);
                Vector3 inward = new Vector3(-pos.x, 0f, -pos.z).normalized;
                Vector3 bpos = pos + inward * (WALL_THICKNESS * 0.5f + 0.12f);
                banner.transform.localPosition = new Vector3(bpos.x, PIT_HEIGHT - 0.6f, bpos.z);
                banner.transform.localRotation = Quaternion.LookRotation(inward, Vector3.up);
                banner.transform.localScale = new Vector3(2.6f, 1.0f, 0.1f);
                Object.DestroyImmediate(banner.GetComponent<Collider>());   // 렌더러 전용
                var brend = banner.GetComponent<MeshRenderer>();
                if (brend != null)
                {
                    brend.sharedMaterial = wallMat;
                    brend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                caps++;
            }
            return caps;
        }

        /// <summary>기둥 12개(내면 따라) — 각 기둥 밑에 접지 블롭(총 12 — 테스트 계약). 반환: 블롭 수.</summary>
        private static int BuildColumns(GameObject room, Material wallMat)
        {
            int blobs = 0;
            foreach (float ang in ColumnAngles)
            {
                Vector3 pos = EllipsePoint(ang, PIT_OUTER_A - 1.6f, PIT_OUTER_B - 1.6f);
                var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                col.name = $"ArenaPit_Column_{(int)ang}";
                col.transform.SetParent(room.transform, false);
                col.transform.localPosition = new Vector3(pos.x, PIT_HEIGHT * 0.5f, pos.z);
                col.transform.localScale = new Vector3(1.0f, PIT_HEIGHT, 1.0f);
                var rend = col.GetComponent<MeshRenderer>();
                if (rend != null) rend.material = wallMat;
                IndoorQualityLayers.AddBlobAt(room, new Vector3(pos.x, 0f, pos.z), 1.2f, 1.2f, 0.16f);
                blobs++;
            }
            return blobs;
        }

        /// <summary>글래디에이터 배치구역 — 서/동 평면 링(외곽 어두운 원 + 내부 모래색 원 — 비충돌).</summary>
        private static void BuildDeploymentAreas(GameObject room, Shader shader)
        {
            foreach (float x in new[] { -12f, 12f })
            {
                var outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                outer.name = $"ArenaPit_Deploy_{(x < 0 ? "W" : "E")}_Outer";
                outer.transform.SetParent(room.transform, false);
                outer.transform.localPosition = new Vector3(x, SAND_TOP_Y, 0f);
                outer.transform.localScale = new Vector3(9.8f, 0.01f, 9.8f);
                var orend = outer.GetComponent<MeshRenderer>();
                if (orend != null)
                    orend.material = new Material(shader) { name = "ArenaPit_DeployRingMat", color = new Color(0.55f, 0.47f, 0.34f) };
                DestroyCollider(outer);

                var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                inner.name = $"ArenaPit_Deploy_{(x < 0 ? "W" : "E")}_Inner";
                inner.transform.SetParent(room.transform, false);
                inner.transform.localPosition = new Vector3(x, SAND_TOP_Y + 0.005f, 0f);
                inner.transform.localScale = new Vector3(7.8f, 0.01f, 7.8f);
                var irend = inner.GetComponent<MeshRenderer>();
                if (irend != null)
                    irend.material = new Material(shader) { name = "ArenaPit_DeployInnerMat", color = new Color(0.76f, 0.68f, 0.52f) };
                DestroyCollider(inner);
            }
        }

        /// <summary>장애물 — 바구니/단지/돌 6개(설계도 장애물 범례, 결정론 위치).</summary>
        private static void BuildObstacles(GameObject room, Shader shader)
        {
            var spots = new (Vector2 xz, float h, Color c)[]
            {
                (new Vector2(5f, 3f), 0.6f, new Color(0.45f, 0.33f, 0.18f)),    // 바구니
                (new Vector2(-5f, 3f), 0.6f, new Color(0.45f, 0.33f, 0.18f)),   // 바구니
                (new Vector2(7f, -2f), 0.7f, new Color(0.60f, 0.45f, 0.28f)),   // 단지
                (new Vector2(-7f, -2f), 0.7f, new Color(0.60f, 0.45f, 0.28f)),  // 단지
                (new Vector2(0f, 8f), 0.5f, new Color(0.52f, 0.50f, 0.47f)),    // 돌
                (new Vector2(0f, -8f), 0.5f, new Color(0.52f, 0.50f, 0.47f)),   // 돌
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var ob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ob.name = $"ArenaPit_Obstacle_{i}";
                ob.transform.SetParent(room.transform, false);
                ob.transform.localPosition = new Vector3(spots[i].xz.x, SAND_TOP_Y + spots[i].h * 0.5f, spots[i].xz.y);
                ob.transform.localScale = new Vector3(0.8f, spots[i].h, 0.8f);
                var rend = ob.GetComponent<MeshRenderer>();
                if (rend != null)
                    rend.material = new Material(shader) { name = $"ArenaPit_ObstacleMat_{i}", color = spots[i].c };
            }
        }

        /// <summary>중앙 결투 마커 — 결투 원(r≈3 링) + 출발선 2(비충돌 데칼).</summary>
        private static void BuildCenterMarkers(GameObject room, Shader shader)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ArenaPit_DuelRing";
            ring.transform.SetParent(room.transform, false);
            ring.transform.localPosition = new Vector3(0f, SAND_TOP_Y, 0f);
            ring.transform.localScale = new Vector3(6f, 0.01f, 6f);
            var rrend = ring.GetComponent<MeshRenderer>();
            if (rrend != null)
                rrend.material = new Material(shader) { name = "ArenaPit_DuelRingMat", color = new Color(0.50f, 0.42f, 0.30f) };
            DestroyCollider(ring);

            var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "ArenaPit_DuelRing_Inner";
            inner.transform.SetParent(room.transform, false);
            inner.transform.localPosition = new Vector3(0f, SAND_TOP_Y + 0.005f, 0f);
            inner.transform.localScale = new Vector3(5f, 0.01f, 5f);
            var irend = inner.GetComponent<MeshRenderer>();
            if (irend != null)
                irend.material = new Material(shader) { name = "ArenaPit_DuelInnerMat", color = new Color(0.76f, 0.68f, 0.52f) };
            DestroyCollider(inner);

            for (int s = -1; s <= 1; s += 2)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = $"ArenaPit_StartLine_{(s < 0 ? "W" : "E")}";
                line.transform.SetParent(room.transform, false);
                line.transform.localPosition = new Vector3(5f * s, SAND_TOP_Y + 0.01f, 0f);
                line.transform.localScale = new Vector3(0.15f, 0.02f, 2.4f);
                var lrend = line.GetComponent<MeshRenderer>();
                if (lrend != null)
                    lrend.material = new Material(shader) { name = "ArenaPit_StartLineMat", color = new Color(0.85f, 0.80f, 0.70f) };
                DestroyCollider(line);
            }
        }

        /// <summary>남측 왕가 박스 — 설계도 하단 중앙 작은 돌출부(저상 platform + 기둥 2). 반환: 캡 수.</summary>
        private static int BuildRoyalBox(GameObject room, Material wallMat, Shader shader)
        {
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "ArenaPit_RoyalBox";
            platform.transform.SetParent(room.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.35f, -(PIT_OUTER_B + 1.5f));
            platform.transform.localScale = new Vector3(6f, 0.7f, 4.5f);
            var rend = platform.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                // [125차 후속24 A1] 왕가 박스 = 회반죽 파일(금빛 틴트) 팩토리. 폴백: 기존 단색.
                rend.material = ArenaOutdoorMaterialFactory.CreateRoyalBox()
                    ?? new Material(shader) { name = "ArenaPit_RoyalBoxMat", color = new Color(0.58f, 0.50f, 0.40f) };
            }

            // 왕가 박스 톱 캡(렌더러 전용 — 캡 계약)
            IndoorQualityLayers.AddBoxCap(room, "ArenaPit_RoyalBox_TopCap",
                new Vector3(0f, 0.78f, -(PIT_OUTER_B + 1.5f)), new Vector3(6f, 0.15f, 4.5f));
            return 1;
        }

        /// <summary>출구 트리거 — 북측 중앙(θ=90°) 내면 문 박스 + BuildingTrigger "Exit". E키 → 복귀.</summary>
        private static void AddExitTrigger(GameObject room, Material wallMat)
        {
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "ArenaPit_ExitDoor";
            door.transform.SetParent(room.transform, false);
            door.transform.localPosition = new Vector3(0f, 1.5f, PIT_OUTER_B - 1.2f);
            door.transform.localScale = new Vector3(2f, 3f, 0.4f);
            var rend = door.GetComponent<MeshRenderer>();
            if (rend != null) rend.material = wallMat;
            var trigger = door.AddComponent<BuildingTrigger>();
            trigger.BuildingType = "Exit";
            Debug.Log("[ArenaPitInteriorBuilder] 출구 트리거 배치 — 월드 복귀");
        }

        private static void TryLoadPitGlb(GameObject room, string nation, string nationStyle)
        {
            string glbKey = ArenaPitGlbKey(nationStyle);
            if (RuntimeModelLoader.TryGetModel(glbKey, out var modelPrefab))
            {
                GameObject inst = Object.Instantiate(modelPrefab, room.transform, false);
                inst.name = "ArenaPit_Model";
                inst.transform.localPosition = new Vector3(0f, 0f, 0f);
                Debug.Log($"[ArenaPitInteriorBuilder] 투기장 GLB 로드: '{glbKey}' → 중앙 배치");
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
            // [125차 후속24 A1] 국가 절차 텍스처(국가색 유지) + 석재 노멀 relief 결합(성 내부 HQ 계약 이식).
            // 팩토리 null(셰이더 부재 등) 시 기존 단일 텍스처 경로 폴백.
            return ArenaOutdoorMaterialFactory.CreateWall(tex, nationStyle)
                ?? new Material(shader) { name = $"ArenaPit_WallMat_{nationStyle}", mainTexture = tex };
        }

        // ── 타원 기하 헬퍼 ────────────────────────────────────────────────
        private static Vector3 EllipsePoint(float deg, float a, float b)
        {
            float rad = deg * Mathf.Deg2Rad;
            return new Vector3(a * Mathf.Cos(rad), 0f, b * Mathf.Sin(rad));
        }

        private static Vector3 EllipseTangent(float deg, float a, float b)
        {
            float rad = deg * Mathf.Deg2Rad;
            Vector3 t = new Vector3(-a * Mathf.Sin(rad), 0f, b * Mathf.Cos(rad));
            return t.normalized;
        }

        private static bool IsGateAngle(float deg)
        {
            foreach (float gate in GateAngles)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(deg, gate)) <= GATE_HALF_ANGLE_DEG) return true;
            }
            return false;
        }

        /// <summary>장식 프리미티브 콜라이더 제거(비충돌 데칼 계약).</summary>
        private static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
    }
}
