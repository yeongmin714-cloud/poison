using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// 방독면 프레임 업데이트(내구도·유지시간 감소) — 장착/해제는 인벤토리 Mask 슬롯이 담당(→ GasMaskEquipmentLink).
    /// 수동 키 토글 없음(V키 토글 제거) — 방독면은 인벤에서만 장착.
    /// </summary>
    public class GasMaskController : MonoBehaviour
    {
        private void Start()
        {
            if (!GasMaskSystem.IsInitialized)
                GasMaskSystem.Initialize();
        }

        private void Update()
        {
            // 장착 상태와 무관하게 내구도/유지시간 시스템을 프레임마다 갱신
            GasMaskSystem.Update(Time.deltaTime);
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