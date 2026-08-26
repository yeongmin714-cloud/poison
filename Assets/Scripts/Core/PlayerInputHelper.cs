using UnityEngine;
using UnityEngine.InputSystem;

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

        bool wasActive = player.activeInHierarchy;
        if (wasActive) player.SetActive(false);  // OnEnable 방지

        var pi = player.GetComponent<PlayerInput>() ?? player.AddComponent<PlayerInput>();
        pi.actions = actions;
        pi.defaultActionMap = defaultActionMap;
        pi.notificationBehavior = PlayerNotifications.InvokeUnityEvents;

        if (wasActive) player.SetActive(true);   // 이제 OnEnable 호출 - actions 이미 할당됨

        Debug.Log($"[PlayerInputHelper] ✅ PlayerInput 설정 완료: actions={actions.name}, defaultMap={defaultActionMap}");
        return pi;
    }

    /// <summary>
    /// Resources에서 PlayerControls 로드 후 설정
    /// </summary>
    public static PlayerInput SetupPlayerInputFromResources(GameObject player, string resourcePath = "Input/PlayerControls", string defaultActionMap = "Player")
    {
        var actions = Resources.Load<InputActionAsset>(resourcePath);
        if (actions == null)
        {
            Debug.LogError($"[PlayerInputHelper] InputActionAsset not found at Resources/{resourcePath}");
            return null;
        }
        return SetupPlayerInput(player, actions, defaultActionMap);
    }
}