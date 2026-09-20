using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// SC2/RTS식 부대 선택 하이라이트 링 — 셰이더(SelectionRing.shader)를 Quad에 입혀
    /// 발밑에 원형 링을 렌더한다. 콜라이더를 철저히 제거해 HoverTargetClassifier/RTS 레이캐스트를 오염시키지 않는다.
    /// 프리팹은 저작하지 않고 Awake에서 절차 생성(Quad + 머티리얼) — 에이전트 자율 저작의 .prefab 비신뢰성 회피.
    /// </summary>
    public class SelectionRingController : MonoBehaviour
    {
        [Tooltip("팀/국가 색 — SetColor로 주입")]
        public Color color = new Color(0.2f, 0.5f, 1f);

        private static Shader _shader;
        private Material _mat;
        private Renderer _rend;

        private void Awake()
        {
            if (_shader == null)
                _shader = Shader.Find("Custom/SelectionRing");

            // 발밑 지면 링 — Quad를 XZ 평면(위쪽 노멀)으로 눕힌다.
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "SelectionRingQuad";
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = Vector3.one;

            // 콜라이더 제거 — 커서 분류/명령 레이캐스트 오염 방지
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _rend = quad.GetComponent<Renderer>();
            if (_shader != null)
            {
                _mat = new Material(_shader);
                _mat.SetColor("_TeamColor", color);
                if (_rend != null) _rend.sharedMaterial = _mat;
            }
            else
            {
                // [P20-3 수리] 셰이더 부재 폴백 — 절차 링 텍스처(완전한 원) + Unlit 투명.
                //   기존엔 머티리얼 null → 기본 라이트 쿼드(사각) 노출 + IMGUI 원/EarthTrail 반원 폴백이 겹쳐 보였다.
                _mat = CreateFallbackRingMaterial();
                if (_rend != null) _rend.sharedMaterial = _mat;
            }
        }

        /// <summary>[P20-3] 절차 링 머티리얼 — 흰색 완전 원 텍스처(알파) + Unlit 틴트. SetColor로 색 변경.</summary>
        private Material CreateFallbackRingMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            var m = new Material(shader) { name = "SelectionRing_Fallback" };
            m.mainTexture = BuildRingTexture(256);
            m.color = color;
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            return m;
        }

        private static Texture2D BuildRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "SelectionRingTex" };
            float c = size * 0.5f;
            float rOuter = c * 0.92f, rInner = c * 0.72f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = 0f;
                    if (d <= rOuter && d >= rInner) a = 1f;
                    else if (d > rOuter && d < rOuter + 3f) a = 1f - (d - rOuter) / 3f;      // 외측 소프트
                    else if (d < rInner && d > rInner - 3f) a = 1f - (rInner - d) / 3f;     // 내측 소프트
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>팀/국가 색 주입 (생성 직후 GuardSelectionManager가 호출).</summary>
        public void SetColor(Color c)
        {
            color = c;
            if (_mat != null)
            {
                if (_mat.HasProperty("_TeamColor")) _mat.SetColor("_TeamColor", c);
                else _mat.color = c;   // [P20-3] 폴백(Unlit) 경로 — 틴트로 색 반영
            }
            if (_rend != null) _rend.enabled = true;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
