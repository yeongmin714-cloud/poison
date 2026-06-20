using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 국가 호감도 시스템 싱글톤.
    /// 각 국가(NationType)별 -100~100 범위의 호감도를 관리합니다.
    /// </summary>
    public class NationReputationSystem : MonoBehaviour
    {
        public static NationReputationSystem Instance { get; private set; }

        [Header("국가 호감도 설정")]
        [SerializeField] private int _minReputation = -100;
        [SerializeField] private int _maxReputation = 100;
        [SerializeField] private int _defaultReputation = 0;
        [SerializeField] private int _tutorialReputation = 10; // 동(East) 튜토리얼 영지 기본값

        // 내부 저장: NationType.ToString() → value
        private Dictionary<string, int> _reputations = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDefaults();
        }

        /// <summary>기본 호감도 초기화</summary>
        private void InitializeDefaults()
        {
            foreach (NationType nation in System.Enum.GetValues(typeof(NationType)))
            {
                if (nation == NationType.None) continue;
                _reputations[nation.ToString()] = (nation == NationType.East) ? _tutorialReputation : _defaultReputation;
            }
        }

        // ===== 조회 =====

        /// <summary>특정 국가의 호감도 반환 (-100 ~ 100)</summary>
        public int GetReputation(NationType nation)
        {
            string key = nation.ToString();
            if (_reputations.ContainsKey(key))
                return _reputations[key];
            return _defaultReputation;
        }

        /// <summary>
        /// 호감도에 따른 레벨 텍스트 반환.
        /// 80+=충성, 50+=우호, 20+=호의, -20~20=중립, -50~-20=냉담, -80~-50=적대, -80↓=선전포고
        /// </summary>
        public string GetReputationLevel(NationType nation)
        {
            int rep = GetReputation(nation);
            if (rep >= 80) return "충성";
            if (rep >= 50) return "우호";
            if (rep >= 20) return "호의";
            if (rep > -20) return "중립";
            if (rep > -50) return "냉담";
            if (rep > -80) return "적대";
            return "선전포고";
        }

        /// <summary>
        /// 호감도 기반 가격/효율 배율 반환.
        /// 우호(50+)=0.8x, 호의(20~49)=0.9x, 중립(-20~20)=1.0x, 냉담(-50~-20)=1.2x, 적대(-80~-50)=1.5x
        /// </summary>
        public float GetReputationMultiplier(NationType nation)
        {
            int rep = GetReputation(nation);
            if (rep >= 50) return 0.8f;
            if (rep >= 20) return 0.9f;
            if (rep > -20) return 1.0f;
            if (rep > -50) return 1.2f;
            return 1.5f;
        }

        // ===== 변경 =====

        /// <summary>호감도 증가/감소 (범위 자동 클램프)</summary>
        public void AddReputation(NationType nation, int delta)
        {
            if (nation == NationType.None) return;
            string key = nation.ToString();
            int current = GetReputation(nation);
            current = Mathf.Clamp(current + delta, _minReputation, _maxReputation);
            _reputations[key] = current;
            Debug.Log($"[NationReputation] {nation} 호감도 {delta:+0;-0} → {current} ({GetReputationLevel(nation)})");
        }

        /// <summary>호감도 직접 설정</summary>
        public void SetReputation(NationType nation, int value)
        {
            if (nation == NationType.None) return;
            string key = nation.ToString();
            _reputations[key] = Mathf.Clamp(value, _minReputation, _maxReputation);
        }

        // ===== 저장/로드 =====

        /// <summary>모든 국가 호감도를 Dictionary<string,int>로 반환 (SaveData용)</summary>
        public Dictionary<string, int> GetAllReputations()
        {
            return new Dictionary<string, int>(_reputations);
        }

        /// <summary>저장된 Dictionary<string,int>로 호감도 복원</summary>
        public void LoadAllReputations(Dictionary<string, int> data)
        {
            if (data == null) return;
            _reputations.Clear();
            foreach (var kvp in data)
            {
                _reputations[kvp.Key] = Mathf.Clamp(kvp.Value, _minReputation, _maxReputation);
            }
            // 누락된 국가가 있으면 기본값으로 채움
            foreach (NationType nation in System.Enum.GetValues(typeof(NationType)))
            {
                if (nation == NationType.None) continue;
                string key = nation.ToString();
                if (!_reputations.ContainsKey(key))
                    _reputations[key] = (nation == NationType.East) ? _tutorialReputation : _defaultReputation;
            }
        }
    }
}