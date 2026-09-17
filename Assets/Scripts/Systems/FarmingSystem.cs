using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// P4 농사 + 확장 — 약초(herb) 자원 연결 시스템.
    /// 농사 밭(FarmPlot) GameObject에 시각+Capsule 자리표시자를 만들고, 파종(Plant)→성장(Update 누적)→수확(Harvest)→약초(herb_yakcho) 인벤토리 적립.
    /// - 밭: "FarmPlot" 태그 + 이름에 "farm"|"경지" 포함. FarmingPlot(seedId, plantTime, grownDurationSec) 컴포넌트가 상태 보유.
    /// - 성장은 코루틴 없이 Update에서 Time.deltaTime을 누적(프레임 고정 없음, 인스턴스 파괴에도 리셋 가능).
    /// - 파종 시 PlayerInventory에서 씨앗(seed_herb) 1개 소모(RemoveItem), 수확 시 약초(herb_yakcho) 적립(AddItem).
    /// - ContextCommandRouter/HoverTargetClassifier가 호출 가능하도록 전부 public static. 인스턴스/널 가드는 내부 처리.
    /// </summary>
    public class FarmingSystem : MonoBehaviour
    {
        const string LogTag = "[FarmingSystem]";
        const string PlotTag = "FarmPlot";

        /// <summary>기본 성장 시간(초) — IsGrown 판정 및 Harvest 기준.</summary>
        public const float DefaultGrowDurationSec = 25f;
        /// <summary>파종에 소모되는 씨앗 id (PlayerInventory.Seed_Herb).</summary>
        public const string SeedItemId = "seed_herb";
        /// <summary>수확/채집으로 산출되는 약초 id (PlayerInventory.Herb_Yakcho).</summary>
        public const string HerbItemId = "herb_yakcho";

        /// <summary>싱글턴 — 비월드/파괴 대비.</summary>
        public static FarmingSystem Instance { get; private set; }

        // 등록된 모든 밭(FarmingPlot) — Update가 deltaTime 누적을 순회. 파괴된 밭은 정리.
        static readonly List<FarmingPlot> _plots = new List<FarmingPlot>();
        static int _plotCounter;

        /// <summary>
        /// 싱글턴 보장. parent가 있으면 그 자식으로 생성(중복 방지).
        /// ContextCursorSystem/EquipmentManager의 Ensure 패턴과 동일.
        /// </summary>
        public static FarmingSystem Ensure(GameObject parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<FarmingSystem>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("FarmingSystem");
            if (parent != null) go.transform.SetParent(parent.transform, false);
            return go.AddComponent<FarmingSystem>();
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

        /// <summary>등록된 모든 밭의 성장을 Time.deltaTime 만큼 누적 (코루틴 없음, 프레임 고정 없음).</summary>
        private void Update()
        {
            float dt = Time.deltaTime;
            // 역순 순회 — 파괴된 밭 정리 안전.
            for (int i = _plots.Count - 1; i >= 0; i--)
            {
                var plot = _plots[i];
                if (plot == null || plot.gameObject == null)
                {
                    _plots.RemoveAt(i);
                    continue;
                }
                // 파종된 씨앗이 있고 아직 다 자라지 않았을 때만 누적.
                if (string.IsNullOrEmpty(plot.seedId)) continue;
                if (plot.grownAccum >= plot.grownDurationSec) continue;
                plot.grownAccum += dt;
            }
        }

        // ===== Public Static API (라우터/분류기 호출용) =====

        /// <summary>
        /// 지정 위치에 밭을 생성. "FarmPlot" 태그 + "FarmPlot_N" 이름 + FarmingPlot(seedId/plantTime/grownDurationSec) 부착.
        /// Capsule 시각 자리표시자(물리 간섭 방지로 Collider 제거). 시스템 인스턴스가 없으면 자동 Ensure 후 등록.
        /// </summary>
        public static GameObject CreateFarmPlot(Vector3 pos)
        {
            var system = Ensure();
            if (system == null)
            {
                Debug.LogWarning($"{LogTag} 시스템 인스턴스가 없어 밭 생성 실패.");
                return null;
            }

            _plotCounter++;
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"FarmPlot_{_plotCounter}";
            go.tag = PlotTag;
            go.transform.position = pos != null ? pos : Vector3.zero;
            go.transform.localScale = new Vector3(1.2f, 0.15f, 1.2f); // 넓적한 밭 자리표시자

            // CreatePrimitive가 만드는 BoxCollider 제거 — 의도치 않은 물리 간섭 방지 (Capsule엔 collider 없을 수도 → 널 가드).
            var cld = go.GetComponent<Collider>();
            if (cld != null) DestroyImmediate(cld);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial.color = new Color(0.36f, 0.25f, 0.13f); // 흙색

            var plot = go.AddComponent<FarmingPlot>();
            plot.seedId = "";
            plot.plantTime = 0f;
            plot.grownDurationSec = DefaultGrowDurationSec;
            plot.grownAccum = 0f;
            _plots.Add(plot);

            Debug.Log($"{LogTag} ✅ 밭 생성: {go.name} @ {pos}");
            return go;
        }

        /// <summary>
        /// 밭에 씨앗을 파종. PlayerInventory에서 씨앗 1개 소모 후 plantTime=Time.time, 성장 누적 0으로 리셋.
        /// 성공 true. 밭 없음/씨앗 부족/이미 파종됨/인벤 미존재 시 false.
        /// </summary>
        public static bool Plant(GameObject plot, string seedItemId)
        {
            var comp = GetPlot(plot);
            if (comp == null)
            {
                Debug.LogWarning($"{LogTag} Plant 실패 — FarmingPlot 컴포넌트가 없는 밭입니다.");
                return false;
            }
            if (!string.IsNullOrEmpty(comp.seedId))
            {
                Debug.LogWarning($"{LogTag} Plant 실패 — 이미 {comp.seedId} 파종됨.");
                return false;
            }
            if (string.IsNullOrEmpty(seedItemId))
            {
                Debug.LogWarning($"{LogTag} Plant 실패 — 씨앗 id가 비어있습니다.");
                return false;
            }
            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.LogWarning($"{LogTag} Plant 실패 — PlayerInventory 미존재.");
                return false;
            }
            // 씨앗 소모 전 보유 확인 + 소모 (RemoveItem)
            if (inv.GetItemCount(seedItemId) <= 0)
            {
                Debug.Log($"{LogTag} Plant 실패 — 씨앗 {seedItemId} 보유량 0.");
                return false;
            }
            if (!inv.RemoveItem(seedItemId, 1))
            {
                Debug.LogWarning($"{LogTag} Plant 실패 — RemoveItem 실패(보유 부족).");
                return false;
            }

            comp.seedId = seedItemId;
            comp.plantTime = Time.time;
            comp.grownAccum = 0f;
            Debug.Log($"{LogTag} 🌱 {plot.name}에 {seedItemId} 파종 완료.");
            return true;
        }

        /// <summary>파종된 씨앗이 성장 시간(grownDurationSec)을 모두 채웠는지. 밭 없음/미파종 시 false.</summary>
        public static bool IsGrown(GameObject plot)
        {
            var comp = GetPlot(plot);
            if (comp == null) return false;
            if (string.IsNullOrEmpty(comp.seedId)) return false;
            return comp.grownAccum >= comp.grownDurationSec;
        }

        /// <summary>
        /// 자란 밭을 수확. PlayerInventory에 약초(Herb_Yakcho) count만큼 적립 후 밭을 땅(soil ready) 상태로 리셋.
        /// 반환: 수확한 개수(성공 &gt;=1). 밭 없음/미파종/미성장/인벤 미존재 시 0.
        /// </summary>
        public static int Harvest(GameObject plot)
        {
            var comp = GetPlot(plot);
            if (comp == null)
            {
                Debug.LogWarning($"{LogTag} Harvest 실패 — FarmingPlot 컴포넌트가 없는 밭입니다.");
                return 0;
            }
            if (string.IsNullOrEmpty(comp.seedId))
            {
                Debug.LogWarning($"{LogTag} Harvest 실패 — 파종된 것이 없습니다.");
                return 0;
            }
            if (!IsGrown(plot))
            {
                float remain = comp.grownDurationSec - comp.grownAccum;
                Debug.Log($"{LogTag} ⏳ {plot.name} 아직 성장 중 — 남은 시간 {remain:F1}s.");
                return 0;
            }
            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.LogWarning($"{LogTag} Harvest 실패 — PlayerInventory 미존재.");
                return 0;
            }

            int count = 1;
            inv.AddItem(PlayerInventory.Herb_Yakcho, count);

            // 수확 후 밭 리셋 (soil ready) — 재파종 가능.
            comp.seedId = "";
            comp.plantTime = 0f;
            comp.grownAccum = 0f;

            Debug.Log($"{LogTag} 🌾 {plot.name} 수확 완료 — 약초 x{count} 획득.");
            return count;
        }

        /// <summary>밭이 비어 파종 가능한 상태인지 (등록 밭 + 미파종). 편의 헬퍼.</summary>
        public static bool IsPlotReady(GameObject plot)
        {
            var comp = GetPlot(plot);
            return comp != null && string.IsNullOrEmpty(comp.seedId);
        }

        /// <summary>GameObject에서 FarmingPlot 컴포넌트 조회 (없으면 null). GetComponentInChildren(true) — 비활성 포함.</summary>
        public static FarmingPlot GetPlot(GameObject go)
        {
            if (go == null) return null;
            return go.GetComponentInChildren<FarmingPlot>(true);
        }
    }

    /// <summary>
    /// 밭(FarmPlot) 1개의 상태 컴포넌트. FarmingSystem이 Update에서 성장 누적.
    /// seedId: 파종된 씨앗 id (비면 soil ready) / plantTime: 파종 시각 / grownDurationSec: 성장 요구 시간 / grownAccum: 누적 성장 시간.
    /// </summary>
    public class FarmingPlot : MonoBehaviour
    {
        public string seedId = "";
        public float plantTime = 0f;
        public float grownDurationSec = FarmingSystem.DefaultGrowDurationSec;
        public float grownAccum = 0f;
    }
}