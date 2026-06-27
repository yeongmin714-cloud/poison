using System.Collections;
using ProjectName.Core;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 34: Procedural 3D flag pole + flag display for territory buildings.
    /// Creates a flag pole (Cylinder) and flag (Cube/Plane) as child GameObjects,
    /// with configurable owner colors and half-mast support.
    /// Waving animation via Sin wave on flag Y offset in Update().
    /// Phase 3.5: Added FadeTransition for smooth color changes.
    /// </summary>
    public class FlagPoleDisplay : MonoBehaviour
    {
        [Header("Flag Pole Dimensions")]
        [SerializeField] private float _poleHeight = 3f;
        [SerializeField] private float _poleRadius = 0.08f;
        [SerializeField] private float _flagWidth = 1.2f;
        [SerializeField] private float _flagHeight = 0.8f;
        [SerializeField] private float _flagThickness = 0.05f;

        [Header("Waving Animation")]
        [SerializeField] private float _waveSpeed = 2f;
        [SerializeField] private float _waveAmplitude = 0.08f;

        private GameObject _poleObject;
        private GameObject _flagObject;
        private MeshRenderer _flagRenderer;
        private MeshRenderer _poleRenderer;
        private Material _flagMaterial;
        private Material _poleMaterial;

        private float _baseFlagY;
        private bool _isHalfMast;
        private bool _waveEnabled = true;
        private bool _isTransitioning;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            CreateFlagPoleVisuals();
        }

        /// <summary>
        /// Creates the procedural 3D flag pole and flag as child GameObjects.
        /// </summary>
        private void CreateFlagPoleVisuals()
        {
            // --- Pole: Cylinder ---
            _poleObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _poleObject.name = "FlagPole";
            _poleObject.transform.SetParent(transform, false);
            _poleObject.transform.localPosition = Vector3.zero;
            // Cylinder default height is 2, radius 0.5. Scale to desired dimensions.
            _poleObject.transform.localScale = new Vector3(
                _poleRadius * 2f,
                _poleHeight * 0.5f,
                _poleRadius * 2f
            );
            _poleRenderer = _poleObject.GetComponent<MeshRenderer>();
            if (_poleRenderer != null)
            {
                _poleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // --- Flag: Cube (thin rectangular flag shape) ---
            _flagObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _flagObject.name = "Flag";
            _flagObject.transform.SetParent(transform, false);

            float poleTopY = _poleHeight * 0.5f;
            _baseFlagY = poleTopY - _flagHeight * 0.25f;

            // Position flag extending forward (+Z) from pole top
            _flagObject.transform.localPosition = new Vector3(0f, _baseFlagY, _flagWidth * 0.5f + _poleRadius);

            // Scale: thin rectangular shape
            _flagObject.transform.localScale = new Vector3(
                _flagThickness,
                _flagHeight,
                _flagWidth
            );

            _flagRenderer = _flagObject.GetComponent<MeshRenderer>();
            if (_flagRenderer != null)
            {
                _flagRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // --- Set default materials ---
            UpdateFlagColor(Color.white, new Color(0.5f, 0.5f, 0.5f));
        }

        /// <summary>
        /// Updates the flag and pole materials to the specified colors.
        /// </summary>
        private void UpdateFlagColor(Color flagColor, Color poleColor)
        {
            // Clean up old flag material
            if (_flagMaterial != null)
            {
                Destroy(_flagMaterial);
                _flagMaterial = null;
            }

            _flagMaterial = MaterialHelper.CreateLitMaterial(flagColor, $"{gameObject.name}_FlagMat");
            if (_flagMaterial != null && _flagRenderer != null)
            {
                _flagRenderer.material = _flagMaterial;
            }

            if (_poleMaterial != null)
            {
                Destroy(_poleMaterial);
                _poleMaterial = null;
            }

            if (_poleRenderer != null)
            {
                _poleMaterial = MaterialHelper.CreateLitMaterial(poleColor, $"{gameObject.name}_PoleMat");
                if (_poleMaterial != null)
                {
                    _poleRenderer.material = _poleMaterial;
                }
            }
        }

        /// <summary>
        /// Gets the target colors for a given nation and player flag state.
        /// </summary>
        private Color[] GetTargetColors(NationType nation, bool isPlayer)
        {
            if (isPlayer && EmblemManager.Instance != null)
            {
                return new[]
                {
                    EmblemManager.GetEmblemColor(EmblemManager.Instance.CurrentEmblem.primaryColor),
                    EmblemManager.GetEmblemColor(EmblemManager.Instance.CurrentEmblem.secondaryColor)
                };
            }

            var flagDef = NationFlagDatabase.GetFlag(nation);
            Color flagColor = flagDef.flagColor;
            Color poleColor = Color.gray;

            switch (nation)
            {
                case NationType.None:
                    flagColor = Color.white;
                    poleColor = Color.gray;
                    break;
                case NationType.Empire:
                    poleColor = new Color(0.8f, 0.7f, 0.2f);
                    break;
                default:
                    poleColor = Color.gray;
                    break;
            }

            return new[] { flagColor, poleColor };
        }

        /// <summary>
        /// Stops any active fade transition immediately.
        /// Safe to call even if no transition is running.
        /// </summary>
        private void StopFadeIfActive()
        {
            if (_isTransitioning && _fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _isTransitioning = false;
                _fadeCoroutine = null;
            }
        }

        /// <summary>
        /// Smoothly transitions the flag material color from current to target over duration seconds.
        /// Stops waving animation during the transition if specified.
        /// </summary>
        public void FadeTransition(NationType newOwner, bool isPlayer, float duration = 0.5f)
        {
            StopFadeIfActive();

            Color[] targetColors = GetTargetColors(newOwner, isPlayer);
            _fadeCoroutine = StartCoroutine(FadeColorCoroutine(targetColors[0], targetColors[1], duration));
        }

        private IEnumerator FadeColorCoroutine(Color targetFlagColor, Color targetPoleColor, float duration)
        {
            _isTransitioning = true;
            bool wasWaveEnabled = _waveEnabled;
            SetWaveEnabled(false);

            Color startFlagColor = _flagMaterial != null ? _flagMaterial.color : Color.white;
            Color startPoleColor = _poleMaterial != null ? _poleMaterial.color : Color.gray;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Color currentFlag = Color.Lerp(startFlagColor, targetFlagColor, t);
                Color currentPole = Color.Lerp(startPoleColor, targetPoleColor, t);

                if (_flagMaterial != null)
                    _flagMaterial.color = currentFlag;

                if (_poleMaterial != null)
                    _poleMaterial.color = currentPole;

                yield return null;
            }

            // Final snap
            if (_flagMaterial != null)
                _flagMaterial.color = targetFlagColor;
            if (_poleMaterial != null)
                _poleMaterial.color = targetPoleColor;

            SetWaveEnabled(wasWaveEnabled);
            _isTransitioning = false;
            _fadeCoroutine = null;
        }

        private void Update()
        {
            if (_flagObject == null || !_waveEnabled) return;

            // Simple waving animation: Sin wave on flag Y offset
            float waveOffset = Mathf.Sin(Time.time * _waveSpeed) * _waveAmplitude;
            Vector3 pos = _flagObject.transform.localPosition;
            pos.y = GetTargetFlagY() + waveOffset;
            _flagObject.transform.localPosition = pos;
        }

        /// <summary>
        /// Returns the target Y position for the flag based on half-mast state.
        /// </summary>
        private float GetTargetFlagY()
        {
            return _isHalfMast ? _baseFlagY * 0.5f : _baseFlagY;
        }

        /// <summary>
        /// Sets the owner of this territory and updates flag colors accordingly.
        /// Uses NationFlagDatabase for nation colors or EmblemManager for player colors.
        /// </summary>
        public void SetOwner(NationType nation, bool isPlayer)
        {
            StopFadeIfActive();

            if (isPlayer)
            {
                SetPlayerFlag();
                return;
            }

            Color[] colors = GetTargetColors(nation, false);
            UpdateFlagColor(colors[0], colors[1]);
        }

        /// <summary>
        /// Sets the flag to half-mast (contested state) or restores full-mast.
        /// </summary>
        public void SetHalfMast(bool halfMast)
        {
            _isHalfMast = halfMast;

            // Immediately update position if animating
            if (_flagObject != null)
            {
                Vector3 pos = _flagObject.transform.localPosition;
                pos.y = GetTargetFlagY();
                _flagObject.transform.localPosition = pos;
            }
        }

        /// <summary>
        /// Sets the flag to use player emblem colors via EmblemManager.
        /// Uses primary color for the flag and secondary for the pole.
        /// </summary>
        public void SetPlayerFlag()
        {
            StopFadeIfActive();

            if (EmblemManager.Instance != null)
            {
                Material playerMat = EmblemManager.Instance.CreateFlagMaterial();
                if (playerMat != null && _flagRenderer != null)
                {
                    if (_flagMaterial != null)
                    {
                        Destroy(_flagMaterial);
                    }
                    _flagMaterial = playerMat;
                    _flagRenderer.material = _flagMaterial;
                }

                // Pole uses secondary emblem color
                Material secMat = EmblemManager.Instance.CreateSecondaryMaterial();
                if (secMat != null && _poleRenderer != null)
                {
                    if (_poleMaterial != null)
                    {
                        Destroy(_poleMaterial);
                    }
                    _poleMaterial = secMat;
                    _poleRenderer.material = _poleMaterial;
                }
            }
        }

        /// <summary>
        /// Enables or disables the waving animation.
        /// </summary>
        public void SetWaveEnabled(bool enabled)
        {
            _waveEnabled = enabled;
        }

        /// <summary>
        /// Returns true if waving animation is currently enabled.
        /// </summary>
        public bool IsWaveEnabled => _waveEnabled;

        /// <summary>
        /// Returns true if the flag is currently at half-mast.
        /// </summary>
        public bool IsHalfMast => _isHalfMast;

        /// <summary>
        /// Returns the current flag material (for testing/inspection).
        /// </summary>
        public Material CurrentFlagMaterial => _flagMaterial;

        /// <summary>
        /// Returns the current pole material (for testing/inspection).
        /// </summary>
        public Material CurrentPoleMaterial => _poleMaterial;

        /// <summary>
        /// Returns true if a fade transition is currently in progress.
        /// </summary>
        public bool IsTransitioning => _isTransitioning;

        /// <summary>
        /// Returns the flag child GameObject.
        /// </summary>
        public GameObject FlagObject => _flagObject;

        /// <summary>
        /// Returns the pole child GameObject.
        /// </summary>
        public GameObject PoleObject => _poleObject;

        /// <summary>
        /// Returns the base Y position of the flag (full-mast height).
        /// </summary>
        public float BaseFlagY => _baseFlagY;

        private void OnDestroy()
        {
            if (_flagMaterial != null)
            {
                Destroy(_flagMaterial);
                _flagMaterial = null;
            }
            if (_poleMaterial != null)
            {
                Destroy(_poleMaterial);
                _poleMaterial = null;
            }
        }
    }
}