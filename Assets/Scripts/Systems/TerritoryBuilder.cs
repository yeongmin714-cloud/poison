using System.Linq;
using ProjectName.Core.Data;
using UnityEngine;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C9-02~04: 전 영지(82개) 월드 위치에 따른 Placeholder 일괄 생성기
    /// TerritoryManager와 함께 배치되며, 게임 시작 시 모든 영지의 건물과 병사를 생성합니다.
    /// 이미 생성된 오브젝트가 있으면 건너뜁니다 (중복 생성 방지).
    /// </summary>
    [RequireComponent(typeof(TerritoryManager))]
    public class TerritoryBuilder : MonoBehaviour
    {
        [Header("생성 설정")]
        [SerializeField] private bool _autoBuildOnStart = true;

        [Header("건물 크기")]
        [SerializeField] private Vector3 _buildingSize = new Vector3(3, 2, 3);
        [SerializeField] private Vector3 _houseSize = new Vector3(2.5f, 1.5f, 2.5f);
        [SerializeField] private Vector3 _squareSize = new Vector3(6, 0.2f, 6);

        [Header("병사 배치 반경")]
        [SerializeField] private float _guardCircleRadius = 5f;

        private bool _hasBuilt = false;

        private void Start()
        {
            if (_autoBuildOnStart)
            {
                BuildAllTerritories();
            }
        }

        /// <summary>Backward-compatible: BuildTerritory() = BuildAllTerritories()  (테스트 호환)</summary>
        public void BuildTerritory()
        {
            BuildAllTerritories();
        }

        /// <summary>전체 82개 영지 건물과 병사 생성 (중복 방지)</summary>
        public void BuildAllTerritories()
        {
            if (_hasBuilt) return;

            var definitions = TerritoryDatabase.Instance.GetAllDefinitions();
            int builtCount = 0;

            foreach (var def in definitions)
            {
                if (IsTerritoryAlreadyBuilt(def))
                {
                    continue;
                }

                BuildSingleTerritory(def);
                builtCount++;

                if (builtCount % 10 == 0)
                {
                    Debug.Log($"[TerritoryBuilder] 진행 중: {builtCount}/{definitions.Count()} 영지 생성됨");
                }
            }

            _hasBuilt = true;
            Debug.Log($"[TerritoryBuilder] 전체 영지 Placeholder 생성 완료! 총 {builtCount}개 영지 신규 생성");
        }

        /// <summary>단일 영지 생성</summary>
        private void BuildSingleTerritory(TerritoryDefinition def)
        {
            Vector3 center = def.worldPosition;
            string parentName = $"Territory_{def.nation}_{def.id.index:D2}";

            // 부모 컨테이너 생성
            var parentGo = new GameObject(parentName);
            parentGo.transform.position = center;

            // 건물들 생성 (중앙 광장 + 상점 + 크래프트하우스 + 교회 + NPC 주택 4채)
            BuildBuildingsAt(parentGo.transform, center, def.nation);

            // 병사들 생성 (guardCount만큼, 영지 주변 원형 배치)
            BuildGuardsAt(parentGo.transform, center, def.guardCount, def.difficulty, def.nation);
        }

        /// <summary>이미 생성되었는지 확인 (부모 컨테이너 이름으로 체크)</summary>
        private bool IsTerritoryAlreadyBuilt(TerritoryDefinition def)
        {
            string parentName = $"Territory_{def.nation}_{def.id.index:D2}";
            return GameObject.Find(parentName) != null;
        }

        private void BuildBuildingsAt(Transform parent, Vector3 center, NationType nation)
        {
            // 광장 (중앙)
            CreateBuilding("TownSquare", BuildingPlaceholder.BuildingType.Other,
                center, _squareSize, new Color(0.6f, 0.5f, 0.3f), parent);

            // 상점 (왼쪽)
            CreateBuilding("Shop", BuildingPlaceholder.BuildingType.Shop,
                center + new Vector3(-8, 0, 0), _buildingSize, Color.yellow, parent);

            // 크래프트하우스 (오른쪽)
            CreateBuilding("CraftHouse", BuildingPlaceholder.BuildingType.CraftHouse,
                center + new Vector3(8, 0, 0), _buildingSize, Color.cyan, parent);

            // 교회 (뒤쪽)
            CreateBuilding("Church", BuildingPlaceholder.BuildingType.Church,
                center + new Vector3(0, 0, -8), _buildingSize, Color.white, parent);

            // NPC 주택 4채 (모서리들)
            CreateBuilding("NPCHouse1", BuildingPlaceholder.BuildingType.NPCHouse,
                center + new Vector3(-8, 0, -8), _houseSize, Color.gray, parent);
            CreateBuilding("NPCHouse2", BuildingPlaceholder.BuildingType.NPCHouse,
                center + new Vector3(8, 0, -8), _houseSize, Color.gray, parent);
            CreateBuilding("NPCHouse3", BuildingPlaceholder.BuildingType.NPCHouse,
                center + new Vector3(-8, 0, 8), _houseSize, Color.gray, parent);
            CreateBuilding("NPCHouse4", BuildingPlaceholder.BuildingType.NPCHouse,
                center + new Vector3(8, 0, 8), _houseSize, Color.gray, parent);
        }

        private void BuildGuardsAt(Transform parent, Vector3 center, int guardCount, TerritoryDifficulty difficulty, NationType nation)
        {
            int count = Mathf.Max(1, guardCount);
            int baseLevel = GetBaseGuardLevel(difficulty);

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Sin(angle) * _guardCircleRadius, 0, Mathf.Cos(angle) * _guardCircleRadius);
                int level = baseLevel + (i % 3); // 레벨 분산
                CreateGuard($"Guard_{i + 1}", center + offset, GetGuardName(nation), level, nation, parent);
            }
        }

        /// <summary>난이도별 기본 병사 레벨 반환</summary>
        private int GetBaseGuardLevel(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => 1,   // 1-3
                TerritoryDifficulty.Ring2 => 3,   // 3-5
                TerritoryDifficulty.Ring3 => 5,   // 5-8
                TerritoryDifficulty.Ring4 => 8,   // 8-12
                TerritoryDifficulty.Empire => 10, // 10-15
                _ => 1
            };
        }

        /// <summary>국가별 병사 이름 접두사</summary>
        private string GetGuardName(NationType nation)
        {
            return nation switch
            {
                NationType.East => "동방 병사",
                NationType.West => "서부 병사",
                NationType.South => "남부 병사",
                NationType.North => "북부 병사",
                NationType.Empire => "황제 친위대",
                NationType.Dracula => "스켈레톤 병졸",
                _ => "병사"
            };
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
        /// 건물 Placeholder 생성 (GLB 우선, 없으면 Primitive Cube) - 부모 지정 가능
        /// </summary>
        private void CreateBuilding(string name, BuildingPlaceholder.BuildingType type, Vector3 position, Vector3 scale, Color color, Transform parent = null)
        {
            string modelKey = GetModelKeyForBuilding(type);
            var go = TrySpawnModelOrPlaceholder(modelKey, name, position, scale, color, PrimitiveType.Cube);

            if (parent != null)
                go.transform.SetParent(parent);

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
        /// 병사 Placeholder 생성 (GLB "soldier" 우선, 없으면 Primitive Capsule) - 부모 지정 가능
        /// </summary>
        private void CreateGuard(string name, Vector3 position, string guardName, int level, NationType nation, Transform parent = null)
        {
            var go = TrySpawnModelOrPlaceholder("soldier", name, position,
                new Vector3(1.5f, 2f, 1.5f), new Color(0.2f, 0.4f, 0.8f), PrimitiveType.Capsule);

            if (parent != null)
                go.transform.SetParent(parent);

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
            var buildings = FindObjectsByType<BuildingPlaceholder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var b in buildings) Destroy(b.gameObject);

            var guards = FindObjectsByType<GuardPlaceholder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var g in guards) Destroy(g.gameObject);

            // 부모 컨테이너도 정리
            var allObjects = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var t in allObjects)
            {
                if (t.name.StartsWith("Territory_"))
                    Destroy(t.gameObject);
            }

            _hasBuilt = false;
            Debug.Log("[TerritoryBuilder] 모든 Placeholder 제거 완료");
        }
    }
}