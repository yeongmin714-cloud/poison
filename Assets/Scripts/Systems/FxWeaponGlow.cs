using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 70-U2] 스윙 발화 무기 발광 펄스 — 공격 스윙 시 장착 무기 본체 emissive 펄스(피드백 공백 수리).
    /// 근거: 패망만 무기 플래시 존재([28-P1] PlayerCombat.WeaponFlashPulseRoutine — private 코루틴 재사용 불가 구조).
    /// 계약: 캡처/원복(28-P1 승계 — _EmissionColor 원본 캡처 후 복원), 유한 수명 러너(FxImpactLight 선례 —
    /// 단일 복원 경로), 머티리얼 대입은 47-a 단일 관문(FxMaterialLibrary.TryAssignSharedMaterials) 경유,
    /// 원본 판독은 sharedMaterials 직접 판독(49-T1 게터 금지 승계 — FxDissolveUtility 선례),
    /// EditMode 무동작 가드, 결정론(난수 미사용), 인스턴스 클론 방식 — shared 무손상,
    /// 종료 시 원본 배열 원복+인스턴스 파괴(누출 금지). 색 단일 소스는 호출부(FXPalette 경유) 전달.
    /// [무번호 80-P3] 트레일 층 — 칼날 팁 즉발 잔상(Pulse 동시 발화, 속도 게이트·알파 캡·수명 창 계약 승계).
    /// </summary>
    public static class FxWeaponGlow
    {
        // ── [무번호 80-P3] 트레일 층 — 칼날 팁 즉발 잔상 ──
        // 계획 no80 Task 3.1~3.3: 아크(스윕 도형)와 별개로 무기 본체에 붙는 잔상 — 수명 창 0.12~0.18s.
        // 계약 승계: AttackArcVFX.AlphaCap 0.62(트레일 알파 최대치 — 53~60차), AttackArcVFX.RefTipSpeed(속도 게이트 —
        //   기준 속도 미만 미발화), WeaponSwingTrail 45차 P4 폭 곡선, [26-ST2] 중복 구현 금지(머티리얼 공개 팩토리 위임),
        //   79-R20 색 단일 소스(호출부 전달색 — FXPalette.AttackPrimary), 유한 수명 러너(누출 금지),
        //   EditMode 무동작 가드(Pulse 진입 가드 승계), 결정론(난수 0건).
        internal const float TrailLifetime = 0.18f;      // [80-P3] 방출 창 — 계획 창 0.12~0.18s 상한
        internal const float TrailFadeGrace = 0.18f;     // [80-P3] 페이드 유예 — 트레일 time과 동일(잔상 소멸 보장)
        internal const float TrailWidth = 0.045f;        // [무번호 83 정제] 가는 액센트(0.055→0.045 — 흰 잔상 절제, 세련 톤)
        internal const float TrailMinVertexDistance = 0.04f;
        internal const float TrailAlphaCap = AttackArcVFX.AlphaCap;   // [80-P3] 알파 캡 0.62 승계
        internal const float TrailAlphaScale = 0.72f;    // [무번호 83 정제] 알파 캡×0.72 ≈ 0.45 — 트레일 잔상 톤 완화(세련 톤)
        /// <summary>무기 비주얼 전체 emissive 펄스 — FireComboSlash의 AttackArcVFX.Swing 호출 지점 인접에서 발화.
        /// visual==null(주먹/미장착)은 조용 스킵. 이펙트 렌더러(Trail/Particle/Line)는 스킵(AnimalAI M1 필터 선례).</summary>
        public static void Pulse(GameObject visual, Color color, float duration)
        {
            if (!Application.isPlaying) return;   // EditMode 무동작 가드(21-K1 선례)
            if (visual == null || duration <= 0f) return;   // 주먹/미장착 — 조용 스킵
            try
            {
                TrailBurst(color);   // [80-P3] 트레일 층 동시 발화 — 발광 펄스와 독립(펄스 실패와 무관), 신규 키/파라미터 없음
                var pairs = new List<GlowPair>();
                var instances = new List<Material>();   // 유한 수명 — 종료 시 파괴
                foreach (var rend in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (rend == null) continue;
                    if (rend is TrailRenderer || rend is ParticleSystemRenderer || rend is LineRenderer)
                        continue;   // 이펙트 렌더러 스킵 — AnimalAI M1 필터 선례
                    Material[] shared;
                    try { shared = rend.sharedMaterials; } catch (System.Exception) { continue; }   // sharedMaterials 직접 판독(무인스턴스화)
                    if (shared == null || shared.Length == 0) continue;

                    var instArr = new Material[shared.Length];
                    bool any = false;
                    for (int i = 0; i < shared.Length; i++)
                    {
                        var m = shared[i];
                        if (m == null || !m.HasProperty("_EmissionColor")) { instArr[i] = m; continue; }
                        var inst = Object.Instantiate(m);   // 인스턴스 클론 — shared 무손상
                        inst.EnableKeyword("_EMISSION");
                        inst.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f));   // 펄스 색
                        instArr[i] = inst;
                        instances.Add(inst);
                        any = true;
                    }
                    if (!any) continue;
                    // 47-a 단일 관문 — 실패 시 가드 내부에서 원본 배열 복원됨.
                    if (FxMaterialLibrary.TryAssignSharedMaterials(rend, instArr, "FxWeaponGlow"))
                        pairs.Add(new GlowPair(rend, shared));
                    else
                    {
                        foreach (var im in instArr)
                            if (im != null && instances.Remove(im)) Object.Destroy(im);   // 미소유 인스턴스 누출 금지(47-b 선례)
                    }
                }
                if (pairs.Count == 0) return;

                var go = new GameObject("FxWeaponGlowRunner");
                go.AddComponent<WeaponGlowRunner>().Init(pairs, instances, duration);
            }
            catch (System.Exception) { }   // 시각 보조 — 전투 흐름 절대 방해 금지(FxImpactLight 선례)
        }

        /// <summary>렌더러별 원본 배열 보관 — 종료 시 원복 기준(shared에서 캡처, 무인스턴스화).</summary>
        private readonly struct GlowPair
        {
            public readonly Renderer Rend;
            public readonly Material[] Originals;
            public GlowPair(Renderer rend, Material[] originals) { Rend = rend; Originals = originals; }
        }

        /// <summary>유한 수명 러너 — duration 경과 후 원본 배열 원복+인스턴스 파괴+자가 파괴.
        /// 복원은 Restore 단일 경로(Update 종료 + 조기 파괴/씬 전환 모두 커버 — FxImpactLight 감산 선례).</summary>
        sealed class WeaponGlowRunner : MonoBehaviour
        {
            private List<GlowPair> _pairs;
            private List<Material> _instances;
            private float _duration;
            private float _t;
            private bool _restored;

            public void Init(List<GlowPair> pairs, List<Material> instances, float duration)
            {
                _pairs = pairs;
                _instances = instances;
                _duration = Mathf.Max(0.05f, duration);
                _t = 0f;
                _restored = false;
            }

            private void Update()
            {
                _t += Time.unscaledDeltaTime;   // unscaled — 히트스톱 중에도 원복 보장
                if (_t >= _duration) { Restore(); Destroy(gameObject); }
            }

            private void Restore()
            {
                if (_restored) return;
                _restored = true;
                if (_pairs != null)
                {
                    foreach (var p in _pairs)
                    {
                        if (p.Rend == null) continue;   // 파괴된 렌더러 — 무해 스킵(인스턴스는 아래에서 파괴)
                        FxMaterialLibrary.TryAssignSharedMaterials(p.Rend, p.Originals, "FxWeaponGlowRestore");   // 원본 배열 원복(47-a 관문)
                    }
                    _pairs = null;
                }
                if (_instances != null)
                {
                    foreach (var m in _instances) if (m != null) Object.Destroy(m);   // 인스턴스 파괴(누출 금지)
                    _instances = null;
                }
            }

            private void OnDestroy() { Restore(); }   // 조기 파괴 공용 — 단일 복원 경로
        }

        /// <summary>[무번호 80-P3] 트레일 층 발화 — Pulse와 같은 발화점(신규 키/파라미터 없음 — Task 3.3).
        ///   팁 앵커(WeaponSwingTrail.GetTrailTipAnchor — 손 본 추적 공짜 승계)에 트레일 GO를 자식 부착.
        ///   Anchor 부재(미장착 엣지)는 조용 스킵. 호출부 예외 가드(Pulse try) 내에서만 발화.</summary>
        private static void TrailBurst(Color color)
        {
            GameObject tip = WeaponSwingTrail.GetTrailTipAnchor();
            if (tip == null) return;   // 팁 앵커 부재 — 조용 스킵(AttackArcVFX 호스트 선례)
            var go = new GameObject("FxWeaponTrailBurst");
            go.AddComponent<WeaponTrailBurstRunner>().Init(tip, color);
        }

        /// <summary>[무번호 80-P3] 트레일 버스트 러너 — 팁 앵커 자식 트레일 GO(손 본 추적 공짜 동기),
        ///   속도 게이트(AttackArcVFX.RefTipSpeed 미만 미발화 — 속도비례 계약 승계)로 방출,
        ///   방출 창(TrailLifetime) 경과 후 방출 중단 → 페이드 유예(TrailFadeGrace) 후 트레일 GO+러너 자가 파괴
        ///   (유한 수명 — 누출 금지). 속도 측정 = AttackArcVFX.WriteSample 동일 기준(직전 프레임 위치 거리/dt — 결정론).
        ///   unscaled 수명 시계(히트스톱 중에도 정리 보장 — WeaponGlowRunner 선례) + scaled dt 속도
        ///   (히트스톱에서 dist/dt가 함께 스케일되어 게이트가 게임 시간축 속도를 그대로 본다).</summary>
        private sealed class WeaponTrailBurstRunner : MonoBehaviour
        {
            private GameObject _trailGo;
            private TrailRenderer _trail;
            private Vector3 _lastPos;
            private bool _hasLast;
            private float _t;
            private bool _emitDone;

            public void Init(GameObject tip, Color color)
            {
                // 기존 버스트 트레일 교체 — 동일 앵커 중첩 방지(레이어 1개 유지 — 연속 콤보 누적 금지)
                Transform old = tip.transform.Find("FxWeaponTrail");
                if (old != null) Object.Destroy(old.gameObject);
                _trailGo = new GameObject("FxWeaponTrail");
                _trailGo.transform.SetParent(tip.transform, false);   // 팁 앵커 자식 — 손 본 추적 공짜 동기
                _trail = _trailGo.AddComponent<TrailRenderer>();
                _trail.time = TrailLifetime;
                _trail.widthMultiplier = TrailWidth;
                var width = new AnimationCurve();
                width.AddKey(0f, 1f); width.AddKey(0.6f, 0.65f); width.AddKey(1f, 0f);   // BOTW 테이퍼(45차 P4 곡선 승계)
                _trail.widthCurve = width;
                _trail.startColor = new Color(color.r, color.g, color.b, TrailAlphaCap * TrailAlphaScale);   // [83 정제] 캡×0.72 ≈ 0.45 — 절제된 잔상 톤
                _trail.endColor = new Color(color.r, color.g, color.b, 0f);
                _trail.numCapVertices = 4;
                _trail.alignment = LineAlignment.View;   // 카메라 빌보드(WeaponSwingTrail 계약 승계)
                _trail.minVertexDistance = TrailMinVertexDistance;
                _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _trail.emitting = false;   // 게이트 통과 시에만 방출
                _trail.sharedMaterial = GetTrailMaterial();
            }

            private static Material _trailMat;   // 세션 캐시(48-T3 머티리얼 세션 캐시 선례) — 러너마다 할당 금지
            private static Material GetTrailMaterial()
            {
                if (_trailMat != null) return _trailMat;
                _trailMat = WeaponSwingTrail.CreateTrailMaterialShared(1f);   // [26-ST2] 중복 구현 금지 — 공개 팩토리 위임
                return _trailMat;
            }

            private void LateUpdate()
            {
                if (_trailGo == null) { Destroy(gameObject); return; }   // 팁 앵커 파괴(재장착) — 버스트 조기 종료
                _t += Time.unscaledDeltaTime;   // unscaled 수명 — 히트스톱 중에도 정리 보장
                float dt = Time.deltaTime;   // scaled — 게임 시간축 속도(히트스톱에서 dist/dt가 함께 스케일 → 게이트 불변)
                Vector3 p = _trailGo.transform.position;
                float speed = _hasLast && dt > 0f ? Vector3.Distance(_lastPos, p) / dt : 0f;
                _lastPos = p; _hasLast = true;
                if (!_emitDone) _trail.emitting = speed >= AttackArcVFX.RefTipSpeed;   // [80-P3] 속도 게이트 — 기준 속도 미만 미발화
                if (_t >= TrailLifetime) { _emitDone = true; _trail.emitting = false; }
                if (_t >= TrailLifetime + TrailFadeGrace)
                {
                    Object.Destroy(_trailGo);
                    Destroy(gameObject);   // 유한 수명 — 누출 금지
                }
            }

            private void OnDestroy()
            {
                if (_trailGo != null) Object.Destroy(_trailGo);   // 조기 파괴(씬 전환) 시에도 트레일 정리
            }
        }
    }
}
