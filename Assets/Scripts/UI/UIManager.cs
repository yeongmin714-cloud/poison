using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;

namespace ProjectName.UI
{
    /// <summary>
    /// UI 창들을 관리하는 메인 매니저.
    /// 키 입력을 감지하고, 해당하는 윈도우를 열거나 닫습니다.
    /// 
    /// [Unity 초보자 설명]
    /// - 게임 시작 시 자동으로 생성되는 싱글톤(하나만 존재)입니다
    /// - 매 프레임마다 Q/R/I/M 키를 체크합니다
    /// - 키가 눌리면 해당 윈도우를 열거나 닫습니다 (Toggle)
    /// - ESC 키는 현재 열려있는 창을 닫습니다
    /// - LootWindow는 LootBasket 오브젝트와 상호작용할 때 열립니다
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class UIManager : MonoBehaviour
    {
        [Header("Key Bindings")]
        [SerializeField] private KeyBindings _keyBindings;

        [Header("Windows")]
        public PlayerStatusWindow statusWindow;
        public UIWindow questWindow;
        public UIWindow recipeWindow;
        public UIWindow inventoryWindow;
        public UIWindow mapWindow;
        public LootWindow lootWindow;
        public RevengeListWindow revengeListWindow;
        public CraftingUI craftingWindow;
        public CookingUI cookingWindow;
        public RepairStationUI repairWindow;
        public UIWindow equipmentWindow;
        public UIWindow warehouseWindow;

        // 열려있는 윈도우 스택 (ESC로 순서대로 닫기)
        private Stack<UIWindow> _openWindows = new Stack<UIWindow>();

        // 싱글톤 인스턴스
        private static UIManager _instance;
        public static UIManager Instance => _instance;

        /// <summary>
        /// [RuntimeInitializeOnLoadMethod] 폴백: 씬에 UIManager가 없으면 자동 생성.
        /// GameManager.InitializeSystems()보다 먼저 실행되어 Awake() 타이밍 문제를 방지합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreateFallback()
        {
            if (_instance != null) return;

            var existing = UnityEngine.Object.FindAnyObjectByType<UIManager>();
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var go = new GameObject("UIManager");
            go.AddComponent<UIManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            Debug.Log("[UIManager] Auto-created via RuntimeInitializeOnLoadMethod fallback.");
        }

        private void Awake()
        {
            // 싱글톤 설정 (중복 생성 방지)
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            // DontDestroyOnLoad는 Root GameObject에서만 동작하므로, 부모가 있으면 분리
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // _keyBindings가 없으면 기본값 생성
            if (_keyBindings == null)
                _keyBindings = ScriptableObject.CreateInstance<KeyBindings>();
        }

        /// <summary>외부에서 KeyBindings 설정 (SceneSetup에서 호출)</summary>
        public void SetKeyBindings(KeyBindings kb)
        {
            _keyBindings = kb;
        }

        private void Update()
        {
            // 게임이 멈춤 상태면 무시 (UI 열려있을 때)
            if (Time.timeScale == 0) return;

            // 키 입력 체크
            CheckKeyInput();
        }

        /// <summary>
        /// 키 입력을 체크하고 해당 윈도우를 Toggle
        /// </summary>
        private void CheckKeyInput()
        {
            if (_keyBindings == null) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // ESC = 현재 열린 창 닫기
            if (WasKeyPressedThisFrame(keyboard, _keyBindings.GetKey("Close")))
            {
                CloseTopWindow();
                return;
            }

            if (WasKeyPressedThisFrame(keyboard, _keyBindings.GetKey("Quest")))
                ToggleWindow(questWindow);
            if (WasKeyPressedThisFrame(keyboard, _keyBindings.GetKey("Recipe")))
                ToggleWindow(recipeWindow);
            if (WasKeyPressedThisFrame(keyboard, _keyBindings.GetKey("Inventory")))
                ToggleWindow(inventoryWindow);
            if (WasKeyPressedThisFrame(keyboard, _keyBindings.GetKey("Map")))
                ToggleWindow(mapWindow);
            if (WasKeyPressedThisFrame(keyboard, _keyBindings.GetKey("RevengeList")))
                ToggleWindow(revengeListWindow);
            if (WasKeyPressedThisFrame(keyboard, _keyBindings.GetKey("Crafting")))
                ToggleWindow(craftingWindow);
            if (WasKeyPressedThisFrame(keyboard, _keyBindings.GetKey("Equipment")))
                ToggleWindow(equipmentWindow);
            if (WasKeyPressedThisFrame(keyboard, _keyBindings.GetKey("Warehouse")))
                ToggleWindow(warehouseWindow);
        }

        private static bool WasKeyPressedThisFrame(Keyboard k, KeyCode code)
        {
            switch (code)
            {
                case KeyCode.I: return k.iKey.wasPressedThisFrame;
                case KeyCode.Q: return k.qKey.wasPressedThisFrame;
                case KeyCode.R: return k.rKey.wasPressedThisFrame;
                case KeyCode.M: return k.mKey.wasPressedThisFrame;
                case KeyCode.K: return k.kKey.wasPressedThisFrame;
                case KeyCode.C: return k.cKey.wasPressedThisFrame;
                case KeyCode.E: return k.eKey.wasPressedThisFrame;
                case KeyCode.W: return k.wKey.wasPressedThisFrame;
                case KeyCode.Escape: return k.escapeKey.wasPressedThisFrame;
                default: return false;
            }
        }

        /// <summary>
        /// 윈도우 열기/닫기 토글
        /// </summary>
        public void ToggleWindow(UIWindow window)
        {
            if (window == null) return;

            if (window.IsOpen)
            {
                // 이미 열려있으면 닫고 스택에서 제거
                window.Hide();
                if (_openWindows.Contains(window))
                {
                    var tempStack = new Stack<UIWindow>(_openWindows);
                    var newStack = new Stack<UIWindow>();
                    foreach (var w in tempStack)
                    {
                        if (w != window)
                            newStack.Push(w);
                    }
                    _openWindows = new Stack<UIWindow>(newStack);
                }

                // Phase 8.3: UI 닫힘 사운드
                SoundManager.Instance?.PlayUI("ui_close");
            }
            else
            {
                // 닫혀있으면 열고 스택에 추가
                window.Show();
                _openWindows.Push(window);

                // Phase 8.3: UI 열림 사운드
                SoundManager.Instance?.PlayUI("ui_open");
            }
        }

        /// <summary>
        /// 가장 위에 있는 창 닫기 (ESC 키)
        /// </summary>
        private void CloseTopWindow()
        {
            if (_openWindows.Count > 0)
            {
                var topWindow = _openWindows.Pop();
                if (topWindow != null)
                {
                    topWindow.Hide();
                    // Phase 8.3: UI 닫힘 사운드
                    SoundManager.Instance?.PlayUI("ui_close");
                }
            }
        }

        /// <summary>
        /// 모든 창 닫기
        /// </summary>
        public void CloseAllWindows()
        {
            while (_openWindows.Count > 0)
            {
                var window = _openWindows.Pop();
                if (window != null)
                {
                    window.Hide();
                    // Phase 8.3: UI 닫힘 사운드
                    SoundManager.Instance?.PlayUI("ui_close");
                }
            }
        }

        /// <summary>
        /// 특정 타입의 윈도우를 찾아서 엽니다.
        /// CraftingStation 등에서 호출.
        /// </summary>
        public void OpenWindow(System.Type windowType)
        {
            if (windowType == typeof(CraftingUI) && craftingWindow != null)
            {
                if (!craftingWindow.IsOpen)
                {
                    craftingWindow.Show();
                    _openWindows.Push(craftingWindow);
                    SoundManager.Instance?.PlayUI("ui_open");
                }
            }
            else if (windowType == typeof(CookingUI) && cookingWindow != null)
            {
                if (!cookingWindow.IsOpen)
                {
                    cookingWindow.Show();
                    _openWindows.Push(cookingWindow);
                    SoundManager.Instance?.PlayUI("ui_open");
                }
            }
            else
            {
                Debug.LogWarning($"[UIManager] 알 수 없는 윈도우 타입: {windowType.Name}");
            }
        }

        /// <summary>
        /// 특정 윈도우가 열려있는지 확인
        /// </summary>
        public bool IsWindowOpen<T>() where T : UIWindow
        {
            if (questWindow is T && questWindow.IsOpen) return true;
            if (recipeWindow is T && recipeWindow.IsOpen) return true;
            if (inventoryWindow is T && inventoryWindow.IsOpen) return true;
            if (mapWindow is T && mapWindow.IsOpen) return true;
            if (lootWindow is T && lootWindow.IsOpen) return true;
            if (revengeListWindow is T && revengeListWindow.IsOpen) return true;
            if (craftingWindow is T && craftingWindow.IsOpen) return true;
            if (cookingWindow is T && cookingWindow.IsOpen) return true;
            return false;
        }

        /// <summary>
        /// LootBasket을 열기 (LootBasket 오브젝트와 상호작용 시 호출)
        /// ILootBasket 인터페이스를 사용하여 UI→Systems 의존성 제거
        /// </summary>
        public void OpenLootWindow(ILootBasket basket)
        {
            if (lootWindow == null)
            {
                Debug.LogWarning("[UIManager] LootWindow가 설정되지 않았습니다!");
                return;
            }

            if (basket == null || basket.IsEmpty)
            {
                Debug.Log("[UIManager] 바스켓이 비어 있습니다.");
                return;
            }

            // 이미 다른 바스켓이 열려있으면 닫고 새로 열기
            if (lootWindow.IsOpen)
            {
                lootWindow.Hide();
                RemoveFromStack(lootWindow);
            }

            lootWindow.OpenForBasket(basket);
            _openWindows.Push(lootWindow);
        }

        /// <summary>
        /// 스택에서 특정 윈도우 제거
        /// </summary>
        private void RemoveFromStack(UIWindow window)
        {
            var tempList = new List<UIWindow>(_openWindows);
            tempList.Remove(window);
            _openWindows = new Stack<UIWindow>();
            // 역순으로 다시 푸시 (스택 순서 유지)
            for (int i = tempList.Count - 1; i >= 0; i--)
                _openWindows.Push(tempList[i]);
        }

        // --- 씬에서 UIManager를 찾을 때 사용 ---
        public static UIManager FindOrCreate()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("UIManager");
            var manager = go.AddComponent<UIManager>();
            return manager;
        }
    }
}