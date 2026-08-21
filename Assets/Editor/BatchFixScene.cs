using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Reflection;
using System.Collections.Generic;
/// <summary>
/// 배치모드 호환 씬 수정 - Tools > Scene Fix > Batch Fix Scene
/// </summary>
public class BatchFixScene
{
    private const string ScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("Tools/Scene Fix/Batch Fix Scene")]
    public static void BatchFix()
    {
        Debug.Log("========================================");
        Debug.Log("[BatchFixScene] === 배치모드 씬 수정 시작 ===");
        Debug.Log("========================================");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        
        int fixedCount = 0;

        // 1. Player 위치 수정 (1450, 1.1, 0)
        fixedCount += FixPlayerPosition();

        // 2. Player Camera에 TopDownCameraController 추가
        fixedCount += FixPlayerCamera();

        // 3. Ground_Inner 생성/메시/콜라이더/컴포넌트
        fixedCount += FixGroundInner();

        // 4. HUD 스케일 정규화 (4x → 1x)
        fixedCount += FixHUDScale();

        // 5. Player 캡슐 폴백 생성 (GLB 모델 없을 때)
        fixedCount += EnsurePlayerCapsuleFallback();

        // 6. 중복 Camera 정리
        fixedCount += RemoveDuplicateCameras();

        // 7. 씬 저장
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"[BatchFixScene] 씬 저장 완료! 총 {fixedCount}개 항목 수정됨");

        Debug.Log("========================================");
        Debug.Log("[BatchFixScene] ✅ 배치모드 씬 수정 완료!");
        Debug.Log("========================================");
    }

    static int FixPlayerPosition()
    {
        int count = 0;
        var player = GameObject.FindWithTag("Player");
        if (player == null) return 0;

        // Player가 뼈대의 자식이면 루트로 이동
        if (player.transform.parent != null && IsBoneName(player.transform.parent.name))
        {
            Debug.Log($"[BatchFix] Player가 뼈대({player.transform.parent.name})의 자식임 - 루트로 이동");
            var pos = player.transform.position;
            var rot = player.transform.rotation;
            player.transform.SetParent(null);
            player.transform.position = pos;
            player.transform.rotation = rot;
            count++;
        }

        // 올바른 스폰 위치로 이동
        var targetPos = new Vector3(1450f, 1.1f, 0f);
        if (Vector3.Distance(player.transform.position, targetPos) > 0.1f)
        {
            player.transform.position = targetPos;
            Debug.Log($"[BatchFix] Player 위치 수정: {targetPos}");
            count++;
        }

        return count;
    }

    static int FixPlayerCamera()
    {
        int count = 0;
        var cam = GameObject.Find("Player Camera");
        if (cam == null) return 0;

        // 카메라를 씬 루트로
        if (cam.transform.parent != null && IsBoneName(cam.transform.parent.name))
        {
            Debug.Log($"[BatchFix] Player Camera가 뼈대({cam.transform.parent.name})의 자식임 - 루트로 이동");
            cam.transform.SetParent(null);
            count++;
        }

        // 위치/회전 초기화
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;
        cam.transform.localScale = Vector3.one;
        count++;

        // TopDownCameraController 추가
        var tdcType = System.Type.GetType("ProjectName.Systems.TopDownCameraController, Assembly-CSharp");
        if (tdcType != null && cam.GetComponent(tdcType) == null)
        {
            cam.AddComponent(tdcType);
            count++;
            Debug.Log("[BatchFix] TopDownCameraController 추가됨");
        }

        return count;
    }

    static int FixGroundInner()
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

        // MeshFilter
        var filter = ground.GetComponent<MeshFilter>();
        if (filter == null) filter = ground.AddComponent<MeshFilter>();

        // Plane 메쉬 할당
        if (filter.sharedMesh == null)
        {
            var planeMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx") ?? Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            if (planeMesh != null)
            {
                filter.sharedMesh = planeMesh;
                Debug.Log("[BatchFix] Plane.fbx 메쉬 할당됨");
                count++;
            }
        }

        // MeshRenderer
        var renderer = ground.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = ground.AddComponent<MeshRenderer>();

        // 머티리얼
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

        Debug.Log($"[BatchFix] Ground_Inner 수정 완료");
        return count;
    }

    static int FixHUDScale()
        {
            int count = 0;
            var hudType = HUDType();
            if (hudType == null) return 0;
        
            var hud = Object.FindAnyObjectByType(hudType);
            if (hud == null) return 0;
        
            // 리플렉션으로 private 필드 강제 설정
            var fields = new (string name, object value)[]
        {
            ("_barWidth", 700),
            ("_barHeight", 70),
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
                Debug.Log($"[BatchFix] HUD.{name} = {value}");
                count++;
            }
        }

        if (count > 0) EditorUtility.SetDirty(hud);
        Debug.Log($"[BatchFix] HUD 스케일 정규화 완료 (4x → 1x)");
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
            Debug.Log("[BatchFix] PlayerModel 없음 - 캡슐 폴백 생성");
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
        var model2 = player.transform.Find("PlayerModel");
        if (model2 != null && model2.GetComponentInChildren<SkinnedMeshRenderer>() == null && model2.GetComponentInChildren<MeshRenderer>() == null)
        {
            Debug.Log("[BatchFix] PlayerModel에 메시 없음 - 캡슐로 대체");
            foreach (Transform child in model2) Object.DestroyImmediate(child.gameObject);
            
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "CapsuleFallback";
            capsule.transform.SetParent(model2);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localRotation = Quaternion.identity;
            capsule.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            
            var capCollider = capsule.GetComponent<CapsuleCollider>();
            if (capCollider != null) Object.DestroyImmediate(capCollider);
            var capRb = capsule.GetComponent<Rigidbody>();
            if (capRb != null) Object.DestroyImmediate(capRb);
            
            model2.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            foreach (Transform child in model2.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = model2.gameObject.layer;
            
            count++;
        }

        return count;
    }

    static int RemoveDuplicateCameras()
    {
        int count = 0;
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        
        Camera mainCam = null;
        foreach (var cam in cameras)
        {
            if (cam.CompareTag("MainCamera"))
            {
                if (mainCam == null) mainCam = cam;
                else { Object.DestroyImmediate(cam.gameObject); count++; }
            }
        }
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

    static System.Type HUDType()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.FullName.StartsWith("Assembly-CSharp") || asm.FullName.StartsWith("ProjectName"))
            {
                var t = asm.GetType("ProjectName.UI.HUD");
                if (t != null) return t;
            }
        }
        return null;
    }
}