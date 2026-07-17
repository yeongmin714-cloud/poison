using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 프로시저럴 애니메이션용 본 유틸리티 (정적 클래스).
    /// 본 역할 열거형, 이름 매핑, 자동 감지 기능 제공.
    /// </summary>
    public static class ProceduralBoneUtility
    {
        // ──────────────────────────────────────────────
        // 본 역할 열거형 (시스템 내부 표준 이름)
        // ──────────────────────────────────────────────
        public enum BoneRole
        {
            // Root
            Root,           // Hips / Pelvis / Root
            Hip,            // Hips (Root와 동일하거나 자식)

            // Spine chain (root → head)
            Spine0,         // Lower spine
            Spine1,         // Mid spine
            Spine2,         // Upper spine / Chest
            Spine3,         // Neck base
            Neck,           // Neck
            Head,           // Head

            // Left Arm
            L_Clavicle,     // Left clavicle / collar
            L_Shoulder,     // Left shoulder / upper arm
            L_Elbow,        // Left elbow / lower arm
            L_Wrist,        // Left wrist / hand
            L_Hand,         // Left hand (end effector)
            L_Fingers,      // Optional: finger root

            // Right Arm
            R_Clavicle,
            R_Shoulder,
            R_Elbow,
            R_Wrist,
            R_Hand,
            R_Fingers,

            // Left Leg
            L_Hip,          // Left hip / thigh root
            L_Knee,         // Left knee / shin
            L_Ankle,        // Left ankle / foot
            L_Foot,         // Left foot (end effector)
            L_Toes,         // Optional: toes

            // Right Leg
            R_Hip,
            R_Knee,
            R_Ankle,
            R_Foot,
            R_Toes,
        }

        // ──────────────────────────────────────────────
        // 매핑 테이블 (소문자 키 → BoneRole)
        // ──────────────────────────────────────────────
        private static readonly Dictionary<string, BoneRole> _nameToRole = new Dictionary<string, BoneRole>
        {
            // Root / Hip variants
            { "root", BoneRole.Root },
            { "hips", BoneRole.Root },
            { "pelvis", BoneRole.Root },
            { "hip", BoneRole.Hip },
            { "hip.l", BoneRole.L_Hip },
            { "hip.r", BoneRole.R_Hip },
            { "leftuplexg", BoneRole.L_Hip },
            { "rightuplexg", BoneRole.R_Hip },

            // Spine chain
            { "spine", BoneRole.Spine0 },
            { "spine1", BoneRole.Spine0 },
            { "spine2", BoneRole.Spine1 },
            { "spine3", BoneRole.Spine2 },
            { "spine4", BoneRole.Spine3 },
            { "chest", BoneRole.Spine2 },
            { "upperchest", BoneRole.Spine3 },
            { "neck", BoneRole.Neck },
            { "neck1", BoneRole.Neck },
            { "head", BoneRole.Head },

            // Left Arm
            { "clavicle.l", BoneRole.L_Clavicle },
            { "clavicle_l", BoneRole.L_Clavicle },
            { "leftshoulder", BoneRole.L_Clavicle },
            { "shoulder.l", BoneRole.L_Shoulder },
            { "shoulder_l", BoneRole.L_Shoulder },
            { "upperarm.l", BoneRole.L_Shoulder },
            { "upperarm_l", BoneRole.L_Shoulder },
            { "arm.l", BoneRole.L_Shoulder },
            { "arm_l", BoneRole.L_Shoulder },
            { "elbow.l", BoneRole.L_Elbow },
            { "elbow_l", BoneRole.L_Elbow },
            { "forearm.l", BoneRole.L_Elbow },
            { "forearm_l", BoneRole.L_Elbow },
            { "lowerarm.l", BoneRole.L_Elbow },
            { "lowerarm_l", BoneRole.L_Elbow },
            { "wrist.l", BoneRole.L_Wrist },
            { "wrist_l", BoneRole.L_Wrist },
            { "hand.l", BoneRole.L_Hand },
            { "hand_l", BoneRole.L_Hand },
            { "lefthand", BoneRole.L_Hand },
            { "fingers.l", BoneRole.L_Fingers },

            // Right Arm
            { "clavicle.r", BoneRole.R_Clavicle },
            { "clavicle_r", BoneRole.R_Clavicle },
            { "rightshoulder", BoneRole.R_Clavicle },
            { "shoulder.r", BoneRole.R_Shoulder },
            { "shoulder_r", BoneRole.R_Shoulder },
            { "upperarm.r", BoneRole.R_Shoulder },
            { "upperarm_r", BoneRole.R_Shoulder },
            { "arm.r", BoneRole.R_Shoulder },
            { "arm_r", BoneRole.R_Shoulder },
            { "elbow.r", BoneRole.R_Elbow },
            { "elbow_r", BoneRole.R_Elbow },
            { "forearm.r", BoneRole.R_Elbow },
            { "forearm_r", BoneRole.R_Elbow },
            { "lowerarm.r", BoneRole.R_Elbow },
            { "lowerarm_r", BoneRole.R_Elbow },
            { "wrist.r", BoneRole.R_Wrist },
            { "wrist_r", BoneRole.R_Wrist },
            { "hand.r", BoneRole.R_Hand },
            { "hand_r", BoneRole.R_Hand },
            { "righthand", BoneRole.R_Hand },
            { "fingers.r", BoneRole.R_Fingers },

            // Left Leg
            { "thigh.l", BoneRole.L_Hip },
            { "thigh_l", BoneRole.L_Hip },
            { "upleg.l", BoneRole.L_Hip },
            { "upleg_l", BoneRole.L_Hip },
            { "upperleg.l", BoneRole.L_Hip },
            { "upperleg_l", BoneRole.L_Hip },
            { "knee.l", BoneRole.L_Knee },
            { "knee_l", BoneRole.L_Knee },
            { "leg.l", BoneRole.L_Knee },
            { "leg_l", BoneRole.L_Knee },
            { "shin.l", BoneRole.L_Knee },
            { "shin_l", BoneRole.L_Knee },
            { "calf.l", BoneRole.L_Knee },
            { "calf_l", BoneRole.L_Knee },
            { "ankle.l", BoneRole.L_Ankle },
            { "ankle_l", BoneRole.L_Ankle },
            { "foot.l", BoneRole.L_Foot },
            { "foot_l", BoneRole.L_Foot },
            { "toes.l", BoneRole.L_Toes },
            { "toes_l", BoneRole.L_Toes },

            // Right Leg
            { "thigh.r", BoneRole.R_Hip },
            { "thigh_r", BoneRole.R_Hip },
            { "upleg.r", BoneRole.R_Hip },
            { "upleg_r", BoneRole.R_Hip },
            { "upperleg.r", BoneRole.R_Hip },
            { "upperleg_r", BoneRole.R_Hip },
            { "knee.r", BoneRole.R_Knee },
            { "knee_r", BoneRole.R_Knee },
            { "leg.r", BoneRole.R_Knee },
            { "leg_r", BoneRole.R_Knee },
            { "shin.r", BoneRole.R_Knee },
            { "shin_r", BoneRole.R_Knee },
            { "calf.r", BoneRole.R_Knee },
            { "calf_r", BoneRole.R_Knee },
            { "ankle.r", BoneRole.R_Ankle },
            { "ankle_r", BoneRole.R_Ankle },
            { "foot.r", BoneRole.R_Foot },
            { "foot_r", BoneRole.R_Foot },
            { "toes.r", BoneRole.R_Toes },
            { "toes_r", BoneRole.R_Toes },

            // Numbered bones (bone_0, bone_1...) - handled separately in BuildMap
        };

        // ──────────────────────────────────────────────
        // 공개 API
        // ──────────────────────────────────────────────

        /// <summary>
        /// 본 이름에서 역할 감지 (대소문자 무시, 접미사 제거).
        /// </summary>
        public static BoneRole DetectRoleFromName(string boneName)
        {
            string key = boneName.ToLowerInvariant();

            // Remove common suffixes/prefixes
            key = key.Replace(".l", "").Replace(".r", "")
                     .Replace("_l", "").Replace("_r", "")
                     .Replace("left", "").Replace("right", "")
                     .Replace(".left", "").Replace(".right", "");

            if (_nameToRole.TryGetValue(key, out BoneRole role))
                return role;

            return BoneRole.Root; // Unknown -> Root fallback
        }

        /// <summary>
        /// Animator에서 본 역할별 매핑 자동 구성.
        /// 반환: BoneRole → Transform 매핑 (없는 역할은 null)
        /// </summary>
        public static Dictionary<BoneRole, Transform> BuildMap(Animator animator)
        {
            var map = new Dictionary<BoneRole, Transform>();
            foreach (BoneRole role in System.Enum.GetValues(typeof(BoneRole)))
                map[role] = null;

            if (animator == null || animator.transform == null)
                return map;

            var allTransforms = animator.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t == animator.transform) continue;

                var role = DetectRoleFromName(t.name);
                if (role != BoneRole.Root && !map.ContainsKey(role))
                {
                    map[role] = t;
                }
            }

            // Root 본은 Animator.rootTransform
            if (!map.ContainsKey(BoneRole.Root) || map[BoneRole.Root] == null)
                map[BoneRole.Root] = animator.transform;

            // Fallback: Hip이 없으면 Root 사용
            if (map[BoneRole.Hip] == null && map[BoneRole.Root] != null)
                map[BoneRole.Hip] = map[BoneRole.Root];

            // Fallback: 번호 매겨진 본(bone_0, bone_1...) 휴리스틱 매핑
            ApplyNumberedBoneHeuristic(map, allTransforms);

            // Validate critical bones
            ValidateCriticalBones(map, animator.transform);

            return map;
        }

        /// <summary>
        /// 번호 매겨진 본(bone_0, bone_1...) 휴리스틱 매핑.
        /// 4족/2족 공통으로 깊이/자식수 기반 추론.
        /// </summary>
        private static void ApplyNumberedBoneHeuristic(Dictionary<BoneRole, Transform> map, Transform[] allTransforms)
        {
            var numberedBones = new List<Transform>();
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith("bone_") || t.name.StartsWith("Bone_"))
                    numberedBones.Add(t);
            }

            if (numberedBones.Count < 10) return; // Not a numbered skeleton

            // Sort by hierarchy depth (root first)
            numberedBones.Sort((a, b) => GetDepth(a).CompareTo(GetDepth(b)));

            // Heuristic: First bone = Root/Hips
            if (map[BoneRole.Root] == null && numberedBones.Count > 0)
                map[BoneRole.Root] = numberedBones[0];

            // Find longest chain from root = spine
            var root = map[BoneRole.Root];
            if (root != null)
            {
                var spineChain = FindLongestChain(root, numberedBones);
                if (spineChain.Count >= 3)
                {
                    map[BoneRole.Spine0] = spineChain[0];
                    if (spineChain.Count > 1) map[BoneRole.Spine1] = spineChain[1];
                    if (spineChain.Count > 2) map[BoneRole.Spine2] = spineChain[2];
                    if (spineChain.Count > 3) map[BoneRole.Neck] = spineChain[3];
                    if (spineChain.Count > 4) map[BoneRole.Head] = spineChain[4];
                }
            }

            // Find 4 limb chains from spine ends
            FindLimbChains(numberedBones, map);
        }

        private static int GetDepth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { d++; t = t.parent; }
            return d;
        }

        private static List<Transform> FindLongestChain(Transform root, List<Transform> candidates)
        {
            var chain = new List<Transform>();
            Transform current = root;
            var candidateSet = new HashSet<Transform>(candidates);

            while (true)
            {
                Transform child = null;
                int maxDescendants = -1;

                foreach (var c in candidateSet)
                {
                    if (IsDescendantOf(c, current))
                    {
                        int desc = CountDescendants(c, candidateSet);
                        if (desc > maxDescendants)
                        {
                            maxDescendants = desc;
                            child = c;
                        }
                    }
                }

                if (child == null || child == current) break;
                chain.Add(child);
                current = child;
            }
            return chain;
        }

        private static bool IsDescendantOf(Transform child, Transform ancestor)
        {
            while (child != null)
            {
                if (child == ancestor) return true;
                child = child.parent;
            }
            return false;
        }

        private static int CountDescendants(Transform t, HashSet<Transform> set)
        {
            int count = 0;
            foreach (Transform c in t)
                if (set.Contains(c)) count += 1 + CountDescendants(c, set);
            return count;
        }

        private static void FindLimbChains(List<Transform> numberedBones, Dictionary<BoneRole, Transform> map)
        {
            var hips = map[BoneRole.Root];
            if (hips == null) return;

            var limbRoots = new List<Transform>();
            foreach (Transform child in hips)
            {
                if (numberedBones.Contains(child))
                    limbRoots.Add(child);
            }

            // Sort by descendant count (legs have more)
            limbRoots.Sort((a, b) => CountDescendants(b, new HashSet<Transform>(numberedBones))
                                    .CompareTo(CountDescendants(a, new HashSet<Transform>(numberedBones))));

            // Assign: 0,1 = legs (more descendants), 2,3 = arms
            if (limbRoots.Count >= 4)
            {
                // Legs
                map[BoneRole.L_Hip] = limbRoots[0];
                map[BoneRole.R_Hip] = limbRoots[1];
                MapLimbChain(limbRoots[0], map, true, true);  // Left leg
                MapLimbChain(limbRoots[1], map, true, false); // Right leg

                // Arms
                map[BoneRole.L_Shoulder] = limbRoots[2];
                map[BoneRole.R_Shoulder] = limbRoots[3];
                MapLimbChain(limbRoots[2], map, false, true); // Left arm
                MapLimbChain(limbRoots[3], map, false, false); // Right arm
            }
        }

        private static void MapLimbChain(Transform root, Dictionary<BoneRole, Transform> map, bool isLeg, bool isLeft)
        {
            var chain = new List<Transform>();
            Transform current = root;
            while (current != null)
            {
                chain.Add(current);
                Transform next = null;
                foreach (Transform c in current)
                    if (c != current) { next = c; break; }
                current = next;
            }

            if (isLeg)
            {
                if (chain.Count >= 1) { if (isLeft) map[BoneRole.L_Hip] = chain[0]; else map[BoneRole.R_Hip] = chain[0]; }
                if (chain.Count >= 2) { if (isLeft) map[BoneRole.L_Knee] = chain[1]; else map[BoneRole.R_Knee] = chain[1]; }
                if (chain.Count >= 3) { if (isLeft) map[BoneRole.L_Ankle] = chain[2]; else map[BoneRole.R_Ankle] = chain[2]; }
                if (chain.Count >= 4) { if (isLeft) map[BoneRole.L_Foot] = chain[3]; else map[BoneRole.R_Foot] = chain[3]; }
            }
            else
            {
                if (chain.Count >= 1) { if (isLeft) map[BoneRole.L_Shoulder] = chain[0]; else map[BoneRole.R_Shoulder] = chain[0]; }
                if (chain.Count >= 2) { if (isLeft) map[BoneRole.L_Elbow] = chain[1]; else map[BoneRole.R_Elbow] = chain[1]; }
                if (chain.Count >= 3) { if (isLeft) map[BoneRole.L_Wrist] = chain[2]; else map[BoneRole.R_Wrist] = chain[2]; }
                if (chain.Count >= 4) { if (isLeft) map[BoneRole.L_Hand] = chain[3]; else map[BoneRole.R_Hand] = chain[3]; }
            }
        }

        private static void ValidateCriticalBones(Dictionary<BoneRole, Transform> map, Transform animatorRoot)
        {
            var critical = new[] { BoneRole.Root, BoneRole.Spine0, BoneRole.Head };
            foreach (var role in critical)
            {
                if (map[role] == null)
                    Debug.LogWarning($"[ProceduralBoneUtility] Critical bone missing: {role}. Animator: {animatorRoot.name}");
            }
        }
    }
}