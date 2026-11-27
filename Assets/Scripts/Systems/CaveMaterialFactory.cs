// [143차 P1] 절차 동굴 머티리얼 팩토리 — 암반 컬러/노멀 텍스처를 코드 생성(에셋 파일 0).
//
// 결정론 계약 — 유니티 Random API 0건. Hash01(정수 혼합 해시) → ValueNoise(smoothstep
//   bilinear) → Fbm(2옥타브) 전부 순수 함수라 동일 해상도·시드에서 런/머신 무관 동일 픽셀.
//   머티리얼은 캐시 텍스처를 공유(EnsureCache null 가드 — 선례: IndoorMaterialFactory._blobShadowTex).
// Resources/Indoor 불변 — 이 팩토리는 Resources/Indoor 소스를 읽지도 쓰지도 않는다(파일 I/O 0,
//   기존 Indoor 머티리얼/텍스처 무영향). 생성물은 런타임 전용.
using System;
using UnityEngine;

namespace ProjectName.Systems
{
    public static class CaveMaterialFactory
    {
        // ---- 정적 캐시(텍스처 4종 — 컬러/노멀 × 바닥/벽) ----
        private static Texture2D _floorColorTex;
        private static Texture2D _floorNormalTex;
        private static Texture2D _wallColorTex;
        private static Texture2D _wallNormalTex;

        // [158차 P3-C] 실내 고품질 캐시 — 벽면 마스너리/받침/돌판 경로/자갈
        private static Texture2D _sideColorTex;
        private static Texture2D _sideNormalTex;
        private static Texture2D _bandColorTex;
        private static Texture2D _bandNormalTex;
        private static Texture2D _pathColorTex;
        private static Texture2D _pathNormalTex;
        private static Texture2D _gravelColorTex;
        private static Material _gravelMat;

        // [165차 P1] 던전 전용 흙 — 농밭 흙 텍스처(FarmPlotVisuals) 재사용 폐기.
        // 농밭 경준선 정현파 밴드가 던전에서 "밭 이랑" 반복 패턴으로 보이는 결함(사용자 지적 + vision 실측).
        private static Texture2D _dirtColorTex;
        private static Texture2D _dirtNormalTex;

        private const int ColorTexSize = 512;    // [157차 B-A] 컬러 512² — 실내 고품질(안개 축소로 디테일 가시도 상승 동반)
        private const int NormalTexSize = 128;   // 노멀 128² — sobel CPU 비용 절반

        // 기본 톤 — 어두운 암반(빌더 기존 컬러 승계). 컴파일 타임 상수 Color 금지 → static readonly.
        private static readonly Color FloorTone = new Color(0.25f, 0.22f, 0.20f);
        private static readonly Color WallTone = new Color(0.22f, 0.20f, 0.18f);

        // 노이즈 시드 — 바닥/벽 상이(같은 수식 재사용, 패턴 겹침 방지).
        private const int FloorSeed = 143;
        private const int WallSeed = 243;

        // [158차 P3-C] 품질 텍스처 상수 — 사이즈/시드/톤(벽면 마스너리·받침·돌판·자갈 상이)
        private const int QualityTexSize = 512;
        private const int GravelSize = 128;
        private const int SideSeed = 343;
        private const int BandSeed = 443;
        private const int PathSeed = 543;
        private const int GravelSeed = 643;
        private static readonly Color SideTone = new Color(0.26f, 0.24f, 0.22f);
        private static readonly Color BandTone = new Color(0.19f, 0.17f, 0.16f);
        private static readonly Color PathTone = new Color(0.36f, 0.34f, 0.32f);
        private static readonly Color GravelTone = new Color(0.33f, 0.31f, 0.29f);

        // [165차 P1] 던전 흙 상수 — 컬러 1024²(성내부 기준원 IndoorTextureGenerator 512² 상회, CPU 1회 캐시),
        // 노멀 512²(기존 NormalTexSize=128과 별도 — 자갈/균열 요철 전용), 시드 743(기존 143~643과 충돌 없음),
        // 톤은 SoilBaseColor(0.42,0.29,0.16) 계열에서 약간 어둡게 조정한 던전 암흙.
        private const int DirtColorTexSize = 1024;
        private const int DirtNormalTexSize = 512;
        private const int DirtSeed = 743;
        private static readonly Color DungeonDirtTone = new Color(0.36f, 0.25f, 0.14f);

        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string StandardShaderName = "Standard";

        // ================================================================
        // 공개 API (시그니처 고정)
        // ================================================================

        /// <summary>바닥 머티리얼 — 바닥 컬러 텍스처 + 노멀, tiling (0.5,0.5)(1타일=2m), 흰색 틴트.</summary>
        public static Material CreateCaveFloor()
        {
            EnsureCache();
            var mat = CreateMaterial("CaveFloorMat");
            mat.SetTexture("_BaseMap", _floorColorTex);
            mat.mainTextureScale = new Vector2(0.5f, 0.5f);
            mat.SetColor("_BaseColor", Color.white);
            ApplyNormalMap(mat, _floorNormalTex);
            return mat;
        }

        /// <summary>[165차 P1] 동굴 바닥 흙 머티리얼 — 던전 전용 절차 흙 텍스처(컬러 1024² + 노멀 512²).
        /// 156차 FarmPlotVisuals.GetOrCreateSoilTexture() 재사용은 폐기 — 농밭 경준선 정현파 밴드가
        /// 던전에서 "밭 이랑" 반복 패턴으로 보이는 결함(사용자 지적). 신설 구성: 흙 베이스 다옥타브 fbm
        /// + 잔자갈 스펙클 + 미세 균열선 + 마모 얼룩(경준선 밴드 없음, 결정론 시드 DirtSeed=743).
        /// 바닥 메시 UV는 셀당 1 유지(CaveInteriorBuilder.CreateCellFloor 무수정) → 머티리얼
        /// mainTextureScale 0.5(2셀=4m 주기 연속 패턴)로 타일 반복 티 완화.</summary>
        public static Material CreateDirtFloor()
        {
            EnsureDirtCache();
            var mat = CreateMaterial("CaveDirtFloorMat");
            mat.SetTexture("_BaseMap", _dirtColorTex);
            if (mat.HasProperty("_MainTex")) mat.mainTexture = _dirtColorTex;
            mat.mainTextureScale = new Vector2(0.5f, 0.5f);   // 셀당 UV 1 → 0.5 = 2셀 주기 — 반복 티 완화
            mat.SetColor("_BaseColor", Color.white);
            ApplyNormalMap(mat, _dirtNormalTex);   // [165차] 자갈/균열 대응 요철 신설(기존 노멀 없음 → 신규 부여)
            return mat;
        }

        /// <summary>[165차 P1] 던전 흙 텍스처 캐시 — null 가드 1회 생성(선례: EnsureCache/EnsureQualityCache).
        /// 컬러/노멀 해상도 상이(1024/512)라 높이장을 각 해상도로 각각 생성(동일 시드·수식 — 패턴 일치).</summary>
        private static void EnsureDirtCache()
        {
            if (_dirtColorTex != null) return;
            _dirtColorTex = CreateDirtFloorColorTexture(
                BuildDirtFloorHeightField(DirtColorTexSize, DirtSeed), DirtColorTexSize, DungeonDirtTone, DirtSeed, "CaveDirtFloorColorTex");
            _dirtNormalTex = CreateNormalFromHeight(
                BuildDirtFloorHeightField(DirtNormalTexSize, DirtSeed), DirtNormalTexSize, "CaveDirtFloorNormalTex");
        }

        // ================================================================
        // [158차 P3-C] 실내 고품질 — 마스너리 벽면/받침/돌판 경로/자갈
        // ================================================================

        /// <summary>[158차 P3-C] 벽면 마스너리 머티리얼 — 석재 줄눈(가로 코스 8단·세로 교차 스태거 블록) 절차 텍스처.
        ///   세로 tiling 1(3m 벽 전체 1회 — 줄눈 정렬 보장), 가로 length/2. 노멀=높이장(줄눈 그루브) sobel.</summary>
        public static Material CreateWallSide(float length)
        {
            EnsureQualityCache();
            var mat = CreateMaterial("CaveWallSideMat");
            mat.SetTexture("_BaseMap", _sideColorTex);
            mat.mainTextureScale = new Vector2(Mathf.Max(1f, length / 2f), 1f);
            mat.SetColor("_BaseColor", Color.white);
            ApplyNormalMap(mat, _sideNormalTex);
            return mat;
        }

        /// <summary>[158차 P3-C] 벽 받침 밴드 머티리얼 — 벽 하단 플린스(짙은 돌·거친 fbm). 세로 tiling 1.</summary>
        public static Material CreateBaseBandMat(float length)
        {
            EnsureQualityCache();
            var mat = CreateMaterial("CaveWallBaseBandMat");
            mat.SetTexture("_BaseMap", _bandColorTex);
            mat.mainTextureScale = new Vector2(Mathf.Max(1f, length / 2f), 1f);
            mat.SetColor("_BaseColor", Color.white);
            ApplyNormalMap(mat, _bandNormalTex);
            return mat;
        }

        /// <summary>[158차 P3-C] 돌판 경로 머티리얼 — 2×2 슬랩+그라우트 줄눈(512²·셀 쿼드 UV 1:1 대응).</summary>
        public static Material CreateStonePathMat()
        {
            EnsureQualityCache();
            var mat = CreateMaterial("CaveStonePathMat");
            mat.SetTexture("_BaseMap", _pathColorTex);
            mat.mainTextureScale = Vector2.one;
            mat.SetColor("_BaseColor", Color.white);
            ApplyNormalMap(mat, _pathNormalTex);
            return mat;
        }

        /// <summary>[158차 P3-C] 자갈 디테일 머티리얼 — 잔돌 원 스탬프(128²·공유 캐시 인스턴스).</summary>
        public static Material CreateGravelMat()
        {
            EnsureQualityCache();
            if (_gravelMat == null) _gravelMat = CreateMaterial("CaveGravelMat");
            _gravelMat.SetTexture("_BaseMap", _gravelColorTex);
            _gravelMat.mainTextureScale = Vector2.one;
            _gravelMat.SetColor("_BaseColor", Color.white);
            return _gravelMat;
        }

        /// <summary>[158차 P3-C] 품질 텍스처 캐시 — 마스너리/받침/돌판/자갈 null 가드 1회 생성(선례: EnsureCache).</summary>
        private static void EnsureQualityCache()
        {
            EnsureCache();
            if (_sideColorTex != null) return;
            var sideH = BuildMasonryHeightField(QualityTexSize, SideSeed);   // [165차 P2] 1회 생성 — 컬러/노멀 공유
            _sideColorTex = CreateMasonryColorTexture(sideH, QualityTexSize, SideTone, SideSeed, "CaveWallSideColorTex");
            _sideNormalTex = CreateNormalFromHeight(sideH, QualityTexSize, "CaveWallSideNormalTex");   // 노멀 512² — 256 상향 요건 충족
            _bandColorTex = CreateColorFromHeight(BuildBaseBandHeightField(QualityTexSize, BandSeed), QualityTexSize, BandTone, "CaveWallBaseBandColorTex");
            _bandNormalTex = CreateNormalFromHeight(BuildBaseBandHeightField(QualityTexSize, BandSeed), QualityTexSize, "CaveWallBaseBandNormalTex");
            _pathColorTex = CreateColorFromHeight(BuildStonePathHeightField(QualityTexSize, PathSeed), QualityTexSize, PathTone, "CaveStonePathColorTex");
            _pathNormalTex = CreateNormalFromHeight(BuildStonePathHeightField(QualityTexSize, PathSeed), QualityTexSize, "CaveStonePathNormalTex");
            _gravelColorTex = CreateColorFromHeight(BuildGravelHeightField(GravelSize, GravelSeed), GravelSize, GravelTone, "CaveGravelColorTex");
        }

        /// <summary>벽 머티리얼 — 벽 컬러 텍스처 + 노멀, tiling (length/2, height/2)(변 길이별 타일링).</summary>
        public static Material CreateCaveWall(float length, float height)
        {
            EnsureCache();
            var mat = CreateMaterial("CaveWallMat");
            mat.SetTexture("_BaseMap", _wallColorTex);
            mat.mainTextureScale = new Vector2(length / 2f, height / 2f);
            mat.SetColor("_BaseColor", Color.white);
            ApplyNormalMap(mat, _wallNormalTex);
            return mat;
        }

        /// <summary>바닥 톤 스트립 — 바닥 컬러 텍스처 재사용, 호출부 톤 보존(_BaseColor=tone).</summary>
        public static Material CreateCaveFloorStrip(Color tone)
        {
            EnsureCache();
            var mat = CreateMaterial("CaveFloorStrip_Mat");
            mat.SetTexture("_BaseMap", _floorColorTex);
            mat.mainTextureScale = new Vector2(0.5f, 0.5f);
            mat.SetColor("_BaseColor", tone);   // 기존 plaza/dirt 톤 보존
            ApplyNormalMap(mat, _floorNormalTex);
            return mat;
        }

        // ================================================================
        // [143차 P3] 입구 빛웅덩이 — 방사 페이드 웜 화이트 텍스처+투명 머티리얼
        // (선례: IndoorMaterialFactory.GetBlobShadowTexture / CreateBlobShadowMat 동일 구조 복제)
        // ================================================================

        private static Texture2D _entranceLightPoolTex;

        /// <summary>[143차 P3] 입구 빛웅덩이 텍스처(128²) — 중심 웜 화이트(1.0, 0.80, 0.55) 알파 0.55 →
        /// 가장자리 0 방사 감쇠. linear:false, Repeat/Bilinear. 캐시 1회(선례: _blobShadowTex).</summary>
        public static Texture2D GetEntranceLightPoolTexture()
        {
            if (_entranceLightPoolTex != null) return _entranceLightPoolTex;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear: false)
            {
                name = "CaveEntranceLightPoolTex",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = 0.55f * Mathf.Pow(1f - d, 1.5f);
                    pixels[y * size + x] = new Color(1.0f, 0.80f, 0.55f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _entranceLightPoolTex = tex;
            return tex;
        }

        /// <summary>[143차 P3] 입구 빛웅덩이 머티리얼 — CreateBlobShadowMat 셰이더/블렌드 셋업 그대로 복제
        /// (URP Unlit 투명 + SrcAlpha/OneMinusSrcAlpha + ZWrite 0 + _SURFACE_TYPE_TRANSPARENT).
        /// URP Unlit 부재 시 Sprites/Default 폴백, 그마저 부재하면 null(호출부 스킵).</summary>
        public static Material CreateEntranceLightPoolMat()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            var m = new Material(shader) { name = "CaveEntranceLightPool" };
            var tex = GetEntranceLightPoolTexture();
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            else m.mainTexture = tex;
            m.color = new Color(1f, 1f, 1f, 1f);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        // ================================================================
        // [143차 P4] 불꽃 — 수직 물방울 알파+y 그라데이션 텍스처+투명 unlit 머티리얼
        // (셋업 선례: GetEntranceLightPoolTexture / CreateEntranceLightPoolMat 동일 구조 복제)
        // ================================================================

        private static Texture2D _flameTex;

        /// <summary>[143차 P4] 불꽃 텍스처(128²) — 좌우 대칭 물방울 알파(하단 넓고 상단 뾰족,
        /// uv.y 폭 감소 곡선), 색 y 그라데이션 하단 (2.2,1.1,0.35)→상단 (1.0,0.45,0.12).
        /// linear:false, Repeat/Bilinear. 캐시 1회(선례: _entranceLightPoolTex).</summary>
        public static Texture2D GetFlameTexture()
        {
            if (_flameTex != null) return _flameTex;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear: false)
            {
                name = "CaveFlameTex",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color[size * size];
            var bottom = new Color(2.2f, 1.1f, 0.35f);   // 하단 HDR 웜 코어
            var top = new Color(1.0f, 0.45f, 0.12f);     // 상단 딥 오렌지
            for (int y = 0; y < size; y++)
            {
                float dy = (y + 0.5f) / size;                        // 0=하단, 1=상단
                float halfWidth = 0.42f * Mathf.Pow(1f - dy, 0.7f);  // 물방울 폭 감소 곡선 — 상단 뾰족
                var c = Color.Lerp(bottom, top, dy);
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;             // 좌우 대칭(중심 0.5)
                    float t = halfWidth > 0.0001f ? Mathf.Clamp01(Mathf.Abs(dx) / halfWidth) : 1f;
                    float a = 1f - t;
                    a = a * a * (3f - 2f * a);                       // smoothstep 가장자리 소프트
                    pixels[y * size + x] = new Color(c.r, c.g, c.b, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _flameTex = tex;
            return tex;
        }

        /// <summary>[143차 P4] 불꽃 머티리얼 — CreateEntranceLightPoolMat 셋업 그대로 복제
        /// (URP Unlit 투명 + SrcAlpha/OneMinusSrcAlpha + ZWrite 0 + _SURFACE_TYPE_TRANSPARENT),
        /// name "CaveFlame", _BaseMap=GetFlameTexture(). HDR 색 배율은 텍스처가 진다(추가 SetColor 없음).</summary>
        public static Material CreateFlameMat()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;
            var m = new Material(shader) { name = "CaveFlame" };
            var tex = GetFlameTexture();
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            else m.mainTexture = tex;
            m.color = new Color(1f, 1f, 1f, 1f);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return m;
        }

        // ================================================================
        // 머티리얼/텍스처 생성
        // ================================================================

        /// <summary>URP Lit 우선, 부재 시 Standard 폴백(Shader.Find 2단).</summary>
        private static Material CreateMaterial(string materialName)
        {
            var shader = Shader.Find(UrpLitShaderName);
            if (shader == null) shader = Shader.Find(StandardShaderName);
            return new Material(shader) { name = materialName };
        }

        /// <summary>노멀 적용 — _NORMALMAP 키워드+_BumpMap(스케일 1). 실패 시 컬러 폴백(경고).</summary>
        private static void ApplyNormalMap(Material mat, Texture2D normalTex)
        {
            try
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1f);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CaveMaterialFactory] 노멀 적용 실패 — 컬러 렌더 폴백: " + e.Message);
            }
        }

        /// <summary>정적 캐시 null 가드 — 최초 1회만 텍스처 생성(선례: IndoorMaterialFactory._blobShadowTex).</summary>
        private static void EnsureCache()
        {
            if (_floorColorTex == null)
            {
                _floorColorTex = CreateColorTexture(ColorTexSize, FloorTone, FloorSeed, "CaveFloorColorTex");
                _floorNormalTex = CreateNormalTexture(NormalTexSize, FloorSeed, "CaveFloorNormalTex");
            }
            if (_wallColorTex == null)
            {
                _wallColorTex = CreateColorTexture(ColorTexSize, WallTone, WallSeed, "CaveWallColorTex");
                _wallNormalTex = CreateNormalTexture(NormalTexSize, WallSeed, "CaveWallNormalTex");
            }
        }

        /// <summary>컬러 텍스처 — 기본 톤×(0.85+0.30·fbm) 암반 음영 + 고주파 균열줄(~4% 0.75배). linear=false.</summary>
        private static Texture2D CreateColorTexture(int size, Color tone, int seed, string texName)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear: false)
            {
                name = texName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float shade = 0.85f + 0.30f * Fbm(x * 0.06f, y * 0.06f, seed);
                    if (Hash01(x, y, seed + 7) > 0.96f)   // 고주파 균열줄 — ~4% 픽셀
                        shade *= 0.75f;
                    px[y * size + x] = new Color(
                        Mathf.Clamp01(tone.r * shade),
                        Mathf.Clamp01(tone.g * shade),
                        Mathf.Clamp01(tone.b * shade),
                        1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);   // mipmap 포함
            return tex;
        }

        /// <summary>노멀 텍스처 — 높이장=fbm, sobel 미분 → normal=(−dx·k, −dy·k, 1) 정규화, RGB=n·0.5+0.5. linear=true.</summary>
        private static Texture2D CreateNormalTexture(int size, int seed, string texName)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear: true)
            {
                name = texName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            // 높이장 사전 계산(sobel 이웃 샘플용).
            float[] h = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    h[y * size + x] = Fbm(x * 0.06f, y * 0.06f, seed);

            const float k = 2.0f;   // 법선 기울기 강도
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int yUp = (y + 1) % size;
                int yDn = (y - 1 + size) % size;
                for (int x = 0; x < size; x++)
                {
                    int xL = (x - 1 + size) % size;
                    int xR = (x + 1) % size;
                    // sobel — wrap 이웃(타일 경계 연속).
                    float dx = (h[yDn * size + xR] + 2f * h[y * size + xR] + h[yUp * size + xR])
                             - (h[yDn * size + xL] + 2f * h[y * size + xL] + h[yUp * size + xL]);
                    float dy = (h[yUp * size + xL] + 2f * h[yUp * size + x] + h[yUp * size + xR])
                             - (h[yDn * size + xL] + 2f * h[yDn * size + x] + h[yDn * size + xR]);
                    Vector3 n = new Vector3(-dx * k, -dy * k, 1f).normalized;
                    px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // ================================================================
        // 결정론 노이즈 (Random API 0건 — 순수 함수)
        // ================================================================

        /// <summary>정수 혼합 해시 → [0,1]. 곱 오버플로는 의도적 랩(결정론 유지).</summary>
        // ================================================================
        // [158차 P3-C] 높이장 빌더 — 마스너리/받침/돌판/자갈(결정론·유니티 Random 0건)
        // ================================================================

        /// <summary>마스너리 높이장 — 가로 코스 8단(64px), 세로 블록 교차 스태거(128px, 홀수 코스 반블록 오프셋),
        ///   줄눈 4px 그루브(경계 거리 감쇠 — [165차 P2] 그루브 하한 0.28→0.18 심화), 블록별 해시 요철 지터
        ///   + 다옥타브 입면(굵은 fbm + 고주파 미세 노이즈 — [165차 P2] 표면 입체감 강화).
        ///   세로 tiling 1 전제(3m=512px). 블록 id는 랩 연속(스태거 봉합 블록 동일 id).</summary>
        private static float[] BuildMasonryHeightField(int size, int seed)
        {
            var h = new float[size * size];
            float coursePx = size / 8f;
            float brickPx = size / 4f;
            int blocks = Mathf.RoundToInt(size / brickPx);
            for (int y = 0; y < size; y++)
            {
                int course = Mathf.FloorToInt(y / coursePx);
                float coursePhase = (course % 2 == 1) ? brickPx * 0.5f : 0f;
                for (int x = 0; x < size; x++)
                {
                    float bx = ((x + coursePhase) % brickPx) / brickPx;
                    float by = (y % coursePx) / coursePx;
                    float joint = Mathf.Min(
                        Mathf.Clamp01(Mathf.Min(bx, 1f - bx) * brickPx / 4f),
                        Mathf.Clamp01(Mathf.Min(by, 1f - by) * coursePx / 4f));
                    int blockX = Mathf.FloorToInt((x + coursePhase) / brickPx) % blocks;   // 랩 연속 블록 id
                    float jitter = 0.90f + 0.20f * Hash01(blockX, course, seed + 5);       // [165차 P2] 블록별 요철 편차
                    float face = 0.70f
                               + 0.22f * Fbm(x * 0.05f, y * 0.05f, seed)
                               + 0.08f * Fbm(x * 0.16f, y * 0.16f, seed + 23);   // [165차 P2] 고주파 미세 입면 추가
                    h[y * size + x] = face * jitter * (0.18f + 0.82f * joint);   // [165차 P2] 그루브 심화(0.28→0.18)
                }
            }
            return h;
        }

        /// <summary>받침 높이장 — 고주파 fbm 거친 플린스(줄눈 없음 — 매끈 기저 대비).</summary>
        private static float[] BuildBaseBandHeightField(int size, int seed)
        {
            var h = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    h[y * size + x] = 0.55f + 0.45f * Fbm(x * 0.09f, y * 0.09f, seed);
            return h;
        }

        /// <summary>돌판 높이장 — 2×2 슬랩(256px) 경계 그라우트 6px 그루브 + 슬랩별 톤 지터(해시) + fbm 입면.</summary>
        private static float[] BuildStonePathHeightField(int size, int seed)
        {
            var h = new float[size * size];
            float slabPx = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sx = x % slabPx;
                    float sy = y % slabPx;
                    float edge = Mathf.Min(Mathf.Min(sx, slabPx - sx), Mathf.Min(sy, slabPx - sy));
                    float grout = Mathf.Clamp01(edge / 6f);
                    float jitter = 0.90f + 0.20f * Hash01(Mathf.FloorToInt(x / slabPx), Mathf.FloorToInt(y / slabPx), seed);
                    float face = 0.75f + 0.25f * Fbm(x * 0.06f, y * 0.06f, seed);
                    h[y * size + x] = face * jitter * Mathf.Lerp(0.30f, 1f, grout);
                }
            }
            return h;
        }

        /// <summary>자갈 높이장 — 32px 격자 잔돌 원(해시 지터·반경 7~11px) + 기저 fbm, 최대값 병합.</summary>
        private static float[] BuildGravelHeightField(int size, int seed)
        {
            var h = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    h[y * size + x] = 0.35f + 0.25f * Fbm(x * 0.08f, y * 0.08f, seed);
            int cells = size / 32;
            for (int gy = 0; gy < cells; gy++)
            {
                for (int gx = 0; gx < cells; gx++)
                {
                    float ox = gx * 32f + 8f + Hash01(gx, gy, seed) * 16f;
                    float oy = gy * 32f + 8f + Hash01(gx + 91, gy, seed) * 16f;
                    float r = 7f + Hash01(gx + 37, gy + 13, seed) * 4f;
                    for (int dy = -12; dy <= 12; dy++)
                    {
                        int y = Mathf.RoundToInt(oy) + dy;
                        if (y < 0 || y >= size) continue;
                        for (int dx = -12; dx <= 12; dx++)
                        {
                            int x = Mathf.RoundToInt(ox) + dx;
                            if (x < 0 || x >= size) continue;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d > r) continue;
                            float pebble = Mathf.Clamp01((r - d) / r);
                            float val = 0.55f + 0.45f * pebble;
                            int idx = y * size + x;
                            if (val > h[idx]) h[idx] = val;
                        }
                    }
                }
            }
            return h;
        }

        // ================================================================
        // [165차 P1/P2] 던전 흙 + 마스너리 고품질 빌더 — 결정론(Hash01/Fbm 순수 함수, Random API 0건)
        // ================================================================

        /// <summary>[165차 P1] 던전 흙 높이장 — 농밭 경준선 밴드 없음. 구성: 흙 베이스 다옥타브 fbm
        ///   (굵은 기복 + 중간 덩어리 + 미세 입자 = 실효 6옥타브) + 잔자갈 스펙클(고주파 해시 돌기 상위 2.5%)
        ///   + 미세 균열선(리지드 노이즈 좁은 밴드 함몰) + 마모 얼룩(저주파 패치 표면 완만화).</summary>
        private static float[] BuildDirtFloorHeightField(int size, int seed)
        {
            var h = new float[size * size];
            float s = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x * s, fy = y * s;
                    float v = Fbm(fx * 6f, fy * 6f, seed)
                            + 0.5f * Fbm(fx * 13f, fy * 13f, seed + 17)
                            + 0.25f * Fbm(fx * 27f, fy * 27f, seed + 53);
                    v /= 1.75f;   // [0,1] 정규화(가중합 1.75)
                    float sp = Hash01(x / 4, y / 4, seed + 29);                // 잔자갈 스펙클 — 4텍셀 블롭(단일 텍셀 → 미니피케이션 에일리어싱 대각 줄무늬 결함 수리)
                    if (sp > 0.975f) v += 0.18f * (sp - 0.975f) * 40f;
                    float crack = Mathf.Abs(Fbm(fx * 9f, fy * 9f, seed + 41) - 0.5f);   // 미세 균열선 — 리지드 밴드
                    if (crack < 0.02f) v -= 0.22f * (1f - crack * 50f);
                    float wear = Fbm(fx * 2.2f, fy * 2.2f, seed + 67);         // 마모 얼룩 — 저주파 완만화
                    v = Mathf.Lerp(v, 0.5f + 0.3f * (wear - 0.5f), 0.35f * wear);
                    h[y * size + x] = Mathf.Clamp01(v);
                }
            }
            return h;
        }

        /// <summary>[165차 P1] 던전 흙 컬러 — 높이장 음영 + 저주파 온도 모틀(웜/쿨 편차, 적색·청색 채널 반대편 가중)
        /// + 잔자갈 스펙클 회색 기운(돌 입자 구분). 경준선 밴드 없음(농밭 디자인 배제). mipmap, linear:false.</summary>
        private static Texture2D CreateDirtFloorColorTexture(float[] h, int size, Color tone, int seed, string texName)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear: false)
            {
                name = texName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,   // [165차] 미피 블렌드 — 축소 샘플링 에일리어싱 억제
                anisoLevel = 8,                      // [165차] 탑다운 저각 축소 대응
            };
            var px = new Color[size * size];
            float s = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float fx = x * s, fy = y * s;
                    float shade = 0.72f + 0.50f * h[i];
                    float mottle = Fbm(fx * 3.1f, fy * 3.1f, seed + 83) - 0.5f;   // 온도 모틀
                    float r = tone.r * shade * (1f + 0.12f * mottle);
                    float g = tone.g * shade * (1f + 0.07f * mottle);
                    float b = tone.b * shade * (1f - 0.07f * mottle);
                    float sp = Hash01(x / 4, y / 4, seed + 29);                   // 높이장과 동일 해시(4텍셀 블롭) — 요철·스펙클 일치
                    if (sp > 0.975f)
                    {
                        float t = (sp - 0.975f) * 40f;
                        r = Mathf.Lerp(r, r * 1.25f + 0.05f, t);
                        g = Mathf.Lerp(g, g * 1.25f + 0.05f, t);
                        b = Mathf.Lerp(b, b * 1.25f + 0.08f, t);   // 잔자갈 — 약간 밝은 회색 기운
                    }
                    px[i] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>[165차 P2] 마스너리 컬러 — 높이장 음영 + 블록별 해시 톤 편차(GenerateStoneWall StableTileHash
        /// 패턴 준용 — Hash01 블록 좌표, 높이장 jitter와 동일 해시로 요철·톤 일치) + 픽셀 그레인
        /// + 마모/이끼 얼룩 스탬프(줄눈·블록 엣지 인접 우선 가중, 이끼는 하단 코스 가중 — 습기 상승 암반).
        /// 결정론(순수 함수). mipmap, linear:false.</summary>
        private static Texture2D CreateMasonryColorTexture(float[] h, int size, Color tone, int seed, string texName)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear: false)
            {
                name = texName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[size * size];
            float coursePx = size / 8f;
            float brickPx = size / 4f;
            int blocks = Mathf.RoundToInt(size / brickPx);
            for (int y = 0; y < size; y++)
            {
                int course = Mathf.FloorToInt(y / coursePx);
                float coursePhase = (course % 2 == 1) ? brickPx * 0.5f : 0f;
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float bx = ((x + coursePhase) % brickPx) / brickPx;
                    float by = (y % coursePx) / coursePx;
                    float joint = Mathf.Min(
                        Mathf.Clamp01(Mathf.Min(bx, 1f - bx) * brickPx / 4f),
                        Mathf.Clamp01(Mathf.Min(by, 1f - by) * coursePx / 4f));
                    int blockX = Mathf.FloorToInt((x + coursePhase) / brickPx) % blocks;   // 랩 연속 블록 id
                    float blockTone = 0.90f + 0.20f * Hash01(blockX, course, seed + 5);    // 블록별 톤 편차
                    float edge = 1f - joint;
                    float wear = Mathf.Clamp01((Fbm(x * 0.035f, y * 0.035f, seed + 31) - 0.52f) * 6f)
                               * (0.35f + 0.65f * edge);                                   // 마모 — 엣지 인접 우선
                    float moss = Mathf.Clamp01((Fbm(x * 0.05f, y * 0.05f, seed + 47) - 0.60f) * 8f)
                               * (0.15f + 0.85f * edge) * (course <= 2 ? 1.5f : 1f);       // 이끼 — 엣지 + 하단 코스 가중
                    float grain = 0.96f + 0.08f * Hash01(x, y, seed + 61);                 // 픽셀 미세 그레인
                    float shade = (0.78f + 0.44f * h[i]) * blockTone * (1f - 0.18f * wear) * grain;
                    var c = new Color(
                        Mathf.Clamp01(tone.r * shade),
                        Mathf.Clamp01(tone.g * shade),
                        Mathf.Clamp01(tone.b * shade), 1f);
                    var mossColor = new Color(
                        Mathf.Clamp01(0.30f * shade),
                        Mathf.Clamp01(0.40f * shade),
                        Mathf.Clamp01(0.20f * shade), 1f);
                    px[i] = Color.Lerp(c, mossColor, Mathf.Clamp01(moss) * 0.65f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>높이장 → 컬러 텍스처 — shade=0.78+0.44·h, tone×shade(저수위=줄눈 어둡게). mipmap, linear:false.</summary>
        private static Texture2D CreateColorFromHeight(float[] h, int size, Color tone, string texName)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear: false)
            {
                name = texName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                float shade = 0.78f + 0.44f * h[i];
                px[i] = new Color(
                    Mathf.Clamp01(tone.r * shade),
                    Mathf.Clamp01(tone.g * shade),
                    Mathf.Clamp01(tone.b * shade), 1f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>높이장 → 노멀 텍스처 — sobel 미분(랩 이웃), RGB=n·0.5+0.5. linear:true, 그루브 요철 반영.</summary>
        private static Texture2D CreateNormalFromHeight(float[] h, int size, string texName)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear: true)
            {
                name = texName,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,   // [165차] 미피 블렌드 — 노멀 축소 에일리어싱 억제
                anisoLevel = 4,
            };
            var px = new Color[size * size];
            const float k = 2.5f;
            for (int y = 0; y < size; y++)
            {
                int yUp = (y + 1) % size;
                int yDn = (y + size - 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int xL = (x + size - 1) % size;
                    int xR = (x + 1) % size;
                    float dx = (h[yDn * size + xR] + 2f * h[y * size + xR] + h[yUp * size + xR])
                             - (h[yDn * size + xL] + 2f * h[y * size + xL] + h[yUp * size + xL]);
                    float dy = (h[yUp * size + xL] + 2f * h[yUp * size + x] + h[yUp * size + xR])
                             - (h[yDn * size + xL] + 2f * h[yDn * size + x] + h[yDn * size + xR]);
                    var n = new Vector3(-dx * k, -dy * k, 1f).normalized;
                    px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true, false);   // [165차] 미피 생성 — 축소 샘플링 대응
            return tex;
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393) + (uint)(y * 668265263) + (uint)(seed * 974634211);
                h ^= h >> 13; h *= 1274126177u;   // 셔프트+곱 1회차
                h ^= h >> 16; h *= 1103515245u;   // 셔프트+곱 2회차
                return (int)((h ^ (h >> 16)) & 0x7fffffffu) / 2147483647f;
            }
        }

        /// <summary>값 노이즈 — 격자 4모서리 Hash01 + smoothstep(t·t·(3−2t)) bilinear.</summary>
        private static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float tx = x - xi;
            float ty = y - yi;
            float sx = tx * tx * (3f - 2f * tx);
            float sy = ty * ty * (3f - 2f * ty);
            float a = Hash01(xi, yi, seed);
            float b = Hash01(xi + 1, yi, seed);
            float c = Hash01(xi, yi + 1, seed);
            float d = Hash01(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        /// <summary>fBm — ValueNoise 2옥타브(진폭 1.0+0.5), 합 1.5로 정규화 → [0,1].</summary>
        private static float Fbm(float x, float y, int seed)
        {
            float n = ValueNoise(x, y, seed)
                    + 0.5f * ValueNoise(x * 2.03f + 11.7f, y * 2.03f + 5.9f, seed + 71);
            return n / 1.5f;
        }
    }
}
