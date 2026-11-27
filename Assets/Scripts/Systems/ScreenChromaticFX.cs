using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 31-V3] 크로매틱 수차 스파이크 — 화면 계층 마지막 조각(무번호 23 계획서의 V3 전환 구현).
    ///  - 런타임 Volume(isGlobal) + VolumeProfile에 ChromaticAberration 오버라이드만 추가(IndoorPostProcessController 선례 —
    ///    런타임 프로필 생성, 에셋 파일 없이 코드 상수가 진실원).
    ///  - Spike(cam, peak, total): intensity 0→peak→0 스파이크(전체 0.1s, 진입 0.02s — 기본값은 호출 인라인 상수).
    ///  - 인스턴스 1개 재사용(오브젝트 누적 금지), 스파이크 중 재호출 = 타이머 리스타트.
    ///  - unscaled 시간(VignettePulse 선례 — 히트스톱 중에도 재생), EditMode 무동작 가드,
    ///    try/catch(시각 보조 — 전투 방해 금지), 결정론(외부 난수 0건), OnDestroy 프로필 파괴(누출 금지).
    /// </summary>
    public static class ScreenChromaticFX
    {
        private static ScreenChromaticRunner _runner;

        /// <summary>[무번호 31-V3] 수차 스파이크 발화 — peakIntensity(예: 0.35f) / totalDuration(예: 0.1f, 진입 0.02s).</summary>
        public static void Spike(Camera cam, float peakIntensity, float totalDuration)
        {
            if (!Application.isPlaying || cam == null) return;   // EditMode 무동작 가드 + null 카메라
            try
            {
                if (_runner == null)
                {
                    var go = new GameObject("ScreenChromaticFXRunner");
                    go.transform.SetParent(cam.transform, false);
                    _runner = go.AddComponent<ScreenChromaticRunner>();
                }
                _runner.Restart(peakIntensity, totalDuration);   // 스파이크 중 재호출 = 타이머 리스타트(인스턴스 재사용)
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 방해 금지
        }
    }

    /// <summary>[무번호 31-V3] 스파이크 러너 — 글로벌 볼륨 1개 + 절차 프로필(ChromaticAberration 오버라이드만).</summary>
    internal sealed class ScreenChromaticRunner : MonoBehaviour
    {
        public const float EntrySeconds = 0.02f;   // 진입 0.02s — 0→peak 램프(계획서 고정)

        private Volume _volume;
        private VolumeProfile _profile;
        private ChromaticAberration _ca;
        private float _peak;
        private float _total;
        private float _elapsed;

        private void Awake()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _ca = _profile.Add<ChromaticAberration>(true);
            _ca.intensity.Override(0f);
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.weight = 1f;
            _volume.profile = _profile;
        }

        /// <summary>스파이크 재시작 — 타이머 리스타트, 인스턴스는 그대로 재사용(오브젝트 누적 금지).</summary>
        public void Restart(float peakIntensity, float totalDuration)
        {
            _peak = Mathf.Max(0f, peakIntensity);
            _total = Mathf.Max(EntrySeconds, totalDuration);
            _elapsed = 0f;
            enabled = true;
        }

        private void Update()
        {
            try
            {
                _elapsed += Time.unscaledDeltaTime;   // unscaled — 히트스톱 중에도 재생
                if (_elapsed >= _total)
                {
                    _ca.intensity.Override(0f);
                    enabled = false;   // 스파이크 완료 — 인스턴스 재사용(오브젝트 누적 금지)
                    return;
                }
                float t = _elapsed <= EntrySeconds
                    ? Mathf.Clamp01(_elapsed / EntrySeconds)
                    : Mathf.Clamp01(1f - (_elapsed - EntrySeconds) / Mathf.Max(0.0001f, _total - EntrySeconds));
                _ca.intensity.Override(_peak * t);
            }
            catch (System.Exception) { enabled = false; }   // 시각 보조 — 전투 방해 금지
        }

        private void OnDestroy()
        {
            if (_profile != null) Object.Destroy(_profile);   // 프로필 파괴(누출 금지)
            _volume = null;
            _ca = null;
        }
    }
}
