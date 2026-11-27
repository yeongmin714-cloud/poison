using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 35-S2] 히트 실루엣 화이트아웃 — BotW식 피격 순간 실루엣 백색 발광 플래시.
    ///  - FxRimOverlay 재사용(29-D3 — 소스 메시/본 공유 제2 렌더러) — 흰 림 강도 스파이크 0.1s 후 해제.
    ///  - 기존 47차 sharedMaterial _Color 틴트(CombatVFXController.PlayHitFlash)와 병행 — 림=윤곽, 틴트=면(이원 계층).
    ///  - 동시 플래시 예산 가드(상한 6 — 33-A1 선례), EditMode 무동작 가드, 유한 수명 해제(누출 금지),
    ///    셰이더/메시 부재 = 조용 스킵 폴백(기존 틴트만 — 29-D3 폴백 계약 승계), 외부 난수 미사용.
    /// </summary>
    public static class FxHitSilhouette
    {
        /// <summary>[35-S2] 동시 플래시 예산 상한 — 초과 시 조용 스킵(대규모 전투 안전망).</summary>
        public const int MaxConcurrentFlashes = 6;
        private const float FlashSeconds = 0.1f;
        private const float PeakIntensity = 3f;
        private static int _active;

        /// <summary>[35-S2] 실루엣 플래시 — 피격 대상 SMR(없으면 MeshRenderer 정적 오버로드)에 흰 림 스파이크.</summary>
        public static void Flash(GameObject target)
        {
            if (!Application.isPlaying || target == null) return;   // EditMode 무동작 가드
            if (_active >= MaxConcurrentFlashes) return;            // 예산 가드 — 초과 조용 스킵
            try
            {
                GameObject overlay = null;
                var smr = target.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh != null)
                    overlay = FxRimOverlay.AttachRim(smr, FXPalette.AttackPrimary, 2.0f, PeakIntensity);   // [79-R20] 공격 이펙트 통일색
                if (overlay == null)
                {
                    var mr = target.GetComponentInChildren<MeshRenderer>();
                    if (mr != null)
                        overlay = FxRimOverlay.AttachRimStatic(mr.gameObject, FXPalette.AttackPrimary, 2.0f, PeakIntensity);   // [79-R20] 공격 이펙트 통일색
                }
                if (overlay == null) return;   // 셰이더 부재/메시 부재 폴백 — 기존 틴트 경로만(29-D3 승계)
                _active++;
                overlay.AddComponent<SilhouetteFadeRunner>().Init(FlashSeconds);
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 방해 금지
        }

        private static void Release()
        {
            _active = Mathf.Max(0, _active - 1);
        }

        /// <summary>[35-S2] 플래시 러너 — 강도 peak → 선형 소멸 → FxRimOverlay.Release(머티리얼+GO 파괴).
        ///  카운트 감산은 OnDestroy 단일 경로(FxImpactLight 선례 — 이중 감산 방지).</summary>
        sealed class SilhouetteFadeRunner : MonoBehaviour
        {
            private float _t;
            private float _duration;

            public void Init(float duration)
            {
                _duration = Mathf.Max(0.02f, duration);
                _t = 0f;
            }

            private void Update()
            {
                _t += Time.unscaledDeltaTime;   // unscaled — 히트스톱 중에도 재생
                if (_t >= _duration) { FxRimOverlay.Release(gameObject); return; }
                FxRimOverlay.SetRimIntensity(gameObject, PeakIntensity * (1f - Mathf.Clamp01(_t / _duration)));
            }

            private void OnDestroy()
            {
                FxHitSilhouette.Release();   // 유한 수명 종료/조기 소멸 공용 — 카운트 정합(단일 감산 경로)
            }
        }
    }
}
