using NUnit.Framework;
using System.IO;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [205차 배치A-1] 소형 수리 2건 계약 — 소스 문자열 어서션(BombFluidPreview193Tests 관례).
    ///  ①P5 폭탄 0개 알림 제거 — 0개 무장 거부는 조용히(Debug.Log+return) 유지, 게이트 내 PlayerNotify.Notify 부재
    ///    (사용자 지시: "폭탄 부족시 따로 알림 안떠도 괜찮고")
    ///  ②P8 Test_10 인벤 안장 지급 — 폭탄 3개 지급 직후 안장 1개 지급(말 길들이기 테스트)
    ///  ③N204 바구니 병존 — HorseMountSystem 월드 안장 바구니(204차) 무수정 검증(파일 읽기 전용, 수정 금지)
    /// </summary>
    public class BombNotifyAndSaddle205Tests
    {
        private const string ControllerPath =
            "Assets/Scripts/Systems/BombArmController.cs";
        private const string SetupPath =
            "Assets/Scripts/Systems/TestTerritoryCombatSetup.cs";
        private const string HorseMountPath =
            "Assets/Scripts/Systems/HorseMountSystem.cs";

        private string Src(string path)
        {
            return System.IO.File.ReadAllText(path);
        }

        [Test]
        public void N205_BombGate_Silent_Reject()
        {
            // [205차 P5] 폭탄 0개 게이트 — 알림 제거, 조용한 거부 유지
            string src = Src(ControllerPath);

            // 게이트 slice — "int count = PlayerInventory.Instance" 헤더부터 다음 "return;" 까지
            int gateIdx = src.IndexOf("int count = PlayerInventory.Instance");
            Assert.That(gateIdx, Is.GreaterThanOrEqualTo(0),
                "[205] 폭탄 0개 게이트 헤더 존재(개수 게이트 유지)");
            int returnIdx = src.IndexOf("return;", gateIdx);
            Assert.That(returnIdx, Is.GreaterThan(gateIdx),
                "[205] 게이트 구간 내 return; 존재 — 거부 로직 유지 핀");
            string slice = src.Substring(gateIdx, returnIdx - gateIdx + "return;".Length);

            Assert.That(slice, Does.Contain("return;"),
                "[205] 0개 무장 거부(return) 유지 — 조용한 거부 계약");
            Assert.That(slice, Does.Contain("Debug.Log"),
                "[205] 0개 거부 시 Debug.Log 유지 — 콘솔 추적 가능");
            Assert.That(slice, Does.Not.Contain("PlayerNotify.Notify"),
                "[205] 폭탄 0개 게이트 내 알림 부재 — 사용자 지시(폭탄 부족시 따로 알림 안떠도 괜찮고)");
            Assert.That(src, Does.Contain("[205차] 알림 제거"),
                "[205] 주석 마커 존재 — 알림 제거 근거 문서화");
        }

        [Test]
        public void N205_Test10_Saddle_Grant()
        {
            // [205차 P8] Test_10 인벤 안장 지급 — 폭탄 지급 직후 위치 핀
            string src = Src(SetupPath);

            Assert.That(src, Does.Contain("bombInv.AddItem(PlayerInventory.Saddle, 1)"),
                "[205] Test_10 인벤 안장 1개 지급(PlayerInventory.Saddle — id=mat_saddle 선례 패턴)");
            int bombIdx = src.IndexOf("PlayerInventory.Bomb_Explosive, 3");
            Assert.That(bombIdx, Is.GreaterThanOrEqualTo(0),
                "[205] 기존 폭탄 3개 지급 존재 — 무수정 핀");
            int saddleIdx = src.IndexOf("bombInv.AddItem(PlayerInventory.Saddle, 1)");
            Assert.That(saddleIdx, Is.GreaterThan(bombIdx),
                "[205] 안장 지급 = 폭탄 지급 직후(같은 if 블록) 위치 핀");
        }

        [Test]
        public void N204_Basket_Intact()
        {
            // [204차] 월드 안장 바구니 병존 유지 — HorseMountSystem은 본 배치에서 수정 금지(읽기만)
            string src = Src(HorseMountPath);
            Assert.That(src, Does.Contain("LootBasket.Create("),
                "[204] 월드 안장 바구니 병존 유지 — 205차 인벤 안장과 공존(사용자 기본값 승인)");
        }
    }
}
