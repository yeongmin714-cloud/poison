//#define RUN_INITIALIZER // 주석 해제 후 Unity 에디터에서 Play하면 자동 실행

using ProjectName.Core.Data;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 42: 도감 초기 데이터 생성기.
    /// Resources/Encyclopedia/EncyclopediaDatabase.asset 파일이 없을 때
    /// 최초 1회 실행되어 기본 항목들을 생성합니다.
    ///
    /// 사용법:
    /// 1. Unity 에디터에서 이 스크립트가 포함된 씬 실행
    /// 2. #define RUN_INITIALIZER 주석 해제
    /// 3. 자동으로 ScriptableObject가 생성됨
    ///
    /// 또는 에디터 메뉴: Tools > Encyclopedia > Generate Initial Data
    /// </summary>
    public static class EncyclopediaDataInitializer
    {
        private const string DATABASE_PATH = "Assets/Resources/Encyclopedia/EncyclopediaDatabase.asset";

        /// <summary>
        /// 초기 데이터베이스를 생성합니다.
        /// 기존 파일이 있으면 덮어쓰지 않습니다.
        /// </summary>
        [UnityEditor.MenuItem("Tools/Encyclopedia/Generate Initial Data")]
        public static void GenerateDatabase()
        {
#if UNITY_EDITOR
            // 기존 에셋 확인
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<EncyclopediaDatabase>(DATABASE_PATH);
            if (existing != null)
            {
                Debug.Log("[EncyclopediaDataInitializer] 기존 데이터베이스가 있습니다. 업데이트를 위해 삭제 후 재생성하세요.");
                if (!UnityEditor.EditorUtility.DisplayDialog("도감 데이터 생성",
                    "기존 데이터베이스가 있습니다.\n삭제하고 재생성하시겠습니까?\n(저장된 발견 데이터는 유지됩니다)",
                    "예", "아니오"))
                    return;

                UnityEditor.AssetDatabase.DeleteAsset(DATABASE_PATH);
                UnityEditor.AssetDatabase.Refresh();
            }

            var db = ScriptableObject.CreateInstance<EncyclopediaDatabase>();
            db.categories = new System.Collections.Generic.List<EncyclopediaCategoryData>();

            // 8개 카테고리 데이터 생성
            CreateHerbEntries(db);
            CreateMonsterEntries(db);
            CreateCookingEntries(db);
            CreatePotionEntries(db);
            CreateLordEntries(db);
            CreateTerritoryEntries(db);
            CreateDocumentEntries(db);
            CreateAchievementEntries(db);

            // Resources 폴더 확인
            string dirPath = "Assets/Resources/Encyclopedia";
            if (!System.IO.Directory.Exists(dirPath))
            {
                System.IO.Directory.CreateDirectory(dirPath);
                UnityEditor.AssetDatabase.Refresh();
            }

            UnityEditor.AssetDatabase.CreateAsset(db, DATABASE_PATH);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            Debug.Log($"[EncyclopediaDataInitializer] 도감 데이터 생성 완료! " +
                $"{db.TotalEntryCount}개 항목, {db.categories.Count}개 카테고리");
#else
            Debug.LogWarning("[EncyclopediaDataInitializer] 에디터 전용 기능입니다.");
#endif
        }

        // ===== 카테고리별 데이터 =====

        private static void CreateHerbEntries(EncyclopediaDatabase db)
        {
            var cat = new EncyclopediaCategoryData(EncyclopediaCategory.Herb, "약초", "🌿");

            AddEntry(cat, "HERB_A1", "쓴풀", "쓴맛이 나는 기본 약초. 기초 연금술 재료.", EncyclopediaRarity.Common, "초원 지대");
            AddEntry(cat, "HERB_A2", "인삼", "뿌리에서 강한 생명력이 느껴지는 약초.", EncyclopediaRarity.Uncommon, "산림 지대");
            AddEntry(cat, "HERB_A3", "구기자", "붉은 열매가 달린 관목. 시력 회복에 좋다.", EncyclopediaRarity.Common, "언덕 지대");
            AddEntry(cat, "HERB_M1", "우담즙", "신경을 안정시키는 효과가 있는 약초.", EncyclopediaRarity.Common, "습지 지대");
            AddEntry(cat, "HERB_M2", "박하", "상쾌한 향이 나는 약초. 정신 집중에 도움.", EncyclopediaRarity.Common, "초원 지대");
            AddEntry(cat, "HERB_M3", "라벤더", "보라색 꽃이 아름다운 약초. 불면증에 효과.", EncyclopediaRarity.Uncommon, "언덕 지대");
            AddEntry(cat, "HERB_R1", "금불초", "황금빛 꽃잎을 가진 희귀 약초.", EncyclopediaRarity.Rare, "깊은 산속");
            AddEntry(cat, "HERB_R2", "용아초", "용의 이빨을 닮은 전설적인 약초.", EncyclopediaRarity.Epic, "화산 지대");
            AddEntry(cat, "HERB_P1", "고삼", "강한 독성을 지닌 약초. 조심히 다뤄야 한다.", EncyclopediaRarity.Uncommon, "늪지대");
            AddEntry(cat, "HERB_P2", "창포", "뿌리줄기가 굵은 약초. 소화에 좋다.", EncyclopediaRarity.Common, "물가");

            db.categories.Add(cat);
        }

        private static void CreateMonsterEntries(EncyclopediaDatabase db)
        {
            var cat = new EncyclopediaCategoryData(EncyclopediaCategory.Monster, "몬스터", "🥩");

            AddEntry(cat, "MON_M01", "토끼", "온순한 초식 동물. 고기와 가죽을 얻을 수 있다.", EncyclopediaRarity.Common, "초원 지대");
            AddEntry(cat, "MON_M02", "늑대", "무리를 지어 사냥하는 육식 동물.", EncyclopediaRarity.Common, "산림 지대");
            AddEntry(cat, "MON_M03", "곰", "거대한 덩치를 가진 위협적인 몬스터.", EncyclopediaRarity.Uncommon, "깊은 숲");
            AddEntry(cat, "MON_M04", "멧돼지", "돌진 공격을 하는 위험한 야생 동물.", EncyclopediaRarity.Common, "산림 지대");
            AddEntry(cat, "MON_M05", "독사", "강한 독을 가진 뱀. 조심해야 한다.", EncyclopediaRarity.Uncommon, "늪지대");
            AddEntry(cat, "MON_M06", "그리핀", "독수리 머리와 사자 몸을 가진 전설의 짐승.", EncyclopediaRarity.Rare, "높은 산");
            AddEntry(cat, "MON_M07", "드래곤", "모든 생물의 정점에 서 있는 전설의 존재.", EncyclopediaRarity.Legendary, "화산 내부");
            AddEntry(cat, "MON_M08", "고블린", "작지만 교활한 몬스터. 무리 지어 행동한다.", EncyclopediaRarity.Common, "동굴");
            AddEntry(cat, "MON_M09", "오크", "강력한 힘을 가진 녹색 피부의 전사.", EncyclopediaRarity.Uncommon, "오크 부족 마을");
            AddEntry(cat, "MON_M10", "리치", "죽음을 다스리는 강력한 언데드 마법사.", EncyclopediaRarity.Epic, "고대 유적");

            db.categories.Add(cat);
        }

        private static void CreateCookingEntries(EncyclopediaDatabase db)
        {
            var cat = new EncyclopediaCategoryData(EncyclopediaCategory.Cooking, "요리", "🍲");

            AddEntry(cat, "COOK_01", "야채 스프", "신선한 야채로 만든 따뜻한 스프. 체력 회복에 좋다.", EncyclopediaRarity.Common, "기본 요리");
            AddEntry(cat, "COOK_02", "고기 스테이크", "두툼한 고기를 굽고 소금과 후추로 간을 한 요리.", EncyclopediaRarity.Common, "기본 요리");
            AddEntry(cat, "COOK_03", "생선 구이", "갓 잡은 생선을 노릇노릇하게 구운 요리.", EncyclopediaRarity.Common, "항구 마을");
            AddEntry(cat, "COOK_04", "약초 닭백숙", "약초와 닭을 푹 고은 보양식.", EncyclopediaRarity.Uncommon, "한방 요리");
            AddEntry(cat, "COOK_05", "용의 숨결 스튜", "매운 향신료를 듬뿍 넣은 불같은 스튜.", EncyclopediaRarity.Rare, "화산 지대");
            AddEntry(cat, "COOK_06", "제국의 만찬", "온갖 귀한 재료로 만든 제국 최고의 요리.", EncyclopediaRarity.Epic, "제국 수도");
            AddEntry(cat, "COOK_07", "황금 빵", "금가루를 살짝 뿌린 달콤한 빵.", EncyclopediaRarity.Uncommon, "제국 빵집");
            AddEntry(cat, "COOK_08", "드래곤 정식", "드래곤의 알로 만든 전설적인 요리.", EncyclopediaRarity.Legendary, "???");
            AddEntry(cat, "COOK_09", "버섯 수프", "야생 버섯으로 만든 담백한 수프.", EncyclopediaRarity.Common, "숲 속 오두막");
            AddEntry(cat, "COOK_10", "꿀 팬케이크", "꿀을 듬뿍 뿌린 부드러운 팬케이크.", EncyclopediaRarity.Common, "기본 요리");

            db.categories.Add(cat);
        }

        private static void CreatePotionEntries(EncyclopediaDatabase db)
        {
            var cat = new EncyclopediaCategoryData(EncyclopediaCategory.Potion, "약물", "🧪");

            AddEntry(cat, "POT_H01", "치유 물약", "기본적인 치유 효과를 가진 물약.", EncyclopediaRarity.Common, "연금술");
            AddEntry(cat, "POT_H02", "강력 치유 물약", "많은 양의 체력을 회복시키는 물약.", EncyclopediaRarity.Uncommon, "연금술");
            AddEntry(cat, "POT_H03", "완전 회복 물약", "모든 상태 이상을 회복하고 체력을 가득 채운다.", EncyclopediaRarity.Epic, "고급 연금술");
            AddEntry(cat, "POT_B01", "힘의 물약", "일시적으로 힘을 증가시킨다.", EncyclopediaRarity.Common, "연금술");
            AddEntry(cat, "POT_B02", "민첩의 물약", "일시적으로 이동 속도를 증가시킨다.", EncyclopediaRarity.Common, "연금술");
            AddEntry(cat, "POT_B03", "지혜의 물약", "일시적으로 지혜를 증가시킨다.", EncyclopediaRarity.Uncommon, "연금술");
            AddEntry(cat, "POT_P01", "독 물약", "적에게 치명적인 독을 바르는 데 사용.", EncyclopediaRarity.Common, "암살 기술");
            AddEntry(cat, "POT_P02", "수면 가스", "일정 범위의 적을 수면 상태로 만든다.", EncyclopediaRarity.Uncommon, "암살 기술");
            AddEntry(cat, "POT_P03", "폭발 물약", "던지면 폭발하여 주변에 피해를 준다.", EncyclopediaRarity.Rare, "연금술");
            AddEntry(cat, "POT_L01", "불멸의 비약", "일시적으로 죽음을 극복하는 전설의 물약.", EncyclopediaRarity.Legendary, "???");
            AddEntry(cat, "POT_L02", "변신 물약", "일시적으로 다른 모습으로 변신한다.", EncyclopediaRarity.Rare, "고급 연금술");
            AddEntry(cat, "POT_L03", "투명 물약", "일시적으로 투명 상태가 된다.", EncyclopediaRarity.Epic, "고급 연금술");

            db.categories.Add(cat);
        }

        private static void CreateLordEntries(EncyclopediaDatabase db)
        {
            var cat = new EncyclopediaCategoryData(EncyclopediaCategory.Lord, "영주", "👑");

            AddEntry(cat, "LORD_01", "아서 왕", "얼음 왕관 영지를 다스리는 엄격한 영주.", EncyclopediaRarity.Rare, "Ice Crown (북부)");
            AddEntry(cat, "LORD_02", "살라딘", "사막 영지를 다스리는 현명한 영주.", EncyclopediaRarity.Rare, "Sand (서부)");
            AddEntry(cat, "LORD_03", "볼카누스", "화산 영지를 다스리는 강력한 영주.", EncyclopediaRarity.Rare, "Red Desert (남부)");
            AddEntry(cat, "LORD_04", "엘프 여왕", "동부 숲 영지를 다스리는 우아한 영주.", EncyclopediaRarity.Rare, "East Forest (동부)");
            AddEntry(cat, "LORD_05", "황제", "제국 전체를 다스리는 최고 권력자.", EncyclopediaRarity.Epic, "Empire");
            AddEntry(cat, "LORD_06", "항구 백작", "항구 도시를 다스리는 상인 출신 영주.", EncyclopediaRarity.Uncommon, "Port Town (동부)");
            AddEntry(cat, "LORD_07", "늪지 영주", "늪지대를 다스리는 신비로운 영주.", EncyclopediaRarity.Uncommon, "늪지대");
            AddEntry(cat, "LORD_08", "산악 영주", "높은 산악 지대를 다스리는 강인한 영주.", EncyclopediaRarity.Rare, "산악 지대");

            db.categories.Add(cat);
        }

        private static void CreateTerritoryEntries(EncyclopediaDatabase db)
        {
            var cat = new EncyclopediaCategoryData(EncyclopediaCategory.Territory, "영지", "🏰");

            AddEntry(cat, "TERR_01", "얼음 왕관 (Ice Crown)", "영원한 겨울이 지배하는 북부 영지.", EncyclopediaRarity.Uncommon, "북부 대륙");
            AddEntry(cat, "TERR_02", "사막 (Sand)", "끝없는 모래바다가 펼쳐진 서부 영지.", EncyclopediaRarity.Uncommon, "서부 대륙");
            AddEntry(cat, "TERR_03", "붉은 사막 (Red Desert)", "용암이 흐르는 남부 영지.", EncyclopediaRarity.Uncommon, "남부 대륙");
            AddEntry(cat, "TERR_04", "동부 숲 (East Forest)", "울창한 숲이 우거진 동부 영지.", EncyclopediaRarity.Uncommon, "동부 대륙");
            AddEntry(cat, "TERR_05", "제국 (Empire)", "모든 영지를 통합하는 중앙 제국.", EncyclopediaRarity.Epic, "대륙 중앙");
            AddEntry(cat, "TERR_06", "항구 도시 (Port Town)", "무역의 중심지인 항구 영지.", EncyclopediaRarity.Common, "동부 해안");
            AddEntry(cat, "TERR_07", "늪지대 (Swamp)", "안개가 자욱한 위험한 늪 영지.", EncyclopediaRarity.Common, "남서부");
            AddEntry(cat, "TERR_08", "산악 지대 (Mountain)", "높은 봉우리와 깊은 계곡의 영지.", EncyclopediaRarity.Common, "대륙 북동부");
            AddEntry(cat, "TERR_09", "고대 유적 (Ancient Ruins)", "잊혀진 문명의 유적이 있는 영지.", EncyclopediaRarity.Rare, "대륙 곳곳");
            AddEntry(cat, "TERR_10", "비밀의 정원 (Secret Garden)", "전설 속에만 존재한다는 숨겨진 영지.", EncyclopediaRarity.Legendary, "???");

            db.categories.Add(cat);
        }

        private static void CreateDocumentEntries(EncyclopediaDatabase db)
        {
            var cat = new EncyclopediaCategoryData(EncyclopediaCategory.Document, "문서", "📜");

            AddEntry(cat, "DOC_01", "고대 연금술 비급", "잃어버린 연금술 기술이 기록된 문서.", EncyclopediaRarity.Rare, "고대 유적");
            AddEntry(cat, "DOC_02", "제국 건국 신화", "제국이 세워진 이야기를 담은 문서.", EncyclopediaRarity.Uncommon, "제국 도서관");
            AddEntry(cat, "DOC_03", "용의 전설", "드래곤에 관한 모든 기록을 모은 문서.", EncyclopediaRarity.Rare, "???");
            AddEntry(cat, "DOC_04", "약초 백과", "모든 약초의 효능과 채집 장소를 기록한 책.", EncyclopediaRarity.Common, "초원 지대");
            AddEntry(cat, "DOC_05", "몬스터 도감", "몬스터의 약점과 습성을 기록한 사냥꾼의 수첩.", EncyclopediaRarity.Common, "사냥꾼 길드");
            AddEntry(cat, "DOC_06", "영주 혈통서", "모든 영지 영주의 가계도를 기록한 문서.", EncyclopediaRarity.Uncommon, "제국 기록 보관소");
            AddEntry(cat, "DOC_07", "비밀 결사 문서", "암흑 세력의 음모가 기록된 비밀 문서.", EncyclopediaRarity.Epic, "비밀 결사 은신처");
            AddEntry(cat, "DOC_08", "예언의 두루마리", "세계의 종말을 예언한 고대 두루마리.", EncyclopediaRarity.Legendary, "???");
            AddEntry(cat, "DOC_09", "요리 왕의 레시피", "전설적인 요리사가 남긴 비밀 레시피.", EncyclopediaRarity.Uncommon, "제국 주방");
            AddEntry(cat, "DOC_10", "별자리 지도", "밤하늘의 모든 별자리를 기록한 천문 지도.", EncyclopediaRarity.Rare, "천문대");

            db.categories.Add(cat);
        }

        private static void CreateAchievementEntries(EncyclopediaDatabase db)
        {
            var cat = new EncyclopediaCategoryData(EncyclopediaCategory.Achievement, "업적", "🏆");

            AddEntry(cat, "ACH_01", "첫 걸음", "게임을 처음 시작했다.", EncyclopediaRarity.Common, "튜토리얼 완료");
            AddEntry(cat, "ACH_02", "약초 채집가", "10종의 약초를 모두 채집했다.", EncyclopediaRarity.Uncommon, "자연");
            AddEntry(cat, "ACH_03", "몬스터 헌터", "10종의 몬스터를 모두 처치했다.", EncyclopediaRarity.Uncommon, "전투");
            AddEntry(cat, "ACH_04", "대 요리사", "10종의 요리를 모두 제조했다.", EncyclopediaRarity.Uncommon, "요리");
            AddEntry(cat, "ACH_05", "연금술 대가", "12종의 물약을 모두 제조했다.", EncyclopediaRarity.Uncommon, "연금술");
            AddEntry(cat, "ACH_06", "영주와의 만남", "모든 영주를 만났다.", EncyclopediaRarity.Rare, "탐험");
            AddEntry(cat, "ACH_07", "세계의 탐험가", "모든 영지를 방문했다.", EncyclopediaRarity.Rare, "탐험");
            AddEntry(cat, "ACH_08", "문서 수집가", "모든 문서를 발견했다.", EncyclopediaRarity.Rare, "지식");
            AddEntry(cat, "ACH_09", "도감 마스터", "도감을 100% 완성했다.", EncyclopediaRarity.Legendary, "도감");
            AddEntry(cat, "ACH_10", "전설의 시작", "전설 등급 아이템을 획득했다.", EncyclopediaRarity.Epic, "장비");

            db.categories.Add(cat);
        }

        // ===== 헬퍼 =====

        private static void AddEntry(EncyclopediaCategoryData cat, string id, string name,
            string desc, EncyclopediaRarity rarity, string location)
        {
            var entry = ScriptableObject.CreateInstance<EncyclopediaEntry>();
            entry.Initialize(id, cat.category, name, desc, rarity, location);
            entry.name = id; // 에셋 이름
            cat.entries.Add(entry);
        }
    }
}