using NUnit.Framework;
using System.IO;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [205차 배치C] 폭탄 투척 VFX 고품질화 계약 — 소스 문자열 어서션(BombFluidPreview193Tests 관례).
    ///  ① V205_Throw_Trail_Attached — 투척 트레일(폭탄 자식 "Bomb_Trail") + 절차 ParticleSystem + [206차] 상수 인상 + TrailRenderer 리본
    ///  ② V205_Explosion_MultiStage — 폭발 다단(플래시/화구/연기) + [206차] 충격링 제거(Does.Not 핀) + FirePath 무손상
    ///  ③ V205_Transient_NoLeak — 과도 오브젝트 자가 소멸 경로(Destroy 핀 ≥2, DontDestroyOnLoad 부재, 수명 상수)
    ///  ④ V205_Legacy_Pins_Intact — AddComponent<BombExplosionVisual>() + 히트스톱/빨간 텔레그래프/펄스/SFX 계약 문자열(189/193/195차)
    /// </summary>
    public class BombExplosionVFX205Tests
    {
        private const string ArmPath =
            "Assets/Scripts/Systems/BombArmController.cs";
        private const string VisualPath =
            "Assets/Scripts/Systems/BombExplosionVisual.cs";

        private static string SrcArm() => File.ReadAllText(ArmPath);
        private static string SrcVisual() => File.ReadAllText(VisualPath);

        // ── ① 투척 트레일 ──
        [Test]
        public void V205_Throw_Trail_Attached()
        {
            string src = SrcArm();

            Assert.That(src, Does.Contain("\"Bomb_Trail\""),
                "[205] 트레일 호스트 자식명 Bomb_Trail 존재 — 폭탄 자식(폭발 파괴 시 자동 소멸)");
            Assert.That(src, Does.Contain("AttachThrowTrail(go);"),
                "[205] SpawnThrownBomb에서 트레일 부착 호출 — GLB 비주얼 직후");
            Assert.That(src, Does.Contain("AddComponent<ParticleSystem>()"),
                "[205] 트레일 = 절차 ParticleSystem(신규 에셋 불필요)");
            Assert.That(src, Does.Contain("ParticleSystemSimulationSpace.World"),
                "[205] 트레일 월드 시뮬레이션 — 비행 궤적 잔상 형성");
            Assert.That(src, Does.Contain("FxMaterialLibrary.CreateSoftParticle"),
                "[205] 트레일 머티리얼 = 소프트 파티클 단일 빌더 재사용(29-D2 인프라)");
            Assert.That(src, Does.Contain("[205차] 투척 트레일"),
                "[205] 트레일 [205차] 주석 마커 존재");
            // 상수 고정 핀([206차] 인상 — 회갈색 퍼프 수명 ~1.2s · 크기 ~0.25m, 스파크 수명 ~0.5s)
            Assert.That(src, Does.Contain("TrailSmokeLifetime = 1.2f"),
                "[206] 연기 수명 0.6→1.2s 인상 고정");
            Assert.That(src, Does.Contain("TrailSmokeSize = 0.25f"),
                "[206] 연기 크기 0.15→0.25m 인상 고정");
            Assert.That(src, Does.Contain("TrailSparkLifetime = 0.5f"),
                "[206] 소형 주황 스파크 수명 0.25→0.5s 인상 고정");
            Assert.That(src, Does.Contain("TrailSmokeRate = 48f"),
                "[206] 연기 에미션 24→48 인상 고정");
            Assert.That(src, Does.Contain("TrailSparkRate = 24f"),
                "[206] 스파크 에미션 12→24 인상 고정");
            Assert.That(src, Does.Contain("main.maxParticles = 256"),
                "[206] 트레일 파티클 풀 96→256 인상 고정");
            // [206차] TrailRenderer 리본 핀 — 폭탄 GO 직속 리본 잔상(시간/폭 테이퍼/그라디언트/미공유 머티리얼)
            Assert.That(src, Does.Contain("AttachThrowTrailRibbon(bombGo);"),
                "[206] AttachThrowTrail 내 트레일 리본 호출 — PS 잔상과 공존");
            Assert.That(src, Does.Contain("AddComponent<TrailRenderer>()"),
                "[206] 트레일 리본 = TrailRenderer(폭탄 GO 직속 — 폭발 파괴 시 자동 소멸)");
            Assert.That(src, Does.Contain("widthCurve = AnimationCurve.Linear(0f, 0.22f, 1f, 0.02f)"),
                "[206] 리본 폭 테이퍼 0.22→0.02m 고정");
            Assert.That(src, Does.Contain("tr.time = 0.45f"),
                "[206] 리본 잔상 시간 0.45s 고정");
            Assert.That(src, Does.Contain("FxMaterialLibrary.CreateSoftParticle(\"BombTrailRibbonMat\""),
                "[206] 리본 머티리얼 = 미공유 신규 인스턴스(공유 캐시 금지)");
            // EditMode 안전 — 런타임 전용 경로 가드(21-K1 선례)
            Assert.That(src, Does.Contain("if (!Application.isPlaying) return;   // EditMode 가드 — 런타임 전용"),
                "[205] 트레일 EditMode 가드 — 런타임 전용 경로(21-K1 선례)");
        }

        // ── ② 폭발 다단 ──
        [Test]
        public void V205_Explosion_MultiStage()
        {
            string src = SrcVisual();

            // 4스테이지 마커 상수(플래시/화구/링/연기)
            Assert.That(src, Does.Contain("[205차] A 플래시 퀵"),
                "[205] 스테이지 A 플래시 퀵 마커");
            Assert.That(src, Does.Contain("[205차] B 화구"),
                "[205] 스테이지 B 화구 마커");
            Assert.That(src, Does.Contain("[205차] E 연기 페이드"),
                "[205] 스테이지 E 연기 페이드 마커");
            // [206차] 충격링 제거 — 사용자 결정(구 D 스테이지 전면 철수 — 부정 잠금 핀으로 승계)
            Assert.That(src, Does.Not.Contain("[205차] D 충격 링"),
                "[206] 구 D 충격 링 마커 부재 — 충격링 제거(사용자 결정)");
            Assert.That(src, Does.Contain("internal const string StageFlashMarker"),
                "[205] A 스테이지 마커 상수 — 소스 핀 단일 소스");
            Assert.That(src, Does.Contain("internal const string StageFireballMarker"),
                "[205] B 스테이지 마커 상수 — 소스 핀 단일 소스");
            Assert.That(src, Does.Not.Contain("internal const string StageShockRingMarker"),
                "[206] 구 D 스테이지 마커 상수 부재 — 충격링 제거(사용자 결정)");
            Assert.That(src, Does.Contain("internal const string StageSmokeMarker"),
                "[205] E 스테이지 마커 상수 — 소스 핀 단일 소스");

            // [206차] 충격링 제거 — 사용자 결정(구 D 충격 링 ShockwaveRingFX.Spawn 경로 부정 잠금)
            Assert.That(src, Does.Not.Contain("ShockwaveRingFX.Spawn"),
                "[206] 충격링 ShockwaveRingFX.Spawn 호출 부재 — D 스테이지 철수(사용자 결정)");
            Assert.That(src, Does.Not.Contain("ShockRingDuration = 0.45f"),
                "[206] 구 충격 링 지속 상수 부재 — 충격링 제거(사용자 결정)");

            // 스테이지 수명/스케일 상수(스펙 계약)
            Assert.That(src, Does.Contain("FlashLifetime = 0.05f"),
                "[205] A 플래시 수명 0.05s");
            Assert.That(src, Does.Contain("FlashScaleRatio = 0.8f"),
                "[205] A 플래시 스케일 = 폭발반경×0.8");
            Assert.That(src, Does.Contain("FireballGrowTime = 0.15f"),
                "[205] B 화구 스케일 펄스 0.15s");
            Assert.That(src, Does.Contain("FireballScaleRatio = 0.7f"),
                "[205] B 화구 목표 스케일 = 폭발반경×0.7");
            Assert.That(src, Does.Contain("SmokeLifetime = 1.2f"),
                "[205] E 연기 수명 ~1.2s");
            Assert.That(src, Does.Contain("SmokePuffCount = 3"),
                "[205] E 연기 퍼프 개수(스펙 2~3개)");

            // C 기존 화염 — FirePath/FireLifetime 무손상 핀(다단과 공존)
            Assert.That(src, Does.Contain("private const string FirePath = \"FX/Fire/VFX_Fire_01_Big\""),
                "[205] FirePath 무손상 — 기존 C 스테이지 경로 계약");
            Assert.That(src, Does.Contain("private const float FireLifetime = 2.5f"),
                "[205] FireLifetime 무손상 — 기존 C 스테이지 수명 계약");

            // 폭발 반경 실측 — Bomb.explosionRadius + 기본값 폴백
            Assert.That(src, Does.Contain("GetComponent<Bomb>()"),
                "[205] 폭발 반경 Bomb.explosionRadius 실측");
            Assert.That(src, Does.Contain("DefaultExplosionRadius = 3f"),
                "[205] 폭발 반경 기본값 폴백(3f — BombArmController 기본값 동일)");
        }

        // ── ③ 과도 오브젝트 누수 0 ──
        [Test]
        public void V205_Transient_NoLeak()
        {
            string src = SrcVisual();

            // 영속화 불사용 — 과도 오브젝트는 전부 자가 소멸
            Assert.That(src, Does.Not.Contain("DontDestroyOnLoad"),
                "[205] DontDestroyOnLoad 부재 — 과도 오브젝트 자가 소멸 계약(누수 0)");

            // 자가 소멸 경로 핀 — Object.Destroy 2소 이상(스펙 예시 기준)
            int destroyCount = CountOccurrences(src, "Object.Destroy(");
            Assert.That(destroyCount, Is.GreaterThanOrEqualTo(2),
                "[205] Object.Destroy 자가 소멸 경로 2소 이상 — 플래시/화구/연기 수명 소멸");

            // 러너 자기 파괴 + 스폰별 머티리얼 정리
            Assert.That(src, Does.Contain("Destroy(gameObject);   // 자기 파괴 — 누수 0"),
                "[205] TransientStageRunner 수명 종료 자기 파괴");
            Assert.That(src, Does.Contain("Object.Destroy(_mat);"),
                "[205] 스폰별 머티리얼 OnDestroy 정리(SparkRunner 선례 — 조기 파괴 누수 방지)");
            Assert.That(src, Does.Contain("Object.Destroy(go, SmokeLifetime + 0.2f);"),
                "[205] 연기 호스트 유한 수명 소멸(FxImpactDustColumn 선례)");
            Assert.That(src, Does.Contain("Object.Destroy(col);   // 시각 전용 — 기존 BombFlash 선례"),
                "[205] 플래시 콜라이더 즉시 제거 — 시각 전용(기존 선례)");

            // 수명 상수 존재 — 무기한 생존 경로 없음
            Assert.That(src, Does.Contain("FlashLifetime = 0.05f"),
                "[205] 플래시 수명 상수 — 무기한 생존 금지");
            Assert.That(src, Does.Contain("FireballLifetime = 0.5f"),
                "[205] 화구 수명 상수 — 무기한 생존 금지");
        }

        // ── ④ 기존 계약 무손상(189/193/195차) ──
        [Test]
        public void V205_Legacy_Pins_Intact()
        {
            string arm = SrcArm();

            // 폭발 시각 부착 계약(194/195/201차 — SoldierBombVisuals201Tests 동일 핀)
            Assert.That(arm, Does.Contain("go.AddComponent<BombExplosionVisual>();"),
                "[205] AddComponent<BombExplosionVisual>() 부착 무손상");
            // [195차 P6] 히트스톱+셰이크 — 다단 VFX와 공존(무수정)
            Assert.That(arm, Does.Contain("HitStopManager.RequestHitStop(0.05f, 0.12f);"),
                "[205] 히트스톱 0.05s 계약 무손상(195차 P6)");
            Assert.That(arm, Does.Contain("CameraShake.Instance.Shake(0.25f, 0.25f);"),
                "[205] 카메라 셰이크 계약 무손상(195차 P6)");
            // [193차] 빨간 폭발범위 텔레그래프 링 + 내부 필
            Assert.That(arm, Does.Contain("ConfigureAimLine(_aimLandingLine, AimLandingSegments, 0.06f, AimPreviewRed, loop: true)"),
                "[205] 빨간 폭발범위 텔레그래프 링 무손상(193차)");
            Assert.That(arm, Does.Contain("AimPreviewRedFill"),
                "[205] 착지 내부 필 무손상(193차)");
            // [189/195차] 착지 링 sin 펄스 2Hz + 호 그레이딩
            Assert.That(arm, Does.Contain("LandingPulseHz = 2f"),
                "[205] 착지 링 sin 펄스 2Hz 무손상(189/195차)");
            Assert.That(arm, Does.Contain("widthCurve = new AnimationCurve("),
                "[205] 호 그레이딩 widthCurve 무손상(195차)");
            // [189차] 폭발 SFX 배선 + [193차] 스탯 피해량 주입(BombDamageConsume193 계약)
            Assert.That(arm, Does.Contain("bomb.explosionSound = Resources.Load<AudioClip>(\"Audio/SFX/SFX_Bomb_Explosion\");"),
                "[205] 폭발 SFX 배선 무손상(189차)");
            Assert.That(arm, Does.Contain("bomb.bombDamage = "),
                "[205] 스탯 기반 피해량 주입 무손상(193차 BombDamageConsume 계약)");
            Assert.That(arm, Does.Contain("GetItemCount(item.id) <= 0"),
                "[205] 무장 개수 게이트 유지(193차 — 205차A 조용한 거부 계약과 공존)");
            // [201차] GLB 비주얼 + 물리 파이프라인 무손상
            Assert.That(arm, Does.Contain("AttachThrownBombVisual(go);"),
                "[205] 투척 폭탄 GLB 비주얼 부착 무손상(201차)");
            Assert.That(arm, Does.Contain("rb.linearVelocity = velocity;"),
                "[205] Rigidbody 초기속도 무손상(201차 물리 파이프라인)");

            string vis = SrcVisual();
            // 기존 폭발 시각 무손상 — 스파크 버스트 + 폭발 트리거 스윙 + fire 블록
            Assert.That(vis, Does.Contain("CombatVFXController.SpawnHitSparks(pos);"),
                "[205] 기존 스파크 버스트 무손상");
            Assert.That(vis, Does.Contain("FxEnvReaction.TriggerExplosionSway(pos);"),
                "[205] 폭발 트리거 스윙 무손상(무번호 73-E2 — FxEnvReactionP73Tests 핀 동일)");
            Assert.That(vis, Does.Contain("GameObject.Destroy(fire, FireLifetime);"),
                "[205] fire 수명 Destroy 무손상(189차 Sfx189Tests 핀 동일)");
            Assert.That(vis, Does.Contain("GetComponentsInChildren<AudioSource>(true)"),
                "[205] 불 VFX 묻은 AudioSource 차단 무손상(189차)");
        }

        /// <summary>문자열 발생 횟수 카운트 — 자가 소멸 경로 소 대조용(BombThrowAnimationTests 선례).</summary>
        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            int idx = haystack.IndexOf(needle, System.StringComparison.Ordinal);
            while (idx >= 0)
            {
                count++;
                idx = haystack.IndexOf(needle, idx + needle.Length, System.StringComparison.Ordinal);
            }
            return count;
        }
    }
}
