using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;
using TimeWarp.Modules.Paradox;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TimeWarp.Gameplay
{
    /// <summary>
    /// Minimal example of driving the engine from player input.
    ///
    /// INPUT COMPATIBILITY: works with the legacy Input Manager, the new Input System
    /// package, or both. Unity defines ENABLE_LEGACY_INPUT_MANAGER / ENABLE_INPUT_SYSTEM
    /// automatically from Player Settings > Active Input Handling, so this compiles and
    /// runs in every configuration without the user changing any project setting.
    /// </summary>
    public class RewindTester : MonoBehaviour
    {
        // ==========================================
        // INPUT ABSTRACTION
        // One place to read keys, so the logic below stays readable.
        // ==========================================

        private static bool KeyPressedThisFrame(int digit)
        {
#if ENABLE_INPUT_SYSTEM
            Key key = DigitToKey(digit);
            if (Keyboard.current != null && key != Key.None && Keyboard.current[key].wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha0 + digit)) return true;
#endif
            return false;
        }

        private static bool KeyReleasedThisFrame(int digit)
        {
#if ENABLE_INPUT_SYSTEM
            Key key = DigitToKey(digit);
            if (Keyboard.current != null && key != Key.None && Keyboard.current[key].wasReleasedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyUp(KeyCode.Alpha0 + digit)) return true;
#endif
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        private static Key DigitToKey(int digit)
        {
            switch (digit)
            {
                case 1: return Key.Digit1;
                case 2: return Key.Digit2;
                case 3: return Key.Digit3;
                case 4: return Key.Digit4;
                default: return Key.None;
            }
        }
#endif

        // ==========================================
        // DEMO LOGIC
        // ==========================================

        private void Update()
        {
            // Guard: the manager may not exist yet (or at all) in this scene.
            if (TimeWarpManager.Instance == null) return;

            // 1. REWIND TIME / GHOST RECORDING (Hold 1)
            if (KeyPressedThisFrame(1))
            {
                TimeWarpManager.Instance.TriggerAction(GlobalAction.Rewind);
                Debug.Log("[Tester] Rewinding...");
            }
            else if (KeyReleasedThisFrame(1))
            {
                TimeWarpManager.Instance.StopGlobalAction();
                // The clone is born exactly when you release the 1 key (end of anomaly)
                Debug.Log("[Tester] End of rewind! If the object had Paradox enabled, a dormant Ghost just appeared on the map.");
            }

            // 2. SLOW MOTION (Toggle on 2)
            if (KeyPressedThisFrame(2))
            {
                if (TimeWarpManager.Instance.GlobalTimeState == TimeState.SlowMotion)
                    TimeWarpManager.Instance.StopGlobalAction();
                else
                    TimeWarpManager.Instance.TriggerAction(GlobalAction.SlowMotion, targetScale: 0.2f);
            }

            // 3. TIME STOP (With a 10-second duration skill on 3)
            if (KeyPressedThisFrame(3))
            {
                TimeWarpManager.Instance.TriggerAction(GlobalAction.TimeStop, duration: 10.0f);
            }

            // 4. WAKING UP THE GHOST TEAM (Key 4)
            if (KeyPressedThisFrame(4))
            {
                var allGhosts = FindObjectsByType<TimeEchoGhost>(FindObjectsInactive.Exclude);

                int triggeredCount = 0;
                foreach (var ghost in allGhosts)
                {
                    ghost.StartPlayback();
                    triggeredCount++;
                }

                if (triggeredCount > 0)
                {
                    Debug.Log($"[Tester] ACTION! Woke up and started {triggeredCount} Ghosts at the same time!");
                }
                else
                {
                    Debug.LogWarning("[Tester] No dormant Ghosts. Run around first, hold '1', release, and repeat this a few times!");
                }
            }
        }
    }
}
