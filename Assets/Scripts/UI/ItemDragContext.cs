using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 2026-09-11: 창 간 드래그앤드롭 공유 컨텍스트 (정적 상태 — GC 캐시 관례 준수).
    /// InventoryWindow(인벤 슬롯/창고 컨텍스트)와 WarehouseUI(창고 슬롯)가
    /// 드래그 시작/드롭 판정/고스트 렌더를 공유한다.
    /// - 드래그 시작: Begin(...) — 소스 창이 자신의 상태를 기록
    /// - 드롭 판정: 각 창이 자신의 Rect 캐시에 대해 TryConsumeDrop* 호출
    /// - 고스트: DrawGhost() — 매 프레임 1회만 그림(프레임 스탬프 가드, 이중 렌더 방지)
    /// 텍스처 파기 금지: Icon은 ItemIconDatabase 캐시/ArtLibrary 참조만 보관.
    /// </summary>
    public static class ItemDragContext
    {
        public enum Source { None, Inventory, Warehouse }

        public static bool Active;
        public static Source SourceType = Source.None;
        public static int SourceIndex = -1;          // Inventory: 전역 슬롯 인덱스 / Warehouse: 창고 슬롯 인덱스
        public static string TerritoryId = null;     // Warehouse 소스일 때 창고 territoryId
        public static PlayerInventory.ItemData Item; // 드래그 중 아이템
        public static Texture2D Icon;                // 고스트 아이콘 (nullable — 폴백 사각형)

        private static int _lastGhostFrame = -1;     // 프레임당 1회 고스트 가드
        private static GUIStyle _ghostLabelStyle;    // GC 방지 캐시 (정적 — 도메인 리로드 시 자동 정리)

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
        }

        /// <summary>고스트(반투명 아이콘)를 마우스 커서에 따라 그린다. 프레임당 최초 1회만 렌더.</summary>
        public static void DrawGhost()
        {
            if (!Active || Item == null) return;
            if (Event.current == null) return;
            if (_lastGhostFrame == Time.frameCount) return;
            _lastGhostFrame = Time.frameCount;

            Vector2 m = Event.current.mousePosition;
            float size = 64f;
            Rect r = new Rect(m.x + 14f, m.y + 10f, size, size);
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.65f);   // 반투명 팔로우
            if (Icon != null)
                GUI.DrawTexture(r, Icon, ScaleMode.ScaleToFit);
            else
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = prevColor;

            // 이름 라벨
            if (_ghostLabelStyle == null)
            {
                _ghostLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            }
            GUI.Label(new Rect(r.xMax + 6f, r.y + 4f, 320f, 26f), Item.displayName, _ghostLabelStyle);
        }
    }
}
