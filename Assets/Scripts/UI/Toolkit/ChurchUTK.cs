// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 108,114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round D2 — 성당/교회 시스템 UI (UTK).
    /// 원본 2개 통합 — Assets/Scripts/UI/ChurchSystemUI.cs (124줄) + Assets/Scripts/UI/ChurchNPCInteraction.cs (131줄).
    /// 두 원본 모두 E키 근접 상호작용으로 ChurchUI(기부 메뉴)를 여는 브릿지이므로, 기부 기능 버튼을 통합 포팅한다.
    /// 원본 파일은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 친밀도 — ChurchSystem.Instance.GetFavor/MaxFavor 실측 + GetFavorLevelText/GetFavorBenefitsText.
    ///  ② 기부 버튼 — DonateGold(10/50/100) 실측 호출 (각 +1/+5/+10 친밀도, 골드 차감).
    ///  ③ 영주 대면 — CanRequestAudience() 실측 (80+ 활성).
    ///  ④ 상태 메시지 — 기부 성공/실패 3초 표시 (400ms 폴링 타이머).
    ///  ⑤ 진입 로그 — [ChurchUTK] UnityEngine.Debug 각 상호작용 별도 기록.
    /// [진입점] static Open(npcName) / Toggle(). UTKWindowBase 상속 + static Ensure.
    /// </summary>
    public class ChurchUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static ChurchUTK _instance;
        public static ChurchUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new ChurchUTK();
        }

        /// <summary>성당 UI 열기. ChurchSystemUI/ChurchNPCInteraction의 E키 상호작용 대응.</summary>
        public static void Open(string npcName = "성당 관리인")
        {
            Ensure();
            _instance.BeginOpen(npcName);
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 =====
        private const float WinW = 460f;
        private const float WinH = 570f;
        private const long RefreshMs = 400L;
        private const float StatusDisplaySec = 3f;

        // ===== 상태 =====
        private string _npcName = "성당 관리인";
        private string _statusMessage = "";
        private float _statusTimer;

        // ===== 레퍼런스 =====
        private readonly Label _titleLabel;
        private readonly Label _favorLabel;
        private readonly VisualElement _favorBarBg;
        private readonly VisualElement _favorBarFill;
        private readonly Label _levelLabel;
        private readonly Label _benefitsLabel;
        private readonly Label _statusLabel;
        private readonly Button _audienceBtn;
        private readonly Label _goldLabel;
        private readonly IVisualElementScheduledItem _refreshTask;

        private ChurchUTK() : base("⛪ 성당", new Vector2(WinW, WinH))
        {
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.flexGrow = 1f;
            _content.style.paddingLeft = 12f;
            _content.style.paddingRight = 12f;
            _content.style.paddingTop = 8f;
            _content.style.paddingBottom = 8f;
            _content.style.backgroundColor = new StyleColor(UTKColor.BgPanelDark);

            var titleBar = this.Q<VisualElement>("TitleBar");
            if (titleBar != null)
            {
                titleBar.style.backgroundColor = new StyleColor(UTKColor.BgPanel);
                titleBar.style.borderBottomWidth = 1f;
                titleBar.style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
            }

            // 친밀도/레벨 핵심 정보를 골드 악센트가 있는 헤더 카드에 모은다.
            var favorCard = MakeCard("FavorHeaderCard", UTKColor.BgPanel);
            favorCard.style.borderLeftWidth = 3f;
            favorCard.style.borderLeftColor = new StyleColor(UTKColor.AccentRare);
            _titleLabel = new Label("⛪ 성당 — 기부 메뉴");
            _titleLabel.style.fontSize = 15f;
            _titleLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            favorCard.Add(_titleLabel);

            _favorLabel = new Label("친밀도: 0/100");
            _favorLabel.style.fontSize = 18f;
            _favorLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _favorLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            _favorLabel.style.marginTop = 5f;
            favorCard.Add(_favorLabel);

            _favorBarBg = new VisualElement();
            _favorBarBg.name = "FavorBarBackground";
            _favorBarBg.style.height = 12f;
            _favorBarBg.style.marginTop = 8f;
            _favorBarBg.style.marginBottom = 5f;
            _favorBarBg.style.backgroundColor = new StyleColor(UTKColor.BgPanelDark);
            _favorBarBg.style.borderTopLeftRadius = 6f;
            _favorBarBg.style.borderTopRightRadius = 6f;
            _favorBarBg.style.borderBottomLeftRadius = 6f;
            _favorBarBg.style.borderBottomRightRadius = 6f;
            favorCard.Add(_favorBarBg);

            _favorBarFill = new VisualElement();
            _favorBarFill.name = "FavorBarFill";
            _favorBarFill.style.height = 12f;
            _favorBarFill.style.backgroundColor = new StyleColor(UTKColor.AccentRare);
            _favorBarFill.style.borderTopLeftRadius = 6f;
            _favorBarFill.style.borderTopRightRadius = 6f;
            _favorBarFill.style.borderBottomLeftRadius = 6f;
            _favorBarFill.style.borderBottomRightRadius = 6f;
            _favorBarBg.Add(_favorBarFill);

            _levelLabel = new Label("");
            _levelLabel.style.fontSize = 14f;
            _levelLabel.style.color = new StyleColor(UTKColor.AccentRare);
            _levelLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            favorCard.Add(_levelLabel);
            _content.Add(favorCard);

            // 혜택은 보조 카드로 분리해 정보 위계를 명확히 한다.
            var benefitsCard = MakeCard("BenefitsCard", new Color32(33, 38, 45, 255));
            var benefitsTitle = new Label("현재 친밀도 혜택");
            benefitsTitle.style.fontSize = 13f;
            benefitsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            benefitsTitle.style.color = new StyleColor(UTKColor.TextPrimary);
            benefitsCard.Add(benefitsTitle);

            _benefitsLabel = new Label("");
            _benefitsLabel.style.fontSize = 12f;
            _benefitsLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _benefitsLabel.style.whiteSpace = WhiteSpace.Normal;
            _benefitsLabel.style.marginTop = 5f;
            benefitsCard.Add(_benefitsLabel);
            _content.Add(benefitsCard);

            // 하단 액션 띠: 헌금 버튼과 영주 대면 요청을 함께 배치한다.
            var actionStrip = MakeCard("ChurchActionStrip", UTKColor.BgPanel);
            actionStrip.style.marginTop = 2f;
            var donateTitle = new Label("금화 헌금");
            donateTitle.style.fontSize = 13f;
            donateTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            donateTitle.style.color = new StyleColor(UTKColor.TextPrimary);
            actionStrip.Add(donateTitle);

            var donateRow = new VisualElement();
            donateRow.style.flexDirection = FlexDirection.Row;
            donateRow.style.marginTop = 5f;
            donateRow.Add(MakeDonateButton("10G\n+1", 10));
            donateRow.Add(MakeDonateButton("50G\n+5", 50));
            donateRow.Add(MakeDonateButton("100G\n+10", 100));
            actionStrip.Add(donateRow);

            _goldLabel = new Label("");
            _goldLabel.style.fontSize = 12f;
            _goldLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _goldLabel.style.marginTop = 4f;
            actionStrip.Add(_goldLabel);

            _audienceBtn = UTKButton.Create("👑 영주 대면 요청 (친밀도 80+)", OnRequestAudience, UTKButton.Variant.Secondary);
            _audienceBtn.style.marginTop = 7f;
            actionStrip.Add(_audienceBtn);
            _content.Add(actionStrip);

            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 13f;
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.marginTop = 6f;
            _statusLabel.style.display = DisplayStyle.None;
            _content.Add(_statusLabel);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 690f;
            style.top = 220f;

            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                TickStatus();
                Refresh();
            }).Every(RefreshMs);
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            if (ChurchSystem.Instance != null)
                UnityEngine.Debug.Log("[ChurchUTK] 성당 UI 열림 (친밀도: " + ChurchSystem.Instance.GetFavor() + "/" + ChurchSystem.Instance.MaxFavor + ")");
            Refresh();
        }

        public override void Hide()
        {
            base.Hide();
            UnityEngine.Debug.Log("[ChurchUTK] 성당 UI 닫힘");
        }

        private void BeginOpen(string npcName)
        {
            _npcName = npcName ?? "성당 관리인";
            _statusMessage = "";
            _statusTimer = 0f;
            _statusLabel.style.display = DisplayStyle.None;
            Show();
        }

        // =================== 갱신 ===================

        private void TickStatus()
        {
            if (_statusLabel.style.display == DisplayStyle.None) return;
            _statusTimer -= RefreshMs / 1000f;
            if (_statusTimer <= 0f)
            {
                _statusMessage = "";
                _statusLabel.style.display = DisplayStyle.None;
            }
        }

        private void Refresh()
        {
            var system = ChurchSystem.Instance;
            if (system == null)
            {
                _favorLabel.text = "ChurchSystem이 없습니다.";
                _audienceBtn.SetEnabled(false);
                return;
            }

            int favor = system.GetFavor();
            int maxFavor = system.MaxFavor;
            _titleLabel.text = "⛪ " + system.ChurchName + " — " + _npcName;
            _favorLabel.text = "친밀도: " + favor + "/" + maxFavor;

            float pct = maxFavor > 0f ? Mathf.Clamp01(favor / (float)maxFavor) : 0f;
            if (_favorBarBg.resolvedStyle.width > 0f)
                _favorBarFill.style.width = _favorBarBg.resolvedStyle.width * pct;
            _favorBarFill.style.backgroundColor = new StyleColor(UTKColor.AccentRare);

            _levelLabel.text = system.GetFavorLevelText();
            _benefitsLabel.text = system.GetFavorBenefitsText();
            _audienceBtn.SetEnabled(system.CanRequestAudience());
            _audienceBtn.text = "👑 영주 대면 요청" + (system.CanRequestAudience() ? " (가능)" : " (친밀도 80 필요)");
            _goldLabel.text = "💰 보유 골드: " + GetPlayerGold() + "G";
        }

        // =================== 기부 / 대면 ===================

        private Button MakeDonateButton(string label, int amount)
        {
            return UTKButton.Create(label, () => OnDonate(amount), UTKButton.Variant.Primary);
        }

        private void OnDonate(int amount)
        {
            var system = ChurchSystem.Instance;
            if (system == null)
            {
                SetStatus("❌ ChurchSystem이 없습니다.", new Color(1f, 0.6f, 0.4f));
                return;
            }

            int spent = system.DonateGold(amount);
            if (spent > 0)
            {
                SetStatus("✅ " + spent + "골드 기부 완료! 친밀도 +" + (spent / 10), new Color(0.6f, 1f, 0.6f));
                UnityEngine.Debug.Log("[ChurchUTK] 기부 완료 — " + spent + "G, 친밀도 " + system.GetFavor() + " (NPC: " + _npcName + ")");
            }
            else
            {
                SetStatus("❌ 골드가 부족합니다.", new Color(1f, 0.6f, 0.4f));
                UnityEngine.Debug.LogWarning("[ChurchUTK] 기부 실패 — 골드 부족 (" + amount + "G 요청, 보유 " + GetPlayerGold() + "G)");
            }
            Refresh();
        }

        private void OnRequestAudience()
        {
            var system = ChurchSystem.Instance;
            if (system == null || !system.CanRequestAudience())
            {
                SetStatus("❌ 영주 대면은 친밀도 80 이상 필요합니다.", new Color(1f, 0.6f, 0.4f));
                return;
            }
            SetStatus("✅ 영주 대면 요청 접수! (Phase 5.7.5 대면 UI 대기)", new Color(1f, 0.95f, 0.5f));
            UnityEngine.Debug.Log("[ChurchUTK] 영주 대면 요청 (친밀도 " + system.GetFavor() + ")");
        }

        private void SetStatus(string msg, Color color)
        {
            _statusMessage = msg;
            _statusTimer = StatusDisplaySec;
            _statusLabel.text = msg;
            _statusLabel.style.color = new StyleColor(color);
            _statusLabel.style.display = DisplayStyle.Flex;
        }

        // =================== 헬퍼 ===================

        private static VisualElement MakeCard(string elementName, Color background)
        {
            var card = new VisualElement { name = elementName };
            card.style.flexDirection = FlexDirection.Column;
            card.style.backgroundColor = new StyleColor(background);
            card.style.borderLeftWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderTopWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftColor = new StyleColor(UTKColor.BorderBronze);
            card.style.borderRightColor = new StyleColor(UTKColor.BorderBronze);
            card.style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
            card.style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
            card.style.borderTopLeftRadius = 8f;
            card.style.borderTopRightRadius = 8f;
            card.style.borderBottomLeftRadius = 8f;
            card.style.borderBottomRightRadius = 8f;
            card.style.paddingLeft = 10f;
            card.style.paddingRight = 10f;
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 8f;
            card.style.marginBottom = 7f;
            return card;
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