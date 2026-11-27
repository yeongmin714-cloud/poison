using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 경작지 1칸 — 농경(재배) 상태머신 [26차 농경시스템 업그레이드 — docs/FARM_UPGRADE_PLAN.md].
    ///
    /// 상태 흐름 (모여라 동물의 숲 스타일):
    ///   Grass(잔디밭, 초기) ──굉이질 채널──▶ Tilled(구멍)
    ///   Dirt(흙땅, 수확/정리 후) ──굉이질 채널──▶ Tilled
    ///   Tilled ──파종 채널(씨앗 1 소비)──▶ Planted(평평한 흙길, 수분 100 시작)
    ///   Planted → Growing(성장 3일, 수분>0일 때만 진행) → Ready(수확 가능)
    ///   Growing/Ready ──수분 0──▶ Withered(시듦) ──정리 채널──▶ Dirt
    ///   Ready ──Ready 경과 7일 초과──▶ Rotted(부패) ──정리 채널──▶ Dirt
    ///   Growing/Ready ──수확 채널──▶ Dirt(기본 흙땅)
    ///
    /// - 모든 플레이어 상호작용은 GatherChannelSystem(파랑 진행바 — 채광/벌목과 동일) 경유.
    /// - 성장: TimeManager 절대 게임시간(CurrentDay*86400 + GameTime). 없으면 Time.time 폴백.
    /// - Ready 시 HerbPickup AddComponent → 기존 채집 흐름(LootBasket + EXP) 재사용.
    /// - 순수 판정 함수(ComputeGrowProgress/ComputeReadyElapsedDays/ComputeMoistureFraction)는
    ///   EditMode 테스트 대상 — MonoBehaviour 상태 없이 절대시간 스냅샷만으로 판정.
    /// </summary>
    public class FarmPlot : MonoBehaviour
    {
        public enum CropPhase { Grass, Dirt, Tilled, Planted, Growing, Ready, Withered, Rotted }

        [Header("경작지 설정")]
        [SerializeField] private CropPhase _phase = CropPhase.Grass;
        [SerializeField] private HerbPickup.HerbType _defaultCrop = HerbPickup.HerbType.Red;
        [SerializeField] private NationType _plotNation = NationType.East;   // 소속 영지(Inspector/Configure)
        [SerializeField] private int _plotIndex = 1;                         // 소속 영지 인덱스
        [SerializeField] private float _interactRange = 2.5f;

        [Header("성장/수분 설정 [26차]")]
        [SerializeField, Min(1)] private int _growDurationDay = 3;           // 숙성까지 필요한 게임 일수 (2→3일)
        [SerializeField, Min(1)] private int _rotGraceDays = 7;              // Ready 경과 후 부패까지 허용 일수

        // ===== 런타임 상태 =====
        private HerbPickup.HerbType _cropType = HerbPickup.HerbType.Red;
        private double _plantedGameTime = 0;     // Planted(파종) 시각
        private double _readyGameTime = 0;       // Ready 도달 시각 (부패 판정 기준)
        private double _lastMoistureTick = 0;    // 수분 드레인 누산 기준 시각
        private HerbPickup _herbRef;             // Ready 시 부착(기존 채집 시스템 재사용)

        // ===== [153차 P2] 채널 애니메이션 + 리듬 타격 피드백 상태 =====
        /// <summary>[153차 P2] FarmPlot 소유 채널 종류 — 채널 시작 시 세팅, 종료/이탈 후 None 정리.</summary>
        private FarmChannelFeedback _activeFarmChannel;
        /// <summary>[153차 P2] 채널 타격 이벤트 구독 플래그 — 구독/해제 짝 유지용(파괴 시 해제).</summary>
        private bool _farmImpactSubscribed;

        /// <summary>[153차 P2] FarmPlot 소유 채널 구분 — 애니 재발화 방식과 타격 피드백 종류를 함께 구분한다.</summary>
        private enum FarmChannelFeedback { None, TillSoil, PlantSoil, PourWater }

        // ===== 소유 검증 =====
        public bool IsOwned { get; private set; }
        private float _ownershipCheckTimer = OwnershipCheckInterval;  // Start 즉시 1회 검사 유도

        // ===== 시각 [153차 P1 — FarmPlotVisuals 위임 상태(internal: 동일 어셈블리 시각부가 판독/기록)] =====
        internal Transform _soilVisual;           // 흙 타일(Grass/Dirt/Tilled/Planted 공통 베이스)
        internal Transform _holeVisual;           // 굉이질 구멍(Tilled) 시각 — [153차 P1] 프로시저럴 격자 메시
        internal Transform _cropVisual;           // 허브(파종→성장→숙성/시듦/부패) 시각 — [153차 P3] GLB 승격 피벗
        internal Renderer[] _cropRenderers;       // [153차 P3] 작물 전 renderer — 틴트/기본색 적용 대상
        internal Color[] _cropGlbBaseColors;      // [153차 P3] GLB 기본색 캐시(건강 복원용 — Sphere 폴백은 null)
        internal bool _cropIsGlb;                 // [153차 P3] GLB 승격 여부 — 색 적용 경로 판별
        internal HerbPickup.HerbType _cropGlbBuiltForType;  // [153차 P3] 현재 시각이 승격된 작물 타입 — 불일치 시 재빌드
        internal int _cropGlbBuiltForStage;   // [무번호 183-P3] 현재 시각이 승격된 단계 — 성장 스왑 재빌드 판정
        internal Vector3 _cropBaseEuler;          // [153차 P3] 스웨이 기준 회전(선례 _baseVisualEuler)
        internal bool _cropBaseEulerSet;          // [153차 P3] 스웨이 기준 캡처 여부

        // ===== 플레이어 상호작용 =====
        private Transform _player;
        private float _playerRefreshTimer;
        private bool _playerNearby;

        // ===== 상태 메시지 =====
        private string _statusMsg = "";
        private float _statusUntil = 0f;

        // 근접 다수 플롯이 같은 화면 자리에 텍스트를 겹치지 않게 — 프레임당 대표 1플롯만 프롬프트 표시
        private static FarmPlot _guiOwner;
        private static int _guiOwnerFrame = -1;

        /// <summary>[151차 P3] 농경 상태 메시지 브로드캐스트 — Systems→UI 단방향 계약(UI가 구독).
        ///   NPCDialoguePanelUTK가 구독해 중앙 하단 대화판넬로 표출(OnGUI 근접 프롬프트는 조작 힌트로 유지).</summary>
        public static event System.Action<string> FarmStatusBroadcast;

        private const float OwnershipCheckInterval = 1.5f;
        private const float PlayerRefreshInterval = 2f;
        private const double SecondsPerGameDay = 86400.0;

        // ===== 채널 시간 [26차 채널링 규약 — docs/FARM_UPGRADE_PLAN.md §2] =====
        public const float TillChannelSeconds = 2f;      // 굉이질
        public const float PlantChannelSeconds = 1f;     // 파종
        public const float WaterChannelSeconds = 1.5f;   // 물주기
        public const float HarvestChannelSeconds = 2f;   // 수확
        public const float ClearChannelSeconds = 1.5f;   // 정리(시듦/부패)

        // ===== 공개 API =====

        public CropPhase Phase => _phase;
        public HerbPickup.HerbType CropType => _cropType;

        /// <summary>숙성까지 필요한 게임 일수(허브별 기본 노출용)</summary>
        public int GrowDurationDay
        {
            get => _growDurationDay;
            set => _growDurationDay = Mathf.Max(1, value);
        }

        /// <summary>Ready 경과 후 부패까지 허용 일수 [26차]</summary>
        public int RotGraceDays
        {
            get => _rotGraceDays;
            set => _rotGraceDays = Mathf.Max(1, value);
        }

        /// <summary>
        /// 외부(셋업/매니저) 초기화: 소속 영지 + 기본 작물 + 성장 일수 설정 후 소유 검증.
        /// 미소유 영지면 즉시 Grass 강제 + 상태 메시지.
        /// </summary>
        public void Configure(NationType nation, int index, HerbPickup.HerbType crop, int growDays)
        {
            _plotNation = nation;
            _plotIndex = index;
            _defaultCrop = crop;
            _cropType = crop;
            GrowDurationDay = growDays;

            ValidateOwnership(true);
        }

        /// <summary>
        /// 소유 검증: TerritoryDatabase의 영지 상태를 조회해 PlayerOwned 여부로 IsOwned 갱신.
        /// 미소유/상실 시 밭을 강제 Grass 초기화 + 상태 메시지.
        /// </summary>
        private void ValidateOwnership(bool force)
        {
            var db = TerritoryDatabase.Instance;
            var st = db != null ? db.GetState(_plotNation, _plotIndex) : null;
            bool owned = st != null && st.ownership == TerritoryOwnership.PlayerOwned;

            bool wasOwned = IsOwned;
            IsOwned = owned;

            if (!owned)
            {
                if (_phase != CropPhase.Grass)
                {
                    ResetToGrass("아군 영지에서만 경작 가능 — 소유 상실/미소유");
                    ShowStatus("⚠️ 아군 영지에서만 경작 가능");
                }

                if (force || wasOwned)
                    Debug.Log($"[FarmPlot] {name}: ({_plotNation} #{_plotIndex}) 아군 영지가 아님 — 경작 불가");
            }
            else if (force)
            {
                Debug.Log($"[FarmPlot] {name}: ({_plotNation} #{_plotIndex}) 플레이어 소유 영지 — 경작 가능");
            }
        }

        /// <summary>
        /// HerbType → 파종에 필요한 씨앗 ItemData 매핑 (PlayerInventory 정의).
        /// 씨앗 id는 "herb_seed_*" 규약을 따른다.
        /// </summary>
        private static PlayerInventory.ItemData SeedItemForCrop(HerbPickup.HerbType crop)
        {
            switch (crop)
            {
                case HerbPickup.HerbType.Red:    return PlayerInventory.Seed_Red;
                case HerbPickup.HerbType.Purple: return PlayerInventory.Seed_Purple;
                case HerbPickup.HerbType.Yellow: return PlayerInventory.Seed_Yellow;
                case HerbPickup.HerbType.Silver: return PlayerInventory.Seed_Silver;
                case HerbPickup.HerbType.Green:  return PlayerInventory.Seed_Green;
                default:                         return PlayerInventory.Seed_Red;
            }
        }

        // ===== 순수 판정 함수 [26차 — EditMode 테스트 대상] =====

        /// <summary>성장 진행률(0~1). plantedToNow 경과(초)와 성장 일수만으로 판정.</summary>
        public static float ComputeGrowProgress01(double plantedToNow, int growDays)
        {
            double target = System.Math.Max(1, growDays) * SecondsPerGameDay;
            return Mathf.Clamp01((float)(plantedToNow / target));
        }

        /// <summary>Ready 경과 일수(내림). 부패 판정용.</summary>
        public static int ComputeReadyElapsedDays(double readyToNow)
        {
            if (readyToNow < 0.0) return 0;
            return (int)(readyToNow / SecondsPerGameDay);
        }

        /// <summary>
        /// 수분 잔량(0~1) — lastTickToNow 경과(초) 동안 1일에 걸쳐 리니어 드레인.
        /// 물주기 시 시작값 1.0으로 재기준.
        /// </summary>
        public static float ComputeMoistureFraction(double lastTickToNow)
        {
            if (lastTickToNow <= 0.0) return 1f;
            return Mathf.Clamp01(1f - (float)(lastTickToNow / SecondsPerGameDay));
        }

        /// <summary>부패 판정 — Ready 경과가 허용 일수(초 단위)를 초과하면 부패 [26차: "일주일을 넘기면"].</summary>
        public static bool IsRotten(double readyToNow, int graceDays)
        {
            if (readyToNow < 0.0) return false;
            return readyToNow > (double)System.Math.Max(1, graceDays) * SecondsPerGameDay;
        }

        // ===== 상태 조작 (채널 완료 콜백에서 호출) =====

        /// <summary>굉이질 완료 — Grass/Dirt → Tilled(구멍) [26차].</summary>
        public void CompleteTill()
        {
            if (_phase != CropPhase.Grass && _phase != CropPhase.Dirt) return;
            _phase = CropPhase.Tilled;
            RefreshVisuals();
            ShowStatus("⛏️ 굉이질 완료 — 구멍이 생겼다. 씨앗을 뿌릴 수 있다");
            Debug.Log($"[FarmPlot] ⛏️ {name}: 굉이질 완료 → Tilled(구멍)");
        }

        /// <summary>파종 완료 — Tilled → Planted(평평한 흙길, 수분 100) [26차].</summary>
        public void CompletePlant()
        {
            if (_phase != CropPhase.Tilled) return;

            var seed = SeedItemForCrop(_defaultCrop);
            var inv = PlayerInventory.Instance;
            if (seed == null || inv == null || !inv.HasItem(seed.id))
            {
                ShowStatus("씨앗이 부족합니다 — 채집/상점에서 씨앗을 구하세요");
                return;   // Tilled 유지 — 씨앗 없으면 파종 진행 안 됨
            }

            inv.RemoveItem(seed.id, 1);
            Debug.Log($"[FarmPlot] 🌰 {name}: {seed.displayName} 1개 소비 → 파종 (남은 씨앗: {inv.GetItemCount(seed.id)}개)");

            _cropType = _defaultCrop;
            _plantedGameTime = CurrentClock();
            _readyGameTime = 0;
            _lastMoistureTick = _plantedGameTime;
            _phase = CropPhase.Planted;
            RefreshVisuals();
            ShowStatus($"🌱 {_cropType} 파종! (성장 {GrowDurationDay}일 — 매일 물주기)");
            Debug.Log($"[FarmPlot] 🌱 {name}: {_cropType} 파종 (게임 {GrowDurationDay}일 후 수확, " +
                      $"시간원={(TimeManager.Instance != null ? "TimeManager 연동" : "Time.time 실시간 폴백")})");
        }

        /// <summary>물주기 완료 — 수분 100% 충전 [26차].</summary>
        public void CompleteWater()
        {
            if (_phase != CropPhase.Planted && _phase != CropPhase.Growing && _phase != CropPhase.Ready) return;
            _lastMoistureTick = CurrentClock();
            ShowStatus("💧 물을 주었다 — 수분 100%");
            Debug.Log($"[FarmPlot] 💧 {name}: 물주기 완료 — 수분 100% 충전");
        }

        /// <summary>물뿌리개 물주기 완료 — 저장량 1 소비 + 수분 100% 충전 [26차 Phase 4].</summary>
        public void CompleteWaterWithCan()
        {
            if (_phase != CropPhase.Planted && _phase != CropPhase.Growing && _phase != CropPhase.Ready) return;
            if (!WateringCanSystem.Consume(1))
            {
                ShowStatus("물뿌리개에 물이 없습니다 — 호수에서 충전하세요");
                return;
            }

            // [133차] 물 붓는 피드백 — Consume 성공 직후 물방울 파티클+물 소리(셰이크 없음: 농경은 근접 화면이라 과함).
            GatherFeedbackSystem.PlayImpact(GatherFeedbackKind.WaterPour,
                transform.position + Vector3.up * 0.3f, shake: false);

            CompleteWater();
            Debug.Log($"[FarmPlot] 💧 {name}: 물뿌리개 사용 (남은 물: {WateringCanSystem.Water}/{WateringCanSystem.Max})");
        }

        /// <summary>정리 완료 — Withered/Rotted → Dirt [26차].</summary>
        public void CompleteClear()
        {
            if (_phase != CropPhase.Withered && _phase != CropPhase.Rotted) return;
            SetPhaseDirt(_phase == CropPhase.Withered ? "시든 작물 정리" : "부패 작물 정리");
        }

        /// <summary>수확 완료 경로 — HerbPickup 수확 시그널 → Dirt [26차: Empty→Dirt 변경].</summary>
        private void OnHarvested()
        {
            SetPhaseDirt($"{_cropType} 수확 완료");
        }

        private void SetPhaseDirt(string reason)
        {
            _phase = CropPhase.Dirt;
            _plantedGameTime = 0;
            _readyGameTime = 0;
            _lastMoistureTick = 0;

            if (_herbRef != null)
            {
                _herbRef.OnHarvestStarted -= OnHarvested;
                Destroy(_herbRef);   // 프레임 말 파괴 → HerbPickup의 Respawn 예약도 함께 해제, 다음 Ready에 재부착
                _herbRef = null;
            }

            RefreshVisuals();

            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[FarmPlot] {name}: 밭 초기화 → Dirt ({reason})");
        }

        private void ResetToGrass(string reason)
        {
            _phase = CropPhase.Grass;
            _plantedGameTime = 0;
            _readyGameTime = 0;
            _lastMoistureTick = 0;

            if (_herbRef != null)
            {
                _herbRef.OnHarvestStarted -= OnHarvested;
                Destroy(_herbRef);
                _herbRef = null;
            }

            RefreshVisuals();

            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[FarmPlot] {name}: 밭 초기화 → Grass ({reason})");
        }

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            // y 보정: Test_10의 SurfaceY(x,z)+0.1 계약 — 배치 y가 수식 표면보다 낮으면 보정(흙타일 지면 박힘)
            float surfaceY = TryGetSurfaceY(transform.position.x, transform.position.z);
            if (transform.position.y < surfaceY + 0.02f)
                transform.position = new Vector3(transform.position.x, surfaceY + 0.1f, transform.position.z);

            BuildSoilVisual();
            BuildHoleVisual();
            BuildCropVisual();
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            ValidateOwnership(true);
            RefreshVisuals();
        }

        /// <summary>[153차 P2] 파괴 시 채널 타격 구독 해제 — 이벤트 잔존 구독 방지.</summary>
        private void OnDestroy()
        {
            if (!_farmImpactSubscribed) return;
            GatherChannelSystem.Instance.OnImpact -= OnFarmChannelImpact;
            _farmImpactSubscribed = false;
            _activeFarmChannel = FarmChannelFeedback.None;
        }

        private void Update()
        {
            // 1) 소유 검증(주기적) — 영지 상실 시 강제 Grass 초기화
            _ownershipCheckTimer += Time.deltaTime;
            if (_ownershipCheckTimer >= OwnershipCheckInterval)
            {
                _ownershipCheckTimer = 0f;
                ValidateOwnership(false);
            }

            // 2) 상태 전이 [26차]
            TickPhase();

            // 3) 플레이어 근접 + E키 — 채널 게이트 시작
            UpdatePlayerProximity();

            // [153차 P2] 채널 타격 리듬 구독 관리 — HerbPickup 선례 패턴(ActiveTarget 귀속 판정, 매 프레임).
            //   FarmPlot이 시작한 채널(굉이질/파종/정리/물주기)만 구독 — 수확 채널은 Ready 단계에서
            //   부착된 약초 오브젝트가 자체 처리하므로 이중 배선이 생기지 않는다.
            bool isFarmChannelTarget = GatherChannelSystem.Instance.ActiveTarget == transform;
            if (isFarmChannelTarget && !_farmImpactSubscribed)
            {
                GatherChannelSystem.Instance.OnImpact += OnFarmChannelImpact;
                _farmImpactSubscribed = true;
            }
            else if (!isFarmChannelTarget && _farmImpactSubscribed)
            {
                GatherChannelSystem.Instance.OnImpact -= OnFarmChannelImpact;
                _farmImpactSubscribed = false;
                _activeFarmChannel = FarmChannelFeedback.None;   // 채널 종료/이탈 — 추적 정리.
            }

            if (_playerNearby && Input.GetKeyDown(KeyCode.E))
            {
                if (!IsOwned)
                {
                    ShowStatus("⚠️ 아군 영지에서만 경작 가능");
                }
                else
                {
                    TryBeginContextChannel();
                }
            }

            // [153차 P3] 작물 바람 스웨이 틱 훅 — 기존 로직 뒤 1줄(ResourceNode.ApplyWoodSway 수학 재사용)
            FarmPlotVisuals.Tick(this);
        }

        /// <summary>상태 전이 틱 — 성장 진행/수분 드레인/부패/시듦 판정 [26차].</summary>
        private void TickPhase()
        {
            switch (_phase)
            {
                case CropPhase.Planted:
                    _phase = CropPhase.Growing;          // 파종 직후 다음 틱에서 Growing으로
                    RefreshVisuals();
                    break;

                case CropPhase.Growing:
                {
                    double now = CurrentClock();
                    // 수분 0 → 시듦 (성장 진행 전 판정 — 말라죽은 작물은 성장하지 않는다)
                    if (ComputeMoistureFraction(now - _lastMoistureTick) <= 0f)
                    {
                        _phase = CropPhase.Withered;
                        RefreshVisuals();
                        ShowStatus("🥀 물이 부족해 작물이 말라죽었다 — [E] 정리");
                        Debug.Log($"[FarmPlot] 🥀 {name}: 수분 고갈 → Withered");
                        break;
                    }
                    if (ComputeGrowProgress01(now - _plantedGameTime, _growDurationDay) >= 1f)
                        TransitionToReady();
                    else
                        RefreshVisuals();
                    break;
                }

                case CropPhase.Ready:
                {
                    double now = CurrentClock();
                    // 수분 0 → 시듦 (Ready에서도 방치하면 말라죽는다)
                    if (ComputeMoistureFraction(now - _lastMoistureTick) <= 0f)
                    {
                        _phase = CropPhase.Withered;
                        if (_herbRef != null)
                        {
                            _herbRef.OnHarvestStarted -= OnHarvested;
                            Destroy(_herbRef);
                            _herbRef = null;
                        }
                        RefreshVisuals();
                        ShowStatus("🥀 물이 부족해 작물이 말라죽었다 — [E] 정리");
                        Debug.Log($"[FarmPlot] 🥀 {name}: 수분 고갈 → Withered (Ready 방치)");
                        break;
                    }
                    // Ready 경과 7일 초과 → 부패
                    if (IsRotten(now - _readyGameTime, _rotGraceDays))
                    {
                        _phase = CropPhase.Rotted;
                        if (_herbRef != null)
                        {
                            _herbRef.OnHarvestStarted -= OnHarvested;
                            Destroy(_herbRef);
                            _herbRef = null;
                        }
                        RefreshVisuals();
                        ShowStatus("🧟 수확 시기를 놓쳐 작물이 부패했다 — [E] 정리");
                        Debug.Log($"[FarmPlot] 🧟 {name}: Ready {_rotGraceDays}일 초과 → Rotted");
                    }
                    break;
                }
            }
        }

        /// <summary>상태별 컨텍스트 채널 시작 [26차 — 모든 농경 상호작용은 채널 게이트].</summary>
        private void TryBeginContextChannel()
        {
            // Ready: 부착된 HerbPickup이 자체 E키 채널 수확을 처리 — 중복 시작 방지
            if (_phase == CropPhase.Ready && _herbRef != null) return;

            switch (_phase)
            {
                case CropPhase.Grass:
                case CropPhase.Dirt:
                    GatherChannelSystem.Instance.Begin(
                        transform, CompleteTill, FarmPlot.TillChannelSeconds);
                    // [153차 P2] 채널 시작 직후 굉이질 애니 발화 + 타격 리듬(흙) 추적 세팅
                    _activeFarmChannel = FarmChannelFeedback.TillSoil;
                    FireChannelStartAnim(false);
                    break;

                case CropPhase.Tilled:
                    // 씨앗 사전 검증 — 채널 낭비 방지(상호작용불가=피드백 필수 계약)
                    var seed = SeedItemForCrop(_defaultCrop);
                    var inv = PlayerInventory.Instance;
                    if (seed == null || inv == null || !inv.HasItem(seed.id))
                    {
                        ShowStatus("씨앗이 부족합니다 — 채집/상점에서 씨앗을 구하세요");
                        break;
                    }
                    GatherChannelSystem.Instance.Begin(
                        transform, CompletePlant, FarmPlot.PlantChannelSeconds);
                    // [153차 P2] 채널 시작 직후 파종(채집형) 애니 발화 + 타격 리듬(흙) 추적 세팅
                    _activeFarmChannel = FarmChannelFeedback.PlantSoil;
                    FireChannelStartAnim(true);
                    break;

                case CropPhase.Planted:
                case CropPhase.Growing:
                case CropPhase.Ready:
                    // 물뿌리개 + 물 저장량 사전 검증 [26차 Phase 4] — 상호작용불가=피드백 필수 계약
                    if (!WateringCanSystem.HasCan())
                    {
                        ShowStatus("물뿌리개가 필요합니다 — 상점에서 구하세요");
                        break;
                    }
                    if (!WateringCanSystem.HasWater())
                    {
                        ShowStatus("물뿌리개에 물이 없습니다 — 호수에서 충전하세요");
                        break;
                    }
                    GatherChannelSystem.Instance.Begin(
                        transform, CompleteWaterWithCan, FarmPlot.WaterChannelSeconds);
                    // [153차 P2] 물주기 — 애니 발화 없음(WaterPour 피드백만). 타격 리듬 추적 세팅.
                    _activeFarmChannel = FarmChannelFeedback.PourWater;
                    break;

                case CropPhase.Withered:
                case CropPhase.Rotted:
                    GatherChannelSystem.Instance.Begin(
                        transform, CompleteClear, FarmPlot.ClearChannelSeconds);
                    // [153차 P2] 채널 시작 직후 정리(굉이질형) 애니 발화 + 타격 리듬(흙) 추적 세팅
                    _activeFarmChannel = FarmChannelFeedback.TillSoil;
                    FireChannelStartAnim(false);
                    break;
            }
        }

        // ===== [153차 P2] 채널 애니메이션 + 리듬 타격 피드백 =====

        /// <summary>[153차 P2] 채널 시작 직후 플레이어 애니 발화 — 채널 종류별 1회.
        /// 플레이어 HumanoidClipDriver 취득은 HerbPickup 채널 시작 선례(GetComponent → 자식 폴백) 준용.</summary>
        private void FireChannelStartAnim(bool gatherAnim)
        {
            if (_player == null) return;
            var driver = _player.GetComponent<HumanoidClipDriver>()
                ?? _player.GetComponentInChildren<HumanoidClipDriver>();
            if (driver == null) return;
            if (gatherAnim) driver.TriggerGather();   // 파종 — 채집형 동작
            else driver.TriggerTill();                // 굉이질/정리 — 굉이질 동작
        }

        /// <summary>[153차 P2] 채널 타격 리듬 핸들러 — 굉이질/정리·파종은 동작 재시작 + 흙 피드백,
        /// 물주기는 물방울 피드백만(애니 재발화 없음).</summary>
        private void OnFarmChannelImpact()
        {
            if (this == null) return;   // 파괴 후 잔여 발화 방어(Unity fake-null 포함).
            Vector3 impactPos = transform.position + Vector3.up * 1f;   // 플롯 상단 +1m — HerbPickup 선례 오프셋.
            switch (_activeFarmChannel)
            {
                case FarmChannelFeedback.TillSoil:
                    ReplayFarmAction("Till");
                    GatherFeedbackSystem.PlayImpact(GatherFeedbackKind.Soil, impactPos);
                    break;

                case FarmChannelFeedback.PlantSoil:
                    ReplayFarmAction("Gather");
                    GatherFeedbackSystem.PlayImpact(GatherFeedbackKind.Soil, impactPos);
                    break;

                case FarmChannelFeedback.PourWater:
                    GatherFeedbackSystem.PlayImpact(GatherFeedbackKind.WaterPour, impactPos, shake: false);
                    break;
            }
        }

        /// <summary>[153차 P2] 채널 리듬 동작 유지 — 플레이어 클립 드라이버 상태를 0프레임부터 재시작.</summary>
        private void ReplayFarmAction(string stateName)
        {
            if (_player == null) return;
            var driver = _player.GetComponent<HumanoidClipDriver>()
                ?? _player.GetComponentInChildren<HumanoidClipDriver>();
            driver?.ReplayAction(stateName);
        }

        /// <summary>(선택) 즉시 수확 — 테스트/병사 워커용. Ready가 아니면 무시 [26차 계승].</summary>
        public void HarvestDirectly()
        {
            if (_phase != CropPhase.Ready)
            {
                Debug.LogWarning($"[FarmPlot] {name}: 현재 {_phase} — 즉시 수확은 Ready에서만 가능");
                return;
            }

            if (_herbRef == null) AttachHerbPickup();
            _herbRef?.TryAutoGather(out _, out _);   // OnHarvestStarted → OnHarvested → Dirt 리셋
        }

        private void TransitionToReady()
        {
            _phase = CropPhase.Ready;
            _readyGameTime = CurrentClock();
            AttachHerbPickup();
            RefreshVisuals();
            ShowStatus($"🧺 {_cropType} 수확 준비 완료 — [E] 수확! (7일 내)");
            Debug.Log($"[FarmPlot] 🌾 {name}: {_cropType} 숙성 완료! (HerbPickup 부착 — E키 수확, 부패까지 {_rotGraceDays}일)");
        }

        // ===== HerbPickup 연동 =====

        private void AttachHerbPickup()
        {
            if (_herbRef != null) return;

            _herbRef = GetComponent<HerbPickup>();
            if (_herbRef == null)
                _herbRef = gameObject.AddComponent<HerbPickup>();

            // _herbType은 private SerializeField — 프로젝트 관례대로 리플렉션으로 설정
            var hf = typeof(HerbPickup).GetField("_herbType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hf?.SetValue(_herbRef, _cropType);

            _herbRef.OnHarvestStarted += OnHarvested;
        }

        // ===== 시간 =====

        /// <summary>
        /// 절대 게임시간. TimeManager.GameTime은 [0, 86400)으로 정규화되고 일수는 CurrentDay에 누적되므로
        /// 합산해야 자정 롤오버/수면 점프에도 성장 일수가 정확하다.
        /// TimeManager.Instance가 없으면 Time.time 실시간 폴백(성장은 실시간 경과).
        /// </summary>
        private static double CurrentClock()
        {
            var tm = TimeManager.Instance;
            if (tm == null) return Time.time;
            return (double)tm.CurrentDay * SecondsPerGameDay + tm.GameTime;
        }

        // ===== 시각 =====

        /// <summary>[153차 P1] 흙 타일 빌드 — FarmPlotVisuals 위임(시각부 분리 — 상태머신은 FarmPlot 유지).</summary>
        private void BuildSoilVisual() => FarmPlotVisuals.BuildSoilVisual(this);

        /// <summary>[153차 P1] 굉이질 구멍 시각 — FarmPlotVisuals 위임(Rim 4Cube+Pit Cube 폐기 → 단일 프로시저럴 격자 메시).</summary>
        private void BuildHoleVisual() => FarmPlotVisuals.BuildHoleVisual(this);

        /// <summary>[153차 P3] 작물 시각 — FarmPlotVisuals 위임(Sphere 폐기 → GLB 승격, 실패 시 Sphere 폴백).</summary>
        private void BuildCropVisual() => FarmPlotVisuals.BuildCropVisual(this);

        /// <summary>상태별 시각 갱신 [26차 — 구멍/흙길/작물 통합] — [153차 P1] FarmPlotVisuals 위임.
        ///   기존 계약 유지: 구멍은 Tilled에서만 SetActive, 작물 스케일 0.12→0.4→0.8, 시듦/부패 강제색.</summary>
        private void RefreshVisuals() => FarmPlotVisuals.RefreshVisuals(this);

        internal Color? _cropColorOverride;   // [153차 P1] 시듦/부패 강제색 — null이면 작물 기본색(FarmPlotVisuals 소비)

        internal float GrowProgress01()   // [153차 P1] FarmPlotVisuals 소비 — internal(동일 어셈블리)
        {
            return ComputeGrowProgress01(CurrentClock() - _plantedGameTime, _growDurationDay);
        }

        internal float MoistureFraction()   // [153차 P1] FarmPlotVisuals 소비 — internal(동일 어셈블리)
        {
            return ComputeMoistureFraction(CurrentClock() - _lastMoistureTick);
        }

        // [153차 P1] GetSoilColor/GetCropColor는 FarmPlotVisuals로 이관(흙/작물 팔레트 단일 소유).

        // ===== 플레이어 근접 =====

        private void UpdatePlayerProximity()
        {
            _playerRefreshTimer -= Time.deltaTime;
            if (_player == null && _playerRefreshTimer <= 0f)
            {
                _playerRefreshTimer = PlayerRefreshInterval;
                var playerGO = GameObject.FindGameObjectWithTag("Player");
                if (playerGO != null) _player = playerGO.transform;
            }

            _playerNearby = _player != null &&
                            Vector3.Distance(transform.position, _player.position) <= _interactRange;
        }

        // ===== 상태 메시지 =====

        private void ShowStatus(string message)
        {
            _statusMsg = message;
            _statusUntil = Time.time + 3.5f;
            FarmStatusBroadcast?.Invoke(message);   // [151차 P3] 대화판넬(중앙 하단) 동시 표출 — 문구 문자열 무변경
        }

        private void OnGUI()
        {
            if (!_playerNearby) return;

            // 프레임당 근접 플롯 1개만 대표 표시(라벨 겹침 방지)
            if (_guiOwnerFrame != Time.frameCount)
            {
                _guiOwnerFrame = Time.frameCount;
                _guiOwner = null;
            }
            if (_guiOwner == null) _guiOwner = this;
            if (_guiOwner != this) return;

            string prompt;
            if (!IsOwned)
                prompt = "⚠️ 아군 영지에서만 경작 가능";
            else switch (_phase)
            {
                case CropPhase.Grass:
                case CropPhase.Dirt:
                    prompt = "⛏️ [E] 굉이질 — 땅을 판다 (2초)";
                    break;
                case CropPhase.Tilled:
                    prompt = $"🌱 [E] 파종 — {_defaultCrop} 씨앗 (1초)";
                    break;
                case CropPhase.Planted:
                case CropPhase.Growing:
                {
                    float m = MoistureFraction() * 100f;
                    prompt = _phase == CropPhase.Planted
                        ? $"🌱 {_cropType} 발아 — 성장 중… 💧수분 {m:F0}%"
                        : $"🌾 {_cropType} 성장 중… ({GrowProgress01():P0}) 💧수분 {m:F0}%";
                    break;
                }
                case CropPhase.Ready:
                    prompt = $"🧺 [E] 수확 — {_cropType} (부패 전 {RemainingRotDays():F1}일)";
                    break;
                case CropPhase.Withered:
                    prompt = "🥀 말라죽은 작물 — [E] 정리 (1.5초)";
                    break;
                case CropPhase.Rotted:
                    prompt = "🧟 부패한 작물 — [E] 정리 (1.5초)";
                    break;
                default:
                    prompt = "";
                    break;
            }

            GUI.Label(new Rect(14f, 10f, 460f, 24f), prompt);

            if (!string.IsNullOrEmpty(_statusMsg) && Time.time < _statusUntil)
                GUI.Label(new Rect(14f, 34f, 460f, 24f), _statusMsg);
        }

        private float RemainingRotDays()
        {
            double elapsed = CurrentClock() - _readyGameTime;
            return Mathf.Max(0f, (float)(_rotGraceDays - elapsed / SecondsPerGameDay));
        }

        // ===== 지형 계약 (TestTerritoryCombatSetup.SurfaceY와 동일 수식) =====

        private static float TryGetSurfaceY(float x, float z)
        {
            try
            {
                return 1f + TerrainGenerator.GetHeightAt(x, z, BiomeType.Plains, 42);
            }
            catch
            {
                return 1f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsOwned ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}
