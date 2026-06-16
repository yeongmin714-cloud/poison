using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Holds metadata about a loaded GLB model, including its detected rig type.
    /// </summary>
    public struct ModelMetadata
    {
        /// <summary>The loaded prefab reference.</summary>
        public GameObject Prefab;
        /// <summary>The detected model type (Static, RiggedHumanoid, RiggedQuadruped, RiggedMonster).</summary>
        public ModelType ModelType;

        /// <summary>
        /// Creates a new ModelMetadata entry.
        /// </summary>
        /// <param name="prefab">The loaded prefab GameObject.</param>
        /// <param name="modelType">The detected rig type.</param>
        public ModelMetadata(GameObject prefab, ModelType modelType)
        {
            Prefab = prefab;
            ModelType = modelType;
        }
    }

    /// <summary>
    /// GLB 모델을 런타임에 로드하고 관리하는 정적 클래스입니다.
    /// Resources/Models/UserProvided/ 폴더에서 GLB 프리팹을 로드하여
    /// Placeholder(기본 도형)를 실제 모델로 교체하는 데 사용합니다.
    /// 
    /// 사용 예:
    /// <code>
    /// if (RuntimeModelLoader.TryGetModel("player", out var model))
    /// {
    ///     Instantiate(model, transform);
    /// }
    /// </code>
    /// 
    /// v2: rig detection — 각 로드된 모델의 뼈대 정보를 분석하여
    /// <see cref="ModelType"/> (Static, RiggedHumanoid, RiggedQuadruped, RiggedMonster)을
    /// 감지하고 <see cref="ModelMetadata"/>에 저장합니다.
    /// </summary>
    public static class RuntimeModelLoader
    {
        /// <summary>로드된 모델 캐시 (key: 소문자 파일명, value: GLB GameObject 프리팹)</summary>
        private static Dictionary<string, GameObject> _loadedModels;

        /// <summary>로드된 모델 메타데이터 캐시 (key: 소문자 파일명)</summary>
        private static Dictionary<string, ModelMetadata> _modelMetadata;

        /// <summary>초기화 완료 여부</summary>
        private static bool _isInitialized;

        /// <summary>로드 시도 여부 (한 번만 시도)</summary>
        private static bool _attemptedLoad;

        /// <summary>
        /// 모델 로더가 초기화되었는지 여부를 반환합니다.
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Resources/Models/UserProvided/ 폴더에서 모든 GLB 프리팹을 로드하여 캐시합니다.
        /// 최초 호출 시 한 번만 실행되며, 이후에는 캐시된 데이터를 사용합니다.
        /// </summary>
        public static void Initialize()
        {
            if (_attemptedLoad)
                return;

            _attemptedLoad = true;
            _loadedModels = new Dictionary<string, GameObject>();
            _modelMetadata = new Dictionary<string, ModelMetadata>();

            try
            {
                // Resources.LoadAll로 UserProvided 폴더의 모든 GameObject 로드
                GameObject[] allPrefabs = Resources.LoadAll<GameObject>("Models/UserProvided");

                if (allPrefabs == null || allPrefabs.Length == 0)
                {
                    Debug.Log("[RuntimeModelLoader] UserProvided 폴더에 로드할 모델이 없습니다.");
                    _isInitialized = true;
                    return;
                }

                int loadedCount = 0;
                foreach (var prefab in allPrefabs)
                {
                    if (prefab == null)
                        continue;

                    // 파일명(확장자 제외)을 소문자로 변환하여 key로 사용
                    string key = prefab.name.ToLowerInvariant();

                    // 중복 키는 첫 번째 로드된 것으로 유지
                    if (!_loadedModels.ContainsKey(key))
                    {
                        _loadedModels.Add(key, prefab);

                        // Rig detection: check prefab hierarchy for bones
                        ModelType modelType = DetectModelType(prefab);
                        _modelMetadata.Add(key, new ModelMetadata(prefab, modelType));

                        loadedCount++;
                        Debug.Log($"[RuntimeModelLoader] 모델 '{key}' 감지: {modelType}");
                    }
                    else
                    {
                        Debug.LogWarning($"[RuntimeModelLoader] 중복 모델 키 무시: '{key}' (기존 유지)");
                    }
                }

                Debug.Log($"[RuntimeModelLoader] {loadedCount}개의 모델 로드 완료 (총 {allPrefabs.Length}개 프리팹 발견)");
                _isInitialized = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RuntimeModelLoader] 모델 로드 중 오류 발생: {ex.Message}");
                _isInitialized = true; // 실패해도 초기화는 완료 상태로 표시
            }
        }

        /// <summary>
        /// 지정된 모델 키에 해당하는 GLB 프리팹이 있는지 확인하고 반환합니다.
        /// </summary>
        /// <param name="modelKey">모델 키 (소문자, 확장자 제외 파일명, 예: "player", "soldier", "blue_castle")</param>
        /// <param name="prefab">찾은 GLB 프리팹 (없으면 null)</param>
        /// <returns>모델이 존재하면 true, 아니면 false</returns>
        public static bool TryGetModel(string modelKey, out GameObject prefab)
        {
            prefab = null;

            if (!EnsureInitialized())
                return false;

            if (string.IsNullOrEmpty(modelKey))
                return false;

            string key = modelKey.ToLowerInvariant();
            return _loadedModels.TryGetValue(key, out prefab);
        }

        /// <summary>
        /// 지정된 모델 키에 해당하는 GLB 프리팹이 있는지 확인합니다.
        /// </summary>
        /// <param name="modelKey">모델 키 (소문자, 확장자 제외 파일명)</param>
        /// <returns>모델이 존재하면 true, 아니면 false</returns>
        public static bool HasModel(string modelKey)
        {
            if (!EnsureInitialized())
                return false;

            if (string.IsNullOrEmpty(modelKey))
                return false;

            return _loadedModels.ContainsKey(modelKey.ToLowerInvariant());
        }

        /// <summary>
        /// 현재 로드된 모든 모델 키 목록을 반환합니다. (디버깅용)
        /// </summary>
        /// <returns>로드된 모델 키 문자열 배열</returns>
        public static string[] GetAllLoadedModels()
        {
            if (!EnsureInitialized() || _loadedModels == null)
                return System.Array.Empty<string>();

            var keys = new string[_loadedModels.Count];
            _loadedModels.Keys.CopyTo(keys, 0);
            return keys;
        }

        /// <summary>
        /// 캐시된 모든 로드된 모델의 수를 반환합니다.
        /// </summary>
        /// <returns>로드된 모델 수</returns>
        public static int LoadedModelCount()
        {
            if (!EnsureInitialized() || _loadedModels == null)
                return 0;

            return _loadedModels.Count;
        }

        /// <summary>
        /// 초기화가 완료되었는지 확인하고, 미완료 시 초기화를 시도합니다.
        /// </summary>
        /// <returns>초기화 성공 또는 이미 완료되었으면 true</returns>
        private static bool EnsureInitialized()
        {
            if (!_attemptedLoad)
            {
                Initialize();
            }
            return _isInitialized;
        }

        /// <summary>
        /// 캐시된 모든 모델을 초기화하고 다시 로드합니다.
        /// Resources 폴더 변경 시 호출할 수 있습니다.
        /// </summary>
        public static void Reload()
        {
            _attemptedLoad = false;
            _isInitialized = false;
            _loadedModels = null;
            _modelMetadata = null;
            Initialize();
        }

        // ──────────────────────────────────────────────
        //  Model Metadata API (v2: rig detection)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 지정된 모델 키에 해당하는 메타데이터 (프리팹 + ModelType)를 반환합니다.
        /// </summary>
        /// <param name="modelKey">모델 키 (소문자, 확장자 제외 파일명).</param>
        /// <param name="metadata">찾은 ModelMetadata 구조체 (없으면 기본값).</param>
        /// <returns>메타데이터가 존재하면 true, 아니면 false</returns>
        public static bool TryGetModelMetadata(string modelKey, out ModelMetadata metadata)
        {
            metadata = default;

            if (!EnsureInitialized() || _modelMetadata == null)
                return false;

            if (string.IsNullOrEmpty(modelKey))
                return false;

            string key = modelKey.ToLowerInvariant();
            return _modelMetadata.TryGetValue(key, out metadata);
        }

        /// <summary>
        /// 지정된 모델 키의 <see cref="ModelType"/>을 반환합니다.
        /// </summary>
        /// <param name="modelKey">모델 키 (소문자, 확장자 제외 파일명).</param>
        /// <returns>감지된 ModelType, 모델이 없으면 ModelType.Static.</returns>
        public static ModelType GetModelType(string modelKey)
        {
            if (TryGetModelMetadata(modelKey, out ModelMetadata metadata))
                return metadata.ModelType;

            return ModelType.Static;
        }

        /// <summary>
        /// GLB 프리팹의 뼈대 구조를 분석하여 ModelType을 감지합니다.
        /// </summary>
        /// <param name="prefab">로드된 GLB 프리팹 GameObject.</param>
        /// <returns>감지된 ModelType.</returns>
        private static ModelType DetectModelType(GameObject prefab)
        {
            if (prefab == null)
                return ModelType.Static;

            Transform root = prefab.transform;

            // 1. Check for Animator component (suggests rigged model)
            Animator animator = prefab.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                // Check if it's a humanoid avatar
                if (animator.isHuman)
                    return ModelType.RiggedHumanoid;

                // Has animator but not humanoid — could be generic rigged
                // Fall through to bone name detection
            }

            // 2. Check for SkinnedMeshRenderer with bones
            SkinnedMeshRenderer[] skinnedRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skinnedRenderers.Length > 0)
            {
                foreach (var smr in skinnedRenderers)
                {
                    if (smr.bones != null && smr.bones.Length > 0)
                    {
                        // Has actual bone references — detect type from bone names
                        return DetectTypeFromBoneNames(root);
                    }
                }
            }

            // 3. Check for Transform hierarchy with bone naming patterns
            return DetectTypeFromBoneNames(root);
        }

        /// <summary>
        /// 뼈대 이름 패턴을 분석하여 ModelType을 결정합니다.
        /// </summary>
        private static ModelType DetectTypeFromBoneNames(Transform root)
        {
            if (root == null)
                return ModelType.Static;

            // Collect all bone-like transforms
            var allBones = root.GetComponentsInChildren<Transform>(true);

            if (allBones.Length <= 1)
                return ModelType.Static; // Root only, no children

            // Check for quadruped bones
            if (AnimationBoneDefinitions.TryFindBoneCanonical(root, "UpperLeg_FL") != null ||
                AnimationBoneDefinitions.TryFindBoneCanonical(root, "Paw_FL") != null ||
                AnimationBoneDefinitions.TryFindBoneCanonical(root, "UpperLeg_HL") != null)
            {
                return ModelType.RiggedQuadruped;
            }

            // Check for humanoid bones
            if (AnimationBoneDefinitions.TryFindBoneCanonical(root, "UpperArm_L") != null &&
                AnimationBoneDefinitions.TryFindBoneCanonical(root, "UpperLeg_L") != null)
            {
                return ModelType.RiggedHumanoid;
            }

            // Check for head + spine (generic rigged)
            if (AnimationBoneDefinitions.TryFindBoneCanonical(root, "Head") != null &&
                AnimationBoneDefinitions.TryFindBoneCanonical(root, "Spine") != null)
            {
                // Count spine-like bones to differentiate monster from humanoid
                int spineCount = CountSpineLikeBones(root);
                if (spineCount >= 5)
                    return ModelType.RiggedMonster;
                return ModelType.RiggedHumanoid;
            }

            // Fallback: if there are many child transforms with bone-like structure
            if (allBones.Length >= 5)
                return ModelType.RiggedMonster;

            return ModelType.Static;
        }

        /// <summary>
        /// Spine/body-like bone의 개수를 셉니다.
        /// </summary>
        private static int CountSpineLikeBones(Transform root)
        {
            int count = 0;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t == root) continue;
                string name = t.name.ToLowerInvariant();
                if (name.Contains("spine") || name.Contains("body") ||
                    name.Contains("segment") || name.Contains("vertebra"))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
