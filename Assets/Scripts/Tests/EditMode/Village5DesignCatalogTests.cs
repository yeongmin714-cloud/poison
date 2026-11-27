using System.Collections.Generic;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [145차 P1] 마을 설계 10종 카탈로그 계약 테스트 — docs/145_VILLAGE_5_DESIGNS_PLAN.md §2/§6.
    /// Village5DesignCatalog(배치표/목표높이표/전수 검산 API) 실데이터 산술 어서션 +
    /// Village5DesignRenderer 소스계약(문자열 — CaveInteriorSceneTests 선례).
    ///
    /// 축: ①46파일 전수 표준세트 커버리지(중복 Bell Tower 포함) ②랜드마크 마을당 종류 1개
    /// ③MinSpacing/광장 반경 ④HighlandProfile 불변식(분지 바닥/수면/고원 상면 순, 다리 양끝 육지,
    /// 고원 상면 배치) ⑤렌더러 소스계약(Play 가드/루트 중복 가드/결정론/단일소스 샘플러).
    /// 함정 준수: .Or.Contain 체인 금지(CS0019), UnityEngine.Random 0건.
    /// </summary>
    public sealed class Village5DesignCatalogTests
    {
        private const string RendererPath = "Assets/Scripts/Systems/Village5DesignRenderer.cs";
        private const string CatalogPath = "Assets/Scripts/Systems/Village5DesignCatalog.cs";

        /// <summary>랜드마크급 건물 id — 마을당 같은 종류 1개 제한 대상(계획 §2 원칙 ②).</summary>
        private static readonly string[] LandmarkIds =
        {
            "Church", "Gatehouse", "ClockTower", "WizardTower", "Watermill", "Windmill",
            "BellTower", "BellTowerDup", "BellTowerAlt",
        };

        private static Village5Design FindDesign(string id)
        {
            foreach (var d in Village5DesignCatalog.Designs)
                if (d.Id == id) return d;
            return null;
        }

        // ── 1. 카탈로그 기본 — 46파일 / 유일 파일명 ──

        [Test]
        public void Catalog_ContainsFortySixDefs_WithUniqueFileNames()
        {
            Assert.That(Village5DesignCatalog.BuildingCount, Is.EqualTo(46),
                "[145차 P1] 마을 GLB 46종 전수 등록(P0 감사 파일 수와 일치).");
            var seen = new HashSet<string>();
            foreach (var def in Village5DesignCatalog.Buildings)
            {
                Assert.That(def.FileName, Does.EndWith(".glb"), $"GLB 파일명 계약: {def.Id}");
                Assert.That(seen.Contains(def.FileName), Is.False,
                    $"파일명 중복 금지(중복 파일은 별도 id로 등록): {def.FileName}");
                seen.Add(def.FileName);
                Assert.That(def.TargetHeight, Is.GreaterThan(0f),
                    $"목표높이 양수 계약: {def.Id}");
            }
        }

        // ── 2. 전수 검산 — 46파일(중복 포함) 표준 세트 ≥1 배치 ──

        [Test]
        public void StandardSet_CoversAllFortySixFiles_ZeroMissing()
        {
            var missing = Village5DesignCatalog.FindMissingStandard();
            Assert.That(missing.Count, Is.EqualTo(0),
                $"[145차 P1] 46파일 전수 배치(누락 금지 계약) — 누락: {string.Join(",", missing)}");
            Assert.That(Village5DesignCatalog.IsEveryBuildingPlacedStandard(), Is.True,
                "전수 검산 API가 true — 한 건물도 빠지면 안 된다(사용자 명시 계약).");
        }

        [Test]
        public void StandardSet_EntryCount_MatchesMatrix()
        {
            // 확정 배분 매트릭스: V1 18 / V2 17(변형 종탑 포함) / V3 16 / V4 15(튜더 2인스턴스) / V5 16
            Assert.That(FindDesign("V1").Entries.Length, Is.EqualTo(18), "V1 강변자급 18엔트리.");
            Assert.That(FindDesign("V2").Entries.Length, Is.EqualTo(17), "V2 종교교역 17엔트리(성당 지구 종탑 2 포함).");
            Assert.That(FindDesign("V3").Entries.Length, Is.EqualTo(16), "V3 광산군사 16엔트리.");
            Assert.That(FindDesign("V4").Entries.Length, Is.EqualTo(15), "V4 성벽행정 15엔트리(TudorHouse 2인스턴스).");
            Assert.That(FindDesign("V5").Entries.Length, Is.EqualTo(16), "V5 숲속휴양 16엔트리.");
            Assert.That(Village5DesignCatalog.TotalStandardEntryCount(), Is.EqualTo(82),
                "표준 세트 총 배치 인스턴스 82(중복 인스턴스 포함).");
        }

        [Test]
        public void BellTower_ThreeFiles_AllPlacedInStandardSet()
        {
            // P0 실측: 원본(d74a5810) + "(1)" 중복(d74a5810) + 변형(a5ae8861, tri 5758≠5756 별개 에셋)
            Assert.That(CountInStandardSet("BellTower"), Is.GreaterThanOrEqualTo(1),
                "종탑 원본 파일(d74a5810.glb) 표준 세트 배치.");
            Assert.That(CountInStandardSet("BellTowerDup"), Is.GreaterThanOrEqualTo(1),
                "종탑 (1) 중복 파일 표준 세트 배치(V5 북 고지 — 중복 파일 소진).");
            Assert.That(CountInStandardSet("BellTowerAlt"), Is.GreaterThanOrEqualTo(1),
                "종탑 변형 파일(a5ae8861.glb — 별개 에셋) 표준 세트 배치(V2 성당 지구 제2종탑).");
        }

        [Test]
        public void TargetHeights_MatchPlannedScaleTable()
        {
            // 목표높이표(진실치) 스팟 검증 — 현실적 규모 하드코딩 계약
            Assert.That(Village5DesignCatalog.Get("ThatchedCottage").TargetHeight, Is.EqualTo(4.0f), "Thatched 4.0m.");
            Assert.That(Village5DesignCatalog.Get("MedievalCottage").TargetHeight, Is.EqualTo(4.5f), "Medieval Cottage 4.5m.");
            Assert.That(Village5DesignCatalog.Get("TudorHouse").TargetHeight, Is.EqualTo(5.5f), "Tudor 5.5m.");
            Assert.That(Village5DesignCatalog.Get("TreeHouse").TargetHeight, Is.EqualTo(7.0f), "Tree House 7.0m.");
            Assert.That(Village5DesignCatalog.Get("Tavern").TargetHeight, Is.EqualTo(6.5f), "Tavern 6.5m.");
            Assert.That(Village5DesignCatalog.Get("Watermill").TargetHeight, Is.EqualTo(8.0f), "Watermill 8.0m.");
            Assert.That(Village5DesignCatalog.Get("Windmill").TargetHeight, Is.EqualTo(11.0f), "Windmill 11.0m.");
            Assert.That(Village5DesignCatalog.Get("Church").TargetHeight, Is.EqualTo(12.0f), "Church 12.0m.");
            Assert.That(Village5DesignCatalog.Get("WizardTower").TargetHeight, Is.EqualTo(14.0f), "Wizard Tower 14.0m.");
            Assert.That(Village5DesignCatalog.Get("WoodenDock").TargetHeight, Is.EqualTo(0.44f), "Wooden Dock 0.44m(데크 모듈).");
            Assert.That(Village5DesignCatalog.Get("DockFishingRod").TargetHeight, Is.EqualTo(0.44f), "Dock Fishing Rod 0.44m.");
            Assert.That(Village5DesignCatalog.Get("WhimsicalVillage").TargetHeight, Is.EqualTo(3.0f), "Whimsical 디오라마 3.0m.");
        }

        // ── 3. 랜드마크 — 마을당 같은 종류 1개 제한 + 7종 전부 표준 세트 등장 ──

        [Test]
        public void Landmarks_AtMostOnePerKindPerVillage()
        {
            foreach (var village in Village5DesignCatalog.StandardDesigns())
            {
                foreach (string kind in LandmarkIds)
                {
                    int count = 0;
                    foreach (var e in village.Entries)
                        if (e.BuildingId == kind) count++;
                    Assert.That(count, Is.LessThanOrEqualTo(1),
                        $"[{village.Id}] 랜드마크 {kind} 마을당 1개 제한 위반 — count={count}");
                }
            }
        }

        [Test]
        public void Landmarks_EveryKindPlacedInStandardSet()
        {
            foreach (string kind in LandmarkIds)
                Assert.That(CountInStandardSet(kind), Is.GreaterThanOrEqualTo(1),
                    $"랜드마크 {kind} 표준 세트 최소 1배치.");
        }

        // ── 4. 간격/광장 — MinSpacing 6m + footprint 보수폭, 광장 반경 6m ──

        [Test]
        public void MinSpacing_Respected_InAllTenVillages()
        {
            foreach (var village in Village5DesignCatalog.Designs)
            {
                var entries = village.Entries;
                for (int i = 0; i < entries.Length; i++)
                {
                    for (int j = i + 1; j < entries.Length; j++)
                    {
                        float dx = entries[i].LocalX - entries[j].LocalX;
                        float dz = entries[i].LocalZ - entries[j].LocalZ;
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        float required = Village5DesignCatalog.RequiredSpacing(entries[i], entries[j]);
                        Assert.That(dist, Is.GreaterThanOrEqualTo(required),
                            $"[{village.Id}] MinSpacing 위반: {entries[i].BuildingId}~{entries[j].BuildingId} " +
                            $"dist={dist:F2} < required={required:F2}");
                    }
                }
            }
        }

        [Test]
        public void PlazaRadius_NoBuildingWithinSixMetersOfPlazaCenter()
        {
            foreach (var village in Village5DesignCatalog.Designs)
            {
                float px, pz;
                Village5DesignCatalog.PlazaCenter(village, out px, out pz);
                foreach (var e in village.Entries)
                {
                    if (e.BuildingId == "FountainPlaza") continue;   // 광장 그 자체는 예외
                    float dx = e.LocalX - px;
                    float dz = e.LocalZ - pz;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    Assert.That(dist, Is.GreaterThanOrEqualTo(Village5DesignCatalog.PlazaRadius),
                        $"[{village.Id}] 광장 반경 {Village5DesignCatalog.PlazaRadius}m 내 건물 금지 위반: {e.BuildingId} d={dist:F2}");
                }
            }
        }

        // ── 5. 고산 프로파일 불변식 ──

        [Test]
        public void HighlandProfiles_BasinBottom_Lt_WaterY_Lt_PlateauTop()
        {
            foreach (var village in Village5DesignCatalog.HighlandDesigns())
            {
                Assert.That(village.Profile, Is.Not.Null, $"[{village.Id}] 고산 설계는 프로파일 필수.");
                Assert.That(village.Profile.BasinBottomY, Is.LessThan(village.Profile.WaterY),
                    $"[{village.Id}] basinBottom({village.Profile.BasinBottomY}) < waterY({village.Profile.WaterY}).");
                Assert.That(village.Profile.WaterY, Is.LessThan(village.Profile.PlateauTopY),
                    $"[{village.Id}] waterY({village.Profile.WaterY}) < plateauTop({village.Profile.PlateauTopY}).");
            }
        }

        [Test]
        public void HighlandProfiles_Sampler_BasinFloorBelowWater_PlateauTopFlat()
        {
            foreach (var village in Village5DesignCatalog.HighlandDesigns())
            {
                var p = village.Profile;
                float basinH = p.SampleHeight(p.BasinCenterX, p.BasinCenterZ);
                Assert.That(basinH, Is.LessThan(p.WaterY),
                    $"[{village.Id}] 분지 중심 샘플 {basinH:F2} < waterY {p.WaterY} — 수면이 분지 바닥 위.");
                float topH = p.SampleHeight(p.PlateauCenterX, p.PlateauCenterZ);
                Assert.That(topH, Is.GreaterThanOrEqualTo(p.PlateauTopY - 0.5f),
                    $"[{village.Id}] 고원 중심 샘플 {topH:F2} ≈ plateauTop {p.PlateauTopY} — 메시/접지 단일소스.");
                Assert.That(topH, Is.GreaterThan(p.WaterY),
                    $"[{village.Id}] 고원 상면이 수면 위 — 고도차 성립.");
            }
        }

        [Test]
        public void HighlandEntries_AllOnPlateauFlatTop()
        {
            foreach (var village in Village5DesignCatalog.HighlandDesigns())
            {
                var p = village.Profile;
                foreach (var e in village.Entries)
                {
                    float dx = e.LocalX - p.PlateauCenterX;
                    float dz = e.LocalZ - p.PlateauCenterZ;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    Assert.That(dist, Is.LessThanOrEqualTo(p.PlateauRadius),
                        $"[{village.Id}] 고원 상면 배치 계약 위반: {e.BuildingId} dist={dist:F2} > plateauRadius={p.PlateauRadius}");
                    float h = p.SampleHeight(e.LocalX, e.LocalZ);
                    Assert.That(h, Is.GreaterThanOrEqualTo(p.WaterY + 0.2f),
                        $"[{village.Id}] 수면 위 육지 배치: {e.BuildingId} h={h:F2} (waterY={p.WaterY})");
                }
            }
        }

        [Test]
        public void HighlandBridges_EndsOnLand_And_PolylineCrossesBasin()
        {
            foreach (var village in Village5DesignCatalog.HighlandDesigns())
            {
                var p = village.Profile;
                float h0 = p.SampleHeight(p.BridgeX0, p.BridgeZ0);
                float h1 = p.SampleHeight(p.BridgeX1, p.BridgeZ1);
                Assert.That(h0, Is.GreaterThanOrEqualTo(p.WaterY + 0.15f),
                    $"[{village.Id}] 다리 시작점 육지(고원) 계약: h0={h0:F2} waterY={p.WaterY}");
                Assert.That(h1, Is.GreaterThanOrEqualTo(p.WaterY + 0.15f),
                    $"[{village.Id}] 다리 끝점 육지 계약: h1={h1:F2} waterY={p.WaterY}");

                float midX = (p.BridgeX0 + p.BridgeX1) * 0.5f;
                float midZ = (p.BridgeZ0 + p.BridgeZ1) * 0.5f;
                float dMid = Mathf.Sqrt(
                    (midX - p.BasinCenterX) * (midX - p.BasinCenterX) +
                    (midZ - p.BasinCenterZ) * (midZ - p.BasinCenterZ));
                Assert.That(dMid, Is.LessThanOrEqualTo(p.BasinRadius + Village5HighlandProfile.BasinSkirtWidth),
                    $"[{village.Id}] 다리가 수면(분지)을 건넌다: 중점 dist={dMid:F1} ≤ basin+skirt={p.BasinRadius + Village5HighlandProfile.BasinSkirtWidth}");
            }
        }

        [Test]
        public void HighlandDesigns_PlotEighty_StandardDesigns_PlotSixty()
        {
            foreach (var village in Village5DesignCatalog.HighlandDesigns())
                Assert.That(village.PlotSize, Is.EqualTo(80f), $"[{village.Id}] 고산 플롯 80×80m.");
            foreach (var village in Village5DesignCatalog.StandardDesigns())
                Assert.That(village.PlotSize, Is.EqualTo(60f), $"[{village.Id}] 표준 플롯 60×60m.");
        }

        // ── 6. 렌더러 소스계약(문자열 — CaveInteriorSceneTests 선례) ──

        [Test]
        public void Renderer_SourceContract_Guards_And_SingleSourceSampler()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("[145차 P2]"), "145차 P2 마커 주석(출처 추적 계약).");
            Assert.That(src, Does.Contain("\"Village5Designs_Root\""), "루트 이름 Village5Designs_Root — 중복 생성 가드 대상.");
            Assert.That(src, Does.Contain("GameObject.Find(RootName)"), "루트 중복 생성 가드(VillageBuilder 선례).");
            Assert.That(src, Does.Contain("Application.isPlaying"), "Play 가드 — 에디터 무동작(VillageBuilder 계약).");
            Assert.That(src, Does.Contain("profile.SampleHeight"), "고산 메시/접지 단일소스 샘플러 공유(침하 0 계약).");
            Assert.That(src, Does.Contain("VillageBuildingCatalog.InstantiateAtGround"),
                "GLB 로드+정규화+접지 재사용 — VillageBuildingCatalog 선례 준용.");
            Assert.That(src, Does.Contain("MeshCollider"), "고산 지형 보행 가능(메시 콜라이더).");
            Assert.That(src.Contains("UnityEngine.Random.Range"), Is.False,
                "UnityEngine.Random API 사용 0건 — 결정론 계약(주석 언급은 허용, 145차 P3 계약 진화).");
            Assert.That(src.Contains("new System.Random"), Is.False, "System.Random 인스턴스화 0건 — 결정론 계약.");
            Assert.That(src, Does.Contain("Village5DesignCatalog.StandardDesigns"), "표준/고산 분리 배치(두 줄 구성).");
        }

        [Test]
        public void Catalog_SourceContract_Markers_And_DecisionConstants()
        {
            string src = System.IO.File.ReadAllText(CatalogPath);
            Assert.That(src, Does.Contain("[145차 P1]"), "145차 P1 마커 주석(출처 추적 계약).");
            Assert.That(src, Does.Contain("MinSpacing = 6f"), "MinSpacing 6m — VillageBuilder 계약 상속.");
            Assert.That(src, Does.Contain("PlazaRadius = 6f"), "광장 반경 6m — 기존 PlazaRadius 계약 상속.");
            Assert.That(src, Does.Contain("FootprintFactor = 1.2f"), "footprint 보수폭 = TargetHeight×1.2.");
            Assert.That(src, Does.Contain("Assets/새로운 glb/마을 GLB/"), "마을 GLB 디렉토리 경로 계약.");
            Assert.That(src.Contains("UnityEngine.Random"), Is.False, "카탈로그 결정론 — Random 0건.");
        }

        // ── 7. HTML 엔티티 오염 검사(컨벤션 — AI 산출 파일 엔티티 누수 금지) ──

        [Test]
        public void NewSources_FreeOfHtmlEntities()
        {
            foreach (string path in new[] { CatalogPath, RendererPath })
            {
                string src = System.IO.File.ReadAllText(path);
                Assert.That(src.Contains("&gt;"), Is.False, $"HTML 엔티티 누수 금지: {path}");
                Assert.That(src.Contains("&lt;"), Is.False, $"HTML 엔티티 누수 금지: {path}");
                Assert.That(src.Contains("&amp;"), Is.False, $"HTML 엔티티 누수 금지: {path}");
                Assert.That(src.Contains("&quot;"), Is.False, $"HTML 엔티티 누수 금지: {path}");
            }
        }


        // ── 8. [145차 P2b] Test_15 고산 검증 씬 계약 ──

        [Test]
        public void Renderer_BuildPlot_SinglePlotApi_145P2b()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("public static GameObject BuildPlot(string designId, Vector3 center)"),
                "단일 플롯 API — Test_15 고산 검증 씬이 H1만 배치할 수 있어야 한다.");
            Assert.That(src, Does.Contain("BuildPlot(design.Id"), "BuildAll은 BuildPlot 경유(동작 동일성).");
        }

        [Test]
        public void Renderer_IdyllicGround_And_WaterFactory_145P2b()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("terrain/textures_idyllic/east_grass1_albedo"),
                "고산 지면 = 메인씬 동일 Idyllic 텍스처.");
            Assert.That(src, Does.Contain("WaterMaterialUpgrader.CreateNationTintedWaterMaterial"),
                "수면 = 메인씬 동일 Idyllic 워터 팩토리(LakeGenerator 동일 소스).");
            Assert.That(src, Does.Contain("terrain/textures_idyllic/west_dirt_albedo"),
                "흙길 = Idyllic L4 dirt_path 동일 소스 텍스처.");
            Assert.That(src, Does.Contain("BuildDirtPaths"), "고산 플롯 흙길(접근로+광장 링) 존재.");
        }

        [Test]
        public void Test15_BasicTerrain_Contract_147()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/TestTerritoryCombatSetup.cs");
            Assert.That(src, Does.Contain("private const string Test15DefaultVillageDesignId = \"V1\";"),
                "[147차] 기본 설계 id = V1(강변 자급자족 마을 — §5 확정, const 1줄 교체 가능).");
            Assert.That(src, Does.Contain("Village5DesignRenderer.BuildPlotFlat(Test15DefaultVillageDesignId, best.center, \"_Game\")"),
                "[147차] 설계 마을 플롯 교체 — 마을 GLB 46종 카탈로그 배치.");
            Assert.That(src, Does.Contain("P0Time(\"마을플롯\""),
                "[147차] P0 계측 — 마을플롯 단계 Stopwatch 래핑(§1-3).");
            Assert.That(src.Contains("VillageBuilder.BuildSingleVillage(best);"), Is.False,
                "구형 단일 마을(hut GLB+울타리 링 270) 호출 소멸 — 로딩 경량화(146차 계약 승계).");
            Assert.That(src.Contains("Village5DesignRenderer.BuildPlot(\"H1\""), Is.False,
                "Idyllic 플롯 호출 소멸 유지 — 로딩 경량화(146차 계약 승계).");
            Assert.That(src.Contains("BuildAll(showcaseOrigin"), Is.False,
                "쇼케이스 10마을 빌드 소멸 유지 — 로딩 경량화(146차 계약 승계).");

            string terrain = System.IO.File.ReadAllText("Assets/Scripts/Systems/TerrainGenerator.cs");
            Assert.That(terrain, Does.Contain("[147차] Test_15(Livelihood) 평지 게이트"),
                "[147차] TerrainGenerator 게이트 마커 — GetHeightAt 선두 상수 반환.");
            Assert.That(terrain, Does.Contain("public const float Test15FlatHeight = 0f;"),
                "평지 상수 단일 소스 — SurfaceY/ClampToGround/블롭 전파 정합(계획 §1-1).");
        }

        [Test]
        public void Renderer_BuildPlotFlat_FlatPlotApi_147()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("public static GameObject BuildPlotFlat(string designId, Vector3 center, string suffix)"),
                "[147차] 평지 플롯 API 신설 — 기존 경로(BuildPlot/BuildStandardPlot/BuildHighlandPlot) 무수정.");
            Assert.That(src, Does.Contain("[147차] 평지 플롯"), "147차 마커 주석(출처 추적 계약).");
            Assert.That(src, Does.Contain("GROUND_BASE + TerrainGenerator.Test15FlatHeight"),
                "평지 상수 샘플러 — 표면 y = 1 + Test15FlatHeight(접지 단일소스).");
            Assert.That(src, Does.Contain("BuildStandardDirtPaths(plot.transform, design, sampler)"),
                "흙길 유지(진입로+광장 링 — 캐시 메시 저가, §5 ②).");
            Assert.That(src, Does.Contain("PlaceEntries(plot.transform, design, sampler)"),
                "설계 배치표 전수 배치 + 기초 링/디퍼링/블롭 공통 경로 재사용.");
            Assert.That(src, Does.Contain("design.Type != Village5Type.Standard"),
                "고산 설계 평지 모드 대상 외 가드(프로필 지형 전제).");
        }

        // ── 9. [145차 P3-TOUR] BuildAll(origin) 오버로드 + PlotPitch 130m ──

        [Test]
        public void Renderer_BuildAllOriginOverload_PlotPitch130_145P3()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("public static void BuildAll(Vector3 origin)"),
                "[145차 P3-TOUR] BuildAll origin 오버로드 존재(투어 코스 원점 지정).");
            Assert.That(src, Does.Contain("public static void BuildAll()"),
                "기존 BuildAll() 시그니처 보존 계약.");
            Assert.That(src, Does.Contain("BuildAll(Vector3.zero)"),
                "기존 BuildAll() → origin 오버로드 위임(동작 동일성).");
            Assert.That(src, Does.Contain("PlotPitch = 130f"),
                "[145차 P3-TOUR] PlotPitch 130m — 플롯 60/80m + 투어 보행 거리 단축.");
            Assert.That(src, Does.Contain("origin + new Vector3(col * PlotPitch"),
                "플롯 배치 좌표 = origin + (col×pitch, 0, row×pitch).");
            Assert.That(src, Does.Contain("origin=("),
                "빌드 완료 로그에 origin 포함(투어 좌표 추적).");
        }

        // ── 10. [145차 P3-STD] 표준 플롯 Idyllic 지면+흙길 승격 ──

        [Test]
        public void Renderer_StandardPlot_IdyllicGround_DirtPaths_145P3()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("CreateIdyllicGrassMaterial"), 
                "[145차 P3-STD] 표준/고산 공용 Idyllic 잔디 머티리얼 팩토리(단일소스).");
            Assert.That(src, Does.Contain("CreateIdyllicGrassMaterial($\"Village5_{design.Id}_Terrain_Idyllic\")"),
                "[145차 P5-EDGE] 표준 지면도 공용 팩토리 경유 — 고산과 동일 명명(_Terrain_Idyllic) 통일.");
            Assert.That(src, Does.Contain("Models/UserProvided/terrain/textures_idyllic/east_grass1_albedo"),
                "표준 지면 = 메인씬 동일 Idyllic 텍스처 소스.");
            Assert.That(src, Does.Contain("BuildStandardDirtPaths"),
                "[145차 P3-STD] 표준 플롯 흙길(진입점→광장+광장 링) 존재.");
            Assert.That(src, Does.Contain("BuildDirtPathCore"),
                "흙길 공용 코어 — 표준/고산 샘플러 파라미터화.");
            Assert.That(src, Does.Contain("DirtPathEndpoints"),
                "진입점→광장 폴리라인 단일소스(흙길 생성과 산포 제약 공유).");
            Assert.That(src, Does.Contain("StandardGroundSampler(design)"),
                "[145차 P5-EDGE] 표준 접지 샘플러 = 미니 지형 메시와 동일 함수 단일소스(고산과 동일 구조).");
            Assert.That(src, Does.Contain("StandardGroundHeight"),
                "[145차 P5-EDGE] 표준 지면 높이 = FBM 굴곡+가장자리 falloff 단일 함수.");
            // [147차] 계약 승계 — 박스 상수 함수 금지는 유지하되, 평지 플롯(BuildPlotFlat)의 상수 샘플러는
            //   Test15FlatHeight 기반으로 한정해 허용(147차 §6-2 착지분 — 갱신 계약).
            Assert.That(src, Does.Contain("(x, z) => GROUND_BASE + TerrainGenerator.Test15FlatHeight"),
                "[147차] 평지 플롯 상수 샘플러 = Test15FlatHeight 기반만 허용(표면 y=1+0 — GROUND_BASE 계약).");
            Assert.That(src.Contains("(x, z) => GROUND_BASE)"), Is.False,
                "[147차] Test15FlatHeight 미기반 상수 샘플러 금지 — 145차 P5-EDGE 계약 승계.");
        }

        // ── 11. [145차 P3-NATURE] 결정론 자연 산포 ──

        [Test]
        public void Renderer_NatureScatter_Markers_Constraints_Determinism_145P3()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("[145차 P3-NATURE]"), "산포 마커 주석(출처 추적 계약).");
            Assert.That(src, Does.Contain("NatureTreeCount = 14"), "나무 14그루/마을.");
            Assert.That(src, Does.Contain("NatureBushCount = 8"), "덤불 8그루/마을.");
            Assert.That(src, Does.Contain("NatureBuildingClear = 2f"),
                "제약 ① 건물 footprint+2m.");
            Assert.That(src, Does.Contain("NaturePathClear = 3f"),
                "제약 ②④ 흙길 폴리라인/광장 링/다리 3m.");
            Assert.That(src, Does.Contain("NatureMinSpacing = 4f"), "제약 ⑥ 자연물 상호 4m.");
            Assert.That(src, Does.Contain("NatureTreeDir + NatureTreePrefabs[treesPlaced % NatureTreePrefabs.Length]"),
                "나무 프리팹 = Resources Idyllic 실측 풀 인덱스 사이클.");
            Assert.That(src, Does.Contain("IdyllicPrefabs/Trees/"), "나무 프리팹 디렉토리 = Resources Idyllic 실측.");
            Assert.That(src, Does.Contain("IdyllicPrefabs/Bushes/"), "덤불 프리팹 디렉토리 = Resources Idyllic 실측.");
            Assert.That(src, Does.Contain("Size = 5f + Frac01(h) * 5f"),
                "[145차 P5-CLUSTER] 나무 목표높이 5~10m 클러스터 분산(기존 6~9m 인덱스 기반 갱신).");
            Assert.That(src, Does.Contain("0.8f + (bushesPlaced % 5) * 0.1f"), "덤불 0.8~1.2m 인덱스 기반.");
            Assert.That(src, Does.Contain("if (go == null) continue;   // 로드 실패 — 스킵(프리미티브 폴백 없음)"),
                "프리팹 로드 실패 시 스킵(자연물은 폴백 대상 아님 — 생략이 위조보다 낫다).");
        }

        [Test]
        public void Renderer_NatureScatter_DeterministicSeed_NoRandom_145P3()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("GetHash(design.Id + \"_nature145\")"),
                "[145차 P3-NATURE] 산포 후보 시드 = 설계 id 결정론 해시(VillageBuilder GetHash 패턴).");
            Assert.That(src, Does.Contain("NatureGoldenAngle"),
                "후보 = 황금각 나선표본(결정론 순회).");
            Assert.That(src, Does.Contain("private static int NextHash(int h)"),
                "xorshift32 정수 흐름 — 후보별 결정론 지터.");
            Assert.That(src.Contains("UnityEngine.Random.Range"), Is.False,
                "산포 결정론 — Random API 사용 0건(주석 언급 허용, 145차 P3 계약 진화).");
            Assert.That(src, Does.Contain("NatureCandidateSlots"),
                "후보 슬롯 순회 + 미충족 스킵(배치 수 보장 아님 — 실배치 수 로그 계약).");
        }

        // ── 12. [145차 P4-PAD] 고산 평탄화 패드 — SampleHeightWithPads 수치 어서션 ──

        [Test]
        public void HighlandPads_SampleHeightWithPads_CoreFlat_BlendBand_FreeOutside_145P4()
        {
            // 합성 프로파일(H4 실측값) + 인공 패드(padY 5.0 — 자연 높이와 다르게 강제해 수치 어서션)
            var profile = new Village5HighlandProfile(-8f, -8f, 16f, 9f, 18f, 10f, 9f, 0f, 0.7f, 1.9f, -1.2f, 32.8f, 20.2f);
            var pads = new List<Village5TerrainPad> { new Village5TerrainPad(0f, 0f, 6f, 5.0f) };

            // 완전 코어(d ≤ radius×0.75 = 4.5m) — padY와 정확히 일치
            Assert.That(profile.SampleHeightWithPads(0f, 0f, pads), Is.EqualTo(5.0f).Within(1e-4f),
                "[145차 P4-PAD] 패드 코어 중심 = padY(완전 평탄).");
            Assert.That(profile.SampleHeightWithPads(3.0f, 0f, pads), Is.EqualTo(5.0f).Within(1e-4f),
                "[145차 P4-PAD] 패드 코어 내부(d=3.0 ≤ 4.5) = padY.");

            // 블렌드 밖(d ≥ radius+4 = 10m) — 자연 SampleHeight와 정확히 일치
            float naturalFar = profile.SampleHeight(0f, 10.5f);
            Assert.That(profile.SampleHeightWithPads(0f, 10.5f, pads), Is.EqualTo(naturalFar).Within(1e-4f),
                "[145차 P4-PAD] 블렌드 밖 = 자연 SampleHeight와 일치(기존 지형 불변).");

            // 블렌드 밴드(4.5 < d < 10) — padY와 자연 사이에 머문다
            float blend = profile.SampleHeightWithPads(0f, 7.0f, pads);
            float naturalAt7 = profile.SampleHeight(0f, 7.0f);
            Assert.That(blend, Is.LessThanOrEqualTo(Mathf.Max(5.0f, naturalAt7) + 1e-4f),
                "[145차 P4-PAD] 블렌드 밴드 상한 — padY/자연 범위 밖으로 튀지 않는다.");
            Assert.That(blend, Is.GreaterThanOrEqualTo(Mathf.Min(5.0f, naturalAt7) - 1e-4f),
                "[145차 P4-PAD] 블렌드 밴드 하한 — padY/자연 범위 밖으로 튀지 않는다.");

            // 패드 없음/null — 자연 위임(기존 SampleHeight 무수정 계약)
            Assert.That(profile.SampleHeightWithPads(0f, 0f, null), Is.EqualTo(profile.SampleHeight(0f, 0f)).Within(1e-4f),
                "[145차 P4-PAD] 패드 목록 null = 자연 위임.");
        }

        // ── 13. [145차 P4] 접지감 패키지 소스계약 — Foundation/DirtPad/Shore/Blob + 빌드 순서 ──

        [Test]
        public void Renderer_GroundingPackage_SourceContracts_145P4()
        {
            string src = System.IO.File.ReadAllText(RendererPath);

            // A — 평탄화 패드(메시/접지 단일소스 유지)
            Assert.That(src, Does.Contain("[145차 P4-PAD]"), "평탄화 패드 마커 주석.");
            Assert.That(src, Does.Contain("profile.SampleHeightWithPads(x, z, pads)"),
                "고산 접지 = 패드 포함 위임 래퍼 샘플러(단일소스).");
            Assert.That(src, Does.Contain("BuildHighlandPads"), "패드 목록 생성(건물 전부+나무 각 1건+다리 양끝 2건).");
            Assert.That(src, Does.Contain("Mathf.Max(footprint * 0.5f + 1.5f, 2.5f)"),
                "건물 패드 반경 = max(footprint/2+1.5, 2.5).");
            Assert.That(src, Does.Contain("ComputeNaturePositions(design, (x, z) => profile.SampleHeight(x, z)"),
                "자연 산포 후보 위치 선결정(자연 샘플러 — 결과 불변 빌드 순서 계약).");
            Assert.That(src, Does.Contain("BuildHighlandMesh(plot.transform, design, profile, sampler)"),
                "미니 지형 메시가 패드 샘플러 사용(메시/접지 단일소스 — 빌드 순서 계약).");
            Assert.That(src, Does.Contain("패드 A 해당 없음"), "표준 플롯은 이미 평탄 — A 미적용 기록.");

            // B — 코블스톤 기초 링
            Assert.That(src, Does.Contain("[145차 P4-FOUNDATION]"), "기초 링 마커.");
            Assert.That(src, Does.Contain("empire_cobble_albedo"), "기초 = 코블스톤 텍스처(Resources 실존 소스).");
            Assert.That(src, Does.Contain("_Foundation"), "기초 링 이름 계약 Village5_<id>_<idx>_Foundation.");
            Assert.That(src, Does.Contain("footprint * 0.62f"), "기초 반경 = footprint×0.62.");
            Assert.That(src, Does.Contain("groundY + 0.25f - 0.15f"), "기초 상면 = 지면+0.25(건물 0.25m 파묻힘).");

            // C — 흙 디퍼링 디스크
            Assert.That(src, Does.Contain("[145차 P4-DIRTDECAL]"), "디퍼링 디스크 마커.");
            Assert.That(src, Does.Contain("west_dirt_albedo"), "디퍼링 = 기존 흙길과 동일 west_dirt 소스.");
            Assert.That(src, Does.Contain("_DirtPad"), "디퍼링 디스크 이름 계약.");
            Assert.That(src, Does.Contain("footprint * 0.85f"), "디퍼링 반경 = footprint×0.85(기초보다 넓고 아래).");

            // D — 수변 갈대 산포
            Assert.That(src, Does.Contain("[145차 P4-SHORE]"), "수변 산포 마커.");
            Assert.That(src, Does.Contain("IdyllicPrefabs/Shore/"), "수변 프리팹 = Resources IdyllicPrefabs/Shore.");
            Assert.That(src, Does.Contain("Cattail_01"), "수변 프리팹 실존 소스 1.");
            Assert.That(src, Does.Contain("Reeds_01"), "수변 프리팹 실존 소스 2.");
            Assert.That(src, Does.Contain("WaterY + 0.3f"), "수면 위 육지 스킵 — 잠김 지점만 배치(결정론).");
            Assert.That(src, Does.Contain("1.0f + Frac01(h) * 0.4f"), "수변 스케일 1.0~1.4(xorshift — Random 0건).");

            // E — 로컬 블롭 섀도우
            Assert.That(src, Does.Contain("[145차 P4-BLOB]"), "블롭 섀도우 마커.");
            Assert.That(src, Does.Contain("IndoorMaterialFactory.CreateBlobShadowMat()"),
                "블롭 머티리얼 = IndoorMaterialFactory 공유 팩토리 재사용(매 빌드 1회 캐시).");
            Assert.That(src, Does.Contain("TerrainGenerator.GetHeightAt"),
                "BlobShadow.cs 전역 지형 의존 회피 사유 주석 기록(미니 지형 프로파일 불일치).");
            Assert.That(src, Does.Contain("_Blob"), "블롭 이름 계약.");
            Assert.That(src, Does.Contain("Quaternion.Euler(-90f, 0f, 0f)"), "수평 쿼드 회전 -90 X.");
            Assert.That(src, Does.Contain("footprint * 1.6f"), "건물 블롭 = footprint×1.6.");
        }

        [Test]
        public void Catalog_PadSampler_SourceContract_145P4()
        {
            string src = System.IO.File.ReadAllText(CatalogPath);
            Assert.That(src, Does.Contain("[145차 P4-PAD]"), "카탈로그 측 패드 마커 주석.");
            Assert.That(src, Does.Contain("public struct Village5TerrainPad"), "패드 구조체(cx, cz, radius, padY).");
            Assert.That(src, Does.Contain("public float SampleHeightWithPads"), "패드 포함 위임 래퍼 존재.");
            Assert.That(src, Does.Contain("float h = SampleHeight(localX, localZ);"),
                "위임 래퍼 — 기존 SampleHeight 재호출(기존 단일소스 무수정 계약).");
            Assert.That(src, Does.Contain("Mathf.SmoothStep"), "블렌드 존 = smoothstep(r×0.75~r+4m).");
        }

        // ── 14. [145차 P5] 완전 자연화 소스계약 — EDGE/GRASS/TEXMIX/CLUSTER/CURVE/WATERFX ──

        [Test]
        public void Renderer_StandardPlot_TerrainMesh_SingleSourceSampler_145P5()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("[145차 P5-EDGE]"), "표준 플롯 미니 지형 메시 마커 주석.");
            Assert.That(src, Does.Contain("BuildTerrainMeshCore"),
                "[145차 P5-EDGE] 표준/고산 공용 지형 메시 코어(박스 패치 대체).");
            Assert.That(src.Contains("_GroundPatch"), Is.False,
                "[145차 P5-EDGE] 박스 지면 패치 문자열 제거 확인(갱신 계약 — 미니 메시로 교체).");
            Assert.That(src, Does.Contain("StandardGroundSampler(design)"),
                "표준 접지 샘플러 = 메시 정점과 동일 단일 함수(PlaceEntries/나무/흙길/기초/디퍼링/섀도우 공유).");
            Assert.That(src, Does.Contain("StandardBumpAmp = 0.3f"), "중심부 미세 FBM 굴곡 진폭 ≤0.3m.");
            Assert.That(src, Does.Contain("StandardBumpFreq = 0.08f"), "굴곡 기본 주파수 ~0.08.");
            Assert.That(src, Does.Contain("StandardEdgeBaseY = GROUND_BASE - 0.1f"),
                "가장자리 기저 레벨 = GROUND_BASE−0.1(SetupGround/SurfaceY 실측 기본값 — 승인치).");
            Assert.That(src, Does.Contain("NoiseHash"), "결정론 밸류 노이즈 해시(Random 0건).");
        }

        [Test]
        public void Renderer_NatureClustering_Contract_145P5()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("[145차 P5-CLUSTER]"), "나무 클러스터링 마커 주석.");
            Assert.That(src, Does.Contain("NatureClusterMin = 3"), "클러스터 수 하한 3.");
            Assert.That(src, Does.Contain("NatureClusterMax = 5"), "클러스터 수 상한 5.");
            Assert.That(src, Does.Contain("NatureClusterRadius + (u1 + u2) * 0.5f * NatureClusterSpread"),
                "가우시안 유사 산포 — 균등 2합 평균(중심 쏠림), 반경 3~6m.");
            Assert.That(src, Does.Contain("NatureClusterMinSpacing = 2.2f"),
                "클러스터 내부 상호 간격 완화(외곽 링 4m 유지 — NatureMinSpacing 계약 보존).");
            Assert.That(src, Does.Contain("NaturePairSpacing = 1.5f"),
                "덤불 페어링 간격 완화 — 나무 1.2m 이내 배치(기존 덤불 수 유지).");
            Assert.That(src, Does.Contain("단독 나무 최소 2그루 예약 — 외곽 링 유지"),
                "잔여 단독 나무 외곽 링 유지 계약.");
        }

        [Test]
        public void Renderer_GrassFlowerScatter_Contract_145P5()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("[145차 P5-GRASS]"), "잔디/꽃 산포 마커 주석.");
            Assert.That(src, Does.Contain("IdyllicGrassCover 재사용 판정"),
                "IdyllicGrassCover 재사용 판정 주석 기록 계약(근거 명시).");
            Assert.That(src, Does.Contain("정적 프리팹 산포로 대체"),
                "미채용 판정 → 정적 프리팹 산포 대체(플레이어 추종 컬링 구조 불일치).");
            Assert.That(src, Does.Contain("IdyllicPrefabs/Grass/"), "잔디 프리팹 = Resources 실측 디렉토리.");
            Assert.That(src, Does.Contain("IdyllicPrefabs/Flowers/"), "개별 꽃 프리팹 = Resources 실측 디렉토리.");
            Assert.That(src, Does.Contain("IdyllicPrefabs/Meadows/"), "꽃밭 덩어리 = Resources 실측 디렉토리.");
            Assert.That(src, Does.Contain("FlowerMeadowPrefabs"), "FlowerMeadow 큰 덩어리 프리팹 풀.");
            Assert.That(src, Does.Contain("GrassTuftCount = 105"), "잔디 튜프 105(90~120 범위 중간).");
            Assert.That(src, Does.Contain("FlowerCount = 16"), "개별 꽃 16(12~20 범위 중간).");
            Assert.That(src, Does.Contain("GrassBuildingClear = 1.5f"), "건물 footprint+1.5m 제약.");
            Assert.That(src, Does.Contain("GrassMinSpacing = 0.8f"), "상호 0.8m 제약.");
            Assert.That(src, Does.Contain("0.8f + Frac01(h) * 0.5f"), "스케일 0.8~1.3(xorshift32 — Random 0건).");
            Assert.That(src, Does.Contain("StripColliders"), "정적 데코 콜라이더 제거(GO 예산·물리 비용).");
        }

        [Test]
        public void Renderer_TexturePatches_Contract_145P5()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("[145차 P5-TEXMIX]"), "지면 텍스처 혼합 마커 주석.");
            Assert.That(src, Does.Contain("east_moss_albedo"), "이끼 패치 = east_moss_albedo(Resources 실존 소스).");
            Assert.That(src, Does.Contain("empire_rock_albedo"), "바위 노출 = empire_rock_albedo(Resources 실존 소스).");
            Assert.That(src, Does.Contain("CreateSurfacePatchDisc"), "지면 추종 패치 디스크(팬 메시 — 접지 단일소스).");
            Assert.That(src, Does.Contain("0.012f"), "패치 디스크 lift = 지면+0.012(디퍼링과 동일 방식).");
            Assert.That(src, Does.Contain("CreatePatchMaterial"), "패치 머티리얼 = 디퍼링과 동일 팩토리 방식(정적 캐시).");
        }

        [Test]
        public void Renderer_CurvedDirtPaths_Contract_145P5()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("[145차 P5-CURVE]"), "곡선 보행로 마커 주석.");
            Assert.That(src, Does.Contain("TerrainPathGenerator.CurvedRoads 수학 결정론 이식"),
                "CurvedRoads 선례 수학(양끝 0 봉투 + 사행) 이식 기록.");
            Assert.That(src, Does.Contain("2.5f + Frac01(h) * 1.0f"), "수직 오프셋 진폭 2.5~3.5m.");
            Assert.That(src, Does.Contain("1f + Frac01(h) * 0.5f"), "사행 주기 1~1.5.");
            Assert.That(src, Does.Contain("1.0f + Frac01(hW) * 0.4f"), "세그먼트별 폭 halfW 1.0~1.4(xorshift).");
            Assert.That(src, Does.Contain("CachedPaths"), "폴리라인 캐시 단일소스(흙길 스트립/산포 제약 공유).");
            Assert.That(src, Does.Contain("가지길"), "광장→Landmark 가지길(footprint+1.5m 지점).");
            Assert.That(src, Does.Contain("DistanceToPolylineXZ"), "곡선 폴리라인 거리 검산(직선 좌표 참조 갱신).");
        }

        [Test]
        public void Renderer_WaterFinish_Contract_145P5()
        {
            string src = System.IO.File.ReadAllText(RendererPath);
            Assert.That(src, Does.Contain("[145차 P5-WATERFX]"), "물·불 마감 마커 주석.");
            Assert.That(src, Does.Contain("IdyllicPrefabs/Water/"), "수련 프리팹 = Resources 실측 디렉토리.");
            Assert.That(src, Does.Contain("Waterlily_01"), "수련 소스 1(Resources 실측).");
            Assert.That(src, Does.Contain("LilyPads_01"), "수련잎 소스(Resources 실측).");
            Assert.That(src, Does.Contain("profile.WaterY + 0.02f"), "수련 y = 수면 위 +0.02.");
            Assert.That(src, Does.Contain("IdyllicPrefabs/Rocks/"), "수변 바위 = Resources 실측 디렉토리.");
            Assert.That(src, Does.Contain("Rock_Big_01"), "수변 바위 소스(Resources 실측).");
            Assert.That(src, Does.Contain("0.8f + Frac01(h) * 0.8f"), "수변 바위 스케일 0.8~1.6.");
            Assert.That(src, Does.Contain("CampfireFirePrefab = \"FX/Fire/VFX_Fire_01_Big\""),
                "캠프파이어 화염 = Resources/FX/Fire 실존 프리팹.");
            Assert.That(src, Does.Contain("AttachCampfireFire"), "화염 부착 훅(PlaceEntries 경유).");
            Assert.That(src, Does.Contain("0.35f + Frac01(h) * 0.15f"), "화염 스케일 0.35~0.5.");
            Assert.That(src, Does.Contain("ParticleSystem"), "파티클 자동 재생 상태 확인 로그 계약.");
        }

        // ── 헬퍼 ──

        private static int CountInStandardSet(string buildingId)
        {
            int count = 0;
            foreach (var village in Village5DesignCatalog.StandardDesigns())
                foreach (var e in village.Entries)
                    if (e.BuildingId == buildingId) count++;
            return count;
        }
    }
}
