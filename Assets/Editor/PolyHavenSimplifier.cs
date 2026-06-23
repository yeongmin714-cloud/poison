using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Poly Haven 3D 모델을 단순 Primitive로 교체/복원하는 Editor 스크립트.
/// 사용법: Tools > Poly Haven > Replace With Primitives (씬 가볍게)
///         Tools > Poly Haven > Restore From Backup (원상복구)
///         
/// 백업 데이터: Assets/Scenes/PolyHaven_Transform_Backup.json
/// </summary>
public class PolyHavenSimplifier : EditorWindow
{
    [MenuItem("Tools/Poly Haven/Replace With Primitives")]
    private static void ReplaceWithPrimitives()
    {
        var backup = new PolyHavenBackup();

        // === Step 1: Find and record all Poly Haven objects ===
        FindAndBackup("Tree_", backup);
        FindAndBackup("Rock_", backup);
        FindAndBackup("periwinkle_plant_", backup);
        FindAndBackup("searsia_lucida_", backup);
        FindAndBackup("fir_tree_01_", backup);

        if (backup.entries.Count == 0)
        {
            EditorUtility.DisplayDialog("No Poly Haven objects found",
                "No objects matching Poly Haven naming patterns were found in the current scene.", "OK");
            return;
        }

        // Also find child MeshRenderer objects under trees and rocks
        FindChildMeshObjects(backup);

        // === Step 2: Save JSON backup ===
        string jsonPath = Application.dataPath + "/Scenes/PolyHaven_Transform_Backup.json";
        string json = JsonUtility.ToJson(backup, true);
        File.WriteAllText(jsonPath, json);
        Debug.Log($"[PolyHaven] Backed up {backup.entries.Count} entries to {jsonPath}");

        // === Step 3: Replace with primitives ===
        int replaced = 0;
        foreach (var entry in backup.entries)
        {
            var obj = GameObject.Find(entry.fullPath);
            if (obj == null) continue;

            // Determine primitive type based on category
            PrimitiveType primType;
            Color primColor;
            Vector3 primScale = Vector3.one;

            if (entry.category.StartsWith("Tree_") || entry.category.StartsWith("fir_tree"))
            {
                // Trees → green cylinder (trunk) + green sphere (canopy)
                primType = PrimitiveType.Cylinder;
                primColor = new Color(0.25f, 0.55f, 0.15f); // forest green
                primScale = new Vector3(1f, 1.5f, 1f);
            }
            else if (entry.category.StartsWith("Rock_"))
            {
                // Rocks → gray box with slight randomness
                primType = PrimitiveType.Cube;
                primColor = new Color(0.5f, 0.5f, 0.5f); // gray
                primScale = Vector3.one * Random.Range(0.8f, 1.5f);
            }
            else if (entry.category.StartsWith("periwinkle") || entry.category.StartsWith("searsia"))
            {
                // Plants → small green sphere
                primType = PrimitiveType.Sphere;
                primColor = new Color(0.3f, 0.6f, 0.2f); // plant green
                primScale = Vector3.one * 0.5f;
            }
            else
            {
                primType = PrimitiveType.Cube;
                primColor = Color.gray;
            }

            // Record original scale before modifying
            entry.originalScale = obj.transform.localScale;

            // Destroy all children (the glTF sub-meshes) but keep transform
            int childCount = obj.transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                var child = obj.transform.GetChild(i);
                Undo.DestroyObjectImmediate(child.gameObject);
            }

            // Destroy MeshFilter, MeshRenderer, MeshCollider
            var mf = obj.GetComponent<MeshFilter>();
            var mr = obj.GetComponent<MeshRenderer>();
            var mc = obj.GetComponent<MeshCollider>();
            if (mf != null) Undo.DestroyObjectImmediate(mf);
            if (mr != null) Undo.DestroyObjectImmediate(mr);
            if (mc != null) Undo.DestroyObjectImmediate(mc);

            // Add primitive
            var prim = GameObject.CreatePrimitive(primType);
            prim.transform.SetParent(obj.transform.parent);
            prim.transform.localPosition = obj.transform.localPosition;
            prim.transform.localRotation = obj.transform.localRotation;
            prim.transform.localScale = Vector3.Scale(obj.transform.localScale, primScale);
            prim.name = obj.name + "_PRIM";
            Undo.RegisterCreatedObjectUndo(prim, "Replace PolyHaven with Primitive");

            // Apply color material
            var renderer = prim.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = primColor;
            renderer.sharedMaterial = mat;

            // Remove collider from primitives (performance)
            var primCollider = prim.GetComponent<MeshCollider>();
            if (primCollider != null)
            {
                Object.DestroyImmediate(primCollider);
            }

            // Disable original object (keep it in scene for restore reference)
            obj.SetActive(false);
            Undo.RegisterFullObjectHierarchyUndo(obj, "Disable PolyHaven object");

            entry.primitiveName = prim.name;
            replaced++;
        }

        // === Step 4: Save updated JSON with primitive names ===
        json = JsonUtility.ToJson(backup, true);
        File.WriteAllText(jsonPath, json);

        Debug.Log($"[PolyHaven] ✅ Replaced {replaced} Poly Haven objects with primitives.");
        EditorUtility.DisplayDialog("Poly Haven Simplifier",
            $"✅ 완료!\n\n{backup.entries.Count}개 Poly Haven 오브젝트를 단순 Primitive로 교체했습니다.\n\n백업 데이터: Assets/Scenes/PolyHaven_Transform_Backup.json\n\n복원하려면: Tools > Poly Haven > Restore From Backup", "OK");

        // Refresh scene
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Poly Haven/Restore From Backup")]
    private static void RestoreFromBackup()
    {
        string jsonPath = Application.dataPath + "/Scenes/PolyHaven_Transform_Backup.json";
        if (!File.Exists(jsonPath))
        {
            EditorUtility.DisplayDialog("Backup not found",
                $"백업 파일이 없습니다: {jsonPath}\n\n먼저 'Replace With Primitives'를 실행해야 합니다.", "OK");
            return;
        }

        string json = File.ReadAllText(jsonPath);
        var backup = JsonUtility.FromJson<PolyHavenBackup>(json);

        if (backup == null || backup.entries.Count == 0)
        {
            EditorUtility.DisplayDialog("Invalid backup", "백업 데이터가 비어있습니다.", "OK");
            return;
        }

        int restored = 0;

        foreach (var entry in backup.entries)
        {
            // Find the original (disabled) object
            var originalObj = GameObject.Find(entry.fullPath);
            if (originalObj == null)
            {
                Debug.LogWarning($"[PolyHaven] 원본을 찾을 수 없음: {entry.fullPath}");
                continue;
            }

            // Find and destroy the primitive
            if (!string.IsNullOrEmpty(entry.primitiveName))
            {
                var prim = GameObject.Find(entry.primitiveName);
                if (prim != null)
                {
                    Undo.DestroyObjectImmediate(prim);
                }
            }

            // Re-enable the original object
            originalObj.SetActive(true);
            Undo.RegisterFullObjectHierarchyUndo(originalObj, "Restore PolyHaven object");
            restored++;
        }

        Debug.Log($"[PolyHaven] ✅ Restored {restored} Poly Haven objects.");
        EditorUtility.DisplayDialog("Poly Haven Restore",
            $"✅ 복원 완료!\n\n{restored}개 Poly Haven 오브젝트를 원래대로 되돌렸습니다.\n원래 MainScene_PolyHaven_Backup.unity 참조 가능.", "OK");

        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Poly Haven/Delete All Primitives (Revert to Empty)")]
    private static void DeleteAllPrimitives()
    {
        if (!EditorUtility.DisplayDialog("Delete All Primitives?",
            "모든 Primitive Placeholder를 삭제합니다.\n나중에 복원하려면 Restore from Backup을 실행하세요.\n\n계속하시겠습니까?", "삭제", "취소"))
            return;

        string jsonPath = Application.dataPath + "/Scenes/PolyHaven_Transform_Backup.json";
        if (!File.Exists(jsonPath))
        {
            EditorUtility.DisplayDialog("Backup not found", "백업 파일이 없습니다.", "OK");
            return;
        }

        string json = File.ReadAllText(jsonPath);
        var backup = JsonUtility.FromJson<PolyHavenBackup>(json);

        int deleted = 0;
        foreach (var entry in backup.entries)
        {
            if (!string.IsNullOrEmpty(entry.primitiveName))
            {
                var prim = GameObject.Find(entry.primitiveName);
                if (prim != null)
                {
                    Undo.DestroyObjectImmediate(prim);
                    deleted++;
                }
            }
        }

        Debug.Log($"[PolyHaven] Deleted {deleted} primitives (backup data preserved).");
        EditorUtility.DisplayDialog("PolyHaven Cleanup",
            $"✅ {deleted}개 Primitive 삭제 완료.\n백업 데이터는 유지됩니다. 복원하려면 Restore From Backup 실행.", "OK");
    }

    private static void FindAndBackup(string namePrefix, PolyHavenBackup backup)
    {
        var allObjects = GameObject.FindObjectsOfType<GameObject>(true);
        foreach (var obj in allObjects)
        {
            if (obj.name.StartsWith(namePrefix))
            {
                // Check if it has children or MeshFilter (actual PolyHaven instance)
                if (obj.transform.childCount > 0 || obj.GetComponent<MeshFilter>() != null)
                {
                    var entry = new PolyHavenEntry
                    {
                        fullPath = GetFullPath(obj),
                        name = obj.name,
                        category = namePrefix.TrimEnd('_'),
                        localPosition = obj.transform.localPosition,
                        localRotation = obj.transform.localRotation.eulerAngles,
                        localScale = obj.transform.localScale,
                        originalScale = obj.transform.localScale,
                        primitiveName = ""
                    };
                    backup.entries.Add(entry);
                }
            }
        }
    }

    private static void FindChildMeshObjects(PolyHavenBackup backup)
    {
        // For tree.016 children (jacaranda/tree_small sub-objects)
        var treesWithChildren = GameObject.FindObjectsOfType<Transform>(true);
        foreach (var t in treesWithChildren)
        {
            if (t.name.StartsWith("tree.016") && t.childCount > 0)
            {
                string parentPath = GetFullPath(t.parent.gameObject);
                // Check if parent is already in backup
                bool found = false;
                foreach (var entry in backup.entries)
                {
                    if (entry.fullPath == parentPath)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    var entry = new PolyHavenEntry
                    {
                        fullPath = GetFullPath(t.gameObject),
                        name = t.name,
                        category = "Tree_child",
                        localPosition = t.localPosition,
                        localRotation = t.localRotation.eulerAngles,
                        localScale = t.localScale,
                        originalScale = t.localScale,
                        primitiveName = ""
                    };
                    backup.entries.Add(entry);
                }
            }
        }
    }

    private static string GetFullPath(GameObject obj)
    {
        if (obj == null) return "";
        var path = new System.Text.StringBuilder();
        var current = obj.transform;
        while (current != null)
        {
            if (path.Length > 0)
                path.Insert(0, "/");
            path.Insert(0, current.name);
            current = current.parent;
        }
        return path.ToString();
    }
}

[System.Serializable]
public class PolyHavenBackup
{
    public List<PolyHavenEntry> entries = new List<PolyHavenEntry>();
}

[System.Serializable]
public class PolyHavenEntry
{
    public string fullPath;
    public string name;
    public string category;
    public Vector3Serializable localPosition;
    public Vector3Serializable localRotation;
    public Vector3Serializable localScale;
    public Vector3Serializable originalScale;
    public string primitiveName;
}

[System.Serializable]
public struct Vector3Serializable
{
    public float x, y, z;

    public Vector3Serializable(float x, float y, float z)
    {
        this.x = x; this.y = y; this.z = z;
    }

    public static implicit operator Vector3(Vector3Serializable v) => new Vector3(v.x, v.y, v.z);
    public static implicit operator Vector3Serializable(Vector3 v) => new Vector3Serializable(v.x, v.y, v.z);
}