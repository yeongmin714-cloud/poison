using System.Collections.Generic;
using ProjectName.Systems;
using ProjectName.Core;
using UnityEngine;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [130차] 동물의 숲식 획득 연출 지휘 — HarvestResultBridge의 소비자(HarvestResultUTK 대체).
    ///
    /// publish 소비 시 오케스트레이션(순서 계약):
    ///  ① NPC 대화 패널(OpenForAcquisition — 획득 문안 1행, 선택지 없음) — 패널이 입력 게이트 겸 세션 수명.
    ///  ② 플레이어 얼굴 위 부유 획득 GLB([149차 P2] 구 손끝 부착을 전 종류 대체 — 동물의 숲 스타일.
    ///    AcquisitionItemVisualResolver — 매핑 실패 시 아이콘+아이템명 폴백 표시 [144차 P9-ACQ],
    ///    연출 중단 없음 — 143차 감사 §2-1① 승인: "생략 대신 폴백").
    ///  ③ DialogueCameraDirector.BeginUpperBody(상반신 클로즈업 — CameraFollowSuppressed 게이트 내장).
    ///
    /// 환호 트리거는 여기서 하지 않는다 — 발화 지점(ResourceNode/FishingSystem/FarmingSystem/HerbPickup)
    /// 직접 호출 구조(129차 계약) 유지, 이중 SetTrigger 방지.
    ///
    /// 세션 수명: CheerHoldSeconds(환호 4.7s+마진) 경과 → 패널 Hide + 카메라 End(멱등) + 부유 GLB 제거([149차 P2]).
    /// 카메라는 패널 닫힘 폴링으로도 자동 End(105차 B 단일 경로 — 직접 End는 이중 안전).
    /// 연출 중 재발행(낚시 직후 채집 등) → 현재 세션 종료 후 재시작(리셋 계약).
    ///
    /// 병사 자동작업은 publish 경로에 없음(TryAutoMine/LootBasket — 소스 계약) → 연출 미발동(사용자 확정).
    ///
    /// [153차 P4] 획득 연출 VFX — 부유 GLB 주변 FX 그룹(AcqFloatFX): 양쪽 빛기둥(등급별 강약 §4-2 확정)·
    ///  헤일로·상승 스파클·등장 팝(back-out 0.35s) + P4b 환호 반짝(플레이어 주위 — §4-4 포함 확정).
    ///  희귀도 색 진실원: EquipmentRarityData.GetRarityColor(HarvestResultBridge.LatestRarity) — HarvestResultUTK 선례 단일 소비.
    /// </summary>
    public static class AcquisitionCinema
    {
        /// <summary>연출 유지 시간(초) — 환호 클립 4.7s + 0.3s 마진([130차] 원샷 계약).</summary>
        public const float CheerHoldSeconds = 5.0f;

        // ── [149차 P2] 얼굴 위 부유 사양(전 종류 — 낚시 포함) ──
        /// <summary>부유 앵커(플레이어 루트 기준 상승 m) — 캐릭터 1.8m 계약 머리(프로브 1.7m) 위,
        ///   상반신 클로즈업 프레임 상단(LookAt 1.40m ± 0.62m ≈ 2.02m — [130차] UpperBody 산식) 안쪽.
        ///   구 손끝 오프셋(오른손 본 로컬 0) 대체 — 손은 프레임 하단/환호 클립 가변으로 실측 미표출 원인.</summary>
        private const float FloatAnchorUpM = 1.85f;
        /// <summary>세션 전체 천천히 상승량(m) — 1.85→1.95, 프레임 상단 2.02m 안쪽 유지.</summary>
        private const float FloatRiseM = 0.10f;
        /// <summary>미세 사인 진동 진폭(m) / 주파수(Hz).</summary>
        private const float FloatBobAmplitudeM = 0.03f;
        private const float FloatBobHz = 1.2f;
        /// <summary>종료 페이드 길이(초) — 머티리얼 알파(_BaseColor/_Color, 셰이더 미지원 시 파괴까지 표시 유지).</summary>
        private const float FloatFadeSeconds = 0.8f;
        /// <summary>빌보드 부드러움 계수(1/s — 매 틱 Slerp).</summary>
        private const float FloatBillboardSlerp = 10f;

        // ── [153차 P4] 부유 FX 사양(빛기둥/헤일로/스파클/등장 팝/환호 반짝) ──
        /// <summary>빛기둥 좌우 x 오프셋(m) — 부유 앵커 기준 ±0.55m.</summary>
        private const float FxShaftOffsetXM = 0.55f;
        /// <summary>빛기둥 쿼드 폭(m) — 세로 길이는 등급 표(RarityShaftLength) 산출.</summary>
        private const float FxShaftWidthM = 0.28f;
        /// <summary>빛기둥 세로 중심 y(m) — 부유 중심 기준 소폭 상단 배치.</summary>
        private const float FxShaftCenterYM = 0.1f;
        /// <summary>빛기둥 사인 바위가 진폭(도)/주파수(Hz) — 좌우 대칭 위상(AnimateFloat 적용).</summary>
        private const float FxShaftSwayDeg = 3.5f;
        private const float FxShaftSwayHz = 0.9f;
        /// <summary>헤일로 — 크기(m)/알파/z 오프셋(GLB 뒤 = 빌보드 로컬 -z).</summary>
        private const float FxHaloSizeM = 0.9f;
        private const float FxHaloAlpha = 0.5f;
        private const float FxHaloOffsetZM = -0.15f;
        /// <summary>등장 팝 — 0→피크≈1.15→1.0 back-out 이징 지속(초). back 상수 2.2일 때 피크 ≈1.154(1.15 계약).</summary>
        private const float FxPopSeconds = 0.35f;
        private const float FxPopBackConst = 2.2f;
        /// <summary>아이템 자체 이징 강도 — 기본(Common) 0.35 + 티어당 0.15(Unique 1.10) — Common도 약하게(§4-2).</summary>
        private const float FxEmissionBaseIntensity = 0.35f;
        private const float FxEmissionStepPerTier = 0.15f;
        /// <summary>아이템 스파클 — 반경(m)/수명(s)/초당률(수명×률 ≈ 동시 입자 ~20).</summary>
        private const float FxItemSparkleRadiusM = 0.22f;
        private const float FxItemSparkleLifetimeS = 1.5f;
        private const float FxItemSparkleRatePerSec = 13f;   // [154차 P3] 표 base — SparkleRateTable[0]와 동일 값(승계 참조용)

        // [154차 P3] 모션/구조 강화 — 등급색 표(EquipmentRarityData) 무변경(사용자 확정), 구조/모션만 확장.
        private const float FxShaftCoreWidthM = 0.14f;    // 코어 빛기둥 폭(아우터 FxShaftWidthM의 절반 — 코어+아우터 2겹)
        private const float FxShaftCoreAlphaMul = 1.6f;   // 코어 알파 배수(표값 ×1.6, 클램프 1.0)
        private const float FxRingRadiusAM = 0.35f;       // 회전 링 A 반경(내링)
        private const float FxRingRadiusBM = 0.50f;       // 회전 링 B 반경(외링)
        private const float FxRingAngularSpeedDeg = 70f;  // 링 회전 각속도(도/초 — A/B 반대 방향)
        private const float FxWaveDurationS = 0.35f;      // 등장 링 웨이브 수명(팝 FxPopSeconds와 동기)
        private const float FxWaveRadiusStartM = 0.2f;
        private const float FxWaveRadiusEndM = 0.9f;
        private const float FxWaveStartAlpha = 0.6f;
        /// <summary>P4b 환호 반짝 — 반경(m)/발화 지속(s)/파티클 수명(s)/초당률 — 세션(CheerHoldSeconds) 내 소진.</summary>
        private const float FxCheerRadiusM = 0.8f;
        private const float FxCheerDurationS = 4f;
        private const float FxCheerParticleLifetimeS = 1.2f;
        private const float FxCheerRatePerSec = 10f;

        // [158차 P4-D] 지면 링 파동 — 발화 지점(플레이어 루트 y=FxGroundRingY) 수평 확산 링 2겹(0.6s·페이드 — 계획 §P4-D).
        //  제2파동 지연 스태거(0.25s). 등급 티어: Common(0)=1겹, Rare+(1+)=2겹, 티어당 끝 반경 +0.12m — 계획 등급 티어 연장.
        private const float FxGroundRingY = 0.05f;          // 바닥 위 높이(m) — z-fighting 회피
        private const float FxGroundRingStartM = 0.25f;     // 시작 반경(m)
        private const float FxGroundRingEndM = 1.6f;        // 끝 반경(m, 티어당 +0.12)
        private const float FxGroundRingDurationS = 0.6f;   // 확산 지속(초 — 계획 확정)
        private const float FxGroundRingDelayS = 0.25f;     // 제2파동 지연(초)
        private const float FxGroundRingAlpha = 0.45f;      // 시작 알파 → 0 페이드
        private const float FxGroundRingStepPerTier = 0.12f;
        // [158차 P4-D] 코어 펄스 — _CoreIntensity 사인 펄스(정적 2.0 과노출 → 1.6↔2.4 숨쉬는 빛, 주기 0.8s — 계획 확정).
        //  에픽+(3+) 진폭 +0.1(계획 등급 티어 연장). 파장/진폭은 Time 기반(엔진 영역 — Random API 금지 계약 무영향).
        private const float FxCorePulseMid = 2.0f;
        private const float FxCorePulseAmp = 0.4f;          // 진폭(±0.4 → 1.6~2.4)
        private const float FxCorePulseHz = 1.25f;          // 주기 0.8s
        private const float FxCorePulseAmpEpicStep = 0.1f;
        /// <summary>[158차 P4-D] 스파클 에픽+(3+) 강화 — 크기 배수/추가 버스트(154차 표 무손상 연장).</summary>
        public const float SparkleEpicBoostSizeMul = 1.2f;
        public const int SparkleEpicBoostBurst = 6;

        private static Updater _updater;
        private static int _lastSeenToken = -1;   // 부트 시점 동기화 — 스타트 직전 publish 재연출 방지(HarvestResultUTK 선례)
        private static float _sessionEndTime = -1f;
        private static GameObject _floatItem;       // [149차 P2] 부유 피벗 루트(player 자식) — 구 손끝 오브젝트 대체
        private static Transform _floatVisual;      // 빌보드 회전 컨테이너(pivot 자식 — 회전 중심=콘텐츠 중심)
        private static Vector3 _floatBaseLocal;     // 센터링 후 부유 앵커 로컬 오프셋(애니 기준점)
        private static float _floatStartTime;       // 부유 애니 시작 시각(상승/진동 위상)
        private static readonly List<Renderer> _floatRenderers = new List<Renderer>();   // 페이드 대상(인스턴스 머티리얼)
        private static readonly List<Color> _floatBaseColors = new List<Color>();        // 페이드 기준색(알파 곱용)

        // ── [153차 P4] 부유 FX 상태 ──
        private static GameObject _floatFxRoot;       // FX 그룹 루트(pivot 자식 "AcqFloatFX" — pivot 파괴에 동반 소멸)
        private static Transform _floatFxShaftL;      // 좌측 빛기둥(사인 바위가 대상)
        private static Transform _floatFxShaftR;      // 우측 빛기둥(좌우 대칭 위상)
        private static Transform _floatFxShaftCoreL;  // [154차 P3] 좌측 코어 빛기둥(외곽층과 반대 위상)
        private static Transform _floatFxShaftCoreR;  // [154차 P3] 우측 코어 빛기둥
        private static Transform _floatFxRingA;       // [154차 P3] 회전 링 A(내링 — +각속도)
        private static Transform _floatFxRingB;       // [154차 P3] 회전 링 B(외링 — -각속도)
        private static Transform _floatFxRingWave;    // [154차 P3] 등장 링 웨이브(확산 1발 — FxWaveDurationS 후 비활성)
        private static ParticleSystem _floatSparkle;  // 아이템 스파클(발화 소유권 = CreateFloatFx)
        private static ParticleSystem _floatSparkleCore;  // [154차 P3] 백색 코어 스파클(발화 소유권 = CreateFloatFx)
        private static Color _waveBaseTint;           // [154차 P3] 링 웨이브 기준색(자체 알파 구동용 — 페이드 등록 제외)
        /// <summary>[153차 P4] FX 페이드 대상(별도 리스트 — GLB 페이드와 동일 알파를 AnimateFloat가 적용).</summary>
        private static readonly List<Renderer> _fxRenderers = new List<Renderer>();
        /// <summary>[153차 P4] FX 페이드 기준색(알파 곱용).</summary>
        private static readonly List<Color> _fxBaseColors = new List<Color>();
        private static GameObject _cheerFxRoot;       // P4b 환호 반짝 루트(player 자식 — EndPresentationInternal 명시 파괴)
        private static Texture2D _shaftTexture;       // 빛기둥 절차 텍스처 캐시(1회 생성 — 결정론)
        private static Texture2D _radialTexture;      // radial 절차 텍스처 캐시(헤일로/파티클 공용)
        private static Texture2D _ringTexture;        // [154차 P3] 링 절차 텍스처 캐시(회전 링/웨이브 공용)
        private static Texture2D _starTexture;        // [158차 P4-D] 별빛 4점 교차 텍스처 캐시(스파클 전용 — 사각 금지 계약)
        private static GameObject _groundRingRoot;    // [158차 P4-D] 지면 링 루트(player 자식 — pivot 밖, End 명시 파괴)
        private static Transform _groundRingA;        // 지면 링 파동 1(즉시)
        private static Transform _groundRingB;        // 지면 링 파동 2(지연 — tier 0이면 미생성)
        private static Color _groundRingTint;         // 지면 링 기준 틴트(알파 애니용)
        private static float _groundRingEndM;         // 등급 차등 반영 끝 반경(스폰 시 확정)
        /// <summary>[158차 P4-D] 코어 펄스 대상 머티리얼(빔 셰이더만 — CreateFxQuad 등록, End 정리).</summary>
        private static readonly List<Material> _fxPulseMats = new List<Material>();
        /// <summary>[153차 P4] 빛기둥 길이 표(m) — tier 0..5 = ItemRarity Common..Unique(§4-2 확정: Common 0.8 → 티어당 +0.2 → Unique 1.8).</summary>
        private static readonly float[] ShaftLengthTableM = { 0.8f, 1.0f, 1.2f, 1.4f, 1.6f, 1.8f };
        /// <summary>[153차 P4] 빛기둥 알파 표 — Common 0.25 → 티어당 +0.10 → Unique 0.75(§4-2). 길이 표와 동일 클램프 계약.</summary>
        private static readonly float[] ShaftAlphaTable = { 0.25f, 0.35f, 0.45f, 0.55f, 0.65f, 0.75f };

        // [154차 P3] 스파클 티어 배수 — 등급 단조 증가(빛기둥 표 선례 준용). 백색 코어는 고정 보조색(매핑 표 아님).
        public static readonly float[] SparkleRateTable = { 13f, 16f, 20f, 24f, 28f, 32f };
        public static readonly float[] SparkleSizeMulTable = { 1.0f, 1.1f, 1.2f, 1.35f, 1.5f, 1.7f };
        public static readonly Color SparkleCoreColor = new Color(1f, 1f, 1f, 1f);   // 백색 코어(고정 보조색 — 등급 매핑 아님)

        public static bool IsPresenting => _sessionEndTime > 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        public static void Ensure()
        {
            if (_updater != null) return;
            _updater = new GameObject("AcquisitionCinema").AddComponent<Updater>();
            Object.DontDestroyOnLoad(_updater.gameObject);
            _lastSeenToken = HarvestResultBridge.LatestToken;   // 스타트 동기화
        }

        // ===== 문안 포맷 (public static — EditMode 직접 판정) =====

        /// <summary>한글 객체 조사(을/를) — 마지막 음절 종성 판정(0xAC00~0xD7A3, (c-0xAC00)%28). 결정론.</summary>
        public static string KoreanObjectParticle(string noun)
        {
            if (string.IsNullOrEmpty(noun)) return "을(를)";
            char last = noun[noun.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return "을(를)";   // 한글 밖 — 보수 표기
            return ((last - 0xAC00) % 28) != 0 ? "을" : "를";
        }

        /// <summary>획득 문안 — 채집/채광/벌목/농경 "X(을)를 획득했다!" / 낚시 "X(을)를 낚았다!". count > 1 → "X x{count}".</summary>
        public static string FormatAcquiredText(HarvestResultBridge.HarvestKind kind, string itemName, int count)
        {
            string name = itemName ?? "";
            string particle = KoreanObjectParticle(name);
            string amount = count > 1 ? $" x{count}" : "";
            return kind == HarvestResultBridge.HarvestKind.Fishing
                ? $"{name}{amount}{particle} 낚았다!"
                : $"{name}{amount}{particle} 획득했다!";
        }

        // ===== [153차 P4] 빛기둥 등급별 강약 (public static — EditMode 직접 판정) =====

        /// <summary>빛기둥 길이(m) — tier 클램프 0..5 결정론(범위 밖 입력도 표 밖 값 반환 금지 — 테스트 직접 판정용 public).</summary>
        public static float RarityShaftLength(int tier)
        {
            return ShaftLengthTableM[Mathf.Clamp(tier, 0, ShaftLengthTableM.Length - 1)];
        }

        /// <summary>빛기둥 알파 — tier 클램프 0..5 결정론(§4-2 확정 강약 계약의 유일 진입점).</summary>
        public static float RarityShaftAlpha(int tier)
        {
            return ShaftAlphaTable[Mathf.Clamp(tier, 0, ShaftAlphaTable.Length - 1)];
        }

        /// <summary>[154차 P3] 코어 빛기둥 알파 — 표값 ×1.6 클램프 1.0(외곽층과 반대 위상 바위가). EditMode 직접 판정용 public.</summary>
        public static float RarityShaftCoreAlpha(int tier)
        {
            return Mathf.Clamp01(RarityShaftAlpha(tier) * FxShaftCoreAlphaMul);
        }

        /// <summary>[154차 P3] 스파클 초기 버스트 입자 수 — 티어 단조(8 + 4×tier). 결정론(테스트 접근용 public).</summary>
        public static int SparkleBurstCount(int tier)
        {
            return 8 + 4 * Mathf.Clamp(tier, 0, 5);
        }

        /// <summary>[158차 P4-D] 지면 링 겹수 — Common(0)=1, Rare+(1..5)=2(계획 등급 티어 확정). 결정론.</summary>
        public static int GroundRingCount(int tier)
        {
            return Mathf.Clamp(tier, 0, 5) >= 1 ? 2 : 1;
        }

        /// <summary>[158차 P4-D] 지면 링 끝 반경(m) — 기본 1.6 + 티어당 +0.12. 결정론.</summary>
        public static float GroundRingEndRadius(int tier)
        {
            return FxGroundRingEndM + FxGroundRingStepPerTier * Mathf.Clamp(tier, 0, 5);
        }

        /// <summary>[158차 P4-D] 코어 펄스 진폭 — 기본 ±0.4(1.6↔2.4), 에픽+(3+) ±0.5. 결정론.</summary>
        public static float CorePulseAmplitude(int tier)
        {
            return FxCorePulseAmp + (Mathf.Clamp(tier, 0, 5) >= 3 ? FxCorePulseAmpEpicStep : 0f);
        }

        // ===== 소비·오케스트레이션 (internal static — EditMode/Updater 공용) =====

        internal static void Tick()
        {
            if (_sessionEndTime > 0f && Time.time >= _sessionEndTime)
            {
                EndPresentation();
                return;
            }
            if (_sessionEndTime > 0f)
            {
                AnimateFloat();   // [149차 P2] 부유 애니(상승+사인 진동+페이드+빌보드) — 연출 중 매 틱
                return;           // 재발행은 종료 후 다음 틱 처리(리셋 계약: 여기선 무시)
            }
            if (_lastSeenToken == HarvestResultBridge.LatestToken) return;

            _lastSeenToken = HarvestResultBridge.LatestToken;
            HarvestResultBridge.MarkConsumed();   // 소비 즉시 플래그 정리(다른 소비자 부재 — [130차] 단일 소비자)
            StartPresentation();
        }

        internal static void StartPresentation()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;   // 씬 전환 경합 — 연출 생략(예외 금지)

            // 재진입 정리(멱등 — 이전 세션 잔재 제거 후 새로 시작)
            EndPresentationInternal();
            _sessionEndTime = Time.time + CheerHoldSeconds;

            // ① 대화 패널 — 획득 문안(입력 게이트)
            string text = FormatAcquiredText(
                HarvestResultBridge.LatestKind, ResolveDisplayName(), HarvestResultBridge.LatestCount);
            NPCDialoguePanelUTK.OpenForAcquisition(player.transform, text);   // CS1503 — 파라미터 Component 계약

            // ② 얼굴 위 부유 GLB — 매핑 실패 시 아이콘+아이템명 폴백(생략 금지 — [144차 P9-ACQ] 승계)
            AttachFloatItem(player);

            // ③ 상반신 클로즈업 — [142차 I] frontView: 카메라를 플레이어 '정면'에 설치(구: 후면 촬영 결함).
            DialogueCameraDirector.BeginUpperBody(player.transform, "", frontView: true);   // CS1503 — 파라미터 Component 계약

            // ④ [153차 P4] P4b 환호 반짝(포함 확정 §4-4) — 플레이어 루트 주위 소 스파클 1계통.
            //    TriggerCheer 발화 지점 계약 무변경(여기서 SetTrigger 호출 금지 — FX 스폰만), 파괴 소유권은 EndPresentationInternal.
            SpawnCheerSparkle(player);
            // ⑤ [158차 P4-D] 지면 링 파동 — 발화 지점 수평 확산 링(Common 1겹/Rare+ 2겹, 0.6s 페이드).
            SpawnGroundRings(player);
            Debug.Log($"[AcquisitionCinema] 🎉 획득 연출 시작 — {text}");
        }

        internal static void EndPresentation()
        {
            EndPresentationInternal();
        }

        private static void EndPresentationInternal()
        {
            _sessionEndTime = -1f;
            DialogueCameraDirector.End();   // 멱등 — 비활성 세션 무영향(패널 폴링 End와 이중 안전)
            if (NPCDialoguePanelUTK.Instance != null)
                NPCDialoguePanelUTK.Instance.Hide();
            if (_floatItem != null)
                Object.Destroy(_floatItem);   // [149차 P2] 부유 피벗+GLB/폴백 일괄 제거(멱등)
            _floatItem = null;
            _floatVisual = null;
            _floatRenderers.Clear();
            _floatBaseColors.Clear();

            // [153차 P4] FX 잔존 정리 — FX 그룹은 pivot(_floatItem) 파괴에 동반 소멸 → 레퍼런스만 해제(멱등).
            _floatFxRoot = null;
            _floatFxShaftL = null;
            _floatFxShaftR = null;
            _floatFxShaftCoreL = null;   // [154차 P3]
            _floatFxShaftCoreR = null;   // [154차 P3]
            _floatFxRingA = null;        // [154차 P3]
            _floatFxRingB = null;        // [154차 P3]
            _floatFxRingWave = null;     // [154차 P3]
            _floatSparkle = null;
            _floatSparkleCore = null;    // [154차 P3]
            _fxRenderers.Clear();
            _fxBaseColors.Clear();
            // [153차 P4] P4b 환호 반짝 — player 자식(별도 루트)이라 pivot 파괴 경로 밖 → 명시 파괴(누수 금지).
            if (_cheerFxRoot != null)
                Object.Destroy(_cheerFxRoot);
            _cheerFxRoot = null;
            // [158차 P4-D] 지면 링 — player 자식(별도 루트) → cheer와 동일 명시 파괴(누수 금지). 펄스 머티리얼은 pivot 파괴에 동반 소멸 → 레퍼런스만 해제.
            if (_groundRingRoot != null)
                Object.Destroy(_groundRingRoot);
            _groundRingRoot = null;
            _groundRingA = null;
            _groundRingB = null;
            _fxPulseMats.Clear();
        }

        /// <summary>표시명 우선순위 — ItemData.displayName(낚시 publish는 itemName에 "(을)를" 가공문 포함) → itemName.</summary>
        private static string ResolveDisplayName()
        {
            var data = HarvestResultBridge.LatestItemData;
            if (data != null && !string.IsNullOrEmpty(data.displayName))
                return data.displayName;
            return HarvestResultBridge.LatestItemName ?? "";
        }

        /// <summary>
        /// [149차 P2] 획득 GLB 얼굴 위 부유(전 종류 — 낚시 포함) — 구 손끝 부착 대체(동물의 숲 스타일).
        ///  ① 부모=player transform, 로컬 오프셋=얼굴 위 FloatAnchorUpM(상반신 클로즈업 프레임 안 보장).
        ///  ② GLB 경로=AcquisitionItemVisualResolver 기존 재사용(매핑 신설 금지) — 실패 시 폴백(생략 금지).
        ///  ③ 부유 애니=AnimateFloat(천천히 상승+미세 사인 진동+종료 알파 페이드+카메라 빌보드).
        ///  ④ 렌더러 바운드 중앙정렬 — GLB 피벗 편차(베이스/센터) 흡수, 부유점 기준 중앙 부유.
        /// </summary>
        private static void AttachFloatItem(GameObject player)
        {
            var data = HarvestResultBridge.LatestItemData;
            if (data == null) return;   // 아이템 정보 부재 — 표시 후보 없음(문안 패널·카메라는 계속)

            // 진단 로그 1줄 — resolver 결과 경로/폴백 여부("GLB 전혀 안 보임" 원인 추적 — [149차]).
            string editorPath = AcquisitionItemVisualResolver.ResolveEditorAssetPath(data);
            Debug.Log($"[149차] 획득 GLB resolver — id={data.id} editorPath={(editorPath ?? "null(폴백)")}");

            var glb = AcquisitionItemVisualResolver.LoadVisual(data);
            bool isFallback = glb == null;
            if (isFallback)
            {
                // [144차 P9-ACQ 승계] GLB 미매핑/미상 id → 조용한 생략 폐기 — 아이콘+아이템명 폴백(연출 계속).
                glb = CreateFloatFallback(data);
                Debug.Log($"[144차 P9-ACQ] GLB 미매핑 → 아이콘+텍스트 폴백 얼굴 위 부유 표시 — id={data.id}");
            }

            // 부유 구조: player ─ pivot(오프셋+부유 애니) ─ visual(빌보드 회전) ─ glb(센터링 콘텐츠)
            var pivot = new GameObject("AcqFloatPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, FloatAnchorUpM, 0f);   // 얼굴 위 앵커
            var visualGo = new GameObject("AcqFloatVisual");
            visualGo.transform.SetParent(pivot.transform, false);
            glb.transform.SetParent(visualGo.transform, false);

            if (!isFallback)
                NormalizeScale(glb, 0.35f);   // 부유 가시 크기(구 손끝 정규화 동일치 — 폴백은 자체 스케일 계약)
            CenterOnPivot(glb, pivot.transform);   // 피벗 편차 흡수 — 빌보드 회전 중심=콘텐츠 중심

            _floatItem = pivot;
            _floatVisual = visualGo.transform;
            _floatBaseLocal = pivot.transform.localPosition;
            _floatStartTime = Time.time;
            CacheFloatRenderers(glb);
            CreateFloatFx(pivot, glb);   // [153차 P4] 부유 GLB 주변 FX 그룹(빛기둥/헤일로/스파클/이징) — pivot 자식 자동 동기
        }

        /// <summary>[149차 P2] 렌더러 바운드 중심을 부유 피벗에 정렬(월드 시프트 — 부모 회전/스케일 무관).
        ///   회전 중심(_floatVisual 원점)=콘텐츠 중심이 되어 빌보드 시 흔들림이 없다.</summary>
        private static void CenterOnPivot(GameObject visual, Transform pivot)
        {
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            visual.transform.position += pivot.position - b.center;
        }

        /// <summary>[149차 P2] 페이드 대상 렌더러 캐시 — renderer.material 인스턴스화(공유 머티리얼 오염 방지).</summary>
        private static void CacheFloatRenderers(GameObject root)
        {
            _floatRenderers.Clear();
            _floatBaseColors.Clear();
            foreach (var rend in root.GetComponentsInChildren<Renderer>())
            {
                if (rend == null) continue;
                Material m = rend.material;
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                    : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                _floatRenderers.Add(rend);
                _floatBaseColors.Add(c);
            }
        }

        /// <summary>
        /// [153차 P4] 부유 GLB 주변 FX 그룹 — pivot 자식 "AcqFloatFX"(상승/사인 진동에 자동 동기).
        ///  ① 양쪽 빛기둥: 좌우 대칭 additive 빌보드 쿼드 — 길이/알파 = 등급 티어 표(RarityShaftLength/RarityShaftAlpha, §4-2 확정).
        ///  ② 헤일로: GLB 뒤 radial 그라디언트 쿼드(등급색, FxHaloSizeM).
        ///  ③ 스파클: 아이템 주위 상승 스파클 파티클 — playOnAwake=false → 이 메서드가 명시 Play()(발화 소유권).
        ///  ④ 아이템 자체 이징: GLB 머티리얼 _EMISSION+등급색(Common도 약하게 — AddWeaponGlow Rare+ 게이트 미재사용).
        ///  빌보드 계약: 쿼드 개별 회전 대신 그룹 전체를 AnimateFloat에서 _floatVisual과 동일 Slerp 빌보드 적용 —
        ///  그룹 로컬 x축이 화면 평면에 놓여 좌우 ±오프셋 대칭이 유지된다.
        ///  페이드 계약: 쿼드 렌더러는 _fxRenderers에 별도 등록 → AnimateFloat가 GLB와 동일 알파 페이드 적용(잔상 금지).
        /// </summary>
        private static void CreateFloatFx(GameObject pivot, GameObject glb)
        {
            int tier = Mathf.Clamp((int)HarvestResultBridge.LatestRarity, 0, 5);   // ItemRarity 6단계(0..5) — 진실원 계약
            Color rarityColor = EquipmentRarityData.GetRarityColor(HarvestResultBridge.LatestRarity);

            var fxRoot = new GameObject("AcqFloatFX");
            fxRoot.transform.SetParent(pivot.transform, false);
            _floatFxRoot = fxRoot;

            // ① 양쪽 빛기둥 — 다층(코어+아우터) [154차 P3]: 아우터=기존 0.28m/표 알파, 코어=0.14m/표 알파×1.6(클램프 1.0).
            //    등급 강약은 기존 RarityShaftLength/RarityShaftAlpha 표 그대로(사용자 확정 — 색 표 무변경).
            float shaftLen = RarityShaftLength(tier);
            float outerAlpha = RarityShaftAlpha(tier);
            Color shaftTint = new Color(rarityColor.r, rarityColor.g, rarityColor.b, outerAlpha);
            Color coreTint = new Color(rarityColor.r, rarityColor.g, rarityColor.b, RarityShaftCoreAlpha(tier));
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                float sign = left ? -1f : 1f;
                Vector3 pos = new Vector3(FxShaftOffsetXM * sign, FxShaftCenterYM, 0f);
                Renderer outer = CreateFxQuad(left ? "AcqShaftL" : "AcqShaftR", fxRoot.transform, GetShaftTexture(),
                    new Vector2(FxShaftWidthM, shaftLen), shaftTint, pos);
                RegisterFxRenderer(outer, shaftTint);
                if (left) _floatFxShaftL = outer.transform; else _floatFxShaftR = outer.transform;
                Renderer core = CreateFxQuad(left ? "AcqShaftCoreL" : "AcqShaftCoreR", fxRoot.transform, GetShaftTexture(),
                    new Vector2(FxShaftCoreWidthM, shaftLen), coreTint, pos);
                RegisterFxRenderer(core, coreTint);
                if (left) _floatFxShaftCoreL = core.transform; else _floatFxShaftCoreR = core.transform;
            }

            // ② 헤일로 — GLB 뒤(빌보드 로컬 -z = 카메라 반대편).
            Color haloTint = new Color(rarityColor.r, rarityColor.g, rarityColor.b, FxHaloAlpha);
            RegisterFxRenderer(CreateFxQuad("AcqHalo", fxRoot.transform, GetRadialTexture(),
                new Vector2(FxHaloSizeM, FxHaloSizeM), haloTint, new Vector3(0f, 0f, FxHaloOffsetZM)), haloTint);

            // ②' [154차 P3] 회전 링 2겹 — 부유 아이템 주위 화면 평면 원형, A/B 반대 방향 등속 스핀.
            //    링 쿼드는 fxRoot 빌보드에 의해 항상 카메라를 향함 → 로컬 Z축 회전 = 화면 평면 스핀.
            Color ringTint = new Color(rarityColor.r, rarityColor.g, rarityColor.b, RarityShaftAlpha(tier));
            RegisterFxRenderer(CreateFxQuad("AcqRingA", fxRoot.transform, GetRingTexture(),
                new Vector2(FxRingRadiusAM * 2f, FxRingRadiusAM * 2f), ringTint, Vector3.zero), ringTint);
            _floatFxRingA = fxRoot.transform.Find("AcqRingA");
            RegisterFxRenderer(CreateFxQuad("AcqRingB", fxRoot.transform, GetRingTexture(),
                new Vector2(FxRingRadiusBM * 2f, FxRingRadiusBM * 2f), ringTint, new Vector3(0f, 0f, FxHaloOffsetZM)), ringTint);
            _floatFxRingB = fxRoot.transform.Find("AcqRingB");

            // ②'' [154차 P3] 등장 링 웨이브 — 팝(0.35s)과 동기 확산 링 1발(0.2→0.9m, 알파 0.6→0).
            //    페이드 등록 제외 — 수명 0.35s가 세션 페이드(0.8s)보다 짧아 자체 구동이 단일 진실원(AnimateFloat 직접 구동).
            var waveQuad = CreateFxQuad("AcqRingWave", fxRoot.transform, GetRingTexture(),
                new Vector2(FxWaveRadiusStartM * 2f, FxWaveRadiusStartM * 2f),
                new Color(rarityColor.r, rarityColor.g, rarityColor.b, FxWaveStartAlpha), Vector3.zero);
            _floatFxRingWave = waveQuad.transform;
            _waveBaseTint = waveQuad.material.HasProperty("_BaseColor")
                ? waveQuad.material.GetColor("_BaseColor")
                : waveQuad.material.GetColor("_Color");

            // ③ 스파클 — 발화 소유권: playOnAwake=false 후 여기서 명시 Play(). [154차 P3] 티어 배수 표+초기 버스트 적용.
            //    [158차 P4-D] 에픽+(3+) 강화 — 크기 ×1.2·추가 버스트 +6(표 무손상 연장 — 결정론).
            float sparkleSize = SparkleSizeMulTable[tier] * (tier >= 3 ? SparkleEpicBoostSizeMul : 1f);
            int sparkleBurst = SparkleBurstCount(tier) + (tier >= 3 ? SparkleEpicBoostBurst : 0);
            _floatSparkle = CreateSparkleSystem(fxRoot.transform, rarityColor,
                FxItemSparkleRadiusM, FxItemSparkleLifetimeS, SparkleRateTable[tier], sparkleSize,
                sparkleBurst);
            _floatSparkle.Play();
            // ③' [154차 P3] 백색 코어 스파클 — 고정 보조색(매핑 표 아님), 더 좁은 반경/짧은 수명/작은 입자.
            _floatSparkleCore = CreateSparkleSystem(fxRoot.transform, SparkleCoreColor,
                FxItemSparkleRadiusM * 0.55f, FxItemSparkleLifetimeS * 0.6f, SparkleRateTable[tier], 0.6f,
                SparkleBurstCount(tier));
            _floatSparkleCore.Play();

            // ④ 아이템 자체 이징 — 인스턴스 머티리얼에 등급색 발광(Common도 약하게).
            ApplyRarityEmission(glb, tier);
        }

        /// <summary>[153차 P4] FX 페이드 등록 — CreateFxQuad가 자체 인스턴스 머티리얼을 보유(인스턴스화 계약 충족) → 기준색 저장.</summary>
        private static void RegisterFxRenderer(Renderer rend, Color baseColor)
        {
            if (rend == null) return;
            _fxRenderers.Add(rend);
            _fxBaseColors.Add(baseColor);
        }

        /// <summary>[153차 P4] FX 쿼드 1장 생성 — 절차 텍스처+additive 머티리얼(URP Particles/Unlit, Sprites/Default 폴백).
        ///  셰이더 미발견 시 기본 머티리얼 유지(생략 없음 — 연출 중단 없음 계약).</summary>
        private static Renderer CreateFxQuad(string name, Transform parent, Texture2D tex, Vector2 size, Color tint, Vector3 localPos,
            bool preferBeamShader = true)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(quad.GetComponent<Collider>());   // 시각 전용 — 콜라이더 폐기
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            var rend = quad.GetComponent<MeshRenderer>();
            // [157차 P-D1] 고급 빛기둥 셰이더 — 에지 소프트 페더+세로 그라데이션+스크롤 노이즈+HDR 코어.
            //   부재 시 기존 Particles/Unlit 폴백(기존 외관 100% 보존 — 폴백 선례).
            //   [158차 P4-D] preferBeamShader=false — 지면 링 등 수평면 전용(빔 셰이더 세로 그라데이션이 깊이 방향 왜곡 → 폴백 고정).
            Shader shader = preferBeamShader
                ? Shader.Find("Custom/AcquisitionBeam")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default")
                : Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            bool beamShader = shader != null && shader.name == "Custom/AcquisitionBeam";
            Material mat = shader != null ? new Material(shader) : rend.material;   // 셰이더 부재 → 기본 머티리얼(무해)
            mat.name = name + "_Mat";
            mat.mainTexture = tex;
            mat.color = tint;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);   // [153차 P4-폴리시] URP 진실원 색 세팅
            if (beamShader) mat.SetFloat("_CoreIntensity", FxCorePulseMid);   // [158차 P4-D] 펄스 중심값 — AnimateFloat가 사인 진동(기존 정적 2.0 대체)
            // additive 블렌딩 — SpecialEffectsController L373 선례(SrcAlpha/One, ZWrite off, 큐 3000, 투명 서피스)
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", 0);   // 양면 렌더 — 빌보드 뒷면 컬링 무해화
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (shader != null) rend.material = mat;
            if (beamShader) _fxPulseMats.Add(mat);   // [158차 P4-D] 코어 펄스 대상 등록(빔 셰이더만 — End에서 정리)
            return rend;
        }

        /// <summary>[154차 P3] 링 절차 텍스처(64×64) — 환형(ring): 반경 0.8 중심 밴드 + 소프트 폴오프. 캐시 1회(결정론).</summary>
        private static Texture2D GetRingTexture()
        {
            if (_ringTexture != null) return _ringTexture;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "AcqRingTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size - 0.5f) * 2f;             // -1..1
                    float ny = ((y + 0.5f) / size - 0.5f) * 2f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);                // 0(중심)..~1.41(모서리)
                    float band = Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) / 0.14f);   // 링 밴드(0.66~0.94)
                    float innerFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.55f) / 0.11f));  // 내부 소프트 스타트
                    float outerFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1.05f - r) / 0.11f));  // 외부 소프트 엔드
                    pixels[y * size + x] = new Color(1f, 1f, 1f, band * innerFade * outerFade);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _ringTexture = tex;
            return tex;
        }

        /// <summary>[153차 P4] 빛기둥 절차 텍스처(64×128) — 세로 그라디언트: 상단 퍼짐/하단 좁음 + 가로 중심 1→가장자리 0. 캐시 1회.</summary>
        private static Texture2D GetShaftTexture()
        {
            if (_shaftTexture != null) return _shaftTexture;
            const int w = 64, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "AcqShaftTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float ny = (y + 0.5f) / h;                                  // 0=하단 → 1=상단
                float widthFactor = Mathf.Lerp(0.45f, 1.0f, ny);            // 하단 좁음 → 상단 퍼짐
                float endSoften = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(ny, 1f - ny) / 0.08f));
                for (int x = 0; x < w; x++)
                {
                    float nx = Mathf.Abs((x + 0.5f) / w - 0.5f) * 2f;       // 0=중심 → 1=가장자리
                    float core = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(nx / widthFactor));   // [153차 P4-폴리시] 선형→소프트 스텝 — 가장자리 페더(빛 확산 판독)
                    float a = Mathf.Clamp01(core) * endSoften;
                    float aSoft = core * endSoften; pixels[y * w + x] = new Color(1f, 1f, 1f, aSoft);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _shaftTexture = tex;
            return tex;
        }

        /// <summary>[153차 P4] radial 절차 텍스처(128×128) — 중심 1→가장자리 0(헤일로/파티클 공용). 캐시 1회.</summary>
        private static Texture2D GetRadialTexture()
        {
            if (_radialTexture != null) return _radialTexture;
            const int w = 128, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "AcqRadialTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w - 0.5f, dy = (y + 0.5f) / h - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;           // 0=중심 → 1=가장자리
                    float a = Mathf.Clamp01(1f - r);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, a * a);       // 소프트 radial(제곱 폴오프)
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _radialTexture = tex;
            return tex;
        }

        /// <summary>
        /// [158차 P4-D] 별빛 4점 교차 절차 텍스처(64×64) — 스파클 전용(사각 금지 계약 — 계획 §P4-D "별빛 4점 교차 알파 텍스처").
        ///  수학: x/y축 방향 좁은 지수 감쇠 스파이크 + 중심 원형 코어. 대각선 성분은 소멸(사각형 판독 방지) — 결정론(Random 불요).
        ///  캐시 1회(결정론 계약 — GetRingTexture/GetRadialTexture 선례 준용).
        /// </summary>
        private static Texture2D GetStarTexture()
        {
            if (_starTexture != null) return _starTexture;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "AcqStarTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size - 0.5f) * 2f;          // -1..1
                    float ny = ((y + 0.5f) / size - 0.5f) * 2f;
                    float lx = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(nx)), 14f);   // x축 스파이크(좁게)
                    float ly = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(ny)), 14f);   // y축 스파이크(좁게)
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float core = Mathf.Max(0f, 1f - r * 1.35f);          // 중심 원형 코어
                    float a = Mathf.Clamp01(lx + ly + core * core * 0.9f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _starTexture = tex;
            return tex;
        }

        /// <summary>
        /// [153차 P4] 절차 스파클 파티클 시스템 — 에셋 불요 런타임 생성.
        /// 발화 소유권 계약: playOnAwake=false로 생성하고 Play()는 호출측(소유 메서드)이 명시 호출.
        /// 입자 색은 startColor 스트림으로 주입(머티리얼 틴트는 흰색 유지 — 이중 틴트 방지).
        /// </summary>
        private static ParticleSystem CreateSparkleSystem(Transform parent, Color color, float radius, float lifetimeSec, float ratePerSec, float sizeMul = 1f, int burstCount = 0)
        {
            var go = new GameObject("AcqSparkle");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;                                   // 발화 소유권 — 소유 메서드가 명시 Play()
            main.loop = true;
            main.startLifetime = lifetimeSec;
            main.startSpeed = 0.25f;
            main.startSize = 0.03f * Mathf.Max(0.05f, sizeMul);         // 소 스파클(사양 ~0.03m) × 티어 배수 [154차 P3]
            main.startColor = color;
            main.gravityModifier = 0f;
            main.maxParticles = 64;                                     // 상한 소폭 — 성능 위생
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // 부유 이동 추종

            var emission = ps.emission;
            emission.rateOverTime = ratePerSec;                         // 수명×률 ≈ 동시 입자 수
            if (burstCount > 0)
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });   // [154차 P3] 발화 초기 버스트

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = 0.35f;                                         // 상승 스파클

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "AcqSparkle_Mat" };
                mat.mainTexture = GetStarTexture();   // [158차 P4-D] 별빛 4점 교차 — radial 소프트닛 교체(글리터 인상 + 사각 잔존 0)
                // additive 블렌딩 — SpecialEffectsController L373 선례(SrcAlpha/One, ZWrite off, 큐 3000)
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.SetInt("_Cull", 0);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                psr.material = mat;
            }
            return ps;
        }

        /// <summary>[153차 P4] 아이템 자체 이징 — GLB 머티리얼(이미 인스턴스화됨)에 _EMISSION+등급색 발광.
        ///  SpecialEffectsController AddWeaponGlow 기법(L571-613) 준용 — 단, Common도 약하게 적용(Rare+ 게이트 미재사용).
        ///  _EmissionColor 부재 머티리얼은 무시(폴백 스프라이트 등 — 연출 중단 없음).</summary>
        private static void ApplyRarityEmission(GameObject root, int tier)
        {
            Color color = EquipmentRarityData.GetRarityColor(HarvestResultBridge.LatestRarity);
            float intensity = FxEmissionBaseIntensity + FxEmissionStepPerTier * tier;
            foreach (var rend in root.GetComponentsInChildren<Renderer>())
            {
                if (rend == null) continue;
                Material m = rend.material;   // CacheFloatRenderers 선행 인스턴스화 — 동일 인스턴스 재사용
                if (!m.HasProperty("_EmissionColor")) continue;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * intensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
        }

        /// <summary>
        /// [153차 P4] P4b 환호 반짝(포함 확정 §4-4) — 연출 시작과 동기로 플레이어 루트 주위 소 스파클 1계통 스폰.
        ///  · 별도 루트(AcqCheerSparkle, player 자식) — pivot 경로 밖 → 파괴 소유권은 EndPresentationInternal 명시(누수 금지).
        ///  · TriggerCheer 발화 지점 계약 무변경 — 여기서 SetTrigger 호출 금지(FX 스폰만).
        /// </summary>
        private static void SpawnCheerSparkle(GameObject player)
        {
            Color color = EquipmentRarityData.GetRarityColor(HarvestResultBridge.LatestRarity);
            var root = new GameObject("AcqCheerSparkle");
            root.transform.SetParent(player.transform, false);
            root.transform.localPosition = new Vector3(0f, 0.9f, 0f);   // 몸통 중심부 기준 반경 스폰
            var ps = CreateSparkleSystem(root.transform, color, FxCheerRadiusM, FxCheerParticleLifetimeS, FxCheerRatePerSec);
            var main = ps.main;
            main.duration = FxCheerDurationS;   // 발화 지속 ~4s — 세션(5s) 내 자연 소진
            main.loop = false;
            ps.Play();                          // 환호 발화 소유권 — SpawnCheerSparkle
            _cheerFxRoot = root;
        }

        /// <summary>
        /// [158차 P4-D] 지면 링 파동 — 발화 지점(플레이어 루트 y=FxGroundRingY)에서 수평 확산 링(계획 §P4-D "확장 링 2중, 히트감").
        ///  겹수 = GroundRingCount(tier): Common 1겹 / Rare+ 2겹. 제2파동은 AnimateFloat에서 FxGroundRingDelayS 스태거.
        ///  빔 셰이더 미적용(CreateFxQuad preferBeamShader:false) — 세로 그라데이션이 바닥 눕힌 링에 깊이 왜곡 → 폴백 셰이더 고정.
        ///  쿼드는 바닥에 눕힘(localRotation X=90°) — 탑다운 시 카메라 피치에 따라 타원 판독(의도된 지면 파동).
        ///  파괴 소유권 = EndPresentationInternal(player 자식 — cheer와 동일 명시 파괴).
        /// </summary>
        private static void SpawnGroundRings(GameObject player)
        {
            int tier = Mathf.Clamp((int)HarvestResultBridge.LatestRarity, 0, 5);
            Color rc = EquipmentRarityData.GetRarityColor(HarvestResultBridge.LatestRarity);
            _groundRingTint = new Color(rc.r, rc.g, rc.b, FxGroundRingAlpha);
            _groundRingEndM = GroundRingEndRadius(tier);
            var root = new GameObject("AcqGroundRings");
            root.transform.SetParent(player.transform, false);
            root.transform.localPosition = new Vector3(0f, FxGroundRingY, 0f);
            float startDia = FxGroundRingStartM * 2f;
            Renderer ringA = CreateFxQuad("AcqGroundRingA", root.transform, GetRingTexture(),
                new Vector2(startDia, startDia), _groundRingTint, Vector3.zero, false);
            ringA.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥 눕힘(XZ 평면)
            _groundRingA = ringA.transform;
            if (GroundRingCount(tier) >= 2)
            {
                Renderer ringB = CreateFxQuad("AcqGroundRingB", root.transform, GetRingTexture(),
                    new Vector2(startDia, startDia), _groundRingTint, Vector3.zero, false);
                ringB.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _groundRingB = ringB.transform;
            }
            _groundRingRoot = root;
            Debug.Log($"[AcquisitionCinema] [158차 P4-D] 지면 링 파동 스폰 — 겹수={GroundRingCount(tier)} 끝반경={_groundRingEndM:F2}m");
        }

        /// <summary>[158차 P4-D] 지면 링 1겹 확산 애니 — 지연 전 숨김(대기)·수명 후 비활성(멱등), 알파 0.45→0 페이드.</summary>
        private static void AnimateGroundRing(Transform ring, float elapsed, float delayS)
        {
            if (ring == null) return;
            float t = (elapsed - delayS) / FxGroundRingDurationS;
            if (t < 0f)
            {
                if (ring.gameObject.activeSelf) ring.gameObject.SetActive(false);   // 제2파동 대기
                return;
            }
            if (t >= 1f)
            {
                if (ring.gameObject.activeSelf) ring.gameObject.SetActive(false);   // 수명 종료
                return;
            }
            if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
            float dia = Mathf.Lerp(FxGroundRingStartM, _groundRingEndM, t) * 2f;
            ring.localScale = new Vector3(dia, dia, 1f);
            var rend = ring.GetComponent<MeshRenderer>();
            if (rend == null) return;
            Material m = rend.material;
            Color c = _groundRingTint;
            c.a = FxGroundRingAlpha * (1f - t);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.SetColor("_Color", c);
        }

        /// <summary>[153차 P4] 카메라 향해 Slerp 빌보드(FX 그룹용) — _floatVisual 기존 로직과 동일 수학(FloatBillboardSlerp 공유).</summary>
        private static void SlerpBillboard(Transform target)
        {
            var cam = Camera.main;
            if (cam == null || target == null) return;
            Vector3 toCam = cam.transform.position - target.position;
            if (toCam.sqrMagnitude <= 0.000001f) return;
            Quaternion face = Quaternion.LookRotation(toCam.normalized);
            target.rotation = Quaternion.Slerp(target.rotation, face, Mathf.Clamp01(Time.deltaTime * FloatBillboardSlerp));
        }

        /// <summary>[149차 P2] 부유 애니 — 천천히 상승+미세 사인 진동+종료 알파 페이드+카메라 빌보드(매 틱, 부드럽게).</summary>
        private static void AnimateFloat()
        {
            if (_floatItem == null) return;
            float elapsed = Mathf.Max(0f, Time.time - _floatStartTime);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, CheerHoldSeconds));
            float rise = FloatRiseM * t;
            float bob = Mathf.Sin(elapsed * FloatBobHz * 2f * Mathf.PI) * FloatBobAmplitudeM;
            _floatItem.transform.localPosition = _floatBaseLocal + Vector3.up * (rise + bob);

            // ── [153차 P4] 등장 팝 — pivot 스케일 0→피크≈1.15→1.0 back-out 이징(FxPopSeconds 동안만, 상승/진동과 합성) ──
            if (elapsed < FxPopSeconds)
            {
                float u = Mathf.Clamp01(elapsed / FxPopSeconds);
                float eased = 1f + (FxPopBackConst + 1f) * Mathf.Pow(u - 1f, 3f) + FxPopBackConst * Mathf.Pow(u - 1f, 2f);
                _floatItem.transform.localScale = Vector3.one * Mathf.Max(0.0001f, eased);
            }
            else if (_floatItem.transform.localScale != Vector3.one)
            {
                _floatItem.transform.localScale = Vector3.one;   // 팝 종료 확정(멱등)
            }

            if (_floatVisual != null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 toCam = cam.transform.position - _floatVisual.position;
                    if (toCam.sqrMagnitude > 0.000001f)
                    {
                        Quaternion face = Quaternion.LookRotation(toCam.normalized);
                        _floatVisual.rotation = Quaternion.Slerp(_floatVisual.rotation, face,
                            Mathf.Clamp01(Time.deltaTime * FloatBillboardSlerp));
                    }
                }
            }

            // ── [153차 P4] FX 그룹 빌보드 — _floatVisual과 동일 빌보드 계약을 그룹에 적용(쿼드 개별 회전 불요) ──
            if (_floatFxRoot != null)
                SlerpBillboard(_floatFxRoot.transform);

            // ── [153차 P4] 빛기둥 사인 바위가 — 빌보드 로컬 Z축 회전(좌우 대칭 위상) ──
            float swayDeg = Mathf.Sin(elapsed * FxShaftSwayHz * 2f * Mathf.PI) * FxShaftSwayDeg;
            if (_floatFxShaftL != null) _floatFxShaftL.localRotation = Quaternion.Euler(0f, 0f, swayDeg);
            if (_floatFxShaftR != null) _floatFxShaftR.localRotation = Quaternion.Euler(0f, 0f, -swayDeg);
            // ── [154차 P3] 코어층 바위가 — 외곽층과 반대 위상(동일 진폭·주파수) ──
            if (_floatFxShaftCoreL != null) _floatFxShaftCoreL.localRotation = Quaternion.Euler(0f, 0f, -swayDeg);
            if (_floatFxShaftCoreR != null) _floatFxShaftCoreR.localRotation = Quaternion.Euler(0f, 0f, swayDeg);
            // ── [154차 P3] 회전 링 — A/B 반대 방향 등속 스핀(화면 평면, 로컬 Z축 회전) ──
            if (_floatFxRingA != null) _floatFxRingA.Rotate(0f, 0f, FxRingAngularSpeedDeg * Time.deltaTime, Space.Self);
            if (_floatFxRingB != null) _floatFxRingB.Rotate(0f, 0f, -FxRingAngularSpeedDeg * Time.deltaTime, Space.Self);
            // ── [154차 P3] 등장 링 웨이브 — 팝 동기 확산(스케일 0.2→0.9m, 알파 0.6→0), 수명 후 비활성(멱등) ──
            if (_floatFxRingWave != null)
            {
                if (elapsed >= FxWaveDurationS)
                {
                    if (_floatFxRingWave.gameObject.activeSelf) _floatFxRingWave.gameObject.SetActive(false);
                }
                else
                {
                    float wt = elapsed / FxWaveDurationS;
                    float dia = Mathf.Lerp(FxWaveRadiusStartM, FxWaveRadiusEndM, wt) * 2f;
                    _floatFxRingWave.localScale = new Vector3(dia, dia, 1f);
                    Color wc = _waveBaseTint; wc.a = FxWaveStartAlpha * (1f - wt);
                    Material wm = _floatFxRingWave.GetComponent<MeshRenderer>() != null
                        ? _floatFxRingWave.GetComponent<MeshRenderer>().material : null;
                    if (wm != null)
                    {
                        if (wm.HasProperty("_BaseColor")) wm.SetColor("_BaseColor", wc);
                        else wm.SetColor("_Color", wc);
                    }
                }
            }

            // ── [158차 P4-D] 지면 링 파동 — 2겹 수평 확산(A 즉시/B FxGroundRingDelayS 지연), 수명 후 비활성(멱등) ──
            AnimateGroundRing(_groundRingA, elapsed, 0f);
            AnimateGroundRing(_groundRingB, elapsed, FxGroundRingDelayS);
            // ── [158차 P4-D] 코어 펄스 — _CoreIntensity 사인 진동(정적 2.0 과노출 → 숨쉬는 빛). 에픽+(3+) 진폭 ↑ ──
            float pulse = FxCorePulseMid + CorePulseAmplitude((int)HarvestResultBridge.LatestRarity)
                * Mathf.Sin(elapsed * FxCorePulseHz * 2f * Mathf.PI);
            for (int i = 0; i < _fxPulseMats.Count; i++)
            {
                var pm = _fxPulseMats[i];
                if (pm == null) continue;
                if (pm.HasProperty("_CoreIntensity")) pm.SetFloat("_CoreIntensity", pulse);
            }

            float remaining = _sessionEndTime - Time.time;
            if (remaining < FloatFadeSeconds)
            {
                float alpha = Mathf.Clamp01(remaining / FloatFadeSeconds);
                for (int i = 0; i < _floatRenderers.Count; i++)
                {
                    var rend = _floatRenderers[i];
                    if (rend == null) continue;
                    Material m = rend.material;
                    if (!m.HasProperty("_BaseColor") && !m.HasProperty("_Color")) continue;
                    Color c = _floatBaseColors[i];
                    c.a *= alpha;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    else m.SetColor("_Color", c);
                    m.renderQueue = 3000;   // 알파 블렌딩 후보 큐(셰이더 미지원 시 무해)
                }

                // ── [153차 P4] FX 렌더러 동일 알파 페이드 — 별도 리스트(_fxRenderers) 순회(③ 페이드 계약) ──
                for (int i = 0; i < _fxRenderers.Count; i++)
                {
                    var fxRend = _fxRenderers[i];
                    if (fxRend == null) continue;
                    Material fxMat = fxRend.material;
                    if (!fxMat.HasProperty("_BaseColor") && !fxMat.HasProperty("_Color")) continue;
                    Color fc = _fxBaseColors[i];
                    fc.a *= alpha;
                    if (fxMat.HasProperty("_BaseColor")) fxMat.SetColor("_BaseColor", fc);
                    else fxMat.SetColor("_Color", fc);
                }
            }
        }

        /// <summary>
        /// [144차 P9-ACQ → 149차 P2 승계] GLB 미매핑 폴백 구성(결정론) — 얼굴 위 부유 위치 SpriteRenderer(절차 아이콘) + TextMesh(아이템명).
        /// 아이콘은 기존 어휘 재사용: ProceduralIconGenerator.GetOrCreateIcon(C8-35 — ItemData.icon 서비스,
        /// 카테고리별 절차 생성이므로 null 없음). 아이콘 실패 시에도 아이템명 텍스트는 반드시 표시(생략 금지가 목적).
        /// 스프라이트 PPU 32 → 32px 아이콘 = 1m → 스케일 0.25 = 25cm(부유 가시 크기, GLB 정규화 0.35m 선례 준용).
        /// </summary>
        private static GameObject CreateFloatFallback(PlayerInventory.ItemData data)
        {
            var root = new GameObject("AcqFloatFallback");

            var icon = ProceduralIconGenerator.GetOrCreateIcon(data);
            if (icon != null)
            {
                var iconGo = new GameObject("AcqFallbackIcon");
                iconGo.transform.SetParent(root.transform, false);
                var sr = iconGo.AddComponent<SpriteRenderer>();
                sr.sprite = icon;
                sr.sortingOrder = 100;   // 캐릭터/지형 위 가시성 보장
                iconGo.transform.localScale = Vector3.one * 0.25f;
            }

            var textGo = new GameObject("AcqFallbackText");
            textGo.transform.SetParent(root.transform, false);
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = !string.IsNullOrEmpty(data.displayName) ? data.displayName : data.id;   // 표시명 없으면 id라도 표시
            tm.fontSize = 48;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            textGo.transform.localPosition = new Vector3(0f, 0.25f, 0f);   // 아이콘 위 25cm

            return root;
        }

        /// <summary>렌더러 바운드를 목표 높이로 정규화(부유 가시 크기 — 어선/약초/목재 크기 편차 흡수). 실패 시 스케일 0.5.</summary>
        private static void NormalizeScale(GameObject visual, float targetHeight)
        {
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
            {
                visual.transform.localScale = Vector3.one * 0.5f;
                return;
            }
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            // [130차] 최대 차원 기준 — 물고기는 가로가 길고 약초는 세로가 길다(높이 기준이면 물고기 과대).
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxDim < 0.0001f)
            {
                visual.transform.localScale = Vector3.one * 0.5f;
                return;
            }
            // 부모 본 스케일 영향 배제를 위한 월드 기준 보정 — SetParent(false) 후 로컬 적용
            float worldScale = visual.transform.lossyScale.x;
            float localScale = targetHeight / Mathf.Max(0.0001f, maxDim * Mathf.Max(0.0001f, worldScale));
            visual.transform.localScale = Vector3.one * Mathf.Clamp(localScale, 0.05f, 3f);
        }

        /// <summary>폴링 러너 — HarvestResultUTK Updater 선례(독립 GO, DontDestroyOnLoad).</summary>
        private class Updater : MonoBehaviour
        {
            private void Update() => Tick();
        }
    }
}
