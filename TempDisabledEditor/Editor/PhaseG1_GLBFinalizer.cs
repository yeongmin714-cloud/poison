using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Editor
{
    /// <summary>
    /// Phase G1-07: GLB model final swap + character shading for the Poison game.
    /// Scans the scene for remaining placeholders, swaps them with GLB models from
    /// UserProvided/, adds Animation Rigging to swapped models, and sets up
    /// character shading (rim light via material properties).
    /// </summary>
    public static class PhaseG1_GLBFinalizer
    {
        private const string UserProvidedFolder = "Assets/Resources/Models/UserProvided";
        private const string ScenePath = "Assets/Scenes/MainScene.unity";

        // Known placeholder prefixes for scene scanning
        private static readonly string[] PlaceholderPrefixes =
        {
            "Placeholder_",
            "Player"
        };

        // ================================================================
        //  Menu Items
        // ================================================================

        /// <summary>
        /// Runs ModelSwapper on all remaining placeholders in the scene.
        /// </summary>
        [MenuItem("Tools/Phase G1/Finalize GLB Swap")]
        public static void FinalizeGLBSwap()
        {
            int count = RunFullGLBSwap();
            if (count > 0)
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[PhaseG1_GLBFinalizer] ✅ GLB swap finalized: {count} models swapped.");
            EditorUtility.DisplayDialog("Phase G1-07",
                $"GLB swap finalized.\n{count} model(s) swapped.\nScene saved.", "OK");
        }

        [MenuItem("Tools/Phase G1/Finalize GLB Swap", true)]
        private static bool ValidateFinalizeGLBSwap() => true;

        /// <summary>
        /// Sets up character shading (rim light effect + PBR materials) on all
        /// swapped GLB models in the scene.
        /// </summary>
        [MenuItem("Tools/Phase G1/Setup Character Shading")]
        public static void SetupCharacterShading()
        {
            int count = ApplyCharacterShadingToScene();
            if (count > 0)
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[PhaseG1_GLBFinalizer] ✅ Character shading applied to {count} model(s).");
            EditorUtility.DisplayDialog("Phase G1-07",
                $"Character shading applied to {count} model(s).\nScene saved.", "OK");
        }

        [MenuItem("Tools/Phase G1/Setup Character Shading", true)]
        private static bool ValidateSetupCharacterShading() => true;

        /// <summary>
        /// Runs both GLB swap AND character shading in sequence.
        /// </summary>
        [MenuItem("Tools/Phase G1/Full GLB Finalize")]
        public static void FullGLBFinalize()
        {
            int swapCount = RunFullGLBSwap();
            int shadingCount = ApplyCharacterShadingToScene();

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"[PhaseG1_GLBFinalizer] ✅ Full finalize complete: {swapCount} swapped, {shadingCount} shaded.");
            EditorUtility.DisplayDialog("Phase G1-07",
                $"Full GLB finalize complete.\n{swapCount} model(s) swapped.\n{shadingCount} model(s) shaded.\nScene saved.", "OK");
        }

        [MenuItem("Tools/Phase G1/Full GLB Finalize", true)]
        private static bool ValidateFullGLBFinalize() => true;

        // ================================================================
        //  GLB Swap Logic
        // ================================================================

        /// <summary>
        /// Scans scene for all remaining placeholders and swaps them with
        /// available GLB models from UserProvided/.
        /// </summary>
        /// <returns>Number of models successfully swapped.</returns>
        private static int RunFullGLBSwap()
        {
            string folderPath = Path.Combine(Application.dataPath, "Resources/Models/UserProvided");
            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"[PhaseG1_GLBFinalizer] UserProvided folder not found: {folderPath}");
                return 0;
            }

            // Open the scene
            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            // Discover all GLB files
            var glbFiles = Directory.GetFiles(folderPath, "*.glb");
            if (glbFiles.Length == 0)
            {
                Debug.Log("[PhaseG1_GLBFinalizer] No GLB files found in UserProvided.");
                return 0;
            }

            // Build a map of available GLBs: basename (lowercase) -> assetPath
            var availableGLBs = new Dictionary<string, string>();
            foreach (var glbPath in glbFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(glbPath);
                string lowerName = fileName.ToLowerInvariant();
                if (!availableGLBs.ContainsKey(lowerName))
                {
                    availableGLBs[lowerName] = GetRelativeAssetPath(glbPath);
                }
            }

            // Find all placeholders in the scene
            var placeholders = FindAllPlaceholders();
            int swapCount = 0;

            foreach (var placeholder in placeholders)
            {
                string placeholderName = placeholder.name;
                string glbKey = ResolveGLBKeyForPlaceholder(placeholderName, availableGLBs);

                if (glbKey == null)
                {
                    Debug.Log($"[PhaseG1_GLBFinalizer] No GLB mapping for placeholder: {placeholderName}");
                    continue;
                }

                // Skip if already swapped (has SkinnedMeshRenderer children, meaning GLB is loaded)
                if (HasGLBModel(placeholder))
                {
                    Debug.Log($"[PhaseG1_GLBFinalizer] {placeholderName} already has GLB model, skipping.");
                    continue;
                }

                string assetPath = availableGLBs[glbKey];
                var loadedModel = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (loadedModel == null)
                {
                    Debug.LogWarning($"[PhaseG1_GLBFinalizer] Failed to load GLB: {assetPath}");
                    continue;
                }

                // Determine if this is a rigged model
                bool isRigged = ModelSwapperIsRigged(loadedModel);

                // Perform swap — use ModelSwapper's logic via reflection or direct call
                bool isPlayer = placeholderName == "Player";
                if (isPlayer)
                {
                    SwapPlayerPlaceholder(placeholder, loadedModel);
                }
                else
                {
                    SwapGameObject(placeholder, loadedModel, placeholderName);
                }

                // Find swapped object (after destroy + instantiate, name may persist)
                GameObject swappedObj = GameObject.Find(placeholderName);

                if (swappedObj != null)
                {
                    // Add Animation Rigging and controller for rigged models
                    if (isRigged)
                    {
                        SetupRiggingOnCharacter(swappedObj);
                    }
                }
                else if (isRigged)
                {
                    // Couldn't find swapped object — try looking for recently created objects
                    Debug.Log($"[PhaseG1_GLBFinalizer] Warning: could not find swapped '{placeholderName}', rigging may be skipped.");
                }

                swapCount++;
                Debug.Log($"[PhaseG1_GLBFinalizer] ✅ {placeholderName} → {glbKey}.glb swapped{(isRigged ? " (Rigged)" : "")}");
            }

            // Also run tiered model swap as a complement
            RunTieredSwap();

            Debug.Log($"[PhaseG1_GLBFinalizer] Complete: {swapCount} models swapped.");
            return swapCount;
        }

        /// <summary>
        /// Runs the tiered model swap via ModelSwapper's internal method.
        /// </summary>
        private static void RunTieredSwap()
        {
            // Use reflection to call ModelSwapper.SwapTieredModelsInternal
            var type = typeof(ModelSwapper);
            var method = type.GetMethod("SwapTieredModelsInternal",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, null);
                Debug.Log("[PhaseG1_GLBFinalizer] Tiered model swap completed.");
            }
        }

        /// <summary>
        /// Uses the same rigged detection logic as ModelSwapper.
        /// </summary>
        private static bool ModelSwapperIsRigged(GameObject glbPrefab)
        {
            if (glbPrefab == null) return false;

            // 1. Animator component
            Animator animator = glbPrefab.GetComponentInChildren<Animator>();
            if (animator != null) return true;

            // 2. SkinnedMeshRenderer with bones
            SkinnedMeshRenderer[] skinnedRenderers = glbPrefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in skinnedRenderers)
            {
                if (smr.bones != null && smr.bones.Length > 0)
                    return true;
            }

            // 3. Bone-like transform names
            Transform[] allTransforms = glbPrefab.GetComponentsInChildren<Transform>(true);
            int boneLikeCount = 0;
            foreach (var t in allTransforms)
            {
                if (t == glbPrefab.transform) continue;
                string name = t.name.ToLowerInvariant();
                if (name.Contains("bone") || name.Contains("armature") ||
                    name.Contains("spine") || name.Contains("head") ||
                    name.Contains("leg") || name.Contains("arm") ||
                    name.Contains("paw") || name.Contains("tail"))
                {
                    boneLikeCount++;
                }
            }

            return boneLikeCount >= 3;
        }

        /// <summary>
        /// Checks if a GameObject already has a loaded GLB model (has skinned mesh or
        /// GLB-pattern children).
        /// </summary>
        private static bool HasGLBModel(GameObject go)
        {
            // If it already has a SkinnedMeshRenderer, it's a GLB
            if (go.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                return true;

            // If it has a MeshRenderer with UserProvided material patterns
            MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (r.sharedMaterial != null &&
                    (r.sharedMaterial.name.Contains("glb") ||
                     r.sharedMaterial.name.Contains("GLB") ||
                     r.sharedMaterial.shader.name.Contains("GLTF") ||
                     r.sharedMaterial.shader.name.Contains("glTF")))
                {
                    return true;
                }
            }

            return false;
        }

        // ================================================================
        //  Placeholder Discovery
        // ================================================================

        /// <summary>
        /// Finds all placeholder GameObjects in the scene by name patterns.
        /// </summary>
        public static List<GameObject> FindAllPlaceholders()
        {
            var results = new List<GameObject>();

            // Search by placeholder prefix
            foreach (var prefix in PlaceholderPrefixes)
            {
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.scene.isLoaded && obj.name.StartsWith(prefix))
                    {
                        // Skip if already processed
                        if (!results.Contains(obj))
                            results.Add(obj);
                    }
                }
            }

            Debug.Log($"[PhaseG1_GLBFinalizer] Found {results.Count} placeholders in scene.");
            return results;
        }

        /// <summary>
        /// Resolves a GLB key for a given placeholder name by checking ModelMapping,
        /// then falling back to direct name matching.
        /// </summary>
        private static string ResolveGLBKeyForPlaceholder(string placeholderName,
            Dictionary<string, string> availableGLBs)
        {
            // Check ModelMapping (reverse lookup)
            string reverseKey = ReverseLookupMapping(placeholderName);
            if (reverseKey != null && availableGLBs.ContainsKey(reverseKey.ToLowerInvariant()))
            {
                return reverseKey.ToLowerInvariant();
            }

            // Try direct name matching: strip "Placeholder_" prefix, lowercase
            string stripped = placeholderName;
            if (stripped.StartsWith("Placeholder_"))
                stripped = stripped.Substring("Placeholder_".Length);

            string lowerStripped = stripped.ToLowerInvariant();

            // Try exact match
            if (availableGLBs.ContainsKey(lowerStripped))
                return lowerStripped;

            // Try common variations
            string[] variations = GenerateNameVariations(lowerStripped);
            foreach (var v in variations)
            {
                if (availableGLBs.ContainsKey(v))
                    return v;
            }

            return null;
        }

        /// <summary>
        /// Generates name variations for fuzzy matching between placeholder names
        /// and GLB filenames.
        /// </summary>
        private static string[] GenerateNameVariations(string name)
        {
            var variations = new List<string>();

            // Common mapping: ElectricPorcupine -> electric_spine_hedgehog
            var knownMappings = new Dictionary<string, string>
            {
                { "electricporcupine", "electric_spine_hedgehog" },
                { "swampcroc", "swamp_alligator" },
                { "forestspirit", "wooden_forest_spirit" },
                { "ogre", "swamp_ogre" },
                { "griffin", "griffon" },
                { "lord", "lord_npc" },
                { "herbred", "herb_red" },
                { "herbgreen", "herb_green" },
                { "herbblue", "herb_blue" },
                { "herbpurple", "herb_purple" },
                { "herbyellow", "herb_yellow" },
                { "herbsilver", "herb_silver" },
                { "craftingtable", "craft_blend" },
                { "cookingstation", "craft_cook" },
                { "potionheal", "potion_heal" },
                { "potionpoison", "potion_poison" },
                { "potiondrug", "potion_drug" },
                { "potionantidote", "potion_antidote" },
                { "recipebook", "recipebook" },
                { "castleblue", "blue_castle" },
                { "castlegreen", "green_castle" },
                { "castlepurple", "purple_castle" },
                { "castlered", "red_castle" },
                { "hut", "hut" },
                { "kingdom", "kingdom" },
                { "soldier", "soldier" },
                { "soldier", "soldier_rigged" },
                { "rabbit", "rabbit" },
                { "wolf", "wolf" },
                { "deer", "deer" },
                { "crow", "crow" },
                { "bat", "bat" },
                { "snake", "snake" },
                { "giantrat", "giant_rat" },
                { "slime", "slime" },
                { "golem", "golem" },
                { "firelizard", "fire_lizard" },
                { "wildtroll", "wild_troll" },
                { "banshee", "banshee" },
                { "salamander", "salamander" },
                { "shadowassassin", "shadow_assassin" },
                { "minotaur", "minotaur" },
                { "manticore", "manticore" }
            };

            if (knownMappings.TryGetValue(name, out string mapped))
            {
                variations.Add(mapped);
            }

            return variations.ToArray();
        }

        /// <summary>
        /// Performs a reverse lookup on ModelMapping to find which GLB filename
        /// maps to the given placeholder GameObject name.
        /// </summary>
        private static string ReverseLookupMapping(string placeholderName)
        {
            // Known placeholder -> GLB filename mappings (reverse of ModelMapping)
            var reverseMap = new Dictionary<string, string>
            {
                { "Player", "player_rigged" },
                { "Placeholder_Hut", "hut" },
                { "Placeholder_Castle_Blue", "blue_castle" },
                { "Placeholder_Castle_Green", "green_castle" },
                { "Placeholder_Castle_Purple", "purple_castle" },
                { "Placeholder_Castle_Red", "red_castle" },
                { "Placeholder_Kingdom", "kingdom" },
                { "Placeholder_CraftingTable", "craft_blend" },
                { "Placeholder_CookingStation", "craft_cook" },
                { "Placeholder_Lord", "npc_lord_rigged" },
                { "Placeholder_Soldier", "soldier_rigged" },
                { "Placeholder_Herb_Red", "herb_red" },
                { "Placeholder_Herb_Green", "herb_green" },
                { "Placeholder_Herb_Blue", "herb_blue" },
                { "Placeholder_Herb_Purple", "herb_purple" },
                { "Placeholder_Herb_Yellow", "herb_yellow" },
                { "Placeholder_Herb_Silver", "herb_silver" },
                { "Placeholder_Rabbit", "rabbit" },
                { "Placeholder_Wolf", "wolf" },
                { "Placeholder_Boar", "boar" },
                { "Placeholder_Deer", "deer" },
                { "Placeholder_Crow", "crow" },
                { "Placeholder_Bat", "bat" },
                { "Placeholder_Snake", "snake" },
                { "Placeholder_GiantRat", "giant_rat" },
                { "Placeholder_Slime", "slime" },
                { "Placeholder_Golem", "golem" },
                { "Placeholder_FireLizard", "fire_lizard" },
                { "Placeholder_ElectricPorcupine", "electric_spine_hedgehog" },
                { "Placeholder_SwampCroc", "swamp_alligator" },
                { "Placeholder_WildTroll", "wild_troll" },
                { "Placeholder_ForestSpirit", "wooden_forest_spirit" },
                { "Placeholder_Ogre", "swamp_ogre" },
                { "Placeholder_Banshee", "banshee" },
                { "Placeholder_Griffin", "griffon" },
                { "Placeholder_Minotaur", "minotaur" },
                { "Placeholder_Manticore", "manticore" },
                { "Placeholder_Salamander", "salamander" },
                { "Placeholder_ShadowAssassin", "shadow_assassin" },
                { "Placeholder_Potion_Heal", "potion_heal" },
                { "Placeholder_Potion_Poison", "potion_poison" },
                { "Placeholder_Potion_Drug", "potion_drug" },
                { "Placeholder_Potion_Antidote", "potion_antidote" },
                { "Placeholder_RecipeBook", "recipebook" }
            };

            reverseMap.TryGetValue(placeholderName, out string glbKey);
            return glbKey;
        }

        // ================================================================
        //  Swap Functions (mirrors ModelSwapper)
        // ================================================================

        /// <summary>
        /// Swaps a player placeholder with a GLB model as a child, preserving
        /// the Player GameObject and its components (CharacterController, etc.).
        /// </summary>
        private static void SwapPlayerPlaceholder(GameObject player, GameObject glbPrefab)
        {
            var placeholder = player.GetComponent<PlayerPlaceholder>();
            if (placeholder != null)
            {
                placeholder.ClearPlaceholder();
                Object.DestroyImmediate(placeholder);
            }

            // Remove existing Avatar children if any
            var existingAvatar = player.transform.Find("Avatar");
            if (existingAvatar != null)
            {
                Object.DestroyImmediate(existingAvatar.gameObject);
            }

            var glbInstance = Object.Instantiate(glbPrefab, player.transform);
            glbInstance.name = "Avatar";
            glbInstance.transform.localPosition = Vector3.zero;
            glbInstance.transform.localRotation = Quaternion.identity;
            glbInstance.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Swaps a non-player placeholder GameObject with a GLB model,
        /// destroying the old placeholder and instantiating the GLB in its place.
        /// </summary>
        private static void SwapGameObject(GameObject oldObj, GameObject glbPrefab, string newName)
        {
            var parent = oldObj.transform.parent;
            var pos = oldObj.transform.position;
            var rot = oldObj.transform.rotation;
            var scale = oldObj.transform.localScale;

            Object.DestroyImmediate(oldObj);

            var newObj = Object.Instantiate(glbPrefab, parent);
            newObj.name = newName;
            newObj.transform.position = pos;
            newObj.transform.rotation = rot;
            newObj.transform.localScale = scale;
        }

        // ================================================================
        //  Character Rigging Setup
        // ================================================================

        /// <summary>
        /// Sets up Animation Rigging and animation controller on a swapped character.
        /// </summary>
        private static void SetupRiggingOnCharacter(GameObject characterObject)
        {
            if (characterObject == null) return;

            // AnimationRiggingSetup
            var riggingSetup = characterObject.GetComponent<AnimationRiggingSetup>();
            if (riggingSetup == null)
            {
                riggingSetup = characterObject.AddComponent<AnimationRiggingSetup>();
            }
            riggingSetup.FindBones();
            riggingSetup.SetupRigging();

            // RigAnimationController
            var animator = characterObject.GetComponent<Animator>();
            if (animator == null)
            {
                // Add Animator if missing (RigAnimationController requires it)
                animator = characterObject.AddComponent<Animator>();
            }

            var animController = characterObject.GetComponent<RigAnimationController>();
            if (animController == null)
            {
                characterObject.AddComponent<RigAnimationController>();
            }

            Debug.Log($"[PhaseG1_GLBFinalizer] Rigging + Animation set up on '{characterObject.name}'");
        }

        // ================================================================
        //  Character Shading (Rim Light + PBR)
        // ================================================================

        /// <summary>
        /// Scans the scene for swapped GLB models and applies rim light shading
        /// and PBR material enhancements.
        /// </summary>
        /// <returns>Number of models shaded.</returns>
        private static int ApplyCharacterShadingToScene()
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != ScenePath && !string.IsNullOrEmpty(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }

            // Find all GameObjects with SkinnedMeshRenderer or MeshRenderer that
            // look like they came from a GLB swap
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            int shadedCount = 0;

            foreach (var obj in allObjects)
            {
                if (!obj.scene.isLoaded) continue;
                if (!obj.activeInHierarchy) continue;

                // Check if this object has renderers that need shading
                var skinnedRenderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
                var meshRenderers = obj.GetComponentsInChildren<MeshRenderer>();

                bool hasRenderers = (skinnedRenderers != null && skinnedRenderers.Length > 0) ||
                                    (meshRenderers != null && meshRenderers.Length > 0);

                if (!hasRenderers) continue;

                // Skip obvious environment/UI objects
                if (obj.name.Contains("Terrain") || obj.name.Contains("Water") ||
                    obj.name.Contains("UI_") || obj.name.Contains("Canvas"))
                    continue;

                // Apply shading to all renderers on this object
                bool shaded = false;
                foreach (var smr in skinnedRenderers)
                {
                    if (ApplyRimLightToRenderer(smr))
                        shaded = true;
                }
                foreach (var mr in meshRenderers)
                {
                    if (ApplyRimLightToRenderer(mr))
                        shaded = true;
                }

                if (shaded)
                    shadedCount++;
            }

            Debug.Log($"[PhaseG1_GLBFinalizer] Character shading applied to {shadedCount} objects.");
            return shadedCount;
        }

        /// <summary>
        /// Applies rim light shader properties to a renderer's materials.
        /// Sets up PBR material with rim light effect if possible.
        /// </summary>
        private static bool ApplyRimLightToRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterials == null)
                return false;

            bool anyModified = false;

            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                var mat = renderer.sharedMaterials[i];
                if (mat == null) continue;

                // Make material instance so we don't modify the original asset
                Material instancedMat;
                if (Application.isPlaying)
                {
                    instancedMat = renderer.materials[i];
                }
                else
                {
                    instancedMat = new Material(mat);
                    instancedMat.name = mat.name + "_Shaded";
                }

                bool modified = ConfigureRimLightMaterial(instancedMat, renderer);
                if (modified)
                {
                    // Assign back
                    var mats = renderer.sharedMaterials;
                    mats[i] = instancedMat;
                    renderer.sharedMaterials = mats;
                    anyModified = true;
                }
            }

            return anyModified;
        }

        /// <summary>
        /// Configures a material with rim light properties.
        /// Uses either the URP Lit shader's built-in rim/emission properties
        /// or adds custom rim light via emission color with fresnel-like falloff.
        /// </summary>
        private static bool ConfigureRimLightMaterial(Material mat, Renderer renderer)
        {
            if (mat == null) return false;

            string shaderName = mat.shader != null ? mat.shader.name : "";

            // Only configure materials using standard URP shaders or GLTF shaders
            bool isURPShader = shaderName.Contains("Universal Render Pipeline") ||
                               shaderName.Contains("URP") ||
                               shaderName.Contains("Lit");

            bool isGLTFShader = shaderName.Contains("glTF") || shaderName.Contains("GLTF");

            if (!isURPShader && !isGLTFShader)
                return false;

            bool modified = false;

            if (isURPShader)
            {
                // Set up rim light using URP Lit shader properties
                // Use emission to simulate rim light

                // Enable emission
                if (mat.HasProperty("_EmissionColor"))
                {
                    Color existingEmission = mat.GetColor("_EmissionColor");
                    if (existingEmission.maxColorComponent < 0.01f)
                    {
                        // No existing emission — set a subtle rim color
                        mat.SetColor("_EmissionColor", new Color(0.3f, 0.4f, 0.8f, 1f) * 0.5f);
                        mat.EnableKeyword("_EMISSION");
                        modified = true;
                    }
                }

                // Set smoothness for PBR feel
                if (mat.HasProperty("_Smoothness"))
                {
                    float smoothness = mat.GetFloat("_Smoothness");
                    if (smoothness < 0.2f)
                    {
                        mat.SetFloat("_Smoothness", 0.4f);
                        modified = true;
                    }
                }

                // Set metallic if available
                if (mat.HasProperty("_Metallic"))
                {
                    float metallic = mat.GetFloat("_Metallic");
                    if (metallic < 0.05f && renderer is SkinnedMeshRenderer)
                    {
                        // Organic characters get slight metallic for specular highlights
                        mat.SetFloat("_Metallic", 0.15f);
                        modified = true;
                    }
                }

                // Enable specular highlights
                if (mat.HasProperty("_SpecularHighlights"))
                {
                    mat.SetFloat("_SpecularHighlights", 1f);
                    modified = true;
                }

                // Set environment reflections
                if (mat.HasProperty("_EnvironmentReflections"))
                {
                    mat.SetFloat("_EnvironmentReflections", 1f);
                    modified = true;
                }
            }
            else if (isGLTFShader)
            {
                // GLTF shader — try to enable similar properties if available
                if (mat.HasProperty("_EmissionFactor"))
                {
                    Vector3 emission = mat.GetVector("_EmissionFactor");
                    if (emission.magnitude < 0.01f)
                    {
                        mat.SetVector("_EmissionFactor", new Vector3(0.3f, 0.4f, 0.8f) * 0.5f);
                        modified = true;
                    }
                }

                if (mat.HasProperty("_MetallicFactor"))
                {
                    float metallic = mat.GetFloat("_MetallicFactor");
                    if (metallic < 0.05f)
                    {
                        mat.SetFloat("_MetallicFactor", 0.15f);
                        modified = true;
                    }
                }

                if (mat.HasProperty("_RoughnessFactor"))
                {
                    float roughness = mat.GetFloat("_RoughnessFactor");
                    if (roughness > 0.8f)
                    {
                        mat.SetFloat("_RoughnessFactor", 0.6f);
                        modified = true;
                    }
                }
            }

            return modified;
        }

        // ================================================================
        //  Utility
        // ================================================================

        /// <summary>
        /// Converts an absolute file path to a Unity relative asset path.
        /// </summary>
        private static string GetRelativeAssetPath(string absolutePath)
        {
            if (absolutePath.StartsWith("/mnt/"))
            {
                var driveLetter = absolutePath[2];
                var rest = absolutePath.Substring(3);
                absolutePath = char.ToUpper(driveLetter) + ":" + rest.Replace('/', '\\');
            }

            absolutePath = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath;
            if (absolutePath.StartsWith(dataPath))
            {
                return "Assets" + absolutePath.Substring(dataPath.Length);
            }
            return absolutePath;
        }
    }
}