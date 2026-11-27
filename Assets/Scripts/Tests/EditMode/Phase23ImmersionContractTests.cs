using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [133차 P2/P3] 실감 피드백 Phase 2(사운드 완성)·Phase 3(세계 생태) 계약 테스트.
    /// 진실 출처: docs/133_IMMERSION_FEEDBACK_PLAN.md §Phase 2/3.
    ///  · P2 소스계약: 발소리 플레이스홀더 실재화(clip 할당), 변형 랜덤+피치, 크래프트/요리/연금 징글 배선.
    ///  · P2 에셋계약: 발소리 표면 변형 12종 + UI음 3종 로드.
    ///  · P3 소스계약: 주민 밤 귀가(이벤트 기반)·교차 정지·나비 스포너(밤 비활성·상한).
    ///  · P3 에셋계약: Resources/Butterfly 빌드 미러 3종(에디터 전용 경로 회피 계약).
    /// </summary>
    public class Phase23ImmersionContractTests
    {
        private const string SfxManagerPath = "Assets/Scripts/Systems/SoundEffectManager.cs";
        private const string CraftingPath = "Assets/Scripts/UI/CraftingUI.cs";
        private const string CookingPath = "Assets/Scripts/UI/CookingUI.cs";
        private const string WandererPath = "Assets/Scripts/Systems/VillagerWanderer.cs";
        private const string ButterflyPath = "Assets/Scripts/Systems/ButterflyAmbientSpawner.cs";

        // ───────────────────────── P2 소스계약 ─────────────────────────

        /// <summary>[133차-P2] 계약 1 — FootstepReal_SourceContract: 발소리 플레이스홀더 실재화.</summary>
        [Test]
        public void FootstepReal_SourceContract_133()
        {
            var src = System.IO.File.ReadAllText(SfxManagerPath);
            // 플레이스홀더 계약 소멸 — PlaySurfacedSFX가 클립을 할당해 재생해야 한다.
            string body = Slice(src, "public void PlaySurfacedSFX", "private AudioClip LoadCachedVariantClip");
            if (body.Length == 0)
                body = Slice(src, "public void PlaySurfacedSFX", "public bool IsPlaying");
            Assert.That(body, Is.Not.Empty, "[133차-P2] PlaySurfacedSFX 영역 슬라이스 실패.");
            Assert.That(body, Does.Contain("source.clip ="),
                "[133차-P2] 발소리 실재화 — source.clip 할당 존재(clip=null 플레이스홀더 소멸).");
            Assert.That(src, Does.Not.Contain("실제 오디오 없음"),
                "[133차-P2] 플레이스홀더 로그 소멸 계약.");
            Assert.That(src, Does.Contain("LoadCachedVariantClip"),
                "[133차-P2] 변형 클립 캐시 로더 존재.");
        }

        /// <summary>[133차-P2] 계약 2 — FootstepVariants_AssetContract: 표면 변형 12종 로드.</summary>
        [Test]
        public void FootstepVariants_AssetContract_133()
        {
            foreach (var surface in new[] { "step_grass", "step_stone", "step_wood", "step_water" })
            {
                int count = 0;
                for (int i = 0; i < 3; i++)
                {
                    var clip = Resources.Load<AudioClip>($"Audio/SFX/SFX_Footstep_{surface}_{i:000}");
                    if (clip != null) count++;
                }
                Assert.That(count, Is.EqualTo(3),
                    $"[133차-P2] 발소리 '{surface}' 변형 3종 로드 — 실측 {count}.");
            }
        }

        /// <summary>[133차-P2] 계약 3 — CraftJingle_SourceContract: 크래프트/요리/연금 징글 배선.</summary>
        [Test]
        public void CraftJingle_SourceContract_133()
        {
            Assert.That(System.IO.File.ReadAllText(CraftingPath), Does.Contain("PlayAcquireJingle"),
                "[133차-P2] CraftingUI 크래프트 성공 징글 배선.");
            Assert.That(System.IO.File.ReadAllText(CookingPath), Does.Contain("PlayAcquireJingle"),
                "[133차-P2] CookingUI 요리 성공 징글 배선.");
            // [144차 D6] AlchemyStationUTK 폐기 → 연금 징글 계약은 살아있는 경로 AlchemyBenchUTK(OpenAlchemyUI 라우트)로 승계.
            var alchemy = System.IO.File.ReadAllText("Assets/Scripts/UI/Toolkit/AlchemyBenchUTK.cs");
            Assert.That(alchemy, Does.Contain("PlayAcquireJingle"),
                "[133차-P2] 연금 성공 징글 배선(144차 폐기 승계 — AlchemyBenchUTK.OnCraftSuccess).");
        }

        /// <summary>[133차-P2] 계약 4 — UISfx_AssetContract: UI 클릭/열기/닫기음 로드.</summary>
        [Test]
        public void UISfx_AssetContract_133()
        {
            Assert.That(Resources.Load<AudioClip>("Sounds/UI/UI_Click"), Is.Not.Null,
                "[133차-P2] UI_Click 로드(클릭 무음 플레이스홀더 해소).");
            Assert.That(Resources.Load<AudioClip>("Sounds/UI/UI_Open"), Is.Not.Null,
                "[133차-P2] UI_Open 로드.");
            Assert.That(Resources.Load<AudioClip>("Sounds/UI/UI_Close"), Is.Not.Null,
                "[133차-P2] UI_Close 로드.");
        }

        // ───────────────────────── P3 소스계약 ─────────────────────────

        /// <summary>[133차-P3] 계약 5 — NightSchedule_SourceContract: 이벤트 기반 밤 귀가 스케줄.</summary>
        [Test]
        public void NightSchedule_SourceContract_133()
        {
            var src = System.IO.File.ReadAllText(WandererPath);
            Assert.That(src, Does.Contain("OnNightStart"), 
                "[133차-P3] TimeManager.OnNightStart 구독(이벤트 기반 — 폴링 아님).");
            Assert.That(src, Does.Contain("OnDayStart"),
                "[133차-P3] TimeManager.OnDayStart 구독(낮 복귀 재배회).");
            Assert.That(src, Does.Contain("_nightReturnWalking"),
                "[133차-P3] 홈 복귀 보행 게이트 존재.");
        }

        /// <summary>[133차-P3] 계약 6 — CrossEncounter_SourceContract: 주민 교차 정지 계약.</summary>
        [Test]
        public void CrossEncounter_SourceContract_133()
        {
            var src = System.IO.File.ReadAllText(WandererPath);
            Assert.That(src, Does.Contain("CrossRegistry"),
                "[133차-P3] 교차 레지스트리 존재(중복 등록 방지).");
            // 조우 계약 수치 — 1.8m 조우 / 2.5초 정지 / 8초 쿨다운(계획 §Phase 3).
            Assert.That(src, Does.Contain("1.8f"), "[133차-P3] 조우 거리 1.8m.");
            Assert.That(src, Does.Contain("2.5f"), "[133차-P3] 정지 시간 2.5초.");
            Assert.That(src, Does.Contain("8f"), "[133차-P3] 쌍 쿨다운 8초.");
        }

        /// <summary>[133차-P3] 계약 7 — ButterflySpawner_SourceContract: 스폰 계약(밤 비활성·결정론).</summary>
        [Test]
        public void ButterflySpawner_SourceContract_133()
        {
            var src = System.IO.File.ReadAllText(ButterflyPath);
            Assert.That(src, Does.Contain("IsNight"),
                "[133차-P3] 나비 밤 비활성(IsNight 질의) 존재.");
            Assert.That(src, Does.Contain("Random"),
                "[133차-P3] 결정론 랜덤(마을 주민 System.Random 규약).");
            Assert.That(src, Does.Contain("ButterflySpawn"),
                "[133차-P3] 에셋팩 자체 스폰 스크립트 제거 계약(부모 영역 부재 방지).");
        }

        /// <summary>[133차-P3] 계약 8 — ButterflyMirror_AssetContract: Resources 빌드 미러 3종.</summary>
        [Test]
        public void ButterflyMirror_AssetContract_133()
        {
            foreach (var name in new[] { "Butterfly_01", "Butterfly_02", "Butterfly_03" })
            {
                var prefab = Resources.Load<GameObject>($"Butterfly/{name}");
                Assert.That(prefab, Is.Not.Null,
                    $"[133차-P3] Resources/Butterfly/{name} 빌드 미러 로드(에디터 AssetDatabase 전용 회피 계약).");
            }
        }

        private static string Slice(string src, string beginAnchor, string endAnchor)
        {
            int begin = src.IndexOf(beginAnchor, System.StringComparison.Ordinal);
            if (begin < 0) return string.Empty;
            int end = src.IndexOf(endAnchor, begin + beginAnchor.Length, System.StringComparison.Ordinal);
            return end > begin ? src.Substring(begin, end - begin) : src.Substring(begin);
        }
    }
}
