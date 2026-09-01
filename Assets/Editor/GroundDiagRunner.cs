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
        Debug.Log("[DiagPlan0] ===== 진단 종료 =====");
    }
}