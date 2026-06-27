using ProjectName.Core;
using System;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// Phase 30: 이름 풀 — 영주/용병/국가명.
    /// </summary>
    public static class NamePools
    {
        // ===== 국가별 영주 이름 =====
        public static readonly string[] LordEast = {
            "세드릭", "알드릭", "레오나드", "마커스", "펜드릭",
            "드레이크", "알릭", "에드릭", "하롤드", "랜슬롯",
            "고드윈", "테오도어", "에드먼드", "로드릭", "가윈",
            "오스왈드", "레지날드", "프레드릭", "밀러드", "오스릭"
        };

        public static readonly string[] LordWest = {
            "가레트", "던컨", "루시우스", "발터", "고든",
            "레이너드", "마그누스", "알딘", "콘래드", "필립",
            "시어도어", "윌리엄", "아서", "라파엘", "길버트",
            "호레이스", "제라드", "랜돌프", "실레스", "티모시"
        };

        public static readonly string[] LordSouth = {
            "발카르", "렉산더", "크라토스", "유스티안", "티베리우스",
            "막시무스", "마리우스", "센트리우스", "아티우스", "게르마니쿠스",
            "세베루스", "아우렐리우스", "에어리쿠스", "타키투스", "베스파시아누스",
            "트라야누스", "옥타비우스", "플라비우스", "메텔루스", "코르넬리우스"
        };

        public static readonly string[] LordNorth = {
            "언윈", "울릭", "프리스트", "브라엄", "라그나르",
            "스벤", "비요른", "에이리크", "군나르", "이바르",
            "라우리츠", "크누트", "하랄드", "올라프", "시구르드",
            "엘리프", "헤르만", "볼프강", "디트리히", "알브레히트"
        };

        public static readonly string[] LordEmpire = {
            "아우구스투스", "발렌티누스", "콘스탄티누스", "옥타비안", "마르쿠스",
            "율리우스", "클라우디우스", "파비우스", "리키니우스", "아나스타시우스",
            "유스티누스", "헤라클리우스", "니케포루스", "아르카디우스", "테오도시우스",
            "레오니다스", "필립포스", "카시우스", "루피누스", "마우리키우스"
        };

        // ===== 드라큘라 영주 이름 =====
        public static readonly string[] LordDracula = {
            "블라드", "노스페라투", "드라코", "라두", "바실리",
            "알루카드", "카미라", "바토리", "오를로크", "레드아이",
            "블러드본", "나이트메어", "섀도우윙", "다크하트", "세라",
            "미스트발", "팡고른", "블러드레인", "다크문", "뱀파이어로드"
        };

        // ===== 용병 이름 =====
        public static readonly string[] MercenaryFirst = {
            "루카스", "이사벨라", "로완", "세라핀", "도리스",
            "에이든", "레이라", "마크시무스", "미라벨", "콜린",
            "벨린다", "오르소", "실비아", "라자루스", "니콜",
            "프레야", "캐스퍼", "에스메랄다", "티볼트", "모르가나"
        };

        public static readonly string[] MercenaryLast = {
            "드래곤본", "블레이드", "위스퍼", "아이언하트", "스톰베인",
            "셰이드워커", "블랙우드", "팔콘아이", "사일런트", "골드머인",
            "나이트폴", "블러드윙", "선더러스", "레드킨", "크리스탈하트",
            "미스트케슬", "파이어브랜드", "아이스베일", "스카이워커", "문로드"
        };

        // ===== 국가명 =====
        public const string KingdomNameEast = "동방 비르텐시아 왕국";
        public const string KingdomNameWest = "서부 아르델리아 대공국";
        public const string KingdomNameSouth = "남부 이그니스 제국";
        public const string KingdomNameNorth = "북부 프로스트가드 왕국";
        public const string EmpireName = "중앙 아우레우스 제국";
        public const string DraculaKingdomName = "드라큘라의 영지";

        /// <summary>국가 타입에 따른 국가명 반환</summary>
        /// <param name="nation">국가 타입 (NationType)</param>
        /// <returns>해당 국가의 표시 이름 문자열</returns>
        public static string GetKingdomName(NationType nation)
        {
            return nation switch
            {
                NationType.East => KingdomNameEast,
                NationType.West => KingdomNameWest,
                NationType.South => KingdomNameSouth,
                NationType.North => KingdomNameNorth,
                NationType.Empire => EmpireName,
                NationType.Dracula => DraculaKingdomName,
                _ => "알 수 없는 국가"
            };
        }

        /// <summary>국가별 영주 이름 풀 반환</summary>
        /// <param name="nation">국가 타입 (NationType)</param>
        /// <returns>해당 국가의 영주 이름 문자열 배열</returns>
        public static string[] GetLordNames(NationType nation)
        {
            return nation switch
            {
                NationType.East => LordEast,
                NationType.West => LordWest,
                NationType.South => LordSouth,
                NationType.North => LordNorth,
                NationType.Empire => LordEmpire,
                NationType.Dracula => LordDracula,
                _ => LordEast
            };
        }

        /// <summary>랜덤 영주 이름 생성</summary>
        /// <param name="nation">국가 타입 (NationType)</param>
        /// <param name="rng">난수 생성기 (null 시 새 인스턴스 생성)</param>
        /// <returns>랜덤하게 선택된 영주 이름 문자열</returns>
        public static string GetRandomLordName(NationType nation, System.Random rng = null)
        {
            rng ??= new System.Random();
            var pool = GetLordNames(nation);
            return pool[rng.Next(pool.Length)];
        }

        /// <summary>랜덤 용병 이름 생성 (이름 + 성)</summary>
        /// <param name="rng">난수 생성기 (null 시 새 인스턴스 생성)</param>
        /// <returns>"이름 성" 형식의 랜덤 용병 이름 문자열</returns>
        public static string GetRandomMercenaryName(System.Random rng = null)
        {
            rng ??= new System.Random();
            string first = MercenaryFirst[rng.Next(MercenaryFirst.Length)];
            string last = MercenaryLast[rng.Next(MercenaryLast.Length)];
            return $"{first} {last}";
        }
    }
}