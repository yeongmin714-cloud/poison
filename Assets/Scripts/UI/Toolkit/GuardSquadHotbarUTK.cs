using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI;        // GuardIconRenderer
using ProjectName.Systems;   // GuardPlaceholder, GuardSelectionManager

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 Round B — 병사 부대 핫바 (GuardSquadHotbar 796줄 uGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/GuardSquadHotbar.cs — 절대 수정 금지.
    ///
    /// [역할 분담 — 입력 충돌 방지]
    ///   키 입력(Tab 토글 / F+1~8 등록 / 1~8 선택)은 원본 GuardSquadHotbar가 담당.
    ///   본 UTK는 "표시"와 "해제(슬롯 우클릭)"만 담당한다.
    ///
    /// [데이터 소스 — 원본과 공유]
    ///   원본의 등록 슬롯(_slots: GuardPlaceholder[][])을 private 필드 리플렉션으로 실측해
    ///   동일한 슬롯 구조를 그대로 읽는다 (셔틀 복제 없음, 원본이 진실 공급원).
    ///   표시 모드는 원본 static GuardSquadHotbar.IsSquadMode 로 판정.
    ///   해제는 원본 public instance 메서드 guardHotbar.UnregisterSlot(index) 를 호출.
    ///
    /// [구성]
    ///   ① 하단 중앙 8슬롯 — 원본 레이아웃 상수와 동일 (SlotSize=96 등).
    ///   ② 각 슬롯 숫자 라벨(1~8) + 대표 병사 아바타(실제 3D 아이콘 우선, 국적색 원형+이니셜 폴백).
    ///   ③ 등록/사망/미등록 배경 톤, "Lv{N}", 그룹 인원 "x{생존}".
    ///   ④ 슬롯 우클릭(button==1) → 원본 UnregisterSlot(index) 호출 (해제).
    ///   ⑤ 표시 모드에 따라 화면 표시/숨김을 폴링 동기화 (0.5초 — 원본 RefreshAvatarsPeriodically 관례).
    ///
    /// 상시 노출 바 — 순수 VisualElement (UTKWindowBase 윈도우 아님) + Updater로 UIRoot 부착.
    /// </summary>
    public class GuardSquadHotbarUTK : VisualElement
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static GuardSquadHotbarUTK _instance;
        public static GuardSquadHotbarUTK Instance => _instance;

        /// <summary>부대 핫바 인스턴스 보장 + Updater 부착 (U7 스위치오버 호출점용).</summary>
        public static GuardSquadHotbarUTK Ensure()
        {
            if (_instance == null)
            {
                _instance = new GuardSquadHotbarUTK();
                var go = new GameObject("GuardSquadHotbarUTK");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<Updater>().bar = _instance;
                Debug.Log("[SquadUTK] Ensure()로 인스턴스 생성 — 부대 핫바 준비");
            }
            return _instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // [U8 은퇴] 부대 렌더는 HotbarUIUTK로 흡수 — 독립 바 미표시

        }

        // ===== 설정 — 원본 GuardSquadHotbar 레이아웃 상수와 동일 =====
        private const int   SlotCount    = 8;
        private const float SlotSize     = 96f;
        private const float SlotGap      = 10f;
        private const float Bottom       = 12f;
        private const float NumBoxHeight = 24f;
        private const float NumBoxGap    = 6f;
        private const float AvatarSize   = 56f;

        // 다크 테마 색상 (원본 톤 유지)
        private static readonly Color ColorPanelBg   = new Color(0.06f, 0.06f, 0.08f, 0.72f);
        private static readonly Color ColorSlotBg    = new Color(0.13f, 0.13f, 0.16f, 0.88f);
        private static readonly Color ColorSlotEmpty = new Color(0.13f, 0.13f, 0.16f, 0.40f);
        private static readonly Color ColorKeyBox    = new Color(0.10f, 0.10f, 0.12f, 0.90f);
        private static readonly Color ColorText      = new Color(0.88f, 0.88f, 0.88f, 1f);
        private static readonly Color ColorTextDim   = new Color(0.70f, 0.70f, 0.74f, 0.9f);
        private static readonly Color ColorCount     = new Color(1f, 0.95f, 0.75f, 1f);

        // 국적색 (동=빨강, 서=파랑, 남=초록, 북=보라, 기타=회색)
        private static readonly Color ColorNationEast   = new Color(0.92f, 0.28f, 0.26f, 1f);
        private static readonly Color ColorNationWest   = new Color(0.30f, 0.56f, 0.95f, 1f);
        private static readonly Color ColorNationSouth  = new Color(0.30f, 0.78f, 0.40f, 1f);
        private static readonly Color ColorNationNorth  = new Color(0.64f, 0.42f, 0.90f, 1f);
        private static readonly Color ColorNationDefault = new Color(0.62f, 0.62f, 0.66f, 1f);

        private static readonly Color ColorAvatarDead  = new Color(0.42f, 0.42f, 0.46f, 0.85f);
        private static readonly Color ColorAvatarEmpty = new Color(0.30f, 0.30f, 0.34f, 0.35f);
        private static readonly Color ColorTextDead    = new Color(0.55f, 0.55f, 0.58f, 1f);

        // ===== 슬롯 시각 요소 =====
        private readonly VisualElement[] _slotBgs;   // 슬롯 배경 (등록/빈 톤)
        private readonly VisualElement[] _avatarIcons; // 실제 3D 아이콘 (backgroundImage)
        private readonly Label[]         _initialTexts; // 폴백 이니셜
        private readonly Label[]         _levelTexts;   // "Lv{N}"
        private readonly Label[]         _countTexts;   // "x{생존}"
        private readonly string[]        _appliedIconSignature; // 아이콘 갱신 중복 방지 서명

        // 아이콘 리얼 미베이크 시 1회 경고용 (사망/빈 슬롯과 구분)
        private bool _squadModeLast;

        private GuardSquadHotbarUTK()
        {
            name = "SquadHotbar";
            AddToClassList("utk-squad-hotbar");
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.bottom = Bottom;
            style.alignItems = Align.Center;
            style.flexDirection = FlexDirection.Column;
            style.paddingTop = 12f;
            style.paddingRight = 14f;
            style.paddingBottom = 12f;
            style.paddingLeft = 14f;
            style.backgroundColor = new StyleColor(ColorPanelBg);
            pickingMode = PickingMode.Position;

            _slotBgs = new VisualElement[SlotCount];
            _avatarIcons = new VisualElement[SlotCount];
            _initialTexts = new Label[SlotCount];
            _levelTexts = new Label[SlotCount];
            _countTexts = new Label[SlotCount];
            _appliedIconSignature = new string[SlotCount];

            var row = new VisualElement();
            row.name = "SquadRows";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            Add(row);

            for (int i = 0; i < SlotCount; i++)
            {
                var cell = new VisualElement();
                cell.name = "SquadCell_" + i;
                cell.style.flexDirection = FlexDirection.Column;
                cell.style.alignItems = Align.Center;
                cell.style.marginLeft = SlotGap * 0.5f;
                cell.style.marginRight = SlotGap * 0.5f;
                row.Add(cell);

                // 슬롯 배경
                var bg = new VisualElement();
                bg.name = "SquadSlotBg_" + i;
                bg.style.width = SlotSize;
                bg.style.height = SlotSize;
                bg.style.backgroundColor = new StyleColor(ColorSlotEmpty);
                bg.style.borderTopWidth = 1f;
                bg.style.borderRightWidth = 1f;
                bg.style.borderBottomWidth = 1f;
                bg.style.borderLeftWidth = 1f;
                bg.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.15f));
                bg.style.borderRightColor = new StyleColor(new Color(1f, 1f, 1f, 0.15f));
                bg.style.borderBottomColor = new StyleColor(new Color(1f, 1f, 1f, 0.15f));
                bg.style.borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.15f));
                bg.style.position = Position.Relative;
                bg.style.alignItems = Align.Center;
                bg.style.justifyContent = Justify.Center;
                cell.Add(bg);
                _slotBgs[i] = bg;

                // 실제 3D 아이콘 (backgroundImage — GuardIconRenderer)
                var icon = new VisualElement();
                icon.name = "SquadAvatar_" + i;
                icon.style.width = AvatarSize;
                icon.style.height = AvatarSize;
                icon.style.display = DisplayStyle.None;
                bg.Add(icon);
                _avatarIcons[i] = icon;

                // 폴백 이니셜
                var initial = new Label(string.Empty);
                initial.name = "SquadInitial_" + i;
                initial.style.fontSize = 24f;
                initial.style.unityFontStyleAndWeight = FontStyle.Bold;
                initial.style.color = new StyleColor(ColorText);
                initial.style.unityTextAlign = TextAnchor.MiddleCenter;
                initial.style.flexGrow = 1f;
                bg.Add(initial);
                _initialTexts[i] = initial;

                // Lv (슬롯 하단)
                var level = new Label(string.Empty);
                level.name = "SquadLevel_" + i;
                level.style.fontSize = 13f;
                level.style.color = new StyleColor(ColorTextDim);
                level.style.position = Position.Absolute;
                level.style.left = 4f;
                level.style.right = 4f;
                level.style.bottom = 4f;
                level.style.unityTextAlign = TextAnchor.MiddleCenter;
                bg.Add(level);
                _levelTexts[i] = level;

                // 인원 수 (우상단)
                var count = new Label(string.Empty);
                count.name = "SquadCount_" + i;
                count.style.fontSize = 16f;
                count.style.color = new StyleColor(ColorCount);
                count.style.position = Position.Absolute;
                count.style.top = 2f;
                count.style.right = 5f;
                bg.Add(count);
                _countTexts[i] = count;

                // 하단 숫자 박스 (1~8)
                var keyBox = new VisualElement();
                keyBox.name = "SquadKeyBox_" + i;
                keyBox.style.width = 26f;
                keyBox.style.height = NumBoxHeight;
                keyBox.style.backgroundColor = new StyleColor(ColorKeyBox);
                keyBox.style.alignItems = Align.Center;
                keyBox.style.justifyContent = Justify.Center;
                keyBox.style.marginTop = NumBoxGap;
                cell.Add(keyBox);

                var keyLabel = new Label((i + 1).ToString());
                keyLabel.name = "SquadKey_" + i;
                keyLabel.style.fontSize = 15f;
                keyLabel.style.color = new StyleColor(ColorTextDim);
                keyBox.Add(keyLabel);

                // 해제 — 슬롯 우클릭 (button==1) → 원본 UnregisterSlot
                int capturedIndex = i;
                bg.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 1) return;
                    UnregisterSlot(capturedIndex);
                });
            }

            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        // =====================================================================
        //  데이터 소스 — 원본 GuardSquadHotbar와 공유 (private _slots 리플렉션 실측)
        // =====================================================================
        private static GuardSquadHotbar FindOriginal()
        {
            try { return Object.FindAnyObjectByType<GuardSquadHotbar>(); }
            catch { return null; }
        }

        private static GuardPlaceholder[][] ReadSlots(GuardSquadHotbar original)
        {
            if (original == null) return null;
            var field = typeof(GuardSquadHotbar).GetField("_slots",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) return null;
            return field.GetValue(original) as GuardPlaceholder[][];
        }

        // =====================================================================
        //  해제 API — 원본 public 메서드 위임 (입력 중복 없이 표시/해제만 담당)
        // =====================================================================
        public static void UnregisterSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;
            var original = FindOriginal();
            if (original == null)
            {
                Debug.LogWarning("[SquadUTK] 원본 GuardSquadHotbar 없음 — 해제 스킵");
                return;
            }
            original.UnregisterSlot(index);
            Debug.Log($"[SquadUTK] 슬롯 {index + 1} 해제 요청 (원본 경유)");
        }

        // =====================================================================
        //  표시 갱신 — 원본 RefreshSlotVisual 로직을 UTK 비주얼로 미러링
        // =====================================================================
        private void RefreshAll()
        {
            var original = FindOriginal();
            GuardPlaceholder[][] slots = ReadSlots(original);

            // 부대 모드 표시 동기화 (원본 static 판정 — 입력 충돌 방지 위해 표시만)
            bool squadMode = GuardSquadHotbar.IsSquadMode;
            if (squadMode != _squadModeLast)
            {
                _squadModeLast = squadMode;
                Debug.Log($"[SquadUTK] 부대 모드 표시 반영: {(squadMode ? "병사 부대" : "아이템")}");
            }
            style.display = squadMode ? DisplayStyle.Flex : DisplayStyle.None;

            if (slots == null)
            {
                for (int i = 0; i < SlotCount; i++) ClearSlot(i);
                return;
            }

            for (int i = 0; i < SlotCount && i < slots.Length; i++)
                RefreshSlot(i, slots[i]);
        }

        private void RefreshSlot(int index, GuardPlaceholder[] members)
        {
            // 등록/생존/대표 병사 집계 (원본 RefreshSlotVisual 동일 로직)
            bool hasMembers = false;
            if (members != null)
            {
                foreach (var g in members) { if (g != null) { hasMembers = true; break; } }
            }

            GuardPlaceholder lead = null;
            int aliveCount = 0;
            int total = 0;
            if (members != null)
            {
                foreach (var g in members)
                {
                    if (g == null) continue;
                    total++;
                    if (g.IsAlive) { aliveCount++; if (lead == null) lead = g; }
                }
                if (lead == null)
                {
                    foreach (var g in members) { if (g != null) { lead = g; break; } }
                }
            }

            bool registered = lead != null;
            bool dead = registered && !lead.IsAlive;

            _slotBgs[index].style.backgroundColor = new StyleColor(hasMembers ? ColorSlotBg : ColorSlotEmpty);

            // 실제 3D 아이콘 시도 (생존 대표만) — 폴백 원 자리엔 이니셜
            bool useRealIcon = false;
            if (registered && !dead && lead != null)
            {
                try
                {
                    Texture2D iconTex = GuardIconRenderer.GetOrCreateIcon(lead);
                    if (iconTex != null)
                    {
                        string sig = iconTex.GetInstanceID().ToString();
                        if (_appliedIconSignature[index] != sig)
                        {
                            _avatarIcons[index].style.backgroundImage =
                                new StyleBackground(Background.FromTexture2D(iconTex));
                            _appliedIconSignature[index] = sig;
                        }
                        _avatarIcons[index].style.display = DisplayStyle.Flex;
                        useRealIcon = true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[SquadUTK] 실제 아이콘 적용 실패 — 절차 아바타 폴백: " + e.Message);
                }
            }

            if (!useRealIcon)
            {
                _avatarIcons[index].style.display = DisplayStyle.None;
                _appliedIconSignature[index] = null;
            }

            // 이니셜 (실제 아이콘 표시 중 숨김)
            if (registered && !useRealIcon)
            {
                _initialTexts[index].text = GetInitial(lead.GuardName);
                _initialTexts[index].style.color = new StyleColor(dead ? ColorTextDead : ColorText);
                _initialTexts[index].style.backgroundColor = new StyleColor(
                    !registered ? ColorAvatarEmpty : dead ? ColorAvatarDead : GetNationColor(lead.Nation));
            }
            else
            {
                _initialTexts[index].text = string.Empty;
                _initialTexts[index].style.backgroundColor = StyleKeyword.Null;
            }

            _levelTexts[index].text = registered ? "Lv" + lead.Level : string.Empty;
            _levelTexts[index].style.color = new StyleColor(dead ? ColorTextDead : ColorTextDim);
            _countTexts[index].text = registered && total > 1 ? "x" + aliveCount : string.Empty;
        }

        private void ClearSlot(int index)
        {
            _slotBgs[index].style.backgroundColor = new StyleColor(ColorSlotEmpty);
            _avatarIcons[index].style.display = DisplayStyle.None;
            _appliedIconSignature[index] = null;
            _initialTexts[index].text = string.Empty;
            _initialTexts[index].style.backgroundColor = StyleKeyword.Null;
            _levelTexts[index].text = string.Empty;
            _countTexts[index].text = string.Empty;
        }

        private static string GetInitial(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int len = (char.IsSurrogate(name[0]) && name.Length >= 2) ? 2 : 1;
            return name.Substring(0, len);
        }

        private static Color GetNationColor(string nation)
        {
            if (string.IsNullOrEmpty(nation)) return ColorNationDefault;
            if (nation.Contains("동")) return ColorNationEast;
            if (nation.Contains("서")) return ColorNationWest;
            if (nation.Contains("남")) return ColorNationSouth;
            if (nation.Contains("북")) return ColorNationNorth;
            return ColorNationDefault;
        }

        // =====================================================================
        //  Updater — UIRoot 부착 + 0.5초 폴링 (사망/파괴 반영, 화면 동기화)
        // =====================================================================
        private class Updater : MonoBehaviour
        {
            public GuardSquadHotbarUTK bar;
            private float _tick = 0.5f;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && bar != null && bar.parent == null)
                    root.Add(bar);

                if (bar == null) return;
                _tick -= Time.unscaledDeltaTime;
                if (_tick <= 0f)
                {
                    _tick = 0.5f;
                    if (bar.parent != null)
                        bar.RefreshAll();
                }
            }
        }
    }
}