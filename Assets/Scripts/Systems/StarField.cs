using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C13-05: 밤에만 표시되는 별 ParticleSystem.
    /// TimeManager.IsNight에 따라 자동 생성/파괴됩니다.
    /// </summary>
    [RequireComponent(typeof(TimeManager))]
    public class StarField : MonoBehaviour
    {
        [Header("Star Settings")]
        [SerializeField] private int _starCount = 200;
        [SerializeField] private float _starRange = 100f;
        [SerializeField] private float _starSize = 0.15f;
        [SerializeField] private Color _starColor1 = Color.white;
        [SerializeField] private Color _starColor2 = new Color(1f, 0.9f, 0.6f); // 노란빛

        [Header("Twinkle")]
        [SerializeField] private float _minTwinkleSpeed = 0.5f;
        [SerializeField] private float _maxTwinkleSpeed = 2f;

        private TimeManager _timeManager;
        private ParticleSystem _particleSystem;
        private GameObject _particleGo;
        private bool _wasNight;

        private void Start()
        {
            _timeManager = TimeManager.Instance;
            if (_timeManager == null)
            {
                Debug.LogWarning("[StarField] TimeManager.Instance가 없습니다.");
                enabled = false;
                return;
            }

            _wasNight = _timeManager.IsNight;
            if (_wasNight)
            {
                CreateStarField();
            }
        }

        private void Update()
        {
            if (_timeManager == null) return;

            bool isNight = _timeManager.IsNight;

            if (isNight && !_wasNight)
            {
                // 밤 시작 → 별 생성
                CreateStarField();
            }
            else if (!isNight && _wasNight)
            {
                // 낮 시작 → 별 파괴
                DestroyStarField();
            }

            _wasNight = isNight;
        }

        private void CreateStarField()
        {
            if (_particleGo != null) return;

            _particleGo = new GameObject("StarField");
            _particleGo.transform.SetParent(transform, false);

            _particleSystem = _particleGo.AddComponent<ParticleSystem>();

            var main = _particleSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = _starCount;
            main.duration = 86400f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                _minTwinkleSpeed, _maxTwinkleSpeed
            );
            main.startSpeed = 0f;
            main.startSize = _starSize;
            main.startColor = _starColor1;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Emission: 모든 별을 한 번에 방출
            var emission = _particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, (short)_starCount)
            });

            // Shape: 구체 영역
            var shape = _particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = _starRange;
            shape.randomDirectionAmount = 1f;

            // Renderer: 작은 점
            var renderer = _particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            // 색상 다양화 (노랑 ~ 흰색) + 반짝임
            var colorOverLifetime = _particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(_starColor1, 0f),
                    new GradientColorKey(_starColor2, 0.5f),
                    new GradientColorKey(_starColor1, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.2f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0.3f, 0.75f),
                    new GradientAlphaKey(0.8f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            _particleSystem.Play();
        }

        private void DestroyStarField()
        {
            if (_particleGo == null) return;

            if (_particleSystem != null)
            {
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Destroy(_particleGo);
            _particleGo = null;
            _particleSystem = null;
        }

        private void OnDestroy()
        {
            DestroyStarField();
        }
    }
}
