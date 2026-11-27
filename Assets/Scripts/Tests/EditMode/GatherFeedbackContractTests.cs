using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [133차] 채집 피드백 계약 테스트 — 소스계약 + 에셋계약.
    /// 진실 출처: docs/133_IMMERSION_FEEDBACK_PLAN.md §Phase 1.
    ///  · 소스계약: GatherFeedbackSystem API, 채널 타격 이벤트, 5지점 배선(벌목/채광/채집/낚시/물뿌리개),
    ///    획득 징글, 카메라 셰이크 공개 전환.
    ///  · 에셋계약: 사운드 17종(Resources/Sounds/SFX/Gather) + VFX 프리펩 5종·링 텍스처(Resources/VFX/Gather)
    ///    Resources.Load 실측(RollDirectionsAnimSetup 배치 후 기준 — 에셋계약은 빌더 실행 후 GREEN).
    /// </summary>
    public class GatherFeedbackContractTests
    {
        private const string FeedbackPath = "Assets/Scripts/Systems/GatherFeedbackSystem.cs";
        private const string ChannelPath = "Assets/Scripts/Systems/GatherChannelSystem.cs";
        private const string NodePath = "Assets/Scripts/Systems/ResourceNode.cs";
        private const string HerbPath = "Assets/Scripts/Systems/HerbPickup.cs";
        private const string FishingPath = "Assets/Scripts/Systems/FishingSystem.cs";
        private const string FarmPath = "Assets/Scripts/Systems/FarmPlot.cs";
        private const string BridgePath = "Assets/Scripts/Systems/HarvestResultBridge.cs";
        private const string MovementPath = "Assets/Scripts/Systems/PlayerMovement.cs";

        // ───────────────────────── 소스계약 ─────────────────────────

        /// <summary>[133차] 계약 1 — FeedbackApi_SourceContract: 피드백 시스템 공개 API 존재.</summary>
        [Test]
        public void FeedbackApi_SourceContract_133()
        {
            var src = System.IO.File.ReadAllText(FeedbackPath);
            Assert.That(src, Does.Contain("public enum GatherFeedbackKind"),
                "[133차] GatherFeedbackKind enum 존재.");
            // [153차 P2] Soil 멤버 추가(농경 흙 타격 — 굉이질/정리/파종) — 기존 7종 유지.
            foreach (var kind in new[] { "Wood", "Mine", "Herb", "Soil", "FishBite", "FishCast", "WaterPour", "Acquire" })
                Assert.That(src, Does.Contain(kind + ","),
                    $"[133차] GatherFeedbackKind 멤버 '{kind}' 존재.");
            Assert.That(src, Does.Contain("public static void PlayImpact(GatherFeedbackKind kind, Vector3 position, bool shake"),
                "[133차] PlayImpact(kind, position, shake) 시그니처 존재.");
            Assert.That(src, Does.Contain("public static void PlayAcquireJingle"),
                "[133차] PlayAcquireJingle(등급별 피치 변주) 존재.");
            // 발화 계약 — playOnAwake=false 프리펩이므로 명시 Play() 필수(교차 브랜치 정합).
            Assert.That(src, Does.Contain("ps.Play()"),
                "[133차] 스폰 파티클 명시 Play() 호출 존재(playOnAwake=false 계약).");
        }

        /// <summary>[133차] 계약 2 — ChannelImpactEvent_SourceContract: 채널 타격 리듬 이벤트 존재.</summary>
        [Test]
        public void ChannelImpactEvent_SourceContract_133()
        {
            var src = System.IO.File.ReadAllText(ChannelPath);
            Assert.That(src, Does.Contain("public event Action OnImpact"),
                "[133차] GatherChannelSystem.OnImpact 이벤트 존재.");
            Assert.That(src, Does.Contain("OnImpact?.Invoke()"),
                "[133차] 타격 리듬 구간 통과 시 발화 배선 존재.");
        }

        /// <summary>[133차] 계약 3 — HookWiring_SourceContract: 5지점 배선 존재.</summary>
        [Test]
        public void HookWiring_SourceContract_133()
        {
            Assert.That(System.IO.File.ReadAllText(NodePath), Does.Contain("GatherFeedbackSystem.PlayImpact"),
                "[133차] ResourceNode(벌목/채광) 타격 배선 존재.");
            Assert.That(System.IO.File.ReadAllText(HerbPath), Does.Contain("GatherFeedbackSystem.PlayImpact"),
                "[133차] HerbPickup(채집) 타격 배선 존재.");
            var fishing = System.IO.File.ReadAllText(FishingPath);
            Assert.That(fishing, Does.Contain("GatherFeedbackKind.FishCast"),
                "[133차] FishingSystem 출조/성공 배선 존재.");
            Assert.That(fishing, Does.Contain("GatherFeedbackKind.FishBite"),
                "[133차] FishingSystem 입질 배선 존재.");
            Assert.That(System.IO.File.ReadAllText(FarmPath), Does.Contain("GatherFeedbackKind.WaterPour"),
                "[133차] FarmPlot 물뿌리개 배선 존재.");
            var bridge = System.IO.File.ReadAllText(BridgePath);
            Assert.That(bridge, Does.Contain("PlayAcquireJingle"),
                "[133차] HarvestResultBridge.Publish 획득 징글 배선 존재.");
        }

        /// <summary>[133차] 계약 4 — ShakePublic_SourceContract: 카메라 셰이크 공개 전환(흔들림 재활용 계약).</summary>
        [Test]
        public void ShakePublic_SourceContract_133()
        {
            var src = System.IO.File.ReadAllText(MovementPath);
            Assert.That(src, Does.Contain("public void TriggerCameraShake"),
                "[133차] TriggerCameraShake public 전환(133차 피드백 재활용).");
        }

        /// <summary>[153차 P2] 계약 5 — SoilMapping_SourceContract: 농경 흙 타격 매핑 존재.
        /// WaterPour 기존 계약(프리펩 부재 시 조용한 스킵+로그 1회)은 변경 없음.</summary>
        [Test]
        public void SoilMapping_SourceContract_153()
        {
            var src = System.IO.File.ReadAllText(FeedbackPath);
            Assert.That(src, Does.Contain("SFX_Gather_Soil"),
                "[153차 P2] Soil 클립 프리픽스 매핑 존재.");
            Assert.That(src, Does.Contain("GatherVFX_SoilBurst"),
                "[153차 P2] Soil VFX 프리펩명 매핑 존재.");
        }

        // ───────────────────────── 에셋계약 ─────────────────────────

        /// <summary>[133차] 계약 5 — GatherSfx_AssetContract: 사운드 17종 Resources 로드.</summary>
        [Test]
        public void GatherSfx_AssetContract_133()
        {
            var clips = Resources.LoadAll<AudioClip>("Sounds/SFX/Gather");
            Assert.That(clips.Length, Is.EqualTo(17),
                "[133차] 채집 사운드 17종(SFX_Gather_* 8 / SFX_Fish_* 5 / SFX_Water_Pour 2 / SFX_Acquire_Common / SFX_Coin) 로드.");
            foreach (var prefix in new[] { "SFX_Gather_Wood", "SFX_Gather_Mine", "SFX_Gather_Herb",
                                            "SFX_Fish_Bite", "SFX_Fish_Cast", "SFX_Water_Pour" })
            {
                int count = clips.Count(c => c.name.StartsWith(prefix));
                Assert.That(count, Is.GreaterThanOrEqualTo(2),
                    $"[133차] '{prefix}' 변형 2종 이상(랜덤 반복 방지 계약) — 실측 {count}.");
            }
            Assert.That(clips.Any(c => c.name == "SFX_Acquire_Common"), Is.True,
                "[133차] 획득 징글 클립 존재.");
        }

        /// <summary>[133차] 계약 6 — GatherVfx_AssetContract: VFX 프리펩 5종 + 링 텍스처 로드.</summary>
        [Test]
        public void GatherVfx_AssetContract_133()
        {
            // [153차 P2] GatherVFX_SoilBurst 추가(농경 흙 타격 — 빌더 배치 후 GREEN).
            foreach (var name in new[] { "GatherVFX_WoodChips", "GatherVFX_StoneFragments", "GatherVFX_LeafPuff",
                                         "GatherVFX_WaterDroplets", "GatherVFX_FishRipple", "GatherVFX_SoilBurst" })
            {
                var prefab = Resources.Load<GameObject>($"VFX/Gather/{name}");
                Assert.That(prefab, Is.Not.Null, $"[133차] VFX 프리펩 '{name}' 로드(빌더 배치 후 GREEN).");
                var ps = prefab.GetComponent<ParticleSystem>();
                Assert.That(ps, Is.Not.Null, $"[133차] '{name}'에 ParticleSystem 존재.");
                Assert.That(ps.main.playOnAwake, Is.False,
                    $"[133차] '{name}' playOnAwake=false — 호출부 명시 Play() 계약.");
            }
            var ring = Resources.Load<Texture2D>("VFX/Gather/FX_Ring");
            Assert.That(ring, Is.Not.Null, "[133차] FX_Ring.png(절차 생성 물결 링 텍스처) 로드.");
        }
    }
}
