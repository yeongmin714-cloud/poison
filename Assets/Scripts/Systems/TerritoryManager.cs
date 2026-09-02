using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 영지 관리자 — 현재 로드된 영지의 건물, 병사, 시설 등을 추적하고 관리합니다.
    /// TerritoryDatabase와 연동하여 영지 정의/상태를 제공합니다.
    ///</summary>
    /// 사용법:
    ///   TerritoryManager.Instance.CurrentTerritoryId  // 현재 영지 ID
    ///   TerritoryManager.Instance.TerritoryDatabase    // TerritoryDatabase 인스턴스
    /// 씬 기반 싱글톤 (FixMainScene에서 생성)
    public class TerritoryManager : MonoBehaviour
    {
        public static TerritoryManager Instance { get; private set; }
        static bool _isQuitting = false;

        // NOTE: RuntimeInitializeOnLoadMethod 제거 - FixMainScene에서 씬에 생성하므로 불필요
        // 4중 방어 패턴 불필요 (씬 기반으로 단순화)

        public static TerritoryManager GetOrCreate()
        {
            if (Instance == null && !_isQuitting)
            {
                var go = new GameObject("[TerritoryManager]");
                Instance = go.AddComponent<TerritoryManager>();
                DontDestroyOnLoad(go);
            }
            return Instance;
        }

        [Header("영지 설정")]
        [SerializeField] private NationType _currentNation = NationType.East;
        [SerializeField] private int _currentTerritoryIndex = 1;

        // 건물 목록
        private readonly Dictionary<string, BuildingPlaceholder> _buildings = new Dictionary<string, BuildingPlaceholder>();
        // 병사 목록
        private readonly Dictionary<string, GuardPlaceholder> _guards = new Dictionary<string, GuardPlaceholder>();

        // 캐싱: TerritoryDatabase 인스턴스
        private TerritoryDatabase _territoryDatabase;

        /// <summary>현재 영지의 TerritoryId</summary>
        public TerritoryId CurrentTerritoryId => new TerritoryId(_currentNation, _currentTerritoryIndex);

        /// <summary>로드된 모든 건물 이름 목록 (읽기 전용 복사본)</summary>
        public ICollection<string> BuildingNames => new List<string>(_buildings.Keys);

        /// <summary>로드된 모든 병사 이름 목록 (읽기 전용 복사본)</summary>
        public ICollection<string> GuardNames => new List<string>(_guards.Keys);

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // TerritoryDatabase 캐싱
            _territoryDatabase = TerritoryDatabase.Instance;
            if (_territoryDatabase == null)
            {
                Debug.LogError("[TerritoryManager] TerritoryDatabase.Instance가 null입니다. 영지 기능이 동작하지 않습니다.");
                return;
            }

            // 모든 건물과 병사 오브젝트를 찾아서 등록
            FindAndRegisterAll<BuildingPlaceholder>(_buildings, "건물");
            FindAndRegisterAll<GuardPlaceholder>(_guards, "병사");

            // 영지 데이터 출력 (null 방어)
            var def = _territoryDatabase.GetDefinition(CurrentTerritoryId);
            if (def.id.nation != NationType.None)
            {
                Debug.Log($"[TerritoryManager] 영지 초기화 완료: {def.territoryName} ({def.nation} Ring{(int)def.difficulty + 1}) " +
                          $"건물: {_buildings.Count}개, 병사: {_guards.Count}명");
            }
        }

        private void OnApplicationQuit() => _isQuitting = true;

        private void Start()
        {
            SpawnTerritoryNPCs();
        }

        /// <summary>
        /// 영지 초기화 시 NPC를 스폰합니다.
        /// TerritoryNPCSpawner(UI 어셈블리)를 리플렉션으로 호출 (Systems→UI 직접 참조 불가, 순환 참조 방지).
        /// </summary>
        private void SpawnTerritoryNPCs()
        {
            var def = CurrentDefinition;
            if (def.id.nation == NationType.None)
            {
                Debug.LogWarning("[TerritoryManager] 영지 정의가 없어 NPC 스폰을 건너뜁니다.");
                return;
            }

            var spawnerType = FindTypeInAssemblies("TerritoryNPCSpawner");
            if (spawnerType == null)
            {
                Debug.LogWarning("[TerritoryManager] TerritoryNPCSpawner 타입을 찾을 수 없습니다.");
                return;
            }

            string territoryId = CurrentTerritoryId.ToString();
            int tier = (int)def.difficulty + 1;
            Vector3 center = GetTerritoryCenter();

            try
            {
                var generateMethod = spawnerType.GetMethod("GenerateNPCs",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var getSpawnPosMethod = spawnerType.GetMethod("GetSpawnPosition",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var spawnMethod = spawnerType.GetMethod("SpawnNPC",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                if (generateMethod == null || getSpawnPosMethod == null || spawnMethod == null)
                {
                    Debug.LogWarning("[TerritoryManager] TerritoryNPCSpawner 메서드를 찾을 수 없습니다.");
                    return;
                }

                var npcsResult = generateMethod.Invoke(null, new object[] { territoryId, tier });
                if (npcsResult is System.Collections.IEnumerable npcs)
                {
                    int count = 0;
                    foreach (var npc in npcs)
                    {
                        int npcIndex = GetNpcIndex(npc);
                        Vector3 pos = (Vector3)getSpawnPosMethod.Invoke(null, new object[] { territoryId, npcIndex, center });
                        spawnMethod.Invoke(null, new object[] { npc, pos });
                        count++;
                    }
                    Debug.Log($"[TerritoryManager] NPC {count}명 스폰 완료 ({territoryId}, tier {tier})");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TerritoryManager] NPC 스폰 실패: {e.Message}");
            }
        }

        /// <summary>NPCInstance struct의 NpcIndex 필드/속성을 읽습니다.</summary>
        private static int GetNpcIndex(object npc)
        {
            if (npc == null) return 0;
            var t = npc.GetType();

            var field = t.GetField("NpcIndex");
            if (field != null && field.GetValue(npc) is int idx) return idx;

            var prop = t.GetProperty("NpcIndex");
            if (prop != null && prop.GetValue(npc) is int pIdx) return pIdx;

            return 0;
        }

        /// <summary>여러 네임스페이스/어셈블리에서 타입을 검색합니다.</summary>
        private static System.Type FindTypeInAssemblies(string typeName)
        {
            foreach (var ns in new[] { "", "ProjectName.UI.", "ProjectName.Systems.", "ProjectName.Core." })
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = asm.GetType(ns + typeName);
                    if (type != null) return type;
                }
            }
            return null;
        }

        /// <summary>
        /// 씬에서 특정 타입의 컴포넌트를 모두 찾아 딕셔너리에 등록합니다.
        /// Unity 6000+ FindObjectsByType 사용.
        /// </summary>
        private static void FindAndRegisterAll<T>(Dictionary<string, T> registry, string label) where T : Component
        {
            var objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            foreach (var obj in objects)
            {
                if (!registry.ContainsKey(obj.name))
                {
                    registry.Add(obj.name, obj);
                }
                else
                {
                    Debug.LogWarning($"[TerritoryManager] 중복된 {label} 이름 발견: {obj.name}");
                }
            }
        }

        /// <summary>
        /// 씬 스캔을 다시 수행해 건물/병사를 딕셔너리에 재등록합니다.
        /// TerritoryBuilder가 영지 오브젝트를 Awake 이후(분산 코루틴)에 생성하므로,
        /// 빌드 완료 후 이 메서드를 호출해야 등록이 반영됩니다.
        /// </summary>
        public void RefreshRegistrations()
        {
            _buildings.Clear();
            _guards.Clear();
            FindAndRegisterAll<BuildingPlaceholder>(_buildings, "건물");
            FindAndRegisterAll<GuardPlaceholder>(_guards, "병사");
            Debug.Log($"[TerritoryManager] 영지 재등록: 건물 {_buildings.Count}개, 병사 {_guards.Count}명");
        }

        /// <summary>
        /// 건물 이름으로 건물 객체 찾기
        /// </summary>
        public BuildingPlaceholder GetBuilding(string name)
        {
            _buildings.TryGetValue(name, out var building);
            return building;
        }

        /// <summary>
        /// 병사 이름으로 병사 객체 찾기
        /// </summary>
        public GuardPlaceholder GetGuard(string name)
        {
            _guards.TryGetValue(name, out var guard);
            return guard;
        }

        /// <summary>
        /// 특정 유형의 모든 건물을 읽기 전용 리스트로 반환합니다.
        /// (yield return 대신 List를 반환하여 GC Alloc을 호출 측에서 제어 가능)
        /// </summary>
        public List<BuildingPlaceholder> GetBuildingsByType(BuildingPlaceholder.BuildingType type)
        {
            var results = new List<BuildingPlaceholder>(_buildings.Count);
            foreach (var building in _buildings.Values)
            {
                if (building.buildingType == type)
                    results.Add(building);
            }
            return results;
        }

        /// <summary>
        /// 현재 영지의 중심점 계산 (모든 건물의 평균 위치)
        /// </summary>
        public Vector3 GetTerritoryCenter()
        {
            if (_buildings.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var building in _buildings.Values)
            {
                sum += building.transform.position;
            }
            return sum / _buildings.Count;
        }

        /// <summary>특정 영지 ID의 중심점을 반환합니다. (건물 평균 위치로 폴백)</summary>
        public Vector3 GetTerritoryCenter(TerritoryId territoryId)
        {
            // TerritoryDefinition은 centerPosition 필드가 없으므로
            // 현재 로드된 건물들의 중심점으로 폴백합니다.
            return GetTerritoryCenter();
        }

        /// <summary>
        /// 영지 소유주 변경 (TerritoryBannerSystem 호출용 스텁)
        /// </summary>
        public void SetTerritoryOwner(string territoryId, NationType owner)
        {
            Debug.Log($"[TerritoryManager] SetTerritoryOwner: {territoryId} → {owner} (스텁)");
        }

        /// <summary>
        /// 현재 영지의 난이도 설명 문자열
        /// </summary>
        public string GetDifficultyDescription()
        {
            var db = _territoryDatabase ?? TerritoryDatabase.Instance;
            if (db == null) return "알 수 없음 (DB 없음)";

            var def = db.GetDefinition(CurrentTerritoryId);
            if (def.id.nation == NationType.None) return "알 수 없음 (정의 없음)";

            return def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => "🟢 쉬움 (Ring 1)",
                TerritoryDifficulty.Ring2 => "🟡 보통 (Ring 2)",
                TerritoryDifficulty.Ring3 => "🟠 어려움 (Ring 3)",
                TerritoryDifficulty.Ring4 => "🔴 매우 어려움 (Ring 4)",
                TerritoryDifficulty.Empire => "👑 황제국",
                _ => "알 수 없음"
            };
        }

        /// <summary>TerritoryDatabase 인스턴스 (안전 접근)</summary>
        public TerritoryDatabase TerritoryDatabase
        {
            get
            {
                if (_territoryDatabase == null)
                    _territoryDatabase = TerritoryDatabase.Instance;
                return _territoryDatabase;
            }
        }

        /// <summary>현재 영지 정의 (null-safe)</summary>
        public TerritoryDefinition CurrentDefinition
        {
            get
            {
                var db = TerritoryDatabase;
                return db != null ? db.GetDefinition(CurrentTerritoryId) : new TerritoryDefinition();
            }
        }

        /// <summary>현재 영지 상태 (null-safe)</summary>
        public TerritoryState CurrentState
        {
            get
            {
                var db = TerritoryDatabase;
                return db != null ? db.GetState(CurrentTerritoryId) : null;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            _territoryDatabase = null;
        }
    }
}