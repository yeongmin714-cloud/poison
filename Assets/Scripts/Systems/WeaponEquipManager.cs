using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 무기 장착/해제 정적 유틸 (싱글턴 아님).
    /// 검 4종 GLB(Assets/Resources/Models/UserProvided/{steel,crystal,stone,wood}_sword.glb)를
    /// 플레이어 Animator의 RightHand 본에 부착/제거한다.
    /// - GameSetup.Start: 기본 장착(steel) — 기존 인라인 검 부착 블록에서 이관
    /// - InventoryWindow 무기 슬롯: 사용자 장착/해제
    /// 부착 규격(기존 GameSetup 튜닝 값 유지):
    ///   localPosition (0, 0.12, 0.02), localRotation Euler(0,0,90),
    ///   렌더러 bounds 최장축 0.9m 스케일 정규화.
    /// </summary>
    public static class WeaponEquipManager
    {
        // 현재 부착된 검 인스턴스 (null = 비장착)
        static GameObject _current;

        /// <summary>현재 장착 중인 검 id (steel/crystal/stone/wood). null = 비장착.</summary>
        public static string CurrentId { get; private set; }

        /// <summary>검이 장착 중인지 여부.</summary>
        public static bool IsEquipped => _current != null;

        /// <summary>
        /// 검 장착. 기존 검이 있으면 먼저 제거 후 새 검을 RightHand에 부착.
        /// id는 "steel" / "crystal" / "stone" / "wood" (GLB 파일명 접미사 _sword와 결합).
        /// </summary>
        public static void Equip(string id, Transform player)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning("[WeaponEquipManager] id가 비어 있음 — 장착 스킵");
                return;
            }
            if (player == null)
            {
                Debug.LogWarning("[WeaponEquipManager] player가 null — 장착 스킵");
                return;
            }

            // ① 기존 검 제거 (중복 장착 방지)
            UnequipInternal(logOnDestroy: false);

            // ② 플레이어 자식 중 Animator → RightHand 본 획득
            var animator = player.GetComponentInChildren<Animator>();
            var handBone = animator != null
                ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;
            if (handBone == null)
            {
                Debug.LogWarning("[WeaponEquipManager] 플레이어에서 Animator/RightHand 본 미발견 — 장착 스킵");
                return;
            }

            // ③ GLB 프리팹 로드 + 인스턴스화
            var prefab = Resources.Load<GameObject>("Models/UserProvided/" + id + "_sword");
            if (prefab == null)
            {
                Debug.LogWarning($"[WeaponEquipManager] 검 프리팹 로드 실패: Models/UserProvided/{id}_sword");
                return;
            }
            var sword = Object.Instantiate(prefab, handBone);
            sword.name = id + "_sword";

            // ④ 부착 규격: 위치/회전 (손 아래로 검신이 나가도록 — 스크린샷 튜닝 전제)
            sword.transform.localPosition = new Vector3(0f, 0.12f, 0.02f);
            sword.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // 스케일 정규화 — 렌더러 bounds 기준 총길이 ~0.9m
            var rends = sword.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float len = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (len > 0.01f) sword.transform.localScale *= 0.9f / len;
            }

            // ⑤ 상태 갱신 + 로그
            _current = sword;
            CurrentId = id;
            Debug.Log($"[WeaponEquipManager] ✅ 검 장착: {id} → RightHand({handBone.name})");
        }

        /// <summary>검 해제. 장착된 검이 있으면 파괴하고 상태를 초기화.</summary>
        public static void Unequip()
        {
            UnequipInternal(logOnDestroy: true);
        }

        /// <summary>내부 해제 경로 (Equip 재사용 시 로그 억제).</summary>
        static void UnequipInternal(bool logOnDestroy)
        {
            if (_current != null)
            {
                Object.Destroy(_current);
                if (logOnDestroy)
                    Debug.Log($"[WeaponEquipManager] 🔓 검 해제: {CurrentId}");
            }
            else if (logOnDestroy)
            {
                Debug.Log("[WeaponEquipManager] 해제할 장착 검 없음");
            }
            _current = null;
            CurrentId = null;
        }
    }
}
