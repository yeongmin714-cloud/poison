using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C13-02/03: 주야간 조명 및 환경 제어 시스템.
    /// TimeManager의 DayProgress에 따라 태양/주변광/안개를 변경합니다.
    /// </summary>
    [RequireComponent(typeof(TimeManager))]
    public class DayNightCycle : MonoBehaviour
    {
        [Header("Sun Reference")]
        [SerializeField] private Light _sunLight;

        [Header("Sun Rotation")]
        [SerializeField] private float _longitude = 30f;

        [Header("Sun Colors")]
        [SerializeField] private Color _noonColor = new Color(1f, 0.95f, 0.8f);
        [SerializeField] private Color _eveningColor = new Color(1f, 0.5f, 0.2f);
        [SerializeField] private Color _nightColor = new Color(0.1f, 0.1f, 0.3f);

        [Header("Sun Intensity")]
        [SerializeField] private float _noonIntensity = 1f;
        [SerializeField] private float _eveningIntensity = 0.3f;
        [SerializeField] private float _nightIntensity = 0f;

        [Header("Shadow Strength")]
        [SerializeField] private float _noonShadowStrength = 1f;
        [SerializeField] private float _nightShadowStrength = 0f;

        [Header("Ambient Light")]
        [SerializeField] private Color _dayAmbient = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color _nightAmbient = new Color(0.05f, 0.05f, 0.1f);

        [Header("Fog Settings")]
        [SerializeField] private Color _dayFogColor = new Color(0.7f, 0.75f, 0.8f);
        [SerializeField] private Color _nightFogColor = new Color(0.05f, 0.05f, 0.1f);
        [SerializeField] private float _dayFogDensity = 0.01f;
        [SerializeField] private float _nightFogDensity = 0.03f;

        private TimeManager _timeManager;
        private Light _resolvedSun;
        private bool _hasSun;

        private void Start()
        {
            _timeManager = TimeManager.Instance;
            if (_timeManager == null)
            {
                Debug.LogError("[DayNightCycle] TimeManager.Instance가 없습니다.");
                enabled = false;
                return;
            }

            // Sun 참조 해결
            if (_sunLight != null)
            {
                _resolvedSun = _sunLight;
                _hasSun = true;
            }
            else
            {
                var found = FindObjectOfType<Light>();
                if (found != null && found.type == LightType.Directional)
                {
                    _resolvedSun = found;
                    _hasSun = true;
                    _sunLight = found;
                }
                else
                {
                    Debug.LogWarning("[DayNightCycle] Directional Light를 찾을 수 없습니다.");
                    _hasSun = false;
                }
            }
        }

        private void Update()
        {
            if (_timeManager == null) return;

            float dayProgress = _timeManager.DayProgress; // 0.0 ~ 1.0

            // ===== 태양 회전 =====
            // dayFactor: Cos(2π * dayProgress) 기반 보간값
            // 0(자정, Cos=1) → 0.5(정오, Cos=-1) → 1.0(자정, Cos=1)
            // dayFactor = (1 - Cos(2π * dayProgress)) / 2
            //   → 0: Lerp(180,0,0)=180° (자정, X=180°)
            //   → 1: Lerp(180,0,1)=0° (정오, X=0°)
            float cosValue = Mathf.Cos(dayProgress * Mathf.PI * 2f);
            float dayFactor = (1f - cosValue) / 2f;

            if (_hasSun && _resolvedSun != null)
            {
                // X축 회전: 0°(정오) ~ 180°(자정)
                _resolvedSun.transform.eulerAngles = new Vector3(
                    Mathf.Lerp(180f, 0f, dayFactor),
                    _longitude,
                    0f
                );

                // ===== Sun 색상/강도 보간 =====
                // dayFactor 구간: 0(자정) → 0.25(아침) → 0.5(정오) → 0.75(저녁) → 1.0(자정)
                Color sunColor;
                float sunIntensity;
                float shadowStrength;

                if (dayFactor < 0.25f)
                {
                    // 자정 → 아침 (밤 → 새벽)
                    float t = dayFactor / 0.25f;
                    sunColor = Color.Lerp(_nightColor, _eveningColor, t);
                    sunIntensity = Mathf.Lerp(_nightIntensity, _eveningIntensity, t);
                    shadowStrength = Mathf.Lerp(_nightShadowStrength, _noonShadowStrength, t);
                }
                else if (dayFactor < 0.5f)
                {
                    // 아침 → 정오
                    float t = (dayFactor - 0.25f) / 0.25f;
                    sunColor = Color.Lerp(_eveningColor, _noonColor, t);
                    sunIntensity = Mathf.Lerp(_eveningIntensity, _noonIntensity, t);
                    shadowStrength = Mathf.Lerp(_noonShadowStrength, _noonShadowStrength, t);
                }
                else if (dayFactor < 0.75f)
                {
                    // 정오 → 저녁
                    float t = (dayFactor - 0.5f) / 0.25f;
                    sunColor = Color.Lerp(_noonColor, _eveningColor, t);
                    sunIntensity = Mathf.Lerp(_noonIntensity, _eveningIntensity, t);
                    shadowStrength = Mathf.Lerp(_noonShadowStrength, _noonShadowStrength, t);
                }
                else
                {
                    // 저녁 → 자정
                    float t = (dayFactor - 0.75f) / 0.25f;
                    sunColor = Color.Lerp(_eveningColor, _nightColor, t);
                    sunIntensity = Mathf.Lerp(_eveningIntensity, _nightIntensity, t);
                    shadowStrength = Mathf.Lerp(_noonShadowStrength, _nightShadowStrength, t);
                }

                _resolvedSun.color = sunColor;
                _resolvedSun.intensity = sunIntensity;

                // Shadow Strength (Unity 2019+)
                #if UNITY_2019_1_OR_NEWER
                _resolvedSun.shadowStrength = shadowStrength;
                #endif

                // 태양이 지평선 아래(밤)면 비활성화
                // dayFactor 0~0.25, 0.75~1.0 = 밤 시간대 → 태양 아래
                float sunAngle = Mathf.Lerp(180f, 0f, dayFactor);
                _resolvedSun.enabled = sunAngle > 5f && sunAngle < 175f;
            }

            // ===== 환경 설정 (Cos 기반 부드러운 보간) =====
            // cosValue: 1(자정) → -1(정오) → 1(자정)
            // ambientT: 0(자정, 완전 밤) → 1(정오, 완전 낮)
            float ambientT = (-cosValue + 1f) / 2f;

            RenderSettings.ambientLight = Color.Lerp(_nightAmbient, _dayAmbient, ambientT);
            RenderSettings.fogColor = Color.Lerp(_nightFogColor, _dayFogColor, ambientT);
            RenderSettings.fogDensity = Mathf.Lerp(_nightFogDensity, _dayFogDensity, ambientT);
            RenderSettings.fog = true;
        }
    }
}
