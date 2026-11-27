using System.Reflection;
using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    public sealed class InvadedLordEncounterTests
    {
        private GameObject _root;
        private InvadedLordEncounter _encounter;
        private bool _indoorBefore;

        [SetUp]
        public void SetUp()
        {
            _indoorBefore = ProjectName.Core.UITransitionState.IndoorActive;
            ProjectName.Core.UITransitionState.IndoorActive = false;
            _root = new GameObject("InvadedLordEncounterTest");
            _encounter = _root.AddComponent<InvadedLordEncounter>();
        }

        [TearDown]
        public void TearDown()
        {
            LordSurrenderSystem.ResetAll();
            ProjectName.Core.UITransitionState.IndoorActive = _indoorBefore;
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void Initialize_StartsNeutralAndRegistersAnUnresolvedLivingLord()
        {
            _encounter.Initialize("West_03", MakeDefinition());

            Assert.That(_encounter.State, Is.EqualTo(InvadedLordEncounter.EncounterState.NotStarted));
            Assert.That(_encounter.LordName, Is.EqualTo("영주_03"));
            Assert.That(_encounter.HasTerritoryId, Is.True);
            Assert.That(_encounter.IsAlive, Is.True);
            var lord = LordSurrenderSystem.GetLordData(_encounter.TerritoryId);
            Assert.That(lord.lordName, Is.EqualTo("영주_03"));
            Assert.That(lord.hasSurrendered, Is.False);
            Assert.That(lord.isAlive, Is.True);
        }

        [Test]
        public void DamageIsIgnoredUntilAttackIsChosen_ThenDefeatDoesNotResolveTerritory()
        {
            _encounter.Initialize("West_03", MakeDefinition());
            float before = _encounter.CurrentHP;
            _encounter.TakeDamage(10f, Vector3.forward, "sword");
            Assert.That(_encounter.CurrentHP, Is.EqualTo(before));

            Assert.That(_encounter.BeginCombat(), Is.True);
            SetField("_hp", 5f);
            _encounter.TakeDamage(10f, Vector3.forward, "sword");

            Assert.That(_encounter.State, Is.EqualTo(InvadedLordEncounter.EncounterState.Defeated));
            Assert.That(_encounter.IsAlive, Is.False);
            Assert.That(_encounter.IsResolved, Is.False, "전투 승리 자체는 영지 점령·몰수 처리를 실행하지 않는다.");
            Assert.That(TerritoryDatabase.Instance.GetState(_encounter.TerritoryId).ownership,
                Is.Not.EqualTo(TerritoryOwnership.PlayerOwned));
        }

        [Test]
        public void NorthernAnchor_IsInsideBedroomAndIsNotTheLobbyOrigin()
        {
            Vector3 anchor = InvadedLordEncounter.GetNorthernBedroomAnchor();
            Assert.That(InvadedLordEncounter.IsNorthernBedroomPosition(anchor), Is.True);
            Assert.That(anchor.z, Is.GreaterThan(14f));
        }

        private static TerritoryDefinition MakeDefinition()
        {
            return new TerritoryDefinition
            {
                id = new TerritoryId(NationType.West, 3),
                territoryName = "LordRoom_01",
                nation = NationType.West,
                difficulty = TerritoryDifficulty.Ring1,
                guardCount = 3,
                lord = new LordInfo { lordName = "영주_03", personality = LordPersonality.Neutral },
                worldPosition = Vector3.zero
            };
        }

        private void SetField(string name, object value)
        {
            var field = typeof(InvadedLordEncounter).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(_encounter, value);
        }
    }
}
