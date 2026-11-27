using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [125차-38] 미니맵 목적지 경로 오버레이 — 월드맵 '지점 전송 (Fast Travel)' 신계약(③)의 렌더 파트.
    ///
    /// [계약 출처] docs/125_38_WORLDMAP_FLUENT_PINS_ROUTE.md — Figma 95:167 내 Line 2개 참조
    ///   (stroke #FF9E2C w3.0 / #58A6FF w2.4, '경로=목적지 핀 색'). 호출부(WorldMapWindowUTK.ExecuteFastTravel)는
    ///   선택 목적지 핀 강조색 #58A6FF(Accent)를 경로색으로 전달한다.
    ///
    /// [렌더 방식 — 벡터메시 금지 계약(84차) 준수]
    ///   Mesh API 일절 미사용(벡터메시 계약 — 84차). 경로 = 얇은 회전 사각형 세그먼트(VisualElement
    ///   border-radius 원통형 캡 + rotate)의 대시 폴리라인. 세그먼트는 풀링 재사용(프레임마다 신규 할당 금지).
    ///
    /// [좌표계 — MinimapUTK 단일 소스 공유 계약]
    ///   월드→미니맵 로컬 변환은 MinimapUTK.UpdateMapCrop(플레이어 중심 크롭)·FillBlips와 동일 산식:
    ///   px = half + dx/(2·localRadius)·side, py = half − dz/(2·localRadius)·side (+x=우, +z=북=상).
    ///   MinimapUTK.Refresh() 말단 훅(OnMinimapRefresh)에서 매 리프레시 재계산 — 플레이어 이동을 경로가 추종.
    ///
    /// [수명 규칙]
    ///   - 미니맵 비가시(프레임 폭 0)/플레이어 부재 → 세그먼트 자동 숨김(경로 상태는 유지 — 복귀 시 재표시).
    ///   - 도착 판정(플레이어↔목적지 24m 이내) → 자동 ClearRoute(텔레포트 직후 잔상 경로 소멸).
    /// </summary>
    public static class MinimapRouteOverlay
    {
        // ===== 진실치 =====
        /// <summary>경로색 기본값 — Figma 95:167 Line #58A6FF w2.4(호출부 미지정 시).</summary>
        private static readonly Color DefaultColor = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 1f);
        /// <summary>선 두께 — Figma #58A6FF Line stroke w2.4.</summary>
        private const float Thickness = 2.4f;
        /// <summary>대시 길이(px) — 세그먼트 간격 기본안(작업 지시: 직선 대시).</summary>
        private const float DashLength = 14f;
        /// <summary>대시 간격(px).</summary>
        private const float DashGap = 10f;
        /// <summary>세그먼트 상한 — 풀 크기 가드(미니맵 원 지름 대비 여유).</summary>
        private const int MaxSegments = 64;
        /// <summary>도착 판정 거리(m) — 플레이어가 목적지에 근접하면 경로 자동 소멸.</summary>
        private const float ArrivalMeters = 24f;

        // ===== 상태 =====
        private static Vector3 _target;
        private static Color _color = DefaultColor;
        private static bool _active;
        private static VisualElement _host;
        private static readonly List<VisualElement> _segments = new List<VisualElement>(MaxSegments);

        /// <summary>경로 활성 여부 — 월드맵 측 표시/중복 호출 가드용.</summary>
        public static bool IsActive => _active;

        // =====================================================================
        //  공개 API (작업 지시 계약 시그니처)
        // =====================================================================

        /// <summary>목적지 경로 표시 시작 — 색 기본값 #58A6FF(Figma Line w2.4).</summary>
        public static void ShowRouteTo(Vector3 targetWorldPos)
        {
            ShowRouteTo(targetWorldPos, DefaultColor);
        }

        /// <summary>목적지 경로 표시 시작 — targetWorldPos=목적지 월드좌표(영지 핀 worldPosition 단일 진실원),
        /// color=경로색('경로=목적지 핀 색' 계약 — WorldMapWindowUTK는 선택 핀 강조색 #58A6FF 전달).
        /// 실제 렌더는 MinimapUTK.Refresh() 훅(OnMinimapRefresh)에서 수행 — 미니맵 부재 시 상태만 보관.</summary>
        public static void ShowRouteTo(Vector3 targetWorldPos, Color color)
        {
            _target = targetWorldPos;
            _color = (color.a > 0f) ? color : DefaultColor;
            _active = true;
        }

        /// <summary>경로 표시 해제 — 세그먼트 전수 숨김(풀은 보존 — 재표시 시 재사용).</summary>
        public static void ClearRoute()
        {
            _active = false;
            if (_host != null)
                _host.style.display = DisplayStyle.None;
        }

        // =====================================================================
        //  MinimapUTK 훅 — Refresh() 말단 호출(훅 1곳 계약)
        // =====================================================================

        /// <summary>미니맵 리프레시 동기 갱신 — mapFrame=MinimapUTK._mapFrame(원형 클립 호스트),
        /// playerWorldPos=플레이어 실측 위치(null이면 자동 숨김), localRadius=MinimapUTK.LocalRadius(크롭 반경),
        /// fallbackSide=프레임 레이아웃 미확정 시 폴백 한 변 길이(MinimapUTK.FillBlips 폴백과 동일 값).</summary>
        internal static void OnMinimapRefresh(VisualElement mapFrame, Vector3? playerWorldPos, float localRadius, float fallbackSide)
        {
            if (!_active)
            {
                if (_host != null && _host.style.display != DisplayStyle.None)
                    _host.style.display = DisplayStyle.None;
                return;
            }
            // 미니맵 비가시/플레이어 부재 → 자동 숨김(상태 유지)
            if (mapFrame == null || playerWorldPos == null)
            {
                if (_host != null)
                    _host.style.display = DisplayStyle.None;
                return;
            }
            float side = mapFrame.contentRect.width;
            if (side < 1f)
                side = fallbackSide;   // 레이아웃 미확정 프레임 — FillBlips와 동일 폴백
            if (side < 1f)
            {
                if (_host != null)
                    _host.style.display = DisplayStyle.None;
                return;
            }

            EnsureHost(mapFrame);

            // 로컬 좌표 — MinimapUTK.UpdateMapCrop/FillBlips 동일 산식(+x=우, +z=북=상, 플레이어 중앙)
            Vector3 p = playerWorldPos.Value;
            float dx = _target.x - p.x;
            float dz = _target.z - p.z;
            // 도착 판정 — XZ 거리 기준(텔레포트 직후 목적지 중심 도착 → 경로 자동 소멸)
            if (dx * dx + dz * dz <= ArrivalMeters * ArrivalMeters)
            {
                ClearRoute();
                return;
            }
            float half = side * 0.5f;
            float px = half + (dx / (2f * localRadius)) * side;
            float py = half - (dz / (2f * localRadius)) * side;

            // 대시 세그먼트 기하 — 시작점은 화면 중앙(플레이어), 끝점은 목적지 로컬 좌표
            float sx = half, sy = half;   // 플레이어는 로컬뷰에서 항상 중앙(UpdateMapCrop 크롭 계약)
            float ex = px, ey = py;
            float vx = ex - sx, vy = ey - sy;
            float totalLen = Mathf.Sqrt(vx * vx + vy * vy);
            if (totalLen < 1f)
            {
                _host.style.display = DisplayStyle.None;
                return;
            }
            float ux = vx / totalLen, uy = vy / totalLen;
            float angleDeg = -Mathf.Atan2(vy, vx) * Mathf.Rad2Deg;   // UTK y 하향 — 화면 각도 반전
            float step = DashLength + DashGap;
            int count = Mathf.Clamp(Mathf.CeilToInt(totalLen / step), 1, MaxSegments);

            _host.style.display = DisplayStyle.Flex;
            int used = 0;
            for (int i = 0; i < count; i++)
            {
                float d0 = i * step;
                if (d0 >= totalLen) break;
                float d1 = Mathf.Min(d0 + DashLength, totalLen);
                float len = d1 - d0;
                if (len < 1f) break;
                float midX = sx + ux * (d0 + len * 0.5f);
                float midY = sy + uy * (d0 + len * 0.5f);

                VisualElement seg;
                if (used < _segments.Count)
                {
                    seg = _segments[used];
                }
                else
                {
                    seg = new VisualElement { name = "RouteSegment", pickingMode = PickingMode.Ignore };
                    seg.style.position = Position.Absolute;
                    float cap = Thickness * 0.5f;
                    seg.style.borderTopLeftRadius = cap;   // 원통형 대시 캡 — border-radius 원 계약(벡터메시 금지)
                    seg.style.borderTopRightRadius = cap;
                    seg.style.borderBottomLeftRadius = cap;
                    seg.style.borderBottomRightRadius = cap;
                    _host.Add(seg);
                    _segments.Add(seg);
                }
                seg.style.display = DisplayStyle.Flex;
                seg.style.left = midX - len * 0.5f;
                seg.style.top = midY - Thickness * 0.5f;
                seg.style.width = len;
                seg.style.height = Thickness;
                seg.style.rotate = new Rotate(new Angle(angleDeg, AngleUnit.Degree));   // 회전 중심=기본(center)
                seg.style.backgroundColor = new StyleColor(_color);
                used++;
            }
            // 미사용 풀 숨김 — 프레임마다 신규 할당 없이 길이 변화 추종
            for (int i = used; i < _segments.Count; i++)
                _segments[i].style.display = DisplayStyle.None;
        }

        /// <summary>호스트 지연 생성 — MapFrame 직속(마커/플레이어 마커 위). pickingMode Ignore —
        /// 미니맵 우클릭 명령(PointerDown) 투과 보장. 원형 클립은 MapFrame overflow Hidden이 담당.</summary>
        private static void EnsureHost(VisualElement mapFrame)
        {
            if (_host != null && _host.parent == mapFrame)
                return;
            if (_host != null)
                _host.RemoveFromHierarchy();
            _host = new VisualElement { name = "RouteOverlayHost" };
            _host.style.position = Position.Absolute;
            _host.style.left = 0f;
            _host.style.top = 0f;
            _host.style.right = 0f;
            _host.style.bottom = 0f;
            _host.pickingMode = PickingMode.Ignore;
            mapFrame.Add(_host);
        }
    }
}