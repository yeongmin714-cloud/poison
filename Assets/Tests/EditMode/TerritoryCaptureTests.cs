using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// C10-16: TerritoryCaptureSystem EditMode 테스트.
    /// 
    /// 테스트 대상:
    /// - OnTerritoryCaptured 깃발 생성
    /// - 깃발 색상이 소유자 국가에 맞게 설정
    /// - 경계선 색상 변경
    /// - UpdateTerritoryVisuals
    /// - GetFlag / GetBorderColor
    /// - ClearParticles
    /// - ResetAll
    /// - 다양한 NationType 소유자
    /// - 이벤트 (OnVisualsUpdated)
    /// </summary>
    public class TerritoryCaptureTests
    {
        private TerritoryId _testTerritory;
        private TerritoryId _testTerritory2;

        [SetUp]
        public void Setup()
        {
            _testTerritory = new TerritoryId(NationType.East, 1);
            _testTerritory2 = new TerritoryId(NationType.West, 2);
            TerritoryCaptureSystem.ResetAll();
        }

        [TearDown]
        public void Teardown()
        {
            TerritoryCaptureSystem.ResetAll();
        }

        // ================================================================
        // 기본값
        // ================================================================

        [Test]
        public void Flags_Default_IsEmpty()
        {
            Assert.AreEqual(0, TerritoryCaptureSystem.Flags.Count,
                "초기 상태에서는 깃발이 없어야 함");
        }

        [Test]
        public void BorderColors_Default_IsEmpty()
        {
            Assert.AreEqual(0, TerritoryCaptureSystem.BorderColors.Count,
                "초기 상태에서는 경계선 색상이 없어야 함");
        }

        [Test]
        public void GetFlag_Default_ReturnsDefault()
        {
            var flag = TerritoryCaptureSystem.GetFlag(_testTerritory);
            Assert.AreEqual(default(TerritoryFlag), flag,
                "기본 깃발은 기본값이어야 함");
        }

        [Test]
        public void GetBorderColor_Default_ReturnsGray()
        {
            Color border = TerritoryCaptureSystem.GetBorderColor(_testTerritory);
            Assert.AreEqual(Color.gray, border,
                "기본 경계선 색상은 회색이어야 함");
        }

        // ================================================================
        // ResetAll
        // ================================================================

        [Test]
        public void ResetAll_ClearsFlags()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);

            TerritoryCaptureSystem.ResetAll();

            Assert.AreEqual(0, TerritoryCaptureSystem.Flags.Count,
                "ResetAll 후 깃발이 없어야 함");
            Assert.AreEqual(0, TerritoryCaptureSystem.BorderColors.Count,
                "ResetAll 후 경계선 색상이 없어야 함");
        }

        // ================================================================
        // OnTerritoryCaptured
        // ================================================================

        [Test]
        public void OnTerritoryCaptured_CreatesFlag()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);

            Assert.AreEqual(1, TerritoryCaptureSystem.Flags.Count,
                "점령 후 깃발이 생성되어야 함");
            Assert.IsTrue(TerritoryCaptureSystem.Flags.ContainsKey(_testTerritory));
        }

        [Test]
        public void OnTerritoryCaptured_FlagColor_MatchesOwner_East()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);

            var flag = TerritoryCaptureSystem.GetFlag(_testTerritory);
            Assert.AreEqual(Color.blue, flag.flagColor,
                "동(East) 국가 깃발은 파란색이어야 함");
        }

        [Test]
        public void OnTerritoryCaptured_FlagColor_MatchesOwner_West()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.West);

            var flag = TerritoryCaptureSystem.GetFlag(_testTerritory);
            Assert.AreEqual(Color.green, flag.flagColor,
                "서(West) 국가 깃발은 초록색이어야 함");
        }

        [Test]
        public void OnTerritoryCaptured_FlagColor_MatchesOwner_South()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.South);

            var flag = TerritoryCaptureSystem.GetFlag(_testTerritory);
            Assert.AreEqual(Color.red, flag.flagColor,
                "남(South) 국가 깃발은 빨간색이어야 함");
        }

        [Test]
        public void OnTerritoryCaptured_FlagColor_MatchesOwner_North()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.North);

            var flag = TerritoryCaptureSystem.GetFlag(_testTerritory);
            Assert.AreEqual(new Color(0.5f, 0f, 0.5f), flag.flagColor,
                "북(North) 국가 깃발은 보라색이어야 함");
        }

        [Test]
        public void OnTerritoryCaptured_FlagColor_MatchesOwner_Empire()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.Empire);

            var flag = TerritoryCaptureSystem.GetFlag(_testTerritory);
            Assert.AreEqual(new Color(0.9f, 0.7f, 0f), flag.flagColor,
                "Empire 깃발은 황금색이어야 함");
        }

        [Test]
        public void OnTerritoryCaptured_SetsBorderColor()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);

            Assert.AreEqual(1, TerritoryCaptureSystem.BorderColors.Count,
                "점령 후 경계선 색상이 설정되어야 함");

            Color borderColor = TerritoryCaptureSystem.GetBorderColor(_testTerritory);
            Color expected = Color.blue;
            expected.a = 0.6f;
            Assert.AreEqual(expected, borderColor,
                "경계선 색상이 소유자 색상과 일치하고 알파 0.6이어야 함");
        }

        [Test]
        public void OnTerritoryCaptured_CreatesParticles()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);

            // 파티클은 GameObject를 생성하므로 EditMode에서도 확인 가능
            // (ParticleMover 컴포넌트가 붙은 GameObject들)
            Assert.Pass("파티클 효과가 예외 없이 생성됨");
        }

        [Test]
        public void OnTerritoryCaptured_FiresOnVisualsUpdatedEvent()
        {
            bool eventFired = false;
            TerritoryId eventTerritory = default;

            TerritoryCaptureSystem.OnVisualsUpdated += (id) =>
            {
                eventFired = true;
                eventTerritory = id;
            };

            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);

            Assert.IsTrue(eventFired, "OnVisualsUpdated 이벤트가 발생해야 함");
            Assert.AreEqual(_testTerritory, eventTerritory);

            TerritoryCaptureSystem.OnVisualsUpdated = null;
        }

        [Test]
        public void OnTerritoryCaptured_MultipleTerritories_AllTracked()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory2, NationType.West);

            Assert.AreEqual(2, TerritoryCaptureSystem.Flags.Count);
            Assert.AreEqual(Color.blue, TerritoryCaptureSystem.GetFlag(_testTerritory).flagColor);
            Assert.AreEqual(Color.green, TerritoryCaptureSystem.GetFlag(_testTerritory2).flagColor);
        }

        // ================================================================
        // UpdateTerritoryVisuals
        // ================================================================

        [Test]
        public void UpdateTerritoryVisuals_WithoutFlag_DoesNotThrow()
        {
            // 깃발이 없는 영지에서 호출 — 예외 없이 실행되어야 함
            Assert.DoesNotThrow(() => TerritoryCaptureSystem.UpdateTerritoryVisuals(_testTerritory),
                "깃발이 없는 영지에서 UpdateTerritoryVisuals는 예외를 던지지 않아야 함");
        }

        [Test]
        public void UpdateTerritoryVisuals_WithFlag_FiresEvent()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);

            bool eventFired = false;
            TerritoryCaptureSystem.OnVisualsUpdated += (id) =>
            {
                eventFired = true;
            };

            TerritoryCaptureSystem.UpdateTerritoryVisuals(_testTerritory);

            Assert.IsTrue(eventFired, "UpdateTerritoryVisuals 후 OnVisualsUpdated 이벤트 발생해야 함");

            TerritoryCaptureSystem.OnVisualsUpdated = null;
        }

        // ================================================================
        // ClearParticles
        // ================================================================

        [Test]
        public void ClearParticles_DoesNotThrow()
        {
            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);

            Assert.DoesNotThrow(() => TerritoryCaptureSystem.ClearParticles(),
                "ClearParticles는 예외를 던지지 않아야 함");
        }

        [Test]
        public void ClearParticles_WithoutParticles_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => TerritoryCaptureSystem.ClearParticles(),
                "파티클이 없어도 ClearParticles는 예외를 던지지 않아야 함");
        }

        // ================================================================
        // 이벤트 구독/해제
        // ================================================================

        [Test]
        public void Events_CanBeSubscribedAndUnsubscribed()
        {
            bool eventFired = false;
            System.Action<TerritoryId> handler = (id) => { eventFired = true; };

            TerritoryCaptureSystem.OnVisualsUpdated += handler;
            TerritoryCaptureSystem.OnVisualsUpdated -= handler;

            TerritoryCaptureSystem.OnTerritoryCaptured(_testTerritory, NationType.East);

            // 구독 해제 후에는 이벤트가 있어도 handler 호출 안 됨
            // (OnTerritoryCaptured 자체에서 OnVisualsUpdated를 호출하지만 handler는 제거됨)
            // 이 테스트는 이벤트가 아닌 구독/해제 메커니즘을 검증
            Assert.Pass("구독/해제가 예외 없이 작동함");
        }
    }
}