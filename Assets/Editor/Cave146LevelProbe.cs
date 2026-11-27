using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [146차 P0] 동굴 던전 1층→2층 하강 "플레이어 외부 판정" 런타임 근거 수집 프로브 — Play 모드 진단 전용.
    ///
    /// 배경: Test_21_CaveInterior에서 1→2층 하강 시 플레이어가 건물 외부로 판정된다는 사용자 보고.
    ///   정적 검산(다각형 내부 판정)은 완료 — 스폰/포탈 전부 F2 다각형 내부 확인. 본 프로브는 런타임 근거 수집이 목적.
    ///   원인 후보: ①E키 경합 ②실내판정 플래그 이원화(IsIndoor vs IndoorActive) ③카메라/시각 ④물리(바닥 누락/벽 충돌).
    ///
    /// 시퀀스(단계별 try-catch 격리 — 한 스텝 실패가 전체 중단 금지, Farm153/154 선례):
    ///   스텝1(1층): CavePortal_Down 발견 → 플레이어 포탈 위치 텔레포트(x/z, y 유지) → 3프레임 대기
    ///     → STATE(pos/portal/floor/IsIndoor(리플렉션)/IndoorActive/OverlapSphere 3m/E키 후보) + 플레이어 카메라 캡처
    ///   스텝2(하강): CaveLevelDirector.GoDeeper(현재 CaveRoom) 직접 호출 — CaveLevelPortal.Update의 E분기와 동일 경로
    ///     → 10프레임 대기
    ///   스텝3(2층): CurrentFloor/CaveRoom 재발견/플레이어 pos/바닥 레이캐스트(player+2m→down)/OverlapSphere 1.5m(벽 파묻힘)/
    ///     룸 bounds vs 플레이어 포함/카메라 상태 + 캡처 2컷(플레이어 뷰/ortho 탑다운)
    ///   스텝4(가독성): ambientLight 임시 상향(저장→상향→캡처→복원) 탑다운 1컷
    ///
    /// 판정 로그(ASCII 프리픽스 — 인코딩 무관): "CHECK floor_inside=..." / "CHECK floor_below_hit=..." /
    ///   "CHECK wall_overlap=0/N" / "CAPTURE ..." / 최종 "DONE". 폴백 시 "SIMULATED" 마커로 근거 등급 명시.
    ///
    /// [146차 P0] 배치 Play 펌프 설계 — 프로젝트 실측 교훈 반영:
    ///   ① Play 진입 도메인 리로드가 EditorApplication.update 구독+정적 필드를 초기화(PlayCaptureHarness 21차 실측)
    ///     → SessionState(리로드 생존) + [InitializeOnLoad] 재구독으로 펌프를 복원한다.
    ///   ② 플레이어 루프 구동: EditorApplication.QueuePlayerLoopUpdate 펌프 + 런너 MonoBehaviour 코루틴
    ///     (LordDaughterPlayVerify 126차 선례 — 런너 Start 코루틴으로 프레임 흐름 보장).
    ///   ③ 펌프 사망 판정: Time.frameCount 진동 여부 — 배치모드에서 Play 펌프가 불가능하면(실측 판단)
    ///     에디트모드 DestroyImmediate 기반 GoDeeper 시퀀스 수동 재현으로 폴백하고 "SIMULATED" 마커로 근거 등급 명시.
    ///
    /// 실행(부모 몫 — 이 스크립트는 작성까지만. run_cave146_probe.sh 참조):
    ///   Unity.exe -batchmode -projectPath C:/Unity/code
    ///     -executeMethod ProjectName.EditorTools.Cave146LevelProbe.RunAll
    ///     -logFile C:/Unity/code/TestOutput/cave146_probe_run.log
    ///   ⚠ -quit 금지(Play 모드 배치 계약 — executeMethod 복귀 즉시 종료되어 Play 펌프가 소멸.
    ///     126차 Play 선례 동일 — 프로브가 스스로 EditorApplication.Exit(0/1)로 종료).
    ///   ⚠ -nographics 금지 — RT 캡처에 그래픽스 필요.
    /// 산출: Screenshots/cave146_1_f1_portal.png / cave146_2_f2_player_view.png /
    ///   cave146_3_f2_topdown.png / cave146_4_f2_topdown_bright.png
    /// 패턴 준용: Farm153PlayProbe(RT+cam.Render+ReadPixels — 34차 선례), Farm154HoleIsolationProbe(판정지표/격리).
    /// 소유권: 프로브 임시 카메라/RT는 프로브가 파괴. 플레이어 텔레포트/ambientLight 상향은 캡처 직후 원복 또는
    ///   일회성 배치 런이라 복원 불필요(씬 저장 없음). 프로덕션 코드(CaveLevelDirector/Builder/Portal) 무수정 — 진단만.
    /// </summary>
    public static class Cave146LevelProbe
    {
        private const string Tag = "[146차 P0]";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_21_CaveInterior.unity";
        private const string SessionKey = "cave146_probe_active";
        private const string RoomName = "CaveRoom";
        private const string DownPortalName = "CavePortal_Down";
        private const string ProbeCamPrefix = "Cave146Probe";
        private const int RTW = 1024;
        private const int RTH = 768;
        private const float SetupTimeoutSec = 15f;      // CaveRoom+Player 발견 대기 상한(스펙)
        private const float RunnerStartTimeoutSec = 20f; // 런너 미기동 → 펌프 사망 판정
        private const float HeartbeatTimeoutSec = 10f;   // 런 기동 후 무응답 → 펌프 중도 사망 판정
        private const float OverallTimeoutSec = 180f;    // 전체 감시 — 무한 대기 방지

        /// <summary>폴백 재현 모드 마커 — true면 모든 로그에 SIMULATED 접두(근거 등급 하향 명시).</summary>
        internal static bool Simulated;

        /// <summary>스텝 격리 실패 카운트 — 0이 아니면 최종 Exit(1)(모든 스텝은 끝까지 실행).</summary>
        internal static int StepErrors;

        // ── 펌프 상태 머신(도메인 리로드 후 재구독 경유 — 정적 초기값 재사용 안전) ──
        private enum Phase { WaitingPlay, WaitingSetup, StartingRunner, WaitingRunner, Finished }
        private static Phase _phase = Phase.WaitingPlay;
        private static float _phaseStartRealtime;
        private static int _framesAtPhaseStart;
        private static float _runStartRealtime = -1f;
        private static float _lastHeartbeatRealtime = -1f;
        private static Cave146PlayRunner _runner;

        /// <summary>이중 구독 가드 — 리로드 시 정적 초기화로 자동 해제되고, 리로드 없는 환경에서는 중복 방지.</summary>
        private static bool _pumpSubscribed;

        /// <summary>배치 런 진입점 — 씬 오픈 → Play 진입 요청. 이후 펌프/런너가 이어받는다.</summary>
        public static void RunAll()
        {
            Debug.Log($"{Tag} === 동굴 1→2층 하강 런타임 근거 수집 프로브 시작 ===");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                // 리로드 생존 플래그 — Play 진입 도메인 리로드 후 [InitializeOnLoad]가 펌프를 재구독(21차 실측 교훈).
                SessionState.SetBool(SessionKey, true);
                EditorApplication.isPlaying = true;
                SubscribePump();
                Debug.Log($"{Tag} Play 진입 요청 — {ScenePath} (펌프는 리로드 후 InitializeOnLoad가 재구독)");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{Tag} FAIL setup — 씬 오픈/Play 진입 실패");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>펌프 구독(이중 가드) — 리로드 후에는 정적 초기화로 _pumpSubscribed=false가 되어 재구독된다.</summary>
        private static void SubscribePump()
        {
            if (_pumpSubscribed) return;
            _pumpSubscribed = true;
            EditorApplication.update += Pump;
        }

        private static void UnsubscribePump()
        {
            _pumpSubscribed = false;
            EditorApplication.update -= Pump;
        }

        /// <summary>Play 진입 도메인 리로드 후 펌프 재구독 — SessionState 플래그로 본 런만 이어받는다.</summary>
        internal static void ResubscribeAfterReload()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            _runStartRealtime = (float)EditorApplication.timeSinceStartup;   // 리로드 후 기준 재설정(정적 초기화 대응)
            SubscribePump();
            Debug.Log($"{Tag} 펌프 재구독 완료 — 도메인 리로드 후 이어받기(isPlaying={EditorApplication.isPlaying})");
        }

        /// <summary>에디터 업데이트 펌프 — 플레이어 루프 촉진+상태 머신 구동. 예외 시 즉시 종료(Exit 1).</summary>
        private static void Pump()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();   // 배치모드 플레이어 루프 명시 촉진(무해)

                if (_runStartRealtime < 0f) _runStartRealtime = (float)EditorApplication.timeSinceStartup;
                float now = (float)EditorApplication.timeSinceStartup;
                if (now - _runStartRealtime > OverallTimeoutSec)
                {
                    FailFatal($"전체 감시 초과({OverallTimeoutSec}s) — 무한 대기 차단");
                    return;
                }

                switch (_phase)
                {
                    case Phase.WaitingPlay:
                        if (!EditorApplication.isPlaying) return;
                        EnterPhase(Phase.WaitingSetup);
                        break;

                    case Phase.WaitingSetup:
                        PumpWaitingSetup(now);
                        break;

                    case Phase.StartingRunner:
                        StartRunner();
                        break;

                    case Phase.WaitingRunner:
                        PumpWaitingRunner(now);
                        break;

                    case Phase.Finished:
                        break;
                }

                // 런 기동 후 감시 — 펌프가 살아 있는데 하트비트가 끊기면 플레이어 루프 중도 사망
                PumpHeartbeatWatch(now);
            }
            catch (Exception e)
            {
                FailFatal($"펌프 예외 — {e}");
            }
        }

        private static void EnterPhase(Phase phase)
        {
            _phase = phase;
            _phaseStartRealtime = (float)EditorApplication.timeSinceStartup;
            _framesAtPhaseStart = Time.frameCount;
        }

        /// <summary>씬 부트 대기 — CaveRoom+Player 발견 → 런너 기동. 타임아웃 시 펌프 사망/셋업 실패 판정.</summary>
        private static void PumpWaitingSetup(float now)
        {
            var room = GameObject.Find(RoomName);
            var player = GameObject.FindGameObjectWithTag("Player");
            if (room != null && player != null)
            {
                State($"setup room={Describe(room)} player={Describe(player)} " +
                    $"floor={CaveLevelDirector.CurrentFloor} frameCount={Time.frameCount}");
                EnterPhase(Phase.StartingRunner);
                return;
            }
            if (now - _phaseStartRealtime <= SetupTimeoutSec) return;

            // 타임아웃 — 원인 판별: 프레임이 전혀 흘렀는지(펌프 생존)로 분리
            bool pumpAlive = Time.frameCount != _framesAtPhaseStart;
            if (pumpAlive)
            {
                Debug.LogError($"{Tag} FAIL setup — {SetupTimeoutSec}s 내 {RoomName}/Player 미발견" +
                    $"(frameCount {_framesAtPhaseStart}→{Time.frameCount} — 펌프는 생존, 씬 부트 실패)");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.LogWarning($"{Tag} 배치 Play 펌프 무반응(frameCount {_framesAtPhaseStart} 정지) — SIMULATED 폴백 전환");
                RunSimulatedFallback();
            }
        }

        /// <summary>런너 생성 — Play 모드 MonoBehaviour 코루틴이 스텝 시퀀스를 구동(126차 선례).</summary>
        private static void StartRunner()
        {
            var go = new GameObject("Cave146ProbeRunner");
            _runner = go.AddComponent<Cave146PlayRunner>();
            _lastHeartbeatRealtime = (float)EditorApplication.timeSinceStartup;
            EnterPhase(Phase.WaitingRunner);
            State("runner 생성 — 스텝 시퀀스 위임");
        }

        private static void PumpWaitingRunner(float now)
        {
            if (_runner == null) return;
            if (_runner.Began)
            {
                if (_lastHeartbeatRealtime < 0f) _lastHeartbeatRealtime = now;
                _phase = Phase.Finished;   // 이후는 런너가 주도 — 펌프는 하트비트 감시만
                return;
            }
            if (now - _phaseStartRealtime <= RunnerStartTimeoutSec) return;

            bool pumpAlive = Time.frameCount != _framesAtPhaseStart;
            if (pumpAlive)
            {
                Debug.LogError($"{Tag} FAIL — 런너가 {RunnerStartTimeoutSec}s 내 기동하지 않음(펌프 생존, Start 미호출)");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
            else
            {
                Debug.LogWarning($"{Tag} 배치 Play 펌프 무반응(frameCount 정지) — SIMULATED 폴백 전환");
                RunSimulatedFallback();
            }
        }

        /// <summary>런너 하트비트 수신 — 런너 Update에서 호출(펌프 중도 사망 감시용).</summary>
        internal static void NoteHeartbeat()
        {
            _lastHeartbeatRealtime = (float)EditorApplication.timeSinceStartup;
        }

        /// <summary>런 진행 감시 — 펌프가 살아 있는데 하트비트가 끊기면 플레이어 루프 중도 사망으로 판정.</summary>
        private static void PumpHeartbeatWatch(float now)
        {
            if (_runner == null || !_runner.Began) return;
            if (now - _lastHeartbeatRealtime <= HeartbeatTimeoutSec) return;
            FailFatal($"플레이어 루프 중도 사망 — 하트비트 {HeartbeatTimeoutSec}s 정지(마지막 frameCount={Time.frameCount})");
        }

        /// <summary>정상 종료 — 모든 스텝 실행 후 호출. 스텝 실패가 하나라도 있으면 Exit(1).</summary>
        internal static void Finish()
        {
            bool ok = StepErrors == 0;
            Debug.Log($"{Tag} {(Simulated ? "SIMULATED " : "")}DONE — steps_ok={ok} " +
                $"(산출: Screenshots/cave146_1~4*.png, 로그: TestOutput/cave146_probe_run.log)");
            SessionState.SetBool(SessionKey, false);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>치명 실패 — 진행 불가. LogException 등가 로그+Exit(1).</summary>
        private static void FailFatal(string reason)
        {
            Debug.LogError($"{Tag} FAIL fatal — {reason}");
            SessionState.SetBool(SessionKey, false);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.Exit(1);
        }

        // ================================================================
        // [146차 P0] 폴백 — 배치 Play 펌프 불가(실측) 시 에디트모드 DestroyImmediate 수동 재현
        // ================================================================

        /// <summary>
        /// SIMULATED 폴백 — GoDeeper 시퀀스(룸 파괴→리빌드→스폰 텔레포트)를 DestroyImmediate로 수동 재현.
        /// 근거 등급 하향(Object.Destroy 지연 해소 vs 실제 Play 프레임 흐름 차이) — 모든 로그에 SIMULATED 마커.
        /// </summary>
        private static void RunSimulatedFallback()
        {
            UnsubscribePump();
            _phase = Phase.Finished;
            Simulated = true;
            // ⚠ isPlaying=false를 여기서 호출하지 않는다 — Play 종료 티어다운이 이후 생성하는
            //   진단 오브젝트(룸/플레이어)를 파괴할 수 있다. 종료는 Finish/FailFatal이 Exit 직전에 처리.
            Debug.LogWarning($"{Tag} SIMULATED 모드 전환 — 배치 Play 펌프 불가 판정. " +
                "에디트모드 DestroyImmediate 수동 재현(근거 등급: 시뮬레이션 — 실측 Play 대비 낮음)");

            try
            {
                // ① 1층 재현 — BuildFloor가 CurrentFloor=1 세팅+빌드(GoDeeper 내부와 동일 빌드 경로)
                var room1 = CaveLevelDirector.BuildFloor("cave_test", 2, 1);
                var player = EnsureSimulatedPlayer();
                var portal = room1 != null ? room1.transform.Find(DownPortalName) : null;
                State($"f1 room={Describe(room1)} player={Describe(player)} portal={Describe(portal != null ? portal.gameObject : null)}");
                if (room1 == null || player == null || portal == null)
                {
                    Debug.LogError($"{Tag} FAIL setup — SIMULATED 1층 재현 실패(room/player/portal 미발견)");
                    SessionState.SetBool(SessionKey, false);
                    EditorApplication.Exit(1);
                    return;
                }
                player.transform.position = new Vector3(
                    portal.position.x, player.transform.position.y, portal.position.z);
                LogPlayerState("f1", player, portal.gameObject);
                CapturePlayerViewSubstitute(player, "cave146_1_f1_portal.png");

                // ② 하강 재현 — GoDeeper의 Destroy(지연) 대신 DestroyImmediate 즉시 파괴 → BuildFloor(2)
                UnityEngine.Object.DestroyImmediate(room1);
                var room2 = CaveLevelDirector.BuildFloor("cave_test", 2, 2);
                player.transform.position = CaveInteriorBuilder.GetFloorSpawnPosition(2);   // GoDeeper TeleportPlayerToFloorSpawn 동일 경로
                State($"f2 room={Describe(room2)} spawn={V3(player.transform.position)} " +
                    $"spawnTarget={V3(CaveInteriorBuilder.GetFloorSpawnPosition(2))}");

                // ③ 2층 진단+캡처 — Play 경로와 동일 판정지표
                LogFloor2Diagnostics(player, room2);
                CaptureTopDown(room2, "cave146_3_f2_topdown.png", false);
                CaptureTopDown(room2, "cave146_4_f2_topdown_bright.png", true);
                Finish();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{Tag} FAIL — SIMULATED 재현 예외");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>SIMULATED용 플레이어 확보 — 씬에 Player 태그 오브젝트가 없으면 최소 생성(이동 컴포넌트 없음 — 판정 대상만).</summary>
        private static GameObject EnsureSimulatedPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) return player;
            player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = CaveInteriorBuilder.GetEntranceSpawnPosition(2);
            State($"simulated player 생성 — {Describe(player)} pos={V3(player.transform.position)}");
            return player;
        }

        // ================================================================
        // [146차 P0] 공용 로그/판정 헬퍼 — ASCII 프리픽스(인코딩 무관 파싱)
        // ================================================================

        internal static void State(string msg) => Debug.Log($"{Tag} {(Simulated ? "SIMULATED " : "")}STATE {msg}");
        internal static void Check(string msg) => Debug.Log($"{Tag} {(Simulated ? "SIMULATED " : "")}CHECK {msg}");
        internal static void Captured(string fileName, int kb) =>
            Debug.Log($"{Tag} {(Simulated ? "SIMULATED " : "")}CAPTURE {fileName} ({kb}KB)");
        internal static void LogStepException(string step, Exception e)
        {
            StepErrors++;
            Debug.LogException(e);
            Debug.LogError($"{Tag} FAIL {step} — 격리 처리(후속 스텝 계속)");
        }

        internal static string Describe(UnityEngine.Object o) => o == null ? "null" : o.name;
        private static string V3(Vector3 v) => v.ToString("F2", CultureInfo.InvariantCulture);

        /// <summary>실내판정 플래그(후보② 이원화 검증) — IsIndoor는 UI 어셈블리라 리플렉션(어셈블리 탐색형 — 선례 복제).</summary>
        private static string GetIsIndoorStr()
        {
            try
            {
                Type t = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType("ProjectName.UI.IndoorSceneTransition");
                    if (t != null) break;
                }
                var p = t?.GetProperty("IsIndoor", BindingFlags.Public | BindingFlags.Static);
                if (p == null) return "n/a(reflection miss)";
                return ((bool)p.GetValue(null, null)).ToString().ToLowerInvariant();
            }
            catch (Exception e)
            {
                return $"n/a({e.GetType().Name})";
            }
        }

        /// <summary>룸 이름 자식 탐색 — 비활성 포함(포탈 프레임 구조체 변화 대응).</summary>
        internal static Transform FindNamed(GameObject root, string name)
        {
            if (root == null) return null;
            var direct = root.transform.Find(name);
            if (direct != null) return direct;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>E키 소비 후보 나열(후보① E키 경합 검증) — ①콜라이더 경유 이름 매칭 + ②콜라이더 없는 거리 판정형 직접 스캔.</summary>
        internal static void LogEKeyCandidates(Vector3 playerPos)
        {
            var found = new List<string>();
            foreach (var col in Physics.OverlapSphere(playerPos, 3f))
                foreach (var comp in col.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    var n = comp.GetType().Name;
                    if (IsEKeyCandidateName(n))
                        AddCandidate(found, $"{n}({col.name} d={Vector3.Distance(col.transform.position, playerPos):F2}m)");
                }
            ScanDirect<CaveLevelPortal>(playerPos, found);
            ScanDirect<GemChest>(playerPos, found);
            ScanDirect<ResourceNode>(playerPos, found);
            State("e_candidates=" + (found.Count == 0 ? "none" : string.Join(" | ", found.ToArray())));
        }

        private static bool IsEKeyCandidateName(string typeName) =>
            typeName.Contains("Portal") || typeName.Contains("GemChest") ||
            typeName.Contains("ResourceNode") || typeName.Contains("InteractionPanel") ||
            typeName.Contains("Interact");

        private static void ScanDirect<T>(Vector3 playerPos, List<string> found) where T : Component
        {
            foreach (var comp in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(comp.transform.position, playerPos);
                if (d <= 3f) AddCandidate(found, $"{typeof(T).Name}({comp.name} d={d:F2}m)");
            }
        }

        private static void AddCandidate(List<string> found, string entry)
        {
            if (!found.Contains(entry)) found.Add(entry);
        }

        /// <summary>플레이어 활성 카메라 — enabled+계층 활성 우선, CameraFollow 계열 보유 최우선(후속15/16 수리 선례).</summary>
        internal static Camera FindPlayerCamera()
        {
            Camera best = null;
            bool bestFollow = false;
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.name.StartsWith(ProbeCamPrefix, StringComparison.Ordinal)) continue;   // 프로브 임시 카메라 제외
                if (!cam.enabled || !cam.gameObject.activeInHierarchy) continue;
                if (cam.targetTexture != null) continue;   // 이미 RT 사용 중(비활성 프리뷰 등) — 플레이어 뷰 아님
                bool follow = HasCameraFollow(cam);
                if (best == null || (follow && !bestFollow)) { best = cam; bestFollow = follow; }
            }
            return best;
        }

        private static bool HasCameraFollow(Camera cam)
        {
            foreach (var comp in cam.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (comp.GetType().Name.Contains("CameraFollow")) return true;
            }
            return false;
        }

        /// <summary>플레이어 카메라 그대로 캡처 — targetTexture 임시 교체 후 원복(카메라 상태 보존).</summary>
        internal static void CaptureFromCamera(Camera cam, string fileName)
        {
            if (cam == null)
            {
                Debug.LogError($"{Tag} FAIL capture {fileName} — 플레이어 카메라 미발견");
                StepErrors++;
                return;
            }
            var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            int kb = ReadAndSave(rt, fileName);
            cam.targetTexture = prevTarget;   // 원복 — 플레이어 카메라 렌더 경로 보존
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Captured(fileName, kb);
        }

        /// <summary>ortho 탑다운 캡처 — 프로브 임시 카메라(태그 없음). bright=true면 ambientLight 임시 상향(저장→상향→캡처→복원).</summary>
        internal static void CaptureTopDown(GameObject room, string fileName, bool bright)
        {
            if (room == null || !TryRoomBounds(room, out var bounds))
            {
                Debug.LogError($"{Tag} FAIL capture {fileName} — 룸 bounds 산출 실패");
                StepErrors++;
                return;
            }
            float orthoSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.1f;
            var go = new GameObject($"{ProbeCamPrefix}TopCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            cam.cullingMask = ~0;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.transform.position = new Vector3(bounds.center.x, bounds.max.y + 15f, bounds.center.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographicSize = orthoSize;

            Color ambientBefore = RenderSettings.ambientLight;   // 저장 — 캡처 직후 복원(일회성 런이지만 원복 계약 준용)
            if (bright) RenderSettings.ambientLight = new Color(1f, 1f, 1f, 1f);
            try
            {
                var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();
                int kb = ReadAndSave(rt, fileName);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                Captured(fileName, kb);
                State($"topdown bounds={bounds.min.ToString("F1", CultureInfo.InvariantCulture)}~" +
                    $"{bounds.max.ToString("F1", CultureInfo.InvariantCulture)} orthoSize={orthoSize:F1} bright={bright}");
            }
            finally
            {
                RenderSettings.ambientLight = ambientBefore;     // 복원
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>RT→PNG 저장 공통(34차 선례). 반환: 파일 크기 KB.</summary>
        private static int ReadAndSave(RenderTexture rt, string fileName)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var png = tex.EncodeToPNG();
            string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, png);
            UnityEngine.Object.DestroyImmediate(tex);
            return png.Length / 1024;
        }

        /// <summary>룸 전체 렌더러 bounds 결합 — 암석 링(장식 GLB) 포함으로 다각형보다 약간 큼(판정 시 주석 참조).</summary>
        internal static bool TryRoomBounds(GameObject room, out Bounds bounds)
        {
            bounds = default;
            var renderers = room != null ? room.GetComponentsInChildren<Renderer>() : null;
            if (renderers == null || renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        /// <summary>1층 STATE — pos/portal/floor/실내 플래그 2종/3m 콜라이더/E키 후보(후보①② 검증 데이터).</summary>
        internal static void LogPlayerState(string when, GameObject player, GameObject portal)
        {
            Vector3 pos = player.transform.position;
            State($"{when} pos={V3(pos)} portal={(portal != null ? $"{portal.name}@{V3(portal.transform.position)}" : "null")} " +
                $"floor={CaveLevelDirector.CurrentFloor} isIndoor={GetIsIndoorStr()} indoorActive={UITransitionState.IndoorActive}");
            var near = Physics.OverlapSphere(pos, 3f);
            State($"{when} overlap3m=[{JoinColliderNames(near, 12)}]");
            LogEKeyCandidates(pos);
        }

        /// <summary>2층 진단+판정 — 바닥 레이캐스트/벽 파묻힘/룸 bounds 포함(후보③④ 검증 데이터).</summary>
        internal static void LogFloor2Diagnostics(GameObject player, GameObject room)
        {
            Vector3 pos = player.transform.position;
            State($"f2 pos={V3(pos)} floor={CaveLevelDirector.CurrentFloor} isIndoor={GetIsIndoorStr()} " +
                $"indoorActive={UITransitionState.IndoorActive} room={Describe(room)}");

            // 바닥 존재(후보④) — 플레이어+2m에서 하향 레이캐스트
            var origin = pos + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 60f))
                Check($"floor_below_hit={hit.collider.name}@{hit.distance.ToString("F2", CultureInfo.InvariantCulture)}m " +
                    $"point_y={hit.point.y.ToString("F2", CultureInfo.InvariantCulture)}");
            else
                Check("floor_below_hit=none");

            // 벽 파묻힘(후보④) — 1.5m 내 콜라이더 중 벽 계열 카운트
            var near = Physics.OverlapSphere(pos, 1.5f);
            int walls = 0;
            foreach (var col in near)
                if (col.name.StartsWith("CaveWall", StringComparison.Ordinal)) walls++;
            Check($"wall_overlap={walls}/{near.Length}");
            State($"f2 overlap1.5m=[{JoinColliderNames(near, 12)}]");

            // 룸 bounds vs 플레이어(후보②/정적 검산 대조) — floor_inside는 y 무시 xz 포함 판정(사용자 증상이 평면 판정이므로)
            if (TryRoomBounds(room, out var bounds))
            {
                bool insideFull = bounds.Contains(pos);
                bool insideXZ = pos.x >= bounds.min.x && pos.x <= bounds.max.x &&
                                pos.z >= bounds.min.z && pos.z <= bounds.max.z;
                Check($"floor_inside={(insideXZ ? "true" : "false")}" +
                    $" (bounds_full={(insideFull ? "true" : "false")} — 룸 렌더러 bounds 결합, xz 기준)");
                State($"f2 bounds min={V3(bounds.min)} max={V3(bounds.max)}");
            }
            else
            {
                Check("floor_inside=false (bounds 산출 실패)");
            }

            // 카메라 상태(후보③)
            var cam = FindPlayerCamera();
            if (cam != null)
                State($"camera name={cam.name} pos={V3(cam.transform.position)} enabled={cam.isActiveAndEnabled} " +
                    $"ortho={cam.orthographic} orthoSize={cam.orthographicSize.ToString("F1", CultureInfo.InvariantCulture)}");
            else
                State("camera name=null (활성 플레이어 카메라 미발견)");
        }

        private static string JoinColliderNames(Collider[] colliders, int max)
        {
            if (colliders.Length == 0) return "none";
            var names = new List<string>();
            for (int i = 0; i < colliders.Length && names.Count < max; i++)
                names.Add(colliders[i].name);
            string suffix = colliders.Length > max ? $" (+{colliders.Length - max} more)" : "";
            return string.Join(", ", names.ToArray()) + suffix;
        }

        /// <summary>SIMULATED 전용 — 플레이어 뷰 대체 캡처(에디트모드에 실제 팔로우 카메라 부재 — IndoorCameraFollow 계약 시점 근사: 피치 65°/거리 11m).</summary>
        private static void CapturePlayerViewSubstitute(GameObject player, string fileName)
        {
            Vector3 pos = player.transform.position;
            var go = new GameObject($"{ProbeCamPrefix}SimView");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            cam.cullingMask = ~0;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            // IndoorCameraFollow 기본 시점 근사 — 피치 65°/거리 11m(3/4뷰)
            Vector3 offset = Quaternion.Euler(65f, 0f, 0f) * new Vector3(0f, 0f, -11f);
            cam.transform.position = pos + offset;
            cam.transform.LookAt(pos + Vector3.up * 1f);
            try
            {
                var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();
                int kb = ReadAndSave(rt, fileName);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                Captured(fileName, kb);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }

    /// <summary>
    /// [146차 P0] Play 모드 스텝 런너 — 코루틴으로 프레임 흐름을 보장하며 시퀀스를 수행(126차 선례).
    /// 에디터 어셈블리 소속이지만 배치 Play에서 AddComponent/Start 구동은 126차 실측 경유(선례 계약).
    /// </summary>
    public class Cave146PlayRunner : MonoBehaviour
    {
        /// <summary>시퀀스 개시 플래그 — 펌프가 런너 기동을 확인하는 신호(Start 첫 줄 세팅).</summary>
        public bool Began { get; private set; }

        private IEnumerator Start()
        {
            Began = true;
            Cave146LevelProbe.NoteHeartbeat();

            // 씬 부트 안정화 — CaveInteriorTestSetup Awake(빌드/스폰/조명) 완료 대기
            for (int i = 0; i < 10; i++) yield return null;

            // 스텝 격리 — yield try-catch 제약 회피: 내부 열거를 수동 MoveNext로 감싸 예외를 밖에서 포착
            yield return RunGuarded(Step1Floor1(), "step1");
            yield return RunGuarded(Step2GoDeeper(), "step2");
            yield return RunGuarded(Step3Floor2State(), "step3");
            yield return RunGuarded(Step4BrightCapture(), "step4");
            yield return RunGuarded(Step5CameraDeepDiag(), "step5");

            Cave146LevelProbe.Finish();
        }

        private void Update()
        {
            Cave146LevelProbe.NoteHeartbeat();   // 펌프 중도 사망 감시용 하트비트
        }

        /// <summary>내부 코루틴 격리 실행 — MoveNext 예외를 잡아 스텝 실패가 전체 중단하지 않게 한다(Farm153 선례 계약).</summary>
        private static IEnumerator RunGuarded(IEnumerator inner, string step)
        {
            while (true)
            {
                bool next;
                try { next = inner.MoveNext(); }
                catch (Exception e)
                {
                    Cave146LevelProbe.LogStepException(step, e);
                    yield break;
                }
                if (!next) yield break;
                yield return inner.Current;
            }
        }

        // ── 스텝1: 1층 포탈 근접 상태 + 플레이어 카메라 캡처 ──
        private IEnumerator Step1Floor1()
        {
            var room = GameObject.Find("CaveRoom");
            var player = GameObject.FindGameObjectWithTag("Player");
            var portalT = Cave146LevelProbe.FindNamed(room, "CavePortal_Down");
            var portal = portalT != null ? portalT.gameObject : null;
            Cave146LevelProbe.State($"step1 room={Cave146LevelProbe.Describe(room)} " +
                $"player={Cave146LevelProbe.Describe(player)} portal={Cave146LevelProbe.Describe(portal)}");
            if (player == null || portal == null)
            {
                Debug.LogError("[146차 P0] FAIL step1 — player/portal 미발견(하강 재현 불가 — 이후 스텝 스킵)");
                Cave146LevelProbe.StepErrors++;
                yield break;
            }

            // 텔레포트 — 포탈 x/z, 플레이어 y 유지(서 있는 높이 보존 — 실제 보행 접근 재현)
            player.transform.position = new Vector3(
                portal.transform.position.x, player.transform.position.y, portal.transform.position.z);
            for (int i = 0; i < 3; i++) yield return null;

            Cave146LevelProbe.LogPlayerState("f1", player, portal);
            var camPre = Cave146LevelProbe.FindPlayerCamera();
            Cave146LevelProbe.State("f1 cam=" + (camPre != null ?
                camPre.transform.position.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " id=" + camPre.GetInstanceID() : "null"));
            Cave146LevelProbe.CaptureFromCamera(Cave146LevelProbe.FindPlayerCamera(), "cave146_1_f1_portal.png");
        }

        // ── 스텝2: 하강 — CaveLevelPortal.Update E분기와 동일 직접 호출(포탈 컴포넌트 경유 아님 — 경합 배제 단일 경로) ──
        private IEnumerator Step2GoDeeper()
        {
            var room = GameObject.Find("CaveRoom");
            Cave146LevelProbe.State($"step2 goDeeper before floor={CaveLevelDirector.CurrentFloor} room={Cave146LevelProbe.Describe(room)}");
            if (room == null)
            {
                Debug.LogError("[146차 P0] FAIL step2 — CaveRoom 미발견(GoDeeper 스킵)");
                Cave146LevelProbe.StepErrors++;
                yield break;
            }
            var next = CaveLevelDirector.GoDeeper(room);
            for (int i = 0; i < 10; i++) yield return null;   // 리빌드/텔레포트/콜라이더 정착 대기
            Cave146LevelProbe.State($"step2 goDeeper after floor={CaveLevelDirector.CurrentFloor} " +
                $"next={Cave146LevelProbe.Describe(next)}");
            // 프레임별 카메라/플레이어 추적 — 유실 시점 특정(어느 프레임에 (0,0,0)이 되는가)
            var p2 = GameObject.FindGameObjectWithTag("Player");
            var cam2 = Cave146LevelProbe.FindPlayerCamera();
            for (int i = 0; i < 5; i++)
            {
                yield return null;
                Cave146LevelProbe.State("trace f" + i +
                    " cam=" + (cam2 != null ? cam2.transform.position.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "null") +
                    " id=" + (cam2 != null ? cam2.GetInstanceID().ToString() : "dead") +
                    " player=" + (p2 != null ? p2.transform.position.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "null") +
                    " ts=" + Time.timeScale.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        // ── 스텝3: 2층 후 판정지표 + 캡처 2컷(플레이어 뷰/탑다운) ──
        private IEnumerator Step3Floor2State()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var room = GameObject.Find("CaveRoom");
            if (player == null || room == null)
            {
                Debug.LogError("[146차 P0] FAIL step3 — player/room 미발견(2층 판정 불가)");
                Cave146LevelProbe.StepErrors++;
                yield break;
            }
            Cave146LevelProbe.Check($"floor_current={CaveLevelDirector.CurrentFloor} room_refound={(room != null ? "true" : "false")}");
            Cave146LevelProbe.LogFloor2Diagnostics(player, room);
            Cave146LevelProbe.CaptureFromCamera(Cave146LevelProbe.FindPlayerCamera(), "cave146_2_f2_player_view.png");
            Cave146LevelProbe.CaptureTopDown(room, "cave146_3_f2_topdown.png", false);
            yield return null;
        }

        // ── 스텝4: 가독성 컷 — ambientLight 임시 상향 탑다운(저장→상향→캡처→복원은 CaptureTopDown 내부) ──
        private IEnumerator Step4BrightCapture()
        {
            var room = GameObject.Find("CaveRoom");
            if (room == null)
            {
                Debug.LogError("[146차 P0] FAIL step4 — CaveRoom 미발견(가독성 컷 스킵)");
                Cave146LevelProbe.StepErrors++;
                yield break;
            }
            Cave146LevelProbe.CaptureTopDown(room, "cave146_4_f2_topdown_bright.png", true);
            yield return null;
        }

        // ── 스텝5: 카메라 유실 심층 진단 — 60프레임 추가 대기 후 카메라 전수/서프레스 플래그/플레이어 하부 콜라이더 실측 ──
        private IEnumerator Step5CameraDeepDiag()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[146차 P0] FAIL step5 — player 미발견");
                Cave146LevelProbe.StepErrors++;
                yield break;
            }
            for (int i = 0; i < 60; i++) yield return null;   // IndoorCameraFollow 재획득/LateUpdate 충분히 진행 대기

            Cave146LevelProbe.State("camdeep suppressed=" + ProjectName.Systems.PlayerMovement.CameraFollowSuppressed +
                " timeScale=" + Time.timeScale.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                " frameCount=" + Time.frameCount);

            // 플레이어/카메라 컴포넌트 활성 상태 — "갱신 루프 정지" 판별
            var pm = player.GetComponent<ProjectName.Systems.PlayerMovement>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            {
                var follow = c.GetComponent<ProjectName.Systems.IndoorCameraFollow>();
                Cave146LevelProbe.State("cam name=" + c.name +
                    " pos=" + c.transform.position.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                    " tag=" + (c.CompareTag("MainCamera") ? "MainCamera" : "none") +
                    " active=" + c.gameObject.activeInHierarchy +
                    " enabled=" + c.isActiveAndEnabled +
                    " follow=" + (follow != null ? follow.isActiveAndEnabled.ToString() : "absent") +
                    " parent=" + (c.transform.parent != null ? c.transform.parent.name : "root"));
            }
            Cave146LevelProbe.State("pm enabled=" + (pm != null ? pm.isActiveAndEnabled.ToString() : "absent") +
                " playerActive=" + player.activeInHierarchy);

            // 플레이어 하부 레이캐스트 — 자기 콜라이더 회피(플레이어 캡슐 하단 아래에서 발사)
            Vector3 p = player.transform.position;
            Vector3 origin = p + new Vector3(0f, -1.05f, 0f);
            var ray = new Ray(origin, Vector3.down);
            if (Physics.Raycast(ray, out var hit, 30f))
                Cave146LevelProbe.Check("below_hit name=" + hit.collider.name +
                    " point_y=" + hit.point.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                    " dist=" + hit.distance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            else
                Cave146LevelProbe.Check("below_hit=NONE(30m — 바닥 부재/누락 정황)");

            // 5m 내 콜라이더 전수 — 플레이어 발판/장애물 특정
            var hits = Physics.OverlapSphere(p, 5f);
            var names = new System.Collections.Generic.List<string>();
            foreach (var col in hits)
            {
                var b = col.bounds;
                names.Add(col.name + "(" + col.GetType().Name +
                    " c=" + b.center.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                    " s=" + b.size.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ")");
            }
            Cave146LevelProbe.State("overlap5m=" + (names.Count == 0 ? "none" : string.Join(" | ", names.ToArray())) +
                " playerPos=" + p.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));

            Cave146LevelProbe.CaptureFromCamera(Cave146LevelProbe.FindPlayerCamera(), "cave146_5_f2_after60f.png");

            // 결정 실험 — timeScale이 0이면 1로 회복 후 30프레임: 카메라 재추적+플레이어 착지 여부
            if (Time.timeScale < 0.5f)
            {
                Cave146LevelProbe.State("RECOVER timeScale 0→1 시도");
                Time.timeScale = 1f;
                for (int i = 0; i < 30; i++) yield return null;
                var cam3 = Cave146LevelProbe.FindPlayerCamera();
                Cave146LevelProbe.State("after ts=1 cam=" + (cam3 != null ? cam3.transform.position.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "null") +
                    " player=" + player.transform.position.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                Cave146LevelProbe.CaptureFromCamera(cam3, "cave146_6_f2_recovered.png");
            }
            yield return null;
        }
    }

    /// <summary>[146차 P0] Play 진입 도메인 리로드 후 펌프 재구독 부트 — SessionState 플래그로 본 런만 이어받는다.</summary>
    [InitializeOnLoad]
    internal static class Cave146ProbeBoot
    {
        static Cave146ProbeBoot()
        {
            // 배치 런 중 리로드(Play 진입/해제)마다 호출 — 플래그가 있을 때만 펌프 재구독(21차 실측 교훈 반영).
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("cave146_probe_active", false))
                    Cave146LevelProbe.ResubscribeAfterReload();
            };
        }
    }
}
