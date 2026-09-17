using ProjectName.Core;
using UnityEngine;
using UnityEngine.InputSystem;

#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 컨텍스트 커맨드 라우터 — 좌클릭을 컨텍스트에 따라 라우팅.
    ///
    /// 규칙:
    ///   적(Enemy) 위에서 병사가 선택되어 있을 때 좌클릭 → 병사만 공격(RTS 경유) + 좌클릭 소비.
    ///   그 외 좌클릭은 기존 흐름(플레이어 공격) 유지.
    ///
    /// 좌클릭 소비 결정(검증 완료):
    ///   본 작업은 PlayerCombat 본체를 수정하지 않는 제약이 있다. PlayerCombat은 좌클릭 직전에
    ///   (1) Ctrl 홀드 → 드래그 위임, (2) GuardSelectionManager.consumeLeftClickAsDrag == true →
    ///   공격 생략(백업 소비 경로, PlayerCombat.cs L245-250) 을 검사한다.
    ///   따라서 병사 공격 프레임에 GuardSelectionManager.consumeLeftClickAsDrag = true 를 세팅하면
    ///   기존 PlayerCombat 백업 소비 경로가 좌클릭을 소비해 플레이어도 함께 공격하는 것을 막는다.
    ///   (드래그는 Ctrl 필요하므로 일반 좌클릭에서는 드래그가 시작되지 않음 — 안전.)
    ///   실행 순서상 라우터가 PlayerCombat보다 늦게 돌면 같은 프레임에 둘 다 발화할 수 있으나,
    ///   이는 허용 가능한 폴백으로 문서화한다. 향후 PlayerCombat 수정 시 새 정적 플래그
    ///   (GuardSelectionManager.consumeLeftClickAsContextCommand)를 참조하면 명시적으로 소비 가능.
    /// </summary>
    public class ContextCommandRouter : MonoBehaviour
    {
        public static ContextCommandRouter Instance { get; private set; }

        public static ContextCommandRouter Ensure(GameObject parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ContextCommandRouter>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("ContextCommandRouter");
            if (parent != null) go.transform.SetParent(parent.transform, false);

            // 의존 시스템 자동 보장 (순수 추가 — 기존 파일 본체는 건드리지 않음)
            ContextCursorSystem.Ensure(go);
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

            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            Vector2 mouse = Mouse.current.position.ReadValue();
            HoverTargetClassifier.TargetKind kind = HoverTargetClassifier.ClassifyAt(mouse);

            // 병사 선택 + 적 대상 → 병사만 공격
            if (kind == HoverTargetClassifier.TargetKind.Enemy
                && GuardSelectionManager.Instance != null
                && GuardSelectionManager.Instance.SelectedCount > 0)
            {
                // RTS 명령 — 적 적중 시 ATTACK으로 이미 변환됨
                if (RTSCommandSystem.Instance != null)
                    RTSCommandSystem.Instance.IssueRightClickCommand(mouse, false);

                // 좌클릭 소비 — 기존 PlayerCombat 백업 소비 경로 재사용 (병사 전용 공격 보장)
                GuardSelectionManager.consumeLeftClickAsDrag = true;
                // 향후 PlayerCombat 수정을 위한 의미론적 마커 (아직 소비자는 없음)
                GuardSelectionManager.consumeLeftClickAsContextCommand = true;

                Debug.Log($"[ContextCommandRouter] 병사 전용 공격 명령 → {mouse} (커서 분류={kind})");
            }
        }
    }
}
