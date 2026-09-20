using ProjectName.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Systems;

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
        private static Vector3? _returnPosition;      // 진입 직전 플레이어 위치(퇴출 복귀용)
        private const float INDOOR_FLOOR_Y = 0f;      // 실내 바닥 높이 (IndoorBuilder.CreateRoom: 바닥 XZ 평면 y=0)
        private static bool _initialized;

        /// <summary>[P14] 실내 활성 여부 — 병사/몬스터 AI가 플레이어 추적/어그로를 실내로 끌고 오는 것 차단.
        ///   Systems 측 AI 루프가 이 플래그를 게이트로 읽는다(Update 초입 정지).</summary>
        public static bool IsIndoor { get; private set; }

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
            // 현재 씬 저장
            _previousSceneName = SceneManager.GetActiveScene().name;
            _pendingBuildingType = buildingType;
            _pendingNationStyle = nationStyle;
            _pendingIsPlayerOwned = isPlayerOwned;
            _pendingTerritoryKey = territoryKey;

            // 진입 직전 플레이어 위치 저장(퇴출 시 복귀)
            var enteringPlayer = GameObject.FindGameObjectWithTag("Player");
            _returnPosition = enteringPlayer != null ? (Vector3?)enteringPlayer.transform.position : null;



            // 이미 IndoorScene이 로드되어 있으면 언로드 후 재로드
            Scene indoorScene = SceneManager.GetSceneByName(INDOOR_SCENE_NAME);
            if (indoorScene.isLoaded)
            {
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

            string buildingType = _pendingBuildingType ?? string.Empty;


            // IndoorScene을 활성 씬으로 설정
            SceneManager.SetActiveScene(scene);

            // buildingType에 따라 적절한 Builder 호출
            switch (buildingType.ToLower())
            {
                case "crafthouse":
                    CraftHouseInteriorBuilder.BuildCraftHouseInterior();
                    break;
                case "church":
                    ChurchInteriorBuilder.BuildChurchInterior();
                    break;
                case "house":
                case "npchouse":
                    HouseInteriorBuilder.BuildHouseInterior();
                    break;
                case "castle":
                    string nation = _pendingNationStyle ?? "Empire";
                    // INTERIOR-VAR: 영지 키(우선)/nation+소유로 결정론 해시 → 8종 레이아웃 변형.
                    // 같은 영지 재방문 시 항상 같은 배치(결정론), 다른 영지는 다른 배치.
                    int layoutVariant = ComputeLayoutVariant(_pendingTerritoryKey, nation, _pendingIsPlayerOwned);
                    // 소유 상태 분기: 플레이어 소유 성 → PlayerCastleInteriorBuilder, 영주 성 → CastleInteriorBuilder
                    GameObject interior = _pendingIsPlayerOwned
                        ? PlayerCastleInteriorBuilder.BuildPlayerCastleInterior(nation, layoutVariant)
                        : CastleInteriorBuilder.BuildCastleInterior(nation, layoutVariant);
                    if (interior != null)
                        TerritoryBuilder.SpawnInteriorFixtures(interior.transform.position, nation);
                    break;
                case "barn":
                    BarnInteriorBuilder.BuildBarnInterior();
                    break;
                case "shop":
                    ShopInteriorBuilder.BuildShopInterior();
                    break;
                case "cave":
                    CaveInteriorBuilder.BuildCaveInterior(_pendingNationStyle ?? "default", 1);
                    break;
                default:
                    Debug.LogWarning($"[IndoorSceneTransition] 알 수 없는 buildingType: '{buildingType}'. 기본 주택 생성.");
                    HouseInteriorBuilder.BuildHouseInterior();
                    break;
            }

            // [P17-D] 고품질 실내 표면 적용 — 제공 심리스 텍스처(Resources/Indoor/) 있으면
            //   바닥/벽(석재+회반죽 투톤)/짚단 데칼/웜 조명으로 교체. 없으면 절차 생성 유지(폴백).
            ApplyHighQualityInterior();

            // 플레이어를 내부 원점으로 이동(카메라는 플레이어 추적 유지) — builders는 원점 부근에 내부 생성
            var indoorPlayer = GameObject.FindGameObjectWithTag("Player");
            if (indoorPlayer == null)
                indoorPlayer = UnityEngine.Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>()?.gameObject;
            if (indoorPlayer != null)
                indoorPlayer.transform.position = new Vector3(0f, INDOOR_FLOOR_Y + 0.5f, 0f); // 여유 높이(바닥 콜라이더 위)

            // 2026-09-09(6차): 플레이어 하이어라키를 IndoorScene으로 이동 — 계층 표시+활성 씬 정합
            if (indoorPlayer != null && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(indoorPlayer, scene);
                Debug.Log($"[IndoorSceneTransition] 플레이어 이동 완료 → 소속 씬: {indoorPlayer.scene.name} / pos: {indoorPlayer.transform.position}");
            }
            else
            {
                // 2026-09-09(7차 FIX): sceneLoaded 콜백 시점에 플레이어가 아직 없음(로딩 타이밍) →
                // 지연 재시도 러너가 다음 프레임부터 플레이어를 찾아 IndoorScene 이동+스폰
                Debug.LogError("[IndoorSceneTransition] 플레이어 미발견 — 지연 재시도 러너 가동");
                var runnerGO = new GameObject("IndoorEnterRunner");
                runnerGO.AddComponent<IndoorEnterRunner>().Init(scene);
            }

            // 2026-09-09(5차→6차 FIX): 셸은 씬에 상주하지만 구버전 씬/에디터 메모리 상태 대비 런타임 폴백 생성
            if (GameObject.Find("MedievalShell") == null)
                ProjectName.Systems.MedievalShellBuilder.CreateShell();

            // 2026-09-09(6차 FIX): IndoorCamera가 존재할 때만 메인 카메라 비활성 — 없으면 메인 카메라(플레이어 추적) 유지
            var mainCamGO = GameObject.FindGameObjectWithTag("MainCamera");
            var indoorCam = GameObject.Find("IndoorCamera");
            if (indoorCam != null && indoorCam.activeInHierarchy)
            {
                if (mainCamGO != null) mainCamGO.SetActive(false);
            }
            else if (mainCamGO != null && !mainCamGO.activeSelf)
            {
                mainCamGO.SetActive(true); // 이전 진입 실패 복구
            }

            // 2026-09-09(3차 FIX): 까만 화면 방지 — 실내 앰비언트를 따뜻한 플랫톤으로
            // (활성 씬이 IndoorScene이므로 RenderSettings는 실내 것만 적용, 복귀 시 자동 원복)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.38f, 0.30f);

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

        /// <summary>
        /// 건물 퇴출. "IndoorScene"을 Additive 씬에서 언로드하고 이전 씬으로 복귀합니다.
        /// </summary>
        public static void ExitBuilding()
        {
            if (string.IsNullOrEmpty(_previousSceneName))
            {
                Debug.LogWarning("[IndoorSceneTransition] 이전 씬 이름이 없음. 기본 씬(WorldScene)으로 복귀.");
                _previousSceneName = DEFAULT_WORLD_SCENE;
            }



            // 이전 씬을 활성화
            Scene prevScene = SceneManager.GetSceneByName(_previousSceneName);
            if (prevScene.isLoaded)
            {
                SceneManager.SetActiveScene(prevScene);
            }

            // IndoorScene Additive 언로드
            Scene indoorScene = SceneManager.GetSceneByName(INDOOR_SCENE_NAME);
            if (indoorScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(INDOOR_SCENE_NAME);
            }
            else
            {
                Debug.LogWarning("[IndoorSceneTransition] 언로드할 IndoorScene이 없음.");
            }

            // 진입 직전 위치로 플레이어 복귀
            var exitingPlayer = GameObject.FindGameObjectWithTag("Player");
            if (exitingPlayer == null)
                exitingPlayer = UnityEngine.Object.FindAnyObjectByType<ProjectName.Systems.PlayerMovement>()?.gameObject;
            if (exitingPlayer != null && _returnPosition.HasValue)
                exitingPlayer.transform.position = _returnPosition.Value;
            _returnPosition = null;

            // 2026-09-09(6차): 플레이어를 다시 메인 씬으로 이동(하이어라키 복귀)
            if (exitingPlayer != null && prevScene.isLoaded)
                SceneManager.MoveGameObjectToScene(exitingPlayer, prevScene);

            // 2026-09-09(5차): 카메라 복귀 — 메인 카메라만 재활성(IndoorCamera는 씬 언로드로 자동 제거)
            var mainCamGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCamGO != null) mainCamGO.SetActive(true);

            _previousSceneName = null;
            IsIndoor = false;   // [P14] 월드 복귀 — AI 게이트 OFF
            ProjectName.Core.UITransitionState.IndoorActive = false;
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
        private static void ApplyHighQualityInterior()
        {
            if (!ProjectName.Core.IndoorTextureLoader.HasFiles) return;

            var room = GameObject.Find("Room");
            if (room == null)
            {
                Debug.LogWarning("[IndoorSceneTransition] HQ 적용 실패 — Room 없음");
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

            ProjectName.Systems.IndoorMaterialFactory.ApplyToRoom(room, w, h, d);
            ScatterStrawDecals(room, w, d);

            // 조명 강화 — 예시 분위기(촛불 웜톤 2000K + 어두운 앰비언트)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.17f, 0.13f);   // 기존 0.45 → 어둡게, 조명이 입힌다
            ProjectName.Systems.IndoorLighting.AddPointLight(room, new Vector3(w * 0.3f, 2.6f, d * 0.25f),
                new Color(1.00f, 0.66f, 0.36f), Mathf.Max(w, d) * 0.9f, 1.2f);
            ProjectName.Systems.IndoorLighting.AddPointLight(room, new Vector3(-w * 0.3f, 2.6f, -d * 0.25f),
                new Color(1.00f, 0.62f, 0.30f), Mathf.Max(w, d) * 0.8f, 1.0f);
            EnableUrpSoftShadows();
            Debug.Log($"[IndoorSceneTransition] HQ 실내 적용 — 방 {w:F1}x{h:F1}x{d:F1}, 웜 조명 2등");
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
        /// 성 내부 8종 레이아웃 변형 결정론 시드 계산 (INTERIOR-VAR).
        /// 영지 키(우선) 또는 nation+소유조로 djb2 문자열 해시 → 0..7.
        /// Random/시간 미사용 — 같은 입력 → 항상 같은 variant (재방문 시 동일 배치).
        /// </summary>
        private static int ComputeLayoutVariant(string territoryKey, string nation, bool isPlayerOwned)
        {
            string seedStr;
            if (!string.IsNullOrEmpty(territoryKey))
            {
                seedStr = territoryKey;
            }
            else
            {
                seedStr = (nation ?? "Empire") + (isPlayerOwned ? "_P" : "_L");
            }

            // djb2 해시 (결정론, GC 추가 할당 없음)
            uint hash = 5381u;
            foreach (char c in seedStr)
            {
                hash = ((hash << 5) + hash) + (uint)(c & 0x7F);
            }
            return (int)(hash % 8u);
        }
    }
}
