using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P12 — 인벤/설명/창고 화면 3분할 레이아웃 (UIRoot 폭 기준).
    /// 기존 고정 px 배치(16/596/1044)는 패널 스케일·해상도가 바뀌면 분할이 깨졌다.
    /// UIRoot resolved 폭을 3등분해 각 창에 컬럼 폭/위치를 배정한다.
    ///   좌 1/3 = InventoryWindowUTK, 중 1/3 = ItemDescriptionWindowUTK, 우 1/3 = WarehouseWindowUTK.
    /// </summary>
    public static class UTKThreeColumnLayout
    {
        public const float TopMargin = 96f;   // 기존 관례 유지(핫바/상단 HUD 회피)

        /// <summary>UIRoot 폭 (실패 시 1920 폴백).</summary>
        public static float RootWidth
        {
            get
            {
                var root = UIToolkitBootstrap.UIRoot;
                float w = root != null ? root.resolvedStyle.width : 0f;
                return w > 0f ? w : 1920f;
            }
        }

        public static float RootHeight
        {
            get
            {
                var root = UIToolkitBootstrap.UIRoot;
                float h = root != null ? root.resolvedStyle.height : 0f;
                return h > 0f ? h : 1080f;
            }
        }

        /// <summary>컬럼 인덱스(0=좌/1=중/2=우)의 x 좌표 + 컬럼 내부 폭.</summary>
        public static void GetColumn(int index, out float x, out float width)
        {
            float w = RootWidth / 3f;
            x = w * index;
            width = w - 16f;   // 컬럼 간 16px 여백
        }

        /// <summary>창을 컬럼에 정렬 (폭이 컬럼보다 크면 좌측 정렬 유지).</summary>
        public static void Place(VisualElement win, int index)
        {
            if (win == null) return;
            GetColumn(index, out float x, out float colW);
            win.style.left = x + 8f;                 // 컬럼 좌측 8px 인셋
            win.style.top = TopMargin;
            float winW = win.resolvedStyle.width;
            if (winW <= 0f && win.style.width != null) winW = win.style.width.value.value;
            if (winW > 0f && winW > colW)
                win.style.width = colW;              // 컬럼 초과 시 컬럼 폭으로 수축
        }
    }
}
