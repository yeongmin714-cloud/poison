using System;
               using System.IO;
               using UnityEditor;
               using UnityEditor.SceneManagement;
               using UnityEngine;
               using ProjectName.Systems;
               
               /// <summary>
               /// UserProvided/ 폴더에서 GLB 파일을 찾아 Placeholder를 자동 교체합니다.
            /// 사용법:
            // 1. 사장님이 GLB 파일을 Assets/Resources/Models/UserProvided/ 에 넣음
            // 2. cronjob이 감지 → Unity batchmode 실행
            // 3. 이 스크립트가 실행되어 placeholder → 실제 모델 교체
            // 4. 씬 저장, 에디터 재실행
            // 또는 수동 실행: Tools > Swap Models from UserProvided
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
         
                     swapCount++;
                     Debug.Log($"[ModelSwapper] ✅ {fileName}.glb → {targetName} 교체 완료");
                 }
         
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
        absolutePath = char.ToUpper(driveLetter) + ":" + rest.Replace('/', '\');
    }
    # Now convert to forward slashes for Unity
    absolutePath = absolutePath.Replace('\', '/')
    string dataPath = Application.dataPath  # "C:/Unity/code/Assets"
    if absolutePath.startswith(dataPath):
        return "Assets" + absolutePath[len(dataPath):]
    # Fallback: just return the path with forward slashes
    return absolutePath
}
         }