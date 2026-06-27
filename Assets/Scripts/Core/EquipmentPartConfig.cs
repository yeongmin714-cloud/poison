using System.Collections.Generic;

namespace ProjectName.Core
{
    /// <summary>
    /// 장비가 장착될 수 있는 신체 부위를 정의하고,
    /// 레벨별 부위 장착 확률을 관리하는 정적 클래스.
    /// </summary>
    public static class EquipmentPartConfig
    {
        // ===================== 장비 부위 열거형 =====================

        public enum EquipmentPart
        {
            Head,    // 머리
            Body,    // 몸통
            Hands,   // 손
            Feet,    // 발
            Weapon   // 무기
        }

        public static readonly EquipmentPart[] AllParts = new[]
        {
            EquipmentPart.Head,
            EquipmentPart.Body,
            EquipmentPart.Hands,
            EquipmentPart.Feet,
            EquipmentPart.Weapon
        };

        // ===================== 레벨별 부위 장착 확률 =====================

        private static readonly Dictionary<int, float> LevelSlotProbability = new Dictionary<int, float>
        {
            { 10, 0.25f }, // Lv1-10
            { 20, 0.45f }, // Lv11-20
            { 30, 0.65f }, // Lv21-30
            { 40, 0.80f }, // Lv31-40
            { 50, 0.90f }, // Lv41-50
        };

        /// <summary>
        /// 주어진 레벨에 해당하는 부위별 장착 확률을 반환합니다.
        /// </summary>
        public static float GetSlotProbability(int level)
        {
            if (level <= 10) return LevelSlotProbability[10];
            if (level <= 20) return LevelSlotProbability[20];
            if (level <= 30) return LevelSlotProbability[30];
            if (level <= 40) return LevelSlotProbability[40];
            return LevelSlotProbability[50];
        }

        /// <summary>
        /// 주어진 레벨과 부위에 대해 장착 확률을 반환합니다.
        /// (모든 부위가 동일한 확률을 공유합니다.)
        /// </summary>
        public static float GetSlotProbability(int level, EquipmentPart part)
        {
            return GetSlotProbability(level);
        }

        // ===================== 랜덤 슬롯 롤 =====================

        /// <summary>
        /// 주어진 레벨에 대해 랜덤으로 장착된 부위 목록을 반환합니다.
        /// 각 부위는 GetSlotProbability 확률로 독립적으로 장착 여부가 결정됩니다.
        /// 최소 1개는 보장됩니다.
        /// </summary>
        public static List<EquipmentPart> RollSlots(int level)
        {
            float prob = GetSlotProbability(level);
            var result = new List<EquipmentPart>();

            foreach (var part in AllParts)
            {
                if (UnityEngine.Random.value <= prob)
                    result.Add(part);
            }

            // 최소 1개 보장
            if (result.Count == 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, AllParts.Length);
                result.Add(AllParts[randomIndex]);
            }

            return result;
        }

        // ===================== 슬롯 개수 범위 =====================

        /// <summary>
        /// 주어진 레벨에서 장비 슬롯 개수 범위의 최소값을 반환합니다.
        /// Lv1-10: 1, Lv11-20: 2, Lv21-30: 3, Lv31-50: 4
        /// </summary>
        public static int GetSlotCountMin(int level)
        {
            if (level >= 1 && level <= 10)  return 1;
            if (level >= 11 && level <= 20) return 2;
            if (level >= 21 && level <= 30) return 3;
            if (level >= 31 && level <= 50) return 4;
            if (level < 1)  return 1;
            return 4;
        }

        /// <summary>
        /// 주어진 레벨에서 장비 슬롯 개수 범위의 최대값을 반환합니다.
        /// Lv1-10: 2, Lv11-20: 3, Lv21-30: 4, Lv31-50: 5
        /// </summary>
        public static int GetSlotCountMax(int level)
        {
            if (level >= 1 && level <= 10)  return 2;
            if (level >= 11 && level <= 20) return 3;
            if (level >= 21 && level <= 30) return 4;
            if (level >= 31 && level <= 50) return 5;
            if (level < 1)  return 2;
            return 5;
        }

        // ===================== 부위 표시명 =====================

        public static string GetPartDisplayName(EquipmentPart part)
        {
            switch (part)
            {
                case EquipmentPart.Head: return "투구";
                case EquipmentPart.Body: return "갑옷";
                case EquipmentPart.Hands: return "장갑";
                case EquipmentPart.Feet: return "부츠";
                case EquipmentPart.Weapon: return "무기";
                default: return "기타";
            }
        }
    }
}