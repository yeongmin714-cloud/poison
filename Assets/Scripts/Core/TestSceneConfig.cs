using UnityEngine;

/// <summary>
/// 테스트 씬에서 활성화할 시스템을 지정하는 설정 컴포넌트.
/// TestSceneGenerator가 생성한 씬에 자동으로 추가됩니다.
/// 메인씬(MainScene)에서는 사용되지 않으며, GameSetup이 이 값을 읽어 시스템을 선택적으로 초기화합니다.
/// </summary>
public class TestSceneConfig : MonoBehaviour
{
    [Tooltip("테스트 포커스 설명 (Generator에서 자동 설정)")]
    public string testFocus = "";

    [Tooltip("활성화할 시스템 목록")]
    public string[] enabledSystems = new string[0];

    [Tooltip("이 Scene이 테스트 Scene인지 여부 (자동 설정)")]
    public bool isTestScene = true;

    /// <summary>
    /// 특정 시스템이 활성화되어 있는지 확인
    /// </summary>
    public bool IsSystemEnabled(string systemName)
    {
        if (enabledSystems == null || enabledSystems.Length == 0)
            return false;

        foreach (var s in enabledSystems)
        {
            if (s == "All" || s == systemName)
                return true;
        }
        return false;
    }
}