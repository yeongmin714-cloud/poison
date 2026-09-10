using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;

namespace ProjectName.Systems
{
    /// <summary>
    /// 경작지 1칸 — 농경(재배) 상태머신.
    /// Empty(빈 밭) → Seeded(파종) → Growing(성장) → Ready(숙성) → 수확(HerbPickup 재사용) → Empty.
    ///
    /// - 플레이어 근접 + E키: Empty에서 파종 / Ready에서는 부착된 HerbPickup이 자체 E키 Harvest() 처리.
    /// - 소유 검증: TerritoryDatabase.GetState(nation, index).ownership == PlayerOwned 인
    ///   내 영지(PlayerOwned)에서만 경작 가능. 미소유/상실 시 밭 강제 Empty 초기화 + 메시지.
    /// - 성장: TimeManager의 절대 게임시간(CurrentDay*86400 + GameTime, 자정 롤오버 안전)로
    ///   게임 일수 경과를 계산. TimeManager가 없으면 Time.time 실시간 폴백.
    /// - Ready 시 HerbPickup AddComponent + 리플렉션 _herbType 설정 → 기존 채집 흐름
    ///   (E키 → LootBasket.Create + Random yield + EXP)을 그대로 재사용.
    ///   OnHarvestStarted 구독으로 수확 감지 → 밭 Empty 리셋 + HerbPickup 제거(다음 Ready 재부착).
    /// </summary>
    public class FarmPlot : MonoBehaviour
    {
        public enum CropPhase { Empty, Seeded, Growing, Ready }

        [Header("경작지 설정")]
        [SerializeField] private CropPhase _phase = CropPhase.Empty;
        [SerializeField] private HerbPickup.HerbType _defaultCrop = HerbPickup.HerbType.Red;
        [SerializeField] private NationType _plotNation = NationType.East;   // 소속 영지(Inspector/Configure)
        [SerializeField] private int _plotIndex = 1;                         // 소속 영지 인덱스
        [SerializeField] private float _interactRange = 2.5f;

        [Header("성장 설정")]
        [SerializeField, Min(1)] private int _growDurationDay = 2;           // 숙성까지 필요한 게임 일수

        // ===== 런타임 상태 =====
        private HerbPickup.HerbType _cropType = HerbPickup.HerbType.Red;
        private double _seededGameTime = 0;
        private HerbPickup _herbRef;                  // Ready 시 부착(기존 채집 시스템 재사용)

        // ===== 소유 검증 =====
        public bool IsOwned { get; private set; }
        private float _ownershipCheckTimer = OwnershipCheckInterval;  // Start 즉시 1회 검사 유도

        // ===== 시각 =====
        private Transform _soilVisual;                // 흙타일
        private Transform _cropVisual;                // 허브(씨앗→성장→숙성) 시각
        private Renderer _cropRenderer;

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

        private const float OwnershipCheckInterval = 1.5f;
        private const float PlayerRefreshInterval = 2f;
        private const double SecondsPerGameDay = 86400.0;

        // ===== 공개 API =====

        public CropPhase Phase => _phase;
        public HerbPickup.HerbType CropType => _cropType;

        /// <summary>숙성까지 필요한 게임 일수(허브별 기본 노출용)</summary>
        public int GrowDurationDay
        {
            get => _growDurationDay;
            set => _growDurationDay = Mathf.Max(1, value);
        }

        /// <summary>
        /// 외부(셋업/매니저) 초기화: 소속 영지 + 기본 작물 + 성장 일수 설정 후 소유 검증.
        /// 미소유 영지면 즉시 Empty 강제 + 상태 메시지.
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
        /// force=true: Configure/Start 등 직후 1회 확정 검사(상태 로그 포함).
        /// force=false: Update 주기 검사(OwnershipCheckInterval) — 상실 시에만 로그/리셋.
        /// 미소유/상실 시 밭을 강제 Empty 초기화(SetPhaseEmpty 재사용) + 상태 메시지.
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
                if (_phase != CropPhase.Empty)
                {
                    SetPhaseEmpty("아군 영지에서만 경작 가능 — 소유 상실/미소유");
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

        /// <summary>
        /// 파종(Empty → Seeded). 소유 영지 + 해당 작물의 씨앗 1개 소비 필요.
        /// 씨앗이 없으면 상태 메시지만 표시하고 phase를 변경하지 않는다.
        /// </summary>
        public void Plant(HerbPickup.HerbType crop)
        {
            if (_phase != CropPhase.Empty) return;

            if (!IsOwned)
            {
                ShowStatus("아군 영지에서만 경작 가능");
                return;
            }

            // 씨앗 검사: 인벤토리에 해당 작물의 씨앗이 있어야 파종 가능 (phase 변경 없음)
            var seed = SeedItemForCrop(crop);
            var inv = PlayerInventory.Instance;
            if (seed == null || inv == null || !inv.HasItem(seed.id))
            {
                ShowStatus("씨앗이 부족합니다 — 채집/상점에서 씨앗을 구하세요");
                Debug.Log($"[FarmPlot] {name}: {crop} 파종 실패 — 씨앗 없음 ({seed?.displayName ?? crop + " 씨앗"})");
                return;
            }

            // 씨앗 1개 소비 후 파종 진행
            inv.RemoveItem(seed.id, 1);
            Debug.Log($"[FarmPlot] 🌰 {name}: {seed.displayName} 1개 소비 → 파종 (남은 씨앗: {inv.GetItemCount(seed.id)}개)");

            _cropType = crop;
            _seededGameTime = CurrentClock();
            _phase = CropPhase.Seeded;
            RefreshCropVisual();
            ShowStatus($"🌱 {_cropType} 파종! (성장 {GrowDurationDay}일)");
            Debug.Log($"[FarmPlot] 🌱 {name}: {crop} 파종 (게임 {GrowDurationDay}일 후 수확, " +
                      $"시간원={(TimeManager.Instance != null ? "TimeManager 연동" : "Time.time 실시간 폴백")})");
        }

        /// <summary>(선택) 즉시 수확 — 테스트용. Ready가 아니면 무시.</summary>
        public void HarvestDirectly()
        {
            if (_phase != CropPhase.Ready)
            {
                Debug.LogWarning($"[FarmPlot] {name}: 현재 {_phase} — 즉시 수확은 Ready에서만 가능");
                return;
            }

            if (_herbRef == null) AttachHerbPickup();
            _herbRef?.TryAutoGather(out _, out _);   // OnHarvestStarted → OnHarvested → Empty 리셋
        }

        // ===== Unity Lifecycle =====

        private void Awake()
        {
            // y 보정: Test_10의 SurfaceY(x,z)+0.1 계약 — 배치 y가 수식 표면보다 낮으면 보정(흙타일 지면 박힘)
            float surfaceY = TryGetSurfaceY(transform.position.x, transform.position.z);
            if (transform.position.y < surfaceY + 0.02f)
                transform.position = new Vector3(transform.position.x, surfaceY + 0.1f, transform.position.z);

            BuildSoilVisual();
            BuildCropVisual();
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            ValidateOwnership(true);
            RefreshCropVisual();
        }

        private void Update()
        {
            // 1) 소유 검증(주기적) — 영지 상실 시 강제 Empty 초기화
            _ownershipCheckTimer += Time.deltaTime;
            if (_ownershipCheckTimer >= OwnershipCheckInterval)
            {
                _ownershipCheckTimer = 0f;
                ValidateOwnership(false);
            }

            // 2) 상태 전이
            if (_phase == CropPhase.Seeded)
            {
                _phase = CropPhase.Growing;          // 파종 직후 다음 Update에서 Growing으로
                RefreshCropVisual();
            }
            else if (_phase == CropPhase.Growing)
            {
                int elapsedDay = (int)((CurrentClock() - _seededGameTime) / SecondsPerGameDay);
                if (elapsedDay >= _growDurationDay)
                    TransitionToReady();
                else
                    RefreshCropVisual();
            }

            // 3) 플레이어 근접 + E키
            UpdatePlayerProximity();

            if (_playerNearby && Input.GetKeyDown(KeyCode.E))
            {
                if (!IsOwned)
                {
                    ShowStatus("⚠️ 아군 영지에서만 경작 가능");
                }
                else if (_phase == CropPhase.Empty)
                {
                    Plant(_defaultCrop);
                }
                // Ready: 부착된 HerbPickup이 자체 E키 Harvest()를 처리 — 여기서 중복 처리하지 않음
            }
        }

        // ===== 상태 전이 =====

        private void TransitionToReady()
        {
            _phase = CropPhase.Ready;
            AttachHerbPickup();
            RefreshCropVisual();
            ShowStatus($"🧺 {_cropType} 수확 준비 완료 — [E] 수확!");
            Debug.Log($"[FarmPlot] 🌾 {name}: {_cropType} 숙성 완료! (HerbPickup 부착 — E키 수확)");
        }

        private void SetPhaseEmpty(string reason)
        {
            _phase = CropPhase.Empty;
            _seededGameTime = 0;

            if (_herbRef != null)
            {
                _herbRef.OnHarvestStarted -= OnHarvested;
                Destroy(_herbRef);   // 프레임 말 파괴 → HerbPickup의 Respawn 예약도 함께 해제, 다음 Ready에 재부착
                _herbRef = null;
            }

            RefreshCropVisual();

            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[FarmPlot] {name}: 밭 초기화 ({reason})");
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

        /// <summary>HerbPickup.Harvest()/TryAutoGather() 수확 시작 시그널 → 밭 Empty 리셋.</summary>
        private void OnHarvested()
        {
            SetPhaseEmpty($"{_cropType} 수확 완료");
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

        private void BuildSoilVisual()
        {
            if (_soilVisual != null) return;

            var soil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            soil.name = "Soil";
            soil.transform.SetParent(transform, false);
            soil.transform.localPosition = new Vector3(0f, -0.05f, 0f);   // 흙타일이 지면에 약간 박히게
            soil.transform.localScale = new Vector3(1f, 0.1f, 1f);

            var soilRenderer = soil.GetComponent<MeshRenderer>();
            if (soilRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(0.42f, 0.29f, 0.16f, 1f);           // 갈색 토양
                soilRenderer.material = mat;
            }

            var soilCol = soil.GetComponent<Collider>();
            if (soilCol != null) Destroy(soilCol);   // 시각 전용 — 전투 Raycast/루팅 간섭 방지

            _soilVisual = soil.transform;
        }

        private void BuildCropVisual()
        {
            if (_cropVisual != null) return;

            var crop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crop.name = "Crop";
            crop.transform.SetParent(transform, false);
            crop.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);

            _cropRenderer = crop.GetComponent<MeshRenderer>();
            if (_cropRenderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = GetCropColor(_cropType);
                _cropRenderer.material = mat;
            }

            var cropCol = crop.GetComponent<Collider>();
            if (cropCol != null) Destroy(cropCol);   // 시각 전용

            _cropVisual = crop.transform;
            _cropVisual.gameObject.SetActive(false);
        }

        /// <summary>성장 단계별 허브 시각 갱신 — Seeded 0.12, Growing 0.12→0.4, Ready 0.8.</summary>
        private void RefreshCropVisual()
        {
            if (_cropVisual == null) BuildCropVisual();
            if (_cropVisual == null) return;

            switch (_phase)
            {
                case CropPhase.Empty:
                    _cropVisual.gameObject.SetActive(false);
                    break;
                case CropPhase.Seeded:
                    _cropVisual.gameObject.SetActive(true);
                    SetCropScale(0.12f);
                    break;
                case CropPhase.Growing:
                    _cropVisual.gameObject.SetActive(true);
                    SetCropScale(Mathf.Lerp(0.12f, 0.4f, GrowProgress01()));
                    break;
                case CropPhase.Ready:
                    _cropVisual.gameObject.SetActive(true);
                    SetCropScale(0.8f);
                    break;
            }
        }

        private void SetCropScale(float s)
        {
            if (_cropVisual == null) return;

            _cropVisual.localScale = new Vector3(s, s, s);
            _cropVisual.localPosition = new Vector3(0f, 0.5f * s + 0.02f, 0f);   // 흙 위에 안착
            if (_cropRenderer != null)
                _cropRenderer.material.color = GetCropColor(_cropType);
        }

        private float GrowProgress01()
        {
            double target = (double)_growDurationDay * SecondsPerGameDay;
            if (target <= 0.0) return 1f;
            return Mathf.Clamp01((float)((CurrentClock() - _seededGameTime) / target));
        }

        private static Color GetCropColor(HerbPickup.HerbType type)
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
            else if (_phase == CropPhase.Empty)
                prompt = $"🌱 [E] 파종 — {_defaultCrop}";
            else if (_phase == CropPhase.Seeded)
                prompt = $"🌱 {_cropType} 발아 — 성장 중…";
            else if (_phase == CropPhase.Growing)
                prompt = $"🌾 {_cropType} 성장 중… ({GrowProgress01():P0})";
            else
                prompt = $"🧺 [E] 수확 — {_cropType}";

            GUI.Label(new Rect(14f, 10f, 380f, 24f), prompt);

            if (!string.IsNullOrEmpty(_statusMsg) && Time.time < _statusUntil)
                GUI.Label(new Rect(14f, 34f, 380f, 24f), _statusMsg);
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