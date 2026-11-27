using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [실내 고품질화 P1/P2/P3] 실내 가구·조명·소품 배치 패스 — 기존 빌더 뒤에 부착되는 가산 패스.
    ///
    /// 설계 원칙:
    ///   ①가산 전용 — 기존 빌더의 지오메트리/토폴로지/앵커 무수정(소실 금지 계약).
    ///   ②장식 가구는 렌더러 전용(콜라이더 제거 — CreateRendererOnlyStoneBox 선례) —
    ///     기능 앵커(작업대/저장고/무기고) 블로킹 금지.
    ///   ③결정론 — nationStyle/빌딩타입 문자열 해시만으로 변형 고정(엔진 난수 미사용).
///   ③b조명 계약 — 성 조명 계약테스트(46/48차)의 강도 ≤2.9·반경 ≤7 "understated accent" 준수:
///     넓은 커버는 대형 광원 대신 소형광 다수로(방당 ≤2 — InteriorQualityBaseline 예산 내).
    ///   ④리소스 — InteriorPropCatalog(Resources/Models/Restaurant 325 GLB + Interior 소품).
    ///
    /// 방별 구성(영주 성 — 9/29 설계도 정합 [무번호 15]): 로비(중앙 벽난로+벤치 4+횃불 스탠드 4+카펫) /
    ///   침실(침대 1[빌더 기능침대]+협탁 2+옷장+집무 책상+카펫) / 제작소(용광로+모루+가죽 작업대+갑옷 거치대) /
    ///   창고(선반열+물류 데스크 2+상자·포대·항아리 — 타일 공통 석재) / 병영(침대 8[빌더 기능침대]+작전 탁자+무기 거치대) /
    ///   연금술소(시약 선반+실험대+절구) / 입구홀(러그 스트립).
    ///   [무번호 15] 설계도 외 누적 가산분(왕좌/악기/보물/책장/훈련 기구 등) 전량 제거 — 난잡 해소.
    /// </summary>
    public static class InteriorFurnishingPass
    {
        private const float LampY = 4.2f;      // 천장 등 걸이 높이(성 홀 기준)
        private const float WallMargin = 2.0f; // 벽 이격

        private static GameObject Root(GameObject room, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(room.transform, false);
            return go;
        }

        // ── [무번호 88] 설계도 정합 헬퍼 — 크기 정규화는 수평 최대축 기준(방 폭 비율 목표치) ──
        /// <summary>수평 최대축(x/z 중 긴 축)을 목표 길이에 맞춘 균일 스케일 — 벤치/긴 탁자/선반/옷장 등 배치 가구.</summary>
        private static void NormalizeMaxAxis(GameObject go, float targetMeters)
        {
            if (go == null || targetMeters <= 0f) return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float longest = Mathf.Max(b.size.x, b.size.z);
            if (longest <= 0.0001f) return;
            go.transform.localScale = go.transform.localScale * (targetMeters / longest);
        }

        /// <summary>오브젝트 최상단 월드 y — 긴 탁자/실험대 위 소품 얹기(방 루트 y 보정).</summary>
        private static float TopY(GameObject go, Transform room)
        {
            if (go == null) return 0f;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 0f;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b.max.y - room.position.y;
        }

        /// <summary>대각벽 벤치 — 두 로비 정점의 변 중점을 내측 1.3m 인셋, 벽 방향 요우. [무번호 88] 설계도 "대각선 벽면 따라 4개".</summary>
        private static void PlaceWallBench(Transform parent, string name, Vector2 a, Vector2 b, float lengthM)
        {
            Vector2 mid = (a + b) * 0.5f;
            Vector2 dir = (b - a).normalized;
            Vector2 inward = new Vector2(-dir.y, dir.x);
            if (inward.x * mid.x + inward.y * mid.y > 0f) inward = -inward;   // 로비 중심(원점) 쪽 면 선택
            Vector3 pos = new Vector3(mid.x + inward.x * 1.3f, 0f, mid.y + inward.y * 1.3f);
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            // [무번호 88] 대각벽 중앙에는 3.2m 포탈 — 벤치 중심을 벽 방향 +4.1m 오프셋(포탈 절반 1.6+벤치 절반 2.0+간격 0.5).
            Vector2 off = dir * 4.1f;
            BuildBench(parent, name, new Vector3(pos.x + off.x, 0f, pos.y + off.y), yaw, lengthM);   // 절차(정확 치수, GLB 종횡비 왜곡 회피)
        }

        /// <summary>절차 긴 벤치 — 좌판+등받이+다리 2(설계도 긴 원목 벤치). 좌판 상판 ~1.1m·전고 ~2.3m.</summary>
        private static GameObject BuildBench(Transform parent, string name, Vector3 pos, float yawDeg, float lengthM)
        {
            var woodMat = InteriorQualityBaseline.GetTrimMat("benchWood", new Color(0.44f, 0.31f, 0.19f));
            var bench = new GameObject(name);
            bench.transform.SetParent(parent, false);
            bench.transform.localPosition = pos;
            bench.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            InteriorQualityBaseline.AddBox(bench.transform, name + "_Seat", new Vector3(0f, 1.0f, 0f), new Vector3(lengthM, 0.22f, 1.1f), woodMat);
            InteriorQualityBaseline.AddBox(bench.transform, name + "_Back", new Vector3(0f, 1.7f, -0.5f), new Vector3(lengthM, 1.2f, 0.14f), woodMat);
            for (int i = -1; i <= 1; i += 2)
                InteriorQualityBaseline.AddBox(bench.transform, name + "_Leg" + (i > 0 ? "E" : "W"),
                    new Vector3(i * (lengthM * 0.42f), 0.45f, 0f), new Vector3(0.24f, 0.9f, 0.92f), woodMat);
            return bench;
        }

        /// <summary>절차 벽난로 — 석재 몸체+개구부(내벽 검정)+마틸 상판. [무번호 88] 설계도 벽난로(로비 12.7m/침실 9m).</summary>
        private static GameObject BuildFireplace(Transform parent, string name, Vector3 pos, float yawDeg, float widthM)
        {
            var stoneMat = InteriorQualityBaseline.GetTrimMat("fireplaceStone", new Color(0.52f, 0.51f, 0.48f));
            var darkMat = InteriorQualityBaseline.GetTrimMat("fireplaceInner", new Color(0.12f, 0.09f, 0.08f));
            var fp = new GameObject(name);
            fp.transform.SetParent(parent, false);
            fp.transform.localPosition = pos;
            fp.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            float h = Mathf.Min(5.5f, widthM * 0.45f);
            InteriorQualityBaseline.AddBox(fp.transform, name + "_Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(widthM, h, 1.1f), stoneMat);
            InteriorQualityBaseline.AddBox(fp.transform, name + "_Opening", new Vector3(0f, h * 0.28f, 0.56f), new Vector3(widthM * 0.55f, h * 0.56f, 0.12f), darkMat);
            InteriorQualityBaseline.AddBox(fp.transform, name + "_Mantel", new Vector3(0f, h * 0.64f, 0.64f), new Vector3(widthM * 1.08f, 0.3f, 0.36f), stoneMat);
            return fp;
        }

        /// <summary>절차 장롱형 옷장 — 벽면 세로 긴 옷장(몸체+이문 판). [무번호 88] 침실 서벽 10m.</summary>
        private static GameObject BuildWardrobe(Transform parent, string name, Vector3 pos, float yawDeg, float lengthM)
        {
            var bodyMat = InteriorQualityBaseline.GetTrimMat("wardrobeWood", new Color(0.38f, 0.26f, 0.16f));
            var doorMat = InteriorQualityBaseline.GetTrimMat("wardrobeDoor", new Color(0.30f, 0.20f, 0.12f));
            var w = new GameObject(name);
            w.transform.SetParent(parent, false);
            w.transform.localPosition = pos;
            w.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            InteriorQualityBaseline.AddBox(w.transform, name + "_Body", new Vector3(0f, 1.55f, 0f), new Vector3(lengthM, 3.1f, 1.15f), bodyMat);
            InteriorQualityBaseline.AddBox(w.transform, name + "_Doors", new Vector3(0f, 1.55f, 0.6f), new Vector3(lengthM * 0.92f, 2.7f, 0.06f), doorMat);
            return w;
        }

        /// <summary>절차 소파 — 좌석+등받이+팔걸이 2. [무번호 88] 침실 남측 7.2m.</summary>
        private static GameObject BuildSofa(Transform parent, string name, Vector3 pos, float yawDeg, float lengthM)
        {
            var seatMat = InteriorQualityBaseline.GetTrimMat("sofaSeat", new Color(0.55f, 0.22f, 0.20f));
            var woodMat = InteriorQualityBaseline.GetTrimMat("sofaWood", new Color(0.34f, 0.23f, 0.14f));
            var s = new GameObject(name);
            s.transform.SetParent(parent, false);
            s.transform.localPosition = pos;
            s.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            InteriorQualityBaseline.AddBox(s.transform, name + "_Seat", new Vector3(0f, 0.95f, 0.15f), new Vector3(lengthM, 0.5f, 1.7f), seatMat);
            InteriorQualityBaseline.AddBox(s.transform, name + "_Back", new Vector3(0f, 1.75f, -0.72f), new Vector3(lengthM, 1.4f, 0.3f), seatMat);
            for (int i = -1; i <= 1; i += 2)
                InteriorQualityBaseline.AddBox(s.transform, name + "_Arm" + (i > 0 ? "E" : "W"),
                    new Vector3(i * (lengthM * 0.5f - 0.25f), 1.35f, 0.15f), new Vector3(0.5f, 0.9f, 1.7f), woodMat);
            return s;
        }

        /// <summary>절차 스탠드 선반 — 프레임+판 n단(성 스케일 정확 치수). [무번호 88] 창고/연금 선반.</summary>
        private static GameObject BuildShelf(Transform parent, string name, Vector3 pos, float yawDeg,
            float widthM, float heightM, float depthM, int shelves)
        {
            var woodMat = InteriorQualityBaseline.GetTrimMat("shelfWood", new Color(0.42f, 0.30f, 0.18f));
            var shelf = new GameObject(name);
            shelf.transform.SetParent(parent, false);
            shelf.transform.localPosition = pos;
            shelf.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            for (int i = 0; i < shelves; i++)
            {
                float y = heightM * (i + 1) / (shelves + 1);
                InteriorQualityBaseline.AddBox(shelf.transform, name + "_Board" + i, new Vector3(0f, y, 0f), new Vector3(widthM, 0.14f, depthM), woodMat);
            }
            for (int i = -1; i <= 1; i += 2)
                InteriorQualityBaseline.AddBox(shelf.transform, name + "_Post" + (i > 0 ? "E" : "W"),
                    new Vector3(i * (widthM * 0.5f - 0.15f), heightM * 0.5f, 0f), new Vector3(0.22f, heightM, depthM * 0.9f), woodMat);
            return shelf;
        }

        /// <summary>영주 성(플레이어/영주 공용) 가구 패스 — BuildPlayerCastleInterior/BuildCastleInterior 말미 호출.</summary>
        public static void ApplyCastle(GameObject room, InteriorLayoutDefinition layout, string nationStyle)
        {
            if (room == null || layout == null) return;
            // [무번호 88] seed 폐기 — 결정론 변형 선택은 카탈로그 내부(PickSuffix)로 이관된 상태에서 본 패스 사용처 소진.

            Vector2[] lobby = layout.GetLobbyVertices();
            float x0 = 1f, z0 = 1f;
            foreach (var v in lobby)
            {
                x0 = Mathf.Max(x0, Mathf.Abs(v.x));
                z0 = Mathf.Max(z0, Mathf.Abs(v.y));
            }
            float hw = layout.RoomWidth * 0.5f;
            float hd = layout.RoomDepth * 0.5f;
            // [존 경계 — InteriorLayoutDefinition 존 규칙 동일 공식] 로비 북변 절반폭/측면 x/날개 경계 y.
            float northHalfWidth = Mathf.Abs(lobby[1].x);   // 18(기준형)
            float sideHalfWidth = lobby[2].x;               // 30(기준형)
            float wingMidY = lobby[2].y;                    // 8(기준형)

            // [무번호 32] 영지 실내 공통 석타일 단일 소스 위임 — 국가 키 폐기(씬 통일 계약).
            var stoneMat = InteriorQualityBaseline.GetCommonInteriorFloorMat();
            // [무번호 15] 저장고 전용 어두운 타일 폐기 — 전 방 공통 석타일(사용자 지시 "저장고 타일도 나머지와 동일").
            //   [무번호 15] woodMat/woodFloorMat/mattressMat 로컬 폐기 — 침대·목재 바닥 오버레이가 빌더/설계도 정합으로 이관되어 미사용.

            // ── [무번호 88 — 설계도 정밀 재배치] 13패스 격자 실측(docs/plans/2026-11-20_no88-lord-interior-furnishing-analysis.md) 기준.
            //   크기·개수·종류를 설계도 방 폭 대비 비율로 정합(사용자 지시 — [가구크기표준 무번호 16] 수치 대체). 조명 계약(46/48차) 유지.
            // ── 로비 [설계도]: 북벽 벽난로(폭 21%≈12.7m) + 사대각 벤치 4(대각벽 78%≈10.6m) + 중앙 카펫(36%×27%≈24×11m)
            //   + 벽부착 횃불 E/W 2(남입구 2는 입구홀 소유) + 바닥 석판 데칼 3(입구 좌우 2+중앙 1). 구 모서리 브레이저 스탠드 4 제거.
            var lobbyRoot = Root(room, "QualityFurnish_Lobby");
            // 벽난로 — 북벽 중앙 밀착(설계도 "북쪽 중앙 벽난로" — 구 중앙 배치에서 이동). 폭 12.7m — 절차 석재(정확 치수).
            BuildFireplace(lobbyRoot.transform, "Lobby_Fireplace", new Vector3(0f, 0f, z0 - 1.6f), 0f, 12.7f);
            // 사대각 벤치 4 — 대각벽(13.4m)의 중앙 포탈(3.2m)을 피해 문 옆 오프셋 배치·등이 벽
            //   (설계도 "대각선 벽면을 따라 배치된 4개의 긴 나무 벤치" — 벤치=대각벽 ~30% ≈ 4.0m. 포탈 중앙 배치와 기하 양립).
            PlaceWallBench(lobbyRoot.transform, "Lobby_Bench_NE", lobby[1], lobby[2], 4.0f);
            PlaceWallBench(lobbyRoot.transform, "Lobby_Bench_SE", lobby[3], lobby[4], 4.0f);
            PlaceWallBench(lobbyRoot.transform, "Lobby_Bench_SW", lobby[5], lobby[6], 4.0f);
            PlaceWallBench(lobbyRoot.transform, "Lobby_Bench_NW", lobby[7], lobby[0], 4.0f);
            // 벽부착 횃불 2 — E/W 벽 중앙(설계도 "서쪽 벽 1, 동쪽 벽 1") — targetHeight 0(벽 부착 계약).
            InteriorPropCatalog.PlacePolyfork(lobbyRoot.transform, "wall-torch-bracket-f5ad14", "Lobby_Torch_W", new Vector3(-x0 + 0.3f, 0f, 3.0f), 90f, 0f);
            InteriorPropCatalog.PlacePolyfork(lobbyRoot.transform, "wall-torch-bracket-f5ad14", "Lobby_Torch_E", new Vector3(x0 - 0.3f, 0f, -3.0f), -90f, 0f);
            // 중앙 카펫 — 24×11m(설계도 격자 실측 36%×27%).
            InteriorQualityBaseline.AddRug(room, "Lobby_Rug", new Vector3(0f, 0f, 0f), 24f, 11f,
                InteriorQualityBaseline.GetRugMat("lobbyRug", new Color(0.52f, 0.16f, 0.14f), new Color(0.30f, 0.20f, 0.12f)));
            // 바닥 석판 데칼 3 — 남입구 좌우 2 + 중앙 1(설계도 "문양이 새겨진 석판").
            var decalMat = InteriorQualityBaseline.GetTrimMat("lobbyStoneDecal", new Color(0.55f, 0.54f, 0.50f));
            InteriorQualityBaseline.AddBox(room.transform, "Lobby_Decal_W", new Vector3(-4.5f, 0.012f, -11.3f), new Vector3(2.2f, 0.024f, 2.2f), decalMat);
            InteriorQualityBaseline.AddBox(room.transform, "Lobby_Decal_E", new Vector3(4.5f, 0.012f, -11.3f), new Vector3(2.2f, 0.024f, 2.2f), decalMat);
            InteriorQualityBaseline.AddBox(room.transform, "Lobby_Decal_C", new Vector3(0f, 0.012f, -11.8f), new Vector3(2.6f, 0.024f, 2.6f), decalMat);
            // 천장 샹들리에 2 — 조명 인프라(설계도 도면 외 — 조명 계약 유지).
            InteriorPropCatalog.PlacePolyfork(lobbyRoot.transform, "iron-chandelier-1337dc", "Lobby_Lamp_N", new Vector3(0f, 0f, z0 * 0.34f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(lobbyRoot.transform, "iron-chandelier-1337dc", "Lobby_Lamp_S", new Vector3(0f, 0f, -z0 * 0.34f), 0f, 0f);
            IndoorLighting.AddPointLight(lobbyRoot, new Vector3(0f, 3.6f, z0 * 0.34f), new Color(1f, 0.80f, 0.55f), 7f, 1.7f, true, true);
            IndoorLighting.AddPointLight(lobbyRoot, new Vector3(0f, 3.6f, -z0 * 0.34f), new Color(1f, 0.80f, 0.55f), 7f, 1.7f, true, true);
            // [무번호 15] 설계도 외 제거 — 왕좌/촛대/올리브/커튼/바로크 벽/랜턴/금화·보물/하프·피아노·하프시코드/
            //   용화 화로(중앙 벽난로로 승격)/보물상/벨벳 발판/원정 지도/기사 석상/천장 걸이 화분 — 로비는 설계도 3요소만.

            // ── 침실(북) [설계도]: 캐노피 대형 침대(북벽 중앙·9×11m — 빌더 기능침대 확대+앵커 이동은 빌더 측 [무번호 88])
            //   + 침대 카펫(43%×31%≈15.5×9.3m) + 옷장(서벽·길이 10m) + 책상+의자 2세트(북동/동벽) + 소파+벽난로(남측)
            //   + 상자 1(남서) + 횃불 4(북벽 2+남입구 2). 구 협탁 2·촛불 2 제거(설계도 부재).
            var bedroomRoot = Root(room, "QualityFurnish_Bedroom");
            const float BedCX = 0f, BedCZ = 35.5f;   // [무번호 88] 빌더 기능침대 최종 중심(북벽 밀착 3.2m 간격)과 정합
            InteriorQualityBaseline.AddRug(room, "Bedroom_Carpet", new Vector3(BedCX, 0f, BedCZ), 15.5f, 9.3f,
                InteriorQualityBaseline.GetRugMat("bedroomCarpet", new Color(0.52f, 0.16f, 0.14f), new Color(0.30f, 0.20f, 0.12f)));
            // 캐노피 — 기능침대(9×11m) 4주+상부 프레임 3재(설계도 "캐노피 대형 침대") — 렌더러 전용.
            var canopyMat = InteriorQualityBaseline.GetTrimMat("bedroomCanopy", new Color(0.40f, 0.28f, 0.17f));
            for (int i = 0; i < 4; i++)
            {
                float px = BedCX + (i % 2 == 0 ? -4.3f : 4.3f);
                float pz = BedCZ + (i < 2 ? 5.3f : -5.3f);
                InteriorQualityBaseline.AddBox(bedroomRoot.transform, $"Bedroom_CanopyPost_{i}", new Vector3(px, 2.3f, pz), new Vector3(0.2f, 4.6f, 0.2f), canopyMat);
            }
            InteriorQualityBaseline.AddBox(bedroomRoot.transform, "Bedroom_CanopyBar_N", new Vector3(BedCX, 4.68f, BedCZ + 5.3f), new Vector3(8.9f, 0.18f, 0.18f), canopyMat);
            InteriorQualityBaseline.AddBox(bedroomRoot.transform, "Bedroom_CanopyBar_S", new Vector3(BedCX, 4.68f, BedCZ - 5.3f), new Vector3(8.9f, 0.18f, 0.18f), canopyMat);
            InteriorQualityBaseline.AddBox(bedroomRoot.transform, "Bedroom_CanopyBar_W", new Vector3(BedCX - 4.3f, 4.68f, BedCZ), new Vector3(0.18f, 0.18f, 10.9f), canopyMat);
            InteriorQualityBaseline.AddBox(bedroomRoot.transform, "Bedroom_CanopyBar_E", new Vector3(BedCX + 4.3f, 4.68f, BedCZ), new Vector3(0.18f, 0.18f, 10.9f), canopyMat);
            // 대형 옷장 1 — 서벽 중앙부, 세로 길이 10m(설계도 "서쪽의 대형 옷장" — 격자 실측 33% 깊이).
            // 대형 옷장 1 — 서벽 중앙부, 세로 길이 10m(설계도 "서쪽의 대형 옷장") — 절차 장롱형(정확 치수).
            BuildWardrobe(bedroomRoot.transform, "Bedroom_Wardrobe", new Vector3(-northHalfWidth + 1.8f, 0f, 33.5f), 90f, 10f);
            // 책상+의자 2세트 — 북동 코너 + 동벽(설계도 2세트). 폭 5.4m(격자 실측 ~19%).
            InteriorQualityBaseline.BuildWoodTable(bedroomRoot.transform, "Bedroom_Desk_E", new Vector3(15.4f, 0f, 30.5f), 90f, 5.4f, 2.2f, 0.95f);
            InteriorQualityBaseline.BuildWoodChair(bedroomRoot.transform, "Bedroom_DeskChair_E", new Vector3(13.7f, 0f, 30.5f), -90f);
            // 소파 + 벽난로 — 남측 중앙(설계도 "남쪽 중앙 벽난로+2인용 소파"). 소파 7.2m/벽난로 9m.
            BuildSofa(bedroomRoot.transform, "Bedroom_Sofa", new Vector3(-8f, 0f, 20.9f), 180f, 7.2f);   // [무번호 88] 절차 소파(정확 치수) — 남벽 중앙 문 회피 서측
            BuildFireplace(bedroomRoot.transform, "Bedroom_Fireplace", new Vector3(-8f, 0f, 15.6f), 180f, 9f);   // [무번호 88] 절차 석재 벽난로 — 문 회피 서측
            // 상자 1 — 남서(설계도 "푸른색 상자").
            InteriorPropCatalog.PlacePolyfork(bedroomRoot.transform, "timber-crate-ba0739", "Bedroom_Crate", new Vector3(-13.2f, 0f, 19.8f), 0f, 0f);
            IndoorLighting.AddPointLight(bedroomRoot, new Vector3(0f, 3.2f, BedCZ), new Color(1f, 0.78f, 0.52f), 6.8f, 1.6f, true, true);
            // 벽 횔도 4 — 북벽 침대 양옆 2 + 남입구 양옆 2(설계도 4개 — origin_y 빌드·targetHeight 0 필수)
            InteriorPropCatalog.PlacePolyfork(bedroomRoot.transform, "wall-torch-bracket-f5ad14", "Bedroom_Torch_NW", new Vector3(-9.5f, 0f, hd - 0.25f), 180f, 0f);
            InteriorPropCatalog.PlacePolyfork(bedroomRoot.transform, "wall-torch-bracket-f5ad14", "Bedroom_Torch_NE", new Vector3(9.5f, 0f, hd - 0.25f), 180f, 0f);
            InteriorPropCatalog.PlacePolyfork(bedroomRoot.transform, "wall-torch-bracket-f5ad14", "Bedroom_Torch_SW", new Vector3(-6f, 0f, z0 + 0.25f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(bedroomRoot.transform, "wall-torch-bracket-f5ad14", "Bedroom_Torch_SE", new Vector3(6f, 0f, z0 + 0.25f), 0f, 0f);

            // ── 제작소(북서) [설계도]: 중앙 화로(북벽·5.4m) + 원형 가마(북동·8.2m) + 모루(북서·3.3m) + 작업대 4종
            //   (도구 북서 6.5m/가죽가공 동벽/갑옷 남동 8.7m/가죽 남서 5.4m) + 서벽 선반·소형 작업대 2 + 장작 2 + 원자재 상자 3.
            var craftRoot = Root(room, "QualityFurnish_Craft");
            // [존 규칙 준수] Craft = NW 블록(x<−18, z>8) + 중앙 서측 스트립(x<−30, −8≤z≤8) — 포탈(Lobby_West x=−30 z=0·NW 대각) 통행 확보.
            // 중앙 화로 — 북벽 중앙(설계도 "대형 화로"). 폭 5.4m(격자 13%).
            var craftForge = InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "blacksmith-forge-09fa85", "Craft_Forge", new Vector3(-38.4f, 0f, 41.6f), 180f, 0f);
            NormalizeMaxAxis(craftForge, 5.4f);
            // 원형 가마 — 북동 코너(설계도 "원형 가마/화로"). 폭 8.2m(격자 20%).
            var craftSmelter = InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "smelting-furnace-3209f1", "Craft_Smelter", new Vector3(-21.8f, 0f, 41.2f), 180f, 0f);
            NormalizeMaxAxis(craftSmelter, 8.2f);
            // 모루 — 북서 바닥(설계도 "모루"). 자연 크기 기준 3.3m.
            var craftAnvil = InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "anvil-1286cb", "Craft_Anvil", new Vector3(-47.5f, 0f, 34.5f), -45f, 0f);
            NormalizeMaxAxis(craftAnvil, 3.3f);
            // 도구 작업대(북서) + 도구 선반(서벽) — 설계도 "망치/집게가 놓인 긴 탁자". 폭 6.5m.
            InteriorQualityBaseline.BuildWoodTable(craftRoot.transform, "Craft_ToolBench", new Vector3(-52.5f, 0f, 38.2f), 0f, 6.5f, 2.4f, 0.95f);
            InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "tool-rack-f77b81", "Craft_ToolRack", new Vector3(-58.2f, 0f, 30f), 90f, 3.5f);   // [무번호 88] 높이 정규화(성 스케일) — 길이 정규화 왜곡 회피
            // 가죽 가공대 — 동벽 세로(설계도 "가죽이 펼쳐진 가공대").
            InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "laced-hide-stretching-rack-8152a1", "Craft_HideRack", new Vector3(-19.7f, 0f, 26f), -90f, 0f);
            // 갑옷 작업대(남동·폭 8.7m) + 갑옷 거치대(설계도 "투구 부품과 도구").
            InteriorQualityBaseline.BuildWoodTable(craftRoot.transform, "Craft_ArmourBench", new Vector3(-27f, 0f, 12.3f), 0f, 8.7f, 2.4f, 0.95f);
            InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "armour-stand-3d77c4", "Craft_ArmourStand", new Vector3(-21.2f, 0f, 14.2f), 15f, 0f);
            // 가죽 작업대(남서·폭 5.4m) + 서벽 소형 작업대 2(설계도 "재봉틀/소형 작업대").
            InteriorQualityBaseline.BuildWoodTable(craftRoot.transform, "Craft_LeatherBench", new Vector3(-52f, 0f, 12.3f), 0f, 5.4f, 2.2f, 0.95f);
            InteriorQualityBaseline.BuildWoodTable(craftRoot.transform, "Craft_SideBench_N", new Vector3(-57f, 0f, 24f), 90f, 2.4f, 1.0f, 0.95f);
            InteriorQualityBaseline.BuildWoodTable(craftRoot.transform, "Craft_SideBench_S", new Vector3(-57f, 0f, 20.2f), 90f, 2.4f, 1.0f, 0.95f);
            // 장작 더미 2 — 화로 옆 + 남측(설계도 "장작 더미").
            InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "firewood-stack-8a2899", "Craft_Firewood_0", new Vector3(-34.5f, 0f, 39.8f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "firewood-stack-8a2899", "Craft_Firewood_1", new Vector3(-45f, 0f, 11.6f), 25f, 0f);
            // 원자재 상자 3 — 원형 가마 서측 클러스터(설계도 "Raw materials").
            InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "timber-crate-ba0739", "Craft_Material_0", new Vector3(-28f, 0f, 37.5f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "timber-crate-ba0739", "Craft_Material_1", new Vector3(-28f, 0f, 35.9f), 15f, 0f);
            InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "timber-crate-ba0739", "Craft_Material_2", new Vector3(-29.6f, 0f, 36.9f), -10f, 0f);
            IndoorLighting.AddPointLight(craftRoot, new Vector3(-36f, 3.0f, 39.5f),
                new Color(1f, 0.42f, 0.18f), 6.8f, 1.9f, true, true);
            // 벽 횔도 1 — 서벽 라인(조명 인프라 유지 — origin_y=2.1 빌드·targetHeight 0 필수)
            InteriorPropCatalog.PlacePolyfork(craftRoot.transform, "wall-torch-bracket-f5ad14", "Craft_Torch_W", new Vector3(-hw + 0.25f, 0f, 20f), 90f, 0f);

            // ── 창고(북동) [설계도]: 북벽 선반 3(각 8.2m — Valuables×2+Logistics) + 동벽 긴 선반(15m) + 서벽 긴 탁자(15m)+자루 6
            //   + 물류 책상+의자(남동·8.6m) + 보물상자(북서) + 상자 3 + 통 3 + 바구니 1. 구 동벽 선반 4·데스크 2·항아리 3 제거.
            var storageRoot = Root(room, "QualityFurnish_Storage");
            // 북벽 선반 3 — Valuables(좌·우)+Logistics desk(중앙)(설계도 라벨). 각 폭 8.2m(격자 20%).
            for (int i = 0; i < 3; i++)
            {
                BuildShelf(storageRoot.transform, $"Storage_NShelf_{i}", new Vector3(28f + i * 10.4f, 0f, 42.3f), 180f,
                    8.2f, 4.5f, 1.2f, 3);   // [무번호 88] 절차 선반 8.2×4.5×1.2(3단 — 정확 치수)
            }
            // 동벽 긴 선반 — 세로 15m(설계도 "동벽 긴 선반").
            BuildShelf(storageRoot.transform, "Storage_EastShelf", new Vector3(57.4f, 0f, 26f), -90f, 15f, 4.5f, 1.2f, 3);   // [무번호 88] 동벽 긴 선반 15m(3단)
            // 서벽 긴 탁자 — x=18 벽 라인, 세로 15m(설계도 "서벽 긴 탁자")+자루 6(바닥 3+상판 3).
            var storageLongTable = InteriorQualityBaseline.BuildWoodTable(storageRoot.transform, "Storage_LongTable", new Vector3(19.9f, 0f, 21.5f), 90f, 15f, 2.2f, 2.3f);   // [무번호 88] 절차 긴 탁자(정확 치수)
            float storageTopY = TopY(storageLongTable, room.transform);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "grain-sack-401d96", "Storage_Sack_F0", new Vector3(21.8f, 0f, 15.5f), 15f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "grain-sack-401d96", "Storage_Sack_F1", new Vector3(22.6f, 0f, 16.8f), -20f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "loot-sack-4762f5", "Storage_Sack_F2", new Vector3(21.5f, 0f, 18.2f), 40f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "grain-sack-401d96", "Storage_Sack_T0", new Vector3(19.9f, storageTopY - 0.15f, 17f), 10f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "loot-sack-4762f5", "Storage_Sack_T1", new Vector3(19.9f, storageTopY - 0.15f, 20.5f), -25f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "grain-sack-401d96", "Storage_Sack_T2", new Vector3(19.9f, storageTopY - 0.15f, 24f), 50f, 0f);
            // 물류 책상+의자 — 남동(설계도 "서류/동전/주머니가 놓인 작업대"). 폭 8.6m(격자 21%).
            InteriorQualityBaseline.BuildWoodTable(storageRoot.transform, "Storage_Desk", new Vector3(44f, 0f, 11.6f), 0f, 8.6f, 2.6f, 0.95f);
            InteriorQualityBaseline.BuildWoodChair(storageRoot.transform, "Storage_DeskChair", new Vector3(44f, 0f, 9.3f), 180f);
            // 바구니 1 — 책상 서측(설계도 "둥근 바구니").
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "woven-storage-basket-983d80", "Storage_Basket", new Vector3(40.2f, 0f, 11.4f), 0f, 0f);
            // 보물상자(북서) + 상자 3 + 통 3(설계도 "보물상자/나무 상자/나무통").
            var storageStrongbox = InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "iron-banded-strongbox-698383", "Storage_Strongbox", new Vector3(21f, 0f, 41f), 0f, 0f);
            NormalizeMaxAxis(storageStrongbox, 5.7f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "timber-crate-ba0739", "Storage_Crate_0", new Vector3(55.8f, 0f, 37.2f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "timber-crate-ba0739", "Storage_Crate_1", new Vector3(47f, 0f, 11.7f), 15f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "timber-crate-ba0739", "Storage_Crate_2", new Vector3(48.5f, 0f, 12.5f), -10f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "oak-barrel-b65311", "Storage_Barrel_0", new Vector3(52.5f, 0f, 41.2f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "oak-barrel-b65311", "Storage_Barrel_1", new Vector3(54.3f, 0f, 41.5f), 30f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "oak-barrel-b65311", "Storage_Barrel_2", new Vector3(55.7f, 0f, 34.6f), 0f, 0f);
            // 횃불 2 — 북벽 선반 사이(설계도 "선반 사이 횃불 2").
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "wall-torch-bracket-f5ad14", "Storage_Torch_W", new Vector3(33.2f, 0f, 42.45f), 180f, 0f);
            InteriorPropCatalog.PlacePolyfork(storageRoot.transform, "wall-torch-bracket-f5ad14", "Storage_Torch_E", new Vector3(43.6f, 0f, 42.45f), 180f, 0f);
            // [존 규칙 준수] 창고 바닥 오버레이 2장 — [무번호 15] 전 방 공통 석타일(어두운 타일 폐기 — 사용자 지시).
            InteriorQualityBaseline.AddFloorOverlay(room, "Storage_StoneFloor_Strip",
                new Vector3((sideHalfWidth + hw) * 0.5f, 0f, 0f), hw - sideHalfWidth - 1f, 16f, stoneMat);
            InteriorQualityBaseline.AddFloorOverlay(room, "Storage_StoneFloor_NE",
                new Vector3((northHalfWidth + hw) * 0.5f, 0f, (wingMidY + hd) * 0.5f),
                hw - northHalfWidth - 1f, hd - wingMidY - 1f, stoneMat);
            IndoorLighting.AddPointLight(storageRoot, new Vector3(52f, 3.4f, 0f),   // [무번호 88] 날개 스트립 중앙(구 storageX 참조 교체)
                new Color(1f, 0.72f, 0.50f), 6.8f, 1.2f, true, false);

            // ── 병영(남서) [설계도]: 작전 테이블 2(북벽 라인·11~12m·지도) + 집무 책상+의자(북동·6.8m) + 중앙 보관함(5.7m)
            //   + 훈련 더미 1(동측 — 신규 Polyfork). 기능침대는 빌더 BarrackBed 20(병사 수용 계약 — TerritoryBuilder 1명/침대) 유지 —
            //   설계도 표기 6개와의 편차는 기능 계약 우선(보고 명시).
            var barracksRoot = Root(room, "QualityFurnish_Barracks");
            // 작전 테이블 2 — 북서(원정 지도)+북중앙(전술 지판)(설계도 "지도가 펼쳐진 대형 테이블 2"). 폭 11/12m(격자 20~25%).
            Vector3 warTablePos = new Vector3(-45f, 0f, -17.6f);
            var warTableW = InteriorQualityBaseline.BuildWoodTable(barracksRoot.transform, "Barracks_WarTable_W", warTablePos, 0f, 11f, 2.6f, 0.95f);
            InteriorPropCatalog.PlacePolyfork(barracksRoot.transform, "campaign-map-scroll-d4a382", "Barracks_WarMap", new Vector3(warTablePos.x, TopY(warTableW, room.transform) - 0.02f, warTablePos.z), 0f, 0f);
            Vector3 warTablePos2 = new Vector3(-26f, 0f, -17.2f);
            var warTableE = InteriorQualityBaseline.BuildWoodTable(barracksRoot.transform, "Barracks_WarTable_E", warTablePos2, 0f, 12f, 2.8f, 0.95f);
            InteriorPropCatalog.PlacePolyfork(barracksRoot.transform, "battle-map-tile-69e0b1", "Barracks_WarMap_Tile", new Vector3(warTablePos2.x, TopY(warTableE, room.transform) - 0.02f, warTablePos2.z), 12f, 0f);
            // 집무 책상+의자 — 북동(설계도 "장부와 책"). 폭 6.8m(격자 12%) — 기능침대 열(x≤−8.3) 회피 세로 배치.
            InteriorQualityBaseline.BuildWoodTable(barracksRoot.transform, "Barracks_Desk", new Vector3(-4.8f, 0f, -18f), 90f, 6.8f, 2.4f, 0.95f);
            InteriorQualityBaseline.BuildWoodChair(barracksRoot.transform, "Barracks_DeskChair", new Vector3(-7f, 0f, -18f), 90f);
            // 중앙 보관함 — 입구 안쪽(설계도 "침대 사이 나무 상자"). 폭 5.7m(격자 10%).
            var barracksStrongbox = InteriorPropCatalog.PlacePolyfork(barracksRoot.transform, "iron-banded-strongbox-698383", "Barracks_Strongbox", new Vector3(-30f, 0f, -28.5f), 0f, 0f);
            NormalizeMaxAxis(barracksStrongbox, 5.7f);
            // 훈련 더미 — 동측(설계도 "갑옷을 입힌 목인형" — [무번호 88] Polyfork 신규 수용).
            InteriorPropCatalog.PlacePolyfork(barracksRoot.transform, "straw-training-dummy-64eeb4", "Barracks_TrainingDummy", new Vector3(-6.8f, 0f, -28f), -15f, 0f);
            IndoorLighting.AddPointLight(barracksRoot, new Vector3(-34f, 3.0f, -22f),
                new Color(1f, 0.76f, 0.50f), 6.6f, 1.5f, true, true);

            // ── 연금술소(남동) [설계도]: 시약 선반 2단(남서·8.2m) + 가마솥(남중앙·6m) + 증류 실험대(남중앙·11.3m)
            //   + 증류기(신규 Polyfork)+플라스크 2(상판 위) + 절구 2(남동·석기) + 동벽 실험판(세로 7.5m).
            //   구 맷돌(saddle-quern)·촛불 제거(설계도 부재) — 수용은 skip 유지 전환.
            var alchemyRoot = Root(room, "QualityFurnish_Alchemy");
            // 시약 선반 2단 — 남서, 남벽 밀착(설계도 "상단 큰 시약병 4+하단 소병 6"). 폭 8.2m(격자 15~22%).
            BuildShelf(alchemyRoot.transform, "Alchemy_Shelf", new Vector3(13f, 0f, -41.5f), 0f, 8.2f, 4.5f, 1.2f, 3);   // [무번호 88] 시약 선반 8.2m(3단)
            // 가마솥 — 남중앙(설계도 "불 위의 대형 가마솥(녹색 액체)"). 폭 6m(격자 10~12%).
            var alchemyCauldron = InteriorPropCatalog.PlacePolyfork(alchemyRoot.transform, "bubbling-tripod-cauldron-124e15", "Alchemy_Cauldron", new Vector3(27f, 0f, -40f), 0f, 0f);
            NormalizeMaxAxis(alchemyCauldron, 6f);
            // 증류 실험대 + 증류기 + 플라스크 2 — 남중앙(설계도 "삼각대 증류기+수집 플라스크"). 실험대 폭 11.3m(격자 20%).
            var alchemyBench = InteriorQualityBaseline.BuildWoodTable(alchemyRoot.transform, "Alchemy_DistillBench", new Vector3(41f, 0f, -41f), 180f, 11.3f, 2.6f, 2.3f);   // [무번호 88] 절차 실험대(정확 치수)
            float alchemyTopY = TopY(alchemyBench, room.transform);
            var alchemyRetort = InteriorPropCatalog.PlacePolyfork(alchemyRoot.transform, "bubbling-retort-4f9721", "Alchemy_Retort", new Vector3(41f, alchemyTopY - 0.1f, -41.2f), 0f, 0f);
            NormalizeMaxAxis(alchemyRetort, 4f);
            var alchemyFlaskA = InteriorPropCatalog.PlacePolyfork(alchemyRoot.transform, "alchemy-flask-b1b4fb", "Alchemy_Flask_0", new Vector3(37.5f, TopY(alchemyBench, room.transform) - 0.05f, -41.3f), 0f, 0f);
            NormalizeMaxAxis(alchemyFlaskA, 1.2f);
            var alchemyFlaskB = InteriorPropCatalog.PlacePolyfork(alchemyRoot.transform, "alchemy-flask-b1b4fb", "Alchemy_Flask_1", new Vector3(44.5f, TopY(alchemyBench, room.transform) - 0.05f, -41.3f), 30f, 0f);
            NormalizeMaxAxis(alchemyFlaskB, 1.2f);
            string[] potions = { "MP_Potion_1_1", "MP_Potion_2_1", "MP_Potion_3_1", "MP_Potion_4_1", "MP_Potion_1_2", "MP_Jar_1_A" };
            for (int i = 0; i < potions.Length; i++)
            {
                Vector3 pos = new Vector3(36.2f + (i % 3) * 1.1f, alchemyTopY + 0.22f, -40.6f + (i / 3) * 1.4f);
                InteriorPropCatalog.PlaceProp(alchemyRoot.transform, $"Alchemy_Potion{i}", potions[i], pos, i * 37f, 0.22f);
            }
            // 절구 2 — 남동 바닥(설계도 "석제 절구 세트 2" — 석기 대용 taheta). 각 폭 4m(격자 합계 15%).
            var alchemyMortarA = InteriorPropCatalog.PlacePolyfork(alchemyRoot.transform, "taheta-stone-basin-336021", "Alchemy_Mortar_0", new Vector3(52.5f, 0f, -40.2f), 0f, 0f);
            NormalizeMaxAxis(alchemyMortarA, 4f);
            var alchemyMortarB = InteriorPropCatalog.PlacePolyfork(alchemyRoot.transform, "taheta-stone-basin-336021", "Alchemy_Mortar_1", new Vector3(56f, 0f, -40.7f), 40f, 0f);
            NormalizeMaxAxis(alchemyMortarB, 4f);
            // 동벽 실험판 — 벽부착 세로 7.5m(설계도 "원형 플라스크 3+시약병 고정판").
            BuildShelf(alchemyRoot.transform, "Alchemy_WallBoard", new Vector3(57.2f, 0f, -25f), -90f, 7.5f, 4.2f, 0.5f, 3);   // [무번호 88] 동벽 실험판 7.5m(3단·수평판)
            IndoorLighting.AddPointLight(alchemyRoot, new Vector3(38f, 2.8f, -40f),   // [무번호 88] 남벽 설비 라인(구 alcX/alcZ 참조 교체)
                new Color(0.62f, 0.40f, 0.95f), 6.4f, 1.7f, true, false);

            // ── 입구홀(남측 중앙) [무번호 15 — 9/29 설계도 정합]: 러그 스트립 + 벽 횔도(조명) — 설계도 외 수납/화로/갑옷 제거 ──
            var entryRoot = Root(room, "QualityFurnish_Entry");
            InteriorQualityBaseline.AddRug(room, "Entry_RugStrip", new Vector3(0f, 0f, -(z0 + hd) * 0.5f + 1f),
                3.2f, (hd - z0) * 0.7f,
                InteriorQualityBaseline.GetRugMat("entryRug", new Color(0.38f, 0.24f, 0.16f), new Color(0.24f, 0.18f, 0.12f)));
            IndoorLighting.AddPointLight(entryRoot, new Vector3(0f, 3.2f, -(z0 + hd) * 0.5f),
                new Color(1f, 0.78f, 0.52f), 6.6f, 1.4f, true, true);
            // 벽 횔도 2 — 회랑 E/W 벽(조명 인프라 유지 — origin_y=2.1 빌드·targetHeight 0 필수)
            InteriorPropCatalog.PlacePolyfork(entryRoot.transform, "wall-torch-bracket-f5ad14", "Entry_Torch_W", new Vector3(-1.85f, 0f, -20f), 90f, 0f);
            InteriorPropCatalog.PlacePolyfork(entryRoot.transform, "wall-torch-bracket-f5ad14", "Entry_Torch_E", new Vector3(1.85f, 0f, -20f), -90f, 0f);
        }

        /// <summary>
        /// 일반 건물 가구 패스 — 소형 실내(8~15m)용. buildingType 라벨 기준 방별 최소 구성.
        ///   house=침대+식탁 / tavern=탁자세트+잔 / restaurant=탁자세트+주방 / shop=카운터+진열 /
        ///   crafthouse=작업대+화로 / church=벤치열+제단광 / barn=상자+건초톤.
        /// </summary>
        public static void ApplyBuilding(GameObject room, string buildingType, float width, float depth, string nationStyle,
            float wallHeight = 3f)
        {
            if (room == null) return;
            int seed = InteriorQualityBaseline.StableHash($"{buildingType}_{nationStyle}");
            var woodMat = InteriorQualityBaseline.GetTrimMat($"wood_{buildingType}", new Color(0.42f, 0.30f, 0.19f));
            var mattressMat = InteriorQualityBaseline.GetTrimMat("mattress", new Color(0.88f, 0.86f, 0.80f));
            var root = Root(room, "QualityFurnish");
            float hx = width * 0.5f, hz = depth * 0.5f;

            switch (buildingType)
            {
                case "house":
                    InteriorQualityBaseline.BuildBed(root.transform, "House_Bed",
                        new Vector3(-hx + 1.2f, 0f, hz - 1.3f), 180f, woodMat, mattressMat, new Color(0.45f, 0.22f, 0.28f));
                    InteriorQualityBaseline.BuildWoodTable(root.transform, "House_Table", new Vector3(hx - 1.8f, 0f, -hz + 1.6f), 0f);
                    InteriorQualityBaseline.BuildWoodChair(root.transform, "House_Chair_a", new Vector3(hx - 2.9f, 0f, -hz + 1.6f), 90f);
                    InteriorQualityBaseline.BuildWoodChair(root.transform, "House_Chair_b", new Vector3(hx - 0.7f, 0f, -hz + 1.6f), -90f);
                    InteriorPropCatalog.PlaceFurnit(root.transform, "House_Cabinet", new Vector3(-hx + 1.0f, 0f, -hz + 1.0f), 0f, seed + 3, 1.6f);
                    InteriorQualityBaseline.AddRug(room, "House_Rug", new Vector3(0f, 0f, 0f), Mathf.Min(2.6f, width * 0.4f), Mathf.Min(1.8f, depth * 0.4f),
                        InteriorQualityBaseline.GetRugMat("houseRug", new Color(0.45f, 0.17f, 0.15f), new Color(0.28f, 0.20f, 0.12f)));
                    IndoorLighting.AddPointLight(root, new Vector3(0f, 2.2f, 0f), new Color(1f, 0.78f, 0.52f), 6.5f, 1.5f, true, true);
                    // [157차 P3] 가산 — 촛불 1(식탁 위 — BuildWoodTable tableH 0.76f 실측·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "candle-spike-cluster-d7c566", "House_Candle", new Vector3(hx - 1.8f, 0.76f, -hz + 1.6f), 0f, 0f);
                    // [157차 P3] 가산 — Timber Crate 1(남동 구석 — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "timber-crate-ba0739", "House_Crate", new Vector3(hx - 1.4f, 0f, hz - 1.4f), 0f, 0f);
                    // [158차 P1] 가산 — Armchair 1(북서 거실 — 침대 동측 0.9m 이격·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "armchair-980c7c", "House_Armchair", new Vector3(-hx + 2.6f, 0f, hz - 1.0f), 180f, 0f);
                    // [158차 P1] 가산 — Potted Olive Tree 1(남동 코너 — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "potted-olive-tree-7eee05", "House_OliveTree", new Vector3(hx - 1.2f, 0f, -hz + 1.2f), 0f, 0f);
                    // [158차 P1] 가산 — Area Rug 1(중앙 동측 — 기존 House_Rug(중앙) z-fight 회피 x 오프셋·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "area-rug-5c56e2", "House_AreaRug", new Vector3(hx * 0.5f, 0f, 0f), 0f, 0f);
            // [159차 P1] 허브 화분 — 식탁 위(촛불과 0.6m 이격·y 0.76f)
            InteriorPropCatalog.PlacePolyfork(root.transform, "table-herb-pot-3c0cf4", "House_HerbPot", new Vector3(hx - 2.4f, 0.76f, -hz + 1.6f), 0f, 0f);
            // [160차 P1] 바질 화분 — 식탁 위(candle과 0.4m·y 0.76f) + 여행 트렁크 — 입구(cabinet과 1.6m·targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "basil-pot-125949", "House_BasilPot", new Vector3(hx - 1.4f, 0.76f, -hz + 1.6f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "travelling-trunk-2616ab", "House_Trunk", new Vector3(-hx + 2.6f, 0f, -hz + 1.2f), 90f, 0f);
                    break;

                case "tavern":
                    // [가구 v2] 원목 탁자 2세트(사용자 지시 — 테이블 과다 금지·원목만)+원목 스툴.
                    for (int i = 0; i < 2; i++)
                    {
                        float tz = Mathf.Lerp(-hz + 1.8f, hz - 2.2f, i);
                        Vector3 tablePos = new Vector3(-hx + 2.4f, 0f, tz);
                        // [157차 P3] 교체 — Timber Trestle Table(targetHeight=기존 BuildWoodTable tableH 0.76f 실측 유지)
                        InteriorPropCatalog.PlacePolyfork(root.transform, "timber-trestle-table-024d88", $"Tavern_Table{i}", tablePos, 0f, 0.76f);
                        InteriorQualityBaseline.BuildWoodStool(root.transform, $"Tavern_Stool{i}_a", tablePos + new Vector3(0.95f, 0f, 0f), -90f);
                        InteriorQualityBaseline.BuildWoodStool(root.transform, $"Tavern_Stool{i}_b", tablePos + new Vector3(-0.95f, 0f, 0f), 90f);
                        InteriorPropCatalog.PlaceProp(root.transform, $"Tavern_Plate{i}", i % 2 == 0 ? "MP_Plate_1_A" : "MP_Bread_1_A",
                            tablePos + new Vector3(0f, 0.80f, 0f), i * 40f, 0.06f);
                    }
                    InteriorPropCatalog.PlaceOwen(root.transform, "Tavern_Hearth", new Vector3(hx - 1.2f, 0f, hz - 1.4f), -90f);
                    IndoorLighting.AddPointLight(root, new Vector3(0f, 2.6f, 0f), new Color(1f, 0.72f, 0.46f), 6.8f, 1.6f, true, true);
                    // [157차 P3] 가산 — Oak Barrel 2(남동 구석 라인 — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "oak-barrel-b65311", "Tavern_Barrel_0", new Vector3(hx - 1.3f, 0f, -hz + 1.2f), 0f, 0f);
                    InteriorPropCatalog.PlacePolyfork(root.transform, "oak-barrel-b65311", "Tavern_Barrel_1", new Vector3(hx - 1.3f, 0f, -hz + 2.2f), 0f, 0f);
                    // [157차 P3] 가산 — 촛불 1(테이블1 위 — plate 중앙 회피 x+0.5 오프셋)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "candle-spike-cluster-d7c566", "Tavern_Candle", new Vector3(-hx + 2.9f, 0.76f, hz - 2.2f), 0f, 0f);
                    // [158차 P1] 가산 — Back-Bar Shelf 1(북벽 중앙 — targetHeight 0 자연 높이)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "back-bar-shelf-and-f5f279", "Tavern_BackBar", new Vector3(0f, 0f, hz - 0.6f), 180f, 0f);
                    // [158차 P1] 가산 — Wooden Bench 2(테이블 북/남측 1.1m — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "wooden-bench-02e2c8", "Tavern_Bench_0", new Vector3(-hx + 2.4f, 0f, -hz + 0.7f), 0f, 0f);
                    InteriorPropCatalog.PlacePolyfork(root.transform, "wooden-bench-02e2c8", "Tavern_Bench_1", new Vector3(-hx + 2.4f, 0f, hz - 3.3f), 0f, 0f);
                    // [158차 P1] 가산 — Bubbling Tripod Cauldron 1(주방 남동 코너 — barrel 북측 1.2m 이격·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "bubbling-tripod-cauldron-124e15", "Tavern_Cauldron", new Vector3(hx - 1.4f, 0f, -hz + 3.4f), 0f, 0f);
            // [160차 P1] 훈연기 — 주방 코너(cauldron 동측 1.4m·targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "controllable-food-smoker-aac206", "Tavern_Smoker", new Vector3(hx - 1.4f, 0f, -hz + 4.8f), 0f, 0f);
            // [160차 P1] 술집 피아노 — 북벽(back-bar 동측 끝 옆·targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "saloon-upright-piano-4b9c64", "Tavern_Piano", new Vector3(1.8f, 0f, hz - 0.6f), 180f, 0f);
            // [160차 P1] 위스키 세트 — 테이블0 위(서측 테이블 tz=-hz+1.8f·y 0.76f)
            InteriorPropCatalog.PlacePolyfork(root.transform, "whiskey-bottle-and-glass-fa24b4", "Tavern_Whiskey", new Vector3(-hx + 2.9f, 0.76f, -hz + 1.8f), 0f, 0f);
            // [160차 P1] 와인 단지 — 테이블1 위(candle 대각 0.27m 이격·y 0.76f)
            InteriorPropCatalog.PlacePolyfork(root.transform, "clay-wine-jug-b4ebd3", "Tavern_WineJug", new Vector3(-hx + 2.75f, 0.76f, hz - 1.95f), 40f, 0f);
            // [159차 P1] 전설 상자 + 금화 — back-bar 동측(화로와 1.4m 이격·targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "legendary-chest-4abed4", "Tavern_LegendaryChest", new Vector3(hx - 2.6f, 0f, hz - 1.5f), 180f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "gold-coin-stack-a9ef52", "Tavern_CoinStack", new Vector3(hx - 2.6f, 0f, hz - 2.7f), 0f, 0f);
                    break;

                case "restaurant":
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 tablePos = new Vector3(-hx + 2.2f + i * 0.2f, 0f, Mathf.Lerp(-hz + 2.0f, hz - 2.0f, i));
                        // [157차 P3] 교체 — Timber Trestle Table(targetHeight=기존 BuildWoodTable tableH 0.76f 실측 유지)
                        InteriorPropCatalog.PlacePolyfork(root.transform, "timber-trestle-table-024d88", $"Restaurant_Table{i}", tablePos, 0f, 0.76f);
                        InteriorQualityBaseline.BuildWoodChair(root.transform, $"Restaurant_Chair{i}_a", tablePos + new Vector3(1.0f, 0f, 0f), -90f);
                        InteriorQualityBaseline.BuildWoodChair(root.transform, $"Restaurant_Chair{i}_b", tablePos + new Vector3(-1.0f, 0f, 0f), 90f);
                    }
                    InteriorPropCatalog.PlaceKitchen(root.transform, "Restaurant_Pot", "CookingPot_1_A",
                        new Vector3(hx - 1.4f, 0f, -hz + 1.4f), 0f, 0.26f);
                    IndoorLighting.AddPointLight(root, new Vector3(0f, 2.6f, 0f), new Color(1f, 0.80f, 0.58f), 6.6f, 1.5f, true, false);
                    // [157차 P3] 가산 — 촛불 1(테이블1 위 — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "candle-spike-cluster-d7c566", "Restaurant_Candle", new Vector3(-hx + 2.4f, 0.76f, hz - 2.0f), 0f, 0f);
                    // [158차 P1] 가산 — Clay Bread Oven 1(북벽 — targetHeight 0 자연 높이)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "clay-bread-oven-860b06", "Restaurant_Oven", new Vector3(hx - 2.2f, 0f, hz - 1.2f), 180f, 0f);
                    // [158차 P1] 가산 — Wooden Bench 1(테이블0 남측 1.1m — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "wooden-bench-02e2c8", "Restaurant_Bench", new Vector3(-hx + 2.2f, 0f, -hz + 0.9f), 0f, 0f);
                    // [158차 P1] 가산 — Grain Sack 1(주방 서측 — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "grain-sack-401d96", "Restaurant_Sack", new Vector3(hx - 2.6f, 0f, -hz + 2.6f), 15f, 0f);
            // [159차 P1] 농산물 바구니 2(화로 동측·targetHeight 0) + 빵 2(마지막 테이블 위 y 0.76f)
            InteriorPropCatalog.PlacePolyfork(root.transform, "basket-of-produce-7420cf", "Restaurant_Produce_0", new Vector3(hx - 0.9f, 0f, hz - 2.6f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "basket-of-produce-7420cf", "Restaurant_Produce_1", new Vector3(hx - 0.9f, 0f, hz - 3.5f), 15f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "bread-loaf-0e0ab5", "Restaurant_Bread_0", new Vector3(-hx + 3.0f, 0.76f, hz - 2.15f), 10f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "bread-loaf-0e0ab5", "Restaurant_Bread_1", new Vector3(-hx + 3.05f, 0.76f, hz - 1.85f), -15f, 0f);
            // [160차 P1] 콘서트 하프 — 동측 코너(oven/produce 회피·targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "concert-pedal-harp-eb1a21", "Restaurant_ConcertHarp", new Vector3(hx - 1.2f, 0f, 0f), -90f, 0f);
                    break;

                case "shop":
                    // [가구 v2] 카운터도 원목(사용자 지시 — 색상 테이블 배제).
                    InteriorQualityBaseline.BuildWoodTable(root.transform, "Shop_Counter", new Vector3(0f, 0f, -hz + 1.6f), 0f, 2.2f, 0.8f, 0.92f);
                    InteriorPropCatalog.PlaceStandShelf(root.transform, "Shop_Shelf_a", new Vector3(-hx + 1.1f, 0f, 0f), 90f, seed);
                    InteriorPropCatalog.PlaceStandShelf(root.transform, "Shop_Shelf_b", new Vector3(hx - 1.1f, 0f, 0f), -90f, seed + 1);
                    InteriorPropCatalog.PlaceProp(root.transform, "Shop_GoodsA", "MP_Bag_1_A", new Vector3(0.5f, 0.82f, -hz + 1.6f), 0f, 0.24f);
                    InteriorPropCatalog.PlaceProp(root.transform, "Shop_GoodsB", "MP_Apple_1_A", new Vector3(-0.5f, 0.82f, -hz + 1.6f), 0f, 0.10f);
                    IndoorLighting.AddPointLight(root, new Vector3(0f, 2.6f, 0f), new Color(1f, 0.82f, 0.60f), 6.4f, 1.5f, true, false);
                    // [157차 P3] 가산 — Timber Crate 2(북측 구석 라인 — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "timber-crate-ba0739", "Shop_Crate_0", new Vector3(hx - 1.4f, 0f, hz - 1.4f), 0f, 0f);
                    InteriorPropCatalog.PlacePolyfork(root.transform, "timber-crate-ba0739", "Shop_Crate_1", new Vector3(hx - 2.4f, 0f, hz - 1.4f), 0f, 0f);
                    // [158차 P1] 가산 — Woven Storage Basket 2(카운터 서측 옆 진열 — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "woven-storage-basket-983d80", "Shop_Basket_0", new Vector3(-1.9f, 0f, -hz + 1.6f), 0f, 0f);
                    InteriorPropCatalog.PlacePolyfork(root.transform, "woven-storage-basket-983d80", "Shop_Basket_1", new Vector3(-1.9f, 0f, -hz + 2.7f), 20f, 0f);
            // [159차 P1] 농산물 진열 2 + 우유 통 — 카운터 남측(바닥·targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "basket-of-produce-7420cf", "Shop_Produce_0", new Vector3(-0.9f, 0f, -hz + 0.7f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "basket-of-produce-7420cf", "Shop_Produce_1", new Vector3(0.9f, 0f, -hz + 0.7f), 20f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "milk-churn-895735", "Shop_MilkChurn", new Vector3(1.9f, 0f, -hz + 1.2f), 0f, 0f);
            // [160차 P1] 석조 물두멍 — 진열(basket과 1.0m·targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "taheta-stone-basin-336021", "Shop_StoneBasin", new Vector3(-2.9f, 0f, -hz + 1.6f), 0f, 0f);
                    break;

                case "crafthouse":
                    InteriorPropCatalog.PlaceStationTable(root.transform, "Craft_Table_a", new Vector3(-hx + 1.8f, 0f, 0.6f), 90f, seed);
                    InteriorPropCatalog.PlaceStationTable(root.transform, "Craft_Table_b", new Vector3(-hx + 1.8f, 0f, -1.6f), 90f, seed + 1);
                    InteriorPropCatalog.PlaceOwen(root.transform, "Craft_Forge", new Vector3(hx - 1.3f, 0f, 0f), -90f);
                    InteriorQualityBaseline.BuildCrateStack(root.transform, "Craft_Crates", new Vector3(0f, 0f, hz - 1.4f), seed + 7, woodMat);
                    IndoorLighting.AddPointLight(root, new Vector3(hx - 1.6f, 2.2f, 0f), new Color(1f, 0.42f, 0.18f), 6.2f, 1.8f, true, true);
                    // [157차 P3] 가산 — Standing Brazier 1(화로 옆 북측 1.5m — 자연 높이)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "standing-brazier-c9e4ee", "Craft_Brazier", new Vector3(hx - 1.3f, 0f, 1.5f), 0f, 0f);
                    // [158차 P1] 가산 — Anvil 1(화로 남측 1.5m 라인 — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "anvil-1286cb", "CraftHouse_Anvil", new Vector3(hx - 1.3f, 0f, -1.5f), -90f, 0f);
                    // [158차 P1] 가산 — Firewood Stack 1(서벽 라인 — 작업대열 북측 여백·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "firewood-stack-8a2899", "CraftHouse_Firewood", new Vector3(-hx + 1.8f, 0f, hz - 1.3f), 0f, 0f);
            // [160차 P1] 제련로 — 화로 북측 hero(brazier와 1.1m·targetHeight 0 자연 크기)
            InteriorPropCatalog.PlacePolyfork(root.transform, "smelting-furnace-3209f1", "CraftHouse_Smelting", new Vector3(hx - 1.3f, 0f, 2.6f), -90f, 0f);
            // [160차 P1] 공구 랙 + 가죽 건조대 — 서벽 라인(1.6m 이격·targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "tool-rack-f77b81", "CraftHouse_ToolRack", new Vector3(-hx + 1.8f, 0f, hz - 2.8f), 90f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "laced-hide-stretching-rack-8152a1", "CraftHouse_HideRack", new Vector3(-hx + 1.8f, 0f, hz - 4.4f), 90f, 0f);
                    break;

                case "church":
                    // [가구 v2] 벤치도 원목 스툴(유색 쿠션 배제).
                    for (int i = 0; i < 3; i++)
                    {
                        InteriorQualityBaseline.BuildWoodStool(root.transform, $"Church_BenchRow{i}_a", new Vector3(-1.1f, 0f, -1.4f + i * 1.4f), 0f);
                        InteriorQualityBaseline.BuildWoodStool(root.transform, $"Church_BenchRow{i}_b", new Vector3(1.1f, 0f, -1.4f + i * 1.4f), 0f);
                    }
                    InteriorQualityBaseline.AddRug(room, "Church_Aisle", new Vector3(0f, 0f, 0f), 2.2f, depth * 0.55f,
                        InteriorQualityBaseline.GetRugMat("churchRug", new Color(0.30f, 0.26f, 0.40f), new Color(0.55f, 0.45f, 0.20f)));
                    IndoorLighting.AddPointLight(root, new Vector3(0f, 2.8f, hz - 1.2f), new Color(0.85f, 0.86f, 1.0f), 6.8f, 1.6f, true, false);
                    IndoorLighting.AddPointLight(root, new Vector3(0f, 1.4f, hz - 1.4f), new Color(1f, 0.85f, 0.45f), 5f, 1.2f, true, true);
                    // [157차 P3] 가산 — Chained Book Lectern 1(제단 옆 — 회중 남향·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "chained-book-lectern-381597", "Church_Lectern", new Vector3(1.6f, 0f, hz - 1.6f), 180f, 0f);
                    // [158차 P1] 가산 — Pipe Organ 1(북벽 중앙 — 자연 4×8×2m·방고 70% 상한 스케일 보정(wallHeight 실측 파생))
                    float organTargetH = Mathf.Min(8f, wallHeight * 0.7f);
                    InteriorPropCatalog.PlacePolyfork(root.transform, "pipe-organ-36e035", "Church_PipeOrgan", new Vector3(0f, 0f, hz - 1.0f), 180f, organTargetH);
                    // [158차 P1] 가산 — Brass Candelabra 2(제단 양옆 ±2.6 — Lectern 1.0m 이격·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "brass-candelabra-89ab76", "Church_Candelabra_W", new Vector3(-2.6f, 0f, hz - 1.4f), 180f, 0f);
                    InteriorPropCatalog.PlacePolyfork(root.transform, "brass-candelabra-89ab76", "Church_Candelabra_E", new Vector3(2.6f, 0f, hz - 1.4f), 180f, 0f);
                    break;

                case "barn":
                    InteriorQualityBaseline.BuildCrateStack(root.transform, "Barn_Crates_a", new Vector3(-hx + 1.3f, 0f, -hz + 1.3f), seed, woodMat);
                    InteriorQualityBaseline.BuildCrateStack(root.transform, "Barn_Crates_b", new Vector3(hx - 1.3f, 0f, -hz + 1.3f), seed + 3, woodMat);
                    InteriorQualityBaseline.BuildCrateStack(root.transform, "Barn_Crates_c", new Vector3(0f, 0f, hz - 1.4f), seed + 5, woodMat);
                    IndoorLighting.AddPointLight(root, new Vector3(0f, 2.4f, 0f), new Color(1f, 0.75f, 0.50f), 6.5f, 1.3f, true, false);
                    // [157차 P3] 가산 — Oak Barrel 2(북동 구석 라인 — targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "oak-barrel-b65311", "Barn_Barrel_0", new Vector3(hx - 1.3f, 0f, hz - 1.3f), 0f, 0f);
                    InteriorPropCatalog.PlacePolyfork(root.transform, "oak-barrel-b65311", "Barn_Barrel_1", new Vector3(hx - 2.3f, 0f, hz - 1.3f), 0f, 0f);
                    // [157차 P3] 가산 — Timber Crate 2(남측 중앙 — 구석 스택 사이·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "timber-crate-ba0739", "Barn_Crate_0", new Vector3(0f, 0f, -hz + 1.3f), 0f, 0f);
                    InteriorPropCatalog.PlacePolyfork(root.transform, "timber-crate-ba0739", "Barn_Crate_1", new Vector3(1.0f, 0f, -hz + 1.3f), 0f, 0f);
                    // [158차 P1] 가산 — Saddle on a Rack 1(서벽 중앙 — 리그 원본 포즈·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "saddle-on-a-rack-f034cd", "Barn_SaddleRack", new Vector3(-hx + 1.3f, 0f, 0.5f), 90f, 0f);
                    // [158차 P1] 가산 — Treasure Chest 1(동벽 중앙 — 뚜껑 닫힌 리그 원본 포즈·targetHeight 0)
                    InteriorPropCatalog.PlacePolyfork(root.transform, "treasure-chest-373a2e", "Barn_TreasureChest", new Vector3(hx - 1.3f, 0f, 0.5f), -90f, 0f);
            // [159차 P1] 원예 작업대 + 우유 통 2 — 북벽/북서(158차 saddle/chest 회피·targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "potting-bench-3a85ec", "Barn_PottingBench", new Vector3(hx - 1.6f, 0f, hz - 3.0f), 180f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "milk-churn-895735", "Barn_MilkChurn_0", new Vector3(-hx + 1.3f, 0f, hz - 1.4f), 0f, 0f);
            InteriorPropCatalog.PlacePolyfork(root.transform, "milk-churn-895735", "Barn_MilkChurn_1", new Vector3(-hx + 2.2f, 0f, hz - 1.4f), 0f, 0f);
            // [160차 P1] 우유 통 스탠드 — churn 라인 동측(targetHeight 0)
            InteriorPropCatalog.PlacePolyfork(root.transform, "milk-churn-stand-130fe2", "Barn_ChurnStand", new Vector3(-hx + 3.1f, 0f, hz - 1.4f), 0f, 0f);
                    break;
            }

            // [벽 품질 v2] 걸레받이 밴드(E/W 내측)+벽톱 캡(N/E/W) — 성 선례 승계(남측은 수조 절단으로 생략).
            AddRoomTrim(root, buildingType, width, depth, wallHeight);
            // [바닥 품질 v2] 문 앞 마모 데칼(성 AddFloorWearStamps 선례 승계 — 결정론 위치).
            var wearMat = IndoorMaterialFactory.CreateFloorWearMat();
            if (wearMat != null)
            {
                InteriorQualityBaseline.AddFloorOverlay(room, $"Wear_{buildingType}_Door",
                    new Vector3(0f, 0f, -depth * 0.5f + 0.7f), 1.3f, 0.9f, wearMat, 0.018f);
            }
        }

        /// <summary>
        /// [벽 품질 v2] 소형 방 벽 트림 — 걸레받이 밴드(E/W 내측, 높이 0.15m)+벽톱 캡(N/E/W 상단).
        ///   남측 벽은 수조(디오라마) 시각 절단 대상이라 트림도 생략(절단 시 부동 방지).
        ///   밴드는 바닥 높이라 N측 문 통과 위험을 피해 E/W만. 캡은 문 상단(2.8m) 위라 N 포함 안전.
        /// </summary>
        private static void AddRoomTrim(GameObject room, string buildingType, float width, float depth,
            float wallHeight, float wallThickness = 0.1f)
        {
            var bandMat = InteriorQualityBaseline.GetTrimMat($"band_{buildingType}", new Color(0.22f, 0.16f, 0.11f));
            var capSolid = InteriorQualityBaseline.GetTrimMat($"capSolid_{buildingType}", new Color(0.46f, 0.45f, 0.43f));
            float hw = width * 0.5f, hd = depth * 0.5f;
            float bandH = InteriorQualityBaseline.BaseboardHeightM;

            // 걸레받이 밴드 — E/W 내측 벽면
            InteriorQualityBaseline.AddBox(room.transform, $"Trim_{buildingType}_Band_E",
                new Vector3(hw - wallThickness * 0.5f - 0.02f, bandH * 0.5f, 0f),
                new Vector3(0.05f, bandH, depth - 0.3f), bandMat);
            InteriorQualityBaseline.AddBox(room.transform, $"Trim_{buildingType}_Band_W",
                new Vector3(-(hw - wallThickness * 0.5f - 0.02f), bandH * 0.5f, 0f),
                new Vector3(0.05f, bandH, depth - 0.3f), bandMat);

            // 벽톱 캡 — N/E/W 상단(문 상단 위 — 안전)
            InteriorQualityBaseline.AddBox(room.transform, $"Trim_{buildingType}_Cap_N",
                new Vector3(0f, wallHeight + 0.045f, -hd),
                new Vector3(width + 0.12f, 0.09f, wallThickness + 0.14f), capSolid);
            InteriorQualityBaseline.AddBox(room.transform, $"Trim_{buildingType}_Cap_E",
                new Vector3(hw, wallHeight + 0.045f, 0f),
                new Vector3(wallThickness + 0.14f, 0.09f, depth + 0.12f), capSolid);
            InteriorQualityBaseline.AddBox(room.transform, $"Trim_{buildingType}_Cap_W",
                new Vector3(-hw, wallHeight + 0.045f, 0f),
                new Vector3(wallThickness + 0.14f, 0.09f, depth + 0.12f), capSolid);
        }
    }
}
