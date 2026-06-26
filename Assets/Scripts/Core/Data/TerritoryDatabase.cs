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

        // ===== 영주 이름 배열 (Core 내장 — NamePools 의존 없음) =====
        private static readonly string[] _lordEastNames = {
            "세드릭", "알드릭", "레오나드", "마커스", "펜드릭",
            "드레이크", "알릭", "에드릭", "하롤드", "랜슬롯",
            "고드윈", "테오도어", "에드먼드", "로드릭", "가윈",
            "오스왈드", "레지날드", "프레드릭", "밀러드", "오스릭"
        };
        private static readonly string[] _lordWestNames = {
            "가레트", "던컨", "루시우스", "발터", "고든",
            "레이너드", "마그누스", "알딘", "콘래드", "필립",
            "시어도어", "윌리엄", "아서", "라파엘", "길버트",
            "호레이스", "제라드", "랜돌프", "실레스", "티모시"
        };
        private static readonly string[] _lordSouthNames = {
            "발카르", "렉산더", "크라토스", "유스티안", "티베리우스",
            "막시무스", "마리우스", "센트리우스", "아티우스", "게르마니쿠스",
            "세베루스", "아우렐리우스", "에어리쿠스", "타키투스", "베스파시아누스",
            "트라야누스", "옥타비우스", "플라비우스", "메텔루스", "코르넬리우스"
        };
        private static readonly string[] _lordNorthNames = {
            "언윈", "울릭", "프리스트", "브라엄", "라그나르",
            "스벤", "비요른", "에이리크", "군나르", "이바르",
            "라우리츠", "크누트", "하랄드", "올라프", "시구르드",
            "엘리프", "헤르만", "볼프강", "디트리히", "알브레히트"
        };

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

        public TerritoryDefinition GetDefinition(string key)
        {
            if (_definitions.TryGetValue(key, out var def))
                return def;
            Debug.LogWarning($"[TerritoryDatabase] 정의 없음: {key}");
            return default;
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
            GenerateAllDefinitions();
        }

        /// <summary>
        /// 81개 전 영지 정의 생성 (Seed 기반 결정론적 생성)
        /// </summary>
        private void GenerateAllDefinitions()
        {
            // 국가별, 링별로 5개씩 총 80영지 + 황제국 1영지 생성
            foreach (NationType nation in new[] { NationType.East, NationType.West, NationType.South, NationType.North })
            {
                for (int ring = 0; ring < 4; ring++)
                {
                    TerritoryDifficulty difficulty = (TerritoryDifficulty)ring;
                    int baseGuardCount = GetGuardCount(nation, difficulty);

                    for (int t = 0; t < 5; t++)
                    {
                        int index = ring * 5 + t + 1; // 1~20
                        string name = GenerateTerritoryName(nation, index);
                        LordInfo lord = GenerateLordInfo(nation, difficulty, index);
                        string desc = GenerateDescription(nation, difficulty, index);

                        AddDefinition(nation, index, name, nation, difficulty, baseGuardCount, lord, desc);
                    }
                }
            }

            // 황제국 (인덱스 1, Ring = Empire, 병사 50명)
            {
                var rng = new System.Random("Empire_1".GetHashCode());
                string[] empireNames = {
                    "황제국의 심장 아우리아", "황제국 빛의 대성당", "황제국 황금 돔",
                    "황제국 대리석 전당", "황제국 보석 정원", "황제국 태양 광장",
                    "황제국 학자의 탑", "황제국 황실 정원", "황제국 수정 궁전",
                    "황제국 은빛 분수대"
                };
                string name = empireNames[rng.Next(empireNames.Length)];
                
                var lord = new LordInfo
                {
                    lordName = "아우구스투스 황제",
                    preferredFood = new[] { "구운 고기", "생선 스튜", "버섯 수프", "야채 샐러드", "빵과 치즈", "포도주", "꿀 케이크", "로스트 치킨", "양고기 스테이크", "해산물 파이" }[rng.Next(10)],
                    chronicDisease = "위 질환, 통풍, 고혈압",
                    loyalty = 95 + rng.Next(6), // 95~100
                    personality = (LordPersonality)rng.Next(7)
                };

                string description = "모든 영지의 중심, 황제가 다스리는 최후의 요새. 50명의 정예 친위대가 수호한다.";

                AddDefinition(NationType.Empire, 1, name, NationType.Empire,
                    TerritoryDifficulty.Empire, 50, lord, description);
            }

            // 드라큘라 영지 (Night Dracula, 인덱스 1, Ring4 난이도, 병사 10명)
            {
                var draculaLord = new LordInfo
                {
                    lordName = "드라큘라 백작",
                    preferredFood = "붉은 포도주",
                    chronicDisease = "",
                    loyalty = 0,
                    personality = LordPersonality.Cruel
                };

                string draculaDescription = "밤에만 출현하는 저주받은 성. 드라큘라 백작과 그의 스켈레톤 군대가 지키고 있다.";

                AddDefinition(NationType.Dracula, 1, "드라큘라의 성", NationType.Dracula,
                    TerritoryDifficulty.Ring4, 10, draculaLord, draculaDescription, isNightOnly: true);
            }
        }

        /// <summary>
        /// 국가 & 난이도에 따른 병사 수 반환 (ROADMAP 3.1 표)
        /// </summary>
        private static int GetGuardCount(NationType nation, TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => nation switch
                {
                    NationType.East => 3,
                    NationType.West => 4,
                    NationType.South => 4,
                    NationType.North => 5,
                    _ => 3
                },
                TerritoryDifficulty.Ring2 => nation switch
                {
                    NationType.East => 5,
                    NationType.West => 7,
                    NationType.South => 8,
                    NationType.North => 10,
                    _ => 5
                },
                TerritoryDifficulty.Ring3 => nation switch
                {
                    NationType.East => 8,
                    NationType.West => 12,
                    NationType.South => 15,
                    NationType.North => 18,
                    _ => 8
                },
                TerritoryDifficulty.Ring4 => nation switch
                {
                    NationType.East => 12,
                    NationType.West => 18,
                    NationType.South => 25,
                    NationType.North => 35,
                    _ => 12
                },
                TerritoryDifficulty.Empire => 50,
                _ => 3
            };
        }

        /// <summary>
        /// 영지 이름 생성 (Seed 기반 결정론적)
        /// </summary>
        private static string GenerateTerritoryName(NationType nation, int index)
        {
            string prefix = nation switch
            {
                NationType.East => "동쪽",
                NationType.West => "서부",
                NationType.South => "남부",
                NationType.North => "북부",
                _ => "영지"
            };

            // 지역명 풀 (Ring별 5개씩, 총 20개)
            string[] locationNames = nation switch
            {
                NationType.East => new[] {
                    "평화로운 초원", "맑은 시내", "푸른 언덕", "꽃피는 들판", "별빛 숲",
                    "산들바람 계곡", "무지개 언덕", "노래하는 개울", "햇살 가득한 곳", "푸른 호수",
                    "숲속 공터", "은하수 언덕", "이슬 맺힌 숲", "해바라기 밭", "평온한 마을",
                    "비단결 강", "아침 안개 마을", "신비로운 숲", "황금 들판", "영원한 봄의 정원"
                },
                NationType.West => new[] {
                    "황무지", "바위 계곡", "늪지대", "모래 언덕", "고원",
                    "척박한 땅", "메마른 강", "가시 덤불", "돌무더기", "먼지 바람",
                    "깊은 협곡", "황량한 언덕", "마른 나무 숲", "바위투성이", "갈라진 땅",
                    "선인장 평원", "뾰족 바위", "석양 언덕", "그늘 계곡", "침묵의 사막"
                },
                NationType.South => new[] {
                    "불꽃 평야", "화산 기슭", "용암 계곡", "붉은 모래", "재투성이",
                    "뜨거운 온천", "증기 분수", "용암 분출구", "용의 아귀", "불타는 숲",
                    "검은 재", "화염 동굴", "마그마 호수", "유황 냄새", "불기둥",
                    "시뻘건 돌", "화산재 언덕", "굽이치는 용암", "불의 정원", "작열하는 땅"
                },
                NationType.North => new[] {
                    "설원", "얼음 계곡", "눈보라 고개", "빙하 호수", "툰드라",
                    "얼음 동굴", "눈 덮인 숲", "빙벽", "얼음 왕관", "눈사태 언덕",
                    "싸락눈 평원", "흰 눈길", "얼음 조각", "칼바람 언덕", "설송 숲",
                    "얼음 꽃", "북극성 언덕", "얼음 탑", "눈사람 마을", "빙하의 숨결"
                },
                _ => new[] { "초원", "언덕", "계곡", "평야", "숲" }
            };

            // index는 1~20, 배열은 0~19
            string location = locationNames[(index - 1) % locationNames.Length];
            return $"{prefix} {location}";
        }

        /// <summary>
        /// 영주 정보 생성 (Seed 기반 결정론적)
        /// </summary>
        private static LordInfo GenerateLordInfo(NationType nation, TerritoryDifficulty difficulty, int index)
        {
            int seed = ($"{nation}_{index}").GetHashCode();
            var rng = new System.Random(seed);

            // 영주 이름 (Core 내장 배열 — NamePools 의존 없음)
            string[] namePool = nation switch
            {
                NationType.East => _lordEastNames,
                NationType.West => _lordWestNames,
                NationType.South => _lordSouthNames,
                NationType.North => _lordNorthNames,
                _ => _lordEastNames
            };
            string lordName = namePool[(index - 1) % namePool.Length];

            // 선호 음식 (10종 중 랜덤)
            string[] foods = { "구운 고기", "생선 스튜", "버섯 수프", "야채 샐러드", "빵과 치즈", "포도주", "꿀 케이크", "로스트 치킨", "양고기 스테이크", "해산물 파이" };
            string preferredFood = foods[rng.Next(foods.Length)];

            // 지병 (Ring별)
            string chronicDisease = difficulty switch
            {
                TerritoryDifficulty.Ring1 => "",
                TerritoryDifficulty.Ring2 => rng.Next(4) == 0 ? new[] { "통풍", "관절염", "두통" }[rng.Next(3)] : "",
                TerritoryDifficulty.Ring3 => new[] { "편두통", "천식", "소화불량", "불면증" }[rng.Next(4)],
                TerritoryDifficulty.Ring4 => rng.Next(2) == 0
                    ? new[] { "심장병", "당뇨", "간질환", "폐질환", "신장병" }[rng.Next(5)]
                    : $"{new[] { "심장병", "당뇨", "간질환", "폐질환", "신장병" }[rng.Next(5)]}, {new[] { "심장병", "당뇨", "간질환", "폐질환", "신장병" }[rng.Next(5)]}",
                _ => ""
            };

            // 충성심 (Ring별)
            int loyalty = difficulty switch
            {
                TerritoryDifficulty.Ring1 => 70 + rng.Next(11),  // 70~80
                TerritoryDifficulty.Ring2 => 75 + rng.Next(11),  // 75~85
                TerritoryDifficulty.Ring3 => 80 + rng.Next(11),  // 80~90
                TerritoryDifficulty.Ring4 => 85 + rng.Next(11),  // 85~95
                _ => 70
            };

            // 성격 (랜덤)
            LordPersonality personality = (LordPersonality)rng.Next(7);

            return new LordInfo
            {
                lordName = $"{lordName} 경",
                preferredFood = preferredFood,
                chronicDisease = chronicDisease,
                loyalty = loyalty,
                personality = personality
            };
        }

        /// <summary>
        /// 영지 설명 생성
        /// </summary>
        private static string GenerateDescription(NationType nation, TerritoryDifficulty difficulty, int index)
        {
            string nationName = nation switch
            {
                NationType.East => "동쪽",
                NationType.West => "서부",
                NationType.South => "남부",
                NationType.North => "북부",
                _ => "미지의"
            };

            string ringDesc = difficulty switch
            {
                TerritoryDifficulty.Ring1 => "국경 근처의 조용한",
                TerritoryDifficulty.Ring2 => "내륙의 평화로운",
                TerritoryDifficulty.Ring3 => "중앙부의 격전지",
                TerritoryDifficulty.Ring4 => "황제국 인근의 최전선",
                _ => "모든 영지의 중심"
            };

            return $"{nationName} {ringDesc} 영지. {(int)difficulty + 1}단계 난이도.";
        }

        private void InitializeStates()
        {
            foreach (var kvp in _definitions)
            {
                _states[kvp.Key] = new TerritoryState(kvp.Value.id);
            }
        }

        private void AddDefinition(NationType nation, int index, string name, NationType nationType,
            TerritoryDifficulty difficulty, int guardCount, LordInfo lord, string description, bool isNightOnly = false)
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
                description = description,
                isNightOnly = isNightOnly
            };
            _definitions[id.ToString()] = def;
        }
    }
}