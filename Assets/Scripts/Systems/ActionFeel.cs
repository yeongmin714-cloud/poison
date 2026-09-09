using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 액션 필 모드.
    /// Balanced: 저사양 기본 모드 (무거운 이펙트 생략).
    /// HighSpec: 테스트 씬 전용 (전체 이펙트 활성화).
    /// </summary>
    public enum ActionFeelMode
    {
        Balanced = 0,
        HighSpec = 1,
    }

    /// <summary>
    /// G2-06: 액션 필 품질 게이트 (정적 클래스, 순수 플래그 게이트).
    /// 다른 모듈이 ActionFeel.HighSpec을 읽어 이펙트 강도를 분기한다.
    /// MonoBehaviour 아님, Update 루프 없음.
    /// </summary>
    public static class ActionFeel
    {
        // ================================================================
        // 모드 상태 — 기본값 Balanced (저사양)
        // ================================================================
        public static ActionFeelMode Mode = ActionFeelMode.Balanced;

        /// <summary>HighSpec 모드 여부 (테스트 씬 전용). 다른 모듈은 이 플래그만 읽는다.</summary>
        public static bool HighSpec => Mode == ActionFeelMode.HighSpec;

        /// <summary>모드 변경 후 콘솔에 로그를 남긴다.</summary>
        public static void SetMode(ActionFeelMode m)
        {
            Mode = m;
            Debug.Log($"[ActionFeel] mode -> {m}");
        }
    }
}
