using UnityEngine;
using TimeWarp.Core.Data;

namespace TimeWarp.Modules.Physics
{
    // This script allows you to create preset files directly from the Unity menu
    [CreateAssetMenu(fileName = "New TimeWarp Rule Preset", menuName = "TimeWarp/Rule Preset")]
    public class TimeWarpRulePreset : ScriptableObject
    {
        public TimeWarpRule Rule;
    }
}