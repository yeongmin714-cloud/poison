using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;

/// <summary>
/// 사장님이 GLB 파일을 제공하기 전까지 사용할 Placeholder(임시 도형)들을 생성합니다.
/// 
/// 작동 방식:
/// 1. 이 스크립트가 모든 임시 오브젝트를 생성
/// 2. 사장님이 GLB를 UserProvided/ 에 넣으면 ModelSwapper가 자동 교체
/// 3. placeholder → 실제 모델로 변경됨
/// 
/// [현재 placeholder 목록]
/// - Player: 기본 도형 5개로 만든 사람 (PlayerPlaceholder 스크립트)
/// </summary>
public static class PlaceholderSetup
{
    [MenuItem("Tools/Placeholders - Create All")]
    public static void CreateAllPlaceholders()
    {
        // 현재 씬 열기
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");

        // 1. 플레이어 Placeholder
        SetupPlayerPlaceholder();

        Debug.Log("[PlaceholderSetup] 모든 Placeholder 생성 완료!");
        Debug.Log("[PlaceholderSetup] GLB 파일을 Assets/Resources/Models/UserProvided/ 에 넣으면 자동 교체됩니다.");
    }

    /// <summary>
    /// 플레이어에 임시 사람 모양 추가
    /// </summary>
    private static void SetupPlayerPlaceholder()
    {
        var player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("[PlaceholderSetup] Player 오브젝트를 찾을 수 없습니다!");
            return;
        }

        // PlayerPlaceholder 스크립트 추가 (기본 도형으로 사람 모양 생성)
        var placeholder = player.GetComponent<PlayerPlaceholder>();
        if (placeholder == null)
        {
            placeholder = player.AddComponent<PlayerPlaceholder>();
            Debug.Log("[PlaceholderSetup] Player에 PlayerPlaceholder 추가됨");
        }
        else
        {
            Debug.Log("[PlaceholderSetup] PlayerPlaceholder 이미 존재함");
        }
    }

    [MenuItem("Tools/Placeholders - Create All", true)]
    private static bool Validate()
    {
        return true;
    }
}