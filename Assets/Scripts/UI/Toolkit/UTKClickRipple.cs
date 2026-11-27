using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [클릭 빛물결 FX] — UI 클릭 지점에서 퍼지는 원형 빛 물결(순백) 오버레이.
    /// CraftBenchBaseUTK.StartCraftCompletionFx 선례 계승:
    ///   오버레이(pickingMode Ignore · 절대좌표) + schedule.Execute().Every(16L) 드라이버 + 세대 토큰(연타/재부착 레이스 수리).
    ///  - 원형은 border-radius 스타일링 — 벡터메시 금지(84차 계약) 준수(Mesh 생성 0).
    ///  - 물결 요소는 클릭 시 생성/수명 종료 시 제거 — 상시 렌더 무변화(기존 창/UI 렌더 계약 유지).
    ///  - 동시 물결 예산(MaxRipples) 초과 시 가장 오래된 물결 즉시 정리(과다 연타 방어).
    ///  - PointerDown 훅은 UIRoot TrickleDown — 모든 UI 요소 클릭만 포착(빈 화면 월드 클릭은 가드로 무물결).
    /// </summary>
    public class UTKClickRipple : VisualElement
    {
        private static UTKClickRipple _instance;
        private static long _generation;   // 세대 토큰 — Ensure 재부착 시 선점 증가, 구세대 드라이버의 신세대 간섭 차단

        // ── 연출 상수(소스계약 핀) ──
        internal const float RippleLifeSeconds = 0.45f;    // 물결 수명(확산+페이드 총합)
        internal const float RippleStartScale = 0.25f;     // 시작 스케일(0.25 → 1.0 EaseOutQuad 확산)
        internal const float RipplePeakOpacity = 0.85f;    // 시작 피크 알파(이후 선형 페이드아웃)
        internal const float RippleDiameter = 64f;         // 최종 지름(px) — [185차] 96→64 사용자 요구 "조금 더 작게"
        internal const int MaxRipples = 6;                 // 동시 물결 예산(연타 상한)

        // 순백(사용자 확정 — 중립적 클릭 발광) 링 + 은은한 백색 내부 빛
        internal static readonly Color RippleRingColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        internal static readonly Color RippleFillColor = new Color(1f, 1f, 1f, 0.10f);

        private readonly List<VisualElement> _active = new List<VisualElement>();   // 활성 물결(0번 = 가장 오래된 것)
        private readonly long _myGeneration;   // 이 인스턴스가 속한 세대(구세대 드라이버 자가 종료 판정용)
        private VisualElement _hookRoot;       // PointerDown 훅이 걸린 루트(탈착 시 정확히 해제하기 위한 캐시)
        private bool _hooked;

        /// <summary>외부 강제 보장용 — 멱등(중복 실행 무시). UTKCursorOverlay Ensure 스타일.</summary>
        public static UTKClickRipple Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _generation++;   // 신규 세대 선점 — 구세대 인스턴스의 드라이버는 세대 불일치로 자가 종료(연타/재부착 레이스 수리)
            _instance = new UTKClickRipple();
            root.Add(_instance);
            Debug.Log("[UTKClickRipple] 클릭 빛물결 오버레이 부착 — 클릭 지점 원형 물결(벡터메시 0)");
            return _instance;
        }

        private UTKClickRipple()
        {
            _myGeneration = _generation;
            name = "UTKClickRipple";
            pickingMode = PickingMode.Ignore;           // 클릭은 실제 대상으로 — 오버레이는 시각 전용(포인터 흡수 금지)
            style.position = Position.Absolute;
            style.left = 0f;
            style.top = 0f;
            style.width = new Length(100f, LengthUnit.Percent);
            style.height = new Length(100f, LengthUnit.Percent);

            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);
        }

        // ─────────────────────────── PointerDown 훅(루트 TrickleDown) ───────────────────────────

        private void OnAttached(AttachToPanelEvent evt)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            _hookRoot = root;
            if (_hooked) return;
            _hooked = true;
            // 루트 TrickleDown — 이벤트 대상이 어떤 창/버튼이든(또는 빈 화면 폴백 루트든) 물결은 항상 포착
            root.RegisterCallback<PointerDownEvent>(OnRootPointerDown, TrickleDown.TrickleDown);
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            if (_hooked && _hookRoot != null)
            {
                _hookRoot.UnregisterCallback<PointerDownEvent>(OnRootPointerDown);
                _hooked = false;
            }
            _hookRoot = null;
            ClearRipples();   // 탈착 시 잔여 물결 일괄 제거 — 각 드라이버는 ring.panel null 가드로 자가 정지
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            // UI 전용 가드 — 빈 화면(월드) 클릭 무물결(사용자 계약: "모든 UI에서 클릭이 필요할 때" = UI 클릭만).
            //  피킹 결과가 루트 자신이거나 없으면(null) UI 클릭이 아니므로 물결 없이 통과.
            if (evt.target == null || evt.target == _hookRoot) return;
            // evt.position = 패널(월드) 좌표 → 오버레이 로컬 좌표 변환 1회(RecommendedTaxWindowUTK _slider.WorldToLocal 선례 — 확장 메서드라 수신자 명시)
            SpawnRipple(this.WorldToLocal(evt.position));
        }

        // ─────────────────────────── 물결 스폰 + 16ms 드라이버 ───────────────────────────

        private void SpawnRipple(Vector2 center)
        {
            // 오버레이 재선두 — 열린 창들 위로 물결을 렌더하기 위함(다음 창 개방 시 그 창이 앞으로, 클릭마다 재선두).
            //  커서 오버레이는 자체 BringToFront 유지(선례) — 커서는 물결 위에 유지된다.
            this.BringToFront();

            // 예산 초과 — 가장 오래된 물결 즉시 정리(연타 방어; 잘린 물결의 드라이버는 ring.panel null 가드로 자가 정지)
            if (_active.Count >= MaxRipples)
            {
                var evicted = _active[0];
                _active.RemoveAt(0);
                if (evicted != null) evicted.RemoveFromHierarchy();
            }

            // 페이즈 1 — 링 오버레이(클릭 시에만 생성 → 기존 렌더 무변화)
            var ring = new VisualElement { name = "ClickRipple", pickingMode = PickingMode.Ignore };
            ring.style.position = Position.Absolute;
            ring.style.width = RippleDiameter;
            ring.style.height = RippleDiameter;
            ring.style.left = center.x - RippleDiameter * 0.5f;   // 중심 = 클릭 지점
            ring.style.top = center.y - RippleDiameter * 0.5f;
            // 원형 = border-radius 반지름(벡터메시 금지 84차 — 스타일링 원형만 사용)
            float half = RippleDiameter * 0.5f;
            ring.style.borderTopLeftRadius = half;
            ring.style.borderTopRightRadius = half;
            ring.style.borderBottomLeftRadius = half;
            ring.style.borderBottomRightRadius = half;
            ring.style.borderTopWidth = 2f;
            ring.style.borderRightWidth = 2f;
            ring.style.borderBottomWidth = 2f;
            ring.style.borderLeftWidth = 2f;
            ring.style.borderTopColor = RippleRingColor;
            ring.style.borderRightColor = RippleRingColor;
            ring.style.borderBottomColor = RippleRingColor;
            ring.style.borderLeftColor = RippleRingColor;
            ring.style.backgroundColor = RippleFillColor;
            ring.style.opacity = RipplePeakOpacity;
            ring.style.scale = new Scale(new Vector2(RippleStartScale, RippleStartScale));   // 중심 원점 확산(transform 기반 — 레이아웃 무영향)
            Add(ring);
            ring.BringToFront();
            _active.Add(ring);

            // 드라이버 — 16ms 틱, 진행도 기반 순수함수 업데이트(CraftBench 완료연출 선례 동일)
            long age = 0L;
            IVisualElementScheduledItem task = null;
            task = schedule.Execute(() =>
            {
                // 조기 종료 가드 — 세대 불일치(재부착 레이스)·탈착 시 자가 정지(신세대 요소 간섭 금지)
                if (_myGeneration != _generation || panel == null || ring.panel == null)
                {
                    task.Pause();
                    return;
                }
                age += 16L;
                float t = age / 1000f;
                if (t >= RippleLifeSeconds)
                {
                    // 종료 정리 — 물결 요소 제거(시작 시 생성/종료 시 제거 계약)
                    _active.Remove(ring);
                    ring.RemoveFromHierarchy();
                    task.Pause();
                    return;
                }
                float u = t / RippleLifeSeconds;
                float ease = 1f - (1f - u) * (1f - u);   // EaseOutQuad — 확산 감속
                float s = RippleStartScale + (1f - RippleStartScale) * ease;
                ring.style.scale = new Scale(new Vector2(s, s));
                ring.style.opacity = RipplePeakOpacity * (1f - u);   // 선형 페이드아웃
            }).Every(16L);
        }

        private void ClearRipples()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var ring = _active[i];
                if (ring != null) ring.RemoveFromHierarchy();
            }
            _active.Clear();
        }
    }
}
