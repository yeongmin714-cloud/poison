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

        /// <summary>[U8 수리] 포인터가 UTK UI 위에 있음 — 게임 월드 클릭(공격/RTS/카메라) 차단용. UTK측 Update에서 갱신.</summary>
        public static bool PointerOverUI;

        /// <summary>[U8 수리] UTK 창이 하나라도 열려 있음 — 월드 이름표/헤드UI 은퇴용(창 위 렌더 겹침 방지). UTKWindowManager가 갱신.</summary>
        public static bool AnyWindowOpen;

        /// <summary>[P14] 실내 씬 활성 — 병사/몬스터 AI가 플레이어 추적/어그로를 실내로 끌고 오는 것 차단.
        ///   IndoorSceneTransition(Enter/Exit)에서 갱신, Systems AI 루프가 게이트로 읽음(Core 양방향 공용 규약).</summary>
        public static bool IndoorActive;

        /// <summary>[GNB] 지형 관찰 테스트씬(TerrainOnly)에서 UI 전체 비활성 — UIToolkitBootstrap이 루트를 숨기고
        ///   신규 UI 생성을 막는 게이트. GameSetup.RunTerrainOnlySetup이 세팅.</summary>
        public static bool DisableAllUi;
    }
}
