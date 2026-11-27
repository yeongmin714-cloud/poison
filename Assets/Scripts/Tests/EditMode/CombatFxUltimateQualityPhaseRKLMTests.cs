using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 21] 전투 VFX 최종 고품질화 계약테스트 — R 공통 링 렌더(절차 밴드 텍스처) + K 이동(파티클 퍼프/이중 톤)
    /// / L 몬스터 공격(수렴 링/스콜치 데칼). 계획서: docs/plans/2026-11-12_combat-vfx-ultimate-quality-plan.md
    /// M1 방패 에미션 = 방패 렌더러 참조 경로 부재로 미채택(계획서 조건부 판정 — 머티리얼 인스턴스 리스크).
    /// </summary>
    public class CombatFxUltimateQualityPhaseRKLMTests
    {
        // ── Phase R ────────────────────────────────────────────────

        /// <summary>[R1] 절차 밴드 텍스처 — 가우시안 글로우+결정론 노이즈+시임 랩 블렌드.</summary>
        [Test]
        public void R1_BandTexture_Procedural()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("private static Texture2D EnsureBandTexture()"),
                "밴드 텍스처 생성기 신설(무번호 21-R1)");
            Assert.IsTrue(src.Contains("Mathf.Exp(-dv * dv * 3.2f)"), "v축 가우시안 글로우");
            Assert.IsTrue(src.Contains("시임 랩 블렌드"), "u=0/1 이음새 랩 블렌드");
            Assert.IsTrue(src.Contains("43758.5453f"), "u축 결정론 해시 노이즈(랜덤 0건)");
        }

        /// <summary>[R2] Spawn 경로 텍스처 할당 — 단색 튜브 → 그라디언트 에너지 밴드.</summary>
        [Test]
        public void R2_Spawn_UsesBandTexture()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("mat.mainTexture = EnsureBandTexture();"),
                "Spawn 머티리얼에 밴드 텍스처 할당(무번호 21-R2)");
        }

        /// <summary>[R3] 두께 보상 — 페더링으로 줄어드는 체감 두께 회복(0.09→0.115).</summary>
        [Test]
        public void R3_ThicknessCompensation()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("private const float RING_THICKNESS = 0.115f;"),
                "링 두께 0.115m 보상(무번호 21-R3)");
        }

        /// <summary>[R4] 시그니처 전수 불변 — 6개 공개 스폰 API 계약.</summary>
        [Test]
        public void R4_Signatures_Unchanged()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("public static void Spawn(Vector3 worldPos, float maxRadius, Color color, float duration)"), "Spawn");
            Assert.IsTrue(src.Contains("public static void SpawnMoveTrailDust(Vector3 groundPos)"), "SpawnMoveTrailDust");
            Assert.IsTrue(src.Contains("public static void SpawnArrivalPuff(Vector3 groundPos)"), "SpawnArrivalPuff");
            Assert.IsTrue(src.Contains("public static void SpawnDirectionalSpark(Vector3 origin, Vector3 direction, Color color, float duration = 0.3f)"), "SpawnDirectionalSpark");
            Assert.IsTrue(src.Contains("public static void SpawnParryDeflectFan(Vector3 origin, Vector3 attackerDir, Color color, float duration = 0.25f)"), "SpawnParryDeflectFan");
            var cvx = System.IO.File.ReadAllText("Assets/Scripts/Systems/CombatVFXController.cs");
            Assert.IsTrue(cvx.Contains("public static void SpawnCritBurst(Vector3 position, int sparkCount = 12, float speedScale = 1f, float sizeScale = 1f, Color? tint = null)"), "SpawnCritBurst");   // [172차 P2] 색 파라미터화 갱신
        }

        // ── Phase K ────────────────────────────────────────────────

        /// <summary>[K1] 먼지 파티클 퍼프 — 절차 소프트 서클(사각 금지 계약)+5입자 한정.</summary>
        [Test]
        public void K1_TrailPuff_Particles()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("public static void SpawnMoveTrailPuff(Vector3 groundPos)"),
                "SpawnMoveTrailPuff 신설(무번호 21-K1)");
            Assert.IsTrue(src.Contains("private static Texture2D EnsurePuffTexture()"), "소프트 서클 텍스처");
            Assert.IsTrue(src.Contains("사각 금지 계약"), "사각 금지 계약 마커(156차 선례 준용)");
            Assert.IsTrue(src.Contains("private const int PuffParticleCount = 5;"), "입자 수 한정(게이팅 병행)");
            Assert.IsTrue(src.Contains("a * a * (3f - 2f * a)"), "smoothstep 방사 감쇠(하드 엣지 방지)");
        }

        /// <summary>[K2] 트레일 2레이어 — 지면 링+공기 퍼프 병행(0.6m 게이팅 유지).</summary>
        [Test]
        public void K2_Trail_TwoLayer()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/GuardPlaceholder.cs");
            int idxRing = src.IndexOf("ShockwaveRingFX.SpawnMoveTrailDust(");
            // [무번호 22-Q2] 시작 킥이 파일 앞쪽(SetCommandTarget)에 존재 → 트레일 지점 이후 첫 퍼프를 탐색
            int idxPuff = src.IndexOf("ShockwaveRingFX.SpawnMoveTrailPuff(", idxRing);
            Assert.IsTrue(idxRing > 0 && idxPuff > idxRing, "링→퍼프 순서 배선(무번호 21-K2)");
            Assert.IsTrue(src.Contains("(next - _lastTrailDustPos).sqrMagnitude >= TrailDustGapSq"),
                "0.6m 게이팅 계약 유지(스폰 수 증가 없음)");
        }

        /// <summary>[K3] 이동 링 이중 톤 — 외곽 코어 에지+내측 딥 톤.</summary>
        [Test]
        public void K3_MoveRing_DualTone()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/CommandMarker.cs");
            Assert.IsTrue(src.Contains("이중 톤 밴드"), "이중 톤 마커(무번호 21-K3)");
            Assert.IsTrue(src.Contains("Mathf.Abs(r - 0.835f) / 0.015f"), "외곽 코어 에지 라인");
            Assert.IsTrue(src.Contains("band * (1f - 0.22f * innerDeep) + edgeLine * 0.5f"), "딥 톤+에지 부스트 합성");
        }

        // ── Phase L ────────────────────────────────────────────────

        /// <summary>[L1] 수렴 링 — 수축 트윈 신설+인디케이터 75% 자체 발화(1회 가드).</summary>
        [Test]
        public void L1_ConvergingRing()
        {
            var fx = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(fx.Contains("public static void SpawnConvergingRing(Vector3 worldPos, float startRadius, Color color, float duration)"),
                "SpawnConvergingRing 신설(무번호 21-L1)");
            Assert.IsTrue(fx.Contains("private class ConvergeRunner"), "수축 Runner");
            Assert.IsTrue(fx.Contains("float easeIn = t * t;   // 수축 가속"), "easeIn 수축 곡선");
            var ind = System.IO.File.ReadAllText("Assets/Scripts/Systems/AttackTelegraphIndicator.cs");
            Assert.IsTrue(ind.Contains("bool _convergeSpawned;"), "1회 발화 가드 필드");
            Assert.IsTrue(ind.Contains("t >= 0.75f"), "채움 75% 시점 발화");
            Assert.IsTrue(ind.Contains("ShockwaveRingFX.SpawnConvergingRing(transform.position"), "자체 발화 배선");
        }

        /// <summary>[L2] 스콜치 데칼 — DecalSpawner 4번째 타입+명중 지점 배선(빈 스윙 미배치).</summary>
        [Test]
        public void L2_ScorchMark_Decal()
        {
            var ds = System.IO.File.ReadAllText("Assets/Scripts/Systems/DecalSpawner.cs");
            Assert.IsTrue(ds.Contains("CreateScorchTexture()"), "스콜치 절차 텍스처(무번호 21-L2)");
            Assert.IsTrue(ds.Contains("_configMap[\"ScorchMark\"] = _scorchMarkConfig;"), "타입 맵 등록");
            Assert.IsTrue(ds.Contains("방사 크랙 라인 8본"), "결정론 크랙(랜덤 0건 마커)");
            var ai = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.IsTrue(ai.Split(new[] { "SpawnDecal(\"ScorchMark\"" }, System.StringSplitOptions.None).Length - 1 == 2,
                "명중 2분기(어그로 대상/플레이어) 배선 — 빈 스윙 분기 미포함");
            Assert.IsTrue(ai.Contains("빈 스윙은 미배치"), "판정 정직성 마커");
        }
    }
}
