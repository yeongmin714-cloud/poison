using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [199차·P4 클릭 글로우] — 눌린 UI 요소 자체의 순백 글로우 플래시(눌리는 느낌 강화).
    /// UTKClickRipple(커서 위치 원형 물결)과 상호 보완: 물결 = 클릭 지점, 글로우 = 눌린 요소 경계.
    /// UTKClickRipple 선례 계승: 오버레이(pickingMode Ignore · 절대좌표) + schedule.Execute().Every(16L)
    /// 드라이버 + 세대 토큰(연타/재부착 레이스 가드) + 동시 예산(MaxGlows) 퇴거.
    ///  - 글로우 요소는 클릭 시 생성/수명 종료 시 제거 — 상시 렌더 무변화(기존 창/UI 렌더 계약 유지).
    ///  - 원형 사각 = border-radius 스타일링 — 벡터메시 금지(84차 계약) 준수(Mesh 생성 0).
    ///  - 앵커 산정: evt.target(최심 피킹)에서 위로 소정 층수만 상승해 최소 크기 이상 요소(버튼/슬롯 판)를
    ///    감싼다(라벨·아이콘에만 붙는 과소 글로우 방지). 찾지 못하면 target 자체 — 무glow 아님.
    ///  - PointerDown 훅은 UIRoot TrickleDown — 모든 UI 요소 클릭만 포착(빈 화면 월드 클릭은 가드로 무glow).
    /// </summary>
    public class UTKPressGlow : VisualElement
    {
        private static UTKPressGlow _instance;
        private static long _generation;   // 세대 토큰 — Ensure 재부착 시 선점, 구세대 드라이버 자가 종료

        // ── 연출 상수(소스계약 핀) ──
        internal const float GlowLifeSeconds = 0.22f;    // 글로우 수명(눌림 플래시 — 물결 0.45f보다 짧게)
        internal const float GlowPeakOpacity = 0.55f;    // 피크 알파(선형 페이드아웃 시작점)
        internal const float GlowFillAlpha = 0.10f;      // 내부 빛 채움(리플 0.10 동일 미감)
        internal const float GlowBorderWidth = 2f;       // 경계 두께(리플 2f 동일)
        internal const float GlowRadius = 8f;            // 모서리 반경(창/슬롯 라운드 스타일 정합)
        internal const float GlowPad = 3f;               // 요소 경계 바깥 여백
        internal const float MinAnchorSizePx = 26f;      // 앵커 최소 크기 — 이보다 작은 요소는 상위로 상승
        internal const int MaxClimbLevels = 4;           // 앵커 상승 상한(과도 상승 방지 — 창 전체 글로우 방지)
        internal const int MaxGlows = 8;                 // 동시 글로우 예산(연타 상한)

        // 순백(중립적 클릭 발광 — UTKClickRipple 사용자 확정 계약 연속성) 경계 + 은은한 백색 내부 빛
        internal static readonly Color GlowColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        internal static readonly Color GlowFillColor = new Color(1f, 1f, 1f, GlowFillAlpha);

        private readonly List<VisualElement> _active = new List<VisualElement>();   // 활성 글로우(0번 = 가장 오래된 것)
        private readonly long _myGeneration;   // 이 인스턴스가 속한 세대(구세대 드라이버 자가 종료 판정용)
        private VisualElement _hookRoot;       // PointerDown 훅이 걸린 루트(탈착 시 정확히 해제하기 위한 캐시)
        private bool _hooked;

        /// <summary>외부 강제 보장용 — 멱등(중복 실행 무지). UTKClickRipple Ensure 스타일.</summary>
        public static UTKPressGlow Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _generation++;   // 신규 세대 선점 — 구세대 인스턴스의 드라이버는 세대 불일치로 자가 종료
            _instance = new UTKPressGlow();
            root.Add(_instance);
            Debug.Log("[UTKPressGlow] 눌림 글로우 오버레이 부착 — 요소 경계 플래시(벡터메시 0)");
            return _instance;
        }

        private UTKPressGlow()
        {
            _myGeneration = _generation;
            name = "UTKPressGlow";
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
            // 루트 TrickleDown — 이벤트 대상이 어떤 창/버튼이든(또는 빈 화면 폴백 루트든) 글로우는 항상 포착
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
            ClearGlows();   // 탈착 시 잔여 글로우 일괄 제거 — 각 드라이버는 glow.panel null 가드로 자가 정지
        }

        private void OnRootPointerDown(PointerDownEvent evt)
        {
            // UI 전용 가드 — 빈 화면(월드) 클릭 무glow(UTKClickRipple 동일 계약: UI 클릭만 발광).
            if (evt.target == null || evt.target == _hookRoot) return;

            // 앵커 상승 — 최심 피킹(라벨/아이콘)에서 최소 크기 요소(버튼/슬롯 판)까지 소정 층수만 상승.
            var anchor = evt.target as VisualElement;
            int climbs = 0;
            while (anchor != null && climbs < MaxClimbLevels
                   && (anchor.worldBound.width < MinAnchorSizePx || anchor.worldBound.height < MinAnchorSizePx))
            {
                var up = anchor.parent;
                if (up == null || up == _hookRoot) break;   // 루트까지만 — 전화면 글로우 금지
                anchor = up;
                climbs++;
            }
            if (anchor == null) return;

            // 요소 worldBounds(패널 좌표) → 오버레이 로컬 변환 2점(WorldToLocal 확장 메서드 수신자 선례 — 리플 동일)
            Vector2 localMin = this.WorldToLocal(anchor.worldBound.min);
            Vector2 localMax = this.WorldToLocal(anchor.worldBound.max);
            SpawnGlow(localMin, localMax);
        }

        // ─────────────────────────── 글로우 스폰 + 16ms 드라이버 ───────────────────────────

        private void SpawnGlow(Vector2 localMin, Vector2 localMax)
        {
            // 오버레이 재선두 — 열린 창들 위로 글로우를 렌더(리플 동일; 커서는 자체 BringToFront 유지).
            this.BringToFront();

            // 예산 초과 — 가장 오래된 글로우 즉시 정리(연타 방어; 잘린 요소의 드라이버는 panel null 가드로 자가 정지)
            if (_active.Count >= MaxGlows)
            {
                var evicted = _active[0];
                _active.RemoveAt(0);
                if (evicted != null) evicted.RemoveFromHierarchy();
            }

            // 플래시 1 — 요소 경계 오버레이(클릭 시에만 생성 → 기존 렌더 무변화). 원위 고정(스케일 없음 —
            //  눌린 요소 자체가 밝아지는 감각: 0→피크 즉시 도달 후 선형 페이드아웃)
            var glow = new VisualElement { name = "PressGlow", pickingMode = PickingMode.Ignore };
            glow.style.position = Position.Absolute;
            glow.style.left = localMin.x - GlowPad;
            glow.style.top = localMin.y - GlowPad;
            glow.style.width = Mathf.Max(1f, localMax.x - localMin.x) + GlowPad * 2f;
            glow.style.height = Mathf.Max(1f, localMax.y - localMin.y) + GlowPad * 2f;
            glow.style.borderTopLeftRadius = GlowRadius;
            glow.style.borderTopRightRadius = GlowRadius;
            glow.style.borderBottomLeftRadius = GlowRadius;
            glow.style.borderBottomRightRadius = GlowRadius;
            glow.style.borderTopWidth = GlowBorderWidth;
            glow.style.borderRightWidth = GlowBorderWidth;
            glow.style.borderBottomWidth = GlowBorderWidth;
            glow.style.borderLeftWidth = GlowBorderWidth;
            glow.style.borderTopColor = GlowColor;
            glow.style.borderRightColor = GlowColor;
            glow.style.borderBottomColor = GlowColor;
            glow.style.borderLeftColor = GlowColor;
            glow.style.backgroundColor = GlowFillColor;
            glow.style.opacity = GlowPeakOpacity;   // 시작 = 피크(눌림 즉시 발광) → 선형 페이드아웃
            Add(glow);
            glow.BringToFront();
            _active.Add(glow);

            // 드라이버 — 16ms 틱, 진행도 기반 순수함수 업데이트(리플/CraftBench 선례 동일)
            long age = 0L;
            IVisualElementScheduledItem task = null;
            task = schedule.Execute(() =>
            {
                // 조기 종료 가드 — 세대 불일치(재부착 레이스)·탈착 시 자가 정지(신세대 요소 간섭 금지)
                if (_myGeneration != _generation || panel == null || glow.panel == null)
                {
                    task.Pause();
                    return;
                }
                age += 16L;
                float t = age / 1000f;
                if (t >= GlowLifeSeconds)
                {
                    // 종료 정리 — 글로우 요소 제거(시작 시 생성/종료 시 제거 계약)
                    _active.Remove(glow);
                    glow.RemoveFromHierarchy();
                    task.Pause();
                    return;
                }
                float u = t / GlowLifeSeconds;
                // 페이드아웃 EaseOutQuad — 초반은 밝게 유지, 후반 급수축(눌림 후 빛이 스며들 듯)
                float ease = 1f - (1f - u) * (1f - u);
                glow.style.opacity = GlowPeakOpacity * (1f - ease);
            }).Every(16L);
        }

        private void ClearGlows()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var glow = _active[i];
                if (glow != null) glow.RemoveFromHierarchy();
            }
            _active.Clear();
        }
    }
}
