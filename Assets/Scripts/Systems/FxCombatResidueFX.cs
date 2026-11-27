using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 71-W1] 전투 잔상 비네트 — 처치 직후 긴장 잔광(중립 청록).
    ///  - 트리거: CombatCameraEffects.PlayKill() 처치 진입 단일 관문 훅(AnimalAI:1513 직접 · PlayKillVFX:825 수렴 실측).
    ///  - 재사용: ScreenVignetteFX.Begin(23-V API — 본 라운드 무수정, 69-R7 신선 텍스처 계약 보존).
    ///  - 활성 창 가드: 단일 인스턴스(연속 처치 중첩 방지 — 비네트 스택 없음). static 상태의 해제는
    ///    ResidueWindowRunner.OnDestroy 단일 경로(FxImpactLight 감사 OnDestroy 단일 경로 선례 — 이중 감산 방지).
    ///  - 색: FXPalette.MoveCommand 위임(청록 — CommandRingPresentation.MoveTargetColor 리터럴
    ///    (0.188, 0.835, 0.784, 1) 단일 소스 미러, 28-U "값 복제 금지" 규약 준수). 피격 적색/패망 골드와 구별.
    ///  - unscaled 시간(히트스톱/슬로모 중에도 재생 — VignettePulse 선례), EditMode 무동작 가드, 결정론 — 난수 0건.
    /// </summary>
    public static class FxCombatResidueFX
    {
        /// <summary>[71-W1] 비네트 파라미터 — peak 0.22 / fadeIn 0.15 / hold 0.35 / fadeOut 1.0 = 총 1.5s(계획서 스펙).</summary>
        public const float PeakAlpha = 0.22f;
        public const float FadeInSec = 0.15f;
        public const float HoldSec = 0.35f;
        public const float FadeOutSec = 1.0f;

        /// <summary>[71-W1] 활성 창 길이 — 비네트 총 수명과 동일(창 활성 중 신규 펄스 금지 = 연속 처치 중첩 방지).</summary>
        public const float WindowSec = FadeInSec + HoldSec + FadeOutSec;

        /// <summary>활성 창 러너 — null이 아니면 창 활성(단일 인스턴스 가드). 해제는 OnDestroy 단일 경로만.</summary>
        private static ResidueWindowRunner _runner;

        /// <summary>[무번호 71-W1] 처치 잔상 펄스 — 활성 창 내 재진입 무시(단일 인스턴스), 미활성 시 1회 발화.</summary>
        public static void Pulse()
        {
            if (!Application.isPlaying) return;   // EditMode 무동작 가드
            if (_runner != null) return;          // 활성 창 가드 — 연속 처치 중첩 방지(단일 인스턴스)
            try
            {
                var cam = Camera.main;
                if (cam == null) return;
                // [71-W1] 색 단일 소스 — FXPalette.MoveCommand(청록) 경유, ScreenVignetteFX.Begin 재사용(23-V API).
                var pulse = ScreenVignetteFX.Begin(cam, FXPalette.MoveCommand, PeakAlpha, FadeInSec, HoldSec, FadeOutSec);
                if (pulse == null) return;   // Begin 실패(비네트 생성 불가 등) — 창 미개시 무음 폴백
                var host = new GameObject("FxCombatResidueWindow") { hideFlags = HideFlags.HideAndDontSave };
                _runner = host.AddComponent<ResidueWindowRunner>();
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 흐름 절대 방해 금지(CombatFXGate 예외 격리 선례)
        }

        /// <summary>[71-W1] 창 해제 — ResidueWindowRunner.OnDestroy에서만 호출되는 단일 해제 경로.</summary>
        private static void ReleaseWindow() { _runner = null; }

        /// <summary>[71-W1] 활성 창 러너 — unscaled 시간 계수, WindowSec 경과 시 자가 파괴 → OnDestroy가 유일 해제 경로.</summary>
        sealed class ResidueWindowRunner : MonoBehaviour
        {
            private float _t;

            private void Update()
            {
                _t += Time.unscaledDeltaTime;   // unscaled — 히트스톱 중에도 창 유지(VignettePulse 선례)
                if (_t >= WindowSec) Destroy(gameObject);
            }

            private void OnDestroy() { FxCombatResidueFX.ReleaseWindow(); }   // 해제 단일 경로
        }
    }
}
