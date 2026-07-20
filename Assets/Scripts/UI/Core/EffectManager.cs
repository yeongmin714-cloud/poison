using UnityEngine;
using UnityEngine.UI;

namespace UI.Core
{
    public class EffectManager : MonoBehaviour
    {
        public static EffectManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void PlayEffect(string effectName, Transform target)
        {
            // Implementation for playing effects
        }

        public void PlayEffect(string effectName, Vector3 position)
        {
            // Implementation for playing effects at position
        }

        public void StopEffect(string effectName)
        {
            // Implementation for stopping effects
        }
    }
}