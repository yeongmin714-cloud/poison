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
            // ── 원형 그라디언트 텍스처 (64x64, 파일 생성 없음) ──
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
                    // 중앙 불투명 → 가장자리로 갈수록 투명 (smoothstep)
                    float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, dist));
                    pixels[y * TEX_SIZE + x] = new Color(0f, 0f, 0f, ALPHA * alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            // ── URP Unlit 투명 머티리얼 ──
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[BlobShadow] URP Unlit 셰이더 없음 — 비활성화");
                enabled = false;
                return;
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
    }
}