using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-09: 충격 사운드 FX (정적 클래스) — 일반/치명/처치 레이어드 임팩트음.
    /// 재생 경로는 SoundManagerEnhanced.PlaySFX(string, float) 단 하나.
    /// PlaySFX는 Resources.Load 실패 시 Debug.Log(플레이스홀더 안내) 후 조용히 반환하므로
    /// 클립 에셋이 없어도 크래시/에러가 절대 발생하지 않는 가장 안전한 경로다
    /// (SoundEffectManager.SFXType은 impact_* 커스텀 ID를 표현할 수 없어 사용하지 않음).
    /// ActionFeel.HighSpec에서만 재생 — Balanced(저사양) 경로는 완전 무음 유지.
    /// MonoBehaviour 아님, Update 루프 없음.
    /// [무번호 72-W3] 타격 소리 피치 위계 — 피해량 결정론 밴드 매핑(팔 피치 위계 없음 → 3밴드+크리 가산).
    /// </summary>
    public static class ImpactSoundFX
    {
        // ================================================================
        // SFX ID 상수 — Resources/Sounds/SFX/{id} 로 로드됨 (에셋 없어도 안전)
        // ================================================================
        /// <summary>일반 타격 — 낮고 짧은 '쿵' (메인 레이어)</summary>
        private const string SFX_THUD = "impact_thud";
        /// <summary>치명타 — 더 크고 날카로운 임팩트 (메인 레이어)</summary>
        private const string SFX_CRIT = "impact_crit";
        /// <summary>처치 — 더 깊고 묵직한 임팩트 (메인 레이어)</summary>
        private const string SFX_KILL = "impact_kill";
        /// <summary>가벼운 잔향/덜그럭 레이어 (모든 등급의 서브 레이어)</summary>
        private const string SFX_RATTLE = "impact_rattle";

        // ================================================================
        // [무번호 72-W3] 피치 밴드 상수 — 결정론(Random 0건). 밴드 경계는 PlayerCombat
        // Heavy(≥15) 공유 기준. 피해<8 → 0.92 / 8≤d<15 → 1.00 / ≥15 → 1.06, 크리 ×1.04(상한 1.15).
        // ================================================================
        /// <summary>경타(피해 &lt; 8) — 낮고 둔한 피치.</summary>
        private const float PitchBandLow = 0.92f;
        /// <summary>중간 밴드(8 ≤ 피해 &lt; 15) 및 구호환(damage &lt; 0) 기준 피치.</summary>
        private const float PitchBandMid = 1.00f;
        /// <summary>강타(피해 ≥ 15) — 높고 아픈 피치.</summary>
        private const float PitchBandHigh = 1.06f;
        /// <summary>치명타 피치 가산 배율.</summary>
        private const float PitchCritMul = 1.04f;
        /// <summary>피치 상한 클램프 — 크리 가산 누적 과다 방지.</summary>
        private const float PitchCeiling = 1.15f;
        /// <summary>경타/중간 밴드 경계(피해량).</summary>
        private const float BandLowThreshold = 8f;
        /// <summary>중간/강타 밴드 경계(피해량 — PlayerCombat Heavy ≥15 공유 기준).</summary>
        private const float BandHighThreshold = 15f;

        /// <summary>
        /// 충격 사운드 재생 진입점.
        /// 등급 판정: 처치(isKill) > 치명타(isCrit) > 일반.
        /// 등급별로 2~3개의 PlaySFX 레이어를 약간 다른 볼륨으로 겹쳐 두께감을 만든다.
        /// 클립 에셋이 없으면 PlaySFX 내부에서 플레이스홀더 로그만 남고 무시된다.
        /// [무번호 72-W3] damage 선택 파라미터(기본 -1 = 구호환, 피치 기준 1.0) — 호출부는
        /// CombatFXGate 단일. 모든 레이어가 동일 피치로 재생(밴드 위계는 타격 강도 신호).
        /// </summary>
        public static void PlayHit(bool isCrit, bool isKill, float damage = -1f)
        {
            // 하이스펙 게이트 — Balanced(저사양) 경로는 아무 소리도 내지 않는다
            if (!ActionFeel.HighSpec) return;

            // [무번호 72-W3] 결정론 피치 산출 — 밴드 매핑 + 크리 가산(계산 1회, 레이어 공유).
            float pitch = ResolvePitch(isCrit, damage);

            if (isKill)
            {
                // 처치: 깊은 임팩트(메인) + 치명 임팩트 잔향 + 가벼운 덜그럭 3중 레이어
                Play(SFX_KILL, 1.0f, pitch);
                Play(SFX_CRIT, 0.55f, pitch);
                Play(SFX_RATTLE, 0.45f, pitch);
                Debug.Log($"[ImpactSFX-HS] kill impact layers=3 pitch={pitch:0.000}");
            }
            else if (isCrit)
            {
                // 치명타: 크고 날카로운 임팩트(메인) + 낮은 쿵(바디) + 가벼운 덜그럭 3중 레이어
                Play(SFX_CRIT, 0.9f, pitch);
                Play(SFX_THUD, 0.5f, pitch);
                Play(SFX_RATTLE, 0.4f, pitch);
                Debug.Log($"[ImpactSFX-HS] crit impact layers=3 pitch={pitch:0.000}");
            }
            else
            {
                // 일반 타격: 낮은 쿵(메인) + 가벼운 덜그럭 2중 레이어
                Play(SFX_THUD, 0.8f, pitch);
                Play(SFX_RATTLE, 0.35f, pitch);
                Debug.Log($"[ImpactSFX-HS] hit impact layers=2 pitch={pitch:0.000}");
            }
        }

        // ================================================================
        // [무번호 72-W3] 피치 밴드 매핑 — 결정론(조건 분기만, Random 0건).
        // damage < 0(구호환, 피해 미전달) → 1.0 기준. 그 외 3밴드 → 크리 ×1.04 → 상한 1.15 클램프.
        // ================================================================
        private static float ResolvePitch(bool isCrit, float damage)
        {
            float band = damage < 0f
                ? PitchBandMid
                : (damage < BandLowThreshold ? PitchBandLow
                    : (damage < BandHighThreshold ? PitchBandMid : PitchBandHigh));
            if (isCrit) band *= PitchCritMul;
            return Mathf.Clamp(band, 0f, PitchCeiling);
        }

        // ================================================================
        // 내부 재생 헬퍼 — 매 호출마다 Instance null 가드 (크래시 방지)
        // ================================================================
        private static void Play(string clipId, float volume, float pitch)
        {
            // Instance 프로퍼티는 앱 종료 중이면 null을 반환하므로 레이어마다 가드한다.
            // 가드에 걸리면 조용히 무시 — 어떤 경우에도 예외가 밖으로 새지 않는다.
            var sm = SoundManagerEnhanced.Instance;
            if (sm == null) return;

            sm.PlaySFX(clipId, volume, pitch);
        }
    }
}
