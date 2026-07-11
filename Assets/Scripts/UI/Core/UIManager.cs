using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 1.5: UI 매니저 싱글톤.
    /// 키 입력 감지 + 윈도우 스택 관리 + ESC로 현재 창 닫기.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Key Bindings")]
        [SerializeField] private KeyBindings _keyBindings;

        [Header("Window References")]
        [SerializeField] private QuestWindow _questWindow;
        [SerializeField] private RecipeWindow _recipeWindow;
        [SerializeField] private InventoryWindow _inventoryWindow;
        [SerializeField] private MapWindow _mapWindow;
        [SerializeField] private LootWindow _lootWindow;

        public static UIManager Instance { get; private set; }

        // 윈도우 스택 (ESC로 최상위 창 닫기)
        private readonly List<UIWindow> _windowStack = new List<UIWindow>();

        // 액션 이름 → UIWindow 매핑
        private readonly Dictionary<string, UIWindow> _windows = new Dictionary<string, UIWindow>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 정적 프로퍼티에 참조 저장 (레거시 호환)
            questWindow = _questWindow;
            recipeWindow = _recipeWindow;
            inventoryWindow = _inventoryWindow;
            mapWindow = _mapWindow;
            lootWindow = _lootWindow;
        }

        private void Start()
        {
            // 윈도우 딕셔너리 초기화
            RegisterWindow("Quest", _questWindow);
            RegisterWindow("Recipe", _recipeWindow);
            RegisterWindow("Inventory", _inventoryWindow);
            RegisterWindow("Map", _mapWindow);

            // LootBasket 이벤트 구독 — E 키로 LootWindow 열기
            LootBasket.OnOpenLootWindowRequested += HandleLootWindowRequest;
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제 (메모리 누수 방지)
            LootBasket.OnOpenLootWindowRequested -= HandleLootWindowRequest;
        }

        private void HandleLootWindowRequest(ProjectName.Core.ILootBasket basket)
        {
            if (_lootWindow == null)
            {
                Debug.LogWarning("[UIManager] LootWindow 참조가 없습니다. 직접 루팅으로 폴백합니다.");
                basket.TakeAll();
                return;
            }
            _lootWindow.OpenForBasket(basket);
        }

        private void RegisterWindow(string actionName, UIWindow window)
        {
            if (window != null && !_windows.ContainsKey(actionName))
            {
                _windows[actionName] = window;
            }
        }

        private void Update()
        {
            if (_keyBindings == null) return;

            var bindings = _keyBindings.GetAllBindings();

            // 각 액션 키 감지
            foreach (var kvp in bindings)
            {
                string actionName = kvp.Key;
                KeyCode key = kvp.Value;

                if (key == KeyCode.None) continue;
                if (actionName == "Close") continue; // ESC는 별도 처리

                if (Input.GetKeyDown(key))
                {
                    HandleActionKey(actionName);
                }
            }

            // ESC 키 — 윈도우 스택에서 최상위 창 닫기
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscapeKey();
            }
        }

        /// <summary>
        /// 액션 키 입력 처리: 해당 윈도우 토글 (열려있으면 닫고, 닫혀있으면 열기)
        /// </summary>
        private void HandleActionKey(string actionName)
        {
            if (_windows.TryGetValue(actionName, out var window))
            {
                ToggleWindow(window);
            }
            else
            {
                Debug.Log($"[UIManager] 알 수 없는 액션: {actionName}");
            }
        }

        /// <summary>
        /// ESC 키 처리: 최상위 윈도우 닫기 (스택이 비어있으면 무시)
        /// </summary>
        private void HandleEscapeKey()
        {
            // 스택에서 가장 위에 있는(가장 최근에 열린) 윈도우를 닫음
            for (int i = _windowStack.Count - 1; i >= 0; i--)
            {
                var window = _windowStack[i];
                if (window != null && window.IsOpen)
                {
                    window.Hide();
                    _windowStack.RemoveAt(i);
                    return;
                }
            }

            // 스택이 비어있거나 모든 윈도우가 이미 닫혀 있음
            _windowStack.Clear();
        }

        /// <summary>
        /// 윈도우 열기 (스택 푸시)
        /// </summary>
        public void OpenWindow(UIWindow window)
        {
            if (window == null) return;
            if (window.IsOpen) return;

            window.Show();
            if (!_windowStack.Contains(window))
            {
                _windowStack.Add(window);
            }
        }

        /// <summary>
        /// 윈도우 닫기 (스택에서 제거)
        /// </summary>
        public void CloseWindow(UIWindow window)
        {
            if (window == null) return;
            if (!window.IsOpen) return;

            window.Hide();
            _windowStack.Remove(window);
        }

        /// <summary>
        /// 윈도우 토글 (열려있으면 닫고, 닫혀있으면 열기)
        /// </summary>
        public void ToggleWindow(UIWindow window)
        {
            if (window == null) return;

            if (window.IsOpen)
            {
                CloseWindow(window);
            }
            else
            {
                OpenWindow(window);
            }
        }

        // ================================================================
        // 레거시 호환 인터페이스
        // ================================================================

        public void ShowUI(string uiName)
        {
            if (_windows.TryGetValue(uiName, out var window))
            {
                OpenWindow(window);
            }
        }

        public void HideUI(string uiName)
        {
            if (_windows.TryGetValue(uiName, out var window))
            {
                CloseWindow(window);
            }
        }

        public void ToggleUI(string uiName)
        {
            if (_windows.TryGetValue(uiName, out var window))
            {
                ToggleWindow(window);
            }
        }

        public void OpenWindow(System.Type windowType)
        {
            foreach (var kvp in _windows)
            {
                if (kvp.Value != null && kvp.Value.GetType() == windowType)
                {
                    OpenWindow(kvp.Value);
                    return;
                }
            }
            Debug.Log("[UIManager] OpenWindow: " + windowType.Name + " (찾을 수 없음)");
        }

        public void ToggleWindow(System.Type windowType)
        {
            foreach (var kvp in _windows)
            {
                if (kvp.Value != null && kvp.Value.GetType() == windowType)
                {
                    ToggleWindow(kvp.Value);
                    return;
                }
            }
            Debug.Log("[UIManager] ToggleWindow: " + windowType.Name + " (찾을 수 없음)");
        }

        // ================================================================
        // 정적 윈도우 참조 (레거시)
        // ================================================================

        public static CraftingUI craftingWindow { get; set; }
        public static InventoryWindow inventoryWindow { get; set; }
        public static WarehouseUI warehouseWindow { get; set; }
        public static LootWindow lootWindow { get; set; }
        public static QuestWindow questWindow { get; set; }
        public static RecipeWindow recipeWindow { get; set; }
        public static MapWindow mapWindow { get; set; }

        public void SetKeyBindings(KeyBindings keyBindings)
        {
            _keyBindings = keyBindings;
            Debug.Log("[UIManager] SetKeyBindings: " + keyBindings);
        }
    }
}