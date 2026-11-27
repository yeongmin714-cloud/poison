using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [무번호 29-D1] 절차 플립북 생성기 — 연기/불꽃 플립북 아틀라스를 시드 고정 절차 노이즈로 생성.
    /// 결정론 계약: 난수 API 0건(해시 기반 value noise — 156차 GetStarTexture 선례),
    /// 생성 1회 후 정적 캐시(재생성 금지), mipChain=false(파티클 용도).
    /// [무번호 80-P1 갱신] 아크 전용 상수 분리 — 정상 아크 256px 셀·10프레임(4×4·1024×1024 pow2,
    ///   141차 표준 갱신 — 사용자 결정 Q1), 연기 플립북은 141차 표준(128px·4×2) 무변동.
    ///   크리 전용 아틀라스는 512px 셀(4×4·2048×2048 pow2) — B안 분리(사용자 결정 Q4).
    /// </summary>
    public static class FxFlipbookGenerator
    {
        private const int CellSize = 128;      // [무번호 80-P1] 연기 전용 — 141차 표준 계열(아크에서 분리됨)
        private const int Columns = 4;         // 연기 4×2 = 8슬롯, 사용 6프레임
        private const int Rows = 2;
        private const int Frames = 6;
        private const int Seed = 20261113;     // 연기 시드 고정 — 결정론(재현 가능한 아틀라스)

        // [무번호 80-P1] 정상 참격 아크 전용 상수 — 256px 셀·10프레임(4×4 그리드 1024×1024 pow2,
        //   16슬롯 중 10프레임 사용 — 미사용 6칸은 FillUnusedCells가 마지막 프레임 복제(79 선례).
        //   141차 표준 갱신(사용자 결정 Q1 — 256px를 아크 전용 예외가 아닌 생성 표준으로).
        private const int ArcCellSize = 256;
        private const int ArcColumns = 4;
        private const int ArcRows = 4;
        private const int ArcFrames = 10;
        private const int ArcSeed = 20261113;  // 37-S3 시드 승계 — 정상 아크 결정론 유지

        // [무번호 80-P1] 크리 전용 아틀라스(B안 분리) — 크리만 512px 셀(4×4 그리드 2048×2048 pow2),
        //   정상 아크와 동일 10프레임(페이싱 패리티 — frameOverTime 0→1 전순환 계약 76-S1).
        //   크리는 화면에서 크고 오래 보이는 이펙트라 4배 디테일을 셀 단위로 배정
        //   (정상 아크와 동일 그리드로 통합하면 정상까지 4배 메모리 낭비 — B안 근거).
        private const int CritCellSize = 512;
        private const int CritColumns = 4;
        private const int CritRows = 4;
        private const int CritFrames = 10;
        private const int CritSeed = 20261119; // 크리 전용 시드 — 정상 아크와 독립 결정론

        private static Texture2D _smoke;

        /// <summary>연기 플립북 아틀라스(4×2 그리드, 하단 6프레임 사용) — 1회 생성 후 캐시.</summary>
        public static Texture2D GetSmokeFlipbook()
        {
            if (_smoke != null) return _smoke;
            _smoke = Generate();
            return _smoke;
        }

        private static Texture2D _slashArc;

        /// <summary>[무번호 37-S3] 참격 아크 플립북 아틀라스(10프레임 — 80-P1 갱신 256px·4×4·1024×1024).
        ///   아크 휩쓸기: 스윕 각도가 프레임마다 진행(ease-out) + 코어-에지 이중 알파 + 노이즈 요철
        ///   (해시 FBM 결정론 — 연기 파이프라인 재사용).</summary>
        public static Texture2D GetSlashArcFlipbook()
        {
            if (_slashArc != null) return _slashArc;
            _slashArc = GenerateSlashArc();
            return _slashArc;
        }

        private static Texture2D _critArc;

        /// <summary>[무번호 80-P1] 크리 전용 참격 아크 아틀라스(B안 분리 — 4×4·2048×2048, 512px 셀, 캐시 1회).
        ///   크리 스윕: −75°→+75°(정상 ±60°보다 넓은 휩쓸기), 코어 20°(정상 14° 위계), 장반경(정상 0.44 → 0.64) —
        ///   정상 아크와 다른 형상 위계(70-U3 갱신 79-R20: 색은 통일, 크기·모양으로만 위계). 결정론(크리 전용 시드).</summary>
        public static Texture2D GetCritSlashArcFlipbook()
        {
            if (_critArc != null) return _critArc;
            _critArc = GenerateCritSlashArc();
            return _critArc;
        }

        /// <summary>[무번호 80-P1] 크리 아틀라스 생성 — 정상 아크와 동일 구조(코어-에지·노이즈 요철),
        ///   형상 파라미터만 위계. 10프레임 — 정상과 동일 페이싱.</summary>
        private static Texture2D GenerateCritSlashArc()
        {
            int w = CritCellSize * CritColumns, h = CritCellSize * CritRows;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];
            for (int f = 0; f < CritFrames; f++)
            {
                int cx = (f % CritColumns) * CritCellSize;
                int cy = (CritRows - 1 - (f / CritColumns)) * CritCellSize;   // top-down 행 순서(29-D1 정합)
                float t = f / (float)(CritFrames - 1);
                float e = 1f - (1f - t) * (1f - t);   // ease-out — 후반 감속(칼날 스윕 실제 가속 곡선)
                for (int y = 0; y < CritCellSize; y++)
                {
                    for (int x = 0; x < CritCellSize; x++)
                    {
                        float u = x / (float)(CritCellSize - 1) - 0.5f;   // 셀 중심 원점
                        float v = y / (float)(CritCellSize - 1) - 0.5f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float ang = Mathf.Atan2(v, u) * Mathf.Rad2Deg;   // -180..180
                        // 스윕 창: -75°→+75° 진행(ease-out), 코어 20° + 에지 페더 28°(정상 14°/20° 위계)
                        float sweep = Mathf.Lerp(-75f, 75f, e);
                        float dAng = Mathf.Abs(Mathf.DeltaAngle(ang, sweep));
                        float core = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - dAng / 20f));
                        core *= core;
                        float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - dAng / 28f));
                        // 장반경 아크: 0.22~0.64 (캡 후보아 감쇠 — 정상 0.44보다 긴 반경 위계)
                        float rIn = Mathf.Lerp(0.22f, 0.12f, t);
                        float rOut = Mathf.Lerp(0.64f, 0.44f, t);
                        // 노이즈 결(연기 파이프라인 동일 FBM — 크리 전용 시드) + 반경 요철(칼날 느낌)
                        float n = Fbm(u * 6f + CritSeed * 0.0001f, v * 6f, CritSeed + f * 31);
                        float wob = 0.12f * (n - 0.5f);
                        float ring = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rOut + wob - r)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(r - rIn));
                        float a = Mathf.Clamp01((core * 0.85f + edge * 0.35f) * ring * (0.55f + 0.75f * n) * (1f - 0.25f * t));
                        byte alpha = (byte)(a * 255f);
                        px[(cy + y) * w + (cx + x)] = new Color32(255, 244, 214, alpha);   // 웜 화이트(틴트는 머티리얼 담당 — 79-R20 통일색)
                    }
                }
            }
            FillUnusedCells(px, w, CritCellSize, CritColumns, CritRows, CritFrames);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>크리 아틀라스 열 수(플립북 UV 분할용).</summary>
        public static int CritAtlasColumns => CritColumns;
        /// <summary>크리 아틀라스 행 수.</summary>
        public static int CritAtlasRows => CritRows;

        private static Texture2D GenerateSlashArc()
        {
            int w = ArcCellSize * ArcColumns, h = ArcCellSize * ArcRows;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];
            for (int f = 0; f < ArcFrames; f++)
            {
                int cx = (f % ArcColumns) * ArcCellSize;
                int cy = (ArcRows - 1 - (f / ArcColumns)) * ArcCellSize;   // top-down 행 순서(29-D1 정합)
                float t = f / (float)(ArcFrames - 1);
                // [무번호 80-P1] 스윕 진행 ease-out — 후반 감속(칼날 스윕 실제 가속 곡선 정합)
                float e = 1f - (1f - t) * (1f - t);
                for (int y = 0; y < ArcCellSize; y++)
                {
                    for (int x = 0; x < ArcCellSize; x++)
                    {
                        float u = x / (float)(ArcCellSize - 1) - 0.5f;   // 셀 중심 원점
                        float v = y / (float)(ArcCellSize - 1) - 0.5f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float ang = Mathf.Atan2(v, u) * Mathf.Rad2Deg;   // -180..180
                        // 스윕 창: -60°→+60° 진행(ease-out), 코어-에지 이중 알파 —
                        //   중심 코어 14°(고알파) + 외곽 에지 20°(저알파 페더). RGB는 단일색(79-R20 계약), 모양만 이중화.
                        float sweep = Mathf.Lerp(-60f, 60f, e);
                        float dAng = Mathf.Abs(Mathf.DeltaAngle(ang, sweep));
                        float core = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - dAng / 14f));
                        core *= core;
                        float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - dAng / 20f));
                        // 반경 아크: 0.18~0.44 (캡 후보아 감쇠 — t에 따라 안쪽 반경 축소)
                        float rIn = Mathf.Lerp(0.18f, 0.10f, t);
                        float rOut = Mathf.Lerp(0.44f, 0.30f, t);
                        // 노이즈 결(연기 파이프라인 동일 FBM) + 반경 요철(칼날 느낌 — 계획 Task 1.2)
                        float n = Fbm(u * 6f + ArcSeed * 0.0001f, v * 6f, ArcSeed + f * 31);
                        float wob = 0.12f * (n - 0.5f);
                        float ring = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rOut + wob - r)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(r - rIn));
                        float a = Mathf.Clamp01((core * 0.85f + edge * 0.35f) * ring * (0.55f + 0.75f * n) * (1f - 0.25f * t));
                        byte alpha = (byte)(a * 255f);
                        px[(cy + y) * w + (cx + x)] = new Color32(255, 244, 214, alpha);   // 웜 화이트(참격 궤적 톤 — 79-R20 통일색 틴트는 머티리얼 담당)
                    }
                }
            }
            FillUnusedCells(px, w, ArcCellSize, ArcColumns, ArcRows, ArcFrames);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>정상 아크 프레임 수(4×4 그리드 중 사용 프레임 — 80-P1 갱신).</summary>
        public static int ArcFrameCount => ArcFrames;
        /// <summary>정상 아크 아틀라스 열 수(플립북 UV 분할용).</summary>
        public static int ArcAtlasColumns => ArcColumns;
        /// <summary>정상 아크 아틀라스 행 수.</summary>
        public static int ArcAtlasRows => ArcRows;

        /// <summary>연기 프레임 수(4×2 그리드 중 사용 프레임 — 141차 표준 무변동).</summary>
        public static int FrameCount => Frames;
        /// <summary>연기 아틀라스 열 수(플립북 UV 분할용 — DeathVFX 소비).</summary>
        public static int AtlasColumns => Columns;
        /// <summary>연기 아틀라스 행 수.</summary>
        public static int AtlasRows => Rows;

        private static Texture2D Generate()
        {
            int w = CellSize * Columns, h = CellSize * Rows;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[w * h];
            for (int f = 0; f < Frames; f++)
            {
                int cx = (f % Columns) * CellSize;
                int cy = (Rows - 1 - (f / Columns)) * CellSize;   // [무번호 29-D1] 위→아래 프레임 순서(Unity 시트 애니 row-major top-down 정합)
                float t = f / (float)(Frames - 1);       // 0→1 진행: 상승+확산
                for (int y = 0; y < CellSize; y++)
                {
                    for (int x = 0; x < CellSize; x++)
                    {
                        // 셀 로컬 uv(0..1), 아래=0
                        float u = x / (float)(CellSize - 1);
                        float v = y / (float)(CellSize - 1);
                        // 상승 드리프트: 노이즈 샘플 y를 t만큼 아래에서 끌어올림
                        float n = Fbm(u * 3.0f + Seed * 0.0001f, v * 3.0f - t * 1.2f, Seed + f * 17);
                        // 기둥 프로파일: 수직 중심선(u≈0.5) 가우시안 — t에 따라 확산
                        float spread = 0.18f + 0.30f * t;
                        float pillar = Mathf.Exp(-((u - 0.5f) * (u - 0.5f)) / (2f * spread * spread));
                        // 상단 감쇠(연기 꼬리) + 하단 약간 유지
                        float vert = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - 0.06f) / 0.94f));
                        vert *= 1f - 0.35f * t;              // 시간 경과 밀도 감소
                        float a = Mathf.Clamp01(n * 1.4f * pillar * vert);
                        // 연기 회백색(알파 소프트)
                        byte alpha = (byte)(a * 255f);
                        var c = new Color32(232, 232, 232, alpha);
                        px[cy + y < h && cx + x < w ? (cy + y) * w + (cx + x) : 0] = c;
                    }
                }
            }
            FillUnusedCells(px, w, CellSize, Columns, Rows, Frames);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>[무번호 79] 미사용 셀 보존 — frameOverTime 0→1 전순환(76-S1 계약)이 수명 말 미기록 셀을
        ///   샘플한다(79-R4 실측: slashArc 셀 6·7 중심 (0,0,0,0)). 마지막 프레임을 복제해 전순환에서도 형상 유지.</summary>
        private static void FillUnusedCells(Color32[] px, int w, int cellSize, int columns, int rows, int frames)
        {
            for (int f = frames; f < columns * rows; f++)
            {
                int sx = ((frames - 1) % columns) * cellSize;
                int sy = (rows - 1 - ((frames - 1) / columns)) * cellSize;
                int cx = (f % columns) * cellSize;
                int cy = (rows - 1 - (f / columns)) * cellSize;
                for (int y = 0; y < cellSize; y++)
                    for (int x = 0; x < cellSize; x++)
                        px[(cy + y) * w + (cx + x)] = px[(sy + y) * w + (sx + x)];
            }
        }

        /// <summary>3옥타브 FBM value noise — 해시 기반 결정론(Random 0건).</summary>
        private static float Fbm(float x, float y, int seed)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int o = 0; o < 3; o++)
            {
                sum += amp * ValueNoise(x * freq, y * freq, seed + o * 131);
                amp *= 0.5f; freq *= 2.07f;   // 비정수 주파수 — 격자 아티팩트 회피
            }
            return sum;
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float su = fx * fx * (3f - 2f * fx);   // smoothstep 보간
            float sv = fy * fy * (3f - 2f * fy);
            float n00 = Hash(ix, iy, seed), n10 = Hash(ix + 1, iy, seed);
            float n01 = Hash(ix, iy + 1, seed), n11 = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, su), Mathf.Lerp(n01, n11, su), sv);
        }

        /// <summary>정수 격자 해시 — 결정론(동일 입력=동일 출력, Random 0건).</summary>
        private static float Hash(int ix, int iy, int seed)
        {
            float s = Mathf.Sin(ix * 127.1f + iy * 311.7f + seed * 0.113f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }
    }
}
