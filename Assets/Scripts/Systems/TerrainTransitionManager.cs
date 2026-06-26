using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 국가 간 지형 전환 구역 (Transition Zone) 관리
    /// 두 국가의 Biome 사이를 부드럽게 블렌딩하는 계산 제공
    /// </summary>
    public static class TerrainTransitionManager
    {
        /// <summary>
        /// 전환 구역 폭 (50m)
        /// </summary>
        public const float TRANSITION_ZONE_WIDTH = 50f;

        /// <summary>
        /// 두 Biome 사이의 블렌드 계수 계산
        /// </summary>
        /// <param name="playerPosition">플레이어 현재 위치</param>
        /// <param name="borderPosition">국경선 상의 가장 가까운 위치</param>
        /// <param name="borderDirection">A→B 방향 (정규화된 벡터)</param>
        /// <returns>블렌드 계수 (0.0 = 완전히 A 국가 지형, 1.0 = 완전히 B 국가 지형)</returns>
        public static float CalculateBlend(
            Vector3 playerPosition,
            Vector3 borderPosition,
            Vector3 borderDirection)
        {
            // 플레이어에서 국경선까지의 벡터
            Vector3 fromBorder = playerPosition - borderPosition;

            // 국경선 방향으로의 투영 거리 (양수 = B국 방향, 음수 = A국 방향)
            float signedDistance = Vector3.Dot(fromBorder, borderDirection);

            // 전환 구역 내에서 0~1 블렌드로 매핑
            // -TRANSITION_ZONE_WIDTH/2 ~ +TRANSITION_ZONE_WIDTH/2 범위를 0~1로
            float halfZone = TRANSITION_ZONE_WIDTH * 0.5f;
            float blend = (signedDistance + halfZone) / TRANSITION_ZONE_WIDTH;

            return Mathf.Clamp01(blend);
        }

        /// <summary>
        /// 혼합 Biome 데이터 생성 (높이 + 색상)
        /// </summary>
        /// <param name="x">월드 X 좌표</param>
        /// <param name="z">월드 Z 좌표</param>
        /// <param name="biomeA">A 국가 Biome 정의</param>
        /// <param name="seedA">A 국가 노이즈 시드</param>
        /// <param name="biomeB">B 국가 Biome 정의</param>
        /// <param name="seedB">B 국가 노이즈 시드</param>
        /// <param name="blend">블렌드 계수 (0.0=A, 1.0=B)</param>
        /// <returns>(height, color) — 블렌딩된 높이와 색상</returns>
        public static (float height, Color color) GetBlendedTerrainData(
            float x, float z,
            BiomeDefinition biomeA, int seedA,
            BiomeDefinition biomeB, int seedB,
            float blend)
        {
            // Biome A 높이 계산
            float noiseA = Mathf.PerlinNoise(
                x * biomeA.noiseFrequency + seedA,
                z * biomeA.noiseFrequency + seedA);
            float heightA = noiseA * biomeA.noiseAmplitude;

            // Biome B 높이 계산
            float noiseB = Mathf.PerlinNoise(
                x * biomeB.noiseFrequency + seedB,
                z * biomeB.noiseFrequency + seedB);
            float heightB = noiseB * biomeB.noiseAmplitude;

            // 높이: 선형 보간
            float blendedHeight = Mathf.Lerp(heightA, heightB, blend);

            // 색상: 선형 보간
            Color blendedColor = Color.Lerp(biomeA.surfaceColor, biomeB.surfaceColor, blend);

            return (blendedHeight, blendedColor);
        }
    }
}
