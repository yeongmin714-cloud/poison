using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// [137차 T2] Test_21_CaveInterior 전용 — 동굴 실내 테스트씬 부트스트래퍼.
    ///
    /// 계획서: docs/137_RESOURCE_TEST_CAVE_PLAN.md §2 T2.
    /// 선례: LordPeaceVisitTestSetup(Test_19) — Awake 최상단 씬 이름 게이트/
    ///   EnsureGameManager/조명 보정(DNC 봉쇄+방향광 0)/OnDestroy 원복 패턴 복제.
    ///
    /// 구성:
    ///   - CaveInteriorBuilder.BuildCaveInterior("cave_test", 2) — 현행 빌더 그대로(무수정 계약).
    ///   - 천장 숨김: CaveCeiling renderer.enabled=false (탑다운 천장 금지 계약 — 원본 파괴 금지).
    ///   - 조명 보정: DNC 봉쇄 + 방향광 0(Test_19 동일 경로). 앰비언트 부스트 ×1.25는 생략 —
    ///     동굴 암흑 유지가 목적(빌더의 (0.02,0.02,0.06)@0.10 앰비언트 존중).
    ///   - 플레이어 스폰: 입구(남측) 위치 — Test_19 EnsurePlayer 선례 준용.
    ///   - 보석상자: GemChest 카운트 로그(4 기대 — 138차 고정 4개 계약).
    ///
    /// 검증 포인트 (Play 게이트):
    ///   ① 탑다운 시점에서 천장이 보이지 않는다(숨김 — 파괴 아님)
    ///   ② 동굴이 어둡고 횟불 7개가 플리커(TorchFlicker)한다
    ///   ③ 보석상자 4개(고정) E키 → 개방 상호작용
    /// </summary>
    public sealed class CaveInteriorTestSetup : MonoBehaviour
    {
        public const string TestSceneName = "Test_21_CaveInterior";

        // [실내 관람 모드] Test_23_IndoorTour도 부트 경로를 재사용 — 씬 게이트 2종 허용(그 외 로직 무수정).
        private const string TourSceneName = "Test_23_IndoorTour";

        // 동굴 생성 인자 — 결정론 시드(territoryId 기반), tier 2(⭐⭐ 보통: 12×3.3×9m).
        private const string CaveTerritoryId = "cave_test";
        private const int CaveTier = 2;
        private const string CeilingChildName = "CaveCeiling";
        private const int LightingMaxWaitFrames = 300;

        // ---- Test_21 전용 상태 (Awake에서 기록 → OnDestroy에서 원복) ----
        private bool _owned;
        private bool _previousBuildPaused;
        private bool _builderPreexisting;
        private GameObject _caveRoom;
        private Color _ambientBeforeTest;
        private bool _ambientRecorded;
        private DayNightCycle _dnc;
        private bool _dncWasEnabled;
        private readonly List<(Light light, float intensity, LightShadows shadows)> _lightSnapshots = new List<(Light, float, LightShadows)>();
        private bool _lightingApplied;

        private void Awake()
        {
            // [137차 T2] 씬 이름 게이트 — 다른 씬에 실려도 무동작(Test_19 선례).
            // [실내 관람 모드] Test_23_IndoorTour(관람 부트 씬)도 허용 — 2종 게이트(부트/재빌드/플레이어/카메라
            //   경로 전부 재사용; 투어 순회 중 퇴출→씬 재로드 때마다 본 Awake이 동굴을 재빌드한다).
            if (gameObject.scene.name != TestSceneName && gameObject.scene.name != TourSceneName)
                return;
            _owned = true;

            // 실내 상태 — 이동/전투 게이트가 실내로 작동(Test_19 계약 동일).
            UITransitionState.IndoorActive = true;

            // 월드 영지 GLB 생성 차단 — Test_19 선례(Awake에서 세우고 OnDestroy에서 원복).
            _previousBuildPaused = TerritoryBuilder.BuildPaused;
            TerritoryBuilder.BuildPaused = true;
            _builderPreexisting = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include) != null;

            _ambientBeforeTest = RenderSettings.ambientLight;
            _ambientRecorded = true;
            NeutralizeSceneSunlights();
            EnsureGameManager();

            // [137차 T2] 동굴 생성 — 현행 CaveInteriorBuilder 그대로(수정 금지 계약).
            _caveRoom = CaveInteriorBuilder.BuildCaveInterior(CaveTerritoryId, CaveTier);
            if (_caveRoom == null)
            {
                Debug.LogError("[CaveTest] CaveInteriorBuilder returned null.");
                return;
            }
            int roomLightCount = 0;
            foreach (var l in _caveRoom.GetComponentsInChildren<Light>(true))
                roomLightCount++;
            Debug.Log($"[CaveTest] 🕳️ 동굴 Room 획득 — {_caveRoom.name} / 자식 {_caveRoom.transform.childCount}개 / 조명 {roomLightCount}개 (tier {CaveTier})");

            // [146차 P2] 실내 미니맵 캡처 훅 — 룸 빌드 완료 지점(플레이어 스폰 전 — 오브젝트 오염 없음).
            //   Systems→UI 직접 참조 금지 계약(Systems asmdef에 UI 레퍼런스 부재) → CaveLevelDirector 선례의
            //   FindCrossAssemblyType 리플렉션 경유. 실패 시 로그 1행+무음(크래시 금지).
            CaptureMinimapForRoom(_caveRoom);

            // [137차 T2] 천장 숨김 — 탑다운 천장 금지 계약(원본 파괴 금지, enabled만).
            HideCaveCeiling(_caveRoom);

            // 조명 보정 — DNC 봉쇄+방향광 0(Test_19 경로). 앰비언트 부스트 생략(암흑 유지 목적).
            StartCoroutine(ApplyCaveLightingWhenReady());

            // 플레이어 + 카메라 — 입구(남측) 스폰, Test_19 선례 준용.
            EnsurePlayer();
            EnsureCamera();

            // 보석상자 확인 — 4 기대(138차 고정 4개 계약).
            ValidateGemChests();

            Debug.Log("[CaveTest] 🧪 동굴 실내 테스트 준비 — ①탑다운(천장 숨김) ②횟불 플리커 7개 ③보석상자 E키 개방 ④암흑 유지(앰비언트 부스트 없음)");
        }

        private void OnDestroy()
        {
            if (!_owned) return;
            _owned = false;

            UITransitionState.IndoorActive = false;
            TerritoryBuilder.BuildPaused = _previousBuildPaused;

            // GameManager(DontDestroyOnLoad)가 이 씬 중 만든 TerritoryBuilder 제거 — Test_19 선례.
            if (!_builderPreexisting)
            {
                var builder = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include);
                if (builder != null) Destroy(builder);
            }

            // 조명 원복 — Test_19 선례(DNC 복원/방향광 원값/앰비언트 원값).
            if (_dnc != null) _dnc.enabled = _dncWasEnabled;
            foreach (var snap in _lightSnapshots)
            {
                if (snap.light == null) continue;
                snap.light.intensity = snap.intensity;
                snap.light.shadows = snap.shadows;
            }
            _lightSnapshots.Clear();
            _dnc = null;
            if (_lightingApplied && _ambientRecorded)
            {
                RenderSettings.ambientLight = _ambientBeforeTest;
                _lightingApplied = false;
                _ambientRecorded = false;
            }

            // 자기가 만든 동굴 Room만 제거(씬 언로드 시 자동 소멸분 — 명시 파괴는 이중 안전).
            if (_caveRoom != null)
            {
                Destroy(_caveRoom);
                _caveRoom = null;
            }
        }

        // ================================================================
        // 천장 숨김 (탑다운 천장 금지 계약)
        // ================================================================

        /// <summary>
        /// [137차 T2] CaveCeiling renderer.enabled=false — 탑다운 시점에 천장이 시야를 막지 않게 한다.
        /// Test_19의 HideCeiling은 Ceiling(문자열 하드코딩)을 찾으므로 동굴명(CaveCeiling)에 맞춘
        /// 동굴 전용 처리. 파괴가 아니라 enabled만 — 원본 보존 계약.
        /// [138차 Cave-A] 빌더 천장 폐기로 CaveCeiling 부재가 정상 — 부재 시 스킵 로그 1회(P-C null 가드).
        /// </summary>
        private static void HideCaveCeiling(GameObject room)
        {
            Transform ceiling = room.transform.Find(CeilingChildName);
            var renderer = ceiling != null ? ceiling.GetComponent<MeshRenderer>() : null;
            if (renderer != null)
            {
                renderer.enabled = false;
                Debug.Log("[CaveTest] ✅ 천장 숨김 — CaveCeiling renderer.enabled=false (원본 보존 — 탑다운 천장 금지 계약)");
            }
            else
            {
                // [138차 Cave-A] 천장 폐기 정합 — 빌더가 더 이상 CaveCeiling을 만들지 않는다(탑다운 천장 금지 계약).
                //   부재는 정상 상태: 경고가 아니라 스킵 로그 1회만 남긴다(P-C 최소 정합 — 타 로직 무수정).
                Debug.Log("[CaveTest] CaveCeiling 부재 — 천장 숨김 스킵(138차 P-A 천장 폐기 정합, 탑다운 계약).");
            }
        }

        // ================================================================
        // 조명 보정 (Test_19 ApplyTestLightingWhenReady 선례 — 앰비언트 부스트 생략)
        // ================================================================

        /// <summary>
        /// [137차 T2] 동굴 조명 보정 — Test_19 선례(DNC 봉쇄+방향광 off).
        /// 동굴이므로 앰비언트 부스트 ×1.25는 생략 — 빌더의 암흑 앰비언트 유지가 목적.
        /// 코루틴 무음사망 계약 회피(Update 폴링 코루틴 — 기존 계약 준용).
        /// </summary>
        private IEnumerator ApplyCaveLightingWhenReady()
        {
            for (int frame = 0; frame < LightingMaxWaitFrames; frame++)
            {
                var dnc = Object.FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
                if (dnc != null)
                {
                    _dnc = dnc;
                    _dncWasEnabled = dnc.enabled;
                    dnc.enabled = false;
                    foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    {
                        if (light == null || light.type != LightType.Directional) continue;
                        _lightSnapshots.Add((light, light.intensity, light.shadows));
                        light.intensity = 0f;
                        light.shadows = LightShadows.None;
                    }
                    _lightingApplied = true;
                    Debug.Log($"[CaveTest] 조명 보정 적용 — DNC 봉쇄(원복 예정), 방향광 {_lightSnapshots.Count}개 off, 앰비언트 부스트 없음(동굴 암흑 유지)");
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning("[CaveTest] DayNightCycle 미발견 — 테스트 조명 보정 건너뜀.");
        }

        private static void NeutralizeSceneSunlights()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light == null || light.type != LightType.Directional) continue;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
            }
        }

        // ================================================================
        // 플레이어 / 카메라 (Test_19 선례 복사 — 입구 위치만 동굴에 맞춤)
        // ================================================================

        private static void EnsurePlayer()
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
            // 플레이어 시각+애니 통일 — Test_19/Arena 선례(GLB+ForceBiped, 1.8m 정규화 내장).
            if (player.GetComponent<PlayerPlaceholder>() == null) player.AddComponent<PlayerPlaceholder>();
            // [158차 P1-A] Test_21 한정 플레이어 Lv10 부스팅 — 보석상자 게이트(gem_chest=Lv10) 통과용.
            //   PlayerStats 싱글톤: 이미 살아있는 인스턴스가 있으면 그쪽을 사용(Awake의 Destroy(gameObject)
            //   파괴 루틴 회피 — AddComponent로 싱글톤 중복 생성 시 플레이어 오브젝트 통째 파괴 위험).
            //   레벨업은 AddEXP(GetExpForLevel(10)) 정식 경유 — 스탯 포인트 +5/레벨·PlayerHealth 최대 HP
            //   갱신 동반(사적 _level 필드 우회 금지 계약).
            var stats = PlayerStats.Instance != null ? PlayerStats.Instance : player.GetComponent<PlayerStats>();
            if (stats == null) stats = player.AddComponent<PlayerStats>();
            if (stats.Level < 10)
            {
                int neededExp = Mathf.Max(0, stats.GetExpForLevel(10) - stats.CurrentEXP);
                if (neededExp > 0) stats.AddEXP(neededExp);
                Debug.Log($"[CaveTest] [158차 P1-A] 플레이어 Lv10 부스팅 — Level={stats.Level} (보석상자 게이트 gem_chest=Lv10 통과, 정식 AddEXP 경유)");
            }
            // [143차 P0] 입구(남측, +z) 스폰 — 빌더 입구 변 기준 계산(137차 사각룸 잔좌표 수리).
            player.transform.position = CaveInteriorBuilder.GetEntranceSpawnPosition(CaveTier);
            Debug.Log($"[CaveTest] 플레이어 스폰 — 입구(남측) 위치 {player.transform.position}");
        }

        private static void EnsureCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
            }
            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null) cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            // [카메라통일] 직교 폐기 → Test_10식 원근(PM 오빗 57° 단일 기록자 계약).
            cam.orthographic = false;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;   // farClip 500 계약 유지(Test_10 선례)
            // [카메라통일] 잔존 IndoorCameraFollow 제거 — PM 원근 단일 기록자(투어 DDOL 카메라 재사용 대비).
            var staleFollow = camGO.GetComponent<IndoorCameraFollow>();
            if (staleFollow != null) Object.Destroy(staleFollow);
            if (camGO.GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
                camGO.AddComponent<AudioListener>();
        }

        // ================================================================
        // 보석상자 확인 (4 기대)
        // ================================================================

        /// <summary>[137차 T2][138차 Cave-Follow] 보석상자 카운트 확인 — 고정 4개 계약 실측 로그.</summary>
        private static void ValidateGemChests()
        {
            var chest = Object.FindAnyObjectByType<GemChest>();
            if (chest == null)
            {
                Debug.LogError("[CaveTest] 보석상자 0개 — 기대 4 이탈(빌더 배치 확인 필요).");
                return;
            }
            int count = Object.FindObjectsByType<GemChest>(FindObjectsSortMode.None).Length;
            if (count == 4)
                Debug.Log($"[CaveTest] 💎 보석상자 {count}개 확인 — 4개 기대 통과(138차 고정 4개, 결정론 시드 cave_test_cave)");
            else
                Debug.LogError($"[CaveTest] 보석상자 {count}개 — 기대 4 이탈(138차 고정 4개 계약 확인 필요).");
        }

        // ================================================================
        // [146차 P2] 실내 미니맵 캡처 위임 (Systems→UI 어셈블리 경계 — 리플렉션)
        // ================================================================

        /// <summary>[146차 P2] 실내 미니맵 캡처 위임 — ProjectName.UI.Toolkit.IndoorMinimapBaker.CaptureFor(GameObject)
        ///   리플렉션 호출(Systems→UI 직접 참조 금지 — CaveLevelDirector 선례 동일 경계면). 실패 무음(크래시 금지).</summary>
        private static void CaptureMinimapForRoom(GameObject room)
        {
            try
            {
                var t = FindCrossAssemblyType("ProjectName.UI.Toolkit.IndoorMinimapBaker");
                var m = t?.GetMethod("CaptureFor", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(GameObject) }, null);
                m?.Invoke(null, new object[] { room });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CaveTest] 실내 미니맵 캡처 위임 실패(무시): {e.Message}");
            }
        }

        /// <summary>[146차 P2] CaveLevelDirector 선례 복제 — 어셈블리 탐색형 타입 조회(이름 불변).</summary>
        private static System.Type FindCrossAssemblyType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        // ================================================================
        // 공용 셋업 헬퍼 (Test_19 선례 복사)
        // ================================================================

        private static void EnsureGameManager()
        {
            GameObject gm = GameManager.Instance != null ? GameManager.Instance.gameObject : new GameObject("GameManager");
            if (GameManager.Instance == null)
                gm.AddComponent<GameManager>();
            if (Object.FindAnyObjectByType<GuardHostilitySystem>(FindObjectsInactive.Include) == null)
                gm.AddComponent<GuardHostilitySystem>();
            if (Object.FindAnyObjectByType<GuardVisualAttachSystem>(FindObjectsInactive.Include) == null)
                gm.AddComponent<GuardVisualAttachSystem>();
        }

        /// <summary>[137차 T2] 화면 안내문 — Test_19 OnGUI 선례.</summary>
        private void OnGUI()
        {
            if (!_owned) return;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            GUI.Label(new Rect(12f, 12f, 560f, 88f),
                "[Test_21] 동굴 실내 테스트 — 깜깜한 지형 + 횟불\n" +
                "① 탑다운 시점 — 천장 숨김(파괴 아님)으로 시야 확보\n" +
                "② 횟불 7개 플리커(TorchFlicker) — 암흑 속 웜 오렌지 조명\n" +
                "③ 보석상자(💎) E키 → 개방 상호작용 (4개 고정 배치)");
        }
    }
}
