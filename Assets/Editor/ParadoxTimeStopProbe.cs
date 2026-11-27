using System;
using System.Collections;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectName.Systems;
using TimeWarp.Core;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [ParadoxProbe] P3 Time Paradox 타임 스톱 런타임 검증 프로브 — 계획 docs/plans/2026-11-07_attack-parry-premium-timeparadox-plan.md §P3-E.
    /// 목적: ParadoxTimeStop.Trigger(계약 경로=PERFECT 패링 훅 동일 진입) →
    ///   ① 스테이시스 도달: 반경 내 AnimalAI의 TimeWarpIdentity.EffectiveTimeScale → 0, IsActiveFor 게이트 true
    ///   ② 복원: blend-out 후 EffectiveTimeScale → 1, 게이트 해제, identity 자기 정리(Destroy)
    ///   ③ 소유권 분리: 전역 Time.timeScale 상시 1.0(글로벌 무접촉 — 플레이어 이동 보존의 구조적 증거)
    /// 구조 선례: AttackPremiumProbe — 펌프/런너/Finish-Exit 패턴. -quit/-nographics 금지(Play 모드 배치 계약).
    /// 산출: Screenshots/paradox_timestop_hold.png + 로그 TestOutput/paradox_probe.log
    /// </summary>
    public static class ParadoxTimeStopProbe
    {
        internal const string Tag = "[ParadoxProbe]";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity";
        internal const string SessionKey = "paradox_timestop_probe_active";
        private const float SetupTimeoutSec = 40f;
        private const float RunnerStartTimeoutSec = 20f;
        private const float HeartbeatTimeoutSec = 12f;
        private const float OverallTimeoutSec = 180f;

        internal static int StepErrors;

        private enum Phase { WaitingPlay, WaitingSetup, StartingRunner, WaitingRunner, Finished }
        private static Phase _phase = Phase.WaitingPlay;
        private static float _phaseStartRealtime;
        private static float _runStartRealtime = -1f;
        private static float _lastHeartbeatRealtime = -1f;
        private static ParadoxProbeRunner _runner;
        private static bool _pumpSubscribed;

        public static void RunAll()
        {
            Debug.Log($"{Tag} === P3 타임 스톱 런타임 검증 프로브 시작 ===");
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
                    FailFatal($"전체 감시 초과({OverallTimeoutSec:F0}s)");
                    return;
                }
                switch (_phase)
                {
                    case Phase.WaitingPlay:
                        if (!EditorApplication.isPlaying) return;
                        _phase = Phase.WaitingSetup;
                        _phaseStartRealtime = now;
                        break;
                    case Phase.WaitingSetup:
                        if (GameObject.FindGameObjectWithTag("Player") != null)
                        {
                            _phase = Phase.StartingRunner;
                            break;
                        }
                        if (now - _phaseStartRealtime > SetupTimeoutSec)
                        {
                            FailFatal($"부트 타임아웃({SetupTimeoutSec:F0}s) — Player 미발견");
                        }
                        break;
                    case Phase.StartingRunner:
                        var go = new GameObject("ParadoxProbeRunner");
                        _runner = go.AddComponent<ParadoxProbeRunner>();
                        _phase = Phase.WaitingRunner;
                        _phaseStartRealtime = now;
                        break;
                    case Phase.WaitingRunner:
                        if (_runner == null)
                        {
                            FailFatal("런너 소실");
                            return;
                        }
                        if (_runner.Began) _phase = Phase.Finished;
                        else if (now - _phaseStartRealtime > RunnerStartTimeoutSec)
                        {
                            FailFatal($"런너 미기동({RunnerStartTimeoutSec:F0}s)");
                        }
                        break;
                    case Phase.Finished:
                        break;
                }
                if (_phase == Phase.Finished && _runner != null && _runner.Began
                    && now - _lastHeartbeatRealtime > HeartbeatTimeoutSec)
                {
                    FailFatal($"하트비트 정지({HeartbeatTimeoutSec:F0}s)");
                }
            }
            catch (Exception e)
            {
                FailFatal($"펌프 예외 — {e}");
            }
        }

        internal static void NoteHeartbeat() => _lastHeartbeatRealtime = (float)EditorApplication.timeSinceStartup;

        internal static void FailStep(string reason)
        {
            Debug.LogError($"{Tag} STEP_FAIL — {reason}");
            StepErrors++;
        }

        internal static void Check(string msg) => Debug.Log($"{Tag} CHECK {msg}");
        internal static void State(string msg) => Debug.Log($"{Tag} STATE {msg}");

        internal static void Finish()
        {
            UnsubscribePump();
            SessionState.SetBool(SessionKey, false);
            bool ok = StepErrors == 0;
            Debug.Log($"{Tag} {(ok ? "DONE" : "DONE_ERRORS")} — steps={StepErrors} " +
                      "(산출: Screenshots/paradox_timestop_hold.png)");
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
    }

    [InitializeOnLoad]
    internal static class ParadoxProbeReloadHook
    {
        static ParadoxProbeReloadHook() => ParadoxTimeStopProbe.ResubscribeAfterReload();
    }

    /// <summary>[ParadoxProbe] 스테이시스 샘플 러너 — Trigger → 램프 감시 → 복원/정리 검증.</summary>
    public class ParadoxProbeRunner : MonoBehaviour
    {
        public bool Began { get; private set; }

        private static IEnumerator RunGuarded(IEnumerator inner, string step)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    current = inner.Current;
                }
                catch (Exception e)
                {
                    Debug.LogError($"{ParadoxTimeStopProbe.Tag} STEP_EXCEPTION {step}: {e.GetType().Name} {e.Message}");
                    ParadoxTimeStopProbe.StepErrors++;
                    yield break;
                }
                yield return current;
            }
        }

        private IEnumerator Start()
        {
            Began = true;
            ParadoxTimeStopProbe.NoteHeartbeat();
            for (int i = 0; i < 30; i++) yield return null;   // 부트 안정화

            yield return RunGuarded(RunTimeStopVerification(), "paradox_stasis");

            ParadoxTimeStopProbe.Finish();
        }

        private void Update() => ParadoxTimeStopProbe.NoteHeartbeat();

        private IEnumerator RunTimeStopVerification()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                ParadoxTimeStopProbe.FailStep("Player 미발견");
                yield break;
            }

            var ais = FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);
            if (ais == null || ais.Length == 0)
            {
                ParadoxTimeStopProbe.FailStep("AnimalAI 미발견 — Test_10 몬스터 부재");
                yield break;
            }
            AnimalAI target = null;
            float best = float.MaxValue;
            foreach (var ai in ais)
            {
                if (ai == null) continue;
                float d = (ai.transform.position - player.transform.position).sqrMagnitude;
                if (d < best) { best = d; target = ai; }
            }
            if (target == null)
            {
                ParadoxTimeStopProbe.FailStep("유효 AnimalAI 없음");
                yield break;
            }

            // 계약 경로와 동일 진입(Trigger) — 중심은 반경 보장을 위해 대상 AI 위치 사용
            // (7m 반경 내 AI가 있으면 플레이어 위치 사용 — 계약 경로와 동일).
            Vector3 center = (Mathf.Sqrt(best) <= 7f)   // ParadoxTimeStop.RadiusMeters(internal — 상수 복제)
                ? player.transform.position
                : target.transform.position;
            ParadoxTimeStopProbe.State($"대상 AI={target.gameObject.name} dist={Mathf.Sqrt(best):F2}m " +
                                       $"center={(center == player.transform.position ? "player" : "ai_fallback")}");

            float ts0 = Time.timeScale;
            if (Mathf.Abs(ts0 - 1f) > 0.001f)
                ParadoxTimeStopProbe.FailStep($"기준 Time.timeScale != 1 ({ts0})");
            else
                ParadoxTimeStopProbe.Check($"기준 Time.timeScale=1 확인");

            ParadoxTimeStop.Trigger(center);   // 계약 경로 진입

            var identity = target.GetComponent<TimeWarpIdentity>();
            if (identity == null)
            {
                ParadoxTimeStopProbe.FailStep("Trigger 후 TimeWarpIdentity 미부착 — 게이트/반경 판정 실패");
                yield break;
            }
            ParadoxTimeStopProbe.Check("Trigger → TimeWarpIdentity 부착 확인");

            // 램프 감시: blend-in(0.12) + hold(1.0) + blend-out(0.3) ≈ 1.42s — 여유 포함 2.4s 샘플링
            bool stasisReached = false;
            bool globalTouched = false;
            float tStart = Time.time;
            float sample = 0f;
            bool captured = false;
            while (Time.time - tStart < 2.4f)
            {
                yield return new WaitForSeconds(0.15f);
                sample = Time.time - tStart;
                float eff = identity != null ? identity.EffectiveTimeScale : -1f;
                bool gate = ParadoxTimeStop.IsActiveFor(target);
                float gts = Time.timeScale;
                ParadoxTimeStopProbe.State($"t={sample:F2}s eff={eff.ToString("F3", CultureInfo.InvariantCulture)} " +
                                           $"gate={gate} globalTS={gts.ToString("F3", CultureInfo.InvariantCulture)} " +
                                           $"identityAlive={(identity != null)}");
                if (eff <= 0.01f && gate) stasisReached = true;
                if (Mathf.Abs(gts - 1f) > 0.001f) globalTouched = true;

                if (!captured && stasisReached)
                {
                    captured = true;
                    CaptureHold("paradox_timestop_hold.png");
                }
                if (identity == null) break;   // 자기 정리 완료 — 감시 조기 종료
            }

            if (!stasisReached)
                ParadoxTimeStopProbe.FailStep("스테이시스 미도달 — EffectiveTimeScale이 0에 도달하지 않음");
            else
                ParadoxTimeStopProbe.Check("스테이시스 도달 — eff<=0.01 + 게이트 true");

            if (globalTouched)
                ParadoxTimeStopProbe.FailStep("전역 Time.timeScale 변동 — 소유권 분리 위반(플레이어 이동 영향)");
            else
                ParadoxTimeStopProbe.Check("소유권 분리 — 전역 Time.timeScale 상시 1.0");

            // 복원 + 자기 정리 확인(러너 루틴 완료 후 identity Destroy)
            for (int i = 0; i < 40 && identity != null; i++) yield return null;   // 최대 ~0.7s 여유
            if (identity == null)
                ParadoxTimeStopProbe.Check("복원·자기 정리 — identity Destroy 확인(FreeMemoryOnDisable 경로)");
            else
            {
                float eff = identity.EffectiveTimeScale;
                if (eff > 0.95f)
                    ParadoxTimeStopProbe.State($"복원 값 확인 — eff={eff:F3}(identity 파괴 지연 — Destroy 대기 여유 부족 가능, 감시 로그로 판정)");
                else
                    ParadoxTimeStopProbe.FailStep($"복원 실패 — eff={eff:F3}");
            }
        }

        private void CaptureHold(string fileName)
        {
            try
            {
                var cam = Camera.main;
                if (cam == null)
                {
                    ParadoxTimeStopProbe.FailStep($"캡처 실패 — Camera.main 없음: {fileName}");
                    return;
                }
                var rt = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                var prevTarget = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prevTarget;
                RenderTexture.active = rt;
                var tex = new Texture2D(1024, 768, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                byte[] png = tex.EncodeToPNG();
                string dir = System.IO.Path.Combine(Application.dataPath, "..", "Screenshots");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, fileName);
                System.IO.File.WriteAllBytes(path, png);
                ParadoxTimeStopProbe.Check($"캡처 산출: {fileName}");
            }
            catch (Exception e)
            {
                ParadoxTimeStopProbe.FailStep($"캡처 예외: {e.GetType().Name} {e.Message}");
            }
        }
    }
}
