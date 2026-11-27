using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 29-S2/P2] 발자국·패링 디테일 계약 — S1-2 스텝 동기/S2-2 강도 연동/P1-2 무기 림/
    /// P2-2 궤적 차등/P3-2 공격자 림. 잠금 시그니처 보존(슬롯 패턴) + 폴백/결정론 검증.
    /// </summary>
    public class FxDetailS2P2Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── S1-2: 애니 시스템 조회만(무수정) + 결정론 매핑 ──
        [Test]
        public void Source_S1_2_StepSyncReadOnly()
        {
            string src = Read("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.That(src, Does.Contain("[무번호 29-S1-2]"), "[29-S1-2] 마커.");
            Assert.That(src, Does.Contain("_rigAnim.CurrentSpeed"),
                "[29-S1-2] 드라이버 CurrentSpeed 조회(애니 시스템 무수정 — 계획 계약).");
            Assert.That(src, Does.Contain("_stepFlip = !_stepFlip;"),
                "[29-S1-2] 기존 28-S1 교차 배선 무손상(잠금 보존).");
            Assert.That(src, Does.Contain("animSpeed / _moveSpeed"),
                "[29-S1-2] 속도비 결정론 매핑.");
        }

        // ── S2-2: 강도 슬롯(잠금 시그니처 보존) + 3연동 지점 ──
        [Test]
        public void Source_S2_2_IntensityCoupling()
        {
            string src = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.That(src, Does.Contain("public static void SetNextPuffIntensity(float intensity)"),
                "[29-S2-2] 강도 슬롯 API.");
            Assert.That(src, Does.Contain("_pendingPuffIntensity = 1f;"),
                "[29-S2-2] 단발 소비(슬롯 리셋 — 오염 방지).");
            Assert.That(src, Does.Contain("0.08f * k, 0.22f * k"),
                "[29-S2-2] 시작 크기 강도 비례.");
            Assert.That(src, Does.Contain("Mathf.Clamp(Mathf.RoundToInt(PuffParticleCount * k), 3, 10)"),
                "[29-S2-2] 입자 수 강도 비례(3~10 클램프 — 성능 상한).");
            Assert.That(src, Does.Contain("SpawnMoveTrailPuffCore(groundPos, kArr);"),
                "[29-S2-2] 도착 강도 = 강도 비례 입자 레이어(링 반경/수명 계약 무손상 — IN1 잠금 보존 설계).");
            string guard = Read("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.That(guard, Does.Contain("SetNextPuffIntensity(Mathf.Clamp(0.8f + (t - here).sqrMagnitude / 36f"),
                "[29-S2-2] 시작 킥 = 거리 비례(가속 연동).");
            Assert.That(guard, Does.Contain("SetNextPuffIntensity(Mathf.Clamp(0.7f + _lastFrameSpeed / 3.5f"),
                "[29-S2-2] 도착 = 직전 속도 비례(감속 연동 — approach 계승).");
            Assert.That(guard, Does.Contain("private float _lastFrameSpeed;"),
                "[29-S2-2] 속도 추적 필드.");
        }

        // ── S1-3: 발자국 먼지 소프트 파티클(D-2 경유 — DustPuffMat 스왑으로 충족) ──
        [Test]
        public void Source_S1_3_SoftDustViaD2()
        {
            string src = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.That(src, Does.Contain("FxMaterialLibrary.CreateSoftParticle(\"DustPuffMat\""),
                "[29-S1-3] 발자국 퍼프 머티리얼이 소프트 파티클(D-2) 경유 — 지면 관통 제거.");
        }

        // ── P1-2: 무기 플래시 림 + 캡처/원복 계약 보존 ──
        [Test]
        public void Source_P1_2_WeaponFlashRim()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("[무번호 29-P1-2]"), "[29-P1-2] 마커.");
            Assert.That(src, Does.Contain("FxRimOverlay.AttachRimStatic(visual, FXPalette.ParryWhite, 2.5f, 1.6f)"),
                "[29-P1-2] 무기 림 = ParryGold 단일 소스.");
            Assert.That(src, Does.Contain("if (rim != null) FxRimOverlay.Release(rim);"),
                "[29-P1-2] 림 해제(누출 금지).");
            Assert.That(src, Does.Contain("mat.SetColor(\"_EmissionColor\", cap.emission);   // 캡처값 원복 — 기존 발광 무손상"),
                "[29-P1-2] 기존 캡처/원복 계약 무손상.");
            string fx = Read("Assets/Scripts/Systems/FxRimOverlay.cs");
            Assert.That(fx, Does.Contain("public static GameObject AttachRimStatic(GameObject sourceVisual"),
                "[29-P1-2] 정적 메시 림 오버로드 존재.");
        }

        // ── P2-2: 등급 궤적 차등 + 기존 리터럴 보존 ──
        [Test]
        public void Source_P2_2_GradeArc()
        {
            string src = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.That(src, Does.Contain("[무번호 29-P2-2]"), "[29-P2-2] 마커.");
            Assert.That(src, Does.Contain("public static void SetNextReflectPerfect(bool perfect)"),
                "[29-P2-2] 등급 슬롯 API(잠금 시그니처 보존 설계).");
            Assert.That(src, Does.Contain("(0.06f - 0.10f * Mathf.Abs(spread))"),
                "[29-P2-2] 기존 GOOD 리프트 리터럴 무손상(28-P2 잠금 보존).");
            Assert.That(src, Does.Contain("+ (_perfectArc ? 0.05f : 0f)"),
                "[29-P2-2] PERFECT 상승 보정(더 높은 바운스).");
            Assert.That(src, Does.Contain("_lengthMul = _perfectArc ? 1.25f : 1f;"),
                "[29-P2-2] PERFECT 호 길이 ×1.25(더 긴 궤적).");
            Assert.That(src, Does.Contain("_perfectArc = reflect && ConsumeReflectPerfect();"),
                "[29-P2-2] 리플렉 전용 소비(기본 아크 무영향).");
            string pc = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            // [176차 갱신] 패막 경로 리플렉 궤적 철수 — 방사형 산란 제거(원형 링만 유지).
            //   SetNextReflectPerfect/SpawnParryReflectWisps 정의(잠금 시그니처)는 무손상 — 호출부 소멸만 검증.
            Assert.That(pc.Split(new[] { "SetNextReflectPerfect(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 등급 슬롯 배선 철수 — 리플렉 궤적 소멸");
            Assert.That(pc.Split(new[] { "ShockwaveRingFX.SpawnParryReflectWisps(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 리플렉 궤적 호출 0건 — 방사형 산란 제거");
        }

        // ── P3-2: 공격자 백색 림 + M1 상충 가드 ──
        [Test]
        public void Source_P3_2_AttackerWhiteRim()
        {
            string src = Read("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.That(src, Does.Contain("[무번호 29-P3-2]"), "[29-P3-2] 마커.");
            Assert.That(src, Does.Contain("if (_chargeRim == null)"),
                "[29-P3-2] M1 충전 림 상충 가드 유지.");
            Assert.That(src, Does.Contain("AttachRim(rimSmr, Color.white, 2.2f, 1.8f)"),
                "[29-P3-2] 백색 림(실루엣 반전).");
            Assert.That(src, Does.Contain("if (whiteRim != null) FxRimOverlay.Release(whiteRim);"),
                "[29-P3-2] 림 해제(누출 금지).");
        }

        // ── 전체 결정론 스윕 ──
        [Test]
        public void Source_S2P2_AllDeterministic()
        {
            Assert.That(Read("Assets/Scripts/Systems/ShockwaveRingFX.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[29-S2/P2] ShockwaveRingFX Random 0건.");
            Assert.That(Read("Assets/Scripts/Systems/GuardPlaceholder.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[29-S2/P2] GuardPlaceholder Random 0건.");
            Assert.That(Read("Assets/Scripts/Systems/PlayerCombat.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[29-S2/P2] PlayerCombat Random 0건.");
        }
    }
}
