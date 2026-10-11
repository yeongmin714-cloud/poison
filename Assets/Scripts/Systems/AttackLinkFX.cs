using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [15차 B4] 공격 명령 연결선 — 명령 병사↔공격 지점을 잇는 얇은 액센트 라인(0.9s 페이드 후 소멸).
    /// LineRenderer + 인스턴스 Unlit 머티리얼(공유 변이 금지). 실패는 조용히 스킵.
    /// </summary>
    public static class AttackLinkFX
    {
        public static void Spawn(Vector3 from, Vector3 to)
        {
            try
            {
                var go = new GameObject("AttackLinkFX");
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.SetPosition(0, from + Vector3.up * 0.6f);
                lr.SetPosition(1, to + Vector3.up * 0.3f);
                lr.startWidth = 0.07f;
                lr.endWidth = 0.03f;
                lr.numCapVertices = 2;
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) { Object.Destroy(go); return; }
                var mat = new Material(shader) { name = "AttackLink_Mat" };
                mat.color = new Color(0.345f, 0.651f, 1f, 0.65f);
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
                lr.material = mat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                go.AddComponent<AttackLinkFader>().material = mat;
            }
            catch { /* FX 실패는 전투 방해 금지 원칙대로 흡수 */ }
        }
    }

    /// <summary>연결선 페이더 — 0.9s 알파 페이드 후 자가 소멸.</summary>
    public class AttackLinkFader : MonoBehaviour
    {
        public Material material;
        private float _elapsed;
        private const float Life = 0.9f;

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Life);
            if (material != null)
            {
                Color c = material.color;
                c.a = 0.65f * (1f - t);
                material.color = c;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
