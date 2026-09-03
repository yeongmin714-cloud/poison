using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectName.Systems
{
    /// <summary>
    /// [T-R6] 런타임 뷰티 컬러그레이딩 (Global Volume → ColorAdjustments).
    ///
    /// 빈 프로필 에셋(Default_VolumeProfile_Enhanced.asset)에 에디터 없이 런타임으로
    /// ColorAdjustments 오버라이드를 주입한다. 채도 +12, 콘트라스트 +5 (계획 R6).
    ///
    /// 화이트아웃 방지 (과거 39/40 사고 재발 방지):
    ///   - postExposure 를 건드리지 않고(overrideState=false, 0 유지) → 노출 불변.
    ///   - ColorAdjustments는 tonemapping 전 단계라 값이 작으면 클리핑을 만들지 않는다.
    ///     콘트라스트 +5 는 중간톤 대비만 소폭 확대, 채도 +12 는 색만 포화(휘도 무변) —
    ///     선형 휘도 상한(≈1.17, ACES로 <1.0 압축)을 넘지 않아 클리핑 <10% 유지.
    ///
    /// API 사용: UnityEngine.Rendering.Volume.profile getter는 공유 프로필을 이 볼륨 전용으로
    ///   자동 Instantiate(클론)해서 돌려준다 → 원본 에셋 파일은 불변(빈 프로필 에셋 YAML 갱신 없음).
    ///   그 클론에 Add&lt;ColorAdjustments&gt;(overrides:true)로 오버라이드를 쌓는다.
    ///   (프로젝트 기존 사용 예: PhaseG2_PostProcessingSetup.cs ConfigureColorAdjustments — 같은 패턴)
    /// </summary>
    public class MoodProfileSetup : MonoBehaviour
    {
        // 계획 R6: 채도 +12, 콘트라스트 +5
        public const float SaturationBoost = 12f;
        public const float ContrastBoost = 5f;

        private static bool _applied; // 싱글턴 가드 (씬/런타임 재실행 대비)

        private void Start()
        {
            if (_applied)
            {
                Debug.Log("[MoodProfileSetup] 이미 적용됨 — 스킵 (1회 가드)");
                return;
            }

            // ── 글로벌 볼륨 탐색 (씬: 'Global Volume', isGlobal=1, sharedProfile=Default_VolumeProfile_Enhanced) ──
            var vol = FindAnyObjectByType<UnityEngine.Rendering.Volume>();
            if (vol == null)
            {
                Debug.LogWarning("[MoodProfileSetup] 씬에 Volume(Global Volume) 없음 — 컬러그레이딩 생략");
                return;
            }

            // profile getter가 공유 프로필을 이 볼륨 전용 클론으로 만들고 반환한다 (원본 에셋 불변).
            VolumeProfile profile = vol.profile;
            if (profile == null)
            {
                Debug.LogWarning("[MoodProfileSetup] Volume.profile이 null — 컬러그레이딩 생략");
                return;
            }

            ColorAdjustments ca;
            if (!profile.TryGet(out ca))
            {
                ca = profile.Add<ColorAdjustments>(overrides: true);
                Debug.Log("[MoodProfileSetup] ColorAdjustments 오버라이드 추가 (런타임 클론 프로필)");
            }

            // 채도 +12 (계획)
            ca.saturation.overrideState = true;
            ca.saturation.value = SaturationBoost;

            // 콘트라스트 +5 (계획)
            ca.contrast.overrideState = true;
            ca.contrast.value = ContrastBoost;

            // 노출은 불변 (화이트아웃 방지) — overrideState=false 유지
            ca.postExposure.overrideState = false;
            ca.hueShift.overrideState = false;
            ca.colorFilter.overrideState = false;

            _applied = true;
            Debug.Log($"[MoodProfileSetup] ✅ 컬러그레이딩 적용: saturation=+{SaturationBoost}, contrast=+{ContrastBoost} (postExposure 불변, 클리핑 안전)");
        }

        // rime: 토글만 존재 — 비활성화하려면 Start에서 early-return하도록 빌드 옵션.
    }
}