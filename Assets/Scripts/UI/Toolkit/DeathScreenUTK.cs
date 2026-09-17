using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 — 사망 화면 (원본 DeathScreenUI.cs 201줄 포팅, additive).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 원본 데이터 경로 그대로:
    ///   - 부활: PlayerHealth._isDead 리플렉션 해제 + HealFull()
    ///   - 로드: SaveManager.Instance.Load(0) (첫 번째 슬롯)
    ///   - 표시: Time.timeScale = 0 홀드, 숨김 시 1 복원
    /// 전체화면 붉은 페이드 오버레이 + 부활/로드 2버튼. [DeathUTK] 실측 로그.
    /// </summary>
    public class DeathScreenUTK : VisualElement
    {
        private const float FadeDuration = 0.6f;

        private static DeathScreenUTK _instance;
        private readonly VisualElement _fadeRoot;
        private readonly VisualElement _buttonRow;
        private readonly Label _title;

        private bool _visible;
        private float _fadeAlpha;
        private bool _fadingIn;

        private DeathScreenUTK()
        {
            style.position = Position.Absolute;
            style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            style.backgroundColor = new StyleColor(new Color(0.55f, 0f, 0f, 0f)); // 붉은 페이드
            style.display = DisplayStyle.None;
            pickingMode = PickingMode.Position; // 뒤 클릭 차단

            _fadeRoot = new VisualElement();
            _fadeRoot.style.position = Position.Absolute;
            _fadeRoot.style.left = 0; _fadeRoot.style.right = 0; _fadeRoot.style.top = 0; _fadeRoot.style.bottom = 0;
            _fadeRoot.style.alignItems = Align.Center;
            _fadeRoot.style.justifyContent = Justify.Center;
            Add(_fadeRoot);

            _title = new Label("☠ 사망");
            _title.style.fontSize = 60;
            _title.style.color = new StyleColor(Color.white);
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.marginBottom = 24;
            _fadeRoot.Add(_title);

            _buttonRow = new VisualElement();
            _buttonRow.style.flexDirection = FlexDirection.Row;
            _buttonRow.style.marginTop = 8;
            _fadeRoot.Add(_buttonRow);

            var respawnBtn = UTKButton.Create("🔄 부활", OnRespawn, UTKButton.Variant.Primary);
            respawnBtn.style.width = 180;
            respawnBtn.style.height = 46;
            respawnBtn.style.marginRight = 16;
            _buttonRow.Add(respawnBtn);

            var loadBtn = UTKButton.Create("📂 저장 불러오기", OnLoadGame, UTKButton.Variant.Secondary);
            loadBtn.style.width = 180;
            loadBtn.style.height = 46;
            _buttonRow.Add(loadBtn);

            UTKWindowBase.ApplyUIToolkitFont(this);
            schedule.Execute(TickFade).Every(50);
        }

        /// <summary>UTK 루트에 부착된 인스턴스 보장(멱등).</summary>
        public static DeathScreenUTK Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new DeathScreenUTK();
            root.Add(_instance);
            Debug.Log("[DeathUTK] 사망 화면 인스턴스 생성");
            return _instance;
        }

        /// <summary>사망 화면 표시 — Time.timeScale 0 홀드(원본 동일).</summary>
        public static void Show()
        {
            var i = Ensure();
            if (i == null) return;
            i._visible = true;
            i._fadingIn = true;
            i._fadeAlpha = 0f;
            i.style.display = DisplayStyle.Flex;
            i.BringToFront();
            Time.timeScale = 0f;
            Debug.Log("[DeathUTK] 사망 화면 표시 (timeScale=0)");
        }

        /// <summary>숨김 — timeScale 1 복원(원본 동일).</summary>
        public static void Hide()
        {
            if (_instance == null) return;
            _instance._visible = false;
            _instance.style.display = DisplayStyle.None;
            Time.timeScale = 1f;
            Debug.Log("[DeathUTK] 사망 화면 숨김 (timeScale=1)");
        }

        private void TickFade()
        {
            if (!_visible || !_fadingIn) return;
            _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 0.85f, (Time.unscaledDeltaTime / FadeDuration) * 0.85f);
            var c = style.backgroundColor.value;
            style.backgroundColor = new StyleColor(new Color(c.r, c.g, c.b, _fadeAlpha));
            if (_fadeAlpha >= 0.85f) _fadingIn = false;
        }

        private void OnRespawn()
        {
            // 원본 데이터 경로 동일 — _isDead 리플렉션 해제 + HealFull()
            if (PlayerHealth.Instance != null)
            {
                var deadField = typeof(PlayerHealth).GetField("_isDead",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (deadField != null)
                    deadField.SetValue(PlayerHealth.Instance, false);
                PlayerHealth.Instance.HealFull();
            }
            Debug.Log("[DeathUTK] 부활 실행");
            Hide();
        }

        private void OnLoadGame()
        {
            Hide();
            // 원본 동일 — SaveManager 첫 번째 슬롯 로드
            if (SaveManager.Instance != null)
                SaveManager.Instance.Load(0);
            Debug.Log("[DeathUTK] 슬롯 0 로드");
        }
    }
}
