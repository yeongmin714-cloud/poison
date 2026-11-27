using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 73] 환경 반응 VFX — 계획서 docs/plans/2026-11-18_no73-env-reaction-rain-splash-plan.md
    ///  - E2 오브젝트 스윙: 타격 임팩트/폭발 지점 반경 내 잔디·약초 오브젝트가 절차 본 회전으로 흔들린다.
    ///    37-E2 "규모 검토 후" 게이트 이행 — 전용 정점 변조 셰이더는 GLB 소재 머티리얼 스왑 리스크로
    ///    이월(Play 체감 후 후속 판정), 검증된 코드 레버(OverlapSphere 쿼리 + FXMotion 이징 본 회전)로 착지.
    ///  - E3 우천 워터 스플래시: Rain 상태 타격 지점에 물색 소형 입자 1회 발화(25-IN1 날씨 틴트 연동 —
    ///    링 예산 미소비 — 입자 전용 레이어).
    ///  - 결정론 — 난수 경로 0건(25-IN1 계약 준용). EditMode에서 쿼리·스폰 0건(Application.isPlaying 가드).
    /// </summary>
    public static class FxEnvReaction
    {
        // ── [무번호 73-E2] 오브젝트 스윙 상수 ─────────────────────
        private const float SwayRadius = 3f;             // 타격 임팩트 반응 반경(m) — 환경 감지(기존 FX 링 반경 대비 넓음)
        private const float SwayExplosionRadius = 4.5f;  // 폭발 반응 반경(m) — 임팩트 1.5배
        private const int SwayMaxTargets = 4;            // 동시 스윙 상한 — 스팸 임팩트 게이팅(스폰 수 한정 계약 병행)
        private const float SwayDurationSec = 0.6f;      // 딥→원복 총 수명(초)
        private const float SwayMaxAngleDeg = 14f;       // 타격 딥 최대 각(도)
        private const float SwayExplosionAngleDeg = 21f; // 폭발 딥 최대 각(도) — 1.5배
        private const float SwayMinIntervalSec = 0.15f;  // 동일 임팩트 이중 발화 흡수(MIN_SPAWN_INTERVAL 선례)
        private static float _lastSwayTriggerTime = -999f;

        // ── [무번호 73-E3] 우천 워터 스플래시 상수 ────────────────
        private const int SplashParticleCount = 6;        // 스폰 수 한정(게이팅 계약 — PuffParticleCount 선례 병행)
        private const float SplashDespawnSeconds = 0.8f;  // 입자 수명 0.5s + 여유(PuffDespawnSeconds 선례)
        private static Texture2D _splashTex;              // 절차 물방울 텍스처 캐시(1회 생성 — 결정론)
        private static readonly Color SplashWaterColor = new Color(0.65f, 0.9f, 1f, 0.8f); // [무번호 25-IN1] Rain 청백 정합

        /// <summary>[무번호 73-E3] Rain 게이트 — WeatherManager 부재/비Rain = false(25-IN1 판정식 동일 조건 — 안전 폴백).</summary>
        internal static bool IsRainNow()
        {
            var wm = WeatherManager.Instance;
            if (wm == null) return false;
            return wm.CurrentWeather == WeatherManager.WeatherType.Rain;
        }

        /// <summary>[무번호 73-E3] 우천 워터 스플래시 — 임팩트 지점 물색 소형 입자 퍼프(물 튀김).
        /// SpawnMoveTrailPuffCore 선례 그대로(먼지→물색, 미세 부양→낙하 물성) — 링 예산 미소비, 결정론.</summary>
        internal static void SpawnRainSplash(Vector3 groundPos)
        {
            if (!Application.isPlaying) return;   // EditMode — 소스 계약 검증 전용
            if (!IsRainNow()) return;             // [무번호 73-E3] Rain 게이트(25-IN1 연동)

            var go = new GameObject("RainSplash") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = groundPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(SplashWaterColor);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.35f;   // 물은 낙하한다(먼지 미세 부양 -0.06 대비 — 물 튀김 물성)
            main.maxParticles = 16;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;   // 1회 Emit만 — 지속 방출 없음

            // 수명 알파 페이드 — 물방울은 커지지 않음(크기 확산 커브 없음 — 먼지와 물성 차별)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.55f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rnd = go.GetComponent<ParticleSystemRenderer>();
            if (rnd != null)
            {
                var mat = FxMaterialLibrary.CreateSoftParticle("RainSplashMat", SplashWaterColor, EnsureSplashTexture());
                rnd.material = mat;
                rnd.renderMode = ParticleSystemRenderMode.Billboard;
                rnd.alignment = ParticleSystemRenderSpace.View;
                rnd.sortMode = ParticleSystemSortMode.Distance;
            }
            ps.Emit(SplashParticleCount);
            // [무번호 21-K1 수리] DestroyImmediate 가드 선례 — EditMode 입자 시뮬 부재 환경 안전(156차).
            if (Application.isPlaying) Object.Destroy(go, SplashDespawnSeconds);
            else Object.DestroyImmediate(go);
        }

        /// <summary>[무번호 73-E3] 절차 물방울 텍스처 128×128 — 물성이 먼지(구름 노이즈)와 달라 전용 텍스처로 분리
        ///   (기존 먼지 빌더는 private 잠금 — FxShaderAssetsDTests T3 계약, 무접촉). 물방울은 노이즈 없이
        ///   매끈한 방사 소프트 마스크 단일 소스 — 결정론, 사각 금지 계약.</summary>
        internal static Texture2D EnsureSplashTexture()
        {
            if (_splashTex != null) return _splashTex;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.name = "RainSplashTex";
            var px = new Color[S * S];
            float c = S * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                    // 방사 소프트 마스크 — 가장자리 smoothstep 감쇠(사각 금지 계약, 결정론)
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d));
                    px[y * S + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            _splashTex = tex;
            return tex;
        }

        /// <summary>[무번호 73-E2] 타격 임팩트 → 반경 내 잔디/약초 오브젝트 절차 스윙(스윙·타격 임팩트 반응).</summary>
        internal static void TriggerImpactSway(Vector3 impactPos)
        {
            TriggerSway(impactPos, SwayRadius, SwayMaxAngleDeg, true);
        }

        /// <summary>[무번호 73-E2] 폭발 → 반경·강도 확대 스윙(BombExplosionVisual 발화 지점 — 폭발은 저빈도라 쿨다운 미적용).</summary>
        internal static void TriggerExplosionSway(Vector3 pos)
        {
            TriggerSway(pos, SwayExplosionRadius, SwayExplosionAngleDeg, false);
        }

        /// <summary>[무번호 73-E2] 스윙 공통 — OverlapSphere 쿼리(GuardTaskSystem 선례) → 대상 상한 게이팅 → 러너 부착.</summary>
        private static void TriggerSway(Vector3 pos, float radius, float maxAngleDeg, bool gateByCooldown)
        {
            if (!Application.isPlaying) return;   // EditMode — 소스 계약 검증 전용
            if (gateByCooldown && Time.time - _lastSwayTriggerTime < SwayMinIntervalSec) return;
            if (gateByCooldown) _lastSwayTriggerTime = Time.time;

            var hits = Physics.OverlapSphere(pos, radius);
            if (hits == null || hits.Length == 0) return;

            // 대상 판정 — GuardTaskSystem.IsGatherNode 판정식과 동일 조건(중복 정의 금지 — 37-E2 대상 계약).
            var candidates = new List<Collider>();
            foreach (var hit in hits)
                if (hit != null && hit.gameObject != null && GuardTaskSystem.IsGatherNode(hit.gameObject))
                    candidates.Add(hit);
            if (candidates.Count == 0) return;
            // 거리 오름차순(동일 거리 시 instanceID — 결정론 정렬)
            candidates.Sort((a, b) =>
            {
                int d = Vector3.Distance(pos, a.transform.position)
                    .CompareTo(Vector3.Distance(pos, b.transform.position));
                return d != 0 ? d : a.gameObject.GetInstanceID().CompareTo(b.gameObject.GetInstanceID());
            });
            int spawned = 0;
            for (int i = 0; i < candidates.Count && spawned < SwayMaxTargets; i++)
            {
                var t = candidates[i].transform;
                if (t.GetComponent<FxSwayRunner>() != null) continue;   // 스윙 중 오브젝트 재발화 흡수
                var runner = t.gameObject.AddComponent<FxSwayRunner>();
                runner.Init(pos, maxAngleDeg, SwayDurationSec);
                spawned++;
            }
        }
    }

    /// <summary>
    /// [무번호 73-E2] 오브젝트 절차 스윙 러너 — 임팩트에서 밀려나듯 단일 딥(+소형 리바운드) 후 원복.
    /// 절차 본 회전(물리엔진 미사용 — FxDeathRelax W2 선례). 이징은 FXMotion.Progress 재사용
    /// (무번호 28-U3 표준 — 재구현 금지 규약). 유한 수명 — 원복 후 컴포넌트 자가 해제
    /// (예산 가드 FxImpactLight OnDestroy 단일 감산 선례 — 누출 금지).
    /// </summary>
    public class FxSwayRunner : MonoBehaviour
    {
        private Quaternion _baseRotation;
        private Vector3 _axis;
        private float _angleDeg;
        private float _duration;
        private float _t;
        private bool _init;

        public void Init(Vector3 impactPos, float maxAngleDeg, float duration)
        {
            _baseRotation = transform.rotation;   // 월드 회전 기준 — 부모 회전 무관 결정론 원복
            Vector3 dir = transform.position - impactPos;
            dir.y = 0f;
            dir = dir.sqrMagnitude < 0.0001f ? Vector3.forward : dir.normalized;
            // 임팩트에서 밀려나듯 dir 방향으로 넘어가는 축 — 수평 법선(결정론 — up × dir 회전 시 up가 dir로 기움)
            _axis = Vector3.Cross(Vector3.up, dir).normalized;
            if (_axis.sqrMagnitude < 0.0001f) _axis = Vector3.right;
            _angleDeg = maxAngleDeg;
            _duration = Mathf.Max(0.05f, duration);
            _t = 0f;
            _init = true;
        }

        private void Update()
        {
            if (!_init) { Destroy(this); return; }
            _t += Time.deltaTime;
            float p = FXMotion.Progress(_t / _duration);   // [무번호 28-U3] 표준 이징 재사용
            // 단일 딥 + 소형 리바운드 — 사이클 1.25회 sin 감쇠(결정론 상수만)
            float sway = Mathf.Sin(p * Mathf.PI * 1.25f) * (1f - p * 0.35f) * _angleDeg;
            transform.rotation = _baseRotation * Quaternion.AngleAxis(sway, _axis);
            if (_t >= _duration)
            {
                transform.rotation = _baseRotation;   // 종료 원복 보장
                Destroy(this);   // 컴포넌트만 해제 — 오브젝트 보존(리스폰/재발화 대응)
            }
        }

        private void OnDestroy()
        {
            if (transform == null) return;
            transform.rotation = _baseRotation;   // 조기 파괴(씬 전환 등) 원복 보장
        }
    }
}
