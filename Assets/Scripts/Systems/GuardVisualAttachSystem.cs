using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 2026: 병사(Soldier) 방어구 비주얼 부착 시스템 (ADDITIVE — 플레이어 ArmorVisualAttachSystem과 무관).
    ///
    /// 병사(GuardPlaceholder)의 본 릭(27 bones, Blender식 `.L/.R` 접미 이름 — spine/spine.001..005,
    /// shoulder/upper_arm/forearm/hand/breast/pelvis/thigh/shin/foot/toe)에
    /// 병사가 착용 중인 장비(GuardPlaceholder의 HelmetItem/ArmorItem/BootsItem/GlovesItem/ShieldItem/
    /// WeaponItem)의 GLB 비주얼을 부착한다.
    ///
    /// - 슬롯 판정: 아이템 id 키워드(helmet/armor/boot|shoe/glove/shield) → VisualSlot.
    ///   무기는 WeaponItem 전용 경로([P30 후속②] AddWeaponToDesired — {tier}_{kind} GLB: steel_sword 등).
    /// - 부착 본 매핑: 크라운=spine.005, 가슴=spine.003, 척추=spine, 손/발=hand.L/.R·foot.L/.R,
    ///   방패=hand.L(폴백 forearm.L). `.L/.R` 접미 그대로 정확 매칭 → 대소문자 무시 → 부분일치 순.
    /// - GLB 없는 절차 장비(equip_armor_*)는 티어 기본 비주얼(steel/wood)로 폴백해 병사가 항상 방어구를 입게 한다.
    ///   (덩치 1.8x 병사는 모델 부모 스케일이 그대로 본에 전파 — 본 공간 스케일 1.0이면 자동 확대.)
    /// - 딩글톤: Instance + Ensure(parent). 임의 구독 없음 (폴링 동기화).
    /// </summary>
    public class GuardVisualAttachSystem : MonoBehaviour
    {
        const string LogTag = "[GuardVisual]";
        const string GlbResourceRoot = "Models/UserProvided/";
        const float SyncIntervalSec = 0.35f;

        /// <summary>병사 비주얼 슬롯 (아이템 id 키워드로 판정).</summary>
        public enum VisualSlot
        {
            None,       // 비주얼 없음 (재료/기타)
            Helmet,
            Armor,
            Shoes,
            Gloves,
            Back,
            Weapon,     // [P30 후속②] 무기 — WeaponItem 전용(GLB {tier}_{kind}: steel_sword 등)
        }

        /// <summary>부착 상태 — 비주얼 리스트 + 부착한 itemId.</summary>
        class SlotState
        {
            public string itemId;
            public readonly List<GameObject> visuals = new List<GameObject>();
        }

        // ===== 싱글톤 =====
        public static GuardVisualAttachSystem Instance { get; private set; }

        // ===== 상태 =====
        readonly Dictionary<int, Dictionary<VisualSlot, SlotState>> _perGuard =
            new Dictionary<int, Dictionary<VisualSlot, SlotState>>();
        float _syncTimer;

        // ===== 병사 본 맵 (슬롯별 후보 본 이름 — .L/.R 접미 그대로. 순서: 정확 매칭 사전순 우선) =====
        static readonly Dictionary<VisualSlot, string[]> SoldierBoneMap = new Dictionary<VisualSlot, string[]>
        {
            // 크라운(헬멧) → spine.005 (폴백 spine)
            { VisualSlot.Helmet,  new[] { "spine.005", "spine.003", "spine" } },
            // 가슴/등(갑옷) → spine.003 (폴백 spine)
            { VisualSlot.Armor,   new[] { "spine.003", "spine.005", "spine" } },
            // 양발
            { VisualSlot.Shoes,   new[] { "foot.L", "foot.R" } },
            // 양손
            { VisualSlot.Gloves,  new[] { "hand.L", "hand.R" } },
            // 방패 → 왼손 (폴백 왼팔)
            { VisualSlot.Back,    new[] { "hand.L", "forearm.L" } },
            // [P30 후속②] 무기 → 오른손 (폴백 오른팔/왼손). 활만 왼손 우선(AttachVisuals에서 재정렬)
            { VisualSlot.Weapon,  new[] { "hand.R", "forearm.R", "hand.L" } },
        };

        // [P30 후속②] 활(양손) 부착 본 — 왼손 우선(활쏘기 자세, 플레이어 WeaponEquipManager 규약 동일)
        static readonly string[] BowBoneCandidates = { "hand.L", "forearm.L", "hand.R" };

        // [P30 후속②] 무기 GLB 티어 폴백 순서 (id에서 티어 미확정/미존재 시 — Models/UserProvided/{tier}_{kind}.glb)
        // steel 우선: 레거시 병사 전리품(iron_sword 등)이 에픽(crystal)으로 과장되지 않게.
        static readonly string[] WeaponTierFallbacks = { "steel", "stone", "wood", "crystal" };

        // 티어 기본 폴백 (GLB 없는 절차 장비용)
        static string TierFallback(VisualSlot slot, int level)
            => slot == VisualSlot.Helmet ? (level >= 40 ? "steel_helmet" : (level >= 20 ? "wood_helmet" : null))
             : slot == VisualSlot.Armor  ? (level >= 40 ? "steel_armor"  : (level >= 20 ? "wood_armor"  : null))
             : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>부모 GO에 컴포넌트 추가 (이미 있으면 스킵). 부트/테스트 씬에서 1회 호출.</summary>
        public static void Ensure(GameObject parent)
        {
            if (parent == null || parent.GetComponent<GuardVisualAttachSystem>() != null) return;
            parent.AddComponent<GuardVisualAttachSystem>();
        }

        /// <summary>
        /// [P30 후속②] 즉시 동기화 트리거 — 장비 등록(EquipAllyItem) 등 슬롯 변경 직후 호출.
        /// 0.35s 폴링 대기 없이 해당 병사 rig을 즉시 재부착. 인스턴스 부재 시 자동 Ensure(셀프 힐링) 후 동기화.
        /// 멱등 — 중복 호출해도 upToDate 판정으로 재부착 스킵.
        /// </summary>
        public static void RequestRefreshFor(GuardPlaceholder guard)
        {
            if (guard == null) return;
            var inst = Instance;
            if (inst == null)
            {
                inst = Object.FindAnyObjectByType<GuardVisualAttachSystem>(FindObjectsInactive.Include);
                if (inst == null)
                {
                    var go = new GameObject("GuardVisualAttachSystem");
                    inst = go.AddComponent<GuardVisualAttachSystem>();
                    Debug.Log($"{LogTag} 인스턴스 부재 — 자동 생성(Ensure) 후 즉시 동기화");
                }
            }
            try
            {
                inst.SyncGuard(guard);
                inst._syncTimer = 0f;   // 다음 폴링 주기 정상화
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LogTag} RequestRefreshFor 예외: {e.Message}");
            }
        }

        // ===== 폴링 동기화 (0.35s 스로틀) =====
        private void Update()
        {
            _syncTimer += Time.deltaTime;
            if (_syncTimer < SyncIntervalSec) return;
            _syncTimer = 0f;

            var guards = Object.FindObjectsByType<GuardPlaceholder>(FindObjectsInactive.Exclude);
            foreach (var g in guards)
            {
                if (g == null) continue;
                try { SyncGuard(g); }
                catch (System.Exception e) { Debug.LogWarning($"{LogTag} SyncGuard 예외: {e.Message}"); }
            }
        }

        // ===== 슬롯 판정 =====
        public static VisualSlot VisualSlotFor(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return VisualSlot.None;
            string id = itemId.ToLowerInvariant();
            if (id.Contains("helmet")) return VisualSlot.Helmet;
            if (id.Contains("armor")) return VisualSlot.Armor;
            if (id.Contains("boot") || id.Contains("shoe")) return VisualSlot.Shoes;
            if (id.Contains("glove")) return VisualSlot.Gloves;
            if (id.Contains("shield")) return VisualSlot.Back;
            return VisualSlot.None;
        }

        // ===== 대상 동기화 =====
        void SyncGuard(GuardPlaceholder guard)
        {
            int key = guard.GetHashCode();
            var body = ResolveBody(guard);
            if (body == null) return;

            // [후속22] GuardPlaceholder 장비 필드(실착 플레이어 방어구)를 시각 슬롯 맵으로.
            // 전리품(DropEquippedItems)도 동일 필드 소스이므로 "보이는 그대로 드랍"된다.
            // (기존 GuardEquipmentSystem.GetAllGuardEquipment 의존은 제거 — 무기/악기 비주얼은 별도 시스템)
            var desired = new Dictionary<VisualSlot, string>();
            AddFieldToDesired(guard, guard.HelmetItem, desired);
            AddFieldToDesired(guard, guard.ArmorItem, desired);
            AddFieldToDesired(guard, guard.BootsItem, desired);
            AddFieldToDesired(guard, guard.GlovesItem, desired);
            AddFieldToDesired(guard, guard.ShieldItem, desired);
            AddWeaponToDesired(guard.WeaponItem, desired);   // [P30 후속②] 무기 비주얼 — 폴링 누락분 보완

            if (!_perGuard.TryGetValue(key, out var slots))
                _perGuard[key] = slots = new Dictionary<VisualSlot, SlotState>();

            // ① 원하는 슬롯 부족/불일치 → 재부착
            foreach (var kv in desired)
            {
                bool upToDate = slots.TryGetValue(kv.Key, out var st)
                                && st.itemId == kv.Value && AllAlive(st.visuals);
                if (upToDate) continue;
                DestroySlot(slots, kv.Key);
                AttachVisuals(body, kv.Key, kv.Value, slots);
            }

            // ② 더 이상 원하지 않는 슬롯 → 파괴
            var stale = new List<VisualSlot>();
            foreach (var vs in slots.Keys)
                if (!desired.ContainsKey(vs)) stale.Add(vs);
            foreach (var vs in stale) DestroySlot(slots, vs);
        }

        /// <summary>[후속22] GuardPlaceholder 장비 필드 1개를 desired 시각 슬롯 맵에 반영 (null/무관 슬롯 무시).</summary>
        void AddFieldToDesired(GuardPlaceholder guard, PlayerInventory.ItemData field,
                               Dictionary<VisualSlot, string> desired)
        {
            if (field == null) return;
            string raw = field.id;
            if (string.IsNullOrEmpty(raw)) return;
            var vs = VisualSlotFor(raw);
            if (vs == VisualSlot.None) return;
            string resolved = ResolveVisualId(guard, vs, raw);
            if (resolved != null) desired[vs] = resolved;
        }

        /// <summary>[P30 후속②] WeaponItem 1개를 desired 시각 슬롯 맵에 반영 (id→GLB {tier}_{kind} 해석, 부재 시 슬롯 미부착).</summary>
        void AddWeaponToDesired(PlayerInventory.ItemData field,
                                Dictionary<VisualSlot, string> desired)
        {
            if (field == null) return;
            string raw = field.id;
            if (string.IsNullOrEmpty(raw)) return;
            string resolved = ResolveWeaponVisualId(raw);
            if (resolved != null) desired[VisualSlot.Weapon] = resolved;
        }

        /// <summary>
        /// [P30 후속②] 무기 id → GLB 비주얼 id 해석.
        /// 1) 원본 id 그대로, 2) {tier}_{kind} 후보(티어 추출 → crystal/steel/stone/wood 폴백).
        /// kind(sword/spear/bow/dagger) 미확정 또는 GLB 전무 → null (무기 비주얼 스킵, 데이터는 유지).
        /// </summary>
        static string ResolveWeaponVisualId(string rawId)
        {
            string kind = WeaponKind(rawId);
            if (kind == null) return null;
            if (LoadVisualPrefab(rawId) != null) return rawId;

            string tier = WeaponTier(rawId);
            if (tier != null && LoadVisualPrefab(tier + "_" + kind) != null) return tier + "_" + kind;
            foreach (var fb in WeaponTierFallbacks)
                if (fb != tier && LoadVisualPrefab(fb + "_" + kind) != null) return fb + "_" + kind;
            return null;
        }

        /// <summary>무기 id에서 종류 추출 (sword/spear/bow/dagger 키워드 — 서로 접두 충돌 없음). 미확정 null.</summary>
        static string WeaponKind(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string low = id.ToLowerInvariant();
            if (low.Contains("sword"))  return "sword";
            if (low.Contains("spear"))  return "spear";
            if (low.Contains("bow"))    return "bow";
            if (low.Contains("dagger")) return "dagger";
            return null;
        }

        /// <summary>무기 id에서 티어 추출 (마지막 '_' 토큰이 GLB 티어면 반환 — weapon_sword_steel/steel_sword 공용). 미확정 null.</summary>
        static string WeaponTier(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            int idx = id.LastIndexOf('_');
            if (idx < 0 || idx == id.Length - 1) return null;
            string tail = id.Substring(idx + 1).ToLowerInvariant();
            foreach (var t in WeaponTierFallbacks)
                if (tail == t) return t;
            return null;
        }

        static bool AllAlive(List<GameObject> list)
        {
            if (list == null || list.Count == 0) return false;
            foreach (var v in list) if (v == null) return false;
            return true;
        }

        void AttachVisuals(GameObject body, VisualSlot slot, string itemId,
                           Dictionary<VisualSlot, SlotState> slots)
        {
            if (!slots.TryGetValue(slot, out var st))
                slots[slot] = st = new SlotState();
            st.itemId = itemId;

            // [P30 후속②] 활(양손)은 왼손 우선 — 그 외 무기/방어구는 슬롯 기본 본 순서
            var boneNames = SoldierBoneMap[slot];
            if (slot == VisualSlot.Weapon && itemId.Contains("bow"))
                boneNames = BowBoneCandidates;

            foreach (var boneName in boneNames)
            {
                var bone = FindBoneOnTarget(body, new[] { boneName });
                if (bone == null) continue;

                // [후속22] Shoes/Gloves는 좌우 본(.L/.R)에 {id}_left / {id}_right GLB 부착 (부재 시 원본 {id} 폴백)
                string visualId = ResolveSideVisualId(slot, boneName, itemId);
                var prefab = LoadVisualPrefab(visualId);
                if (prefab == null) continue;

                var visual = Object.Instantiate(prefab, bone);
                visual.name = visualId;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = Vector3.one; // 본 공간 — 덩치 1.8x는 부모 스케일이 자동 전파
                // [201차] 손에 쥐는 슬롯(무기/방패)은 병사 신장 비율로 월드 크기 정규화 —
                //   GLB 자연 크기 편차·덩치 차이와 무관하게 "병사 크기에 맞춘" 부착(사용자 요구).
                //   신체 착용 슬롯(Helmet/Armor/Shoes/Gloves)은 부모 스케일 자동 전파 유지(본 핏 보존).
                if (slot == VisualSlot.Weapon || slot == VisualSlot.Back)
                    NormalizeHandItemScale(visual, body, visualId);
                st.visuals.Add(visual);

                // [무번호 26-ST3] 병사 스윙 휘적 리본 부착 — 무기 GLB 인스턴스 자식으로 팁 트레일(플레이어 WeaponSwingTrail과 별개 인스턴스)
                // 슬롯 판정(실측): 병사 무기는 VisualSlot.Weapon으로만 해석(ResolveWeaponVisualId → desired[VisualSlot.Weapon]) —
                // Shoes/Gloves(.L/.R 사이드 GLB) 등 비무기 슬롯에는 부착하지 않는다. try/catch는 GuardPlaceholder 선례 준용.
                if (slot == VisualSlot.Weapon)
                {
                    try { SoldierSwingTrail.AttachTo(visual); } catch (System.Exception) { }
                }
            }
        }

        // ────── [201차] 손에 쥐는 장비 신장 비율 정규화 ──────

        /// <summary>무기 kind별 목표 길이(병사 신장 배수) — 검 0.70·창 0.95·활 0.75·단검 0.40. 미확정(kind 없음) 0.70.</summary>
        internal static float WeaponLengthRatio(string visualId)
        {
            if (string.IsNullOrEmpty(visualId)) return 0.70f;
            string low = visualId.ToLowerInvariant();
            if (low.Contains("spear"))  return 0.95f;
            if (low.Contains("bow"))    return 0.75f;
            if (low.Contains("dagger")) return 0.40f;
            return 0.70f;   // sword 및 기타
        }

        /// <summary>방패(Back 슬롯 — hand.L 부착) 목표 최대폭(병사 신장 배수).</summary>
        internal const float ShieldWidthRatio = 0.40f;

        /// <summary>
        /// 병사 신장(body 렌더러 bounds 높이) 대비 목표 최대축으로 균일 스케일 보정 1회.
        /// GLB를 부착 직후 실측 bounds로 보정 — 본 공간 localScale=one 유지(부모 전파와 합산되지 않게 명시 배수로 재조정).
        /// bounds 무효(0)면 무동작 — 파손 방지.
        /// </summary>
        void NormalizeHandItemScale(GameObject visual, GameObject body, string visualId)
        {
            float guardH = MeasureGuardHeight(body);
            if (guardH <= 0.01f) return;

            var rends = visual.GetComponentsInChildren<Renderer>();
            float curMax = 0f;
            foreach (var r in rends)
            {
                if (r == null || r.bounds.size == Vector3.zero) continue;
                curMax = Mathf.Max(curMax, Mathf.Max(r.bounds.size.x, Mathf.Max(r.bounds.size.y, r.bounds.size.z)));
            }
            if (curMax <= 0.001f) return;

            float ratio = (visualId != null && visualId.Contains("shield")) ? ShieldWidthRatio : WeaponLengthRatio(visualId);
            float desired = guardH * ratio;
            visual.transform.localScale *= desired / curMax;
        }

        /// <summary>병사 신장 — 전 렌더러 world bounds 높이(캡슐/레챗 등 절차 폴백 포함).</summary>
        static float MeasureGuardHeight(GameObject body)
        {
            if (body == null) return 0f;
            var rends = body.GetComponentsInChildren<Renderer>();
            bool any = false;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any ? b.size.y : 0f;
        }

        /// <summary>
        /// [후속22] 슬롯/본 이름에 따라 부착할 GLB id를 결정.
        /// Shoes/Gloves는 좌우 본(foot.L/.R·hand.L/.R)에 대해 {id}_left / {id}_right GLB를 우선 사용.
        /// 좌우 전용 GLB가 없으면(또는 그 외 슬롯) 원본 {id} 그대로.
        /// </summary>
        static string ResolveSideVisualId(VisualSlot slot, string boneName, string itemId)
        {
            bool isSided = (slot == VisualSlot.Shoes || slot == VisualSlot.Gloves);
            if (!isSided || string.IsNullOrEmpty(itemId))
                return itemId;

            if (boneName.EndsWith(".R") || boneName.EndsWith(".L"))
            {
                string side = boneName.EndsWith(".L") ? "_left" : "_right";
                string sided = itemId + side;
                if (LoadVisualPrefab(sided) != null) return sided;
            }
            return itemId; // 좌우 전용 GLB 부재 → 원본 id 폴백
        }

        void DestroySlot(Dictionary<VisualSlot, SlotState> slots, VisualSlot slot)
        {
            if (!slots.TryGetValue(slot, out var st)) return;
            foreach (var v in st.visuals)
                if (v != null) Destroy(v);
            st.visuals.Clear();
            slots.Remove(slot);
        }

        // ===== GLB / 티어 폴백 =====
        static GameObject LoadVisualPrefab(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            return Resources.Load<GameObject>(GlbResourceRoot + itemId)
                   ?? Resources.Load<GameObject>(GlbResourceRoot + itemId + ".glb");
        }

        /// <summary>정확 GLB 있으면 그 id, 없으면 티어 기본 폴백 id 반환 (Helmet/Armor만).</summary>
        string ResolveVisualId(GuardPlaceholder guard, VisualSlot slot, string rawId)
        {
            if (LoadVisualPrefab(rawId) != null) return rawId;
            if (slot == VisualSlot.Helmet || slot == VisualSlot.Armor)
            {
                string fb = TierFallback(slot, guard.Level);
                if (fb != null && LoadVisualPrefab(fb) != null) return fb;
            }
            return null;
        }

        // ===== 본/바디 해석 =====
        /// <summary>병사 GO에서 릭(리그) 바디 GO를 찾는다. self/직접자식 'Body'|'GuardModel', 없으면 Animator.</summary>
        static GameObject ResolveBody(GuardPlaceholder guard)
        {
            var root = guard.gameObject;
            if (root == null) return null;

            string rn = root.name;
            if (rn.Contains("Body") || rn.Contains("GuardModel")) return root;

            foreach (Transform child in root.transform)
            {
                if (child == null) continue;
                string cn = child.name;
                if (cn.Contains("Body") || cn.Contains("GuardModel")) return child.gameObject;
            }

            var anim = root.GetComponentInChildren<Animator>();
            return anim != null ? anim.gameObject : null;
        }

        /// <summary>
        /// 바디 GO(descendants 포함)에서 후보 이름과 일치하는 본 Transform 탐색.
        /// 매칭 순서: 정확 일치 → 대소문자 무시 일치 → 부분 포함. (첫 매칭 반환)
        /// </summary>
        static Transform FindBoneOnTarget(GameObject body, string[] names)
        {
            if (body == null || names == null || names.Length == 0) return null;
            var ts = body.GetComponentsInChildren<Transform>(true);

            // 1) 정확 일치
            foreach (var t in ts)
                foreach (var n in names)
                    if (t.name == n) return t;

            // 2) 대소문자 무시 일치
            foreach (var t in ts)
                foreach (var n in names)
                    if (string.Equals(t.name, n, System.StringComparison.OrdinalIgnoreCase)) return t;

            // 3) 부분 포함
            foreach (var t in ts)
            {
                string low = t.name.ToLowerInvariant();
                foreach (var n in names)
                    if (low.Contains(n.ToLowerInvariant())) return t;
            }
            return null;
        }
    }
}
