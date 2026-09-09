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

        /// <summary>
        /// 충격 사운드 재생 진입점.
        /// 등급 판정: 처치(isKill) > 치명타(isCrit) > 일반.
        /// 등급별로 2~3개의 PlaySFX 레이어를 약간 다른 볼륨으로 겹쳐 두께감을 만든다.
        /// 클립 에셋이 없으면 PlaySFX 내부에서 플레이스홀더 로그만 남고 무시된다.
        /// </summary>
        public static void PlayHit(bool isCrit, bool isKill)
        {
            // 하이스펙 게이트 — Balanced(저사양) 경로는 아무 소리도 내지 않는다
            if (!ActionFeel.HighSpec) return;

            if (isKill)
            {
                // 처치: 깊은 임팩트(메인) + 치명 임팩트 잔향 + 가벼운 덜그럭 3중 레이어
                Play(SFX_KILL, 1.0f);
                Play(SFX_CRIT, 0.55f);
                Play(SFX_RATTLE, 0.45f);
                Debug.Log($"[ImpactSFX-HS] kill impact layers=3");
            }
            else if (isCrit)
            {
                // 치명타: 크고 날카로운 임팩트(메인) + 낮은 쿵(바디) + 가벼운 덜그럭 3중 레이어
                Play(SFX_CRIT, 0.9f);
                Play(SFX_THUD, 0.5f);
                Play(SFX_RATTLE, 0.4f);
                Debug.Log($"[ImpactSFX-HS] crit impact layers=3");
            }
            else
            {
                // 일반 타격: 낮은 쿵(메인) + 가벼운 덜그럭 2중 레이어
                Play(SFX_THUD, 0.8f);
                Play(SFX_RATTLE, 0.35f);
                Debug.Log($"[ImpactSFX-HS] hit impact layers=2");
            }
        }

        // ================================================================
        // 내부 재생 헬퍼 — 매 호출마다 Instance null 가드 (크래시 방지)
        // ================================================================
        private static void Play(string clipId, float volume)
        {
            // Instance 프로퍼티는 앱 종료 중이면 null을 반환하므로 레이어마다 가드한다.
            // 가드에 걸리면 조용히 무시 — 어떤 경우에도 예외가 밖으로 새지 않는다.
            var sm = SoundManagerEnhanced.Instance;
            if (sm == null) return;

            sm.PlaySFX(clipId, volume);
        }
    }
}
