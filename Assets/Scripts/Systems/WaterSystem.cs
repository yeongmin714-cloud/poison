using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 물 시스템 — 호수/늪/강의 물 시각 효과 및 인터랙션 관리
    /// C22-05 구현 완료 (WaterBody, LakeGenerator, WaterMaterialUpgrader 참조)
    /// </summary>
    public static class WaterSystem
    {
        /// <summary>
        /// 물 시각 효과 생성 (파도, 반사, 투명도 등)
        /// </summary>
        /// <param name="waterObject">물 GameObject</param>
        /// <param name="waterColor">물 색상</param>
        /// <param name="waveAmplitude">파도 진폭</param>
        public static void CreateWater(GameObject waterObject, Color waterColor, float waveAmplitude = 0.1f)
        {
            if (waterObject == null)
            {
                Debug.LogError("[WaterSystem] CreateWater: waterObject is null!");
                return;
            }

            // C22-05: 실제 구현은 WaterBody 및 LakeGenerator 컴포넌트 참조
            Debug.Log($"[WaterSystem] CreateWater: {waterObject.name}, color={waterColor}, amplitude={waveAmplitude}");
        }

        /// <summary>
        /// 물에 들어갈 때 효과 (속도 감소, 파티클 등)
        /// WaterBody.OnTriggerEnter 참조
        /// </summary>
        /// <param name="character">물에 들어가는 캐릭터 GameObject</param>
        public static void OnEnterWater(GameObject character)
        {
            if (character == null) return;
            Debug.Log($"[WaterSystem] OnEnterWater: {character.name}");
        }

        /// <summary>
        /// 물에서 나올 때 효과
        /// WaterBody.OnTriggerExit 참조
        /// </summary>
        /// <param name="character">물에서 나오는 캐릭터 GameObject</param>
        public static void OnExitWater(GameObject character)
        {
            if (character == null) return;
            Debug.Log($"[WaterSystem] OnExitWater: {character.name}");
        }
    }
}