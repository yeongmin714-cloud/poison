using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 25-IN] Phase IN 잔존 이관 계약테스트 — 소스 문자열 계약 6케이스.
    /// EA2(사망 FX 배선: MonsterData 경로 교체+폴백 / AnimalAI 주석 해제),
    /// EA1(몬스터 개성 임팩트 틴트: MonsterAccentImpactColor),
    /// IN1(날씨 반응 먼지 틴트: WeatherTintDust),
    /// FA2(스콜치 텍스처 고도화: 크랙 테이퍼 + 블롯 비대칭 해시).
    /// 계획서: docs/plans/2026-11-12_combat-vfx-glow-pipeline-round25-plan.md §Phase IN.
    /// 모든 판정은 소스 문자열 검사(결정론) — Random 0건, 컴파일/Play 불요.
    /// </summary>
    public class CombatFxFollowupINTests
    {
        /// <summary>[EA2-1] MonsterData — deathEffectPrefab Resources 경로 FX/Death/ 교체 + DeathSmoke 폴백(1단 if/else).</summary>
        [Test]
        public void EA2_MonsterData_DeathPathAndFallback()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Core/MonsterData.cs");
            Assert.IsTrue(src.Contains("FX/Death/{monsterId}_death"),
                "deathEffectPrefab 로드 경로 FX/Death/ 교체(무번호 25-EA2)");
            Assert.IsTrue(src.Contains("FX/Death/DeathSmoke"),
                "로드 실패 시 공용 폴백 DeathSmoke(실존 프리팹)");
            Assert.IsTrue(src.Contains("if (deathEffectPrefab == null)"),
                "폴백 1단 if/else 가드(랜덤 0건)");
        }

        /// <summary>[EA2-2] AnimalAI — 사망 FX 신계약(무번호 25-A1 수리 반영): Instantiate(deathEffectPrefab) 직접 호출
        /// 폐기 → DeathVFX.PlayDeathVFX(유한 수명: main.loop=false + 루핑 Stop + Destroy 예약) 경유 배선.
        /// [무번호 25-A1] 주석만 유지, 구 코드/구주석 잔존 금지. 부재 검증은 실제 코드 호출 패턴
        /// "Instantiate(deathEffectPrefab, transform" 기준(A1 마커 주석의 문언 인용과 충돌하지 않도록 정밀화).
        /// MonsterData.deathEffectPrefab 필드+폴백 로드는 유지(EA2 MonsterData 계약 무손상).</summary>
        [Test]
        public void EA2_AnimalAI_DeathFX_DeathVFXPath()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.IsTrue(src.Contains("[무번호 25-A1]"),
                "사망 FX 수리 계약 마커 — DeathVFX 유한 수명 경유(무번호 25-A1)");
            Assert.IsTrue(src.Contains("DeathVFX.PlayDeathVFX"),
                "사망 FX DeathVFX.PlayDeathVFX 경유 배선(무번호 25-EA2/A1 신계약)");
            Assert.That(src, Does.Not.Contain("Instantiate(deathEffectPrefab, transform"),
                "사망 FX Instantiate 직접 호출 폐기(무한 반복 근원 제거 — 무번호 25-A1)");
            Assert.That(src, Does.Not.Contain("// if (deathEffectPrefab"),
                "구주석 잔존 금지 — 구 코드 주석화 금지(A1 마커만 유지)");
            var md = System.IO.File.ReadAllText("Assets/Scripts/Core/MonsterData.cs");
            Assert.IsTrue(md.Contains("public GameObject deathEffectPrefab"),
                "MonsterData.deathEffectPrefab 필드 유지(EA2 MonsterData 계약 무손상)");
            Assert.IsTrue(md.Contains("Resources.Load<GameObject>(\"FX/Death/DeathSmoke\")"),
                "MonsterData DeathSmoke 폴백 로드 경로 유지(EA2 MonsterData 계약)");
        }

        /// <summary>[EA1] 몬스터 개성 임팩트 틴트 — MonsterAccentImpactColor 정의 + 호출부 사용 + 0.25 시프트.</summary>
        [Test]
        public void EA1_MonsterAccentImpactColor_DefinitionAndCall()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/AnimalAI.cs");
            Assert.IsTrue(src.Contains("MonsterAccentImpactColor(Color baseImpactColor, Color bodyColor)"),
                "틴트 헬퍼 정의(무번호 25-EA1)");
            // [195차 P1] 임팩트 링 호출 철거 — 호출부 헬퍼 경유 어서션을 부재 계약으로 리핀(정의/수학 어서션은 유지).
            Assert.That(src, Does.Not.Contain("MonsterAccentImpactColor(new Color(1f, 0.25f, 0.2f, 0.8f)"),
                "[195차 P1] 지면 임팩트 링(1.4m) 호출 철거 — 헬퍼 정의는 시그니처 계약으로 유지(무번호 25-EA1)");
            Assert.IsTrue(src.Contains("Color.Lerp(baseImpactColor, sat, 0.25f)"),
                "적색 우세 유지 — hue 25% 시프트");
        }

        /// <summary>[IN1] 날씨 반응 먼지 틴트 — WeatherTintDust 정의 + Rain/Snow 분기 + 트레일/퍼프 본문 배선.</summary>
        [Test]
        public void IN1_WeatherTintDust_DefinitionAndWiring()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(src.Contains("internal static Color WeatherTintDust(Color baseColor)"),
                "날씨 틴트 헬퍼 정의(무번호 25-IN1)");
            Assert.IsTrue(src.Contains("WeatherManager.Instance"),
                "WeatherManager 접근(부재 시 기존색 안전 가드)");
            Assert.IsTrue(src.Contains("WeatherManager.WeatherType.Rain"),
                "Rain 청백 물 스플래시 분기");
            Assert.IsTrue(src.Contains("WeatherManager.WeatherType.Snow"),
                "Snow 흰 파우더 분기");
            Assert.IsTrue(src.Contains("Spawn(groundPos, TrailDustRadius, WeatherTintDust(TrailDustColor), TrailDustDuration);"),
                "SpawnMoveTrailDust 본문 틴트 경유 배선");
            Assert.IsTrue(src.Contains("Spawn(groundPos, ArrivalPuffRadius, WeatherTintDust(ArrivalPuffColor), ArrivalPuffDuration);"),
                "SpawnArrivalPuff 본문 틴트 경유 배선");
        }

        /// <summary>[FA2] 스콜치 텍스처 고도화 — 마커 + 크랙 테이퍼/블롯 비대칭 해시 심볼(실제 도입명).</summary>
        [Test]
        public void FA2_ScorchTexture_TaperAndAsymmetricBlot()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/DecalSpawner.cs");
            Assert.IsTrue(src.Contains("[무번호 25-FA2]"),
                "스콜치 고도화 마커(무번호 25-FA2)");
            Assert.IsTrue(src.Contains("crackTaper"),
                "크랙 두께 테이퍼(중심 두껍→끝 가늘) 도입 심볼");
            Assert.IsTrue(src.Contains("blotOffsetLeft"),
                "블롯 좌측 오프셋 — 결정론 해시 도입 심볼");
            Assert.IsTrue(src.Contains("blotOffsetRight"),
                "블롯 우측 오프셋 — 좌/우 비대칭 보장");
        }

        /// <summary>[계약 유지] 스콜치 수명 10s 토큰 + 날씨 Rain/Snow 분기 토큰 동시 존재.</summary>
        [Test]
        public void Contracts_Lifetime10s_And_WeatherBranches()
        {
            var ds = System.IO.File.ReadAllText("Assets/Scripts/Systems/DecalSpawner.cs");
            Assert.IsTrue(ds.Contains("lifetime = 10f"),
                "스콜치 데칼 수명 10s 계약 토큰 유지");
            var fx = System.IO.File.ReadAllText("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.IsTrue(fx.Contains("case WeatherManager.WeatherType.Rain:"),
                "날씨 Rain 분기 토큰(무번호 25-IN1)");
            Assert.IsTrue(fx.Contains("case WeatherManager.WeatherType.Snow:"),
                "날씨 Snow 분기 토큰(무번호 25-IN1)");
        }
    }
}
