using System.Collections;
using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 청크 기반 런타임 지형 생성 파이프라인 (Ring1 1450m 커버 확장).
    /// ±1600m를 400m 청크 8×8=64개로 생성한다(4m/쿼드, MeshCollider 포함).
    ///
    /// 높이 규약: 지표면 y = 1f + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42)
    /// — Ground_Inner/TerritoryBuilder/PlayerMovement와 동일 규약이라 봉합면 높이 오차 없음.
    ///
    /// UV: Ground_Inner 메시의 uv를 런타임에 읽어 동일 스케일/오프셋의 affine 매핑
    /// (u = s*worldX + o)으로 월드 연속을 보장. 읽기 실패 시 worldXZ/3200+0.5 폴백
    /// (±1600m 청크 전체 폭 = NationTerrainController 결합 방위 텍스처 스팬과 정합).
    ///
    /// 전 청크 완료(또는 플레이어가 서 있는 청크 완료 직후) Ground_Inner의
    /// MeshRenderer+MeshCollider만 비활성(z-fighting/이중 물리 방지) —
    /// 오브젝트 자체는 활성 유지(TerrainTextureApplier 등 컴포넌트 보존).
    /// </summary>
    public class RuntimeTerrainChunkManager : MonoBehaviour
    {
        [Header("Chunk Grid")]
        [SerializeField] private float chunkSize = 400f;   // 청크 한 변 (m)
        [SerializeField] private int vertsPerSide = 100;   // 청크 변 정점 수 (100×100 → 4m/쿼드)
        [SerializeField] private float worldHalf = 1600f;  // 월드 반경 → 8×8 = 64청크 (±1600m 커버)

        [Header("Static Grass (청크당 1드로우콜 병합 잔디)")]
        [SerializeField] private float grassSpacing = 4f;   // 잔디 그리드 간격(m) — 4f → 정점 격자 2칸 간격(50×50=2500지점/청크)
        [SerializeField] private float grassHeight = 0.6f;  // 터프 기본 높이 (실제 0.5~0.7m 랜덤 변동)
        [SerializeField] private float grassWidth = 0.8f;   // 터프 쿼드 폭

        private const float GroundBase = 1f;   // 지표면 기저 y (GetHeightAt + 1f)
        private const int Seed = 42;

        // 재진입 방지 플래그 (정적 — 중복 AddComponent/재Start 모두 차단)
        private static bool _builtOnce;

        // 정적 잔디 공유 머티리얼 캐시 (전 청크 1개 공유) + 청크당 터프 수 최초 1회 로그 플래그
        private static Material _grassMaterial;
        private static bool _grassLoggedOnce;

        private Transform _chunkRoot;
        private bool _innerDisabled;

        // Ground_Inner에서 수집한 기준 정보 (BuildChunks에서 설정)
        private int _groundLayer;
        private string _groundTag = "Untagged";
        private Material _sharedMaterial;
        private float _uScale = 1f / 3200f, _uOffset = 0.5f;   // 폴백: worldX/3200 + 0.5 (±1600m 정합)
        private float _vScale = 1f / 3200f, _vOffset = 0.5f;

        private void Start()
        {
            if (_builtOnce)
            {
                Debug.Log("[TerrainChunks] 이미 생성됨 — 스킵 (재진입 방지 플래그)");
                return;
            }
            // 씬에 이미 청크가 남아 있으면(재생/재부착) 스킵
            if (GameObject.Find("Ground_Chunk_0_0") != null)
            {
                _builtOnce = true;
                Debug.Log("[TerrainChunks] 기존 청크 감지 — 스킵 (재진입 방지)");
                return;
            }
            StartCoroutine(BuildChunks());
        }

        private IEnumerator BuildChunks()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // ── 0) Ground_Inner 기준 정보 수집 (레이어/태그/머티리얼/UV 매핑) ──
            GameObject inner = GameObject.Find("Ground_Inner");
            if (inner != null)
            {
                _groundLayer = inner.layer;
                _groundTag = string.IsNullOrEmpty(inner.tag) ? "Untagged" : inner.tag;

                var innerMr = inner.GetComponent<MeshRenderer>();
                if (innerMr != null && innerMr.sharedMaterial != null)
                {
                    // 원본 머티리얼을 공유 (복제하지 않음) — NationTerrainController의 국가별
                    // mainTexture 교체 / TerrainTextureApplier 변형이 전 청크에 그대로 전파되도록.
                    // 메시는 공유 금지(청크별 자체 메시 유지), 머티리얼만 원본 공유.
                    _sharedMaterial = innerMr.sharedMaterial;
                }

                var innerMf = inner.GetComponent<MeshFilter>();
                if (innerMf != null && innerMf.sharedMesh != null && innerMf.sharedMesh.isReadable)
                {
                    // T-B2 09-06: 결합 텍스처가 ±1600m 월드를 uv 0..1에 1:1 매핑하도록 변경됨
                    // (NationTerrainController — 방위 고정 색). Ground_Inner의 구 uv는 ±1000m 기준이라
                    // 역산하면 ±1600m 청크에서 uv 범위 초과(래핑/클램프 오색) → 역산 폐기, 고정 매핑 사용.
                    Debug.Log("[TerrainChunks] UV 고정 매핑 사용: worldXZ/3200+0.5 (결합 텍스처 ±1600 1:1 정합)");
                }
                else
                {
                    Debug.LogWarning("[TerrainChunks] Ground_Inner 메시 UV 미확보(isReadable=false?) → 고정 매핑 사용");
                }
            }
            else
            {
                Debug.LogWarning("[TerrainChunks] Ground_Inner 없음 — 기본 레이어/태그/UV 폴백으로 진행");
            }
            if (_sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                _sharedMaterial = new Material(shader) { color = new Color(0.4f, 0.6f, 0.3f) };
                Debug.LogWarning("[TerrainChunks] Ground_Inner 머티리얼 복제 실패 — URP Lit 폴백 사용");
            }

            // ── 1) 생성 순서 결정: 플레이어(없으면 원점)에서 가까운 청크부터 ──
            Vector3 origin = Vector3.zero;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) origin = player.transform.position;

            int gridCount = Mathf.Max(1, Mathf.RoundToInt(worldHalf * 2f / chunkSize)); // 8
            var order = new List<(int ix, int iz, float dist2)>(gridCount * gridCount);
            for (int iz = 0; iz < gridCount; iz++)
            {
                for (int ix = 0; ix < gridCount; ix++)
                {
                    float cx = -worldHalf + chunkSize * (ix + 0.5f);
                    float cz = -worldHalf + chunkSize * (iz + 0.5f);
                    float dx = cx - origin.x;
                    float dz = cz - origin.z;
                    order.Add((ix, iz, dx * dx + dz * dz));
                }
            }
            order.Sort((p, q) => p.dist2.CompareTo(q.dist2));

            // 플레이어가 서 있는 청크 (완료 즉시 Ground_Inner 비활성 트리거용)
            int playerCx = Mathf.Clamp(Mathf.FloorToInt((origin.x + worldHalf) / chunkSize), 0, gridCount - 1);
            int playerCz = Mathf.Clamp(Mathf.FloorToInt((origin.z + worldHalf) / chunkSize), 0, gridCount - 1);

            // ── 2) 순차 생성 (청크당 1프레임 yield — 부하 분산) ──
            _chunkRoot = new GameObject("Ground_Chunks").transform;

            int built = 0;
            long totalVerts = 0;
            foreach (var c in order)
            {
                BuildChunk(c.ix, c.iz);
                built++;
                totalVerts += (long)vertsPerSide * vertsPerSide;

                if (built % 8 == 0)
                    Debug.Log($"[TerrainChunks] {built}/{order.Count}");

                // 플레이어 청크 완료 또는 전 청크 완료 시 Ground_Inner 렌더/콜라이더 정리 (1회)
                if ((c.ix == playerCx && c.iz == playerCz) || built == order.Count)
                    DisableGroundInner();

                yield return null;
            }

            _builtOnce = true;
            Debug.Log($"[TerrainChunks] 완료: {built}청크 / 정점 {totalVerts:N0}개 / 소요 {sw.Elapsed.TotalSeconds:F1}초");
        }

        /// <summary>청크 1개 생성: 메시(100×100, GetHeightAt 기반) + 렌더러 + MeshCollider.</summary>
        private void BuildChunk(int ix, int iz)
        {
            float cx = -worldHalf + chunkSize * (ix + 0.5f);
            float cz = -worldHalf + chunkSize * (iz + 0.5f);
            int vps = vertsPerSide;
            float step = chunkSize / (vps - 1); // ≈4.04m/쿼드

            // 정점: 월드XZ = 청크중심±200, y = GetHeightAt(worldX, worldZ) + 1f
            var verts = new Vector3[vps * vps];
            var uvs = new Vector2[vps * vps];
            for (int j = 0; j < vps; j++)
            {
                float wz = cz - chunkSize * 0.5f + j * step;
                for (int i = 0; i < vps; i++)
                {
                    float wx = cx - chunkSize * 0.5f + i * step;
                    float h = TerrainGenerator.GetHeightAt(wx, wz, BiomeType.Plains, Seed);
                    int idx = j * vps + i;
                    // GO를 (cx,0,cz)에 두므로 로컬 y = 월드 y (로컬 XZ = 월드XZ - 청크중심)
                    verts[idx] = new Vector3(wx - cx, h + GroundBase, wz - cz);
                    uvs[idx] = new Vector2(_uScale * wx + _uOffset, _vScale * wz + _vOffset);
                }
            }

            // 삼각형: (a,c,b),(b,c,d) — 평탄면 기준 +Y 위향
            var tris = new int[(vps - 1) * (vps - 1) * 6];
            int t = 0;
            for (int j = 0; j < vps - 1; j++)
            {
                for (int i = 0; i < vps - 1; i++)
                {
                    int a = j * vps + i;   // (x0, z0)
                    int b = a + 1;         // (x1, z0)
                    int c = a + vps;       // (x0, z1)
                    int d = c + 1;         // (x1, z1)
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            }

            // 안전장치: 첫 삼각형 법선이 -Y(Dot<0)면 전체 와인딩 반전
            Vector3 va = verts[tris[0]];
            Vector3 vb = verts[tris[1]];
            Vector3 vc = verts[tris[2]];
            Vector3 n = Vector3.Cross(vb - va, vc - va);
            if (Vector3.Dot(n, Vector3.up) < 0f)
            {
                for (int i = 0; i < tris.Length; i += 3)
                {
                    (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                }
            }

            var mesh = new Mesh
            {
                name = $"Ground_Chunk_{ix}_{iz}_Mesh",
                indexFormat = verts.Length > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // 오브젝트: 이름은 "Ground" 포함(DiagP1 name.Contains("Ground") 인식 유지),
            // 레이어/태그는 Ground_Inner와 동일
            var go = new GameObject($"Ground_Chunk_{ix}_{iz}");
            go.layer = _groundLayer;
            try { go.tag = _groundTag; } catch { /* 태그 미정의 예외 무시 */ }
            go.transform.SetParent(_chunkRoot, false);
            go.transform.position = new Vector3(cx, 0f, cz);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _sharedMaterial;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            // 정적 병합 잔디 (청크당 1드로우콜, 전 지형 커버) — 지형 메시/콜라이더 로직 무변경
            AddStaticGrass(go, ix, iz, verts);
        }

        /// <summary>
        /// 청크 전면 정적 병합 잔디 (청크당 1드로우콜, 전 지형 커버).
        /// 청크 메시 정점 격자를 grassSpacing 기반 보간으로 샘플(기본: 정점 2칸 간격 = 50×50 = 2500지점/청크)하고,
        /// 지점당 교차 쿼드 2개(45° 차, 폭 grassWidth, 높이 0.5~0.7m 랜덤, Y회전 랜덤) 터프를
        /// 청크 로컬 좌표로 누적해 단일 병합 메시로 생성한다 (터프당 8정점/8트라이앵글).
        /// 호수 수면(lake.radius × 1.05) 영역은 제외. 콜라이더 없음, 그림자 off, 머티리얼 공유.
        /// System.Random(청크인덱스 기반 시드) 사용 — 실행마다 동일 결과(결정론).
        /// </summary>
        private void AddStaticGrass(GameObject chunkGO, int ix, int iz, Vector3[] chunkVerts)
        {
            int vps = vertsPerSide;
            float step = chunkSize / (vps - 1);

            // 잔디 격자 간격: grassSpacing=4f → 정점 격자 2칸 간격(≈8m) = 50×50 = 2500지점/청크
            int stride = Mathf.Max(1, Mathf.RoundToInt(grassSpacing / step * 2f));
            int gridN = (vps - 1) / stride + 1; // 100 정점 / stride 2 → 50

            var lakes = TerrainGenerator.Lakes; // public IReadOnlyList<TerrainLakeDef> (center/radius)

            var gVerts = new List<Vector3>(gridN * gridN * 8);
            var gUv = new List<Vector2>(gridN * gridN * 8);
            var gTris = new List<int>(gridN * gridN * 24);

            var rand = new System.Random(ix * 7919 + iz * 104729 + 1); // 청크인덱스 기반 결정론 시드
            float halfW = grassWidth * 0.5f;
            float rot45 = Mathf.PI * 0.25f; // 교차 쿼드 45° 차
            int tufts = 0;

            for (int j = 0; j < vps; j += stride)
            {
                for (int i = 0; i < vps; i += stride)
                {
                    Vector3 p = chunkVerts[j * vps + i]; // 청크 로컬 (GO.y=0 규약 → y = 월드높이)

                    // 호수 제외: 지점 월드XZ가 lake 중심으로부터 radius × 1.05 이내면 스킵
                    float wx = chunkGO.transform.position.x + p.x;
                    float wz = chunkGO.transform.position.z + p.z;
                    bool inLake = false;
                    for (int l = 0; l < lakes.Count; l++)
                    {
                        float rr = lakes[l].radius * 1.05f;
                        float dx = wx - lakes[l].center.x;
                        float dz = wz - lakes[l].center.z;
                        if (dx * dx + dz * dz < rr * rr) { inLake = true; break; }
                    }
                    if (inLake) continue;

                    float baseY = p.y + 0.05f; // 지면 살짝 위 (묻힘/z-fighting 방지)
                    float h = grassHeight + (float)(rand.NextDouble() - 0.5) * 0.2f; // 0.5~0.7m
                    float rot = (float)rand.NextDouble() * Mathf.PI; // 랜덤 Y회전 (쿼드 좌우 대칭 → π 주기)

                    // 교차 쿼드 2개 (45° 차) — 각 쿼드: 폭 grassWidth, 높이 h, 정점 4 + 삼각형 2
                    for (int q = 0; q < 2; q++)
                    {
                        float ang = rot + q * rot45;
                        float cos = Mathf.Cos(ang) * halfW;
                        float sin = Mathf.Sin(ang) * halfW;
                        int vBase = gVerts.Count;

                        gVerts.Add(new Vector3(-cos, baseY, -sin));
                        gVerts.Add(new Vector3(cos, baseY, sin));
                        gVerts.Add(new Vector3(cos, baseY + h, sin));
                        gVerts.Add(new Vector3(-cos, baseY + h, -sin));

                        gUv.Add(new Vector2(0f, 0f));
                        gUv.Add(new Vector2(1f, 0f));
                        gUv.Add(new Vector2(1f, 1f));
                        gUv.Add(new Vector2(0f, 1f));

                        gTris.Add(vBase); gTris.Add(vBase + 1); gTris.Add(vBase + 2);
                        gTris.Add(vBase); gTris.Add(vBase + 2); gTris.Add(vBase + 3);
                    }
                    tufts++;
                }
            }

            if (gVerts.Count == 0) return; // 전 지점 제외(호수) 등 — 빈 메시 방지

            var grassMesh = new Mesh
            {
                name = $"Ground_Chunk_{ix}_{iz}_GrassMesh",
                indexFormat = gVerts.Count > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            grassMesh.vertices = gVerts.ToArray();
            grassMesh.uv = gUv.ToArray();
            grassMesh.triangles = gTris.ToArray();
            grassMesh.RecalculateBounds(); // Unlit — 법선 재계산 불필요

            // 잔디 오브젝트: 청크 자식, 콜라이더 없음, 그림자 off (청크당 1드로우콜)
            var grassGO = new GameObject($"Ground_Chunk_{ix}_{iz}_Grass");
            grassGO.layer = chunkGO.layer;
            grassGO.transform.SetParent(chunkGO.transform, false);

            var gmf = grassGO.AddComponent<MeshFilter>();
            gmf.sharedMesh = grassMesh;
            var gmr = grassGO.AddComponent<MeshRenderer>();
            gmr.sharedMaterial = GetGrassMaterial();
            gmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (!_grassLoggedOnce)
            {
                _grassLoggedOnce = true;
                int gridCount = Mathf.Max(1, Mathf.RoundToInt(worldHalf * 2f / chunkSize));
                long totalTufts = (long)tufts * gridCount * gridCount;
                Debug.Log($"[TerrainChunks] 정적 잔디: 청크당 터프 {tufts:N0}개 / 정점 {gVerts.Count:N0}개 " +
                          $"(전체 {gridCount * gridCount}청크 ≈ 터프 {totalTufts:N0} / 정점 {totalTufts * 8:N0}, 1드로우콜/청크)");
            }
        }

        /// <summary>
        /// 정적 잔디 공유 머티리얼 (전 청크 1개 — 1회 생성 캐시).
        /// URP/Unlit + _Cull=0(양면 — 교차 쿼드 뒷면 렌더) / 실패 시 Sprites/Default 폴백.
        /// </summary>
        private static Material GetGrassMaterial()
        {
            if (_grassMaterial != null) return _grassMaterial;

            Color grass = new Color(0.32f, 0.52f, 0.22f);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                _grassMaterial = new Material(shader) { color = grass };
                _grassMaterial.SetFloat("_Cull", 0f); // 0=Off(양면)
                return _grassMaterial;
            }

            shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            _grassMaterial = new Material(shader) { color = grass };
            Debug.LogWarning("[TerrainChunks] URP/Unlit 없음 — 잔디 머티리얼 폴백 사용");
            return _grassMaterial;
        }

        /// <summary>
        /// Ground_Inner 메시의 uv에서 affine 매핑(worldXZ → uv)을 역산한다.
        /// 로컬X + 오브젝트X = 월드X 규약(TerrainTextureApplier Phase B와 동일) 사용.
        /// 실패 시 out 값에 폴백(worldXZ/3200+0.5)을 유지하고 false 반환.
        /// </summary>
        private static bool TryReadUvMapping(Mesh mesh, Vector3 groundPos,
            out float uScale, out float uOffset, out float vScale, out float vOffset)
        {
            uScale = 1f / 3200f; uOffset = 0.5f;
            vScale = 1f / 3200f; vOffset = 0.5f;

            var verts = mesh.vertices;
            var uvs = mesh.uv;
            if (verts == null || uvs == null || verts.Length != uvs.Length || verts.Length == 0)
                return false;

            int minX = 0, maxX = 0, minZ = 0, maxZ = 0;
            for (int i = 1; i < verts.Length; i++)
            {
                if (verts[i].x < verts[minX].x) minX = i;
                if (verts[i].x > verts[maxX].x) maxX = i;
                if (verts[i].z < verts[minZ].z) minZ = i;
                if (verts[i].z > verts[maxZ].z) maxZ = i;
            }

            float dx = verts[maxX].x - verts[minX].x;
            float dz = verts[maxZ].z - verts[minZ].z;
            if (dx < 0.5f || dz < 0.5f) return false;

            float us = (uvs[maxX].x - uvs[minX].x) / dx;
            float vs = (uvs[maxZ].y - uvs[minZ].y) / dz;
            if (!float.IsFinite(us) || Mathf.Abs(us) < 1e-6f) return false;
            if (!float.IsFinite(vs) || Mathf.Abs(vs) < 1e-6f) return false;

            uScale = us;
            uOffset = uvs[minX].x - us * (verts[minX].x + groundPos.x);
            vScale = vs;
            vOffset = uvs[minZ].y - vs * (verts[minZ].z + groundPos.z);
            return true;
        }

        /// <summary>
        /// Ground_Inner의 MeshRenderer+MeshCollider만 비활성 (1회).
        /// 오브젝트/컴포넌트는 유지 — TerrainTextureApplier 등이 살아 있어야 하므로 SetActive(false) 금지.
        /// </summary>
        private void DisableGroundInner()
        {
            if (_innerDisabled) return;
            _innerDisabled = true;

            var gi = GameObject.Find("Ground_Inner");
            if (gi == null) return;

            var mr = gi.GetComponent<MeshRenderer>();
            var mc = gi.GetComponent<MeshCollider>();
            if (mr != null) mr.enabled = false;
            if (mc != null) mc.enabled = false;
            Debug.Log("[TerrainChunks] ✅ Ground_Inner 렌더러/콜라이더 비활성 (오브젝트·컴포넌트는 유지 — TerrainTextureApplier 보존)");
        }
    }
}
