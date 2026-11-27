using NUnit.Framework;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 28-P1/P2/P3] 패망 피드백 확장 — PlayerCombat + AnimalAI 소스 계약(Source 잠금).
    /// P1 무기 본체 플래시(캡처→펄스→원복) / P2 리플렉 궤적 배선(wave2) / P3 공격자 백색 반전 배선.
    /// </summary>
    public class ParryWeaponFlashPhasePTests
    {
        private static string ReadPc() => System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
        private static string ReadAi() => System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");

        [Test]
        public void Source_WeaponFlash()
        {
            string pc = ReadPc();
            Assert.That(pc, Does.Contain("private System.Collections.IEnumerator WeaponFlashPulseRoutine()"),
                "[무번호 28-P1] 무기 플래시 코루틴이 존재해야 한다.");
            Assert.That(pc, Does.Contain("WeaponEquipManager.CurrentVisual"),
                "[무번호 28-P1] 무기 비주얼은 CurrentVisual 단일 경유(105차 F1 계약 노출).");
            Assert.That(pc, Does.Contain("FXPalette.ParryWhite"),
                "[무번호 28-P1] 플래시 색은 팔레트 단일 소스(로컬 리터럴 금지).");
            // 부정 잠금은 루틴 "정의부" 앵커 한정 — 첫 언급(주석)이나 파일 전문은 기존 J1 흰 링 리터럴(L773/L1089)을 오탐한다(100차 함정).
            int rIdx = pc.IndexOf("private System.Collections.IEnumerator WeaponFlashPulseRoutine()");
            Assert.That(rIdx, Is.GreaterThanOrEqualTo(0), "[무번호 28-P1] 루틴 정의 시그니처 존재.");
            string block = pc.Substring(rIdx);
            Assert.That(block.Substring(0, System.Math.Min(block.Length, 2200)), Does.Not.Contain("new Color(1f, 0.95f, 0.55f"),
                "[무번호 28-P1] 플래시 루틴 내 ParryGold 로컬 리터럴 복제 금지(28-U1 위임 계약).");
        }

        [Test]
        public void Source_FlashWave0()
        {
            string pc = ReadPc();
            Assert.That(pc, Does.Contain("if (Application.isPlaying) StartCoroutine(WeaponFlashPulseRoutine());"),
                "[무번호 28-P1] wave0 블록에서 무기 플래시 배선(Play 전용 가드).");
            Assert.That(pc, Does.Contain("[무번호 28-P1]"), "[무번호 28-P1] 마커 존재.");
        }

        [Test]
        public void Source_ReflectWiring()
        {
            string pc = ReadPc();
            // [176차 갱신] wave2 리플렉 궤적 철수 — 방사형 산란 제거(사용자 지시: 원형 링만 유지).
            //   SpawnParryReflectWisps 정의(잠금 시그니처)는 무손상 — 호출부 소멸만 검증.
            Assert.That(pc.Split(new[] { "ShockwaveRingFX.SpawnParryReflectWisps(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 리플렉 궤적 호출 0건 — 방사형 산란 제거");
        }

        [Test]
        public void Source_AttackerFlash()
        {
            string pc = ReadPc();
            string ai = ReadAi();
            Assert.That(ai, Does.Contain("public void PlayParryFlash()"),
                "[무번호 28-P3] AnimalAI 공격자 플래시 진입점 존재.");
            Assert.That(ai, Does.Contain("[무번호 28-P3]"), "[무번호 28-P3] 마커 존재.");
            int knockIdx = pc.IndexOf("ApplyPerfectParryAttackerKnockback(kbDir, attacker);");
            int flashIdx = pc.IndexOf("attacker.GetComponent<AnimalAI>()");
            Assert.That(knockIdx, Is.GreaterThanOrEqualTo(0), "[무번호 28-P3] 기존 넉백 배선 무손상.");
            Assert.That(flashIdx, Is.GreaterThan(knockIdx),
                "[무번호 28-P3] 플래시 배선은 넉백 직후 위치(PERFECT 블록 내).");
        }

        [Test]
        public void Source_WhitePulse()
        {
            string ai = ReadAi();
            Assert.That(ai, Does.Contain("1.8f"), "[무번호 28-P3] 백색 반전 강도(white ×1.8) 존재.");
            Assert.That(ai, Does.Contain("WaitForSecondsRealtime"),
                "[무번호 28-P3] 플래시 복귀는 unscaled 시간(히트스톱 프리징 중에도 복귀 — X1 선례).");
        }

        [Test]
        public void Source_NoRandom_InNewBlocks()
        {
            string pc = ReadPc();
            string ai = ReadAi();
            // [무번호 28-P] 신규 블록 결정론 — 코드형 난수 0건(파일 전체가 아닌 신규 심볼 주변 한정)
            int i1 = pc.IndexOf("WeaponFlashPulseRoutine");
            Assert.That(i1, Is.GreaterThanOrEqualTo(0));
            string block = pc.Substring(i1);
            Assert.That(block.Substring(0, System.Math.Min(block.Length, 3000)), Does.Not.Contain("UnityEngine.Random"),
                "[무번호 28-P1] 무기 플래시 블록 결정론.");
            int i2 = ai.IndexOf("PlayParryFlash");
            Assert.That(i2, Is.GreaterThanOrEqualTo(0));
            string block2 = ai.Substring(i2);
            Assert.That(block2.Substring(0, System.Math.Min(block2.Length, 2500)), Does.Not.Contain("UnityEngine.Random"),
                "[무번호 28-P3] 공격자 플래시 블록 결정론.");
        }
    }
}
