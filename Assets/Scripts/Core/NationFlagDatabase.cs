using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// Phase 34: 국가별 국기 데이터베이스.
    /// 6개 국가(NationType.East/West/South/North/Empire/Dracula)의 국기 정의를 제공합니다.
    /// </summary>
    public static class NationFlagDatabase
    {
        private static readonly Dictionary<NationType, NationFlagDefinition> _flags;

        static NationFlagDatabase()
        {
            _flags = new Dictionary<NationType, NationFlagDefinition>
            {
                {
                    NationType.East,
                    new NationFlagDefinition
                    {
                        nation = NationType.East,
                        colorName = "파랑",
                        description = "동쪽의 시작을 상징",
                        flagColor = Color.blue,
                        symbolName = "떠오르는 태양",
                        symbolEmoji = "🌅"
                    }
                },
                {
                    NationType.West,
                    new NationFlagDefinition
                    {
                        nation = NationType.West,
                        colorName = "초록",
                        description = "서쪽의 대지와 자연",
                        flagColor = Color.green,
                        symbolName = "떡갈나무 잎",
                        symbolEmoji = "🌿"
                    }
                },
                {
                    NationType.South,
                    new NationFlagDefinition
                    {
                        nation = NationType.South,
                        colorName = "빨강",
                        description = "남쪽의 열정과 전투",
                        flagColor = Color.red,
                        symbolName = "불꽃",
                        symbolEmoji = "🔥"
                    }
                },
                {
                    NationType.North,
                    new NationFlagDefinition
                    {
                        nation = NationType.North,
                        colorName = "보라",
                        description = "북쪽의 냉철함",
                        flagColor = new Color(0.6f, 0.2f, 1f),
                        symbolName = "눈송이/산",
                        symbolEmoji = "❄️"
                    }
                },
                {
                    NationType.Empire,
                    new NationFlagDefinition
                    {
                        nation = NationType.Empire,
                        colorName = "황금",
                        description = "중앙 황제의 권위",
                        flagColor = new Color(1f, 0.85f, 0.2f),
                        symbolName = "독수리/왕관",
                        symbolEmoji = "👑"
                    }
                },
                {
                    NationType.Dracula,
                    new NationFlagDefinition
                    {
                        nation = NationType.Dracula,
                        colorName = "검정",
                        description = "밤의 어둠과 피",
                        flagColor = new Color(0.8f, 0f, 0f),
                        symbolName = "박쥐",
                        symbolEmoji = "🦇"
                    }
                }
            };
        }

        /// <summary>
        /// 특정 국가의 국기 정의를 반환합니다.
        /// </summary>
        public static NationFlagDefinition GetFlag(NationType nation)
        {
            if (_flags.TryGetValue(nation, out var definition))
                return definition;

            Debug.LogWarning($"[NationFlagDatabase] 정의되지 않은 국가: {nation}");
            return default;
        }

        /// <summary>
        /// 모든 6개 국가의 국기 정의 목록을 반환합니다.
        /// </summary>
        public static List<NationFlagDefinition> GetAllFlags()
        {
            return new List<NationFlagDefinition>(_flags.Values);
        }

        /// <summary>
        /// 주어진 NationType이 유효한 국기 정의를 가지고 있는지 확인합니다.
        /// </summary>
        public static bool HasFlag(NationType nation)
        {
            return _flags.ContainsKey(nation);
        }
    }
}