using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 무기 장착/해제 정적 유틸 (싱글턴 아님).
    /// 검/창/활 GLB(Assets/Resources/Models/UserProvided/{id}_sword|_spear|_bow.glb)를
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

        /// <summary>
        /// 현재 장착 타입 (비장착 = Fist).
        /// 모델 프리팹 로드 실패 시에도 세팅을 유지 — 모델 없이 클립(애니메이션) 모드만으로 타입 전환 가능.
        /// </summary>
        public static WeaponType CurrentType { get; private set; } = WeaponType.Fist;

        /// <summary>검이 장착 중인지 여부.</summary>
        public static bool IsEquipped => _current != null;

        /// <summary>검 장착 (하위 호환 오버로드 — WeaponType.Sword로 위임).</summary>
        public static void Equip(string id, Transform player)
        {
            Equip(id, player, WeaponType.Sword);
        }

        /// <summary>
        /// 무기 장착. 기존 무기가 있으면 먼저 제거 후 새 무기를 RightHand에 부착.
        /// id는 "steel" / "crystal" / "stone" / "wood" (타입별 GLB 접미사 _sword/_spear/_bow와 결합).
        /// 프리팹 로드 실패 시 경고 후 모델 부착만 스킵 — CurrentType/PlayerCombat 반영은 유지되어
        /// 클립(애니메이션) 모드만으로 타입 전환이 동작한다.
        /// </summary>
        public static void Equip(string id, Transform player, WeaponType type)
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

            // ① 기존 무기 제거 (중복 장착 방지)
            UnequipInternal(logOnDestroy: false);

            // ② 타입 반영 — 모델 유무와 무관하게 먼저 세팅 (로드 실패 시 클립 모드만 전환)
            CurrentType = type;
            SyncPlayerCombat(type);

            // ③ 플레이어 자식 중 Animator → RightHand 본 획득
            var animator = player.GetComponentInChildren<Animator>();
            var handBone = animator != null
                ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;
            if (handBone == null)
            {
                Debug.LogWarning("[WeaponEquipManager] 플레이어에서 Animator/RightHand 본 미발견 — 장착 스킵");
                return;
            }

            // ④ GLB 프리팹 로드 + 인스턴스화 (타입별 파일명 접미사 분기)
            string suffix = type == WeaponType.Bow ? "_bow" : type == WeaponType.Spear ? "_spear" : "_sword";
            var prefab = Resources.Load<GameObject>("Models/UserProvided/" + id + suffix);
            if (prefab == null)
            {
                Debug.LogWarning($"[WeaponEquipManager] 무기 프리팹 로드 실패: Models/UserProvided/{id}{suffix} — 모델 없이 클립 모드만 전환");
                return;
            }
            var sword = Object.Instantiate(prefab, handBone);
            sword.name = id + suffix;

            // ⑤ 부착 규격: 위치/회전 (손 아래로 검신이 나가도록 — 스크린샷 튜닝 전제)
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

            // ⑥ 상태 갱신 + 로그
            _current = sword;
            CurrentId = id;
            Debug.Log($"[WeaponEquipManager] ✅ 무기 장착: {id}{suffix} (type={type}) → RightHand({handBone.name})");
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
            CurrentType = WeaponType.Fist;
        }

        /// <summary>PlayerCombat에 해당 타입의 WeaponData 정적 인스턴스를 반영 (전투 스탯/클립 모드 동기화).</summary>
        static void SyncPlayerCombat(WeaponType type)
        {
            if (PlayerCombat.Instance == null) return;
            var data = type == WeaponType.Bow ? WeaponData.Bow
                     : type == WeaponType.Spear ? WeaponData.Spear
                     : WeaponData.Sword;
            PlayerCombat.Instance.SetWeapon(data);
        }
    }
}
