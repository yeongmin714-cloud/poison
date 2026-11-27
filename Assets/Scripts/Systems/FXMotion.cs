using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 28-U3] VFX 모션 언어 3종 표준 — 28 계획 U-3: 신규 FX(M/S/P) 전부 3종에서 선택 사용.
    ///   기존 검증 이징(20-I3 smoothstep+임박 가속 / 20-J3 easeInCubic)을 순수 함수로 추출해
    ///   FX 간 모션 언어를 통일한다(사용자 요구: "VFX 전체 통일감"). 결정론 — 난수 사용 0건(무작위 없음).
    /// 수명 규약: 짧은 FX 0.4~0.9s / 지속 링 1s 표시 구간([72차] 계약) / 소멸 팝 금지(램프다운 — 24-FA1·C3 선례).
    /// </summary>
    public static class FXMotion
    {
        /// <summary>[무번호 28-U3] 짧은 FX(버스트/퍼프/스파크) 최소 수명(초).</summary>
        public const float ShortFxMinSec = 0.4f;
        /// <summary>[무번호 28-U3] 짧은 FX(버스트/퍼프/스파크) 최대 수명(초).</summary>
        public const float ShortFxMaxSec = 0.9f;
        /// <summary>[무번호 28-U3] 지속형 지면 링 표시 구간([72차] 1s 규약 — 도착 소멸 미적용 계약 유지).</summary>
        public const float GroundRingDisplaySec = 1.0f;

        /// <summary>
        /// [무번호 28-U3] 진행류 표준 이징 — smoothstep(20-I3 선례) + 임박 가속.
        /// 마지막 20% 구간(p≥0.8)에서 잔여 여유의 10%를 추가로 조임(→1.0 조기 도달, "곧 도착" 신호).
        /// [20-I3] 원형(CommandMarker L159-160): smoothstep 진행 + ≥0.8 구간 추가 10% 조임 — 구조 계승.
        /// </summary>
        public static float Progress(float t)
        {
            t = Mathf.Clamp01(t);
            float p = Mathf.SmoothStep(0f, 1f, t);   // [20-I3] smoothstep 이징(자연 감쇠)
            if (p < 0.8f) return p;
            // 임박 가속 — 마지막 20%에서 잔여 여유의 10%씩 조임(1.0 클램프 — 단조·연속)
            return Mathf.Min(1f, p + 0.10f * ((p - 0.8f) / 0.2f));
        }

        /// <summary>
        /// [무번호 28-U3] 잔광류 표준 이징 — easeInCubic(t*t*t).
        /// [20-J3] 선례: 수명 대부분 밝게 유지 후 마지막 소멸(타격감 잔광).
        /// </summary>
        public static float Afterglow(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>
        /// [무번호 28-U3] 출현류 표준 이징 — easeOutBack 소폭(오버슈트 ≈8%).
        /// 표준 c1=1.70158(≈10% 오버슈트) → c1=1.5로 절제(≈8%) — "출현 펄스"가 과장되지 않게.
        /// 스폰 확산/등장 팝에 사용. 결정론 — 난수 사용 0건(무작위 없음).
        /// </summary>
        public static float SpawnPulse(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.5f;      // 표준 1.70158 → 절제(오버슈트 ≈8%)
            const float c3 = c1 + 1f;   // easeOutBack 표준 계수 관계
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
