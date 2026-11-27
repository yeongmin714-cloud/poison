using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectName.Core;
using ProjectName.Systems;
using ProjectName.UI.Toolkit;

namespace ProjectName.UI
{
    /// <summary>
    /// [실내 관람 모드] Test_23_IndoorTour 전용 — 57° 원근 걸어보기 투어([카메라통일] 영주 테스트씬 동일 시점).
    ///
    /// 계획서: docs/plans/2026-11-06_indoor-tour-walk-mode-plan.md §2.
    /// 순서 진실 소스: Assets/Editor/IndoorTourProbe.cs의 TourStep[] Steps(당일 14/14 실측) —
    ///   TourSequence는 그 라벨 순서와 1:1(이중 소스 금지 — IndoorTourModeTests.T1이 검증).
    ///   cave_f1=부트(BootF1, CaveInteriorTestSetup 경로), cave_f2=GoDeeper, tavern=직접빌드 스텝.
    ///
    /// 키: ] 다음 / [ 이전 — 14엔트리 링 랩어라운드. 휠 줌은 추적 거리 6~20m 조절(실내 클램프 계약 동일).
    /// 카메라: 부트 시 MainCamera 확보 + 원근 57°(드라이버 단일 기록자 — 영주 테스트씬과 동일 시점).
    ///   카메라 소유 계약: 드라이버가 CameraFollowSuppressed=true로 유일 기록자(구 초기 설계 "억제 세션 미생성" 주석은 폐기).
    ///
    /// 생존 설계(핵심): IndoorSceneTransition.MaybeUnloadWorldScene는 EnterBuilding마다 월드 씬을
    ///   무조건 언로드한다(실측 — IndoorTourProbe 주석 "러너 소실 재발 2회"). Test_23에 살던 씬 인스턴스는
    ///   첫 건물 진입에서 파괴되므로, 부트 완료 후 순회 상태/키/라벨을 DontDestroyOnLoad 드라이버 호스트로
    ///   이관한다. 씬 인스턴스는 부트 대기+카메라 구성+드라이버 스폰만 담당하고 은퇴(enabled=false).
    ///   Test_23 재로드 시(퇴출 경로) 새 씬 인스턴스가 카메라를 재구성하고 기존 드라이버에 인계 후 은퇴.
    ///
    /// 로그 프리픽스 [IndoorTour](ASCII) — 스텝 시작 TOUR_ENTRY / 완료 SETTLED.
    /// </summary>
    // [카메라 소유] 억제 게이트(CameraFollowSuppressed=true)로 PM 추적/팔로워를 수동화 — 기록자는 본 드라이버뿐.
    //   HandleCameraShake는 Update 페이즈(원점 복원) → LateUpdate 배치가 위상적으로 항상 이김(실행 순서 속성 불요).
    public sealed class IndoorTourMode : MonoBehaviour
    {
        public const string TourSceneName = "Test_23_IndoorTour";

        private const string LogTag = "[IndoorTour]";
        private const string CaveRoomName = "CaveRoom";
        private const string IndoorSceneName = "IndoorScene";   // IndoorSceneTransition의 실내 씬명(사적 const 동일값)
        private const float BootTimeoutSec = 25f;               // CaveInteriorTestSetup.Awake 부트 완료 대기 상한
        private const float CaveReentryTimeoutSec = 30f;        // 퇴출→Test_23 복귀 대기 상한
        private const float CaveRoomWaitSec = 10f;              // 재로드 씬의 CaveRoom 재빌드 대기 상한
        private const int SettleFrames = 90;                    // 프로브 SettleFrames 동일(전환 정착)
        private const int TavernSettleFrames = 30;              // 직접빌드 정착(스펙)
        // [카메라통일 57°] 관람 카메라도 전 씬 공통 각도 — 영주 테스트씬(Test_PlayerCastleInterior)과 동일 시점.
        //   기존 90° 탑다운은 11-06 관람 모드 초기 설계였으나 사용자 지시로 57° 원근 3/4뷰로 정정.
        //   수치는 IndoorCameraFollow 기본 경로와 1:1(Pitch 57/DefaultDist 11/클램프 6~20) — 이중 소스 금지 원칙상
        //   IndoorCameraFollow.Pitch가 단일 소스지만 Systems→UI 역참조 금지 계약으로 상수 동기(ResolvePitch와 동일 논리).
        internal const float TourPitchDeg = 57f;
        internal const float TourDefaultDistance = 11f;
        internal const float TourZoomMin = 6f;
        internal const float TourZoomMax = 20f;

        /// <summary>순회 시퀀스 14엔트리 — IndoorTourProbe.Steps 라벨 순서와 1:1(진실 소스. 수정 금지).</summary>
        public static readonly string[] TourSequence =
        {
            "cave_f1", "cave_f2", "house", "castle_player", "castle_lord",
            "shop", "restaurant", "crafthouse", "church", "barn",
            "arena", "afterlife", "arenapit", "tavern",
        };

        private static IndoorTourMode _driver;   // 생존 드라이버(DontDestroyOnLoad 호스트 소유)
        private Camera _tourCam;                 // 소유 카메라 참조(파괴 시 재탐색)
        private float _tourDistance = TourDefaultDistance;   // [카메라통일 57°] 관람 추적 거리 — 휠로 6~20m 조절
        private static bool _ownsCamera;         // 억제 게이트 점유 표식(ConfigureTourCamera static 경유 — OnDisable 원복 계약)
        private bool _isDriver;                  // 드라이버 인스턴스 표식(스포너가 AddComponent 직후 설정)
        private bool _busy;                      // 전환 코루틴 가드 — 진행 중 키 입력 무시
        private bool _stepFailed;                // 현재 스텝 실패 플래그(인덱스 유지 계약)
        private int _currentIndex;               // 초기 0 = cave_f1(부트 상태와 일치)

        // ================================================================
        // 순수 함수(테스트 대상 — IndoorTourModeTests T2/T3/T4)
        // ================================================================

        /// <summary>씬 게이트 순수 함수 — 투어 씬에서만 관람 로직이 깨어난다(타 씬 무동작 계약).</summary>
        public static bool IsTourScene(string sceneName) => sceneName == TourSceneName;

        /// <summary>다음 인덱스 — 14엔트리 링 랩어라운드.</summary>
        public static int NextIndex(int i) => WrapIndex(i + 1);

        /// <summary>이전 인덱스 — 14엔트리 링 랩어라운드.</summary>
        public static int PrevIndex(int i) => WrapIndex(i - 1);

        /// <summary>카메라 피치 결정 — 오버라이드(0 초과) 우선, 기본 57°(IndoorCameraFollow.Pitch 동일값 — 각도 v2).</summary>
        public static float ResolvePitch(float overrideDeg) => overrideDeg > 0f ? overrideDeg : 57f;

        private static int WrapIndex(int i)
        {
            int n = TourSequence.Length;
            return ((i % n) + n) % n;
        }

        private static int IndexOfLabel(string label)
        {
            for (int i = 0; i < TourSequence.Length; i++)
            {
                if (TourSequence[i] == label) return i;
            }
            return -1;
        }

        // ================================================================
        // 부트 — 씬 게이트 → 부트 대기 → 카메라 구성 → 드라이버 이관
        // ================================================================

        private void Awake()
        {
            // 씬 게이트 — 타 씬 무동작 계약(Test_19/21 선례). 관람 로직은 Test_23에서만 깨어난다.
            if (gameObject.scene.name != TourSceneName)
            {
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            if (_isDriver) return;   // 드라이버 — 부트/카메라 구성은 스폰자(씬 인스턴스)가 이미 수행
            StartCoroutine(BootAndHandOver());
        }

        private IEnumerator BootAndHandOver()
        {
            // 부트 대기 — CaveInteriorTestSetup.Awake가 CaveRoom+Player를 세울 때까지(25s 상한).
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < BootTimeoutSec)
            {
                if (GameObject.FindGameObjectWithTag("Player") != null &&
                    GameObject.Find(CaveRoomName) != null)
                    break;
                yield return null;
            }
            if (GameObject.FindGameObjectWithTag("Player") == null || GameObject.Find(CaveRoomName) == null)
                Debug.LogError($"{LogTag} 부트 타임아웃({BootTimeoutSec:F0}s) — Player/CaveRoom 미발견(순회 키는 계속 시도)");

            ConfigureTourCamera();

            // 생존 이관 — EnterBuilding마다 월드 언로드(P19 실측)가 씬 인스턴스를 파괴한다.
            //   순회 상태/키/라벨을 DontDestroyOnLoad 호스트로 옮긴다(프로브 러너 소실 교훈의 런타임 해법).
            if (_driver == null)
            {
                var host = new GameObject("IndoorTourDriver");
                var driver = host.AddComponent<IndoorTourMode>();   // Awake 게이트 — 생성 시점 활성 씬=Test_23 통과
                driver._isDriver = true;
                DontDestroyOnLoad(host);
                _driver = driver;
                Debug.Log($"{LogTag} 드라이버 호스트 가동 — DontDestroyOnLoad(씬 언로드 생존)");
            }
            else
            {
                Debug.Log($"{LogTag} 기존 드라이버 생존 확인 — 씬 인스턴스는 카메라 재구성만 수행");
            }

            enabled = false;   // 씬 인스턴스 은퇴 — Update/OnGUI/코루틴은 드라이버 소유(이중 입력 방지)
        }

        /// <summary>관람 카메라 구성 — MainCamera 확보 + 원근 57°([카메라통일] 영주 테스트씬 동일 시점).</summary>
        private static void ConfigureTourCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null)
            {
                Debug.LogWarning($"{LogTag} MainCamera 미발견 — 카메라 구성 스킵(CaveInteriorTestSetup.EnsureCamera 의존)");
                return;
            }
            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogWarning($"{LogTag} MainCamera에 Camera 컴포넌트 부재 — 카메라 구성 스킵");
                return;
            }
            cam.orthographic = false;             // [카메라통일 57°] 원근 3/4뷰 — 직교 탑다운 폐기
            var follow = camGO.GetComponent<IndoorCameraFollow>();
            if (follow == null)
                follow = camGO.AddComponent<IndoorCameraFollow>();
            follow.PitchOverrideDeg = -1f;        // [카메라통일 57°] 기본 57° 경로 — 탑다운 오버라이드 폐기(드라이버가 유일 기록자)
            follow.ZoomOrthographicSize = false;  // 휠은 관람 모드가 직접 처리(팔로워 억제 세션 중 이중 소비 방지)
            // [카메라 생존] EnterBuilding마다 월드(Test_23)가 언로드된다(실측 — 구 투어 cam=null 12엔트리).
            //   씬 소속 카메라는 언로드와 함께 파괴되고 셸 IndoorCamera는 전환부 우선순위 블록이
            //   비활성화하므로 실내 중 활성 카메라 0대 = 암흑 화면. 관람 카메라를 DDOL로 승격해
            //   전 실내 구간 구동 주체를 유지한다(메인 게임의 생존 카메라 구조와 동일한 결과).
            //   Test_21 EnsureCamera는 무수정 — 본 승격은 관람 모드(Test_23)에서만 수행.
            if (Application.isPlaying)
            {
                // [카메라 소유] PlayerMovement.ApplyFollowCamera는 LateUpdate "최종 적용" 계약으로 매 프레임
                //   오빗(피치 40~80 클램프 — 90 불가)을 덮어쓰고 HandleCameraShake는 저장 원점(0,0,0)으로
                //   localPosition 복원한다(실측 — 3중 기록자 충돌). DialogueCameraDirector 선례의 억제 게이트로
                //   소유권 획득: 억제 동안 PM 추적/팔로워 모두 수동 → 관람 모드가 유일 기록자
                //   (DefaultExecutionOrder 1000 — LateUpdate 최후). OnDisable에서 원복.
                PlayerMovement.CameraFollowSuppressed = true;
                _ownsCamera = true;
                UnityEngine.Object.DontDestroyOnLoad(camGO);
                Debug.Log($"{LogTag} 카메라 소유 — 억제 게이트 ON + DDOL 승격(월드 언로드 생존)");
            }
            Debug.Log($"{LogTag} 카메라 구성 — 원근 57°(영주 테스트씬 동일) + 휠 줌(거리 6~20m)");
        }

        /// <summary>
        /// 활성 카메라 원근 57° 재적용 — 월드 언로드로 Test_23 카메라가 파괴되면 실내 중 구동 주체가
        /// 셸 IndoorCamera(IndoorScene 소유)로 바뀐다(IndoorSceneTransition 후속16 우선순위
        /// 실측: MainCamera가 IndoorCameraFollow 보유 시에만 구동 주체 유지). 스텝 도착/주기 감시에서
        /// MainCamera 태그 → 셸 IndoorCamera 순으로 활성 카메라를 찾아 원근 57°를 다시 씌운다.
        /// arenapit(관전 세션 소유 카메라)은 제외 — 세션 계약 존중.
        /// </summary>
        private static void ReapplyTourCamera()
        {
            GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
            if (camGO == null || !camGO.activeInHierarchy || camGO.GetComponent<Camera>() == null)
            {
                GameObject shell = GameObject.Find("IndoorCamera");
                if (shell != null && shell.activeInHierarchy) camGO = shell;
            }
            if (camGO == null) return;   // 전환 공백 프레임 — 무음 스킵
            Camera cam = camGO.GetComponent<Camera>();
            if (cam == null) return;
            var follow = camGO.GetComponent<IndoorCameraFollow>();
            if (follow == null) follow = camGO.AddComponent<IndoorCameraFollow>();
            cam.orthographic = false;              // [카메라통일 57°] 원근 유지
            follow.ZoomOrthographicSize = false;   // 휠은 드라이버 LateUpdate 직접 처리(이중 소비 방지)
            follow.PitchOverrideDeg = -1f;         // 기본 57° 경로(탑다운 오버라이드 폐기)
        }

        // ================================================================
        // 프로브 훅 — 배치 투어 프로브 전용(프로덕션 무영향). IndoorTourProbe 선례 계약.
        // ================================================================

        /// <summary>현재 생존 드라이버(프로브 대상). 없으면 null — 프로브가 대기한다.</summary>
        public static IndoorTourMode ActiveDriver => _driver;

        /// <summary>전환 진행 중 여부(프로브 폴링용 — SETTLED 로그 대기의 구조적 대체).</summary>
        public bool IsBusy => _busy;

        /// <summary>프로브용 즉시 배치 — 코루틴 컨텍스트(Update~LateUpdate 사이)에서 캡처 전 호출.
        ///   HandleCameraShake의 원점 복원이 Update 페이즈라 미배치 상태로 cam.Render()하면 원점 스냅샷이 된다.</summary>
        public void DebugPlaceCameraNow()
        {
            if (_tourCam == null)
            {
                GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
                if (camGO != null) _tourCam = camGO.GetComponent<Camera>();
            }
            PlaceTourCamera();
        }

        /// <summary>프로브용 순회 기동 — GoTo 코루틴 기동(키 입력 없이 지정 인덱스로 전환).</summary>
        public void DebugGoToStart(int targetIndex)
        {
            if (_busy)
            {
                Debug.Log($"{LogTag} 프로브 요청 무시 — 전환 중(_busy)");
                return;
            }
            StartCoroutine(GoTo(targetIndex));
        }

        // ================================================================
        // 순회 입력/라벨 — 드라이버 전용(씬 인스턴스는 은퇴)
        // ================================================================

        private int _reapplyFrameCounter;   // 카메라 재적용 스로틀(120프레임 @60fps ≈ 2s)

        /// <summary>관람 카메라 배치 — 억제 게이트로 유일 기록자가 된 드라이버의 매 프레임 최종 적용.
        ///   arenapit(관전 세션)은 배치/휠 제외 — 세션 소유 존중. 플레이어 추적은 걷기 관람의 핵심.</summary>
        private void LateUpdate()
        {
            if (!_isDriver || !_ownsCamera) return;
            if (TourSequence[WrapIndex(_currentIndex)] == "arenapit") return;   // 관전 세션 존중
            PlaceTourCamera();
        }

        /// <summary>관람 카메라 배치 1회 수행 — LateUpdate 매 프레임 경로와 프로브 캡처 직전 경유 공용.
        ///   코루틴 컨텍스트는 Update직후~LateUpdate전(HandleCameraShake 원점 복원 직후 상태)이므로
        ///   배치 프레임 외 캡처는 반드시 본 메서드로 재배치 후 렌더해야 한다.</summary>
        private void PlaceTourCamera()
        {
            if (_tourCam == null)
            {
                GameObject camGO = GameObject.FindGameObjectWithTag("MainCamera");
                if (camGO == null) return;   // 전환 공백 프레임 — 무음 스킵
                _tourCam = camGO.GetComponent<Camera>();
                if (_tourCam == null) return;
            }

            // [카메라통일 57°] 휠 = 추적 거리 조절(6~20m) — 스크롤 위 = 확대(거리 감소, PM 동일 부호).
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _tourDistance = Mathf.Clamp(_tourDistance - scroll * 1.5f, TourZoomMin, TourZoomMax);

            // [카메라통일 57°] 57° 3/4뷰 배치 — IndoorCameraFollow 기본 경로와 동일 산식
            //   (플레이어+1.4m 앵커에서 피치 방향 후방 거리, LookRotation은 플레이어+1.2m 조준).
            //   수직 탑다운(+25m/Euler 90) 폐기 — 셸 지붕/벽이 시야에 들어오는 인형의 집 뷰.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            Vector3 pos = player.transform.position + new Vector3(0f, 1.4f, 0f)
                  + Quaternion.Euler(TourPitchDeg, 0f, 0f) * new Vector3(0f, 0f, -_tourDistance);
            _tourCam.transform.position = pos;
            _tourCam.transform.rotation = Quaternion.LookRotation(player.transform.position + new Vector3(0f, 1.2f, 0f) - pos);
        }

        private void OnDisable()
        {
            if (_isDriver && _ownsCamera)
            {
                PlayerMovement.CameraFollowSuppressed = false;   // 소유 해제 — 게이트 원복(Director End 선례)
                _ownsCamera = false;
            }
        }

        private void Update()
        {
            if (!_isDriver || _busy) return;
            if (++_reapplyFrameCounter % 120 == 0)
                ReapplyTourCamera();   // 전환부/셸이 카메라를 교체한 경우 원근 57° 유지(스로틀 감시)
            if (Input.GetKeyDown(KeyCode.RightBracket))
                StartCoroutine(GoTo(_currentIndex + 1));   // ] 다음
            else if (Input.GetKeyDown(KeyCode.LeftBracket))
                StartCoroutine(GoTo(_currentIndex - 1));   // [ 이전
        }

        /// <summary>좌상단 라벨 — CaveInteriorTestSetup.OnGUI 선례(_owned 게이트 → 드라이버 게이트).</summary>
        private void OnGUI()
        {
            if (!_isDriver) return;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            string label = TourSequence[WrapIndex(_currentIndex)];
            string suffix = _busy ? "   전환 중..." : string.Empty;
            GUI.Label(new Rect(12f, 104f, 720f, 24f),
                $"[실내 관람] 현재: {label}  ([ 이전 / ] 다음 / 휠 줌 6~20m){suffix}");
        }

        // ================================================================
        // 전환 코루틴 — 스텝 실행 → 도착 보정 → 인덱스 갱신
        // ================================================================

        private IEnumerator GoTo(int targetIndex)
        {
            if (_busy) yield break;
            _busy = true;
            int target = WrapIndex(targetIndex);
            string label = TourSequence[target];
            Debug.Log($"{LogTag} TOUR_ENTRY {label} (index {target})");
            _stepFailed = false;

            switch (label)
            {
                case "cave_f1":
                    yield return RunCaveF1Step();
                    break;
                case "cave_f2":
                    yield return RunCaveF2Step();
                    break;
                case "tavern":
                    yield return RunTavernStep();
                    break;
                default:
                    yield return RunBuildingStep(label);
                    break;
            }

            if (_stepFailed)
            {
                Debug.Log($"{LogTag} STEP_FAILED {label} — 인덱스 유지({_currentIndex}: {TourSequence[WrapIndex(_currentIndex)]})");
            }
            else
            {
                // 도착 보정 — ①실내 플래그 재개(퇴출/언로드 체인이 닫은 값을 실내 도착 시 원상 복구;
                //   Test_23 언로드 시 구 CaveInteriorTestSetup.OnDestroy가 IndoorActive=false로 닫는 실측 경로 보정)
                //   ②pit가 세운 관전 세션 잔존 해제(세션 생성 금지 계약 — 해제만 수행). pit 도착은 전환부 소유 그대로.
                if (label != "arenapit")
                {
                    UITransitionState.IndoorActive = true;
                    ClearStaleSpectatorSession();
                    ReapplyTourCamera();      // 월드 언로드/셸 IndoorCamera 교체 후 원근 57° 재적용([카메라통일 57°])
                    // [카메라통일 57°] 셸 비활성(DisableShellForTopDown) 폐기 — 57°에서는 MedievalShell 벽/지붕이
                    //   인형의 집 뷰의 일부로 필요하다(영주 테스트씬과 동일 조건). InteriorSun도 활성 유지.
                    if (_ownsCamera) PlayerMovement.CameraFollowSuppressed = true;   // ClearStale 원복 재점등 — 관람 소유 유지
                }
                _currentIndex = target;   // 실측 도착 라벨 = 계약 인덱스(성공 시에만 갱신)
                Debug.Log($"{LogTag} SETTLED {label} (index {_currentIndex})");
            }
            _busy = false;
        }

        /// <summary>건물 11종 — EnterBuilding 공용 경로(프로브 EnterBuilding 스텝 1:1). 훅/미니맵은 전환부 자연 발화(우회 금지).</summary>
        private IEnumerator RunBuildingStep(string label)
        {
            string buildingType = label;
            string nation = null;
            bool playerOwned = false;
            string territoryKey = null;
            if (label == "castle_player")
            {
                buildingType = "castle";
                nation = "Empire";
                playerOwned = true;
            }
            else if (label == "castle_lord")
            {
                buildingType = "castle";
                nation = "Empire";
                territoryKey = "indoor_tour_lord";   // 프로브 실측 인자(진실 소스)
            }

            try
            {
                IndoorSceneTransition.EnterBuilding(buildingType, nation, playerOwned, territoryKey);
            }
            catch (Exception e)
            {
                Debug.Log($"{LogTag} STEP_EXCEPTION {label}: {e.GetType().Name} {e.Message}");
                _stepFailed = true;
                yield break;
            }

            for (int i = 0; i < SettleFrames; i++)
                yield return null;   // 로드/리빌드 + 미니맵/수조 훅 자연 발화 대기(프로브 동일 — 우회 금지)
        }

        /// <summary>cave_f1 — 실내 중이면 퇴출→Test_23 복귀 대기 → CaveRoom 대기 → 폴백 재빌드 → 입구 스폰 → 정착.</summary>
        private IEnumerator RunCaveF1Step()
        {
            // (a) 건물 실내 중이면 퇴출 — 월드(Test_23) 재로드 완료까지 대기(30s 상한).
            if (IndoorSceneTransition.IsIndoor)
            {
                try
                {
                    IndoorSceneTransition.ExitBuilding();
                }
                catch (Exception e)
                {
                    Debug.Log($"{LogTag} STEP_EXCEPTION cave_f1(퇴출): {e.GetType().Name} {e.Message}");
                    _stepFailed = true;
                    yield break;
                }

                float t0 = Time.realtimeSinceStartup;
                while (IndoorSceneTransition.IsIndoor && Time.realtimeSinceStartup - t0 < CaveReentryTimeoutSec)
                    yield return null;
                while (Time.realtimeSinceStartup - t0 < CaveReentryTimeoutSec)
                {
                    Scene tour = SceneManager.GetSceneByName(TourSceneName);
                    if (tour.isLoaded && SceneManager.GetActiveScene().name == TourSceneName)
                        break;
                    yield return null;
                }
                if (IndoorSceneTransition.IsIndoor || SceneManager.GetActiveScene().name != TourSceneName)
                {
                    Debug.Log($"{LogTag} STEP_EXCEPTION cave_f1: 퇴출/씬 복귀 타임아웃({CaveReentryTimeoutSec:F0}s)");
                    _stepFailed = true;
                    yield break;
                }
            }

            // (b) CaveRoom 대기(10s) — 씬 리로드 시 CaveInteriorTestSetup.Awake가 재빌드한다(검증 경로).
            GameObject room = null;
            float t1 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t1 < CaveRoomWaitSec)
            {
                room = GameObject.Find(CaveRoomName);
                if (room != null) break;
                yield return null;
            }

            // (c) 폴백 — 부재 시 잔존 룸 파괴 후 직접 빌드(Test_21 부트와 동일 인자) + 캡처 대행.
            if (room == null)
            {
                Debug.Log($"{LogTag} CaveRoom 부재 — 잔존 룸 정리 후 BuildCaveInterior 직접 빌드(폴백)");
                DestroyLeftoverRoomRoots();
                try
                {
                    room = CaveInteriorBuilder.BuildCaveInterior("cave_test", 2);
                }
                catch (Exception e)
                {
                    Debug.Log($"{LogTag} STEP_EXCEPTION cave_f1(재빌드): {e.GetType().Name} {e.Message}");
                    _stepFailed = true;
                    yield break;
                }
                if (room != null)
                    IndoorMinimapBaker.CaptureFor(room);   // 직접빌드는 전환부 훅 우회 — 프로브 대행 경로 동일
            }
            if (room == null)
            {
                Debug.Log($"{LogTag} STEP_EXCEPTION cave_f1: 동굴 재빌드 실패");
                _stepFailed = true;
                yield break;
            }

            // (d) 플레이어 입구 스폰 — 퇴출 복귀 위치(_returnPosition) 덮어쓰기를 입구 좌표로 정정.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                player.transform.position = CaveInteriorBuilder.GetEntranceSpawnPosition(2);

            // (e) 정착.
            for (int i = 0; i < SettleFrames; i++)
                yield return null;
        }

        /// <summary>cave_f2 — 현재 cave_f1이면 GoDeeper 직후 정착, 아니면 cave_f1 경유 후 GoDeeper(단방향 계약).</summary>
        private IEnumerator RunCaveF2Step()
        {
            if (_currentIndex != IndexOfLabel("cave_f1") || GameObject.Find(CaveRoomName) == null)
            {
                yield return RunCaveF1Step();
                if (_stepFailed) yield break;
            }

            GameObject room = GameObject.Find(CaveRoomName);
            if (room == null)
            {
                Debug.Log($"{LogTag} STEP_EXCEPTION cave_f2: CaveRoom 미발견 — GoDeeper 불가");
                _stepFailed = true;
                yield break;
            }

            // Cave146 동일 경로 — 룸 파괴→다음 층 리빌드(미니맵 캡처 훅 발화)→스폰 텔레포트→토스트.
            try
            {
                CaveLevelDirector.GoDeeper(room);
            }
            catch (Exception e)
            {
                Debug.Log($"{LogTag} STEP_EXCEPTION cave_f2: {e.GetType().Name} {e.Message}");
                _stepFailed = true;
                yield break;
            }

            for (int i = 0; i < SettleFrames; i++)
                yield return null;
        }

        /// <summary>tavern — EnterBuilding에 case 부재(실측) → 프로브 TavernDirect 동일 직접빌드+캡처 대행+중심 스폰.</summary>
        private IEnumerator RunTavernStep()
        {
            // 실내 컨텍스트 보장 — 부트 직후 [ 랩어라운드 등 IndoorScene 미로드 케이스: 검증된 house 진입 경유.
            if (!SceneManager.GetSceneByName(IndoorSceneName).isLoaded)
            {
                Debug.Log($"{LogTag} IndoorScene 미로드 — house 경유 후 tavern 직접빌드(랩어라운드 엣지)");
                try
                {
                    IndoorSceneTransition.EnterBuilding("house", null, false, null);
                }
                catch (Exception e)
                {
                    Debug.Log($"{LogTag} STEP_EXCEPTION tavern(house 경유): {e.GetType().Name} {e.Message}");
                    _stepFailed = true;
                    yield break;
                }
                for (int i = 0; i < SettleFrames; i++)
                    yield return null;
            }

            // 위생 — 직접빌드는 전환부 룸 추적 밖(스택 방지; 프로브는 단발 투어라 불요했던 정리).
            DestroyLeftoverRoomRoots();

            GameObject tavern = null;
            try
            {
                tavern = TavernInteriorBuilder.BuildTavernInterior("default", 1);
            }
            catch (Exception e)
            {
                Debug.Log($"{LogTag} STEP_EXCEPTION tavern: {e.GetType().Name} {e.Message}");
                _stepFailed = true;
                yield break;
            }
            if (tavern == null)
            {
                Debug.Log($"{LogTag} STEP_EXCEPTION tavern: BuildTavernInterior 실패");
                _stepFailed = true;
                yield break;
            }

            IndoorMinimapBaker.CaptureFor(tavern);   // Systems→UI 훅 미발화 경로 — 프로브 동일 대행

            // 플레이어를 룸 bounds 중심(+1.2m)으로 — 전 렌더러 Encapsulate(프로브 TryBounds 동일 알고리즘).
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && TryEncapsulatedBounds(tavern, out Bounds bounds))
                player.transform.position = new Vector3(bounds.center.x, 1.2f, bounds.center.z);

            for (int i = 0; i < TavernSettleFrames; i++)
                yield return null;
        }

        // ================================================================
        // 헬퍼 — 잔존 정리/관전 세션 해제/bounds
        // ================================================================

        /// <summary>잔존 룸 정리 — 로드 씬 루트 중 이름이 Room으로 끝나는 오브젝트만 파괴(직접빌드 스택 방지).</summary>
        private static void DestroyLeftoverRoomRoots()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (!root.name.EndsWith("Room", StringComparison.Ordinal)) continue;
                    Debug.Log($"{LogTag} 잔존 룸 정리 — {scene.name}/{root.name}");
                    UnityEngine.Object.Destroy(root);
                }
            }
        }

        /// <summary>관전 세션 잔존 해제 — pit 진입분(억제 플래그+관전 락[DDOL 생존])을 pit 외 도착지에서만 해제.
        ///   세션 생성(=true 대입)은 금지 계약 — 해제만 수행한다.</summary>
        private static void ClearStaleSpectatorSession()
        {
            PlayerMovement.CameraFollowSuppressed = false;
            var staleLock = UnityEngine.Object.FindAnyObjectByType<ArenaSpectatorCameraLock>(FindObjectsInactive.Include);
            if (staleLock != null)
            {
                UnityEngine.Object.Destroy(staleLock);   // 컴포넌트만 — 호스트 카메라 보호
                Debug.Log($"{LogTag} 잔존 관전 카메라 락 해제 — ArenaSpectatorCameraLock");
            }
        }

        /// <summary>룸 전체 렌더러 bounds Encapsulate — 프로브 TryBounds 동일 알고리즘(런타임 복제).</summary>
        private static bool TryEncapsulatedBounds(GameObject room, out Bounds bounds)
        {
            bounds = default;
            var renderers = room != null ? room.GetComponentsInChildren<Renderer>(true) : null;
            if (renderers == null || renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
