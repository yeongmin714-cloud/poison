namespace ProjectName.Core
{
    /// <summary>
    /// [U8 은퇴 게이트] UI Toolkit 전환 상태 플래그 — Core 참조(양방향 공용).
    /// UIToolkitBootstrap이 UTK 루트 준비 시 true 세팅. 원본 IMGUI HUD류는 이 플래그로
    /// 자가 은퇴(OnGUI 조기 리턴)하여 UTK와의 이중 표시를 방지한다.
    /// 되돌리기: 플래그 false (또는 부트스트랩 제거) → 원본 IMGUI 100% 복귀.
    /// </summary>
    public static class UITransitionState
    {
        /// <summary>UTK 루트가 살아있고 원본 HUD류가 은퇴해야 하면 true.</summary>
        public static bool UtkActive;
    }
}
