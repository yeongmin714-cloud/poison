using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 2026-10-23 (120차): 아레나 설계도 재구축 검증 전용 테스트 씬 부트스트랩.
    /// 빈 씬 이름을 "Test_18_Arena"로 만들고 Play하면 자동 구성:
    /// 야외 스테이징 + [125차 후속] 진입 즉시 로비(여관 설계도) 자동 전환
    /// → 관리인 F키 → 베팅 확정 시 자동 투기장 전환 → 출구 E키 복귀(야외 스테이징은 복귀용으로 유지).
    /// InteriorSystemsTestSetup 선례와 동일 패턴(씬 이름 게이트 + Runner).
    /// </summary>
    public static class ArenaTestSetup
    {
        private const string TestSceneName = "Test_18_Arena";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != TestSceneName) return;
            if (Object.FindAnyObjectByType<ArenaTestRunner>() != null) return;

            var go = new GameObject("ArenaTestRunner");
            go.AddComponent<ArenaTestRunner>();
        }
    }

    public class ArenaTestRunner : MonoBehaviour
    {
        private void Awake()
        {
            // [125차 후속6] 중복 러너 가드 — 실내 복귀 등 재진입 시 기존 DDOL 러너를 재사용.
            // (Bootstrap에서 AddComponent 후 반환 컴포넌트를 별도로 사용하지 않으므로 여기서 파괴해도 NRE 없음)
            var existingRunner = UnityEngine.Object.FindObjectOfType<ArenaTestRunner>();
            if (existingRunner != null && existingRunner != this)
            {
                Destroy(gameObject);   // [125차 후속6] 실내 복귀 등 재진입 시 DDOL 기존 러너 재사용 — 중복 안내문/카메라 방지
                return;
            }
            UnityEngine.Object.DontDestroyOnLoad(gameObject);   // [125차 후속6] P19 월드 언로드(Test_18_Arena 언로드)에서 러너·안내문 생존

            SetupGround();
            SetupStagingDeco();   // [125차 후속39] 스테이징 Idyllic 데코 산포 — Ground 자식 부착(N1 토글 계약)
            SetupUIManager();
            SetupCamera();
            SetupPlayer();
            SetupArenaEntry();
            SeedGold();
            // [125차 후속23 F2] 스테이징(야외) 포그 해제 — 포그=실내씬 전용 계약(사용자 지시).
            RenderSettings.fog = false;
            Debug.Log("[ArenaTest] 구성 완료 — 진입 즉시 로비 → 관리인 F키 → 베팅 → 투기장 자동 전환 (야외 스테이징은 복귀용으로 유지)");
            // [125차 후속25] 스테이징 시점 1회 감사 — RT 캡처+포그/라이트/씬 실측(로비 자동전환 0.3s 이전에 캡처)
            Invoke(nameof(StagingAuditDeferred), 0.15f);
            // [125차 후속] 진입 즉시 로비 자동 전환 — 0.3s 지연은 UIRoot/UIManager/플레이어 셋업
            // 완료 후 EnterBuilding(씬 로드 수반)을 안전히 호출하기 위한 근접 지연(실측 후 0.2~0.5s 범위 조정 가능).
            Invoke(nameof(EnterLobbyDeferred), 0.3f);
        }

        /// <summary>[125차 후속25] 스테이징 결정 감사 — 사용자가 본 "포그"가 스테이징인지 투기장인지 분리.</summary>
        private void StagingAuditDeferred()
        {
            RenderAuditProbe.Run("staging");
        }

        // [125차 후속] 진입 즉시 로비 — 야외 스테이징 생략(E키 트리거와 동일 진입 경로).
        private bool _autoEntered;
        private void EnterLobbyDeferred()
        {
            if (_autoEntered) return;
            _autoEntered = true;
            IndoorSceneTransition.EnterBuilding(IndoorTransitionSetup.TYPE_ARENA, "Western");
            // [125차 후속10] 실내 진입 후 골드 보장 — PlayerStats는 씬 바운드(Core/PlayerStats.cs: DDOL 제거 계약)라
            //   스테이징(Test_18_Arena) 언로드 시 소멸 → 실내(로비)에서 베팅 금액이 0이 되는 문제 방지.
            //   1.0s 지연 = IndoorScene 로드+빌더 완료 후 안전 재시딩(대상 금액 미달 시 델타만 추가 — 멱등).
            Invoke(nameof(EnsureLobbyGold), 1.0f);
            Debug.Log("[ArenaTest] 진입 즉시 로비 전환 요청 — 아레나(여관) 실내");
        }

        /// <summary>야외 스테이징 지면(120×120) — 로비 진입 전 대기 공간.</summary>
        private void SetupGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(12f, 1f, 12f); // 120x120m — [125차 후속39] 외부 스테이징 확장
            // [125차 후속39] 메인씬 Idyllic 잔디 지면 재사용(TerrainTextureApplier 선례: east_grass1 텍스처).
            var rend = ground.GetComponent<Renderer>();
            var grassMat = Resources.Load<Material>("URP/Ground_Grass_Mat");
            if (grassMat != null)
            {
                var inst = new Material(grassMat);   // 런타임 인스턴스 — 에셋 무손상(T-G5-W2 선례)
                inst.mainTextureScale = new Vector2(20f, 20f);   // 120m/6m=20타일 — 원본 6m 타일 밀도 유지
                rend.sharedMaterial = inst;
            }
            else
            {
                rend.sharedMaterial.color = new Color(0.35f, 0.32f, 0.28f);   // 폴백 계약 — 리소스 부재 시 기존 갈색 유지
            }
            _ground = ground;   // [125차 후속33 N1] 스테이징 전용 리소스 — 실내(로비/투기장)에서 비활성 토글 대상

            // [125차 후속] 복귀용 스테이징 가시성 — Directional Light가 없으면 1개 생성(복귀 시 검은 화면 방지).
            bool hasDirectionalLight = false;
            foreach (var l in Object.FindObjectsByType<Light>())
            {
                if (l != null && l.type == LightType.Directional) { hasDirectionalLight = true; break; }
            }
            if (!hasDirectionalLight)
            {
                var lightGO = new GameObject("StagingDirectionalLight");
                var dirLight = lightGO.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.intensity = 1.0f;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                _stagingLight = lightGO;   // [125차 후속33 N1] — 실내 촛불 웜광 분위기 보존(같은 스테이징 전용 리소스)
            }
        }

        /// <summary>[125차 후속39] 스테이징 Idyllic 데코 산포 — 결정론 seed로 링 r∈[24,54]m 원점 중심 배치.
        ///   전부 Ground 자식으로 부착(후속33 N1 토글 계약: 실내 토글 시 데코가 함께 비활성 — 별도 루트 금지).</summary>
        private void SetupStagingDeco()
        {
            var ground = _ground;
            if (ground == null) return;

            // [125차 후속39] 결정론 seed 계약 — 매 실행 동일 배치(회귀 재현 가능).
            System.Random rng = new System.Random(18261);

            var trees = new[]
            {
                Resources.Load<GameObject>("IdyllicPrefabs/Trees/BlossomTree_01"),
                Resources.Load<GameObject>("IdyllicPrefabs/Trees/BlossomTree_04"),
            };
            var bushes = new[]
            {
                Resources.Load<GameObject>("IdyllicPrefabs/Bushes/Bush_01_01"),
                Resources.Load<GameObject>("IdyllicPrefabs/Bushes/Bush_02_01"),
            };
            var rocks = new[]
            {
                Resources.Load<GameObject>("IdyllicPrefabs/Rocks/Rock_Big_01"),
                Resources.Load<GameObject>("IdyllicPrefabs/Rocks/Rock_Big_02"),
            };
            var grass = new[]
            {
                Resources.Load<GameObject>("IdyllicPrefabs/Grass/Grass_01"),
                Resources.Load<GameObject>("IdyllicPrefabs/Grass/Grass_02"),
            };

            // [125차 후속39] 로비 방 실측치 45.6×61.3m(원점 중심) — 데코 배치 금지구역.
            const float BanX = 27f;
            const float BanZ = 35f;
            int placedTrees = 0, placedBushes = 0, placedRocks = 0, placedGrass = 0, skipped = 0;

            void Place(GameObject prefabA, GameObject prefabB, int countA, int countB, string prefix)
            {
                int total = countA + countB;
                for (int i = 0; i < total; i++)
                {
                    var prefab = (i < countA) ? prefabA : prefabB;
                    if (prefab == null) { skipped++; continue; }   // 폴백 계약 — 프리팹 부재 시 예외 없이 스킵

                    Vector3 pos = Vector3.zero;
                    bool ok = false;
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        float r = Mathf.Lerp(24f, 54f, (float)rng.NextDouble());
                        float rad = (float)rng.NextDouble() * 360f * Mathf.Deg2Rad;
                        float x = Mathf.Cos(rad) * r;
                        float z = Mathf.Sin(rad) * r;
                        if (Mathf.Abs(x) < BanX && Mathf.Abs(z) < BanZ) continue;   // 금지구역 히트 → 재표본(항목당 최대 20회)
                        pos = new Vector3(x, 0f, z);
                        ok = true;
                        break;
                    }
                    if (!ok) { skipped++; continue; }

                    var deco = Object.Instantiate(prefab);
                    deco.name = prefix + i;
                    deco.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.15f, (float)rng.NextDouble());   // 스케일 지터
                    deco.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    deco.transform.position = new Vector3(pos.x, 0f, pos.z);   // y=0 접지(평면 톱)
                    deco.transform.SetParent(ground.transform, true);   // Ground 자식 부착(N1 토글 계약) — worldPositionStays로 부모 스케일(12,1,12) 보상
                    if (prefix.Contains("Tree")) placedTrees++;
                    else if (prefix.Contains("Bush")) placedBushes++;
                    else if (prefix.Contains("Rock")) placedRocks++;
                    else placedGrass++;
                }
            }

            Place(trees[0], trees[1], 4, 4, "ArenaStagingDeco_Tree_");     // 나무 8(두 종 4+4)
            Place(bushes[0], bushes[1], 5, 5, "ArenaStagingDeco_Bush_");   // 덤불 10(두 종 5+5)
            Place(rocks[0], rocks[1], 3, 2, "ArenaStagingDeco_Rock_");     // 바위 5(두 종 3+2)
            Place(grass[0], grass[1], 12, 12, "ArenaStagingDeco_Grass_");  // 잔디 24(두 종 12+12)

            Debug.Log($"[ArenaTest] [125차 후속39] 스테이징 Idyllic 데코 배치 — 나무{placedTrees}/덤불{placedBushes}/바위{placedRocks}/잔디{placedGrass}(스킵 {skipped})");
        }

        // [125차 후속33 N1] 스테이징 전용 리소스 가시성 상태 — 실내(로비/투기장)에서 Ground+직사광 비활성
        private GameObject _ground;
        private GameObject _stagingLight;
        private float _roomPollTimer;
        private bool _indoorActiveCache;

        /// <summary>[125차 후속33 N1] 스테이징 전용 리소스 가시성 토글 — ArenaRoom/ArenaPitRoom 존재 시
        ///   Ground(60×60, y=0)+StagingDirectionalLight를 비활성한다. 근거: 로비 Floor(y=0)와 Ground가 동평면
        ///   경합 → Ground가 바닥을 덮아 주점 텍스처가 가려짐(스크린샷 실측 — 로비만 가림, 투기장 모래 y=0.15는
        ///   Ground 위라 정상). 스테이징 복귀(출구 E키) 시 복원. 저비용 폴링(0.5s) — 테스트씬 전용.</summary>
        private void Update()
        {
            _roomPollTimer += Time.deltaTime;
            if (_roomPollTimer < 0.5f) return;
            _roomPollTimer = 0f;
            bool indoor = GameObject.Find("ArenaRoom") != null || GameObject.Find("ArenaPitRoom") != null;
            if (indoor == _indoorActiveCache) return;
            _indoorActiveCache = indoor;
            if (_ground != null) _ground.SetActive(!indoor);
            if (_stagingLight != null) _stagingLight.SetActive(!indoor);
            Debug.Log($"[ArenaTest] [125차 후속33 N1] 스테이징 리소스 토글 — 실내={indoor} → Ground/StagingLight {(!indoor ? "활성" : "비활성")}");
        }

        private void SetupPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.tag = "Player";
            }
            if (player.GetComponent<PlayerMovement>() == null) player.AddComponent<PlayerMovement>();
            if (player.GetComponent<UnityEngine.InputSystem.PlayerInput>() == null)
            {
                var pi = player.AddComponent<UnityEngine.InputSystem.PlayerInput>();
                pi.defaultActionMap = "Player";
                pi.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
            }
            if (player.GetComponent<PlayerCombat>() == null) player.AddComponent<PlayerCombat>();
            if (player.GetComponent<PlayerHealth>() == null) player.AddComponent<PlayerHealth>();
            if (player.GetComponent<PlayerInventory>() == null) player.AddComponent<PlayerInventory>();
            // [125차 후속10] 플레이어 시각+애니 통일 — 다른 테스트셋업 선례(TestPlayerSetup 등 AddComponent<PlayerPlaceholder>)
            //   과 동일 경로. PlayerPlaceholder가 RuntimeModelLoader "player"(Player_Rigged) GLB를 부착하고
            //   ModelAnimatorAssigner(ForceBiped)로 애니메이션까지 배선한다(메인씬과 동일) — 테스트씬 플레이어가
            //   시각 없음/애니 없음(T포즈)이던 근본 수리. 1.8m 키 계약은 PlayerPlaceholder 내부 정규화가 담당.
            if (player.GetComponent<PlayerPlaceholder>() == null) player.AddComponent<PlayerPlaceholder>();
            player.transform.position = new Vector3(0f, 1.5f, -8f);
        }

        // [125차 후속3] 전 테스트씬 공통 탑다운 패턴(IndoorWar/LordInteraction 동일) —
        // 직교 카메라 + IndoorCameraFollow(플레이어 태그 레이지 탐색 → 셋업 순서 무관)로
        // 매 프레임 플레이어 추종. 기존 고정 위치/회전 라인은 폐기(LateUpdate가 배치 주도).
        private void SetupCamera()
        {
            // [125차 후속6] 기존 DDOL 카메라 재사용 — 실내 복귀 등 재진입 시 중복 생성 방지.
            var existingCam = GameObject.Find("ArenaTestCamera");
            if (existingCam != null)
            {
                // IndoorCameraFollow가 이미 구성되어 있으므로 추가 설정 없이 조기 반환.
                return;
            }

            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("ArenaTestCamera"); // [125차 후속6] 카메라 GO 명명
                camGO.tag = "MainCamera";
            }
            else
            {
                camGO.name = "ArenaTestCamera"; // [125차 후속6] 씬 카메라 재활용 시에도 명명 통일
            }
            UnityEngine.Object.DontDestroyOnLoad(camGO);   // [125차 후속6] P19 월드 언로드에서 카메라 생존 — 검은 화면 근본 수리
            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null) cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.orthographic = true;
            cam.orthographicSize = 14f;
            cam.nearClipPlane = 0.1f;
            // [125차 후속20] farClipPlane 120→500 — 타 테스트셋업 계약(TestTimeWeather/TestTerritory/TestPlayer 등 전원 500) 정합.
            //   120은 관전 카메라(수직 26m)+외곽 타원(45.6×47.8m) 수용에 충분하나, 줌아웃 시 카메라 이동/거리 여유 부족 →
            //   타 씬과 동일 계약값으로 통일(아레나 지형 잘림 방지 — 타 테스트셋업과 동일 실측).
            cam.farClipPlane = 500f;
            var follow = camGO.GetComponent<IndoorCameraFollow>();
            if (follow == null)
                follow = camGO.AddComponent<IndoorCameraFollow>();
            // [Test_16] 직교 프레이밍 줌 — 휠이 orthographicSize를 조절(거리 줌은 직교에서 무효).
            follow.ZoomOrthographicSize = true;
            if (camGO.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();
        }

        /// <summary>
        /// [125차 후속9] UIManager 최소 셋 — legacy uGUI 창 시딩 폐기(주씬과 동일 상태).
        /// 주씬은 legacy 창(WarehouseUI/CraftingUI/CookingUI/AlchemyUI/InventoryWindow)을 시딩하지 않고,
        /// 소비처(CraftingStation/TerritoryWarehouse/QuickSlotUI)는 모두 null 가드로 살아있으므로 동작 등가.
        /// 특히 AlchemyUI.CreateButton 환경 특이 NRE(AddComponent가 null 반환 → bg.color NRE,
        /// CreateButton:247 반복)의 유일 소비처가 본 시딩이므로 여기서 근본 제거.
        /// 베팅 흐름은 UTK(ArenaWindowUTK)라 legacy 창 부재 영향 없음.
        /// </summary>
        private void SetupUIManager()
        {
            if (Game.UI.Core.UIManager.Instance != null) return;
            var go = new GameObject("UIManager");
            go.AddComponent<Game.UI.Core.UIManager>();
            // [125차 후속9] legacy 창 시딩 5종 폐기 — NewWindow 제네릭 헬퍼도 소비처 소멸로 함께 폐기(데드코드).
        }

        /// <summary>아레나 진입 트리거 + 시각 마커 — 플레이어 스폰에서 북쪽 4m(E키 상호작용 범위).</summary>
        private void SetupArenaEntry()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "ArenaEntry_Marker";
            marker.transform.position = new Vector3(0f, 0.1f, -4f);
            marker.transform.localScale = new Vector3(4f, 0.2f, 2f);
            var mr = marker.GetComponent<Renderer>();
            if (mr != null) mr.sharedMaterial.color = new Color(0.55f, 0.42f, 0.20f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            IndoorTransitionSetup.CreateArenaTrigger(new Vector3(0f, 0f, -4f), "Western");
            Debug.Log("[ArenaTest] 아레나 트리거 배치 (Western) — E키로 베팅 로비 진입");
        }

        /// <summary>베팅/메뉴 동작 검증용 골드 시딩.</summary>
        private void SeedGold()
        {
            var stats = PlayerStats.Instance ?? FindAnyObjectByType<PlayerStats>();
            if (stats != null) stats.AddGold(5000, "arena_test_seed");   // [125차 후속10] 베팅 여유 금액(500→5000) — 사용자 요구: 배팅할 수 있을 만큼 충분하게
        }

        /// <summary>[125차 후속10] 실내(로비) 골드 보장 — 씬 바운드 PlayerStats 대비 지연 재시딩(멱등: 미달분만 보충).</summary>
        private void EnsureLobbyGold()
        {
            var stats = PlayerStats.Instance ?? FindAnyObjectByType<PlayerStats>();
            if (stats == null)
            {
                var go = new GameObject("PlayerStats");
                stats = go.AddComponent<PlayerStats>();   // 실내 씬에 인스턴스 부재 시 신설(Awake가 Instance 등록)
            }
            const int targetGold = 5000;
            if (stats.Gold < targetGold)
            {
                stats.AddGold(targetGold - stats.Gold, "arena_test_seed_indoor");
                Debug.Log($"[ArenaTest] [125차 후속10] 실내 골드 보장 — {stats.Gold}G (미달분 보충, 대상 {targetGold}G)");
            }
            else
            {
                Debug.Log($"[ArenaTest] [125차 후속10] 실내 골드 보장 — {stats.Gold}G (대상 충족 — 보충 없음)");
            }
        }

        private void OnGUI()
        {
            float w = 640f, h = 30f;
            var r = new Rect(8f, Screen.height - h - 8f, w, h);
            GUI.Box(r, " 아레나 테스트 — 로비(여관) → 관리인 클릭(F키) → 대화하기/베팅하기 → 베팅 확정 시 자동 투기장 전환·전투·정산 → 확인 후 로비 → 남동측 출구 E키 복귀(투기장 문은 장식)");
        }
    }
}
