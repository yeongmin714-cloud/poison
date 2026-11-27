// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;            // PlayerInventory
using ProjectName.Systems;         // EquipmentManager, LootBasket
using ProjectName.UI;              // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U2 Round 1 — 인벤토리 통합 그리드 (MVP 6경로).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/InventoryWindow.cs (3891줄) — 그 전체 기능이 아닌 후속 6경로만 이식.
    /// 원본은 절대 수정하지 않는다.
    ///
    /// [MVP 6경로]
    ///  ① 통합 그리드   — PlayerInventory.GetAllSlots() 전 카테고리를 단일 그리드(행당 N칸)로 표시.
    ///                     UTKSlot(아이콘+카운트+등급 테두리), 매 갱신 재조회(즉시 반영).
    ///  ② Loot→인벤     — IUTKDropTarget 구현, Drop에서 PlayerInventory.AddItem 호출(원본 TryTakeDraggedToInventory 데이터 경로).
    ///  ③ 인벤 내 스왑  — 드래그 소스=인벤 슬롯, 드롭 타겟=인벤 슬롯 → 원본 슬롯 교체 경로 호출.
    ///  ④ 인벤 밖 드롭  — 땅에 바구니(LootBasket.Create + AddItem) — 원본 TryDropDraggedToTerrain와 동일 API.
    ///  ⑤ 장비 해제 수신— EquipmentManager.OnEquipmentChanged 구독해 갱신만(해제 자체는 Equipment 창 라운드).
    ///  ⑥ 즉시 갱신     — 폴링(refresh 스케줄) + 장비 이벤트 + 드롭 직후 재조회.
    ///  각 경로에 [InventoryUTK] Debug.Log 실측 로그.
    /// </summary>
    public class InventoryWindowUTK : UTKWindowBase, IUTKDropTarget
    {
        // ===== 싱글턴 / 팩토리 =====
        private static InventoryWindowUTK _instance;
        public static InventoryWindowUTK Instance => _instance;

        // ===== [66차] 피그마 15:33/221:8 장비 카드 팔레트 =====
        private static readonly Color EquipAccent = new Color(0.345f, 0.651f, 1f);        // #58A6FF
        private static readonly Color EquipBadgeText = new Color(0.043f, 0.055f, 0.078f); // #0B0E14
        private static readonly Color EquipStripEpic = new Color(0.737f, 0.549f, 1f);     // #BC8CFF
        private static readonly Color EquipStripRare = new Color(0.122f, 0.435f, 0.922f); // #1F6FEB
        private static readonly Color EquipStripCommon = new Color(0.545f, 0.580f, 0.620f); // #8B949E
        private static readonly Color EquipHotkeyBg = new Color(0.941f, 0.953f, 0.961f);  // #F0F3F5
        private static readonly Color EquipHotkeyText = new Color(0.102f, 0.114f, 0.125f); // #1A1D20
        // [68차 — Figma 15:4] 장비 카드 배경(#21262D 60%)·디바이더(#30363D 50%)
        private static readonly Color EquipCardBg = new Color(0.129f, 0.149f, 0.176f, 0.6f);   // #21262D/0.60
        // [144차 P1-INV][A43] 가방 셀 배경 — 15:66/15:83/18:44 다수결 진실치 #21262D/실효 0.6로 교정.
        //   구 68차 "#30363D/0.80" 폐기 — inventory.md 알파 재검증 정정(hue+알파 이중 괴리 확정).
        //   예외 셀 15:63(#30363D@0.8+액센트 스트로크)은 목업 이력 셀로 판정 제외.
        private static readonly Color BagCellBg = new Color(0.129f, 0.149f, 0.176f, 0.6f);     // #21262D/0.60
        private static readonly Color DividerColor = new Color(0.188f, 0.212f, 0.239f, 0.5f); // #30363D/0.50 — [144차 알파 재검증] 0.502 반올림권 PASS 유지

        private void ApplyInventoryBodyLayout(bool requireAttachedPanel = true)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null || _equipPanel == null || _bagViewport == null
                || _tabBar == null || _footer == null || _weightLabel == null || _weightValue == null
                || _divider == null)
                return;
            if (requireAttachedPanel && (panel == null || _content.panel == null || panel != _content.panel || panel != root.panel))
                return;
            if (_isApplyingInventoryBodyLayout)
                return;

            Vector2 rootSize = new Vector2(root.resolvedStyle.width, root.resolvedStyle.height);
            if (rootSize.x <= 0f || rootSize.y <= 0f || float.IsNaN(rootSize.x) || float.IsNaN(rootSize.y))
                return;

            _isApplyingInventoryBodyLayout = true;
            try
            {
                // [Figma 정합 v3] regions는 윈도우-로컬 raw Figma px — 등배수 스케일은 디자인공간이 담당.
                // [136차 B1] Figma 15:5 진실치 재실측 — EquipmentBody y 144 / EquipGridBody y 187.4 / BagViewport y 390.
                //    실측 검증: equip 행은 아래 equipGrid(equipGridBody−equipBody → 섹션-로컬 14.4,43.4 427.2×172.8)에서
                //    left/width를 취하므로 그리드 폭·위치는 리전 값 단일소스([134차 F] 실측 341.42=섹션 456×0.75 렌더치,
                //    그리드 요소 아님). 섹션/뷰포트 y도 리전 갱신만으로 반영 — 하드코딩 y 오프셋 없음 확인.
                Rect equipBody = InventoryClusterPanelRegions.InventoryEquipmentBody;
                Rect equipGridBody = InventoryClusterPanelRegions.InventoryEquipGridBody;
                Rect bagBody = InventoryClusterPanelRegions.InventoryBagViewport;
                Rect footerBody = InventoryClusterPanelRegions.InventoryFooterBody;

                // The Figma region origin is in window space. Translate it to _content-local space
                // using the direct child's actual layout offset; never call ChangeCoordinatesTo here.
                // [149차 P3] 원점 변환 단위 정합 — _content.layout은 창-로컬 '스케일 적용 후' px(k 배수)인데
                //   region은 raw Figma px. 기존엔 k로 나누지 않고 무단 차감해 내부 전체가 chrome×(1−k)만큼
                //   하단으로 밀렸고(런타임 k=0.75 → +12px), 푸터 하단 여백이 21.75→9.5px로 눌렸다(사용자 보고 여백).
                float designK = FigmaCanvasLayout.DesignScale(rootSize);
                Vector2 contentOrigin = requireAttachedPanel
                    ? new Vector2(_content.layout.x, _content.layout.y) / Mathf.Max(0.0001f, designK)
                    : Vector2.zero;
                SetLocalRect(_equipPanel, equipBody.x - contentOrigin.x, equipBody.y - contentOrigin.y, equipBody.width, equipBody.height);
                SetLocalRect(_bagViewport, bagBody.x - contentOrigin.x, bagBody.y - contentOrigin.y, bagBody.width, bagBody.height);

                // [Figma 15:4] 탭 바(윈도우-로컬 24,84,456,36.2) + 적재량 푸터(윈도우-로컬 24,827,456,56).
                Rect tabsBody = InventoryClusterPanelRegions.InventoryTabsBody;
                SetLocalRect(_tabBar, tabsBody.x - contentOrigin.x, tabsBody.y - contentOrigin.y, tabsBody.width, tabsBody.height);
                SetLocalRect(_footer, footerBody.x - contentOrigin.x, footerBody.y - contentOrigin.y, footerBody.width, footerBody.height);

                // [71차] 푸터 자식 = WeightRow-로컬 Rect(Regions 신설) + footerRow.y(7.2) 가산 → footer-로컬 배치.
                float rowY = InventoryClusterPanelRegions.InventoryWeightRow.y;   // 7.2
                Rect goldBalanceRow = InventoryClusterPanelRegions.GoldBalanceRow;
                SetLocalRect(_goldBalance, goldBalanceRow.x, goldBalanceRow.y + rowY, goldBalanceRow.width, goldBalanceRow.height);
                Rect dumbbellRow = InventoryClusterPanelRegions.DumbbellRow;
                SetLocalRect(_goldDumbbell, dumbbellRow.x, dumbbellRow.y + rowY, dumbbellRow.width, dumbbellRow.height);
                Rect weightLabelRow = InventoryClusterPanelRegions.WeightLabelRow;
                SetLocalRect(_weightLabel, weightLabelRow.x, weightLabelRow.y + rowY, weightLabelRow.width, weightLabelRow.height);
                Rect weightValueRow = InventoryClusterPanelRegions.WeightValueRow;
                SetLocalRect(_weightValue, weightValueRow.x, weightValueRow.y + rowY, weightValueRow.width, weightValueRow.height);
                // ProgressBar는 footer-로컬(0,41) — row 가산 없음.
                Rect progressRow = InventoryClusterPanelRegions.ProgressRow;
                SetLocalRect(_progressBar, progressRow.x, progressRow.y, progressRow.width, progressRow.height);

                // [68차] Divider — 윈도우-로컬 (24,365) 456×1.2
                Rect dividerBody = InventoryClusterPanelRegions.InventoryDivider;
                SetLocalRect(_divider, dividerBody.x - contentOrigin.x, dividerBody.y - contentOrigin.y, dividerBody.width, dividerBody.height);

                // Both Figma positions are window-local, so subtraction yields _equipPanel-local.
                Rect equipGrid = new Rect(
                    equipGridBody.x - equipBody.x,
                    equipGridBody.y - equipBody.y,
                    equipGridBody.width,
                    equipGridBody.height);

                float cell = InventoryClusterPanelRegions.CellSize;
                float bagGap = InventoryClusterPanelRegions.CellGap;
                float equipGap = InventoryClusterPanelRegions.EquipCellGap;
                float equipRowGap = Mathf.Max(0f, (equipGrid.height - EquipRows * cell) / (EquipRows - 1));
                int rowIndex = 0;
                foreach (var row in _equipPanel.Children())
                {
                    // [68차] 섹션 라벨/넘버 바는 카드 행이 아니다 — 크기 순회 제외(독립 absolute 배치).
                    if (row.name == "EquipSectionLabel" || row.name == "EquipNumbersBar") continue;
                    row.style.position = Position.Absolute;
                    row.style.left = equipGrid.x;
                    row.style.top = equipGrid.y + rowIndex * (cell + equipRowGap);
                    row.style.width = equipGrid.width;
                    row.style.height = cell;
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.flexWrap = Wrap.NoWrap;
                    row.style.justifyContent = Justify.FlexStart;
                    row.style.marginBottom = 0f;
                    int columnIndex = 0;
                    foreach (var slot in row.Children())
                    {
                        slot.style.width = cell;
                        slot.style.height = cell;
                        slot.style.flexShrink = 0f;
                        slot.style.marginRight = columnIndex < EquipColumns - 1 ? equipGap : 0f;
                        columnIndex++;
                    }
                    rowIndex++;
                }

                _bagViewport.contentContainer.style.width = bagBody.width;
                _bagViewport.contentContainer.style.flexWrap = Wrap.Wrap;
                _bagViewport.contentContainer.style.justifyContent = Justify.Center;
                _bagViewport.contentContainer.style.alignContent = Align.FlexStart;
                _bagViewport.contentContainer.style.alignItems = Align.FlexStart;
                _bagViewport.contentContainer.style.paddingLeft = 0f;
                _bagViewport.contentContainer.style.paddingRight = 0f;
                int bagSlotIndex = 0;
                int bagSlotCount = _grid.childCount;
                foreach (var slot in _grid.Children())
                {
                    slot.style.width = cell;
                    slot.style.height = cell;
                    slot.style.flexShrink = 0f;
                    slot.style.marginLeft = 0f;
                    slot.style.marginRight = bagSlotIndex % Columns < Columns - 1 ? bagGap : 0f;
                    slot.style.marginTop = 0f;
                    slot.style.marginBottom = bagSlotIndex / Columns < (bagSlotCount + Columns - 1) / Columns - 1 ? bagGap : 0f;
                    bagSlotIndex++;
                }
            }
            finally
            {
                _isApplyingInventoryBodyLayout = false;
            }
        }

        private void OnInventoryAttached(AttachToPanelEvent evt)
        {
            RegisterRootGeometryCallback(UIToolkitBootstrap.UIRoot);
            if (IsOpen)
                ApplyInventoryBodyLayout();
        }

        private void OnInventoryDetached(DetachFromPanelEvent evt)
        {
            UnregisterRootGeometryCallback();
        }

        private void RegisterRootGeometryCallback(VisualElement root)
        {
            if (root == null || panel == null || root.panel != panel)
                return;
            if (_geometryRoot == root)
                return;
            UnregisterRootGeometryCallback();
            _geometryRoot = root;
            _geometryRoot.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        private void UnregisterRootGeometryCallback()
        {
            if (_geometryRoot != null)
            {
                _geometryRoot.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
                _geometryRoot = null;
            }
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            var root = _geometryRoot;
            if (root == null || evt.target != root || !IsOpen || _isApplyingInventoryBodyLayout || _isHandlingRootGeometry)
                return;
            if (panel == null || panel != root.panel || _content.panel != panel)
                return;
            _isHandlingRootGeometry = true;
            try
            {
                InventoryClusterFigmaLayout.ApplyInventory(this, _content, root);   // [Figma 정합 v3] 등배수
                ApplyInventoryBodyLayout();
            }
            finally
            {
                _isHandlingRootGeometry = false;
            }
        }

        private static void SetLocalRect(VisualElement element, float x, float y, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = x;
            element.style.top = y;
            element.style.width = width;
            element.style.height = height;
        }

        /// <summary>팩토리 — UIRoot 좌측 배치(좌:인벤 관례). 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new InventoryWindowUTK();
        }

        /// <summary>인벤 열기(팩토리 겸용).</summary>
        public static void Open()
        {
            UTKInventoryClusterComposition.OpenInventory();
        }

        /// <summary>인벤토리/상세 Figma sibling composition 토글.</summary>
        public static void Toggle()
        {
            UTKInventoryClusterComposition.ToggleInventory();
        }

        // ===== 설정 =====
        private const float WinW = 420f;    // Historical base constructor size; Show applies Figma outer bounds.
        private const float WinH = 760f;
        private const int BagRows = InventoryClusterPanelRegions.InventoryVisibleBagRows;
        private const int EquipColumns = 5; // Figma 장비 5열
        private const int EquipRows = 2;    // Figma 장비 2행
        private const int Columns = 5;   // Figma 가방 그리드 행당 5칸

        // [Figma 15:4 카테고리 탭] 탭 5종 라벨 — 전체/무기/방어구/소모품/재료.
        private static readonly string[] TabLabels = { "전체", "무기", "방어구", "소모품", "재료" };

        private const long RefreshMs = 250L;

        // Epic은 공유 테마의 토큰이 아닌 의미론적 등급색으로 유지한다.
        private static readonly Color RankEpicColor = new Color32(0xA3, 0x71, 0xF7, 0xFF);

        /// <summary>공유 UTKTheme 크롬 + 인벤 창의 기존 닫기 버튼 세부 스타일.</summary>
        private void ApplyInventoryWindowStyle()
        {
            var titleBar = this.Q("TitleBar");
            UTKTheme.ApplyWindowChrome(this, titleBar, _content);

            // 이 창은 standalone 표준 크롬 창이며, 베이크된 우드 패널 이미지만 사용하지 않는다.
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            if (titleBar != null)
            {
                titleBar.style.borderTopLeftRadius = UTKTheme.RadiusMain;
                titleBar.style.borderTopRightRadius = UTKTheme.RadiusMain;
            }

            if (_titleLabel != null)
            {
                // [Figma 15:4 헤더 진실치] 제목 24/700, #F0F6FC(=UTKTheme.TextMain), 패널-로컬 (24, 29.2).
                //   표준 크롬 TitleBar(.utk-title-bar flex, padding 6/8) 구조상 절대좌표 (24, 29.2)는
                //   공유 크롬(USS/UTKWindowBase) 불변 제약으로 산출 불가 → 주석 판단기록, 수치(글자/색)만 교정.
                //   before: fontSize = USS var(--fs-lg) 38 → 진실치 24 교정. Bold(700)는 USS -unity-font-style 유지(이미 진실치).
                _titleLabel.style.fontSize = 24f;
                _titleLabel.style.color = new StyleColor(UTKTheme.TextMain);
            }

            // [144차 P1-INV][A6 → P0-1] 헤더 Subtitle "INVENTORY" — Figma 15:21 (패널-로컬 (153,38)) 78×17,
            //   14.4/400 #8B949E. TitleGroup(15:19) HORIZONTAL gap 12 — 제목 우측 +12px 배치(상세/창고 선례 동일).
            //   [144차 P0 V4] 부제 토큰 계약(14.4/400/TextSub)은 StyleFigmaText로 그대로 이행. 다만 V4 헬퍼
            //   BuildPanelHeader2Row는 "자체 타이틀을 새로 만드는 세로 2행 헤더"라 공용 크롬 TitleBar(타이틀 보유)와
            //   병용하면 타이틀이 중복되므로 구조 적용은 배제하고 토큰+gap 12 배치 계약만 적용한다(판단기록).
            if (titleBar != null && _titleLabel != null)
            {
                var subtitle = new Label("INVENTORY") { name = "InventorySubtitle" };
                // [185차 후속] 가독성 조정 — 사용자 확정: 본문 14.4→16.8(Figma 대비 상향, 인벤 스코프)
                UTKTheme.StyleFigmaText(subtitle, 16.8f, 400, UTKTheme.TextSub);
                subtitle.style.flexGrow = 0f;
                subtitle.style.flexShrink = 0f;
                subtitle.style.marginLeft = 12f;   // Figma TitleGroup(15:19) gap 12 — 제목 우 +12px
                subtitle.style.whiteSpace = WhiteSpace.NoWrap;
                subtitle.pickingMode = PickingMode.Ignore;
                // 크롬 .utk-title-label flexGrow 1 → 부제가 타이틀에서 멀어져 우단(닫기 버튼 옆)에 붙는다.
                //   이 창 한정 인라인으로 flexGrow 0 — 부제가 Figma 구조(제목 바로 우측)대로 붙는다.
                //   남는 공간은 TitleBar 자체의 드래그 영역(공용 크롬 동작 불변).
                _titleLabel.style.flexGrow = 0f;
                titleBar.Insert(1, subtitle);   // 타이틀 다음, 닫기 버튼 앞
                ApplyUIToolkitFont(subtitle);   // ApplyUIToolkitFont(this) 이후 추가되므로 개별 적용
            }

            // 닫기 버튼: [144차 P1-INV][A7] Figma 15:22 진실치 — 31.2×31.2 · r7.2 · stroke #30363D@0.502 1.2 ·
            //   fill #FFFFFF@0.059(창고 D4 닫기와 동일 체계). 기본 ✕ 텍스트 유지(벡터 아이콘은 A9 P2 근사 별도).
            var closeBtn = this.Q<Button>("CloseButton");
            if (closeBtn != null)
            {
                closeBtn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                closeBtn.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.0588f));
                closeBtn.style.width = 31.2f;
                closeBtn.style.height = 31.2f;
                // 크롬 TitleBar 높이 불변 가드 — 버튼 31.2가 바 높이를 키워 본문 리전을 +3.2px 아래로 밀지 않게
                //   바깥기여(31.2−3.2=28)를 유지한다. 타이틀 라벨 24px 라인이 지배 높이면 무효인 안전망.
                closeBtn.style.marginTop = closeBtn.style.marginBottom = -1.6f;
                UTKTheme.ApplyElementBorder(closeBtn);   // #30363D@0.502 / 1.2 — [144차 P0 V2] 공용 어휘
                UTKTheme.ApplyRadius(closeBtn, 7.2f);
                closeBtn.style.color = new StyleColor(UTKTheme.TextMain);
            }
        }

        /// <summary>[68차 → 144차] 가방 셀 Figma 15:4 정합 — bg #21262D 60%([144차 A43] hue 교정) · radius 9.6 ·
        ///   테두리 #30363D@0.502 1.2([144차 A44] 신설 — 현재 JSON 진실, 68차 "테두리 없음" 구기록 폐기),
        ///   아이콘 38.4 중앙(21.6,21.6), Count 우하단 13.2/500 #8B949E. 호버만 액센트 1.2(Figma 미정의 시각 피드백).</summary>
        private static void ApplyDarkSlotBase(UTKSlot slot)
        {
            if (slot == null) return;
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);   // 우드 베이크 이미지 제거
            slot.style.backgroundColor = new StyleColor(BagCellBg);                 // [144차 A43] #21262D/0.60
            SetSlotBorder(slot, UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth); // [144차 A44] #30363D@0.502 1.2 — [144차 P0 V2] 어휘
            slot.style.borderTopLeftRadius = 9.6f;
            slot.style.borderTopRightRadius = 9.6f;
            slot.style.borderBottomLeftRadius = 9.6f;
            slot.style.borderBottomRightRadius = 9.6f;
            // [68차] 아이콘 38.4×38.4 카드 중앙 — USS 100% 팽창 대체(피그마 GridRow 셀 실측)
            var iconHost = slot.IconHost;
            if (iconHost != null)
            {
                iconHost.style.position = Position.Absolute;
                iconHost.style.left = 21.6f;
                iconHost.style.top = 21.6f;
                iconHost.style.width = 38.4f;
                iconHost.style.height = 38.4f;
                iconHost.style.flexGrow = 0f;
            }
            // [68차] Count 우하단 (right 7.6, bottom 4.6) — [185차 후속] 13.2→15.6 가독성 조정(하우징 17→20)
            var countLabel = slot.Q<Label>("Count");
            if (countLabel != null)
            {
                countLabel.style.position = Position.Absolute;
                countLabel.style.right = 7.6f;
                countLabel.style.bottom = 3.2f;
                countLabel.style.height = 20f;
                countLabel.style.fontSize = 15.6f;
                countLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                countLabel.style.color = new StyleColor(UTKTheme.TextSub);
                countLabel.style.marginRight = 0f;
                countLabel.style.marginBottom = 0f;
                countLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            }

            // 호버 강조(시각만): 진입=액센트 1.2, 이탈=크롬 테두리 복원([144차 A44] — 기본 테두리가 생겼으므로 0 복원 폐기)
            slot.RegisterCallback<PointerEnterEvent>(_ => SetSlotBorder(slot, UTKTheme.Accent, UTKTheme.BorderChromeWidth));
            slot.RegisterCallback<PointerLeaveEvent>(_ => SetSlotBorder(slot, UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth));
        }

        /// <summary>[68차 → 144차] 장비 카드 Figma 15:4 정합 — bg #21262D 60% · radius 9.6 ·
        ///   테두리 #30363D@0.502 1.2([144차 A44 동일 진실치 — 15:36/15:41/18:13/18:19 장비 ItemSlot 스트로크]).
        ///   USS .utk-slot margin 3px 인라인 상쇄 필수(행/열 간격이 ApplyInventoryBodyLayout 전담).</summary>
        private static void ApplyEquipCardBase(UTKSlot slot)
        {
            if (slot == null) return;
            slot.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            slot.style.backgroundColor = new StyleColor(EquipCardBg);   // #21262D/0.60
            slot.style.marginTop = slot.style.marginBottom = 0f;
            slot.style.marginLeft = slot.style.marginRight = 0f;
            SetSlotBorder(slot, UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth);   // [144차 A44] 전 셀 테두리
            slot.style.borderTopLeftRadius = 9.6f;
            slot.style.borderTopRightRadius = 9.6f;
            slot.style.borderBottomLeftRadius = 9.6f;
            slot.style.borderBottomRightRadius = 9.6f;
            slot.RegisterCallback<PointerEnterEvent>(_ => SetSlotBorder(slot, UTKTheme.Accent, UTKTheme.BorderChromeWidth));
            slot.RegisterCallback<PointerLeaveEvent>(_ => SetSlotBorder(slot, UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth));
        }

        /// <summary>[68차] TierStrip 등급색 공용 판정 — epic+ 보라 / rare 파랑 / 그 외 회색(피그마 진실치, 66차 로직 추출).</summary>
        private static Color StripColorForRank(int rarityIndex)
        {
            return rarityIndex >= 3 ? EquipStripEpic : (rarityIndex == 2 ? EquipStripRare : EquipStripCommon);
        }

        private static void SetSlotBorderWidth(VisualElement ve, float width)
        {
            if (ve == null) return;
            ve.style.borderTopWidth = ve.style.borderBottomWidth = ve.style.borderLeftWidth = ve.style.borderRightWidth = new StyleFloat(width);
        }

        private static void SetSlotBorder(VisualElement ve, Color c, float width)
        {
            if (ve == null) return;
            ve.style.borderTopColor = ve.style.borderBottomColor = ve.style.borderLeftColor = ve.style.borderRightColor = new StyleColor(c);
            SetSlotBorderWidth(ve, width);
        }

        /// <summary>공유 토큰 기반 레어도 팔레트 — Epic 퍼플은 의미론적 색상으로 유지.</summary>
        private static Color? RankColor(int rarityIndex)
        {
            switch (rarityIndex)
            {
                case 0: return UTKTheme.TextSub;          // common — 보조그레이
                case 1:
                case 2: return UTKTheme.Accent;           // uncommon/rare — 액센트
                case 3: return RankEpicColor;             // epic — 의미론적 퍼플 #A371F7
                case 4:
                case 5: return UTKTheme.Gold;             // legendary/unique — 금색
                default: return null;                     // 범위 밖 — Theme 기본 유지
            }
        }

        // ===== 레퍼런스 =====
        private readonly VisualElement _grid;
        private readonly ScrollView _bagViewport;
        private readonly VisualElement _equipPanel;        // [3분할] 좌측 상단 장비 2x5
        private readonly System.Collections.Generic.Dictionary<string, UTKSlot> _equipSlotIcons
            = new System.Collections.Generic.Dictionary<string, UTKSlot>();   // [P16-2] 장비 아이콘 슬롯
        private readonly System.Collections.Generic.Dictionary<string, VisualElement> _equipStrips
            = new System.Collections.Generic.Dictionary<string, VisualElement>();   // [66차→68차] TierStrip 상단 가로 등급줄

        private readonly VisualElement _warehousePanel;    // [3분할] 우측 창고 그리드 (상호작용시만)
        private readonly Label _warehouseEmptyHint;
        private Label _descName;                            // [3분할] 중앙 설명창
        private Label _descText;
        private readonly VisualElement _tabBar;             // [Figma 15:4] 카테고리 탭 바(5종)
        private readonly List<Button> _tabButtons = new List<Button>();
        private int _activeTabIndex;                        // 활성 탭(0=전체)
        private readonly VisualElement _footer;             // [Figma 15:4] 적재량 푸터(WeightRow)
        private readonly Label _weightLabel;
        private readonly Label _weightValue;
        private readonly VisualElement _goldBalance;        // [71차] 피그마 225:4 GoldBalance 위젯(공용 빌더)
        private readonly VisualElement _goldDumbbell;       // [71차] WeightTitle dumbbell 절차 아이콘(3rect)
        private Label _goldValue;                           // [71차] GoldValue — 지갑 단일소스 라벨
        private readonly VisualElement _progressBar;        // [71차] ProgressBar(footer-local (0,41) 456×7.2)
        private readonly VisualElement _progressFill;       // [71차] ProgressFill #58A6FF — 사용률 fill
        private bool _goldSubscribed;                       // [71차] GoldChanged 구독 수명(Show/Hide 동기)
        private readonly VisualElement _divider;            // [68차] Figma 15:4 Divider(장비↔가방 구분선)
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;
        private EquipmentManager _subscribedEquip;
        private VisualElement _geometryRoot;
        private bool _isApplyingInventoryBodyLayout;
        private bool _isHandlingRootGeometry;

        /// <summary>현재 선택된 인벤 아이템 (QuickSlot 등록용 노출).</summary>
        private PlayerInventory.ItemData _selectedItemData;

        // =====================================================================
        //  선택 접근자 — QuickSlotUTK 등록용 (원본 InventoryWindow.HasSelectedItem/GetSelectedItemData 대응)
        // =====================================================================

        /// <summary>인벤토리에서 선택된 아이템이 있는지.</summary>
        public bool HasSelectedInInventory() => _selectedItemData != null;

        /// <summary>현재 선택된 인벤토리 아이템 (없으면 null).</summary>
        public PlayerInventory.ItemData GetSelectedInventoryItem() => _selectedItemData;

        // 드롭 타겟 재생성 위생: 각 리프레시에서 이전 슬롯 바인딩 해제 후 재등록.
        private readonly List<VisualElement> _slotTargets = new List<VisualElement>();

        private InventoryWindowUTK() : base("인벤토리", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            // Root-scaled Figma body regions use _content-local coordinates; titlebar remains base chrome.
            _content.style.paddingTop = 0f;
            _content.style.paddingBottom = 0f;
            _content.style.paddingLeft = 0f;
            _content.style.paddingRight = 0f;

            // ④ 월드 드롭 백드롭: UIRoot(전체 화면) — 인벤 드롭이 UI 밖이면 땅에 바구니.
            RegisterWorldDrop();

            // 장비 본문(2×5) — inherited UTKWindowBase titlebar is intentionally not duplicated.
            _equipPanel = new VisualElement { name = "InventoryEquipmentBody" };
            _equipPanel.style.position = Position.Absolute;
            _equipPanel.style.flexDirection = FlexDirection.Column;
            _equipPanel.style.flexWrap = Wrap.NoWrap;
            _equipPanel.style.alignContent = Align.FlexStart;
            _equipPanel.style.alignItems = Align.FlexStart;
            _equipPanel.style.justifyContent = Justify.FlexStart;
            // [144차 P1-INV][A18] EquippedSection 카드 크롬 — Figma 15:33 r9.6 + stroke #30363D@0.502 1.2
            //   + fill #FFFFFF@0.02(알파 재검증 확정 — 저알파 화이트 글래스). 구 68차 무크롬 상태 해소.
            //   [144차 P0 V2] ApplyElementBorder/ApplyRadius 공용 어휘. 절대자식(라벨/행/바)은 패널 코너 기준
            //   배치 유지(진실치 SectionLabel (14.4,14.4) 등 불변).
            _equipPanel.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.02f));
            UTKTheme.ApplyElementBorder(_equipPanel);
            UTKTheme.ApplyRadius(_equipPanel, 9.6f);
            _content.Add(_equipPanel);
            BuildEquipPanel();

            // [68차] Figma 15:4 EquippedSection 라벨 — "장착 중인 장비" 14.4/700 #58A6FF at equipPanel-local (14.4,14.4).
            var equipSectionLabel = new Label("장착 중인 장비") { name = "EquipSectionLabel" };
            equipSectionLabel.style.position = Position.Absolute;
            equipSectionLabel.style.left = 14.4f;
            equipSectionLabel.style.top = 14.4f;
            // [185차 후속] 가독성 조정 — 섹션 라벨 14.4→16.8 + 하우징 확대(세로 여백 사용자 확정)
            UTKTheme.StyleFigmaText(equipSectionLabel, 16.8f, 700, UTKTheme.Accent);
            equipSectionLabel.pickingMode = PickingMode.Ignore;
            _equipPanel.Add(equipSectionLabel);

            // [68차] Figma 15:4 Divider — 장비 섹션↔가방 그리드 구분선 (24,365) 456×1.2 #30363D 50%.
            _divider = new VisualElement { name = "InventoryDivider" };
            _divider.style.position = Position.Absolute;
            _divider.style.backgroundColor = new StyleColor(DividerColor);
            _divider.pickingMode = PickingMode.Ignore;
            _content.Add(_divider);

            // Keep _grid as ScrollView.contentContainer: refresh clear/add and slot DnD registration
            // continue to target the same element, which remains the direct parent of every slot.
            _bagViewport = new ScrollView(ScrollViewMode.Vertical) { name = "InventoryBagViewport" };
            _bagViewport.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _bagViewport.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _bagViewport.style.position = Position.Absolute;
            _bagViewport.style.overflow = Overflow.Hidden;
            _bagViewport.contentContainer.style.flexDirection = FlexDirection.Row;
            _bagViewport.contentContainer.style.flexWrap = Wrap.Wrap;
            _bagViewport.contentContainer.style.justifyContent = Justify.FlexStart;
            _bagViewport.contentContainer.style.alignContent = Align.FlexStart;
            _bagViewport.contentContainer.style.alignItems = Align.FlexStart;
            _bagViewport.contentContainer.style.width = Length.Percent(100f);
            _bagViewport.contentContainer.style.flexGrow = 0f;
            _bagViewport.contentContainer.style.flexShrink = 0f;
            _content.Add(_bagViewport);
            _grid = _bagViewport.contentContainer;
            _grid.name = "InvGrid";

            // ── [Figma 15:4 카테고리 탭 바] 탭 5종(전체/무기/방어구/소모품/재료) — 클릭 시 가방 그리드 필터 재렌더.
            // 기하: TabContainer 윈도우-로컬 (24,84,456,36.2), 탭 각 87.4×36.2, x stride 92.2(갭 4.8).
            // 탭별 필터 매핑(RefreshGrid/MatchesActiveTab 참조): 전체=전부, 무기=Weapon, 방어구=Armor+Accessory,
            // 소모품=Food+Potion+Drug, 재료=Herb+Meat+Material. Quest/Tool/Arrow/Bomb는 Figma 탭 미분류 —
            // "전체" 탭에서만 노출한다(탭 필터에서 제외).
            _tabBar = new VisualElement { name = "InventoryTabs" };
            for (int i = 0; i < InventoryClusterPanelRegions.InventoryTabCount; i++)
            {
                int tabIndex = i;   // 클로저 캡처 고정
                var tab = new Button(() => SetActiveTab(tabIndex))
                {
                    name = "InvTab_" + TabLabels[i],
                    text = TabLabels[i]
                };
                tab.style.position = Position.Absolute;
                tab.style.left = InventoryClusterPanelRegions.InventoryTabStride * i;
                tab.style.width = InventoryClusterPanelRegions.InventoryTabWidth;
                tab.style.height = InventoryClusterPanelRegions.InventoryTabHeight;
                tab.style.borderTopWidth = tab.style.borderBottomWidth = tab.style.borderLeftWidth = tab.style.borderRightWidth = 0f;
                // 코너 radius — [68차] 피그마 15:4 탭 실측 7.2(기존 RadiusBadge 4 대체).
                //   [144차 참고] 스트로크는 ApplyTabVisuals가 상태별(활성/비활성)로 지정 — 초기 0은 ApplyTabVisuals 즉시 덮어씀.
                tab.style.borderTopLeftRadius = 7.2f;
                tab.style.borderTopRightRadius = 7.2f;
                tab.style.borderBottomLeftRadius = 7.2f;
                tab.style.borderBottomRightRadius = 7.2f;
                // [Figma 15:4] 탭 라벨 14.4/700 공통 — Button은 TextElement라 자체 color로 라벨색 제어.
                // [185차 후속] 가독성 조정 — 탭 라벨 14.4→16.8(인벤 스코프, 공용 FontTab 토큰 무변경)
                tab.style.fontSize = 16.8f;
                tab.style.unityFontStyleAndWeight = FontStyle.Bold;
                _tabBar.Add(tab);
                _tabButtons.Add(tab);
            }
            _content.Add(_tabBar);
            ApplyTabVisuals();

            // ── [Figma 15:4 적재량 푸터] footer region(24,827,456,56) = WeightRow.
            // Figma는 kg(34.8/120.0) 목업이나 런타임 무게 데이터 부재 — 위조 금지 규약(요리 푸터 선례:
            // "사용 N/40" 슬롯 기반)에 따라 무게 kg 대신 사용 슬롯 수로 표기한다. 값은 RefreshGrid에서 갱신.
            _footer = new VisualElement { name = "InventoryFooter" };
            _weightLabel = new Label("인벤토리 적재량");
            _weightLabel.pickingMode = PickingMode.Ignore;
            // [185차 후속] 가독성 조정 — 적재량 라벨/값 15.6→18 + 하우징 높이 확대(세로 여백)
            UTKTheme.StyleFigmaText(_weightLabel, 18f, 400, UTKTheme.TextSub, TextAnchor.MiddleLeft);
            _footer.Add(_weightLabel);
            _weightValue = new Label("");
            _weightValue.pickingMode = PickingMode.Ignore;
            UTKTheme.StyleFigmaText(_weightValue, 18f, 700, UTKTheme.Accent, TextAnchor.MiddleRight);   // [185차 후속] 15.6→18
            _footer.Add(_weightValue);
            // [71차] 피그마 225:4~7 GoldBalance — 공용 위젯(pill "골드" + 값 라벨), 지갑 단일소스 표시.
            _goldBalance = UTKGoldBalance.Build(out _goldValue);
            _goldBalance.style.position = Position.Absolute;
            _footer.Add(_goldBalance);
            // [71차] dumbbell 절차 아이콘(16.8×16.8, #8B949E) — WeightTitle 아이콘(Figma 벡터 기하 모사).
            _goldDumbbell = BuildDumbbellIcon();
            _footer.Add(_goldDumbbell);
            // [71차] ProgressBar(footer-local (0,41) 456×7.2) — bg #FFFFFF/8% + fill #58A6FF(사용률).
            _progressBar = new VisualElement { name = "InventoryProgress" };
            _progressBar.style.position = Position.Absolute;
            _progressBar.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.08f));
            _progressBar.style.borderTopLeftRadius = 3.6f;
            _progressBar.style.borderTopRightRadius = 3.6f;
            _progressBar.style.borderBottomLeftRadius = 3.6f;
            _progressBar.style.borderBottomRightRadius = 3.6f;
            _progressBar.pickingMode = PickingMode.Ignore;
            _progressFill = new VisualElement { name = "InventoryProgressFill" };
            _progressFill.style.height = 7.2f;
            _progressFill.style.backgroundColor = new StyleColor(UTKTheme.Accent);
            _progressFill.style.borderTopLeftRadius = 3.6f;
            _progressFill.style.borderTopRightRadius = 3.6f;
            _progressFill.style.borderBottomLeftRadius = 3.6f;
            _progressFill.style.borderBottomRightRadius = 3.6f;
            _progressFill.pickingMode = PickingMode.Ignore;
            _progressBar.Add(_progressFill);
            _footer.Add(_progressBar);
            _content.Add(_footer);
            UpdateWeightFooter(0, InventoryClusterPanelRegions.InventoryMaxSlots);
            ApplyInventoryBodyLayout(requireAttachedPanel: false);
            RegisterCallback<AttachToPanelEvent>(OnInventoryAttached);
            RegisterCallback<DetachFromPanelEvent>(OnInventoryDetached);

            // [독립 창] 설명창은 ItemDescriptionWindowUTK(별개 창), 창고/전리품은 각각 독립 창.

            ApplyUIToolkitFont(this);

            // [Figma GitHub-dark 리스타일] 공유 UTKTheme 크롬 — 시각 전용.
            ApplyInventoryWindowStyle();

            // [144차 P1-INV][P0-2] 코너 데칼 4개 — Figma 15:6/15:9/15:12/15:15 DecalTL/TR/BL/BR,
            //   14.4×14.4 + L자 12×1.8 #58A6FF. [144차 P0 V1] UTKTheme.AddCornerDecals 공용 어휘.
            //   outsideCorner=false(안쪽 모드) — 창 루트 .utk-window overflow:hidden 으로 바깥걸침 모드는 클립된다.
            //   Δ 기록: 진실치 데칼 박스는 패널 코너 (0,0) 기점(L 12 길이) — 헬퍼 안쪽 모드는 inset 4·L 14.4
            //   (≤4.8px 시각 근사, V1 어휘 계약 우선 — 창고 D17 전용 구현과 이중 구조화하지 않음).
            UTKTheme.AddCornerDecals(this, 14.4f, 1.8f, "#58A6FF", outsideCorner: false);

            // 윈도우 자체 = ②Loot 수령 / ③패널 위 인벤 취소 타겟 (슬롯보다 하위 우선).
            UTKDragDrop.RegisterDropTarget(this, this);

            // 기본 숨김. [149차 P3] 구 3분할 좌측 배치 헬퍼 호출 제거 — Figma 단일 배치 소스
            //   (InventoryClusterFigmaLayout.ApplyInventory 144,84,504,912)와 경합해 창 위치가 흔들렸다.
            style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  생명주기 (UTKWindowBase 훅)
        // =====================================================================

        public override void Show()
        {
            base.Show();   // Register + 표시 + OnWindowOpen
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            // [149차 P3] 구 3분할 좌측 배치 헬퍼 호출 제거 — 창 외곽/내부 배치는 Figma 단일 소스
            //   (ApplyInventory 144,84,504,912)가 전담한다. 기존 3분할 좌측 배치가 Figma 배치와 경합했다.
            InventoryClusterFigmaLayout.ApplyInventory(this, _content, root);   // [Figma 정합 v3] 등배수
            RegisterRootGeometryCallback(root);
            ApplyInventoryBodyLayout();
            EnsureEquipSubscription();
            EnsureGoldSubscription();   // [71차] 골드 표시 — GoldChanged 실시간(개방 중 수령 즉시 반영)
            StartRefreshLoop();
            RefreshGrid(force: true);   // [Figma 15:4 탭 필터] 열림 시 활성 탭 필터 강제 적용(포인터가 UI 위여도 무시)
            RefreshEquipPanel();   // [U8 확장] 장비 임베드 갱신
            Debug.Log("[InventoryUTK] 인벤토리 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            UnregisterRootGeometryCallback();
            StopRefreshLoop();
            UnsubscribeGold();   // [71차] GoldChanged 구독 해제 — 창 닫힘 누수 방지
            Debug.Log("[InventoryUTK] 인벤토리 창 닫힘");
        }

        /// <summary>[U8 확장] 우측 장비 임베드 패널 구성 — EquipmentManager 실측 데이터, 우클릭=해제(UnequipSlot).</summary>
        private void BuildEquipPanel()
        {
            var em = EquipmentManager.Instance ?? EquipmentManager.Get();
            if (em == null)
            {
                _equipPanel.Add(new Label("(장비 시스템 없음)"));
                return;
            }

            // [68차] Figma 15:4·15:33 개정 EquippedSection 진실치 — 65차 구 스타일(세로 등급줄/카드내
            //   핫키 배지/슬롯명 라벨/UTKSlot 인셋 박스) 폐기:
            //   카드=UTKSlot 루트 81.6(bg #21262D 60% · radius 9.6 · border 0), TierStrip=상단 가로
            //   81.6×3.6, EQ 배지 22.6×15.8(4.8,4.8) "EQ" 9.6/700 #0B0E14(무기 카드 제외),
            //   아이콘 38.4 중앙, 카드 라벨 삭제(툴팁 대체), 무기 핫키는 WeaponSlotNumbers 바로 이동.
            // [66차] 슬롯 순서 — 8 장비 슬롯 + 마지막 2칸 = 무기 로드아웃 슬롯 1·2(WeaponEquipManager 바인딩).
            //   기존 "무기"(EquipmentSlot.Weapon 원슬롯) → 로드아웃 2슬롯으로 대체 — 장착 무기는 Equipped 테두리로 표시.
            var defs = new List<(string label, EquipmentManager.EquipmentSlot slot, bool isWeapon, int loadoutIndex)>
            {
                ("투구", EquipmentManager.EquipmentSlot.Helmet, false, -1),
                ("갑옷", EquipmentManager.EquipmentSlot.Armor, false, -1),
                ("신발", EquipmentManager.EquipmentSlot.Shoes, false, -1),
                ("방패", EquipmentManager.EquipmentSlot.Back, false, -1),
                ("장갑", EquipmentManager.EquipmentSlot.Gloves, false, -1),
                ("방독면", EquipmentManager.EquipmentSlot.Mask, false, -1),
                ("분사기", EquipmentManager.EquipmentSlot.Bag, false, -1),
                ("반지", EquipmentManager.EquipmentSlot.Ring, false, -1),
                ("무기 1", EquipmentManager.EquipmentSlot.Helmet, true, 0),
                ("무기 2", EquipmentManager.EquipmentSlot.Helmet, true, 1),
            };

            float cell = InventoryClusterPanelRegions.CellSize;
            VisualElement row = null;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (i % EquipColumns == 0)
                {
                    row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.FlexStart;
                    row.style.justifyContent = Justify.FlexStart;
                    row.style.marginBottom = 0f;
                    _equipPanel.Add(row);
                }

                // [68차] 카드 루트 = UTKSlot — 클릭/툴팁 영역이 카드 전체(81.6). 구 카드 컨테이너+인셋 박스 구조 해체.
                var card = new UTKSlot();
                card.name = "EquipSlot_" + def.label;
                ApplyEquipCardBase(card);       // bg #21262D 60% + radius 9.6 + border 0 + USS margin 상쇄 + 호버 액센트
                card.SetRank("common");
                card.style.width = cell;
                card.style.height = cell;
                card.style.flexShrink = 0f;

                // [피그마 15:4] TierStrip — 상단 가로 (0,0) 81.6×3.6 (65차 세로줄 폐기)
                var strip = new VisualElement();
                strip.name = "EquipStrip_" + def.label;
                strip.style.position = Position.Absolute;
                strip.style.left = 0f;
                strip.style.top = 0f;
                strip.style.width = cell;
                strip.style.height = InventoryClusterPanelRegions.EquippedStripHeight;
                strip.style.backgroundColor = new StyleColor(EquipStripCommon);
                strip.pickingMode = PickingMode.Ignore;
                card.Add(strip);

                // [피그마 15:4] 아이콘 38.4×38.4 카드 중앙 (21.6,21.6) — USS 100% 팽창 대체
                var iconHost = card.IconHost;
                if (iconHost != null)
                {
                    iconHost.style.position = Position.Absolute;
                    iconHost.style.left = 21.6f;
                    iconHost.style.top = 21.6f;
                    iconHost.style.width = 38.4f;
                    iconHost.style.height = 38.4f;
                    iconHost.style.flexGrow = 0f;
                }

                // [피그마 15:4] EquipBadge — 좌상단 EQ 배지 22.6×15.8(4.8,4.8) "EQ" 9.6/700 (무기 슬롯은 배지 없음)
                if (!def.isWeapon)
                {
                    var badge = new VisualElement();
                    badge.name = "EquipBadge_" + def.label;
                    badge.style.position = Position.Absolute;
                    badge.style.left = 4.8f;
                    badge.style.top = 4.8f;
                    badge.style.width = 22.6f;
                    badge.style.height = 15.8f;
                    badge.style.backgroundColor = new StyleColor(EquipAccent);
                    badge.style.borderBottomLeftRadius = 3.6f;
                    badge.style.borderBottomRightRadius = 3.6f;
                    badge.style.borderTopLeftRadius = 3.6f;
                    badge.style.borderTopRightRadius = 3.6f;
                    badge.style.alignItems = Align.Center;
                    badge.style.justifyContent = Justify.Center;
                    badge.pickingMode = PickingMode.Ignore;
                    var eqText = new Label("EQ");
                    eqText.style.fontSize = 12f;   // [185차 후속] 9.6→12 가독성 조정(뱃지 15.8 높이 내 수직 중앙 유지)
                    eqText.style.unityFontStyleAndWeight = FontStyle.Bold;
                    eqText.style.color = new StyleColor(EquipBadgeText);
                    eqText.pickingMode = PickingMode.Ignore;
                    badge.Add(eqText);
                    card.Add(badge);
                }
                // [68차] 무기 카드내 핫키 배지(구 221:8 카드 배치) 삭제 — WeaponSlotNumbers 바(카드열 4·5)로 이동.

                // [68차] 하단 슬롯명 라벨 삭제 — Figma 15:4 카드에는 라벨이 없다(슬롯 정보는 툴팁 대체).

                // 우클릭 — 일반: 해제(EquipmentManager.UnequipSlot) / 무기: 장착 토글(P1 로드아웃 경로)
                var capturedDef = def;
                card.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 1) return;
                    if (capturedDef.isWeapon)
                    {
                        ToggleWeaponLoadout(capturedDef.loadoutIndex);
                    }
                    else
                    {
                        bool ok = em.UnequipSlot(capturedDef.slot);
                        Debug.Log($"[InventoryUTK] 장비 해제(우클릭) slot={capturedDef.slot} → {ok}");
                    }
                });

                row.Add(card);
                string key = def.isWeapon ? "Loadout" + def.loadoutIndex : def.slot.ToString();
                _equipSlotIcons[key] = card;
                _equipStrips[key] = strip;
            }

            BuildEquipNumbersBar();
        }

        /// <summary>
        /// [68차] WeaponSlotNumbers 바(피그마 15:4/221:8) — equipPanel-local (14.4,210) 427.2×18.
        /// 스페이서 3칸(81.6×12) + 뱃지 2칸 — 뱃지 18×18 radius 3.6 bg #F0F3F5, 숫자 10.8/900(Bold 근사)
        /// #1A1D20, 카드열 4·5(0-based 3·4) 셀 중앙 정렬. 무기 카드 자체에는 배지를 두지 않는다.
        /// </summary>
        private void BuildEquipNumbersBar()
        {
            var bar = new VisualElement { name = "EquipNumbersBar" };
            bar.style.position = Position.Absolute;
            bar.style.left = 14.4f;
            bar.style.top = 210f;
            bar.style.width = 427.2f;
            bar.style.height = 18f;
            bar.style.flexDirection = FlexDirection.Row;
            bar.pickingMode = PickingMode.Ignore;
            for (int c = 0; c < EquipColumns; c++)
            {
                var cellVe = new VisualElement { name = c < 3 ? "WeaponNumberSpacer" : "WeaponNumber" + (c - 2) };
                cellVe.style.width = 81.6f;   // 카드 열 폭과 동일 — 뱃지는 열 중앙
                cellVe.style.height = 18f;
                cellVe.style.flexShrink = 0f;
                cellVe.style.marginRight = c < EquipColumns - 1 ? InventoryClusterPanelRegions.EquipCellGap : 0f;
                // [144차 P1-INV][A33] 핫키 배지 열 중앙정렬 — Figma 221:20 배지 x=열 x+31.8(=(81.6−18)/2),
                //   HotkeyBadge layout mainAlign/crossAlign CENTER(진실치). 정적 분석상 cellVe 정렬 미설정으로
                //   좌측 배치였다. inline 명시로 USS 상속 개입 가능성 차단 — 런타임 USS 상속 잔여 가능성은
                //   Play 실측 게이트(inventory.md 보류 3번)로 별도 확인.
                cellVe.style.alignItems = Align.Center;
                cellVe.style.justifyContent = Justify.Center;
                if (c >= 3)   // 무기 카드 열 4·5
                {
                    var hot = new VisualElement();
                    hot.style.width = 18f;
                    hot.style.height = 18f;
                    hot.style.backgroundColor = new StyleColor(EquipHotkeyBg);
                    hot.style.borderBottomLeftRadius = 3.6f;
                    hot.style.borderBottomRightRadius = 3.6f;
                    hot.style.borderTopLeftRadius = 3.6f;
                    hot.style.borderTopRightRadius = 3.6f;
                    hot.style.alignItems = Align.Center;
                    hot.style.justifyContent = Justify.Center;
                    hot.pickingMode = PickingMode.Ignore;
                    var numText = new Label((c - 2).ToString());
                    numText.style.fontSize = 13.2f;   // [185차 후속] 10.8→13.2 가독성 조정(EQ 뱃지 12와 일관)
                    numText.style.unityFontStyleAndWeight = FontStyle.Bold;
                    numText.style.color = new StyleColor(EquipHotkeyText);
                    numText.pickingMode = PickingMode.Ignore;
                    hot.Add(numText);
                    cellVe.Add(hot);
                }
                bar.Add(cellVe);
            }
            _equipPanel.Add(bar);
        }

        /// <summary>[66차] 무기 로드아웃 슬롯 우클릭 — 장착 토글(Equip → 로드아웃 등록 + 핫바 자동 등록 P1 경로 재사용).</summary>
        private void ToggleWeaponLoadout(int loadoutIndex)
        {
            var entry = WeaponEquipManager.GetLoadoutSlot(loadoutIndex);
            if (string.IsNullOrEmpty(entry.ItemId))
            {
                Debug.Log($"[InventoryUTK] 무기 슬롯 {loadoutIndex + 1} 비어있음 — 토글 스킵");
                return;
            }
            if (entry.Equipped)
            {
                WeaponEquipManager.Unequip();
                Debug.Log($"[InventoryUTK] 무기 슬롯 {loadoutIndex + 1} 해제 토글 — {entry.ItemId}");
            }
            else
            {
                var playerT = GetPlayerTransform();
                if (playerT == null)
                {
                    Debug.LogWarning("[InventoryUTK] 플레이어 미발견 — 무기 장착 스킵");
                    return;
                }
                WeaponEquipManager.Equip(entry.ItemId, playerT, entry.Type);
                Debug.Log($"[InventoryUTK] 무기 슬롯 {loadoutIndex + 1} 장착 토글 — {entry.ItemId}");
            }
        }

        // 플레이어 Transform 해결 — HotbarUI.GetPlayerTransform 동일 패턴(1초 캐시)
        private Transform _cachedPlayerT;
        private float _playerCacheTime;

        private Transform GetPlayerTransform()
        {
            if (_cachedPlayerT != null && Time.unscaledTime - _playerCacheTime < 1f)
                return _cachedPlayerT;

            _cachedPlayerT = null;
            var pm = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            if (pm != null) _cachedPlayerT = pm.transform;
            if (_cachedPlayerT == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) _cachedPlayerT = tagged.transform;
            }
            _playerCacheTime = Time.unscaledTime;
            return _cachedPlayerT;
        }

        /// <summary>[U8 확장] 장비 임베드 갱신 — 아이콘/등급줄/무기 로드아웃(장착 테두리). 창 열림/장비 변경 시.</summary>
        private void RefreshEquipPanel()
        {
            var em = EquipmentManager.Instance ?? EquipmentManager.Get();
            if (em == null || _equipPanel == null) return;
            foreach (var kv in _equipSlotIcons)
            {
                string key = kv.Key;
                var iconSlot = kv.Value;
                if (iconSlot == null) continue;

                PlayerInventory.ItemData itemData = null;
                bool isLoadout = key.StartsWith("Loadout");
                bool equipped = false;

                if (isLoadout)
                {
                    // [66차] 무기 로드아웃 슬롯 — WeaponEquipManager.GetLoadoutSlot(0/1) 바인딩(P1)
                    int loadoutIndex = key[key.Length - 1] - '0';
                    var entry = WeaponEquipManager.GetLoadoutSlot(loadoutIndex);
                    equipped = entry.Equipped;
                    if (!string.IsNullOrEmpty(entry.ItemId))
                        itemData = PlayerInventory.GetItemById(entry.ItemId);
                }
                else
                {
                    var slot = (EquipmentManager.EquipmentSlot)System.Enum.Parse(typeof(EquipmentManager.EquipmentSlot), key);
                    var data = em.GetSlotData(slot);
                    if (data != null && !string.IsNullOrEmpty(data.itemId))
                    {
                        itemData = PlayerInventory.GetItemById(data.itemId);
                    }
                }

                // [P20-5] 아이콘+등급 갱신 — [68차] 등급은 상단 TierStrip 색이 담당(보더링 폐기), 빈 슬롯은 아이콘 제거
                if (itemData != null)
                {
                    iconSlot.SetIcon(ItemIconDatabase.GetOrCreateIcon(itemData));
                    iconSlot.SetRank(UTKRarity.ClassForIndex((int)itemData.rarity));
                    iconSlot.tooltip = $"{itemData.displayName}{(equipped ? " (장착 중)" : "")}";
                }
                else
                {
                    iconSlot.SetIcon(null);
                    iconSlot.SetRank("common");
                    iconSlot.tooltip = isLoadout ? $"무기 슬롯 {(key[key.Length - 1] == '0' ? 1 : 2)} (비어있음)" : null;
                }

                // [66차→68차] TierStrip 등급색 — epic+ 보라/레어 파랑/일반 회색(피그마 진실치 — 공용 판정 추출)
                if (_equipStrips.TryGetValue(key, out var strip) && strip != null)
                {
                    int r = itemData != null ? (int)itemData.rarity : 0;
                    strip.style.backgroundColor = new StyleColor(StripColorForRank(r));
                }

                // [66차→68차→144차] 무기 로드아웃 — 장착 중 슬롯 테두리 #58A6FF 2px(런타임 상태 피드백),
                //   아니면 크롬 테두리 복원([144차 A44] — 0 복원은 전 셀 테두리 신설로 폐기)
                if (isLoadout)
                {
                    if (equipped) SetSlotBorder(iconSlot, EquipAccent, 2f);
                    else SetSlotBorder(iconSlot, UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth);
                }
            }
        }

        /// <summary>[3분할] 중앙 설명창 갱신 — 슬롯 클릭 시 호출. PlayerInventory.ItemData 직접 표시.</summary>
        public void ShowItemDescription(PlayerInventory.ItemData item, int count)
        {
            if (_descName == null) return;
            if (item == null) { _descName.text = "아이템을 선택하세요"; _descText.text = ""; return; }
            _descName.text = item.displayName ?? item.id;
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(item.description)) sb.AppendLine(item.description);
            sb.AppendLine($"카테고리: {item.category}");
            sb.AppendLine($"등급: {item.rarity}");
            if (item.maxDurability > 0) sb.AppendLine($"내구도: {item.maxDurability}");
            _descText.text = sb.ToString();
            Debug.Log($"[InventoryUTK] 설명창 갱신 — {item.displayName ?? item.id}");
        }

        /// <summary>[3분할] 우측 창고 패널 표시/숨김 — 창고 상호작용 시 Show(territoryId) 경유.</summary>
        public void SetWarehouseMode(string territoryId, bool visible)
        {
            if (_warehousePanel == null || _warehouseEmptyHint == null) return;
            _warehousePanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _warehouseEmptyHint.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            Debug.Log($"[InventoryUTK] 창고 패널 {(visible ? "표시" : "숨김")} (territory={territoryId})");
        }

        /// <summary>창이 UIRoot에 부착(등록)된 후 ④ 백드롭과 ②③ 윈도우 타겟을 등록한다.</summary>
        protected override void OnWindowOpen()
        {
            RegisterWorldDrop();
            UTKDragDrop.RegisterDropTarget(this, this);
            // [134차 F] 개방즉시+250ms 기하 로그 — Figma 15:5 진실치 대조용(셸/첫 행 5셀 rect/장착 패널).
            // +16.6px 이동 감지는 InventoryClusterPanelRegions+계약테스트 고정으로 본 차수 미구현 — 본 로그가 판정 데이터.
            LogGeometry("개방즉시");
            schedule.Execute(() => LogGeometry("250ms")).StartingIn(250);
        }

        /// <summary>[134차 F] 인벤 기하 실측 — 창 박스 + 첫 행 5셀(InvSlot_0~4) rect + 장착 패널 바운스.
        /// 진실치 대조: docs/134_FIGMA_4FRAMES_TRUTH.md ③ — 셀 82×82 r9.6 gap9.6, 열 x 24/115.2/206.4/297.6/388.8, rows y390~, EquippedSection(24,144) 456×230.</summary>
        private void LogGeometry(string phase)
        {
            var cells = new System.Text.StringBuilder();
            for (int i = 0; i < 5; i++)
            {
                var cell = Content != null ? Content.Q("InvSlot_" + i) : null;
                cells.Append($"InvSlot_{i}={(cell != null ? cell.worldBound.ToString() : "미발견")} ");
            }
            var equipPanel = Content != null ? Content.Q("InventoryEquipmentBody") : null;
            Debug.Log($"[134차 F] 인벤 기하({phase}) — winBound={worldBound} display={style.display.value} " +
                      $"winStyle=({style.width.value.value:F1}x{style.height.value.value:F1}) " +
                      $"{cells}equipPanel={(equipPanel != null ? equipPanel.worldBound.ToString() : "미발견")}");
        }

        /// <summary>창 닫힘 — 창 타겟 해제(재열림 시 재등록).</summary>
        protected override void OnWindowClosed()
        {
            UTKDragDrop.UnregisterDropTarget(this);
            UnsubscribeEquip();
        }

        // =====================================================================
        //  ⑥ 즉시 갱신 — 폴링 + 장비 이벤트
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshGrid();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        private void EnsureEquipSubscription()
        {
            var em = EquipmentManager.Get();
            if (!ReferenceEquals(_subscribedEquip, em))
            {
                if (_subscribedEquip != null) _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribedEquip = em;
                if (_subscribedEquip != null) _subscribedEquip.OnEquipmentChanged += OnEquipmentChanged;
            }
        }

        private void UnsubscribeEquip()
        {
            if (_subscribedEquip != null)
            {
                _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribedEquip = null;
            }
        }

        /// <summary>⑤ 장비 해제 수신 — 외부에서 해제가 일어나면 즉시 갱신만(해제 자체는 Equipment 창 라운드).</summary>
        private void OnEquipmentChanged(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            if (!IsOpen) return;
            Debug.Log($"[InventoryUTK] 장비 변경 수신(slot={slot}, item={itemId ?? "none"}) — 그리드 갱신");
            RefreshEquipPanel();   // [U8 확장] 장비 임베드 즉시 갱신
            // [66차 수리] force — 명시 이벤트 경로는 PointerOverUI/드래그 가드 우회.
            //   기존: 해제 직후 커서가 UI 위 → 250ms 폴링이 계속 스킵 → "장비→인벤 바로 표기 안 됨".
            RefreshGrid(force: true);
        }

        // =====================================================================
        //  ① 통합 그리드 — 매 갱신 재조회
        // =====================================================================

        /// <summary>[Figma 15:4 탭 필터 매핑] 전체=전부, 무기=Weapon, 방어구=Armor+Accessory,
        /// 소모품=Food+Potion+Drug, 재료=Herb+Meat+Material.
        /// Quest/Tool/Arrow/Bomb는 Figma 탭 미분류 — "전체" 탭에서만 노출한다(탭 필터에서 제외).</summary>
        internal static bool MatchesActiveTab(int tabIndex, PlayerInventory.ItemCategory category)
        {
            switch (tabIndex)
            {
                case 0: return true;   // 전체 = 전부
                case 1: return category == PlayerInventory.ItemCategory.Weapon;                       // 무기
                case 2: return category == PlayerInventory.ItemCategory.Armor                          // 방어구
                            || category == PlayerInventory.ItemCategory.Accessory;
                case 3: return category == PlayerInventory.ItemCategory.Food                           // 소모품
                            || category == PlayerInventory.ItemCategory.Potion
                            || category == PlayerInventory.ItemCategory.Drug;
                case 4: return category == PlayerInventory.ItemCategory.Herb                           // 재료
                            || category == PlayerInventory.ItemCategory.Meat
                            || category == PlayerInventory.ItemCategory.Material;
                default: return true;
            }
        }

        /// <summary>[Figma 15:4] 활성 탭 시각 — 활성: accent fill #58A6FF + 라벨 #0B0E14 + 스트로크 #000000@0.0 1.2 /
        ///   비활성: 투명 배경(fill #000000@0.0 = 코드 투명과 동치 — 144차 알파 재검증) + 라벨 #8B949E
        ///   + 크롬 스트로크 #30363D@0.502 1.2([144차 P1-INV][A14/A15] — 15:25/15:27 진실치).</summary>
        private void ApplyTabVisuals()
        {
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var tab = _tabButtons[i];
                if (tab == null) continue;
                bool active = i == _activeTabIndex;
                tab.style.backgroundColor = new StyleColor(active ? UTKTheme.Accent : new Color(0f, 0f, 0f, 0f));
                // Button은 TextElement — 자체 color가 라벨색이다.
                tab.style.color = new StyleColor(active ? UTKTheme.BgBase : UTKTheme.TextSub);
                // [144차 P1-INV][A14/A15] 탭 스트로크 — 활성 #000000 실효알파 0(진실치 구조 일치, 렌더 무영향),
                //   비활성 #30363D@0.502 1.2 — [144차 P0 V2] 공용 어휘.
                SetSlotBorder(tab, active ? new Color(0f, 0f, 0f, 0f) : UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth);
            }
        }

        /// <summary>[Figma 15:4] 탭 클릭 — 활성 탭 전환 + 가방 그리드 필터 재렌더(강제).</summary>
        public void SetActiveTab(int index)
        {
            if (index < 0 || index >= _tabButtons.Count || index == _activeTabIndex)
                return;
            _activeTabIndex = index;
            ApplyTabVisuals();
            // 탭 클릭은 포인터가 UI 위에서 일어나므로 PointerOverUI 가드를 우회한다(force).
            RefreshGrid(force: true);
            Debug.Log($"[InventoryUTK] 카테고리 탭 전환: {TabLabels[index]}");
        }

        /// <summary>[Figma 15:4 푸터] 값 갱신 — [71차] 사용량+골드(지갑 단일소스)+진행바 fill 통합 갱신.
        ///   무게 데이터 부재로 "사용 N/40" 슬롯 기반 표기(위조 금지 규약) 유지, fill 폭 = 진행바 폭 × 사용률.</summary>
        private void UpdateWeightFooter(int usedSlots, int capacity)
        {
            if (_weightValue != null)
                _weightValue.text = $"사용 {usedSlots}/{capacity}";
            // [71차] 골드 라벨 — 지갑(PlayerStats) 단일소스. 전리품 골드 수령 → GoldChanged → 즉시 증가.
            UTKGoldBalance.Refresh(_goldValue);
            if (_progressFill != null && capacity > 0)
            {
                float progressWidth = InventoryClusterPanelRegions.ProgressRow.width;
                _progressFill.style.width = progressWidth * Mathf.Clamp01(usedSlots / (float)capacity);
            }
        }

        /// <summary>[71차] dumbbell 절차 아이콘 — 16.8×16.8 #8B949E 3rect(좌우 추 + 중앙 바).
        ///   Figma WeightTitle 벡터의 기하 모사(위조 데이터 아님 — 시각 아이콘).</summary>
        private static VisualElement BuildDumbbellIcon()
        {
            var icon = new VisualElement { name = "DumbbellIcon" };
            icon.style.position = Position.Absolute;
            icon.style.width = 16.8f;
            icon.style.height = 16.8f;
            icon.pickingMode = PickingMode.Ignore;
            Color c = UTKTheme.TextSub;
            void Plate(float x, float y, float w, float h)
            {
                var r = new VisualElement();
                r.style.position = Position.Absolute;
                r.style.left = x; r.style.top = y;
                r.style.width = w; r.style.height = h;
                r.style.backgroundColor = new StyleColor(c);
                r.pickingMode = PickingMode.Ignore;
                icon.Add(r);
            }
            Plate(1.2f, 3.6f, 2.4f, 9.6f);    // 좌 추
            Plate(13.2f, 3.6f, 2.4f, 9.6f);   // 우 추
            Plate(4.8f, 7.2f, 7.2f, 2.4f);    // 중앙 바
            return icon;
        }

        // ── [71차] GoldChanged 구독 수명 — Show/Attach 시 구독, Hide/Detach 시 해제(중복·누수 방지). ──

        private void EnsureGoldSubscription()
        {
            if (_goldSubscribed) return;
            if (ProjectName.Core.PlayerStats.Instance == null) return;
            ProjectName.Core.PlayerStats.GoldChanged -= OnGoldChangedForFooter;   // 안전 — 중복 방지
            ProjectName.Core.PlayerStats.GoldChanged += OnGoldChangedForFooter;
            _goldSubscribed = true;
        }

        private void UnsubscribeGold()
        {
            if (!_goldSubscribed) return;
            if (ProjectName.Core.PlayerStats.Instance != null)
                ProjectName.Core.PlayerStats.GoldChanged -= OnGoldChangedForFooter;
            _goldSubscribed = false;
        }

        private void OnGoldChangedForFooter(ProjectName.Core.PlayerStats.GoldLedgerEntry entry)
        {
            UTKGoldBalance.Refresh(_goldValue);   // 전리품 골드 수령 등 — 창 개방 중 즉시 갱신
        }

        private void RefreshGrid(bool force = false)
        {
            // [U8 수리] 드래그 중 셀 재생성 금지 — 캡처 상실로 드래그 도중 취소되는 뿌리 차단
            // (탭 클릭 등 명시 경로는 force로 가드를 우회한다.)
            if (!force && UTKDragDrop.Active) return;
            // [P16-3 수리] 커서가 UI 위면 재생성 스킵 — 250ms 폴링마다 셀이 새로 만들어져
            //   hover 중인 슬롯(특히 등급 테두리 아이템)이 찰나 리셋 = "반반 짤려 반짝" 뿌리.
            //   포인터가 떠나면 다음 폴링에 정상 재생성. 클릭/드래그/우클릭 경로는 즉시 갱신 유지.
            if (!force && ProjectName.Core.UITransitionState.PointerOverUI) return;

            var inv = PlayerInventory.Instance;
            var slots = inv != null ? inv.GetAllSlots() : null;
            int total = slots != null ? slots.Length : 0;

            // [Figma 15:4 탭 필터] 활성 탭 카테고리에 해당하는 "사용 중" 슬롯의 원본 인덱스만 그린다.
            // 저장소 계약 불변 — PlayerInventory 데이터 구조는 건드리지 않고 표시 단계에서만 필터링한다.
            var visibleIndices = new List<int>(total);
            int usedSlots = 0;
            for (int i = 0; i < total; i++)
            {
                var item = slots[i]?.item;
                if (item == null || slots[i].count <= 0) continue;
                // [70차] 골드는 지갑 단일소스 — 인벤 그리드에 미표시(구세이드 잔존 골드 슬롯도 노출 금지).
                if (slots[i].item != null && slots[i].item.id == "gold") continue;
                usedSlots++;
                if (MatchesActiveTab(_activeTabIndex, item.category))
                    visibleIndices.Add(i);
            }
            int viewCount = visibleIndices.Count;

            // 이전 슬롯 드롭 타겟 해제
            for (int i = 0; i < _slotTargets.Count; i++)
            {
                var cell = _slotTargets[i];
                UTKDragDrop.UnregisterDropTarget(cell);
                if (cell != null && cell.parent == _grid)
                    cell.RemoveFromHierarchy();
            }
            _slotTargets.Clear();
            _grid.Clear();

            int rows = Mathf.Max(BagRows, viewCount > 0 ? (viewCount + Columns - 1) / Columns : 0);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    // 필터된 뷰 위치(pos) → 원본 슬롯 인덱스 매핑(뷰 밖 = 빈 셀 가이드 -1).
                    int pos = r * Columns + c;
                    int idx = pos < viewCount ? visibleIndices[pos] : -1;
                    _grid.Add(BuildSlotCell(slots, idx, total));
                }
            }
            // [Figma 15:4 WeightRow] 사용 슬롯 수 갱신 — capacity는 인벤 배열 크기(_maxSlots=40).
            UpdateWeightFooter(usedSlots, slots != null ? total : InventoryClusterPanelRegions.InventoryMaxSlots);
            ApplyInventoryBodyLayout();
        }

        private VisualElement BuildSlotCell(PlayerInventory.ItemSlot[] slots, int idx, int total)
        {
            var cell = new UTKSlot();
            cell.name = "InvSlot_" + idx;
            // [Figma 정합 v3] 셀은 raw Figma px(81.6/9.6) — 등배수는 디자인공간 스케일이 담당.
            float cellSize = InventoryClusterPanelRegions.CellSize;
            float cellGap = InventoryClusterPanelRegions.CellGap;
            cell.style.width = cellSize;
            cell.style.height = cellSize;
            cell.style.flexShrink = 0f;
            cell.style.marginTop = 0f;
            cell.style.marginBottom = cellGap;
            cell.style.marginLeft = 0f;
            cell.style.marginRight = cellGap;
            ApplyDarkSlotBase(cell);   // [144차 A43/A44] Figma 15:4 셀 — bg #21262D 60% · radius 9.6 · 테두리 크롬 1.2 · 아이콘 38.4 · Count 우하단

            // [68차] 상단 TierStrip — 등급색(빈 셀=common 회색), Figma GridRow 셀 실측(81.6×3.6 최상단)
            var cellStrip = new VisualElement { name = "InvStrip" };
            cellStrip.style.position = Position.Absolute;
            cellStrip.style.left = 0f;
            cellStrip.style.top = 0f;
            cellStrip.style.width = cellSize;
            cellStrip.style.height = InventoryClusterPanelRegions.EquippedStripHeight;
            cellStrip.style.backgroundColor = new StyleColor(EquipStripCommon);
            cellStrip.pickingMode = PickingMode.Ignore;
            cell.Add(cellStrip);

            if (idx >= 0 && idx < total && slots[idx] != null && slots[idx].item != null && slots[idx].count > 0)
            {
                var slotData = slots[idx];
                cell.SetIcon(ItemIconDatabase.GetOrCreateIcon(slotData.item));
                cell.SetCount(slotData.count);
                cell.SetRank(UTKRarity.ClassForIndex((int)slotData.item.rarity));
                cellStrip.style.backgroundColor = new StyleColor(StripColorForRank((int)slotData.item.rarity));   // [68차] 등급=상단 스트립

                // ③ 인벤 소스 드래그(좌/우) + 좌클릭 설명 + 우클릭 비드래그=사용/장착
                int globalIdx = idx;
                var item = slotData.item;
                var slotRef = slotData;
                UTKDragDrop.MakeDraggable(cell, () => MakeSlotPayload(globalIdx, item),
                    () => OnSlotClick(globalIdx),
                    () => OnSlotRightClick(globalIdx, slotRef, item));

                // ③ 인벤 타겟 — 슬롯 위 드롭 = 교체(스왑) (슬롯이 윈도우 타겟보다 상위 우선)
                IUTKDropTarget slotTarget = new SlotDropTarget(this, globalIdx);
                UTKDragDrop.RegisterDropTarget(cell, slotTarget);
                _slotTargets.Add(cell);
            }
            else
            {
                cell.SetRank("common");   // 빈 셀 가이드
                // [68차] 빈 셀 스트립 = common 회색(생성 시 기본값 유지 — 보더링 폐기)
            }

            return cell;
        }

        private UTKDragPayload MakeSlotPayload(int slotIndex, PlayerInventory.ItemData item)
        {
            var p = new UTKDragPayload();
            p.Source = UTKDragSourceKind.Inventory;
            p.SourceIndex = slotIndex;
            p.Item = item;
            p.Icon = item != null ? ItemIconDatabase.GetOrCreateIcon(item) : null;
            return p;
        }

        private void OnSlotClick(int slotIndex)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;
            var slots = inv.GetAllSlots();
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;
            var slotData = slots[slotIndex];
            if (slotData == null || slotData.item == null)
            {
                _selectedItemData = null;
                ItemDescriptionWindowUTK.Clear();
                return;
            }
            _selectedItemData = slotData.item;
            // [Figma 15:4] 푸터는 WeightRow 전용 — 선택 피드백은 상세창(ItemDescriptionWindowUTK, 클러스터 v3)이 담당한다.
            ItemDescriptionWindowUTK.ShowItem(slotData.item, slotData.count);   // [독립 창] 중앙 설명창 갱신
            Debug.Log($"[InventoryUTK] 슬롯 선택(클릭): {slotData.item.displayName} (슬롯 {slotIndex})");
        }

        // [68차] 무기 id 해석은 WeaponItemResolver 단일소스로 위임 — private 맵 중복 해체
        // (HotbarUIUTK 숫자키 들기와 동일 규약. 규약: 레거시 InventoryWindow의 장비 id 해석 규칙 유지.)
        private static bool TryResolveWeaponEquip(string itemId, out string equipId, out WeaponType type)
            => WeaponItemResolver.TryResolve(itemId, out equipId, out type);

        private static EquipmentManager.EquipmentSlot MapArmorSlot(string id)
        {
            string s = (id ?? "").ToLowerInvariant();
            if (s.Contains("helmet") || s.Contains("투구")) return EquipmentManager.EquipmentSlot.Helmet;
            if (s.Contains("shoe") || s.Contains("boot") || s.Contains("신발")) return EquipmentManager.EquipmentSlot.Shoes;
            if (s.Contains("glove") || s.Contains("장갑")) return EquipmentManager.EquipmentSlot.Gloves;
            if (s.Contains("mask") || s.Contains("마스크")) return EquipmentManager.EquipmentSlot.Mask;
            if (s.Contains("pack") || s.Contains("bag") || s.Contains("가방")) return EquipmentManager.EquipmentSlot.Bag;
            if (s.Contains("cape") || s.Contains("망토") || s.Contains("shield") || s.Contains("방패") || s.EndsWith("_back")) return EquipmentManager.EquipmentSlot.Back;
            return EquipmentManager.EquipmentSlot.Armor;
        }

        /// <summary>[U8 요구] 가방 우클릭(비드래그) — 창고 열림 중=입고 / 소모품=사용 / 무기·방어구=장착.</summary>
        private void OnSlotRightClick(int slotIndex, PlayerInventory.ItemSlot slotRef, PlayerInventory.ItemData item)
        {
            // [U8 요구] 창고가 열려 있으면 우클릭 = 입고 (창고↔인벤 이동)
            var wh = WarehouseWindowUTK.Instance;
            if (wh != null && wh.IsOpen)
            {
                bool deposited = wh.DepositFromInventory(slotIndex, item);
                if (deposited) RefreshGrid();
                return;
            }

            var cat = item.category;
            // [Milestone G] 비밀 통행증 사용 — 소모 + 비밀상점 활성 + 상점 창 열기
            if (item.id == SecretShopSystem.SecretPassItemId)
            {
                if (SecretShopSystem.ConsumeSecretPass())
                {
                    RefreshGrid();
                    ItemDescriptionWindowUTK.Clear();
                    Debug.Log("[InventoryUTK] 비밀 통행증 사용 — 비밀상점 활성");
                    ShopWindowUTK.Open(); // Active 상태이므로 비밀상점 섹션 표시 (멱등 Open)
                }
                else Debug.LogWarning("[InventoryUTK] 비밀 통행증 없음 — 사용 불가");
                return;
            }
            if (cat == PlayerInventory.ItemCategory.Weapon || cat == PlayerInventory.ItemCategory.Armor)
            {
                // 시스템을 직접 호출해 레거시 InventoryWindow 인스턴스 유무와 무관하게 장착한다.
                var playerT = GameObject.FindWithTag("Player")?.transform;
                if (slotRef?.item == null) return;
                if (playerT == null)
                {
                    Debug.LogWarning("[InventoryWindow] Player 없음 — 장착 스킵");
                    RefreshGrid();
                    return;
                }

                if (cat == PlayerInventory.ItemCategory.Weapon)
                {
                    // InventoryWindow.TryResolveWeaponEquip과 같은 id/type 해석 규칙.
                    if (!TryResolveWeaponEquip(item.id, out string equipId, out WeaponType wType))
                    {
                        Debug.Log($"[Equip] 우클릭 장착 {item.id} → 결과 실패(사유: 무기 id 해석 실패)");
                        RefreshGrid();
                        return;
                    }
                    WeaponEquipManager.Equip(equipId, playerT, wType);
                    bool ok = WeaponEquipManager.CurrentId == equipId;
                    Debug.Log(ok
                        ? $"[Equip] 우클릭 장착 {item.id} → 결과 성공 (equipId={equipId}, type={wType})"
                        : $"[Equip] 우클릭 장착 {item.id} → 결과 실패(사유: 모델/손본 로드 실패 — equipId={equipId}, 매니저 로그 참조)");
                }
                else
                {
                    var em = EquipmentManager.Get();
                    if (em == null)
                    {
                        Debug.Log($"[Equip] 우클릭 장착 {item.id} → 결과 실패(사유: EquipmentManager 생성 불가 — 비플레이)");
                        RefreshGrid();
                        return;
                    }
                    var equipSlot = MapArmorSlot(item.id);
                    Debug.Log($"[Equip] 우클릭 장착 {item.id} → EquipmentManager.EquipItem 호출 (slot={equipSlot}, cat={item.category})");
                    bool equipped = em.EquipItem(slotRef, equipSlot);
                    Debug.Log(equipped
                        ? $"[Equip] 우클릭 장착 {item.id} → 결과 성공 ({equipSlot}, 인벤에서 1개 제거됨)"
                        : $"[Equip] 우클릭 장착 {item.id} → 결과 실패(사유: 기존 장비 해제 실패 or 인벤 가득 — {equipSlot})");
                }

                RefreshGrid();
                Debug.Log($"[InventoryUTK] 장착(우클릭): {item.displayName}");
                return;
            }
            if (cat == PlayerInventory.ItemCategory.Potion
                || cat == PlayerInventory.ItemCategory.Food
                || cat == PlayerInventory.ItemCategory.Herb
                || cat == PlayerInventory.ItemCategory.Drug)
            {
                var pi = PlayerInventory.Instance;
                if (pi != null)
                {
                    pi.UseItem(slotIndex);
                    RefreshGrid();
                    ItemDescriptionWindowUTK.Clear();
                    Debug.Log($"[InventoryUTK] 소모품 사용(우클릭): {item.displayName}");
                }
                return;
            }
            Debug.Log($"[InventoryUTK] 우클릭 — {item.displayName} (사용/장착 대상 아님)");
        }

        // =====================================================================
        //  IUTKDropTarget — ② Loot 수령 / ③ 패널 위 인벤 취소
        // =====================================================================

        public bool CanDrop(UTKDragPayload payload)
        {
            if (payload == null || payload.Item == null) return false;
            // Loot → 인벤 수령 / 창고 → 인벤 출고 / 인벤 → 패널 위(빈 영역)는 소비(취소 — 땅 드롭 방지)
            return payload.Source == UTKDragSourceKind.Loot
                || payload.Source == UTKDragSourceKind.Warehouse
                || payload.Source == UTKDragSourceKind.Inventory;
        }

        public bool Drop(UTKDragPayload payload)
        {
            if (payload == null || payload.Item == null) return false;

            if (payload.Source == UTKDragSourceKind.Loot)
            {
                TakeLootToInventory(payload);
                return true;
            }
            if (payload.Source == UTKDragSourceKind.Inventory)
            {
                // 인벤 그리드 위(빈 셀/패널) 드롭 = 소비(취소) — 아이템 유지. 슬롯 교체는 슬롯 타겟이 담당.
                return true;
            }
            if (payload.Source == UTKDragSourceKind.Warehouse)
            {
                // [U8 수리] 창고 → 인벤 출고 (드래그) — WarehouseSystem.TransferToInventory 단일 경로
                var ws = WarehouseSystem.Instance;
                if (ws == null) return false;
                bool ok = ws.TransferToInventory(payload.TerritoryId, payload.SourceIndex, 1);
                if (ok) RefreshGrid();
                Debug.Log($"[InventoryUTK] 창고→인벤 출고(드래그): {payload.Item.displayName} (성공={ok})");
                return ok;
            }
            return false;
        }

        // =====================================================================
        //  데이터 경로 (원본 시그니처 실측 맵핑)
        // =====================================================================

        /// <summary>② Loot→인벤: PlayerInventory.AddItem (원본 TryTakeDraggedToInventory 데이터 경로).</summary>
        private void TakeLootToInventory(UTKDragPayload payload)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return;
            bool ok = inv.AddItem(payload.Item, 1);
            if (ok) RefreshGrid();
            Debug.Log($"[InventoryUTK] 전리품→인벤 이동(드래그): {payload.Item.displayName} (성공={ok})");
        }

        /// <summary>③ 인벤 내 슬롯 교체(스왑) — 원본 슬롯 교체 경로와 동일한 배열 스왑.</summary>
        private bool SwapSlots(int from, int to)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return false;
            var all = inv.GetAllSlots();
            if (all == null || from < 0 || from >= all.Length || to < 0 || to >= all.Length || from == to)
                return false;
            var tmp = all[from];
            all[from] = all[to];
            all[to] = tmp;
            RefreshGrid();
            Debug.Log($"[InventoryUTK] 슬롯 교체(드래그): {from} ↔ {to}");
            return true;
        }

        /// <summary>④ 인벤 밖 드롭 = 땅에 바구니 — 원본 TryDropDraggedToTerrain(false)와 동일 API.</summary>
        private bool DropToTerrain(UTKDragPayload payload)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null || payload == null || payload.Item == null) return false;

            // 1) 커서 레이캐스트 → 지형 지점 (미스 시 플레이어 전방 폴백)
            Vector3 spawnPos;
            Camera cam = Camera.main;
            Vector2 panelPos = UTKDragDrop.LastDropPos;
            RaycastHit hit;
            if (cam != null
                && Physics.Raycast(cam.ScreenPointToRay(new Vector3(panelPos.x, cam.scaledPixelHeight - panelPos.y, 0f)),
                    out hit, 500f))
            {
                spawnPos = hit.point;
            }
            else
            {
                var playerT = GameObject.FindWithTag("Player")?.transform;
                if (playerT == null)
                {
                    Debug.Log($"[InventoryUTK] 지형 버림 취소(사유: 레이캐스트 미스 + Player 없음) — {payload.Item.displayName} 유지");
                    return false;
                }
                spawnPos = playerT.position + playerT.forward * 1.5f;
            }

            // 2) 소모: 인벤에서 1개 제거 (실패 시 롤백 — 취소)
            if (!inv.RemoveItem(payload.Item.id, 1))
            {
                Debug.Log($"[InventoryUTK] 지형 버림 취소(사유: 인벤에서 제거 실패) — {payload.Item.displayName} 유지");
                return false;
            }

            // 3) 바구니 스폰 + 1개 담기
            var basket = LootBasket.Create(spawnPos);
            if (basket == null)
            {
                Debug.LogError($"[InventoryUTK] 지형 버림 — LootBasket 생성 실패({spawnPos}), {payload.Item.displayName} 소모됨");
                return false;
            }
            basket.AddItem(payload.Item, 1);
            RefreshGrid();
            Debug.Log($"[InventoryUTK] 지형에 버림: {payload.Item.displayName} → 바구니 ({spawnPos}) — E키로 회수");
            return true;
        }

        // ─────────────────────────────── ④ 월드 백드롭 ───────────────────────────────

        private static IUTKDropTarget _worldDrop;
        private static bool _worldDropAttached;

        /// <summary>UIRoot 전체를 인벤-세계 드롭 백드롭으로 등록 (멱등).</summary>
        private void RegisterWorldDrop()
        {
            if (_worldDropAttached) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;

            _worldDrop = new WorldDropTarget();

            UTKDragDrop.RegisterDropTarget(root, _worldDrop);
            _worldDropAttached = true;
        }

        /// <summary>③ 슬롯 드롭 타겟 — 인벤 소스가 다른 슬롯 위 드롭 → 슬롯 교체(스왑).</summary>
        private sealed class SlotDropTarget : IUTKDropTarget
        {
            private readonly InventoryWindowUTK _owner;
            private readonly int _slotIndex;

            public SlotDropTarget(InventoryWindowUTK owner, int slotIndex)
            {
                _owner = owner;
                _slotIndex = slotIndex;
            }

            public bool CanDrop(UTKDragPayload payload)
            {
                return payload != null && payload.Item != null
                    && payload.Source == UTKDragSourceKind.Inventory
                    && payload.SourceIndex >= 0 && payload.SourceIndex != _slotIndex;
            }

            public bool Drop(UTKDragPayload payload)
                => _owner.SwapSlots(payload.SourceIndex, _slotIndex);
        }

        /// <summary>④ 월드 드롭 백드롭 — 인벤 소스가 UI 밖(월드)에 드롭 → 땅에 바구니. [66차] 백드롭 마커 — 구체 타겟(핫바 슬롯 등) 존재 시 양보.</summary>
        private sealed class WorldDropTarget : IUTKBackdropDropTarget
        {
            public bool CanDrop(UTKDragPayload payload)
            {
                return payload != null && payload.Item != null
                    && payload.Source == UTKDragSourceKind.Inventory;
            }

            public bool Drop(UTKDragPayload payload)
                => _instance != null && _instance.DropToTerrain(payload);
        }
    }
}