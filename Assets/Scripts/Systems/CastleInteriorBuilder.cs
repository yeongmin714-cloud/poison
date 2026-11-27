using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [2026-10-10 J2 — 영지 실내 통일] 영주 성(플레이어 비소유) 전용 잠금 콘텐츠.
    /// 구 48×36 영주 성 셸은 폐기 — 진입 분기가 PlayerCastleInteriorBuilder로 통일되어
    /// 모든 영지 실내가 플레이어 성 표준(배치/모습/품질 계층 H1~H5)을 따른다.
    /// 본 클래스는 통일 레이아웃의 로비 포탈에 잠긴 문 4개(도끼질 콘텐츠)를 재정착시킨다.
    /// 잠금 계약 불변: 집무실 VeryHard / 금고 Legendary / 무기고 Hard / 문서고 Easy.
    /// 배치: [2026-10-14 통합 — 레이아웃별 소비 정착] 고정 벽 이름 대신 방에 부착된
    ///       InteriorLayoutContext의 활성 레이아웃 정의에서 포탈을 의미(연결 존)로 해석한다:
    ///       집무실→Bedroom / 금고→Storage / 무기고→Barracks / 문서고→Craft.
    ///       여러 포탈이 같은 존으로 열리면 결정론 규칙(동쪽/북서쪽 등)으로 하나를 고른다.
    ///       컨텍스트가 없는 방은 기준형 KeepOctagon 폴백(구 호출자 안전망) — 팔각 기준형에서는
    ///       기존과 완전히 동일한 벽 선택·위치·이름·ID가 나온다. 남쪽 입구(Lobby_South)와
    ///       연금술실 포탈은 개방.
    /// </summary>
    public static class CastleInteriorBuilder
    {
        /// <summary>통일 레이아웃의 로비 포탈 4곳에 영주 성 잠긴 문을 부착한다.</summary>
        public static void AddLordLockedDoors(GameObject room)
        {
            if (room == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;
            Material woodMat = new Material(shader) { name = "LordLock_WoodMat", color = new Color(0.42f, 0.29f, 0.17f) };
            Material stoneMat = new Material(shader) { name = "LordLock_StoneMat", color = new Color(0.38f, 0.36f, 0.33f) };

            // [재정착 규약] 포탈(3.2×2.8 절단) 중심에 문 패널 — 개구부 전체 봉쇄(해금 전 통과 불가).
            float halfHeight = PlayerCastleInteriorBuilder.PortalHeight * 0.5f;   // 1.4
            float doorWidth = PlayerCastleInteriorBuilder.PortalWidth + 0.2f;     // 3.4 — 틈 봉쇄
            float doorHeight = PlayerCastleInteriorBuilder.PortalHeight;          // 2.8

            // [2026-10-14 통합] 활성 레이아웃 정의 해석 — 컨텍스트 부재 시 기준형 폴백.
            InteriorLayoutContext.TryGetLayout(room, out InteriorLayoutDefinition layout);
            if (layout == null) layout = InteriorLayoutDefinition.KeepOctagon;

            // 잠금 계약 — 연결 존(의미) 기준. 벽 이름은 정의별로 다를 수 있으므로 사용하지 않는다.
            (string connectedTo, string lockId, string label,
                LockpickingSystem.LockDifficulty difficulty, Material mat)[] lockContract =
            {
                ("Bedroom", "castle_lord_office", "🚪 영주 집무실 (잠김)",
                    LockpickingSystem.LockDifficulty.VeryHard, woodMat),
                ("Storage", "castle_vault", "💰 금고실 (전설 잠김)",
                    LockpickingSystem.LockDifficulty.Legendary, stoneMat),
                ("Barracks", "castle_armory", "⚔️ 무기고 (잠김)",
                    LockpickingSystem.LockDifficulty.Hard, stoneMat),
                ("Craft", "castle_archive", "📜 문서고 (잠김)",
                    LockpickingSystem.LockDifficulty.Easy, woodMat),
            };

            PlayerCastleInteriorBuilder.WallLayout[] walls = layout.GetWalls();
            foreach (var entry in lockContract)
            {
                PlayerCastleInteriorBuilder.WallLayout portal = SelectPortal(walls, entry.connectedTo);
                if (string.IsNullOrEmpty(portal.Name))
                {
                    Debug.LogWarning($"[CastleInteriorBuilder] {layout.LayoutId}에서 {entry.connectedTo}향 " +
                        "로비 포탈을 찾지 못해 잠긴 문 1개 생략.");
                    continue;
                }

                // 포탈 개구부 중심 — 스팬 중앙 + DoorwayOffset(벽 방향 단위벡터) 반영.
                Vector2 delta = portal.End - portal.Start;
                Vector2 center2D = (portal.Start + portal.End) * 0.5f +
                    (delta.sqrMagnitude > 0.0001f ? delta.normalized * portal.DoorwayOffset : Vector2.zero);
                Vector3 center = new Vector3(center2D.x, halfHeight, center2D.y);
                float rotationY = Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg;
                CreateDoor(room, portal.Name + "_LordLock", entry.label, entry.lockId,
                    entry.difficulty, center, rotationY, doorWidth, doorHeight, entry.mat);
            }
            Debug.Log($"[CastleInteriorBuilder] 영주 성 잠긴 문 4개 재정착 완료 — 활성 레이아웃 {layout.LayoutId} 로비 포탈.");
        }

        /// <summary>
        /// 로비에서 <paramref name="connectedTo"/> 존으로 열린 실제 포탈 개구부를 고른다.
        /// 후보가 여러 개면 기능별 결정론 규칙을 적용한다(정의 선언 순서 → 안정적 결과):
        /// Bedroom=가장 북쪽, Storage=가장 동쪽, Barracks=가장 남서쪽, Craft=가장 북서쪽.
        /// 팔각 기준형에서는 기존 고정 선택(Lobby_North/East/SouthWest/NorthWest)과 동일하다.
        /// </summary>
        private static PlayerCastleInteriorBuilder.WallLayout SelectPortal(
            PlayerCastleInteriorBuilder.WallLayout[] walls, string connectedTo)
        {
            PlayerCastleInteriorBuilder.WallLayout best = default;
            float bestScore = float.NegativeInfinity;
            foreach (PlayerCastleInteriorBuilder.WallLayout wall in walls)
            {
                if (wall.DoorwayWidth <= 0f || wall.ConnectedFrom != "Lobby" ||
                    wall.ConnectedTo != connectedTo)
                    continue;

                Vector2 midpoint = (wall.Start + wall.End) * 0.5f;
                float score;
                switch (connectedTo)
                {
                    case "Bedroom":  score = midpoint.y;          break;  // 가장 북쪽
                    case "Storage":  score = midpoint.x;          break;  // 가장 동쪽
                    case "Barracks": score = -(midpoint.x + midpoint.y); break;  // 가장 남서쪽
                    case "Craft":    score = -(midpoint.x - midpoint.y); break;  // 가장 북서쪽
                    default:         score = 0f;                  break;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = wall;
                }
            }
            return best;
        }

        private static void CreateDoor(GameObject parent, string objectName, string label, string locationId,
            LockpickingSystem.LockDifficulty difficulty, Vector3 position, float rotationY,
            float width, float height, Material material)
        {
            GameObject door = new GameObject(objectName);
            door.transform.SetParent(parent.transform);
            door.transform.localPosition = position;
            door.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            door.transform.localScale = new Vector3(width, height, 0.3f);
            MeshFilter filter = door.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            MeshRenderer renderer = door.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            LockedDoor lockedDoor = door.AddComponent<LockedDoor>();
            lockedDoor.LocationId = locationId;
            lockedDoor.Difficulty = difficulty;
            // LockedDoor handles the lock interaction/state only; it has no collider or
            // physical open behavior. Block passage while locked, then clear it on unlock.
            // 로컬 (1,1,1) × 스케일 → 포탈 전체를 정확히 봉쇄(구판 1.2m 문의 측면 틈 결함 해소).
            BoxCollider doorCollider = door.AddComponent<BoxCollider>();
            doorCollider.size = new Vector3(1f, 1f, 1f);
            doorCollider.center = Vector3.zero;
            door.AddComponent<LockedDoorCollision>();
            NameplateDisplay nameplate = door.AddComponent<NameplateDisplay>();
            nameplate.DisplayName = label;
        }
    }

    /// <summary>Synchronizes a generated lock door's solid collider with its lock state.</summary>
    public sealed class LockedDoorCollision : MonoBehaviour
    {
        private LockedDoor _lockedDoor;
        private BoxCollider _collider;

        private void Awake()
        {
            _lockedDoor = GetComponent<LockedDoor>();
            _collider = GetComponent<BoxCollider>();
            UpdateCollider();
        }

        private void Update()
        {
            UpdateCollider();
        }

        private void UpdateCollider()
        {
            if (_lockedDoor != null && _collider != null)
                _collider.enabled = _lockedDoor.IsLocked;
        }
    }
}
