using System.Text.RegularExpressions;
using NUnit.Framework;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [Phase 4] 조합 아이템 id 3체계 통일 검증.
    /// HerbComboDatabase.MakeComboId 도입 후 모든 생성부(CraftingUI/CraftingHelper/AlchemyUI)가
    /// 동일한 결정론 id("combo_<사전순재료1>_<사전순재료2>")를 사용하는지 확인한다.
    /// </summary>
    public class ComboItemIdTests
    {
        /// <summary>정규 canonical 형식: combo_{영문1자+숫자1~2}_{영문1자+숫자1~2} (예: combo_A1_P2)</summary>
        private static readonly Regex CanonicalComboIdRegex =
            new Regex(@"^combo_[A-Z]\d{1,2}_[A-Z]\d{1,2}$");

        [Test]
        public void MakeComboId_OrderIndependent()
        {
            // 인자 순서와 무관하게 동일 id 생성 (사전순 정렬: A1 < P2)
            string fwd = HerbComboDatabase.MakeComboId("A1", "P2");
            string rev = HerbComboDatabase.MakeComboId("P2", "A1");

            Assert.AreEqual("combo_A1_P2", fwd, "A1+P2 → combo_A1_P2");
            Assert.AreEqual("combo_A1_P2", rev, "P2+A1 → 동일 id (순서 무관)");
            Assert.AreEqual(fwd, rev, "두 호출 결과 일치");
        }

        [Test]
        public void MakeComboId_Deterministic()
        {
            // 동일 입력 3회 → 동일 출력 (결정론)
            string a = HerbComboDatabase.MakeComboId("H1", "M2");
            string b = HerbComboDatabase.MakeComboId("H1", "M2");
            string c = HerbComboDatabase.MakeComboId("H1", "M2");

            Assert.AreEqual(a, b, "1회차 = 2회차");
            Assert.AreEqual(b, c, "2회차 = 3회차");
            Assert.IsFalse(string.IsNullOrEmpty(a), "빈 id 아님");
        }

        [Test]
        public void HerbComboResultIds_CanonicalFormat()
        {
            var combos = HerbComboDatabase.AllCombos;
            Assert.Greater(combos.Count, 0, "GAME_DATA에서 조합 DB 로드 (비어있으면 안 됨)");

            foreach (var kv in combos)
            {
                var result = kv.Value;
                Assert.IsFalse(string.IsNullOrEmpty(result.resultId),
                    $"resultId 비어있음: key={kv.Key}");
                Assert.IsTrue(CanonicalComboIdRegex.IsMatch(result.resultId),
                    $"resultId가 정규 형식(combo_[A-Z]0-99_[A-Z]0-99) 아님: '{result.resultId}' (key={kv.Key})");
                // 이중 접두사/combo_ 2회 등장 방지 검증 (AlchemyUI combo_combo_ 버그 회귀 가드)
                Assert.AreEqual(1, Regex.Matches(result.resultId, "combo_").Count,
                    $"combo_ 접두사 중복: '{result.resultId}'");
            }
        }
    }
}
