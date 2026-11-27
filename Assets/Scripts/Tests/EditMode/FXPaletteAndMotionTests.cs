using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 28-U1/U3] 공용 팔레트 + 모션 언어 계약 — 숫자 검증 테스트(순수 함수·상수).
    /// 팔레트: 기존 단일 소스 위임 항목은 소스와 동일성, 리터럴 항목은 출처 리터럴과 일치.
    /// 모션: 3종 이징의 경계값·단조성·오버슈트 계약.
    /// </summary>
    public class FXPaletteAndMotionTests
    {
        private const float Eps = 1e-4f;

        // ===== T1 팔레트 위임 — CommandRingPresentation 단일 소스 동일성 =====

        [Test]
        public void Palette_CommandColors_DelegateToSingleSource()
        {
            Assert.That(FXPalette.MoveCommand, Is.EqualTo(CommandRingPresentation.MoveTargetColor).Within(Eps),
                "[무번호 28-U1] 이동 명령 청록은 CommandRingPresentation 단일 소스를 위임해야 한다(값 복제 금지).");
            Assert.That(FXPalette.SelectedGuard, Is.EqualTo(CommandRingPresentation.SelectedGuardColor).Within(Eps),
                "[무번호 28-U1] 선택 링 청록은 단일 소스 위임.");
            Assert.That(FXPalette.AttackCommand, Is.EqualTo(CommandRingPresentation.ExplicitTargetColor).Within(Eps),
                "[무번호 28-U1] 공격 명령 주황은 단일 소스 위임.");
            Assert.That(FXPalette.RallyCommand, Is.EqualTo(CommandRingPresentation.RallyColor).Within(Eps),
                "[무번호 28-U1] 집결 금은 단일 소스 위임(위조 배선 금지 — 상수만).");
        }

        // ===== T2 팔레트 리터럴 — 출처 리터럴 미러 일치 =====

        [Test]
        public void Palette_LiteralColors_MirrorSourceLiterals()
        {
            Assert.That(FXPalette.MonsterDanger, Is.EqualTo(new Color(1f, 0.30f, 0.16f, 0.45f)).Within(Eps),
                "[무번호 28-U1] 몬스터 위험 적색 — 18-B3 계약 리터럴.");
            Assert.That(FXPalette.Dust, Is.EqualTo(new Color(0.78f, 0.72f, 0.62f, 0.30f)).Within(Eps),
                "[무번호 28-U1] 먼지 흙색 — ShockwaveRingFX TrailDustColor(19-F1) 리터럴 미러.");
            Assert.That(FXPalette.ParryGold, Is.EqualTo(new Color(1f, 0.95f, 0.55f, 0.70f)).Within(Eps),
                "[무번호 28-U1] 패망 PERFECT 금 — PlayerCombat(20-J1) 리터럴 미러.");
            Assert.That(FXPalette.ParryCyan, Is.EqualTo(new Color(0.55f, 0.8f, 1f, 0.50f)).Within(Eps),
                "[무번호 28-U1] 패망 청 큐 — PlayerCombat(20-J1) 리터럴 미러.");
            Assert.That(FXPalette.SoldierRibbon, Is.EqualTo(new Color(0.75f, 0.92f, 1f, 1f)).Within(Eps),
                "[무번호 28-U1] 병사 리본 청백 — SoldierSwingTrail(26-ST1) 리터럴 미러.");
            Assert.That(FXPalette.MonsterAccent, Is.EqualTo(new Color(1f, 0.25f, 0.2f, 0.8f)).Within(Eps),
                "[무번호 28-U1] 몬스터 액센트 — AnimalAI(25-IN) 리터럴 미러.");
        }

        // ===== T3 Afterglow — easeInCubic 경계/정확값 =====

        [Test]
        public void Motion_Afterglow_EaseInCubic_Boundaries()
        {
            Assert.That(FXMotion.Afterglow(0f), Is.EqualTo(0f).Within(Eps), "[무번호 28-U3] Afterglow(0)=0.");
            Assert.That(FXMotion.Afterglow(0.5f), Is.EqualTo(0.125f).Within(Eps), "[무번호 28-U3] Afterglow(0.5)=0.125 — easeInCubic.");
            Assert.That(FXMotion.Afterglow(1f), Is.EqualTo(1f).Within(Eps), "[무번호 28-U3] Afterglow(1)=1.");
        }

        // ===== T4 Progress — smoothstep+임박 가속(단조·경계·가속 성질) =====

        [Test]
        public void Motion_Progress_Monotonic_WithLateAcceleration()
        {
            Assert.That(FXMotion.Progress(0f), Is.EqualTo(0f).Within(Eps), "[무번호 28-U3] Progress(0)=0.");
            Assert.That(FXMotion.Progress(1f), Is.EqualTo(1f).Within(Eps), "[무번호 28-U3] Progress(1)=1.");

            float prev = -1f;
            for (int i = 0; i <= 100; i++)
            {
                float v = FXMotion.Progress(i / 100f);
                Assert.That(v, Is.GreaterThanOrEqualTo(prev - Eps), "[무번호 28-U3] Progress는 단조 증가.");
                Assert.That(v, Is.LessThanOrEqualTo(1f + Eps), "[무번호 28-U3] Progress는 1.0을 넘지 않는다.");
                prev = v;
            }
            // 임박 가속 — 0.82 입력이 smoothstep 단독(≈0.914)보다 앞당겨진다(마지막 20% 조임).
            Assert.That(FXMotion.Progress(0.82f), Is.GreaterThan(0.9f),
                "[무번호 28-U3] 임박 가속 — 마지막 20% 구간이 smoothstep 단독보다 앞당겨진다(20-I3 계승).");
        }

        // ===== T5 SpawnPulse — easeOutBack 소폭(오버슈트 ≈8%) =====

        [Test]
        public void Motion_SpawnPulse_SmallOvershoot_Boundaries()
        {
            Assert.That(FXMotion.SpawnPulse(0f), Is.EqualTo(0f).Within(1e-3f), "[무번호 28-U3] SpawnPulse(0)=0.");
            Assert.That(FXMotion.SpawnPulse(1f), Is.EqualTo(1f).Within(Eps), "[무번호 28-U3] SpawnPulse(1)=1.");

            float max = float.MinValue;
            for (int i = 0; i <= 100; i++)
            {
                float v = FXMotion.SpawnPulse(i / 100f);
                if (v > max) max = v;
            }
            Assert.That(max, Is.GreaterThan(1.03f), "[무번호 28-U3] 오버슈트 존재(출현 펄스).");
            Assert.That(max, Is.LessThan(1.15f), "[무번호 28-U3] 오버슈트 소폭 계약(≈8% — 절제된 출현).");
        }

        // ===== T6 마커/결정론 =====

        [Test]
        public void Source_Markers_And_NoRandom()
        {
            string palette = System.IO.File.ReadAllText("Assets/Scripts/Systems/FXPalette.cs");
            string motion = System.IO.File.ReadAllText("Assets/Scripts/Systems/FXMotion.cs");
            Assert.That(palette, Does.Contain("[무번호 28-U1]"), "[무번호 28-U1] 팔레트 마커 존재.");
            Assert.That(motion, Does.Contain("[무번호 28-U3]"), "[무번호 28-U3] 모션 마커 존재.");
            Assert.That(motion, Does.Not.Contain("UnityEngine.Random"), "[무번호 28-U3] FXMotion은 결정론 — 난수 API 코드형 0건.");
        }
    }
}
