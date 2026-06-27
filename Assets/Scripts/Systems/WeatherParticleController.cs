using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// G1-05: Weather Particle Controller — Creates and manages particle systems
    /// for rain and snow weather effects. Listens to WeatherManager.OnWeatherChanged
    /// to enable/disable the appropriate particle system.
    /// </summary>
    public class WeatherParticleController : MonoBehaviour
    {
        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("Rain")]
        [SerializeField] private int _rainMaxParticles = 3000;
        [SerializeField] private float _rainEmissionRate = 200f;
        [SerializeField] private float _rainSpeed = 15f;

        [Header("Snow")]
        [SerializeField] private int _snowMaxParticles = 1000;
        [SerializeField] private float _snowEmissionRate = 80f;
        [SerializeField] private float _snowSpeed = 3f;
        [SerializeField] private float _snowSwayAmount = 2f;

        [Header("References (auto-resolved)")]
        [SerializeField] private WeatherManager _weatherManager;

        // ================================================================
        // Private State
        // ================================================================

        private ParticleSystem _rainSystem;
        private ParticleSystem _snowSystem;
        private GameObject _rainGo;
        private GameObject _snowGo;
        private bool _initialized;

        // ================================================================
        // Public Properties (for tests)
        // ================================================================

        public ParticleSystem RainSystem => _rainSystem;
        public ParticleSystem SnowSystem => _snowSystem;
        public bool HasRainSystem => _rainSystem != null;
        public bool HasSnowSystem => _snowSystem != null;

        // ================================================================
        // Unity Lifecycle
        // ================================================================

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if (_weatherManager != null)
            {
                _weatherManager.OnWeatherChanged -= OnWeatherChanged;
            }
        }

        // ================================================================
        // Initialization
        // ================================================================

        private void Initialize()
        {
            if (_initialized) return;

            // Resolve WeatherManager
            if (_weatherManager == null)
                _weatherManager = FindAnyObjectByType<WeatherManager>();
            if (_weatherManager == null)
                _weatherManager = WeatherManager.Instance;

            if (_weatherManager == null)
            {
                Debug.LogError("[WeatherParticleController] WeatherManager not found.");
                return;
            }

            // Create rain particle system
            _rainGo = new GameObject("RainParticles");
            _rainGo.transform.SetParent(transform, false);
            _rainGo.transform.localPosition = Vector3.zero;
            _rainSystem = _rainGo.AddComponent<ParticleSystem>();
            ConfigureRainParticles();

            // Create snow particle system
            _snowGo = new GameObject("SnowParticles");
            _snowGo.transform.SetParent(transform, false);
            _snowGo.transform.localPosition = Vector3.zero;
            _snowSystem = _snowGo.AddComponent<ParticleSystem>();
            ConfigureSnowParticles();

            // Apply current weather state
            UpdateParticleState(_weatherManager.CurrentWeather);

            // Subscribe to weather changes
            _weatherManager.OnWeatherChanged += OnWeatherChanged;

            _initialized = true;
        }

        // ================================================================
        // Particle Configuration
        // ================================================================

        private void ConfigureRainParticles()
        {
            var main = _rainSystem.main;
            main.maxParticles = _rainMaxParticles;
            main.startLifetime = 2f;
            main.startSpeed = _rainSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = new Color(0.5f, 0.6f, 0.8f, 0.6f);
            main.gravityModifier = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Shape: box high above
            var shape = _rainSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 1f, 40f);

            // Emission
            var emission = _rainSystem.emission;
            emission.rateOverTime = _rainEmissionRate;

            // Velocity over lifetime: angle rain at 45 degrees
            var velocity = _rainSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
            velocity.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

            // Renderer
            var renderer = _rainSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.material = CreateWeatherParticleMaterial();
            // Assign procedural texture AFTER material is set (not before, or it gets discarded)
            renderer.material.mainTexture = CreateProceduralParticleTexture(8, 8, new Color(0.7f, 0.8f, 1f, 0.7f));
        }

        private void ConfigureSnowParticles()
        {
            var main = _snowSystem.main;
            main.maxParticles = _snowMaxParticles;
            main.startLifetime = 4f;
            main.startSpeed = _snowSpeed;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.05f);
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            main.gravityModifier = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Shape: box high above, slightly smaller than rain
            var shape = _snowSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40f, 1f, 40f);

            // Emission
            var emission = _snowSystem.emission;
            emission.rateOverTime = _snowEmissionRate;

            // Velocity over lifetime: gentle horizontal sway
            var velocity = _snowSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-_snowSwayAmount, _snowSwayAmount);
            velocity.z = new ParticleSystem.MinMaxCurve(-_snowSwayAmount, _snowSwayAmount);
            velocity.space = ParticleSystemSimulationSpace.Local;

            // Renderer
            var renderer = _snowSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateWeatherParticleMaterial();
            // Assign procedural texture AFTER material is set (not before, or it gets discarded)
            renderer.material.mainTexture = CreateProceduralParticleTexture(8, 8, Color.white);
        }

        // ================================================================
        // Procedural Texture
        // ================================================================

        private static Texture2D CreateProceduralParticleTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "ProceduralParticle_" + color.r + "_" + color.g + "_" + color.b;

            float cx = width * 0.5f;
            float cy = height * 0.5f;
            float radius = Mathf.Min(width, height) * 0.4f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha * color.a));
                }
            }

            tex.Apply();
            return tex;
        }

        private static Material CreateWeatherParticleMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
                ?? Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = "WeatherParticleMaterial";
            return mat;
        }

        // ================================================================
        // Weather Response
        // ================================================================

        private void OnWeatherChanged(WeatherManager.WeatherType weather)
        {
            UpdateParticleState(weather);
        }

        private void UpdateParticleState(WeatherManager.WeatherType weather)
        {
            bool shouldRain = weather == WeatherManager.WeatherType.Rain;
            bool shouldSnow = weather == WeatherManager.WeatherType.Snow;

            if (_rainGo != null)
                _rainGo.SetActive(shouldRain);

            if (_snowGo != null)
                _snowGo.SetActive(shouldSnow);

            if (shouldRain && _rainSystem != null && !_rainSystem.isPlaying)
                _rainSystem.Play();
            else if (!shouldRain && _rainSystem != null && _rainSystem.isPlaying)
                _rainSystem.Stop();

            if (shouldSnow && _snowSystem != null && !_snowSystem.isPlaying)
                _snowSystem.Play();
            else if (!shouldSnow && _snowSystem != null && _snowSystem.isPlaying)
                _snowSystem.Stop();
        }

        // ================================================================
        // Public API (for tests)
        // ================================================================

        /// <summary>Force (re)initialization. Safe to call multiple times.</summary>
        public void ForceInitialize()
        {
            Cleanup();
            _initialized = false;
            Initialize();
        }

        /// <summary>Destroy and null out all created objects (for re-init or cleanup).</summary>
        private void Cleanup()
        {
            if (_weatherManager != null)
            {
                _weatherManager.OnWeatherChanged -= OnWeatherChanged;
            }

            if (_rainGo != null)
            {
                if (Application.isPlaying)
                    Destroy(_rainGo);
                else
                    DestroyImmediate(_rainGo);
                _rainGo = null;
                _rainSystem = null;
            }

            if (_snowGo != null)
            {
                if (Application.isPlaying)
                    Destroy(_snowGo);
                else
                    DestroyImmediate(_snowGo);
                _snowGo = null;
                _snowSystem = null;
            }
        }

        /// <summary>Set WeatherManager reference (for tests).</summary>
        public void SetWeatherManager(WeatherManager manager)
        {
            _weatherManager = manager;
        }
    }
}