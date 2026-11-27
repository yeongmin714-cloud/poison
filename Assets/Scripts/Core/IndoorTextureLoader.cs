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

        // [125차 후속32 T1] 주점 세트 커버 — 기존 계약 승계(바닥 1장=2.4×2.4m / 벽 1장=2.2×1.1m)
        public const float TavernFloorCoverMeters = 2.4f;
        public const float TavernWallCoverX = 2.2f;
        public const float TavernWallCoverY = 1.1f;

        private static Texture2D _floor, _floorN, _wallStone, _wallStoneN, _wallPlaster, _wallPlasterN, _wood, _woodN, _straw;
        // [125차 후속32 T1] 주점 세트(tavern_*) — 아레나 로비 전용(성 내부 불변 계약 유지 — 기존 5종과 병행)
        private static Texture2D _tavernFloor, _tavernFloorN, _tavernWallPlaster, _tavernWallPlasterN, _tavernWallTimber, _tavernWallTimberN;
        private static bool _loaded;

        /// <summary>[P17] 실내 기둥 표시 여부 — 기본 OFF(예시 이미지 스타일: 기둥 없음).</summary>
        public static bool IncludePillars = false;

        /// <summary>[2026-10-10 F1] 벽 하부 석재 밴드 복귀 옵션 — 기본 OFF(계획 D1 전면 회반죽 유지).
        /// true 시 성 내부 벽 하부 ~1.1m에 wall_stone_lower 밴드(예시의 하단 석재 노출)가 얹힌다.</summary>
        public static bool IncludeWallStoneLower = false;

        public static bool HasFiles { get { Load(); return _floor != null; } }

        /// <summary>[125차 후속32 T1] 주점 세트 존재 판정 — tavern_floor_plank 로드 성공 여부(파일 부재 시 기존 경로 폴백).</summary>
        public static bool HasTavernFiles { get { Load(); return _tavernFloor != null; } }

        public static Texture2D Floor { get { Load(); return _floor; } }
        public static Texture2D FloorNormal { get { Load(); return _floorN; } }
        public static Texture2D WallStone { get { Load(); return _wallStone; } }
        public static Texture2D WallStoneNormal { get { Load(); return _wallStoneN; } }
        public static Texture2D WallPlaster { get { Load(); return _wallPlaster; } }
        public static Texture2D WallPlasterNormal { get { Load(); return _wallPlasterN; } }
        public static Texture2D WoodNormal { get { Load(); return _woodN; } }
        public static Texture2D Wood { get { Load(); return _wood; } }
        public static Texture2D StrawDecal { get { Load(); return _straw; } }
        public static Texture2D TavernFloor { get { Load(); return _tavernFloor; } }
        public static Texture2D TavernFloorNormal { get { Load(); return _tavernFloorN; } }
        public static Texture2D TavernWallPlaster { get { Load(); return _tavernWallPlaster; } }
        public static Texture2D TavernWallPlasterNormal { get { Load(); return _tavernWallPlasterN; } }
        public static Texture2D TavernWallTimber { get { Load(); return _tavernWallTimber; } }
        public static Texture2D TavernWallTimberNormal { get { Load(); return _tavernWallTimberN; } }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _floor = Resources.Load<Texture2D>(BasePath + "floor_flagstone");
            _floorN = Resources.Load<Texture2D>(BasePath + "floor_flagstone_n");
            _wallStone = Resources.Load<Texture2D>(BasePath + "wall_stone_lower");
            _wallStoneN = Resources.Load<Texture2D>(BasePath + "wall_stone_lower_n");
            _wallPlaster = Resources.Load<Texture2D>(BasePath + "wall_plaster_upper");
            _wallPlasterN = Resources.Load<Texture2D>(BasePath + "wall_plaster_upper_n");
            _wood = Resources.Load<Texture2D>(BasePath + "wood_frame");
            _woodN = Resources.Load<Texture2D>(BasePath + "wood_frame_n");
            _straw = Resources.Load<Texture2D>(BasePath + "floor_straw_decal");
            // [125차 후속32 T1] 주점 세트 — 아레나 로비 전용(파일 부재 시 null → 기존 경로 폴백)
            _tavernFloor = Resources.Load<Texture2D>(BasePath + "tavern_floor_plank");
            _tavernFloorN = Resources.Load<Texture2D>(BasePath + "tavern_floor_plank_n");
            _tavernWallPlaster = Resources.Load<Texture2D>(BasePath + "tavern_wall_plaster");
            _tavernWallPlasterN = Resources.Load<Texture2D>(BasePath + "tavern_wall_plaster_n");
            _tavernWallTimber = Resources.Load<Texture2D>(BasePath + "tavern_wall_timber");
            _tavernWallTimberN = Resources.Load<Texture2D>(BasePath + "tavern_wall_timber_n");
            Debug.Log($"[IndoorTextureLoader] 로드 — floor={(_floor != null)} normal={(_floorN != null)} " +
                      $"stone={(_wallStone != null)} stoneN={(_wallStoneN != null)} plaster={(_wallPlaster != null)} " +
                      $"wood={(_wood != null)} straw={(_straw != null)} " +
                      $"tavern[floor={(_tavernFloor != null)} floorN={(_tavernFloorN != null)} plaster={(_tavernWallPlaster != null)} plasterN={(_tavernWallPlasterN != null)} timber={(_tavernWallTimber != null)} timberN={(_tavernWallTimberN != null)}] [125차 후속32 T1]");
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
