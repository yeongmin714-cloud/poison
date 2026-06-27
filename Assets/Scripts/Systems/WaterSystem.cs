using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 물 시스템 퍼사드 — 호수/늪/강의 물 시각 효과 및 인터랙션 관리.
    /// C22-05: 런타임 구현은 <see cref="WaterBody"/> 및 <see cref="LakeGenerator"/> 참조.
    /// 이 클래스는 외부 호출을 위한 정적 API 표면 역할을 하며,
    /// WaterBody/LakeGenerator 내부에서 직접 처리할 수도 있음.
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
            if (character == null)
            {
                Debug.LogError("[WaterSystem] OnEnterWater: character is null!");
                return;
            }
            Debug.Log($"[WaterSystem] OnEnterWater: {character.name}");
        }

        /// <summary>
        /// 물에서 나올 때 효과
        /// WaterBody.OnTriggerExit 참조
        /// </summary>
        /// <param name="character">물에서 나오는 캐릭터 GameObject</param>
        public static void OnExitWater(GameObject character)
        {
            if (character == null)
            {
                Debug.LogError("[WaterSystem] OnExitWater: character is null!");
                return;
            }
            Debug.Log($"[WaterSystem] OnExitWater: {character.name}");
        }
    }
}