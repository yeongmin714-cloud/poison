using NUnit.Framework;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 37-2R] 고품질 계층 1R — H1 강타격 프리즈 / G3 위험 방향 인디케이터 / H3 지면 크랙 / G1 투사체 트레일.
    /// </summary>
    public class FxQualityH1G3H3G1Tests
    {
        private static string Read(string p) => System.IO.File.ReadAllText(p);

        // ── H1: 강타격 프리즈(2사이트 — 충전 강공/일반·반격) ──
        [Test]
        public void Source_H1_HeavyHitFreeze()
        {
            string src = Read("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.That(src, Does.Contain("[무번호 37-H1]"), "[37-H1] 마커.");
            int i1 = src.IndexOf("[무번호 37-H1] 강타격 프리즈 — 피해≥15");
            int i2 = src.IndexOf("[무번호 37-H1] 강타격 프리즈 — 동일 밴드");
            Assert.That(i1, Is.GreaterThanOrEqualTo(0), "[37-H1] 충전 강공 사이트 훅.");
            Assert.That(i2, Is.GreaterThanOrEqualTo(0), "[37-H1] 일반/반격 사이트 훅.");
            Assert.That(src, Does.Contain("RequestHitStop(0.05f, 0.06f)"),
                "[37-H1] 프리즈 밴드(0.05s@0.06 — 36 파리 등급 0.07/0.03과 무충돌).");
            Assert.That(src, Does.Contain("if (damage >= 15f && Application.isPlaying)"),
                "[37-H1] 피해≥15(Heavy 기준 공유)+Play 가드.");
        }

        // ── G3: 위험 방향 인디케이터 ──
        [Test]
        public void Source_G3_EdgeDanger()
        {
            string src = Read("Assets/Scripts/Systems/ScreenEdgeDangerFX.cs");
            Assert.That(src, Does.Contain("[무번호 37-G3]"), "[37-G3] 마커.");
            Assert.That(src, Does.Contain("ProjectName.Core.PlayerHealth.OnPlayerDamaged += OnPlayerDamaged"),
                "[37-G3] Core 무수정 — 구독만(28-M2 선례).");
            Assert.That(src, Does.Contain("if (inside) return;"),
                "[37-G3] 시야 내 공격은 미표시(시야 밖 한정).");
            Assert.That(src, Does.Contain("_elapsed += Time.unscaledDeltaTime;"),
                "[37-G3] unscaled 시간(히트스톱 중 가동 — 31-V3 선례).");
            Assert.That(src, Does.Contain("catch (System.Exception) { }"),
                "[37-G3] 시각 보조 try/catch(전투 방해 금지).");
            Assert.That(src, Does.Contain("enabled = false;   // 인스턴스 재사용(누적 금지)"),
                "[37-G3] 인스턴스 1개 재사용.");
        }

        // ── H3: 지면 크랙 ──
        [Test]
        public void Source_H3_GroundCrack()
        {
            string src = Read("Assets/Scripts/Systems/FxGroundCrackFX.cs");
            Assert.That(src, Does.Contain("[무번호 37-H3]"), "[37-H3] 마커.");
            Assert.That(src, Does.Contain("const int Budget = 2;"), "[37-H3] 동시 상한 2(예산 가드).");
            Assert.That(src, Does.Contain("Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out var hit, 5f"),
                "[37-H3] 지면 스냅(하향 레이캐스트 — 폴백 y=0.03).");
            Assert.That(src, Does.Contain("DeterministicAngle"), "[37-H3] 결정론 배각(해시 — Random 0건).");
            Assert.That(src, Does.Contain("int branches = 6;"), "[37-H3] 6분지 균열.");
            // 훅은 CombatFXGate(파편 33-D1 옆) — 대상 파일 정정.
            Assert.That(Read("Assets/Scripts/Systems/CombatFXGate.cs"), Does.Contain("if (isCrit || damage >= 15f) FxGroundCrackFX.Burst(position);"),
                "[37-H3] 훅 = 파편(33-D1)과 동일 게이트.");
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "[37-H3] Random 0건.");
        }

        // ── G1: 투사체 속도 비례 트레일 ──
        [Test]
        public void Source_G1_TrailGlow()
        {
            string src = Read("Assets/Scripts/Systems/ArrowProjectile.cs");
            Assert.That(src, Does.Contain("[무번호 37-G1]"), "[37-G1] 마커.");
            Assert.That(src, Does.Contain("FXMotion.Afterglow(Mathf.Clamp01(_rb.linearVelocity.magnitude / 24f))"),
                "[37-G1] Afterglow 이징(24m/s 기준 정규화).");
            Assert.That(src, Does.Contain("_trail.time = Mathf.Lerp(0.06f, 0.18f, glow);"),
                "[37-G1] 트레일 길이 속도 비례.");
            Assert.That(src, Does.Contain("new GradientColorKey(new Color(1f, 0.99f, 0.97f), 0f), new GradientColorKey(strk1, 1f)"),
                "[37-G1] 티어 그래디언트(25-C3) 무손상.");
        }

        // ── 전체 결정론/존재 스윕 ──
        [Test]
        public void Source_2R_AllDeterministic()
        {
            Assert.That(Read("Assets/Scripts/Systems/ScreenEdgeDangerFX.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[37-2R] ScreenEdgeDangerFX Random 0건.");
            Assert.That(Read("Assets/Scripts/Systems/PlayerCombat.cs"), Does.Not.Contain("UnityEngine.Random"),
                "[37-2R] PlayerCombat Random 0건(기존 계약 유지).");
        }
    }
}
