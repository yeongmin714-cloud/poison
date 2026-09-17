using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// P4 채집 — 풀/초목(Grass/Herb) 노드에서 약초(herb_yakcho)를 채집하는 시스템.
    /// - 노드는 "Gather"|"Grass" 태그 또는 이름에 "grass"|"herb"|"풀" 포함 GameObject(라우터/분류기가 전달).
    /// - TryGather: 성공 시 PlayerInventory에 약초(Heb_Yakcho) 1개 적립(AddItem) 후 짧은 리스폰 타이머(기본 15s) 시작.
    ///   타이머 완료 전 다시 TryGather하면 false(아직 리스폰 중). 타이머 경과 시 다시 채집 가능.
    /// - 리스폰은 코루틴 없이 Update에서 Time.deltaTime 누적 — 인스턴스 파괴에도 안전, 프레임 고정 없음.
    /// - 라우터가 호출 가능하도록 public static (ContextCommandRouter/HoverTargetClassifier). 인스턴스/널 가드 내부 처리.
    /// </summary>
    public class GatheringSystem : MonoBehaviour
    {
        const string LogTag = "[GatheringSystem]";

        /// <summary>채집 후 노드가 다시 자라나는 시간(초).</summary>
        public const float GatherRespawnSec = 15f;
        /// <summary>채집으로 산출되는 약초 id (PlayerInventory.Herb_Yakcho).</summary>
        public const string HerbItemId = "herb_yakcho";

        /// <summary>싱글턴 — 비월드/파괴 대비.</summary>
        public static GatheringSystem Instance { get; private set; }

        // 노드 → 리스폰 완료 시각(Time.time 기준). Update가 만료 항목 정리.
        // "Gathered" 플래그 = 이 맵에 등록되어 있고 아직 만료 전인 상태.
        readonly Dictionary<GameObject, float> _respawnUntil = new Dictionary<GameObject, float>();

        /// <summary>
        /// 싱글턴 보장. parent가 있으면 그 자식으로 생성(중복 방지).
        /// ContextCursorSystem/EquipmentManager의 Ensure 패턴과 동일.
        /// </summary>
        public static GatheringSystem Ensure(GameObject parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<GatheringSystem>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("GatheringSystem");
            if (parent != null) go.transform.SetParent(parent.transform, false);
            return go.AddComponent<GatheringSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>리스폰 만료된 노드 정리 (파괴된 노드 포함).</summary>
        private void Update()
        {
            float now = Time.time;
            var expiredKeys = new List<GameObject>();
            foreach (var kv in _respawnUntil)
            {
                if (kv.Key == null || kv.Value <= now)
                    expiredKeys.Add(kv.Key);
            }
            foreach (var key in expiredKeys)
                _respawnUntil.Remove(key);
        }

        // ===== Public Static API (라우터/분류기 호출용) =====

        /// <summary>
        /// 노드에서 약초를 채집. 성공 시 PlayerInventory에 약초(Heb_Yakcho) 1개 적립 후 리스폰 타이머 시작, true 반환.
        /// 노드가 null/이미 리스폰 중이면 false. 인벤 미존재/가득 참이면 false.
        /// </summary>
        public static bool TryGather(GameObject node)
        {
            if (node == null) return false;

            var system = Ensure();
            if (system == null)
            {
                Debug.LogWarning($"{LogTag} 시스템 인스턴스가 없어 채집 실패.");
                return false;
            }

            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.LogWarning($"{LogTag} TryGather 실패 — PlayerInventory 미존재.");
                return false;
            }

            // 이미 채집 직후 리스폰 중이면 거부.
            var respawn = system._respawnUntil.TryGetValue(node, out var until);
            if (respawn && until > Time.time)
            {
                Debug.Log($"{LogTag} ⏳ {node.name} 리스폰 중 — 채집 불가.");
                return false;
            }

            if (!inv.AddItem(PlayerInventory.Herb_Yakcho, 1))
            {
                Debug.LogWarning($"{LogTag} TryGather 실패 — 인벤토리가 가득 참.");
                return false;
            }

            // Gathered 플래그 설정(리스폰 맵 등록) + 리스폰 타이머 시작.
            system._respawnUntil[node] = Time.time + GatherRespawnSec;

            Debug.Log($"{LogTag} 🌿 {node.name} 채집 성공 — 약초 x1 획득 (리스폰 {GatherRespawnSec:F0}s).");
            return true;
        }

        /// <summary>
        /// 노드가 현재 채집 가능한 상태인지 (null 아님 + 리스폰 중 아님). 라우터가 시각/아이콘 판정에 사용.
        /// </summary>
        public static bool CanGather(GameObject node)
        {
            if (node == null) return false;
            var system = Ensure();
            if (system == null) return false;
            var respawn = system._respawnUntil.TryGetValue(node, out var until);
            return !(respawn && until > Time.time);
        }

        /// <summary>노드에 리스폰 타이머가 걸려 있는지 ("Gathered" 상태인지).</summary>
        public static bool IsRespawning(GameObject node)
        {
            if (node == null || Instance == null) return false;
            var respawn = Instance._respawnUntil.TryGetValue(node, out var until);
            return respawn && until > Time.time;
        }
    }
}