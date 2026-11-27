using System;

namespace ProjectName.Systems
{
    /// <summary>
    /// [156차 §1] 이계 카드 데이터 계약 (고정).
    /// - 카드 종류: 무기 획득 / 무기 강화 / 스킬(자동발동) / 패시브 / 특수(Legendary)
    /// - 희귀도: Common / Rare / Epic / Legendary
    /// - 효과 수치는 제네릭 슬롯(MultA/MultB/Damage/Heal/Flat/Radius/Duration/Interval/Int1)에 담는다.
    ///   실제 적용 훅은 Phase 4/5(WeaponSystem/SkillSystem/PassiveSystem)가 Id 기준으로 해석한다.
    /// - 런 한정 소유 — 귀환/퍼머데스 시 소멸(기존 증강과 동일 계약).
    /// </summary>
    public enum CardKind { Weapon, WeaponUpgrade, Skill, Passive, Special }
    public enum CardRarity { Common, Rare, Epic, Legendary }

    public sealed class CardDef
    {
        // ===== 계약 고정 필드 =====
        public string Id;
        public string Name;
        public string Desc;
        public CardKind Kind;
        public CardRarity Rarity;
        public int MaxStacks;

        // ===== 효과 슬롯 (Id별 의미는 CardCatalog 주석 참조) =====
        public float MultA;        // 주 배율 (쿨다운/데미지/이속 등 — Id 기준 해석)
        public float MultB;        // 부 배율 (무기: 서브 스탯 등)
        public float Damage;       // 플랫 피해량
        public float Heal;         // 플랫 회복량
        public float Flat;         // 기타 플랫 값 (HP 보너스/초 단위 보정 등)
        public float Radius;       // 반경 (m)
        public float Duration;     // 지속/쿨다운 시간 (s)
        public float Interval;     // 간격 (s) — 틱 주기/웨이브 간격/자동발사 주기
        public int Int1;           // 정수 슬롯 (화살 수/확률%/지속초/연쇄 조건 등)

        // ===== 무기 슬롯/레벨 (강화 카드 전용 — 획득 무기는 WeaponId=Id, Level=1) =====
        public string WeaponId;    // 강화 대상 기본 무기 Id
        public int Level;          // 기본 무기 1 / 강화 2·3
    }
}
