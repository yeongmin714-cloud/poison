using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.Systems
{
    /// <summary>
    /// [155차 P2] Test_22_AfterlifeCemetery 전용 — 사망→이계 체인 테스트씬 부트스트래퍼.
    ///
    /// 선례: CaveInteriorTestSetup(Test_21, 137차 T2) — 씬 이름 게이트 / EnsureGameManager /
    ///   TerritoryBuilder.BuildPaused 봉쇄 / 조명 보정 / OnDestroy 원복 패턴 복제.
    ///
    /// 선례와의 차이점(155차 P2 스펙):
    ///   - 룸을 직접 빌드하지 않는다 — 실제 사망 체인을 검증한다:
    ///     부트 1.5s 대기 → PlayerHealth.TakeDamage(float.MaxValue, Vector3.zero, "executed")
    ///     → Die → TryRouteDeathToAfterlife(리플렉션) → AfterlifeSurvivalSystem.TryInterceptDeath
    ///     (UseAfterlifeRespawn 기본 true) → EnterAfterlife → BuildingEvents.RequestEnterBuilding("afterlife")
    ///     → IndoorSceneTransition → AfterlifeInteriorBuilder.BuildAfterlifeInterior(공동묘지 리빌드).
    ///   - 사망 무기 타입 "executed"는 실측 PlayerHealth.IsParryEligibleDamageType에서 패링 제외 목록 —
    ///     TryParryReflection 스킵 확정 → 즉사 보장 경로(hitDirection Vector3.zero — 넉백 없음).
    ///   - AfterlifeSurvivalSystem.TestTargetSecondsOverride = 30f (부트에서 설정 — 훅 자체는 프로덕션 null 무변경).
    ///   - 상태 로그: [AfterlifeTestSetup] STATE — 진입 성공 / AfterlifeRoom 존재 / ExitDoor 상태.
    ///     부트 오브젝트는 이계 진입 시 월드 씬(Test_22) 언로드로 파괴되므로, 상태 보고는
    ///     DontDestroyOnLoad 감시자(AfterlifeStateWatcher)가 담당한다.
    /// </summary>
    public sealed class AfterlifeCemeteryTestSetup : MonoBehaviour
    {
        public const string TestSceneName = "Test_22_AfterlifeCemetery";

        // ---- 155차 P2 테스트 상수 ----
        private const float DeathDelaySec = 1.5f;          // 스펙 — 부트 후 1.5s 대기 후 강제 즉사
        private const float TestTargetSeconds = 30f;       // 이계 목표 시간 오버라이드(부트 전용 훅)
        private const string DeathCauseType = "executed";  // 패링 제외 무기 타입 — 즉사 보장(실측)
        private const string RoomName = "AfterlifeRoom";
        private const float WatchEntryTimeoutSec = 15f;    // 진입 성공 감시 상한
        private const float WatchRoomTimeoutSec = 10f;     // 룸 발견 추가 감시 상한

        /// <summary>프로세스 1회 가드 — 귀환 후 월드 씬(Test_22)이 리로드되어도 재사망하지 않게 한다.
        ///   도메인 리로드(Play 진입/해제)에서 자연 리셋 — 세션당 1회 체인 계약.</summary>
        private static bool s_chainExecuted;

        // ---- Test_22 전용 상태 (Awake에서 기록 → OnDestroy에서 원복) ----
        private bool _owned;
        private bool _previousBuildPaused;
        private bool _builderPreexisting;
        private Color _ambientBeforeTest;
        private bool _ambientRecorded;
        private DayNightCycle _dnc;
        private bool _dncWasEnabled;
        private readonly List<(Light light, float intensity, LightShadows shadows)> _lightSnapshots =
            new List<(Light light, float intensity, LightShadows shadows)>();
        private bool _lightingApplied;

        private void Awake()
        {
            // [155차 P2] 씬 이름 게이트 — 다른 씬에 실려도 무동작(Test_21 선례).
            if (gameObject.scene.name != TestSceneName)
                return;
            _owned = true;

            // 월드 영지 GLB 생성 차단 — Test_21 선례(Awake에서 세우고 OnDestroy에서 원복).
            _previousBuildPaused = TerritoryBuilder.BuildPaused;
            TerritoryBuilder.BuildPaused = true;
            _builderPreexisting = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include) != null;

            // 조명 보정 — 방향광 0 + DNC 봉쇄(발견 시). 빈 테스트씬은 DNC 부재가 정상 — 정적 1회 판정.
            _ambientBeforeTest = RenderSettings.ambientLight;
            _ambientRecorded = true;
            NeutralizeSceneSunlights();
            DisableDayNightCycleIfPresent();
            EnsureGameManager();

            // [155차 P2] 이계 목표 시간 훅 — 프로덕션은 null(난이도별) / 부트에서만 30s.
            AfterlifeSurvivalSystem.TestTargetSecondsOverride = TestTargetSeconds;

            // 플레이어 + 카메라 — Test_21 선례 준용(사망 체인의 당사자).
            EnsurePlayer();
            EnsureCamera();

            StartCoroutine(DeathChainWhenReady());
            SpawnStateWatcher();

            Debug.Log("[AfterlifeTestSetup] 🧪 공동묘지 사망 체인 테스트 준비 — 1.5s 후 강제 즉사 → 이계 자동 진입 검증 " +
                      $"(목표 시간 오버라이드 {TestTargetSeconds:0}s, UseAfterlifeRespawn={AfterlifeSurvivalSystem.UseAfterlifeRespawn})");
        }

        private void OnDestroy()
        {
            if (!_owned) return;
            _owned = false;

            TerritoryBuilder.BuildPaused = _previousBuildPaused;

            // GameManager(DontDestroyOnLoad)가 이 씬 중 만든 TerritoryBuilder 제거 — Test_21 선례.
            if (!_builderPreexisting)
            {
                var builder = Object.FindAnyObjectByType<TerritoryBuilder>(FindObjectsInactive.Include);
                if (builder != null) Destroy(builder);
            }

            // 조명 원복 — Test_21 선례(DNC 복원/방향광 원값/앰비언트 원값).
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

            // [155차 P2] 훅 원복 — 조건 원복 계약(이후 진입은 프로덕션 난이도 경로).
            AfterlifeSurvivalSystem.TestTargetSecondsOverride = null;
        }

        // ================================================================
        // 사망 체인 발화 (155차 P2 핵심 — 실제 죽음 경로 검증)
        // ================================================================

        /// <summary>
        /// [155차 P2] 부트 1.5s 대기 → 플레이어 강제 즉사 — PlayerHealth의 실제 죽음 경로
        /// (TakeDamage → ApplyDamage → Die)를 호출한다. 이후 이계 가로채기는 시스템 몫.
        /// 상태 보고는 DDOL 감시자가 담당 — 이 오브젝트는 진입 시 월드 씬 언로드로 소멸.
        /// </summary>
        private IEnumerator DeathChainWhenReady()
        {
            if (s_chainExecuted)
            {
                // 귀환 → 월드 씬 리로드로 재부트된 사이클 — 세션당 1회 계약(도메인 리로드 시 리셋).
                Debug.Log("[AfterlifeTestSetup] 사망 체인 이미 실행됨(프로세스 가드) — 재사망 스킵.");
                yield break;
            }
            s_chainExecuted = true;

            yield return new WaitForSeconds(DeathDelaySec);

            var hp = PlayerHealth.Instance;
            if (hp == null)
            {
                Debug.LogError("[AfterlifeTestSetup] FAIL — PlayerHealth.Instance 미발견(즉사 발화 불가).");
                yield break;
            }
            if (!AfterlifeSurvivalSystem.UseAfterlifeRespawn)
            {
                Debug.LogError("[AfterlifeTestSetup] FAIL — UseAfterlifeRespawn=false(이계 가로채기 게이트 닫힘 — 스펙 이탈).");
                yield break;
            }

            Debug.Log($"[AfterlifeTestSetup] 💀 강제 즉사 발화 — TakeDamage(float.MaxValue, Vector3.zero, \"{DeathCauseType}\") " +
                      $"직전 pos={hp.transform.position} hp={hp.CurrentHP:0}/{hp.MaxHP:0}");
            hp.TakeDamage(float.MaxValue, Vector3.zero, DeathCauseType);
            // 체인: ApplyDamage → HP 0 → Die() → OnPlayerDied → TryRouteDeathToAfterlife →
            //       TryInterceptDeath(가로채기) → EnterAfterlife(시간 동결+현장 부활) →
            //       RequestEnterBuilding("afterlife") → 공동묘지 리빌드.
            // 기존 리스폰 코루틴은 가로채기 성공 시 시작되지 않는다(PlayerHealth.Die 라우팅 계약).
        }

        // ================================================================
        // DDOL 상태 감시자 — 진입 성공/룸/문 상태 보고 (씬 언로드 생존)
        // ================================================================

        /// <summary>[155차 P2] 상태 감시자 스폰 — 부트와 독립 수명(DDOL)으로 STATE 로그를 남긴다.</summary>
        private void SpawnStateWatcher()
        {
            var go = new GameObject("[AfterlifeTestWatchdog]");
            DontDestroyOnLoad(go);
            go.AddComponent<AfterlifeStateWatcher>();
        }

        /// <summary>
        /// [155차 P2] 이계 진입 상태 감시 — IsActive → AfterlifeRoom → ExitDoor 순차 폴링 후
        ///   최종 STATE 1회 보고. 모든 대기 상한 내 폴링(무한 대기 금지).
        /// </summary>
        private sealed class AfterlifeStateWatcher : MonoBehaviour
        {
            private IEnumerator Start()
            {
                // ① 진입 성공 — IsActive 플립 대기(가로채기 성공 = 사망 체인 발화 증거).
                float t = 0f;
                while (!AfterlifeSurvivalSystem.IsActive)
                {
                    if (t > WatchEntryTimeoutSec)
                    {
                        Debug.LogError($"[AfterlifeTestSetup] STATE entry_success=false (isActive=false, {WatchEntryTimeoutSec:0}s 내 가로채기 없음 — 사망 체인/게이트 검증 필요)");
                        yield break;
                    }
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log("[AfterlifeTestSetup] STATE entry_success=true (TryInterceptDeath → 이계 진입 가로채기 확인)");

                // ② AfterlifeRoom 존재 — 실내 씬 리빌드 대기.
                GameObject room = null;
                t = 0f;
                while (room == null)
                {
                    room = GameObject.Find(RoomName);
                    if (room != null) break;
                    if (t > WatchRoomTimeoutSec)
                    {
                        Debug.LogError($"[AfterlifeTestSetup] STATE room=none ({WatchRoomTimeoutSec:0}s 내 {RoomName} 미발견 — 빌더/진입 흐름 확인 필요)");
                        yield break;
                    }
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                Debug.Log($"[AfterlifeTestSetup] STATE room_found={room.name} pos={room.transform.position} children={room.transform.childCount}");

                // ③ ExitDoor 상태 — 개시 비활성 기대(완주 시 개방).
                var door = Object.FindAnyObjectByType<AfterlifeExitDoor>(FindObjectsInactive.Include);
                if (door != null)
                    Debug.Log($"[AfterlifeTestSetup] STATE exit_door=found active={door.IsActive} pos={door.transform.position}");
                else
                    Debug.LogError("[AfterlifeTestSetup] STATE exit_door=none (AfterlifeExitDoor 미발견)");

                // ④ 최종 상태 — 플레이어(현장 부활 HP 50% 기대)/시간 동결.
                var hp = PlayerHealth.Instance;
                if (hp != null)
                    Debug.Log($"[AfterlifeTestSetup] STATE player pos={hp.transform.position} hp={hp.CurrentHP:0}/{hp.MaxHP:0} " +
                              $"timeFrozen={TimeManager.IsTimeFrozen} elapsed={AfterlifeSurvivalSystem.Elapsed:0.0}/{TestTargetSeconds:0}s");
                else
                    Debug.LogWarning("[AfterlifeTestSetup] STATE player=null (이계 진입 후 플레이어 미발견)");
            }
        }

        // ================================================================
        // 조명 보정 (Test_21 선례 — 빈 씬 실정에 맞춘 정적 1회 판정)
        // ================================================================

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

        /// <summary>DNC 발견 시에만 봉쇄(스냅샷 → OnDestroy 원복) — 빈 테스트씬은 DNC 부재가 정상(부재 시 무음 스킵).</summary>
        private void DisableDayNightCycleIfPresent()
        {
            var dnc = Object.FindAnyObjectByType<DayNightCycle>(FindObjectsInactive.Include);
            if (dnc == null) return;
            _dnc = dnc;
            _dncWasEnabled = dnc.enabled;
            dnc.enabled = false;
            _lightingApplied = true;
            Debug.Log("[AfterlifeTestSetup] 조명 보정 — DNC 봉쇄(원복 예정), 방향광 0, 앰비언트 원본 기록");
        }

        // ================================================================
        // 플레이어 / 카메라 (Test_21 선례 복사 — 스폰 위치만 공동묘지에 맞춤)
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
            if (player.GetComponent<PlayerPlaceholder>() == null) player.AddComponent<PlayerPlaceholder>();
            // 스폰 — 중앙 남측 여유지(분수/무덤 호와 충돌 없는 발판). 사망 직후 위치는 이계 흐름이 이동시킨다.
            player.transform.position = new Vector3(0f, 1.2f, 8f);
            Debug.Log($"[AfterlifeTestSetup] 플레이어 스폰 — pos={player.transform.position} (사망 체인 당사자)");
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
        // 공용 셋업 헬퍼 (Test_21 선례 복사)
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

        /// <summary>[155차 P2] 화면 안내문 — Test_21 OnGUI 선례.</summary>
        private void OnGUI()
        {
            if (!_owned) return;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            GUI.Label(new Rect(12f, 12f, 560f, 88f),
                "[Test_22] 공동묘지 사망 체인 테스트 — 즉사 → 이계 자동 진입\n" +
                "① 1.5s 후 강제 즉사(TakeDamage 'executed') → 가로채기 확인\n" +
                "② 이계 진입(시간 동결 + 현장 부활 50%) → 공동묘지 리빌드\n" +
                "③ 목표 30s(테스트 훅) 완주 → 귀환 문 개방 → 접촉 시 귀환");
        }
    }
}
