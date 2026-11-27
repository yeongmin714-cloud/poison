using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [84차] 고품질화 3종 — 소스 문자열 어서션(진실치 마커 검증).
    /// ①수면 패널 등배수 창박스(raw 624×1101 하단 절단 → 세이브 섹션 화면 밖 수리)
    /// ②HUD 게이지 벡터 메시 승격(UTKGaugeStripArc → UTKCircularGauge + TrailFraction 통합)
    /// ③RTS 링 셰이더 고품질(이중 링 + 부드러운 회전 대시 + 스폰 확산 웨이브)
    /// </summary>
    public sealed class CanvasFitAndRingQualityTests
    {
        private static string Read(string rel) => System.IO.File.ReadAllText(rel);

        [Test]
        public void SleepPanel_Applies_Uniform_Canvas_Scale()
        {
            var src = Read("Assets/Scripts/UI/Toolkit/SleepPanelUTK.cs");
            Assert.That(src, Does.Contain("ApplyCanvasFit"),
                "[84차] 등배수 창박스 적용 진입점");
            Assert.That(src, Does.Contain("Mathf.Min(rootW / FigmaCanvasLayout.CanvasWidth, rootH / FigmaCanvasLayout.CanvasHeight)"),
                "[84차] k=min(rootW/1920, rootH/1080) — 창박스=figma×k 계약");
            Assert.That(src, Does.Contain("style.width = WinW * k;").And.Contain("style.height = WinH * k;"),
                "[84차] 창 크기 = figma×k(구 raw 통짜 — 909 화면에서 하단 192px 절단 원인)");
            Assert.That(src, Does.Contain("_content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));"),
                "[84차] 내부 raw 유지 + transform.scale=k — 글자/간격 왜곡 없음");
        }

        [Test]
        public void HudGauge_Uses_HighQualityStrip_Bake_With_Separate_Trail()
        {
            var src = Read("Assets/Scripts/UI/Toolkit/HUDUTK.cs");
            Assert.That(src, Does.Contain("new UTKGaugeStripArc()"),
                "[85차] 게이지 arc = 베이크 스트립 확정 복귀(벡터 메시 Play 소실 재발 폐기)");
            Assert.That(src, Does.Not.Contain("new UTKCircularGauge()"),
                "[85차] 벡터 메시 게이지 잔존 금지(HUD)");
            Assert.That(src, Does.Contain("_hpTrailArc.style.width = GaugeSize;")
                .And.Contain("PositionArc(_hpTrailArc, 0f, k);"),
                "[85차] 뒷줄 아크 = 앞 아크 동일 프레임/동일 슬롯(83차 계약 유지)");
        }

        [Test]
        public void GaugeStrip_Bake_Is_HighResolution_With_Raised_Import_Cap()
        {
            var strip = Read("Assets/Scripts/UI/Toolkit/UTKGaugeStripArc.cs");
            Assert.That(strip, Does.Contain("public const int FrameSize = 126;"),
                "[85차] 프레임 126px — 65×126=8190 ≤ 임포트 상한(다운스케일 0)");
            var meta = Read("Assets/Resources/UI/UTKGaugeArcStrip.png.meta");
            Assert.That(meta, Does.Contain("maxTextureSize: 8192"),
                "[85차] 임포트 상향 — 구 2048 캡이 8320×128을 2048×31로 강제 다운스케일(저품질 근본 원인)");
        }

        [Test]
        public void SelectionRing_Shader_Has_DoubleRing_Dash_And_Wave()
        {
            var src = Read("Assets/Resources/FX/Selection/SelectionRing.shader");
            Assert.That(src, Does.Contain("_SecondRingOffset").And.Contain("_SecondRingAlpha"),
                "[84차] 이중 링 — 주 밴드 외곽 얇은 보조 링");
            Assert.That(src, Does.Contain("float dash = smoothstep(0.00, 0.18, arcPos) * (1.0 - smoothstep(0.45, 0.62, arcPos));"),
                "[85차 부드러움] 회전 대시 — 완화된 대비+넓은 페이드 에지(84차 하드 에지 개선)");
            Assert.That(src, Does.Contain("_WaveT").And.Contain("lerp(_RingRadius * 0.30, 0.97, _WaveT)"),
                "[84차] 스폰 확산 웨이브 — 중심→외곽 1회성 링");
        }

        [Test]
        public void CommandMarker_Drives_Wave_And_Persistent_Rings_Get_Dash_Speed()
        {
            var marker = Read("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.That(marker, Does.Contain("burstMat.SetFloat(\"_WaveT\", burstT)"),
                "[84차] ack 버스트 구간 웨이브 구동");
            Assert.That(marker, Does.Contain("endMat.SetFloat(\"_WaveT\", -1f)"),
                "[84차] 버스트 종료 웨이브 off 원복(1회성)");
            var pres = Read("Assets/Scripts/Systems/CommandRingPresentation.cs");
            Assert.That(pres, Does.Contain("material.SetFloat(\"_ArcSpeed\", PersistentArcSpeed);"),
                "[84차] 지속형(이동/선택) 링 대시 회전 활성");
            Assert.That(pres, Does.Contain("material.SetFloat(\"_SecondRingOffset\", ShaderSecondRingOffset);"),
                "[84차] 이중 링 배선");
        }
    }
}
