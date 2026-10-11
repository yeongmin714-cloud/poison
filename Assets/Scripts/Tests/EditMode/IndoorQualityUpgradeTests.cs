using NUnit.Framework;
using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [2026-10-10 실내 고품질화 F1~F5] 계약 테스트.
    /// - F1: 회반죽/목재 노멀맵 선택 로드 경로, 하부 석재 밴드 옵션(기본 OFF), 짚 데칼 적용기.
    /// - F2: 절차 불꽃 쿠키(큐브맵), 화재 플리커(다중 사인+클램프), ember 발광판(렌더러 전용).
    /// - F3: 천장 들보(병합 메시·무콜라이더), 곡면 배너, 캔들라브라.
    /// - F4: 실내 전용 포스트 프로세싱 게이트 + 톤 상한 계약.
    /// - F5: 가구 GLB 카탈로그(기존 IndoorFurnitureCatalog GLB 우선+폴백) 계약 유지.
    /// - H1: 벽 톱면 돌 캡(모든 토폴로지 벽, 렌더러 전용, y≈6.01) / H2: 가구 접지 블롭 섀도우.
    /// - H3: 존별 조명 색온도 틴트 / H4: 포인트라이트 섀도우 OFF / H5: 바닥 마모 스탬프 12장.
    /// </summary>
    public sealed class IndoorQualityUpgradeTests
    {
        private const string LoaderPath = "Assets/Scripts/Core/IndoorTextureLoader.cs";
        private const string FactoryPath = "Assets/Scripts/Systems/IndoorMaterialFactory.cs";
        private const string BuilderPath = "Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs";
        private const string LightingPath = "Assets/Scripts/Systems/IndoorLighting.cs";
        private const string PostPath = "Assets/Scripts/Systems/IndoorPostProcessController.cs";

        // ======================================================================
        // F1 — 텍스처 세트
        // ======================================================================

        [Test]
        public void F1_Loader_HasOptionalNormalPaths_AndStoneLowerOptionDefaultOff()
        {
            string src = System.IO.File.ReadAllText(LoaderPath);
            Assert.That(src, Does.Contain("wall_plaster_upper_n"), "회반죽 노멀맵 선택 로드 경로.");
            Assert.That(src, Does.Contain("wood_frame_n"), "목재 노멀맵 선택 로드 경로.");
            Assert.That(src, Does.Contain("public static bool IncludeWallStoneLower = false"),
                "하부 석재 밴드 옵션은 기본 OFF여야 한다(계획 D1 전면 회반죽 유지).");
        }

        [Test]
        public void F1_Factory_ProvidesStoneLower_Ember_StrawMaterials()
        {
            string src = System.IO.File.ReadAllText(FactoryPath);
            Assert.That(src, Does.Contain("public static Material CreateWallStoneLower()"), "하부 석재 머티리얼.");
            Assert.That(src, Does.Contain("public static Material CreateEmberUnlit("), "HDR 발광 머티리얼.");
            Assert.That(src, Does.Contain("public static Material CreateStrawDecal()"), "짚 데칼 머티리얼.");
            Assert.That(src, Does.Contain("_SURFACE_TYPE_TRANSPARENT"), "짚 데칼은 투명(Unlit transparent)이어야 한다.");
        }

        [Test]
        public void F1_Builder_WiresQualityLayers_AfterScalePass()
        {
            string src = System.IO.File.ReadAllText(BuilderPath);
            int timber = src.IndexOf("AddTimberWallFrame(room, GetTopologyWalls()", System.StringComparison.Ordinal);
            int band = src.IndexOf("AddWallStoneLowerBands(room, GetTopologyWalls()", System.StringComparison.Ordinal);
            int straw = src.IndexOf("AddStrawFloorDecals(room)", System.StringComparison.Ordinal);
            Assert.That(timber, Is.GreaterThanOrEqualTo(0), "D3 팀버 호출부.");
            Assert.That(band, Is.GreaterThan(timber), "석재 밴드는 팀버(스케일 패스 후) 다음에 호출.");
            Assert.That(straw, Is.GreaterThan(timber), "짚 데칼은 스케일 패스 후(최종 좌표) 호출.");
            Assert.That(src.IndexOf("AddCeilingBeams(", System.StringComparison.Ordinal), Is.EqualTo(-1),
                "천장 들보는 탑다운 뷰 방해로 제거 확정 — 호출부 재추가 금지.");
        }

        [Test]
        public void F1_BuiltInterior_StrawDecals_Exist_RendererOnly()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                string[] names = { "StrawMat_Entry_1", "StrawMat_Entry_2", "StrawMat_Kitchen" };
                foreach (string name in names)
                {
                    Transform mat = room.transform.Find(name);
                    Assert.That(mat, Is.Not.Null, name + " 짚 데칼이 존재해야 한다.");
                    Assert.That(mat.GetComponent<Collider>(), Is.Null, name + "은 무콜라이더 데칼이어야 한다.");
                    var r = mat.GetComponent<MeshRenderer>();
                    Assert.That(r, Is.Not.Null);
                    Assert.That(r.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000),
                        name + "은 투명 큐(Transparent 3000+)로 렌더링돼야 한다.");
                }
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ======================================================================
        // F2 — 조명 고도화
        // ======================================================================

        [Test]
        public void F2_Lighting_PointCookie_AndFireFlicker()
        {
            string src = System.IO.File.ReadAllText(LightingPath);
            Assert.That(src, Does.Contain("GetOrCreateFlameCookie"), "절차 불꽃 쿠키 팩토리.");
            Assert.That(src, Does.Contain("light.cookie = cookie"), "AddPointLight가 쿠키를 부착해야 한다.");
            Assert.That(src, Does.Contain("phase1"), "플리커 러너별 위상차(다중 사인) 계약.");
            Assert.That(src, Does.Contain("Mathf.Clamp(multiplier, FLICKER_MIN_MULT, FLICKER_MAX_MULT)"),
                "플리커 배율은 기존 계약 밴드(0.8~1.2)로 클램프.");
        }

        [Test]
        public void F2_BuiltInterior_EmberGlowPads_RendererOnly_NoBrazierNaming()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                int ember = 0;
                foreach (Transform child in room.transform)
                {
                    if (!child.name.StartsWith("EmberGlow_")) continue;
                    ember++;
                    Assert.That(child.GetComponent<Collider>(), Is.Null,
                        child.name + " — ember 발광판은 렌더러 전용이어야 한다.");
                }
                Assert.That(ember, Is.EqualTo(2), "입구 좌우 ember 발광판 2장이 존재해야 한다.");
                // 물리 브레이저 금지 계약(기존 테스트 보강 — 명명 회피 확인)
                Assert.That(System.IO.File.ReadAllText(BuilderPath), Does.Not.Contain("BrazierCoals"),
                    "물리 브레이저 명명 회피 — EmberGlow로 대체.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ======================================================================
        // F3 — 지오메트리
        // ======================================================================

        [Test]
        public void F3_BuiltInterior_CeilingBeams_Banner_Candelabras_Exist()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                // 천장 들보 — [10-10 제거] 탑다운 뷰에서 입체 박스가 방을 가로막아 제거 확정.
                Transform beams = room.transform.Find("CastleCeilingBeams_TimberFrame");
                Assert.That(beams, Is.Null,
                    "천장 들보는 탑다운 뷰 방해(렌더링+그림자)로 제거돼야 한다 — 재추가 금지 계약.");

                // 곡면 배너 — BannerCloth가 메시(박스 프리미티브 아님)로 존재
                Transform banner = room.transform.Find("PlayerBanner/BannerCloth");
                Assert.That(banner, Is.Not.Null, "배너 천이 존재해야 한다.");
                Assert.That(banner.GetComponent<Collider>(), Is.Null, "곡면 배너는 렌더러 전용.");
                Assert.That(banner.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(30),
                    "곡면 배너는 세그먼트 메시여야 한다(평면 박스 아님).");

                // 캔들라브라 2기 + emissive 불꽃
                int candelabra = 0;
                foreach (Transform child in room.transform)
                {
                    if (!child.name.StartsWith("Candelabra_")) continue;
                    candelabra++;
                    Transform flame = child.Find("CandleFlame");
                    Assert.That(flame, Is.Not.Null, child.name + "에 emissive 불꽃이 있어야 한다.");
                }
                Assert.That(candelabra, Is.EqualTo(2), "로비 좌우 캔들라브라 2기.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void F3_BuiltInterior_StoneLowerBand_NotBuiltByDefault()
        {
            IndoorTextureLoader.IncludeWallStoneLower = false;
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                foreach (Transform child in room.transform)
                    Assert.That(child.name.EndsWith("_StoneLowerBand"), Is.False,
                        "하부 석재 밴드는 옵션 OFF일 때 생성되지 않아야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(room);
                IndoorTextureLoader.IncludeWallStoneLower = false;
            }
        }

        // ======================================================================
        // F4 — 포스트 프로세싱
        // ======================================================================

        [Test]
        public void F4_PostProcess_Gate_And_ToneCaps()
        {
            string src = System.IO.File.ReadAllText(PostPath);
            Assert.That(src, Does.Contain("activeScene == IndoorSceneName || activeScene.Contains(\"Interior\")"),
                "게이트 규약은 IndoorAeroFogController와 동일해야 한다.");
            Assert.That(src, Does.Contain("public const float BloomIntensity = 0.28f"), "Bloom 상한 계약.");
            Assert.That(src, Does.Contain("public const float VignetteIntensity = 0.35f"), "Vignette 상한 계약.");
            Assert.That(src, Does.Contain("_cameraRestore"), "카메라 renderPostProcessing 진입 전 값 복원 계약.");
            Assert.That(src, Does.Contain("profile.Add<Bloom>(true)"), "절차 프로필 — Bloom.");
            Assert.That(src, Does.Contain("profile.Add<Vignette>(true)"), "절차 프로필 — Vignette.");
        }

        // ======================================================================
        // F5 — 가구 GLB 카탈로그 (기존 구현 — 계약 유지 확인)
        // ======================================================================

        [Test]
        public void G1_FloorDecor_AllRemoved_OnlyWallTimberRemains()
        {
            // [10-10 사용자 판정 누적] 존 매트/벽 AO/걸레받이 전부 제거 확정 —
            //   탑다운에서 벽이 안 보여 이들만 "떠 있는 선"으로 읽힘. 바닥은 원본 텍스처만.
            //   벽 가독성 개선은 벽 톱면 캡(G2 계획)으로 별도 검토.
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                int skirting = 0;
                foreach (Transform child in room.transform)
                {
                    Assert.That(child.name.StartsWith("ZoneMat_"), Is.False,
                        child.name + " — 존 매트 제거 확정.");
                    Assert.That(child.name.EndsWith("_WallAO"), Is.False,
                        child.name + " — 벽 AO 제거 확정.");
                    if (child.name.EndsWith("_WallSkirting")) skirting++;
                }
                Assert.That(skirting, Is.EqualTo(0), "걸레받이 제거 확정 — 재추가 금지.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        [Test]
        public void F5_FurnitureCatalog_GlbFirst_WithProceduralFallback()
        {
            string src = System.IO.File.ReadAllText("Assets/Scripts/Systems/IndoorFurnitureCatalog.cs");
            Assert.That(src, Does.Contain("침대.glb"), "침대 GLB 풀(파일 투입 시 GLB 침대 자동 적용).");
            Assert.That(src, Does.Contain("IndoorFurniturePlacer.CreateBed"), "GLB 실패 시 절차 폴백 유지.");
            Assert.That(src, Does.Contain("go.AddComponent<Bed>()"), "GLB 침대도 Bed 세이브 상호작용 유지.");
        }
        // ======================================================================
        // H1 — 벽 톱면 돌 캡 (걸레받이의 정공법 — 벽이 짙은 돌 선으로 읽힘)
        // ======================================================================

        [Test]
        public void H1_Builder_WiresTopCaps_AfterStrawDecals()
        {
            string src = System.IO.File.ReadAllText(BuilderPath);
            int straw = src.IndexOf("AddStrawFloorDecals(room)", System.StringComparison.Ordinal);
            int caps = src.IndexOf("AddWallTopCaps(room, GetTopologyWalls()", System.StringComparison.Ordinal);
            int wear = src.IndexOf("AddFloorWearStamps(room)", System.StringComparison.Ordinal);
            Assert.That(caps, Is.GreaterThan(straw), "벽 캡은 스케일 패스 후(최종 좌표) 짚 데칼 다음 호출.");
            Assert.That(wear, Is.GreaterThan(caps), "마모 스탬프도 스케일 패스 후(최종 좌표) 호출.");
            Assert.That(src, Does.Contain("capY = RoomHeight + 0.01f"), "캡 y는 벽 톱면 위(z-fight 회피).");
        }

        [Test]
        public void H1_BuiltInterior_WallTopCaps_Exist_RendererOnly()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                int caps = 0;
                foreach (Transform child in room.transform)
                {
                    if (!child.name.EndsWith("_TopCap")) continue;
                    caps++;
                    Assert.That(child.GetComponent<Collider>(), Is.Null,
                        child.name + " — 벽 캡은 렌더러 전용(무콜라이더)이어야 한다.");
                    var r = child.GetComponent<MeshRenderer>();
                    Assert.That(r, Is.Not.Null, child.name + " — 메시 렌더러 필수.");
                    // 캡 y는 버텍스 로컬 좌표로 존재(CreateMergedMeshChild는 y=0 배치) — 메시 bounds로 판정.
                    var mf = child.GetComponent<MeshFilter>();
                    Assert.That(mf, Is.Not.Null);
                    Assert.That(mf.sharedMesh.bounds.center.y,
                        Is.EqualTo(PlayerCastleInteriorBuilder.RoomHeight + 0.01f).Within(0.02f),
                        child.name + " — 캡은 벽 톱면(6.0) 위에 놓여야 한다.");
                }
                Assert.That(caps, Is.GreaterThanOrEqualTo(14),
                    "축정렬 벽 14면 + 로비 사선 8면 중 최소 축정렬 전체에 캡이 존재해야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ======================================================================
        // H2 — 가구 접지 블롭 섀도우
        // ======================================================================

        [Test]
        public void H2_Catalog_AndFactory_ProvideBlobShadow()
        {
            string cat = System.IO.File.ReadAllText("Assets/Scripts/Systems/IndoorFurnitureCatalog.cs");
            string fac = System.IO.File.ReadAllText(FactoryPath);
            // 7개 Create*(Table/Chair/Shelf/Counter/Bed/Crate/Mirror) 전부 AddBlobShadow 호출
            int calls = 0;
            int idx = 0;
            string needle = "AddBlobShadow(go,";
            while ((idx = cat.IndexOf(needle, idx, System.StringComparison.Ordinal)) >= 0) { calls++; idx += needle.Length; }
            Assert.That(calls, Is.EqualTo(7), "카탈로그 7개 Create* 전부 블롭 섀도우 부착.");
            Assert.That(cat, Does.Contain("ShadowCastingMode.Off"),
                "블롭은 그림자를 방출하지 않는다(렌더러 전용).");
            Assert.That(fac, Does.Contain("GetBlobShadowTexture"), "블롭 텍스처 팩토리.");
            Assert.That(fac, Does.Contain("CreateBlobShadowMat"), "블롭 머티리얼 팩토리.");
        }

        [Test]
        public void H2_BuiltInterior_FurnitureBlobShadows_Exist()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                int blobs = 0;
                foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "BlobShadow") continue;
                    blobs++;
                    Assert.That(t.GetComponent<Collider>(), Is.Null, "블롭 섀도우는 콜라이더 없음(시각 전용).");
                }
                Assert.That(blobs, Is.GreaterThanOrEqualTo(20),
                    "침대 20개(+테이블/선반/상자) 블롭 섀도우가 존재해야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ======================================================================
        // H3 — 존별 조명 색온도 틴트
        // ======================================================================

        [Test]
        public void H3_Builder_AppliesZoneTint_ToWorkLights()
        {
            string src = System.IO.File.ReadAllText(BuilderPath);
            int uses = System.Text.RegularExpressions.Regex.Matches(src, "TintForZone\\(").Count;
            Assert.That(uses, Is.GreaterThanOrEqualTo(7),
                "TintForZone 선언 1 + 라이트 배선 6+(작업풀 4/사인 2/바운스 2).");
            Assert.That(src, Does.Contain("GetTopologyZoneAt(finalPos)"), "틴트는 존 판정 기반(최종 좌표).");
            // 강도 불변 — 틴트는 색만 변경(계약 문서)
            Assert.That(src, Does.Contain("강도 불변"), "틴트는 강도를 변경하지 않는다.");
        }

        // ======================================================================
        // H4 — 포인트라이트 섀도우 OFF (디더링 제거)
        // ======================================================================

        [Test]
        public void H4_Lighting_PointLightShadows_Disabled()
        {
            string src = System.IO.File.ReadAllText(LightingPath);
            Assert.That(src, Does.Contain("light.shadows = LightShadows.None"),
                "포인트라이트 실시간 섀도우 OFF — 저해상도 큐브 섀도우 디더링 제거.");
            Assert.That(src, Does.Not.Contain("light.shadows = LightShadows.Soft"),
                "Soft 섀도우 잔존 금지(재추가 시 디더링 재발).");
        }

        // ======================================================================
        // H5 — 바닥 마모 스탬프
        // ======================================================================

        [Test]
        public void H5_BuiltInterior_FloorWearStamps_Exist_RendererOnly()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                int wear = 0;
                foreach (Transform child in room.transform)
                {
                    if (!child.name.StartsWith("FloorWear_")) continue;
                    wear++;
                    Assert.That(child.GetComponent<Collider>(), Is.Null,
                        child.name + " — 마모 스탬프는 렌더러 전용이어야 한다.");
                    Assert.That(child.localPosition.y, Is.EqualTo(0.03f).Within(0.005f),
                        child.name + " — 바닥 데칼 위 얇은 높이 유지(스케일 패스 후 최종 좌표).");
                }
                Assert.That(wear, Is.EqualTo(12), "마모 스탬프 12장(소량 유지 계약).");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ======================================================================
        // [2026-10-11 사용자 요구 — 횃불 은은한 글로우] 화광원 바닥 부드러운 웜 광원 풀
        // ======================================================================

        [Test]
        public void TorchGlow_Factory_ProvidesFlameGlowMaterial()
        {
            string src = System.IO.File.ReadAllText(FactoryPath);
            Assert.That(src, Does.Contain("public static Material CreateFlameGlowMat()"),
                "횃불 글로우 머티리얼 팩토리(투명 Unlit + 가산 웜 블렌드).");
            Assert.That(src, Does.Contain("public static Texture2D GetFlameGlowTexture()"),
                "방사형 웜 글로우 텍스처 팩토리(최대 알파 0.5 — 은은한 풀).");
        }

        [Test]
        public void TorchGlow_BuiltInterior_PoolsExist_RendererOnly()
        {
            GameObject room = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            Assert.That(room, Is.Not.Null);
            try
            {
                // 입구 화광 2곳 + 천장 랜턴 중앙 — 루트 직속(authored 좌표 → 스케일 패스 동행).
                string[] rootNames = { "FlameGlow_EntranceL", "FlameGlow_EntranceR", "FlameGlow_Center" };
                foreach (string name in rootNames)
                {
                    Transform pool = room.transform.Find(name);
                    Assert.That(pool, Is.Not.Null, name + " 횃불 글로우 풀이 존재해야 한다.");
                    Assert.That(pool.GetComponent<Collider>(), Is.Null, name + "은 무콜라이더여야 한다.");
                    var r = pool.GetComponent<MeshRenderer>();
                    Assert.That(r, Is.Not.Null);
                    Assert.That(r.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),
                        name + "은 그림자를 방출하지 않아야 한다.");
                    Assert.That(r.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo(3000),
                        name + "은 투명 큐(Transparent 3000+)로 렌더링돼야 한다.");
                }
                // 캔들라브라 2기 풀 — 루트 자식(스케일 동행), 중심(x=z=0)에 부착.
                int candelabraPools = 0;
                foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
                    if (t.name == "FlameGlow_Pool") candelabraPools++;
                Assert.That(candelabraPools, Is.EqualTo(2),
                    "캔들라브라 2기에 글로우 풀이 각각 부착돼야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }
    }
}
