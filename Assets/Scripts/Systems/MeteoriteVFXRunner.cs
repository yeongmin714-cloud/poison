using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 78-P1] Meteorite VFX 러너 — 강타격 보완 레이어(교체 아님 — MagicHit 룩 무변경 계약).
    /// 임포트 완료 에셋(Assets/Resources/FX/Meteorite/)을 Resources.Load로 지연 로드 + static 캐시,
    /// 강타격 지점에서 4단계 시퀀스(예고 → 지면 임팩트+라이트 플래시 → 2차 파장)를 발화한다.
    ///   - t=0        : 01_Anticipation(작은 예고 — 스케일 0.6, 0.4s 수명)
    ///   - t=0.12s    : 03_GroundImpact 1차(크리 1.5배 위계 — isCrit 전달) + LightFlash 동시 발화
    ///   - t=0.12+0.08: 04_GroundImpactAdds(2차 파장 — 77 리플 뒤)
    ///   - ImpactBuffer.vfx는 P1 후속(예산 계산 미검증)으로 발화 금지 — 리소스 경로/로드 코드 일절 없음(T7 계약).
    /// 패턴 선례: SlashVFXRunner(47차 — .vfx 지연 로드+캐시+런타임 빌드: new GameObject → AddComponent&lt;VisualEffect&gt;
    /// → 에셋 할당 → Reinit → Play), FxSecondaryRipple(무번호 77 — unscaled 지연 스케줄 선례),
    /// FxDistortionWave(동시 상한 정적 카운터 예산 가드 선례).
    /// 계약: 결정론(Random 0건) · unscaled 시간([23-X1] 규약 — 히트스톱 중에도 진행) · LDR 캡(66·68 — 룩 에셋 자체 준수,
    /// 러너는 색/머티리얼 일절 조작 안 함) · 링 예산 공유(ShockwaveRingFX 콘솔 무접촉 — _activeRingCount 미참조) ·
    /// 동시 활성 인스턴스 상한 6 예산 가드(X2 링 예산 계약 준수 — 초과 시 스킵 + 1회 경고).
    /// </summary>
    public static class MeteoriteVFXRunner
    {
        // ── Resources.Load 경로(에셋명에서 확장자 제외 — 부모 임포트 계약) ──
        private const string AnticipationPath     = "FX/Meteorite/VFX/01_Anticipation";
        private const string GroundImpactPath     = "FX/Meteorite/VFX/03_GroundImpact";
        private const string GroundImpactAddsPath = "FX/Meteorite/VFX/04_GroundImpactAdds";
        private const string LightFlashPath       = "FX/Meteorite/VFX/LightFlash";
        // [무번호 78-P1] ImpactBuffer.vfx — P1 후속(예산 계산 미검증)으로 발화 금지(계획 계약).
        //   리소스 경로 문자열/상수/로드 코드 일절 없음 — 정적 검증 T7(MeteoriteVfxP78Tests)이 grep으로 강제.

        // ── 결정론 시간 계수(unscaled — WaitForSecondsRealtime/unscaledDeltaTime 단일 언어) ──
        /// <summary>예고(01_Anticipation) 발화 지연 — t=0(Burst 즉시).</summary>
        public const float AnticipationDelaySec = 0f;
        /// <summary>1차 지면 임팩트(+LightFlash 동시) 발화 지연 — t=0.12s.</summary>
        public const float GroundImpactDelaySec = 0.12f;
        /// <summary>2차 파장(04_GroundImpactAdds) 발화 지연 — t=0.12s+0.08s=0.20s(77 리플 뒤).</summary>
        public const float AddsDelaySec = 0.20f;
        /// <summary>1차→2차 파장 간격(0.08s) — AddsDelaySec - GroundImpactDelaySec 산출 근거 상수.</summary>
        public const float AddsGapSec = 0.08f;
        /// <summary>예고(01_Anticipation) 수명 — 0.4s(계획 계약: 작은 예고, 짧은 수명).</summary>
        public const float AnticipationLifetimeSec = 0.4f;
        /// <summary>임팩트 계열(03/04/LightFlash) 수명 — 1.2s(유한 수명 — 잔존 GO 누출 금지).</summary>
        public const float ImpactLifetimeSec = 1.2f;
        /// <summary>예고 고정 스케일 — 0.6(작은 예고, 계획 계약).</summary>
        public const float AnticipationScale = 0.6f;
        /// <summary>크리 위계 배수 — 1.5(76-S3/71-L2 계열 크리 1.5배 위계 정합).</summary>
        public const float CritScaleMultiplier = 1.5f;

        /// <summary>동시 활성 인스턴스 상한 — 6(X2 링 예산 계약 준수. 1 버스트 최대 4 인스턴스 → 1.5 버스트 동시 허용).</summary>
        public const int MaxConcurrentInstances = 6;

        // ── static 캐시/상태(SlashVFXRunner 선례) ──────────────────────
        private static VisualEffectAsset _anticipationAsset;
        private static VisualEffectAsset _groundImpactAsset;
        private static VisualEffectAsset _groundImpactAddsAsset;
        private static VisualEffectAsset _lightFlashAsset;
        private static bool _loadWarned;      // 로드 실패 1회 경고(static bool — 스팸 방지 선례)
        private static int _activeCount;      // 동시 활성 인스턴스 예산(FxDistortionWave 선례 — 단일 소유)
        private static bool _budgetWarned;    // 예산 초과 1회 경고(static bool)

        // ================================================================
        // 공개 진입점
        // ================================================================

        /// <summary>
        /// [무번호 78-P1] 강타격 지점 Meteorite 4단계 시퀀스 발화 — CombatFXGate 강타격 게이트 밴드
        /// (isCrit || damage &gt;= 15f)에서 호출. EditMode 무동작 가드(스폰 0건 — 77 선례), Random 0건.
        /// </summary>
        public static void Burst(Vector3 position, bool isCrit)
        {
            if (!Application.isPlaying) return;   // EditMode 무동작 가드 — 스폰 0건(FxSecondaryRipple.Schedule 선례)

            // 스케줄러 호스트 — HideAndDontSave(하이어라키 비표시 + 씬 잔존 방지 — 71-L2/77 호스트 선례).
            var host = new GameObject("MeteoriteVfxSequencer") { hideFlags = HideFlags.HideAndDontSave };
            host.AddComponent<MeteoriteVfxSequencer>().Init(position, isCrit);
        }

        /// <summary>
        /// [무번호 180] 몬스터 스킬(AoEExplosion)용 임팩트 전용 시퀀스 — 기존 Burst의 임팩트 레이어만 재사용
        /// (03_GroundImpact+LightFlash 동시 → 0.08s 후 04_GroundImpactAdds). 01_Anticipation 미포함(즉시 폭발 — 판정-시각 정합).
        /// 기존 Burst·내부 상수 무수정(additive) — 지연로드+캐시+unscaled+예산 가드 선례 그대로 승계.
        /// </summary>
        public static void PlayImpactSequence(Vector3 position)
        {
            if (!Application.isPlaying) return;   // EditMode 무동작 가드 — 스폰 0건(Burst 선례)

            var host = new GameObject("MonsterSkillImpactSequencer") { hideFlags = HideFlags.HideAndDontSave };
            host.AddComponent<MeteoriteVfxImpactSequencer>().Init(position);
        }

        /// <summary>예산 감산 — 수명 러너 OnDestroy 단일 경로(FxDistortionWave.Release 선례 — 이중 감산 방지).</summary>
        private static void Release() { _activeCount = Mathf.Max(0, _activeCount - 1); }

        /// <summary>
        /// 단일 스테이지 발화 — 예산 가드 → 에셋 지연 로드(캐시) → 런타임 빌드(47차 선례:
        /// new GameObject → AddComponent&lt;VisualEffect&gt; → 에셋 할당 → Reinit → Play) → 유한 수명 부착.
        /// </summary>
        private static void SpawnStage(string resourcePath, Vector3 position, float scale, float lifetimeSec)
        {
            // 예산 가드 — 동시 활성 인스턴스 상한 6 초과 시 스킵 + 1회 경고(X2 링 예산 계약 준수).
            if (_activeCount >= MaxConcurrentInstances)
            {
                if (!_budgetWarned)
                {
                    _budgetWarned = true;
                    Debug.LogWarning($"[MeteoriteVFX] 동시 인스턴스 예산 초과({MaxConcurrentInstances}) — 스킵({resourcePath})");
                }
                return;
            }

            VisualEffectAsset asset = LoadAsset(resourcePath);
            if (asset == null) return;   // 로드 실패 — 로더가 1회 경고, 조용히 반환(전투 흐름 절대 방해 없음)

            _activeCount++;

            // 런타임 빌드 — 프리팹 경유 없이 GO + VisualEffect를 코드로 조립(SlashVFXRunner 47차 선례 그대로).
            // VFXRenderer는 UnityEngine 네이티브가 AddComponent<VisualEffect> 시 자동 부착(명시 AddComponent 불가 — 선례 주석).
            var go = new GameObject("MeteoriteVFX_" + resourcePath) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.localScale = Vector3.one * scale;   // 결정론 스케일 — 크리 1.5배 위계(isCrit 전달 결과)

            var ve = go.AddComponent<VisualEffect>();
            ve.visualEffectAsset = asset;   // 리소스 에셋 직접 할당
            ve.Reinit();                    // 에셋 교체 후 리셋(순서 규약: 할당 → Reinit → Play)
            ve.Play();

            // 유한 수명 — unscaled 카운트다운 후 자가 파괴(OnDestroy에서 예산 감산 단일 경로).
            go.AddComponent<MeteoriteVfxLifetime>().Init(lifetimeSec);
        }

        /// <summary>
        /// 에셋 지연 로드 + static 캐시(SlashVFXRunner 선례). 로드 실패 시 static bool로 1회만 경고.
        /// </summary>
        private static VisualEffectAsset LoadAsset(string resourcePath)
        {
            VisualEffectAsset asset;
            // Unity 오버로드 null 규약 준수(?? 연산자 미사용 — SlashVFXRunner 선례의 if-null 패턴).
            switch (resourcePath)
            {
                case AnticipationPath:
                    if (_anticipationAsset == null) _anticipationAsset = Resources.Load<VisualEffectAsset>(resourcePath);
                    asset = _anticipationAsset;
                    break;
                case GroundImpactPath:
                    if (_groundImpactAsset == null) _groundImpactAsset = Resources.Load<VisualEffectAsset>(resourcePath);
                    asset = _groundImpactAsset;
                    break;
                case GroundImpactAddsPath:
                    if (_groundImpactAddsAsset == null) _groundImpactAddsAsset = Resources.Load<VisualEffectAsset>(resourcePath);
                    asset = _groundImpactAddsAsset;
                    break;
                case LightFlashPath:
                    if (_lightFlashAsset == null) _lightFlashAsset = Resources.Load<VisualEffectAsset>(resourcePath);
                    asset = _lightFlashAsset;
                    break;
                default:
                    asset = Resources.Load<VisualEffectAsset>(resourcePath);
                    break;
            }
            if (asset == null && !_loadWarned)
            {
                _loadWarned = true;
                Debug.LogWarning($"[MeteoriteVFX] Resources 로드 실패: {resourcePath} — .vfx 임포트 상태 확인 필요, 스테이지 스킵(전투 흐름 무영향)");
            }
            return asset;
        }

        /// <summary>
        /// [무번호 78-P1] 4단계 시퀀서 — unscaled 지연(WaitForSecondsRealtime — [23-X1]/77 선례)으로
        /// 예고 → 임팩트+라이트 플래시(동일 프레임) → 2차 파장을 순차 발화 후 자가 파괴(유한 수명 — 누출 금지).
        /// </summary>
        private sealed class MeteoriteVfxSequencer : MonoBehaviour
        {
            private Vector3 _position;
            private bool _isCrit;

            public void Init(Vector3 position, bool isCrit)
            {
                _position = position;
                _isCrit = isCrit;
                StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                // ── t=0: 01_Anticipation — 작은 예고(스케일 0.6, 0.4s 수명) ──
                SpawnStage(AnticipationPath, _position, AnticipationScale, AnticipationLifetimeSec);

                // ── t=0.12s: 03_GroundImpact 1차(크리 1.5배 위계 — isCrit 전달) + LightFlash 동시(라이트 플래시) ──
                yield return new WaitForSecondsRealtime(GroundImpactDelaySec);   // [23-X1] unscaled 규약 — 히트스톱 중에도 진행
                float impactScale = _isCrit ? CritScaleMultiplier : 1f;
                SpawnStage(GroundImpactPath, _position, impactScale, ImpactLifetimeSec);
                SpawnStage(LightFlashPath, _position, impactScale, ImpactLifetimeSec);   // GroundImpact와 동일 프레임(T6 계약)

                // ── t=0.12s+0.08s: 04_GroundImpactAdds — 2차 파장(77 리플 뒤) ──
                yield return new WaitForSecondsRealtime(AddsGapSec);             // 0.08s 추가 대기(결정론 상수)
                SpawnStage(GroundImpactAddsPath, _position, impactScale, ImpactLifetimeSec);

                // [무번호 78-P1] ImpactBuffer.vfx는 P1 후속(예산 계산 미검증)으로 발화 금지 — 본 시퀀스에서 미발화(T7 계약).

                if (Application.isPlaying) Object.Destroy(gameObject);
                else Object.DestroyImmediate(gameObject);                        // EditMode(21-K1 선례 — 방어적 이중 가드)
            }

            private void OnDestroy()
            {
                // 안전망 — 발화 대기 중 조기 파괴(씬 전환 등) 시에도 잔여 발화 차단(77 선례).
                StopAllCoroutines();
            }
        }

        /// <summary>
        /// [무번호 180] 임팩트 전용 시퀀서 — 예고(01_Anticipation) 없이 t=0에 03_GroundImpact+LightFlash 동시 발화 →
        /// 0.08s(AddsGapSec 결정론 상수) 후 04_GroundImpactAdds. unscaled 지연(WaitForSecondsRealtime — [23-X1] 선례),
        /// 비크리 스케일 1 고정, 자가 파괴(유한 수명 — 누출 금지). 예산 가드/캐시는 SpawnStage·LoadAsset 단일 경로 승계.
        /// </summary>
        private sealed class MeteoriteVfxImpactSequencer : MonoBehaviour
        {
            private Vector3 _position;

            public void Init(Vector3 position)
            {
                _position = position;
                StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                // ── t=0: 03_GroundImpact + LightFlash 동시(Burst 임팩트 프레임 재사용 — 비크리 스케일 1) ──
                SpawnStage(GroundImpactPath, _position, 1f, ImpactLifetimeSec);
                SpawnStage(LightFlashPath, _position, 1f, ImpactLifetimeSec);   // GroundImpact와 동일 프레임(T6 계약)

                // ── t=0.08s: 04_GroundImpactAdds — 2차 파장(77 리플 뒤) ──
                yield return new WaitForSecondsRealtime(AddsGapSec);
                SpawnStage(GroundImpactAddsPath, _position, 1f, ImpactLifetimeSec);

                if (Application.isPlaying) Object.Destroy(gameObject);
                else Object.DestroyImmediate(gameObject);                        // EditMode(21-K1 선례 — 방어적 이중 가드)
            }

            private void OnDestroy()
            {
                StopAllCoroutines();   // 안전망 — 발화 대기 중 조기 파괴 시 잔여 발화 차단(77 선례)
            }
        }

        /// <summary>
        /// 스테이지 수명 러너 — unscaled 카운트다운 후 자가 파괴. 예산 감산은 OnDestroy 단일 경로.
        /// </summary>
        private sealed class MeteoriteVfxLifetime : MonoBehaviour
        {
            private float _remainingSec;

            public void Init(float lifetimeSec) { _remainingSec = lifetimeSec; }

            private void Update()
            {
                _remainingSec -= Time.unscaledDeltaTime;   // unscaled — [23-X1] 규약(결정론 시간 언어 단일)
                if (_remainingSec <= 0f) Destroy(gameObject);
            }

            private void OnDestroy()
            {
                MeteoriteVFXRunner.Release();   // 예산 감산 — 단일 경로(FxDistortionWave 선례)
            }
        }
    }
}
