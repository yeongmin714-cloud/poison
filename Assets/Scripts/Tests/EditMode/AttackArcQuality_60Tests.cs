using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 60] 아크 고품질 잔여분 계약 — 47계획 §8 P2의 미착지 3항목.
    /// ①폭 프로파일 지수 상수화(1.7 — 전두엽 유지·꼬리 가늘게 강화) ②속도 비례 알파(_spd 버퍼+RefTipSpeed)
    /// ③글로우/코어 색 보간(FXPalette.AttackPrimary 35% — 공격 이펙트 통일색 단일 소스 계약). 전부 긍정 잠금(Contains) — 주석 오염 오탐 회피.
    /// </summary>
    public class AttackArcQuality_60Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);
        private static string Src() => Read("Assets/Scripts/Systems/AttackArcVFX.cs");

        [Test]
        public void Arc_WidthProfilePow_60()
        {
            string src = Src();
            Assert.That(src, Does.Contain("internal const float WidthProfilePow = 1.7f;"),
                "[무번호 60] 폭 프로파일 지수 1.7 핀.");
            Assert.That(src, Does.Contain("Mathf.Pow(life01, WidthProfilePow)"),
                "[무번호 60] EmitPass 폭 계산이 지수 상수 사용.");
            Assert.That(src, Does.Contain("WidthProfilePow = 1.7f"),
                "[무번호 60] 기존 인라인 1.2 폐기 확인용 — 상수 선언 존재.");
        }

        [Test]
        public void Arc_SpeedAlpha_60()
        {
            string src = Src();
            Assert.That(src, Does.Contain("private float[] _spd;"),
                "[무번호 60] 속도 링버퍼 필드.");
            Assert.That(src, Does.Contain("_spd[_write] = speed;"),
                "[무번호 60] WriteSample 속도 기록.");
            Assert.That(src, Does.Contain("public static float RefTipSpeed = 14f;"),
                "[무번호 60] 기준 팁 속도 파사드 핀.");
            Assert.That(src, Does.Contain("float speedBoost = spd < 0f ? 1f : Mathf.Clamp(0.55f + 0.45f"),
                "[무번호 60] EmitPass 속도 boost(하한 0.55·버퍼 부재=중립 1.0 폴백).");
            Assert.That(src, Does.Contain("Mathf.Clamp01(life01 * alphaMul * speedBoost)"),
                "[무번호 60] 알파에 boost 적용.");
            Assert.That(src, Does.Contain("(_spd != null && idx < _spd.Length) ? _spd[idx] : -1f"),
                "[무번호 60] 버퍼 미할당(리플렉션 직접 진입) 호환 — 미측정=중립.");
        }

        [Test]
        public void Arc_GlowPaletteLerp_60()
        {
            string src = Src();
            Assert.That(src, Does.Contain("Color glowTint = Color.Lerp(coreTint, FXPalette.AttackPrimary, 0.35f);"),
                "[무번호 60 갱신 79-R20] 글로우=코어→FXPalette.AttackPrimary 35% 보간(공격 이펙트 통일색).");
            Assert.That(src, Does.Contain("EmitPass(now, trailSec, halfW, 1.65f, GlowPassAlphaMul, glowTint,"),
                "[무번호 60] 글로우 패스가 보간색 사용.");
        }
    }
}
