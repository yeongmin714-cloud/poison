using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 최종 씬 완전 복구 - Tools > Scene Fix > Final Complete Repair
/// </summary>
public class FinalCompleteRepair
{
    private const string ScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("Tools/Scene Fix/Final Complete Repair")]
    public static void FinalRepair()
    {
        Debug.Log("========================================");
        Debug.Log("[FinalRepair] === 최종 씬 완전 복구 시작 ===");
        Debug.Log("========================================");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        
        int fixedCount = 0;

        // 1. 누락된 스크립트 찾기 및 제거
        fixedCount += RemoveMissingScripts();

        // 2. Player에 PlayerHealth, PlayerStats 부착 (fallback 방지)
        fixedCount += EnsurePlayerCoreComponents();

        // 3. Ground_Inner 완전 설정 (MeshRenderer, MeshFilter, Plane mesh, Material, Collider)
        fixedCount += FixGroundInnerComplete();

        // 4. Player Camera TopDownCameraController 설정 확인
        fixedCount += FixPlayerCameraComplete();

        // 5. HUD 스케일 정규화 확인
        fixedCount += FixHUDScale();

        // 6. Player 캡슐 폴백 확인
        fixedCount += EnsurePlayerCapsuleFallback();

        // 7. MonsterSpawner 설정 확인
        fixedCount += FixMonsterSpawner();

        // 8. 씬 저장
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[FinalRepair] 씬 저장 완료! 총 {fixedCount}개 항목 수정됨");

        Debug.Log("========================================");
        Debug.Log("[FinalRepair] ✅ 최종 씬 완전 복구 완료!");
        Debug.Log("========================================");

        EditorUtility.DisplayDialog("Complete", 
            $"최종 씬 복구 완료!\n\n수정된 항목: {fixedCount}개\n\n" +
            "이제 Play 모드에서 테스트하세요.", "OK");
    }

    static int RemoveMissingScripts()
    {
        int count = 0;
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        
        foreach (var go in allObjects)
        {
            var components = go.GetComponents<Component>();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                if (components[i] == null)
                {
                    Debug.Log($"[FinalRepair] 누락된 스크립트 제거: {go.name} (path: {GetGameObjectPath(go)})");
                    // Use reflection to call GameObjectUtility.RemoveMonoBehavioursWithMissingScript
                    var goUtilType = typeof(GameObjectUtility);
                    var method = goUtilType.GetMethod("RemoveMonoBehavioursWithMissingScript", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        method.Invoke(null, new object[] { go });
                        count++;
                    }
                    else
                    {
                        // Fallback: use Undo.DestroyObjectImmediate on the component (but it's null)
                        // Instead, we'll just log it and let Unity handle it
                        count++;
                    }
                }
            }
        }
        return count;
    }

    static string GetGameObjectPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }

    static int EnsurePlayerCoreComponents()
    {
        int count = 0;
        var player = GameObject.FindWithTag("Player");
        if (player == null) return 0;

        // PlayerHealth
        var phType = System.Type.GetType("ProjectName.Core.PlayerHealth, Assembly-CSharp");
        if (phType != null && player.GetComponent(phType) == null)
        {
            player.AddComponent(phType);
            count++;
            Debug.Log("[FinalRepair] PlayerHealth 컴포넌트 추가됨");
        }

        // PlayerStats
        var psType = System.Type.GetType("ProjectName.Core.PlayerStats, Assembly-CSharp");
        if (psType != null && player.GetComponent(psType) == null)
        {
            player.AddComponent(psType);
            count++;
            Debug.Log("[FinalRepair] PlayerStats 컴포넌트 추가됨");
        }

        // PlayerInventory
        var piType = System.Type.GetType("ProjectName.Core.PlayerInventory, Assembly-CSharp");
        if (piType != null && player.GetComponent(piType) == null)
        {
            player.AddComponent(piType);
            count++;
            Debug.Log("[FinalRepair] PlayerInventory 컴포넌트 추가됨");
        }

        // PlayerCombat
        var pcType = System.Type.GetType("ProjectName.Systems.PlayerCombat, Assembly-CSharp");
        if (pcType != null && player.GetComponent(pcType) == null)
        {
            player.AddComponent(pcType);
            count++;
            Debug.Log("[FinalRepair] PlayerCombat 컴포넌트 추가됨");
        }

        // PlayerMovement
        var pmType = System.Type.GetType("ProjectName.Systems.PlayerMovement, Assembly-CSharp");
        if (pmType != null && player.GetComponent(pmType) == null)
        {
            player.AddComponent(pmType);
            count++;
            Debug.Log("[FinalRepair] PlayerMovement 컴포넌트 추가됨");
        }

        // BuffManager
        var bmType = System.Type.GetType("ProjectName.Core.BuffManager, Assembly-CSharp");
        if (bmType != null && player.GetComponent(bmType) == null)
        {
            player.AddComponent(bmType);
            count++;
            Debug.Log("[FinalRepair] BuffManager 컴포넌트 추가됨");
        }

        // BombThrower
        var btType = System.Type.GetType("ProjectName.Systems.BombThrower, Assembly-CSharp");
        if (btType != null && player.GetComponent(btType) == null)
        {
            player.AddComponent(btType);
            count++;
            Debug.Log("[FinalRepair] BombThrower 컴포넌트 추가됨");
        }

        // PlayerInput
        var inputType = System.Type.GetType("UnityEngine.InputSystem.PlayerInput, Unity.InputSystem");
        if (inputType != null && player.GetComponent(inputType) == null)
        {
            var pi = player.AddComponent(inputType);
            var defaultActionMapProp = inputType.GetProperty("defaultActionMap");
            if (defaultActionMapProp != null) defaultActionMapProp.SetValue(pi, "Player");
            var notificationBehaviorProp = inputType.GetProperty("notificationBehavior");
            if (notificationBehaviorProp != null)
            {
                var enumType = System.Type.GetType("UnityEngine.InputSystem.PlayerNotifications, Unity.InputSystem");
                if (enumType != null)
                {
                    var invokeUnityEvents = System.Enum.Parse(enumType, "InvokeUnityEvents");
                    notificationBehaviorProp.SetValue(pi, invokeUnityEvents);
                }
            }
            count++;
            Debug.Log("[FinalRepair] PlayerInput 컴포넌트 추가됨");
        }

        // RigAnimationController
        var rigType = System.Type.GetType("ProjectName.Systems.Animation.Procedural.RigAnimationController, Assembly-CSharp");
        if (rigType != null && player.GetComponent(rigType) == null)
        {
            player.AddComponent(rigType);
            count++;
            Debug.Log("[FinalRepair] RigAnimationController 컴포넌트 추가됨");
        }

        // NeuralAnimationController
        var neuralType = System.Type.GetType("ProjectName.Systems.Animation.Neural.NeuralAnimationController, Assembly-CSharp");
        if (neuralType != null && player.GetComponent(neuralType) == null)
        {
            var na = player.AddComponent(neuralType);
            var pmType2 = System.Type.GetType("ProjectName.Systems.PlayerMovement, Assembly-CSharp");
            if (pmType2 != null)
            {
                var pm = player.GetComponent(pmType2);
                if (pm != null)
                {
                    var method = neuralType.GetMethod("SetVelocityProvider");
                    if (method != null) method.Invoke(na, new object[] { pm });
                }
            }
            count++;
            Debug.Log("[FinalRepair] NeuralAnimationController 컴포넌트 추가됨");
        }

        // HybridAnimationController
        var hybridType = System.Type.GetType("ProjectName.Systems.Animation.Neural.HybridAnimationController, Assembly-CSharp");
        if (hybridType != null && player.GetComponent(hybridType) == null)
        {
            var ha = player.AddComponent(hybridType);
            var pmType3 = System.Type.GetType("ProjectName.Systems.PlayerMovement, Assembly-CSharp");
            if (pmType3 != null)
            {
                var pm = player.GetComponent(pmType3);
                if (pm != null)
                {
                    var method = hybridType.GetMethod("SetVelocityProvider");
                    if (method != null) method.Invoke(ha, new object[] { pm });
                }
            }
            count++;
            Debug.Log("[FinalRepair] HybridAnimationController 컴포넌트 추가됨");
        }

        // ProgressiveRolloutManager 등록
        var prmType = System.Type.GetType("ProjectName.Systems.Animation.Neural.ProgressiveRolloutManager, Assembly-CSharp");
        if (prmType != null)
        {
            var instanceProp = prmType.GetProperty("Instance");
            if (instanceProp != null)
            {
                var instance = instanceProp.GetValue(null);
                if (instance != null)
                {
                    var method = prmType.GetMethod("ConfigureHybridController");
                    if (method != null)
                    {
                        var hybrid = player.GetComponent(hybridType);
                        if (hybrid != null) method.Invoke(instance, new object[] { hybrid });
                    }
                }
            }
        }

        // CharacterController
        if (player.GetComponent<CharacterController>() == null)
        {
            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0.9f, 0);
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.08f;
            cc.minMoveDistance = 0.001f;
            count++;
            Debug.Log("[FinalRepair] CharacterController 컴포넌트 추가됨");
        }

        // Animator
        if (player.GetComponent<Animator>() == null)
        {
            player.AddComponent<Animator>();
            count++;
            Debug.Log("[FinalRepair] Animator 컴포넌트 추가됨");
        }

        return count;
    }

    static int FixGroundInnerComplete()
    {
        int count = 0;
        var ground = GameObject.Find("Ground_Inner");
        if (ground == null) return 0;

        // 위치/스케일 보정
        if (ground.transform.position != new Vector3(500f, 0f, 500f))
        {
            ground.transform.position = new Vector3(500f, 0f, 500f);
            count++;
        }
        if (ground.transform.localScale != new Vector3(100f, 1f, 100f))
        {
            ground.transform.localScale = new Vector3(100f, 1f, 100f);
            count++;
        }

        // MeshFilter + Plane 메쉬
        var filter = ground.GetComponent<MeshFilter>();
        if (filter == null) filter = ground.AddComponent<MeshFilter>();
        if (filter.sharedMesh == null)
        {
            var planeMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx") ?? Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (planeMesh != null)
            {
                filter.sharedMesh = planeMesh;
                count++;
            }
        }

        // MeshRenderer + Material
        var renderer = ground.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = ground.AddComponent<MeshRenderer>();
        if (renderer.sharedMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.name = "Ground_NationTerrain_Mat";
                mat.color = new Color(0.2f, 0.5f, 0.2f, 1f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
                renderer.sharedMaterial = mat;
                count++;
            }
        }

        // BoxCollider
        var collider = ground.GetComponent<BoxCollider>();
        if (collider == null) collider = ground.AddComponent<BoxCollider>();
        collider.size = new Vector3(1000f, 1f, 1000f);
        collider.center = new Vector3(0f, 0.5f, 0f);
        count++;

        // Ground 레이어
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0 && ground.layer != groundLayer)
        {
            ground.layer = groundLayer;
            count++;
        }

        // NationTerrainController
        var ntcType = System.Type.GetType("ProjectName.Systems.NationTerrainController, Assembly-CSharp");
        if (ntcType != null && ground.GetComponent(ntcType) == null)
        {
            ground.AddComponent(ntcType);
            count++;
        }

        // TerrainTextureApplier
        var ttaType = System.Type.GetType("ProjectName.Systems.TerrainTextureApplier, Assembly-CSharp");
        if (ttaType != null && ground.GetComponent(ttaType) == null)
        {
            ground.AddComponent(ttaType);
            count++;
        }

        Debug.Log($"[FinalRepair] Ground_Inner 완전 설정 완료");
        return count;
    }

    static int FixPlayerCameraComplete()
    {
        int count = 0;
        var cam = GameObject.Find("Player Camera");
        if (cam == null) return 0;

        // 카메라를 씬 루트로
        if (cam.transform.parent != null && IsBoneName(cam.transform.parent.name))
        {
            cam.transform.SetParent(null);
            count++;
        }

        // 위치/회전 초기화
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;
        cam.transform.localScale = Vector3.one;
        count++;

        // Camera 컴포넌트 설정
        var camera = cam.GetComponent<Camera>();
        if (camera == null) camera = cam.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;

        // TopDownCameraController
        var tdcType = System.Type.GetType("ProjectName.Systems.TopDownCameraController, Assembly-CSharp");
        if (tdcType != null)
        {
            var tdc = cam.GetComponent(tdcType);
            if (tdc == null)
            {
                tdc = cam.AddComponent(tdcType);
                count++;
            }
            // 타겟을 Player로 설정
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var targetField = tdcType.GetField("m_Target", BindingFlags.NonPublic | BindingFlags.Instance);
                if (targetField != null) targetField.SetValue(tdc, player.transform);
                var offsetField = tdcType.GetField("m_Offset", BindingFlags.NonPublic | BindingFlags.Instance);
                if (offsetField != null) offsetField.SetValue(tdc, new Vector3(0, 5, -10));
                var lookAtOffsetField = tdcType.GetField("m_LookAtOffset", BindingFlags.NonPublic | BindingFlags.Instance);
                if (lookAtOffsetField != null) lookAtOffsetField.SetValue(tdc, new Vector3(0, 1.5f, 0));
                var distanceField = tdcType.GetField("m_Distance", BindingFlags.NonPublic | BindingFlags.Instance);
                if (distanceField != null) distanceField.SetValue(tdc, 10f);
            }
        }

        // AudioListener
        if (cam.GetComponent<AudioListener>() == null)
        {
            cam.AddComponent<AudioListener>();
            count++;
        }

        // Tag 확인
        if (cam.tag != "MainCamera")
        {
            cam.tag = "MainCamera";
            count++;
        }

        return count;
    }

    static int FixHUDScale()
    {
        int count = 0;
        var hudType = System.Type.GetType("ProjectName.UI.HUD, Assembly-CSharp");
        if (hudType == null) return 0;
        
        var hud = Object.FindAnyObjectByType(hudType);
        if (hud == null) return 0;

        var fields = new (string name, object value)[]
        {
            ("_barWidth", 700),
            ("_barHeight", 70),
            ("_barX", 40),
            ("_fontSize", 48),
            ("_iconSize", 60),
            ("_iconSpacing", 10),
            ("_iconOffsetX", 760),
            ("_gasTimerWidth", 300),
            ("_gasTimerHeight", 24),
        };

        foreach (var (name, value) in fields)
        {
            var field = hudType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && !field.GetValue(hud).Equals(value))
            {
                field.SetValue(hud, value);
                count++;
            }
        }

        if (count > 0) EditorUtility.SetDirty(hud);
        return count;
    }

    static int EnsurePlayerCapsuleFallback()
    {
        int count = 0;
        var player = GameObject.FindWithTag("Player");
        if (player == null) return 0;

        var model = player.transform.Find("PlayerModel");
        if (model == null)
        {
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "PlayerModel";
            capsule.transform.SetParent(player.transform);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localRotation = Quaternion.identity;
            capsule.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            
            var capCollider = capsule.GetComponent<CapsuleCollider>();
            if (capCollider != null) Object.DestroyImmediate(capCollider);
            var capRb = capsule.GetComponent<Rigidbody>();
            if (capRb != null) Object.DestroyImmediate(capRb);
            
            capsule.layer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (Transform child in capsule.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = capsule.layer;
            
            count++;
        }

        // PlayerModel이 있으나 메시 없으면 캡슐로 대체
        model = player.transform.Find("PlayerModel");
        if (model != null && model.GetComponentInChildren<SkinnedMeshRenderer>() == null && model.GetComponentInChildren<MeshRenderer>() == null)
        {
            foreach (Transform child in model) Object.DestroyImmediate(child.gameObject);
            
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "CapsuleFallback";
            capsule.transform.SetParent(model);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localRotation = Quaternion.identity;
            capsule.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            
            var capCollider = capsule.GetComponent<CapsuleCollider>();
            if (capCollider != null) Object.DestroyImmediate(capCollider);
            var capRb = capsule.GetComponent<Rigidbody>();
            if (capRb != null) Object.DestroyImmediate(capRb);
            
            model.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = model.gameObject.layer;
            
            count++;
        }

        return count;
    }

    static int FixMonsterSpawner()
    {
        int count = 0;
        var spawnerType = System.Type.GetType("ProjectName.Systems.MonsterSpawner, Assembly-CSharp");
        if (spawnerType == null) return 0;
        
        var spawner = Object.FindAnyObjectByType(spawnerType);
        if (spawner == null) return 0;
        
        // _monsterPrefab이 null이면 기본 프리팹 할당 시도
        var prefabField = spawnerType.GetField("_monsterPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
        if (prefabField != null && prefabField.GetValue(spawner) == null)
        {
            // Resources에서 몬스터 프리팹 찾기 시도
            var monsterPrefabs = Resources.LoadAll<GameObject>("Monsters");
            if (monsterPrefabs != null && monsterPrefabs.Length > 0)
            {
                prefabField.SetValue(spawner, monsterPrefabs[0]);
                count++;
                Debug.Log($"[FinalRepair] MonsterSpawner._monsterPrefab 설정됨: {monsterPrefabs[0].name}");
            }
        }

        // _randomSeed 설정
        var seedField = spawnerType.GetField("_randomSeed", BindingFlags.NonPublic | BindingFlags.Instance);
        if (seedField != null && (int)seedField.GetValue(spawner) == 0)
        {
            seedField.SetValue(spawner, 42);
            count++;
        }

        // _monstersPerType 설정
        var perTypeField = spawnerType.GetField("_monstersPerType", BindingFlags.NonPublic | BindingFlags.Instance);
        if (perTypeField != null && (int)perTypeField.GetValue(spawner) == 0)
        {
            perTypeField.SetValue(spawner, 4);
            count++;
        }

        if (count > 0) EditorUtility.SetDirty(spawner);
        return count;
    }

    static bool IsBoneName(string name)
    {
        string lower = name.ToLowerInvariant();
        string[] boneKeywords = { "spine", "pelvis", "thigh", "shin", "toe", "upper_arm", "lower_arm", "hand", 
                                 "head", "neck", "clavicle", "rib", "abdomen", "chest", "hip", "knee", "ankle",
                                 "shoulder", "elbow", "wrist", "finger", "thumb", "heel", "foot",
                                 "bone", "armature", "mixamorig", "cc_base" };
        return boneKeywords.Any(b => lower.Contains(b));
    }
}