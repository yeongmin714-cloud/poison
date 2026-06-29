using System;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core.Data
{
    /// <summary>
    /// Phase 42: 도감 카테고리 8종.
    /// </summary>
    public enum EncyclopediaCategory
    {
        Herb,       // 🌿 약초
        Monster,    // 🥩 몬스터
        Cooking,    // 🍲 요리
        Potion,     // 🧪 약물
        Lord,       // 👑 영주
        Territory,  // 🏰 영지
        Document,   // 📜 문서
        Achievement // 🏆 업적
    }

    /// <summary>
    /// Phase 42: 도감 등급 (Rarity).
    /// </summary>
    public enum EncyclopediaRarity
    {
        Common,    // 흔함
        Uncommon,  // 희귀
        Rare,      // 영웅
        Epic,      // 전설
        Legendary  // 신화
    }

    /// <summary>
    /// Phase 42: 도감 개별 항목 ScriptableObject.
    /// 에디터에서 직접 생성하거나 EncyclopediaDataInitializer에서 코드로 생성.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEncyclopediaEntry", menuName = "Encyclopedia/Entry")]
    public class EncyclopediaEntry : ScriptableObject
    {
        [Header("식별")]
        public string entryId = "ENTRY_001";
        public EncyclopediaCategory category = EncyclopediaCategory.Herb;

        [Header("정보")]
        public string entryName = "이름";
        [TextArea(3, 6)]
        public string description = "설명";
        public EncyclopediaRarity rarity = EncyclopediaRarity.Common;
        public string location = "발견 장소";

        [Header("발견 상태 (런타임)")]
        [SerializeField] private bool _discovered;
        [SerializeField] private string _discoveryDate;

        /// <summary>발견 여부 (런타임에 변경)</summary>
        public bool IsDiscovered
        {
            get => _discovered;
            set => _discovered = value;
        }

        /// <summary>발견 날짜 문자열 (런타임에 기록)</summary>
        public string DiscoveryDate
        {
            get => _discoveryDate;
            set => _discoveryDate = value;
        }

        /// <summary>
        /// 초기화 헬퍼 (코드에서 생성 시 사용).
        /// </summary>
        public void Initialize(string id, EncyclopediaCategory cat, string name, string desc,
            EncyclopediaRarity r, string loc)
        {
            entryId = id;
            category = cat;
            entryName = name;
            description = desc;
            rarity = r;
            location = loc;
            _discovered = false;
            _discoveryDate = null;
        }

        /// <summary>
        /// 이 항목을 발견 상태로 표시하고 날짜를 기록합니다.
        /// </summary>
        public void Discover()
        {
            if (_discovered) return;
            _discovered = true;
            _discoveryDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }

        /// <summary>
        /// 등급에 따른 표시 색상 (GUI 용).
        /// </summary>
        public Color GetRarityColor()
        {
            switch (rarity)
            {
                case EncyclopediaRarity.Common:    return Color.white;
                case EncyclopediaRarity.Uncommon:  return Color.green;
                case EncyclopediaRarity.Rare:      return Color.cyan;
                case EncyclopediaRarity.Epic:      return new Color(0.8f, 0.2f, 0.9f); // 보라
                case EncyclopediaRarity.Legendary: return new Color(1f, 0.5f, 0f);      // 주황
                default: return Color.white;
            }
        }

        /// <summary>등급 한글 이름</summary>
        public string GetRarityName()
        {
            switch (rarity)
            {
                case EncyclopediaRarity.Common:    return "흔함";
                case EncyclopediaRarity.Uncommon:  return "희귀";
                case EncyclopediaRarity.Rare:      return "영웅";
                case EncyclopediaRarity.Epic:      return "전설";
                case EncyclopediaRarity.Legendary: return "신화";
                default: return "???";
            }
        }
    }

    /// <summary>
    /// Phase 42: 카테고리별 도감 데이터를 묶는 런타임 컨테이너.
    /// ScriptableObject 리스트를 보관하고, 발견 상태를 추적합니다.
    /// </summary>
    [Serializable]
    public class EncyclopediaCategoryData
    {
        public EncyclopediaCategory category;
        public string categoryName;
        public string categoryIcon; // 이모지 아이콘 문자열
        public List<EncyclopediaEntry> entries = new List<EncyclopediaEntry>();

        public EncyclopediaCategoryData(EncyclopediaCategory cat, string name, string icon)
        {
            category = cat;
            categoryName = name;
            categoryIcon = icon;
        }

        /// <summary>이 카테고리의 전체 항목 수</summary>
        public int TotalCount => entries.Count;

        /// <summary>이 카테고리에서 발견된 항목 수</summary>
        public int DiscoveredCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].IsDiscovered) count++;
                return count;
            }
        }

        /// <summary>수집률 (0.0 ~ 1.0)</summary>
        public float CompletionRate => TotalCount > 0 ? (float)DiscoveredCount / TotalCount : 0f;
    }

    /// <summary>
    /// Phase 42: 전체 도감 데이터를 저장하는 ScriptableObject.
    /// Resources/Encyclopedia/ 폴더에 배치하여 런타임 로드.
    /// </summary>
    [CreateAssetMenu(fileName = "EncyclopediaDatabase", menuName = "Encyclopedia/Database")]
    public class EncyclopediaDatabase : ScriptableObject
    {
        public List<EncyclopediaCategoryData> categories = new List<EncyclopediaCategoryData>();

        /// <summary>전체 항목 수</summary>
        public int TotalEntryCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < categories.Count; i++)
                    count += categories[i].TotalCount;
                return count;
            }
        }

        /// <summary>전체 발견 항목 수</summary>
        public int TotalDiscoveredCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < categories.Count; i++)
                    count += categories[i].DiscoveredCount;
                return count;
            }
        }

        /// <summary>전체 수집률 (0.0 ~ 1.0)</summary>
        public float OverallCompletionRate =>
            TotalEntryCount > 0 ? (float)TotalDiscoveredCount / TotalEntryCount : 0f;

        /// <summary>ID로 항목 찾기</summary>
        public EncyclopediaEntry FindEntryById(string entryId)
        {
            for (int c = 0; c < categories.Count; c++)
            {
                var cat = categories[c];
                for (int e = 0; e < cat.entries.Count; e++)
                {
                    if (cat.entries[e].entryId == entryId)
                        return cat.entries[e];
                }
            }
            return null;
        }

        /// <summary>카테고리별 데이터 가져오기</summary>
        public EncyclopediaCategoryData GetCategory(EncyclopediaCategory cat)
        {
            for (int i = 0; i < categories.Count; i++)
                if (categories[i].category == cat)
                    return categories[i];
            return null;
        }
    }
}