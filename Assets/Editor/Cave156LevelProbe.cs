using System.Collections;
using System.Globalization;
using ProjectName.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [156차] 동굴 셀맵 배치 Play 프로브 — 직각 꺽임 셀맵 재설계(17각 다각형 폐기)의 런타임 근거 수집.
    /// 검증: ①F1 셋업(바닥/얇은 벽/캡/횟불/가드/블로커/흙 머티리얼/포탈) ②스폰 착지
    /// ③입구 봉합(블로커 통과 불가 + 낙하 0 — 사용자 지시 1급) ④CaveFallGuard 안전망
    /// ⑤F1→F5 포탈 순회(각 층 스폰 착지+캡처) ⑥탑다운 캡처 5층(vision 판독용).
    /// 구조 선례: Cave146LevelProbe(146차 P0) — 펌프/런너/Finish-Exit 패턴 1:1.
    /// -quit/-nographics 금지(Play 모드 배치 계약 — run_cave146_probe.sh 사유 승계).
    /// </summary>
    public static class Cave156LevelProbe
    {
        private const string Tag = "[156차]";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_21_CaveInterior.unity";
        private const string SessionKey = "cave156_probe_active";
        private const string RoomName = "CaveRoom";
        private const int RTW = 1024;
        private const int RTH = 768;
        private const float SetupTimeoutSec = 15f;
        private const float RunnerStartTimeoutSec = 20f;
        private const float HeartbeatTimeoutSec = 25f;
        private const float OverallTimeoutSec = 300f;   // 5층 순회+캡처 10컷 — 146차 180s 대비 여유

        /// <summary>스텝 격리 실패 카운트 — 0이 아니면 최종 Exit(1)(모든 스텝은 끝까지 실행).</summary>
        internal static int StepErrors;

        private enum Phase { WaitingPlay, WaitingSetup, StartingRunner, WaitingRunner, Finished }
        private static Phase _phase = Phase.WaitingPlay;
        private static float _phaseStartRealtime;
        private static int _framesAtPhaseStart;
        private static float _runStartRealtime = -1f;
        private static float _lastHeartbeatRealtime = -1f;
        private static Cave156PlayRunner _runner;
        private static bool _pumpSubscribed;

        /// <summary>배치 런 진입점 — 씬 오픈 → Play 진입 요청(이후 펌프/런너가 이어받는다).</summary>
        public static void RunAll()
        {
            Debug.Log($"{Tag} === 동굴 셀맵 배치 Play 프로브 시작 ===");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetBool(SessionKey, true);
                EditorApplication.isPlaying = true;
                SubscribePump();
                Debug.Log($"{Tag} Play 진입 요청 — {ScenePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{Tag} FAIL setup — 씬 오픈/Play 진입 실패");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
        }

        private static void SubscribePump()
        {
            if (_pumpSubscribed) return;
            _pumpSubscribed = true;
            EditorApplication.update += Pump;
        }

        /// <summary>Play 진입 도메인 리로드 후 펌프 재구독 — SessionState 플래그로 본 런만 이어받는다.</summary>
        internal static void ResubscribeAfterReload()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            _runStartRealtime = (float)EditorApplication.timeSinceStartup;
            SubscribePump();
            Debug.Log($"{Tag} 펌프 재구독 완료 — 도메인 리로드 후 이어받기");
        }

        private static void Pump()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
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
                        if (_runner == null) return;
                        if (_runner.Began)
                        {
                            if (_lastHeartbeatRealtime < 0f) _lastHeartbeatRealtime = now;
                            _phase = Phase.Finished;   // 이후는 런너가 주도 — 펌프는 하트비트 감시만
                        }
                        else if (now - _phaseStartRealtime > RunnerStartTimeoutSec)
                        {
                            FailFatal($"런너 미기동({RunnerStartTimeoutSec}s) — 펌프 사망 판정");
                        }
                        break;
                    case Phase.Finished:
                        break;
                }
                if (_runner != null && _runner.Began && now - _lastHeartbeatRealtime > HeartbeatTimeoutSec)
                {
                    // [156차] 플레이어 루프 중도 사망(146차 실측 재발) — 잔여 층 검증은 에디트모드 폴백으로 계속(근거 등급 하향 명시).
                    Debug.LogWarning($"{Tag} 플레이어 루프 중도 사망(frameCount={Time.frameCount}) — SIMULATED 폴백 전환(잔여 층 검증)");
                    RunTourFallback();
                    return;
                }
            }
            catch (System.Exception e)
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

        private static void PumpWaitingSetup(float now)
        {
            var room = GameObject.Find(RoomName);
            var player = GameObject.FindGameObjectWithTag("Player");
            if (room != null && player != null)
            {
                State($"setup room={Describe(room)} player={Describe(player)} floor={CaveLevelDirector.CurrentFloor} frameCount={Time.frameCount}");
                EnterPhase(Phase.StartingRunner);
                return;
            }
            if (now - _phaseStartRealtime <= SetupTimeoutSec) return;
            Debug.LogError($"{Tag} FAIL setup — {SetupTimeoutSec}s 내 {RoomName}/Player 미발견(frameCount {_framesAtPhaseStart}→{Time.frameCount})");
            SessionState.SetBool(SessionKey, false);
            EditorApplication.Exit(1);
        }

        private static void StartRunner()
        {
            var go = new GameObject("Cave156ProbeRunner");
            _runner = go.AddComponent<Cave156PlayRunner>();
            _lastHeartbeatRealtime = (float)EditorApplication.timeSinceStartup;
            EnterPhase(Phase.WaitingRunner);
            State("runner 생성 — 스텝 시퀀스 위임");
        }

        /// <summary>정상 종료 — 스텝 실패가 하나라도 있으면 Exit(1).</summary>
        internal static void Finish()
        {
            bool ok = StepErrors == 0;
            Debug.Log($"{Tag} DONE — steps_ok={ok} errors={StepErrors} " +
                "(산출: Screenshots/cave156_f1~f5*.png, 로그: TestOutput/cave156_probe_run.log)");
            SessionState.SetBool(SessionKey, false);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void FailFatal(string reason)
        {
            Debug.LogError($"{Tag} FAIL fatal — {reason}");
            SessionState.SetBool(SessionKey, false);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.Exit(1);
        }

        /// <summary>[156차] 잔여 층 폴백 — 146차 RunSimulatedFallback 패턴(에디트모드 BuildFloor 수동 재현).
        /// 현재 층 이후를 순회: 층별 체크(포탈/횟불) + 탑다운 캡처. 근거 등급: 시뮬레이션(로그 SIMULATED 마커).</summary>
        private static void RunTourFallback()
        {
            _phase = Phase.Finished;
            State("SIMULATED 폴백 — 에디트모드 잔여 층 순회 시작");
            try
            {
                var old = GameObject.Find(RoomName);
                if (old != null) Object.DestroyImmediate(old);
                for (int floor = CaveLevelDirector.CurrentFloor + 1; floor <= 5; floor++)
                {
                    var room = CaveLevelDirector.BuildFloor("cave_test", 2, floor);
                    if (room == null) { CheckFail($"SIMULATED F{floor} — BuildFloor null"); continue; }
                    int torches = room.GetComponentsInChildren<TorchFlicker>(true).Length;
                    int caps = 0;
                    foreach (Transform c in room.transform) if (c.name.StartsWith("CaveWallCap_")) caps++;
                    var up = FindNamed(room, $"CavePortal_F{floor}_Up");
                    var down = FindNamed(room, $"CavePortal_F{floor}_Down");
                    State($"SIMULATED F{floor} torches={torches} caps={caps} up={(up != null ? "yes" : "null")} " +
                        $"down={(floor < 5 ? (down != null ? "yes" : "null") : (down == null ? "absent-ok" : "unexpected"))}");
                    if (torches != 8) CheckFail($"SIMULATED F{floor} — TorchFlicker 8 기대: {torches}");
                    if (up == null) CheckFail($"SIMULATED F{floor} — 상승 포탈 미배치");
                    if (floor < 5 && down == null) CheckFail($"SIMULATED F{floor} — 하강 포탈 미배치");
                    if (floor == 5 && down != null) CheckFail("SIMULATED F5 — 최심부 하강 포탈 존재(계약 위반)");
                    CaptureTopDown(room, $"cave156_f{floor}_topdown.png", false);
                    CaptureTopDown(room, $"cave156_f{floor}_topdown_bright.png", true);
                    if (floor < 5) Object.DestroyImmediate(room);   // 다음 층 빌드 전 정리
                }
                Finish();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{Tag} FAIL — SIMULATED 폴백 예외");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
        }

        // ── 공용 유틸(146차 선례 축소판) ──

        internal static void State(string s) => Debug.Log($"{Tag} {s}");

        /// <summary>스텝 격리 체크 — 실패 카운트만 증가(스텝은 끝까지 실행).</summary>
        internal static void CheckFail(string s)
        {
            StepErrors++;
            Debug.LogError($"{Tag} CHECK-FAIL {s}");
        }

        internal static string Describe(GameObject go)
        {
            if (go == null) return "null";
            return $"{go.name}(active={go.activeInHierarchy})";
        }

        internal static Transform FindNamed(GameObject root, string name)
        {
            if (root == null) return null;
            var t = root.transform.Find(name);
            if (t != null) return t;
            foreach (var c in root.GetComponentsInChildren<Transform>(true))
                if (c.name == name) return c;
            return null;
        }

        internal static Camera FindPlayerCamera()
        {
            var cam = Camera.main;
            if (cam != null) return cam;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.CompareTag("MainCamera")) return c;
            return null;
        }

        /// <summary>카메라 1컷 RT 캡처 → Screenshots/{fileName}. 146차 ReadAndSave 패턴 축소 재구현.</summary>
        internal static void CaptureFromCamera(Camera cam, string fileName)
        {
            if (cam == null) { CheckFail($"capture camera null — {fileName}"); return; }
            var rt = new RenderTexture(RTW, RTH, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(RTW, RTH, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, RTW, RTH), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes($"Screenshots/{fileName}", tex.EncodeToPNG());
                State($"CAPTURE {fileName}");
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        /// <summary>발밑 바닥 실측 — 플레이어 캡슐 하단 아래(origin=pos−1.05)에서 하향 레이캐스트(자기 캡슐 회피).</summary>
        internal static string FeetRaycast(GameObject player)
        {
            Vector3 p = player.transform.position;
            var ray = new Ray(p + new Vector3(0f, -1.05f, 0f), Vector3.down);
            if (Physics.Raycast(ray, out var hit, 30f))
                return $"below_hit name={hit.collider.name} point_y={hit.point.y.ToString("F2", CultureInfo.InvariantCulture)} dist={hit.distance.ToString("F2", CultureInfo.InvariantCulture)}";
            return "below_hit=NONE(30m — 바닥 부재/누락 정황)";
        }

        /// <summary>탑다운 캡처 — Cave146LevelProbe.CaptureTopDown(internal) 재사용(앰비언트 상향/복원 포함).</summary>
        internal static void CaptureTopDown(GameObject room, string fileName, bool bright)
            => Cave146LevelProbe.CaptureTopDown(room, fileName, bright);
    }

    /// <summary>[156차] Play 모드 러너 — 스텝 시퀀스 코루틴(126차/146차 선례).</summary>
    public class Cave156PlayRunner : MonoBehaviour
    {
        public bool Began { get; private set; }

        private IEnumerator Start()
        {
            Began = true;
            yield return RunGuarded(Step1_F1ChecksAndCapture(), "step1_F1ChecksAndCapture");
            yield return RunGuarded(Step2_EntranceSeal(), "step2_EntranceSeal");
            yield return RunGuarded(Step3_FallGuard(), "step3_FallGuard");
            yield return RunGuarded(Step4_FloorTour(), "step4_FloorTour");
            Cave156LevelProbe.Finish();
        }

        private static IEnumerator RunGuarded(IEnumerator inner, string step)
        {
            bool ran = false;
            while (true)
            {
                try
                {
                    if (!inner.MoveNext()) break;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[156차] FAIL {step} — 예외: {e}");
                    Cave156LevelProbe.StepErrors++;
                    break;
                }
                ran = true;
                yield return inner.Current;
            }
            if (!ran) { /* 빈 코루틴 — 무해 */ }
        }

        // ── 스텝1: F1 셋업 전수 체크 + 캡처 ──
        private IEnumerator Step1_F1ChecksAndCapture()
        {
            var room = GameObject.Find("CaveRoom");
            var player = GameObject.FindGameObjectWithTag("Player");
            Cave156LevelProbe.State($"step1 room={Cave156LevelProbe.Describe(room)} player={Cave156LevelProbe.Describe(player)}");
            if (room == null || player == null)
            {
                Cave156LevelProbe.CheckFail("step1 — room/player 미발견");
                yield break;
            }

            // 바닥 — 병합 쿼드 메시+MeshCollider(충돌 단일소스 계약)
            var floorT = room.transform.Find("CaveFloor");
            if (floorT == null)
            {
                Cave156LevelProbe.CheckFail("step1 — CaveFloor 미발견");
                yield break;
            }
            var mf = floorT.GetComponent<MeshFilter>();
            var bounds = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds();
            Cave156LevelProbe.State($"CHECK floor_mesh size=({bounds.size.x:F1},{bounds.size.z:F1}) " +
                $"collider={floorT.GetComponent<MeshCollider>() != null}");
            if (bounds.size.x < 90f || bounds.size.z < 66f)
                Cave156LevelProbe.CheckFail($"step1 — 바닥 크기 미달(경계 96×72 기대): {bounds.size.x:F1}×{bounds.size.z:F1}");

            var mr = floorT.GetComponent<MeshRenderer>();
            string matName = mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "null";
            Cave156LevelProbe.State($"CHECK floor_mat={matName}");
            if (!matName.StartsWith("CaveDirtFloorMat"))
                Cave156LevelProbe.CheckFail($"step1 — 바닥 머티리얼이 흙 단일화 아님: {matName}");

            // 얇은 돌 벽 — 전부 BoxCollider 보유(낙하 봉합 충돌)
            int wallCount = 0; int wallColliderMissing = 0;
            foreach (Transform c in room.transform)
                if (c.name.StartsWith("CaveWall_"))
                {
                    wallCount++;
                    if (c.GetComponent<BoxCollider>() == null) wallColliderMissing++;
                }
            Cave156LevelProbe.State($"CHECK walls count={wallCount} colliderMissing={wallColliderMissing}");
            if (wallColliderMissing > 0)
                Cave156LevelProbe.CheckFail($"step1 — 벽 콜라이더 누락 {wallColliderMissing}개(낙하 경로 잔존)");

            // 평면 캡 51 — 렌더러 전용
            int capCount = 0; int capCollider = 0;
            foreach (Transform c in room.transform)
                if (c.name.StartsWith("CaveWallCap_"))
                {
                    capCount++;
                    if (c.GetComponent<Collider>() != null) capCollider++;
                }
            Cave156LevelProbe.State($"CHECK caps count={capCount} withCollider={capCollider}");
            if (capCount != 51 || capCollider > 0)
                Cave156LevelProbe.CheckFail($"step1 — 캡 계약 이탈(기대 51/콜라이더 0): {capCount}/{capCollider}");

            // 횟불 9(벽 8+화로 1, ≤9 갱신 상한) + 낙하 가드 + 입구 봉합
            int torches = room.GetComponentsInChildren<TorchFlicker>(true).Length;
            Cave156LevelProbe.State($"CHECK torches={torches}");
            if (torches != 9) Cave156LevelProbe.CheckFail($"step1 — TorchFlicker 9 기대: {torches}");
            var guard = room.GetComponent<CaveFallGuard>();
            Cave156LevelProbe.State($"CHECK fallguard={(guard != null ? "attached" : "MISSING")}");
            if (guard == null) Cave156LevelProbe.CheckFail("step1 — CaveFallGuard 미부착(안전망 부재)");

            var doorRoot = Cave156LevelProbe.FindNamed(room, "CaveEntranceDoor");
            var blocker = doorRoot != null ? doorRoot.Find("CaveDoorBlocker") : null;
            bool blockerEnabled = blocker != null && blocker.GetComponent<Collider>() != null && blocker.GetComponent<Collider>().enabled;
            Cave156LevelProbe.State($"CHECK door_blocker={(blocker != null ? blocker.position.ToString("F1", CultureInfo.InvariantCulture) : "null")} enabled={blockerEnabled}");
            if (!blockerEnabled) Cave156LevelProbe.CheckFail("step1 — 입구 블로커 미봉합(낙하 경로)");

            // 포탈 — D/U 마커 좌표 존재
            var down = Cave156LevelProbe.FindNamed(room, "CavePortal_F1_Down");
            var up = Cave156LevelProbe.FindNamed(room, "CavePortal_F1_Up");
            Cave156LevelProbe.State($"CHECK portals down={(down != null ? down.position.ToString("F1", CultureInfo.InvariantCulture) : "null")} " +
                $"up={(up != null ? up.position.ToString("F1", CultureInfo.InvariantCulture) : "null")}");
            if (down == null || up == null) Cave156LevelProbe.CheckFail("step1 — F1 포탈 미배치");

            // 스폰 착지 실측
            player.transform.position = CaveInteriorBuilder.GetEntranceSpawnPosition(1);
            for (int i = 0; i < 5; i++) yield return null;
            Cave156LevelProbe.State("CHECK spawn " + Cave156LevelProbe.FeetRaycast(player) +
                $" pos={player.transform.position.ToString("F2", CultureInfo.InvariantCulture)}");

            // 캡처 — 플레이어 뷰 + F1 탑다운(일반/밝기 보정)
            var pcam = Cave156LevelProbe.FindPlayerCamera();
            if (pcam != null)
            {
                Cave156LevelProbe.State($"CAMDIAG main={pcam.name} pos={pcam.transform.position.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} " +
                    $"clear={pcam.clearFlags} mask=0x{pcam.cullingMask:X8} bg={pcam.backgroundColor} " +
                    $"ortho={pcam.orthographic} fov={pcam.fieldOfView:F0} near={pcam.nearClipPlane:F2} far={pcam.farClipPlane:F0} " +
                    $"enabled={pcam.enabled} active={pcam.isActiveAndEnabled} depth={pcam.depth}");
                var allCams = UnityEngine.Object.FindObjectsByType<Camera>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
                foreach (var c in allCams)
                    Cave156LevelProbe.State($"CAMALL name={c.name} tag={c.tag} pos={c.transform.position.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} " +
                        $"enabled={c.enabled} ortho={c.orthographic} depth={c.depth} parent={(c.transform.parent != null ? c.transform.parent.name : "null")}");
                Cave156LevelProbe.State($"RENDIAG fog={UnityEngine.RenderSettings.fog} fogColor={UnityEngine.RenderSettings.fogColor} " +
                    $"fogMode={UnityEngine.RenderSettings.fogMode} density={UnityEngine.RenderSettings.fogDensity:F3} " +
                    $"ambientMode={UnityEngine.RenderSettings.ambientMode} ambient={UnityEngine.RenderSettings.ambientLight}");
            }
            Cave156LevelProbe.CaptureFromCamera(pcam, "cave156_f1_player_view.png");
            Cave156LevelProbe.CaptureTopDown(room, "cave156_f1_topdown.png", false);
            Cave156LevelProbe.CaptureTopDown(room, "cave156_f1_topdown_bright.png", true);
        }

        // ── 스텝2: 입구 봉합 실측 — 블로커 통과 시도 + 관측 후 낙하 판정(사용자 지시 1급) ──
        private IEnumerator Step2_EntranceSeal()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) { Cave156LevelProbe.CheckFail("step2 — player 미발견"); yield break; }
            var cc = player.GetComponent<CharacterController>();
            if (cc == null) { Cave156LevelProbe.CheckFail("step2 — CharacterController 미발견"); yield break; }

            // 입구 통로 안쪽(셀 (23,30) → 월드 (−2,24))에서 봉합 방향(+z)으로 밀기
            player.transform.position = new Vector3(-2f, 1.5f, 24f);
            for (int i = 0; i < 3; i++) yield return null;

            float startY = player.transform.position.y;
            float maxZ = player.transform.position.z;
            float minY = startY;
            for (int i = 0; i < 30; i++)
            {
                cc.Move(new Vector3(0f, 0f, 0.15f));   // 총 4.5m 밀기 — 봉합 실패 시 문 라인 통과
                yield return null;
                maxZ = Mathf.Max(maxZ, player.transform.position.z);
                minY = Mathf.Min(minY, player.transform.position.y);
            }
            Cave156LevelProbe.State($"CHECK entrance_push startY={startY.ToString("F2", CultureInfo.InvariantCulture)} maxZ={maxZ.ToString("F2", CultureInfo.InvariantCulture)} minY={minY.ToString("F2", CultureInfo.InvariantCulture)}");

            // 관측 — 밀기 중지 후 중력 60프레임: 봉합 통과 시 바닥 없어 낙하한다(실내 클램프 게이트 부재 — 146차 수리 유지).
            //   minY 추적 = 가드 구조와 독립 판정(가드가 −3m에서 구조해도 낙하 사실은 minY로 남는다).
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                minY = Mathf.Min(minY, player.transform.position.y);
            }
            float y = player.transform.position.y;
            bool fell = minY < -2f;
            Cave156LevelProbe.State($"CHECK entrance_seal finalZ={player.transform.position.z.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"maxZ={maxZ.ToString("F2", CultureInfo.InvariantCulture)} minY={minY.ToString("F2", CultureInfo.InvariantCulture)} y={y.ToString("F2", CultureInfo.InvariantCulture)} fell={fell}");
            if (maxZ > 28.5f)
                Cave156LevelProbe.CheckFail($"step2 — 입구 블로커 통과(봉합 실패): maxZ={maxZ.ToString("F2", CultureInfo.InvariantCulture)}");
            if (fell)
                Cave156LevelProbe.CheckFail($"step2 — 낙하 발생(minY={minY.ToString("F2", CultureInfo.InvariantCulture)}) — 봉합/바닥 경로 이탈(가드 구조 여부와 무관)");
        }

        // ── 스텝3: CaveFallGuard 안전망 실측 — y 강하 → 스폰 복귀 ──
        private IEnumerator Step3_FallGuard()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) { Cave156LevelProbe.CheckFail("step3 — player 미발견"); yield break; }

            // [156차 진단] 가드 미복귀 원인 특정 — ①Player 태그 오브젝트 수/인스턴스 ②가드 캐시 대상 ③프레임별 샘플
            var allPlayers = GameObject.FindGameObjectsWithTag("Player");
            var roomForGuard = GameObject.Find("CaveRoom");
            var guard = roomForGuard != null ? roomForGuard.GetComponent<CaveFallGuard>() : null;
            var guardPlayerField = guard != null ? typeof(CaveFallGuard).GetField("_player",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) : null;
            object guardPlayer = guardPlayerField != null && guard != null ? guardPlayerField.GetValue(guard) : null;
            Cave156LevelProbe.State($"DIAG players={allPlayers.Length} probeId={player.GetInstanceID()} " +
                $"guardPlayerId={(guardPlayer is Transform gt ? gt.gameObject.GetInstanceID().ToString() : "null")}");
            foreach (var taggedPlayer in allPlayers)
                Cave156LevelProbe.State($"DIAG player pos={taggedPlayer.transform.position.ToString("F2", CultureInfo.InvariantCulture)} id={taggedPlayer.GetInstanceID()}");

            player.transform.position = new Vector3(0f, -5f, 0f);   // 홀 중앙 셀 하부 — 가드 판정선(−3m) 이하
            for (int i = 0; i < 40; i++)   // 쿨다운(1.0s 게임시간) 경과 포함 — 프레임별 15회 샘플
            {
                yield return null;
                if (i < 15)
                    Cave156LevelProbe.State($"DIAG f{i} pos={player.transform.position.ToString("F2", CultureInfo.InvariantCulture)} " +
                        $"time={Time.time.ToString("F2", CultureInfo.InvariantCulture)} frameCount={Time.frameCount}");
            }
            var p = player.transform.position;
            bool recovered = p.y > 0.5f;
            Cave156LevelProbe.State($"CHECK fallguard pos={p.ToString("F2", CultureInfo.InvariantCulture)} recovered={recovered} floor={CaveLevelDirector.CurrentFloor}");
            if (!recovered)
                Cave156LevelProbe.CheckFail($"step3 — 낙하 가드 미동작(y={p.y.ToString("F2", CultureInfo.InvariantCulture)} — 스폰 복귀 실패)");
        }

        // ── 스텝4: F1→F5 포탈 순회 — 층별 스폰 착지 + 포탈 존재 + 탑다운 캡처 ──
        private IEnumerator Step4_FloorTour()
        {
            for (int target = 2; target <= 5; target++)
            {
                var room = GameObject.Find("CaveRoom");
                if (room == null) { Cave156LevelProbe.CheckFail($"step4 F{target} — CaveRoom 미발견"); yield break; }
                Cave156LevelProbe.State($"step4 goDeeper before floor={CaveLevelDirector.CurrentFloor} → target={target}");
                CaveLevelDirector.GoDeeper(room);
                for (int i = 0; i < 15; i++) yield return null;   // 리빌드/텔레포트/콜라이더 정착 대기

                var room2 = GameObject.Find("CaveRoom");
                var player = GameObject.FindGameObjectWithTag("Player");
                if (room2 == null || player == null)
                {
                    Cave156LevelProbe.CheckFail($"step4 F{target} — 재빌드 후 room/player 미발견");
                    yield break;
                }
                bool floorOk = CaveLevelDirector.CurrentFloor == target;
                Cave156LevelProbe.State($"CHECK floor_current={CaveLevelDirector.CurrentFloor} expected={target} ok={floorOk}");
                if (!floorOk) Cave156LevelProbe.CheckFail($"step4 — 층 전환 실패(기대 {target})");

                var up = Cave156LevelProbe.FindNamed(room2, $"CavePortal_F{target}_Up");
                var down = Cave156LevelProbe.FindNamed(room2, $"CavePortal_F{target}_Down");
                Cave156LevelProbe.State($"CHECK f{target}_portals up={(up != null ? "yes" : "null")} down={(target < 5 ? (down != null ? "yes" : "null") : (down == null ? "absent-ok" : "unexpected"))}");
                if (up == null) Cave156LevelProbe.CheckFail($"step4 — F{target} 상승 포탈 미배치");
                if (target < 5 && down == null) Cave156LevelProbe.CheckFail($"step4 — F{target} 하강 포탈 미배치");
                if (target == 5 && down != null) Cave156LevelProbe.CheckFail("step4 — F5 최심부에 하강 포탈 존재(계약 위반)");

                int torches = room2.GetComponentsInChildren<TorchFlicker>(true).Length;
                Cave156LevelProbe.State($"CHECK f{target}_torches={torches}");
                // [156차 셀맵] 화로 F 마커는 F1 전용 — F2~F5는 벽횟불 8(≤9 상한), F1은 9(벽 8+화로 1).
                int expectedTorches = target == 1 ? 9 : 8;
                if (torches != expectedTorches)
                    Cave156LevelProbe.CheckFail($"step4 — F{target} TorchFlicker {expectedTorches} 기대: {torches}");

                for (int i = 0; i < 10; i++) yield return null;   // 착지 정착
                Cave156LevelProbe.State($"CHECK f{target}_spawn " + Cave156LevelProbe.FeetRaycast(player) +
                    $" pos={player.transform.position.ToString("F2", CultureInfo.InvariantCulture)}");

                Cave156LevelProbe.CaptureTopDown(room2, $"cave156_f{target}_topdown.png", false);
                Cave156LevelProbe.CaptureTopDown(room2, $"cave156_f{target}_topdown_bright.png", true);
            }
        }
    }

    /// <summary>[156차] Play 진입 도메인 리로드 후 펌프 재구독 부트 — 146차 선례 1:1.</summary>
    [InitializeOnLoad]
    internal static class Cave156ProbeBoot
    {
        static Cave156ProbeBoot()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("cave156_probe_active", false))
                    Cave156LevelProbe.ResubscribeAfterReload();
            };
        }
    }
}
