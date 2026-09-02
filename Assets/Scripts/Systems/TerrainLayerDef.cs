using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 지형 멀티레어 스플랫을 위한 단일 레이어 정의.
    /// 각 국가에 3~4개의 레이어를 배정하고, Baker가 높이/경사/노이즈 가중치로 블렌드한다.
    /// </summary>
    public struct TerrainLayerDef
    {
        public string layerName;
        public Texture2D albedo;
        public NationType nation;
        public float heightBlendCenter;   // 정규화 높이[0,1] 이 레이어가 우세한 위치
        public float heightBlendWidth;    // 정규화 높이 혼합 폭
        public float slopePreference;     // 범위 [-1,1] 경사가 높을수록 가중 증가(+) / 감소(-)
        public float strength;            // 전체 강도 배율
        public float tiling;              // 이 레이어 UV 타일 크기(미터)

        /// <summary>
        /// 국가당 레이어 4종 생성 (저지대/중지대/고지대바위/느슨흙).
        /// 텍스처가 부족하면 기존 텍스처를 재사용(안전). list가 비면 빈 리스트 반환.
        /// </summary>
        public static List<TerrainLayerDef> CreateForNation(NationType nation, List<Texture2D> textures)
        {
            var layers = new List<TerrainLayerDef>();
            if (textures == null || textures.Count == 0) return layers;

            Texture2D t0 = textures[0];
            Texture2D t1 = textures.Count > 1 ? textures[1] : textures[0];
            Texture2D t2 = textures.Count > 2 ? textures[2] : textures[0];
            Texture2D t3 = textures.Count > 3 ? textures[3] : textures[0];
            const float TILE = 60f;

            layers.Add(Make("lowland",    nation, t0, 0.05f, 0.20f, -0.9f, 1.0f, TILE));
            layers.Add(Make("midland",    nation, t1, 0.50f, 0.28f,  0.0f, 0.8f, TILE));
            layers.Add(Make("rock_upland",nation, t2, 0.85f, 0.25f,  1.0f, 1.0f, TILE));
            layers.Add(Make("loose_dirt", nation, t3, 0.25f, 0.40f, -0.4f, 0.6f, TILE));
            return layers;
        }

        private static TerrainLayerDef Make(string name, NationType nat, Texture2D tex,
            float hCenter, float hWidth, float slopePref, float str, float tile)
        {
            TerrainLayerDef d;
            d.layerName = name;
            d.albedo = tex;
            d.nation = nat;
            d.heightBlendCenter = hCenter;
            d.heightBlendWidth = hWidth;
            d.slopePreference = slopePref;
            d.strength = str;
            d.tiling = tile;
            return d;
        }
    }
}
