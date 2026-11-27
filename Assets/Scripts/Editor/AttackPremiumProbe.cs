using UnityEngine;
using UnityEditor;

/// <summary>
/// [프리미엄 프로브] 배치 진단 2건: ①P0 — Player_Rigged FBX 머티리얼/텍스처 바인딩 덤프(흰색 실루엣 원인 규명)
/// ②P1 — 프리미엄 텔레그래프 링 렌더 캡처(fill 0.5 지점 — 57° 사선 시점).
/// 에디터 모드(플레이 불요) — 결정론, Random 0건. 실행: -executeMethod AttackPremiumProbe.Run
/// </summary>
public static class AttackPremiumProbe
{
    public static void Run()
    {
        // ── P0: Player_Rigged FBX 머티리얼 진단 ──
        var log = new System.Text.StringBuilder();
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/UserProvided/fbx/Player_Rigged.fbx");
        if (fbx == null)
        {
            log.AppendLine("P0: FBX 로드 실패 — 경로 확인 필요");
        }
        else
        {
            var renderers = fbx.GetComponentsInChildren<Renderer>(true);
            log.AppendLine($"P0: renderers={renderers.Length}");
            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { log.AppendLine($"P0: {r.name} material=NULL"); continue; }
                    string baseMap = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "NULL";
                    string mainTex = m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null ? m.GetTexture("_MainTex").name : "NULL";
                    Color col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                    log.AppendLine($"P0: r={r.name} mat={m.name} shader={m.shader.name} baseMap={baseMap} mainTex={mainTex} color=({col.r:F2},{col.g:F2},{col.b:F2})");
                }
            }
            var instantiated = Object.Instantiate(fbx);
            foreach (var r in instantiated.GetComponentsInChildren<Renderer>(true))
            {
                log.AppendLine($"P0-inst: r={r.name} matCount={r.sharedMaterials.Length} mat0={(r.sharedMaterials[0] != null ? r.sharedMaterials[0].name + "/" + r.sharedMaterials[0].shader.name : "NULL")}");
            }
            Object.DestroyImmediate(instantiated);
        }
        Debug.Log("[AttackPremiumProbe] ===MATERIAL_DUMP===\n" + log.ToString());

        // ── P1: 프리미엄 텔레그래프 링 렌더 캡처 ──
        var ind = ProjectName.Systems.AttackTelegraphIndicator.Spawn(new Vector3(0f, 0f, 0f), 2.2f, 0.5f);
        if (ind == null)
        {
            Debug.LogError("[AttackPremiumProbe] 인디케이터 스폰 실패");
            return;
        }
        var comp = ind.GetComponent<ProjectName.Systems.AttackTelegraphIndicator>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var mf = typeof(ProjectName.Systems.AttackTelegraphIndicator).GetField("_instanceMat", flags);
        var mat = mf != null ? mf.GetValue(comp) as Material : null;
        if (mat != null)
        {
            mat.SetFloat("_Fill01", 0.5f);
            mat.SetFloat("_EdgePulse", 0.75f);
        }
        var camGO = new GameObject("ProbeCam");
        var cam = camGO.AddComponent<Camera>();
        cam.transform.position = new Vector3(0f, 10f, -5f);
        cam.transform.rotation = Quaternion.Euler(57f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.34f, 0.19f);
        var rt = new RenderTexture(800, 600, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(800, 600, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes("C:/Unity/code/TestOutput/telegraph_premium_probe.png", tex.EncodeToPNG());
        Debug.Log("[AttackPremiumProbe] ===CAPTURE=== saved TestOutput/telegraph_premium_probe.png");
        Debug.Log("[AttackPremiumProbe] DONE");
    }
}
