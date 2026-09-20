using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P20-7 — 우클릭 명령 지점 표시 링. SelectionRing 셰이더 우선, 없으면 절차 링 텍스처.
    /// 1.5초간 축소+페이드 후 소멸. 지면 0.03 위(짚단 데칼 위).
    /// </summary>
    public class CommandMarker : MonoBehaviour
    {
        private float _life = 1.5f;
        private float _elapsed;
        private Vector3 _startScale;
        private Renderer _rend;
        private Color _baseColor = new Color(1f, 0.82f, 0.35f);

        public static void Spawn(Vector3 position, Color? color = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CommandMarker";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = new Vector3(position.x, position.y + 0.03f, position.z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * 1.6f;
            var marker = go.AddComponent<CommandMarker>();
            if (color.HasValue) marker._baseColor = color.Value;
        }

        private void Start()
        {
            _rend = GetComponent<Renderer>();
            var shader = Shader.Find("Custom/SelectionRing");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetColor("_TeamColor", _baseColor);
                mat.SetFloat("_RingRadius", 0.8f);
                mat.SetFloat("_RingThickness", 0.12f);
                mat.SetFloat("_PulseSpeed", 0f);
                mat.SetFloat("_ArcSpeed", 1.5f);
            }
            else
            {
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(unlit != null ? unlit : Shader.Find("Sprites/Default"));
                mat.mainTexture = BuildRingTexture(256);
                mat.color = _baseColor;
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            if (_rend != null) _rend.sharedMaterial = mat;
            _startScale = transform.localScale;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _life);
            transform.localScale = _startScale * (1f - 0.45f * t);
            if (_rend != null)
            {
                var m = _rend.sharedMaterial;
                if (m.HasProperty("_TeamColor")) { var c = m.GetColor("_TeamColor"); c.a = 1f - t; m.SetColor("_TeamColor", c); }
                else if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = 1f - t; m.SetColor("_BaseColor", c); }
                else if (m.HasProperty("_Color")) { var c = m.GetColor("_Color"); c.a = 1f - t; m.SetColor("_Color", c); }
            }
            if (_elapsed >= _life) Destroy(gameObject);
        }

        private static Texture2D BuildRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f;
            float rOuter = c * 0.9f, rInner = c * 0.68f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = 0f;
                    if (d <= rOuter && d >= rInner) a = 1f;
                    else if (d > rOuter && d < rOuter + 4f) a = 1f - (d - rOuter) / 4f;
                    else if (d < rInner && d > rInner - 4f) a = 1f - (rInner - d) / 4f;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
