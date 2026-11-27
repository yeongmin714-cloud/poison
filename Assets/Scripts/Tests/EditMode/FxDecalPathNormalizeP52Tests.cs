using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 52] URP 데칼 경로 정규화 + DecalProjector 감시 계약.
    /// 근거(11-16 사용자 재보고 + MissingReferenceException 스택): ①DecalSpawner가 DecalProjector에
    /// URP/Lit 머티리얼을 세팅 — Lit에는 데칼 투영 패스가 없어 프로젝터 박스가 **불투명 사각**으로 렌더
    /// ("흰색 사각링"의 뿌리, 기존 "호환" 주석은 오류 가정) ②CommandMarker 데칼 1순위 셰이더가
    /// "Shader Graphs/Decal"(매뉴얼 카피 가족) — 파손 시 **마젠타 링** ③URP DecalProjector는 Renderer가
    /// 아니라 센티널 기존 감지를 완전히 통과했다(52-c에서 커버).
    /// </summary>
    public class FxDecalPathNormalizeP52Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        [Test]
        public void DecalSpawner_UsesUrPDecalShader_NotLit()
        {
            // [무번호 58-P2 계약 진화] 52차 "URP 내장 Decal 1순위" 핀은 58 프로브 실측으로 폐기 —
            //   URP 내장 Decal 셰이더는 Shader.Find = NULL(스트리핑·미로드)이라 1순위가 항상 실패했고,
            //   2순위 매뉴얼 카피 폴백의 머티리얼 기본값(color 0,0,0,0+mainTex null)이 히트 지점 거대
            //   흰/검 표면을 냈다(테스트 11 [51-SCREEN] 57,597px). 대체 = 지면 쿼드 + URP Particles/Unlit.
            string src = Read("Assets/Scripts/Systems/DecalSpawner.cs");
            Assert.That(src, Does.Contain("[무번호 58-P2] 구 프로젝터 경로 폐기"), "[58-P2] 진화 마커.");
            Assert.That(src, Does.Contain("Shader.Find(\"Universal Render Pipeline/Particles/Unlit\")"),
                "[58-P2] URP Particles/Unlit 1순위 — Shader.Find 실측 안정(내장 Decal은 미로드 실측).");
            Assert.That(src, Does.Not.Contain("Shader.Find(\"Universal Render Pipeline/Decal\")"),
                "[58-P2] 미로드 실측 셰이더(내장 Decal) Find 폐기.");
            Assert.That(src, Does.Not.Contain("Shader.Find(\"Shader Graphs/Decal\")"),
                "[58-P2] 매뉴얼 카피 폴백 폐기(머티리얼 기본값 불일치 실측).");
            // Lit 데칼 잔존 금지 — 투영 패스 부재로 불투명 사각 렌더(흰 사각링 뿌리).
            Assert.That(src, Does.Not.Contain("Shader.Find(\"Universal Render Pipeline/Lit\")"),
                "[52-b 승계] Lit 셰이더 데칼 잔존 금지.");
            // per-instance 생성 널 가드 — 머티리얼 부재 시 데칼 스킵(사각 렌더 방지).
            Assert.That(src, Does.Contain("데칼 기본 머티리얼 부재 — 데칼 스킵"),
                "[52-b 승계] 기본 머티리얼 부재 시 데칼 스킵.");
        }

        [Test]
        public void CommandMarker_DecalShaderNormalized_WithEditorErrorFallback()
        {
            // [계약 진화 65-P1] 52-a의 CommandMarker 데칼 정규화는 데칼 경로 **완전 폐기**로 승계.
            //   근거: 58-P2 프로브(내장 Decal Shader.Find NULL + 매뉴얼 카피 그래프 텍스처 무시 →
            //   불투명 흑 박스) + 공격씬 테스트 12 영상 실측(명령마다 ~0.6s 검은 사각, 센서 사각).
            //   52-a의 "정규화로 살린다" 전제가 파손 경로를 잠그고 있었으므로 폐기 계약으로 교체.
            string src = Read("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.That(src, Does.Contain("[무번호 65-P1] 데칼 경로 **완전 폐기**"), "[65-P1] 폐기 선언.");
            Assert.That(src, Does.Not.Contain("Shader.Find(\"Universal Render Pipeline/Decal\")"),
                "[65-P1] URP 내장 Decal 참조 소멸(Shader.Find NULL 실측 — 항상 실패).");
            Assert.That(src, Does.Not.Contain("Shader.Find(\"Shader Graphs/Decal\")"),
                "[65-P1] 매뉴얼 카피 그래프 참조 소멸(텍스처 무시 → 흑 박스).");
            Assert.That(src, Does.Not.Contain("UnityEditor.ShaderUtil.ShaderHasError"),
                "[65-P1] 데칼 진실원 경로 소멸(데칼 자체 폐기).");
            Assert.That(src, Does.Contain("if (_persistent) SetupPersistent();"),
                "[65-P1] 쿼드 단일 소스.");
        }

        [Test]
        public void Sentinel_CoversDecalProjectors()
        {
            string src = Read("Assets/Scripts/Systems/MagentaSentinel.cs");
            Assert.That(src, Does.Contain("using UnityEngine.Rendering.Universal;"),
                "[52-c] URP 네임스페이스 — DecalProjector 접근.");
            Assert.That(src, Does.Contain("[무번호 52-c] URP DecalProjector 전수 감시"),
                "[52-c] 데칼 프로젝터 감시 블록.");
            Assert.That(src, Does.Contain("FindObjectsByType<DecalProjector>"),
                "[52-c] DecalProjector 전수 탐색.");
            Assert.That(src, Does.Contain("[52-SENTINEL] ⚠️ 데칼 프로젝터 머티리얼 null"),
                "[52-c] null 머티리얼 1급 보고.");
            Assert.That(src, Does.Contain("[52-SENTINEL] ⚠️ 데칼 텍스처 이상"),
                "[52-c] 베이크 실패 후보 보고(순백/마젠타 평균).");
            Assert.That(src, Does.Contain("CollectDecalCandidates"),
                "[52-c] 화면 역산에 데칼 후보 포함 — [51-SCREEN]이 데칼도 이름으로 보고.");
        }
    }
}
