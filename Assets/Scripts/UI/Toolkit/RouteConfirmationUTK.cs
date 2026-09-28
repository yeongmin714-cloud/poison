using System;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round B-2b — 🗺️ 오토루트 확인 팝업 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/RouteConfirmationUI.cs (359줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 표시 — "[아이템명] — [영지명] (으)로 이동" + 잔여 시간.
    ///  ② 자동 닫힘 — 3초 카운트다운 후 자동 Close (100ms 폴링).
    ///  ③ [🚶 이동] — 영지 ID "Nation_Index" 파싱 → 좌표(index*10, 0, nation*10) 계산 →
    ///     AutoMoveManager.SetDestination 실측 호출 후 닫기 (원본과 동일한 좌표 산식).
    ///  ④ [취소] / ESC — Close.
    ///  100ms 폴링으로 잔여 시간 갱신. 각 경로에 [RouteUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open(itemName, territoryName, territoryId) / Ensure().
    /// </summary>
    public class RouteConfirmationUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static RouteConfirmationUTK _instance;
        public static RouteConfirmationUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new RouteConfirmationUTK();
        }

        /// <summary>오토루트 확인 팝업 표시.</summary>
        public static void Open(string itemName, string territoryName, string territoryId)
        {
            Ensure();
            _instance._itemName = itemName;
            _instance._territoryName = string.IsNullOrEmpty(territoryName) ? "알 수 없는 영지" : territoryName;
            _instance._territoryId = territoryId;
            _instance._openTime = Time.time;
            _instance.Show();
        }

        // ===== 설정 =====
        private const float WinW = 560f;
        private const float WinH = 260f;
        private const float AutoCloseTime = 3f;
        private const long TickMs = 100L;

        // ===== 상태 =====
        private string _itemName = "";
        private string _territoryName = "";
        private string _territoryId = "";
        private float _openTime = 0f;

        // ===== 레퍼런스 =====
        private Label _descLabel;
        private Label _timerLabel;
        private IVisualElementScheduledItem _tickTask;

        private RouteConfirmationUTK() : base("📍 오토루트", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.justifyContent = Justify.FlexStart;
            _content.style.paddingLeft = 16f;
            _content.style.paddingRight = 16f;
            _content.style.paddingTop = 14f;
            _content.style.paddingBottom = 12f;

            // 목적지 경로 헤더 카드
            var routeCard = new VisualElement();
            routeCard.style.flexDirection = FlexDirection.Column;
            routeCard.style.paddingLeft = 14f;
            routeCard.style.paddingRight = 14f;
            routeCard.style.paddingTop = 12f;
            routeCard.style.paddingBottom = 12f;
            routeCard.style.backgroundColor = new StyleColor(UTKColor.BgPanel);
            routeCard.style.borderTopWidth = 1f;
            routeCard.style.borderBottomWidth = 1f;
            routeCard.style.borderLeftWidth = 1f;
            routeCard.style.borderRightWidth = 1f;
            routeCard.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            routeCard.style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            routeCard.style.borderLeftColor = new StyleColor(UTKColor.IronLine);
            routeCard.style.borderRightColor = new StyleColor(UTKColor.IronLine);
            routeCard.style.borderTopLeftRadius = 8f;
            routeCard.style.borderTopRightRadius = 8f;
            routeCard.style.borderBottomLeftRadius = 8f;
            routeCard.style.borderBottomRightRadius = 8f;

            _descLabel = MakeLabel("", UTKColor.TextPrimary);
            _descLabel.style.fontSize = 16f;
            _descLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _descLabel.style.whiteSpace = WhiteSpace.Normal;
            routeCard.Add(_descLabel);
            _content.Add(routeCard);

            // 보조 카드: 자동 닫힘 타이머와 목적지 식별 정보
            var detailsCard = new VisualElement();
            detailsCard.style.flexDirection = FlexDirection.Column;
            detailsCard.style.marginTop = 10f;
            detailsCard.style.paddingLeft = 14f;
            detailsCard.style.paddingRight = 14f;
            detailsCard.style.paddingTop = 9f;
            detailsCard.style.paddingBottom = 9f;
            detailsCard.style.backgroundColor = new StyleColor(UTKColor.BgPanel);
            detailsCard.style.borderTopWidth = 1f;
            detailsCard.style.borderBottomWidth = 1f;
            detailsCard.style.borderLeftWidth = 1f;
            detailsCard.style.borderRightWidth = 1f;
            detailsCard.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            detailsCard.style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            detailsCard.style.borderLeftColor = new StyleColor(UTKColor.IronLine);
            detailsCard.style.borderRightColor = new StyleColor(UTKColor.IronLine);
            detailsCard.style.borderTopLeftRadius = 8f;
            detailsCard.style.borderTopRightRadius = 8f;
            detailsCard.style.borderBottomLeftRadius = 8f;
            detailsCard.style.borderBottomRightRadius = 8f;

            _timerLabel = MakeLabel("", UTKColor.TextSecondary);
            _timerLabel.style.fontSize = 12f;
            detailsCard.Add(_timerLabel);
            var routeInfoLabel = MakeLabel("목적지 경로 정보", UTKColor.TextSecondary);
            routeInfoLabel.style.fontSize = 11f;
            routeInfoLabel.style.marginTop = 3f;
            detailsCard.Add(routeInfoLabel);
            _content.Add(detailsCard);

            // 하단 액션 띠 — 상단 골드 구분선, 주요 이동/보조 취소
            var actionSpacer = new VisualElement();
            actionSpacer.style.flexGrow = 1f;
            _content.Add(actionSpacer);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            btnRow.style.alignItems = Align.Center;
            btnRow.style.paddingTop = 10f;
            btnRow.style.borderTopWidth = 1f;
            btnRow.style.borderTopColor = new StyleColor(UTKColor.BorderGold);
            btnRow.Add(UTKButton.Create("🚶 이동", ExecuteAutoMoveAndClose, UTKButton.Variant.Primary));
            btnRow.Add(UTKButton.Create("취소", () => Close(), UTKButton.Variant.Secondary));
            _content.Add(btnRow);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 340f;
            style.top = 300f;
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 340f;
            style.top = 300f;
            UpdateText();
            StartTicking();
            Debug.Log($"[RouteUTK] 📍 팝업 표시: {_itemName} → {_territoryName}");
        }

        public override void Hide()
        {
            base.Hide();
            StopTicking();
            _itemName = "";
            _territoryName = "";
            _territoryId = "";
            Debug.Log("[RouteUTK] 팝업 닫힘");
        }

        // ===== 카운트다운 (100ms 폴링) =====

        private void StartTicking()
        {
            if (_tickTask != null) return;
            _tickTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                float remaining = AutoCloseTime - (Time.time - _openTime);
                if (remaining <= 0f)
                {
                    Close();
                    return;
                }
                _timerLabel.text = $"⏱️ {remaining:F1}초 후 자동 닫힘";
            }).Every(TickMs);
        }

        private void StopTicking()
        {
            if (_tickTask != null)
            {
                _tickTask.Pause();
                _tickTask = null;
            }
        }

        private void UpdateText()
        {
            _descLabel.text = $"📍 {_itemName} — {_territoryName} (으)로 이동";
            _timerLabel.text = $"⏱️ {AutoCloseTime:F1}초 후 자동 닫힘";
        }

        // ===== [🚶 이동] 실행 =====

        /// <summary>이동 + 닫기 (원본 ExecuteAutoMove + Close).</summary>
        private void ExecuteAutoMoveAndClose()
        {
            if (string.IsNullOrEmpty(_territoryId))
            {
                Debug.LogWarning("[RouteUTK] 영지 ID가 없어 이동할 수 없습니다.");
                Close();
                return;
            }

            // "Nation_Index" 파싱 (원본과 동일)
            string[] parts = _territoryId.Split('_');
            if (parts.Length != 2)
            {
                Debug.LogWarning($"[RouteUTK] 영지 ID 형식 오류: {_territoryId}");
                Close();
                return;
            }
            if (!Enum.TryParse<NationType>(parts[0], out var nation))
            {
                Debug.LogWarning($"[RouteUTK] 국가 파싱 실패: {parts[0]}");
                Close();
                return;
            }
            if (!int.TryParse(parts[1], out int index))
            {
                Debug.LogWarning($"[RouteUTK] 영지 인덱스 파싱 실패: {parts[1]}");
                Close();
                return;
            }
            if (AutoMoveManager.Instance == null)
            {
                Debug.LogError("[RouteUTK] AutoMoveManager 인스턴스가 없습니다!");
                Close();
                return;
            }

            // 원본과 동일한 세계 좌표 산식
            Vector3 worldPos = new Vector3(index * 10f, 0f, (int)nation * 10f);
            AutoMoveManager.Instance.SetDestination(worldPos);
            Debug.Log($"[RouteUTK] 📍 {_territoryName} (으)로 자동 이동합니다 (worldPos={worldPos})");
            Close();
        }

        // ===== 헬퍼 =====

        private static Label MakeLabel(string text, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = 14f;
            l.style.color = new StyleColor(color);
            return l;
        }
    }
}