using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// Phase 34: 국기 등록 화면 (PlayerFlagRegistrationWindow) EditMode 테스트.
    /// </summary>
    public class Phase34_FlagRegistrationTests
    {
        private GameObject _root;
        private PlayerFlagRegistrationWindow _window;
        private GameObject _emblemGo;
        private GameObject _statsGo;

        [SetUp]
        public void Setup()
        {
            // 이전 테스트의 싱글톤 참조 정리 (auto-property backing field)
            ClearSingletonInstance<EmblemManager>("<Instance>k__BackingField");
            ClearSingletonInstance<PlayerStats>("<Instance>k__BackingField");

            // 루트 오브젝트
            _root = new GameObject("TestRoot");
            _window = _root.AddComponent<PlayerFlagRegistrationWindow>();

            // EmblemManager 싱글톤
            _emblemGo = new GameObject("TestEmblemManager");
            _emblemGo.AddComponent<EmblemManager>();

            // PlayerStats 싱글톤 (골드 관리)
            _statsGo = new GameObject("TestPlayerStats");
            var stats = _statsGo.AddComponent<PlayerStats>();
            stats.AddGold(500);
        }

        [TearDown]
        public void Teardown()
        {
            if (_window != null && _window.gameObject != null)
                Object.DestroyImmediate(_window.gameObject);
            if (_emblemGo != null)
                Object.DestroyImmediate(_emblemGo);
            if (_statsGo != null)
                Object.DestroyImmediate(_statsGo);
            if (_root != null)
                Object.DestroyImmediate(_root);

            ClearSingletonInstance<EmblemManager>("<Instance>k__BackingField");
            ClearSingletonInstance<PlayerStats>("<Instance>k__BackingField");
        }

        private static void ClearSingletonInstance<T>(string backingFieldName)
        {
            var field = typeof(T).GetField(backingFieldName,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);
        }

        // ── 1. Window opens/closes ──
        [Test]
        public void Window_StartsClosed()
        {
            Assert.IsFalse(_window.IsOpen, "Window should start closed");
        }

        [Test]
        public void Window_OpenAndClose()
        {
            _window.Show();
            Assert.IsTrue(_window.IsOpen, "Window should be open after Show()");

            _window.Hide();
            Assert.IsFalse(_window.IsOpen, "Window should be closed after Hide()");
        }

        // ── 2. OnShow loads current emblem as defaults ──
        [Test]
        public void OnShow_LoadsDefaultsFromCurrentEmblem()
        {
            _window.Show();
            Assert.AreEqual("내 문장", _window.EditName);
            Assert.AreEqual(EmblemColor.Gold, _window.EditPrimaryColor);
            Assert.AreEqual(EmblemShape.Shield, _window.EditShape);
        }

        [Test]
        public void OnShow_LoadsCustomEmblemDefaults()
        {
            // Change emblem to custom values first
            var customEmblem = new PlayerEmblemData
            {
                emblemName = "용의 깃발",
                shape = EmblemShape.Dragon,
                primaryColor = EmblemColor.Red,
                secondaryColor = EmblemColor.Blue
            };
            EmblemManager.Instance.ChangeEmblem(customEmblem, 500);

            _window.Show();
            Assert.AreEqual("용의 깃발", _window.EditName);
            Assert.AreEqual(EmblemColor.Red, _window.EditPrimaryColor);
            Assert.AreEqual(EmblemShape.Dragon, _window.EditShape);
        }

        // ── 3. Color selection changes preview ──
        [Test]
        public void SetColor_ChangesPrimaryColor()
        {
            _window.Show();

            _window.SetEditColor(EmblemColor.Blue);
            Assert.AreEqual(EmblemColor.Blue, _window.EditPrimaryColor);

            _window.SetEditColor(EmblemColor.Purple);
            Assert.AreEqual(EmblemColor.Purple, _window.EditPrimaryColor);

            _window.SetEditColor(EmblemColor.Black);
            Assert.AreEqual(EmblemColor.Black, _window.EditPrimaryColor);
        }

        // ── 4. Shape selection changes preview ──
        [Test]
        public void SetShape_ChangesEditShape()
        {
            _window.Show();

            _window.SetEditShape(EmblemShape.Dragon);
            Assert.AreEqual(EmblemShape.Dragon, _window.EditShape);

            _window.SetEditShape(EmblemShape.Crown);
            Assert.AreEqual(EmblemShape.Crown, _window.EditShape);
        }

        // ── 5. Name truncation (max 8 chars) ──
        [Test]
        public void NameField_TruncatesAt8Chars()
        {
            _window.Show();

            // Simulate setting a long name; OnGUI's TextField caps at 8,
            // but we test the internal setter properly caps
            _window.SetEditName("열두글자이름입니다");
            string name = _window.EditName;
            Assert.LessOrEqual(name.Length, 8, "Name should be at most 8 characters");
        }

        [Test]
        public void NameField_AcceptsShortName()
        {
            _window.Show();
            _window.SetEditName("문장");
            Assert.AreEqual("문장", _window.EditName);
        }

        // ── 6. Confirm with valid data (enough gold) ──
        [Test]
        public void Confirm_WithValidData_ChangesEmblemAndCloses()
        {
            _window.Show();

            _window.SetEditName("용의국기");
            _window.SetEditColor(EmblemColor.Red);
            _window.SetEditShape(EmblemShape.Dragon);

            _window.TestConfirm();

            Assert.AreEqual("용의국기", EmblemManager.Instance.CurrentEmblem.emblemName);
            Assert.AreEqual(EmblemColor.Red, EmblemManager.Instance.CurrentEmblem.primaryColor);
            Assert.AreEqual(EmblemShape.Dragon, EmblemManager.Instance.CurrentEmblem.shape);
            Assert.IsFalse(_window.IsOpen, "Window should close after successful confirm");
        }

        // ── 7. Confirm with insufficient gold ──
        [Test]
        public void Confirm_WithInsufficientGold_ShowsErrorAndStaysOpen()
        {
            // Reset gold to 50 (less than 100 cost)
            PlayerStats.Instance.SpendGold(450);

            _window.Show();
            _window.SetEditName("가난한국기");
            _window.TestConfirm();

            // Emblem should NOT be changed (still defaults)
            Assert.AreEqual("내 문장", EmblemManager.Instance.CurrentEmblem.emblemName);
            Assert.IsTrue(_window.IsOpen, "Window should stay open on failure");
            Assert.IsTrue(_window.HasMessage, "Should show error message");
            Assert.IsTrue(_window.Message.Contains("골드") || _window.Message.Contains("부족"), "Message should mention gold/insufficient");
        }

        // ── 8. Confirm with exactly enough gold ──
        [Test]
        public void Confirm_WithExactlyEnoughGold_Succeeds()
        {
            // Reset to exactly 100 gold
            PlayerStats.Instance.SpendGold(400); // was 500, now 100

            _window.Show();
            _window.SetEditName("딱맞는국기");
            _window.SetEditColor(EmblemColor.Green);
            _window.SetEditShape(EmblemShape.Star);

            _window.TestConfirm();

            Assert.AreEqual("딱맞는국기", EmblemManager.Instance.CurrentEmblem.emblemName);
            Assert.IsFalse(_window.IsOpen);
        }

        // ── 9. Cost display ──
        [Test]
        public void ChangeCost_Is100()
        {
            Assert.IsNotNull(EmblemManager.Instance);
            Assert.AreEqual(100, EmblemManager.Instance.ChangeCost);
        }

        // ── 10. Secondary color preserved after change ──
        [Test]
        public void Confirm_PreservesSecondaryColor()
        {
            _window.Show();
            _window.SetEditName("보조색유지");
            _window.TestConfirm();

            Assert.AreEqual(EmblemColor.Red, EmblemManager.Instance.CurrentEmblem.secondaryColor,
                "secondaryColor should remain as default (Red)");
        }

        // ── 11. Toggle works ──
        [Test]
        public void Window_Toggle_OpensAndCloses()
        {
            Assert.IsFalse(_window.IsOpen);

            _window.Toggle();
            Assert.IsTrue(_window.IsOpen, "Toggle should open window");

            _window.Toggle();
            Assert.IsFalse(_window.IsOpen, "Toggle should close window");
        }

        // ── 12. Confirm with empty name shows error ──
        [Test]
        public void Confirm_WithEmptyName_ShowsError()
        {
            _window.Show();
            _window.SetEditName("");
            _window.TestConfirm();

            Assert.IsTrue(_window.HasMessage, "Should show message for empty name");
            Assert.IsTrue(_window.Message.Contains("이름"), "Message should mention name");
            Assert.IsTrue(_window.IsOpen, "Window should stay open");
        }
    }
}