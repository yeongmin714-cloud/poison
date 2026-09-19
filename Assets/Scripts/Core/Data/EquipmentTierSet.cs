using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// Phase O1 (C-O1-01): 장비 티어 세트 데이터 (정적).
    ///
    /// OpenMMO ITEM_TIERS.md 벤치마크 — 포이즌 4티어 체계(wood/steel/stone/crystal)로 매핑:
    ///   Leather(가죽세트) = wood 전용      — Common(Uncommon보다 아래), 시작~초반 파밍 목표
    ///   Chain(사슬세트)   = steel 전용     — Uncommon, 중반 파밍 목표
    ///   Plate(판금세트)   = stone+crystal — Rare~Epic, 후반 파밍 목표
    ///
    /// 파밍 보상 원칙: 같은 부위에서 상위 세트 부위가 하위 세트보다 높은 등급(rarity)이어야 한다.
    ///   helmet/armor/boot: wood(Common) < steel(Uncommon) < stone(Rare)
    ///   glove:             wood(Common) < steel(Uncommon) < crystal(Epic)
    ///
    /// 세트 보너스(정수 포인트 — GuardEquipmentSystem 공/방 보너스 단위와 동일 척도):
    ///   가죽: 2세트 공+1 / 4세트 공+1 방+1
    ///   사슬: 2세트 방+2 / 4세트 공+2 방+2
    ///   판금: 2세트 방+3 / 4세트 공+3 방+3
    ///
    /// Core 계층(static 데이터)이므로 Systems/UI를 참조하지 않는다 (CS0234 회피).
    /// </summary>
    public enum EquipmentSetKind
    {
        Leather = 0, // 가죽세트
        Chain   = 1, // 사슬세트
        Plate   = 2, // 판금세트
    }

    public struct SetBonus
    {
        public int attackBonus;
        public int defenseBonus;
        public string description;

        public static SetBonus Zero => new SetBonus { attackBonus = 0, defenseBonus = 0, description = "" };
    }

    public static class EquipmentTierSet
    {
        // ── 세트 부위 정의 (투구/갑옷/신발/장갑 각 1개 — PlayerInventory 4티어 레지스트리 참조) ──

        /// <summary>가죽세트: wood 전용 (입문 파밍 목표)</summary>
        private static readonly PlayerInventory.ItemData[] LeatherParts =
        {
            PlayerInventory.HelmetWood,
            PlayerInventory.ArmorWood,
            PlayerInventory.BootWood,
            PlayerInventory.GloveWood,
        };

        /// <summary>사슬세트: steel 전용 (중반 파밍 목표)</summary>
        private static readonly PlayerInventory.ItemData[] ChainParts =
        {
            PlayerInventory.HelmetSteel,
            PlayerInventory.ArmorSteel,
            PlayerInventory.BootSteel,
            PlayerInventory.GloveSteel,
        };

        /// <summary>판금세트: stone+crystal (후반 파밍 목표 — 세트가 인접 티어에 걸친다: OpenMMO 원칙)</summary>
        private static readonly PlayerInventory.ItemData[] PlateParts =
        {
            PlayerInventory.HelmetStone,
            PlayerInventory.ArmorStone,
            PlayerInventory.BootStone,
            PlayerInventory.GloveCrystal,
        };

        // ── 세트 보너스 상수 ──
        private static readonly SetBonus LeatherBonus2 = new SetBonus { attackBonus = 1, defenseBonus = 0, description = "가죽세트(2) 공격력 +1" };
        private static readonly SetBonus LeatherBonus4 = new SetBonus { attackBonus = 1, defenseBonus = 1, description = "가죽세트 완성 공격력 +1 방어력 +1" };
        private static readonly SetBonus ChainBonus2   = new SetBonus { attackBonus = 0, defenseBonus = 2, description = "사슬세트(2) 방어력 +2" };
        private static readonly SetBonus ChainBonus4   = new SetBonus { attackBonus = 2, defenseBonus = 2, description = "사슬세트 완성 공격력 +2 방어력 +2" };
        private static readonly SetBonus PlateBonus2   = new SetBonus { attackBonus = 0, defenseBonus = 3, description = "판금세트(2) 방어력 +3" };
        private static readonly SetBonus PlateBonus4   = new SetBonus { attackBonus = 3, defenseBonus = 3, description = "판금세트 완성 공격력 +3 방어력 +3" };

        private static readonly EquipmentSetKind[] AllKinds =
        {
            EquipmentSetKind.Leather,
            EquipmentSetKind.Chain,
            EquipmentSetKind.Plate,
        };

        /// <summary>세트에 속한 부위 4개 (투구/갑옷/신발/장갑)를 반환한다.</summary>
        public static PlayerInventory.ItemData[] GetSetParts(EquipmentSetKind kind)
        {
            switch (kind)
            {
                case EquipmentSetKind.Leather: return LeatherParts;
                case EquipmentSetKind.Chain:   return ChainParts;
                case EquipmentSetKind.Plate:   return PlateParts;
                default: return new PlayerInventory.ItemData[0];
            }
        }

        /// <summary>아이템이 어느 세트에 소속되어 있는가? (미소속이면 null)</summary>
        public static EquipmentSetKind? GetSetForItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            foreach (var kind in AllKinds)
            {
                foreach (var part in GetSetParts(kind))
                {
                    if (part != null && part.id == itemId) return kind;
                }
            }
            return null;
        }

        /// <summary>장착 목록 중 해당 세트 파츠의 개수 (id 비교).</summary>
        public static int CountSetPieces(EquipmentSetKind kind, List<PlayerInventory.ItemData> equippedItems)
        {
            if (equippedItems == null || equippedItems.Count == 0) return 0;

            int count = 0;
            foreach (var part in GetSetParts(kind))
            {
                if (part == null) continue;
                foreach (var equipped in equippedItems)
                {
                    if (equipped != null && equipped.id == part.id)
                    {
                        count++;
                        break; // 같은 파츠 중복 장착은 1개로만 카운트
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 세트 보너스 계산. 2개 이상 = 소효과, 4개(완성) = 대효과, 1개 이하 = Zero.
        /// 3개는 소효과를 유지한다 (완성 특별 보상은 4개에서만).
        /// </summary>
        public static SetBonus GetActiveSetBonus(EquipmentSetKind kind, int piecesCount)
        {
            switch (kind)
            {
                case EquipmentSetKind.Leather:
                    return piecesCount >= 4 ? LeatherBonus4 : (piecesCount >= 2 ? LeatherBonus2 : SetBonus.Zero);
                case EquipmentSetKind.Chain:
                    return piecesCount >= 4 ? ChainBonus4 : (piecesCount >= 2 ? ChainBonus2 : SetBonus.Zero);
                case EquipmentSetKind.Plate:
                    return piecesCount >= 4 ? PlateBonus4 : (piecesCount >= 2 ? PlateBonus2 : SetBonus.Zero);
                default:
                    return SetBonus.Zero;
            }
        }

        /// <summary>세트 한글 이름.</summary>
        public static string GetSetDisplayName(EquipmentSetKind kind)
        {
            switch (kind)
            {
                case EquipmentSetKind.Leather: return "가죽세트";
                case EquipmentSetKind.Chain:   return "사슬세트";
                case EquipmentSetKind.Plate:   return "판금세트";
                default: return "알 수 없는 세트";
            }
        }

        /// <summary>툴팁용 세트 요약 문자열 (부위 구성 + 보너스).</summary>
        public static string GetSetSummary(EquipmentSetKind kind)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{GetSetDisplayName(kind)}]");
            sb.AppendLine("구성:");
            foreach (var part in GetSetParts(kind))
            {
                if (part == null) continue;
                sb.AppendLine($"  - {part.displayName}");
            }
            var b2 = GetActiveSetBonus(kind, 2);
            var b4 = GetActiveSetBonus(kind, 4);
            sb.AppendLine($"2세트: {b2.description}");
            sb.AppendLine($"4세트: {b4.description}");
            return sb.ToString();
        }

        /// <summary>전체 세트 종류 (순회용).</summary>
        public static EquipmentSetKind[] GetAllSetKinds() => AllKinds;
    }
}
