using UnityEngine;

namespace ProjectName.Systems.Animation.Procedural.Locomotion.Ground
{
    /// <summary>
    /// 레이캐스트 배치 처리 + 캐싱 최적화 클래스.
    ///
    /// [목적]
    /// ProceduralAnimationController.UpdateGroundDetection()에서
    /// 매 프레임 4개 발에 대해 개별 Physics.Raycast()를 수행하던 것을
    /// 캐싱 + Physics.RaycastNonAlloc 배치 처리로 최적화합니다.
    ///
    /// [특징]
    /// - 캐시 유효 시간: 0.1초 (100ms)
    /// - 캐시 무효화 조건: 캐릭터 이동 거리 > 0.1m 또는 쿼리 오리진 변화 > 0.05m
    /// - Physics.RaycastNonAlloc으로 GC 할당 제로
    /// - Collect → Resolve 패턴: 모든 쿼리를 모은 후 한 번에 해소
    /// - 쿼리당 최대 1개 히트, 최대 capacity개(기본 4개) 쿼리 지원
    /// </summary>
    public class TerrainCache
    {
        // ──────────────────────────────────────────────
        // 상수
        // ──────────────────────────────────────────────

        /// <summary>캐시 유효 시간 (초). 이 시간이 지나면 무조건 재레이캐스트.</summary>
        const float CACHE_DURATION = 0.1f;

        /// <summary>캐릭터 위치 기반 무효화 거리 (미터). 이 이상 이동하면 캐시 무효.</summary>
        const float INVALIDATION_DISTANCE = 0.1f;

        /// <summary>개별 쿼리 오리진 변화 임계값 (미터). 오리진이 이 이상 변하면 해당 엔트리만 무효.</summary>
        const float ORIGIN_THRESHOLD = 0.05f;

        /// <summary>RaycastNonAlloc당 최대 히트 수 (1이면 최상단 히트만 수집).</summary>
        const int MAX_HITS_PER_QUERY = 1;

        // ──────────────────────────────────────────────
        // 공개 구조체
        // ──────────────────────────────────────────────

        /// <summary>레이캐스트 샘플 결과 (히트 정보의 최소 집합).</summary>
        public struct TerrainSample
        {
            /// <summary>지면에 히트했는지 여부</summary>
            public bool Hit;

            /// <summary>히트 포인트 (월드 좌표)</summary>
            public Vector3 Point;

            /// <summary>히트 표면의 법선 벡터</summary>
            public Vector3 Normal;

            /// <summary>오리진부터 히트까지의 거리</summary>
            public float Distance;

            /// <summary>유효하지 않은 샘플 (Hit=false) 반환</summary>
            public static TerrainSample Invalid => new TerrainSample { Hit = false };
        }

        // ──────────────────────────────────────────────
        // 내부 구조체
        // ──────────────────────────────────────────────

        /// <summary>캐시 엔트리 — 각 쿼리 인덱스별로 하나씩 저장.</summary>
        struct CacheEntry
        {
            /// <summary>이 캐시가 유효한지 여부</summary>
            public bool Valid;

            /// <summary>캐시 생성 시점의 오리진 (월드 좌표)</summary>
            public Vector3 Origin;

            /// <summary>캐시된 샘플 결과</summary>
            public TerrainSample Sample;

            /// <summary>캐시가 생성된 타임스탬프 (Time.time)</summary>
            public float Timestamp;
        }

        /// <summary>프레임 내에서 대기 중인 쿼리 (Resolve()에서 한꺼번에 처리).</summary>
        struct PendingQuery
        {
            /// <summary>쿼리 인덱스 (0 ~ capacity-1)</summary>
            public int Index;

            /// <summary>레이캐스트 오리진 (월드 좌표)</summary>
            public Vector3 Origin;

            /// <summary>레이 방향 (일반적으로 Vector3.down)</summary>
            public Vector3 Direction;

            /// <summary>최대 탐색 거리</summary>
            public float MaxDistance;

            /// <summary>히트할 레이어마스크</summary>
            public LayerMask LayerMask;
        }

        // ──────────────────────────────────────────────
        // 필드
        // ──────────────────────────────────────────────

        /// <summary>캐시 엔트리 배열 (인덱스 = 쿼리 인덱스)</summary>
        readonly CacheEntry[] _cache;

        /// <summary>대기 중인 쿼리 배열 (Resolve()에서 처리)</summary>
        readonly PendingQuery[] _pendingQueries;

        /// <summary>RaycastNonAlloc 재사용 버퍼 (GC 할당 방지)</summary>
        readonly RaycastHit[] _hitBuffer;

        /// <summary>최대 지원 쿼리 수</summary>
        readonly int _capacity;

        /// <summary>현재 프레임에 추가된 대기 쿼리 수</summary>
        int _pendingCount;

        /// <summary>마지막 프레임의 Time.time</summary>
        float _lastFrameTime;

        /// <summary>마지막 프레임의 캐릭터 위치 (BeginFrame에서 설정)</summary>
        Vector3 _lastCharacterPosition;

        /// <summary>글로벌 캐시 더티 플래그 (시간/거리 기반 무효화)</summary>
        bool _cacheDirty;

        /// <summary>현재 프레임이 BeginFrame으로 시작되었는지</summary>
        bool _frameActive;

        // ──────────────────────────────────────────────
        // 생성자
        // ──────────────────────────────────────────────

        /// <param name="capacity">최대 쿼리 포인트 수 (기본 4 = 양발 + 양손).</param>
        public TerrainCache(int capacity = 4)
        {
            _capacity = Mathf.Max(1, capacity);
            _cache = new CacheEntry[_capacity];
            _pendingQueries = new PendingQuery[_capacity];
            _hitBuffer = new RaycastHit[MAX_HITS_PER_QUERY];
            _lastFrameTime = -1f;
            _cacheDirty = true;
            _frameActive = false;
            _pendingCount = 0;
        }

        // ──────────────────────────────────────────────
        // 공개 메서드
        // ──────────────────────────────────────────────

        /// <summary>
        /// 매 프레임 최초 1회 호출. 캐릭터의 현재 월드 위치를 받아 글로벌 캐시 무효화를 판단.
        /// </summary>
        /// <param name="characterPosition">캐릭터의 현재 월드 위치 (transform.position).</param>
        public void BeginFrame(Vector3 characterPosition)
        {
            _frameActive = true;
            _pendingCount = 0;

            float now = Time.time;

            // 첫 프레임이면 무조건 더티
            if (_lastFrameTime < 0f)
            {
                _cacheDirty = true;
            }
            else
            {
                float dt = now - _lastFrameTime;
                float dist = Vector3.Distance(characterPosition, _lastCharacterPosition);

                // 시간 기반 무효화: 캐시 유효 시간(0.1초) 경과
                // 거리 기반 무효화: 캐릭터가 0.1m 이상 이동
                if (dt > CACHE_DURATION || dist > INVALIDATION_DISTANCE)
                {
                    _cacheDirty = true;
                }
            }

            _lastFrameTime = now;
            _lastCharacterPosition = characterPosition;
        }

        /// <summary>
        /// 레이캐스트 쿼리를 추가 (아래 방향 일반). Resolve() 호출 전까지는 실제 레이캐스트 수행 안 함.
        /// </summary>
        /// <param name="index">쿼리 인덱스 (0 ~ capacity-1, 각 발/손에 고유 인덱스 할당).</param>
        /// <param name="origin">레이캐스트 오리진 (월드 좌표, 발 위치 + Vector3.up * 0.2f).</param>
        /// <param name="maxDistance">최대 탐색 거리.</param>
        /// <param name="layerMask">히트할 레이어마스크.</param>
        public void AddQuery(int index, Vector3 origin, float maxDistance, LayerMask layerMask)
        {
            AddQuery(index, origin, Vector3.down, maxDistance, layerMask);
        }

        /// <summary>
        /// 레이캐스트 쿼리를 추가 (방향 지정). Resolve() 호출 전까지는 실제 레이캐스트 수행 안 함.
        /// </summary>
        /// <param name="index">쿼리 인덱스 (0 ~ capacity-1).</param>
        /// <param name="origin">레이캐스트 오리진 (월드 좌표).</param>
        /// <param name="direction">레이 방향 (일반적으로 Vector3.down).</param>
        /// <param name="maxDistance">최대 탐색 거리.</param>
        /// <param name="layerMask">히트할 레이어마스크.</param>
        public void AddQuery(int index, Vector3 origin, Vector3 direction, float maxDistance, LayerMask layerMask)
        {
            if (!_frameActive)
            {
                UnityEngine.Debug.LogWarning("[TerrainCache] BeginFrame()을 먼저 호출해야 합니다.");
                return;
            }

            if (index < 0 || index >= _capacity)
            {
                UnityEngine.Debug.LogWarning($"[TerrainCache] 인덱스 {index}가 범위를 벗어났습니다 (capacity={_capacity}).");
                return;
            }

            if (_pendingCount >= _capacity)
            {
                UnityEngine.Debug.LogWarning($"[TerrainCache] 대기 쿼리 초과 (최대 {_capacity}).");
                return;
            }

            _pendingQueries[_pendingCount++] = new PendingQuery
            {
                Index = index,
                Origin = origin,
                Direction = direction.normalized,
                MaxDistance = maxDistance,
                LayerMask = layerMask
            };
        }

        /// <summary>
        /// 모든 대기 중인 쿼리를 해소.
        /// - 캐시가 유효한 쿼리는 생략 (재레이캐스트 안 함)
        /// - 캐시가 무효인 쿼리만 Physics.RaycastNonAlloc으로 재수집
        /// - Resolve() 호출 후 GetResult()로 결과 조회 가능
        /// </summary>
        public void Resolve()
        {
            if (!_frameActive)
            {
                UnityEngine.Debug.LogWarning("[TerrainCache] BeginFrame()을 먼저 호출해야 합니다.");
                return;
            }

            float now = Time.time;

            for (int i = 0; i < _pendingCount; i++)
            {
                var query = _pendingQueries[i];
                int idx = query.Index;

                // 개별 캐시 엔트리 확인
                var entry = _cache[idx];

                // 글로벌 더티 OR 개별 오리진 변화 → 재레이캐스트
                bool needsRaycast = _cacheDirty ||
                                    !entry.Valid ||
                                    (now - entry.Timestamp) > CACHE_DURATION ||
                                    Vector3.Distance(entry.Origin, query.Origin) > ORIGIN_THRESHOLD;

                if (needsRaycast)
                {
                    // Physics.RaycastNonAlloc: GC 할당 없이 재사용 버퍼 사용
                    // 버퍼 크기가 1이므로 가장 가까운 히트만 수집
                    int hitCount = Physics.RaycastNonAlloc(
                        query.Origin,
                        query.Direction,
                        _hitBuffer,
                        query.MaxDistance,
                        query.LayerMask
                    );

                    TerrainSample sample;
                    if (hitCount > 0)
                    {
                        RaycastHit hit = _hitBuffer[0];
                        sample = new TerrainSample
                        {
                            Hit = true,
                            Point = hit.point,
                            Normal = hit.normal,
                            Distance = hit.distance
                        };
                    }
                    else
                    {
                        sample = TerrainSample.Invalid;
                    }

                    // 캐시 업데이트
                    _cache[idx] = new CacheEntry
                    {
                        Valid = true,
                        Origin = query.Origin,
                        Sample = sample,
                        Timestamp = now
                    };
                }
                // 캐시 히트 (레이캐스트 불필요) — 아무것도 안 함
            }

            // 글로벌 더티 플래그 리셋
            _cacheDirty = false;
            _pendingCount = 0;
            _frameActive = false;
        }

        /// <summary>
        /// 특정 인덱스의 캐시된 레이캐스트 결과를 반환.
        /// Resolve() 호출 후에만 유효한 값을 반환합니다.
        /// </summary>
        /// <param name="index">쿼리 인덱스 (0 ~ capacity-1).</param>
        /// <returns>캐시된 TerrainSample. 캐시가 없으면 Invalid 반환.</returns>
        public TerrainSample GetResult(int index)
        {
            if (index < 0 || index >= _capacity)
            {
                return TerrainSample.Invalid;
            }

            var entry = _cache[index];
            return entry.Valid ? entry.Sample : TerrainSample.Invalid;
        }

        /// <summary>
        /// 강제로 모든 캐시를 무효화. 다음 Resolve()에서 모든 쿼리가 재레이캐스트됩니다.
        /// 캐릭터가 순간이동하거나 씬이 변경된 경우 호출.
        /// </summary>
        public void InvalidateAll()
        {
            _cacheDirty = true;
            for (int i = 0; i < _capacity; i++)
            {
                _cache[i] = default;
            }
            _lastFrameTime = -1f;
        }

        /// <summary>
        /// 현재 캐시 상태를 요약한 진단 문자열 (디버깅/프로파일링용).
        /// </summary>
        public string GetDiagnostics()
        {
            int validCount = 0;
            int hitCount = 0;
            float now = Time.time;

            for (int i = 0; i < _capacity; i++)
            {
                if (_cache[i].Valid)
                {
                    validCount++;
                    if (_cache[i].Sample.Hit) hitCount++;
                }
            }

            float elapsed = _lastFrameTime > 0f ? (Time.time - _lastFrameTime) : 0f;
            return $"TerrainCache | capacity={_capacity} | valid={validCount}/{_capacity} | hits={hitCount} | " +
                   $"dirty={_cacheDirty} | elapsed={elapsed:F3}s | pending={_pendingCount}";
        }
    }
}