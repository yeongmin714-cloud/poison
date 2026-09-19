// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round D2 — 가스 분사기 HUD 오버레이 (UTK).
    /// 원본: Assets/Scripts/UI/GasSprayUI.cs (299줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 장착 상태 — GasSprayerController.Instance.IsEquipped 실측.
    ///  ② 물약 정보 — LoadedPotionId/LoadedPotionCount + GasSprayer.ClassifyPotion 실측 타입 색/이모지.
    ///  ③ 타이머 — CurrentSprayTimeRemaining / IsReloading / ReloadTimeRemaining, GasSprayerManager.GetGradeData.
    ///  ④ 진행바 — 재장전(파랑)/분사(색상 띠) 비율 표시.
    ///  ⑤ 타입 설명 — GetTypeDescription 실측 매핑 로그.
    ///  각 경로에 [GasUTK] UnityEngine.Debug 로그. 400ms 폴링으로 상태 갱신.
    /// [진입점] static Open()/Toggle(). UTKWindowBase 상속 + static Ensure.
    /// </summary>
    public class GasSprayUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static GasSprayUTK _instance;
        public static GasSprayUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new GasSprayUTK();
        }

        /// <summary>가스 분사기 HUD 열기 (원본 OnDrawGUI 대응).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 =====
        private const float WinW = 320f;
        private const float WinH = 170f;
        private const long RefreshMs = 400L;

        // ===== 레퍼런스 =====
        private readonly Label _equipLabel;
        private readonly Label _potionLabel;
        private readonly Label _timerLabel;
        private readonly Label _typeLabel;
        private readonly VisualElement _barBg;
        private readonly VisualElement _barFill;
        private readonly IVisualElementScheduledItem _refreshTask;

        private static readonly Color EmptyColor = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color PoisonColor = new Color(1f, 0.2f, 0.2f);
        private static readonly Color MentalColor = new Color(0.7f, 0.2f, 1f);
        private static readonly Color HealColor = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color BuffColor = new Color(0.2f, 0.5f, 1f);

        private GasSprayUTK() : base("💨 가스 분사기", new Vector2(WinW, WinH))
        {
            _content.style.flexDirection = FlexDirection.Column;

            _equipLabel = MakeLabel(UTKColor.TextPrimary, true);
            _content.Add(_equipLabel);

            _potionLabel = MakeLabel(new Color(0.8f, 0.7f, 0.3f), true);
            _content.Add(_potionLabel);

            _timerLabel = MakeLabel(UTKColor.TextPrimary, true);
            _content.Add(_timerLabel);

            // ── 진행바 ──
            _barBg = new VisualElement();
            _barBg.style.height = 16f;
            _barBg.style.marginTop = 6f;
            _barBg.style.marginBottom = 4f;
            _barBg.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.8f));
            _content.Add(_barBg);

            _barFill = new VisualElement();
            _barFill.style.height = 14f;
            _barFill.style.width = 0f;
            _barFill.style.backgroundColor = new StyleColor(new Color(0.3f, 0.8f, 0.3f));
            _barBg.Add(_barFill);

            _typeLabel = MakeLabel(new Color(0.7f, 0.7f, 0.7f), true);
            _typeLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_typeLabel);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 40f;
            style.bottom = 200f;

            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) Refresh();
            }).Every(RefreshMs);
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            Refresh();
            UnityEngine.Debug.Log("[GasUTK] 가스 분사기 HUD 열림");
        }

        public override void Hide()
        {
            base.Hide();
            UnityEngine.Debug.Log("[GasUTK] 가스 분사기 HUD 닫힘");
        }

        // =================== 갱신 ===================

        private void Refresh()
        {
            var ctrl = GasSprayerController.Instance;
            if (ctrl == null)
            {
                _equipLabel.text = "⚠️ 가스 분사기 시스템 없음";
                _potionLabel.text = "";
                _timerLabel.text = "";
                _typeLabel.text = "";
                _barFill.style.width = 0f;
                return;
            }

            if (!ctrl.IsEquipped)
            {
                _equipLabel.text = "⚠️ 분사기 미장착";
                _potionLabel.text = "⚠️ 물약 없음";
                _timerLabel.text = "💨 준비됨";
                _typeLabel.text = "";
                _barFill.style.width = 0f;
                UnityEngine.Debug.Log("[GasUTK] 미장착 상태 — 화면 표시 생략");
                return;
            }

            _equipLabel.text = "🔧 " + ctrl.EquippedSprayerName + " (등급: " + ctrl.CurrentGrade + ")";

            string potionId = ctrl.LoadedPotionId;
            int count = ctrl.LoadedPotionCount;
            bool hasPotion = !string.IsNullOrEmpty(potionId) && count > 0;

            PotionType type = hasPotion ? GasSprayer.ClassifyPotion(potionId) : PotionType.None;
            Color typeColor = hasPotion ? GetTypeColor(type) : EmptyColor;

            _potionLabel.text = hasPotion
                ? GetTypeEmoji(type) + " " + potionId + " x" + count + " (" + GetTypeName(type) + ")"
                : "⚠️ 물약 없음";
            _potionLabel.style.color = new StyleColor(typeColor);

            float ratio = 0f;
            if (ctrl.IsReloading)
            {
                float reloadDuration = GasSprayerManager.GetReloadTime(ctrl.CurrentGrade);
                ratio = reloadDuration > 0f
                    ? Mathf.Clamp01(1f - (ctrl.ReloadTimeRemaining / reloadDuration))
                    : 1f;
                _timerLabel.text = "🔄 재장전... " + Mathf.Max(0f, ctrl.ReloadTimeRemaining).ToString("F1") + "s";
                _barFill.style.backgroundColor = new StyleColor(new Color(0.3f, 0.5f, 1f, 0.9f));
                UnityEngine.Debug.Log("[GasUTK] 재장전 중 — 잔여 " + ctrl.ReloadTimeRemaining.ToString("F1") + "s, 진행 " + ratio.ToString("F2"));
            }
            else
            {
                var data = GasSprayerManager.GetGradeData(ctrl.CurrentGrade);
                float remaining = Mathf.Max(0f, ctrl.CurrentSprayTimeRemaining);
                float maxTime = data.maxSprayTime;

                if (data.isUnlimited || remaining >= float.MaxValue / 2)
                {
                    _timerLabel.text = "♾️ 무제한";
                    ratio = 1f;
                    _barFill.style.backgroundColor = new StyleColor(AccentRare(0.9f));
                }
                else
                {
                    ratio = maxTime > 0f ? Mathf.Clamp01(remaining / maxTime) : 0f;
                    _timerLabel.text = "💨 " + remaining.ToString("F1") + "s / " + maxTime.ToString("F0") + "s";
                    _barFill.style.backgroundColor = new StyleColor(BarColorFor(ratio));
                }

                if (hasPotion)
                {
                    _typeLabel.text = GetTypeDescription(type);
                    UnityEngine.Debug.Log("[GasUTK] 물약 " + potionId + " 감시 — 잔여 " + remaining.ToString("F1") + "s");
                }
                else
                {
                    _typeLabel.text = "물약 미장전 — 기본 분사";
                }
            }

            _barFill.style.width = (_barBg.resolvedStyle.width > 0f)
                ? _barBg.resolvedStyle.width * ratio
                : 0f;
        }

        // =================== 헬퍼 ===================

        private static Label MakeLabel(Color color, bool bold)
        {
            var l = new Label("");
            l.style.fontSize = 14f;
            l.style.color = new StyleColor(color);
            l.style.marginTop = 2f;
            l.style.marginBottom = 2f;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        private static Color AccentRare(float alpha)
            => new Color(0.9f, 0.72f, 0.23f, alpha);

        private static Color BarColorFor(float ratio)
        {
            if (ratio > 0.5f) return Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);
            return Color.Lerp(Color.red, Color.yellow, ratio * 2f);
        }

        private static Color GetTypeColor(PotionType type)
        {
            switch (type)
            {
                case PotionType.Poison: return PoisonColor;
                case PotionType.Mental: return MentalColor;
                case PotionType.Heal:   return HealColor;
                case PotionType.Buff:   return BuffColor;
                default:                return EmptyColor;
            }
        }

        private static string GetTypeEmoji(PotionType type)
        {
            switch (type)
            {
                case PotionType.Poison: return "☠️";
                case PotionType.Mental: return "🌀";
                case PotionType.Heal:   return "💚";
                case PotionType.Buff:   return "💪";
                default:                return "🧪";
            }
        }

        private static string GetTypeName(PotionType type)
        {
            switch (type)
            {
                case PotionType.Poison: return "공격성(독)";
                case PotionType.Mental: return "정신성(마약)";
                case PotionType.Heal:   return "회복성(치료)";
                case PotionType.Buff:   return "물리성(강화)";
                default:                return "알 수 없음";
            }
        }

        private static string GetTypeDescription(PotionType type)
        {
            switch (type)
            {
                case PotionType.Poison: return "🔴 붉은 안개 — 적 지속 데미지 5~15";
                case PotionType.Mental: return "🟣 보라색 안개 — 적 환각/혼란";
                case PotionType.Heal:   return "🟢 초록색 안개 — 아군 체력 회복";
                case PotionType.Buff:   return "🔵 파란색 안개 — 아군 버프";
                default:                return "";
            }
        }
    }
}