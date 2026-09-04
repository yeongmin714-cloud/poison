using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
using System.Collections;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// PNG 텍스처 기반 국가별 지형 텍스처 적용 시스템.
    /// Resources/Models/UserProvided/terrain/textures_idyllic/ 에서 PNG를 로드하여
    /// URP Lit Material로 변환, Ground MeshRenderer에 적용한다.
    /// NationTerrainController를 대체하여 동작한다.
    ///
    /// 지원 국가 접두사: east_, west_, south_, north_, empire_, dracula_, extra_
    /// (기존 textures/ 폴더는 회귀 폴백용으로 유지 — _textureResourcesPath 기본값 참고)
    /// </summary>
    public class TerrainTextureApplier : MonoBehaviour
    {
        [Header("Texture Resources")]
        // 2026-09: Idyllic Fantasy Nature PNG 텍스처 세트로 교체.
        // 기존 jpg 폴더(Models/UserProvided/terrain/textures/)는 회귀 시 폴백용으로 유지 —
        // 이 기본값을 되돌리면 기존 텍스처 세트로 복귀 가능. 국가 접두사 분류 로직은 동일 동작.
        [SerializeField] private string _textureResourcesPath = "Models/UserProvided/terrain/textures_idyllic/";

        [Header("Material Settings")]
        [SerializeField] private float _metallic = 0f;
        [SerializeField] private float _smoothness = 0.1f;
        [SerializeField] private float _textureTiling = 200f;

        [Header("Splatting (T-G2)")]
        [SerializeField] private bool _useSplatting = true;
        [SerializeField] private int _splatResolution = 2048;   // T-G4: 1024→2048 (격자 2m/px 뭉개짐 완화)
        [SerializeField] private int _splatSeed = 20260902;

        [Header("Runtime State")]
        [SerializeField] private NationType _currentNation = NationType.East;

        // Loaded textures by nation
        private Dictionary<NationType, List<Texture2D>> _nationTextures;
        private List<Texture2D> _extraTextures;

        // Created materials keyed by nation
        private Dictionary<NationType, Material> _nationMaterials;

        // Cached references
        private MeshRenderer _meshRenderer;
        private NationTerrainController _nationController;

        /// <summary>Current active nation material.</summary>
        public NationType CurrentNation => _currentNation;

        /// <summary>All created nation materials (readonly for tests).</summary>
        public IReadOnlyDictionary<NationType, Material> NationMaterials => _nationMaterials;

        // ================================================================
        //  T-G5: Ground_Grass_Mat._BaseMap 재발 방지 가드
        // ================================================================

        /// <summary>
        /// 에디터가 머티리얼을 재저장하며 _BaseMap을 null로 되돌리는 사고(09-02~03 4회)를
        /// 차단하기 위한 런타임/에디터 가드. Ground_Grass_Mat 3개 사본의 _BaseMap이 null이
        /// 면 Idyllic 잔디 알베도(east_grass1_albedo)를 자동 재할당한다.
        /// GameSetup 등에서도 재사용 가능하도록 public static.
        /// </summary>
        public static int EnsureGroundGrassBaseMap()
        {
            const string TX_PATH = "Models/UserProvided/terrain/textures_idyllic/east_grass1_albedo";
            const string MAT_RES_PATH = "URP/Ground_Grass_Mat";
            int restored = 0;

            Texture2D grass = null;
            try { grass = Resources.Load<Texture2D>(TX_PATH); }
            catch { grass = null; }
            if (grass == null)
            {
                // Resources 로드 실패 시 조용히 skip (예외 없음).
                return 0;
            }

            // 1) 런타임 접근 가능한 사본: Resources/URP/Ground_Grass_Mat
            Material resMat = Resources.Load<Material>(MAT_RES_PATH);
            if (resMat != null && resMat.GetTexture("_BaseMap") == null)
            {
                resMat.SetTexture("_BaseMap", grass);
                restored++;
            }

#if UNITY_EDITOR
            // 2) Resources 밖 사본들: 에디터 전용 AssetDatabase 경유 (실패 시 예외 없이 skip)
            string[] assetPaths =
            {
                "Assets/Materials/Ground_Grass_Mat.mat",
                "Assets/URP/Ground_Grass_Mat.mat",
            };
            foreach (string path in assetPaths)
            {
                try
                {
                    var editorMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (editorMat != null && editorMat.GetTexture("_BaseMap") == null)
                    {
                        editorMat.SetTexture("_BaseMap", grass);
                        UnityEditor.EditorUtility.SetDirty(editorMat);
                        restored++;
                    }
                }
                catch (System.Exception)
                {
                    // 실패해도 예외 없이 skip
                }
            }
#endif

            if (restored > 0)
            {
                Debug.LogWarning($"[TerrainTextureApplier] ⚠️ Ground_Grass_Mat._BaseMap null 감지 → east_grass1_albedo 자동 복구 ({restored}개)");
            }
            return restored;
        }

        // ================================================================
        //  T-G5-W2: 런타임 인스턴스 복제 가드 (에디터 재저장 사고 근본 차단)
        // ================================================================
        // 에셋 파일을 고치지 않고 씬의 MeshRenderer에 INSTANCE(런타임 복제) 머티리얼을
        // 직접 심어 에디터가 나중에 에셋을 재저장하며 _BaseMap을 null로 되돌려도
        // 렌더에 영향이 없도록 한다. (에디터는 런타임 인스턴스에 손대지 못함)
        private const string GROUND_GRASS_TEX_PATH = "Models/UserProvided/terrain/textures_idyllic/east_grass1_albedo";
        private static Texture2D _cachedGroundGrassTex;

        /// <summary>east_grass1 알베도를 캐시하여 반환. 실패 시 null.</summary>
        private static Texture2D GetGroundGrassTexture()
        {
            if (_cachedGroundGrassTex == null)
            {
                try { _cachedGroundGrassTex = Resources.Load<Texture2D>(GROUND_GRASS_TEX_PATH); }
                catch { _cachedGroundGrassTex = null; }
            }
            return _cachedGroundGrassTex;
        }

        /// <summary>머티리얼 명에 담긴 국가 접두사에 맞는 대표 지면 텍스처 경로(스플랫 폴백용).</summary>
        private static string GetNationRepTexturePath(string matName)
        {
            string n = matName ?? "";
            if (n.IndexOf("West", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Models/UserProvided/terrain/textures_idyllic/west_dirt_albedo";
            if (n.IndexOf("North", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Models/UserProvided/terrain/textures_idyllic/north_grass_albedo";
            if (n.IndexOf("South", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Models/UserProvided/terrain/textures_idyllic/south_dirt_albedo";
            if (n.IndexOf("Empire", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Models/UserProvided/terrain/textures_idyllic/empire_cobble_albedo";
            if (n.IndexOf("Dracula", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Models/UserProvided/terrain/textures_idyllic/east_grass2_albedo";
            return GROUND_GRASS_TEX_PATH;   // default 국가 대표 잔디
        }

        /// <summary>머티리얼의 _BaseMap이 파손(null 또는 빌트인 흰색/Default-Diffuse)인지 판별.</summary>
        private static bool IsBaseMapBroken(Material m)
        {
            if (m == null) return false;
            Texture tex = null;
            try { tex = m.GetTexture("_BaseMap"); } catch { tex = null; }
            if (tex == null) return true;
            string name = tex.name;
            return string.IsNullOrEmpty(name)
                   || name.IndexOf("White", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("Default-Diffuse", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 씬 내 모든 MeshRenderer의 지면 머티리얼(Ground_Grass* / 외곽 Ground 평면 / Terrain_*_Mat)을
        /// 런타임 INSTANCE로 복제해 _BaseMap이 파손됐으면 알베도를 재할당한다.
        /// 에셋 파일은 절대 수정하지 않으므로 에디터 재저장과 무관하게 렌더가 보장된다.
        /// </summary>
        public static int FixGroundMaterialsRuntime()
        {
            Texture2D grass = GetGroundGrassTexture();
            if (grass == null) return 0;   // Resources 로드 실패 시 조용히 skip

            int repaired = 0;
            MeshRenderer[] renderers;
            try { renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None); }
            catch { renderers = null; }
            if (renderers == null || renderers.Length == 0) return 0;

            foreach (MeshRenderer r in renderers)
            {
                if (r == null) continue;
                MeshRenderer rm = r;   // 로컬 참조 (런타임 스트립트 무결성 유지용)
                Material[] shared = rm.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;

                for (int i = 0; i < shared.Length; i++)
                {
                    Material m = shared[i];
                    if (m == null) continue;
                    string name = m.name == null ? "" : m.name;

                    // 대상 판별: (a) Ground_Grass* 머티리얼, (b) 메시 없는 외곽 Ground 평면,
                    // (c) Terrain_*_Mat 스플랫 머티리얼
                    bool isGroundGrass = name.IndexOf("Ground_Grass", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isOuterPlane = name.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0 && rm.sharedMesh == null;
                    bool isSplatMat = name.IndexOf("Terrain_", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!isGroundGrass && !isOuterPlane && !isSplatMat) continue;
                    if (!IsBaseMapBroken(m)) continue;

                    Texture2D fallbackTex = isSplatMat
                        ? (Resources.Load<Texture2D>(GetNationRepTexturePath(name)) ?? grass)
                        : grass;
                    if (fallbackTex == null) continue;

                    // ── 런타임 INSTANCE 복제 (에셋 파일 미수정) ──
                    Material clone = null;
                    try
                    {
                        Shader shader = m.shader != null ? m.shader : Shader.Find("Universal Render Pipeline/Lit");
                        clone = new Material(shader != null ? shader : Shader.Find("Standard"));
                        clone.name = name + "_Runtime";
                        clone.CopyPropertiesFromMaterial(m);       // _BaseColor(원본 녹색) 등 보존
                        clone.SetTexture("_BaseMap", fallbackTex);
                        if (clone.HasProperty("_MainTex")) clone.SetTexture("_MainTex", fallbackTex);
                        // 스플랫은 전 세계 매핑, 일반 지면은 원본 타일링 유지
                        if (isSplatMat)
                        {
                            clone.mainTextureScale = Vector2.one;
                            clone.mainTextureOffset = Vector2.zero;
                        }
                    }
                    catch (System.Exception)
                    {
                        if (clone != null) Object.Destroy(clone);
                        continue;
                    }
                    if (clone == null) continue;

                    // 해당 슬롯만 인스턴스로 교체 (다른 슬롯/에셋 무관)
                    try { rm.materials[i] = clone; } catch { Object.Destroy(clone); continue; }
                    repaired++;
                }
            }

            if (repaired > 0)
            {
                Debug.LogWarning($"[TerrainTextureApplier] 🔧 런타임 지면 머티리얼 복구: {repaired}개 (인스턴스 방식)");
            }
            return repaired;
        }

        // ================================================================
        //  Unity Lifecycle
        // ================================================================

        private void Awake()
        {
            // Disable NationTerrainController if present
            _nationController = GetComponent<NationTerrainController>();
            if (_nationController != null)
            {
                _nationController.enabled = false;
                Debug.Log("[TerrainTextureApplier] NationTerrainController disabled.");
            }

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
            {
                Debug.LogError("[TerrainTextureApplier] No MeshRenderer found on Ground.");
                return;
            }

            // 지연 로딩: 텍스처 로드는 Start() 또는 첫 UpdateForPosition() 호출 시 수행
            // Awake에서는 간단한 fallback만 적용
            _nationTextures = new Dictionary<NationType, List<Texture2D>>();
            _extraTextures = new List<Texture2D>();
            _nationMaterials = new Dictionary<NationType, Material>();
            Debug.Log("[TerrainTextureApplier] Awake: 지연 로딩 모드 (텍스처는 Start에서 로드)");
        }

        private void Start()
        {
            // T-G5: 에디터가 머티리얼 재저장하며 _BaseMap을 null로 되돌리는 사고(09-02~03 4회)를 영구 차단.
            EnsureGroundGrassBaseMap();

            // Start에서 텍스처 로드 — Awake 블로킹 방지
            LoadTextures();
            CreateMaterials();
            if (_nationMaterials.Count > 0)
            {
                ApplyMaterialForNation(_currentNation);
            }

            // === Phase 1 진단: 지형/콜라이더/착지 실제 상태 숫자로 확정 ===
            DiagnoseGroundState();
        }

        /// <summary>지형 메시/콜라이더/스폰 지점 충돌 상태를 로그로 출력해 근본 원인 확정.</summary>
        private void DiagnoseGroundState()
        {
            GameObject ground = gameObject;
            var mf = ground.GetComponent<MeshFilter>();
            var mr = ground.GetComponent<MeshRenderer>();
            var mc = ground.GetComponent<Collider>();

            Debug.Log("[DiagP1] ===== 지형 상태 진단 ===== ");
            // 0) 실제 렌더 재질/알베도 — 지면이 회색인 원인 확정
            var mrr = ground.GetComponent<MeshRenderer>();
            if (mrr != null && mrr.sharedMaterial != null)
            {
                var mm = mrr.sharedMaterial;
                Texture2D albedo = null;
                try { albedo = mm.HasProperty("_BaseMap") ? (Texture2D)mm.GetTexture("_BaseMap") : null; }
                catch { albedo = null; }
                Debug.Log($"[DiagP1] 실제재질='{mm.name}' shader='{mm.shader?.name}' _BaseMap={(albedo != null ? albedo.name : "NULL")}");
            }
            else
            {
                Debug.Log($"[DiagP1] MeshRenderer/Material 없음 (mr={(mrr != null ? "있음" : "없음")})");
            }
            // 1) 메시/콜라이더 일치 여부
            string mfName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "NULL";
            string mcMesh = mc is MeshCollider mmc && mmc.sharedMesh != null ? mmc.sharedMesh.name : (mc != null ? mc.GetType().Name+"(noMesh)" : "NULL");
            int mfVerts = mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : -1;
            Debug.Log($"[DiagP1] MeshFilter={mfName} vtx={mfVerts} bounds={(mf?.sharedMesh != null ? mf.sharedMesh.bounds.ToString() : "NULL")}");
            Debug.Log($"[DiagP1] Collider={mc?.GetType().Name} enabled={mc?.enabled} sharedMesh={mcMesh}");

            // 2) 지형 높이 (스폰 지점)
            Vector3 spawn = ProjectName.Core.PlayerSpawnConfig.SpawnPosition;
            float h = ProjectName.Systems.TerrainGenerator.GetHeightAt(spawn.x, spawn.z, ProjectName.Core.Data.BiomeType.Plains, 42);
            Debug.Log($"[DiagP1] 스폰({spawn.x:F0},{spawn.z:F0}) 지형높이={h:F2} +Ground1={h+1f:F2}");

            // 3) 스폰 지점 아래 RaycastAll → 지형 콜라이더가 물리 세계에 있는지 전체 나열
            // (지형 증폭으로 표면이 높아졌으므로 계산된 표면 위 30m에서 60m 하향 캐스트)
            Vector3 o = new Vector3(spawn.x, h + 1f + 30f, spawn.z);
            RaycastHit[] allHits = Physics.RaycastAll(o, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            Debug.Log($"[DiagP1] 스폰상공 RaycastAll(60m, 표면+30m에서) 히트수={allHits.Length}");
            bool terrainColliderFound = false;
            foreach (var hh in allHits)
            {
                if (hh.collider == null) continue;
                bool isTerrain = hh.collider.gameObject.name.Contains("Ground");
                if (isTerrain) terrainColliderFound = true;
                Debug.Log($"[DiagP1]   ↓ hit: {hh.collider.gameObject.name} y={hh.point.y:F2} layer={hh.collider.gameObject.layer}");
            }
            Debug.Log($"[DiagP1] ★ 지형콜라이더(Ground*) 존재={terrainColliderFound}  (False면 지형 콜라이더 파손 → Phase B)");

            // 3-2) 플레이어 앞 지면 지점(화면에 보이는 회색 지면)에 뭐가 있는지
            // (표면 위 30m에서 시작 — 증폭 지형에서도 항상 지형 위에서 시작 보장)
            Vector3 probePoint = new Vector3(spawn.x + 6f, h + 1f + 30f, spawn.z + 6f);
            bool rc2 = Physics.Raycast(probePoint, Vector3.down, out RaycastHit hit2, 60f, ~0, QueryTriggerInteraction.Ignore);
            if (rc2)
            {
                var mrAt = hit2.collider != null ? hit2.collider.GetComponent<MeshRenderer>() : null;
                string matAt = mrAt != null && mrAt.sharedMaterial != null ? mrAt.sharedMaterial.name : "(재질없음)";
                Debug.Log($"[DiagP1] 전방지면({spawn.x + 6f:F0},{spawn.z + 6f:F0}) 아래 hit: {hit2.collider?.gameObject.name} y={hit2.point.y:F2} 재질={matAt}");
            }
            else
            {
                Debug.LogWarning("[DiagP1] 전방지면 아래 20m에 콜라이더 없음 → 회색은 배경/허공");
            }

            // 4) 지형 위 서기 체크용 — 지형 표면 상대
            Debug.Log($"[DiagP1] 지형 표면 세계y={h+1f:F2} (스폰플레이어y={spawn.y:F2})");

            // 5) 카메라 위치/회전 — 지형이 "위"에 보이는지(카메라가 지형 아래에서 위를 보는지) 판별
            var cam = Camera.main;
            if (cam != null)
            {
                Debug.Log($"[DiagP1] Camera '{cam.name}' worldPos=({cam.transform.position.x:F1},{cam.transform.position.y:F1},{cam.transform.position.z:F1}) euler=({cam.transform.eulerAngles.x:F1},{cam.transform.eulerAngles.y:F1},{cam.transform.eulerAngles.z:F1})");
                // 지형 표면(1+g)보다 카메라가 아래인지
                Debug.Log($"[DiagP1] 카메라가 지형표면({h+1f:F2})보다 {(cam.transform.position.y < h+1f ? "아래 → 위를 봄(지형이 천장)" : "위 → 아래를 봄(정상)")}");
            }
            else
            {
                Debug.LogWarning("[DiagP1] Main Camera 없음");
            }

            // 6) 플레이어 오브젝트 위치 (캐슬을 찾아)
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Debug.Log($"[DiagP1] Player at ({player.transform.position.x:F1},{player.transform.position.y:F1},{player.transform.position.z:F1})");
            }
            // === Phase B: 지형 메시 재표본 + 조건부 와인딩 반전 ===
            // 씬에 베이크된 Ground_Inner 메시는 진폭 증폭/호수 분지 카브 이전에 구워져 굴곡이 빠져 있다.
            // 모든 정점 높이를 TerrainGenerator.GetHeightAt으로 재표본해 굴곡+호수 분지를 반영하고,
            // 첫 삼각형 법선이 아래(-Y)일 때만 인덱스를 뒤집어 +Y로 세운다.
            var mcFix = ground.GetComponent<MeshCollider>();
            var mfFix = ground.GetComponent<MeshFilter>();
            if (mfFix != null && mfFix.sharedMesh != null && mfFix.sharedMesh.isReadable)
            {
                var meshFix = mfFix.sharedMesh;
                Vector3 groundPos = ground.transform.position;

                // 1) 정점 재표본 — 메시는 월드 XZ 좌표(중심 0)로 베이크되어 있고 Ground가 (0,?,0)에
                //    있으므로 월드 = 로컬 + 오브젝트 위치. GetHeightAt은 월드 원점 기준 0-높이를
                //    반환하므로 로컬 y = GetHeightAt + 1 - groundPos.y 로 세계 표면(=GetHeightAt+1)과 일치.
                var verts = meshFix.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    float worldX = verts[i].x + groundPos.x;
                    float worldZ = verts[i].z + groundPos.z;
                    verts[i].y = ProjectName.Systems.TerrainGenerator.GetHeightAt(
                        worldX, worldZ, ProjectName.Core.Data.BiomeType.Plains, 42) + 1f - groundPos.y;
                }
                meshFix.vertices = verts;
                meshFix.RecalculateNormals();

                // 2) 조건부 와인딩: 첫 삼각형 법선 Dot(normal, Vector3.up) < 0 일 때만 인덱스 반전
                //    (무조건 반전은 TerrainGenerator가 이미 +Y 와인딩으로 구우면 이중 반전 버그를 일으킴)
                var tris = meshFix.triangles;
                bool needsFlip = false;
                if (tris.Length >= 3)
                {
                    Vector3 a = verts[tris[0]];
                    Vector3 b = verts[tris[1]];
                    Vector3 c = verts[tris[2]];
                    Vector3 nrm = Vector3.Cross(b - a, c - a);
                    needsFlip = Vector3.Dot(nrm, Vector3.up) < 0f;
                    if (needsFlip)
                    {
                        for (int i = 0; i < tris.Length; i += 3)
                        {
                            (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]); // 마지막 두 인덱스 교환 = 와인딩 반전
                        }
                        meshFix.triangles = tris;
                        meshFix.RecalculateNormals();
                    }
                    Debug.Log($"[DiagP1] 재표본+와인딩: vtx={verts.Length} flip={needsFlip}");
                }
                meshFix.RecalculateBounds();

                // MeshCollider 재동기화 (파괴된 메시/구 와인딩 쿠킹 해제)
                if (mcFix != null)
                {
                    mcFix.sharedMesh = null;
                    mcFix.sharedMesh = meshFix;
                    mcFix.enabled = false;
                    mcFix.enabled = true;
                }
                Physics.SyncTransforms();

                // 검증: 지형 위에서 아래로 raycast → 이제 Ground_Inner가 잡혀야 함
                Vector3 testO = new Vector3(spawn.x, 10f, spawn.z);
                bool reHit = Physics.Raycast(testO, Vector3.down, out RaycastHit reH, 20f, ~0, QueryTriggerInteraction.Ignore);
                Debug.Log($"[DiagP1] ★ 재표본+와인딩 후 raycast={reHit} 대상={(reHit ? reH.collider?.gameObject.name : "여전히 없음")} y={(reHit ? reH.point.y.ToString("F2") : "-")}");
            }
            else
            {
                Debug.LogWarning($"[DiagP1] 재표본 픽스 스킵: mesh readable={mfFix?.sharedMesh?.isReadable} mfFix={mfFix != null}");
            }

            Debug.Log("[DiagP1] ===== 진단 끝 =====");
        }
 
        private void OnDestroy()
        {
            // Cleanup created materials to prevent memory leaks
            if (_nationMaterials != null)
            {
                foreach (var kvp in _nationMaterials)
                {
                    if (kvp.Value != null)
                    {
                        if (Application.isPlaying)
                            Destroy(kvp.Value);
                        else
                            DestroyImmediate(kvp.Value);
                    }
                }
                _nationMaterials.Clear();
            }
        }

        // ================================================================
        //  Texture Loading
        // ================================================================

        /// <summary>
        /// Loads all PNG textures from the resources path and categorizes
        /// them by nation prefix (east_, west_, south_, north_, empire_, dracula_, extra_).
        /// </summary>
        public void LoadTextures()
        {
            _nationTextures = new Dictionary<NationType, List<Texture2D>>();
            _extraTextures = new List<Texture2D>();

            Texture2D[] allTextures = Resources.LoadAll<Texture2D>(_textureResourcesPath);
            if (allTextures == null || allTextures.Length == 0)
            {
                Debug.LogWarning("[TerrainTextureApplier] No textures found at: " + _textureResourcesPath);
                return;
            }

            foreach (Texture2D tex in allTextures)
            {
                if (tex == null) continue;

                string lowerName = tex.name.ToLowerInvariant();

                if (lowerName.StartsWith("east_"))
                    AddToNation(NationType.East, tex);
                else if (lowerName.StartsWith("west_"))
                    AddToNation(NationType.West, tex);
                else if (lowerName.StartsWith("south_"))
                    AddToNation(NationType.South, tex);
                else if (lowerName.StartsWith("north_"))
                    AddToNation(NationType.North, tex);
                else if (lowerName.StartsWith("empire_"))
                    AddToNation(NationType.Empire, tex);
                else if (lowerName.StartsWith("dracula_"))
                    AddToNation(NationType.Dracula, tex);
                else if (lowerName.StartsWith("extra_") || lowerName.StartsWith("extra"))
                    _extraTextures.Add(tex);
                else
                    Debug.Log($"[TerrainTextureApplier] Unrecognized texture prefix: {tex.name}");
            }

            Debug.Log($"[TerrainTextureApplier] Loaded {allTextures.Length} textures. " +
                      $"East={(_nationTextures.ContainsKey(NationType.East) ? _nationTextures[NationType.East].Count : 0)}, " +
                      $"West={(_nationTextures.ContainsKey(NationType.West) ? _nationTextures[NationType.West].Count : 0)}, " +
                      $"South={(_nationTextures.ContainsKey(NationType.South) ? _nationTextures[NationType.South].Count : 0)}, " +
                      $"North={(_nationTextures.ContainsKey(NationType.North) ? _nationTextures[NationType.North].Count : 0)}, " +
                      $"Empire={(_nationTextures.ContainsKey(NationType.Empire) ? _nationTextures[NationType.Empire].Count : 0)}, " +
                      $"Dracula={(_nationTextures.ContainsKey(NationType.Dracula) ? _nationTextures[NationType.Dracula].Count : 0)}, " +
                      $"Extra={_extraTextures.Count}");
        }

        private void AddToNation(NationType nation, Texture2D tex)
        {
            if (!_nationTextures.ContainsKey(nation))
                _nationTextures[nation] = new List<Texture2D>();
            _nationTextures[nation].Add(tex);
        }

        // ================================================================
        //  Material Creation
        // ================================================================

        /// <summary>
        /// Creates URP Lit materials for each nation using loaded textures.
        /// Material naming: "Terrain_{nation}_Mat"
        /// T-G2: 멀티레이어 스플랫 — 국가별 텍스처 목록으로 높이/경사/노이즈 기반
        /// 스플랫 맵을 베이크하여 _BaseMap에 적용. 베이크 실패 시 기존 단일 텍스처 폴백.
        /// </summary>
        public void CreateMaterials()
        {
            _nationMaterials = new Dictionary<NationType, Material>();

            if (_nationTextures == null)
            {
                Debug.LogError("[TerrainTextureApplier] _nationTextures is null. Call LoadTextures() first.");
                return;
            }

            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North, NationType.Empire, NationType.Dracula })
            {
                if (!_nationTextures.ContainsKey(nation) || _nationTextures[nation].Count == 0)
                {
                    if (nation != NationType.Dracula)
                    {
                        Debug.LogWarning($"[TerrainTextureApplier] No textures for {nation}. Skipping material.");
                    }
                    continue;
                }

                Texture2D mainTex = _nationTextures[nation][0];
                Material mat = CreateLitMaterial($"Terrain_{nation}_Mat", mainTex);

                // === 멀티레이어 스플랫 (T-G2): 단일 텍스처 → 높이/경사/노이즈 블렌드 스플랫 맵 ===
                if (_useSplatting)
                {
                    Texture2D splat = TryBakeSplat(nation, _nationTextures[nation]);
                    if (splat != null)
                    {
                        mat.SetTexture("_BaseMap", splat);
                        mat.mainTexture = splat;
                        mat.mainTextureScale = Vector2.one;   // 스플랫 맵은 전 세계 매핑(타일 1)
                        mat.mainTextureOffset = Vector2.zero;

                        // 근거리 미세 텍스처 복원: 스플랫 저해상도 뭉개짐을 URP DetailAlbedoMap으로 보완
                        // (Phase T-R3: 5레이어 팔레트에서 대표 잔디/초원/숲 텍스처를 우선 선택)
                        var natList = _nationTextures[nation];
                        Texture2D detailTex = PickDetailTexture(natList);
                        if (detailTex == null) detailTex = natList.Count > 0 ? natList[0] : null;
                        if (detailTex != null)
                        {
                            mat.SetTexture("_DetailAlbedoMap", detailTex);
                            mat.SetTextureScale("_DetailAlbedoMap", Vector2.one * 45f);   // T-G4: 60→45 (디테일 빈도 감소, 2048 스플랫과 균형)
                            mat.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
                            mat.EnableKeyword("_DETAIL_MULX2");
                            mat.SetFloat("_DetailNormalMapScale", 1f);
                        }
                        bool detailOK = mat.GetTexture("_DetailAlbedoMap") != null && mat.IsKeywordEnabled("_DETAIL_MULX2");
                        Debug.Log($"[TerrainTextureApplier] {nation} 스플랫 맵 적용: {splat.name} + 디테일알베도({(detailTex != null ? detailTex.name : "NULL")}) | detailOK={detailOK} (detailTex={mat.GetTexture("_DetailAlbedoMap") != null}, kw={mat.IsKeywordEnabled("_DETAIL_MULX2")})");
                    }
                    else
                    {
                        Debug.LogWarning($"[TerrainTextureApplier] {nation} 스플랫 실패 → 단일 텍스처 폴백.");
                    }
                }

                _nationMaterials[nation] = mat;
            }

            Debug.Log($"[TerrainTextureApplier] Created {_nationMaterials.Count} nation materials.");
        }

        /// <summary>스플랫 맵 베이크 시도 — 실패 시 null 반환(기존 단일 텍스처 폴백 트리거).</summary>
        private Texture2D TryBakeSplat(NationType nation, List<Texture2D> textures)
        {
            try
            {
                var readable = new List<Texture2D>();
                foreach (Texture2D t in textures)
                {
                    if (t == null) continue;
                    readable.Add(IsTextureReadable(t) ? t : MakeReadableCopy(t));
                }
                if (readable.Count == 0) return null;
                return TerrainSplatBaker.BakeSplatMap(nation, readable, _splatResolution, _splatSeed);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TerrainTextureApplier] Splat bake exception for {nation}: {e.Message}");
                return null;
            }
        }

        private static bool IsTextureReadable(Texture2D t)
        {
            if (t == null) return false;
            try { t.GetPixel(0, 0); return true; } catch { return false; }
        }

        /// <summary>
        /// Phase T-R3: 5레이어 팔레트에서 근거리 디테일 알베도로 쓸 대표 지면 텍스처 선택.
        /// 잔디/초원/숲(저지대·중지대) 이름을 우선해 평지 대표 질감을 복원한다.
        /// </summary>
        private static Texture2D PickDetailTexture(List<Texture2D> list)
        {
            if (list == null || list.Count == 0) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || string.IsNullOrEmpty(t.name)) continue;
                string ln = t.name.ToLowerInvariant();
                if (ln.Contains("grass") || ln.Contains("meadow") || ln.Contains("forest"))
                    return t;
            }
            return list.Count > 1 ? list[1] : list[0];
        }

        /// <summary>비읽기 텍스처를 RenderTexture 경유로 읽기 가능한 RGBA32 복사본으로 변환.</summary>
        private Texture2D MakeReadableCopy(Texture2D src)
        {
            RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            copy.wrapMode = TextureWrapMode.Repeat;
            copy.name = src.name + "_readable";
            return copy;
        }

        private Material CreateLitMaterial(string name, Texture2D mainTex)
        {
            if (mainTex == null)
            {
                Debug.LogError($"[TerrainTextureApplier] mainTex is null for material '{name}'. Using fallback.");
                return CreateFallbackMaterial(name);
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                Debug.LogWarning("[TerrainTextureApplier] URP Lit shader not found, falling back to Standard.");
            }

            Material mat = new Material(shader);
            mat.name = name;

            // Assign main texture
            if (shader.name.Contains("Universal Render Pipeline/Lit"))
            {
                mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", _metallic);
                mat.SetFloat("_Smoothness", _smoothness);
            }
            else
            {
                mat.mainTexture = mainTex;
                mat.SetFloat("_Metallic", _metallic);
                mat.SetFloat("_Glossiness", _smoothness);
            }

            mat.mainTextureScale = Vector2.one * _textureTiling;

            return mat;
        }

        /// <summary>
        /// Creates a fallback material when the primary texture is null.
        /// Uses a plain white material with the correct shader.
        /// </summary>
        private static Material CreateFallbackMaterial(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = name + "_Fallback";
            mat.SetColor("_BaseColor", Color.magenta);
            mat.SetColor("_Color", Color.magenta);
            Debug.LogWarning($"[TerrainTextureApplier] Created fallback material '{mat.name}'.");
            return mat;
        }

        private void ApplyExtraTexture(Material mat, Texture2D extraTex, float blendStrength, string label)
        {
            if (extraTex == null) return;

            // For URP Lit, we can use the material's second texture slot or blend via color
            // Simple approach: blend texture into the material's main texture by averaging
            // In a more advanced approach, we'd add a secondary texture property
            if (mat.shader.name.Contains("Universal Render Pipeline/Lit"))
            {
                // Try to use a detail/albedo or blend property
                // If the shader supports it, set detail texture
                if (mat.HasProperty("_DetailAlbedoMap"))
                {
                    mat.SetTexture("_DetailAlbedoMap", extraTex);
                    mat.SetFloat("_DetailAlbedoMapScale", blendStrength);
                }
            }

            Debug.Log($"[TerrainTextureApplier] Applied {label} to {mat.name}");
        }

        // ================================================================
        //  Material Application
        // ================================================================

        /// <summary>
        /// Applies the material for the given nation to the Ground MeshRenderer.
        /// </summary>
        /// <param name="nation">Target nation type</param>
        public void ApplyMaterialForNation(NationType nation)
        {
            if (_meshRenderer == null)
            {
                Debug.LogError("[TerrainTextureApplier] MeshRenderer not available.");
                return;
            }

            if (_nationMaterials == null || !_nationMaterials.ContainsKey(nation))
            {
                Debug.LogWarning($"[TerrainTextureApplier] No material for {nation}. Using fallback.");
                return;
            }

            _currentNation = nation;
            Material mat = _nationMaterials[nation];
            _meshRenderer.sharedMaterial = mat;
            // 멀티레이어 스플랫 맵(TerrainTextureApplier 생성, 이름 'Splat_...')이면 전 세계 매핑(타일1) 유지.
            // 폴백한 단일 텍스처는 기존처럼 200 타일링.
            if (_useSplatting && mat.mainTexture != null && mat.mainTexture.name.StartsWith("Splat_"))
            {
                mat.mainTextureScale = Vector2.one;
                mat.mainTextureOffset = Vector2.zero;
            }
            else
            {
                mat.mainTextureScale = Vector2.one * _textureTiling;
            }

            Debug.Log($"[TerrainTextureApplier] Applied material '{mat.name}' for {nation}.");
        }

        /// <summary>
        /// Updates the terrain material based on world position
        /// using NationTerrainController.GetNationFromPosition.
        /// </summary>
        /// <param name="worldPos">Player or camera world position</param>
        public void UpdateForPosition(Vector3 worldPos)
        {
            NationType nation = NationTerrainController.GetNationFromPosition(worldPos);
            if (nation != _currentNation && _nationMaterials.ContainsKey(nation))
            {
                ApplyMaterialForNation(nation);
            }
        }

        // ================================================================
        //  Public Query Methods (for tests)
        // ================================================================

        /// <summary>Number of nation textures loaded.</summary>
        public int NationTextureCount(NationType nation)
        {
            return _nationTextures != null && _nationTextures.ContainsKey(nation)
                ? _nationTextures[nation].Count
                : 0;
        }

        /// <summary>Number of extra textures loaded.</summary>
        public int ExtraTextureCount => _extraTextures?.Count ?? 0;

        /// <summary>Whether a material exists for the given nation.</summary>
        public bool HasMaterialFor(NationType nation)
        {
            return _nationMaterials != null && _nationMaterials.ContainsKey(nation);
        }
    }
}