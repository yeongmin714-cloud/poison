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
                // [2026-09-23 수리] 구버전 조건 !map.ContainsKey(role)은 위 prefill(전 역할 null 초기화)로
                // 항상 false → 이름 사전 매핑이 전혀 동작하지 않는 사코드였다. 첫 매칭 1본만 채택하도록 수정
                // (null인 역할만 — 후속 토폴로지 매핑이 다리/척추를 덮어쓰고, 어깨 등은 이 이름 매핑이 우선 유지).
                if (role != BoneRole.Root && map[role] == null)
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

            // The Animator root is already assigned above. Keep it unchanged; bone_0 can be a translated pelvis
            // joint (or root fallback), and replacing the root with the first-depth candidate destabilizes transforms.
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
                    // [2026-09-22 치명 버그 수리] current 자신도 IsDescendantOf(자기자신)=true로
                    // 후보에 들어 max descendants=자기자신 → 즉시 break → chain이 항상 비었다.
                    // 이것이 전 몬스터 spine=없음(3뼈만 매핑)의 진짜 뿌리.
                    if (c == current) continue;
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
                // 사지 체인은 전신 높이의 15%보다 짧을 수 있다(특히 토끼 뒷다리/악어 다리).
                // 체인 자체 길이와 하강 방향을 같이 확인해 장식 체인은 배제하되, 짧은 다리 누락을 막는다.
                float chainLength = 0f;
                for (int i = 1; i < c.Count; i++) chainLength += Vector3.Distance(c[i - 1].position, c[i].position);
                float minDrop = Mathf.Min(rigHeight * 0.15f, chainLength * 0.18f);
                bool downwardLimb = drop >= minDrop && chainLength >= rigHeight * 0.12f;
                // 하강 체인만 후보 풀에 넣는다. 아래로 처진 꼬리를 후보로 삼지 않도록
                // 옆으로 뻗은 악어 사지는 아래의 분기-미러 경로에서 별도 확인한다.
                if (downwardLimb) legChains.Add(c);
            }
            // 다리가 아래로 뻗지 않는 리그(뱀/장어 등) — None 힌트에선 판정 보류
            // [2026-09-23 phase1 수리] 보수적 구조 인지 다리 후보 확정.
            // 다리 체인은 반드시 구조적 근거 중 하나를 가져야 한다:
            //  (a) 부착 루트 X가 좌우 대칭인 형제 체인(일반 사지 — 좌/우 한 쌍), 또는
            //  (b) 같은 위치에서 갈라져 원위부 팁 X가 좌우 대칭인 형제(악어 분기 어깨 22/26처럼
            //      겹친 루트에서 하위 관절 회전으로 원위부가 벌어지는 구조).
            // 두 근거가 없는 긴 하강 체인(만티코어 꼬리 31→34)은 spanY가 커도 다리 후보에서
            // 제외한다 — spanY 정렬 최상위 꼬리가 L_Hip을 차지하던 버그의 뿌리.
            // 부착/팁 X는 트리 루트 기준 로컬 좌표로 판정(회전 무관).
            var tipXByChain = new Dictionary<List<Transform>, float>();
            foreach (var c in legChains)
            {
                tipXByChain[c] = treeRoot.InverseTransformPoint(c[c.Count - 1].position).x;
            }

            bool IsMirroredPair(float xA, float xB, float minMagnitude)
            {
                if (Mathf.Abs(xA) < minMagnitude || Mathf.Abs(xB) < minMagnitude) return false;
                if (Mathf.Sign(xA) == Mathf.Sign(xB)) return false;
                return Mathf.Abs(xA + xB) <= Mathf.Max(0.06f, 0.25f * Mathf.Max(Mathf.Abs(xA), Mathf.Abs(xB)));
            }

            var structureLegChains = new List<List<Transform>>();
            foreach (var c in legChains)
            {
                bool keep = false;
                float tipX = tipXByChain[c];
                foreach (var o in legChains)
                {
                    if (o == c) continue;
                    float rootX = treeRoot.InverseTransformPoint(c[0].position).x;
                    float otherRootX = treeRoot.InverseTransformPoint(o[0].position).x;
                    if (IsMirroredPair(rootX, otherRootX, 0.04f)) { keep = true; break; }
                }
                if (!keep && hint == BoneFamilyHint.Quadruped)
                {
                    foreach (var candidate in chains)
                    {
                        if (candidate == c || !HasSharedBranchRoot(c, candidate, set)) continue;
                        float candidateLength = 0f;
                        for (int k = 1; k < candidate.Count; k++)
                            candidateLength += Vector3.Distance(candidate[k - 1].position, candidate[k].position);
                        float candidateDrop = candidate[0].position.y - candidate[candidate.Count - 1].position.y;
                        bool candidateSideReach = candidateLength >= rigHeight * 0.12f
                            && candidate[0].position.y <= (minY + maxY) * 0.7f;
                        if (!candidateSideReach) continue;
                        float candidateTipX = treeRoot.InverseTransformPoint(candidate[candidate.Count - 1].position).x;
                        if (IsMirroredPair(tipX, candidateTipX, 0.03f)) { keep = true; break; }
                    }
                }
                if (keep) structureLegChains.Add(c);
            }
            legChains = structureLegChains;
            if (legChains.Count < 2) return;

            // 전/후·좌/우 분류 — [2026-09-23 phase1 수리] 좌우 서명은 사지 '부착 루트' X 기준
            // (해부학적 힙 위치). 평균 위치는 사슬 휘어짐에 희석되어 중앙으로 수렴해 오분류를 낳는다.
            // 부착 X가 0에 가깝지만 원위부 팁이 좌우로 벌어진 분기 어깨(악어)는 팁 X로 서명한다.
            var scored = new List<(List<Transform> chain, Vector3 local, float spanY)>();
            foreach (var c in legChains)
            {
                Vector3 local = treeRoot.InverseTransformPoint(c[0].position);
                if (Mathf.Abs(local.x) < 0.03f && Mathf.Abs(tipXByChain[c]) >= 0.03f)
                    local.x = tipXByChain[c];
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
                    bool distinctSides = Mathf.Sign(A.local.x) != Mathf.Sign(B.local.x)
                        && Mathf.Abs(A.local.x) >= 0.04f && Mathf.Abs(B.local.x) >= 0.04f;
                    // [2026-09-23 phase1 수리] distinctRoots — 겹친 루트(거리 <0.02, 서로 다른 본)에서
                    // 갈라진 두 사슬은 원위부 팁 X가 좌우 대칭이면 유효한 좌/우 쌍이다(악어 분기 어깨).
                    // 서명은 이미 팁 X로 대체되어 있으므로 local.x 대칭으로 판정한다.
                    float rootDist = Vector3.Distance(A.chain[0].position, B.chain[0].position);
                    bool distinctRoots;
                    if (rootDist >= 0.08f)
                        distinctRoots = true;
                    else if (rootDist < 0.02f && A.chain[0] != B.chain[0])
                        distinctRoots = Mathf.Sign(A.local.x) != Mathf.Sign(B.local.x)
                            && Mathf.Abs(A.local.x) >= 0.03f && Mathf.Abs(B.local.x) >= 0.03f;
                    else
                        distinctRoots = false; // 중간 거리(0.02~0.08)는 기존과 같이 거부
                    bool similarSpan = Mathf.Abs(A.spanY - B.spanY) <= 0.35f * Mathf.Max(A.spanY, B.spanY, 0.01f);
                    if (mirrorX && distinctSides && distinctRoots && similarSpan)
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
                // 페어링 실패 폴백은 금지한다. 좌우 X가 0에 겹친 중앙/몸통 체인을
                // 두 다리로 강제하면 악어 로그처럼 같은 위치 본이 좌/우 다리가 되어 뒤틀림을 유발한다.
                // 검증된 미러 페어가 없으면 다리 역할을 비워 두고, 추측 매핑보다 미구동을 택한다.
                return;
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
                // 4족: 페어 2개 — [2026-09-22] 머리 체인(비다리 최고점) 기준으로 앞/뒤 판정.
                // 기존 z>=0 가정은 리그 forward가 -Z인 리그에서 전/후 스왑(앞다리에 뒷다리 굽힘)을 만들었다.
                Vector3 headTip = Vector3.zero; float bestY = float.MinValue; bool hasHead = false;
                foreach (var c in chains)
                {
                    if (legChains.Exists(l => l[0] == c[0])) continue; // 다리 체인 제외
                    var tip = c[c.Count - 1];
                    if (tip.position.y > bestY) { bestY = tip.position.y; headTip = tip.position; hasHead = true; }
                }
                var sortedPairs = pairs.OrderBy(p =>
                {
                    // [2026-09-22 수리] head판정=False일 때 정렬 키가 전부 0 → 앞/뒤 배치가 임의가 되는
                    // 회귀 수리 — 이전 휴리스틱(z 평균)으로 폴백.
                    if (!hasHead) return -(p.a.local.z + p.b.local.z) * 0.5f;
                    Vector3 midW = (p.a.chain[0].position + p.b.chain[0].position) * 0.5f;
                    return Vector3.Distance(midW, headTip); // LINQ OrderBy 오름차순: 머리에 가까운 페어가 앞다리
                }).ToList();
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
                    UnityEngine.Debug.Log($"[ProceduralBoneUtility] 4족 배치: 앞[{fLeft[0].name},{fRight[0].name}] 뒤[{bLeft[0].name},{bRight[0].name}] head판정={hasHead}");
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

            // [2026-09-23 수리] 날개(어깨) 매핑 — 4족 토폴로지 경로는 FillArmRoles 미호출이라
            // L_Shoulder/R_Shoulder가 항상 null → ApplyWingFlap이 이름 사전 매핑("arm.l"류) 리그에서만
            // 발동하는 뿌리(QAPROGRESS P-ANIM7 ⚠️). 다리로 소비되지 않은 남은 체인에서 좌우 미러 페어를
            // 어깨 역할로 배치한다(griffin/manticore 등 익명 리그 날개 플랩 활성화).
            // 척추 매핑 '전에' 실행해 날개 뼈를 척추 후보에서도 제외 — 최장 체인 탐색이 날개로 새는 것 방지.
            if (!bipedMode)
            {
                MapWingChains(chains, legChains, spineLegBones, map, treeRoot, rigHeight, minY, maxY);
            }

            // [2026-09-22 수리] 척추/목/머리 — 다리로 소비된 뼈를 '제외한' 남은 뼈에서만 최장 체인.
            // 구버전은 전체 뼈에서 최장 체인을 골라 다리 뼈가 Head/Neck에 배치되고 ApplyHeadLook이
            // 다리 뼈를 회전시켜 몸이 뒤틀렸다. 판단이 서지 않으면 Head/Neck은 null로 남긴다
            // (ApplyHeadLook은 Head 미매핑 시 안전한 no-op).
            if (spineLegBones.Count > 0)
            {
                var remaining = set.Where(b => !spineLegBones.Contains(b)).ToList();
                var spineChain = FindLongestChain(treeRoot, remaining);
                // [2026-09-22 수리] 임계 3→2 — 익명 리그의 척추는 1~2뼈로 짧아 Count>=3에서 전부 누락됐다
                // (실측: 캡 적용 후에도 전 몬스터 spine=없음). 2뼈부터 Spine0/1 배치.
                if (spineChain.Count >= 2)
                {
                    map[BoneRole.Spine0] = spineChain[0];
                    if (spineChain.Count > 1) map[BoneRole.Spine1] = spineChain[1];
                    if (spineChain.Count > 2) map[BoneRole.Spine2] = spineChain[2];
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

        /// <summary>
        /// [2026-09-23 수리] 4족 날개(어깨) 매핑 — 다리로 소비되지 않은 남은 잎 체인에서 좌우 미러 페어를
        /// 골라 L/R_Shoulder 역할로 배치한다(ApplyWingFlap 구동 대상 — griffin/manticore 익명 리그).
        /// 보수 판정(장식 오탐 방지): ①이름 사전 매핑으로 어깨가 이미 확보됐으면 스킵(덮어쓰기 없음)
        /// ②부착 높이 상반부(어깨는 몸 위쪽 — 꼬리 제외) ③체인 세계길이 ≥ 리그 높이×0.3(귀·뿔 등 짧은 장식 제외)
        /// ④부착 |X| ≥ 다리힙 평균 폭×0.4(몸 중앙 부착 머리 장식 제외). 채택 페어는 부착 높이가 가장 높은 1개.
        /// </summary>
        static void MapWingChains(List<List<Transform>> chains, List<List<Transform>> legChains,
            HashSet<Transform> consumed, Dictionary<BoneRole, Transform> map,
            Transform treeRoot, float rigHeight, float minY, float maxY)
        {
            if (map[BoneRole.L_Shoulder] != null || map[BoneRole.R_Shoulder] != null)
                return; // 이름 사전 매핑 등으로 이미 확보 — 토폴로지 추정으로 덮어쓰지 않는다

            // 날개 후보 체인 — 다리 판정 체인과 소비 뼈(다리)가 섞인 체인 제외
            var leftovers = new List<List<Transform>>();
            foreach (var c in chains)
            {
                if (legChains.Exists(l => l[0] == c[0])) continue;
                bool hasConsumed = false;
                foreach (var b in c)
                {
                    if (consumed.Contains(b)) { hasConsumed = true; break; }
                }
                if (hasConsumed) continue;
                leftovers.Add(c);
            }
            if (leftovers.Count < 2) return;

            // 판정 임계 — 다리힙 평균 |X|(몸 폭 절반 근사), 리그 중심 높이, 리그 높이 기반 최소 체인 길이
            float legHipWidth = 0f;
            foreach (var l in legChains)
                legHipWidth += Mathf.Abs(treeRoot.InverseTransformPoint(l[0].position).x);
            legHipWidth = legHipWidth > 0f ? legHipWidth / legChains.Count : 0.1f;

            float centerY = (minY + maxY) * 0.5f;
            float minSpan = Mathf.Max(0.05f, rigHeight * 0.3f);
            float minOffsetX = legHipWidth * 0.4f;

            var scored = new List<(List<Transform> chain, Vector3 local, float span)>();
            foreach (var c in leftovers)
            {
                Vector3 sum = Vector3.zero;
                foreach (var b in c) sum += b.position;
                Vector3 local = treeRoot.InverseTransformPoint(sum / c.Count);
                float span = Vector3.Distance(c[0].position, c[c.Count - 1].position);
                if (c[0].position.y < centerY) continue;       // 하반부 부착 = 날개 아님(꼬리 등)
                if (Mathf.Abs(local.x) < minOffsetX) continue; // 중앙 부착 = 날개 아님(머리 장식 등)
                if (span < minSpan) continue;                  // 너무 짧음 = 장식(귀·뿔 등)
                scored.Add((c, local, span));
            }
            if (scored.Count < 2) return;

            // 좌우 미러 페어링 — 다리 페어링과 동일 판정(mirrorX + 유사 세계길이). 미페어(뿔 1개 등) 제외.
            (List<Transform> chain, Vector3 local, float span) bestA = default, bestB = default;
            float bestAttachY = float.MinValue;
            var used = new HashSet<int>();
            for (int i = 0; i < scored.Count; i++)
            {
                if (used.Contains(i)) continue;
                for (int j = i + 1; j < scored.Count; j++)
                {
                    if (used.Contains(j)) continue;
                    var A = scored[i]; var B = scored[j];
                    bool mirrorX = Mathf.Abs(A.local.x + B.local.x)
                        <= Mathf.Max(0.08f, 0.25f * Mathf.Max(Mathf.Abs(A.local.x), Mathf.Abs(B.local.x)));
                    bool similarSpan = Mathf.Abs(A.span - B.span) <= 0.35f * Mathf.Max(A.span, B.span, 0.01f);
                    if (!mirrorX || !similarSpan) continue;

                    float attachY = (A.chain[0].position.y + B.chain[0].position.y) * 0.5f;
                    if (attachY > bestAttachY) { bestAttachY = attachY; bestA = A; bestB = B; }
                    used.Add(i); used.Add(j);
                    break;
                }
            }
            if (bestA.chain == null) return;

            var left = bestA.local.x <= bestB.local.x ? bestA.chain : bestB.chain;
            var right = bestA.local.x <= bestB.local.x ? bestB.chain : bestA.chain;
            FillArmRoles(map, left, true);
            FillArmRoles(map, right, false);
            consumed.UnionWith(left);  // 척추 매핑 후보에서도 제외(최장 체인이 날개로 새는 것 방지)
            consumed.UnionWith(right);

            UnityEngine.Debug.Log($"[ProceduralBoneUtility] 날개 배치: L[{left[0].name}..{left[left.Count - 1].name}] R[{right[0].name}..{right[right.Count - 1].name}] → 어깨 역할 부여(플랩 대상)");
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

        static bool HasSharedBranchRoot(List<Transform> a, List<Transform> b, HashSet<Transform> set)
        {
            Transform parentA = a[0] != null ? a[0].parent : null;
            Transform parentB = b[0] != null ? b[0].parent : null;
            if (parentA == null || parentA != parentB || !set.Contains(parentA)) return false;
            return CountChildrenInSet(parentA, set) > 2;
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