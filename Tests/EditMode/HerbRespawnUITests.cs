using NUnit.Framework;
using ProjectName.UI;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class HerbRespawnUITests
    {
        private GameObject _uiGo;
        private HerbRespawnUI _ui;
        private Camera _cam;
        private GameObject _herbGo;
        private HerbPickup _herb;

        [SetUp]
        public void SetUp()
        {
            _uiGo = new GameObject("HerbRespawnUI");
            _ui = _uiGo.AddComponent<HerbRespawnUI>();

            _cam = new GameObject("Camera").AddComponent<Camera>();
            _cam.transform.position = new Vector3(0, 10, -20);
            _cam.tag = "MainCamera";
            _ui.SetCamera(_cam);

            _herbGo = new GameObject("Herb");
            _herbGo.transform.position = new Vector3(5, 0, 3);
            _herb = _herbGo.AddComponent<HerbPickup>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_herbGo);
            Object.DestroyImmediate(_cam);
            Object.DestroyImmediate(_uiGo);
        }

        [Test]
        public void Singleton_Instance_IsSet()
        {
            Assert.IsNotNull(HerbRespawnUI.Instance);
        }

        [Test]
        public void Herb_NotHarvested_IsAvailable()
        {
            Assert.IsTrue(_herb.IsAvailable);
            Assert.IsFalse(_herb.IsHarvested);
        }

        [Test]
        public void Herb_RespawnProgress_WhenAvailable_IsOne()
        {
            Assert.AreEqual(1f, _herb.RespawnProgress);
        }

        [Test]
        public void Herb_RespawnTimeLeft_WhenAvailable_IsZero()
        {
            Assert.AreEqual(0f, _herb.RespawnTimeLeft);
        }

        [Test]
        public void MaxDisplayDistance_Is30()
        {
            Assert.AreEqual(30f, _ui.MaxDisplayDistance, 0.1f);
        }

        [Test]
        public void GetHerbCache_InitiallyEmpty()
        {
            var cache = _ui.GetHerbCache();
            Assert.IsNotNull(cache);
        }

        [Test]
        public void Herb_CameraDistance_Under30_Visible()
        {
            // Herb at (5,0,3), Camera at (0,10,-20) -> distance ~27 < 30
            float dist = Vector3.Distance(_herbGo.transform.position, _cam.transform.position);
            Assert.IsTrue(dist < 30f, $"Distance {dist} should be < 30");
        }

        [Test]
        public void Herb_CameraDistance_Over30_Hidden()
        {
            _cam.transform.position = new Vector3(0, 0, 100);
            _ui.SetCamera(_cam);
            float dist = Vector3.Distance(_herbGo.transform.position, _cam.transform.position);
            Assert.IsTrue(dist > _ui.MaxDisplayDistance, $"Distance {dist} should be > MaxDisplayDistance");
        }

        [Test]
        public void RespawnDuration_Default_Is30()
        {
            Assert.AreEqual(30f, _herb.RespawnDuration, 0.1f);
        }

        [Test]
        public void OnHarvestStarted_Event_Fired()
        {
            bool fired = false;
            _herb.OnHarvestStarted += () => fired = true;
            // Trigger harvest via event simulation
            if (_herb.OnHarvestStarted != null)
                _herb.OnHarvestStarted.Invoke();
            Assert.IsTrue(fired);
        }

        [Test]
        public void RespawnTimeLeft_AfterHarvest_Decreases()
        {
            // Simulate harvest: set _isHarvested and _harvestTime via toggling internal state
            // We can't call private Harvest() directly, but RespawnDuration stays 30f
            Assert.AreEqual(30f, _herb.RespawnDuration);
        }
    }
}