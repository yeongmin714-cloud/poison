using ProjectName.Core.Data;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [123차] Phase 2 — 월드맵 영지 마커 호버 영주 툴팁 내용 포맷터 (순수 함수, static만 구성).
    /// WorldMapWindowUTK.TerritoryMarker의 PointerEnter/Move에서 호버 시점 실시간 상태로 호출.
    /// MapWindow.TerritoryTooltipContent(저장고/배치 라인)·Systems 측 동명 클래스와 이름 충돌 회피를 위해 별도 명명.
    /// 4줄 구성: ①영지명 ②국가 ③영주 ④소유 상태.
    /// 영주명은 TerritoryState.ownerLordName([123차] Phase 1)을 소비 — 점령·탈환 즉시 반영(호버 시점 실시간 조회).
    /// </summary>
    public static class WorldMapTerritoryTooltipContent
    {
        /// <summary>국가 한글 표기 — WorldMapWindowUTK.NationLabel과 동일 매핑(실측 기준).</summary>
        public static string NationLine(NationType n)
        {
            switch (n)
            {
                case NationType.East: return "동국(East)";
                case NationType.West: return "서국(West)";
                case NationType.South: return "남국(South)";
                case NationType.North: return "북국(North)";
                case NationType.Empire: return "황제국(Empire)";
                case NationType.Dracula: return "드라큘라(Dracula)";
                default: return "미소속";
            }
        }

        /// <summary>
        /// 라인 3 — 영주 표기. ownership 기준:
        /// PlayerOwned → "플레이어"(플레이어 점령은 추적 저장 없음 — 계획서 기본안),
        /// Unoccupied → "무주",
        /// 그 외(LordOwned/Contested) → state.ownerLordName(빈 값이면 def.lord.lordName 폴백 — 구세이브/초기화 전 원래 영주).
        /// Dracula 영지는 ownerLordName 초기 매핑이 def.lord.lordName(드라큘라 백작)이므로 그대로 표기된다.
        /// </summary>
        public static string OwnerLordLine(TerritoryDefinition def, TerritoryState state)
        {
            TerritoryOwnership own = state != null ? state.ownership : TerritoryOwnership.Unoccupied;
            if (own == TerritoryOwnership.PlayerOwned) return "영주: 플레이어";
            if (own == TerritoryOwnership.Unoccupied) return "영주: 무주";
            string tracked = state != null ? state.ownerLordName : null;
            if (string.IsNullOrEmpty(tracked)) tracked = def.lord.lordName;
            return "영주: " + tracked;
        }

        /// <summary>라인 4 — 소유 상태(ownership enum 기준): 무주/플레이어 소유/영주 소유/분쟁.</summary>
        public static string OwnershipStateLine(TerritoryOwnership o)
        {
            switch (o)
            {
                case TerritoryOwnership.PlayerOwned: return "플레이어 소유";
                case TerritoryOwnership.LordOwned: return "영주 소유";
                case TerritoryOwnership.Contested: return "분쟁";
                case TerritoryOwnership.Unoccupied:
                default: return "무주";
            }
        }

        /// <summary>4줄 툴팁 전문("\n" 구분). state null 허용 — 해당 라인은 기본값(무주) 처리.</summary>
        public static string Build(TerritoryDefinition def, TerritoryState state)
        {
            TerritoryOwnership own = state != null ? state.ownership : TerritoryOwnership.Unoccupied;
            return def.territoryName + "\n"
                 + NationLine(def.nation) + "\n"
                 + OwnerLordLine(def, state) + "\n"
                 + OwnershipStateLine(own);
        }
    }
}
