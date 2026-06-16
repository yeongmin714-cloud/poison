using ProjectName.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectName.Systems
{
    /// <summary>
    /// C11-11: 튜토리얼 집 실내 전환.
    /// 건물 진입/퇴출 씬 전환 관리.
    /// </summary>
    public static class IndoorSceneTransition
    {
        private static string _previousSceneName;
        private static string _pendingBuildingType;

        /// <summary>
        /// 건물 진입. 현재 씬 이름을 저장하고 "IndoorScene"으로 전환 후
        /// buildingType에 따라 적절한 Builder 호출.
        /// </summary>
        /// <param name="buildingType">
        /// "CraftHouse", "Church", "House", "Castle", "Shop" 중 하나.
        /// </param>
        public static void EnterBuilding(string buildingType)
        {
            // 현재 씬 저장
            _previousSceneName = SceneManager.GetActiveScene().name;
            _pendingBuildingType = buildingType;

            Debug.Log($"[IndoorSceneTransition] 진입: 현재 씬 '{_previousSceneName}' → IndoorScene (buildingType: {buildingType})");

            // SceneManager.sceneLoaded 콜백 등록
            SceneManager.sceneLoaded += OnIndoorSceneLoaded;

            // 씬 로드 (LoadingManager 사용)
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.LoadSceneAsync("IndoorScene");
            }
            else
            {
                Debug.LogWarning("[IndoorSceneTransition] LoadingManager.Instance가 없음. 직접 LoadSceneAsync 호출.");
                SceneManager.LoadSceneAsync("IndoorScene");
            }
        }

        /// <summary>
        /// 씬 로드 완료 콜백. _pendingBuildingType에 맞는 Builder를 호출.
        /// </summary>
        private static void OnIndoorSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "IndoorScene") return;

            // 중복 실행 방지
            SceneManager.sceneLoaded -= OnIndoorSceneLoaded;

            string buildingType = _pendingBuildingType ?? string.Empty;
            Debug.Log($"[IndoorSceneTransition] IndoorScene 로드 완료. Builder 호출: {buildingType}");

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
                    CastleInteriorBuilder.BuildCastleInterior("Empire");
                    break;
                case "shop":
                    ShopInteriorBuilder.BuildShopInterior();
                    break;
                default:
                    Debug.LogWarning($"[IndoorSceneTransition] 알 수 없는 buildingType: '{buildingType}'. 기본 주택 생성.");
                    HouseInteriorBuilder.BuildHouseInterior();
                    break;
            }

            _pendingBuildingType = null;
            Debug.Log("[IndoorSceneTransition] Builder 호출 완료.");
        }

        /// <summary>
        /// 건물 퇴출. 저장된 이전 씬으로 복귀.
        /// </summary>
        public static void ExitBuilding()
        {
            if (string.IsNullOrEmpty(_previousSceneName))
            {
                Debug.LogWarning("[IndoorSceneTransition] 이전 씬 이름이 없음. 기본 씬(WorldScene)으로 복귀.");
                _previousSceneName = "WorldScene";
            }

            Debug.Log($"[IndoorSceneTransition] 퇴출: '{SceneManager.GetActiveScene().name}' → '{_previousSceneName}'");

            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.LoadSceneAsync(_previousSceneName);
            }
            else
            {
                SceneManager.LoadSceneAsync(_previousSceneName);
            }

            _previousSceneName = null;
        }
    }
}