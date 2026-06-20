using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// G1-08: Ambient Effect Manager — Singleton that manages biome-based
    /// ambient particle effects (fireflies, leaves, dust, embers).
    /// Detects the player's current region/biome and enables/disables
    /// the appropriate particle effects automatically.
    ///
    /// Effect mapping by nation/biome:
    ///   East (Plains/Forest):   Fireflies + Falling Leaves
    ///   West (Reed/Desert):     Dust
    ///   South (Desert/Volcanic): Embers + Dust
    ///   North (Tundra/Mountain): Falling Leaves (frost-like)
    ///   Empire:                  Fireflies + Embers
    /// </summary>
    public class AmbientEffectManager : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static AmbientEffectManager _instance;
        private static bool _instanceQuitting;

        public static AmbientEffectManager Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("AmbientEffectManager");
                    _instance = go.AddComponent<AmbientEffectManager>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        // ================================================================
        // Ambient Effect Enum
        // ================================================================

        public enum AmbientEffectType
        {
            None,
            Fireflies,
            Leaves,
            Dust,
            Embers
        }

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("Detection")]
        [SerializeField] private Transform _player;
        [SerializeField] private float _updateInterval = 1f;
        [SerializeField] private float _effectRadius = 30f;

        [Header("Fireflies (grassland/forest — East)")]
        [SerializeField] private int _firefliesMaxParticles = 150;
        [SerializeField] private float _firefliesEmissionRate = 10f;
        [SerializeField] private Color _firefliesColor = new Color(1f, 0.9f, 0.4f);
        [SerializeField] private float _firefliesSineSpeed = 2f;
        [SerializeField] private float _firefliesSineAmplitude = 2f;

        [Header("Leaves (near trees — East/North)")]
        [SerializeField] private int _leavesMaxParticles = 75;
        [SerializeField] private float _leavesEmissionRate = 8f;
        [SerializeField] private Color _leavesColor1 = new Color(0.55f, 0.35f, 0.15f);
        [SerializeField] private Color _leavesColor2 = new Color(0.30f, 0.55f, 0.15f);

        [Header("Dust (desert/wasteland — West/South)")]
        [SerializeField] private int _dustMaxParticles = 40;
        [SerializeField] private float _dustEmissionRate = 5f;
        [SerializeField] private Color _dustColor = new Color(0.65f, 0.55f, 0.35f);

        [Header("Embers (near buildings/Empire/South)")]
        [SerializeField] private int _embersMaxParticles = 30;
        [SerializeField] private float _embersEmissionRate = 6f;
        [SerializeField] private Color _embersColor = new Color(1f, 0.5f, 0.1f);

        // ================================================================
        // Private State
        // ================================================================

        private ParticleSystem _firefliesSystem;
        private ParticleSystem _leavesSystem;
        private ParticleSystem _dustSystem;
        private ParticleSystem _embersSystem;

        private GameObject _firefliesGo;
        private GameObject _leavesGo;
        private GameObject _dustGo;
        private GameObject _embersGo;

        private NationType _currentNation = NationType.East;
        private BiomeType _currentBiome = BiomeType.Plains;
        private float _updateTimer;
        private bool _initialized;

        // ================================================================
        // Public Properties (for tests)
        // ================================================================

        public NationType CurrentNation => _currentNation;
        public BiomeType CurrentBiome => _currentBiome;

        public ParticleSystem FirefliesSystem => _firefliesSystem;
        public ParticleSystem LeavesSystem => _leavesSystem;
        public ParticleSystem DustSystem => _dustSystem;
        public ParticleSystem EmbersSystem => _embersSystem;

        public bool HasFirefliesSystem => _firefliesSystem != null;
        public bool HasLeavesSystem => _leavesSystem != null;
        public bool HasDustSystem => _dustSystem != null;
        public bool HasEmbersSystem => _embersSystem != null;

        public GameObject FirefliesGo => _firefliesGo;
        public GameObject LeavesGo => _leavesGo;
        public GameObject DustGo => _dustGo;
        public GameObject EmbersGo => _embersGo;

        public bool Initialized => _initialized;

        /// <summary>Current effective ambient effects given the detected biome.</summary>
        public AmbientEffectType CurrentEffect
        {
            get
            {
                return _currentNation switch
                {
                    NationType.East => AmbientEffectType.Fireflies,
                    NationType.West => AmbientEffectType.Dust,
                    NationType.South => AmbientEffectType.Embers,
                    NationType.North => AmbientEffectType.Leaves,
                    NationType.Empire => AmbientEffectType.Fireflies,
                    _ => AmbientEffectType.None
                };
            }
        }

        // ================================================================
        // Unity Lifecycle
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
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_initialized)
                return;

            // Update detection timer
            _updateTimer -= Time.deltaTime;
            if (_updateTimer <= 0f)
            {
                _updateTimer = _updateInterval;
                DetectBiomeAndUpdateEffects();
            }

            // Animate fireflies with sine wave movement
            if (_firefliesGo != null && _firefliesGo.activeSelf && _firefliesSystem != null)
            {
                AnimateFireflies();
            }
        }

        // ================================================================
        // Initialization
        // ================================================================

        private void Initialize()
        {
            if (_initialized) return;

            // Resolve player transform
            if (_player == null)
            {
                var playerMove = FindObjectOfType<PlayerMovement>();
                if (playerMove != null)
                    _player = playerMove.transform;
                else
                    _player = Camera.main?.transform ?? new GameObject("AmbientOrigin").transform;
            }

            // Create all four particle systems
            _firefliesGo = CreateParticleSystem("FirefliesParticles", _firefliesMaxParticles);
            _firefliesSystem = _firefliesGo.GetComponent<ParticleSystem>();
            ConfigureFireflies();

            _leavesGo = CreateParticleSystem("LeavesParticles", _leavesMaxParticles);
            _leavesSystem = _leavesGo.GetComponent<ParticleSystem>();
            ConfigureLeaves();

            _dustGo = CreateParticleSystem("DustParticles", _dustMaxParticles);
            _dustSystem = _dustGo.GetComponent<ParticleSystem>();
            ConfigureDust();

            _embersGo = CreateParticleSystem("EmbersParticles", _embersMaxParticles);
            _embersSystem = _embersGo.GetComponent<ParticleSystem>();
            ConfigureEmbers();

            // Start with all disabled
            SetAllEffectsActive(false);

            // Immediate detection
            _updateTimer = 0f;
            DetectBiomeAndUpdateEffects();

            _initialized = true;
            Debug.Log("[AmbientEffectManager] Initialized with all ambient particle systems.");
        }

        private GameObject CreateParticleSystem(string name, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.maxParticles = maxParticles;
            main.startLifetime = 4f;
            main.startSpeed = 1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = true;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = _effectRadius;
            shape.randomDirectionAmount = 1f;

            // Renderer
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = CreateAmbientParticleMaterial();

            return go;
        }

        // ================================================================
        // Particle Configuration
        // ================================================================

        private void ConfigureFireflies()
        {
            var main = _firefliesSystem.main;
            main.startLifetime = 6f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new Color(_firefliesColor.r, _firefliesColor.g, _firefliesColor.b, 0.7f);

            var emission = _firefliesSystem.emission;
            emission.rateOverTime = _firefliesEmissionRate;

            var colorOverLifetime = _firefliesSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fadeAlpha = new ParticleSystem.MinMaxGradient(
                new Color(1, 1, 1, 0.9f),
                new Color(1, 1, 1, 0f));
            fadeAlpha.mode = ParticleSystemGradientMode.TwoColors;
            colorOverLifetime.color = fadeAlpha;

            var sizeOverLifetime = _firefliesSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);

            // Unity 6: assign procedural texture via renderer material instead of startTexture
            var firefliesRenderer = _firefliesSystem.GetComponent<ParticleSystemRenderer>();
            firefliesRenderer.material.mainTexture = CreateProceduralParticleTexture(8, 8, _firefliesColor);
        }

        private void ConfigureLeaves()
        {
            var main = _leavesSystem.main;
            main.startLifetime = 5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new Color(0.65f, 0.50f, 0.25f, 0.7f);
            main.gravityModifier = 0.3f;

            var emission = _leavesSystem.emission;
            emission.rateOverTime = _leavesEmissionRate;

            var colorOverLifetime = _leavesSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fadeAlpha = new ParticleSystem.MinMaxGradient(
                new Color(1, 1, 1, 0.8f),
                new Color(1, 1, 1, 0f));
            fadeAlpha.mode = ParticleSystemGradientMode.TwoColors;
            colorOverLifetime.color = fadeAlpha;

            var velocity = _leavesSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            // Rotate leaves
            var rotationOverLifetime = _leavesSystem.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

            // Unity 6: assign procedural texture via renderer material instead of startTexture
            var leavesRenderer = _leavesSystem.GetComponent<ParticleSystemRenderer>();
            leavesRenderer.material.mainTexture = CreateProceduralParticleTexture(12, 8, _leavesColor1);
        }

        private void ConfigureDust()
        {
            var main = _dustSystem.main;
            main.startLifetime = 3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new Color(_dustColor.r, _dustColor.g, _dustColor.b, 0.5f);

            var emission = _dustSystem.emission;
            emission.rateOverTime = _dustEmissionRate;

            var colorOverLifetime = _dustSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fadeAlpha = new ParticleSystem.MinMaxGradient(
                new Color(1, 1, 1, 0.6f),
                new Color(1, 1, 1, 0f));
            fadeAlpha.mode = ParticleSystemGradientMode.TwoColors;
            colorOverLifetime.color = fadeAlpha;

            var velocity = _dustSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            // Unity 6: assign procedural texture via renderer material instead of startTexture
            var dustRenderer = _dustSystem.GetComponent<ParticleSystemRenderer>();
            dustRenderer.material.mainTexture = CreateProceduralParticleTexture(6, 6, _dustColor);
        }

        private void ConfigureEmbers()
        {
            var main = _embersSystem.main;
            main.startLifetime = 5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new Color(_embersColor.r, _embersColor.g, _embersColor.b, 0.8f);
            main.gravityModifier = -0.1f; // Float upward

            var emission = _embersSystem.emission;
            emission.rateOverTime = _embersEmissionRate;

            var colorOverLifetime = _embersSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new ParticleSystem.MinMaxGradient(
                new Color(1, 1, 1, 0.9f),
                new Color(1, 1, 1, 0f));
            fade.mode = ParticleSystemGradientMode.TwoColors;
            colorOverLifetime.color = fade;

            var velocity = _embersSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            var sizeOverLifetime = _embersSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);

            // Unity 6: assign procedural texture via renderer material instead of startTexture
            var embersRenderer = _embersSystem.GetComponent<ParticleSystemRenderer>();
            embersRenderer.material.mainTexture = CreateProceduralParticleTexture(8, 8, _embersColor);
        }

        // ================================================================
        // Procedural Texture Helpers
        // ================================================================

        private static Texture2D CreateProceduralParticleTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "AmbientParticle_" + color.r + "_" + color.g + "_" + color.b;

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

        private static Material CreateAmbientParticleMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Particles/Standard Unlit");

            var mat = new Material(shader);
            mat.name = "AmbientParticleMaterial";
            return mat;
        }

        // ================================================================
        // Biome Detection
        // ================================================================

        /// <summary>
        /// Detect the current nation and biome from the player's position,
        /// then enable the appropriate ambient effects.
        /// </summary>
        public void DetectBiomeAndUpdateEffects()
        {
            if (_player == null)
                return;

            Vector3 pos = _player.position;
            NationType detectedNation = NationTerrainController.GetNationFromPosition(pos);
            _currentNation = detectedNation;

            // Determine a representative biome for the detected nation
            // Use the deterministic mapping from BiomeData
            _currentBiome = BiomeData.GetBiomeForTerritory(detectedNation, 0);

            ApplyEffectsForNation(detectedNation);
        }

        /// <summary>
        /// Applies the correct ambient effects based on the given nation.
        /// </summary>
        private void ApplyEffectsForNation(NationType nation)
        {
            // Disable all first
            SetAllEffectsActive(false);

            switch (nation)
            {
                case NationType.East:
                    // Grassland/forest: Fireflies + Leaves
                    SetEffectActive(_firefliesGo, _firefliesSystem, true);
                    SetEffectActive(_leavesGo, _leavesSystem, true);
                    break;

                case NationType.West:
                    // Desert/wasteland: Dust
                    SetEffectActive(_dustGo, _dustSystem, true);
                    break;

                case NationType.South:
                    // Desert/volcanic: Embers + Dust
                    SetEffectActive(_embersGo, _embersSystem, true);
                    SetEffectActive(_dustGo, _dustSystem, true);
                    break;

                case NationType.North:
                    // Tundra/mountain: Leaves (frost-like)
                    SetEffectActive(_leavesGo, _leavesSystem, true);
                    break;

                case NationType.Empire:
                    // Empire center: Fireflies + Embers
                    SetEffectActive(_firefliesGo, _firefliesSystem, true);
                    SetEffectActive(_embersGo, _embersSystem, true);
                    break;

                default:
                    break;
            }
        }

        private void SetAllEffectsActive(bool active)
        {
            SetEffectActive(_firefliesGo, _firefliesSystem, active);
            SetEffectActive(_leavesGo, _leavesSystem, active);
            SetEffectActive(_dustGo, _dustSystem, active);
            SetEffectActive(_embersGo, _embersSystem, active);
        }

        private static void SetEffectActive(GameObject go, ParticleSystem ps, bool active)
        {
            if (go == null || ps == null)
                return;

            go.SetActive(active);

            if (active && !ps.isPlaying)
                ps.Play();
            else if (!active && ps.isPlaying)
                ps.Stop();
        }

        // ================================================================
        // Firefly Animation (Sine Wave Movement)
        // ================================================================

        private void AnimateFireflies()
        {
            if (_firefliesSystem == null || !_firefliesSystem.isPlaying)
                return;

            // Use velocity over lifetime to give fireflies gentle sine-wave motion
            float sineVal = Mathf.Sin(Time.time * _firefliesSineSpeed) * _firefliesSineAmplitude;

            var velocity = _firefliesSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(sineVal * -0.3f, sineVal * 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(
                _firefliesSineAmplitude * 0.1f,
                _firefliesSineAmplitude * 0.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(sineVal * -0.3f, sineVal * 0.3f);
        }

        // ================================================================
        // Public API (for tests and editor)
        // ================================================================

        /// <summary>Set player transform reference.</summary>
        public void SetPlayerTransform(Transform player)
        {
            _player = player;
        }

        /// <summary>Force manual detection. Used by editor and tests.</summary>
        public void ForceDetectAndUpdate()
        {
            DetectBiomeAndUpdateEffects();
        }

        /// <summary>Force (re)initialization. Safe to call multiple times.</summary>
        public void ForceInitialize()
        {
            // Clean up existing systems before re-initializing
            DestroyParticleSystem(ref _firefliesGo, ref _firefliesSystem);
            DestroyParticleSystem(ref _leavesGo, ref _leavesSystem);
            DestroyParticleSystem(ref _dustGo, ref _dustSystem);
            DestroyParticleSystem(ref _embersGo, ref _embersSystem);

            _initialized = false;
            Initialize();
        }

        private static void DestroyParticleSystem(ref GameObject go, ref ParticleSystem ps)
        {
            if (go != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(go);
                else
                    Object.DestroyImmediate(go);
                go = null;
                ps = null;
            }
        }

        /// <summary>Get the update interval setting.</summary>
        public float UpdateInterval => _updateInterval;

        /// <summary>Get the effect radius setting.</summary>
        public float EffectRadius => _effectRadius;

        // ================================================================
        // Editor helpers (accessed via reflection in tests)
        // ================================================================

        internal void SetUpdateInterval(float value)
        {
            _updateInterval = value;
        }
    }
}
