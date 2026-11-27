using System;
using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [클릭 빛물결 FX] 계약테스트 — UTKClickRipple + UIToolkitBootstrap 1줄 훅.
    /// 실제 물결 애니는 Play 영역 — EditMode에서는 로직/소스계약만 검증(기존 관례 동일).
    ///  ① 연출 상수 소스계약(0.45f 수명 · 0.25f 시작 스케일 · 0.85f 피크 · 96px · Every(16L) 드라이버 · 순백 0xFFFFFF)
    ///  ② 오버레이 계약 — pickingMode.Ignore + Position.Absolute + border-radius 원형(스타일링)
    ///  ③ 세대 토큰(연타/재부착 레이스 수리) + 동시 물결 예산(MaxRipples) 퇴거 경로
    ///  ④ 부트스트랩 1줄 훅 — Ensure() 내 UTKClickRipple.Ensure() 1회 존재(커서 오버레이 이후 부착)
    ///  ⑤ 벡터메시 금지(84차 계약) — CreateMesh/Mesh( 소스 부재
    /// </summary>
    public class UTKClickRippleTests
    {
        private const string SrcPath = "Assets/Scripts/UI/Toolkit/UTKClickRipple.cs";
        private const string BootstrapPath = "Assets/Scripts/UI/Toolkit/UIToolkitBootstrap.cs";

        [Test]
        public void ClickRippleFx_TimeConstants_RippleTimingsWhiteAndDriverPresent()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Contain("0.45f"), "물결 수명 0.45s(확산+페이드 총합)");
            Assert.That(src, Does.Contain("0.25f"), "시작 스케일 0.25(0.25 → 1.0 확산)");
            Assert.That(src, Does.Contain("0.85f"), "피크 알파 0.85(선형 페이드아웃 시작점)");
            Assert.That(src, Does.Contain("64f"), "최종 지름 64px([185차] 96→64 축소 — 사용자 요구)");
            Assert.That(src, Does.Contain("Every(16L)"), "16ms 애니메이션 드라이버(schedule.Execute().Every — CraftBench 선례)");
            Assert.That(src, Does.Contain("0xFF, 0xFF, 0xFF"), "순백 링 #FFFFFF(사용자 확정 — 중립적 클릭 발광)");
            Assert.That(src, Does.Contain("EaseOutQuad"), "확산 감속 이징 주석 핀(EaseOutQuad)");
        }

        [Test]
        public void ClickRippleFx_OverlayElement_IgnorePickingAbsolutePositionAndStyledCircle()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Contain("name = \"ClickRipple\", pickingMode = PickingMode.Ignore"),
                "물결 링 오버레이 — pickingMode Ignore(포인터 흡수 금지)");
            Assert.That(src, Does.Contain("style.position = Position.Absolute"),
                "절대좌표 오버레이(CraftBench 플래시 선례 동일)");
            Assert.That(src, Does.Contain("borderTopLeftRadius"), "원형 = border-radius 스타일링(좌상단)");
            Assert.That(src, Does.Contain("borderBottomRightRadius"), "원형 = border-radius 스타일링(우하단)");
            Assert.That(src, Does.Contain("WorldToLocal(evt.position)"), "패널 좌표 → 오버레이 로컬 변환(확장 메서드 수신자 선례)");
            Assert.That(src, Does.Contain("TrickleDown.TrickleDown"), "루트 TrickleDown 훅 — 모든 UI 요소 클릭 포착");
            Assert.That(src, Does.Contain("evt.target == _hookRoot"),
                "UI 전용 가드 — 빈 화면(월드) 클릭 무물결(UI 클릭 필요 계약)");
            Assert.That(src, Does.Contain("this.BringToFront()"),
                "스폰 시 오버레이 재선두 — 열린 창 위로 물결 렌더(커서는 자체 재선두 유지)");
        }

        [Test]
        public void ClickRippleFx_GenerationTokenAndBudget_RaceGuardsPresent()
        {
            string src = System.IO.File.ReadAllText(SrcPath);
            Assert.That(src, Does.Contain("_generation++"), "세대 토큰 선점(Ensure 재부착 시 증가 — 연타/재부착 레이스 수리)");
            Assert.That(src, Does.Contain("_myGeneration != _generation"),
                "드라이버 세대 불일치 가드 — 구세대 드라이버 자가 종료");
            Assert.That(src, Does.Contain("panel == null || ring.panel == null"),
                "탈착 조기 종료 가드(CraftBench 선례 동일)");
            Assert.That(src, Does.Contain("task.Pause()"), "드라이버 자가 정지(task.Pause)");
            Assert.That(src, Does.Contain("MaxRipples"), "동시 물결 예산 상수");
            Assert.That(src, Does.Contain("_active.RemoveAt(0)"), "예산 초과 시 가장 오래된 물결 퇴거");
            Assert.That(src, Does.Contain("RemoveFromHierarchy()"), "종료/퇴경 정리 — 요소 제거(시작 시 생성/종료 시 제거 계약)");
        }

        [Test]
        public void ClickRippleFx_BootstrapHook_SingleLineEnsureAfterCursorOverlay()
        {
            string src = System.IO.File.ReadAllText(BootstrapPath);
            int hookCount = CountOccurrences(src, "UTKClickRipple.Ensure();");
            Assert.That(hookCount, Is.EqualTo(1), "부트스트랩 훅은 정확히 1줄(중복 부착 금지 — Ensure 멱등)");
            int cursorIdx = src.IndexOf("UTKCursorOverlay.Ensure();", StringComparison.Ordinal);
            int hookIdx = src.IndexOf("UTKClickRipple.Ensure();", StringComparison.Ordinal);
            Assert.That(cursorIdx, Is.GreaterThanOrEqualTo(0), "커서 오버레이 훅 존재(기존 계약 유지)");
            Assert.That(hookIdx, Is.GreaterThan(cursorIdx),
                "물결 오버레이는 커서 오버레이 이후 부착(부착 순서 계약 — Ensure 훅 위치)");
            int bowIdx = src.IndexOf("BowAimReticleUTK.Ensure();", StringComparison.Ordinal);
            Assert.That(hookIdx, Is.GreaterThan(bowIdx), "리티클 훅 다음 위치(훅 순서 무손상)");
        }

        [Test]
        public void ClickRippleFx_NoVectorMeshUsage()
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
