using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [A5 이계] 봉헌(탈주) 결정론 EditMode 테스트 (77차 A5).
    /// 검증: 비활성 무연산 / 수확 50% 절반 / 봉헌 20% 차감(소유 골드) / 0골드 무료 탈주.
    /// 활성화는 리플렉션(IsActive private set — 75차 P18 표준). 씬 경로는 Application.isPlaying 가드로 편집모드 무해.
    /// </summary>
    public sealed class AfterlifeSurrenderTests
    {
        private static readonly PropertyInfo ActiveProp =
            typeof(AfterlifeSurvivalSystem).GetProperty("IsActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        private GameObject _statsGo;   // 누수 방지 — TearDown에서 파괴(75차 도메인 위생)

        [SetUp]
        public void SetUp()
        {
            AfterlifeSurvivalSystem.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            AfterlifeSurvivalSystem.ResetAll();
            // 도메인 위생 — 리플렉션으로 세팅한 PlayerStats.Instance 원복 + 테스트 GO 파괴
            //   (GO 누수 시 후속 파일의 FindAnyObjectByType 폴백이 골드 잔존 컴포넌트를 오발견 — ConstructionTests 파생 실패 실측)
            if (_statsGo != null)
            {
                Object.DestroyImmediate(_statsGo);
                _statsGo = null;
            }
            typeof(PlayerStats).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);
        }

        [Test]
        public void Surrender_InactiveRun_NoChange()
        {
            Assert.That(AfterlifeSurvivalSystem.IsActive, Is.False);
            AfterlifeSurvivalSystem.Surrender();

            Assert.That(AfterlifeSurvivalSystem.IsActive, Is.False, "비활성 런 — 무연산");
            Assert.That(TimeManager.IsTimeFrozen, Is.False, "시간 동결 없음");
            Assert.AreEqual(0, AfterlifeSurvivalSystem.RunGold);
            Assert.AreEqual(0, AfterlifeSurvivalSystem.RunXP);
        }

        [Test]
        public void Surrender_HalvesHarvest_AndEndsRun()
        {
            ActivateRun();
            AfterlifeSurvivalSystem.NotifyOrbAbsorbed(AfterlifeOrb.OrbType.Gold, 10);
            AfterlifeSurvivalSystem.NotifyOrbAbsorbed(AfterlifeOrb.OrbType.XP, 9);

            AfterlifeSurvivalSystem.Surrender();

            // PlayerStats 없음 — 정산 스킵 → RunGold/RunXP는 정산 후 리셋으로 0
            Assert.AreEqual(0, AfterlifeSurvivalSystem.RunGold, "골드 정산 스킵 + 리셋");
            Assert.AreEqual(0, AfterlifeSurvivalSystem.RunXP, "XP 정산 스킵 + 리셋");
            Assert.That(AfterlifeSurvivalSystem.IsActive, Is.False, "즉시 귀환 — 런 종료");
            Assert.That(TimeManager.IsTimeFrozen, Is.False, "시간 복원");
        }

        [Test]
        public void Surrender_Tributes20PercentOfOwnedGold()
        {
            _statsGo = new GameObject("AfterlifeTest_Stats");
            var stats = _statsGo.AddComponent<PlayerStats>();
            typeof(PlayerStats).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, stats); // Awake 미실행 — 리플렉션 세팅 (75차 P18 패턴)
            stats.AddGold(100, "test_seed");

            ActivateRun();
            AfterlifeSurvivalSystem.NotifyOrbAbsorbed(AfterlifeOrb.OrbType.Gold, 10);
            AfterlifeSurvivalSystem.NotifyOrbAbsorbed(AfterlifeOrb.OrbType.XP, 9);

            AfterlifeSurvivalSystem.Surrender();

            Assert.AreEqual(85, stats.Gold, "봉헌 20G 차감(100→80) + 수확 절반 5G 정산 지급 = 85");
            Assert.AreEqual(4, stats.CurrentEXP, "XP 절반 정산 — 9 → 4 (floor 4.5)");
        }

        [Test]
        public void Surrender_ZeroGold_NoTribute()
        {
            _statsGo = new GameObject("AfterlifeTest_StatsBroke");
            var stats = _statsGo.AddComponent<PlayerStats>();
            typeof(PlayerStats).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, stats);
            stats.AddGold(4, "test_seed"); // 4 × 0.2 = 0.8 → floor 0 — 봉헌 없음

            ActivateRun();
            AfterlifeSurvivalSystem.Surrender();

            Assert.AreEqual(4, stats.Gold, "0골드 봉헌 — 골드 무변동");
            Assert.That(AfterlifeSurvivalSystem.IsActive, Is.False, "탈주는 항상 가능(소프트락 없음)");
        }

        private static void ActivateRun()
        {
            ActiveProp?.SetValue(null, true); // IsActive = true — 시스템이 아닌 테스트에서만
        }
    }
}
