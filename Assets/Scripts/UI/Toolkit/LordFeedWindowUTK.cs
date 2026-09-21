using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P30-E: 영주 음식주기 창 (UTK) — 문이 열린 영지의 영주 Placeholder(E키)에서 호출.
    ///
    /// [구현]
    ///  ① 진입 — LordFeedTarget.OnFeedRequested(territoryKey, lordName) 이벤트 구독.
    ///  ② 게이트 — TerritoryLordDoorSystem.IsLordDoorOpen false면 안내 문구만 표시.
    ///  ③ 목록 — 인벤 Food 카테고리 아이템 → "주기" → TerritoryLordDoorSystem.TryFeedLord
    ///     (아이템 제거 + 영지 loyaltyToPlayer +5).
    ///  ④ 정보 — 영주 중독도(=영지 오염도, TerritoryDrugSystem.GetLordAddiction) 표시.
    ///  순수 VisualElement 트리, IMGUI 없음.
    /// </summary>
    public class LordFeedWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static LordFeedWindowUTK _instance;
        public static LordFeedWindowUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new LordFeedWindowUTK();
            LordFeedTarget.OnFeedRequested += HandleFeedRequested;   // 구독 1회 (싱글턴 수명)
        }

        private static void HandleFeedRequested(string territoryKey, string lordName)
        {
            OpenFor(territoryKey, lordName);
        }

        /// <summary>영주 음식주기 창 열기 (진입점).</summary>
        public static void OpenFor(string territoryKey, string lordName)
        {
            Ensure();
            _instance.BeginFeed(territoryKey, lordName);
        }

        // ===== 설정 =====
        private const float WinW = 500f;
        private const float WinH = 380f;

        // ===== 상태 =====
        private string _territoryKey = "";
        private string _lordName = "영주";

        // ===== 레퍼런스 =====
        private readonly VisualElement _list;
        private readonly Label _summaryLabel;
        private readonly Label _infoLabel;
        private readonly Label _statusLabel;

        private LordFeedWindowUTK() : base("🍗 영주 음식주기", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _summaryLabel = new Label("🍗 영주 음식주기");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 17f;
            _content.Add(_summaryLabel);

            _infoLabel = MakeLabel("", UTKColor.TextSecondary);
            _infoLabel.style.marginTop = 4f;
            _content.Add(_infoLabel);

            _list = new VisualElement();
            _list.name = "LordFeedList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            _statusLabel = MakeLabel("", UTKColor.TextPrimary);
            _statusLabel.style.marginTop = 4f;
            _content.Add(_statusLabel);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 380f;
            style.top = 160f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 380f;
            style.top = 160f;
            Debug.Log("[LordFeedUTK] 영주 음식주기 창 열림");
        }

        // =================== 렌더링 ===================

        private void BeginFeed(string territoryKey, string lordName)
        {
            _territoryKey = territoryKey ?? string.Empty;
            _lordName = string.IsNullOrEmpty(lordName) ? "영주" : lordName;
            _statusLabel.text = "";
            Show();
            RefreshFeedList();
        }

        private void RefreshFeedList()
        {
            _list.Clear();
            _summaryLabel.text = "🍗 " + _lordName + "에게 음식주기";

            float lordAddiction = TerritoryDrugSystem.GetLordAddiction(_territoryKey);
            bool doorOpen = TerritoryLordDoorSystem.IsLordDoorOpen(_territoryKey);
            _infoLabel.text = "영지: " + _territoryKey +
                              " · 영주 중독도(=영지 오염도): " + lordAddiction.ToString("F1") +
                              " · 문: " + (doorOpen ? "열림" : "잠김");

            if (!doorOpen)
            {
                _list.Add(MakeLabel("(영주실 문이 잠겨 있어 음식을 줄 수 없습니다 — 영주 중독도 " +
                                    TerritoryLordDoorSystem.DOOR_OPEN_ADDICTION_THRESHOLD.ToString("F0") +
                                    " 이상 필요)", UTKColor.TextSecondary));
                _list.Add(UTKButton.Create("닫기 ✕", Close, UTKButton.Variant.Danger));
                return;
            }

            var slots = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetSlotsByCategory(PlayerInventory.ItemCategory.Food)
                : null;

            bool any = false;
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot == null || slot.item == null || slot.count <= 0) continue;
                    any = true;
                    _list.Add(BuildFeedRow(slot));
                }
            }

            if (!any)
                _list.Add(MakeLabel("(인벤토리에 음식(Food)이 없습니다.)", UTKColor.TextSecondary));

            _list.Add(UTKButton.Create("닫기 ✕", Close, UTKButton.Variant.Danger));
        }

        private VisualElement BuildFeedRow(PlayerInventory.ItemSlot slot)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;

            var name = MakeLabel("🍗 " + slot.item.displayName + " x" + slot.count, UTKColor.TextPrimary);
            name.style.flexGrow = 1f;
            row.Add(name);

            row.Add(UTKButton.Create("주기", () => FeedSlot(slot), UTKButton.Variant.Primary));

            ApplyUIToolkitFont(row);
            return row;
        }

        private void FeedSlot(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null) return;

            bool ok = TerritoryLordDoorSystem.TryFeedLord(_territoryKey, slot.item.id);
            if (ok)
            {
                _statusLabel.text = slot.item.displayName + " 지급 완료 — 영지 충성도 +5";
                Debug.Log($"[LordFeedUTK] 🍗 {_lordName}에게 {slot.item.displayName} 지급 → {_territoryKey} 충성도 +5");
            }
            else
            {
                _statusLabel.text = "지급 실패 (문 잠김 또는 아이템 소모 불가)";
            }
            RefreshFeedList();
        }

        private static Label MakeLabel(string text, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = 13f;
            label.style.color = new StyleColor(color);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
