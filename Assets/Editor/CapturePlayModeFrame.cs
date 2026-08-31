using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Renders actual Play Mode (Awake/Start run, Terrain_East_Mat applied) and captures the
// final frame that the user would see. Batchmode-safe: enters Play, waits, screenshots, exits.
public static class CapturePlayModeFrame
{
    private static int _frameCount;
    private static bool _captured;
    private static Camera _cam;

    [MenuItem("Tools/Debug/Capture Play Mode Frame")]
    public static void Run()
    {
        // Open scene then enter play mode. In batchmode this still runs Awake/Start of scene objects.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.isPlaying = true;
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _frameCount = 0;
            _captured = false;
            EditorApplication.update += CaptureLoop;
            Debug.Log("[CapturePM] Entered Play Mode. Waiting for stable frame...");
        }
    }

    static void CaptureLoop()
    {
        _frameCount++;
        // Wait enough frames for Awake/Start + one full render
        if (_frameCount < 20) return;
        if (_captured) return;
        _captured = true;

        // Find camera that renders (Main Camera driven by Cinemachine)
        _cam = Camera.main;
        if (_cam == null)
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cams.Length == 0) { Debug.LogError("[CapturePM] No camera"); Finish(); return; }
            _cam = cams[0];
        }
        Debug.Log("[CapturePM] Capturing camera: " + _cam.name + " pos=" + _cam.transform.position);

        // Check what material terrain has in actual Play Mode
        var ground = GameObject.Find("Ground_Inner");
        if (ground != null)
        {
            var mr = ground.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                var m = mr.sharedMaterial;
                Debug.Log("[CapturePM] Ground material=" + m.name + " shader=" + (m.shader != null ? m.shader.name : "NULL") + " _BaseMap=" + (m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "NULL"));
            }
        }

        int resX = 1280, resY = 720;
        var rt = new RenderTexture(resX, resY, 24);
        _cam.targetTexture = rt;
        _cam.Render();
        _cam.targetTexture = null;

        var tex = new Texture2D(resX, resY, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, resX, resY), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        byte[] png = tex.EncodeToPNG();
        string outPath = "C:/Unity/code/Screenshots/_capture_playmode.png";
        System.IO.File.WriteAllBytes(outPath, png);
        Debug.Log("[CapturePM] Saved: " + outPath + " bytes=" + png.Length);

        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);

        Finish();
    }

    static void Finish()
    {
        EditorApplication.update -= CaptureLoop;
        EditorApplication.isPlaying = false;
        // In batchmode, exiting play then quitting editor to terminate the process.
        Debug.Log("[CapturePM] Done. Exiting play mode.");
        EditorApplication.Exit(0);
    }
}