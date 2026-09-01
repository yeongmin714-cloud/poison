using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 배치로 MainScene을 열고 콜라이더들이 실제로 존재·활성인지 + Physics.Raycast로 잡히는지 검증.
/// 플레이어가 지면을 뚫고 SafetyFloor까지 추락하는 근본 원인 파악용.
/// 실행: Unity -batchmode -executeMethod GroundDiagRunner.Run
/// </summary>
public static class GroundDiagRunner
{
    public static void Run()
    {
        string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Debug.Log("[DiagCollider] 씬 로드됨: " + scene.name);

        // 1) 모든 Collider 나열
        var colliders = Object.FindObjectsOfType<Collider>();
        Debug.Log($"[DiagCollider] 씬 내 Collider 수 = {colliders.Length}");
        foreach (var c in colliders)
        {
            if (c == null) continue;
            string type = c.GetType().Name;
            Debug.Log($"[DiagCollider]  {c.gameObject.name} | {type} | enabled={c.enabled} activeInHierarchy={c.gameObject.activeInHierarchy} layer={c.gameObject.layer}");
        }

        // 2) 특정 위치의 CollisionFloor/지형 Raycast 테스트
        TestRaycast(new Vector3(728f, 5f, -529f), "현재스폰위(728,-529)");
        TestRaycast(new Vector3(0f, 5f, 0f), "중앙(0,0)");

        EditorApplication.Exit(0);
    }

    static void TestRaycast(Vector3 origin, string label)
    {
        // 아래로 10m Raycast
        bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit hitInfo, 10f, ~0, QueryTriggerInteraction.Ignore);
        if (hit)
        {
            Debug.Log($"[DiagCollider] Raycast {label}: HIT → '{hitInfo.collider?.gameObject.name}' y={hitInfo.point.y} ({hitInfo.collider?.GetType().Name})");
        }
        else
        {
            Debug.LogWarning($"[DiagCollider] Raycast {label}: MISS (10m 안 콜라이더 없음!)");
        }
    }
}