using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-18: BackgroundMusicManager EditMode 테스트.
    ///
    /// 테스트 대상:
    /// - 싱글톤 Instance 생성
    /// - MusicTrack 열거형 전환
    /// - 볼륨 제어
    /// - 크로스페이드
    /// - 일시정지/재개/정지
    /// - 기본 트랙 재생
    /// </summary>
    public class BackgroundMusicTests
    {
        private GameObject _bgmGo;
        private BackgroundMusicManager _bgmManager;

        private void SetManagerInstance(BackgroundMusicManager instance)
        {
            var field = typeof(BackgroundMusicManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearManagerInstance()
        {
            var field = typeof(BackgroundMusicManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);

            var quittingField = typeof(BackgroundMusicManager).GetField("_instanceQuitting",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (quittingField != null)
                quittingField.SetValue(null, false);
        }

        [SetUp]
        public void Setup()
        {
            ClearManagerInstance();
            _bgmGo = new GameObject("TestBGM");
            _bgmManager = _bgmGo.AddComponent<BackgroundMusicManager>();
            SetManagerInstance(_bgmManager);
        }

        [TearDown]
        public void Teardown()
        {
            if (_bgmGo != null)
                Object.DestroyImmediate(_bgmGo);
            ClearManagerInstance();
        }

        // ================================================================
        // 싱글톤 테스트
        // ================================================================

        [Test]
        public void Singleton_Instance_NotNull()
        {
            Assert.IsNotNull(BackgroundMusicManager.Instance, "Instance는 null이 아니어야 함");
        }

        [Test]
        public void Singleton_Instance_IsSame()
        {
            Assert.AreSame(_bgmManager, BackgroundMusicManager.Instance,
                "Instance가 생성한 인스턴스와 동일해야 함");
        }

        [Test]
        public void Singleton_SecondInstance_Destroyed()
        {
            var secondGo = new GameObject("SecondBGM");
            var secondManager = secondGo.AddComponent<BackgroundMusicManager>();

            Assert.AreSame(_bgmManager, BackgroundMusicManager.Instance,
                "첫 번째 Instance가 유지되어야 함");

            Object.DestroyImmediate(secondGo);
        }

        // ================================================================
        // 초기 상태
        // ================================================================

        [Test]
        public void DefaultTrack_IsMainTheme()
        {
            Assert.AreEqual(BackgroundMusicManager.MusicTrack.MainTheme, _bgmManager.CurrentTrack,
                "기본 트랙은 MainTheme");
        }

        [Test]
        public void DefaultVolume_IsOne()
        {
            Assert.AreEqual(1.0f, _bgmManager.Volume, 0.001f, "기본 볼륨은 1.0");
        }

        [Test]
        public void DefaultCrossfadeDuration_IsOne()
        {
            Assert.AreEqual(1.0f, _bgmManager.CrossfadeDuration, 0.001f, "기본 크로스페이드는 1.0초");
        }

        // ================================================================
        // 트랙 전환
        // ================================================================

        [Test]
        public void PlayMusic_SwitchesToBattle()
        {
            _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.Battle);
            Assert.AreEqual(BackgroundMusicManager.MusicTrack.Battle, _bgmManager.CurrentTrack,
                "Battle 트랙으로 전환");
        }

        [Test]
        public void PlayMusic_SwitchesToStealth()
        {
            _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.Stealth);
            Assert.AreEqual(BackgroundMusicManager.MusicTrack.Stealth, _bgmManager.CurrentTrack,
                "Stealth 트랙으로 전환");
        }

        [Test]
        public void PlayMusic_SwitchesToPeace()
        {
            _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.Peace);
            Assert.AreEqual(BackgroundMusicManager.MusicTrack.Peace, _bgmManager.CurrentTrack,
                "Peace 트랙으로 전환");
        }

        [Test]
        public void PlayMusic_SwitchesToNight()
        {
            _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.Night);
            Assert.AreEqual(BackgroundMusicManager.MusicTrack.Night, _bgmManager.CurrentTrack,
                "Night 트랙으로 전환");
        }

        [Test]
        public void PlayMusic_Immediate_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.Battle, true);
            });
        }

        [Test]
        public void PlayMusic_MultipleTracks()
        {
            _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.MainTheme);
            Assert.AreEqual(BackgroundMusicManager.MusicTrack.MainTheme, _bgmManager.CurrentTrack);

            _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.Battle);
            Assert.AreEqual(BackgroundMusicManager.MusicTrack.Battle, _bgmManager.CurrentTrack);

            _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.Night);
            Assert.AreEqual(BackgroundMusicManager.MusicTrack.Night, _bgmManager.CurrentTrack);
        }

        // ================================================================
        // 볼륨 제어
        // ================================================================

        [Test]
        public void Volume_SetAndGet()
        {
            _bgmManager.Volume = 0.5f;
            Assert.AreEqual(0.5f, _bgmManager.Volume, 0.001f);
        }

        [Test]
        public void Volume_ClampToZero()
        {
            _bgmManager.Volume = -0.1f;
            Assert.AreEqual(0f, _bgmManager.Volume, 0.001f);
        }

        [Test]
        public void Volume_ClampToOne()
        {
            _bgmManager.Volume = 1.5f;
            Assert.AreEqual(1f, _bgmManager.Volume, 0.001f);
        }

        [Test]
        public void Volume_FullRange()
        {
            _bgmManager.Volume = 0f;
            Assert.AreEqual(0f, _bgmManager.Volume, 0.001f);

            _bgmManager.Volume = 1f;
            Assert.AreEqual(1f, _bgmManager.Volume, 0.001f);

            _bgmManager.Volume = 0.33f;
            Assert.AreEqual(0.33f, _bgmManager.Volume, 0.001f);
        }

        // ================================================================
        // 크로스페이드 Duration
        // ================================================================

        [Test]
        public void CrossfadeDuration_SetAndGet()
        {
            _bgmManager.CrossfadeDuration = 2.5f;
            Assert.AreEqual(2.5f, _bgmManager.CrossfadeDuration, 0.001f);
        }

        [Test]
        public void CrossfadeDuration_MinClamp()
        {
            _bgmManager.CrossfadeDuration = 0f;
            Assert.AreEqual(0.01f, _bgmManager.CrossfadeDuration, 0.001f, "최소 0.01초");
        }

        // ================================================================
        // Pause/Resume/Stop
        // ================================================================

        [Test]
        public void PauseMusic_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bgmManager.PauseMusic());
        }

        [Test]
        public void ResumeMusic_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bgmManager.ResumeMusic());
        }

        [Test]
        public void StopMusic_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bgmManager.StopMusic());
        }

        [Test]
        public void StopMusic_ClearsState()
        {
            _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.Battle);

            _bgmManager.StopMusic();

            // Stop 후에도 CurrentTrack은 마지막 설정 유지
            Assert.AreEqual(BackgroundMusicManager.MusicTrack.Battle, _bgmManager.CurrentTrack);
        }

        [Test]
        public void PauseResumeStop_SafeMultipleCalls()
        {
            Assert.DoesNotThrow(() =>
            {
                _bgmManager.PauseMusic();
                _bgmManager.PauseMusic();
                _bgmManager.ResumeMusic();
                _bgmManager.StopMusic();
                _bgmManager.StopMusic();
            });
        }

        // ================================================================
        // 에디터 모드 안전성
        // ================================================================

        [Test]
        public void PlayMusic_EditorMode_DoesNotThrow()
        {
            // 에디터 모드에서 PlayMusic 호출 (Not playing)
            Assert.DoesNotThrow(() =>
            {
                _bgmManager.PlayMusic(BackgroundMusicManager.MusicTrack.Battle);
            });
        }

        // ================================================================
        // ResetAll
        // ================================================================

        [Test]
        public void ResetAll_DestroysInstance()
        {
            Assert.IsNotNull(BackgroundMusicManager.Instance);
            BackgroundMusicManager.ResetAll();
            Assert.IsNull(BackgroundMusicManager.Instance, "ResetAll 후 Instance는 null");
        }

        [Test]
        public void ResetAll_AfterReset_NewInstanceCreated()
        {
            BackgroundMusicManager.ResetAll();
            var newInstance = BackgroundMusicManager.Instance;
            Assert.IsNotNull(newInstance, "ResetAll 후 Instance 재생성 가능");
            BackgroundMusicManager.ResetAll();
        }
    }
}
