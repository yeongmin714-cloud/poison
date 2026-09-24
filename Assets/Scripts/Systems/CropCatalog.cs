using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 작물 100종 카탈로그 (09-24 신규) — 이름 개편된 crop-*.glb 100종 전체.
    ///
    /// GLB: Assets/새로운 glb/crops-fish/crop-&lt;Key&gt;.glb (crop-apple.glb ~ crop-호밀.glb,
    /// 구 crop-c002~c100.glb는 삭제됨). 인덱스 순서 = CropKeys 배열 순서(결정론 고정).
    ///
    /// 등급(결정론 인덱스 해시): [0..79] Common, [80..94] Uncommon, [95..99] Rare.
    ///   hash = (index * 2654435761) % 5 → 결측 시 인접 밴드로 승격/강등 없이 단순 밴드 유지.
    ///
    /// 아이템: crop_&lt;Key&gt;(수확물, maxStack 99) / crop_&lt;Key&gt;_seed(씨앗, maxStack 20).
    /// HerbPickup._cropKey 경유로 수확/씨앗 드랍 모두 카탈로그 아이템을 사용한다.
    /// </summary>
    public static class CropCatalog
    {
        const string GLB_DIR = "Assets/새로운 glb/crops-fish/";

        public const int TotalCount = 100;
        public const int UncommonStart = 80;   // [0..79] Common
        public const int RareStart = 95;       // [80..94] Uncommon, [95..99] Rare

        /// <summary>작물 100종 키(GLB 파일명에서 crop- 접두사/.glb 제거, 순서 고정).</summary>
        public static readonly string[] Keys =
        {
            "apple", "carrot", "corn", "peach", "pear", "plum", "pumpkin", "radish",
            "가지", "감", "감자", "강낭콩", "고구마", "고수", "고추", "귀리", "귤",
            "기장", "녹두", "대추", "대파", "들깨", "딸기", "땅콩", "라임", "라즈베리",
            "레몬", "로즈마리", "마", "마늘", "망고", "매실", "메밀", "멜론", "모과",
            "무화과", "미나리", "밀", "바나나", "바질", "밤", "배추", "벼", "병아리콩",
            "보리", "부추", "브로콜리", "블랙베리", "블루베리", "비트", "사탕수수",
            "살구", "상추", "샐러리", "생강", "석류", "수박", "수수", "순무", "시금치",
            "쑷갓", "아보카도", "아스파라거스", "아티초크", "양배추", "양파", "연근",
            "오디", "오렌지", "오이", "오크라", "완두", "우엉", "유자", "유채", "자몽",
            "잠두", "조", "차", "참깨", "참외", "청경채", "체리", "카카오", "커피",
            "케일", "콜리플라워", "콩", "크랜배리", "키위", "토란", "토마토", "파슬리",
            "파인애플", "파프리카", "팥", "포도", "해바라기", "호두", "호밀",
        };

        static readonly string[] _korean =
        {
            "사과", "당근", "옥수수", "복숭아", "배", "자두", "호박", "무",
            "가지", "감", "감자", "강낭콩", "고구마", "고수", "고추", "귀리", "귤",
            "기장", "녹두", "대추", "대파", "들깨", "딸기", "땅콩", "라임", "라즈베리",
            "레몬", "로즈마리", "마", "마늘", "망고", "매실", "메밀", "멜론", "모과",
            "무화과", "미나리", "밀", "바나나", "바질", "밤", "배추", "벼", "병아리콩",
            "보리", "부추", "브로콜리", "블랙베리", "블루베리", "비트", "사탕수수",
            "살구", "상추", "샐러리", "생강", "석류", "수박", "수수", "순무", "시금치",
            "쑷갓", "아보카도", "아스파라거스", "아티초크", "양배추", "양파", "연근",
            "오디", "오렌지", "오이", "오크라", "완두", "우엉", "유자", "유채", "자몽",
            "잠두", "조", "차", "참깨", "참외", "청경채", "체리", "카카오", "커피",
            "케일", "콜리플라워", "콩", "크랜배리", "키위", "토란", "토마토", "파슬리",
            "파인애플", "파프리카", "팥", "포도", "해바라기", "호두", "호밀",
        };

        static readonly Dictionary<string, int> _indexByKey = BuildIndex();

        static Dictionary<string, int> BuildIndex()
        {
            var d = new Dictionary<string, int>(TotalCount);
            for (int i = 0; i < TotalCount; i++)
                d[Keys[i]] = i;
            return d;
        }

        /// <summary>키로 인덱스 조회 (미등록 키면 -1).</summary>
        public static int IndexOf(string key) =>
            key != null && _indexByKey.TryGetValue(key, out int i) ? i : -1;

        /// <summary>GLB 에셋 경로 (에디터 AssetDatabase 로드용 — FishCatalog/NaturalResourceSpawner 선례).</summary>
        public static string GlbPath(int index) => GLB_DIR + "crop-" + Keys[index] + ".glb";

        /// <summary>결정론 인덱스 해시 (0..4) — Uncommon/Rare 승격 후보용.</summary>
        public static int HashIndex(int index) => (int)((long)index * 2654435761L % 5);

        /// <summary>인덱스 → 등급 (결정론: [0..79] Common / [80..94] Uncommon / [95..99] Rare).</summary>
        public static ItemRarity RarityOf(int index) =>
            index >= RareStart ? ItemRarity.Rare
          : index >= UncommonStart ? ItemRarity.Uncommon
          : ItemRarity.Common;

        /// <summary>수확물 ItemData (정적 캐시 없이 새 인스턴스 — FishCatalog.AllItems와 달리 호출빈도 낮음).</summary>
        public static PlayerInventory.ItemData GetItem(int index)
        {
            string key = Keys[index];
            return new PlayerInventory.ItemData
            {
                id = "crop_" + key,
                displayName = "🌾 " + _korean[index],
                description = "수확한 " + _korean[index] + ". 신선하다.",
                category = PlayerInventory.ItemCategory.Food,
                maxStack = 99,
                rarity = RarityOf(index),
            };
        }

        /// <summary>씨앗 ItemData — 파종/재배용(코드 정책상 Food 카테고리).</summary>
        public static PlayerInventory.ItemData GetSeed(int index)
        {
            string key = Keys[index];
            return new PlayerInventory.ItemData
            {
                id = "crop_" + key + "_seed",
                displayName = "🌰 " + _korean[index] + "씨",
                description = _korean[index] + " 씨앗. 심으면 자란다.",
                category = PlayerInventory.ItemCategory.Food,
                maxStack = 20,
                rarity = RarityOf(index),
            };
        }

        public static PlayerInventory.ItemData GetItemByKey(string key)
        {
            int i = IndexOf(key);
            return i >= 0 ? GetItem(i) : PlayerInventory.Fruit_Apple;   // 폴백 (HerbPickup 기존 경로 유지)
        }

        public static PlayerInventory.ItemData GetSeedByKey(string key)
        {
            int i = IndexOf(key);
            return i >= 0 ? GetSeed(i) : PlayerInventory.Fruit_AppleSeed;   // 폴백
        }

        public static string GetKorean(string key)
        {
            int i = IndexOf(key);
            return i >= 0 ? _korean[i] : key;
        }
    }
}
