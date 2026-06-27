using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C10-03~05: BuildingTrigger 배치 유틸리티.
    /// 각 건물 유형별 트리거를 월드 씬에 쉽게 배치할 수 있는 헬퍼 메서드를 제공합니다.
    /// </summary>
    public static class IndoorTransitionSetup
    {
        // ── 건물 유형 상수 ───────────────────────────────────────────
        public const string TYPE_HOUSE = "House";
        public const string TYPE_SHOP = "Shop";
        public const string TYPE_CRAFT_HOUSE = "CraftHouse";
        public const string TYPE_CHURCH = "Church";
        public const string TYPE_CASTLE = "Castle";

        // ── 기본 상호작용 범위 ──────────────────────────────────────
        public const float DEFAULT_INTERACT_RANGE = 3f;
        public const float CASTLE_INTERACT_RANGE = 4f;

        /// <summary>
        /// 지정된 위치에 BuildingTrigger를 생성하고 설정합니다.
        /// </summary>
        /// <param name="position">트리거 위치 (문 앞)</param>
        /// <param name="buildingType">건물 유형 (House, Shop, CraftHouse, Church, Castle)</param>
        /// <param name="interactRange">상호작용 범위 (기본 3f)</param>
        /// <param name="parent">부모 Transform (선택사항)</param>
        /// <returns>생성된 BuildingTrigger GameObject</returns>
        public static GameObject CreateBuildingTrigger(Vector3 position, string buildingType, float interactRange = DEFAULT_INTERACT_RANGE, Transform parent = null)
        {
            if (string.IsNullOrWhiteSpace(buildingType))
            {
                Debug.LogError("[IndoorTransitionSetup] buildingType이 null 또는 빈 문자열");
                return null;
            }

            GameObject triggerGo = new GameObject($"{buildingType}_Trigger");
            triggerGo.transform.position = position;

            if (parent != null)
                triggerGo.transform.SetParent(parent);

            var trigger = triggerGo.AddComponent<BuildingTrigger>();
            trigger.BuildingType = buildingType;
            trigger.InteractRange = interactRange;

            Debug.Log($"[IndoorTransitionSetup] {buildingType} BuildingTrigger 생성 완료 at {position}");
            return triggerGo;
        }

        /// <summary>
        /// 튜토리얼 집 BuildingTrigger 생성. buildingType = "House".
        /// </summary>
        public static GameObject CreateTutorialHouseTrigger(Vector3 position, Transform parent = null)
        {
            return CreateBuildingTrigger(position, TYPE_HOUSE, DEFAULT_INTERACT_RANGE, parent);
        }

        /// <summary>
        /// 상점 BuildingTrigger 생성. buildingType = "Shop".
        /// </summary>
        public static GameObject CreateShopTrigger(Vector3 position, Transform parent = null)
        {
            return CreateBuildingTrigger(position, TYPE_SHOP, DEFAULT_INTERACT_RANGE, parent);
        }

        /// <summary>
        /// 크래프트하우스 BuildingTrigger 생성. buildingType = "CraftHouse".
        /// </summary>
        public static GameObject CreateCraftHouseTrigger(Vector3 position, Transform parent = null)
        {
            return CreateBuildingTrigger(position, TYPE_CRAFT_HOUSE, DEFAULT_INTERACT_RANGE, parent);
        }

        /// <summary>
        /// 교회 BuildingTrigger 생성. buildingType = "Church".
        /// </summary>
        public static GameObject CreateChurchTrigger(Vector3 position, Transform parent = null)
        {
            return CreateBuildingTrigger(position, TYPE_CHURCH, DEFAULT_INTERACT_RANGE, parent);
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
            GameObject go = CreateBuildingTrigger(position, TYPE_CASTLE, CASTLE_INTERACT_RANGE, parent);
            if (go == null) return null;
            var trigger = go.GetComponent<BuildingTrigger>();
            if (trigger != null)
                trigger.NationStyle = nationStyle;
            Debug.Log($"[IndoorTransitionSetup] Castle 트리거 생성 완료 (nationStyle: {nationStyle})");
            return go;
        }
    }
}