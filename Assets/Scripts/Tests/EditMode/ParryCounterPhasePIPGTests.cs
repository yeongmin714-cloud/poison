using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 36-2R] 패망 임팩트/반격/위계 계약 — PI3 트리플 링 3박 / PS2 반격 식별 링 / PG1 GOOD 톤 보강.
    /// 기각 기록: PI2(기존 디플렉트 팬/스윕 스트릭과 중복), PS1(반격 큐 = ParryImpactRoutine 내 즉시 해방 — 대기 상태 부재, 실측).
    /// </summary>
    public class ParryCounterPhasePIPGTests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── PI3: 트리플 링 3박 — +0.12s 서드 원거리 링 ──
        [Test]
        public void Source_36PI3_TripleRing()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("[무번호 36-PI3] 트리플 링 3박 — +0.12s 서드 원거리 잔광 링(파열 리듬의 셋째 박)."),
                "[36-PI3] wave0 PERFECT 훅 마커.");
            Assert.That(src, Does.Contain("if (Application.isPlaying)\n                            StartCoroutine(ParryThirdRingRoutine(groundP));"),
                "[36-PI3] EditMode 미발화 가드(호출부) — 테스트 안전.");
            Assert.That(src, Does.Contain("private System.Collections.IEnumerator ParryThirdRingRoutine(Vector3 groundP)"),
                "[36-PI3] 루틴 시그니처.");
            Assert.That(src, Does.Contain("yield return new WaitForSecondsRealtime(0.12f);"),
                "[36-PI3] unscaled 대기 — 히트스톱 중 리듬 유지(23-X1 선례).");
            Assert.That(src, Does.Contain("ShockwaveRingFX.Spawn(groundP, 4.4f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.45f), 0.75f);"),
                "[36-P1] 서드 링 — 95차 금색 리터럴 계열(아우터 0.5 알파 대비 더 옅은 잔광).");
            Assert.That(src, Does.Contain("ShockwaveRingFX.Spawn(groundP, 3.3f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.5f), 0.75f);"),
                "[36-PI3] 기존 아우터 링(95차) 무손상.");
        }

        // ── PS2: 반격 식별 링 — 배율 적용 타 한정 ──
        [Test]
        public void Source_36PS2_CounterIdentRing()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("[무번호 36-PS2] 반격 식별 링 — 큐 해방 1타(반격 배율 적용 타)에 한해 타겟 지점 흰 링(일반 타격과 구분)."),
                "[36-PS2] 훅 마커.");
            Assert.That(src, Does.Contain("if (counterMultiplier != 1f && LastHitValid && Application.isPlaying)"),
                "[36-PS2] 반격 타 한정 + 유효 히트점 가드 + EditMode 가드.");
            Assert.That(src, Does.Contain("ShockwaveRingFX.Spawn(LastHitPoint, 0.8f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.7f), 0.3f);"),
                "[36-PS2] 타겟 지점 흰 링(ParryGold 단일 소스).");
            int iTake = src.IndexOf("target.TakeDamage(damage, hitDirection, _currentWeapon?.weaponType.ToString() ?? \"melee\");");
            int iRing = src.IndexOf("[무번호 36-PS2] 반격 식별 링");
            Assert.That(iRing, Is.GreaterThan(iTake), "[36-PS2] 데미지 적용 후 발화(순서 계약).");
        }

        // ── PG1: GOOD 톤 보강 — 금-청 이중(PERFECT 위계 무손상) ──
        [Test]
        public void Source_36PG1_GoodTone()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("[무번호 36-PG1] GOOD 톤 보강 — 흰 미니 링([171차] 흰 단일 — 성공이지만 만점 아님)."),
                "[36-PG1] 훅 마커.");
            Assert.That(src, Does.Contain("if (grade != ParryGrade.Perfect)\n                        ShockwaveRingFX.Spawn(p, 0.9f, new Color(FXPalette.ParryWhite.r, FXPalette.ParryWhite.g, FXPalette.ParryWhite.b, 0.6f), 0.35f);"),
                "[36-PG1] GOOD 전용 흰 미니 링.");
            // [172c] Fluent 확산링 → 수렴-역전 링(SpawnParryConvergeRing) 교체 계약
            Assert.That(src, Does.Contain("ShockwaveRingFX.SpawnParryConvergeRing(p, 1.6f, ringColor);"),
                "[172c] 수렴-역전 링 승계(Fluent 확산링 폐기).");
        }

        // ── 기각 기록 — 중복/전제 불성실 재유입 방지 ──
        [Test]
        public void Source_36_Rejections_Documented()
        {
            // PI2 기각 근거: 디플렉트 팬(FanRunner 스트릭)·스윕 스윕 스트릭이 이미 메시 스트릭 계층 — 신규 스트릭은 중복.
            string fx = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.That(fx, Does.Contain("public static void SpawnParryDeflectFan(Vector3 origin, Vector3 attackerDir, Color color, float duration = 0.25f)"),
                "[36-기각:PI2] 기존 디플렉트 팬(스트릭 계열) 존재 — 방향성 스트릭 신규 구현 금지.");
            // PS1 기각 근거: 반격 큐는 ParryImpactRoutine 내 즉시 해방(L821~) — "대기 펄스" 전제 불성실.
            string pc = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(pc, Does.Contain("float counter = ConsumeQueuedCounterAttack();"),
                "[36-기각:PS1] 큐 즉시 해방 구조 존재 — 대기 상태 펄스 신규 구현 금지.");
        }
    }
}
