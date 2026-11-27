using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// 영지 툴팁 확장 라인 계약 테스트 — [2026-10-21 사용자 요구] 동결.
    /// MapWindow 호버 툴팁(플레이어 소유 영지)의 저장고/배치 라인 포맷과
    /// 경고 기준(≥80% 곧 가득, 100% 가득, 배정 0명=전원 휴식 경고)을 동결한다.
    /// 포맷 변경 시 이 테스트가 계약 갱신을 강제한다.
    /// </summary>
    public class TerritoryTooltipContentTests
    {
        // ==================== 저장고 라인 ====================

        [Test]
        public void BuildStorageLine_Normal_NoWarning()
        {
            string line = TerritoryTooltipContent.BuildStorageLine(5, 20);
            Assert.AreEqual("저장고: 5/20", line);
        }

        [Test]
        public void BuildStorageLine_AtWarnThreshold_ShowsWarn()
        {
            // 80% = 경고 임계 이상 → "곧 가득" 경고
            string line = TerritoryTooltipContent.BuildStorageLine(16, 20);
            StringAssert.Contains("⚠️", line);
            StringAssert.Contains("16/20", line);
            StringAssert.Contains("확장 권장", line);
        }

        [Test]
        public void BuildStorageLine_Full_ShowsFullWarning()
        {
            string line = TerritoryTooltipContent.BuildStorageLine(20, 20);
            StringAssert.Contains("🔴", line);
            StringAssert.Contains("가득", line);
            StringAssert.Contains("반입 불가", line);
        }

        [Test]
        public void BuildStorageLine_Overfull_StillFullWarning()
        {
            // 방어 케이스: used > capacity여도 크래시 없이 가득 경고
            string line = TerritoryTooltipContent.BuildStorageLine(25, 20);
            StringAssert.Contains("🔴", line);
        }

        [Test]
        public void BuildStorageLine_ZeroCapacity_NoInfo()
        {
            string line = TerritoryTooltipContent.BuildStorageLine(0, 0);
            StringAssert.Contains("정보 없음", line);
        }

        // ==================== 배치 라인 ====================

        [Test]
        public void BuildDeploymentLine_MixedDeployment_SortedWithResting()
        {
            var counts = new Dictionary<GuardTaskSystem.GuardTask, int>
            {
                { GuardTaskSystem.GuardTask.Defend, 3 },
                { GuardTaskSystem.GuardTask.Gather, 2 },
                { GuardTaskSystem.GuardTask.Mine, 2 },
                { GuardTaskSystem.GuardTask.Attack, 1 },
                { GuardTaskSystem.GuardTask.None, 0 }
            };
            string line = TerritoryTooltipContent.BuildDeploymentLine(counts);
            // 표시 순서 고정: 수비 → 동행 → 채집 → 사냥 → 농사 → 광부 → 특사 → 휴식
            StringAssert.Contains("수비 3", line);
            StringAssert.Contains("동행 1", line);
            StringAssert.Contains("채집 2", line);
            StringAssert.Contains("광부 2", line);
            StringAssert.Contains("휴식 0", line);
            StringAssert.Contains("배치: 수비 3 · 동행 1 · 채집 2 · 광부 2 · 휴식 0", line);
        }

        [Test]
        public void BuildDeploymentLine_AllResting_ShowsWarning()
        {
            var counts = new Dictionary<GuardTaskSystem.GuardTask, int>
            {
                { GuardTaskSystem.GuardTask.None, 8 }
            };
            string line = TerritoryTooltipContent.BuildDeploymentLine(counts);
            StringAssert.Contains("⚠️ 배치 없음 — 전원 휴식", line);
        }

        [Test]
        public void BuildDeploymentLine_SomeResting_KeepsSummary()
        {
            var counts = new Dictionary<GuardTaskSystem.GuardTask, int>
            {
                { GuardTaskSystem.GuardTask.Defend, 2 },
                { GuardTaskSystem.GuardTask.None, 3 }
            };
            string line = TerritoryTooltipContent.BuildDeploymentLine(counts);
            StringAssert.Contains("수비 2", line);
            StringAssert.Contains("휴식 3", line);
            StringAssert.DoesNotContain("배치 없음", line);
        }

        [Test]
        public void BuildDeploymentLine_EmptyCounts_NoInfo()
        {
            string line = TerritoryTooltipContent.BuildDeploymentLine(new Dictionary<GuardTaskSystem.GuardTask, int>());
            StringAssert.Contains("정보 없음", line);
        }

        [Test]
        public void BuildDeploymentLine_NullCounts_NoInfo()
        {
            string line = TerritoryTooltipContent.BuildDeploymentLine(null);
            StringAssert.Contains("정보 없음", line);
        }

        [Test]
        public void BuildDeploymentLine_AllRolesRepresented_NoCrash()
        {
            var counts = new Dictionary<GuardTaskSystem.GuardTask, int>
            {
                { GuardTaskSystem.GuardTask.Defend, 1 },
                { GuardTaskSystem.GuardTask.Attack, 1 },
                { GuardTaskSystem.GuardTask.Gather, 1 },
                { GuardTaskSystem.GuardTask.Hunt, 1 },
                { GuardTaskSystem.GuardTask.Farm, 1 },
                { GuardTaskSystem.GuardTask.Mine, 1 },
                { GuardTaskSystem.GuardTask.Envoy, 1 },
                { GuardTaskSystem.GuardTask.None, 1 }
            };
            string line = TerritoryTooltipContent.BuildDeploymentLine(counts);
            StringAssert.Contains("수비 1", line);
            StringAssert.Contains("동행 1", line);
            StringAssert.Contains("채집 1", line);
            StringAssert.Contains("사냥 1", line);
            StringAssert.Contains("농사 1", line);
            StringAssert.Contains("광부 1", line);
            StringAssert.Contains("특사 1", line);
            StringAssert.Contains("휴식 1", line);
        }
    }
}
