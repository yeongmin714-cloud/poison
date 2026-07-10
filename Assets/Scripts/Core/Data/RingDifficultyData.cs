using UnityEngine;
using System.Collections.Generic;
#pragma warning disable 0414

namespace ProjectName.Core.Data
{
    /// <summary>
    /// ROADMAP Phase 3.1 — 방사형 난이도 시스템 데이터.
    /// 
    /// 각 Ring(TerritoryDifficulty)에 해당하는 모든 난이도 매개변수를
    /// 중앙 집중식으로 제공합니다.
    ///
    /// 사용법:
    ///   RingDifficultyData.GetGuardLevelRange(TerritoryDifficulty.Ring1) → Vector2Int(1, 3)
    ///   RingDifficultyData.GetLordTasteTier(TerritoryDifficulty.Ring4) → LordTasteTier.Royal
    ///   RingDifficultyData.GetMonsterTierForDifficulty(TerritoryDifficulty.Ring2) → MonsterTier[]
    /// </summary>
    public static class RingDifficultyData
    {
        // ========================================================
        // 영주 입맛 등급
        // ========================================================
        public enum LordTasteTier
        {
            Basic,    // 아무 음식 OK (Ring1)
            Standard, // 기본 요리 선호 (Ring2)
            Gourmet,  // 고급 요리만 (Ring3)
            Royal     // 왕실급 최고 요리만 (Ring4/Empire)
        }

        // ========================================================
        // 방어 등급
        // ========================================================
        public enum DefenseRating
        {
            Low,       // 낮음 (Ring1)
            Medium,    // 보통 (Ring2)
            High,      // 높음 (Ring3)
            VeryHigh   // 매우 높음 (Ring4/Empire)
        }

        // ========================================================
        // 점령 보상 등급
        // ========================================================
        public enum RewardTier
        {
            Small,     // 작음 (Ring1)
            Medium,    // 보통 (Ring2)
            Large,     // 큼 (Ring3)
            VeryLarge  // 매우 큼 (Ring4/Empire)
        }

        // ========================================================
        // 병사 레벨 범위 — Ring별 기본 (ROADMAP 3.1 표)
        // ========================================================

        /// <summary>
        /// Ring별 병사 레벨 범위 반환 (Ring1=1~10, Ring2=11~20, Ring3=21~30, Ring4=31~40)
        /// </summary>
        public static Vector2Int GetGuardLevelRange(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => new Vector2Int(1, 10),
                TerritoryDifficulty.Ring2  => new Vector2Int(11, 20),
                TerritoryDifficulty.Ring3  => new Vector2Int(21, 30),
                TerritoryDifficulty.Ring4  => new Vector2Int(31, 40),
                TerritoryDifficulty.Empire => new Vector2Int(41, 50),
                _                         => new Vector2Int(1, 5)
            };
        }

        /// <summary>
        /// 국가별 Ring별 병사 레벨 범위 (ROADMAP 국가 가중치 표)
        /// </summary>
        public static Vector2Int GetGuardLevelRange(NationType nation, TerritoryDifficulty difficulty)
        {
            return (nation, difficulty) switch
            {
                // 동 (East) — 초급, 시작 지역
                (NationType.East, TerritoryDifficulty.Ring1) => new Vector2Int(1, 3),
                (NationType.East, TerritoryDifficulty.Ring2) => new Vector2Int(4, 8),
                (NationType.East, TerritoryDifficulty.Ring3) => new Vector2Int(9, 14),
                (NationType.East, TerritoryDifficulty.Ring4) => new Vector2Int(15, 20),

                // 서 (West) — 중상급
                (NationType.West, TerritoryDifficulty.Ring1) => new Vector2Int(3, 6),
                (NationType.West, TerritoryDifficulty.Ring2) => new Vector2Int(7, 12),
                (NationType.West, TerritoryDifficulty.Ring3) => new Vector2Int(13, 18),
                (NationType.West, TerritoryDifficulty.Ring4) => new Vector2Int(19, 25),

                // 남 (South) — 고급
                (NationType.South, TerritoryDifficulty.Ring1) => new Vector2Int(5, 9),
                (NationType.South, TerritoryDifficulty.Ring2) => new Vector2Int(10, 15),
                (NationType.South, TerritoryDifficulty.Ring3) => new Vector2Int(16, 22),
                (NationType.South, TerritoryDifficulty.Ring4) => new Vector2Int(23, 30),

                // 북 (North) — 최고 난이도
                (NationType.North, TerritoryDifficulty.Ring1) => new Vector2Int(8, 12),
                (NationType.North, TerritoryDifficulty.Ring2) => new Vector2Int(13, 20),
                (NationType.North, TerritoryDifficulty.Ring3) => new Vector2Int(21, 28),
                (NationType.North, TerritoryDifficulty.Ring4) => new Vector2Int(29, 40),

                // 황제국
                (_, TerritoryDifficulty.Empire) => new Vector2Int(41, 50),

                _ => new Vector2Int(1, 5)
            };
        }

        // ========================================================
        // 병사 수 범위 — Ring별 (ROADMAP 3.1 표)
        // ========================================================

        /// <summary>
        /// Ring별 병사 수 범위 반환
        /// Ring1=3~5, Ring2=6~10, Ring3=11~20, Ring4=21~40
        /// </summary>
        public static Vector2Int GetGuardCountRange(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => new Vector2Int(3, 5),
                TerritoryDifficulty.Ring2  => new Vector2Int(6, 10),
                TerritoryDifficulty.Ring3  => new Vector2Int(11, 20),
                TerritoryDifficulty.Ring4  => new Vector2Int(21, 40),
                TerritoryDifficulty.Empire => new Vector2Int(50, 50),
                _                         => new Vector2Int(3, 5)
            };
        }

        // ========================================================
        // 영주 입맛 등급 (ROADMAP 3.1 표)
        // ========================================================

        /// <summary>
        /// Ring별 영주 입맛 난이도
        /// Ring1=아무 음식OK, Ring2=기본 요리, Ring3=고급 요리, Ring4=왕실급
        /// </summary>
        public static LordTasteTier GetLordTasteTier(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => LordTasteTier.Basic,
                TerritoryDifficulty.Ring2  => LordTasteTier.Standard,
                TerritoryDifficulty.Ring3  => LordTasteTier.Gourmet,
                TerritoryDifficulty.Ring4  => LordTasteTier.Royal,
                TerritoryDifficulty.Empire => LordTasteTier.Royal,
                _                         => LordTasteTier.Basic
            };
        }

        // ========================================================
        // 영주 지병 (ROADMAP 3.1 표)
        // ========================================================

        /// <summary>
        /// Ring별 영주 지병 수
        /// Ring1=0개, Ring2=0~1개, Ring3=1~2개, Ring4=2~3개
        /// </summary>
        public static Vector2Int GetLordDiseaseCountRange(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => new Vector2Int(0, 0),
                TerritoryDifficulty.Ring2  => new Vector2Int(0, 1),
                TerritoryDifficulty.Ring3  => new Vector2Int(1, 2),
                TerritoryDifficulty.Ring4  => new Vector2Int(2, 3),
                TerritoryDifficulty.Empire => new Vector2Int(3, 4),
                _                         => new Vector2Int(0, 0)
            };
        }

        // ========================================================
        // 방어 등급 (ROADMAP 3.1 표)
        // ========================================================

        /// <summary>
        /// Ring별 방어 등급
        /// Ring1=낮음, Ring2=보통, Ring3=높음, Ring4=매우 높음
        /// </summary>
        public static DefenseRating GetDefenseRating(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => DefenseRating.Low,
                TerritoryDifficulty.Ring2  => DefenseRating.Medium,
                TerritoryDifficulty.Ring3  => DefenseRating.High,
                TerritoryDifficulty.Ring4  => DefenseRating.VeryHigh,
                TerritoryDifficulty.Empire => DefenseRating.VeryHigh,
                _                         => DefenseRating.Low
            };
        }

        /// <summary>
        /// 방어 등급 배율 (전투력 계산용)
        /// Low=0.8x, Medium=1.0x, High=1.3x, VeryHigh=1.6x
        /// </summary>
        public static float GetDefenseMultiplier(TerritoryDifficulty difficulty)
        {
            return GetDefenseRating(difficulty) switch
            {
                DefenseRating.Low      => 0.8f,
                DefenseRating.Medium   => 1.0f,
                DefenseRating.High     => 1.3f,
                DefenseRating.VeryHigh => 1.6f,
                _                      => 1.0f
            };
        }

        // ========================================================
        // 점령 보상 등급 (ROADMAP 3.1 표)
        // ========================================================

        /// <summary>
        /// Ring별 점령 보상 등급
        /// Ring1=작음, Ring2=보통, Ring3=큼, Ring4=매우 큼
        /// </summary>
        public static RewardTier GetRewardTier(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => RewardTier.Small,
                TerritoryDifficulty.Ring2  => RewardTier.Medium,
                TerritoryDifficulty.Ring3  => RewardTier.Large,
                TerritoryDifficulty.Ring4  => RewardTier.VeryLarge,
                TerritoryDifficulty.Empire => RewardTier.VeryLarge,
                _                         => RewardTier.Small
            };
        }

        /// <summary>
        /// 보상 배율 (보상량 계산용)
        /// Small=1x, Medium=2x, Large=4x, VeryLarge=8x
        /// </summary>
        public static float GetRewardMultiplier(TerritoryDifficulty difficulty)
        {
            return GetRewardTier(difficulty) switch
            {
                RewardTier.Small     => 1.0f,
                RewardTier.Medium    => 2.0f,
                RewardTier.Large     => 4.0f,
                RewardTier.VeryLarge => 8.0f,
                _                    => 1.0f
            };
        }

        // ========================================================
        // 난이도 별점 (ROADMAP 3.1 표)
        // ========================================================

        /// <summary>
        /// Ring별 난이도 별점 문자열
        /// </summary>
        public static string GetDifficultyStars(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => "⭐~⭐⭐",
                TerritoryDifficulty.Ring2  => "⭐⭐~⭐⭐⭐",
                TerritoryDifficulty.Ring3  => "⭐⭐⭐~⭐⭐⭐⭐",
                TerritoryDifficulty.Ring4  => "⭐⭐⭐⭐~⭐⭐⭐⭐⭐",
                TerritoryDifficulty.Empire => "⭐⭐⭐⭐⭐",
                _                         => "⭐"
            };
        }

        /// <summary>
        /// 난이도 티어 번호 (1~5, 디버그/계산용)
        /// </summary>
        public static int GetDifficultyTier(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => 1,
                TerritoryDifficulty.Ring2  => 2,
                TerritoryDifficulty.Ring3  => 3,
                TerritoryDifficulty.Ring4  => 4,
                TerritoryDifficulty.Empire => 5,
                _                         => 1
            };
        }

        // ========================================================
        // 몬스터 배치 매핑 (ROADMAP 3.6 표)
        // ========================================================

        /// <summary>
        /// 영지 난이도별 배치 몬스터 티어 배열
        /// (Phase 3.6 — MonsterSpawner 연동)
        /// </summary>
        public static MonsterTier[] GetMonsterTiersForDifficulty(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => new[] { MonsterTier.Beginner },
                TerritoryDifficulty.Ring2  => new[] { MonsterTier.Beginner, MonsterTier.Intermediate },
                TerritoryDifficulty.Ring3  => new[] { MonsterTier.Intermediate },
                TerritoryDifficulty.Ring4  => new[] { MonsterTier.Intermediate, MonsterTier.Advanced },
                TerritoryDifficulty.Empire => new[] { MonsterTier.Advanced },
                _                         => new[] { MonsterTier.Beginner }
            };
        }

        /// <summary>
        /// 영지 난이도별 몬스터 마리 수 범위 (ROADMAP 3.6 표)
        /// (Phase 3.6 — MonsterSpawner 연동)
        /// </summary>
        public static Vector2Int GetMonsterCountRange(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => new Vector2Int(3, 4),
                TerritoryDifficulty.Ring2  => new Vector2Int(4, 5),
                TerritoryDifficulty.Ring3  => new Vector2Int(3, 5),
                TerritoryDifficulty.Ring4  => new Vector2Int(4, 6),
                TerritoryDifficulty.Empire => new Vector2Int(8, 12),
                _                         => new Vector2Int(3, 4)
            };
        }

        /// <summary>
        /// 특정 Ring에 해당하는 영지 인덱스 범위 (1~20, 5개씩)
        /// </summary>
        public static Vector2Int GetTerritoryIndicesForRing(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => new Vector2Int(1, 5),
                TerritoryDifficulty.Ring2 => new Vector2Int(6, 10),
                TerritoryDifficulty.Ring3 => new Vector2Int(11, 15),
                TerritoryDifficulty.Ring4 => new Vector2Int(16, 20),
                _                       => new Vector2Int(1, 1)
            };
        }

        /// <summary>
        /// 국가 이름 (한글) 반환
        /// </summary>
        public static string GetNationDisplayName(NationType nation)
        {
            return nation switch
            {
                NationType.East    => "동 (East)",
                NationType.West    => "서 (West)",
                NationType.South   => "남 (South)",
                NationType.North   => "북 (North)",
                NationType.Empire  => "황제국",
                NationType.Dracula => "드라큘라",
                _                 => "알 수 없음"
            };
        }

        // ===== 유틸리티 =====

        /// <summary>
        /// 병사 레벨을 국가/Ring 기반으로 랜덤 생성 (시드 기반 결정론적)
        /// </summary>
        public static int GenerateGuardLevel(NationType nation, TerritoryDifficulty difficulty, int seed)
        {
            var rng = new System.Random(seed);
            Vector2Int range = GetGuardLevelRange(nation, difficulty);
            return rng.Next(range.x, range.y + 1);
        }

        /// <summary>
        /// 영지 난이도 설명 문자열
        /// </summary>
        public static string GetDifficultyDescription(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1  => "🟢 최외곽 — 쉬움",
                TerritoryDifficulty.Ring2  => "🟡 중간 바깥 — 보통",
                TerritoryDifficulty.Ring3  => "🟠 중간 안쪽 — 어려움",
                TerritoryDifficulty.Ring4  => "🔴 황제국 인접 — 매우 어려움",
                TerritoryDifficulty.Empire => "👑 황제국 — 최종",
                _                         => "❓ 알 수 없음"
            };
        }
    }
}