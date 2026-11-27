using NUnit.Framework;
using System.IO;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 58-P2] 데칼 URP 지면 쿼드 전환 계약 — 테스트 11 [51-SCREEN] 실측 "흰 클러스터 57,597px
    /// (화면 99.98%)" + 지속 근원 "Decal_ScorchMark_0[DecalProjector|ScorchMarkMat|Shader Graphs/Decal]".
    /// 뿌리 = ①URP 내장 Decal 셰이더 Shader.Find 미로드(58 프로브 실측 NULL) ②2순위 매뉴얼 카피 폴백의
    /// 신규 머티리얼 기본값 color(0,0,0,0)+mainTex null. 수리 = DecalProjector 폐기 → 지면 쿼드 +
    /// URP Particles/Unlit 투명(39차 표준 세트) + 절차 텍스처 직접 바인딩.
    /// </summary>
    public class DecalUrpQuad_58Tests
    {
        [Test]
        public void DecalSpawner_UrpQuadDecal_Contract()
        {
            string path = "Assets/Scripts/Systems/DecalSpawner.cs";
            Assert.IsTrue(File.Exists(path), "DecalSpawner 소스 존재.");
            string src = File.ReadAllText(path);

            // DecalProjector 경로 폐기 — 프로젝터/매뉴얼 카피 폴백 완전 제거.
            Assert.That(src, Does.Not.Contain("DecalProjector"),
                "[58-P2] DecalProjector 경로 폐기(파손 셰이더 폴백 + 박스 불투명 렌더 근원).");
            Assert.That(src, Does.Not.Contain("Shader Graphs/Decal"),
                "[58-P2] 매뉴얼 카피 폴백 셰이더 제거 — 신규 머티리얼 기본값 color(0,0,0,0)+mainTex null 실측.");
            Assert.That(src, Does.Not.Contain("\"Universal Render Pipeline/Decal\""),
                "[58-P2] URP 내장 Decal Shader.Find 폐기(미로드 실측 — 52-b 1순위 항상 실패).");

            // 지면 쿼드 + URP Particles/Unlit 투명 표준.
            Assert.That(src, Does.Contain("Universal Render Pipeline/Particles/Unlit"),
                "[58-P2] URP Particles/Unlit 1순위(39차 표준 계열).");
            Assert.That(src, Does.Contain("Quaternion.Euler(90f, 0f, 0f)"),
                "[58-P2] 지면 수평 쿼드(세로 Quad → 수평).");
            Assert.That(src, Does.Contain("Vector3.up * 0.02f"),
                "[58-P2] 지면 리프트 — 언더 렌더 방지.");
            Assert.That(src, Does.Contain("EnableKeyword(\"_SURFACE_TYPE_TRANSPARENT\")"),
                "[58-P2] 투명 키워드(39차 표준 세트).");
            Assert.That(src, Does.Contain("renderQueue = 2990"),
                "[58-P2] 지면 흔적 큐(블롭 섀도우 2985 위·일반 투명 아래).");
            Assert.That(src, Does.Contain("SetTexture(\"_BaseMap\", texture)"),
                "[58-P2] 절차 텍스처 직접 바인딩 — 슬롯 불일치(기본 흰) 차단.");

            // 기존 계약 승계 — 절차 텍스처/수명/풀링/FA1 페이드 무손상.
            Assert.That(src, Does.Contain("CreateScorchTexture()"), "[21-L2 승계] 스콜치 절차 텍스처 유지.");
            Assert.That(src, Does.Contain("lifetime = 10f"), "[25-FA2 승계] 스콜치 수명 10s 유지.");
            Assert.That(src, Does.Contain("FadeOutDuration"), "[FA1 승계] 수명 말미 알파 램프다운 유지.");
            Assert.That(src, Does.Contain("Decal_Quad"), "[58-P2] 지면 쿼드 명명.");
        }
    }
}
