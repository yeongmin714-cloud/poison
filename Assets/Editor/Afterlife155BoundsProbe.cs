using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>[155차 P3] Cem_* 프리팹 실측 bounds — 스케일 튜닝 기초자료(-quit 허용).</summary>
    public static class Afterlife155BoundsProbe
    {
        public static void RunAll()
        {
            var sb = new StringBuilder("# [155차] Cem_* 프리팹 bounds 실측 (prefab 로컬, scale 1)\n");
            foreach (var kv in ProjectName.Systems.AfterlifeCemeteryCatalog.All)
            {
                var prefab = Resources.Load<GameObject>("Models/Cemetery/" + kv.Key);
                if (prefab == null) { sb.AppendLine(kv.Key + " | LOAD FAIL"); continue; }
                var go = Object.Instantiate(prefab);
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) { sb.AppendLine(kv.Key + " | NO RENDERER"); Object.DestroyImmediate(go); continue; }
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                var size = b.size; var min = b.min;
                sb.AppendLine($"{kv.Key} | size=({size.x:F2},{size.y:F2},{size.z:F2}) bottomOffsetY={min.y - go.transform.position.y:F3}");
                Object.DestroyImmediate(go);
            }
            File.WriteAllText("TestOutput/155_cemetery_bounds.txt", sb.ToString());
            Debug.Log("[155차 bounds] DONE");
        }
    }
}
