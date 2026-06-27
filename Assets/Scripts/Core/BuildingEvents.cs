using System;
using UnityEngine;

namespace ProjectName.Core
{
    /// <summary>
    /// FIX-01: 건물 진입/퇴출 이벤트 브리지.
    /// Systems → UI 간 순환 참조를 깨기 위한 정적 이벤트 시스템.
    /// BuildingTrigger(시스템즈)가 이벤트를 발생시키고,
    /// IndoorSceneTransition(시스템즈)이 구독하여 처리합니다.
    /// </summary>
    public static class BuildingEvents
    {
        /// <summary>건물 진입 요청 (buildingType, nationStyle)</summary>
        public static event Action<string, string> OnEnterBuildingRequest;

        /// <summary>건물 퇴출 요청</summary>
        public static event Action OnExitBuildingRequest;

        /// <summary>
        /// 건물 진입 요청을 발생시킵니다.
        /// </summary>
        /// <param name="buildingType">건물 유형 (예: "House", "Shop", "CraftHouse", "Church", "Castle")</param>
        /// <param name="nationStyle">국가 스타일 (Castle 전용, 기본값 null)</param>
        /// <exception cref="ArgumentException">buildingType이 null 또는 빈 문자열인 경우 발생</exception>
        public static void RequestEnterBuilding(string buildingType, string nationStyle = null)
        {
            if (string.IsNullOrEmpty(buildingType))
            {
                Debug.LogError("[BuildingEvents] RequestEnterBuilding: buildingType이 null 또는 빈 문자열입니다.");
                return;
            }

            OnEnterBuildingRequest?.Invoke(buildingType, nationStyle);
        }

        /// <summary>
        /// 건물 퇴출 요청을 발생시킵니다.
        /// </summary>
        public static void RequestExitBuilding()
        {
            OnExitBuildingRequest?.Invoke();
        }

        /// <summary>테스트용: 건물 진입 구독자 존재 여부 확인</summary>
        public static bool HasEnterBuildingSubscribers => OnEnterBuildingRequest != null;

        /// <summary>테스트용: 건물 퇴출 구독자 존재 여부 확인</summary>
        public static bool HasExitBuildingSubscribers => OnExitBuildingRequest != null;
    }
}