using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [148차 P4] 지하 5층 전수 프로브 — 층별 빌드+탑다운 캡처+포탈/스폰/횟불 실측. Edit 모드 배치(-quit 허용).
    /// 산출: TestOutput/cave148_f{1..5}_topdown.png + 로그 STATE/CHECK/DONE 지표.
    /// </summary>
    public static class Cave148P4Probe
    {
        const string Tag = "[148차 P4]";
        const int RTW = 1024, RTH = 768;
        static int _errors;

        public static void RunAll()
        {
            int[] expectedPortals = { 2, 2, 2, 2, 1 };   // F0:하강+상승 / F1~F3:상승+하강 / F4:상승만(최심부)
            int[] expectedTorches = { 7, 3, 4, 5, 3 };   // F0 중앙화로 포함 / F1~F4 층별 구성

            for (int fi = 0; fi < 5; fi++)
            {
                var room = ProjectName.Systems.CaveInteriorBuilder.BuildCaveInterior("default", 1, fi);
                if (room == null)
                {
                    Debug.LogError($"{Tag} FAIL F{fi + 1} 빌드");
                    _errors++;
                    continue;
                }

                var portals = room.GetComponentsInChildren<ProjectName.Systems.CaveLevelPortal>(true);
                Check($"F{fi + 1} 포탈 수", portals.Length == expectedPortals[fi],
                    $"{portals.Length}/{expectedPortals[fi]}");

                var flames = room.GetComponentsInChildren<Renderer>(true);
                int torchCount = 0;
                foreach (var r in flames)
                    if (r.name == "Visual" && r.transform.parent != null &&
                        r.transform.parent.name.StartsWith("CaveTorch_", System.StringComparison.Ordinal))
                        torchCount++;
                Check($"F{fi + 1} 횟불 수", torchCount == expectedTorches[fi],
                    $"{torchCount}/{expectedTorches[fi]}");

                var spawn = ProjectName.Systems.CaveInteriorBuilder.GetFloorSpawnPosition(fi + 1);
                bool inBounds = SpawnInsideRenderers(room, spawn);
                Check($"F{fi + 1} 스폰 실내", inBounds, SpawnStr(spawn));

                CaptureTopDown(room, $"cave148_f{fi + 1}_topdown.png");
                Object.DestroyImmediate(room);
            }

            Debug.Log($"{Tag} DONE errors={_errors}");
            EditorApplication.Exit(_errors == 0 ? 0 : 1);
        }

        static void Check(string label, bool ok, string detail)
        {
            Debug.Log($"{Tag} CHECK {label}: {(ok ? "OK" : "FAIL")} ({detail})");
            if (!ok) _errors++;
        }

        static string SpawnStr(Vector3 v) =>
            $"({v.x.ToString("F1", CultureInfo.InvariantCulture)},{v.y.ToString("F1", CultureInfo.InvariantCulture)},{v.z.ToString("F1", CultureInfo.InvariantCulture)})";

        static bool SpawnInsideRenderers(GameObject room, Vector3 spawn)
        {
            var floor = room.transform.Find("CaveFloor");
            var col = floor != null ? floor.GetComponent<Collider>() : null;
            if (col == null) return false;
            var p = new Vector3(spawn.x, 1.0f, spawn.z);
            return col.bounds.Contains(p);
        }

        static void CaptureTopDown(GameObject room, string fileName)
        {
            var renderers = room.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { _errors++; return; }
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float orthoSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.1f;
            var go = new GameObject("Cave148P4Cam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            cam.cullingMask = ~0;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.transform.position = new Vector3(bounds.center.x, bounds.max.y + 15f, bounds.center.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographicSize = orthoSize;

            Color ambientBefore = RenderSettings.ambientLight;
            RenderSettings.ambientLight = Color.white;
            try
            {
                var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                var png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine("TestOutput", fileName), png);
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
                Debug.Log($"{Tag} CAPTURE {fileName} ortho={orthoSize.ToString("F1", CultureInfo.InvariantCulture)}");
            }
            finally
            {
                RenderSettings.ambientLight = ambientBefore;
                Object.DestroyImmediate(go);
            }
        }
    }
}
