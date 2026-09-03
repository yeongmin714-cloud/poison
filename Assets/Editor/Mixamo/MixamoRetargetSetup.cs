using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectName.Editor
{
    /// <summary>
    /// UserProvided FBX (GLB에서 컨버팅된 Humanoid 대상 모델)의 Animation Rig Type을
    /// Generic → Humanoid 로 전환하고 HumanDescription 매핑을 구성한다.
    /// 믹사모 클립 FBX 애니메이션을 이 아바타로 리타겟핑하기 위한 전제 작업.
    ///
    /// 사용법: 메뉴 Tools > Anim > Apply Humanoid Rigs
    /// 본 구조 (검증됨): metarig → Root(hips) → spine → spine.001~005 /
    ///                   thigh.L/R, shoulder.L/R → upper_arm → forearm → hand
    /// </summary>
    public static class MixamoRetargetSetup
    {
        private static readonly string[] ModelPaths =
        {
            "Assets/Resources/Models/UserProvided/fbx/Player_Rigged.fbx",
            "Assets/Resources/Models/UserProvided/fbx/soldier_lv1-20_rigged.fbx",
            "Assets/Resources/Models/UserProvided/fbx/soldier_lv20-40_rigged.fbx",
            "Assets/Resources/Models/UserProvided/fbx/soldier_lv40-50_rigged.fbx",
        };

        [MenuItem("Tools/Anim/Apply Humanoid Rigs")]
        public static void ApplyHumanoidRigs()
        {
            foreach (var path in ModelPaths)
            {
                try
                {
                    ApplyHumanoidRig(path);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MixamoRetarget] {path} 작업 실패: {e.Message}\n{e.StackTrace}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MixamoRetarget] Humanoid 라이그 적용 메뉴 완료.");
        }

        /// <summary>단일 FBX에 Humanoid 라이그를 적용한다.</summary>
        private static void ApplyHumanoidRig(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[MixamoRetarget] ModelImporter를 얻지 못함: {path}");
                return;
            }

            // 전체 트랜스폼 계층을 SkeletonBone[] 으로 스냅샷 (루트 + 메시 노드 포함).
            var skeleton = BuildSkeletonFromModel(path);
            if (skeleton.Length == 0)
            {
                Debug.LogError($"[MixamoRetarget] {path} 스켈레톤 수집 실패");
                return;
            }

            var desc = new HumanDescription
            {
                human = BuildHumanBones(),
                skeleton = skeleton,
                hasTranslationDoF = true,
                lowerArmTwist = 0.5f,
                upperArmTwist = 0.5f,
                upperLegTwist = 0.5f,
            };

            importer.animationType = ModelImporterAnimationType.Human; // Unity 6.4: Humanoid → Human으로 개명됨
            importer.humanDescription = desc;
            importer.SaveAndReimport();

            VerifyAvatar(path);
        }

        /// <summary>
        /// 임시 프리팹 인스턴스를 만들고 전체 자식 Transform 을 재귀 순회해
        /// SkeletonBone[] (name/localPosition/localRotation/localScale) 을 채운다.
        /// 루트 뼈를 포함하며, MESH 노드 등 비-본 메시 트랜스폼도 skeleton 배열에 포함된다.
        /// 순회 후 임시 객체는 파괴된다.
        /// </summary>
        private static SkeletonBone[] BuildSkeletonFromModel(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[MixamoRetarget] 프리팹 로드 실패: {path}");
                return new SkeletonBone[0];
            }

            GameObject temp = null;
            try
            {
                temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            catch
            {
                // 모델 프리팹 컨텍스트로 못 만들면 일반 인스턴스로 폴백.
                temp = UnityEngine.Object.Instantiate(prefab);
            }

            var bones = new List<SkeletonBone>();
            try
            {
                if (temp != null)
                    CollectTransforms(temp.transform, bones);
            }
            finally
            {
                if (temp != null)
                    UnityEngine.Object.DestroyImmediate(temp);
            }

            return bones.ToArray();
        }

        /// <summary>Transform 을 전위 순회하며 SkeletonBone 을 누적한다.</summary>
        private static void CollectTransforms(Transform t, List<SkeletonBone> bones)
        {
            bones.Add(new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale,
            });

            for (var i = 0; i < t.childCount; i++)
                CollectTransforms(t.GetChild(i), bones);
        }

        /// <summary>
        /// boneName→humanName 매핑으로 HumanBone[] 을 구성한다.
        /// 각 뼈는 useDefaultValues=true(기본 리밋) 를 적용한다.
        /// </summary>
        private static HumanBone[] BuildHumanBones()
        {
            var map = new Dictionary<string, string>
            {
                { "Root",        "Hips" },
                { "spine",       "Spine" },
                { "spine.001",   "Chest" },
                { "spine.002",   "UpperChest" },
                { "spine.003",   "Neck" },
                { "spine.004",   "Head" },
                { "shoulder.L",  "LeftShoulder" },
                { "shoulder.R",  "RightShoulder" },
                { "upper_arm.L", "LeftUpperArm" },
                { "upper_arm.R", "RightUpperArm" },
                { "forearm.L",   "LeftLowerArm" },
                { "forearm.R",   "RightLowerArm" },
                { "hand.L",      "LeftHand" },
                { "hand.R",      "RightHand" },
                { "thigh.L",     "LeftUpperLeg" },
                { "thigh.R",     "RightUpperLeg" },
                { "shin.L",      "LeftLowerLeg" },
                { "shin.R",      "RightLowerLeg" },
                { "foot.L",      "LeftFoot" },
                { "foot.R",      "RightFoot" },
                { "toe.L",       "LeftToes" },
                { "toe.R",       "RightToes" },
            };

            var list = new List<HumanBone>(map.Count);
            foreach (var kv in map)
            {
                list.Add(new HumanBone
                {
                    boneName = kv.Key,
                    humanName = kv.Value,
                    limit = new HumanLimit { useDefaultValues = true },
                });
            }

            return list.ToArray();
        }

        /// <summary>
        /// 재임포트 후 Avatar 서브에셋 존재 여부와 isValid 를 확인해 로그를 남긴다.
        /// </summary>
        private static void VerifyAvatar(string path)
        {
            Avatar avatar = null;
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            if (all != null)
            {
                foreach (var o in all)
                {
                    if (o is Avatar a)
                    {
                        avatar = a;
                        break;
                    }
                }
            }

            if (avatar == null)
            {
                Debug.LogWarning($"[MixamoRetarget] {path}: Avatar 서브에셋 미발견 (Humanoid 변환 실패 가능).");
                return;
            }

            Debug.Log($"[MixamoRetarget] {path} → Humanoid 완료. Avatar={avatar.name}, isValid={avatar.isValid}");
        }
    }
}