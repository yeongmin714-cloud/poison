using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 4: 가스 분사기 — 플레이어 등(Back 슬롯)에 장착하는 가스 분사기.
    /// G 키로 분사 On/Off, GasSprayerController 상태에 따라 효과 적용.
    /// - 물약/독약/마약 삽입 가능
    /// - 등급별 최대 분사 시간 (GasSprayerManager 참조)
    /// - 빈 상태에서 새 물약 넣으면 재사용 가능 (재장전)
    /// </summary>
    public class GasSprayer : MonoBehaviour
    {
        [Header("Spray Controls")]
        // G 키는 Input System Keyboard.current.gKey로 직접 처리

        [Header("Effect Settings")]
        [SerializeField] private LayerMask _targetLayers = -1; // Default: Everything
        [SerializeField] private float _effectInterval = 0.5f;  // 효과 체크 간격 (초)

        [Header("Poison (공격성/독)")]
        [SerializeField] private float _minPoisonDamage = 5f;
        [SerializeField] private float _maxPoisonDamage = 15f;

        [Header("Mental (정신성/마약)")]
        [SerializeField] private float _confusionDuration = 3f;
        [SerializeField] private float _slowAmount = 0.3f;

        [Header("Heal (회복성/치료)")]
        [SerializeField] private float _allyHealAmount = 10f;

        [Header("Buff (물리성/강화)")]
        [SerializeField] private float _allyBuffDuration = 5f;
        [SerializeField] private float _allyDefenseBuff = 10f;
        [SerializeField] private float _allyAttackBuff = 5f;

        [Header("Spray Fog Colors")]
        [SerializeField] private Color _poisonFogColor = new Color(0.2f, 0.65f, 0.08f, 0.5f);  // 독성 짙은 초록 안개
        [SerializeField] private Color _mentalFogColor = new Color(0.6f, 0.15f, 0.85f, 0.45f); // 보라색 안개
        [SerializeField] private Color _healFogColor = new Color(0.15f, 0.85f, 0.15f, 0.45f);  // 초록색 안개
        [SerializeField] private Color _buffFogColor = new Color(0.15f, 0.35f, 0.95f, 0.45f);  // 파란색 안개

        // ── Internal state ───────────────────────────────────────────────
        private GasSprayerController _controller;
        private float _effectTimer;
        private bool _lastFrameSpraying;

        // Persistent, bounded ParticleSystems form the visual pool. Their world-space
        // particles retain the travelled path while the nozzles follow the player.
        private readonly List<ParticleSystem> _plumeSystems = new List<ParticleSystem>(3);
        private readonly List<ParticleSystemRenderer> _plumeRenderers = new List<ParticleSystemRenderer>(3);
        private readonly List<Material> _plumeMaterials = new List<Material>(3);
        private readonly List<Texture2D> _plumeTextures = new List<Texture2D>(3);
        private static readonly Dictionary<Texture2D, Material> SharedGasMaterials =
            new Dictionary<Texture2D, Material>();
        private static readonly Dictionary<Texture2D, int> SharedGasMaterialUsers =
            new Dictionary<Texture2D, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void CleanupUnusedGasMaterials()
        {
            if (SharedGasMaterials.Count == 0) return;

            var unusedTextures = new List<Texture2D>();
            foreach (var entry in SharedGasMaterials)
            {
                if (!SharedGasMaterialUsers.TryGetValue(entry.Key, out int users) || users <= 0)
                    unusedTextures.Add(entry.Key);
            }

            for (int i = 0; i < unusedTextures.Count; i++)
                RemoveGasMaterial(unusedTextures[i]);
        }

        private static void RemoveGasMaterial(Texture2D texture)
        {
            if (ReferenceEquals(texture, null) || !SharedGasMaterials.TryGetValue(texture, out Material material))
                return;

            SharedGasMaterials.Remove(texture);
            SharedGasMaterialUsers.Remove(texture);
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        private static void RetainGasMaterial(Texture2D texture)
        {
            if (texture == null) return;
            SharedGasMaterialUsers.TryGetValue(texture, out int users);
            SharedGasMaterialUsers[texture] = users + 1;
        }

        private static void ReleaseGasMaterial(Texture2D texture)
        {
            if (texture == null || !SharedGasMaterialUsers.TryGetValue(texture, out int users))
                return;

            users--;
            if (users <= 0) RemoveGasMaterial(texture);
            else SharedGasMaterialUsers[texture] = users;
        }

        // ── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _controller = GasSprayerController.Instance;
            if (_controller == null)
            {
                Debug.LogWarning("[GasSprayer] GasSprayerController.Instance를 찾을 수 없습니다. GasSprayer 비활성화.");
                enabled = false;
                return;
            }

            CreatePlumePool();

            _controller.OnSprayingChanged += HandleSprayingChanged;
            _controller.OnPotionDoseDepleted += HandlePotionDoseDepleted;
            _controller.OnGPressRequested += HandleGPressRequested;
        }

        private void HandleSprayingChanged(bool spraying)
        {
            InvokeGasSprayUIMethod(spraying ? "ShowSprayStatus" : "HideSprayStatus", spraying);
        }

        private void HandlePotionDoseDepleted()
        {
            if (_controller != null && !_controller.IsSpraying && _controller.LoadedPotionCount <= 0)
                InvokeGasSprayUIAction("ShowDoseExhaustedStatus");
        }

        private void HandleGPressRequested()
        {
            // A deliberate G press always wins over a pending exhausted-dose notice.
            InvokeGasSprayUIAction("HideSprayStatus");
        }

        private static void InvokeGasSprayUIAction(string methodName)
        {
            InvokeGasSprayUIMethod(methodName, false);
        }

        // Reflection keeps Systems independent of the ProjectName.UI assembly.
        private static void InvokeGasSprayUIMethod(string methodName, bool ensure)
        {
            var uiType = System.Type.GetType("ProjectName.UI.Toolkit.GasSprayUTK, ProjectName.UI");
            if (uiType == null) return;

            const System.Reflection.BindingFlags staticFlags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
            if (ensure)
                uiType.GetMethod("Ensure", staticFlags)?.Invoke(null, null);

            var instance = uiType.GetProperty("Instance", staticFlags)?.GetValue(null);
            if (instance == null) return;
            instance.GetType().GetMethod(methodName)?.Invoke(instance, null);
        }

        private void Start()
        {
            // 초기 효과 타이머 설정 (0.5초 후 첫 효과)
            _effectTimer = _effectInterval;
        }

        private void Update()
        {
            if (_controller == null) return;

            // G is read only here. isPressed makes spray last exactly as long as the hold;
            // focus/device loss resets the control and is treated as a release.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool gHeld = keyboard != null && keyboard.gKey.isPressed;
            _controller.SetGInputHeld(gHeld);

            bool gameplaySpraying = _controller.IsSpraying && _controller.IsEquipped && !_controller.IsReloading;
            // 분사 중 효과 처리
            if (gameplaySpraying)
            {
                if (!_lastFrameSpraying)
                {
                    // 분사 시작 시 초기화
                    _effectTimer = 0f;
                    _lastFrameSpraying = true;
                }

                // 주기적 효과 적용
                _effectTimer += Time.deltaTime;
                while (_effectTimer >= _effectInterval)
                {
                    _effectTimer -= _effectInterval;
                    ApplySprayEffects();
                }

                EmitPlume();
            }
            else
            {
                StopPlumeEmission();
                if (_lastFrameSpraying)
                {
                    _lastFrameSpraying = false;
                    _effectTimer = _effectInterval; // 리셋
                }
            }

        }

        // ── Effect Application ───────────────────────────────────────────

        /// <summary>
        /// 현재 장전된 물약 타입에 따라 적/아군에게 효과 적용.
        /// </summary>
        private void ApplySprayEffects()
        {
            GasSprayerData data = _controller.GetCurrentSprayerData();
            float range = data.sprayRange;
            Vector3 origin = transform.position + Vector3.up * 0.5f; // 약간 위에서 분사

            // 장전된 물약 타입 확인
            string potionId = _controller.LoadedPotionId;
            PotionType potionType = ClassifyPotion(potionId);

            // 범위 내 모든 Collider 감지
            Collider[] hits = Physics.OverlapSphere(origin, range, _targetLayers);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue; // 자기 자신 제외

                switch (potionType)
                {
                    case PotionType.Poison:
                        ApplyPoisonEffect(hit);
                        break;
                    case PotionType.Mental:
                        ApplyMentalEffect(hit);
                        break;
                    case PotionType.Heal:
                        ApplyHealEffect(hit);
                        break;
                    case PotionType.Buff:
                        ApplyBuffEffect(hit);
                        break;
                    case PotionType.None:
                    default:
                        // 물약 없음 = 기본 분사 효과 없음
                        break;
                }
            }
        }

        /// <summary>
        /// 공격성(독) 효과: 짙은 초록 안개, 적에게 지속 데미지 5~15
        /// </summary>
        private void ApplyPoisonEffect(Collider target)
        {
            // 적 여부 확인: IDamageable 구현체인지 (적/몬스터)
            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null) return;

            // [2026-10-08] 플레이어는 방독면 착용 중이면 독가스 효과를 받지 않는다.
            if (target.CompareTag("Player") && GasMaskSystem.IsActive) return;

            float damage = Random.Range(_minPoisonDamage, _maxPoisonDamage);
            Vector3 hitDir = (target.transform.position - transform.position).normalized;
            damageable.TakeDamage(damage, hitDir, "GasSprayer_Poison");
        }

        /// <summary>
        /// 정신성(마약) 효과: 보라색 안개, 적 환각/혼란 (슬로우 + 혼란 효과)
        /// </summary>
        private void ApplyMentalEffect(Collider target)
        {
            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null) return;

            // IDamageable이면 적으로 간주 — 슬로우/혼란 버프 부여
            if (BuffManager.Instance != null)
            {
                // 슬로우 효과
                BuffManager.Instance.AddBuff("Slowness", _slowAmount, _confusionDuration);
                // 혼란 효과 (커스텀 버프 ID)
                BuffManager.Instance.AddBuff("Confusion", 1f, _confusionDuration);
            }
        }

        /// <summary>
        /// 회복성(치료) 효과: 초록색 안개, 아군 체력 회복
        /// </summary>
        private void ApplyHealEffect(Collider target)
        {
            // 플레이어 자신이거나 아군이면 회복
            if (target.CompareTag("Player"))
            {
                if (PlayerHealth.Instance != null && !PlayerHealth.Instance.IsDead)
                {
                    PlayerHealth.Instance.Heal(_allyHealAmount);
                }
            }
            // 아군 NPC 등은 IDamageable이 아니므로 PlayerHealth만 우선 처리
        }

        /// <summary>
        /// 물리성(강화) 효과: 파란색 안개, 아군 버프 (방어력/공격력 증가)
        /// </summary>
        private void ApplyBuffEffect(Collider target)
        {
            if (target.CompareTag("Player"))
            {
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.AddBuff("DefenseUp", _allyDefenseBuff, _allyBuffDuration);
                    BuffManager.Instance.AddBuff("AttackUp", _allyAttackBuff, _allyBuffDuration);
                }
            }
        }

        // ── Spray Visual Effect ──────────────────────────────────────────

        private void CreatePlumePool()
        {
            CreatePlumeLayer("GasSprayer_Soft", "UI/GasPlumeSoft", 48f, 3f, 0.78f, 0.4f, 0.9f, 0.24f);
            CreatePlumeLayer("GasSprayer_Puff", "UI/GasPlumeLobe", 24f, 3f, 0.62f, 0.75f, 1.2f, 0.42f);
            CreatePlumeLayer("GasSprayer_Wisp", "UI/GasPlumeWisp", 18f, 3f, 0.34f, 0.3f, 0.75f, 0.55f);
        }

        private void CreatePlumeLayer(string layerName, string textureResource, float rate,
            float lifetime, float size, float speedMin, float speedMax, float noiseStrength)
        {
            Texture2D texture = Resources.Load<Texture2D>(textureResource);
            if (texture == null)
            {
                Debug.LogWarning("[GasSprayer] Missing baked plume texture: " + textureResource);
                return;
            }

            Material material = GetOrCreateGasMaterial(texture);
            if (material == null)
            {
                Debug.LogWarning("[GasSprayer] No compatible particle shader for plume texture: " + textureResource);
                return;
            }

            var layerObject = new GameObject(layerName);
            layerObject.transform.SetParent(transform, false);
            layerObject.transform.localPosition = new Vector3(0f, 0.3f, 0.25f);
            layerObject.transform.localRotation = Quaternion.identity;

            ParticleSystem particles = layerObject.AddComponent<ParticleSystem>();
            // AddComponent creates/starts the default particle system before our configuration.
            // Fully stop and clear it before changing duration to avoid Unity's runtime warning.
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = layerName.Contains("Puff") ? 96 : layerName.Contains("Soft") ? 192 : 72;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime, lifetime + 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.72f, size * 1.28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = layerName.Contains("Wisp") ? -0.035f : 0.025f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = layerName.Contains("Wisp") ? 17f : 23f;
            shape.radius = layerName.Contains("Wisp") ? 0.055f : 0.09f;
            shape.radiusThickness = 0.15f;

            // startSpeed is sampled along the cone at birth. World simulation keeps
            // that initial direction; a velocity-over-lifetime module here would either
            // rotate with the emitter (Local) or drift along a fixed global axis (World).
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = false;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = 0.42f;
            noise.octaveCount = 2;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.42f, 0.12f),
                    new GradientAlphaKey(0.27f, 0.58f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.58f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.5f)));

            ParticleSystemRenderer renderer = layerObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.55f;
            renderer.sharedMaterial = material;

            _plumeSystems.Add(particles);
            _plumeRenderers.Add(renderer);
            _plumeMaterials.Add(material);
            _plumeTextures.Add(texture);
            RetainGasMaterial(texture);
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static Material GetOrCreateGasMaterial(Texture2D texture)
        {
            if (texture == null)
                return null;
            if (SharedGasMaterials.TryGetValue(texture, out Material cached) && cached != null)
                return cached;
            if (SharedGasMaterials.ContainsKey(texture))
                RemoveGasMaterial(texture);

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Simple Lit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            var material = new Material(shader) { name = "GasSprayer_Pooled_" + texture.name };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);

            // URP Particles Unlit defaults to opaque even though these plume masks
            // rely on texture and particle alpha. Set the complete transparent state
            // on the runtime material so it renders correctly outside the Editor.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_SrcBlendAlpha"))
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_DstBlendAlpha"))
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            SharedGasMaterials[texture] = material;
            return material;
        }

        private void EmitPlume()
        {
            PotionType type = ClassifyPotion(_controller.LoadedPotionId);
            Color tint = GetFogColor(type);
            if (tint.a <= 0f)
            {
                StopPlumeEmission();
                return;
            }

            float radius = Mathf.Clamp(_controller.GetCurrentSprayerData().sprayRange, 0.1f, 8f);
            float height = Mathf.Clamp(radius * 0.75f, 0.1f, 6f);
            const float density = 0.78f;
            AeroGasSprayBridge.Publish(new AeroGasSprayPayload(transform, radius, height, density, tint));

            if (_plumeSystems.Count == 0)
                return;

            for (int i = 0; i < _plumeSystems.Count; i++)
            {
                ParticleSystem particles = _plumeSystems[i];
                if (particles == null) continue;
                ParticleSystem.MainModule main = particles.main;
                main.startColor = tint;
                if (!particles.isPlaying)
                    particles.Play(false);
            }
        }

        private void StopPlumeEmission()
        {
            AeroGasSprayBridge.Stop();
            for (int i = 0; i < _plumeSystems.Count; i++)
            {
                if (_plumeSystems[i] != null && _plumeSystems[i].isEmitting)
                    _plumeSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void StopSprayEffect()
        {
            StopPlumeEmission();
        }

        private void OnDestroy()
        {
            AeroGasSprayBridge.Stop();
            if (_controller != null)
            {
                _controller.OnSprayingChanged -= HandleSprayingChanged;
                _controller.OnPotionDoseDepleted -= HandlePotionDoseDepleted;
                _controller.OnGPressRequested -= HandleGPressRequested;
            }
            StopSprayEffect();
            for (int i = 0; i < _plumeSystems.Count; i++)
                if (_plumeSystems[i] != null) Destroy(_plumeSystems[i].gameObject);
            _plumeSystems.Clear();
            _plumeRenderers.Clear();
            for (int i = 0; i < _plumeTextures.Count; i++)
                ReleaseGasMaterial(_plumeTextures[i]);
            _plumeTextures.Clear();
            _plumeMaterials.Clear();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// 물약 타입 분류 — 아이템 ID(PotionType 접두사) 기반
        /// </summary>
        public static PotionType ClassifyPotion(string potionId)
        {
            if (string.IsNullOrEmpty(potionId))
                return PotionType.None;

            // 접두사 기반 분류
            if (potionId.StartsWith("독_") || potionId.StartsWith("Poison_") || potionId.StartsWith("Poison"))
                return PotionType.Poison;
            if (potionId.StartsWith("마약_") || potionId.StartsWith("Mental_") || potionId.StartsWith("Mental"))
                return PotionType.Mental;
            if (potionId.StartsWith("치료_") || potionId.StartsWith("Heal_") || potionId.StartsWith("Heal")
                || potionId == "HP포션" || potionId.Contains("Potion"))
                return PotionType.Heal;
            if (potionId.StartsWith("강화_") || potionId.StartsWith("Buff_") || potionId.StartsWith("Buff"))
                return PotionType.Buff;

            // 기본값: 아이템에 'Potion' 또는 '물약' 포함 시 Heal로 분류
            if (potionId.Contains("Potion") || potionId.Contains("물약"))
                return PotionType.Heal;

            return PotionType.None;
        }

        /// <summary>
        /// 물약 타입에 따른 안개 색상 반환
        /// </summary>
        private Color GetFogColor(PotionType type)
        {
            return type switch
            {
                PotionType.Poison => _poisonFogColor,
                PotionType.Mental => _mentalFogColor,
                PotionType.Heal   => _healFogColor,
                PotionType.Buff   => _buffFogColor,
                _                 => Color.clear
            };
        }


        /// <summary>
        /// 분사 중 효과 처리 비활성화/재시작 시 호출
        /// </summary>
        private void OnDisable()
        {
            if (_controller != null) _controller.SetGInputHeld(false);
            AeroGasSprayBridge.Stop();
            StopSprayEffect();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _controller != null)
                _controller.SetGInputHeld(false);
        }

    }

    /// <summary>
    /// 물약 속성 타입 열거형
    /// </summary>
    public enum PotionType
    {
        None,
        Poison,   // 공격성(독) — 붉은 안개, 적 지속 데미지
        Mental,   // 정신성(마약) — 보라색 안개, 적 환각/혼란
        Heal,     // 회복성(치료) — 초록색 안개, 아군 체력 회복
        Buff      // 물리성(강화) — 파란색 안개, 아군 버프
    }
}
