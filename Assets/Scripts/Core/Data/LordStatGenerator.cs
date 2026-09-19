using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// Phase O4 (C-O4-02): 영주 능력치 생성기 — OpenMMO COMBAT.md 벤치마크.
    ///
    /// 절차: 4d6 drop lowest 6회 → 국가 성향 보정 → 링별 목표 합계 리밸런싱(3~18 클램프)
    ///   → GUARD = clamp(10 + (DEX−10)/2, 1, 20) (C# int 나눗셈 0-버림 — Rust 정수나눗셈과 동일)
    ///
    /// 결정론: 시드 = djb2(territoryId) — 같은 영지는 항상 같은 영주 능력치.
    /// 링별 목표 합계: Ring1=66 / Ring2=72 / Ring3=78 / Ring4=84 / Empire=84.
    /// Core 계층 — Systems 참조 금지(TerritoryDatabase는 Core.Data).
    /// </summary>
    public static class LordStatGenerator
    {
        private const int StatMin = 3;
        private const int StatMax = 18;
        private const int EmpireGuardTarget = 84;

        /// <summary>영주 6속성 + GUARD.</summary>
        public struct LordStats
        {
            public int str;
            public int dex;
            public int con;
            public int intel;
            public int wis;
            public int cha;
            public int guard;
            public int ring;      // TerritoryDifficulty 정수값 (로그/문서용)
            public string nationId;

            public int Total => str + dex + con + intel + wis + cha;

            public override string ToString()
                => $"STR {str} / DEX {dex} / CON {con} / INT {intel} / WIS {wis} / CHA {cha} | GUARD {guard} | 합계 {Total}";
        }

        // ── 국가 성향 보정 (COMBAT.md 클래스 보정 패턴 — NationType 기반) ──
        private static void ApplyNationFlavor(ref LordStats s, NationType nation)
        {
            switch (nation)
            {
                case NationType.East:   s.con += 1; break;                    // 방어적
                case NationType.West:   s.dex += 1; break;                    // 기회주의
                case NationType.South:  s.str += 1; break;                    // 공격적
                case NationType.North:  s.str += 1; s.con += 1; break;        // 강경
                case NationType.Empire: s.cha += 2; break;                    // 권위
                default: break; // None/Dracula — 보정 없음
            }
        }

        /// <summary>링별 목표 합계 (높은 링일수록 영주 체급 상승).</summary>
        public static int TargetSumForRing(TerritoryDifficulty ring)
        {
            switch (ring)
            {
                case TerritoryDifficulty.Ring1: return 66;
                case TerritoryDifficulty.Ring2: return 72;
                case TerritoryDifficulty.Ring3: return 78;
                case TerritoryDifficulty.Ring4: return 84;
                case TerritoryDifficulty.Empire: return EmpireGuardTarget;
                default: return 72;
            }
        }

        /// <summary>4d6 drop lowest — 4회 d6, 최소 1개 제외 후 3개 합산 (범위 3~18).</summary>
        public static int Roll4d6DropLowest(System.Random rng)
        {
            int min = 7;
            int sum = 0;
            for (int i = 0; i < 4; i++)
            {
                int roll = rng.Next(1, 7);
                if (roll < min) min = roll;
                sum += roll;
            }
            return sum - min; // 4개 합 − 최소 1개
        }

        /// <summary>GUARD = clamp(10 + (DEX−10)/2, 1, 20) — int 나눗셈 0-버림.</summary>
        public static int GuardFromDex(int dex)
        {
            int mod = (dex - 10) / 2; // C# 정수 나눗셈: -1/2 = 0 (0-버림)
            return Mathf.Clamp(10 + mod, 1, 20);
        }

        /// <summary>
        /// 영주 능력치 생성 (결정론 — territoryId 시드).
        /// 절차: 4d6 roll → 국가 보정 → 클램프(3~18) → 합계 리밸런싱 → GUARD 산출.
        /// </summary>
        public static LordStats Generate(string territoryId, NationType nation, TerritoryDifficulty ring)
        {
            int seed = Djb2(territoryId ?? "default");
            var rng = new System.Random(seed);

            var s = new LordStats { ring = (int)ring, nationId = nation.ToString() };

            // 1) 4d6 roll
            s.str = Roll4d6DropLowest(rng);
            s.dex = Roll4d6DropLowest(rng);
            s.con = Roll4d6DropLowest(rng);
            s.intel = Roll4d6DropLowest(rng);
            s.wis = Roll4d6DropLowest(rng);
            s.cha = Roll4d6DropLowest(rng);

            // 2) 국가 성향 보정
            ApplyNationFlavor(ref s, nation);

            // 3) 클램프 3~18
            s.str = Mathf.Clamp(s.str, StatMin, StatMax);
            s.dex = Mathf.Clamp(s.dex, StatMin, StatMax);
            s.con = Mathf.Clamp(s.con, StatMin, StatMax);
            s.intel = Mathf.Clamp(s.intel, StatMin, StatMax);
            s.wis = Mathf.Clamp(s.wis, StatMin, StatMax);
            s.cha = Mathf.Clamp(s.cha, StatMin, StatMax);

            // 4) 목표 합계 리밸런싱 — 낮으면 최저+1, 높으면 최고−1 (캡 도달 시 안전 탈출)
            int target = TargetSumForRing(ring);
            int safety = 256;
            while (s.Total != target && safety-- > 0)
            {
                if (s.Total < target && RaiseLowest(ref s)) continue;
                if (s.Total > target && LowerHighest(ref s)) continue;
                break; // 모든 스탯이 캡 — 더 이상 조정 불가
            }

            s.guard = GuardFromDex(s.dex);
            return s;
        }

        private static bool RaiseLowest(ref LordStats s)
        {
            int lowest = Mathf.Min(Mathf.Min(Mathf.Min(s.str, s.dex), Mathf.Min(s.con, s.intel)), Mathf.Min(s.wis, s.cha));
            if (lowest >= StatMax) return false;
            if (s.str == lowest) { s.str++; return true; }
            if (s.dex == lowest) { s.dex++; return true; }
            if (s.con == lowest) { s.con++; return true; }
            if (s.intel == lowest) { s.intel++; return true; }
            if (s.wis == lowest) { s.wis++; return true; }
            s.cha++; return true;
        }

        private static bool LowerHighest(ref LordStats s)
        {
            int highest = Mathf.Max(Mathf.Max(Mathf.Max(s.str, s.dex), Mathf.Max(s.con, s.intel)), Mathf.Max(s.wis, s.cha));
            if (highest <= StatMin) return false;
            if (s.str == highest) { s.str--; return true; }
            if (s.dex == highest) { s.dex--; return true; }
            if (s.con == highest) { s.con--; return true; }
            if (s.intel == highest) { s.intel--; return true; }
            if (s.wis == highest) { s.wis--; return true; }
            s.cha--; return true;
        }

        /// <summary>영지 정의 자동 조회 오버로드 — TerritoryDefinition.nation/difficulty 사용.</summary>
        public static LordStats GenerateForTerritory(string key)
        {
            var def = TerritoryDatabase.Instance?.GetDefinition(key);
            if (def.HasValue)
                return Generate(key, def.Value.nation, def.Value.difficulty);
            return Generate(key, NationType.None, TerritoryDifficulty.Ring2);
        }

        /// <summary>djb2 문자열 해시 — 결정론 시드 (월드맵 일지 jitter와 동일 관례).</summary>
        public static int Djb2(string text)
        {
            uint hash = 5381u;
            foreach (char c in text)
                hash = ((hash << 5) + hash) + c;
            return unchecked((int)hash);
        }
    }
}
