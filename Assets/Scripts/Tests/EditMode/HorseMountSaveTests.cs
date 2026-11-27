using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 86-P1] 말 세이브 연동 계약 — DTO 직렬화 라운드트립 + 수출입 null/빈 폴백 + pending 버퍼.
    /// Instance 미부트(EditMode) 경로가 전부 예외 없이 폴백되는 것이 핵심(구세이브 = 전부 야생 유지).
    /// </summary>
    public class HorseMountSaveTests
    {
        [SetUp]
        public void AssumeCleanInstance()
        {
            // 다른 테스트가 HorseMountSystem 인스턴스를 만들어둔 경우 이 테스트는 판정 불가 → 스킵
            Assume.That(HorseMountSystem.Instance, Is.Null,
                "HorseMountSystem 인스턴스가 존재하면 Export/Import가 실인스턴스 경로로 분기된다");
        }

        [TearDown]
        public void Cleanup()
        {
            HorseMountSystem.PendingImport = null;
        }

        [Test]
        public void HorseSaveEntry_JsonRoundtrip()
        {
            var e = new HorseSaveEntry { breed = "백마", tamed = true, posX = 1.5f, posY = 2.5f, posZ = 3.5f };
            string json = JsonUtility.ToJson(e);
            var back = JsonUtility.FromJson<HorseSaveEntry>(json);
            Assert.That(back.breed, Is.EqualTo("백마"), "[86-P1] 품종 라운드트립");
            Assert.That(back.tamed, Is.True, "[86-P1] 길들임 라운드트립");
            Assert.That(back.posX, Is.EqualTo(1.5f).Within(0.0001f), "[86-P1] 좌표 라운드트립");
            Assert.That(back.posZ, Is.EqualTo(3.5f).Within(0.0001f), "[86-P1] 좌표 라운드트립");
        }

        [Test]
        public void SaveData_HasHorsesField_DefaultEmpty()
        {
            var data = new SaveData();
            Assert.That(data.horses, Is.Not.Null, "[86-P1] horses 필드 기본값 빈 리스트");
            Assert.That(data.saveVersion, Is.EqualTo(6), "[86-P1] saveVersion v6 승격");
            Assert.That(JsonUtility.ToJson(data).Contains("horses"), Is.True,
                "[86-P1] SaveData JSON에 horses 직렬화 포함");
        }

        [Test]
        public void ExportSaveData_NoInstance_ReturnsEmpty_NoThrow()
        {
            List<HorseSaveEntry> list = null;
            Assert.DoesNotThrow(() => list = HorseMountSystem.ExportSaveData(),
                "[86-P1] 미부트 수출 — 예외 없이 빈 리스트");
            Assert.That(list, Is.Not.Null);
            Assert.That(list.Count, Is.EqualTo(0));
        }

        [Test]
        public void ImportSaveData_NullOrEmpty_NoThrow_NoPending()
        {
            Assert.DoesNotThrow(() => HorseMountSystem.ImportSaveData(null),
                "[86-P1] 널 임포트 — 구세이브 폴백(무시)");
            Assert.DoesNotThrow(() => HorseMountSystem.ImportSaveData(new List<HorseSaveEntry>()),
                "[86-P1] 빈 리스트 임포트 — 무시");
            Assert.That(HorseMountSystem.PendingImport, Is.Null,
                "[86-P1] 널/빈 임포트는 pending을 건드리지 않는다(멱등)");
        }

        [Test]
        public void ImportSaveData_NoInstance_BufferedPending()
        {
            var entries = new List<HorseSaveEntry>
            {
                new HorseSaveEntry { breed = "적토마", tamed = true, posX = 4f, posY = 1f, posZ = 5f }
            };
            HorseMountSystem.ImportSaveData(entries);
            Assert.That(HorseMountSystem.PendingImport, Is.Not.Null,
                "[86-P1] Instance 미성립 → pending 버퍼 저장");
            Assert.That(HorseMountSystem.PendingImport.Count, Is.EqualTo(1));
            Assert.That(HorseMountSystem.PendingImport[0].breed, Is.EqualTo("적토마"));
            Assert.That(HorseMountSystem.PendingImport[0].tamed, Is.True);
        }
    }
}
