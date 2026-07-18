using UnityEngine;
using UnityEditor;
using ProjectName.Systems.Animation.Procedural;
using ProjectName.Systems.Animation.Procedural.Bones;
using ProjectName.Systems.Animation.Procedural.Debug;

namespace ProjectName.Editor
{
    /// <summary>
    /// Editor utility to set up Test_01_Player scene with procedural animation.
    /// </summary>
    public class ProceduralAnimTestSetup : EditorWindow
    {
        [MenuItem("Tools/Procedural Animation/🧪 Setup Test_01_Player")]
        public static void SetupTestPlayer()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Test_01_Player")
            {
                if (EditorUtility.DisplayDialog("Wrong Scene", 
                    "Please open Test_01_Player scene first.\nOpen it via Tools/Test Scenes/🏃 Test_01_Player", "OK"))
                {
                    // Open the scene
                    var scenePath = "Assets/Scenes/TestScenes/Test_01_Player.unity";
                    if (System.IO.File.Exists(scenePath))
                        EditorSceneManager.OpenScene(scenePath);
                }
                return;
            }

            SetupProceduralPlayer();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ProceduralAnimTestSetup] ✅ Test_01_Player configured for procedural animation");
        }

        static void SetupProceduralPlayer()
        {
            // Find or create test player setup
            var setup = FindObjectOfType<TestPlayerSetup>();
            if (setup == null)
            {
                var go = new GameObject("_TestPlayerSetup");
                setup = go.AddComponent<TestPlayerSetup>();
            }

            // Find player model
            var playerModel = GameObject.Find("PlayerModel");
            if (playerModel == null)
            {
                // Try to find any rigged model
                var animators = FindObjectsOfType<Animator>();
                foreach (var a in animators)
                {
                    if (a.gameObject.name.Contains("Player") || a.gameObject.name.Contains("Model"))
                    {
                        playerModel = a.gameObject;
                        break;
                    }
                }
            }

            if (playerModel != null)
            {
                // Add procedural animation components
                AddProceduralComponents(playerModel);
                Debug.Log($"[ProceduralAnimTestSetup] Added procedural components to {playerModel.name}");
            }
            else
            {
                Debug.LogWarning("[ProceduralAnimTestSetup] No player model found in scene");
            }
        }

        static void AddProceduralComponents(GameObject model)
        {
            // 1. ProceduralBoneMap
            var boneMap = model.GetComponent<ProceduralBoneMap>();
            if (boneMap == null)
                boneMap = model.AddComponent<ProceduralBoneMap>();

            // 2. ProceduralAnimationController
            var controller = model.GetComponent<ProceduralAnimationController>();
            if (controller == null)
                controller = model.AddComponent<ProceduralAnimationController>();

            // 3. ProceduralAnimStateMachine
            var stateMachine = model.GetComponent<ProceduralAnimStateMachine>();
            if (stateMachine == null)
                stateMachine = model.AddComponent<ProceduralAnimStateMachine>();

            // 4. Debug registrar
            var debug = model.GetComponent<ProceduralAnimDebugRegistrar>();
            if (debug == null)
                debug = model.AddComponent<ProceduralAnimDebugRegistrar>();

            // 5. Ensure Rigidbody
            var rb = model.GetComponent<Rigidbody>();
            if (rb == null)
                rb = model.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 6. Ensure Animator
            var anim = model.GetComponent<Animator>();
            if (anim == null)
                anim = model.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.updateMode = AnimatorUpdateMode.Fixed;
            anim.animatePhysics = true;

            // 7. Remove old clip-based components
            RemoveLegacyComponents(model);
        }

        static void RemoveLegacyComponents(GameObject model)
        {
            // Remove old animation components
            var legacyComponents = new[]
            {
                typeof(ProceduralPoseController),
                typeof(RigAnimationController),
                typeof(ModelAnimatorAssigner),
            };

            foreach (var type in legacyComponents)
            {
                var comp = model.GetComponent(type);
                if (comp != null)
                {
                    Debug.Log($"[ProceduralAnimTestSetup] Removing legacy component: {type.Name}");
                    DestroyImmediate(comp);
                }
            }

            // Also remove from children
            foreach (var type in legacyComponents)
            {
                var comps = model.GetComponentsInChildren(type, true);
                foreach (var comp in comps)
                    DestroyImmediate(comp);
            }
        }

        [MenuItem("Tools/Procedural Animation/🧹 Remove Legacy Anim Components")]
        public static void RemoveLegacyAnimComponents()
        {
            var models = FindObjectsOfType<Animator>();
            foreach (var anim in models)
            {
                RemoveLegacyComponents(anim.gameObject);
            }
            Debug.Log("[ProceduralAnimTestSetup] Legacy animation components removed");
        }

        [MenuItem("Tools/Procedural Animation/🔍 Validate Scene Setup")]
        public static void ValidateSceneSetup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Debug.Log($"[ProceduralAnimTestSetup] Validating scene: {scene.name}");

            var controllers = FindObjectsOfType<ProceduralAnimationController>();
            Debug.Log($"  ProceduralAnimationController: {controllers.Length}");

            var stateMachines = FindObjectsOfType<ProceduralAnimStateMachine>();
            Debug.Log($"  ProceduralAnimStateMachine: {stateMachines.Length}");

            var boneMaps = FindObjectsOfType<ProceduralBoneMap>();
            Debug.Log($"  ProceduralBoneMap: {boneMaps.Length}");

            var debugRegs = FindObjectsOfType<ProceduralAnimDebugRegistrar>();
            Debug.Log($"  ProceduralAnimDebugRegistrar: {debugRegs.Length}");

            var legacyPoses = FindObjectsOfType<ProceduralPoseController>();
            Debug.Log($"  Legacy ProceduralPoseController: {legacyPoses.Length} (should be 0)");

            var legacyRigs = FindObjectsOfType<RigAnimationController>();
            Debug.Log($"  Legacy RigAnimationController: {legacyRigs.Length} (should be 0)");
        }
    }
}