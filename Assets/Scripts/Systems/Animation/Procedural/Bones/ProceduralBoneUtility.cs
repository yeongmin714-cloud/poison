using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Bones
{
    /// <summary>
    /// Procedural animation bone utility (static class).
    /// Bone role enumeration, name mapping, automatic detection.
    /// </summary>
    public static class ProceduralBoneUtility
    {
        // Use the shared BoneRole enum from Bones namespace
        // public enum BoneRole { ... } // REMOVED - use Bones.BoneRole instead

        // ──────────────────────────────────────────────
        // Mapping Table (lowercase key → BoneRole)
        // ──────────────────────────────────────────────
        static readonly Dictionary<string, BoneRole> _nameToRole = new Dictionary<string, BoneRole>
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

            // Spine chain - standard Humanoid
            { "spine", BoneRole.Spine0 },
            { "spine0", BoneRole.Spine0 },
            { "spine1", BoneRole.Spine1 },
            { "spine2", BoneRole.Spine2 },
            { "spine3", BoneRole.Spine3 },
            { "spine4", BoneRole.Spine3 },
            { "chest", BoneRole.Spine2 },
            { "upperchest", BoneRole.Spine3 },
            { "neck", BoneRole.Neck },
            { "neck1", BoneRole.Neck },
            { "head", BoneRole.Head },

            // Additional common GLB/Blender/Mixamo bone names
            { "spine_0", BoneRole.Spine0 },
            { "spine_1", BoneRole.Spine1 },
            { "spine_2", BoneRole.Spine2 },
            { "spine_3", BoneRole.Spine3 },
            { "spine_4", BoneRole.Spine3 },
            { "spine_5", BoneRole.Spine3 },
            { "spine_01", BoneRole.Spine0 },
            { "spine_02", BoneRole.Spine1 },
            { "spine_03", BoneRole.Spine2 },
            { "spine_04", BoneRole.Neck },
            { "spine_05", BoneRole.Neck },
            { "back", BoneRole.Spine0 },
            { "back1", BoneRole.Spine0 },
            { "back2", BoneRole.Spine1 },
            { "back3", BoneRole.Spine2 },
            { "back4", BoneRole.Spine3 },
            { "torso", BoneRole.Spine0 },
            { "torso1", BoneRole.Spine1 },
            { "torso2", BoneRole.Spine2 },
            { "torso3", BoneRole.Spine3 },
            { "abdomen", BoneRole.Spine0 },
            { "abdomen1", BoneRole.Spine0 },
            { "abdomen2", BoneRole.Spine1 },
            { "chest1", BoneRole.Spine2 },
            { "chest2", BoneRole.Spine2 },
            { "chest3", BoneRole.Spine3 },
            { "upper_chest", BoneRole.Spine3 },
            { "neck_01", BoneRole.Neck },
            { "neck_02", BoneRole.Neck },
            { "head_01", BoneRole.Head },
            { "head_end", BoneRole.Head },

            // More head variants (requested)
            { "headtop", BoneRole.Head },
            { "head_tip", BoneRole.Head },
            { "head_mid", BoneRole.Head },
            { "cc_base_head", BoneRole.Head },
            { "mixamorig:head", BoneRole.Head },
            { "머리", BoneRole.Head },
            { "head1", BoneRole.Head },
            { "head2", BoneRole.Head },

            // Blender metarig / Rigify bone names (Player_Rigged.glb)
            { "spine.005", BoneRole.Head },
            { "spine.004", BoneRole.Neck },
            { "spine.003", BoneRole.Spine3 },
            { "spine.002", BoneRole.Spine2 },
            { "spine.001", BoneRole.Spine1 },
            { "shoulder.L", BoneRole.L_Clavicle },
            { "shoulder.R", BoneRole.R_Clavicle },
            { "upper_arm.L", BoneRole.L_Shoulder },
            { "upper_arm.R", BoneRole.R_Shoulder },
            { "forearm.L", BoneRole.L_Elbow },
            { "forearm.R", BoneRole.R_Elbow },
            { "hand.L", BoneRole.L_Hand },
            { "hand.R", BoneRole.R_Hand },
            { "thigh.L", BoneRole.L_Hip },
            { "thigh.R", BoneRole.R_Hip },
            { "shin.L", BoneRole.L_Knee },
            { "shin.R", BoneRole.R_Knee },
            { "foot.L", BoneRole.L_Foot },
            { "foot.R", BoneRole.R_Foot },
            { "toe.L", BoneRole.L_Toes },
            { "toe.R", BoneRole.R_Toes },
            { "pelvis.L", BoneRole.L_Hip },
            { "pelvis.R", BoneRole.R_Hip },
            { "breast.L", BoneRole.Spine3 },
            { "breast.R", BoneRole.Spine3 },

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

            // Additional common leg bone names
            { "thigh_01", BoneRole.L_Hip },
            { "thigh_02", BoneRole.R_Hip },
            { "calf_01", BoneRole.L_Knee },
            { "calf_02", BoneRole.R_Knee },
            { "foot_01", BoneRole.L_Foot },
            { "foot_02", BoneRole.R_Foot },
            { "toes_01", BoneRole.L_Toes },
            { "toes_02", BoneRole.R_Toes },
        };

        // ──────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Detect bone role from name (case-insensitive, suffix-agnostic).
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
        /// Build bone role → Transform mapping from Animator.
        /// </summary>
        public static Dictionary<BoneRole, Transform> BuildMap(Animator animator, BoneFamilyHint hint = BoneFamilyHint.None)
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

            // Root bone = Animator root
            if (!map.ContainsKey(BoneRole.Root) || map[BoneRole.Root] == null)
                map[BoneRole.Root] = animator.transform;

            // Hip fallback
            if (map[BoneRole.Hip] == null && map[BoneRole.Root] != null)
                map[BoneRole.Hip] = map[BoneRole.Root];

            // Numbered bone heuristic (bone_0, bone_1...)
            ApplyNumberedBoneHeuristic(map, allTransforms, hint);

            // Validate
            ValidateCriticalBones(map, animator.transform);

            return map;
        }

        // ──────────────────────────────────────────────
        // Numbered Bone Heuristic (bone_0, bone_1...)
        // ──────────────────────────────────────────────

        static void ApplyNumberedBoneHeuristic(Dictionary<BoneRole, Transform> map, Transform[] allTransforms, BoneFamilyHint hint)
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

            // First bone = Root/Hips
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

            // Find limb chains — [2026-09-22 수리] 전체 트리 탐색 토폴로지 매핑으로 재작성.
            // 구현은 Root 직계 자식만 보고 >=4개를 요구해 GLB 동물 리그(다리가 척추 노드에서
            // 갈라지는 구조)에서 전부 실패 → 3뼈만 매핑되어 보행 애니가 구동 불능이었다.
            FindLimbChains(numberedBones, map, hint);
        }

        static int GetDepth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { d++; t = t.parent; }
            return d;
        }

        static List<Transform> FindLongestChain(Transform root, List<Transform> candidates)
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

        static bool IsDescendantOf(Transform child, Transform ancestor)
        {
            while (child != null)
            {
                if (child == ancestor) return true;
                child = child.parent;
            }
            return false;
        }

        static int CountDescendants(Transform t, HashSet<Transform> set)
        {
            int count = 0;
            foreach (Transform c in t)
                if (set.Contains(c)) count += 1 + CountDescendants(c, set);
            return count;
        }

        static void FindLimbChains(List<Transform> numberedBones, Dictionary<BoneRole, Transform> map, BoneFamilyHint hint)
        {
            // [2026-09-22 전면 재작성 — 익명 리그 토폴로지 매핑]
            // 전체 트리에서 "가지 노드에서 뻗어 잎까지 이어지는 체인"을 모두 수집해,
            // 지면(아래) 방향 체인=다리, 위/옆 체인=팔(2족)로 분류한다.
            // 뼈 위치는 트리 루트 기준 로컬좌표로 판정(회전 무관).
            var set = new HashSet<Transform>(numberedBones);
            if (set.Count < 8) return;

            // 트리 루트 = 집합 내 부모가 없는 뼈
            Transform treeRoot = null;
            foreach (var b in set)
            {
                if (b.parent == null || !set.Contains(b.parent)) { treeRoot = b; break; }
            }
            if (treeRoot == null) return;

            // 잎 체인 수집 — 잎에서 위로 올라가 분기 노드(자식 2+) 직전까지.
            // [2026-09-22 수리] 체인 길이 캡(4뼈) — 캡 없이는 단일 자식 연쇄를 타고 골반·척추까지
            // 흡수해 전 몬스터 spine=없음이 되고, IK가 척추 뼈를 무릎처럼 꺾어 몸이 뒤틀렸다.
            var chains = new List<List<Transform>>();
            foreach (var leaf in set)
            {
                if (CountChildrenInSet(leaf, set) != 0) continue; // 잎만

                var chain = new List<Transform>();
                var cur = leaf;
                while (chain.Count < 4 && cur != null && set.Contains(cur) && cur != treeRoot && CountChildrenInSet(cur, set) <= 1)
                {
                    chain.Add(cur);
                    cur = cur.parent;
                }
                chain.Reverse();
                if (chain.Count >= 2)
                    chains.Add(chain);
            }
            if (chains.Count == 0) return;

            // 리그 기하: 높이/중심 — 다리 판정 임계용
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var b in set)
            {
                minY = Mathf.Min(minY, b.position.y);
                maxY = Mathf.Max(maxY, b.position.y);
            }
            float rigHeight = Mathf.Max(0.05f, maxY - minY);

            // 다리 후보: 체인 끝(잎)이 체인 시작보다 유의미하게 아래로 뻗은 체인
            var legChains = new List<List<Transform>>();
            foreach (var c in chains)
            {
                float drop = c[0].position.y - c[c.Count - 1].position.y;
                if (drop >= rigHeight * 0.15f) legChains.Add(c);
            }
            // 다리가 아래로 뻗지 않는 리그(뱀/장어 등) — None 힌트에선 판정 보류
            if (legChains.Count < 2) return;

            // 전/후·좌/우 분류 — 트리 루트 기준 평균 로컬 위치
            var scored = new List<(List<Transform> chain, Vector3 local, float spanY)>();
            foreach (var c in legChains)
            {
                Vector3 sum = Vector3.zero;
                foreach (var b in c) sum += b.position;
                Vector3 avg = sum / c.Count;
                Vector3 local = treeRoot.InverseTransformPoint(avg);
                float span = Mathf.Abs(c[c.Count - 1].position.y - c[0].position.y);
                scored.Add((c, local, span));
            }
            // 긴 체인 우선(다리 스트라이드 품질)
            scored.Sort((a, b) => b.spanY.CompareTo(a.spanY));

            if (hint == BoneFamilyHint.Special)
            {
                // 특수형은 Root만 필요 — 다리/팔 역할 배치 생략
                return;
            }

            // [2026-09-22 수리] 좌우 미러 페어링 — 다리는 반드시 X가 대칭인 쌍으로 존재한다.
            // 미페어 체인(꼬리/날개/뿔)은 다리 후보에서 제외한다. 구버전은 "가장 긴 4개"를 골라
            // 꼬리가 다리 자리를 차지하고 실제 다리가 매핑 누락되어 몸이 뒤틀렸다.
            var pairs = new List<((List<Transform> chain, Vector3 local, float spanY) a, (List<Transform> chain, Vector3 local, float spanY) b)>();
            var used = new HashSet<int>();
            for (int i = 0; i < scored.Count; i++)
            {
                if (used.Contains(i)) continue;
                for (int j = i + 1; j < scored.Count; j++)
                {
                    if (used.Contains(j)) continue;
                    var A = scored[i]; var B = scored[j];
                    bool mirrorX = Mathf.Abs(A.local.x + B.local.x) <= Mathf.Max(0.08f, 0.25f * Mathf.Max(Mathf.Abs(A.local.x), Mathf.Abs(B.local.x)));
                    bool similarSpan = Mathf.Abs(A.spanY - B.spanY) <= 0.35f * Mathf.Max(A.spanY, B.spanY, 0.01f);
                    if (mirrorX && similarSpan)
                    {
                        pairs.Add((A, B));
                        used.Add(i); used.Add(j);
                        break;
                    }
                }
            }

            var spineLegBones = new HashSet<Transform>(); // 다리로 소비된 뼈 — 척추 매핑에서 제외
            if (pairs.Count == 0 && scored.Count >= 2)
            {
                // 페어링 실패 폴백 — 상위 2개를 좌/우 다리로(구동 보장 우선)
                pairs.Add((scored[0], scored[1]));
            }

            bool bipedMode = hint == BoneFamilyHint.Biped || (hint == BoneFamilyHint.None && pairs.Count == 1);

            if (bipedMode)
            {
                // 2족: 페어 1개=다리, 남은 체인 2개=팔
                var pr = pairs[0];
                var leftLeg = pr.a.local.x <= pr.b.local.x ? pr.a.chain : pr.b.chain;
                var rightLeg = pr.a.local.x <= pr.b.local.x ? pr.b.chain : pr.a.chain;
                FillLegRoles(map, leftLeg, false, true);
                FillLegRoles(map, rightLeg, false, false);
                spineLegBones.UnionWith(leftLeg); spineLegBones.UnionWith(rightLeg);

                var armCands = scored.Where(s => !spineLegBones.Contains(s.chain[0])).ToList();
                if (armCands.Count >= 2)
                {
                    var lArm = armCands[0].local.x <= armCands[1].local.x ? armCands[0].chain : armCands[1].chain;
                    var rArm = armCands[0].local.x <= armCands[1].local.x ? armCands[1].chain : armCands[0].chain;
                    FillArmRoles(map, lArm, true);
                    FillArmRoles(map, rArm, false);
                    spineLegBones.UnionWith(lArm); spineLegBones.UnionWith(rArm);
                }
            }
            else
            {
                // 4족: 페어 2개 — z가 큰 쪽=앞다리(앞좌/앞우), 작은 쪽=뒷다리(Hind)
                var sortedPairs = pairs.OrderByDescending(p => (p.a.local.z + p.b.local.z) * 0.5f).ToList();
                if (sortedPairs.Count >= 2)
                {
                    var frontPair = sortedPairs[0];
                    var backPair = sortedPairs[1];
                    var fLeft = frontPair.a.local.x <= frontPair.b.local.x ? frontPair.a.chain : frontPair.b.chain;
                    var fRight = frontPair.a.local.x <= frontPair.b.local.x ? frontPair.b.chain : frontPair.a.chain;
                    var bLeft = backPair.a.local.x <= backPair.b.local.x ? backPair.a.chain : backPair.b.chain;
                    var bRight = backPair.a.local.x <= backPair.b.local.x ? backPair.b.chain : backPair.a.chain;
                    FillLegRoles(map, fLeft, false, true);
                    FillLegRoles(map, fRight, false, false);
                    FillLegRoles(map, bLeft, true, true);
                    FillLegRoles(map, bRight, true, false);
                    spineLegBones.UnionWith(fLeft); spineLegBones.UnionWith(fRight);
                    spineLegBones.UnionWith(bLeft); spineLegBones.UnionWith(bRight);
                }
                else if (sortedPairs.Count == 1)
                {
                    // 페어 1개뿐(4족 리그에서 뒷다리 미검출) — 앞다리로만 배치(구동 보장)
                    var pr = sortedPairs[0];
                    var leftLeg = pr.a.local.x <= pr.b.local.x ? pr.a.chain : pr.b.chain;
                    var rightLeg = pr.a.local.x <= pr.b.local.x ? pr.b.chain : pr.a.chain;
                    FillLegRoles(map, leftLeg, false, true);
                    FillLegRoles(map, rightLeg, false, false);
                    spineLegBones.UnionWith(leftLeg); spineLegBones.UnionWith(rightLeg);
                }
            }

            // [2026-09-22 수리] 척추/목/머리 — 다리로 소비된 뼈를 '제외한' 남은 뼈에서만 최장 체인.
            // 구버전은 전체 뼈에서 최장 체인을 골라 다리 뼈가 Head/Neck에 배치되고 ApplyHeadLook이
            // 다리 뼈를 회전시켜 몸이 뒤틀렸다. 판단이 서지 않으면 Head/Neck은 null로 남긴다
            // (ApplyHeadLook은 Head 미매핑 시 안전한 no-op).
            if (spineLegBones.Count > 0)
            {
                var remaining = set.Where(b => !spineLegBones.Contains(b)).ToList();
                var spineChain = FindLongestChain(treeRoot, remaining);
                if (spineChain.Count >= 3)
                {
                    map[BoneRole.Spine0] = spineChain[0];
                    map[BoneRole.Spine1] = spineChain[1];
                    map[BoneRole.Spine2] = spineChain[2];
                    float centerY = (minY + maxY) * 0.5f;
                    if (spineChain.Count >= 4 && spineChain[spineChain.Count - 2].position.y >= centerY)
                        map[BoneRole.Neck] = spineChain[spineChain.Count - 2];
                    if (spineChain[spineChain.Count - 1].position.y >= centerY)
                        map[BoneRole.Head] = spineChain[spineChain.Count - 1];
                }
                // 매핑 결과 진단 로그(1회/리그) — 어떤 체인이 다리로 배치됐는지 즉시 검증 가능
                UnityEngine.Debug.Log($"[ProceduralBoneUtility] 토폴로지 매핑: {treeRoot.name} legs={spineLegBones.Count}개 뼈, spine={(map[BoneRole.Spine0] != null ? map[BoneRole.Spine0].name : "없음")}, head={(map[BoneRole.Head] != null ? map[BoneRole.Head].name : "없음(HeadLook 생략)")}");
            }
        }

        /// <summary>다리 체인 → Hip/Knee/Ankle/Foot(또는 Hind* 역할) 배치. 짧은 체인은 끝뼈로 폴백.</summary>
        static void FillLegRoles(Dictionary<BoneRole, Transform> map, List<Transform> chain, bool hind, bool left)
        {
            BoneRole hip = hind ? (left ? BoneRole.L_HindHip : BoneRole.R_HindHip)
                                : (left ? BoneRole.L_Hip : BoneRole.R_Hip);
            BoneRole knee = hind ? (left ? BoneRole.L_HindKnee : BoneRole.R_HindKnee)
                                 : (left ? BoneRole.L_Knee : BoneRole.R_Knee);
            BoneRole ankle = hind ? (left ? BoneRole.L_HindAnkle : BoneRole.R_HindAnkle)
                                  : (left ? BoneRole.L_Ankle : BoneRole.R_Ankle);
            BoneRole foot = hind ? (left ? BoneRole.L_HindFoot : BoneRole.R_HindFoot)
                                 : (left ? BoneRole.L_Foot : BoneRole.R_Foot);

            map[hip] = chain[0];
            map[knee] = chain.Count > 1 ? chain[1] : chain[chain.Count - 1];
            map[ankle] = chain.Count > 2 ? chain[2] : chain[chain.Count - 1];
            map[foot] = chain[chain.Count - 1];
        }

        /// <summary>팔 체인 → Shoulder/Elbow/Wrist/Hand 배치(2족 전용).</summary>
        static void FillArmRoles(Dictionary<BoneRole, Transform> map, List<Transform> chain, bool left)
        {
            BoneRole shoulder = left ? BoneRole.L_Shoulder : BoneRole.R_Shoulder;
            BoneRole elbow = left ? BoneRole.L_Elbow : BoneRole.R_Elbow;
            BoneRole wrist = left ? BoneRole.L_Wrist : BoneRole.R_Wrist;
            BoneRole hand = left ? BoneRole.L_Hand : BoneRole.R_Hand;

            map[shoulder] = chain[0];
            map[elbow] = chain.Count > 1 ? chain[1] : chain[chain.Count - 1];
            map[wrist] = chain.Count > 2 ? chain[2] : chain[chain.Count - 1];
            map[hand] = chain[chain.Count - 1];
        }

        static int CountChildrenInSet(Transform t, HashSet<Transform> set)
        {
            int count = 0;
            foreach (Transform c in t)
                if (set.Contains(c)) count++;
            return count;
        }

        static void ValidateCriticalBones(Dictionary<BoneRole, Transform> map, Transform animatorRoot)
        {
            // Check if this is a small creature that doesn't need a Head bone
            bool isSmallCreature = IsSmallCreature(animatorRoot, map);

            // Apply fallbacks FIRST to avoid false warnings
            // Fallback: if Spine0 missing but we have Spine1/Spine2/Spine3/Neck, use the first available spine bone
            if (map[BoneRole.Spine0] == null)
            {
                if (map[BoneRole.Spine1] != null) map[BoneRole.Spine0] = map[BoneRole.Spine1];
                else if (map[BoneRole.Spine2] != null) map[BoneRole.Spine0] = map[BoneRole.Spine2];
                else if (map[BoneRole.Spine3] != null) map[BoneRole.Spine0] = map[BoneRole.Spine3];
                else if (map[BoneRole.Neck] != null) map[BoneRole.Spine0] = map[BoneRole.Neck];
            }

            // Fallback: if Head missing but we have Neck, use Neck
            if (map[BoneRole.Head] == null && map[BoneRole.Neck] != null)
            {
                map[BoneRole.Head] = map[BoneRole.Neck];
            }

            // Fallback: if still no Spine0, use Root as last resort
            if (map[BoneRole.Spine0] == null && map[BoneRole.Root] != null)
            {
                map[BoneRole.Spine0] = map[BoneRole.Root];
            }

            // NOW validate critical bones (after fallbacks applied)
                        var critical = new[] { BoneRole.Root, BoneRole.Spine0 };
            
                        // Head is NOT critical for any creature - many GLB models don't have proper head bones
                        // The heuristic will try to assign it from spine chain index 4, but it's optional
            
                        foreach (var role in critical)
                        {
                            if (map[role] == null)
                                UnityEngine.Debug.LogWarning($"[ProceduralBoneUtility] Critical bone missing: {role}. Animator: {animatorRoot.name} - falling back to heuristic mapping");
                        }
            
                        // Head is optional for all creatures - only log as info if missing
                        if (map[BoneRole.Head] == null)
                        {
                            UnityEngine.Debug.Log($"[ProceduralBoneUtility] Head bone not found for {animatorRoot.name} - using heuristic fallback (spine chain index 4). This is normal for many GLB models.");
                        }
                    }

        static bool IsSmallCreature(Transform animatorRoot, Dictionary<BoneRole, Transform> map)
        {
            // Check model name for small creature keywords
            string modelName = animatorRoot.name.ToLowerInvariant();
            if (modelName.Contains("snake") || modelName.Contains("slime") || 
                modelName.Contains("rat") || modelName.Contains("crow") || 
                modelName.Contains("bat") || modelName.Contains("spider") ||
                modelName.Contains("worm") || modelName.Contains("fish") ||
                modelName.Contains("insect") || modelName.Contains("bug"))
                return true;

            // Check total bone count (small creatures have fewer bones)
            int boneCount = map.Count;
            if (boneCount < 15)
                return true;

            return false;
        }
    }
}