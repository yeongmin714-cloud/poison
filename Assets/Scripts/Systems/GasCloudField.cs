using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Radially expanding, ground-hugging gas cloud VFX built from the project's soft cloud sprites.
    /// The cloud is deployed once at its spawn point; it never follows the source transform.
    /// </summary>
    public class GasCloudField : MonoBehaviour
    {
        private const float ExpansionDuration = 0.6f;
        private const float FadeDuration = 0.8f;

        [SerializeField] private PotionType _affectingType = PotionType.None;

        private readonly List<ParticleSystem> _cloudSystems = new List<ParticleSystem>();
        private readonly List<ParticleSystemRenderer> _cloudRenderers = new List<ParticleSystemRenderer>();
        private readonly List<Material> _createdMaterials = new List<Material>();
        private float _radius = 1f;
        private float _duration = 5f;
        private float _elapsed;
        private float _expansionStartScale = 0.3f;
        private bool _configured;
        private bool _emissionStopped;

        /// <summary>The potion type this field represents. Can be updated by gameplay integration.</summary>
        public PotionType AffectingType
        {
            get { return _affectingType; }
            set
            {
                _affectingType = value;
                ApplyTint();
            }
        }

        /// <summary>Convenience hook for systems that need to respect an active gas mask.</summary>
        public bool IsProtectionActive { get { return GasMaskSystem.IsActive; } }

        /// <summary>
        /// Deploys a gas cloud at the target's current position plus offset. The source is not
        /// retained as a parent, so the field remains stationary after it has been spawned.
        /// </summary>
        public static GameObject Spawn(Transform followTarget, Vector3 offset, float radius, float duration, PotionType type)
        {
            Vector3 spawnPosition = (followTarget != null ? followTarget.position : Vector3.zero) + offset;
            GameObject cloudObject = new GameObject("GasCloudField");
            cloudObject.transform.position = spawnPosition;

            GasCloudField field = cloudObject.AddComponent<GasCloudField>();
            field.Configure(radius, duration, type);
            return cloudObject;
        }

        /// <summary>Configures or reconfigures the field and builds its particle systems.</summary>
        public void Configure(float radius, float duration, PotionType type)
        {
            ClearVisuals();
            _radius = Mathf.Max(0.1f, radius);
            _duration = Mathf.Max(ExpansionDuration, duration);
            _elapsed = 0f;
            _emissionStopped = false;
            _affectingType = type;
            _expansionStartScale = Mathf.Clamp(1.5f / _radius, 0.05f, 1f);
            transform.localScale = Vector3.one * _expansionStartScale;

            Texture2D softTexture = Resources.Load<Texture2D>("UI/GasCloudSoft");
            Texture2D puffTexture = Resources.Load<Texture2D>("UI/GasCloudPuff");
            Texture2D wispTexture = Resources.Load<Texture2D>("UI/GasCloudWisp");
            Texture2D ringTexture = Resources.Load<Texture2D>("UI/GasCloudRing");

            if (softTexture != null)
                CreateCloudSystem("GasCloudSoft", softTexture, 24f, 18, _radius * 0.72f, 2.4f, 1.1f, false);
            if (puffTexture != null)
                CreateCloudSystem("GasCloudPuff", puffTexture, 11f, 10, _radius * 0.55f, 2.0f, 0.8f, false);
            if (wispTexture != null)
                CreateCloudSystem("GasCloudWisp", wispTexture, 6f, 6, _radius * 0.82f, 3.0f, 0.38f, false);
            if (ringTexture != null)
                CreateCloudSystem("GasCloudRing", ringTexture, 0f, 1, 0.01f, ExpansionDuration, _radius * 2f, true);

            ApplyTint();
            _configured = true;
        }

        private void Update()
        {
            if (!_configured)
                return;

            _elapsed += Time.deltaTime;
            float expansion = Mathf.Clamp01(_elapsed / ExpansionDuration);
            expansion = expansion * expansion * (3f - 2f * expansion);
            float targetScale = Mathf.Lerp(_expansionStartScale, 1f, expansion);
            transform.localScale = Vector3.one * targetScale;

            if (!_emissionStopped && _elapsed >= _duration)
            {
                StopCloudEmission();
                _emissionStopped = true;
            }

            float fade = _emissionStopped ? Mathf.Clamp01(1f - (_elapsed - _duration) / FadeDuration) : 1f;
            for (int i = 0; i < _createdMaterials.Count; i++)
                SetMaterialColor(_createdMaterials[i], GetTypeTint(_affectingType, fade));

            if (_emissionStopped && _elapsed >= _duration + FadeDuration)
                Destroy(gameObject);
        }

        private void CreateCloudSystem(string systemName, Texture2D texture, float emissionRate,
            int burstCount, float shapeRadius, float particleLifetime, float particleSize, bool expansionRing)
        {
            GameObject systemObject = new GameObject(systemName);
            systemObject.transform.SetParent(transform, false);

            ParticleSystem particleSystem = systemObject.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = systemObject.GetComponent<ParticleSystemRenderer>();
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = Mathf.Max(_duration + FadeDuration, 1f);
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = particleLifetime;
            main.startSpeed = expansionRing ? 0f : 0.08f;
            main.startSize = particleSize;
            main.startColor = Color.white;
            main.gravityModifier = expansionRing ? 0f : 0.025f;
            main.maxParticles = expansionRing ? 1 : Mathf.Max(48, burstCount * 3);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = expansionRing ? 0f : emissionRate;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = !expansionRing;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.01f, shapeRadius);
            shape.radiusThickness = 0.85f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = !expansionRing;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.radial = 0.12f;
            velocity.y = -0.025f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient alphaGradient = new Gradient();
            alphaGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.52f, 0.12f), new GradientAlphaKey(0.32f, 0.72f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = expansionRing;
            if (expansionRing)
            {
                AnimationCurve ringSize = new AnimationCurve(
                    new Keyframe(0f, 0.08f),
                    new Keyframe(0.72f, 0.82f),
                    new Keyframe(1f, 1f));
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, ringSize);
            }

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.5f;
            Material material = CreateSpriteMaterial(texture);
            if (material != null)
            {
                renderer.sharedMaterial = material;
                _createdMaterials.Add(material);
                _cloudRenderers.Add(renderer);
            }

            _cloudSystems.Add(particleSystem);
            particleSystem.Play();
        }

        private static Material CreateSpriteMaterial(Texture2D texture)
        {
            // URP-first fallback chain, matching the project's established particle material setup.
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Simple Lit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("[GasCloudField] No compatible particle shader was found.");
                return null;
            }

            Material material = new Material(shader);
            material.name = "GasCloudField_" + texture.name + "_Generated";
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            return material;
        }

        private void ApplyTint()
        {
            Color tint = GetTypeTint(_affectingType, 1f);
            for (int i = 0; i < _createdMaterials.Count; i++)
                SetMaterialColor(_createdMaterials[i], tint);
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private static Color GetTypeTint(PotionType type, float alpha)
        {
            Color tint;
            switch (type)
            {
                case PotionType.Poison: tint = new Color(1f, 0.15f, 0.15f, 1f); break;
                case PotionType.Mental: tint = new Color(0.6f, 0.15f, 0.85f, 1f); break;
                case PotionType.Heal: tint = new Color(0.15f, 0.85f, 0.15f, 1f); break;
                case PotionType.Buff: tint = new Color(0.15f, 0.35f, 0.95f, 1f); break;
                default: tint = Color.white; break;
            }
            tint.a = alpha * 0.48f;
            return tint;
        }

        private void StopCloudEmission()
        {
            for (int i = 0; i < _cloudSystems.Count; i++)
            {
                if (_cloudSystems[i] == null)
                    continue;
                ParticleSystem.EmissionModule emission = _cloudSystems[i].emission;
                emission.enabled = false;
                _cloudSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void ClearVisuals()
        {
            for (int i = 0; i < _cloudSystems.Count; i++)
            {
                if (_cloudSystems[i] != null)
                    Destroy(_cloudSystems[i].gameObject);
            }
            for (int i = 0; i < _createdMaterials.Count; i++)
            {
                if (_createdMaterials[i] != null)
                    Destroy(_createdMaterials[i]);
            }
            _cloudSystems.Clear();
            _cloudRenderers.Clear();
            _createdMaterials.Clear();
        }

        private void OnDestroy()
        {
            ClearVisuals();
        }
    }
}
