// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 618
using System.Collections.Generic;
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

        /// <summary>
        /// 영지/병사 배치 일시중단 스위치.
        /// 지형 Play 검증 중 렉 원인(성 GLB 로드 + 문지기 병사 FBX/애니 부착 82개 영지)을 차단하기 위해
        /// 2026-09-04 일시 true로 설정했던 것. 지형 고도화·검증 완료(계획 v3 벽/회반죽) 후 메인씬 복귀를 위해
        /// false로 되돌림 — SetPaused(false) 호출로 런타임 재개도 가능.
        /// </summary>
        public static bool BuildPaused = false;

        /// <summary>
        /// 영지/병사 배치 일시중단 여부를 토글합니다. 값이 실제로 바뀔 때만 로그를 남깁니다.
        /// false로 전환 시 _hasBuilt를 리셋하고, 현재 씬에 builder가 살아 있으면 코루틴을 재시작합니다.
        /// </summary>
        public static void SetPaused(bool paused)
        {
            if (BuildPaused == paused) return;

            BuildPaused = paused;
            Debug.Log($"[TerritoryBuilder] 영지/병사 배치 {(paused ? "일시중지" : "재개")}");

            if (!paused)
            {
                // 재개 시 중단된 상태(_hasBuilt=false)에서 배치가 동작하도록 리셋 + 재시작 여지 제공
                var builder = FindAnyObjectByType<TerritoryBuilder>();
                if (builder != null)
                {
                    builder._hasBuilt = false;
                    if (builder.isActiveAndEnabled)
                    {
                        // Start()가 이미 끝났다면 명시적으로 코루틴 재개
                        builder.StartCoroutine(builder.BuildAllCoroutine());
                    }
                }
            }
        }

        private void Start()
        {
            if (BuildPaused)
            {
                Debug.Log("[TerritoryBuilder] 지형 검증 중 영지/병사 배치 일시중지 (BuildPaused=true)");
                return;
            }

            if (_autoBuildOnStart)
            {
                // 82개 영지를 1프레임당 1개씩 분산 생성 (프리징 방지)
                StartCoroutine(BuildAllCoroutine());
            }
        }

        /// <summary>Backward-compatible: BuildTerritory() = BuildAllTerritories()  (테스트 호환)</summary>
        public void BuildTerritory()
        {
            if (BuildPaused) return; // 지형 검증 중 일시중단 — 조용히 무시
            BuildAllTerritories();
        }

        /// <summary>전체 82개 영지 건물과 병사 생성 (중복 방지)</summary>
        public void BuildAllTerritories()
        {
            if (BuildPaused) return; // 지형 검증 중 일시중단 — 조용히 무시
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
            bool pauseLogged = false; // 순회 중 일시중단 시 로그 1회만

            foreach (var def in definitions)
            {
                // 런타임 토글 대응: 순회 중 BuildPaused로 바뀌면 중단 (_hasBuilt=false 유지 → 재개 가능)
                if (BuildPaused)
                {
                    if (!pauseLogged)
                    {
                        Debug.Log($"[TerritoryBuilder] 순회 중 일시중단 → 배치 중단 (현재 {builtCount}개 생성됨, BuildPaused=true)");
                        pauseLogged = true;
                    }
                    yield break;
                }

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

            // 4방위국 아레나 배치 (영지 아님 — 메인씬에 국별 아레나 4곳 + 문 트리거)
            try
            {
                BuildArenaRegions();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TerritoryBuilder] 아레나 배치 실패: {e.Message}");
            }
        }

        /// <summary>TerritoryManager에 건물/병사 재등록 요청 (빌드 완료 후 스캔)</summary>
        private void RefreshRegistrations()
        {
            if (TerritoryManager.Instance != null)
                TerritoryManager.Instance.RefreshRegistrations();
        }

        /// <summary>
        /// 4방위국(동/서/남/북) 아레나를 메인씬에 배치 — 영지와 무관한 국별 방향 특정 지점.
        /// 각 아레나는 국가 기준 각도 방향, 링 사이 중간 거리(1150m)에 놓아 영지 밀집과 분리.
        /// 문(BuildingTrigger TYPE_ARENA) = E키 상호작용 → ArenaInteriorBuilder 실내 진입.
        /// </summary>
        public void BuildArenaRegions()
        {
            BuildArenaRegion(NationType.East,  "Eastern",  0f);
            BuildArenaRegion(NationType.North, "Northern", 90f);
            BuildArenaRegion(NationType.West,  "Western",  180f);
            BuildArenaRegion(NationType.South, "Southern", 270f);
            Debug.Log("[TerritoryBuilder] 4방위국 아레나 4곳 배치 완료");
        }

        /// <summary>국가 방향 각도(도)와 링 중간 거리로 아레나 1곳 생성.</summary>
        private void BuildArenaRegion(NationType nation, string nationStyle, float angleDeg)
        {
            string rootName = $"Arena_{nationStyle}";
            if (GameObject.Find(rootName) != null) return; // 중복 방지

            // 링1(1450)과 링2(1000) 사이 중간 거리 → 영지 밀집과 분리된 국가 영역 내 지점
            const float arenaDistance = 1150f;
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(rad) * arenaDistance, 0f, Mathf.Sin(rad) * arenaDistance);

            var root = new GameObject(rootName);
            root.transform.position = pos;

            // 지형 높이 보정 (아레나 바닥이 지면에 닿도록)
            float groundY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, 42) + 1f;
            root.transform.position = new Vector3(pos.x, groundY, pos.z);

            // 아레나 출입문(BuildingTrigger) — E키로 실내 진입. 문은 루트 중심 남쪽(World 방향은 일괄 +Z)에 배치.
            Vector3 doorPos = new Vector3(pos.x, groundY, pos.z + 6f);
            IndoorTransitionSetup.CreateArenaTrigger(doorPos, nationStyle, root.transform);

            Debug.Log($"[TerritoryBuilder] 아레나 배치: {rootName} @ {pos} (angle {angleDeg}°, style {nationStyle})");
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

            // 문지기 생성 (성문 앞 2~5명, guardCount는 전쟁 페이즈 데이터로 유지)
            // Phase C: 문지기 수 = 영주 방어 성향 기반 배분(LordPersonalitySystem) — territoryId 전달 필요
            BuildGuardsAt(parentGo.transform, center, def.id, def.guardCount, def.difficulty, def.nation);
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

            // 성문(GateAnchor): center에서 Empire 중심(0,0,0) 반대(바깥) 방향, castle 성벽 바로 밖(반경 108%)
            // (기존 60%는 성벽 안쪽이라 문지기가 성 메시에 파묻혀 안 보였음)
            Vector3 c2 = new Vector3(center.x, 0, center.z);
            Vector3 gateDir = c2.sqrMagnitude < 0.0001f ? Vector3.back : Vector3.Normalize(c2);
            float radius = Mathf.Max(GetCastleRadius(castleRoot), 1f);
            var gate = new GameObject("GateAnchor");
            gate.transform.SetParent(castleRoot.transform);
            gate.transform.position = new Vector3(
                center.x + gateDir.x * radius * 1.08f,
                groundY,
                center.z + gateDir.z * radius * 1.08f);

            // 성문 앞 BuildingTrigger (외부 → 성 내부 진입). nationStyle/territoryKey를 전달해 내부 등장·소유 판정에 사용.
            string castleNationStyle = GetNationStyle(nation);
            // parent.name = "Territory_{nation}_{index:02}" → "Territory_" 제거로 "East_01" 형식 키 생성 (TerritoryId.ToString()과 동일)
            string territoryKey = parent.name.StartsWith("Territory_") ? parent.name.Substring("Territory_".Length) : null;
            IndoorTransitionSetup.CreateBuildingTrigger(
                gate.transform.position,
                IndoorTransitionSetup.TYPE_CASTLE,
                IndoorTransitionSetup.CASTLE_INTERACT_RANGE,
                castleRoot.transform,
                castleNationStyle,
                territoryKey);
            Debug.Log($"[TerritoryBuilder] {parent.name}: 성문 Castle BuildingTrigger 생성 (nationStyle: {castleNationStyle})");
        }

        /// <summary>
        /// NationType → 건물 인테리어 국가 스타일 문자열 매핑.
        /// (CastleInteriorBuilder/IndoorSceneTransition의 nationStyle 파라미터 형식)
        /// </summary>
        private static string GetNationStyle(NationType nation)
        {
            return nation switch
            {
                NationType.East => "Eastern",
                NationType.West => "Western",
                NationType.South => "Southern",
                NationType.North => "Northern",
                _ => "Empire" // None / Empire / Dracula / 기타
            };
        }

        /// <summary>
        /// 성 내부(R2)에 상점과 크래프트하우스를 배치합니다.
        /// IndoorSceneTransition의 castle 케이스에서 BuildCastleInterior() 후 호출됩니다.
        /// </summary>
        /// <param name="roomCenter">성 내부 방 중심 (BuildCastleInterior 반환 GameObject.transform.position)</param>
        /// <param name="nationStyle">국가 스타일 (Eastern, Western, Southern, Northern, Empire)</param>
        public static void SpawnInteriorFixtures(Vector3 roomCenter, string nationStyle)
        {
            //── 상점 (roomCenter 좌측 5m) ──
            Vector3 shopPos = new Vector3(roomCenter.x - 5f, roomCenter.y, roomCenter.z);
            Vector3 shopScale = new Vector3(2.5f, 2f, 2.5f);
            GameObject shop = TrySpawnModelOrPlaceholder("hut", "Interior_Shop", shopPos, shopScale,
                new Color(0.8f, 0.6f, 0.2f), PrimitiveType.Cube);
            if (shop != null)
            {
                PlaceOnIndoorFloor(shop, roomCenter.y); // y를 실내 바닥 기준으로 고정
                var ph = shop.AddComponent<BuildingPlaceholder>();
                ph.buildingType = BuildingPlaceholder.BuildingType.Shop;
                ph.buildingName = "상점";
                IndoorTransitionSetup.CreateBuildingTrigger(shopPos, IndoorTransitionSetup.TYPE_SHOP,
                    IndoorTransitionSetup.DEFAULT_INTERACT_RANGE, shop.transform);
                Debug.Log($"[TerritoryBuilder] 성 내부 상점 배치 (style: {nationStyle})");
            }

            //── 크래프트하우스 (roomCenter 우측 5m) ──
            Vector3 craftPos = new Vector3(roomCenter.x + 5f, roomCenter.y, roomCenter.z);
            Vector3 craftScale = new Vector3(2.5f, 2f, 2.5f);
            GameObject craft = TrySpawnModelOrPlaceholder("craft_blend", "Interior_CraftHouse", craftPos, craftScale,
                new Color(0.6f, 0.8f, 0.2f), PrimitiveType.Cube);
            if (craft != null)
            {
                PlaceOnIndoorFloor(craft, roomCenter.y); // y를 실내 바닥 기준으로 고정
                var ph = craft.AddComponent<BuildingPlaceholder>();
                ph.buildingType = BuildingPlaceholder.BuildingType.CraftHouse;
                ph.buildingName = "크래프트하우스";
                IndoorTransitionSetup.CreateBuildingTrigger(craftPos, IndoorTransitionSetup.TYPE_CRAFT_HOUSE,
                    IndoorTransitionSetup.DEFAULT_INTERACT_RANGE, craft.transform);
                Debug.Log($"[TerritoryBuilder] 성 내부 크래프트하우스 배치 (style: {nationStyle})");
            }
        }

        /// <summary>
        /// 실내 배치 오브젝트의 Y좌표를 실내 바닥(roomCenter.y) 기준으로 고정합니다.
        /// TrySpawnModelOrPlaceholder는 월드 지형 높이(GetHeightAt)로 보정하므로,
        /// 실내 씬에서 사용할 땐 이 메서드로 재보정합니다.
        /// </summary>
        private static void PlaceOnIndoorFloor(GameObject go, float floorY)
        {
            var pos = go.transform.position;
            go.transform.position = new Vector3(pos.x, floorY, pos.z);
        }

        /// <summary>
        /// [Phase F-1] 영주 성 실내 수비 병사 스폰 — Phase A 성향 배분(interiorDefenseSoldiers)만큼
        /// roomCenter 주변에 병사를 배치하고 플레이어 적대 설정. 소프트 게이트(야외 문지기 전원 포섭)를
        /// 뚫은 플레이어가 실내 진입 시, 성 내부 미배치 수비병이 전원 공격하도록 한다.
        /// (실내 상점/크래프트는 플레이어 소유 성으로 이관 — 타 영주 성은 영주실+수비병만)
        /// </summary>
        /// <param name="roomCenter">성 내부 방 중심 (BuildCastleInterior 반환 transform.position, 실내 바닥 y=0)</param>
        /// <param name="nationStyle">국가 스타일 (로그용)</param>
        /// <param name="territoryKey">영지 키 (예: "East_01") — 성향 배분 조회용</param>
        public static void SpawnInteriorDefenseGuards(Vector3 roomCenter, string nationStyle, string territoryKey)
        {
            try
            {
                if (string.IsNullOrEmpty(territoryKey)) return;
                var db = TerritoryDatabase.Instance;
                if (db == null) return;

                var state = db.GetState(territoryKey);
                if (state == null) return;
                var def = db.GetDefinition(state.id);
                if (def.id.nation == NationType.None) return; // 판정 불가 — 스폰 생략

                int count = LordPersonalitySystem.GetDeploymentPlan(def.id).interiorDefenseSoldiers;
                if (count <= 0) return;

                int baseLevel = GetBaseGuardLevel(def.difficulty);
                for (int i = 0; i < count; i++)
                {
                    float lat = ((i % 2) == 0 ? 1f : -1f) * (2f + (i / 2) * 2f); // ±2, ∓2, ±4, ∓4 ...
                    float dep = 2f + (i / 2) * 3f; // 왕좌 앞쪽으로 깊이 배치
                    Vector3 pos = new Vector3(roomCenter.x + lat, roomCenter.y, roomCenter.z + dep);
                    var go = CreateGuard($"InteriorGuard_{i + 1}", pos, GetGuardName(def.nation), baseLevel + (i % 3), def.nation, null, default, def.difficulty);
                    if (go == null) continue;

                    // 실내 바닥 y 고정(월드 지형 높이 오염 방지 — CreateGuard는 GetHeightAt 보정함)
                    go.transform.position = new Vector3(pos.x, roomCenter.y + 0.05f, pos.z);

                    var ph = go.GetComponent<GuardPlaceholder>();
                    if (ph != null)
                    {
                        ph.SetInCombat(true);
                        ph.HostileToPlayerFaction = true; // 실내 진입자 플레이어 공격 대상(실외 영지 기반 GuardHostilitySystem은 실내 미적용)
                    }
                    Debug.Log($"[TerritoryBuilder] 영주 성 실내 수비 병사 배치: {go.name} (Lv.{baseLevel + (i % 3)})");
                }
                Debug.Log($"[TerritoryBuilder] 영주 성 실내 수비 병사 {count}명 배치 완료 (style: {nationStyle}, {territoryKey})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TerritoryBuilder] 실내 수비 병사 배치 실패: {e.Message}");
            }
        }

        /// <summary>
        /// [2026-10-07] 플레이어 소유 성(PlayerCastleInteriorBuilder)의 막사에 아군 병사를 배치.
        /// room의 자식 "BarracksMapTable"(병사 관리 테이블) 주변으로 3명을 놓아 실제 플레이어 성 실내에
        /// 아군 수비병이 보이도록 한다. CreateGuard가 월드 지형 높이로 보정하므로 실내 바닥 y로 재고정한다.
        /// 적 실내 수비병(SpawnInteriorDefenseGuards)과 달리 SetRecruited(true) 아군 + 플레이어 비적대.
        /// </summary>
        /// <param name="room">플레이어 성 방 루트(PlayerCastleInteriorBuilder 반환값)</param>
        /// <param name="nationStyle">국가 스타일 (로그용)</param>
        /// <param name="count">배치할 아군 병사 수 (기본 3)</param>
        public static void SpawnInteriorAlliedGuards(GameObject room, string nationStyle, int count = 3)
        {
            try
            {
                if (room == null || count <= 0) return;

                // 배럭 지휘탁자(병사 관리 테이블) 위치: authored localPosition(-12.25,0,-14.3)이
                // 2.45 배 스케일 패스 후 room 기준 final local(-30,0,-35)로 확정됨. room.transform.position은
                // 실내 바닥 원점이므로 거기에 상대 오프셋을 더한다(Transform.gameObject 체이닝 불가 회피).
                Vector3 roomBase = room.transform.position;
                Vector3 barrackPos = new Vector3(roomBase.x - 30f, roomBase.y, roomBase.z - 35f);
                float floorY = roomBase.y; // 실내 바닥 기준

                NationType nation = NationTypeForStyle(nationStyle);
                int baseLevel = 8;
                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = new Vector3(barrackPos.x - (count - 1) * 0.6f + i * 1.2f, floorY, barrackPos.z + 2f);
                    var go = CreateGuard($"BarracksAlly_{i + 1}", pos, "막사 수비병",
                        baseLevel + (i % 3), nation, null, default, TerritoryDifficulty.Ring1);
                    if (go == null) continue;

                    // [36→37차] 내 소속 영지 실내 병사 = 움직이지 말고 **배럭 침대에 누워 휴식**.
                    //   배치는 공개 헬퍼(PlaceAllyOnBed) 경유 — 병사배치 UI의 개별 재배치와 동일 코드 경로.
                    Transform bed = room.transform.Find($"BarrackBed_{i + 1}");

                    var ph = go.GetComponent<GuardPlaceholder>();
                    bool bedPlaced = false;
                    if (bed != null)
                        bedPlaced = PlaceAllyOnBed(go, bed, i + 1);
                    if (!bedPlaced)
                    {
                        if (bed != null)
                            Debug.LogWarning($"[TerritoryBuilder] {bed.name} 배치 실패 — 탁자 옵 대기 배치로 폴백");
                        else
                            Debug.LogWarning($"[TerritoryBuilder] BarrackBed_{i + 1} 미발견 — 탁자 옵 대기 배치");
                        go.transform.position = new Vector3(pos.x, floorY + 0.05f, pos.z);
                        if (ph != null) ph.SetIndoorBedIndex(-1);
                    }

                    if (ph != null)
                    {
                        ph.SetRecruited(true);          // 아군 — 플레이어와 동맹
                        ph.HostileToPlayerFaction = false;
                        // [38차] 병사배치 UI 목록 등재 — 영지 미소속(실내) 아군으로 등록.
                        GuardManager.Instance?.RegisterStandalone(ph);
                    }
                    Debug.Log($"[TerritoryBuilder] 🪖 아군 막사 병사 배치: {go.name} (Lv.{baseLevel + (i % 3)})");
                }

                // [38차] "영지 소속 모든 병사는 침대에 누워있게" — 침대 미배치 아군(외부에서
                //   등록된 포섭 병사 포함)을 빈 침대에 순차 배치(BarrackBed_1..20, 1명/침대).
                AssignBedsToAllIndoorAllies(room);

                Debug.Log($"[TerritoryBuilder] 아군 막사 병사 {count}명 배치 완료 — 침대 휴식 (style: {nationStyle})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TerritoryBuilder] 아군 막사 병사 배치 실패: {e.Message}");
            }
        }

        /// <summary>국가 스타일 문자열 → NationType 매핑 (PlayerCastleInteriorBuilder와 동일 키).</summary>
        private static NationType NationTypeForStyle(string nationStyle)
        {
            switch (nationStyle?.ToLower())
            {
                case "eastern": return NationType.East;
                case "western": return NationType.West;
                case "southern": return NationType.South;
                case "northern": return NationType.North;
                case "empire": return NationType.Empire;
                default: return NationType.Empire;
            }
        }

        /// <summary>[36차] 침대 시각 bounds 실측 — 자식 렌더러(매트리스/프레임/베개) 캡슐화.
        ///   authored/스케일패스 좌표 어느 쪽이든 런타임 실측으로 정확한 상면/중심/깊이를 얻는다.</summary>
        private static Bounds GetBedBounds(Transform bed)
        {
            var rends = bed.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return default;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            return b;
        }

        /// <summary>
        /// [37차] 실내 아군 병사를 지정 침대에 눕힌다(병사배치 UI 개별 재배치용 공개 API).
        /// 눕기: FBX 피벗=발 기준 → X축 −90° 피치로 몸이 −Z(침대 깊이 방향)로 눕고, 발 피벗을
        /// 침대 발칪(z+)에, 몸 높이(y)는 매트리스 상면(bounds.max.y)+0.12 — 머리가 베개 쪽.
        /// 위치는 bounds 실측이라 authored/×2.45 스케일 어느 쪽이든 무관.
        /// 성공 시 guard.SetIndoorBedIndex(bedNumber).
        /// </summary>
        public static bool PlaceAllyOnBed(GameObject guardGo, Transform bed, int bedNumber)
        {
            if (guardGo == null || bed == null || bedNumber < 1) return false;
            Bounds bb = GetBedBounds(bed);
            if (bb.size.sqrMagnitude <= 0.0001f) return false;

            float bedTop = bb.max.y;
            Vector3 bedCenter = bb.center;
            float bedDepth = Mathf.Max(0.5f, bb.size.z);
            Vector3 anchor = new Vector3(
                bedCenter.x,
                bedTop + 0.12f,
                bedCenter.z + bedDepth * 0.5f - 0.25f);   // 발칪(탁자 반대 끝)에서 몸이 −Z로
            Quaternion pose = Quaternion.Euler(-90f, 0f, 0f);   // 눕기(머리 = −Z 쪽)
            guardGo.transform.position = anchor;
            guardGo.transform.rotation = pose;

            // [39차] 누움 강제 — 애니메이터/전투 AI가 루트를 덮어써 서서 끓는 문제(사용자 재보고)
            //   을 LateUpdate 재단언으로 근본 차단. 기상(WakeFromBed) 시 제거.
            var old = guardGo.GetComponent<BedRestEnforcer>();
            if (old != null) Object.Destroy(old);
            var enforcer = guardGo.AddComponent<BedRestEnforcer>();
            enforcer.Anchor = anchor;
            enforcer.Pose = pose;

            var ph = guardGo.GetComponent<GuardPlaceholder>();
            if (ph != null) ph.SetIndoorBedIndex(bedNumber);
            Debug.Log($"[TerritoryBuilder] 🛏️ 아군 병사 침대 휴식 배치: {guardGo.name} → {bed.name} (침대 {bedNumber})");
            return true;
        }

        /// <summary>
        /// [39차] 기상 — 침대 휴식 해제: 강제자 제거 + 서 있는 자세로 침대 옆 바닥에 세움.
        /// 역할 배치(출동/수비) 직전에 호출 — 이후 SetCommandTarget으로 배치지점까지 보행.
        /// </summary>
        public static void WakeFromBed(GuardPlaceholder guard)
        {
            if (guard == null) return;
            int bedNumber = guard.IndoorBedIndex;
            var enforcer = guard.GetComponent<BedRestEnforcer>();
            if (enforcer != null) Object.Destroy(enforcer);

            if (bedNumber > 0)
            {
                var bedGo = GameObject.Find($"BarrackBed_{bedNumber}");
                if (bedGo != null)
                {
                    Bounds bb = GetBedBounds(bedGo.transform);
                    Vector3 c = bb.size.sqrMagnitude > 0.0001f ? bb.center : bedGo.transform.position;
                    // 침대 루트 y = 바닥 — 서 있는 발 위치로 옆으로 내려선다
                    guard.transform.position = new Vector3(c.x + 0.9f, bedGo.transform.position.y + 0.05f, c.z);
                }
                guard.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 침대 쪽 보고 서기
                Debug.Log($"[TerritoryBuilder] 🪖 {guard.GuardName} 기상 (침대 {bedNumber} → 출동 준비)");
            }
            guard.SetIndoorBedIndex(-1);
        }

        /// <summary>[39차] 실내 기준점 — 로비 중앙(출구 방향)과 막사 앞(수비 근무지).
        ///   IndoorScene은 월드가 언로드된 별도 씬이므로 출동=로비(문 앞)까지 보행이
        ///   실내에서 확인 가능한 이동이고, 월드 복귀 시점의 문밖 배치는 배치 시스템 몫.</summary>
        public static bool TryGetIndoorPost(GuardPlaceholder guard, GuardTaskSystem.GuardTask task, out Vector3 post)
        {
            post = default;
            var room = GameObject.Find("Room");
            if (room == null) return false;
            Vector3 roomBase = room.transform.position;
            // 출동(공격/농사/채집/광물/수색/특사) = 로비 중앙(문 앞) — 수비 = 막사 앞 영지 안 근무지
            post = task == GuardTaskSystem.GuardTask.Defend
                ? roomBase + new Vector3(-15f, 0f, -20f)
                : roomBase + new Vector3(0f, 0f, 0f);
            return true;
        }

        /// <summary>[37차] 병사배치 UI용 — 씬에서 BarrackBed_n을 찾아 선택 병사를 눕힌다.</summary>
        public static bool TryPlaceAllyOnBed(GuardPlaceholder guard, int bedNumber)
        {
            if (guard == null || bedNumber < 1) return false;
            var bedGo = GameObject.Find($"BarrackBed_{bedNumber}");
            if (bedGo == null) return false;
            return PlaceAllyOnBed(guard.gameObject, bedGo.transform, bedNumber);
        }

        /// <summary>
        /// [38차] "영지 소속 모든 병사는 침대에 누워있게" — 실내의 침대 미배치 아군 전원을
        /// 빈 침대(BarrackBed_1..20)에 순차 배치. 이미 누운 병사(IndoorBedIndex>0)는 유지.
        /// GuardManager 등록 아군(standalone 포함)까지 훑어 외부 유입 포섭 병사도 커버.
        /// </summary>
        public static void AssignBedsToAllIndoorAllies(GameObject room)
        {
            if (room == null) return;

            // 사용 중 침대 수집
            var used = new HashSet<int>();
            var allies = new List<GuardPlaceholder>();
            var gm = GuardManager.Instance;
            if (gm != null)
            {
                foreach (var g in gm.GetAllPlayerGuards())
                    if (g != null && g.IsRecruited)
                        allies.Add(g);
            }

            foreach (var g in allies)
            {
                if (g.IndoorBedIndex > 0)
                {
                    used.Add(g.IndoorBedIndex);
                }
            }

            // 침대 오브젝트 프리캐시(1..20)
            var beds = new Dictionary<int, Transform>();
            for (int n = 1; n <= 20; n++)
            {
                var bed = room.transform.Find($"BarrackBed_{n}");
                if (bed != null) beds[n] = bed;
            }

            foreach (var g in allies)
            {
                if (g.IndoorBedIndex > 0) continue;   // 이미 누워 있음 — 유지
                for (int n = 1; n <= 20; n++)
                {
                    if (used.Contains(n)) continue;
                    if (!beds.TryGetValue(n, out var bed)) continue;
                    if (PlaceAllyOnBed(g.gameObject, bed, n))
                    {
                        used.Add(n);
                        break;
                    }
                }
            }
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
                // castle.glb는 잘못 저장된 파일로 삭제됨(사용자) → Empire/Dracula도 blue_castle 사용
                _ => "blue_castle"
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
        /// 성문(GateAnchor) 앞에 문지기를 배치합니다.
        /// 문지기 수는 영주 방어 성향 기반 — 방어적/소극적 영주는 문지기가 많아 게이트가 강하고,
        /// 공격적 영주는 문지기가 적어 게이트가 약하다 (Phase C: LordPersonalitySystem 배분 모델 통일, 클램프 1~5).
        /// (guardCount는 전체 주둔군 수 — 전쟁 페이즈에서 사용하도록 그대로 유지하며 여기서는 문지기만 배치)
        /// </summary>
        private void BuildGuardsAt(Transform parent, Vector3 center, TerritoryId territoryId, int guardCount, TerritoryDifficulty difficulty, NationType nation)
        {
            int baseLevel = GetBaseGuardLevel(difficulty);

            // 성문 방향 (Empire 중심 0,0,0 기준 바깥쪽)
            Vector3 c2 = new Vector3(center.x, 0, center.z);
            Vector3 gateDir = c2.sqrMagnitude < 0.0001f ? Vector3.back : Vector3.Normalize(c2);

            // 영주 성향(D) 기반 문지기 수 — 공격적 영주=적게, 방어적 영주=많게 (Phase A 배분 모델 통일)
            // 판정 불가(영지 미확인 등 gatekeeperSoldiers=0)여도 clamp 하한 1로 문지기 최소 보장
            int gatekeeperCount = Mathf.Clamp(
                LordPersonalitySystem.GetDeploymentPlan(territoryId).gatekeeperSoldiers, 1, 5);

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
                CreateGuard($"GateGuard_{i + 1}", p, GetGuardName(nation), level, nation, parent, gateDir, difficulty);
            }
        }

        /// <summary>난이도별 기본 병사 레벨 반환</summary>
        private static int GetBaseGuardLevel(TerritoryDifficulty difficulty)
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

        /// <summary>[09-28] 난이도별 장비 희귀도 보너스 (0~4) — GuardEquipmentSpawner 등급 승격용.</summary>
        private static int GearBonusForDifficulty(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => 0,
                TerritoryDifficulty.Ring2 => 1,
                TerritoryDifficulty.Ring3 => 2,
                TerritoryDifficulty.Ring4 => 3,
                TerritoryDifficulty.Empire => 4,
                _ => 0
            };
        }

        /// <summary>국가별 병사 이름 접두사</summary>
        private static string GetGuardName(NationType nation)
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

        /// <summary>[41차] 출동 병사 월드 스폰 — AttackSquadDeployment가 문밖 재스폰+역할 루틴 재부여.
        ///   CreateGuard 위임(높이/비주얼/장비 체인 동일) — 배치조 명령이 월드에서 실제 수행된다.</summary>
        public static GameObject SpawnDeployedGuard(string name, int level, NationType nation, Vector3 pos)
        {
            return CreateGuard(name, pos, "출동 병사", level, nation, null, default, TerritoryDifficulty.Ring1);
        }

        /// <summary>[39차] 병사 FBX 키 공개 — 병사배치 UI 3D 미리보기가 동일 비주얼 소스 사용.</summary>
        public static string GetSoldierFbxKeyForLevel(int level)
        {
            return SoldierFbxKeyForLevel(level);
        }

        /// <summary>
        /// 병사 Placeholder 생성 (GLB "soldier" 우선, 없으면 Primitive Capsule) - 부모 지정 가능
        /// </summary>
        /// <summary>
        /// 전쟁 시 주둔군 스폰 API — def.guardCount명 병사를 center 주변 반경 10~20m 원형으로 배치.
        /// 평시에는 문지기만 스폰되며, 이 API는 전쟁 시스템(TerritoryWarManager 등)이 호출할 목적으로 제공.
        /// </summary>
        public static System.Collections.Generic.List<GameObject> SpawnGarrison(TerritoryDefinition? def, Vector3 center)
        {
            var spawned = new System.Collections.Generic.List<GameObject>();
            if (def == null) return spawned;
            var rng = new System.Random(def.Value.id.index + 7777); // 영지 인덱스 기반 고정 시드 (결정론)
            int count = Mathf.Max(1, def.Value.guardCount);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + (float)(rng.NextDouble() * 0.4 - 0.2);
                float radius = 10f + (float)rng.NextDouble() * 10f;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var soldier = CreateGuard($"Garrison_{def.Value.nation}_{def.Value.id.index}_{i + 1}", pos,
                    GetGuardName(def.Value.nation), GetBaseGuardLevel(def.Value.difficulty) + (i % 3), def.Value.nation, null, default, def.Value.difficulty);
                if (soldier != null) spawned.Add(soldier);
            }
            Debug.Log($"[TerritoryBuilder] 주둔군 스폰: {def.Value.territoryName} {count}명");
            return spawned;
        }

        /// <summary>병사 클래스 랜덤 (레벨 무관): 40% 방패 / 30% 대검 / 30% 궁수</summary>
        private static string ClassRoll()
        {
            double r = UnityEngine.Random.value;
            if (r < 0.4) return "Shield";
            if (r < 0.7) return "GreatSword";
            return "Archer";
        }

        /// <summary>레벨 구간별 Humanoid FBX Resources 경로</summary>
        private static string SoldierFbxKeyForLevel(int level)
        {
            if (level <= 20) return "Models/UserProvided/fbx/soldier_lv1-20_rigged";
            if (level <= 40) return "Models/UserProvided/fbx/soldier_lv20-40_rigged";
            return "Models/UserProvided/fbx/soldier_lv40-50_rigged";
        }

        private static GameObject CreateGuard(string name, Vector3 position, string guardName, int level, NationType nation, Transform parent = null, Vector3 forward = default, TerritoryDifficulty difficulty = TerritoryDifficulty.Ring1)
        {
            // ── 병사 클래스 랜덤 배치 (레벨 무관): 40% 방패 / 30% 대검 / 30% 궁수 ──
            string classKey = ClassRoll();

            // ── 레벨 구간별 Humanoid FBX 비주얼 (GLB "soldier" 대체) ──
            string fbxKey = SoldierFbxKeyForLevel(level);
            var modelPrefab = Resources.Load<GameObject>(fbxKey);

            GameObject go;
            if (modelPrefab == null)
            {
                // FBX 로드 실패 시 기존 GLB/Primitive Capsule 폴백 유지
                go = TrySpawnModelOrPlaceholder("soldier", name, position,
                    Vector3.one, new Color(0.2f, 0.4f, 0.8f), PrimitiveType.Capsule);
            }
            else
            {
                go = Object.Instantiate(modelPrefab);
                go.name = name;
                // 피벗은 바닥 기준 가정 → 약간 띄워 지면 겹침 방지
                float groundY = TerrainGenerator.GetHeightAt(position.x, position.z, BiomeType.Plains, 42) + 1f;
                go.transform.position = new Vector3(position.x, groundY + 0.05f, position.z);
                go.transform.localScale = Vector3.one;
                Debug.Log($"[TerritoryBuilder] Humanoid FBX '{fbxKey}'로 '{name}' 생성 (클래스: {classKey}, Lv.{level})");
            }

            // 성문/진영 방향 정면 설정 (바깥을 향함). 기본값이 아니면 적용
            if (forward != Vector3.zero)
                go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            if (parent != null)
                go.transform.SetParent(parent);

            var placeholder = go.AddComponent<GuardPlaceholder>();
            // [09-28] 영지 난이도 기반 장비 희귀도 보너스 — GuardEquipmentSpawner가 등급 승격에 사용.
            placeholder.GearDifficultyBonus = GearBonusForDifficulty(difficulty);
            // ── Animator + 믹사모 컨트롤러 + 드라이버 (병사 모드) ──
            if (modelPrefab != null)
            {
                var anim = go.GetComponent<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                anim.runtimeAnimatorController =
                    Resources.Load<RuntimeAnimatorController>($"Animation/Controllers/Soldier{classKey}_AC");
                // 루트 모션 비활성 (병사 이동도 위치 델타/커맨드가 제어)
                anim.applyRootMotion = false;
                var driver = go.AddComponent<HumanoidClipDriver>();
                driver.mode = HumanoidClipDriver.DriveMode.Soldier;
                var glbKey = fbxKey.Replace("/fbx/", "/");
                HumanoidClipDriver.CopyMaterialsFromGlb(go, glbKey);
            }
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

            return go;
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