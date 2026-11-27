using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 33-B1] 블룸 임팩트 펄스 — 포스트 계층 2(31-V3 ScreenChromaticFX 볼륨 인스턴스 패턴 재사용).
    ///  - 별도 클래스(잠금 분리) + 별도 프로필(Bloom 오버라이드만) — CA 볼륨과 파라미터 충돌 없음.
    ///  - 스파이크 중에만 오버라이드 active=true, 종료 시 active=false로 글로벌 블룸(PhaseG2) 완전 회복 — 잔존 억제.
    ///  - unscaled 시간(히트스톱 중에도 재생), EditMode 무동작 가드, 인스턴스 1개 재사용(누적 금지),
    ///    OnDestroy 프로필 파괴(누출 금지), 결정론 — 외부 난수 미사용.
    /// </summary>
    public static class ScreenBloomFX
    {
        private static BloomPulseRunner _runner;

        /// <summary>[33-B1] 블룸 스파이크 — peakIntensity(글로벌 1.0 위 오버라이드 값, 예: 2.2), 전체 길이(예: 0.12s).
        ///  기존 스파이크 진행 중 재호출 시 타이머 리스타트(인스턴스 재사용 — 오브젝트 누적 금지).</summary>
        public static void Spike(Camera cam, float peakIntensity, float totalDuration)
        {
            if (!Application.isPlaying || cam == null) return;   // EditMode 무동작 가드 + null 카메라
            try
            {
                if (_runner == null)
                {
                    var go = new GameObject("ScreenBloomFXRunner");
                    go.transform.SetParent(cam.transform, false);
                    _runner = go.AddComponent<BloomPulseRunner>();
                }
                _runner.Restart(peakIntensity, totalDuration);   // 스파이크 중 재호출 = 타이머 리스타트
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 방해 금지
        }
    }

    /// <summary>[33-B1] 블룸 펄스 러너 — 글로벌 볼륨 1개 + 절차 프로필(Bloom 오버라이드만).</summary>
    internal sealed class BloomPulseRunner : MonoBehaviour
    {
        private Volume _volume;
        private VolumeProfile _profile;
        private Bloom _bloom;
        private float _peak;
        private float _total;
        private float _elapsed;

        private void Awake()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = _profile.Add<Bloom>(true);
            _bloom.intensity.Override(0f);
            _bloom.active = false;   // 비스파이크 구간 — 글로벌 블룸(PhaseG2) 그대로
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
            _total = Mathf.Max(0.01f, totalDuration);
            _elapsed = 0f;
            if (_bloom != null) _bloom.active = true;
            enabled = true;
        }

        private void Update()
        {
            try
            {
                _elapsed += Time.unscaledDeltaTime;   // unscaled — 히트스톱 중에도 재생
                if (_bloom == null) { enabled = false; return; }
                if (_elapsed >= _total)
                {
                    _bloom.active = false;   // 글로벌 블룸 회복 — 잔존 억제(31-V3 0 복귀 계약 승계)
                    enabled = false;
                    return;
                }
                _bloom.intensity.Override(_peak * (1f - Mathf.Clamp01(_elapsed / _total)));   // 즉시 peak → 선형 소멸
            }
            catch (System.Exception) { enabled = false; }   // 시각 보조 — 전투 방해 금지
        }

        private void OnDestroy()
        {
            if (_profile != null) Object.Destroy(_profile);   // 프로필 파괴(누출 금지)
            _volume = null;
            _bloom = null;
        }
    }
}
