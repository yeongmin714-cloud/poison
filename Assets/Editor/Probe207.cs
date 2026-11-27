using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// [207차 P0] 진단 프로브 — (a)카메라 전수 덤프: Test_10 플레이 세션에서 존재하는 모든 Camera(name/id/pos/euler/
/// enabled/tag/scene) + PlayerMovement._camera·_cameraTransform·FollowCamera instanceID 실측 → 원점 매몰 조준
/// 카메라(camId -2578 실측)의 정체와 FollowCamera 갈라짐 경로 확정. (b)말 자세 분리: 운영 스폰 백마(애니 유) vs
/// 로우 GLB Instantiate(애니 무) bounds·루트 본 회전 실측 + 스크린샷 2장 → 누움 근원이 GLB 정적인지 클립 구동인지 판정.
/// 배치 진입: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod Probe207.Run -logFile ... (—quit 금지,
/// 프로브가 Exit 호출). 수정 0건 — 읽기전용 측정만. 산출: TestOutput/no207_probe_report.txt + no207_horseA/B PNG.
/// </summary>
public static class Probe207
{
    const string Tag = "[Probe207]";
    const string ScenePath = "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity";
    const string OutDir = "C:/Unity/code/TestOutput";
    const string ReportAbs = "C:/Unity/code/TestOutput/no207_probe_report.txt";
    const string SessionKey = "Probe207Active";
    const string SessionT0 = "Probe207T0";

    static int _phase;
    static GameObject _horseA;
    static Camera _probeCam;
    static bool _pumpSubscribed;

    public static void Run()
    {
        try
        {
            File.WriteAllText(ReportAbs, $"=== Probe207 시작 {DateTime.Now:HH:mm:ss} ===\n");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int sceneCams = UnityEngine.Object.FindObjectsOfType<Camera>(true).Length;
            Log($"에디트타임 씬 '{scene.name}' 카메라 수={sceneCams} (기대 0 — 씬 무카메라 계약)");
            SessionState.SetBool(SessionKey, true);
            SessionState.SetFloat(SessionT0, 0f);
            EditorApplication.isPlaying = true;
            SubscribePump();
            Log("Play 진입 요청");
        }
        catch (Exception e)
        {
            Log($"❌ Run 예외: {e.GetType().Name}: {e.Message}");
            EditorApplication.Exit(1);
        }
    }

    static void SubscribePump()
    {
        if (_pumpSubscribed) return;
        _pumpSubscribed = true;
        EditorApplication.update += Pump;
    }

    /// <summary>도메인 리로드 후 재구독 — [InitializeOnLoad] 부트가 호출.</summary>
    internal static void ResubscribeAfterReload()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        SubscribePump();
    }

    static void Pump()
    {
        if (!SessionState.GetBool(SessionKey, false)) { return; }
        if (!EditorApplication.isPlaying)
        {
            // 플레이 진입 실패 타임아웃 (t0 세팅 후 180초 내 미진입 등)
            float t0 = SessionState.GetFloat(SessionT0, 0f);
            if (t0 > 0f && Time.realtimeSinceStartup - t0 > 180f)
            {
                Log("❌ 플레이 세션 타임아웃 — 프로브 실패 종료");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
            return;
        }
        float start = SessionState.GetFloat(SessionT0, 0f);
        if (start <= 0f)
        {
            SessionState.SetFloat(SessionT0, Time.realtimeSinceStartup);
            return;
        }
        float t = Time.realtimeSinceStartup - start;

        if (_phase == 0 && t >= 6f)
        {
            _phase = 1;
            try
            {
                DumpCameras("t=6s");
                _horseA = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                    .FirstOrDefault(x => x.name.Contains("WildHorse"))?.gameObject;
                if (_horseA == null) Log("⚠️ WildHorse 미발견 — TestSceneSpawn 스폰 상태 확인 필요");
                else
                {
                    SamplePose(_horseA, "A(운영 스폰=애니 유) @t6");
                    Capture(_horseA, "no207_horseA_withanim.png");
                }
            }
            catch (Exception e) { Log($"❌ phase1 예외: {e.GetType().Name}: {e.Message}"); }
        }
        else if (_phase == 1 && t >= 9f)
        {
            _phase = 2;
            try
            {
                if (_horseA != null) SamplePose(_horseA, "A2 @t9(3초 후 재실측 — 클립 구동 변화)");
                var prefab = Resources.Load<GameObject>("Models/UserProvided/말/백마");
                if (prefab == null) Log("❌ 백마 프리팹 로드 실패(Resources 경로)");
                else
                {
                    var b = UnityEngine.Object.Instantiate(prefab, new Vector3(-6f, 0.1f, 5f), Quaternion.identity);
                    b.name = "Probe207_RawGLB";
                    SamplePose(b, "B(로우 GLB=애니 무)");
                    Capture(b, "no207_horseB_raw.png");
                }
                DumpCameras("t=9s(2차)");
            }
            catch (Exception e) { Log($"❌ phase2 예외: {e.GetType().Name}: {e.Message}"); }
        }
        else if (_phase == 2 && t >= 11f)
        {
            _phase = 3;
            Log("=== 프로브 종료 — 판정 근거는 본 리포트 + PNG 2장 ===");
            SessionState.SetBool(SessionKey, false);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }

    static void DumpCameras(string when)
    {
        try
        {
            var cams = Resources.FindObjectsOfTypeAll<Camera>();
            Log($"--- 카메라 전수 덤프({when}): {cams.Length}개 ---");
            foreach (var c in cams)
            {
                var go = c.gameObject;
                string sceneName = go.scene.IsValid() ? (string.IsNullOrEmpty(go.scene.name) ? "(DDOL/무명)" : go.scene.name) : "(invalid)";
                Log($"cam name={go.name} id={c.GetInstanceID()} enabled={c.enabled} active={go.activeInHierarchy} tag='{go.tag}' pos={c.transform.position:F2} euler={c.transform.eulerAngles:F0} scene={sceneName} ortho={c.orthographic}");
            }
            var main = Camera.main;
            Log($"Camera.main: {(main != null ? $"{main.gameObject.name} id={main.GetInstanceID()} pos={main.transform.position:F2}" : "null")}");

            MonoBehaviour pm = null;
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Name == "PlayerMovement") { pm = mb; break; }
            if (pm == null) { Log("PlayerMovement 인스턴스 미발견"); return; }
            var ty = pm.GetType();
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
            var fCam = ty.GetField("_camera", F);
            var fTr = ty.GetField("_cameraTransform", F);
            var pFollow = ty.GetProperty("FollowCamera", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Log($"PlayerMovement._camera={(RefId(fCam?.GetValue(pm) as Camera))} _cameraTransform={(RefId(fTr?.GetValue(pm) as Transform))} FollowCamera={(RefId(pFollow?.GetValue(null) as Camera))}");
        }
        catch (Exception e) { Log($"❌ DumpCameras 예외: {e.Message}"); }
    }

    static string RefId(Camera c) => c == null ? "null" : $"{c.gameObject.name}(id={c.GetInstanceID()}, pos={c.transform.position:F2})";
    static string RefId(Transform t) => t == null ? "null" : $"{t.gameObject.name}(id={t.gameObject.GetInstanceID()}, pos={t.position:F2})";

    static void SamplePose(GameObject g, string label)
    {
        try
        {
            var t = g.transform;
            Log($"--- 포즈 {label}: name={g.name} pos={t.position:F2} rotEuler={t.rotation.eulerAngles:F0} lossyScale={t.lossyScale:F2}");
            var rends = g.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Log($"  bounds center={b.center:F2} size={b.size:F2}");
            }
            foreach (var bone in t.GetComponentsInChildren<Transform>(true))
            {
                if (bone == t) continue;
                string n = bone.name;
                if (n.Contains("Root") || n.Contains("spine") || n.Contains("hips") || n.Contains("torso") || n.Contains("chest"))
                    Log($"  bone {n}: worldEuler={bone.rotation.eulerAngles:F0} localEuler={bone.localRotation.eulerAngles:F0} worldPos={bone.position:F2}");
            }
            var anim = g.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                Log($"  animator ctrl={anim.runtimeAnimatorController.name}");
                try
                {
                    var infos = anim.GetCurrentAnimatorClipInfo(0);
                    if (infos.Length > 0) Log($"  currentClip={infos[0].clip.name} len={infos[0].clip.length:F2}s");
                    else Log("  currentClip=(없음 — 재생 클립 0)");
                }
                catch (Exception e2) { Log($"  animator 상태 조회 실패: {e2.Message}"); }
            }
            else Log("  animator: 없음 또는 컨트롤러 미할당");
        }
        catch (Exception e) { Log($"❌ SamplePose 예외({label}): {e.Message}"); }
    }

    static void Capture(GameObject horse, string fileName)
    {
        try
        {
            var rends = horse.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) { Log($"⚠️ 캡처 스킵({fileName}) — 렌더러 0"); return; }
            var bb = rends[0].bounds;
            foreach (var r in rends) bb.Encapsulate(r.bounds);
            if (_probeCam == null)
            {
                var go = new GameObject("Probe207Cam");
                _probeCam = go.AddComponent<Camera>();
                _probeCam.fieldOfView = 50f;
                _probeCam.clearFlags = CameraClearFlags.Color;
                _probeCam.backgroundColor = new Color(0.08f, 0.08f, 0.16f);
            }
            Vector3 center = bb.center;
            _probeCam.transform.position = center + new Vector3(0f, 4.5f, -9f);
            _probeCam.transform.rotation = Quaternion.LookRotation(center - _probeCam.transform.position, Vector3.up);
            var rt = new RenderTexture(1024, 576, 24, RenderTextureFormat.ARGB32);
            var req = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            RenderPipeline.SubmitRenderRequest(_probeCam, req);
            RenderTexture.active = rt;
            var tex = new Texture2D(1024, 576, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1024, 576), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, fileName), tex.EncodeToPNG());
            Log($"캡처: {fileName} — target={horse.name} boundsSize={bb.size:F2} camPos={_probeCam.transform.position:F2}");
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(rt);
        }
        catch (Exception e) { Log($"❌ Capture 예외({fileName}): {e.Message}"); }
    }

    static void Log(string m)
    {
        Debug.Log($"{Tag} {m}");
        try { File.AppendAllText(ReportAbs, $"{DateTime.Now:HH:mm:ss} {m}\n"); } catch { /* 로그 파일 실패는 무시 */ }
    }
}

/// <summary>도메인 리로드 후 펌프 재구독 부트.</summary>
[InitializeOnLoad]
internal static class Probe207Boot
{
    static Probe207Boot() => Probe207.ResubscribeAfterReload();
}
