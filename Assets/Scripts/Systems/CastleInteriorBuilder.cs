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
    /// 배치: Lobby_North(침실방 향) / Lobby_East(창고 향) / Lobby_SouthWest(막사 향) /
    ///       Lobby_NorthWest(제작소 향). 남쪽 입구(Lobby_South)와 연금술실 포탈은 개방.
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

            foreach (PlayerCastleInteriorBuilder.WallLayout wall in PlayerCastleInteriorBuilder.GetTopologyWalls())
            {
                string lockId; string label; LockpickingSystem.LockDifficulty difficulty; Material mat;
                switch (wall.Name)
                {
                    case "Lobby_North":     // 침실방 향 포탈
                        lockId = "castle_lord_office"; label = "🚪 영주 집무실 (잠김)";
                        difficulty = LockpickingSystem.LockDifficulty.VeryHard; mat = woodMat;
                        break;
                    case "Lobby_East":      // 창고 향 포탈
                        lockId = "castle_vault"; label = "💰 금고실 (전설 잠김)";
                        difficulty = LockpickingSystem.LockDifficulty.Legendary; mat = stoneMat;
                        break;
                    case "Lobby_SouthWest": // 막사 향 포탈
                        lockId = "castle_armory"; label = "⚔️ 무기고 (잠김)";
                        difficulty = LockpickingSystem.LockDifficulty.Hard; mat = stoneMat;
                        break;
                    case "Lobby_NorthWest": // 제작소 향 포탈
                        lockId = "castle_archive"; label = "📜 문서고 (잠김)";
                        difficulty = LockpickingSystem.LockDifficulty.Easy; mat = woodMat;
                        break;
                    default:
                        continue;
                }
                // 포탈 중심(DoorwayOffset=0 → 스팬 중앙) + 벽 방향 정렬(캡/팀버와 동일 각도 규약).
                Vector2 delta = wall.End - wall.Start;
                Vector3 center = new Vector3(
                    (wall.Start.x + wall.End.x) * 0.5f, halfHeight,
                    (wall.Start.y + wall.End.y) * 0.5f);
                float rotationY = Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg;
                CreateDoor(room, wall.Name + "_LordLock", label, lockId, difficulty,
                    center, rotationY, doorWidth, doorHeight, mat);
            }
            Debug.Log("[CastleInteriorBuilder] 영주 성 잠긴 문 4개 재정착 완료 — 통일 레이아웃 로비 포탈.");
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
