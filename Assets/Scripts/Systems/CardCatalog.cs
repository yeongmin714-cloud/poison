using System.Collections.Generic;
using System.Linq;

namespace ProjectName.Systems
{
    /// <summary>
    /// [156차 §2][156차 P3] 이계 카드 카탈로그 — 신규 40장 풀 (+ 기존 증강 6종 = UI 표시 총량 46).
    /// - 무기 획득 4 / 무기 강화 8 / 스킬 8 / 패시브 12 + 스택II 4 [156차 P3] / 특수 4
    /// - 모든 수치는 계획서 §2 값 그대로 — Phase 4/5 훅이 Id 기준으로 해석한다.
    /// </summary>
    public static class CardCatalog
    {
        /// <summary>효과 슬롯 규약 주석 (카드별 Id → 슬롯 의미):</summary>
        //
        // 무기 획득:
        //   w_saw_dagger   MultA=쿨다운 배율(0.7)  MultB=데미지 배율(0.8)
        //   w_grave_sword  MultA=데미지 배율(1.1) + 넉백(Phase 4 연결)
        //   w_pierce_spear MultA=데미지 배율(1.25) MultB=쿨다운 배율(1.15)
        //   w_soul_bow     Interval=자동발사 주기(6) MultA=화살 데미지 배율(0.8)
        // 무기 강화 (WeaponId/Level 지정):
        //   단검 Lv2 MultA=쿨다운(0.85) / Lv3 MultA=데미지(2.0, 연속타)
        //   검   Lv2 MultA=데미지(1.3)  / Lv3 MultA=데미지(1.5)
        //   창   Lv2 MultA=데미지(1.4)  / Lv3 Radius=관통 길이(6) Int1=관통률(60)
        //   활   Lv2 Int1=화살 수(2)    / Lv3 Int1=화살 수(3)
        // 스킬 (전부 자동발동 — 키 계약 무변경):
        //   s_spin_slash  Duration=쿨다운(8)  Radius=3.5  Damage=15
        //   s_charge      Duration=쿨다운(12) 최근접 타격 Damage=25
        //   s_shockwave   Duration=쿨다운(15) Radius=6 Damage=20 + 넉백 + 히트스톱 0.03
        //   s_holy_cross  Duration=쿨다운(20) Radius=5 Damage=18 Heal=10
        //   s_fire_zone   Duration=쿨다운(15) Radius=2.5 Damage=6 Interval=틱(0.5) Int1=지속초(3)
        //   s_time_warp   Duration=쿨다운(25) MultA=적 이속 배율(0.4) Int1=지속초(2)
        //   s_tentacle    Duration=쿨다운(12) Radius=경로 반경(1.5) Damage=8
        //   s_bone_shield Duration=쿨다운(10) 1회 완전 방어
        // 패시브:
        //   p_vampire_fang      Heal=처치힐(2)
        //   p_skull_ring        Int1=연쇄 조건(10)  MultA=오브 배율(2)
        //   p_emperor_seal      MultA=데미지 증가(1.10) MaxStacks=5
        //   p_steel_shield      MultA=받는피해 배율(0.92) MaxStacks=3
        //   p_death_scythe      Int1=즉사 확률(5) — 정예 5% 가산
        //   p_tombstone         Flat=최대HP 증가(50) MaxStacks 무제한(int.MaxValue)
        //   p_time_sand         Interval=웨이브 간격(135) — 기본 120 교체
        //   p_soul_jar          MultA=오브 가치 배율(1.2)
        //   p_shadow_boots      MultA=구르기 거리 배율(1.2)
        //   p_perfect_mark      Flat=PERFECT 히트스톱 추가(0.02s)
        //   p_chain_seal        MultA=적 이속 배율(0.9) MaxStacks=3
        //   p_afterlife_talisman Flat=텔레그래프 추가(0.1s)
        // 패시브 스택II ([156차 P3] — 기존 패시브의 +1 추가 스택 II형 · ApplyPassive 복제 case):
        //   p_emperor_seal_plus  MultA=데미지 증가(1.10) MaxStacks=1
        //   p_tombstone_plus     Flat=최대HP 증가(50) MaxStacks=1
        //   p_steel_shield_plus  MultA=받는피해 배율(0.92) MaxStacks=1
        //   p_vampire_fang_plus  Heal=처치힐(2) MaxStacks=1
        // 특수 (전부 Legendary / 1회성):
        //   sp_second_life     MultA=부활 HP 비율(0.5)
        //   sp_pledge_receipt  MultA=탈주 수확 비율(1.0)
        //   sp_wave_pardon     Int1=무시 횟수(1)
        //   sp_realm_gate      MultA=수확 감면 비율(0.5)

        private static readonly CardDef[] _cards =
        {
            // ── 무기 획득 4 ──
            new CardDef { Id="w_saw_dagger",   Name="톱니 단검",   Desc="공격 쿨다운 ×0.7 · 데미지 ×0.8", Kind=CardKind.Weapon, Rarity=CardRarity.Common,    MaxStacks=1, MultA=0.7f,  MultB=0.8f, Level=1 },
            new CardDef { Id="w_grave_sword",  Name="묘지기의 검", Desc="데미지 ×1.1 + 넉백",            Kind=CardKind.Weapon, Rarity=CardRarity.Common,    MaxStacks=1, MultA=1.1f,  Level=1 },
            new CardDef { Id="w_pierce_spear", Name="관통의 창",   Desc="데미지 ×1.25 · 쿨다운 ×1.15",   Kind=CardKind.Weapon, Rarity=CardRarity.Rare,      MaxStacks=1, MultA=1.25f, MultB=1.15f, Level=1 },
            new CardDef { Id="w_soul_bow",     Name="혼백 활",     Desc="6초마다 자동 발사 (데미지 ×0.8)", Kind=CardKind.Weapon, Rarity=CardRarity.Epic,      MaxStacks=1, Interval=6f, MultA=0.8f,  Level=1 },

            // ── 무기 강화 12 (Lv2 / Lv3) ──
            new CardDef { Id="w_saw_dagger_2",   Name="톱니 단검 II",   Desc="쿨다운 ×0.85",                    Kind=CardKind.WeaponUpgrade, Rarity=CardRarity.Common, MaxStacks=1, MultA=0.85f, WeaponId="w_saw_dagger",   Level=2 },
            new CardDef { Id="w_saw_dagger_3",   Name="톱니 단검 III",  Desc="데미지 ×2.0 (연속타)",            Kind=CardKind.WeaponUpgrade, Rarity=CardRarity.Common, MaxStacks=1, MultA=2.0f,  WeaponId="w_saw_dagger",   Level=3 },
            new CardDef { Id="w_grave_sword_2",  Name="묘지기의 검 II",  Desc="데미지 ×1.3",                    Kind=CardKind.WeaponUpgrade, Rarity=CardRarity.Common, MaxStacks=1, MultA=1.3f,  WeaponId="w_grave_sword",  Level=2 },
            new CardDef { Id="w_grave_sword_3",  Name="묘지기의 검 III", Desc="데미지 ×1.5",                    Kind=CardKind.WeaponUpgrade, Rarity=CardRarity.Common, MaxStacks=1, MultA=1.5f,  WeaponId="w_grave_sword",  Level=3 },
            new CardDef { Id="w_pierce_spear_2", Name="관통의 창 II",   Desc="데미지 ×1.4",                    Kind=CardKind.WeaponUpgrade, Rarity=CardRarity.Common, MaxStacks=1, MultA=1.4f,  WeaponId="w_pierce_spear", Level=2 },
            new CardDef { Id="w_pierce_spear_3", Name="관통의 창 III",  Desc="전방 6m 관통 60%",                Kind=CardKind.WeaponUpgrade, Rarity=CardRarity.Common, MaxStacks=1, Radius=6f,  Int1=60, WeaponId="w_pierce_spear", Level=3 },
            new CardDef { Id="w_soul_bow_2",     Name="혼백 활 II",     Desc="자동 발사 2화살",                 Kind=CardKind.WeaponUpgrade, Rarity=CardRarity.Common, MaxStacks=1, Int1=2, WeaponId="w_soul_bow", Level=2 },
            new CardDef { Id="w_soul_bow_3",     Name="혼백 활 III",    Desc="자동 발사 3화살",                 Kind=CardKind.WeaponUpgrade, Rarity=CardRarity.Common, MaxStacks=1, Int1=3, WeaponId="w_soul_bow", Level=3 },

            // ── 스킬 8 (자동발동 — 키 계약 무변경) ──
            new CardDef { Id="s_spin_slash",  Name="회전 베기",     Desc="8초마다 3.5m 원형 15 피해",            Kind=CardKind.Skill, Rarity=CardRarity.Common, MaxStacks=1, Duration=8f,  Radius=3.5f, Damage=15f },
            new CardDef { Id="s_charge",      Name="돌진",          Desc="12초마다 최근접 적에게 25 피해",        Kind=CardKind.Skill, Rarity=CardRarity.Common, MaxStacks=1, Duration=12f, Damage=25f },
            new CardDef { Id="s_shockwave",   Name="충격파",        Desc="15초마다 6m 20 피해 + 넉백 + 히트스톱", Kind=CardKind.Skill, Rarity=CardRarity.Rare,   MaxStacks=1, Duration=15f, Radius=6f, Damage=20f },
            new CardDef { Id="s_holy_cross",  Name="성십자",        Desc="20초마다 5m 18 피해 + HP 10 회복",     Kind=CardKind.Skill, Rarity=CardRarity.Rare,   MaxStacks=1, Duration=20f, Radius=5f, Damage=18f, Heal=10f },
            new CardDef { Id="s_fire_zone",   Name="화염 지대",     Desc="15초마다 3초간 2.5m 6 피해 ×0.5초 틱", Kind=CardKind.Skill, Rarity=CardRarity.Rare,   MaxStacks=1, Duration=15f, Radius=2.5f, Damage=6f, Interval=0.5f, Int1=3 },
            new CardDef { Id="s_time_warp",   Name="시간 왜곡",     Desc="25초마다 적 이속 0.4× (2초)",          Kind=CardKind.Skill, Rarity=CardRarity.Epic,   MaxStacks=1, Duration=25f, MultA=0.4f, Int1=2 },
            new CardDef { Id="s_tentacle",    Name="서리촉수",      Desc="12초마다 최원거리 오브 끌어당김 + 경로 1.5m 8 피해", Kind=CardKind.Skill, Rarity=CardRarity.Epic, MaxStacks=1, Duration=12f, Radius=1.5f, Damage=8f },
            new CardDef { Id="s_bone_shield", Name="골 방패",       Desc="10초마다 1회 완전 방어",               Kind=CardKind.Skill, Rarity=CardRarity.Rare,   MaxStacks=1, Duration=10f },

            // ── 패시브 12 ──
            new CardDef { Id="p_vampire_fang",     Name="뱀파이어 송곳니", Desc="처치 시 HP 2 회복",              Kind=CardKind.Passive, Rarity=CardRarity.Common, MaxStacks=1,             Heal=2f },
            new CardDef { Id="p_skull_ring",       Name="해골 반지",       Desc="10연속 처치 시 다음 오브 ×2",     Kind=CardKind.Passive, Rarity=CardRarity.Common, MaxStacks=1,             Int1=10, MultA=2f },
            new CardDef { Id="p_emperor_seal",     Name="황제 인장",       Desc="데미지 +10% (최대 5스택)",        Kind=CardKind.Passive, Rarity=CardRarity.Common, MaxStacks=5,             MultA=1.10f },
            new CardDef { Id="p_steel_shield",     Name="강철 방패",       Desc="받는 피해 ×0.92 (최대 3스택)",   Kind=CardKind.Passive, Rarity=CardRarity.Common, MaxStacks=3,             MultA=0.92f },
            new CardDef { Id="p_death_scythe",     Name="사신 낫",         Desc="5% 즉사 확률 (정예 +5% 가산)",   Kind=CardKind.Passive, Rarity=CardRarity.Rare,   MaxStacks=1,             Int1=5 },
            new CardDef { Id="p_tombstone",        Name="묘비",            Desc="최대 HP +50 (스택당)",           Kind=CardKind.Passive, Rarity=CardRarity.Common, MaxStacks=int.MaxValue,  Flat=50f },
            new CardDef { Id="p_time_sand",        Name="시간의 모래",     Desc="웨이브 간격 120초 → 135초",      Kind=CardKind.Passive, Rarity=CardRarity.Rare,   MaxStacks=1,             Interval=135f },
            new CardDef { Id="p_soul_jar",         Name="영혼 단지",       Desc="오브 가치 ×1.2",                 Kind=CardKind.Passive, Rarity=CardRarity.Common, MaxStacks=1,             MultA=1.2f },
            new CardDef { Id="p_shadow_boots",     Name="그림자 장화",     Desc="구르기 거리 ×1.2",               Kind=CardKind.Passive, Rarity=CardRarity.Rare,   MaxStacks=1,             MultA=1.2f },
            new CardDef { Id="p_perfect_mark",     Name="완벽의 낙인",     Desc="PERFECT 히트스톱 +0.02초",       Kind=CardKind.Passive, Rarity=CardRarity.Rare,   MaxStacks=1,             Flat=0.02f },
            new CardDef { Id="p_chain_seal",       Name="사슬 인장",       Desc="적 이속 ×0.9 (최대 3스택)",      Kind=CardKind.Passive, Rarity=CardRarity.Common, MaxStacks=3,             MultA=0.9f },
            new CardDef { Id="p_afterlife_talisman", Name="저승 부적",     Desc="텔레그래프 +0.1초",              Kind=CardKind.Passive, Rarity=CardRarity.Rare,   MaxStacks=1,             Flat=0.1f },

            // ── 패시브 스택II 4 ([156차 P3] — 기존 패시브의 +1 스택 II형 · 풀 36→40, UI 표시 총량 46 = 신규 40 + 기존 증강 6) ──
            new CardDef { Id="p_emperor_seal_plus",  Name="황제의 인장 II",     Desc="공격력 +10% 추가 스택",    Kind=CardKind.Passive, Rarity=CardRarity.Rare, MaxStacks=1, MultA=1.10f },
            new CardDef { Id="p_tombstone_plus",     Name="묘비 조각 II",       Desc="최대 HP +50 추가 스택",    Kind=CardKind.Passive, Rarity=CardRarity.Rare, MaxStacks=1, Flat=50f },
            new CardDef { Id="p_steel_shield_plus",  Name="강철 방패 II",       Desc="피해 감소 추가 스택",      Kind=CardKind.Passive, Rarity=CardRarity.Rare, MaxStacks=1, MultA=0.92f },
            new CardDef { Id="p_vampire_fang_plus",  Name="뱀파이어 송곳니 II", Desc="처치 회복 +2 추가",        Kind=CardKind.Passive, Rarity=CardRarity.Rare, MaxStacks=1, Heal=2f },

            // ── 특수 4 (Legendary / 1회성) ──
            new CardDef { Id="sp_second_life",    Name="제2의 생명",   Desc="사망 시 1회 부활 (HP 50%)",   Kind=CardKind.Special, Rarity=CardRarity.Legendary, MaxStacks=1, MultA=0.5f },
            new CardDef { Id="sp_pledge_receipt", Name="서약 영수증",  Desc="탈주 수확 비율 0.5 → 1.0",    Kind=CardKind.Special, Rarity=CardRarity.Legendary, MaxStacks=1, MultA=1.0f },
            new CardDef { Id="sp_wave_pardon",    Name="웨이브 사면",  Desc="다음 웨이브 1회 무시",        Kind=CardKind.Special, Rarity=CardRarity.Legendary, MaxStacks=1, Int1=1 },
            new CardDef { Id="sp_realm_gate",     Name="귀환의 문",    Desc="즉시 귀환 문 개방 + 수확 ×0.5", Kind=CardKind.Special, Rarity=CardRarity.Legendary, MaxStacks=1, MultA=0.5f },
        };

        public static IReadOnlyList<CardDef> Cards => _cards;

        public static CardDef Get(string id) => _cards.FirstOrDefault(c => c.Id == id);
    }
}
