using System.Collections.Generic;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// [153차 P1] 농경 시각부 전담 정적 클래스 — FarmPlot의 시각 생성/갱신을 위임 분리
    /// (docs/153_FARMING_QUALITY_ACQ_VFX_PLAN.md §3 P1/P3 — 26차 "분리 권장" 이행).
    ///
    /// 품질 요소:
    ///  - 절차 텍스처: 밭흙 베이스(갈색 0.42,0.29,0.16 계열)+입자 노이즈+경준선(furrow) 정현파 밴드
    ///    + 대응 normal — static 캐시, 고정 시드 결정론(에셋 파일 금지 계약 — 코드 생성).
    ///  - Tilled 구멍: Rim 4Cube+Pit 단일 Cube 폐기 → 단일 프로시저럴 격자 메시(중앙 ~5×5 정점 내림
    ///    최대 −0.08m, 내벽 경사, 흙둑 rim 비대칭 높이, 정점색 AO) — 탑다운 음영 판독이 품질 핵심.
    ///    [154차 P2] 검정 렌더 루트 수리: RecalculateTangents(탄젠트 부재+_NORMALMAP = TBN 퇴화 = 직사광 소실 검정)
    ///    + 구멍 루트 상승 2mm(사용자 승인) + 그림자 수신 해제(보험) + AO 프리멀티 텍스처 전환
    ///    (정점색 AO는 URP/Lit이 무시 — 죽은 데이터 → 텍스처 명암으로 이식).
    ///  - 수분 가시화: Planted~Ready 흙길 밝기 = ComputeSoilBrightness(MoistureFraction) 보간
    ///    (촉촉 ×0.75 어둡 ↔ 마름 ×1.1 밝음 — 수분 바 없이도 밭 상태 판독).
    ///  - 작물 GLB 승격 [153차 P3]: HerbType→herb_*.glb(Resources — resolver 선례 동일 매핑),
    ///    실패 시 기존 Sphere 폴백(연출 중단 없음). 성장 스케일 계약(0.12→0.4→0.8) 유지 — GLB는
    ///    bounds 최대차원 정규화 후 피벗 로컬스케일 곱(AcquisitionCinema.NormalizeScale 수학 참고).
    ///    시듦/부패 틴트는 전 renderer material 인스턴스화 후 적용(공유 머티리얼 오염 금지 —
    ///    CacheFloatRenderers 선례).
    ///  - 밭 타일 팩 승격 [무번호 182-P2]: Farm Pack Field_1_1 GLB(Resources.UserProvided)로 흙 타일 승격 —
    ///    Grass=기존 잔디 큐브 유지, Tilled=큐브·팩 타일 모두 비표시(구멍만), Dirt~Rotted=팩 타일 우선 표시.
    ///    팩 모델 부재(Resources 로드 실패) 시 전 상태 기존 큐브 경로 폴백(연출 중단 없음).
    ///  - 바람 스웨이: Growing/Ready 작물 소진폭 사인(0.7~1.5도) — FarmPlot.Update에서 Tick 1줄 훅.
    ///    ResourceNode.ApplyWoodSway 수학 재사용(원본 파일 미수정).
    ///
    /// 계약 보존: FarmPlot 상태머신/E키/채널/수분·성장 판정 로직 무변경, 구멍은 Tilled에서만
    /// SetActive, 플롯 시각 깊이 −0.1m 이내, 시각 전용(콜라이더 파괴).
    /// </summary>
    public static class FarmPlotVisuals
    {
        // ===== [153차 P1] 절차 밭흙 텍스처 — static 캐시 1회 생성, 고정 시드 결정론 =====

        private const int SoilTextureSize = 256;
        private const int SoilTextureSeed = 153153;   // 고정 시드 — 153차 마커(결정론 계약)
        private const float FurrowRowCount = 5f;      // 타일 세로 기준 경준선 줄 수
        private const float FurrowWaveHz = 1.5f;      // 밴드 위상 흔들림(타일 내 사인 주기)
        private const float FurrowWaveAmp = 0.35f;    // 밴드 흔들림 진폭(줄 단위)
        private const float FurrowDepth = 0.13f;      // 이랑/고랑 밝기 대비
        private const float GrainStrength = 0.09f;    // 입자 노이즈 대비
        private const float SoilNormalStrength = 0.5f;// normal 경사 강도("간단 생성" 계약)

        /// <summary>밭흙 베이스색 — 기존 Dirt/Tilled 단색(GetSoilColor)과 동일 톤. 텍스처 평균이 이 값에
        ///   정규화되어 흰 배수 적용 시 기존 단색 외관과 연속성을 유지한다.</summary>
        public static readonly Color SoilBaseColor = new Color(0.42f, 0.29f, 0.16f, 1f);

        private static Texture2D _soilTexture;   // 밭흙 베이스+노이즈+경준선(캐시 — 결정론)
        private static Texture2D _soilNormal;    // 경준선 대응 normal(캐시)

        /// <summary>[153차 P1] 밭흙 절차 텍스처 — 첫 호출에서 생성 후 static 캐시(동일 인스턴스 반환 — 결정론).
        ///   public static — EditMode 직접 판정용.</summary>
        public static Texture2D GetOrCreateSoilTexture()
        {
            if (_soilTexture == null) _soilTexture = CreateSoilTexture();
            return _soilTexture;
        }

        /// <summary>[153차 P1] 경준선 대응 normal — 밭흙 텍스처와 같은 캐시 계약(고정 시드 결정론).</summary>
        public static Texture2D GetOrCreateSoilNormal()
        {
            if (_soilNormal == null) _soilNormal = CreateSoilNormal();
            return _soilNormal;
        }

        /// <summary>밭흙 텍스처 생성 — 베이스 갈색+입자 노이즈+경준선 정현파 밴드. 채널 평균을 베이스색에
        ///   정규화해 흰 배수 적용 시 기존 단색(GetSoilColor)과 동일 밝기(연속성 계약).</summary>
        private static Texture2D CreateSoilTexture()
        {
            int size = SoilTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "FarmSoil_Procedural",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };

            var rnd = new System.Random(SoilTextureSeed);   // 고정 시드 — 픽셀 순회 순서 고정(결정론)
            var base32 = (Color32)SoilBaseColor;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);

                    // 경준선(furrow) — 정현파 밴드: 밴드 축 위상에 사인파를 얹어 직선 rim의 기계적 느낌을 완화.
                    // 탑다운에서 이랑(밝음)/고랑(어둠) 교차가 격차 판독으로 이어진다.
                    float band = v * FurrowRowCount + Mathf.Sin(u * 2f * Mathf.PI * FurrowWaveHz) * FurrowWaveAmp;
                    float furrow = Mathf.Sin(band * 2f * Mathf.PI);   // -1..1 밴드 프로파일

                    // 입자 노이즈 — 흙 알갱이 스페클: 대칭 흔들기로 평균 보존.
                    float n = (float)rnd.NextDouble();
                    float grain = (n - 0.5f) * GrainStrength;
                    if (n < 0.05f) grain -= 0.10f;                    // 어두운 돌 입자
                    else if (n > 0.95f) grain += 0.08f;               // 밝은 모래 입자

                    // 채널별 미세 편차 — 냉/온 톤 흔들기(갈색 가족 유지, 이랑은 냉기 있는 흙)
                    float rMod = 1f + furrow * FurrowDepth * 0.9f + grain;
                    float gMod = 1f + furrow * FurrowDepth * 1.0f + grain;
                    float bMod = 1f + furrow * FurrowDepth * 1.1f + grain;
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(base32.r * rMod), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(base32.g * gMod), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(base32.b * bMod), 0, 255),
                        255);
                }
            }

            // 평균 정규화 — 채널별 평균이 베이스색과 정확히 일치하도록 배수 보정(결정론).
            // [154차 P2-수리] 단위 혼용 수리 — 베이스색(0..1 float)을 바이트 스케일(×255)로 환산 후 비교.
            //   구 산식(0.42÷107≈0.0039)은 전 픽셀을 0으로 스케일링 — 흙 텍스처 전체가 검정이 됐다
            //   (실측: 인게임 굉이질 오목 (1,1,1), EditMode AO 텍스처 판독 0.0, 153차 프로브 무채색 — 전부 본 결함).
            Color32 avg = ComputeChannelAverage(pixels);
            var norm = new Vector3(
                SoilBaseColor.r * 255f / Mathf.Max(1f, avg.r),
                SoilBaseColor.g * 255f / Mathf.Max(1f, avg.g),
                SoilBaseColor.b * 255f / Mathf.Max(1f, avg.b));
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(pixels[i].r * norm.x), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(pixels[i].g * norm.y), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(pixels[i].b * norm.z), 0, 255),
                    255);

            tex.SetPixels32(pixels);
            tex.Apply(true);   // 밉 자동 생성(기존 외관 연속성 — 흰 배수 × 텍스처 평균 = 베이스색)
            return tex;
        }

        /// <summary>밭흙 normal 생성 — 경준선 높이장(텍스처와 동일 정현파 밴드 공식)을 중앙 차분으로
        ///   탄젠트 공간 normal로 변환하는 간단 생성("normal은 줄무늬 대응 간단 생성" 계약).</summary>
        private static Texture2D CreateSoilNormal()
        {
            int size = SoilTextureSize;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true)   // linear — normal 맵
            {
                name = "FarmSoil_Procedural_Normal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            // 높이장 — 텍스처와 같은 경준선 공식(위상 일치)
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float band = v * FurrowRowCount + Mathf.Sin(u * 2f * Mathf.PI * FurrowWaveHz) * FurrowWaveAmp;
                    height[y * size + x] = Mathf.Sin(band * 2f * Mathf.PI);
                }
            }

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 중앙 차분 기울기 → 탄젠트 공간 normal(이랑 볼록/고랑 오목 — 줄무늬 경사)
                    int xl = Mathf.Max(0, x - 1), xr = Mathf.Min(size - 1, x + 1);
                    int yl = Mathf.Max(0, y - 1), yr = Mathf.Min(size - 1, y + 1);
                    float dhdx = (height[y * size + xr] - height[y * size + xl]) / Mathf.Max(1, xr - xl);
                    float dhdy = (height[yr * size + x] - height[yl * size + x]) / Mathf.Max(1, yr - yl);
                    var n = new Vector3(-dhdx * SoilNormalStrength, -dhdy * SoilNormalStrength, 1f).normalized;
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 0, 255),
                        255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true);
            return tex;
        }

        /// <summary>채널 평균(Color32 정수 누산 — 결정론).</summary>
        private static Color32 ComputeChannelAverage(Color32[] pixels)
        {
            long r = 0, g = 0, b = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                r += pixels[i].r;
                g += pixels[i].g;
                b += pixels[i].b;
            }
            return new Color32(
                (byte)(r / pixels.Length),
                (byte)(g / pixels.Length),
                (byte)(b / pixels.Length),
                255);
        }

        // ===== [153차 P1] 수분 밝기 판정 =====

        /// <summary>촉촉(수분 1.0) 기본 흙색 배수 — 어둡게(갈수록 젖은 흙).</summary>
        public const float DampSoilBrightness = 0.75f;
        /// <summary>마름(수분 0) 기본 흙색 배수 — 밝게.</summary>
        public const float DrySoilBrightness = 1.1f;

        /// <summary>[153차 P1] 수분 → 흙 밝기 배수 — 촉촉(×0.75 어둡)과 마름(×1.1 밝음) 사이 선형 보간.
        ///   public static — EditMode 직접 판정용. 범위 밖 입력은 Clamp01로 경계 수렴.</summary>
        public static float ComputeSoilBrightness(float moisture)
        {
            float m = Mathf.Clamp01(moisture);
            return Mathf.Lerp(DrySoilBrightness, DampSoilBrightness, m);
        }

        // ===== [153차 P3] 작물 GLB 매핑 — AcquisitionItemVisualResolver 선례 동일 =====

        /// <summary>밭작물 GLB 루트(Resources 키) — UserProvided 위치 계약(resolver 선례).</summary>
        public const string CropGlbResourcesDir = "Models/UserProvided";

        /// <summary>[153차 P3] HerbType → GLB 키 결정론 — resolver ResolveHerbGlbKey 선례 동일 매핑
        ///   (Yellow↔herb_gold, Silver↔herb_blue — [130차] 계약 승계). 추정 불가 시 기본 herb_green.</summary>
        public static string ResolveCropGlbKey(HerbPickup.HerbType type)
        {
            switch (type)
            {
                case HerbPickup.HerbType.Red:    return "herb_red";
                case HerbPickup.HerbType.Purple: return "herb_purple";
                case HerbPickup.HerbType.Yellow: return "herb_gold";   // [130차] HerbType.Yellow ↔ GLB herb_gold
                case HerbPickup.HerbType.Silver: return "herb_blue";   // [130차] HerbType.Silver ↔ GLB herb_blue
                case HerbPickup.HerbType.Green:  return "herb_green";
                default:                         return "herb_green";  // resolver 선례 — 미상 타입 기본
            }
        }

        /// <summary>[153차 P3] HerbType → Resources 로드 경로 결정론("Models/UserProvided/herb_gold" 식).
        ///   public static — EditMode 직접 판정용(매핑 결정론 계약).</summary>
        public static string CropGlbResourcesPath(HerbPickup.HerbType type)
        {
            return CropGlbResourcesDir + "/" + ResolveCropGlbKey(type);
        }

        /// <summary>GLB 프리팹 로드 — Resources 단일 경로(UserProvided GLB는 Resources 하위 실측 존재).
        ///   실패 시 null → 호출처 폴백(연출 중단 없음 계약).</summary>
        private static GameObject LoadCropGlbPrefab(HerbPickup.HerbType type)
        {
            return Resources.Load<GameObject>(CropGlbResourcesPath(type));
        }

        // ===== [무번호 183-P3] Farm Pack 작물 단계 모델 매핑 — 약초 5종 ↔ 팩 식물(사용자 승인) =====

        /// <summary>[무번호 183-P3] HerbType → Farm Pack 단계 모델 Resources 키(어림→성숙 순).
        ///   성장 스왑 계약: Growing은 마지막(성숙) 단계에 도달하지 않는다 — 성숙 룩은 Ready에서만
        ///   등장(수확 신호 가독). 단일 모델 타입은 길이 1 — 기존 스케일 보간만으로 성장 표현.</summary>
        public static readonly string[] CropStages_Red    = { "TomatoPlant_1_A", "TomatoPlant_2_A" };
        public static readonly string[] CropStages_Green  = { "PotatoPlant_1", "PotatoPlant_2", "PotatoPlant_3" };
        public static readonly string[] CropStages_Purple = { "EggPlant_Plant" };
        public static readonly string[] CropStages_Yellow = { "CornPlant" };
        public static readonly string[] CropStages_Silver = { "Cabbage" };

        /// <summary>[무번호 183-P3] 단계 키 목록 결정론 — 미상 타입 null → 기존 herb GLB 폴백 경로.</summary>
        public static string[] ResolveCropStageGlbKeys(HerbPickup.HerbType type)
        {
            switch (type)
            {
                case HerbPickup.HerbType.Red:    return CropStages_Red;
                case HerbPickup.HerbType.Green:  return CropStages_Green;
                case HerbPickup.HerbType.Purple: return CropStages_Purple;
                case HerbPickup.HerbType.Yellow: return CropStages_Yellow;
                case HerbPickup.HerbType.Silver: return CropStages_Silver;
                default:                         return null;
            }
        }

        /// <summary>[무번호 183-P3] 단계 인덱스 결정론(순수 함수 — 26차 EditMode 표준).
        ///   비표시 상태(Grass/Dirt/Tilled)=0(최소 단계 선빌드 — 성장 스왑 트리거 방지) ·
        ///   Planted=0 · Growing=0..stageCount-2로 진행 스왑(마지막 단계 제외) ·
        ///   Ready/Withered/Rotted=stageCount-1(성숙 — 틴트는 기존 경로).</summary>
        public static int ComputeCropStageIndex(FarmPlot.CropPhase phase, float growProgress01, int stageCount)
        {
            if (stageCount <= 1) return 0;
            switch (phase)
            {
                case FarmPlot.CropPhase.Planted:
                case FarmPlot.CropPhase.Grass:
                case FarmPlot.CropPhase.Dirt:
                case FarmPlot.CropPhase.Tilled:
                    return 0;
                case FarmPlot.CropPhase.Growing:
                {
                    int idx = Mathf.FloorToInt(Mathf.Clamp01(growProgress01) * (stageCount - 1));
                    return Mathf.Clamp(idx, 0, stageCount - 2);
                }
                default:
                    return stageCount - 1;
            }
        }

        /// <summary>[무번호 183-P3] 단계 프리팹 로드 — 팩 모델 부재 시 null → 호출처가 기존 herb
        ///   GLB 폴백으로 접는다(연출 중단 없음 3단계: 팩 단계 → herb GLB → Sphere).</summary>
        private static GameObject LoadCropStagePrefab(HerbPickup.HerbType type, int stageIndex)
        {
            var keys = ResolveCropStageGlbKeys(type);
            if (keys == null || stageIndex < 0 || stageIndex >= keys.Length) return null;
            return Resources.Load<GameObject>(CropGlbResourcesDir + "/" + keys[stageIndex]);
        }

        /// <summary>[무번호 183-P3] 현재 플롯 목표 단계 — 순수 판정의 인스턴스 어댑터.</summary>
        private static int CurrentCropStageIndex(FarmPlot plot)
        {
            if (plot == null) return 0;
            var keys = ResolveCropStageGlbKeys(plot.CropType);
            return ComputeCropStageIndex(plot.Phase, plot.GrowProgress01(), keys?.Length ?? 1);
        }

        // ===== [153차 P1] Tilled 구멍 — 단일 프로시저럴 격자 메시(Rim 4Cube+Pit Cube 폐기) =====

        /// <summary>구멍 격자 해상도(9×9 — 중앙 ~5×5 영역이 내림 대상).</summary>
        public const int HoleGridSize = 9;
        /// <summary>중앙 최대 내림(m) — 플롯 시각 깊이 −0.1m 이내 계약 안(기존 흙타일 두께와 동일 수준).</summary>
        public const float HoleMaxDepth = 0.08f;
        /// <summary>흙둑(rim) 비대칭 높이 범위(m) — 파낸 흙이 불규칙하게 일어난 형태.</summary>
        public const float HoleBermMinHeight = 0.012f;
        public const float HoleBermMaxHeight = 0.032f;
        /// <summary>정점색 배수 — 내벽/바닥 짙은 갈색(0.22,0.14,0.08 톤) ↔ 둑 밝은 갈색(0.48,0.34,0.19 톤).</summary>
        private const float VertexMulFloor = 0.40f;   // 바닥(가장 어두움 — AO)
        private const float VertexMulWall = 0.45f;    // 내벽
        private const float VertexMulLip = 0.62f;     // 내벽 상단(경사면)
        private const float VertexMulBerm = 1f;       // 흙둑(가장 밝게 — 탑다운 음영 판독)
        private const float VertexMulEdge = 0.95f;    // 타일 가장자리(평탄 흙과 이음)
        private const float HoleMaterialMul = 1.12f;  // 재질 배수 — 정점색 배수와 곱해 스펙 톤에 도달
        private const float SkirtDepth = 0.1f;        // 치막 — 기존 흙타일 두께(−0.1m)와 동일 계약

        /// <summary>[154차 P2] 구멍 메시 루트 상승(m) — 사용자 승인 수리. 플롯 원점 = 지형+0.1이라 지면 매몰은
        ///   무관하나, 가장자리 링(y=0)이 지면/흙타일 상면과 동일 평면이던 케이스의 z-파이팅 보험(2mm).
        ///   public const — EditMode 직접 판정용 계약 상수.</summary>
        public const float HoleMeshLiftY = 0.002f;

        /// <summary>흙둑 비대칭 높이 — 정점 좌표 해시 결정론(입력 같으면 항상 같은 높이).</summary>
        private static float BermHeight(int i, int j)
        {
            int hash = (i * 73856093) ^ (j * 19349663) ^ SoilTextureSeed;
            float f = (hash & 0x3FF) / 1023f;   // 0..1
            return Mathf.Lerp(HoleBermMinHeight, HoleBermMaxHeight, f);
        }

        /// <summary>정점색 — 텍스처/재질에 곱해지는 명암 배수(내벽 어둡 ↔ 둑 밝음 — 탑다운 AO 판독).
        ///   [154차 P2] URP/Lit은 정점색을 무시(죽은 데이터 — H3 실측) — 실제 AO는 GetOrCreateHoleTexture
        ///   프리멀티 텍스처로 이관. 빌트인/기타 셰이더 호환용으로 정점색 세팅 자체는 유지(무해).</summary>
        private static Color VertexShadeColor(float mul)
        {
            return new Color(mul, mul, mul, 1f);
        }

        /// <summary>[153차 P1] Tilled 구멍 메시 결정론 빌드 — 9×9 격자(중앙 ~5×5 정점 내림 최대 −0.08m,
        ///   내벽 경사, 흙둑 rim은 정점 해시 비대칭 높이, 정점색 AO)+가장자리 치막(기존 흙타일 두께 −0.1m와 동일).
        ///   호출마다 새 Mesh(호출처 소유 — 캐시 없음). EditMode 직접 판정용 public static.</summary>
        public static Mesh BuildTillHoleMesh()
        {
            int size = HoleGridSize;
            float step = 1f / (size - 1);   // 타일 전체 1×1

            // 링(r) = 중심에서의 체비셰프 거리(사각 링 — 구 사각 pit 계승): 0..4
            var heights = new float[size, size];
            var colorMul = new float[size, size];
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    int ring = Mathf.Max(Mathf.Abs(i - size / 2), Mathf.Abs(j - size / 2));
                    switch (ring)
                    {
                        case 0:  heights[i, j] = -HoleMaxDepth; colorMul[i, j] = VertexMulFloor; break;        // 바닥
                        case 1:  heights[i, j] = -HoleMaxDepth * 0.95f; colorMul[i, j] = VertexMulWall; break; // 내벽 하단
                        case 2:  heights[i, j] = -HoleMaxDepth * 0.69f; colorMul[i, j] = VertexMulLip; break;  // 내벽 상단
                        case 3:  heights[i, j] = BermHeight(i, j); colorMul[i, j] = VertexMulBerm; break;      // 흙둑(비대칭)
                        default: heights[i, j] = 0f; colorMul[i, j] = VertexMulEdge; break;                    // 가장자리
                    }
                }
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var cols = new List<Color>();
            var tris = new List<int>();

            // 상면 정점 — y는 링 프로파일(중앙 내림)
            int[,] topIndex = new int[size, size];
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    topIndex[i, j] = verts.Count;
                    verts.Add(new Vector3(i * step - 0.5f, heights[i, j], j * step - 0.5f));
                    uvs.Add(new Vector2(i / (float)(size - 1), j / (float)(size - 1)));
                    cols.Add(VertexShadeColor(colorMul[i, j]));
                }
            }

            // 상면 삼각형(법선 +y 위쪽) — (v00, v01, v11), (v00, v11, v10)
            for (int j = 0; j < size - 1; j++)
            {
                for (int i = 0; i < size - 1; i++)
                {
                    int v00 = topIndex[i, j], v10 = topIndex[i + 1, j];
                    int v01 = topIndex[i, j + 1], v11 = topIndex[i + 1, j + 1];
                    tris.Add(v00); tris.Add(v01); tris.Add(v11);
                    tris.Add(v00); tris.Add(v11); tris.Add(v10);
                }
            }

            // 치막(skirt) — 가장자리 둘레를 아래로 내려 기존 흙타일 두께(−0.1m)와 동일하게 막는다.
            // 둘레 순회(위에서 볼 때 반시계): (−0.5,−0.5)→(−0.5,0.5)[−x면] →(0.5,0.5)[+z면]
            //   →(0.5,−0.5)[+x면] →(−0.5,−0.5)[−z면] — 체인 방향 d마다 외면 = cross(down, d).
            int ringCount = (size - 1) * 4;   // 코너 중복 없는 둘레 정점 수
            var perimeter = new int[ringCount];
            for (int k = 0; k < size - 1; k++)
            {
                perimeter[k] = topIndex[0, k];                                    // x=−0.5, z 증가
                perimeter[size - 1 + k] = topIndex[k, size - 1];                  // z=+0.5, x 증가
                perimeter[2 * (size - 1) + k] = topIndex[size - 1, size - 1 - k]; // x=+0.5, z 감소
                perimeter[3 * (size - 1) + k] = topIndex[size - 1 - k, 0];        // z=−0.5, x 감소
            }

            for (int t = 0; t < ringCount; t++)
            {
                int iTopA = perimeter[t];
                int iTopB = perimeter[(t + 1) % ringCount];
                Vector3 pA = verts[iTopA];
                Vector3 pB = verts[iTopB];

                // 바닥 정점(중복 — 법선 분리, 상면 정점과 공유 안 함)
                int bA = verts.Count;
                verts.Add(new Vector3(pA.x, -SkirtDepth, pA.z));
                uvs.Add(new Vector2(uvs[iTopA].x, 0f));
                cols.Add(VertexShadeColor(VertexMulEdge * 0.85f));
                int bB = verts.Count;
                verts.Add(new Vector3(pB.x, -SkirtDepth, pB.z));
                uvs.Add(new Vector2(uvs[iTopB].x, 0f));
                cols.Add(VertexShadeColor(VertexMulEdge * 0.85f));

                tris.Add(iTopA); tris.Add(bA); tris.Add(bB);
                tris.Add(iTopA); tris.Add(bB); tris.Add(iTopB);
            }

            var mesh = new Mesh { name = "FarmSoil_TillHole" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            // [154차 P2] 루트 수리 — 탄젠트 부재 + _NORMALMAP 활성 = TBN 퇴화 → 법선 파괴 → 직사광 기여 소실 = 검정 렌더.
            //   RecalculateTangents로 탄젠트를 채워 TBN을 성립시킨다(흙타일 빌트인 Cube는 탄젠트 보유라 정상이었던 것과 동일 조건).
            mesh.RecalculateTangents();
            return mesh;
        }

        // ===== 시각 빌드/갱신 — FarmPlot 시각부 위임 진입점 [153차 P1] =====

        /// <summary>URP/Lit 셰이더 확보 — URP 미초기화 시 Standard 폴백(기존 경로 유지 선례).</summary>
        private static Shader UrpLitShader()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            return sh != null ? sh : Shader.Find("Standard");
        }

        /// <summary>밭흙 디테일 배선 — 절차 텍스처+normal(URP/Lit _BumpMap+keyword). 특성 미지원 시 무해.</summary>
        private static void ApplySoilDetailTexture(Material mat)
        {
            if (mat == null) return;
            if (mat.HasProperty("_MainTex")) mat.mainTexture = GetOrCreateSoilTexture();
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", GetOrCreateSoilNormal());
                mat.EnableKeyword("_NORMALMAP");
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.5f);
            }
        }

        /// <summary>흙 타일 빌드 — 1×0.1×1 Cube, y −0.05(기존 계약)+밭흙 절차 텍스처 배선. 시각 전용.</summary>
        public static void BuildSoilVisual(FarmPlot plot)
        {
            if (plot._soilVisual != null) return;

            var soil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            soil.name = "Soil";
            soil.transform.SetParent(plot.transform, false);
            soil.transform.localPosition = new Vector3(0f, -0.05f, 0f);   // 흙타일이 지면에 약간 박히게(기존 계약)
            soil.transform.localScale = new Vector3(1f, 0.1f, 1f);

            var soilRenderer = soil.GetComponent<MeshRenderer>();
            if (soilRenderer != null)
            {
                var mat = new Material(UrpLitShader());
                ApplySoilDetailTexture(mat);   // 밭흙 텍스처/normal 배선 [153차 P1]
                // 초기색 — 텍스처가 베이스색을 보유하므로 Grass 외에는 흰 배수(Start의 RefreshVisuals가 상태색 교정)
                mat.color = plot.Phase == FarmPlot.CropPhase.Grass ? GetSoilColor(plot.Phase) : Color.white;
                soilRenderer.material = mat;
            }

            var soilCol = soil.GetComponent<Collider>();
            if (soilCol != null) Object.Destroy(soilCol);   // 시각 전용 — 전투 Raycast/루팅 간섭 방지(기존 계약)

            plot._soilVisual = soil.transform;
        }

        // ===== [무번호 182-P2] Farm Pack 밭 타일 승격 — 팩 모델 존재 시 우선, 부재 시 기존 큐브 폴백 =====

        /// <summary>[무번호 182-P2] 팩 밭 타일 GLB(Resources 키) — SM_Field_1_1 → SM_ 접두 제거 수용
        ///   (herb_* 네이밍 선례 통일). glTFast 임포트 — herb GLB와 동일 폴더·동일 방식.
        ///   GLB 내부 이미지 uri("../../Textures/farm_atlas.png")는 glb 위치 기준 상대경로 →
        ///   Assets/Resources/Textures/farm_atlas.png로 해석 [182차 수리 — Assets/Textures 배치 시
        ///   임포트 실패(해석 경로 산술: UserProvided의 2단계 위=Resources)](아틀라스 원본 무수정 — 141차 계약).</summary>
        public const string SoilPackResourcesPath = "Models/UserProvided/Field_1_1";

        private static GameObject _soilPackPrefab;   // 팩 타일 프리팹 캐시 — Resources.Load 1회 시도(결정론)
        private static bool _soilPackProbeDone;      // 로드 시도 완료 플래그 — 실패 재시도 방지(폴백 고정)

        /// <summary>[무번호 182-P2] 팩 밭 타일 프리팹 로드 — 1회 시도 후 캐시. 부재 시 null 반환 →
        ///   전 상태 기존 큐브 경로 폴백(연출 중단 없음 계약).</summary>
        private static GameObject LoadSoilPackPrefab()
        {
            if (!_soilPackProbeDone)
            {
                _soilPackProbeDone = true;
                _soilPackPrefab = Resources.Load<GameObject>(SoilPackResourcesPath);
            }
            return _soilPackPrefab;
        }

        /// <summary>[무번호 182-P2] 팩 밭 타일 확보 — 플롯 자식 "SoilPack" 검색, 부재 시 로드 성분에 한해 1회 빌드.
        ///   NormalizeGlbScale 재용(최대차원 1.0) → CenterBoundsOnPivot 재용(중심 정렬) → renderer bounds 상면이
        ///   y=0이 되도록 extents만큼 내림(기존 큐브 상면 y=0 계약 동일) → DestroyCropColliders 재용(시각 전용)
        ///   → 렌더러 머티리얼 인스턴스화(CacheCropRenderers 선례 — 공유 머티리얼 오염 금지).
        ///   로드 실패/렌더러 부재 시 null — 기존 큐브 경로 폴백(FarmPlot.cs 무변경 — 플롯 자식 이름 검색으로 캐시 대체).</summary>
        private static Transform EnsureSoilPackVisual(FarmPlot plot)
        {
            if (plot == null) return null;

            var existing = plot.transform.Find("SoilPack");
            if (existing != null) return existing;

            var prefab = LoadSoilPackPrefab();
            if (prefab == null) return null;   // 폴백 계약 — 기존 큐브 경로 유지(연출 중단 없음)

            var glb = Object.Instantiate(prefab, plot.transform, false);
            glb.name = "SoilPack";
            NormalizeGlbScale(glb);                       // 최대차원 1.0 — 기존 1×1m 타일 계약 동일

            var rends = glb.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
            {
                Object.Destroy(glb);                      // 렌더러 부재 — 팩 타일 무의미 → 기존 큐브 폴백
                return null;
            }

            CenterBoundsOnPivot(glb, plot.transform);     // bounds 중심 정렬(선례 수학 재용 — 원본 미수정)
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            // 상면이 y=0 — 정렬 후 bounds 중심이 플롯 원점, 상면 = +extents.y → extents만큼 내림(기존 큐브 계약 동일).
            glb.transform.localPosition += new Vector3(0f, -b.extents.y, 0f);

            DestroyCropColliders(glb);                    // 시각 전용(기존 계약)

            // 머티리얼 인스턴스화 + 초기 흰 배수 — 이후 .material getter가 동일 인스턴스 반환(Unity 캐시 = 캐시 계약).
            foreach (var rend in rends)
            {
                if (rend == null) continue;
                SetBaseColor(rend.material, Color.white);
            }

            glb.SetActive(false);                         // 표시는 RefreshVisuals 게이트에서 제어
            return glb.transform;
        }

        /// <summary>[무번호 182-P2] 팩 타일 수분 밝기 틴트 — ApplySoilMaterialColor와 병렬, 기존 큐브 산식과 동일 계약.
        ///   Dirt/Tilled=흰 배수(밭흙 텍스처가 베이스색 보유 — 기존 큐브 분기 동일), Planted 이후=
        ///   white × ComputeSoilBrightness(MoistureFraction)(촉촉 ×0.75 어둡 ↔ 마름 ×1.1 밝음 — 수분 가시화 이식).
        ///   전 렌더러 인스턴스 머티리얼에 _BaseColor 기록(SetBaseColor 재용 — URP/내장 호환).</summary>
        private static void ApplyPackTileTint(FarmPlot plot, Transform packTile)
        {
            if (plot == null || packTile == null) return;
            var rends = packTile.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;

            Color tint;
            if (plot.Phase == FarmPlot.CropPhase.Dirt || plot.Phase == FarmPlot.CropPhase.Tilled)
            {
                tint = Color.white;                       // 기존 큐브 Dirt/Tilled 분기 동일 — 텍스처 베이스색
            }
            else
            {
                float b = ComputeSoilBrightness(plot.MoistureFraction());
                tint = new Color(b, b, b, 1f);            // 기존 큐브 default 분기 산식 동일 [153차 P1]
            }

            for (int i = 0; i < rends.Length; i++)
            {
                var rend = rends[i];
                if (rend == null) continue;
                var m = rend.material;                    // 인스턴스 머티리얼 — 공유 머티리얼 오염 금지
                if (m == null) continue;
                SetBaseColor(m, tint);
            }
        }

        /// <summary>[153차 P1] 굉이질 구멍 시각 — 단일 프로시저럴 격자 메시(오목+흙둑+정점색 AO).
        ///   GameObject 1개+MeshFilter/MeshRenderer — 시각 전용(콜라이더 없음). 기존 계약: Tilled에서만 SetActive.</summary>
        public static void BuildHoleVisual(FarmPlot plot)
        {
            if (plot._holeVisual != null) return;

            var holeRoot = new GameObject("TillHole");
            holeRoot.transform.SetParent(plot.transform, false);
            // [154차 P2] 구멍 루트 +0.002 상승(사용자 승인 — HoleMeshLiftY) — 동일 평면/지면 접촉 z-파이팅 보험.
            //   메시 로컬 기하는 무변경 — 최대 내림 −0.08, 플롯 시각 깊이 −0.1m 이내 계약 유지.
            holeRoot.transform.localPosition = new Vector3(0f, HoleMeshLiftY, 0f);

            var mf = holeRoot.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildTillHoleMesh();

            var mr = holeRoot.AddComponent<MeshRenderer>();
            mr.material = GetOrCreateHoleMaterial();
            // [154차 P2] 그림자 수신 해제(사용자 승인 보험) — 실내 그림자 광원 대비, 렌더 비용 0.
            mr.receiveShadows = false;

            plot._holeVisual = holeRoot.transform;
            plot._holeVisual.gameObject.SetActive(false);   // Tilled에서만 — 기존 계약
        }

        // ===== [154차 P2] 구멍 전용 AO 프리멀티 텍스처 — 정점색(URP/Lit 무시) 대체 =====

        private static Texture2D _holeTexture;   // 구멍 전용 AO 프리멀티 텍스처 — static 캐시(결정론)

        /// <summary>[154차 P2] 구멍 전용 AO 프리멀티 텍스처 — GetOrCreateSoilTexture() 픽셀 복사 후 링별 명암 프리멀티.
        ///   배경: 정점색 AO는 URP/Lit이 무시(H3 실측 — 죽은 데이터) → 동일 톤 구조를 텍스처 명암으로 이식.
        ///   링 배수는 기존 VertexMul 상수 재사용(중앙 0.40 / 내벽 0.45·0.62 / 둑 1.0 / 가장자리 0.95).
        ///   static 캐시 + 고정 시드 결정론(GetOrCreateSoilTexture 캐시 패턴 준용). public static — EditMode 직접 판정용.</summary>
        public static Texture2D GetOrCreateHoleTexture()
        {
            if (_holeTexture == null) _holeTexture = CreateHoleTexture();
            return _holeTexture;
        }

        /// <summary>[154차 P2] AO 프리멀티 생성 — 밭흙 텍스처 복사 후, UV를 9×9 격자 좌표로 환산해 링별 배수 적용.
        ///   링 판정은 BuildTillHoleMesh 상면 UV와 동일 체비셰프 격자(중심 격자거리 반올림 0..4) — 결정론.</summary>
        private static Texture2D CreateHoleTexture()
        {
            var src = GetOrCreateSoilTexture();
            int size = src.width;
            var pixels = src.GetPixels32();   // 밭흙 베이스(노이즈/경준선 포함) 복사 — 외관 연속성

            int maxRing = HoleGridSize / 2;   // 9×9 격자의 최외곽 링 = 4
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // UV → 격자 좌표(BuildTillHoleMesh 상면 UV가 타일 전체 0..1) → 중심 격자거리 체비셰프
                    float d = Mathf.Max(
                        Mathf.Abs(x / (float)(size - 1) * (HoleGridSize - 1) - maxRing),
                        Mathf.Abs(y / (float)(size - 1) * (HoleGridSize - 1) - maxRing));
                    int ring = Mathf.Clamp(Mathf.RoundToInt(d), 0, maxRing);
                    float mul;
                    switch (ring)
                    {
                        case 0:  mul = VertexMulFloor; break;   // 중앙 바닥(가장 어두움 — AO)
                        case 1:  mul = VertexMulWall;  break;   // 내벽 하단
                        case 2:  mul = VertexMulLip;   break;   // 내벽 상단(경사면)
                        case 3:  mul = VertexMulBerm;  break;   // 흙둑(가장 밝게)
                        default: mul = VertexMulEdge;  break;   // 타일 가장자리
                    }
                    var p = pixels[y * size + x];
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(p.r * mul), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(p.g * mul), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(p.b * mul), 0, 255),
                        255);
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "FarmSoil_TillHole_AO",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };
            tex.SetPixels32(pixels);
            tex.Apply(true);   // 밉 재생성 — 재질 배수 × 프리멀티 AO 명암
            return tex;
        }

        private static Material _holeMaterial;   // 구멍 전용 재질 — 불변(플롯별 변형 없음 — 전 플롯 공유 캐시)

        /// <summary>구멍 재질 — 밭흙 normal + AO 프리멀티 텍스처 [154차 P2], 배수 1.12(AO 명암과 곱해 내벽/둑 스펙 톤 도달).</summary>
        private static Material GetOrCreateHoleMaterial()
        {
            if (_holeMaterial == null)
            {
                _holeMaterial = new Material(UrpLitShader())
                {
                    color = new Color(HoleMaterialMul, HoleMaterialMul, HoleMaterialMul, 1f),
                };
                ApplySoilDetailTexture(_holeMaterial);   // normal/_NORMALMAP/배수 배선 유지 [153차 P1]
                // [154차 P2] AO 전환 — mainTexture를 구멍 전용 AO 프리멀티 텍스처로 교체(정점색은 URP/Lit 무시 — 죽은 데이터).
                if (_holeMaterial.HasProperty("_MainTex")) _holeMaterial.mainTexture = GetOrCreateHoleTexture();
            }
            return _holeMaterial;
        }

        /// <summary>[153차 P3] 작물 시각 — Sphere 폐기 → GLB 승격(Resources.UserProvided herb_*.glb).
        ///   로드 성공 시 GLB를 Crop 피벗 자식으로 정규화 배치, 실패 시 기존 Sphere 폴백(연출 중단 없음 계약).
        ///   피벗 로컬스케일 곱 구조로 성장 스케일 계약(0.12→0.4→0.8) 유지.</summary>
        public static void BuildCropVisual(FarmPlot plot)
        {
            // [153차 P3] 작물 타입 불일치 재빌드 — Configure/파종으로 CropType이 바뀌면 GLB(또는 폴백)를
            //   새로 승격. 기존 Sphere 계약색 경로는 색만 따라갔지만 GLB 메시 자체가 타입 의존이라 재빌드 필요.
            // [무번호 183-P3] 타입 불일치 + 단계 불일치(성장 스왑) 재빌드 — 153차 패턴 확장.
            if (plot._cropVisual != null && (plot._cropGlbBuiltForType != plot.CropType || plot._cropGlbBuiltForStage != CurrentCropStageIndex(plot)))
            {
                plot._cropVisual = null;
                plot._cropRenderers = null;
                plot._cropGlbBaseColors = null;
                plot._cropIsGlb = false;
                plot._cropBaseEulerSet = false;   // 스웨이 기준 회전 초기화
            }

            if (plot._cropVisual != null) return;

            var pivot = new GameObject("Crop");
            pivot.transform.SetParent(plot.transform, false);

            bool isGlb = false;
            int stage = CurrentCropStageIndex(plot);
            var glbPrefab = LoadCropStagePrefab(plot.CropType, stage);   // [무번호 183-P3] 팩 단계 모델 우선
            if (glbPrefab == null) glbPrefab = LoadCropGlbPrefab(plot.CropType);   // 폴백 — 기존 herb GLB(계약 유지)
            if (glbPrefab != null)
            {
                // GLB 승격 — Instantiate → bounds 최대차원 1.0 정규화 → 피벗 중심 정렬(흙 위 안착)
                var glb = Object.Instantiate(glbPrefab, pivot.transform, false);
                glb.name = "Crop_GLB";
                NormalizeGlbScale(glb);                       // 최대차원 1.0 — 피벗 스케일 s = 유효 크기 s(Sphere 직경 계약 동일)
                CenterBoundsOnPivot(glb, pivot.transform);    // GLB 피벗 편차 흡수 — 발이 흙에 안착(선례 CenterOnPivot 수학)
                isGlb = true;
            }
            else
            {
                // 폴백 — 기존 Sphere(피벗 스케일 곱 = 기존 직경 s 계약 동일 — 연출 중단 없음)
                var crop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crop.name = "Crop_Sphere";
                crop.transform.SetParent(pivot.transform, false);
                crop.transform.localScale = Vector3.one;
            }

            DestroyCropColliders(pivot.gameObject);               // 시각 전용(기존 계약)
            CacheCropRenderers(plot, pivot.transform, isGlb);     // material 인스턴스화 + 기본색 캐시 [153차 P3]
            plot._cropVisual = pivot.transform;
            plot._cropIsGlb = isGlb;
            plot._cropGlbBuiltForType = plot.CropType;   // [153차 P3] 승격 타입 기록 — 불일치 재빌드 판정 기준
            plot._cropGlbBuiltForStage = stage;   // [무번호 183-P3] 승격 단계 기록 — 성장 스왑 재빌드 판정
            plot._cropVisual.gameObject.SetActive(false);   // 파종 전 비표시(기존 계약)
        }

        /// <summary>[153차 P3] GLB 렌더러 bounds 최대차원 → 목표치 정규화 — AcquisitionCinema.NormalizeScale
        ///   수학 참고(복사 허용 — 원본 미수정). 렌더러 부재/퇴화 바운드 시 스케일 0.5 폴백(선례 동일).</summary>
        private static void NormalizeGlbScale(GameObject visual, float targetMaxDim = 1f)
        {
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
            {
                visual.transform.localScale = Vector3.one * 0.5f;
                return;
            }
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            // [130차 선례] 최대 차원 기준 — 약초는 세로가 길고 물고기는 가로가 길다(높이 기준이면 물고기 과대).
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxDim < 0.0001f)
            {
                visual.transform.localScale = Vector3.one * 0.5f;
                return;
            }
            // 부모 스케일 영향 배제를 위한 월드 기준 보정 — 로컬 적용(선례 동일)
            float worldScale = visual.transform.lossyScale.x;
            float localScale = targetMaxDim / Mathf.Max(0.0001f, maxDim * Mathf.Max(0.0001f, worldScale));
            visual.transform.localScale = Vector3.one * Mathf.Clamp(localScale, 0.05f, 3f);
        }

        /// <summary>[153차 P3] 콘텐츠 bounds 중심을 피벗에 정렬(월드 시프트 — 부모 회전/스케일 무관,
        ///   AcquisitionCinema.CenterOnPivot 수학 참고). GLB 원점이 발/베이스에 있어도 안착·스웨이 중심이 콘텐츠 중심.</summary>
        private static void CenterBoundsOnPivot(GameObject visual, Transform pivot)
        {
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            visual.transform.position += pivot.position - b.center;
        }

        /// <summary>작물 콜라이더 파괴 — 시각 전용(기존 계약).</summary>
        private static void DestroyCropColliders(GameObject root)
        {
            var cols = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) Object.Destroy(cols[i]);
        }

        /// <summary>[153차 P3] 틴트/기본색 적용 대상 전 renderer 캐시 — renderer.material 인스턴스화
        ///   (공유 머티리얼 오염 금지 — CacheFloatRenderers 선례). GLB 기본색 캐시로 건강 복원 경로 마련.</summary>
        private static void CacheCropRenderers(FarmPlot plot, Transform root, bool isGlb)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);   // include inactive — 생성 직후 비표시라 전체 조사
            var list = new List<Renderer>();
            var colors = new List<Color>();
            foreach (var rend in rends)
            {
                if (rend == null) continue;
                var m = rend.material;   // 인스턴스화 — 이 시점부터 전부 플롯 소유 인스턴스
                if (m == null) continue;
                list.Add(rend);
                if (isGlb)
                    colors.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                        : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
            }
            plot._cropRenderers = list.ToArray();
            plot._cropGlbBaseColors = colors.ToArray();
        }

        /// <summary>작물 색 적용 — 우선순위: 시듦/부패 강제색 → GLB 기본색 복원 → 폴백 Sphere 계약색 [153차 P3].</summary>
        private static void ApplyCropColor(FarmPlot plot)
        {
            var rends = plot._cropRenderers;
            if (rends == null) return;
            var glbBase = plot._cropGlbBaseColors;

            for (int i = 0; i < rends.Length; i++)
            {
                var rend = rends[i];
                if (rend == null) continue;
                var m = rend.material;
                if (m == null) continue;

                if (plot._cropColorOverride.HasValue)
                {
                    SetBaseColor(m, plot._cropColorOverride.Value);       // 시듦(갈색)/부패(검붉은) 틴트
                }
                else if (plot._cropIsGlb && glbBase != null && i < glbBase.Length)
                {
                    SetBaseColor(m, glbBase[i]);                          // 건강 GLB — 기본색 복원(틴트 해제)
                }
                else
                {
                    SetBaseColor(m, GetCropColor(plot.CropType));         // 폴백 Sphere — 기존 계약색
                }
            }
        }

        /// <summary>머티리얼 기본색 기록 — URP(_BaseColor)/내장(_Color) 호환(선례 경로).</summary>
        private static void SetBaseColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        /// <summary>흙타일 상태색 — Grass=잔디 단색(기존 계약), Dirt/Tilled=텍스처 베이스(흰 배수),
        ///   Planted 이후=수분 밝기 보간 [153차 P1 — 수분 가시화 계약].</summary>
        private static void ApplySoilMaterialColor(Renderer soilRenderer, FarmPlot plot)
        {
            if (soilRenderer == null) return;
            var mat = soilRenderer.material;
            if (mat == null) return;

            switch (plot.Phase)
            {
                case FarmPlot.CropPhase.Grass:
                    mat.mainTexture = null;                      // 잔디 단색 — 기존 계약 유지
                    SetBaseColor(mat, GetSoilColor(plot.Phase));
                    break;

                case FarmPlot.CropPhase.Dirt:
                case FarmPlot.CropPhase.Tilled:
                    ApplySoilDetailTexture(mat);                 // 베이스 갈색은 텍스처가 보유 — 흰 배수
                    SetBaseColor(mat, Color.white);
                    break;

                default:
                    // Planted/Growing/Ready/Withered/Rotted — 경작 흙길 + 수분 밝기 보간 [153차 P1]
                    ApplySoilDetailTexture(mat);
                    float b = ComputeSoilBrightness(plot.MoistureFraction());
                    SetBaseColor(mat, new Color(b, b, b, 1f));
                    break;
            }
        }

        /// <summary>상태별 시각 갱신 [26차 — 구멍/흙길/작물 통합] — [153차 P1] FarmPlot에서 위임받은 전체 갱신.
        ///   기존 계약 유지: 구멍은 Tilled에서만 SetActive, 작물 스케일 0.12→0.4→0.8, 시듦/부패 강제색.</summary>
        public static void RefreshVisuals(FarmPlot plot)
        {
            if (plot._soilVisual == null) BuildSoilVisual(plot);
            if (plot._holeVisual == null) BuildHoleVisual(plot);
            // [무번호 183-P3] 성장 단계 스왑 — 목표 단계가 빌드 단계와 다르면 재빌드(Growing 진행 스왑).
            if (plot._cropVisual != null && plot._cropGlbBuiltForStage != CurrentCropStageIndex(plot))
            {
                plot._cropVisual = null;
                plot._cropRenderers = null;
                plot._cropGlbBaseColors = null;
                plot._cropIsGlb = false;
                plot._cropBaseEulerSet = false;
            }
            if (plot._cropVisual == null) BuildCropVisual(plot);
            if (plot._soilVisual == null || plot._holeVisual == null || plot._cropVisual == null) return;   // 기존 방어 유지

            bool tilled = plot.Phase == FarmPlot.CropPhase.Tilled;

            // 구멍 시각 — Tilled에서만 표시(기존 계약 유지)
            plot._holeVisual.gameObject.SetActive(tilled);

            // [무번호 182-P2] 팩 밭 타일 확보 — 리소스 로드 성공 시 1회 빌드, 부재 시 null(기존 큐브 경로 폴백 계약).
            Transform packTile = EnsureSoilPackVisual(plot);

            // [무번호 182-P2] 팩 밭 타일 게이트 — Grass=비표시(기존 잔디 큐브 현상 유지),
            //   Tilled=비표시(구멍만 — 기존 로직 확장), Dirt/Planted/Growing/Ready/Withered/Rotted=표시.
            bool showPackTile = packTile != null && !tilled && plot.Phase != FarmPlot.CropPhase.Grass;
            if (packTile != null) packTile.gameObject.SetActive(showPackTile);

            // [153차 P1] Tilled 동안 평탄 흙타일을 내려 구멍 메시(중앙 내림+둑)가 타일 위를 대표 —
            //   흙타일 상면(y 0)이 구멍 내림부를 가리는 가림/z-파이트 방지. 시각 전용 스왑(상태머신 무관).
            //   [무번호 182-P2] 확장 — 팩 타일 표시 상태에서도 기존 큐브 비표시(팩 타일이 큐브를 대체).
            //   팩 타일 부재(null) 시 기존 경로 그대로(!tilled) — 현상 유지(폴백 계약).
            plot._soilVisual.gameObject.SetActive(!tilled && !showPackTile);

            // 흙타일 상태색 — Grass=잔디색 / Dirt·Tilled=흙색 / Planted 이후=수분 밝기 [153차 P1]
            var soilRenderer = plot._soilVisual.GetComponent<MeshRenderer>();
            if (soilRenderer != null) ApplySoilMaterialColor(soilRenderer, plot);

            // [무번호 182-P2] 팩 타일 수분 밝기 틴트 — 표시 상태에서 기존 큐브 산식과 동일 계약 적용.
            if (showPackTile) ApplyPackTileTint(plot, packTile);

            // 작물 시각 — 기존 스케일 계약 유지 [26차]
            switch (plot.Phase)
            {
                case FarmPlot.CropPhase.Grass:
                case FarmPlot.CropPhase.Dirt:
                case FarmPlot.CropPhase.Tilled:
                    plot._cropVisual.gameObject.SetActive(false);
                    break;
                case FarmPlot.CropPhase.Planted:
                    plot._cropVisual.gameObject.SetActive(true);
                    SetCropScale(plot, 0.12f);
                    break;
                case FarmPlot.CropPhase.Growing:
                    plot._cropVisual.gameObject.SetActive(true);
                    SetCropScale(plot, Mathf.Lerp(0.12f, 0.4f, plot.GrowProgress01()));
                    break;
                case FarmPlot.CropPhase.Ready:
                    plot._cropVisual.gameObject.SetActive(true);
                    SetCropScale(plot, 0.8f);
                    break;
                case FarmPlot.CropPhase.Withered:
                    plot._cropVisual.gameObject.SetActive(true);
                    SetCropScale(plot, 0.35f);                          // 시들어 축소
                    SetCropColorOverride(plot, new Color(0.45f, 0.32f, 0.18f, 1f));   // 갈색 처짐
                    break;
                case FarmPlot.CropPhase.Rotted:
                    plot._cropVisual.gameObject.SetActive(true);
                    SetCropScale(plot, 0.4f);
                    SetCropColorOverride(plot, new Color(0.30f, 0.16f, 0.16f, 1f));   // 검붉은 부패
                    break;
            }
        }

        /// <summary>작물 스케일 적용 — 기존 계약(로컬스케일 s, 흙 위 안착 0.5s+0.02, 기본색/틴트 적용) [153차 P3].
        ///   GLB는 피벗 로컬스케일 곱 구조 — 피벗 스케일 s = 유효 최대차원 s(기존 Sphere 직경 계약 동일).</summary>
        public static void SetCropScale(FarmPlot plot, float s)
        {
            if (plot._cropVisual == null) return;

            plot._cropVisual.localScale = new Vector3(s, s, s);
            plot._cropVisual.localPosition = new Vector3(0f, 0.5f * s + 0.02f, 0f);   // 흙 위에 안착(기존 산식)
            ApplyCropColor(plot);
        }

        /// <summary>시듦/부패 강제색 [26차] — GLB는 전 renderer 틴트(인스턴스 머티리얼) [153차 P3].</summary>
        public static void SetCropColorOverride(FarmPlot plot, Color c)
        {
            plot._cropColorOverride = c;
            ApplyCropColor(plot);
        }

        /// <summary>상태별 흙타일 색 [26차 — 잔디/흙/경작 흙길 구분 계약] — [153차 P1] FarmPlot에서 이관.
        ///   Dirt/Tilled 이후는 텍스처+배수 체계 — 이 팔레트는 Grass 계약색과 톤 기준으로 유지.</summary>
        public static Color GetSoilColor(FarmPlot.CropPhase phase)
        {
            switch (phase)
            {
                case FarmPlot.CropPhase.Grass:  return new Color(0.36f, 0.55f, 0.24f, 1f);   // 잔디(밭 주변과 동일 톤)
                case FarmPlot.CropPhase.Tilled: return new Color(0.42f, 0.29f, 0.16f, 1f);   // 흙(구멍 둘레)
                case FarmPlot.CropPhase.Dirt:   return new Color(0.42f, 0.29f, 0.16f, 1f);   // 기본 흙땅
                default:                        return new Color(0.46f, 0.33f, 0.19f, 1f);   // 경작 흙길(파종 후 평평)
            }
        }

        /// <summary>작물 타입별 기본색 [26차] — [153차 P1] FarmPlot에서 이관(폴백 Sphere 경로 계약 유지).</summary>
        public static Color GetCropColor(HerbPickup.HerbType type)
        {
            switch (type)
            {
                case HerbPickup.HerbType.Red:    return new Color(0.85f, 0.20f, 0.20f, 1f);   // 빨강
                case HerbPickup.HerbType.Purple: return new Color(0.70f, 0.20f, 0.80f, 1f);   // 보라
                case HerbPickup.HerbType.Yellow: return new Color(0.90f, 0.85f, 0.20f, 1f);   // 노랑
                case HerbPickup.HerbType.Silver: return new Color(0.75f, 0.75f, 0.80f, 1f);   // 회백
                case HerbPickup.HerbType.Green:  return new Color(0.20f, 0.70f, 0.20f, 1f);   // 초록
                default:                         return Color.white;
            }
        }

        // ===== [153차 P3] 작물 바람 스웨이 — ResourceNode.ApplyWoodSway 수학 재사용(원본 미수정) =====

        /// <summary>스웨이 진폭 범위(도) — 밭 작물은 연약해 벌목(0.7→3.0도)보다 절제된 소진폭.</summary>
        public const float SwayMinDegrees = 0.7f;
        public const float SwayMaxDegrees = 1.5f;
        /// <summary>스웨이 주파수 — ResourceNode.ApplyWoodSway의 16f 좌우 까닭(수학 재사용).</summary>
        public const float SwayHz = 16f;

        /// <summary>[153차 P3] 작물 바람 스웨이 틱 — FarmPlot.Update 끝단 1줄 훅.
        ///   Growing/Ready 작물에 Time.time 기반 소진폭 사인 스웨이(성장 진행에 따라 0.7→1.5도). 나머지 상태는 복원.</summary>
        public static void Tick(FarmPlot plot)
        {
            if (plot == null || plot._cropVisual == null) return;

            var phase = plot.Phase;
            bool swayActive = (phase == FarmPlot.CropPhase.Growing || phase == FarmPlot.CropPhase.Ready)
                && plot._cropVisual.gameObject.activeSelf;
            if (!swayActive)
            {
                StopSway(plot);
                return;
            }

            if (!plot._cropBaseEulerSet)
            {
                plot._cropBaseEuler = plot._cropVisual.localEulerAngles;   // 기준 회전 캡처(선례 _baseVisualEuler)
                plot._cropBaseEulerSet = true;
            }

            float p = plot.GrowProgress01();                            // 성장 진행(Ready=1)
            float amp = Mathf.Lerp(SwayMinDegrees, SwayMaxDegrees, p);  // 소진폭 0.7~1.5도
            float sway = Mathf.Sin(Time.time * SwayHz) * amp;           // 좌우 까닭(선례 수학)
            plot._cropVisual.localEulerAngles = plot._cropBaseEuler + new Vector3(0f, 0f, sway);
        }

        /// <summary>스웨이 정지 — 원래 회전 복원(ResourceNode.StopSway 선례).</summary>
        private static void StopSway(FarmPlot plot)
        {
            if (!plot._cropBaseEulerSet) return;
            if (plot._cropVisual != null) plot._cropVisual.localEulerAngles = plot._cropBaseEuler;
            plot._cropBaseEulerSet = false;
        }
    }
}
