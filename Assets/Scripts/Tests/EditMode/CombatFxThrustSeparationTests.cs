using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [176차] 창(스피어) 콤보 계약 — 소스 어서션(AttackSwingJudgmentContractTests 소스 grep 선례).
    /// 사용자 정정(11-23): 창에는 "찌르기 동작"이 없다(AttackThrust는 명칭뿐, 클립은 2스윙).
    /// 1클릭=1타(스윙1), 2클릭=2타(팔로업 WeaponCombo 스윙2)로 콤보 분리 + 양 타 모두 흰색 틴트 슬래시.
    /// [174차]에 신설된 찌르기 전용 FX(FireComboThrust → PlayThrustStage) 라우팅은 철회 — 정의는 추후 재사용을 위해 존속.
    /// </summary>
    public class CombatFxThrustSeparationTests
    {
        private const string DriverPath = "Assets/Scripts/Systems/HumanoidClipDriver.cs";
        private const string RunnerPath = "Assets/Scripts/Systems/SlashVFXRunner.cs";

        /// <summary>부분 문자열 개수 카운터 — 발화 횟수 계약 판정용.</summary>
        private static int CountOf(string haystack, string needle)
        {
            int count = 0, idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }

        // ── ① 창 경로 = 슬래시 아크(찌르기 FX 철회) ──

        [Test]
        public void Driver_SpearPath_DoesNotCallThrustFx()
        {
            var src = System.IO.File.ReadAllText(DriverPath);
            Assert.That(src, Does.Not.Contain("FireComboThrust(1)"),
                "[176차] 스윙1은 슬래시 아크(FireComboSlash)로 발화되어야 한다 — 찌르기 FX 라우팅 철회.");
            Assert.That(src, Does.Not.Contain("FireComboThrust(2)"),
                "[176차] 스윙2 자동 발화 게이트가 철수되어 FireComboThrust(2) 호출이 남아 있으면 안 된다.");
        }

        [Test]
        public void Driver_SpearOpener_FiresSlashPerSwing()
        {
            var src = System.IO.File.ReadAllText(DriverPath);
            Assert.That(CountOf(src, "FireComboSlash(1)"), Is.GreaterThanOrEqualTo(2),
                "오프너 스윙1 발화부(시간 게이트+상태 게이트 이중 가드) 2곳 모두 슬래시 아크를 발화해야 한다.");
            Assert.That(src, Does.Contain("FireComboSlash(2)"),
                "팔로업(WeaponCombo 스윙2)도 슬래시 아크를 발화해야 한다.");
            Assert.That(src, Does.Contain("PlayThrustStage"),
                "PlayThrustStage 정의(SlashVFXRunner)·FireComboThrust 정의는 추후 재사용을 위해 존속한다.");
        }

        // ── ② 2타 = 2클릭 전용(자동 발화 게이트 철수) ──

        [Test]
        public void Driver_SpearSwing2_IsSecondClickGated()
        {
            var src = System.IO.File.ReadAllText(DriverPath);
            Assert.That(src, Does.Not.Contain("_spearOpenerResolveAtTime2"),
                "[176차] 오프너 내장 2스윙 자동 시간 게이트가 철수되어야 한다 — 1클릭으로 2타까지 나가지 않게.");
            Assert.That(src, Does.Not.Contain("ShouldResolveSpearOpenerSwing2(Mathf.Max"),
                "[176차] 스윙2 상태 게이트의 호출부가 철수되어야 한다(판정 함수 정의는 존속).");
            Assert.That(src, Does.Contain("ShouldResolveSpearOpenerSwing2"),
                "판정 함수 정의는 단위테스트(HumanoidClipDriverComboTests 리플렉션 핀)를 위해 존속한다.");
            Assert.That(src, Does.Contain("ShouldResolveSpearFollowupHit"),
                "2타 해결은 2클릭 승인 팔로업 게이트(ShouldResolveSpearFollowupHit) 단일 소스여야 한다.");
            Assert.That(src, Does.Contain("_spearAcceptedClicks = 2"),
                "2클릭 수용 경로(_spearAcceptedClicks = 2)가 존재해야 한다 — 콤보 2타 진입 전제.");
        }

        // ── ③ 양 타 흰색 틴트 ──

        [Test]
        public void SlashStageTint_IsAlwaysWhite_ForBothSpearStages()
        {
            var src = System.IO.File.ReadAllText(RunnerPath);
            Assert.That(src, Does.Contain("=> CoreTint; // 항상 흰색"),
                "ComboStageTint는 모든 스테이지에서 흰색(CoreTint)을 반환해야 한다 — 사용자 지시: 창 1타·2타 동일 흰색 틴트.");
        }

        // ── ④ 기존 계약 문자열 존속 ──

        [Test]
        public void Driver_LegacyContractStrings_Preserved()
        {
            var src = System.IO.File.ReadAllText(DriverPath);
            Assert.That(CountOf(src, "[166-C]"), Is.GreaterThanOrEqualTo(2),
                "[166-C] 스윙별 Slash VFX 발화 마커 주석이 양 발화부에 유지되어야 한다(계약 문자열 소멸 금지).");
            Assert.That(src, Does.Contain("FireComboSlash(swing + 1)"),
                "레거시 Attack* 경로도 스윙별 Slash VFX를 발화해야 한다.");
            Assert.That(src, Does.Contain("_combat?.ResolveComboSwingHit(2)"),
                "스윙 2 판정(팔로업 경로)이 존재해야 한다.");
        }

        // ── ⑤ 쿨다운 ≤ 스테이지 간격(부등식 계약 — 174차 실측 승계) ──

        [Test]
        public void SlashCooldown_IsBelow_MinComboStageGap()
        {
            var src = System.IO.File.ReadAllText(RunnerPath);
            Assert.That(src, Does.Contain("MIN_STYLIZED_SLASH_INTERVAL = 0.25f"),
                "슬래시 공용 쿨다운 0.25s — 174차 실측(오프너 최악 간격 0.54s·근접 세그먼트 0.76s) 이하.");
            Assert.That(src, Does.Contain("MinComboStageGapSeconds"),
                "MinComboStageGapSeconds(0.54) 부등식 근거 주석이 유지되어야 한다.");
        }
    }
}
