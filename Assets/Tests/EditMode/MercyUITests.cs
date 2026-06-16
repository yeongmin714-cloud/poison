using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-11: MercyUI + LordSurrenderSystem EditMode 테스트.
    /// 
    /// 테스트 대상:
    /// - LordSurrenderSystem 상태 기계 (TrySummonLord, ExecuteLord, SpareLord)
    /// - LordData 기본값 및 구조체 필드
    /// - TerritoryState 업데이트 (ownership, lordSurrendered 등)
    /// - MercyUI.Show/Hide/IsVisible
    /// - 여러 영지 독립적 처리
    /// </summary>
    public class MercyUITests
    {
        private TerritoryId _testTerritory;

        [SetUp]
        public void Setup()
        {
            _testTerritory = new TerritoryId(NationType.East, 1);
            LordSurrenderSystem.ResetAll();

            // TerritoryDatabase가 초기화되어 있는지 확인
            // (static singleton이므로 이미 데이터 있음)
        }

        [TearDown]
        public void Teardown()
        {
            LordSurrenderSystem.ResetAll();
        }

        // ================================================================
        // LordData 기본값
        // ================================================================

        [Test]
        public void GetLordData_Default_ReturnsDefaultStruct()
        {
            var data = LordSurrenderSystem.GetLordData(_testTerritory);

            Assert.IsFalse(data.isAlive, "초기화되지 않은 영주는 isAlive=false");
            Assert.IsFalse(data.hasSurrendered, "초기화되지 않은 영주는 hasSurrendered=false");
            Assert.AreEqual(default(TerritoryId), data.territoryId,
                "초기화되지 않은 영주는 기본 TerritoryId");
        }

        [Test]
        public void GetLordData_UnknownTerritory_ReturnsDefault()
        {
            var unknown = new TerritoryId(NationType.Empire, 99);
            var data = LordSurrenderSystem.GetLordData(unknown);

            Assert.IsFalse(data.isAlive);
            Assert.AreEqual("", data.lordName);
        }

        // ================================================================
        // TrySummonLord
        // ================================================================

        [Test]
        public void TrySummonLord_ReturnsTrue_WhenGuardsDefeated()
        {
            bool result = LordSurrenderSystem.TrySummonLord(_testTerritory);

            Assert.IsTrue(result, "영지 정의가 있으면 영주 소환 성공");
        }

        [Test]
        public void TrySummonLord_SetsLordData()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);

            var data = LordSurrenderSystem.GetLordData(_testTerritory);

            Assert.IsTrue(data.isAlive, "소환된 영주는 생존");
            Assert.IsTrue(data.hasSurrendered, "소환된 영주는 항복 상태");
            Assert.AreEqual(_testTerritory, data.territoryId);
            Assert.IsFalse(string.IsNullOrEmpty(data.lordName), "영주 이름이 있어야 함");
            Assert.AreEqual(100f, data.health, 0.001f);
        }

        [Test]
        public void TrySummonLord_SetsTerritoryState_LordSurrendered()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);

            var state = TerritoryDatabase.Instance.GetState(_testTerritory);
            Assert.IsNotNull(state);
            Assert.IsTrue(state.lordSurrendered, "TerritoryState.lordSurrendered = true");
        }

        [Test]
        public void TrySummonLord_CalledTwice_ReturnsFalse()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);

            bool secondAttempt = LordSurrenderSystem.TrySummonLord(_testTerritory);
            Assert.IsFalse(secondAttempt, "두 번째 소환 시도는 실패해야 함");
        }

        [Test]
        public void TrySummonLord_AfterExecute_ReturnsFalse()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.ExecuteLord(_testTerritory);

            bool attempt = LordSurrenderSystem.TrySummonLord(_testTerritory);
            Assert.IsFalse(attempt, "처형 후 소환 시도는 실패");
        }

        [Test]
        public void TrySummonLord_FiresOnLordSummonedEvent()
        {
            bool eventFired = false;
            TerritoryId eventTerritory = default;

            LordSurrenderSystem.OnLordSummoned += (tId, data) =>
            {
                eventFired = true;
                eventTerritory = tId;
            };

            LordSurrenderSystem.TrySummonLord(_testTerritory);

            Assert.IsTrue(eventFired);
            Assert.AreEqual(_testTerritory, eventTerritory);

            LordSurrenderSystem.OnLordSummoned = null;
        }

        [Test]
        public void TrySummonLord_FiresOnLordSurrenderedEvent()
        {
            bool eventFired = false;

            LordSurrenderSystem.OnLordSurrendered += (tId, data) =>
            {
                eventFired = true;
                Assert.IsTrue(data.hasSurrendered);
                Assert.IsTrue(data.isAlive);
            };

            LordSurrenderSystem.TrySummonLord(_testTerritory);

            Assert.IsTrue(eventFired);

            LordSurrenderSystem.OnLordSurrendered = null;
        }

        // ================================================================
        // ExecuteLord
        // ================================================================

        [Test]
        public void ExecuteLord_KillsLord()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.ExecuteLord(_testTerritory);

            var data = LordSurrenderSystem.GetLordData(_testTerritory);
            Assert.IsFalse(data.isAlive, "처형 후 영주 사망");
            Assert.AreEqual(0f, data.health, 0.001f);
        }

        [Test]
        public void ExecuteLord_SetsTerritoryState_PlayerOwned()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.ExecuteLord(_testTerritory);

            var state = TerritoryDatabase.Instance.GetState(_testTerritory);
            Assert.AreEqual(TerritoryOwnership.PlayerOwned, state.ownership,
                "처형 후 영지는 PlayerOwned");
        }

        [Test]
        public void ExecuteLord_SetsTerritoryState_LordExecuted()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.ExecuteLord(_testTerritory);

            var state = TerritoryDatabase.Instance.GetState(_testTerritory);
            Assert.IsTrue(state.lordExecuted, "처형 후 lordExecuted = true");
        }

        [Test]
        public void ExecuteLord_WithoutSummon_DoesNothing()
        {
            // 소환 없이 처형 시도
            LordSurrenderSystem.ExecuteLord(_testTerritory);

            var state = TerritoryDatabase.Instance.GetState(_testTerritory);
            Assert.AreNotEqual(TerritoryOwnership.PlayerOwned, state.ownership,
                "소환 없이 처형하면 소유권 변경 없음");
        }

        [Test]
        public void ExecuteLord_FiresOnLordExecutedEvent()
        {
            bool eventFired = false;

            LordSurrenderSystem.OnLordExecuted += (tId, data) =>
            {
                eventFired = true;
                Assert.IsFalse(data.isAlive);
            };

            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.ExecuteLord(_testTerritory);

            Assert.IsTrue(eventFired);

            LordSurrenderSystem.OnLordExecuted = null;
        }

        // ================================================================
        // SpareLord
        // ================================================================

        [Test]
        public void SpareLord_LordStaysAlive()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.SpareLord(_testTerritory);

            var data = LordSurrenderSystem.GetLordData(_testTerritory);
            Assert.IsTrue(data.isAlive, "살려주기 후 영주 생존");
            Assert.IsTrue(data.hasSurrendered);
        }

        [Test]
        public void SpareLord_SetsTerritoryState_PlayerOwned()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.SpareLord(_testTerritory);

            var state = TerritoryDatabase.Instance.GetState(_testTerritory);
            Assert.AreEqual(TerritoryOwnership.PlayerOwned, state.ownership,
                "살려주기 후 영지는 PlayerOwned");
        }

        [Test]
        public void SpareLord_SetsTerritoryState_LordSpared()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.SpareLord(_testTerritory);

            var state = TerritoryDatabase.Instance.GetState(_testTerritory);
            Assert.IsTrue(state.lordSpared, "살려주기 후 lordSpared = true");
        }

        [Test]
        public void SpareLord_IncreasesLoyaltyBonus()
        {
            LordSurrenderSystem.TrySummonLord(_testTerritory);

            var stateBefore = TerritoryDatabase.Instance.GetState(_testTerritory);
            float loyaltyBefore = stateBefore.loyaltyToPlayer;

            LordSurrenderSystem.SpareLord(_testTerritory);

            var stateAfter = TerritoryDatabase.Instance.GetState(_testTerritory);
            Assert.Greater(stateAfter.loyaltyToPlayer, loyaltyBefore,
                "살려주기 후 충성도 증가");
            Assert.AreEqual(Mathf.Min(loyaltyBefore + 30f, 100f), stateAfter.loyaltyToPlayer, 0.001f);
        }

        [Test]
        public void SpareLord_WithoutSummon_DoesNothing()
        {
            LordSurrenderSystem.SpareLord(_testTerritory);

            var state = TerritoryDatabase.Instance.GetState(_testTerritory);
            Assert.AreNotEqual(TerritoryOwnership.PlayerOwned, state.ownership,
                "소환 없이 살려주기하면 소유권 변경 없음");
        }

        [Test]
        public void SpareLord_FiresOnLordSparedEvent()
        {
            bool eventFired = false;

            LordSurrenderSystem.OnLordSpared += (tId, data) =>
            {
                eventFired = true;
                Assert.IsTrue(data.isAlive);
            };

            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.SpareLord(_testTerritory);

            Assert.IsTrue(eventFired);

            LordSurrenderSystem.OnLordSpared = null;
        }

        // ================================================================
        // 여러 영지 독립적 처리
        // ================================================================

        [Test]
        public void MultipleTerritories_IndependentLords()
        {
            var territory2 = new TerritoryId(NationType.West, 1);
            var territory3 = new TerritoryId(NationType.South, 2);

            // 영주 소환
            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.TrySummonLord(territory2);

            var lord1 = LordSurrenderSystem.GetLordData(_testTerritory);
            var lord2 = LordSurrenderSystem.GetLordData(territory2);
            var lord3 = LordSurrenderSystem.GetLordData(territory3);

            Assert.IsTrue(lord1.isAlive, "영지 1 영주 생존");
            Assert.IsTrue(lord2.isAlive, "영지 2 영주 생존");
            Assert.IsFalse(lord3.isAlive, "영지 3 영주는 소환 안됨");
        }

        [Test]
        public void MultipleTerritories_ExecuteOne_DoesNotAffectOther()
        {
            var territory2 = new TerritoryId(NationType.West, 1);

            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.TrySummonLord(territory2);

            // 영지 1만 처형
            LordSurrenderSystem.ExecuteLord(_testTerritory);

            var lord1 = LordSurrenderSystem.GetLordData(_testTerritory);
            var lord2 = LordSurrenderSystem.GetLordData(territory2);

            Assert.IsFalse(lord1.isAlive, "영지 1 영주 사망");
            Assert.IsTrue(lord2.isAlive, "영지 2 영주 생존");

            var state1 = TerritoryDatabase.Instance.GetState(_testTerritory);
            var state2 = TerritoryDatabase.Instance.GetState(territory2);

            Assert.AreEqual(TerritoryOwnership.PlayerOwned, state1.ownership,
                "영지 1은 PlayerOwned");
            Assert.AreEqual(TerritoryOwnership.Unoccupied, state2.ownership,
                "영지 2는 Unoccupied 유지");
        }

        // ================================================================
        // GetSurrenderText (성격별)
        // ================================================================

        [Test]
        public void GetSurrenderText_ReturnsTextBasedOnPersonality()
        {
            var lordData = new LordSurrenderSystem.LordData
            {
                lordName = "테스트 영주",
                territoryId = _testTerritory,
                isAlive = true,
                hasSurrendered = true,
                personality = LordPersonality.Cowardly
            };

            string text = LordSurrenderSystem.GetSurrenderText(lordData);
            Assert.IsFalse(string.IsNullOrEmpty(text), "Cowardly 영주 항복 텍스트가 있어야 함");
            Assert.IsTrue(text.Contains("살려주"), "Cowardly 영주는 목숨을 구걸하는 텍스트");

            // Brave
            lordData.personality = LordPersonality.Brave;
            text = LordSurrenderSystem.GetSurrenderText(lordData);
            Assert.IsTrue(text.Contains("인정"), "Brave 영주는 실력을 인정하는 텍스트");

            // Greedy
            lordData.personality = LordPersonality.Greedy;
            text = LordSurrenderSystem.GetSurrenderText(lordData);
            Assert.IsTrue(text.Contains("목숨"), "Greedy 영주는 목숨을 구걸");
        }

        // ================================================================
        // MercyUI.Show / Hide
        // ================================================================

        [Test]
        public void MercyUI_Show_SetsIsVisible()
        {
            Assert.IsFalse(MercyUI.IsVisible, "초기에는 IsVisible = false");

            MercyUI.Show(_testTerritory, "테스트 영주");
            Assert.IsTrue(MercyUI.IsVisible, "Show 후 IsVisible = true");
            Assert.AreEqual(_testTerritory.ToString(), MercyUI.CurrentTerritoryId.ToString());

            MercyUI.Hide();
        }

        [Test]
        public void MercyUI_Hide_ClearsVisible()
        {
            MercyUI.Show(_testTerritory, "테스트 영주");
            Assert.IsTrue(MercyUI.IsVisible);

            MercyUI.Hide();
            Assert.IsFalse(MercyUI.IsVisible, "Hide 후 IsVisible = false");
            Assert.IsFalse(MercyUI.IsRewardVisible, "Hide 후 IsRewardVisible = false");
        }

        [Test]
        public void MercyUI_Show_AfterHide_WorksAgain()
        {
            MercyUI.Show(_testTerritory, "첫 번째");
            MercyUI.Hide();

            var territory2 = new TerritoryId(NationType.West, 1);
            MercyUI.Show(territory2, "두 번째 영주");

            Assert.IsTrue(MercyUI.IsVisible);
            Assert.AreEqual(territory2.ToString(), MercyUI.CurrentTerritoryId.ToString());

            MercyUI.Hide();
        }

        [Test]
        public void MercyUI_Show_DoesNotRequireLordSummon()
        {
            // LordSurrenderSystem 없이도 MercyUI.Show는 동작 (독립적 UI)
            MercyUI.Show(_testTerritory, "독립 영주");
            Assert.IsTrue(MercyUI.IsVisible);

            MercyUI.Hide();
        }

        // ================================================================
        // ResetAll (LordSurrenderSystem)
        // ================================================================

        [Test]
        public void ResetAll_ClearsAllLordData()
        {
            var territory2 = new TerritoryId(NationType.West, 2);

            LordSurrenderSystem.TrySummonLord(_testTerritory);
            LordSurrenderSystem.TrySummonLord(territory2);

            Assert.IsTrue(LordSurrenderSystem.GetLordData(_testTerritory).isAlive);
            Assert.IsTrue(LordSurrenderSystem.GetLordData(territory2).isAlive);

            LordSurrenderSystem.ResetAll();

            Assert.IsFalse(LordSurrenderSystem.GetLordData(_testTerritory).isAlive,
                "ResetAll 후 영주 데이터 초기화");
            Assert.IsFalse(LordSurrenderSystem.GetLordData(territory2).isAlive);
        }

        // ================================================================
        // GetPersonalityName
        // ================================================================

        [Test]
        public void GetPersonalityName_ReturnsKoreanNames()
        {
            Assert.AreEqual("보통", LordSurrenderSystem.GetPersonalityName(LordPersonality.Neutral));
            Assert.AreEqual("탐욕스러움", LordSurrenderSystem.GetPersonalityName(LordPersonality.Greedy));
            Assert.AreEqual("의심 많음", LordSurrenderSystem.GetPersonalityName(LordPersonality.Suspicious));
            Assert.AreEqual("용감함", LordSurrenderSystem.GetPersonalityName(LordPersonality.Brave));
            Assert.AreEqual("겁많음", LordSurrenderSystem.GetPersonalityName(LordPersonality.Cowardly));
            Assert.AreEqual("현명함", LordSurrenderSystem.GetPersonalityName(LordPersonality.Wise));
            Assert.AreEqual("잔인함", LordSurrenderSystem.GetPersonalityName(LordPersonality.Cruel));
        }
    }
}