using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Core
{
    /// <summary>
    /// Tracks player's drug addiction level and applies drug effects.
    /// </summary>
    public static class DrugEffectSystem
    {
        private static float _drugAddictionLevel = 0f;   // 0~100
        private static bool _initialized = false;

        public static float DrugAddictionLevel => _drugAddictionLevel;
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// Initialize the addiction system.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _drugAddictionLevel = 0f;
            _initialized = true;
            Debug.Log("[DrugEffectSystem] Initialized. Addiction: 0%");
        }


        private static Color GetPoisonColor(DrugInfo info)
        {
            switch (info.addiction)
            {
                case AddictionLevel.Low: return Color.red; // 공격성 (red)
                case AddictionLevel.Medium: return new Color(0.5f, 0f, 0.5f); // 보라색 (purple)
                case AddictionLevel.High: return Color.green; // 초록색 (green)
                case AddictionLevel.VeryHigh: return Color.blue; // 파란색 (blue)
                case AddictionLevel.Extreme: return Color.blue; // extreme also blue
                case AddictionLevel.Fatal: return Color.blue; // fatal also blue
                default: return Color.white;
            }
        }


        /// <summary>
        /// Returns gold generated from the drug.
        /// </summary>
        public static int ApplyDrug(int stage)
        {
            if (!_initialized) Initialize();

            var drugInfo = DrugDatabase.GetByStage(stage);
            if (!drugInfo.HasValue)
            {
                Debug.LogWarning($"[DrugEffectSystem] Unknown drug stage: {stage}");
                return 0;
            }

            var drug = drugInfo.Value;

            // Calculate gold generated
            // Stage 1-3: low profit (5-20 gold)
            // Stage 4-6: medium profit (30-80 gold)
            // Stage 7-8: high profit (100-200 gold)
            // Stage 9-10: extreme profit (300-500 gold)
            int baseGold = stage switch
            {
                1 => 5,
                2 => 10,
                3 => 20,
                4 => 35,
                5 => 50,
                6 => 80,
                7 => 120,
                8 => 180,
                9 => 300,
                10 => 500,
                _ => 0
            };

            // Addiction scales by stage
            // Stage 1-3: +1~2%
            // Stage 4-6: +3~5%
            // Stage 7-8: +8~10%
            // Stage 9-10: +15~20%
            float addictionIncrease = stage switch
            {
                1 => 1f,
                2 => 1.5f,
                3 => 2f,
                4 => 3f,
                5 => 4f,
                6 => 5f,
                7 => 8f,
                8 => 10f,
                9 => 15f,
                10 => 20f,
                _ => 0f
            };

            // Apply addiction increase (blocked by gas mask)
            if (GasMaskSystem.IsActive)
                addictionIncrease = 0f;

            _drugAddictionLevel = Mathf.Clamp(_drugAddictionLevel + addictionIncrease, 0f, 100f);

            // Apply money to player
            int goldGained = Mathf.RoundToInt(baseGold * (1f + _drugAddictionLevel * 0.01f)); // addiction bonus
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.AddGold(goldGained);
            }

            Debug.Log($"[DrugEffectSystem] 💊 {drug.drugName} (Stage {stage}): +{goldGained} gold, " +
                      $"addiction +{addictionIncrease}% (now {_drugAddictionLevel:F1}%)");

            // Spawn poison VFX
            Color poisonColor = GetPoisonColor(drug);
            Vector3 playerPos = PlayerStats.Instance != null ? PlayerStats.Instance.transform.position : Vector3.zero;
            // PoisonVFX.Spawn(poisonColor, playerPos, 5f); // TODO: VFX system needed

            return goldGained;
        }
        /// <summary>
        /// Reduce addiction over time (e.g., 1% per in-game day).
        /// </summary>
        public static void ReduceAddiction(float amount)
        {
            _drugAddictionLevel = Mathf.Max(0f, _drugAddictionLevel - amount);
            Debug.Log($"[DrugEffectSystem] Addiction reduced by {amount}% (now {_drugAddictionLevel:F1}%)");
        }

        /// <summary>
        /// Get the addiction level description for UI display.
        /// </summary>
        public static string GetAddictionLabel()
        {
            return _drugAddictionLevel switch
            {
                <= 0f => "깨끗함",
                <= 20f => "영향 없음",
                <= 40f => "가벼운 의존",
                <= 60f => "중독",
                <= 80f => "심각한 중독",
                <= 100f => "완전 중독",
                _ => "과다복용 위험"
            };
        }

        /// <summary>
        /// Creates a PlayerInventory.ItemData for a drug.
        /// </summary>
        public static PlayerInventory.ItemData CreateDrugItem(int stage)
        {
            var drugInfo = DrugDatabase.GetByStage(stage);
            if (!drugInfo.HasValue) return null;

            var drug = drugInfo.Value;

            // Spawn poison VFX
            Color poisonColor = GetPoisonColor(drug);
            Vector3 playerPos = PlayerStats.Instance != null ? PlayerStats.Instance.transform.position : Vector3.zero;
            // PoisonVFX.Spawn(poisonColor, playerPos, 5f); // TODO: VFX system needed

            return new PlayerInventory.ItemData
            {
                id = $"drug_{stage:D2}",
                displayName = drug.drugName,
                description = $"Stage {stage} | 중독성: {drug.addiction} | {drug.description}",
                category = PlayerInventory.ItemCategory.Drug,
                maxStack = 10
            };
        }
    }
}