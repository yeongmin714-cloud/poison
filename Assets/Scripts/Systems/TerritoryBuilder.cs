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
            // C9-04: 영지 입구 병사 3명
            CreateGuard("Guard_Entrance1", _territoryCenter + new Vector3(-2, 0, 2), "리카드 병사", 1, NationType.East);
            CreateGuard("Guard_Entrance2", _territoryCenter + new Vector3(2, 0, 2), "리카드 병사", 1, NationType.East);
            CreateGuard("Guard_Entrance3", _territoryCenter + new Vector3(0, 0, 3), "리카드 병사", 2, NationType.East);
        }

        private void CreateBuilding(string name, BuildingPlaceholder.BuildingType type, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.tag = "Untagged";

            // 색상 적용 (URP Lit)
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = MaterialHelper.CreateLitMaterial(color, $"{name}_Mat");
            }

            var placeholder = go.AddComponent<BuildingPlaceholder>();
            placeholder.buildingType = type;
            placeholder.buildingName = name;

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

        private void CreateGuard(string name, Vector3 position, string guardName, int level, NationType nation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.5f, 2f, 1.5f);

            // 색상 적용 (파란색 = 동쪽 국가)
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = MaterialHelper.CreateLitMaterial(new Color(0.2f, 0.4f, 0.8f), $"{name}_Mat");
            }

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