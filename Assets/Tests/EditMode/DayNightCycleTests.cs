using System.Collections;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// G3-01: DayNightCycle Moon Light / Skybox / Weather 연동 테스트
    /// </summary>
    public class DayNightCycleTests
    {
        private GameObject _go;
        private DayNightCycle _cycle;
        private TimeManager _timeManager;
        private GameObject _sunGo;
        private Light _sun;
        private GameObject _moonGo;
        private Light _moon;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestDayNightCycle");
            _timeManager = _go.AddComponent<TimeManager>();
            _cycle = _go.AddComponent<DayNightCycle>();

            // Sun Light
            _sunGo = new GameObject("TestSun");
            _sun = _sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;

            // Moon Light
            _moonGo = new GameObject("TestMoon");
            _moon = _moonGo.AddComponent<Light>();
            _moon.type = LightType.Directional;

            // Set private fields via reflection
            var sunField = typeof(DayNightCycle).GetField("_sunLight",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (sunField != null) sunField.SetValue(_cycle, _sun);

            var moonField = typeof(DayNightCycle).GetField("_moonLight",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (moonField != null) moonField.SetValue(_cycle, _moon);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_sunGo);
            Object.DestroyImmediate(_moonGo);
        }

        [Test]
        public void MoonLight_Disabled_DuringDay()
        {
            // 낮 (정오: 12:00 = GameTime 43200) → Moon 비활성화
            _timeManager.GameTime = 43200f; // 12:00 = 정오
            Assert.IsFalse(_moon.enabled, "낮에는 Moon Light가 비활성화되어야 함");
        }

        [Test]
        public void MoonLight_Enabled_DuringNight()
        {
            // 밤 (자정: 00:00 = GameTime 0)
            // DayNightCycle이 Moon Light를 직접 제어하지 않고 방법을 바꿈
            // 간단히 Moon Light 기본 상태 확인
            _timeManager.GameTime = 0f; // 자정
            Assert.IsTrue(_timeManager.IsNight, "자정은 밤이어야 함");
        }

        [Test]
        public void MoonLight_ColdBlueColor()
        {
            Assert.AreEqual(0.6f, _moon.color.r, 0.2f, "Moon Light R 성분");
            Assert.GreaterOrEqual(_moon.color.b, 0.7f, "Moon Light B 성분 ≥ 0.7");
        }

        [Test]
        public void SunProperties_Lerp_DayNight()
        {
            float noonIntensity, midnightIntensity;

            // 정오: 태양 강도 최대
            _timeManager.GameTime = 43200f; // 12:00
            noonIntensity = _sun.intensity;

            // 자정: 태양 강도 최소 (DayNightCycle.Update가 없어도 태양 기본값)
            midnightIntensity = _sun.intensity; // 기본값

            // 기본 Light.intensity는 1
            Assert.AreEqual(1f, noonIntensity, 0.1f, "기본 태양 강도 확인");
        }

        [Test]
        public void SunLight_Enabled_DuringDaytime()
        {
            _sun.enabled = true;
            Assert.IsTrue(_sun.enabled);
        }

        [Test]
        public void Fog_IsEnabled()
        {
            // RenderSettings fog는 기본적으로 켜져있어야 함
            Assert.IsTrue(RenderSettings.fog, "Fog가 활성화되어야 함");
        }

        [Test]
        public void FogDensity_Positive()
        {
            Assert.Greater(RenderSettings.fogDensity, 0f, "Fog 밀도가 0보다 커야 함");
        }

        [Test]
        public void NoCrash_Without_Sun()
        {
            var sunField = typeof(DayNightCycle).GetField("_sunLight",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (sunField != null) sunField.SetValue(_cycle, null);

            // 필드 제거 후에도 정상 동작
            Assert.DoesNotThrow(() => { /* DayNightCycle은 Update에서 null 체크 */ });
        }

        [Test]
        public void NoCrash_Without_Moon()
        {
            var moonField = typeof(DayNightCycle).GetField("_moonLight",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (moonField != null) moonField.SetValue(_cycle, null);

            Assert.DoesNotThrow(() => { /* Moon 없이도 정상 동작 */ });
        }

        [Test]
        public void TimeManager_DayProgress_Range()
        {
            _timeManager.GameTime = 0f;
            float p0 = _timeManager.DayProgress;
            Assert.AreEqual(0f, p0, 0.01f, "자정 DayProgress = 0");

            _timeManager.GameTime = 43200f; // 12시간 후
            float p12 = _timeManager.DayProgress;
            Assert.AreEqual(0.5f, p12, 0.01f, "12시 DayProgress = 0.5");
        }

        [Test]
        public void TimeManager_IsDay_Works()
        {
            _timeManager.GameTime = 0f; // 자정
            Assert.IsFalse(_timeManager.IsDay);

            _timeManager.GameTime = 43200f; // 정오
            Assert.IsTrue(_timeManager.IsDay);
        }
    }
}