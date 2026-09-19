using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase O5: 군집 회피 — 정지 유닛 분리 (서로 밀어내기).
    /// docs/reference/openmmo/MONSTER_SEPARATION.md 셀 점유 설계 이식:
    ///   - "셀은 멈춰 서 있는 유닛만 점유" → 이동 중인 유닛은 밀지 않고, 서 있는 유닛끼리만 분리한다.
    ///   - GuardPlaceholder에 이동 중 여부 public 프로퍼티가 없으므로 위치 변화 속도로 추정하되,
    ///     자기 push가 만든 변위는 정지 판정에서 제외한다(밀린 유닛이 '이동 중'으로 오판되어
    ///     밀기가 멈추는 자기소멸 버그 방지).
    ///
    /// 퍼포먼스: 씬 전체 매 틱 스캔 금지 —
    ///   1) FindObjectsByType 결과를 1초 주기로 정적 리스트(_unitCache)에 캐시
    ///   2) 프레임당 maxUnitsPerFrame개 로테이션 스캔(프레임 슬라이싱)
    ///   3) 플레이어로부터 cullDistance 밖 유닛은 스킵
    /// 밀기는 유닛별 lastPushTime으로 tickSeconds 스로틀.
    /// 순수 계산(ComputePush)은 static으로 분리해 EditMode 테스트 가능 (FormationSpreadTests).
    /// </summary>
    public class SeparationSystem : MonoBehaviour
    {
        public static SeparationSystem Instance { get; private set; }

        [Header("Phase O5 — 군집 회피")]
        [SerializeField] private float _scanRadius = 2.2f;        // 겹침 판정 반경(m)
        [SerializeField] private float _pushAmount = 0.02f;       // 틱당 밀어내기 거리(m)
        [SerializeField] private float _tickSeconds = 0.1f;       // 유닛별 밀기 스로틀(초)
        [SerializeField] private int _maxUnitsPerFrame = 40;      // 프레임당 최대 스캔 유닛 수
        [SerializeField] private float _cullDistance = 60f;       // 플레이어로부터 이 거리 밖은 스킵(m)
        [SerializeField] private float _stationarySpeed = 0.05f;  // 정지 판정 속도 임계값(m/s, 자기 push 변위 제외 기준)
        [SerializeField] private float _cacheRefreshSeconds = 1f; // 유닛 캐시 재수집 주기(초)

        private readonly List<GuardPlaceholder> _unitCache = new List<GuardPlaceholder>();
        private Dictionary<GuardPlaceholder, Vector3> _lastPositions = new Dictionary<GuardPlaceholder, Vector3>();
        private Dictionary<GuardPlaceholder, float> _lastSampleTimes = new Dictionary<GuardPlaceholder, float>();
        private Dictionary<GuardPlaceholder, float> _lastPushTimes = new Dictionary<GuardPlaceholder, float>();
        private Dictionary<GuardPlaceholder, float> _selfPushAccum = new Dictionary<GuardPlaceholder, float>(); // 마지막 샘플 이후 자기 push 누적 변위
        private int _scanCursor;
        private float _nextCacheRefresh;
        private Transform _playerTransform;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float now = Time.time;
            if (now >= _nextCacheRefresh) RefreshUnitCache(now);
            ScanSlice(now);
        }

        /// <summary>유닛 캐시 재수집 (1초 주기) — 사라진 유닛의 추적 상태도 같이 정리.</summary>
        private void RefreshUnitCache(float now)
        {
            _nextCacheRefresh = now + _cacheRefreshSeconds;
            _unitCache.Clear();

            var found = FindObjectsByType<GuardPlaceholder>(FindObjectsSortMode.None);
            foreach (var unit in found)
            {
                if (unit != null && unit.gameObject.activeInHierarchy)
                    _unitCache.Add(unit);
            }

            // 사라진 유닛의 속도 기준점 정리 (현 캐시에 있는 것만 보존)
            var positions = new Dictionary<GuardPlaceholder, Vector3>();
            var times = new Dictionary<GuardPlaceholder, float>();
            foreach (var unit in _unitCache)
            {
                if (_lastPositions.TryGetValue(unit, out var p)) positions[unit] = p;
                if (_lastSampleTimes.TryGetValue(unit, out var t)) times[unit] = t;
            }
            _lastPositions = positions;
            _lastSampleTimes = times;
            _selfPushAccum.Clear();
        }

        /// <summary>
        /// 프레임 슬라이싱 스캔 — 커서부터 maxUnitsPerFrame개씩 로테이션하며 분리 처리.
        /// 전 유닛이 매 프레임 스캔되지 않고 순환하며 처리된다.
        /// </summary>
        private void ScanSlice(float now)
        {
            int total = _unitCache.Count;
            if (total == 0) return;

            EnsurePlayerCache();
            if (_playerTransform == null) return; // 컬링 기준(플레이어) 부재 → 이번 프레임 스킵

            int batch = Mathf.Min(_maxUnitsPerFrame, total);
            foreach (int offset in System.Linq.Enumerable.Range(0, batch))
            {
                int idx = (_scanCursor + offset) % total;
                var unit = _unitCache[idx];
                if (unit == null || !unit.gameObject.activeInHierarchy || !unit.IsAlive) continue;
                TrySeparate(unit, now);
            }
            _scanCursor = (_scanCursor + batch) % total;
        }

        /// <summary>
        /// 유닛 1개 분리 처리: 컬링 → 정지 판정 → 스로틀 → 겹친 정지 이웃 수집 → 반대 방향 push 1회 적용.
        /// </summary>
        private void TrySeparate(GuardPlaceholder unit, float now)
        {
            Vector3 pos = unit.transform.position;

            // 플레이어 컬링 — cullDistance 밖 유닛은 스캔 스킵 (성능)
            if (Vector3.Distance(pos, _playerTransform.position) > _cullDistance) return;

            // 기준점 없음(첫 스캔) → 이번 틱에 기록만 하고 다음 틱부터 판정 (스폰/텔레포트 오판 방지)
            if (!_lastPositions.TryGetValue(unit, out var lastPos) ||
                !_lastSampleTimes.TryGetValue(unit, out var lastTime))
            {
                _lastPositions[unit] = pos;
                _lastSampleTimes[unit] = now;
                return;
            }

            // 정지 판정 — 위치 변화 속도 추정 (자기 push가 만든 변위는 제외)
            float dt = now - lastTime;
            float rawDist = Vector3.Distance(pos, lastPos);
            _selfPushAccum.TryGetValue(unit, out float selfPushed);
            float speed = dt > 0.0001f ? Mathf.Max(0f, rawDist - selfPushed) / dt : float.MaxValue;

            _lastPositions[unit] = pos;
            _lastSampleTimes[unit] = now;
            _selfPushAccum[unit] = 0f; // 샘플 기준 리셋

            if (speed > _stationarySpeed) return; // 이동 중 → 셀 점유 없음(밀지 않음)

            // 스로틀 — 유닛별 lastPushTime, tickSeconds 이내 재밀기 금지
            if (_lastPushTimes.TryGetValue(unit, out var lastPush) && now - lastPush < _tickSeconds)
                return;

            // 겹친 이웃 수집 — 반경 scanRadius 내 '서 있는' 타 유닛만 (이동 중 이웃은 밀지 않음)
            Vector3 accumulated = Vector3.zero;
            bool anyOverlap = false;
            foreach (var other in _unitCache)
            {
                if (other == null || ReferenceEquals(other, unit)) continue;
                if (!other.gameObject.activeInHierarchy || !other.IsAlive) continue;

                Vector3 otherPos = other.transform.position;
                if ((otherPos - pos).sqrMagnitude > _scanRadius * _scanRadius) continue;

                // 이웃 정지 판정 (이웃의 마지막 스캔 기준점 대비, 이웃의 자기 push 변위 제외)
                if (!_lastPositions.TryGetValue(other, out var oLast) ||
                    !_lastSampleTimes.TryGetValue(other, out var oTime))
                    continue; // 이웃 기준점 없음 → 미판정, 스킵

                float oDt = now - oTime;
                float oRaw = Vector3.Distance(otherPos, oLast);
                _selfPushAccum.TryGetValue(other, out float oPushed);
                float oSpeed = oDt > 0.0001f ? Mathf.Max(0f, oRaw - oPushed) / oDt : float.MaxValue;
                if (oSpeed > _stationarySpeed) continue;

                accumulated += ComputePush(pos, otherPos, _pushAmount);
                anyOverlap = true;
            }
            if (!anyOverlap) return;

            // 합산 push 클램프 — 틱당 총 이동량 ≤ pushAmount (여러 유닛에 둘러싸여도 과밀기 방지)
            Vector3 push = accumulated;
            float mag = push.magnitude;
            if (mag <= 0.0001f) return;
            if (mag > _pushAmount) push *= _pushAmount / mag;

            ApplyPush(unit, push);
            _selfPushAccum[unit] += push.magnitude; // 다음 정지 판정에서 제외될 자기 변위
            _lastPushTimes[unit] = now;
        }

        /// <summary>
        /// 밀기 벡터 계산 (순수 정적 — EditMode 테스트 가능).
        /// 자신 − 상대 방향(XZ 정규화) × pushAmount. 0거리(동일 좌표)는 0 반환,
        /// 음수 pushAmount는 0으로 클램프 → 크기는 항상 ≤ pushAmount.
        /// </summary>
        public static Vector3 ComputePush(Vector3 selfPos, Vector3 otherPos, float pushAmount)
        {
            Vector3 dir = selfPos - otherPos;
            dir.y = 0f;
            float mag = dir.magnitude;
            if (mag < 0.0001f) return Vector3.zero; // 0거리 방어
            float clamped = Mathf.Max(0f, pushAmount);
            return dir / mag * clamped;
        }

        /// <summary>push 적용 — CharacterController 있으면 Move, 없으면 transform 직접 이동.</summary>
        private static void ApplyPush(GuardPlaceholder unit, Vector3 push)
        {
            var controller = unit.GetComponent<CharacterController>();
            if (controller != null && controller.enabled)
            {
                controller.Move(push);
            }
            else
            {
                unit.transform.position += push;
            }
        }

        /// <summary>플레이어 캐시 갱신 (컬링 기준).</summary>
        private void EnsurePlayerCache()
        {
            if (_playerTransform != null && _playerTransform.gameObject.activeInHierarchy) return;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }
    }
}
