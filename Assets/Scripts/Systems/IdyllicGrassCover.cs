using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase CC1: 플레이어 주변 동적 잔디 커버.
    /// 기존 IdyllicDecoPlacer의 정적 잔디(1개/125㎡)가 안 보이던 문제를 해결하는 밀집 커버.
    ///
    /// 설계:
    ///  · 반경 45m 원형 내 Grass_01/02/03 풋을 10m 셀(청크) 단위로 배치 (활성 ≈ 6000~9000).
    ///  · 청크 중심이 반경 45m 안에 들어오면 활성, 떠나면 비활성 + 풀 회수.
    ///  · 프레임당 생성 예산 50개(스파이크 방지) — 새 청크 진입 시 지연 생성.
    ///  · 밀도 가중치: 꽃밭(GetFlowerPatchMask>0.5)/숲(GetForestPatchMask>0.5) 내부 ×3.
    ///  · y = 1 + GetHeightAt(x,z,Plains,42) + 0.04, 경사 30° 스킵, 호수 수변(1.02r) 제외,
    ///    스케일 0.7~1.3, 기울기 ±8°, 결정론(청크 좌표 해시).
    ///  · 풀(pool): 떠난 청크의 잔디 오브젝트를 비활성 상태로 재사용해 메모리 상한을 유지한다.
    ///
    /// 반경 45m 내만 존재하므로 외부 컬링(IdyllicGrassCuller)은 불필요하다.
    /// GameSetup이 플레이어 생성 후 Configure(playerTransform)을 호출해 배선한다.
    /// </summary>
    public class IdyllicGrassCover : MonoBehaviour
    {
        const float CELL_SIZE = 10f;
        const float RADIUS = 45f;
        const int   BUDGET_PER_FRAME = 50;      // 프레임당 잔디 생성 예산 (스파이크 방지)
        const int   BASE_PER_CELL = 90;          // 셀(10×10m=100㎡)당 개수 ≈ ~6300/45m 원
        const int   DENSE_MULT = 3;              // 꽃밭/숲 마스크 내부 밀도 배수
        const float MASK_HI = 0.5f;
        const float GROUND_BASE = 1f;
        const float GRASS_TILT_DEG = 8f;
        const float SLOPE_MAX_DEG = 30f;
        const float LAKE_EXCLUDE_FACTOR = 1.02f;
        const float SCALE_MIN = 0.7f, SCALE_MAX = 1.3f;
        const float BOUND_MAX = 950f;
        const int   MAX_POOL = 16000;            // 풀 상한 (활성 ~6300 + 여유)
        const int   FANTASY_SEED = 20260904;     // 숲 마스크 시드 (IdyllicDecoPlacer와 동일)

        Transform _player;
        readonly List<GameObject> _prefabs = new List<GameObject>();
        readonly Dictionary<long, Chunk> _chunks = new Dictionary<long, Chunk>();
        readonly List<Chunk> _buildQueue = new List<Chunk>();
        readonly List<GameObject> _pool = new List<GameObject>();
        Transform _parent;
        Vector2Int _lastPlayerCell;
        bool _initialized;
        int _budgetUsedThisFrame;

        class Chunk
        {
            public Vector2Int cell;
            public readonly List<GameObject> items = new List<GameObject>();
            public bool built;
            public bool active;
        }

        /// <summary>플레이어 앵커 설정 후 동적 잔디 커버 시작. prefs가 null/비면 Resources/Grass 자동 로드.</summary>
        public void Configure(Transform player, List<GameObject> prefs = null)
        {
            _player = player;
            if (prefs != null && prefs.Count > 0)
                _prefabs.AddRange(prefs);
            if (_prefabs.Count == 0)
            {
                var loaded = Resources.LoadAll<GameObject>("IdyllicPrefabs/Grass");
                if (loaded != null)
                    foreach (var g in loaded) if (g != null) _prefabs.Add(g);
                if (_prefabs.Count == 0)
                    Debug.LogWarning("[IdyllicGrassCover] 잔디 프리팹 없음 — Resources/IdyllicPrefabs/Grass 확인");
            }
            if (_parent == null)
            {
                var go = new GameObject("IdyllicDynamicGrass");
                go.layer = 0;
                _parent = go.transform;
            }
            _initialized = true;
            _lastPlayerCell = new Vector2Int(int.MaxValue, int.MaxValue);
            Debug.Log($"[IdyllicGrassCover] Configure 완료 — 플레이어={(_player != null ? _player.name : "null")} 프리팹={_prefabs.Count}");
        }

        void Update()
        {
            if (!_initialized || _player == null) return;
            var pc = _player.position;
            var playerCell = WorldToCell(pc.x, pc.z);
            if (playerCell.x != _lastPlayerCell.x || playerCell.y != _lastPlayerCell.y)
                UpdateActiveSet(pc, playerCell);
            _lastPlayerCell = playerCell;

            _budgetUsedThisFrame = 0;
            while (_buildQueue.Count > 0 && _budgetUsedThisFrame < BUDGET_PER_FRAME)
            {
                var c = _buildQueue[0];
                _buildQueue.RemoveAt(0);
                BuildChunk(c);
            }
        }

        Vector2Int WorldToCell(float x, float z)
        {
            return new Vector2Int(Mathf.FloorToInt(x / CELL_SIZE), Mathf.FloorToInt(z / CELL_SIZE));
        }
        static long CellKey(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

        void UpdateActiveSet(Vector3 playerPos, Vector2Int playerCell)
        {
            // 1) 범위 밖 청크 비활성 + 풀 회수 (천천히 치우지 않고 즉시)
            foreach (var kv in _chunks)
            {
                var c = kv.Value;
                if (!c.active) continue;
                float wx = (c.cell.x + 0.5f) * CELL_SIZE;
                float wz = (c.cell.y + 0.5f) * CELL_SIZE;
                float dx = wx - playerPos.x, dz = wz - playerPos.z;
                if (dx * dx + dz * dz <= RADIUS * RADIUS) continue;
                SetChunkActive(c, false);
            }

            // 2) 범위 안 청크 활성 (미구축이면 빌드 큐)
            int range = Mathf.CeilToInt(RADIUS / CELL_SIZE) + 1;
            for (int cx = playerCell.x - range; cx <= playerCell.x + range; cx++)
            {
                for (int cz = playerCell.y - range; cz <= playerCell.y + range; cz++)
                {
                    float wx = (cx + 0.5f) * CELL_SIZE;
                    float wz = (cz + 0.5f) * CELL_SIZE;
                    float dx = wx - playerPos.x, dz = wz - playerPos.z;
                    if (dx * dx + dz * dz > RADIUS * RADIUS) continue;
                    long key = CellKey(cx, cz);
                    Chunk c;
                    if (!_chunks.TryGetValue(key, out c))
                    {
                        c = new Chunk { cell = new Vector2Int(cx, cz) };
                        _chunks[key] = c;
                    }
                    if (!c.built)
                    {
                        if (!_buildQueue.Contains(c))
                            _buildQueue.Add(c);
                    }
                    else if (!c.active)
                        SetChunkActive(c, true);
                }
            }
        }

        void BuildChunk(Chunk c)
        {
            float wx = (c.cell.x + 0.5f) * CELL_SIZE;
            float wz = (c.cell.y + 0.5f) * CELL_SIZE;
            float orgX = c.cell.x * CELL_SIZE;
            float orgZ = c.cell.y * CELL_SIZE;
            long seedKey = CellKey(c.cell.x, c.cell.y);

            var nation = NationTerrainController.GetNationFromPosition(new Vector3(wx, 0f, wz));
            bool dense = TerrainShape.GetFlowerPatchMask(wx, wz) > MASK_HI
                || TerrainShape.GetForestPatchMask(wx, wz, nation, FANTASY_SEED) > MASK_HI;
            int count = dense ? BASE_PER_CELL * DENSE_MULT : BASE_PER_CELL;

            c.items.Clear();
            for (int k = 0; k < count && _budgetUsedThisFrame < BUDGET_PER_FRAME; k++)
            {
                float lx = Hash01(seedKey, k, 1) * CELL_SIZE;
                float lz = Hash01(seedKey, k, 2) * CELL_SIZE;
                float x = orgX + lx;
                float z = orgZ + lz;
                if (Mathf.Abs(x) > BOUND_MAX - 2f || Mathf.Abs(z) > BOUND_MAX - 2f) continue;
                if (IsNearLakeWater(x, z, LAKE_EXCLUDE_FACTOR)) continue;
                if (TerrainSplatBaker.EstimateSlopeDegrees(x, z) > SLOPE_MAX_DEG) continue;
                float y = GROUND_BASE + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42) + 0.04f;
                var model = _prefabs[k % _prefabs.Count];
                float scale = (SCALE_MIN + (SCALE_MAX - SCALE_MIN) * Hash01(seedKey, k, 3))
                              * (0.9f + 0.2f * Hash01(seedKey, k, 4));
                var go = GetFromPool(model);
                go.transform.SetParent(_parent, false);
                go.transform.position = new Vector3(x, y, z);
                go.transform.rotation = Quaternion.Euler(
                    (Hash01(seedKey, k, 5) * 2f - 1f) * GRASS_TILT_DEG,
                    Hash01(seedKey, k, 6) * 360f,
                    (Hash01(seedKey, k, 7) * 2f - 1f) * GRASS_TILT_DEG);
                go.transform.localScale = Vector3.one * scale;
                c.items.Add(go);
                _budgetUsedThisFrame++;
            }
            c.built = true;
            SetChunkActive(c, true); // 빌드 직후 활성 (플레이어 범위 안이므로)
        }

        GameObject GetFromPool(GameObject model)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var c = _pool[i];
                if (c == null) { _pool.RemoveAt(i); i--; continue; }
                if (c.name == model.name) { _pool.RemoveAt(i); return c; }
            }
            var fresh = Object.Instantiate(model, _parent);
            fresh.layer = 0;
            return fresh;
        }

        void SetChunkActive(Chunk c, bool active)
        {
            foreach (var go in c.items)
                if (go != null) go.SetActive(active);
            c.active = active;
            if (!active)
                ReturnToPool(c);
        }

        void ReturnToPool(Chunk c)
        {
            foreach (var go in c.items)
            {
                if (go == null) continue;
                go.SetActive(false);
                if (_pool.Count < MAX_POOL)
                    _pool.Add(go);
                else Object.Destroy(go);
            }
            c.items.Clear();
            c.built = false;
        }

        static bool IsNearLakeWater(float x, float z, float marginFactor)
        {
            var lakes = TerrainGenerator.Lakes;
            for (int i = 0; i < lakes.Count; i++)
            {
                var lake = lakes[i];
                float dx = x - lake.center.x;
                float dz = z - lake.center.z;
                float m = lake.radius * marginFactor;
                if (dx * dx + dz * dz < m * m)
                    return true;
            }
            return false;
        }

        /// <summary>청크 좌표/인덱스 기반 결정론 0~1 해시.</summary>
        static float Hash01(long seedKey, int k, int salt)
        {
            uint h = (uint)seedKey;
            h ^= (uint)(k * 0x9E3779B9);
            h += (uint)(salt * 0x85EBCA6B);
            h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
            h = (h ^ (h >> 13)) * 0xC2B2AE35u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}