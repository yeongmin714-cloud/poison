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
    /// [PremiumProbe] 공격/패링 프리미엄 배치 진단 프로브 — docs/plans/2026-11-07_attack-parry-premium-timeparadox-plan.md P0/P4.
    /// 목적:
    ///   ① P0 — 플레이어 흰색 무광 실루엣 원인 진단: 플레이어 계층 전 렌더러 머티리얼 덤프
    ///      (머티리얼명/셰이더명/_BaseMap·_MainTex 텍스처 바인딩 유무/베이스 컬러) → 로그로 원인 특정.
    ///   ② P1 — 프리미엄 텔레그래프 링 캡처: AttackTelegraphIndicator.Spawn(공용 경로 그대로) →
    ///      fill 초기/후기/종료 플래시 3컷 + 자가 파괴 확인(셰이더 경로 실사용 검증).
    ///   ③ P2 — 패링 화이트 프레임 캡처: ParryFlashFader 카메라 자식 풀스크린 쿼드 진단 생성(지연 0.5s — 캡처 가독성 전용) 후 1컷.
    /// 구조 선례: Cave156LevelProbe/IndoorTourProbe — 펌프/런너/Finish-Exit 패턴. -quit/-nographics 금지(Play 모드 배치 계약).
    /// 산출: Screenshots/premium_player_view.png, premium_ring_fill_early.png, premium_ring_fill_late.png,
    ///       premium_ring_conclude.png, premium_parry_flash.png + 로그 TestOutput/attack_premium_probe.log
    /// </summary>
    public static class AttackPremiumProbe
    {
        internal const string Tag = "[PremiumProbe]";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_10_TerritoryCombat.unity";
        internal const string SessionKey = "attack_premium_probe_active";
        private const int RTW = 1024;
        private const int RTH = 768;
        private const float SetupTimeoutSec = 40f;   // Test_10 부트(147차 실측 Awake 수 초) 대기 상한
        private const float RunnerStartTimeoutSec = 20f;
        private const float HeartbeatTimeoutSec = 12f;
        private const float OverallTimeoutSec = 180f;

        internal static int StepErrors;

        private enum Phase { WaitingPlay, WaitingSetup, StartingRunner, WaitingRunner, Finished }
        private static Phase _phase = Phase.WaitingPlay;
        private static float _phaseStartRealtime;
        private static float _runStartRealtime = -1f;
        private static float _lastHeartbeatRealtime = -1f;
        private static PremiumRunner _runner;
        private static bool _pumpSubscribed;

        public static void RunAll()
        {
            Debug.Log($"{Tag} === 공격/패링 프리미엄 배치 진단 프로브 시작 ===");
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
                        var go = new GameObject("PremiumRunner");
                        _runner = go.AddComponent<PremiumRunner>();
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
                      "(산출: Screenshots/premium_*.png)");
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

        // ── 캡처 헬퍼(IndoorTourProbe 선례 — RT+cam.Render+ReadPixels) ──
        internal static void CaptureFromCamera(Camera cam, string fileName)
        {
            if (cam == null)
            {
                FailStep($"캡처 실패 — 카메라 미발견: {fileName}");
                return;
            }
            var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            try
            {
                cam.Render();
                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;
                string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, fileName);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                int kb = (int)(new FileInfo(path).Length / 1024);
                UnityEngine.Object.DestroyImmediate(tex);
                Check($"CAPTURE {fileName} ({kb}KB)");
            }
            finally
            {
                cam.targetTexture = prevTarget;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>[무번호5] 진단 전용 카메라 — 팔로우 카메라 상태(억제/위치) 무관하게 대상을 확실히 담는 뷰.
        /// 파괴 책임은 호출자(런너) — enable=false로 두고 RT 캡처용 Render만 호출.</summary>
        internal static Camera CreateDiagCamera(Vector3 focus, Vector3 offset, string name)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main != null)
            {
                cam.CopyFrom(main);
                cam.cullingMask = main.cullingMask;
                cam.backgroundColor = main.backgroundColor;
                cam.clearFlags = main.clearFlags;
            }
            cam.transform.position = focus + offset;
            cam.transform.LookAt(focus);
            cam.enabled = false;
            cam.aspect = (float)RTW / RTH;
            return cam;
        }

        internal static Camera FindMainCamera()
        {
            Camera best = null;
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            {
                if (!cam.isActiveAndEnabled) continue;
                if (cam.CompareTag("MainCamera")) return cam;
                if (best == null) best = cam;
            }
            return best;
        }
    }

    /// <summary>[PremiumProbe] Play 모드 런너 — P0 덤프 → P1 링 캡처 → P2 패링 플래시 캡처.</summary>
    public class PremiumRunner : MonoBehaviour
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
                    Debug.LogError($"{AttackPremiumProbe.Tag} STEP_EXCEPTION {step}: {e.GetType().Name} {e.Message}");
                    AttackPremiumProbe.StepErrors++;
                    yield break;
                }
                yield return current;
            }
        }

        private IEnumerator Start()
        {
            Began = true;
            AttackPremiumProbe.NoteHeartbeat();
            for (int i = 0; i < 30; i++) yield return null;   // 부트 안정화

            yield return RunGuarded(StepP0SilhouetteDump(), "p0_silhouette");
            yield return RunGuarded(StepP1RingCapture(), "p1_ring");
            yield return RunGuarded(StepP2ParryFlash(), "p2_parry");

            AttackPremiumProbe.Finish();
        }

        private void Update() => AttackPremiumProbe.NoteHeartbeat();

        // ── P0: 플레이어 계층 머티리얼 전수 덤프 ──
        private IEnumerator StepP0SilhouetteDump()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                AttackPremiumProbe.FailStep("P0 — Player 미발견");
                yield break;
            }
            var cam = AttackPremiumProbe.FindMainCamera();
            AttackPremiumProbe.State($"P0 player={player.name} cam={Describe(cam)} pos={V3(player.transform.position)}");
            yield return null;

            var renderers = player.GetComponentsInChildren<Renderer>(true);
            int matTotal = 0, matNoTex = 0, matWhiteish = 0;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;
                string path = BuildPath(r.transform);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    matTotal++;
                    if (m == null)
                    {
                        Debug.Log($"{AttackPremiumProbe.Tag} MAT slot={i} path={path} mat=NULL(누락 — 흰 실루엣 후보)");
                        matNoTex++;
                        continue;
                    }
                    // [무번호5 정정] glTFast 그래프는 _BaseMap/_MainTex 외 프로퍼티명(baseColorTexture 등) 사용 —
                    //   셰이더 텍스처 프로퍼티 전수 검사로 정판정(1차 덤프의 NO_TEXTURE 오판 방지).
                    string shaderName = m.shader != null ? m.shader.name : "NULL_SHADER";
                    string texDump = "";
                    bool anyBound = false;
                    if (m.shader != null)
                    {
                        var parts = new System.Collections.Generic.List<string>();
                        int count = m.shader.GetPropertyCount();
                        for (int pi = 0; pi < count; pi++)
                        {
                            if (m.shader.GetPropertyType(pi) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                            string pn = m.shader.GetPropertyName(pi);
                            var tv = m.GetTexture(pn);
                            if (tv != null) anyBound = true;
                            parts.Add($"{pn}={(tv != null ? tv.name : "NULL")}");
                        }
                        texDump = string.Join(" ", parts);
                    }
                    if (m.mainTexture != null) anyBound = true;
                    Color baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                                    : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    bool whiteish = !anyBound && baseColor.r > 0.85f && baseColor.g > 0.85f && baseColor.b > 0.85f;
                    if (!anyBound) matNoTex++;
                    if (whiteish) matWhiteish++;
                    Debug.Log($"{AttackPremiumProbe.Tag} MAT path={path} slot={i} mat={m.name} shader={shaderName} " +
                              $"texProps=[{texDump}] color=({baseColor.r:F2},{baseColor.g:F2},{baseColor.b:F2},{baseColor.a:F2})" +
                              (whiteish ? " <== WHITE_CANDIDATE" : ""));
                }
            }
            AttackPremiumProbe.Check($"P0 요약 — renderers={renderers.Length} mats={matTotal} noTex={matNoTex} whiteish={matWhiteish}");
            yield return null;
            AttackPremiumProbe.CaptureFromCamera(cam, "premium_player_view.png");
            // [무번호5] 진단 전용 근접 뷰 추가 — 팔로우 카메라가 녹색 단색을 내놓는 경우 대비
            var diag = AttackPremiumProbe.CreateDiagCamera(player.transform.position + Vector3.up * 1.0f,
                new Vector3(0f, 3f, -4f), "PremiumDiagCam_Player");
            AttackPremiumProbe.CaptureFromCamera(diag, "premium_player_closeup.png");
            Destroy(diag.gameObject);
        }

        // ── P1: 프리미엄 텔레그래프 링 — 공용 Spawn 경로 그대로 ──
        private IEnumerator StepP1RingCapture()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                AttackPremiumProbe.FailStep("P1 — Player 미발견");
                yield break;
            }
            Vector3 spot = player.transform.position + player.transform.forward * 2.5f;
            var go = ProjectName.Systems.AttackTelegraphIndicator.Spawn(spot, 1.6f, 1.5f);
            if (go == null)
            {
                AttackPremiumProbe.FailStep("P1 — Spawn null(폴백 경로도 실패)");
                yield break;
            }
            AttackPremiumProbe.State($"P1 ring spawned at={V3(spot)}");

            // [무번호5] 진단 전용 카메라 — 팔로우 카메라 대신 링 지점을 확실히 담는 뷰(녹색 단색 프레임 원인 차단)
            var cam = AttackPremiumProbe.CreateDiagCamera(spot, new Vector3(0f, 7f, -7f), "PremiumDiagCam_Ring");

            yield return new WaitForSeconds(0.35f);   // fill 초기
            AttackPremiumProbe.CaptureFromCamera(cam, "premium_ring_fill_early.png");

            yield return new WaitForSeconds(0.95f);   // fill 후기(~1.3s)
            AttackPremiumProbe.CaptureFromCamera(cam, "premium_ring_fill_late.png");

            // 종료 플래시(1.5s 경과 후 0.12s 창) — 소거 타이밍 감안 1.62s 지점 캡처
            yield return new WaitForSeconds(0.32f);
            AttackPremiumProbe.CaptureFromCamera(cam, "premium_ring_conclude.png");

            // 자가 파괴 확인 — 종료 플래시(0.12s) 경과 후
            yield return new WaitForSeconds(0.3f);
            bool destroyed = go == null;
            AttackPremiumProbe.Check($"P1 자가파괴={destroyed}");
            if (!destroyed) AttackPremiumProbe.FailStep("P1 — 종료 후 자가 파괴 실패");
            Destroy(cam.gameObject);
        }

        // ── P2: 패링 화이트 프레임 — ParryFlashFader 직접 기동(진단 전용 지연 0.5s) ──
        private IEnumerator StepP2ParryFlash()
        {
            var main = AttackPremiumProbe.FindMainCamera();
            if (main == null)
            {
                AttackPremiumProbe.FailStep("P2 — 카메라 미발견");
                yield break;
            }
            // [무번호5] 진단 전용 카메라(플레이어 전방 3m) — 플래시 쿼드를 여기에 부착해 확정 뷰 확보
            var player2 = GameObject.FindGameObjectWithTag("Player");
            Vector3 focus2 = player2 != null
                ? player2.transform.position + player2.transform.forward * 3f
                : main.transform.position + main.transform.forward * 3f;
            var cam = AttackPremiumProbe.CreateDiagCamera(focus2, new Vector3(0f, 2f, -5f), "PremiumDiagCam_Flash");
            cam.aspect = main.aspect;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "PremiumProbe_ParryFlash";
            UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(cam.transform, false);
            // 풀스크린 커버 — 카메라 fov 60° 기준 근사, ZWrite Off 투명 머티리얼(Init이 구성)
            quad.transform.localPosition = new Vector3(0f, 0f, 1.0f);
            float halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.0f;
            float halfW = halfH * cam.aspect;
            quad.transform.localScale = new Vector3(halfW * 2f, halfH * 2f, 1f);

            var fader = quad.AddComponent<ProjectName.Systems.ParryFlashFader>();
            // 진단 전용: 지연 0.5s(프로덕션 0.07s 계약은 TelegraphPremiumTests 소스계약으로 검증 — 캡처 가독성 목적)
            fader.Init(new Color(1f, 1f, 1f, 1f), 0.85f, 0.5f);

            yield return new WaitForSeconds(0.10f);   // 피크 근방(전 25% 구간=0.125s 내)
            AttackPremiumProbe.CaptureFromCamera(cam, "premium_parry_flash.png");

            yield return new WaitForSeconds(0.6f);    // 자가 파괴 경과
            bool destroyed = quad == null;
            AttackPremiumProbe.Check($"P2 자가파괴={destroyed}");
            if (!destroyed)
            {
                Destroy(quad);
                AttackPremiumProbe.FailStep("P2 — 자가 파괴 실패(프로브가 정리)");
            }
            Destroy(cam.gameObject);
        }

        private static string Describe(UnityEngine.Object o) => o != null ? o.name : "null";
        private static string V3(Vector3 v) => v.ToString("F2", CultureInfo.InvariantCulture);

        private static string BuildPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            var p = t.parent;
            while (p != null && p != p.root)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }
    }

    /// <summary>[PremiumProbe] 리로드 재구독 부트 — SessionState 플래그(Cave146 선례).</summary>
    [InitializeOnLoad]
    internal static class AttackPremiumProbeBoot
    {
        static AttackPremiumProbeBoot()
        {
            if (!SessionState.GetBool(AttackPremiumProbe.SessionKey, false)) return;
            EditorApplication.delayCall += () => AttackPremiumProbe.ResubscribeAfterReload();
        }
    }
}
