using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// GLB 모델 아이템 아이콘 렌더러 (2026-09-10).
    ///
    /// Resources/Models/UserProvided/ 에 GLB가 존재하는 아이템(id 보유 PlayerInventory.ItemData)에 대해,
    /// GLB 프리팹을 원격 위치에 마운트 → 오프스크린 카메라가 RenderTexture에 자동 렌더 →
    /// 다음 프레임 ReadPixels로 Texture2D(128×128) 베이크 → item.id 기준 캐시.
    ///
    /// - ItemIconDatabase.GetOrCreateIcon() 경유로 사용 권장(절차 아이콘 폴백 자동 적용).
    /// - 베이크 큐는 Update에서 1개씩 순차 처리(동시 다수 카메라 생성으로 인한 퍼포먼스 급락 방지).
    /// - InventoryWindow의 시스템 프리뷰(_previewRT)와는 무관한 독립 인스턴스.
    /// - 임시 노드/카메라/RT는 베이크 직후 파괴, 예외 시에도 정리 후 폴백 진행(크래시 금지).
    /// </summary>
    public class GblItemIconRenderer : MonoBehaviour
    {
        private const int IconSize = 128;      // 아이콘 텍스처 크기
        private const int MaxCacheSize = 200;  // 캐시 상한 — 초과 시 새 베이크 중단(폴백 아이콘 유지)

        /// <summary>싱글턴. Awake에서 중복 제거 후 할당 (DontDestroyOnLoad 없음).</summary>
        public static GblItemIconRenderer Instance;

        // ── 캐시/큐 (static — 씬 전환 후에도 베이크된 아이콘 텍스처 유지) ──
        private static readonly Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();
        private static readonly List<BakeJob> _bakeQueue = new List<BakeJob>();
        private static readonly HashSet<string> _queuedIds = new HashSet<string>();

        /// <summary>GLB가 없다고 확인된 아이템 id (재검색 생략 — 매 프레임 Resources.Load 방지).</summary>
        private static readonly HashSet<string> _knownMissing = new HashSet<string>();

        /// <summary>베이크 큐 항목: (itemId, modelKey, 노드, 카메라, RT, 마운트 여부).</summary>
        private struct BakeJob
        {
            public string itemId;
            public string modelKey;
            public GameObject node;
            public Camera cam;
            public RenderTexture rt;
            public bool mounted;
        }

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// 아이템의 GLB 아이콘을 반환(캐시). 없으면 베이크 큐에 등록하고 null 반환
        /// (호출자가 OnGUI 등 매 프레임 호출하면 몇 프레임 후 아이콘이 자동 반영됨).
        /// GLB가 없으면 null → 호출측이 절차 아이콘 폴백.
        /// </summary>
        public static Texture2D GetOrCreateIcon(PlayerInventory.ItemData item)
        {
            try
            {
                if (item == null || string.IsNullOrEmpty(item.id))
                    return null;

                // 에디터 모드 가드 — Resources.Load는 Play 전용
                if (!Application.isPlaying)
                    return null;

                // 캐시 히트
                if (_iconCache.TryGetValue(item.id, out Texture2D cached))
                {
                    if (cached != null)
                        return cached;
                    _iconCache.Remove(item.id); // 파괴된 텍스처 — 재베이크
                }

                // 모델 없음이 확인된 id — 즉시 폴백
                if (_knownMissing.Contains(item.id))
                    return null;

                // 큐 대기 중이면 대기 (베이크 완료까지 null)
                if (_queuedIds.Contains(item.id))
                    return null;

                // 모델키 해석 실패 → 절차 폴백
                string modelKey = ResolveModelKey(item);
                if (string.IsNullOrEmpty(modelKey))
                {
                    _knownMissing.Add(item.id); // 음성 캐시 — 재검색 생략
                    return null;
                }

                // 캐시 상한 초과 — 새 베이크 중단(폴백 유지)
                if (_iconCache.Count >= MaxCacheSize)
                    return null;

                // 베이크 큐 등록
                if (EnsureInstance() == null)
                    return null;

                _bakeQueue.Add(new BakeJob
                {
                    itemId = item.id,
                    modelKey = modelKey,
                    mounted = false
                });
                _queuedIds.Add(item.id);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GblItemIconRenderer] GetOrCreateIcon 실패(폴백 진행): " + e.Message);
            }
            return null;
        }

        // ================================================================
        //  Unity 생명주기
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            // 대기 큐의 임시 노드/카메라/RT 정리 (베이크 완료분 캐시는 유지)
            CleanupQueue();

            if (Instance == this)
                Instance = null;
        }

        /// <summary>베이크 큐를 1개씩 순차 처리. 마운트 프레임 → 다음 프레임 베이크.</summary>
        private void Update()
        {
            if (_bakeQueue.Count == 0)
                return;

            BakeJob job = _bakeQueue[0];

            if (!job.mounted)
            {
                // 1단계: 노드/카메라/RT 마운트 (카메라가 targetTexture RT에 매 프레임 자동 렌더)
                if (TryMount(ref job))
                {
                    _bakeQueue[0] = job; // mounted=true 저장 — 다음 프레임에 베이크
                }
                else
                {
                    // 마운트 실패 — 큐에서 제거(호출측 폴백 아이콘 유지)
                    _bakeQueue.RemoveAt(0);
                    _queuedIds.Remove(job.itemId);
                }
                return; // 프레임당 1개 처리
            }

            // 2단계: RT → Texture2D 베이크 → 캐시 저장 → 임시 자원 파괴
            Texture2D tex = BakeFromRT(job);
            if (tex != null)
                _iconCache[job.itemId] = tex;

            CleanupJob(ref job);
            _bakeQueue.RemoveAt(0);
            _queuedIds.Remove(job.itemId);
        }

        // ================================================================
        //  싱글턴 보조
        // ================================================================

        /// <summary>인스턴스가 없으면 자동 생성 (정적 호출만으로 시스템이 동작하도록).</summary>
        private static GblItemIconRenderer EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            try
            {
                var go = new GameObject("GblItemIconRenderer");
                go.AddComponent<GblItemIconRenderer>(); // Awake가 Instance 할당
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GblItemIconRenderer] 인스턴스 생성 실패: " + e.Message);
            }
            return Instance;
        }

        // ================================================================
        //  모델키 해석
        // ================================================================

        /// <summary>
        /// 명시 맵 (weapon_{type}_{metal} → {metal}_{type}).
        /// steel/crystal/stone/wood × sword/spear/bow/dagger — GLB 전부 존재 확인됨.
        /// </summary>
        private static readonly Dictionary<string, string> _itemToModel = new Dictionary<string, string>
        {
            { "weapon_sword_wood",     "wood_sword" },
            { "weapon_spear_wood",     "wood_spear" },
            { "weapon_bow_wood",       "wood_bow" },
            { "weapon_dagger_wood",    "wood_dagger" },
            { "weapon_sword_steel",    "steel_sword" },
            { "weapon_spear_steel",    "steel_spear" },
            { "weapon_bow_steel",      "steel_bow" },
            { "weapon_dagger_steel",   "steel_dagger" },
            { "weapon_sword_crystal",  "crystal_sword" },
            { "weapon_spear_crystal",  "crystal_spear" },
            { "weapon_bow_crystal",    "crystal_bow" },
            { "weapon_dagger_crystal", "crystal_dagger" },
            { "weapon_sword_stone",    "stone_sword" },
            { "weapon_spear_stone",    "stone_spear" },
            { "weapon_bow_stone",      "stone_bow" },
            { "weapon_dagger_stone",   "stone_dagger" },
        };

        /// <summary>
        /// 아이템 id → GLB 모델키.
        /// 1) 명시 맵 조회
        /// 2) 관례 후보: id 그대로 → 접두사 제거("weapon_","mat_","meat_","combo_","item_")
        ///    → 후보2 그대로(하이픈/언더스코어 등 변형 없이 trim만). RuntimeModelLoader.HasModel로 존재 확인.
        /// 3) 없으면 null.
        /// </summary>
        private static string ResolveModelKey(PlayerInventory.ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
                return null;

            // 1) 명시 맵
            if (_itemToModel.TryGetValue(item.id, out string mapped)
                && RuntimeModelLoader.HasModel(mapped))
            {
                return mapped;
            }

            // 2) 관례 후보 1: id 그대로 (예: herb_red, steel_sword)
            if (RuntimeModelLoader.HasModel(item.id))
                return item.id;

            // 3) 관례 후보 2: 접두사 제거 (예: mat_rabbit_fur → rabbit_fur)
            string stripped = StripPrefix(item.id);
            if (!string.IsNullOrEmpty(stripped) && stripped != item.id
                && RuntimeModelLoader.HasModel(stripped))
            {
                return stripped;
            }

            // 4) 관례 후보 3: 후보2 그대로(하이픈/언더스코어 변형 없이 trim만)
            string trimmed = stripped?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && trimmed != stripped
                && RuntimeModelLoader.HasModel(trimmed))
            {
                return trimmed;
            }

            return null;
        }

        /// <summary>id에서 알려진 접두사를 제거합니다.</summary>
        private static string StripPrefix(string id)
        {
            string[] prefixes = { "weapon_", "mat_", "meat_", "combo_", "item_" };
            foreach (string p in prefixes)
            {
                if (id.StartsWith(p, System.StringComparison.OrdinalIgnoreCase))
                    return id.Substring(p.Length);
            }
            return id;
        }

        // ================================================================
        //  마운트: 노드/카메라/RT 생성 (InventoryWindow.EnsurePreviewSetup 검증 패턴)
        // ================================================================

        private static bool TryMount(ref BakeJob job)
        {
            try
            {
                var prefab = Resources.Load<GameObject>("Models/UserProvided/" + job.modelKey);
                if (prefab == null)
                    return false; // 모델 없음 — 폴백

                job.node = Instantiate(prefab);
                job.node.name = "GblIconNode_" + job.modelKey;
                Vector3 remotePos = new Vector3(10000f, 1000f, 10000f); // 씬 물체와 안 겹치는 원격 위치
                job.node.transform.position = remotePos;

                // 스케일 정규화: 키 ~1.8m로
                var rends = job.node.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    float h = b.size.y;
                    if (h > 0.01f) job.node.transform.localScale *= 1.8f / h;
                    var b2 = rends[0].bounds;
                    foreach (var r in rends) b2.Encapsulate(r.bounds);
                    job.node.transform.position += new Vector3(0f, remotePos.y - b2.min.y, 0f);
                }

                job.rt = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32); // 아이콘 크기
                job.rt.name = "GblIconRT_" + job.modelKey;
                job.rt.Create();

                var camGo = new GameObject("GblIconCam_" + job.modelKey);
                job.cam = camGo.AddComponent<Camera>();
                job.cam.clearFlags = CameraClearFlags.SolidColor;
                job.cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f, 1f); // 짙은 슬레이트(기존 미리보기와 동일 미감)
                job.cam.cullingMask = ~0;
                job.cam.nearClipPlane = 0.1f;
                job.cam.farClipPlane = 20f;
                job.cam.fieldOfView = 30f;
                job.cam.targetTexture = job.rt;

                // 프레이밍: 렌더러 bounds 중심을 살짝 사선에서 바라보도록 카메라 배치
                if (rends.Length > 0)
                {
                    var rb = rends[0].bounds;
                    foreach (var r in rends) rb.Encapsulate(r.bounds);
                    Vector3 center = rb.center;
                    float halfH = Mathf.Max(0.5f, rb.extents.y * 1.25f);
                    float dist = halfH / Mathf.Tan(job.cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    job.cam.transform.position = center + new Vector3(dist * 0.35f, 0f, -dist);
                    job.cam.transform.LookAt(center);
                }

                // 첫 프레임 내용 보장 (이후에는 카메라 자동 렌더)
                try { job.cam.Render(); }
                catch { /* 첫 렌더 실패 무시 — 자동 렌더가 이어서 처리 */ }

                job.mounted = true;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GblItemIconRenderer] 마운트 실패('" + job.modelKey + "', 폴백 진행): " + e.Message);
                CleanupJob(ref job);
                return false;
            }
        }

        // ================================================================
        //  베이크: RT → Texture2D (TerrainTextureApplier/WaterMaterialUpgrader 검증 패턴)
        // ================================================================

        private static Texture2D BakeFromRT(BakeJob job)
        {
            try
            {
                if (job.rt == null)
                    return null;

                // 마운트 후 다음 프레임 — RT에 렌더된 내용을 ReadPixels로 복사
                Texture2D copy = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = job.rt;
                copy.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
                copy.Apply();
                RenderTexture.active = prev;

                copy.wrapMode = TextureWrapMode.ClampToEdge;
                copy.hideFlags = HideFlags.HideAndDontSave; // 씬 전환/메모리 누수 방지 (ItemIconDatabase 전례)
                copy.name = "GblIcon_" + job.modelKey;
                return copy;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GblItemIconRenderer] 베이크 실패('" + job.modelKey + "', 폴백 진행): " + e.Message);
                return null;
            }
        }

        // ================================================================
        //  임시 자원 정리 (InventoryWindow.ReleasePreview 검증 패턴)
        // ================================================================

        /// <summary>작업 항목의 임시 노드/카메라/RT를 파괴합니다.</summary>
        private static void CleanupJob(ref BakeJob job)
        {
            if (job.cam != null) { Destroy(job.cam.gameObject); job.cam = null; }
            if (job.node != null) { Destroy(job.node); job.node = null; }
            if (job.rt != null)
            {
                job.rt.Release();
                Destroy(job.rt);
                job.rt = null;
            }
        }

        /// <summary>대기 중인 모든 큐 항목의 임시 자원을 정리합니다 (OnDestroy 안전망).</summary>
        private static void CleanupQueue()
        {
            for (int i = 0; i < _bakeQueue.Count; i++)
            {
                BakeJob job = _bakeQueue[i];
                CleanupJob(ref job);
            }
            _bakeQueue.Clear();
            _queuedIds.Clear();
        }
    }
}
