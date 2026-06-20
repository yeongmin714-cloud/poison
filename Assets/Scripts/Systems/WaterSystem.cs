using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 물 시스템 — 호수/늪/강의 물 시각 효과 및 인터랙션 관리
    /// TODO: C22-05 상세 구현
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
            // TODO: C22-05 물 쉐이더/머티리얼 설정, 파도 애니메이션
            Debug.Log($"[WaterSystem] CreateWater: {waterObject.name}, color={waterColor}");
        }

        /// <summary>
        /// 물에 들어갈 때 효과 (속도 감소, 파티클 등)
        /// </summary>
        public static void OnEnterWater(GameObject character)
        {
            // TODO: C22-05
        }

        /// <summary>
        /// 물에서 나올 때 효과
        /// </summary>
        public static void OnExitWater(GameObject character)
        {
            // TODO: C22-05
        }
    }
}