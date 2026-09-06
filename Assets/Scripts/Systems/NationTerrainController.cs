using System.Collections;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 3.6.7 / C22-09: Nation-specific terrain texture controller.
    /// Attached to the Ground GameObject. On Awake/Start, it generates
    /// procedural textures that combine the 3-ring zone system with
    /// nation-specific color tints, applied based on world position.
    ///
    /// C22-09 Enhancement: Smooth terrain transition when player moves
    /// between nation territories — store previous/target nation and
    /// lerp texture blend over 2 seconds via SmoothNationTransition().
    ///
    /// Ring zones (distance from center):
    ///   Ring1: 0-350m   — brown mud+leaves
    ///   Ring2: 350-700m — rocky terrain
    ///   Ring3: 700-1000m — coast sand+rocks
    ///
    /// Nation territories (direction from center):
    ///   East   = x+ (green grassland)
    ///   West   = x- (yellow desert)
    ///   South  = z- (red volcanic)
    ///   North  = z+ (SNOW WHITE — 설원 고정 강화)
    ///   Empire = center region (golden)
    ///
    /// The texture is a single 256x256 procedurally generated map that
    /// composites ring zone coloring with nation-specific tint at each
    /// pixel. UV coordinates are mapped to world-space positions across the
    /// full ±1600m chunk world (3200x3200m, 64 chunks — uv = world/3200 + 0.5).
    ///
    /// 2026-09 (방위색 고정 표시): UpdateForCurrentNation()의 전역 재생성은
    /// 호출부 제거로 비활성 — 결합(방위 고정) 텍스처가 항상 유지된다.
    /// 함수 자체는 C22-09 스무스 전환 API로 보존.
    /// </summary>
    public class NationTerrainController : MonoBehaviour
    {
        [Header("Texture Settings")]
        [SerializeField] private int _textureSize = 256;
        [SerializeField] private float _textureTiling = 200f;

        [Header("Ring Zone Colors")]
        [SerializeField] private Color _ring1Color = new Color(0.45f, 0.30f, 0.15f); // brown_mud_leaves
        [SerializeField] private Color _ring2Color = new Color(0.40f, 0.35f, 0.30f); // rocky_terrain
        [SerializeField] private Color _ring3Color = new Color(0.70f, 0.60f, 0.40f); // coast_sand_rocks

        [Header("Nation Tint Colors")]
        [SerializeField] private Color _eastTint = new Color(0.20f, 0.55f, 0.15f);   // green grassland
        [SerializeField] private Color _westTint = new Color(0.75f, 0.65f, 0.20f);  // yellow desert
        [SerializeField] private Color _southTint = new Color(0.55f, 0.15f, 0.10f); // red volcanic
        [SerializeField] private Color _northTint = new Color(0.93f, 0.95f, 0.98f); // north snow — 설원 백색(강화)
        [SerializeField] private Color _empireTint = new Color(0.85f, 0.72f, 0.18f); // golden
        [SerializeField] private Color _draculaTint = new Color(0.25f, 0.05f, 0.05f); // dark red/black

        [Header("Nation Overlay")]
        [SerializeField, Range(0f, 1f)] private float _baseTintStrength = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _tintNoiseVariation = 0.25f;

        [Header("North Snow Fix (설원 고정 강화)")]
        // 북쪽 픽셀만 tint 강도를 이 값으로 수렴시켜 방위색이 링 베이스에 묻히지 않게 한다.
        [SerializeField, Range(0f, 1f)] private float _northTintStrength = 0.9f;

        [Header("C22-09: Smooth Transition")]
        [SerializeField] private float _transitionDuration = 2.0f;

        // C22-09: Smooth transition state
        private NationType _previousNation = NationType.Empire;
        private NationType _currentNation = NationType.Empire;
        private Texture2D _previousTexture;
        private Texture2D _targetTexture;
        private Material _terrainMaterial;
        private bool _isTransitioning = false;
        private Coroutine _transitionCoroutine;

        // Cached nation tint lookup
        private Color GetNationTint(NationType nation)
        {
            switch (nation)
            {
                case NationType.East:   return _eastTint;
                case NationType.West:   return _westTint;
                case NationType.South:  return _southTint;
                case NationType.North:  return _northTint;
                case NationType.Empire: return _empireTint;
                case NationType.Dracula: return _draculaTint;
                default:                return Color.white;
            }
        }

        /// <summary>Dracula tint color (readonly for tests).</summary>
        public Color DraculaTint => _draculaTint;

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>Texture size (readonly for tests)</summary>
        public int TextureSize => _textureSize;

        /// <summary>Ring 1 color (readonly for tests)</summary>
        public Color Ring1Color => _ring1Color;

        /// <summary>East tint color (readonly for tests)</summary>
        public Color EastTint => _eastTint;

        /// <summary>Current nation (readonly for tests).</summary>
        public NationType CurrentNation => _currentNation;

        /// <summary>Previous nation (readonly for tests).</summary>
        public NationType PreviousNation => _previousNation;

        /// <summary>Whether a transition is in progress.</summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>Transition duration setting.</summary>
        public float TransitionDuration => _transitionDuration;

        /// <summary>The terrain material (readonly for tests).</summary>
        public Material TerrainMaterial => _terrainMaterial;

        // ================================================================
        //  Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            ApplyNationTerrainTexture();
        }

        // Start() 제거(2026-09): TerritoryManager 기반 UpdateForCurrentNation 전역 재생성은
        // 플레이어 현재 국가 텍스처로 결합 맵을 통째로 덮어써 방위 고정 색을 파괴했다.
        // 방위색은 플레이어 위치와 무관하게 방위별로 고정 표시되어야 하므로 호출부를 제거했다.

        /// <summary>
        /// Applies a combined ring-zone + nation-specific procedural texture
        /// to this GameObject's material.
        /// </summary>
        public void ApplyNationTerrainTexture()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.Log("[NationTerrainController] Adding MeshRenderer to Ground.");
                renderer = gameObject.AddComponent<MeshRenderer>();
            }

            // Ensure MeshFilter exists for MeshRenderer to work
            var filter = GetComponent<MeshFilter>();
            if (filter == null)
            {
                Debug.Log("[NationTerrainController] Adding MeshFilter to Ground.");
                filter = gameObject.AddComponent<MeshFilter>();
            }

            // Assign default Plane mesh if no mesh assigned
            if (filter.sharedMesh == null)
            {
                filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx") ?? Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                Debug.Log("[NationTerrainController] Assigned default Plane mesh to Ground.");
            }

            _terrainMaterial = renderer.sharedMaterial;
            if (_terrainMaterial == null)
            {
                // Create a fresh URP/Lit material
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                _terrainMaterial = new Material(shader);
                _terrainMaterial.name = "Ground_NationTerrain_Mat";
                renderer.sharedMaterial = _terrainMaterial;
            }

            // Create procedural texture
            Texture2D tex = GenerateCombinedTexture();
            _terrainMaterial.mainTexture = tex;
            // 결합 텍스처는 ±1600m 월드 전체를 1장으로 커버하는 방위 고정 맵 —
            // uv 0..1에 1:1 매핑되어야 방위색이 지역별로 고정된다.
            // (구 _textureTiling=200은 16m 주기 반복 타일이라 방위색이 반복되어 고정 불가)
            _terrainMaterial.mainTextureScale = Vector2.one;
            _terrainMaterial.mainTextureOffset = Vector2.zero;
            _terrainMaterial.SetFloat("_Metallic", 0f);
            _terrainMaterial.SetFloat("_Smoothness", 0.1f);

            Debug.Log("[NationTerrainController] Applied combined ring+nation terrain texture.");
        }

        // ================================================================
        //  C22-09: Update with Smooth Transition
        // ================================================================

        /// <summary>
        /// Re-generates the texture optimized for the given nation with
        /// smooth transition from the previous nation's texture.
        /// Transition blends over _transitionDuration seconds (default 2s).
        /// </summary>
        /// NOTE(2026-09): 현재 프로젝트 어디에서도 호출하지 않는다(구 Start 호출부 제거).
        /// 호출 시 GenerateCombinedTexture의 방위 고정 결합 텍스처를 국가 집중
        /// 텍스처로 덮어쓰므로, 재사용 전에 그 영향을 감안할 것.
        /// <param name="nation">Target nation type</param>
        public void UpdateForCurrentNation(NationType nation)
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;

            // Sync _terrainMaterial with the current renderer material
            _terrainMaterial = renderer.sharedMaterial;

            // Store previous nation and texture before generating new one
            _previousNation = _currentNation;
            _previousTexture = renderer.sharedMaterial.mainTexture as Texture2D;

            // Destroy the old target texture if it exists and is not the same as previous
            if (_targetTexture != null && _targetTexture != _previousTexture && !IsPersistentTexture(_targetTexture))
            {
                Destroy(_targetTexture);
            }

            // Generate the new target texture
            _currentNation = nation;
            _targetTexture = GenerateNationFocusedTexture(nation);

            // Start smooth transition
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            _transitionCoroutine = StartCoroutine(SmoothNationTransition());
        }

        /// <summary>
        /// C22-09: Coroutine that smoothly lerps the terrain texture
        /// from the previous nation's texture to the target nation's
        /// texture over _transitionDuration seconds.
        /// Uses a blend texture approach: loads both textures and
        /// samples pixels progressively.
        /// </summary>
        private IEnumerator SmoothNationTransition()
        {
            _isTransitioning = true;
            float elapsed = 0f;

            // Ensure we have both textures to blend between
            if (_previousTexture == null || _targetTexture == null)
            {
                // Fall back to immediate swap
                if (_terrainMaterial != null)
                {
                    _terrainMaterial.mainTexture = _targetTexture;
                }
                Debug.Log("[NationTerrainController] Immediate texture swap (no previous texture).");
                _isTransitioning = false;
                yield break;
            }

            // Get the material
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null || _terrainMaterial == null)
            {
                _isTransitioning = false;
                yield break;
            }

            int size = Mathf.Max(64, _textureSize);
            Debug.Log($"[NationTerrainController] Smooth transition: {_previousNation} -> {_currentNation} over {_transitionDuration}s");

            // Track previous blend texture for cleanup
            Texture2D previousBlendTex = null;

            while (elapsed < _transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _transitionDuration);

                // Smooth step for nicer easing
                float smoothT = t * t * (3f - 2f * t);

                // Create blend texture by sampling from both source textures
                Texture2D blendTex = GenerateBlendTexture(_previousTexture, _targetTexture, smoothT);

                if (blendTex != null && _terrainMaterial != null)
                {
                    _terrainMaterial.mainTexture = blendTex;

                    // Destroy previous frame's blend texture to prevent leak
                    if (previousBlendTex != null && previousBlendTex != blendTex)
                    {
                        Destroy(previousBlendTex);
                    }
                    previousBlendTex = blendTex;
                }

                yield return null;
            }

            // Final: set to target texture
            if (_terrainMaterial != null)
            {
                _terrainMaterial.mainTexture = _targetTexture;
            }

            // Cleanup the last intermediate blend texture (no longer needed)
            if (previousBlendTex != null && previousBlendTex != _targetTexture)
            {
                Destroy(previousBlendTex);
                previousBlendTex = null;
            }

            // Cleanup previous texture
            if (_previousTexture != null && !IsPersistentTexture(_previousTexture))
            {
                Destroy(_previousTexture);
                _previousTexture = null;
            }

            _isTransitioning = false;
            _transitionCoroutine = null;
            Debug.Log($"[NationTerrainController] Transition complete: now {_currentNation}");
        }

        /// <summary>
        /// Generates a blend texture by interpolating between two source textures.
        /// </summary>
        private Texture2D GenerateBlendTexture(Texture2D from, Texture2D to, float blend)
        {
            // Check if textures are readable before attempting GetPixels
            if (!IsTextureReadable(from) || !IsTextureReadable(to))
            {
                Debug.Log($"[NationTerrainController] Source texture not readable ({from?.name} / {to?.name}). Skipping blend, will use immediate swap.");
                return null;
            }

            int size = Mathf.Max(from.width, to.width);
            size = Mathf.Min(size, _textureSize * 2); // cap for performance

            Texture2D blendTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            blendTex.name = $"TerrainBlend_{_previousNation}to{_currentNation}_t{blend:F2}";
            blendTex.wrapMode = TextureWrapMode.Repeat;
            blendTex.filterMode = FilterMode.Bilinear;

            Color[] fromPixels = from.GetPixels(0);
            Color[] toPixels = to.GetPixels(0);
            Color[] blendPixels = new Color[size * size];

            for (int i = 0; i < size * size; i++)
            {
                int fx = i % size;
                int fy = i / size;

                // Sample from source textures with nearest-neighbor scaling
                float u = (float)fx / size;
                float v = (float)fy / size;

                int fromX = Mathf.FloorToInt(u * from.width);
                int fromY = Mathf.FloorToInt(v * from.height);
                int toX = Mathf.FloorToInt(u * to.width);
                int toY = Mathf.FloorToInt(v * to.height);

                fromX = Mathf.Clamp(fromX, 0, from.width - 1);
                fromY = Mathf.Clamp(fromY, 0, from.height - 1);
                toX = Mathf.Clamp(toX, 0, to.width - 1);
                toY = Mathf.Clamp(toY, 0, to.height - 1);

                Color fromColor = fromPixels[fromY * from.width + fromX];
                Color toColor = toPixels[toY * to.width + toX];

                blendPixels[i] = Color.Lerp(fromColor, toColor, blend);
            }

            blendTex.SetPixels(blendPixels);
            blendTex.Apply();
            return blendTex;
        }

        /// <summary>
        /// Checks if a texture is a persistent asset (not dynamically created).
        /// </summary>
        private static bool IsPersistentTexture(Texture2D tex)
        {
            if (tex == null) return false;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.Contains(tex);
#else
            return false;
#endif
        }

        /// <summary>
        /// Checks if a texture is readable (can call GetPixels/GetPixels32).
        /// Returns false for compressed/non-readable textures with a single log.
        /// </summary>
        private static bool IsTextureReadable(Texture2D tex)
        {
            if (tex == null) return false;
            
            try
            {
                // Attempting to read a single pixel will throw if not readable
                tex.GetPixel(0, 0);
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        /// <summary>
        /// Immediately cancel any in-progress transition and set to target.
        /// </summary>
        public void CancelTransition()
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }
            _isTransitioning = false;

            if (_terrainMaterial != null && _targetTexture != null)
            {
                _terrainMaterial.mainTexture = _targetTexture;
            }
        }

        // ================================================================
        //  Texture Generation
        // ================================================================

        /// <summary>
        /// Generates a full 256x256 combined texture covering all ring zones
        /// and nation territories. Each pixel's world position determines
        /// the ring base color and nation tint blend.
        /// </summary>
        public Texture2D GenerateCombinedTexture()
        {
            int size = Mathf.Max(64, _textureSize);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "NationTerrain_Combined";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Map UV to world position (±1600m 청크 월드, 3200x3200 — 청크 UV 폴백 1/3200+0.5와 정합)
                    float wx = (u - 0.5f) * 3200f;
                    float wz = (v - 0.5f) * 3200f;
                    float dist = Mathf.Sqrt(wx * wx + wz * wz);

                    // Determine nation from position
                    NationType nation = GetNationFromPosition(new Vector3(wx, 0f, wz));
                    Color nationTint = GetNationTint(nation);

                    // 북쪽(설원)은 방위색을 강하게 고정 — 기본 강도(0.35±노이즈)로는
                    // 흰색이 링 베이스색에 묻히므로 목표 강도로 수렴시킨다.
                    float tintOverride = (nation == NationType.North) ? _northTintStrength : -1f;

                    // Compute pixel color
                    Color pixelColor = ComputePixelColor(wx, wz, dist, nationTint, x, y, size, tintOverride);
                    pixels[y * size + x] = pixelColor;
                }
            }

            // 흙길 네트워크 오버레이 — 기존 방위/링 색 로직 위에 블렌드만 수행(시각 전용, 높이 평탄화 없음).
            PaintDirtPaths(pixels, size, 1600f);

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Generates a texture focused on a single nation (stronger tint,
        /// other nations muted). Useful for territory-centric views.
        /// </summary>
        public Texture2D GenerateNationFocusedTexture(NationType focusNation)
        {
            int size = Mathf.Max(64, _textureSize);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = $"NationTerrain_{focusNation}";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            Color focusTint = GetNationTint(focusNation);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float wx = (u - 0.5f) * 3200f;
                    float wz = (v - 0.5f) * 3200f;
                    float dist = Mathf.Sqrt(wx * wx + wz * wz);

                    NationType nation = GetNationFromPosition(new Vector3(wx, 0f, wz));
                    Color tint = (nation == focusNation) ? focusTint : Color.Lerp(focusTint, Color.gray, 0.5f);

                    Color pixelColor = ComputePixelColor(wx, wz, dist, tint, x, y, size);
                    pixels[y * size + x] = pixelColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ================================================================
        //  Dirt Path Network (흙길 네트워크 — 결합 텍스처 오버레이)
        // ================================================================

        /// <summary>흙길 색 (머드 로드 — 예시 이미지 컨셉).</summary>
        private static readonly Color DirtPathColor = new Color(0.52f, 0.40f, 0.28f);

        /// <summary>도로 반폭 (m) — 중심선에서 이 거리까지 페인트(전체 폭 10m).</summary>
        private const float DirtPathHalfWidth = 5f;

        /// <summary>중심선에서의 최대 블렌드 강도 (가장자리로 갈수록 0%로 페이드).</summary>
        private const float DirtPathMaxAlpha = 0.85f;

        /// <summary>
        /// 결정론적 흙길 네트워크 세그먼트(월드 XZ, static 캐시 — 재생성 시 항상 동일):
        ///  - 4개 방사형 스포크: 각 방위(East 0°/North 90°/West 180°/South 270°, atan2(z,x) 체계)로
        ///    원점에서 반경 100m → 1500m까지 직선 세그먼트
        ///  - 링 도로 2개: 반경 550m(Ring3), 1000m(Ring2) — 각 방위 스포크 끝단을 잇는
        ///    원호를 15° 간격 폴리라인으로 근사(링당 24 세그먼트)
        ///  - 스폰 연결: PlayerSpawnConfig.SpawnPosition(약 (728, -529))에서 가장 가까운
        ///    스포크의 수선 발 지점까지 1개 세그먼트
        /// </summary>
        private static readonly PathSegment[] DirtPathSegments = BuildDirtPathSegments();

        /// <summary>월드 XZ 평면 위 선분. AABB는 픽셀 루프 사전 필터링용(반폭 여유 포함).</summary>
        private struct PathSegment
        {
            public float X0, Z0, X1, Z1;         // 끝점 (월드 XZ)
            public float MinX, MaxX, MinZ, MaxZ; // AABB (+반폭 여유)

            public PathSegment(float x0, float z0, float x1, float z1)
            {
                X0 = x0; Z0 = z0; X1 = x1; Z1 = z1;
                MinX = Mathf.Min(x0, x1) - DirtPathHalfWidth;
                MaxX = Mathf.Max(x0, x1) + DirtPathHalfWidth;
                MinZ = Mathf.Min(z0, z1) - DirtPathHalfWidth;
                MaxZ = Mathf.Max(z0, z1) + DirtPathHalfWidth;
            }
        }

        private static PathSegment[] BuildDirtPathSegments()
        {
            var list = new List<PathSegment>(64);

            const float spokeInner = 100f;
            const float spokeOuter = 1500f;
            const float ring3Radius = 550f;
            const float ring2Radius = 1000f;
            const float ringStepDeg = 15f;

            // 1) 방사형 스포크 — East 0° / North 90° / West 180° / South 270°
            //    (GetNationFromPosition과 동일한 atan2(z, x) 각도 체계)
            var spokes = new PathSegment[4];
            for (int i = 0; i < 4; i++)
            {
                float rad = (i * 90f) * Mathf.Deg2Rad;
                float cx = Mathf.Cos(rad);
                float cz = Mathf.Sin(rad);
                spokes[i] = new PathSegment(
                    cx * spokeInner, cz * spokeInner,
                    cx * spokeOuter, cz * spokeOuter);
                list.Add(spokes[i]);
            }

            // 2) 링 도로 — Ring3(550m) / Ring2(1000m), 15° 간격 폴리라인 원호 근사
            AppendRing(list, ring3Radius, ringStepDeg);
            AppendRing(list, ring2Radius, ringStepDeg);

            // 3) 스폰 연결 — 스폰에서 가장 가까운 스포크의 수선 발 지점까지 1개 세그먼트
            Vector3 spawn = PlayerSpawnConfig.SpawnPosition;
            int bestSpoke = -1;
            float bestDist = float.MaxValue;
            Vector2 bestFoot = Vector2.zero;
            for (int i = 0; i < spokes.Length; i++)
            {
                Vector2 foot = ClosestPointOnSegment(spawn.x, spawn.z, spokes[i]);
                float d = Mathf.Sqrt((foot.x - spawn.x) * (foot.x - spawn.x)
                                   + (foot.y - spawn.z) * (foot.y - spawn.z));
                if (d < bestDist)
                {
                    bestDist = d;
                    bestSpoke = i;
                    bestFoot = foot;
                }
            }
            if (bestSpoke >= 0 && bestDist > 0.5f)
            {
                list.Add(new PathSegment(spawn.x, spawn.z, bestFoot.x, bestFoot.y));
            }

            return list.ToArray();
        }

        /// <summary>원호를 반경 radius, stepDeg 간격 폴리라인으로 근사해 세그먼트를 추가한다.</summary>
        private static void AppendRing(List<PathSegment> list, float radius, float stepDeg)
        {
            int count = Mathf.RoundToInt(360f / stepDeg);
            for (int i = 0; i < count; i++)
            {
                float a0 = i * stepDeg * Mathf.Deg2Rad;
                float a1 = ((i + 1) % count) * stepDeg * Mathf.Deg2Rad;
                list.Add(new PathSegment(
                    Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius,
                    Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius));
            }
        }

        /// <summary>점 (px, pz)에서 세그먼트까지의 최단 거리 (월드 m).</summary>
        private static float DistanceToSegment(float px, float pz, PathSegment s)
        {
            float dx = s.X1 - s.X0;
            float dz = s.Z1 - s.Z0;
            float lenSq = dx * dx + dz * dz;
            float t = lenSq > 1e-6f ? ((px - s.X0) * dx + (pz - s.Z0) * dz) / lenSq : 0f;
            t = Mathf.Clamp01(t);
            float cx = s.X0 + t * dx - px;
            float cz = s.Z0 + t * dz - pz;
            return Mathf.Sqrt(cx * cx + cz * cz);
        }

        /// <summary>점 (px, pz)에서 세그먼트 위 최근접 지점 (월드 XZ).</summary>
        private static Vector2 ClosestPointOnSegment(float px, float pz, PathSegment s)
        {
            float dx = s.X1 - s.X0;
            float dz = s.Z1 - s.Z0;
            float lenSq = dx * dx + dz * dz;
            float t = lenSq > 1e-6f ? ((px - s.X0) * dx + (pz - s.Z0) * dz) / lenSq : 0f;
            t = Mathf.Clamp01(t);
            return new Vector2(s.X0 + t * dx, s.Z0 + t * dz);
        }

        /// <summary>
        /// 결합 텍스처 픽셀에 흙길 네트워크를 오버레이 페인트한다.
        /// 픽셀↔월드 매핑은 GenerateCombinedTexture의 u=x/size, wx=(u-0.5)*worldHalf*2와
        /// 동일(+z → +y 픽셀). 세그먼트 AABB 사전 필터링으로 대부분의 픽셀×세그먼트 조합을
        /// 조기 스킵하므로 256²~1024² 모두 허용 가능.
        /// </summary>
        private void PaintDirtPaths(Color[] pixels, int size, float worldHalf)
        {
            float worldSize = worldHalf * 2f;
            float halfWidth = DirtPathHalfWidth;
            Color dirt = DirtPathColor;
            PathSegment[] segments = DirtPathSegments;

            for (int y = 0; y < size; y++)
            {
                float wz = ((float)y / size - 0.5f) * worldSize;
                int row = y * size;
                for (int x = 0; x < size; x++)
                {
                    float wx = ((float)x / size - 0.5f) * worldSize;

                    // 최근접 세그먼트 거리 (AABB 사전 필터링)
                    float best = float.MaxValue;
                    for (int i = 0; i < segments.Length; i++)
                    {
                        PathSegment s = segments[i];
                        if (wx < s.MinX || wx > s.MaxX || wz < s.MinZ || wz > s.MaxZ)
                            continue;
                        float d = DistanceToSegment(wx, wz, s);
                        if (d < best) best = d;
                    }
                    if (best >= halfWidth) continue;

                    // 중심선(d=0)에서 85% → 가장자리(d=반폭)에서 0%로 부드럽게 페이드
                    float t = best / halfWidth;
                    float alpha = DirtPathMaxAlpha * Mathf.SmoothStep(1f, 0f, t);
                    int idx = row + x;
                    pixels[idx] = Color.Lerp(pixels[idx], dirt, alpha);
                }
            }
        }

        // ================================================================
        //  Core Pixel Computation
        // ================================================================

        /// <summary>
        /// Computes a single pixel color by blending ring-zone base color
        /// with Perlin noise variation and nation-specific tint.
        /// </summary>
        private Color ComputePixelColor(float wx, float wz, float dist, Color nationTint, int px, int py, int size,
            float tintStrengthOverride = -1f)
        {
            const int seed = 42;

            // --- Ring zone base color ---
            Color ringBase;
            if (dist < 350f)
            {
                ringBase = _ring1Color;
            }
            else if (dist < 700f)
            {
                float t = (dist - 350f) / 350f;
                ringBase = Color.Lerp(_ring1Color, _ring2Color, t);
            }
            else
            {
                float t = Mathf.Min((dist - 700f) / 300f, 1f);
                ringBase = Color.Lerp(_ring2Color, _ring3Color, t);
            }

            // --- Perlin noise ---
            float n1 = Mathf.PerlinNoise(px * 0.04f + seed * 0.01f, py * 0.04f + seed * 0.01f);
            float n2 = Mathf.PerlinNoise(px * 0.08f + seed * 0.1f + 100f, py * 0.08f + seed * 0.1f + 100f);
            float n3 = Mathf.PerlinNoise(px * 0.02f + seed * 0.2f + 200f, py * 0.02f + seed * 0.2f + 200f);

            float variation = (n1 - 0.5f) * 0.25f + (n2 - 0.5f) * 0.12f;

            Color noisyBase = new Color(
                Mathf.Clamp01(ringBase.r + variation),
                Mathf.Clamp01(ringBase.g + variation * 0.8f),
                Mathf.Clamp01(ringBase.b + variation * 0.6f),
                1f
            );

            // --- Nation tint overlay ---
            float tintStrength = _baseTintStrength + n3 * _tintNoiseVariation;
            float distFactor = Mathf.Clamp01(dist / 1600f);   // ±1600m 월드 기준
            tintStrength *= (0.7f + distFactor * 0.3f);

            // 설원(북쪽) 고정 강화: override 지정 시 노이즈 변동 10%만 남기고 목표 강도로 수렴
            if (tintStrengthOverride >= 0f)
            {
                tintStrength = Mathf.Clamp01(Mathf.Lerp(tintStrength, tintStrengthOverride, 0.9f));
            }

            Color finalColor = Color.Lerp(noisyBase, nationTint, tintStrength);

            // Center darkening
            if (dist < 50f)
            {
                float centerDarken = 1f - (1f - dist / 50f) * 0.15f;
                finalColor *= centerDarken;
            }

            return finalColor;
        }

        // ================================================================
        //  Nation from Position
        // ================================================================

        /// <summary>
        /// Determines which nation's territory a given world position belongs to.
        /// Based on the angle/direction from center (0,0,0).
        ///
        ///   East   = x+  (0°)
        ///   North  = z+  (90°)
        ///   West   = x-  (180°)
        ///   South  = z-  (270°)
        ///   Empire = within 50m of center
        /// </summary>
        /// <param name="worldPos">World position to evaluate</param>
        /// <returns>NationType for the given position</returns>
        public static NationType GetNationFromPosition(Vector3 worldPos)
        {
            // Empire territory: within 50m of center
            float dist = worldPos.magnitude;
            if (dist < 50f)
                return NationType.Empire;

            // Determine territory based on angle
            float angle = Mathf.Atan2(worldPos.z, worldPos.x) * Mathf.Rad2Deg;
            // Normalize to 0-360
            if (angle < 0f) angle += 360f;

            // East:   -45° to 45°   (centered on x+)
            // North:   45° to 135°  (centered on z+)
            // West:   135° to 225° (centered on x-)
            // South:  225° to 315° (centered on z-)
            if (angle < 45f || angle >= 315f)
                return NationType.East;
            else if (angle < 135f)
                return NationType.North;
            else if (angle < 225f)
                return NationType.West;
            else
                return NationType.South;
        }
    }
}