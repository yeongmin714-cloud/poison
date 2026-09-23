// GNB P-B: 새 GLB 프리팹을 카테고리 기준(원본 Idyllic) 높이에 맞춰 루트 로컬스케일 보정.
// 실행: Unity -quit -batchmode -projectPath <win> -executeMethod GnbRescale.ApplyAll -logFile <win>
using UnityEngine;
using UnityEditor;

public class GnbRescale
{
    static string[] Cats = new string[] { "Trees", "Rocks", "Bushes", "Grass", "Flowers", "Shore", "Water", "Meadows" };

    static float Target(string cat)
    {
        if (cat == "Trees") return 8.6f;
        if (cat == "Rocks") return 3.7f;
        if (cat == "Bushes") return 1.9f;
        if (cat == "Grass") return 0.70f;
        if (cat == "Flowers") return 0.57f;
        if (cat == "Shore") return 2.03f;
        if (cat == "Water") return 1.33f;
        return 0.29f; // Meadows
    }

    static float MeasureHeight(GameObject go)
    {
        if (go == null) return -1f;
        Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs == null || rs.Length == 0) return -1f;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) if (rs[i] != null) b.Encapsulate(rs[i].bounds);
        Vector3 e = b.extents;
        return e.y * 2f;
    }

    public static void ApplyAll()
    {
        Debug.Log("[GNB-Rescale] apply start");
        int scaled = 0, failed = 0;
        foreach (string folder in Cats)
        {
            string dir = "Assets/Resources/IdyllicPrefabs/" + folder;
            float target = Target(folder);
            string[] guids = AssetDatabase.FindAssets("", new string[] { dir });
            if (guids == null) continue;
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (p == null || !p.EndsWith(".prefab")) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null) continue;
                var inst = Object.Instantiate(prefab);
                float h = MeasureHeight(inst);
                if (h <= 0f) { Object.DestroyImmediate(inst); failed++; continue; }
                float factor = target / h;
                Vector3 s = inst.transform.localScale;
                float k = factor != 0f ? factor : 1f;
                inst.transform.localScale = new Vector3(s.x * k, s.y * k, s.z * k);
                var saved = PrefabUtility.SaveAsPrefabAsset(inst, p);
                Object.DestroyImmediate(inst);
                if (saved != null) scaled++;
                else failed++;
            }
        }
        AssetDatabase.Refresh();
        Debug.Log("[GNB-Rescale] done scaled=" + scaled + " failed=" + failed);
        EditorApplication.Exit(0);
    }
}