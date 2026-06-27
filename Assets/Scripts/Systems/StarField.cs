using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C13-05 + G3-01: 밤에만 표시되는 별 ParticleSystem.
    /// TimeManager.IsNight에 따라 자동 생성/파괴됩니다.
    /// 
    /// [G3-01] 개선 사항:
    ///   - 별 반짝임(깜빡임) 강화 - sizeOverLifetime + colorOverLifetime
    ///   - 반짝임 주기/크기 랜덤화
    ///   - 부드러운 알파 페이드 인/아웃
    /// </summary>
    public class StarField : MonoBehaviour
    {
        [Header("Star Settings")]
        [SerializeField] private int _starCount = 200;
        [SerializeField] private float _starRange = 100f;
        [SerializeField] private float _starSize = 0.15f;
        [SerializeField] private Color _starColor1 = Color.white;
        [SerializeField] private Color _starColor2 = new Color(1f, 0.9f, 0.6f); // 노란빛

        [Header("Twinkle (G3-01)")]
        [SerializeField] private float _minTwinkleSpeed = 0.5f;
        [SerializeField] private float _maxTwinkleSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float _minTwinkleSize = 0.3f;  // 반짝임 최소 크기 계수
        [SerializeField, Range(0f, 1f)] private float _minTwinkleAlpha = 0.1f; // 반짝임 최소 알파

        [Header("Appearance")]
        [SerializeField] private float _starBrightness = 1f;

        private TimeManager _timeManager;
        private ParticleSystem _particleSystem;
        private GameObject _particleGo;
        private bool _wasNight;

        private void Awake()
        {
            // 싱글톤 Instance를 통해 TimeManager 참조 획득
            _timeManager = TimeManager.Instance;
            if (_timeManager == null)
            {
                Debug.LogWarning("[StarField] TimeManager.Instance가 없습니다.");
                enabled = false;
                return;
            }

            // 이벤트 구독: 폴링 대신 TimeManager의 OnDayNightChanged 사용
            _timeManager.OnDayNightChanged += OnDayNightChanged;
        }

        private void Start()
        {
            // Awake에서 이미 Instance 체크 완료
            _wasNight = _timeManager.IsNight;
            if (_wasNight)
            {
                CreateStarField();
            }
        }

        private void OnDayNightChanged(bool isDay)
        {
            if (isDay)
            {
                DestroyStarField();
            }
            else
            {
                CreateStarField();
            }
        }

        private void OnDestroy()
        {
            if (_timeManager != null)
            {
                _timeManager.OnDayNightChanged -= OnDayNightChanged;
            }
            DestroyStarField();
        }

        /// <summary>
        /// 별 ParticleSystem을 생성합니다.
        /// [G3-01] sizeOverLifetime과 colorOverLifetime을 사용한 반짝임 구현.
        /// </summary>
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

            // Emission: rateOverTime으로 별을 지속적으로 재생성
            // 각 별의 평균 수명을 기준으로 _starCount를 유지하도록 rate 계산
            var emission = _particleSystem.emission;
            emission.enabled = true;
            float avgLifetime = (_minTwinkleSpeed + _maxTwinkleSpeed) * 0.5f;
            float ratePerSecond = avgLifetime > 0f ? _starCount / avgLifetime : _starCount;
            emission.rateOverTime = Mathf.Max(ratePerSecond, 1f);

            // 초기 별들을 한 번에 방출하여 즉시 별이 보이게 함
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, (short)_starCount)
            });

            // Shape: 구체 영역 (표면 분포)
            var shape = _particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = _starRange;
            shape.randomDirectionAmount = 0f;        // 불필요한 방향 랜덤화 제거
            shape.alignToDirection = false;

            // Renderer: URP 호환 Particle 셰이더 사용
            var renderer = _particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null)
            {
                // Fallback: Built-in Sprites/Default (URP가 아닌 환경 대응)
                particleShader = Shader.Find("Sprites/Default");
            }
            renderer.material = new Material(particleShader);
            renderer.sortingOrder = -1; // 배경보다 뒤에 렌더링

            // ================================================================
            // [G3-01] 별 반짝임 (깜빡임) 강화
            // ================================================================

            // (A) Size Over Lifetime: 별 크기가 시간에 따라 커졌다 작아짐
            var sizeOverLifetime = _particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = false;

            // AnimationCurve: 0→1→0 (한 번 반짝임)
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, _minTwinkleSize);           // 시작: 작게
            sizeCurve.AddKey(0.15f, 1f);                     // 15%: 최대 크기
            sizeCurve.AddKey(0.5f, _minTwinkleSize);         // 50%: 다시 작게
            sizeCurve.AddKey(0.85f, 1f);                     // 85%: 최대 크기
            sizeCurve.AddKey(1f, _minTwinkleSize);           // 끝: 작게

            // 키를 부드럽게 (Auto smooth)
            for (int i = 0; i < sizeCurve.length; i++)
            {
                sizeCurve.SmoothTangents(i, 0.5f);
            }

            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // (B) Color Over Lifetime: 알파 채널 깜빡임 + 색상 변화
            var colorOverLifetime = _particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            // Gradient: 알파가 0.1↔1.0으로 깜빡이고 색상도 흰색↔노란색 변화
            var gradient = new Gradient();

            // 밝기 계수를 미리 계산해서 Color 연산 최적화
            Color brightColor1 = _starColor1 * _starBrightness;
            Color brightColor2 = _starColor2 * _starBrightness;

            // Color keys: 흰색(별색1) ↔ 노란색(별색2)
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(brightColor1, 0f),
                    new GradientColorKey(brightColor2, 0.25f),
                    new GradientColorKey(brightColor1, 0.5f),
                    new GradientColorKey(brightColor2, 0.75f),
                    new GradientColorKey(brightColor1, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(_minTwinkleAlpha, 0f),     // 시작: 거의 안보임
                    new GradientAlphaKey(1f, 0.15f),                // 15%: 최대 밝기
                    new GradientAlphaKey(_minTwinkleAlpha, 0.3f),   // 30%: 다시 어둡게
                    new GradientAlphaKey(1f, 0.5f),                 // 50%: 최대 밝기
                    new GradientAlphaKey(_minTwinkleAlpha * 0.5f, 0.65f), // 65%: 어둡게
                    new GradientAlphaKey(1f, 0.8f),                 // 80%: 최대 밝기
                    new GradientAlphaKey(_minTwinkleAlpha, 1f)      // 끝: 어둡게
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
                // 기존 파티클까지 즉시 제거해야 잔상이 남지 않음
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            Destroy(_particleGo);
            _particleGo = null;
            _particleSystem = null;
        }
    }
}