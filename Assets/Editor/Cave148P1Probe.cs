using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [148차 P1] 갈색 잔존 요소 귀속 격리 프로브 — 2층 탑다운에서 레이어 토글 캡처로 출처 단정.
    /// 산출: TestOutput/cave148_f2_{all,no_rockring,no_caps,no_assetwalls}.png + cave148_f1_all.png
    /// Edit 모드 배치(-quit 허용) — 런타임 전환 로직 불요, 순수 비주얼 귀속 목적.
    /// </summary>
    public static class Cave148P1Probe
    {
        const string Tag = "[148차 P1]";
        const int RTW = 1024, RTH = 768;
        static int _errors;

        public static void RunAll()
        {
            var room2 = ProjectName.Systems.CaveInteriorBuilder.BuildCaveInterior("default", 1, 1);
            if (room2 == null) { Debug.LogError($"{Tag} FAIL F2 빌드"); EditorApplication.Exit(1); return; }

            Capture(room2, "cave148_f2_all.png", null);
            Capture(room2, "cave148_f2_no_rockring.png", "CaveRockRing");
            Capture(room2, "cave148_f2_no_caps.png", "CaveWallCap");
            Capture(room2, "cave148_f2_no_assetwalls.png", "CaveAssetWalls");
            Object.DestroyImmediate(room2);

            var room1 = ProjectName.Systems.CaveInteriorBuilder.BuildCaveInterior("default", 1, 0);
            if (room1 != null)
            {
                Capture(room1, "cave148_f1_all.png", null);
                Object.DestroyImmediate(room1);
            }

            Debug.Log($"{Tag} DONE errors={_errors}");
            EditorApplication.Exit(_errors == 0 ? 0 : 1);
        }

        static void Capture(GameObject room, string fileName, string hidePrefix)
        {
            if (!TryRoomBounds(room, out var bounds))
            {
                Debug.LogError($"{Tag} FAIL {fileName} — bounds 산출 실패");
                _errors++;
                return;
            }
            float orthoSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.1f;
            var go = new GameObject("Cave148ProbeCam");
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

            // 레이어 토글 — 이름/조상 이름 prefix 매칭(Visual 자식 포함 귀속).
            var hidden = new System.Collections.Generic.List<Renderer>();
            if (hidePrefix != null)
            {
                foreach (var r in room.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;
                    var t = r.transform;
                    bool match = t.name.StartsWith(hidePrefix, System.StringComparison.Ordinal);
                    while (!match && t.parent != null)
                    {
                        t = t.parent;
                        if (t == room.transform) break;
                        match = t.name.StartsWith(hidePrefix, System.StringComparison.Ordinal);
                    }
                    if (match) { r.enabled = false; hidden.Add(r); }
                }
            }

            Color ambientBefore = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(1f, 1f, 1f, 1f);
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
                string path = Path.Combine("TestOutput", fileName);
                File.WriteAllBytes(path, png);
                int kb = png.Length / 1024;
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
                Debug.Log($"{Tag} CAPTURE {fileName} {kb}KB hidden={hidden.Count} ortho={orthoSize.ToString("F1", CultureInfo.InvariantCulture)}");
            }
            finally
            {
                RenderSettings.ambientLight = ambientBefore;
                foreach (var r in hidden) r.enabled = true;
                Object.DestroyImmediate(go);
            }
        }

        static bool TryRoomBounds(GameObject room, out Bounds bounds)
        {
            var renderers = room.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { bounds = default; return false; }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
