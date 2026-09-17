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
        private const float SlotH = 70f;

        private SaveData[] _slotInfos;
        private int _slotCount;
        private bool _loadingInProgress;
        private VisualElement _slotList;

        private LoadGameUTK()
        {
            name = "LoadGameUTK";
            AddToClassList("utk-window");
            pickingMode = PickingMode.Position;
            style.display = DisplayStyle.None;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            var title = new Label("저장 파일 불러오기");
            title.style.fontSize = 26f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(new Color(0.9f, 0.7f, 0.3f, 1f));
            title.style.marginBottom = 14f;
            Add(title);

            _slotList = new VisualElement();
            _slotList.name = "SlotList";
            _slotList.style.width = WinW;
            Add(_slotList);

            var backBtn = UTKButton.Create("← 뒤로", OnBackClicked, UTKButton.Variant.Secondary);
            backBtn.style.marginTop = 14f;
            Add(backBtn);

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
            var cell = new VisualElement();
            cell.name = "Slot_" + index;
            cell.style.width = WinW;
            cell.style.height = SlotH;
            cell.style.flexDirection = FlexDirection.Column;
            cell.style.justifyContent = Justify.Center;
            cell.style.paddingLeft = 14f;
            cell.style.marginBottom = 8f;
            cell.pickingMode = PickingMode.Position;

            SaveData info = (_slotInfos != null && index < _slotInfos.Length) ? _slotInfos[index] : null;
            bool hasSave = info != null;

            cell.style.backgroundColor = new StyleColor(
                hasSave ? new Color(0.2f, 0.35f, 0.5f, 0.9f) : new Color(0.3f, 0.3f, 0.3f, 0.7f));

            if (hasSave)
            {
                var header = new Label($"슬롯 {index + 1} — {info.timestamp ?? "날짜 없음"}");
                header.style.color = new StyleColor(UTKColor.TextPrimary);
                header.style.fontSize = 15f;
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                cell.Add(header);

                string dayStr = info.time != null ? $"Day {info.time.day}" : "Day ?";
                string levelStr = info.player != null ? $"Lv.{info.player.level}" : "Lv.?";
                var detail = new Label($"{dayStr}  |  {levelStr}");
                detail.style.color = new StyleColor(new Color(0.75f, 0.75f, 0.75f, 1f));
                detail.style.fontSize = 13f;
                cell.Add(detail);

                int captured = index;
                cell.RegisterCallback<PointerDownEvent>(_ => OnSlotClicked(captured));
            }
            else
            {
                var slotLabel = new Label($"슬롯 {index + 1}");
                slotLabel.style.color = new StyleColor(UTKColor.TextPrimary);
                slotLabel.style.fontSize = 14f;
                cell.Add(slotLabel);

                var empty = new Label("비어있음");
                empty.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f, 1f));
                empty  .style.unityFontStyleAndWeight = FontStyle.Italic;
                cell.Add(empty);
            }

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