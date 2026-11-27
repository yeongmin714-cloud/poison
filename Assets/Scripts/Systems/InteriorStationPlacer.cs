// 2026-10-23 (122차 R1b): 빈 실내 시설 직접 배치 — [122차 빈 실내 규칙] 완성.
// FurnishStations=false로 생성된 빈 실내에서 플레이어가 골드를 지불하고 4종 시설을 직접 배치한다:
//   1. 저장고(창고 선반) — TerritoryWarehouse 부착 + WarehouseSystem.MarkWarehouseBuilt(영지 키)
//      → 병사 채집/채광/사냥 일일 원정 수확물 적립 대상(R2 게이트 해제).
//   2. 작업대(크래프트) — TerritoryCraftingStation 부착 (장비 제작창).
//   3. 요리 테이블 — CookingStation 부착 (요리창).
//   4. 연금 테이블 — AlchemyStation 부착 (연금창).
// 배치 상태는 정적 레지스트리(영지 키별)로 관리 — 실내 재진입(방 리빌드) 시 복원.
// 골드 차감은 PlayerStats.SpendGold(source "interior_station") — O2 원장 규약 준수.
// 키 폴링은 Input System(ArenaSystem L743 선례 — Keyboard.current.digit1Key 등).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>배치 가능한 실내 시설 정의.</summary>
    public class InteriorStationDef
    {
        public string id;
        public string displayName;
        public string promptKey;       // 표시용 ("1"~"4")
        public int goldCost;
        public Vector3 position;       // 방 로컬 기준 (mirrorX 미적용 원본)
            }

    /// <summary>
    /// 빈 실내 시설 배치 시스템 — 정적 레지스트리 + 방 부착 프롬프트.
    /// PlayerCastleInteriorBuilder(빈 실내 규칙)에서 OnEmptyInteriorBuilt로 진입.
    /// </summary>
    public static class InteriorStationPlacer
    {
        // ── 배치 가능 시설 테이블 (4종 — [122차 R1b]) ──
        public static readonly InteriorStationDef[] Stations =
        {
            new InteriorStationDef
            {
                id = "warehouse", displayName = "저장고", promptKey = "1", goldCost = 250,
                position = new Vector3(16f, 0f, 1.5f),
            },
            new InteriorStationDef
            {
                id = "workbench", displayName = "작업대(크래프트)", promptKey = "2", goldCost = 300,
                position = new Vector3(-16f, 0f, -16f),
            },
            new InteriorStationDef
            {
                id = "cooking", displayName = "요리 테이블", promptKey = "3", goldCost = 200,
                position = new Vector3(1f, 0f, 14f),
            },
            new InteriorStationDef
            {
                id = "alchemy", displayName = "연금 테이블", promptKey = "4", goldCost = 350,
                position = new Vector3(6f, 0f, 9f),
            },
        };

        /// <summary>UI 어셈블리 상호작용 컴포넌트 타입 이름(리플렉션 부착 — asmdef 경계).</summary>
        public const string WarehouseTypeName = "ProjectName.UI.TerritoryWarehouse, ProjectName.UI";
        public const string CraftingStationTypeName = "ProjectName.UI.TerritoryCraftingStation, ProjectName.UI";
        public const string CookingStationTypeName = "ProjectName.UI.CookingStation, ProjectName.UI";
        public const string AlchemyStationTypeName = "ProjectName.UI.AlchemyStation, ProjectName.UI";

        /// <summary>영지 키 → 배치 완료 시설 id 집합 (정적 — 방 리빌드 복원 소스).</summary>
        private static readonly Dictionary<string, HashSet<string>> _placed =
            new Dictionary<string, HashSet<string>>();

        private static Material _shelfMat;
        private static Material _workbenchMat;
        private static Material _cookingMat;
        private static Material _alchemyMat;
        private static Material _standMat;

        // ─────────────────────────────────────────────────────────────
        // 조회 API
        // ─────────────────────────────────────────────────────────────
        public static bool IsPlaced(string territoryKey, string stationId)
        {
            return _placed.TryGetValue(territoryKey ?? "", out var set) && set.Contains(stationId);
        }

        /// <summary>
        /// 빈 실내 진입 — 배치 프롬프트만 부착(빌더 훅). 기존 배치 복원은 프롬프트 Start(런타임)에서
        /// 수행 — EditMode 테스트는 Start 미실행이라 정적 레지스트리 잔여로 오염되지 않음.
        /// </summary>
        public static void OnEmptyInteriorBuilt(GameObject room, string territoryKey)
        {
            if (room == null || string.IsNullOrEmpty(territoryKey)) return;
            if (room.GetComponent<InteriorStationPlacerPrompt>() != null) return;   // 멱등

            var prompt = room.AddComponent<InteriorStationPlacerPrompt>();
            prompt.TerritoryKey = territoryKey;
        }

        /// <summary>영지 키에 배치 완료된 시설 id 목록(복원 순회용 — 없으면 빈 컬렉션).</summary>
        public static IEnumerable<string> PlacedIds(string territoryKey)
        {
            return _placed.TryGetValue(territoryKey ?? "", out var set) ? (IEnumerable<string>)set : System.Array.Empty<string>();
        }

        /// <summary>테스트용 정적 상태 리셋(WarehouseSystem.UnmarkWarehouseForTest 선례).</summary>
        public static void ResetForTest()
        {
            _placed.Clear();
        }

        internal static InteriorStationDef FindDefForTest(string id)
        {
            foreach (var d in Stations)
                if (d.id == id) return d;
            return null;
        }

        // ─────────────────────────────────────────────────────────────
        // 배치 실행
        // ─────────────────────────────────────────────────────────────
        /// <summary>시설 배치 — 골드 차감 + 실물 생성 + UI 컴포넌트 부착 + 레지스트리 기록. 실패 시 사유 반환(null=성공).</summary>
        public static string TryPlace(GameObject room, string territoryKey, string stationId)
        {
            var def = FindDefForTest(stationId);
            if (def == null) return "알 수 없는 시설";
            if (room == null) return "실내가 없습니다";
            if (IsPlaced(territoryKey, stationId)) return $"이미 배치된 시설: {def.displayName}";

            var stats = PlayerStats.Instance != null ? PlayerStats.Instance : Object.FindAnyObjectByType<PlayerStats>();
            if (stats == null) return "PlayerStats 없음";
            if (stats.Gold < def.goldCost) return $"골드 부족 (필요 {def.goldCost}G)";
            if (!stats.SpendGold(def.goldCost, "interior_station")) return "골드 차감 실패";

            SpawnStationForTest(room, territoryKey, def, silent: false);
            return null;
        }

        /// <summary>시설 실물 생성 + UI 상호작용 부착 + 레지스트리 기록.</summary>
        internal static void SpawnStationForTest(GameObject room, string territoryKey, InteriorStationDef def, bool silent)
        {
            EnsureMaterials();
            float x = def.position.x;   // [레거시 정합] 주 시설 좌표는 mx 미적용(PlayerCastleInteriorBuilder 동일)
            var pos = new Vector3(x, def.position.y, def.position.z);

            GameObject station = null;
            switch (def.id)
            {
                case "warehouse":
                {
                    // 저장고 — 창고 선반 2개 + TerritoryWarehouse 부착 + MarkWarehouseBuilt
                    var shelf1 = IndoorFurnitureCatalog.CreateShelf(2.2f, 2.2f, 0.6f, _shelfMat, 3);
                    shelf1.name = "StorageShelf_1";
                    shelf1.transform.SetParent(room.transform, false);
                    shelf1.transform.localPosition = pos;
                    shelf1.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                    var shelf2 = IndoorFurnitureCatalog.CreateShelf(2.2f, 2.2f, 0.6f, _shelfMat, 3);
                    shelf2.name = "StorageShelf_2";
                    shelf2.transform.SetParent(room.transform, false);
                    shelf2.transform.localPosition = pos + new Vector3(0f, 0f, 2.5f);
                    shelf2.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                    AttachUiComponent(shelf2, WarehouseTypeName, territoryKey);
                    WarehouseSystem.MarkWarehouseBuilt(territoryKey);
                    station = shelf1;
                    break;
                }
                case "workbench":
                {
                    var workbench = IndoorFurnitureCatalog.CreateCounter(2.6f, 1.0f, 1.0f, _workbenchMat);
                    workbench.name = "Workbench";
                    workbench.transform.SetParent(room.transform, false);
                    workbench.transform.localPosition = pos;
                    AttachUiComponent(workbench, CraftingStationTypeName, territoryKey, "영지 작업대");
                    station = workbench;
                    break;
                }
                case "cooking":
                {
                    var table = IndoorFurnitureCatalog.CreateCounter(1.4f, 0.95f, 0.9f, _cookingMat);
                    table.name = "CookingTable";
                    table.transform.SetParent(room.transform, false);
                    table.transform.localPosition = pos;
                    AttachUiComponent(table, CookingStationTypeName);
                    station = table;
                    break;
                }
                case "alchemy":
                {
                    var table = IndoorFurnitureCatalog.CreateTable(1.4f, 1.4f, 0.95f, _alchemyMat);
                    table.name = "AlchemyTable";
                    table.transform.SetParent(room.transform, false);
                    table.transform.localPosition = pos;
                    AttachUiComponent(table, AlchemyStationTypeName);
                    station = table;
                    break;
                }
            }

            if (station == null) return;

            if (!_placed.TryGetValue(territoryKey, out var set))
            {
                set = new HashSet<string>();
                _placed[territoryKey] = set;
            }
            set.Add(def.id);

            if (!silent)
                Debug.Log($"[InteriorStationPlacer] 시설 배치 — {def.displayName} @ {territoryKey} ({def.goldCost}G 지출)");
        }

        // ─────────────────────────────────────────────────────────────
        // UI 어셈블리 리플렉션 부착 (asmdef 경계 — PlayerCastleInteriorBuilder 선례)
        // ─────────────────────────────────────────────────────────────
        private static void AttachUiComponent(GameObject target, string assemblyQualifiedTypeName,
            params object[] configureArgs)
        {
            if (target == null) return;
            System.Type uiType = System.Type.GetType(assemblyQualifiedTypeName);
            if (uiType == null)
            {
                Debug.LogWarning($"[InteriorStationPlacer] '{assemblyQualifiedTypeName}' 타입 미발견 — 상호작용 없이 진행.");
                return;
            }
            Component component = target.AddComponent(uiType);
            if (component == null) return;

            var configure = uiType.GetMethod("Configure");
            if (configure == null) return;
            var parameters = configure.GetParameters();
            var invokeArgs = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < configureArgs.Length && configureArgs[i] != null)
                    invokeArgs[i] = configureArgs[i];
                else if (parameters[i].HasDefaultValue)
                    invokeArgs[i] = parameters[i].DefaultValue;
            }
            configure.Invoke(component, invokeArgs);
        }

        private static void EnsureMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (_shelfMat == null)
                _shelfMat = new Material(shader) { name = "Placer_ShelfMat", color = new Color(0.55f, 0.42f, 0.26f) };
            if (_workbenchMat == null)
                _workbenchMat = new Material(shader) { name = "Placer_WorkbenchMat", color = new Color(0.35f, 0.24f, 0.14f) };
            if (_cookingMat == null)
                _cookingMat = new Material(shader) { name = "Placer_CookingMat", color = new Color(0.50f, 0.32f, 0.18f) };
            if (_alchemyMat == null)
                _alchemyMat = new Material(shader) { name = "Placer_AlchemyMat", color = new Color(0.36f, 0.26f, 0.44f) };
            if (_standMat == null)
                _standMat = new Material(shader) { name = "Placer_StandMat", color = new Color(0.42f, 0.36f, 0.55f) };
        }
    }

    /// <summary>
    /// 빈 실내 배치 프롬프트 — 플레이어 근접 시 OnGUI 안내 + 숫자키 1~4 배치.
    /// 실내 방(리빌드마다 재생성)에 부착 — [122차 R1b].
    /// </summary>
    public class InteriorStationPlacerPrompt : MonoBehaviour
    {
        public string TerritoryKey;

        // 근접 판정 — 실내는 단일 공간이라 방 트랜스폰 기준 완화 반경(프롬프트 상시 표시).
        private bool _playerNearby;
        private float _lastMessage;
        private string _statusMessage;
        private bool _restored;

        /// <summary>런타임 복원 — 방 리빌드(재진입) 시 레지스트리에 기록된 시설을 무음 재생성.
        /// EditMode 테스트는 Start 미실행이라 정적 레지스트리 잔여로 오염되지 않음.</summary>
        private void Start()
        {
            if (_restored || string.IsNullOrEmpty(TerritoryKey)) return;
            _restored = true;
            var room = transform.parent != null ? transform.parent.gameObject : gameObject;
            foreach (string id in new List<string>(InteriorStationPlacer.PlacedIds(TerritoryKey)))
            {
                var def = InteriorStationPlacer.FindDefForTest(id);
                if (def != null)
                    InteriorStationPlacer.SpawnStationForTest(room, TerritoryKey, def, silent: true);
            }
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(TerritoryKey)) return;

            // 근접 판정 — 플레이어와 방 기준 거리(실내는 단일 공간이라 방 트랜스폰 기준 완화 반경)
            var player = GameObject.FindGameObjectWithTag("Player");
            _playerNearby = player != null &&
                Vector3.Distance(player.transform.position, transform.position) < 12f;
            if (!_playerNearby) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            var room = transform.parent != null ? transform.parent.gameObject : gameObject;
            for (int i = 0; i < InteriorStationPlacer.Stations.Length; i++)
            {
                var def = InteriorStationPlacer.Stations[i];
                UnityEngine.InputSystem.Controls.KeyControl key = i switch
                {
                    0 => kb.digit1Key, 1 => kb.digit2Key, 2 => kb.digit3Key, _ => kb.digit4Key,
                };
                if (key == null || !key.wasPressedThisFrame) continue;

                string error = InteriorStationPlacer.TryPlace(room, TerritoryKey, def.id);
                _statusMessage = error != null
                    ? $"✗ {def.displayName}: {error}"
                    : $"✓ {def.displayName} 배치 완료";
                _lastMessage = Time.time;
                break;   // 프레임당 1배치
            }
        }

        private void OnGUI()
        {
            if (!_playerNearby || string.IsNullOrEmpty(TerritoryKey)) return;

            float y = Screen.height - 150f;
            GUI.Box(new Rect(12f, y, 460f, 120f), " [122차] 빈 실내 — 시설 직접 배치 (숫자키)");
            y += 24f;
            foreach (var def in InteriorStationPlacer.Stations)
            {
                bool placed = InteriorStationPlacer.IsPlaced(TerritoryKey, def.id);
                string state = placed ? "✓ 배치됨" : $"{def.goldCost} G — [{def.promptKey}]";
                GUI.Label(new Rect(20f, y, 440f, 22f), $"{def.promptKey}. {def.displayName}  —  {state}");
                y += 22f;
            }
            if (!string.IsNullOrEmpty(_statusMessage) && Time.time - _lastMessage < 4f)
            {
                GUI.Label(new Rect(20f, y + 2f, 440f, 22f), _statusMessage);
            }
        }
    }
}
