using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 22] 전투 VFX 최종 폴리시 계약테스트 — P 공통 링 모션(워블 스크롤/echo/밝기 부스트)
    /// / Q 이동(프랙탈 먼지+난류/시작 킥/편차) / S 몬스터 공격(수렴 2겹+응축 플래시/스윙 파동/불티/순회 에너지)
    /// / T 패링(곡선 위스프/물성 위계). 계획서: docs/plans/2026-11-12_combat-vfx-final-polish-plan.md
    /// P3 Unlit 전환 = Play 비교 대기(부분 적용 — 대형 링 밝기 부스트만), Q1 = 프랙탈 교체 적용.
    /// </summary>
    public class CombatFxPolishPhasePQSTTests
    {
        // ── Phase P ────────────────────────────────────────────────

        /// <summary>[P1] 밴드 노이즈 2옥타브+스크롤 — 링 가장자리 유기적 워블.</summary>
        [Test]
        public void P1_NoiseWobble_Scroll()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("float cellA = Mathf.Floor(u * 24f);") && src.Contains("float cellB = Mathf.Floor(u * 8f);"),
                "u축 노이즈 2옥타브(24셀+8셀 — 무번호 22-P1)");
            Assert.IsTrue(src.Contains("_mat.mainTextureOffset = new Vector2(_elapsed * 0.35f, 0f);"),
                "RingRunner 텍스처 스크롤(워블)");
        }

        /// <summary>[P2] 이중 웨이브 echo — ≥1m 한정, 수명 25% 자체 스폰, 재귀 가드, API 무변경.</summary>
        [Test]
        public void P2_DoubleWave_Echo()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("private static void SpawnCore(Vector3 worldPos, float maxRadius, Color color, float duration, bool allowEcho)"),
                "SpawnCore 분리(공개 시그니처 불변)");
            Assert.IsTrue(src.Contains("if (_allowEcho && !_echoSpawned && _maxRadius >= 1.0f && t >= 0.25f)"),
                "echo 조건(≥1m/25%/1회 가드)");
            Assert.IsTrue(src.Contains("SpawnCore(transform.position, _maxRadius * 1.12f, echo, _duration * 1.6f, false);"),
                "echo 파라미터(1.12배/1.6배/0.4배 — 재귀 금지 false)");
            Assert.IsTrue(src.Contains("ring.AddComponent<RingRunner>().Init(maxRadius, color, duration, allowEcho);"),
                "가드 전달 배선");
        }

        /// <summary>[P3 부분] 대형 링 밝기 부스트 — Unlit 전환은 Play 비교 대기.</summary>
        [Test]
        public void P3_BrightnessBoost_Partial()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("if (maxRadius >= 1.0f) color = new Color(color.r * 1.15f, color.g * 1.15f, color.b * 1.15f, color.a);"),
                "대형 링 밝기 부스트(무번호 22-P3 부분 적용)");
            Assert.IsTrue(src.Contains("Play 비교 후 판단"), "전면 전환 연기 마커");
        }

        // ── Phase Q ────────────────────────────────────────────────

        /// <summary>[Q1] 프랙탈 먼지 텍스처 — 3옥타브 value noise(결정론)+사각 금지.</summary>
        [Test]
        public void Q1_FractalDustTexture()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("private static float PuffNoise(float x, float y, float seed)"),
                "value noise 함수(무번호 22-Q1)");
            Assert.IsTrue(src.Contains("3옥타브 value noise"), "옥타브 마커");
            Assert.IsTrue(src.Contains("PuffNoise(ux * 5f, uy * 5f, 1f)") && src.Contains("PuffNoise(ux * 23f, uy * 23f, 13f)"),
                "3옥타브 합성 배선(5/11/23 셀)");
            Assert.IsTrue(src.Contains("사각 금지 계약"), "사각 금지 계약 유지");
        }

        /// <summary>[Q1] 난류 모듈 — 입자 흐느적거림.</summary>
        [Test]
        public void Q1_TurbulenceModule()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("var turbulence = ps.noise;") && src.Contains("turbulence.strength = 0.1f;"),
                "PS 난류 모듈(무번호 22-Q1)");
        }

        /// <summary>[Q2] 이동 시작 킥 — SetCommandTarget 이동 명령+1.5m 게이트.</summary>
        [Test]
        public void Q2_StartKick_Wired()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            Assert.IsTrue(src.Contains("[무번호 22-Q2] 이동 시작 킥"), "시작 킥 마커");
            int idxGate = src.IndexOf("if (!a)");
            int idxPuff = src.IndexOf("ShockwaveRingFX.SpawnMoveTrailPuff(new Vector3(gp.x, gp.y + 0.06f, gp.z))");
            Assert.IsTrue(idxGate > 0 && idxPuff > 0 && idxPuff > idxGate && idxPuff > src.IndexOf("public void SetCommandTarget"),
                "이동 명령 전용 게이트 배선(공격 명령 제외)");
            Assert.IsTrue(src.Contains("(t - here).sqrMagnitude >= 2.25f"), "1.5m 거리 게이트");
        }

        /// <summary>[Q3] 입자 편차 확대 — 균일감 제거.</summary>
        [Test]
        public void Q3_VarianceWidened()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("main.startSize = new ParticleSystem.MinMaxCurve(0.08f * k, 0.22f * k);"),
                "크기 편차(22-Q3 → 29-S2-2 강도 배율 진화 — 편차 비율 2.75× 유지, k=0.5~2)");
            Assert.IsTrue(src.Contains("main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);"),
                "수명 편차 0.4~0.75");
        }

        // ── Phase S ────────────────────────────────────────────────

        /// <summary>[S1] 수렴 2겹+응축 플래시 — public 시그니처 불변, 내부 Core 분리.</summary>
        [Test]
        public void S1_ConvergeTwoLayer_AndClimaxFlash()
        {
            var fx = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(fx.Contains("SpawnConvergingRingCore(worldPos, startRadius, color, duration);"), "바깥 링");
            Assert.IsTrue(fx.Contains("SpawnConvergingRingCore(worldPos, startRadius * 0.6f, inner, duration * 1.25f);"),
                "안쪽 링(0.6배/1.25배/0.5배)");
            var ind = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackTelegraphIndicator.cs");
            Assert.IsTrue(ind.Contains("bool _concludeFlashSpawned;"), "응축 플래시 1회 가드");
            Assert.IsTrue(ind.Contains("ShockwaveRingFX.Spawn(transform.position, 0.25f, fc, 0.15f);"),
                "중심 소형 플래시 링 배선");
        }

        /// <summary>[S2] 스윙 지면 파동 — FanRunner 파라미터화+3본 좁은 팬 배선.</summary>
        [Test]
        public void S2_SwingSweep_Wired()
        {
            var fx = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(fx.Contains("public static void SpawnSwingSweep(Vector3 groundPos, Vector3 swingDir, Color color, float duration = 0.25f)"),
                "SpawnSwingSweep 신설(무번호 22-S2)");
            Assert.IsTrue(fx.Contains("float halfAngleDeg, float streakMin, float streakMax)"),
                "FanRunner 파라미터화(팬 계약 기본값 유지)");
            Assert.IsTrue(fx.Contains("host.AddComponent<FanRunner>().Init(groundPos, swingDir, 3, color, duration, _litShader, 15f, 1.0f, 1.4f);"),
                "±15° 3본/1.0~1.4m 파라미터");
            var ai = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.IsTrue(ai.Split(new[] { "ShockwaveRingFX.SpawnSwingSweep(" }, System.StringSplitOptions.None).Length - 1 == 2,
                "명중 2분기 배선(어그로/플레이어)");
        }

        /// <summary>[S3] 불티 파편 — 런타임 전용 랜덤 명시+EditMode 가드.</summary>
        [Test]
        public void S3_EmberBurst()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("public static void SpawnEmberBurst(Vector3 groundPos)"),
                "SpawnEmberBurst 신설(무번호 22-S3)");
            Assert.IsTrue(src.Contains("런타임 전용 랜덤 경로"), "프로브 미경유 명시");
            Assert.IsTrue(src.Contains("if (Application.isPlaying) Object.Destroy(go, 1.2f);"),
                "EditMode 가드 처음부터 적용(21-K1 수리 선례)");
            var ai = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.IsTrue(ai.Split(new[] { "ShockwaveRingFX.SpawnEmberBurst(" }, System.StringSplitOptions.None).Length - 1 == 2,
                "명중 2분기 배선");
        }

        /// <summary>[S4] 텔레그래프 에지 순회 에너지 — _Scan 재활용 순회 스팟.</summary>
        [Test]
        public void S4_OrbitEnergy()
        {
            var src = System.IO.File.ReadAllText("Assets/Shaders/TelegraphRing.shader");
            Assert.IsTrue(src.Contains("float orbitSpot = exp(-orbit * orbit * 18.0) * band;"),
                "순회 스팟(무번호 22-S4)");
            Assert.IsTrue(src.Contains("col.rgb = saturate(col.rgb + orbitSpot * 0.85);"), "가산 배선");
            foreach (var prop in new[] { "_BaseColor", "_Fill01", "_EdgePulse", "_Wedge01", "_AttackAngle", "_Scan", "_StartPulse", "_Flash" })
                Assert.IsTrue(src.Contains(prop), $"B2 프로퍼티 잔존: {prop}");
        }

        // ── Phase T ────────────────────────────────────────────────

        /// <summary>[T1] 곡선 위스프 아크 — 베지어 draw-on, 결정론, 패링 배선.</summary>
        [Test]
        public void T1_ParryArcWisps()
        {
            var fx = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(fx.Contains("public static void SpawnParryArcWisps(Vector3 origin, Vector3 attackerDir, Color color, float duration = 0.3f)"),
                "SpawnParryArcWisps 신설(무번호 22-T1)");
            Assert.IsTrue(fx.Contains("private class WispRunner"), "베지어 Runner");
            Assert.IsTrue(fx.Contains("Lerp(a, b, u));   // 2차 베지어"), "베지어 산식");
            int wispIdx = fx.IndexOf("private class WispRunner");
            int convergeIdx = fx.IndexOf("private class ConvergeRunner");
            string wispBlock = fx.Substring(wispIdx, convergeIdx - wispIdx);
            Assert.IsFalse(wispBlock.Contains("Random."), "WispRunner 결정론 — Random 0건");
            var pc = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            // [176차 갱신] 패막 경로 위스프 철수 — 방사형 산란 제거(원형 링만 유지).
            //   SpawnParryArcWisps 정의/WispRunner/AnimalAI 몬스터 아크(X3)는 무손상 — 호출부 소멸만 검증.
            Assert.IsTrue(pc.Split(new[] { "ShockwaveRingFX.SpawnParryArcWisps(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 곡선 위스프 호출 0건 — 방사형 산란 제거");
        }

        /// <summary>[T2] 등급별 물성 위계 — PERFECT 굵고 빠름/GOOD 얇고 느림.</summary>
        [Test]
        public void T2_GradePhysicsHierarchy()
        {
            var cvx = System.IO.File.ReadAllText("Assets/Scripts/Systems/CombatVFXController.cs");
            Assert.IsTrue(cvx.Contains("public static void SpawnCritBurst(Vector3 position, int sparkCount = 12, float speedScale = 1f, float sizeScale = 1f, Color? tint = null)"),
                "물성 파라미터 시그니처(무번호 22-T2 + 172차 P2 색 파라미터)");
            Assert.IsTrue(cvx.Contains("main.startSpeed = new ParticleSystem.MinMaxCurve(6f * speedScale, 9f * speedScale);"),
                "속도 위계 배선");
            var pc = System.IO.File.ReadAllText("Assets/Scripts/Systems/PlayerCombat.cs");
            // [176차 갱신] 패막 경로 버스트 철수 — 등급 물성 전달(1.0/0.7·0.75)도 호출 소멸과 함께 폐기.
            //   SpawnCritBurst 정의 시그니처(12방향 기본)는 무손상 — 호출부 소멸만 검증.
            Assert.IsTrue(pc.Split(new[] { "CombatVFXController.SpawnCritBurst(" },
                System.StringSplitOptions.None).Length - 1 == 0,
                "[176차] 패막 경로 스파클 버스트 호출 0건 — 방사형 산란 제거");
        }
    }
}
