using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-14 폴백 가구 품질 패스] 계약 테스트.
    /// - 폴백(프리미티브) 가구의 정제 디테일(트림/에이프런/철 코너/소품) 존재 + 렌더러 전용(콜라이더 없음).
    /// - 침대 상호작용 계약(Bed + BoxCollider isTrigger) 불변 — 폴백 디테일 추가가 건드리지 않는다.
    /// - 상자/거울 폴백의 디테일 옵트아웃(서랍/책 줄 미생성).
    /// - 푸트프린트 계약: 모든 자식은 기존 치수 안쪽(소품 상부 오버행 제외)에 배치, 바닥 침투 없음.
    /// GLB 경로는 에셋 존재 여부에 따라 결과가 달라지므로 의도적으로 검증하지 않는다(플래키 방지).
    /// </summary>
    public sealed class IndoorFurnitureFallbackQualityTests
    {
        private static void AssertRendererOnly(GameObject go, string label)
        {
            Assert.That(go.GetComponent<Collider>(), Is.Null, label + " — 장식은 콜라이더가 없어야 한다(렌더러 전용).");
            Assert.That(go.GetComponent<MeshRenderer>(), Is.Not.Null, label + " — 메시 렌더러 필요.");
            Assert.That(go.GetComponent<MeshRenderer>().sharedMaterial, Is.Not.Null, label + " — 머티리얼 할당 필요.");
        }

        /// <summary>모든 자식이 루트 푸트프린트 내부(XZ)에 있고 바닥 아래로 침투하지 않는지 검사.</summary>
        private static void AssertFootprint(GameObject root, float width, float depth, float minY, float maxY)
        {
            foreach (Transform child in root.transform)
            {
                Vector3 p = child.localPosition;
                Vector3 s = child.localScale;
                Assert.That(Mathf.Abs(p.x) + s.x * 0.5f, Is.LessThanOrEqualTo(width * 0.5f + 0.03f),
                    child.name + " — X 푸트프린트 이탈.");
                Assert.That(Mathf.Abs(p.z) + s.z * 0.5f, Is.LessThanOrEqualTo(depth * 0.5f + 0.03f),
                    child.name + " — Z 푸트프린트 이탈.");
                Assert.That(p.y - s.y * 0.5f, Is.GreaterThanOrEqualTo(minY), child.name + " — 바닥 침투.");
                Assert.That(p.y + s.y * 0.5f, Is.LessThanOrEqualTo(maxY), child.name + " — 천장/과도 상부 돌출.");
            }
        }

        [Test]
        public void Fallback_Table_RefinedDetails_WithinFootprint_RendererOnlyDecor()
        {
            GameObject table = IndoorFurniturePlacer.CreateTable(1.6f, 0.9f, 0.95f, null);
            Assert.That(table, Is.Not.Null);
            Assert.That(table.name, Is.EqualTo("Table"), "기존 루트 이름 계약 유지.");
            try
            {
                Assert.That(table.transform.Find("TableTop"), Is.Not.Null, "기존 상판 유지.");
                Assert.That(table.transform.Find("Leg_FrontLeft"), Is.Not.Null, "기존 다리 명명 유지.");

                string[] decor =
                {
                    "TopTrim", "Apron_Front", "Apron_Back", "Apron_Left", "Apron_Right",
                    "CornerPlate_FL", "CornerPlate_FR", "CornerPlate_BL", "CornerPlate_BR",
                    "Props_BookCover_Lower", "Props_Pages", "Props_BookCover_Upper",
                    "Props_Paper", "Props_Bottle", "Props_BottleNeck"
                };
                foreach (string name in decor)
                {
                    Transform t = table.transform.Find(name);
                    Assert.That(t, Is.Not.Null, name + " — 폴백 디테일 존재.");
                    AssertRendererOnly(t.gameObject, name);
                }
                // 푸트프린트 내 배치 + 바닥 침투 없음. 상부는 소품(병 꼭대기 ≈ height+0.23)까지 허용.
                AssertFootprint(table, 1.6f, 0.9f, -0.005f, 0.95f + 0.25f);
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void Fallback_Bed_InteractionContract_Unchanged_WithClothDetail()
        {
            GameObject bed = IndoorFurniturePlacer.CreateBed(1.2f, 2.0f, null);
            Assert.That(bed, Is.Not.Null);
            Assert.That(bed.name, Is.EqualTo("Bed"), "기존 루트 이름 계약 유지.");
            try
            {
                // 상호작용 계약 불변(C16-02): Bed 컴포넌트 + BoxCollider(isTrigger).
                var bedComponent = bed.GetComponent<Bed>();
                Assert.That(bedComponent, Is.Not.Null, "Bed 컴포넌트 유지(E키 상호작용).");
                BoxCollider bc = bed.GetComponent<BoxCollider>();
                Assert.That(bc, Is.Not.Null, "BoxCollider 유지.");
                Assert.That(bc.isTrigger, Is.True, "isTrigger 계약.");
                Assert.That(bc.center, Is.EqualTo(new Vector3(0f, 0.3f, 0f)), "콜라이더 중심 불변.");
                Assert.That(bc.size, Is.EqualTo(new Vector3(1.2f, 0.6f, 2.0f)), "콜라이더 크기 불변.");

                // 직물 디테일 — 렌더러 전용.
                foreach (string name in new[] { "Blanket", "Blanket_Fold", "FrameCorner_FL", "FrameCorner_BR" })
                {
                    Transform t = bed.transform.Find(name);
                    Assert.That(t, Is.Not.Null, name + " — 침대 디테일 존재.");
                    AssertRendererOnly(t.gameObject, name);
                }
                Assert.That(bed.transform.Find("Mattress"), Is.Not.Null, "기존 매트리스 유지.");
                Assert.That(bed.transform.Find("Pillow"), Is.Not.Null, "기존 베개 유지.");

                // 모든 자식 콜라이더는 기존과 동일한 BoxCollider 타입(새 콜라이더 유형 추가 없음).
                foreach (Transform child in bed.transform)
                {
                    Collider c = child.GetComponent<Collider>();
                    if (c == null) continue;
                    Assert.That(c, Is.TypeOf<BoxCollider>(), child.name + " — 콜라이더 유형 불변(BoxCollider).");
                }
                // 직물/코너 상부는 상호작용 콜라이더(상단 0.6) 안쪽.
                AssertFootprint(bed, 1.2f, 2.0f, -0.005f, 0.62f);
            }
            finally
            {
                Object.DestroyImmediate(bed);
            }
        }

        [Test]
        public void Fallback_DetailOptOut_CrateAndMirrorSilhouettes()
        {
            // 상자 폴백(detailed=false): 서랍 없음, 트림/철 코너는 유지.
            GameObject counter = IndoorFurniturePlacer.CreateCounter(1.5f, 1.0f, 0.9f, null, false);
            Assert.That(counter, Is.Not.Null);
            try
            {
                Assert.That(counter.transform.Find("Drawer_Face"), Is.Null, "옵트아웃 시 서랍 패이스 미생성.");
                Assert.That(counter.transform.Find("Drawer_Knob"), Is.Null, "옵트아웃 시 서랍 손잡이 미생성.");
                Assert.That(counter.transform.Find("TopTrim"), Is.Not.Null, "트림은 옵트아웃 대상이 아니다.");
            }
            finally
            {
                Object.DestroyImmediate(counter);
            }

            // 거울 폴백(detailed=false): 책 줄/병 없음, 후면 패널/상단 캡 유지.
            GameObject shelf = IndoorFurniturePlacer.CreateShelf(1.8f, 1.8f, 0.6f, null, 3, false);
            Assert.That(shelf, Is.Not.Null);
            try
            {
                foreach (Transform child in shelf.transform)
                    Assert.That(child.name.StartsWith("Book_"), Is.False, "옵트아웃 시 책 미생성.");
                Assert.That(shelf.transform.Find("DecantBottle"), Is.Null, "옵트아웃 시 병 미생성.");
                Assert.That(shelf.transform.Find("BackPanel"), Is.Not.Null, "후면 패널은 유지.");
                Assert.That(shelf.transform.Find("Shelf_Top"), Is.Not.Null, "상단 캡은 유지.");
            }
            finally
            {
                Object.DestroyImmediate(shelf);
            }
        }

        [Test]
        public void Fallback_Chair_SeatCushion_RendererOnly()
        {
            GameObject chair = IndoorFurniturePlacer.CreateChair(1.0f, null);
            Assert.That(chair, Is.Not.Null);
            Assert.That(chair.name, Is.EqualTo("Chair"), "기존 루트 이름 계약 유지.");
            try
            {
                // 기존 구조 부위(Seat/Backrest)는 콜라이더 보존 계약 — 존재만 검사.
                foreach (string name in new[] { "Seat", "Backrest" })
                {
                    Assert.That(chair.transform.Find(name), Is.Not.Null, name + " — 기존 구조 부위 유지.");
                }
                // 신규 장식 부위는 렌더러 전용(콜라이더 없음) 계약.
                foreach (string name in new[] { "SeatCushion", "BackrestTopRail", "Stretcher_Front" })
                {
                    Transform t = chair.transform.Find(name);
                    Assert.That(t, Is.Not.Null, name + " — 의자 디테일 존재.");
                    AssertRendererOnly(t.gameObject, name);
                }
                AssertFootprint(chair, 0.47f, 0.47f, -0.005f, 1.02f);
            }
            finally
            {
                Object.DestroyImmediate(chair);
            }
        }

        [Test]
        public void Fallback_MaterialDifferentiation_AtLeastThreeDistinctMaterials()
        {
            GameObject table = IndoorFurniturePlacer.CreateTable(1.6f, 0.9f, 0.95f, null);
            Assert.That(table, Is.Not.Null);
            try
            {
                var mats = new System.Collections.Generic.HashSet<Material>();
                foreach (MeshRenderer r in table.GetComponentsInChildren<MeshRenderer>())
                    mats.Add(r.sharedMaterial);
                Assert.That(mats.Count, Is.GreaterThanOrEqualTo(3),
                    "베이스+악센트(다크우드/철/직물 등) 머티리얼 차등화로 재질 판별성 확보.");
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }
    }
}
