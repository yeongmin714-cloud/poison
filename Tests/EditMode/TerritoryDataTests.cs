using NUnit.Framework;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-01 영지 데이터 구조 테스트
    /// </summary>
    public class TerritoryDataTests
    {
        // ===================== 열거형 테스트 =====================

        [Test]
        public void NationType_IsDefined()
        {
            Assert.IsNotNull(typeof(NationType), "NationType 열거형이 정의되어야 합니다");
        }

        [Test]
        public void NationType_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)NationType.None, "NationType.None = 0");
            Assert.AreEqual(1, (int)NationType.East);
            Assert.AreEqual(2, (int)NationType.West);
            Assert.AreEqual(3, (int)NationType.South);
            Assert.AreEqual(4, (int)NationType.North);
            Assert.AreEqual(5, (int)NationType.Empire);
        }

        [Test]
        public void TerritoryDifficulty_IsDefined()
        {
            Assert.IsNotNull(typeof(TerritoryDifficulty), "TerritoryDifficulty 열거형이 정의되어야 합니다");
        }

        [Test]
        public void TerritoryOwnership_IsDefined()
        {
            Assert.IsNotNull(typeof(TerritoryOwnership), "TerritoryOwnership 열거형이 정의되어야 합니다");
            Assert.AreEqual(0, (int)TerritoryOwnership.Unoccupied);
            Assert.AreEqual(1, (int)TerritoryOwnership.PlayerOwned);
            Assert.AreEqual(2, (int)TerritoryOwnership.LordOwned);
            Assert.AreEqual(3, (int)TerritoryOwnership.Contested);
        }

        [Test]
        public void LordPersonality_IsDefined()
        {
            Assert.IsNotNull(typeof(LordPersonality), "LordPersonality 열거형이 정의되어야 합니다");
            Assert.GreaterOrEqual(7, (int)LordPersonality.Cruel + 1, "7가지 성격이 정의되어야 합니다");
        }

        // ===================== struct 테스트 =====================

        [Test]
        public void TerritoryId_ToString_Format()
        {
            var id = new TerritoryId(NationType.East, 1);
            string str = id.ToString();
            Assert.AreEqual("East_01", str, "TerritoryId.ToString() 형식 확인");
        }

        [Test]
        public void TerritoryId_HashCode()
        {
            var id1 = new TerritoryId(NationType.East, 1);
            var id2 = new TerritoryId(NationType.East, 1);
            Assert.AreEqual(id1.GetHashCode(), id2.GetHashCode(), "동일한 TerritoryId는 같은 HashCode를 가져야 합니다");
        }

        [Test]
        public void TerritoryDefinition_HasAllFields()
        {
            var fieldNames = typeof(TerritoryDefinition).GetFields();
            var nameSet = new System.Collections.Generic.HashSet<string>();
            foreach (var f in fieldNames)
                nameSet.Add(f.Name);

            Assert.IsTrue(nameSet.Contains("id"), "TerritoryDefinition에 id 필드 필요");
            Assert.IsTrue(nameSet.Contains("territoryName"), "territoryName 필드 필요");
            Assert.IsTrue(nameSet.Contains("nation"), "nation 필드 필요");
            Assert.IsTrue(nameSet.Contains("difficulty"), "difficulty 필드 필요");
            Assert.IsTrue(nameSet.Contains("guardCount"), "guardCount 필드 필요");
            Assert.IsTrue(nameSet.Contains("lord"), "lord 필드 필요");
            Assert.IsTrue(nameSet.Contains("description"), "description 필드 필요");
        }

        [Test]
        public void LordInfo_HasAllFields()
        {
            var fieldNames = typeof(LordInfo).GetFields();
            var nameSet = new System.Collections.Generic.HashSet<string>();
            foreach (var f in fieldNames)
                nameSet.Add(f.Name);

            Assert.IsTrue(nameSet.Contains("lordName"), "LordInfo에 lordName 필드 필요");
            Assert.IsTrue(nameSet.Contains("preferredFood"), "preferredFood 필드 필요");
            Assert.IsTrue(nameSet.Contains("chronicDisease"), "chronicDisease 필드 필요");
            Assert.IsTrue(nameSet.Contains("loyalty"), "loyalty 필드 필요");
            Assert.IsTrue(nameSet.Contains("personality"), "personality 필드 필요");
        }

        // ===================== TerritoryDatabase 테스트 =====================

        [Test]
        public void TerritoryDatabase_Singleton_Works()
        {
            var db = TerritoryDatabase.Instance;
            Assert.IsNotNull(db, "TerritoryDatabase.Instance가 null이 아니어야 합니다");

            var db2 = TerritoryDatabase.Instance;
            Assert.AreSame(db, db2, "TerritoryDatabase는 싱글톤이어야 합니다");
        }

        [Test]
        public void TerritoryDatabase_HasEastRing1()
        {
            var db = TerritoryDatabase.Instance;
            var def = db.GetDefinition(NationType.East, 1);

            Assert.AreEqual("동쪽 초원지대 1번지", def.territoryName,
                "East_01 영지 이름이 '동쪽 초원지대 1번지'여야 합니다");
            Assert.AreEqual(NationType.East, def.nation);
            Assert.AreEqual(TerritoryDifficulty.Ring1, def.difficulty);
        }

        [Test]
        public void TerritoryDatabase_East1_LordInfo()
        {
            var def = TerritoryDatabase.Instance.GetDefinition(NationType.East, 1);

            Assert.AreEqual("리카드 경", def.lord.lordName, "영주 이름 확인");
            Assert.AreEqual("구운 고기", def.lord.preferredFood, "선호 음식 확인");
            Assert.AreEqual("통풍", def.lord.chronicDisease, "지병 확인");
            Assert.AreEqual(70, def.lord.loyalty, "충성심 확인");
            Assert.AreEqual(LordPersonality.Neutral, def.lord.personality, "성격 확인");
        }

        [Test]
        public void TerritoryDatabase_UnknownDefinition_ReturnsDefault()
        {
            var def = TerritoryDatabase.Instance.GetDefinition(NationType.Empire, 99);
            Assert.IsNull(def.territoryName, "존재하지 않는 영지는 기본값을 반환해야 합니다");
        }

        [Test]
        public void TerritoryDatabase_GetAllDefinitions()
        {
            int count = 0;
            foreach (var def in TerritoryDatabase.Instance.GetAllDefinitions())
            {
                Assert.IsNotNull(def.territoryName, "모든 정의는 이름이 있어야 합니다");
                count++;
            }
            Assert.GreaterOrEqual(count, 1, "최소 1개 이상의 영지 정의가 있어야 합니다");
        }

        [Test]
        public void TerritoryDatabase_GetDefinitionsByNation()
        {
            int eastCount = 0;
            foreach (var def in TerritoryDatabase.Instance.GetDefinitionsByNation(NationType.East))
            {
                Assert.AreEqual(NationType.East, def.nation);
                eastCount++;
            }
            Assert.GreaterOrEqual(eastCount, 1, "동(East) 국가에 최소 1개 영지가 있어야 합니다");
        }

        // ===================== TerritoryState 테스트 =====================

        [Test]
        public void TerritoryState_InitialState()
        {
            var state = new TerritoryState(new TerritoryId(NationType.East, 1));
            Assert.AreEqual(TerritoryOwnership.Unoccupied, state.ownership,
                "초기 소유 상태는 Unoccupied여야 합니다");
            Assert.AreEqual(1f, state.guardAliveRatio, "초기 병사 생존율은 1.0이어야 합니다");
            Assert.IsFalse(state.isUnderAttack, "초기 전쟁 상태는 false여야 합니다");
        }

        [Test]
        public void TerritoryDatabase_State_CanBeModified()
        {
            var db = TerritoryDatabase.Instance;
            var state = db.GetState(NationType.East, 1);
            Assert.IsNotNull(state, "East_01 상태가 존재해야 합니다");

            // 소유권 변경
            state.ownership = TerritoryOwnership.PlayerOwned;
            Assert.AreEqual(TerritoryOwnership.PlayerOwned, state.ownership,
                "소유권이 PlayerOwned로 변경되어야 합니다");

            // 전쟁 상태 변경
            state.isUnderAttack = true;
            Assert.IsTrue(state.isUnderAttack, "전쟁 상태가 true여야 합니다");
        }

        [Test]
        public void TerritoryDatabase_SetOwnership()
        {
            TerritoryDatabase.Instance.SetOwnership(NationType.East, 1, TerritoryOwnership.PlayerOwned);
            var state = TerritoryDatabase.Instance.GetState(NationType.East, 1);
            Assert.AreEqual(TerritoryOwnership.PlayerOwned, state.ownership);
        }
    }
}