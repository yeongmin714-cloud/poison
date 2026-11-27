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
    /// - [2026-11-25 전 아이템 동기 베이크] 에디터 Play — 물고기 전용이던 동기 베이크([34차])를
    ///   전 아이템으로 확장(마운트→cam.Render→ReadPixels→캐시, 1프레임 완결). 큐 경로의
    ///   "마운트 프레임→다음 프레임 베이크" 2프레임 갭(N번째 아이템 ≈ 2N프레임 지연)이 제거되어
    ///   전 아이템 아이콘이 즉시·균일 표출.
    /// - 베이크 큐는 Update에서 1개씩 순차 처리(동시 다수 카메라 생성으로 인한 퍼포먼스 급락 방지) —
    ///   에디터에서는 동기 베이크 실패 시의 폴백 + 비에디터(디바이스) 빌드 본경로로 강등.
    /// - InventoryWindow의 시스템 프리뷰(_previewRT)와는 무관한 독립 인스턴스.
    /// - 임시 노드/카메라/RT는 베이크 직후 파괴, 예외 시에도 정리 후 폴백 진행(크래시 금지).
    /// - [166차 E] 아이콘 방향 통일 — 카메라 정면 고정(FrameIconCamera) + 최장축 수평 자동 정규화
    ///   (ApplyIconOrientation) + per-key 방향 오버라이드(_orientationOverrides).
    /// </summary>
    public class GblItemIconRenderer : MonoBehaviour
    {
        private const int IconSize = 256;      // 아이콘 텍스처 크기 — [197차] 128→256 고품질화(RT/ReadPixels 전부 이 상수 공유)
        /// <summary>[197차] 베이크 RT 크기 = IconSize*4 — 4× 슈퍼샘플 렌더 후 4×4 박스다운샘플(16탭 AA).
        ///   MSAA RT ReadPixels가 배치 d3d12에서 GPU 디바이스 크래시하는 실측(IconBakeFix194 재베이크)의 대체 경로.</summary>
        private const int BakeRTSize = IconSize * 4;

        // ── [209차 B] 베이크 후처리 튜닝 상수(public const — 튜닝 단일 수정점) ──
        /// <summary>[209차 B] 비네트 강도 — 정규화 중심거리 제곱(d²·0.5, 코너=1.0)에 곱하는 최대 감광률.
        ///   0.15 = 모서리 최대 15% 감광(완만) — 중심부는 거의 무영향, 아이콘 시각적 응집감 부여.</summary>
        public const float VignetteStrength = 0.15f;
        /// <summary>[209차 B] 콘트라스트 부스트 — 0.5 피벗 확장((c-0.5)·k+0.5). 1.05 = 마일드.</summary>
        public const float ContrastBoost = 1.05f;
        /// <summary>[209차 B] 채도 부스트 — Rec.601 루마 보존 선형 확장. 1.05 = 마일드.</summary>
        public const float SaturationBoost = 1.05f;
        /// <summary>[209차 B] 아이템 픽셀 판정 알파 임계 — 미만은 배경 픽셀로 후처리 스킵(알파/색 원값 보존).
        ///   RT 배경 실측(209차): 양 베이크 경로 모두 CameraClearFlags.SolidColor + backgroundColor
        ///   알파 1(불투명, Color(0.06,0.06,0.09,1)) → 현행 전 픽셀이 임계를 통과해 전면 적용.
        ///   향후 배경 투명 RT 전환 시에도 본 가드가 배경 무손상을 자동 보장(선제 설계).</summary>
        public const float ItemAlphaThreshold = 0.05f;

        /// <summary>[197차] 4× 슈퍼샘플 베이크 — RT(IconSize*4)를 ReadPixels 후 N×N 박스 평균으로 IconSize 텍스처 생성.
        ///   MSAA 대체 AA(16타일 평균 ≈ 4x 박스 필터) — 계단현상 완화 + 고해상도 동시 확보.</summary>
        private static Texture2D BakeFromRTDownsampled(RenderTexture rt, string texName)
        {
            Texture2D big = new Texture2D(BakeRTSize, BakeRTSize, TextureFormat.RGBA32, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            big.ReadPixels(new Rect(0, 0, BakeRTSize, BakeRTSize), 0, 0);
            big.Apply();
            RenderTexture.active = prev;
            Color32[] bigPx = big.GetPixels32();
            Object.DestroyImmediate(big);
            const int N = 4;                      // 다운샘플 배율 = BakeRTSize/IconSize
            float inv = 1f / (N * N);
            Color32[] outPx = new Color32[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            {
                int srcRow = y * N * BakeRTSize;
                int dstRow = y * IconSize;
                for (int x = 0; x < IconSize; x++)
                {
                    int sr = srcRow + x * N;
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int dy = 0; dy < N; dy++)
                    {
                        int row = sr + dy * BakeRTSize;
                        for (int dx = 0; dx < N; dx++)
                        {
                            Color32 p = bigPx[row + dx];
                            r += p.r; g += p.g; b += p.b; a += p.a;
                        }
                    }
                    // [209차 B] 박스 평균 출력 픽셀 후처리 — 비네트/콘트라스트/채도(알파 보존 가드).
                    //   바이트 반올림 후 정규화 입력 — 후처리 미적용(배경) 픽셀의 round-trip은 항등
                    //   (byte→[0,1]→byte 재반올림 = 원값, 기존 출력과 비트 동일 보존).
                    byte br = (byte)(r * inv + 0.5f), bg = (byte)(g * inv + 0.5f);
                    byte bb = (byte)(b * inv + 0.5f), ba = (byte)(a * inv + 0.5f);
                    float fr = br / 255f, fg = bg / 255f, fb = bb / 255f;
                    float nx = (x + 0.5f) / IconSize * 2f - 1f;
                    float ny = (y + 0.5f) / IconSize * 2f - 1f;
                    ApplyIconPixelPostFX(nx, ny, ba / 255f, ref fr, ref fg, ref fb);
                    outPx[dstRow + x] = new Color32(
                        (byte)(fr * 255f + 0.5f), (byte)(fg * 255f + 0.5f),
                        (byte)(fb * 255f + 0.5f), ba);
                }
            }
            var copy = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            copy.SetPixels32(outPx);
            copy.Apply();
            copy.wrapMode = TextureWrapMode.Clamp;
            copy.hideFlags = HideFlags.HideAndDontSave; // 씬 전환/메모리 누수 방지 (ItemIconDatabase 전례)
            copy.name = texName;
            return copy;
        }

        /// <summary>
        /// [209차 B] 다운샘플 출력 픽셀 후처리(순수 함수 — 입력→출력 결정적, 단위 테스트 가능).
        /// ① 비네트: 정규화 중심거리 제곱 폴백((nx²+ny²)·0.5, 코너=1.0) — 중심부 영향 최소화.
        /// ② 콘트라스트(0.5 피벗) + 채도(루마 보존) 마일드 부스트.
        /// 알파 보존 가드: alpha &lt; ItemAlphaThreshold면 무처리 조기 반환 — 배경 픽셀 알파/색 보존.
        /// 입력 RGB는 [0,1] 정규화, 출력은 Clamp01 보장(콘트라스트 오버슈트 클리핑 방지).
        /// </summary>
        public static void ApplyIconPixelPostFX(float nx, float ny, float alpha, ref float r, ref float g, ref float b)
        {
            if (alpha < ItemAlphaThreshold)
                return;                       // 배경 픽셀 — 알파/색 보존 가드
            float vig = 1f - VignetteStrength * (nx * nx + ny * ny) * 0.5f;   // 코너(±1,±1) → 1-Strength
            r *= vig; g *= vig; b *= vig;
            r = (r - 0.5f) * ContrastBoost + 0.5f;
            g = (g - 0.5f) * ContrastBoost + 0.5f;
            b = (b - 0.5f) * ContrastBoost + 0.5f;
            float lum = 0.299f * r + 0.587f * g + 0.114f * b;
            r = Mathf.Clamp01(lum + (r - lum) * SaturationBoost);
            g = Mathf.Clamp01(lum + (g - lum) * SaturationBoost);
            b = Mathf.Clamp01(lum + (b - lum) * SaturationBoost);
        }
        // [197차] 직교 충전률 마진 — 최대 화면축 반경의 112% 프레이밍(충전률 ~89% 목표,
        //   194차 1.25마진+퍼스펙티브 후퇴를 직교 전환으로 대체. 1.08→1.12: 기울임 물체 모서리 여유).
        private const float IconFillMargin = 1.12f;

        /// <summary>[197차] 베이크 전용 3점 조명 릭 — 카메라 자식으로 부착(정리는 카메라 파괴와 동반).
        ///   씬 디렉셔널 위에 얹히는 보조광 — 방향성이 본질. 키(우상단 웜화이트)+필(좌측 저강도)+림(후상단 외곽선).
        ///   [209차 B] 명암 대비 상향(완만): 키 0.45→0.60·필 0.15→0.10(명암 극대화)·림 0.30→0.45(에지 발광 팝),
        ///   키 각도 (30,40,0)→(35,25,0) 측면성 강화, 림 각도 (0,25,180) 유지.
        ///   근거 — 고강도 조명은 흰 재질 백색클리핑 실측(197차 2차 하향)이 있어 상향 폭을 완만히 제한
        ///   (키 +33%가 상한선, 추가 상향 시 클리핑 재발 위험 — Play 재베이크 실측 대기).</summary>
        private static void AttachIconLightRig(Camera cam)
        {
            CreateRigLight(cam, "IconKey", new Vector3(35f, 25f, 0f), new Color(1f, 0.96f, 0.9f), 0.60f);
            CreateRigLight(cam, "IconFill", new Vector3(-45f, 15f, 0f), new Color(0.8f, 0.88f, 1f), 0.10f);
            CreateRigLight(cam, "IconRim", new Vector3(0f, 25f, 180f), new Color(1f, 1f, 1f), 0.45f);
        }

        private static void CreateRigLight(Camera cam, string name, Vector3 euler, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(cam.transform, false);
            go.transform.localRotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }
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
            public string glbPath;   // [2026-10-11 28차] 물고기 등 UserProvided 밖 GLB 직결 경로(에디터 로드)
            public GameObject node;
            public Camera cam;
            public RenderTexture rt;
            public bool mounted;
        }

        // ================================================================
        //  Public API
        // ================================================================

        /// <summary>
        /// 아이템의 GLB 아이콘을 반환(캐시). 에디터 Play에서는 동기 베이크로 즉시 반환(1프레임 완결).
        /// 동기 베이크 불가/실패 시 베이크 큐에 등록하고 null 반환
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

                // 모델키 해석 실패 → 물고기 GLB 직결 검사 후에도 없으면 절차 폴백
                string modelKey = ResolveModelKey(item);
                string glbPath = null;
                if (string.IsNullOrEmpty(modelKey))
                {
                    // [2026-10-11 28차] 물고기(fish_glb_<key>) — FishCatalog GLB 직결
                    //   (Assets/새로운 glb/crops-fish/, 에디터 AssetDatabase 로드 — UserProvided 밖).
                    glbPath = ResolveFishGlbPath(item);
                    if (string.IsNullOrEmpty(glbPath))
                    {
                        _knownMissing.Add(item.id); // 음성 캐시 — 재검색 생략
                        return null;
                    }
                    modelKey = item.id;   // 캐시/노드/RT 명명용
                }

                // 캐시 상한 초과 — 새 베이크 중단(폴백 유지). 동기 베이크도 상한 검사 뒤로 이동 —
                //   상한 계약이 물고기 직결 경로에도 균일 적용(기존 물고기 동기 베이크는 상한 우회였음).
                if (_iconCache.Count >= MaxCacheSize)
                    return null;

                // [2026-11-25 전 아이템 동기 베이크] 에디터 Play — 물고기 전용([34차])이던 동기 베이크를
                //   전 아이템으로 확장. 마운트→cam.Render→ReadPixels→캐시가 1회 호출에 완결되어
                //   큐의 "마운트 프레임→다음 프레임 베이크" 2프레임 갭(N번째 아이템 ≈ 2N프레임 지연)
                //   없이 전 아이템 아이콘이 즉시·균일 표출. 실패 시 아래 큐로 낙하
                //   (물고기 [34차]와 동일 폴백 규약 — 크래시 금지).
#if UNITY_EDITOR
                var synced = SyncBakeItem(item.id, modelKey, glbPath);
                if (synced != null)
                    return synced;
#endif

                // 베이크 큐 등록 (에디터 동기 베이크 실패 폴백 + 비에디터 빌드 본경로)
                if (EnsureInstance() == null)
                    return null;

                _bakeQueue.Add(new BakeJob
                {
                    itemId = item.id,
                    modelKey = modelKey,
                    glbPath = glbPath,
                    mounted = false
                });
                _queuedIds.Add(item.id);
                // [32차 진단] 물고기 GLB 미표시 원인 판별용 — id당 1회(큐 등록 시점)
                if (glbPath != null)
                    Debug.Log($"[GblItemIconRenderer] 베이크 큐 등록 — {item.id} glb={glbPath}");
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
        /// 명시 맵 (item id → GLB 모델명). 명시 맵이 관례 후보(접두 제거 등)보다 우선.
        /// 무기 16종: weapon_{type}_{tier} → {tier}_{type} (type=sword/spear/bow/dagger,
        /// tier=wood/steel/crystal/stone — GLB 전부 존재 확인됨).
        /// 방어구/부속 28종: armor/helmet/boots/gloves/shield/gas_mask/chemical_pack_{tier}
        ///   → {tier}_{armor|helmet|boot_left|glove_left|shield|gas_mask|chemical_pack}.
        /// boots/gloves는 아이콘용 단일 좌측 모델(boot_left/glove_left) 사용.
        /// GLB 부재 id는 ResolveModelKey의 RuntimeModelLoader.HasModel 검사로 절차 폴백.
        /// </summary>
        private static readonly Dictionary<string, string> _itemToModel = new Dictionary<string, string>
        {
            // ── 무기 16종 (weapon_{type}_{tier} → {tier}_{type}) ──
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

            // ── 방어구 20종 (× 4티어: wood/steel/stone/crystal) ──
            { "armor_wood",     "wood_armor" },
            { "armor_steel",    "steel_armor" },
            { "armor_stone",    "stone_armor" },
            { "armor_crystal",  "crystal_armor" },
            { "helmet_wood",    "wood_helmet" },
            { "helmet_steel",   "steel_helmet" },
            { "helmet_stone",   "stone_helmet" },
            { "helmet_crystal", "crystal_helmet" },
            { "boots_wood",     "wood_boot_left" },   // [TEST28-69차] 아이콘은 실존 좌 GLB 참조(통합 아이템 id wood_boot는 GLB 파일 없음)
            { "boots_steel",    "steel_boot_left" },
            { "boots_stone",    "stone_boot_left" },
            { "boots_crystal",  "crystal_boot_left" },
            { "gloves_wood",    "wood_glove_left" },
            { "gloves_steel",   "steel_glove_left" },
            { "gloves_stone",   "stone_glove_left" },
            { "gloves_crystal", "crystal_glove_left" },
            // [TEST28-69차] 통합 부츠/장갑 아이템 id → 실존 좌 GLB (아이콘 베이크용 — ResolveModelKey 1순위 맵)
            { "wood_boot",      "wood_boot_left" },
            { "steel_boot",     "steel_boot_left" },
            { "stone_boot",     "stone_boot_left" },
            { "crystal_boot",   "crystal_boot_left" },
            { "wood_glove",     "wood_glove_left" },
            { "steel_glove",    "steel_glove_left" },
            { "stone_glove",    "stone_glove_left" },
            { "crystal_glove",  "crystal_glove_left" },
            { "shield_wood",    "wood_shield" },
            { "shield_steel",   "steel_shield" },
            { "shield_stone",   "stone_shield" },
            { "shield_crystal", "crystal_shield" },

            // ── 부속 8종 (gas_mask/chemical_pack × 4티어) ──
            { "gas_mask_wood",     "wood_gas_mask" },
            { "gas_mask_steel",    "steel_gas_mask" },
            { "gas_mask_stone",    "stone_gas_mask" },
            { "gas_mask_crystal",  "crystal_gas_mask" },
            { "chemical_pack_wood",   "wood_chemical_pack" },
            { "chemical_pack_steel",  "steel_chemical_pack" },
            { "chemical_pack_stone",  "stone_chemical_pack" },
            { "chemical_pack_crystal","crystal_chemical_pack" },
        };

        /// <summary>
        /// 아이템 id → GLB 모델키.
        /// 1) 명시 맵 조회
        /// 2) 관례 후보: id 그대로 → 접두사 제거("weapon_","mat_","meat_","combo_","item_")
        ///    → 후보2 그대로(하이픈/언더스코어 등 변형 없이 trim만). RuntimeModelLoader.HasModel로 존재 확인.
        /// 3) 없으면 null.
        /// [68차 P3] public 전환 — 핫바 숫자키 들기(ItemHoldManager.Hold)에서 재사용.
        /// </summary>
        public static string ResolveModelKey(PlayerInventory.ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
                return null;

            // 1) 명시 맵
            if (_itemToModel.TryGetValue(item.id, out string mapped)
                && RuntimeModelLoader.HasModel(mapped))
            {
                return mapped;
            }

            // [172차] 1.5) 왕실 요리 dish_XXX — 요리명 어미 → GLB 버킷(DishGlbCatalog, 17종 공유 버킷).
            //   버킷 미매칭/GLB 부재 시 관례 후보로 낙하 → 결국 null(절차 아이콘 폴백 유지).
            if (item.id.StartsWith(DishGlbCatalog.DishPrefix, System.StringComparison.Ordinal))
            {
                string dishBucket = DishGlbCatalog.ModelKey(item.id);
                if (!string.IsNullOrEmpty(dishBucket) && RuntimeModelLoader.HasModel(dishBucket))
                    return dishBucket;
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

        /// <summary>[2026-10-11 28차] fish_glb_<key> → FishCatalog GLB 전체 경로. 물고기 외 null.</summary>
        private static string ResolveFishGlbPath(PlayerInventory.ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.id)) return null;
            const string prefix = "fish_glb_";
            if (!item.id.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return null;
            string key = item.id.Substring(prefix.Length);
            string path = FishCatalog.GlbPathByKey(key);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        // ================================================================
        //  [34차 → 2026-11-25] 동기 베이크 — 1회 호출 안에서 마운트→렌더→ReadPixels→캐시까지
        //  완결(큐/프레임 갭 없음). 물고기 전용 진입점으로 증명된 동일 프레임 완결성을 전
        //  아이템으로 확장: 프리팹 해석(GLB 직결 경로 → Resources/UserProvided, TryMount 동일
        //  규약) 후 공용 본(SyncBakeFromPrefab) 1회 위임. 방향·프레이밍·거치대 은닉 규약 통일.
        // ================================================================
#if UNITY_EDITOR
        /// <summary>
        /// [2026-11-25 전 아이템 확장] 동기 베이크 진입 — 프리팹 해석 후 공용 본 위임.
        /// ① glbPath(물고기 등 UserProvided 밖 GLB) → 에디터 AssetDatabase 로드
        /// ② modelKey → Resources.Load("Models/UserProvided/") (TryMount와 동일 규약·순서)
        /// 프리팹 부재 시 null 반환 — 호출측이 큐 폴백 → 최종 절차 아이콘 폴백(크래시 금지).
        /// </summary>
        private static Texture2D SyncBakeItem(string itemId, string modelKey, string glbPath)
        {
            if (string.IsNullOrEmpty(itemId))
                return null;
            try
            {
                GameObject prefab = null;
                if (!string.IsNullOrEmpty(glbPath))
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
                    if (prefab == null)
                        Debug.LogWarning($"[GblItemIconRenderer] 동기 베이크 실패(프리팹 없음) — {glbPath}");
                }
                if (prefab == null && !string.IsNullOrEmpty(modelKey))
                    prefab = Resources.Load<GameObject>("Models/UserProvided/" + modelKey);
                if (prefab == null)
                    return null;
                return SyncBakeFromPrefab(itemId, modelKey, prefab);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GblItemIconRenderer] 동기 베이크 예외(폴백 진행): " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 동기 베이크 공용 본 — Instantiate→거치대 은닉→방향 정규화→(요리 틴트)→최장축핏 스케일→
        /// 바닥 정렬→오프스크린 렌더→ReadPixels→캐시 저장. TryMount/BakeFromRT와 공용 헬퍼
        /// (ApplyIconOrientation/FrameIconCamera) 경유로 방향·프레이밍 통일. 임시 노드/카메라/RT는
        /// 베이크 직후 파괴, 예외 시에도 정리 후 폴백(크래시 금지 — 헤더 계약).
        /// </summary>
        private static Texture2D SyncBakeFromPrefab(string itemId, string modelKey, GameObject prefab)
        {
            GameObject node = null;
            GameObject camGo = null;
            RenderTexture rt = null;
            try
            {
                node = Object.Instantiate(prefab);
                node.name = "GblIconSync_" + itemId;
                Vector3 remotePos = new Vector3(10000f, 1000f, 10000f);
                node.transform.position = remotePos;

                // [165b차] 거치대 노드 은닉 — TryMount와 동일 규약(아이콘에 거치대 포함 방지).
                foreach (var n in ItemHoldManager.GetHideNodeNames(modelKey) ?? System.Array.Empty<string>())
                    ItemHoldManager.FindHideNode(node.transform, n)?.gameObject.SetActive(false);

                var rends = node.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0)
                {
                    Object.DestroyImmediate(node);
                    node = null;
                    return null;
                }
                // [166차 E] 방향 정규화 선행 — 최장축 수평(per-key 오버라이드 우선, TryMount 동일).
                ApplyIconOrientation(modelKey, node, rends);
                // [172차] 왕실 요리 — 재료 카테고리 hue 틴트(dish_ 전용, white면 무동작, MPB만).
                if (itemId != null && itemId.StartsWith(DishGlbCatalog.DishPrefix, System.StringComparison.Ordinal))
                    DishGlbCatalog.ApplyTint(node, DishGlbCatalog.TintFor(DishGlbCatalog.CatsFor(itemId)));
                // [198차] per-item 아이콘 텍스처 오버라이드(테이블 등록분만 — MPB 텍스처 교체, 틴트와 공존).
                ItemIconTextureOverrides.TryApply(itemId, node);
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                // [166차 E] 최장축핏 일반화 — 높이핏 폭발 방지(TryMount와 동일 규약).
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxDim > 0.01f) node.transform.localScale *= 1.8f / maxDim;
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                node.transform.position += new Vector3(0f, remotePos.y - b.min.y, 0f);
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);

                rt = new RenderTexture(BakeRTSize, BakeRTSize, 24, RenderTextureFormat.ARGB32); // [197차] 2× 슈퍼샘플 RT
                rt.Create();
                camGo = new GameObject("GblIconSyncCam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f, 1f);
                cam.cullingMask = ~0;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 20f;
                cam.fieldOfView = 30f;
                cam.targetTexture = rt;

                // [142차 H] 아이콘 잘림 수리(수평/수직 반각 전체핏) + [166차 E] 정면 고정 —
                // 공용 헬퍼로 TryMount와 동일 프레이밍(아이콘 방향·프레이밍 통일).
                FrameIconCamera(cam, b);
                AttachIconLightRig(cam);   // [197차] 베이크 전용 3점 조명 릭
                cam.Render();

                // [197차] 2× 슈퍼샘플 베이크 — 512 RT 렌더 → 256 박스다운샘플(MSAA 대체:
                //   배치 d3d12에서 MSAA RT ReadPixels가 GPU 디바이스 크래시하는 실측 대체 경로).
                Texture2D copy = BakeFromRTDownsampled(rt, "GblIcon_" + itemId);

                // [187차 수리] 동기베이크는 같은 프레임에 여러 아이템을 연속 베이크 — Destroy(프레임 끝 지연)로 두면
                //   직전 아이템 노드가 프레임 끝까지 살아 같은 원격 위치(10000,1000,10000)에 남아 뒤 아이템의
                //   카메라가 겹쳐 찍는다("아이템 겹침" 사용자 실측 187차). DestroyImmediate 즉시 파괴 —
                //   비동기 큐 경로는 프레임 간 처리라 영향 없음(원본 유지). [126차] DestroyImmediate 스폰 코드 규약.
                Object.DestroyImmediate(camGo);
                camGo = null;
                Object.DestroyImmediate(node);
                node = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                rt = null;

                _iconCache[itemId] = copy;
                Debug.Log($"[GblItemIconRenderer] 동기 베이크 완료 — {itemId} model={modelKey}");
                return copy;
            }
            catch (System.Exception e)
            {
                // 예외 시에도 임시 자원 정리 후 폴백(크래시 금지 — 헤더 계약).
                //  [187차 수리] 동기베이크 경로는 같은 프레임 연속 호출이라 지연 파괴 불가 — DestroyImmediate.
                if (camGo != null) Object.DestroyImmediate(camGo);
                if (node != null) Object.DestroyImmediate(node);
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
                Debug.LogWarning("[GblItemIconRenderer] 동기 베이크 예외(폴백 진행): " + e.Message);
                return null;
            }
        }
#endif

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
        //  [166차 E] 아이콘 방향 통일 — 카메라 정면 고정 + 최장축 수평 자동 정규화
        //  + per-key 방향 오버라이드(ItemHoldManager.HoldConfig와 같은 패턴 선례)
        // ================================================================

        /// <summary>
        /// per-key 방향 오버라이드(오일러 각도, 절대 적용) — 자동 최장축 정규화가 어긋나는 종만 수기 지정.
        /// 초기 비움(자동 정규화 우선) — Play 실측(낚싯대·검·헬멧·말 샘플)에서 어긋나는 종만 추가(Play 트림 대상).
        /// </summary>
        private static readonly Dictionary<string, Vector3> _orientationOverrides = new Dictionary<string, Vector3>
        {
            // [194차 P3] 접시/음식류 — GLB가 XZ 평면에 눕혀져 있거나(비좁형 판정으로 자동 회전 제외)
            //   원본 (90,0,0) 굽음(바닥면 노출)이라 정면 카메라에서 측면만 보임 → 절대 오일러로
            //   상면이 카메라를 향하는 3/4 뷰(X −70°) 통일. 진단 프로브 ext 실측 → 접촉시트 vision 검증.
            { "dish_stirfry",    new Vector3(-70f, 0f, 0f) },
            { "dish_braise",     new Vector3(-70f, 0f, 0f) },
            { "dish_steam",      new Vector3(-70f, 0f, 0f) },
            { "dish_salad",      new Vector3(-70f, 0f, 0f) },
            { "dish_stew",       new Vector3(-70f, 0f, 0f) },
            { "dish_jeon",       new Vector3(-70f, 0f, 0f) },
            { "dish_sashimi",    new Vector3(-70f, 0f, 0f) },
            { "dish_ricebowl",   new Vector3(-70f, 0f, 0f) },
            { "dish_steak",      new Vector3(-70f, 0f, 0f) },
            { "dish_pie",        new Vector3(-70f, 0f, 0f) },
            { "grilled_skin",    new Vector3(-70f, 0f, 0f) },
            { "tempura",         new Vector3(-70f, 0f, 0f) },
            { "yakitori_skewer", new Vector3(-70f, 0f, 0f) },
            // 물약 2종/삽 — Y장축 원본이 자동 롤(Euler(0,0,−90))로 눕워져 보임 → 원본 직립 복원(절대 0).
            { "potion_sedative", new Vector3(0f, 0f, 0f) },
            { "potion_stealth",  new Vector3(0f, 0f, 0f) },
            { "tool_shovel",     new Vector3(0f, 0f, 0f) },
            // 곡괭이 — 평면 위시 뷰(자루 일자) → T자 프로필이 보이는 틸트.
            { "tool_pickaxe",    new Vector3(-65f, 0f, 0f) },
            // 수정 파편 — X장축 눕힘 → 직립(+X→+Y).
            { "crystal_shard",   new Vector3(0f, 0f, 90f) },
            // 엄니/이빨 — Z장축 → 팁이 위를 향게 직립(R_x(−90): +Z→+Y).
            { "boar_tusk",       new Vector3(-90f, 0f, 0f) },
            { "wolf_tooth",      new Vector3(-90f, 0f, 0f) },
            // 목재 갑옷 — GLB 원본 (90,0,0) 굽음으로 바닥면이 보임 → 원본 자세 되돌림.
            { "wood_armor",      new Vector3(0f, 0f, 0f) },
            // 예: { "fishing_rod", new Vector3(0f, 0f, 90f) },
        };

        /// <summary>[166차 E] per-key 방향 오버라이드 조회 — 미등록 키 false(자동 정규화 사용).</summary>
        public static bool TryGetOrientationOverride(string modelKey, out Vector3 localEuler)
        {
            if (modelKey != null && _orientationOverrides.TryGetValue(modelKey, out localEuler))
                return true;
            localEuler = Vector3.zero;   // 미등록/null — CS0177 방지 명시 할당
            return false;
        }

        /// <summary>
        /// [166차 E] 최장축 수평 자동 정규화 회전 산출(순수 함수 — 월드 bounds extents 입력).
        /// 이미 X장축(수평)이거나 비좁형(최장축이 차 2위 축의 ElongationRatio배 미만 — 헬멧 등)이면
        /// identity(원본 베이크 방향 유지). Y장축 → 롤 -90°(+Y→+X), Z장축 → 요 +90°(+Z→+X).
        /// 임계 2.0은 초기 휴리스틱 — 중간형인 방어구류(1.4~1.8배)가 오회전하지 않게 보수값(Play 트림 대상).
        /// </summary>
        public static Quaternion ComputeLongestAxisHorizontalRotation(Vector3 extents, float elongationRatio = 2.0f)
        {
            float longest = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            if (longest <= 0.0001f)
                return Quaternion.identity;   // 영 bounds — 원본 유지
            float second;
            if (extents.x >= extents.y && extents.x >= extents.z)
                second = Mathf.Max(extents.y, extents.z);
            else if (extents.y >= extents.x && extents.y >= extents.z)
                second = Mathf.Max(extents.x, extents.z);
            else
                second = Mathf.Max(extents.x, extents.y);
            if (longest < second * elongationRatio)
                return Quaternion.identity;   // 비좁형(또는 쌍장축 타이) — 원본 유지
            if (extents.x >= longest)
                return Quaternion.identity;   // 이미 수평
            if (extents.y >= longest)
                return Quaternion.Euler(0f, 0f, -90f);   // +Y → +X
            return Quaternion.Euler(0f, 90f, 0f);        // +Z → +X
        }

        /// <summary>
        /// [166차 E] 마운트 노드에 아이콘 방향 적용 — per-key 오버라이드(절대 오일러) 우선,
        /// 없으면 자동 최장축 수평 정규화. 자동은 월드 보정(correction * 현재회전) — 베이크 루트 회전이
        /// 있어도 월드축 기준으로 정렬. identity면 회전 없음(원본 베이크 방향 유지). 실패 시 원본 유지(폴백).
        /// </summary>
        private static void ApplyIconOrientation(string modelKey, GameObject node, Renderer[] rends)
        {
            try
            {
                if (node == null || rends == null || rends.Length == 0)
                    return;
                if (TryGetOrientationOverride(modelKey, out Vector3 euler))
                {
                    node.transform.rotation = Quaternion.Euler(euler);   // 절대 적용 — 수기 지정 각도가 최종
                    return;
                }
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                Quaternion correction = ComputeLongestAxisHorizontalRotation(b.extents);
                if (correction != Quaternion.identity)
                    node.transform.rotation = correction * node.transform.rotation;   // 월드축 보정
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GblItemIconRenderer] 방향 정규화 실패(원본 방향 유지): " + e.Message);
            }
        }

        /// <summary>
        /// 아이콘 프레이밍 공용 헬퍼 — [197차] 직교(orthographic) 전체핏으로 전면 대체: 반각/FOV/원근
        /// 후퇴 수식 폐지, orthographicSize = 최대 화면축 반경 × IconFillMargin(충전률 92% 목표 —
        /// 194차 1.25마진+퍼스펙티브 후퇴 대체, Z-깊은 물체의 원근 확대 절단 근원 자체 제거).
        /// [166차 E] 카메라 정면 고정(center + (0,0,-dist) — 사선 제거) 유지. TryMount/SyncBake 양 경로가
        /// 이 헬퍼 하나만 사용(아이콘 방향·프레이밍 통일 보장).
        /// </summary>
        private static void FrameIconCamera(Camera cam, Bounds b)
        {
            Vector3 center = b.center;
            // [197차] 직교 전환 — 원근 확대/절단이 성립하지 않아 194차 extZ 가산 로직 제거(직교는 깊이 무관).
            cam.orthographic = true;
            // 정사각 RT(aspect 1)라 수직 half = 수평 half — 최대 화면축 반경에 마진을 곱해 전체핏.
            cam.orthographicSize = Mathf.Max(b.extents.x, b.extents.y) * IconFillMargin;
            // [166차 E→197차] 위치 계약 유지 — dist는 직교 깊이용 최소 거리(extZ+1)만 확보.
            float dist = b.extents.z + 1f;
            cam.transform.position = center + new Vector3(0f, 0f, -dist);
            cam.transform.LookAt(center);
            cam.nearClipPlane = 0.05f;   // [197차] 근접면(dist-extZ=1)을 여유 있게 포함
            cam.farClipPlane = dist + b.extents.z + 2f;   // 물체 후면(dist+extZ)보다 넉넉
        }

        // ================================================================
        //  마운트: 노드/카메라/RT 생성 (InventoryWindow.EnsurePreviewSetup 검증 패턴)
        // ================================================================

        private static bool TryMount(ref BakeJob job)
        {
            try
            {
                GameObject prefab = null;
                if (!string.IsNullOrEmpty(job.glbPath))
                {
#if UNITY_EDITOR
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(job.glbPath);
#endif
                }
                if (prefab == null)
                    prefab = Resources.Load<GameObject>("Models/UserProvided/" + job.modelKey);
                if (prefab == null)
                {
                    // [32차 진단] 마운트 실패 원인 가시화 — 경로/폴백 여부를 로그로(성공 시 무출력)
                    Debug.LogWarning($"[GblItemIconRenderer] 마운트 실패(모델 없음) — id={job.itemId} glbPath={job.glbPath ?? "null"} modelKey={job.modelKey}");
                    return false; // 모델 없음 — 폴백
                }

                job.node = Instantiate(prefab);
                // [165b차] 거치대 노드 은닉 — ItemHoldManager per-key HideNodeNames 재사용(아이콘에 거치대 포함 방지).
                //   직계 Find 미발견 시 이름 재귀 검색(GLB 중첩: propped-fishing-rod/rod/stick).
                foreach (var n in ItemHoldManager.GetHideNodeNames(job.modelKey) ?? System.Array.Empty<string>())
                    ItemHoldManager.FindHideNode(job.node.transform, n)?.gameObject.SetActive(false);
                job.node.name = "GblIconNode_" + job.modelKey;
                Vector3 remotePos = new Vector3(10000f, 1000f, 10000f); // 씬 물체와 안 겹치는 원격 위치
                job.node.transform.position = remotePos;

                var rends = job.node.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    // [166차 E] 방향 정규화 선행 — 최장축 수평(per-key 오버라이드 우선).
                    //   이후 스케일/바닥정렬/프레이밍은 전부 회전 후 bounds 재판독으로 정합.
                    ApplyIconOrientation(job.modelKey, job.node, rends);
                    // [172차] 왕실 요리 — 재료 카테고리 hue 틴트(dish_ 전용, white면 무동작, MPB만).
                    if (job.itemId != null && job.itemId.StartsWith(DishGlbCatalog.DishPrefix, System.StringComparison.Ordinal))
                        DishGlbCatalog.ApplyTint(job.node, DishGlbCatalog.TintFor(DishGlbCatalog.CatsFor(job.itemId)));
                    // [198차] per-item 아이콘 텍스처 오버라이드(테이블 등록분만 — MPB 텍스처 교체, 틴트와 공존).
                    ItemIconTextureOverrides.TryApply(job.itemId, job.node);

                    // 스케일 정규화: 최장축 ~1.8m로
                    //   [166차 E] 높이핏(size.y) → 최장축핏 일반화 — 정립(Y장축) 모델은 기존과 동일 결과,
                    //   X/Z장축 베이크는 높이핏에서 수십 배 폭발 → 정규화 전제에 맞는 목표축.
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    if (maxDim > 0.01f) job.node.transform.localScale *= 1.8f / maxDim;
                    var b2 = rends[0].bounds;
                    foreach (var r in rends) b2.Encapsulate(r.bounds);
                    job.node.transform.position += new Vector3(0f, remotePos.y - b2.min.y, 0f);
                }

                job.rt = new RenderTexture(BakeRTSize, BakeRTSize, 24, RenderTextureFormat.ARGB32); // [197차] 2× 슈퍼샘플 RT
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

                // 프레이밍: [142차 H] 수평/수직 반각 전체핏 + [166차 E] 정면 고정 — 공용 헬퍼(SyncBake와 통일)
                if (rends.Length > 0)
                {
                    var rb = rends[0].bounds;
                    foreach (var r in rends) rb.Encapsulate(r.bounds);
                    FrameIconCamera(job.cam, rb);
                    AttachIconLightRig(job.cam);   // [197차] 베이크 전용 3점 조명 릭
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
                // [197차] 2× 슈퍼샘플 베이크 — 512 RT → 256 박스다운샘플(MSAA 대체 경로).
                Texture2D copy = BakeFromRTDownsampled(job.rt, "GblIcon_" + job.modelKey);
                if (copy == null) return null;
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
