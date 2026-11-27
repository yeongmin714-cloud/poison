using System.Collections.Generic;
using UnityEngine;

namespace TimeWarp.Modules.Zones.Profiles
{
    public enum TimeEffectType { SlowMotion, Stasis, Rewind }
    public enum ExitCondition { OnLeaveTrigger, Timed, ManualOrExternal }

    [CreateAssetMenu(fileName = "NewTimeProfile", menuName = "TimeWarp/Enhanced Time Profile")]
    public class TimeProfile : ScriptableObject
    {
        public TimeEffectType EffectType = TimeEffectType.SlowMotion;
        
        [Header("Time Settings (SlowMotion)")]
        public float TargetTimeScale = 0.2f;
        public bool UseRandomScale = false;
        public Vector2 RandomScaleRange = new Vector2(0.1f, 0.4f);

        [Header("Rewind Specific")]
        [Tooltip("How fast the object's tape rewinds (1.0 = normal backward speed)")]
        public float RewindSpeed = 1.0f;

        [Header("Transition")]
        public AnimationCurve BlendInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float BlendInDuration = 0.5f;
        public AnimationCurve BlendOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float BlendOutDuration = 0.5f;

        [Header("Logic & Durations")]
        public ExitCondition ExitRule = ExitCondition.OnLeaveTrigger;
        
        [Tooltip("If Timed: how many seconds the effect lasts after entry. If OnLeave: how many seconds the effect persists after leaving the zone.")]
        public float Duration = 2.0f; 
        
        [Tooltip("If enabled, the zone acts as a toggle. Entering turns the effect on, entering again turns it off.")]
        public bool ToggleOnEnter = false;
        
        [Header("Stasis Specific")]
        public bool AccumulateDamage = true;
        public float ReleaseForceMultiplier = 1.0f;

        public LayerMask AffectedLayers = ~0;

        [Tooltip("OPTIONAL tag filter, combined with Affected Layers using OR.\n\n" +
                 "Leave empty and only the layer mask matters (existing behaviour).\n" +
                 "Layers are a scarce resource - only 32 exist and most projects need them " +
                 "for the collision matrix - so filtering by tag lets you target objects " +
                 "without spending one.\n\n" +
                 "To filter purely by tag, set Affected Layers to Nothing.")]
        public List<string> AffectedTags = new List<string>();

        
        public float GetTargetScale() 
        {
            return UseRandomScale ? Random.Range(RandomScaleRange.x, RandomScaleRange.y) : TargetTimeScale;
        }
    }
}