using UnityEngine;
using TimeWarp.Core;

namespace TimeWarp.Modules.Gameplay
{
    /// <summary>
    /// Synchronizes time anomalies (SlowMo, Stasis, Rewind) across a group of connected objects.
    /// Essential for multi-part entities like ragdolls, segmented bosses, or vehicles to prevent 
    /// physics joints from tearing apart when only one part enters a TimeWarpZone.
    /// </summary>
    [AddComponentMenu("TimeWarp/Gameplay/Time Warp Group Sync")]
    public class TimeWarpGroupSync : MonoBehaviour
    {
        [Tooltip("Assign all the individual segments (TimeWarpIdentity scripts) of your multi-part object here.")]
        public TimeWarpIdentity[] GroupMembers;

        /// <summary>
        /// LateUpdate is crucial here. It ensures synchronization happens ONLY AFTER 
        /// all TimeWarpZones have finished calculating their local overlap math during Update().
        /// </summary>
        private void LateUpdate()
        {
            if (GroupMembers == null || GroupMembers.Length == 0) return;

            float minTimeScale = 1f;
            float maxOffset = 0f;

            // ==========================================
            // 1. SCOUTING PHASE
            // Find the strongest anomaly affecting any member of the group.
            // Example: If the right arm gets hit by a Time Stop zone, its LocalTimeScale drops to 0.
            // ==========================================
            for (int i = 0; i < GroupMembers.Length; i++)
            {
                if (GroupMembers[i] == null) continue;

                // Find the slowest time scale (closest to 0)
                if (GroupMembers[i].LocalTimeScale < minTimeScale)
                {
                    minTimeScale = GroupMembers[i].LocalTimeScale;
                }

                // Find the strongest rewind effect (highest offset)
                if (GroupMembers[i].FractionalOffset > maxOffset)
                {
                    maxOffset = GroupMembers[i].FractionalOffset;
                }
            }

            // ==========================================
            // 2. SYNCHRONIZATION PHASE
            // Force the strongest anomaly onto the ENTIRE group.
            // This ensures all connected parts freeze, slow down, or rewind in the exact same frame.
            // ==========================================
            for (int i = 0; i < GroupMembers.Length; i++)
            {
                if (GroupMembers[i] == null) continue;

                GroupMembers[i].LocalTimeScale = minTimeScale;
                GroupMembers[i].FractionalOffset = maxOffset;
            }
        }

        // ==========================================
        // EDITOR TOOLS & UTILITIES
        // ==========================================

        /// <summary>
        /// Automatically finds and assigns all TimeWarpIdentities in this object and its children.
        /// Extremely useful for quickly setting up complex ragdolls without manual drag-and-dropping.
        /// </summary>
        [ContextMenu("Auto-Fill: Find Group Members")]
        public void AutoFillMembers()
        {
            GroupMembers = GetComponentsInChildren<TimeWarpIdentity>(true);
            Debug.Log($"[TimeWarpGroupSync] Found and assigned {GroupMembers.Length} members to the group.");
        }
    }
}