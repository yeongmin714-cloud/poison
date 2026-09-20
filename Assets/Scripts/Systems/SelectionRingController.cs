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
        private static Shader _shaderStatic;   // [P22-4] 팩토리 캐시
        private Material _mat;
        private Renderer _rend;

        /// <summary>[P22-4] 공용 링 머티리얼 팩토리 — 셰이더 우선, 실패 시 절차 링 텍스처 폴백.
        ///   PlayerRangeRing(무기 사거리 표시)도 동일 팩토리 사용(품질 통일). fallback: 텍스처 링 반지름 0.92.</summary>
        public static Material CreateRingMaterial(Color color)
        {
            var shader = Shader.Find("Custom/SelectionRing") ?? _shaderStatic;
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;

            bool isCustom = shader.name == "Custom/SelectionRing";
            var m = new Material(shader) { name = "SelectionRing" + (isCustom ? "_Shader" : "_Fallback") };
            if (isCustom)
            {
                m.SetColor("_TeamColor", color);
            }
            else
            {
                m.mainTexture = BuildRingTexture(256);
                m.color = color;
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
            }
            return m;
        }

        /// <summary>폴백 텍스처 링 여부 — 링 월드 반경 계산용(텍스처 링 반지름 = 쿼드 반의 0.92).</summary>
        public static bool IsFallback(Material m) => m != null && m.name.Contains("Fallback");

        private void Awake()
        {
            if (_shader == null)
                _shader = Shader.Find("Custom/SelectionRing");
            _shaderStatic = _shader;

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
            _mat = CreateRingMaterial(color);   // [P22-4] 공용 팩토리(셰이더/절차 폴백 통일)
            if (_rend != null && _mat != null) _rend.sharedMaterial = _mat;
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
