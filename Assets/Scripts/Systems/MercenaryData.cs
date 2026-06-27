using System;

namespace ProjectName.Systems
{
    /// <summary>
    /// P25-02: 용병 데이터 구조체.
    /// 이름, 등급, 능력치, 비용, 특수능력, 배경 스토리를 포함합니다.
    /// </summary>
    [Serializable]
    public struct MercenaryData
    {
        /// <summary>용병 고유 ID</summary>
        public string id;

        /// <summary>용병 이름</summary>
        public string mercenaryName;

        /// <summary>용병 등급 (1=일반, 2=고급, 3=정예, 4=전설)</summary>
        public MercenaryGrade grade;

        /// <summary>최대 체력</summary>
        public float maxHP;

        /// <summary>공격력</summary>
        public float attack;

        /// <summary>방어력</summary>
        public float defense;

        /// <summary>이동 속도</summary>
        public float moveSpeed;

        /// <summary>고용 비용 (골드)</summary>
        public int hireCost;

        /// <summary>특수 능력 설명</summary>
        public string specialAbility;

        /// <summary>배경 스토리</summary>
        public string backStory;

        /// <summary>직업/타입 (Soldier, Bard 등)</summary>
        public string jobType;

        /// <summary>생성자</summary>
        public MercenaryData(
            string id, string mercenaryName, MercenaryGrade grade,
            float maxHP, float attack, float defense, float moveSpeed,
            int hireCost, string specialAbility, string backStory, string jobType = "Soldier")
        {
            this.id = id;
            this.mercenaryName = mercenaryName;
            this.grade = grade;
            this.maxHP = maxHP;
            this.attack = attack;
            this.defense = defense;
            this.moveSpeed = moveSpeed;
            this.hireCost = hireCost;
            this.specialAbility = specialAbility;
            this.backStory = backStory;
            this.jobType = jobType;
        }

        /// <summary>등급에 따른 별표 표기</summary>
        public string GradeStars
        {
            get
            {
                return grade switch
                {
                    MercenaryGrade.Normal => "★",
                    MercenaryGrade.High => "★★",
                    MercenaryGrade.Elite => "★★★",
                    MercenaryGrade.Legendary => "★★★★",
                    _ => "★"
                };
            }
        }

        /// <summary>일반 병사 대비 능력치 배율</summary>
        public float StatMultiplier
        {
            get
            {
                return grade switch
                {
                    MercenaryGrade.Normal => 1.5f,
                    MercenaryGrade.High => 2.0f,
                    MercenaryGrade.Elite => 2.5f,
                    MercenaryGrade.Legendary => 3.0f,
                    _ => 1.5f
                };
            }
        }
    }

    /// <summary>용병 등급</summary>
    public enum MercenaryGrade
    {
        Normal,    // ★ 일반
        High,      // ★★ 고급
        Elite,     // ★★★ 정예
        Legendary  // ★★★★ 전설
    }
}