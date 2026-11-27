using NUnit.Framework;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// TerritoryDatabase 영주 데이터 결정론 검증 (링 가중 음식 선택 포함).
    /// 결정론 계약: GenerateLordInfo는 Seed 기반으로 재시작 간 동일 결과 보장.
    /// </summary>
    public sealed class TerritoryDataTests
    {
        private const string T3HardFood1 = "꿀 케이크";
        private const string T3HardFood2 = "양고기 스테이크";
        private const string T3HardFood3 = "해산물 파이";

        /// <summary>
        /// East_01 (Ring1) 영주 정보 — 링 가중 음식 선택 도입 후 결정론 값.
        /// [계산 근거] seed = GetDeterministicHash("East_1"), rng.Next(10) 첫 추첨 = 슬롯 5,
        /// Ring1 슬롯 배열 index 5 = "빵과 치즈" (기존 균등 랜덤 시절 "구운 고기"에서 변경).
        /// </summary>
        [Test]
        public void TerritoryDatabase_East1_LordInfo()
        {
            var def = TerritoryDatabase.Instance.GetDefinition("East_01");
            Assert.IsNotNull(def, "East_01 정의 존재");
            Assert.AreEqual(TerritoryDifficulty.Ring1, def.difficulty, "East_01은 Ring1");
            Assert.AreEqual("빵과 치즈", def.lord.preferredFood,
                "East_01(Ring1) preferredFood — 링 가중치 도입 후 결정론 값 (seed 해시 → 첫 추첨 슬롯 5)");
        }

        /// <summary>
        /// Ring4 영주의 선호 음식은 T3(어려움) 음식 집합에만 속해야 한다.
        /// [2026-10-21 실측] 제약 조건이 "T3 고정"이므로 T2 음식은 포함 금지.
        /// Ring4 슬롯 배열은 T3 100% (꿀 케이크 4/양고기 스테이크 3/해산물 파이 3) — 최강 링은 어려운 음식만.
        /// </summary>
        [Test]
        public void TerritoryDatabase_Ring4_Lords_PreferHardFoods()
        {
            // Ring4 = index 16~20, 4개 국가 × 5 = 20개 영지 전수 검증
            var nations = new[] { NationType.East, NationType.West, NationType.South, NationType.North };
            foreach (var nation in nations)
            {
                for (int index = 16; index <= 20; index++)
                {
                    var def = TerritoryDatabase.Instance.GetDefinition(nation, index);
                    Assert.IsNotNull(def, $"{nation}_{index:D2} 정의 존재");
                    Assert.AreEqual(TerritoryDifficulty.Ring4, def.difficulty, $"{nation}_{index:D2}은 Ring4");

                    string food = def.lord.preferredFood;
                    bool isT3 = food == T3HardFood1 || food == T3HardFood2 || food == T3HardFood3;
                    Assert.IsTrue(isT3,
                        $"{nation}_{index:D2} Ring4 preferredFood('{food}')는 T3 집합({T3HardFood1}/{T3HardFood2}/{T3HardFood3})에 속해야 함");
                }
            }
        }
        /// <summary>
        /// Empire(황제국, 최강 난이도) 영주의 선호 음식도 고급(T3) 음식 집합에 속해야 한다.
        /// [2026-10-21 사용자 요구] 강한 영지의 영주일수록 재료를 구하기 힘든 음식이 지정되도록.
        /// 현재 소스는 Empire 영주가 균등 10종 풀에서 선택하므로 이 테스트가 RED로 실패해야 정상.
        /// </summary>
        [Test]
        public void TerritoryDatabase_EmpireLord_PreferHardFoods()
        {
            var def = TerritoryDatabase.Instance.GetDefinition(NationType.Empire, 1);
            Assert.IsNotNull(def, "Empire_01 정의 존재");
            Assert.AreEqual(TerritoryDifficulty.Empire, def.difficulty, "Empire_01은 Empire 난이도");

            string food = def.lord.preferredFood;
            bool isT3 = food == T3HardFood1 || food == T3HardFood2 || food == T3HardFood3;
            Assert.IsTrue(isT3,
                $"Empire_01 preferredFood('{food}')는 최강 난이도인 만큼 T3 집합({T3HardFood1}/{T3HardFood2}/{T3HardFood3})에 속해야 함");
        }
    }
}