using NUnit.Framework;
using ProjectName.UI.Toolkit;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [63차] 수면 패널 센터링 경합 수리 계약 — ComputeFitBox 순수 계산 + 소스 어서션
    /// (SleepPanelFigmaTests 스타일 — GoldEconomyTests 소스 어서션 선례).
    /// 루트원인: OnWindowOpen의 raw 624×1101 기준 중앙배치 스케줄이 Open()의 ApplyCanvasFit
    /// ([84차] 등배수 창박스)을 16ms 뒤 덮어쓰는 경합 → k≠1 화면(예: 2560×1440)에서
    /// 하단 ~197px 절단(수동 저장 슬롯 3~5행 + "저장 후 수면 시작" 버튼 화면 밖 —
    /// "침대에서 세이브 UI가 안뜬다" 사용자 보고의 직접 원인).
    /// </summary>
    public class SleepPanelFitTests
    {
        private static string Panel() => System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/SleepPanelUTK.cs");

        // ═════════════════════════ 순수 계산 — SleepPanelUTK.ComputeFitBox(out 3개) ═════════════════════════

        [Test]
        public void ComputeFitBox_2560x1440_UniformScale_TopClamped_NoBottomCut()
        {
            SleepPanelUTK.ComputeFitBox(2560f, 1440f, out float k, out float left, out float top);
            Assert.That(k, Is.EqualTo(2560f / 1920f).Within(0.0001f),
                "k=min(rootW/1920, rootH/1080)=4/3 등배수(2560×1440은 1920×1080과 동일 16:9).");
            Assert.That(left, Is.EqualTo((2560f - 624f * k) * 0.5f).Within(0.01f),
                "left=스케일 창박스(624k) 수평 중앙.");
            Assert.That(top, Is.EqualTo(0f).Within(0.0001f),
                "top=max(0,(1440−1101k)/2) — 1440−1101×4/3<0 → 0 클램프(구 경합의 top=169 재발 금지).");
            // [63차] 하단 절단 상한 = 피그마 설계 자체 블리드(패널 1101 > 캔버스 1080 → 21 design px × k).
            // 스펙 원문 "top+1101k ≤ 1440"은 1101×4/3=1468>1440으로 산술 성립 불가 — 등배수 계약상
            // 가능한 최선은 "설계 블리드(21px×k=28px) 이내 절단"이며, 구 경합(중앙 배치 덮어쓰기 →
            // top=169.5, 절단 ~197px)은 이 한계를 크게 초과하므로 재발 시 본 테스트가 적출한다.
            float bottomOverflow = Mathf.Max(0f, top + 1101f * k - 1440f);
            Assert.That(bottomOverflow, Is.LessThanOrEqualTo(21f * k + 0.01f),
                "하단 절단 ≤ 설계 블리드 21px×k — 세이브 슬롯 3~5행/시작 버튼이 절단되던 ~197px 재발 금지.");
        }

        [Test]
        public void ComputeFitBox_1024x768_FitsFully_NoBottomOrRightCut()
        {
            SleepPanelUTK.ComputeFitBox(1024f, 768f, out float k, out float left, out float top);
            Assert.That(k, Is.EqualTo(1024f / 1920f).Within(0.0001f),
                "k=min(1024/1920, 768/1080)=0.5333 — 폭 제한 등배수.");
            Assert.That(top + 1101f * k, Is.LessThanOrEqualTo(768f + 0.01f),
                "top+1101k ≤ 768 — 하단 절단 없음(1101×0.5333=587.2 < 768).");
            Assert.That(left + 624f * k, Is.LessThanOrEqualTo(1024f + 0.01f),
                "left+624k ≤ 1024 — 우측 절단 없음.");
        }

        [Test]
        public void ComputeFitBox_1920x1080_BaseResolution_UnityScale_TopZero()
        {
            SleepPanelUTK.ComputeFitBox(1920f, 1080f, out float k, out float left, out float top);
            Assert.That(k, Is.EqualTo(1f).Within(0.0001f),
                "기준 해상도 — k=1.0(등배수 계약 기준점).");
            Assert.That(top, Is.EqualTo(0f).Within(0.0001f),
                "top=max(0,(1080−1101)/2)=0 — 패널 1101>캔버스 1080 설계 블리드는 상단 클램프로 흡수.");
            Assert.That(left, Is.EqualTo((1920f - 624f) * 0.5f).Within(0.01f),
                "left=648 raw 중앙.");
        }

        [Test]
        public void ComputeFitBox_UnresolvableLayout_GuardsToZero()
        {
            SleepPanelUTK.ComputeFitBox(0f, 1080f, out float kZero, out _, out _);
            Assert.That(kZero, Is.EqualTo(0f).Within(0.0001f),
                "레이아웃 미확정(≤1) — k=0 가드(호출부 적용 스킵, 재시도 대기).");
            SleepPanelUTK.ComputeFitBox(float.NaN, 1080f, out float kNaN, out _, out _);
            Assert.That(kNaN, Is.EqualTo(0f).Within(0.0001f),
                "NaN 입력 — k=0 가드(기존 ApplyCanvasFit 가드 의미 유지 + NaN 명시 차단).");
        }

        // ═════════════════════════ 소스 어서션 — 경합 제거/위임 계약 ═════════════════════════

        /// <summary>[63차] 개방 경합 수리 — ApplyCanvasFit 재시도 스케줄로 교체, 구 중앙배치 덮어쓰기 부재.</summary>
        [Test]
        public void OnWindowOpen_SchedulesApplyCanvasFit_CenteringOverwriteRemoved()
        {
            var src = Panel();
            Assert.That(src, Does.Contain("schedule.Execute(ApplyCanvasFit)"),
                "[63차] OnWindowOpen — 등배수 창박스 재시도 스케줄([88차 B1] 선례 패턴).");
            Assert.That(src, Does.Contain("schedule.Execute(ApplyCanvasFit).StartingIn(400)"),
                "[63차] 레이아웃 지연 대응 3단 재시도(16/120/400ms).");
            Assert.That(src, Does.Not.Contain("CenterOnParent"),
                "[63차] raw 624×1101 기준 중앙배치 덮어쓰기 경합 완전 제거(메서드+호출부+주석 토큰).");
        }

        /// <summary>[63차] ApplyCanvasFit → ComputeFitBox 위임 계약 — 배치 산식 단일 소스화.</summary>
        [Test]
        public void ApplyCanvasFit_DelegatesToComputeFitBox_PureHelper()
        {
            var src = Panel();
            Assert.That(src, Does.Contain(
                    "public static void ComputeFitBox(float rootW, float rootH, out float k, out float left, out float top)"),
                "[63차] 순수 헬퍼 공개 시그니처(편집기 테스트 직접 호출 대상).");
            Assert.That(src, Does.Contain(
                    "ComputeFitBox(root.resolvedStyle.width, root.resolvedStyle.height, out float k, out float left, out float top)"),
                "[63차] ApplyCanvasFit 본문이 헬퍼에 위임(산식 이중 유지 금지).");
            Assert.That(src, Does.Contain("FigmaCanvasLayout.CanvasWidth").And.Contain("FigmaCanvasLayout.CanvasHeight"),
                "[63차] 캔버스 진실치 1920/1080은 FigmaCanvasLayout 상수 단일 소스.");
        }
    }
}
