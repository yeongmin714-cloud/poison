using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ProjectName.Systems;
using System.IO;

namespace ProjectName.EditorTools
{
    /// <summary>
    /// [79-R19] 전보 비둘기 검증 배치 프로브 — 셀프 종료.
    ///
    /// 목적: Test_15_Livelihood에서 Test15PigeonBoot가 플레이 즉시 비둘기를 발화하는지,
    /// 비둘기가 날아와 도착(호버) → 디스폰 직후 전투로그 배너(WarNotificationUI)가 뜨는지 정량 판정.
    ///   Phase PLAY — 씬 15 진입 → Test15PigeonBoot 자가발화 → PigeonMessenger.Active 관측
    ///   관측: 스폰 확인 / 날개 뼈 flap 확인 / 비행 진행(플레이어로 수렴) / 도착·호버 / 디스폰 후 배너
    ///
    /// 판정 계약 (솔직 보고):
    ///   - 스폰 미발화 → "비둘기 미스폰" (부트/씬/플레이어 원인 로그 포함)
    ///   - 스폰됐지만 날개 뼈 미발견 → "날개 뼈 미발견 — 날갯짓 없음" (직선 비행만)
    ///   - 디스폰 후 배너 History에 "어제 전역 전보" 미발견 → "배너 미표시" (전보 유실 결함)
    ///   - 전부 충족 → "비둘기 배달 PASS"
    ///
    /// 안전 계약 (Fx79MagentaSwapProbe 승계): -executeMethod 태그 무장, 배치 전용,
    ///   파일 캡처 표준(RT 감지 금지), 요약 파일 기록 후 Exit.
    /// </summary>
    [InitializeOnLoad]
    public static class Fx79PigeonProbe
    {
        const string PhaseKey = "Fx79PigeonProbePhase";
        const string ScenePath = "Assets/Scenes/TestScenes/Test_15_Livelihood.unity";
        internal const string SummaryPath = "TestOutput/no79pigeon.txt";
        const string EntryTag = "Fx79PigeonProbe";

        static Fx79PigeonProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
        }

        static bool Armed
        {
            get
            {
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                    if (args[i] != null && args[i].Contains(EntryTag)) return true;
                return false;
            }
        }

        public static void Run()
        {
            if (!Armed) { Debug.LogWarning("[79P-PROBE] 미무장 — executeMethod 태그 불일치, 무시"); return; }
            if (Phase != 0) { Debug.LogWarning("[79P-PROBE] 이미 진행 중(phase=" + Phase + ") — 무시"); return; }
            Debug.Log("[79P-PROBE] Run — 씬 개봉: " + ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Phase = 1;
        }

        static int Phase { get { return SessionState.GetInt(PhaseKey, 0); } set { SessionState.SetInt(PhaseKey, value); } }

        static void Tick()
        {
            if (!Armed) return;
            int phase = Phase;
            if (phase == 1 && !EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.Log("[79P-PROBE] Play 진입 요청");
                    EditorApplication.isPlaying = true;
                }
            }
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!Armed) return;
            if (state == PlayModeStateChange.EnteredPlayMode && Phase < 2)
            {
                Phase = 2;
                var hostGo = new GameObject("Fx79PigeonProbeHost") { hideFlags = HideFlags.HideAndDontSave };
                hostGo.AddComponent<Fx79PigeonProbeHost>();
                Debug.Log("[79P-PROBE] 호스트 생성 — 비둘기 관측 시퀀스 개시");
            }
            else if (state == PlayModeStateChange.EnteredEditMode && Phase >= 3)
            {
                Phase = 0;
                Debug.Log("[79P-PROBE] FINISH — 편집모드 복귀, Exit(0)");
                EditorApplication.Exit(0);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode && Phase == 2)
            {
                Phase = 3;
            }
        }
    }

    /// <summary>플레이 모드 구동부 — 비둘기 스폰/비행/도착/디스폰/배너 관측.</summary>
    public class Fx79PigeonProbeHost : MonoBehaviour
    {
        const float MaxWait = 30f;          // 스폰+비행+도착+이륙+배너 총 예산
        const float CaptureInterval = 3f;   // 스크린샷 주기
        const float SampleInterval = 0.5f;  // 관측 주기

        float _t;
        float _nextSample;
        float _nextCapture;
        int _captures;
        bool _spawnSeen;
        bool _wingsFound;
        bool _flightProgressed;
        float _spawnTime;
        float _minDistToPlayer = float.MaxValue;
        Transform _leftWing, _rightWing;
        string _verdict = "미판정";
        readonly System.Text.StringBuilder _samples = new System.Text.StringBuilder();

        void Update()
        {
            _t += Time.deltaTime;

            // ── 스폰 관측 ──
            if (!_spawnSeen && PigeonMessenger.Active != null)
            {
                _spawnSeen = true;
                _spawnTime = Time.time;
                Debug.Log("[79P-PROBE] 비둘기 스폰 관측 — t=" + _spawnTime.ToString("F1"));
            }

            // ── 관측 루프 ──
            if (_t >= _nextSample)
            {
                _nextSample = _t + SampleInterval;
                Sample();
            }
            if (_t >= _nextCapture)
            {
                _nextCapture = _t + CaptureInterval;
                Capture();
            }

            // ── 종료 조건: 비둘기 디스폰(Active == null) 후 배너 확인 ──
            if (_spawnSeen && PigeonMessenger.Active == null && Time.time - _spawnTime > 3f)
            {
                Finish();
            }
            else if (_t >= MaxWait)
            {
                Finish();
            }
        }

        void Sample()
        {
            var pigeon = PigeonMessenger.Active;
            if (pigeon == null) return;

            // 날개 뼈 탐색(1회)
            if (!_wingsFound)
            {
                foreach (var tr in pigeon.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.name == "LeftWing") _leftWing = tr;
                    else if (tr.name == "RightWing") _rightWing = tr;
                }
                _wingsFound = _leftWing != null && _rightWing != null;
                if (_wingsFound)
                    Debug.Log("[79P-PROBE] 날개 뼈 발견 — LeftWing/RightWing (절차적 날갯짓 경로 활성)");
            }

            // 비행 진행 관측 — 플레이어로 수렴 거리
            // [79-R19-2] 플레이어 해석 — PlayerController.Instance 우선, 태그 폴백(씬 15 실측: SetupPlayer가 PlayerController 미부착)
            Transform playerTr = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            if (playerTr == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) playerTr = tagged.transform;
            }
            if (playerTr != null)
            {
                float dist = Vector3.Distance(pigeon.transform.position, playerTr.position);
                if (dist < _minDistToPlayer) _minDistToPlayer = dist;
                if (_minDistToPlayer < 4f) _flightProgressed = true;
            }

            // flap 증거 — 날개 뼈 localRotation.z 시간 경과 변화
            if (_leftWing != null)
            {
                float z = _leftWing.localRotation.eulerAngles.z;
                _samples.AppendLine("t=" + _t.ToString("F1") + " dist=" + (_minDistToPlayer).ToString("F1") + " wingZ=" + z.ToString("F1"));
            }
        }

        void Capture()
        {
            if (_captures >= 5) return;
            string path = "TestOutput/no79pigeon_" + _captures + ".png";
            ScreenCapture.CaptureScreenshot(path);
            _captures++;
            Debug.Log("[79P-PROBE] 캡처 #" + _captures + " — " + path);
        }

        void Finish()
        {
            // 배너 확인 — 디스폰 후 WarNotificationUI.History에 "어제 전역 전보"
            bool bannerShown = false;
            string bannerMsg = "";
            try
            {
                foreach (var h in WarNotificationUI.History)
                {
                    if (h.message != null && h.message.Contains("어제 전역 전보"))
                    {
                        bannerShown = true;
                        bannerMsg = h.message;
                        break;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[79P-PROBE] History 조회 실패: " + e.Message);
            }

            if (!_spawnSeen)
                _verdict = "비둘기 미스폰 — Test15PigeonBoot 미발화/플레이어 부재/GLB 부재 원인 로그 확인";
            else if (!_wingsFound)
                _verdict = "날개 뼈 미발견 — 날갯짓 없이 직선 비행만 (GLB 리그 노드명 확인 필요)";
            else if (!bannerShown)
                _verdict = "배너 미표시 — 전보 유실 결함 (디스폰 후 WarNotificationUI 미발화)";
            else if (!_flightProgressed)
                _verdict = "비둘기 스폰+배너 OK, 비행 수렴 미확인 (dist=" + _minDistToPlayer.ToString("F1") + ")";
            else
                _verdict = "비둘기 배달 PASS — 스폰/날개/비행수렴/디스폰 후 배너 전부 관측";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[79P-PROBE] " + _verdict);
            sb.AppendLine("spawnSeen=" + _spawnSeen + " wingsFound=" + _wingsFound + " flightProgressed=" + _flightProgressed);
            sb.AppendLine("minDistToPlayer=" + _minDistToPlayer.ToString("F1"));
            sb.AppendLine("bannerShown=" + bannerShown);
            sb.AppendLine("bannerMsg=" + (bannerMsg ?? "").Replace("\n", " | "));
            sb.AppendLine("captures=" + _captures);
            sb.AppendLine(_samples.ToString());
            File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", Fx79PigeonProbe.SummaryPath), sb.ToString());
            Debug.Log("[79P-PROBE] 요약 기록: " + Fx79PigeonProbe.SummaryPath);

            EditorApplication.isPlaying = false;
        }
    }
}
