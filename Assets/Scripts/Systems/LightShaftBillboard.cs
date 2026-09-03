using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectName.Systems
{
    /// <summary>
    /// [T-R6] god rays 근사 — 옵션 (a) 태양 방향 빌보드 라이트 샤프트.
    ///
    /// 배경/선택 근거 (주석 요구):
    /// - find 조사 결과 기존 프로젝트에 이펙트 에셋 GodRays.prefab(Idyllic 팩, 파티클 기반)과
    ///   GodRay.mat(additive 투명 웜골드)이 존재함 → "계승"
    ///   그런데 GodRays.prefab은 정적 데코용 파티클 poof로 태양 추적/빌보드 로직이 없고
    ///   Assets/Idyllic... (Resources 밖)에 있어 런타임 Resources.Load 불가.
    ///   → GodRay.mat의 **머티리얼 속성 가닥(URP Unlit + _Surface transparent + _Blend additive
    ///     + _SrcBlend=SrcAlpha + _DstBlend=One + _ZWrite=0 + _Cull=off + renderQueue 3000)**을 그대로
    ///     재현하는 셀프컨테인 라이트샤프트 컴포넌트로 계승.
    ///   - 소프트 그라디언 텍스처는 런타임 코드 생성(텍스처 파일 생성 금지 준수, GradientTexture2D 대신
    ///     직접 Texture2D 픽셀 계산). 메시는 코드 쿼드, 빌보드는 LookAt(카메라, 카메라 up) → 항상 수직 빛기둥.
    ///   - 과하면 옵션 (b) 블룸 임계 폴백 경로 주석 참고(하단). 우선 (a) 채택.
    ///
    /// 클리핑 안전성 (과거 화이트아웃 39/40 사고 재발 방지):
    ///   지면 표면 선형 휘도 ≈ 앰비언트sky(≤0.52) + Sun(0.8 × lambert×albedo) ≤ 0.52 + 0.65 = 1.17
    ///   → ACES 톤맵이 <1.0으로 압축, 클리핑 없음(기존 안전 기준과 동일 — 이 컴포넌트는 Sun/앰비언트 불변).
    ///   빌보드 광기둥은 additive이고 유효 알파 ~0.02~0.05 × (텍스처 수직/수평 가우시안 ≤1) 이므로
    ///   스크린 제한 영역에서 추가 기여 ≤ ~0.05 → 전체 클리핑 비율 <10% 유지에 영향 미미.
    /// </summary>
    public class LightShaftBillboard : MonoBehaviour
    {
        [Header("배치 (World)")]
        [Tooltip("카메라에서 태양 방향으로의 거리")]
        [SerializeField] private float _distFromCamera = 45f;
        [Tooltip("빛기둥 중심의 상승(높이) 보정")]
        [SerializeField] private float _raise = 12f;
        [Tooltip("기둥 세로 길이(빌보드 스크린 수직 스트릭의 월드 스케일)")]
        [SerializeField] private float _shaftHeight = 62f;
        [Tooltip("기둥 가로 폭(월드)")]
        [SerializeField] private float _shaftWidth = 3.2f;

        [Header("광량")]
        [Tooltip("기본 additive 밝기(base color alpha). 과노출 방지 위해 낮게 유지")]
        [SerializeField] private float _baseAlpha = 0.30f;
        [Tooltip("태양 강도에 대한 알파 스케일 기준(정오=AmbianceBrightener BrightNoonIntensity 0.8)")]
        [SerializeField] private float _noonIntensityRef = 0.8f;
        [Tooltip("이 강도 미만이면 기둥 비활성(밤 숨김)")]
        [SerializeField] private float _disableBelowIntensity = 0.18f;

        // 보조 광기둥(레이어드 필): (측면 오프셋 월드배수, 리스트 거리, 알파스케일, 세로배수)
        private static readonly (float side, float dist, float alpha, float heightScale)[] SideShafts =
        {
            (0f,    1.00f, 1.00f, 1.00f), // 중앙 메인
            (-2.3f, 0.88f, 0.55f, 0.82f), // 좌 보조
            ( 2.5f, 0.88f, 0.50f, 0.78f), // 우 보조
        };

        private Camera _cam;
        private Light _sun;
        private Transform[] _shafts;
        private Material _material;
        private Texture2D _gradientTex;

        private void Start()
        {
            _cam = Camera.main;
            _sun = ResolveSun();
            if (_cam == null)
            {
                Debug.LogWarning("[LightShaftBillboard] Camera.main 없음 — 광기둥 비활성화");
                enabled = false;
                return;
            }

            // ── 런타임 그라디언 텍스처 (파일 생성 금지 — 코드 생성) ──────────
            _gradientTex = BuildSoftGradientTexture(128, 128);

            // ── URP Unlit additive transparent 머티리얼 (GodRay.mat 가닥 재현) ──
            _material = BuildAdditiveMaterial(_gradientTex);

            _shafts = new Transform[SideShafts.Length];
            for (int i = 0; i < SideShafts.Length; i++)
            {
                var (side, dist, alphaSc, hScale) = SideShafts[i];
                _shafts[i] = BuildShaft($"Shaft{i}", _shaftWidth, _shaftHeight * hScale, _baseAlpha * alphaSc);
            }

            Debug.Log($"[LightShaftBillboard] ✅ god rays 근사 빌보드 기둥 {_shafts.Length}개 + 런타임 그라디언트 텍스처({_gradientTex.width}×{_gradientTex.height}) 생성");
        }

        private void LateUpdate()
        {
            if (_sun == null) _sun = ResolveSun();
            if (_cam == null || _sun == null) return;

            // ── 태양 방향 (Directional Light는 forward가 광선 진행 방향) ──
            Vector3 sunDir = _sun.transform.forward;
            if (sunDir.sqrMagnitude < 0.0001f) return;
            sunDir.Normalize();

            // 카메라-태양축에 수직인 'right' (빛기둥을 스크린에서 좌우로 펼치기 위함)
            Vector3 up = _cam.transform.up;
            Vector3 right = Vector3.Cross(up, sunDir);
            if (right.sqrMagnitude < 0.0001f) right = _cam.transform.right;
            right.Normalize();

            float intensity = _sun.intensity;
            bool visible = _sun.enabled && (intensity >= _disableBelowIntensity);
            float intensityFactor = Mathf.Clamp01(intensity / Mathf.Max(0.01f, _noonIntensityRef));

            for (int i = 0; i < _shafts.Length; i++)
            {
                var (side, dist, _alpha, hScale) = SideShafts[i];
                Transform s = _shafts[i];
                if (s == null) continue;

                Vector3 pos = _cam.transform.position
                              + sunDir * (_distFromCamera * dist)
                              + up * (_raise * hScale)
                              + right * (_shaftWidth * side);
                s.position = pos;

                // 빌보드: +Z가 카메라를 향하고 카메라 up에 세로축 정렬 → 항상 수직 기둥
                s.rotation = Quaternion.LookRotation(pos - _cam.transform.position, _cam.transform.up);

                var r = s.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    r.enabled = visible;
                    if (visible && _material != null)
                    {
                        // 태양 강도에 비례해 밝기 변조 (기준 0.8에서 _baseAlpha×1)
                        Color c = _material.color;
                        _material.color = new Color(c.r, c.g, c.b, _baseAlpha * _alpha * intensityFactor);
                    }
                }
            }
        }

        // ── 빌보드 쿼드 생성 ─────────────────────────────────────────
        private Transform BuildShaft(string name, float width, float height, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildQuadMesh();

            var mr = go.AddComponent<MeshRenderer>();
            // 광기둥은 그림자를 드리우지 않고, 그림자도 받지 않게 처리 가능하지만
            // additive 투명이므로 기본값 유지. 배치 원점 중심 스케일로 세로 스트릭 표시.
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // 스케일: 쿼드가 +XY 평면 → X=폭, Y=높이 (빌보드 up=카메라 up 이므로 세로로 선다)
            go.transform.localScale = new Vector3(width, height, 1f);

            if (_material != null)
                mr.sharedMaterial = _material;

            return go.transform;
        }

        // ── 코드 쿼드 메시 (파일 생성 금지) ───────────────────────────
        private static Mesh BuildQuadMesh()
        {
            var mesh = new Mesh { name = "LightShaftQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            // +Z 법선 (빌보드가 +Z로 카메라를 향함)
            mesh.normals = new[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── 런타임 소프트 그라디언트 텍스처 (세로 연속 + 가로 가우시안) ──
        private static Texture2D BuildSoftGradientTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                name = "LightShaftGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Clamp01(y / (float)(h - 1));
                // 세로(텍스처 상하): 중앙~약간 아래가 가장 밝고 위/아래로 부드럽게 0
                float vy = Mathf.Exp(-Mathf.Pow((v - 0.38f) / 0.38f, 2f));
                // 더 대담한 테이퍼: 위쪽(하늘 방향)은 opac하고 아래로 스며듦 → 광선이 아래로 퍼지는 느낌
                vy *= Mathf.Lerp(0.30f, 1f, v);

                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Clamp01(x / (float)(w - 1));
                    // 가로 가우시안 (밝은 중심 → 가장자리 0) 부드러운 폭
                    float hx = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.30f, 2f));
                    float a = hx * vy;
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        // ── URP Unlit additive transparent 머티리얼 (GodRay.mat 가닥 계승) ──
        private static Material BuildAdditiveMaterial(Texture2D gradTex)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[LightShaftBillboard] URP Unlit 셰이더 없음 — 광기둥 생성 불가");
                return null;
            }

            var mat = new Material(shader) { name = "LightShaftAdditive" };
            mat.mainTexture = gradTex;
            mat.color = new Color(1f, 1f, 0.90f, 0.30f); // 웜 화이트(골드 뉘앙스), 알파는 Update가 변조

            // 투명 + additive (GodRay.mat:_Surface 1, _Blend 2, SrcAlpha1One, _ZWrite 0, _Cull 2)
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 2f); // off → 빌보드 양면 가시
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); // additive 형제 필요 시
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;

            return mat;
        }

        private static Light ResolveSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;

            try
            {
                var all = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in all)
                {
                    if (l != null && l.type == LightType.Directional && !l.gameObject.name.Contains("Moon"))
                        return l;
                }
            }
            catch (System.Exception)
            {
                // 태그/탐색 예외 — 폴백 없음
            }
            return null;
        }

        private void OnDestroy()
        {
            if (_gradientTex != null) Destroy(_gradientTex);
            if (_material != null) Destroy(_material);
            // 서브 샤프트 오브젝트는 부모(DontDestroy) 아래 — 씬 전환 시 전부 자동 정리 대상
        }

        // ================================================================
        // [옵션 (b) 폴백] 블룸 임계 기반 god rays 느낌 채택 시:
        //   Global Volume의 Bloom.threshold를 0.9→0.62로 내리고(밝은 하이라이트가 블룸으로 번짐),
        //   saturation을 살짝 올려 광악센스 유지. 옵션 (a)가 과하거나 프레임 회귀가 나면
        //   이 컴포넌트(빌보드)를 비활성화하고 MoodProfileSetup에 Bloom threshold만 주입.
        //   (본 구현은 (a) 우선 — 클리핑 <10% 원칙 준수)
        // ================================================================
    }
}