using UnityEngine;
using System.Collections.Generic;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// G2-05: 전투 VFX 컨트롤러 (정적 클래스, 간소화).
    /// 히트플래시, 데미지 폰트, 스파크, 블러드 스플래터.
    /// G2-06 HighSpec: 파편(디브리) 버스트, 크리티컬 버스트, 파티클 부스트.
    /// 저사양(Balanced) 경로는 기존 동작 그대로 유지 — 강화는 모두 ActionFeel.HighSpec 게이트.
    /// 2026-09-13(45차 P1): 런타임 파티클 6종 전부 FXPalette.ApplyTo 머티리얼 정규화 —
    /// 머티리얼 미지정 기본 셰이더(마젠타 RGB (219,19,219) 원인) 제거. 색은 BOTW 팔레트 통일.
    /// </summary>
    public static class CombatVFXController
    {
        // ================================================================
        // 1. 히트플래시 — 피격 시 0.1초간 모델 하얗게
        // ================================================================
        public static void PlayHitFlash(GameObject target)
        {
            if (target == null) return;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var mats = new System.Collections.Generic.List<Material>();
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                var m = r.sharedMaterial;
                // 47차 후속5: ShaderGraph 재질(_Color 부재) 가드 — 콘솔 에러 스팸 제거.
                if (m == null || !m.HasProperty("_Color")) continue;
                // [2026-09-15 Phase F-FLASH] 공유 재질은 1회만 begin(중복 카운트 방지)
                if (mats.Contains(m)) continue;
                mats.Add(m);
                FlashMaterialBegin(m);
            }
            if (mats.Count == 0) return;

            var go = new GameObject("HitFlashRunner");
            go.AddComponent<HitFlashRunner>().Init(renderers, mats);
        }

        // ================================================================
        // [2026-09-15 Phase F-FLASH] 히트플래시 재질 레지스트리
        //   뿌리: 기존 구현은 r.sharedMaterial.color 를 직접 흰색으로 바꾸고 '플래시별' 색 스냅샷으로 복원했다.
        //   공유 재질을 쓰는 몬스터가 여럿이거나 연속/중첩 타격이면 스냅샷이 이미 흰색이 되어
        //   복원해도 흰색이 남는 레이스가 발생(테스트19 "흰색에서 안 돌아옴").
        //   수정: 재질별 '진짜 원본 색/이미션'을 최초 1회만 기록 + refcount 로 동시 플래시를 세고,
        //         마지막 플래시가 끝날 때만 원본으로 복원한다. 대상이 파괴돼도 재질 키 기반이라 복원 성공.
        // ================================================================
        private static readonly System.Collections.Generic.Dictionary<Material, Color> _flashOrigColor = new System.Collections.Generic.Dictionary<Material, Color>();
        private static readonly System.Collections.Generic.Dictionary<Material, int> _flashRef = new System.Collections.Generic.Dictionary<Material, int>();
        private static readonly System.Collections.Generic.Dictionary<Material, Color> _flashOrigEmission = new System.Collections.Generic.Dictionary<Material, Color>();

        private static void FlashMaterialBegin(Material m)
        {
            if (m == null || !m.HasProperty("_Color")) return;
            _flashRef.TryGetValue(m, out int c);
            if (c == 0)
            {
                _flashOrigColor[m] = m.color;
                if (m.HasProperty("_EmissionColor"))
                {
                    _flashOrigEmission[m] = m.GetColor("_EmissionColor");
                    m.EnableKeyword("_EMISSION");
                }
                m.color = Color.white;
            }
            _flashRef[m] = c + 1;
        }

        private static void FlashMaterialEnd(Material m)
        {
            if (m == null) return;
            if (!_flashRef.TryGetValue(m, out int c)) return;
            c--;
            if (c > 0) { _flashRef[m] = c; return; }
            _flashRef.Remove(m);
            // 원본 복원(반드시 — 흰색 잔존 금지)
            if (_flashOrigColor.TryGetValue(m, out Color oc)) { m.color = oc; _flashOrigColor.Remove(m); }
            if (_flashOrigEmission.TryGetValue(m, out Color oe))
            {
                m.SetColor("_EmissionColor", oe);
                _flashOrigEmission.Remove(m);
                if (oe.maxColorComponent <= 0.001f) m.DisableKeyword("_EMISSION");
            }
            else if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", Color.black);
                m.DisableKeyword("_EMISSION");
            }
        }

        // ================================================================
        // 2. 데미지 폰트 — OnGUI WorldToScreenPoint, 1.5초 Fade Out
        //    데미지 숫자 실패가 전투(사망 판정/전리품)를 절대 방해하지 않도록
        //    전체 try-catch 격리 + 스팸 가드 경고(1초 쿨다운).
        // [2026-09-15 Phase A] 데미지 타입별 색상/애니메이션 추가
        // ================================================================
        private static float _lastVfxWarningTime = -999f;

        private static void LogVfxWarningOnce(string message)
        {
            if (Time.realtimeSinceStartup - _lastVfxWarningTime < 1f) return;
            _lastVfxWarningTime = Time.realtimeSinceStartup;
            Debug.LogWarning($"[CombatVFXController] {message}");
        }

        // 데미지 숫자 타입 (public으로 노출하여 ShowDamageNumber에서 사용)
        public enum DamageNumberType
        {
            Normal,      // 일반 데미지 (흰)
            Critical,    // 치명타 (골드)
            BackAttack,  // 백어택 (주황)
            Heal,        // 치유 (초록)
            Mana,        // 마나/리소스 (파랑)
        }

        public static void ShowDamageNumber(Vector3 worldPos, int damage, Color color, DamageNumberType type = DamageNumberType.Normal)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("DamageNumber");
                go.transform.position = worldPos;
                go.AddComponent<DamageNumberRunner>().Init(damage.ToString(), color, type);
            }
            catch (System.Exception ex)
            {
                // 스팸 가드(1초 쿨다운) 경고 1회 후 조용히 반환 — 전투 흐름은 계속된다.
                LogVfxWarningOnce($"데미지 숫자 생성 실패(무시): {ex.GetType().Name}: {ex.Message}");
                if (go != null) Object.Destroy(go);
            }
        }

        // ================================================================
        // 3. 타격 파티클 Sparks — 12개 직선 스파크 버스트
        //    [2026-09-13 스파크 개선] 느슨한 사각 점(노랑 10개) → 날카로운 직선 스파크.
        //    골드화이트(1,0.9,0.5) + 스트레치 렌더(velocityScale 0.2)로 속도 방향
        //    선형 스파크 렌더 — BOTW식 밝은 코어 임팩트 스타일.
        //    머티리얼은 FXPalette.ParticleMaterial 정규화 적용(45차 P1) — 기본 머티리얼
        //    방치(URP 미지원 → 마젠타 원인)를 제거한다.
        // ================================================================
        public static void SpawnHitSparks(Vector3 position)
        {
            var go = new GameObject("HitSparks", typeof(ParticleSystem));
            go.transform.position = position;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = FXPalette.Accent; // 45차 P2: 골드화이트(팔레트 Accent) — 순수 노랑 제거

            // 타격 지점 밀집 발화 — 느슨하게 퍼지던 기본 Shape 반경을 조여 직선 스파크 강조
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            // 스트레치 렌더 — 파티클을 속도 방향으로 늘려 사각 점이 아닌 직선 스파크
            var rnd = go.GetComponent<ParticleSystemRenderer>();
            if (rnd != null)
            {
                rnd.renderMode = ParticleSystemRenderMode.Stretch;
                rnd.velocityScale = 0.2f;
                FXPalette.ApplyTo(rnd); // 45차 P1: 머티리얼 정규화 — 마젠타 방지
            }
            ps.Emit(12);

            // G2-06 HighSpec: 파티클 2배(+10) + 미묘한 상향 바이어스. 수치 기존 유지, 스타일은
            // 위 main 설정(골드화이트 스트레치 스파크)을 그대로 상속한다.
            // 저사양(Balanced) 경로는 위 Emit(12) 그대로 유지된다.
            if (ActionFeel.HighSpec)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 vel = Random.insideUnitSphere.normalized * Random.Range(2f, 5f);
                    vel.y = Mathf.Abs(vel.y) * 0.5f + 0.5f; // 상향 바이어스
                    var ep = new ParticleSystem.EmitParams { velocity = vel };
                    ps.Emit(ep, 1);
                }
            }

            Object.Destroy(go, 0.6f);
        }

        // ================================================================
        // 4. 블러드 스플래터 — 3개 파티클, 방향 적용 (색: 팔레트 Blood 붉은색)
        // ================================================================
        public static void SpawnBloodSplatter(Vector3 position, Vector3 direction)
        {
            var go = new GameObject("BloodSplatter", typeof(ParticleSystem));
            go.transform.position = position;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = FXPalette.Blood; // 45차 P2: 순수 red → 팔레트 Blood

            // 45차 P1: 머티리얼 정규화 — 기본 머티리얼 방치(마젠타 원인) 제거
            FXPalette.ApplyTo(go.GetComponent<ParticleSystemRenderer>());

            Vector3 vel = direction.normalized * 2.5f;
            var emitParams = new ParticleSystem.EmitParams
            {
                velocity = vel,
                applyShapeToPosition = true
            };
            ps.Emit(emitParams, 3);

            // G2-06 HighSpec: 파티클 증가(3→8) + 미묘한 상향 바이어스.
            // 저사양(Balanced) 경로는 위 Emit(emitParams, 3) 그대로 유지된다.
            if (ActionFeel.HighSpec)
            {
                for (int i = 0; i < 5; i++)
                {
                    Vector3 boostVel = direction.normalized * Random.Range(0.5f, 2.5f)
                                     + Random.insideUnitSphere * 0.8f;
                    boostVel.y = Mathf.Abs(boostVel.y) * 0.5f + 0.4f; // 상향 바이어스
                    var ep = new ParticleSystem.EmitParams { velocity = boostVel };
                    ps.Emit(ep, 1);
                }
            }

            Object.Destroy(go, 1f);
        }

        // ================================================================
        // 5. 암살 VFX — 흰색 섬광(팔레트 Core) + 블러드 스플래터(팔레트 Blood)
        // ================================================================
        public static void PlayAssassinationVFX(Vector3 position)
        {
            // 흰색 섬광 (ParticleSystem) — 45차 P2: 붉은 섬광 → Core(흰) 섬광
            var flashGo = new GameObject("AssassinationFlash", typeof(ParticleSystem));
            flashGo.transform.position = position + Vector3.up * 0.5f;
            var flashPs = flashGo.GetComponent<ParticleSystem>();
            var flashMain = flashPs.main;
            flashMain.startLifetime = 0.3f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            flashMain.startColor = FXPalette.Core;
            flashPs.Emit(5);
            // 45차 P1: 머티리얼 정규화 — 마젠타 방지
            FXPalette.ApplyTo(flashGo.GetComponent<ParticleSystemRenderer>());
            Object.Destroy(flashGo, 0.4f);

            // 블러드 스플래터 (확산)
            var bloodGo = new GameObject("AssassinationBlood", typeof(ParticleSystem));
            bloodGo.transform.position = position;
            var bloodPs = bloodGo.GetComponent<ParticleSystem>();
            var bloodMain = bloodPs.main;
            bloodMain.startLifetime = 0.6f;
            bloodMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            bloodMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            bloodMain.startColor = FXPalette.Blood; // 45차 P2: 순수 red → 팔레트 Blood
            for (int i = 0; i < 3; i++)
            {
                Vector3 vel = Random.insideUnitSphere.normalized * 2.5f;
                vel.y = Mathf.Abs(vel.y) * 0.5f;
                var emitParams = new ParticleSystem.EmitParams { velocity = vel };
                bloodPs.Emit(emitParams, 3);
            }
            // 45차 P1: 머티리얼 정규화 — 마젠타 방지
            FXPalette.ApplyTo(bloodGo.GetComponent<ParticleSystemRenderer>());
            Object.Destroy(bloodGo, 0.8f);

            Debug.Log($"[CombatVFXController] Assassination VFX at {position}");
        }

        // ================================================================
        // 6. 파편 조각(디브리) 버스트 — 갈색/회색 샤드 + 밝은 플렉 (HighSpec 전용)
        //    Balanced 모드에서는 즉시 반환 (저사양 스폰 없음, 기존 동작 불변)
        //    [45차 P1] 색은 갈색/회색 그대로 유지, 머티리얼만 FXPalette 정규화 적용.
        // ================================================================
        public static void SpawnHitDebris(Vector3 position, Vector3 direction, bool isCrit)
        {
            if (!ActionFeel.HighSpec) return; // HighSpec 전용 — 저사양(Balanced)은 스폰하지 않는다

            int count = isCrit ? 14 : 8;

            var go = new GameObject("HitDebris", typeof(ParticleSystem));
            go.transform.position = position;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            // 갈색/회색 파편 — Two-Color 랜덤 (다층 파편 레이어)
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.45f, 0.32f, 0.18f, 1f),   // 갈색
                new Color(0.55f, 0.55f, 0.55f, 1f));  // 회색

            // 파편 샤드: 타격 방향 + 구면 산란, 하향(-Y) 중력 경향
            for (int i = 0; i < count; i++)
            {
                Vector3 vel = direction.normalized * Random.Range(2f, 4f)
                            + Random.insideUnitSphere * Random.Range(2f, 4f);
                vel.y -= Random.Range(1f, 3f);
                var ep = new ParticleSystem.EmitParams { velocity = vel };
                ps.Emit(ep, 1);
            }

            // 밝은 스파크 플렉 2개(크리티컬 3개)를 파편 사이에 섞기
            int flecks = isCrit ? 3 : 2;
            for (int i = 0; i < flecks; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    velocity = Random.insideUnitSphere.normalized * Random.Range(3f, 6f),
                    startColor = FXPalette.Accent, // 45차 P2: 골드 플렉(팔레트 Accent, 값 동일)
                    startSize = Random.Range(0.04f, 0.08f)
                };
                ps.Emit(ep, 1);
            }

            // 45차 P1: 머티리얼 정규화 — 기본 머티리얼 방치(마젠타 원인) 제거
            FXPalette.ApplyTo(go.GetComponent<ParticleSystemRenderer>());

            Object.Destroy(go, 1.2f);
        }

        // ================================================================
        // 7. 크리티컬 버스트 — 화이트 코어 플래시 + 주황 직선 스파크 (HighSpec 전용)
        //    [2026-09-13 크리 버스트 절제] 붉은 플래시(1,0.2,0.2)+블롭 → BOTW식 절제 스타일:
        //    화이트 코어 플래시(1,0.95,0.8, 속도 0, 0.15s 즉시 소멸) + 주황 외곽(1,0.55,0.2)
        //    직선 스파크 12개. 파편은 기존 SpawnHitDebris(갈색/회색)가 담당 — 여기서 중복 스폰하지 않는다.
        //    [45차 P2] 플래시=팔레트 Core(흰), 외곽 스파크=팔레트 Edge(주황).
        // ================================================================
        public static void SpawnCritBurst(Vector3 position)
        {
            if (!ActionFeel.HighSpec) return; // HighSpec 전용 — 저사양(Balanced)은 스폰하지 않는다

            var go = new GameObject("CritBurst", typeof(ParticleSystem));
            go.transform.position = position;
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = FXPalette.Edge; // 45차 P2: 주황 외곽 스파크(팔레트 Edge)
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 타격 지점 밀집 발화 + 스트레치 렌더 — SpawnHitSparks와 동일 직선 스파크 스타일
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var rnd = go.GetComponent<ParticleSystemRenderer>();
            if (rnd != null)
            {
                rnd.renderMode = ParticleSystemRenderMode.Stretch;
                rnd.velocityScale = 0.2f;
                FXPalette.ApplyTo(rnd); // 45차 P1: 머티리얼 정규화 — 마젠타 방지
            }

            // 레이어 1: 화이트 코어 플래시 — 속도 0, 0.15s 즉시 소멸(1-2프레임 섬광)
            for (int i = 0; i < 3; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    velocity = Vector3.zero,
                    startColor = FXPalette.Core, // 45차 P2: 화이트 코어 플래시(팔레트 Core)
                    startLifetime = 0.15f,
                    startSize = Random.Range(0.6f, 1.0f)
                };
                ps.Emit(ep, 1);
            }

            // 레이어 2: 주황 외곽 직선 스파크 12개 — 수평 링 + 살짝 위로 번짐
            const int sparkCount = 12;
            for (int i = 0; i < sparkCount; i++)
            {
                float angle = (360f / sparkCount) * i * Mathf.Deg2Rad + Random.Range(-0.15f, 0.15f);
                Vector3 vel = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(6f, 9f);
                vel.y = Random.Range(0.5f, 1.5f); // 링이 살짝 위로 번지게
                var ep = new ParticleSystem.EmitParams
                {
                    velocity = vel,
                    startLifetime = Random.Range(0.15f, 0.25f),
                    startSize = Random.Range(0.03f, 0.06f)
                };
                ps.Emit(ep, 1);
            }

            Object.Destroy(go, 0.5f);
        }

        // ================================================================
        // 내부 Runner: HitFlash 복원 + 아웃라인
        // [2026-09-15 Phase A] 히트 플래시 + 아웃라인(흰/금 테두리 0.15초)
        // ================================================================
        private class HitFlashRunner : MonoBehaviour
        {
            private Renderer[] _renderers;
            private System.Collections.Generic.List<Material> _mats;
            private Dictionary<Renderer, Vector3> _originalScales;
            private float _elapsed;
            private bool _restored;

            public void Init(Renderer[] renderers, System.Collections.Generic.List<Material> mats)
            {
                _renderers = renderers;
                _mats = mats;
                _elapsed = 0f;
                _restored = false;

                // [Phase A] 아웃라인용 원본 스케일 저장
                _originalScales = new Dictionary<Renderer, Vector3>(renderers.Length);
                foreach (Renderer r in renderers)
                {
                    if (r != null)
                        _originalScales[r] = r.transform.localScale;
                }
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float duration = 0.15f; // 아웃라인/플래시 지속시간 0.15초

                if (_elapsed < duration)
                {
                    // [Phase A] 아웃라인 효과: 스케일 1.05배 + 이미션(발광) 추가
                    float t = _elapsed / duration;
                    float scaleMultiplier = Mathf.Lerp(1.05f, 1f, t); // 1.05배 → 1.0배
                    float emissionIntensity = Mathf.Lerp(1.5f, 0f, t); // 발광 1.5 → 0

                    foreach (Renderer r in _renderers)
                    {
                        if (r == null) continue;

                        // 스케일로 아웃라인 효과 (약간 부풀리기)
                        if (_originalScales.TryGetValue(r, out Vector3 origScale))
                        {
                            r.transform.localScale = origScale * scaleMultiplier;
                        }
                    }

                    // [Phase F-FLASH] 이미션 글로우 — 재질 레지스트리 경유(직접 원본 훼손 금지)
                    if (_mats != null)
                    {
                        foreach (var m in _mats)
                        {
                            if (m == null || !m.HasProperty("_EmissionColor")) continue;
                            m.SetColor("_EmissionColor", Color.white * emissionIntensity);
                        }
                    }
                }

                if (_elapsed >= duration && !_restored)
                {
                    Restore();
                    _restored = true;
                    Destroy(gameObject);
                }
            }

            private void Restore()
            {
                // [Phase F-FLASH] 재질 복원은 refcount 레지스트리에 위임 — 반드시 원본으로 돌아온다.
                if (_mats != null)
                {
                    foreach (var m in _mats) FlashMaterialEnd(m);
                }

                // [Phase A] 아웃라인 복원: 스케일 원복
                if (_renderers != null && _originalScales != null)
                {
                    foreach (Renderer r in _renderers)
                    {
                        if (r == null) continue;
                        if (_originalScales.TryGetValue(r, out Vector3 origScale))
                            r.transform.localScale = origScale;
                    }
                }
            }

            private void OnDestroy()
            {
                if (!_restored) Restore();
                _restored = true;
            }
        }

        // ================================================================
        // 내부 Runner: IMGUI 데미지 폰트 (1.5초 Fade Out + Pop Animation + 색상 코딩)
        // [2026-09-15 Phase A] 팝 애니메이션(스케일 바운스 1.35→1.0) + 데미지 타입별 색상:
        //   일반=흰(Core), 크리티컬=골드(Accent), 백어택=주황(Edge), 치유=초록, 마나=파랑
        // ================================================================
        private class DamageNumberRunner : MonoBehaviour
        {
            private string _text;
            private Color _color;
            private float _elapsed;
            private Camera _cam;
            private GUIContent _guiContent;
            private float _popScale = 1f;
            private DamageNumberType _type = DamageNumberType.Normal;

            // GUI 스타일 static 캐시 — GUI.skin 접근은 OnGUI 내부에서만 1회 수행.
            // (관례: OnGUI 내 라인당 new 금지 → static 필드 + 최초 1회 생성)
            private static GUIStyle _styleCache;
            private static GUIStyle _shadowStyleCache;
            private static GUIStyle _critStyleCache;

            public void Init(string text, Color color, DamageNumberType type = DamageNumberType.Normal)
            {
                _text = text;
                _color = color;
                _elapsed = 0f;
                _cam = Camera.main;
                _type = type;
                _popScale = 1.35f; // 팝 시작 스케일
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;

                // 팝 애니메이션: 1.35 → 1.0 (0.15초간 ease-out)
                if (_elapsed < 0.15f)
                {
                    _popScale = Mathf.Lerp(1.35f, 1f, _elapsed / 0.15f);
                }
                else
                {
                    _popScale = 1f;
                }

                transform.position += Vector3.up * (1.2f * Time.deltaTime);
                if (_elapsed >= 1.5f)
                    Destroy(gameObject);
            }

            private static float _styleScale = -1f;

            private static void EnsureStyles()
            {
                // [2026-09-15 Phase J] 해상도 비례 + 가독성 상향 — 기존 14/18px 고정은 저해상/고해상에서
                // 읽기 어렵고 초록 지형 대비가 약했다(테스트17 판정). 스케일이 바뀌면 스타일을 재생성한다.
                float s = Mathf.Clamp(Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f)), 0.5f, 2.5f);
                if (_styleCache != null && _shadowStyleCache != null && _critStyleCache != null
                    && Mathf.Approximately(_styleScale, s)) return;
                _styleScale = s;

                _styleCache = new GUIStyle(GUI.skin.label)
                {
                    fontSize = (int)(22 * s),
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };

                _shadowStyleCache = new GUIStyle(_styleCache)
                {
                    normal = { textColor = new Color(0, 0, 0, 0.65f) }
                };

                _critStyleCache = new GUIStyle(_styleCache)
                {
                    fontSize = (int)(32 * s), // 크리티컬은 더 크게
                    fontStyle = FontStyle.Bold
                };
            }

            private void OnGUI()
            {
                if (string.IsNullOrEmpty(_text)) return;

                if (_cam == null)
                {
                    _cam = Camera.main;
                    if (_cam == null) return;
                }

                if (_guiContent == null)
                    _guiContent = new GUIContent(_text);

                if (_styleCache == null || _shadowStyleCache == null || _critStyleCache == null)
                {
                    try { EnsureStyles(); }
                    catch { return; }
                }
                if (_styleCache == null || _shadowStyleCache == null || _critStyleCache == null) return;

                Vector3 screenPos = _cam.WorldToScreenPoint(transform.position);
                if (screenPos.z < 0) return;
                screenPos.y = Screen.height - screenPos.y;

                float alpha = Mathf.Lerp(1f, 0f, _elapsed / 1.5f);

                // 데미지 타입별 스타일 선택
                var style = (_type == DamageNumberType.Critical || _type == DamageNumberType.BackAttack)
                    ? _critStyleCache : _styleCache;

                style.normal.textColor = new Color(_color.r, _color.g, _color.b, alpha);
                _shadowStyleCache.normal.textColor = new Color(0, 0, 0, alpha * 0.5f);

                Vector2 textSize = style.CalcSize(_guiContent);

                // 팝 스케일 적용
                float scaledWidth = textSize.x * _popScale;
                float scaledHeight = textSize.y * _popScale;

                Rect rect = new Rect(
                    screenPos.x - scaledWidth * 0.5f,
                    screenPos.y - scaledHeight * 0.5f,
                    scaledWidth, scaledHeight);

                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), _guiContent, _shadowStyleCache);
                GUI.Label(rect, _guiContent, style);
            }
        }
    // ================================================================
        // 8. 킬 VFX — 슬로우모션 + 화이트 플래시 + XP 팝업 (파티클 버스트 제거)
        //    몬스터 사망 시 호출 (AnimalAI.Die에서)
        // ================================================================
        public static void PlayKillVFX(Vector3 position, int xpGain = 0)
        {
            // ① 슬로우모션 — 0.12초간 timeScale 0.3 → 1.0 복원
            var host = new GameObject("KillSlowmoHost");
            host.AddComponent<KillSlowmoRunner>().Init();

            // ② 화이트 플래시 (전체 화면) — 0.15초간
            var flashGo = new GameObject("KillFlash", typeof(ParticleSystem));
            flashGo.transform.position = Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 1f : position;
            var flashPs = flashGo.GetComponent<ParticleSystem>();
            var flashMain = flashPs.main;
            flashMain.startLifetime = 0.15f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = new ParticleSystem.MinMaxCurve(2f, 3f);
            flashMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.4f),
                new Color(1f, 1f, 1f, 0f)
            );
            flashPs.Emit(3);
            FXPalette.ApplyTo(flashGo.GetComponent<ParticleSystemRenderer>());
            Object.Destroy(flashGo, 0.3f);

            // ③ XP 팝업 (데미지 넘버 스타일) — XP 획득 시만
            if (xpGain > 0)
            {
                ShowDamageNumber(position + Vector3.up * 1.5f, xpGain, new Color(0.3f, 1f, 0.5f, 1f), DamageNumberType.Heal);
            }

            // ④ 카메라 임펄스 (살짝)
            if (CombatCameraEffects.Instance != null)
            {
                CombatCameraEffects.PlayKill();
            }

            Debug.Log($"[CombatVFXController] Kill VFX at {position}, XP={xpGain}");
        }

        // ================================================================
        // 내부 Runner: 킬 슬로우모션
        // ================================================================
        private class KillSlowmoRunner : MonoBehaviour
        {
            private float _timer;
            private const float Duration = 0.12f;
            private const float SlowScale = 0.3f;

            public void Init()
            {
                _timer = 0f;
                Time.timeScale = SlowScale;
                Time.fixedDeltaTime = 0.02f * SlowScale;
            }

            private void Update()
            {
                _timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_timer / Duration);
                // ease-out 복원
                Time.timeScale = Mathf.Lerp(SlowScale, 1f, t * t);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;

                if (_timer >= Duration)
                {
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = 0.02f;
                    Destroy(gameObject);
                }
            }

            private void OnDestroy()
            {
                // 안전 복원
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
            }
        }

        // ================================================================
        // 9. 솔저/NPC 커맨드 경로 마커 — 플레이어가 병사/NPC에게 이동/공격 명령 시
        /// 목표 지점까지 점선 경로 + 방향 화살표 표시 (지속 시간 후 페이드아웃)
        // ================================================================
        public static void ShowCommandPath(Vector3 startPos, Vector3 endPos, Color color, float duration = 3f)
        {
            var go = new GameObject("CommandPathMarker");
            go.transform.position = startPos;
            var marker = go.AddComponent<CommandPathMarker>();
            marker.Init(startPos, endPos, color, duration);
        }

        /// <summary>
        /// 병사/용병/병력 이동 경로 미리보기 — 여러 웨이포인트 지원
        /// </summary>
        public static void ShowCommandPathMulti(Vector3[] waypoints, Color color, float duration = 3f)
        {
            if (waypoints == null || waypoints.Length < 2) return;
            var go = new GameObject("CommandPathMarkerMulti");
            go.transform.position = waypoints[0];
            var marker = go.AddComponent<CommandPathMarker>();
            marker.InitMulti(waypoints, color, duration);
        }

        // ================================================================
        // 내부: 커맨드 경로 마커
        // ================================================================
        private class CommandPathMarker : MonoBehaviour
        {
            private Vector3 _startPos;
            private Vector3 _endPos;
            private Vector3[] _waypoints;
            private Color _color;
            private float _duration;
            private float _elapsed;
            private LineRenderer _line;
            private bool _isMulti;

            private const float LineWidth = 0.12f;
            private const int Segments = 32;
            private const float ArrowSize = 0.5f;

            public void Init(Vector3 start, Vector3 end, Color color, float duration)
            {
                _startPos = start;
                _endPos = end;
                _color = color;
                _duration = duration;
                _elapsed = 0f;
                _isMulti = false;

                SetupLine();
                DrawPath(new Vector3[] { start, end });
            }

            public void InitMulti(Vector3[] waypoints, Color color, float duration)
            {
                _waypoints = waypoints;
                _color = color;
                _duration = duration;
                _elapsed = 0f;
                _isMulti = true;
                _startPos = waypoints[0];
                _endPos = waypoints[waypoints.Length - 1];

                SetupLine();
                DrawPath(waypoints);
            }

            private void SetupLine()
            {
                _line = gameObject.AddComponent<LineRenderer>();
                _line.useWorldSpace = true;
                _line.startWidth = LineWidth;
                _line.endWidth = LineWidth * 0.5f;
                _line.material = CreatePathMaterial(_color);
                _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _line.receiveShadows = false;
                // LineRenderer uses sortingLayerID/sortingOrder instead of renderQueue
                _line.sortingOrder = 100;
            }

            private Material CreatePathMaterial(Color color)
            {
                Shader urpUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                Material mat;
                if (urpUnlit != null)
                {
                    mat = new Material(urpUnlit);
                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                    if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
                    if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.EnableKeyword("_EMISSION");
                }
                else
                {
                    mat = new Material(Shader.Find("Sprites/Default"));
                }
                mat.SetColor("_BaseColor", color);
                mat.SetColor("_EmissionColor", color * 1.5f);
                return mat;
            }

            private void DrawPath(Vector3[] points)
            {
                if (points.Length < 2) return;

                // 곡선 보간으로 부드러운 경로 생성
                List<Vector3> smoothPoints = new List<Vector3>();
                for (int i = 0; i < points.Length - 1; i++)
                {
                    Vector3 p0 = points[i];
                    Vector3 p1 = points[i + 1];
                    for (int s = 0; s < Segments; s++)
                    {
                        float t = (float)s / Segments;
                        // 약간의 높이 오프셋으로 지형 위 표시
                        Vector3 lerped = Vector3.Lerp(p0, p1, t);
                        lerped.y = Mathf.Max(lerped.y + 0.15f, p0.y + 0.1f, p1.y + 0.1f);
                        smoothPoints.Add(lerped);
                    }
                }
                // 마지막 점 추가
                Vector3 last = points[points.Length - 1];
                last.y = Mathf.Max(last.y + 0.15f, last.y + 0.1f);
                smoothPoints.Add(last);

                _line.positionCount = smoothPoints.Count;
                _line.SetPositions(smoothPoints.ToArray());

                // 방향 화살표 (마지막 세그먼트 끝)
                if (points.Length >= 2)
                {
                    AddArrowHead(smoothPoints[smoothPoints.Count - 1],
                        (smoothPoints[smoothPoints.Count - 1] - smoothPoints[smoothPoints.Count - 2]).normalized);
                }
            }

            private void AddArrowHead(Vector3 pos, Vector3 dir)
            {
                // 간단한 화살표 헤드용 추가 라인 렌더러
                var arrowGo = new GameObject("ArrowHead");
                arrowGo.transform.SetParent(transform);
                var arrowLine = arrowGo.AddComponent<LineRenderer>();
                arrowLine.useWorldSpace = true;
                arrowLine.startWidth = LineWidth * 1.5f;
                arrowLine.endWidth = 0f;
                arrowLine.material = _line.material;
                arrowLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arrowLine.receiveShadows = false;

                Vector3 right = Vector3.Cross(dir, Vector3.up).normalized;
                Vector3 up = Vector3.up * 0.3f;
                Vector3 tip = pos;
                Vector3 baseL = pos - dir * ArrowSize + right * ArrowSize * 0.5f + up;
                Vector3 baseR = pos - dir * ArrowSize - right * ArrowSize * 0.5f + up;

                arrowLine.positionCount = 3;
                arrowLine.SetPositions(new Vector3[] { tip, baseL, baseR });
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);

                // 페이드아웃
                float alpha = Mathf.Lerp(1f, 0f, t);
                Color fadedColor = new Color(_color.r, _color.g, _color.b, alpha);
                if (_line != null)
                {
                    _line.startColor = fadedColor;
                    _line.endColor = new Color(fadedColor.r, fadedColor.g, fadedColor.b, alpha * 0.5f);
                    // 매터리얼도 업데이트
                    if (_line.material != null)
                    {
                        _line.material.SetColor("_BaseColor", fadedColor);
                        _line.material.SetColor("_EmissionColor", fadedColor * 1.5f);
                    }
                }

                // 화살표도 페이드
                var arrowLines = GetComponentsInChildren<LineRenderer>();
                foreach (var al in arrowLines)
                {
                    if (al != _line && al.material != null)
                    {
                        al.startColor = fadedColor;
                        al.endColor = fadedColor;
                        al.material.SetColor("_BaseColor", fadedColor);
                        al.material.SetColor("_EmissionColor", fadedColor * 1.5f);
                    }
                }

                if (_elapsed >= _duration)
                {
                    Destroy(gameObject);
                }
            }
        }

    }
}