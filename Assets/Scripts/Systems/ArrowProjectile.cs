using ProjectName.Core;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// AB-05/06: 화살 발사체.
    /// 중력의 영향을 받는 포물선 궤적으로 날아가며,
    /// 적 충돌 시 데미지를 입힙니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ArrowProjectile : MonoBehaviour
    {
        /// <summary>Shared bow-flight tuning used by firing and trajectory consumers.</summary>
        public const float BaseSpeed = 84f;
        public const float GravityScale = 0.15f;
        public const float FlightLifetime = 10f;
        public static float GetSpeedForPower(float power) => BaseSpeed * (0.7f + 0.5f * Mathf.Clamp01(power));

        private float _damage = 10f;
        private float _lifetime = FlightLifetime;
        private float _elapsed = 0f;
        private Rigidbody _rb;
        private Collider _collider;
        private TrailRenderer _trail;      // [P25-C3] 소형 밝은 트레일
        private bool _stuck = false;    // 명중/지면 꽂힘 시 true — 회전 정렬·충돌 재처리 방지
        private float _wobbleTime = -1f;   // [P22-3] 박힘 직후 미세 진동 타이머
        private Quaternion _stuckRotation;
        /// <summary>[70차 후속19/C6] 발사 파워(0~1) — ArrowManager가 세팅. 파워 풀 명중 시 크리틱 연출.</summary>
        public float _power = 1f;


        /// <summary>[C 고품질] ArrowManager가 Spawn 후 주입 — 3티어 파라미터(관통/발광/스파크). Awake 이후 호출돼도 트레일은 유지.</summary>
        public void SetArrowData(ProjectName.Core.ArrowData data)
        {
            if (data == null) return;
            _arrowData = data;
            if (data.canPierce)
                _pierceRemaining = 1;   // 마법 화살 — 적 1기 추가 관통
            ApplyArrowVisuals();
        }

        /// <summary>[C 고품질] 주입된 티어로 트레일 그래디언트/스파크 트레일을 갱신. Awake에서 기본(일반)으로 생성됐어도
        /// SetArrowData가 실제 티어를 주입하므로 여기서 재색/보강한다.</summary>
        private void ApplyArrowVisuals()
        {
            if (_trail == null) return;
            var strk1 = _arrowData != null ? _arrowData.streakColor : new Color(0.75f, 0.82f, 1f);
            var tgrad = new Gradient();
            tgrad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.99f, 0.97f), 0f), new GradientColorKey(strk1, 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0.5f, 0.15f), new GradientAlphaKey(0.28f, 0.45f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = tgrad;

            // 화살 종류별 2중 스파크/색상 변형 없이 모든 화살을 순백색 단일 네온 트레일로 표시.
            _trail.startColor = Color.white;
            _trail.endColor = Color.white;
        }

        // [C 고품질] 3티어 파라미터(ArrowManager가 주입, Spawn 오버로드 경유)
        private ProjectName.Core.ArrowData _arrowData = ProjectName.Core.ArrowData.Regular;
        private int _pierceRemaining = 0;        // 마법 화살 관통 잔여(적 1기)
        private int _piercedId = -1;             // 이미 관통한 대상 instanceId (중복 재데미지 방지)
        private float _sparkAccum = 0f;          // 강화 화살 스파크 방출 누적(초)


        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            EnsureTrailRenderer();
        }

        private void EnsureTrailRenderer()
        {
            // Awake may not run for objects created by EditMode tooling; keep Spawn visuals complete too.
            if (_trail != null) return;

            // 아주 짧은 순백색 애더티브 네온 잔상. 70~84m/s 기준 약 1.5~1.85m 길이.
            _trail = GetComponent<TrailRenderer>();
            if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.06f;             // [테스트45 P3] 발사~도착점까지 잔상 유지(0.022→0.06)
            _trail.startWidth = 0.07f;        // [테스트45 P3] 흰 큼임 일부 감소 — 연한 Fluent 라인
            _trail.endWidth = 0.003f;         // 꼬리로 극미세 테이퍼
            _trail.minVertexDistance = 0.03f; // 고속에서도 빈틈 없이
            _trail.generateLightingData = false;
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            var trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var tmat = new Material(trailShader != null ? trailShader : Shader.Find("Sprites/Default"));
            // [참조] 흰 스트릭이 화면에서 또렷이 빛나도록 애더티브 블렌드(WeaponSwingTrail 검증 패턴).
            if (tmat.HasProperty("_Surface")) tmat.SetFloat("_Surface", 1f);              // Transparent
            if (tmat.HasProperty("_Blend")) tmat.SetFloat("_Blend", 2f);                  // Additive
            tmat.SetOverrideTag("RenderType", "Transparent");
            if (tmat.HasProperty("_SrcBlend")) tmat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (tmat.HasProperty("_DstBlend")) tmat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (tmat.HasProperty("_ZWrite")) tmat.SetFloat("_ZWrite", 0f);
            tmat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _trail.material = tmat;
            var tgrad = new Gradient();
            tgrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0.5f, 0.15f), new GradientAlphaKey(0.28f, 0.45f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = tgrad;
        }

        /// <summary>화살 발사</summary>
        public static ArrowProjectile Spawn(Vector3 position, Vector3 direction, float speed, float damage, Color trailColor)
        {
            var go = new GameObject("Arrow(Clone)");
            go.transform.position = position;
            Vector3 launchDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            // The capsule's long local +Y axis follows the initial camera-ray velocity.
            go.transform.rotation = Quaternion.LookRotation(launchDirection) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.12f, 0.9f, 0.12f);

            // Retain the former cylinder's capsule physics shape without its procedural mesh.
            var collider = go.AddComponent<CapsuleCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;                 // 중력은 아래 Update에서 공유 설정값으로 수동 적용
            rb.linearVelocity = launchDirection * speed;
            rb.linearDamping = 0f;                 // 비행 중 저항 없음
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;   // [후속19/A4] 프레임 간 보간 — 트레일 끊김 완화

            var arrow = go.AddComponent<ArrowProjectile>();
            arrow._damage = damage;
            arrow.EnsureTrailRenderer();

            // [P20-4 진단] 스폰 회전 vs 조준 방향 정합 1회 실측 — "세워서 나감/방향 다름" 즉별
            float dot = Vector3.Dot(go.transform.up, launchDirection);
            Debug.Log($"[Arrow][P20-4] 스폰 정합 — up·dir={dot:F3}(±1이 정상), dir={launchDirection}");

            // Keep the primitive's capsule collider and rigidbody, but use only the white neon trail as a visual.
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;

            return arrow;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
            }

            // Shared scaled gravity; skip stuck arrows and any gravity-driven projectile.
            if (!_stuck && _rb != null && _rb.useGravity == false)
            {
                _rb.linearVelocity += Physics.gravity * GravityScale * Time.deltaTime;
            }

            // [P22-3] 박힌 직후 미세 진동 — 0.3s 감쇠 흔들림(임팩트 체감), 이후 고정
            if (_wobbleTime >= 0f)
            {
                _wobbleTime += Time.deltaTime;
                if (_wobbleTime < 0.3f)
                {
                    float decay = 1f - _wobbleTime / 0.3f;
                    float wob = Mathf.Sin(_wobbleTime * 40f) * 2.5f * decay;
                    transform.rotation = _stuckRotation * Quaternion.Euler(wob, 0f, 0f);
                }
                else _wobbleTime = -1f;
            }

            // 회전을 속도 방향으로 정렬 (박힌 화살은 유지).
            // The cylinder's local +Y shaft axis is aligned to velocity, matching its launch orientation.
            if (!_stuck && _rb != null && _rb.linearVelocity.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(_rb.linearVelocity.normalized) * Quaternion.Euler(90f, 0f, 0f);
            }
        }

        /// <summary>[P22-3] 지면 먼지 퍼프 — shadow_glow 소프트 텍스처 파티클 6개, 0.5s 감쇠(과장 없음).</summary>
        private void SpawnGroundPuff()
        {
            var soft = Resources.Load<Texture2D>("UI/shadow_glow");
            var go = new GameObject("ArrowGroundPuff");
            go.transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 1.6f;
            main.startSize = 0.5f;
            main.startColor = new Color(0.55f, 0.48f, 0.38f, 0.8f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.12f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
            var tex = soft != null ? soft : Texture2D.whiteTexture;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.mainTexture = tex;
            mat.color = new Color(0.6f, 0.52f, 0.4f, 0.7f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.material = mat;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;

            }
            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            col2.color = grad;
            Object.Destroy(go, 1.2f);
        }

        /// <summary>[P25-C3] 박힘 시 트레일 제거 헬퍼.</summary>
        private void DisableTrail()
        {
            if (_trail != null) _trail.enabled = false;
        }

        /// <summary>[P25-C2] 발사 머즐 퍼프 — 활 위치에서 짧고 작게(0.25s, 6입자).</summary>
        public static void SpawnMuzzlePuff(Vector3 pos)
        {
            var soft = Resources.Load<Texture2D>("UI/shadow_glow");
            var go = new GameObject("BowMuzzlePuff");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.25f;
            main.loop = false;
            main.startLifetime = 0.22f;
            main.startSpeed = 0.6f;
            main.startSize = 0.18f;
            main.startColor = new Color(0.85f, 0.8f, 0.7f, 0.85f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.06f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
            var tex = soft != null ? soft : Texture2D.whiteTexture;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.mainTexture = tex;
            mat.color = new Color(0.9f, 0.85f, 0.75f, 0.8f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.material = mat;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            col2.color = grad;
            Object.Destroy(go, 0.6f);
        }

        /// <summary>[P25-C4] 명중 별 섬광 — StarFlare 텍스처 Quad 빌보드, 0.15s 스케일업+페이드 후 소멸.</summary>
        public static void SpawnStarFlare(Vector3 pos)
        {
            var tex = Resources.Load<Texture2D>("UI/StarFlare");
            if (tex == null) return;
            try
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "ArrowStarFlare";
                go.transform.position = pos + new Vector3(0f, 0.7f, 0f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 탑다운 카메라 기준 수평 빌보드
                go.transform.localScale = Vector3.one * 0.35f;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.mainTexture = tex;
                    mat.color = new Color(1f, 1f, 0.95f, 1f);
                    mr.material = mat;
                }
                go.AddComponent<StarFlareAnim>();
            }
            catch (System.Exception e)
            {
                Debug.Log("[Arrow][P25-C4] 별 섬광 생성 실패: " + e.ToString());
            }
        }

        /// <summary>별 섬광 애니 — 0.15s 스케일업(0.35→1.0) + 0.2s 알파 페이드 후 소멸.</summary>
        private class StarFlareAnim : MonoBehaviour
        {
            private float _t = 0f;
            private Material _mat;
            private void Awake()
            {
                var mr = GetComponent<MeshRenderer>();
                if (mr != null) _mat = mr.material;
            }
            private void Update()
            {
                _t += Time.deltaTime;
                float grow = Mathf.Clamp01(_t / 0.15f);          // 0.15s 스케일업
                transform.localScale = Vector3.one * (0.35f + 0.65f * grow);
                float fadeStart = 0.15f;
                if (_t > fadeStart && _mat != null)
                {
                    float f = Mathf.Clamp01((_t - fadeStart) / 0.2f);  // 0.2s 페이드
                    _mat.color = new Color(1f, 1f, 0.95f, 1f - f);
                }
                if (_t > 0.4f) { Object.Destroy(gameObject); }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 적 감지 — 몬스터/적 병사 + 영주. [TEST21-FOLLOWUP] Guard/DraculaLord 추가.
            // [2026-09-16 69차 후속10] RecruitedSoldier 제외 — 화살이 내 소속 병사를 관통(아군 오인 피해 차단).
            //   관통 처리: 내 병사 히트는 어느 분기에도 걸리지 않아 화살이 계속 비행한다.
            GameObject hitGO = other != null ? other.gameObject : null;
            bool isTarget = hitGO != null
                && (hitGO.CompareTag("Enemy") || hitGO.CompareTag("Monster")
                    || hitGO.CompareTag("Guard")
                    || hitGO.CompareTag("DraculaLord"));
            bool isOwnSoldier = hitGO != null && hitGO.CompareTag("RecruitedSoldier");   // 아군 — 아무 처리 없음(관통)
            if (isTarget)
            {
                // [2026-09-26 젤다 화살예시 방패 막기] 방패를 착용한 적 병사(생존·미포섭)는
                // 화살을 막아낸다 — 무피해 + 막기 VFX(별섬광/확장 링/노랑 스파크) + 화살 소멸.
                // 아군(IsRecruited)은 대상이 아니므로 여기서 제외(기존 관통 유지).
                GuardPlaceholder guardHit = hitGO != null ? hitGO.GetComponentInParent<GuardPlaceholder>() : null;
                if (guardHit != null && guardHit.IsAlive && !guardHit.IsRecruited && guardHit.ShieldItem != null)
                {
                    Vector3 blockPoint = other != null ? other.ClosestPoint(transform.position) : transform.position;
                    ArrowShieldBlockFX.Play(blockPoint);
                    DisableTrail();
                    Destroy(gameObject);
                    return;
                }

                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    Vector3 hitDir = (other.transform.position - transform.position).normalized;
                    damageable.TakeDamage(_damage, hitDir, "Arrow");
                }

                // [C 고품질] 마법 화살 — 적 1기 관통. 이미 관통한 대상 재데미지/재소멸 방지.
                bool piercedThis = false;
                int thisId = hitGO != null ? hitGO.GetInstanceID() : -1;
                if (_arrowData != null && _arrowData.canPierce && _pierceRemaining > 0
                    && hitGO != null && !hitGO.CompareTag("DraculaLord"))
                {
                    if (_piercedId != thisId)
                    {
                        _piercedId = thisId;
                        _pierceRemaining--;
                        piercedThis = true;
                    }
                }


                // [70차 후속19/C2·C3] 명중 피드백 — 활 히트스톱+흔들림(파워 풀=PlayCrit 강화) + 데미지 숫자(골드)
                if (_power >= 0.95f) CombatCameraEffects.PlayCrit();
                else CombatCameraEffects.PlayHit(ProjectName.Core.WeaponType.Bow);
                // [요구] 노란 파티클 제거 — 이미 피격 이펙트가 있으므로 명중 스파크/크리틱 버스트는 뽑지 않는다.
                //   유지: 데미지 숫자(골드)·카메라 히트피드백(PlayHit/PlayCrit: 히트스톱·흔들림)·임팩트 사운드/styles 및 trail off.
                //   (CombatVFXController 파일은 다른 무기 슬래시가 공유하므로 여기서 수정 금지 — 본 파일에서만 호출 제거)
                Vector3 hitPoint = other != null ? other.ClosestPoint(transform.position) : transform.position;
                CombatVFXController.ShowDamageNumber(other.transform.position + Vector3.up * 1.0f,
                    Mathf.RoundToInt(_damage), new Color(1f, 0.85f, 0.4f));

                // [P25-C4 안1] 적 명중 = 별 섬광 + 화살 소멸(예시 소멸형 절충).
                //   기존 6초 타겟 박힘은 소멸로 대체 — 섬광/데미지 숫자/히트스톱/임팩트음이 즉각 피드백.
                // [C 고품질] 마법 화살 관통 시에는 소멸하지 않고 비행 지속(트레일 잔상 강조).
                // [테스트45 P2] 타격 별섬광(이상한 파티클) 제거 — 피격 이펙트(데미지 숫자·히트스톱)만 남긴다.
                // SpawnStarFlare(hitPoint);
                if (piercedThis)
                {
                    // 관통 — 데미지 처리는 위에서 적용, 화살은 계속 비행(한 대상만, 신규 적 재데미지).
                    return;
                }
                DisableTrail();
                Destroy(gameObject);
            }
            else if (isOwnSoldier)
            {
                // 내 소속 병사 — 피해 없이 관통(지면/벽 충돌 분기에도 걸리지 않도록 명시적 no-op)
            }
            // 지면/벽 충돌
            else if (!other.CompareTag("Player") && !other.isTrigger)
            {
                _stuck = true;                                   // 회전 정렬 스킵·일관화
                _lifetime = Mathf.Min(_lifetime, _elapsed + 2f); // 2초 후 소멸
                if (_rb != null) _rb.linearVelocity = Vector3.zero;
                if (_collider != null) _collider.enabled = false; // 중복 충돌 방지
                DisableTrail();                                  // [P25-C3] 박힘 트레일 제거

                // [P22-3] 박힘 진동 시작 (지면 먼지 퍼프 SpawnGroundPuff는 테스트47에서 제거 — 검은 파티클 요구)
                _wobbleTime = 0f;
                _stuckRotation = transform.rotation;
                // SpawnGroundPuff();
            }
        }
    }
}
