using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Z3.4 블롭 섀도우: 플레이어 접지감용 반투명 원형 그림자.
    /// 64x64 원형 그라디언트 텍스처를 런타임 생성(파일 생성 없음)해 URP Unlit
    /// 투명 머티리얼(알파 0.25, 검정, renderQueue 2985, ZWrite off)로 얹은
    /// 수평 쿼드를 플레이어 발 아래 지면(GetHeightAt + 0.05m)에 LateUpdate로 따라다니게 한다.
    /// 예외가 발생하면 조용히(경고 1회 후) 비활성화한다.
    /// </summary>
    public class BlobShadow : MonoBehaviour
    {
        const float RADIUS = 0.8f;   // AA3 09-04: 0.6→0.8 (접지감 그림자 강화)
        const float GROUND_BASE = 1f;      // 지형 메시 기저 (Ground_Inner)
        const int TEX_SIZE = 64;
        const float ALPHA = 0.35f;   // AA3 09-04: 0.25→0.35

        MeshRenderer _renderer;

        void Start()
        {
            try
            {
                Build();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BlobShadow] 생성 실패 — 비활성화: {e}");
                enabled = false;
            }
        }

        void LateUpdate()
        {
            if (_renderer == null || _renderer.transform == null) return;

            var p = transform.position;
            float groundY = GROUND_BASE + TerrainGenerator.GetHeightAt(p.x, p.z, BiomeType.Plains, 42);
            _renderer.transform.position = new Vector3(p.x, groundY + 0.05f, p.z);

            // 항상 수평 (법선 +Y) — 지형 경사와 무관하게 접지 계열 그림자
            _renderer.transform.rotation = Quaternion.identity;
        }

        void Build()
        {
            // ── 원형 그라디언트 텍스처 + URP Unlit 투명 머티리얼 (헬퍼 재사용) ──
            var tex = BuildGradientTexture(ALPHA);
            var mat = BuildMaterial(tex);
            if (mat == null) { enabled = false; return; }

            // ── 수평 쿼드 (지름 2r) ──
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);  // Quad는 기본 세로(법선 +Z)
            quad.name = "BlobShadow_Quad";
            quad.transform.SetParent(transform, worldPositionStays: false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 세로 Quad → 수평
            quad.transform.localScale = new Vector3(RADIUS * 2f, RADIUS * 2f, 1f);

            Object.Destroy(quad.GetComponent<Collider>());

            _renderer = quad.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = mat;

            // 초기 위치 설정
            var p = transform.position;
            float groundY = GROUND_BASE + TerrainGenerator.GetHeightAt(p.x, p.z, BiomeType.Plains, 42);
            _renderer.transform.position = new Vector3(p.x, groundY + 0.05f, p.z);
            _renderer.transform.rotation = Quaternion.identity;

            Debug.Log("[BlobShadow] ✅ 블롭 섀도우 생성 (64x64 그라디언트, 반경 0.8m, 알파 0.35 — AA3 09-04)");
        }

        /// <summary>64×64 원형 그라디언트 텍스처 (중앙 불투명 → 가장자리 투명, 파일 생성 없음).</summary>
        static Texture2D BuildGradientTexture(float alpha)
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            tex.name = "BlobShadow_Gradient";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[TEX_SIZE * TEX_SIZE];
            float inv = 1f / (TEX_SIZE * 0.5f);
            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    float dx = (x + 0.5f) - TEX_SIZE * 0.5f;
                    float dy = (y + 0.5f) - TEX_SIZE * 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) * inv; // 0 중심, 1 원둘레
                    float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, dist));
                    pixels[y * TEX_SIZE + x] = new Color(0f, 0f, 0f, alpha * a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>URP Unlit 투명 블롭 섀도우 머티리얼 (알파 블렌드, ZWrite off, renderQueue 2985).</summary>
        static Material BuildMaterial(Texture2D tex)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[BlobShadow] URP Unlit 셰이더 없음 — 비활성화");
                return null;
            }
            var mat = new Material(shader);
            mat.name = "BlobShadow_Mat";
            mat.SetTexture("_BaseMap", tex);
            mat.color = new Color(0f, 0f, 0f, 1f);          // 검정
            mat.SetFloat("_Surface", 1f);                   // Transparent
            mat.SetFloat("_Blend", 0f);                     // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);                       // ZWrite off
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            mat.renderQueue = 2985;                         // 지면 위, 일반 투명 부수픽션 위
            return mat;
        }

        /// <summary>
        /// CC3: 정적(Update 없는 1회 배치) 블롭 섀도우 — 나무/바위 데코에 접지감 그림자 부착.
        /// 호출 규격: 나무(반경 1.2m)/바위(0.9m), 알파 기본 0.3. 쿼드를 해당 데코에 부모로 삼되
        /// 데코 스케일을 상쇄해 세계 크기가 정확히 radius*2가 되도록 한다. 텍스처/머티리얼은
        /// 전 데코가 1장(alpha 0.3)을 공유 — GPU 배칭, 파일 생성 없음.
        /// </summary>
        static Texture2D _staticTex;
        static Material _staticMat;

        public static void AttachStatic(GameObject go, float radius, float alpha = 0.3f)
        {
            if (go == null || radius <= 0f) return;
            try
            {
                if (_staticMat == null)
                {
                    _staticTex = BuildGradientTexture(alpha);
                    _staticMat = BuildMaterial(_staticTex);
                }
                if (_staticMat == null) return;

                float s = go.transform.lossyScale.x;
                if (s <= 0.0001f) s = 1f;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "BlobShadow_Static";
                Object.Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(go.transform, false);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = new Vector3(radius * 2f / s, radius * 2f / s, 1f);
                quad.transform.localPosition = new Vector3(0f, 0.05f / s, 0f);
                quad.GetComponent<MeshRenderer>().sharedMaterial = _staticMat;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BlobShadow] AttachStatic 실패 — 스킵: " + e.Message);
            }
        }
    }
}