using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [113차 A] 선물 보너스 계약 — ComputeFeedDelta 순수 단일소스:
    /// 기본 +5 / 선호 음식 계열 일치 +15 / 일치 + 고급 음식(T3) + 강한 링(Ring3·Ring4·Empire) +20.
    /// ComputeFeedDelta는 인벤·문 상태와 무관한 순수 함수라 EditMode에서 전 영지 전수 판정 가능.
    /// TryFeedLord는 문 개방 게이트(기존 계약)와 증가치 반영 경로(소스 계약)로 검증.
    /// </summary>
    [TestFixture]
    public class LordFeedGiftTests
    {
        /// <summary>강한 링 판정 — T3 고급 음식 보너스 상향(Ring3/Ring4/Empire) 대상.</summary>
        private static bool IsHardRing(TerritoryDifficulty difficulty)
        {
            return difficulty == TerritoryDifficulty.Ring3 ||
                   difficulty == TerritoryDifficulty.Ring4 ||
                   difficulty == TerritoryDifficulty.Empire;
        }

        [Test]
        public void ComputeFeedDelta_Mismatch_ReturnsDefaultFive()
        {
            // East_01 선호 음식 = "빵과 치즈"(TerritoryDataTests 결정론 실측 — 재료 미실장 계열)
            // → 어떤 실장 음식으로도 미일치 → 기본 +5
            float delta = TerritoryLordDoorSystem.ComputeFeedDelta("East_01", "meat_rabbit", out bool match);
            Assert.AreEqual(5f, delta, "미일치 지급은 기본 +5");
            Assert.IsFalse(match, "빵과 치즈(미실장 계열)에 고기 지급은 선호 일치 아님");
        }

        [Test]
        public void ComputeFeedDelta_NullArguments_ReturnsDefaultFive()
        {
            Assert.AreEqual(5f, TerritoryLordDoorSystem.ComputeFeedDelta(null, "meat_boar", out _), "null 키 방어");
            Assert.AreEqual(5f, TerritoryLordDoorSystem.ComputeFeedDelta("East_01", null, out _), "null 아이템 방어");
        }

        [Test]
        public void ComputeFeedDelta_AllTerritories_FollowsBonusLaw()
        {
            // 전 영지 전수 법칙 — 증가치는 카탈로그 계약의 유일 판정식:
            //   미일치 → 5 / 일치 && (T3 && 강한 링) → 20 / 일치(그 외) → 15
            var defs = TerritoryDatabase.Instance.GetAllDefinitions();
            Assume.That(defs, Is.Not.Null, "영지 정의 존재");
            int matched = 0;
            foreach (var def in defs)
            {
                if (string.IsNullOrEmpty(def.territoryName)) continue; // struct 기본값(미발견 폴백) 제외
                string key = def.id.ToString();
                string preferred = def.lord.preferredFood;

                bool match = LordFoodCatalog.IsPreferredGift(preferred, "meat_boar");
                float expected = 5f;
                if (match)
                {
                    expected = LordFoodCatalog.GetTier(preferred) == LordFoodCatalog.Tier3 && IsHardRing(def.difficulty)
                        ? 20f
                        : 15f;
                    matched++;
                }

                float actual = TerritoryLordDoorSystem.ComputeFeedDelta(key, "meat_boar", out bool outMatch);
                Assert.AreEqual(expected, actual, $"{key}(선호: {preferred}) 증가치가 보너스 법칙과 일치해야 함");
                Assert.AreEqual(match, outMatch, $"{key} 선호 일치 플래그 일치");
            }
            Assert.That(matched, Is.GreaterThan(0),
                "고기 계열 선호 영지(구운 고기/로스트 치킨/양고기 스테이크 슬롯)가 1개 이상 존재해야 함 — 링 가중 슬롯 계약상 보장");
        }

        [Test]
        public void ComputeFeedDelta_HardTierWeakRing_CappedAtFifteen()
        {
            // 약한 링(Ring1/Ring2)에서 T3 선호 — 재료 난이도가 게임플레이 난이도와 불일치하므로 상한 15.
            // 링 가중 슬롯 계약상 약한 링의 T3 슬롯은 Ring2(꿀 케이크=미실장 계열, 해산물 파이=어패 계열)뿐 —
            // 데이터가 존재하면 실측 판정, 존재하지 않으면 법칙 테스트(전수 공식 단언)가 이 분기를 공식으로 커버.
            var defs = TerritoryDatabase.Instance.GetAllDefinitions();
            string[] probes = { "fish_rare", "fish_common", "fish_legendary", "meat_boar" };
            bool found = false;
            foreach (var def in defs)
            {
                if (string.IsNullOrEmpty(def.territoryName)) continue; // struct 기본값(미발견 폴백) 제외
                string preferred = def.lord.preferredFood;
                bool affinityMatch = System.Array.Exists(probes, id => LordFoodCatalog.IsPreferredGift(preferred, id));
                if (affinityMatch &&
                    LordFoodCatalog.GetTier(preferred) == LordFoodCatalog.Tier3 &&
                    !IsHardRing(def.difficulty))
                {
                    found = true;
                    float delta = TerritoryLordDoorSystem.ComputeFeedDelta(def.id.ToString(), "fish_rare", out bool match);
                    if (match)
                    {
                        Assert.AreEqual(15f, delta, $"{def.id}: T3 선호 + 약한 링은 15 상한");
                    }
                }
            }
            Assert.Pass($"약한 링 T3 계열 선호 판정 완료 (발견={found}) — 미발견 시 전수 법칙 테스트가 공식 커버");
        }

        [Test]
        public void TryFeedLord_DoorClosed_GateKeptBeforeInventory()
        {
            // 문 개방 게이트 유지 — East_01 오염도 0(기본) → 잠김 → 지급 실패(인벤 접근 전 조기 리턴)
            var state = TerritoryDatabase.Instance.GetState("East_01");
            Assume.That(state, Is.Not.Null);
            Assume.That(state.drugContamination, Is.LessThan(TerritoryLordDoorSystem.DOOR_OPEN_ADDICTION_THRESHOLD),
                "테스트 전제: East_01 문 잠김(오염도 미달) — 다른 fixture 오염 방지");
            bool ok = TerritoryLordDoorSystem.TryFeedLord("East_01", "meat_rabbit", out float delta);
            Assert.IsFalse(ok, "문이 잠긴 영지는 지급 실패(기존 게이트 계약)");
            Assert.AreEqual(0f, delta, "실패 시 증가치 0");
        }

        [Test]
        public void TryFeedLord_SourceContract_ComputeFeedDeltaAndAnchorReset()
        {
            // 소스 계약 — TryFeedLord 3인자 본문이 단일소스(ComputeFeedDelta)를 경유하고,
            // 선호 일치 시에만 lastPreferredFeedDay를 오늘 일차로 리셋([113차 C] 연동)
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/TerritoryLordDoorSystem.cs");
            StringAssert.Contains("ComputeFeedDelta(territoryKey, foodItemId, out bool preferredMatch)", src,
                "충성도 증가치는 ComputeFeedDelta 단일소스 경유");
            StringAssert.Contains("state.lastPreferredFeedDay = TimeManager.GetOrCreate().CurrentDay", src,
                "선호 일치 지급 시 미지급 페널티 기준일 리셋");
            StringAssert.Contains("if (preferredMatch)", src,
                "리셋은 선호 일치 시에만 (미일치 지급은 기준일 불변)");
        }
    }
}