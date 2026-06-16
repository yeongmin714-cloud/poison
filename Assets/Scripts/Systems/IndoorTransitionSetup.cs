using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-03~05: BuildingTrigger 배치 유틸리티.
    /// 각 건물 유형별 트리거를 월드 씬에 쉽게 배치할 수 있는 헬퍼 메서드를 제공합니다.
    /// </summary>
    public static class IndoorTransitionSetup
    {
        /// <summary>
        /// 지정된 위치에 BuildingTrigger를 생성하고 설정합니다.
        /// </summary>
        /// <param name="position">트리거 위치 (문 앞)</param>
        /// <param name="buildingType">건물 유형 (House, Shop, CraftHouse, Church, Castle)</param>
        /// <param name="interactRange">상호작용 범위 (기본 3f)</param>
        /// <param name="parent">부모 Transform (선택사항)</param>
        /// <returns>생성된 BuildingTrigger GameObject</returns>
        public static GameObject CreateBuildingTrigger(Vector3 position, string buildingType, float interactRange = 3f, Transform parent = null)
        {
            GameObject triggerGo = new GameObject($"{buildingType}_Trigger");
            triggerGo.transform.position = position;

            if (parent != null)
                triggerGo.transform.SetParent(parent);

            var trigger = triggerGo.AddComponent<BuildingTrigger>();
            trigger.BuildingType = buildingType;
            trigger.InteractRange = interactRange;

            // Tag 설정 (Player 찾기 위함)
            // BuildingTrigger.Start()에서 GameObject.FindGameObjectWithTag("Player")를 사용하므로
            // Player 태그가 월드에 존재해야 합니다.

            Debug.Log($"[IndoorTransitionSetup] {buildingType} BuildingTrigger 생성 완료 at {position}");
            return triggerGo;
        }

        /// <summary>
        /// 튜토리얼 집 BuildingTrigger 생성. buildingType = "House".
        /// </summary>
        public static GameObject CreateTutorialHouseTrigger(Vector3 position, Transform parent = null)
        {
            return CreateBuildingTrigger(position, "House", 3f, parent);
        }

        /// <summary>
        /// 상점 BuildingTrigger 생성. buildingType = "Shop".
        /// </summary>
        public static GameObject CreateShopTrigger(Vector3 position, Transform parent = null)
        {
            return CreateBuildingTrigger(position, "Shop", 3f, parent);
        }

        /// <summary>
        /// 크래프트하우스 BuildingTrigger 생성. buildingType = "CraftHouse".
        /// </summary>
        public static GameObject CreateCraftHouseTrigger(Vector3 position, Transform parent = null)
        {
            return CreateBuildingTrigger(position, "CraftHouse", 3f, parent);
        }

        /// <summary>
        /// 교회 BuildingTrigger 생성. buildingType = "Church".
        /// </summary>
        public static GameObject CreateChurchTrigger(Vector3 position, Transform parent = null)
        {
            return CreateBuildingTrigger(position, "Church", 3f, parent);
        }

        /// <summary>
        /// 성 BuildingTrigger 생성. buildingType = "Castle".
        /// 국가 스타일을 추가로 전달할 수 있습니다 (IndoorSceneTransition.EnterBuilding의 nationStyle 파라미터).
        /// </summary>
        /// <param name="position">트리거 위치</param>
        /// <param name="nationStyle">국가 스타일 (Eastern, Western, Southern, Northern, Empire)</param>
        /// <param name="parent">부모 Transform (선택사항)</param>
        public static GameObject CreateCastleTrigger(Vector3 position, string nationStyle = "Empire", Transform parent = null)
        {
            // Castle 트리거는 buildingType="Castle"로 생성
            // NationStyle은 EnterBuilding 호출 시 전달됨
            return CreateBuildingTrigger(position, "Castle", 4f, parent);
        }
    }
}