using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U0 — 런타임 부트스트랩.
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 씬 로드 직전(BeforeSceneLoad) 1회 실행되어:
    ///   - Resources/UI/PanelSettings 로드 (없으면 경고 1회, 재시도 없음 — 에디터 스크립트가 생성)
    ///   - DontDestroyOnLoad "UTKRoot" GameObject + UIDocument 생성
    ///   - rootVisualElement에 Theme.uss 적용
    ///   - static UIRoot 접근자 + public Ensure() (CoreSystemsBootstrap Ensure 스타일, 멱등) 제공
    /// </summary>
    public static class UIToolkitBootstrap
    {
        private const string PanelSettingsRes = "UI/PanelSettings";
        private const string ThemeUssRes      = "UI/Theme";
        private const string RootGoName       = "UTKRoot";

        private static UIDocument _document;
        private static bool _warnedPanelMissing;

        /// <summary>UTK 루트 UIDocument 루트 VisualElement (없으면 null).</summary>
        public static VisualElement UIRoot => _document != null ? _document.rootVisualElement : null;

        /// <summary>부트스트랩이 이미 완료(중복 가드)되었는지.</summary>
        public static bool IsReady => _document != null;

        /// <summary>[GNB] 지형 관찰 씬에서 UI 전체 비활성 상태 여부.</summary>
        public static bool IsDisabled() => ProjectName.Core.UITransitionState.DisableAllUi;

        /// <summary>[GNB] TerrainOnly 씬용 — true 시 기존 UTK 루트를 숨기고 신규 UI 생성을 막는다.</summary>
        public static void SetDisabled(bool v)
        {
            ProjectName.Core.UITransitionState.DisableAllUi = v;
            if (_document != null && _document.rootVisualElement != null)
                _document.rootVisualElement.style.visibility = v ? Visibility.Hidden : Visibility.Visible;
            if (v) ProjectName.Core.UITransitionState.UtkActive = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Ensure();
        }

        /// <summary>외부 강제 보장용 — 멱등(중복 실행 무시). CoreSystemsBootstrap Ensure 스타일.</summary>
        public static void Ensure()
        {
            // [GNB] 지형 관찰 씬 — UI 부트스트랩 생략(렉 제거)
            if (ProjectName.Core.UITransitionState.DisableAllUi)
                return;
            if (_document != null)
                return;

            var settings = Resources.Load<PanelSettings>(PanelSettingsRes);
            if (settings == null)
            {
                if (!_warnedPanelMissing)
                {
                    _warnedPanelMissing = true;
                    Debug.LogWarning($"[UIToolkitBootstrap] PanelSettings 없음({PanelSettingsRes}) — 에디터의 UIToolkitSetup이 생성 예정. UI Toolkit 비활성화.");
                }
                return;
            }

            var go = new GameObject(RootGoName);
            Object.DontDestroyOnLoad(go);
            _document = go.AddComponent<UIDocument>();
            _document.panelSettings = settings;

            var theme = Resources.Load<StyleSheet>(ThemeUssRes);
            if (theme != null)
                _document.rootVisualElement.styleSheets.Add(theme);
            else
                Debug.LogWarning($"[UIToolkitBootstrap] Theme.uss 로드 실패({ThemeUssRes})");

            ProjectName.Core.UITransitionState.UtkActive = true;   // [U8 은퇴 게이트] 원본 HUD류 자가 은퇴 트리거
            // [U8] 월드 이름표 오버레이 — UIRoot "먼저"(index 0) 부착되어 모든 창보다 아래 깔림.
            NameplateOverlayUTK.Ensure();
            // [P22-5] 좌하단 원형 스테이터스 게이지 (IMGUI 스태미나 바/하트 HUD 대체)
            StatusGaugesUTK.Ensure();
            // [P22-2] 화면 커서 오버레이 (3D 앵커 커서 폐기 — 항상 정확히 마우스 위치 표시)
            UTKCursorOverlay.Ensure();
            // [P25-C1] 활 조준 리티클 (기본 숨김 — 드로 시 표시)
            BowAimReticleUTK.Ensure();
            // [UTK] 원본 IMGUI 이름표/헤드UI 3종 은퇴 — NameplateOverlayUTK가 데이터·표시 규칙을 계승.
            ProjectName.Systems.GuardHeadUI.s_retired = true;
            ProjectName.Systems.MonsterHeadUI.s_retired = true;
            ProjectName.Systems.NameplateDisplay.s_retired = true;
            Debug.Log("[UIToolkitBootstrap] UI Toolkit 부트스트랩 완료 (UTKRoot)");
        }

        // ─────────────────────────── ESC 통합 (선택적 게이트) ───────────────────────────
        // Pause 게이트: 게임 일시정지 시 ESC 무시 여부 — 추후 스펙 확정 시 여기에 통합.
        // 1프레임 Update용 멤버로 사용. WindowManager의 Update 루프에서 호출되도록 공개 유틸로.
        internal static bool EscapePressedThisFrame()
        {
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame && !_ignoreEscapeWhenPaused();
        }

        private static bool _ignoreEscapeWhenPaused()
        {
            // 추후 Pause 상태 조회 연동 지점 — 현재는 항상 허용.
            return false;
        }
    }
}