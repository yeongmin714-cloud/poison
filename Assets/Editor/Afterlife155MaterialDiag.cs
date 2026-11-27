using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [155차 P0 후속] Necropoly 선별 프리팹 재질 진단 — 셰이더/텍스처/ST 덤프(-quit 허용).
    /// 산출: TestOutput/155_cemetery_material_diag.txt
    /// </summary>
    public static class Afterlife155MaterialDiag
    {
        public static void RunAll()
        {
            var sb = new StringBuilder("# [155차] Cem_* 프리팹 재질 진단\n");
            string[] keys =
            {
                "Cem_Statue_01", "Cem_Grave_01", "Cem_Tomb_Niche", "Cem_Obelisk",
                "Cem_Monument", "Cem_Fountain", "Cem_Coffin", "Cem_Cross",
                "Cem_Candle", "Cem_Bone", "Cem_Urn", "Cem_Lantern", "Cem_Bench", "Cem_Fence",
            };
            foreach (var key in keys)
            {
                var prefab = Resources.Load<GameObject>("Models/Cemetery/" + key);
                if (prefab == null) { sb.AppendLine(key + " | LOAD FAIL"); continue; }
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                sb.AppendLine($"== {key} renderers={renderers.Length}");
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (var r in renderers)
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) { sb.AppendLine($"  {r.name} | mat=NULL"); continue; }
                        string id = m.name + "|" + (m.shader != null ? m.shader.name : "NULL_SHADER");
                        if (!seen.Add(id)) continue;
                        string tex = m.mainTexture != null ? m.mainTexture.name : "none";
                        var st = m.mainTextureScale; var off = m.mainTextureOffset;
                        sb.AppendLine($"  mat={m.name} shader={m.shader.name} tex={tex} scale=({st.x:F1},{st.y:F1}) offset=({off.x:F2},{off.y:F2})");
                    }
                }
            }
            File.WriteAllText("TestOutput/155_cemetery_material_diag.txt", sb.ToString());
            Debug.Log("[155차 진단] DONE → TestOutput/155_cemetery_material_diag.txt");
        }
    }
}
