using NUnit.Framework;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [170차 C1] 홀로그램 실물 형태 패리티 — 실물 빌더 홀로그램 API(BuildHologramVisual) 라이브 오브젝트 검증.
    /// 실측 파트 수(형태 패리티)·콜라이더 0(StripColliders 계약)·E키 상호작용 컴포넌트 배제·미정의 id null 반환.
    /// EditMode 안전 — Object.DestroyImmediate 정리(126차 함정 계약: Destroy 금지).
    /// </summary>
    public sealed class ConstructionHologramShapeTests
    {
        private static void AssertFacilityShape(GameObject go, int expectedParts, string label)
        {
            Assert.That(go, Is.Not.Null, label + " — 홀로그램 시각 생성.");
            Assert.That(go.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(expectedParts),
                label + " — 실측 파트 수(실물 빌더 형태 패리티).");
            Assert.That(go.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0),
                label + " — 콜라이더 0(StripColliders 계약).");
            Assert.That(go.GetComponentsInChildren<FacilityStationInteractable>(true).Length, Is.EqualTo(0),
                label + " — E키 상호작용 배제(홀로그램 경로 계약).");
        }

        private static void Destroy(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        // ═══════════════ 시설 5종 — 라이브 오브젝트 ═══════════════

        [Test]
        public void P170_Cooking_Hologram_LiveShape_3Parts_NoCollider()
        {
            var go = FacilityPropBuilder.BuildHologramVisual("bp_cooking", NationType.East);
            try { AssertFacilityShape(go, 3, "요리"); }
            finally { Destroy(go); }
        }

        [Test]
        public void P170_Forge_Hologram_LiveShape_4Parts_NoCollider()
        {
            var go = FacilityPropBuilder.BuildHologramVisual("bp_forge", NationType.East);
            try { AssertFacilityShape(go, 4, "장비"); }
            finally { Destroy(go); }
        }

        [Test]
        public void P170_Barracks_Hologram_LiveShape_5Parts_NoCollider()
        {
            var go = FacilityPropBuilder.BuildHologramVisual("bp_barracks", NationType.East);
            try { AssertFacilityShape(go, 5, "병사배치"); }
            finally { Destroy(go); }
        }

        [Test]
        public void P170_Alchemy_Hologram_LiveShape_2Parts_NoCollider()
        {
            var go = FacilityPropBuilder.BuildHologramVisual("bp_alchemy", NationType.East);
            try { AssertFacilityShape(go, 2, "연금술"); }
            finally { Destroy(go); }
        }

        [Test]
        public void P170_TrainingHall_Hologram_LiveShape_10Parts_NoCollider()
        {
            var go = FacilityPropBuilder.BuildHologramVisual("bp_traininghall", NationType.East);
            try { AssertFacilityShape(go, 10, "훈련소"); }
            finally { Destroy(go); }
        }

        // ═══════════════ 음식점 — 라이브 오브젝트 ═══════════════

        [Test]
        public void P170_Restaurant_Hologram_LiveShape_6Parts_NoCollider()
        {
            var go = RestaurantPropBuilder.BuildHologramVisual("bp_restaurant", NationType.East);
            try
            {
                Assert.That(go, Is.Not.Null, "음식점 — 홀로그램 시각 생성.");
                Assert.That(go.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(6),
                    "음식점 — 실측 파트 수 6(Body+Roof4+Door 실물 형태 패리티).");
                Assert.That(go.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(0),
                    "음식점 — 콜라이더 0(홀로그램 스트립 계약).");
                Assert.That(go.GetComponentsInChildren<RestaurantPropInteractable>(true).Length, Is.EqualTo(0),
                    "음식점 — E키 상호작용 배제(홀로그램 경로 계약).");
            }
            finally { Destroy(go); }
        }

        // ═══════════════ 미정의 blueprintId — null 반환(방어 계약) ═══════════════

        [Test]
        public void P170_Facility_Hologram_UndefinedId_ReturnsNull()
        {
            var go = FacilityPropBuilder.BuildHologramVisual("bp_wall", NationType.East);
            try { Assert.That(go, Is.Null, "시설 — 미정의 blueprintId null 반환(실물 경로 default 방어 동일)."); }
            finally { Destroy(go); }
        }

        [Test]
        public void P170_Restaurant_Hologram_UndefinedId_ReturnsNull()
        {
            var go = RestaurantPropBuilder.BuildHologramVisual("bp_wall", NationType.East);
            try { Assert.That(go, Is.Null, "음식점 — 미정의 blueprintId null 반환(bp_restaurant 전용 게이트)."); }
            finally { Destroy(go); }
        }
    }
}
