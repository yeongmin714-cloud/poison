namespace ProjectName.Core.Data
{
    /// <summary>
    /// 튜토리얼 가이드 항목을 정의하는 구조체.
    /// 각 가이드는 고유 ID, 제목, 설명, 트리거 액션 정보를 담습니다.
    /// </summary>
    [System.Serializable]
    public struct GuideEntry
    {
        public string id;
        public string title;
        public string description;
        public string actionTrigger;
    }

    /// <summary>
    /// T-Cycle-01: 모든 튜토리얼 가이드 ID 상수와 전체 목록.
    /// T4(야외) 11종 + T6(영지) 5종 + 스페셜 3종 = 총 19종.
    /// </summary>
    public static class TutorialGuideData
    {
        // ================================================================
        // T4: 야외 조작 가이드 (01~11)
        // ================================================================

        public const string ID_01_MOVEMENT = "01_movement";
        public const string ID_02_CAMERA = "02_camera";
        public const string ID_03_ATTACK = "03_attack";
        public const string ID_04_DASH = "04_dash";
        public const string ID_05_ROLL = "05_roll";
        public const string ID_06_CHOP_TREE = "06_chop_tree";
        public const string ID_07_MINE_STONE = "07_mine_stone";
        public const string ID_08_HERB_PICK = "08_herb_pick";
        public const string ID_09_INVENTORY = "09_inventory";
        public const string ID_10_CRAFT = "10_craft";
        public const string ID_11_RECIPE_BOOK = "11_recipe_book";

        // ================================================================
        // T6: 영지 조작 가이드 (12~22)
        // ================================================================

        public const string ID_12_GUARD_INTERACT = "12_guard_interact";
        public const string ID_13_GUARD_INFO = "13_guard_info";
        public const string ID_14_GUARD_EQUIP = "14_guard_equip";
        public const string ID_15_GASMASK = "15_gasmask";
        public const string ID_16_GAS_SPRAYER = "16_gas_sprayer";
        public const string ID_17_GUARD_MISSION = "17_guard_mission";
        public const string ID_18_SHOP = "18_shop";
        public const string ID_19_WORLD_MAP = "19_world_map";
        public const string ID_20_STATUS = "20_status";
        public const string ID_22_BUILDING_ENTER = "22_building_enter";

        // ================================================================
        // 스페셜 가이드 (시퀀스용)
        // ================================================================

        public const string ID_TUTORIAL_START = "tutorial_start";
        public const string ID_EXECUTION_READY = "execution_ready";
        public const string ID_TERRITORY_INTRO = "territory_intro";

        // ================================================================
        // 전체 가이드 목록 (캐시)
        // ================================================================

        private static readonly GuideEntry[] _allGuides = new GuideEntry[]
        {
            // T4: 야외 조작
            new GuideEntry { id = ID_01_MOVEMENT,     title = "이동하기",      description = "WASD 키로 캐릭터를 이동하세요.", actionTrigger = "최초 WASD 입력" },
            new GuideEntry { id = ID_02_CAMERA,       title = "시점 회전",     description = "마우스 우클릭 드래그로 시점을 회전하세요.", actionTrigger = "최초 마우스 우클릭" },
            new GuideEntry { id = ID_03_ATTACK,       title = "몬스터 사냥",   description = "좌클릭으로 몬스터를 공격하세요.", actionTrigger = "최초 좌클릭 공격" },
            new GuideEntry { id = ID_04_DASH,         title = "대쉬",          description = "Shift 키로 빠르게 대쉬할 수 있습니다.", actionTrigger = "최초 Shift 입력" },
            new GuideEntry { id = ID_05_ROLL,         title = "구르기",        description = "Space 키로 구를 수 있습니다 (무적 판정).", actionTrigger = "최초 Space 입력" },
            new GuideEntry { id = ID_06_CHOP_TREE,    title = "나무 채집",     description = "E키로 나무를 캐서 재료를 얻으세요.", actionTrigger = "최초 E키 나무 채집" },
            new GuideEntry { id = ID_07_MINE_STONE,   title = "돌 채집",       description = "E키로 돌을 캐서 재료를 얻으세요.", actionTrigger = "최초 E키 돌 채집" },
            new GuideEntry { id = ID_08_HERB_PICK,    title = "약초 채집",     description = "E키로 약초를 채집하세요.", actionTrigger = "최초 E키 약초 채집" },
            new GuideEntry { id = ID_09_INVENTORY,    title = "인벤토리",      description = "I키로 인벤토리를 열어 아이템을 확인하세요.", actionTrigger = "최초 I키 입력" },
            new GuideEntry { id = ID_10_CRAFT,        title = "제작하기",      description = "크래프트 테이블 앞에서 E키로 아이템을 제작하세요.", actionTrigger = "최초 E키 제작대" },
            new GuideEntry { id = ID_11_RECIPE_BOOK,  title = "레시피 북",     description = "R키로 레시피 북을 열어 조합법을 확인하세요.", actionTrigger = "최초 R키 입력" },

            // T6: 영지 조작
            new GuideEntry { id = ID_12_GUARD_INTERACT, title = "병사 상호작용",  description = "E키로 병사에게 말을 걸어보세요.", actionTrigger = "최초 E키 병사" },
            new GuideEntry { id = ID_13_GUARD_INFO,     title = "병사 정보",      description = "병사 정보창에서 레벨, 호감도, 중독도를 확인하세요.", actionTrigger = "최초 병사 정보창" },
            new GuideEntry { id = ID_14_GUARD_EQUIP,    title = "병사 장비",      description = "병사에게 장비를 지급하면 전투력이 상승합니다.", actionTrigger = "최초 장비 지급" },
            new GuideEntry { id = ID_15_GASMASK,        title = "방독면",         description = "방독면을 장착하면 독안개를 막을 수 있습니다.", actionTrigger = "최초 방독면 장착" },
            new GuideEntry { id = ID_16_GAS_SPRAYER,    title = "가스 분사기",    description = "가스 분사기를 Back 슬롯에 장착해 사용하세요.", actionTrigger = "최초 가스분사기 장착" },
            new GuideEntry { id = ID_17_GUARD_MISSION,  title = "병사 임무",      description = "병사에게 임무를 지정할 수 있습니다 (특사/정보원/약초꾼).", actionTrigger = "최초 임무 지정" },
            new GuideEntry { id = ID_18_SHOP,           title = "상점 이용",      description = "E키로 상점을 열어 아이템을 사고팔 수 있습니다.", actionTrigger = "최초 상점 이용" },
            new GuideEntry { id = ID_19_WORLD_MAP,      title = "월드맵",         description = "M키로 월드맵을 열어 전체 영지를 확인하세요.", actionTrigger = "최초 M키 입력" },
            new GuideEntry { id = ID_20_STATUS,         title = "스테이터스",     description = "C키로 스테이터스 창을 열어 레벨과 스탯을 확인하세요.", actionTrigger = "최초 C키 입력" },
            new GuideEntry { id = ID_22_BUILDING_ENTER, title = "건물 출입",      description = "E키로 건물에 출입할 수 있습니다.", actionTrigger = "최초 건물 출입" },

            // 스페셜
            new GuideEntry { id = ID_TUTORIAL_START,  title = "튜토리얼 시작",  description = "길 잃은 영주를 만났습니다. 퀘스트를 확인하세요!", actionTrigger = "영주 등장 후" },
            new GuideEntry { id = ID_EXECUTION_READY, title = "처형 준비",      description = "모든 재료를 모았습니다! 영주에게 음식을 전달하세요.", actionTrigger = "퀘스트 완료" },
            new GuideEntry { id = ID_TERRITORY_INTRO, title = "첫 영지",        description = "축하합니다! 이제 이 영지가 당신의 것입니다.", actionTrigger = "영지 진입" },
        };

        /// <summary>
        /// 전체 가이드 목록 (캐시된 배열, 읽기 전용 용례 권장)
        /// </summary>
        public static GuideEntry[] AllGuides => _allGuides;

        /// <summary>
        /// ID로 GuideEntry 찾기
        /// </summary>
        public static GuideEntry? FindById(string id)
        {
            foreach (var guide in _allGuides)
            {
                if (guide.id == id)
                    return guide;
            }
            return null;
        }
    }
}