using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 2026-09-13(45차 P6): 방어구 슬롯 GLB 비주얼 부착 시스템.
    /// EquipmentManager.OnEquipmentChanged(slot, itemId) 구독 →
    /// Helmet/Armor/Shoes/Gloves/Back 슬롯의 장착 GLB를 플레이어(Tag "Player") 아바타 본에 부착/파괴한다.
    /// - Weapon 슬롯은 WeaponEquipManager 소유 → 본 시스템이 건드리지 않음(스킵).
    /// - GLB 로드: WeaponEquipManager.Equip과 동일 방식 — Resources.Load("Models/UserProvided/{itemId}")
    ///   (방어구 GLB id = 파일명 그대로: wood_helmet/wood_armor/wood_boot_left/wood_boot_right/
    ///   wood_glove_left/wood_glove_right/wood_shield. steel/stone/crystal 계열도 동일 규칙).
    /// - 슬롯 매핑: Helmet→Head 1개 / Armor→Spine(없으면 Chest) 1개 / Shoes→좌우 토큰으로
    ///   LeftFoot|RightFoot(토큰 없는 통짜 id는 양발) / Gloves→LeftHand|RightHand(양손 동일) /
    ///   Back(방패)→LeftHand(없으면 LeftForeArm) 1개.
    /// - 아바타 지연도착 대응: 부트 직후 GLB 아바타의 본이 늦게 도착하므로(Rebind 선례 —
    ///   TestPlayerAnimatorBoot), 본 미발견 시 최대 5초 폴링(0.5s 간격) 후 재시도, 실패 시 경고 1회로 스킵.
    ///   Player는 GameObject.FindWithTag("Player") 기반으로 매 시도 재조회(대상 교체 대비).
    /// - 로그 스팸 가드: 경고는 키별 1회만(정적 HashSet).
    /// </summary>
    public class ArmorVisualAttachSystem : MonoBehaviour
    {
        const string LogTag = "[ArmorVisual]";
        const string PlayerTag = "Player";
        const string GlbResourceRoot = "Models/UserProvided/";   // WeaponEquipManager와 동일 GLB 경로
        const float BonePollTimeoutSec = 5f;                      // 본 도착 폴링 최대 대기
        const float BonePollIntervalSec = 0.5f;                   // 폴링 간격
        const float SubscribeWaitTimeoutSec = 10f;                // EquipmentManager 부트 대기 상한

        // ── 슬롯별 부착 포즈 튜닝 테이블 (초기 대략값 — Play 판정 후 로그 보고 튜닝) ──
        //   LocalPos  : 본 기준 부착 로컬 좌표 / LocalEuler: 부착 회전(Euler) / Scale: 균등 스케일.
        //   Helmet (0,0.1,0) = 머리 본 원점에서 위로 살짝. Armor (0,0,0) = 척추 본 원점 그대로.
        struct AttachPose
        {
            public Vector3 LocalPos;
            public Vector3 LocalEuler;
            public float Scale;
        }

        static readonly Dictionary<EquipmentManager.EquipmentSlot, AttachPose> _poseTable =
            new Dictionary<EquipmentManager.EquipmentSlot, AttachPose>
        {
            { EquipmentManager.EquipmentSlot.Helmet, new AttachPose { LocalPos = new Vector3(0f, 0.1f, 0f), LocalEuler = Vector3.zero, Scale = 1.0f } },
            { EquipmentManager.EquipmentSlot.Armor,  new AttachPose { LocalPos = Vector3.zero,             LocalEuler = Vector3.zero, Scale = 1.0f } },
            { EquipmentManager.EquipmentSlot.Shoes,  new AttachPose { LocalPos = new Vector3(0f, 0.05f, 0f), LocalEuler = Vector3.zero, Scale = 1.0f } },
            { EquipmentManager.EquipmentSlot.Gloves, new AttachPose { LocalPos = Vector3.zero,             LocalEuler = Vector3.zero, Scale = 1.0f } },
            { EquipmentManager.EquipmentSlot.Back,   new AttachPose { LocalPos = new Vector3(0f, 0.02f, 0.06f), LocalEuler = Vector3.zero, Scale = 1.0f } }, // [TEST25-66차] 방패 — 손 앞쪽 살짝 이격(판이 손 원점에 겹쳐 가려지지 않게)
        };

        // 슬롯별 부착 비주얼 인스턴스 (Shoes/Gloves는 좌우 2개 → List)
        readonly Dictionary<EquipmentManager.EquipmentSlot, List<GameObject>> _slotVisuals =
            new Dictionary<EquipmentManager.EquipmentSlot, List<GameObject>>();

        // 로그 스팸 가드 (정적 캐시 — 인스턴스 재생성 사이에도 1회 유지)
        static readonly HashSet<string> _warnedKeys = new HashSet<string>();

        bool _subscribed;

        // ===== 초기화 =====

        private void Awake()
        {
            // 부모 쪽 EnsureArmorVisualAttachSystem()이 AddComponent만 하므로 여기서 초기화.
            // EquipmentManager가 이미 부트되어 있으면 즉시 구독+초기 동기화,
            // 아니면 부트 순서 대기 후 구독(코루틴 — Edit 모드 Awake에서는 미실행).
            if (EquipmentManager.Instance != null)
            {
                Subscribe();
                InitialSyncAll();
            }
            else if (Application.isPlaying)
            {
                StartCoroutine(SubscribeWhenReadyRoutine());
            }
        }

        /// <summary>EquipmentManager 부트 대기 후 구독 + 초기 동기화 (0.5s 간격 폴링).</summary>
        IEnumerator SubscribeWhenReadyRoutine()
        {
            for (float t = 0f; t <= SubscribeWaitTimeoutSec; t += BonePollIntervalSec)
            {
                if (EquipmentManager.Instance != null)
                {
                    Subscribe();
                    InitialSyncAll();
                    yield break;
                }
                yield return new WaitForSeconds(BonePollIntervalSec);
            }
            WarnOnce("no-em", $"{LogTag} EquipmentManager 미발견({SubscribeWaitTimeoutSec:0}초) — 구독/초기 동기화 스킵");
        }

        void Subscribe()
        {
            if (_subscribed || EquipmentManager.Instance == null) return;
            EquipmentManager.Instance.OnEquipmentChanged += HandleEquipmentChanged;
            _subscribed = true;
            Debug.Log($"{LogTag} EquipmentManager.OnEquipmentChanged 구독 완료");
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            if (EquipmentManager.Instance != null)
                EquipmentManager.Instance.OnEquipmentChanged -= HandleEquipmentChanged;
            _subscribed = false;
        }

        /// <summary>구독 직후 현재 장착 상태 전체 동기화(Weapon 슬롯 제외).</summary>
        void InitialSyncAll()
        {
            var em = EquipmentManager.Instance;
            if (em == null) return;
            foreach (EquipmentManager.EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)))
            {
                if (slot == EquipmentManager.EquipmentSlot.Weapon) continue;
                string itemId = em.GetItemId(slot);
                if (!string.IsNullOrEmpty(itemId))
                    HandleEquipmentChanged(slot, itemId);
            }
        }

        // ===== 이벤트 처리 =====

        void HandleEquipmentChanged(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            // Weapon 슬롯은 WeaponEquipManager 소유 — 스킵
            if (slot == EquipmentManager.EquipmentSlot.Weapon) return;

            // 2026-09-15(H-3): 이벤트 수신 실측 로그 — 부착 전 도달 여부/해제(Detach) 여부를
            // 홉 단위로 판별. 부착 성공만(✅)이 아니라 수신 자체를 로그로 남겨 dead-end을 구분한다.
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.Log($"{LogTag} OnEquipmentChanged 수신 ({slot}) — 해제(Detach)");
                DestroySlotVisuals(slot, log: true);
                return;
            }
            Debug.Log($"{LogTag} OnEquipmentChanged 수신 ({slot}) item={itemId} — 부착 시작");
            // [TEST25-66차] 동기 즉시 부착 1차 시도 — 본+GLB가 이 프레임에서 해결되면 코루틴 없이 즉시 완료.
            //   뿌리(실측): 부트 Awake 중 발화된 AttachRoutine 코루틴이 첫 yield 후 재개되지 않고 침묵 사망해
            //   "부착 시작" 로그만 남고 장비가 안 보였다(Editor.log: 성공/미발견/로드실패 로그 전부 0건).
            //   동기 경로는 코루틴 수명과 무관하게 같은 프레임에 부착+로그가 고정된다.
            if (TryAttachImmediate(slot, itemId)) return;
            StartCoroutine(AttachRoutine(slot, itemId));
        }

        /// <summary>
        /// [TEST25-66차] 동기 즉시 부착 — 본 해석+GLB 로드가 즉시 성공하면 코루틴 없이 그 자리에서 완료.
        /// 성공 true(코루틴 불필요) / 실패 false(폴링 코루틴 폴백). 예외는 로그 후 false(코루틴이 재시도·경고).
        /// </summary>
        bool TryAttachImmediate(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            try
            {
                // ① 기존 비주얼 파괴 (동일 슬롯 재부착/교체 공통)
                DestroySlotVisuals(slot, log: false);

                // ② 부착 본 즉시 해석
                var bones = ResolveBones(slot, itemId);
                if (bones == null) return false;

                // ③ GLB 즉시 로드 (확장자 없는 경로 우선, .glb 폴백)
                var prefab = Resources.Load<GameObject>(GlbResourceRoot + itemId);
                if (prefab == null)
                    prefab = Resources.Load<GameObject>(GlbResourceRoot + itemId + ".glb");
                if (prefab == null) return false;

                // ④ 즉시 부착
                InstantiateAttached(slot, itemId, prefab, bones);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogTag} 동기 즉시 부착 예외({slot}, {itemId}): {e.Message} — 폴링 코루틴으로 폴백");
                return false;
            }
        }

        /// <summary>[TEST25-66차] 본별 인스턴스 부착 + 성공 로그(bounds 실측 포함 — 침묵 경로 금지).</summary>
        void InstantiateAttached(EquipmentManager.EquipmentSlot slot, string itemId, GameObject prefab, Transform[] bones)
        {
            var pose = _poseTable.TryGetValue(slot, out var p) ? p : new AttachPose { LocalPos = Vector3.zero, LocalEuler = Vector3.zero, Scale = 1f };
            var boneNames = new List<string>();
            Renderer firstRend = null;
            var totalBounds = new Bounds();
            foreach (var bone in bones)
            {
                if (bone == null) continue;
                var visual = Instantiate(prefab, bone);
                visual.name = itemId;
                visual.transform.localPosition = pose.LocalPos;
                visual.transform.localRotation = Quaternion.Euler(pose.LocalEuler);
                visual.transform.localScale = Vector3.one * pose.Scale;
                AddVisual(slot, visual);
                boneNames.Add(bone.name);
                var rends = visual.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (!r.enabled) continue;
                    if (firstRend == null) { firstRend = r; totalBounds = r.bounds; }
                    else totalBounds.Encapsulate(r.bounds);
                }
            }

            if (boneNames.Count == 0)
            {
                Debug.LogWarning($"{LogTag} ⚠️ {slot} 비주얼 부착 실패: {itemId} — 유효 본 0개");
                return;
            }
            if (firstRend != null)
            {
                float centerDist = bones[0] != null ? Vector3.Distance(totalBounds.center, bones[0].position) : -1f;
                Debug.Log($"{LogTag} ✅ {slot} 비주얼 부착: {itemId} → {string.Join(", ", boneNames)} (bounds size={totalBounds.size:F2}, 본↔중심 {centerDist:F2}m)");
                if (centerDist > 1.0f)
                    Debug.LogWarning($"{LogTag} ⚠️ {slot} 비주얼 중심이 본에서 1m 이상 벗어남 — 포즈 튜닝 필요(slot={slot}, item={itemId})");
            }
            else
            {
                Debug.Log($"{LogTag} ✅ {slot} 비주얼 부착: {itemId} → {string.Join(", ", boneNames)} (활성 렌더러 없음 — 시각 확인 요망)");
            }
        }

        /// <summary>
        /// 슬롯 비주얼 부착 폴백: 기존 파괴 → 부착 본 폴링(최대 5초) → GLB 로드+부착.
        /// [TEST25-66차] 동기 즉시 부착(TryAttachImmediate) 실패분만 도달 — 본/GLB 지연 도착 대비 폴링.
        /// </summary>
        IEnumerator AttachRoutine(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            // ① 기존 비주얼 파괴 (동일 슬롯 재부착/교체 공통 — 즉시 부착 실패분이면 이미 비어 있어 멱등)
            DestroySlotVisuals(slot, log: false);

            // ② 부착 본 폴링 — Player 매 시도 재조회(대상 교체 대비)
            Transform[] bones = null;
            for (float t = 0f; t <= BonePollTimeoutSec; t += BonePollIntervalSec)
            {
                bones = ResolveBones(slot, itemId);
                if (bones != null) break;
                yield return new WaitForSeconds(BonePollIntervalSec);
            }
            if (bones == null)
            {
                WarnOnce($"bones:{slot}", $"{LogTag} {slot} 부착 본 미발견({BonePollTimeoutSec:0}초 폴링) — {itemId} 부착 스킵");
                yield break;
            }

            // ③ GLB 로드 (WeaponEquipManager와 동일 경로, id = 파일명)
            // [TEST23-FIX] 확장자 없는 경로 우선, 실패 시 .glb 폴백 (슬라임/병사 선례와 동일).
            var prefab = Resources.Load<GameObject>(GlbResourceRoot + itemId);
            if (prefab == null)
                prefab = Resources.Load<GameObject>(GlbResourceRoot + itemId + ".glb");
            if (prefab == null)
            {
                WarnOnce($"load:{itemId}", $"{LogTag} GLB 로드 실패: {GlbResourceRoot}{itemId}(.glb 폴백 포함) — {slot} 부착 스킵");
                yield break;
            }

            // ④ 포즈 테이블 적용 + 본별 부착 (+ 성공 로그)
            InstantiateAttached(slot, itemId, prefab, bones);
        }

        // ===== 본 해석 =====

        /// <summary>
        /// 슬롯+id 토큰으로 부착 본 집합 해석. 미완성이면 null(폴링 재시도).
        /// id의 "left"/"right" 토큰으로 좌우 분기(Shoes/Gloves). 토큰 없는 통짜 id는 양쪽 모두.
        /// </summary>
        Transform[] ResolveBones(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            var playerGO = GameObject.FindWithTag(PlayerTag);
            if (playerGO == null) return null;

            var animator = playerGO.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isActiveAndEnabled) return null;

            string idLower = itemId != null ? itemId.ToLowerInvariant() : string.Empty;
            bool left = idLower.Contains("left");
            bool right = idLower.Contains("right");

            switch (slot)
            {
                case EquipmentManager.EquipmentSlot.Helmet:
                {
                    var head = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (head == null) head = FindBoneByName(animator, new[] { "head" });
                    return Single(head);
                }

                case EquipmentManager.EquipmentSlot.Armor:
                {
                    var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                    if (spine == null) spine = FindBoneByName(animator, new[] { "spine" });
                    if (spine != null) return Single(spine);
                    var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                    if (chest == null) chest = FindBoneByName(animator, new[] { "chest" });
                    return Single(chest);
                }

                case EquipmentManager.EquipmentSlot.Shoes:
                {
                    var lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    if (lf == null) lf = FindBoneByName(animator, new[] { "leftfoot", "left_foot", "foot_l" });
                    var rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    if (rf == null) rf = FindBoneByName(animator, new[] { "rightfoot", "right_foot", "foot_r" });
                    if (left) return Single(lf);
                    if (right) return Single(rf);
                    return Pair(lf, rf);
                }

                case EquipmentManager.EquipmentSlot.Gloves:
                {
                    var lh = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    if (lh == null) lh = FindBoneByName(animator, new[] { "lefthand", "left_hand", "hand_l" });
                    var rh = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (rh == null) rh = FindBoneByName(animator, new[] { "righthand", "right_hand", "hand_r" });
                    if (left) return Single(lh);
                    if (right) return Single(rh);
                    return Pair(lh, rh);
                }

                case EquipmentManager.EquipmentSlot.Back: // 방패류 — 왼손 우선, 없으면 왼팔뚝
                {
                    var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    if (hand == null) hand = FindBoneByName(animator, new[] { "lefthand", "left_hand", "hand_l" });
                    if (hand != null) return Single(hand);
                    var foreArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    if (foreArm == null) foreArm = FindBoneByName(animator, new[] { "leftlowerarm", "left_lower_arm", "lowerarm_l", "forearm_l" });
                    return Single(foreArm);
                }

                default:
                    return null;
            }
        }

        static Transform[] Single(Transform bone) => bone != null ? new[] { bone } : null;

        static Transform[] Pair(Transform a, Transform b)
        {
            if (a == null || b == null) return null; // 쌍 본은 둘 다 도착해야 부착
            return new[] { a, b };
        }

        /// <summary>
        /// 2026-09-16(45차 P6): 이름 기반 본 재탐색 폴백 — WeaponEquipManager의 H-GRIP2 패턴.
        /// 플레이어 아바타가 Humanoid 매핑이 아닌 Generic/FBX라 GetBoneTransform이 null을 반환할 때,
        /// GetComponentsInChildren 순회 + 소문자 이름 부분일치 키워드로 본을 찾는다. 첫 매칭 반환.
        /// </summary>
        static Transform FindBoneByName(Animator animator, string[] keywords)
        {
            if (animator == null || keywords == null || keywords.Length == 0) return null;
            var ts = animator.GetComponentsInChildren<Transform>(true);
            foreach (var t in ts)
            {
                var name = t.name.ToLowerInvariant();
                for (int i = 0; i < keywords.Length; i++)
                {
                    if (name.Contains(keywords[i])) return t;
                }
            }
            return null;
        }

        // ===== 비주얼 관리 =====

        void AddVisual(EquipmentManager.EquipmentSlot slot, GameObject visual)
        {
            if (!_slotVisuals.TryGetValue(slot, out var list))
            {
                list = new List<GameObject>();
                _slotVisuals[slot] = list;
            }
            list.Add(visual);
        }

        /// <summary>슬롯의 기존 비주얼 전부 파괴(이미 파괴된 참조는 무시 — 멱등).</summary>
        void DestroySlotVisuals(EquipmentManager.EquipmentSlot slot, bool log)
        {
            if (!_slotVisuals.TryGetValue(slot, out var list)) return;
            bool destroyedAny = false;
            foreach (var v in list)
            {
                if (v == null) continue; // 아바타 교체 등으로 이미 파괴됨
                Destroy(v);
                destroyedAny = true;
            }
            list.Clear();
            if (log && destroyedAny)
                Debug.Log($"{LogTag} 🔓 {slot} 비주얼 해제");
        }

        // 경고 1회성 가드 — 동일 키 재경고 억제(스팸 방지)
        static void WarnOnce(string key, string message)
        {
            if (!_warnedKeys.Add(key)) return;
            Debug.LogWarning(message);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            // 잔존 비주얼 정리(부모 본과 함께 파괴되는 경우가 대부분이지만 멱등 처리)
            foreach (EquipmentManager.EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentManager.EquipmentSlot)))
                DestroySlotVisuals(slot, log: false);
        }
    }
}
