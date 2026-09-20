using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// P17 — 실내 고품질 텍스처 로더 (Resources/Indoor/).
    /// 사용자 제공 심리스 텍스처가 존재하면 그것을, 없으면 기존 절차 생성(IndoorTextureGenerator) 폴백.
    ///
    /// [파일 규약] Assets/Resources/Indoor/
    ///   floor_flagstone.png      — 바닥 석재 (심리스, 이미지 1장 ≈ 2.4×2.4m)
    ///   floor_flagstone_n.png    — 바닥 노멀맵 (선택)
    ///   wall_stone_lower.png     — 벽 하부 석재 (심리스, 가로 ≈ 2.2m × 세로 ≈ 1.1m)
    ///   wall_stone_lower_n.png   — 벽 노멀맵 (선택)
    ///   wall_plaster_upper.png   — 벽 상부 회반죽
    ///   wood_frame.png           — 목재 프레임(기둥/보/문틀 공용, 세로 나뭇결)
    ///   floor_straw_decal.png    — 짚단/약초 바닥 데칼 (알파 포함, 선택)
    /// </summary>
    public static class IndoorTextureLoader
    {
        private const string BasePath = "Indoor/";

        // 이미지 1장이 커버하는 실제 미터 크기 (타일링 계산의 단일 소스)
        public const float FloorCoverMeters = 2.4f;      // floor_flagstone 1장 = 2.4m
        public const float WallStoneCoverX = 2.2f;       // wall_stone_lower 1장 가로
        public const float WallStoneCoverY = 1.1f;       // wall_stone_lower 1장 세로
        public const float WallPlasterCoverX = 2.2f;     // 회반죽도 동일 그리드
        public const float DecalSizeMeters = 1.2f;       // 짚단 데칼 한 장

        private static Texture2D _floor, _floorN, _wallStone, _wallStoneN, _wallPlaster, _wood, _straw;
        private static bool _loaded;

        /// <summary>[P17] 실내 기둥 표시 여부 — 기본 OFF(예시 이미지 스타일: 기둥 없음).</summary>
        public static bool IncludePillars = false;

        public static bool HasFiles { get { Load(); return _floor != null; } }

        public static Texture2D Floor { get { Load(); return _floor; } }
        public static Texture2D FloorNormal { get { Load(); return _floorN; } }
        public static Texture2D WallStone { get { Load(); return _wallStone; } }
        public static Texture2D WallStoneNormal { get { Load(); return _wallStoneN; } }
        public static Texture2D WallPlaster { get { Load(); return _wallPlaster; } }
        public static Texture2D Wood { get { Load(); return _wood; } }
        public static Texture2D StrawDecal { get { Load(); return _straw; } }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _floor = Resources.Load<Texture2D>(BasePath + "floor_flagstone");
            _floorN = Resources.Load<Texture2D>(BasePath + "floor_flagstone_n");
            _wallStone = Resources.Load<Texture2D>(BasePath + "wall_stone_lower");
            _wallStoneN = Resources.Load<Texture2D>(BasePath + "wall_stone_lower_n");
            _wallPlaster = Resources.Load<Texture2D>(BasePath + "wall_plaster_upper");
            _wood = Resources.Load<Texture2D>(BasePath + "wood_frame");
            _straw = Resources.Load<Texture2D>(BasePath + "floor_straw_decal");
            Debug.Log($"[IndoorTextureLoader] 로드 — floor={(_floor != null)} normal={(_floorN != null)} " +
                      $"stone={(_wallStone != null)} stoneN={(_wallStoneN != null)} plaster={(_wallPlaster != null)} " +
                      $"wood={(_wood != null)} straw={(_straw != null)}");
        }

        /// <summary>Wrap/필터/리니어(노멀) 표준 세팅 — 로드 직후 1회.</summary>
        public static void Configure(Texture2D tex, bool linear, bool mipmaps = true)
        {
            if (tex == null) return;
            tex.wrapModeU = TextureWrapMode.Repeat;
            tex.wrapModeV = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
        }
    }
}
