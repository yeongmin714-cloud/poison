using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>바드 버프 데이터</summary>
    [System.Serializable]
    public struct BardBuffData
    {
        public float attackBonus;   // 공격력 보너스 비율 (0.15 = +15%)
        public float defenseBonus;  // 방어력 보너스 비율
        public float speedBonus;    // 이동속도 보너스 비율
        public string sourceId;     // 버프 출처 용병 ID
    }

    /// <summary>바드 버프 관리자 (싱글톤)</summary>
    public class BardBuffManager : MonoBehaviour
    {
        public static BardBuffManager Instance { get; private set; }

        // 버프 받은 가드 목록: guard instance ID → buff data
        private Dictionary<int, BardBuffData> _buffedGuards =
            new Dictionary<int, BardBuffData>();

        // 버프 받은 용병 목록: mercenary instance ID → buff data
        private Dictionary<int, BardBuffData> _buffedMercenaries =
            new Dictionary<int, BardBuffData>();

        // 버프 출처별 가드 카운트
        private Dictionary<string, HashSet<int>> _sourceToGuards =
            new Dictionary<string, HashSet<int>>();

        // 버프 출처별 용병 카운트
        private Dictionary<string, HashSet<int>> _sourceToMercenaries =
            new Dictionary<string, HashSet<int>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void RegisterBuffedGuard(GuardPlaceholder guard, BardBuffData buffData)
        {
            int id = guard.GetInstanceID();
            _buffedGuards[id] = buffData;

            if (!_sourceToGuards.ContainsKey(buffData.sourceId))
                _sourceToGuards[buffData.sourceId] = new HashSet<int>();
            _sourceToGuards[buffData.sourceId].Add(id);
        }

        public void RegisterBuffedMercenary(MercenaryPlaceholder merc, BardBuffData buffData)
        {
            int id = merc.GetInstanceID();
            _buffedMercenaries[id] = buffData;

            if (!_sourceToMercenaries.ContainsKey(buffData.sourceId))
                _sourceToMercenaries[buffData.sourceId] = new HashSet<int>();
            _sourceToMercenaries[buffData.sourceId].Add(id);
        }

        public void UnregisterGuard(GuardPlaceholder guard)
        {
            int id = guard.GetInstanceID();
            _buffedGuards.Remove(id);
            foreach (var kvp in _sourceToGuards)
            {
                kvp.Value.Remove(id);
            }
        }

        public void UnregisterMercenary(MercenaryPlaceholder merc)
        {
            int id = merc.GetInstanceID();
            _buffedMercenaries.Remove(id);
            foreach (var kvp in _sourceToMercenaries)
            {
                kvp.Value.Remove(id);
            }
        }

        /// <summary>특정 출처(sourceId)의 모든 버프 제거 (바드 파괴/비활성화 시)</summary>
        public void ClearBySource(string sourceId)
        {
            // 가드 버프 제거
            if (_sourceToGuards.TryGetValue(sourceId, out var guardIds))
            {
                foreach (int id in guardIds)
                {
                    _buffedGuards.Remove(id);
                }
                _sourceToGuards.Remove(sourceId);
            }

            // 용병 버프 제거
            if (_sourceToMercenaries.TryGetValue(sourceId, out var mercIds))
            {
                foreach (int id in mercIds)
                {
                    _buffedMercenaries.Remove(id);
                }
                _sourceToMercenaries.Remove(sourceId);
            }
        }

        public bool TryGetBuff(GuardPlaceholder guard, out BardBuffData buffData)
        {
            return _buffedGuards.TryGetValue(guard.GetInstanceID(), out buffData);
        }

        public bool TryGetMercenaryBuff(MercenaryPlaceholder merc, out BardBuffData buffData)
        {
            return _buffedMercenaries.TryGetValue(merc.GetInstanceID(), out buffData);
        }

        public int GetBuffedCountBySource(string sourceId)
        {
            int count = 0;
            if (_sourceToGuards.TryGetValue(sourceId, out var guardSet))
                count += guardSet.Count;
            if (_sourceToMercenaries.TryGetValue(sourceId, out var mercSet))
                count += mercSet.Count;
            return count;
        }

        public void Clear()
        {
            _buffedGuards.Clear();
            _buffedMercenaries.Clear();
            _sourceToGuards.Clear();
            _sourceToMercenaries.Clear();
        }
    }
}