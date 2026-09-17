using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 1-C — 저장 슬롯 선택 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/Systems/SaveSlotUI.cs (298줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 슬롯 3~N개 (SaveManager.SlotCount 실측, 최소 3) — 타임스탬프 + Day/Lv 표시.
    ///  ② 저장 버튼 → SaveManager.Save(i) 직접 호출 → TimeManager.SleepFor(sleepHours, ...).
    ///  ③ 확인 버튼은 선택된 슬롯 있을 때만 활성 (SetEnabled).
    ///  각 경로에 [SaveUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open(float sleepHours) / Ensure(). 전체화면 오버레이.
    /// </summary>
    public class SaveSlotUTK : VisualElement
    {
        // ===== 싱글턴 / 팩토리 =====
        private static SaveSlotUTK _instance;
        public static SaveSlotUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new SaveSlotUTK();
        }

        /// <summary>저장 슬롯 선택 표시.</summary>
        public static void Open(float sleepHours)
        {
            Ensure();
            _instance._sleepHours = sleepHours;
            _instance._selectedSlot = -1;
            _instance.RefreshSlots();
            _instance.ShowToRoot();
            Debug.Log($"[SaveUTK] 저장 슬롯 표시 (sleepHours={sleepHours})");
        }

        private const int MinSlots = 3;
        private const float WinW = 420f;
        private const float SlotH = 60f;

        private float _sleepHours;
        private int _selectedSlot = -1;
        private SaveData[] _slotInfos;
        private VisualElement _slotList;
        private Button _confirmBtn;

        private SaveSlotUTK()
        {
            name = "SaveSlotUTK";
            AddToClassList("utk-window");
            pickingMode = PickingMode.Position;
            style.display = DisplayStyle.None;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            var title = new Label("저장 슬롯 선택");
            title.style.fontSize = 26f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(new Color(0.9f, 0.7f, 0.3f, 1f));
            title.style.marginBottom = 14f;
            Add(title);

            _slotList = new VisualElement();
            _slotList.name = "SlotList";
            _slotList.style.width = WinW;
            Add(_slotList);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 14f;
            Add(btnRow);

            _confirmBtn = UTKButton.Create("확인", OnConfirm, UTKButton.Variant.Primary);
            _confirmBtn.SetEnabled(false);
            btnRow.Add(_confirmBtn);
            btnRow.Add(UTKButton.Create("취소", OnCancel, UTKButton.Variant.Secondary));

            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        // ===== 슬롯 목록 구성 =====

        private int GetSlotCount()
        {
            int n = SaveManager.Instance != null ? SaveManager.Instance.SlotCount : MinSlots;
            return Mathf.Max(MinSlots, n);
        }

        private void RefreshSlots()
        {
            _slotInfos = SaveManager.Instance != null
                ? SaveManager.Instance.GetAllSlotInfos()
                : null;

            _slotList.Clear();
            int count = GetSlotCount();
            for (int i = 0; i < count; i++)
            {
                SaveData info = (_slotInfos != null && i < _slotInfos.Length) ? _slotInfos[i] : null;
                _slotList.Add(BuildSlotButton(i, info));
            }
            _confirmBtn.SetEnabled(_selectedSlot >= 0);
        }

        private VisualElement BuildSlotButton(int index, SaveData info)
        {
            var cell = new VisualElement();
            cell.name = "Slot_" + index;
            cell.style.width = WinW;
            cell.style.height = SlotH;
            cell.style.flexDirection = FlexDirection.Column;
            cell.style.justifyContent = Justify.Center;
            cell.style.paddingLeft = 14f;
            cell.style.marginBottom = 6f;
            cell.style.backgroundColor = new StyleColor(
                info != null ? new Color(0.2f, 0.35f, 0.5f, 0.9f) : new Color(0.3f, 0.3f, 0.3f, 0.8f));
            cell.pickingMode = PickingMode.Position;

            var text = new Label(FormatSlotLabel(index, info));
            text.style.color = new StyleColor(UTKColor.TextPrimary);
            text.style.fontSize = 14f;
            cell.Add(text);

            int captured = index;
            cell.RegisterCallback<PointerDownEvent>(_ =>
            {
                _selectedSlot = captured;
                _confirmBtn.SetEnabled(true);
                Debug.Log($"[SaveUTK] 슬롯 {captured + 1} 선택");
            });

            return cell;
        }

        private string FormatSlotLabel(int index, SaveData info)
        {
            if (info == null)
                return $"슬롯 {index + 1} — 비어있음";
            int day = info.time != null ? info.time.day : 0;
            int lv = info.player != null ? info.player.level : 0;
            return $"슬롯 {index + 1} — {info.timestamp} (Day {day}, Lv.{lv})";
        }

        // ===== 버튼 콜백 (원본 실측 직접 호출) =====

        private void OnConfirm()
        {
            if (_selectedSlot < 0) return;

            Debug.Log($"[SaveUTK] 슬롯 {_selectedSlot} 저장 → SleepFor({_sleepHours})");
            if (SaveManager.Instance != null)
                SaveManager.Instance.Save(_selectedSlot);

            Hide();

            if (TimeManager.Instance != null)
                TimeManager.Instance.SleepFor(_sleepHours, OnWakeUpComplete);
            else
                Debug.LogWarning("[SaveUTK] TimeManager 인스턴스 없음 — 수면 스킵");
        }

        private void OnCancel()
        {
            Debug.Log("[SaveUTK] 저장 취소");
            Hide();
        }

        private void OnWakeUpComplete()
        {
            Debug.Log("[SaveUTK] 기상 완료!");
        }

        private void Hide()
        {
            RemoveFromHierarchy();
            style.display = DisplayStyle.None;
            _selectedSlot = -1;
            _slotInfos = null;
        }

        private void ShowToRoot()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[SaveUTK] UIRoot 없음 — 오버레이 표시 불가");
                return;
            }
            style.display = DisplayStyle.Flex;
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.top = 0f;
            style.bottom = 0f;
            if (parent == null)
                root.Add(this);
            BringToFront();
        }
    }
}