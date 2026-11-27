using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 65] 피격 흰/검 사각 종결 계약 — 공격씬 테스트 12 영상 픽셀 실측(흰 링 #f8f8f8 포화,
    /// 검정 박스 #080000 하드엣지, [51-SCREEN] 데칼 패스 사각 미보고)에 따른 근본 수리 잠금.
    /// </summary>
    public class HitDefectClosure_65Tests
    {
        private static string Read(string path)
            => System.IO.File.ReadAllText(path).Replace("\r\n", "\n");

        [Test]
        public void Marker_65_DecalPathRetired()
        {
            string src = Read("Assets/Scripts/Systems/CommandMarker.cs");
            // 검은 사각 뿌리 = 지속형 마커의 DecalProjector 박스(58-P2 프로브: 내장 Decal Shader.Find NULL
            // + 매뉴얼 카피 그래프 텍스처 무시 → 불투명 흑 박스). 쿼드 경로 단일 소스로 고정.
            Assert.That(src, Does.Not.Contain("AddComponent<DecalProjector>"), "[65-P1] DecalProjector 부착 소멸.");
            Assert.That(src, Does.Not.Contain("Shader Graphs/Decal"), "[65-P1] 파손 그래프 참조 소멸.");
            Assert.That(src, Does.Not.Contain("DecalScaleMode"), "[65-P1] 데칼 스케일 모드 잔재 소멸.");
            Assert.That(src, Does.Contain("if (_persistent) SetupPersistent();"), "[65-P1] 지속형 쿼드 경로.");
            Assert.That(src, Does.Contain("[무번호 65-P1] TrySetupDecal **삭제**"), "[65-P1] 폐기 근거 주석 보존.");
        }

        [Test]
        public void SelectionRing_65_AccentSaturationCap()
        {
            string src = Read("Assets/Resources/FX/Selection/SelectionRing.shader");
            // 흰 사각(확산 웨이브 링) 뿌리 = 강조색 HDR 배수(_Intensity×highlight×rim)의 프레임버퍼 포화.
            // saturate 캡으로 팀색 위계 유지 + 순백 차단(54차 알파 캡 계열 — 구조적 차단 계약).
            Assert.That(src, Does.Contain("half3 accentCol = saturate(_TeamColor.rgb * _Intensity * highlight * (1.0 + _RimIntensity * rim));"),
                "[65-P2] 강조색 LDR 캡(순백 포화 차단).");
            Assert.That(src, Does.Contain("[무번호 65-P2] 강조색 포화 캡"), "[65-P2] 근거 주석 보존.");
            // 알파 블렌드 계약 무손상(39차 표준)
            Assert.That(src, Does.Contain("Blend SrcAlpha OneMinusSrcAlpha"), "[65-P2] 알파 블렌드 유지.");
        }
    }
}
