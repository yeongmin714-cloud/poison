using NUnit.Framework;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [실내 고품질화 P1] InteriorQualityBaseline/InteriorPropCatalog/InteriorFurnishingPass 계약 6건.
    ///
    /// T1 골격 상수(기준 사진 판독값) / T2 시리즈 결정론(순수 함수) / T3 엔진 난수 미사용 소스계약 /
    /// T4 성 가구 패스 훅 소스계약 / T5 리소스 존재(Restaurant 플랫+Interior 복사본) /
    /// T5b Polyfork GLB 13종 파일 존재(157차 P4) / T6 절차 침대 결정론.
    /// ⚠ 문자열 계약 자기 오염 주의(149차) — 대상 파일의 검증 문자열을 본 파일 주석에 기재 금지.
    /// </summary>
    public class InteriorQualityBaselineTests
    {
        // ── T1. 골격 상수 — 품질바 판독값 고정 ──

        [Test]
        public void T1_BaselineConstants_MatchQualityBar()
        {
            Assert.That(InteriorQualityBaseline.TileSizeM, Is.EqualTo(1.0f), "타일 모듈 1m 계약.");
            Assert.That(InteriorQualityBaseline.WallHeightM, Is.EqualTo(2.5f), "벽 높이 기준.");
            Assert.That(InteriorQualityBaseline.MaxLightsPerRoom, Is.EqualTo(4), "방당 광 예산 상한.");
            Assert.That(InteriorQualityBaseline.MaxLightsLobby, Is.EqualTo(6), "로비 광 예산 상한.");
        }

        // ── T2. 시리즈 결정론 — 같은 인덱스 = 같은 변형(랩어라운드) ──

        [Test]
        public void T2_SeriesPick_DeterministicAndWrapping()
        {
            string a1 = InteriorPropCatalog.PickSuffix(InteriorPropCatalog.Table2Suffixes, 3);
            string a2 = InteriorPropCatalog.PickSuffix(InteriorPropCatalog.Table2Suffixes, 3);
            Assert.That(a1, Is.EqualTo(a2), "동일 인덱스 재현.");
            Assert.That(InteriorPropCatalog.PickSuffix(InteriorPropCatalog.Table2Suffixes, 18),
                Is.EqualTo(InteriorPropCatalog.PickSuffix(InteriorPropCatalog.Table2Suffixes, 0)),
                "랩어라운드 — count 초과 인덱스는 나머지 정규화.");
            Assert.That(InteriorPropCatalog.PickFurnit(0), Does.StartWith("Furnit_"),
                "Furnit 계열 이름 형식.");
            Assert.That(InteriorPropCatalog.PickFurnit(144), Is.EqualTo(InteriorPropCatalog.PickFurnit(0)),
                "Furnit 랩어라운드 — 8시리즈×18변형 주기.");
        }

        // ── T3. 엔진 난수 미사용 — 신규 3파일 소스계약(결정론 계약) ──

        [Test]
        public void T3_NoEngineRandomInNewInteriorFiles_SourceContract()
        {
            string[] files =
            {
                "Assets/Scripts/Systems/InteriorPropCatalog.cs",
                "Assets/Scripts/Systems/InteriorQualityBaseline.cs",
                "Assets/Scripts/Systems/InteriorFurnishingPass.cs",
            };
            foreach (string path in files)
            {
                string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
                    Application.dataPath, "..", path));
                Assert.That(src, Does.Not.Contain("UnityEngine.Random"),
                    $"{path} — 엔진 난수 미사용 계약.");
                Assert.That(src, Does.Not.Contain(".Range("),
                    $"{path} — 범위 난수 호출 미사용(정수 해시만).");
            }
        }

        // ── T4. 성 가구 패스 훅 — 빌더 말미 호출 소스계약 ──

        [Test]
        public void T4_CastleFurnishingPassHookedIntoPlayerCastleBuilder_SourceContract()
        {
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "..", "Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs"));
            Assert.That(src, Does.Contain("InteriorFurnishingPass.ApplyCastle(room, layoutDefinition, nationStyle)"),
                "성 빌드 말미 가구 패스 호출 계약.");
            // 호출 위치 계약 — 수조 적용 뒤(미니맵 캡처 뒤 가산 패스 순서 유지)
            int dioramaAt = src.IndexOf("IndoorDioramaKit.Apply(room)", System.StringComparison.Ordinal);
            int passAt = src.IndexOf("InteriorFurnishingPass.ApplyCastle", System.StringComparison.Ordinal);
            Assert.That(passAt, Is.GreaterThan(dioramaAt), "가구 패스는 수조 적용 뒤 — 렌더 계층 순서 계약.");
        }

        // ── T5. 리소스 존재 — Restaurant 플랫 + Interior 선별 복사본 ──

        [Test]
        public void T5_InteriorResourcesExist()
        {
            Assert.That(Resources.Load<GameObject>("Models/Restaurant/Table_2_A"), Is.Not.Null,
                "Restaurant 플랫 팩 — Table 시리즈 존재.");
            Assert.That(Resources.Load<GameObject>("Models/Restaurant/Furnit_A_1"), Is.Not.Null,
                "Restaurant 플랫 팩 — Furnit 계열 존재.");
            Assert.That(Resources.Load<GameObject>("Models/Interior/Decor/Lights_1_A"), Is.Not.Null,
                "Interior 복사본 — 천장 등.");
            Assert.That(Resources.Load<GameObject>("Models/Interior/Kitchen/CookingPot_1_A"), Is.Not.Null,
                "Interior 복사본 — 주방 소품.");
            Assert.That(Resources.Load<GameObject>("Models/Interior/Props/MP_Potion_1_1"), Is.Not.Null,
                "Interior 복사본 — 물약 소품(연금술소).");
        }

        // ── T5b. Polyfork 리소스 존재 — 13종 GLB 파일 존재(raw glb는 직렬화 에셋이 아니라 Resources.Load 불가 — 파일 방식) ──
        // [157차 P4] 신설 — Polyfork 13종 .glb 실물 파일 존재 검증(T5의 존재 계약 계열 확장).

        [Test]
        public void T5b_InteriorPolyforkGlbResourcesExist()
        {
            string[] polyforkIds =
            {
                "alchemy-workbench-117b73",
                "candle-spike-cluster-d7c566",
                "chained-book-lectern-381597",
                "iron-banded-strongbox-698383",
                "iron-chandelier-1337dc",
                "looted-gilded-throne-008fc3",
                "oak-barrel-b65311",
                "scroll-rack-f465dd",
                "standing-brazier-c9e4ee",
                "timber-crate-ba0739",
                "timber-trestle-table-024d88",
                "wall-torch-bracket-f5ad14",
                "weapon-rack-082d7f",
            };
            string dir = System.IO.Path.Combine(Application.dataPath, "Resources", "Models", "Polyfork");
            foreach (string id in polyforkIds)
            {
                Assert.That(System.IO.File.Exists(System.IO.Path.Combine(dir, id + ".glb")), Is.True,
                    $"Polyfork GLB 리소스 존재 — {id}.");
            }
        }

        // ── T6b. 성 변형별 가구 패스 실측 진단(플레이어/영주 레이아웃 모두 QualityFurnish 루트 보유 계약) ──

        [Test]
        public void T6b_CastleVariants_AllHaveFurnishingRoots()
        {
            GameObject owned = ProjectName.Systems.PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            GameObject lord = ProjectName.Systems.PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(
                "Empire", 0, null, "indoor_tour_lord");
            try
            {
                int CountRoots(GameObject room)
                {
                    int n = 0;
                    foreach (Transform t in room.transform)
                        if (t.name.StartsWith("QualityFurnish", System.StringComparison.Ordinal)) n++;
                    return n;
                }
                int ownedRoots = CountRoots(owned);
                int lordRoots = CountRoots(lord);
                Debug.Log($"[InteriorTest] ownedRoots={ownedRoots} lordRoots={lordRoots} " +
                          $"ownedChildren={owned.transform.childCount} lordChildren={lord.transform.childCount}");
                // 진단 — 로비 루트 자식 전수(이름/bounds/위치): 캡처 미표출 원인 규명용.
                foreach (Transform t in owned.transform)
                {
                    if (!t.name.StartsWith("QualityFurnish", System.StringComparison.Ordinal)) continue;
                    Debug.Log($"[InteriorTest] ROOT {t.name} children={t.childCount}");
                    int shown = 0;
                    foreach (Transform c in t)
                    {
                        if (shown >= 10) { Debug.Log($"[InteriorTest]   ... (+{t.childCount - shown} more)"); break; }
                        var rends = c.GetComponentsInChildren<Renderer>(true);
                        Bounds b = new Bounds();
                        bool has = false;
                        foreach (var rr in rends)
                        {
                            if (!has) { b = rr.bounds; has = true; }
                            else b.Encapsulate(rr.bounds);
                        }
                        Debug.Log($"[InteriorTest]   {c.name} active={c.gameObject.activeInHierarchy} " +
                                  $"pos={c.position:F1} size={(has ? b.size.ToString("F2") : "noRenderer")}");
                        shown++;
                    }
                }
                Assert.That(ownedRoots, Is.GreaterThanOrEqualTo(7),
                    "플레이어 성 — QualityFurnish 존 루트 7개(로비+6방).");
                Assert.That(lordRoots, Is.GreaterThanOrEqualTo(7),
                    "영주 성(타 영지키) — QualityFurnish 존 루트 7개(동일 패스 적용 계약).");
            }
            finally
            {
                Object.DestroyImmediate(owned);
                Object.DestroyImmediate(lord);
            }
        }

        // ── T6c. 성 좌표 실측 진단(스케일 혼용 규명 — Workbench 앵커 실배치 위치) ──

        [Test]
        public void T6c_CastleCoordinateScaleProbe()
        {
            GameObject room = ProjectName.Systems.PlayerCastleInteriorBuilder.BuildPlayerCastleInterior("Empire", 0);
            try
            {
                // ①작업대 앵커(작성 (−40,25))의 실제 배치 위치 — 스케일 판정 근거.
                Transform wb = room.transform.Find("Workbench");
                Vector3 wbPos = wb != null ? wb.position : Vector3.zero;
                // ②바닥/외곽 셸 대표 자식 bounds — 방 실면적 판정.
                var floors = room.GetComponentsInChildren<MeshFilter>(true);
                float maxSpan = 0f;
                string maxName = "";
                foreach (var mf in floors)
                {
                    var b = mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds();
                    float span = Mathf.Max(b.size.x, b.size.z) * mf.transform.lossyScale.x;
                    if (span > maxSpan) { maxSpan = span; maxName = mf.name; }
                }
                Debug.Log($"[InteriorProbe] workbenchPos={wbPos:F2} maxMeshSpan={maxSpan:F1}({maxName}) " +
                          $"childCount={room.transform.childCount}");
                // 진단 전용 — 앵커 이름은 변형별 상이(정보 수집 목적, 단정 없음).
            }
            finally
            {
                Object.DestroyImmediate(room);
            }
        }

        // ── T7. 텍스처 품질 v2 — 바닥/벽 512 소스계약 ──

        [Test]
        public void T7_TextureGeneratorDefaultIs512_SourceContract()
        {
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "..", "Assets/Scripts/Core/IndoorTextureGenerator.cs"));
            Assert.That(src, Does.Contain("DEFAULT_SIZE = 512"),
                "바닥/벽 텍스처 512화 계약(품질바).");
        }

        // ── T8. 원목 가구 소스계약 — 유색 GLB 테이블/의자 배제(사용자 지시) ──

        [Test]
        public void T8_WoodFurnitureUsed_ColoredTablesExcluded_SourceContract()
        {
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "..", "Assets/Scripts/Systems/InteriorFurnishingPass.cs"));
            Assert.That(src, Does.Contain("BuildWoodTable"),
                "원목 테이블 팩토리 사용.");
            Assert.That(src, Does.Contain("BuildWoodChair"),
                "원목 의자 팩토리 사용.");
            Assert.That(src, Does.Contain("BuildWoodStool"),
                "원목 스툴 팩토리 사용.");
            Assert.That(src, Does.Not.Contain("PlaceTable("),
                "유색 GLB 테이블 배치 배제(원목 전용 — 카운터 포함).");
            Assert.That(src, Does.Not.Contain("PlaceChair("),
                "유색 GLB 의자 배치 배제.");
            Assert.That(src, Does.Not.Contain("PlaceStool("),
                "유색 GLB 스툴 배치 배제.");
            // [157차 P4] 완성 계약 갱신(94차 — 기존 어서션 유지 + 배선 전환 가산):
            // 로비/주점/식당 탁자는 목재 팩토리 루프에서 Polyfork 배선으로 전환 완료.
            // 하우스 카운터 등 잔여 팩토리 호출은 유효 — 구 팩토리 계약 유지 + 신규 배선 계약 병기.
            Assert.That(src, Does.Contain("PlacePolyfork"),
                "Polyfork GLB 배치 API 배선 사용.");
            Assert.That(src, Does.Contain("timber-trestle-table-024d88"),
                "로비·주점·식당 탁자 — Polyfork 원목 탁자 배선(157차 교체 완성 계약).");
        }

        // ── T9. 벽 트림 v2 — 걸레받이/벽톱 캡 소스계약 ──

        [Test]
        public void T9_RoomTrimBandsAndCaps_SourceContract()
        {
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
                Application.dataPath, "..", "Assets/Scripts/Systems/InteriorFurnishingPass.cs"));
            Assert.That(src, Does.Contain("AddRoomTrim(root"),
                "건물 벽 트림 패스 호출.");
            Assert.That(src, Does.Contain("Band_E"), "걸레받이 동측 밴드.");
            Assert.That(src, Does.Contain("Cap_N"), "벽톱 북측 캡.");
            Assert.That(src, Does.Contain("CreateFloorWearMat"),
                "문 앞 마모 데칼(바닥 사용흔적).");
        }

        // ── T6. 절차 침대 결정론 — 동일 인자 = 동일 구성 ──

        [Test]
        public void T6_ProceduralBed_DeterministicStructure()
        {
            var wood = InteriorQualityBaseline.GetTrimMat("t6wood", new Color(0.4f, 0.3f, 0.2f));
            var mattress = InteriorQualityBaseline.GetTrimMat("t6mattress", Color.white);
            var bedA = InteriorQualityBaseline.BuildBed(null, "T6_BedA", Vector3.zero, 0f, wood, mattress, Color.red);
            var bedB = InteriorQualityBaseline.BuildBed(null, "T6_BedB", Vector3.zero, 0f, wood, mattress, Color.red);
            try
            {
                Assert.That(bedA.transform.childCount, Is.EqualTo(bedB.transform.childCount),
                    "동일 인자 침대 — 자식 구성 1:1.");
                Assert.That(bedA.transform.childCount, Is.GreaterThanOrEqualTo(6),
                    "침대 구성 요소 하한(프레임/머리판/매트리스/베개/이불/다리 4 = 9).");
            }
            finally
            {
                Object.DestroyImmediate(bedA);
                Object.DestroyImmediate(bedB);
            }
        }
    }
}
