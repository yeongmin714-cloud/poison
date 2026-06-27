using System.Collections;
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
    ///   North  = z+ (gray tundra)
    ///   Empire = center region (golden)
    ///
    /// The texture is a single 256x256 procedurally generated map that
    /// composites ring zone coloring with nation-specific tint at each
    /// pixel. UV coordinates are mapped to world-space positions on the
    /// 1000x1000 terrain plane.
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
        [SerializeField] private Color _northTint = new Color(0.50f, 0.50f, 0.55f); // gray tundra
        [SerializeField] private Color _empireTint = new Color(0.85f, 0.72f, 0.18f); // golden
        [SerializeField] private Color _draculaTint = new Color(0.25f, 0.05f, 0.05f); // dark red/black

        [Header("Nation Overlay")]
        [SerializeField, Range(0f, 1f)] private float _baseTintStrength = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _tintNoiseVariation = 0.25f;

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

        private void Start()
        {
            // Optionally update if TerritoryManager becomes available later
            if (TerritoryManager.Instance != null)
            {
                UpdateForCurrentNation(TerritoryManager.Instance.CurrentTerritoryId.nation);
            }
        }

        /// <summary>
        /// Applies a combined ring-zone + nation-specific procedural texture
        /// to this GameObject's material.
        /// </summary>
        public void ApplyNationTerrainTexture()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning("[NationTerrainController] No MeshRenderer found on Ground.");
                return;
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
            _terrainMaterial.mainTextureScale = Vector2.one * _textureTiling;
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

                    // Map UV to world position (terrain centered at origin, 1000x1000)
                    float wx = (u - 0.5f) * 1000f;
                    float wz = (v - 0.5f) * 1000f;
                    float dist = Mathf.Sqrt(wx * wx + wz * wz);

                    // Determine nation from position
                    NationType nation = GetNationFromPosition(new Vector3(wx, 0f, wz));
                    Color nationTint = GetNationTint(nation);

                    // Compute pixel color
                    Color pixelColor = ComputePixelColor(wx, wz, dist, nationTint, x, y, size);
                    pixels[y * size + x] = pixelColor;
                }
            }

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

                    float wx = (u - 0.5f) * 1000f;
                    float wz = (v - 0.5f) * 1000f;
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
        //  Core Pixel Computation
        // ================================================================

        /// <summary>
        /// Computes a single pixel color by blending ring-zone base color
        /// with Perlin noise variation and nation-specific tint.
        /// </summary>
        private Color ComputePixelColor(float wx, float wz, float dist, Color nationTint, int px, int py, int size)
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
            float distFactor = Mathf.Clamp01(dist / 1000f);
            tintStrength *= (0.7f + distFactor * 0.3f);

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