using UnityEngine;
using TimeWarp.Core.Managers;
using TimeWarp.Core;

namespace TimeWarp.Modules.Gameplay
{
    [AddComponentMenu("TimeWarp/Gameplay/Time Warp Invoker")]
    public class TimeWarpInvoker : MonoBehaviour
    {
        [Header("Quick Settings")]
        public float DefaultDuration = 2.0f;
        
        public void CastTimeStop()
        {
            TimeWarpManager.Instance.TriggerAction(GlobalAction.TimeStop, DefaultDuration);
        }

        public void CastSlowMotion(float scale)
        {
            float targetScale = scale > 0 ? scale : -1f;
            TimeWarpManager.Instance.TriggerAction(GlobalAction.SlowMotion, DefaultDuration, targetScale: targetScale);
        }

        public void RewindBySeconds(float secondsToRewind)
        {
            TimeWarpManager.Instance.TriggerAction(GlobalAction.Rewind, duration: secondsToRewind);
        }

        public void CancelAnyAction()
        {
            TimeWarpManager.Instance.StopGlobalAction();
        }

        // ==========================================
        // NEW: Waking up all Ghosts!
        // ==========================================
        public void TriggerAllWaitingGhosts()
        {
            // FIXED: Removed deprecated FindObjectsSortMode.None. 
            // Used FindObjectsInactive.Exclude to target only active ghosts in the scene.
            var allGhosts = FindObjectsByType<TimeWarp.Modules.Paradox.TimeEchoGhost>(FindObjectsInactive.Exclude);
            
            foreach (var ghost in allGhosts)
            {
                ghost.StartPlayback();
            }
            
            Debug.Log($"[TimeWarp] Woke up and started {allGhosts.Length} waiting Ghosts!");
        }
    }
}