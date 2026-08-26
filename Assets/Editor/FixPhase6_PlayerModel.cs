using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class FixPhase6_PlayerModel
{
    [MenuItem("Tools/Poison/Fix Phase 6 - PlayerModel")]
    public static void FixPlayerModel()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        var scene = SceneManager.GetSceneByName("MainScene");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath);
        }

        Debug.Log("=== PHASE 6: PLAYER MODEL VERIFICATION START ===");

        var player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("[Phase6] Player NOT FOUND");
            return;
        }

        var playerModel = player.transform.Find("PlayerModel")?.gameObject;
        if (playerModel == null)
        {
            Debug.LogError("[Phase6] PlayerModel NOT FOUND");
            return;
        }

        // 1. Animator 확인 및 설정
        var animator = playerModel.GetComponent<Animator>();
        if (animator == null) animator = playerModel.AddComponent<Animator>();

        // 2. ModelAnimatorAssigner 확인
        var assignerType = Type.GetType("ProjectName.Systems.Animation.ModelAnimatorAssigner, Assembly-CSharp")
                          ?? Type.GetType("ProjectName.Systems.Animation.ModelAnimatorAssigner, Assembly-CSharp-firstpass")
                          ?? AppDomain.CurrentDomain.GetAssemblies()
                              .SelectMany(a => a.GetTypes())
                              .FirstOrDefault(t => t.Name == "ModelAnimatorAssigner" && typeof(Component).IsAssignableFrom(t));
        
        if (assignerType == null)
        {
            Debug.LogWarning("[Phase6] ModelAnimatorAssigner type not found or not a Component, skipping");
        }
        else
        {
            var assigner = playerModel.GetComponent(assignerType);
            if (assigner == null)
            {
                assigner = playerModel.AddComponent(assignerType);
            }

            if (assigner != null)
            {
                // modelType 설정
                var modelTypeField = assignerType.GetField("modelType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var modelTypeProp = assignerType.GetProperty("modelType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (modelTypeField != null)
                {
                    var enumType = modelTypeField.FieldType;
                    if (enumType.IsEnum)
                    {
                        var humanoid = Enum.Parse(enumType, "Humanoid");
                        modelTypeField.SetValue(assigner, humanoid);
                    }
                }
                else if (modelTypeProp != null)
                {
                    var enumType = modelTypeProp.PropertyType;
                    if (enumType.IsEnum)
                    {
                        var humanoid = Enum.Parse(enumType, "Humanoid");
                        modelTypeProp.SetValue(assigner, humanoid);
                    }
                }
                Debug.Log("[Phase6] ModelAnimatorAssigner configured");
            }
        }

        // 3. Player_Rigged.glb 로드 확인
        var glb = Resources.Load<GameObject>("Models/UserProvided/Player_Rigged");
        if (glb != null)
        {
            Debug.Log("[Phase6] Player_Rigged.glb found in Resources");
            
            // 기존 모델 정리
            var existingSmrs = playerModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in existingSmrs)
            {
                if (smr.gameObject != playerModel)
                {
                    GameObject.DestroyImmediate(smr.gameObject);
                }
            }

            // GLB 인스턴스화
            var instance = PrefabUtility.InstantiatePrefab(glb, playerModel.transform) as GameObject;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // SkinnedMeshRenderer 머티리얼 확인
            var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr.sharedMaterials == null || smr.sharedMaterials.Length == 0)
                {
                    smr.sharedMaterials = CreateDefaultCharacterMaterials();
                }
            }

            // Animator의 Avatar 설정
            var instanceAnimator = instance.GetComponent<Animator>();
            if (instanceAnimator != null)
            {
                var avatar = instanceAnimator.avatar;
                if (avatar != null)
                {
                    animator.avatar = avatar;
                    Debug.Log($"[Phase6] Avatar assigned: {avatar.name}");
                }
            }
            else
            {
                Debug.Log("[Phase6] GLB has no Animator component, ModelAnimatorAssigner will handle at runtime");
            }

            // AnimatorController는 ModelAnimatorAssigner가 런타임에 설정
            Debug.Log("[Phase6] Player_Rigged.glb instantiated and configured");
        }
        else
        {
            Debug.LogWarning("[Phase6] Player_Rigged.glb NOT FOUND - using fallback capsule");
            CreateFallbackModel(playerModel);
        }

        // 4. 중복 컴포넌트 정리 (Neural auto-setup 방지)
        CleanupDuplicateComponents(player);

        // 5. 씬 저장
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Phase6] Scene saved to: {scenePath}");
        Debug.Log("=== PHASE 6: PLAYER MODEL VERIFICATION COMPLETE ===");
    }

    static Material[] CreateDefaultCharacterMaterials()
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.8f, 0.7f, 0.6f);
        mat.name = "Character_Default";
        return new Material[] { mat };
    }

    static void CreateFallbackModel(GameObject parent)
    {
        // 기존 메시 렌더러 정리
        var existingMrs = parent.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var existingMr in existingMrs)
                    {
                        if (existingMr.gameObject != parent)
                        {
                            GameObject.DestroyImmediate(existingMr.gameObject);
                        }
                    }

        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.transform.SetParent(parent.transform);
        capsule.transform.localPosition = new Vector3(0, 0.9f, 0);
        capsule.transform.localScale = new Vector3(0.5f, 1.8f, 0.5f);
        capsule.name = "FallbackModel";
        GameObject.DestroyImmediate(capsule.GetComponent<CapsuleCollider>());

        var capsuleMr = capsule.GetComponent<MeshRenderer>();
        capsuleMr.materials = CreateDefaultCharacterMaterials();
        capsuleMr.shadowCastingMode = ShadowCastingMode.On;
        capsuleMr.receiveShadows = true;
    }

    static void CleanupDuplicateComponents(GameObject player)
    {
        // 중복 Rigidbody 제거
        var rbs = player.GetComponents<Rigidbody>();
        for (int i = 1; i < rbs.Length; i++)
        {
            GameObject.DestroyImmediate(rbs[i]);
            Debug.Log("[Phase6] Removed duplicate Rigidbody");
        }

        // 중복 Animator 제거 (PlayerModel에서만 허용)
        var modelObj = player.transform.Find("PlayerModel")?.gameObject;
        if (modelObj != null)
        {
            var animators = modelObj.GetComponents<Animator>();
            for (int i = 1; i < animators.Length; i++)
            {
                GameObject.DestroyImmediate(animators[i]);
                Debug.Log("[Phase6] Removed duplicate Animator on PlayerModel");
            }
        }

        // PlayerInput null 체크
        var playerInput = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null && playerInput.actions == null)
        {
            playerInput.actions = Resources.Load<UnityEngine.InputSystem.InputActionAsset>("Input/PlayerControls");
            playerInput.defaultActionMap = "Player";
            Debug.Log("[Phase6] Fixed PlayerInput actions reference");
        }
    }
}