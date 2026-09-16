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

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _trail = GetComponent<TrailRenderer>();
            if (_trail == null)
                _trail = gameObject.AddComponent<TrailRenderer>();

            _trail.time = 0.5f;    // [TEST28-69차] 0.35→0.5 — 잔상 길게(비행 가시성)
            _trail.startWidth = 0.08f;   // [TEST27-68차] 0.05→0.08 — 원거리 가시성
            _trail.endWidth = 0.01f;
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
            go.transform.localScale = new Vector3(0.06f, 0.7f, 0.06f);

            // Collider 설정
            var collider = go.GetComponent<CapsuleCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.linearVelocity = direction * speed;
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

            return arrow;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
            }

            // 회전을 속도 방향으로 정렬
            if (_rb != null && _rb.linearVelocity.magnitude > 0.1f)
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
                Destroy(gameObject);
            }
            else if (isOwnSoldier)
            {
                // 내 소속 병사 — 피해 없이 관통(지면/벽 충돌 분기에도 걸리지 않도록 명시적 no-op)
            }
            // 지면/벽 충돌
            else if (!other.CompareTag("Player") && !other.isTrigger)
            {
                _lifetime = Mathf.Min(_lifetime, _elapsed + 2f); // 2초 후 소멸
                if (_rb != null) _rb.linearVelocity = Vector3.zero;
                if (_collider != null) _collider.enabled = false; // 중복 충돌 방지
            }
        }
    }
}
