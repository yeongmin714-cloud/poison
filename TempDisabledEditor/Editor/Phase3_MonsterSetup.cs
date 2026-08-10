using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;

public static class Phase3_MonsterSetup
{
    [MenuItem("Tools/Phase 3.8 - 몬스터 배치 & 체력 시스템")]
    public static void SetupMonstersAndHealth()
    {
        // 기존 TopDownScene 열기
        string scenePath = "Assets/Scenes/TopDownScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // ===== 1. MonsterSpawner (원점에 배치) =====
        var spawnerGO = GameObject.Find("MonsterSpawner");
        if (spawnerGO == null)
        {
            spawnerGO = new GameObject("MonsterSpawner");
            var spawner = spawnerGO.AddComponent<MonsterSpawner>();
            Debug.Log("[Setup] ✅ MonsterSpawner 생성");
        }
        else
        {
            Debug.Log("[Setup] ⏭️ MonsterSpawner 이미 존재");
        }

        // ===== 2. PlayerHealth — Player 오브젝트에 추가 =====
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            var health = playerGO.GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = playerGO.AddComponent<PlayerHealth>();
                health.SetInvincibleTime(0.5f);
                Debug.Log("[Setup] ✅ PlayerHealth → Player에 추가");
            }
            else
            {
                Debug.Log("[Setup] ⏭️ PlayerHealth 이미 존재");
            }
        }
        else
        {
            Debug.LogError("[Setup] ❌ Player 게임오브젝트를 찾을 수 없습니다!");
        }

        // ===== 3. HUD (체력바 UI) =====
        var uiGO = GameObject.Find("UI");
        if (uiGO != null)
        {
            var hud = uiGO.GetComponent<HUD>();
            if (hud == null)
            {
                hud = uiGO.AddComponent<HUD>();
                Debug.Log("[Setup] ✅ HUD → UI에 추가");
            }
            else
            {
                Debug.Log("[Setup] ⏭️ HUD 이미 존재");
            }
        }
        else
        {
            // UI 없으면 새로 생성
            uiGO = new GameObject("UI");
            uiGO.AddComponent<HUD>();
            Debug.Log("[Setup] ✅ UI + HUD 새로 생성");
        }

        // ===== 씬 저장 =====
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Setup] ✅ TopDownScene 저장 완료 → {scenePath}");
        Debug.Log("[Setup] 🎮 이제 Play를 누르면 몬스터 24종이 거리별로 배치됩니다!");

        // batchmode 종료
        EditorApplication.Exit(0);
    }

    [MenuItem("Tools/Phase 3.8 - 몬스터 배치 & 체력 시스템", true)]
    private static bool Validate() => true;
}