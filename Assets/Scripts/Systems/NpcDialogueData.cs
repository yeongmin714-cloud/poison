using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 37-03: NPC 일상 대사 데이터 ScriptableObject.
    /// 시간대별, 상황별 대사 목록을 저장합니다.
    /// 각 NPC마다 별도의 ScriptableObject로 생성하여 할당합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewNpcDialogueData", menuName = "Environmental/NpcDialogueData")]
    public class NpcDialogueData : ScriptableObject
    {
        [Header("NPC 기본 정보")]
        [SerializeField] private string _npcId;
        public string NpcId => _npcId;

        [SerializeField] private string _npcName;
        public string NpcName => _npcName;

        [SerializeField, TextArea(2, 4)] private string _npcDescription;
        public string NpcDescription => _npcDescription;

        [Header("시간대별 대사")]
        [SerializeField] private TimeSlotDialogues _morningDialogues = new TimeSlotDialogues("아침", 5, 11);
        [SerializeField] private TimeSlotDialogues _afternoonDialogues = new TimeSlotDialogues("오후", 11, 17);
        [SerializeField] private TimeSlotDialogues _eveningDialogues = new TimeSlotDialogues("저녁", 17, 21);
        [SerializeField] private TimeSlotDialogues _nightDialogues = new TimeSlotDialogues("밤", 21, 5);

        [Header("특수 상황 대사")]
        [SerializeField] private List<string> _rainDialogues = new List<string>();
        [SerializeField] private List<string> _snowDialogues = new List<string>();
        [SerializeField] private List<string> _festivalDialogues = new List<string>();
        [SerializeField] private List<string> _combatDialogues = new List<string>();

        [Header("E키 상호작용 대사 (랜덤)")]
        [SerializeField] private List<string> _interactionDialogues = new List<string>();

        // ================================================================
        // 시간대별 대사 클래스
        // ================================================================

        [System.Serializable]
        public class TimeSlotDialogues
        {
            [SerializeField] private string _slotName;
            [SerializeField] private int _startHour;  // 시작 시간 (포함)
            [SerializeField] private int _endHour;    // 종료 시간 (미포함, 밤 21~5 처리용)

            [SerializeField] private List<string> _dialogues = new List<string>();

            public string SlotName => _slotName;
            public int StartHour => _startHour;
            public int EndHour => _endHour;
            public List<string> Dialogues => _dialogues;

            public TimeSlotDialogues(string slotName, int startHour, int endHour)
            {
                _slotName = slotName;
                _startHour = startHour;
                _endHour = endHour;
            }

            /// <summary>
            /// 현재 시간이 이 슬롯에 속하는지 확인합니다.
            /// 밤(21~5)은 자정을 넘어가므로 특별 처리.
            /// </summary>
            public bool IsInSlot(int currentHour)
            {
                if (_startHour < _endHour)
                {
                    // 일반 범위 (아침 5~11, 오후 11~17, 저녁 17~21)
                    return currentHour >= _startHour && currentHour < _endHour;
                }
                else
                {
                    // 자정을 넘는 범위 (밤 21~5)
                    return currentHour >= _startHour || currentHour < _endHour;
                }
            }

            /// <summary>
            /// 이 슬롯에서 랜덤 대사를 반환합니다. 대사가 없으면 빈 문자열.
            /// </summary>
            public string GetRandomDialogue()
            {
                if (_dialogues == null || _dialogues.Count == 0)
                    return string.Empty;

                return _dialogues[Random.Range(0, _dialogues.Count)];
            }
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 현재 시간에 맞는 시간대 슬롯을 반환합니다.
        /// </summary>
        public TimeSlotDialogues GetCurrentTimeSlot(int currentHour)
        {
            if (_morningDialogues.IsInSlot(currentHour)) return _morningDialogues;
            if (_afternoonDialogues.IsInSlot(currentHour)) return _afternoonDialogues;
            if (_eveningDialogues.IsInSlot(currentHour)) return _eveningDialogues;
            if (_nightDialogues.IsInSlot(currentHour)) return _nightDialogues;
            return _morningDialogues; // fallback
        }

        /// <summary>
        /// 현재 시간대에 맞는 랜덤 대사를 반환합니다.
        /// </summary>
        public string GetRandomDialogueForTime(int currentHour)
        {
            var slot = GetCurrentTimeSlot(currentHour);
            if (slot != null)
            {
                string dialogue = slot.GetRandomDialogue();
                if (!string.IsNullOrEmpty(dialogue))
                    return dialogue;
            }

            // fallback: 상호작용 대사
            return GetRandomInteractionDialogue();
        }

        /// <summary>
        /// 랜덤 상호작용(E키) 대사를 반환합니다.
        /// </summary>
        public string GetRandomInteractionDialogue()
        {
            if (_interactionDialogues == null || _interactionDialogues.Count == 0)
                return "...";

            return _interactionDialogues[Random.Range(0, _interactionDialogues.Count)];
        }

        /// <summary>
        /// 날씨에 따른 특수 대사를 반환합니다 (있을 경우).
        /// </summary>
        public string GetWeatherDialogue(WeatherManager.WeatherType weather)
        {
            List<string> pool = weather switch
            {
                WeatherManager.WeatherType.Rain => _rainDialogues,
                WeatherManager.WeatherType.Snow => _snowDialogues,
                _ => null
            };

            if (pool != null && pool.Count > 0)
                return pool[Random.Range(0, pool.Count)];

            return string.Empty;
        }
    }
}