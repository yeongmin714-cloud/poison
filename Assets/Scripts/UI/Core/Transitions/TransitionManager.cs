using UnityEngine;
using System.Collections;

namespace UI.Core.Transitions
{
    public class TransitionManager : MonoBehaviour
    {
        public static TransitionManager Instance { get; private set; }

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

        public IEnumerator SmoothTransition(float duration, System.Action<float> updateAction, System.Action onComplete = null)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                updateAction?.Invoke(progress);
                yield return null;
            }
            onComplete?.Invoke();
        }
    }
}