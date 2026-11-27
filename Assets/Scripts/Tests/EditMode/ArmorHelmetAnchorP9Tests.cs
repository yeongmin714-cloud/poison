using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-P9] 투구 두상 앵커 수리 — 소스 계약(Source 잠금).
    /// 13:27 Play 실측: anchor 3.01/본↔중심 1.21m — 0.28×span 필터가 부착 시점 포즈의 팔/손을
    /// 두상으로 오포함(headTop 역산 3.22=스킨 최대) → 투구 공중 부유. 수리: 0.06×span 중심축 +
    /// 두상 유효성 게이트(상반부) + 랜드마크 진단 로그.
    /// </summary>
    public class ArmorHelmetAnchorP9Tests
    {
        private static string ReadSystem() => System.IO.File.ReadAllText("Assets/Scripts/Systems/ArmorVisualAttachSystem.cs");

        [Test]
        public void Source_TightCenterlineFilter()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("float axis = 0.06f * span;"),
                "[무번호 29-P9] 두상 중심축 필터가 0.06×span(머리 반폭 스케일 일치)이어야 한다.");
            Assert.That(src, Does.Not.Contain("0.28f * span"),
                "[무번호 29-P9] 과대 필터(0.28×span — 팔/손 오포함 원인) 잔존 금지.");
        }

        [Test]
        public void Source_HeadValidityGate()
        {
            string src = ReadSystem();
            // [무번호 29-P10 갱신] 두상 필드/게이트는 진단 데이터로 유지 — 앵커 경로에서는 제거(기각).
            Assert.That(src, Does.Contain("public bool headValid;"),
                "[무번호 29-P9] 랜드마크 두상 유효성 필드(진단용) 유지.");
            Assert.That(src, Does.Contain("headTop >= mn + 0.5f * span"),
                "[무번호 29-P9] 두상 상반부 게이트(진단 데이터 품질) 유지.");
        }

        [Test]
        public void Source_AnchorDiagLog_And_Fallback()
        {
            string src = ReadSystem();
            // [무번호 29-P10 갱신] 진단 로그는 P10 라벨로 승격 — 앵커는 105차 공식 단일 경로.
            Assert.That(src, Does.Contain("[29-P10] 투구 앵커 = 105차 공식"),
                "[무번호 29-P10] 앵커 진단 로그(headTop 참고/helmetH/anchorY)가 존재해야 한다.");
            Assert.That(src, Does.Contain("worldAnchor = bone.position + Vector3.up * (0.26f * BodyScale); // [105차 F3]"),
                "[무번호 29-P10] 투구 앵커 = 105차 공식 단일 배선(무조건).");
        }

        [Test]
        public void Source_Marker_And_Deterministic()
        {
            string src = ReadSystem();
            Assert.That(src, Does.Contain("[무번호 29-P9]"), "[무번호 29-P9] 마커 존재.");
            int idx = src.IndexOf("[29-P9] 투구 두상 앵커");
            string block = src.Substring(System.Math.Max(0, idx - 1500), System.Math.Min(3000, src.Length - System.Math.Max(0, idx - 1500)));
            Assert.That(block, Does.Not.Contain("UnityEngine.Random"),
                "[무번호 29-P9] 앵커 산출 결정론 — 난수 API 0건.");
        }
    }
}
