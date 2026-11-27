using System;
using System.Globalization;
using System.IO;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI;
using ProjectName.UI.Toolkit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [IndoorTour] 실내 전 씬 투어 스크린샷 프로브 — 배치 Play 진단 전용(프로덕션 무수정).
    /// 목적:
    ///   ① 146차 실내 미니맵(IndoorMinimapBaker 훅 3곳)이 모든 실내 진입 경로에서 발화하는지 런타임 증거 수집.
    ///   ② 실내 전 씬(house/castle(플레이어+영주)/shop/restaurant/crafthouse/church/barn/
    ///      cave F1/F2/arena/afterlife/arenapit + tavern 직접빌드)의 게임뷰 + 미니맵 평면도 PNG 산출.
    /// 경로: Test_21 부트(동굴 F1) → GoDeeper(F2) → EnterBuilding 10종 순차 진입 → tavern 직접빌드(EnterBuilding case 부재 갭 진단).
    /// 산출: Screenshots/tour_&lt;label&gt;_view.png + tour_&lt;label&gt;_minimap.png (512² 평면도 = 미니맵 배경 그대로).
    /// 설계: 런너 MonoBehaviour 미사용 — EnterBuilding이 씬 언로드/리로드(P19)를 매번 일으켜 런너 소실 재발(실측 2회).
    ///   → Cave146 펌프 패턴을 상태 머신으로 확장(Phase×StepState), 코루틴/런너 의존 제거.
    ///   카메라 소실(세계 언로드 후 MainCamera 파괴) → 추적캠 근사 65° 피치 대체 뷰 폴백( cave146 CapturePlayerViewSubstitute 선례).
    /// 실행: run_indoor_tour.sh — -executeMethod ProjectName.EditorTools.IndoorTourProbe.RunAll
    ///   ⚠ -quit 금지(Play 배치 계약 — 프로브가 스스로 EditorApplication.Exit), -nographics 금지(RT 캡처).
    /// </summary>
    public static class IndoorTourProbe
    {
        internal const string Tag = "[IndoorTour]";
        internal const string SessionKey = "indoor_tour_probe_active";
        private const string ScenePath = "Assets/Scenes/TestScenes/Test_21_CaveInterior.unity";
        private const string ProbeCamPrefix = "IndoorTourProbe";
        private const int RTW = 1024;
        private const int RTH = 768;
        private const float SetupTimeoutSec = 20f;        // Test_21 부트(CaveRoom+Player) 대기 상한
        private const float OverallTimeoutSec = 600f;     // 투어 13엔트리 전체 감시
        private const int SettleFrames = 90;              // EnterBuilding additive 로드+리빌드 정착 대기(~3s)
        private const int SettleTimeoutFrames = 400;      // 정착 감시 상한
        private const int BootFrames = 30;                // Test_21 부트 안정화(동굴 F1 캡처 전)

        internal static int StepErrors;

        // ── 투어 스텝 정의(EnterBuilding 공용 경로 — 훅 1곳 자연 발화) ──
        private enum StepKind { BootF1, GoDeeper, EnterBuilding, TavernDirect }
        private struct TourStep
        {
            public string label;
            public StepKind kind;
            public string buildingType;
            public string nation;
            public bool playerOwned;
            public string territoryKey;
        }
        private static readonly TourStep[] Steps =
        {
            new TourStep { label = "cave_f1",       kind = StepKind.BootF1 },
            new TourStep { label = "cave_f2",       kind = StepKind.GoDeeper },
            new TourStep { label = "house",         kind = StepKind.EnterBuilding, buildingType = "house" },
            new TourStep { label = "castle_player", kind = StepKind.EnterBuilding, buildingType = "castle", nation = "Empire", playerOwned = true },
            new TourStep { label = "castle_lord",   kind = StepKind.EnterBuilding, buildingType = "castle", nation = "Empire", playerOwned = false, territoryKey = "indoor_tour_lord" },
            new TourStep { label = "shop",          kind = StepKind.EnterBuilding, buildingType = "shop" },
            new TourStep { label = "restaurant",    kind = StepKind.EnterBuilding, buildingType = "restaurant" },
            new TourStep { label = "crafthouse",    kind = StepKind.EnterBuilding, buildingType = "crafthouse" },
            new TourStep { label = "church",        kind = StepKind.EnterBuilding, buildingType = "church" },
            new TourStep { label = "barn",          kind = StepKind.EnterBuilding, buildingType = "barn" },
            new TourStep { label = "arena",         kind = StepKind.EnterBuilding, buildingType = "arena" },
            new TourStep { label = "afterlife",     kind = StepKind.EnterBuilding, buildingType = "afterlife" },
            new TourStep { label = "arenapit",      kind = StepKind.EnterBuilding, buildingType = "arenapit" },
            new TourStep { label = "tavern",        kind = StepKind.TavernDirect },
        };

        // ── 펌프 상태 머신(런너 없음 — 씬 언로드에도 생존) ──
        private enum Phase { WaitingPlay, WaitingSetup, Touring, Finished }
        private enum StepState { Enter, Wait, Capture }
        private static Phase _phase = Phase.WaitingPlay;
        private static StepState _stepState = StepState.Enter;
        private static int _stepIndex;
        private static int _entryFrame;
        private static float _phaseStartRealtime;
        private static float _runStartRealtime = -1f;
        private static bool _pumpSubscribed;
        private static Bounds _lastBounds;
        private static bool _hasLastBounds;

        /// <summary>배치 런 진입점 — Test_21 오픈 → Play 진입 요청(이후 펌프가 이어받음).</summary>
        public static void RunAll()
        {
            Debug.Log($"{Tag} === 실내 전 씬 투어 스크린샷 프로브 시작 ===");
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                SessionState.SetBool(SessionKey, true);   // Play 도메인 리로드 생존 — InitializeOnLoad가 펌프 재구독
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

        /// <summary>Play 도메인 리로드 후 펌프 재구독 — SessionState 플래그로 본 런만 이어받기.</summary>
        internal static void ResubscribeAfterReload()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            _runStartRealtime = (float)EditorApplication.timeSinceStartup;
            _pumpSubscribed = false;
            SubscribePump();
            Debug.Log($"{Tag} 펌프 재구독 — 도메인 리로드 후 이어받기(isPlaying={EditorApplication.isPlaying})");
        }

        private static void Pump()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();   // 배치 Play 루프 촉진(무해)
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
                        if (GameObject.Find("CaveRoom") != null &&
                            GameObject.FindGameObjectWithTag("Player") != null)
                        {
                            State($"setup room=CaveRoom player=Player frameCount={Time.frameCount}");
                            _phase = Phase.Touring;
                        }
                        else if (now - _phaseStartRealtime > SetupTimeoutSec)
                        {
                            FailFatal($"부트 타임아웃({SetupTimeoutSec:F0}s) — CaveRoom/Player 미발견");
                        }
                        break;
                    case Phase.Touring:
                        DriveTour();
                        break;
                    case Phase.Finished:
                        break;
                }
            }
            catch (Exception e)
            {
                FailFatal($"펌프 예외 — {e}");
            }
        }

        // ── 투어 구동 — Enter(진입 호출) → Wait(정착) → Capture(로그+캡처) 순환 ──
        private static void DriveTour()
        {
            if (_stepIndex >= Steps.Length)
            {
                Finish();
                return;
            }
            var step = Steps[_stepIndex];
            switch (_stepState)
            {
                case StepState.Enter:
                    State($"tour_entry {step.label} ({step.kind})");
                    try
                    {
                        InvokeStep(step);
                    }
                    catch (Exception e)
                    {
                        LogStepException(step.label, e);
                        _stepIndex++;
                        return;
                    }
                    _entryFrame = Time.frameCount;
                    _stepState = StepState.Wait;
                    break;

                case StepState.Wait:
                    int waited = Time.frameCount - _entryFrame;
                    if (waited >= SettleFrames || waited >= SettleTimeoutFrames)
                        _stepState = StepState.Capture;
                    break;

                case StepState.Capture:
                    try
                    {
                        CaptureOnly(step.label);
                    }
                    catch (Exception e)
                    {
                        LogStepException(step.label, e);
                        StepErrors++;
                    }
                    _stepIndex++;
                    _stepState = StepState.Enter;
                    break;
            }
        }

        private static void InvokeStep(TourStep step)
        {
            switch (step.kind)
            {
                case StepKind.BootF1:
                    // Test_21 부트가 동굴 F1을 이미 빌드 — BootFrames 정착 후 캡처만.
                    for (int i = 0; i < BootFrames; i++) EditorApplication.QueuePlayerLoopUpdate();
                    break;
                case StepKind.GoDeeper:
                    var room = GameObject.Find("CaveRoom");
                    if (room == null) throw new Exception("CaveRoom 미발견 — GoDeeper 불가");
                    CaveLevelDirector.GoDeeper(room);
                    break;
                case StepKind.EnterBuilding:
                    IndoorSceneTransition.EnterBuilding(
                        step.buildingType, step.nation, step.playerOwned, step.territoryKey);
                    break;
                case StepKind.TavernDirect:
                    // ⚠ 갭 진단: EnterBuilding에 "tavern" case 부재(실측 L309~409 전수) — 직접빌드 경로로만 진입 가능.
                    //   Systems 빌더 → UI 훅이 미발화(EnterBuilding 공용 경로 밖) → 미니맵 캡처는 프로브가 대행.
                    var tavern = TavernInteriorBuilder.BuildTavernInterior();
                    if (tavern == null) throw new Exception("TavernInteriorBuilder.BuildTavernInterior 실패");
                    IndoorMinimapBaker.CaptureFor(tavern);
                    var p = GameObject.FindGameObjectWithTag("Player");
                    if (p != null && TryBounds(tavern, out var b))
                    {
                        p.transform.position = new Vector3(b.center.x, 1.5f, b.center.z);
                        for (int i = 0; i < BootFrames; i++) EditorApplication.QueuePlayerLoopUpdate();
                    }
                    break;
            }
        }

        // ── 캡처 — 로그+bounds 변화 판정+뷰(RT)+미니맵 평면도 ──
        private static void CaptureOnly(string label)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            bool has = IndoorMinimapBaker.HasCapture;
            Bounds bounds = has ? IndoorMinimapBaker.LatestBounds : default;
            bool changed = _hasLastBounds && BoundsChanged(_lastBounds, bounds);
            var cam = FindPlayerCamera();
            State($"{label} hasCapture={has} bounds={(has ? BoundsStr(bounds) : "n/a")} " +
                  $"bounds_changed={(changed ? "true" : _hasLastBounds ? "false" : "first")} " +
                  $"player={V3(player != null ? player.transform.position : Vector3.zero)} cam={Describe(cam)} " +
                  $"floor={CaveLevelDirector.CurrentFloor}");
            if (changed) Check($"{label} bounds_changed=true — 훅 발화 확인(캡처 갱신)");

            // [판독용 부스트] 뷰 캡처도 탑다운 캡처와 동일하게 앰비언트 임시 상향(캡처 전용 — 복원 보장).
            //   근거: 성 무드 계약(46/48차)으로 실내가 어둡다 — 무드는 유지하되 판독 컷만 밝힌다.
            Color viewAmbientBefore = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.60f, 1f);
            try
            {
                if (cam != null) CaptureFromCamera(cam, $"tour_{label}_view.png");
                else CaptureSubstituteView(player, $"tour_{label}_view.png");
            }
            finally
            {
                RenderSettings.ambientLight = viewAmbientBefore;   // 복원
            }
            if (has) CaptureTopDownBounds(bounds, $"tour_{label}_topdown.png");   // 수조 구조 증명 컷

            if (has) SaveMinimapTexture($"tour_{label}_minimap.png");
            else
            {
                Check($"{label} minimap_texture=MISSING — 훅 미발화 정황(갭 증거)");
                StepErrors++;
            }
            _lastBounds = bounds;
            _hasLastBounds = has;
        }

        // ── 캡처 헬퍼(RT→PNG, Cave146 선례) ──
        internal static Camera FindPlayerCamera()
        {
            Camera best = null;
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
            {
                if (cam.name.StartsWith(ProbeCamPrefix, StringComparison.Ordinal)) continue;
                if (!cam.isActiveAndEnabled) continue;
                if (cam.targetTexture != null) continue;
                if (best == null || cam.CompareTag("MainCamera")) best = cam;
            }
            return best;
        }

        internal static void CaptureFromCamera(Camera cam, string fileName)
        {
            var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                int kb = ReadAndSave(rt, fileName);
                Captured(fileName, kb);
            }
            finally
            {
                cam.targetTexture = prev;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>세계 언로드 후 MainCamera 소실 폴백 — 실내 추적캠 근사(65° 피치 3/4뷰, 대체 카메라).</summary>
        private static void CaptureSubstituteView(GameObject player, string fileName)
        {
            Vector3 p = player != null ? player.transform.position : Vector3.zero;
            var go = new GameObject($"{ProbeCamPrefix}SubView");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
            float pitch = 65f * Mathf.Deg2Rad;
            const float dist = 12f;
            cam.transform.position = p + new Vector3(0f, dist * Mathf.Cos(pitch), -dist * Mathf.Sin(pitch));
            cam.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                int kb = ReadAndSave(rt, fileName);
                Captured(fileName, kb);
            }
            finally
            {
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>룸 전방위 ortho 탑다운 캡처 — 수조(디오라마) 구조 증명용(Cave146 CaptureTopDown 선례, ambient 임시 상향→복원).</summary>
        internal static void CaptureTopDownBounds(Bounds bounds, string fileName)
        {
            float orthoSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.1f;
            var go = new GameObject($"{ProbeCamPrefix}TopCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            cam.cullingMask = ~0;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            cam.transform.position = new Vector3(bounds.center.x, bounds.max.y + 15f, bounds.center.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographicSize = orthoSize;

            Color ambientBefore = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(0.9f, 0.9f, 0.95f, 1f);   // 판독용 임시 상향
            try
            {
                var rt = new RenderTexture(RTW, RTH, 24, RenderTextureFormat.ARGB32);
                try
                {
                    cam.targetTexture = rt;
                    cam.Render();
                    int kb = ReadAndSave(rt, fileName);
                    Captured(fileName, kb);
                }
                finally
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }
            finally
            {
                RenderSettings.ambientLight = ambientBefore;   // 복원
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void SaveMinimapTexture(string fileName)
        {
            var src = IndoorMinimapBaker.LatestTexture;
            if (src == null)
            {
                Check($"{fileName} 소스 텍스처 소실");
                StepErrors++;
                return;
            }
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.SetPixels32(src.GetPixels32());
            copy.Apply();
            string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, copy.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(copy);
            Captured(fileName, (int)(new FileInfo(path).Length / 1024));
        }

        private static int ReadAndSave(RenderTexture rt, string fileName)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string dir = Path.Combine(Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return (int)(new FileInfo(path).Length / 1024);
        }

        internal static bool TryBounds(GameObject room, out Bounds bounds)
        {
            bounds = default;
            var renderers = room != null ? room.GetComponentsInChildren<Renderer>(true) : null;
            if (renderers == null || renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private static bool BoundsChanged(Bounds a, Bounds b) =>
            Vector3.SqrMagnitude(a.min - b.min) > 0.0001f || Vector3.SqrMagnitude(a.max - b.max) > 0.0001f;

        // ── 로그/종료 ──
        internal static void State(string msg) => Debug.Log($"{Tag} STATE {msg}");
        internal static void Check(string msg) => Debug.Log($"{Tag} CHECK {msg}");
        internal static void Captured(string fileName, int kb) => Debug.Log($"{Tag} CAPTURE {fileName} ({kb}KB)");
        internal static void LogStepException(string step, Exception e) =>
            Debug.Log($"{Tag} STEP_EXCEPTION {step}: {e.GetType().Name} {e.Message}");
        internal static string Describe(UnityEngine.Object o) => o != null ? o.name : "null";
        internal static string V3(Vector3 v) => v.ToString("F2", CultureInfo.InvariantCulture);
        internal static string BoundsStr(Bounds b) =>
            $"min={V3(b.min)} max={V3(b.max)} size={V3(b.size)}";

        internal static void Finish()
        {
            bool ok = StepErrors == 0;
            Debug.Log($"{Tag} {(ok ? "DONE" : "DONE_ERRORS")} — steps={Steps.Length} errors={StepErrors} " +
                      $"(산출: Screenshots/tour_*.png)");
            SessionState.SetBool(SessionKey, false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static void FailFatal(string reason)
        {
            Debug.LogError($"{Tag} FAIL {reason} (steps={StepErrors})");
            SessionState.SetBool(SessionKey, false);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Play 도메인 리로드 후 펌프 재구독 부트 — Cave146ProbeBoot 선례.</summary>
    [InitializeOnLoad]
    internal static class IndoorTourProbeBoot
    {
        static IndoorTourProbeBoot()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(IndoorTourProbe.SessionKey, false))
                    IndoorTourProbe.ResubscribeAfterReload();
            };
        }
    }
}
