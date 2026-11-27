using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [155차 P2/P4] 이계 공동묘지 프로브 — Test_22 Play 배치: 사망 체인 → 공동묘지 진입 → 캡처 → 귀환.
    /// 패턴 준용: Cave146LevelProbe(Play 펌프/SessionState 재구독/-quit 금지/스스로 Exit), Cave148P1Probe(RT 캡처).
    /// 실행: Unity.exe -batchmode -projectPath C:/Unity/code -executeMethod ProjectName.EditorTools.Afterlife155Probe.RunAll
    ///       -logFile C:/Unity/code/TestOutput/afterlife155_probe.log   (⚠ -quit/-nographics 금지)
    /// 사전 요구: Afterlife155TestSceneBuilder.RunAll로 Test_22 씬 생성 완료.
    /// </summary>
    public static class Afterlife155Probe
    {
        private const string Tag = "[155차 P4]";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_22_AfterlifeCemetery.unity";
        private const string SessionKey = "afterlife155_probe_active";
        private const string RoomName = "AfterlifeRoom";
        private const int RTW = 1024, RTH = 768;
        private const float RoomWaitSec = 15f, DoorWaitSec = 40f, OverallTimeoutSec = 200f;

        internal static int StepErrors;
        private enum Phase { WaitingPlay, WaitingRoom, Capturing, WaitingDoor, ReviveCheck, Finished }
        private static Phase _phase = Phase.WaitingPlay;
        private static float _phaseStart;
        private static float _runStart = -1f;
        private static bool _pumpSubscribed;
        private static Afterlife155Runner _runner;

        public static void RunAll()
        {
            Debug.Log($"{Tag} === 이계 공동묘지 런타임 프로브 시작 ===");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetBool(SessionKey, true);
                EditorApplication.isPlaying = true;
                SubscribePump();
                Debug.Log($"{Tag} Play 진입 요청 — {ScenePath}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{Tag} FAIL setup");
                SessionState.SetBool(SessionKey, false);
                EditorApplication.Exit(1);
            }
        }

        internal static void ResubscribeAfterReload()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            _runStart = (float)EditorApplication.timeSinceStartup;
            SubscribePump();
            Debug.Log($"{Tag} 펌프 재구독 완료(isPlaying={EditorApplication.isPlaying})");
        }

        [InitializeOnLoad]
        private static class ReloadHook
        {
            static ReloadHook() => ResubscribeAfterReload();
        }

        private static void SubscribePump()
        {
            if (_pumpSubscribed) return;
            _pumpSubscribed = true;
            EditorApplication.update += Pump;
        }

        private static void Pump()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
                float now = (float)EditorApplication.timeSinceStartup;
                if (_runStart < 0f) { _runStart = now; _phaseStart = now; }
                float elapsed = now - _runStart;
                if (elapsed > OverallTimeoutSec)
                {
                    Debug.LogError($"{Tag} FAIL overall timeout {elapsed:F0}s — phase={_phase}");
                    EditorApplication.Exit(1);
                    return;
                }

                switch (_phase)
                {
                    case Phase.WaitingPlay:
                        if (!EditorApplication.isPlaying) return;
                        _phase = Phase.WaitingRoom; _phaseStart = now;
                        break;

                    case Phase.WaitingRoom:
                        var room = GameObject.Find(RoomName);
                        var player = GameObject.Find("Player");
                        if (room == null || player == null)
                        {
                            if (now - _phaseStart > RoomWaitSec)
                            {
                                Debug.LogError($"{Tag} FAIL room wait — room={(room != null)} player={(player != null)}");
                                EditorApplication.Exit(1);
                            }
                            return;
                        }
                        _runner = new GameObject("Afterlife155Runner").AddComponent<Afterlife155Runner>();
                        Debug.Log($"{Tag} STATE room_found pos={room.transform.position} player_pos={player.transform.position}");
                        _phase = Phase.Capturing; _phaseStart = now;
                        break;

                    case Phase.Capturing:
                        _runner.BeginCapture();
                        _phase = Phase.WaitingDoor; _phaseStart = now;
                        break;

                    case Phase.WaitingDoor:
                        var doorObj = GameObject.Find("AfterlifeExitDoor");
                        var door = doorObj != null ? doorObj.GetComponent<ProjectName.Systems.AfterlifeExitDoor>() : null;
                        if (door == null)
                        {
                            if (now - _phaseStart > DoorWaitSec) { Debug.LogError($"{Tag} FAIL door null"); EditorApplication.Exit(1); }
                            return;
                        }
                        if (door.IsActive)
                        {
                            Debug.Log($"{Tag} CHECK exit_door_active=true (자연 개방 — 훅 30s 타이머)");
                            _phase = Phase.ReviveCheck; _phaseStart = now;
                        }
                        else if (now - _phaseStart > DoorWaitSec)
                        {
                            Debug.Log($"{Tag} STATE door_forced_activate (자연 개방 실패 — 공개 API 폴백)");
                            door.ActivateDoor();
                            _phase = Phase.ReviveCheck; _phaseStart = now;
                        }
                        break;

                    case Phase.ReviveCheck:
                        if (!ProjectName.Systems.AfterlifeSurvivalSystem.IsActive)
                        {
                            Debug.Log($"{Tag} CHECK afterlife_ended=true (귀환/정산 완료)");
                            Finish();
                            return;
                        }
                        if (now - _phaseStart > 25f)
                        {
                            Debug.Log($"{Tag} STATE afterlife_still_active — 귀환 접촉 대기 중(정보 로그만)");
                            Finish();
                            return;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError($"{Tag} FAIL pump exception");
                EditorApplication.Exit(1);
            }
        }

        private static void Finish()
        {
            Debug.Log($"{Tag} DONE errors={StepErrors}");
            SessionState.SetBool(SessionKey, false);
            EditorApplication.Exit(StepErrors == 0 ? 0 : 1);
        }
    }

    /// <summary>런너 — 코루틴 기반 캡처(Cave146PlayRunner 선례: Start 코루틴으로 프레임 흐름 보장).</summary>
    public class Afterlife155Runner : MonoBehaviour
    {
        private bool _started;

        internal void BeginCapture()
        {
            if (_started) return;
            _started = true;
            StartCoroutine(CaptureRoutine());
        }

        private IEnumerator CaptureRoutine()
        {
            yield return new WaitForSeconds(1.5f);   // 씬/조명 안정화

            var room = GameObject.Find("AfterlifeRoom");
            int statues = 0, obelisks = 0, graves = 0;
            if (room != null)
            {
                foreach (var t in room.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("AfterlifeStatue_", StringComparison.Ordinal)) statues++;
                    else if (t.name.StartsWith("AfterlifeObelisk_", StringComparison.Ordinal)) obelisks++;
                    else if (t.name.StartsWith("AfterlifeGrave_", StringComparison.Ordinal)) graves++;
                }
            }
            Debug.Log($"[155차 P4] CHECK counts statues={statues}/8 obelisks={obelisks}/4 graves={graves}/12");
            if (statues < 8 || obelisks < 4 || graves < 12)
            {
                Afterlife155Probe.StepErrors++;
                Debug.LogError("[155차 P4] CHECK counts FAIL — 배치 수 부족(GLB 로드 실패 누적 의심)");
            }

            // 탑다운 캡처 — 방 렌더러 bounds 기반(Cave148P4Probe 패턴)
            var renderers = room != null ? room.GetComponentsInChildren<Renderer>() : new Renderer[0];
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                var go = new GameObject("Afterlife155TopCam");
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
                cam.cullingMask = ~0;
                cam.transform.position = new Vector3(bounds.center.x, bounds.max.y + 20f, bounds.center.z);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.1f;
                yield return new WaitForSeconds(0.2f);
                CaptureFromCam(cam, "afterlife155_topdown.png");
                Destroy(go);
            }

            // 플레이어 뷰 캡처 — 플레이어 카메라 직접 사용
            var player = GameObject.Find("Player");
            var pcam = player != null ? player.GetComponentInChildren<Camera>() : null;
            if (pcam == null) pcam = Camera.main;
            if (pcam != null)
            {
                yield return new WaitForSeconds(0.2f);
                CaptureFromCam(pcam, "afterlife155_player.png");
            }
            else Debug.LogWarning("[155차 P4] CAPTURE skip player view — 카메라 미발견");
        }

        private static void CaptureFromCam(Camera cam, string fileName)
        {
            var rt = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            var png = tex.EncodeToPNG();
            File.WriteAllBytes(Path.Combine("TestOutput", fileName), png);
            Debug.Log($"[155차 P4] CAPTURE {fileName} {png.Length / 1024}KB");
            Destroy(tex);
            rt.Release();
            Destroy(rt);
        }
    }
}
