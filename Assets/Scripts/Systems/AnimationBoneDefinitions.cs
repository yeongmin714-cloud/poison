using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Describes the general shape/type of a character model for bone-mapping purposes.
    /// </summary>
    public enum CharacterType
    {
        /// <summary>Two-legged humanoid character (player, NPCs).</summary>
        Humanoid,
        /// <summary>Four-legged creature (wolf, bear, horse).</summary>
        Quadruped,
        /// <summary>Monster with a non-standard skeleton — fallback heuristic search.</summary>
        Monster
    }

    /// <summary>
    /// Static class containing bone-name constants and lookup helpers for humanoid,
    /// quadruped, and monster skeletons. Used by <see cref="AnimationRiggingSetup"/>
    /// to locate bones on rigged GLB models at runtime.
    /// </summary>
    public static class AnimationBoneDefinitions
    {
        // ──────────────────────────────────────────────
        //  Humanoid bone names
        // ──────────────────────────────────────────────

        /// <summary>All bone names for a standard humanoid skeleton, ordered roughly head-to-toe.</summary>
        public static readonly string[] HumanoidBoneNames =
        {
            "Head", "Neck", "Spine", "Chest", "Hips",
            "UpperArm_L", "UpperArm_R",
            "LowerArm_L", "LowerArm_R",
            "Hand_L", "Hand_R",
            "UpperLeg_L", "UpperLeg_R",
            "LowerLeg_L", "LowerLeg_R",
            "Foot_L", "Foot_R"
        };

        /// <summary>Alternative names commonly found in Mixamo / GLB exports for humanoids.</summary>
        private static readonly string[] HumanoidAlternates =
        {
            "head", "HEAD", "Head_Top", "Head_End",
            "neck", "NECK", "Neck_Pivot",
            "spine", "SPINE", "Spine1", "Spine2",
            "chest", "CHEST", "Chest_Pivot",
            "hips", "HIPS", "Hip", "Pelvis",
            "upperarm_l", "Upper_Arm_L", "Arm_L", "LeftArm", "Left_Arm",
            "upperarm_r", "Upper_Arm_R", "Arm_R", "RightArm", "Right_Arm",
            "lowerarm_l", "Lower_Arm_L", "Forearm_L", "Forearm_L",
            "lowerarm_r", "Lower_Arm_R", "Forearm_R", "Forearm_R",
            "hand_l", "Hand_L", "LeftHand", "Left_Hand",
            "hand_r", "Hand_R", "RightHand", "Right_Hand",
            "upperleg_l", "Upper_Leg_L", "Leg_L", "LeftLeg", "Left_Leg",
            "upperleg_r", "Upper_Leg_R", "Leg_R", "RightLeg", "Right_Leg",
            "lowerleg_l", "Lower_Leg_L", "Leg_Low_L",
            "lowerleg_r", "Lower_Leg_R", "Leg_Low_R",
            "foot_l", "Foot_L", "LeftFoot", "Left_Foot",
            "foot_r", "Foot_R", "RightFoot", "Right_Foot"
        };

        // ──────────────────────────────────────────────
        //  Quadruped bone names
        // ──────────────────────────────────────────────

        /// <summary>All bone names for a standard quadruped skeleton.</summary>
        public static readonly string[] QuadrupedBoneNames =
        {
            "Head", "Neck", "Spine", "Chest", "Hips",
            "UpperLeg_FL", "UpperLeg_FR",
            "LowerLeg_FL", "LowerLeg_FR",
            "Paw_FL", "Paw_FR",
            "UpperLeg_HL", "UpperLeg_HR",
            "LowerLeg_HL", "LowerLeg_HR",
            "Paw_HL", "Paw_HR",
            "Tail"
        };

        /// <summary>Alternative quadruped bone name variants.</summary>
        private static readonly string[] QuadrupedAlternates =
        {
            "head", "HEAD", "Head_Top",
            "neck", "NECK",
            "spine", "SPINE", "Spine1", "Spine2",
            "chest", "CHEST",
            "hips", "HIPS", "Pelvis",
            "upperleg_fl", "UpperLeg_F_L", "FrontLeg_L", "Front_Leg_L", "Arm_FL",
            "upperleg_fr", "UpperLeg_F_R", "FrontLeg_R", "Front_Leg_R", "Arm_FR",
            "lowerleg_fl", "LowerLeg_F_L", "FrontKnee_L",
            "lowerleg_fr", "LowerLeg_F_R", "FrontKnee_R",
            "paw_fl", "Paw_F_L", "Foot_FL", "Hoof_FL",
            "paw_fr", "Paw_F_R", "Foot_FR", "Hoof_FR",
            "upperleg_hl", "UpperLeg_H_L", "BackLeg_L", "Rear_Leg_L",
            "upperleg_hr", "UpperLeg_H_R", "BackLeg_R", "Rear_Leg_R",
            "lowerleg_hl", "LowerLeg_H_L", "BackKnee_L",
            "lowerleg_hr", "LowerLeg_H_R", "BackKnee_R",
            "paw_hl", "Paw_H_L", "Foot_HL", "Hoof_HL",
            "paw_hr", "Paw_H_R", "Foot_HR", "Hoof_HR",
            "tail", "TAIL", "Tail_End"
        };

        // ──────────────────────────────────────────────
        //  Monster bone names (heuristic / fallback)
        // ──────────────────────────────────────────────

        /// <summary>Monster skeletons vary widely; these are common bone groups searched heuristically.</summary>
        public static readonly string[][] MonsterBoneGroups =
        {
            new[] { "Head", "head", "HEAD", "Skull", "skull", "MonsterHead" },
            new[] { "Neck", "neck", "NECK" },
            new[] { "Spine", "spine", "SPINE", "Body", "body", "Torso", "torso" },
            new[] { "Hips", "hips", "HIPS", "Hip", "hip", "Pelvis", "pelvis" },
            new[] { "Arm_L", "arm_l", "LeftArm", "Left_Arm", "L_Arm" },
            new[] { "Arm_R", "arm_r", "RightArm", "Right_Arm", "R_Arm" },
            new[] { "Leg_L", "leg_l", "LeftLeg", "Left_Leg", "L_Leg" },
            new[] { "Leg_R", "leg_r", "RightLeg", "Right_Leg", "R_Leg" }
        };

        // ──────────────────────────────────────────────
        //  Lookup map (built once)
        // ──────────────────────────────────────────────

        private static Dictionary<string, string[]> _alternateMap;

        /// <summary>
        /// Lazily builds a lookup from canonical bone name → list of alternative names.
        /// </summary>
        private static Dictionary<string, string[]> GetAlternateMap()
        {
            if (_alternateMap != null)
                return _alternateMap;

            _alternateMap = new Dictionary<string, string[]>();

            // Humanoid alternates: every 2 entries (canonical, alt)
            for (int i = 0; i < HumanoidBoneNames.Length; i++)
            {
                var alts = new List<string>();
                int altStart = i * 4; // up to 4 alts per bone in the alternates array
                for (int j = 0; j < 4; j++)
                {
                    int idx = altStart + j;
                    if (idx < HumanoidAlternates.Length)
                        alts.Add(HumanoidAlternates[idx]);
                }
                _alternateMap[HumanoidBoneNames[i]] = alts.ToArray();
            }

            // Quadruped alternates
            for (int i = 0; i < QuadrupedBoneNames.Length; i++)
            {
                var alts = new List<string>();
                int altStart = i * 4;
                for (int j = 0; j < 4; j++)
                {
                    int idx = altStart + j;
                    if (idx < QuadrupedAlternates.Length)
                        alts.Add(QuadrupedAlternates[idx]);
                }
                _alternateMap[QuadrupedBoneNames[i]] = alts.ToArray();
            }

            return _alternateMap;
        }

        // ──────────────────────────────────────────────
        //  Public helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Attempts to find a Transform in the hierarchy of <paramref name="root"/>
        /// that matches any of the provided <paramref name="possibleNames"/>.
        /// Search is breadth-first, case-insensitive, and stops at the first match.
        /// </summary>
        /// <param name="root">The root Transform to search beneath.</param>
        /// <param name="possibleNames">One or more bone names to try.</param>
        /// <returns>The matching Transform, or null if none found.</returns>
        public static Transform TryFindBone(Transform root, string[] possibleNames)
        {
            if (root == null || possibleNames == null || possibleNames.Length == 0)
                return null;

            // Build a hash set for fast lookups
            var lookup = new HashSet<string>();
            foreach (string name in possibleNames)
            {
                if (!string.IsNullOrEmpty(name))
                    lookup.Add(name.ToLowerInvariant());
            }

            // Breadth-first search
            var queue = new Queue<Transform>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();

                if (lookup.Contains(current.name.ToLowerInvariant()))
                    return current;

                for (int i = 0; i < current.childCount; i++)
                    queue.Enqueue(current.GetChild(i));
            }

            return null;
        }

        /// <summary>
        /// Attempts to find a bone by its canonical name, checking alternates if the
        /// canonical name does not match any child.
        /// </summary>
        /// <param name="root">Root Transform to search.</param>
        /// <param name="canonicalName">The canonical bone name (e.g. "Head", "UpperArm_L").</param>
        /// <returns>The matching Transform, or null.</returns>
        public static Transform TryFindBoneCanonical(Transform root, string canonicalName)
        {
            // Try direct canonical name first
            Transform result = TryFindBone(root, new[] { canonicalName });
            if (result != null)
                return result;

            // Try alternates
            var map = GetAlternateMap();
            if (map.TryGetValue(canonicalName, out string[] alts))
            {
                result = TryFindBone(root, alts);
                if (result != null)
                    return result;
            }

            // Try a case-insensitive variant of canonical
            return TryFindBone(root, new[] { canonicalName.ToLowerInvariant(), canonicalName.ToUpperInvariant() });
        }

        /// <summary>
        /// Returns the set of bone names appropriate for the given character type.
        /// For <see cref="CharacterType.Monster"/>, returns heuristically-common bone groups
        /// flattened into a single array.
        /// </summary>
        /// <param name="type">The character type.</param>
        /// <returns>An array of bone name strings.</returns>
        public static string[] GetBoneNamesForType(CharacterType type)
        {
            switch (type)
            {
                case CharacterType.Humanoid:
                    return HumanoidBoneNames;
                case CharacterType.Quadruped:
                    return QuadrupedBoneNames;
                case CharacterType.Monster:
                {
                    var names = new List<string>();
                    foreach (var group in MonsterBoneGroups)
                    {
                        foreach (string name in group)
                        {
                            if (!names.Contains(name))
                                names.Add(name);
                        }
                    }
                    return names.ToArray();
                }
                default:
                    return HumanoidBoneNames;
            }
        }

        /// <summary>
        /// Returns alternative name variants for a given canonical bone name.
        /// </summary>
        /// <param name="canonicalName">The canonical bone name.</param>
        /// <returns>Array of alternative names, or an empty array if none known.</returns>
        public static string[] GetAlternateNames(string canonicalName)
        {
            if (string.IsNullOrEmpty(canonicalName))
                return System.Array.Empty<string>();

            var map = GetAlternateMap();
            if (map.TryGetValue(canonicalName, out string[] alts))
                return alts;

            return System.Array.Empty<string>();
        }
    }
}