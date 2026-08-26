using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

/// <summary>
/// PlayerInput 안전 설정 헬퍼 - OnEnable 전 actions 할당 보장
/// </summary>
public static class PlayerInputHelper
{
    /// <summary>
    /// PlayerInput 컴포넌트 안전 추가 및 설정
    /// GameObject를 잠시 비활성화하여 OnEnable이 actions 할당 이후에 호출되도록 보장
    /// </summary>
    public static PlayerInput SetupPlayerInput(GameObject player, InputActionAsset actions, string defaultActionMap = "Player")
    {
        if (player == null) return null;
        if (actions == null) 
        {
            Debug.LogError("[PlayerInputHelper] InputActionAsset is null!");
            return null;
        }

        // Verify action map exists
        var actionMap = actions.FindActionMap(defaultActionMap);
        if (actionMap == null)
        {
            var availableMaps = actions.actionMaps.Select(m => m.name).ToArray();
            Debug.LogError($"[PlayerInputHelper] Action map '{defaultActionMap}' not found in {actions.name}. Available maps: [{string.Join(", ", availableMaps)}]");
            return null;
        }

        bool wasActive = player.activeInHierarchy;
        if (wasActive) player.SetActive(false);  // OnEnable 방지

        var pi = player.GetComponent<PlayerInput>() ?? player.AddComponent<PlayerInput>();
        pi.actions = actions;
        pi.defaultActionMap = defaultActionMap;
        pi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;

        if (wasActive) player.SetActive(true);   // 이제 OnEnable 호출 - actions 이미 할당됨

        Debug.Log($"[PlayerInputHelper] ✅ PlayerInput 설정 완료: actions={actions.name}, defaultMap={defaultActionMap}, maps=[{string.Join(", ", actions.actionMaps.Select(m => m.name))}]");
        return pi;
    }

    /// <summary>
    /// Resources에서 PlayerControls 로드 후 설정 (다중 폴백 전략)
    /// </summary>
    public static PlayerInput SetupPlayerInputFromResources(GameObject player, string resourcePath = "Input/PlayerControls", string defaultActionMap = "Player")
    {
        // Strategy 1: Resources.Load with exact path
        var actions = Resources.Load<InputActionAsset>(resourcePath);
        
        // Strategy 2: Try alternative paths
        if (actions == null)
        {
            var altPaths = new[] { "PlayerControls", "Input/PlayerControls", "Controls/PlayerControls" };
            foreach (var path in altPaths)
            {
                actions = Resources.Load<InputActionAsset>(path);
                if (actions != null)
                {
                    Debug.Log($"[PlayerInputHelper] Loaded from alternative path: {path}");
                    break;
                }
            }
        }

        // Strategy 3: Load all and find by name
        if (actions == null)
        {
            var allActions = Resources.LoadAll<InputActionAsset>("");
            actions = allActions.FirstOrDefault(a => a.name == "PlayerControls");
            if (actions != null)
            {
                Debug.Log($"[PlayerInputHelper] Found by name search: {actions.name}");
            }
        }

        if (actions == null)
        {
            var available = string.Join(", ", Resources.LoadAll<InputActionAsset>("").Select(a => a.name));
            Debug.LogError($"[PlayerInputHelper] InputActionAsset 'PlayerControls' not found in Resources! Searched: {resourcePath} + alternatives. Available in Resources: [{available}]");
            return null;
        }

        return SetupPlayerInput(player, actions, defaultActionMap);
    }
}