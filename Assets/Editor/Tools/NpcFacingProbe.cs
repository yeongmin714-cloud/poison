using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using ProjectName.Systems;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [135차 P2] NPC 정면/피부 진단 프로브 — 배치모드 1회 실행, 수리 없음(측정 전용).
/// 목적:
///  1. 스폰 직후(플립 철회 상태) NPC 계층의 골격 forward vs 메시 시각 forward 180° 어긋남 수치 확정
///     — 메시 정점을 루트 로컬 공간에서 샘플: 머리 영역(y 상위) 정점 mean z의 부호 = 메시 얼굴 방향.
///  2. 대화 카메라 프레이밍 재현 스크린샷(정면/근접/후방) — 사용자 눈(얼굴 보임 여부)과
///     근평면 침입(피부 깨짐) 재현 판정용.
/// 산출: TestOutput/npc_facing_probe.txt + TestOutput/npc_facing_*.png
/// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod NpcFacingProbe.Run -quit
/// </summary>
public static class NpcFacingProbe
{
    private const string OutTxt = "TestOutput/npc_facing_probe.txt";

    public static void Run()
    {
        var log = new StringWriter();

        // ── 조명/환경 (배치모드 렌더용 최소 셋업) ──
        var sunGo = new GameObject("ProbeSun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sunGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f, 1f);

        // ── NPC 스폰 (실제 스폰 경로 재사용 — TryGetModel + EnsureNpcTexture 리플렉션) ──
        string[] keys = { "girl1", "man1" };   // [135차 P2] 실제 VillagerKeys 키 체계(man1/girl1... — "Villager_N"은 npcName)
        var spawned = new GameObject[2];
        for (int i = 0; i < keys.Length; i++)
        {
            if (!RuntimeModelLoader.TryGetResourceKey(keys[i], out var resKey) ||
                !RuntimeModelLoader.TryGetModel(resKey, out var prefab) || prefab == null)
            {
                log.WriteLine($"[135차 P2] {keys[i]}(→{resKey ?? "null"}) 프리팹 로드 실패");
                continue;
            }
            var npc = Object.Instantiate(prefab);
            npc.name = keys[i] + "_Probe";
            npc.transform.position = new Vector3(i * 2f, 0f, 0f);
            // [135차 P2] 세이보 환경 재현 — 루트 forward=+Z(플레이어가 +Z 측에 있다고 가정)
            npc.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var ensureTex = typeof(VillageNpcSpawner).GetMethod("EnsureNpcTexture",
                BindingFlags.NonPublic | BindingFlags.Static);
            ensureTex?.Invoke(null, new object[] { npc, keys[i] });
            spawned[i] = npc;
        }

        // ── 수치 덤프: 계층 회전 + 메시 정점 기준 얼굴 방향 ──
        foreach (var npc in spawned)
        {
            if (npc == null) continue;
            log.WriteLine($"== {npc.name} ==");
            log.WriteLine($"root.forward=({npc.transform.forward.x:F2},{npc.transform.forward.y:F2},{npc.transform.forward.z:F2})");
            foreach (Transform child in npc.transform)
            {
                var e = child.rotation.eulerAngles;
                log.WriteLine($"  child[{child.name}] localRot=({child.localRotation.eulerAngles.x:F1},{child.localRotation.eulerAngles.y:F1},{child.localRotation.eulerAngles.z:F1}) worldYaw={e.y:F1} hasSmr={(child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)}");
            }
            var smrs = npc.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                // 루트 로컬 공간에서 정점 샘플 — SMR의 bindpose 기준(루트=보통 모델 루트의 부모)
                var root = npc.transform;
                Vector3 rootLocalOfSmr = root.InverseTransformPoint(smr.transform.position);
                Vector3[] verts = mesh.vertices;
                float sumZ = 0f, headSumZ = 0f; int headN = 0;
                float maxY = float.MinValue;
                foreach (var v in verts) if (v.y > maxY) maxY = v.y;
                float headThresh = maxY * 0.88f;
                foreach (var v in verts)
                {
                    sumZ += v.z;
                    if (v.y >= headThresh) { headSumZ += v.z; headN++; }
                }
                float meanZ = sumZ / verts.Length;
                float headMeanZ = headN > 0 ? headSumZ / headN : 0f;
                var b = smr.bounds;
                log.WriteLine($"  smr[{smr.name}] rootBone={smr.rootBone?.name ?? "null"} smrLocalPosInRoot=({rootLocalOfSmr.x:F2},{rootLocalOfSmr.y:F2},{rootLocalOfSmr.z:F2})");
                log.WriteLine($"    verts={verts.Length} meanZ(root-local)={meanZ:F4} headMeanZ(y>={headThresh:F2},n={headN})={headMeanZ:F4} boundsCenterZ={b.center.z:F3} extents=({b.extents.x:F2},{b.extents.y:F2},{b.extents.z:F2})");
                log.WriteLine($"    얼굴방향판정: headMeanZ 부호 {(headMeanZ >= 0 ? "+Z(루트 forward 동일)" : "-Z(루트 forward 반대 — 180° 어긋남 의심)")}");
                log.WriteLine($"    nearClipDefault={ (smr.GetComponentInParent<Renderer>() != null ? "n/a" : "n/a") }");
            }
        }

        // ── 스크린샷: 대화 카메라 프레이밍 재현 ──
        // 대화 카메라 기하(ComputeFullBodyCamPos 재현): center=target+up×0.9, bearing=플레이어→NPC=-Z,
        // camPos = center − bearing×dist → +Z 측. dist 2.92(기본)/1.4(근접 스트레스)/후방 -2.92.
        var camGo = new GameObject("ProbeCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 500f;
        var rt = new RenderTexture(960, 540, 24);
        cam.targetTexture = rt;

        Capture(cam, rt, new Vector3(1f, 1.5f, 2.92f), new Vector3(1f, 0.9f, 0f), "npc_facing_front");
        Capture(cam, rt, new Vector3(0f, 1.5f, 1.4f), new Vector3(0f, 1.3f, 0f), "npc_facing_close14");
        Capture(cam, rt, new Vector3(0f, 1.5f, 0.6f), new Vector3(0f, 1.4f, 0f), "npc_facing_close06");
        Capture(cam, rt, new Vector3(1f, 1.5f, -2.92f), new Vector3(1f, 0.9f, 0f), "npc_facing_back");

        log.WriteLine($"[135차 P2] 스크린샷 4종 저장 완료(front/close14/close06/back)");
        log.WriteLine("[135차 P2] DONE");

        File.WriteAllText(OutTxt, log.ToString());
        Debug.Log($"[135차 P2] DONE — {OutTxt}");
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(rt);
    }

    private static void Capture(Camera cam, RenderTexture rt, Vector3 pos, Vector3 lookAt, string name)
    {
        cam.transform.position = pos;
        cam.transform.LookAt(lookAt);
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes($"TestOutput/{name}.png", tex.EncodeToPNG());
        RenderTexture.active = null;
        Object.DestroyImmediate(tex);
    }

    /// <summary>[135차 P2-v2] 애니메이션 재생 상태 정면 판정 — Idle/Walk/TalkA 재생 후 렌더+수치.
    /// 바인드 포즈는 정면(+Z) 확인됨(P2 1차) → 클립 베이크 요각 오프셋 유무를 렌더로 확정.</summary>
    public static void RunAnimated()
    {
        var log = new StringWriter();
        var sunGo = new GameObject("ProbeSun2");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sunGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f, 1f);

        if (!RuntimeModelLoader.TryGetResourceKey("girl1", out var resKey) ||
            !RuntimeModelLoader.TryGetModel(resKey, out var prefab) || prefab == null)
        {
            log.WriteLine("[135차 P2-v2] girl1 로드 실패");
            File.WriteAllText("TestOutput/npc_facing_probe_anim.txt", log.ToString());
            return;
        }
        var npc = Object.Instantiate(prefab);
        npc.name = "girl1_AnimProbe";
        npc.transform.position = Vector3.zero;
        npc.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);   // 루트=+Z(플레이어 측)

        // 실제 스폰 배선 재사용(공개 메서드) — 오버라이드 컨트롤러+클립 슬롯 구성
        VillageNpcSpawner.EnsureVillageNpcAnimation(npc, "girl1", "Villager_7");
        var ensureTex = typeof(VillageNpcSpawner).GetMethod("EnsureNpcTexture",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        ensureTex?.Invoke(null, new object[] { npc, "girl1" });

        var animator = npc.GetComponent<Animator>();
        if (animator == null)
        {
            log.WriteLine("[135차 P2-v2] Animator 부재(루트)");
            File.WriteAllText("TestOutput/npc_facing_probe_anim.txt", log.ToString());
            return;
        }
        animator.enabled = true;

        var camGo = new GameObject("ProbeCam2");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 500f;
        var rt = new RenderTexture(960, 540, 24);
        cam.targetTexture = rt;

        var smr = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Transform hips = smr != null ? smr.rootBone : null;

        foreach (var state in new[] { "Idle", "Walk", "TalkA" })
        {
            bool played = false;
            try { animator.Play(state, 0, 0f); played = true; } catch { }
            animator.Update(0.2f);   // 1스텝 진행 — 클립 베이크 포즈 반영
            if (hips != null)
            {
                float hipsYaw = hips.rotation.eulerAngles.y;
                float rootYaw = npc.transform.eulerAngles.y;
                float delta = Mathf.DeltaAngle(rootYaw, hipsYaw);
                log.WriteLine($"state={state} played={played} rootYaw={rootYaw:F1} hipsWorldYaw={hipsYaw:F1} rootYaw-hipsYaw delta={delta:F1}  (|delta|>90 = 몸이 등을 보임)");
            }
            cam.transform.position = new Vector3(0f, 1.5f, 2.92f);
            cam.transform.LookAt(new Vector3(0f, 0.9f, 0f));
            Capture(cam, rt, Vector3.zero, Vector3.zero, "npc_anim_" + state.ToLower());
        }

        log.WriteLine("[135차 P2-v2] DONE");
        File.WriteAllText("TestOutput/npc_facing_probe_anim.txt", log.ToString());
        Debug.Log("[135차 P2-v2] DONE");
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(rt);
    }

    /// <summary>[135차 P3-A] 24캐릭터 전수 클립 베이크 요각 측정 — Idle/Walk/TalkA/Run 재생 후
    /// Hips world yaw − root yaw delta 기록. |delta|>90 = 클립이 등 방향 베이크.</summary>
    public static void RunClipYaw()
    {
        var log = new StringWriter();
        string[] keys = { "man1", "girl1", "boy1", "oldman1", "oldwoman1", "man2", "girl2", "boy2",
            "oldman2", "oldwoman2", "man3", "girl3", "boy3", "oldman3", "oldwoman3",
            "man4", "girl4", "boy4", "oldwoman4", "man5", "girl5", "boy5", "oldwoman5", "girl6" };
        foreach (var key in keys)
        {
            if (!RuntimeModelLoader.TryGetResourceKey(key, out var resKey) ||
                !RuntimeModelLoader.TryGetModel(resKey, out var prefab) || prefab == null)
            {
                log.WriteLine($"{key}: LOAD_FAIL");
                continue;
            }
            var npc = Object.Instantiate(prefab);
            npc.transform.position = Vector3.zero;
            npc.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            VillageNpcSpawner.EnsureVillageNpcAnimation(npc, key, key + "_probe");
            var animator = npc.GetComponent<Animator>();
            var smr = npc.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Transform hips = smr != null ? smr.rootBone : null;
            if (animator == null || hips == null) { log.WriteLine($"{key}: NO_ANIM_OR_HIPS"); Object.DestroyImmediate(npc); continue; }
            var row = new List<string>();
            foreach (var state in new[] { "Idle", "Walk", "TalkA", "Run" })
            {
                try { animator.Play(state, 0, 0f); } catch { row.Add(state + "=NO_STATE"); continue; }
                animator.Update(0.2f);
                float d1 = Mathf.DeltaAngle(npc.transform.eulerAngles.y, hips.rotation.eulerAngles.y);
                animator.Update(0.3f);
                float d2 = Mathf.DeltaAngle(npc.transform.eulerAngles.y, hips.rotation.eulerAngles.y);
                row.Add($"{state}={d1:F1}/{d2:F1}");
            }
            log.WriteLine($"{key}: " + string.Join("  ", row));
            Object.DestroyImmediate(npc);
        }
        File.WriteAllText("TestOutput/npc_clip_yaw_table.txt", log.ToString());
        Debug.Log("[135차 P3-A] DONE 측정");
    }

    /// <summary>[135차 P3-A 적용] |delta|>90 클립의 FBX meta orientationOffsetY 교정(−delta)+재임포트+재측정.
    /// 사전 측정 표(TestOutput/npc_clip_yaw_table.txt)를 파싱해 대상 클립만 수정 — 수기 편집 없음.</summary>
    public static void RunClipYawFix()
    {
        var table = File.ReadAllLines("TestOutput/npc_clip_yaw_table.txt");
        var log = new StringWriter();
        int patched = 0;
        foreach (var line in table)
        {
            var m = System.Text.RegularExpressions.Regex.Match(line, "^(\\w+):.*Idle=(-?\\d+\\.\\d)");
            if (!m.Success) continue;
            string key = m.Groups[1].Value;
            float idleDelta = float.Parse(m.Groups[2].Value);
            // 대상: |delta|>90인 상태 — 교정값 = −delta (Idle/TalkA 개별 파싱)
            var pairs = new List<(string state, float delta)>();
            foreach (Match sm in Regex.Matches(line, "(Idle|Walk|TalkA|Run)=(-?\\d+\\.\\d)/(-?\\d+\\.\\d)"))
            {
                float d1 = float.Parse(sm.Groups[2].Value);
                float d2 = float.Parse(sm.Groups[3].Value);
                // 베이크 판정: 두 샘플 일관(|d1−d2|<15) + 유의 오프셋(|d1|>30) — 동적 포즈(Walk 스윙)는 제외
                // [135차 P3-A 2차 누적] |d|>8 & 일관 — TalkA 잔여 +22° 수렴용. 누적(기존 offset − delta).
                if (Mathf.Abs(d1) > 8f && Mathf.Abs(d1 - d2) < 15f) pairs.Add((sm.Groups[1].Value, d1));
            }
            if (pairs.Count == 0) continue;
            // FBX 파일 탐색: Assets/Resources/Models/UserProvided/Npc2026/<스템>/<스템>_<state>.fbx
            // state 매핑: Idle→_idle, TalkA→_talk
            string stem = NpcStemForKey(key);
            foreach (var (state, delta) in pairs)
            {
                string suffix = state == "Idle" ? "idle" : state == "TalkA" ? "talk" : state.ToLower();
                string fbx = $"Assets/Resources/Models/UserProvided/Npc2026/{stem}/{stem}_{suffix}.fbx";
                if (!File.Exists(fbx)) { log.WriteLine($"{key}/{state}: FBX 없음 {fbx}"); continue; }
                float offset = Mathf.Round(delta);   // [135차 P3-A 3차] 부호 실측 보정(gain=−1)
                bool ok = PatchOrientationOffsetY(fbx, suffix, offset, log);
                if (ok) { patched++; log.WriteLine($"{key}/{state}: offset {offset:F0} 적용"); }
            }
        }
        AssetDatabase.Refresh();
        log.WriteLine($"[135차 P3-A] 패치 {patched}건 — 재측정은 RunClipYaw로");
        File.WriteAllText("TestOutput/npc_clip_yaw_fix_log.txt", log.ToString());
        Debug.Log($"[135차 P3-A] FIX DONE {patched}");
    }

    private static string NpcStemForKey(string key)
    {
        // girl1 → Npc_Girl_1 / man1 → Npc_Man_1 / oldwoman1 → Npc_OldWoman_1
        string[] prefixes = { "oldwoman", "oldman", "girl", "boy", "man" };
        foreach (var pre in prefixes)
        {
            if (key.StartsWith(pre))
            {
                string num = key.Substring(pre.Length);
                string cap = char.ToUpper(pre[0]) + pre.Substring(1);
                return $"Npc_{cap}_{num}";
            }
        }
        return key;
    }

    private static bool PatchOrientationOffsetY(string fbxPath, string takeKeyword, float offset, StringWriter log, bool allowMaterialize = true)
    {
        // [135차 P3-A] 메타 YAML 직접 편집 — Unity 6000.x ModelImporterClipAnimation API에
        // orientationOffsetY 노출이 없어(순차 시도 CS1061 2건) 직렬화 키로 직접 치환.
        // idle/talk 전용 FBX = 단일 테이크 → clipAnimations 블록의 첫 orientationOffsetY가 대상.
        string meta = fbxPath + ".meta";
        if (!File.Exists(meta)) { log.WriteLine($"{fbxPath}: meta 없음"); return false; }
        var lines = new List<string>(File.ReadAllLines(meta));
        int clipStart = -1;
        for (int i = 0; i < lines.Count; i++)
            if (lines[i].Contains("clipAnimations:")) { clipStart = i; break; }
        if (clipStart < 0) { log.WriteLine($"{fbxPath}: clipAnimations 없음"); return false; }
        for (int i = clipStart + 1; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("orientationOffsetY:"))
            {
                int indent = lines[i].Length - lines[i].TrimStart().Length;
                float existing = 0f;
                float.TryParse(lines[i].TrimStart().Substring("orientationOffsetY:".Length).Trim(), out existing);
                // [135차 P3-A 3차] 실측 gain=−1(offset +N → raw −N 이동) → 호출부가 offset=+delta 전달,
                // 여기선 기존값에 전달값 누적. (pass2에서 −delta 적용 시 TalkA 22→44 역행 확인).
                float accumulated = existing + offset;
                lines[i] = new string(' ', indent) + "orientationOffsetY: " + accumulated.ToString("0");
                File.WriteAllLines(meta, lines);
                AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
                return true;
            }
        }
        // clipAnimations: [](미커스터마이즈) 케이스 — API로 기본 클립 블록을 직렬화한 뒤 재시도
        // (Unity 6000.x ModelImporterClipAnimation에 orientationOffsetY API 노출 없음 → 블록 생성만 API 사용).
        if (!allowMaterialize) { log.WriteLine($"{fbxPath}: orientationOffsetY 라인 미발견(블록 생성 후에도)"); return false; }
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            var defaults = importer.defaultClipAnimations;
            if (defaults != null && defaults.Length > 0)
            {
                importer.clipAnimations = defaults;   // 블록 직렬화(값은 직후 텍스트 패치)
                importer.SaveAndReimport();   // .meta 플러시(블록 직렬화+재임포트) — 무플러시 재귀 스택오버플로 수리
                return PatchOrientationOffsetY(fbxPath, takeKeyword, offset, log, allowMaterialize: false);
            }
        }
        log.WriteLine($"{fbxPath}: orientationOffsetY 라인 미발견(블록 생성 실패)");
        return false;
    }
}
