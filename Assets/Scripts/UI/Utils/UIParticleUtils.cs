using UnityEngine;

namespace ProjectName.UI.Utils
{
    public class UIParticleUtils : MonoBehaviour
    {
        [Header("Particle Settings")]
        public new ParticleSystem particleSystem;

        public void PlayParticles()
        {
            // Play particle effects
            if (particleSystem != null)
            {
                particleSystem.Play();
            }
        }

        public void StopParticles()
        {
            // Stop particle effects
            if (particleSystem != null)
            {
                particleSystem.Stop();
            }
        }

        public void EmitParticles(int count)
        {
            // Emit specified number of particles
            if (particleSystem != null)
            {
                particleSystem.Emit(count);
            }
        }
    }
}