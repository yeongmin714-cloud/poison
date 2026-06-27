using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;
using ProjectName.Systems;

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

        // 열려있는 윈도우 리스트 (리스트 끝 = 최상단). Stack 대신 List 사용으로 GC 할당 제거.
        private List<UIWindow> _openWindows = new List<UIWindow>();

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

            // LootBasket 이벤트 구독 (LootWindow 열기 요청 처리)
            LootBasket.OnOpenLootWindowRequested += OpenLootWindow;
        }

        private void OnDestroy()
        {
            // LootBasket 이벤트 구독 해제
            LootBasket.OnOpenLootWindowRequested -= OpenLootWindow;
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
                window.Hide();
                RemoveFromStack(window);    // Stack 재구축 없이 List에서 제거
                SoundManager.Instance?.PlayUI("ui_close");
            }
            else
            {
                window.Show();
                _openWindows.Add(window);   // Stack.Push 대신 List.Add (GC 0)
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
                int lastIndex = _openWindows.Count - 1;
                var topWindow = _openWindows[lastIndex];
                _openWindows.RemoveAt(lastIndex);   // Stack.Pop 대신 RemoveAt (GC 0)
                if (topWindow != null)
                {
                    topWindow.Hide();
                    SoundManager.Instance?.PlayUI("ui_close");
                }
            }
        }

        /// <summary>
        /// 모든 창 닫기
        /// </summary>
        public void CloseAllWindows()
        {
            for (int i = _openWindows.Count - 1; i >= 0; i--)
            {
                var window = _openWindows[i];
                if (window != null)
                {
                    window.Hide();
                    SoundManager.Instance?.PlayUI("ui_close");
                }
            }
            _openWindows.Clear();   // Clear로 한 번에 정리 (GC 0)
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
                    _openWindows.Add(craftingWindow);
                    SoundManager.Instance?.PlayUI("ui_open");
                }
            }
            else if (windowType == typeof(CookingUI) && cookingWindow != null)
            {
                if (!cookingWindow.IsOpen)
                {
                    cookingWindow.Show();
                    _openWindows.Add(cookingWindow);
                    SoundManager.Instance?.PlayUI("ui_open");
                }
            }
            else if (windowType == typeof(RepairStationUI) && repairWindow != null)
            {
                if (!repairWindow.IsOpen)
                {
                    repairWindow.Show();
                    _openWindows.Add(repairWindow);
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
            _openWindows.Add(lootWindow);
        }

        /// <summary>
        /// 리스트에서 특정 윈도우 제거 (Stack 재구축 없음, GC 할당 0)
        /// </summary>
        private void RemoveFromStack(UIWindow window)
        {
            _openWindows.Remove(window);    // List.Remove = O(n) 선형탐색, 할당 0
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
