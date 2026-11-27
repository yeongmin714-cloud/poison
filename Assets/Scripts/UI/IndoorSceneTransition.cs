using ProjectName.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Systems;
using ProjectName.Core.Data;

namespace ProjectName.UI
{
    /// <summary>
    /// C10-02: 실내/외 맵 전환 관리 (Additive Scene Loading).
    /// 건물 진입 시 "IndoorScene"을 Additive 모드로 로드하고,
    /// 퇴출 시 Additive 씬을 언로드합니다.
    /// </summary>
    public static class IndoorSceneTransition
    {
        private const string INDOOR_SCENE_NAME = "IndoorScene";
        private const string DEFAULT_WORLD_SCENE = "WorldScene";

        private static string _previousSceneName;
        private static string _pendingBuildingType;
        private static string _pendingNationStyle;
        private static bool _pendingIsPlayerOwned;
        private static string _pendingTerritoryKey;   // INTERIOR-VAR: 레이아웃 변형 결정론 시드용
        private static string _activeCastleTerritoryKey;
        private static string _activeCastleNationStyle;
        private static bool _activeCastleIsPlayerOwned;
        private static Vector3? _returnPosition;      // 진입 직전 플레이어 위치(퇴출 복귀용)
        private const float INDOOR_FLOOR_Y = 0f;      // 실내 바닥 높이 (IndoorBuilder.CreateRoom: 바닥 XZ 평면 y=0)
        private const float IndoorCamDistance = 11f;  // [카메라통일 A4] 실내 PM 원근 카메라 거리(PM 기본값 동일)
        private static float _preIndoorCamDistance = -1f;   // [카메라통일 A4] 진입 직전 거리(<0 = 미저장 — 중첩 진입 시 첫 값 보존)
        private static bool _initialized;

        // ── [P19] 별개 씬 분리 (월드 언로드) ──
        //   진입: 월드 씬 이름/위치 저장 → IndoorScene 로드 → 플레이어 이동 → 월드 언로드(렉 해소)
        //   퇴출: 월드 재로드 → 활성화 → 플레이어 복귀 위치 이동 → IndoorScene 언로드
        //   병사/몬스터는 월드 소속 — 언로드 시 함께 정리, 재로드 시 씬 셋업이 자동 재스폰(사용자 확정).
        private static string _worldSceneName;        // 복귀할 월드 씬 이름
        private static bool _worldUnloaded;           // 월드 언로드 완료 플래그
        private static GameObject _playerHolder;      // 실내→실내 전환 시 플레이어 임시 보관(DontDestroyOnLoad)
        private static GameObject _activeRoom;        // [125차 후속12] 현재 실내 룸(제자리 리빌드 파괴 대상 추적)

        /// <summary>[P14] 실내 활성 여부 — 병사/몬스터 AI가 플레이어 추적/어그로를 실내로 끌고 오는 것 차단.
        ///   Systems 측 AI 루프가 이 플래그를 게이트로 읽는다(Update 초입 정지).</summary>
        public static bool IsIndoor { get; private set; }

        /// <summary>[152차 P1] 현재 성 실내에 보존된 영지 키 — 성 진입(EnterBuilding buildingType=castle)에서
        ///   _pendingTerritoryKey로 확정되고 비성 실내(BuildAndSettleIndoor 타입별 정리)/퇴출(FinishExit)은 null.
        ///   ConstructionManager 실내 배치 분기(훈련장 등 비음식점 시설)가 실내 원점 좌표 오귀속(황제국
        ///   Empire_1) 대신 실제 성 영지로 귀속시키기 위해 읽는다. 읽기 전용 — 기존 캡슐화(setter 미노출) 유지.</summary>
        public static string ActiveCastleTerritoryKey => _activeCastleTerritoryKey;

        /// <summary>[P8 수리] 정적 생성자는 "타입 최초 접근 시"에만 실행 — 테스트 씬처럼 이 타입을
        ///        참조하는 코드가 없으면 구독 자체가 없어 E키가 무반응이 된다(실측).
        ///        RuntimeInitializeOnLoadMethod로 게임 시작 시 확정 구독.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RuntimeEnsureSubscription()
        {
            Initialize();
        }

        /// <summary>정적 생성자: BuildingEvents 구독 (중복 방지)</summary>
        static IndoorSceneTransition()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            BuildingEvents.OnEnterBuildingRequest += HandleEnterBuilding;
            BuildingEvents.OnExitBuildingRequest += ExitBuilding;
            TerritoryDatabase.OwnershipChanged += OnTerritoryOwnershipChanged;
            TerritoryDatabase.OwnershipLost += OnTerritoryOwnershipLost; // [F3] 배신/AI 탈환 — 영주 소유 성 재구성

        }

        private static void OnTerritoryOwnershipChanged(TerritoryId id, TerritoryOwnership ownership)
        {
            try
            {
                if (ownership != TerritoryOwnership.PlayerOwned) return;
                if (!IsIndoor || !IsIndoorSceneLoaded()) return;
                if (string.IsNullOrEmpty(_activeCastleTerritoryKey)) return;
                if (id.ToString() != _activeCastleTerritoryKey) return;
                // Pending 값은 씬 빌드 완료 시 초기화되므로 현재 실내 소유 상태를 별도 추적한다.
                if (_activeCastleIsPlayerOwned) return;

                string nation = _activeCastleNationStyle ?? "Empire";
                string territoryKey = _activeCastleTerritoryKey;
                _activeCastleIsPlayerOwned = true; // 재전환 로딩 중 중복 이벤트/재호출 방지
                Debug.Log($"[IndoorSceneTransition] ⚔️ 영지 {id} 내 소속 전환 — 실내를 플레이어 소유 성으로 재구성");
                EnterBuilding("castle", nation, true, territoryKey);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[IndoorSceneTransition] 소유권 전환 실내 갱신 실패: {e.Message}");
            }
        }

        /// <summary>
        /// [F3] 소유 상실(영주 배신/AI 재탈환) — 플레이어 소유 실내에 있는 경우
        /// 영주 소유 성으로 재구성한다 (수비 병사 + 영주 조우 자동 스폰 = 재탈환 전투 준비).
        /// OnTerritoryOwnershipChanged의 대칭 분기.
        /// </summary>
        private static void OnTerritoryOwnershipLost(TerritoryId id, TerritoryOwnership ownership)
        {
            try
            {
                if (ownership == TerritoryOwnership.PlayerOwned) return;
                if (!IsIndoor || !IsIndoorSceneLoaded()) return;
                if (string.IsNullOrEmpty(_activeCastleTerritoryKey)) return;
                if (id.ToString() != _activeCastleTerritoryKey) return;
                if (!_activeCastleIsPlayerOwned) return;

                string nation = _activeCastleNationStyle ?? "Empire";
                string territoryKey = _activeCastleTerritoryKey;
                _activeCastleIsPlayerOwned = false; // 재전환 로딩 중 중복 이벤트/재호출 방지
                Debug.Log($"[IndoorSceneTransition] 🐍 영지 {id} 소유 상실 — 실내를 영주 소유 성으로 재구성 (수비 병사 재배치)");
                EnterBuilding("castle", nation, false, territoryKey);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[IndoorSceneTransition] 소유 상실 실내 갱신 실패: {e.Message}");
            }
        }

        private static void HandleEnterBuilding(string buildingType, string nationStyle, bool isPlayerOwned, string territoryKey)
        {
            EnterBuilding(buildingType, nationStyle, isPlayerOwned, territoryKey);
        }

        /// <summary>
        /// 건물 진입. 현재 활성 씬 이름을 저장하고 "IndoorScene"을 Additive 모드로 로드한 후,
        /// buildingType에 따라 적절한 Builder를 호출합니다.
        /// 로드 완료 후 IndoorScene을 활성 씬으로 설정합니다.
        /// </summary>
        /// <param name="buildingType">
        /// "CraftHouse", "Church", "House", "Castle", "Shop" 중 하나.
        /// </param>
        /// <param name="nationStyle">
        /// Castle 타입 진입 시 국가 스타일 (예: "Empire", "Eastern", "Western", "Southern", "Northern").
        /// 기본값 null.
        /// </param>
        /// <param name="isPlayerOwned">
        /// Castle 타입 진입 시 플레이어 소유 성 여부 (true → PlayerCastleInteriorBuilder, false → CastleInteriorBuilder).
        /// 기본값 false.
        /// </param>
        public static void EnterBuilding(string buildingType, string nationStyle = null, bool isPlayerOwned = false, string territoryKey = null)
        {
            // [P19] 실내→실내 전환(성 내부 상점/크래프트하우스) — 월드 복귀 상태를 보존하고
            //   플레이어만 임시 보관(DontDestroyOnLoad)해 IndoorScene 언로드에서 파괴되지 않게 한다.
            bool alreadyIndoor = SceneManager.GetActiveScene().name == INDOOR_SCENE_NAME;

            if (!alreadyIndoor)
            {
                // 월드 진입 — 씬/위치 저장(퇴출 복귀용. 씬이 언로드돼도 데이터는 남는다)
                _previousSceneName = SceneManager.GetActiveScene().name;
                _worldSceneName = _previousSceneName;
                _worldUnloaded = false;

                var enteringPlayer = GameObject.FindGameObjectWithTag("Player");
                _returnPosition = enteringPlayer != null ? (Vector3?)enteringPlayer.transform.position : null;
            }

            _pendingBuildingType = buildingType;
            _pendingNationStyle = nationStyle;
            _pendingIsPlayerOwned = isPlayerOwned;
            _pendingTerritoryKey = territoryKey;

            // 이미 IndoorScene이 로드되어 있으면 언로드 후 재로드 (실내→실내 전환)
            Scene indoorScene = SceneManager.GetSceneByName(INDOOR_SCENE_NAME);
            if (indoorScene.isLoaded)
            {
                // [125차 후속12] 마지막 씬 언로드 불가 감지 — 실내 단독 구동(테스트씬/메인 실내 진입 후)에서
                //   IndoorScene이 유일한 로드 씬이면 UnloadSceneAsync가 Unity 제약으로 거부된다
                //   ("Unloading the last loaded scene is not supported" Play 실측 → 베팅 전환 전체 실패의 뿌리).
                //   → 언로드/재로드 대신 제자리 리빌드: 기존 룸 파괴 → BuildAndSettleIndoor 직접 호출
                //   (동일 빌더/후처리 로직 공유 — 플레이어 홀더 이동/씬 전환 없음).
                bool isOnlyLoadedScene = true;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s.isLoaded && s.name != INDOOR_SCENE_NAME) { isOnlyLoadedScene = false; break; }
                }
                if (isOnlyLoadedScene)
                {
                    Debug.Log("[IndoorSceneTransition] [125차 후속12] 실내 단독 구동 — 마지막 씬 언로드 불가 → 제자리 리빌드 경로");
                    DestroyCurrentIndoorRoom();
                    SceneManager.sceneLoaded -= OnIndoorSceneLoaded;   // 잔존 구독 방어(멱등)
                    BuildAndSettleIndoor(indoorScene);
                    return;
                }

                if (alreadyIndoor)
                {
                    Scene holder = GetOrCreatePlayerHolder();
                    var p = GameObject.FindGameObjectWithTag("Player");
                    if (p != null) SceneManager.MoveGameObjectToScene(p, holder);
                    Debug.Log("[IndoorSceneTransition] 실내→실내 전환 — 플레이어 임시 보관");
                }
                SceneManager.sceneUnloaded += OnPreviousIndoorUnloaded;
                SceneManager.UnloadSceneAsync(INDOOR_SCENE_NAME);
                return;
            }

            // SceneManager.sceneLoaded 콜백 등록
            SceneManager.sceneLoaded += OnIndoorSceneLoaded;

            IsIndoor = true;   // [P14] 실내 진입 — 월드 AI 추적/어그로 게이트 ON
            ProjectName.Core.UITransitionState.IndoorActive = true;

            // [P16-4 강화] 진입 순간 월드 상태 정리 — 어그로 잔존/명령 잔존이 게이트와 무관하게
            //   남아 이동 프레임(velocity)으로 실내에 유입되는 것을 뿌리에서 제거.
            ClearWorldAggroAndCommands();

            // 씬 Additive 로드
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.LoadSceneAsync(INDOOR_SCENE_NAME, 0.3f, 0.3f, LoadSceneMode.Additive);
            }
            else
            {
                Debug.LogWarning("[IndoorSceneTransition] LoadingManager.Instance가 없음. 직접 LoadSceneAsync 호출.");
                SceneManager.LoadSceneAsync(INDOOR_SCENE_NAME, LoadSceneMode.Additive);
            }
        }

        /// <summary>
        /// 이전 IndoorScene이 언로드된 후 새로 Additive 로드합니다.
        /// </summary>
        /// <summary>[125차 후속12] 현재 실내 룸 파괴 — 제자리 리빌드 전 정리(룸 자식 트리거도 함께 소멸).</summary>
        private static void DestroyCurrentIndoorRoom()
        {
            if (_activeRoom != null)
            {
                Debug.Log($"[IndoorSceneTransition] [125차 후속12] 기존 실내 룸 파괴 — {_activeRoom.name}");
                UnityEngine.Object.Destroy(_activeRoom);
                _activeRoom = null;
                return;
            }
            // 추적 실패 폴백 — 알려진 룸명 직접 탐색(구 버전 호환)
            var legacy = GameObject.Find("ArenaRoom");
            if (legacy == null) legacy = GameObject.Find("ArenaPitRoom");
            if (legacy != null)
            {
                Debug.Log($"[IndoorSceneTransition] [125차 후속12] 기존 실내 룸 파괴(폴백 탐색) — {legacy.name}");
                UnityEngine.Object.Destroy(legacy);
            }
        }

        /// <summary>[125차 후속25 R3-1] 씬 루트 이름 탐색 — 비활성 루트도 검색(GameObject.Find는 활성만 검색).
        ///   MedievalShell 비활성화 후 복원을 위해 비활성 객체도 찾아야 한다.</summary>
        private static GameObject FindSceneRoot(Scene scene, string rootName)
        {
            if (!scene.isLoaded) return null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == rootName) return root;
            return null;
        }

        private static void OnPreviousIndoorUnloaded(Scene scene)
        {
            if (scene.name != INDOOR_SCENE_NAME) return;
            SceneManager.sceneUnloaded -= OnPreviousIndoorUnloaded;

            SceneManager.sceneLoaded += OnIndoorSceneLoaded;
            SceneManager.LoadSceneAsync(INDOOR_SCENE_NAME, LoadSceneMode.Additive);
        }

        /// <summary>
        /// 씬 로드 완료 콜백. _pendingBuildingType에 맞는 Builder를 호출하고 IndoorScene을 활성화합니다.
        /// </summary>
        private static void OnIndoorSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != INDOOR_SCENE_NAME) return;

            Debug.Log($"[IndoorSceneTransition] OnIndoorSceneLoaded 진입 — buildingType: {_pendingBuildingType}, owner: {_pendingIsPlayerOwned}");

            // 중복 실행 방지
            SceneManager.sceneLoaded -= OnIndoorSceneLoaded;

            BuildAndSettleIndoor(scene);   // [125차 후속12] 본문 공용화 — 재로드 경로/제자리 리빌드 경로 공유
        }

        /// <summary>
        /// [125차 후속12] 실내 빌드+정착 공용 본문 — OnIndoorSceneLoaded(언로드/재로드 경로)와
        /// EnterBuilding의 제자리 리빌드 경로(마지막 씬 언로드 불가 — "Unloading the last loaded scene
        /// is not supported" Play 실측)가 동일 로직을 공유한다. _pending* 값은 호출 전 설정분을 소비.
        /// </summary>
        private static void BuildAndSettleIndoor(Scene scene)
        {
            string buildingType = _pendingBuildingType ?? string.Empty;
            Debug.Log($"[IndoorSceneTransition][P20-6] 빌더 시작 — buildingType='{buildingType}'");   // 진단: type 누락 즉별
            // 성이 아닌 건물 진입이면 기존 성의 추적 상태를 정리한다.
            _activeCastleTerritoryKey = null;
            _activeCastleNationStyle = null;
            _activeCastleIsPlayerOwned = false;
            ProjectName.Core.UITransitionState.IndoorCastleTerritoryKey = null;   // [152차 P1-수리] Core 공용 키 정리

            // IndoorScene을 활성 씬으로 설정
            SceneManager.SetActiveScene(scene);

            // [P20-6] 빌더 반환 Room을 직접 전달 — GameObject.Find 실패 변수 제거 + 예외 격리
            GameObject hqRoom = null;
            try
            {
                switch (buildingType.ToLower())
                {
                    case "crafthouse":
                        hqRoom = CraftHouseInteriorBuilder.BuildCraftHouseInterior();
                        break;
                    case "church":
                        hqRoom = ChurchInteriorBuilder.BuildChurchInterior();
                        break;
                    case "house":
                    case "npchouse":
                        hqRoom = HouseInteriorBuilder.BuildHouseInterior();
                        break;
                    case "castle":
                        string nation = _pendingNationStyle ?? "Empire";
                        _activeCastleTerritoryKey = _pendingTerritoryKey;
                        ProjectName.Core.UITransitionState.IndoorCastleTerritoryKey = _pendingTerritoryKey;   // [152차 P1-수리] Core 공용 — Systems 실내 배치 분기 공급(Systems→UI 직접 참조 금지 준용)
                        _activeCastleNationStyle = nation;
                        _activeCastleIsPlayerOwned = _pendingIsPlayerOwned;
                        // INTERIOR-VAR: 영지 키(우선)/nation+소유로 결정론 해시 → 8종 가구 배치 변형.
                        // [2026-10-14 Phase 4] 가구 variant(0~7)와 건축 레이아웃 계열은 독립 축 —
                        //   variant는 기존 djb2 해시를 그대로 유지하고, 계열은 아래 FNV-1a 배정으로 별도 선택.
                        int layoutVariant = ComputeLayoutVariant(_pendingTerritoryKey, nation, _pendingIsPlayerOwned);
                        // [2026-10-14 Phase 4 — 영지 키 레이아웃 배정] 결정론 FNV-1a — 같은 영지 키는
                        //   항상 같은 계열(재방문/소유권 전환 재현 보장). null/빈 키는 기준형 KeepOctagon 폴백.
                        InteriorLayoutDefinition interiorLayout = InteriorLayoutDefinition.SelectForTerritory(_pendingTerritoryKey);
                        // [2026-10-10 J1 — 영지 실내 통일] 소유 여부 무관 동일 빌더 — 모든 영지 실내가
                        //   플레이어 성 표준(배치/모습/품질 계층 H1~H5)을 따른다. 구 영주 성 셸(48×36) 폐기.
                        //   J1 계약 호출 형태: PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nation, layoutVariant)
                        //   → [Phase 4] 명시 레이아웃 정의 3인자 오버로드로 확장(2인자는 KeepOctagon 고정 폴백).
                        //   → [F4] 4인자 — 실제 영지 키 전달(작업대/저장고가 점령 영지에 귀속; null이면 국가 매핑 폴백)
                        hqRoom = PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nation, layoutVariant, interiorLayout, _pendingTerritoryKey);
                        // [J2] 잠긴 문 콘텐츠(집무실/금고/무기고/문서고)는 유지 — 로비 포탈 재정착(영주 성만).
                        if (hqRoom != null && !_pendingIsPlayerOwned)
                            CastleInteriorBuilder.AddLordLockedDoors(hqRoom);
                        if (hqRoom != null)
                        {
                            // [38차] 병사 관리 체계 보장 — 씬 전환으로 GuardManager(씬 싱글턴)가
                            //   소실된 경우 재생성(배치 UI 목록/역할 배정이 실내에서도 작동).
                            if (GuardManager.Instance == null)
                            {
                                var gmGO = new GameObject("GuardManager");
                                gmGO.AddComponent<GuardManager>();
                            }
                            GuardTaskSystem.Ensure();

                            // [무번호 89] 실내 시설 동일화 — 소유 무관 상점/크래프트하우스 배치(사용자 지시
                            //   "다른 영주 소속 영지도 실내가 완전히 동일"). 병사 구성/잠긴 문은 게임 상태 콘텐츠로 유지.
                            TerritoryBuilder.SpawnInteriorFixtures(hqRoom.transform.position, nation);
                            // [Phase F] 플레이어 소유 성: 아군 막사 병사 / 영주 성: 성향 배분 실내 수비 병사 스폰+적대
                            if (_pendingIsPlayerOwned)
                            {
                                TerritoryBuilder.SpawnInteriorAlliedGuards(hqRoom, nation); // 배럭 침대에 아군 병사(38차 — 모든 영지 소속 병사 침대 휴식)
                            }
                            else
                            {
                                // [126차 정정] 영주 딸 — 생존 영주(비PlayerOwned) 진입 본경로. SpawnForInvadedTerritory
                                //   내부 훅에서 결정론 롬 충족 시에만 확률 등장(점령 완료 성에는 등장하지 않는다).
                                TerritoryBuilder.SpawnInteriorDefenseGuards(hqRoom.transform.position, nation, _pendingTerritoryKey);
                                InvadedLordEncounter.SpawnForInvadedTerritory(hqRoom, _pendingTerritoryKey);
                            }
                        }
                        break;
                    case "barn":
                        BarnInteriorBuilder.BuildBarnInterior();          // void 반환 — Room 탐색 폴백
                        hqRoom = GameObject.Find("Room");
                        break;
                    case "shop":
                        hqRoom = ShopInteriorBuilder.BuildShopInterior();
                        break;
                    case "restaurant":
                        hqRoom = RestaurantInteriorBuilder.BuildRestaurantInterior();
                        break;
                    case "cave":
                        _activeCastleTerritoryKey = null;
                        _activeCastleNationStyle = null;
                        _activeCastleIsPlayerOwned = false;
                        hqRoom = CaveInteriorBuilder.BuildCaveInterior(_pendingNationStyle ?? "default", 1);
                        break;
                    case "arena":
                        _activeCastleTerritoryKey = null;
                        _activeCastleNationStyle = null;
                        _activeCastleIsPlayerOwned = false;
                        hqRoom = ArenaInteriorBuilder.BuildArenaInterior(_pendingNationStyle ?? "Empire");
                        break;
                    case "afterlife":
                        // [A2 이계] 사망자의 마지막 항전 아레나 — 상점/병사/잠금 문 없음,
                        //   웨이브/귀환 문은 AfterlifeSurvivalSystem이 소유(Room은 (5000,0,5000) 월드 격리).
                        _activeCastleTerritoryKey = null;
                        _activeCastleNationStyle = null;
                        _activeCastleIsPlayerOwned = false;
                        hqRoom = AfterlifeInteriorBuilder.BuildAfterlifeInterior();
                        break;
                    case "arenapit":
                        _activeCastleTerritoryKey = null;
                        _activeCastleNationStyle = null;
                        _activeCastleIsPlayerOwned = false;
                        hqRoom = ArenaPitInteriorBuilder.BuildArenaPitInterior(_pendingNationStyle ?? "Empire");
                        break;
                    default:
                        _activeCastleTerritoryKey = null;
                        _activeCastleNationStyle = null;
                        _activeCastleIsPlayerOwned = false;
                        Debug.LogWarning($"[IndoorSceneTransition] 알 수 없는 buildingType: '{buildingType}'. 기본 주택 생성.");
                        hqRoom = HouseInteriorBuilder.BuildHouseInterior();
                        break;
                }
                Debug.Log($"[IndoorSceneTransition][P20-6] 빌더 완료 — Room={(hqRoom != null ? hqRoom.name : "NULL")}");
                _activeRoom = hqRoom;   // [125차 후속12] 제자리 리빌드 파괴 대상 추적

                // [146차 P2] 실내 미니맵 캡처 훅 — 룸 빌드 완료 지점(동일 UI 어셈블리 직접 호출).
                //   위치 근거(실측): 빌더 switch 직후(L409)로 플레이어 텔레포트(L432~)·HQ 표면 적용 이전 —
                //   캡처 RT에 플레이어 오브젝트 오염이 없고, try 블록 내이라 빌더 예외 시 자연 스킵(무음).
                ProjectName.UI.Toolkit.IndoorMinimapBaker.CaptureFor(hqRoom);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[IndoorSceneTransition][P20-6] 빌더 예외 — buildingType='{buildingType}': {e}");
            }

            // [P17-D] 고품질 실내 표면 적용 — Room을 빌더 반환값으로 직접 전달(파일 없으면 폴백 유지)
            //   [125차 후속22 F3] buildingType 전달 — 투기장(야외 경기장)은 실내 HQ 체계 개입 금지
            ApplyHighQualityInterior(hqRoom, buildingType);

            // [P20-6] Room 최종 실패 시에만 셸 폴백 — 성공 시 셸이 설계 실내를 덮지 않게 순서 보존
            if (hqRoom == null && GameObject.Find("MedievalShell") == null)
                ProjectName.Systems.MedievalShellBuilder.CreateShell();

            // 플레이어를 내부 원점으로 이동(카메라는 플레이어 추적 유지) — builders는 원점 부근에 내부 생성
            var indoorPlayer = GameObject.FindGameObjectWithTag("Player");
            if (indoorPlayer == null)
                indoorPlayer = UnityEngine.Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>()?.gameObject;
            // [125차 후속13] 투기장(ArenaPit)은 관전 씬 — 플레이어를 남측 관전 위치에 배치(투기 중앙 회피)
            // [125차 후속28 F2] 관전 위치 -18→-7.5 — 관전 카메라(0,26,0 ortho 8)의 세로 시야(±8m) 안쪽으로
            //   이동(사용자 관찰 "플레이어 보이지 않음"의 구조적 원인 = z -18은 뷰 밖). y 0.65 = 모래 톱 0.15 위.
            bool isPit = buildingType.Equals("arenapit", System.StringComparison.OrdinalIgnoreCase);
            if (indoorPlayer != null)
                indoorPlayer.transform.position = isPit
                    ? new Vector3(0f, 0.65f, -7.5f)   // 남측 관전 위치 — 관전 뷰 내부(하단), 병사 전투 구역 밖
                    : new Vector3(0f, INDOOR_FLOOR_Y + 0.5f, 0f); // 여유 높이(바닥 콜라이더 위)

            // 2026-09-09(6차): 플레이어 하이어라키를 IndoorScene으로 이동 — 계층 표시+활성 씬 정합
            if (indoorPlayer != null && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(indoorPlayer, scene);
                Debug.Log($"[IndoorSceneTransition] 플레이어 이동 완료 → 소속 씬: {indoorPlayer.scene.name} / pos: {indoorPlayer.transform.position}");
                ReleasePlayerHolder();          // [P19] 실내→실내 전환 홀더 정리
                MaybeUnloadWorldScene();        // [P19] 플레이어 안전 이동 확인 후 월드 언로드(렉 해소)
            }
            else
            {
                // 2026-09-09(7차 FIX): sceneLoaded 콜백 시점에 플레이어가 아직 없음(로딩 타이밍) →
                // 지연 재시도 러너가 다음 프레임부터 플레이어를 찾아 IndoorScene 이동+스폰
                Debug.LogError("[IndoorSceneTransition] 플레이어 미발견 — 지연 재시도 러너 가동");
                var runnerGO = new GameObject("IndoorEnterRunner");
                runnerGO.AddComponent<IndoorEnterRunner>().Init(scene);
            }

            // 2026-09-09(6차 FIX): IndoorCamera가 존재할 때만 메인 카메라 비활성 — 없으면 메인 카메라(플레이어 추적) 유지
            // [125차 후속16] 카메라 우선순위 수리 — MainCamera가 추적캠(IndoorCameraFollow 보유, 테스트씬 공통 탑다운
            //   계약)이면 그쪽이 구동 주체: 비활성이면 재활성 + 셸 IndoorCamera 비활성. 근거: 기존 무조건 비활성이
            //   ①실내씬 각도를 공통 패턴(65° 추적)과 다르게 만들고 ②비활성 카메라는 태그 검색에서 제외되어
            //   투기장 관전 카메라 세팅(후속13)이 통째로 스킵됐다(후속15 검증 로그 부재 실측).
            //   MainCamera가 추적캠이 아니면 기존 동작 유지(메인 게임 회귀 방지 가드).
            var mainCamGO = GameObject.FindGameObjectWithTag("MainCamera");
            var indoorCam = GameObject.Find("IndoorCamera");
            if (mainCamGO == null)
            {
                // 비활성 포함 탐색 — 기존 블록이 비활성화한 메인 카메라 복구용
                foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                {
                    if (c.gameObject.CompareTag("MainCamera")) { mainCamGO = c.gameObject; break; }
                }
            }
            bool mainIsFollowCam = mainCamGO != null && mainCamGO.GetComponent<IndoorCameraFollow>() != null;
            if (mainIsFollowCam)
            {
                if (!mainCamGO.activeSelf) mainCamGO.SetActive(true);
                if (indoorCam != null && indoorCam.activeInHierarchy) indoorCam.SetActive(false);
            }
            else if (indoorCam != null && indoorCam.activeInHierarchy)
            {
                if (mainCamGO != null) mainCamGO.SetActive(false);
            }
            else if (mainCamGO != null && !mainCamGO.activeSelf)
            {
                mainCamGO.SetActive(true); // 이전 진입 실패 복구
            }

            // [카메라통일 A4] PM 원근 카메라 실내 거리 세팅 — 관전 세션(arenapit) 제외. 월드에서 휠 줌아웃한
            //   거리(_camDistance 최대 30m)를 그대로 실내에 들이면 원근 오빗이 벽 밖까지 물러난다 →
            //   진입 시 실내 적정 거리(11m = PM 기본값/IndoorCameraFollow.DefaultDist 동일값)로 세팅하고
            //   복귀(FinishExit) 시 진입 직전 거리를 동일 공개 프로퍼티 경로로 복원한다(중첩 실내→실내는 첫 값 보존).
            if (!isPit)
            {
                var pmForCam = indoorPlayer != null
                    ? indoorPlayer.GetComponent<ProjectName.Systems.PlayerMovement>()
                    : UnityEngine.Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>();
                if (pmForCam != null)
                {
                    if (_preIndoorCamDistance < 0f) _preIndoorCamDistance = pmForCam.CameraDistance;
                    pmForCam.CameraDistance = IndoorCamDistance;
                    Debug.Log($"[IndoorSceneTransition] [카메라통일 A4] PM 원근 카메라 실내 거리 세팅 — {IndoorCamDistance}m (진입 직전 {_preIndoorCamDistance:0.0}m)");
                }
            }

            // 2026-09-09(3차 FIX): 까만 화면 방지 — 실내 앰비언트를 따뜻한 플랫톤으로
            // (활성 씬이 IndoorScene이므로 RenderSettings는 실내 것만 적용, 복귀 시 자동 원복)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // [125차 후속12] 앰비언트 폴백은 빌더 부재 시만 — 이 블록이 빌더가 설정한 앰비언트를 항상
            //   (0.13,0.11,0.09)로 되돌려 실내 조명 상향(후속5/7/8)을 전부 무효화했다(어두움의 진짜 뿌리 —
            //   빌더 "후" 실행 순서 실측). 빌더가 없는 셸 폴백 씬에만 디폴트 앰비언트를 적용한다.
            if (hqRoom == null)
                RenderSettings.ambientLight = new Color(0.13f, 0.11f, 0.09f);

            // [125차 후속25 R3-1] 투기장=야외 경기장 — IndoorScene 베이크 실내 껍데기(MedievalShell)와 InteriorSun 비활성화.
            //   셸 8m 벽 내부에 투기장이 있으면 저각 카메라 시야가 셸 벽 텍스처로 가득 차 "회색 화면"이 된다
            //   (영상10 판독 확정: 회색=밀착된 벽 텍스처). 로비/일반 실내 복귀 시 씬 기본 상태(셸 활성)로 복원.
            {
                var shell = FindSceneRoot(scene, "MedievalShell");
                var interiorSun = FindSceneRoot(scene, "InteriorSun");
                if (isPit)
                {
                    if (shell != null) shell.SetActive(false);
                    if (interiorSun != null) interiorSun.SetActive(false);
                    Debug.Log("[IndoorSceneTransition] [125차 후속25 R3-1] 투기장 야외화 — MedievalShell/InteriorSun 비활성화");
                }
                else
                {
                    if (shell != null && !shell.activeSelf) shell.SetActive(true);
                    if (interiorSun != null && !interiorSun.activeSelf) interiorSun.SetActive(true);
                    // [125차 후속25 R3-3] 투기장 세션 종료 — 관전 카메라 락 해제(추적 카메라 복귀 허용)
                    if (_spectatorLock != null)
                    {
                        UnityEngine.Object.Destroy(_spectatorLock.gameObject);
                        _spectatorLock = null;
                        Debug.Log("[IndoorSceneTransition] [125차 후속25 R3-3] 관전 카메라 락 해제 — 로비 복귀");
                    }
                }
            }

            // [125차 후속13] 투기장 관전 카메라 — 플레이어 추종 탑다운 대신 아레나 중앙 수직 하강 고정
            //   (사용자 지시: "아레나 중앙 기준 아래로 내려다 보도록 카메라를 고정" — 경기가 눈에 잘 들어오게).
            //   복귀(arena 로비) 시 추적 재개+공통 탑다운 orthoSize 복원.
            // [125차 후속16] 관전 카메라 탐색 — 비활성 포함(기존 블록이 비활성화한 케이스 대비 — 후속15 검증
            //   로그 부재 실측의 뿌리). MainCamera 태그 우선, 없으면 ArenaTestCamera.
            Camera duelCam = null;
            GameObject duelCamGO = null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            {
                if (c.gameObject.CompareTag("MainCamera") || c.name == "ArenaTestCamera") { duelCam = c; duelCamGO = c.gameObject; break; }
            }
            if (isPit)
            {
                // [카메라통일 A4] 억제 게이트 제거 — 관전 세션 소유는 ArenaSpectatorCameraLock(아래 Attach)이
                //   PM.ApplyFollowCamera 선두 게이트(ArenaSpectatorCameraLock.Active)로 담당한다. PM 억제 없이
                //   실내/관전 모두 단일 소유 계약(잠금 컴포넌트)으로 정리 — 이중 억제 경로 폐기.
                if (duelCamGO != null)
                {
                    if (!duelCamGO.activeSelf) duelCamGO.SetActive(true);   // [125차 후속16] 비활성 복구
                    duelCamGO.transform.position = ArenaSpectatorCameraLock.SpectatorPos;   // [125차 후속31 M1] 사선 관전 — 수직 폐기(단일 소스 공유)
                    duelCamGO.transform.rotation = Quaternion.Euler(ArenaSpectatorCameraLock.SpectatorPitchDeg, 0f, 0f);
                    if (duelCam != null && duelCam.orthographic) duelCam.orthographicSize = ArenaSpectatorCameraLock.SpectatorOrthoSize;   // [125차 후속31 M1] 8→10 — 사선 투영 수용
                    var shellCam = GameObject.Find("IndoorCamera");
                    if (shellCam != null && shellCam.activeInHierarchy) shellCam.SetActive(false);   // 셸 카메라 경합 방지
                    Debug.Log($"[IndoorSceneTransition] [125차 후속15/16] 관전 카메라 확정 — pos={duelCamGO.transform.position} / euler={duelCamGO.transform.rotation.eulerAngles} / ortho={(duelCam != null && duelCam.orthographic ? duelCam.orthographicSize.ToString("0.0") : "원근")}");
                }
                else
                {
                    Debug.LogError("[IndoorSceneTransition] [125차 후속16] 관전 카메라 미발견(비활성 포함) — 카메라 체계 점검 필요");
                }
                Debug.Log("[IndoorSceneTransition] [125차 후속13] 투기장 관전 카메라 고정 — 중앙 수직 하강(orthoSize 8)");
                // [125차 후속28 F3] 결투선수 이름표 보장 — 오버레이 명시 생성(멱등, 부트스트랩 의존 제거)
                ProjectName.UI.Toolkit.NameplateOverlayUTK.Ensure();
                AuditPitRendering(hqRoom);   // [125차 후속22 F1] 렌더 감사 — 1회성 실측 덤프
                // [125차 후속25 R3-2] 지연 감사 — 빌드 2.5s 후 상태 덤프+RT 캡처(영상10의 "회색 덮임" 시점 판별용).
                //   결투 개시~수 초 후 카메라/셸/라이트 상태를 수치로 특정한다.
                RenderAuditProbe.RunDelayed("pit_late", 2.5f);
                // [125차 후속25 R3-3] 관전 카메라 소유 강제 — pit_late 실측(빌드 2.5s 후 카메라 (0,26,0)→(0,0,0) 낙하,
                //   y=0=모래 내부 → 시야 void) 근거. 세션 동안 매 프레임 말 관전 프레이밍 재어셜 + 이동 주체 페이즈 진단.
                if (duelCam != null && _spectatorLock == null)
                    _spectatorLock = ProjectName.Systems.ArenaSpectatorCameraLock.Attach(duelCam);
            }
            else if (buildingType.Equals("arena", System.StringComparison.OrdinalIgnoreCase))
            {
                ProjectName.Systems.PlayerMovement.CameraFollowSuppressed = false;   // 로비 복귀 — 추적 재개
                if (duelCamGO != null)
                {
                    var lobCam = duelCamGO.GetComponent<Camera>();
                    if (lobCam != null && lobCam.orthographic) lobCam.orthographicSize = 14f;   // 공통 탑다운 계약값 복원
                }
                Debug.Log("[IndoorSceneTransition] [125차 후속13] 로비 복귀 — 추적 카메라 재개");
            }

            _pendingBuildingType = null;
            _pendingNationStyle = null;
            _pendingIsPlayerOwned = false;

        }

        /// <summary>[P16-4] 월드 몬스터 어그로 해제 + 병사 이동/전투 명령 취소 (실내 진입 순간 1회).</summary>
        private static void ClearWorldAggroAndCommands()
        {
            // 몬스터 어그로 전량 해제 (추격 목표 상실 → Idle 복귀)
            var monsters = UnityEngine.Object.FindObjectsByType<ProjectName.Systems.AnimalAI>(
                UnityEngine.FindObjectsSortMode.None);
            foreach (var m in monsters)
            {
                if (m != null) m.ClearAggro();
            }

            // 병사 이동/공격 명령 + 전투 상태 해제 (명령 잔존 이동 차단)
            var guards = UnityEngine.Object.FindObjectsByType<ProjectName.Systems.GuardPlaceholder>(
                UnityEngine.FindObjectsSortMode.None);
            foreach (var g in guards)
            {
                if (g == null) continue;
                g.ClearCommand();
                g.SetInCombat(false);
            }
            Debug.Log($"[IndoorSceneTransition] 월드 정리 — 어그로 해제 {monsters.Length}체, 명령 해제 {guards.Length}명");
        }

        // ═══════════════════════ [P19] 별개 씬 분리 헬퍼 ═══════════════════════

        /// <summary>실내→실내 전환용 플레이어 임시 보관 오브젝트(DontDestroyOnLoad 씬).</summary>
        private static Scene GetOrCreatePlayerHolder()
        {
            if (_playerHolder == null)
            {
                _playerHolder = new GameObject("IndoorPlayerHolder");
                UnityEngine.Object.DontDestroyOnLoad(_playerHolder);
            }
            return _playerHolder.scene;
        }

        private static void ReleasePlayerHolder()
        {
            if (_playerHolder != null)
            {
                UnityEngine.Object.Destroy(_playerHolder);
                _playerHolder = null;
            }
        }

        /// <summary>[P19] 플레이어가 실내 씬으로 옮겨진 뒤 호출 — 월드 씬 언로드(렉 해소 핵심).</summary>
        private static void MaybeUnloadWorldScene()
        {
            if (_worldUnloaded || string.IsNullOrEmpty(_worldSceneName)) return;
            if (_worldSceneName == INDOOR_SCENE_NAME) return;

            Scene world = SceneManager.GetSceneByName(_worldSceneName);
            if (!world.isLoaded) { _worldUnloaded = true; return; }

            SceneManager.sceneUnloaded += OnWorldSceneUnloaded;
            SceneManager.UnloadSceneAsync(world);
            Debug.Log($"[IndoorSceneTransition] 월드 언로드 시작 — '{_worldSceneName}' (실내 단독 구동, 렉 해소)");
        }

        private static void OnWorldSceneUnloaded(Scene scene)
        {
            if (scene.name != _worldSceneName) return;
            SceneManager.sceneUnloaded -= OnWorldSceneUnloaded;
            _worldUnloaded = true;
            Debug.Log($"[IndoorSceneTransition] 월드 언로드 완료 — 실내 단독 구동 중 (병사/몬스터는 재입장 시 자동 재스폰)");
        }

        /// <summary>[P19] IndoorEnterRunner가 플레이어 이동을 마친 뒤 호출(지연 스폰 경로).</summary>
        public static void OnPlayerSettledIndoor()
        {
            ReleasePlayerHolder();
            MaybeUnloadWorldScene();
        }

        /// <summary>
        /// 건물 퇴출. [P19] 월드가 언로드된 경우 월드를 재로드하고 플레이어를 복귀 위치로 되돌린 뒤
        /// IndoorScene을 언로드한다. 월드가 아직 살아있으면(폴백) 기존 Additive 복귀 경로.
        /// </summary>
        public static void ExitBuilding()
        {
            if (string.IsNullOrEmpty(_previousSceneName) || string.IsNullOrEmpty(_worldSceneName))
            {
                Debug.LogWarning("[IndoorSceneTransition] 복귀 씬 이름이 없음. 기본 씬(WorldScene)으로 복귀.");
                _previousSceneName = DEFAULT_WORLD_SCENE;
                _worldSceneName = DEFAULT_WORLD_SCENE;
            }

            Scene worldScene = SceneManager.GetSceneByName(_worldSceneName);

            if (_worldUnloaded || !worldScene.isLoaded)
            {
                // [P19] 월드 재로드 → 로드 완료 콜백에서 플레이어 복귀 + Indoor 언로드
                Debug.Log($"[IndoorSceneTransition] 월드 재로드 시작 — '{_worldSceneName}'");
                SceneManager.sceneLoaded += OnWorldSceneReloaded;
                SceneManager.LoadSceneAsync(_worldSceneName, LoadSceneMode.Additive);
                return;
            }

            // 폴백: 월드가 아직 살아있음 — 기존 즉시 복귀 경로
            FinishExit(worldScene);
        }

        private static void OnWorldSceneReloaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != _worldSceneName) return;
            SceneManager.sceneLoaded -= OnWorldSceneReloaded;
            Debug.Log($"[IndoorSceneTransition] 월드 재로드 완료 — '{scene.name}' (병사/몬스터 씬 셋업 자동 재스폰)");
            FinishExit(scene);
        }

        /// <summary>퇴출 공통 마무리 — 활성화/플레이어 복귀/Indoor 언로드/플래그 정리.</summary>
        private static void FinishExit(Scene worldScene)
        {
            SceneManager.SetActiveScene(worldScene);

            // 플레이어 복귀 위치 복원 + 월드 씬으로 이동
            var exitingPlayer = GameObject.FindGameObjectWithTag("Player");
            if (exitingPlayer == null)
                exitingPlayer = UnityEngine.Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>()?.gameObject;
            if (exitingPlayer != null)
            {
                if (_returnPosition.HasValue)
                    exitingPlayer.transform.position = _returnPosition.Value;
                SceneManager.MoveGameObjectToScene(exitingPlayer, worldScene);
            }

            // [41차] 출동 병사 배치 — 역할 부여된 실내 병사를 문밖(입장 위치)에 재스폰하고
            //   역할 루틴을 재개한다(Attack=추종/Defend=영지 안 수비/Farm·Gather·Mine·Hunt=주변 작업).
            //   IndoorScene 언로드 전 스냅샷 → 월드 활성화 후 스폰 순서.
            Vector3 gatePos = exitingPlayer != null
                ? exitingPlayer.transform.position
                : (_returnPosition ?? Vector3.zero);
            ProjectName.Systems.AttackSquadDeployment.CaptureFromIndoor(gatePos);

            _returnPosition = null;
            _activeCastleTerritoryKey = null;
            _activeCastleNationStyle = null;
            _activeCastleIsPlayerOwned = false;
            ProjectName.Core.UITransitionState.IndoorCastleTerritoryKey = null;   // [152차 P1-수리] Core 공용 키 정리

            ProjectName.Systems.AttackSquadDeployment.SpawnAllInWorld();

            // IndoorScene 언로드 — 플레이어가 이미 월드로 이동한 뒤라 안전
            Scene indoorScene = SceneManager.GetSceneByName(INDOOR_SCENE_NAME);
            if (indoorScene.isLoaded)
                SceneManager.UnloadSceneAsync(INDOOR_SCENE_NAME);
            else
                Debug.LogWarning("[IndoorSceneTransition] 언로드할 IndoorScene이 없음.");

            // 카메라 복귀 — 월드 메인 카메라 재활성(IndoorCamera는 씬 언로드로 자동 제거)
            var mainCamGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCamGO != null) mainCamGO.SetActive(true);

            _previousSceneName = null;
            _worldSceneName = null;
            _worldUnloaded = false;
            IsIndoor = false;   // [P14] 월드 복귀 — AI 게이트 OFF
            ProjectName.Core.UITransitionState.IndoorActive = false;
            ProjectName.Core.UITransitionState.IndoorCastleTerritoryKey = null;   // [152차 P1-수리] 월드 복귀 — Core 공용 키 정리

            // [카메라통일 A4] 실외 복귀 — 진입 직전 PM 카메라 거리 복원(동일 공개 프로퍼티 경로, 1회성).
            if (_preIndoorCamDistance >= 0f)
            {
                var pmForCam = UnityEngine.Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>();
                if (pmForCam != null) pmForCam.CameraDistance = _preIndoorCamDistance;
                _preIndoorCamDistance = -1f;
            }

            // [F4] 실내 점령/상실로 펜딩된 월드 시각 요소(깃발/파티클/야외 크래프트 스테이션) 일괄 적용
            ProjectName.Systems.TerritoryCaptureSystem.FlushPendingVisuals();

            Debug.Log("[IndoorSceneTransition] 퇴출 완료 — 월드 복귀, 위치 복원");
        }

        /// <summary>
        /// 현재 로드된 건물 유형 반환 (테스트 및 디버깅용).
        /// </summary>
        public static string GetPendingBuildingType() => _pendingBuildingType;

        /// <summary>
        /// 이전 씬 이름 반환 (테스트 및 디버깅용).
        /// </summary>
        public static string GetPreviousSceneName() => _previousSceneName;

        /// <summary>
        /// IndoorScene이 현재 Additive 로드되어 있는지 확인합니다.
        /// </summary>
        public static bool IsIndoorSceneLoaded()
        {
            Scene scene = SceneManager.GetSceneByName(INDOOR_SCENE_NAME);
            return scene.isLoaded;
        }

        /// <summary>[P17-D] 실내 고품질 표면 적용 — Room 탐색 → 머티리얼 교체 + 짚단 데칼 + 웜 조명.</summary>
        private static void ApplyHighQualityInterior(GameObject room, string buildingType)
        {
            // [125차 후속22 F3] 투기장(arenapit)은 야외 경기장 — 실내 HQ 체계(짚 데칼/촛불 웜광/석재 벽 팩토리) 전면 스킵.
            //   근거(후속22 진단): HQ가 투기장에 "Floor" 자식 부재로 기본 12×4×10 치수로 개입,
            //   빌더 앰비언트 0.17을 (0.06,0.05,0.04)로 재짓밟음(후속12 앰비언트 덮어쓰기 패턴의 HQ 경로 재발),
            //   짚 데칼 9개가 모래 속에 매장(y=0.012 < 모래 톱 0.15) — 야외 경기장과 실내 HQ 체계 불일치.
            if (!string.IsNullOrEmpty(buildingType) &&
                buildingType.Equals("arenapit", System.StringComparison.OrdinalIgnoreCase)) return;

            if (!ProjectName.Core.IndoorTextureLoader.HasFiles) return;

            if (room == null)
            {
                // [P20-6] 빌더 실패 확정 — 셸 폴백은 ExitBuilding 직전 단계에서만 생성(위에서 처리)
                Debug.LogError("[IndoorSceneTransition][P20-6] HQ 미적용 — 빌더 Room NULL (buildingType/빌더 로그 확인)");
                return;
            }

            // 방 크기 실측 — Floor/벽 렌더러 바운드
            var floorR = room.transform.Find("Floor");
            float w = 12f, d = 10f, h = 4f;
            if (floorR != null)
            {
                var b = floorR.GetComponent<MeshRenderer>();
                if (b != null)
                {
                    w = b.bounds.size.x;
                    d = b.bounds.size.z;
                    var wallR = room.transform.Find("Wall_Front");
                    if (wallR != null)
                    {
                        var wr = wallR.GetComponent<MeshRenderer>();
                        if (wr != null) h = wr.bounds.size.y;
                    }
                }
            }

            // [125차 후속32 T3] 아레나 로비(arena) — 중세 주점 팔레트(tavern_* 세트). 성 내부는 기존 HQ 유지(불변 계약),
            //   arenapit(외부 투기장)은 상단 조기 반환(실외 계약). tavern 세트 부재 시 기존 경로 폴백(팩토리 내부 가드).
            bool isArenaLobby = !string.IsNullOrEmpty(buildingType) &&
                buildingType.Equals("arena", System.StringComparison.OrdinalIgnoreCase);
            if (isArenaLobby && ProjectName.Core.IndoorTextureLoader.HasTavernFiles)
            {
                ProjectName.Systems.IndoorMaterialFactory.ApplyTavernToRoom(room, w, h, d);
            }
            else
            {
                ProjectName.Systems.IndoorMaterialFactory.ApplyToRoom(room, w, h, d);
                ScatterStrawDecals(room, w, d);
            }

            // [126차] 모서리 고품질화 — 벽-벽/벽-바닥 모서리 AO 데칼(결정론, 렌더러 전용, 지오메트리 무변경)
            ProjectName.Systems.IndoorCornerAO.Apply(room, w, h, d);

            // 조명 강화 — 예시 분위기(촛불 웜톤 2000K + 어두운 앰비언트)
            //   [125차 후속22 F2] 빌더 조명 존재 시 HQ 앰비언트/웜광 개입 금지 — 빌더 소유 계약(후속12) 확장.
            //   근거(후속22 진단): HQ가 빌더 앰비언트(투기장 0.17·로비 0.38)를 항상 (0.06,0.05,0.04)로 되돌려
            //   빌더 조명 수리를 무력화했었다(로비는 ArenaFillSun 방향광이 버틴 것, 투기장은 그마저 밤엔 부족).
            //   셸 폴백 등 조명 없는 룸에서만 HQ가 앰비언트+웜광을 세팅(기존 동작 유지).
            bool roomHasLights = room != null && room.GetComponentInChildren<Light>(true) != null;
            if (!roomHasLights)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.06f, 0.05f, 0.04f);   // 주변광 대폭 축소, 조명이 입힌다
                ProjectName.Systems.IndoorLighting.AddPointLight(room, new Vector3(w * 0.3f, 2.6f, d * 0.25f),
                    new Color(1.00f, 0.66f, 0.36f), Mathf.Max(w, d) * 0.9f, 1.2f);
                ProjectName.Systems.IndoorLighting.AddPointLight(room, new Vector3(-w * 0.3f, 2.6f, -d * 0.25f),
                    new Color(1.00f, 0.62f, 0.30f), Mathf.Max(w, d) * 0.8f, 1.0f);
            }
            EnableUrpSoftShadows();
            Debug.Log($"[IndoorSceneTransition] HQ 실내 적용 — 방 {w:F1}x{h:F1}x{d:F1}" +
                      (roomHasLights ? " · 빌더 조명 존재 — 앰비언트/웜광 스킵 [125차 후속22 F2]" : ", 웜 조명 2등"));
        }

        /// <summary>[125차 후속22 F1] 투기장 렌더 감사 — 1회성. 활성 카메라 전수(cullingMask/targetTexture 포함)+
        ///   핵심 지형 렌더러(모래/에이프런/벽/발광 화염) 실측(enabled·layer·셰이더·bounds)+RenderSettings+플레이어 좌표.
        ///   근거: 영상9에서 발광 화염(emission)조차 미렌더 = 조명이 아닌 렌더 체계 문제 → 이 덤프로 뿌리 즉시 판별.</summary>
        private static bool _pitAuditLogged;
        private static ProjectName.Systems.ArenaSpectatorCameraLock _spectatorLock;   // [125차 후속25 R3-3] 관전 카메라 소유 락
        private static void AuditPitRendering(GameObject room)
        {
            if (_pitAuditLogged || room == null) return;
            _pitAuditLogged = true;
            try
            {
                var sb = new System.Text.StringBuilder();
                var amb = RenderSettings.ambientLight;
                sb.AppendLine("[IndoorSceneTransition] [125차 후속22 F1] 투기장 렌더 감사 ─────────────");
                sb.AppendLine($"  RenderSettings: ambientMode={RenderSettings.ambientMode} ambient=({amb.r:F2},{amb.g:F2},{amb.b:F2}) fog={RenderSettings.fog}(density {RenderSettings.fogDensity:F4})");
                foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                {
                    sb.AppendLine($"  CAM '{cam.name}' tag={cam.tag} goActive={cam.gameObject.activeInHierarchy} enabled={cam.enabled} depth={cam.depth} " +
                                  $"ortho={cam.orthographic}/{cam.orthographicSize:0.0} pos={cam.transform.position} " +
                                  $"mask=0x{cam.cullingMask:X8} clear={cam.clearFlags} tex={(cam.targetTexture != null ? cam.targetTexture.name : "-")}");
                }
                var renderers = room.GetComponentsInChildren<Renderer>(true);
                int activeCount = 0;
                foreach (var r in renderers) if (r.enabled && r.gameObject.activeInHierarchy) activeCount++;
                sb.AppendLine($"  ROOM '{room.name}' active={room.activeInHierarchy} children={room.transform.childCount} renderers={renderers.Length}(활성 {activeCount})");
                string[] keys = { "ArenaPit_Stage", "ArenaPit_Apron", "ArenaPit_WallSeg_0", "ArenaPit_WallSeg_1", "ArenaPit_TorchFlame_34" };
                foreach (var key in keys)
                {
                    var t = room.transform.Find(key);
                    if (t == null) { sb.AppendLine($"  KEY '{key}' = 미발견"); continue; }
                    var r = t.GetComponent<Renderer>();
                    var mat = r != null ? r.sharedMaterial : null;
                    sb.AppendLine($"  KEY '{key}' goActive={t.gameObject.activeInHierarchy} rend={(r != null ? r.enabled.ToString() : "무렌더러")} " +
                                  $"layer={LayerMask.LayerToName(t.gameObject.layer)}({t.gameObject.layer}) " +
                                  $"shader={(mat != null && mat.shader != null ? mat.shader.name : "NULL")} " +
                                  $"bounds={(r != null ? r.bounds.center.ToString("F1") + " / " + r.bounds.size.ToString("F1") : "-")}");
                    // [125차 후속25] 머티리얼 프로브 — _BaseColor/텍스처 평균 밝기(검은 텍스처 가설 직접 판정)/발광 실측
                    if (mat != null)
                    {
                        var bc = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                        bool emis = mat.IsKeywordEnabled("_EMISSION") && mat.HasProperty("_EmissionColor");
                        sb.AppendLine($"    MAT base=({bc.r:F2},{bc.g:F2},{bc.b:F2}) tex={RenderAuditProbe.DescribeTexture(mat.mainTexture)}" +
                                      (emis ? $" emission={mat.GetColor("_EmissionColor")}" : ""));
                    }
                }
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) sb.AppendLine($"  PLAYER pos={player.transform.position} active={player.activeInHierarchy}");
                Debug.Log(sb.ToString());
                // [125차 후속25] 결정 감사 v2 — RT 캡처+씬 루트 전수+라이트 전수+포그 강제원 덤프(별도 로그)
                RenderAuditProbe.Run("pit");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[IndoorSceneTransition] [125차 후속22 F1] 렌더 감사 실패: {e.Message}");
            }
        }

        /// <summary>[P17-D] 짚단/약초 데칼 산포 — 결정론 시드(djb2, 재방문 동일 배치), 지면 위 0.01.</summary>
        private static void ScatterStrawDecals(GameObject room, float width, float depth)
        {
            var tex = ProjectName.Core.IndoorTextureLoader.StrawDecal;
            if (tex == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;

            uint hash = 5381u;
            foreach (char c in room.GetInstanceID().ToString())
                hash = ((hash << 5) + hash) + (uint)(c & 0x7F);

            const int Count = 9;
            for (int i = 0; i < Count; i++)
            {
                hash = ((hash << 5) + hash) + (uint)i;
                float fx = ((hash >> 8) & 0xFF) / 255f;   // 0..1 결정론
                float fz = ((hash >> 16) & 0xFF) / 255f;
                float rot = ((hash >> 24) & 0x03) * 90f;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "StrawDecal_" + i;
                UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(room.transform);
                quad.transform.localPosition = new Vector3(
                    Mathf.Lerp(-width * 0.42f, width * 0.42f, fx),
                    0.012f,
                    Mathf.Lerp(-depth * 0.42f, depth * 0.42f, fz));
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, rot);
                float s = ProjectName.Core.IndoorTextureLoader.DecalSizeMeters;
                quad.transform.localScale = new Vector3(s, s, 1f);

                var m = new Material(shader) { name = "IndoorHQ_Straw" };
                m.mainTexture = tex;
                var mr = quad.GetComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // 투명 데칼 — 렌더 큐 투명, z테스트 유지
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
            }
            Debug.Log("[IndoorSceneTransition] 짚단 데칼 9장 산포 완료");
        }

        /// <summary>[P17-B] URP 소프트 섀도우 ON — 예시의 부드러운 그림자(런타임 1회, 실패 무해).</summary>
        private static void EnableUrpSoftShadows()
        {
            try
            {
                var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                if (asset == null) return;
                // [P17-B] softShadowsSupported는 버전별 API 차이 — 리플렉션 세팅(실패 무해)
                var prop = asset.GetType().GetProperty("softShadowsSupported");
                if (prop != null && prop.CanWrite && prop.GetValue(asset) is bool on && !on)
                    prop.SetValue(asset, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[IndoorSceneTransition] URP 소프트 섀도우 설정 실패(무해): " + e.Message);
            }
        }

        /// <summary>
        /// 성 내부 가구 배치 변형 — [무번호 15 — 실내 구조 통일] 단일 변형 고정(0).
        /// 기존 djb2 해시 0..7 분할은 폐기 — 사용자 지시 "모든 영지 실내 가구 배치도 동일".
        /// 영지 키/nation/소유 인자는 시그니처 호환용으로 유지(호출부 무수정)하고 결정론 계약 유지.
        /// </summary>
        private static int ComputeLayoutVariant(string territoryKey, string nation, bool isPlayerOwned)
        {
            return 0;
        }
    }
}
