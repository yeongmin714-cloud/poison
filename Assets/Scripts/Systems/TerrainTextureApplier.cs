using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
using System.Collections;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// PNG 텍스처 기반 국가별 지형 텍스처 적용 시스템.
    /// Resources/Models/UserProvided/terrain/textures/ 에서 PNG를 로드하여
    /// URP Lit Material로 변환, Ground MeshRenderer에 적용한다.
    /// NationTerrainController를 대체하여 동작한다.
    ///
    /// 지원 국가 접두사: east_, west_, south_, north_, empire_, dracula_, extra_
    /// </summary>
    public class TerrainTextureApplier : MonoBehaviour
    {
        [Header("Texture Resources")]
        [SerializeField] private string _textureResourcesPath = "Models/UserProvided/terrain/textures/";

        [Header("Material Settings")]
        [SerializeField] private float _metallic = 0f;
        [SerializeField] private float _smoothness = 0.1f;
        [SerializeField] private float _textureTiling = 200f;

        [Header("Splatting (T-G2)")]
        [SerializeField] private bool _useSplatting = true;
        [SerializeField] private int _splatResolution = 1024;
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
                        var natList = _nationTextures[nation];
                        Texture2D detailTex = natList.Count > 1 ? natList[1] : natList[0];
                        mat.SetTexture("_DetailAlbedoMap", detailTex);
                        mat.SetTextureScale("_DetailAlbedoMap", Vector2.one * 60f);
                        mat.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
                        mat.EnableKeyword("_DETAIL_MULX2");
                        mat.SetFloat("_DetailNormalMapScale", 1f);
                        Debug.Log($"[TerrainTextureApplier] {nation} 스플랫 맵 적용: {splat.name} + 디테일알베도({detailTex.name})");
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