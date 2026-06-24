using System.Collections.Generic;
using System.Linq;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// PNG 텍스처 기반 국가별 지형 텍스처 적용 시스템.
    /// Resources/Models/UserProvided/terrain/textures/ 에서 PNG를 로드하여
    /// URP Lit Material로 변환, Ground MeshRenderer에 적용한다.
    /// NationTerrainController를 대체하여 동작한다.
    /// </summary>
    public class TerrainTextureApplier : MonoBehaviour
    {
        [Header("Texture Resources")]
        [SerializeField] private string _textureResourcesPath = "Models/UserProvided/terrain/textures/";

        [Header("Material Settings")]
        [SerializeField] private float _metallic = 0f;
        [SerializeField] private float _smoothness = 0.1f;
        [SerializeField] private float _textureTiling = 200f;

        [Header("Runtime State")]
        [SerializeField] private NationType _currentNation = NationType.East;

        // Loaded textures by nation
        private Dictionary<NationType, List<Texture2D>> _nationTextures;
        private List<Texture2D> _extraTextures;

        // Created materials keyed by nation
        private Dictionary<NationType, Material> _nationMaterials;

        // Cached references
        private MeshRenderer _meshRenderer;
        private NationTerrainController _nationController;

        /// <summary>Current active nation material.</summary>
        public NationType CurrentNation => _currentNation;

        /// <summary>All created nation materials (readonly for tests).</summary>
        public IReadOnlyDictionary<NationType, Material> NationMaterials => _nationMaterials;

        // ================================================================
        //  Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            // Disable NationTerrainController if present
            _nationController = GetComponent<NationTerrainController>();
            if (_nationController != null)
            {
                _nationController.enabled = false;
                Debug.Log("[TerrainTextureApplier] NationTerrainController disabled.");
            }

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
            {
                Debug.LogError("[TerrainTextureApplier] No MeshRenderer found on Ground.");
                return;
            }

            LoadTextures();
            CreateMaterials();
            ApplyMaterialForNation(_currentNation);
        }

        // ================================================================
        //  Texture Loading
        // ================================================================

        /// <summary>
        /// Loads all PNG textures from the resources path and categorizes
        /// them by nation prefix (east_, west_, south_, north_, empire_, extra_).
        /// </summary>
        public void LoadTextures()
        {
            _nationTextures = new Dictionary<NationType, List<Texture2D>>();
            _extraTextures = new List<Texture2D>();

            Texture2D[] allTextures = Resources.LoadAll<Texture2D>(_textureResourcesPath);
            if (allTextures == null || allTextures.Length == 0)
            {
                Debug.LogWarning("[TerrainTextureApplier] No textures found at: " + _textureResourcesPath);
                return;
            }

            foreach (Texture2D tex in allTextures)
            {
                if (tex == null) continue;

                string lowerName = tex.name.ToLowerInvariant();

                if (lowerName.StartsWith("east_"))
                    AddToNation(NationType.East, tex);
                else if (lowerName.StartsWith("west_"))
                    AddToNation(NationType.West, tex);
                else if (lowerName.StartsWith("south_"))
                    AddToNation(NationType.South, tex);
                else if (lowerName.StartsWith("north_"))
                    AddToNation(NationType.North, tex);
                else if (lowerName.StartsWith("empire_"))
                    AddToNation(NationType.Empire, tex);
                else if (lowerName.StartsWith("extra_"))
                    _extraTextures.Add(tex);
                else
                    Debug.LogWarning($"[TerrainTextureApplier] Unrecognized texture prefix: {tex.name}");
            }

            Debug.Log($"[TerrainTextureApplier] Loaded {allTextures.Length} textures. " +
                      $"East={_nationTextures.ContainsKey(NationType.East) ? _nationTextures[NationType.East].Count : 0}, " +
                      $"West={_nationTextures.ContainsKey(NationType.West) ? _nationTextures[NationType.West].Count : 0}, " +
                      $"South={_nationTextures.ContainsKey(NationType.South) ? _nationTextures[NationType.South].Count : 0}, " +
                      $"North={_nationTextures.ContainsKey(NationType.North) ? _nationTextures[NationType.North].Count : 0}, " +
                      $"Empire={_nationTextures.ContainsKey(NationType.Empire) ? _nationTextures[NationType.Empire].Count : 0}, " +
                      $"Extra={_extraTextures.Count}");
        }

        private void AddToNation(NationType nation, Texture2D tex)
        {
            if (!_nationTextures.ContainsKey(nation))
                _nationTextures[nation] = new List<Texture2D>();
            _nationTextures[nation].Add(tex);
        }

        // ================================================================
        //  Material Creation
        // ================================================================

        /// <summary>
        /// Creates URP Lit materials for each nation using loaded textures.
        /// Material naming: "Terrain_{nation}_Mat"
        /// Extra textures are applied as secondary/blend textures:
        ///   extra1 (red) → south, empire
        ///   extra2 (gray) → north
        ///   extra3 (yellow) → west, empire
        /// </summary>
        public void CreateMaterials()
        {
            _nationMaterials = new Dictionary<NationType, Material>();

            // Map extra textures by index
            Texture2D extra1 = _extraTextures.Count > 0 ? _extraTextures[0] : null;
            Texture2D extra2 = _extraTextures.Count > 1 ? _extraTextures[1] : null;
            Texture2D extra3 = _extraTextures.Count > 2 ? _extraTextures[2] : null;

            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire })
            {
                if (!_nationTextures.ContainsKey(nation) || _nationTextures[nation].Count == 0)
                {
                    Debug.LogWarning($"[TerrainTextureApplier] No textures for {nation}. Skipping material.");
                    continue;
                }

                Material mat = CreateLitMaterial($"Terrain_{nation}_Mat", _nationTextures[nation][0]);

                // Apply extra textures for specific nations
                if (nation == NationType.South || nation == NationType.Empire)
                {
                    ApplyExtraTexture(mat, extra1, "_BaseMap", 0.3f, "extra1(red)");
                }
                if (nation == NationType.North)
                {
                    ApplyExtraTexture(mat, extra2, "_BaseMap", 0.3f, "extra2(gray)");
                }
                if (nation == NationType.West || nation == NationType.Empire)
                {
                    ApplyExtraTexture(mat, extra3, "_BaseMap", 0.3f, "extra3(yellow)");
                }

                _nationMaterials[nation] = mat;
            }

            Debug.Log($"[TerrainTextureApplier] Created {_nationMaterials.Count} nation materials.");
        }

        private Material CreateLitMaterial(string name, Texture2D mainTex)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                Debug.LogWarning("[TerrainTextureApplier] URP Lit shader not found, falling back to Standard.");
            }

            Material mat = new Material(shader);
            mat.name = name;

            // Assign main texture
            if (shader.name.Contains("Universal Render Pipeline/Lit"))
            {
                mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", _metallic);
                mat.SetFloat("_Smoothness", _smoothness);
            }
            else
            {
                mat.mainTexture = mainTex;
                mat.SetFloat("_Metallic", _metallic);
                mat.SetFloat("_Glossiness", _smoothness);
            }

            mat.mainTextureScale = Vector2.one * _textureTiling;

            return mat;
        }

        private void ApplyExtraTexture(Material mat, Texture2D extraTex, string propertyName, float blendStrength, string label)
        {
            if (extraTex == null) return;

            // For URP Lit, we can use the material's second texture slot or blend via color
            // Simple approach: blend texture into the material's main texture by averaging
            // In a more advanced approach, we'd add a secondary texture property
            if (mat.shader.name.Contains("Universal Render Pipeline/Lit"))
            {
                // Try to use a detail/albedo or blend property
                // If the shader supports it, set detail texture
                if (mat.HasProperty("_DetailAlbedoMap"))
                {
                    mat.SetTexture("_DetailAlbedoMap", extraTex);
                    mat.SetFloat("_DetailAlbedoMapScale", blendStrength);
                }
            }

            Debug.Log($"[TerrainTextureApplier] Applied {label} to {mat.name}");
        }

        // ================================================================
        //  Material Application
        // ================================================================

        /// <summary>
        /// Applies the material for the given nation to the Ground MeshRenderer.
        /// </summary>
        /// <param name="nation">Target nation type</param>
        public void ApplyMaterialForNation(NationType nation)
        {
            if (_meshRenderer == null)
            {
                Debug.LogError("[TerrainTextureApplier] MeshRenderer not available.");
                return;
            }

            if (_nationMaterials == null || !_nationMaterials.ContainsKey(nation))
            {
                Debug.LogWarning($"[TerrainTextureApplier] No material for {nation}. Using fallback.");
                return;
            }

            _currentNation = nation;
            _meshRenderer.material = _nationMaterials[nation];
            _meshRenderer.material.mainTextureScale = Vector2.one * _textureTiling;

            Debug.Log($"[TerrainTextureApplier] Applied material '{_nationMaterials[nation].name}' for {nation}.");
        }

        /// <summary>
        /// Updates the terrain material based on world position
        /// using NationTerrainController.GetNationFromPosition.
        /// </summary>
        /// <param name="worldPos">Player or camera world position</param>
        public void UpdateForPosition(Vector3 worldPos)
        {
            NationType nation = NationTerrainController.GetNationFromPosition(worldPos);
            if (nation != _currentNation && _nationMaterials.ContainsKey(nation))
            {
                ApplyMaterialForNation(nation);
            }
        }

        // ================================================================
        //  Public Query Methods (for tests)
        // ================================================================

        /// <summary>Number of nation textures loaded.</summary>
        public int NationTextureCount(NationType nation)
        {
            return _nationTextures != null && _nationTextures.ContainsKey(nation)
                ? _nationTextures[nation].Count
                : 0;
        }

        /// <summary>Number of extra textures loaded.</summary>
        public int ExtraTextureCount => _extraTextures?.Count ?? 0;

        /// <summary>Whether a material exists for the given nation.</summary>
        public bool HasMaterialFor(NationType nation)
        {
            return _nationMaterials != null && _nationMaterials.ContainsKey(nation);
        }
    }
}