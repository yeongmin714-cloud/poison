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
        private const float WinH = 200f;
        private const long RefreshMs = 400L;

        // ===== 레퍼런스 =====
        private readonly Label _equipLabel;
        private readonly Label _potionLabel;
        private readonly Label _timerLabel;
        private readonly Label _typeLabel;
        private readonly VisualElement _barBg;
        private readonly VisualElement _barFill;
        private readonly Label _doseLabel;
        private readonly VisualElement _doseBarBg;
        private readonly VisualElement _doseBarFill;
        private readonly IVisualElementScheduledItem _refreshTask;
        private bool _sprayStatusMode;
        private bool _doseExhaustedStatusMode;
        private long _doseExhaustedGeneration;

        private static readonly Color EmptyColor = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color PoisonColor = new Color(1f, 0.2f, 0.2f);
        private static readonly Color MentalColor = new Color(0.7f, 0.2f, 1f);
        private static readonly Color HealColor = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color BuffColor = new Color(0.2f, 0.5f, 1f);

        private GasSprayUTK() : base("💨 가스 분사기", new Vector2(WinW, WinH))
        {
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 10f;
            _content.style.paddingRight = 10f;
            _content.style.paddingTop = 8f;
            _content.style.paddingBottom = 8f;

            // GitHub-dark 헤더 카드: 장착 중인 가스통/등급을 우선 강조.
            var equipmentCard = new VisualElement();
            equipmentCard.name = "GasEquipmentCard";
            equipmentCard.style.flexDirection = FlexDirection.Column;
            equipmentCard.style.backgroundColor = new StyleColor(new Color32(22, 27, 34, 255)); // #161B22
            equipmentCard.style.paddingLeft = 12f;
            equipmentCard.style.paddingRight = 12f;
            equipmentCard.style.paddingTop = 8f;
            equipmentCard.style.paddingBottom = 8f;
            equipmentCard.style.marginBottom = 6f;
            equipmentCard.style.borderTopLeftRadius = 7f;
            equipmentCard.style.borderTopRightRadius = 7f;
            equipmentCard.style.borderBottomLeftRadius = 7f;
            equipmentCard.style.borderBottomRightRadius = 7f;
            equipmentCard.style.borderLeftWidth = 3f;
            equipmentCard.style.borderLeftColor = new StyleColor(new Color32(88, 166, 255, 255)); // #58A6FF

            _equipLabel = MakeLabel(new Color32(240, 246, 252, 255), true);
            _equipLabel.style.fontSize = 15.6f;
            _equipLabel.style.color = new StyleColor(new Color32(88, 166, 255, 255));
            equipmentCard.Add(_equipLabel);
            _content.Add(equipmentCard);

            // 물약 잔량과 분사/재장전 타이머는 보조 카드에 묶는다.
            var statusCard = new VisualElement();
            statusCard.name = "GasStatusCard";
            statusCard.style.flexDirection = FlexDirection.Column;
            statusCard.style.backgroundColor = new StyleColor(new Color32(33, 38, 45, 255)); // #21262D
            statusCard.style.paddingLeft = 12f;
            statusCard.style.paddingRight = 12f;
            statusCard.style.paddingTop = 7f;
            statusCard.style.paddingBottom = 7f;
            statusCard.style.borderTopLeftRadius = 7f;
            statusCard.style.borderTopRightRadius = 7f;
            statusCard.style.borderBottomLeftRadius = 7f;
            statusCard.style.borderBottomRightRadius = 7f;

            _potionLabel = MakeLabel(new Color(0.8f, 0.7f, 0.3f), true);
            _potionLabel.style.fontSize = 13.2f;
            statusCard.Add(_potionLabel);

            _timerLabel = MakeLabel(new Color32(240, 246, 252, 255), true);
            _timerLabel.style.fontSize = 13.2f;
            statusCard.Add(_timerLabel);

            // ── 진행바 ──
            _barBg = new VisualElement();
            _barBg.name = "GasProgressTrack";
            _barBg.style.height = 10f;
            _barBg.style.marginTop = 5f;
            _barBg.style.marginBottom = 4f;
            _barBg.style.backgroundColor = new StyleColor(new Color32(11, 14, 20, 255)); // #0B0E14
            _barBg.style.borderTopLeftRadius = 5f;
            _barBg.style.borderTopRightRadius = 5f;
            _barBg.style.borderBottomLeftRadius = 5f;
            _barBg.style.borderBottomRightRadius = 5f;
            statusCard.Add(_barBg);

            _barFill = new VisualElement();
            _barFill.style.height = 10f;
            _barFill.style.width = 0f;
            _barFill.style.backgroundColor = new StyleColor(new Color(0.3f, 0.8f, 0.3f));
            _barFill.style.borderTopLeftRadius = 5f;
            _barFill.style.borderTopRightRadius = 5f;
            _barFill.style.borderBottomLeftRadius = 5f;
            _barFill.style.borderBottomRightRadius = 5f;
            _barBg.Add(_barFill);

            _typeLabel = MakeLabel(new Color32(139, 148, 158, 255), false);
            _typeLabel.style.fontSize = 12f;
            _typeLabel.style.whiteSpace = WhiteSpace.Normal;
            statusCard.Add(_typeLabel);

            // Continuous active-dose status, separate from canister fuel/reload bar.
            _doseLabel = MakeLabel(new Color32(240, 246, 252, 255), true);
            _doseLabel.name = "PotionDoseStatus";
            _doseLabel.style.fontSize = 12f;
            statusCard.Add(_doseLabel);
            _doseBarBg = new VisualElement { name = "PotionDoseTrack" };
            _doseBarBg.style.height = 7f;
            _doseBarBg.style.marginTop = 3f;
            _doseBarBg.style.backgroundColor = new StyleColor(new Color32(11, 14, 20, 255));
            _doseBarBg.style.borderTopLeftRadius = 4f;
            _doseBarBg.style.borderTopRightRadius = 4f;
            _doseBarBg.style.borderBottomLeftRadius = 4f;
            _doseBarBg.style.borderBottomRightRadius = 4f;
            _doseBarFill = new VisualElement { name = "PotionDoseFill" };
            _doseBarFill.style.height = 7f;
            _doseBarFill.style.width = 0f;
            _doseBarFill.style.backgroundColor = new StyleColor(new Color32(88, 166, 255, 255));
            _doseBarFill.style.borderTopLeftRadius = 4f;
            _doseBarFill.style.borderTopRightRadius = 4f;
            _doseBarFill.style.borderBottomLeftRadius = 4f;
            _doseBarFill.style.borderBottomRightRadius = 4f;
            _doseBarBg.Add(_doseBarFill);
            statusCard.Add(_doseBarBg);
            _content.Add(statusCard);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 40f;
            style.bottom = 200f;

            _refreshTask = schedule.Execute(() =>
            {
                var controller = GasSprayerController.Instance;
                if (controller != null && controller.IsSpraying)
                {
                    if (!_sprayStatusMode) ShowSprayStatus();
                    Refresh();
                }
                else if (_sprayStatusMode)
                {
                    HideSprayStatus();
                }
                else if (IsOpen && !_doseExhaustedStatusMode) Refresh();
            }).Every(RefreshMs);
        }

        // =================== 생명주기 ===================

        /// <summary>Enable spray-status mode: it owns visibility and disappears on stop.</summary>
        public void ShowSprayStatus()
        {
            Ensure();
            _instance._doseExhaustedStatusMode = false;
            _instance._doseExhaustedGeneration++;
            _instance._sprayStatusMode = true;
            if (!_instance.IsOpen) _instance.Show();
            _instance.Refresh();
        }

        /// <summary>Show an accessible, short-lived notice only when the active dose is exhausted.</summary>
        public void ShowDoseExhaustedStatus()
        {
            Ensure();
            var instance = _instance;
            instance._sprayStatusMode = false;
            instance._doseExhaustedStatusMode = true;
            long generation = ++instance._doseExhaustedGeneration;
            if (!instance.IsOpen) instance.Show();
            instance.Refresh();
            instance._doseLabel.text = "🧪 Dose exhausted — reload a dose to continue";
            instance._doseLabel.tooltip = "The loaded dose and matching inventory stock are empty.";
            instance._doseLabel.name = "PotionDoseStatus";
            instance._doseLabel.EnableInClassList("gas-dose-exhausted", true);
            instance.schedule.Execute(() =>
            {
                if (instance == null || instance._doseExhaustedGeneration != generation) return;
                instance._doseExhaustedStatusMode = false;
                instance._doseLabel.EnableInClassList("gas-dose-exhausted", false);
                if (!instance._sprayStatusMode && instance.IsOpen) instance.Hide();
            }).StartingIn(1800);
        }

        public void HideSprayStatus()
        {
            if (_instance == null) return;
            _instance._sprayStatusMode = false;
            _instance._doseExhaustedStatusMode = false;
            _instance._doseExhaustedGeneration++;
            _instance._doseLabel.EnableInClassList("gas-dose-exhausted", false);
            if (_instance.IsOpen) _instance.Hide();
        }

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
                _doseLabel.text = "";
                _doseBarFill.style.width = 0f;
                return;
            }

            if (!ctrl.IsEquipped)
            {
                _equipLabel.text = "⚠️ 분사기 미장착";
                _potionLabel.text = "⚠️ 물약 없음";
                _timerLabel.text = "💨 준비됨";
                _typeLabel.text = "";
                _barFill.style.width = 0f;
                _doseLabel.text = "";
                _doseBarFill.style.width = 0f;
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

            float doseDuration = ctrl.PotionDoseDuration;
            float doseRemaining = Mathf.Clamp(ctrl.PotionDoseTimeRemaining, 0f, doseDuration);
            float doseRatio = hasPotion && doseDuration > 0f ? Mathf.Clamp01(doseRemaining / doseDuration) : 0f;
            _doseLabel.text = hasPotion
                ? "🧪 현재 1회분 " + doseRemaining.ToString("F1") + "s / " + doseDuration.ToString("F1") + "s"
                : "🧪 활성 1회분 없음";
            _doseBarFill.style.width = (_doseBarBg.resolvedStyle.width > 0f)
                ? _doseBarBg.resolvedStyle.width * doseRatio
                : 0f;
        }

        // =================== 헬퍼 ===================

        private static Label MakeLabel(Color color, bool bold)
        {
            var l = new Label("");
            l.style.fontSize = 14.4f;
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