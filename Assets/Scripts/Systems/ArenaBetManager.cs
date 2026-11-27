// 2026-10-23 (121차): 아레나 베팅 관리인 NPC 마커 — 로비(ArenaInteriorBuilder) 배치.
// 좌클릭/F키 → HoverTargetClassifier.ArenaManager → InteractionPanelUTK(피그마 261:3 — 대화하기/베팅하기).
// 베팅하기 → ArenaWindowUTK.Open(NationStyle). Systems 전용 컴포넌트(UI 의존 없음 — 분류기 직접 판정용).
using UnityEngine;

namespace ProjectName.Systems
{
    public class ArenaBetManager : MonoBehaviour
    {
        /// <summary>로비 국가 스타일 — ArenaWindowUTK 개방에 전달(아레나 테마 유지).</summary>
        public string NationStyle = "Western";
        /// <summary>대화하기 선택 시 표시 문구.</summary>
        public string Greeting = "어서 오세요! 오늘의 2인 난투 배당을 확인하세요.";
    }
}
