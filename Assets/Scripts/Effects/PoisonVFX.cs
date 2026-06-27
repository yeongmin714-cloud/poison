using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core.Effects
{
    /// <summary>
    /// Spawns a poison cloud VFX with a given color.
    /// </summary>
    public class PoisonVFX : MonoBehaviour
    {
        private ParticleSystem _ps;
        private ParticleSystemRenderer _rend;
        private Material _createdMaterial;
        private float _duration;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            _rend = GetComponent<ParticleSystemRenderer>();
            if (_ps == null)
            {
                Debug.LogError("[PoisonVFX] ParticleSystem component not found.");
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            // 동적으로 생성한 Material 정리 (메모리 누수 방지)
            if (_createdMaterial != null)
            {
                Destroy(_createdMaterial);
                _createdMaterial = null;
            }
        }

        /// <summary>
        /// Initializes the poison VFX with color and duration.
        /// Call after instantiation.
        /// </summary>
        public void Initialize(Color color, float duration = 5f)
        {
            if (_ps == null) return;

            _duration = duration;
            var main = _ps.main;
            main.startColor = color;

            // playOnAwake가 false인 상태에서만 Play() 호출
            _ps.Play();

            // Auto destroy after duration
            Destroy(gameObject, duration);
        }

        /// <summary>
        /// URP 호환 파티클 셰이더를 Fallback 체인으로 찾아 반환한다.
        /// </summary>
        private static Shader FindParticleShader()
        {
            return Shader.Find("Universal Render Pipeline/Particles/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");
        }

        /// <summary>
        /// Spawns a poison cloud at the given position with the specified color.
        /// </summary>
        public static GameObject Spawn(Color color, Vector3 position, float duration = 5f)
        {
            // Create a simple particle system gameobject
            GameObject go = new GameObject("PoisonCloudVFX");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            var rend = go.AddComponent<ParticleSystemRenderer>();

            // Basic particle setup
            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.prewarm = false;
            main.playOnAwake = false;               // 중복 Play 방지
            main.startLifetime = Mathf.Min(3f, duration); // duration보다 길지 않게
            main.startSpeed = 2f;
            main.startSize = 2f;
            main.gravityModifier = 0.1f;
            main.startColor = color;

            var emission = ps.emission;
            emission.rateOverTime = 20f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 3f;

            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.lifetimeLoss = 0.2f;
            collision.minKillSpeed = 1f;

            // Renderer settings — URP Fallback 체인 사용
            Shader shader = FindParticleShader();
            rend.material = new Material(shader);
            rend.material.name = "PoisonVFX_Mat_Generated";
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.lengthScale = 1f;
            rend.velocityScale = 0f;

            // Add the PoisonVFX script to handle destruction
            var script = go.AddComponent<PoisonVFX>();
            // 동적 Material 참조를 전달하여 OnDestroy에서 정리
            script._createdMaterial = rend.material;
            script.Initialize(color, duration);

            return go;
        }
    }
}