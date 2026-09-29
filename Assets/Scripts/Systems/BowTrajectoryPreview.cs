using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Systems
{
    /// <summary>
    /// Legacy facade retained for existing callers. Trajectory lines are disabled; the old host is
    /// explicitly removed on bootstrap, scene entry, and any legacy Ensure call.
    /// </summary>
    public static class BowTrajectoryPreview
    {
        /// <summary>Legacy API: deliberately does not create a trajectory preview.</summary>
        public static void Ensure() => Kill();

        /// <summary>Remove both the cached and any surviving legacy trajectory hosts.</summary>
        public static void Kill()
        {
            _host = null;
            foreach (var host in Resources.FindObjectsOfTypeAll<BowTrajectoryHost>())
            {
                if (host == null || !host.gameObject.scene.IsValid()) continue;
                Object.Destroy(host.gameObject);
            }
        }

        private static BowTrajectoryHost _host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCleanup()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Kill();
    }

    /// <summary>Legacy host type kept only so stale instances can be detected and removed.</summary>
    public sealed class BowTrajectoryHost : MonoBehaviour
    {
        // If an old/persistent host survives until its first frame, fail closed even without the facade.
        private void LateUpdate()
        {
            if (!BowAimState.Drawing || BowAimState.ReleasePending)
                Destroy(gameObject);
        }
    }
}
