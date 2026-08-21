using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// MainScene 정리 및 복구 스크립트 (Editor 전용 - Reflection 사용).
/// 사용법: Tools > Scene Cleanup > Clean And Restore MainScene
/// </summary>
public class SceneCleanup
{
    private const string ScenePath = "Assets/Scenes/MainScene.unity";

    // Reflection용 타입 캐시
    private static System.Type t_PlayerMovement;
    private static System.Type t_PlayerCombat;
    private static System.Type t_PlayerHealth;
    private static System.Type t_PlayerInventory;
    private static System.Type t_PlayerStats;
    private static System.Type t_BombThrower;
    private static System.Type t_BuffManager;
    private static System.Type t_PlayerPlaceholder;
    private static System.Type t_GameSetup;

    static SceneCleanup()
    {
        CacheTypes();
    }

    static void CacheTypes()
    {
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (a.FullName.StartsWith("Assembly-CSharp") || a.FullName.StartsWith("ProjectName"))
            {
                t_PlayerMovement = a.GetType("ProjectName.Systems.PlayerMovement");
                t_PlayerCombat = a.GetType("ProjectName.Systems.PlayerCombat");
                t_PlayerHealth = a.GetType("ProjectName.Core.PlayerHealth");
                t_PlayerInventory = a.GetType("ProjectName.Core.PlayerInventory");
                t_PlayerStats = a.GetType("ProjectName.Core.PlayerStats");
                t_BombThrower = a.GetType("ProjectName.Systems.BombThrower");
                t_BuffManager = a.GetType("ProjectName.Core.BuffManager");
                t_PlayerPlaceholder = a.GetType("ProjectName.Systems.PlayerPlaceholder");
                
                // GameSetup is in global namespace
                t_GameSetup = a.GetType("GameSetup");
                
                // Also try without namespace prefix for other types
                if (t_PlayerMovement == null) t_PlayerMovement = a.GetType("PlayerMovement");
                if (t_PlayerCombat == null) t_PlayerCombat = a.GetType("PlayerCombat");
                if (t_PlayerHealth == null) t_PlayerHealth = a.GetType("PlayerHealth");
                if (t_PlayerInventory == null) t_PlayerInventory = a.GetType("PlayerInventory");
                if (t_PlayerStats == null) t_PlayerStats = a.GetType("PlayerStats");
                if (t_BombThrower == null) t_BombThrower = a.GetType("BombThrower");
                if (t_BuffManager == null) t_BuffManager = a.GetType("BuffManager");
                if (t_PlayerPlaceholder == null) t_PlayerPlaceholder = a.GetType("PlayerPlaceholder");
                
                if (t_PlayerMovement != null) break;
            }
        }
        
        // Fallback: search all assemblies
        if (t_GameSetup == null)
        {
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t_GameSetup = a.GetType("GameSetup");
                if (t_GameSetup != null) break;
            }
        }
    }

    [MenuItem("Tools/Scene Cleanup/Clean And Restore MainScene")]
        public static void CleanAndRestore()
        {
            Debug.Log("[SceneCleanup] === CleanAndRestore START ===");
        
            // 배치 모드에서는 다이얼로그 없이 바로 실행 (DisplayDialog가 배치 모드에서 예외 발생)
            bool isBatch = false;
            try { var dummy = EditorUtility.DisplayDialog("", "", ""); } catch { isBatch = true; }
            Debug.Log($"[SceneCleanup] isBatch = {isBatch}");

            if (!isBatch && !EditorUtility.DisplayDialog("Scene Cleanup",
                "MainScene을 완전히 정리하고 게임 시스템을 다시 설정합니다.\n\n" +
                "수행 작업:\n" +
                "1. 중복 오브젝트 제거 (Cube.*, NurbsPath.*, tree.*, BezierCurve.* 등)\n" +
                "2. Player 계층 구조 복구 (Player를 루트로, Avatar를 자식으로)\n" +
                "3. Player Camera 계층 구조 복구\n" +
                "4. 모든 게임 시스템 재생성 (GameSetup 실행)\n" +
                "5. 씬 저장\n\n계속하시겠습니까?",
                "Clean & Restore", "Cancel"))
            {
                Debug.Log("[SceneCleanup] User cancelled");
                return;
            }

            Debug.Log("[SceneCleanup] Starting cleanup...");

            try
            {
                Debug.Log("========================================");
                Debug.Log("[SceneCleanup] === MainScene 정리 및 복구 시작 ===");
                Debug.Log("========================================");

                // 씬 열기
                Debug.Log("[SceneCleanup] Opening scene...");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[SceneCleanup] Scene opened");

                int removedCount = 0;

                // 1. 중복 오브젝트 제거
                Debug.Log("[SceneCleanup] Removing duplicate objects...");
                removedCount += RemoveDuplicateObjects();
                Debug.Log($"[SceneCleanup] Removed {removedCount} duplicates");

                // 2. Player 계층 구조 복구
                Debug.Log("[SceneCleanup] Fixing Player hierarchy...");
                FixPlayerHierarchy();
                Debug.Log("[SceneCleanup] Player hierarchy fixed");

                // 3. Player Camera 계층 구조 복구
                Debug.Log("[SceneCleanup] Fixing Player Camera hierarchy...");
                FixPlayerCameraHierarchy();
                Debug.Log("[SceneCleanup] Player Camera hierarchy fixed");

                // 4. GameSetup 실행 (모든 게임 시스템 생성)
                Debug.Log("[SceneCleanup] Running GameSetup...");
                RunGameSetup();
                Debug.Log("[SceneCleanup] GameSetup completed");

                // 5. 씬 저장
                Debug.Log("[SceneCleanup] Saving scene...");
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log($"[SceneCleanup] 씬 저장 완료! 제거된 오브젝트: {removedCount}개");

                Debug.Log("========================================");
                Debug.Log("[SceneCleanup] ✅ MainScene 정리 및 복구 완료!");
                Debug.Log("========================================");

                if (!isBatch)
                {
                    EditorUtility.DisplayDialog("Complete", 
                        $"MainScene 정리 및 복구 완료!\n\n" +
                        $"제거된 중복 오브젝트: {removedCount}개\n" +
                        $"Player 계층 구조 복구됨\n" +
                        $"게임 시스템 재생성됨\n" +
                        $"씬 저장됨", "OK");
                }
            
                Debug.Log("[SceneCleanup] === CleanAndRestore END ===");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SceneCleanup] ERROR: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

    [MenuItem("Tools/Scene Cleanup/Remove Duplicate Objects Only")]
    public static void RemoveDuplicatesOnly()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int count = RemoveDuplicateObjects();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Done", $"Removed {count} duplicate objects", "OK");
    }

    /// <summary>
    /// 중복 오브젝트 제거 (Cube.*, NurbsPath.*, tree.*, BezierCurve.* 등)
    /// </summary>
    static int RemoveDuplicateObjects()
    {
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        var toRemove = new List<GameObject>();
        var seenNames = new HashSet<string>();

        foreach (var go in allObjects)
        {
            if (go == null) continue;

            string name = go.name;
            bool isDuplicate = false;

            // 중복 패턴 확인 (Cube., NurbsPath., tree., BezierCurve. 등)
            if (name.StartsWith("Cube.") ||
                name.StartsWith("NurbsPath.") ||
                name.StartsWith("tree.") ||
                name.StartsWith("BezierCurve."))
            {
                if (seenNames.Contains(name))
                {
                    isDuplicate = true;
                }
                else
                {
                    seenNames.Add(name);
                }
            }

            // Player 뼈대 오브젝트들 중 Player 자체가 아닌 것들
            if (IsPlayerBone(name) && name != "Player")
            {
                // Player의 자식인 뼈대들은 유지 (Avatar 하위)
                if (go.transform.root.name == "Player" && go.transform.parent != null)
                {
                    // Avatar 하위면 유지
                }
                else
                {
                    isDuplicate = true;
                }
            }

            // 중복된 GameObject 이름들 (같은 이름이 여러 개인 경우)
            // 첫 번째 것만 남기고 나머지 제거
            if (!isDuplicate)
            {
                // 씬 루트에 있는 동일 이름 오브젝트들 중복 확인
                var rootObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
                int count = 0;
                foreach (var rootObj in rootObjects)
                {
                    if (rootObj.transform.parent == null && rootObj.name == name)
                    {
                        count++;
                        if (count > 1)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                }
            }

            if (isDuplicate)
            {
                toRemove.Add(go);
            }
        }

        // 중복되지 않은 첫 번째 것만 남기기 위해 이름별로 그룹화
        var nameGroups = toRemove.GroupBy(g => g.name).ToDictionary(g => g.Key, g => g.ToList());
        var finalToRemove = new List<GameObject>();
        
        foreach (var kvp in nameGroups)
        {
            // 첫 번째는 유지, 나머지는 제거
            for (int i = 1; i < kvp.Value.Count; i++)
            {
                finalToRemove.Add(kvp.Value[i]);
            }
        }

        Debug.Log($"[SceneCleanup] 중복 후보: {toRemove.Count}개, 최종 제거 대상: {finalToRemove.Count}개");

        foreach (var go in finalToRemove)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        Debug.Log($"[SceneCleanup] 중복 오브젝트 {finalToRemove.Count}개 제거됨");
        return finalToRemove.Count;
    }

    static bool IsPlayerBone(string name)
    {
        string[] boneNames = { "spine", "pelvis", "thigh", "shin", "toe", "upper_arm", "lower_arm", "hand", 
                              "head", "neck", "clavicle", "rib", "abdomen", "chest", "hip", "knee", "ankle",
                              "shoulder", "elbow", "wrist", "finger", "thumb", "heel", "foot" };
        string lower = name.ToLower();
        return boneNames.Any(b => lower.Contains(b));
    }

    /// <summary>
        /// Player 계층 구조 복구:
        /// Player (루트, 컴포넌트 보유) > Avatar (GLB 모델, 뼈대)
        /// </summary>
        static void FixPlayerHierarchy()
        {
            // Player 태그로 찾기
            var player = GameObject.FindWithTag("Player");
        
            // 태그로 못 찾으면 이름으로 찾기
            if (player == null)
            {
                player = GameObject.Find("Player");
            }
        
            // 그래도 없으면 생성 (fallback)
            if (player == null)
            {
                Debug.Log("[SceneCleanup] Player가 없음 - 새로 생성");
                player = new GameObject("Player");
                player.tag = "Player";
                player.transform.position = new Vector3(0, 1.1f, 0);
            }
        
            Debug.Log($"[SceneCleanup] Found/Created Player: {player.name}, tag: {player.tag}");

            // Player가 뼈대의 자식인지 확인
            var parent = player.transform.parent;
            if (parent != null && IsPlayerBone(parent.name))
            {
                Debug.Log($"[SceneCleanup] Player가 뼈대({parent.name})의 자식임 - 계층 구조 복구 필요");

                // Avatar 찾기 (GLB 모델 루트)
                var avatar = FindAvatarUnderBone(parent);
                if (avatar != null)
                {
                    // Player를 루트로 이동
                    var originalPos = player.transform.position;
                    var originalRot = player.transform.rotation;

                    // Player를 씬 루트로
                    player.transform.SetParent(null);
                    player.transform.position = originalPos;
                    player.transform.rotation = originalRot;

                    // Avatar를 Player의 자식으로
                    avatar.transform.SetParent(player.transform);
                    avatar.transform.localPosition = Vector3.zero;
                    avatar.transform.localRotation = Quaternion.identity;
                    avatar.transform.localScale = Vector3.one;

                    Debug.Log($"[SceneCleanup] Player 계층 구조 복구 완료: Player > Avatar");
                }
            }
            else
            {
                Debug.Log("[SceneCleanup] Player 계층 구조 정상");
            }

            // Player 컴포넌트 확인/추가
            EnsurePlayerComponents(player);
        }

    static GameObject FindAvatarUnderBone(Transform bone)
    {
        // 뼈대 계층에서 Avatar 찾기 (보통 GLB 모델의 루트)
        var root = bone.root;
        if (root.name == "Avatar" || root.name.Contains("Model") || root.GetComponentInChildren<SkinnedMeshRenderer>() != null)
        {
            return root.gameObject;
        }

        // 자식 중에 SkinnedMeshRenderer가 있는 것 찾기
        foreach (Transform child in root)
        {
            if (child.GetComponentInChildren<SkinnedMeshRenderer>() != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    static void EnsurePlayerComponents(GameObject player)
    {
        // 필수 컴포넌트들 (Reflection 사용)
        AddComponentIfMissing(player, typeof(CharacterController));
        AddComponentIfMissing(player, typeof(Animator));
        
        var rb = player.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = player.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        AddComponentByType(player, t_PlayerMovement);
        AddComponentByType(player, t_PlayerCombat);
        AddComponentByType(player, t_PlayerHealth);
        AddComponentByType(player, t_PlayerInventory);
        AddComponentByType(player, t_PlayerStats);
        AddComponentByType(player, typeof(UnityEngine.InputSystem.PlayerInput));
        AddComponentByType(player, t_BombThrower);
        AddComponentByType(player, t_BuffManager);
        AddComponentByType(player, t_PlayerPlaceholder);

        // PlayerInput 설정
        var pi = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (pi != null)
        {
            pi.defaultActionMap = "Player";
            pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
        }

        Debug.Log("[SceneCleanup] Player 컴포넌트 확인/추가 완료");
    }

    static void AddComponentIfMissing(GameObject go, System.Type type)
    {
        if (type != null && go.GetComponent(type) == null)
        {
            go.AddComponent(type);
        }
    }

    static void AddComponentByType(GameObject go, System.Type type)
    {
        if (type != null && go.GetComponent(type) == null)
        {
            go.AddComponent(type);
        }
    }

    /// <summary>
    /// Player Camera 계층 구조 복구
    /// </summary>
    static void FixPlayerCameraHierarchy()
    {
        var cam = GameObject.Find("Player Camera");
        
        // Player Camera가 없으면 생성
        if (cam == null)
        {
            Debug.Log("[SceneCleanup] Player Camera가 없음 - 새로 생성");
            cam = new GameObject("Player Camera");
            cam.tag = "MainCamera";
            
            var camera = cam.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Depth;
            camera.cullingMask = -1;
            camera.depth = 0;
        }
        
        var parent = cam.transform.parent;
        if (parent != null && IsPlayerBone(parent.name))
        {
            Debug.Log($"[SceneCleanup] Player Camera가 뼈대({parent.name})의 자식임 - 복구");

            // Player Camera를 씬 루트로
            cam.transform.SetParent(null);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;

            // Camera 컴포넌트 확인
            if (cam.GetComponent<Camera>() == null)
                cam.AddComponent<Camera>();

            // Tag 확인
            if (cam.tag != "MainCamera")
                cam.tag = "MainCamera";

            Debug.Log("[SceneCleanup] Player Camera 계층 구조 복구 완료");
        }
        else
        {
            Debug.Log("[SceneCleanup] Player Camera 계층 구조 정상");
        }
    }

    /// <summary>
    /// GameSetup 실행하여 모든 게임 시스템 생성
    /// </summary>
    static void RunGameSetup()
    {
        // GameSetup 컴포넌트가 있는 오브젝트 찾기/생성
        var setupObj = GameObject.Find("GameSetup");
        if (setupObj == null)
        {
            setupObj = new GameObject("GameSetup");
        }

        var setup = setupObj.GetComponent(t_GameSetup);
        if (setup == null && t_GameSetup != null)
        {
            setup = setupObj.AddComponent(t_GameSetup);
        }

        if (setup != null)
        {
            // GameSetup의 private 메서드 호출 (Reflection)
            var setupPlayerMethod = t_GameSetup.GetMethod("SetupPlayerComponents", BindingFlags.NonPublic | BindingFlags.Instance);
            var setupWorldMethod = t_GameSetup.GetMethod("SetupWorldComponents", BindingFlags.NonPublic | BindingFlags.Instance);

            if (setupPlayerMethod != null)
                setupPlayerMethod.Invoke(setup, null);
            if (setupWorldMethod != null)
                setupWorldMethod.Invoke(setup, null);

            Debug.Log("[SceneCleanup] GameSetup 실행 완료");
        }
        else
        {
            Debug.LogWarning("[SceneCleanup] GameSetup 타입을 찾을 수 없음");
        }
    }

    [MenuItem("Tools/Scene Cleanup/Fix Player Hierarchy Only")]
    public static void FixPlayerOnly()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        FixPlayerHierarchy();
        FixPlayerCameraHierarchy();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Done", "Player hierarchy fixed", "OK");
    }
}