using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// G3-12: FootstepSoundController — 발소리 처리.
    /// 표면 종류 감지(Raycast), 걷기/달리기/대쉬에 따라 간격 조절.
    /// SoundEffectManager.Instance.PlaySFX(SFXType.Footstep) 사용.
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

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _playerMovement = FindObjectOfType<PlayerMovement>();
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
                SoundEffectManager.Instance?.PlaySFX(SoundEffectManager.SFXType.Footstep);
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
    /// G3-12: UISoundIntegrator — OnGUI 기반 UI 사운드 통합.
    /// UISoundManager와 함께 동작하는 보조 컴포넌트.
    /// 기존 UISoundManager를 수정하지 않고 UI 사운드를 보강합니다.
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

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            DetectUIClick();
            DetectPanelTransition();
        }

        /// <summary>
        /// OnGUI에서 마우스 클릭을 감지하여 버튼 클릭 사운드를 재생합니다.
        /// </summary>
        private void DetectUIClick()
        {
            if (Event.current == null) return;

            // 마우스 왼쪽 버튼이 UI 영역에서 떼어졌을 때
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                // GUI 컨트롤이 활성화된 상태에서만 클릭으로 간주
                if (GUIUtility.hotControl != 0)
                {
                    PlayClick();
                }
            }
        }

        /// <summary>
        /// OnGUI 레이아웃 이벤트를 통해 UI 패널 열림/닫힘을 감지합니다.
        /// </summary>
        private void DetectPanelTransition()
        {
            if (Event.current == null) return;
            if (Event.current.type != EventType.Layout) return;

            // Layout 단계에서는 GUI 컨트롤 수가 변경될 때 패널 상태 변화로 간주
            // 실제 패널 감지는 외부에서 Setter를 통해 명시적으로 호출하는 것이 정확함
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
        private MonoBehaviour _biomeComponent;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Biome 관련 컴포넌트 탐색
            _biomeComponent = FindBiomeComponent();
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
        /// 씬에서 Biome 관련 컴포넌트를 찾습니다.
        /// </summary>
        private MonoBehaviour FindBiomeComponent()
        {
            // "Biome"이 이름에 포함된 컴포넌트 검색
            var allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            foreach (var mb in allBehaviours)
            {
                if (mb != null && mb.GetType().Name.Contains("Biome"))
                {
                    return mb;
                }
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
        /// 1. Biome 컴포넌트가 있으면 해당 정보 사용
        /// 2. 없으면 씬 이름 키워드로 판단
        /// </summary>
        private string GetCurrentBiome()
        {
            if (_biomeComponent != null)
            {
                // BiomeComponent에 biomeName 같은 public 필드/속성이 있을 수 있음
                var type = _biomeComponent.GetType();
                var biomeProp = type.GetProperty("CurrentBiome")
                              ?? type.GetProperty("BiomeName")
                              ?? type.GetProperty("biomeName");
                if (biomeProp != null)
                {
                    string value = biomeProp.GetValue(_biomeComponent) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }

                var biomeField = type.GetField("currentBiome")
                             ?? type.GetField("biomeName")
                             ?? type.GetField("_currentBiome");
                if (biomeField != null)
                {
                    string value = biomeField.GetValue(_biomeComponent) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
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
}