using NUnit.Framework;
using ProjectName.UI.Toolkit;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [계획 v2 Phase 2] HUD 게이지/핫바 "화면 하단 고정 앵커" 일원화 계약 단정.
    ///
    /// 하단 고정 앵커는 사용자 요구(캡처 간 세로 편차 제거) — Figma 캔버스 y=855.2/861.6 좌표 규약과
    /// 의도적 차이임을 계획 v2 Phase 2에 기록(소스 한국어 주석 동일 문구 포함).
    /// Figma 23:6 원형 링 3층 폼(링 텍스처 / UTKCircularGauge arc / 중앙 아이콘)과 간격 24 raw 유지도 함께 단정.
    ///
    /// 소스 계약 테스트 스타일 — QuestWindowFigma93Tests/FigmaCanvasLayoutTests 선례처럼
    /// 프로덕션 소스 파일 텍스트를 읽어 배선 계약을 단정한다(최소 단정 원칙).
    /// </summary>
    public class HudBottomAnchorTests
    {
        [Test]
        public void BottomAnchorMargins_DeclareScreenBottomContract_24Gauge_12Hotbar()
        {
            Assert.That(HUDUTK.GaugeBottomMarginPx, Is.EqualTo(24f).Within(0.001f),
                "게이지 하단 여백은 계획 v2 Phase 2 지정 상수 24px여야 한다.");
            Assert.That(HotbarUIUTK.HotbarBottomMarginPx, Is.EqualTo(12f).Within(0.001f),
                "핫바 하단 여백은 계획 v2 Phase 2 지정 상수 12px여야 한다.");
        }

        [Test]
        public void HUDUTK_PositionHost_UsesScreenBottomAnchorAndDropsFigmaCanvasYAnchor()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/HUDUTK.cs");
            Assert.That(source, Does.Contain("rootSize.y - GaugeSize * k - GaugeBottomMarginPx"),
                "게이지 호스트 top은 화면 하단 고정 앵커식(rootSize.y - GaugeSize * k - 24px)이어야 한다.");
            Assert.That(source, Does.Not.Contain("855.2f * anchorY"),
                "게이지 Y 배치는 Figma 캔버스 분율 앵커(855.2f * anchorY)를 더 이상 쓰지 않는다(계획 v2 Phase 2).");
        }

        [Test]
        public void HotbarUIUTK_ApplyFigmaBounds_UsesScreenBottomAnchorAndDropsFigmaCanvasYAnchor()
        {
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/HotbarUIUTK.cs");
            Assert.That(source, Does.Contain("rootSize.y - FigmaHeight * k - HotbarBottomMarginPx"),
                "핫바 top은 화면 하단 고정 앵커식(rootSize.y - FigmaHeight * k - 12px)이어야 한다.");
            Assert.That(source, Does.Not.Contain("861.6f * anchorY"),
                "핫바 Y 배치는 Figma 캔버스 분율 앵커(861.6f * anchorY)를 더 이상 쓰지 않는다(계획 v2 Phase 2).");
        }

        [Test]
        public void HUDUTK_KeepsFigma236CircularRingThreeLayerFormContract()
        {
            // 3층 폼: ①링 배경 텍스처(UI/HudGaugeRing, ScaleToFit) ②UTKCircularGauge 벡터 arc ③중앙 아이콘(GaugeIconSize 68.8).
            // 시각 재설계 금지 — 현행 폼 잔존만 단정.
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/HUDUTK.cs");
            Assert.That(source, Does.Contain("Resources.Load<Texture2D>(\"UI/HudGaugeRing\")"),
                "3층 폼 1층 — 링 배경 텍스처(UI/HudGaugeRing) 로드 잔존.");
            Assert.That(source, Does.Contain("ScaleToFit"),
                "3층 폼 1층 — 링 배경 ScaleToFit 잔존.");
            Assert.That(source, Does.Contain("new UTKCircularGauge()"),
                "3층 폼 2층 — UTKCircularGauge 벡터 arc(배경 디머 링 + FillColor 시계방향 fill) 잔존.");
            Assert.That(source, Does.Contain("GaugeIconSize"),
                "3층 폼 3층 — 중앙 아이콘 GaugeIconSize 배치 잔존.");
            Assert.That(HUDUTK.GaugeIconSize, Is.EqualTo(68.8f).Within(0.001f),
                "중앙 아이콘 크기는 Figma 23:6 값 68.8을 유지해야 한다.");
        }

        [Test]
        public void GaugeSpacing_KeepsFigma236Raw24Gap()
        {
            Assert.That(HUDUTK.GaugeGap, Is.EqualTo(24f).Within(0.001f),
                "게이지 간격은 Figma 23:6 간격 24 raw를 유지해야 한다.");
            Assert.That(HUDUTK.GaugeSize, Is.EqualTo(137.6f).Within(0.001f),
                "게이지 크기는 Figma 23:6 bounds 137.6을 유지해야 한다.");
        }

        [Test]
        public void HUDUTK_BuildsFigma236SevenLayerGaugeStack_WithSemanticColors()
        {
            // [2026-10-09 Phase B] 3층 → Figma 7층 스택 확장 계약.
            string source = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/HUDUTK.cs");
            Assert.That(source, Does.Contain("BgGlow"), "층1 — 게이지색 저알파 뒤광(bg-radial-glow 대체).");
            Assert.That(source, Does.Contain("OuterTrack"), "층2 — outer-track 백색 저알파 링.");
            Assert.That(source, Does.Contain("new UTKCircularGauge()"), "층3 — 진행 arc(UTKCircularGauge) 잔존.");
            Assert.That(source, Does.Contain("InnerPlate"), "층5 — inner-plate 흰 원판.");
            Assert.That(source, Does.Contain("RimHighlight"), "층6 — inner-plate-rim-highlight.");
            Assert.That(source, Does.Contain("_hpColor = new Color(1f, 0.30f, 0.30f, 1f)"),
                "시맨틱 색 계약 — HP 레드 유지(Figma 목업 오렌지 미채택 근거: 09-24 사용자 설계).");
            Assert.That(source, Does.Contain("_stColor = new Color(1f, 0.83f, 0.30f, 1f)"),
                "시맨틱 색 계약 — 스태미나 옐로 유지.");
            Assert.That(source, Does.Contain("rootSize.y - GaugeSize * k - GaugeBottomMarginPx"),
                "하단 고정 앵커 계약(Phase 2)은 Phase B 확장 후에도 잔존해야 한다.");
        }
    }
}