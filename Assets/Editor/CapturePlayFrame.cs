using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CapturePlayFrame
{
    [MenuItem("Tools/Debug/Capture Play Frame")]
    public static void Run()
    {
        // Open scene and capture a rendered frame WITHOUT Play Mode (batchmode-safe)
        // This gives the actual GPU-rendered result of the terrain material.
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");

        // Find the camera that renders the game view (Main Camera or the VCam's camera)
        Camera cam = Camera.main;
        if (cam == null)
        {
            // find any enabled camera
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cams.Length == 0) { Debug.LogError("[Capture] No camera found"); return; }
            cam = cams[0];
        }
        Debug.Log("[Capture] Using camera: " + cam.name + " pos=" + cam.transform.position + " rot=" + cam.transform.eulerAngles);

        // Position camera to look at terrain from a slight angle so ground is visible.
        // Ground is centered at origin, ~y=1. Player capsule near origin.
        cam.transform.position = new Vector3(0f, 6f, -8f);
        cam.transform.LookAt(new Vector3(0f, 0f, 5f));

        // Ensure terrain renderers are active and visible
        var ground = GameObject.Find("Ground_Inner");
        if (ground != null)
        {
            var mr = ground.GetComponent<MeshRenderer>();
            Debug.Log("[Capture] Ground_Inner material=" + (mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "NULL") + " shader=" + (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.shader != null ? mr.sharedMaterial.shader.name : "NULL"));
        }

        // Render into a RenderTexture and save as PNG
        int resX = 1280, resY = 720;
        var rt = new RenderTexture(resX, resY, 24);
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;

        var tex = new Texture2D(resX, resY, TextureFormat.RGB24, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, resX, resY), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        byte[] png = tex.EncodeToPNG();
        string outPath = "C:/Unity/code/Screenshots/_capture_render.png";
        System.IO.File.WriteAllBytes(outPath, png);
        Debug.Log("[Capture] Saved frame to " + outPath + " bytes=" + png.Length);

        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
    }
}