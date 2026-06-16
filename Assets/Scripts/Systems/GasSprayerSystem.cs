using System;
using System.Collections.Generic;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C8-31: 가스 분사기 등급
    /// </summary>
    public enum GasSprayerGrade
    {
        Wood = 0,
        Stone = 1,
        Iron = 2,
        Reinforced = 3,
        SpecialAlloy = 4
    }

    /// <summary>
    /// C8-31: 가스 분사기 데이터 정의
    /// </summary>
    [System.Serializable]
    public struct GasSprayerData
    {
        public GasSprayerGrade grade;
        public string sprayerName;              // e.g. "나무 가스 분사기"
        public float maxSprayTime;              // seconds (0 = unlimited for SpecialAlloy)
        public bool isUnlimited;                // true for SpecialAlloy grade
        public float sprayRange;                // 분사 범위 반경
        public string[] requiredMaterials;      // 재료 ID 리스트
        public int[] requiredMaterialCounts;    // 재료 수량
        public string equippedSlotName;         // "Back"
        public float sprayTimeMultiplier;       // 물약 속성별 분사 지속 시간 감소율
    }

    /// <summary>
    /// C8-31: 가스 분사기 데이터 시스템
    /// </summary>
    public static class GasSprayerManager
    {
        public static readonly string BACK_SLOT_NAME = "Back";

        // ===== Static grade definitions =====

        public static readonly GasSprayerData WoodSprayer = new GasSprayerData
        {
            grade = GasSprayerGrade.Wood,
            sprayerName = "나무 가스 분사기",
            maxSprayTime = 10f,
            isUnlimited = false,
            sprayRange = 3f,
            requiredMaterials = new[] { "나무", "가죽" },
            requiredMaterialCounts = new[] { 3, 2 },
            equippedSlotName = "Back",
            sprayTimeMultiplier = 1.0f
        };

        public static readonly GasSprayerData StoneSprayer = new GasSprayerData
        {
            grade = GasSprayerGrade.Stone,
            sprayerName = "돌 가스 분사기",
            maxSprayTime = 25f,
            isUnlimited = false,
            sprayRange = 4f,
            requiredMaterials = new[] { "돌", "철" },
            requiredMaterialCounts = new[] { 5, 2 },
            equippedSlotName = "Back",
            sprayTimeMultiplier = 0.8f
        };

        public static readonly GasSprayerData IronSprayer = new GasSprayerData
        {
            grade = GasSprayerGrade.Iron,
            sprayerName = "철 가스 분사기",
            maxSprayTime = 45f,
            isUnlimited = false,
            sprayRange = 5f,
            requiredMaterials = new[] { "철", "강화가죽" },
            requiredMaterialCounts = new[] { 5, 3 },
            equippedSlotName = "Back",
            sprayTimeMultiplier = 0.6f
        };

        public static readonly GasSprayerData ReinforcedSprayer = new GasSprayerData
        {
            grade = GasSprayerGrade.Reinforced,
            sprayerName = "강화 가스 분사기",
            maxSprayTime = 90f,
            isUnlimited = false,
            sprayRange = 7f,
            requiredMaterials = new[] { "철", "희귀보석" },
            requiredMaterialCounts = new[] { 10, 2 },
            equippedSlotName = "Back",
            sprayTimeMultiplier = 0.4f
        };

        public static readonly GasSprayerData SpecialAlloySprayer = new GasSprayerData
        {
            grade = GasSprayerGrade.SpecialAlloy,
            sprayerName = "특수합금 분사기",
            maxSprayTime = 0f,
            isUnlimited = true,
            sprayRange = 10f,
            requiredMaterials = new[] { "철", "용비늘" },
            requiredMaterialCounts = new[] { 20, 1 },
            equippedSlotName = "Back",
            sprayTimeMultiplier = 0f
        };

        /// <summary>
        /// 등급에 해당하는 GasSprayerData 반환
        /// </summary>
        public static GasSprayerData GetGradeData(GasSprayerGrade grade)
        {
            return grade switch
            {
                GasSprayerGrade.Wood => WoodSprayer,
                GasSprayerGrade.Stone => StoneSprayer,
                GasSprayerGrade.Iron => IronSprayer,
                GasSprayerGrade.Reinforced => ReinforcedSprayer,
                GasSprayerGrade.SpecialAlloy => SpecialAlloySprayer,
                _ => throw new ArgumentException($"Unknown GasSprayerGrade: {grade}")
            };
        }

        /// <summary>
        /// 분사기 이름으로 등급 찾기
        /// </summary>
        public static GasSprayerGrade GetGradeBySprayerName(string name)
        {
            foreach (var grade in GetAllGrades())
            {
                var data = GetGradeData(grade);
                if (data.sprayerName == name)
                    return grade;
            }
            throw new ArgumentException($"No sprayer found with name: {name}");
        }

        /// <summary>
        /// 모든 분사기 이름 배열 반환
        /// </summary>
        public static string[] GetAllSprayerNames()
        {
            var grades = GetAllGrades();
            var names = new string[grades.Length];
            for (int i = 0; i < grades.Length; i++)
            {
                names[i] = GetGradeData(grades[i]).sprayerName;
            }
            return names;
        }

        /// <summary>
        /// 모든 등급 배열 반환
        /// </summary>
        public static GasSprayerGrade[] GetAllGrades()
        {
            return new[]
            {
                GasSprayerGrade.Wood,
                GasSprayerGrade.Stone,
                GasSprayerGrade.Iron,
                GasSprayerGrade.Reinforced,
                GasSprayerGrade.SpecialAlloy
            };
        }

        /// <summary>
        /// 인벤토리에 제작 재료가 충분한지 확인 (PlayerInventory 기반)
        /// </summary>
        public static bool CanCraftSprayer(GasSprayerGrade grade, PlayerInventory inventory)
        {
            if (inventory == null) return false;
            var data = GetGradeData(grade);
            return CheckMaterials(data, id => inventory.GetItemCount(id));
        }

        /// <summary>
        /// 인벤토리에 제작 재료가 충분한지 확인 (Func 기반, 테스트 가능)
        /// </summary>
        public static bool CanCraftSprayer(GasSprayerGrade grade, Func<string, int> itemCountGetter)
        {
            if (itemCountGetter == null) return false;
            var data = GetGradeData(grade);
            return CheckMaterials(data, itemCountGetter);
        }

        private static bool CheckMaterials(GasSprayerData data, Func<string, int> countGetter)
        {
            for (int i = 0; i < data.requiredMaterials.Length; i++)
            {
                int count = countGetter(data.requiredMaterials[i]);
                if (count < data.requiredMaterialCounts[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 모든 물약을 분사기에 삽입 가능 (true)
        /// </summary>
        public static bool CanInsertPotion(GasSprayerGrade grade, string potionItemId)
        {
            return true;
        }

        /// <summary>
        /// Back 슬롯은 항상 사용 가능
        /// </summary>
        public static bool IsBackSlotAvailable()
        {
            return true;
        }

        /// <summary>
        /// 분사 지속 시간 계산 (물약 수량 × 분사기 기본 분사 시간 / 초당 소모율)
        /// Unlimited 등급은 float.MaxValue 반환
        /// </summary>
        public static float CalculateSprayDuration(GasSprayerGrade grade, int potionCount)
        {
            var data = GetGradeData(grade);
            if (data.isUnlimited)
                return float.MaxValue;

            if (data.sprayTimeMultiplier <= 0f)
                return float.MaxValue;

            return potionCount * data.maxSprayTime / data.sprayTimeMultiplier;
        }

        /// <summary>
        /// 분사 완료 후 재장전 시간 반환
        /// </summary>
        public static float GetReloadTime(GasSprayerGrade grade)
        {
            return grade switch
            {
                GasSprayerGrade.Wood => 3.0f,
                GasSprayerGrade.Stone => 2.5f,
                GasSprayerGrade.Iron => 2.0f,
                GasSprayerGrade.Reinforced => 1.5f,
                GasSprayerGrade.SpecialAlloy => 0f,
                _ => 3.0f
            };
        }
    }
}