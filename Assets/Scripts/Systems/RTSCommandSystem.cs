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

        // [무번호 28-S3] 스태거 세대 가드 — 새 명령이 오면 대기 중 지연분 폐기(결정론)
        private int _moveStaggerGeneration;

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

        private Camera ResolveMainCamera()
        {
            // 카메라가 이 컴포넌트보다 늦게 활성화되는 경우도 명령 시점에 재탐색
            if (_mainCamera == null || !_mainCamera.gameObject.activeInHierarchy)
                _mainCamera = Camera.main;
            return _mainCamera;
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
            // [104차 F1] 건설 홀로그램 배치 중 — 우클릭 명령 무시 (배치 클릭과 충돌 방지)
            if (ConstructionHologram.IsPlacementActive) return;
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
            Camera commandCamera = ResolveMainCamera();
            if (commandCamera == null) { Debug.LogWarning("[RTSCommandSystem][P20-7] 카메라 null — 명령 불가"); return; }

            if (selected == null || selected.Count == 0)
            {
                Debug.Log("[RTSCommandSystem] 명령 내릴 병사 없음 (선택 & 소속 병사 모두 없음)");
                return;
            }

            Ray ray = commandCamera.ScreenPointToRay(mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance, _commandLayerMask))
            {
                Debug.Log("[RTSCommandSystem] 레이캐스트 적중 실패");
                return;
            }

            // 적 대상 확인 (IDamageable)
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                // 공격 명령 — 페이드 마커(즉시), selected 전달(중복 호출 방지)
                if (ctrl) IssueSynchronizedAttackCommand(selected, target, hit.point);
                else IssueAttackCommand(selected, target, hit.point);
                RTSCommandFeedback.PlayAttackAck();   // [15차 A1] 공격 확인음
            }
            else
            {
                // 이동 명령 — 지속 지면 원형 링(도착까지), selected 전달
                if (ctrl) IssueSynchronizedMoveCommand(selected, hit.point);
                else IssueMoveCommand(selected, hit.point);
                RTSCommandFeedback.PlayMoveAck();   // [15차 A1] 이동 확인음
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
                    guard.StopNow();   // [15차] 이동/공격/홀드 즉시 취소 + 유휴
                }
            }

            Debug.Log($"[RTSCommandSystem] {selected.Count}명 정지 (X키)");
        }

        /// <summary>[15차 사용자 확정] 홀드 — 선택 병사 제자리 고정 + 사정거리 내 자동 반격.</summary>
        public void HoldSelectedGuards()
        {
            var selected = GetSelectedGuards();
            ClearActiveMoveMarks();
            if (selected == null || selected.Count == 0)
            {
                Debug.Log("[RTSCommandSystem] 홀드할 병사 없음");
                return;
            }

            int count = 0;
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive)
                {
                    guard.SetHold(true);
                    count++;
                }
            }

            Debug.Log($"[RTSCommandSystem] {count}명 홀드 (H키)");
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
                    if (targetTransform != null) guard.SetExplicitAttackTarget(target, attackPos);
                    else guard.SetCommandTarget(attackPos, true);
                    guard.SetInCombat(true);
                    AttackLinkFX.Spawn(guard.transform.position, attackPos);   // [15차 B4] 연결선
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
                    if (targetTransform != null) guard.SetExplicitAttackTarget(target, attackPos);
                    else guard.SetCommandTarget(attackPos, true);
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
            IssueMoveCommandCore(selected, position);
        }

        /// <summary>이동 명령 본체 — 화면 레이캐스트 경로와 월드 직행(미니맵) 경로가 공유.</summary>
        private void IssueMoveCommandCore(IReadOnlyList<GuardPlaceholder> selected, Vector3 position)
        {
            var aliveGuards = new List<GuardPlaceholder>();
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive) aliveGuards.Add(guard);
            }
            if (aliveGuards.Count == 0)
            {
                Debug.Log("[RTSCommandSystem] 이동 명령 대상 없음(생존 0)");
                return;
            }

            // 분산 지점 수 = 생존 병사 수 (죽은 병사는 제외)
            Vector3[] spread = FormationSpread.Distribute(position, aliveGuards.Count);

            ClearActiveMoveMarks();   // [P26] 새 이동 명령 → 기존 지속 마커 정리
            // [무번호 28-S3] 그룹 이동 스태거 — 유닛별 0.04s 리듬 발화(부대 이동감). 동기화 계열은 기존 목적 유지(미적용).
            //   세대 가드: 대기 중 새 명령이 오면 지연분 전부 폐기(만료 명령이 늦게 덮어쓰는 것 방지).
            //   폴백: 컴포넌트 비활성 시 코루틴 불가 → 기존 동기 경로 즉시 발화(기존 동작 등가).
            int generation = ++_moveStaggerGeneration;
            if (!isActiveAndEnabled)
            {
                for (int i = 0; i < aliveGuards.Count; i++)
                {
                    aliveGuards[i].SetCommandTarget(spread[i], false);
                }
            }
            else
            {
                for (int i = 0; i < aliveGuards.Count; i++)
                {
                    StartCoroutine(DelayedMoveCommand(aliveGuards[i], spread[i], i * StaggerStepSeconds, generation));
                }
            }
            // [16차 사용자 요구] 경로 점 제거 — 최종 지점 링만 표시
            // [51차 사용자 요구 — 이동 링 1개] 병사별 지속 링 N개 폐기 → 명령 지점(클릭점)에 **링 1개**.
            //   그룹 소유 — 전원 도착(명령 해제)/사망 시 소멸(CommandMarker 그룹 판정).
            var mark = CommandMarker.Spawn(position, null, null, CommandRingPresentation.MoveMarkerScale, aliveGuards);
            if (mark != null) _activeMoveMarks.Add(mark);
            Debug.Log($"[RTSCommandSystem] {aliveGuards.Count}명 이동 명령 → {position} (링 1개 — 분산 {spread.Length}지점)");
        }

        /// <summary>[무번호 28-S3] 유닛별 지연 발화 — 인덱스 기반 결정론 지연(delay = index × StaggerStepSeconds), 세대 불일치 시 폐기.</summary>
        private System.Collections.IEnumerator DelayedMoveCommand(GuardPlaceholder guard, Vector3 position, float delay, int generation)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);   // 결정론 지연(인덱스 기반 — Random 0건)
            if (generation != _moveStaggerGeneration) yield break;    // 세대 불일치 — 폐기
            if (guard == null) yield break;
            guard.SetCommandTarget(position, false);
        }

        /// <summary>[무번호 28-S3] 스태거 간격(초) — 유닛당 리듬 간격(계획서 확정값 0.04s).</summary>
        private const float StaggerStepSeconds = 0.04f;

        /// <summary>
        /// 일제 이동 명령 (Ctrl+우클릭) — [51차 사용자 요구] 동일 지점이어도 **겹침 금지**: 전원 동일 좌표 대신
        /// FormationSpread 분산 슬롯 배정(기본 이동과 동일 규칙). 링도 명령 지점 1개만.
        /// </summary>
        private void IssueSynchronizedMoveCommand(IReadOnlyList<GuardPlaceholder> selected, Vector3 position)
        {
            var aliveGuards = new List<GuardPlaceholder>();
            foreach (var guard in selected)
            {
                if (guard != null && guard.IsAlive) aliveGuards.Add(guard);
            }
            if (aliveGuards.Count == 0)
            {
                Debug.Log("[RTSCommandSystem] 일제 이동 명령 대상 없음(생존 0)");
                return;
            }

            Vector3[] spread = FormationSpread.Distribute(position, aliveGuards.Count);
            ClearActiveMoveMarks();   // [P26] 새 이동 명령 → 기존 지속 마커 정리
            for (int i = 0; i < aliveGuards.Count; i++)
            {
                aliveGuards[i].SetCommandTarget(spread[i], false);
            }
            var mark = CommandMarker.Spawn(position, null, null, CommandRingPresentation.MoveMarkerScale, aliveGuards);
            if (mark != null) _activeMoveMarks.Add(mark);
            Debug.Log($"[RTSCommandSystem] {aliveGuards.Count}명 일제 이동 명령 → {position} (링 1개 — 분산 적용, 겹침 금지)");
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

        /// <summary>[15차 A4] 미니맵 우클릭 등 월드 좌표 직행 이동 — 화면 레이캐스트 우회.</summary>
        public void IssueMoveCommandAtWorld(Vector3 position)
        {
            var selected = GetSelectedGuards();
            if (selected == null || selected.Count == 0)
            {
                Debug.Log("[RTSCommandSystem] 이동 명령 대상 없음 (월드 직행)");
                return;
            }
            IssueMoveCommandCore(selected, position);
            RTSCommandFeedback.PlayMoveAck();
        }

        /// <summary>[51차 폐기] 병사별 지속 링 스폰 — 이동 링 1개 요구로 IssueMoveCommandCore/
        ///   IssueSynchronizedMoveCommand가 명령 지점 그룹 링 1개를 직접 스폰. 잔존 호출부 없음.</summary>

        /// <summary>모든 활성 지속 마커 제거.</summary>
        private void ClearActiveMoveMarks()
        {
            foreach (var m in _activeMoveMarks)
                if (m != null && m.gameObject != null) Object.Destroy(m.gameObject);
            _activeMoveMarks.Clear();
        }

        private void Update()
        {
            // [16차 사용자 요구] 컨텍스트 커서 제거 — 기본 커서 유지(상호작용 프롬프트만 표시)

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
            Camera commandCamera = ResolveMainCamera();
            if (commandCamera == null) return false;

            Ray ray = commandCamera.ScreenPointToRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, _raycastMaxDistance, _commandLayerMask))
            {
                hitPoint = hit.point;
                return true;
            }
            return false;
        }
    }
}