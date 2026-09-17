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
    /// 병사가 착용 중인 방어구 장비(GuardPlaceholder의 HelmetItem/ArmorItem/BootsItem/GlovesItem/ShieldItem)의
    /// GLB 비주얼을 부착한다.
    ///
    /// - 슬롯 판정: 아이템 id 키워드(helmet/armor/boot|shoe/glove/shield) → VisualSlot.
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
        };

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

            foreach (var boneName in SoldierBoneMap[slot])
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
                st.visuals.Add(visual);
            }
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
