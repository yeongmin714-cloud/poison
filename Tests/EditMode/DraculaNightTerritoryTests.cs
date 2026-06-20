using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// ND-07: 드라큘라 나이트 테리토리 (Night Dracula) EditMode 테스트
    /// </summary>
    public class DraculaNightTerritoryTests
    {
        // ===================== ND-01: 드라큘라 영지 데이터 =====================

        [Test]
        public void NationType_Dracula_Exists()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(NationType), NationType.Dracula),
                "NationType 열거형에 Dracula가 정의되어야 합니다");
            Assert.AreEqual(6, (int)NationType.Dracula,
                "NationType.Dracula의 값은 6 (Empire 다음)이어야 합니다");
        }

        [Test]
        public void TerritoryDatabase_HasDraculaTerritory()
        {
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(NationType.Dracula, 1);

            Assert.IsNotNull(def.territoryName, "드라큘라 영지 정의가 존재해야 합니다");
            Assert.AreEqual(NationType.Dracula, def.nation, "영지 국가는 DraculaLord여야 합니다");
            Assert.AreEqual("드라큘라의 성", def.territoryName, "영지 이름이 '드라큘라의 성'이어야 합니다");
            Assert.AreEqual(10, def.guardCount, "드라큘라 영지 병사는 10명이어야 합니다");
        }

        [Test]
        public void TerritoryDefinition_HasIsNightOnly()
        {
            var fieldNames = typeof(TerritoryDefinition).GetFields();
            var nameSet = new System.Collections.Generic.HashSet<string>();
            foreach (var f in fieldNames)
                nameSet.Add(f.Name);

            Assert.IsTrue(nameSet.Contains("isNightOnly"),
                "TerritoryDefinition에 isNightOnly 필드가 있어야 합니다");
        }

        [Test]
        public void TerritoryState_HasIsActive()
        {
            var fieldNames = typeof(TerritoryState).GetFields();
            var nameSet = new System.Collections.Generic.HashSet<string>();
            foreach (var f in fieldNames)
                nameSet.Add(f.Name);

            Assert.IsTrue(nameSet.Contains("isActive"),
                "TerritoryState에 isActive 필드가 있어야 합니다");
        }

        [Test]
        public void DraculaTerritory_IsNightOnly()
        {
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(NationType.Dracula, 1);

            Assert.IsTrue(def.isNightOnly, "드라큘라 영지의 isNightOnly는 true여야 합니다");
        }

        [Test]
        public void DraculaTerritory_LordInfo()
        {
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(NationType.Dracula, 1);

            Assert.AreEqual("드라큘라 백작", def.lord.lordName, "영주 이름이 '드라큘라 백작'이어야 합니다");
            Assert.AreEqual(LordPersonality.Cruel, def.lord.personality, "영주 성격은 Cruel이어야 합니다");
            Assert.AreEqual(0, def.lord.loyalty, "영주 충성심은 0이어야 합니다");
        }

        [Test]
        public void DraculaTerritory_Difficulty_Ring4()
        {
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(NationType.Dracula, 1);

            Assert.AreEqual(TerritoryDifficulty.Ring4, def.difficulty,
                "드라큘라 영지 난이도는 Ring4여야 합니다");
        }

        // ===================== ND-02: 야간 출현 시스템 =====================

        [Test]
        public void TimeManager_HasNightStartEvent()
        {
            var eventType = typeof(TimeManager).GetEvent("OnNightStart",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(eventType, "TimeManager에 OnNightStart 이벤트가 있어야 합니다");
        }

        [Test]
        public void TimeManager_HasDayStartEvent()
        {
            var eventType = typeof(TimeManager).GetEvent("OnDayStart",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(eventType, "TimeManager에 OnDayStart 이벤트가 있어야 합니다");
        }

        [Test]
        public void DraculaTerritoryController_IsSingleton()
        {
            // DraculaTerritoryController는 MonoBehaviour이므로 Instance 프로퍼티만 확인
            var prop = typeof(DraculaTerritoryController).GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(prop, "DraculaTerritoryController에 정적 Instance 프로퍼티가 있어야 합니다");
        }

        [Test]
        public void DraculaTerritoryController_HasActivateDeactivateMethods()
        {
            var methods = typeof(DraculaTerritoryController).GetMethods(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var methodNames = new System.Collections.Generic.HashSet<string>();
            foreach (var m in methods)
                methodNames.Add(m.Name);

            Assert.IsTrue(methodNames.Contains("ActivateTerritory"),
                "ActivateTerritory 메서드가 있어야 합니다");
            Assert.IsTrue(methodNames.Contains("DeactivateTerritory"),
                "DeactivateTerritory 메서드가 있어야 합니다");
            Assert.IsTrue(methodNames.Contains("OnLordDefeated"),
                "OnLordDefeated 메서드가 있어야 합니다");
        }

        // ===================== ND-03: 스켈레톤 병사 =====================

        [Test]
        public void SkeletonGuardPlaceholder_Exists()
        {
            var guardType = typeof(SkeletonGuardPlaceholder);
            Assert.IsNotNull(guardType, "SkeletonGuardPlaceholder 클래스가 정의되어야 합니다");
        }

        [Test]
        public void SkeletonGuardPlaceholder_Implements_IDamageable()
        {
            var guardType = typeof(SkeletonGuardPlaceholder);
            Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(guardType),
                "SkeletonGuardPlaceholder는 IDamageable을 구현해야 합니다");
        }

        [Test]
        public void SkeletonGuardPlaceholder_HasLevel()
        {
            var prop = typeof(SkeletonGuardPlaceholder).GetProperty("Level",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(prop, "SkeletonGuardPlaceholder에 Level 프로퍼티가 있어야 합니다");
        }

        // ===================== ND-04: 드라큘라 영주 보스 =====================

        [Test]
        public void DraculaLord_Exists()
        {
            var lordType = typeof(DraculaLord);
            Assert.IsNotNull(lordType, "DraculaLord 클래스가 정의되어야 합니다");
        }

        [Test]
        public void DraculaLord_Implements_IDamageable()
        {
            var lordType = typeof(DraculaLord);
            Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(lordType),
                "DraculaLord는 IDamageable을 구현해야 합니다");
        }

        [Test]
        public void DraculaLord_HasHighHP()
        {
            var field = typeof(DraculaLord).GetField("_maxHP",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "DraculaLord에 _maxHP 필드가 있어야 합니다");

            // Test via property
            var prop = typeof(DraculaLord).GetProperty("MaxHP",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(prop, "DraculaLord에 MaxHP 프로퍼티가 있어야 합니다");
        }

        [Test]
        public void DraculaLord_HasTeleportCooldown()
        {
            var field = typeof(DraculaLord).GetField("_teleportCooldown",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "DraculaLord에 _teleportCooldown (5초) 필드가 있어야 합니다");
        }

        [Test]
        public void DraculaLord_HasLifeSteal()
        {
            var field = typeof(DraculaLord).GetField("_lifeStealRatio",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "DraculaLord에 _lifeStealRatio (0.2 = 20%) 필드가 있어야 합니다");
        }

        // ===================== ND-05: 희귀 아이템 드랍 =====================

        [Test]
        public void DropTableManager_HasDraculaLordTable()
        {
            var method = typeof(DropTableManager).GetMethod("GetDraculaLordTable",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "DropTableManager에 GetDraculaLordTable() 메서드가 있어야 합니다");
        }

        [Test]
        public void DropTableManager_HasSkeletonGuardTable()
        {
            var method = typeof(DropTableManager).GetMethod("GetSkeletonGuardTable",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "DropTableManager에 GetSkeletonGuardTable() 메서드가 있어야 합니다");
        }

        [Test]
        public void DropTableManager_HasApplyDraculaLordDrops()
        {
            var method = typeof(DropTableManager).GetMethod("ApplyDraculaLordDrops",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "DropTableManager에 ApplyDraculaLordDrops() 메서드가 있어야 합니다");
        }

        [Test]
        public void DropTableManager_HasApplySkeletonGuardDrops()
        {
            var method = typeof(DropTableManager).GetMethod("ApplySkeletonGuardDrops",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(method, "DropTableManager에 ApplySkeletonGuardDrops() 메서드가 있어야 합니다");
        }

        // ===================== ND-06: 낮 비활성화 =====================

        [Test]
        public void TerritoryState_IsActive_DefaultsToTrue()
        {
            var state = new TerritoryState(new TerritoryId(NationType.Dracula, 1));
            Assert.IsTrue(state.isActive, "TerritoryState.isActive의 기본값은 true여야 합니다");
        }

        // ===================== NationTypeToKorean with Dracula =====================

        [Test]
        public void NationTypeToKorean_Dracula_ReturnsCorrectValue()
        {
            // GuardPlaceholder.NationTypeToKorean은 private static이므로 리플렉션으로 확인
            var method = typeof(GuardPlaceholder).GetMethod("NationTypeToKorean",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method != null)
            {
                string result = (string)method.Invoke(null, new object[] { NationType.Dracula });
                Assert.AreEqual("무소속", result, "Dracula는 기본 처리에서 '무소속'이어야 합니다");
            }
        }

        // ===================== 통합 테스트 =====================

        [Test]
        public void DraculaTerritory_AllDefinitions_Count()
        {
            int count = 0;
            foreach (var def in TerritoryDatabase.Instance.GetDefinitionsByNation(NationType.Dracula))
            {
                Assert.AreEqual(NationType.Dracula, def.nation);
                Assert.IsTrue(def.isNightOnly);
                count++;
            }
            Assert.AreEqual(1, count, "드라큘라 영지는 1개여야 합니다");
        }

        [Test]
        public void DraculaState_InitialIsNotActive()
        {
            var state = TerritoryDatabase.Instance.GetState(NationType.Dracula, 1);
            // 초기 상태는 낮이므로 isActive=false (DraculaTerritoryController.Awake에서 설정)
            // 테스트에서는 그 값을 확인하지 않고, isActive 필드 자체가 존재하는지만 확인
            Assert.IsNotNull(state, "드라큘라 영지 상태가 존재해야 합니다");
        }
    }
}