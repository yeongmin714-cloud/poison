using NUnit.Framework;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for HerbComboDatabase.
    /// Tests key generation, lookup logic, and data loading.
    /// </summary>
    public class HerbComboDatabaseTests
    {
        [Test]
        public void GetCombo_KnownPair_ReturnsResult()
        {
            // 쓴풀(A1) + 가시덤불(A2) = 독성 가시액
            var result = HerbComboDatabase.GetCombo("A1", "A2");
            Assert.IsTrue(result.HasValue, "A1 + A2 should produce a combo");
            Assert.AreEqual("독성 가시액", result.Value.resultName);
            Assert.AreEqual("적 체력 점진적 감소", result.Value.effect);
        }

        [Test]
        public void GetCombo_ReverseOrder_SameResult()
        {
            // Order-independent: A2 + A1 should be same as A1 + A2
            var fwd = HerbComboDatabase.GetCombo("A1", "A2");
            var rev = HerbComboDatabase.GetCombo("A2", "A1");
            Assert.IsTrue(fwd.HasValue);
            Assert.IsTrue(rev.HasValue);
            Assert.AreEqual(fwd.Value.resultName, rev.Value.resultName);
            Assert.AreEqual(fwd.Value.effect, rev.Value.effect);
        }

        [Test]
        public void GetCombo_NonExistent_ReturnsNull()
        {
            var result = HerbComboDatabase.GetCombo("A1", "Z99");
            Assert.IsFalse(result.HasValue, "Non-existent combo should return null");
        }

        [Test]
        public void GetCombo_SameHerb_ReturnsNull()
        {
            // Same herb combos are not defined
            var result = HerbComboDatabase.GetCombo("A1", "A1");
            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void GetCombo_RecoveryHerbs_ReturnsResult()
        {
            // 회복꽃(H1) + 생명수뿌리(H2) = 만능 치유액
            var result = HerbComboDatabase.GetCombo("H1", "H2");
            Assert.IsTrue(result.HasValue, "H1 + H2 should produce a combo");
            Assert.AreEqual("만능 치유액", result.Value.resultName);
            Assert.AreEqual("체력 풀회복", result.Value.effect);
        }

        [Test]
        public void AllCombos_Loaded_HasExpectedCount()
        {
            // 약물 조합 = 80종 (공격20 + 정신20 + 회복20 + 물리20)
            int count = HerbComboDatabase.AllCombos.Count;
            Assert.AreEqual(80, count, "Should have 80 herb combinations loaded");
        }

        [Test]
        public void GetCombo_MentalHerbs_ReturnsResult()
        {
            // 향기꽃(M1) + 환각포자(M2) = 혼란의 향수
            var result = HerbComboDatabase.GetCombo("M1", "M2");
            Assert.IsTrue(result.HasValue, "M1 + M2 should produce a combo");
            Assert.AreEqual("혼란의 향수", result.Value.resultName);
        }

        [Test]
        public void GetCombo_PhysicalHerbs_ReturnsResult()
        {
            // 잡초(P1) + 맑은잎(P2) = 기초 접착제
            var result = HerbComboDatabase.GetCombo("P1", "P2");
            Assert.IsTrue(result.HasValue, "P1 + P2 should produce a combo");
            Assert.AreEqual("기초 접착제", result.Value.resultName);
        }
    }
}