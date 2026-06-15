using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Core.Data
{
    /// <summary>
    /// 영지 데이터베이스 — 모든 영지(81개)의 정의와 상태를 관리합니다.
    /// 
    /// 사용법:
    ///   TerritoryDatabase db = TerritoryDatabase.Instance;
    ///   TerritoryDefinition def = db.GetDefinition(NationType.East, 1);
    ///   TerritoryState state = db.GetState(NationType.East, 1);
    /// </summary>
    public class TerritoryDatabase
    {
        private static TerritoryDatabase _instance;
        public static TerritoryDatabase Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TerritoryDatabase();
                return _instance;
            }
        }

        private readonly Dictionary<string, TerritoryDefinition> _definitions = new Dictionary<string, TerritoryDefinition>();
        private readonly Dictionary<string, TerritoryState> _states = new Dictionary<string, TerritoryState>();

        private TerritoryDatabase()
        {
            InitializeDefinitions();
            InitializeStates();
        }

        // ===== 정의 조회 =====

        public TerritoryDefinition GetDefinition(NationType nation, int index)
        {
            string key = new TerritoryId(nation, index).ToString();
            if (_definitions.TryGetValue(key, out var def))
                return def;
            Debug.LogWarning($"[TerritoryDatabase] 정의 없음: {key}");
            return default;
        }

        public TerritoryDefinition GetDefinition(TerritoryId id)
        {
            return GetDefinition(id.nation, id.index);
        }

        public IEnumerable<TerritoryDefinition> GetAllDefinitions()
        {
            return _definitions.Values;
        }

        public IEnumerable<TerritoryDefinition> GetDefinitionsByNation(NationType nation)
        {
            foreach (var def in _definitions.Values)
            {
                if (def.nation == nation)
                    yield return def;
            }
        }

        // ===== 상태 조회/변경 =====

        public TerritoryState GetState(NationType nation, int index)
        {
            string key = new TerritoryId(nation, index).ToString();
            if (_states.TryGetValue(key, out var state))
                return state;
            Debug.LogWarning($"[TerritoryDatabase] 상태 없음: {key}");
            return null;
        }

        public TerritoryState GetState(TerritoryId id)
        {
            return GetState(id.nation, id.index);
        }

        public void SetOwnership(NationType nation, int index, TerritoryOwnership ownership)
        {
            var state = GetState(nation, index);
            if (state != null)
                state.ownership = ownership;
        }

        public void SetOwnership(TerritoryId id, TerritoryOwnership ownership)
        {
            SetOwnership(id.nation, id.index, ownership);
        }

        // ===== 초기화 =====

        private void InitializeDefinitions()
        {
            // Phase 5 첫 번째 영지: 튜토리얼 영지 (동(East) Ring 1, 1번)
            AddDefinition(NationType.East, 1, "동쪽 초원지대 1번지", NationType.East,
                TerritoryDifficulty.Ring1, 3,
                new LordInfo
                {
                    lordName = "리카드 경",
                    preferredFood = "구운 고기",
                    chronicDisease = "통풍",
                    loyalty = 70,
                    personality = LordPersonality.Neutral
                },
                "동쪽 국경의 조용한 초원 영지. 병사는 적고 영주는 느긋하다.");

            // 추가 영지는 Phase 진행 시 확장
        }

        private void InitializeStates()
        {
            foreach (var kvp in _definitions)
            {
                _states[kvp.Key] = new TerritoryState(kvp.Value.id);
            }
        }

        private void AddDefinition(NationType nation, int index, string name, NationType nationType,
            TerritoryDifficulty difficulty, int guardCount, LordInfo lord, string description)
        {
            var id = new TerritoryId(nation, index);
            var def = new TerritoryDefinition
            {
                id = id,
                territoryName = name,
                nation = nationType,
                difficulty = difficulty,
                guardCount = guardCount,
                lord = lord,
                description = description
            };
            _definitions[id.ToString()] = def;
        }
    }
}