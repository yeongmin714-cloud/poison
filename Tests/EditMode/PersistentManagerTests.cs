using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectName.Tests.EditMode
{
    public class PersistentManagerTests
    {
        [Test]
        public void PersistentManager_Instance_IsNotNull()
        {
            var instance = PersistentManager.Instance;
            Assert.IsNotNull(instance);
        }

        [Test]
        public void PersistentManager_SurvivesSceneLoad()
        {
            var instance = PersistentManager.Instance;
            Assert.IsTrue(instance.gameObject.scene.name == "DontDestroyOnLoad");
        }
    }
}