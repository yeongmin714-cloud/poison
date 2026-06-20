using UnityEngine;

/// <summary>
/// 게임 시작 시 MonsterSpawner, PlayerHealth, HUD를 자동 설정.
/// Assembly-CSharp (기본 어셈블리) — 모든 asmdf 참조 가능.
/// </summary>
public class GameSetup : MonoBehaviour
{
    [Header("Auto Setup")]
    [SerializeField] private bool _autoSetup = true;

    private void Start()
    {
        if (!_autoSetup) return;

        // 1. PlayerHealth — Player 태그 오브젝트에 추가
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            if (player.GetComponent<ProjectName.Core.PlayerHealth>() == null)
            {
                var health = player.AddComponent<ProjectName.Core.PlayerHealth>();
                health.SetInvincibleTime(0.5f);
                Debug.Log("[GameSetup] ✅ PlayerHealth → Player에 추가");
            }

            // 1-2. BombThrower — 폭탄 투척 시스템 추가
            if (player.GetComponent<ProjectName.Systems.BombThrower>() == null)
            {
                var thrower = player.AddComponent<ProjectName.Systems.BombThrower>();
                Debug.Log("[GameSetup] ✅ BombThrower → Player에 추가");
            }
        }

        // 2. MonsterSpawner (원점)
        var spawner = FindAnyObjectByType<ProjectName.Systems.MonsterSpawner>();
        if (spawner == null)
        {
            var spawnerGO = new GameObject("MonsterSpawner");
            spawnerGO.AddComponent<ProjectName.Systems.MonsterSpawner>();
            Debug.Log("[GameSetup] ✅ MonsterSpawner 생성");
        }

        // 3. HUD
        var hud = FindAnyObjectByType<ProjectName.UI.HUD>();
        if (hud == null)
        {
            var hudGO = new GameObject("HUD");
            hudGO.AddComponent<ProjectName.UI.HUD>();
            Debug.Log("[GameSetup] ✅ HUD 생성");
        }

        // 4. BuffManager
        var buffManager = FindAnyObjectByType<ProjectName.Core.BuffManager>();
        if (buffManager == null)
        {
            var buffGO = new GameObject("BuffManager");
            buffGO.AddComponent<ProjectName.Core.BuffManager>();
            Debug.Log("[GameSetup] ✅ BuffManager 생성");
        }

        _autoSetup = false; // 한 번만 실행
    }
}