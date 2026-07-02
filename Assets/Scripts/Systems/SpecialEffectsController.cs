using System.Collections;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Utils;
using UnityEngine;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 41-2: 셰이더 이펙트 시스템 (코드 기반, Shader Graph 불필요).
    /// 보석 반짝임, 독안개, 무기 발광, 선택 테두리 효과를 관리합니다.
    /// </summary>
    public class SpecialEffectsController : MonoBehaviour
    {
        // ================================================================
        // Singleton
        // ================================================================

        private static SpecialEffectsController _instance;
        private static bool _instanceQuitting;

        /// <summary>싱글톤 인스턴스</summary>
        public static SpecialEffectsController Instance
        {
            get
            {
                if (_instanceQuitting) return null;
                if (_instance == null)
                {
                    var go = new GameObject("SpecialEffectsController");
                    _instance = go.AddComponent<SpecialEffectsController>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ================================================================
        // Serialized Fields
        // ================================================================

        [Header("Gem Glow 설정")]
        [SerializeField, Tooltip("반짝임 속도")] private float _gemPulseSpeed = 2f;
        [SerializeField, Tooltip("반짝임 진폭")] private float _gemPulseAmplitude = 0.5f;
        [SerializeField, Tooltip("빌보드 회전 속도")] private float _gemBillboardRotateSpeed = 30f;

        [Header("Poison Fog 설정")]
        [SerializeField, Tooltip("안개 지속 시간 (초)")] private float _fogDuration = 4f;
        [SerializeField, Tooltip("안개 시작 크기")] private float _fogStartSize = 1f;
        [SerializeField, Tooltip("안개 종료 크기")] private float _fogEndSize = 5f;
        [SerializeField, Tooltip("안개 파티클 수")] private int _fogParticleCount = 30;

        [Header("Weapon Glow 설정")]
        [SerializeField, Tooltip("무기 발광 펄스 속도")] private float _weaponGlowPulseSpeed = 1.5f;

        [Header("Selection Outline 설정")]
        [SerializeField, Tooltip("선택 링 반지름")] private float _outlineRadius = 0.6f;
        [SerializeField, Tooltip("선택 링 높이")] private float _outlineHeight = 0.05f;
        [SerializeField, Tooltip("선택 링 알파 최소")] private float _outlineAlphaMin = 0.3f;
        [SerializeField, Tooltip("선택 링 알파 최대")] private float _outlineAlphaMax = 0.7f;
        [SerializeField, Tooltip("선택 링 펄스 속도")] private float _outlinePulseSpeed = 2f;

        // ================================================================
        // Private State
        // ================================================================

        // Gem Glow: 추적 대상
        private readonly Dictionary<GameObject, GemGlowData> _activeGemGlows = new Dictionary<GameObject, GemGlowData>();

        // Poison Fog: 현재 활성화된 독안개 리스트
        private readonly List<PoisonFogInstance> _activeFogs = new List<PoisonFogInstance>();

        // Weapon Glow: 추적 대상
        private readonly Dictionary<GameObject, WeaponGlowData> _activeWeaponGlows = new Dictionary<GameObject, WeaponGlowData>();

        // Selection Outline: 추적 대상
        private readonly Dictionary<GuardPlaceholder, SelectionOutlineData> _activeOutlines = new Dictionary<GuardPlaceholder, SelectionOutlineData>();

        // 캐시된 플레이어 Transform (Poison Fog 위치용)
        private Transform _playerTransform;

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
            // 플레이어 참조 캐싱
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                _playerTransform = playerGO.transform;

            // GasSprayerController에 훅 연결 (분사 시 독안개)
            ConnectToGasSprayer();
        }

        private void Update()
        {
            // Gem Glow: 펄싱 업데이트
            UpdateGemGlows();

            // Poison Fog: 수명 및 확장 업데이트
            UpdatePoisonFogs();

            // Weapon Glow: 펄싱 업데이트
            UpdateWeaponGlows();

            // Selection Outline: 위치 추적 및 알파 펄싱
            UpdateSelectionOutlines();
        }

        // ================================================================
        // 1. 💎 Gem Glow (보석 반짝임)
        // ================================================================

        /// <summary>
        /// 보석 상자에 반짝임 효과를 추가합니다.
        /// Point Light 펄싱 + 회전하는 Quad 빌보드 + 발광 머티리얼.
        /// </summary>
        /// <param name="gemChest">대상 GemChest</param>
        /// <param name="gemColor">보석 색상</param>
        public void AddGemGlow(GemChest gemChest, Color gemColor)
        {
            if (gemChest == null)
            {
                Debug.LogWarning("[SpecialEffectsController] GemChest가 null입니다.");
                return;
            }

            GameObject target = gemChest.gameObject;

            // 중복 추가 방지
            if (_activeGemGlows.ContainsKey(target))
            {
                Debug.Log("[SpecialEffectsController] 이미 Gem Glow가 적용되어 있습니다.");
                return;
            }

            var data = new GemGlowData();

            // 1. Point Light 추가 또는 기존 Light 활용
            Light existingLight = gemChest.GetComponentInChildren<Light>();
            if (existingLight != null && existingLight.type == LightType.Point)
            {
                data.light = existingLight;
            }
            else
            {
                var lightGO = new GameObject("GemGlowLight");
                lightGO.transform.SetParent(target.transform);
                lightGO.transform.localPosition = Vector3.up * 0.5f;
                data.light = lightGO.AddComponent<Light>();
            }

            data.light.type = LightType.Point;
            data.light.range = 3f;
            data.light.intensity = 1f;
            data.light.color = gemColor;
            data.baseIntensity = 1f;

            // 2. 회전하는 빌보드 (Quad + 발광 Material)
            var billboardGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            billboardGO.name = "GemGlowBillboard";
            billboardGO.transform.SetParent(target.transform);
            billboardGO.transform.localPosition = Vector3.up * 0.8f;
            billboardGO.transform.localScale = Vector3.one * 0.3f;

            // Quad의 MeshRenderer 제거 후 새로 생성 (기존 Primitive 메테리얼 대체)
            var mr = billboardGO.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // 발광 Material 생성
                Material emissionMat = CreateEmissionMaterial(gemColor, "GemGlowMat");
                if (emissionMat != null)
                    mr.material = emissionMat;
            }

            data.billboard = billboardGO.transform;
            data.rotateSpeed = _gemBillboardRotateSpeed;

            _activeGemGlows[target] = data;

            Debug.Log($"[SpecialEffectsController] Gem Glow 추가됨: {target.name}");
        }

        /// <summary>
        /// 보석 반짝임 효과를 제거합니다.
        /// </summary>
        public void RemoveGemGlow(GemChest gemChest)
        {
            if (gemChest == null) return;

            GameObject target = gemChest.gameObject;
            if (!_activeGemGlows.TryGetValue(target, out var data)) return;

            // Light 제거 (기존 GemChest의 Light는 남김)
            if (data.light != null && data.light.gameObject.name == "GemGlowLight")
                Destroy(data.light.gameObject);

            // 빌보드 제거
            if (data.billboard != null)
                Destroy(data.billboard.gameObject);

            _activeGemGlows.Remove(target);
        }

        /// <summary>
        /// Gem Glow의 펄싱 애니메이션을 매 프레임 업데이트합니다.
        /// </summary>
        private void UpdateGemGlows()
        {
            if (_activeGemGlows.Count == 0) return;

            var keysToRemove = new List<GameObject>();

            foreach (var kvp in _activeGemGlows)
            {
                GameObject target = kvp.Key;
                GemGlowData data = kvp.Value;

                // 대상이 파괴되었는지 확인
                if (target == null || data.light == null)
                {
                    keysToRemove.Add(target);
                    continue;
                }

                // Light 강도 펄싱
                if (data.light != null)
                {
                    float pulse = Mathf.Sin(Time.time * _gemPulseSpeed) * _gemPulseAmplitude;
                    data.light.intensity = data.baseIntensity + pulse;
                }

                // 빌보드 회전
                if (data.billboard != null)
                {
                    data.billboard.Rotate(Vector3.up, _gemBillboardRotateSpeed * Time.deltaTime);
                }
            }

            // 제거
            foreach (var key in keysToRemove)
                _activeGemGlows.Remove(key);
        }

        // ================================================================
        // Gem Glow 데이터 구조
        // ================================================================

        private class GemGlowData
        {
            public Light light;
            public Transform billboard;
            public float baseIntensity = 1f;
            public float rotateSpeed = 30f;
        }

        // ================================================================
        // 2. 🌫️ Poison Fog (독안개)
        // ================================================================

        /// <summary>
        /// 약병 타입별 독안개 색상을 반환합니다.
        /// red=poison, purple=drug, green=heal, blue=physical
        /// </summary>
        private static Color GetFogColorByPotionType(string potionType)
        {
            if (string.IsNullOrEmpty(potionType)) return new Color(0.5f, 0f, 0f, 0.3f); // 기본: 독

            string lower = potionType.ToLowerInvariant();

            if (lower.Contains("poison") || lower.Contains("독") || lower.Contains("venom"))
                return new Color(1f, 0f, 0f, 0.3f); // red = poison
            if (lower.Contains("drug") || lower.Contains("마약") || lower.Contains("약물"))
                return new Color(0.6f, 0f, 0.8f, 0.3f); // purple = drug
            if (lower.Contains("heal") || lower.Contains("회복") || lower.Contains("치유") || lower.Contains("health"))
                return new Color(0f, 1f, 0f, 0.3f); // green = heal
            if (lower.Contains("physical") || lower.Contains("물리") || lower.Contains("강화") || lower.Contains("phys"))
                return new Color(0f, 0.5f, 1f, 0.3f); // blue = physical

            return new Color(1f, 0f, 0f, 0.3f); // 기본: red (독)
        }

        /// <summary>
        /// 지정된 위치에 독안개 파티클을 생성합니다.
        /// </summary>
        /// <param name="position">생성 위치</param>
        /// <param name="potionType">물약 타입 (poison/drug/heal/physical)</param>
        public void SpawnPoisonFog(Vector3 position, string potionType)
        {
            Color fogColor = GetFogColorByPotionType(potionType);

            var fogGO = new GameObject("PoisonFog");
            fogGO.transform.position = position;

            var ps = fogGO.AddComponent<ParticleSystem>();

            // Main 모듈
            var main = ps.main;
            main.loop = false;
            main.startLifetime = _fogDuration;
            main.startSpeed = 0.3f;
            main.startSize = _fogStartSize;
            main.startColor = fogColor;
            main.gravityModifier = 0f;
            main.maxParticles = _fogParticleCount;
            main.playOnAwake = false;

            // Emission
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, _fogParticleCount)
            });

            // Shape: Sphere
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            // Size over Lifetime: 1 → 5 (확장)
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 5f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color over Lifetime: 페이드 아웃
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaGradient = new Gradient();
            alphaGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(fogColor.a, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = alphaGradient;

            // Renderer: Additive
            var renderer = fogGO.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;

                // Additive Material 생성
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Lit");

                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.name = "PoisonFogMat";
                    // 부드러운 원형 텍스처
                    mat.mainTexture = CreateSoftCircleTexture(16, 16, fogColor);

                    if (shader.name.Contains("Unlit"))
                    {
                        // Additive 블렌딩
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive
                        mat.SetInt("_ZWrite", 0);
                        mat.renderQueue = 3000;
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    }
                    else
                    {
                        mat.SetFloat("_Surface", 1f);
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        mat.SetInt("_ZWrite", 0);
                        mat.renderQueue = 3000;
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    }

                    renderer.material = mat;
                }

                renderer.sortingFudge = -1f; // 다른 파티클 앞에 렌더링
            }

            // 인스턴스 추적
            var instance = new PoisonFogInstance
            {
                gameObject = fogGO,
                particleSystem = ps,
                startTime = Time.time,
                duration = _fogDuration,
                fogColor = fogColor
            };
            _activeFogs.Add(instance);

            // 재생
            ps.Play();

            // 자동 제거 코루틴
            StartCoroutine(DestroyPoisonFogDelayed(fogGO, _fogDuration + 1f));
        }

        /// <summary>
        /// 독안개 오브젝트를 지연 제거합니다.
        /// </summary>
        private IEnumerator DestroyPoisonFogDelayed(GameObject fogGO, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (fogGO != null)
            {
                // 리스트에서 제거
                _activeFogs.RemoveAll(f => f.gameObject == fogGO);
                Destroy(fogGO);
            }
        }

        /// <summary>
        /// 매 프레임 Poison Fog 상태를 업데이트합니다.
        /// </summary>
        private void UpdatePoisonFogs()
        {
            float now = Time.time;
            _activeFogs.RemoveAll(f =>
            {
                if (f.gameObject == null || f.particleSystem == null) return true;

                float elapsed = now - f.startTime;
                if (elapsed >= f.duration)
                    return false; // 코루틴에서 제거

                // 진행도 기반 업데이트
                float t = elapsed / f.duration;

                // 크기: 1 → 5
                if (f.particleSystem != null)
                {
                    var main = f.particleSystem.main;
                    float currentSize = Mathf.Lerp(_fogStartSize, _fogEndSize, t);
                    main.startSize = currentSize;
                }

                return false;
            });
        }

        /// <summary>
        /// Poison Fog 인스턴스 데이터
        /// </summary>
        private class PoisonFogInstance
        {
            public GameObject gameObject;
            public ParticleSystem particleSystem;
            public float startTime;
            public float duration;
            public Color fogColor;
        }

        /// <summary>
        /// GasSprayerController의 분사 이벤트에 독안개를 연결합니다.
        /// </summary>
        private void ConnectToGasSprayer()
        {
            // GasSprayerController가 이미 존재하면 연결
            var gasSprayer = FindAnyObjectByType<GasSprayerController>();
            if (gasSprayer != null)
            {
                Debug.Log("[SpecialEffectsController] GasSprayerController와 연결됨.");
                // GasSprayerController의 분사 이벤트는 OnEquipChanged로 확인
                // 실제 분사는 StartSpray/StopSpray에서 처리
            }
        }

        /// <summary>
        /// 가스 분사기 분사 시 독안개를 생성합니다 (외부 호출용).
        /// GasSprayerController.StartSpray()에서 호출됩니다.
        /// </summary>
        public void OnGasSprayStart()
        {
            if (_playerTransform == null)
            {
                ResolvePlayer();
                if (_playerTransform == null) return;
            }

            string potionId = "";
            var gasSprayer = FindAnyObjectByType<GasSprayerController>();
            if (gasSprayer != null)
                potionId = gasSprayer.LoadedPotionId;

            // 플레이어 위치에 독안개 생성
            SpawnPoisonFog(_playerTransform.position + _playerTransform.forward * 1.5f, potionId);
        }

        // ================================================================
        // 3. ⚔️ Weapon Glow (무기 발광)
        // ================================================================

        /// <summary>
        /// 등급에 따른 발광 색상을 반환합니다.
        /// Rare=파랑, Epic=보라, Legendary=주황, Unique=황금
        /// </summary>
        private static Color GetGlowColorByRarity(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Rare => new Color(0.2f, 0.4f, 1f),      // 파랑
                ItemRarity.Epic => new Color(0.7f, 0.2f, 1f),      // 보라
                ItemRarity.Legendary => new Color(1f, 0.5f, 0f),   // 주황
                ItemRarity.Unique => new Color(1f, 0.85f, 0.2f),   // 황금
                _ => Color.white
            };
        }

        /// <summary>
        /// 무기 GameObject에 등급 기반 발광 효과를 추가합니다.
        /// Material의 _EmissionColor를 수정하거나, 폴백으로 Child Light를 생성합니다.
        /// </summary>
        /// <param name="weaponGO">무기 GameObject</param>
        /// <param name="rarity">아이템 등급 (Rare+만 적용)</param>
        public void AddWeaponGlow(GameObject weaponGO, ItemRarity rarity)
        {
            if (weaponGO == null)
            {
                Debug.LogWarning("[SpecialEffectsController] weaponGO가 null입니다.");
                return;
            }

            // Rare 미만은 적용 안 함
            if (rarity < ItemRarity.Rare) return;

            // 중복 방지
            if (_activeWeaponGlows.ContainsKey(weaponGO))
            {
                Debug.Log("[SpecialEffectsController] 이미 Weapon Glow가 적용되어 있습니다.");
                return;
            }

            Color glowColor = GetGlowColorByRarity(rarity);
            var data = new WeaponGlowData
            {
                rarity = rarity,
                glowColor = glowColor,
                baseIntensity = 1f
            };

            // 1. MeshRenderer에서 Material 찾아서 Emission 설정 시도
            var renderers = weaponGO.GetComponentsInChildren<MeshRenderer>();
            bool emissionApplied = false;

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;

                foreach (var mat in renderer.materials)
                {
                    if (mat == null) continue;

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", glowColor * 0.5f);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        emissionApplied = true;
                        data.emissionMaterials.Add(mat);
                    }
                }
            }

            // SkinnedMeshRenderer도 확인
            var skinnedRenderers = weaponGO.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var renderer in skinnedRenderers)
            {
                if (renderer == null) continue;

                foreach (var mat in renderer.materials)
                {
                    if (mat == null) continue;

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", glowColor * 0.5f);
                        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        emissionApplied = true;
                        data.emissionMaterials.Add(mat);
                    }
                }
            }

            // 2. Emission이 안 되는 Material → Child Light 생성
            if (!emissionApplied)
            {
                var lightGO = new GameObject("WeaponGlowLight");
                lightGO.transform.SetParent(weaponGO.transform);
                lightGO.transform.localPosition = Vector3.zero;

                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 2f;
                light.intensity = 0.5f;
                light.color = glowColor;

                data.fallbackLight = light;
                data.fallbackLightGO = lightGO;
            }

            _activeWeaponGlows[weaponGO] = data;

            Debug.Log($"[SpecialEffectsController] Weapon Glow 적용: {weaponGO.name} ({rarity})");
        }

        /// <summary>
        /// 무기 발광 효과를 제거합니다.
        /// </summary>
        public void RemoveWeaponGlow(GameObject weaponGO)
        {
            if (weaponGO == null) return;
            if (!_activeWeaponGlows.TryGetValue(weaponGO, out var data)) return;

            // Emission 복원
            foreach (var mat in data.emissionMaterials)
            {
                if (mat != null)
                {
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                }
            }

            // Fallback Light 제거
            if (data.fallbackLightGO != null)
                Destroy(data.fallbackLightGO);

            _activeWeaponGlows.Remove(weaponGO);
        }

        /// <summary>
        /// 매 프레임 Weapon Glow 펄싱을 업데이트합니다.
        /// </summary>
        private void UpdateWeaponGlows()
        {
            if (_activeWeaponGlows.Count == 0) return;

            var keysToRemove = new List<GameObject>();

            foreach (var kvp in _activeWeaponGlows)
            {
                GameObject target = kvp.Key;
                WeaponGlowData data = kvp.Value;

                if (target == null)
                {
                    keysToRemove.Add(target);
                    continue;
                }

                float pulse = 0.5f + Mathf.Sin(Time.time * _weaponGlowPulseSpeed) * 0.25f;

                // Emission Material 업데이트
                foreach (var mat in data.emissionMaterials)
                {
                    if (mat != null && mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", data.glowColor * pulse);
                    }
                }

                // Fallback Light 업데이트
                if (data.fallbackLight != null)
                {
                    data.fallbackLight.intensity = 0.3f + pulse * 0.5f;
                }
            }

            foreach (var key in keysToRemove)
                _activeWeaponGlows.Remove(key);
        }

        /// <summary>
        /// Weapon Glow 데이터 구조
        /// </summary>
        private class WeaponGlowData
        {
            public ItemRarity rarity;
            public Color glowColor;
            public float baseIntensity = 1f;
            public List<Material> emissionMaterials = new List<Material>();
            public Light fallbackLight;
            public GameObject fallbackLightGO;
        }

        // ================================================================
        // 4. 🎯 Selection Outline (선택 테두리)
        // ================================================================

        /// <summary>
        /// GuardPlaceholder 아래에 선택 표시 링을 생성합니다.
        /// </summary>
        public void AddSelectionOutline(GuardPlaceholder guard)
        {
            if (guard == null)
            {
                Debug.LogWarning("[SpecialEffectsController] Guard가 null입니다.");
                return;
            }

            // 중복 방지
            if (_activeOutlines.ContainsKey(guard))
            {
                Debug.Log("[SpecialEffectsController] 이미 Selection Outline이 있습니다.");
                return;
            }

            // 평평한 원통(Cylinder) 생성
            var ringGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGO.name = "SelectionOutline_" + guard.name;
            ringGO.transform.SetParent(guard.transform);
            ringGO.transform.localPosition = new Vector3(0f, 0.025f, 0f); // 바닥 바로 위
            ringGO.transform.localScale = new Vector3(
                _outlineRadius * 2f / 0.5f, // Cylinder 기본 반지름 0.5
                _outlineHeight / 1f,         // Cylinder 기본 높이 1
                _outlineRadius * 2f / 0.5f
            );

            // Collider 제거
            var collider = ringGO.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            // Material 설정: 반투명 파랑
            var mr = ringGO.GetComponent<MeshRenderer>();
            Material outlineMat = null;

            if (mr != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                if (shader != null)
                {
                    outlineMat = new Material(shader);
                    outlineMat.name = "SelectionOutlineMat";
                    outlineMat.color = new Color(0.2f, 0.4f, 1f, 0.5f); // 파란색 반투명

                    // 투명 설정
                    outlineMat.SetFloat("_Surface", 1f);
                    outlineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    outlineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    outlineMat.SetInt("_ZWrite", 0);
                    outlineMat.renderQueue = 3000;
                    outlineMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    outlineMat.SetOverrideTag("RenderType", "Transparent");

                    mr.material = outlineMat;
                }
            }

            var data = new SelectionOutlineData
            {
                guard = guard,
                ringObject = ringGO,
                ringRenderer = mr,
                outlineMaterial = outlineMat
            };

            _activeOutlines[guard] = data;
        }

        /// <summary>
        /// GuardPlaceholder의 선택 표시 링을 제거합니다.
        /// </summary>
        public void RemoveSelectionOutline(GuardPlaceholder guard)
        {
            if (guard == null) return;
            if (!_activeOutlines.TryGetValue(guard, out var data)) return;

            if (data.ringObject != null)
                Destroy(data.ringObject);

            if (data.outlineMaterial != null)
                Destroy(data.outlineMaterial);

            _activeOutlines.Remove(guard);
        }

        /// <summary>
        /// 매 프레임 Selection Outline 위치 추적 및 알파 펄싱.
        /// </summary>
        private void UpdateSelectionOutlines()
        {
            if (_activeOutlines.Count == 0) return;

            var toRemove = new List<GuardPlaceholder>();

            foreach (var kvp in _activeOutlines)
            {
                GuardPlaceholder guard = kvp.Key;
                SelectionOutlineData data = kvp.Value;

                // Guard가 null이거나 선택 해제되었거나 파괴되었으면 제거
                if (guard == null || !guard.IsSelected || data.ringObject == null)
                {
                    toRemove.Add(guard);
                    continue;
                }

                // 위치 추적 (이미 자식이므로 transform.follow는 자동)
                // 알파 펄싱: 0.3 ~ 0.7
                if (data.outlineMaterial != null)
                {
                    float alpha = _outlineAlphaMin +
                        (Mathf.Sin(Time.time * _outlinePulseSpeed) * 0.5f + 0.5f)
                        * (_outlineAlphaMax - _outlineAlphaMin);

                    Color c = data.outlineMaterial.color;
                    data.outlineMaterial.color = new Color(c.r, c.g, c.b, alpha);
                }
            }

            // 정리
            foreach (var guard in toRemove)
            {
                RemoveSelectionOutline(guard);
            }
        }

        /// <summary>
        /// Selection Outline 데이터 구조
        /// </summary>
        private class SelectionOutlineData
        {
            public GuardPlaceholder guard;
            public GameObject ringObject;
            public MeshRenderer ringRenderer;
            public Material outlineMaterial;
        }

        // ================================================================
        // 유틸리티 메서드
        // ================================================================

        /// <summary>
        /// 발광 Material을 생성합니다. URP/Lit 셰이더 사용.
        /// </summary>
        private Material CreateEmissionMaterial(Color emissionColor, string materialName)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[SpecialEffectsController] URP/Lit 셰이더를 찾을 수 없습니다.");
                return null;
            }

            var mat = new Material(shader);
            mat.name = materialName;
            mat.color = emissionColor;
            mat.SetColor("_EmissionColor", emissionColor * 0.8f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;

            return mat;
        }

        /// <summary>
        /// 소프트 서클 텍스처를 생성합니다.
        /// </summary>
        private Texture2D CreateSoftCircleTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = width * 0.5f;
            float cy = height * 0.5f;
            float maxDist = Mathf.Min(cx, cy);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f) - cx;
                    float dy = (y + 0.5f) - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - (dist / maxDist));
                    alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep

                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 플레이어 Transform을 다시 탐색합니다.
        /// </summary>
        private void ResolvePlayer()
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                _playerTransform = playerGO.transform;
        }
    }
}