using System.Collections.Generic;
using UnityEngine;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 병사 실제 3D 외형 아이콘 렌더러 (2026-09-11).
    ///
    /// GuardPlaceholder(런타임 생성 캡슐 병사)의 실제 외형(캡슐 바디 + 국적색 머티리얼 + 장비 실루엣)을
    /// 오프스크린 카메라로 RenderTexture에 찍어 Texture2D(128×128)로 베이크 후 캐시한다.
    /// GblItemIconRenderer(아이템 GLB)의 2단계 베이크 패턴을 그대로 재사용하되, 병사는 런타임 생성
    /// 개체라 프리팹 마운트 대신 **살아있는 guard 개체 자체를** 원격 위치로 잠깐 이동시켜 촬영한다:
    ///   1단계(마운트 프레임) — guard AI(GuardPlaceholder 컴포넌트)를 잠깐 비활성 + 원격 좌표로 이동 +
    ///                          전용 카메라(targetTexture=RT) 구성 후 cam.Render()로 첫 프레임 촬영.
    ///   2단계(다음 프레임)   — RT → ReadPixels로 Texture2D 베이크 → 캐시 저장 →
    ///                          **반드시 원래 position/enabled/선택 링으로 즉시 복원** (모든 경로, 누락 금지).
    ///
    /// - 캐시 키: nation|level|guardName — "같은 국적+레벨+이름이면 같은 외형"(캡슐+국적색+레벨 기반 장비) 전제.
    ///   GetInstanceID는 씬 재진입마다 새로 발급되어 캐시가 무한 쌓이므로 사용하지 않는다.
    /// - 베이크 큐는 Update에서 프레임당 1개씩 순차 처리 — 병사가 한 번에 한 개체씩만 잠깐 이동한다.
    /// - 선택 링(SelectionOutline_ 접두 자식 렌더러)은 아이콘 오염 방지를 위해 촬영 동안만 숨겼다가 복원.
    /// - 실패/예외 시 null 반환 → 호출측(GuardSquadHotbar)이 기존 절차 아바타(국적색 원형+이니셜) 폴백.
    ///   전 경로 try/catch — 크래시/예외 전파 금지.
    /// </summary>
    public class GuardIconRenderer : MonoBehaviour
    {
        private const int IconSize = 128;      // 아이콘 텍스처 크기 (GblItemIconRenderer 동일)
        private const int MaxCacheSize = 200;  // 캐시 상한 — 초과 시 새 베이크 중단(폴백 아이콘 유지)
        private const int MaxFailCount = 3;    // 키별 연속 실패 허용 횟수 — 초과 시 음성 캐시(재시도 중단)

        /// <summary>병사 촬영용 원격 좌표 — GblItemIconRenderer(10000,1000,10000)와 다른 자리(상호 오염 방지).</summary>
        private static readonly Vector3 RemotePos = new Vector3(10500f, 1000f, 10500f);

        /// <summary>싱글턴. Awake에서 중복 제거 후 할당 (DontDestroyOnLoad 없음 — 씬 전환 시 소멸).</summary>
        public static GuardIconRenderer Instance;

        // ── 캐시/큐 (static — 씬 전환 후에도 베이크된 아이콘 텍스처 유지) ──
        private static readonly Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();
        private static readonly List<BakeJob> _bakeQueue = new List<BakeJob>();
        private static readonly HashSet<string> _queuedKeys = new HashSet<string>();

        /// <summary>베이크 불가가 확인된 키(구조적 실패/연속 실패) — 재시도 생략, 즉시 폴백.</summary>
        private static readonly HashSet<string> _knownBroken = new HashSet<string>();
        private static readonly Dictionary<string, int> _failCounts = new Dictionary<string, int>();

        /// <summary>베이크 큐 항목: 살아있는 guard 개체를 임시 이동시켜 촬영한다(임시 노드 생성 없음).</summary>
        private struct BakeJob
        {
            public string cacheKey;
            public GuardPlaceholder guard;      // 촬영 대상 개체 — 씬 전환/사망으로 null·무효화 가능
            public Vector3 origPos;             // 촬영 후 복원할 원래 위치
            public Quaternion origRot;          // 촬영 후 복원할 원래 회전
            public bool guardWasEnabled;        // 원래 GuardPlaceholder.enabled 상태
            public bool teleported;             // 이동/비활성 완료 여부 (복원 멱등성 플래그)
            public Renderer[] suppressedRends;  // 촬영 동안만 숨긴 선택 링 렌더러
            public Camera cam;
            public RenderTexture rt;
            public bool mounted;
        }

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// 병사의 실제 3D 외형 아이콘을 반환(캐시). 없으면 베이크 큐에 등록하고 null 반환
        /// (호출측이 0.5초 폴링으로 재호출하면 몇 프레임 후 아이콘이 자동 반영됨).
        /// 사망(IsAlive false)/파괴 병사는 큐 등록하지 않고 null → 절차 아바타 폴백.
        /// 캐시에 이미 있으면 사망 병사라도 그대로 반환.
        /// </summary>
        public static Texture2D GetOrCreateIcon(GuardPlaceholder guard)
        {
            try
            {
                if (guard == null)
                    return null;

                // 에디터 모드 가드 — 오프스크린 렌더는 Play 전용
                if (!Application.isPlaying)
                    return null;

                string key = BuildCacheKey(guard);
                if (string.IsNullOrEmpty(key))
                    return null;

                // 캐시 히트
                if (_iconCache.TryGetValue(key, out Texture2D cached))
                {
                    if (cached != null)
                        return cached;
                    _iconCache.Remove(key); // 파괴된 텍스처 — 재베이크
                }

                // 사망/비활성 병사 — 촬영 불가, 큐 등록 금지(폴백 유지)
                if (!guard.IsAlive || !guard.gameObject.activeInHierarchy)
                    return null;

                // 베이크 불가 확인 키 — 즉시 폴백
                if (_knownBroken.Contains(key))
                    return null;

                // 큐 대기 중이면 대기 (베이크 완료까지 null)
                if (_queuedKeys.Contains(key))
                    return null;

                // 캐시 상한 초과 — 새 베이크 중단(폴백 유지)
                if (_iconCache.Count >= MaxCacheSize)
                    return null;

                // 베이크 큐 등록
                if (EnsureInstance() == null)
                    return null;

                _bakeQueue.Add(new BakeJob
                {
                    cacheKey = key,
                    guard = guard,
                    mounted = false,
                    teleported = false
                });
                _queuedKeys.Add(key);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GuardIconRenderer] GetOrCreateIcon 실패(폴백 진행): " + e.Message);
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
            // 대기 큐 정리 + 이동 중인 병사 원위치(안전망) — 베이크 완료분 캐시는 유지
            CleanupQueue();

            if (Instance == this)
                Instance = null;
        }

        /// <summary>베이크 큐를 프레임당 1개씩 순차 처리. 마운트 프레임 → 다음 프레임 베이크+원위치.</summary>
        private void Update()
        {
            if (_bakeQueue.Count == 0)
                return;

            BakeJob job = _bakeQueue[0];

            if (!job.mounted)
            {
                // 1단계: 병사 임시 이동 + 카메라/RT 마운트 (cam.Render()로 첫 프레임 촬영)
                if (TryMount(ref job))
                {
                    _bakeQueue[0] = job; // mounted=true 저장 — 다음 프레임에 베이크
                }
                else
                {
                    // 마운트 실패 — 병사 원위치 후 큐에서 제거(호출측 폴백 아이콘 유지)
                    RestoreGuard(ref job); // TryMount 내부 복원과 중복돼도 멱등(무해)
                    CleanupJob(ref job);
                    _bakeQueue.RemoveAt(0);
                    _queuedKeys.Remove(job.cacheKey);
                }
                return; // 프레임당 1개 처리
            }

            // 2단계: RT → Texture2D 베이크 → 캐시 저장 → 병사 반드시 원위치 → 임시 자원 파괴
            Texture2D tex = null;
            bool targetValid = false;
            try
            {
                // 촬영 대상 생존 재확인 — 사망(SetActive false)/파괴 시 베이크 스킵(조용한 폴기, 실패 누적 없음)
                targetValid = job.guard != null && job.guard.gameObject.activeInHierarchy;
                if (targetValid)
                    tex = BakeFromRT(job);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GuardIconRenderer] 베이크 중 예외('" + job.cacheKey + "', 폴백 진행): " + e.Message);
                RegisterFailure(job.cacheKey, false);
            }
            finally
            {
                RestoreGuard(ref job); // 원위치 복원 — 성공/실패 모든 경로에서 누락 금지
                CleanupJob(ref job);   // 카메라/RT 파괴
            }

            if (tex != null)
            {
                _iconCache[job.cacheKey] = tex;
                _failCounts.Remove(job.cacheKey);
            }
            else if (targetValid)
            {
                RegisterFailure(job.cacheKey, false); // 대상은 유효했는데 베이크 실패 — 누적 후 음성 캐시
            }

            _bakeQueue.RemoveAt(0);
            _queuedKeys.Remove(job.cacheKey);
        }

        // ================================================================
        //  싱글턴 보조
        // ================================================================

        /// <summary>인스턴스가 없으면 자동 생성 (정적 호출만으로 시스템이 동작하도록 — GblItemIconRenderer 동일).</summary>
        private static GuardIconRenderer EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            try
            {
                var go = new GameObject("GuardIconRenderer");
                go.AddComponent<GuardIconRenderer>(); // Awake가 Instance 할당
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GuardIconRenderer] 인스턴스 생성 실패: " + e.Message);
            }
            return Instance;
        }

        // ================================================================
        //  캐시 키 / 실패 누적
        // ================================================================

        /// <summary>
        /// 외형 식별 캐시 키 — "guard_{nation}_{level}_{guardName}".
        /// 외형은 캡슐 바디 + 국적색 머티리얼 + 레벨 기반 자동 장비(GuardEquipmentSpawner)로 결정되므로
        /// 같은 국적+레벨+이름이면 동일 외형으로 간주한다. 인스턴스 ID는 씬마다 새로 발급되어
        /// 캐시가 무한 쌓이므로 사용하지 않는다.
        /// </summary>
        private static string BuildCacheKey(GuardPlaceholder guard)
        {
            if (guard == null)
                return null;
            return "guard_" + guard.Nation + "_" + guard.Level + "_" + guard.GuardName;
        }

        /// <summary>
        /// 베이크 실패 누적 — 구조적 실패(렌더러 부재)는 즉시 음성 캐시, 그 외는 MaxFailCount회 연속
        /// 실패 시 음성 캐시로 전환해 폴링마다 무한 재촬영되는 것을 차단한다.
        /// </summary>
        private static void RegisterFailure(string key, bool structural)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (structural)
            {
                _knownBroken.Add(key);
                _failCounts.Remove(key);
                return;
            }

            int count = _failCounts.TryGetValue(key, out int current) ? current + 1 : 1;
            if (count >= MaxFailCount)
            {
                _knownBroken.Add(key);
                _failCounts.Remove(key);
                Debug.LogWarning("[GuardIconRenderer] 키 '" + key + "' 연속 베이크 실패 — 음성 캐시(절차 아바타 유지)");
            }
            else
            {
                _failCounts[key] = count;
            }
        }

        // ================================================================
        //  마운트: 살아있는 병사 임시 이동 + 오프스크린 카메라/RT
        //  (GblItemIconRenderer.TryMount 검증 패턴 — 노드 생성 대신 live 개체 이동)
        // ================================================================

        private static bool TryMount(ref BakeJob job)
        {
            try
            {
                GuardPlaceholder guard = job.guard;
                if (guard == null)
                    return false; // 큐 대기 중 파괴됨(씬 전환 등) — 조용히 폴기

                // 마운트 시점 재확인 — 사망/비활성된 인스턴스는 촬영하지 않는다(폴백)
                if (!guard.IsAlive || !guard.gameObject.activeInHierarchy)
                    return false;

                job.origPos = guard.transform.position;
                job.origRot = guard.transform.rotation;

                // 촬영 동안 AI 정지 — 원격 위치에서의 자발 이동/회전/전투 행동 차단
                job.guardWasEnabled = guard.enabled;
                guard.enabled = false;

                // 원격 좌표로 잠깐 이동 — 씬 물체와 안 겹치는 빈 공간(물리 간섭 없음)
                guard.transform.position = RemotePos;
                job.teleported = true;

                // 렌더러 수집 — 선택 링(SelectionOutline_)은 아이콘 오염 방지를 위해 촬영 동안만 숨김
                var allRends = guard.GetComponentsInChildren<Renderer>();
                var suppressed = new List<Renderer>();
                var bodyRends = new List<Renderer>();
                foreach (var r in allRends)
                {
                    if (r == null) continue;
                    if (r.name.StartsWith("SelectionOutline_"))
                    {
                        r.enabled = false;
                        suppressed.Add(r);
                    }
                    else
                    {
                        bodyRends.Add(r);
                    }
                }
                job.suppressedRends = suppressed.ToArray();

                if (bodyRends.Count == 0)
                {
                    RegisterFailure(job.cacheKey, true); // 렌더러 부재 = 구조적 실패 — 음성 캐시
                    return false;
                }

                job.rt = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32); // 아이콘 크기
                job.rt.name = "GuardIconRT_" + job.cacheKey;
                job.rt.Create();

                var camGo = new GameObject("GuardIconCam_" + job.cacheKey);
                job.cam = camGo.AddComponent<Camera>();
                job.cam.clearFlags = CameraClearFlags.SolidColor;
                job.cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f, 1f); // 짙은 슬레이트(GblItemIconRenderer 동일 미감)
                job.cam.cullingMask = ~0;
                job.cam.nearClipPlane = 0.1f;
                job.cam.farClipPlane = 20f;
                job.cam.fieldOfView = 30f;
                job.cam.targetTexture = job.rt;

                // 프레이밍: 병사 렌더러 bounds 중심을 살짝 사선에서 바라보도록 카메라 배치
                // (GblItemIconRenderer.TryMount 프레이밍 재사용 — live 개체라 스케일 정규화는 생략)
                Bounds rb = bodyRends[0].bounds;
                foreach (var r in bodyRends) rb.Encapsulate(r.bounds);
                Vector3 center = rb.center;
                float halfH = Mathf.Max(0.5f, rb.extents.y * 1.25f);
                float dist = halfH / Mathf.Tan(job.cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                job.cam.transform.position = center + new Vector3(dist * 0.35f, 0f, -dist);
                job.cam.transform.LookAt(center);

                // 첫 프레임 내용 보장 (이후에는 카메라 자동 렌더)
                try { job.cam.Render(); }
                catch { /* 첫 렌더 실패 무시 — 자동 렌더가 이어서 처리 */ }

                job.mounted = true;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GuardIconRenderer] 마운트 실패('" + job.cacheKey + "', 폴백 진행): " + e.Message);
                RegisterFailure(job.cacheKey, false);
                RestoreGuard(ref job); // 이동/비활성까지 진행됐다면 반드시 복원
                CleanupJob(ref job);
                return false;
            }
        }

        // ================================================================
        //  베이크: RT → Texture2D (GblItemIconRenderer.BakeFromRT 검증 패턴 그대로)
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

                copy.wrapMode = TextureWrapMode.Clamp;
                copy.filterMode = FilterMode.Bilinear;
                copy.hideFlags = HideFlags.HideAndDontSave; // 씬 전환/메모리 누수 방지 (ItemIconDatabase 전례)
                copy.name = "GuardIcon_" + job.cacheKey;
                return copy;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GuardIconRenderer] 베이크 실패('" + job.cacheKey + "', 폴백 진행): " + e.Message);
                return null;
            }
        }

        // ================================================================
        //  병사 복원 / 임시 자원 정리 (InventoryWindow.ReleasePreview 검증 패턴)
        // ================================================================

        /// <summary>
        /// 촬영했던 병사를 원래 상태로 복원(위치/회전 + enabled + 숨긴 선택 링).
        /// 성공/실패/파괴 모든 경로에서 호출 — teleported 플래그로 멱등(중복 호출 무해).
        /// </summary>
        private static void RestoreGuard(ref BakeJob job)
        {
            if (!job.teleported)
                return;
            job.teleported = false;

            try
            {
                GuardPlaceholder guard = job.guard;
                if (guard != null)
                {
                    guard.transform.position = job.origPos; // 즉시 원위치 — 플레이 경험 보존
                    guard.transform.rotation = job.origRot;
                    guard.enabled = job.guardWasEnabled;    // AI 재개 (촬영 중 사망 SetActive false와 무관 — enabled 플래그만 복원)

                    if (job.suppressedRends != null)
                    {
                        foreach (var r in job.suppressedRends)
                        {
                            if (r != null) r.enabled = true; // 선택 링 복원
                        }
                        job.suppressedRends = null;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GuardIconRenderer] 병사 원위치 복원 실패: " + e.Message);
            }
        }

        /// <summary>작업 항목의 임시 카메라/RT를 파괴합니다. 병사 개체는 절대 파괴하지 않는다.</summary>
        private static void CleanupJob(ref BakeJob job)
        {
            if (job.cam != null) { Destroy(job.cam.gameObject); job.cam = null; }
            if (job.rt != null)
            {
                job.rt.Release();
                Destroy(job.rt);
                job.rt = null;
            }
            job.suppressedRends = null;
        }

        /// <summary>대기 중인 모든 큐 항목의 병사 원위치 + 임시 자원을 정리합니다 (OnDestroy 안전망).</summary>
        private static void CleanupQueue()
        {
            for (int i = 0; i < _bakeQueue.Count; i++)
            {
                BakeJob job = _bakeQueue[i];
                RestoreGuard(ref job);
                CleanupJob(ref job);
            }
            _bakeQueue.Clear();
            _queuedKeys.Clear();
        }
    }
}