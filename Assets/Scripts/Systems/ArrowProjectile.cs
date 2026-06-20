using ProjectName.Core;
using UnityEngine;

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
        private int _damage = 10;
        private float _lifetime = 5f;
        private float _elapsed = 0f;
        private TrailRenderer _trail;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            if (_trail == null)
                _trail = gameObject.AddComponent<TrailRenderer>();

            _trail.time = 0.3f;
            _trail.startWidth = 0.05f;
            _trail.endWidth = 0.01f;
            _trail.minVertexDistance = 0.1f;
            _trail.material = new Material(Shader.Find("Sprites/Default"));
        }

        /// <summary>화살 발사</summary>
        public static ArrowProjectile Spawn(Vector3 position, Vector3 direction, float speed, int damage, Color trailColor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Arrow(Clone)";
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(direction);
            go.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);

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
            var rb = GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.magnitude > 0.1f)
            {
                transform.forward = rb.linearVelocity.normalized;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 적 감지
            if (other.CompareTag("Enemy") || other.CompareTag("Monster"))
            {
                var damageable = other.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    Vector3 hitDir = (other.transform.position - transform.position).normalized;
                    damageable.TakeDamage(_damage, hitDir, "Arrow");
                }
                Destroy(gameObject);
            }
            // 지면/벽 충돌
            else if (!other.CompareTag("Player") && !other.isTrigger)
            {
                _lifetime = Mathf.Min(_lifetime, _elapsed + 2f); // 2초 후 소멸
                var rb = GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
                GetComponent<Collider>().enabled = false; // 중복 충돌 방지
            }
        }
    }
}
