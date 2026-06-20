using System.Collections.Generic;
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
        [SerializeField] private LayerMask _groundLayerMask = 1; // Default layer

        private Camera _mainCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mainCamera = Camera.main;
        }

        /// <summary>
        /// 현재 선택된 병사들을 가져옴 (GuardSelectionManager에 위임)
        /// </summary>
        private IReadOnlyList<GuardPlaceholder> GetSelectedGuards()
        {
            if (GuardSelectionManager.Instance != null)
                return GuardSelectionManager.Instance.SelectedGuards;
            return new List<GuardPlaceholder>().AsReadOnly();
        }

        // ===== 퍼블릭 API =====

        /// <summary>
        /// 우클릭 명령 — 적 대상이면 공격, 지형이면 이동
        /// Ctrl 키와 함께 누르면 모든 선택 병사가 동일한 지점을 타겟으로 일제 공격/이동
        /// </summary>
        public void IssueRightClickCommand(Vector3 mousePosition, bool ctrl = false)
        {
            if (_mainCamera == null) return;

            var selected = GetSelectedGuards();
            if (selected == null || selected.Count == 0)
            {
                Debug.Log("[RTSCommandSystem] 선택된 병사가 없어 명령 무시");
                return;
            }

            Ray ray = _mainCamera.ScreenPointToRay(mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance))
            {
                Debug.Log("[RTSCommandSystem] 레이캐스트 적중 실패");
                return;
            }

            // 적 대상 확인 (IDamageable)
            IDamageable target = hit.collider.GetComponent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                // 공격 명령
                if (ctrl)
                {
                    IssueSynchronizedAttackCommand(target, hit.point);
                }
                else
                {
                    IssueAttackCommand(target);
                }
            }
            else
            {
                // 이동 명령
                if (ctrl)
                {
                    IssueSynchronizedMoveCommand(hit.point);
                }
                else
                {
                    IssueMoveCommand(hit.point);
                }
            }
        }

        /// <summary>
        /// H키 — 모든 선택된 병사의 명령 취소 (공격 중단)
        /// </summary>
        public void StopAllSelectedGuards()
        {
            var selected = GetSelectedGuards();
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
        private void IssueAttackCommand(IDamageable target)
        {
            var selected = GetSelectedGuards();
            int count = 0;
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive)
                {
                    var targetTransform = (target as MonoBehaviour)?.transform;
                    Vector3 targetPos = targetTransform != null ? targetTransform.position : guard.transform.position;
                    guard.SetCommandTarget(targetPos, true);
                    guard.SetInCombat(true);
                    count++;
                }
            }
            Debug.Log($"[RTSCommandSystem] {count}명 공격 명령");
        }

        /// <summary>
        /// 일제 공격 명령 (Ctrl+우클릭) — 모든 병사가 동일한 타겟 위치를 공격
        /// </summary>
        private void IssueSynchronizedAttackCommand(IDamageable target, Vector3 hitPoint)
        {
            var selected = GetSelectedGuards();
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
            Debug.Log($"[RTSCommandSystem] {count}명 일제 공격 명령 → {attackPos}");
        }

        /// <summary>
        /// 이동 명령
        /// </summary>
        private void IssueMoveCommand(Vector3 position)
        {
            var selected = GetSelectedGuards();
            int count = 0;
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive)
                {
                    guard.SetCommandTarget(position, false);
                    count++;
                }
            }
            Debug.Log($"[RTSCommandSystem] {count}명 이동 명령 → {position}");
        }

        /// <summary>
        /// 일제 이동 명령 (Ctrl+우클릭) — 모든 병사가 동일한 지점으로 이동
        /// </summary>
        private void IssueSynchronizedMoveCommand(Vector3 position)
        {
            var selected = GetSelectedGuards();
            int count = 0;
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive)
                {
                    guard.SetCommandTarget(position, false);
                    count++;
                }
            }
            Debug.Log($"[RTSCommandSystem] {count}명 일제 이동 명령 → {position}");
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
            if (Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance))
            {
                hitPoint = hit.point;
                return true;
            }
            return false;
        }
    }
}