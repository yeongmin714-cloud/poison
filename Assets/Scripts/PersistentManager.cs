using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// Persistent singleton that survives scene loads.
    /// </summary>
    public class PersistentManager : MonoBehaviour
    {
        private static PersistentManager _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static PersistentManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[PersistentManager]");
                    _instance = go.AddComponent<PersistentManager>();
                }
                return _instance;
            }
        }
    }
}