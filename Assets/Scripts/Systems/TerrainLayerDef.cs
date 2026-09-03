using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 지형 멀티레이어 스플랫을 위한 단일 레이어 정의.
    /// 각 국가에 3~4개의 레이어를 배정하고, Baker가 높이/경사/노이즈 가중치로 블렌드한다.
    /// colorTint는 방위별 테마 색조 보정 — Idyllic 밝은 판타지 톤 텍스처에 곱해져
    /// 국가 고유 색감(동초록/남붉은사막/북설원/서사막/중심대리석)을 유지·강화한다.
    /// </summary>
    public struct TerrainLayerDef
    {
        public string layerName;
        public Texture2D albedo;
        public NationType nation;
        public Color colorTint;           // 방위 테마 색조(알베도에 곱해짐). 기본 Color.white
        public float heightBlendCenter;   // 정규화 높이[0,1] 이 레이어가 우세한 위치
        public float heightBlendWidth;    // 정규화 높이 혼합 폭
        public float slopePreference;     // 범위 [-1,1] 경사가 높을수록 가중 증가(+) / 감소(-)
        public float strength;            // 전체 강도 배율
        public float tiling;              // 이 레이어 UV 타일 크기(미터)

        /// <summary>
        /// 국가당 레이어 4종 생성 (저지대/중지대/고지대바위/느슨흙).
        /// 텍스처가 부족하면 기존 텍스처를 재사용(안전). list가 비면 빈 리스트 반환.
        /// 국가별 colorTint로 방위 테마 색을 강화 (결정론적 — 같은 시드 → 같은 결과).
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

            // 방위별 테마 색조 (Idyllic 밝은 판타지 톤 → 방위 테마 강화용 보정값, 조정 가능)
            Color tint;
            switch (nation)
            {
                case NationType.East:   tint = new Color(0.95f, 1.05f, 0.90f); break; // 초록 초원 강조
                case NationType.South:  tint = new Color(1.10f, 0.78f, 0.70f); break; // 붉은 사막
                case NationType.North:  tint = new Color(0.96f, 0.99f, 1.05f); break; // 연청/설원
                case NationType.West:   tint = new Color(1.08f, 0.90f, 0.72f); break; // 황갈 사막
                case NationType.Empire: tint = new Color(0.95f, 0.97f, 1.00f); break; // 회백 대리석
                default:                tint = Color.white;                    break; // None/Dracula
            }

            layers.Add(Make("lowland",    nation, t0, tint, 0.05f, 0.20f, -0.9f, 1.0f, TILE));
            layers.Add(Make("midland",    nation, t1, tint, 0.50f, 0.28f,  0.0f, 0.8f, TILE));
            layers.Add(Make("rock_upland",nation, t2, tint, 0.85f, 0.25f,  1.0f, 1.0f, TILE));
            layers.Add(Make("loose_dirt", nation, t3, tint, 0.25f, 0.40f, -0.4f, 0.6f, TILE));
            return layers;
        }

        private static TerrainLayerDef Make(string name, NationType nat, Texture2D tex, Color tint,
            float hCenter, float hWidth, float slopePref, float str, float tile)
        {
            TerrainLayerDef d;
            d.layerName = name;
            d.albedo = tex;
            d.nation = nat;
            d.colorTint = tint;
            d.heightBlendCenter = hCenter;
            d.heightBlendWidth = hWidth;
            d.slopePreference = slopePref;
            d.strength = str;
            d.tiling = tile;
            return d;
        }
    }
}
