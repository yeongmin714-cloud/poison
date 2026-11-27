using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [204차] Test_10 말 이동 테스트 스폰 계약 — HorseMountSystem.TestSceneSpawn().
    /// 배경: 사용자 지시 "테스트씬 10에 안장도 하나 넣어줘 말 움직임 테스트해보게" →
    ///   Test_10_TerritoryCombat에 야생 말(백마) 1 + 말 안장(mat_saddle) 픽업 바구니 1을 스폰해
    ///   픽업 → 길들이기(안장+E) → 탑승 → 이동 전체 흐름을 테스트 가능하게 한다.
    /// 소스 계약(HorseSpawnSceneGate195Tests 관행 — src 문자열 잠금 + MemberSlice 본문 핀):
    ///  ① TestSceneSpawn 게이트 배선(씬명 리터럴 + 조기 반환)
    ///  ② 말 1마리(SpawnHorse 경로 재사용) + 안장 바구니 1개(LootBasket.Create + PlayerInventory.Saddle)
    ///  ③ 바구니 lifetime 3600s 핀(기본 30f 소멸 방지)
    ///  ④ 195차 야생 스폰 게이트 무손상 재핀(회귀 감시)
    ///  ⑤ Awake 훅 위치(pending 복원 후) + 단일 호출
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 Unity 배치를 실행하지 않음).
    /// </summary>
    public class HorseTestSceneSpawn204Tests
    {
        private static string Src() =>
            System.IO.File.ReadAllText("Assets/Scripts/Systems/HorseMountSystem.cs");

        /// <summary>헤더~다음 멤버 경계까지의 본문 조각 추출(HorseSpawnSceneGate195Tests.MemberSlice 동일 패턴).</summary>
        private static string MemberSlice(string src, string header, string nextHeader)
        {
            int fn = src.IndexOf(header, System.StringComparison.Ordinal);
            Assert.That(fn, Is.GreaterThanOrEqualTo(0), "[204차] " + header + " 정의가 존재해야 한다.");
            int end = src.IndexOf(nextHeader, fn, System.StringComparison.Ordinal);
            return end > fn ? src.Substring(fn, end - fn) : src.Substring(fn);
        }

        [Test]
        public void T1_TestSceneSpawn_Gate()
        {
            var src = Src();
            Assert.That(src, Does.Contain("void TestSceneSpawn()"),
                "[204차] Test_10 전용 스폰 경로(TestSceneSpawn)가 존재해야 한다.");
            string body = MemberSlice(src, "void TestSceneSpawn()", "void SpawnHorse(BreedSpec");
            Assert.That(body, Does.Contain("Test_10_TerritoryCombat"),
                "[204차] 스폰은 Test_10_TerritoryCombat 씬에서만 동작해야 한다(씬명 리터럴 게이트 핀).");
            Assert.That(body, Does.Contain("return;"),
                "[204차] 비 Test_10 씬에서는 조기 반환해야 한다(기존 씬 무영향 게이트).");
        }

        [Test]
        public void T2_OneHorse_OneSaddle_Basket()
        {
            string body = MemberSlice(Src(), "void TestSceneSpawn()", "void SpawnHorse(BreedSpec");
            Assert.That(body, Does.Contain("SpawnHorse(Breeds[0]"),
                "[204차] 야생 말 1마리 — SpawnHorse 기존 경로(백마=Breeds[0]) 재사용 핀(크기 정규화+접지+애니 승계).");
            Assert.That(body, Does.Contain("LootBasket.Create("),
                "[204차] 말 안장 픽업 바구니 — LootBasket.Create 기존 경로 핀.");
            Assert.That(body, Does.Contain("PlayerInventory.Saddle"),
                "[204차] 안장 아이템은 PlayerInventory.Saddle(mat_saddle) 정적 필드 사용 핀.");
            Assert.That(body, Does.Contain("AddItem"),
                "[204차] 바구니에 안장 1개 적재(AddItem) 핀.");
        }

        [Test]
        public void T3_Saddle_Lifetime_3600()
        {
            string body = MemberSlice(Src(), "void TestSceneSpawn()", "void SpawnHorse(BreedSpec");
            Assert.That(body, Does.Contain("3600f"),
                "[204차] 바구니 lifetime 3600s 명시 핀 — 기본 30f는 30초 후 소멸되어 테스트 흐름 붕괴(사용 금지).");
        }

        [Test]
        public void T4_WildGate_Intact()
        {
            // [204차] 195차 G3 재핀 — TestSceneSpawn 추가 후에도 야생 스폰 게이트(비 MainScene 스킵) 무손상 감시.
            string body = MemberSlice(Src(), "void SpawnHorses()", "void SpawnHorse(BreedSpec");
            Assert.That(body, Does.Contain("if (!IsMainScene())"),
                "[204차] SpawnHorses의 195차 씬 게이트(if (!IsMainScene()))가 유지되어야 한다.");
            Assert.That(body, Does.Contain("return;"),
                "[204차] SpawnHorses의 비 MainScene 조기 반환(195차 G3)이 유지되어야 한다.");
        }

        [Test]
        public void T5_Awake_Hook_After_PendingImport()
        {
            var src = Src();
            int hook = src.IndexOf("TestSceneSpawn();", System.StringComparison.Ordinal);
            int pending = src.IndexOf("PendingImport != null", System.StringComparison.Ordinal);
            Assert.That(hook, Is.GreaterThanOrEqualTo(0),
                "[204차] Awake의 TestSceneSpawn() 호출이 존재해야 한다.");
            Assert.That(pending, Is.GreaterThanOrEqualTo(0),
                "[204차] Awake의 pending 세이브 복원 블록이 존재해야 한다(기존 무손상).");
            Assert.That(hook, Is.GreaterThan(pending),
                "[204차] TestSceneSpawn()은 pending 세이브 복원(PendingImport 처리) 이후에 호출되어야 한다.");
        }

        [Test]
        public void T6_Single_Call()
        {
            var src = Src();
            int count = 0; int idx = 0;
            while ((idx = src.IndexOf("TestSceneSpawn();", idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += 1;
            }
            Assert.That(count, Is.EqualTo(1),
                "[204차] TestSceneSpawn() 호출은 정확히 1회 — 중복 호출 시 말/바구니 이중 스폰(정확히 1회 핀).");
        }
    }
}
