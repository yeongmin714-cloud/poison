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
        private TrailRenderer _trail;
        private Rigidbody _rb;
        private Collider _collider;
        private bool _stuck = false;    // 명중/지면 꽂힘 시 true — 회전 정렬·충돌 재처리 방지
        private static readonly float GravityScale = 0.22f;   // [화살-사거리2] 0.45→0.22 — 낙하 1.17s·사거리 ~80m(테스트 2: 여전히 짧음)

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _trail = GetComponent<TrailRenderer>();
            if (_trail == null)
                _trail = gameObject.AddComponent<TrailRenderer>();

            _trail.time = 1.6f;          // [화살-가시성2] 0.9→1.6 — 비행 전체를 잔상이 덮음(속도70 기준 ~110m 커버)
            _trail.startWidth = 0.22f;   // 0.13→0.22 — 원거리 식별 강화
            _trail.endWidth = 0.05f;
            _trail.minVertexDistance = 0.08f;
            _trail.material = new Material(Shader.Find("Sprites/Default"));
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
            go.transform.localScale = new Vector3(0.13f, 1.15f, 0.13f); // [화살-가시성2] (0.09,0.85)→(0.13,1.15) — 샤프트 굵게·길게

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

            var arrow = go.AddComponent<ArrowProjectile>();
            arrow._damage = damage;

            if (arrow._trail != null)
            {
                arrow._trail.startColor = trailColor;
                arrow._trail.endColor = trailColor * 0.3f;
            }

            // Renderer
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.material.color = trailColor * 0.7f;
            }

            // [2026-09-17] 화살 모델 조립 — 샤프트(실린더) + 촉(콘) + 플레처(사각조각 3개) 자식 추가.
            //   피벗은 샤프트 중심(발사 origin과 동일) 유지. 자식 전부 Rigidbody 없이 부모에 종속되며,
            //   콜라이더를 제거해 명중 시 불필요한 2차 충돌을 만들지 않는다.
            try
            {
                AssembleArrow(go, trailColor);
            }
            catch (System.Exception e)
            {
                // 조립 실패(Cone/Cube 프리미티브 null 등) 시에도 화살은 최소한 샤프트 실린더로 동작.
                Debug.Log("[Arrow] 화살 머리/깃털 조립 실패 → 샤프트만 유지: " + e.ToString());
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
            head.transform.localPosition = new Vector3(0f, 0.45f, 0f);  // 샤프트 Y 반지름(0.35) + 헤드 길이 절반쯤
            head.transform.localScale = new Vector3(0.07f, 0.18f, 0.07f); // 뾰족한 촉 (단위 콘: 반지름1·높이1)
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
                fin.transform.localScale = new Vector3(0.03f, 0.22f, 0.08f);
                // X축으로 샤프트 표면에 살짝 오프셋 → 길이축(Y) 회전으로 120° 방사 팬.
                fin.transform.localPosition = new Vector3(0.045f, -0.5f, 0f);
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

            // 회전을 속도 방향으로 정렬 (박힌 화살은 유지)
            if (!_stuck && _rb != null && _rb.linearVelocity.magnitude > 0.1f)
            {
                transform.forward = _rb.linearVelocity.normalized;
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
            }
        }
    }
}
