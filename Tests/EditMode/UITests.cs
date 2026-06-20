using NUnit.Framework;
using UnityEngine;
using ProjectName.UI;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// UI 프레임워크 테스트.
    /// 테스트 가능한 부분 (KeyBindings, UIWindow 기본 동작)을 검증합니다.
    /// </summary>
    public class UITests
    {
        // ===================== KeyBindings 테스트 =====================

        [Test]
        public void KeyBindings_DefaultValues_AreCorrect()
        {
            var bindings = ScriptableObject.CreateInstance<KeyBindings>();

            Assert.AreEqual(KeyCode.Q, bindings.GetKey("Quest"), "퀘스트 기본키는 Q");
            Assert.AreEqual(KeyCode.R, bindings.GetKey("Recipe"), "레시피 기본키는 R");
            Assert.AreEqual(KeyCode.I, bindings.GetKey("Inventory"), "인벤토리 기본키는 I");
            Assert.AreEqual(KeyCode.M, bindings.GetKey("Map"), "지도 기본키는 M");
            Assert.AreEqual(KeyCode.Escape, bindings.GetKey("Close"), "닫기 기본키는 ESC");
        }

        [Test]
        public void KeyBindings_CanChangeKey()
        {
            var bindings = ScriptableObject.CreateInstance<KeyBindings>();

            bindings.SetKey("Quest", KeyCode.F1);
            Assert.AreEqual(KeyCode.F1, bindings.GetKey("Quest"), "F1으로 변경 가능");
        }

        [Test]
        public void KeyBindings_UnknownAction_ReturnsNone()
        {
            var bindings = ScriptableObject.CreateInstance<KeyBindings>();
            Assert.AreEqual(KeyCode.None, bindings.GetKey("NonExistent"));
        }

        [Test]
        public void KeyBindings_HasAllActions()
        {
            var actions = KeyBindings.GetActionNames();
            Assert.AreEqual(5, actions.Length, "5개 액션이 있어야 함");
            Assert.Contains("Quest", actions);
            Assert.Contains("Recipe", actions);
            Assert.Contains("Inventory", actions);
            Assert.Contains("Map", actions);
            Assert.Contains("Close", actions);
        }

        // ===================== UIWindow 테스트 =====================

        [Test]
        public void UIWindow_OnCreate_IsClosed()
        {
            var go = new GameObject("TestWindow");
            var window = go.AddComponent<TestWindow>();

            Assert.IsFalse(window.IsOpen, "생성 직후에는 닫혀있어야 함");
        }

        [Test]
        public void UIWindow_Show_SetsIsOpen()
        {
            var go = new GameObject("TestWindow");
            var window = go.AddComponent<TestWindow>();
            window.SetRoot(go);

            window.Show();

            Assert.IsTrue(window.IsOpen, "Show() 후에는 열려있어야 함");
        }

        [Test]
        public void UIWindow_Hide_SetsIsClosed()
        {
            var go = new GameObject("TestWindow");
            var window = go.AddComponent<TestWindow>();
            window.SetRoot(go);

            window.Show();
            window.Hide();

            Assert.IsFalse(window.IsOpen, "Hide() 후에는 닫혀있어야 함");
        }

        [Test]
        public void UIWindow_Toggle_ChangesState()
        {
            var go = new GameObject("TestWindow");
            var window = go.AddComponent<TestWindow>();
            window.SetRoot(go);

            // Toggle 1: 닫힘 → 열림
            window.Toggle();
            Assert.IsTrue(window.IsOpen, "첫 Toggle = 열림");

            // Toggle 2: 열림 → 닫힘
            window.Toggle();
            Assert.IsFalse(window.IsOpen, "두번째 Toggle = 닫힘");
        }

        [Test]
        public void UIWindow_MultipleToggle_Works()
        {
            var go = new GameObject("TestWindow");
            var window = go.AddComponent<TestWindow>();
            window.SetRoot(go);

            for (int i = 0; i < 5; i++)
                window.Toggle();

            // 5번 토글 = 홀수 → 열림
            Assert.IsTrue(window.IsOpen, "5번 토글 후 열림");

            window.Toggle();
            Assert.IsFalse(window.IsOpen, "6번 토글 후 닫힘");
        }

        // ===================== UIManager 테스트 =====================

        [Test]
        public void UIManager_FindOrCreate_CreatesInstance()
        {
            var manager = UIManager.FindOrCreate();

            Assert.IsNotNull(manager, "UIManager 인스턴스 생성");
            Assert.AreEqual(manager, UIManager.Instance, "Instance가 생성된 매니저와 같음");
        }

        [Test]
        public void UIManager_CloseAllWindows_ClosesAll()
        {
            var manager = UIManager.FindOrCreate();

            var win1 = CreateWindow("Win1");
            var win2 = CreateWindow("Win2");

            manager.CloseAllWindows();

            Assert.IsFalse(win1.IsOpen, "모든 창 닫힘");
            Assert.IsFalse(win2.IsOpen, "모든 창 닫힘");
        }

        // 헬퍼: 테스트용 윈도우 생성
        private TestWindow CreateWindow(string name)
        {
            var go = new GameObject(name);
            var window = go.AddComponent<TestWindow>();
            window.SetRoot(go);
            return window;
        }
    }

    /// <summary>
    /// 테스트용 UIWindow 구현 (추상 클래스는 직접 생성 불가)
    /// </summary>
    public class TestWindow : UIWindow
    {
        public void SetRoot(GameObject root)
        {
            _windowRoot = root;
        }

        protected override void OnShow()
        {
            // 테스트용 — 추가 동작 없음
        }

        protected override void OnHide()
        {
            // 테스트용 — 추가 동작 없음
        }
    }
}