using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// Persistent singleton that survives scene loads.
    /// Uses lazy initialization with DontDestroyOnLoad support.
    /// </summary>
    [DisallowMultipleComponent]
    public class PersistentManager : MonoBehaviour
    {
        private static PersistentManager _instance;
        private static bool _instanceQuitting = false;

        /// <summary>
        /// Gets the singleton instance. Creates a new GameObject if none exists.
        /// Returns null if the application is quitting.
        /// </summary>
        public static PersistentManager Instance
        {
            get
            {
                if (_instanceQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("[PersistentManager]");
                    _instance = go.AddComponent<PersistentManager>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _instanceQuitting = false;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void OnApplicationQuit()
        {
            _instanceQuitting = true;
        }
    }
}