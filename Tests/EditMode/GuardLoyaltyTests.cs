using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C9-10 병사 호감도 시스템 테스트
    /// </summary>
    public class GuardLoyaltyTests
    {
        // ===================== 상수 확인 =====================

        [Test]
        public void LoyaltySystem_Constants_AreDefined()
        {
            Assert.AreEqual(10f, GuardLoyaltySystem.SAME_NATION_BONUS, "동일 국가 보너스 10");
            Assert.AreEqual(-20f, GuardLoyaltySystem.HOSTILE_NATION_PENALTY, "적대 국가 패널티 -20");
            Assert.AreEqual(-5f, GuardLoyaltySystem.HOSTILE_MULTI_PENALTY, "추가 적대 패널티 -5");
        }

        // ===================== GiveGift 테스트 =====================

        [Test]
        public void GiveGift_IncreasesLoyalty()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 50f;

            GuardLoyaltySystem.GiveGift(guard, 50);
            Assert.Greater(guard.Loyalty, 50f, "선물 후 호감도가 증가해야 합니다");
            Assert.LessOrEqual(guard.Loyalty, 100f, "호감도는 100을 초과할 수 없습니다");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GiveGift_HighValue_GivesMoreLoyalty()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 50f;

            GuardLoyaltySystem.GiveGift(guard, 10);
            float lowGiftLoyalty = guard.Loyalty;

            guard.Loyalty = 50f;
            GuardLoyaltySystem.GiveGift(guard, 200);
            float highGiftLoyalty = guard.Loyalty;

            Assert.Greater(highGiftLoyalty, lowGiftLoyalty, "고가 선물이 더 많은 호감도를 줘야 합니다");

            Object.DestroyImmediate(go);
        }

        // ===================== GiveDrug 테스트 =====================

        [Test]
        public void GiveDrug_IncreasesLoyaltyAndAddiction()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 50f;
            guard.Addiction = 0f;

            GuardLoyaltySystem.GiveDrug(guard, 1);

            Assert.AreEqual(65f, guard.Loyalty, "약물 제공 후 호감도 +15");
            Assert.AreEqual(5f, guard.Addiction, "약물 제공 후 중독도 +5");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void GiveDrug_HighPotency()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();

            GuardLoyaltySystem.GiveDrug(guard, 3);
            Assert.AreEqual(15f, guard.Addiction, "고효능 약물: 중독도 +15");

            Object.DestroyImmediate(go);
        }

        // ===================== Threaten 테스트 =====================

        [Test]
        public void Threaten_IncreasesLoyaltyTemporarily()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 50f;

            GuardLoyaltySystem.Threaten(guard);
            Assert.AreEqual(70f, guard.Loyalty, "위협 후 호감도 +20");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyThreatBacklash_DecreasesLoyalty()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.Loyalty = 70f;

            GuardLoyaltySystem.ApplyThreatBacklash(guard);
            Assert.AreEqual(40f, guard.Loyalty, "위협 보복 후 호감도 -30");

            Object.DestroyImmediate(go);
        }

        // ===================== GetLoyaltyTag 테스트 =====================

        [Test]
        public void GetLoyaltyTag_ReturnsCorrectTags()
        {
            Assert.AreEqual("충성", GuardLoyaltySystem.GetLoyaltyTag(95));
            Assert.AreEqual("우호적", GuardLoyaltySystem.GetLoyaltyTag(75));
            Assert.AreEqual("보통", GuardLoyaltySystem.GetLoyaltyTag(50));
            Assert.AreEqual("냉담", GuardLoyaltySystem.GetLoyaltyTag(25));
            Assert.AreEqual("적대", GuardLoyaltySystem.GetLoyaltyTag(10));
        }

        // ===================== UpdateLoyaltyByTerritory 테스트 =====================

        [Test]
        public void UpdateLoyaltyByTerritory_SameNationBonus()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo("테스트 병사", 1, NationType.East);

            // East_01을 플레이어 소유로 설정
            TerritoryDatabase.Instance.SetOwnership(NationType.East, 1, TerritoryOwnership.PlayerOwned);

            GuardLoyaltySystem.UpdateLoyaltyByTerritory(guard);

            // 기본 50 + 동일 국가 보너스 10 = 60
            Assert.GreaterOrEqual(guard.Loyalty, 60f, "동일 국가 영지 소유 시 호감도 상승");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UpdateLoyaltyByTerritory_HostilePenalty()
        {
            var go = new GameObject("TestGuard");
            var guard = go.AddComponent<GuardPlaceholder>();
            guard.SetGuardInfo("테스트 병사", 1, NationType.East);

            // 동(East)의 적대국은 서(West). 서 영지를 플레이어 소유로
            TerritoryDatabase.Instance.SetOwnership(NationType.West, 1, TerritoryOwnership.PlayerOwned);

            GuardLoyaltySystem.UpdateLoyaltyByTerritory(guard);

            // 기본 50 + 적대 패널티 -20 = 30
            Assert.LessOrEqual(guard.Loyalty, 30f, "적대 국가 영지 소유 시 호감도 하락");

            Object.DestroyImmediate(go);
        }

        // ===================== UpdateAllGuards 테스트 =====================

        [Test]
        public void UpdateAllGuards_WorksWithMultipleGuards()
        {
            var go1 = new GameObject("Guard1");
            var g1 = go1.AddComponent<GuardPlaceholder>();
            g1.SetGuardInfo("동쪽 병사", 1, NationType.East);

            var go2 = new GameObject("Guard2");
            var g2 = go2.AddComponent<GuardPlaceholder>();
            g2.SetGuardInfo("서쪽 병사", 1, NationType.West);

            // East_01을 플레이어 소유로
            TerritoryDatabase.Instance.SetOwnership(NationType.East, 1, TerritoryOwnership.PlayerOwned);

            GuardLoyaltySystem.UpdateAllGuards();

            // 동쪽 병사는 동일 국가 보너스 +10
            Assert.GreaterOrEqual(g1.Loyalty, 60f, "동쪽 병사 호감도 상승");

            // 서쪽 병사는 East(적대국) 영지 점령 → -20
            Assert.LessOrEqual(g2.Loyalty, 30f, "서쪽 병사 호감도 하락");

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
        }
    }
}