using NUnit.Framework;
using ProjectName.Core.Data;

namespace ProjectName.Tests.EditMode
{
    public class GourmetDatabaseTests
    {
        [Test]
        public void GourmetDatabase_Loaded_All5Grades()
        {
            var all = GourmetDatabase.All;
            Assert.AreEqual(5, all.Count, "Should have 5 gourmet grades (1★~5★)");
        }

        [Test]
        public void Grade1_IsSeomin()
        {
            var grade = GourmetDatabase.GetGrade(1);
            Assert.IsTrue(grade.HasValue);
            Assert.AreEqual("서민", grade.Value.gradeName);
        }

        [Test]
        public void Grade3_IsJunggeup()
        {
            var grade = GourmetDatabase.GetGrade(3);
            Assert.IsTrue(grade.HasValue);
            Assert.AreEqual("중급", grade.Value.gradeName);
        }

        [Test]
        public void Grade5_IsWangshil()
        {
            var grade = GourmetDatabase.GetGrade(5);
            Assert.IsTrue(grade.HasValue);
            Assert.AreEqual("왕실", grade.Value.gradeName);
        }

        [Test]
        public void Grade5_Description_MentionsKing()
        {
            var grade = GourmetDatabase.GetGrade(5);
            Assert.IsTrue(grade.HasValue);
            Assert.IsTrue(grade.Value.description.Contains("왕"), 
                "Top grade should mention the king");
        }

        [Test]
        public void InvalidGrade_ReturnsNull()
        {
            var grade = GourmetDatabase.GetGrade(0);
            Assert.IsFalse(grade.HasValue);
            grade = GourmetDatabase.GetGrade(6);
            Assert.IsFalse(grade.HasValue);
        }

        [Test]
        public void DishInfo_CanHaveStarRating()
        {
            var dish = new DishInfo
            {
                Id = "D99",
                DisplayName = "테스트 요리",
                StarRating = 3
            };
            Assert.AreEqual(3, dish.StarRating);
        }
    }
}