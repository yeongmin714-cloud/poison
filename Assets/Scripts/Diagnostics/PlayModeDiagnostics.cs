using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.Core.Data;

/// <summary>
/// Play 모드 진입 시 실제 씬 상태를 진단하는 스크립트
/// GameObject에 붙여서 Play 모드에서 실행
/// </summary>
public class PlayModeDiagnostics : MonoBehaviour
{
    [Header("진단 설정")]
    [SerializeField] private bool _runOnStart = true;
    [SerializeField] private float _delayBeforeCheck = 2f;

    private IEnumerator Start()
    {
        if (!_runOnStart) yield break;

        yield return new WaitForSeconds(_delayBeforeCheck);

        RunDiagnostics();

        Debug.Log("========== PLAY MODE DIAGNOSTICS END ==========");
    }

    /// <summary>
    /// 배치모드에서 실행 가능한 정적 진단 메서드
    /// </summary>
    public static void RunDiagnostics()
    {
        // 씬 로드
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");

        Debug.Log("========== PLAY MODE DIAGNOSTICS START ==========");

        CheckCameraStatic();
        CheckPlayerStatic();
        CheckGroundStatic();
        CheckLightsStatic();
        CheckSkyboxStatic();
        CheckSystemsStatic();
        CheckRenderSettingsStatic();
        CheckLayersAndTagsStatic();

        Debug.Log("========== PLAY MODE DIAGNOSTICS END ==========");
    }

    private static void CheckCameraStatic()
    {
        Debug.Log("--- CAMERA CHECK ---");
        var cams = Camera.allCameras;
        Debug.Log("Total cameras: " + cams.Length);

        foreach (var cam in cams)
        {
            Debug.Log("  Camera: " + cam.name + ", enabled=" + cam.enabled + ", tag=" + cam.tag);
            Debug.Log("    Position: " + cam.transform.position + ", Rotation: " + cam.transform.eulerAngles);
            Debug.Log("    ClearFlags: " + cam.clearFlags + ", BackgroundColor: " + cam.backgroundColor);
            Debug.Log("    CullingMask: " + cam.cullingMask + " (layers: " + LayerMask.LayerToName(cam.cullingMask) + ")");
            Debug.Log("    Near/Far: " + cam.nearClipPlane + "/" + cam.farClipPlane + ", FOV: " + cam.fieldOfView);

            var topDown = cam.GetComponent<TopDownCameraController>();
            if (topDown != null)
            {
                Debug.Log("    TopDownCameraController: FOUND");
                var field = topDown.GetType().GetField("_playerTransform", BindingFlags.NonPublic | BindingFlags.Instance);
                Debug.Log("      _playerTransform: " + field?.GetValue(topDown));
            }
        }

        if (Camera.main != null)
        {
            Debug.Log("Main Camera: " + Camera.main.name);
        }
        else
        {
            Debug.LogError("MAIN CAMERA IS NULL!");
        }
    }

    private static void CheckPlayerStatic()
    {
        Debug.Log("--- PLAYER CHECK ---");
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("PLAYER NOT FOUND WITH TAG 'Player'!");
            return;
        }

        Debug.Log("Player found: " + player.name + ", active=" + player.activeInHierarchy);
        Debug.Log("  Position: " + player.transform.position + ", Rotation: " + player.transform.eulerAngles);
        Debug.Log("  Scale: " + player.transform.localScale);
        Debug.Log("  Layer: " + player.layer + " (" + LayerMask.LayerToName(player.layer) + ")");

        var mf = player.GetComponent<MeshFilter>();
        var mr = player.GetComponent<MeshRenderer>();
        var cc = player.GetComponent<CharacterController>();
        var pm = player.GetComponent<PlayerMovement>();
        var pc = player.GetComponent<PlayerCombat>();
        var ph = player.GetComponent<PlayerHealth>();
        var anim = player.GetComponent<Animator>();

        Debug.Log("  MeshFilter: " + (mf != null ? "YES" : "NO") + " mesh=" + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "NULL"));
        Debug.Log("  MeshRenderer: " + (mr != null ? "YES" : "NO") + " enabled=" + (mr != null && mr.enabled) + " mat=" + (mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "NULL"));
        Debug.Log("  CharacterController: " + (cc != null ? "YES" : "NO") + " height=" + (cc != null ? cc.height : 0) + " radius=" + (cc != null ? cc.radius : 0));
        Debug.Log("  PlayerMovement: " + (pm != null ? "YES" : "NO") + " enabled=" + (pm != null && pm.enabled));
        Debug.Log("  PlayerCombat: " + (pc != null ? "YES" : "NO") + " enabled=" + (pc != null && pc.enabled));
        Debug.Log("  PlayerHealth: " + (ph != null ? "YES" : "NO") + " HP=" + ph?.CurrentHP + "/Max=" + ph?.MaxHP);
        Debug.Log("  Animator: " + (anim != null ? "YES" : "NO") + " enabled=" + (anim != null && anim.enabled) + " avatar=" + (anim != null && anim.avatar != null ? "YES" : "NO"));

        Debug.Log("  Children count: " + player.transform.childCount);
        for (int i = 0; i < player.transform.childCount; i++)
        {
            var child = player.transform.GetChild(i);
            var cmr = child.GetComponent<MeshRenderer>();
            var cmf = child.GetComponent<MeshFilter>();
            Debug.Log("    Child " + i + ": " + child.name + ", pos=" + child.localPosition + ", MeshRenderer=" + (cmr != null) + ", MeshFilter=" + (cmf != null));
        }
    }

    private static void CheckGroundStatic()
    {
        Debug.Log("--- GROUND CHECK ---");
        var ground = GameObject.Find("Ground_Inner");
        if (ground == null)
        {
            Debug.LogError("Ground_Inner NOT FOUND!");
            return;
        }

        Debug.Log("Ground found: " + ground.name + ", active=" + ground.activeInHierarchy);
        Debug.Log("  Position: " + ground.transform.position + ", Scale: " + ground.transform.localScale);

        var mf = ground.GetComponent<MeshFilter>();
        var mr = ground.GetComponent<MeshRenderer>();
        var col = ground.GetComponent<Collider>();

        Debug.Log("  MeshFilter: " + (mf != null ? "YES" : "NO") + " mesh=" + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "NULL") + " verts=" + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0));
        Debug.Log("  MeshRenderer: " + (mr != null ? "YES" : "NO") + " enabled=" + (mr != null && mr.enabled) + " mat=" + (mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "NULL") + " shader=" + (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.shader != null ? mr.sharedMaterial.shader.name : "NULL"));
        Debug.Log("  Collider: " + (col != null ? "YES" : "NO"));
        Debug.Log("  Layer: " + ground.layer + " (" + LayerMask.LayerToName(ground.layer) + ")");
        Debug.Log("  Bounds: " + mr?.bounds);
    }

    private static void CheckLightsStatic()
    {
        Debug.Log("--- LIGHTS CHECK ---");
        var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        Debug.Log("Total lights: " + lights.Length);

        foreach (var l in lights)
        {
            Debug.Log("  Light: " + l.name + ", type=" + l.type + ", enabled=" + l.enabled);
            Debug.Log("    Color: " + l.color + ", Intensity: " + l.intensity);
            Debug.Log("    Position: " + l.transform.position + ", Rotation: " + l.transform.eulerAngles);
            if (l.type == LightType.Directional)
            {
                Debug.Log("    Shadows: strength=" + l.shadowStrength + ", bias=" + l.shadowBias);
            }
        }

        if (lights.Length == 0)
            Debug.LogError("NO LIGHTS IN SCENE!");
    }

    private static void CheckSkyboxStatic()
    {
        Debug.Log("--- SKYBOX CHECK ---");
        Debug.Log("RenderSettings.skybox: " + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL"));
        if (RenderSettings.skybox != null)
        {
            Debug.Log("  Shader: " + RenderSettings.skybox.shader?.name);
            Debug.Log("  _SkyTint: " + RenderSettings.skybox.GetColor("_SkyTint"));
            Debug.Log("  _Exposure: " + RenderSettings.skybox.GetFloat("_Exposure"));
        }
        else
        {
            Debug.LogWarning("NO SKYBOX ASSIGNED!");
        }
    }

    private static void CheckSystemsStatic()
    {
        Debug.Log("--- SYSTEMS CHECK ---");

        var systems = new (string, System.Type)[]
        {
            ("GameManager", typeof(GameManager)),
            ("TerritoryManager", typeof(TerritoryManager)),
            ("TerritoryBuilder", typeof(TerritoryBuilder)),
            ("MonsterSpawner", typeof(MonsterSpawner)),
            ("GuardManager", typeof(GuardManager)),
            ("MonsterAggroSystem", typeof(MonsterAggroSystem)),
            ("TimeManager", typeof(TimeManager)),
            ("DayNightCycle", typeof(DayNightCycle)),
            ("WeatherManager", typeof(WeatherManager)),
            ("SoundManager", typeof(SoundManager)),
            ("CoreSystemsBootstrap", typeof(CoreSystemsBootstrap)),
        };

        foreach (var entry in systems)
        {
            var name = entry.Item1;
            var type = entry.Item2;
            var obj = FindAnyObjectByType(type);
            Debug.Log("  " + name + ": " + (obj != null ? "FOUND" : "MISSING") + (obj != null ? " (GO: " + obj.name + ")" : ""));
        }

        // TerritoryDatabase는 MonoBehaviour가 아니므로 별도 체크
        try
        {
            var db = TerritoryDatabase.Instance;
            Debug.Log("  TerritoryDatabase: FOUND (Instance=" + (db != null ? "OK" : "NULL") + ")");
        }
        catch (System.Exception e)
        {
            Debug.Log("  TerritoryDatabase: ERROR - " + e.Message);
        }

        var tb = FindAnyObjectByType<TerritoryBuilder>();
        if (tb != null)
        {
            var builtField = typeof(TerritoryBuilder).GetField("_hasBuilt", BindingFlags.NonPublic | BindingFlags.Instance);
            var hasBuilt = builtField?.GetValue(tb);
            Debug.Log("  TerritoryBuilder._hasBuilt: " + hasBuilt);
        }

        var tm = FindAnyObjectByType<TerritoryManager>();
        if (tm != null)
        {
            var buildingsField = typeof(TerritoryManager).GetField("_buildings", BindingFlags.NonPublic | BindingFlags.Instance);
            var guardsField = typeof(TerritoryManager).GetField("_guards", BindingFlags.NonPublic | BindingFlags.Instance);
            var buildings = buildingsField?.GetValue(tm) as System.Collections.IDictionary;
            var guards = guardsField?.GetValue(tm) as System.Collections.IDictionary;
            Debug.Log("  TerritoryManager buildings: " + (buildings?.Count ?? 0) + ", guards: " + (guards?.Count ?? 0));
        }
    }

    private static void CheckRenderSettingsStatic()
    {
        Debug.Log("--- RENDER SETTINGS ---");
        Debug.Log("Ambient Light: " + RenderSettings.ambientLight);
        Debug.Log("Ambient Mode: " + RenderSettings.ambientMode);
        Debug.Log("Fog: " + RenderSettings.fog + ", Color: " + RenderSettings.fogColor + ", Density: " + RenderSettings.fogDensity);
        Debug.Log("Default Reflection: " + RenderSettings.defaultReflectionMode);
    }

    private static void CheckLayersAndTagsStatic()
    {
        Debug.Log("--- LAYER/TAG CHECK ---");
        var player = GameObject.FindWithTag("Player");
        Debug.Log("Player tag: " + (player != null ? "FOUND" : "NOT FOUND"));

        var ground = GameObject.Find("Ground_Inner");
        if (ground != null)
            Debug.Log("Ground layer: " + ground.layer + " (" + LayerMask.LayerToName(ground.layer) + ")");

        var cam = Camera.main;
        if (cam != null)
        {
            Debug.Log("Camera cullingMask: " + cam.cullingMask);
            if (ground != null)
            {
                bool included = (cam.cullingMask & (1 << ground.layer)) != 0;
                Debug.Log("Ground layer in cullingMask: " + included);
            }

            if (player != null)
            {
                bool included = (cam.cullingMask & (1 << player.layer)) != 0;
                Debug.Log("Player layer in cullingMask: " + included);
            }
        }
    }
}