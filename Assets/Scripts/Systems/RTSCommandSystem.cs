using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-20: RTS 명령 시스템 — 우클릭 공격/이동 자동 판단, H키 중단, Ctrl+우클릭 일제 공격
    /// 
    /// 사용법:
    ///   RTSCommandSystem.Instance.IssueRightClickCommand(mousePosition) // 우클릭
    ///   RTSCommandSystem.Instance.IssueRightClickCommand(mousePosition, ctrl: true) // Ctrl+우클릭
    ///   RTSCommandSystem.Instance.StopAllSelectedGuards() // H키
    /// </summary>
    public class RTSCommandSystem : MonoBehaviour
    {
        public static RTSCommandSystem Instance { get; private set; }

        [Header("명령 설정")]
        [SerializeField] private float _raycastMaxDistance = 200f;

        [Header("레이캐스트 레이어")]
        [Tooltip("명령 처리에 사용할 레이어 마스크 (적 유닛 + 지형이 포함된 레이어를 지정)")]
        [SerializeField] private LayerMask _commandLayerMask = ~0; // Everything by default

        private Camera _mainCamera;

        // 빈 리스트 캐시 — 매 GetSelectedGuards() 호출 시 할당 방지
        private static readonly ReadOnlyCollection<GuardPlaceholder> _emptySelected =
            new List<GuardPlaceholder>().AsReadOnly();

        // [P26] 활성 이동 명령 지속 마커 — 소유 병사 도착/취소/사망 시 자동 소멸 추적용
        private readonly List<CommandMarker> _activeMoveMarks = new List<CommandMarker>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            // 씬 전환 시 Camera.main이 stale될 수 있으므로 갱신
            if (_mainCamera == null || !_mainCamera.gameObject.activeInHierarchy)
                _mainCamera = Camera.main;
        }

        /// <summary>
        /// 현재 선택된 병사들을 가져옴 (GuardSelectionManager에 위임)
        /// </summary>
        private IReadOnlyList<GuardPlaceholder> GetSelectedGuards()
        {
            if (GuardSelectionManager.Instance != null)
                return GuardSelectionManager.Instance.SelectedGuards;
            return _emptySelected;
        }

        // ===== 퍼블릭 API =====

        /// <summary>
        /// 우클릭 명령 — 적 대상이면 공격, 지형이면 이동
        /// Ctrl 키와 함께 누르면 모든 선택 병사가 동일한 지점을 타겟으로 일제 공격/이동
        /// </summary>
        public void IssueRightClickCommand(Vector3 mousePosition, bool ctrl = false)
        {
            // [P26] Ctrl+우클릭 미선택 폴백: 선택 병사가 없으면 전체 소속(포섭) 병사로 일괄 이동/공격.
            //   비Ctrl 우클릭은 선택 필수 유지(기존 확산 형성 이동) — 사용자 요구 "Ctrl+우클릭=이동" 정확 반영.
            var originalSel = GetSelectedGuards();
            IReadOnlyList<GuardPlaceholder> selected = originalSel;
            if (selected == null || selected.Count == 0)
            {
                if (!ctrl)
                {
                    Debug.Log("[RTSCommandSystem] 선택된 병사 없음 — Ctrl+우클릭은 소속 병사 일괄 이동, 일반 우클릭은 병사 선택 후");
                    return;
                }
                selected = GetFallbackGuards();
            }
            Debug.Log($"[RTSCommandSystem][P26] 우클릭 수신 — selected={selected?.Count ?? 0}(원선택 {originalSel?.Count ?? 0}) ctrl={ctrl} mouse={mousePosition}");
            if (_mainCamera == null) { Debug.LogWarning("[RTSCommandSystem][P20-7] 카메라 null — 명령 불가"); return; }

            if (selected == null || selected.Count == 0)
            {
                Debug.Log("[RTSCommandSystem] 명령 내릴 병사 없음 (선택 & 소속 병사 모두 없음)");
                return;
            }

            Ray ray = _mainCamera.ScreenPointToRay(mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance, _commandLayerMask))
            {
                Debug.Log("[RTSCommandSystem] 레이캐스트 적중 실패");
                return;
            }

            // 적 대상 확인 (IDamageable)
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                // 공격 명령 — 페이드 마커(즉시), selected 전달(중복 호출 방지)
                if (ctrl) IssueSynchronizedAttackCommand(selected, target, hit.point);
                else IssueAttackCommand(selected, target, hit.point);
            }
            else
            {
                // 이동 명령 — 지속 지면 원형 링(도착까지), selected 전달
                if (ctrl) IssueSynchronizedMoveCommand(selected, hit.point);
                else IssueMoveCommand(selected, hit.point);
            }
        }

        /// <summary>
        /// H키 — 모든 선택된 병사의 명령 취소 (공격 중단)
        /// </summary>
        public void StopAllSelectedGuards()
        {
            var selected = GetSelectedGuards();
            ClearActiveMoveMarks();   // [P26] H키 중단 — 지속 이동 마커 제거
            if (selected == null || selected.Count == 0)
            {
                Debug.Log("[RTSCommandSystem] 정지할 병사 없음");
                return;
            }

            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive)
                {
                    guard.ClearCommand();
                    guard.SetInCombat(false);
                }
            }

            Debug.Log($"[RTSCommandSystem] {selected.Count}명 명령 취소 (H키)");
        }

        /// <summary>
        /// 선택된 병사 수 반환
        /// </summary>
        public int SelectedCount => GetSelectedGuards()?.Count ?? 0;

        // ===== 내부 명령 처리 =====

        /// <summary>
        /// 개별 공격 명령 — 각 병사가 각자 타겟을 공격
        /// </summary>
        private void IssueAttackCommand(IReadOnlyList<GuardPlaceholder> selected, IDamageable target, Vector3 hitPoint)
        {
            var targetTransform = (target as MonoBehaviour)?.transform;
            Vector3 attackPos = targetTransform != null ? targetTransform.position : hitPoint;

            int count = 0;
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive)
                {
                    guard.SetCommandTarget(attackPos, true);
                    guard.SetInCombat(true);
                    count++;
                }
            }
            CommandMarker.Spawn(attackPos);   // [P26] 공격 지점 페이드 마커
            Debug.Log($"[RTSCommandSystem] {count}명 공격 명령 → {attackPos}");
        }

        /// <summary>
        /// 일제 공격 명령 (Ctrl+우클릭) — 모든 병사가 동일한 타겟 위치를 공격
        /// </summary>
        private void IssueSynchronizedAttackCommand(IReadOnlyList<GuardPlaceholder> selected, IDamageable target, Vector3 hitPoint)
        {
            var targetTransform = (target as MonoBehaviour)?.transform;
            Vector3 attackPos = targetTransform != null ? targetTransform.position : hitPoint;

            int count = 0;
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive)
                {
                    guard.SetCommandTarget(attackPos, true);
                    guard.SetInCombat(true);
                    count++;
                }
            }
            CommandMarker.Spawn(attackPos);   // [P26] 공격 지점 페이드 마커
            Debug.Log($"[RTSCommandSystem] {count}명 일제 공격 명령 → {attackPos}");
        }

        /// <summary>
        /// 이동 명령 — Phase O5: 목적지 주위 링 분산 배치로 개별 목표 부여 (뭉침 해소).
        /// 부대 전원에 같은 좌표를 주는 대신 FormationSpread.Distribute로 중심 1 + 링 배치.
        /// </summary>
        private void IssueMoveCommand(IReadOnlyList<GuardPlaceholder> selected, Vector3 position)
        {
            // 분산 지점 수 = 생존 병사 수 (죽은 병사는 제외)
            int aliveCount = 0;
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive) aliveCount++;
            }

            Vector3[] spread = FormationSpread.Distribute(position, aliveCount);

            int count = 0;
            ClearActiveMoveMarks();   // [P26] 새 이동 명령 → 기존 지속 마커 정리
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive)
                {
                    guard.SetCommandTarget(spread[count], false);
                    TrackMoveMark(guard, spread[count]);   // [P26] 병사별 도착까지 지속 링
                    count++;
                }
            }
            Debug.Log($"[RTSCommandSystem] {count}명 이동 명령 → {position} (링 분산 {spread.Length}지점)");
        }

        /// <summary>
        /// 일제 이동 명령 (Ctrl+우클릭) — 모든 병사가 동일한 지점으로 이동
        /// </summary>
        private void IssueSynchronizedMoveCommand(IReadOnlyList<GuardPlaceholder> selected, Vector3 position)
        {
            int count = 0;
            ClearActiveMoveMarks();   // [P26] 새 이동 명령 → 기존 지속 마커 정리
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive)
                {
                    guard.SetCommandTarget(position, false);
                    TrackMoveMark(guard, position);   // [P26] 동일 지점 — 전원 도착까지 링 잔존
                    count++;
                }
            }
            Debug.Log($"[RTSCommandSystem] {count}명 일제 이동 명령 → {position}");
        }

        // ===== [P26] 지속 이동 마커 관리 =====

        /// <summary>
        /// 미선택 폴백 — 선택 병사가 없으면 전체 소속(포섭) 병사 목록 반환(선택 필터와 동일 기준).
        /// </summary>
        private IReadOnlyList<GuardPlaceholder> GetFallbackGuards()
        {
            var all = new List<GuardPlaceholder>();
            foreach (var g in FindObjectsByType<GuardPlaceholder>())
            {
                if (g.IsAlive && (g.IsRecruited || g.gameObject.CompareTag("RecruitedSoldier")))
                    all.Add(g);
            }
            return all.AsReadOnly();
        }

        /// <summary>병사별 목표 지점에 지속 원형 링 표시(도착/취소/사망 시 자동 소멸).</summary>
        private void TrackMoveMark(GuardPlaceholder guard, Vector3 targetPos)
        {
            var mark = CommandMarker.Spawn(targetPos, null, guard);   // persistent
            if (mark != null) _activeMoveMarks.Add(mark);
        }

        /// <summary>모든 활성 지속 마커 제거.</summary>
        private void ClearActiveMoveMarks()
        {
            foreach (var m in _activeMoveMarks)
                if (m != null && m.gameObject != null) Object.Destroy(m.gameObject);
            _activeMoveMarks.Clear();
        }

        private void Update()
        {
            // [P26] 소유 병사 도착/취소/사망으로 자동 소멸된 마커를 추적 리스트에서 제거
            for (int i = _activeMoveMarks.Count - 1; i >= 0; i--)
            {
                var m = _activeMoveMarks[i];
                if (m == null || m.gameObject == null || !m.gameObject.activeInHierarchy)
                    _activeMoveMarks.RemoveAt(i);
            }
        }

        // ===== 유틸리티 =====

        /// <summary>
        /// 월드 좌표가 유효한 레이캐스트 적중점인지 확인
        /// </summary>
        public bool TryGetHitPoint(Vector3 mousePosition, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            if (_mainCamera == null) return false;

            Ray ray = _mainCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance, _commandLayerMask))
            {
                hitPoint = hit.point;
                return true;
            }
            return false;
        }
    }
}