using ProjectName.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Systems
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

        /// <summary>정적 생성자: BuildingEvents 구독</summary>
        static IndoorSceneTransition()
        {
            BuildingEvents.OnEnterBuildingRequest += HandleEnterBuilding;
            BuildingEvents.OnExitBuildingRequest += ExitBuilding;
            Debug.Log("[IndoorSceneTransition] BuildingEvents 구독 완료");
        }

        private static void HandleEnterBuilding(string buildingType, string nationStyle)
        {
            EnterBuilding(buildingType, nationStyle);
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
        public static void EnterBuilding(string buildingType, string nationStyle = null)
        {
            // 현재 씬 저장
            _previousSceneName = SceneManager.GetActiveScene().name;
            _pendingBuildingType = buildingType;
            _pendingNationStyle = nationStyle;

            Debug.Log($"[IndoorSceneTransition] 진입: 현재 씬 '{_previousSceneName}' → Additive IndoorScene (buildingType: {buildingType})");

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
            Debug.Log($"[IndoorSceneTransition] IndoorScene 로드 완료 (mode: {mode}). Builder 호출: {buildingType}");

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
                    HouseInteriorBuilder.BuildHouseInterior();
                    break;
                case "castle":
                    string nation = _pendingNationStyle ?? "Empire";
                    CastleInteriorBuilder.BuildCastleInterior(nation);
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

            _pendingBuildingType = null;
            _pendingNationStyle = null;
            Debug.Log("[IndoorSceneTransition] Builder 호출 완료.");
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

            Debug.Log($"[IndoorSceneTransition] 퇴출: IndoorScene 언로드 → '{_previousSceneName}'");

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
    }
}