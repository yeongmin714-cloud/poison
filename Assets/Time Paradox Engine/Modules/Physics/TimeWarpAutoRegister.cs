using UnityEngine;
using TimeWarp.Modules.Physics;

namespace TimeWarp.Core
{
    /// <summary>
    /// Attach this script to PREFABS that are spawned (Instantiate) during gameplay 
    /// (e.g., debris, projectiles, new enemies). 
    /// It will automatically ask AutoSetup to inject time components.
    /// </summary>
    [AddComponentMenu("TimeWarp/Core/Time Warp Auto Register")]
    public class TimeWarpAutoRegister : MonoBehaviour
    {
        // FIX: Changed from Start() to OnEnable(), so registration to the Manager 
        // happens in the first available microsecond after Instantiate().
        private void OnEnable()
        {
            if (TimeWarpAutoSetup.Instance != null)
            {
                TimeWarpAutoSetup.Instance.ApplyRules(gameObject);
            }
            else
            {
                Debug.LogWarning("[TimeWarp] Missing TimeWarpAutoSetup object in the scene. The new object did not receive time properties.");
            }
        }
    }
}