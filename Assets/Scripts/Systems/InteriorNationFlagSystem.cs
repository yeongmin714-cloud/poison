using ProjectName.Core.Utils;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 실내 소속 깃발 시스템 — 성 내부(로비) 진입 시 이 성의 소속 국가(4방위국/제국)를
    /// 국가 깃발 GLB로 표시한다. 기준형(KeepOctagon)에서는 지휘책상(x=0, z=36.75) 오른쪽
    /// 옆 레거시 (9, 36.75)에 배치한다.
    ///
    /// [2026-10-14 통합 — 레이아웃별 소비 정착] 방에 부착된 InteriorLayoutContext의 활성
    /// 정의를 해석한다. 기준형 KeepOctagon은 레거시 (9, 0, 36.75)를 그대로 유지하고,
    /// 그 외 정의는 ResolveFlagLocalPosition이 정의 로비 내부(지휘책상 비침입 2m) 좌표를
    /// 결정론으로 정한다(책상 실측 + (9,0) → centroid → 4m 후보 → centroid 폴백, 무예외).
    ///
    /// - 국가 깃발 GLB는 Resources/Models/UserProvided/ 에 이미 존재
    ///   (east/west/south/north/kingdom_flag) → RuntimeModelLoader.TryGetModel로 1회 로드 후 캐시 재사용.
    /// - GLB 원본 크기가 들쭉날쭉하므로 렌더러 bounds 기반으로 전체 높이 3.5m로 정규화
    ///   (실내 천장 6m — 로비 가시성 + 여백 균형).
    /// - 정규화 후 월드 bounds를 다시 측정해 발치(bounds.min.y)를 룸 바닥(룸 로컬 y=0)에 스냅.
    /// - 어떤 경우에도 예외를 밖으로 던지지 않는다(실내 빌드 신뢰성 계약 — 깃발 실패가 방 생성을 중단시키지 않음).
    /// </summary>
    public static class InteriorNationFlagSystem
    {
        /// <summary>깃발 전체 목표 높이(m) — 천장 6m 기준 가시성/여백 균형.</summary>
        private const float TargetHeight = 3.5f;

        /// <summary>
        /// 기준형(KeepOctagon) 레거시 룸 로컬 배치 좌표 — 지휘책상(x=0, z=36.75) 오른쪽 옆.
        /// 기둥 x범위(±3.5~4.5)와 무충돌. 비기준형 정의는 ResolveFlagLocalPosition이 재해석.
        /// </summary>
        private static readonly Vector3 LegacyFlagLocalPosition = new Vector3(9f, 0f, 36.75f);

        /// <summary>지휘책상 오브젝트 이름 — 실측 트랜스폼 탐색과 정의 앵커 조회 공용.</summary>
        private const string CommandDeskObjectName = "CommandDesk";

        /// <summary>기준형 지휘책상 룸 로컬 XZ — 레거시 깃발 (9, 0, 36.75)의 기준점.</summary>
        private static readonly Vector2 LegacyCommandDeskPosition = new Vector2(0f, 36.75f);

        /// <summary>기준형 책상 옆 깃발 오프셋 — 레거시 (9, 0).</summary>
        private static readonly Vector2 DeskAdjacentOffset = new Vector2(9f, 0f);

        /// <summary>깃발-책상 비침입 반경(m) — 정의 로비 내부 정착 시 최소 이격.</summary>
        private const float MinDeskDistance = 2f;

        /// <summary>centroid 폴백 후보 오프셋 — 결정론 순서 +x, -x, +z, -z 각 4m.</summary>
        private static readonly Vector2[] CentroidCandidateOffsets =
        {
            new Vector2(4f, 0f), new Vector2(-4f, 0f), new Vector2(0f, 4f), new Vector2(0f, -4f)
        };

        /// <summary>
        /// yaw 0 = 절차형 폴백 깃발 천이 남쪽 입구(-z)를 향함. GLB는 제작 방향(natural) 유지.
        /// </summary>
        private const float FlagYawDegrees = 0f;

        /// <summary>깃발 오브젝트 이름 — 씬 조회/정리 기준.</summary>
        private const string FlagObjectName = "InteriorNationFlag";

        /// <summary>
        /// room(실내 방 루트)에 국가 소속 깃발을 배치한다. 실패해도 절대 예외를 던지지 않는다.
        /// </summary>
        /// <param name="room">PlayerCastleInteriorBuilder가 만든 방 루트(룸 로컬 y=0 = 바닥).</param>
        /// <param name="nationStyle">"Eastern"/"Western"/"Southern"/"Northern"/"Empire" (대소문자 무시).</param>
        public static void PlaceFlag(GameObject room, string nationStyle)
        {
            if (room == null)
            {
                Debug.LogWarning("[InteriorNationFlag] room이 null — 깃발 배치 생략.");
                return;
            }

            try
            {
                string key = ResolveFlagKey(nationStyle, out bool knownStyle);
                if (!knownStyle)
                {
                    Debug.LogWarning(
                        $"[InteriorNationFlag] 알 수 없는 국가 스타일: '{nationStyle}'. kingdom_flag 폴백 사용.");
                }

                GameObject flag = null;
                if (RuntimeModelLoader.TryGetModel(key, out GameObject prefab) && prefab != null)
                {
                    flag = UnityEngine.Object.Instantiate(prefab);
                    if (flag != null && !HasAnyRenderer(flag))
                    {
                        // 렌더러 없는 GLB는 배치해도 보이지 않음 → 절차형 폴백으로 전환.
                        UnityEngine.Object.DestroyImmediate(flag);
                        flag = null;
                    }
                }

                bool fromGlb = flag != null;
                if (flag == null)
                {
                    // 절차형 폴백 — GLB 부재/무렌더러여도 실내 빌드는 절대 중단되지 않는다.
                    flag = CreateProceduralFlag(nationStyle);
                }

                flag.name = FlagObjectName;
                flag.transform.SetParent(room.transform, false);
                flag.transform.localRotation = Quaternion.Euler(0f, FlagYawDegrees, 0f);
                Vector3 flagLocalPosition = ResolveFlagLocalPosition(room);
                flag.transform.localPosition = flagLocalPosition;

                if (fromGlb)
                {
                    NormalizeFlagHeight(flag);
                }

                float measuredHeight = SnapFeetToFloor(room, flag, flagLocalPosition);

                Debug.Log(
                    $"[InteriorNationFlag] 배치 — {nationStyle} → {key}, 높이 {measuredHeight:0.0}m " +
                    $"@ ({flagLocalPosition.x:0.##}, {flagLocalPosition.z:0.##})" +
                    (fromGlb ? "" : " (절차형 폴백)"));
            }
            catch (System.Exception ex)
            {
                // 깃발 1개 실패가 실내 빌드 전체를 깨면 안 된다 — 로그 1건 후 계속.
                Debug.LogWarning($"[InteriorNationFlag] 깃발 배치 실패(무시하고 실내 빌드 계속): {ex.Message}");
            }
        }

        /// <summary>
        /// [2026-10-14 통합 — 레이아웃별 소비 정착] 룸에 배치될 깃발의 방 로컬 좌표(순수 조회, 부작용 없음).
        /// - 룸 null/컨텍스트 부재/정의 해석 실패 → 기준형 KeepOctagon 레거시 (9, 0, 36.75).
        /// - KeepOctagon → 레거시 위치 그대로(구 호출자·테스트 계약 불변).
        /// - 그 외 정의 → 지휘책상 실측 위치 + (9, 0)이 Lobby이고 책상에서 2m 이상이면 채택;
        ///   아니면 로비 다각형 정점 평균 중심(centroid)과 그 +x/-x/+z/-z 4m 후보를 결정론 순서로 검사.
        ///   모든 후보가 실패하면 centroid로 폴백 — 절대 예외를 던지지 않는다.
        /// </summary>
        public static Vector3 ResolveFlagLocalPosition(GameObject room)
        {
            if (room == null) return LegacyFlagLocalPosition;
            if (!InteriorLayoutContext.TryGetLayout(room, out InteriorLayoutDefinition layout) || layout == null)
                return LegacyFlagLocalPosition;
            if (layout.LayoutId == InteriorLayoutDefinition.KeepOctagonLayoutId)
                return LegacyFlagLocalPosition;

            Vector2 desk = GetCommandDeskPositionForTest(room);
            Vector2 deskAdjacent = desk + DeskAdjacentOffset;
            if (IsFlagPlacementValid(layout, deskAdjacent, desk))
                return new Vector3(deskAdjacent.x, 0f, deskAdjacent.y);

            Vector2 centroid = ComputeLobbyCentroid(layout.GetLobbyVertices());
            if (IsFlagPlacementValid(layout, centroid, desk))
                return new Vector3(centroid.x, 0f, centroid.y);

            foreach (Vector2 offset in CentroidCandidateOffsets)
            {
                Vector2 candidate = centroid + offset;
                if (IsFlagPlacementValid(layout, candidate, desk))
                    return new Vector3(candidate.x, 0f, candidate.y);
            }
            return new Vector3(centroid.x, 0f, centroid.y); // 최후 폴백 — 결정론·무예외.
        }

        /// <summary>테스트 관찰용 별칭 — ResolveFlagLocalPosition과 동일한 해석을 반환한다.</summary>
        public static Vector3 GetFlagLocalPositionForTest(GameObject room) => ResolveFlagLocalPosition(room);

        /// <summary>
        /// 테스트 관찰용 — 깃발 배치 해석이 기준으로 삼는 지휘책상의 방 로컬 XZ(순수 조회).
        /// 우선순위: 룸의 실제 CommandDesk 트랜스폼(직접 자식) → 정의의 "CommandDesk" 앵커
        /// (미정의 시 (0,0) 기존 계약) → 컨텍스트 부재 시 레거시 (0, 36.75).
        /// </summary>
        public static Vector2 GetCommandDeskPositionForTest(GameObject room)
        {
            if (room == null) return LegacyCommandDeskPosition;
            Transform desk = room.transform.Find(CommandDeskObjectName);
            if (desk != null)
                return new Vector2(desk.localPosition.x, desk.localPosition.z);
            if (InteriorLayoutContext.TryGetLayout(room, out InteriorLayoutDefinition layout) && layout != null)
                return layout.GetAnchorPosition(CommandDeskObjectName);
            return LegacyCommandDeskPosition;
        }

        /// <summary>후보 유효성 — 정의 판정상 Lobby 내부 + 책상 비침입 반경 2m.</summary>
        private static bool IsFlagPlacementValid(InteriorLayoutDefinition layout, Vector2 point, Vector2 desk)
        {
            return layout.GetZoneAt(point) == "Lobby" && Vector2.Distance(point, desk) >= MinDeskDistance;
        }

        /// <summary>
        /// 로비 다각형 정점 평균 중심(centroid) — 정의 문서의 "정점 평균 중심" 산법과 동일.
        /// 정점이 비면 레거시 깃발 XZ로 방어 폴백(무예외 계약).
        /// </summary>
        private static Vector2 ComputeLobbyCentroid(Vector2[] lobbyVertices)
        {
            if (lobbyVertices == null || lobbyVertices.Length == 0)
                return new Vector2(LegacyFlagLocalPosition.x, LegacyFlagLocalPosition.z);
            Vector2 sum = Vector2.zero;
            foreach (Vector2 vertex in lobbyVertices)
                sum += vertex;
            return sum / lobbyVertices.Length;
        }

        /// <summary>
        /// 국가 스타일 → Resources 깃발 키 매핑. 알 수 없는 스타일은 kingdom_flag 폴백.
        /// </summary>
        private static string ResolveFlagKey(string nationStyle, out bool knownStyle)
        {
            knownStyle = true;
            switch ((nationStyle ?? string.Empty).ToLowerInvariant())
            {
                case "eastern": return "east_flag";
                case "western": return "west_flag";
                case "southern": return "south_flag";
                case "northern": return "north_flag";
                case "empire": return "kingdom_flag";
                default:
                    knownStyle = false;
                    return "kingdom_flag";
            }
        }

        /// <summary>
        /// GLB 높이 정규화 — 자식 렌더러 bounds를 캡슐화해 전체 높이를 TargetHeight(3.5m)로
        /// 균일 스케일 배수 조정(TerritoryBuilder.NormalizeCastleScale 선례 복제).
        /// </summary>
        private static void NormalizeFlagHeight(GameObject flag)
        {
            Bounds bounds;
            if (!TryEncapsulateRenderersBounds(flag, out bounds))
                return;

            float currentHeight = bounds.size.y;
            if (currentHeight <= 0.0001f)
                return; // 높이 0 모델 — 스케일 보정 불가, 원본 스케일로 배치.

            float factor = TargetHeight / currentHeight;
            flag.transform.localScale *= factor;
        }

        /// <summary>
        /// 발치 스냅 — 부모 연결/스케일 후 월드 bounds를 다시 측정해 bounds.min.y를
        /// 룸 바닥(룸 로컬 y=0 → 월드 TransformPoint y)과 일치시킨다. 실측 높이(m) 반환.
        /// </summary>
        private static float SnapFeetToFloor(GameObject room, GameObject flag, Vector3 flagLocalPosition)
        {
            Bounds bounds;
            if (!TryEncapsulateRenderersBounds(flag, out bounds))
                return 0f;

            Vector3 anchorWorld = room.transform.TransformPoint(flagLocalPosition);
            float deltaY = anchorWorld.y - bounds.min.y;
            flag.transform.position += new Vector3(0f, deltaY, 0f);
            return bounds.size.y;
        }

        /// <summary>
        /// 자식 렌더러 전체 bounds 캡슐화(TerritoryBuilder.GetCastleRadius 선례 패턴).
        /// 렌더러가 하나도 없으면 false.
        /// </summary>
        private static bool TryEncapsulateRenderersBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private static bool HasAnyRenderer(GameObject go)
        {
            return go.GetComponentInChildren<Renderer>(true) != null;
        }

        /// <summary>
        /// 절차형 폴백 깃발 — 기둥(Cylinder r=0.05, h=3.5) + 깃발 천(Cube 1.2×0.8×0.04, 국가색).
        /// 국가색은 TerritoryBuilder.GetCastleFallbackColor 팔레트 패턴 재사용, URP Lit로 라이팅 반영.
        /// </summary>
        private static GameObject CreateProceduralFlag(string nationStyle)
        {
            Color nationColor = GetNationFlagColor(nationStyle);

            GameObject flag = new GameObject(FlagObjectName);

            // 기둥 — Unity Cylinder 기본 반경 0.5/높이 2 → (0.1, 3.5, 0.1) = r 0.05m, h 3.5m.
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(flag.transform, false);
            pole.transform.localScale = new Vector3(0.1f, TargetHeight, 0.1f);
            pole.transform.localPosition = new Vector3(0f, TargetHeight * 0.5f, 0f);
            var poleRenderer = pole.GetComponent<MeshRenderer>();
            if (poleRenderer != null)
                poleRenderer.sharedMaterial = MaterialHelper.CreateLitMaterial(
                    new Color(0.28f, 0.22f, 0.18f), "InteriorFlagPole_Mat");

            // 깃발 천 — 기둥 상단에 매단 얇은 박스(양면에서 보임). 천 윗면은 기둥 꼭대기(3.5m) 바로 아래.
            GameObject cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.name = "Cloth";
            cloth.transform.SetParent(flag.transform, false);
            cloth.transform.localScale = new Vector3(1.2f, 0.8f, 0.04f);
            cloth.transform.localPosition = new Vector3(0.62f, TargetHeight - 0.45f, 0f);
            var clothRenderer = cloth.GetComponent<MeshRenderer>();
            if (clothRenderer != null)
                clothRenderer.sharedMaterial = MaterialHelper.CreateLitMaterial(
                    nationColor, "InteriorFlagCloth_Mat");

            return flag;
        }

        /// <summary>
        /// 국가별 폴백 색상 — TerritoryBuilder.GetCastleFallbackColor 팔레트 동일값.
        /// </summary>
        private static Color GetNationFlagColor(string nationStyle)
        {
            switch ((nationStyle ?? string.Empty).ToLowerInvariant())
            {
                case "eastern": return new Color(0.25f, 0.45f, 0.9f);
                case "western": return new Color(0.2f, 0.75f, 0.3f);
                case "southern": return new Color(0.85f, 0.2f, 0.15f);
                case "northern": return new Color(0.6f, 0.3f, 0.8f);
                default: return new Color(0.85f, 0.83f, 0.8f); // Empire / 기타
            }
        }
    }
}
