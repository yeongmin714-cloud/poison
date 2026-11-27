using System;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [158차 P2] 플레이어 대상 알림 통일 버스 — Systems→UI 단방향 정적 이벤트 계약(FarmPlot.FarmStatusBroadcast 선례).
    /// Systems 측 발화자: PlayerNotify.Notify(msg) — UI(NPCDialoguePanelUTK)가 Broadcast += ShowTransientMessage 구독.
    /// 규칙: ①UI에서 Systems 정적 이벤트만 구독(역참조 금지) ②문구는 발화점 소유 — 버스는 전달만 ③발화 null/빈 문자열 무시.
    /// 게이트 차단/도구 부족/채널 진행 등 "상호작용불가=피드백 필수" 계약의 표준 경로.
    /// </summary>
    public static class PlayerNotify
    {
        /// <summary>알림 브로드캐스트 — UI 계층에서 구독(ShowTransientMessage 등). Systems 내부 구독 금지(UI 역방향 금지 유지).</summary>
        public static event Action<string> Broadcast;

        /// <summary>알림 발화 — 빈/null 무시, 구독자 부재 시 무영향(콘솔 로그는 발화점 몫으로 유지).</summary>
        public static void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Broadcast?.Invoke(message);
        }

        /// <summary>테스트/씬 전환 후 구독 정리용 — 프로덕션 경로에서 호출 금지(테스트 전용).</summary>
        public static void ResetSubscriptionsForTest()
        {
            Broadcast = null;
        }
    }
}
