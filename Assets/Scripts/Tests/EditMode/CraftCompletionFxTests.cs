using System;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [제작완료 연출] 계약테스트 — CraftBenchBaseUTK 3페이즈 완료 연출(빛 차오름 → 번쩍 → 조합품 등장).
    /// 실제 페이즈 애니는 Play 영역 — EditMode에서는 로직/소스계약만 검증(기존 관례 동일).
    ///  ① 골드/화이트 시간 상수 소스계약(0.72f · 0.12f · 0.25f + 0.84f/0.35f · #D4AF37 · 16ms 드라이버)
    ///  ② ExecuteCraft 실패 경로 연출 호출 부재(실패 분기에 StartCraftCompletionFx 미호출)
    ///  ③ 로직선행 계약 — 성공 분기에서 OnCraftSuccess/RefreshStorage가 연출 호출보다 앞선 순서
    ///  ④ 플래시/글로우 오버레이 pickingMode.Ignore + 결과 팝 scale 원복(1,1)
    ///  ⑤ 벡터메시 금지(84차 계약) — CreateMesh/Mesh( 소스 부재
    /// </summary>
    public class CraftCompletionFxTests
    {
        private const string BaseSourcePath = "Assets/Scripts/UI/Toolkit/CraftBenchBaseUTK.cs";

        [Test]
        public void CraftCompletionFx_TimeConstants_GoldAndWhitePhaseTimingsPresent()
        {
            string src = System.IO.File.ReadAllText(BaseSourcePath);
            Assert.That(src, Does.Contain("0.72f"), "페이즈 B 번쩍 시작 시점 0.72s");
            Assert.That(src, Does.Contain("0.12f"), "페이즈 A 슬롯당 스태거 0.12s");
            Assert.That(src, Does.Contain("0.25f"), "페이즈 C 결과 스케일업 0.25s");
            Assert.That(src, Does.Contain("0.84f"), "페이즈 C 결과 등장 시점 0.84s");
            Assert.That(src, Does.Contain("0.35f"), "페이즈 B 플래시 페이드아웃 0.35s");
            Assert.That(src, Does.Contain("#D4AF37"), "프로젝트 골드 #D4AF37");
            Assert.That(src, Does.Contain("Every(16L)"), "16ms 애니메이션 드라이버(schedule.Execute().Every)");
        }

        [Test]
        public void CraftCompletionFx_ExecuteCraftFailureBranch_HasNoFxCall()
        {
            string body = ReadExecuteCraftBody();
            int elseIdx = body.IndexOf("else", StringComparison.Ordinal);
            int rebuildIdx = body.IndexOf("RebuildBook();", StringComparison.Ordinal);
            Assert.That(elseIdx, Is.GreaterThanOrEqualTo(0), "ExecuteCraft 실패 분기(else) 존재");
            Assert.That(rebuildIdx, Is.GreaterThan(elseIdx), "실패 분기 이후 RebuildBook 존재");

            string failureBranch = body.Substring(elseIdx, rebuildIdx - elseIdx);
            Assert.That(failureBranch, Does.Not.Contain("StartCraftCompletionFx"),
                "실패 분기는 연출 없음 — StartCraftCompletionFx 미호출");

            // 연출 호출은 성공 판정 지역 변수 조건부로만 존재
            Assert.That(body, Does.Contain("bool craftSucceeded = TryCraft("),
                "TryCraft 성공 판정이 지역 변수로 분기됨(기존 흐름 동일)");
            Assert.That(body, Does.Contain("if (craftSucceeded) StartCraftCompletionFx();"),
                "연출 호출은 성공 조건부 전용");
        }

        [Test]
        public void CraftCompletionFx_LogicBeforeFx_OnCraftSuccessAndRefreshStoragePrecedeFxCall()
        {
            string body = ReadExecuteCraftBody();
            int logicIdx = body.IndexOf("OnCraftSuccess();", StringComparison.Ordinal);
            int storageIdx = body.IndexOf("RefreshStorage();", StringComparison.Ordinal);
            int fxIdx = body.IndexOf("StartCraftCompletionFx();", StringComparison.Ordinal);
            Assert.That(logicIdx, Is.GreaterThanOrEqualTo(0), "성공 분기 OnCraftSuccess 존재");
            Assert.That(storageIdx, Is.GreaterThanOrEqualTo(0), "로직 RefreshStorage 존재");
            Assert.That(fxIdx, Is.GreaterThanOrEqualTo(0), "성공 경로 연출 호출 존재");
            Assert.That(fxIdx, Is.GreaterThan(logicIdx), "OnCraftSuccess가 연출 호출보다 앞선 순서");
            Assert.That(fxIdx, Is.GreaterThan(storageIdx),
                "RebuildBook/RefreshStorage 로직 완전 실행 후 연출 시작(순수 장식 — 로직 결합 금지)");
        }

        [Test]
        public void CraftCompletionFx_OverlayElements_DeclarePickingModeIgnoreAndScaleRestore()
        {
            string src = System.IO.File.ReadAllText(BaseSourcePath);
            Assert.That(src, Does.Contain("name = \"CraftGlowOverlay\", pickingMode = PickingMode.Ignore"),
                "슬롯 글로우 오버레이 — pickingMode Ignore");
            Assert.That(src, Does.Contain("name = \"CraftFlashOverlay\", pickingMode = PickingMode.Ignore"),
                "조합 영역 화이트 플래시 오버레이 — pickingMode Ignore");
            Assert.That(src, Does.Contain("new Scale(new Vector2(1f, 1f))"),
                "결과 팝 종료 시 scale 원복(1,1)");
        }

        [Test]
        public void CraftCompletionFx_NoVectorMeshUsage()
        {
            string src = System.IO.File.ReadAllText(BaseSourcePath);
            Assert.That(src, Does.Not.Contain("CreateMesh"), "벡터메시 금지(84차 계약) — CreateMesh 부재");
            Assert.That(src, Does.Not.Contain("Mesh("), "벡터메시 금지(84차 계약) — Mesh( 부재");
        }

        /// <summary>ExecuteCraft 메서드 본문 추출 — 소스계약 판정 범위를 메서드로 한정.</summary>
        private static string ReadExecuteCraftBody()
        {
            string src = System.IO.File.ReadAllText(BaseSourcePath);
            int methodStart = src.IndexOf("private void ExecuteCraft()", StringComparison.Ordinal);
            Assert.That(methodStart, Is.GreaterThanOrEqualTo(0), "ExecuteCraft 존재");
            int methodEnd = src.IndexOf("\n    }", methodStart, StringComparison.Ordinal);
            Assert.That(methodEnd, Is.GreaterThan(methodStart), "ExecuteCraft 본문 종료 감지");
            return src.Substring(methodStart, methodEnd - methodStart);
        }
    }
}
