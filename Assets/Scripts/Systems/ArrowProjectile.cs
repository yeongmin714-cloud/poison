using System.Collections.Generic;
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
        private float _damage = 10f;
        private float _lifetime = 5f;
        private float _elapsed = 0f;
        private Rigidbody _rb;
        private Collider _collider;
        private TrailRenderer _trail;      // [P25-C3] 소형 밝은 트레일
        private bool _stuck = false;    // 명중/지면 꽂힘 시 true — 회전 정렬·충돌 재처리 방지
        private float _wobbleTime = -1f;   // [P22-3] 박힘 직후 미세 진동 타이머
        private Quaternion _stuckRotation;
        /// <summary>[70차 후속19/C6] 발사 파워(0~1) — ArrowManager가 세팅. 파워 풀 명중 시 크리틱 연출.</summary>
        public float _power = 1f;
        private static readonly float GravityScale = 0.22f;   // [화살-사거리2] 0.45→0.22 — 낙하 1.17s·사거리 ~80m(테스트 2: 여전히 짧음)

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
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0.5f, 0.45f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = tgrad;

            // 강화 — 은빛 샤프 스파크 이중 트레일 (아직 없을 때만 생성; 기존 재질 공유)
            if (_arrowData != null && _arrowData.sparkTrail && _sparkTrail == null && _trail.material != null)
            {
                try
                {
                    _sparkTrail = gameObject.AddComponent<TrailRenderer>();
                    _sparkTrail.time = 0.08f;
                    _sparkTrail.startWidth = 0.06f;
                    _sparkTrail.endWidth = 0.005f;
                    _sparkTrail.minVertexDistance = 0.05f;
                    _sparkTrail.generateLightingData = false;
                    _sparkTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    _sparkTrail.receiveShadows = false;
                    _sparkTrail.material = _trail.material;
                    var sgrad = new Gradient();
                    sgrad.SetKeys(
                        new[] { new GradientColorKey(new Color(0.95f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.5f, 0.5f, 0.7f), 1f) },
                        new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 0.6f) });
                    _sparkTrail.colorGradient = sgrad;
                }
                catch (System.Exception e)
                {
                    Debug.Log("[Arrow][C] 강화 스파크 트레일 생성 실패(무시): " + e.ToString());
                }
            }
        }

        // [C 고품질] 3티어 파라미터(ArrowManager가 주입, Spawn 오버로드 경유)
        private ProjectName.Core.ArrowData _arrowData = ProjectName.Core.ArrowData.Regular;
        private int _pierceRemaining = 0;        // 마법 화살 관통 잔여(적 1기)
        private int _piercedId = -1;             // 이미 관통한 대상 instanceId (중복 재데미지 방지)
        private float _sparkAccum = 0f;          // 강화 화살 스파크 방출 누적(초)
        private TrailRenderer _sparkTrail = null; // 강화 전용 이중 트레일(은빛 스파크)

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            // [P25-C3] 소형 밝은 트레일 복원 — P20-4가 "긴 선"이라 제거했던 원인은 과장된
            //   시간(1.6s)·폭(0.45) 때문. 예시(BotW) 스타일: 짧고(0.4s) 가는(0.10→0.02)
            //   화이트→하늘색 테이퍼로 비행 감을 살린다. 박힘 시 _trail.enabled=false로 제거.
            // [2026-09-25 젤다 화살예시 재현] 비행 중 방향성 모션 스트릭(선형 흰→옅은 청).
            // 예시(BoTW): 화살 길이 2~3배인 선명한 흰 잔상 + 애더티브 글로우. 기존 time 0.4s는 게임
            // 속도에서 8~16m로 과하게 길게 번져 "모션 스트릭"이 아니라 스미어로 보였다 → 짧고 선명·발광으로.
            _trail = GetComponent<TrailRenderer>();
            if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.12f;              // [참조] ~2~3배 화살 길이 스트릭(고속일수록 길어짐 = 속도감)
            _trail.startWidth = 0.12f;        // 선명 선두(샤프트 반경급)
            _trail.endWidth = 0.02f;          // 꼬리 얇게 테이퍼
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
            var strk0 = new Color(1f, 0.99f, 0.97f);
            var strk1 = _arrowData != null ? _arrowData.streakColor : new Color(0.75f, 0.82f, 1f);
            tgrad.SetKeys(
                new[] { new GradientColorKey(strk0, 0f), new GradientColorKey(strk1, 1f) },  // 선명 흰→티어 스트릭색
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0.5f, 0.45f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = tgrad;

            // [C 고품질] 강화 화살 — 은빛 샤프 스파크 이중 트레일(짧고 또렷, 광량 보강)
            if (_arrowData != null && _arrowData.sparkTrail)
            {
                _sparkTrail = gameObject.AddComponent<TrailRenderer>();
                _sparkTrail.time = 0.08f;
                _sparkTrail.startWidth = 0.06f;
                _sparkTrail.endWidth = 0.005f;
                _sparkTrail.minVertexDistance = 0.05f;
                _sparkTrail.generateLightingData = false;
                _sparkTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _sparkTrail.receiveShadows = false;
                _sparkTrail.material = tmat;   // 동일 애더티브 재질(공유)
                var sgrad = new Gradient();
                sgrad.SetKeys(
                    new[] { new GradientColorKey(new Color(0.95f, 0.95f, 1f), 0f), new GradientColorKey(new Color(0.5f, 0.5f, 0.7f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 0.6f) });
                _sparkTrail.colorGradient = sgrad;
            }
        }

        /// <summary>화살 발사</summary>
        public static ArrowProjectile Spawn(Vector3 position, Vector3 direction, float speed, float damage, Color trailColor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Arrow(Clone)";
            go.transform.position = position;
            // [TEST27-68차] 축 정렬 수리 — Cylinder 길이축은 Y인데 LookRotation은 +Z를 진행방향으로 정렬해
            //   화살이 옆으로 누운 채 날아갔다(엣지온 = 안 보임, 사용자 실측 "화살이 날아가지도 않음").
            //   X축 +90° 회전을 곱해 길이축(Y)을 진행방향으로 세운다.
            go.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.12f, 0.9f, 0.12f); // [P20-4] (0.18,1.3)→(0.12,0.9) — 사용자 "여전히 큼" → 2차 축소

            // Collider 설정
            var collider = go.GetComponent<CapsuleCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;                 // [化살-사거리] 물리 중력 대신 아래 Update에서 축소 중력 수동 적용(45% 중력 → 약 2배 사거리)
            rb.linearVelocity = direction * speed;
            rb.linearDamping = 0f;                 // 비행 중 저항 없음
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;   // [후속19/A4] 프레임 간 보간 — 트레일 끊김 완화

            var arrow = go.AddComponent<ArrowProjectile>();
            arrow._damage = damage;

            AttackSoundLayerManager.PlayArrowWhistle();   // [E 고품질] 비행 휘파람(발사 직후)

            // [P20-4 진단] 스폰 회전 vs 조준 방향 정합 1회 실측 — "세워서 나감/방향 다름" 즉별
            float dot = Vector3.Dot(go.transform.up, direction.normalized);
            Debug.Log($"[Arrow][P20-4] 스폰 정합 — up·dir={dot:F3}(±1이 정상), dir={direction}");

            // Renderer
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.material.color = trailColor * 0.7f;
            }

            // [요구] 실제 화살 GLB 모델 장착(arrow.glb → arrow2 → arrow3 폴백, 전부 실패 시 원기둥 회귀).
            //   성공 시 실린더 렌더러는 숨기고(콜라이더·트레일은 루트가 유지) 모델이 비주얼을 대체한다.
            if (MountArrowModel(go))
            {
                if (renderer != null) renderer.enabled = false;
            }
            else
            {
                // [2026-09-17] 절차 조립 회귀 — 샤프트(실린더) + 촉(콘) + 플레처(사각조각 3개) 자식 추가.
                //   피벗은 샤프트 중심 유지, 자식은 Rigidbody 없이 부모 종속, 콜라이더 제거로 2차 충돌 차단.
                //   실패(Cone/Cube 프리미티브 null 등) 시에도 최소한 샤프트 실린더로 동작.
                try
                {
                    AssembleArrow(go, trailColor);
                }
                catch (System.Exception e)
                {
                    Debug.Log("[Arrow] 화살 머리/깃털 조립 실패 → 샤프트만 유지: " + e.ToString());
                }
            }

            return arrow;
        }

        /// <summary>
        /// 화살 모델 조립 — 샤프트(실린더)에 촉(콘) + 플레처(사각조각 3개)를 자식으로 부착.
        /// 피벗은 샤프트 중심 유지. 자식은 Rigidbody 없이 부모에 종속되며 콜라이더를 제거해
        /// 명중 시 2차 충돌을 만들지 않는다. 실패 시 호출부 try-catch가 샤프트만 보존한다.
        /// </summary>
        private static void AssembleArrow(GameObject shaft, Color trailColor)
        {
            // 촉과 플레처는 **별도 Material 인스턴스**를 사용해야 한다. 같은 Material 객체를 여러
            // 렌더러에 할당한 뒤 각자 .color를 세팅하면 마지막 설정이 전부에 덮어써진다.
            var headMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var featherMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var metalColor = new Color(0.85f, 0.82f, 0.75f);  // 금속 회백색 (촉)
            var featherColor = new Color(0.7f, 0.15f, 0.1f); // 진한 적갈색 (깃털)

            // ---- 촉(헤드) — Cone +Y가 뾰족한 방향. 샤프트 앞쪽(+Y)에 배치 ----
            var head = new GameObject("ArrowHead");
            head.transform.SetParent(shaft.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.32f, 0f);  // [P20-4] 샤프트 반길이(0.45→0.45)에 맞춰 앞단 배치
            head.transform.localScale = new Vector3(0.09f, 0.22f, 0.09f); // [P20-4] 촉 축소
            {
                var mf = head.AddComponent<MeshFilter>();
                mf.mesh = BuildArrowHeadCone();   // PrimitiveType.Cone 없음 → 절차 메시(양면 와인딩)
                var mr = head.AddComponent<MeshRenderer>();
                mr.material = new Material(headMat);
                mr.material.color = metalColor;
            }

            // ---- 플레처(깃털) 3개 — 샤프트 후미(-Y), 길이축(Y) 기준 120° 방사 배치 ----
            for (int i = 0; i < 3; i++)
            {
                var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fin.name = "ArrowFletching" + i;
                fin.transform.SetParent(shaft.transform, false);
                fin.transform.localScale = new Vector3(0.045f, 0.2f, 0.08f); // [P20-4] 깃 축소
                // X축으로 샤프트 표면에 살짝 오프셋 → 길이축(Y) 회전으로 120° 방사 팬.
                fin.transform.localPosition = new Vector3(0.032f, -0.34f, 0f);
                fin.transform.localRotation = Quaternion.Euler(0f, 120f * i, 0f);
                {
                    var c = fin.GetComponent<Collider>();
                    if (c != null) Destroy(c);
                    var mr = fin.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        mr.material = new Material(featherMat);
                        mr.material.color = featherColor;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // [요구] 실제 화살 GLB 장착 — arrow.glb → arrow2 → arrow3 폴백.
        //
        // [GLB 실측] (GLB JSON/바이너리 직접 파싱 — 3모델 공통):
        //   - 메시 장축 = X(길이 1.0), 촉 = -X 쪽(+X단 평균반경 0.50 = 깃털, -X단 0.12 = 촉),
        //   - 노드 회전 Rx(90) — 장축 방향은 불변.
        //   → 촉(-X)을 루트 진행축(로컬 +Y)으로 세우려면 Q_fix = Rz(90)×Ry(180).
        //
        // [전단(스큐) 방지 설계] 루트 스케일이 비균일(0.25, 1.8, 0.25)이라 회전된 자식을
        //   직접 넣으면 찌그러진다. → 래퍼를 "무회전"으로 두고 localScale을 루트 스케일의
        //   역수 비율로 보간해 래퍼 lossyScale을 균일(s)로 만든 뒤, 그 안에서만 모델을 회전.
        //   (균일 스케일 × 회전은 전단이 발생하지 않음 — 수학적 보장)
        //
        // [피팅] 스탠드얼론으로 인스턴스 후 renderer.bounds 실측 → 최장축을 기존 실린더
        //   시각 길이(단위 2 × localScale.y 1.8 = 3.6m)에 자동 스케일. 피벗 = bounds 중심.
        // ─────────────────────────────────────────────────────────────
        private const float ArrowModelTargetLength = 1.8f;   // [P20-4] 2.6→1.8 — 실린더 Y 0.9와 일치(2×0.9), 2차 축소

        private static bool MountArrowModel(GameObject root)
        {
            // ① 프리팹 로드 폴백 체인
            string[] paths = { "Models/UserProvided/arrow", "Models/UserProvided/arrow2", "Models/UserProvided/arrow3" };
            GameObject prefab = null;
            string used = null;
            foreach (var p in paths)
            {
                prefab = Resources.Load<GameObject>(p);
                if (prefab != null) { used = p; break; }
            }
            if (prefab == null)
            {
                Debug.Log("[Arrow] GLB 로드 실패(arrow/arrow2/arrow3 전부) — 절차 화살 회귀");
                return false;
            }

            // ② 스탠드얼론 인스턴스 → 원본 스케일 1 상태에서 bounds 실측(노드 회전 포함 월드=모델 공간)
            GameObject inst = Object.Instantiate(prefab);
            Bounds total = new Bounds(Vector3.zero, Vector3.zero);
            bool hasRenderer = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasRenderer) { total = r.bounds; hasRenderer = true; }
                else total.Encapsulate(r.bounds);
            }
            if (!hasRenderer || total.size.x <= 0f && total.size.y <= 0f && total.size.z <= 0f)
            {
                Debug.Log("[Arrow] GLB 렌더러 없음 — 절차 화살 회귀");
                Object.Destroy(inst);
                return false;
            }
            float maxDim = Mathf.Max(total.size.x, total.size.y, total.size.z);
            float s = ArrowModelTargetLength / Mathf.Max(0.0001f, maxDim);
            Vector3 center = total.center;   // 모델 공간 중심(피벗 보정용)

            // ③ 콜라이더 제거 — 2차 충돌 방지(충돌은 루트 캡슐이 담당)
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);

            // ④ 균일 스케일 래퍼 — 루트 비균일 스케일 역보간으로 lossyScale=(s,s,s) 달성
            var lossy = root.transform.lossyScale;
            var wrapper = new GameObject("ArrowModelWrap");
            wrapper.transform.SetParent(root.transform, false);
            wrapper.transform.localPosition = Vector3.zero;
            wrapper.transform.localRotation = Quaternion.identity;
            wrapper.transform.localScale = new Vector3(
                s / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
                s / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
                s / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));

            // ⑤ 재부모화 + 정렬: [P22-1 수리] 촉(-X) → 진행축(+Y).
            //   기존 Rz(90): (x,y)→(-y,x) — (-1,0)=촉(-X)가 (0,-1)=**-Y 후방**으로 매핑돼
            //   화살이 촉이 뒤로 향한 채 날아갔다(테스트38 "뒤집혀서 나감"). 부호 실수.
            //   Rz(-90): (x,y)→(y,-x) — (-1,0)→(0,1)=+Y 전방. 롤 방향은 Ry(180) 유지(깃털 배치 무해).
            inst.transform.SetParent(wrapper.transform, false);
            inst.transform.localRotation = Quaternion.Euler(0f, 0f, -90f) * Quaternion.Euler(0f, 180f, 0f);
            inst.transform.localScale = Vector3.one;
            inst.transform.localPosition = -(inst.transform.localRotation * center);   // 피벗 = 모델 중심

            // [P22-1 셀프플립 검증] 촉(가는 끝)이 +Y(전방)에 있는지 정밀 판별 — 아니면 자동 180° 플립.
            //   루트/래퍼/메시 로컬 변환을 모두 반영해 Y 상단/하단 반경 평균 비교: 가는 쪽 = 촉.
            float radiusTop = 0f, radiusBottom = 0f;
            int samplesTop = 0, samplesBottom = 0;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                foreach (var v in mesh.vertices)
                {
                    var wp = mf.transform.TransformPoint(v);
                    var local = wrapper.transform.InverseTransformPoint(wp);  // 래퍼 공간 기준(균일 스케일)
                    float r = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                    if (local.y > 0.05f) { radiusTop += r; samplesTop++; }
                    else if (local.y < -0.05f) { radiusBottom += r; samplesBottom++; }
                }
            }
            if (samplesTop > 0 && samplesBottom > 0)
            {
                float avgTop = radiusTop / samplesTop, avgBottom = radiusBottom / samplesBottom;
                bool tipAtTop = avgTop < avgBottom;   // 가는 쪽(반경 작음) = 촉
                if (!tipAtTop)
                {
                    inst.transform.localRotation *= Quaternion.Euler(180f, 0f, 0f);
                    inst.transform.localPosition = -(inst.transform.localRotation * center);
                    Debug.Log("[Arrow][P22-1] 촉이 -Y 감지 — 자동 180° 플립 적용");
                }
                Debug.Log($"[Arrow][P22-1] 촉 방향 검증 — avgR top={avgTop:F3} bottom={avgBottom:F3} → 촉={(tipAtTop ? "+Y(정상)" : "-Y(플립)")}");
            }

            Debug.Log($"[Arrow] GLB 장착: {used} 목표길이={ArrowModelTargetLength:0.0}m (모델 maxDim={maxDim:0.00}, scale={s:0.00})");
            return true;
        }

        /// <summary>
        /// 절차 생성 콘(촉) 메시 — PrimitiveType.Cone이 없으므로 직접 생성.
        /// 단위(반지름1·높이1)로 만들어 localScale로 크기 조절. +Y가 뾰족한 방향.
        /// 양면 와인딩을 넣어 컬링/와인딩 오류로 안 보이는 문제를 원천 차단한다.
        /// </summary>
        private static Mesh BuildArrowHeadCone()
        {
            int seg = 10;
            float radius = 1f, height = 1f;
            int baseCenter = 1 + seg;
            var verts = new Vector3[seg + 2];
            verts[0] = new Vector3(0f, height * 0.5f, 0f);          // 첨점(+Y)
            for (int i = 0; i < seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                verts[1 + i] = new Vector3(Mathf.Cos(a) * radius, -height * 0.5f, Mathf.Sin(a) * radius);
            }
            verts[baseCenter] = new Vector3(0f, -height * 0.5f, 0f); // 밑면 중심

            var tris = new List<int>(seg * 6);
            for (int i = 0; i < seg; i++)
            {
                int a = 1 + i, b = 1 + ((i + 1) % seg);
                // 옆면 (양면)
                tris.Add(0); tris.Add(b); tris.Add(a);
                tris.Add(0); tris.Add(a); tris.Add(b);
                // 밑면 캡 (양면)
                tris.Add(baseCenter); tris.Add(a); tris.Add(b);
                tris.Add(baseCenter); tris.Add(b); tris.Add(a);
            }

            var mesh = new Mesh { name = "ArrowHeadCone" };
            mesh.vertices = verts;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
            }

            // [化살-사거리] 축소 중력 수동 적용 — useGravity=false 상태에서 속도에 가속 추가(0.45*지구중력).
            //   박힌 화살(_stuck)/무중력 상태는 스킵.
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
            // [2026-09-20 방향 수정] 기존 transform.forward(+Z) 세팅은 Spawn에서 조립한
            // 축 정렬(LookRotation*Euler(90,0,0): 촉을 진행축에 맞춤)을 매 프레임 덮어써서
            // 화살 몸통이 진행 방향과 90° 어긋난 채 날아갔다(사용자 실측 "조준 방향으로 안 나감").
            // Spawn과 동일한 복합 회전을 재적용해 비행 내내 촉이 진행 방향을 향하게 한다.
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

                // [화살 명중 임팩트] 활은 PlayerCombat 근접 공격 경로를 타지 않으므로 여기서 직접
                // 임팩트 사운드를 발화한다(활 명중 시 T/P 하이 피치 임팩트). isTarget 분기당 1회만 호출.
                AttackSoundLayerManager.PlayAttackHit(ProjectName.Core.WeaponType.Bow, false);

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
                SpawnStarFlare(hitPoint);
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

                // [P22-3] 박힘 진동 시작 + 지면 먼지 퍼프 1회(베이크 소프트 텍스처 파티클 — 과장 없음)
                _wobbleTime = 0f;
                _stuckRotation = transform.rotation;
                SpawnGroundPuff();
            }
        }
    }
}
