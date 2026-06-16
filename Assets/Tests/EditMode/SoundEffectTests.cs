using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-19: SoundEffectManager EditMode 테스트.
    ///
    /// 테스트 대상:
    /// - 싱글톤 Instance 생성
    /// - AudioSource 풀 초기화
    /// - SFXType 재생
    /// - 3D 공간 재생
    /// - 볼륨 제어
    /// - StopAllSFX
    /// - SFX 열거형 전체 커버리지
    /// </summary>
    public class SoundEffectTests
    {
        private GameObject _sfxGo;
        private SoundEffectManager _sfxManager;

        private void SetManagerInstance(SoundEffectManager instance)
        {
            var field = typeof(SoundEffectManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, instance);
        }

        private void ClearManagerInstance()
        {
            var field = typeof(SoundEffectManager).GetField("_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(null, null);

            var quittingField = typeof(SoundEffectManager).GetField("_instanceQuitting",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (quittingField != null)
                quittingField.SetValue(null, false);
        }

        [SetUp]
        public void Setup()
        {
            ClearManagerInstance();
            _sfxGo = new GameObject("TestSFX");
            _sfxManager = _sfxGo.AddComponent<SoundEffectManager>();
            SetManagerInstance(_sfxManager);
        }

        [TearDown]
        public void Teardown()
        {
            if (_sfxGo != null)
                Object.DestroyImmediate(_sfxGo);
            ClearManagerInstance();
        }

        // ================================================================
        // 싱글톤 테스트
        // ================================================================

        [Test]
        public void Singleton_Instance_NotNull()
        {
            Assert.IsNotNull(SoundEffectManager.Instance, "Instance는 null이 아니어야 함");
        }

        [Test]
        public void Singleton_Instance_IsSame()
        {
            Assert.AreSame(_sfxManager, SoundEffectManager.Instance,
                "Instance가 생성한 인스턴스와 동일해야 함");
        }

        [Test]
        public void Singleton_SecondInstance_Destroyed()
        {
            var secondGo = new GameObject("SecondSFX");
            var secondManager = secondGo.AddComponent<SoundEffectManager>();

            Assert.AreSame(_sfxManager, SoundEffectManager.Instance,
                "첫 번째 Instance가 유지되어야 함");

            Object.DestroyImmediate(secondGo);
        }

        // ================================================================
        // AudioSource 풀
        // ================================================================

        [Test]
        public void PoolSize_Default_IsFour()
        {
            Assert.AreEqual(4, _sfxManager.PoolSize, "기본 풀 크기는 4");
        }

        [Test]
        public void PlaySFX_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _sfxManager.PlaySFX(SoundEffectManager.SFXType.Footstep));
        }

        [Test]
        public void PlaySFX_AllTypes_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.Footstep);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.Gather);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.Craft);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.Combat_Hit);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.Combat_Swing);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.Assassination);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.DoorOpen);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.DoorClose);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.ItemPickup);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.ItemDrop);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.Alarm);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.Victory);
                _sfxManager.PlaySFX(SoundEffectManager.SFXType.Defeat);
            });
        }

        // ================================================================
        // 3D 공간 재생
        // ================================================================

        [Test]
        public void PlaySFXAtPoint_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                _sfxManager.PlaySFXAtPoint(SoundEffectManager.SFXType.Footstep, Vector3.zero));
        }

        [Test]
        public void PlaySFXAtPoint_DifferentPositions_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _sfxManager.PlaySFXAtPoint(SoundEffectManager.SFXType.Alarm, new Vector3(10f, 0f, 5f));
                _sfxManager.PlaySFXAtPoint(SoundEffectManager.SFXType.Victory, new Vector3(-5f, 2f, 3f));
            });
        }

        // ================================================================
        // IsPlaying
        // ================================================================

        [Test]
        public void IsPlaying_Default_False()
        {
            Assert.IsFalse(_sfxManager.IsPlaying(SoundEffectManager.SFXType.Footstep),
                "기본 상태에서 IsPlaying은 false");
        }

        [Test]
        public void IsPlaying_AllTypes_False()
        {
            foreach (SoundEffectManager.SFXType type in System.Enum.GetValues(typeof(SoundEffectManager.SFXType)))
            {
                Assert.IsFalse(_sfxManager.IsPlaying(type), $"IsPlaying({type})은 false여야 함");
            }
        }

        // ================================================================
        // 볼륨 제어
        // ================================================================

        [Test]
        public void Volume_SetAndGet()
        {
            _sfxManager.Volume = 0.7f;
            Assert.AreEqual(0.7f, _sfxManager.Volume, 0.001f);
        }

        [Test]
        public void Volume_ClampToZero()
        {
            _sfxManager.Volume = -0.5f;
            Assert.AreEqual(0f, _sfxManager.Volume, 0.001f);
        }

        [Test]
        public void Volume_ClampToOne()
        {
            _sfxManager.Volume = 2.0f;
            Assert.AreEqual(1f, _sfxManager.Volume, 0.001f);
        }

        [Test]
        public void Volume_FullRange()
        {
            _sfxManager.Volume = 0f;
            Assert.AreEqual(0f, _sfxManager.Volume, 0.001f);

            _sfxManager.Volume = 0.5f;
            Assert.AreEqual(0.5f, _sfxManager.Volume, 0.001f);

            _sfxManager.Volume = 1f;
            Assert.AreEqual(1f, _sfxManager.Volume, 0.001f);
        }

        // ================================================================
        // StopAllSFX
        // ================================================================

        [Test]
        public void StopAllSFX_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _sfxManager.StopAllSFX());
        }

        [Test]
        public void StopAllSFX_AfterPlay_DoesNotThrow()
        {
            _sfxManager.PlaySFX(SoundEffectManager.SFXType.Footstep);
            _sfxManager.PlaySFX(SoundEffectManager.SFXType.Alarm);
            _sfxManager.PlaySFX(SoundEffectManager.SFXType.Victory);

            Assert.DoesNotThrow(() => _sfxManager.StopAllSFX());
        }

        // ================================================================
        // 풀 순환 (라운드 로빈)
        // ================================================================

        [Test]
        public void PlaySFX_MultipleCalls_PoolCycles()
        {
            // 풀 크기(4)보다 많은 SFX 호출 — 라운드 로빈 순환
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    _sfxManager.PlaySFX(SoundEffectManager.SFXType.Footstep);
                }
            });
        }

        // ================================================================
        // ResetAll
        // ================================================================

        [Test]
        public void ResetAll_DestroysInstance()
        {
            Assert.IsNotNull(SoundEffectManager.Instance);
            SoundEffectManager.ResetAll();
            Assert.IsNull(SoundEffectManager.Instance, "ResetAll 후 Instance는 null");
        }

        [Test]
        public void ResetAll_AfterReset_NewInstanceCreated()
        {
            SoundEffectManager.ResetAll();
            var newInstance = SoundEffectManager.Instance;
            Assert.IsNotNull(newInstance, "ResetAll 후 Instance 재생성 가능");
            SoundEffectManager.ResetAll();
        }

        // ================================================================
        // SFXType 열거형 완전성 검증
        // ================================================================

        [Test]
        public void SFXType_AllValues_Defined()
        {
            var values = System.Enum.GetValues(typeof(SoundEffectManager.SFXType));
            Assert.AreEqual(13, values.Length, "SFXType은 13개 값을 가져야 함");
        }

        [Test]
        public void SFXType_ContainsAllExpected()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Footstep"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Gather"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Craft"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Combat_Hit"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Combat_Swing"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Assassination"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "DoorOpen"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "DoorClose"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "ItemPickup"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "ItemDrop"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Alarm"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Victory"));
            Assert.IsTrue(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Defeat"));
        }
    }
}
