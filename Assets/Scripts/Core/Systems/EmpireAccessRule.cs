using ProjectName.Core.Data;

namespace ProjectName.Core.Systems
{
    /// <summary>
    /// 황제국 입장 조건 검증.
    /// 규칙: 모든 국가(East/West/South/North)의 영지 20개씩 총 80개를 
    /// 플레이어가 완전 점령(PlayerOwned + lordExecuted || lordDefeated)해야 입장 가능.
    /// </summary>
    public static class EmpireAccessRule
    {
        /// <summary>
        /// 황제국 입장 가능 여부 확인.
        /// </summary>
        public static bool CanAccessEmpire()
        {
            var db = TerritoryDatabase.Instance;

            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                for (int i = 1; i <= 20; i++)
                {
                    var state = db.GetState(nation, i);
                    if (state == null)
                        return false;
                    if (state.ownership != TerritoryOwnership.PlayerOwned)
                        return false;
                    // 영주가 살아있고 처형되지 않았으면 미점령
                    if (!state.lordExecuted && !state.lordDefeated)
                        return false;
                }
            }
            return true; // 모든 80영지 완전 점령 완료!
        }

        /// <summary>
        /// 현재 점령 진행률 반환 (0.0 ~ 1.0)
        /// </summary>
        public static float GetProgress()
        {
            var db = TerritoryDatabase.Instance;
            int conquered = 0;
            int total = 80;

            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                for (int i = 1; i <= 20; i++)
                {
                    var state = db.GetState(nation, i);
                    if (state != null && state.ownership == TerritoryOwnership.PlayerOwned
                        && (state.lordExecuted || state.lordDefeated))
                    {
                        conquered++;
                    }
                }
            }
            return (float)conquered / total;
        }

        /// <summary>
        /// 점령된 영지 수 반환
        /// </summary>
        public static int GetConqueredCount()
        {
            var db = TerritoryDatabase.Instance;
            int conquered = 0;

            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                for (int i = 1; i <= 20; i++)
                {
                    var state = db.GetState(nation, i);
                    if (state != null && state.ownership == TerritoryOwnership.PlayerOwned
                        && (state.lordExecuted || state.lordDefeated))
                    {
                        conquered++;
                    }
                }
            }
            return conquered;
        }
    }
}