using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 28-M] 몬스터 충전 글로우(M1) + 정예 리듬 오라(M4) — 소스 계약(Source 잠금).
    /// 스펙: /tmp/m_a_brief.md §2/§3/§4 (ArmorBodyMeasureSpaceTests 관행 복제).
    /// ①M1 AnimalAI 충전 글로우 — windup 루프 진행도 t에 비례한 위험 적색 발광(색 단일 소스 FXPalette.MonsterDanger)
    ///   + 해제 3경로(정상 종료/중단/사망) ReleaseChargeGlow.
    /// ②M4 AfterlifeEnemy 정예 리듬 오라 — 정예(Level >= 5) 생존 중 발밑 리듬 링(1.2s 간격·반경 결정론 교대 0.9/0.55)
    ///   + FXPalette.ElitAura(AttackTelegraphIndicator.cs:34 EliteColor 리터럴 미러).
    /// 본 테스트는 브리프 §4 T1~T6 문자열을 그대로 잠금한다. 실행 판정은 QA 서브에이전트 증거로 확정
    /// (구현 서브에이전트는 Unity 배치를 실행하지 않음).
    /// </summary>
    public class MonsterChargeGlowPhaseMTests
    {
        private static string ReadAnimalAI() => System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
        private static string ReadAfterlifeEnemy() => System.IO.File.ReadAllText("Assets/Scripts/Systems/AfterlifeEnemy.cs");
        private static string ReadPalette() => System.IO.File.ReadAllText("Assets/Scripts/Systems/FXPalette.cs");

        // ===== [무번호 28-M1] 소스 계약 — AnimalAI 충전 글로우 =====

        [Test]
        public void Source_ChargeGlowMethod()
        {
            string src = ReadAnimalAI();
            // T1 — UpdateChargeGlow 정의 + windup 루프 내 호출부 존재
            Assert.That(src, Does.Contain("private void UpdateChargeGlow(float t)"),
                "[무번호 28-M1] UpdateChargeGlow 메서드 정의가 AnimalAI에 있어야 한다.");
            Assert.That(src, Does.Contain("UpdateChargeGlow(t);"),
                "[무번호 28-M1] windup 루프에서 t 진행도로 UpdateChargeGlow를 호출해야 한다.");
            // 호출부가 예고 windup 루프 내부(while 뒤)에 위치하는지 순서 잠금
            int idxWhile = src.IndexOf("while (elapsed < TelegraphWindupSeconds)", System.StringComparison.Ordinal);
            int idxCall = src.IndexOf("UpdateChargeGlow(t);", System.StringComparison.Ordinal);
            Assert.That(idxWhile, Is.GreaterThanOrEqualTo(0),
                "[무번호 28-M1] 예고 windup while 루프가 존재해야 한다.");
            Assert.That(idxCall, Is.GreaterThan(idxWhile),
                "[무번호 28-M1] UpdateChargeGlow 호출은 windup 루프 이후(내부)에 배선되어야 한다.");
        }

        [Test]
        public void Source_PaletteDanger()
        {
            string src = ReadAnimalAI();
            // T2 — 색 단일 소스 계약: FXPalette.MonsterDanger 참조 + 로컬 리터럴 부재
            Assert.That(src, Does.Contain("FXPalette.MonsterDanger"),
                "[무번호 28-M1] 충전 글로우 색은 FXPalette.MonsterDanger 단일 소스(U-1)를 참조해야 한다.");
            Assert.That(src, Does.Not.Contain("new Color(1f, 0.30f, 0.16f"),
                "[무번호 28-M1] 위험 적색 리터럴이 AnimalAI에 새로 생기면 팔레트 단일 소스 계약 위반 — 로컬 리터럴 금지.");
        }

        [Test]
        public void Source_Release3Paths()
        {
            string src = ReadAnimalAI();
            // T3 — ReleaseChargeGlow 정의 + 해제 3경로(정상 종료/중단/사망) 호출 카운트 ≥ 3
            Assert.That(src, Does.Contain("private void ReleaseChargeGlow()"),
                "[무번호 28-M1] ReleaseChargeGlow 메서드 정의가 AnimalAI에 있어야 한다.");
            int callCount = System.Text.RegularExpressions.Regex.Matches(
                src, System.Text.RegularExpressions.Regex.Escape("ReleaseChargeGlow();")).Count;
            Assert.That(callCount, Is.GreaterThanOrEqualTo(3),
                "[무번호 28-M1] 해제 3경로(루프 정상 종료/예고 중단/사망)에서 ReleaseChargeGlow가 최소 3회 호출되어야 한다.");
        }

        // ===== [무번호 28-M4] 소스 계약 — AfterlifeEnemy 정예 리듬 오라 + FXPalette =====

        [Test]
        public void Source_EliteAuraPalette()
        {
            // T4 — FXPalette.ElitAura 존재 + AfterlifeEnemy가 SpawnImpactRing에서 참조
            string palette = ReadPalette();
            Assert.That(palette, Does.Contain("public static readonly Color ElitAura"),
                "[무번호 28-M4] FXPalette에 ElitAura 멤버(1멤버 추가)가 존재해야 한다.");
            Assert.That(palette, Does.Contain("new Color(1f, 0.30f, 0.16f, 0.45f)"),
                "[무번호 28-M4] ElitAura는 AttackTelegraphIndicator.cs:34 EliteColor 리터럴(주황-적 1,0.30,0.16 / ALPHA 0.45) 실측 미러여야 한다.");

            string src = ReadAfterlifeEnemy();
            Assert.That(src, Does.Contain("FXPalette.ElitAura"),
                "[무번호 28-M4] AfterlifeEnemy 오라(림 오버레이)는 FXPalette.ElitAura를 참조해야 한다.");
            // [195차 P1] 오라 링 스폰 철거 — 호출부 부재 계약으로 리핀(림 오버레이와 그 ElitAura 참조는 유지).
            Assert.That(src, Does.Not.Contain("ShockwaveRingFX.SpawnImpactRing"),
                "[195차 P1] 정예 오라 링(지면 임팩트 링) 스폰 호출 철거 — 사용자 지시 몬스터 링 시각 폐지.");
            Assert.That(src, Does.Not.Contain("elite: true"),
                "[195차 P1] 오라 링 전용 elite 승계 인자도 호출 철거와 함께 부재해야 한다(림 오버레이 무영향).");
        }

        [Test]
        public void Source_EliteRhythm()
        {
            string src = ReadAfterlifeEnemy();
            // T5 — 정예 게이트 + 1.2s 간격 + 반경 결정론 교대(0.9/0.55)
            Assert.That(src, Does.Contain("Level >= 5"),
                "[무번호 28-M4] 오라는 정예 게이트(Level >= 5)로만 발화해야 한다.");
            Assert.That(src, Does.Contain("Time.time >= _nextEliteAuraAt"),
                "[무번호 28-M4] 오라 리듬은 _nextEliteAuraAt 시각 게이트로 관리되어야 한다.");
            Assert.That(src, Does.Contain("+= 1.2f"),
                "[무번호 28-M4] 오라 간격은 1.2s 리듬(_nextEliteAuraAt += 1.2f)이어야 한다.");
            // [195차 P1] 링 발화 철거 — 반경 교대(홀/짝 counter·0.9/0.55)는 링 전용 인자라 부재 계약으로 리핀.
            Assert.That(src, Does.Not.Contain("% 2 == 0"),
                "[195차 P1] 반경 교대 counter(% 2) 연산은 링 스폰 철거와 함께 부재해야 한다.");
            Assert.That(src, Does.Not.Contain("0.9f : 0.55f"),
                "[195차 P1] 교대 반경 삼항식(0.9f : 0.55f)도 링 스폰 철거와 함께 부재해야 한다(북키핑 step/타이머는 유지).");
        }

        [Test]
        public void Source_Marker()
        {
            // T6 — 마커 + 결정론 잠금(신규 코드에 정규화 난수 API 부재)
            string animalAI = ReadAnimalAI();
            string afterlife = ReadAfterlifeEnemy();
            Assert.That(animalAI, Does.Contain("[무번호 28-M1]"),
                "AnimalAI 신규 코드에 [무번호 28-M1] 마커가 있어야 한다.");
            Assert.That(afterlife, Does.Contain("[무번호 28-M4]"),
                "AfterlifeEnemy 신규 코드에 [무번호 28-M4] 마커가 있어야 한다.");
            Assert.That(animalAI, Does.Not.Contain("UnityEngine.Random"),
                "[무번호 28-M1] AnimalAI 신규 코드 블록은 결정론만 사용 — 정규화 난수 API(UnityEngine.Random) 부재 잠금.");
        }
    }
}
