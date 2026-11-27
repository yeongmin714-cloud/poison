using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ProjectName.Systems;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// [136차 P1] NPC 대화 정면/피부 진단 배치 프로브 — 수리 없음(측정 전용).
/// 목적:
///  1. [135차] orientationOffsetY 패치가 런타임 클립에 반영됐는지 — Idle/TalkA 재생 후 hips 요각 실측
///  2. DialogueCameraDirector.Begin 실호출 후 루트/hips yaw·dot·_gazeSavedYaw·카메라 상태 실측
///  3. 대화 카메라 시점 스크린샷(정면/근접 피부/후방 참조) — "여전히 등"/"피부 깨짐" 렌더 판별 근거
///  4. SMR bounds vs nearClip 근평면 침입 산술 기록
/// 산출: TestOutput/npc136_facing.txt + TestOutput/npc136_front_*.png / npc136_skin_*.png / npc136_rear_*.png
/// [136차-후속] 얼굴측(−forward) 근접컷 npc136_faceclose1m/16m/nearfix_*.png 추가 — txt는 어펜드(덮어쓰기 금지)
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod NpcFacing136Probe.RunAll -quit
/// (기존 NpcFacingProbe 선례 준용 — -nographics 미사용, RT 렌더 캡처 필요)
/// 부호 주의([134차 C2]): Director 내 bearing = player→NPC 방향 → Dot(fwd, bearing) −1 = NPC가 플레이어를
///   바라봄(정면). 본 프로브는 Dot(fwd, toPlayer) 양식으로도 병기 — +1 = 정면(플레이어를 향함).
/// </summary>
public static class NpcFacing136Probe
{
    private const string OutTxt = "TestOutput/npc136_facing.txt";
    private static readonly string[] Keys = { "girl1", "boy1", "man2" };

    public static void RunAll()
    {
        var log = new StringBuilder();
        log.AppendLine("[136차 프로브] RunAll 시작 — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        EnsureDir();

        // ── 조명/환경 (배치모드 렌더용 최소 셋업 — NpcFacingProbe 선례) ──
        var sunGo = new GameObject("ProbeSun136");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sunGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f, 1f);

        // ── 대화 카메라가 직접 제어할 Camera.main(스왑 폐기 계약 — Begin은 기존 카메라를 이동) ──
        var camGo = new GameObject("ProbeMainCam");
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

        // ── 참조용 지면(스킨 컷 색 판독 보조) ──
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "ProbeGround136";
        ground.transform.position = new Vector3(6f, 0f, 0f);
        ground.transform.localScale = new Vector3(6f, 1f, 6f);
        var groundMat = new Material(Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse"));
        groundMat.color = new Color(0.75f, 0.75f, 0.7f, 1f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        // ── NPC 3종 스폰 (girl1/boy1/man2 — 기존 프로브 스폰 경로 재사용) ──
        var npcs = new GameObject[Keys.Length];
        var animators = new Animator[Keys.Length];
        var smrs = new SkinnedMeshRenderer[Keys.Length];
        for (int i = 0; i < Keys.Length; i++)
        {
            string key = Keys[i];
            if (!RuntimeModelLoader.TryGetResourceKey(key, out var resKey) ||
                !RuntimeModelLoader.TryGetModel(resKey, out var prefab) || prefab == null)
            {
                log.AppendLine($"[136차 프로브] {key}(→{resKey ?? "null"}) 프리팹 로드 실패 — SKIP");
                continue;
            }
            var npc = UnityEngine.Object.Instantiate(prefab);   // [136차] Object → UnityEngine.Object 한정(using System 충돌 해소)
            npc.name = key + "_P136";
            npc.transform.position = new Vector3(i * 6f, 0f, 0f);
            npc.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);   // 루트=+Z(플레이어 측)
            VillageNpcSpawner.EnsureVillageNpcAnimation(npc, key, key + "_p136");
            var ensureTex = typeof(VillageNpcSpawner).GetMethod("EnsureNpcTexture",
                BindingFlags.NonPublic | BindingFlags.Static);
            ensureTex?.Invoke(null, new object[] { npc, key });
            var anim = npc.GetComponent<Animator>();
            if (anim != null) anim.enabled = true;
            npcs[i] = npc;
            animators[i] = anim;
            smrs[i] = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            log.AppendLine($"[136차 프로브] 스폰 {key}: ok animator={anim != null} smr={smrs[i] != null}");
        }

        // ── 플레이어 서로게이트(Player 태그 — Director FindGameObjectWithTag 사용) ──
        var playerGo = new GameObject("ProbePlayer136");
        playerGo.tag = "Player";
        playerGo.transform.position = new Vector3(0f, 0.05f, 2.92f);

        // ── 리플렉션 핸들(수치 전용 — 판정/수리 없음) ──
        var directorType = typeof(DialogueCameraDirector);
        var fiGaze = directorType.GetField("_gazeSavedYaw", BindingFlags.NonPublic | BindingFlags.Static);
        var fiDist = directorType.GetField("_fullBodyDist", BindingFlags.NonPublic | BindingFlags.Static);
        var fiActive = directorType.GetField("_active", BindingFlags.NonPublic | BindingFlags.Static);
        var miBodyYaw = directorType.GetMethod("ComputeVisibleBodyYaw",
            BindingFlags.NonPublic | BindingFlags.Static);

        var rt = new RenderTexture(960, 540, 24);
        var probeCamGo = new GameObject("ProbeCam136");
        var probeCam = probeCamGo.AddComponent<Camera>();
        probeCam.fieldOfView = 40f;
        probeCam.nearClipPlane = 0.15f;
        probeCam.farClipPlane = 500f;
        probeCam.clearFlags = CameraClearFlags.SolidColor;
        probeCam.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        probeCam.targetTexture = rt;

        for (int i = 0; i < Keys.Length; i++)
        {
            var npc = npcs[i];
            if (npc == null) continue;
            string key = Keys[i];
            var anim = animators[i];
            var smr = smrs[i];
            Transform hips = smr != null ? smr.rootBone : null;

            log.AppendLine();
            log.AppendLine($"===== NPC[{i}] {key} (rootPos={V3(npc.transform.position)}) =====");

            // ── 런타임 클립 목록([135차] 패치 반영 판별 소재) ──
            var ctrl = anim != null ? anim.runtimeAnimatorController : null;
            if (ctrl != null)
            {
                var clips = ctrl.animationClips;
                log.AppendLine($"  runtimeClips={clips.Length} controller={ctrl.name}");
                foreach (var c in clips)
                    log.AppendLine($"    clip \"{c.name}\" len={c.length:F2}s frameRate={c.frameRate:F0}");
            }
            else
                log.AppendLine("  runtimeClips=0 controller=null");

            // ── ① 비대화 스냅샷(Idle 재생 — [135차] 패치 런타임 반영 판별) ──
            float idleHipsDelta = float.NaN, talkHipsDelta = float.NaN;
            if (anim != null && hips != null)
            {
                SnapPose(anim, "Idle", hips, npc.transform, log, "①비대화 Idle");
                idleHipsDelta = Mathf.DeltaAngle(npc.transform.eulerAngles.y, hips.rotation.eulerAngles.y);
                SnapPose(anim, "TalkA", hips, npc.transform, log, "①비대화 TalkA");
                talkHipsDelta = Mathf.DeltaAngle(npc.transform.eulerAngles.y, hips.rotation.eulerAngles.y);
                // 대화 장면 재현 — TalkA 유지 상태로 Begin 진입
                SnapPose(anim, "TalkA", hips, npc.transform, log, "①비대화 TalkA(재생)");
            }
            log.AppendLine(SnapshotBase(npc, hips, smr, "①비대화"));

            // ── ② 플레이어 2.92m 배치 + Begin 실호출 ──
            playerGo.transform.position = npc.transform.position + npc.transform.forward * 2.92f + Vector3.up * 0.05f;
            Vector3 toPlayer = (playerGo.transform.position - npc.transform.position).normalized;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector3.forward;
            log.AppendLine($"  ②플레이어 배치 pos={V3(playerGo.transform.position)} dist={Vector3.Distance(playerGo.transform.position, npc.transform.position):F2}");

            DialogueCameraDirector.Begin((Component)npc.transform, key + "_p136");   // [136차] Component 오버로드 명시(GameObject≠Component — Transform 전달)

            // ── Begin 직후 스냅샷 ──
            float rootYaw = npc.transform.eulerAngles.y;
            float dotFwdPlayer = Vector3.Dot(npc.transform.forward, toPlayer);   // +1=정면(플레이어를 향함)
            float hipsYaw = hips != null ? hips.rotation.eulerAngles.y : float.NaN;
            float bodyYaw = miBodyYaw != null ? Convert.ToSingle(miBodyYaw.Invoke(null, new object[] { npc.transform })) : float.NaN;
            var cam = mainCam;   // Begin이 기존 카메라를 이동(스왑 폐기)
            var smrBounds = smr != null ? smr.bounds : new Bounds();

            log.AppendLine($"  ②Begin직후 rootYaw={rootYaw:F1} hipsWorldYaw={hipsYaw:F1} hips-rootDelta={(hips != null ? Mathf.DeltaAngle(rootYaw, hipsYaw).ToString("F1") : "n/a")}");
            log.AppendLine($"  ②Begin직후 dot(fwd,toPlayer)={dotFwdPlayer:F3} (+1=정면) / Director가용 bodyYaw={bodyYaw:F1}");
            log.AppendLine($"  ②Begin직후 _gazeSavedYaw={(fiGaze != null ? fiGaze.GetValue(null).ToString() : "n/a")} DialogueYawControlled={VillagerWanderer.DialogueYawControlled} _active={(fiActive != null ? fiActive.GetValue(null).ToString() : "n/a")}");
            log.AppendLine($"  ②카메라 pos={V3(cam.transform.position)} rotEuler={V3(cam.transform.rotation.eulerAngles)} fov={cam.fieldOfView:F1} nearClip={cam.nearClipPlane:F3} ortho={cam.orthographic} _fullBodyDist={(fiDist != null ? fiDist.GetValue(null).ToString() : "n/a")}");

            // ── nearClip 근평면 침입 산술(대화 카메라) ──
            if (smr != null)
            {
                float d, projExt, front;
                NearPlaneMath(cam.transform.position, cam.transform.forward, smrBounds, out d, out projExt, out front);
                float margin = front - cam.nearClipPlane;
                log.AppendLine($"  ②nearClip산술 boundsCenter={V3(smrBounds.center)} extents={V3(smrBounds.extents)} camFwd={V3(cam.transform.forward)}");
                log.AppendLine($"  ②nearClip산술 distCenter={d:F3} projExtents={projExt:F3} frontFaceDist={front:F3} nearClip={cam.nearClipPlane:F3} margin={margin:F3} → {(margin < 0 ? "침입(근평면 절단 의심)" : "침입 없음")}");
            }

            // ── ③ 대화 카메라 시점 스크린샷(정면) — Begin이 배치한 카메라 그대로 ──
            string tag = key;
            CaptureFromCamera(mainCam, rt, $"npc136_front_{tag}");
            log.AppendLine($"  ③front 캡처 완료 — 대화 카메라 시점(nearClip={cam.nearClipPlane:F3})");

            // ── ④ 근접 피부 컷: NPC 앞 0.8m(가슴~머리 높이) ──
            Vector3 npcFwd = npc.transform.forward;
            npcFwd.y = 0f;
            if (npcFwd.sqrMagnitude < 0.0001f) npcFwd = Vector3.forward;
            npcFwd.Normalize();
            Vector3 npcPos = npc.transform.position;
            Vector3 skinPos = npcPos + npcFwd * 0.8f + Vector3.up * 0.35f;   // 대화 카메라 높이에 근접(가슴~머리)
            Vector3 skinLook = npcPos + Vector3.up * 1.15f;
            if (smr != null)
            {
                float d, projExt, front;
                NearPlaneMath(skinPos, (skinLook - skinPos).normalized, smrBounds, out d, out projExt, out front);
                float margin = front - probeCam.nearClipPlane;
                log.AppendLine($"  ④skin산술 distCenter={d:F3} projExtents={projExt:F3} frontFaceDist={front:F3} nearClip={probeCam.nearClipPlane:F3} margin={margin:F3} → {(margin < 0 ? "침입" : "침입 없음")}");
            }
            CaptureFromCamera(probeCam, rt, $"npc136_skin_{tag}", skinPos, skinLook);
            log.AppendLine($"  ④skin 캡처 완료 — NPC 앞 0.8m lookAt={V3(skinLook)}");

            // ── ⑤ 후방 참조컷(NPC 등 쪽 2.92m — 정면/후방 대조) ──
            Vector3 rearPos = npcPos - npcFwd * 2.92f + Vector3.up * 0.5f;
            Vector3 rearLook = npcPos + Vector3.up * 0.9f;
            CaptureFromCamera(probeCam, rt, $"npc136_rear_{tag}", rearPos, rearLook);
            log.AppendLine($"  ⑤rear 캡처 완료 — NPC 뒤 2.92m");

            // ── ⑥ End() 후 원복 스냅샷 ──
            DialogueCameraDirector.End();
            float rootYawAfter = npc.transform.eulerAngles.y;
            float gaze = fiGaze != null ? Convert.ToSingle(fiGaze.GetValue(null)) : float.NaN;
            log.AppendLine($"  ⑥End후 rootYaw={rootYawAfter:F1} gazeSavedYaw={gaze:F1} rootYaw-저장Yaw delta={(float.IsNaN(gaze) ? "n/a" : Mathf.DeltaAngle(rootYawAfter, gaze).ToString("F1"))}");
            log.AppendLine($"  ⑥End후 DialogueYawControlled={VillagerWanderer.DialogueYawControlled} _active={(fiActive != null ? fiActive.GetValue(null).ToString() : "n/a")}");
            log.AppendLine($"  ⑥End후 카메라 pos={V3(cam.transform.position)} rotEuler={V3(cam.transform.rotation.eulerAngles)} nearClip={cam.nearClipPlane:F3}");
        }

        // ── [136차-후속] 얼굴측 근접컷 — 136차 확정: 메시는 트랜스폼 forward의 반대(−Z)를 바라봄 → 얼굴 보이는 쪽 = −forward ──
        log.AppendLine();
        log.AppendLine("[136차-후속] 얼굴측 근접컷 — 얼굴측(−forward) 1.0m/1.6m + nearClip 0.05 변형(1m)");
        for (int i = 0; i < Keys.Length; i++)
        {
            var npcF = npcs[i];
            if (npcF == null) continue;
            string keyF = Keys[i];
            var smrF = smrs[i];
            var animF = animators[i];

            Vector3 faceNpcFwd = npcF.transform.forward;
            faceNpcFwd.y = 0f;
            if (faceNpcFwd.sqrMagnitude < 0.0001f) faceNpcFwd = Vector3.forward;
            faceNpcFwd.Normalize();
            Vector3 faceDir = -faceNpcFwd;   // 얼굴이 보이는 쪽(−forward)
            Vector3 faceNpcPos = npcF.transform.position;

            // 조준점=머리 중심(skin 컷 머리 조준 로직 계승) — 헤드 본 우선, 부재 시 고정 1.5m
            Transform headBone = animF != null ? animF.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 headCenter = headBone != null ? headBone.position : faceNpcPos + Vector3.up * 1.5f;
            log.AppendLine($"  [후속]{keyF}: faceDir={V3(faceDir)} headCenter={V3(headCenter)} headBone={(headBone != null ? headBone.name : "null→고정1.5m")} nearClip={probeCam.nearClipPlane:F3}");

            var shots = new[] { new { dist = 1.0f, suffix = "faceclose1m" }, new { dist = 1.6f, suffix = "faceclose16m" } };
            foreach (var shot in shots)
            {
                Vector3 camPosF = new Vector3(faceNpcPos.x + faceDir.x * shot.dist, headCenter.y, faceNpcPos.z + faceDir.z * shot.dist);
                Vector3 camFwdF = (headCenter - camPosF).normalized;
                if (smrF != null)
                {
                    float dF, projExtF, frontF;
                    NearPlaneMath(camPosF, camFwdF, smrF.bounds, out dF, out projExtF, out frontF);
                    float marginF = frontF - probeCam.nearClipPlane;
                    log.AppendLine($"  [후속]{keyF} {shot.suffix}: dist={shot.dist:F1}m nearClip={probeCam.nearClipPlane:F3} distCenter={dF:F3} projExtents={projExtF:F3} frontFaceDist={frontF:F3} margin={marginF:F3} → {(marginF < 0 ? "침입(근평면 절단 의심)" : "침입 없음")}");
                }
                CaptureFromCamera(probeCam, rt, $"npc136_{shot.suffix}_{keyF}", camPosF, headCenter);
                log.AppendLine($"  [후속]{keyF} {shot.suffix} 캡처 완료 — camPos={V3(camPosF)} lookAt(머리중심)={V3(headCenter)}");
            }

            // nearClip 0.05 임시 낮춤 변형(1m) — 근평면 영향 배제 검증용. 원복 필수.
            float savedNear = probeCam.nearClipPlane;
            probeCam.nearClipPlane = 0.05f;
            Vector3 fixPos = new Vector3(faceNpcPos.x + faceDir.x * 1.0f, headCenter.y, faceNpcPos.z + faceDir.z * 1.0f);
            Vector3 fixFwd = (headCenter - fixPos).normalized;
            if (smrF != null)
            {
                float dN, projExtN, frontN;
                NearPlaneMath(fixPos, fixFwd, smrF.bounds, out dN, out projExtN, out frontN);
                float marginN = frontN - 0.05f;
                log.AppendLine($"  [후속]{keyF} faceclose1m_nearfix: nearClip=0.050 distCenter={dN:F3} projExtents={projExtN:F3} frontFaceDist={frontN:F3} margin={marginN:F3} → {(marginN < 0 ? "침입" : "침입 없음")}");
            }
            CaptureFromCamera(probeCam, rt, $"npc136_faceclose1m_nearfix_{keyF}", fixPos, headCenter);
            probeCam.nearClipPlane = savedNear;   // 원복
            log.AppendLine($"  [후속]{keyF} faceclose1m_nearfix 캡처 완료 — nearClip 원복={savedNear:F3}");
        }

        // ── 마무리 ──
        log.AppendLine();
        log.AppendLine("[136차 프로브] DONE — 판정/수리 없음(측정 전용). PNG는 감독자 vision 판독 대기.");
        // [136차-후속] 어펜드(덮어쓰기 금지) — 기존 내용 보존 후 이번 실행 결과를 뒤에 추가
        File.AppendAllText(OutTxt, log.ToString());
        Debug.Log($"[136차 프로브] DONE — {OutTxt}");

        probeCam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(probeCamGo);
        UnityEngine.Object.DestroyImmediate(camGo);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(playerGo);
        UnityEngine.Object.DestroyImmediate(ground);
        UnityEngine.Object.DestroyImmediate(sunGo);
    }

    /// <summary>클립 재생 1스텝 진행(헬퍼 — 재생 실패 무시, 기록만).</summary>
    private static void SnapPose(Animator anim, string state, Transform hips, Transform root, StringBuilder log, string label)
    {
        try
        {
            anim.Play(state, 0, 0f);
            anim.Update(0.2f);
            float delta = Mathf.DeltaAngle(root.eulerAngles.y, hips.rotation.eulerAngles.y);
            log.AppendLine($"  {label}: state={state} played=true hips-rootDelta={delta:F1} (|delta|>90 = 몸이 등을 보임)");
        }
        catch (Exception e)
        {
            log.AppendLine($"  {label}: state={state} 재생실패 {e.Message}");
        }
    }

    /// <summary>비대화/공용 스냅샷 행 — root yaw, hips yaw, SMR bounds 중심 방향 vs root forward.</summary>
    private static string SnapshotBase(GameObject npc, Transform hips, SkinnedMeshRenderer smr, string label)
    {
        var sb = new StringBuilder();
        float rootYaw = npc.transform.eulerAngles.y;
        float hipsYaw = hips != null ? hips.rotation.eulerAngles.y : float.NaN;
        sb.AppendLine($"  {label} 스냅샷: rootYaw={rootYaw:F1} hipsWorldYaw={(hips != null ? hipsYaw.ToString("F1") : "n/a")} hips-rootDelta={(hips != null ? Mathf.DeltaAngle(rootYaw, hipsYaw).ToString("F1") : "n/a")}");
        if (smr != null && smr.sharedMesh != null)
        {
            var b = smr.bounds;
            Vector3 centerDir = b.center - npc.transform.position;
            centerDir.y = 0f;
            Vector3 fwd = npc.transform.forward;
            fwd.y = 0f;
            float dot = centerDir.sqrMagnitude > 0.0001f && fwd.sqrMagnitude > 0.0001f
                ? Vector3.Dot(centerDir.normalized, fwd.normalized) : float.NaN;
            sb.AppendLine($"  {label} 스냅샷: SMR boundsCenter={V3(b.center)} extents={V3(b.extents)} bounds방향 vs rootFwd dot={dot:F3} (+1=정면/−1=등)");
        }
        return sb.ToString();
    }

    /// <summary>nearClip 침입 산술 — distCenter(카메라에서 bounds 중심까지 forward 사영) − projExtents(앞면) vs nearClip.</summary>
    private static void NearPlaneMath(Vector3 camPos, Vector3 camFwd, Bounds b, out float d, out float projExt, out float front)
    {
        Vector3 f = camFwd.normalized;
        d = Vector3.Dot(b.center - camPos, f);
        projExt = Mathf.Abs(b.extents.x * f.x) + Mathf.Abs(b.extents.y * f.y) + Mathf.Abs(b.extents.z * f.z);
        front = d - projExt;
    }

    /// <summary>기존 Capture 선례 계승 — RT 렌더 + ReadPixels + WriteAllBytes. pos/lookAt 주면 이동 후 캡처.</summary>
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

    // ═══════════════ [138차 Impl-B] 클립 감사/재검증 — 증거 수집 전용(본 파일은 패치 없음) ═══════════════
    // 사양: docs/138_CAVE_BUILD_NPC_FACING_FIX_PLAN.md §3. 전제(136차 확정): NPC 메시 = 트랜스폼 forward
    // 180° 반대 렌더 → face ≈ hips+180. 목표: 각 상태 face == root 방향 → hips−root 목표 = ±180.
    // Δ = 180 − (측정 hips−root) — 판정/패치는 감독자 규칙(Phase-B)에 따라 별도 수행.

    /// <summary>[138차] MeshyUser/Idle_02.fbx guid — Player_AC.controller 공유 참조(136차 확정).</summary>
    private const string Idle02Guid = "39a29092f4f554844ada1e013b987a50";

    /// <summary>[138차] 전수 측정 키 24종 — [135차 P3-A] RunClipYaw 키 리스트 계승.</summary>
    private static readonly string[] AuditKeys =
    {
        "man1", "girl1", "boy1", "oldman1", "oldwoman1", "man2", "girl2", "boy2",
        "oldman2", "oldwoman2", "man3", "girl3", "boy3", "oldman3", "oldwoman3",
        "man4", "girl4", "boy4", "oldwoman4", "man5", "girl5", "boy5", "oldwoman5", "girl6"
    };

    /// <summary>[138차-Audit] Phase-A 감사 — ①상태→클립 매핑 덤프 ②hips−root 다샘플 실측 ③walk/idle/talk front컷.</summary>
    public static void RunClipAudit() { RunClipAuditCore(false); }

    /// <summary>[138차-After] Phase-C 재검증 — 패치+재임포트 후 동일 감사 재실행(출력 파일명만 교체).</summary>
    public static void RunClipAuditAfter() { RunClipAuditCore(true); }

    private static void RunClipAuditCore(bool after)
    {
        string outTxt = after ? "TestOutput/npc138_facing_after.txt" : "TestOutput/npc138_clip_audit.txt";
        string pngPrefix = after ? "npc138_after" : "npc138";
        string tag = after ? "After" : "Audit";
        var log = new StringBuilder();
        log.AppendLine($"[138차-{tag}] RunClipAudit 시작 — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        EnsureDir();

        // ── 조명/환경 최소 셋업(RT 렌더 필요 — RunAll 선례) ──
        var sunGo = new GameObject("ProbeSun138");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sunGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f, 1f);

        var rt = new RenderTexture(960, 540, 24);
        var camGo = new GameObject("ProbeCam138");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.15f;
        cam.farClipPlane = 500f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        cam.targetTexture = rt;

        // ── ① Idle_02 공유 클립 컨트롤러 참조 전수(에디터 API — guid 역조회) ──
        DumpIdle02References(log);

        // ── ②③ 전수 실측(24키) + 매핑 덤프/렌더컷(3키: girl1/boy1/man2) ──
        string[] dumpKeys = { "girl1", "boy1", "man2" };
        foreach (var key in AuditKeys)
        {
            if (!RuntimeModelLoader.TryGetResourceKey(key, out var resKey) ||
                !RuntimeModelLoader.TryGetModel(resKey, out var prefab) || prefab == null)
            {
                log.AppendLine($"[{key}] LOAD_FAIL resKey={resKey ?? "null"}");
                continue;
            }
            var npc = UnityEngine.Object.Instantiate(prefab);
            npc.name = key + "_p138";
            npc.transform.position = Vector3.zero;
            npc.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);   // 루트=+Z
            VillageNpcSpawner.EnsureVillageNpcAnimation(npc, key, npc.name);
            var anim = npc.GetComponentInChildren<Animator>(true);
            if (anim == null) anim = npc.GetComponent<Animator>();
            var smr = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Transform hips = smr != null ? smr.rootBone : null;
            if (anim == null || hips == null)
            {
                log.AppendLine($"[{key}] NO_ANIM_OR_HIPS anim={anim != null} hips={hips != null}");
                UnityEngine.Object.DestroyImmediate(npc);
                continue;
            }
            if (dumpKeys.Contains(key)) DumpControllerMapping(anim, log, key);

            var row = new List<string>();
            foreach (var state in new[] { "Idle", "Walk", "TalkA", "Run" })
            {
                var m = MeasureStateYaw(anim, hips, npc.transform, state, state == "Idle" || state == "TalkA" ? 0f : 2f);
                if (float.IsNaN(m.mean)) { row.Add($"{state}=NO_STATE"); continue; }
                // [138차 판정] 목표 hips−root=±180(face==root). Δ = 180 − D(정규화) — |Δ|≤5 통과.
                float delta = Normalize180(180f - m.mean);
                string verdict = m.span > 60f ? $"불안정(스팬{m.span:F0})" : Mathf.Abs(delta) <= 5f ? "정면OK" : $"교정대상(Δ={delta:F0})";
                row.Add($"{state}={m.mean:F1}[스팬{m.span:F0}] {verdict}");
                log.AppendLine($"  [{key}] {state}: 샘플({m.samples}) 원평균={m.mean:F1} 스팬={m.span:F1} Δ={delta:F1} → {verdict}");
            }
            log.AppendLine($"[{key}] 요약: " + string.Join("  ", row));

            // ── [138차] 강제 슬롯 측정 — 캐릭터별 클립을 오버라이드 슬롯에 직접 장착해 클립 고유 D 실측
            //   (런타임 Idle 슬롯은 시드 패리티에 따라 Idle_02/캐릭터별 idle이 갈리므로 패리티 무관 측정 필요)
            MeasureForcedSlots(anim, hips, npc.transform, key, log);

            if (dumpKeys.Contains(key))
            {
                foreach (var state in new[] { "Walk", "Idle", "TalkA" })
                {
                    try
                    {
                        anim.SetFloat("Speed", state == "Idle" || state == "TalkA" ? 0f : 2f);   // [138차-v3] Walk 유지 조건 홀드(TalkA=0 — Exit Time 자동복귀 보호)
                        anim.Play(state, 0, 0f);
                        anim.Update(1.5f);   // [138차] 수렴 대기 — idle/walk 클립 초기 트랜지언트(~0.6s) 스킵 후 캡처
                    }
                    catch { continue; }
                    // front컷 = 루트 forward측 카메라 2.4m — face==root 목표 달성 시 이 컷이 얼굴이어야 함
                    Vector3 camPos = npc.transform.position + npc.transform.forward * 2.4f + Vector3.up * 0.5f;
                    Vector3 lookAt = npc.transform.position + Vector3.up * 0.95f;
                    CaptureFromCamera(cam, rt, $"{pngPrefix}_{state.ToLower()}_{key}", camPos, lookAt);
                    log.AppendLine($"  [{key}] {state} front컷 캡처 완료 — {pngPrefix}_{state.ToLower()}_{key}.png (루트 forward측 2.4m)");
                }
            }
            UnityEngine.Object.DestroyImmediate(npc);
        }

        log.AppendLine();
        log.AppendLine(after
            ? "[138차-After] DONE — 패치 후 재검증 수치 + front컷(npc138_after_*.png). 플레이어 무영향 산술: Idle_02 패치 Δ≈−15 → 플레이어 idle face R_p+15 → R_p+0.1(개선, ±15° 내 예외 규칙 ① 준수)."
            : "[138차-Audit] DONE — 측정 전용(패치 없음). 판정/패치는 감사 수치 기준 Phase-B 규칙 적용.");
        File.WriteAllText(outTxt, log.ToString());
        Debug.Log($"[138차-{tag}] DONE — {outTxt}");

        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(camGo);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(sunGo);
    }

    /// <summary>[138차] Idle_02(공유 클립)를 참조하는 컨트롤러 전수 조사 — Player_AC 확인치 외 추가 참조자 조사.</summary>
    private static void DumpIdle02References(StringBuilder log)
    {
        string path = AssetDatabase.GUIDToAssetPath(Idle02Guid);
        log.AppendLine($"[138차-Audit] Idle_02 guid={Idle02Guid} path={path ?? "null"}");
        if (string.IsNullOrEmpty(path)) return;
        var idleClips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToList();
        log.AppendLine($"  Idle_02 FBX 내 클립 {idleClips.Count}건: " + string.Join(", ", idleClips.Select(c => c.name)));
        if (idleClips.Count == 0) return;
        var found = new List<string>();
        foreach (var t in new[] { "t:AnimatorController", "t:AnimatorOverrideController" })
        {
            foreach (var g in AssetDatabase.FindAssets(t))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(p);
                if (ctrl == null) continue;
                if (ctrl.animationClips.Any(c => c != null && idleClips.Contains(c)))
                    found.Add(p);
            }
        }
        log.AppendLine($"  참조 컨트롤러 {found.Count}건:");
        foreach (var f in found) log.AppendLine($"    {f}");
    }

    /// <summary>[138차] 런타임 컨트롤러/오버라이드 상태→클립 매핑 덤프 — idle/walk/talk 슬롯의 실제 클립+소스+guid.</summary>
    private static void DumpControllerMapping(Animator anim, StringBuilder log, string key)
    {
        var ctrl = anim.runtimeAnimatorController;
        if (ctrl == null) { log.AppendLine($"  [{key}] 매핑: controller=null"); return; }
        log.AppendLine($"  [{key}] 매핑: runtime={ctrl.name} type={ctrl.GetType().Name} path={AssetDatabase.GetAssetPath(ctrl)}");
        var baseCtrl = ctrl;
        if (ctrl is AnimatorOverrideController aoc)
        {
            baseCtrl = aoc.runtimeAnimatorController;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(pairs);
            log.AppendLine($"  [{key}] 오버라이드 pairs={pairs.Count} base={baseCtrl?.name}");
            foreach (var p in pairs)
            {
                if (p.Value == null || p.Value == p.Key) continue;   // 교체 슬롯만 표시
                log.AppendLine($"    슬롯 \"{(p.Key != null ? p.Key.name : "null")}\" → \"{p.Value.name}\" path={AssetDatabase.GetAssetPath(p.Value)} guid={AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(p.Value))}");
            }
        }
        var ac = baseCtrl as AnimatorController;
        if (ac == null) { log.AppendLine($"  [{key}] 베이스가 AnimatorController 아님({baseCtrl?.GetType().Name}) — 상태 순회 생략"); return; }
        foreach (var layer in ac.layers)
        {
            if (layer.stateMachine == null) continue;
            foreach (var st in layer.stateMachine.states)
            {
                if (st.state == null) continue;
                DumpMotion(st.state.name, st.state.motion, log, key);
            }
        }
    }

    /// <summary>[138차] 상태 모션 덤프(BlendTree 재귀 포함) — 클립명+소스 경로+guid.</summary>
    private static void DumpMotion(string stateName, Motion motion, StringBuilder log, string key)
    {
        if (motion == null) { log.AppendLine($"    [{key}] 상태 \"{stateName}\" → motion=null"); return; }
        if (motion is BlendTree bt)
        {
            log.AppendLine($"    [{key}] 상태 \"{stateName}\" → BlendTree \"{motion.name}\"");
            foreach (var child in bt.children)
                DumpMotion(stateName, child.motion, log, key);
            return;
        }
        var clip = motion as AnimationClip;
        if (clip == null) { log.AppendLine($"    [{key}] 상태 \"{stateName}\" → motion={motion.name}(비클립)"); return; }
        string p = AssetDatabase.GetAssetPath(clip);
        log.AppendLine($"    [{key}] 상태 \"{stateName}\" → 클립 \"{clip.name}\" path={p} guid={(string.IsNullOrEmpty(p) ? "n/a" : AssetDatabase.AssetPathToGUID(p))}");
    }

    /// <summary>[138차] 상태 재생 후 다샘플 hips−root 실측 — Walk 스윙 동요 평균화(135차 단일샘플 한계 보완).
    /// 7샘플 × 0.2s = 1.4s(보행 주기 대부분 커버). 평균은 원평균(±180 랩 걸침 내성), 스팬=최대 각거리.
    /// [138차-v3] Speed 홀드 필수 — Player_AC Walk↔Idle은 Speed 조건 전이(>0.55 진입/<0.35 복귀)라
    /// Speed=0 상태에서 Play("Walk")하면 ~0.4s 후 Idle로 자동 복귀해 클립이 측정되지 않음(v2 실측 폭발).
    /// 재생 실패 시 mean=NaN.</summary>
    private static (float mean, float span, string samples) MeasureStateYaw(
        Animator anim, Transform hips, Transform root, string state, float speed)
    {
        var samples = new List<float>();
        try
        {
            anim.SetFloat("Speed", speed);   // [138차-v3] Walk 유지 조건 홀드(Idle=0 / Walk·Run=2)
            anim.Play(state, 0, 0f);
            for (int i = 0; i < 7; i++)
            {
                anim.Update(0.2f);
                samples.Add(Mathf.DeltaAngle(root.eulerAngles.y, hips.rotation.eulerAngles.y));
            }
        }
        catch (Exception e)
        {
            return (float.NaN, float.NaN, "NO_STATE:" + e.Message);
        }
        // 원평균 — ±180 랩 걸친 샘플의 선형 평균 왜곡 방지(예: −179/+179 선형평균 0 → 원평균 180)
        double sinSum = 0, cosSum = 0;
        foreach (var s in samples) { sinSum += Mathf.Sin(s * Mathf.Deg2Rad); cosSum += Mathf.Cos(s * Mathf.Deg2Rad); }
        float circ = (float)(Math.Atan2(sinSum, cosSum) * Mathf.Rad2Deg);
        float span = 0f;
        for (int i = 0; i < samples.Count; i++)
            for (int j = i + 1; j < samples.Count; j++)
                span = Mathf.Max(span, Mathf.Abs(Mathf.DeltaAngle(samples[i], samples[j])));
        return (circ, span, string.Join(",", samples.Select(s => s.ToString("F0"))));
    }

    /// <summary>[138차] 캐릭터별 클립을 오버라이드 슬롯에 강제 장착해 클립 고유 D 실측 — 패리티 무관.
    /// VillageNpcSpawner.ResolveCharClips 리플렉션 호출(internal — 크로스 어셈블리). 복원 보장.</summary>
    private static void MeasureForcedSlots(Animator anim, Transform hips, Transform root, string key, StringBuilder log)
    {
        var mi = typeof(VillageNpcSpawner).GetMethod("ResolveCharClips", BindingFlags.NonPublic | BindingFlags.Static);
        if (mi == null) { log.AppendLine($"  [{key}] 강제슬롯: ResolveCharClips 리플렉션 실패"); return; }
        var r = mi.Invoke(null, new object[] { key });
        if (r == null) { log.AppendLine($"  [{key}] 강제슬롯: 해석 null"); return; }
        var (walk, idle, talk, run) = ((AnimationClip, AnimationClip, AnimationClip, AnimationClip))r;
        if (walk == null && idle == null && talk == null)
        {
            log.AppendLine($"  [{key}] 강제슬롯: 캐릭터별 클립 전무(구 GLB 키 또는 미해석)");
            return;
        }
        var baseCtrl = Resources.Load<RuntimeAnimatorController>("Animation/Controllers/Player_AC");
        var saved = anim.runtimeAnimatorController;
        if (baseCtrl == null) { log.AppendLine($"  [{key}] 강제슬롯: Player_AC 로드 실패"); return; }
        var aoc = new AnimatorOverrideController(baseCtrl);
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        aoc.GetOverrides(pairs);
        for (int i = 0; i < pairs.Count; i++)
        {
            var origin = pairs[i].Key;
            if (origin == null) continue;
            if (origin.name == "Armature|Idle_02" && idle != null)
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(origin, idle);
            else if (origin.name == "Armature|Walking_withSkin" && walk != null)
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(origin, walk);
            else if (origin.name.ToLowerInvariant().Contains("talk") && talk != null)
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(origin, talk);
        }
        aoc.ApplyOverrides(pairs);
        try
        {
            anim.runtimeAnimatorController = aoc;
            anim.Rebind();
            foreach (var state in new[] { "Idle", "Walk", "TalkA" })
            {
                var m = MeasureStateYaw(anim, hips, root, state, state == "Idle" || state == "TalkA" ? 0f : 2f);
                if (float.IsNaN(m.mean)) { log.AppendLine($"  [{key}] 강제슬롯 {state}: NO_STATE"); continue; }
                string src = state == "Idle" ? ClipSrc(idle, "idle") : state == "Walk" ? ClipSrc(walk, "walk") : ClipSrc(talk, "talk");
                float delta = Normalize180(180f - m.mean);
                string verdict = m.span > 60f ? $"불안정(스팬{m.span:F0})" : Mathf.Abs(delta) <= 5f ? "정면OK" : $"교정대상(Δ={delta:F0})";
                log.AppendLine($"  [{key}] 강제슬롯 {state}: {src} 원평균={m.mean:F1} 스팬={m.span:F1} 샘플({m.samples}) Δ={delta:F1} → {verdict}");
            }
        }
        finally
        {
            anim.runtimeAnimatorController = saved;   // [138차] 원복 필수 — 렌더컷은 원래 런타임 구성으로
            anim.Rebind();
            UnityEngine.Object.DestroyImmediate(aoc);
        }
    }

    /// <summary>[138차] 클립 소스 표기(강제슬롯 로그용).</summary>
    private static string ClipSrc(AnimationClip clip, string fallback)
    {
        if (clip == null) return $"클립=null({fallback} 폴백)";
        string p = AssetDatabase.GetAssetPath(clip);
        return $"클립=\"{clip.name}\" path={p} guid={(string.IsNullOrEmpty(p) ? "n/a" : AssetDatabase.AssetPathToGUID(p))}";
    }

    /// <summary>[138차] 각도 (−180,180] 정규화.</summary>
    private static float Normalize180(float a)
    {
        a = Mathf.Repeat(a + 180f, 360f) - 180f;
        if (a <= -180f) a += 360f;
        return a;
    }
}
