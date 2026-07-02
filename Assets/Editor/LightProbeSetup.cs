using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 라이트 프로브와 리플렉션 프로브를 자동 배치하는 에디터 도구
/// </summary>
public static class LightProbeSetup
{
    // ================================================================
    // Auto-Place Light Probes
    // ================================================================

    [MenuItem("Tools/Graphics/Auto-Place Light Probes")]
    public static void AutoPlaceLightProbes()
    {
        // 1. "Interior" 태그 또는 레이어를 가진 모든 Renderer 찾기
        Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        List<Renderer> interiorRenderers = new List<Renderer>();

        foreach (Renderer r in allRenderers)
        {
            if (r.CompareTag("Interior") || r.gameObject.layer == LayerMask.NameToLayer("Interior"))
            {
                interiorRenderers.Add(r);
            }
        }

        if (interiorRenderers.Count == 0)
        {
            Debug.LogWarning("[LightProbeSetup] 'Interior' 태그 또는 레이어를 가진 Renderer를 찾을 수 없습니다.");
            EditorUtility.DisplayDialog("라이트 프로브 배치", "Interior 태그/레이어를 가진 오브젝트가 없습니다.", "OK");
            return;
        }

        Debug.Log($"[LightProbeSetup] Interior Renderer {interiorRenderers.Count}개 발견");

        // 2. Scene Root 찾기 (LightProbeGroup을 붙일 대상)
        GameObject sceneRoot = GameObject.Find("__SceneRoot");
        if (sceneRoot == null)
        {
            sceneRoot = new GameObject("__SceneRoot");
            Debug.Log("[LightProbeSetup] Scene Root GameObject 생성: __SceneRoot");
        }

        // 3. 기존 LightProbeGroup 제거 (재배치)
        LightProbeGroup existingGroup = sceneRoot.GetComponent<LightProbeGroup>();
        if (existingGroup != null)
        {
            Object.DestroyImmediate(existingGroup);
            Debug.Log("[LightProbeSetup] 기존 LightProbeGroup 제거");
        }

        // 4. 새 LightProbeGroup 추가
        LightProbeGroup probeGroup = sceneRoot.AddComponent<LightProbeGroup>();
        Debug.Log("[LightProbeSetup] LightProbeGroup 컴포넌트 추가됨");

        // 5. Interior 영역별 그룹화 (근접한 Renderer끼리 묶기)
        List<List<Renderer>> roomGroups = GroupRenderersByProximity(interiorRenderers, 3f);
        Debug.Log($"[LightProbeSetup] {roomGroups.Count}개 방(영역) 그룹 생성");

        // 6. 각 방의 Bounding Box 계산 후 4개 코너에 프로브 배치
        List<Vector3> probePositions = new List<Vector3>();

        foreach (var group in roomGroups)
        {
            if (group.Count == 0) continue;

            Bounds bounds = new Bounds(group[0].bounds.center, Vector3.zero);
            foreach (Renderer r in group)
            {
                bounds.Encapsulate(r.bounds);
            }

            // 경계 상자 내부에 4개 프로브 (코너)
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            // 바닥면 4개 코너 (y는 바닥 기준)
            float floorY = center.y - extents.y;
            float ceilingY = center.y + extents.y;
            float probeY = floorY + 0.3f; // 바닥에서 약간 위

            Vector3[] corners = new Vector3[]
            {
                new Vector3(center.x - extents.x * 0.8f, probeY, center.z - extents.z * 0.8f),
                new Vector3(center.x + extents.x * 0.8f, probeY, center.z - extents.z * 0.8f),
                new Vector3(center.x - extents.x * 0.8f, probeY, center.z + extents.z * 0.8f),
                new Vector3(center.x + extents.x * 0.8f, probeY, center.z + extents.z * 0.8f),
            };

            // 중간 높이에 추가 프로브 (선택)
            float midY = (floorY + ceilingY) * 0.5f;
            corners = corners.Concat(new Vector3[]
            {
                new Vector3(center.x, midY, center.z),
            }).ToArray();

            foreach (Vector3 pos in corners)
            {
                // 씬 범위 내에 있는지 확인
                if (IsValidProbePosition(pos))
                {
                    probePositions.Add(pos);
                }
            }

            Debug.Log($"[LightProbeSetup] 방 그룹: 중심 {center}, 크기 {extents}, 프로브 {corners.Length}개");
        }

        // 7. 프로브 위치 적용
        if (probePositions.Count > 0)
        {
            probeGroup.probePositions = probePositions.ToArray();
            EditorUtility.SetDirty(probeGroup);
            EditorUtility.SetDirty(sceneRoot);

            Debug.Log($"[LightProbeSetup] ✅ 총 {probePositions.Count}개 라이트 프로브 배치 완료");
            EditorUtility.DisplayDialog("라이트 프로브 배치",
                $"✅ {probePositions.Count}개 라이트 프로브 배치 완료\n" +
                $"{roomGroups.Count}개 방 그룹에서 생성됨",
                "OK");
        }
        else
        {
            Debug.LogWarning("[LightProbeSetup] 유효한 프로브 위치가 없습니다.");
            EditorUtility.DisplayDialog("라이트 프로브 배치", "유효한 프로브 위치가 없습니다.", "OK");
        }
    }

    [MenuItem("Tools/Graphics/Auto-Place Light Probes", true)]
    private static bool ValidateAutoPlaceLightProbes() => true;

    // ================================================================
    // Place Reflection Probe
    // ================================================================

    [MenuItem("Tools/Graphics/Place Reflection Probe")]
    public static void PlaceReflectionProbe()
    {
        // 현재 씬 카메라 찾기
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[LightProbeSetup] Main Camera를 찾을 수 없습니다.");
            EditorUtility.DisplayDialog("리플렉션 프로브", "Main Camera를 찾을 수 없습니다.", "OK");
            return;
        }

        // Reflection Probe 생성
        GameObject probeGO = new GameObject("Reflection Probe (Auto)");
        probeGO.transform.position = mainCamera.transform.position;

        ReflectionProbe reflectionProbe = probeGO.AddComponent<ReflectionProbe>();
        reflectionProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        reflectionProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        reflectionProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        reflectionProbe.resolution = 128;
        reflectionProbe.hdr = false;
        reflectionProbe.shadowDistance = 0f;
        reflectionProbe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
        reflectionProbe.cullingMask = ~0;
        reflectionProbe.intensityMultiplier = 1f;
        reflectionProbe.boxSize = new Vector3(20f, 10f, 20f);
        reflectionProbe.boxProjection = false;
        reflectionProbe.blendDistance = 1f;

        // 프로브 이름에 카메라 위치 기록
        probeGO.name = $"Reflection Probe ({mainCamera.transform.position.x:F1}, {mainCamera.transform.position.y:F1}, {mainCamera.transform.position.z:F1})";

        // 씬에서 선택
        Selection.activeGameObject = probeGO;
        SceneView.lastActiveSceneView?.FrameSelected();

        Debug.Log($"[LightProbeSetup] ✅ Reflection Probe 생성: 위치 {mainCamera.transform.position}, 해상도 128");
        EditorUtility.DisplayDialog("리플렉션 프로브",
            $"✅ Reflection Probe 생성 완료\n" +
            $"위치: {mainCamera.transform.position}\n" +
            $"해상도: 128",
            "OK");
    }

    [MenuItem("Tools/Graphics/Place Reflection Probe", true)]
    private static bool ValidatePlaceReflectionProbe() => true;

    // ================================================================
    // Helper Methods
    // ================================================================

    /// <summary>
    /// 근접한 Renderer들을 그룹화하여 방(room) 단위로 묶음
    /// </summary>
    private static List<List<Renderer>> GroupRenderersByProximity(List<Renderer> renderers, float maxDistance)
    {
        var groups = new List<List<Renderer>>();
        var ungrouped = new HashSet<Renderer>(renderers);

        while (ungrouped.Count > 0)
        {
            var group = new List<Renderer>();
            var seed = ungrouped.First();
            group.Add(seed);
            ungrouped.Remove(seed);

            bool added;
            do
            {
                added = false;
                var toAdd = new List<Renderer>();

                foreach (var r in group)
                {
                    foreach (var candidate in ungrouped.ToList())
                    {
                        if (Vector3.Distance(r.bounds.center, candidate.bounds.center) <= maxDistance)
                        {
                            toAdd.Add(candidate);
                        }
                    }
                }

                foreach (var r in toAdd)
                {
                    if (ungrouped.Remove(r))
                    {
                        group.Add(r);
                        added = true;
                    }
                }
            } while (added);

            groups.Add(group);
        }

        return groups;
    }

    /// <summary>
    /// 프로브 위치가 유효한지 확인 (NavMesh 위 또는 씬 경계 내)
    /// </summary>
    private static bool IsValidProbePosition(Vector3 position)
    {
        // 기본 검증: NaN/Infinity 체크
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
            return false;
        if (float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            return false;

        // 씬 경계 내 체크 (매우 큰 값 방지)
        const float maxSceneSize = 10000f;
        if (Mathf.Abs(position.x) > maxSceneSize ||
            Mathf.Abs(position.y) > maxSceneSize ||
            Mathf.Abs(position.z) > maxSceneSize)
            return false;

        return true;
    }
}
