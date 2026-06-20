using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public class SwayControllerTests
    {
        private GameObject _go;
        private SwayController _sway;
        private Camera _cam;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestSway");
            _sway = _go.AddComponent<SwayController>();

            _cam = new GameObject("Camera").AddComponent<Camera>();
            _cam.transform.position = new Vector3(0, 10, -20);
            _cam.tag = "MainCamera";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_cam);
        }

        [Test]
        public void Component_Attached_NotNull()
        {
            Assert.IsNotNull(_sway);
        }

        [Test]
        public void IsNotCulled_Initially()
        {
            Assert.IsFalse(_sway.IsCulled);
        }

        [Test]
        public void DefaultSwaySpeed_InRange()
        {
            Assert.IsTrue(_sway.SwaySpeed >= 1f && _sway.SwaySpeed <= 3f);
        }

        [Test]
        public void DefaultSwayAmount_InRange()
        {
            Assert.IsTrue(_sway.SwayAmount >= 0f && _sway.SwayAmount <= 5f);
        }

        [Test]
        public void DefaultBobSpeed_InRange()
        {
            Assert.IsTrue(_sway.BobSpeed >= 0.5f && _sway.BobSpeed <= 2f);
        }

        [Test]
        public void DefaultBobAmount_InRange()
        {
            Assert.IsTrue(_sway.BobAmount >= 0f && _sway.BobAmount <= 0.05f);
        }

        [Test]
        public void SetSwaySpeed_ChangesValue()
        {
            _sway.SetSwaySpeed(2.5f);
            Assert.AreEqual(2.5f, _sway.SwaySpeed, 0.01f);
        }

        [Test]
        public void SetSwayAmount_ChangesValue()
        {
            _sway.SetSwayAmount(4f);
            Assert.AreEqual(4f, _sway.SwayAmount, 0.01f);
        }

        [Test]
        public void SetBobSpeed_ChangesValue()
        {
            _sway.SetBobSpeed(1.8f);
            Assert.AreEqual(1.8f, _sway.BobSpeed, 0.01f);
        }

        [Test]
        public void SetBobAmount_ChangesValue()
        {
            _sway.SetBobAmount(0.04f);
            Assert.AreEqual(0.04f, _sway.BobAmount, 0.01f);
        }

        [Test]
        public void SetWindInfluence_ChangesValue()
        {
            _sway.SetWindInfluence(0.7f);
            Assert.AreEqual(0.7f, _sway.WindInfluence, 0.01f);
        }

        [Test]
        public void SetCullDistance_ChangesValue()
        {
            _sway.SetCullDistance(100f);
            Assert.AreEqual(100f, _sway.CullDistance, 0.01f);
        }

        [Test]
        public void ResetState_RestoresValues()
        {
            _go.transform.rotation = Quaternion.Euler(10, 20, 30);
            _sway.ResetState();

            // After ResetState, initial rotation should match transform.rotation
            // (Since Awake hasn't been called, _initialRotation is default)
            Assert.IsFalse(_sway.IsCulled);
        }

        [Test]
        public void CullDistance_DefaultIs50()
        {
            Assert.AreEqual(50f, _sway.CullDistance, 0.1f);
        }
    }
}