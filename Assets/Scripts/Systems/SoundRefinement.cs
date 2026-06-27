using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// G3-12: FootstepSoundController — 발소리 처리.
    /// 표면 종류 감지(Raycast), 걷기/달리기/대쉬에 따라 간격 조절.
    /// SoundEffectManager.Instance.PlaySurfacedSFX 사용 (표면별 사운드).
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class FootstepSoundController : MonoBehaviour
    {
        [Header("발소리 간격 (초)")]
        [SerializeField, Tooltip("걷기 시 발소리 간격")]
        private float _walkInterval = 0.5f;

        [SerializeField, Tooltip("달리기 시 발소리 간격")]
        private float _runInterval = 0.35f;

        [SerializeField, Tooltip("대쉬 시 발소리 간격")]
        private float _dashInterval = 0.25f;

        [Header("표면 감지")]
        [SerializeField, Tooltip("Raycast 최대 거리")]
        private float _raycastDistance = 0.3f;

        [SerializeField, Tooltip("표면 감지용 LayerMask")]
        private LayerMask _surfaceLayerMask = -1; // Everything

        private PlayerMovement _playerMovement;
        private float _footstepTimer;
        private string _currentSurfaceTag = "step_grass";

        // 중복 인스턴스 방지용 플래그
        private static FootstepSoundController _existingInstance;

        private void Awake()
        {
            if (_existingInstance != null && _existingInstance != this)
            {
                Debug.LogWarning("[FootstepSoundController] 중복 인스턴스 제거");
                Destroy(gameObject);
                return;
            }
            _existingInstance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _playerMovement = GetComponent<PlayerMovement>();
            if (_playerMovement == null)
            {
                Debug.LogWarning("[FootstepSoundController] PlayerMovement를 찾을 수 없습니다.");
            }
        }

        private void Update()
        {
            HandleFootstep();
        }

        /// <summary>
        /// 매 프레임 발소리 처리를 수행합니다.
        /// </summary>
        private void HandleFootstep()
        {
            if (_playerMovement == null) return;
            if (!_playerMovement.IsGrounded) return;
            if (_playerMovement.Velocity.magnitude <= 0.5f)
            {
                _footstepTimer = 0f;
                return;
            }

            // 현재 표면 감지
            DetectSurface();

            // 속도에 따른 간격 선택
            float interval = GetFootstepInterval();

            _footstepTimer += Time.deltaTime;
            if (_footstepTimer >= interval)
            {
                _footstepTimer = 0f;
                // 표면별 발소리 재생 — _currentSurfaceTag를 variant로 전달
                SoundEffectManager.Instance?.PlaySurfacedSFX(SoundEffectManager.SFXType.Footstep, _currentSurfaceTag);
            }
        }

        /// <summary>
        /// 발 아래 표면 종류를 Raycast로 감지합니다.
        /// </summary>
        private void DetectSurface()
        {
            RaycastHit hit;
            Vector3 origin = transform.position + Vector3.up * 0.1f;

            if (Physics.Raycast(origin, Vector3.down, out hit, _raycastDistance, _surfaceLayerMask))
            {
                _currentSurfaceTag = GetSurfaceVariant(hit.collider.tag);
            }
            else
            {
                _currentSurfaceTag = "step_grass";
            }
        }

        /// <summary>
        /// 태그에 따른 표면 변형 이름을 반환합니다.
        /// </summary>
        private static string GetSurfaceVariant(string tag)
        {
            return tag switch
            {
                "Terrain" or "Ground" => "step_grass",
                "Stone" => "step_stone",
                "Wood" => "step_wood",
                "Water" => "step_water",
                _ => "step_grass"
            };
        }

        /// <summary>
        /// 현재 이동 상태에 따른 발소리 간격을 반환합니다.
        /// </summary>
        private float GetFootstepInterval()
        {
            if (_playerMovement.IsDashing) return _dashInterval;
            if (_playerMovement.IsSprinting) return _runInterval;
            return _walkInterval;
        }

        /// <summary>
        /// 표면 변형을 수동으로 설정합니다 (외부 호출용).
        /// </summary>
        /// <param name="surfaceTag">표면 태그 (step_grass, step_stone 등)</param>
        public void SetSurfaceVariant(string surfaceTag)
        {
            _currentSurfaceTag = surfaceTag;
        }

        /// <summary>현재 감지된 표면 변형</summary>
        public string CurrentSurfaceTag => _currentSurfaceTag;

        /// <summary>걷기 간격</summary>
        public float WalkInterval => _walkInterval;

        /// <summary>달리기 간격</summary>
        public float RunInterval => _runInterval;

        /// <summary>대쉬 간격</summary>
        public float DashInterval => _dashInterval;
    }

    /// <summary>
    /// G3-12: UISoundIntegrator — UI 사운드 통합.
    /// UISoundManager와 함께 동작하는 보조 컴포넌트.
    /// OnGUI 대신 Update + Input 시스템 사용 (GC 최적화).
    /// </summary>
    public class UISoundIntegrator : MonoBehaviour
    {
        private enum PanelState
        {
            Unknown,
            Open,
            Closed
        }

        private PanelState _lastPanelState = PanelState.Unknown;
        private bool _previousMouseUp;
        private static UISoundIntegrator _existingInstance;

        private void Awake()
        {
            if (_existingInstance != null && _existingInstance != this)
            {
                Debug.LogWarning("[UISoundIntegrator] 중복 인스턴스 제거");
                Destroy(gameObject);
                return;
            }
            _existingInstance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            DetectUIClick();
        }

        /// <summary>
        /// Update에서 마우스 클릭을 감지하여 버튼 클릭 사운드를 재생합니다.
        /// OnGUI 대신 사용하여 GC 할당을 방지합니다.
        /// </summary>
        private void DetectUIClick()
        {
            bool currentMouseUp = Input.GetMouseButtonUp(0);

            // 마우스 버튼이 막 떼어진 시점 (이전 프레임과 비교)
            if (currentMouseUp && !_previousMouseUp)
            {
                // UI 컨트롤이 활성화된 상태에서만 클릭으로 간주
                if (GUIUtility.hotControl != 0)
                {
                    PlayClick();
                }
            }

            _previousMouseUp = currentMouseUp;
        }

        /// <summary>
        /// 버튼 클릭 사운드를 재생합니다.
        /// </summary>
        public void PlayClick()
        {
            UISoundManager.Instance?.PlayUISound(UISoundManager.UISFXType.UIClick);
        }

        /// <summary>
        /// UI 패널 열림 사운드를 재생합니다.
        /// </summary>
        public void PlayOpen()
        {
            UISoundManager.Instance?.PlayUISound(UISoundManager.UISFXType.UIOpen);
            _lastPanelState = PanelState.Open;
        }

        /// <summary>
        /// UI 패널 닫힘 사운드를 재생합니다.
        /// </summary>
        public void PlayClose()
        {
            UISoundManager.Instance?.PlayUISound(UISoundManager.UISFXType.UIClose);
            _lastPanelState = PanelState.Closed;
        }
    }

    /// <summary>
    /// G3-12: BiomeAmbientController — 바이옴 기반 앰비언트 사운드 관리.
    /// IBiomeProvider 인터페이스를 통해 바이옴 정보를 얻습니다 (리플렉션 대체).
    /// 현재 바이옴을 감지하여 SoundManagerEnhanced.PlayAmbient로 앰비언트 자동 전환.
    /// </summary>
    public class BiomeAmbientController : MonoBehaviour
    {
        [Header("앰비언트 설정")]
        [SerializeField, Tooltip("바이옴 체크 간격 (초)")]
        private float _checkInterval = 2.0f;

        private string _lastBiome;
        private string _currentAmbientName;
        private float _checkTimer;
        private IBiomeProvider _biomeProvider;
        private static BiomeAmbientController _existingInstance;

        private void Awake()
        {
            if (_existingInstance != null && _existingInstance != this)
            {
                Debug.LogWarning("[BiomeAmbientController] 중복 인스턴스 제거");
                Destroy(gameObject);
                return;
            }
            _existingInstance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // IBiomeProvider 인터페이스 구현체 탐색 (리플렉션 대체)
            _biomeProvider = FindBiomeProvider();
            ForceUpdateAmbient();
        }

        private void Update()
        {
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= _checkInterval)
            {
                _checkTimer = 0f;
                CheckBiomeChange();
            }
        }

        /// <summary>
        /// 씬에서 IBiomeProvider 인터페이스를 구현한 컴포넌트를 찾습니다.
        /// FindFirstObjectByType<IBiomeProvider>로 GC 할당 최소화.
        /// </summary>
        private IBiomeProvider FindBiomeProvider()
        {
            // Find any MonoBehaviour that implements IBiomeProvider
            var monoBehaviors = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None, FindObjectsInactive.Include);
            foreach (var mb in monoBehaviors)
            {
                if (mb is IBiomeProvider provider)
                    return provider;
            }
            return null;
        }

        /// <summary>
        /// 현재 바이옴이 변경되었는지 확인하고 필요시 앰비언트를 전환합니다.
        /// </summary>
        private void CheckBiomeChange()
        {
            string currentBiome = GetCurrentBiome();
            if (currentBiome != _lastBiome)
            {
                _lastBiome = currentBiome;
                PlayAmbientForBiome(currentBiome);
            }
        }

        /// <summary>
        /// 현재 바이옴을 결정합니다.
        /// 1. IBiomeProvider 인터페이스 구현체가 있으면 해당 정보 사용
        /// 2. 없으면 씬 이름 키워드로 판단
        /// </summary>
        private string GetCurrentBiome()
        {
            if (_biomeProvider != null)
            {
                string biomeName = _biomeProvider.GetCurrentBiome();
                if (!string.IsNullOrEmpty(biomeName))
                    return biomeName;
            }

            // 폴백: 씬 이름 키워드 분석
            return DetectBiomeFromSceneName();
        }

        /// <summary>
        /// 씬 이름에서 키워드를 추출하여 바이옴을 추정합니다.
        /// </summary>
        private string DetectBiomeFromSceneName()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            sceneName = sceneName.ToLowerInvariant();

            if (sceneName.Contains("forest")) return "Forest";
            if (sceneName.Contains("desert")) return "Desert";
            if (sceneName.Contains("water") || sceneName.Contains("ocean") || sceneName.Contains("lake") || sceneName.Contains("river")) return "Water";
            if (sceneName.Contains("mountain") || sceneName.Contains("hill")) return "Mountain";
            if (sceneName.Contains("swamp") || sceneName.Contains("marsh")) return "Swamp";
            if (sceneName.Contains("town") || sceneName.Contains("village") || sceneName.Contains("city")) return "Town";

            return "Default";
        }

        /// <summary>
        /// 바이옴 이름에 해당하는 앰비언트를 재생합니다.
        /// </summary>
        /// <param name="biome">바이옴 이름</param>
        private void PlayAmbientForBiome(string biome)
        {
            string ambientName = GetAmbientName(biome);
            if (ambientName == _currentAmbientName) return;

            _currentAmbientName = ambientName;
            float volume = GetAmbientVolume(biome);
            SoundManagerEnhanced.Instance?.PlayAmbient(ambientName, volume);
        }

        /// <summary>
        /// 바이옴 이름에 매핑된 앰비언트 리소스 이름을 반환합니다.
        /// </summary>
        private static string GetAmbientName(string biome)
        {
            return biome switch
            {
                "Forest" => "ambient_forest",
                "Desert" => "ambient_desert",
                "Water" => "ambient_water",
                "Mountain" => "ambient_mountain",
                "Swamp" => "ambient_swamp",
                "Town" => "ambient_town",
                _ => "ambient_default"
            };
        }

        /// <summary>
        /// 바이옴별 앰비언트 재생 볼륨을 반환합니다.
        /// </summary>
        private static float GetAmbientVolume(string biome)
        {
            return biome switch
            {
                "Forest" => 0.4f,
                "Desert" => 0.3f,
                "Water" => 0.5f,
                "Mountain" => 0.35f,
                "Swamp" => 0.45f,
                "Town" => 0.3f,
                _ => 0.4f
            };
        }

        /// <summary>
        /// 앰비언트를 강제로 갱신합니다 (외부 호출용).
        /// </summary>
        public void ForceUpdateAmbient()
        {
            _lastBiome = null;
            _checkTimer = _checkInterval; // 즉시 체크
        }

        /// <summary>
        /// 바이옴을 수동으로 설정합니다 (외부 호출용).
        /// </summary>
        /// <param name="biomeName">바이옴 이름</param>
        public void SetBiome(string biomeName)
        {
            if (string.IsNullOrEmpty(biomeName)) return;
            _lastBiome = biomeName;
            PlayAmbientForBiome(biomeName);
        }

        /// <summary>마지막으로 감지된 바이옴</summary>
        public string LastBiome => _lastBiome;

        /// <summary>현재 재생 중인 앰비언트 이름</summary>
        public string CurrentAmbientName => _currentAmbientName;
    }

    /// <summary>
    /// 바이옴 정보 제공자 인터페이스.
    /// BiomeAmbientController가 리플렉션 없이 바이옴 정보를 얻을 수 있게 합니다.
    /// </summary>
    public interface IBiomeProvider
    {
        /// <summary>현재 바이옴 이름을 반환합니다.</summary>
        string GetCurrentBiome();
    }
}