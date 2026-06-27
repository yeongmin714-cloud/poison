using ProjectName.Core.Data;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-02~04: 첫 번째 영지 Placeholder 일괄 생성기
    /// TerritoryManager와 함께 배치되며, 게임 시작 시 건물과 병사를 생성합니다.
    /// 이미 생성된 오브젝트가 있으면 건너뜁니다 (중복 생성 방지).
    /// </summary>
    [RequireComponent(typeof(TerritoryManager))]
    public class TerritoryBuilder : MonoBehaviour
    {
        [Header("생성 설정")]
        [SerializeField] private bool _autoBuildOnStart = true;
        [SerializeField] private Vector3 _territoryCenter = new Vector3(0, 0, 0);

        [Header("건물 크기")]
        [SerializeField] private Vector3 _buildingSize = new Vector3(3, 2, 3);
        [SerializeField] private Vector3 _houseSize = new Vector3(2.5f, 1.5f, 2.5f);

        [Header("병사 설정")]
        [SerializeField] private int _guardCount = 3;

        private bool _hasBuilt = false;

        private void Start()
        {
            if (_autoBuildOnStart)
            {
                BuildTerritory();
            }
        }

        /// <summary>영지 건물과 병사 생성 (중복 방지)</summary>
        public void BuildTerritory()
        {
            if (_hasBuilt) return;

            var existing = FindObjectsOfType<BuildingPlaceholder>();
            if (existing.Length > 0)
            {
                Debug.Log($"[TerritoryBuilder] 이미 {existing.Length}개의 건물이 존재합니다. 생성 건너뜀.");
                _hasBuilt = true;
                return;
            }

            BuildBuildings();
            BuildGuards();
            _hasBuilt = true;

            Debug.Log("[TerritoryBuilder] 첫 번째 영지 Placeholder 생성 완료!");
        }

        private void BuildBuildings()
        {
            // C9-02: 광장 (중앙), 상점, 크래프트하우스, 교회
            // C9-03: NPC 주택 3~4채
            // 광장 (중앙)
            CreateBuilding("TownSquare", BuildingPlaceholder.BuildingType.Other,
                _territoryCenter, new Vector3(6, 0.2f, 6), new Color(0.6f, 0.5f, 0.3f));

            CreateBuilding("Shop", BuildingPlaceholder.BuildingType.Shop, 
                _territoryCenter + new Vector3(-5, 0, 0), _buildingSize, Color.yellow);
            CreateBuilding("CraftHouse", BuildingPlaceholder.BuildingType.CraftHouse, 
                _territoryCenter + new Vector3(5, 0, 0), _buildingSize, Color.cyan);
            CreateBuilding("Church", BuildingPlaceholder.BuildingType.Church, 
                _territoryCenter + new Vector3(0, 0, -5), _buildingSize, Color.white);

            // C9-03: NPC 주택 4채
            CreateBuilding("NPCHouse1", BuildingPlaceholder.BuildingType.NPCHouse, 
                _territoryCenter + new Vector3(-5, 0, -5), _houseSize, Color.gray);
            CreateBuilding("NPCHouse2", BuildingPlaceholder.BuildingType.NPCHouse, 
                _territoryCenter + new Vector3(5, 0, -5), _houseSize, Color.gray);
            CreateBuilding("NPCHouse3", BuildingPlaceholder.BuildingType.NPCHouse, 
                _territoryCenter + new Vector3(-5, 0, 5), _houseSize, Color.gray);
            CreateBuilding("NPCHouse4", BuildingPlaceholder.BuildingType.NPCHouse, 
                _territoryCenter + new Vector3(5, 0, 5), _houseSize, Color.gray);
        }

        private void BuildGuards()
        {
            // C9-04: 영지 입구 병사 (인스펙터 _guardCount만큼 생성)
            int count = Mathf.Max(1, _guardCount);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Sin(angle) * 2.5f, 0, Mathf.Cos(angle) * 2.5f + 1f);
                int level = 1 + (i % 3); // 1~3 레벨 순환
                CreateGuard($"Guard_Entrance{i + 1}", _territoryCenter + offset, "리카드 병사", level, NationType.East);
            }
        }

        /// <summary>
        /// 건물 타입에 대응하는 GLB 모델 키를 반환합니다.
        /// </summary>
        private static string GetModelKeyForBuilding(BuildingPlaceholder.BuildingType type)
        {
            switch (type)
            {
                case BuildingPlaceholder.BuildingType.Shop:
                    return "hut";
                case BuildingPlaceholder.BuildingType.CraftHouse:
                    return "craft_blend";
                case BuildingPlaceholder.BuildingType.NPCHouse:
                    return "hut";
                case BuildingPlaceholder.BuildingType.Church:
                default:
                    return null; // 해당 GLB 모델 없음 → Placeholder 유지
            }
        }

        /// <summary>
        /// GLB 모델이 있으면 Instantiate하고, 없으면 Primitive Placeholder를 생성합니다.
        /// </summary>
        /// <param name="modelKey">GLB 모델 키 (null이면 Placeholder만 생성)</param>
        /// <param name="name">생성할 GameObject 이름</param>
        /// <param name="position">월드 위치</param>
        /// <param name="scale">크기 (Placeholder용)</param>
        /// <param name="fallbackColor">Placeholder 색상</param>
        /// <param name="fallbackType">Placeholder Primitive 타입</param>
        /// <returns>생성된 GameObject (GLB 모델 또는 Placeholder)</returns>
        private static GameObject TrySpawnModelOrPlaceholder(string modelKey, string name,
            Vector3 position, Vector3 scale, Color fallbackColor, PrimitiveType fallbackType)
        {
            // GLB 모델이 있는지 확인
            if (!string.IsNullOrEmpty(modelKey) && RuntimeModelLoader.TryGetModel(modelKey, out var modelPrefab))
            {
                GameObject modelGo = Object.Instantiate(modelPrefab);
                modelGo.name = name;
                modelGo.transform.position = position;
                modelGo.transform.localScale = scale;

                Debug.Log($"[TerritoryBuilder] GLB 모델 '{modelKey}'로 '{name}' 생성");
                return modelGo;
            }

            // GLB가 없으면 Primitive Placeholder 생성
            var go = GameObject.CreatePrimitive(fallbackType);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.tag = "Untagged";

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = MaterialHelper.CreateLitMaterial(fallbackColor, $"{name}_Mat");
            }

            return go;
        }

        /// <summary>
        /// 건물 Placeholder 생성 (GLB 우선, 없으면 Primitive Cube)
        /// </summary>
        private void CreateBuilding(string name, BuildingPlaceholder.BuildingType type, Vector3 position, Vector3 scale, Color color)
        {
            string modelKey = GetModelKeyForBuilding(type);
            var go = TrySpawnModelOrPlaceholder(modelKey, name, position, scale, color, PrimitiveType.Cube);

            var placeholder = go.AddComponent<BuildingPlaceholder>();
            placeholder.buildingType = type;
            placeholder.buildingName = name;

            // BuildingTrigger 컴포넌트 추가 (E키 상호작용용)
            var trigger = go.AddComponent<BuildingTrigger>();
            trigger.BuildingType = type.ToString();
            trigger.InteractRange = 3f;

            // 콜라이더는 끄지 않음 (물리적 블로킹)
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;

            // TextMesh 라벨
            var labelGo = new GameObject($"{name}_Label");
            labelGo.transform.SetParent(go.transform);
            labelGo.transform.localPosition = new Vector3(0, scale.y * 0.5f + 0.5f, 0);
            var textMesh = labelGo.AddComponent<TextMesh>();
            textMesh.text = name;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.characterSize = 0.08f;
            textMesh.color = Color.white;
            textMesh.fontSize = 24;
        }

        /// <summary>
        /// 병사 Placeholder 생성 (GLB "soldier" 우선, 없으면 Primitive Capsule)
        /// </summary>
        private void CreateGuard(string name, Vector3 position, string guardName, int level, NationType nation)
        {
            var go = TrySpawnModelOrPlaceholder("soldier", name, position,
                new Vector3(1.5f, 2f, 1.5f), new Color(0.2f, 0.4f, 0.8f), PrimitiveType.Capsule);

            var placeholder = go.AddComponent<GuardPlaceholder>();
            placeholder.SetGuardInfo(guardName, level, nation);

            // 라벨
            var labelGo = new GameObject($"{name}_Label");
            labelGo.transform.SetParent(go.transform);
            labelGo.transform.localPosition = new Vector3(0, 2, 0);
            var textMesh = labelGo.AddComponent<TextMesh>();
            textMesh.text = $"{guardName} Lv.{level}";
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.characterSize = 0.07f;
            textMesh.color = Color.white;
            textMesh.fontSize = 20;
        }

        /// <summary>
        /// 이미 생성된 모든 건물과 병사를 제거 (리셋용)
        /// </summary>
        public void ClearAll()
        {
            var buildings = FindObjectsOfType<BuildingPlaceholder>();
            foreach (var b in buildings) Destroy(b.gameObject);

            var guards = FindObjectsOfType<GuardPlaceholder>();
            foreach (var g in guards) Destroy(g.gameObject);

            _hasBuilt = false;
            Debug.Log("[TerritoryBuilder] 모든 Placeholder 제거 완료");
        }
    }
}