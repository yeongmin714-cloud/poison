using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 방독면 컨트롤러 — G 키로 착용/해제, 프레임 업데이트 처리.
    /// </summary>
    public class GasMaskController : MonoBehaviour
    {
        [Header("Gas Mask")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.G;
        [SerializeField] private GasMaskGrade _currentGrade = GasMaskGrade.Wood;

        private void Start()
        {
            if (!GasMaskSystem.IsInitialized)
                GasMaskSystem.Initialize();
        }

        private void Update()
        {
            // G 키: 착용/해제 토글
            if (Input.GetKeyDown(_toggleKey))
            {
                ToggleGasMask();
            }

            // 방독면 유지시간 업데이트
            GasMaskSystem.Update(Time.deltaTime);
        }

        private void ToggleGasMask()
        {
            if (GasMaskSystem.IsActive)
            {
                GasMaskSystem.Unequip();
                Debug.Log("[GasMaskController] 방독면 해제 (G)");
            }
            else
            {
                GasMaskSystem.Equip(_currentGrade);
                Debug.Log($"[GasMaskController] 방독면 장착: {_currentGrade} (G)");
            }
        }

        // Inspector에서 등급 변경 가능
        public void SetGrade(GasMaskGrade grade)
        {
            _currentGrade = grade;
        }

        /// <summary>
        /// 방독면 상태 문자열 (UI 표시용)
        /// </summary>
        public static string GetStatusText()
        {
            if (!GasMaskSystem.IsActive || GasMaskSystem.EquippedMask == null)
                return "방독면: 미착용";

            var mask = GasMaskSystem.EquippedMask.Value;
            string durDisplay = mask.maxDurability == int.MaxValue ? "∞" : $"{GasMaskSystem.CurrentDurability}/{mask.maxDurability}";
            return $"🎭 {mask.displayName} | ⏱ {GasMaskSystem.RemainingTime:F1}초 | 🛡️ {durDisplay}";
        }
    }
}