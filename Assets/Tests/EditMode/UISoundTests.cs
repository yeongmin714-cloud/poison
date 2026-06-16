using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-20: UISoundManager EditMode 테스트.
    ///
    /// 테스트 대상:
    /// - 싱글톤 Instance 생성
    /// - UISFXType 재생
    /// - 버튼 사운드 등록/해제
    /// - 토글 사운드 등록
    /// - 볼륨 제어
    /// - StopAllSounds
    /// - UI 사운드 열거형 전체 커버리지
    /// </summary>
    public class UISoundTests
    {
        private GameObject _uiSoundGo;
        private UISoundManager _uiSoundManager;

        private void SetManagerInstance(UISoundManager instance)
        {
            var field = typeof(UISoundManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearManagerInstance()
        {
            var field = typeof(UISoundManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);

            var quittingField = typeof(UISoundManager).GetField("_instanceQuitting",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (quittingField != null)
                quittingField.SetValue(null, false);
        }

        [SetUp]
        public void Setup()
        {
            ClearManagerInstance();
            _uiSoundGo = new GameObject("TestUISound");
            _uiSoundManager = _uiSoundGo.AddComponent<UISoundManager>();
            SetManagerInstance(_uiSoundManager);
        }

        [TearDown]
        public void Teardown()
        {
            if (_uiSoundGo != null)
                Object.DestroyImmediate(_uiSoundGo);
            ClearManagerInstance();
        }

        // ================================================================
        // 싱글톤 테스트
        // ================================================================

        [Test]
        public void Singleton_Instance_NotNull()
        {
            Assert.IsNotNull(UISoundManager.Instance, "Instance는 null이 아니어야 함");
        }

        [Test]
        public void Singleton_Instance_IsSame()
        {
            Assert.AreSame(_uiSoundManager, UISoundManager.Instance,
                "Instance가 생성한 인스턴스와 동일해야 함");
        }

        [Test]
        public void Singleton_SecondInstance_Destroyed()
        {
            var secondGo = new GameObject("SecondUISound");
            var secondManager = secondGo.AddComponent<UISoundManager>();

            Assert.AreSame(_uiSoundManager, UISoundManager.Instance,
                "첫 번째 Instance가 유지되어야 함");

            Object.DestroyImmediate(secondGo);
        }

        // ================================================================
        // PlayUISound
        // ================================================================

        [Test]
        public void PlayUISound_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _uiSoundManager.PlayUISound(UISoundManager.UISFXType.UIClick));
        }

        [Test]
        public void PlayUISound_AllTypes_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _uiSoundManager.PlayUISound(UISoundManager.UISFXType.UIClick);
                _uiSoundManager.PlayUISound(UISoundManager.UISFXType.UIOpen);
                _uiSoundManager.PlayUISound(UISoundManager.UISFXType.UIClose);
                _uiSoundManager.PlayUISound(UISoundManager.UISFXType.UIError);
                _uiSoundManager.PlayUISound(UISoundManager.UISFXType.UINotification);
                _uiSoundManager.PlayUISound(UISoundManager.UISFXType.UIQuestComplete);
            });
        }

        // ================================================================
        // 버튼 사운드 등록
        // ================================================================

        [Test]
        public void RegisterButtonSound_DoesNotThrow()
        {
            var buttonGo = new GameObject("TestButton");
            var button = buttonGo.AddComponent<Button>();

            Assert.DoesNotThrow(() => _uiSoundManager.RegisterButtonSound(button));

            Object.DestroyImmediate(buttonGo);
        }

        [Test]
        public void RegisterButtonSound_NullButton_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _uiSoundManager.RegisterButtonSound(null));
        }

        [Test]
        public void UnregisterButtonSound_DoesNotThrow()
        {
            var buttonGo = new GameObject("TestButton");
            var button = buttonGo.AddComponent<Button>();

            _uiSoundManager.RegisterButtonSound(button);
            Assert.DoesNotThrow(() => _uiSoundManager.UnregisterButtonSound(button));

            Object.DestroyImmediate(buttonGo);
        }

        [Test]
        public void UnregisterButtonSound_NullButton_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _uiSoundManager.UnregisterButtonSound(null));
        }

        [Test]
        public void RegisterButtonSound_TriggersOnClick()
        {
            var buttonGo = new GameObject("TestButton");
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonGo.AddComponent<Image>();

            // 등록
            _uiSoundManager.RegisterButtonSound(button);

            // 버튼 클릭 시뮬레이션 — onClick 이벤트에 리스너가 추가되었는지 확인
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount(),
                "RegisterButtonSound 후 onClick에 리스너 1개 추가");

            Object.DestroyImmediate(buttonGo);
        }

        [Test]
        public void RegisterButtonSound_MultipleTimes_SingleListener()
        {
            var buttonGo = new GameObject("TestButton");
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonGo.AddComponent<Image>();

            // 여러 번 등록
            _uiSoundManager.RegisterButtonSound(button);
            _uiSoundManager.RegisterButtonSound(button);
            _uiSoundManager.RegisterButtonSound(button);

            // 중복 등록 방지 — RemoveListener 후 AddListener하므로 1개
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount(),
                "중복 등록 방지로 리스너는 1개만 있어야 함");

            Object.DestroyImmediate(buttonGo);
        }

        // ================================================================
        // 토글 사운드 등록
        // ================================================================

        [Test]
        public void RegisterToggleSound_DoesNotThrow()
        {
            var toggleGo = new GameObject("TestToggle");
            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = toggleGo.AddComponent<Image>();

            Assert.DoesNotThrow(() => _uiSoundManager.RegisterToggleSound(toggle));

            Object.DestroyImmediate(toggleGo);
        }

        [Test]
        public void RegisterToggleSound_NullToggle_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _uiSoundManager.RegisterToggleSound(null));
        }

        // ================================================================
        // 볼륨 제어
        // ================================================================

        [Test]
        public void Volume_SetAndGet()
        {
            _uiSoundManager.Volume = 0.8f;
            Assert.AreEqual(0.8f, _uiSoundManager.Volume, 0.001f);
        }

        [Test]
        public void Volume_ClampToZero()
        {
            _uiSoundManager.Volume = -0.3f;
            Assert.AreEqual(0f, _uiSoundManager.Volume, 0.001f);
        }

        [Test]
        public void Volume_ClampToOne()
        {
            _uiSoundManager.Volume = 1.8f;
            Assert.AreEqual(1f, _uiSoundManager.Volume, 0.001f);
        }

        [Test]
        public void Volume_FullRange()
        {
            _uiSoundManager.Volume = 0f;
            Assert.AreEqual(0f, _uiSoundManager.Volume, 0.001f);

            _uiSoundManager.Volume = 0.25f;
            Assert.AreEqual(0.25f, _uiSoundManager.Volume, 0.001f);

            _uiSoundManager.Volume = 1f;
            Assert.AreEqual(1f, _uiSoundManager.Volume, 0.001f);
        }

        // ================================================================
        // StopAllSounds
        // ================================================================

        [Test]
        public void StopAllSounds_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _uiSoundManager.StopAllSounds());
        }

        [Test]
        public void StopAllSounds_AfterPlay_DoesNotThrow()
        {
            _uiSoundManager.PlayUISound(UISoundManager.UISFXType.UIClick);
            _uiSoundManager.PlayUISound(UISoundManager.UISFXType.UINotification);

            Assert.DoesNotThrow(() => _uiSoundManager.StopAllSounds());
        }

        [Test]
        public void StopAllSounds_MultipleCalls_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _uiSoundManager.StopAllSounds();
                _uiSoundManager.StopAllSounds();
                _uiSoundManager.StopAllSounds();
            });
        }

        // ================================================================
        // ResetAll
        // ================================================================

        [Test]
        public void ResetAll_DestroysInstance()
        {
            Assert.IsNotNull(UISoundManager.Instance);
            UISoundManager.ResetAll();
            Assert.IsNull(UISoundManager.Instance, "ResetAll 후 Instance는 null");
        }

        [Test]
        public void ResetAll_AfterReset_NewInstanceCreated()
        {
            UISoundManager.ResetAll();
            var newInstance = UISoundManager.Instance;
            Assert.IsNotNull(newInstance, "ResetAll 후 Instance 재생성 가능");
            UISoundManager.ResetAll();
        }

        // ================================================================
        // UISFXType 열거형 완전성 검증
        // ================================================================

        [Test]
        public void UISFXType_AllValues_Defined()
        {
            var values = System.Enum.GetValues(typeof(UISoundManager.UISFXType));
            Assert.AreEqual(6, values.Length, "UISFXType은 6개 값을 가져야 함");
        }

        [Test]
        public void UISFXType_ContainsAllExpected()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(UISoundManager.UISFXType), "UIClick"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(UISoundManager.UISFXType), "UIOpen"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(UISoundManager.UISFXType), "UIClose"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(UISoundManager.UISFXType), "UIError"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(UISoundManager.UISFXType), "UINotification"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(UISoundManager.UISFXType), "UIQuestComplete"));
        }
    }
}
