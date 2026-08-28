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
        foreach (var r in glbRenderers)
            if (r != null) r.enabled = false;
    }

    private void OnEnable()
    {
        foreach (var r in glbRenderers)
            if (r != null) r.enabled = false;
    }
}