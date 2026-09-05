using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// Repairs the Humanoid avatar mapping for Player_Rigged_Heat.fbx.
    ///
    /// Problem: the FBX was imported with an empty humanDescription.human list
    /// (human: [] in the .meta), so Unity built an avatar with NO humanoid bone
    /// mapping. Every Humanoid animation retargeted onto this rig freezes.
    ///
    /// Fix: explicitly write the 22 standard Humanoid bone mappings
    /// (boneName == humanName for this rig) into ModelImporter.humanDescription
    /// and reimport.
    ///
    /// Usage:
    ///   Tools → Anim → Apply Heat Avatar Mapping (Explicit)
    ///   Tools → Anim → Dump Heat Avatar Mapping (diagnostics)
    /// </summary>
    public static class HeatAvatarMappingFix
    {
        // ──────────────────────────────────────────────
        // Constants
        // ──────────────────────────────────────────────

        private const string HeatFbxPath =
            "Assets/Resources/Models/UserProvided/fbx/Player_Rigged_Heat.fbx";

        /// <summary>
        /// The 22 explicit Humanoid mappings for the Heat rig.
        /// Order: canonical (Hips first, toes last).
        /// Excluded on purpose: Chest2, breast.L/R, pelvis.L/R (non-humanoid bones).
        /// </summary>
        private static readonly string[] HumanBones =
        {
            "Hips",
            "Spine",
            "Chest",
            "UpperChest",
            "Neck",
            "Head",
            "LeftShoulder",
            "RightShoulder",
            "LeftUpperArm",
            "RightUpperArm",
            "LeftLowerArm",
            "RightLowerArm",
            "LeftHand",
            "RightHand",
            "LeftUpperLeg",
            "RightUpperLeg",
            "LeftLowerLeg",
            "RightLowerLeg",
            "LeftFoot",
            "RightFoot",
            "LeftToes",
            "RightToes"
        };

        // ──────────────────────────────────────────────
        // Menu: apply mapping
        // ──────────────────────────────────────────────

        [MenuItem("Tools/Anim/Apply Heat Avatar Mapping (Explicit)")]
        public static void ApplyHeatAvatarMapping()
        {
            ModelImporter importer = GetHeatImporter();
            if (importer == null)
                return;

            // Humanoid rig is required for the human mapping to take effect.
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                Debug.LogWarning(
                    $"[HeatAvatarMappingFix] animationType was {importer.animationType}; forcing Humanoid.");
                importer.animationType = ModelImporterAnimationType.Human;
            }

            HumanDescription description = importer.humanDescription;

            var bones = new List<HumanDescriptionBone>(HumanBones.Length);
            foreach (string bone in HumanBones)
            {
                bones.Add(new HumanDescriptionBone
                {
                    boneName = bone,
                    humanName = bone,
                    limit = DefaultLimit()
                });
            }

            description.human = bones.ToArray();

            // Leave description.skeleton and all twist/stretch values untouched:
            // modifying the struct in place preserves them.
            importer.humanDescription = description;
            importer.SaveAndReimport();

            Debug.Log(
                $"[HeatAvatarMappingFix] Applied {bones.Count} humanoid bone mappings to '{HeatFbxPath}' and reimported. " +
                "Verify with Tools → Anim → Dump Heat Avatar Mapping.");
        }

        // ──────────────────────────────────────────────
        // Menu: dump mapping (diagnostics)
        // ──────────────────────────────────────────────

        [MenuItem("Tools/Anim/Dump Heat Avatar Mapping")]
        public static void DumpHeatAvatarMapping()
        {
            ModelImporter importer = GetHeatImporter();
            if (importer == null)
                return;

            HumanDescription description = importer.humanDescription;
            HumanDescriptionBone[] human = description.human;

            var sb = new StringBuilder();
            sb.AppendLine(
                $"[HeatAvatarMappingFix] '{HeatFbxPath}' — animationType={importer.animationType}, " +
                $"avatarSetup={importer.avatarSetup}, human entries={human.Length}, " +
                $"skeleton entries={description.skeleton.Length}");

            if (human.Length == 0)
            {
                sb.AppendLine("  human: []  ← EMPTY! Run 'Apply Heat Avatar Mapping (Explicit)'.");
                Debug.LogWarning(sb.ToString());
                return;
            }

            for (int i = 0; i < human.Length; i++)
            {
                sb.AppendLine($"  [{i:D2}] boneName='{human[i].boneName}' → humanName='{human[i].humanName}'");
            }

            Debug.Log(sb.ToString());
        }

        // ──────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────

        private static ModelImporter GetHeatImporter()
        {
            var importer = AssetImporter.GetAtPath(HeatFbxPath) as ModelImporter;
            if (importer == null)
                Debug.LogError(
                    $"[HeatAvatarMappingFix] ModelImporter not found at '{HeatFbxPath}'. " +
                    "Check that the FBX exists and is imported.");
            return importer;
        }

        private static HumanLimit DefaultLimit()
        {
            // Default (unmodified) limits — matches the zero-valued limit blocks
            // serialized in the .meta files.
            return new HumanLimit
            {
                useDefaultValues = true,
                min = Vector3.zero,
                max = Vector3.zero,
                value = Vector3.zero,
                length = 0f,
                modified = false
            };
        }
    }
}
