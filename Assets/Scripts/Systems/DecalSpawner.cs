using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace ProjectName.Systems
{
    /// <summary>
    /// URP Decal Projector 기반 데칼 스포너 싱글톤
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
        }

        [Header("데칼 타입 설정")]
        [SerializeField] private DecalTypeConfig _bloodSplatConfig;
        [SerializeField] private DecalTypeConfig _poisonPuddleConfig;
        [SerializeField] private DecalTypeConfig _footprintConfig;

        [Header("풀링 설정")]
        [SerializeField] private int _maxDecals = 50;  // 최대 데칼 수

        // ================================================================
        // 내부 상태
        // ================================================================

        private readonly List<DecalInstance> _activeDecals = new List<DecalInstance>();
        private readonly Dictionary<string, DecalTypeConfig> _configMap = new Dictionary<string, DecalTypeConfig>();

        /// <summary>
        /// 활성 데칼 인스턴스
        /// </summary>
        private class DecalInstance
        {
            public GameObject gameObject;
            public DecalProjector projector;
            public float spawnTime;
            public float lifetime;
        }

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
            // 만료된 데칼 정리
            float now = Time.time;
            for (int i = _activeDecals.Count - 1; i >= 0; i--)
            {
                if (now - _activeDecals[i].spawnTime >= _activeDecals[i].lifetime)
                {
                    DestroyDecal(_activeDecals[i]);
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

            // 설정 맵 구축
            _configMap["BloodSplat"] = _bloodSplatConfig;
            _configMap["PoisonPuddle"] = _poisonPuddleConfig;
            _configMap["Footprint"] = _footprintConfig;

            Debug.Log("[DecalSpawner] 절차적 데칼 머티리얼 3종 생성 완료");
        }

        /// <summary>
        /// URP/Lit 셰이더 기반 데칼 머티리얼 생성
        /// </summary>
        private Material CreateDecalMaterial(Texture2D texture, string name)
        {
            // URP/Lit 사용 (DecalProjector와 호환)
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = name;
            material.mainTexture = texture;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);

            // 알파 블렌딩 설정
            material.SetFloat("_Surface", 1.0f);       // Transparent
            material.SetFloat("_Blend", 0.0f);         // Alpha
            material.SetFloat("_AlphaClip", 0.0f);     // Alpha clipping off
            material.SetFloat("_ZWrite", 0.0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // 셰이더 키워드 설정
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");

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

            // 최대 개수 초과 시 가장 오래된 데칼 제거
            while (_activeDecals.Count >= _maxDecals)
            {
                DestroyDecal(_activeDecals[0]);
            }

            // Decal Projector GameObject 생성
            GameObject decalGO = new GameObject($"Decal_{type}_{_activeDecals.Count}");
            decalGO.transform.position = position;

            // 랜덤 회전 적용
            if (config.randomRotation)
            {
                Quaternion randomRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                decalGO.transform.rotation = rotation * randomRot;
            }
            else
            {
                decalGO.transform.rotation = rotation;
            }

            // DecalProjector 컴포넌트 추가 및 설정
            DecalProjector projector = decalGO.AddComponent<DecalProjector>();
            projector.material = config.material;
            projector.size = new Vector3(config.size.x, config.size.y, 2f);
            projector.pivot = new Vector3(0f, 0f, 0f);
            projector.drawDistance = 50f;
            projector.fadeScale = 0.8f;
            projector.startAngleFade = 180f;
            projector.endAngleFade = 180f;
            projector.renderingLayerMask = 1u; // Default layer

            // 인스턴스 저장
            var instance = new DecalInstance
            {
                gameObject = decalGO,
                projector = projector,
                spawnTime = Time.time,
                lifetime = config.lifetime
            };
            _activeDecals.Add(instance);

            Debug.Log($"[DecalSpawner] 데칼 생성: {type} at {position}, 활성={_activeDecals.Count}/{_maxDecals}");
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
                // Material은 공유되므로 DestroyObject만
                Destroy(instance.gameObject);
            }
        }
    }
}
