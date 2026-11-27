using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 68] 링 패밀리 발광 포화 캡 — 임팩트 링(HDR emissive ×2.0) + FXBandingRing "saturate 없이
    /// HDR 합산" 조합이 피격 지점을 따라다니는 순백/암색 판의 뿌리(테스트 12~14 픽셀 실측)인 것을 잠금.
    /// </summary>
    public class RingEmissionCap_68Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        [Test]
        public void BandingRing_68_EmissionSaturateCap()
        {
            string src = Read("Assets/Shaders/FXBandingRing.shader");
            Assert.That(src, Does.Contain("col.rgb = saturate(col.rgb + _EmissionColor.rgb);"),
                "[68-P1] 발광 합산 LDR 캡(순백/암색 판 포화 차단).");
            Assert.That(src, Does.Contain("[무번호 68-P1] saturate 캡"), "[68-P1] 근거 주석 보존.");
            Assert.That(src, Does.Not.Contain("col.rgb += _EmissionColor.rgb;"),
                "[68-P1] 무캡 HDR 합산 소멸.");
            Assert.That(src, Does.Contain("Blend SrcAlpha OneMinusSrcAlpha"), "[68] 알파 블렌드 유지.");
        }

        [Test]
        public void ImpactRing_68_EmissionWiringUnchanged()
        {
            string fx = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            // 발광 배선 자체는 유지(연출 계약) — 셰이더 캡만으로 포화 차단
            Assert.That(fx, Does.Contain("ApplyImpactEmissionBoost(mat, color);"), "[68] 임팩트 발광 배선 유지.");
            Assert.That(fx, Does.Contain("mat.mainTexture = EnsureBandTexture();"), "[68] 밴드 텍스처 바인딩 유지.");
        }
    }
}
