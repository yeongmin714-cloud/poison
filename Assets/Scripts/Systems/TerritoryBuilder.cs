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
                // 82개 영지를 1프레임당 1개씩 분산 생성 (프리징 방지)
                StartCoroutine(BuildAllCoroutine());
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
            RefreshRegistrations();
            Debug.Log($"[TerritoryBuilder] 전체 영지 Placeholder 생성 완료! 총 {builtCount}개 영지 신규 생성");
        }

        /// <summary>
        /// BuildAllTerritories와 동일한 루프를 영지 1개당 1프레임씩 분산 실행합니다 (프리징 방지).
        /// Start()에서 자동 호출됩니다.
        /// </summary>
        private System.Collections.IEnumerator BuildAllCoroutine()
        {
            if (_hasBuilt) yield break;

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

                // 영지 1개당 1프레임 분산 → 82프레임에 걸쳐 생성
                yield return null;
            }

            _hasBuilt = true;
            RefreshRegistrations();
            Debug.Log($"[TerritoryBuilder] 전체 영지 Placeholder 생성 완료 (분산)! 총 {builtCount}개 영지 신규 생성");
        }

        /// <summary>TerritoryManager에 건물/병사 재등록 요청 (빌드 완료 후 스캔)</summary>
        private void RefreshRegistrations()
        {
            if (TerritoryManager.Instance != null)
                TerritoryManager.Instance.RefreshRegistrations();
        }

        /// <summary>단일 영지 생성</summary>
        private void BuildSingleTerritory(TerritoryDefinition def)
        {
            Vector3 center = def.worldPosition;
            string parentName = $"Territory_{def.nation}_{def.id.index:D2}";

            // 호수 겹침 경고 (경고만 — 이동/스킵 안 함. TerrainGenerator shore 배율 1.3f와 동일 기준)
            foreach (var lake in TerrainGenerator.Lakes)
            {
                float dist = Vector3.Distance(new Vector3(center.x, 0f, center.z), new Vector3(lake.center.x, 0f, lake.center.z));
                if (dist < lake.radius * 1.3f)
                {
                    Debug.LogWarning($"[TerritoryBuilder] 영지 '{parentName}'이(가) 호수(중심 {lake.center}, 반경 {lake.radius:F1}m) 인근에 위치 — 지형/물 겹침 가능 (경고만).");
                    break; // 영지당 한 줄 경고
                }
            }

            // 부모 컨테이너 생성
            var parentGo = new GameObject(parentName);
            parentGo.transform.position = center;

            // 성(castle) 생성 (국가별 GLB 중심)
            BuildBuildingsAt(parentGo.transform, center, def.nation);

            // 문지기 생성 (성문 앞 2~4명, guardCount는 전쟁 페이즈 데이터로 유지)
            BuildGuardsAt(parentGo.transform, center, def.guardCount, def.difficulty, def.nation);
        }

        /// <summary>이미 생성되었는지 확인 (부모 컨테이너 이름으로 체크)</summary>
        private bool IsTerritoryAlreadyBuilt(TerritoryDefinition def)
        {
            string parentName = $"Territory_{def.nation}_{def.id.index:D2}";
            return GameObject.Find(parentName) != null;
        }

        /// <summary>
        /// 성(building) 중심 영지 생성 — 국가별 castle GLB(또는 폴백 Cube)를 배치하고 성문(GateAnchor)을 생성합니다.
        /// 외부 건물(광장/상점/크래프트하우스/교회/NPC주택)은 더 이상 배치하지 않습니다 (R2 내부 등장용).
        /// 성 자체는 상호작용 대상이 아니므로 BuildingPlaceholder를 붙이지 않습니다.
        /// </summary>
        private void BuildBuildingsAt(Transform parent, Vector3 center, NationType nation)
        {
            string castleKey = GetCastleModelKey(nation);
            float groundY = TerrainGenerator.GetHeightAt(center.x, center.z, BiomeType.Plains, 42) + 1f;

            // 성 루트 (코드 배치용 컨테이너)
            var castleRoot = new GameObject("Castle");
            castleRoot.transform.position = new Vector3(center.x, groundY, center.z);

            // GLB 모델 로드 → 바운즈 기반 균일 스케일 정규화 (XZ 최대 반경 → 25m)
            if (RuntimeModelLoader.TryGetModel(castleKey, out var modelPrefab))
            {
                GameObject inst = Object.Instantiate(modelPrefab, castleRoot.transform, false);
                inst.name = "CastleModel";
                inst.transform.localPosition = Vector3.zero;
                NormalizeCastleScale(inst);
                Debug.Log($"[TerritoryBuilder] {parent.name}: castle '{castleKey}' 로드, 반경 {GetCastleRadius(castleRoot):F1}m");
            }
            else
            {
                // 폴백: 국가색 Cube (30x15x30)
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "CastleFallback";
                cube.transform.SetParent(castleRoot.transform, false);
                cube.transform.localPosition = Vector3.zero;
                cube.transform.localScale = new Vector3(30, 15, 30);
                cube.tag = "Untagged";
                var renderer = cube.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.material = MaterialHelper.CreateLitMaterial(GetCastleFallbackColor(nation), "Castle_Mat");
                Debug.Log($"[TerritoryBuilder] {parent.name}: castle '{castleKey}' GLB 로드 실패 — Cube 폴백");
            }

            castleRoot.transform.SetParent(parent);

            // 성문(GateAnchor): center에서 Empire 중심(0,0,0) 반대(바깥) 방향으로 castle 반경 60% 지점
            Vector3 c2 = new Vector3(center.x, 0, center.z);
            Vector3 gateDir = c2.sqrMagnitude < 0.0001f ? Vector3.back : Vector3.Normalize(c2);
            float radius = Mathf.Max(GetCastleRadius(castleRoot), 1f);
            var gate = new GameObject("GateAnchor");
            gate.transform.SetParent(castleRoot.transform);
            gate.transform.position = new Vector3(
                center.x + gateDir.x * radius * 0.6f,
                groundY,
                center.z + gateDir.z * radius * 0.6f);
        }

        /// <summary>
        /// 국가별 성문(castle) GLB 모델 키를 반환합니다.
        /// </summary>
        private static string GetCastleModelKey(NationType nation)
        {
            return nation switch
            {
                NationType.East => "blue_castle",
                NationType.West => "green_castle",
                NationType.South => "red_castle",
                NationType.North => "purple_castle",
                _ => "castle" // Empire / Dracula / 기타
            };
        }

        /// <summary>
        /// castle GLB 폴백(프리미티브 Cube)용 국가별 색상을 반환합니다.
        /// </summary>
        private static Color GetCastleFallbackColor(NationType nation)
        {
            return nation switch
            {
                NationType.East => new Color(0.25f, 0.45f, 0.9f),
                NationType.West => new Color(0.2f, 0.75f, 0.3f),
                NationType.South => new Color(0.85f, 0.2f, 0.15f),
                NationType.North => new Color(0.6f, 0.3f, 0.8f),
                _ => new Color(0.85f, 0.83f, 0.8f) // Empire / Dracula / 기타
            };
        }

        /// <summary>
        /// 렌더러 바운즈의 XZ 최대 반경(절반 폭)을 반환합니다. 렌더러가 없으면 0.
        /// </summary>
        private static float GetCastleRadius(GameObject castleGo)
        {
            var renderers = castleGo.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return 0f;
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return Mathf.Max(b.extents.x, b.extents.z);
        }

        /// <summary>
        /// castle GLB를 바운즈 기반 균일 스케일 정규화 — XZ 최대 반경을 목표 반경 25m로 맞춥니다.
        /// </summary>
        private static void NormalizeCastleScale(GameObject castleModel)
        {
            float sourceRadius = GetCastleRadius(castleModel);
            if (sourceRadius <= 0.01f) return;
            float factor = 25f / sourceRadius; // 목표 반경 25m
            castleModel.transform.localScale *= factor;
        }

        /// <summary>
        /// 성문(GateAnchor) 앞에 문지기 2~4명을 배치합니다.
        /// (guardCount는 전체 주둔군 수 — 전쟁 페이즈에서 사용하도록 그대로 유지하며 여기서는 문지기만 배치)
        /// </summary>
        private void BuildGuardsAt(Transform parent, Vector3 center, int guardCount, TerritoryDifficulty difficulty, NationType nation)
        {
            int baseLevel = GetBaseGuardLevel(difficulty);

            // 성문 방향 (Empire 중심 0,0,0 기준 바깥쪽)
            Vector3 c2 = new Vector3(center.x, 0, center.z);
            Vector3 gateDir = c2.sqrMagnitude < 0.0001f ? Vector3.back : Vector3.Normalize(c2);

            // 문지기 수: Ring1/Ring2=2, Ring3=3, Ring4=4
            int gatekeeperCount = difficulty switch
            {
                TerritoryDifficulty.Ring3 => 3,
                TerritoryDifficulty.Ring4 => 4,
                TerritoryDifficulty.Empire => 4,
                _ => 2, // Ring1 / Ring2 / Dracula
            };

            // GateAnchor 월드 위치 (Castle 아래)
            Transform gateAnchor = null;
            var castle = parent.Find("Castle");
            if (castle != null) gateAnchor = castle.Find("GateAnchor");
            Vector3 gatePos = gateAnchor != null ? gateAnchor.position : center + gateDir * 3f;

            // 문 좌우 방향 (gateDir에 수직인 단위벡터)
            Vector3 right = new Vector3(-gateDir.z, 0f, gateDir.x);

            for (int i = 0; i < gatekeeperCount; i++)
            {
                float sign = (i % 2 == 0) ? 1f : -1f;
                int tier = i / 2;
                float lateral = 2f + tier * 2f;          // 2, 2, 4, 4 (문에서 ±2m, ±4m)
                Vector3 p = gatePos + right * (sign * lateral);
                int level = baseLevel + (i % 3);          // 레벨 분산
                // 성을 등지고 바깥(성문 바깥, 접근하는 전투원 방향)을 향함
                CreateGuard($"GateGuard_{i + 1}", p, GetGuardName(nation), level, nation, parent, gateDir);
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
        /// GLB 모델이 있으면 Instantiate하고, 없으면 Primitive Placeholder를 생성합니다.
        /// </summary>
        private static GameObject TrySpawnModelOrPlaceholder(string modelKey, string name,
            Vector3 position, Vector3 scale, Color fallbackColor, PrimitiveType fallbackType)
        {
            // 지형 기저 y 보정: 월드 지표면 = GetHeightAt + 1f (Ground_Inner 월드 y=1 기저)
            float groundY = TerrainGenerator.GetHeightAt(position.x, position.z, BiomeType.Plains, 42) + 1f;

            // GLB 모델이 있는지 확인
            if (!string.IsNullOrEmpty(modelKey) && RuntimeModelLoader.TryGetModel(modelKey, out var modelPrefab))
            {
                GameObject modelGo = Object.Instantiate(modelPrefab);
                modelGo.name = name;
                // GLB 피벗은 바닥 기준 가정 → 약간 띄워 지면 겹침/징파이팅 방지
                modelGo.transform.position = new Vector3(position.x, groundY + 0.05f, position.z);
                modelGo.transform.localScale = scale;

                Debug.Log($"[TerritoryBuilder] GLB 모델 '{modelKey}'로 '{name}' 생성");
                return modelGo;
            }

            // GLB가 없으면 Primitive Placeholder 생성 — 프리미티브 기저 보정 (지면 위 바닥 정착)
            float baseOffsetY = fallbackType switch
            {
                PrimitiveType.Capsule => scale.y,          // 기본 캡슐 높이 2 × scale.y → 반높이 = scale.y
                PrimitiveType.Sphere => scale.y * 0.5f,
                _ => scale.y * 0.5f,                       // Cube(기본) 등
            };
            var go = GameObject.CreatePrimitive(fallbackType);
            go.name = name;
            go.transform.position = new Vector3(position.x, groundY + baseOffsetY, position.z);
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
        /// 병사 Placeholder 생성 (GLB "soldier" 우선, 없으면 Primitive Capsule) - 부모 지정 가능
        /// </summary>
        private void CreateGuard(string name, Vector3 position, string guardName, int level, NationType nation, Transform parent = null, Vector3 forward = default)
        {
            var go = TrySpawnModelOrPlaceholder("soldier", name, position,
                Vector3.one, new Color(0.2f, 0.4f, 0.8f), PrimitiveType.Capsule);

            // 성문/진영 방향 정면 설정 (바깥을 향함). 기본값이 아니면 적용
            if (forward != Vector3.zero)
                go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

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