using UnityEngine;

/// <summary>
/// Runtime component to keep GLB renderers disabled (capsule is the visual representation)
/// Place in a runtime assembly (not Editor)
/// </summary>
public class DisableGLBRenderers : MonoBehaviour
{
    public Renderer[] glbRenderers;

    private void Awake()
    {
        // AddComponent 직후에는 필드 대입 전에 Awake가 먼저 돌 수 있음 (Unity 순서) → null 가드 필수
        if (glbRenderers == null) return;
        foreach (var r in glbRenderers)
            if (r != null) r.enabled = false;
    }

    private void OnEnable()
    {
        if (glbRenderers == null) return;
        foreach (var r in glbRenderers)
            if (r != null) r.enabled = false;
    }
}