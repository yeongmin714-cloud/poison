using System;
using System.Collections.Generic;
using UnityEngine;
using TimeWarp.Modules.Zones; 

namespace TimeWarp.Core.Data
{
    [Serializable]
    public class TimeWarpActionFilters
    {
        [Header("Global Action Allowances")]
        public bool AllowRewind = true;
        public bool AllowTimeStop = true;
        public bool AllowSlowMo = true;

        [Space(10)]
        [Header("Scale & Speed Overrides")]
        public bool OverrideSlowMoScale = false;
        [Range(0.01f, 1f)] public float CustomSlowMoScale = 0.5f;

        public bool OverrideRewindSpeed = false;
        [Range(0.1f, 5f)] public float CustomRewindSpeedMultiplier = 1.0f;

        [Space(10)]
        [Header("Granular Zone Permissions")]
        public bool RequiresZoneForRewind = false;
        public List<TimeWarpPermissionZone> RewindZones = new List<TimeWarpPermissionZone>();

        public bool RequiresZoneForSlowMo = false;
        public List<TimeWarpPermissionZone> SlowMoZones = new List<TimeWarpPermissionZone>();

        public bool RequiresZoneForTimeStop = false;
        public List<TimeWarpPermissionZone> TimeStopZones = new List<TimeWarpPermissionZone>();

        [Space(10)]
        [Header("Zone Restrictions (Culling Optimization)")]
        public bool RecordOnlyInZone = false;
        public List<TimeWarpPermissionZone> RecordingZones = new List<TimeWarpPermissionZone>();
    }
}