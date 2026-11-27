using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [수조 통일] 비던전 실내 공용 디오라마(수조) 스타일 킷 — 9/20 실내씬 예시 기준(2026-11-06).
    ///
    /// 수조 구조 2요소:
    ///   ①천장 제거 — 이름 "Ceiling" 전수 DestroyImmediate(여관/성 선례 — 동프레임 미니맵 캡처 오염 방지)
    ///   ②카메라 측(남측, −z) 벽 "시각만" 절단 — renderer.enabled=false, 콜라이더/메시/GO 활성 유지.
    ///     근거: IndoorCameraFollow 피치65°·yaw 고정(북쪽 바라봄)이라 남측 벽만 시야 차단.
    ///     콜라이더 유지 계약: 플레이어 탈출 불가 + 미니맵 CaptureFor bounds 불변(렌더러+콜라이더 전수 AABB).
    ///     벽톱캡(room 레벨 형제)은 유지 → 탑다운 원칙 "벽윗면 평면캡" 설계도 룩 보존.
    ///
    /// 대상 훅(빌더 return 직전 1줄): 성내부/house/shop/church/crafthouse/restaurant/tavern/barn.
    /// 미적용(호출부 없음 — 제외 확정): 던전(CaveInteriorBuilder) · 이계(AfterlifeInteriorBuilder) ·
    ///   투기장 관중석(ArenaPitInteriorBuilder — 관전 ortho 특수) · 여관(ArenaInteriorBuilder — 이미 남측 입구+천장 파괴 정합).
    ///
    /// 멱등(재호출 안전) + 전체 try-catch(실패 시 경고 1행+무음 계속 — 크래시 금지, 146차 훅 계약 준용).
    /// </summary>
    public static class IndoorDioramaKit
    {
        private const float MinWallHeight = 2f;    // 벽 판정 최소 높이(가구/소품 제외)
        private const float SouthStripMeters = 3f; // 남측 절단 스트립(룸 남단 기준 북측 3m)

        /// <summary>룸 빌드 완료 직후 1줄 호출 — 천장 제거 + 남측 벽 시각 절단(콜라이더 유지).</summary>
        public static void Apply(GameObject room, bool cutSouthWalls = true)
        {
            if (room == null) return;
            try
            {
                RemoveCeilings(room);
                if (cutSouthWalls) CutCameraSideWalls(room);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[IndoorDioramaKit] 적용 실패(무음 계속) — {room.name}: {e.Message}");
            }
        }

        /// <summary>천장 제거 — 이름 Ceiling 전수(직계+손자 2층 스캔).</summary>
        private static void RemoveCeilings(GameObject room)
        {
            foreach (Transform child in room.transform)
            {
                if (child.name.Contains("Ceiling"))
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }
                for (int i = 0; i < child.childCount; i++)
                {
                    Transform grand = child.GetChild(i);
                    if (grand.name.Contains("Ceiling"))
                        Object.DestroyImmediate(grand.gameObject);
                }
            }
        }

        /// <summary>남측(카메라 측, −z) 벽 시각 절단 — renderer만 off, 콜라이더 유지(탈출 불가+미니맵 AABB 불변).</summary>
        private static void CutCameraSideWalls(GameObject room)
        {
            Physics.SyncTransforms();    // 동프레임 stale bounds 방지(EditMode 테스트 실측 — CollisionAndKitchen 선례)
            if (!TryBounds(room, out Bounds bounds)) return;
            float stripMaxZ = bounds.min.z + SouthStripMeters;

            foreach (Renderer rend in room.GetComponentsInChildren<Renderer>(true))
            {
                GameObject go = rend.gameObject;
                if (!rend.enabled) continue;                 // 멱등 — 이미 절단
                string n = go.name;
                // 벽 이름 매치 — CreateRoom 4면(Wall_*)·Barn(Wall)·성 토폴로지(Exterior_*/Lobby_*/Bedroom_*/
                //   *_Boundary/EntryHall_*/Entrance_*) 전 계약 커버(실측: 토폴로지 벽명에 "Wall" 없음)
                bool wallNamed = n.Contains("Wall") || n.Contains("Boundary") || n.Contains("Exterior") ||
                                 n.Contains("Lobby") || n.Contains("EntryHall") || n.Contains("Bedroom") ||
                                 n.Contains("Entrance");
                if (!wallNamed) continue;
                if (IsKeptName(n)) continue;                 // 문/관문 시각 유지
                Collider col = go.GetComponent<Collider>();
                if (col == null) col = go.GetComponentInChildren<Collider>();
                if (col == null) continue;                   // 콜라이더 없는 장식 제외
                if (col.bounds.size.y < MinWallHeight) continue;
                if (col.bounds.max.z > stripMaxZ) continue;  // 남측 스트립 밖 — 유지
                // 시각만 절단 — SetActive(false) 금지(콜라이더 유지 계약). 하위 렌더러(벽 밴드 등) 동반 off.
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;
            }
        }

        /// <summary>시각 유지 이름 게이트 — 진짜 문 오브젝트만(EntryHall/Entrance_*는 잘라야 할 벽 경계).
        ///   출구 인지성: 성=E프롬프트 트리거, 소형실=Door/DoorFrame 오브젝트 유지.</summary>
        private static bool IsKeptName(string n) =>
            n.Contains("Door") || n.Contains("Gate");

        private static bool TryBounds(GameObject room, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = room.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
