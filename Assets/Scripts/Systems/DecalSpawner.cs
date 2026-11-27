using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace ProjectName.Systems
{
    /// <summary>
    /// 지면 쿼드 절차 데칼 스포너 싱글톤 [58-P2] — 구 프로젝터 경로는 폐기(흰/검 표면 렌더 실측)
    /// BloodSplat, PoisonPuddle, Footprint 타입 지원
    /// 모든 텍스처는 코드로 절차적 생성 (외부 이미지 불필요)
    /// </summary>
    public class DecalSpawner : MonoBehaviour
    {
        // ================================================================
        // 싱글톤
        // ================================================================

        private static DecalSpawner _instance;
        private static bool _isQuitting;

        /// <summary>
        /// DecalSpawner 싱글톤 인스턴스
        /// </summary>
        public static DecalSpawner Instance
        {
            get
            {
                if (_isQuitting) return null;

                if (_instance == null)
                {
                    GameObject go = new GameObject("DecalSpawner");
                    _instance = go.AddComponent<DecalSpawner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ================================================================
        // 데칼 타입별 설정
        // ================================================================

        /// <summary>
        /// 데칼 타입별 설정 데이터
        /// </summary>
        [System.Serializable]
        public class DecalTypeConfig
        {
            public Material material;
            public Vector2 size = Vector2.one;
            public float lifetime = 10f;       // 자동 제거 시간 (초)
            public bool randomRotation = true;
            // [무번호 69-R3] 인스턴스 신선 텍스처 생성기 — 공유 런타임 텍스처의 GPU 업로드 실패 시
            //   샘플러가 직전 업로드 텍스처(슬래시 아크 웜화이트 아틀라스)로 별칭되는 실측(사용자 세션
            //   베이지 박스 (249,201,173) = 아크 웜화이트(255,244,214)×피격 비네트 틴트 α≈0.22 —
            //   런타임 바이섹션으로 Decal_ScorchMark 쿼드 특정)을 원천 차단한다. 데칼 스폰마다 신선
            //   생성+Apply → 바인딩 → 수명 말 인스턴스 머티리얼과 함께 파괴(누출 금지).
            public System.Func<Texture2D> textureGenerator;
        }

        [Header("데칼 타입 설정")]
        [SerializeField] private DecalTypeConfig _bloodSplatConfig;
        [SerializeField] private DecalTypeConfig _poisonPuddleConfig;
        [SerializeField] private DecalTypeConfig _footprintConfig;
        private DecalTypeConfig _scorchMarkConfig;   // [무번호 21-L2] 스콜치(전투 흔적) — 절차 생성 코드 전용

        [Header("풀링 설정")]
        [SerializeField] private int _maxDecals = 50;  // 최대 데칼 수

        // ================================================================
        // 내부 상태
        // ================================================================

        private readonly List<DecalInstance> _activeDecals = new List<DecalInstance>();
        private readonly Dictionary<string, DecalTypeConfig> _configMap = new Dictionary<string, DecalTypeConfig>();
        private static int _scorchBlockLogCount;   // [무번호 69-R6] 킬스위치 차단 로그 상한(스팸 방지, 3회 후 침묵)

        /// <summary>
        /// 활성 데칼 인스턴스
        /// </summary>
        private class DecalInstance
        {
            public GameObject gameObject;
            public MeshRenderer meshRenderer;   // [58-P2] 구 프로젝터 경로 → 지면 쿼드 MeshRenderer
            public float spawnTime;
            public float lifetime;
            public Material instanceMaterial;   // FA1: 인스턴스 머티리얼 (페이드아웃)
            public Color baseColor;             // 원본 베이스 컬러 (알파 램프다운 기준)
            public Texture2D instanceTexture;   // [69-R3] 인스턴스 신선 텍스처(수명 말 함께 파괴)
        }

        /// <summary>FA1: 수명 말미 알파 램프다운 구간(초)</summary>
        private const float FadeOutDuration = 2f;

        // ================================================================
        // Unity 생명주기
        // ================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // 절차적 머티리얼 생성
            InitializeMaterials();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void Update()
        {
            // 만료된 데칼 정리 + FA1: 수명 말미 2s 알파 램프다운(팝 소멸 금지)
            float now = Time.time;
            for (int i = _activeDecals.Count - 1; i >= 0; i--)
            {
                var inst = _activeDecals[i];
                float age = now - inst.spawnTime;
                if (age >= inst.lifetime)
                {
                    DestroyDecal(inst);
                }
                else if (inst.lifetime - age <= FadeOutDuration && inst.instanceMaterial != null)
                {
                    // FA1: 수명 말미 2s 알파 램프다운
                    float fade = Mathf.Clamp01((inst.lifetime - age) / FadeOutDuration);
                    inst.instanceMaterial.SetColor("_BaseColor", new Color(inst.baseColor.r, inst.baseColor.g, inst.baseColor.b, inst.baseColor.a * fade));
                }
            }
        }

        // ================================================================
        // 절차적 머티리얼 초기화
        // ================================================================

        /// <summary>
        /// 코드 생성 텍스처로 머티리얼 초기화
        /// </summary>
        private void InitializeMaterials()
        {
            // --- BloodSplat ---
            _bloodSplatConfig = new DecalTypeConfig
            {
                material = CreateDecalMaterial(CreateBloodSplatTexture(), "BloodSplatMat"),
                size = new Vector2(1.0f, 1.0f),
                lifetime = 30f,
                randomRotation = true
            };

            // --- PoisonPuddle ---
            _poisonPuddleConfig = new DecalTypeConfig
            {
                material = CreateDecalMaterial(CreatePoisonPuddleTexture(), "PoisonPuddleMat"),
                size = new Vector2(1.5f, 1.5f),
                lifetime = 25f,
                randomRotation = false
            };

            // --- Footprint ---
            _footprintConfig = new DecalTypeConfig
            {
                material = CreateDecalMaterial(CreateFootprintTexture(), "FootprintMat"),
                size = new Vector2(0.4f, 0.6f),
                lifetime = 60f,
                randomRotation = true
            };

            // --- ScorchMark [무번호 21-L2] — 근접 공격 명중 지점 그을음/크랙 흔적(전투 흔적) ---
            _scorchMarkConfig = new DecalTypeConfig
            {
                material = CreateDecalMaterial(CreateScorchTexture(), "ScorchMarkMat"),
                size = new Vector2(1.2f, 1.2f),
                lifetime = 10f,               // 풀링/maxDecals 50 선례 — 잔존 후 자동 정리
                randomRotation = true
            };
            // [무번호 69-R3] 스코치 방어 틴트 — 텍스처 샘플이 세션 이상으로 밝은 값(사용자 세션 베이지
            //   (249,201,173) 실측)을 반환해도 출력이 어두운 그을음 톤으로 고정되도록 _BaseColor를
            //   어두운 톤으로 강제(텍스처 자체가 암색이므로 배색 불변 — 이중 방어).
            if (_scorchMarkConfig.material != null)
            {
                var scorchTint = new Color(0.30f, 0.27f, 0.27f, 1f);
                _scorchMarkConfig.material.SetColor("_BaseColor", scorchTint);
                _scorchMarkConfig.material.color = scorchTint;
            }

            // 설정 맵 구축
            _configMap["BloodSplat"] = _bloodSplatConfig;
            _configMap["PoisonPuddle"] = _poisonPuddleConfig;
            _configMap["Footprint"] = _footprintConfig;
            _configMap["ScorchMark"] = _scorchMarkConfig;

            // [무번호 69-R3] 인스턴스 신선 텍스처 생성기 배선 — 스폰마다 신선 생성+바인딩(공유 텍스처
            //   별칭 실측 대응). 결정론(시드 고정/해시) — Random 0건 계약 유지.
            _bloodSplatConfig.textureGenerator = CreateBloodSplatTexture;
            _poisonPuddleConfig.textureGenerator = CreatePoisonPuddleTexture;
            _footprintConfig.textureGenerator = CreateFootprintTexture;
            _scorchMarkConfig.textureGenerator = CreateScorchTexture;

            Debug.Log("[DecalSpawner] 절차적 데칼 머티리얼 4종 생성 완료(BloodSplat/PoisonPuddle/Footprint/ScorchMark)");
        }

        /// <summary>
        /// URP/Lit 셰이더 기반 데칼 머티리얼 생성
        /// </summary>
        private Material CreateDecalMaterial(Texture2D texture, string name)
        {
            // [무번호 58-P2] 구 프로젝터 경로 폐기 — 58 프로브 실측: ①URP 내장 데칼 셰이더 = Shader.Find
            //   NULL(스트리핑·미로드) → 52-b의 1순위가 항상 실패하고 ②2순위 매뉴얼 카피 데칼 그래프의
            //   신규 머티리얼 기본값 = color(0,0,0,0)+mainTex null — 슬롯 프로퍼티 불일치로 절차
            //   텍스처가 무시되고, 히트 지점에 **거대 불투명 흰/검 표면**이 렌더(테스트 11 [51-SCREEN] 57,597px =
            //   화면 99.98% 실측 — "피격시마다 흰·검 덩어리"의 최종 뿌리).
            //   대체 = 지면 쿼드 + URP Particles/Unlit 투명(39차 EnsureUnlitTransparent 표준 세트) + 절차 텍스처 직접 바인딩.
            var urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urp == null)
            {
                Debug.LogWarning("[58-DECAL] URP Particles/Unlit 부재 — 절차 데칼 생성 스킵(파손 셰이더로 때우기 금지 — 52-b 계약 승계)");
                return null;
            }
            var material = new Material(urp);
            material.name = name;
            material.SetTexture("_BaseMap", texture);
            material.mainTexture = texture;
            material.color = Color.white;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.color);
            // 39차 투명 표준 세트 — 알파 무시(불투명 사각) 근원 차단
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 2990;   // 블롭 섀도우(2985) 위, 일반 투명(3000) 아래 — 지면 흔적 계층
            return material;
        }

        // ================================================================
        // 절차적 텍스처 생성
        // ================================================================

        /// <summary>
        /// 혈흔 텍스처 생성 (32x32, Perlin 노이즈 기반 랜덤 스플래터 패턴 + 빨간색)
        /// </summary>
        private Texture2D CreateBloodSplatTexture()
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "ProceduralBloodSplat";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;

            // 시드 기반 랜덤 스플래터
            System.Random rng = new System.Random(42);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;

                    // Perlin 노이즈로 불규칙한 가장자리
                    float noise = Mathf.PerlinNoise(x * 0.2f + 1.5f, y * 0.2f + 3.7f);
                    float noise2 = Mathf.PerlinNoise(x * 0.3f + 5.1f, y * 0.3f + 8.3f);

                    // 스플래터 형태: 중앙에서 방사형 + 노이즈 변형
                    float splatter = Mathf.Clamp01(1.0f - dist * (0.8f + noise * 0.4f));
                    splatter *= Mathf.Clamp01(0.7f + noise2 * 0.5f);

                    // 랜덤 방사형 돌기 (튀는 핏방울 효과)
                    float angle = Mathf.Atan2(dy, dx);
                    float radialNoise = Mathf.PerlinNoise(angle * 0.5f + 2.0f, 0.5f);
                    splatter = Mathf.Max(splatter, Mathf.Clamp01(0.8f - Mathf.Abs(dist - 0.6f - radialNoise * 0.3f) * 5f));

                    // 알파 채널
                    float alpha = Mathf.Clamp01(splatter * 0.9f);

                    // 빨간색 혈흔 (약간 어둡게)
                    float redBase = 0.5f + splatter * 0.4f;
                    float green = 0.02f + splatter * 0.05f;
                    float blue = 0.02f + splatter * 0.04f;

                    pixels[y * size + x] = new Color(
                        Mathf.Clamp01(redBase),
                        Mathf.Clamp01(green),
                        Mathf.Clamp01(blue),
                        Mathf.Clamp01(alpha)
                    );
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 독 웅덩이 텍스처 생성 (32x32, 초록색 글로우 패턴)
        /// </summary>
        private Texture2D CreatePoisonPuddleTexture()
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "ProceduralPoisonPuddle";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;

                    // Perlin 노이즈로 불규칙한 웅덩이 형태
                    float noise = Mathf.PerlinNoise(x * 0.25f + 2.0f, y * 0.25f + 5.0f);
                    float noise2 = Mathf.PerlinNoise(x * 0.4f + 7.0f, y * 0.4f + 1.0f);

                    // 웅덩이 형태 (가장자리 퍼짐)
                    float puddle = Mathf.Clamp01(1.0f - dist * (0.6f + noise * 0.5f));
                    puddle = Mathf.Clamp01(puddle + noise2 * 0.2f);

                    // 내부 버블 반짝임
                    float bubbleNoise = Mathf.PerlinNoise(x * 0.5f + 3.0f, y * 0.5f + 7.0f);
                    float glow = Mathf.Clamp01(bubbleNoise * 0.5f + 0.5f);

                    // 초록색 (독 효과)
                    float greenBase = 0.2f + puddle * 0.7f;
                    float red = 0.02f + puddle * 0.1f;
                    float blue = 0.02f + puddle * 0.15f;
                    float alpha = Mathf.Clamp01(puddle * 0.85f);

                    pixels[y * size + x] = new Color(
                        Mathf.Clamp01(red + glow * 0.1f),
                        Mathf.Clamp01(greenBase + glow * 0.2f),
                        Mathf.Clamp01(blue),
                        Mathf.Clamp01(alpha)
                    );
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 발자국 텍스처 생성 (16x16, 간단한 발바닥 형태)
        /// </summary>
        /// <summary>[무번호 21-L2] 스콜치 텍스처 128×128 — 중심 그을음 블롯(방사 감쇠) + 결정론 방사 크랙 라인
        ///   8본(해시 각도 — 랜덤 0건). 외곽 알파 페더로 지면과 소프트 블렌드(사각 엣지 방지).
        ///   [무번호 25-FA2] 고도화 — ①크랙 두께 테이퍼(중심 두껍→끝 가늘, 진행도 t) ②블롯 비대칭
        ///   (좌/우 다른 오프셋 — 결정론 해시). DarkBlend·해상도·randomRotation·수명 10s·maxDecals 50 무변경.</summary>
        private Texture2D CreateScorchTexture()
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.name = "ScorchMarkTex";
            var px = new Color[S * S];
            float c = S * 0.5f;
            // [무번호 25-FA2] 블롯 비대칭 오프셋 — 좌/우(및 수직) 서로 다른 값, 결정론 해시(랜덤 0건).
            float blotHashLeft  = Mathf.Repeat(Mathf.Sin(13.7f * 127.1f) * 43758.5453f, 1f);
            float blotHashRight = Mathf.Repeat(Mathf.Sin(41.3f * 127.1f) * 43758.5453f, 1f);
            float blotHashUp    = Mathf.Repeat(Mathf.Sin(77.9f * 127.1f) * 43758.5453f, 1f);
            float blotOffsetLeft  = (blotHashLeft  - 0.5f) * 0.16f;
            float blotOffsetRight = (blotHashRight - 0.5f) * 0.16f;
            float blotOffsetUp    = (blotHashUp    - 0.5f) * 0.10f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;   // 0..~1.41
                    // 그을음 블롯 — 중심 진한 검은색 → 외곽 소프트 페더
                    // [무번호 25-FA2] 블롯 중심 비대칭 — 좌/우 다른 오프셋(그을음 치우침), 크랙은 원중심 유지
                    float bdx = dx + (dx < 0f ? blotOffsetLeft : blotOffsetRight);
                    float bdy = dy + blotOffsetUp;
                    float bd = Mathf.Sqrt(bdx * bdx + bdy * bdy) / c;
                    float blot = Mathf.Clamp01(1f - bd);
                    blot = blot * blot * (3f - 2f * blot);
                    float a = blot * 0.75f;
                    // 방사 크랙 라인 8본 — 각도 해시(결정론), 중심 0.15~0.75 반경 구간
                    if (d > 0.12f && d < 0.78f)
                    {
                        float ang = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f;
                        float nearest = Mathf.Abs(Mathf.Repeat(ang * 8f + 0.5f, 1f) - 0.5f);
                        float cell = Mathf.Floor(ang * 8f);
                        float jitter = Mathf.Repeat(Mathf.Sin(cell * 91.7f) * 3758.5453f, 1f) * 0.06f - 0.03f;
                        // [무번호 25-FA2] 크랙 두께 테이퍼 — 시작(중심) 두껍게→끝 가늘게(진행도 t, 결정론)
                        float crackProg = Mathf.Clamp01((d - 0.12f) / 0.66f);
                        float crackTaper = Mathf.Lerp(0.026f, 0.007f, crackProg);
                        if (Mathf.Abs(nearest - 0.5f + jitter) < crackTaper)
                            a = Mathf.Max(a, 0.85f * (1f - crackProg) * 0.9f);
                    }
                    // 중심부는 크랙보다 블롯 우선(가장 검게)
                    px[y * S + x] = new Color(0.06f, 0.05f, 0.05f, Mathf.Clamp01(a));
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private Texture2D CreateFootprintTexture()
        {
            int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "ProceduralFootprint";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;

                    // 발바닥 모양: 타원형 + 발가락 영역
                    float heelDist = Mathf.Sqrt(
                        (dx * dx) / ((size * 0.35f) * (size * 0.35f)) +
                        ((dy + 2f) * (dy + 2f)) / ((size * 0.2f) * (size * 0.2f))
                    );

                    // 발가락 영역 (위쪽)
                    float toeDist = Mathf.Sqrt(
                        (dx * dx) / ((size * 0.2f) * (size * 0.2f)) +
                        ((dy - 3f) * (dy - 3f)) / ((size * 0.15f) * (size * 0.15f))
                    );

                    // 작은 발가락 4개
                    float toe1Dist = Mathf.Sqrt(((dx + 2.5f) * (dx + 2.5f)) / 1.5f + ((dy - 4f) * (dy - 4f)) / 1.0f);
                    float toe2Dist = Mathf.Sqrt(((dx + 0.8f) * (dx + 0.8f)) / 1.5f + ((dy - 4.5f) * (dy - 4.5f)) / 1.0f);
                    float toe3Dist = Mathf.Sqrt(((dx - 0.8f) * (dx - 0.8f)) / 1.5f + ((dy - 4.5f) * (dy - 4.5f)) / 1.0f);
                    float toe4Dist = Mathf.Sqrt(((dx - 2.5f) * (dx - 2.5f)) / 1.5f + ((dy - 4f) * (dy - 4f)) / 1.0f);

                    float footprint = Mathf.Clamp01(1.0f - heelDist)
                                   + Mathf.Clamp01(1.0f - toeDist) * 0.5f
                                   + Mathf.Clamp01(1.0f - toe1Dist) * 0.7f
                                   + Mathf.Clamp01(1.0f - toe2Dist) * 0.7f
                                   + Mathf.Clamp01(1.0f - toe3Dist) * 0.7f
                                   + Mathf.Clamp01(1.0f - toe4Dist) * 0.7f;

                    footprint = Mathf.Clamp01(footprint);

                    // 어두운 갈색 (진흙/흙)
                    float darkness = 0.1f + footprint * 0.5f;
                    float alpha = Mathf.Clamp01(footprint * 0.8f);

                    pixels[y * size + x] = new Color(
                        darkness * 0.5f,
                        darkness * 0.3f,
                        darkness * 0.15f,
                        alpha
                    );
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// [무번호 69-R6] 스코치 데칼 킬스위치 — 근원 오브젝트 미생성 계약.
        /// 근거: 사용자 세션 런타임 바이섹션 2회 연속 동일 근원 [Decal_ScorchMark_0/Decal_Quad]
        ///   (12:34 억제 시 6,502→34px / 12:43 억제 시 9,819→40px = 98~99% 붕괴), 그리고 R3 신선
        ///   텍스처+R4 방어 틴트 적용 세션(13:07 에디터 세션 — [69-DIAG] 종료 게이트 발동으로
        ///   R5 컴파일 확인)에서도 사용자가 베이지 잔존 보고. 인스턴스 머티리얼 출력=tex×_BaseColor
        ///   (0.30,0.27,0.27) 수학상 베이지 불가 → 실제 렌더가 인스턴스 경로 밖에서 개입되고 있다는
        ///   미확정 변수가 남음. 존재하지 않는 오브젝트는 렌더되지 않음 — 확정성 최대 원칙으로
        ///   스폰 자체를 차단한다. 혈흔/독/발자국은 바이섹션 미지목 — 유지.
        /// 재개 조건: [69-CULPRIT] 덤프로 근원 렌더 경로가 숫자로 확정된 후.
        /// </summary>
        public const bool ScorchMarkSpawnEnabled = false;

        /// <summary>
        /// 지정된 타입의 데칼을 위치/회전에 생성
        /// </summary>
        /// <param name="type">데칼 타입 ("BloodSplat", "PoisonPuddle", "Footprint")</param>
        /// <param name="position">월드 위치</param>
        /// <param name="rotation">회전</param>
        public void SpawnDecal(string type, Vector3 position, Quaternion rotation)
        {
            if (!_configMap.TryGetValue(type, out DecalTypeConfig config))
            {
                Debug.LogWarning($"[DecalSpawner] 알 수 없는 데칼 타입: {type}");
                return;
            }

            if (config.material == null)
            {
                Debug.LogWarning($"[DecalSpawner] {type} 머티리얼이 null입니다");
                return;
            }

            // [무번호 69-R6] 스코치 킬스위치 게이트 — 오브젝트 생성/레이캐스트 전에 차단(부작용 0).
            if (!ScorchMarkSpawnEnabled && type == "ScorchMark")
            {
                if (_scorchBlockLogCount < 3)
                {
                    Debug.Log("[69-R6] ScorchMark 스폰 차단(킬스위치) — 베이지 근원 오브젝트 미생성");
                    _scorchBlockLogCount++;
                }
                return;
            }

            // 최대 개수 초과 시 가장 오래된 데칼 제거
            while (_activeDecals.Count >= _maxDecals)
            {
                DestroyDecal(_activeDecals[0]);
            }

            // [무번호 59] 지면 스냅 — 하향 레이캐스트(5m), 실패 시 호출부 y 유지(폴백 — 무회귀).
            // 11-16 실측 — 미니지형(원점 표면 y=-0.37)에서 고정 y 데칼 파묻힘, FxGroundCrackFX 스냅 경로는 정상 관측.
            // XZ는 호출부 값 유지, Y만 스냅(FxGroundCrackFX는 hit.point 전체를 쓰지만 데칼은 호출부 XZ가 정답 위치).
            if (Physics.Raycast(position + Vector3.up * 0.2f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore))
                position = new Vector3(position.x, hit.point.y, position.z);

            // [무번호 58-P2] 지면 쿼드 데칼 — 구 프로젝터 경로 폐기(58 프로브 실측: 내장 Decal 셰이더 미로드 +
            //   매뉴얼 카피 폴백의 머티리얼 기본값 불일치 = 거대 흰/검 표면). 항상 수평 쿼드, 진입 회전은
            //   Yyaw만 채택(세로 회전을 그대로 두면 데칼이 서서 세로 사각이 된다 — footRot 호환).
            GameObject decalGO = new GameObject($"Decal_{type}_{_activeDecals.Count}");
            decalGO.transform.position = position + Vector3.up * 0.02f;   // 지면 언더 렌더 방지 리프트
            float yaw = rotation.eulerAngles.y + (config.randomRotation ? Random.Range(0f, 360f) : 0f);
            decalGO.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Decal_Quad";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(decalGO.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 세로 Quad → 수평(지면)
            quad.transform.localScale = new Vector3(config.size.x, config.size.y, 1f);
            var meshRenderer = quad.GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            // [58-P2] per-instance 머티리얼(FA1 페이드아웃 계약 승계) — config 머티리얼은 URP 투명+텍스처 바인딩 완결본
            Material instanceMat = config.material != null ? new Material(config.material) : null;
            if (instanceMat != null) meshRenderer.sharedMaterial = instanceMat;
            else Debug.LogWarning("[58-DECAL] 데칼 기본 머티리얼 부재 — 데칼 스킵");

            // 인스턴스 저장
            var instance = new DecalInstance
            {
                gameObject = decalGO,
                meshRenderer = meshRenderer,
                spawnTime = Time.time,
                lifetime = config.lifetime,
                instanceMaterial = instanceMat,
                baseColor = config.material != null && config.material.HasProperty("_BaseColor")
                    ? config.material.GetColor("_BaseColor") : Color.white
            };
            // [무번호 69-R3] 인스턴스 신선 텍스처 — 스폰 시점 신선 생성+Apply(강제 GPU 업로드)+슬롯
            //   재바인딩. 공유 텍스처의 세션별 샘플러 별칭(사용자 세션 베이지 박스 실측) 원천 차단.
            //   실패 시에도 기존 공유 텍스처 바인딩 유지(폴백 — 스폰 중단 금지).
            if (config.textureGenerator != null)
            {
                try
                {
                    var fresh = config.textureGenerator();
                    if (fresh != null)
                    {
                        fresh.name = $"DecalTex_{type}_{_activeDecals.Count}";
                        instanceMat.SetTexture("_BaseMap", fresh);
                        instanceMat.mainTexture = fresh;
                        instance.instanceTexture = fresh;
                    }
                }
                catch (System.Exception texEx)
                {
                    Debug.LogWarning($"[69-R3] 데칼 신선 텍스처 생성 실패(공유 텍스처 폴백): {texEx.Message}");
                }
            }
            _activeDecals.Add(instance);

            Debug.Log($"[DecalSpawner] 데칼 생성(지면 쿼드): {type} at {position}, 활성={_activeDecals.Count}/{_maxDecals}");
        }

        // ================================================================
        // 내부 메서드
        // ================================================================

        /// <summary>
        /// 데칼 제거 (풀에서 제거 + 파괴)
        /// </summary>
        private void DestroyDecal(DecalInstance instance)
        {
            if (instance == null) return;

            _activeDecals.Remove(instance);

            if (instance.gameObject != null)
            {
                // FA1: 인스턴스 머티리얼은 SpawnDecal에서 new Material로 생성 — 개별 파괴
                if (instance.instanceMaterial != null)
                {
                    Destroy(instance.instanceMaterial);
                }
                // [무번호 69-R3] 인스턴스 신선 텍스처 동반 파괴 — 누출 금지(정적 캐시 아님, 스폰별 생성).
                if (instance.instanceTexture != null)
                {
                    Destroy(instance.instanceTexture);
                }
                Destroy(instance.gameObject);
            }
        }
    }
}
