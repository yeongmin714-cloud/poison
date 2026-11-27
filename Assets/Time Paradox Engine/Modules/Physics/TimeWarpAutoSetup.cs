using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX; 
using UnityEngine.AI; 
using Unity.Cinemachine;
using TimeWarp.Core;
using TimeWarp.Core.Data;
using TimeWarp.Modules.Physics.Dynamics;
using TimeWarp.Modules.Particles;
using TimeWarp.Modules.Visuals;
using TimeWarp.Modules.Gameplay; 
using TimeWarp.Modules.Animation;
using TimeWarp.Modules.Navigation; 
using TimeWarp.Modules.Paradox;
using TimeWarp.Modules.Adapters;
using TimeWarp.Modules.Physics.Joints;
using TimeWarp.Modules.Cameras;
using TimeWarp.Modules.Zones;

namespace TimeWarp.Modules.Physics
{
    /// <summary>
    /// Scans the scene and automatically injects required TimeWarp components 
    /// based on user-defined Rules.
    /// </summary>
    [DefaultExecutionOrder(-99)] 
    public class TimeWarpAutoSetup : MonoBehaviour
    {
        public static TimeWarpAutoSetup Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }
        
        [Header("Auto Setup Settings")]
        [Tooltip("Should the system scan the scene and apply rules automatically on Awake?")]
        public bool ScanSceneOnAwake = true;

        [Header("Auto Assignment Rules")]
        [Tooltip("List of rules defining how should objects be processed by the TimeWarp system.")]
        public List<TimeWarpRule> Rules = new List<TimeWarpRule>();

        private void Awake()
        {
            if (Instance != null && Instance != this) 
            { 
                Destroy(gameObject); 
                return; 
            }
            Instance = this;
        }

        private void Start() 
        { 
            if (ScanSceneOnAwake) RunFullScan(); 
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Gathers all potential targets in the scene and checks them against the rules.
        /// Evaluates all Transforms, ensuring inactive objects and pure logic triggers 
        /// (like PermissionZones) are included in the setup process.
        /// </summary>
        public void RunFullScan()
        {
            if (Rules.Count == 0) return;

            // PERFORMANCE FIX & ACCURACY: Grab absolutely all objects, including inactive ones.
            // FIXED: Removed deprecated FindObjectsSortMode.None
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            
            foreach (var t in allTransforms)
            {
                ApplyRules(t.gameObject);
            }
        }

        /// <summary>
        /// Evaluates a single GameObject against all rules and applies the first matching one.
        /// </summary>
        public void ApplyRules(GameObject target)
        {
            // Skip if the object already has a TimeWarpIdentity attached
            if (target.GetComponent<TimeWarpIdentity>() != null) return;

            foreach (var rule in Rules)
            {
                if (rule.Matches(target)) 
                {
                    InjectComponents(target, rule);
                    break; 
                }
            }
        }

        /// <summary>
        /// Physically attaches all required TimeWarp modules to the GameObject based on the matching rule.
        /// </summary>
        private void InjectComponents(GameObject target, TimeWarpRule rule)
        {
            TimeWarpIdentity identity = target.GetComponent<TimeWarpIdentity>();
            if (identity == null) identity = target.AddComponent<TimeWarpIdentity>();
            
            // Deep copy settings from the rule to the identity, including PermissionZones
            rule.ApplyToIdentity(identity);

            // =========================
            // INJECT 3D PHYSICS
            // =========================
            if (target.GetComponent<Rigidbody>() != null)
            {
                if (rule.InjectTimeBody && !target.TryGetComponent(out TimeBody _)) 
                    target.AddComponent<TimeBody>();
                    
                if (rule.InjectKineticAccumulator)
                {
                    if (!target.TryGetComponent(out KineticAccumulator acc)) 
                        acc = target.AddComponent<KineticAccumulator>();
                    
                    // NEW: Injecting Sequential Hits data from the Rule
                    acc.ReleaseMode = rule.KineticReleaseMode;
                    acc.MaxForceCap = rule.MaxForceCap;
                    acc.SequentialDelay = rule.SequentialDelay;
                }
            }

            // =========================
            // INJECT 2D PHYSICS
            // =========================
            if (target.GetComponent<Rigidbody2D>() != null)
            {
                if (rule.InjectTimeBody && !target.TryGetComponent(out TimeBody2D _)) 
                    target.AddComponent<TimeBody2D>();
                    
                if (rule.InjectKineticAccumulator)
                {
                    if (!target.TryGetComponent(out KineticAccumulator2D acc2d)) 
                        acc2d = target.AddComponent<KineticAccumulator2D>();
                    
                    // NEW: Injecting Sequential Hits data from the Rule for 2D
                    acc2d.ReleaseMode = rule.KineticReleaseMode;
                    acc2d.MaxForceCap = rule.MaxForceCap;
                    acc2d.SequentialDelay = rule.SequentialDelay;
                }
            }

            // Inject AI Navigation
            if ((rule.ActiveChannels & TimeChannels.Navigation) != 0 && rule.InjectTimeNavMeshAgent)
            {
                if (target.GetComponent<NavMeshAgent>() != null && !target.TryGetComponent(out TimeNavMeshAgent _))
                {
                    target.AddComponent<TimeNavMeshAgent>();
                }
            }

            // Inject Echoes & Paradox
            if ((rule.ActiveChannels & TimeChannels.Echoes) != 0)
            {
                if (rule.InjectTimeParadoxModule && !target.TryGetComponent(out TimeParadoxModule _))
                {
                    target.AddComponent<TimeParadoxModule>();
                }
                
                if (rule.InjectTimeEventRecorder && !target.TryGetComponent(out TimeEventRecorder _))
                {
                    target.AddComponent<TimeEventRecorder>();
                }
            }

            // Inject Particles & VFX
            if ((rule.ActiveChannels & TimeChannels.Particles) != 0)
            {
                if (rule.InjectTimeParticles && target.GetComponentInChildren<ParticleSystem>() != null && !target.TryGetComponent(out TimeParticles _))
                {
                    target.AddComponent<TimeParticles>();
                }

                if (rule.InjectTimeVFX && target.GetComponentInChildren<VisualEffect>() != null && !target.TryGetComponent(out TimeVFX _))
                {
                    target.AddComponent<TimeVFX>();
                }
            }

            // Inject Audio
            if ((rule.ActiveChannels & TimeChannels.Audio) != 0 && rule.InjectTimeAudio)
            {
                if (target.GetComponentInChildren<AudioSource>() != null && !target.TryGetComponent(out TimeWarp.Modules.Audio.TimeAudio _))
                {
                    target.AddComponent<TimeWarp.Modules.Audio.TimeAudio>();
                }
            }

            // Inject Shaders
            if ((rule.ActiveChannels & TimeChannels.Shaders) != 0 && rule.InjectTimeShader)
            {
                Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
                if (targetRenderer != null)
                {
                    if (!targetRenderer.gameObject.TryGetComponent(out TimeShader ts))
                    {
                        if (targetRenderer.gameObject.GetComponent<TimeWarpIdentity>() == null)
                        {
                            var childId = targetRenderer.gameObject.AddComponent<TimeWarpIdentity>();
                            childId.Channels = TimeChannels.Shaders;
                        }
                        ts = targetRenderer.gameObject.AddComponent<TimeShader>();
                    }
                    
                    ts.Properties.Clear();
                    foreach (var p in rule.ShaderProperties)
                    {
                        ts.Properties.Add(new ShaderPropertyConfig {
                            PropertyName = p.PropertyName,
                            ReadFromMaterial = p.ReadFromMaterial,
                            ManualBaseSpeed = p.ManualBaseSpeed,
                            RewindSpeed = p.RewindSpeed,
                            UseTimeAccumulation = p.UseTimeAccumulation
                        });
                    }
                }
            }

            // Inject Custom Variables Bridge
            if ((rule.ActiveChannels & TimeChannels.Custom) != 0 && rule.CustomSettings != null && rule.CustomSettings.Bindings.Count > 0)
            {
                if (!target.TryGetComponent(out TimeWarpBridge bridge))
                {
                    bridge = target.AddComponent<TimeWarpBridge>();
                }
                
                bridge.InitializeBindings(rule.CustomSettings);
            }

            // Inject Animation
            if (rule.InjectTimeAnimator && (rule.ActiveChannels & TimeChannels.Animation) != 0)
            {
                if (target.GetComponentInChildren<Animator>() != null)
                {
                    if (!target.TryGetComponent(out TimeAnimator timeAnim)) 
                    {
                        timeAnim = target.AddComponent<TimeAnimator>();
                    }
                    
                    timeAnim.RecordMode = rule.AnimatorMode;
                    timeAnim.HandleRootMotion = rule.HandleRootMotion; 
                    timeAnim.AnimationFramerate = rule.AnimationFramerate; 
                }
            }

            // Inject Joints
            if (rule.InjectTimeHingeJoint && target.GetComponent<HingeJoint>() != null)
            {
                if (!target.TryGetComponent(out TimeHingeJoint _))
                {
                    target.AddComponent<TimeHingeJoint>();
                }
            }

            // Inject Cameras (Cinemachine)
            if (rule.InjectTimeCinemachine)
            {
                if (target.GetComponent<CinemachineBrain>() != null || target.GetComponent<CinemachineCamera>() != null)
                {
                    if (!target.TryGetComponent(out TimeCinemachineBrain _))
                    {
                        target.AddComponent<TimeCinemachineBrain>();
                    }
                }
            }

            // Inject Character Controllers
            if (rule.ControllerAdapter == CharacterAdapterType.UnityCharacterController && target.GetComponent<CharacterController>() != null)
            {
                if (!target.TryGetComponent(out TimeWarpCharacterControllerAdapter _))
                {
                    target.AddComponent<TimeWarpCharacterControllerAdapter>();
                }
            }

            // Inject Lifecycle
            if (rule.InjectTimeLifecycle)
            {
                if (!target.TryGetComponent(out TimeWarpLifecycle lifecycle))
                {
                    lifecycle = target.AddComponent<TimeWarpLifecycle>();
                }
                lifecycle.KeepInMemoryForRewind = rule.LifecycleKeepInMemory;
                lifecycle.DestroyBeforeBirth = rule.LifecycleDestroyBeforeBirth;
            }

            // Inject Logic Kill-Switch (Component Manager)
            if (rule.InjectComponentManager)
            {
                if (!target.TryGetComponent(out TimeWarpComponentManager compManager))
                {
                    compManager = target.AddComponent<TimeWarpComponentManager>();
                }

                compManager.DisableOnRewind = rule.DisableOnRewind;
                compManager.DisableOnTimeStop = rule.DisableOnTimeStop;

                if (rule.AutoDetectForeignScripts) compManager.AutoFillComponents();
                
                if (rule.SpecificComponentsToDisable != null && rule.SpecificComponentsToDisable.Count > 0)
                {
                    compManager.AddComponentsByName(rule.SpecificComponentsToDisable);
                }
            }

            // Finalize registration of modules
            identity.RefreshModules();
        }

        // ==========================================
        // EDITOR TOOLS
        // ==========================================

        [ContextMenu("🔨 Bake Rules to Scene Objects (Editor Only)")]
        private void BakeRulesInEditor()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.LogWarning("[TimeWarp] Please use this feature only in Editor mode (before pressing Play)!");
                return;
            }

            if (Rules.Count == 0) return;

            int modifiedCount = 0;

            // PERFORMANCE FIX & ACCURACY: Grab all Transforms including inactive
            // FIXED: Removed deprecated FindObjectsSortMode.None
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);

            foreach (var t in allTransforms)
            {
                GameObject target = t.gameObject;
                
                if (target.GetComponent<TimeWarpIdentity>() == null)
                {
                    foreach (var rule in Rules)
                    {
                        if (rule.Matches(target)) 
                        {
                            InjectComponents(target, rule);
                            UnityEditor.EditorUtility.SetDirty(target);
                            modifiedCount++;
                            break; 
                        }
                    }
                }
            }

            Debug.Log($"[TimeWarp] Successfully baked rules onto {modifiedCount} static objects.");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        [ContextMenu("🧹 Clear Baked Rules (Editor Only)")]
        private void ClearBakedRulesInEditor()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.LogWarning("[TimeWarp] Please use this feature only in Editor mode (before pressing Play)!");
                return;
            }

            if (!UnityEditor.EditorUtility.DisplayDialog("Clear All TimeWarp Rules", 
                "Warning! This will permanently remove all TimeWarp system scripts from ALL objects in the active scene (except the Manager and AutoSetup). Are you sure?", 
                "Yes, Clear", "Cancel"))
            {
                return;
            }

            int removedCount = 0;
            // FIXED: Removed deprecated FindObjectsSortMode.None
            var allIdentities = FindObjectsByType<TimeWarpIdentity>(FindObjectsInactive.Include);

            foreach (var identity in allIdentities)
            {
                if (identity == null) continue;
                
                GameObject obj = identity.gameObject;
                
                // Protect the AutoSetup object itself from being stripped
                if (obj == this.gameObject) continue;

                var modules = obj.GetComponents<ITimeWarpModule>();
                foreach (var mod in modules)
                {
                    DestroyImmediate((UnityEngine.Object)mod);
                }

                if (obj.TryGetComponent(out KineticAccumulator ka)) DestroyImmediate(ka);
                if (obj.TryGetComponent(out KineticAccumulator2D ka2d)) DestroyImmediate(ka2d);

                DestroyImmediate(identity);
                
                UnityEditor.EditorUtility.SetDirty(obj);
                removedCount++;
            }

            Debug.Log($"[TimeWarp] 🧹 Baked rules have been cleared! Removed the system from {removedCount} objects in the scene.");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }
    }
}