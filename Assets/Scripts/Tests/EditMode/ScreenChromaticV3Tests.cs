using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 31-V3] 크로매틱 수차 스파이크 계약 — 화면 계층 마지막 조각(무번호 23 계획서 V3 전환 구현).
    /// 런타임 Volume 기반(포스트 신규 구축 금지 — IndoorPostProcessController 선례), 인스턴스 재사용, 결정론 검증.
    /// </summary>
    public class ScreenChromaticV3Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── V3: 스파이크 발화부 — 가드/볼륨/프로필 ──
        [Test]
        public void Source_V3_SpikeEntry()
        {
            string src = Read("Assets/Scripts/Systems/ScreenChromaticFX.cs");
            Assert.That(src, Does.Contain("[무번호 31-V3]"), "[31-V3] 마커.");
            Assert.That(src, Does.Contain("public static void Spike(Camera cam, float peakIntensity, float totalDuration)"),
                "[31-V3] 스파이크 시그니처 계약.");
            Assert.That(src, Does.Contain("if (!Application.isPlaying || cam == null) return;"),
                "[31-V3] EditMode 무동작 가드 + null 카메라 가드.");
            Assert.That(src, Does.Contain("catch (System.Exception) { }"),
                "[31-V3] 시각 보조 try/catch — 전투 방해 금지(23-V 선례).");
            Assert.That(src, Does.Not.Contain("Random"), "[31-V3] 결정론 — Random 0건(신규 파일 전문).");
        }

        // ── V3: 런타임 Volume + ChromaticAberration 오버라이드(IndoorPostProcessController 선례) ──
        [Test]
        public void Source_V3_RuntimeVolume()
        {
            string src = Read("Assets/Scripts/Systems/ScreenChromaticFX.cs");
            Assert.That(src, Does.Contain("ScriptableObject.CreateInstance<VolumeProfile>()"),
                "[31-V3] 런타임 프로필 생성 — 에셋 파일 없이 코드 상수가 진실원.");
            Assert.That(src, Does.Contain("_profile.Add<ChromaticAberration>(true)"),
                "[31-V3] ChromaticAberration 오버라이드만 추가(기존 프로필 무접촉).");
            Assert.That(src, Does.Contain("_volume.isGlobal = true;"), "[31-V3] 글로벌 볼륨.");
            Assert.That(src, Does.Contain("_volume.priority = 10f;"),
                "[31-V3] 우선순위 10 — 기존 글로벌 볼륨(기본 0) 위에서 확정 적용.");
            Assert.That(src, Does.Contain("_ca.intensity.Override(0f);"),
                "[31-V3] 종료 시 0 복귀 — 잔존 수차 금지.");
        }

        // ── V3: 인스턴스 재사용(누적 금지) + unscaled + 누출 금지 ──
        [Test]
        public void Source_V3_RunnerLifecycle()
        {
            string src = Read("Assets/Scripts/Systems/ScreenChromaticFX.cs");
            Assert.That(src, Does.Contain("if (_runner == null)"),
                "[31-V3] 인스턴스 1개 재사용 — 오브젝트 누적 금지.");
            Assert.That(src, Does.Contain("public void Restart(float peakIntensity, float totalDuration)"),
                "[31-V3] 스파이크 중 재호출 = 타이머 리스타트.");
            Assert.That(src, Does.Contain("Time.unscaledDeltaTime"),
                "[31-V3] unscaled 시간 — 히트스톱 중에도 재생(VignettePulse 선례).");
            Assert.That(src, Does.Contain("public const float EntrySeconds = 0.02f;"),
                "[31-V3] 진입 0.02s 램프(계획서 고정).");
            Assert.That(src, Does.Contain("if (_profile != null) Object.Destroy(_profile);"),
                "[31-V3] OnDestroy 프로필 파괴 — 누출 금지.");
            Assert.That(src, Does.Contain("enabled = false;"),
                "[31-V3] 스파이크 완료 = Update 정지(인스턴스 유지 재사용).");
        }

        // ── V3: 패링 PERFECT 훅 — [176차 갱신] 크로매틱 스파이크 철수(RGB 프린지 = 청백 왜곡 원인) ──
        [Test]
        public void Source_V3_ParryHook_Removed_176()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            int hookCount = System.Text.RegularExpressions.Regex.Matches(src, "ScreenChromaticFX\\.Spike").Count;
            Assert.That(hookCount, Is.EqualTo(0),
                "[176차] 패막 경로 ScreenChromaticFX.Spike 발화 0건 — RGB 프린지(청백 감각) 제거, 완벽 패막 화면 순백 계약.");
            Assert.That(src, Does.Contain("StartCoroutine(WeaponFlashPulseRoutine());"),
                "[31-V3] 기존 28-P1 무기 플래시 경로 무손상.");
            Assert.That(src, Does.Contain("HitStopManager.RequestHitStop(grade == ParryGrade.Perfect ? 0.07f : 0.03f, 0.12f);"),
                "[31-V3] 기존 159차 히트스톱 경로 무손상.");
        }
    }
}
