using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// 방독면 등급
    /// </summary>
    public enum GasMaskGrade
    {
        Wood,       // 나무 방독면
        Stone,      // 돌 방독면
        Iron,       // 철 방독면
        Reinforced, // 강화 철 방독면
        Special     // 특수 합금 방독면
    }

    /// <summary>
    /// 방독면 데이터 정의
    /// </summary>
    [System.Serializable]
    public struct GasMaskDef
    {
        public GasMaskGrade grade;
        public string displayName;
        public int maxDurability;   // 사용 가능 횟수
        public float duration;      // 초 단위 유지시간
        public string[] requiredMaterials; // 재료 이름 배열 (디버그/표시용)
        public int[] requiredCounts;       // 재료 수량
    }

    /// <summary>
    /// 방독면 장비/해제 및 내구도 관리 시스템
    /// </summary>
    public static class GasMaskSystem
    {
        private static GasMaskDef? _equippedMask = null;
        private static int _currentDurability = 0;
        private static float _remainingTime = 0f;
        private static bool _isActive = false;
        private static bool _initialized = false;

        // Static definitions for all gas mask grades
        public static readonly GasMaskDef WoodMask = new GasMaskDef
        {
            grade = GasMaskGrade.Wood,
            displayName = "나무 방독면",
            maxDurability = 3,
            duration = 10f,
            requiredMaterials = new[] { "나무" },
            requiredCounts = new[] { 5 }
        };

        public static readonly GasMaskDef StoneMask = new GasMaskDef
        {
            grade = GasMaskGrade.Stone,
            displayName = "돌 방독면",
            maxDurability = 8,
            duration = 30f,
            requiredMaterials = new[] { "돌" },
            requiredCounts = new[] { 5 }
        };

        public static readonly GasMaskDef IronMask = new GasMaskDef
        {
            grade = GasMaskGrade.Iron,
            displayName = "철 방독면",
            maxDurability = 15,
            duration = 60f,
            requiredMaterials = new[] { "철" },
            requiredCounts = new[] { 5 }
        };

        public static readonly GasMaskDef ReinforcedMask = new GasMaskDef
        {
            grade = GasMaskGrade.Reinforced,
            displayName = "강화 철 방독면",
            maxDurability = 30,
            duration = 120f,
            requiredMaterials = new[] { "철" },
            requiredCounts = new[] { 10 }
        };

        public static readonly GasMaskDef SpecialMask = new GasMaskDef
        {
            grade = GasMaskGrade.Special,
            displayName = "특수 합금 방독면",
            maxDurability = int.MaxValue, // 무한
            duration = 300f,
            requiredMaterials = new[] { "철", "희귀 재료" },
            requiredCounts = new[] { 20, 1 }
        };

        public static GasMaskDef? EquippedMask => _equippedMask;
        public static int CurrentDurability => _currentDurability;
        public static float RemainingTime => _remainingTime;
        public static bool IsActive => _isActive;
        public static bool IsInitialized => _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _equippedMask = null;
            _currentDurability = 0;
            _remainingTime = 0f;
            _isActive = false;
            _initialized = true;
            Debug.Log("[GasMaskSystem] 초기화 완료");
        }

        /// <summary>
        /// 방독면 장착. 해당 등급의 방독면을 장비하고 타이머 시작.
        /// </summary>
        public static void Equip(GasMaskGrade grade)
        {
            if (!_initialized) Initialize();

            var def = GetDef(grade);
            if (def == null)
            {
                Debug.LogWarning($"[GasMaskSystem] 알 수 없는 방독면 등급: {grade}");
                return;
            }

            _equippedMask = def;
            _currentDurability = def.Value.maxDurability;
            _remainingTime = def.Value.duration;
            _isActive = true;

            Debug.Log($"[GasMaskSystem] 🎭 {def.Value.displayName} 장착 (내구도: {_currentDurability}, 유지시간: {_remainingTime}초)");
        }

        /// <summary>
        /// 방독면 해제
        /// </summary>
        public static void Unequip()
        {
            if (_equippedMask == null) return;

            string name = _equippedMask.Value.displayName;
            _equippedMask = null;
            _currentDurability = 0;
            _remainingTime = 0f;
            _isActive = false;

            Debug.Log($"[GasMaskSystem] {name} 해제");
        }

        /// <summary>
        /// 매 프레임 호출 — 유지시간 감소.
        /// 유지시간이 다하면 자동 해제되고 내구도 1 감소.
        /// </summary>
        public static void Update(float deltaTime)
        {
            if (!_isActive || _equippedMask == null) return;

            _remainingTime -= deltaTime;

            if (_remainingTime <= 0f)
            {
                _currentDurability--;
                var mask = _equippedMask.Value;

                if (_currentDurability <= 0)
                {
                    Debug.Log($"[GasMaskSystem] {mask.displayName} 파괴됨 (내구도 0)");
                    Unequip();
                }
                else
                {
                    _remainingTime = mask.duration; // 재사용
                    Debug.Log($"[GasMaskSystem] {mask.displayName} 재사용 (남은 내구도: {_currentDurability})");
                }
            }
        }

        /// <summary>
        /// 방독면 등급에 따른 정의 반환
        /// </summary>
        public static GasMaskDef? GetDef(GasMaskGrade grade)
        {
            return grade switch
            {
                GasMaskGrade.Wood => WoodMask,
                GasMaskGrade.Stone => StoneMask,
                GasMaskGrade.Iron => IronMask,
                GasMaskGrade.Reinforced => ReinforcedMask,
                GasMaskGrade.Special => SpecialMask,
                _ => null
            };
        }

        /// <summary>
        /// PlayerInventory에 추가할 방독면 ItemData 생성
        /// </summary>
        public static PlayerInventory.ItemData CreateGasMaskItem(GasMaskGrade grade)
        {
            var def = GetDef(grade);
            if (def == null) return null;

            return new PlayerInventory.ItemData
            {
                id = $"gasmask_{(int)grade}",
                displayName = def.Value.displayName,
                description = $"방독면 | 유지시간: {def.Value.duration}초 | 내구도: {def.Value.maxDurability}회",
                category = PlayerInventory.ItemCategory.Material,
                maxStack = 1
            };
        }
    }
}