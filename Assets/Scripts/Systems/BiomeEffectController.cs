using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C22-06: Biome별 이동 속도/시각 효과 제어.
    /// Swamp (늪): dark green tinted fog, slow movement (0.5x), bubbling particle effect.
    /// Desert (사막): sandy/yellow tint, heat haze shimmer.
    /// Uses BiomeType enum to detect current biome and applies temporary
    /// movement speed modifier to PlayerMovement.
    /// </summary>
    public class BiomeEffectController : MonoBehaviour
    {
        [Header("Current Biome")]
        [SerializeField] private BiomeType _currentBiome = BiomeType.Plains;

        [Header("Movement")]
        [SerializeField] private float _speedMultiplier = 1.0f;
        [SerializeField] private bool _isInWater = false;

        [Header("Visual Effects")]
        [SerializeField] private Color _ambientTint = Color.white;
        [SerializeField] private float _fogDensity = 0.0f;
        [SerializeField] private Color _fogColor = Color.gray;

        [Header("Effect Settings")]
        [SerializeField] private float _swampFogDensity = 0.05f;
        [SerializeField] private Color _swampFogColor = new Color(0.1f, 0.25f, 0.08f); // dark green
        [SerializeField] private Color _swampAmbientTint = new Color(0.2f, 0.35f, 0.15f);
        [SerializeField] private float _desertFogDensity = 0.02f;
        [SerializeField] private Color _desertFogColor = new Color(0.75f, 0.65f, 0.35f); // sandy yellow
        [SerializeField] private Color _desertAmbientTint = new Color(0.85f, 0.75f, 0.45f);

        // Bubbling particle simulation (light intensity oscillation)
        [Header("Swamp Bubble Effect")]
        [SerializeField] private Light _bubbleLight;
        [SerializeField] private float _bubbleSpeed = 2.5f;
        [SerializeField] private float _bubbleIntensityMin = 0.2f;
        [SerializeField] private float _bubbleIntensityMax = 0.5f;

        // Heat haze simulation (light shimmer)
        [Header("Desert Haze Effect")]
        [SerializeField] private Light _hazeLight;
        [SerializeField] private float _hazeSpeed = 1.8f;
        [SerializeField] private float _hazeIntensityMin = 0.6f;
        [SerializeField] private float _hazeIntensityMax = 1.2f;

        // Cached ambient values for restoration
        private Color _originalAmbientLight;
        private float _originalFogDensity;
        private Color _originalFogColor;
        private PlayerMovement _playerMovement;

        private void Awake()
        {
            // Cache original environment settings
            _originalAmbientLight = RenderSettings.ambientLight;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalFogColor = RenderSettings.fogColor;

            // Cache player movement reference
            _playerMovement = GetComponent<PlayerMovement>();
        }

        /// <summary>
        /// Biome 변경 — 이동 속도 및 시각 효과 업데이트
        /// </summary>
        /// <param name="biome">새로운 Biome</param>
        public void ApplyBiomeEffect(BiomeType biome)
        {
            _currentBiome = biome;
            BiomeDefinition def = BiomeData.GetDefinition(biome);

            _speedMultiplier = def.moveSpeedModifier;

            // Apply movement speed modifier to PlayerMovement
            if (_playerMovement != null)
            {
                _playerMovement.SpeedModifier = _speedMultiplier;
            }

            // Apply biome-specific visual effects
            switch (biome)
            {
                case BiomeType.Swamp:
                    ApplySwampEffect();
                    break;
                case BiomeType.Desert:
                    ApplyDesertEffect();
                    break;
                default:
                    ResetVisualEffects();
                    break;
            }

            Debug.Log($"[BiomeEffectController] Biome changed to {def.displayName}, speed={_speedMultiplier}");
        }

        /// <summary>
        /// Swamp biome: dark green fog, bubbling light effect
        /// </summary>
        private void ApplySwampEffect()
        {
            RenderSettings.fog = true;
            RenderSettings.fogDensity = _swampFogDensity;
            RenderSettings.fogColor = _swampFogColor;

            RenderSettings.ambientLight = _swampAmbientTint;

            // Enable bubble light animation
            if (_bubbleLight != null)
            {
                _bubbleLight.gameObject.SetActive(true);
            }
            if (_hazeLight != null)
            {
                _hazeLight.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Desert biome: sandy/yellow fog, heat haze shimmer light
        /// </summary>
        private void ApplyDesertEffect()
        {
            RenderSettings.fog = true;
            RenderSettings.fogDensity = _desertFogDensity;
            RenderSettings.fogColor = _desertFogColor;

            RenderSettings.ambientLight = _desertAmbientTint;

            // Enable haze light animation
            if (_hazeLight != null)
            {
                _hazeLight.gameObject.SetActive(true);
            }
            if (_bubbleLight != null)
            {
                _bubbleLight.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Reset to default environmental visuals
        /// </summary>
        private void ResetVisualEffects()
        {
            RenderSettings.fog = false;
            RenderSettings.fogDensity = _originalFogDensity;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.ambientLight = _originalAmbientLight;

            if (_bubbleLight != null)
                _bubbleLight.gameObject.SetActive(false);
            if (_hazeLight != null)
                _hazeLight.gameObject.SetActive(false);
        }

        private void Update()
        {
            // Animate biome-specific effects
            if (_currentBiome == BiomeType.Swamp && _bubbleLight != null && _bubbleLight.gameObject.activeSelf)
            {
                // Bubbling light oscillation
                float intensity = Mathf.Lerp(_bubbleIntensityMin, _bubbleIntensityMax,
                    Mathf.Sin(Time.time * _bubbleSpeed) * 0.5f + 0.5f);
                _bubbleLight.intensity = intensity;
            }
            else if (_currentBiome == BiomeType.Desert && _hazeLight != null && _hazeLight.gameObject.activeSelf)
            {
                // Heat haze shimmer oscillation
                float intensity = Mathf.Lerp(_hazeIntensityMin, _hazeIntensityMax,
                    Mathf.Sin(Time.time * _hazeSpeed) * 0.5f + 0.5f);
                _hazeLight.intensity = intensity;
            }
        }

        /// <summary>
        /// 물에 들어갈 때 속도 감소
        /// </summary>
        public void OnEnterWater()
        {
            _isInWater = true;
            if (_playerMovement != null)
            {
                _playerMovement.SpeedModifier = 0.5f;
            }
        }

        /// <summary>
        /// 물에서 나올 때 속도 복원
        /// </summary>
        public void OnExitWater()
        {
            _isInWater = false;
            if (_playerMovement != null)
            {
                _playerMovement.SpeedModifier = _speedMultiplier;
            }
        }

        // --- Public properties for testing ---
        public BiomeType CurrentBiome => _currentBiome;
        public float SpeedMultiplier => _speedMultiplier;
        public bool IsInWater => _isInWater;
    }
}