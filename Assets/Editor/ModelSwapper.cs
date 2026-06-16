using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;

/// <summary>
/// UserProvided/ 폴더에서 GLB 파일을 찾아 Placeholder를 자동 교체합니다.
/// 사용법:
/// 1. 사장님이 GLB 파일을 Assets/Resources/Models/UserProvided/ 에 넣음
/// 2. cronjob이 감지 → Unity batchmode 실행
/// 3. 이 스크립트가 실행되어 placeholder → 실제 모델 교체
/// 4. 씬 저장, 에디터 재실행
/// 또는 수동 실행: Tools > Swap Models from UserProvided
/// </summary>
public static class ModelSwapper
{
    public static void SwapAndSave()
    {
        SwapAndSave(true);
    }
    // 감시 대상 폴더 (WSL 경로)
    private static readonly string _watchFolder = "Assets/Resources/Models/UserProvided";
    private static readonly string _scenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("Tools/Swap Models from UserProvided")]
    public static void SwapAllModels()
    {
        SwapAndSave(false);
    }

    /// <summary>
    /// UserProvided 폴더 스캔 → 모델 교체 → 씬 저장
    /// batchMode = true 일 때는 EditorApplication.Exit() 호출
    /// </summary>
    static void SwapAndSave(bool batchMode)
    {
        // UserProvided 폴더의 .glb 파일 찾기
        string folderPath = Path.Combine(Application.dataPath, "Resources/Models/UserProvided");
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[ModelSwapper] 폴더 없음: {folderPath}");
            if (batchMode) EditorApplication.Exit(0);
            return;
        }

        var glbFiles = Directory.GetFiles(folderPath, "*.glb");
        if (glbFiles.Length == 0)
        {
            Debug.Log("[ModelSwapper] 교체할 GLB 파일 없음");
            if (batchMode) EditorApplication.Exit(0);
            return;
        }

        Debug.Log($"[ModelSwapper] 찾은 GLB 파일: {glbFiles.Length}개");

        // 현재 씬 열기
        EditorSceneManager.OpenScene(_scenePath);
        int swapCount = 0;

        foreach (var glbPath in glbFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(glbPath);
            var (targetName, mode) = ModelMapping.GetMapping(fileName);

            if (targetName == null)
            {
                Debug.Log($"[ModelSwapper] 알 수 없는 파일 무시: {fileName}.glb");
                continue;
            }

            // 대상 GameObject 찾기
            var target = GameObject.Find(targetName);
            if (target == null)
            {
                Debug.Log($"[ModelSwapper] 대상 없음: {targetName} (아직 씬에 없음)");
                continue;
            }

            // GLB를 Unity Asset으로 로드
            // GLB 파일의 상대 경로 (Assets/...)
            string assetPath = GetRelativeAssetPath(glbPath);
            var loadedModel = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (loadedModel == null)
            {
                Debug.LogWarning($"[ModelSwapper] GLB 임포트 실패: {assetPath}");
                continue;
            }

            // 교체 실행
            bool isRigged = IsRiggedModel(loadedModel);
            if (isRigged)
                Debug.Log($"[ModelSwapper] ⚙️ {fileName}.glb 감지: Rigged 모델");

            if (mode == "child" && targetName == "Player")
            {
                // PlayerPlaceholder 제거 + GLB 모델을 자식으로 추가
                SwapPlayerPlaceholder(target, loadedModel);
            }
            else
            {
                // GameObject 통째로 교체
                SwapGameObject(target, loadedModel, targetName);
            }

            // 교체된 GameObject 찾기
            GameObject swappedObj = GameObject.Find(targetName);
            if (swappedObj != null && isRigged)
            {
                SetupRiggingOnSwap(swappedObj);
            }

            swapCount++;
            Debug.Log($"[ModelSwapper] ✅ {fileName}.glb → {targetName} 교체 완료{(isRigged ? " (Rigged)" : " (Static)")}");
        }

        // 티어드 모델 교체 실행 (기존 교체 후 추가)
        SwapTieredModelsInternal();

        // 씬 저장
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[ModelSwapper] 씬 저장 완료! {swapCount}개 교체됨");

        if (batchMode)
        {
            Debug.Log("[ModelSwapper] 배치모드 완료, 종료합니다.");
            EditorApplication.Exit(0);
        }
    }

    /// <summary>
    /// 플레이어 Placeholder → 실제 GLB 모델 교체
    /// PlayerPlaceholder의 도형들을 제거하고 GLB를 자식으로 추가
    /// </summary>
    static void SwapPlayerPlaceholder(GameObject player, GameObject glbPrefab)
    {
        // PlayerPlaceholder 제거 (도형들 삭제)
        var placeholder = player.GetComponent<PlayerPlaceholder>();
        if (placeholder != null)
        {
            placeholder.ClearPlaceholder();
            UnityEngine.Object.DestroyImmediate(placeholder);
        }

        // 기존 CharacterController는 유지 (충돌/중력 필요)
        // GLB 모델을 Player의 자식으로 추가
        var glbInstance = UnityEngine.Object.Instantiate(glbPrefab, player.transform);
        glbInstance.name = "Avatar";
        glbInstance.transform.localPosition = Vector3.zero;
        glbInstance.transform.localRotation = Quaternion.identity;
        glbInstance.transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 일반 GameObject 통째로 교체
    /// </summary>
    static void SwapGameObject(GameObject oldObj, GameObject glbPrefab, string newName)
    {
        var parent = oldObj.transform.parent;
        var pos = oldObj.transform.position;
        var rot = oldObj.transform.rotation;
        var scale = oldObj.transform.localScale;

        UnityEngine.Object.DestroyImmediate(oldObj);

        var newObj = UnityEngine.Object.Instantiate(glbPrefab, parent);
        newObj.name = newName;
        newObj.transform.position = pos;
        newObj.transform.rotation = rot;
        newObj.transform.localScale = scale;
    }

    /// <summary>
    /// 절대 경로 → Assets/... 상대 경로 변환
    /// </summary>
    static string GetRelativeAssetPath(string absolutePath)
    {
        // Convert WSL path to Windows path if necessary
        if (absolutePath.StartsWith("/mnt/"))
        {
            // Example: /mnt/c/Unity/code/Assets/Models/model.glb
            // -> C:/Unity/code/Assets/Models/model.glb
            var driveLetter = absolutePath[2]; // 'c'
            var rest = absolutePath.Substring(3); // "/Unity/code/Assets/Models/model.glb"
            absolutePath = char.ToUpper(driveLetter) + ":" + rest.Replace('/', '\\');
        }

        // Now convert to forward slashes for Unity
        absolutePath = absolutePath.Replace('\\', '/');
        string dataPath = Application.dataPath; // "C:/Unity/code/Assets"
        if (absolutePath.StartsWith(dataPath))
        {
            return "Assets" + absolutePath.Substring(dataPath.Length);
        }
        // Fallback: just return the path with forward slashes
        return absolutePath;
    }

    #region Tiered Model Swapping

    /// <summary>
    /// UserProvided 폴더를 스캔하여 티어드 GLB 변형을 찾고, Placeholder별로 매핑을 구축합니다.
    /// 메뉴를 통해 수동으로 실행: Tools > Swap Tiered Models
    /// 기존 SwapAndSave() 완료 후 자동으로 호출됩니다.
    /// </summary>
    [MenuItem("Tools/Swap Tiered Models")]
    public static void SwapTieredModels()
    {
        SwapTieredModelsInternal();
    }

    /// <summary>
    /// 티어드 모델 교체 내부 구현.
    /// UserProvided 폴더를 스캔하여 _tier1~_tier5 접미사가 있는 GLB 파일을 찾고,
    /// 각 Placeholder GameObject에 사용 가능한 티어 변형을 로깅합니다.
    /// </summary>
    static void SwapTieredModelsInternal()
    {
        string folderPath = Path.Combine(Application.dataPath, "Resources/Models/UserProvided");
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning("[ModelSwapper] Tiered Swap: 폴더 없음: " + folderPath);
            return;
        }

        var glbFiles = Directory.GetFiles(folderPath, "*.glb");
        if (glbFiles.Length == 0)
        {
            Debug.Log("[ModelSwapper] Tiered Swap: 교체할 GLB 파일 없음");
            return;
        }

        Debug.Log($"[ModelSwapper] Tiered Swap: {glbFiles.Length}개 GLB 파일 스캔 중...");

        // 1단계: 티어드 GLB 파일 찾기 (baseName → List<(suffix, assetPath)>)
        var tieredMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(string suffix, string assetPath)>>();

        foreach (var glbPath in glbFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(glbPath);

            // 티어 접미사 파싱
            if (!ModelMapping.TryParseTierSuffix(fileName, out string baseName, out string suffix))
                continue;

            // 기본 이름이 매핑에 존재하는지 확인
            var (placeholderName, _) = ModelMapping.GetMapping(baseName);
            if (placeholderName == null)
            {
                Debug.Log($"[ModelSwapper] Tiered Swap: 알 수 없는 기본 이름 무시: {baseName} (파일: {fileName})");
                continue;
            }

            string assetPath = GetRelativeAssetPath(glbPath);

            if (!tieredMap.ContainsKey(baseName))
                tieredMap[baseName] = new System.Collections.Generic.List<(string, string)>();

            tieredMap[baseName].Add((suffix, assetPath));
            Debug.Log($"[ModelSwapper] Tiered Swap: 감지 — {fileName}.glb → Placeholder: {placeholderName}, Tier: {suffix}");
        }

        if (tieredMap.Count == 0)
        {
            Debug.Log("[ModelSwapper] Tiered Swap: 티어드 GLB 파일을 찾을 수 없음");
            return;
        }

        // 2단계: 검증 및 요약 출력
        Debug.Log($"[ModelSwapper] Tiered Swap: === 티어드 모델 검증 ===");
        foreach (var kvp in tieredMap)
        {
            var (placeholderName, _) = ModelMapping.GetMapping(kvp.Key);
            var target = GameObject.Find(placeholderName);

            Debug.Log($"[ModelSwapper] Tiered Swap: '{kvp.Key}' → Placeholder: '{placeholderName}' (씬에 {(target != null ? "있음" : "없음")})");

            // 사용 가능한 티어 목록 표시
            string[] availableTiers = ModelMapping.GetAvailableTiers(kvp.Key);
            var foundTiers = new System.Collections.Generic.HashSet<string>();
            foreach (var (suffix, _) in kvp.Value)
                foundTiers.Add(suffix);

            foreach (string tier in availableTiers)
            {
                bool hasFile = foundTiers.Contains(tier);
                Debug.Log($"[ModelSwapper] Tiered Swap:   [{(hasFile ? "✓" : " ")}] {tier} ({(hasFile ? "파일 있음" : "파일 없음")})");
            }
        }

        Debug.Log($"[ModelSwapper] Tiered Swap: 완료 — {tieredMap.Count}개 기본 이름에 대한 티어드 매핑 구축");
    }

    #endregion

    #region Rig-Aware Swap Support

    /// <summary>
    /// GLB 프리팹이 Rigged 모델(뼈대 있음)인지 Static 모델(뼈대 없음)인지 확인합니다.
    /// </summary>
    /// <param name="glbPrefab">확인할 GLB 프리팹.</param>
    /// <returns>Rigged 모델이면 true, Static이면 false.</returns>
    static bool IsRiggedModel(GameObject glbPrefab)
    {
        if (glbPrefab == null)
            return false;

        // 1. Animator 컴포넌트 확인
        Animator animator = glbPrefab.GetComponentInChildren<Animator>();
        if (animator != null)
            return true;

        // 2. SkinnedMeshRenderer의 bones 배열 확인
        SkinnedMeshRenderer[] skinnedRenderers = glbPrefab.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var smr in skinnedRenderers)
        {
            if (smr.bones != null && smr.bones.Length > 0)
                return true;
        }

        // 3. Transform 계층에서 본 패턴 확인 (예: "Bone", "Armature" 키워드)
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
    /// 교체된 Rigged 모델에 AnimationRiggingSetup을 추가하고 설정합니다.
    /// 또한 MotionDetector를 추가하여 자동 모션 설정을 활성화합니다.
    /// </summary>
    /// <param name="swappedObject">씬에 교체된 GameObject.</param>
    static void SetupRiggingOnSwap(GameObject swappedObject)
    {
        if (swappedObject == null) return;

        // AnimationRiggingSetup 추가
        var riggingSetup = swappedObject.GetComponent<AnimationRiggingSetup>();
        if (riggingSetup == null)
        {
            riggingSetup = swappedObject.AddComponent<AnimationRiggingSetup>();
        }

        riggingSetup.FindBones();
        riggingSetup.SetupRigging();

        // MotionDetector 추가 (자동 모션 설정)
        var motionDetector = swappedObject.GetComponent<MotionDetector>();
        if (motionDetector == null)
        {
            motionDetector = swappedObject.AddComponent<MotionDetector>();
        }

        motionDetector.DetectAndSetup();

        Debug.Log($"[ModelSwapper] Rigging setup completed on '{swappedObject.name}'");
    }

    #endregion
}
