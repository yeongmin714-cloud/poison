using System;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ProjectName.Systems;
using ProjectName.UI;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [IndoorTour23] 실내 관람 모드 배치 프로브 — 진단 전용(프로덕션 무영향).
    ///
    /// 목적: Test_23_IndoorTour 부트 → 관람 드라이버(IndoorTourMode.ActiveDriver) 대기 →
    ///   cave_f1(부트 상태 캡처) → cave_f2 → house → castle_player 4스텝 자동 순회에서
    ///   ①전환 성공(SETTLED 로그) ②활성 카메라가 탑다운(IndoorCameraFollow.PitchOverrideDeg==90) 유지
    ///   ③플레이어 뷰 RT 캡처(Screenshots/tour23_*.png)를 실측한다.
    ///
    /// 계약: 실제 경로만 사용 — DebugGoToStart는 IndoorTourMode.GoTo(검증된 EnterBuilding/GoDeeper/재빌드 경로)의
    ///   프로브 기동 래퍼이며 캡처를 우회하지 않는다(훅 자연 발화). arenapit 관전 세션은 본 프로브 범위 외.
    ///
    /// 실행(부모 몫): run_indoor_tour23_probe.sh — -executeMethod ProjectName.EditorTools.IndoorTourMode23Probe.RunAll
    ///   ⚠ -quit 금지(Play 모드 배치 계약 — 프로브가 스스로 EditorApplication.Exit), -nographics 금지(RT 캡처).
    /// 산출: Screenshots/tour23_*.png + 로그 STATE/CHECK/CAPTURE/최종 DONE(Exit 0)/FAIL(Exit 1).
    /// 생존 설계: 런너 호스트 DontDestroyOnLoad — 월드 언로드(EnterBuilding마다 실측)에서 소실 금지
    ///   (구 프로브 "러너 소실 재발 2회" 교훈의 구조적 해법 — IndoorTourMode 드라이버와 동일 패턴).
    /// </summary>
    public static class IndoorTourMode23Probe
    {
        internal const string Tag = "[IndoorTour23]";
        internal const string SessionKey = "indoor_tour23_probe_active";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_23_IndoorTour.unity";
        private const string ProbeCamPrefix = "IndoorTour23Probe";
        private const int RTW = 1024;
        private const int RTH = 768;
        private const float SetupTimeoutSec = 30f;        // CaveInteriorTestSetup 부트(CaveRoom+Player) 대기
        private const float DriverTimeoutSec = 40f;       // 관람 드라이버(부트+이관) 대기
        private const float RunnerStartTimeoutSec = 20f;  // 런너 미기동 판정
        private const float HeartbeatTimeoutSec = 25f;    // 무응답 중도 사망 판정
        private const float OverallTimeoutSec = 420f;     // 전체 감시
        private const float StepBusyTimeoutSec = 90f;     // 스텝 1건(전환+정착) 상한
        private const int SettleFrames = 60;              // 전환 완료 후 추가 정착

        /// <summary>스텝 격리 실패 카운트 — 0이 아니면 최종 Exit(1).</summary>
        internal static int StepErrors;

        private enum Phase { WaitingPlay, WaitingSetup, StartingRunner, WaitingRunner, Finished }
        private static Phase _phase = Phase.WaitingPlay;
        private static float _phaseStartRealtime;
        private static float _runStartRealtime = -1f;
        private static float _lastHeartbeatRealtime = -1f;
        private static Runner _runner;
        private static bool _pumpSubscribed;

        /// <summary>배치 런 진입점 — 씬 오픈 → Play 진입 요청. 이후 펌프/런너가 이어받는다.</summary>
        public static void RunAll()
        {
            Debug.Log($"{Tag} === 실내 관람 모드 프로브 시작 ===");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetBool(SessionKey, true);   // 리로드 생존 — InitializeOnLoad가 펌프 재구독
                EditorApplication.isPlaying = true;
                SubscribePump();
                Debug.Log($"{Tag} Play 진입 요청 — {ScenePath}");
            }
            catch (Exception e)
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

        private static void UnsubscribePump()
        {
            _pumpSubscribed = false;
            EditorApplication.update -= Pump;
        }

        /// <summary>Play 진입 도메인 리로드 후 펌프 재구독 — SessionState 플래그로 본 런만 이어받기.</summary>
        internal static void ResubscribeAfterReload()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            _runStartRealtime = (float)EditorApplication.timeSinceStartup;
            SubscribePump();
            Debug.Log($"{Tag} 펌프 재구독 — 리로드 후 이어받기(isPlaying={EditorApplication.isPlaying})");
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
                    FailFatal($"전체 감시 초과({OverallTimeoutSec:F0}s) — 무한 대기 차단");
                    return;
                }
                switch (_phase)
                {
                    case Phase.WaitingPlay:
                        if (!EditorApplication.isPlaying) return;
                        EnterPhase(Phase.WaitingSetup);
                        break;
                    case Phase.WaitingSetup:
                        if (GameObject.Find("CaveRoom") != null &&
                            GameObject.FindGameObjectWithTag("Player") != null)
                        {
                            State("setup — CaveRoom+Player 부트 완료");
                            EnterPhase(Phase.StartingRunner);
                        }
                        else if (now - _phaseStartRealtime > SetupTimeoutSec)
                        {
                            FailFatal($"부트 타임아웃({SetupTimeoutSec:F0}s) — CaveRoom/Player 미발견");
                        }
                        break;
                    case Phase.StartingRunner:
                    {
                        var go = new GameObject("IndoorTour23RunnerHost");
                        UnityEngine.Object.DontDestroyOnLoad(go);   // 월드 언로드 생존 — 드라이버 동일 패턴
                        _runner = go.AddComponent<Runner>();
                        EnterPhase(Phase.WaitingRunner);
                        break;
                    }
                    case Phase.WaitingRunner:
                        if (_runner == null)
                        {
                            FailFatal("런너 소실 — DDOL 파괴 감지");
                            return;
                        }
                        if (_runner.Began) break;   // 런너가 코루틴 시퀀스 주도 — Finish에서 종료
                        if (now - _phaseStartRealtime > RunnerStartTimeoutSec)
                        {
                            FailFatal($"런너 미기동({RunnerStartTimeoutSec:F0}s) — 배치 Play 펌프 불가 판정");
                        }
                        break;
                    case Phase.Finished:
                        break;
                }
                if (_phase == Phase.WaitingRunner)
                {
                    if (_lastHeartbeatRealtime < 0f) _lastHeartbeatRealtime = now;
                    else if (now - _lastHeartbeatRealtime > HeartbeatTimeoutSec)
                        FailFatal($"하트비트 정지({HeartbeatTimeoutSec:F0}s) — 런너/플레이어 루프 중도 사망");
                }
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
        }

        internal static void NoteHeartbeat() => _lastHeartbeatRealtime = (float)EditorApplication.timeSinceStartup;

        internal static void Finish()
        {
            UnsubscribePump();
            SessionState.SetBool(SessionKey, false);
            bool ok = StepErrors == 0;
            Debug.Log($"{Tag} {(ok ? "DONE" : "DONE_ERRORS")} — steps={StepErrors} " +
                      $"(산출: Screenshots/tour23_*.png)");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void FailFatal(string reason)
        {
            UnsubscribePump();
            SessionState.SetBool(SessionKey, false);
            Debug.LogError($"{Tag} FAIL — {reason} (steps={StepErrors})");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(1);
        }

        // ── 로그 헬퍼(ASCII 프리픽스 — 인코딩 무관) ──
        internal static void State(string msg) => Debug.Log($"{Tag} STATE {msg}");
        internal static void Check(string msg) => Debug.Log($"{Tag} CHECK {msg}");
        internal static void Captured(string fileName) => Debug.Log($"{Tag} CAPTURE {fileName}");

        // ── Play 모드 투어 런너 — 코루틴으로 4스텝 시퀀스 수행 ──
        public class Runner : MonoBehaviour
        {
            /// <summary>펌프가 런너 기동을 확인하는 플래그(Start 첫 줄 세팅).</summary>
            public bool Began { get; private set; }

            private void Update() => IndoorTourMode23Probe.NoteHeartbeat();

            private IEnumerator Start()
            {
                Began = true;
                IndoorTourMode23Probe.NoteHeartbeat();

                // 드라이버 대기 — 부트+이관(카메라 구성 포함) 완료 표식.
                float t0 = Time.realtimeSinceStartup;
                while (IndoorTourMode.ActiveDriver == null)
                {
                    if (Time.realtimeSinceStartup - t0 > DriverTimeoutSec)
                    {
                        IndoorTourMode23Probe.FailFatal($"드라이버 타임아웃({DriverTimeoutSec:F0}s) — ActiveDriver 미생성");
                        yield break;
                    }
                    yield return null;
                }
                IndoorTourMode driver = IndoorTourMode.ActiveDriver;
                IndoorTourMode23Probe.State($"driver ready — busy={driver.IsBusy} " +
                                            $"enabled={driver.enabled} active={driver.gameObject.activeInHierarchy} " +
                                            $"goName={driver.gameObject.name} scene={driver.gameObject.scene.name}");

                // 카메라 전수 스캔 — (0,0,0) 정지 원인 판별용(활성/비활성 전부).
                foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(
                    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                {
                    var f = c.GetComponent<IndoorCameraFollow>();
                    IndoorTourMode23Probe.State(
                        $"cam_scan name={IndoorTourMode23Probe.Describe(c.gameObject)} " +
                        $"goActive={c.gameObject.activeInHierarchy} camEnabled={c.enabled} " +
                        $"tag={(c.CompareTag("MainCamera") ? "MainCamera" : "-")} " +
                        $"follow={(f != null)} followEnabled={(f != null && f.enabled)} " +
                        $"ortho={c.orthographic} pos={c.transform.position.ToString("F2")} " +
                        $"scene={c.gameObject.scene.name}");
                }

                // 스텝0: cave_f1(부트 상태) — 캡처+탑다운 판정.
                yield return SettleCaptureCheck("cave_f1");

                // 스텝1~3: cave_f2(GoDeeper) → house(EnterBuilding+월드 언로드) → castle_player(실내→실내).
                var targets = new[] { (1, "cave_f2"), (2, "house"), (3, "castle_player") };
                foreach (var (index, label) in targets)
                {
                    IndoorTourMode23Probe.State($"go {label} (index {index})");
                    driver.DebugGoToStart(index);

                    float t1 = Time.realtimeSinceStartup;
                    while (driver != null && driver.IsBusy &&
                           Time.realtimeSinceStartup - t1 < StepBusyTimeoutSec)
                        yield return null;
                    if (driver == null)
                    {
                        IndoorTourMode23Probe.FailFatal($"드라이버 소실 — {label} 전환 중");
                        yield break;
                    }
                    if (driver.IsBusy)
                    {
                        IndoorTourMode23Probe.FailFatal($"스텝 타임아웃({StepBusyTimeoutSec:F0}s) — {label}");
                        yield break;
                    }

                    yield return SettleCaptureCheck(label);
                }

                IndoorTourMode23Probe.Finish();
            }

            /// <summary>정착 → 원근 57° 판정 → 뷰 캡처(공용 마무리).</summary>
            private static IEnumerator SettleCaptureCheck(string label)
            {
                for (int i = 0; i < SettleFrames; i++) yield return null;

                // [배치 후 캡처] 코루틴 컨텍스트는 Update직후(LateUpdate 전) — 드라이버 배치를 즉시 재적용.
                var d = IndoorTourMode.ActiveDriver;
                if (d != null) d.DebugPlaceCameraNow();

                Camera cam = FindActiveCamera();
                GameObject camGO = cam != null ? cam.gameObject : null;
                var follow = camGO != null ? camGO.GetComponent<IndoorCameraFollow>() : null;
                // [카메라통일 57°] 관람 카메라 판정 — 원근(비직교) + 탑다운 오버라이드 미사용(≤0 = 기본 57° 경로).
                bool tourCam57 = follow != null &&
                                 follow.PitchOverrideDeg <= 0f && !cam.orthographic;
                var player = GameObject.FindGameObjectWithTag("Player");
                string ppos = player != null ? player.transform.position.ToString("F2") : "null";
                string cpos = camGO != null ? camGO.transform.position.ToString("F2") : "null";
                IndoorTourMode23Probe.Check($"{label} cam={IndoorTourMode23Probe.Describe(camGO)} " +
                                            $"follow={follow != null} pitch={(follow != null ? follow.PitchOverrideDeg : -1f):F1} " +
                                            $"ortho={cam != null && cam.orthographic} tour57={tourCam57} " +
                                            $"suppressed={ProjectName.Systems.PlayerMovement.CameraFollowSuppressed} " +
                                            $"camPos={cpos} playerPos={ppos} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                int n = 0;
                foreach (var c2 in UnityEngine.Object.FindObjectsByType<Camera>(
                    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                {
                    n++;
                    IndoorTourMode23Probe.State(
                        $"cam_check{label} name={IndoorTourMode23Probe.Describe(c2.gameObject)} " +
                        $"active={c2.gameObject.activeInHierarchy} tag={(c2.CompareTag("MainCamera") ? "M" : "-")} " +
                        $"pos={c2.transform.position.ToString("F2")} scene={c2.gameObject.scene.name}");
                }
                IndoorTourMode23Probe.State($"cam_count={n}");
                if (!tourCam57)
                    IndoorTourMode23Probe.StepErrors++;

                IndoorTourMode23Probe.CaptureFromCamera(cam, $"tour23_{label}_view.png");
                yield return null;
            }
        }

        // ── 캡처/탐색 헬퍼(34차 Farm·투어 프로브 선례 — RT+cam.Render+ReadPixels) ──
        internal static string Describe(UnityEngine.Object o) => o != null ? o.name : "null";

        /// <summary>활성 카메라 탐색 — MainCamera 태그 우선 → 셸 IndoorCamera → 기타 활성 카메라(프로브 자신 제외).</summary>
        internal static Camera FindActiveCamera()
        {
            Camera best = null;
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            {
                if (cam.gameObject.name.StartsWith(ProbeCamPrefix, StringComparison.Ordinal)) continue;
                if (!cam.isActiveAndEnabled) continue;
                if (cam.CompareTag("MainCamera")) return cam;
                if (cam.gameObject.name == "IndoorCamera") return cam;   // 셸 우선 — 후속16 구동 주체 계약
                if (best == null) best = cam;
            }
            return best;
        }

        internal static void CaptureFromCamera(Camera cam, string fileName)
        {
            if (cam == null)
            {
                Debug.LogError($"{Tag} FAIL capture — 카메라 미발견: {fileName}");
                StepErrors++;
                return;
            }
            var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            try
            {
                cam.Render();
                SaveRt(rt, fileName);
                Captured(fileName);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static void SaveRt(RenderTexture rt, string fileName)
        {
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "Screenshots");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, fileName), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    /// <summary>[IndoorTour23] 리로드 재구독 부트 — SessionState 플래그로 본 런만 이어받기(선례 계약).</summary>
    [InitializeOnLoad]
    internal static class IndoorTourMode23ProbeBoot
    {
        static IndoorTourMode23ProbeBoot()
        {
            if (!SessionState.GetBool(IndoorTourMode23Probe.SessionKey, false)) return;
            EditorApplication.delayCall += () => IndoorTourMode23Probe.ResubscribeAfterReload();
        }
    }
}
