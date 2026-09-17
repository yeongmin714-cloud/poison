using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round D2 — 깃발/국기 등록 창 (UTK).
    /// 원본: Assets/Scripts/UI/PlayerFlagRegistrationWindow.cs (412줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 이름 입력 — TextField (최대 8자, 원본 8자 제한 동일).
    ///  ② 배경색 선택 — EmblemColor 8종 버튼 그리드 (EmblemManager.GetEmblemColor 실측 색상 표시).
    ///  ③ 문양 선택 — EmblemShape 10종 버튼 (EmblemManager.GetEmblemSymbol 실측 심볼 표시).
    ///  ④ 국기 미리보기 — 배경색 + 문양 심볼 + 이름 (클래스 드로잉).
    ///  ⑤ 완료 — EmblemManager.ChangeEmblem(newEmblem, playerGold) 실측 호출, 골드 차감·성공 로그.
    ///  각 경로에 [FlagUTK] UnityEngine.Debug 로그. 400ms 폴링으로 보유 골드/비용 표시 갱신.
    /// [진입점] static Open() / OpenCreate() / Toggle(). UTKWindowBase 상속 + static Ensure.
    /// </summary>
    public class PlayerFlagUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static PlayerFlagUTK _instance;
        public static PlayerFlagUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new PlayerFlagUTK();
        }

        /// <summary>깃발 등록 창 열기 (원본 OnShow 대응 — 현재 문장을 편집 기본값으로 로드).</summary>
        public static void Open()
        {
            if (EmblemManager.Instance == null)
            {
                UnityEngine.Debug.LogWarning("[FlagUTK] EmblemManager.Instance가 없어 깃발 창을 열 수 없습니다.");
                return;
            }
            Ensure();
            _instance.BeginOpen();
        }

        /// <summary>새 국기 등록 (기본 문장 Shield/Gold로 시작).</summary>
        public static void OpenCreate()
        {
            Ensure();
            _instance.BeginCreate();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 =====
        private const float WinW = 520f;
        private const float WinH = 660f;
        private const long RefreshMs = 400L;
        private const int MaxNameLength = 8;

        private static readonly EmblemColor[] AllColors = (EmblemColor[])System.Enum.GetValues(typeof(EmblemColor));
        private static readonly EmblemShape[] AllShapes = (EmblemShape[])System.Enum.GetValues(typeof(EmblemShape));

        // ===== 편집 상태 =====
        private string _editName = "";
        private EmblemColor _editPrimaryColor = EmblemColor.Gold;
        private EmblemShape _editShape = EmblemShape.Shield;
        private string _message = "";
        private bool _showMessage;

        // ===== 레퍼런스 =====
        private readonly VisualElement _previewBox;
        private readonly Label _previewSymbol;
        private readonly Label _previewName;
        private readonly TextField _nameField;
        private readonly VisualElement _colorGrid;
        private readonly VisualElement _shapeGrid;
        private readonly Label _costLabel;
        private readonly Label _messageLabel;
        private readonly Button _confirmBtn;
        private readonly IVisualElementScheduledItem _refreshTask;

        private PlayerFlagUTK() : base("🏳️ 내 영지의 국기", new Vector2(WinW, WinH))
        {
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.flexGrow = 1f;

            // ── 국기 미리보기 ──
            _previewBox = new VisualElement();
            _previewBox.style.height = 130f;
            _previewBox.style.marginBottom = 10f;
            _previewBox.style.alignItems = Align.Center;
            _previewBox.style.justifyContent = Justify.Center;
            _previewBox.style.flexDirection = FlexDirection.Column;
            _content.Add(_previewBox);

            _previewSymbol = new Label("🛡️");
            _previewSymbol.style.fontSize = 44f;
            _previewSymbol.style.color = Color.white;
            _previewSymbol.style.marginBottom = 4f;
            _content.Add(_previewSymbol);

            _previewName = new Label("이름 없음");
            _previewName.style.fontSize = 18f;
            _previewName.style.color = Color.white;
            _previewName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _content.Add(_previewName);

            // ── 이름 입력 ──
            _content.Add(MakeSectionLabel("📛 이름 (최대 8자)"));
            _nameField = new TextField { maxLength = MaxNameLength, value = "" };
            _nameField.style.marginBottom = 6f;
            _nameField.RegisterValueChangedCallback(evt =>
            {
                _editName = evt.newValue ?? "";
                RefreshPreview();
            });
            _content.Add(_nameField);

            // ── 배경색 선택 ──
            _content.Add(MakeSectionLabel("🎨 배경색"));
            _colorGrid = new VisualElement();
            _colorGrid.style.flexDirection = FlexDirection.Row;
            _colorGrid.style.flexWrap = Wrap.Wrap;
            _colorGrid.style.marginBottom = 8f;
            _content.Add(_colorGrid);
            BuildColorGrid();

            // ── 문양 선택 ──
            _content.Add(MakeSectionLabel("🔰 문양"));
            _shapeGrid = new VisualElement();
            _shapeGrid.style.flexDirection = FlexDirection.Row;
            _shapeGrid.style.flexWrap = Wrap.Wrap;
            _shapeGrid.style.marginBottom = 8f;
            _content.Add(_shapeGrid);
            BuildShapeGrid();

            // ── 비용 정보 ──
            _costLabel = new Label("");
            _costLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _costLabel.style.fontSize = 14f;
            _costLabel.style.marginTop = 4f;
            _costLabel.style.marginBottom = 4f;
            _content.Add(_costLabel);

            // ── 메시지 ──
            _messageLabel = new Label("");
            _messageLabel.style.color = new StyleColor(new Color(0.6f, 1f, 0.6f));
            _messageLabel.style.fontSize = 13f;
            _messageLabel.style.whiteSpace = WhiteSpace.Normal;
            _messageLabel.style.marginTop = 4f;
            _messageLabel.style.marginBottom = 4f;
            _messageLabel.style.display = DisplayStyle.None;
            _content.Add(_messageLabel);

            // ── 완료 버튼 ──
            _confirmBtn = UTKButton.Create("✅ 완료 (등록)", OnConfirm, UTKButton.Variant.Primary);
            _confirmBtn.style.marginTop = 8f;
            _content.Add(_confirmBtn);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 640f;
            style.top = 180f;

            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshCost();
            }).Every(RefreshMs);
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            RefreshCost();
            UnityEngine.Debug.Log("[FlagUTK] 깃발 등록 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            UnityEngine.Debug.Log("[FlagUTK] 깃발 등록 창 닫힘");
        }

        private void BeginOpen()
        {
            var cur = EmblemManager.Instance.CurrentEmblem;
            _editName = cur.emblemName;
            _editPrimaryColor = cur.primaryColor;
            _editShape = cur.shape;
            ResetMessage();
            ApplyEdits();
            Show();
        }

        private void BeginCreate()
        {
            _editName = "";
            _editPrimaryColor = EmblemColor.Gold;
            _editShape = EmblemShape.Shield;
            ResetMessage();
            ApplyEdits();
            Show();
        }

        private void ApplyEdits()
        {
            if (_nameField != null) _nameField.value = _editName ?? "";
            RefreshPreview();
        }

        // =================== 렌더링 ===================

        private void BuildColorGrid()
        {
            foreach (var color in AllColors)
            {
                var btn = new Button(() =>
                {
                    _editPrimaryColor = color;
                    ResetMessage();
                    RefreshPreview();
                    UnityEngine.Debug.Log("[FlagUTK] 배경색 선택: " + color);
                });
                btn.text = " ";
                btn.style.width = 44f;
                btn.style.height = 40f;
                btn.style.marginLeft = 2f;
                btn.style.marginRight = 2f;
                var colStyle = btn.style;
                colStyle.backgroundColor = new StyleColor(EmblemManager.GetEmblemColor(color));
                btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.opacity = 0.8f);
                btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.opacity = 1f);
                _colorGrid.Add(btn);
            }
        }

        private void BuildShapeGrid()
        {
            foreach (var shape in AllShapes)
            {
                var btn = new Button(() =>
                {
                    _editShape = shape;
                    ResetMessage();
                    RefreshPreview();
                    UnityEngine.Debug.Log("[FlagUTK] 문양 선택: " + shape);
                });
                btn.text = EmblemManager.GetEmblemSymbol(shape);
                btn.style.width = 48f;
                btn.style.height = 44f;
                btn.style.marginLeft = 2f;
                btn.style.marginRight = 2f;
                _shapeGrid.Add(btn);
            }
        }

        private void RefreshPreview()
        {
            string displayName = string.IsNullOrEmpty(_editName) ? "이름 없음" : _editName;
            _previewName.text = displayName;
            _previewSymbol.text = EmblemManager.GetEmblemSymbol(_editShape);
            _previewBox.style.backgroundColor = new StyleColor(EmblemManager.GetEmblemColor(_editPrimaryColor));
            _confirmBtn.SetEnabled(!string.IsNullOrEmpty(_editName));
        }

        private void RefreshCost()
        {
            if (EmblemManager.Instance == null) return;
            int cost = EmblemManager.Instance.ChangeCost;
            int gold = GetPlayerGold();
            _costLabel.text = $"💰 변경 비용: {cost}G (보유: {gold}G)";
        }

        private void SetMessage(string msg, bool ok)
        {
            _message = msg;
            _showMessage = true;
            _messageLabel.text = msg;
            _messageLabel.style.color = new StyleColor(ok ? new UnityEngine.Color(0.6f, 1f, 0.6f) : new UnityEngine.Color(1f, 0.6f, 0.4f));
            _messageLabel.style.display = DisplayStyle.Flex;
        }

        private void ResetMessage()
        {
            _message = "";
            _showMessage = false;
            _messageLabel.style.display = DisplayStyle.None;
        }

        private static Label MakeSectionLabel(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 14f;
            l.style.color = new StyleColor(UTKColor.TextPrimary);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 6f;
            return l;
        }

        // =================== 완료 처리 ===================

        private void OnConfirm()
        {
            if (EmblemManager.Instance == null)
            {
                SetMessage("문장 시스템을 사용할 수 없습니다.", false);
                return;
            }

            if (string.IsNullOrEmpty(_editName))
            {
                SetMessage("국기 이름을 입력해주세요.", false);
                return;
            }

            int gold = GetPlayerGold();
            var newEmblem = new PlayerEmblemData
            {
                emblemName = _editName.Trim(),
                shape = _editShape,
                primaryColor = _editPrimaryColor,
                secondaryColor = EmblemManager.Instance.CurrentEmblem.secondaryColor
            };

            bool success = EmblemManager.Instance.ChangeEmblem(newEmblem, gold);
            if (success)
            {
                UnityEngine.Debug.Log("[FlagUTK] 국기 '" + newEmblem.emblemName + "' 등록 완료! (shape=" + newEmblem.shape + ", color=" + newEmblem.primaryColor + ")");
                SetMessage("✅ 국기 '" + newEmblem.emblemName + "' 등록 완료!", true);
                Hide();
            }
            else
            {
                SetMessage("⚠️ 골드가 부족합니다. (필요: " + EmblemManager.Instance.ChangeCost + "G)", false);
                UnityEngine.Debug.LogWarning("[FlagUTK] 국기 등록 실패 — 골드 부족 (보유: " + gold + "G)");
            }
        }

        private static int GetPlayerGold()
        {
            if (PlayerStats.Instance != null)
                return PlayerStats.Instance.Gold;
            if (PlayerInventory.Instance != null)
                return PlayerInventory.Instance.GetItemCount("gold");
            return 0;
        }
    }
}