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
        /// [125차 후속7] 문 트리거만 있던 기존 구성에 외관(임시 경기장)을 추가 배치 —
        ///   월드맵 아레나 핀(WorldMapWindowUTK)과 좌표 산식을 GetArenaWorldPosition으로 공유(단일 진실원).
        /// </summary>
        public void BuildArenaRegions()
        {
            BuildArenaRegion(NationType.East,  "Eastern",  0f);
            BuildArenaRegion(NationType.North, "Northern", 90f);
            BuildArenaRegion(NationType.West,  "Western",  180f);
            BuildArenaRegion(NationType.South, "Southern", 270f);
            Debug.Log("[125차 후속7][TerritoryBuilder] 4방위국 아레나 4곳 배치 완료 (외관 임시 건물 + 문 트리거)");
        }

        /// <summary>국가 방향 각도(도)와 링 중간 거리로 아레나 1곳 생성.</summary>
        private void BuildArenaRegion(NationType nation, string nationStyle, float angleDeg)
        {
            string rootName = $"Arena_{nationStyle}";
            if (GameObject.Find(rootName) != null) return; // 중복 방지

            // [125차 후속7] 링1(1450)과 링2(1000) 사이 중간 거리 → 영지 밀집과 분리된 국가 영역 내 지점.
            //   좌표 산식은 GetArenaWorldPosition(공용 상수 ArenaRingDistance)로 단일화 — 월드맵 아레나 핀과 동일 좌표 보장.
            Vector3 pos = GetArenaWorldPosition(nation);

            var root = new GameObject(rootName);
            root.transform.position = pos;

            // 지형 높이 보정 (아레나 바닥이 지면에 닿도록)
            float groundY = TerrainGenerator.GetHeightAt(pos.x, pos.z, BiomeType.Plains, 42) + 1f;
            root.transform.position = new Vector3(pos.x, groundY, pos.z);

            // 아레나 출입문(BuildingTrigger) — E키로 실내 진입. 문은 루트 중심 남쪽(World 방향은 일괄 +Z)에 배치.
            //   [125차 후속7] 기존 검증본 트리거 — 위치/동작 무수정.
            Vector3 doorPos = new Vector3(pos.x, groundY, pos.z + 6f);
            IndoorTransitionSetup.CreateArenaTrigger(doorPos, nationStyle, root.transform);

            // [125차 후속7] 아레나 외관(임시 건물) — 사장님 GLB 제공 전까지 프리미티브 임시 경기장 배치.
            //   기존 문 트리거는 건드리지 않는다(문은 링 반경 20m 내부 남측 +6m — 개구부 방향과 정렬).
            BuildArenaExteriorPlaceholder(root.transform, nation, nationStyle, groundY);

            Debug.Log($"[TerritoryBuilder] 아레나 배치: {rootName} @ {pos} (angle {angleDeg}°, style {nationStyle})");
        }

        // ===== [125차 후속7] 4방위국 아레나 외관(임시 건물) + 월드맵 핀 공용 좌표 =====

        /// <summary>[125차 후속7] 아레나 링 거리(m) — 링1(1450)/링2(1000) 중간. 월드맵 핀과 공용 상수.</summary>
        public const float ArenaRingDistance = 1150f;

        /// <summary>[125차 후속7] 국가 → 아레나 방위 각도(도). 동 0°(+X) / 북 90°(+Z) / 서 180°(−X) / 남 270°(−Z).</summary>
        public static float GetArenaAngleDeg(NationType nation)
        {
            switch (nation)
            {
                case NationType.East: return 0f;
                case NationType.North: return 90f;
                case NationType.West: return 180f;
                case NationType.South: return 270f;
                default: return 0f;
            }
        }

        /// <summary>
        /// [125차 후속7] 국가 아레나 월드 좌표(XZ 평면, y=0) — 메인씬 배치(BuildArenaRegion)와
        ///   월드맵 아레나 핀(WorldMapWindowUTK)이 동일 산식을 공유하는 단일 진실원.
        /// </summary>
        public static Vector3 GetArenaWorldPosition(NationType nation)
        {
            float rad = GetArenaAngleDeg(nation) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * ArenaRingDistance, 0f, Mathf.Sin(rad) * ArenaRingDistance);
        }

        /// <summary>
        /// [125차 후속7] 아레나 외관(임시 건물) — '사장님 GLB 제공 전까지 임시 건물' 선례(BuildingPlaceholder)에 따라
        ///   arena 루트 자식으로 임시 경기장을 배치한다. GLB 규약
        ///   (Assets/Resources/Models/UserProvided/arena_exterior_&lt;nation&gt;.glb — 실내 arena_&lt;nation&gt;.glb와 구별 접두사)
        ///   이 존재하면 우선 사용, 없으면 프리미티브 폴백(모래 바닥 + 국가색 외벽 링 + 남측 출입문 개구부 + 깃대 2본).
        ///   문 트리거(CreateArenaTrigger, 중심+6m)는 수정하지 않는다 — 개구부가 문 방향(+Z)과 정렬될 뿐.
        /// </summary>
        private void BuildArenaExteriorPlaceholder(Transform parent, NationType nation, string nationStyle, float groundY)
        {
            var exterior = new GameObject($"ArenaExterior_{nationStyle}");
            exterior.transform.SetParent(parent, false);
            exterior.transform.localPosition = Vector3.zero;

            // GLB 우선(외관 전용 규약 키) — 없으면 프리미티브 임시 경기장 폴백
            string glbKey = $"arena_exterior_{NationGlbKey(nation)}";
            if (RuntimeModelLoader.TryGetModel(glbKey, out var modelPrefab) && modelPrefab != null)
            {
                var inst = Object.Instantiate(modelPrefab, exterior.transform, false);
                inst.name = "ArenaExteriorModel";
                inst.transform.localPosition = new Vector3(0f, 0.05f, 0f); // GLB 피벗 바닥 기준 가정 → 지면 겹침 방지 소량 부양
                NormalizeArenaExteriorScale(inst, 20f);                     // XZ 최대 반경 → 20m 정규화(성 25m 정규화 동일 기법)
                Debug.Log($"[125차 후속7][TerritoryBuilder] {parent.name} 외관 GLB '{glbKey}' 사용 — 반경 {GetCastleRadius(inst):F1}m");
            }
            else
            {
                BuildArenaExteriorPrimitives(exterior.transform, nation);
                Debug.Log($"[125차 후속7][TerritoryBuilder] {parent.name} 외관 GLB '{glbKey}' 없음 — 프리미티브 임시 경기장 폴백 (arena_exterior_{NationGlbKey(nation)}.glb 추가 시 자동 전환)");
            }

            // BuildingPlaceholder 부착 — 임시 건물 등록 선례(상점/크래프트하우스 등)와 동일. GLB 교체 시에도 컴포넌트 유지.
            var ph = exterior.AddComponent<BuildingPlaceholder>();
            ph.buildingType = BuildingPlaceholder.BuildingType.Arena;
            ph.buildingName = $"{GetNationKoreanName(nation)} 아레나";
        }

        /// <summary>
        /// [125차 후속7] 아레나 외관 프리미티브 폴백 — 모래 바닥 원판 + 외벽 링 16분할(남측 +Z 출입문 1분할 개구부) + 깃대 2본.
        ///   바닥은 콜라이더 제거(지형 보행 유지 — 계단 단차 방지, 시각 플레이트만),
        ///   외벽/깃대는 충돌 유지(외벽은 실물 장애물). 모든 y는 루트 기저(월드 지표면 = GetHeightAt + 1f) 기준.
        /// </summary>
        private static void BuildArenaExteriorPrimitives(Transform parent, NationType nation)
        {
            const float ringRadius = 20f;   // 외벽 링 반경(m) — 문 트리거(중심+6m)가 링 내부에 위치
            const int wallSegments = 16;    // 외벽 분할 수 (1분할 ≈ 7.85m 호)

            // 모래 바닥 — Cylinder 반경 20m. 콜라이더 제거(지형 보행 그대로).
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "ArenaFloor";
            floor.tag = "Untagged";
            floor.transform.SetParent(parent, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(ringRadius * 2f, 0.4f, ringRadius * 2f);
            var floorCol = floor.GetComponent<Collider>();
            if (floorCol != null) Object.Destroy(floorCol);
            var floorRend = floor.GetComponent<MeshRenderer>();
            if (floorRend != null)
                floorRend.material = MaterialHelper.CreateLitMaterial(new Color(0.78f, 0.70f, 0.52f), "ArenaFloor_Mat");

            // 외벽 링 — Cube 16분할, +Z(방위각 90°) 1분할 개구부가 문 트리거 방향과 정렬.
            Color castleColor = GetCastleFallbackColor(nation);
            Color wallColor = new Color(castleColor.r * 0.55f, castleColor.g * 0.55f, castleColor.b * 0.55f, 1f); // 국가색 암배합(석벽 톤)
            for (int i = 0; i < wallSegments; i++)
            {
                float angle = i * 360f / wallSegments; // 방위각(도) — 90°가 +Z(북국 배치 관례와 동일 좌표계)
                if (Mathf.Abs(Mathf.DeltaAngle(angle, 90f)) < 1f) continue; // +Z 출입문 개구부(문 트리거 정렬)

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"ArenaWall_{i:00}";
                wall.tag = "Untagged";
                wall.transform.SetParent(parent, false);
                float rad = angle * Mathf.Deg2Rad;
                wall.transform.localPosition = new Vector3(Mathf.Cos(rad) * ringRadius, 3f, Mathf.Sin(rad) * ringRadius);
                // 접선 정렬 yaw = -(방위각 + 90°) — Cube 로컬 +X를 원주 접선 방향에 정렬(θ=90°에서 -180° 검증 완료)
                wall.transform.localRotation = Quaternion.Euler(0f, -(angle + 90f), 0f);
                wall.transform.localScale = new Vector3(7.6f, 6f, 2.2f); // 접선 길이 × 높이 × 두께
                var wallRend = wall.GetComponent<MeshRenderer>();
                if (wallRend != null)
                    wallRend.material = MaterialHelper.CreateLitMaterial(wallColor, $"ArenaWall_{i:00}_Mat");
            }

            // 깃대 2본 + 국가색 깃발 — 출입문(90°) 좌우 ±13.5° 외벽선 위. 콜라이더 제거(장식물 — 통행 방해 없음).
            Color poleColor = new Color(0.45f, 0.42f, 0.40f, 1f); // 석재 회색
            for (int side = 0; side < 2; side++)
            {
                float flagAngle = 90f + (side == 0 ? -13.5f : 13.5f);
                float rad = flagAngle * Mathf.Deg2Rad;

                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = $"ArenaFlagPole_{side}";
                pole.tag = "Untagged";
                pole.transform.SetParent(parent, false);
                pole.transform.localPosition = new Vector3(Mathf.Cos(rad) * ringRadius, 3f, Mathf.Sin(rad) * ringRadius);
                pole.transform.localScale = new Vector3(0.4f, 3f, 0.4f); // Cylinder 기본 높이 2 × 3 = 6m
                var poleCol = pole.GetComponent<Collider>();
                if (poleCol != null) Object.Destroy(poleCol);
                var poleRend = pole.GetComponent<MeshRenderer>();
                if (poleRend != null)
                    poleRend.material = MaterialHelper.CreateLitMaterial(poleColor, $"ArenaFlagPole_{side}_Mat");

                var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flag.name = $"ArenaFlag_{side}";
                flag.tag = "Untagged";
                flag.transform.SetParent(pole.transform, false);
                flag.transform.localPosition = new Vector3(0.9f, 2.3f, 0f); // 깃대 상단 측면
                flag.transform.localScale = new Vector3(1.8f, 1.2f, 0.12f);
                var flagCol = flag.GetComponent<Collider>();
                if (flagCol != null) Object.Destroy(flagCol);
                var flagRend = flag.GetComponent<MeshRenderer>();
                if (flagRend != null)
                    flagRend.material = MaterialHelper.CreateLitMaterial(castleColor, $"ArenaFlag_{side}_Mat");
            }
        }

        /// <summary>[125차 후속7] NationType → 외관 GLB 키 접미사(east/west/south/north) — 실내 arena_&lt;nation&gt;.glb 규약과 동일 국가 접미사.</summary>
        private static string NationGlbKey(NationType nation)
        {
            switch (nation)
            {
                case NationType.East: return "east";
                case NationType.West: return "west";
                case NationType.South: return "south";
                case NationType.North: return "north";
                default: return "empire";
            }
        }

        /// <summary>[125차 후속7] NationType → 한국어 국가명(임시 건물 표기/로그용).</summary>
        private static string GetNationKoreanName(NationType nation)
        {
            switch (nation)
            {
                case NationType.East: return "동국";
                case NationType.West: return "서국";
                case NationType.South: return "남국";
                case NationType.North: return "북국";
                default: return "제국";
            }
        }

        /// <summary>[125차 후속7] 외관 GLB XZ 최대 반경을 targetRadius로 균일 스케일 정규화(성 NormalizeCastleScale 동일 기법).</summary>
        private static void NormalizeArenaExteriorScale(GameObject exteriorModel, float targetRadius)
        {
            float sourceRadius = GetCastleRadius(exteriorModel);
            if (sourceRadius <= 0.01f) return;
            float factor = targetRadius / sourceRadius;
            exteriorModel.transform.localScale *= factor;
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

        // ===== [F4] 야외 크래프트 스테이션 — 점령 영지 성 앞 즉석 제작 =====

        /// <summary>영지별 야외 크래프트 스테이션 레지스트리 (key = territoryId.ToString()). 멱등 가드용.</summary>
        private static readonly Dictionary<string, GameObject> _outdoorCraftStations =
            new Dictionary<string, GameObject>();

        /// <summary>
        /// [F4] 야외 크래프트 박스 스폰 — 점령 완료 시 영지 중심 근처(성 앞)에 즉석 제작 스테이션 배치.
        /// TerritoryCraftingStation(ProjectName.UI)을 리플렉션 부착(asmdef 순환 회피 —
        /// PlayerCastleInteriorBuilder.AttachUiComponent 선례). 멱등 — 이미 있으면 기존 객체 반환.
        /// </summary>
        /// <param name="nearCenter">영지 중심 근처 기준 위치 (호출자가 오프셋 적용)</param>
        /// <param name="territoryKey">영지 키 (예: "East_01") — 스테이션 귀속</param>
        /// <returns>스테이션 GameObject (실패 시 null)</returns>
        public static GameObject SpawnOutdoorCraftStation(Vector3 nearCenter, string territoryKey)
        {
            if (string.IsNullOrEmpty(territoryKey))
            {
                Debug.LogWarning("[TerritoryBuilder] 야외 크래프트 스폰 실패 — territoryKey 없음");
                return null;
            }

            // 멱등 가드 — 이미 살아있으면 재스폰 없이 반환
            if (_outdoorCraftStations.TryGetValue(territoryKey, out var existing) && existing != null)
                return existing;
            _outdoorCraftStations.Remove(territoryKey);

            // 성 앞 슬롯 — 크래프트하우스 GLB(선례 "craft_blend"), 없으면 초록 큐브 Placeholder
            GameObject station = TrySpawnModelOrPlaceholder("craft_blend", $"Outdoor_CraftStation_{territoryKey}",
                nearCenter, new Vector3(1.6f, 1.4f, 1.6f), new Color(0.6f, 0.8f, 0.2f), PrimitiveType.Cube);
            if (station == null)
            {
                Debug.LogWarning($"[TerritoryBuilder] 야외 크래프트 스테이션 생성 실패: {territoryKey}");
                return null;
            }

            // TerritoryCraftingStation 리플렉션 부착 + Configure(territoryKey, 표시명)
            const string stationTypeName = "ProjectName.UI.TerritoryCraftingStation, ProjectName.UI";
            System.Type uiType = System.Type.GetType(stationTypeName);
            if (uiType == null)
            {
                Debug.LogWarning("[TerritoryBuilder] TerritoryCraftingStation 타입 미발견 — 상호작용 없는 박스만 배치");
                _outdoorCraftStations[territoryKey] = station;
                return station;
            }

            var component = station.AddComponent(uiType);
            var configure = uiType.GetMethod("Configure");
            if (component != null && configure != null)
            {
                try
                {
                    // Configure(territoryId, stationName, interactRange) — 3인자 전체 전달(Invoke는 기본값 미보충)
                    configure.Invoke(component, new object[] { territoryKey, "영지 작업대 (야외)", null });
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[TerritoryBuilder] 스테이션 Configure 실패: {e.Message}");
                }
            }

            _outdoorCraftStations[territoryKey] = station;
            Debug.Log($"[TerritoryBuilder] 🛠️ 야외 크래프트 스테이션 배치: {territoryKey} at {station.transform.position}");
            return station;
        }

        /// <summary>[F4] 야외 크래프트 스테이션 제거 — 영지 상실(배신/AI 탈환) 시. 없으면 무시(멱등).</summary>
        public static void RemoveOutdoorCraftStation(string territoryKey)
        {
            if (string.IsNullOrEmpty(territoryKey)) return;
            if (!_outdoorCraftStations.Remove(territoryKey, out var station)) return;
            if (station != null)
            {
                // [QA 수리] 편집 모드 Destroy 금지 에러 방지
                if (Application.isPlaying) Object.Destroy(station);
                else Object.DestroyImmediate(station);
            }
            Debug.Log($"[TerritoryBuilder] 야외 크래프트 스테이션 회수: {territoryKey}");
        }

        /// <summary>실내 수비병 soldier-index 오프셋 — 같은 영지의 실외 문지기/주둔군 인덱스(0..)와 레벨 스트림을 분리.</summary>
        private const int INTERIOR_SOLDIER_INDEX_BASE = 1000;

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

                // 영지 난이도 프로필(국가+Ring 범위 · 영지별 Tier) — 실내 수비병 레벨 = 안정적 soldier-index 기반.
                // 실외 문지기/주둔군 인덱스(0..)와 스트림이 겹치지 않도록 실내는 INTERIOR_SOLDIER_INDEX_BASE 오프셋.
                var profile = TerritoryDifficultyProfileSystem.Resolve(def.id.index, def.id.nation, def.difficulty);
                for (int i = 0; i < count; i++)
                {
                    float lat = ((i % 2) == 0 ? 1f : -1f) * (2f + (i / 2) * 2f); // ±2, ∓2, ±4, ∓4 ...
                    float dep = 2f + (i / 2) * 3f; // 왕좌 앞쪽으로 깊이 배치
                    Vector3 pos = new Vector3(roomCenter.x + lat, roomCenter.y, roomCenter.z + dep);
                    int level = profile.SelectGuardLevel(INTERIOR_SOLDIER_INDEX_BASE + i);
                    var go = CreateGuard($"InteriorGuard_{i + 1}", pos, GetGuardName(def.nation), level, def.nation, null, default, def.difficulty);
                    if (go == null) continue;

                    // 실내 바닥 y 고정(월드 지형 높이 오염 방지 — CreateGuard는 GetHeightAt 보정함)
                    go.transform.position = new Vector3(pos.x, roomCenter.y + 0.05f, pos.z);

                    var ph = go.GetComponent<GuardPlaceholder>();
                    if (ph != null)
                    {
                        ph.SetInCombat(true);
                        ph.HostileToPlayerFaction = true; // 실내 진입자 플레이어 공격 대상(실외 영지 기반 GuardHostilitySystem은 실내 미적용)
                    }
                    Debug.Log($"[TerritoryBuilder] 영주 성 실내 수비 병사 배치: {go.name} (Lv.{level})");
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

                // [43차] 침대 휴식 자가수리 스윕 — 실내 진입 3s 후 검증 + 이후 지속(누움 계약 보장).
                BedRestSelfRepair.Ensure(room);

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
                    // [46차] x 오프셋 0.9 → 0.7 — 침대 열 간격 1.4m에서 0.9는 옆침대(반폭 0.6, 시작 +0.8)
                    //   영역에 반쯤 서는 것 → 기상 직후 옆침대와 겹침. 0.7 = 두 침대 사이 통로.
                    guard.transform.position = new Vector3(c.x + 0.7f, bedGo.transform.position.y + 0.05f, c.z);
                }
                guard.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // 침대 쪽 보고 서기
                Debug.Log($"[TerritoryBuilder] 🪖 {guard.GuardName} 기상 (침대 {bedNumber} → 출동 준비)");
            }
            guard.SetIndoorBedIndex(-1);
        }

        /// <summary>[39차] 실내 기준점 — 로비 중앙(출구 방향)과 막사 앞(수비 근무지).
        ///   IndoorScene은 월드가 언로드된 별도 씬이므로 출동=로비(문 앞)까지 보행이
        ///   실내에서 확인 가능한 이동이고, 월드 복귀 시점의 문밖 출동/게이트 복귀는 TerritoryDeploymentSystem(gateFront/영지 중앙)과 연결.
        ///   [45차 근원 수리 — "배치 병사가 기상 후 서 있기만"] 플레이어 성 실내 루트 이름은
        ///   "PlayerCastleRoom"(빌더 362행)인데 GameObject.Find("Room")만 찾아 목적지 실패 →
        ///   보행 명령이 아예 안 나갔다. 양쪽 이름 모두 지원(구 빌더 호환).</summary>
        public static bool TryGetIndoorPost(GuardPlaceholder guard, GuardTaskSystem.GuardTask task, out Vector3 post)
        {
            post = default;
            var room = GameObject.Find("Room");
            if (room == null) room = GameObject.Find("PlayerCastleRoom");   // [45차] 실제 루트 이름
            if (room == null) return false;
            Vector3 roomBase = room.transform.position;
            // [49차 사용자 요구 — "한명은 끝까지 남아있는" 수리] Defend만 막사 앞(-15,-20)이었는데
            //   침대에서 이미 가까워(≈5m) 화면상 '안 움직임'으로 보였다. 요구 = "모두 중앙 집결" —
            //   역할 무관 로비 중앙(문 앞)으로 통일(월드 루틴은 퇴출 시 역할별 수행 — 실내 목적지만 통일).
            post = roomBase + new Vector3(0f, 0f, 0f);
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
                    if (g != null && g.IsRecruited && IsIndoorResident(g))
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
            int bedsFound = 0;
            for (int n = 1; n <= 20; n++)
            {
                var bed = room.transform.Find($"BarrackBed_{n}");
                if (bed != null) { beds[n] = bed; bedsFound++; }
            }

            // [43차 진단] 침대 0개 발견 = 스윕 무동작의 즉시 원인 — 로그로 노출(조용한 실패 차단).
            if (bedsFound == 0)
            {
                Debug.LogWarning($"[TerritoryBuilder][침대진단] BarrackBed_* 0개 발견 — room 자식에서 침대 스캔 폴백 시도");
                for (int n = 1; n <= 20; n++)
                {
                    var bed = GameObject.Find($"BarrackBed_{n}");
                    if (bed != null) { beds[n] = bed.transform; bedsFound++; }
                }
                if (bedsFound == 0)
                {
                    Debug.LogWarning("[TerritoryBuilder][침대진단] 전역 스캔에도 0개 — 배럭 침대가 이 씬에 없음. 빌더 확인 필요");
                }
            }

            int placed = 0;
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
                        placed++;
                        break;
                    }
                }
            }
            // [43차 진단] 스윕 결과 1회 요약 — "누움 자동화 작동 여부"를 로그로 즉명.
            Debug.Log($"[TerritoryBuilder][침대진단] 스윕 완료 — 아군 {allies.Count}명 중 재취침 {placed}명, 침대 발견 {bedsFound}개");
        }

        /// <summary>[43차] 이 병사가 실내 침대 휴식 대상인가 — 출동 예약(역할 부여) 병사는 제외.</summary>
        public static bool IsIndoorResident(GuardPlaceholder g)
        {
            if (g == null) return false;
            var gts = GuardTaskSystem.Instance;
            var task = gts != null ? gts.GetTask(g) : GuardTaskSystem.GuardTask.None;
            return task == GuardTaskSystem.GuardTask.None;
        }

        /// <summary>
        /// [43차] 침대 휴식 자가수리 스윕 — 실내 진입 3s 후 1회 + 이후 5s 간격 지속.
        ///   누워 있지 않은(침대 미배치·기상 상태) 실내 아군을 자동 재취침시킨다.
        ///   배치 UI의 의도 기상(IndoorMoveExempt) 병사는 제외 — "배치되면 움직일 수 있게" 보장.
        ///   BedRestEnforcer 부재 = 누움 무효(누워 있다 해도 애니/AI가 덮어쓸 수 있음)로 간주해 재배치.
        /// </summary>
        private sealed class BedRestSelfRepair : MonoBehaviour
        {
            private GameObject _room;
            private float _nextSweep;
            private int _sweepCount;

            public static void Ensure(GameObject room)
            {
                if (room == null) return;
                var existing = room.GetComponent<BedRestSelfRepair>();
                if (existing == null)
                    existing = room.AddComponent<BedRestSelfRepair>();
                existing._room = room;
                existing._nextSweep = Time.time + 3f;   // 스윕·초기 배치 안착 후 1차 검증
                Debug.Log("[TerritoryBuilder][침대진단] 자가수리 스윕 배선 — 3s 후 1차 검증, 이후 5s 간격");
            }

            private void Update()
            {
                // [44차] 플래그 게이트 제거 — 이 컴포넌트는 실내 Room 오브젝트에만 부착된다
                //   (SpawnInteriorAlliedGuards 경로 보장). 전용 실내 테스트 씬(Test_PlayerCastleInterior)
                //   처럼 IndoorActive를 세팅하지 않는 컨텍스트에서도 스윕이 누움 계약을 지킨다.
                if (Time.time < _nextSweep) return;
                _nextSweep = Time.time + 5f;
                _sweepCount++;

                int repaired = 0;
                var gm = GuardManager.Instance;
                if (gm == null) return;
                foreach (var g in gm.GetAllPlayerGuards())
                {
                    if (g == null || !g.IsRecruited) continue;
                    if (!IsIndoorResident(g)) continue;                    // 출동 예약/배지 병사 제외
                    if (g.IndoorBedIndex > 0 && g.GetComponent<BedRestEnforcer>() != null) continue;   // 정상 누움

                    // 누워 있지 않거나 강제자가 없음 → 재취침
                    var bedGo = g.IndoorBedIndex > 0
                        ? GameObject.Find($"BarrackBed_{g.IndoorBedIndex}")
                        : null;
                    bool done = false;
                    if (bedGo != null)
                        done = PlaceAllyOnBed(g.gameObject, bedGo.transform, g.IndoorBedIndex);
                    if (!done)
                    {
                        // 침대 정보 유실 — 스윕으로 재배치(빈 침대 아무 데나)
                        AssignBedsToAllIndoorAllies(_room);
                        done = g.IndoorBedIndex > 0 && g.GetComponent<BedRestEnforcer>() != null;
                    }
                    if (done) repaired++;
                }
                if (repaired > 0)
                    Debug.Log($"[TerritoryBuilder][침대진단] 자가수리 #{_sweepCount} — 재취침 {repaired}명");
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
            // 영지 난이도 프로필 — 문지기 레벨은 영지별 tier + 안정적 soldier-index(0..N-1)로 결정
            var profile = TerritoryDifficultyProfileSystem.Resolve(territoryId.index, territoryId.nation, difficulty);

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
                int level = profile.SelectGuardLevel(i);  // 프로필 레벨 (soldier-index 기반, 결정론)
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
            => SpawnGarrisonCapped(def, center, int.MaxValue);

        /// <summary>
        /// 주둔군 스폰 — 최대 maxSpawn명만 실제 생성한다(영지 guardCount는 배치 원형 간격 계산에만 사용).
        /// 같은 영지/같은 인덱스의 병사는 maxSpawn과 무관하게 동일 위치·레벨을 가진다(결정론).
        /// </summary>
        public static System.Collections.Generic.List<GameObject> SpawnGarrisonCapped(TerritoryDefinition? def, Vector3 center, int maxSpawn)
        {
            var spawned = new System.Collections.Generic.List<GameObject>();
            if (def == null || maxSpawn <= 0) return spawned;
            var rng = new System.Random(def.Value.id.index + 7777); // 영지 인덱스 기반 고정 시드 (결정론)
            int count = Mathf.Max(1, def.Value.guardCount);
            int toSpawn = Mathf.Min(count, maxSpawn);
            var profile = TerritoryDifficultyProfileSystem.Resolve(def.Value.id.index, def.Value.id.nation, def.Value.difficulty);
            for (int i = 0; i < toSpawn; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + (float)(rng.NextDouble() * 0.4 - 0.2);
                float radius = 10f + (float)rng.NextDouble() * 10f;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                int level = profile.SelectGuardLevel(i);
                var soldier = CreateGuard($"Garrison_{def.Value.nation}_{def.Value.id.index}_{i + 1}", pos,
                    GetGuardName(def.Value.nation), level, def.Value.nation, null, default, def.Value.difficulty);
                if (soldier != null) spawned.Add(soldier);
            }
            Debug.Log($"[TerritoryBuilder] 주둔군 스폰: {def.Value.territoryName} {toSpawn}명 (정의 {count}명 중)");
            return spawned;
        }

        /// <summary>
        /// AI 고용 병사 로스터 복원 전용: 영지 중심(def.worldPosition) 주변의 결정론 슬롯 위치에
        /// 기존 CreateGuard 경로로 병사를 만들고, 저장 DTO(정체성/레벨/스탯)를 적용한다.
        /// 전체 주둔군은 재생성하지 않는다. 생성 실패 시 null.
        /// </summary>
        public static GuardPlaceholder SpawnAIHiredGuard(TerritoryDefinition def, int slotIndex, AIHiredGuardSaveEntry entry)
        {
            if (entry == null) return null;

            int slot = Mathf.Max(0, slotIndex);
            float angle = slot * 2.39996323f;                 // 황금각 — 슬롯마다 고르게 분산 (결정론)
            float radius = 10f + (slot % 5) * 2f;
            Vector3 pos = def.worldPosition + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            string goName = $"AIHire_{def.id}_{slot + 1}";
            int level = Mathf.Max(1, entry.level);
            string guardName = string.IsNullOrEmpty(entry.guardName) ? GetGuardName(def.nation) : entry.guardName;

            GameObject go = CreateGuard(goName, pos, guardName, level, def.nation, null, default, def.difficulty);
            if (go == null) return null;

            var guard = go.GetComponent<GuardPlaceholder>();
            if (guard == null) return null;
            guard.ApplyPersistentRosterEntry(entry);   // 리롤 없이 정체성/스탯/HP 복원
            return guard;
        }

        /// <summary>병사 클래스 랜덤 (레벨 무관): 40% 방패 / 30% 대검 / 30% 궁수</summary>
        private static string ClassRoll()
        {
            double r = UnityEngine.Random.value;
            if (r < 0.4) return "Shield";
            if (r < 0.7) return "GreatSword";
            return "Archer";
        }

        /// <summary>[51차] 표준 인간 키 — 병사+플레이어 전 씬 통일 목표(사용자 확정: 47차의 1.0m는 폐기,
        ///   원래 크기인 1.8m로 모두 통일). 모든 정규화 호출부가 이 상수를 단일 사용.</summary>
        public const float SoldierTargetHeight = 1.8f;

        /// <summary>[47차→51차] 병사 높이를 목표치(SoldierTargetHeight=1.8m)로 등비 정규화 — 렌더러 bounds 실측 기반.
        ///   스케일 경계: 피벗=발 가정 유지(발이 원점, 위로 성장 — 스케일 후 bounds.min.y가
        ///   스케일된 만큼 하강하지 않는 피벗 구조지만, FBX에 따라 다를 수 있어 발 위치 재고정).
        ///   3D 미리보기(RenderSoldierPreview)·플레이어 비주얼도 동일 헬퍼로 1.8m 확보.</summary>
        public static void NormalizeSoldierHeight(GameObject go, float targetHeight, bool skinnedLocalBounds = true)
        {
            if (go == null || targetHeight <= 0.01f) return;
            // [53차] 높이 실측을 포즈/FX 독립(MeasureStandingHeight)으로 교체 — 생성 직후 stale 월드 bounds와
            //   트레일/파티클 렌더러 오염이 스케일을 왜곡한 뿌리 수리([키검증] 플레이어 4.01m 실측 근거).
            //   [54차] 측정 모드 분리 — 병사 FBX는 localBounds(바인드 포즈)가 정밀(전원 1.80m 실측),
            //   플레이어 모델은 localBounds가 골격 스케일 구조로 0.61m 오측(×2.95 폭발) → 플레이어 경로는
            //   월드 bounds(FX 제외) 사용(검증된 1.16→×1.56→1.8m 체인 복원).
            float h = MeasureStandingHeight(go, skinnedLocalBounds);
            if (h <= 0.01f) return;
            var rends = go.GetComponentsInChildren<Renderer>();
            float prevFootY = 0f;
            if (rends != null && rends.Length > 0)
            {
                Bounds b0 = rends[0].bounds;
                foreach (var r in rends) b0.Encapsulate(r.bounds);
                prevFootY = b0.min.y;                  // 스케일 전 발(최하점) 월드 y
            }
            float factor = targetHeight / h;
            go.transform.localScale = go.transform.localScale * factor;
            // 스케일 후 발 위치 재고정 — 월드 bounds 최하단을 스케일 전 값으로 복원(정밀 접지는 GroundModelToY 담당).
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                go.transform.position += new Vector3(0f, prevFootY - b.min.y, 0f);
            }
            Debug.Log($"[TerritoryBuilder] 병사 크기 정규화 — {go.name} 높이 {h:F2}m → {targetHeight:F2}m (scale ×{factor:F2})");
        }

        /// <summary>[53차→54차] 포즈/FX 독립 키 실측 — SkinnedMeshRenderer는 localBounds(바인드 포즈)를
        ///   localToWorldMatrix로 월드 변환해 합산(애니 포즈 무관), Trail/파티클/라인 렌더러는 제외
        ///   (스윙 트레일 등 이펙트 AABB가 순간 수 미터로 부풀어 전체 측정을 오염).
        ///   [54차] skinnedLocalBounds=false면 SkinnedMeshRenderer도 월드 bounds 사용 — 플레이어 모델은
        ///   골격 스케일 구조 때문에 localBounds가 0.61m/7.91m로 오측되므로 월드 계열(1.16m 검증치)을 쓴다.</summary>
        public static float MeasureStandingHeight(GameObject go, bool skinnedLocalBounds = true)
        {
            if (go == null) return 0f;
            bool any = false;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is TrailRenderer || r is ParticleSystemRenderer || r is LineRenderer) continue;   // FX 렌더러 제외
                if (skinnedLocalBounds && r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    var lb = smr.localBounds;                     // 바인드 포즈 로컬 AABB — 애니 포즈 무관
                    var m = smr.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = lb.center + new Vector3(((i & 1) == 0 ? 1 : -1) * lb.extents.x,
                                                             ((i & 2) == 0 ? 1 : -1) * lb.extents.y,
                                                             ((i & 4) == 0 ? 1 : -1) * lb.extents.z);
                        var w = m.MultiplyPoint3x4(corner);
                        if (w.y < minY) minY = w.y;
                        if (w.y > maxY) maxY = w.y;
                    }
                    any = true;
                }
                else
                {
                    var b = r.bounds;                             // 일반 렌더러 — 월드 AABB
                    if (b.min.y < minY) minY = b.min.y;
                    if (b.max.y > maxY) maxY = b.max.y;
                    any = true;
                }
            }
            if (!any) return 0f;
            return Mathf.Max(0f, maxY - minY);
        }

        /// <summary>[50차→51차] 1.8m 고정 계수만 반환하는 순수 함수형 래퍼 — 접지(위치)는 호출부 책임
        ///   (TestTerritoryCombatSetup은 GroundModelToY를 이미 수행). NormalizeSoldierHeight와
        ///   동일 측정(렌더러 bounds)이라 결과가 일치하며, 배율(GetSoldierSizeMultiplier)과 곱해 쓴다.</summary>
        public static float NormalizeSoldierHeightFactor(GameObject go, float targetHeight)
        {
            if (go == null || targetHeight <= 0.01f) return 1f;
            // [53차] 포즈/FX 독립 실측(MeasureStandingHeight) — stale 월드 bounds/이펙트 오염 배제.
            float h = MeasureStandingHeight(go);
            Debug.Log($"[117차 R4] 병사 키실측: {go.name} h={h:F2}m (목표 1.8m)");
            if (h <= 0.01f) return 1f;
            return targetHeight / h;
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
                // [51차 사용자 확정 — 키 1.8m 전 씬 통일] 47차의 1.0m 목표 폐기. 병사 FBX 원본 높이를 실측해
                //   정확히 1.8m(표준 인간 키, SoldierTargetHeight)로 등비 스케일 — 임의 상수 스케일이 아니라
                //   bounds 실측이라 어느 FBX 구간(lv1-20/20-40/40-50)이든 동일 결과. 플레이어 비주얼도
                //   동일 상수로 1.8m 정규화(PlayerPlaceholder/TestPlayerAnimatorBoot/실내 테스트 씬) — 병사-플레이어 동일 키.
                NormalizeSoldierHeight(go, targetHeight: SoldierTargetHeight);
                go.transform.localScale = go.transform.localScale;   // (값 유지 — 위 헬퍼가 localScale 재계산)
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