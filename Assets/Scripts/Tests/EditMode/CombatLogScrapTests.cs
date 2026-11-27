// [123차] Phase 3 — 전투로그 폐기 계약 테스트.
// 목적: ①CombatLog no-op 스텁 계약 동결(AddEntry 후 Count==0 / GetRecentEntries 빈 배열 /
//       GetLastEntry null / Clear no-throw) ②CombatLogUTK.cs 파일 부재 소스 계약
//       ③스텁 소스 계약(시그니처 유지 + 링버퍼 폐기).
// 패턴 선례: WorldMapSceneWiringTests(소스-텍스트 계약, Application.dataPath 기준 상대경로).
using System.IO;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    public class CombatLogScrapTests
    {
        private const string StubSourcePath = "Assets/Scripts/Systems/CombatLog.cs";
        private const string RemovedUiSourcePath = "Assets/Scripts/UI/Toolkit/CombatLogUTK.cs";

        private string ReadSource(string path)
        {
            var full = Path.Combine(Application.dataPath, "..", path);
            Assert.IsTrue(File.Exists(full), $"소스 파일 없음: {path}");
            return File.ReadAllText(full);
        }

        // ==================== no-op 스텁 런타임 계약 ====================

        [Test]
        public void AddEntry_AfterCall_CountStaysZero()
        {
            CombatLog.Clear();
            CombatLog.AddEntry("폐기 검증 엔트리", ProjectName.Systems.LogType.Damage);
            Assert.AreEqual(0, CombatLog.Count, "폐기 스텁은 엔트리를 저장하지 않아야 함");
        }

        [Test]
        public void GetRecentEntries_AlwaysReturnsEmptyList()
        {
            CombatLog.AddEntry("폐기 검증 엔트리");
            var entries = CombatLog.GetRecentEntries();
            Assert.IsNotNull(entries, "빈 배열 계약 — null 반환 금지");
            Assert.AreEqual(0, entries.Count, "폐기 스텁은 항상 빈 배열");
        }

        [Test]
        public void GetLastEntry_AlwaysReturnsNull()
        {
            CombatLog.AddEntry("폐기 검증 엔트리");
            var last = CombatLog.GetLastEntry();
            Assert.IsFalse(last.HasValue, "폐기 스텁은 항상 null");
        }

        [Test]
        public void Clear_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => CombatLog.Clear(), "Clear는 no-op로 예외 없어야 함");
        }

        // ==================== 소스 계약 ====================

        [Test]
        public void CombatLogUTK_FileRemoved()
        {
            // [123차] P3-1 — UI 창 삭제 계약(.cs + .cs.meta 모두 부재)
            var cs = Path.Combine(Application.dataPath, "Scripts/UI/Toolkit/CombatLogUTK.cs");
            var meta = Path.Combine(Application.dataPath, "Scripts/UI/Toolkit/CombatLogUTK.cs.meta");
            Assert.IsFalse(File.Exists(cs), "CombatLogUTK.cs가 삭제되지 않음 — 전투로그 폐기 계약 위반");
            Assert.IsFalse(File.Exists(meta), "CombatLogUTK.cs.meta가 삭제되지 않음");
        }

        [Test]
        public void CombatLogStub_KeepsSignatures_AndNoRingBuffer()
        {
            string src = ReadSource(StubSourcePath);
            // 시그니처 유지 계약 — 호출부 12파일 무수정 보장
            StringAssert.Contains("public static void AddEntry(string message, LogType type = LogType.Normal)", src);
            StringAssert.Contains("public static List<CombatLogEntry> GetRecentEntries(int count = 20)", src);
            StringAssert.Contains("public static CombatLogEntry? GetLastEntry()", src);
            StringAssert.Contains("public static void Clear()", src);
            StringAssert.Contains("public static int Count => 0", src);
            // 상태 저장소 폐기 계약 — 링버퍼 잔존 금지
            StringAssert.DoesNotContain("_entries", src, "링버퍼/상태 저장소가 폐기되지 않음");
            StringAssert.DoesNotContain("MAX_ENTRIES", src, "링버퍼 상수가 폐기되지 않음");
        }
    }
}
