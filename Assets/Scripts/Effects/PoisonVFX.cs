using UnityEngine;

namespace ProjectName.Core.Effects
{
    /// <summary>
    /// Spawns a poison cloud VFX with a given color.
    /// </summary>
    public class PoisonVFX : MonoBehaviour
    {
        private ParticleSystem _ps;
        private float _duration;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
            if (_ps == null)
            {
                Debug.LogError("[PoisonVFX] ParticleSystem component not found.");
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>
        /// Initializes the poison VFX with color and duration.
        /// Call after instantiation.
        /// </summary>
        public void Initialize(Color color, float duration = 5f)
        {
            _duration = duration;
            var main = _ps.main;
            main.startColor = color;
            // Ensure it plays
            _ps.Play();
            // Auto destroy after duration
            Destroy(gameObject, duration);
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
            main.startLifetime = 3f; // particles live 3 seconds
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

            // Renderer settings
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.material = new Material(Shader.Find("Sprites/Default")); // simple sprite
            rend.lengthScale = 1f;
            rend.velocityScale = 0f;

            // Add the PoisonVFX script to handle destruction
            var script = go.AddComponent<PoisonVFX>();
            script.Initialize(color, duration);

            return go;
        }
    }
}