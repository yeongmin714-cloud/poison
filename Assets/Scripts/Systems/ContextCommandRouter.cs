using ProjectName.Core;
using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 컨텍스트 커맨드 라우터 — 좌클릭을 컨텍스트에 따라 라우팅.
    ///
    /// 규칙 (Phase 1 정비):
    ///   Ctrl 홀드 + 단순 클릭(드래그 이동량 &lt; 임계값) + UI 위 아님 + 병사 선택 있음 → 병사 명령 발화.
    ///   - Enemy 위   → 병사만 공격(RTS 경유) + 좌클릭 소비.
    ///   - Farm 위    → 선택 병사 전원 GuardTask.Farm 배정 + 좌클릭 소비.
    ///   - Gather 위  → 선택 병사 전원 GuardTask.Gather 배정 + 좌클릭 소비.
    ///   - Mine 위    → 선택 병사 전원 GuardTask.Mine 배정 + 좌클릭 소비.
    ///   그 외 좌클릭(플레이어 공격)과 Ctrl+드래그(박스 선택)는 기존 흐름 유지.
    ///
    ///   [신규] 단순 클릭 확정 시 커서 아래 실제 GameObject를 레이캐스트로 직접 판정해
    ///   (a) GuardPlaceholder → 병사 상호작용 통합창(F키와 동일, SoldierInteractBridge 경유),
    ///   (b) AnimalAI(몬스터) → 몬스터 정보창(SoldierInteractBridge 몬스터 이벤트 경유 —
    ///       Systems→UI 어셈블리 순환참조 회피) 을 우선 처리하고, 둘 다 아니면
    ///   (c) 기존 switch(kind) 로직(Enemy/Farm/Gather/Mine)을 그대로 따른다(회귀 방지).
    ///
    /// 드래그 선택과의 공존 (이동량으로 구분):
    ///   GuardSelectionManager는 Ctrl+좌클릭 down 시 드래그를 시작하고, 이동량이 _clickThreshold(10px)
    ///   초과한 채 release 되면 박스 선택을 확정한다. 본 라우터는 down 시 "대기 클릭"으로만 기록하고,
    ///   (a) 홀드 중 이동량이 임계값을 초과하거나 (b) 버튼이 먼저 떨어지면 대기를 취소/확정한다.
    ///   즉 이동량 &lt;= 임계값으로 release 된 경우에만 단순 클릭으로 확정해 명령을 발화하므로
    ///   드래그(선택)와 단순 클릭(명령)이 같은 좌클릭 위에서 충돌 없이 공존한다.
    ///
    /// 좌클릭 소비 결정(검증 완료):
    ///   본 작업은 PlayerCombat 본체를 수정하지 않는 제약이 있다. PlayerCombat은 좌클릭 직전에
    ///   (1) Ctrl 홀드 → 드래그 위임, (2) GuardSelectionManager.consumeLeftClickAsDrag == true →
    ///   공격 생략(백업 소비 경로) 을 검사한다.
    ///   따라서 병사 명령 프레임에 GuardSelectionManager.consumeLeftClickAsDrag = true 를 세팅하면
    ///   기존 PlayerCombat 백업 소비 경로가 좌클릭을 소비해 플레이어도 함께 공격하는 것을 막는다.
    ///   consumeLeftClickAsContextCommand는 향후 PlayerCombat 수정 시 참조할 의미론적 마커다.
    /// </summary>
    public class ContextCommandRouter : MonoBehaviour
    {
        public static ContextCommandRouter Instance { get; private set; }

        /// <summary>단순 클릭 판정 임계값(px) — GuardSelectionManager._clickThreshold(10f)와 동일 기준.</summary>
        private const float ClickThresholdPx = 10f;

        // 단순 클릭 대기 상태 — down 시 기록, 이동량 초과 시 드래그로 판정해 취소.
        private bool _pendingClick;
        private Vector2 _pressMouse;

        public static ContextCommandRouter Ensure(GameObject parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ContextCommandRouter>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("ContextCommandRouter");
            if (parent != null) go.transform.SetParent(parent.transform, false);

            // 의존 시스템 자동 보장 (순수 추가 — 기존 파일 본체는 건드리지 않음)
            // [P22-2 수리] 3D 월드 앵커 커서(ContextCursorSystem) 스폰 중단 — UTKCursorOverlay(화면 커서)가 대체.
            //   CursorVisibilityController는 OS 커서 숨김 담당으로 유지(커서 오버레이 전용 동작).
            CursorVisibilityController.Ensure(go);

            return go.AddComponent<ContextCommandRouter>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Mouse.current == null) return;
            var left = Mouse.current.leftButton;

            // ===== 단순 클릭 대기 중 — 이동량으로 드래그(선택)와 구분 =====
            if (_pendingClick)
            {
                Vector2 heldPos = Mouse.current.position.ReadValue();

                // release 프레임 우선 판정 — 이동량이 임계값 이내면 단순 클릭 확정
                if (left.wasReleasedThisFrame)
                {
                    _pendingClick = false;
                    if (Vector2.Distance(_pressMouse, heldPos) <= ClickThresholdPx)
                    {
                        TryIssueContextCommand(heldPos);
                    }
                    return;
                }

                // 홀드 중 이동량 초과 → 드래그(박스 선택)로 전환 — GuardSelectionManager에 위임, 명령 취소
                if (!left.isPressed || Vector2.Distance(_pressMouse, heldPos) > ClickThresholdPx)
                    _pendingClick = false;
                return;
            }

            if (!left.wasPressedThisFrame) return;

            // ===== 명령 후보 조건 — Ctrl 홀드 + UI 위 아님 + 병사 선택 =====
            if (!IsCtrlHeld()) return;
            if (UITransitionState.PointerOverUI) return;                    // UI 위 클릭은 월드 명령 아님
            if (GuardSelectionManager.Instance == null
                || GuardSelectionManager.Instance.SelectedCount == 0) return;

            // 즉시 발화하지 않고 대기 — release 시 이동량으로 단순 클릭/드래그 확정
            _pressMouse = Mouse.current.position.ReadValue();
            _pendingClick = true;
        }

        /// <summary>대기된 단순 클릭 확정 — 커서 분류에 따라 공격/작업 명령 발화.</summary>
        private void TryIssueContextCommand(Vector2 mouse)
        {
            // release 시점 재검사 — 프레임 사이 UI 진입/선택 해제 변동 대비
            if (UITransitionState.PointerOverUI) return;

            var gsm = GuardSelectionManager.Instance;
            if (gsm == null || gsm.SelectedCount == 0) return;

            // [신규] 커서 아래 실제 GameObject 직접 분기 — 병사 상호작용 > 몬스터 정보 우선.
            //   직접 대상(GuardPlaceholder/AnimalAI)을 못 찾으면 false → 기존 switch(kind) 로직 유지(회귀 방지).
            if (TryRouteDirectTarget(mouse)) return;

            HoverTargetClassifier.TargetKind kind = HoverTargetClassifier.ClassifyAt(mouse);
            switch (kind)
            {
                case HoverTargetClassifier.TargetKind.Enemy:
                    IssueAttackCommand(gsm, kind, mouse);
                    break;
                case HoverTargetClassifier.TargetKind.Farm:
                case HoverTargetClassifier.TargetKind.Gather:
                case HoverTargetClassifier.TargetKind.Mine:
                    AssignWorkTask(gsm, kind, mouse);
                    break;
            }
        }

        /// <summary>
        /// [신규] Ctrl+좌클릭(단순 클릭) 대상 직접 라우팅 — 레이캐스트로 커서 아래 실제 GameObject 획득.
        ///   (a) GuardPlaceholder → SoldierInteractBridge.RaiseInteract(guard) — 병사 상호작용 통합창
        ///       (F키와 동일 경로) + 좌클릭 소비 후 true.
        ///   (b) AnimalAI(태그 Monster 몬스터) → 몬스터 정보창 + 좌클릭 소비 후 true.
        ///       Systems 어셈블리에서 UI(MonsterInfoUTK)를 직접 호출하면 asmdef 순환참조라
        ///       SoldierInteractBridge 몬스터 이벤트(RaiseMonsterInfo)로 발화한다(동일 확립 패턴).
        ///   둘 다 아니면 false — 호출부의 기존 switch(kind) 로직이 그대로 실행된다.
        ///
        /// 레이캐스트 수식은 HoverTargetClassifier.ClassifyAt과 동일:
        ///   Camera.main.ScreenPointToRay(mouse) + Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide).
        /// 몬스터는 종종 부모 루트에 AnimalAI가 있고 자식 콜라이더가 히트되므로 GetComponentInParent 필수.
        /// </summary>
        private static bool TryRouteDirectTarget(Vector2 mouse)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(mouse);
            RaycastHit[] hits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return false;

            // RaycastAll 결과 순서는 거리 비보장 — 가장 가까운 히트부터 판정(전경 대상 우선).
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;
                var hitGo = hit.collider.gameObject;
                if (hitGo == null) continue;

                // (a) 병사 — 자식 콜라이더 히트도 부모 체인 검색으로 커버. 사망 병사는 기존 분류 로직에 위임.
                var guard = hitGo.GetComponentInParent<GuardPlaceholder>();
                if (guard != null && guard.IsAlive)
                {
                    SoldierInteractBridge.RaiseInteract(guard);   // F키 상호작용창과 동일 경로
                    ConsumeLeftClick();
                    Debug.Log($"[ContextCommandRouter] 병사 상호작용창 → {guard.GuardName} (직접 레이캐스트)");
                    return true;
                }

                // (b) 몬스터 — GetComponentInParent 필수(자식 콜라이더 히트 커버). 사망체는 기존 분류 로직에 위임.
                var ai = hitGo.GetComponentInParent<AnimalAI>();
                if (ai != null && ai.IsAlive)
                {
                    SoldierInteractBridge.RaiseMonsterInfo(ai);   // MonsterInfoUTK 정보창 (브리지 이벤트 경유)
                    ConsumeLeftClick();
                    Debug.Log($"[ContextCommandRouter] 몬스터 정보창 → {ai.MonsterId} (직접 레이캐스트)");
                    return true;
                }
            }

            return false;
        }

        /// <summary>적 위 Ctrl+좌클릭 — 기존 RTS 공격 경로 유지(회귀 방지) + 좌클릭 소비.</summary>
        private void IssueAttackCommand(GuardSelectionManager gsm,
            HoverTargetClassifier.TargetKind kind, Vector2 mouse)
        {
            // RTS 명령 — 적 적중 시 ATTACK으로 이미 변환됨
            if (RTSCommandSystem.Instance != null)
                RTSCommandSystem.Instance.IssueRightClickCommand(mouse, false);

            ConsumeLeftClick();
            Debug.Log($"[ContextCommandRouter] 병사 전용 공격 명령 → {mouse} (커서 분류={kind})");
        }

        /// <summary>작업 대상(Farm/Gather/Mine) Ctrl+좌클릭 — 선택 병사 전원에 역할 배정 + 좌클릭 소비.</summary>
        private void AssignWorkTask(GuardSelectionManager gsm,
            HoverTargetClassifier.TargetKind kind, Vector2 mouse)
        {
            GuardTaskSystem.GuardTask task;
            switch (kind)
            {
                case HoverTargetClassifier.TargetKind.Farm:   task = GuardTaskSystem.GuardTask.Farm;   break;
                case HoverTargetClassifier.TargetKind.Gather: task = GuardTaskSystem.GuardTask.Gather; break;
                case HoverTargetClassifier.TargetKind.Mine:   task = GuardTaskSystem.GuardTask.Mine;   break;
                default: return;
            }

            // 싱글턴 부트스트랩 — 씬에 없으면 Ensure로 보장
            GuardTaskSystem tasks = GuardTaskSystem.Instance != null
                ? GuardTaskSystem.Instance
                : GuardTaskSystem.Ensure();
            if (tasks == null) return;

            int n = 0;
            var selected = gsm.SelectedGuards;          // 인덱스 루프 — foreach 멤버 수정 회피 규칙
            for (int i = 0; i < selected.Count; i++)
            {
                var g = selected[i];
                if (g == null) continue;
                tasks.AssignTask(g, task);
                n++;
            }

            ConsumeLeftClick();
            Debug.Log($"[ContextCommandRouter] 작업 명령 {task} → {n}명 (커서 분류={kind}, 지점={mouse})");
        }

        /// <summary>좌클릭 소비 — 기존 PlayerCombat 백업 소비 경로 + 의미론적 마커 재사용.</summary>
        private static void ConsumeLeftClick()
        {
            GuardSelectionManager.consumeLeftClickAsDrag = true;
            GuardSelectionManager.consumeLeftClickAsContextCommand = true;
        }

        /// <summary>Ctrl 홀드 여부 — 좌/우 Ctrl 모두 인정 (GuardSelectionManager와 동일 판정).</summary>
        private static bool IsCtrlHeld()
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb.ctrlKey.isPressed || kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
        }
    }
}
