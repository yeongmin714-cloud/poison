using UnityEngine;
using ProjectName.Core;

namespace ProjectName.UI
{
    /// <summary>
    /// 2026-09-11: 창 간 드래그앤드롭 공유 컨텍스트 (정적 상태 — GC 캐시 관례 준수).
    /// InventoryWindow(인벤 슬롯/창고 컨텍스트)와 WarehouseUI(창고 슬롯), LootWindow(전리품 슬롯)가
    /// 드래그 시작/드롭 판정/고스트 렌더를 공유한다.
    /// - 드래그 시작: Begin(...) — 소스 창이 자신의 상태를 기록
    /// - 드롭 판정: 각 창이 자신의 Rect 캐시에 대해 TryConsumeDrop* 호출
    ///   (Loot 소스는 InventoryWindow.ProcessDrag의 전리품 분기가 MouseUp 판정 대행)
    /// - 고스트: DrawGhost() — 매 프레임 1회만 그림(프레임 스탬프 가드, 이중 렌더 방지)
    /// [Phase C] 드래그 타겟 스냅/하이라이트 지원 — 유효 드롭 영역 진입 시 고스트 스냅 + 슬롯 펄스
    /// 텍스처 파기 금지: Icon은 ItemIconDatabase 캐시/ArtLibrary 참조만 보관.
    /// </summary>
    public static class ItemDragContext
    {
        // 2026-09-11(6): Loot 추가 — 전리품 창→인벤 드래그.
        // 2026-09-12(P4): Equipment 추가 — 통합 장비칸(장착 아이템) 소스 드래그(해제 후 인벤 이동).
        // 기존 값 순서 유지(하위 호환), 신규 멤버는 항상 맨 뒤에 추가.
        public enum Source { None, Inventory, Warehouse, Loot, Equipment }

        public static bool Active;
        public static Source SourceType = Source.None;
        public static int SourceIndex = -1;          // Inventory: 전역 슬롯 인덱스 / Warehouse: 창고 슬롯 인덱스 / Loot: 전리품 바구니 항목 인덱스 / Equipment: 장비칸 셀 정의 인덱스
        public static string TerritoryId = null;     // Warehouse 소스일 때 창고 territoryId
        public static PlayerInventory.ItemData Item; // 드래그 중 아이템
        public static Texture2D Icon;                // 고스트 아이콘 (nullable — 폴백 사각형)

        // [Phase C] 드래그 타겟 스냅/하이라이트 상태
        public static bool HasValidDropTarget = false;
        public static Rect ValidDropTargetRect;
        public static string ValidDropTargetType = ""; // "Inventory", "Equip", "Warehouse", "Loot", "Hotbar"
        public static int ValidDropTargetIndex = -1;

        private static int _lastGhostFrame = -1;     // 프레임당 1회 고스트 가드
        private static GUIStyle _ghostLabelStyle;    // GC 방지 캐시 (정적 — 도메인 리로드 시 자동 정리)
        private static float _equipSlotHoverTime = 0f;
        private static int _lastHoveredEquipSlot = -1;

        /// <summary>드래그 시작. item이 null이면 무시.</summary>
        public static void Begin(Source source, int index, PlayerInventory.ItemData item, string territoryId = null)
        {
            if (item == null) return;
            Active = true;
            SourceType = source;
            SourceIndex = index;
            TerritoryId = territoryId;
            Item = item;
            Icon = ItemIconDatabase.GetOrCreateIcon(item);
            // [Phase C] 스냅/하이라이트 상태 초기화
            HasValidDropTarget = false;
            ValidDropTargetRect = Rect.zero;
            ValidDropTargetType = "";
            ValidDropTargetIndex = -1;
        }

        /// <summary>드래그 취소/종료 (상태만 클리어 — 텍스처 파기 없음).</summary>
        public static void Cancel()
        {
            Active = false;
            SourceType = Source.None;
            SourceIndex = -1;
            TerritoryId = null;
            Item = null;
            Icon = null;
            HasValidDropTarget = false;
            ValidDropTargetRect = Rect.zero;
            ValidDropTargetType = "";
            ValidDropTargetIndex = -1;
            _equipSlotHoverTime = 0f;
            _lastHoveredEquipSlot = -1;
        }

        /// <summary>[Phase C] 유효한 드롭 타겟 설정 — 고스트 스냅 및 슬롯 하이라이트용</summary>
        public static void SetValidDropTarget(Rect targetRect, string targetType, int targetIndex = -1)
        {
            HasValidDropTarget = true;
            ValidDropTargetRect = targetRect;
            ValidDropTargetType = targetType;
            ValidDropTargetIndex = targetIndex;
        }

        /// <summary>[Phase C] 드롭 타겟 해제</summary>
        public static void ClearValidDropTarget()
        {
            HasValidDropTarget = false;
            ValidDropTargetRect = Rect.zero;
            ValidDropTargetType = "";
            ValidDropTargetIndex = -1;
        }

        /// <summary>고스트(반투명 아이콘)를 마우스 커서에 따라 그린다. 프레임당 최초 1회만 렌더.
        /// [Phase C] 유효 드롭 타겟이 있으면 고스트가 타겟 중앙으로 스냅되고 펄스 효과 적용</summary>
        public static void DrawGhost()
        {
            if (!Active || Item == null) return;
            if (Event.current == null) return;
            // 2026-09-11(4) 수리: IMGUI는 프레임당 복수 이벤트 패스(MouseDrag → Repaint)를 도는데,
            // 기존엔 입력 패스(MouseDrag 등)에서 스탬프를 먼저 찍어 같은 프레임 Repaint 패스가
            // 프레임 가드에 막혀 고스트가 아예 안 그려졌다. 렌더는 Repaint 패스에서만 수행한다.
            if (Event.current.type != EventType.Repaint) return;
            if (_lastGhostFrame == Time.frameCount) return;
            _lastGhostFrame = Time.frameCount;

            Vector2 m = Event.current.mousePosition;
            float size = 64f;
            
            // [Phase C] 유효 드롭 타겟이 있으면 고스트를 타겟 중앙으로 스냅 (부드러운 이동)
            Vector2 ghostPos;
            float pulseScale = 1f;
            float pulseAlpha = 0.65f;
            
            if (HasValidDropTarget)
            {
                ghostPos = ValidDropTargetRect.center - new Vector2(size * 0.5f, size * 0.5f);
                // 펄스 애니메이션: 0.8~1.2 스케일, 0.5~0.8 알파
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                pulseScale = 0.8f + 0.4f * pulse;
                pulseAlpha = 0.5f + 0.3f * pulse;
            }
            else
            {
                ghostPos = new Vector2(m.x + 14f, m.y + 10f);
            }

            Rect r = new Rect(ghostPos.x, ghostPos.y, size * pulseScale, size * pulseScale);
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, pulseAlpha);   // 반투명 팔로우 + 펄스
            if (Icon != null)
                GUI.DrawTexture(r, Icon, ScaleMode.ScaleToFit);
            else
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prevColor;

            // [Phase C] 유효 타겟 표시: 테두리 글로우
            if (HasValidDropTarget)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                GUI.color = new Color(0.35f, 0.65f, 0.90f, 0.8f * pulse); // 스카이블루 글로우 펄스
                GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = prevColor;
            }

            // 이름 라벨 (고스트가 스냅되면 라벨도 같이 이동)
            if (_ghostLabelStyle == null)
            {
                _ghostLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            }
            GUI.Label(new Rect(r.xMax + 6f, r.y + 4f, 320f, 26f), Item.displayName, _ghostLabelStyle);

            // [Phase C] 장비 슬롯 호버 시 스케일 토글 + 사운드 트리거
            if (HasValidDropTarget && ValidDropTargetType == "Equip")
            {
                if (_lastHoveredEquipSlot != ValidDropTargetIndex)
                {
                    _equipSlotHoverTime = Time.realtimeSinceStartup;
                    _lastHoveredEquipSlot = ValidDropTargetIndex;
                    // 사운드 트리거 (SoundManager가 있으면)
                    try
                    {
                        var sm = ProjectName.Core.SoundManager.Instance;
                        if (sm != null) sm.PlaySFX("UI_Hover");
                    }
                    catch { }
                }
            }
            else
            {
                _lastHoveredEquipSlot = -1;
            }
        }

        /// <summary>[Phase C] 슬롯 하이라이트 렌더링 — 유효 드롭 타겟 슬롯에 펄스 테두리 표시</summary>
        public static void DrawSlotHighlight()
        {
            if (!HasValidDropTarget || Event.current == null || Event.current.type != EventType.Repaint)
                return;

            // 슬롯 종류별 하이라이트 스타일
            Color highlightColor;
            float borderWidth = 3f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f); // 0.5~1.0 펄스

            switch (ValidDropTargetType)
            {
                case "Inventory":
                    highlightColor = new Color(0.35f, 0.65f, 0.90f, 0.4f + 0.3f * pulse); // 스카이블루
                    break;
                case "Equip":
                    highlightColor = new Color(1f, 0.85f, 0.3f, 0.5f + 0.3f * pulse); // 골드
                    borderWidth = 4f;
                    break;
                case "Warehouse":
                    highlightColor = new Color(0.4f, 0.8f, 0.5f, 0.4f + 0.3f * pulse); // 그린
                    break;
                case "Loot":
                    highlightColor = new Color(1f, 0.5f, 0.3f, 0.4f + 0.3f * pulse); // 오렌지
                    break;
                case "Hotbar":
                    highlightColor = new Color(0.8f, 0.4f, 0.9f, 0.4f + 0.3f * pulse); // 퍼플
                    break;
                default:
                    highlightColor = new Color(0.35f, 0.65f, 0.90f, 0.4f + 0.3f * pulse);
                    break;
            }

            // 슬롯 테두리 하이라이트 (9-slice 스타일)
            Rect r = ValidDropTargetRect;
            GUI.color = highlightColor;
            // 상단
            GUI.DrawTexture(new Rect(r.x - borderWidth, r.y - borderWidth, r.width + borderWidth * 2, borderWidth), Texture2D.whiteTexture);
            // 하단
            GUI.DrawTexture(new Rect(r.x - borderWidth, r.yMax, r.width + borderWidth * 2, borderWidth), Texture2D.whiteTexture);
            // 좌측
            GUI.DrawTexture(new Rect(r.x - borderWidth, r.y - borderWidth, borderWidth, r.height + borderWidth * 2), Texture2D.whiteTexture);
            // 우측
            GUI.DrawTexture(new Rect(r.xMax, r.y - borderWidth, borderWidth, r.height + borderWidth * 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // [Phase C] 장비 슬롯일 때 추가: 스케일 토글 효과 (슬롯이 살짝 커졌다 작아짐)
            if (ValidDropTargetType == "Equip")
            {
                float scalePulse = 1f + 0.05f * Mathf.Sin(Time.time * 10f);
                Vector2 center = r.center;
                Rect scaledRect = new Rect(
                    center.x - (r.width * scalePulse) * 0.5f,
                    center.y - (r.height * scalePulse) * 0.5f,
                    r.width * scalePulse,
                    r.height * scalePulse
                );
                // 내부 글로우
                GUI.color = new Color(1f, 0.9f, 0.4f, 0.15f * pulse);
                GUI.DrawTexture(scaledRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = Color.white;
            }
        }
    }
}
