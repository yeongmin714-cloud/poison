using System;
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

        /// <summary>별칭 맵 (short name → actual model key)</summary>
        private static Dictionary<string, string> _aliases;

        /// <summary>초기화 완료 여부</summary>
        private static bool _isInitialized;

        /// <summary>로드 시도 여부 (한 번만 시도)</summary>
        private static bool _attemptedLoad;

        /// <summary>
        /// 모델 로더가 초기화되었는지 여부를 반환합니다.
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// 별칭 맵을 초기화합니다. (더 이상 모든 GLB를 한 번에 로드하지 않음)
        /// 각 모델은 TryGetModel 호출 시 개별적으로 지연 로드됩니다.
        /// </summary>
        public static void Initialize()
        {
            if (_attemptedLoad)
                return;

            _attemptedLoad = true;
            _loadedModels = new Dictionary<string, GameObject>();
            _modelMetadata = new Dictionary<string, ModelMetadata>();
            _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "player", "Player_Rigged" },
                { "soldier", "soldier_lv1-20_rigged" },
                { "soldier_lv1", "soldier_lv1-20_rigged" },
                { "soldier_lv20", "soldier_lv20-40_rigged" },
                { "soldier_lv40", "soldier_lv40-50_rigged" },
                { "mercenary", "mercenary_rigged" },
                { "bard", "bard_rigged" },
                { "lord", "npc_lord_rigged" },
                { "king", "npc_king_rigged" },
                { "shop_npc", "npc_shop_rigged" },
                // [128-2 P1] 스폰 경로 24종 확장 (마을 NPC 전면 교체)
                { "man1", "Npc2026/Npc_Man_1/Npc_Man_1_rigged" },
                { "man2", "Npc2026/Npc_Man_2/Npc_Man_2_rigged" },
                { "man3", "Npc2026/Npc_Man_3/Npc_Man_3_rigged" },
                { "man4", "Npc2026/Npc_Man_4/Npc_Man_4_rigged" },
                { "man5", "Npc2026/Npc_Man_5/Npc_Man_5_rigged" },
                { "girl1", "Npc2026/Npc_Girl_1/Npc_Girl_1_rigged" },
                { "girl2", "Npc2026/Npc_Girl_2/Npc_Girl_2_rigged" },
                { "girl3", "Npc2026/Npc_Girl_3/Npc_Girl_3_rigged" },
                { "girl4", "Npc2026/Npc_Girl_4/Npc_Girl_4_rigged" },
                { "girl5", "Npc2026/Npc_Girl_5/Npc_Girl_5_rigged" },
                { "girl6", "Npc2026/Npc_Girl_6/Npc_Girl_6_rigged" },
                { "oldman1", "Npc2026/Npc_OldMan_1/Npc_OldMan_1_rigged" },
                { "oldman2", "Npc2026/Npc_OldMan_2/Npc_OldMan_2_rigged" },
                { "oldman3", "Npc2026/Npc_OldMan_3/Npc_OldMan_3_rigged" },
                { "boy1", "Npc2026/Npc_Boy_1/Npc_Boy_1_rigged" },
                { "boy2", "Npc2026/Npc_Boy_2/Npc_Boy_2_rigged" },
                { "boy3", "Npc2026/Npc_Boy_3/Npc_Boy_3_rigged" },
                { "boy4", "Npc2026/Npc_Boy_4/Npc_Boy_4_rigged" },
                { "boy5", "Npc2026/Npc_Boy_5/Npc_Boy_5_rigged" },
                { "oldwoman1", "Npc2026/Npc_OldWoman_1/Npc_OldWoman_1_rigged" },
                { "oldwoman2", "Npc2026/Npc_OldWoman_2/Npc_OldWoman_2_rigged" },
                { "oldwoman3", "Npc2026/Npc_OldWoman_3/Npc_OldWoman_3_rigged" },
                { "oldwoman4", "Npc2026/Npc_OldWoman_4/Npc_OldWoman_4_rigged" },
                { "oldwoman5", "Npc2026/Npc_OldWoman_5/Npc_OldWoman_5_rigged" },
                { "dracula", "npc_dracula_rigged" },
                { "hut", "hut" },
                { "shop", "shop" },
                { "castle", "castle" },
                { "bar", "bar" },
                { "church", "hut" },
                { "craft_equip", "craft_equipment" },
                { "craft_cook", "craft_cook" },
                { "craft_blend", "craft_blend" },
                { "kingdom", "kingdom" },
                { "red_castle", "red_castle" },
                { "blue_castle", "blue_castle" },
                { "green_castle", "green_castle" },
                { "purple_castle", "purple_castle" },
                // [레스토랑 실내] 식당 가구 별칭 — Models/Restaurant/ GLB (RestaurantInteriorBuilder 배치용)
                { "restaurantchair", "Chair_1_A" },
                { "restauranttable", "Table_1_A" },
                { "restaurantcounter", "StationTable_1_A" },
            };

            _isInitialized = true;
            Debug.Log("[RuntimeModelLoader] 별칭 맵 초기화 완료 (모델 지연 로딩 모드)");
        }

        /// <summary>
        /// 단일 모델 키를 Resources.Load로 개별 로드하여 캐시합니다.
        /// 기존에 로드된 모델이 있으면 캐시에서 반환합니다.
        /// </summary>
        private static bool LoadModelByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (_loadedModels.ContainsKey(key))
                return true;

            // Resources.Load로 개별 모델 로드 (한 번에 전체 로드 X)
            string resourcePath = "Models/UserProvided/" + key;
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                // [147차 P-A2] 던전 팩 폴백 — Models/Dungeon/ 2차 시도(320종 실스케일 에셋).
                //   UserProvided 동작 무손상(폴백 추가만). 카탈로그: DungeonAssetCatalog.ResourceRoot.
                prefab = Resources.Load<GameObject>("Models/Dungeon/" + key);
            }
            if (prefab == null)
            {
                // [155차 P0] 공동묘지 팩 폴백 — Models/Cemetery/ 3차 시도(Necropoly 선별 프리팹).
                //   카탈로그: AfterlifeCemeteryCatalog(155차 — 이계 공동묘지화 전용).
                prefab = Resources.Load<GameObject>("Models/Cemetery/" + key);
            }
            if (prefab == null)
            {
                // [레스토랑 실내] 식당 가구 팩 폴백 — Models/Restaurant/ 4차 시도(Restourant Furniture GLB).
                //   카탈로그: RestaurantInteriorBuilder(식당 실내 전용).
                prefab = Resources.Load<GameObject>("Models/Restaurant/" + key);
            }
            if (prefab == null)
            {
                // 전체 폴더 스캔 없이 개별 로드 실패 — 해당 키의 모델이 없음
                return false;
            }

            _loadedModels.Add(key, prefab);
            ModelType modelType = DetectModelType(prefab);
            _modelMetadata.Add(key, new ModelMetadata(prefab, modelType));
            Debug.Log($"[RuntimeModelLoader] 지연 로드: '{key}' ({modelType})");
            return true;
        }

        /// <summary>
        /// 지정된 모델 키에 해당하는 GLB 프리팹과 메타데이터를 반환합니다.
        /// 별칭(alias)을 먼저 확인하여 짧은 이름을 실제 모델 키로 변환합니다.
        /// </summary>
        /// <param name="key">모델 키 또는 별칭 (대소문자 무시)</param>
        /// <param name="model">찾은 GLB 프리팹 (없으면 null)</param>
        /// <param name="metadata">찾은 ModelMetadata 구조체 (없으면 기본값)</param>
        /// <returns>모델이 존재하면 true, 아니면 false</returns>
        public static bool TryGetModel(string key, out GameObject model, out ModelMetadata metadata)
        {
            Initialize();
            string rawKey = key ?? "";
            string lowerKey = rawKey.ToLowerInvariant();
            // Check alias
            if (_aliases != null && _aliases.TryGetValue(lowerKey, out string realKey))
                lowerKey = realKey;
            // Lazy load: not yet loaded? try per-key Resources.Load
            if (!_loadedModels.ContainsKey(lowerKey))
            {
                if (!LoadModelByKey(lowerKey) && !string.Equals(rawKey, lowerKey, StringComparison.Ordinal))
                {
                    // [159차 P2] 대소문자 보존 2차 시도 — Restaurant 팩(PascalCase 파일명: Table_2_C 등)을
                    //   변형 키로 직접 로드하기 위함. Resources.Load는 빌드에서 대소문자를 구분하므로
                    //   소문자화된 키 실패 시 원본 대소문자 키로 재시도한다(ArenaInteriorBuilder 변형 순환).
                    LoadModelByKey(rawKey);
                }
            }
            if (_loadedModels.TryGetValue(lowerKey, out model) && _modelMetadata.TryGetValue(lowerKey, out metadata))
                return true;
            if (!string.Equals(rawKey, lowerKey, StringComparison.Ordinal) &&
                _loadedModels.TryGetValue(rawKey, out model) && _modelMetadata.TryGetValue(rawKey, out metadata))
                return true;
            metadata = default;
            model = null;
            return false;
        }

        /// <summary>
        /// 지정된 모델 키에 해당하는 GLB 프리팹이 있는지 확인하고 반환합니다.
        /// 별칭(alias)을 먼저 확인하여 짧은 이름을 실제 모델 키로 변환합니다.
        /// </summary>
        /// <param name="key">모델 키 (소문자, 확장자 제외 파일명, 예: "player", "soldier", "blue_castle")</param>
        /// <param name="model">찾은 GLB 프리팹 (없으면 null)</param>
        /// <returns>모델이 존재하면 true, 아니면 false</returns>
        public static bool TryGetModel(string key, out GameObject model)
        {
            return TryGetModel(key, out model, out _);
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

            string rawKey = modelKey ?? "";
            string key = rawKey.ToLowerInvariant();
            // 별칭 해석: TryGetModel과 동일한 alias 로직 적용
            if (_aliases != null && _aliases.TryGetValue(key, out string realKey))
                key = realKey;
            // Lazy load: not yet loaded? try per-key Resources.Load
            if (!_loadedModels.ContainsKey(key))
            {
                if (!LoadModelByKey(key) && !string.Equals(rawKey, key, StringComparison.Ordinal))
                {
                    // [159차 P2] 대소문자 보존 2차 시도 (TryGetModel과 동일 — Restaurant 팩 PascalCase 키)
                    LoadModelByKey(rawKey);
                }
            }
            return _loadedModels.ContainsKey(key) ||
                   (!string.Equals(rawKey, key, StringComparison.Ordinal) && _loadedModels.ContainsKey(rawKey));
        }

        /// <summary>
        /// [128-2 P1] 별칭(alias)을 리소스 키로 변환합니다.
        /// 등록된 별칭이 있으면 리소스 키를 반환하고, 없으면 원본 문자열을 그대로 반환합니다.
        /// </summary>
        /// <param name="alias">별칭 (예: "man1")</param>
        /// <param name="resourceKey">변환된 리소스 키</param>
        /// <returns>변환 성공 여부</returns>
        public static bool TryGetResourceKey(string alias, out string resourceKey)
        {
            if (string.IsNullOrEmpty(alias))
            {
                resourceKey = null;
                return false;
            }

            if (EnsureInitialized() && _aliases != null && _aliases.TryGetValue(alias.ToLowerInvariant(), out resourceKey))
            {
                return true;
            }

            resourceKey = alias;
            return false;
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

            string rawKey = modelKey ?? "";
            string key = rawKey.ToLowerInvariant();
            // 별칭 해석: TryGetModel과 동일한 alias 로직 적용
            if (_aliases != null && _aliases.TryGetValue(key, out string realKey))
                key = realKey;
            // Lazy load: not yet loaded? try per-key Resources.Load
            if (!_loadedModels.ContainsKey(key))
            {
                if (!LoadModelByKey(key) && !string.Equals(rawKey, key, StringComparison.Ordinal))
                {
                    // [159차 P2] 대소문자 보존 2차 시도 (TryGetModel과 동일 — Restaurant 팩 PascalCase 키)
                    LoadModelByKey(rawKey);
                }
            }
            if (_modelMetadata.TryGetValue(key, out metadata))
                return true;
            if (!string.Equals(rawKey, key, StringComparison.Ordinal))
                return _modelMetadata.TryGetValue(rawKey, out metadata);
            return false;
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

            // ── 넘버링 본 모델 (bone_0, bone_1, ... ) 4족 추정 ──
            // 본 이름이 bone_N 형태이고, Root 아래 깊이 3+ 체인이 4개 이상이면 4족보행으로 판정
            bool allNumbered = true;
            foreach (var t in allBones)
            {
                if (t == root) continue;
                string nm = t.name;
                if (!(nm.StartsWith("bone_", System.StringComparison.OrdinalIgnoreCase)
                    || nm.StartsWith("Bone", System.StringComparison.OrdinalIgnoreCase)
                    || int.TryParse(nm, out _)))
                {
                    allNumbered = false;
                    break;
                }
            }
            if (allNumbered && allBones.Length >= 12)
            {
                // Root 본 추정: 전체 본 중 자식이 가장 많은 본 (Wolf는 bone_0가 SkeletonBindArmature 자식이라 root 직계가 아님)
                Transform rootBone = null;
                int maxChildren = -1;
                foreach (var t in allBones)
                {
                    if (t.childCount > maxChildren)
                    {
                        maxChildren = t.childCount;
                        rootBone = t;
                    }
                }
                if (rootBone != null)
                {
                    int legChainCount = 0;
                    foreach (Transform child in rootBone)
                    {
                        int depth = 0;
                        Transform c = child;
                        while (c != null && c.childCount > 0) { depth++; c = c.GetChild(0); }
                        if (depth >= 3) legChainCount++;
                    }
                    if (legChainCount >= 4)
                        return ModelType.RiggedQuadruped;
                }
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
