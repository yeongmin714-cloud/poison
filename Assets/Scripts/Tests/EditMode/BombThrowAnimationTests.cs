using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [185차] 폭탄 투척 애니메이션 연출 계약테스트 — 실제 애니/코루틴은 Play 영역, EditMode는 소스계약만.
    ///  ① TriggerThrow 발화 — 기존 즉시 스폰으로 던지기 애니 미발화였던 원인 수리 계약
    ///  ② 릴리스 지연 상수(0.35f) — ThrowObj 윈드업 구간 후 폭탄 스폰
    ///  ③ 발화 순서 — 애니 발화가 릴리스(스폰) 정의보다 소스 순서상 선행
    ///  ④ 재발화 가드 — _isThrowPending으로 연타 중복 코루틴 금지
    /// </summary>
    public class BombThrowAnimationTests
    {
        private const string SrcPath = "Assets/Scripts/Systems/BombArmController.cs";

        [Test]
        public void BombThrow_AnimationTriggerAndDelayedRelease_ContractPins()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Contain("TriggerThrow()"),
                "던지기 애니 발화(HumanoidClipDriver Throw 트리거 — Player_AC ThrowObj 상태 존재 확인)");
            Assert.That(src, Does.Contain("ThrowReleaseDelaySeconds"),
                "릴리스 지연 상수 존재(ThrowObj 윈드업 구간)");
            Assert.That(src, Does.Contain("0.35f"), "릴리스 지연값 0.35f");
            Assert.That(src, Does.Contain("ThrowReleaseRoutine"), "코루틴 기반 지연 릴리스");
            Assert.That(src, Does.Contain("GetComponentInChildren<HumanoidClipDriver>()"),
                "드라이버 조회 선례(ConstructionManager) 동일 — 루트/자식 양쪽");
        }

        [Test]
        public void BombThrow_AnimationBeforeSpawn_SourceOrderAndGuard()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            int trig = src.IndexOf("TriggerThrow()", System.StringComparison.Ordinal);
            int rel = src.IndexOf("private void ReleaseBombNow()", System.StringComparison.Ordinal);
            Assert.That(trig, Is.GreaterThanOrEqualTo(0), "TriggerThrow 발화부 존재");
            Assert.That(rel, Is.GreaterThanOrEqualTo(0), "지연 릴리스 본체 존재");
            Assert.That(trig, Is.LessThan(rel),
                "애니 발화가 릴리스 정의보다 소스 먼저(호출→지연→스폰 계약)");
            int guardCount = CountOccurrences(src, "_isThrowPending");
            Assert.That(guardCount, Is.GreaterThanOrEqualTo(3),
                "재발화 가드 필드+게이트+원복 3소 이상(연타 무시 계약)");
        }

        /// <summary>문자열 발생 횟수 카운트 — 가드 소 대조용.</summary>
        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int idx = haystack.IndexOf(needle, System.StringComparison.Ordinal);
            while (idx >= 0)
            {
                count++;
                idx = haystack.IndexOf(needle, idx + needle.Length, System.StringComparison.Ordinal);
            }
            return count;
        }
    }
}
