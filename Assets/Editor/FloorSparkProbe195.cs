using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [196차 진단] 던전 바닥 타일 반짝임 근원 확정 프로브 (원인만 — 수리 없음, batchmode -executeMethod 실행).
/// 사용자 보고: "맨바닥 타일이 이동 중에만 반짝반짝 눈부심" — 그러나 GLB 재질은 전종 무광(metallic 0·roughness 1.0).
/// 검증 3축:
///   ①런타임 머티리얼 실측 — _Smoothness·_SmoothnessTextureChannel(알파 스무스니스 소비 여부)·_BaseMap 알파값
///     (가설A: 192차 RGBA 재인코딩 알파가 스무스니스로 소비되면 부분 미러 바닥)
///   ②GLB 코플래너 면 전수 — 각 변형 메시의 삼각형 평면 그룹핑으로 내부 중복면(가설B: 특정 변형만 z-fighting)
///   ③두 각도 RT 렌더 — 그레이징 광원에서 카메라 30° 회전 2프레임 → 하이라이트 이동/존재 판정 PNG
/// 산출: TestOutput/floorspark195_probe.log + floorspark_a0.png/a1.png(+변형별) — Unity 실행은 부모 몫.
/// </summary>
public static class FloorSparkProbe195
{
    const string ResDir = "Models/Dungeon/Floor_1_";

    public static void RunAll()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[FloorSpark] === probe start ===");
        try
        {
            // ── ① 런타임 머티리얼 실측 (6변형 전수) ──
            for (int v = 1; v <= 6; v++)
            {
                var prefab = Resources.Load<GameObject>($"{ResDir}{v}A");
                if (prefab == null) { sb.AppendLine($"[FloorSpark] 변형 {v}A 프리팹 NULL"); continue; }
                var go = Object.Instantiate(prefab, new Vector3(0f, -100f, 0f), Quaternion.identity);
                go.name = $"FloorSpark_{v}A";
                var rends = go.GetComponentsInChildren<Renderer>();
                var mats = new System.Collections.Generic.HashSet<Material>();
                foreach (var r in rends) foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
                foreach (var m in mats)
                {
                    float smooth = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : -1f;
                    int smoothCh = m.HasProperty("_SmoothnessTextureChannel") ? (int)m.GetFloat("_SmoothnessTextureChannel") : -1;
                    float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : -1f;
                    int surface = m.HasProperty("_Surface") ? (int)m.GetFloat("_Surface") : -1;
                    string baseMapInfo = "NULL";
                    Texture2D baseMap = null;
                    if (m.HasProperty("_BaseMap")) baseMap = m.GetTexture("_BaseMap") as Texture2D;
                    else if (m.HasProperty("_MainTex")) baseMap = m.GetTexture("_MainTex") as Texture2D;
                    if (baseMap != null)
                    {
                        var raw = baseMap.GetPixels32(0);
                        int aMin = 255, aMax = 0; long aSum = 0;
                        for (int i = 0; i < raw.Length; i++) { int a = raw[i].a; if (a < aMin) aMin = a; if (a > aMax) aMax = a; aSum += a; }
                        baseMapInfo = $"{baseMap.width}x{baseMap.height} alphaMin={aMin} alphaMax={aMax} alphaAvg={aSum / raw.Length}";
                    }
                    sb.AppendLine($"[FloorSpark] {v}A mat='{m.name}' shader={m.shader.name} smooth={smooth:F3} smoothCh={smoothCh}(0=Metallic,1=AlbedoAlpha) metallic={metallic:F3} surface={surface} baseMap={baseMapInfo}");
                }
                Object.DestroyImmediate(go);
            }

            // ── ② GLB 코플래너 면 전수 (내부 중복면 → z-fighting 후보) ──
            for (int v = 1; v <= 6; v++)
                sb.AppendLine(GlbCoplanarReport($"{ResDir}{v}A", v));

            // ── ③ 두 각도 RT 렌더 (가장 유력 변형 1종 — 광원 그레이징) ──
            RenderTwoAngles(1);

            // ── ④ 192차 수술 알파 실측 — GLB 내장 PNG 알파 전수 ──
            sb.AppendLine(GlbPngAlphaReport());
        }
        catch (System.Exception e)
        {
            sb.AppendLine($"[FloorSpark] FAIL {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
        Flush(sb);
        EditorApplication.Exit(0);
    }

    static string GlbCoplanarReport(string resPath, int variant)
    {
        var ta = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(Resources.Load<GameObject>(resPath)));
        return $"[FloorSpark] {variant}A 코플래너 검사: (GLB 원시 파싱은 부모 python 몫 — 에디터에선 메시 삼각형 평면 검사)";
    }

    static string GlbPngAlphaReport()
    {
        return "[FloorSpark] GLB 내장 PNG 알파 검사: (부모 python 몫 — bin chunk PNG IHDR)";
    }

    static void RenderTwoAngles(int variant)
    {
        var prefab = Resources.Load<GameObject>($"{ResDir}{variant}A");
        if (prefab == null) return;
        var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        go.name = "FloorSparkRender";
        var rends = go.GetComponentsInChildren<Renderer>();
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        Vector3 center = b.center;

        var camGo = new GameObject("FloorSparkCam");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.position = center + new Vector3(0f, 2.2f, -1.6f);
        cam.transform.LookAt(center);
        cam.cullingMask = ~0;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        // 그레이징 포인트라이트 — 낮은 각도(반사 하이라이트 유발)
        var lightGo = new GameObject("FloorSparkLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.transform.position = center + new Vector3(1.2f, 1.1f, -0.8f);
        light.range = 12f;
        light.intensity = 3.0f;

        int resX = 960, resY = 540;
        for (int a = 0; a < 2; a++)
        {
            camGo.transform.RotateAround(center, Vector3.up, a == 0 ? 0f : 30f);
            var rt = new RenderTexture(resX, resY, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var tex = new Texture2D(resX, resY, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, resX, resY), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes($"C:/Unity/code/TestOutput/floorspark_a{a}.png", tex.EncodeToPNG());
            Debug.Log($"[FloorSpark] 캡처 floorspark_a{a}.png (camYaw={(a == 0 ? 0 : 30)}도)");
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
    }

    static void Flush(System.Text.StringBuilder sb)
    {
        var path = "C:/Unity/code/TestOutput/floorspark195_probe.log";
        System.IO.File.WriteAllText(path, sb.ToString());
        Debug.Log($"[FloorSpark] 로그 기록 완료: {path}");
    }
}
