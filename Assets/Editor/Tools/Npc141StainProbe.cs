using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [141차 확정프로브] 인게임 얼룩 원인 확정 — 교차바인딩 노멀맵 가설 검증.
/// 가설: 전 rigged FBX 머티리얼의 _BumpMap이 Npc_Boy_1/normal.png로 교차 바인딩됨(실측 완료)
///  → 자기 UV와 다른 캐릭터 노멀맵 샘플링 = Lit 전용 얼룩(웹 프리뷰 청결과 정합).
/// 변인: (1)현상태(Boy_1 normal) (2)자기 normal (3)노멀 없음 (4)Unlit — 2각도 렌더.
/// 추가 실측: 메시 탄젠트 유효성(전부 0이면 별개 원인), 머티리얼 프로퍼티.
/// 실행: batchmode -executeMethod Npc141StainProbe.RunProbe
/// 산출: TestOutput/npc141_probe/*.png + npc141_stain_probe.txt
/// </summary>
public static class Npc141StainProbe
{
    public static void RunProbe()
    {
        var log = new StringBuilder();
        log.AppendLine("[141차 프로브] 얼룩 원인 확정 — " + System.DateTime.Now.ToString("HH:mm:ss"));
        System.IO.Directory.CreateDirectory("TestOutput/npc141_probe");

        const string stem = "Npc_Man_2";   // Villager_7 매핑 대상(얼룩 재현 대상)
        string fbxPath = $"Assets/Resources/Models/UserProvided/Npc2026/{stem}/{stem}_rigged.fbx";
        var srcGo = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (srcGo == null) { Debug.LogError("[141차] FBX 로드 실패"); return; }

        // ── 메시 데이터 실측: 탄젠트 유효성 ──
        foreach (var mf in srcGo.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh;
            var tan = m.tangents;
            int zeroTan = 0;
            if (tan != null)
                for (int i = 0; i < tan.Length; i++)
                    if (tan[i].sqrMagnitude < 0.001f) zeroTan++;
            log.AppendLine($"[141차] 메시 {m.name} vert={m.vertexCount} tangent={tan?.Length ?? -1} zeroTan={zeroTan}");
        }

        // ── 런타임 동등 머티리얼 구성 ──
        var baseTex = Resources.Load<Texture2D>($"Models/UserProvided/Npc2026/Textures/{stem}/base_color");
        var selfNormal = Resources.Load<Texture2D>($"Models/UserProvided/Npc2026/Textures/{stem}/normal");
        var boyNormal = Resources.Load<Texture2D>("Models/UserProvided/Npc2026/Textures/Npc_Boy_1/normal");
        log.AppendLine($"[141차] 로드 — base={baseTex?.width}x{baseTex?.height} selfNormal={(selfNormal != null ? selfNormal.width + "x" + selfNormal.height : "null")} boyNormal={boyNormal?.width}x{boyNormal?.height}");

        var go = Object.Instantiate(srcGo);
        go.name = "Probe_" + stem;
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null) { Debug.LogError("[141차] SMR 부재"); return; }

        // 런타임과 동일: FBX 서브어셈블리 머티리얼 복제 + 자기 텍스처 바인딩(_BumpMap은 원본 유지 = 교차상태)
        var srcMat = smr.sharedMaterial;
        var mat = new Material(srcMat);   // [126차 규약] 명시적 인스턴스
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseTex);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseTex);
        smr.sharedMaterial = mat;
        string srcBump = (srcMat.HasProperty("_BumpMap") && srcMat.GetTexture("_BumpMap") != null)
            ? srcMat.GetTexture("_BumpMap").name : "null";
        log.AppendLine("[141차] 시작 머티리얼 — srcMat._BumpMap=" + srcBump + " mat.color=" + mat.color);

        // ── 씬 셋업: 방향광 + 평탄 앰비언트 ──
        var lightGo = new GameObject("ProbeLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;   // 그림자 변수 격리 — 순수 셰이딩만
        lightGo.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);

        // NPC 프레이밍
        var bounds = new Bounds(smr.bounds.center, smr.bounds.size);
        if (bounds.size.magnitude < 0.1f) bounds = new Bounds(go.transform.position, Vector3.one * 2f);
        Vector3 center = bounds.center;
        float dist = Mathf.Max(bounds.size.magnitude * 1.6f, 2.5f);

        var camGo = new GameObject("ProbeCam");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.2f, 0.25f);

        string[] variantNames = { "A1_lit_boyNormal(현상태)", "A2_lit_selfNormal", "A3_lit_noNormal", "A4_unlit" };
        float[] yaws = { 25f, -55f };   // 정면 3/4 + 측면

        int frame = 0;
        for (int v = 0; v < 4; v++)
        {
            // 변인 적용
            if (v == 0) { if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", boyNormal); mat.shader = Shader.Find("Universal Render Pipeline/Lit"); }
            if (v == 1) { if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", selfNormal); mat.shader = Shader.Find("Universal Render Pipeline/Lit"); }
            if (v == 2) { if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null); mat.shader = Shader.Find("Universal Render Pipeline/Lit"); }
            if (v == 3) mat.shader = Shader.Find("Universal Render Pipeline/Unlit");

            for (int a = 0; a < yaws.Length; a++)
            {
                var rot = Quaternion.Euler(8f, yaws[a], 0f);
                camGo.transform.position = center + rot * new Vector3(0, 0, -dist);
                camGo.transform.LookAt(center);
                var rt = new RenderTexture(1024, 1024, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes($"TestOutput/npc141_probe/v{v}_{variantNames[v]}_yaw{yaws[a]}.png", tex.EncodeToPNG());
                RenderTexture.active = null;
                rt.Release();
                Object.DestroyImmediate(tex);
                frame++;
            }
        }
        log.AppendLine($"[141차] 렌더 {frame}컷 완료 → TestOutput/npc141_probe/");

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(lightGo);
        Object.DestroyImmediate(camGo);
        Debug.Log(log.ToString());
        System.IO.File.WriteAllText("TestOutput/npc141_stain_probe.txt", log.ToString());
    }
}
