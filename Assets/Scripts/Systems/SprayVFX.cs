using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C8-33: 분사 VFX — 가스 분사기 속성별 안개 효과
    /// GasSprayerController와 동일 GameObject에 배치하여 분사 시 파티클 효과를 재생합니다.
    /// </summary>
    [RequireComponent(typeof(GasSprayerController))]
    public class SprayVFX : MonoBehaviour
    {
        private GasSprayerController _controller;
        private ParticleSystem _particleSystem;
        private ParticleSystem.MainModule _mainModule;

        [Header("VFX 설정")]
        [SerializeField] private GameObject _particlePrefab;
        [SerializeField] private float _particleLifetime = 2.0f;
        [SerializeField] private float _particleSpeed = 1.5f;
        [SerializeField] private float _particleSize = 0.5f;
        [SerializeField] private int _particleEmissionRate = 30;

        [Header("속성별 색상")]
        [SerializeField] private Color _colorHeal = new Color(1f, 0.2f, 0.2f, 0.6f);       // herb_red: 빨강
        [SerializeField] private Color _colorPoison = new Color(0.6f, 0.1f, 0.8f, 0.6f);   // herb_purple: 보라
        [SerializeField] private Color _colorHallucination = new Color(1f, 0.9f, 0.1f, 0.6f); // herb_yellow: 노랑
        [SerializeField] private Color _colorDetox = new Color(0.8f, 0.8f, 0.9f, 0.6f);    // herb_silver: 은색
        [SerializeField] private Color _colorRegen = new Color(0.2f, 0.9f, 0.3f, 0.6f);    // herb_green: 초록
        [SerializeField] private Color _colorDefault = new Color(0.9f, 0.9f, 0.9f, 0.5f);  // 기본: 흰색 연기

        private ParticleSystem.MinMaxGradient _currentColor;
        private bool _wasSpraying;

        private void Awake()
        {
            _controller = GetComponent<GasSprayerController>();
            if (_controller == null)
            {
                Debug.LogError("[SprayVFX] GasSprayerController를 찾을 수 없습니다!");
                enabled = false;
                return;
            }

            InitializeParticleSystem();
        }

        private void InitializeParticleSystem()
        {
            // 기존 파티클 시스템 확인
            _particleSystem = GetComponent<ParticleSystem>();
            if (_particleSystem == null)
            {
                // 프리팹이 있으면 프리팹으로 생성, 없으면 기본 생성
                if (_particlePrefab != null)
                {
                    var go = Instantiate(_particlePrefab, transform);
                    _particleSystem = go.GetComponent<ParticleSystem>();
                    if (_particleSystem == null)
                    {
                        _particleSystem = go.AddComponent<ParticleSystem>();
                    }
                }
                else
                {
                    _particleSystem = gameObject.AddComponent<ParticleSystem>();
                }
            }

            _mainModule = _particleSystem.main;
            _currentColor = _colorDefault;

            // 기본 설정
            _mainModule.startLifetime = _particleLifetime;
            _mainModule.startSpeed = _particleSpeed;
            _mainModule.startSize = _particleSize;
            _mainModule.startColor = _colorDefault;
            _mainModule.loop = true;
            _mainModule.playOnAwake = false;

            // Emission 설정
            var emission = _particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = _particleEmissionRate;

            // Shape: Cone (분사기 앞으로)
            var shape = _particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.1f;

            // Renderer
            var renderer = _particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 0;
            }

            // 초기에는 정지
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _wasSpraying = false;
        }

        private void Update()
        {
            if (_controller == null || _particleSystem == null)
                return;

            bool isSpraying = _controller.IsSpraying;

            if (isSpraying && !_wasSpraying)
            {
                // 분사 시작 — 파티클 재생
                UpdateParticleColor();
                _particleSystem.Play();
            }
            else if (!isSpraying && _wasSpraying)
            {
                // 분사 중단 — 파티클 정지
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            else if (isSpraying && _wasSpraying)
            {
                // 분사 중 — 컬러 변경 감시 (물약 변경 시)
                UpdateParticleColor();
            }

            _wasSpraying = isSpraying;
        }

        /// <summary>
        /// 현재 물약 속성에 따라 파티클 색상 업데이트
        /// </summary>
        private void UpdateParticleColor()
        {
            Color newColor = GetColorForPotion(_controller.LoadedPotionId);
            _mainModule.startColor = new ParticleSystem.MinMaxGradient(newColor);
        }

        /// <summary>
        /// 물약 ID에 따른 파티클 색상 반환
        /// </summary>
        public Color GetColorForPotion(string potionId)
        {
            return potionId.ToLowerInvariant() switch
            {
                "herb_red" => _colorHeal,
                "herb_purple" => _colorPoison,
                "herb_yellow" => _colorHallucination,
                "herb_silver" => _colorDetox,
                "herb_green" => _colorRegen,
                _ => _colorDefault
            };
        }

        /// <summary>
        /// 파티클 시스템 강제 업데이트 (테스트/외부 호출용)
        /// </summary>
        public void RefreshVFX()
        {
            if (_particleSystem != null)
            {
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                UpdateParticleColor();
                if (_controller != null && _controller.IsSpraying)
                {
                    _particleSystem.Play();
                }
            }
        }

        /// <summary>
        /// 현재 파티클 시스템 참조 (테스트용)
        /// </summary>
        public ParticleSystem CurrentParticleSystem => _particleSystem;
    }
}
