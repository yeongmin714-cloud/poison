using System.Collections;
using ProjectName.EditorTools;
using ProjectName.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [166차] MagicHit 방출 격리 실험 — A(원본팩 프리팹) / B(원본+AlwaysSimulate) / C(생산경로 PlayImpactMulti).
    /// RT 직접 렌더 캡처(69차 방식) — WaitForEndOfFrame 빈 백버퍼 실측 회피.
    /// </summary>
    public static class HitForensic166Probe
    {
        private const string Tag = "[히트F166]";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity";
        private const string SessionKey = "hitf166_active";
        private const float SetupTimeoutSec = 30f;
        private const float OverallTimeoutSec = 240f;

        internal static int StepErrors;
        private enum Phase { WaitingPlay, WaitingSetup, Running, Finished }
        private static Phase _phase = Phase.WaitingPlay;
        private static float _runStartRealtime = -1f;
        private static bool _pumpSubscribed;
        private static HitForensic166Runner _runner;

        [InitializeOnLoadMethod]
        private static void ResubscribeAfterReload()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            _runStartRealtime = (float)EditorApplication.timeSinceStartup;
            SubscribePump();
            Debug.Log($"{Tag} 펌프 재구독 완료");
        }

        public static void RunAll()
        {
            Debug.Log($"{Tag} === 격리 실험 시작 (A원본/BAlwaysSim/C생산) ===");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetBool(SessionKey, true);
                EditorApplication.isPlaying = true;
                SubscribePump();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
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

        private static void Pump()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (_runStartRealtime < 0f) _runStartRealtime = (float)EditorApplication.timeSinceStartup;
                float now = (float)EditorApplication.timeSinceStartup;
                if (now - _runStartRealtime > OverallTimeoutSec)
                {
                    Debug.LogError($"{Tag} FAIL — 전체 감시 초과");
                    StepErrors++;
                    Finish(StepErrors);
                    return;
                }
                if (_phase == Phase.WaitingPlay)
                {
                    if (!EditorApplication.isPlaying) return;
                    _phase = Phase.WaitingSetup;
                    _runStartRealtime = (float)EditorApplication.timeSinceStartup;
                }
                else if (_phase == Phase.WaitingSetup)
                {
                    if (GameObject.FindGameObjectWithTag("Player") == null)
                    {
                        if (now - _runStartRealtime > SetupTimeoutSec)
                        {
                            Debug.LogError($"{Tag} FAIL — 셋업 타임아웃");
                            StepErrors++;
                            Finish(StepErrors);
                        }
                        return;
                    }
                    if (_runner == null)
                    {
                        var host = new GameObject("HitForensic166RunnerHost");
                        _runner = host.AddComponent<HitForensic166Runner>();
                        _phase = Phase.Running;
                        Debug.Log($"{Tag} 러너 기동");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                StepErrors++;
                Finish(StepErrors);
            }
        }

        internal static void Finish(int errors)
        {
            if (_phase == Phase.Finished) return;
            _phase = Phase.Finished;
            Debug.Log($"{Tag} FINISH — StepErrors={errors}");
            SessionState.SetBool(SessionKey, false);
            EditorApplication.update -= Pump;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(errors > 0 ? 1 : 0);
        }
    }

    public class HitForensic166Runner : MonoBehaviour
    {
        private const string Tag = "[히트F166]";

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);

            var ais = Object.FindObjectsByType<AnimalAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in ais) if (m != null && m.enabled) m.enabled = false;

            Camera cam = null;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (c.CompareTag("MainCamera")) { cam = c; break; }
            var player = GameObject.FindGameObjectWithTag("Player");
            if (cam == null || player == null)
            {
                Debug.LogError($"{Tag} FAIL — 카메라/플레이어 미발견");
                HitForensic166Probe.StepErrors++;
                HitForensic166Probe.Finish(HitForensic166Probe.StepErrors);
                yield break;
            }

            Vector3 pPos = player.transform.position;
            Vector3 pFwd = player.transform.forward; pFwd.y = 0f;
            if (pFwd.sqrMagnitude < 0.001f) pFwd = Vector3.forward;
            pFwd.Normalize();
            Vector3 hitChest = new Vector3(pPos.x + pFwd.x * 2.2f, 0.9f, pPos.z + pFwd.z * 2.2f);

            Vector3 camPos = hitChest + new Vector3(0f, 1.6f, -3.2f);
            Quaternion camRot = Quaternion.LookRotation(hitChest - camPos);

            var rt = new RenderTexture(900, 640, 24);
            rt.Create();
            Camera captureCam = Object.Instantiate(cam.gameObject).GetComponent<Camera>();
            Object.Destroy(captureCam.GetComponent<AudioListener>());
            captureCam.transform.SetPositionAndRotation(camPos, camRot);
            captureCam.targetTexture = rt;
            captureCam.enabled = false;

            yield return new WaitForSeconds(0.2f);

            // ── A: 원본 팩 프리팹 직접 스폰 ──
            var srcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Matthew Guz/Hits Effects FREE/Prefab/Magic Hit 2.prefab");
            Debug.Log($"{Tag} A 원본프리팹 로드={(srcPrefab != null ? "OK" : "FAIL")}");
            if (srcPrefab != null)
                yield return RunVariant("A_raw", srcPrefab, hitChest, captureCam, rt, overrideCulling:false);

            // ── B: 원본 + cullingMode=AlwaysSimulate ──
            if (srcPrefab != null)
                yield return RunVariant("B_always", srcPrefab, hitChest, captureCam, rt, overrideCulling:true);

            // ── C: 생산 경로(PlayImpactMulti — 현재 리소스 복제본) ──
            CombatFXGate.PlayHitFX(hitChest, Vector3.back, CombatHitType.Organic, false, 20, Color.white);
            yield return null;
            yield return InventoryAndCapture("C_prod", captureCam, rt);
            yield return InventoryAndCapture("C_prod_f3", captureCam, rt);

            Debug.Log($"{Tag} 격리 실험 완료");
            HitForensic166Probe.Finish(HitForensic166Probe.StepErrors);
        }

        private IEnumerator RunVariant(string label, GameObject srcPrefab, Vector3 pos, Camera captureCam, RenderTexture rt, bool overrideCulling)
        {
            var inst = Object.Instantiate(srcPrefab, pos, Quaternion.identity);
            inst.name = $"HF166_{label}";
            int sysCount = 0;
            foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
            {
                sysCount++;
                if (overrideCulling)
                {
                    var main = ps.main;
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                }
            }
            Debug.Log($"{Tag} {label} 스폰 systems={sysCount} cullingOverride={overrideCulling}");

            // 첫 렌더(스폰 프레임) — 수동 렌더
            yield return null;
            InventoryAndCapture(label + "_f0", captureCam, rt);
            yield return null;
            yield return null;
            yield return InventoryAndCapture(label + "_f3", captureCam, rt);
            yield return new WaitForSeconds(0.25f);
            yield return InventoryAndCapture(label + "_fPlus", captureCam, rt);

            Object.Destroy(inst);
        }

        private YieldInstruction InventoryAndCapture(string label, Camera captureCam, RenderTexture rt)
        {
            int systems = 0, alive = 0;
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (ps.transform.root.name.StartsWith("HF166") || ps.transform.root.name.StartsWith("ImpactVFX"))
                {
                    systems++;
                    alive += ps.particleCount;
                    if (alive < 60)
                    {
                        var pr = ps.GetComponent<ParticleSystemRenderer>();
                        Debug.Log($"{Tag} {label} sys[{ps.name}] count={ps.particleCount} isPlaying={ps.isPlaying} isEmitting={ps.isEmitting} mat={(pr != null && pr.material != null ? pr.material.shader.name : "null")}");
                    }
                }
            }
            Debug.Log($"{Tag} === {label} 인벤토리: 시스템={systems} 생존입자={alive} ===");

            captureCam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(900, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 900, 640), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes($"C:/Unity/code/TestOutput/hf166_{label}.png", tex.EncodeToPNG());
            Object.Destroy(tex);
            return null;
        }
    }
}
