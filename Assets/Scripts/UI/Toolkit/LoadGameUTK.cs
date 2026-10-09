using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U5 Round 1-C — 저장 파일 불러오기 (캐릭터) (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/Systems/LoadGameUI.cs (353줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 슬롯 목록 — SaveManager.GetAllSlotInfos() + SlotCount 실측.
    ///  ② 채워진 슬롯: 타임스탬프 + Day/Lv 표시, 클릭 → SaveManager.Load(i) +
    ///     LoadingManager.LoadSceneAsync("MainScene"). 빈 슬롯은 "비어있음" 표시, 비활성.
    ///  ③ 뒤로 → Close() (메인 메뉴 복귀 경로).
    ///  각 경로에 [LoadUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open() / Ensure(). 전체화면 오버레이.
    /// </summary>
    public class LoadGameUTK : VisualElement
    {
        // ===== 싱글턴 / 팩토리 =====
        private static LoadGameUTK _instance;
        public static LoadGameUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new LoadGameUTK();
        }

        /// <summary>불러오기 화면 표시.</summary>
        public static void Open()
        {
            Ensure();
            _instance.RefreshSlots();
            _instance.ShowToRoot();
            Debug.Log("[LoadUTK] 저장 파일 불러오기 표시");
        }

        private const int DefaultSlots = 5;
        private const float WinW = 450f;
        private const float SlotH = 92f;

        private SaveData[] _slotInfos;
        private int _slotCount;
        private bool _loadingInProgress;
        private VisualElement _slotList;
        private VisualElement _deleteOverlay;
        private Label _deletePrompt;
        private int _pendingDeleteSlot = -1;

        private LoadGameUTK()
        {
            name = "LoadGameUTK";
            AddToClassList("utk-window");
            pickingMode = PickingMode.Position;
            style.display = DisplayStyle.None;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            var panel = UTKTheme.CreatePanel("LoadGamePanel");
            panel.style.width = WinW;
            panel.style.paddingTop = panel.style.paddingBottom = 0f;
            panel.style.paddingLeft = panel.style.paddingRight = 0f;
            panel.style.overflow = Overflow.Hidden;
            Add(panel);

            var title = new Label("저장 파일 불러오기");
            title.name = "LoadGameHeader";
            title.style.fontSize = 21.6f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(UTKTheme.TextMain);
            title.style.backgroundColor = new StyleColor(UTKTheme.Panel);
            title.style.paddingTop = title.style.paddingBottom = 14f;
            title.style.paddingLeft = title.style.paddingRight = 16f;
            title.style.borderBottomWidth = 1f;
            title.style.borderBottomColor = new StyleColor(UTKTheme.Stroke);
            panel.Add(title);

            var slotScroll = new ScrollView();
            slotScroll.name = "SlotScroll";
            slotScroll.style.width = Length.Percent(100f);
            slotScroll.style.maxHeight = 480f;
            slotScroll.style.flexShrink = 1f;
            slotScroll.style.paddingTop = 12f;
            slotScroll.style.paddingBottom = 6f;
            slotScroll.style.paddingLeft = 14f;
            slotScroll.style.paddingRight = 14f;
            _slotList = slotScroll;
            panel.Add(_slotList);

            var footer = new VisualElement();
            footer.name = "LoadGameActions";
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.paddingTop = 10f;
            footer.style.paddingBottom = 10f;
            footer.style.paddingLeft = 14f;
            footer.style.paddingRight = 14f;
            footer.style.backgroundColor = new StyleColor(UTKTheme.PanelSub);
            footer.style.borderTopWidth = 1f;
            footer.style.borderTopColor = new StyleColor(UTKTheme.Stroke);
            panel.Add(footer);
            footer.Add(UTKButton.Create("← 뒤로", OnBackClicked, UTKButton.Variant.Secondary));

            _deleteOverlay = new VisualElement();
            _deleteOverlay.name = "DeleteConfirmOverlay";
            _deleteOverlay.style.position = Position.Absolute;
            _deleteOverlay.style.left = 0f;
            _deleteOverlay.style.right = 0f;
            _deleteOverlay.style.top = 0f;
            _deleteOverlay.style.bottom = 0f;
            _deleteOverlay.style.display = DisplayStyle.None;
            _deleteOverlay.style.alignItems = Align.Center;
            _deleteOverlay.style.justifyContent = Justify.Center;
            _deleteOverlay.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.68f));
            Add(_deleteOverlay);

            var confirmPanel = UTKTheme.CreatePanel("DeleteConfirmPanel");
            confirmPanel.style.width = 320f;
            confirmPanel.style.paddingTop = confirmPanel.style.paddingBottom = 16f;
            confirmPanel.style.paddingLeft = confirmPanel.style.paddingRight = 16f;
            _deleteOverlay.Add(confirmPanel);

            var confirmTitle = new Label("저장 파일 삭제");
            confirmTitle.style.fontSize = 16.8f;
            confirmTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            confirmTitle.style.color = new StyleColor(UTKTheme.TextMain);
            confirmPanel.Add(confirmTitle);
            _deletePrompt = new Label();
            _deletePrompt.style.color = new StyleColor(UTKTheme.TextSub);
            _deletePrompt.style.marginTop = 8f;
            _deletePrompt.style.marginBottom = 14f;
            confirmPanel.Add(_deletePrompt);

            var confirmActions = new VisualElement();
            confirmActions.style.flexDirection = FlexDirection.Row;
            confirmActions.style.justifyContent = Justify.FlexEnd;
            confirmPanel.Add(confirmActions);
            confirmActions.Add(UTKButton.Create("취소", CancelDelete, UTKButton.Variant.Secondary));
            var deleteButton = UTKButton.Create("삭제", ConfirmDelete, UTKButton.Variant.Danger);
            deleteButton.style.marginLeft = 8f;
            confirmActions.Add(deleteButton);

            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        // ===== 슬롯 목록 =====

        private void RefreshSlots()
        {
            if (SaveManager.Instance != null)
            {
                _slotInfos = SaveManager.Instance.GetAllSlotInfos();
                _slotCount = SaveManager.Instance.SlotCount;
            }
            else
            {
                _slotInfos = null;
                _slotCount = 0;
            }
            _loadingInProgress = false;

            _slotList.Clear();
            int displayCount = _slotCount > 0 ? _slotCount : DefaultSlots;
            for (int i = 0; i < displayCount; i++)
                _slotList.Add(BuildSlot(i));
        }

        private VisualElement BuildSlot(int index)
        {
            var cell = UTKTheme.CreatePanel("Slot_" + index, secondary: true);
            cell.style.width = Length.Percent(100f);
            cell.style.minHeight = SlotH;
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            cell.style.paddingLeft = 12f;
            cell.style.paddingRight = 10f;
            cell.style.paddingTop = cell.style.paddingBottom = 9f;
            cell.style.marginBottom = 8f;
            cell.pickingMode = PickingMode.Position;

            SaveData info = (_slotInfos != null && index < _slotInfos.Length) ? _slotInfos[index] : null;
            bool hasSave = info != null;

            var details = new VisualElement();
            details.style.flexDirection = FlexDirection.Column;
            details.style.flexGrow = 1f;
            details.style.minWidth = 0f;
            cell.Add(details);

            var slotLabel = new Label($"슬롯 {index + 1}");
            slotLabel.style.color = new StyleColor(UTKTheme.TextMain);
            slotLabel.style.fontSize = 14.4f;
            slotLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            details.Add(slotLabel);

            string date = hasSave ? (info.timestamp ?? "날짜 없음") : "저장된 게임 없음";
            var dateLabel = new Label(date);
            dateLabel.style.color = new StyleColor(UTKTheme.TextSub);
            dateLabel.style.fontSize = 12f;
            dateLabel.style.marginTop = 2f;
            details.Add(dateLabel);

            string dayStr = hasSave && info.time != null ? $"Day {info.time.day}" : (hasSave ? "Day ?" : "—");
            string levelStr = hasSave && info.player != null ? $"Lv.{info.player.level}" : (hasSave ? "Lv.?" : "—");
            var summary = new Label(hasSave ? $"{dayStr}  ·  {levelStr}" : "비어있음");
            summary.style.color = new StyleColor(hasSave ? UTKTheme.TextMain : UTKTheme.TextSub);
            summary.style.fontSize = 12f;
            summary.style.marginTop = 2f;
            details.Add(summary);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;
            cell.Add(actions);

            int captured = index;
            var loadButton = UTKButton.Create("로드", () => OnSlotClicked(captured), UTKButton.Variant.Primary);
            loadButton.style.width = 52f;
            loadButton.SetEnabled(hasSave);
            actions.Add(loadButton);

            var deleteButton = UTKButton.Create("삭제", () => RequestDelete(captured), UTKButton.Variant.Danger);
            deleteButton.style.width = 52f;
            deleteButton.style.marginLeft = 6f;
            deleteButton.SetEnabled(hasSave);
            actions.Add(deleteButton);

            return cell;
        }

        // ===== 콜백 (원본 실측 직접 호출) =====

        private void OnSlotClicked(int slotIndex)
        {
            if (_loadingInProgress) return;

            SaveData info = (_slotInfos != null && slotIndex < _slotInfos.Length) ? _slotInfos[slotIndex] : null;
            if (info == null)
            {
                Debug.Log("[LoadUTK] 빈 슬롯 — 저장된 게임 없음");
                return;
            }

            Debug.Log($"[LoadUTK] 슬롯 {slotIndex} 불러오기 시작...");
            _loadingInProgress = true;

            if (SaveManager.Instance != null)
                SaveManager.Instance.Load(slotIndex);

            if (LoadingManager.Instance != null)
                LoadingManager.Instance.LoadSceneAsync("MainScene");
            else
            {
                Debug.LogError("[LoadUTK] LoadingManager.Instance가 null입니다.");
                _loadingInProgress = false;
            }
        }

        private void OnBackClicked()
        {
            Debug.Log("[LoadUTK] 메인 메뉴로 돌아가기");
            RemoveFromHierarchy();
            style.display = DisplayStyle.None;
        }

        private void RequestDelete(int slotIndex)
        {
            if (_loadingInProgress) return;
            _pendingDeleteSlot = slotIndex;
            _deletePrompt.text = $"슬롯 {slotIndex + 1}의 저장 파일을 삭제할까요?";
            _deleteOverlay.style.display = DisplayStyle.Flex;
        }

        private void CancelDelete()
        {
            _pendingDeleteSlot = -1;
            _deleteOverlay.style.display = DisplayStyle.None;
        }

        private void ConfirmDelete()
        {
            if (_pendingDeleteSlot < 0) return;
            int slotIndex = _pendingDeleteSlot;
            _pendingDeleteSlot = -1;
            _deleteOverlay.style.display = DisplayStyle.None;

            if (SaveManager.Instance != null)
                SaveManager.Instance.DeleteSlot(slotIndex);
            Debug.Log($"[LoadUTK] 슬롯 {slotIndex} 삭제");
            RefreshSlots();
        }

        private void ShowToRoot()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[LoadUTK] UIRoot 없음 — 오버레이 표시 불가");
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