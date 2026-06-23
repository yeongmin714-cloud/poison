using System;

namespace ProjectName.Core
{
    /// <summary>
    /// FIX-01: 건물 진입/퇴출 이벤트 브리지.
    /// Systems → UI 간 순환 참조를 깨기 위한 정적 이벤트 시스템.
    /// BuildingTrigger(시스템즈)가 이벤트를 발생시키고,
    /// IndoorSceneTransition(UI)이 구독하여 처리합니다.
    /// </summary>
    public static class BuildingEvents
    {
        /// <summary>건물 진입 요청 (buildingType, nationStyle)</summary>
        public static event Action<string, string> OnEnterBuildingRequest;

        /// <summary>건물 퇴출 요청</summary>
        public static event Action OnExitBuildingRequest;

        public static void RequestEnterBuilding(string buildingType, string nationStyle = null)
        {
            OnEnterBuildingRequest?.Invoke(buildingType, nationStyle);
        }

        public static void RequestExitBuilding()
        {
            OnExitBuildingRequest?.Invoke();
        }

        /// <summary>테스트용: 구독자 존재 여부 확인</summary>
        public static bool HasEnterBuildingSubscribers => OnEnterBuildingRequest != null;
        public static bool HasExitBuildingSubscribers => OnExitBuildingRequest != null;
    }
}