using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// Figma 173:4 (288x432) general potion inventory/status panel.
    /// This window has a public Open() entry point only; no gameplay caller or hotkey is
    /// currently wired. It is deliberately separate from the I-key inventory and gas dose UI.
    /// Inventory exposes slot occupancy, not item weight, so the gauge is labeled accordingly.
    ///
    /// [144차 Figma 패리티 수술 — docs/figma_truth_144/potion__173_4.json 진실치 적용 기록]
    /// - P0 #7: 코너 데칼 4개 → UTKTheme.AddCornerDecals(this, 9.6f, 1.8f) (173:5-16, Line 9.6 #58A6FF w1.8, 코너 밖).
    /// - P0 #16: potion-image-wrapper 187.2×274.8 고정, fill #1C2128@0.502, r9.6 (173:24-25).
    ///   _itemIcon 88×88 → wrapper 전체 영역 사용. 원본 scaleMode(ScaleToFit) 유지, 종횡비 기록:
    ///   소스 아이콘은 정방형 → 187.2×274.8 세로 영역에서 좌우 중앙·상하 레터박스 렌더(픽셀 정확 종횡비 보존은 불가).
    ///   이름/수량/설명/물약 목록은 게임 전용 요소(데이터 보존 계약) — wrapper 내부 하단 "오버레이 컬럼"
    ///   (absolute, PickingMode.Position — 목록 행 클릭 경로 유지)으로 이동. 패널 박스 288×432 불변.
    /// - P0 #25: 첫 버튼을 Figma btn-spray-toggle 구조로 교체 — 메인 "G키 분사 / 종료" 12/700 +
    ///   서브 "활성화 전환" 9.6/500, bg #161B22@0.702 r7.2. 기능 배선: GasSprayerController의
    ///   public API(Instance / IsSpraying / StartSpray / StopSpray)가 존재하므로 토글 배선함(G키 바인딩 자체는 게임 입력 측 소관).
    /// - P0 #4: 기존 "복용" 버튼(L176-178) 제거 → 복용 기능은 게임 전용 물약 목록 "행 클릭" 경로로
    ///   이전/보존(행 클릭 = 선택 + 복용). 버튼 행은 [분사 토글][닫기] 2개만(Figma 정합).
    /// - P1 #8(관측): Figma 173:4 fx BACKGROUND_BLUR 36 / DROP_SHADOW 38.4 #000000@0.502 —
    ///   UI Toolkit 런타임 블러/섀도 미지원 → 구현하지 않고 관측 기록만 유지(계약: 그림자/블러 구현 금지).
    /// </summary>
    public sealed class PotionStatusUTK : UTKWindowBase
    {
        private const float PanelWidth = 288f;
        private const float PanelHeight = 432f;
        private static PotionStatusUTK _instance;

        private readonly VisualElement _itemList;
        private readonly Image _itemIcon;
        private readonly Label _itemName;
        private readonly Label _itemCount;
        private readonly Label _itemDescription;
        private readonly Label _occupancyPercent;
        private readonly Label _occupancyFraction;
        private readonly VisualElement _occupancyFill;
        private readonly Label _feedback;
        private readonly Button _sprayButton;
        private readonly Dictionary<string, PlayerInventory.ItemData> _potions = new Dictionary<string, PlayerInventory.ItemData>();
        private string _selectedId;

        private PotionStatusUTK() : base("잔여 물약", new Vector2(PanelWidth, PanelHeight), UTKWindowChrome.Frameless)
        {
            style.width = PanelWidth;
            style.height = PanelHeight;
            style.left = StyleKeyword.Auto;
            style.right = StyleKeyword.Auto;
            style.top = StyleKeyword.Auto;
            style.bottom = StyleKeyword.Auto;
            // [#2] Figma 173:4 fill #0B0E14@0.851 — 알파 0.96 → 0.851.
            style.backgroundColor = new StyleColor(new Color(0.043f, 0.055f, 0.078f, 0.851f));
            // [#3] Figma stroke #30363D@0.8 w1.2 — #2E343D w1 대체.
            Color panelStroke = new Color32(0x30, 0x36, 0x3D, 0xCC); // 0.8 ≈ CC/255
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1.2f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new StyleColor(panelStroke);
            // [#4] 코너 r 14 → 14.4.
            style.borderTopLeftRadius = style.borderTopRightRadius = 14.4f;
            style.borderBottomLeftRadius = style.borderBottomRightRadius = 14.4f;
            // [#7] 코너 데칼 4개 — Figma 173:5-16 (Decal 14.4 프레임 내 Line 9.6 #58A6FF w1.8, 코너 밖).
            UTKTheme.AddCornerDecals(this, 9.6f, 1.8f);
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            // [#5] 패딩 19 → 19.2.
            _content.style.paddingLeft = 19.2f;
            _content.style.paddingRight = 19.2f;
            _content.style.paddingTop = 19.2f;
            _content.style.paddingBottom = 19.2f;

            var header = new VisualElement { name = "PotionHeader" };
            header.style.height = 30f;
            header.style.flexShrink = 0f;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            // [#9] Figma 173:17 header pb 12 — 콘텐츠 h18(title) 상단 정렬 재현.
            header.style.paddingBottom = 12f;
            _content.Add(header);

            // [#10] Figma 173:39 flask-conical 16.8×16.8 — ⚗ 글리프 근사(벡터 아이콘 미지원), 폭 22 → 16.8 고정, 색 Accent 유지.
            var flask = new Label("⚗");
            flask.style.width = 16.8f;
            flask.style.fontSize = 16.8f;
            flask.style.color = new StyleColor(UTKTheme.Accent);
            header.Add(flask);
            // [#11] header-title 16.8 → 15.6 (Figma 173:20, 700/#F0F6FC).
            var title = MakeLabel("잔여 물약", 15.6f, UTKTheme.TextMain, true);
            title.style.flexGrow = 1f;
            header.Add(title);
            // [#12] status-badge — bg #1A382E → #58A6FF@0.082, stroke #58A6FF@0.2 w1.2 추가, r5 → 4.8,
            //       텍스트 9.6 → 10.8/700, 색 Success #3FB950 → #58A6FF.
            //       ⚠ 배지 텍스트는 실상태 유지("IN BAG") — Figma "ACTIVE" 더미로 위조하지 않음(데이터 위조 금지 계약).
            var badge = MakeLabel("IN BAG", 10.8f, UTKTheme.Accent, true);
            badge.style.paddingLeft = 7.2f;
            badge.style.paddingRight = 7.2f;
            badge.style.paddingTop = 2.4f;
            badge.style.paddingBottom = 2.4f;
            badge.style.backgroundColor = new StyleColor(new Color32(0x58, 0xA6, 0xFF, 0x15)); // 0.082 ≈ 21/255
            badge.style.borderTopWidth = badge.style.borderBottomWidth = badge.style.borderLeftWidth = badge.style.borderRightWidth = 1.2f;
            badge.style.borderTopColor = badge.style.borderBottomColor = badge.style.borderLeftColor = badge.style.borderRightColor =
                new StyleColor(new Color32(0x58, 0xA6, 0xFF, 0x33)); // 0.2 ≈ 51/255
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = 4.8f;
            badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 4.8f;
            header.Add(badge);

            var middle = new VisualElement { name = "PotionStatusContent" };
            middle.style.flexGrow = 1f;
            middle.style.minHeight = 0f;
            // [#6] Figma 173:4 VERTICAL gap 19.2 — marginTop/Bottom 10 폐기 → 19.2 간격 체계.
            middle.style.marginTop = 19.2f;
            middle.style.marginBottom = 19.2f;
            middle.style.flexDirection = FlexDirection.Row;
            middle.style.alignItems = Align.Stretch;
            _content.Add(middle);

            // [#16] potion-image-wrapper — 187.2×274.8 고정(기존 flexGrow 동적폭 대체), fill #1C2128@0.502, r9.6,
            //       stroke #30363D@0.8 w1.2 (Figma 173:24 진실치 포함 적용).
            var detail = new VisualElement { name = "PotionImageWrapper" };
            detail.style.width = 187.2f;
            detail.style.flexShrink = 0f;
            // [#13] detail marginRight 14 → 14.4 (Figma 173:23 middle gap 14.4).
            detail.style.marginRight = 14.4f;
            detail.style.backgroundColor = new StyleColor(new Color32(0x1C, 0x21, 0x28, 0x80)); // 0.502 ≈ 128/255
            detail.style.borderTopWidth = detail.style.borderBottomWidth = detail.style.borderLeftWidth = detail.style.borderRightWidth = 1.2f;
            detail.style.borderTopColor = detail.style.borderBottomColor = detail.style.borderLeftColor = detail.style.borderRightColor =
                new StyleColor(panelStroke);
            detail.style.borderTopLeftRadius = detail.style.borderTopRightRadius = 9.6f;
            detail.style.borderBottomLeftRadius = detail.style.borderBottomRightRadius = 9.6f;
            detail.style.flexDirection = FlexDirection.Column;
            detail.style.alignItems = Align.Center;
            middle.Add(detail);

            // [#16] _itemIcon 88×88 → wrapper 전체 영역. 원본 scaleMode(ScaleToFit) 유지 —
            //       종횡비 기록: 정방형 소스 아이콘은 187.2×274.8 세로 프레임에 좌우 중앙·상하 레터박스로 렌더됨.
            _itemIcon = new Image { name = "PotionItemIcon", scaleMode = ScaleMode.ScaleToFit };
            _itemIcon.style.flexGrow = 1f;
            _itemIcon.style.width = Length.Percent(100f);
            _itemIcon.style.height = Length.Percent(100f);
            detail.Add(_itemIcon);

            // [#16] 게임 전용 요소 이동 보존(삭제 금지 계약) — 이름/수량/설명/물약 목록을 wrapper 내부
            //       하단 "오버레이 컬럼"(absolute)으로 이동. 아이콘이 wrapper 전체 영역을 사용하도록
            //       열 흐름에서 제외. 목록 행 클릭 경로(복용)가 살아있으므로 PickingMode.Position 유지.
            var gameOverlay = new VisualElement { name = "PotionGameOverlay" };
            gameOverlay.style.position = Position.Absolute;
            gameOverlay.style.left = 9.6f;
            gameOverlay.style.right = 9.6f;
            gameOverlay.style.bottom = 9.6f;
            gameOverlay.style.flexDirection = FlexDirection.Column;
            gameOverlay.style.alignItems = Align.Center;
            detail.Add(gameOverlay);

            _itemName = MakeLabel("물약 없음", 14.4f, UTKTheme.TextMain, true);
            _itemName.style.whiteSpace = WhiteSpace.Normal;
            _itemName.style.unityTextAlign = TextAnchor.MiddleCenter;
            gameOverlay.Add(_itemName);
            _itemCount = MakeLabel("보유 수량 0", 12f, UTKTheme.Accent, true);
            _itemCount.style.marginTop = 5f;
            gameOverlay.Add(_itemCount);
            _itemDescription = MakeLabel("복용 가능한 물약을 선택하세요.", 12f, UTKTheme.TextSub, false);
            _itemDescription.style.whiteSpace = WhiteSpace.Normal;
            _itemDescription.style.unityTextAlign = TextAnchor.MiddleCenter;
            _itemDescription.style.marginTop = 5f;
            gameOverlay.Add(_itemDescription);
            _itemList = new VisualElement { name = "PotionInventoryList" };
            _itemList.style.width = Length.Percent(100f);
            _itemList.style.marginTop = 8f;
            // 오버레이가 아이콘 영역을 과도히 덮지 않도록 상한(4행) — 물약 목록은 게임 전용 경로로 그대로 접근 가능.
            _itemList.style.maxHeight = 124f;
            _itemList.style.flexDirection = FlexDirection.Column;
            gameOverlay.Add(_itemList);

            var gaugeColumn = new VisualElement { name = "InventorySlotOccupancy" };
            gaugeColumn.style.width = 48f;
            gaugeColumn.style.flexShrink = 0f;
            gaugeColumn.style.alignItems = Align.Center;
            gaugeColumn.style.justifyContent = Justify.Center;
            gaugeColumn.style.flexDirection = FlexDirection.Column;
            middle.Add(gaugeColumn);

            // [#18] 게이지 트랙 — w14 → 14.4, fill #1F2630 → #1C2128@0.902, r7 → 4.8, pad 1.2 추가.
            var track = new VisualElement { name = "SlotOccupancyTrack" };
            track.style.width = 14.4f;
            track.style.flexGrow = 1f;
            track.style.maxHeight = 235f;
            track.style.backgroundColor = new StyleColor(new Color32(0x1C, 0x21, 0x28, 0xE6)); // 0.902 ≈ 230/255
            track.style.paddingLeft = track.style.paddingRight = track.style.paddingTop = track.style.paddingBottom = 1.2f;
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius = 4.8f;
            track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 4.8f;
            track.style.justifyContent = Justify.FlexEnd;
            gaugeColumn.Add(track);
            // [#19] gauge-fill — w10 → 12(트랙 내폭 14.4 − pad 2.4 정합), r5 → 2.4.
            //       Figma 173:28 DROP_SHADOW #00E5FF — UI Toolkit 런타임 섀도 미지원 → 구현하지 않고 관측 기록만(계약).
            _occupancyFill = new VisualElement { name = "SlotOccupancyFill" };
            _occupancyFill.style.width = 12f;
            _occupancyFill.style.alignSelf = Align.Center;
            _occupancyFill.style.backgroundColor = new StyleColor(UTKTheme.Accent);
            _occupancyFill.style.borderTopLeftRadius = _occupancyFill.style.borderTopRightRadius = 2.4f;
            _occupancyFill.style.borderBottomLeftRadius = _occupancyFill.style.borderBottomRightRadius = 2.4f;
            track.Add(_occupancyFill);
            _occupancyPercent = MakeLabel("0%", 13.2f, UTKTheme.Accent, true);
            // [#23] gauge-percent marginTop 8 → 9.2 (Figma gauge-column gap 9.6 근사).
            _occupancyPercent.style.marginTop = 9.2f;
            gaugeColumn.Add(_occupancyPercent);
            // [#22] gauge-fraction — 12 → 10.8, 500 웨이트(UTK Normal 근사 기록), marginTop 2.4 (Figma gauge-labels gap 2.4).
            _occupancyFraction = MakeLabel("0/0", 10.8f, UTKTheme.TextSub, false);
            _occupancyFraction.style.marginTop = 2.4f;
            gaugeColumn.Add(_occupancyFraction);
            var occupancyLabel = MakeLabel("슬롯 사용", 9.6f, UTKTheme.TextSub, false);
            occupancyLabel.style.marginTop = 3f;
            gaugeColumn.Add(occupancyLabel);

            // 게임 전용 복용 결과 라벨 — 열 흐름에서 제외(absolute)하여 middle 274.8 진실치 확보,
            // middle(gap 19.2)과 actions 사이 간격대에 오버레이. 삭제 금지 계약: 이동/보존만.
            _feedback = MakeLabel("", 12f, UTKTheme.TextSub, false);
            _feedback.style.height = 15f;
            _feedback.style.flexShrink = 0f;
            _feedback.style.unityTextAlign = TextAnchor.MiddleCenter;
            _feedback.style.position = Position.Absolute;
            _feedback.style.left = 19.2f;
            _feedback.style.right = 19.2f;
            _feedback.style.bottom = 72f;
            _feedback.pickingMode = PickingMode.Ignore; // 오버레이 — 클릭 가로챔 방지(행 클릭 경로 보호)
            _content.Add(_feedback);

            // [#24] action-buttons-group — h50 → 50.4, 버튼 120×2, gap 9.6(SpaceBetween 유지 — 잔여 9.6 동일).
            var actions = new VisualElement { name = "PotionActions" };
            actions.style.height = 50.4f;
            actions.style.flexShrink = 0f;
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Stretch;
            actions.style.justifyContent = Justify.SpaceBetween;
            _content.Add(actions);
            // [#25] Figma btn-spray-toggle (173:33-35) — 메인 "G키 분사 / 종료" 12/700 + 서브 "활성화 전환" 9.6/500,
            //       bg #161B22@0.702 r7.2. 첫 버튼 "복용" 대체(복용 기능은 물약 목록 행 클릭 경로로 이전 — P0 #4).
            _sprayButton = MakeButton("", UTKTheme.GlassPanelFill, OnSprayToggleClicked);
            _sprayButton.style.width = 120f;
            var sprayText = new VisualElement { name = "SprayButtonLabels" };
            sprayText.style.flexGrow = 1f;
            sprayText.style.flexDirection = FlexDirection.Column;
            sprayText.style.alignItems = Align.Center;
            sprayText.style.justifyContent = Justify.Center;
            sprayText.Add(MakeLabel("G키 분사 / 종료", 12f, UTKTheme.TextMain, true));
            sprayText.Add(MakeLabel("활성화 전환", 9.6f, UTKTheme.TextSub, false)); // 500 웨이트 — UTK Normal 근사
            _sprayButton.Add(sprayText);
            actions.Add(_sprayButton);
            // [#26] btn-close-label — 12/700 TextMain → 14.4/500 #8B949E, bg PanelSub #21262D → #161B22@0.702, r7 → 7.2
            //       (Figma 173:36 진실치 r7.2; 수술 지시서의 "r7"는 진실치로 보정 적용).
            var closeButton = MakeButton("닫기", UTKTheme.GlassPanelFill, Close);
            closeButton.style.width = 120f;
            actions.Add(closeButton);

            ApplyUIToolkitFont(this);
            RefreshInventory();
        }

        /// <summary>Create and show this separate panel. No hotkey or existing gameplay route invokes it yet.</summary>
        public static void Open()
        {
            if (UIToolkitBootstrap.UIRoot == null)
            {
                Debug.LogWarning("[PotionStatusUTK] UIRoot unavailable; panel not opened.");
                return;
            }
            if (_instance == null)
                _instance = new PotionStatusUTK();
            if (_instance.parent == null)
                UIToolkitBootstrap.UIRoot.Add(_instance);
            _instance.RefreshInventory();
            _instance.Show();
        }

        public static void Ensure() { if (_instance == null) _instance = new PotionStatusUTK(); }

        protected override void OnWindowOpen() => RefreshInventory();

        private void RefreshInventory()
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            PlayerInventory.ItemSlot[] slots = inventory != null ? inventory.GetAllSlots() : null;
            int capacity = slots != null ? slots.Length : 0;
            int occupied = 0;
            _potions.Clear();
            if (slots != null)
            {
                foreach (PlayerInventory.ItemSlot slot in slots)
                {
                    if (slot == null || slot.item == null || slot.count <= 0) continue;
                    occupied++;
                    if ((slot.item.category == PlayerInventory.ItemCategory.Potion ||
                         slot.item.category == PlayerInventory.ItemCategory.Drug) &&
                        !string.IsNullOrEmpty(slot.item.id) && !_potions.ContainsKey(slot.item.id))
                        _potions.Add(slot.item.id, slot.item);
                }
            }

            float ratio = capacity > 0 ? Mathf.Clamp01((float)occupied / capacity) : 0f;
            _occupancyFill.style.height = Length.Percent(ratio * 100f);
            _occupancyPercent.text = Mathf.RoundToInt(ratio * 100f) + "%";
            _occupancyFraction.text = occupied + "/" + capacity;

            if (string.IsNullOrEmpty(_selectedId) || !_potions.ContainsKey(_selectedId))
            {
                _selectedId = null;
                foreach (string id in _potions.Keys) { _selectedId = id; break; }
            }
            RebuildPotionList(inventory);
            UpdateSelectedItem(inventory);
        }

        private void RebuildPotionList(PlayerInventory inventory)
        {
            _itemList.Clear();
            if (_potions.Count == 0)
            {
                var empty = MakeLabel("인벤토리에 물약이 없습니다.", 12f, UTKTheme.TextSub, false);
                empty.style.whiteSpace = WhiteSpace.Normal;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                _itemList.Add(empty);
                return;
            }

            foreach (KeyValuePair<string, PlayerInventory.ItemData> entry in _potions)
            {
                string id = entry.Key;
                PlayerInventory.ItemData item = entry.Value;
                int count = inventory != null ? inventory.GetItemCount(id) : 0;
                var row = new Button(() =>
                {
                    _selectedId = id;
                    // [144차 P0 #4] 복용 기능 이전 — 기존 "복용" 버튼을 물약 목록 행 클릭 경로로 이동/보존
                    // (행 클릭 = 선택 + 복용. 버튼 행은 [분사 토글][닫기] 2개만 유지 — Figma 정합).
                    OnUseClicked();
                }) { name = "PotionRow_" + id, text = (item.displayName ?? id) + "   ×" + count };
                row.style.height = 28f;
                row.style.flexShrink = 0f;
                row.style.marginBottom = 3f;
                row.style.paddingLeft = 7f;
                row.style.paddingRight = 7f;
                row.style.backgroundColor = new StyleColor(id == _selectedId ? new Color(0.12f, 0.24f, 0.37f, 1f) : UTKTheme.PanelSub);
                row.style.color = new StyleColor(UTKTheme.TextMain);
                row.style.fontSize = 12f;
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                _itemList.Add(row);
            }
        }

        private void UpdateSelectedItem(PlayerInventory inventory)
        {
            PlayerInventory.ItemData item = null;
            bool hasItem = inventory != null && !string.IsNullOrEmpty(_selectedId) &&
                           _potions.TryGetValue(_selectedId, out item) && inventory.GetItemCount(_selectedId) > 0;
            if (!hasItem)
            {
                _itemIcon.image = null;
                _itemName.text = "물약 없음";
                _itemCount.text = "보유 수량 0";
                _itemDescription.text = "복용 가능한 물약을 선택하세요.";
                // [144차 P0 #4] "복용" 버튼 제거 — 활성/비활성 전환 경로도 행 클릭 경로로 통합(빈 목록이면 행 자체가 없음).
                return;
            }

            _itemIcon.image = item.icon != null ? item.icon.texture : null;
            _itemName.text = string.IsNullOrEmpty(item.displayName) ? item.id : item.displayName;
            _itemCount.text = "보유 수량 " + inventory.GetItemCount(item.id);
            _itemDescription.text = string.IsNullOrEmpty(item.description) ? "선택한 물약을 복용합니다." : item.description;
        }

        private void OnUseClicked()
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            if (inventory == null || string.IsNullOrEmpty(_selectedId) || !_potions.TryGetValue(_selectedId, out PlayerInventory.ItemData item))
                return;
            if (inventory.GetItemCount(item.id) <= 0)
            {
                RefreshInventory();
                return;
            }

            // Consume first so an unavailable item can never grant a free effect.
            if (!inventory.RemoveItem(item.id, 1))
            {
                _feedback.text = "인벤토리에서 물약을 제거할 수 없습니다.";
                _feedback.style.color = new StyleColor(UTKTheme.Warn);
                RefreshInventory();
                return;
            }

            Transform player = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;
            if (!PotionUseSystem.Use(item, player, out string effectText))
            {
                if (inventory.AddItem(item, 1))
                {
                    _feedback.text = "복용 효과를 적용할 수 없어 물약을 반환했습니다.";
                    _feedback.style.color = new StyleColor(UTKTheme.Warn);
                    RefreshInventory();
                    return;
                }
                _feedback.text = "복용 실패 · 물약 반환도 실패했습니다.";
                _feedback.style.color = new StyleColor(UTKTheme.Warn);
                Debug.LogError($"[PotionStatusUTK] Potion use failed and refund failed for item '{item.id}'.");
                RefreshInventory();
                return;
            }

            _feedback.text = string.IsNullOrEmpty(effectText) ? "복용했습니다." : effectText;
            _feedback.style.color = new StyleColor(UTKTheme.Success);
            RefreshInventory();
        }

        /// <summary>
        /// [144차 P0 #25] btn-spray-toggle 기능 배선 — GasSprayerController의 public 토글 API
        /// (Instance / IsSpraying / StartSpray / StopSpray)가 존재하므로 직접 배선.
        /// G키 물리 바인딩 자체는 게임 입력 측(SprayInputHandler) 소관 — 이 버튼은 동일 토글의 UI 진입 경로.
        /// </summary>
        private void OnSprayToggleClicked()
        {
            GasSprayerController controller = GasSprayerController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("[PotionStatusUTK] GasSprayerController.Instance is null; spray toggle unavailable.");
                return;
            }
            if (controller.IsSpraying)
                controller.StopSpray();
            else
                controller.StartSpray();
        }

        private static Label MakeLabel(string text, float size, Color color, bool bold)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = new StyleColor(color);
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            return label;
        }

        private static Button MakeButton(string text, Color color, System.Action callback)
        {
            var button = new Button(callback) { text = text };
            button.style.height = 50.4f; // [#24] Figma action-buttons-group h50.4
            button.style.backgroundColor = new StyleColor(color);
            // [#26] btn-close-label — 14.4 / 500(UTK Normal 근사) / #8B949E
            button.style.color = new StyleColor(UTKTheme.TextSub);
            button.style.fontSize = 14.4f;
            button.style.unityFontStyleAndWeight = FontStyle.Normal;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 7.2f;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 7.2f;
            return button;
        }
    }
}
