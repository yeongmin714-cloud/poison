using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Plan 0 - Ground_Inner 지형이 Play에서 안 보이는 근본 원인을 한 번에 확정하는 런타임 진단.
/// Ground_Inner에 부착하고 Play 진입 시 즉시 실행. Start()에서 1회 로그를 남긴다.
/// 확인 포인트:
///   1) 실제 적용된 셰이더 / 재질 / _BaseMap 텍스처
///   2) 지형 메시 유효성(정점수·bounds·UV) — 2000m면 정상
///   3) 카메라 cullingMask가 Ground_Inner의 layer를 포함하는지(Occlusion/컬링 여부)
///   4) 안개 밀도 / RenderSettings를 로그
/// </summary>
public class GroundRendererDiagnostic : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("[DiagPlan0] ===== GroundRendererDiagnostic START =====");

        // --- 경고: GameObject의 active 상태 ---
        Debug.Log($"[DiagPlan0] GO={gameObject.name} activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy} layer={gameObject.layer}({LayerMask.LayerToName(gameObject.layer)})");

        // --- 1) MeshRenderer + 재질/셰이더/텍스처 ---
        var mr = GetComponent<MeshRenderer>();
        if (mr == null)
        {
            Debug.LogError("[DiagPlan0] MeshRenderer 없음!");
        }
        else if (!mr.enabled)
        {
            Debug.LogWarning("[DiagPlan0] MeshRenderer가 disabled!");
        }
        else
        {
            Debug.Log($"[DiagPlan0] MeshRenderer.enabled={mr.enabled} shadowCasting={mr.shadowCastingMode}");
            if (mr.sharedMaterials != null && mr.sharedMaterials.Length > 0)
            {
                var m = mr.sharedMaterials[0];
                Debug.Log($"[DiagPlan0] Material[0].name='{m?.name}' shader='{m?.shader?.name}'");
                if (m != null)
                {
                    bool hasBasemap = m.HasProperty("_BaseMap");
                    var tex = hasBasemap ? m.GetTexture("_BaseMap") : null;
                    Debug.Log($"[DiagPlan0] Material.HasProperty('_BaseMap')={hasBasemap}");
                    if (tex != null)
                    {
                        Debug.Log($"[DiagPlan0] _BaseMap.name='{tex.name}' size={tex.width}x{tex.height} wrapMode={tex.wrapMode}");
                    }
                    else
                    {
                        Debug.LogWarning($"[DiagPlan0] _BaseMap 텍스처 null! (URP/Lit가 검정/회색 렌더)");
                    }
                    // URP vs Standard 판별
                    string sh = m.shader?.name ?? "NULL";
                    bool isUrp = sh.Contains("Universal Render Pipeline");
                    Debug.Log($"[DiagPlan0] 셰이더가 URP/Lit인가: {isUrp}");
                    if (!isUrp)
                        Debug.LogWarning("[DiagPlan0] URP/Lit가 아님! → Standard 폴백이면 알베도 로직 다르게 동작.");
                }
            }
            else
            {
                Debug.LogWarning("[DiagPlan0] sharedMaterials가 비어 있음!");
            }
        }

        // --- 2) MeshFilter / 메시 유효성 ---
        var mf = GetComponent<MeshFilter>();
        if (mf == null)
        {
            Debug.LogError("[DiagPlan0] MeshFilter 없음!");
        }
        else
 {
            var mesh = mf.sharedMesh;
            if (mesh == null)
            {
                Debug.LogError("[DiagPlan0] Mesh null!");
            }
            else
            {
                Debug.Log($"[DiagPlan0] Mesh.name='{mesh.name}' vertexCount={mesh.vertexCount} subMeshCount={mesh.subMeshCount} uvLength={mesh.uv?.Length} bounds={mesh.bounds} / Center={mesh.bounds.center} Size={mesh.bounds.size}");
                // 2000m 지형이면 size.x/z 근처여야 정상
                if (mesh.bounds.size.x < 100f)
                    Debug.LogWarning($"[DiagPlan0] 메시 X 크기 {mesh.bounds.size.x:F1}m — 매우 작음! 지형이 안 보이는 원인일 수 있음");
                if (mesh.uv == null || mesh.uv.Length < mesh.vertexCount)
                    Debug.LogWarning("[DiagPlan0] UV 없음/불완전 — 텍스처 타일링 불가");
            }
        }

        // --- 3) 카메라 cullingMask vs 이 오브젝트 layer ---
        int myLayerMask = 1 << gameObject.layer;
        foreach (var cam in Camera.allCameras)
        {
            if (cam == null) continue;
            int mask = (int)cam.cullingMask;
            bool included = (mask & myLayerMask) != 0;
            Debug.Log($"[DiagPlan0] Camera '{cam.name}' cullingMask={mask} layerIncluded={included} near={cam.nearClipPlane} far={cam.farClipPlane} clearFlags={cam.clearFlags}");
            if (!included)
                Debug.LogWarning($"[DiagPlan0] 이 오브젝트 레이어({gameObject.layer})가 카메라 cullingMask에 없음 → 렌더 안 됨!");
        }

        // --- 4) 렌더 설정(안개/파이프라인) ---
        Debug.Log($"[DiagPlan0] RenderSettings.fog={RenderSettings.fog} fogMode={RenderSettings.fogMode} fogDensity={RenderSettings.fogDensity} fogColor={RenderSettings.fogColor}");
        Debug.Log($"[DiagPlan0] GraphicsSettings.defaultRenderPipeline={(GraphicsSettings.defaultRenderPipeline?.name ?? "NULL")} currentRenderPipeline={(GraphicsSettings.currentRenderPipeline?.name ?? "NULL")}");
        Debug.Log($"[DiagPlan0] QualitySettings.renderPipeline={(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline?.name ?? "N/A")}");

        Debug.Log("[DiagPlan0] ===== GroundRendererDiagnostic END =====");
    }
}