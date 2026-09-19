using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Phase O6 (C-O6-04): 영주 성취사 생성기 — OpenMMO HEROIC_TALES.md 벤치마크.
    /// 실제 게임 데이터(영지/국가)에서 결정론적으로 서사시 조각을 조립 — 사실(데이터)과 문장(템플릿) 분리.
    /// 경제와 비접촉, 표시 전용.
    /// </summary>
    public static class HeroicTaleGenerator
    {
        private static readonly string[] Origins =
        {
            "몰락 귀가의 혈통", "사병 출신", "상인 가문의 후예", "전쟁 고아", "수도원에서 나온 수수께끼의 인물", "변방 기사",
        };

        private static readonly string[] Ambitions =
        {
            "한 번 잃은 것을 되찾으려 한다", "왕좌 자리를 노리는 꿈을 키운다", "가문의 이름을 다시 세우고 싶어 한다",
            "전쟁이 끝나는 날만을 기다린다",
        };

        private static readonly string[] Weaknesses =
        {
            "와인을 못 끊는다", "격렬한 도박 습관이 있다", "옛 이름을 숨기고 산다",
            "밤마다 두려움에 시달린다", "금화보다 명예를 좇는다",
        };

        private static readonly string[] NationLines =
        {
            "동국의 성벽처럼 방어적이다",      // East
            "서국다운 기회주의자다",          // West
            "남국의 불꽃처럼 공격적이다",      // South
            "북국의 겨울처럼 강경하다",        // North
            "황제의 이름처럼 권위적이다",      // Empire
            "정체를 알 수 없다",              // 기타(None/Dracula)
        };

        /// <summary>영지 결정론 성취사 3문장 (같은 영지 = 항상 같은 이야기).</summary>
        public static string GetTale(string territoryId, NationType nation)
        {
            if (string.IsNullOrEmpty(territoryId)) territoryId = "unknown";
            var rng = new System.Random(LordStatGenerator.Djb2(territoryId + "_tale"));
            return Assemble(rng, nation, null);
        }

        /// <summary>영주 이름이 포함된 성취사 (대면창 표시용).</summary>
        public static string GetTaleForLord(string lordName, string territoryId)
        {
            if (string.IsNullOrEmpty(lordName)) lordName = "영주";
            if (string.IsNullOrEmpty(territoryId)) territoryId = lordName;
            var rng = new System.Random(LordStatGenerator.Djb2(territoryId + "_tale"));
            return Assemble(rng, NationType.None, lordName);
        }

        private static string Assemble(System.Random rng, NationType nation, string lordName)
        {
            string origin = Origins[rng.Next(Origins.Length)];
            string ambition = Ambitions[rng.Next(Ambitions.Length)];
            string weakness = Weaknesses[rng.Next(Weaknesses.Length)];
            string nationLine = NationLines[(int)GetNationIndex(nation)];

            string subject = string.IsNullOrEmpty(lordName) ? "이 영주" : lordName;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{subject}은(는) {origin}로, {ambition}.");
            sb.AppendLine(nationLine);
            sb.Append($"소문에 따르면 — {weakness}.");
            return sb.ToString();
        }

        private static int GetNationIndex(NationType nation)
        {
            switch (nation)
            {
                case NationType.East: return 0;
                case NationType.West: return 1;
                case NationType.South: return 2;
                case NationType.North: return 3;
                case NationType.Empire: return 4;
                default: return 5;
            }
        }
    }
}
