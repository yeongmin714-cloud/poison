using System.Collections.Generic;
using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// 지형 멀티레이어 스플랫을 위한 단일 레이어 정의.
    /// 각 국가에 5개의 레이어를 배정하고, Baker가 절벽/수변/흙길/고도 마스크
    /// (모두 TerrainShape 또는 TerrainPathGenerator/LakeGenerator와 동일 소스,
    /// 즉 "형태와 색이 같은 데이터"에서 계산)로 블렌드한다.
    ///
    /// 레이어 의미 (국가당 5):
    ///   L1 lowland   — 저지대 잔디 (라임그린)
    ///   L2 midland   — 중지대 잔디 (황록)
    ///   L3 rock_cliff— 바위·절벽 (청회) — 절벽 ridge 마스크 m>0.5 공유
    ///   L4 dirt_path — 흙길·황토 — TerrainPathGenerator 흙길 좌표
    ///   L5 moss_water— 이끼·수변 — 호수 반경 +8m 밴드
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
        public float strength;            // 전체 강도 배율 (최신 Baker는 고도 3분위/마스크 기반이라 예비 필드)
        public float tiling;              // 이 레이어 UV 타일 크기(미터)

        /// <summary>
        /// Resources/Models/UserProvided/terrain/textures_idyllic/ 리소스 폴더.
        /// 이 fallback 경로는 Resources.Load로 직접 로드한다 (제공된 목록에 이름이 없을 때).
        /// 기존 JPEG 재사용 원칙을 계승한다.
        /// </summary>
        const string IDYLLIC_FOLDER = "Models/UserProvided/terrain/textures_idyllic/";

        /// <summary>
        /// 국가당 5레이어 생성 (저지대/중지대/바위·절벽/흙길·황토/이끼·수변).
        /// 텍스처 우선순위: ① 제공된 목록에서 이름 일치 ② Resources(Idyllic) 직접 로드 ③ textures[0] 폴백.
        /// 국가별 colorTint로 방위 테마 색을 강화 (결정론적 — 같은 시드 → 같은 결과).
        /// list가 비면 빈 리스트 반환.
        /// </summary>
        public static List<TerrainLayerDef> CreateForNation(NationType nation, List<Texture2D> textures)
        {
            var layers = new List<TerrainLayerDef>();
            if (textures == null || textures.Count == 0) return layers;

            Texture2D fb = textures[0];
            const float TILE = 60f;

            // 방위별 테마 색조 (Idyllic 밝은 판타지 톤 → 방위 테마 강화용 보정값)
            Color tint;
            switch (nation)
            {
                case NationType.East:   tint = new Color(0.95f, 1.05f, 0.90f); break; // 초록 초원 강조
                case NationType.South:  tint = new Color(1.10f, 0.78f, 0.70f); break; // 붉은 사막
                case NationType.North:  tint = new Color(1.02f, 1.02f, 1.12f); break; // 순백 설원 (AA4 09-05 추가 상향 — 북 더 하얗게)
                case NationType.West:   tint = new Color(1.08f, 0.90f, 0.72f); break; // 황갈 사막
                case NationType.Empire: tint = new Color(0.95f, 0.97f, 1.00f); break; // 회백 대리석
                default:                tint = Color.white;                    break; // None/Dracula
            }

            string[] res = GetNationTextureMap(nation);
            // L1..L5 (순서 보장 — ComputeWeights가 이름 기반으로 배정, 부족 시 name으로 식별 안 되면 오름차순 인덱스 폴백)
            Texture2D t0 = Resolve(textures, res[0], fb);
            Texture2D t1 = Resolve(textures, res[1], fb);
            Texture2D t2 = Resolve(textures, res[2], fb);
            Texture2D t3 = Resolve(textures, res[3], fb);
            Texture2D t4 = Resolve(textures, res[4], fb);

            layers.Add(Make("lowland",     nation, t0, tint, 0.08f, 0.22f, -0.8f, 1.0f, TILE));
            layers.Add(Make("midland",     nation, t1, tint, 0.52f, 0.30f,  0.0f, 0.85f, TILE));
            layers.Add(Make("rock_cliff",  nation, t2, tint, 0.85f, 0.25f,  1.0f, 1.0f, TILE));
            layers.Add(Make("dirt_path",  nation, t3, tint, 0.28f, 0.40f, -0.4f, 0.7f, TILE));
            layers.Add(Make("moss_water", nation, t4, tint, 0.15f, 0.30f, -0.9f, 0.8f, TILE));
            return layers;
        }

        /// <summary>
        /// 국가당 5레이어 텍스처 자원명 매핑표 (확정된 21종 Idyllic PNG에서 결정론 선택).
        /// 부족한 슬롯(예: East 바위, South 이끼)은 타국/공통 슬롯 재활용 — "기존 JPEG 재사용" 원칙 계승.
        /// </summary>
        static string[] GetNationTextureMap(NationType nation)
        {
            switch (nation)
            {
                case NationType.East:
                    return new[] { "east_grass1_albedo", "east_grass2_albedo", "empire_rock_albedo", "west_dirt_albedo", "east_moss_albedo" };
                case NationType.South:
                    return new[] { "east_grass1_albedo", "south_dirt2_albedo", "south_rock_albedo", "south_dirt_albedo", "north_moss_albedo" };
                case NationType.West:
                    return new[] { "east_grass1_albedo", "east_grass2_albedo", "west_rock_albedo", "west_dirt_albedo", "west_sand_albedo" };
                case NationType.North:
                    // Z2: L1=north_snow(설원, 고지대 우세), L2=north_grass(청록 잔디, 저지대),
                    //     L3=north_cliff(설빙 바위 유지), L4=empire_dirtstone, L5=north_moss.
                    // ComputeWeights가 북 전용으로 L1=고지대/L2=저지대를 반전 배정한다.
                    return new[] { "north_snow_albedo", "north_grass_albedo", "north_cliff_albedo", "empire_dirtstone_albedo", "north_moss_albedo" };
                case NationType.Empire:
                    return new[] { "east_grass1_albedo", "east_meadow_albedo", "empire_cliff_albedo", "empire_dirtstone_albedo", "empire_cobble_albedo" };
                default:
                    return new[] { "east_grass1_albedo", "east_grass2_albedo", "empire_rock_albedo", "west_dirt_albedo", "east_moss_albedo" };
            }
        }

        /// <summary>
        /// 제공 목록에서 이름 일치 → 없으면 Resources(Idyllic)로 직접 로드 → 마지막 폴백(첫 텍스처).
        /// 이름 일치는 대소문자 무관 부분 일치 (Resources.Load name = 파일명에서 확장자 제거).
        /// </summary>
        static Texture2D Resolve(List<Texture2D> provided, string resName, Texture2D fallback)
        {
            if (!string.IsNullOrEmpty(resName) && provided != null)
            {
                for (int i = 0; i < provided.Count; i++)
                {
                    var t = provided[i];
                    if (t == null || string.IsNullOrEmpty(t.name)) continue;
                    if (t.name.ToLowerInvariant().Contains(resName.ToLowerInvariant()))
                        return t;
                }
            }
            if (!string.IsNullOrEmpty(resName))
            {
                try
                {
                    var tex = Resources.Load<Texture2D>(IDYLLIC_FOLDER + resName);
                    if (tex != null) return tex;
                }
                catch (System.Exception) { /* 로드 실패 시 폴백 */ }
            }
            return fallback;
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