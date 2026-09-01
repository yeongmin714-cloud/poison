using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

/// <summary>
/// 배치 모드(-executeMethod GroundDiagRunner.Run)에서 MainScene을 연 뒤
/// Ground_Inner의 렌더 상태를 진단 로그로 출력한다.
/// Play 진입은 아니지만 씬 로드만으로도 셰이더/재질/메시/카메라/안개 상태를 읽을 수 있다.
/// 실제 Start() 로그(GroundRendererDiagnostic)는 Play에서도 동일하게 남는다.
/// </summary>
public static class GroundDiagRunner
{
    public static void Run()
    {
        string scenePath = "Assets/Scenes/MainScene.unity";
        Debug.Log("[DiagRunner] 씬 로드 시작: " + scenePath);
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[DiagRunner] 씬 열기 실패!");
            return;
        }

        Debug.Log("[DiagRunner] 씬 로드됨: " + scene.name);

        // Ground_Inner 탐색
        var ground = GameObject.Find("Ground_Inner");
        if (ground == null)
        {
            Debug.LogError("[DiagRunner] Ground_Inner를 찾지 못함!");
            return;
        }

        // 씬 로드 후 GroundRendererDiagnostic(MonoBehaviour Start)은 배치에서 자동 실행 안 되므로,
        // 여기서 직접 진단 로그를 남긴다.
        EmitDiagnostics(ground);

        Debug.Log("[DiagRunner] 진단 완료. Exiting.");
        EditorApplication.Exit(0);
    }

    /// <summary>씬 상태를 직접 읽어 렌더 관련 로그를 출력한다.</summary>
    static void EmitDiagnostics(GameObject ground)
    {
        Debug.Log("[DiagPlan0] ===== Ground 렌더 진단 (배치) =====");
        Debug.Log($"[DiagPlan0] GO={ground.name} activeInHierarchy={ground.activeInHierarchy} layer={ground.layer}({LayerMask.LayerToName(ground.layer)})");

        var mr = ground.GetComponent<MeshRenderer>();
        if (mr == null) Debug.LogError("[DiagPlan0] MeshRenderer 없음!");
        else if (!mr.enabled) Debug.LogWarning("[DiagPlan0] MeshRenderer disabled!");
        else
        {
            Debug.Log($"[DiagPlan0] MeshRenderer.enabled={mr.enabled}");
            if (mr.sharedMaterials != null && mr.sharedMaterials.Length > 0)
            {
                var m = mr.sharedMaterials[0];
                Debug.Log($"[DiagPlan0] Material[0].name='{m?.name}' shader='{m?.shader?.name}'");
                if (m != null)
                {
                    bool has = m.HasProperty("_BaseMap");
                    var tex = has ? m.GetTexture("_BaseMap") : null;
                    Debug.Log($"[DiagPlan0] HasProperty(_BaseMap)={has}");
                    if (tex != null)
                        Debug.Log($"[DiagPlan0] _BaseMap.name='{tex.name}' size={tex.width}x{tex.height}");
                    else
                        Debug.LogWarning("[DiagPlan0] _BaseMap null!");
                    string sh = m.shader?.name ?? "NULL";
                    Debug.Log($"[DiagPlan0] URP/Lit인가: {sh.Contains("Universal Render Pipeline")}");
                }
            }
            else Debug.LogWarning("[DiagPlan0] sharedMaterials 비어 있음!");
        }

        var mf = ground.GetComponent<MeshFilter>();
        if (mf == null) Debug.LogError("[DiagPlan0] MeshFilter 없음!");
        else
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) Debug.LogError("[DiagPlan0] Mesh null!");
            else
            {
                Debug.Log($"[DiagPlan0] Mesh.name='{mesh.name}' vertexCount={mesh.vertexCount} bounds={mesh.bounds} size={mesh.bounds.size} uvLen={mesh.uv?.Length}");
                if (mesh.bounds.size.x < 100f) Debug.LogWarning($"[DiagPlan0] 메시 X {mesh.bounds.size.x} — 매우 작음!");
            }
        }

        int myMask = 1 << ground.layer;
        foreach (var cam in Camera.allCameras)
        {
            if (cam == null) continue;
            int mask = (int)cam.cullingMask;
            Debug.Log($"[DiagPlan0] Camera '{cam.name}' cullingMask={mask} layerIncluded={(mask & myMask) != 0} near={cam.nearClipPlane} far={cam.farClipPlane} clearFlags={cam.clearFlags}");
        }

        Debug.Log($"[DiagPlan0] RenderSettings.fog={RenderSettings.fog} fogMode={RenderSettings.fogMode} fogDensity={RenderSettings.fogDensity} fogColor={RenderSettings.fogColor}");
        Debug.Log($"[DiagPlan0] currentRenderPipeline={(GraphicsSettings.currentRenderPipeline?.name ?? "NULL")}");

        // --- 5) 실제 렌더 픽셀 샘플링 (포스트/라이트/재질 최종 결과 반영) ---
        SampleRenderedPixels(ground);

        Debug.Log("[DiagPlan0] ===== 진단 종료 =====");
    }

    /// <summary>카메라로 1프레임 렌더링한 뒤 GameView 중앙/하단 픽셀의 RGB를 읽어 색을 판별한다.</summary>
    static void SampleRenderedPixels(GameObject ground)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[DiagPlan0] Main Camera 없음 — 픽셀 샘플 생략");
            return;
        }

        try
        {
            // RenderTexture에 카메라 렌더링 (현재 렌더 설정 그대로)
            int w = 640, h = 360;
            var rt = new RenderTexture(w, h, 24);
            var oldRT = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = oldRT;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = oldRT;

            // 하단 중앙 근처(화면 70~85% 세로, 중앙 x)가 지면이어야 함
            for (int rowPct = 50; rowPct <= 90; rowPct += 10)
            {
                int y = h * rowPct / 100;
                for (int xOff = -2; xOff <= 2; xOff++)
                {
                    int x = w / 2 + xOff * 40;
                    if (x < 0 || x >= w) continue;
                    var c = tex.GetPixel(x, y);
                    Debug.Log($"[DiagPlan0][PIXEL] row={rowPct}% x={x} center+{xOff}: R={c.r:F2} G={c.g:F2} B={c.b:F2}");
                }
            }

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DiagPlan0] 픽셀 샘플 실패: " + e.Message);
        }

        // --- 6) 포스트프로세싱 비활성 후 재샘플 비교 (포스트가 지형을 뭉개는지 판별) ---
        Debug.Log("[DiagPlan0] === 포스트프로세싱 OFF 대조 실험 ===");
        var vol = GameObject.Find("GlobalVolume");
        if (vol != null) { vol.SetActive(false); Debug.Log("[DiagPlan0] GlobalVolume 비활성화됨"); }
        // 카메라 post-processing 토글 off (URP)
        var camPP = cam;
        if (camPP != null)
        {
            // comp.youtube - UniversalAdditionalCameraData 통해 postProcessing off
            var udd = camPP.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (udd != null) { udd.renderPostProcessing = false; Debug.Log("[DiagPlan0] 카메라 renderPostProcessing=false"); }
        }
        SampleRenderedPixels(ground);

        // --- 7) 순수 초록 재질 강제 → 지형 메시/카메라가 제대로 그려지는지 판별 ---
        SamplePureGreen(ground);
    }

    /// <summary>지형 MeshRenderer에 순수 초록 URP/Lit(_BaseColor 0,1,0) 강제 적용 후 재렌더해 초록이 나오는지 판별.
    /// 초록으로 보이면 → 메시/카메라 정상(+재질 텍스처 문제). 회색이면 → 지형이 아예 안 그려지거나 라이트/메시가 죽은 것.</summary>
    static void SamplePureGreen(GameObject ground)
    {
        var mr = ground?.GetComponent<MeshRenderer>();
        if (mr == null) { Debug.LogWarning("[DiagPlan0][GREEN] MeshRenderer 없음"); return; }

        var oldMat = mr.sharedMaterial;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");

        var green = new Material(sh);
        green.name = "DiagPureGreen";
        if (sh != null && sh.name.Contains("Universal Render Pipeline/Lit"))
        {
            green.SetColor("_BaseColor", new Color(0f, 1f, 0f, 1f));
            green.SetTexture("_BaseMap", null);
        }
        else if (sh != null)
        {
            green.color = Color.green;
        }

        mr.sharedMaterial = green;
        Debug.Log("[DiagPlan0][GREEN] 지형을 순수 초록(0,1,0)으로 강제 적용");

        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[DiagPlan0][GREEN] 카메라 없음"); return; }
        try
        {
            int w = 640, h = 360;
            var rt = new RenderTexture(w, h, 24);
            var oldRT = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = oldRT;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = oldRT;

            for (int rowPct = 50; rowPct <= 75; rowPct += 5)
            {
                int y = h * rowPct / 100;
                var c = tex.GetPixel(w / 2, y);
                Debug.Log($"[DiagPlan0][GREEN] row={rowPct}% center R={c.r:F2} G={c.g:F2} B={c.b:F2} isGreen={(c.g > 0.5f && c.g > c.r + 0.15f)}");
            }

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DiagPlan0][GREEN] 실패: " + e.Message);
        }

        if (oldMat != null) mr.sharedMaterial = oldMat;
    }
}