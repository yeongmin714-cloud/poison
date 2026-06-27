using System.Collections;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.UI;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// T-Cycle-03: 영주 등장 이벤트 시퀀스.
    /// 
    /// 플레이어가 BuildingTrigger.ExitBuilding()을 통해 헛간에서 나온 후
    /// 최초 1회에 한해 영주 NPC 등장 이벤트를 재생합니다.
    ///
    /// Sequence steps:
    ///   1. SFX 문 두드림 (SoundManagerEnhanced.PlaySFX("knock"))
    ///   2. NPC 말풍선: "열어줘!..." (IMGUI 임시 텍스트, 2초)
    ///   3. E키로 문 열림 → 영주 NPC가 문 안으로 들어옴
    ///   4. 영주 대화: "아이고 배고파... 먹을 것 좀 없나?"
    ///   5. 동시에 살인명부(RevengeListWindow) 자동 팝업: "이 영주가 명부에 있다!"
    ///   6. 해당 영주(튜토리얼 영주) RevengeList에 하이라이트
    ///   7. 3초 후 자동 닫힘
    ///   8. TutorialGuideSystem.ShowGuide("tutorial_start") 호출 → T3 퀘스트로 연결
    /// </summary>
    public class TutorialLordSequence : MonoBehaviour
    {
        // ================================================================
        // 싱글톤
        // ================================================================

        private static TutorialLordSequence _instance;
        private static bool _applicationIsQuitting;

        public static TutorialLordSequence Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;

                if (_instance == null)
                {
                    var go = new GameObject("TutorialLordSequence");
                    _instance = go.AddComponent<TutorialLordSequence>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ================================================================
        // PlayerPrefs
        // ================================================================

        private const string PREFS_KEY = "TutorialLordSequence_Played";

        /// <summary>이 시퀀스가 이미 재생되었는지 확인합니다.</summary>
        public static bool HasPlayed => PlayerPrefs.HasKey(PREFS_KEY);

        /// <summary>시퀀스를 재생 완료 상태로 표시합니다.</summary>
        public static void MarkPlayed()
        {
            PlayerPrefs.SetInt(PREFS_KEY, 1);
            PlayerPrefs.Save();
        }

        // ================================================================
        // 설정
        // ================================================================

        [Header("시퀀스 설정")]
        [SerializeField, Tooltip("영주 NPC 프리팹 (LordPlaceholder 또는 기본 Cube)")]
        private GameObject _lordPrefab;

        [SerializeField, Tooltip("헛간 문 위치 (월드 좌표)")]
        private Vector3 _doorPosition = Vector3.zero;

        [SerializeField, Tooltip("영주 NPC 생성 위치 (문 밖)")]
        private Vector3 _lordSpawnPosition = new Vector3(0f, 0f, 5f);

        [SerializeField, Tooltip("영주 NPC가 문 안으로 들어온 후 위치")]
        private Vector3 _lordTargetPosition = new Vector3(0f, 0f, 1.5f);

        [SerializeField, Tooltip("말풍선 지속 시간 (초)")]
        private float _bubbleDuration = 2f;

        [SerializeField, Tooltip("영주 NPC 이동 속도")]
        private float _lordMoveSpeed = 2f;

        // ================================================================
        // 내부 상태
        // ================================================================

        private enum SequenceState
        {
            Idle,
            Step1_Knock,
            Step2_Bubble,
            Step3_WaitForE,
            Step4_LordEnters,
            Step5_Dialogue,
            Step6_RevengeList,
            Step7_Guide,
            Complete
        }

        private SequenceState _state = SequenceState.Idle;
        private GameObject _lordNpc;
        private string _bubbleText;
        private float _bubbleTimer;

        // IMGUI 스타일
        private GUIStyle _bubbleStyle;
        private GUIStyle _dialogueStyle;
        private bool _stylesInitialized;

        // IMGUI 배경 텍스처 (메모리 릭 방지: OnGUI 매 프레임 생성 금지)
        private Texture2D _bubbleBgTexture;

        /// <summary>시퀀스 진행 중인지 여부</summary>
        public bool IsPlaying => _state != SequenceState.Idle && _state != SequenceState.Complete;

        // ================================================================
        // MonoBehaviour 생명주기
        // ================================================================

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_state == SequenceState.Idle || _state == SequenceState.Complete)
                return;

            switch (_state)
            {
                case SequenceState.Step1_Knock:
                    // 자동으로 Step2로 전환
                    _state = SequenceState.Step2_Bubble;
                    _bubbleTimer = _bubbleDuration;
                    _bubbleText = "열어줘!...";
                    Debug.Log("[TutorialLordSequence] Step 2: 말풍선 표시");
                    break;

                case SequenceState.Step2_Bubble:
                    _bubbleTimer -= Time.deltaTime;
                    if (_bubbleTimer <= 0f)
                    {
                        _state = SequenceState.Step3_WaitForE;
                        _bubbleText = "[E] 문을 열어주기";
                        Debug.Log("[TutorialLordSequence] Step 3: E키 대기");
                    }
                    break;

                case SequenceState.Step3_WaitForE:
                    // E 키 입력 대기
                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        Debug.Log("[TutorialLordSequence] E키 입력 — 영주 NPC 진입");
                        _state = SequenceState.Step4_LordEnters;
                        _bubbleText = null;
                    }
                    break;

                case SequenceState.Step4_LordEnters:
                    // 영주 NPC가 문 안으로 들어오는 애니메이션 (이동)
                    if (_lordNpc != null)
                    {
                        Vector3 target = _lordTargetPosition;
                        _lordNpc.transform.position = Vector3.MoveTowards(
                            _lordNpc.transform.position, target, _lordMoveSpeed * Time.deltaTime);

                        if (Vector3.Distance(_lordNpc.transform.position, target) < 0.1f)
                        {
                            _lordNpc.transform.position = target;
                            _state = SequenceState.Step5_Dialogue;
                            _bubbleTimer = _bubbleDuration;
                            _bubbleText = "아이고 배고파... 먹을 것 좀 없나?";
                            Debug.Log("[TutorialLordSequence] Step 5: 영주 대화");
                        }
                    }
                    else
                    {
                        // NPC가 없으면 바로 다음 단계로
                        _state = SequenceState.Step5_Dialogue;
                        _bubbleTimer = _bubbleDuration;
                        _bubbleText = "아이고 배고파... 먹을 것 좀 없나?";
                    }
                    break;

                case SequenceState.Step5_Dialogue:
                    _bubbleTimer -= Time.deltaTime;
                    if (_bubbleTimer <= 0f)
                    {
                        // T-Cycle-04: TutorialRevengeListIntegration이 살인명부 표시/하이라이트/닫힘/가이드 호출을 처리
                        TutorialRevengeListIntegration.ShowRevengeListForTutorial();
                        _state = SequenceState.Complete;
                        MarkPlayed();
                        Debug.Log("[TutorialLordSequence] Step 5 완료 — TutorialRevengeListIntegration 호출, 시퀀스 완료");
                    }
                    break;
            }
        }

        // ================================================================
        // IMGUI OnGUI — 말풍선 및 대화 표시
        // ================================================================

        private void OnGUI()
        {
            if (_state == SequenceState.Idle || _state == SequenceState.Complete)
                return;

            InitializeStyles();

            if (!string.IsNullOrEmpty(_bubbleText))
            {
                DrawBubble();
            }
        }

        private void DrawBubble()
        {
            float screenW = Screen.width;
            float screenH = Screen.height;

            // 말풍선 위치: 화면 중앙 하단
            float bubbleW = 400f;
            float bubbleH = 60f;
            float bubbleX = (screenW - bubbleW) / 2f;
            float bubbleY = screenH - 150f;

            // 말풍선 배경 (캐싱된 텍스처 사용 — 메모리 릭 방지)
            if (_bubbleBgTexture != null)
                GUI.DrawTexture(new Rect(bubbleX, bubbleY, bubbleW, bubbleH), _bubbleBgTexture);

            // 말풍선 텍스트
            GUIStyle style;
            if (_state == SequenceState.Step2_Bubble || _state == SequenceState.Step3_WaitForE)
                style = _bubbleStyle;
            else
                style = _dialogueStyle;

            GUI.Label(new Rect(bubbleX + 20, bubbleY, bubbleW - 40, bubbleH), _bubbleText, style);
        }

        // ================================================================
        // 스타일 초기화
        // ================================================================

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _bubbleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.9f, 0.4f) } // 노란색
            };

            _dialogueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            // 배경 텍스처 — OnGUI 매 프레임 생성 방지를 위해 한 번만 생성
            _bubbleBgTexture = MakeTexture(1, 1, new Color(0f, 0f, 0f, 0.7f));

            _stylesInitialized = true;
        }

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 영주 등장 이벤트 시퀀스를 시작합니다.
        /// BuildingTrigger.ExitBuilding() 후에 호출됩니다.
        /// </summary>
        /// <param name="doorPosition">헛간 문 위치 (월드 좌표)</param>
        public void StartSequence(Vector3 doorPosition)
        {
            if (HasPlayed)
            {
                Debug.Log("[TutorialLordSequence] 이미 재생 완료됨 (PlayerPrefs) — 무시");
                return;
            }

            if (_state != SequenceState.Idle && _state != SequenceState.Complete)
            {
                Debug.LogWarning("[TutorialLordSequence] 시퀀스가 이미 실행 중입니다.");
                return;
            }

            _doorPosition = doorPosition;
            _bubbleText = null;
            _bubbleTimer = 0f;

            // 영주 NPC 생성
            SpawnLordNpc();

            // Step 1: 문 두드리는 SFX
            SoundManagerEnhanced.Instance?.PlaySFX("knock");
            Debug.Log("[TutorialLordSequence] Step 1: 문 두드리는 SFX 재생");

            _state = SequenceState.Step1_Knock;
        }

        /// <summary>
        /// 영주 등장 이벤트 시퀀스를 시작합니다 (기본 위치 사용).
        /// </summary>
        public void StartSequence()
        {
            StartSequence(_doorPosition);
        }

        /// <summary>
        /// 시퀀스를 초기 상태로 리셋합니다 (디버그용).
        /// </summary>
        public void ResetSequence()
        {
            // PlayerPrefs 초기화
            if (HasPlayed)
            {
                PlayerPrefs.DeleteKey(PREFS_KEY);
                PlayerPrefs.Save();
            }

            // 상태 초기화
            _state = SequenceState.Idle;
            _bubbleText = null;
            _bubbleTimer = 0f;

            // 영주 NPC 제거
            if (_lordNpc != null)
            {
                Destroy(_lordNpc);
                _lordNpc = null;
            }

            // 복수명부 리셋 (PlayerPrefs 포함)
            TutorialRevengeListIntegration.ResetShown();

            Debug.Log("[TutorialLordSequence] 시퀀스 리셋 완료 (디버그)");
        }

        // ================================================================
        // 내부 메서드
        // ================================================================

        /// <summary>영주 NPC 오브젝트를 생성합니다.</summary>
        private void SpawnLordNpc()
        {
            if (_lordPrefab != null)
            {
                _lordNpc = Instantiate(_lordPrefab, _lordSpawnPosition, Quaternion.identity);
                _lordNpc.name = "TutorialLord_NPC";
                DontDestroyOnLoad(_lordNpc);
            }
            else
            {
                // 프리팹이 없으면 기본 Cube로 대체
                _lordNpc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _lordNpc.name = "TutorialLord_NPC (Placeholder)";
                _lordNpc.transform.position = _lordSpawnPosition;
                _lordNpc.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);
                DontDestroyOnLoad(_lordNpc);

                // 색상 지정 (빨간색 — 영주 표시)
                var renderer = _lordNpc.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.8f, 0.1f, 0.1f);
                }

                Debug.Log("[TutorialLordSequence] LordPrefab 없음 — 기본 Capsule로 대체");
            }

            Debug.Log($"[TutorialLordSequence] 영주 NPC 생성 위치: {_lordSpawnPosition}");
        }

        // ================================================================
        // IMGUI 헬퍼
        // ================================================================

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var tex = new Texture2D(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return tex;
        }

        // ================================================================
        // 생명주기 정리
        // ================================================================

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            // 배경 텍스처 정리 (메모리 릭 방지)
            if (_bubbleBgTexture != null)
            {
                Destroy(_bubbleBgTexture);
                _bubbleBgTexture = null;
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }
    }
}