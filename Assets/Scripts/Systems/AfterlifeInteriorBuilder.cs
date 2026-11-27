using UnityEngine;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [A2] 이계(冥界) 아레나 빌더 — 사망자의 마지막 항전 공간.
    /// IndoorSceneTransition "afterlife" 케이스에서 호출 (Additive 씬 — 실내 흐름 재사용).
    /// [155차 P1] 공동묘지 리빌드 — 잿빛 흙 바닥(반경 24m) + 석상 링 8(Cem_Statue 사이클) + 오벨리스크 4
    ///   + 무덤 호 12(Gravestone 에피타프 주입) + 센터피스(분수/기념비) + 소품 산포 24
    ///   + 경계 실린더 벽(충돌 단일소스) + 진홍 룬 링 + 붉은 조명 3 + 중앙 청광 + 귀환 문 앵커(비활성 개시).
    /// 결정론 계약: Random 0건 — 좌표/스케일/회전 전부 하드박제 상수. 프리팹 부재 시 조용한 스킵(경고 1회 플래그),
    ///   기존 절차 요소(바닥/경계/룬/조명/문)는 유지(폴백 계약). 탑다운 원칙 유지(천장 없음 — 폐쇄 원형 경계).
    /// </summary>
    public static class AfterlifeInteriorBuilder
    {
        public const float ArenaRadius = 24f;

        /// <summary>[계약 유지] 레거시 상수 — [155차 P1] 프리미티브 기둥 폐기(석상 링 대체), 경계 실린더가 충돌 담당.</summary>
        public const int PillarCount = 14;

        private static Mesh _cylinderMesh;
        private static Mesh _cubeMesh;
        private static bool _warnedMissingAsset;   // 프리팹 부재 경고 1회 플래그(빌드 시작마다 리셋)
        private static int _placedCount;           // [155차 P1] 리빌드 배치 수(석상/무덤/소품 전부)

        /// <summary>
        /// 이계 아레나 생성. 반환 = Room GameObject ("AfterlifeRoom").
        /// </summary>
        public static GameObject BuildAfterlifeInterior()
        {
            var room = new GameObject("AfterlifeRoom");
            room.transform.position = new Vector3(5000f, 0f, 5000f); // 월드 격리 — 충돌/스폰 간섭 방지

            Material floorMat = CreateDarkMaterial(new Color(0.16f, 0.15f, 0.17f), 0.35f); // 잿빛 흙 바닥(155차 P1 리스킨 — 흙 마무리)
            Material pillarMat = CreateDarkMaterial(new Color(0.16f, 0.12f, 0.17f), 0.6f);
            Material runeMat = CreateDarkMaterial(new Color(0.45f, 0.06f, 0.08f), 0.3f); // 진홍 룬 테두리

            // ── 원형 바닥 (Cylinder disc) ──
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "AfterlifeFloor";
            floor.transform.SetParent(room.transform, false);
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(ArenaRadius * 2f, 0.1f, ArenaRadius * 2f);
            var floorCol = floor.GetComponent<Collider>();
            // Cylinder Collider는 윗면만 — 유지(바닥 충돌로 사용). 사이드는 낮아 무해.
            var floorRenderer = floor.GetComponent<MeshRenderer>();
            if (floorRenderer != null) floorRenderer.material = floorMat;

            // 둘레 충돌 벽 — 바닥판 위에 링(투명 아님: 어두운 벽) — 원형 경계 유지
            //   [155차 P1] 기둥 프리미티브 폐기 — 경계 실린더가 둘레 충돌 단일소스(석상 링은 장식 전용).
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wall.name = "AfterlifeBoundary";
            wall.transform.SetParent(room.transform, false);
            wall.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            wall.transform.localScale = new Vector3((ArenaRadius + 1.5f) * 2f, 4.5f, (ArenaRadius + 1.5f) * 2f);
            var wallRenderer = wall.GetComponent<MeshRenderer>();
            if (wallRenderer != null) wallRenderer.material = pillarMat;

            // ── 진홍 룬 링 (바닥 테두리 장식 — 낮은 토러스 대용: 얇은 실린더) ──
            var runeRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            runeRing.name = "AfterlifeRuneRing";
            runeRing.transform.SetParent(room.transform, false);
            runeRing.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            runeRing.transform.localScale = new Vector3((ArenaRadius - 1.2f) * 2f, 0.02f, (ArenaRadius - 1.2f) * 2f);
            var runeRenderer = runeRing.GetComponent<MeshRenderer>();
            if (runeRenderer != null) runeRenderer.material = runeMat;

            // ── 붉은 조명 3개 (삼각 배치) ──
            for (int i = 0; i < 3; i++)
            {
                float angle = (120f * i + 30f) * Mathf.Deg2Rad;
                var lightGo = new GameObject($"AfterlifeLight_{i}");
                lightGo.transform.SetParent(room.transform, false);
                lightGo.transform.localPosition = new Vector3(Mathf.Cos(angle) * ArenaRadius * 0.5f, 6f,
                    Mathf.Sin(angle) * ArenaRadius * 0.5f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.9f, 0.15f, 0.1f);
                light.intensity = 1.4f;
                light.range = ArenaRadius * 1.2f;
                light.shadows = LightShadows.None; // [탑다운 H-series] 섀도우 None 계약
            }

            // 중앙 약한 차가운 조명 (대비)
            var centerGo = new GameObject("AfterlifeCenterLight");
            centerGo.transform.SetParent(room.transform, false);
            centerGo.transform.localPosition = new Vector3(0f, 10f, 0f);
            var centerLight = centerGo.AddComponent<Light>();
            centerLight.type = LightType.Point;
            centerLight.color = new Color(0.5f, 0.5f, 0.7f);
            centerLight.intensity = 0.5f;
            centerLight.range = ArenaRadius;
            centerLight.shadows = LightShadows.None;

            // ── [155차 P1] 공동묘지 리빌드 — 석상 링/무덤 호/센터피스/소품 산포 (전부 결정론 하드박제) ──
            BuildStatueRing(room);
            BuildGraveArc(room);
            BuildCenterpieces(room);
            BuildScatteredProps(room);

            // ── 귀환 문 앵커 (타이머 완료 시 활성 — 시스템이 제어) ──
            var door = new GameObject("AfterlifeExitDoor");
            door.transform.SetParent(room.transform, false);
            door.transform.localPosition = new Vector3(0f, 0f, -ArenaRadius * 0.6f);
            var doorMarker = door.AddComponent<AfterlifeExitDoor>();
            doorMarker.DeactivateDoor(); // 개시 비활성 — 완주 시 ActivateDoor

            Debug.Log($"[AfterlifeInteriorBuilder] ⚰️ 공동묘지 리빌드 완료 — 배치 {_placedCount}개 (석상 링 8 + 오벨리스크 4 + 무덤 12 + 센터피스 2 + 소품 24, 귀환 문 비활성, 반경 {ArenaRadius}m)");

            return room;
        }

        private static Material CreateDarkMaterial(Color color, float smoothness)
        {
            var mat = MaterialHelper.CreateLitMaterial(color, "AfterlifeMat");
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        // ================================================================
        // [155차 P1] 공동묘지 배치 — AfterlifeCemeteryCatalog 키 + RuntimeModelLoader 3차 폴백(결정론 — Random 0건)
        // ================================================================

        /// <summary>배치각도(도) → XZ 로컬 좌표(결정론 산식).</summary>
        private static Vector3 CirclePos(float radius, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
        }

        /// <summary>
        /// [155차 P1] 카탈로그 프리팹 배치 — TryGetModel→Instantiate→StripColliders→AlignVisualBottomToFloor.
        /// 로드 실패 시 조용한 스킵(경고 로그 1회 플래그, 루트 폐기) — 기존 절차 요소는 그대로(폴백 계약).
        /// 스케일/회전은 전부 호출부 하드박제 상수. 반환 = 배치 루트(실패 시 null — 호출부 후속 부착 생략).
        /// </summary>
        private static GameObject PlaceProp(GameObject parent, string goName, string key, Vector3 localPos, float yawDeg, float uniformScale)
        {
            var root = new GameObject(goName);
            root.transform.SetParent(parent.transform, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            root.transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);

            if (!RuntimeModelLoader.TryGetModel(key, out var prefab) || prefab == null)
            {
                WarnMissingAssetOnce(key);
                Object.DestroyImmediate(root);
                return null;
            }

            var visual = Object.Instantiate(prefab, root.transform);
            visual.name = "Visual";
            StripColliders(visual);            // 장식 전용 — 충돌은 경계 실린더/바닥 단일소스 계약
            AlignVisualBottomToFloor(visual);  // 밑면 == 바닥(스포너 선례 — 부모 스케일 보정 포함)
            _placedCount++;
            return root;
        }

        /// <summary>[155차 P3 실측] 석상 개별 스케일 — 프리팹 원본 높이/2.4m 목표(155_cemetery_bounds.txt 실측치 주석).</summary>
        private static float StatueScale(string key)
        {
            switch (key)
            {
                case "Cem_Statue_01": return 0.28f;   // 8.64m
                case "Cem_Statue_02": return 0.31f;   // 7.80m
                case "Cem_Statue_03": return 0.39f;   // 6.16m
                case "Cem_Statue_04": return 0.23f;   // 10.30m
                case "Cem_Statue_05": return 0.28f;   // 8.64m
                case "Cem_Statue_06": return 0.50f;   // 4.73m Gargoyle
                default: return 0.28f;
            }
        }

        /// <summary>[155차 P1] 석상 링 — 반경 0.92R에 8기(45° 간격, 22.5° 오프셋) + 석상 사이 45° 오프셋 오벨리스크 4(90° 간격).</summary>
        private static void BuildStatueRing(GameObject room)
        {
            float ringR = ArenaRadius * 0.92f;

            // 석상 8 — Cem_Statue_01..06 결정론 사이클(07 Eagle은 bounds 피벗 파손 실측 — 제외), yaw=배치각도+180(중심 향함).
            // [155차 P3 실측] 프리팹 원본 높이 4.7~10.3m — 목표 2.4m 개별 스케일 테이블(155_cemetery_bounds.txt).
            for (int i = 0; i < 8; i++)
            {
                float angleDeg = 22.5f + 45f * i;
                string key = $"Cem_Statue_{(i % 6) + 1:00}";
                PlaceProp(room, $"AfterlifeStatue_{i:00}", key, CirclePos(ringR, angleDeg), angleDeg + 180f, StatueScale(key));
            }

            // 오벨리스크 4 — 석상 사이 45° 오프셋(45/135/225/315°), 90° 간격, 스케일 1.2.
            for (int i = 0; i < 4; i++)
            {
                float angleDeg = 45f + 90f * i;
                PlaceProp(room, $"AfterlifeObelisk_{i:00}", "Cem_Obelisk", CirclePos(ringR, angleDeg), angleDeg + 180f, 0.8f);   // 실측 4.34m → ~3.5m
            }
        }

        /// <summary>
        /// [155차 P1] 무덤 호 — 남서쪽(135°~225°) 반경 0.45R~0.7R 3열×4기=12기.
        /// Cem_Grave_01/02/03 사이클, yaw=중심 반대방향(배치각도)+하드박제 변화,
        /// 각기 Gravestone 부착 + Configure(한국어 에피타프 12종 하드박제 — 절제된 세계관 톤).
        /// </summary>
        private static void BuildGraveArc(GameObject room)
        {
            float[] rowRadii =
            {
                ArenaRadius * 0.45f,   // 내열 10.8m
                ArenaRadius * 0.575f,  // 중열 13.8m
                ArenaRadius * 0.7f,    // 외열 16.8m
            };
            float[][] rowAngles =
            {
                new[] { 150f, 172f, 194f, 216f },   // 내열
                new[] { 139f, 161f, 183f, 205f },   // 중열
                new[] { 148f, 170f, 192f, 214f },   // 외열
            };
            float[] yawJitter = { -7f, 5f, -3f, 9f, 6f, -5f, 11f, -9f, 4f, -6f, 8f, -4f }; // 하드박제 변화
            string[] names =
            {
                "낯선 이", "떠난 자", "잠든 자", "먼 곳의 이",
                "이름 없는 자", "오래된 나그네", "바람 둔 자", "침묵한 자",
                "지나간 자", "기다린 자", "돌아온 자", "머문 자",
            };
            string[] epitaphs =
            {
                "여기서 잠들다",
                "두 번째 삶을 산 자",
                "빛을 기다리다 끝내 못 만나다",
                "이름은 흐르고 흔적만 남다",
                "소리 없이 지나간 밤을 기억하라",
                "끝나지 않은 여정의 마지막 기록",
                "말없이 지켜보던 하늘 아래 잠들다",
                "돌아오지 못한 발걸음을 묻다",
                "바람에 실린 마지막 숨",
                "기다림은 돌보다 오래 남다",
                "심연 속에 새겨진 이름 하나",
                "문을 열고 먼저 떠난 이",
            };

            int gi = 0;
            for (int row = 0; row < rowAngles.Length; row++)
            {
                for (int c = 0; c < rowAngles[row].Length; c++)
                {
                    float angleDeg = rowAngles[row][c];
                    var grave = PlaceProp(room, $"AfterlifeGrave_{gi:00}", $"Cem_Grave_{(gi % 3) + 1:00}",
                        CirclePos(rowRadii[row], angleDeg), angleDeg + yawJitter[gi], 1.0f);
                    if (grave != null)
                    {
                        var gravestone = grave.AddComponent<Gravestone>();
                        gravestone.Configure(names[gi], "???", "???", epitaphs[gi]); // 생몰 미상 — 결정론 문자열
                    }
                    gi++;
                }
            }
        }

        /// <summary>[155차 P1] 센터피스 — 중앙 분수 1 + 귀환 문 안쪽 기념비 1.</summary>
        private static void BuildCenterpieces(GameObject room)
        {
            PlaceProp(room, "AfterlifeFountain", "Cem_Fountain", Vector3.zero, 0f, 1.0f);   // 실측 2.62m — 원본 유지
            PlaceProp(room, "AfterlifeMonument", "Cem_Monument", new Vector3(0f, 0f, -ArenaRadius * 0.45f), 0f, 0.75f);   // 실측 7.14m → ~5.4m
        }

        /// <summary>
        /// [155차 P1] 소품 산포 — 좌표/회전/스케일 전부 하드박제 상수(결정론 — Random 0건).
        /// 던전 팩 보조: SkullCandle(던전 키 그대로), Cage_1/Chain_1_1(던전 GLB 파일명 1:1 — "Cage" 단독 키는 실물 부재).
        /// </summary>
        private static void BuildScatteredProps(GameObject room)
        {
            // 관 2 — 서/동 대칭
            PlaceProp(room, "AfterlifeCoffin_W", "Cem_Coffin", new Vector3(-9f, 0f, 3f), 95f, 0.7f);   // 실측 3.0m → ~2.1m
            PlaceProp(room, "AfterlifeCoffin_E", "Cem_Coffin", new Vector3(9f, 0f, -3f), 85f, 0.7f);   // 실측 3.0m → ~2.1m

            // 촛불 4
            PlaceProp(room, "AfterlifeCandle_0", "Cem_Candle", new Vector3(-6f, 0f, -7f), 0f, 1.0f);
            PlaceProp(room, "AfterlifeCandle_1", "Cem_Candle", new Vector3(5.5f, 0f, 6.5f), 40f, 1.0f);
            PlaceProp(room, "AfterlifeCandle_2", "Cem_Candle", new Vector3(-4f, 0f, 8f), 335f, 1.0f);
            PlaceProp(room, "AfterlifeCandle_3", "Cem_Candle", new Vector3(7f, 0f, -6f), 0f, 1.0f);

            // 해골 촛불 2 (던전 팩)
            PlaceProp(room, "AfterlifeSkullCandle_0", "SkullCandle", new Vector3(-2.5f, 0f, -9.5f), 0f, 1.0f);
            PlaceProp(room, "AfterlifeSkullCandle_1", "SkullCandle", new Vector3(3.5f, 0f, 9f), 70f, 1.0f);

            // 뼈 6
            PlaceProp(room, "AfterlifeBone_0", "Cem_Bone", new Vector3(-8f, 0f, -5f), 15f, 0.15f);   // 실측 3.7m(피벗 하단) → ~0.6m 산해골
            PlaceProp(room, "AfterlifeBone_1", "Cem_Bone", new Vector3(-7.5f, 0f, 6f), 320f, 0.8f);
            PlaceProp(room, "AfterlifeBone_2", "Cem_Bone", new Vector3(6f, 0f, 7.5f), 65f, 0.8f);
            PlaceProp(room, "AfterlifeBone_3", "Cem_Bone", new Vector3(8.5f, 0f, 4f), 290f, 0.8f);
            PlaceProp(room, "AfterlifeBone_4", "Cem_Bone", new Vector3(0.5f, 0f, 12f), 30f, 0.8f);
            PlaceProp(room, "AfterlifeBone_5", "Cem_Bone", new Vector3(-12f, 0f, 0.5f), 345f, 0.8f);

            // 항아리 2
            PlaceProp(room, "AfterlifeUrn_0", "Cem_Urn", new Vector3(-13.5f, 0f, 2f), 0f, 0.9f);
            PlaceProp(room, "AfterlifeUrn_1", "Cem_Urn", new Vector3(12.5f, 0f, -2.5f), 90f, 0.9f);

            // 랜턴 2 — 각기 PointLight(따뜻한 잔광 — 섀도우 None 계약)
            var lanternW = PlaceProp(room, "AfterlifeLantern_0", "Cem_Lantern", new Vector3(-10f, 0f, -8f), 0f, 0.14f);   // 실측 14.18m → ~2.0m
            var lanternE = PlaceProp(room, "AfterlifeLantern_1", "Cem_Lantern", new Vector3(10f, 0f, 8f), 180f, 0.14f);   // 실측 14.18m → ~2.0m
            AttachLanternLight(lanternW);
            AttachLanternLight(lanternE);

            // 벤치 2 — 중앙(분수) 향함(석상 링 yaw 관례 동일: 배치각도+180, 하드박제)
            PlaceProp(room, "AfterlifeBench_0", "Cem_Bench", new Vector3(-5f, 0f, 11f), 294f, 1.0f);
            PlaceProp(room, "AfterlifeBench_1", "Cem_Bench", new Vector3(5f, 0f, 11f), 246f, 1.0f);

            // 십자가 2 — 북동(무덤 호 남서와 대각 대칭)
            PlaceProp(room, "AfterlifeCross_0", "Cem_Cross", new Vector3(-9f, 0f, 12f), 307f, 1.0f);
            PlaceProp(room, "AfterlifeCross_1", "Cem_Cross", new Vector3(9f, 0f, 12f), 233f, 1.0f);

            // 우리 1 + 사슬 1 (던전 팩 — 우리 곁에 사슬)
            PlaceProp(room, "AfterlifeCage", "Cage_1", new Vector3(3f, 0f, -11.5f), 20f, 1.0f);
            PlaceProp(room, "AfterlifeChain", "Chain_1_1", new Vector3(2.4f, 0f, -10.6f), 40f, 1.0f);
        }

        /// <summary>[155차 P1] 랜턴 잔광 — PointLight(0.55,0.35,0.15 / 강도 0.8 / 반경 7 / 섀도우 None — 탑다운 계약).</summary>
        private static void AttachLanternLight(GameObject lantern)
        {
            if (lantern == null) return;   // 로드 실패 스킵분 — 고아 조명 방지
            var lightGo = new GameObject("LanternLight");
            lightGo.transform.SetParent(lantern.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.35f, 0.15f);
            light.intensity = 0.8f;
            light.range = 7f;
            light.shadows = LightShadows.None;
        }

        /// <summary>프리팹 로드 실패 경고 — 빌드당 1회만(플래그 — 스팸 방지, 이후 동종 실패는 조용히 스킵).</summary>
        private static void WarnMissingAssetOnce(string key)
        {
            if (_warnedMissingAsset) return;
            _warnedMissingAsset = true;
            Debug.LogWarning($"[AfterlifeInteriorBuilder] 공동묘지 프리팹 로드 실패 — 조용한 스킵(폴백 계약): '{key}' (이후 동종 실패 로그 생략)");
        }

        /// <summary>프리팹 시각 인스턴스의 콜라이더 전부 제거 — 장식 전용(충돌은 경계 실린더/바닥 단일소스).</summary>
        private static void StripColliders(GameObject visual)
        {
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        /// <summary>
        /// 시각 모델(Visual) 렌더러 bounds 밑면을 바닥(y=0)에 정렬 — 부모 스케일 보정 포함
        /// (월드 보정량을 부모 로컬로 변환 — 배치 루트 스케일 0.8~1.2 대응, CaveInteriorBuilder 선례 개선형).
        /// </summary>
        private static void AlignVisualBottomToFloor(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y < 0.0001f) return;   // bounds 미계산(드물게) — 스킵
            var parent = visual.transform.parent;
            if (parent == null) return;
            Vector3 delta = new Vector3(0f, -bounds.min.y, 0f);   // 월드 보정량(밑면 → 바닥 y=0)
            visual.transform.localPosition += parent.worldToLocalMatrix.MultiplyVector(delta);
        }
    }
}
