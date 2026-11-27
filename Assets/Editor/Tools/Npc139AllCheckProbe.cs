using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [139차 P0] 전수 확인 배치 프로브 — 수리 금지(측정 전용). 단일 진입 RunAll().
/// 배경: 138차 패치 후 배치 프로브상 TalkA/Walk ±180 도달(정면)이나 사용자 실재 Play에서
///   "NPC 정면 안 봄" 재보고 → 인게임 실경로와 프로브 합성 씬 차이 의심(직교 탑다운/오버라이드
///   인스턴스/앰비언트 트리거 경로). + 피부 FBX vs GLB 대조 + 키 1.8m 계약 전수 실측.
/// 시퀀스:
///  1. NPC 3종(girl1/boy1/man2) 스폰 — 실경로 조립(RuntimeModelLoader → Instantiate →
///     EnsureNpcTexture(리플렉션·private) → EnsureVillageNpcHeight → EnsureVillageNpcAnimation →
///     NPCAmbientDialogue 부착 → VillagerWanderer(Init/SetWalkModel)).
///  2. 인게임 대화 재현 — 플레이어 서로게이트 2.92m + DialogueCameraDirector.Begin(NPCAmbientDialogue, 이름)
///     실경로 오버로드 → 대화 카메라 스크린샷 + 카메라/NPC/클립 덤프 + 직교 카메라 변형 1회.
///  3. 배회/달리기 렌더 — Walk/Run 상태 크로스페이드 front컷 + hips−root 덤프.
///  4. 키 실측 — NPC 3종 + 병사(SpawnDeployedGuard) + 플레이어 + 동굴 내부 NPC(TerritoryBuilder.
///     MeasureStandingHeight — 1.8m 계약 이탈 여부 전수).
///  5. 피부 대조 — FBX 근접컷(얼굴 0.9m/상체 1.6m/nearClip 0.05 변형) vs GLB NPC(구 GLB
///     NPC_Girl1_Rigged/NPC_Man1_Rigged — RuntimeModelLoader 직키 로드, 실패 시 기록만).
///  6. 텍스처 인벤토리 — Npc2026/Textures 캐릭터별 PNG 경로+해상도+importer maxSize.
/// 산출: TestOutput/npc139_allcheck.txt + TestOutput/npc139_*.png
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod Npc139AllCheckProbe.RunAll -quit
///   (-nographics 미사용 — RT 렌더 캡처 필요, NpcFacing136Probe 선례 준용)
/// </summary>
public static class Npc139AllCheckProbe
{
    private const string OutTxt = "TestOutput/npc139_allcheck.txt";
    private static readonly string[] Keys = { "girl1", "boy1", "man2" };

    /// <summary>[138차] MeshyUser/Idle_02.fbx guid — 패치 클립 사용 여부 판별 기준(136차 프로브 계승).</summary>
    private const string PatchedIdle02Guid = "39a29092f4f554844ada1e013b987a50";

    /// <summary>[139차] 구 GLB NPC 직키(RuntimeModelLoader 무별칭 — Resources 직로드 시도).</summary>
    private static readonly string[] GlbKeys = { "npc_girl1_rigged", "npc_man1_rigged" };

    public static void RunAll()
    {
        var log = new StringBuilder();
        log.AppendLine("[139차 프로브] RunAll 시작 — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        EnsureDir();

        // ── 조명/환경(배치모드 렌더 최소 셋업 — NpcFacing136Probe 선례) ──
        var sunGo = new GameObject("ProbeSun139");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sunGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f, 1f);

        // ── 대화 카메라가 직접 제어할 Camera.main(스왑 폐기 계약 — Begin은 기존 카메라를 이동) ──
        var camGo = new GameObject("ProbeMainCam139");
        var mainCam = camGo.AddComponent<Camera>();
        mainCam.tag = "MainCamera";
        mainCam.enabled = true;
        mainCam.fieldOfView = 40f;
        mainCam.nearClipPlane = 0.3f;
        mainCam.farClipPlane = 500f;
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        camGo.transform.position = new Vector3(0f, 1.5f, 5f);
        camGo.transform.LookAt(Vector3.zero);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "ProbeGround139";
        ground.transform.position = new Vector3(40f, 0f, 0f);
        ground.transform.localScale = new Vector3(8f, 1f, 8f);
        var groundMat = new Material(Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse"));
        groundMat.color = new Color(0.75f, 0.75f, 0.7f, 1f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        var rt = new RenderTexture(960, 540, 24);
        var probeCamGo = new GameObject("ProbeCam139");
        var probeCam = probeCamGo.AddComponent<Camera>();
        probeCam.fieldOfView = 40f;
        probeCam.nearClipPlane = 0.15f;
        probeCam.farClipPlane = 500f;
        probeCam.clearFlags = CameraClearFlags.SolidColor;
        probeCam.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        probeCam.targetTexture = rt;

        // ── ① NPC 3종 스폰(실경로 조립 — SpawnNpc 순서 준용) ──
        var npcs = new GameObject[Keys.Length];
        var animators = new Animator[Keys.Length];
        var smrs = new SkinnedMeshRenderer[Keys.Length];
        var ambient = new NPCAmbientDialogue[Keys.Length];
        for (int i = 0; i < Keys.Length; i++)
        {
            string key = Keys[i];
            if (!RuntimeModelLoader.TryGetResourceKey(key, out var resKey) ||
                !RuntimeModelLoader.TryGetModel(resKey, out var prefab) || prefab == null)
            {
                log.AppendLine($"[139차 스폰] {key}(→{resKey ?? "null"}) 프리팹 로드 실패 — SKIP");
                continue;
            }
            var npc = UnityEngine.Object.Instantiate(prefab);
            npc.name = key + "_P139";
            npc.transform.position = new Vector3(i * 6f, 0f, 0f);
            npc.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            // [134차 A] 텍스처 자가 부착 — private static, 136차 선례 리플렉션
            var ensureTex = typeof(VillageNpcSpawner).GetMethod("EnsureNpcTexture",
                BindingFlags.NonPublic | BindingFlags.Static);
            ensureTex?.Invoke(null, new object[] { npc, key });

            VillageNpcSpawner.EnsureVillageNpcHeight(npc, 0f, key + "_p139");
            if (npc.GetComponent<NPCAmbientDialogue>() == null)
                npc.AddComponent<NPCAmbientDialogue>();
            ambient[i] = npc.GetComponent<NPCAmbientDialogue>();
            VillageNpcSpawner.EnsureVillageNpcAnimation(npc, key, key + "_p139");

            // [99차 F-D] 스폰 후 VillagerWanderer 부착(실경로) — Init/EditMode 예외 방어(기록만)
            try
            {
                var wanderer = npc.AddComponent<VillagerWanderer>();
                wanderer.Init(new Vector3(i * 6f, 0f, 0f), 10f, 9130 + i);
                wanderer.SetWalkModel(key);
                log.AppendLine($"[139차 스폰] {key}: Wanderer 부착+Init ok");
            }
            catch (Exception e)
            {
                log.AppendLine($"[139차 스폰] {key}: Wanderer 부착 실패(기록만) {e.GetType().Name}: {e.Message}");
            }

            var anim = npc.GetComponentInChildren<Animator>(true);
            if (anim == null) anim = npc.GetComponent<Animator>();
            if (anim != null) anim.enabled = true;
            npcs[i] = npc;
            animators[i] = anim;
            smrs[i] = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            log.AppendLine($"[139차 스폰] {key}: ok animator={(anim != null ? anim.name : "null")} " +
                            $"smr={smrs[i] != null} ambient={ambient[i] != null} scale={npc.transform.localScale.x:F3}");
        }

        // ── ② 인게임 대화 재현(실경로 오버로드) ──
        for (int i = 0; i < Keys.Length; i++)
        {
            var npc = npcs[i];
            if (npc == null || ambient[i] == null) continue;
            string key = Keys[i];
            var anim = animators[i];
            var smr = smrs[i];
            Transform hips = smr != null ? smr.rootBone : null;

            log.AppendLine();
            log.AppendLine($"===== ②인게임대화 [{key}] rootPos={V3(npc.transform.position)} =====");

            // 플레이어 서로게이트 2.92m(실경로 대화 거리)
            var playerGo = new GameObject("ProbePlayer139_" + key);
            playerGo.tag = "Player";
            playerGo.transform.position = npc.transform.position + npc.transform.forward * 2.92f + Vector3.up * 0.05f;
            Vector3 toPlayer = (playerGo.transform.position - npc.transform.position).normalized;
            toPlayer.y = 0f;

            float rootYawBefore = npc.transform.eulerAngles.y;
            // 실경로: Begin(NPCAmbientDialogue, 이름) — [92차 결함3] fullBody:true 위임 경로
            DialogueCameraDirector.Begin(ambient[i], key + "_p139");

            // 수 프레임 재현(에디터 배치 = 수동 평가) + Talk 트리거(앰비언트 대화 발화 경로)
            if (anim != null)
            {
                try { anim.Update(0.2f); } catch { }
                try { anim.SetTrigger("Talk"); anim.Update(0.35f); } catch { }
            }

            float rootYaw = npc.transform.eulerAngles.y;
            float hipsYaw = hips != null ? hips.rotation.eulerAngles.y : float.NaN;
            float dotFwd = Vector3.Dot(npc.transform.forward, toPlayer);   // +1=정면(플레이어를 향함)
            var cam = mainCam;
            log.AppendLine($"  rootYaw 전={rootYawBefore:F1} → 후={rootYaw:F1} hipsWorldYaw={(hips != null ? hipsYaw.ToString("F1") : "n/a")} " +
                            $"hips-rootDelta={(hips != null ? Mathf.DeltaAngle(rootYaw, hipsYaw).ToString("F1") : "n/a")}");
            log.AppendLine($"  dot(fwd,toPlayer)={dotFwd:F3} (+1=정면/−1=등) DialogueYawControlled={VillagerWanderer.DialogueYawControlled}");
            float camDist = Vector3.Distance(cam.transform.position, npc.transform.position);
            log.AppendLine($"  카메라 ortho={cam.orthographic} orthoSize={cam.orthographicSize:F2} fov={cam.fieldOfView:F1} " +
                            $"nearClip={cam.nearClipPlane:F3} pos={V3(cam.transform.position)} dist={camDist:F2}");
            DumpCurrentClips(anim, hips, log, "대화중");

            // 대화 카메라 시점 스크린샷 — Begin이 배치한 카메라 그대로
            CaptureFromCamera(mainCam, rt, $"npc139_ingame_dialog_{key}");
            log.AppendLine($"  npc139_ingame_dialog_{key}.png 캡처 완료");

            // [125차 후속10] 직교 카메라 변형 — 사용자 의심(직교 탑다운 경로) 검증: 같은 Begin을 직교 카메라로 재진입
            try
            {
                DialogueCameraDirector.End();
                cam.orthographic = true;
                cam.orthographicSize = 5f;   // 탑다운 광역값에서 Begin 진입(프레이밍은 Director가 1.06m로 재설정)
                DialogueCameraDirector.Begin(ambient[i], key + "_p139");
                if (anim != null) { try { anim.SetTrigger("Talk"); anim.Update(0.35f); } catch { } }
                float camDistO = Vector3.Distance(cam.transform.position, npc.transform.position);
                float hipsYawO = hips != null ? hips.rotation.eulerAngles.y : float.NaN;
                log.AppendLine($"  [직교변형] ortho={cam.orthographic} orthoSize={cam.orthographicSize:F2} " +
                                $"pos={V3(cam.transform.position)} dist={camDistO:F2} rootYaw={npc.transform.eulerAngles.y:F1} " +
                                $"hips-rootDelta={(hips != null ? Mathf.DeltaAngle(npc.transform.eulerAngles.y, hipsYawO).ToString("F1") : "n/a")}");
                CaptureFromCamera(mainCam, rt, $"npc139_ingame_dialog_{key}_ortho");
                log.AppendLine($"  npc139_ingame_dialog_{key}_ortho.png 캡처 완료");
            }
            catch (Exception e)
            {
                log.AppendLine($"  [직교변형] 실패(기록만) {e.GetType().Name}: {e.Message}");
            }
            DialogueCameraDirector.End();
            cam.orthographic = false;   // 원복(직교 변형 잔존 방지)
            UnityEngine.Object.DestroyImmediate(playerGo);
        }

        // ── ③ 배회/달리기 렌더(루트 forward측 카메라 — 138차 front컷 선례) ──
        log.AppendLine();
        log.AppendLine("[139차 ③] 배회/달리기 렌더 — Speed=2 홀드 + 크로스페이드(138차-v3 Walk 유지 조건 준용)");
        for (int i = 0; i < Keys.Length; i++)
        {
            var npc = npcs[i];
            if (npc == null) continue;
            string key = Keys[i];
            var anim = animators[i];
            var smr = smrs[i];
            Transform hips = smr != null ? smr.rootBone : null;
            if (anim == null) continue;

            foreach (var state in new[] { "Walk", "Run" })
            {
                try
                {
                    anim.SetFloat("Speed", 2f);
                    anim.CrossFade(state, 0.2f, 0);
                    anim.Update(1.5f);   // 초기 트랜지언트 스킵(138차 선례)
                }
                catch (Exception e)
                {
                    log.AppendLine($"  [{key}] {state}: 재생실패 {e.Message}");
                    continue;
                }
                float hipsDelta = hips != null ? Mathf.DeltaAngle(npc.transform.eulerAngles.y, hips.rotation.eulerAngles.y) : float.NaN;
                var clipInfos = anim.GetCurrentAnimatorClipInfo(0);
                string clips = clipInfos != null && clipInfos.Length > 0
                    ? string.Join(",", clipInfos.Select(c => c.clip != null ? c.clip.name : "null")) : "없음";
                log.AppendLine($"  [{key}] {state}: hips-rootDelta={hipsDelta:F1} 재생클립=\"{clips}\"");
                Vector3 camPos = npc.transform.position + npc.transform.forward * 2.4f + Vector3.up * 0.5f;
                Vector3 lookAt = npc.transform.position + Vector3.up * 0.95f;
                CaptureFromCamera(probeCam, rt, $"npc139_{state.ToLower()}_{key}", camPos, lookAt);
                log.AppendLine($"  [{key}] {state} front컷 캡처 완료 — npc139_{state.ToLower()}_{key}.png (루트 forward측 2.4m)");
            }
        }

        // ── ④ FBX 근접 피부 컷 — 얼굴 0.9m/상체 1.6m + nearClip 0.05 변형 ──
        // [136차 확정] 메시 forward 반대 렌더 → [138차 패치 후] 목표 face==root(+forward). 양측 컷 병행해 판독 위임.
        log.AppendLine();
        log.AppendLine("[139차 ④] FBX 근접컷 — 얼굴 0.9m(+fwd/−fwd 병행)/상체 1.6m/nearClip 0.05 변형");
        for (int i = 0; i < Keys.Length; i++)
        {
            var npc = npcs[i];
            if (npc == null) continue;
            string key = Keys[i];
            var anim = animators[i];
            var smr = smrs[i];
            if (anim != null)
            {
                try { anim.SetFloat("Speed", 0f); anim.Play("Idle", 0, 0f); anim.Update(0.2f); } catch { }
            }
            Transform headBone = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 headCenter = headBone != null ? headBone.position : npc.transform.position + Vector3.up * 1.5f;
            Vector3 fwd = npc.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 pos = npc.transform.position;

            // 얼굴 0.9m — +forward(138차 패치 목표 방향) / −forward(136차 확정 구 방향) 병행
            Vector3 fFwd = new Vector3(pos.x + fwd.x * 0.9f, headCenter.y, pos.z + fwd.z * 0.9f);
            CaptureFromCamera(probeCam, rt, $"npc139_face09_{key}", fFwd, headCenter);
            Vector3 fBwd = new Vector3(pos.x - fwd.x * 0.9f, headCenter.y, pos.z - fwd.z * 0.9f);
            CaptureFromCamera(probeCam, rt, $"npc139_face09_bwd_{key}", fBwd, headCenter);
            log.AppendLine($"  [{key}] face09(+fwd/-fwd) 캡처 완료 headCenter={V3(headCenter)} headBone={(headBone != null ? headBone.name : "null→고정1.5m")}");

            // 상체 1.6m(+forward)
            Vector3 up16 = new Vector3(pos.x + fwd.x * 1.6f, pos.y + 1.15f, pos.z + fwd.z * 1.6f);
            CaptureFromCamera(probeCam, rt, $"npc139_upper16_{key}", up16, pos + Vector3.up * 1.15f);
            log.AppendLine($"  [{key}] upper16 캡처 완료 — camPos={V3(up16)} lookAt={V3(pos + Vector3.up * 1.15f)}");

            // nearClip 0.05 변형(얼굴 0.9m +fwd) — 근평면 절단 영향 배제 검증. 원복 필수.
            float savedNear = probeCam.nearClipPlane;
            probeCam.nearClipPlane = 0.05f;
            if (smr != null)
            {
                float dN, projExtN, frontN;
                NearPlaneMath(fFwd, (headCenter - fFwd).normalized, smr.bounds, out dN, out projExtN, out frontN);
                float marginN = frontN - 0.05f;
                log.AppendLine($"  [{key}] face09_nearfix 산술: distCenter={dN:F3} projExtents={projExtN:F3} frontFaceDist={frontN:F3} margin={marginN:F3} → {(marginN < 0 ? "침입" : "침입 없음")}");
            }
            CaptureFromCamera(probeCam, rt, $"npc139_face09_nearfix_{key}", fFwd, headCenter);
            probeCam.nearClipPlane = savedNear;   // 원복
            log.AppendLine($"  [{key}] face09_nearfix 캡처 완료 — nearClip 원복={savedNear:F3}");
        }

        // ── ⑤ 키 실측(1.8m 계약 전수) ──
        log.AppendLine();
        log.AppendLine("[139차 ⑤] npc139_heights — MeasureStandingHeight 전수(측정 전용 — 스케일/수리 없음)");
        for (int i = 0; i < Keys.Length; i++)
        {
            var npc = npcs[i];
            if (npc == null) continue;
            string key = Keys[i];
            float h = TerritoryBuilder.MeasureStandingHeight(npc, false);
            log.AppendLine($"  [키] {key}_p139: h={h:F3}m scale={npc.transform.localScale.x:F4} " +
                            $"캘리브레이션=EnsureVillageNpcHeight 적용됨(스폰 경로) 대상1.80m");
        }
        // 병사 — 실경로 SpawnDeployedGuard(Test_19 선례 준용). 내부 NormalizeSoldierHeight(1.8m) 적용 경로.
        GameObject soldier = null;
        try
        {
            soldier = TerritoryBuilder.SpawnDeployedGuard("T139Soldier", 5, NationType.West, new Vector3(46f, 0f, 4f));
            if (soldier != null)
            {
                float hs = TerritoryBuilder.MeasureStandingHeight(soldier, false);
                log.AppendLine($"  [키] 병사 T139Soldier: h={hs:F3}m 캘리브레이션=CreateGuard→NormalizeSoldierHeight 내부 적용(프로브 추가 캘리브레이션 없음) 대상1.80m");
            }
            else log.AppendLine("  [키] 병사 스폰 실패(null)");
        }
        catch (Exception e) { log.AppendLine($"  [키] 병사 스폰 예외 {e.GetType().Name}: {e.Message}"); }
        // 플레이어 — 실모델(Player_Rigged) 인스턴스. 프로브는 캘리브레이션 미적용(원시 실측).
        try
        {
            if (RuntimeModelLoader.TryGetModel("player", out var playerPrefab) && playerPrefab != null)
            {
                var player = UnityEngine.Object.Instantiate(playerPrefab);
                player.name = "T139Player";
                player.transform.position = new Vector3(48f, 0f, 4f);
                float hp = TerritoryBuilder.MeasureStandingHeight(player, false);   // [54차] 플레이어=월드 bounds 계열
                log.AppendLine($"  [키] 플레이어 Player_Rigged: h={hp:F3}m 캘리브레이션=미적용(프로브 원시 실측) 대상1.80m");
                UnityEngine.Object.DestroyImmediate(player);
            }
            else log.AppendLine("  [키] 플레이어 프리팹 로드 실패");
        }
        catch (Exception e) { log.AppendLine($"  [키] 플레이어 예외 {e.GetType().Name}: {e.Message}"); }
        // 동굴 내부 NPC — CaveInteriorBuilder 룸 안 girl1 스폰(동굴 바닥 y 기준)
        try
        {
            var cave = CaveInteriorBuilder.BuildCaveInterior("t139", 1);
            log.AppendLine($"  [동굴] BuildCaveInterior(\"t139\",1) ok room={cave.name} pos={V3(cave.transform.position)}");
            if (RuntimeModelLoader.TryGetResourceKey("girl1", out var caveResKey) &&
                RuntimeModelLoader.TryGetModel(caveResKey, out var cavePrefab) && cavePrefab != null)
            {
                var caveNpc = UnityEngine.Object.Instantiate(cavePrefab);
                caveNpc.name = "girl1_CaveP139";
                caveNpc.transform.position = new Vector3(0f, 0.05f, 1.0f);   // 동굴 바닥 y=0 기준(바닥 큐브 상단)
                var ensureTexC = typeof(VillageNpcSpawner).GetMethod("EnsureNpcTexture", BindingFlags.NonPublic | BindingFlags.Static);
                ensureTexC?.Invoke(null, new object[] { caveNpc, "girl1" });
                VillageNpcSpawner.EnsureVillageNpcHeight(caveNpc, 0f, "girl1_cave_p139");
                VillageNpcSpawner.EnsureVillageNpcAnimation(caveNpc, "girl1", "girl1_cave_p139");
                float hc = TerritoryBuilder.MeasureStandingHeight(caveNpc, false);
                log.AppendLine($"  [키] 동굴 girl1: h={hc:F3}m pos={V3(caveNpc.transform.position)} 캘리브레이션=EnsureVillageNpcHeight 적용됨(groundY=0) 대상1.80m");
                Vector3 caveCamPos = new Vector3(0f, 1.6f, 4.5f);
                CaptureFromCamera(probeCam, rt, "npc139_cave_girl1", caveCamPos, caveNpc.transform.position + Vector3.up * 1.0f);
                log.AppendLine("  npc139_cave_girl1.png 캡처 완료");
                UnityEngine.Object.DestroyImmediate(caveNpc);
            }
            else log.AppendLine("  [동굴] girl1 프리팹 로드 실패");
            UnityEngine.Object.DestroyImmediate(cave);
        }
        catch (Exception e) { log.AppendLine($"  [동굴] 예외 {e.GetType().Name}: {e.Message}"); }

        // ── ⑥ GLB NPC 대조(구 GLB NPC_*_Rigged — 직키 로드 시도, 에셋 이동/복사 없음) ──
        log.AppendLine();
        log.AppendLine("[139차 ⑥] GLB NPC 대조 — 구 GLB(NPC_Girl1_Rigged/NPC_Man1_Rigged) 직키 로드 시도");
        foreach (var glbKey in GlbKeys)
        {
            try
            {
                bool has = RuntimeModelLoader.TryGetModel(glbKey, out var glbPrefab) && glbPrefab != null;
                if (!has)
                {
                    log.AppendLine($"  [GLB] {glbKey}: RuntimeModelLoader 로드 실패 — 기록만(에셋 조작 없음)");
                    continue;
                }
                var glbNpc = UnityEngine.Object.Instantiate(glbPrefab);
                glbNpc.name = glbKey + "_P139";
                glbNpc.transform.position = new Vector3(56f, 0f, 0f);
                glbNpc.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                VillageNpcSpawner.EnsureVillageNpcAnimation(glbNpc, glbKey, glbKey + "_p139");
                var glbAnim = glbNpc.GetComponentInChildren<Animator>(true);
                var glbSmr = glbNpc.GetComponentInChildren<SkinnedMeshRenderer>(true);
                float hG = TerritoryBuilder.MeasureStandingHeight(glbNpc, false);
                log.AppendLine($"  [GLB] {glbKey}: 스폰 ok h={hG:F3}m animator={(glbAnim != null ? glbAnim.name : "null")} smr={glbSmr != null}");
                bool played = false;
                if (glbAnim != null)
                {
                    try
                    {
                        glbAnim.Play("Idle", 0, 0f);
                        glbAnim.Update(0.3f);
                        var ci = glbAnim.GetCurrentAnimatorClipInfo(0);
                        string cn = ci != null && ci.Length > 0 && ci[0].clip != null ? ci[0].clip.name : "null";
                        played = ci != null && ci.Length > 0 && ci[0].clip != null;
                        log.AppendLine($"  [GLB] {glbKey}: Idle 재생 {(played ? "성공 클립=\"" + cn + "\"" : "실패(클립정보 없음)")}");
                    }
                    catch (Exception e2) { log.AppendLine($"  [GLB] {glbKey}: Idle 재생 예외 {e2.Message}"); }
                }
                Vector3 gHead = glbAnim != null && glbAnim.GetBoneTransform(HumanBodyBones.Head) != null
                    ? glbAnim.GetBoneTransform(HumanBodyBones.Head).position : glbNpc.transform.position + Vector3.up * 1.5f;
                Vector3 gFwd = glbNpc.transform.forward; gFwd.y = 0f;
                if (gFwd.sqrMagnitude < 0.0001f) gFwd = Vector3.forward;
                gFwd.Normalize();
                Vector3 gp = glbNpc.transform.position;
                CaptureFromCamera(probeCam, rt, $"npc139_glb_{glbKey}", new Vector3(gp.x + gFwd.x * 0.9f, gHead.y, gp.z + gFwd.z * 0.9f), gHead);
                CaptureFromCamera(probeCam, rt, $"npc139_glb_upper16_{glbKey}", new Vector3(gp.x + gFwd.x * 1.6f, gp.y + 1.15f, gp.z + gFwd.z * 1.6f), gp + Vector3.up * 1.15f);
                log.AppendLine($"  [GLB] {glbKey}: npc139_glb_{glbKey}.png / npc139_glb_upper16_{glbKey}.png 캡처 완료(+forward측)");
                UnityEngine.Object.DestroyImmediate(glbNpc);
            }
            catch (Exception e)
            {
                log.AppendLine($"  [GLB] {glbKey}: 예외 {e.GetType().Name}: {e.Message}");
            }
        }

        // ── ⑦ 텍스처 인벤토리 ──
        log.AppendLine();
        log.AppendLine("[139차 ⑦] 텍스처 인벤토리 — Npc2026/Textures 캐릭터별 PNG(경로/해상도/importer maxSize)");
        DumpTextureInventory(log);

        log.AppendLine();
        log.AppendLine("[139차 프로브] DONE — 판정/수리 없음(측정 전용). PNG는 감독자 vision 판독 대기.");
        File.WriteAllText(OutTxt, log.ToString());
        Debug.Log($"[139차 프로브] DONE — {OutTxt}");

        probeCam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(probeCamGo);
        UnityEngine.Object.DestroyImmediate(camGo);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(ground);
        UnityEngine.Object.DestroyImmediate(sunGo);
        for (int i = 0; i < npcs.Length; i++)
            if (npcs[i] != null) UnityEngine.Object.DestroyImmediate(npcs[i]);
        if (soldier != null) UnityEngine.Object.DestroyImmediate(soldier);
    }

    /// <summary>대화/상태 중 현재 재생 클립 덤프 — GetCurrentAnimatorClipInfo + [138차] 패치 클립(Idle_02 guid) 일치 판별.</summary>
    private static void DumpCurrentClips(Animator anim, Transform hips, StringBuilder log, string label)
    {
        if (anim == null) { log.AppendLine($"  [{label}] animator=null"); return; }
        try
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            var infos = anim.GetCurrentAnimatorClipInfo(0);
            if (infos == null || infos.Length == 0)
            {
                log.AppendLine($"  [{label}] stateHash={st.shortNameHash} 재생클립=없음");
                return;
            }
            foreach (var ci in infos)
            {
                var clip = ci.clip;
                if (clip == null) { log.AppendLine($"  [{label}] 재생클립=null"); continue; }
                string p = AssetDatabase.GetAssetPath(clip);
                string guid = string.IsNullOrEmpty(p) ? "n/a" : AssetDatabase.AssetPathToGUID(p);
                bool patched = guid == PatchedIdle02Guid;
                log.AppendLine($"  [{label}] stateHash={st.shortNameHash} normalizedTime={st.normalizedTime:F2} " +
                                $"재생클립=\"{clip.name}\" path={p} guid={guid} [138차]패치클립일치={patched}");
            }
        }
        catch (Exception e)
        {
            log.AppendLine($"  [{label}] 클립 덤프 예외 {e.Message}");
        }
    }

    /// <summary>[139차 ⑦] 캐릭터별 추출 텍스처 PNG 인벤토리 — 파일 glob + Texture2D 실측 해상도 + importer maxSize.</summary>
    private static void DumpTextureInventory(StringBuilder log)
    {
        string root = "Assets/Resources/Models/UserProvided/Npc2026/Textures";
        if (!Directory.Exists(root))
        {
            log.AppendLine("  텍스처 루트 부재 — " + root);
            return;
        }
        foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d))
        {
            string charName = Path.GetFileName(dir);
            var pngs = Directory.GetFiles(dir, "*.png").OrderBy(f => f).ToArray();
            log.AppendLine($"  [{charName}] PNG {pngs.Length}장");
            foreach (var png in pngs)
            {
                string assetPath = png.Replace('\\', '/');
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                string res = tex != null ? $"{tex.width}x{tex.height}" : "로드실패";
                log.AppendLine($"    {Path.GetFileName(png)} res={res} maxSize={(importer != null ? importer.maxTextureSize.ToString() : "n/a")} path={assetPath}");
            }
        }
    }

    /// <summary>nearClip 침입 산술 — 136차 선례 계승(distCenter − projExtents vs nearClip).</summary>
    private static void NearPlaneMath(Vector3 camPos, Vector3 camFwd, Bounds b, out float d, out float projExt, out float front)
    {
        Vector3 f = camFwd.normalized;
        d = Vector3.Dot(b.center - camPos, f);
        projExt = Mathf.Abs(b.extents.x * f.x) + Mathf.Abs(b.extents.y * f.y) + Mathf.Abs(b.extents.z * f.z);
        front = d - projExt;
    }

    /// <summary>RT 렌더 + ReadPixels + WriteAllBytes — 136차 CaptureFromCamera 선례 계승.</summary>
    private static void CaptureFromCamera(Camera cam, RenderTexture rt, string name, Vector3? pos = null, Vector3? lookAt = null)
    {
        bool restoreTarget = cam.targetTexture != rt;
        if (pos.HasValue) cam.transform.position = pos.Value;
        if (lookAt.HasValue) cam.transform.LookAt(lookAt.Value);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes($"TestOutput/{name}.png", tex.EncodeToPNG());
        RenderTexture.active = null;
        if (restoreTarget) cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void EnsureDir()
    {
        if (!Directory.Exists("TestOutput")) Directory.CreateDirectory("TestOutput");
    }

    private static string V3(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";
}
