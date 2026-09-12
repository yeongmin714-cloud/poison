using UnityEngine;
using System.Collections.Generic;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 무기 장착/해제 정적 유틸 (싱글턴 아님).
    /// 검/창/활 GLB(Assets/Resources/Models/UserProvided/{id}_sword|_spear|_bow.glb)를
    /// 플레이어 Animator의 RightHand 본에 부착/제거한다.
    /// - GameSetup.Start: 기본 장착(steel) — 기존 인라인 검 부착 블록에서 이관
    /// - InventoryWindow 무기 슬롯: 사용자 장착/해제
    /// 부착 규격(2026-09-12 그립 정밀화 — 테스트 영상 5: 전 무기 공용 고정 오프셋이
    /// 창/활/단도에서 길이·피벗·축 차이로 어긋나는 문제 수리):
    ///   - 타입별 그립 포즈 테이블(_gripTable): localPosition/localRotation/목표길이.
    ///     검은 기존 GameSetup 실측 튜닝값 유지, 창/활은 신규 초기값(로그 실측 후 조정).
    ///   - GLB bounds 자동 그립 정렬: 자식 렌더러 bounds 합산 → 최장축=그립축으로 간주,
    ///     bounds 최하단부(그립부)가 테이블 localPosition 앵커에 착지하도록
    ///     pivot-to-grip 오프셋을 localPosition에서 차감 (부착 후 1회, 프레임 지연 없음 —
    ///     renderer bounds는 Instantiate 즉시 유효).
    ///   - 스케일 보정: bounds 최장축이 목표 길이(검0.9/창1.8/활1.0/단도0.45m)의
    ///     0.4~2.2배 범위를 벗어날 때만 targetLength로 균등 스케일 보정.
    ///   - [2026-09-12 P2 공격 FX 개편] 장착 완료 후 WeaponSwingTrail(무기 트레일) 재부착 —
    ///     bounds 최장축 팁(=그립 반대편 끝, 41차 그립 정렬 로직 재사용)에 흰 궤적 TrailRenderer.
    /// </summary>
    public static class WeaponEquipManager
    {
        // ── 타입별 그립 포즈 튜닝 테이블 (2026-09-12: 전 무기 공용 고정 오프셋 → 타입별 분리) ──
        // 값은 초기값 — Play 후 "[Weapon] 그립 정렬" 로그의 offset/bounds 실측으로 미세 조정.
        //   LocalPos  : 그립부(무기 bounds 최하단부)가 착지할 손(RightHand) 본 기준 앵커.
        //               (0,0,0)이면 그립부가 정확히 손 원점에 정렬.
        //   LocalEuler: 손 본 기준 부착 회전(Euler).
        //   TargetLen : bounds 최장축 목표 길이(m) — 0.4~2.2배 범위 밖일 때만 균등 스케일 보정.
        // 검 실측 튜닝값 유지. 단도(dagger)는 WeaponType 항목이 없어 Sword 경로로 장착되며
        // id의 "dagger" 토큰으로 TargetLen만 0.45로 오버라이드(포즈는 검과 동일).
        struct GripPose
        {
            public Vector3 LocalPos;
            public Vector3 LocalEuler;
            public float TargetLen;
        }

        static readonly Dictionary<WeaponType, GripPose> _gripTable = new Dictionary<WeaponType, GripPose>
        {
            { WeaponType.Sword, new GripPose { LocalPos = new Vector3(0f, 0.12f, 0.02f), LocalEuler = new Vector3(0f, 0f, 90f),   TargetLen = 0.9f } }, // 검 — 기존 실측 튜닝값 유지
            { WeaponType.Spear, new GripPose { LocalPos = new Vector3(0f, 0.45f, 0.02f), LocalEuler = new Vector3(-90f, 0f, 0f),  TargetLen = 1.8f } }, // 창 — 자루 중심을 손에, 창두는 전방 상향
            { WeaponType.Bow,   new GripPose { LocalPos = new Vector3(0f, 0.05f, 0.06f), LocalEuler = new Vector3(0f, -90f, 0f),  TargetLen = 1.0f } }, // 활 — 몸통이 손 아래 수직
        };

        // 단도(dagger) 목표 길이 — Sword 포즈 공유, TargetLen만 오버라이드
        const float DaggerTargetLen = 0.45f;
        // 스케일 보정 허용 범위 — 목표 길이의 0.4~2.2배. 범위 내 GLB는 원본 스케일 존중(강제 정규화 폐지)
        const float GripScaleMinRatio = 0.4f;
        const float GripScaleMaxRatio = 2.2f;

        // ── 신규 full-id(weapon_{type}_{tier}) → Resources GLB명 결정 테이블 ──
        // GblItemIconRenderer._itemToModel과 동일 규칙(2026-09-11). dagger 등 suffix 분기가
        // 없는 무기와 기존 wood 3종의 ItemData.id 장착을 지원. 기존 짧은 id("steel" 등)는
        // 이 테이블에 없어 기존 suffix 분기(_sword/_spear/_bow)로 폴백 — 하위 호환 유지.
        static readonly Dictionary<string, string> _itemIdToGlb = new Dictionary<string, string>
        {
            { "weapon_sword_wood",     "wood_sword" },
            { "weapon_spear_wood",     "wood_spear" },
            { "weapon_bow_wood",       "wood_bow" },
            { "weapon_dagger_wood",    "wood_dagger" },
            { "weapon_sword_steel",    "steel_sword" },
            { "weapon_spear_steel",    "steel_spear" },
            { "weapon_bow_steel",      "steel_bow" },
            { "weapon_dagger_steel",   "steel_dagger" },
            { "weapon_sword_stone",    "stone_sword" },
            { "weapon_spear_stone",    "stone_spear" },
            { "weapon_bow_stone",      "stone_bow" },
            { "weapon_dagger_stone",   "stone_dagger" },
            { "weapon_sword_crystal",  "crystal_sword" },
            { "weapon_spear_crystal",  "crystal_spear" },
            { "weapon_bow_crystal",    "crystal_bow" },
            { "weapon_dagger_crystal", "crystal_dagger" },
        };

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
            // 등급 보정: id에 포함된 티어(steel/stone/crystal)로 무기 데미지 배율 적용 (wood/기타 = 1.0)
            SyncPlayerCombat(type, WeaponData.GetTierMultiplier(id), id);

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

            // ④ GLB 프리팹 로드 + 인스턴스화
            //    1) 신규 full-id(weapon_{type}_{tier}) → 매핑 테이블로 GLB명 결정 (dagger 등 신규 무기 지원)
            //    2) 기존 짧은 id(steel 등) → 기존 suffix 분기(_sword/_spear/_bow) 유지 (하위 호환)
            string glbKey;
            if (!_itemIdToGlb.TryGetValue(id, out glbKey))
            {
                string suffix = type == WeaponType.Bow ? "_bow" : type == WeaponType.Spear ? "_spear" : "_sword";
                glbKey = id + suffix;
            }
            var prefab = Resources.Load<GameObject>("Models/UserProvided/" + glbKey);
            if (prefab == null)
            {
                Debug.LogWarning($"[WeaponEquipManager] 무기 프리팹 로드 실패: Models/UserProvided/{glbKey} — 모델 없이 클립 모드만 전환");
                return;
            }
            var sword = Object.Instantiate(prefab, handBone);
            sword.name = glbKey;

            // ⑤ 그립 포즈: 타입별 테이블 적용 (검 = 기존 실측 튜닝값 유지)
            var pose = GetGripPose(type, id);
            sword.transform.localRotation = Quaternion.Euler(pose.LocalEuler);
            sword.transform.localPosition = pose.LocalPos;

            // ⑥ GLB bounds 기반 그립 자동 정렬 + 스케일 보정
            //    (부착 후 1회, 프레임 지연 없음 — renderer bounds는 Instantiate 즉시 유효)
            //    반환값 = 무기 bounds 최장축 팁(그립 반대편 끝) 월드 좌표 — 스윙 트레일 부착점.
            bool gripAligned = ApplyBoundsGripAlignment(sword, handBone, pose, out Vector3 tipWorld);

            // ⑦ 상태 갱신 + 로그
            _current = sword;
            CurrentId = id;
            Debug.Log($"[WeaponEquipManager] ✅ 무기 장착: {glbKey} (type={type}) → RightHand({handBone.name})");

            // ⑧ [P2 공격 FX 개편] 스윙 트레일 (재)부착 — 장착마다 갱신(GLB 교체 시 구 트레일 폐기 후 재생성).
            //    그립 정렬 실패(렌더러 0개 등) 시 팁 미산출 → 트레일 스킵(경고, 장착 자체는 계속).
            if (gripAligned)
                WeaponSwingTrail.Attach(sword, tipWorld);
            else
                Debug.LogWarning("[Weapon] 스윙 트레일 스킵: 그립 정렬 실패로 팁 미산출");
        }

        /// <summary>타입별 그립 포즈 조회. dagger full-id는 Sword 포즈에 TargetLen만 0.45로 오버라이드.</summary>
        static GripPose GetGripPose(WeaponType type, string id)
        {
            if (!_gripTable.TryGetValue(type, out var pose))
                pose = _gripTable[WeaponType.Sword]; // 미등록 타입(Fist 등) 폴백 — 검 포즈
            if (type == WeaponType.Sword && !string.IsNullOrEmpty(id) && id.Contains("dagger"))
                pose.TargetLen = DaggerTargetLen;
            return pose;
        }

        /// <summary>
        /// GLB bounds 기반 그립 자동 정렬 + 스케일 보정 (부착 직후 1회 — 프레임 지연 없음).
        /// 1) 인스턴스 자식 렌더러 bounds 합산 → 최장축 = 그립축으로 간주.
        /// 2) 최장축 길이가 TargetLen의 0.4~2.2배 범위를 벗어나면 targetLength로 균등 스케일 보정.
        /// 3) bounds 최하단부(그립부)가 테이블 localPosition 앵커에 착지하도록 pivot-to-grip
        ///    오프셋을 localPosition에서 차감 — GLB 피벗이 어디에 있든 동일 그립 지점
        ///    (앵커 (0,0,0)이면 그립부가 정확히 손 원점). 렌더러 0개/예외 시 테이블 포즈 유지.
        /// 반환: 정렬 성공 시 true + tipWorld(최장축 끝, 그립 반대편 팁의 월드 좌표) — 실패 시 false + Vector3.zero.
        /// </summary>
        static bool ApplyBoundsGripAlignment(GameObject weapon, Transform handBone, GripPose pose, out Vector3 tipWorld)
        {
            tipWorld = Vector3.zero;
            try
            {
                var rends = weapon.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0)
                {
                    Debug.LogWarning("[Weapon] 그립 정렬 스킵: 렌더러 없음 — 테이블 포즈 그대로 부착");
                    return false;
                }
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.sqrMagnitude < 1e-10f) return false;

                // 무기 최장축 = 그립축
                int axis = 0;
                float len = b.size.x;
                if (b.size.y > len) { axis = 1; len = b.size.y; }
                if (b.size.z > len) { axis = 2; len = b.size.z; }

                // 스케일 보정 — 목표 길이 대비 0.4~2.2배 범위 밖만 균등 보정
                float scaleFix = 1f;
                if (len > 0.0001f && (len < pose.TargetLen * GripScaleMinRatio || len > pose.TargetLen * GripScaleMaxRatio))
                {
                    scaleFix = pose.TargetLen / len;
                    weapon.transform.localScale *= scaleFix;
                }

                // 그립부(최하단부) = 최장축 양끝 면 중심 중 더 낮은 쪽.
                // 수평 무기(Y差 2cm 미만)는 손 원점에 가까운 쪽을 그립으로 판정.
                Vector3 eMin = FaceCenter(b, axis, b.min);
                Vector3 eMax = FaceCenter(b, axis, b.max);
                Vector3 grip = (eMax.y - eMin.y >= 0.02f) ? eMin
                             : (eMin.y - eMax.y >= 0.02f) ? eMax
                             : ((eMin - handBone.position).sqrMagnitude <= (eMax - handBone.position).sqrMagnitude ? eMin : eMax);

                // pivot-to-grip 오프셋(스케일 보정 배율 반영) → localPosition에서 차감
                Vector3 pivot = weapon.transform.position;
                Vector3 gripScaled = pivot + (grip - pivot) * scaleFix;
                Vector3 offset = handBone.InverseTransformPoint(gripScaled) - handBone.InverseTransformPoint(pivot);
                weapon.transform.localPosition -= offset;

                // 팁 월드 좌표 = bounds 중심 + (최장축 방향 단위벡터 × 최장축 절반 길이) — 그립 반대편 끝.
                // 스케일 보정 배율(scaleFix)과 그립 재앵커링 이동을 반영한 최종 월드 좌표로 환산.
                Vector3 axisDir = (b.center - grip).normalized;       // 그립 → 팁 쪽 최장축 단위벡터
                Vector3 tipPre = b.center + axisDir * (len * 0.5f);   // 스케일 보정 전 팁(최장축 끝면 중심)
                tipWorld = pivot + (tipPre - grip) * scaleFix;

                Debug.Log($"[Weapon] 그립 정렬: bone={handBone.name}, offset={offset:F3}, bounds={b.size:F2} (스케일=x{scaleFix:F2})");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Weapon] 그립 정렬 실패(테이블 포즈 유지): {e.Message}");
                return false;
            }
        }

        /// <summary>bounds의 지정축 끝면 중심점(나머지 축은 bounds 중심) — 그립부 후보 좌표.</summary>
        static Vector3 FaceCenter(Bounds b, int axis, Vector3 end)
        {
            Vector3 c = b.center;
            if (axis == 0) c.x = end.x;
            else if (axis == 1) c.y = end.y;
            else c.z = end.z;
            return c;
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

        /// <summary>PlayerCombat에 해당 타입의 WeaponData를 반영 (전투 스탯/클립 모드 동기화).
        /// tierMultiplier ≠ 1이면 damage 등급 보정 복제본을 전달 — 정적 스탯 인스턴스는 오염하지 않음.</summary>
        static void SyncPlayerCombat(WeaponType type, float tierMultiplier, string equipId)
        {
            if (PlayerCombat.Instance == null) return;
            var baseData = type == WeaponType.Bow ? WeaponData.Bow
                     : type == WeaponType.Spear ? WeaponData.Spear
                     : WeaponData.Sword;
            PlayerCombat.Instance.SetWeapon(tierMultiplier != 1f ? baseData.CreateTieredCopy(tierMultiplier) : baseData);

            // 2026-09-12(P5): 무기별 애니 판정 가시화 — 38차 [Equip] 로그 0건 규명용(로직 미변경).
            // 활=ArcheryShot(활발사) / 창=Attack(찌르기) / 그 외(검)=WeaponCombo(3연타)
            Debug.Log($"[Equip] 무기 애니 경로 결정: {type} → {(type == WeaponType.Bow ? "ArcheryShot(활발사)" : type == WeaponType.Spear ? "Attack(찌르기)" : "WeaponCombo(3연타)")} (equipId={equipId}, 배율={tierMultiplier})");
        }
    }
}
