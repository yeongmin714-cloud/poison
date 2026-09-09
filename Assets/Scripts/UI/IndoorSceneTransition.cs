using ProjectName.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// C10-02: 실내/외 맵 전환 관리 (Additive Scene Loading).
    /// 건물 진입 시 "IndoorScene"을 Additive 모드로 로드하고,
    /// 퇴출 시 Additive 씬을 언로드합니다.
    /// </summary>
    public static class IndoorSceneTransition
    {
        private const string INDOOR_SCENE_NAME = "IndoorScene";
        private const string DEFAULT_WORLD_SCENE = "WorldScene";

        private static string _previousSceneName;
        private static string _pendingBuildingType;
        private static string _pendingNationStyle;
        private static bool _pendingIsPlayerOwned;
        private static string _pendingTerritoryKey;   // INTERIOR-VAR: 레이아웃 변형 결정론 시드용
        private static Vector3? _returnPosition;      // 진입 직전 플레이어 위치(퇴출 복귀용)
        private const float INDOOR_FLOOR_Y = 0f;      // 실내 바닥 높이 (IndoorBuilder.CreateRoom: 바닥 XZ 평면 y=0)
        private static bool _initialized;

        /// <summary>정적 생성자: BuildingEvents 구독 (중복 방지)</summary>
        static IndoorSceneTransition()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            BuildingEvents.OnEnterBuildingRequest += HandleEnterBuilding;
            BuildingEvents.OnExitBuildingRequest += ExitBuilding;

        }

        private static void HandleEnterBuilding(string buildingType, string nationStyle, bool isPlayerOwned, string territoryKey)
        {
            EnterBuilding(buildingType, nationStyle, isPlayerOwned, territoryKey);
        }

        /// <summary>
        /// 건물 진입. 현재 활성 씬 이름을 저장하고 "IndoorScene"을 Additive 모드로 로드한 후,
        /// buildingType에 따라 적절한 Builder를 호출합니다.
        /// 로드 완료 후 IndoorScene을 활성 씬으로 설정합니다.
        /// </summary>
        /// <param name="buildingType">
        /// "CraftHouse", "Church", "House", "Castle", "Shop" 중 하나.
        /// </param>
        /// <param name="nationStyle">
        /// Castle 타입 진입 시 국가 스타일 (예: "Empire", "Eastern", "Western", "Southern", "Northern").
        /// 기본값 null.
        /// </param>
        /// <param name="isPlayerOwned">
        /// Castle 타입 진입 시 플레이어 소유 성 여부 (true → PlayerCastleInteriorBuilder, false → CastleInteriorBuilder).
        /// 기본값 false.
        /// </param>
        public static void EnterBuilding(string buildingType, string nationStyle = null, bool isPlayerOwned = false, string territoryKey = null)
        {
            // 현재 씬 저장
            _previousSceneName = SceneManager.GetActiveScene().name;
            _pendingBuildingType = buildingType;
            _pendingNationStyle = nationStyle;
            _pendingIsPlayerOwned = isPlayerOwned;
            _pendingTerritoryKey = territoryKey;

            // 진입 직전 플레이어 위치 저장(퇴출 시 복귀)
            var enteringPlayer = GameObject.FindGameObjectWithTag("Player");
            _returnPosition = enteringPlayer != null ? (Vector3?)enteringPlayer.transform.position : null;



            // 이미 IndoorScene이 로드되어 있으면 언로드 후 재로드
            Scene indoorScene = SceneManager.GetSceneByName(INDOOR_SCENE_NAME);
            if (indoorScene.isLoaded)
            {
                SceneManager.sceneUnloaded += OnPreviousIndoorUnloaded;
                SceneManager.UnloadSceneAsync(INDOOR_SCENE_NAME);
                return;
            }

            // SceneManager.sceneLoaded 콜백 등록
            SceneManager.sceneLoaded += OnIndoorSceneLoaded;

            // 씬 Additive 로드
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.LoadSceneAsync(INDOOR_SCENE_NAME, 0.3f, 0.3f, LoadSceneMode.Additive);
            }
            else
            {
                Debug.LogWarning("[IndoorSceneTransition] LoadingManager.Instance가 없음. 직접 LoadSceneAsync 호출.");
                SceneManager.LoadSceneAsync(INDOOR_SCENE_NAME, LoadSceneMode.Additive);
            }
        }

        /// <summary>
        /// 이전 IndoorScene이 언로드된 후 새로 Additive 로드합니다.
        /// </summary>
        private static void OnPreviousIndoorUnloaded(Scene scene)
        {
            if (scene.name != INDOOR_SCENE_NAME) return;
            SceneManager.sceneUnloaded -= OnPreviousIndoorUnloaded;

            SceneManager.sceneLoaded += OnIndoorSceneLoaded;
            SceneManager.LoadSceneAsync(INDOOR_SCENE_NAME, LoadSceneMode.Additive);
        }

        /// <summary>
        /// 씬 로드 완료 콜백. _pendingBuildingType에 맞는 Builder를 호출하고 IndoorScene을 활성화합니다.
        /// </summary>
        private static void OnIndoorSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != INDOOR_SCENE_NAME) return;

            // 중복 실행 방지
            SceneManager.sceneLoaded -= OnIndoorSceneLoaded;

            string buildingType = _pendingBuildingType ?? string.Empty;


            // IndoorScene을 활성 씬으로 설정
            SceneManager.SetActiveScene(scene);

            // buildingType에 따라 적절한 Builder 호출
            switch (buildingType.ToLower())
            {
                case "crafthouse":
                    CraftHouseInteriorBuilder.BuildCraftHouseInterior();
                    break;
                case "church":
                    ChurchInteriorBuilder.BuildChurchInterior();
                    break;
                case "house":
                case "npchouse":
                    HouseInteriorBuilder.BuildHouseInterior();
                    break;
                case "castle":
                    string nation = _pendingNationStyle ?? "Empire";
                    // INTERIOR-VAR: 영지 키(우선)/nation+소유로 결정론 해시 → 8종 레이아웃 변형.
                    // 같은 영지 재방문 시 항상 같은 배치(결정론), 다른 영지는 다른 배치.
                    int layoutVariant = ComputeLayoutVariant(_pendingTerritoryKey, nation, _pendingIsPlayerOwned);
                    // 소유 상태 분기: 플레이어 소유 성 → PlayerCastleInteriorBuilder, 영주 성 → CastleInteriorBuilder
                    GameObject interior = _pendingIsPlayerOwned
                        ? PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nation, layoutVariant)
                        : CastleInteriorBuilder.BuildCastleInterior(nation, layoutVariant);
                    if (interior != null)
                        TerritoryBuilder.SpawnInteriorFixtures(interior.transform.position, nation);
                    break;
                case "barn":
                    BarnInteriorBuilder.BuildBarnInterior();
                    break;
                case "shop":
                    ShopInteriorBuilder.BuildShopInterior();
                    break;
                case "cave":
                    CaveInteriorBuilder.BuildCaveInterior(_pendingNationStyle ?? "default", 1);
                    break;
                default:
                    Debug.LogWarning($"[IndoorSceneTransition] 알 수 없는 buildingType: '{buildingType}'. 기본 주택 생성.");
                    HouseInteriorBuilder.BuildHouseInterior();
                    break;
            }

            // 플레이어를 내부 원점으로 이동(카메라는 플레이어 추적 유지) — builders는 원점 부근에 내부 생성
            var indoorPlayer = GameObject.FindGameObjectWithTag("Player");
            if (indoorPlayer == null)
                indoorPlayer = UnityEngine.Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>()?.gameObject;
            if (indoorPlayer != null)
                indoorPlayer.transform.position = new Vector3(0f, INDOOR_FLOOR_Y + 0.1f, 0f);

            // 2026-09-09(5차): 실내 카메라 스왑 — 메인 카메라만 비활성(IndoorCamera는 씬 상주·활성 상태로 로드됨)
            var mainCamGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCamGO != null) mainCamGO.SetActive(false);

            // 2026-09-09(3차 FIX): 까만 화면 방지 — 실내 앰비언트를 따뜻한 플랫톤으로
            // (활성 씬이 IndoorScene이므로 RenderSettings는 실내 것만 적용, 복귀 시 자동 원복)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.38f, 0.30f);

            _pendingBuildingType = null;
            _pendingNationStyle = null;
            _pendingIsPlayerOwned = false;

        }

        /// <summary>
        /// 건물 퇴출. "IndoorScene"을 Additive 씬에서 언로드하고 이전 씬으로 복귀합니다.
        /// </summary>
        public static void ExitBuilding()
        {
            if (string.IsNullOrEmpty(_previousSceneName))
            {
                Debug.LogWarning("[IndoorSceneTransition] 이전 씬 이름이 없음. 기본 씬(WorldScene)으로 복귀.");
                _previousSceneName = DEFAULT_WORLD_SCENE;
            }



            // 이전 씬을 활성화
            Scene prevScene = SceneManager.GetSceneByName(_previousSceneName);
            if (prevScene.isLoaded)
            {
                SceneManager.SetActiveScene(prevScene);
            }

            // IndoorScene Additive 언로드
            Scene indoorScene = SceneManager.GetSceneByName(INDOOR_SCENE_NAME);
            if (indoorScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(INDOOR_SCENE_NAME);
            }
            else
            {
                Debug.LogWarning("[IndoorSceneTransition] 언로드할 IndoorScene이 없음.");
            }

            // 진입 직전 위치로 플레이어 복귀
            var exitingPlayer = GameObject.FindGameObjectWithTag("Player");
            if (exitingPlayer == null)
                exitingPlayer = UnityEngine.Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>()?.gameObject;
            if (exitingPlayer != null && _returnPosition.HasValue)
                exitingPlayer.transform.position = _returnPosition.Value;
            _returnPosition = null;

            // 2026-09-09(5차): 카메라 복귀 — 메인 카메라만 재활성(IndoorCamera는 씬 언로드로 자동 제거)
            var mainCamGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCamGO != null) mainCamGO.SetActive(true);

            _previousSceneName = null;
        }

        /// <summary>
        /// 현재 로드된 건물 유형 반환 (테스트 및 디버깅용).
        /// </summary>
        public static string GetPendingBuildingType() => _pendingBuildingType;

        /// <summary>
        /// 이전 씬 이름 반환 (테스트 및 디버깅용).
        /// </summary>
        public static string GetPreviousSceneName() => _previousSceneName;

        /// <summary>
        /// IndoorScene이 현재 Additive 로드되어 있는지 확인합니다.
        /// </summary>
        public static bool IsIndoorSceneLoaded()
        {
            Scene scene = SceneManager.GetSceneByName(INDOOR_SCENE_NAME);
            return scene.isLoaded;
        }

        /// <summary>
        /// 성 내부 8종 레이아웃 변형 결정론 시드 계산 (INTERIOR-VAR).
        /// 영지 키(우선) 또는 nation+소유조로 djb2 문자열 해시 → 0..7.
        /// Random/시간 미사용 — 같은 입력 → 항상 같은 variant (재방문 시 동일 배치).
        /// </summary>
        private static int ComputeLayoutVariant(string territoryKey, string nation, bool isPlayerOwned)
        {
            string seedStr;
            if (!string.IsNullOrEmpty(territoryKey))
            {
                seedStr = territoryKey;
            }
            else
            {
                seedStr = (nation ?? "Empire") + (isPlayerOwned ? "_P" : "_L");
            }

            // djb2 해시 (결정론, GC 추가 할당 없음)
            uint hash = 5381u;
            foreach (char c in seedStr)
            {
                hash = ((hash << 5) + hash) + (uint)(c & 0x7F);
            }
            return (int)(hash % 8u);
        }
    }
}
