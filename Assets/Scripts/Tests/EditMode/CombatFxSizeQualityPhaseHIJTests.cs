using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 20] 전투 VFX 크기·품질 고도화 계약테스트 — H 몬스터 공격(반경 축소+셰이더 고품질+발동 플래시)
    /// / I 이동(트레일 게이팅+도착 퍼프+진행도 이징+세그먼트 룩) / J 패링(디플렉트 팬+등급 위계+잔광 곡선).
    /// 계획서: docs/plans/2026-11-12_combat-vfx-size-quality-upgrade-plan.md
    /// </summary>
    public class CombatFxSizeQualityPhaseHIJTests
    {
        // ── Phase H ────────────────────────────────────────────────

        /// <summary>[H1] _attackRange ×0.8 축소 — 링/판정/추격(1.2×) 공유값 단일 변경(1:1 일치 유지).</summary>
        [Test]
        public void H1_AttackRange_Reduced()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.IsTrue(src.Contains("private float _attackRange = 1.6f;"),
                "기본 _attackRange 1.6m(무번호 20-H1)");
            Assert.IsTrue(src.Contains("_attackRange = 1.6f;   // [무번호 20-H1] 2.0→1.6 축소"),
                "Beginner 티어 1.6m");
            Assert.IsTrue(src.Contains("_attackRange = 2f;   // [무번호 20-H1] 2.5→2.0 축소"),
                "Intermediate 티어 2.0m");
            Assert.IsTrue(src.Contains("_attackRange = 2.4f;   // [무번호 20-H1] 3.0→2.4 축소"),
                "Advanced 티어 2.4m");
            // 스폰/판정 경로 — [190차] 몬스터 적색링(텔레그래프 시각) 제거 — 스폰 경로 문자열은
            //   소스에 존재하면 안 됨(제거 계약으로 리핀). 판정 반경 공유 계약은 무수정 유지.
            Assert.IsFalse(src.Contains("TelegraphArcIndicator.Spawn(transform.position, _attackRange, TelegraphWindupSeconds, arcDir)"),
                "[190차] 몬스터 적색링(공격 텔레그래프 시각) 스폰 삭제 확인 — 시각 없이 판정만 존속");
            Assert.IsTrue(src.Contains("if (playerDist <= _attackRange)"),
                "타격 판정 반경 동일 값 공유(링=판정 1:1)");
        }

        /// <summary>[H2] TelegraphRing 셰이더 고품질 — 그라디언트 채움/위험 임박 램프/에지 셔머, B2 프로퍼티 전수 잔존.</summary>
        [Test]
        public void H2_Shader_QualityMarkers()
        {
            var src = System.IO.File.ReadAllText("Assets/Shaders/TelegraphRing.shader");
            // B2 프로퍼티 8종 불변 계약
            foreach (var prop in new[] { "_BaseColor", "_Fill01", "_EdgePulse", "_Wedge01", "_AttackAngle", "_Scan", "_StartPulse", "_Flash" })
                Assert.IsTrue(src.Contains(prop), $"B2 프로퍼티 잔존: {prop}");
            // 신규 품질 마커
            Assert.IsTrue(src.Contains("dangerRamp"), "위험 임박 램프(채움 80%+ 긴장 상승)");
            Assert.IsTrue(src.Contains("fillGrad"), "채움 방사 그라디언트(중심 진함)");
            Assert.IsTrue(src.Contains("shimmer"), "에지 에너지 셔머(각도 해시 — 결정론)");
            Assert.IsTrue(src.Contains("fillMask * dangerRamp * 0.60"), "채움 컬러 램프 배선");
        }

        /// <summary>[H3] 발동 방향 부채꼴 플래시 — FlashConclude 신설 + 몬스터 2경로 배선.</summary>
        [Test]
        public void H3_FlashConclude_Wired()
        {
            var ind = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackTelegraphIndicator.cs");
            Assert.IsTrue(ind.Contains("public void FlashConclude(Vector3 attackDirection)"),
                "FlashConclude 신설(무번호 20-H3)");
            Assert.IsTrue(ind.Contains("_instanceMat.SetFloat(\"_Wedge01\", 1f)"),
                "발동 시 부채꼴 풀 채움(B2 자산 사용 배선)");
            var ai = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.IsTrue(ai.Contains("telegraphComp.FlashConclude("),
                "AnimalAI conclude 배선");
            var ae = System.IO.File.ReadAllText("Assets/Scripts/Systems/AfterlifeEnemy.cs");
            Assert.IsTrue(ae.Contains("telegraphComp.FlashConclude("),
                "AfterlifeEnemy conclude 배선");
        }

        // ── Phase I ────────────────────────────────────────────────

        /// <summary>[I1] 트레일 거리 게이팅 — 프레임마다 스폰 폐기, 0.6m 간격 연속 자취.</summary>
        [Test]
        public void I1_Trail_DistanceGated()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.IsTrue(src.Contains("private const float TrailDustGap = 0.6f;"),
                "트레일 간격 0.6m 상수(무번호 20-I1)");
            Assert.IsTrue(src.Contains("_lastTrailDustPos"), "간격 추적 필드");
            Assert.IsTrue(src.Contains("(next - _lastTrailDustPos).sqrMagnitude >= TrailDustGapSq"),
                "거리 게이팅 배선");
        }

        /// <summary>[I2] 도착 먼지 퍼프 — 도착 분기 1회 소형 확산(F1 계약과 별개 신규).</summary>
        [Test]
        public void I2_ArrivalPuff_Wired()
        {
            var fx = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(fx.Contains("public static void SpawnArrivalPuff(Vector3 groundPos)"),
                "SpawnArrivalPuff 신설(무번호 20-I2)");
            var guard = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.IsTrue(guard.Contains("ShockwaveRingFX.SpawnArrivalPuff("),
                "도착 분기 퍼프 배선");
            int idxArrival = guard.IndexOf("if (distXZ <= MOVE_CLEAR_RADIUS)");
            int idxPuff = guard.IndexOf("ShockwaveRingFX.SpawnArrivalPuff(");
            Assert.IsTrue(idxArrival > 0 && idxPuff > idxArrival, "도착 분기 내부 배선 순서");
            // F1 트레일("이동 중에만") 계약 불변 — 트레일은 StepToward, 퍼프는 도착 분기(별개)
            Assert.IsTrue(guard.Contains("ShockwaveRingFX.SpawnMoveTrailDust("), "기존 트레일 경로 잔존");
        }

        /// <summary>[I3] 진행도 이징+임박 가속 — smoothstep + ≥0.8 추가 조임. [72차] 규약 불변.</summary>
        [Test]
        public void I3_Progress_EasingAndNearSignal()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("Mathf.SmoothStep(0f, 1f, ComputeArrivalProgress())"),
                "진행도 smoothstep 이징(무번호 20-I3)");
            Assert.IsTrue(src.Contains("0.25f + 0.10f * ((progress - 0.8f) / 0.2f)"),
                "임박 가속(≥0.8 추가 10% 조임)");
            Assert.IsTrue(src.Contains("private const float FadeSeconds = 0.3f;") &&
                          src.Contains("private float _life = 1f;"),
                "[72차] 1s 규약 불변");
            Assert.IsTrue(src.Contains("도착 소멸은 미적용"), "도착 소멸 미적용 마커 불변");
        }

        /// <summary>[I4] 데칼 링 세그먼트 룩 — 6분할 각도 변조 + 중앙 그라디언트 필(C1 패리티).</summary>
        [Test]
        public void I4_DecalRing_SegmentLook()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("회전 세그먼트 룩"), "세그먼트 룩 마커(무번호 20-I4)");
            Assert.IsTrue(src.Contains("ang01 * 6f"), "6분할 변조");
            Assert.IsTrue(src.Contains("CommandRingPresentation.ShaderCenterFillAlpha"),
                "중앙 그라디언트 필(C1 상수 패리티)");
        }

        // ── Phase J ────────────────────────────────────────────────

        /// <summary>[J1] 패링 디플렉트 팬 — 결정론 팬(Ramdom 0건) + 패링 경로 배선.</summary>
        [Test]
        public void J1_DeflectFan_Deterministic_Wired()
        {
            var fx = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(fx.Contains("public static void SpawnParryDeflectFan(Vector3 origin, Vector3 attackerDir, Color color, float duration = 0.25f)"),
                "SpawnParryDeflectFan 신설(무번호 20-J1)");
            // FanRunner 영역 결정론 — 랜덤 호출 금지(프로브 판정 친화)
            int fanIdx = fx.IndexOf("private class FanRunner");
            int runnerEnd = fx.IndexOf("private class RingRunner");
            string fanBlock = fx.Substring(fanIdx, runnerEnd - fanIdx);
            Assert.IsFalse(fanBlock.Contains("Random."), "FanRunner 결정론 — Random 0건");
            var pc = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            // [172차 P3-L3 갱신] 팬 호출 철수 — 곡선 위스프 아크가 방패 스윙 궤적 단일 계층(팬 정의/스윙 파동 무손상)
            Assert.IsTrue(pc.Split(new[] { "ShockwaveRingFX.SpawnParryDeflectFan(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[172차 P3-L3] 팬 경로 패막 호출 철수 — 곡선 샤드 교체");
            // [176차 갱신] 곡선 위스프 아크도 방사형 산란으로 철수 — 팬 철수 계약만 승계(정의/AnimalAI 무손상).
            Assert.IsTrue(pc.Split(new[] { "ShockwaveRingFX.SpawnParryArcWisps(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 곡선 위스프 패막 경로 호출 0건 — 방사형 산란 제거");
        }

        /// <summary>[J2 176차 갱신] 등급 위계 버스트 — [176차] 방사형 산란 제거로 패막 경로 호출 철수
        ///   (SpawnCritBurst 정의/타 경로 무손상 — 호출부 소멸만 검증).</summary>
        [Test]
        public void J2_GradeHierarchy_BurstCount()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            Assert.IsTrue(src.Split(new[] { "CombatVFXController.SpawnCritBurst(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 버스트 호출 0건 — 방사형 산란 제거(원형 링만 유지)");
        }

        /// <summary>[J3] 링 잔광 곡선 — easeInCubic(수명 대부분 밝게 유지 → 마지막 소멸).</summary>
        [Test]
        public void J3_RingFade_EaseInCubic()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("float fadeT = t * t * t;   // easeInCubic"),
                "RingRunner easeInCubic 잔광 곡선(무번호 20-J3)");
        }
    }
}
