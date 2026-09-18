using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;   // PlayerInventory
using ProjectName.UI;     // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U2 — 창 간 드래그앤드롭 코어 (UTK 전용, IMGUI ItemDragContext 미사용).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// [원본 계약] Assets/Scripts/UI/ItemDragContext.cs (240줄) 의 UTK 포팅.
    ///   - UTKDragPayload   : 원본 ItemDragContext 정적 필드(Active/Source/SourceIndex/TerritoryId/Item/Icon)를
    ///                         한 객체로 묶은 셔스루 페이로드.
    ///   - UTKDragDrop       : Begin/Cancel/Complete 정적 진입점 + 드롭 타겟 레지스트리 + 고스트 렌더.
    ///   - IUTKDragSource    : 드래그 시작 통지(소스 창이 필요 시 구현).
    ///   - IUTKDropTarget    : CanDrop/Drop 판정. UTKDragDrop.RegisterDropTarget으로 등록.
    ///   - MakeDraggable     : 임계거리(6px) 기반 Pointer 드래그 헬퍼 — 미만은 클릭(onClick 콜백으로 전달).
    ///
    /// [고스트] UIRoot에 절대배치 VisualElement(background-image=Icon), PointerMove 추적.
    ///   드롭 실패 시 원위치 애니 없이 즉시 소멸(Phase C 스냅/펄스는 후속 라운드).
    /// </summary>

    // ─────────────────────────────────────────────────────────────
    // UTKDragPayload — 원본 ItemDragContext 정적 상태의 셔스루 객체화
    // ─────────────────────────────────────────────────────────────
    public enum UTKDragSourceKind { None, Inventory, Loot, Equipment, Warehouse, Hotbar }

    public class UTKDragPayload
    {
        public UTKDragSourceKind Source = UTKDragSourceKind.None;
        public int SourceIndex = -1;                 // Inventory: 전역 슬롯 인덱스 / Loot: 바구니 항목 인덱스
        public string TerritoryId = null;            // Warehouse 소스일 때 territoryId
        public PlayerInventory.ItemData Item = null; // 드래그 중 아이템 (null이면 무효 페이로드)
        public Texture2D Icon = null;                // 고스트 아이콘 (nullable — 폴백 사각형)
    }

    // ─────────────────────────────────────────────────────────────
    // 드래그 소스 / 드롭 타겟 인터페이스
    // ─────────────────────────────────────────────────────────────
    public interface IUTKDragSource
    {
        /// <summary>드래그 시작 통지 (임계거리 초과 직후, 고스트 생성 전).</summary>
        void BeginDrag(UTKDragPayload payload);
    }

    public interface IUTKDropTarget
    {
        /// <summary>이 타겟에 드롭 가능한지.</summary>
        bool CanDrop(UTKDragPayload payload);

        /// <summary>드롭 수행. 성공(소비) 시 true.</summary>
        bool Drop(UTKDragPayload payload);
    }

    // ─────────────────────────────────────────────────────────────
    // UTKDragDrop — 드래그 라이프사이클 + 드롭 타겟 레지스트리 + 고스트
    // ─────────────────────────────────────────────────────────────
    public static class UTKDragDrop
    {
        public static bool Active;                   // 원본 ItemDragContext.Active 패리티
        public static UTKDragPayload Payload;        // 현재 드래그 페이로드 (없으면 null)

        /// <summary>드롭 판정 시점의 스크린 좌표 (UIRoot 패널 좌표, y 하강). Complete 직전 갱신.</summary>
        public static Vector2 LastDropPos = Vector2.zero;

        /// <summary>MakeDraggable 임계거리(px) — 이거리 미만이면 클릭 유지.</summary>
        public const float DragThresholdPx = 6f;
        private const float GhostSize = 64f;

        private static VisualElement _ghost;
        private static Vector2 _ghostPos = Vector2.zero;
        private static DragSession _drag;

        // 드롭 타겟 레지스트리 (후등록 = 상위 우선 — FindDropTargetAt 역순 순회)
        private static readonly List<DropTargetBinding> _targets = new List<DropTargetBinding>();

        // ─────────────────────────────── 드래그 진입점 ───────────────────────────────

        /// <summary>드래그 시작. payload 또는 Item이 null이면 무시.(원본 ItemDragContext.Begin 패리티)</summary>
        public static void Begin(UTKDragPayload payload)
        {
            if (payload == null || payload.Item == null) return;

            Active = true;
            Payload = payload;
            if (payload.Icon == null)
                payload.Icon = ItemIconDatabase.GetOrCreateIcon(payload.Item);

            ShowGhost();
            _ghostPos = PointerScreenPos();
            MoveGhost(_ghostPos);
        }

        /// <summary>드래그 취소/종료 — 고스트 즉시 소멸(애니 없음), 상태 클리어.</summary>
        public static void Cancel()
        {
            HideGhost();
            Active = false;
            Payload = null;
        }

        /// <summary>
        /// 드롭 완료 — 레지스트리에서 커서 아래 최상위 타겟을 찾아 CanDrop→Drop.
        /// 성공/실패 무관하게 드래그 종료(고스트 즉시 소멸). 드롭 성공(또는 소비) 여부 반환.
        /// </summary>
        public static bool Complete()
        {
            if (!Active || Payload == null)
            {
                Cancel();
                return false;
            }

            bool consumed = false;
            var target = FindDropTargetAt(_ghostPos);
            if (target != null)
            {
                LastDropPos = _ghostPos;
                if (target.CanDrop(Payload))
                    consumed = target.Drop(Payload);
            }

            Cancel();
            return consumed;
        }

        // ─────────────────────────────── 드롭 타겟 레지스트리 ───────────────────────────────

        /// <summary>드롭 타겟 등록. 같은 element 재등록 시 타겟 교체(중복 방지).</summary>
        public static void RegisterDropTarget(VisualElement el, IUTKDropTarget target)
        {
            if (el == null || target == null) return;
            foreach (var b in _targets)
            {
                if (b.element == el)
                {
                    b.target = target;
                    return;
                }
            }
            _targets.Add(new DropTargetBinding(el, target));
        }

        /// <summary>드롭 타겟 해제.</summary>
        public static void UnregisterDropTarget(VisualElement el)
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                if (_targets[i].element == el)
                {
                    _targets.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>화면 좌표 아래 최상위(후등록 우선) 드롭 타겟 반환.</summary>
        public static IUTKDropTarget FindDropTargetAt(Vector2 screenPos)
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                var b = _targets[i];
                if (b.element == null || !b.element.visible) continue;
                var gb = b.element.worldBound;
                if (gb.width <= 0f || gb.height <= 0f) continue;
                if (screenPos.x >= gb.x && screenPos.x <= gb.x + gb.width
                    && screenPos.y >= gb.y && screenPos.y <= gb.y + gb.height)
                {
                    return b.target;
                }
            }
            return null;
        }

        // ─────────────────────────────── MakeDraggable 헬퍼 ───────────────────────────────

        /// <summary>
        /// 임계거리(6px) 기반 드래그 등록. 6px 미만 이동 후 PointerUp = 클릭 유지 → onClick 콜백으로 전달.
        /// 임계 초과 시 factory로 페이로드를 만들어 드래그 시작(엘리먼트가 IUTKDragSource면 BeginDrag 통지).
        /// </summary>
        public static void MakeDraggable(VisualElement ve, System.Func<UTKDragPayload> factory = null,
                                         System.Action onClick = null)
        {
            if (ve == null) return;
            ve.RegisterCallback<PointerDownEvent>(evt => OnDragPointerDown(evt, ve, factory, onClick));
            ve.RegisterCallback<PointerMoveEvent>(evt => OnDragPointerMove(evt));
            ve.RegisterCallback<PointerUpEvent>(evt => OnDragPointerUp(evt));
            ve.RegisterCallback<PointerCaptureOutEvent>(evt => OnDragCaptureOut(evt));
        }

        private static void OnDragPointerDown(PointerDownEvent evt, VisualElement ve,
                                              System.Func<UTKDragPayload> factory, System.Action onClick)
        {
            if (evt.button != 0 || _drag != null) return;
            _drag = new DragSession();
            _drag.element = ve;
            _drag.factory = factory;
            _drag.onClick = onClick;
            _drag.downPos = evt.position;
            _drag.engaged = false;

            // 캡처 즉시 선점 → PointerMove/Up 이벤트를 엘리먼트가 계속 수신(영역 밖 이동 대비).
            ve.CapturePointer(evt.pointerId);
            _drag.captured = true;

            evt.StopPropagation();
        }

        /// <summary>[U8 수리] 수신 엘리먼트 로컬 좌표 → UIRoot 패널 좌표 변환.
        /// PointerMove/Up의 evt.position은 캡처된 엘리먼트 기준 로컬값이라 그대로 쓰면
        /// 고스트/드롭 판정이 어긋난다(Play 실측 드래그 불능 뿌리).</summary>
        private static Vector2 ToRootPos(VisualElement el, Vector2 localPos)
        {
            var world = el.LocalToWorld(localPos);
            var root = UIToolkitBootstrap.UIRoot;
            return root != null ? root.WorldToLocal(world) : world;
        }

        private static void OnDragPointerMove(PointerMoveEvent evt)
        {
            if (_drag == null || !_drag.captured) return;

            if (!_drag.engaged)
            {
                float dx = evt.position.x - _drag.downPos.x;
                float dy = evt.position.y - _drag.downPos.y;
                if (Mathf.Sqrt(dx * dx + dy * dy) < DragThresholdPx)
                    return;   // 임계 미만 — 아직 클릭 유지

                // 임계 초과 → 드래그 시작 (허용 룰: factory 페이로드 필요)
                var payload = _drag.factory != null ? _drag.factory.Invoke() : null;
                if (payload == null || payload.Item == null)
                    return;   // 소스 페이로드 없음 — 클릭 유지 취급

                _drag.engaged = true;
                if (_drag.element is IUTKDragSource source)
                    source.BeginDrag(payload);
                Begin(payload);
            }

            _ghostPos = ToRootPos(_drag.element, evt.position);   // [U8 수리] 로컬→루트 변환
            MoveGhost(_ghostPos);
            evt.StopPropagation();
        }

        private static void OnDragPointerUp(PointerUpEvent evt)
        {
            if (_drag == null) return;

            var drag = _drag;
            _drag = null;   // [U8 수리] 먼저 세션 분리 — ReleasePointer가 동기 발화하는
                            // PointerCaptureOut(OnDragCaptureOut)이 Complete를 먹어버리는 순서 버그 제거

            bool engaged = drag.engaged;
            if (engaged)
            {
                _ghostPos = ToRootPos(drag.element, evt.position);   // 로컬→루트 변환
                LastDropPos = _ghostPos;
                MoveGhost(_ghostPos);
            }

            if (drag.captured)
                drag.element.ReleasePointer(evt.pointerId);

            if (engaged)
            {
                Complete();
            }
            else if (drag.onClick != null)
            {
                // 임계 미만 — 클릭으로 취급, onClick 핸들러로 전달
                drag.onClick.Invoke();
            }

            evt.StopPropagation();
        }

        /// <summary>캡처 상실(외부 간섭) — 드래그 중이면 취소(고스트 즉시 소멸).</summary>
        private static void OnDragCaptureOut(PointerCaptureOutEvent evt)
        {
            if (_drag == null) return;
            if (_drag.engaged)
                Cancel();
            _drag = null;
        }

        // ─────────────────────────────── 고스트 렌더 ───────────────────────────────

        private static void ShowGhost()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            if (_ghost == null || _ghost.parent != root)
            {
                _ghost = new VisualElement();
                _ghost.name = "DragGhost";
                _ghost.style.position = Position.Absolute;
                _ghost.style.width = GhostSize;
                _ghost.style.height = GhostSize;
                _ghost.style.opacity = 0.8f;   // 반투명 팔로우
                _ghost.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.35f));
                _ghost.pickingMode = PickingMode.Ignore;
                root.Add(_ghost);
            }
            if (Payload != null && Payload.Icon != null)
                _ghost.style.backgroundImage = new StyleBackground(Background.FromTexture2D(Payload.Icon));
            else
                _ghost.style.backgroundImage = StyleKeyword.Null;
        }

        private static void MoveGhost(Vector2 panelPos)
        {
            if (_ghost == null) return;
            _ghost.style.left = panelPos.x - GhostSize * 0.5f;
            _ghost.style.top = panelPos.y - GhostSize * 0.5f;
        }

        /// <summary>고스트 제거 — 애니 없이 즉시(드롭 실패/성공/취소 공통).</summary>
        private static void HideGhost()
        {
            if (_ghost != null)
            {
                if (_ghost.parent != null)
                    _ghost.RemoveFromHierarchy();
                _ghost = null;
            }
        }

        private static Vector2 PointerScreenPos()
            => _ghostPos;

        // ─────────────────────────────── 내부 상태 ───────────────────────────────

        private sealed class DropTargetBinding
        {
            public VisualElement element;
            public IUTKDropTarget target;

            public DropTargetBinding(VisualElement el, IUTKDropTarget t)
            {
                element = el;
                target = t;
            }
        }

        private sealed class DragSession
        {
            public VisualElement element;
            public System.Func<UTKDragPayload> factory;
            public System.Action onClick;
            public Vector2 downPos = Vector2.zero;
            public bool engaged;
            public bool captured;
        }
    }
}