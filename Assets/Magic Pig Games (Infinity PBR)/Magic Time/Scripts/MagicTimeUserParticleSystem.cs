using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MagicPigGames.MagicTime
{
    [System.Serializable]
    public class MagicTimeUserParticleSystem : MagicTimeMonoBehaviour
    {
        [Header("Options")]
        public bool handleSimulationSpeed = true;
        public bool handleLifetime = true;

        [Header("Plumbing")]
        public MagicTimeUser magicTimeUser;
        public ParticleSystem[] particleSystems;
        private float[] _cachedSimulationSpeeds;
        private float[] _cachedStartLifetimes;

        protected float _cachedTimeScale;

        public virtual void Awake()
        {
            OnValidate();
        }

        public virtual void Update()
        {
            if (Mathf.Approximately(magicTimeUser.TimeScale, _cachedTimeScale))
                return;

            _cachedTimeScale = magicTimeUser.TimeScale;
            foreach (var ps in particleSystems)
            {
                SetSimulationSpeed(ps, magicTimeUser.TimeScale);
                SetLifetime(ps, magicTimeUser.TimeScale);
            }
        }

        protected void OnEnable()
        {
            for (var i = 0; i < particleSystems.Length; i++)
            {
                SetSimulationSpeed(particleSystems[i], _cachedSimulationSpeeds[i]);
                SetLifetime(particleSystems[i], _cachedTimeScale);
            }
        }

        protected virtual void SetSimulationSpeed(ParticleSystem ps, float timeScale)
        {
            if (!handleSimulationSpeed) return;

            if (ps == null)
            {
                Debug.LogError($"A particle system was null. This should not happen. Check the object {gameObject.name} to ensure " +
                               $"all ParticleSystems are assigned.");
                return;
            }
            var main = ps.main; // Retrieve the MainModule from the ParticleSystem
            main.simulationSpeed = timeScale; // Modify the simulationSpeed directly
        }

        protected virtual void SetLifetime(ParticleSystem ps, float timeScale)
        {
            if (!handleLifetime) return;

            if (ps == null)
            {
                Debug.LogError($"A particle system was null. This should not happen. Check the object {gameObject.name} to ensure " +
                               $"all ParticleSystems are assigned.");
                return;
            }

            var main = ps.main; // Retrieve the MainModule from the ParticleSystem
            // Adjust lifetime: the inverse of the timeScale will scale lifetime (longer if timeScale < 1, shorter if timeScale > 1)
            main.startLifetime = _cachedStartLifetimes[Array.IndexOf(particleSystems, ps)] * (1 / timeScale);
        }

        // Automatically populate particleSystems with all ParticleSystems in children
        public virtual void OnValidate()
        {
            if (particleSystems == null || particleSystems.Length == 0)
                particleSystems = GetComponentsInChildren<ParticleSystem>();

            if (magicTimeUser == null)
                magicTimeUser = GetComponent<MagicTimeUser>();

            _cachedSimulationSpeeds = new float[particleSystems.Length];
            _cachedStartLifetimes = new float[particleSystems.Length];
            for (var i = 0; i < particleSystems.Length; i++)
            {
                var main = particleSystems[i].main;
                _cachedSimulationSpeeds[i] = main.simulationSpeed;
                _cachedStartLifetimes[i] = main.startLifetime.constant; // Cache the original lifetime value
            }
        }
    }
}