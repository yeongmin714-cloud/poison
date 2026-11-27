using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [166-B/C] 공격 스윙별 판정 + Slash VFX 발화 계약 — 판정 순수 함수(스윙 임팩트 테이블 조회)
    /// + 드라이버/러너 배선 소스 어서션(HudBottomAnchorTests·HotbarHoldKeyTests 선례).
    /// 스윙 정점 상수(0.30/0.75)는 Play 실측 트림 대상 — 본 테스트는 구조 계약만 고정한다.
    /// </summary>
    public class AttackSwingJudgmentContractTests
    {
        // ── [166-B] 무기별 스윙 임팩트 테이블 (internal — InternalsVisibleTo(ProjectName.Tests.EditMode) 선례) ──

        [Test]
        public void SwingImpactTable_Spear_TwoSwings()
        {
            var spear = HumanoidClipDriver.LegacySwingImpactTable[WeaponType.Spear];
            Assert.That(spear.Length, Is.EqualTo(2), "찌르기(오프너) 클립 = 휘두르기 2회 — 스윙별 임팩트 2개.");
            Assert.That(spear[0], Is.EqualTo(0.30f), "스윙 1 정점 초기값(Play 트림 대상).");
            Assert.That(spear[1], Is.EqualTo(0.75f), "스윙 2 정점 초기값(Play 트림 대상).");
        }

        [Test]
        public void SwingImpactLookup_TableAndFallback()
        {
            Assert.That(HumanoidClipDriver.GetLegacySwingImpactNormT(WeaponType.Spear, 0), Is.EqualTo(0.30f));
            Assert.That(HumanoidClipDriver.GetLegacySwingImpactNormT(WeaponType.Spear, 1), Is.EqualTo(0.75f));
            // 인덱스 초과 → 폴백(단일 시점 0.50) — 기존 동작 보존.
            Assert.That(HumanoidClipDriver.GetLegacySwingImpactNormT(WeaponType.Spear, 2), Is.EqualTo(0.50f));
            // 미등록 무기 → 폴백 0.50 + 스윙 수 1(기존 단일 임팩트 동작 유지).
            Assert.That(HumanoidClipDriver.GetLegacySwingImpactNormT(WeaponType.Sword, 0), Is.EqualTo(0.50f));
            Assert.That(HumanoidClipDriver.GetLegacySwingImpactCount(WeaponType.Sword), Is.EqualTo(1));
            Assert.That(HumanoidClipDriver.GetLegacySwingImpactCount(WeaponType.Spear), Is.EqualTo(2));
        }

        // ── 배선 소스 어서션 (HudBottomAnchorTests 선례) ──

        [Test]
        public void HumanoidClipDriver_MultiImpactWiring_Present()
        {
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/HumanoidClipDriver.cs");
            Assert.That(src, Does.Contain("[166-B]"),
                "166-B 스윙별 멀티임팩트 마커가 있어야 한다.");
            Assert.That(src, Does.Contain("_combat?.ResolveComboSwingHit(1)"),
                "오프너 스윙 1 판정(ResolveComboSwingHit 재사용)이 있어야 한다.");
            Assert.That(src, Does.Contain("_combat?.ResolveComboSwingHit(2)"),
                "오프너 스윙 2 판정(신규 — 휘두르기 2회마다 1판정)이 있어야 한다.");
            Assert.That(src, Does.Contain("ShouldResolveSpearOpenerSwing2"),
                "스윙 2 상태 게이트(45차 계약 동형 — 시간 게이트와 이중 가드)가 있어야 한다.");
            // [176차] 스윙 2 자동 시간 게이트 철수(2클릭 콤보 전환) — 팔로업 판정 핀으로 교체.
            Assert.That(src, Does.Contain("ShouldResolveSpearFollowupHit"),
                "스윙 2는 2클릭 승인 팔로업 게이트(ShouldResolveSpearFollowupHit)로만 해결되어야 한다.");
            Assert.That(src, Does.Contain("[166-C]"),
                "166-C Slash VFX 스윙별 발화 마커가 있어야 한다.");
            Assert.That(src, Does.Contain("FireComboSlash(swing + 1)"),
                "레거시 Attack* 경로도 스윙별 Slash VFX를 발화해야 한다.");
        }

        [Test]
        public void SlashVfxRunner_SwingPrefabResource_Present()
        {
            Assert.That(System.IO.File.Exists("Assets/Resources/FX/Slash/Slash VFX.prefab"),
                "Free Slash VFX 'Slash VFX.prefab'가 Resources/FX/Slash에 복제돼 있어야 한다(런타임 로드 경로).");
            Assert.That(System.IO.File.Exists("Assets/Resources/FX/Slash/Slash VFX.prefab.meta"), Is.True);
            var src = System.IO.File.ReadAllText("Assets/Scripts/Systems/SlashVFXRunner.cs");
            Assert.That(src, Does.Contain("FX/Slash/Slash VFX"),
                "SlashVFXRunner가 Slash VFX 프리팹을 로드해야 한다(휘두르는 방향 부착 경유).");
        }
    }
}
