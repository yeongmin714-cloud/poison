using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P25-C1 — 활 조준 리티클 (BotW 스타일 화면 스페이스 오버레이).
    /// 좌클릭 드로 중 마우스 위치에 표시: 중앙 점 + 4 꺾쇠 브래킷 + 파워 링 + 잔여 화살 개수.
    /// 드로 파워 0→1에 따라 브래킷이 중앙으로 수렴. 릴리즈(발사) 후 0.4s 페이드아웃(유지감).
    /// 상태는 Systems.BowAimState 폴링(16ms) — UI→Systems 단방향 의존(계층 규칙).
    /// 시각 요소 = 베이크 PNG(점/꺾쇠) + UTKCircularGauge(파워 링) — 프리미티브/IMGUI 라인 금지(P22-6).
    /// </summary>
    public class BowAimReticleUTK : VisualElement
    {
        private static BowAimReticleUTK _instance;

        private const float RootSize = 200f;
        private const float CenterX = RootSize * 0.5f;      // 100
        private const float CenterY = RootSize * 0.5f;
        private const float BaseOffset = 66f;               // 브래킷 최외곽 반경(파워 0) — 0.55 수렴 계수
        private const float FadeMs = 400f;                  // 릴리즈 후 페이드아웃

        private readonly VisualElement _dot;
        private readonly VisualElement _bracketTL, _bracketTR, _bracketBL, _bracketBR;
        private readonly UTKCircularGauge _ring;
        private readonly Label _countLabel;

        private bool _visible;          // 현재 리티클 표시 중 여부
        private float _fadeStart = -1f; // 페이드아웃 시작(ms) — -1 = 페이드 아님

        public static BowAimReticleUTK Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new BowAimReticleUTK();
            root.Add(_instance);
            Debug.Log("[BowAimReticleUTK][P25-C1] 조준 리티클 오버레이 부착");
            return _instance;
        }

        private BowAimReticleUTK()
        {
            name = "BowAimReticleUTK";
            pickingMode = PickingMode.Ignore;   // 클릭은 실제 입력으로 — 시각 전용
            style.position = Position.Absolute;
            style.width = RootSize;
            style.height = RootSize;
            style.left = 0f;
            style.top = 0f;
            style.display = DisplayStyle.None;  // 기본 숨김

            var dotTex = Resources.Load<Texture2D>("UI/ReticleDot");
            var brTL = Resources.Load<Texture2D>("UI/ReticleBracketTL");
            var brTR = Resources.Load<Texture2D>("UI/ReticleBracketTR");
            var brBL = Resources.Load<Texture2D>("UI/ReticleBracketBL");
            var brBR = Resources.Load<Texture2D>("UI/ReticleBracketBR");

            // 중앙 점
            _dot = makeIcon(dotTex, 14f);
            _dot.style.left = CenterX - 7f;
            _dot.style.top = CenterY - 7f;
            Add(_dot);

            // 파워 링(UTKCircularGauge — 시계방향 fill)
            _ring = new UTKCircularGauge();
            _ring.pickingMode = PickingMode.Ignore;
            _ring.style.position = Position.Absolute;
            _ring.style.width = 46f;
            _ring.style.height = 46f;
            _ring.style.left = CenterX - 23f;
            _ring.style.top = CenterY - 23f;
            _ring.FillColor = new Color(0.9f, 0.95f, 1f, 0.92f);   // 밝은 하늘색 파워 링
            Add(_ring);

            // 꺾쇠 브래킷 4개
            _bracketTL = makeIcon(brTL, 64f);
            _bracketTR = makeIcon(brTR, 64f);
            _bracketBL = makeIcon(brBL, 64f);
            _bracketBR = makeIcon(brBR, 64f);
            Add(_bracketTL); Add(_bracketTR); Add(_bracketBL); Add(_bracketBR);

            // 잔여 화살 개수 라벨 — 리티클 우상단
            _countLabel = new Label();
            _countLabel.pickingMode = PickingMode.Ignore;
            _countLabel.style.position = Position.Absolute;
            _countLabel.style.left = CenterX + 46f;
            _countLabel.style.top = CenterY - 82f;
            _countLabel.style.fontSize = 18f;
            _countLabel.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.95f));
            _countLabel.text = "×0";
            Add(_countLabel);

            UpdateBrackets(0f);
            schedule.Execute(UpdateTick).Every(16);
        }

        private void UpdateTick()
        {
            // ① 릴리즈 이벤트 소비 + 페이드아웃 시작
            if (BowAimState.ReleasePending && _visible)
            {
                bool fired = BowAimState.ReleaseFired;
                BowAimState.ResetRelease();
                if (fired)
                {
                    _fadeStart = Time.time * 1000f;   // 발사 후 0.4s 유지감
                }
                else
                {
                    HideNow();                              // 탭 캔슬 — 즉시 숨김
                }
            }

            // ② 페이드아웃 진행
            if (_fadeStart >= 0f)
            {
                float elapsed = Time.time * 1000f - _fadeStart;
                if (elapsed >= FadeMs) HideNow();
                else style.opacity = 1f - elapsed / FadeMs;
                return;
            }

            // ③ 드로 상태 반영
            bool drawing = BowAimState.Drawing;
            if (drawing && !_visible)
            {
                _visible = true;
                style.display = DisplayStyle.Flex;
                style.opacity = 1f;
            }
            if (!drawing || !_visible) return;

            UpdatePosition();
            _ring.Fraction = BowAimState.Power;
            _ring.CommitIfDirty();
            UpdateBrackets(BowAimState.Power);
            if (ArrowManager.Instance != null)
            {
                int count = ArrowManager.Instance.GetTotalArrowCount();
                // [F 고품질] 지금 발사될 화살 종류 표기 — 마법/강화/일반 + 개수. 색상도 티어별.
                var type = ArrowManager.Instance.GetNextArrowType();
                _countLabel.text = "×" + count;
                switch (type)
                {
                    case ProjectName.Core.ArrowData.ArrowType.Magic:
                        _countLabel.text = "◆×" + count;
                        _countLabel.style.color = new StyleColor(new Color(0.95f, 0.4f, 1f, 0.95f)); // 보라
                        break;
                    case ProjectName.Core.ArrowData.ArrowType.Reinforced:
                        _countLabel.text = "●×" + count;
                        _countLabel.style.color = new StyleColor(new Color(0.95f, 0.95f, 1f, 0.95f)); // 은백
                        break;
                    default:
                        _countLabel.text = "×" + count;
                        _countLabel.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.95f)); // 흰
                        break;
                }
            }
        }

        private void HideNow()
        {
            _visible = false;
            _fadeStart = -1f;
            style.display = DisplayStyle.None;
        }

        private void UpdatePosition()
        {
            var root = UIToolkitBootstrap.UIRoot;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (root == null || mouse == null) return;
            var screen = mouse.position.ReadValue();
            float scale = root.worldBound.width / (float)Screen.width;
            if (scale <= 0f) scale = 1f;
            float px = screen.x * scale;
            float py = root.worldBound.height - screen.y * scale;
            style.left = px - RootSize * 0.5f;   // 리티클 중심 = 마우스(조준점)
            style.top = py - RootSize * 0.5f;
            BringToFront();
        }

        /// <summary>파워 0→1: 브래킷이 중앙으로 55% 수렴.</summary>
        private void UpdateBrackets(float power)
        {
            float o = BaseOffset * (1f - power * 0.55f);
            _bracketTL.style.left = CenterX - o;            _bracketTL.style.top = CenterY - o;
            _bracketTR.style.left = CenterX + o - 64f;      _bracketTR.style.top = CenterY - o;
            _bracketBL.style.left = CenterX - o;            _bracketBL.style.top = CenterY + o - 64f;
            _bracketBR.style.left = CenterX + o - 64f;      _bracketBR.style.top = CenterY + o - 64f;
        }

        private VisualElement makeIcon(Texture2D tex, float size)
        {
            var el = new VisualElement();
            el.pickingMode = PickingMode.Ignore;   // [P23 규약] 오버레이 자식은 픽커블 금지
            el.style.position = Position.Absolute;
            el.style.width = size;
            el.style.height = size;
            if (tex != null)
            {
                el.style.backgroundImage = UTKTextureSafe.ToBackground(tex);
                el.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            return el;
        }
    }
}