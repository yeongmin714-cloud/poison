using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [205차 배치B-2] 패막 화면 순백 계약테스트 — 청백 왜곡 수리 핀(소스 문자열 매칭).
    /// 근원 실측: 화이트 프레임(ParryFlashFader 쿼드)이 피크 알파 0.85(<1.0)+삼각 램프로 어떤 프레임도
    /// 오파크에 도달하지 못해 Sprites/Default 알파 블렌드상 씬 블리딩(15%+)이 항상 잔존 — 청톤 씬에서
    /// 화면 플래시가 청백으로 보임(사용자 재보고, 176차 크로매틱 수차 철수 후에도 잔존한 별개 경로).
    /// 수리: 피크 1.0(오파크 = 씬 기여 계수 0, 순백 수학적 보장) + ParryFlashFader 피크 홀드 신설.
    /// 링/림/월드 FX(ShockwaveRingFX·FxRimOverlay·ParryAssetFX)와 FXPalette.ParryWhite 단일 소스는 무손상.
    /// </summary>
    public class ParryWhite205Tests
    {
        private const string PlayerCombatPath = "Assets/Scripts/Systems/PlayerCombat.cs";
        private const string FaderPath = "Assets/Scripts/Systems/ParryFlashFader.cs";
        private const string VignettePath = "Assets/Scripts/Systems/ScreenVignetteFX.cs";
        private const string PalettePath = "Assets/Scripts/Systems/FXPalette.cs";

        // ── T1. [205차] SpawnParryScreenFlash → ParryFlashFader 순백 전달 핀 ──
        [Test]
        public void T1_SpawnParryScreenFlash_Delivers_PureWhite_OpaquePeak()
        {
            var combat = System.IO.File.ReadAllText(PlayerCombatPath);
            // 피크 1.0 — 오파크 프레임에서 합성색 = src.rgb(순백) 그대로, 씬 기여 0%(수학적 보장).
            Assert.That(combat, Does.Contain("fader.Init(Color.white, 1f, 0.07f)"),
                "[205차] 화이트 프레임 피크 1.0(오파크 순백) 전달 — 구 0.85는 씬 블리딩 청백 왜곡 근원.");
            Assert.That(combat, Does.Contain("[205차] 잔여 청백 왜곡 근원 특정"),
                "[205차] 수리 근거 주석 마커 존재.");

            var fader = System.IO.File.ReadAllText(FaderPath);
            // 피크 홀드 — 프레임 샘플링(60fps 스텝≈0.24)이 피크를 관통하지 못해도 최소 1프레임 오파크 보장.
            Assert.That(fader, Does.Contain(": t < 0.55f"),
                "[205차] 피크 홀드 구간(25~55%) 신설 — 삼각 램프의 피크 미관통 문제 제거.");
            Assert.That(fader, Does.Contain("? _peakAlpha"),
                "[205차] 홀드 분기가 _peakAlpha 그대로 유지(오파크 프레임 보장).");
            Assert.That(fader, Does.Contain("[205차] 0→피크(전 25%)→피크 홀드"),
                "[205차] ParryFlashFader 수리 근거 주석 마커 존재.");
            // 기존 Phase D 계약 무손상 — unscaled 시간(히트스톱 동기 물림) 유지.
            Assert.That(fader, Does.Contain("Time.unscaledDeltaTime"),
                "[205차] unscaled 시간 기준 무손상(Phase D 계약).");
        }

        // ── T2. [205차] 화면 경로(수리된 3개 파일) 시안 계열 색 상수 부재 핀 ──
        [Test]
        public void T2_ScreenPathFiles_Have_No_CyanFamily_ColorConstants()
        {
            var sources = new[]
            {
                System.IO.File.ReadAllText(PlayerCombatPath),
                System.IO.File.ReadAllText(FaderPath),
                System.IO.File.ReadAllText(VignettePath),
            };
            // 시안 계열 리터럴: 저R·고G·B=1 (예: (0.55,0.8,1) 청 큐 링 계열) — 화면 플래시 경로 금지.
            var cyanB1 = new System.Text.RegularExpressions.Regex(
                @"new Color\(0\.\d+f,\s*0\.[5-9]\d*f,\s*1f");
            // 청록(틸) 계열 리터럴: 저R·고G·고B<1 (예: (0.16,0.85,0.78) 패러독스 정지 비네트 계열).
            var teal = new System.Text.RegularExpressions.Regex(
                @"new Color\(0\.1\d*f,\s*0\.8\d*f,\s*0\.[6-9]\d*f");
            foreach (var src in sources)
            {
                Assert.IsFalse(cyanB1.IsMatch(src),
                    "[205차] 화면 경로에 시안 계열 색 상수(B=1) 신설 금지 — 패막 플래시는 순백 단일.");
                Assert.IsFalse(teal.IsMatch(src),
                    "[205차] 화면 경로에 청록(틸) 계열 색 상수 신설 금지.");
                Assert.That(src, Does.Not.Contain("ParryCyan"),
                    "[205차] 팔레트 청색(ParryCyan)이 화면 플래시 경로 파일에 누출 금지 — 월드 링 전용.");
            }
            // 대조군: 화면 비네트 피격색(적)은 의도된 틴트 — 존재 유지(오검사 방지 샘플).
            Assert.That(sources[2], Does.Contain("new Color(0.9f, 0.12f, 0.1f)"),
                "[205차] 피격 적색 비네트는 의도색 — 시안 스캔이 오탐하지 않는지 대조 핀.");
        }

        // ── T3. [205차] 링/림 경로 무손상 핀 + ParryWhite 단일 소스 불변 핀 ──
        [Test]
        public void T3_RingRimWorldFX_And_ParryWhite_Source_Intact()
        {
            var combat = System.IO.File.ReadAllText(PlayerCombatPath);
            // 지면 링 2겹 + 아우터 + 수렴-역전 링 + GOOD 미니링 + 트리플 링 — [176차] wave0 원형 계열 무손상.
            Assert.That(combat, Does.Contain("ShockwaveRingFX.Spawn(groundP"),
                "[205차] 지면 링 파동 무손상(링/림 FX 유지 계약).");
            Assert.That(combat, Does.Contain("ShockwaveRingFX.SpawnParryConvergeRing(p, 1.6f, ringColor)"),
                "[205차] 수렴-역전 링 무손상.");
            Assert.That(combat, Does.Contain("ParryThirdRingRoutine"),
                "[205차] 트리플 링 3박 무손상.");
            Assert.That(combat, Does.Contain("FxRimOverlay.AttachRimStatic"),
                "[205차] 패링 가드/무기 림 오버레이 무손상.");
            Assert.That(combat, Does.Contain("ParryAssetFX.Spawn"),
                "[205차] 보유 에셋 FX(Guard_01) 무손상.");
            // 비네트/라이트/블룸 — [176차] 순백 계약 그대로(ParryWhite rgb·무채색 블룸 강도).
            Assert.That(combat, Does.Contain("ScreenVignetteFX.Begin(vc, new Color(FXPalette.ParryWhite.r"),
                "[205차] PERFECT 흰 비네트 배선 무손상(176차 계약).");
            Assert.That(combat, Does.Contain("FxImpactLight.Spike(transform.position + transform.forward * 1f + Vector3.up * 0.9f,"),
                "[205차] 패망 흰 라이트 배선 무손상.");
            Assert.That(combat, Does.Contain("ScreenBloomFX.Spike(vc, 2.2f, 0.12f)"),
                "[205차] 블룸 임팩트 펄스(무채색 강도) 무손상.");

            // ParryWhite 단일 소스(171차) — 값 불변 핀(알파 0.85는 팔레트 정의값, 화면 프레임 피크와 별개).
            var palette = System.IO.File.ReadAllText(PalettePath);
            Assert.That(palette, Does.Contain("public static readonly Color ParryWhite = new Color(1f, 1f, 1f, 0.85f);"),
                "[205차] FXPalette.ParryWhite=(1,1,1,0.85) 단일 소스 불변 — 순백 팔레트 계약(171차 §6).");
        }
    }
}
