using NUnit.Framework;

namespace ProjectName.Tests
{
    /// <summary>
    /// [무번호 73] 환경 반응 VFX 계약테스트 — E2 오브젝트 스윙 / E3 우천 워터 스플래시.
    /// 계획서: docs/plans/2026-11-18_no73-env-reaction-rain-splash-plan.md
    /// 소스 계약 패턴(CombatFxMissingLayersPhaseUVWXTests 선례) — EditMode 안전(런타임 쿼리·스폰 가드).
    /// </summary>
    public class FxEnvReactionP73Tests
    {
        private static string Read(string path) => System.IO.File.ReadAllText(path);

        // ── E3 우천 워터 스플래시 ─────────────────────────────────

        /// <summary>[E3] Rain 게이트 + 입자 전용 레이어 + 임팩트 단일 경로 훅.</summary>
        [Test]
        public void E3_RainSplash_Contracts()
        {
            var src = Read("Assets/Scripts/Systems/FxEnvReaction.cs");
            Assert.IsTrue(src.Contains("[무번호 73-E3]"), "E3 마커");
            Assert.IsTrue(src.Contains("wm.CurrentWeather == WeatherManager.WeatherType.Rain"),
                "Rain 게이트(25-IN1 판정식 동일 조건)");
            Assert.IsTrue(src.Contains("if (wm == null) return false;"),
                "WeatherManager 부재 안전 폴백(25-IN1 계약)");
            Assert.IsTrue(src.Contains("if (!Application.isPlaying) return;"), "EditMode 스폰 0건 가드");
            Assert.IsTrue(src.Contains("FxMaterialLibrary.CreateSoftParticle"),
                "소프트 파티클 경로(기존 머티리얼 인프라 재사용)");
            Assert.IsTrue(src.Contains("ps.Emit(SplashParticleCount);"), "스폰 수 한정(게이팅 계약)");
            Assert.IsTrue(src.Contains("Object.Destroy(go, SplashDespawnSeconds);"), "유한 수명(누출 금지)");
            Assert.IsTrue(src.Contains("Object.DestroyImmediate(go);"),
                "EditMode 즉시 정리 가드(21-K1 수리 — 156차 선례)");

            var runner = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            Assert.IsTrue(runner.Contains("FxEnvReaction.SpawnRainSplash(position);"),
                "임팩트 단일 경로 훅(45차 P3 수렴 유지 — PlayImpactMulti 말미)");
        }

        /// <summary>[E3] 전용 텍스처 분리 + 결정론 + 사각 금지 + 기존 잠금 파일 무접촉.</summary>
        [Test]
        public void E3_SplashTexture_NoSquareNoRandom()
        {
            var src = Read("Assets/Scripts/Systems/FxEnvReaction.cs");
            Assert.IsTrue(src.Contains("EnsureSplashTexture()"),
                "전용 물방울 텍스처(기존 먼지 빌더 private 잠금 보존 — FxShaderAssetsDTests T3)");
            Assert.That(src, Does.Not.Contain("UnityEngine.Random"), "결정론 — 난수 0건(25-IN1 계약)");
            Assert.That(src, Does.Not.Contain("System.Random"), "결정론 — 난수 0건");
            Assert.IsTrue(src.Contains("Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d))"),
                "방사 소프트 마스크(사각 금지 계약)");
            Assert.IsFalse(src.Contains("EnsurePuffTexture"), "ShockwaveRingFX 잠금 함수 무접촉");
        }

        // ── E2 오브젝트 스윙 ─────────────────────────────────────

        /// <summary>[E2] 절차 본 회전 러너 — 판정식 재사용·이징 재사용·원복·물리엔진 미사용.</summary>
        [Test]
        public void E2_SwayRunner_Contracts()
        {
            var src = Read("Assets/Scripts/Systems/FxEnvReaction.cs");
            Assert.IsTrue(src.Contains("[무번호 73-E2]"), "E2 마커");
            Assert.IsTrue(src.Contains("Physics.OverlapSphere"), "쿼리 — OverlapSphere(FindNearestGatherNode 선례)");
            Assert.IsTrue(src.Contains("GuardTaskSystem.IsGatherNode"),
                "대상 판정식 동일 조건(중복 정의 금지 — 37-E2 대상 계약)");
            Assert.IsTrue(src.Contains("FXMotion.Progress"),
                "이징 재사용(28-U3 재구현 금지 규약 — FxDeathRelax 선례)");
            Assert.IsTrue(src.Contains("class FxSwayRunner : MonoBehaviour"), "절차 본 회전 러너");
            Assert.IsTrue(src.Contains("Quaternion.AngleAxis"), "절차 본 회전(물리엔진 미사용)");
            Assert.IsTrue(src.Contains("transform.rotation = _baseRotation;"), "종료 원복");
            Assert.IsTrue(src.Contains("GetComponent<FxSwayRunner>() != null"), "스윙 중 재발화 흡수");
            Assert.IsFalse(src.Contains("Rigidbody"), "물리엔진 미사용 계약(FxDeathRelax W2 동일)");
        }

        /// <summary>[E2] 2 트리거 배선(타격/폭발) + 판정식 internal 노출.</summary>
        [Test]
        public void E2_Triggers_Wired()
        {
            var runner = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            Assert.IsTrue(runner.Contains("FxEnvReaction.TriggerImpactSway(position);"), "타격 트리거(PlayImpactMulti 말미)");
            var bomb = Read("Assets/Scripts/Systems/BombExplosionVisual.cs");
            Assert.IsTrue(bomb.Contains("FxEnvReaction.TriggerExplosionSway(pos);"), "폭발 트리거(OnDisable 발화)");
            var guard = Read("Assets/Scripts/Systems/GuardTaskSystem.cs");
            Assert.IsTrue(guard.Contains("internal static bool IsGatherNode"),
                "판정식 static 노출(시그니처·판정 리터럴 보존)");
        }

        // ── 기존 잠금 보존(무손상 회귀 가드) ─────────────────────

        /// <summary>[73] 기존 계약 무손상 — X2 예산 카운트·결정론·FX 초당 상한·링 예산 미소비.</summary>
        [Test]
        public void P73_Locks_Preserved()
        {
            var ring = Read("Assets/Scripts/Systems/ShockwaveRingFX.cs");
            Assert.AreEqual(2,
                ring.Split(new[] { "_activeRingCount++" }, System.StringSplitOptions.None).Length - 1,
                "X2 증가 지점 2곳(무번호 73 무접촉)");
            var runner = Read("Assets/Scripts/Systems/SlashVFXRunner.cs");
            Assert.That(runner, Does.Not.Contain("UnityEngine.Random"), "P37 결정론 계약 보존");
            var gate = Read("Assets/Scripts/Systems/CombatFXGate.cs");
            Assert.IsTrue(gate.Contains("MAX_FX_PER_SECOND"), "FX 초당 상한 계약 무변경");
            var env = Read("Assets/Scripts/Systems/FxEnvReaction.cs");
            Assert.IsFalse(env.Contains("_activeRingCount"), "E3 링 예산 미소비(입자 전용 레이어 — X2 계약 무손상)");
            Assert.IsFalse(env.Contains("MaxConcurrentRings"), "E3 링 예산 미소비(상수 무참조)");
        }
    }
}
