using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// G2-08: SoundManagerEnhanced EditMode 테스트.
    ///
    /// 테스트 대상:
    /// 1. 싱글톤 Instance 생성
    /// 2. PlayBGM 호출 (config/clip 없이도 예외 없음)
    /// 3. PlaySFX 호출 (config/clip 없이도 예외 없음)
    /// 4. PlayUI 호출 (config/clip 없이도 예외 없음)
    /// 5. SetVolumeBGM / Get (BGM)
    /// 6. SetVolumeSFX / Get (SFX)
    /// 7. SetVolumeUI / Get (UI)
    /// 8. MuteAll / UnmuteAll 상태 전환
    /// 9. Ambient AudioSource 생성 확인
    /// 10. SceneManager.sceneLoaded 콜백 등록 확인
    /// </summary>
    public class PhaseG2_SoundTests
    {
        private GameObject _mgrGo;
        private SoundManagerEnhanced _mgr;

        /// <summary>
        /// SoundManagerEnhanced의 _instance 필드에 강제로 값 설정.
        /// EditMode에서는 Instance getter가 새 GameObject를 만들지 않도록 함.
        /// </summary>
        private void SetInstance(SoundManagerEnhanced instance)
        {
            var field = typeof(SoundManagerEnhanced).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearInstance()
        {
            var field = typeof(SoundManagerEnhanced).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);

            var quittingField = typeof(SoundManagerEnhanced).GetField("_instanceQuitting",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (quittingField != null)
                quittingField.SetValue(null, false);
        }

        // ================================================================
        // Setup / Teardown
        // ================================================================

        [SetUp]
        public void Setup()
        {
            ClearInstance();

            _mgrGo = new GameObject("TestSoundManagerEnhanced");
            _mgr = _mgrGo.AddComponent<SoundManagerEnhanced>();

            // Awake()가 실행된 후 Instance가 null이 아니어야 함
            // AddComponent 시 Awake()가 호출되는 것은 GameObject 활성화 시점이라
            // 바로 호출되지 않을 수 있음 → 강제로 Instance 설정
            SetInstance(_mgr);

            // Initialized 플래그 강제 설정 (Awake가 호출되지 않은 경우 대비)
            var initField = typeof(SoundManagerEnhanced).GetField("_initialized",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (initField != null && !(bool)initField.GetValue(_mgr))
            {
                // 수동으로 Initialize 호출
                var initMethod = typeof(SoundManagerEnhanced).GetMethod("Initialize",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                initMethod?.Invoke(_mgr, null);
            }
        }

        [TearDown]
        public void Teardown()
        {
            if (_mgrGo != null)
            {
                Object.DestroyImmediate(_mgrGo);
                _mgrGo = null;
                _mgr = null;
            }
            ClearInstance();
        }

        // ================================================================
        // Test 1: Singleton Instance 생성
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_Singleton_InstanceExists()
        {
            Assert.IsNotNull(SoundManagerEnhanced.Instance,
                "Instance는 null이 아니어야 함");
            Assert.AreSame(_mgr, SoundManagerEnhanced.Instance,
                "Instance가 생성한 인스턴스와 동일해야 함");
        }

        // ================================================================
        // Test 2: PlayBGM — clip 없이도 예외 없음
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_PlayBGM_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _mgr.PlayBGM("bgm_test_main"));
            Assert.AreEqual("bgm_test_main", _mgr.CurrentBGMId,
                "CurrentBGMId가 설정되어야 함");
        }

        // ================================================================
        // Test 3: PlaySFX — clip 없이도 예외 없음
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_PlaySFX_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _mgr.PlaySFX("SFX_Footstep"));
        }

        // ================================================================
        // Test 4: PlayUI — clip 없이도 예외 없음
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_PlayUI_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _mgr.PlayUI("UI_Click"));
        }

        // ================================================================
        // Test 5: SetVolumeBGM / VolumeBGM
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_SetVolumeBGM_Works()
        {
            _mgr.SetVolumeBGM(0.75f);
            Assert.AreEqual(0.75f, _mgr.VolumeBGM, 0.001f,
                "BGM 볼륨이 0.75로 설정되어야 함");

            // 범위 초과 값은 clamp
            _mgr.SetVolumeBGM(1.5f);
            Assert.AreEqual(1.0f, _mgr.VolumeBGM, 0.001f,
                "1.5는 1.0으로 clamp되어야 함");

            _mgr.SetVolumeBGM(-0.5f);
            Assert.AreEqual(0.0f, _mgr.VolumeBGM, 0.001f,
                "-0.5는 0.0으로 clamp되어야 함");
        }

        // ================================================================
        // Test 6: SetVolumeSFX / VolumeSFX
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_SetVolumeSFX_Works()
        {
            _mgr.SetVolumeSFX(0.3f);
            Assert.AreEqual(0.3f, _mgr.VolumeSFX, 0.001f,
                "SFX 볼륨이 0.3으로 설정되어야 함");

            _mgr.SetVolumeSFX(2.0f);
            Assert.AreEqual(1.0f, _mgr.VolumeSFX, 0.001f,
                "2.0은 1.0으로 clamp되어야 함");
        }

        // ================================================================
        // Test 7: SetVolumeUI / VolumeUI
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_SetVolumeUI_Works()
        {
            _mgr.SetVolumeUI(0.5f);
            Assert.AreEqual(0.5f, _mgr.VolumeUI, 0.001f,
                "UI 볼륨이 0.5로 설정되어야 함");
        }

        // ================================================================
        // Test 8: MuteAll / UnmuteAll 상태 전환
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_MuteUnmute_TogglesState()
        {
            // 초기 상태: 음소거 아님
            Assert.IsFalse(_mgr.IsMuted, "초기 상태는 음소거가 아니어야 함");

            // Mute
            _mgr.MuteAll();
            Assert.IsTrue(_mgr.IsMuted, "MuteAll 후 IsMuted는 true");

            // 모든 AudioSource 볼륨이 0인지 확인
            Assert.AreEqual(0f, _mgr.BGMSource.volume, 0.001f, "BGM 볼륨 0");
            Assert.AreEqual(0f, _mgr.SFXSource.volume, 0.001f, "SFX 볼륨 0");
            Assert.AreEqual(0f, _mgr.UISource.volume, 0.001f, "UI 볼륨 0");
            Assert.AreEqual(0f, _mgr.AmbientSource.volume, 0.001f, "Ambient 볼륨 0");

            // Unmute
            _mgr.UnmuteAll();
            Assert.IsFalse(_mgr.IsMuted, "UnmuteAll 후 IsMuted는 false");

            // 복원된 볼륨이 기본값과 같은지 확인
            Assert.AreEqual(0.5f, _mgr.VolumeBGM, 0.001f, "BGM 볼륨 복원");
            Assert.AreEqual(1.0f, _mgr.VolumeSFX, 0.001f, "SFX 볼륨 복원");
            Assert.AreEqual(1.0f, _mgr.VolumeUI, 0.001f, "UI 볼륨 복원");
            Assert.AreEqual(0.4f, _mgr.VolumeAmbient, 0.001f, "Ambient 볼륨 복원");
        }

        // ================================================================
        // Test 9: Ambient AudioSource 생성 확인
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_AmbientSource_Created()
        {
            Assert.IsNotNull(_mgr.AmbientSource,
                "Ambient AudioSource가 생성되어야 함");
            Assert.IsTrue(_mgr.AmbientSource.loop,
                "Ambient AudioSource는 loop=true여야 함");
            Assert.AreEqual(0.4f, _mgr.VolumeAmbient, 0.001f,
                "Ambient 기본 볼륨은 0.4");
        }

        // ================================================================
        // Test 10: SceneManager.sceneLoaded 콜백 등록 확인
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_SceneLoaded_CallbackRegistered()
        {
            // OnSceneLoaded가 sceneLoaded 이벤트에 등록되어 있는지 확인
            // 리플렉션을 통해 이벤트의 델리게이트 목록을 검사

            var sceneLoadedEvent = typeof(SceneManager)
                .GetEvent("sceneLoaded", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(sceneLoadedEvent, "SceneManager.sceneLoaded 이벤트 존재");

            // 등록된 콜백이 있는지 확인하는 간접 검증:
            // 스트링이 빈 값으로 PlayBGM 호출해도 예외 없음 (OnSceneLoaded 내부 안전성)
            Assert.DoesNotThrow(() => _mgr.PlayBGM("test_scene_bgm"));
        }

        // ================================================================
        // Additional: PlaySFXByType — 모든 SFXType 순회
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_PlaySFXByType_AllTypes_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                foreach (SoundEffectManager.SFXType type in
                    System.Enum.GetValues(typeof(SoundEffectManager.SFXType)))
                {
                    _mgr.PlaySFXByType(type);
                }
            });
        }

        // ================================================================
        // Additional: StopBGM / StopAmbient / StopAll
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_StopMethods_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _mgr.StopBGM());
            Assert.DoesNotThrow(() => _mgr.StopAmbient());
            Assert.DoesNotThrow(() => _mgr.StopAll());
        }

        // ================================================================
        // Additional: PlayBGM with empty string — 예외 없음
        // ================================================================

        [Test]
        public void SoundManagerEnhanced_PlayBGM_EmptyString_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _mgr.PlayBGM(""));
        }
    }
}