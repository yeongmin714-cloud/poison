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
        private bool _stuck = false;    // 명중/지면 꽂힘 시 true — 회전 정렬·충돌 재처리 방지
        /// <summary>[70차 후속19/C6] 발사 파워(0~1) — ArrowManager가 세팅. 파워 풀 명중 시 크리틱 연출.</summary>
        public float _power = 1f;
        private static readonly float GravityScale = 0.22f;   // [화살-사거리2] 0.45→0.22 — 낙하 1.17s·사거리 ~80m(테스트 2: 여전히 짧음)

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            // [P20-4 수리] 트레일 완전 제거 — "긴 선만 뒤따라 화살이 날아가는 느낌이 안 든다" 요구.
            //   TrailRenderer 부착/설정 코드 전면 삭제. 꼬리 없이 화살 본체만 비행.
            var legacyTrail = GetComponent<TrailRenderer>();
            if (legacyTrail != null) Destroy(legacyTrail);
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

            // ⑤ 재부모화 + 정렬: 촉(-X) → 진행축(+Y). 노드 회전은 교체(장축 불변이므로 안전).
            inst.transform.SetParent(wrapper.transform, false);
            inst.transform.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, 180f, 0f);
            inst.transform.localScale = Vector3.one;
            inst.transform.localPosition = -(inst.transform.localRotation * center);   // 피벗 = 모델 중심

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
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    Vector3 hitDir = (other.transform.position - transform.position).normalized;
                    damageable.TakeDamage(_damage, hitDir, "Arrow");
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

                // [2026-09-17] 박힘(stick) — 즉시 제거 대신 화살을 타겟의 자식으로 부모 변경해
                //   6초간 몸통에 박힌 채 잔존시킨다. worldPositionStays:true로 월드 위치/회전 유지.
                _stuck = true;
                _lifetime = Mathf.Min(_lifetime, _elapsed + 6f); // 타겟에 6초간 박힘
                if (_rb != null)
                {
                    _rb.isKinematic = true;
                    _rb.useGravity = false;
                    _rb.linearVelocity = Vector3.zero;
                }
                if (_collider != null) _collider.enabled = false; // 중복 재명중 방지
                if (hitGO != null)
                {
                    transform.SetParent(hitGO.transform, true);
                }
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

                // [요구] 노란 파티클 제거 — 지면/벽 꽂힘 스파크도 동일 사유로 제거(사운드는 기존 유지).
            }
        }
    }
}
