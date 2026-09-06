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

        private const float GroundBase = 1f;   // 지표면 기저 y (GetHeightAt + 1f)
        private const int Seed = 42;

        // 재진입 방지 플래그 (정적 — 중복 AddComponent/재Start 모두 차단)
        private static bool _builtOnce;

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
