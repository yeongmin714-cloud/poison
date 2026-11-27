using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;    // GuardPlaceholder, GuardSquadHotbar
using ProjectName.Core;   // PlayerInventory
using ProjectName.UI;     // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U2 Round 2B — 하단 핫바 (HotbarUI 849줄 uGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/HotbarUI.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 8슬롯 하단 중앙 핫바 (원본 SlotCount=8, Alpha1~8 대응).
    ///   ② 각 슬롯 숫자 라벨(1~8) + 아이콘 — UTKSlot.SetIcon(ItemIconDatabase.GetOrCreateIcon).
    ///   ③ 데이터 소스 재사용 — 원본 HotbarUI와 동일한 PlayerPrefs 키(poison_hotbar_{i})를
    ///      읽고 써서 상호 연동 (등록/해제가 양쪽에 반영).
    ///   ④ 등록 해제 — 슬롯 우클릭(button==1) → PlayerPrefs 클리어 + 아이콘 제거.
    ///   ⑤ GLB 아이콘은 비동기 베이크라 즉시 null일 수 있음 → MonoBehaviour 1초 주기 재시도 (원본 Update 선례).
    ///
    /// 상시 노출 바 — UTKWindowBase(윈도우/닫기)가 아닌 순수 VisualElement(rootVisualElement 직속).
    /// 셀프 부트스트랩 + Updater로 UIRoot 부착 (StatusWindowUTK/ HotbarUI Bootstrap 선례).
    /// </summary>
    public class HotbarUIUTK : VisualElement
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static HotbarUIUTK _instance;
        public static HotbarUIUTK Instance => _instance;

        private const string PrefsIdKey   = "poison_hotbar_{0}";        // 원본과 동일 키
        private const string PrefsNameKey = "poison_hotbar_name_{0}";   // 원본과 동일 키

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        /// <summary>씬 셋업에서 명시적으로 보장할 수 있는 멱등 UTK 핫바 부트스트랩.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new HotbarUIUTK();
            var go = new GameObject("HotbarUIUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().bar = _instance;
            Debug.Log("[HotbarUTK] Ensure 완료 — 하단 핫바(8슬롯) 준비");
        }

        // ===== 설정 =====
        private const int   SlotCount   = 8;        // 원본 SlotCount 대응
        private const float SlotSize    = 96f; // Archived 23:6 SlotBox
        private const float SlotGap     = 19.2f;
        private const float FigmaLeft   = 630f;     // [144차 P8-HB #14] 629.6 → 630 — QuickSlotsHUD 23:68 ax 원문 정합
        private const float FigmaTop    = 861.6f;
        private const float FigmaWidth  = 902.4f;
        private const float FigmaHeight = 124.8f;

        // [144차 P8-HB] Figma 진실색 — docs/figma_truth_144/hud__23_6.json 원문.
        private static readonly Color ItemSlotBg = new Color(0.07f, 0.05f, 0.03f, 0.85f);   // #120D08@0.85 — 2026-10-10 사용자 계약(부대모드 슬롯색 통일). Figma 23:80 #0D1015@0.902와 상이 — 계약 우선(충돌 기록)
        private static readonly Color SlotFillActive = new Color32(0x1D, 0x24, 0x30, 0xE6); // #1D2430@0.902 — 23:98 활성 목업 fill
        private static readonly Color FigmaGold = new Color32(0xD4, 0xAF, 0x37, 0xFF);      // #D4AF37 — 23:99 활성 테두리 / 39:33 TabBadge
        private static readonly Color FigmaBadgeFill = new Color32(0xF0, 0xF3, 0xF5, 0xFF); // #F0F3F5 — 23:87 HotkeyBadge / 39:31 HintIcon
        private static readonly Color FigmaBadgeText = new Color32(0x1A, 0x1D, 0x20, 0xFF); // #1A1D20 — 23:88 배지 숫자 / 39:37 회전 글리프
        private static readonly Color FigmaStrokeDark = new Color32(0x0D, 0x10, 0x15, 0xFF); // #0D1015 — 23:87 배지 stroke / 39:34 TAB 텍스트
        private static readonly Color FigmaCaption = new Color32(0x62, 0x6A, 0x73, 0xFF);   // #626A73 — 23:142 캡션
        private static readonly Color FigmaBracket = new Color32(0x8C, 0x73, 0x4B, 0x40);   // #8C734B@0.251 — 23:70 브래킷 라인

        // [계획 v2 Phase 2] 화면 하단 고정 앵커 여백 — 스케일 후 핫바 시각 하단이 화면 하단에서 12px 위에 고정.
        // 하단 고정 앵커는 사용자 요구(캡처 간 세로 편차 제거) — Figma 캔버스 y=861.6 좌표 규약과 의도적 차이(계획 v2 Phase 2 기록).
        // (FigmaTop은 생성자 초기값용으로만 잔존 — 매 프레임 ApplyFigmaBounds가 하단 앵커로 덮어씀.)
        public  const float HotbarBottomMarginPx = 12f;

        private readonly UTKSlot[] _slots;
        private readonly Label[]   _keyLabels;
        private readonly string[]  _assignedIds;    // 슬롯별 등록 itemId (PlayerPrefs 반영본)
        private readonly string[]  _assignedNames;
        private bool _squadMode;                    // [U8 요구] Tab 전환 모드 — false=아이템, true=부대 지정
        private GuardPlaceholder[] _squadReps;      // [U8 배선] 부대 대표 병사 (정보창 진입용)
        private IVisualElementScheduledItem _pulseSchedule;

        private HotbarUIUTK()
        {
            name = "Hotbar";
            AddToClassList("utk-hotbar");
            style.position = Position.Absolute;
            style.left = FigmaLeft;
            style.top = FigmaTop;
            style.width = FigmaWidth;
            style.height = FigmaHeight;
            style.alignItems = Align.FlexStart;
            style.flexDirection = FlexDirection.Column;
            pickingMode = PickingMode.Position;

            _slots = new UTKSlot[SlotCount];
            _keyLabels = new Label[SlotCount];
            _assignedIds = new string[SlotCount];
            _assignedNames = new string[SlotCount];

            var row = new VisualElement();
            row.name = "HotbarRow";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.width = FigmaWidth;
            row.style.height = FigmaHeight;
            Add(row);

            for (int i = 0; i < SlotCount; i++)
            {
                var cell = new VisualElement();
                cell.name = "HotbarCell_" + i;
                cell.style.flexDirection = FlexDirection.Column;
                cell.style.alignItems = Align.Center;
                cell.style.width = SlotSize;
                cell.style.height = FigmaHeight;
                cell.style.marginRight = i < SlotCount - 1 ? SlotGap : 0f;
                cell.style.alignItems = Align.FlexStart;
                cell.style.flexShrink = 0f;
                row.Add(cell);

                var slot = new UTKSlot();
                slot.name = "Slot_" + i;
                slot.style.width = SlotSize;
                slot.style.height = SlotSize;
                // [144차 P8-HB #5 기록] Figma 23:86 SlotBox fx "INNER_SHADOW:2.4:#FFFFFF/0.031" —
                //   UI Toolkit 런타임은 inner shadow 미지원 → 미구현 기록(보더/코너 브래킷으로 외곽 표현 대체).
                // [144차 P8-HB #1] 코너 브래킷 — 23:69 corner-brackets(라인 8개)를 슬롯 4모서리에 적용.
                AddSlotCornerBrackets(slot);
                cell.Add(slot);

                var keyLabel = new Label((i + 1).ToString());
                keyLabel.name = "Key_" + i;
                keyLabel.style.position = Position.Absolute;
                keyLabel.style.left = 36f;
                keyLabel.style.top = 84f;
                keyLabel.style.width = 24f;
                keyLabel.style.height = 24f;
                // [144차 P8-HB #7] HotkeyBadge 숫자(23:88) — #8B949E → #1A1D20, Geist Mono 900:
                //   FontSubtitle(13.2) 크기 유지 + Bold 근사(단일 폰트 폴백 — UTKTheme.StyleFigmaText 규약).
                keyLabel.style.fontSize = UTKTheme.FontSubtitle;   // [Figma 23:88] 13.2
                keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;   // 900 → Bold 근사(폴백 유지)
                keyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                keyLabel.style.color = new StyleColor(FigmaBadgeText);
                // [144차 P8-HB #6] HotkeyBadge fill(23:87) — #1A1A1F@0.9 → #F0F3F5(밝은 배지).
                keyLabel.style.backgroundColor = new StyleColor(FigmaBadgeFill);
                // [144차 P8-HB #8] HotkeyBadge(23:87) r4.8 + stroke #0D1015 1.8 신설.
                UTKTheme.ApplyRadius(keyLabel, 4.8f);
                UTKTheme.ApplyElementBorder(keyLabel, FigmaStrokeDark, 1.8f);
                cell.Add(keyLabel);

                _slots[i] = slot;
                _keyLabels[i] = keyLabel;

                UTKSlot capturedSlot = slot;
                int capturedIndex = i;
                slot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    // [U8 배선] 부대 모드 좌클릭 = 병사 정보창 (UTK)
                    if (_squadMode && evt.button == 0 && _squadReps != null && capturedIndex < _squadReps.Length && _squadReps[capturedIndex] != null)
                    {
                        GuardInfoUTK.Open(_squadReps[capturedIndex]);
                        return;
                    }
                    if (evt.button == 1 && !_squadMode) UnregisterSlot(capturedIndex);
                });

                // [U8 요구] 좌클릭 드래그 드롭 = 핫바에 아이템 등록 (마인크래프트식)
                UTKDragDrop.RegisterDropTarget(slot, new HotbarSlotDropTarget(capturedIndex));
            }

            var tabHint = new VisualElement { name = "TabRotationHint" };
            tabHint.style.position = Position.Absolute;
            tabHint.style.left = 926f;      // [144차 P8-HB #15] 926.4 → 926 — 39:30 ax 원문 정합
            tabHint.style.top = 28f;        // [144차 P8-HB #15] 28.8 → 28 — 39:30 ay 원문 정합
            tabHint.style.width = 57.6f;
            tabHint.style.height = 67.2f;
            tabHint.style.alignItems = Align.Center;
            // [144차 P8-HB #11] 힌트 프레임 배경 제거 — 39:30 fills 원문 부재(투명). 구 #121721@0.82 + r6 프레임 스타일 삭제.
            tabHint.pickingMode = PickingMode.Ignore;

            // [144차 P8-HB #10] HintIcon 배지 신설 — 39:31: 36×36 r9.6 fill #F0F3F5 + stroke #0D1015 1.8,
            //   오프셋 (11,16) — 5532/1114 − 5521/1098. (DROP_SHADOW 4.8 #000@0.4는 UTK 미지원 → 기록.)
            var hintIcon = new VisualElement { name = "HintIconBadge" };
            hintIcon.style.position = Position.Absolute;
            hintIcon.style.left = 11f;
            hintIcon.style.top = 16f;
            hintIcon.style.width = 36f;
            hintIcon.style.height = 36f;
            UTKTheme.ApplyRadius(hintIcon, 9.6f);
            hintIcon.style.backgroundColor = new StyleColor(FigmaBadgeFill);
            UTKTheme.ApplyElementBorder(hintIcon, FigmaStrokeDark, 1.8f);
            hintIcon.pickingMode = PickingMode.Ignore;
            var rotateHint = new Label("↻");
            rotateHint.style.position = Position.Absolute;
            rotateHint.style.left = 8f;      // 배지 내부 오프셋 — 39:36 (5540−5532, 1122−1114)
            rotateHint.style.top = 8f;
            rotateHint.style.width = 19.2f;  // "↻" 글리프 19.2 근사 유지 — 39:36
            rotateHint.style.height = 19.2f;
            rotateHint.style.fontSize = 15.6f;
            rotateHint.style.color = new StyleColor(FigmaBadgeText);   // #1A1D20 — 39:37 벡터 스트로크(밝은 배지 대비)
            rotateHint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hintIcon.Add(rotateHint);
            tabHint.Add(hintIcon);

            var tabLabel = new Label("TAB");
            tabLabel.name = "TabBadge";   // ApplyFigmaBounds 이름 쿼리용
            tabLabel.style.position = Position.Absolute;
            tabLabel.style.left = 14.4f;
            tabLabel.style.top = 45.6f;
            tabLabel.style.width = 28.8f;
            tabLabel.style.height = 21.6f;
            // [144차 P8-HB #12/#13] TabBadge(39:33) — fill #262B33@0.95 → #D4AF37(골드), r4.8 +
            //   stroke #0D1015 1.8 신설. TAB 텍스트(39:34) #8B949E → #0D1015(JSON 원문 확인),
            //   Geist Mono 900 → Bold 근사(폴백 유지).
            tabLabel.style.fontSize = UTKTheme.FontBadge;   // [Figma 39:34] 12
            tabLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            tabLabel.style.color = new StyleColor(FigmaStrokeDark);
            tabLabel.style.backgroundColor = new StyleColor(FigmaGold);
            UTKTheme.ApplyRadius(tabLabel, 4.8f);
            UTKTheme.ApplyElementBorder(tabLabel, FigmaStrokeDark, 1.8f);
            tabLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            tabHint.Add(tabLabel);
            Add(tabHint);

            // [144차 P8-HB #2] 캡션 — 23:142 "HOTKEY QUICK SLOTS · ACTIVE COMBAT MODE",
            //   13.2 Geist 500 Medium #626A73, 바 하단 오프셋 (394,116) — 4989/1186 − 4595/1070. 폰트 폴백 유지.
            var caption = new Label("HOTKEY QUICK SLOTS · ACTIVE COMBAT MODE");
            caption.name = "HotbarCaption";
            caption.style.position = Position.Absolute;
            caption.style.left = 394f;
            caption.style.top = 116f;
            caption.style.width = 292f;
            caption.style.height = 17f;
            caption.style.fontSize = UTKTheme.FontSubtitle;   // 13.2 — 23:142
            caption.style.unityFontStyleAndWeight = FontStyle.Normal;   // Geist 500 → Normal 근사
            caption.style.color = new StyleColor(FigmaCaption);
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            caption.pickingMode = PickingMode.Ignore;
            Add(caption);

            // 원본 PlayerPrefs 데이터 소스 로드
            LoadAssignedFromPrefs();
            RefreshAllIcons();
            UTKWindowBase.ApplyUIToolkitFont(this);
            EnsureDragPadFeedbackWired();   // [65차 P3] 인벤→핫바 드래그 숫자패드 피드백
        }

        /// <summary>[144차 P8-HB #1] 슬롯 코너 브래킷 — Figma 23:69 corner-brackets(라인 8개: 가로 w9.6 +
        /// 세로 h9.6, #8C734B@0.251, 굵기 2.4)를 슬롯 4모서리에 적용(대각선 브래킷). UTKTheme.AddCornerDecals
        /// (L자 2면 보더형)과 다른 슬라이드 라인형 — 얇은 라인 요소를 직접 구현(코너당 가로+세로 2라인).
        /// PickingMode.Ignore — 슬롯 좌클릭/우클릭/드래그드롭 무손상.</summary>
        private static void AddSlotCornerBrackets(VisualElement slot)
        {
            if (slot == null) return;
            const float arm = 9.6f;    // 브래킷 팔 길이 — 23:70
            const float thick = 2.4f;  // 브래킷 두께 — 23:70 strokeW
            var color = new StyleColor(FigmaBracket);
            void Line(string lineName, float x, float y, float w, float h)
            {
                var line = new VisualElement { name = lineName };
                line.style.position = Position.Absolute;
                line.style.left = x;
                line.style.top = y;
                line.style.width = w;
                line.style.height = h;
                line.style.backgroundColor = color;
                line.pickingMode = PickingMode.Ignore;
                slot.Add(line);
            }
            // 좌상 / 우상 / 좌하 / 우하 — 코너당 가로+세로 라인(SlotSize 96 기준 내부 정렬)
            Line("BracketTL_H", 0f, 0f, arm, thick);
            Line("BracketTL_V", 0f, 0f, thick, arm);
            Line("BracketTR_H", SlotSize - arm, 0f, arm, thick);
            Line("BracketTR_V", SlotSize - thick, 0f, thick, arm);
            Line("BracketBL_H", 0f, SlotSize - thick, arm, thick);
            Line("BracketBL_V", 0f, SlotSize - arm, thick, arm);
            Line("BracketBR_H", SlotSize - arm, SlotSize - thick, arm, thick);
            Line("BracketBR_V", SlotSize - thick, SlotSize - arm, thick, arm);
        }

        // =====================================================================
        //  [65차 P3] 인벤→핫바 드래그 숫자패드 피드백 — 드래그 중 1~8 키 라벨 강조,
        //  커서 아래 슬롯의 숫자를 하이라이트(드롭 타겟 안내). 종료 시 원복.
        // =====================================================================
        private static bool _dragPadWired;
        private bool _dragPadActive;
        private int _padHover = -1;
        private static readonly Color PadIdleBg = FigmaBadgeFill;   // [144차 P8-HB #6] #1A1A1F@0.9 → #F0F3F5 — 배지 fill 정합 원복색
        private static readonly Color PadGlowBg = new Color(0.345f, 0.651f, 1f);       // #58A6FF
        private static readonly Color PadGlowText = new Color(0.043f, 0.055f, 0.078f); // #0B0E14

        private static void EnsureDragPadFeedbackWired()
        {
            if (_dragPadWired) return;
            _dragPadWired = true;
            // 정적 이벤트 — 싱글턴 교체 시에도 Instance 경유로 최신 인스턴스 라우팅(누수 없음)
            UTKDragDrop.DragMoved += pos => Instance?.OnDragPadMove(pos);
            UTKDragDrop.DragEnded += () => Instance?.OnDragPadEnd();
        }

        private void OnDragPadMove(Vector2 panelPos)
        {
            if (!_dragPadActive)
            {
                _dragPadActive = true;
                // 숫자패드 모드 진입 — 1~8 숫자를 강조색으로 표시(드롭 가능 슬롯 안내)
                for (int i = 0; i < SlotCount; i++)
                    _keyLabels[i].style.color = new StyleColor(PadGlowBg);
            }

            int hover = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                var gb = _slots[i].worldBound;
                if (gb.width <= 0f || gb.height <= 0f) continue;
                if (panelPos.x >= gb.x && panelPos.x <= gb.x + gb.width
                    && panelPos.y >= gb.y && panelPos.y <= gb.y + gb.height)
                {
                    hover = i;
                    break;
                }
            }
            if (hover == _padHover) return;

            if (_padHover >= 0 && _padHover < SlotCount)
            {
                _keyLabels[_padHover].style.backgroundColor = new StyleColor(PadIdleBg);
                _keyLabels[_padHover].style.color = new StyleColor(PadGlowBg);
            }
            _padHover = hover;
            if (_padHover >= 0)
            {
                _keyLabels[_padHover].style.backgroundColor = new StyleColor(PadGlowBg);
                _keyLabels[_padHover].style.color = new StyleColor(PadGlowText);
            }
        }

        private void OnDragPadEnd()
        {
            if (!_dragPadActive) return;
            _dragPadActive = false;
            for (int i = 0; i < SlotCount; i++)
            {
                _keyLabels[i].style.backgroundColor = new StyleColor(PadIdleBg);
                _keyLabels[i].style.color = new StyleColor(FigmaBadgeText);   // [144차 P8-HB #7] #8B949E → #1A1D20 원복 정합
            }
            _padHover = -1;
        }

        // =====================================================================
        //  데이터 소스 — 원본 HotbarUI와 동일 PlayerPrefs 키 연동
        // =====================================================================

        private void LoadAssignedFromPrefs()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _assignedIds[i] = PlayerPrefs.GetString(string.Format(PrefsIdKey, i), null);
                _assignedNames[i] = PlayerPrefs.GetString(string.Format(PrefsNameKey, i), null);
            }
        }

        private static string IdKey(int index)   => string.Format(PrefsIdKey, index);
        private static string NameKey(int index) => string.Format(PrefsNameKey, index);

        public bool IsSquadMode => _squadMode;

        // =====================================================================
        //  [68차 P3] 핫바 숫자키 들기 — 아이템 모드 전용 1~8 키 (상단행=InputSystem, 눌패드=legacy)
        //  판정은 ResolveHoldAction 순수 정적 함수(EditMode 테스트 대상 — 키 시뮬 불가 회피),
        //  실행은 TryActivateHoldSlot(런타임 의존). 부대 모드 선택은 GuardSquadHotbar.HandleSelectKeys
        //  단일소스 — 여기서 건드리지 않는다. 65차 숫자패드 드래그 피드백(DragMoved/DragEnded) 보존.
        // =====================================================================

        /// <summary>[68차 P3] 숫자키 입력 판정 결과 — 들기/장착/토글해제/스킵.</summary>
        public enum HotbarHoldAction { None, WeaponEquip, ItemHold, ToggleOff }

        /// <summary>
        /// [68차 P3] 숫자키 판정 순수 함수 — (슬롯 아이템id, 부대모드 여부, F 홀드 여부,
        /// 해당 슬롯 아이템이 현재 활성(장착/홀드) 중인지, 무기 아이템 여부) → 행동.
        ///   부대 모드/F 홀드/빈 슬롯 → None(스킵)  ·  동일 활성 재입력 → ToggleOff
        ///   무기 → WeaponEquip  ·  비무기 → ItemHold.
        /// </summary>
        public static HotbarHoldAction ResolveHoldAction(string slotItemId, bool isSquadMode, bool fHeld,
            bool sameSlotActive, bool isWeaponItem)
        {
            if (isSquadMode) return HotbarHoldAction.None;   // 부대 모드 = GuardSquadHotbar 단일소스
            if (fHeld) return HotbarHoldAction.None;         // F+숫자 = 부대 등록(HandleCtrlAssignKeys) 전용
            if (string.IsNullOrEmpty(slotItemId)) return HotbarHoldAction.None;
            if (sameSlotActive) return HotbarHoldAction.ToggleOff;
            return isWeaponItem ? HotbarHoldAction.WeaponEquip : HotbarHoldAction.ItemHold;
        }

        /// <summary>
        /// [68차 P3] 숫자키 폴링 — Updater에서 매 프레임 호출. 아이템 모드에서만 동작.
        /// 상단 행 = InputSystem kb.digit1~8Key.wasPressedThisFrame,
        /// 눌패드 = legacy Input.GetKeyDown(KeyCode.Keypad1+i) (GuardSquadHotbar 259-275행 선례).
        /// F 홀드(InputSystem kb.fKey.isPressed 또는 legacy KeyCode.F)면 스킵 — F+숫자는 부대 등록 전용.
        /// </summary>
        public void HandleHoldNumberKeys()
        {
            if (_squadMode) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            bool fHeld = kb.fKey.isPressed || Input.GetKey(KeyCode.F);
            if (fHeld) return;
            for (int i = 0; i < SlotCount; i++)
            {
                bool pressed = kb[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame
                    || Input.GetKeyDown(KeyCode.Keypad1 + i);
                if (pressed) TryActivateHoldSlot(i, fHeld);
            }
        }

        /// <summary>
        /// [68차 P3] 슬롯 아이템 들기/장착/해제 — 숫자키 단일 진입점.
        /// 무기 = WeaponItemResolver.TryResolve → WeaponEquipManager.Equip,
        /// 비무기 = GblItemIconRenderer.ResolveModelKey → ItemHoldManager.Hold.
        /// 동일 활성 슬롯 재입력 = 해제 토글(무기=Unequip / 아이템=Unhold), 다른 슬롯 = 교체
        /// (Equip/Hold 내부 상호 정리 경유).
        /// </summary>
        public void TryActivateHoldSlot(int index, bool fHeld)
        {
            if (index < 0 || index >= SlotCount) return;
            string itemId = _assignedIds[index];
            if (string.IsNullOrEmpty(itemId)) { Debug.Log($"[HotbarUTK] 슬롯 {index + 1} 빈 슬롯 — 들기 스킵"); return; }

            PlayerInventory.ItemData item = FindInventoryItem(itemId) ?? PlayerInventory.GetItemById(itemId);
            if (item == null) { Debug.LogWarning($"[HotbarUTK] 슬롯 {index + 1} 아이템 조회 실패: {itemId} — 들기 스킵"); return; }

            bool isWeapon = item.category == PlayerInventory.ItemCategory.Weapon;
            var action = ResolveHoldAction(itemId, _squadMode, fHeld, IsSlotItemActive(itemId, item), isWeapon);
            switch (action)
            {
                case HotbarHoldAction.None:
                    return;
                case HotbarHoldAction.ToggleOff:
                    if (IsBombItem(item))
                    {
                        // [187차 후속] 폭탄 내려놓기 — 같은 슬롯 재입력 토글.
                        BombArmController.Ensure().Disarm();
                        Debug.Log($"[HotbarUTK] 슬롯 {index + 1} 폭탄 내려놓기 — {itemId}");
                    }
                    else if (isWeapon)
                    {
                        WeaponEquipManager.Unequip();
                        Debug.Log($"[HotbarUTK] 슬롯 {index + 1} 무기 해제 토글 — {itemId}");
                    }
                    else
                    {
                        ItemHoldManager.Unhold();
                        Debug.Log($"[HotbarUTK] 슬롯 {index + 1} 홀드 해제 토글 — {itemId}");
                    }
                    return;
                case HotbarHoldAction.WeaponEquip:
                    BombArmController.Instance?.Disarm();   // [187차 후속] 무기 장착 시 폭탄 무장 해제 — 단일 손 홀드 정합
                    if (WeaponItemResolver.TryResolve(itemId, out var equipId, out var type))
                    {
                        var player = GameObject.FindWithTag("Player")?.transform;   // InventoryWindowUTK 1179행 선례
                        WeaponEquipManager.Equip(equipId, player, type);
                        Debug.Log($"[HotbarUTK] 슬롯 {index + 1} 무기 장착 — {itemId} → {equipId}/{type}");
                    }
                    else
                        Debug.LogWarning($"[HotbarUTK] 무기 equipId 해석 실패: {itemId} — no-op");
                    return;
                case HotbarHoldAction.ItemHold:
                    if (IsBombItem(item))
                    {
                        // [187차 후속] 폭탄 들기 = 무장 — 은퇴한 QuickSlotUI 경로(UtkActive 가드) 대체.
                        //  좌클릭 투척은 PlayerCombat 폭탄 분기 → BombArmController.ThrowTowardCursor(185차 애니 연계).
                        BombArmController.Ensure().Arm(item);
                        Debug.Log($"[HotbarUTK] 슬롯 {index + 1} 폭탄 무장 — {itemId} — 좌클릭으로 투척");
                        return;
                    }
                    BombArmController.Instance?.Disarm();   // [187차 후속] 비폭탄 홀드 전환 시 폭탄 무장 해제 — 단일 손 홀드 정합
                    string modelKey = GblItemIconRenderer.ResolveModelKey(item);
                    if (string.IsNullOrEmpty(modelKey))
                    {
                        Debug.LogWarning($"[HotbarUTK] 홀드 모델키 해석 실패: {itemId} — no-op (예외 아님)");
                        return;
                    }
                    var playerHold = GameObject.FindWithTag("Player")?.transform;
                    ItemHoldManager.Hold(modelKey, playerHold);
                    Debug.Log($"[HotbarUTK] 슬롯 {index + 1} 아이템 홀드 — {itemId} → {modelKey}");
                    return;
            }
        }

        /// <summary>[187차 후속] 폭탄 판정 — null 안전 래퍼(ItemData.isBomb).</summary>
        private static bool IsBombItem(PlayerInventory.ItemData item)
            => item != null && item.isBomb;

        /// <summary>[68차 P3] 슬롯 아이템이 현재 활성(장착/홀드) 중인지 — 동일 재입력 토글 판정 근거.</summary>
        private static bool IsSlotItemActive(string itemId, PlayerInventory.ItemData item)
        {
            if (item == null) return false;
            if (item.isBomb)
            {
                // [187차 후속] 폭탄 활성 판정 — 무장 중이고 같은 폭탄이면 동일 재입력 토글(ToggleOff) 트리거.
                var bam = BombArmController.Instance;
                return bam != null && bam.IsArmed && bam.ArmedBomb != null && bam.ArmedBomb.id == itemId;
            }
            if (item.category == PlayerInventory.ItemCategory.Weapon)
            {
                if (!WeaponItemResolver.TryResolve(itemId, out var equipId, out var type)) return false;
                return WeaponEquipManager.CurrentId == equipId && WeaponEquipManager.CurrentType == type;
            }
            string mk = GblItemIconRenderer.ResolveModelKey(item);
            if (string.IsNullOrEmpty(mk)) return false;
            return ItemHoldManager.IsHolding && ItemHoldManager.HeldModelKey == mk;
        }

        // ── [68차 P3] 홀드 중 슬롯 테두리 피드백 — 활성 슬롯 액센트, 이탈 시 인라인 해제 원복 ──
        // [144차 P8-HB #13] 활성 슬롯 Figma 정합(23:98 QuickSlot-3 활성 목업) — 테두리 #58A6FF 2px →
        //   골드 #D4AF37 2.4 + fill #1D2430@0.902. JSON 활성 목업은 골드 단독(등급 테두리 없음) →
        //   활성 중 인라인이 UTKRarity 등급 테두리를 덮고(활성 우선 — 기존 인라인 덮어쓰기 경로 유지),
        //   해제 시 StyleKeyword.Null 원복으로 등급 클래스 스타일 복구(충돌 없음).
        private static readonly Color HoldActiveBorder = FigmaGold;   // #D4AF37 — 23:99
        private const float HoldActiveBorderWidth = 2.4f;             // 23:98 strokeW
        private bool _holdBorderDirty = true;
        private string _lastHoldStateSig;

        /// <summary>홀드/장착 상태 시그니처 — 변화 없으면 DOM 갱신 스킵(매 프레임 GC 억제).</summary>
        private void UpdateHoldBorderFeedback()
        {
            string sig = $"{WeaponEquipManager.CurrentId}|{(int)WeaponEquipManager.CurrentType}"
                + $"|{ItemHoldManager.IsHolding}|{ItemHoldManager.HeldModelKey}|{(_squadMode ? 1 : 0)}";
            if (!_holdBorderDirty && sig == _lastHoldStateSig) return;
            _lastHoldStateSig = sig;
            _holdBorderDirty = false;
            for (int i = 0; i < SlotCount; i++)
                ApplyHoldBorder(_slots[i], !_squadMode && IsSlotItemActive(_assignedIds[i]));
        }

        private static bool IsSlotItemActive(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            var item = FindInventoryItem(itemId) ?? PlayerInventory.GetItemById(itemId);
            return IsSlotItemActive(itemId, item);
        }

        private static void ApplyHoldBorder(VisualElement slot, bool active)
        {
            if (slot == null) return;
            if (active)
            {
                var c = new StyleColor(HoldActiveBorder);
                var w = new StyleFloat(HoldActiveBorderWidth);
                slot.style.borderTopColor = c; slot.style.borderBottomColor = c;
                slot.style.borderLeftColor = c; slot.style.borderRightColor = c;
                slot.style.borderTopWidth = w; slot.style.borderBottomWidth = w;
                slot.style.borderLeftWidth = w; slot.style.borderRightWidth = w;
                slot.style.backgroundColor = new StyleColor(SlotFillActive);   // [144차 P8-HB #13] 활성 fill — 23:98 #1D2430@0.902
            }
            else
            {
                // 원복 — 인라인 스타일 해제(StyleKeyword.Null)로 등급 테두리 클래스 스타일 복구
                var nc = new StyleColor(StyleKeyword.Null);
                var nw = new StyleFloat(StyleKeyword.Null);
                slot.style.borderTopColor = nc; slot.style.borderBottomColor = nc;
                slot.style.borderLeftColor = nc; slot.style.borderRightColor = nc;
                slot.style.borderTopWidth = nw; slot.style.borderBottomWidth = nw;
                slot.style.borderLeftWidth = nw; slot.style.borderRightWidth = nw;
                slot.style.backgroundColor = new StyleColor(ItemSlotBg);   // [144차 P8-HB #13] 비활성 원복 — 계약색 #120D08@0.85
            }
        }

        /// <summary>[U8 요구] Tab 전환 — 아이템 모드 ↔ 부대 지정 모드 (동일 8슬롯 정렬).</summary>
        public void ToggleSquadMode()
        {
            _squadMode = !_squadMode;

            // [2026-09-27 부대 드래그 연동] 부대 모드 상태를 GuardSelectionManager에 동기 —
            //   부대 모드(true)일 때 Ctrl 없이 드래그로 병사 지정 · 우클릭 이동이 되도록.
            //   (U8 게이트로 Tab 전환은 UTK가 담당 — 원본 GuardSquadHotbar.SetSquadMode와 이중 동기,
            //    동일 값을 쓰므로 충돌 없음. 평상 시 false 유지해 좌클릭 공격 보존)
            ProjectName.Systems.GuardSelectionManager.squadModeActive = _squadMode;

            RefreshAllIcons();

            // 전환 펄스 애니 — 스케일 1.15 → 1.0 (150ms, unscaled)
            if (_pulseSchedule != null)
            {
                _pulseSchedule.Pause();
                _pulseSchedule = null;
            }
            style.scale = new StyleScale(new Scale(new Vector2(1.15f, 1.15f)));
            float t = 0f;
            IVisualElementScheduledItem pulseSchedule = null;
            pulseSchedule = schedule.Execute(() =>
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(1f - t / 0.15f);
                float s = 1f + 0.15f * k;
                style.scale = new StyleScale(new Scale(new Vector2(s, s)));
                if (k <= 0f)
                {
                    style.scale = StyleKeyword.Null;
                    pulseSchedule.Pause();
                    if (_pulseSchedule == pulseSchedule) _pulseSchedule = null;
                }
            });
            pulseSchedule.Every(16L);
            pulseSchedule.ExecuteLater(0);
            _pulseSchedule = pulseSchedule;

            Debug.Log($"[HotbarUTK] 모드 전환 → {(_squadMode ? "부대 지정" : "아이템")}");
        }

        /// <summary>[U8 요구] 부대 모드 렌더 — 원본 GuardSquadHotbar._slots 리플렉션 데이터(병사 아바타).</summary>
        private void RefreshSquadSlots()
        {
            if (_squadReps == null || _squadReps.Length != SlotCount) _squadReps = new GuardPlaceholder[SlotCount];
            var original = Object.FindAnyObjectByType<ProjectName.UI.GuardSquadHotbar>();
            if (original == null)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    _slots[i].SetIcon(null);
                    _slots[i].SetCount(0);
                    _slots[i].SetRank("common");
                }
                return;
            }

            // 원본 GuardSquadHotbar private _slots 리플렉션 실측 (GuardSquadHotbarUTK 검증 경로와 동일)
            var field = typeof(GuardSquadHotbar).GetField("_slots",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var groups = field != null ? field.GetValue(original) as GuardPlaceholder[][] : null;
            for (int i = 0; i < SlotCount; i++)
            {
                var members = (groups != null && i < groups.Length) ? groups[i] : null;
                int alive = 0;
                GuardPlaceholder representative = null;
                if (members != null)
                {
                    foreach (var g in members)
                    {
                        if (g == null || !g.IsAlive) continue;
                        alive++;
                        if (representative == null) representative = g;
                    }
                }

                _squadReps[i] = representative;
                if (representative != null)
                {
                    var icon = GuardIconRenderer.GetOrCreateIcon(representative);
                    _slots[i].SetIcon(icon);
                    _slots[i].SetCount(alive);
                    _slots[i].SetRank("common");
                }
                else
                {
                    _slots[i].SetIcon(null);
                    _slots[i].SetCount(0);
                    _slots[i].SetRank("common");
                }
            }
        }

        /// <summary>
        /// 등록 API — 원본 HotbarUI.AssignItem과 동일 데이터 경로(PlayerPrefs 키)로 아이템 지정.
        /// 몸 장비(방어구)는 핫바 지정 금지 (원본 AssignItem 가드 동일).
        /// </summary>
        public static void AssignItem(int index, PlayerInventory.ItemData item)
        {
            if (_instance == null || item == null) return;
            if (index < 0 || index >= SlotCount) return;
            if (item.category == PlayerInventory.ItemCategory.Armor)
            {
                Debug.Log($"[HotbarUTK] 몸 장비는 핫바 지정 불가 — 장비창으로: {item.displayName}");
                return;
            }

            PlayerPrefs.SetString(IdKey(index), item.id);
            PlayerPrefs.SetString(NameKey(index), item.displayName);
            PlayerPrefs.Save();

            _instance._assignedIds[index] = item.id;
            _instance._assignedNames[index] = item.displayName;
            _instance.RefreshSlot(index);
            Debug.Log($"[HotbarUTK] 슬롯 {index + 1}에 '{item.displayName}' 등록");
        }

        /// <summary>등록 해제 — PlayerPrefs 클리어 + 아이콘 제거 (아이템은 인벤에 유지).</summary>
        public static void UnregisterSlot(int index)
        {
            if (_instance == null) return;
            if (index < 0 || index >= SlotCount) return;
            if (string.IsNullOrEmpty(_instance._assignedIds[index])) return;

            string removed = _instance._assignedNames[index] ?? _instance._assignedIds[index];

            PlayerPrefs.DeleteKey(IdKey(index));
            PlayerPrefs.DeleteKey(NameKey(index));
            PlayerPrefs.Save();

            _instance._assignedIds[index] = null;
            _instance._assignedNames[index] = null;
            _instance.RefreshSlot(index);
            Debug.Log($"[HotbarUTK] 슬롯 {index + 1} 등록 해제 — '{removed}' (아이템 인벤 유지)");
        }

        // =====================================================================
        //  아이콘 갱신
        // =====================================================================

        private void RefreshAllIcons()
        {
            // [2026-10-10 사용자 요구] 부대 모드 슬롯 색 통일 — 구 청록 틴트(0.10,0.22,0.30)는 파란 슬롯으로
            //   보였다. 부대 모드도 아이템 모드와 동일한 핫바 슬롯색(다크 브라운)을 쓴다(모드 구분은 아바타/라벨이 담당).
            // [144차 P8-HB #13] 슬롯 fill — 비활성 = 계약색(ItemSlotBg #120D08@0.85) 유지, 아이템 모드
            //   활성 슬롯만 Figma 활성 목업 fill(SlotFillActive #1D2430@0.902 — 23:98)로 덮음(골드 테두리와 한 세트).
            for (int i = 0; i < SlotCount; i++)
            {
                bool holdActive = !_squadMode && IsSlotItemActive(_assignedIds[i]);
                _slots[i].style.backgroundColor = new StyleColor(holdActive ? SlotFillActive : ItemSlotBg);
            }

            if (_squadMode) { RefreshSquadSlots(); return; }   // [U8 요구] 부대 모드 — 동일 8슬롯에 부대 렌더
            for (int i = 0; i < SlotCount; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int index)
        {
            _holdBorderDirty = true;   // [68차 P3] 슬롯 데이터 변화 — 테두리 피드백 재계산 트리거
            string itemId = _assignedIds[index];
            var slot = _slots[index];
            if (slot == null) return;

            if (string.IsNullOrEmpty(itemId))
            {
                slot.SetIcon(null);
                slot.SetCount(0);
                slot.SetRank("common");
                return;
            }

            // 인벤 슬롯에서 ItemData 우선 조회 (수량 표기 + 아이콘), 실패 시 정적 정의 폴백
            PlayerInventory.ItemData item = FindInventoryItem(itemId);
            if (item == null)
                item = PlayerInventory.GetItemById(itemId);

            if (item != null)
            {
                slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                slot.SetRank(UTKRarity.ClassForIndex((int)item.rarity));
                // 스택(>1)만 수량 표기 — 단독 무기/방어구 "1" 노이즈 방지
                slot.SetCount(GetInventoryCount(itemId));
            }
            else
            {
                slot.SetIcon(null);
                slot.SetCount(0);
                slot.SetRank("common");
            }
        }

        // =====================================================================
        //  인벤토리 조회 헬퍼
        // =====================================================================

        private static PlayerInventory.ItemData FindInventoryItem(string itemId)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return null;
            var slots = inv.GetAllSlots();
            if (slots == null) return null;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s != null && s.item != null && s.item.id == itemId)
                    return s.item;
            }
            return null;
        }

        private static int GetInventoryCount(string itemId)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return 0;
            var slots = inv.GetAllSlots();
            if (slots == null) return 0;
            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s != null && s.item != null && s.item.id == itemId)
                    total += s.count;
            }
            return total;
        }

        // =====================================================================
        //  갱신 / 부착용 Updater — 원본 Update() (1초 아이콘 재시도 + count 반영)
        // =====================================================================

        private class Updater : MonoBehaviour
        {
            public HotbarUIUTK bar;
            private float _tick = 1f;
            private bool _invSubscribed;   // [193차] 인벤 수량 이벤트 구독 1회 플래그

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && bar != null && bar.parent == null)
                    root.Add(bar);
                if (root != null && bar != null)
                    bar.ApplyFigmaBounds(root);

                if (bar == null) return;

                // [U8 요구] Tab 직접 폴링 — 아이템 모드 ↔ 부대 지정 모드 전환 (동일 8슬롯 정렬)
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.tabKey.wasPressedThisFrame)
                {
                    bar.ToggleSquadMode();
                    // 원본 GuardSquadHotbar 모드 동기 (1~8 선택 로직 단일소스 유지)
                    var original = Object.FindAnyObjectByType<ProjectName.UI.GuardSquadHotbar>();
                    if (original != null)
                    {
                        var f = typeof(ProjectName.UI.GuardSquadHotbar).GetField("_squadMode",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (f != null) f.SetValue(original, bar.IsSquadMode);
                    }
                }

                // [68차 P3] 핫바 숫자키 들기 — 아이템 모드 전용 (F 홀드/부대 모드는 HandleHoldNumberKeys 게이트에서 스킵)
                bar.HandleHoldNumberKeys();
                // [68차 P3] 홀드/장착 중 슬롯 테두리 피드백 — 상태 변화 시에만 DOM 갱신
                bar.UpdateHoldBorderFeedback();

                // [193차] 인벤 수량 변동 이벤트 구독 — 투척 즉시 개수 갱신(1초 틱 폴백 유지)
                if (!_invSubscribed && ProjectName.Core.PlayerInventory.Instance != null)
                {
                    ProjectName.Core.PlayerInventory.Instance.OnInventoryChanged += bar.RefreshAllIcons;
                    _invSubscribed = true;
                }

                // GLB 아이콘 비동기 베이크 재시도 + 인벤 수량 변동 반영 — 1초 주기 (원본 Update 관례)
                _tick -= Time.unscaledDeltaTime;
                if (_tick <= 0f)
                {
                    _tick = 1f;
                    if (bar.parent != null)
                        bar.RefreshAllIcons();
                }
            }
        }

        private void ApplyFigmaBounds(VisualElement root)
        {
            if (root == null) return;
            Vector2 rootSize = new Vector2(root.resolvedStyle.width, root.resolvedStyle.height);
            if (rootSize.x <= 0f || rootSize.y <= 0f) return;
            // [계획 v2 Phase 2] X만 기존 축 분율 앵커 유지, Y는 화면 하단 고정 앵커로 일원화. 크기는 등배수 k.
            // top = rootSize.y - FigmaHeight * k - HotbarBottomMarginPx:
            //   transformOrigin(0,0)+scale=k라서 스케일 후 시각 높이 = FigmaHeight*k → 시각 하단이 화면 하단에서 12px 위에 고정.
            //   anchorY는 등배수 k 산출에만 사용(핫바 Y 배치에는 미사용).
            // 하단 고정 앵커는 사용자 요구(캡처 간 세로 편차 제거) — Figma 캔버스 y=861.6 좌표 규약과 의도적 차이(계획 v2 Phase 2 기록).
            float anchorX = rootSize.x / FigmaCanvasLayout.CanvasWidth;
            float anchorY = rootSize.y / FigmaCanvasLayout.CanvasHeight;
            float k = Mathf.Min(anchorX, anchorY);
            style.position = Position.Absolute;
            style.left = FigmaLeft * anchorX;
            style.top = rootSize.y - FigmaHeight * k - HotbarBottomMarginPx;
            style.width = FigmaWidth;
            style.height = FigmaHeight;
            style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            style.scale = new StyleScale(new Scale(new Vector2(k, k)));
            float cellWidth = SlotSize;
            float slotHeight = SlotSize;
            float cellHeight = FigmaHeight;
            float horizontalGap = Mathf.Max(0f, (FigmaWidth - SlotCount * cellWidth) / (SlotCount - 1));
            var row = this.Q<VisualElement>("HotbarRow");
            if (row != null)
            {
                row.style.width = FigmaWidth;
                row.style.height = FigmaHeight;
            }

            var hint = this.Q<VisualElement>("TabRotationHint");
            if (hint != null)
            {
                hint.style.left = 926f; hint.style.top = 28f;   // [144차 P8-HB #15] (926.4,28.8) → (926,28) — 39:30 원문
                hint.style.width = 57.6f; hint.style.height = 67.2f;
                // [144차 P8-HB #10] HintIcon 배지 — 글리프 오프셋을 배지 내부 (8,8)로(39:36). 이름 쿼리로 정합.
                var iconBadge = hint.Q<VisualElement>("HintIconBadge");
                var rotation = iconBadge != null ? iconBadge.Q<Label>() : null;
                if (rotation != null)
                {
                    rotation.style.left = 8f;
                    rotation.style.top = 8f;
                    rotation.style.width = 19.2f;
                    rotation.style.height = 19.2f;
                }
                var badge = hint.Q<Label>("TabBadge");
                if (badge != null)
                {
                    badge.style.left = 14.4f;
                    badge.style.top = 45.6f;
                    badge.style.width = 28.8f;
                    badge.style.height = 21.6f;
                }
            }

            for (int i = 0; i < SlotCount; i++)
            {
                var cell = this.Q<VisualElement>("HotbarCell_" + i);
                if (cell == null) continue;
                cell.style.width = cellWidth;
                cell.style.height = cellHeight;
                cell.style.marginRight = i < SlotCount - 1 ? horizontalGap : 0f;
                _slots[i].style.width = cellWidth;
                _slots[i].style.height = slotHeight;
                _keyLabels[i].style.left = 36f;
                _keyLabels[i].style.top = 84f;
                _keyLabels[i].style.width = 24f;
                _keyLabels[i].style.height = 24f;
            }
        }
    }
}

namespace ProjectName.UI.Toolkit
{
    /// <summary>[U8 요구] 핫바 슬롯 드롭 타겟 — 가방에서 좌클릭 드래그로 아이템 등록 (마인크래프트식).</summary>
    public class HotbarSlotDropTarget : IUTKDropTarget
    {
        private readonly int _index;

        public HotbarSlotDropTarget(int index) { _index = index; }

        public bool CanDrop(UTKDragPayload payload)
        {
            if (payload == null || payload.Item == null || payload.Source != UTKDragSourceKind.Inventory)
                return false;

            // [65차 P3 수리] 부대 모드 — 슬롯은 병사 지정 용도. 아이템 드롭 무효(기존: 모드 무시하고 AssignItem 덮어쓰기).
            if (HotbarUIUTK.Instance != null && HotbarUIUTK.Instance.IsSquadMode)
                return false;

            // [65차 P3 수리] 몸 장비 — AssignItem 가드(등록 거부)와 일치시킴.
            //   기존: CanDrop true → Drop "성공" 취급(고스트 소멸) + AssignItem 내부 거부 = 유저 혼란.
            //   CanDrop false → 폴백 체인이 다음 타겟 시도 → 어느 곳도 수령 못하면 아이템 원위(정직한 거절).
            if (payload.Item.category == PlayerInventory.ItemCategory.Armor)
                return false;

            return true;
        }

        public bool Drop(UTKDragPayload payload)
        {
            if (payload?.Item == null) return false;
            HotbarUIUTK.AssignItem(_index, payload.Item);
            return true;
        }
    }
}
