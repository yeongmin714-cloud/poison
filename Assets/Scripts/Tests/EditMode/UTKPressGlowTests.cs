using System;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [클릭 글로우 FX] 계약테스트 — UTKPressGlow + UIToolkitBootstrap 1줄 훅.
    /// 실제 글로우 애니는 Play 영역 — EditMode에서는 로직/소스계약만 검증(UTKClickRippleTests 관례 동일).
    ///  ① 연출 상수 소스계약(0.22f 수명 · 0.55f 피크 · 0.10f 채움 · 8f 반경 · Every(16L) 드라이버 · 순백 0xFFFFFF)
    ///  ② 오버레이 계약 — pickingMode.Ignore + Position.Absolute + worldBound 앵커 + WorldToLocal 변환
    ///  ③ 앵커 상승(라벨→버튼 판) + 루트 상승 금지(전화면 글로우 방지) + 빈 화면 가드
    ///  ④ 세대 토큰 + 동시 예산(MaxGlows) 퇴거 경로
    ///  ⑤ 부트스트랩 1줄 훅 — UTKClickRipple.Ensure() 이후 부착(훅 순서 계약)
    ///  ⑥ 벡터메시 금지(84차 계약) — CreateMesh/Mesh( 소스 부재
    /// </summary>
    public class UTKPressGlowTests
    {
        private const string SrcPath = "Assets/Scripts/UI/Toolkit/UTKPressGlow.cs";
        private const string BootstrapPath = "Assets/Scripts/UI/Toolkit/UIToolkitBootstrap.cs";

        [Test]
        public void PressGlowFx_TimeConstants_LifePeakFillRadiusAndDriverPresent()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Contain("0.22f"), "글로우 수명 0.22s(눌림 플래시 — 물결보다 짧게)");
            Assert.That(src, Does.Contain("0.55f"), "피크 알파 0.55(선형 페이드아웃 시작점)");
            Assert.That(src, Does.Contain("0.10f"), "내부 빛 채움 0.10(리플 동일 미감)");
            Assert.That(src, Does.Contain("8f"), "모서리 반경 8px(창/슬롯 라운드 정합)");
            Assert.That(src, Does.Contain("Every(16L)"), "16ms 애니메이션 드라이버(schedule.Execute().Every — 리플 선례)");
            Assert.That(src, Does.Contain("0xFF, 0xFF, 0xFF"), "순백 글로우 #FFFFFF(중립 발광 — 리플 계약 연속성)");
            Assert.That(src, Does.Contain("EaseOutQuad"), "페이드아웃 이징 주석 핀(EaseOutQuad)");
        }

        [Test]
        public void PressGlowFx_OverlayElement_IgnorePickingAbsoluteAndElementBoundsAnchor()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Contain("name = \"PressGlow\", pickingMode = PickingMode.Ignore"),
                "글로우 오버레이 — pickingMode Ignore(포인터 흡수 금지)");
            Assert.That(src, Does.Contain("style.position = Position.Absolute"),
                "절대좌표 오버레이(리플 선례 동일)");
            Assert.That(src, Does.Contain("anchor.worldBound.min"), "요소 worldBound 앵커 — 요소 자체 경계 계약");
            Assert.That(src, Does.Contain("anchor.worldBound.max"), "요소 worldBound 앵커(대각점)");
            Assert.That(src, Does.Contain("WorldToLocal("), "패널 좌표 → 오버레이 로컬 변환(확장 메서드 수신자 선례)");
            Assert.That(src, Does.Contain("TrickleDown.TrickleDown"), "루트 TrickleDown 훅 — 모든 UI 요소 클릭 포착");
            Assert.That(src, Does.Contain("evt.target == _hookRoot"),
                "UI 전용 가드 — 빈 화면(월드) 클릭 무glow(리플 동일 계약)");
            Assert.That(src, Does.Contain("this.BringToFront()"),
                "스폰 시 오버레이 재선두 — 열린 창 위로 글로우 렌더(커서는 자체 재선두 유지)");
        }

        [Test]
        public void PressGlowFx_AnchorClimb_BoundedAndRootGuardPresent()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Contain("MinAnchorSizePx"), "앵커 최소 크기 상수 — 라벨/아이콘 과소 글로우 방지");
            Assert.That(src, Does.Contain("MaxClimbLevels"), "앵커 상승 상한 — 과도 상승 방지");
            Assert.That(src, Does.Contain("up == _hookRoot"), "루트 상승 금지 — 전화면 글로우 방지(요소 글로우 계약)");
            Assert.That(src, Does.Contain("evt.target as VisualElement"), "최심 피킹 요소에서 앵커 산정 시작");
        }

        [Test]
        public void PressGlowFx_GenerationTokenAndBudget_RaceGuardsPresent()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Contain("_generation++"), "세대 토큰 선점(Ensure 재부착 시 증가 — 연타/재부착 레이스 가드)");
            Assert.That(src, Does.Contain("_myGeneration != _generation"),
                "드라이버 세대 불일치 가드 — 구세대 드라이버 자가 종료");
            Assert.That(src, Does.Contain("panel == null || glow.panel == null"),
                "탈착 조기 종료 가드(리플/CraftBench 선례 동일)");
            Assert.That(src, Does.Contain("task.Pause()"), "드라이버 자가 정지(task.Pause)");
            Assert.That(src, Does.Contain("MaxGlows"), "동시 글로우 예산 상수");
            Assert.That(src, Does.Contain("_active.RemoveAt(0)"), "예산 초과 시 가장 오래된 글로우 퇴거");
            Assert.That(src, Does.Contain("RemoveFromHierarchy()"), "종료/퇴경 정리 — 요소 제거(시작 시 생성/종료 시 제거 계약)");
        }

        [Test]
        public void PressGlowFx_BootstrapHook_SingleLineEnsureAfterClickRipple()
        {
            string src = System.IO.File.ReadAllText(BootstrapPath);
            int hookCount = CountOccurrences(src, "UTKPressGlow.Ensure();");
            Assert.That(hookCount, Is.EqualTo(1), "부트스트랩 훅은 정확히 1줄(중복 부착 금지 — Ensure 멱등)");
            int rippleIdx = src.IndexOf("UTKClickRipple.Ensure();", StringComparison.Ordinal);
            int hookIdx = src.IndexOf("UTKPressGlow.Ensure();", StringComparison.Ordinal);
            Assert.That(rippleIdx, Is.GreaterThanOrEqualTo(0), "물결 훅 존재(기존 계약 유지)");
            Assert.That(hookIdx, Is.GreaterThan(rippleIdx),
                "글로우 오버레이는 물결 오버레이 이후 부착(훅 순서 계약 — 물결은 기존 계약 무손상)");
        }

        [Test]
        public void PressGlowFx_NoVectorMeshUsage()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Not.Contain("CreateMesh"), "벡터메시 금지(84차 계약) — CreateMesh 부재");
            Assert.That(src, Does.Not.Contain("Mesh("), "벡터메시 금지(84차 계약) — Mesh( 부재");
        }

        /// <summary>문자열 발생 횟수 카운트 — 훅 중복 금지 계약용.</summary>
        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int idx = haystack.IndexOf(needle, StringComparison.Ordinal);
            while (idx >= 0)
            {
                count++;
                idx = haystack.IndexOf(needle, idx + needle.Length, StringComparison.Ordinal);
            }
            return count;
        }
    }
}
