using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 27-P4] heat 정규화 측정 이원화 진단 — 소스 계약 테스트(진단 전용 라운드, 행동 변경 금지 잠금).
    /// 스펙: /tmp/p4_brief.md §2~§3. 대상: Assets/Scripts/Systems/TestPlayerAnimatorBoot.cs.
    /// ①정규화 직전 heat 이원 실측(월드 bounds vs localBounds 두 호출 + 대비 로그)
    /// ②정규화 후 실측 로그(스키닝 1프레임 경과, 계약 1.8m 대비 편차)
    /// ③NormalizeSoldierHeight 호출 원형 유지(진단 전용 원칙 — 인자 변경 금지 잠금)
    /// ④heatSwapped 사후 재실측 게이트 존재 ⑤[무번호 27-P4] 마커 존재.
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class HeatNormalizeDiagPhaseP4Tests
    {
        private static string ReadSource() => System.IO.File.ReadAllText("Assets/Scripts/Systems/TestPlayerAnimatorBoot.cs");

        // ===== T1 — 정규화 직전 이원 실측(월드/로컬 양발 측정 + 대비 로그) =====

        [Test]
        public void Source_DualMeasureLog()
        {
            string src = ReadSource();
            Assert.That(src, Does.Contain("[27-P4] heat 키 이원 실측(정규화 전)"),
                "[무번호 27-P4] 정규화 직전 heat 이원 실측 로그가 존재해야 한다.");
            Assert.That(src, Does.Contain("MeasureStandingHeight(heat, skinnedLocalBounds: false)"),
                "[무번호 27-P4] 이원 실측 월드 bounds 판(즉시 판) 호출이 존재해야 한다.");
            Assert.That(src, Does.Contain("MeasureStandingHeight(heat, skinnedLocalBounds: true)"),
                "[무번호 27-P4] 이원 실측 localBounds(바인드 포즈) 호출이 존재해야 한다.");
        }

        // ===== T2 — 정규화 후 실측 로그(오측 판별 증거) =====

        [Test]
        public void Source_PostMeasureLog()
        {
            string src = ReadSource();
            Assert.That(src, Does.Contain("[27-P4] heat 정규화 후 실측"),
                "[무번호 27-P4] 정규화 후 실측 로그(1프레임 경과, 계약 1.8m 대비 편차)가 존재해야 한다.");
        }

        // ===== T3 — NormalizeSoldierHeight 호출 원형 유지(행동 변경 금지 잠금) =====

        [Test]
        public void Source_NormalizeCallUnchanged()
        {
            string src = ReadSource();
            Assert.That(src, Does.Contain("NormalizeSoldierHeight(heat, TerritoryBuilder.SoldierTargetHeight, skinnedLocalBounds: false)"),
                "[무번호 27-P4] NormalizeSoldierHeight 호출 인자 원형 유지(skinnedLocalBounds: false) — 진단 전용 라운드에서 수리/변경 금지 잠금.");
        }

        // ===== T4 — 사후 재실측 게이트(heatSwapped) =====

        [Test]
        public void Source_PostGate()
        {
            string src = ReadSource();
            Assert.That(src, Does.Contain("bool heatSwapped = false;"),
                "[무번호 27-P4] heatSwapped 플래그 선언(Boot 초반 false)이 존재해야 한다 — 컴파일 경로 안전.");
            Assert.That(src, Does.Contain("heatSwapped = true;"),
                "[무번호 27-P4] heat 리그 교체 실시 시점의 true 설정이 존재해야 한다.");
            Assert.That(src, Does.Contain("if (heatSwapped && model != null)"),
                "[무번호 27-P4] 사후 재실측 게이트(heatSwapped && model != null)가 존재해야 한다.");
        }

        // ===== T5 — 라운드 마커 =====

        [Test]
        public void Source_Marker()
        {
            string src = ReadSource();
            Assert.That(src, Does.Contain("[무번호 27-P4]"),
                "[무번호 27-P4] 진단 삽입부 라운드 마커가 존재해야 한다.");
        }
    }
}
